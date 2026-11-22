using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Drawing.Import;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 217: an AutoCAD DXF (2018, 2013, 2010) "never finishes, stuck at Reading the file". The import dialog
/// waits for the engine's summary (drafting.dxfInfo); whatever the file, it answers, or says why it cannot, in time.
/// </summary>
public class DxfReadingTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;

    /// <summary>A DXF as AutoCAD writes it: group codes right-aligned in three columns, CRLF line ends.</summary>
    static string AutoCad(string version, string entities, string blocks = "", string? codepage = null, int units = 4)
    {
        var pairs = new List<(int, string)> { (0, "SECTION"), (2, "HEADER"), (9, "$ACADVER"), (1, version) };
        if (codepage != null) pairs.AddRange([(9, "$DWGCODEPAGE"), (3, codepage)]);
        pairs.AddRange([(9, "$INSUNITS"), (70, units.ToString()), (0, "ENDSEC"), (0, "SECTION"), (2, "BLOCKS")]);
        var sb = new StringBuilder();
        void Put(IEnumerable<(int Code, string Value)> ps) { foreach (var (c, v) in ps) sb.Append(c.ToString().PadLeft(3)).Append("\r\n").Append(v).Append("\r\n"); }
        Put(pairs);
        sb.Append(blocks);
        Put([(0, "ENDSEC"), (0, "SECTION"), (2, "ENTITIES")]);
        sb.Append(entities);
        Put([(0, "ENDSEC"), (0, "SECTION"), (2, "OBJECTS"), (0, "DICTIONARY"), (5, "C"), (0, "XRECORD"), (5, "1F"), (1, "anything"), (0, "ENDSEC"), (0, "EOF")]);
        return sb.ToString();
    }

    static string Pairs(params (int Code, string Value)[] ps) => string.Concat(ps.Select(p => $"{p.Code,3}\r\n{p.Value}\r\n"));
    static string Line(double x0, double y0, double x1, double y1, string layer = "0") => Pairs((0, "LINE"), (8, layer), (10, N(x0)), (20, N(y0)), (11, N(x1)), (21, N(y1)));
    static string Insert(string block, double x = 0, double y = 0, int cols = 1, int rows = 1) =>
        Pairs((0, "INSERT"), (8, "0"), (2, block), (10, N(x)), (20, N(y)), (70, cols.ToString()), (71, rows.ToString()), (44, "2"), (45, "2"));
    static string Block(string name, string body) => Pairs((0, "BLOCK"), (8, "0"), (2, name), (70, "0"), (10, "0"), (20, "0")) + body + Pairs((0, "ENDBLK"), (8, "0"));
    static string Text(string value) => Pairs((0, "TEXT"), (8, "Popisy"), (10, "0"), (20, "-200"), (40, "100"), (1, value));
    static string N(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    static JsonObject Info(Fixture f, string plan, byte[] bytes) =>
        (JsonObject)f.Session.Query("drafting.dxfInfo", new JsonObject { ["viewId"] = plan, ["name"] = "test.dxf", ["data"] = Convert.ToBase64String(bytes) })!;

    [Theory]
    [InlineData("AC1032")]
    [InlineData("AC1027")]
    [InlineData("AC1024")]
    public void An_AutoCAD_DXF_from_a_Windows_folder_is_read_and_imported(string version)
    {
        var f = F();
        var plan = Plan(f);
        // A Czech user's folder: spaces and letters outside ASCII in the path; the file UTF-8 with CRLF, as AutoCAD 2007 and later write it.
        var dir = Path.Combine(Path.GetTempPath(), $"lapin-217 Výkresy {Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "Půdorys 1.NP.dxf");
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(AutoCad(version, Line(0, 0, 5000, 0, "Zdi") + Text("Příčka žluťoučký kůň"))));
            var info = (JsonObject)f.Session.Query("drafting.dxfInfo", new JsonObject { ["viewId"] = plan, ["path"] = path })!;
            Assert.Equal(version, (string)info["version"]!);
            Assert.Equal("mm", (string)info["units"]!["name"]!);
            Assert.Equal(1, (int)info["counts"]!["lines"]!);
            var r = f.Run("drafting.importDxf", new JsonObject { ["viewId"] = plan, ["path"] = path })!;
            Assert.Equal("Půdorys 1.NP.dxf", (string)r["file"]!);
            Assert.Equal(1, (int)r["texts"]!);
            Assert.Contains(f.Model.OfClass(Lapin.Model.IfcClass.Annotation), e => e.PredefinedType == "TEXT" && e.Str("text") == "Příčka žluťoučký kůň");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void A_2004_file_in_the_Czech_code_page_keeps_its_letters()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding(1250).GetBytes(AutoCad("AC1018", Text("Příčka"), codepage: "ANSI_1250"));
        var doc = DxfReader.Read(bytes);
        Assert.Equal("Příčka", Assert.Single(doc.Entities.OfType<DxfText>()).Value);
    }

    [Fact]
    public void A_byte_order_mark_does_not_hide_the_header()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(AutoCad("AC1032", Line(0, 0, 1, 0), units: 6))).ToArray();
        var doc = DxfReader.Read(bytes);
        Assert.Equal("AC1032", doc.Version);
        Assert.Equal(6, doc.Units);
    }

    [Fact]
    public void A_binary_DXF_a_DWG_and_a_file_that_is_no_DXF_are_refused_with_a_reason()
    {
        var f = F();
        var plan = Plan(f);
        var binary = Encoding.ASCII.GetBytes("AutoCAD Binary DXF\r\n\u001a\0").Concat(new byte[] { 0, 0, 7, 0, 1, 2 }).ToArray();
        Assert.Contains("binary DXF", Assert.Throws<CommandException>(() => Info(f, plan, binary)).Message);
        var dwg = Encoding.ASCII.GetBytes("AC1032").Concat(new byte[] { 0, 0, 0, 0, 0, 0x29, 0x1f, 0x9c }).ToArray();
        Assert.Contains("DWG", Assert.Throws<CommandException>(() => Info(f, plan, dwg)).Message);
        Assert.Contains("no DXF sections", Assert.Throws<CommandException>(() => Info(f, plan, Encoding.UTF8.GetBytes("<html>not a drawing</html>"))).Message);
        // The import says the same (it reads the file again).
        Assert.Throws<CommandException>(() => f.Run("drafting.importDxf", new JsonObject { ["viewId"] = plan, ["name"] = "b.dxf", ["data"] = Convert.ToBase64String(binary) }));
    }

    [Fact]
    public void Blocks_that_insert_themselves_end()
    {
        var f = F();
        var plan = Plan(f);
        var blocks = Block("A", Line(0, 0, 1, 1) + Insert("B", 1, 0) + Insert("A", 2, 2)) + Block("B", Insert("A", 0, 1));
        var info = Info(f, plan, Encoding.UTF8.GetBytes(AutoCad("AC1032", Insert("A"), blocks)));
        Assert.Equal(1, (int)info["counts"]!["lines"]!);
    }

    [Fact]
    public void Blocks_that_unfold_into_millions_are_refused_in_seconds_naming_the_block()
    {
        var f = F();
        var plan = Plan(f);
        // Each block inserts the one before twice: 2^30 lines from 30 blocks; and an array insert of 3,000 × 3,000.
        var nest = new StringBuilder(Block("L0", Line(0, 0, 1, 0)));
        for (var i = 1; i < 30; i++) nest.Append(Block($"L{i}", Line(0, 0, 1, 0) + Insert($"L{i - 1}") + Insert($"L{i - 1}", 1, 0)));
        var sw = Stopwatch.StartNew();
        var e = Assert.Throws<CommandException>(() => Info(f, plan, Encoding.UTF8.GetBytes(AutoCad("AC1032", Insert("L29"), nest.ToString()))));
        Assert.Contains("„L29”", e.Message);
        var m = Assert.Throws<CommandException>(() => Info(f, plan, Encoding.UTF8.GetBytes(AutoCad("AC1032", Insert("TILE", 0, 0, 3000, 3000), Block("TILE", Line(0, 0, 1, 0)))))).Message;
        Assert.Contains("„TILE”", m);
        Assert.True(sw.Elapsed.TotalSeconds < 30, $"{sw.Elapsed.TotalSeconds:0.0} s");
        // A big array within the limit is read.
        var ok = Info(f, plan, Encoding.UTF8.GetBytes(AutoCad("AC1032", Insert("TILE", 0, 0, 100, 100), Block("TILE", Line(0, 0, 1, 0)))));
        Assert.Equal(10_000, (int)ok["counts"]!["lines"]!);
    }

    [Fact]
    public void An_arc_with_absurd_angles_is_laid_and_the_reading_ends()
    {
        // 1e17 + 2π is 1e17: turns added one at a time never brought the sweep into range.
        Assert.InRange(DxfCurves.Arc(new(0, 0), 1, 1e17, 0).Count, 3, 80);
        Assert.InRange(DxfCurves.Ellipse(new(0, 0), new(1, 0), 0.5, 0, -1e20).Count, 5, 80);
        Assert.Equal(2 * Math.PI, DxfCurves.Sweep(double.NaN));
        Assert.Equal(2 * Math.PI, DxfCurves.Sweep(0));
        Assert.Equal(1.5 * Math.PI, DxfCurves.Sweep(-0.5 * Math.PI), 12);
        Assert.Equal(Math.PI, DxfCurves.Sweep(5 * Math.PI), 12);
        var f = F();
        var plan = Plan(f);
        var arc = Pairs((0, "ARC"), (8, "0"), (10, "0"), (20, "0"), (40, "100"), (50, "1e17"), (51, "-3e18"));
        var info = Info(f, plan, Encoding.UTF8.GetBytes(AutoCad("AC1032", arc)));
        Assert.Equal(1, (int)info["counts"]!["arcs"]!);
    }
}
