using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde;
using Lapin.Platform.Documents;

namespace Lapin.Engine.Tests;

/// <summary>
/// Title blocks of the user's own (docs/plan-title-blocks.md, decision 340): the project's data and parameters, live text, the
/// template anchored in its corner on any paper, Lapin standard drawing exactly as before, the PDF, the DXF block with its
/// attributes, IFC's Lapin_ProjectInformation both ways, the commands.
/// </summary>
public sealed class TitleBlockTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-title-blocks").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Sheet(Fixture f, string paper = "A3", string name = "Plans", bool portrait = false) =>
        (string)f.Run("sheet.create", new JsonObject { ["name"] = name, ["paper"] = paper, ["orientation"] = portrait ? "portrait" : "landscape" })!["id"]!;

    static JsonObject Template(double w, double h, string anchor, params JsonNode[] items) =>
        new() { ["w"] = w, ["h"] = h, ["anchor"] = anchor, ["items"] = new JsonArray(items) };

    static JsonNode Text(double x, double y, string text, double h = 2.5) =>
        new JsonObject { ["type"] = "text", ["at"] = new JsonArray(x, y), ["text"] = text, ["h"] = h };

    static string Create(Fixture f, JsonObject template, string name = "Finnish") =>
        (string)f.Run("titleBlock.create", new JsonObject { ["name"] = name, ["template"] = template })!["id"]!;

    static List<string> Texts(Fixture f, string sheet) => Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();

    static string PensHash(Fixture f, string sheet)
    {
        var sb = new StringBuilder();
        // The viewports' ids aside (new ones each run).
        foreach (var pen in Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!).Pens) sb.Append(Regex.Replace(pen.ToJson().ToJsonString(), "\"viewport\":\"[^\"]*\",", "")).Append('\n');
        return CdeTransmittal.Sha256(sb.ToString());
    }

    [Fact]
    public void Live_text_fills_tokens_from_the_project_the_sheet_and_the_system()
    {
        var f = F();
        f.Run("project.setInfo", new JsonObject { ["name"] = "Casa Popescu", ["number"] = "24/2026", ["designer"] = "Ciuntuc BIM Studio SRL", ["designerRegistration"] = "J2025031471009", ["author"] = "IC" });
        f.Run("project.setParameter", new JsonObject { ["key"] = "permitAuthority", ["label"] = "Permit authority", ["group"] = "site", ["value"] = "Primăria Huși" });
        f.Run("project.setParameter", new JsonObject { ["key"] = "handover", ["type"] = "date", ["value"] = "2026-12-01" });
        var a = Sheet(f, name: "Ground floor plan");
        Sheet(f, name: "Sections");
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = a, ["fields"] = new JsonObject { ["revision"] = "P02", ["muutos"] = "B" } });
        var tb = Create(f, Template(120, 40, "bottomRight",
            Text(2, 30, "Project: {project.name} · {project.number}"),
            Text(2, 25, "{project.designer}, {project.designerRegistration}"),
            Text(2, 20, "{project.permitAuthority} / {project.handover}"),
            Text(2, 15, "{sheet.number} {sheet.name} rev {sheet.revision} drawn {sheet.drawnBy}"),
            Text(2, 10, "Sheet {sheet.index} of {sheet.count} · {sheet.paper} · {sheet.size} · muutos {sheet.muutos}"),
            Text(2, 5, "{system.app} {system.printDate}")));
        f.Run("sheet.setTitleBlock", new JsonObject { ["id"] = a, ["titleBlockId"] = tb });
        var texts = Texts(f, a);
        Assert.Contains("Project: Casa Popescu · 24/2026", texts);
        Assert.Contains("Ciuntuc BIM Studio SRL, J2025031471009", texts);
        Assert.Contains("Primăria Huși / 01.12.2026", texts);
        Assert.Contains("A-101 Ground floor plan rev P02 drawn IC", texts);
        Assert.Contains("Sheet 1 of 2 · A3 · 420 × 297 mm · muutos B", texts);
        Assert.Contains($"Lapin {DateTime.Now:dd.MM.yyyy}", texts);
        // Documents read the user's own parameters by the same key.
        Assert.Contains(new CoreDocumentFields().Fields(f.Session.Reader, new DocFormat("ro", "mm")).ToList(), x => x.Key == "project.permitAuthority" && x.Value == "Primăria Huși");
    }

    [Fact]
    public void A_parameter_nobody_has_prints_as_a_placeholder_and_the_check_says_so_an_empty_one_prints_nothing()
    {
        var f = F();
        var s = Sheet(f);
        var tb = Create(f, Template(100, 30, "bottomRight", Text(2, 20, "Client: {project.client}|"), Text(2, 10, "Permit {project.permitNumbr}")));
        f.Run("titleBlock.setDefault", new JsonObject { ["id"] = tb });
        var texts = Texts(f, s);
        Assert.Contains("Client: |", texts);
        Assert.Contains("Permit <project.permitNumbr>", texts);
        Assert.Equal(["project.permitNumbr"], TitleBlocks.Unknown(f.Session.Reader, f.Model.Get(s)!));
        var findings = CdeChecks.Builtin.Single(r => r.Id == "titleBlock").Run(new CdeCheckContext(f.Session.Reader, [f.Model.Get(s)!], null, CdeNaming.Of(f.Model))).ToList();
        Assert.Contains(findings, x => x.Message.Contains("project.permitNumbr"));
        Assert.Equal("Client: Popescu|", TitleBlocks.Fill("Client: {project.client}|", k => (true, "Popescu")));
        Assert.Equal(["project.client", "sheet.number"], TitleBlocks.Tokens("{project.client} – {{sheet.number}}"));
    }

    [Fact]
    public void A_template_sits_in_its_corner_of_the_frame_on_every_paper()
    {
        var f = F();
        var frame = new JsonObject { ["left"] = 20, ["top"] = 10, ["right"] = 10, ["bottom"] = 10 };
        var t = Template(100, 40, "bottomRight", Text(5, 5, "corner"));
        t["frame"] = frame;
        var tb = Create(f, t);
        f.Run("titleBlock.setDefault", new JsonObject { ["id"] = tb });
        foreach (var (paper, portrait, w, h) in new[] { ("A3", false, 420.0, 297.0), ("A1", false, 841.0, 594.0), ("A3", true, 297.0, 420.0) })
        {
            var s = Sheet(f, paper, portrait: portrait);
            var page = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!);
            var at = page.Pens.SelectMany(p => p.Texts).Single(x => x.Text == "corner").At;
            Assert.Equal(w - 10 - 100 + 5, at.X, 6);
            Assert.Equal(h - 10 - 5, at.Y, 6);
            var l = TitleBlocks.Layout(f.Model, f.Model.Get(s)!);
            Assert.Equal((20.0, 10.0, w - 10, h - 10), l.Frame);
            Assert.Equal((w - 110, h - 50, w - 10, h - 10), l.Block);
            // The frame drawn at its own margins; a schedule's page the frame less the block.
            Assert.Contains(page.Pens.Where(p => p.Layer == "FRAME").SelectMany(p => p.Segments), sg => sg.A.X == 20 && sg.A.Y == 10 && sg.B.X == w - 10);
            Assert.Equal((w - 30 - 4, h - 20 - 40 - 4), Sheets.FrameSize(f.Model, f.Model.Get(s)!));
        }
        // Top-left: the box at the frame's top-left corner, y up from its bottom.
        f.Run("titleBlock.update", new JsonObject { ["id"] = tb, ["template"] = Template(100, 40, "topLeft", Text(5, 5, "corner")) });
        var a1 = Sheet(f, "A1");
        Assert.Equal((10.0 + 5, 10.0 + 40 - 5), (Sheets.Compose(f.Session.Reader, f.Model.Get(a1)!).Pens.SelectMany(p => p.Texts).Single(x => x.Text == "corner").At is var p ? (p.X, p.Y) : default));
    }

    [Fact]
    public void A_sheet_from_before_draws_exactly_as_it_did_with_Lapin_standard()
    {
        // The hashes of the pens and the DXF as main drew them before title blocks of the user's own.
        var f = F();
        f.Wall(0, 0, 10, 0);
        f.Wall(10, 0, 10, 6);
        var expected = new Dictionary<string, (string Pens, string Dxf)>
        {
            ["A3"] = ("a8343dd727de72b2bc6e13ea06b0e072f88e44f422587874a610fb2bbe3e3859", "80112a7edcd9cf7d84b75b975bd26b64a004d5bccb5c8a033030ddc9c59028dd"),
            ["A1"] = ("42d3135f508e15da7ad7e08c41e4eaddc8bc223e50834de6d341d70c1a8374ad", "42688974fba3e13118b845bdb4d8e2ab8630e95ce6e36311b70db297eed94478"),
        };
        foreach (var paper in new[] { "A3", "A1" })
        {
            var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = paper })!["id"]!;
            f.Run("sheet.titleBlock", new JsonObject { ["id"] = s, ["fields"] = new JsonObject { ["drawnBy"] = "IC", ["project"] = "Casa Popescu", ["date"] = "01.11.2026", ["revision"] = "P01" } });
            if (paper == "A3") f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = "plan:" + f.Storey, ["x"] = 120, ["y"] = 120 });
            Assert.Equal(BuiltinTitleBlocks.Standard, TitleBlocks.AssignedId(f.Model, f.Model.Get(s)!));
            Assert.Equal(expected[paper].Pens, PensHash(f, s));
            Assert.Equal(expected[paper].Dxf, CdeTransmittal.Sha256(Encoding.Latin1.GetString(Sheets.Dxf(f.Session.Reader, f.Model.Get(s)!))));
            // Named as the default explicitly, the same.
            f.Run("sheet.setTitleBlock", new JsonObject { ["id"] = s, ["titleBlockId"] = BuiltinTitleBlocks.Standard });
            Assert.Equal(expected[paper].Pens, PensHash(f, s));
        }
    }

    [Fact]
    public void Lapin_standard_duplicated_draws_the_same_fields_as_items()
    {
        var f = F();
        var s = Sheet(f);
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = s, ["fields"] = new JsonObject { ["drawnBy"] = "IC", ["project"] = "Casa Popescu", ["date"] = "01.11.2026", ["revision"] = "P01", ["status"] = "S2" } });
        var before = Texts(f, s);
        var copy = (string)f.Run("titleBlock.create", new JsonObject { ["from"] = BuiltinTitleBlocks.Standard })!["id"]!;
        Assert.Equal("Lapin standard copy", f.Model.Get(copy)!.Name);
        f.Run("sheet.setTitleBlock", new JsonObject { ["id"] = s, ["titleBlockId"] = copy });
        var after = Texts(f, s);
        // The same texts, the scale aside (Lapin standard prints — for none, the copy nothing).
        Assert.Equal(before.Where(t => t != "—" && t.Length > 0).OrderBy(t => t), after.Where(t => t.Length > 0).OrderBy(t => t));
        Assert.Equal(TitleBlocks.Layout(Sheets.PaperOf(f.Model.Get(s)!), null).Block, TitleBlocks.Layout(f.Model, f.Model.Get(s)!).Block);
    }

    [Fact]
    public void The_pdf_and_the_dxf_carry_the_values_the_dxf_as_a_block_with_attributes()
    {
        var f = F();
        f.Run("project.setInfo", new JsonObject { ["name"] = "Kesämökki", ["designer"] = "Arkkitehdit Oy" });
        var s = Sheet(f, "A1");
        var tb = Create(f, Template(180, 40, "bottomRight",
            new JsonObject { ["type"] = "rect", ["x"] = 0, ["y"] = 0, ["w"] = 180, ["h"] = 40, ["stroke"] = 0.5 },
            Text(3, 30, "Rakennuskohde", 1.8), Text(3, 22, "{project.name}", 4), Text(3, 10, "Suunnittelija: {project.designer}"), Text(120, 10, "{sheet.number}", 5)));
        f.Run("sheet.setTitleBlock", new JsonObject { ["id"] = s, ["titleBlockId"] = tb });

        var pdf = Sheets.Pdf(f.Session.Reader, [f.Model.Get(s)!]);
        var content = PdfText(pdf);
        Assert.Contains("(Kes\\344m\\366kki) Tj", content.Replace("ä", "\\344").Replace("ö", "\\366"));
        Assert.Contains("Arkkitehdit Oy", content);
        Assert.Contains("A-101", content);

        var dxf = Encoding.GetEncoding(1252).GetString(Sheets.Dxf(f.Session.Reader, f.Model.Get(s)!));
        var lines = dxf.Split('\n').Select(x => x.Trim()).ToList();
        int Count(string kind) => lines.Where((x, i) => i > 0 && lines[i - 1] == "0" && x == kind).Count();
        Assert.Equal(1, Count("INSERT"));
        Assert.Equal(3, Count("ATTDEF"));
        Assert.Equal(3, Count("ATTRIB"));
        Assert.Equal(1, Count("SEQEND"));
        Assert.Contains("TB_Finnish", lines);
        Assert.Contains("PROJECT_NAME", lines);
        Assert.Contains("PROJECT_DESIGNER", lines);
        Assert.Contains("Kesämökki", lines);
        Assert.Contains("Suunnittelija: Arkkitehdit Oy", lines);
        Assert.Contains("Rakennuskohde", lines);   // a plain text of the block, not an attribute
        // The block table counts the block (model space, paper space and it).
        var table = dxf.IndexOf("BLOCK_RECORD\n  5\n9\n", StringComparison.Ordinal);
        Assert.Contains("AcDbSymbolTable\n 70\n3\n", dxf[table..(table + 80)]);
    }

    static string PdfText(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var sb = new StringBuilder();
        foreach (Match m in Regex.Matches(text, @"/Length (\d+) /Filter /FlateDecode[^\n]*\nstream\n"))
        {
            var at = m.Index + m.Length;
            var length = int.Parse(m.Groups[1].Value);
            try
            {
                using var z = new System.IO.Compression.ZLibStream(new MemoryStream(pdf, at, length), System.IO.Compression.CompressionMode.Decompress);
                sb.Append(new StreamReader(z, Encoding.Latin1).ReadToEnd());
            }
            catch (InvalidDataException) { }
        }
        return sb.ToString();
    }

    [Fact]
    public void The_project_information_goes_to_IFC_as_Lapin_ProjectInformation_and_comes_back()
    {
        var f = F();
        f.Run("project.setInfo", new JsonObject { ["number"] = "24/2026", ["permit"] = "AC 112/2026", ["designerAddress"] = "Huși\nVaslui" });
        f.Run("project.setParameter", new JsonObject { ["key"] = "sheetSeries", ["label"] = "Sheet series", ["value"] = "ARK" });
        f.Run("project.setParameter", new JsonObject { ["key"] = "grossArea", ["type"] = "number", ["value"] = "142,5" });
        f.Run("project.setParameter", new JsonObject { ["key"] = "handover", ["type"] = "date", ["value"] = "01.12.2026" });
        var ifc = Path.Combine(_dir, "info.ifc");
        IfcExport.Write(f.Session, ifc, null);
        var text = File.ReadAllText(ifc);
        Assert.Contains("'Lapin_ProjectInformation'", text);
        Assert.DoesNotContain("'Pset_Lapin_ProjectInformation'", text);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('permit',$,IFCLABEL('AC 112/2026'),$)", text);
        Assert.Matches(@"IFCPROPERTYSINGLEVALUE\('grossArea','grossArea',IFCREAL\(142\.5\)", text);
        Assert.Contains("IFCDATE('2026-12-01')", text);
        Assert.Contains("IFCTEXT(", text);

        var b = F();
        IfcImport.Run(b.Session, ifc, null, out _);
        Assert.Equal("24/2026", ProjectInfo.Get(b.Model, "number"));
        Assert.Equal("AC 112/2026", ProjectInfo.Get(b.Model, "permit"));
        Assert.Equal("Huși\nVaslui", ProjectInfo.Get(b.Model, "designerAddress"));
        var customs = ProjectInfo.Customs(b.Model);
        Assert.Contains(customs, c => c is { Key: "sheetSeries", Label: "Sheet series", Value: "ARK", Type: "text" });
        Assert.Contains(customs, c => c is { Key: "grossArea", Type: "number", Value: "142.5" });
        Assert.Contains(customs, c => c is { Key: "handover", Type: "date", Value: "2026-12-01" });
    }

    [Fact]
    public void Project_parameters_have_unique_keys_and_values_of_their_type()
    {
        var f = F();
        f.Run("project.setParameter", new JsonObject { ["key"] = "contractor", ["label"] = "Contractor", ["group"] = "client" });
        Assert.Throws<CommandException>(() => f.Run("project.setParameter", new JsonObject { ["key"] = "client" }));        // a field's
        Assert.Throws<CommandException>(() => f.Run("project.setParameter", new JsonObject { ["key"] = "2nd" }));
        Assert.Throws<CommandException>(() => f.Run("project.setParameter", new JsonObject { ["key"] = "x", ["type"] = "number", ["value"] = "many" }));
        Assert.Throws<CommandException>(() => f.Run("project.setParameter", new JsonObject { ["key"] = "y", ["type"] = "date", ["value"] = "next week" }));
        f.Run("project.setParameter", new JsonObject { ["key"] = "contractor", ["value"] = "Constructii SRL" });
        Assert.Equal(("Contractor", "client", "Constructii SRL"), ProjectInfo.Customs(f.Model).Single() is var c ? (c.Label, c.Group, c.Value) : default);
        f.Run("project.setParameter", new JsonObject { ["key"] = "contractor", ["newKey"] = "builder" });
        Assert.Equal("builder", ProjectInfo.Customs(f.Model).Single().Key);
        f.Run("project.removeParameter", new JsonObject { ["key"] = "builder" });
        Assert.Empty(ProjectInfo.Customs(f.Model));
        var info = f.Session.Query("project.info", [])!;
        Assert.Contains((info["fields"] as JsonArray)!, x => (string?)x!["key"] == "designerRegistration" && (string?)x["group"] == "designer");
    }

    [Fact]
    public void Title_blocks_are_made_kept_assigned_and_deleted_back_to_the_default()
    {
        var f = F();
        var s = Sheet(f);
        var t = Sheet(f, name: "Sections");
        var cartus = (string)f.Run("titleBlock.create", new JsonObject { ["from"] = BuiltinTitleBlocks.Cartus, ["name"] = "Cartuș firmă" })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("titleBlock.update", new JsonObject { ["id"] = BuiltinTitleBlocks.Iso7200, ["name"] = "Mine" }));
        Assert.Throws<CommandException>(() => f.Run("titleBlock.update", new JsonObject { ["id"] = cartus, ["template"] = new JsonObject { ["w"] = 2 } }));
        Assert.Throws<CommandException>(() => f.Run("titleBlock.update", new JsonObject { ["id"] = cartus, ["template"] = Template(100, 30, "bottomRight", new JsonObject { ["type"] = "circle" }) }));
        f.Run("titleBlock.setDefault", new JsonObject { ["id"] = BuiltinTitleBlocks.Iso7200 });
        f.Run("sheet.setTitleBlock", new JsonObject { ["id"] = s, ["titleBlockId"] = cartus });
        Assert.Equal(cartus, TitleBlocks.AssignedId(f.Model, f.Model.Get(s)!));
        Assert.Equal(BuiltinTitleBlocks.Iso7200, TitleBlocks.AssignedId(f.Model, f.Model.Get(t)!));
        Assert.Contains("Proiect nr.", Texts(f, s));
        Assert.Contains("Identification number", Texts(f, t));
        var list = f.Session.Query("titleBlock.list", [])!;
        Assert.Equal(BuiltinTitleBlocks.Iso7200, (string?)list["default"]);
        Assert.Equal(1, (int)(list["items"] as JsonArray)!.Single(x => (string?)x!["id"] == cartus)!["uses"]!);
        // Out to a file and back in, as another computer would.
        var json = f.Session.Query("titleBlock.export", new JsonObject { ["id"] = cartus })!.AsObject();
        var again = (string)f.Run("titleBlock.import", new JsonObject { ["json"] = json.DeepClone() })!["id"]!;
        Assert.Equal("Cartuș firmă 2", f.Model.Get(again)!.Name);
        f.Run("titleBlock.delete", new JsonObject { ["id"] = cartus });
        Assert.Equal(BuiltinTitleBlocks.Iso7200, TitleBlocks.AssignedId(f.Model, f.Model.Get(s)!));
        f.Run("titleBlock.setDefault", new JsonObject { ["id"] = again });
        f.Run("titleBlock.delete", new JsonObject { ["id"] = again });
        Assert.Equal(BuiltinTitleBlocks.Standard, TitleBlocks.AssignedId(f.Model, f.Model.Get(s)!));
        Assert.Null(TitleBlocks.TemplateOf(f.Model, f.Model.Get(t)!));
    }

    [Fact]
    public void A_logo_travels_inside_an_exported_title_block()
    {
        var f = F();
        // A 1 × 1 PNG.
        const string png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
        var image = (string)f.Run("image.add", new JsonObject { ["name"] = "Logo", ["mime"] = "image/png", ["data"] = png })!["id"]!;
        var tb = Create(f, Template(100, 30, "bottomRight", new JsonObject { ["type"] = "image", ["x"] = 2, ["y"] = 2, ["w"] = 26, ["h"] = 26, ["imageId"] = image }));
        var s = Sheet(f);
        f.Run("sheet.setTitleBlock", new JsonObject { ["id"] = s, ["titleBlockId"] = tb });
        var img = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!).Pens.SelectMany(p => p.Images).Single();
        Assert.Equal(image, img.ImageId);
        Assert.Equal((420.0 - 10 - 100 + 2, 297.0 - 10 - 28), (img.Corners[0].X, img.Corners[0].Y));
        var json = f.Session.Query("titleBlock.export", new JsonObject { ["id"] = tb })!.AsObject();
        Assert.Equal(png, (string?)json["template"]!["items"]![0]!["data"]);
        var g = F();
        var back = (string)g.Run("titleBlock.import", new JsonObject { ["json"] = json.DeepClone() })!["id"]!;
        var item = TitleBlocks.TemplateById(g.Model, back)!.Items[0]!;
        Assert.Equal(png, g.Model.Get((string)item["imageId"]!)!.Str("data"));
    }

    [Fact]
    public void A_revision_table_item_lists_the_issues_in_its_box()
    {
        var f = F();
        var s = Sheet(f);
        var tb = Create(f, Template(120, 40, "bottomRight",
            new JsonObject { ["type"] = "revisions", ["x"] = 0, ["y"] = 20, ["w"] = 120, ["h"] = 20, ["rowH"] = 5, ["labels"] = new JsonObject { ["revision"] = "MUUTOS" } }));
        f.Run("sheet.setTitleBlock", new JsonObject { ["id"] = s, ["titleBlockId"] = tb });
        var page = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!, issuing: new Sheets.Issuing("P01", "S2", "For coordination", "2026-11-06T10:00:00Z"));
        var texts = page.Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        Assert.Contains("MUUTOS", texts);
        Assert.Contains("P01", texts);
        Assert.Contains("06.11.2026", texts);
        Assert.Contains("For coordination", texts);
        // No table above the block: the template has its own.
        Assert.DoesNotContain("REV", texts);
    }
}
