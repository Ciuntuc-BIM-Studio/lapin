using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

public static partial class TitleBlocks
{
    static Entity Require(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: Class } e ? e
        : BuiltinTitleBlocks.Is(id) ? throw new CommandException("Lapin's own title blocks stay as they are: duplicate one to change it.")
        : throw new CommandException("There is no such title block.");

    /// <summary>A name no other title block of the project has: the one asked, else with " 2", " 3"….</summary>
    static string FreeName(IModelView m, string name, string? except = null)
    {
        var taken = m.OfClass(Class).Where(e => e.Id != except).Select(e => e.Name).Concat(BuiltinTitleBlocks.All.Select(b => b.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name)) return name;
        for (var n = 2; ; n++) if (!taken.Contains($"{name} {n}")) return $"{name} {n}";
    }

    /// <summary>
    /// A template's images brought in: an image item with its file (<c>data</c>, <c>mime</c>: an exported template's) made a
    /// LapinImage of the project and named by its id; one naming an image the project has kept as it is.
    /// </summary>
    static TitleBlockTemplate WithImages(ICommandContext ctx, TitleBlockTemplate t)
    {
        foreach (var item in t.Items.OfType<JsonObject>().Where(i => (string?)i["type"] == "image"))
        {
            if ((string?)item["data"] is not { Length: > 0 } data) continue;
            var mime = (string?)item["mime"] ?? "";
            byte[] bytes;
            try { bytes = Convert.FromBase64String(data); } catch (FormatException) { throw new CommandException("An image of the title block is not base64."); }
            if (bytes.Length == 0 || bytes.Length > Images.MaxBytes || Images.Sniff(bytes) is not { } sniffed || (mime.Length > 0 && sniffed != mime))
                throw new CommandException("An image of the title block is not a PNG or a JPEG of at most 8 MB.");
            var image = new Entity(ctx.NewId(), Images.Class, (string?)item["name"] is { Length: > 0 } n ? n : "Logo", new JsonObject { ["mime"] = sniffed, ["data"] = Convert.ToBase64String(bytes) });
            ctx.Add(image);
            item["imageId"] = image.Id;
            item.Remove("data");
            item.Remove("mime");
            item.Remove("name");
        }
        return t;
    }

    /// <summary>A template whole, to save as a file: its name and template, its images' files inside (so another computer has them).</summary>
    public static JsonObject Export(IModelView m, string id)
    {
        var t = TemplateById(m, id) ?? throw new CommandException("There is no such title block.");
        var json = t.ToJson();
        foreach (var item in (json["items"] as JsonArray ?? []).OfType<JsonObject>().Where(i => (string?)i["type"] == "image"))
            if ((string?)item["imageId"] is { } iid && m.Get(iid) is { Class: Images.Class } image)
            {
                item["data"] = image.Str("data");
                item["mime"] = image.Str("mime");
                item["name"] = image.Name;
                item.Remove("imageId");
            }
        return new JsonObject { ["format"] = "lapin.titleBlock", ["version"] = 1, ["name"] = NameOf(m, id), ["template"] = json };
    }

    static JsonObject Summary(IModelView m, string id, string name, bool builtin, string? description = null)
    {
        var o = new JsonObject
        {
            ["id"] = id, ["name"] = name, ["builtin"] = builtin, ["default"] = DefaultId(m) == id,
            ["uses"] = m.OfClass(Sheets.Class).Count(s => AssignedId(m, s) == id),
        };
        if (description != null) o["description"] = description;
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Title block", [], "#555555", Kind: "resource"));

        // The title blocks: Lapin's, then the project's; which is the default, how many sheets use each.
        r.Query("titleBlock.list", (q, _) =>
        {
            var m = q.Model;
            var list = new JsonArray();
            foreach (var b in BuiltinTitleBlocks.All) list.Add((JsonNode)Summary(m, b.Id, b.Name, true, b.Description));
            foreach (var e in m.OfClass(Class).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)) list.Add((JsonNode)Summary(m, e.Id, e.Name, false));
            return new JsonObject { ["items"] = list, ["default"] = DefaultId(m) };
        });

        r.Query("titleBlock.get", (q, a) =>
        {
            var id = (string?)a["id"] ?? "";
            var t = TemplateById(q.Model, id) ?? throw new CommandException("There is no such title block.");
            return new JsonObject { ["id"] = id, ["name"] = NameOf(q.Model, id), ["builtin"] = BuiltinTitleBlocks.Is(id), ["template"] = t.ToJson() };
        });

        r.Query("titleBlock.export", (q, a) => Export(q.Model, (string?)a["id"] ?? ""));

        // The tokens to insert with their values on a sheet (the first one when none is named), and the project's text styles.
        r.Query("titleBlock.fields", (q, a) =>
        {
            var sheet = q.Model.Get((string?)a["sheetId"] ?? "") is { Class: Sheets.Class } s ? s : q.Model.OfClass(Sheets.Class).FirstOrDefault();
            var book = Lapin.Drawing.Standards.Read(Spatial.Project(q.Model)?.Attrs["drafting"]);
            return new JsonObject
            {
                ["sheetId"] = sheet?.Id,
                ["fields"] = new JsonArray(FieldList(q, sheet).Select(f => (JsonNode)new JsonObject { ["key"] = f.Key, ["label"] = f.Label, ["group"] = f.Group, ["value"] = f.Value }).ToArray()),
                ["textStyles"] = new JsonArray(book.TextStyles.Select(t => (JsonNode)new JsonObject { ["id"] = t.Id, ["name"] = t.Name, ["height"] = t.Height, ["bold"] = t.Bold, ["italic"] = t.Italic, ["font"] = t.Font }).ToArray()),
                ["patterns"] = new JsonArray(Lapin.Drawing.Hatch.HatchLibrary.BuiltInPatterns.Concat(DraftingResources.Patterns(q.Model)).Select(p => p.Name).Distinct().Select(n => (JsonNode)n).ToArray()),
            };
        });

