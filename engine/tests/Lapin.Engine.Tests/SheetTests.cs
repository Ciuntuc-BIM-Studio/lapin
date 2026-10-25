using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Sheets and their exports. Ported from the old Lapin's sheets.test.ts and
/// dxf-r2000.test.ts (same cases and numbers); the PDF and the composition
/// checked on a small house.
/// </summary>
public class SheetTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Sheet(Fixture f, string? paper = null) =>
        (string)f.Run("sheet.create", paper == null ? new JsonObject { ["name"] = "Plans" } : new JsonObject { ["name"] = "Plans", ["paper"] = paper })!["id"]!;

    [Fact]
    public void Paper_by_format_and_orientation_A1_landscape_by_default_and_numbers_count_on()
    {
        var f = F();
        var s = Sheet(f);
        Assert.Equal((841.0, 594.0), Sheets.PaperOf(f.Model.Get(s)!));
        Assert.Equal("A-101", f.Model.Get(s)!.Str("number"));
        f.Run("element.update", new JsonObject { ["id"] = s, ["attrs"] = new JsonObject { ["paper"] = "A3", ["orientation"] = "portrait" } });
        Assert.Equal((297.0, 420.0), Sheets.PaperOf(f.Model.Get(s)!));
        Assert.Equal("A-102", f.Model.Get(Sheet(f))!.Str("number"));
    }

    [Fact]
    public void Viewports_are_placed_moved_and_removed_and_a_view_sits_on_one_sheet()
    {
        var f = F();
        var s = Sheet(f);
        var plan = "plan:" + f.Storey;
        var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = plan, ["x"] = 200, ["y"] = 150 })!["viewportId"]!;
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = s, ["viewportId"] = vp, ["x"] = 250 });
        Assert.Equal(new SheetViewport(vp, plan, 250, 150), Sheets.Viewports(f.Model.Get(s)!).Single());
        Assert.Throws<CommandException>(() => f.Run("sheet.addViewport", new JsonObject { ["id"] = Sheet(f), ["viewId"] = plan }));
        f.Run("sheet.removeViewport", new JsonObject { ["id"] = s, ["viewportId"] = vp });
        Assert.Empty(Sheets.Viewports(f.Model.Get(s)!));
    }

    [Fact]
    public void The_box_fits_the_drawing_until_sized_by_hand()
    {
        // 20 m × 10 m at 1:100 is 200 × 100 mm, plus a 10 mm margin each side.
        Assert.Equal((220.0, 120.0), Sheets.Fit((new Vec2(0, 0), new Vec2(20, 10)), 100));
        var f = F();
        f.Wall(0, 0, 10, 0);
        var s = Sheet(f);
        var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = "plan:" + f.Storey, ["x"] = 200, ["y"] = 150 })!["viewportId"]!;
        var fitted = Sheets.Place(f.Session.Reader, f.Model.Get(s)!).Single();
        Assert.True(fitted.X1 - fitted.X0 > 100 + 20 - 1e-6);   // a 10 m wall at 1:100, and its margins
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = s, ["viewportId"] = vp, ["width"] = 150, ["height"] = 90 });
        var sized = Sheets.Place(f.Session.Reader, f.Model.Get(s)!).Single();
        Assert.Equal((150.0, 90.0), (sized.X1 - sized.X0, sized.Y1 - sized.Y0));
    }

    [Fact]
    public void A_sheet_is_drawn_with_its_frame_title_block_and_views_clipped_to_their_boxes()
    {
        var f = F();
        f.Wall(0, 0, 10, 0);
        f.Wall(10, 0, 10, 6);
        var s = Sheet(f, "A3");
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = s, ["fields"] = new JsonObject { ["drawnBy"] = "IC", ["project"] = "Casa Popescu" } });
        var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = "plan:" + f.Storey, ["x"] = 120, ["y"] = 120 })!["viewportId"]!;
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = s, ["viewportId"] = vp, ["width"] = 80, ["height"] = 80 });
        var page = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!);
        Assert.Equal((420.0, 297.0), (page.W, page.H));
        var texts = page.Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        Assert.Contains("Casa Popescu", texts);
        Assert.Contains("IC", texts);
        Assert.Contains("A-101", texts);
        Assert.Contains("1:100", texts);
        Assert.Contains("Ground floor", texts);   // the view's title
        // Everything the view draws stays in its 80 × 80 box.
        var cut = page.Pens.Where(p => p.Layer == "CUT").SelectMany(p => p.Segments).ToList();
        Assert.NotEmpty(cut);
        Assert.All(cut, x => Assert.True(Math.Min(x.A.X, x.B.X) >= 80 - 1e-9 && Math.Max(x.A.X, x.B.X) <= 160 + 1e-9 && Math.Min(x.A.Y, x.B.Y) >= 80 - 1e-9 && Math.Max(x.A.Y, x.B.Y) <= 160 + 1e-9));
        Assert.Contains(page.Pens, p => p.Layer == "FRAME");
    }

    [Fact]
    public void Sheets_print_to_a_pdf_page_each()
    {
        var f = F();
        f.Wall(0, 0, 6, 0);
        var a = Sheet(f, "A3");
        var b = Sheet(f, "A4");
        f.Run("sheet.addViewport", new JsonObject { ["id"] = a, ["viewId"] = "plan:" + f.Storey });
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = a, ["fields"] = new JsonObject { ["project"] = "Casă în Huși" } });
        var pdf = Sheets.Pdf(f.Session.Reader, [f.Model.Get(a)!, f.Model.Get(b)!]);
        var text = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("/Count 2", text);
        Assert.Contains($"/MediaBox [0 0 {420 * 72 / 25.4:0.###} {297 * 72 / 25.4:0.###}]", text);
        Assert.EndsWith("%%EOF\n", text);
        // The cross-reference table points at every object.
        var xref = int.Parse(Regex.Match(text, @"startxref\n(\d+)").Groups[1].Value);
        Assert.StartsWith("xref", text[xref..]);
    }

    // ------------------------------------------------------------ DXF R2000 (dxf-r2000.test.ts)

    [Fact]
    public void Colours_and_pen_widths_map_to_DXF_values()
    {
        Assert.Equal(7, DxfWriter.AciOf("#000000"));
        Assert.Equal(1, DxfWriter.AciOf("#e05050"));
        Assert.Equal(25, DxfWriter.LineWeight(0.25));
        Assert.Equal(70, DxfWriter.LineWeight(0.7));
        Assert.Equal(35, DxfWriter.LineWeight(0.33));
    }

    [Fact]
    public void Layers_carry_colour_line_type_and_weight_entities_are_bylayer_and_the_handle_seed_is_above_every_handle()
    {
        var w = new DxfWriter(100);
        var walls = w.Layer("A-WALL dims", "#e05050", 0.5, [3, 1.5]);
        w.Line(new(0, 0), new(1, 0), walls, "#e05050", 0.5, [3, 1.5]);
        w.Polyline([new(0, 0), new(1, 1), new(2, 0)], walls, true, "#0000ff", 0.5);
        w.Text(new(0, 0), "Notă", 2.5, walls);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = Encoding.GetEncoding(1252).GetString(w.Bytes("test"));
        Assert.Matches(@"\$ACADVER\n\s*1\nAC1015", text);
        Assert.Matches(@"\n\s*2\nA-WALL dims\n\s*70\n0\n\s*62\n1\n420\n14700624\n\s*6\nLAPIN1\n370\n50\n", text);
        Assert.Matches(@"\n\s*2\nLAPIN1\n[\s\S]*?\n\s*49\n300\n", text);   // dashes scaled to the view
        Assert.Matches(@"LINE\n[\s\S]*?\n\s*8\nA-WALL dims\n100\nAcDbLine", text);   // BYLAYER
        Assert.Matches(@"LWPOLYLINE\n[\s\S]*?\n\s*8\nA-WALL dims\n\s*62\n5\n", text);   // the blue one carries its colour
        Assert.Contains("Not\\U+0103", text);
        var body = text[text.IndexOf("ENDSEC", StringComparison.Ordinal)..];
        var handles = Regex.Matches(body, @"\n {2}5\n([0-9A-F]+)\n").Select(m => Convert.ToInt32(m.Groups[1].Value, 16)).ToList();
        var seed = Convert.ToInt32(Regex.Match(text, @"\$HANDSEED\n\s*5\n([0-9A-F]+)").Groups[1].Value, 16);
        Assert.True(handles.Count > 10 && handles.All(h => h < seed));
        Assert.Equal(handles.Count, handles.Distinct().Count());
    }

    [Fact]
    public void Layer_names_keep_their_letters_escaped_and_lose_commas_and_the_tables_count_their_records()
    {
        var w = new DxfWriter();
        var name = w.Layer("Zidărie, exterior", "#000000", 0.25, [3, 1.5]);
        w.Layer("Țiglă", "#000000", 0.5, [1, 1]);
        w.Line(new(0, 0), new(1, 0), name);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = Encoding.GetEncoding(1252).GetString(w.Bytes("test"));
        var layers = Regex.Matches(text, @"AcDbLayerTableRecord\n\s*2\n([^\n]*)\n").Select(m => m.Groups[1].Value).ToList();
        Assert.Contains("Zid\\U+0103rie_ exterior", layers);
        Assert.Contains("\\U+021Aigl\\U+0103", layers);
        Assert.All(layers, l => Assert.DoesNotMatch("[?,]", l));
        Assert.Matches(@"LINE\n[\s\S]*?\n\s*8\nZid\\U\+0103rie_ exterior\n", text);
        // Each table's 70 is the number of its records: the skeleton's and ours.
        int Count(string table) => int.Parse(Regex.Match(text, $@"\n\s*0\nTABLE\n\s*2\n{table}\n[\s\S]*?AcDbSymbolTable\n\s*70\n(\d+)\n").Groups[1].Value);
        int Records(string table) => Regex.Matches(text, $@"\n\s*0\n{table}\n\s*5\n").Count;
        Assert.Equal(Records("LTYPE"), Count("LTYPE"));
        Assert.Equal(5, Count("LTYPE"));
        Assert.Equal(Records("LAYER"), Count("LAYER"));
        Assert.Equal(4, Count("LAYER"));
    }

    [Fact]
    public void A_pdf_writes_no_NaN_for_a_point_that_is_not_a_number()
    {
        var pen = new Pen("CUT", "#000000", 0.5, null, null);
        pen.Segments.Add((new Vec2(double.NaN, 1), new Vec2(2, double.PositiveInfinity)));
        var pdf = PdfWriter.Write([new PaperPage(100, 100, [pen])]);
        var text = Encoding.Latin1.GetString(pdf);
        var at = text.IndexOf("stream\n", StringComparison.Ordinal) + 7;
        var length = int.Parse(Regex.Match(text, @"/Length (\d+) /Filter /FlateDecode").Groups[1].Value);
        using var z = new System.IO.Compression.ZLibStream(new MemoryStream(pdf, at, length), System.IO.Compression.CompressionMode.Decompress);
        var content = new StreamReader(z, Encoding.Latin1).ReadToEnd();
        Assert.Contains(" l", content);
        Assert.DoesNotContain("NaN", content);
        Assert.DoesNotContain("∞", content);
        Assert.DoesNotContain("Infinity", content);
    }

    [Fact]
    public void A_spreadsheet_keeps_as_text_what_a_number_would_change()
    {
        var bytes = XlsxWriter.Write([("S", [["Mark"], ["007"], ["+5"], ["1.5"], ["12"], ["1e3"], ["-0.25"]])]);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        using var r = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var sheet = r.ReadToEnd();
        Assert.Contains("<t xml:space=\"preserve\">007</t>", sheet);
        Assert.Contains("<t xml:space=\"preserve\">+5</t>", sheet);
        Assert.Contains("<t xml:space=\"preserve\">1e3</t>", sheet);
        Assert.Contains("<c r=\"A4\"><v>1.5</v></c>", sheet);
        Assert.Contains("<c r=\"A5\"><v>12</v></c>", sheet);
        Assert.Contains("<c r=\"A7\"><v>-0.25</v></c>", sheet);
    }

    [Fact]
    public void A_sheet_and_a_view_export_to_DXF()
    {
        var f = F();
        f.Wall(0, 0, 6, 0);
        var s = Sheet(f, "A3");
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = "plan:" + f.Storey });
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var sheet = Encoding.GetEncoding(1252).GetString(Sheets.Dxf(f.Session.Reader, f.Model.Get(s)!));
        Assert.Contains("\nCUT\n", sheet);
        Assert.Contains("\nFRAME\n", sheet);
        var view = Encoding.GetEncoding(1252).GetString(Views.Dxf(f.Session.Reader, Views.Find(f.Model, "plan:" + f.Storey)!));
        // At real size, in millimetres: the 6 m wall reaches x = 6000.
        Assert.Matches(@"\n 11\n6000\n", view);
    }

    /// <summary>With LAPIN_EXPORT_DIR set, a small house's sheet and plan are written there for outside readers (pdfinfo, ezdxf) to check.</summary>
    [Fact]
    public void Exports_for_outside_readers()
    {
        if (Environment.GetEnvironmentVariable("LAPIN_EXPORT_DIR") is not { Length: > 0 } dir) return;
        var f = F();
        f.Wall(0, 0, 10, 0); f.Wall(10, 0, 10, 6); f.Wall(10, 6, 0, 6); var w = f.Wall(0, 6, 0, 0);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = w, ["along"] = 3 });
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = 0, ["y1"] = -1, ["x2"] = 0, ["y2"] = 7 });
        f.Run("drafting.addDimension", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["a"] = new JsonArray(0, -0.5), ["b"] = new JsonArray(10, -0.5), ["at"] = new JsonArray(5, -1.5) });
        var s = Sheet(f, "A3");
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = "plan:" + f.Storey, ["x"] = 130, ["y"] = 130 });
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = s, ["fields"] = new JsonObject { ["project"] = "Casă în Huși", ["drawnBy"] = "IC" } });
        File.WriteAllBytes(Path.Combine(dir, "sheet.pdf"), Sheets.Pdf(f.Session.Reader, [f.Model.Get(s)!]));
        File.WriteAllBytes(Path.Combine(dir, "sheet.dxf"), Sheets.Dxf(f.Session.Reader, f.Model.Get(s)!));
        File.WriteAllBytes(Path.Combine(dir, "plan.dxf"), Views.Dxf(f.Session.Reader, Views.Find(f.Model, "plan:" + f.Storey)!));
    }

    [Fact]
    public void A_view_past_the_frame_or_over_the_title_block_is_flagged()
    {
        var f = new Fixture();
        f.Wall(0, 0, 5, 0);
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["paper"] = "A3" })!["id"]!;
        var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = "plan:" + f.Storey, ["x"] = 100, ["y"] = 100 })!["viewportId"]!;
        JsonArray Warnings() => ((JsonObject)f.Session.Query("sheet.drawing", new JsonObject { ["id"] = sheet })!)["viewports"]![0]!["warnings"]!.AsArray();
        Assert.Empty(Warnings());
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vp, ["x"] = 5, ["y"] = 100 });
        Assert.Contains("outside", Warnings().Select(x => (string)x!));
        // Over the title block (an A3 landscape's: x 220–410, y 237–287).
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vp, ["x"] = 330, ["y"] = 262 });
        Assert.Contains("titleBlock", Warnings().Select(x => (string)x!));
    }
}
