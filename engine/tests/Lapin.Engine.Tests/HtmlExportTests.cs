using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Architecture;
using Lapin.Cost;
using Lapin.Drawing;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Lapin for the browser (docs/html-export.md, decision 482): one .html file that is a viewer opening by double-click
/// (the 3D model, the drawings as SVG, the estimate, the properties) and the .lapin package itself, after a
/// <c>&lt;plaintext hidden&gt;</c> tag, which Lapin reopens as a project. A zip is read from its end, so the package
/// written after the page, its offsets counted from the file's start, is a zip as it stands.
/// </summary>
public sealed class HtmlExportTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-html-").FullName;
    string PathOf(string name) => Path.Combine(_dir, name);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static Fixture Project()
    {
        var f = new Fixture();
        f.Wall(0, 0, 6, 0);
        f.Wall(6, 0, 6, 4);
        f.Run("view.create", new JsonObject { ["kind"] = "section", ["name"] = "Section A", ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 6) });
        return f;
    }

    static byte[] Package(Fixture f)
    {
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        return ms.ToArray();
    }

    const string Page = "<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"><meta name=\"lapin-package\" content=\"lapin-project 3\"><title>t</title></head><body><p>hello</p></body></html>";

    // ------------------------------------------------------------ the file: a page, then the package

    [Fact]
    public void A_package_written_after_a_page_reads_as_the_package_alone()
    {
        var f = Project();
        using var ms = new MemoryStream();
        HtmlPackage.Write(ms, Page, s => ProjectPackage.Write(f.Session, s));
        var bytes = ms.ToArray();

        // The page comes first, as written, then the tag that makes the rest text.
        var head = Encoding.UTF8.GetString(bytes, 0, Page.Length + HtmlPackage.Tail.Length + 1);
        Assert.StartsWith(Page, head);
        Assert.Contains(HtmlPackage.Tail, head);

        // Read as a zip from the start of the file: the same project as the plain package holds.
        var alone = LapinPackage.Read(new MemoryStream(Package(f)));
        var paged = LapinPackage.Read(new MemoryStream(bytes));
        Assert.Equal(alone.Entities.Select(e => (e.Id, e.Class, e.Name)), paged.Entities.Select(e => (e.Id, e.Class, e.Name)));
        Assert.Equal(alone.Entities.Select(e => e.Attrs.ToJsonString()), paged.Entities.Select(e => e.Attrs.ToJsonString()));
        Assert.Equal(alone.Relations.Count, paged.Relations.Count);
        Assert.Equal(alone.Manifest.Parts.Select(p => p.Sha256), paged.Manifest.Parts.Select(p => p.Sha256));

        // Every entry's local header is where the central directory says, counted from the file's start.
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Contains(zip.Entries, e => e.FullName == "manifest.json");
        Assert.True(HtmlPackage.IsLapinPage(bytes));
        Assert.True(HtmlPackage.HasPackage(bytes));
    }

    [Fact]
    public void A_page_and_a_package_are_told_apart_by_their_first_bytes()
    {
        var f = Project();
        Assert.False(HtmlPackage.IsPage(Package(f)));
        Assert.True(HtmlPackage.IsPage(Encoding.UTF8.GetBytes("﻿  <!doctype html><html></html>")));
        Assert.True(HtmlPackage.IsPage(Encoding.UTF8.GetBytes("<html><body>x</body></html>")));
        // A page of someone else's: a page, not Lapin's.
        var foreign = Encoding.UTF8.GetBytes("<!DOCTYPE html><html><head><title>Hi</title></head><body>hello</body></html>");
        Assert.True(HtmlPackage.IsPage(foreign));
        Assert.False(HtmlPackage.IsLapinPage(foreign));
        Assert.False(HtmlPackage.HasPackage(foreign));
        // Lapin's page without its project: Lapin's, nothing to open.
        var viewerOnly = Encoding.UTF8.GetBytes(Page);
        Assert.True(HtmlPackage.IsLapinPage(viewerOnly));
        Assert.False(HtmlPackage.HasPackage(viewerOnly));
    }

    // ------------------------------------------------------------ SVG, from the pens a PDF prints

    [Fact]
    public void Svg_draws_the_pens_as_the_pdf_does()
    {
        var fill = new Pen(PenLayer.Fill, null, 0, "#d0d0d0", null);
        fill.Loops.Add([new Vec2(10, 10), new Vec2(50, 10), new Vec2(50, 30), new Vec2(10, 30)]);
        var cut = new Pen(PenLayer.Cut, "#000000", 0.5, null, null);
        cut.Segments.Add((new Vec2(10, 10), new Vec2(50, 10)));
        cut.Segments.Add((new Vec2(50, 10), new Vec2(50, 30)));
        var hidden = new Pen(PenLayer.Hidden, "#666666", 0.18, null, [1.5, 1]);
        hidden.Segments.Add((new Vec2(10, 40), new Vec2(50, 40)));
        var text = new Pen("TEXT", "#112233", 0, null, null);
        text.Texts.Add(new PenText(new Vec2(20, 50), "Cameră <1> & „baie”", 2.5, 90, "start", "bottom", Bold: true));
        var page = new PaperPage(210, 297, [fill, cut, hidden, text], "Plan", "square");

        var svg = SvgWriter.Write(page, [new SvgPick("2O2Fr$t4X7Zf8NOew3FLOH", [[new Vec2(10, 10), new Vec2(50, 10), new Vec2(50, 30)]], [(new Vec2(10, 10), new Vec2(50, 10))])]);
        var doc = System.Xml.Linq.XDocument.Parse(svg);
        XNamespace_Check(doc);
        Assert.Contains("viewBox=\"0 0 210 297\"", svg);
        Assert.Contains("width=\"210mm\"", svg);
        // A fill even-odd, the lines in one path per pen with their width and caps, dashes on paper.
        Assert.Contains("fill=\"#d0d0d0\"", svg);
        Assert.Contains("fill-rule=\"evenodd\"", svg);
        Assert.Contains("d=\"M10 10L50 10L50 30L10 30Z\"", svg);
        Assert.Contains("stroke-width=\"0.5\"", svg);
        Assert.Contains("stroke-linecap=\"square\"", svg);
        Assert.Contains("stroke-dasharray=\"1.5 1\"", svg);
        Assert.Contains("M10 10L50 10M50 10L50 30", svg);
        // The text: its cap height the text's height (Helvetica's 0.718 em), turned anticlockwise on paper, escaped.
        Assert.Contains("font-size=\"3.48\"", svg);
        Assert.Contains("rotate(-90 20 50)", svg);
        Assert.Contains("font-weight=\"bold\"", svg);
        Assert.Contains("Cameră &lt;1&gt; &amp; „baie”", svg);
        Assert.Contains("fill=\"#112233\"", svg);
        // What a click takes: the element by its GlobalId, never drawn.
        Assert.Contains("data-gid=\"2O2Fr$t4X7Zf8NOew3FLOH\"", svg);
        Assert.Contains("class=\"lp-pick\"", svg);
        Assert.DoesNotContain("<script", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<!--", svg);
    }

    static void XNamespace_Check(System.Xml.Linq.XDocument doc) => Assert.Equal("http://www.w3.org/2000/svg", doc.Root!.Name.NamespaceName);

    [Fact]
    public void Svg_lays_a_picture_by_its_corners()
    {
        // A 1×1 PNG (red), laid on a 40 × 20 mm box, half transparent, clipped to its pen's box.
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");
        var pen = new Pen("IMAGE", null, 0, null, null) { Clip = (0, 0, 30, 30) };
        pen.Images.Add(new PenImage("img", [new Vec2(10, 10), new Vec2(50, 10), new Vec2(50, 30), new Vec2(10, 30)], 0.5, png, "image/png"));
        var svg = SvgWriter.Write(new PaperPage(100, 100, [pen]));
        Assert.Contains("data:image/png;base64,", svg);
        Assert.Contains("opacity=\"0.5\"", svg);
        Assert.Contains("<clipPath", svg);
        Assert.Contains("matrix(40 0 0 20 10 10)", svg);
        System.Xml.Linq.XDocument.Parse(svg);
    }

    [Fact]
    public void A_plan_on_paper_keeps_its_scale_and_its_elements()
    {
        var f = Project();
        var wall = f.Model.OfClass(IfcClass.Wall).First().Id;
        var q = f.Session.Reader;
        var view = Views.Find(f.Model, "plan:" + f.Storey)!;
        var (page, picks) = HtmlExport.ViewOnPaper(Views.Render(q, view, print: true));
        // 6 m at 1:100 is 60 mm on paper, with a margin either side.
        Assert.InRange(page.W, 60, 120);
        Assert.Contains(picks, p => p.Id == wall);
        var svg = SvgWriter.Write(page, picks);
        Assert.Contains($"data-gid=\"{wall}\"", svg);
    }

    // ------------------------------------------------------------ the page

    [Fact]
    public void The_page_keeps_its_scripts_closed_whatever_they_hold()
    {
        var data = new JsonObject { ["project"] = "Casa </script><script>alert(1)</script> <!-- x", ["n"] = 1 };
        var viewer = "var s = '</script><script>alert(2)</script>'; var t = '<!-- y'; window.ok = 1;";
        var html = HtmlExport.Page(data, "QUJD", [("d1", "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>a</text></svg>")], viewer, "Casa", "ro");

        // One closing tag for each block (the data, the GLB, the drawing, the viewer): nothing inside closes its block
        // early, and with no comment opened in a block, a "<script" inside one is text.
        Assert.Equal(4, Regex.Matches(html, "</script", RegexOptions.IgnoreCase).Count);
        Assert.DoesNotContain("<!--", html);
        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<html lang=\"ro\"", html);
        Assert.Contains(HtmlPackage.Meta, html);
        Assert.True(HtmlPackage.IsLapinPage(Encoding.UTF8.GetBytes(html)));

        // The data block reads back as it was given.
        var block = Regex.Match(html, "<script type=\"application/json\" id=\"lapin-data\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(data.ToJsonString(), JsonNode.Parse(block)!.ToJsonString());
        // The viewer, undone as the page undoes it, is the code given.
        var code = Regex.Match(html, "<script id=\"lapin-viewer\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(viewer, HtmlExport.Unscript(code));
        Assert.Contains("id=\"lapin-glb\"", html);
        Assert.Contains("data-drawing=\"d1\"", html);
    }

    [Fact]
    public void Without_a_viewer_the_page_says_how_to_open_it()
    {
        var html = HtmlExport.Page(new JsonObject { ["project"] = "Casa" }, null, [], null, "Casa", "en");
        Assert.Contains("Lapin", html);
        Assert.Contains("File → Open", html);
        Assert.DoesNotContain("id=\"lapin-viewer\"", html);
    }

    // ------------------------------------------------------------ export, then open in Lapin

    static (EngineDispatcher D, Func<string, JsonObject, JsonObject> Call) Engine(Fixture f)
    {
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        JsonObject Call(string method, JsonObject p)
        {
            var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
            var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
            if (o["error"] is { } err) throw new CommandException(err is JsonValue ? (string)err! : (string?)err["message"] ?? err.ToJsonString());
            return o["result"] as JsonObject ?? [];
        }
        return (d, Call);
    }

    static JsonObject DataOf(string html) =>
        (JsonObject)JsonNode.Parse(Regex.Match(html, "<script type=\"application/json\" id=\"lapin-data\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value)!;

    static string PageText(byte[] bytes)
    {
        var end = Encoding.UTF8.GetString(bytes).IndexOf(HtmlPackage.Tail, StringComparison.Ordinal);
        return end < 0 ? Encoding.UTF8.GetString(bytes) : Encoding.UTF8.GetString(bytes)[..end];
    }

    [Fact]
    public void An_exported_page_opens_in_Lapin_as_the_project_unsaved()
    {
        var f = Project();
        var walls = f.Model.OfClass(IfcClass.Wall).Select(e => e.Id).Order().ToList();
        var before = string.Join("\n", f.Model.Entities.Select(e => e.Id + e.Attrs.ToJsonString()));
        var (_, call) = Engine(f);
        var path = PathOf("Casa.html");
        var r = call("project.exportHtml", new JsonObject { ["path"] = path, ["viewer"] = "window.lapinViewer = 1;", ["lang"] = "en" });
        Assert.Equal(path, (string?)r["path"]);
        var bytes = File.ReadAllBytes(path);
        Assert.True(HtmlPackage.IsLapinPage(bytes));
        Assert.True(HtmlPackage.HasPackage(bytes));
        Assert.True((long)r["sizes"]!["project"]! > 0);

        // The page's data: the drawings (the storey's plan and the section), the 3D model, every element's properties.
        var data = DataOf(PageText(bytes));
        var drawings = ((JsonArray)data["drawings"]!).OfType<JsonObject>().ToList();
        Assert.Contains(drawings, d => (string?)d["kind"] == "plan");
        Assert.Contains(drawings, d => (string?)d["kind"] == "section" && (string?)d["name"] == "Section A");
        Assert.True((bool)data["model3d"]!);
        Assert.True((bool)data["hasProject"]!);
        var elements = (JsonObject)data["elements"]!;
        foreach (var w in walls)
        {
            var e = (JsonObject)elements[w]!;
            Assert.Equal("IfcWall", (string?)e["class"]);
            Assert.NotNull(e["storey"]);
            Assert.NotEmpty((JsonArray)e["quantities"]!);
        }
        Assert.Contains("id=\"lapin-glb\"", PageText(bytes));
        // The model is not changed by an export.
        Assert.Equal(before, string.Join("\n", f.Model.Entities.Select(e => e.Id + e.Attrs.ToJsonString())));

        // Opened again: the same model, with no file of its own, so the first save asks where (a .lapin), never over the page.
        var g = new Fixture();
        var (_, open) = Engine(g);
        var opened = open("project.open", new JsonObject { ["path"] = path });
        Assert.Equal("Casa.html", (string?)opened["page"]);
        Assert.Equal(walls, g.Model.OfClass(IfcClass.Wall).Select(e => e.Id).Order().ToList());
        Assert.Null(g.Session.Path);
        Assert.Throws<CommandException>(() => open("project.save", []));
        var saved = PathOf("Casa.lapin");
        open("project.save", new JsonObject { ["path"] = saved });
        Assert.Equal(saved, g.Session.Path);
        Assert.Equal(walls, LapinPackage.Read(saved).Entities.Where(e => e.Class == IfcClass.Wall).Select(e => e.Id).Order().ToList());
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void A_browser_opens_the_page_from_its_bytes()
    {
        var f = Project();
        var (_, call) = Engine(f);
        var r = call("project.exportHtml", new JsonObject { ["lang"] = "en" });
        var bytes = Convert.FromBase64String((string)r["data"]!);
        Assert.EndsWith(".html", (string)r["name"]!);

        var g = new Fixture();
        var (_, open) = Engine(g);
        var opened = open("project.openBytes", new JsonObject { ["name"] = "Casa.html", ["data"] = Convert.ToBase64String(bytes) });
        Assert.Equal("Casa.html", (string?)opened["page"]);
        Assert.Equal(f.Model.OfClass(IfcClass.Wall).Count(), g.Model.OfClass(IfcClass.Wall).Count());
        Assert.Null(g.Session.Path);
        // Saved as bytes: a .lapin of the page's name.
        var saved = open("project.saveBytes", new JsonObject { ["name"] = "Casa.lapin" });
        Assert.Equal("Casa.lapin", (string?)saved["name"]);
    }

    [Fact]
    public void A_page_keeps_the_projects_versions_for_its_first_save()
    {
        var f = Project();
        var (_, call) = Engine(f);
        var source = PathOf("v.lapin");
        call("project.save", new JsonObject { ["path"] = source });
        call("project.saveVersion", new JsonObject { ["name"] = "Concept" });
        var page = PathOf("v.html");
        call("project.exportHtml", new JsonObject { ["path"] = page });

        var g = new Fixture();
        var (_, open) = Engine(g);
        open("project.open", new JsonObject { ["path"] = page });
        var saved = PathOf("again.lapin");
        open("project.save", new JsonObject { ["path"] = saved });
        Assert.Equal("Concept", Assert.Single(PackageVersions.Of(saved).Versions).Name);
    }

    [Fact]
    public void A_page_not_of_Lapins_is_refused_with_why()
    {
        var foreign = PathOf("site.html");
        File.WriteAllText(foreign, "<!DOCTYPE html><html><head><title>Hi</title></head><body>hello</body></html>");
        var (_, open) = Engine(new Fixture());
        var e = Assert.Throws<CommandException>(() => open("project.open", new JsonObject { ["path"] = foreign }));
        Assert.Contains("not written by Lapin", e.Message);
        e = Assert.Throws<CommandException>(() => open("project.openBytes", new JsonObject { ["name"] = "site.html", ["data"] = Convert.ToBase64String(File.ReadAllBytes(foreign)) }));
        Assert.Contains("not written by Lapin", e.Message);

        // Lapin's page exported without its project: it shows the model, it does not open.
        var f = Project();
        var (_, call) = Engine(f);
        var viewerOnly = PathOf("only.html");
        call("project.exportHtml", new JsonObject { ["path"] = viewerOnly, ["project"] = false });
        Assert.False(HtmlPackage.HasPackage(File.ReadAllBytes(viewerOnly)));
        e = Assert.Throws<CommandException>(() => open("project.open", new JsonObject { ["path"] = viewerOnly }));
        Assert.Contains("without its project", e.Message);
    }

    [Fact]
    public void The_options_choose_what_the_page_carries()
    {
        var f = Project();
        var section = f.Model.OfClass(Views.Class).First(v => v.Name == "Section A").Id;
        var (_, call) = Engine(f);
        var path = PathOf("few.html");
        call("project.exportHtml", new JsonObject { ["path"] = path, ["drawings"] = new JsonArray(section), ["model3d"] = false });
        var html = PageText(File.ReadAllBytes(path));
        var data = DataOf(html);
        Assert.Equal(section, (string?)Assert.Single((JsonArray)data["drawings"]!)!["id"]);
        Assert.False((bool)data["model3d"]!);
        Assert.DoesNotContain("id=\"lapin-glb\"", html);

        // What the dialog shows before: the drawings it takes by default, and what each part weighs.
        var plan = call("project.htmlPlan", []);
        var listed = ((JsonArray)plan["drawings"]!).OfType<JsonObject>().ToList();
        Assert.Contains(listed, d => (string?)d["id"] == "plan:" + f.Storey);
        Assert.Contains(listed, d => (string?)d["id"] == section);
        Assert.True((long)plan["sizes"]!["project"]! > 0);
        Assert.False((bool)plan["tables"]!);
    }

    [Fact]
    public void A_drawing_that_shows_no_element_is_listed_but_not_taken_by_default()
    {
        var f = Project();
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        var withWall = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Ground floor" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = withWall, ["viewId"] = "plan:" + f.Storey, ["x"] = 200, ["y"] = 150 });
        var empty = (string)f.Run("sheet.create", new JsonObject { ["name"] = "First floor" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = empty, ["viewId"] = "plan:" + upper, ["x"] = 200, ["y"] = 150 });
        var (_, call) = Engine(f);

        // The dialog lists every drawing; one that draws no element (the upper storey has none) is not ticked.
        var listed = ((JsonArray)call("project.htmlPlan", [])["drawings"]!).OfType<JsonObject>().ToDictionary(d => (string)d["id"]!);
        Assert.True((bool)listed["plan:" + f.Storey]["default"]!);
        Assert.False((bool)listed["plan:" + upper]["default"]!);
        Assert.True((bool)listed["plan:" + upper]["empty"]!);
        Assert.True((bool)listed[withWall]["default"]!);
        Assert.False((bool)listed[empty]["default"]!);
        Assert.False(listed[withWall].ContainsKey("empty"));

        // A page written without a list takes the drawings ticked by default; the empty ones only when named.
        var path = PathOf("defaults.html");
        call("project.exportHtml", new JsonObject { ["path"] = path, ["model3d"] = false, ["project"] = false });
        var ids = ((JsonArray)DataOf(PageText(File.ReadAllBytes(path)))["drawings"]!).Select(d => (string)d!["id"]!).ToList();
        Assert.Contains("plan:" + f.Storey, ids);
        Assert.Contains(withWall, ids);
        Assert.DoesNotContain("plan:" + upper, ids);
        Assert.DoesNotContain(empty, ids);
        call("project.exportHtml", new JsonObject { ["path"] = path, ["drawings"] = new JsonArray(empty), ["model3d"] = false, ["project"] = false });
        Assert.Equal(empty, (string?)Assert.Single((JsonArray)DataOf(PageText(File.ReadAllBytes(path)))["drawings"]!)!["id"]);

        // Told by what the view draws, not by where the elements stand: a section whose Visibility / Graphics hides the walls shows none.
        var section = f.Model.OfClass(Views.Class).First(v => v.Name == "Section A").Id;
        bool Default(string id) => (bool)((JsonArray)call("project.htmlPlan", [])["drawings"]!).OfType<JsonObject>().First(d => (string?)d["id"] == id)["default"]!;
        Assert.True(Default(section));
        f.Run("view.graphics", new JsonObject { ["id"] = section, ["category"] = "walls", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.False(Default(section));
    }

    [Fact]
    public void The_estimate_goes_when_the_edition_has_the_cost_module_marked_as_the_app_marks_it()
    {
        var f = new Fixture(new ArchitectureModule(), new CostModule());
        // Lapin's own values (the template list): the estimate is marked indicative.
        f.Run("cost.addPriceList", new JsonObject { ["template"] = true });
        var type = f.Type("Blocks 250", ("Blocks", 0.25, true));
        f.Wall(0, 0, 10, 0, type);
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(new JsonObject { ["code"] = "LAP-MAS", ["part"] = "core" }) });
        f.Run("cost.createEstimate", new JsonObject { ["name"] = "Deviz" });
        var (_, call) = Engine(f);
        Assert.True((bool)call("project.htmlPlan", [])["tables"]!);
        var path = PathOf("cost.html");
        call("project.exportHtml", new JsonObject { ["path"] = path });
        var data = DataOf(PageText(File.ReadAllBytes(path)));
        var table = (JsonObject)Assert.Single((JsonArray)data["tables"]!)!;
        Assert.Equal("cost", (string?)table["key"]);
        Assert.True((bool)table["indicative"]!);
        Assert.NotEmpty((JsonArray)table["rows"]!);
        Assert.NotEmpty((JsonArray)table["totals"]!);

        // Left out when asked.
        call("project.exportHtml", new JsonObject { ["path"] = path, ["tables"] = false });
        Assert.Empty((JsonArray)DataOf(PageText(File.ReadAllBytes(path)))["tables"]!);
    }
}
