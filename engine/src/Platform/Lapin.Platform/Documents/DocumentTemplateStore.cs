using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing.Documents;
using Lapin.Model;

namespace Lapin.Platform.Documents;

/// <summary>
/// The project's own document templates (docs/plan-documents.md, stage 4): kept whole as JSON on the IfcProject
/// (<c>documentTemplates</c>), so they go with the file; saved from a document, renamed, deleted, imported, their style set.
/// Lapin's built-in ones stay as they are (a change to one is made on its duplicate); this computer's library is the
/// interface's, its templates given whole to <c>document.create</c> and <c>document.importTemplate</c>.
/// </summary>
public static class DocumentTemplateStore
{
    public const string Key = "documentTemplates";

    /// <summary>The templates as kept on the project (JSON objects), a broken one left out.</summary>
    static IEnumerable<JsonObject> Kept(IModelView m) => (Spatial.Project(m)?.Attrs[Key] as JsonArray ?? []).OfType<JsonObject>();

    /// <summary>The project's templates, read.</summary>
    public static IReadOnlyList<DocTemplate> Project(IModelView m)
    {
        var o = new List<DocTemplate>();
        foreach (var t in Kept(m))
        {
            try { o.Add(DocumentTemplates.From(t)); }
            catch (CommandException e) { Console.Error.WriteLine($"[documents] project template {(string?)t["id"]}: {e.Message}"); }
        }
        return o;
    }

    /// <summary>A template by its id: the project's, else a built-in one.</summary>
    public static DocTemplate? Find(IModelView m, string? id) => Project(m).FirstOrDefault(t => t.Id == id) ?? DocumentTemplates.Find(id);

    /// <summary>The template a command names: by id among the project's and the built-in ones, or given whole.</summary>
    public static DocTemplate Resolve(IModelView m, JsonNode? given) => given switch
    {
        JsonValue v when v.TryGetValue<string>(out var id) => Find(m, id) ?? throw new CommandException($"There is no template „{id}”."),
        _ => DocumentTemplates.Resolve(given),
    };

    /// <summary>A template whole, as it is kept and exported.</summary>
    public static JsonObject Json(DocTemplate t) => new()
    {
        ["id"] = t.Id, ["title"] = t.Title, ["description"] = t.Description, ["lang"] = t.Lang, ["isoType"] = t.IsoType,
        ["page"] = t.Page.DeepClone(), ["inputs"] = new JsonArray(t.Inputs.Select(i => (JsonNode)i.ToJson()).ToArray()), ["blocks"] = t.Blocks.DeepClone(),
    };

    /// <summary>A document's shape as a template: its page (and style), blocks, declared inputs, language and type, under a title.</summary>
    public static DocTemplate FromDocument(Entity doc, string id, string title, string? description) => new(
        id, title, description ?? "", doc.Str("lang") is { Length: > 0 } l ? l : "ro",
        doc.Attrs["page"] is JsonObject page ? (JsonObject)page.DeepClone() : [],
        (doc.Attrs["inputDefs"] as JsonArray ?? []).Select(DocInput.From).OfType<DocInput>().ToList(),
        doc.Attrs["blocks"] is JsonArray blocks ? (JsonArray)blocks.DeepClone() : [],
        Documents.IsoType(doc));

    /// <summary>An id from a title (letters and digits, the diacritics dropped) that no built-in or project template has.</summary>
    public static string NewId(IModelView m, string title)
    {
        var flat = new StringBuilder();
        foreach (var c in title.ToLowerInvariant())
        {
            // The letters with diacritics by their base (a table: the engine may run without ICU, where Normalize leaves them).
            var at = Accented.IndexOf(c);
            var ch = at >= 0 ? Plain[at] : c;
            flat.Append(char.IsAsciiLetterOrDigit(ch) ? ch : '-');
        }
        var stem = Regex.Replace(flat.ToString(), "-+", "-").Trim('-');
        if (stem.Length > 40) stem = stem[..40].TrimEnd('-');
        if (stem.Length == 0) stem = "template";
        var taken = Taken(m);
        if (!taken.Contains(stem)) return stem;
        for (var i = 2; ; i++) if (!taken.Contains($"{stem}-{i}")) return $"{stem}-{i}";
    }

