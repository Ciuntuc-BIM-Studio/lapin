using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Documents;

/// <summary>
/// The project's written documents (docs/plan-documents.md): a <c>LapinDocument</c> entity (a resource, never exported to IFC)
/// started from a template, keeping its own copy of the template's page, blocks and declared inputs, the user's inputs, its
/// language, and the revision and status its last issue gave it. Filled from the project each time it is shown or written
/// (<see cref="DocumentFill"/>); "Update from template" takes the template's blocks again and keeps the inputs.
/// </summary>
public static class Documents
{
    public const string Class = "LapinDocument";

    public static IEnumerable<Entity> All(IModelView m) => m.OfClass(Class);

    public static Entity Require(IModelView m, string? id) => m.Get(id ?? "") is { Class: Class } d ? d : throw new CommandException("There is no such document.");

    /// <summary>The ISO 19650 type a document's issued file is named with: its template's (RP a report, SP a specification), RP by default.</summary>
    public static string IsoType(Entity doc) => doc.Str("isoType") is { Length: 2 } t ? t : "RP";

    /// <summary>A name no other document has: the one asked, else with a number after it.</summary>
    static string Unique(IModelView m, string name, string? except = null)
    {
        var taken = All(m).Where(d => d.Id != except).Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name)) return name;
        for (var i = 2; ; i++) if (!taken.Contains($"{name} {i}")) return $"{name} {i}";
    }

    /// <summary>A template's shape written onto a document's attributes (its page, blocks, declared inputs, type), the document's own left as they are.</summary>
    static void FromTemplate(JsonObject attrs, DocTemplate t)
    {
        attrs["template"] = t.Id;
        attrs["templateTitle"] = t.Title;
        attrs["isoType"] = t.IsoType;
        attrs["page"] = t.Page.DeepClone();
        attrs["blocks"] = t.Blocks.DeepClone();
        attrs["inputDefs"] = new JsonArray(t.Inputs.Select(i => (JsonNode)i.ToJson()).ToArray());
    }

    public static Entity Create(ICommandContext ctx, DocTemplate t, string? name)
    {
        var attrs = new JsonObject { ["lang"] = t.Lang, ["inputs"] = new JsonObject() };
        FromTemplate(attrs, t);
        var doc = new Entity(ctx.NewId(), Class, Unique(ctx.Model, name?.Trim() is { Length: > 0 } n ? n : t.Title), attrs);
        ctx.Add(doc);
        return doc;
    }

    public static JsonObject Summary(Entity d) => new()
    {
        ["id"] = d.Id, ["name"] = d.Name, ["template"] = d.Str("template"), ["templateTitle"] = d.Str("templateTitle"), ["lang"] = d.Str("lang"),
        ["revision"] = d.Str("revision"), ["status"] = d.Str("status"), ["isoType"] = IsoType(d),
    };

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Document", [], "#4a6fa5", Kind: "resource", Template: false));
        r.Service<IDocumentFields>(new CoreDocumentFields());
        ProjectInfo.Register(r);
        DocumentEditing.Register(r);
        DocumentTemplateStore.Register(r);

        // A document from a template: a built-in one by its id, or one given whole (a company's, stage 4).
        r.Command("document.create", "New document", (ctx, a) =>
        {
            var doc = Create(ctx, DocumentTemplateStore.Resolve(ctx.Model, a["template"]), (string?)a["name"]);
            return new JsonObject { ["id"] = doc.Id };
        });

        // A document's name, language, page, blocks and input definitions replaced as given; its inputs merged (a null value removes one).
        r.Command("document.update", "Change document", (ctx, a) =>
        {
            var doc = Require(ctx.Model, (string?)a["id"]);
            var attrs = doc.CloneAttrs();
            if (a.ContainsKey("lang")) attrs["lang"] = ((string?)a["lang"])?.Trim() is { Length: > 0 } l ? l : "ro";
            if (a["page"] is JsonObject page) attrs["page"] = page.DeepClone();
            if (a["blocks"] is JsonArray blocks)
            {
                DocumentBlocks.Check(blocks);
                DocumentImages.CheckBoundaries(ctx.Model, blocks);
                attrs["blocks"] = blocks.DeepClone();
            }
            if (a["inputDefs"] is JsonArray defs) attrs["inputDefs"] = DocumentEditing.InputDefs(defs);
            if (a["inputs"] is JsonObject inputs)
            {
                var kept = (attrs["inputs"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
                foreach (var (k, v) in inputs)
                    if (v is JsonValue s && s.TryGetValue<string>(out var text)) kept[k] = text; else kept.Remove(k);
                attrs["inputs"] = kept;
            }
            string? name = null;
            if (a.ContainsKey("name")) name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? Unique(ctx.Model, n, doc.Id) : throw new CommandException("A document needs a name.");
            ctx.Replace(doc.With(name: name, attrs: attrs));
            return null;
        });

        r.Command("document.delete", "Delete document", (ctx, a) =>
        {
            ctx.Remove(Require(ctx.Model, (string?)a["id"]).Id);
            return null;
        });

        // A copy under a new name, its inputs with it; not issued yet (no revision or status).
        r.Command("document.duplicate", "Duplicate document", (ctx, a) =>
        {
            var doc = Require(ctx.Model, (string?)a["id"]);
            var attrs = doc.CloneAttrs();
            attrs.Remove("revision");
            attrs.Remove("status");
            var copy = new Entity(ctx.NewId(), Class, Unique(ctx.Model, ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : $"{doc.Name} (copy)"), attrs);
            ctx.Add(copy);
            return new JsonObject { ["id"] = copy.Id };
        });

        // The template's page, blocks and inputs again (the template given, else the one it came from), the user's inputs kept.
        r.Command("document.refresh", "Update from template", (ctx, a) =>
        {
            var doc = Require(ctx.Model, (string?)a["id"]);
            var t = a["template"] is JsonObject given ? DocumentTemplates.From(given)
                : DocumentTemplateStore.Find(ctx.Model, doc.Str("template")) ?? throw new CommandException($"The template „{doc.Str("template")}” is not in this Lapin.");
            var attrs = doc.CloneAttrs();
            FromTemplate(attrs, t);
            ctx.Replace(doc.With(attrs: attrs));
            return null;
        });

        r.Query("document.list", (q, _) => new JsonArray(All(q.Model).Select(d => (JsonNode)Summary(d)).ToArray()));

        // What a document can read: every field with its label and value now (the document's own too, given its id), the collections, the tables.
        r.Query("document.fields", (q, a) =>
        {
            var doc = (string?)a["id"] is { } id ? Require(q.Model, id) : null;
            var f = doc != null ? DocumentFill.FormatOf(q.Model, doc) : new DocFormat((string?)a["lang"] ?? "ro", DraftingResources.Book(q.Model).Units, DraftingResources.Book(q.Model).AreaRounding);
            var fields = (doc != null ? DocumentFill.OwnFields(doc) : []).Concat(DocumentFill.AllFields(q, f));
            var tables = q.Services.Get<IDocumentTables>() ?? new ScheduleSourceTables();
            return new JsonObject
            {
                ["fields"] = new JsonArray(fields.Select(x => (JsonNode)new JsonObject { ["key"] = x.Key, ["label"] = x.Label, ["value"] = x.Value }).ToArray()),
                ["collections"] = new JsonArray(DocumentFill.AllCollections(q).Select(c => (JsonNode)new JsonObject
                {
                    ["key"] = c.Key, ["label"] = c.Label, ["itemPrefix"] = c.ItemPrefix,
                    // The fields its items have (those of the first), for the editor to offer.
                    ["fields"] = new JsonArray(DocumentFill.Items(q, c.Key, f).FirstOrDefault()?.Keys.Select(k => (JsonNode)k).ToArray() ?? []),
                    ["count"] = DocumentFill.Items(q, c.Key, f).Count,
                }).ToArray()),
                ["tables"] = new JsonArray(tables.Choices(q).Select(t => (JsonNode)new JsonObject { ["source"] = t.Source, ["label"] = t.Label }).ToArray()),
            };
        });

        r.Query("document.preview", (q, a) =>
        {
            var doc = Require(q.Model, (string?)a["id"]);
            var (filled, missing) = DocumentFill.Fill(q, doc);
            var o = DocumentJson.Document(filled, missing, doc);
            o["summary"] = Summary(doc);
            return o;
        });

        // The pictures' pens, for the interface to make their SVG and PNG for an export.
        r.Query("document.images", (q, a) =>
        {
            var (filled, _) = DocumentFill.Fill(q, Require(q.Model, (string?)a["id"]));
            return new JsonArray(DocumentJson.Images(filled).Select(i => (JsonNode)DocumentJson.Image(i)).ToArray());
        });

        r.Query("document.export", (q, a) => DocumentExport.Export(q, Require(q.Model, (string?)a["id"]), (string?)a["format"], a["images"]));
    }
}
