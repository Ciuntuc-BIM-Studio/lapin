using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>
/// What the page exported for the browser carries (decision 482): the drawings (views and sheets; null: the default
/// ones, <see cref="HtmlExport.DefaultDrawings"/>), the 3D model (as a 3D view shows it: <see cref="ViewId"/>, else the
/// whole model), the modules' tables (the estimate), the project itself (without it the page is a viewer only); the
/// viewer's code and its phrases in the exporting user's language (the interface gives them: the engine has no
/// interface of its own), the edition's name and the build.
/// </summary>
public sealed record HtmlExportOptions(IReadOnlyList<string>? Drawings = null, bool Model3d = true, bool Tables = true, bool Project = true, string? ViewId = null,
    string? Viewer = null, JsonObject? Strings = null, string Lang = "en", string? Edition = null, string? App = null, JsonObject? Theme = null)
{
    public static HtmlExportOptions From(JsonObject p)
    {
        static bool Flag(JsonNode? n, bool fallback) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
        return new(
            p["drawings"] is JsonArray d ? d.Select(x => (string?)x).OfType<string>().ToList() : null,
            Flag(p["model3d"], true), Flag(p["tables"], true), Flag(p["project"], true),
            (string?)p["viewId"] is { Length: > 0 } v ? v : null,
            (string?)p["viewer"] is { Length: > 0 } js ? js : null,
            p["strings"] as JsonObject,
            (string?)p["lang"] is { Length: > 0 and <= 8 } lang && lang.All(char.IsAsciiLetter) ? lang : "en",
            (string?)p["edition"] is { Length: > 0 } ed ? ed : null,
            (string?)p["app"] is { Length: > 0 } app ? app : null,
            p["theme"] as JsonObject);
    }
}

/// <summary>
/// Lapin for the browser (decision 482, docs/html-export.md): the project as one .html file that a browser opens by
/// double-click, from file:// with no network — the 3D model (GLB, each node its element's GlobalId), the drawings as
/// SVG drawn by the engine as sheets print them (<see cref="SvgWriter"/>), the modules' tables (the estimate), every
/// element's properties — and, after the page, the project's .lapin package (<see cref="HtmlPackage"/>), which Lapin
/// opens again. Everything is inline: a page from a file can fetch nothing, import no module and start no worker. The
/// model is not changed: no undo step.
/// </summary>
public static class HtmlExport
{
    public const string Format = "lapin-html";
    public const int Version = 1;