    const string Accented = "ăâîșşțţàáäãåçèéêëìíïñòóôöõøùúûüýÿőűłńśźżćęą";
    const string Plain = "aaissttaaaaaceeeeiiinoooooouuuuyyoulnszzcea";

    static HashSet<string> Taken(IModelView m) => [.. DocumentTemplates.BuiltIn.Select(t => t.Id), .. Kept(m).Select(t => (string?)t["id"] ?? "")];

    /// <summary>The project's templates replaced (one undo step).</summary>
    static void Store(ICommandContext ctx, IEnumerable<JsonObject> templates)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        var list = new JsonArray(templates.Select(t => (JsonNode)t.DeepClone()).ToArray());
        if (list.Count > 0) attrs[Key] = list; else attrs.Remove(Key);
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>A template put into the project: one of the same id replaced in its place, else added at the end.</summary>
    static void Put(ICommandContext ctx, DocTemplate t)
    {
        var json = Json(t);
        var all = Kept(ctx.Model).ToList();
        var at = all.FindIndex(x => (string?)x["id"] == t.Id);
        if (at >= 0) all[at] = json; else all.Add(json);
        Store(ctx, all);
    }

    static JsonObject RequireProject(IModelView m, string? id) =>
        Kept(m).FirstOrDefault(t => (string?)t["id"] == id) ?? throw new CommandException(DocumentTemplates.Find(id) != null
            ? "A built-in template is not changed: duplicate it into the project or the library first." : $"There is no template „{id}” in the project.");

    static string Title(JsonNode? n) => ((string?)n)?.Trim() is { Length: > 0 } t ? t : throw new CommandException("A template needs a title.");