        // A block alone, drawn as a sheet would (a template given, or one by id), for the list and the designer.
        r.Query("titleBlock.preview", (q, a) =>
        {
            var t = a["template"] is JsonObject tj ? TitleBlockTemplate.Parse(tj) : TemplateById(q.Model, (string?)a["id"] ?? "") ?? throw new CommandException("There is no such title block.");
            var sheet = q.Model.Get((string?)a["sheetId"] ?? "") is { Class: Sheets.Class } s ? s : q.Model.OfClass(Sheets.Class).FirstOrDefault();
            var (w, h, pens) = Preview(q, t, sheet);
            return new JsonObject { ["w"] = w, ["h"] = h, ["pens"] = new JsonArray(pens.Select(p => (JsonNode)p.ToJson()).ToArray()) };
        });

        // A new title block: an empty box, a copy of another (a built-in or the project's), or a template given (a file's).
        r.Command("titleBlock.create", "New title block", (ctx, a) =>
        {
            TitleBlockTemplate t;
            string name;
            if ((string?)a["from"] is { Length: > 0 } from)
            {
                t = TemplateById(ctx.Model, from) ?? throw new CommandException("There is no such title block to copy.");
                name = (string?)a["name"] is { Length: > 0 } n0 ? n0 : NameOf(ctx.Model, from) + " copy";
            }
            else
            {
                t = a["template"] is JsonObject tj ? TitleBlockTemplate.Parse(tj) : new TitleBlockTemplate(180, 50, "bottomRight", new TitleBlockFrame(), "above", [TitleBlockTemplate.Rect(0, 0, 180, 50, 0.5)]);
                name = (string?)a["name"] is { Length: > 0 } n1 ? n1 : "Title block";
            }
            t = WithImages(ctx, t);
            var e = new Entity(ctx.NewId(), Class, FreeName(ctx.Model, name.Trim()), new JsonObject { ["template"] = t.ToJson() });
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id, ["name"] = e.Name };
        });

        // A title block file read in (Export's JSON): its name and template, its images made the project's.
        r.Command("titleBlock.import", "Import title block", (ctx, a) =>
        {
            var json = a["json"] as JsonObject ?? throw new CommandException("Which title block?");
            if (json["template"] is not JsonObject tj) throw new CommandException("That is not a title block file.");
            var t = WithImages(ctx, TitleBlockTemplate.Parse(tj));
            var e = new Entity(ctx.NewId(), Class, FreeName(ctx.Model, ((string?)a["name"] ?? (string?)json["name"])?.Trim() is { Length: > 0 } n ? n : "Title block"), new JsonObject { ["template"] = t.ToJson() });
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id, ["name"] = e.Name };
        });

        // A title block's name or template changed, one undo step.
        r.Command("titleBlock.update", "Edit title block", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var attrs = e.CloneAttrs();
            if (a["template"] is JsonObject tj) attrs["template"] = WithImages(ctx, TitleBlockTemplate.Parse(tj)).ToJson();
            var name = (string?)a["name"] is { } n ? n.Trim() is { Length: > 0 } nn ? FreeName(ctx.Model, nn, e.Id) : throw new CommandException("A title block needs a name.") : null;
            ctx.Replace(e.With(name: name, attrs: attrs));
            return null;
        });

        // A title block deleted: its sheets go back to the project's default, the project's default back to Lapin standard.
        r.Command("titleBlock.delete", "Delete title block", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            foreach (var s in ctx.Model.OfClass(Sheets.Class).Where(s => s.Str(SheetKey) == e.Id).ToList())
            {
                var attrs = s.CloneAttrs();
                attrs.Remove(SheetKey);
                ctx.Replace(s.With(attrs: attrs));
            }
            if (Spatial.Project(ctx.Model) is { } p && p.Str(DefaultKey) == e.Id)
            {
                var attrs = p.CloneAttrs();
                attrs.Remove(DefaultKey);
                ctx.Replace(p.With(attrs: attrs));
            }
            ctx.Remove(e.Id);
            return null;
        });

        // The project's default title block (Lapin standard: none set).
        r.Command("titleBlock.setDefault", "Default title block", (ctx, a) =>
        {
            var id = (string?)a["id"] ?? BuiltinTitleBlocks.Standard;
            if (TemplateById(ctx.Model, id) == null) throw new CommandException("There is no such title block.");
            var p = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = p.CloneAttrs();
            if (id == BuiltinTitleBlocks.Standard) attrs.Remove(DefaultKey); else attrs[DefaultKey] = id;
            ctx.Replace(p.With(attrs: attrs));
            return null;
        });

        // A sheet's own title block; none (null or empty): the project's default.
        r.Command("sheet.setTitleBlock", "Sheet title block", (ctx, a) =>
        {
            var ids = a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToList() : [(string?)a["id"] ?? ""];
            var tb = (string?)a["titleBlockId"] is { Length: > 0 } t ? t : null;
            if (tb != null && TemplateById(ctx.Model, tb) == null) throw new CommandException("There is no such title block.");
            foreach (var id in ids)
            {
                var sheet = ctx.Model.Get(id) is { Class: Sheets.Class } s ? s : throw new CommandException("There is no such sheet.");
                var attrs = sheet.CloneAttrs();
                if (tb == null) attrs.Remove(SheetKey); else attrs[SheetKey] = tb;
                ctx.Replace(sheet.With(attrs: attrs));
            }
            return null;
        });
    }
}