    /// <summary>The drawings a page takes by default, in the project browser's order: each storey's plan, the sections, the elevations, then the sheets.</summary>
    public static List<JsonObject> DefaultDrawings(IModelView m)
    {
        var o = new List<JsonObject>();
        var views = m.OfClass(Views.Class).ToList();
        foreach (var storey in Spatial.Storeys(m).OrderBy(s => s.Num("elevation")))
        {
            // A level's plan: its own floor plan view once it has one, else its default plan (ui/src/viewport/storeyPlan.ts).
            var own = views.Where(v => FloorPlanOf(v, storey.Id)).ToList();
            if (own.Count == 0) o.Add(Item("plan:" + storey.Id, storey.Name, "plan"));
            foreach (var v in views.Where(v => (v.Str("kind") ?? "plan") == "plan" && v.Str("storeyId") == storey.Id))
                o.Add(Item(v.Id, v.Name, "plan"));
        }
        foreach (var kind in new[] { "section", "elevation" })
            foreach (var v in views.Where(v => v.Str("kind") == kind).OrderBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase))
                o.Add(Item(v.Id, v.Name, kind));
        foreach (var s in m.OfClass(Sheets.Class).OrderBy(s => s.Str("number") ?? "", StringComparer.Ordinal).ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase))
            o.Add(Item(s.Id, s.Name, "sheet", s.Str("number")));
        return o;
    }

    static bool FloorPlanOf(Entity v, string storeyId) =>
        (v.Str("kind") ?? "plan") == "plan" && v.Str("storeyId") == storeyId && v.Str("site") == null
        && !(v.Attrs["foundation"] is JsonValue f && (f.TryGetValue<bool>(out var fb) ? fb : f.ToString().Length > 0));

    static JsonObject Item(string id, string name, string kind, string? number = null)
    {
        var o = new JsonObject { ["id"] = id, ["name"] = name, ["kind"] = kind };
        if (number is { Length: > 0 }) o["number"] = number;
        return o;
    }

    // ------------------------------------------------------------ drawings on paper

    const double Margin = 10;

    /// <summary>A view as a page of its own at its scale (paper mm, y down) with a margin round what it shows, and what a click on it takes.</summary>
    public static (PaperPage Page, List<SvgPick> Picks) ViewOnPaper(Views.Rendered r, string title = "", string lineCap = "round")
    {
        var k = 1000 / r.Scale;
        var (min, max) = r.Bounds ?? (new Vec2(0, 0), new Vec2(1, 1));
        var w = Math.Max(20, (max.X - min.X) * k + 2 * Margin);
        var h = Math.Max(20, (max.Y - min.Y) * k + 2 * Margin);
        Vec2 ToPaper(Vec2 p) => new(Margin + (p.X - min.X) * k, Margin + (max.Y - p.Y) * k);
        var pens = r.Pens.Select(p => Sheets.ClipToBox(p, ToPaper, 0, 0, w, h)).OfType<Pen>().ToList();
        return (new PaperPage(w, h, pens, title, lineCap), Picks(r.Marks, ToPaper, 0, 0, w, h));
    }

    /// <summary>What a click on a drawing takes: each element's pick mark (its cut areas, its seen lines), on paper and cut to the box.</summary>
    static List<SvgPick> Picks(IEnumerable<ViewMark> marks, Func<Vec2, Vec2> toPaper, double x0, double y0, double x1, double y1)
    {
        var o = new List<SvgPick>();
        foreach (var mk in marks)
        {
            if (!mk.Pick || mk.Owner == null) continue;
            var pen = new Pen("PICK", "#000000", 0, null, null);
            foreach (var l in mk.Lines) if (l.Layer == "PICK") pen.Segments.Add((l.A, l.B));
            foreach (var loop in (mk.Data?["loops"] as JsonArray ?? []).OfType<JsonArray>())
            {
                var pts = new List<Vec2>();
                for (var i = 0; i + 1 < loop.Count; i += 2) pts.Add(new Vec2(Json.Number(loop[i]) ?? 0, Json.Number(loop[i + 1]) ?? 0));
                if (pts.Count >= 3) pen.Loops.Add(pts);
            }
            if (Sheets.ClipToBox(pen, toPaper, x0, y0, x1, y1) is not { } on) continue;
            // The outline is drawn by the loops already: the lines are for what has no area.
            o.Add(new SvgPick(mk.Owner, on.Loops, on.Loops.Count > 0 && on.Segments.Count > 400 ? [] : on.Segments));
        }
        return o;
    }

    /// <summary>A drawing (a view or a sheet) as SVG, with its paper size; null when it draws nothing (or cannot be drawn).</summary>
    public static (string Svg, double W, double H)? Drawing(IQueryContext q, string id)
    {
        var m = q.Model;
        var cap = DraftingResources.Book(m).LineCap;
        byte[]? Image(string img) => m.Get(img) is { Class: Images.Class } image ? Images.Bytes(image) : null;
        if (m.Get(id) is { Class: Sheets.Class } sheet)
        {
            var placed = Sheets.Place(q, sheet);
            var page = Sheets.Compose(q, sheet, placed);
            var picks = placed.Where(p => !p.Content).SelectMany(p => Picks(p.Drawing.Marks, p.ToPaper, p.X0, p.Y0, p.X1, p.Y1)).ToList();
            return (SvgWriter.Write(page, picks, Image), page.W, page.H);
        }
        if (Views.Find(m, id) is not { } view || view.Str("kind") is "3d") return null;
        var r = Views.Render(q, view, print: true);
        if (r.Problem != null || r.Bounds == null || r.Pens.Count == 0) return null;
        var (p2, pk) = ViewOnPaper(r, view.Name, cap);
        return (SvgWriter.Write(p2, pk, Image), p2.W, p2.H);
    }

    // ------------------------------------------------------------ properties

    static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// An element as the page's properties panel shows it: name, IFC class, type, storey, materials; its own properties
    /// (the class's fields, as the properties panel labels them), the property sets a file brought, its quantities.
    /// </summary>
    public static JsonObject Properties(IQueryContext q, ModuleCatalog catalog, Entity e)
    {
        var m = q.Model;
        var o = new JsonObject { ["name"] = e.Name, ["class"] = e.Class };
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        if (type != null) o["type"] = type.Name;
        if (Spatial.StoreyOf(m, e.Id) is { } storey) o["storey"] = storey.Name;
        var materials = new List<string>();
        if (LayerSets.BandsOf(m, e.Id) is { } bands)
            foreach (var b in bands) if (b.Material is { } bm && m.Get(bm)?.Name is { } bn && !materials.Contains(bn)) materials.Add(bn);
        if (materials.Count == 0)
        {
            string? Own(Entity x) => x.Str("material") is { } mat && m.Get(mat) is { Class: IfcClass.Material } me ? me.Name
                : m.RelatingOf(x.Id, IfcClass.RelAssociatesMaterial) is { Class: IfcClass.Material } a ? a.Name : null;
            if ((Own(e) ?? (type != null ? Own(type) : null)) is { } own) materials.Add(own);
        }
        if (materials.Count > 0) o["material"] = string.Join(", ", materials);

        var sets = new JsonArray();
        // The element's own fields, as the class describes them.
        if (catalog.Class(e.Class) is { } d)
        {
            var props = new JsonArray();
            foreach (var f in d.Fields)
                if (Value(m, f, e.Attrs[f.Key]) is { } v) props.Add((JsonNode)new JsonArray(f.Label, v));
            if (props.Count > 0) sets.Add((JsonNode)new JsonObject { ["name"] = d.Label, ["props"] = props });
        }
        // The sets a file brought (a reference's, a recognised element's).
        var imported = e.Attrs["properties"] as JsonObject ?? e.Attrs["imported"]?["properties"] as JsonObject;
        foreach (var (name, set) in imported ?? [])
        {
            if (set is not JsonObject so) continue;
            var props = new JsonArray();
            foreach (var (k, v) in so) if (v is JsonValue jv) props.Add((JsonNode)new JsonArray(k, jv.ToString()));
            if (props.Count > 0) sets.Add((JsonNode)new JsonObject { ["name"] = name, ["props"] = props });
        }
        o["sets"] = sets;

        var quantities = new JsonArray();
        IReadOnlyList<QuantitySet> qs;
        try { qs = q.Quantities(e.Id); }
        catch (CommandException) { qs = []; }
        foreach (var s in qs)
        {
            var props = new JsonArray();
            foreach (var x in s.Quantities)
                props.Add((JsonNode)new JsonArray(x.Label ?? x.Name, $"{x.Value.ToString(x.Kind == "COUNT" ? "0" : "0.###", CultureInfo.InvariantCulture)}{(x.Unit.Length > 0 ? " " + x.Unit : "")}"));
            if (props.Count > 0) quantities.Add((JsonNode)new JsonObject { ["name"] = s.PartName is { Length: > 0 } pn ? $"{s.Name} · {pn}" : s.Name, ["props"] = props });
        }
        o["quantities"] = quantities;
        return o;
    }

    /// <summary>A field's value as text: a length in metres, a choice or a text as it is, a reference by its name; tables and colours left out.</summary>
    static string? Value(IModelView m, FieldDescriptor f, JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        switch (f.Kind)
        {
            case "table" or "color": return null;
            case "bool": return v.TryGetValue<bool>(out var b) ? (b ? "Yes" : "No") : null;
            case "ref" or "material": return v.TryGetValue<string>(out var id) ? m.Get(id)?.Name ?? id : null;
            case "length": return Json.Number(v) is { } len && double.IsFinite(len) ? $"{len.ToString("0.###", CultureInfo.InvariantCulture)} m" : null;
            case "angle": return Json.Number(v) is { } ang && double.IsFinite(ang) ? $"{ang.ToString("0.##", CultureInfo.InvariantCulture)}°" : null;
            case "number": return Json.Number(v) is { } num && double.IsFinite(num) ? num.ToString("0.###", CultureInfo.InvariantCulture) : null;
            default: return v.TryGetValue<string>(out var s) && s.Length > 0 ? s : null;
        }
    }

    // ------------------------------------------------------------ the page

    /// <summary>A script's text as the page holds it: nothing in it closes its block or opens a comment (a JavaScript or SVG text reads the same).</summary>
    public static string Script(string s) =>
        Regex.Replace(s, "</(script)", "<\\/$1", RegexOptions.IgnoreCase).Replace("<!--", "<\\!--");

    /// <summary>A script's text as it was given: what <see cref="Script"/> did, undone (the viewer does the same to a drawing).</summary>
    public static string Unscript(string s) =>
        Regex.Replace(s, "<\\\\/(script)", "</$1", RegexOptions.IgnoreCase).Replace("<\\!--", "<!--");

    /// <summary>JSON as a script block holds it: the letters as they are, every &lt; escaped, so no text in it ends the block.</summary>
    public static string JsonText(JsonNode data) => data.ToJsonString(Relaxed).Replace("<", "\\u003c");

    /// <summary>
    /// The page: a doctype, Lapin's marker first in the head, a word for whoever opens it without the viewer (or without
    /// scripts), the data (JSON), the 3D model (GLB, base64), each drawing (SVG), then the viewer that shows them.
    /// </summary>
    public static string Page(JsonObject data, string? glbBase64, IReadOnlyList<(string Id, string Svg)> drawings, string? viewer, string title, string lang, string? hint = null)
    {
        var b = new StringBuilder();
        var t = SvgWriter.Escape(title);
        b.Append("<!DOCTYPE html>\n<html lang=\"").Append(SvgWriter.Escape(lang)).Append("\">\n<head>\n")
         .Append("<meta charset=\"utf-8\">\n").Append(HtmlPackage.Meta).Append('\n')
         .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
         .Append("<meta name=\"generator\" content=\"Lapin\">\n")
         .Append("<title>").Append(t).Append("</title>\n")
         .Append("<style>html,body{margin:0;height:100%;font:14px/1.45 system-ui,-apple-system,'Segoe UI',sans-serif;background:#f6f7f9;color:#1f2328}")
         .Append("@media (prefers-color-scheme:dark){html,body{background:#16181c;color:#e6e8eb}}")
         .Append("#lapin-start{max-width:36rem;margin:12vh auto;padding:0 16px}#lapin-start h1{font-size:1.4rem;margin:0 0 .5rem}plaintext{display:none}</style>\n")
         .Append("</head>\n<body>\n<div id=\"lapin-start\"><h1>").Append(t).Append("</h1><p>")
         .Append(SvgWriter.Escape(hint ?? "A Lapin project. To work on it, open this file in Lapin: File → Open."))
         .Append("</p><noscript><p>Turn on JavaScript to see the model and its drawings.</p></noscript></div>\n");
        b.Append("<script type=\"application/json\" id=\"lapin-data\">").Append(JsonText(data)).Append("</script>\n");
        if (glbBase64 != null) b.Append("<script type=\"application/octet-stream\" id=\"lapin-glb\">").Append(glbBase64).Append("</script>\n");
        foreach (var (id, svg) in drawings)
            b.Append("<script type=\"image/svg+xml\" data-drawing=\"").Append(SvgWriter.Escape(id)).Append("\">").Append(Script(svg)).Append("</script>\n");
        if (viewer != null) b.Append("<script id=\"lapin-viewer\">").Append(Script(viewer)).Append("</script>\n");
        return b.Append("</body>\n</html>").ToString();
    }

    /// <summary>The project's name as the page names it: the IfcProject's, else the file's.</summary>
    static string Title(Session session) => Sheets.ProjectName(session.Reader);

    /// <summary>The page with what the options ask for, and what each part weighs (bytes).</summary>
    public static (byte[] Bytes, JsonObject Sizes) Write(Session session, ProjectFiles files, HtmlExportOptions o, Func<Session, string?, (string Base64, IReadOnlyCollection<string> Ids)?>? glb = null)
    {
        var q = session.Reader;
        var m = session.Model;
        var title = Title(session);
        var sizes = new JsonObject();

        // The drawings, each drawn as the sheets print it.
        var wanted = o.Drawings ?? DefaultDrawings(m).Select(d => (string)d["id"]!).ToList();
        var all = DefaultDrawings(m).ToDictionary(d => (string)d["id"]!);
        var list = new JsonArray();
        var svgs = new List<(string, string)>();
        var gids = new HashSet<string>();
        foreach (var id in wanted.Distinct())
        {
            if (Drawing(q, id) is not { } d) continue;
            var meta = all.TryGetValue(id, out var known) ? (JsonObject)known.DeepClone()
                : m.Get(id) is { } e ? Item(id, e.Name, e.Class == Sheets.Class ? "sheet" : e.Str("kind") ?? "plan", e.Str("number"))
                : Views.Find(m, id) is { } v ? Item(id, v.Name, v.Str("kind") ?? "plan") : Item(id, id, "plan");
            meta["w"] = Math.Round(d.W, 2);
            meta["h"] = Math.Round(d.H, 2);
            list.Add((JsonNode)meta);
            svgs.Add((id, d.Svg));
            foreach (Match x in Regex.Matches(d.Svg, "data-gid=\"([^\"]+)\"")) gids.Add(x.Groups[1].Value);
        }
        sizes["drawings"] = svgs.Sum(s => (long)Encoding.UTF8.GetByteCount(s.Item2));

        // The 3D model: GLB, each element's node keyed by its GlobalId.
        string? glb64 = null;
        if (o.Model3d && glb?.Invoke(session, o.ViewId) is { } model)
        {
            glb64 = model.Base64;
            gids.UnionWith(model.Ids);
        }
        sizes["model3d"] = glb64?.Length ?? 0;

        // The modules' tables (the estimate): only those of the edition's modules, only when asked.
        var tables = new JsonArray();
        if (o.Tables)
            foreach (var source in session.Catalog.All<IPageTables>())
            {
                PageTable? table;
                try { table = source.Table(q); }
                catch (CommandException) { continue; }
                if (table == null) continue;
                tables.Add((JsonNode)table.ToJson());
                foreach (var r in table.Rows) gids.UnionWith(r.Elements);
            }

        // Every element the page shows (in 3D, on a drawing, in a table), with its properties.
        var elements = new JsonObject();
        foreach (var id in gids.Order(StringComparer.Ordinal))
            if (m.Get(id) is { } e) elements[id] = Properties(q, session.Catalog, e);

        var storeys = new JsonArray(Spatial.Storeys(m).OrderBy(s => s.Num("elevation"))
            .Select(s => (JsonNode)new JsonObject { ["id"] = s.Id, ["name"] = s.Name, ["elevation"] = s.Num("elevation") }).ToArray());
        var data = new JsonObject
        {
            ["format"] = Format, ["version"] = Version,
            ["project"] = title,
            ["edition"] = o.Edition ?? session.Product.Name,
            ["app"] = o.App ?? "Lapin",
            ["date"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["lang"] = o.Lang,
            ["strings"] = o.Strings?.DeepClone() ?? new JsonObject(),
            ["theme"] = o.Theme?.DeepClone(),
            ["hasProject"] = o.Project,
            ["model3d"] = glb64 != null,
            ["storeys"] = storeys,
            ["drawings"] = list,
            ["tables"] = tables,
            ["elements"] = elements,
        };
        string Phrase(string en) => (string?)o.Strings?[en] is { Length: > 0 } tr ? tr : en;
        var hint = Phrase(o.Project ? "A Lapin project. To work on it, open this file in Lapin: File → Open." : "A model exported from Lapin, to look at in a browser.");
        var page = Page(data, glb64, svgs, o.Viewer, $"{title} — {o.Edition ?? session.Product.Name}", o.Lang, hint);
        sizes["page"] = Encoding.UTF8.GetByteCount(page);
        sizes["viewer"] = o.Viewer?.Length ?? 0;

        using var ms = new MemoryStream();
        long before = 0;
        HtmlPackage.Write(ms, page, o.Project ? s =>
        {
            before = s.Position;
            ProjectPackage.Write(session, s, files.Versions(session), files.IssueFilesOf(session), files.Foreign(session));
        } : null);
        sizes["project"] = o.Project ? ms.Length - before : 0;
        sizes["total"] = ms.Length;
        return (ms.ToArray(), sizes);
    }

    /// <summary>
    /// What the export dialog shows before anything is written: the drawings taken by default, whether a module has a
    /// table to put (the estimate), and what the parts weigh — the project and the 3D model as they would be written, the
    /// drawings guessed (a sheet holds several views).
    /// </summary>
    public static JsonObject Plan(Session session, ProjectFiles files, Func<Session, string?, long>? glbSize = null, string? viewId = null)
    {
        var q = session.Reader;
        var drawings = DefaultDrawings(session.Model);
        var tables = false;
        foreach (var source in session.Catalog.All<IPageTables>())
            try { if (source.Table(q) != null) { tables = true; break; } }
            catch (CommandException) { }
        using var ms = new MemoryStream();
        ProjectPackage.Write(session, ms, files.Versions(session), files.IssueFilesOf(session), files.Foreign(session));
        var sizes = new JsonObject
        {
            ["project"] = ms.Length,
            ["model3d"] = glbSize?.Invoke(session, viewId) ?? 0,
            // A view about 60 kB of SVG, a sheet about 150 kB: a guess, the drawings are drawn only when written.
            ["drawing"] = 60_000, ["sheet"] = 150_000,
        };
        return new JsonObject { ["drawings"] = new JsonArray([.. drawings]), ["tables"] = tables, ["sizes"] = sizes };
    }

    /// <summary>The page's name beside the project's: House.html for House.lapin.</summary>
    public static string FileName(Session session) =>
        $"{(session.Path is { } p ? Path.GetFileNameWithoutExtension(p) : SafeName(Title(session)))}{HtmlPackage.Suffix}";

    static string SafeName(string s)
    {
        var bad = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var o = new string([.. s.Select(c => bad.Contains(c) ? '_' : c)]).Trim();
        return o.Length > 0 ? o : "project";
    }
}