    public static JsonObject Summary(DocTemplate t, string origin)
    {
        var o = t.Summary();
        o["origin"] = origin;
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        // Every template a document can start from here: Lapin's own, then the project's (this computer's are the interface's).
        r.Query("document.templates", (q, _) => new JsonArray([
            .. DocumentTemplates.BuiltIn.Select(t => (JsonNode)Summary(t, "builtin")),
            .. Project(q.Model).Select(t => (JsonNode)Summary(t, "project")),
        ]));

        // A template whole (to export, keep in the library, duplicate a built-in one): by its id, or made from a document.
        r.Query("document.templateJson", (q, a) =>
        {
            if ((string?)a["templateId"] is { } tid) return Json(Find(q.Model, tid) ?? throw new CommandException($"There is no template „{tid}”."));
            var doc = Documents.Require(q.Model, (string?)a["id"]);
            var title = ((string?)a["title"])?.Trim() is { Length: > 0 } t ? t : doc.Name;
            return Json(FromDocument(doc, (string?)a["newId"] is { Length: > 0 } nid ? nid : NewId(q.Model, title), title, (string?)a["description"]));
        });

        // A document saved as a project template: a new one, or the project's template named by templateId replaced.
        r.Command("document.saveTemplate", "Save as template", (ctx, a) =>
        {
            var doc = Documents.Require(ctx.Model, (string?)a["id"]);
            var title = Title(a["title"]);
            var id = (string?)a["templateId"] is { Length: > 0 } given
                ? DocumentTemplates.Find(given) != null ? throw new CommandException("A built-in template is not replaced: save under a new title.") : given
                : NewId(ctx.Model, title);
            var t = FromDocument(doc, id, title, (string?)a["description"] ?? (string?)Kept(ctx.Model).FirstOrDefault(x => (string?)x["id"] == id)?["description"]);
            DocumentStyles.Check(t.Page);
            DocumentBlocks.Check(t.Blocks);
            Put(ctx, t);
            return new JsonObject { ["templateId"] = id };
        });

        r.Command("document.deleteTemplate", "Delete template", (ctx, a) =>
        {
            var id = (string?)a["templateId"];
            RequireProject(ctx.Model, id);
            Store(ctx, Kept(ctx.Model).Where(t => (string?)t["id"] != id));
            return null;
        });

        r.Command("document.renameTemplate", "Rename template", (ctx, a) =>
        {
            var t = (JsonObject)RequireProject(ctx.Model, (string?)a["templateId"]).DeepClone();
            t["title"] = Title(a["title"]);
            if (a.ContainsKey("description")) t["description"] = ((string?)a["description"])?.Trim() ?? "";
            Put(ctx, DocumentTemplates.From(t));
            return null;
        });

        // A template given whole (a file, the library) into the project: its id kept unless a built-in one has it, or the project
        // has it and overwrite is not asked (then a new id); checked as a template is.
        r.Command("document.importTemplate", "Import template", (ctx, a) =>
        {
            var t = DocumentTemplates.From(a["template"] as JsonObject ?? throw new CommandException("Which template?"));
            DocumentStyles.Check(t.Page);
            var builtIn = DocumentTemplates.Find(t.Id) != null;
            var inProject = Kept(ctx.Model).Any(x => (string?)x["id"] == t.Id);
            var replaced = inProject && !builtIn && a["overwrite"] is JsonValue ov && ov.TryGetValue<bool>(out var yes) && yes;
            if (builtIn || (inProject && !replaced)) t = t with { Id = NewId(ctx.Model, t.Title) };
            if (a.ContainsKey("title")) t = t with { Title = Title(a["title"]) };
            Put(ctx, t);
            return new JsonObject { ["templateId"] = t.Id, ["replaced"] = replaced };
        });

        // A document's or a project template's style (page.style) set whole; null or {} gives Lapin's own look back.
        r.Command("document.setStyle", "Document style", (ctx, a) =>
        {
            var style = a["style"] is JsonObject s && s.Count > 0 ? (JsonObject)s.DeepClone() : null;
            if (style != null) DocumentStyles.Check(new JsonObject { ["style"] = style.DeepClone() });
            JsonObject With(JsonObject? page)
            {
                var p = (page?.DeepClone() as JsonObject) ?? [];
                if (style != null) p["style"] = style.DeepClone(); else p.Remove("style");
                return p;
            }
            if ((string?)a["templateId"] is { } tid)
            {
                var t = (JsonObject)RequireProject(ctx.Model, tid).DeepClone();
                t["page"] = With(t["page"] as JsonObject);
                Put(ctx, DocumentTemplates.From(t));
                return null;
            }
            var doc = Documents.Require(ctx.Model, (string?)a["id"]);
            var attrs = doc.CloneAttrs();
            attrs["page"] = With(attrs["page"] as JsonObject);
            ctx.Replace(doc.With(attrs: attrs));
            return null;
        });

        // The style as kept (a document's, or any template's), {} when it has none.
        r.Query("document.style", (q, a) =>
        {
            var page = (string?)a["templateId"] is { } tid
                ? (Find(q.Model, tid) ?? throw new CommandException($"There is no template „{tid}”.")).Page
                : Documents.Require(q.Model, (string?)a["id"]).Attrs["page"] as JsonObject;
            return page?["style"]?.DeepClone() as JsonObject ?? [];
        });
    }
}

/// <summary>
/// A page's style as kept (<c>page.style</c>, docs/plan-documents.md, stage 4): read leniently for the writers (what is wrong is
/// left out, so a document always writes), checked strictly when it is set.
/// </summary>
public static class DocumentStyles
{
    /// <summary>The fonts the dialog offers for DOCX and ODT (any family name is taken).</summary>
    public static readonly string[] Fonts = ["Arial", "Calibri", "Times New Roman", "Georgia", "Verdana", "Liberation Sans"];
    public static readonly string[] PdfFonts = ["helvetica", "times"];
    public const double MinSize = 6, MaxSize = 24, MaxLogoWidth = 120;
    /// <summary>The most a logo's file may weigh (it is kept in the project and in each document).</summary>
    public const int MaxLogoBytes = 1 << 20;

    static readonly Regex Hex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    static byte[]? Bytes(string? b64)
    {
        if (string.IsNullOrWhiteSpace(b64)) return null;
        try { return Convert.FromBase64String(b64.Contains(',') ? b64[(b64.IndexOf(',') + 1)..] : b64); }
        catch (FormatException) { return null; }
    }

    /// <summary>The style for the writers; null when there is none.</summary>
    public static DocStyle? Parse(JsonNode? node)
    {
        if (node is not JsonObject o || o.Count == 0) return null;
        var font = ((string?)o["font"])?.Trim() is { Length: > 0 and <= 64 } f ? f : null;
        var size = Json.Number(o["size"]) is { } s && s >= MinSize && s <= MaxSize ? s : (double?)null;
        var color = (string?)o["headingColor"] is { } c && Hex.IsMatch(c) ? c.ToLowerInvariant() : null;
        var pdf = ((string?)o["pdfFont"])?.Trim().ToLowerInvariant() is { } pf && PdfFonts.Contains(pf) ? pf : null;
        DocLogo? logo = null;
        if (o["logo"] is JsonObject l && Bytes((string?)l["data"]) is { Length: > 0 and <= MaxLogoBytes } data)
        {
            var w = Json.Number(l["w"]) is { } lw && lw > 0 ? Math.Min(lw, MaxLogoWidth) : 30;
            var candidate = new DocLogo(data, w, (string?)l["position"] == "right" ? "right" : "left");
            if (candidate.Pixels != null) logo = candidate;
        }
        return font == null && size == null && color == null && pdf == null && logo == null ? null : new DocStyle(font, size, color, pdf, logo);
    }

    /// <summary>Refuses a page's style a writer could not use: a size out of range, a colour not #rrggbb, an unknown PDF font, a logo that is not a PNG or JPEG or is too big.</summary>
    public static void Check(JsonObject? page)
    {
        if (page?["style"] is not { } node) return;
        if (node is not JsonObject o) throw new CommandException("A style is a JSON object.");
        if (o["font"] is { } f && ((string?)f)?.Trim() is not { Length: > 0 and <= 64 }) throw new CommandException("A style's font is a family name.");
        if (o["size"] is { } s && (Json.Number(s) is not { } size || size < MinSize || size > MaxSize)) throw new CommandException($"A style's text size is between {MinSize} and {MaxSize} pt.");
        if (o["headingColor"] is { } c && ((string?)c is not { } hex || !Hex.IsMatch(hex))) throw new CommandException("A style's heading colour is written #rrggbb.");
        if (o["pdfFont"] is { } p && ((string?)p is not { } pf || !PdfFonts.Contains(pf.Trim().ToLowerInvariant()))) throw new CommandException("The PDF's font is helvetica or times.");
        if (o["logo"] is { } l)
        {
            if (l is not JsonObject logo) throw new CommandException("A style's logo is a JSON object.");
            var data = Bytes((string?)logo["data"]) ?? throw new CommandException("The logo's picture is base64.");
            if (data.Length > MaxLogoBytes) throw new CommandException($"The logo's file is over {MaxLogoBytes / 1024} KB: make it smaller.");
            if (new DocLogo(data, 30).Pixels == null) throw new CommandException("The logo is a PNG or JPEG picture.");
            if (logo["w"] is { } w && (Json.Number(w) is not { } lw || lw <= 0 || lw > MaxLogoWidth)) throw new CommandException($"The logo's width is up to {MaxLogoWidth} mm.");
        }
    }
}
