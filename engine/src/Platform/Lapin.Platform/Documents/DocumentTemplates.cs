using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Documents;

/// <summary>An input a template asks the user to write (the existing situation, the structure): its key, label, whether it takes several lines, its default text and the group the dialog files it under.</summary>
public sealed record DocInput(string Key, string Label, bool Multiline = true, string? Default = null, string? Group = null)
{
    public JsonObject ToJson() => new() { ["key"] = Key, ["label"] = Label, ["multiline"] = Multiline, ["default"] = Default, ["group"] = Group };

    public static DocInput? From(JsonNode? n) => n is JsonObject o && (string?)o["key"] is { Length: > 0 } key
        ? new DocInput(key, (string?)o["label"] ?? key, o["multiline"] is not JsonValue v || !v.TryGetValue<bool>(out var ml) || ml, (string?)o["default"], (string?)o["group"])
        : null;
}

/// <summary>
/// A document template (docs/plan-documents.md): a document's shape with an id, a title and a description, the inputs it declares,
/// and the ISO 19650 type its issued file is named with (<c>isoType</c>: RP a report, SP a specification; RP when not said).
/// </summary>
public sealed record DocTemplate(string Id, string Title, string Description, string Lang, JsonObject Page, IReadOnlyList<DocInput> Inputs, JsonArray Blocks, string IsoType = "RP")
{
    public JsonObject Summary() => new()
    {
        ["id"] = Id, ["title"] = Title, ["description"] = Description, ["lang"] = Lang, ["isoType"] = IsoType,
        ["inputs"] = new JsonArray(Inputs.Select(i => (JsonNode)i.ToJson()).ToArray()),
    };
}

/// <summary>
/// The templates a project's documents start from: the built-in ones, JSON resources of this assembly under
/// <c>Documents/Templates/</c> (read once), and any template given as JSON (a company's, stage 4; a test's own).
/// </summary>
public static class DocumentTemplates
{
    const string Prefix = "Lapin.Platform.Documents.Templates.";

    static readonly Lazy<IReadOnlyList<DocTemplate>> _builtIn = new(() =>
    {
        var asm = typeof(DocumentTemplates).Assembly;
        var o = new List<DocTemplate>();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var s = asm.GetManifestResourceStream(name);
            if (s == null) continue;
            using var reader = new StreamReader(s);
            // A broken resource is the build's mistake, not the user's: left out rather than failing every document.
            try { o.Add(Parse(reader.ReadToEnd())); }
            catch (Exception e) when (e is CommandException or JsonException) { Console.Error.WriteLine($"[documents] template {name}: {e.Message}"); }
        }
        return o;
    });

    /// <summary>The templates Lapin comes with, in the order of their resource names.</summary>
    public static IReadOnlyList<DocTemplate> BuiltIn => _builtIn.Value;

    public static DocTemplate? Find(string? id) => BuiltIn.FirstOrDefault(t => t.Id == id);

    /// <summary>A template from its JSON text.</summary>
    public static DocTemplate Parse(string json) =>
        From(JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }));

    /// <summary>A template from its JSON (an object with at least an id and blocks), checked.</summary>
    public static DocTemplate From(JsonNode? node)
    {
        if (node is not JsonObject o) throw new CommandException("A template is a JSON object.");
        var id = ((string?)o["id"])?.Trim() is { Length: > 0 } i ? i : throw new CommandException("A template needs an id.");
        if (o["blocks"] is not JsonArray blocks) throw new CommandException($"The template „{id}” has no blocks.");
        DocumentBlocks.Check(blocks);
        var inputs = o["inputs"] is JsonArray ins ? ins.Select(DocInput.From).OfType<DocInput>().ToList() : [];
        return new DocTemplate(id, (string?)o["title"] ?? id, (string?)o["description"] ?? "", (string?)o["lang"] is { Length: > 0 } l ? l : "ro",
            o["page"] is JsonObject page ? (JsonObject)page.DeepClone() : [], inputs, (JsonArray)blocks.DeepClone(),
            (string?)o["isoType"] is { Length: 2 } iso ? iso.ToUpperInvariant() : "RP");
    }

    /// <summary>The template a command names: by id among the built-in ones, or given whole as JSON.</summary>
    public static DocTemplate Resolve(JsonNode? given) => given switch
    {
        JsonObject o => From(o),
        JsonValue v when v.TryGetValue<string>(out var id) => Find(id) ?? throw new CommandException($"There is no template „{id}”."),
        _ => throw new CommandException("Which template?"),
    };
}

/// <summary>The blocks of a document as kept (docs/plan-documents.md, Blocurile): what kinds there are, and a check of their shape.</summary>
public static class DocumentBlocks
{
    public static readonly string[] Kinds = ["heading", "paragraph", "list", "fields", "table", "image", "repeat", "if", "input", "toc", "pageBreak"];

    /// <summary>Refuses blocks a document cannot fill: an unknown kind, a repeat without its collection, an if without its field, nested blocks not a list.</summary>
    public static void Check(JsonArray blocks, int depth = 0)
    {
        if (depth > 16) throw new CommandException("The blocks nest too deep.");
        foreach (var n in blocks)
        {
            if (n is not JsonObject b) throw new CommandException("A block is a JSON object.");
            var kind = (string?)b["kind"];
            if (kind == null || !Kinds.Contains(kind)) throw new CommandException($"„{kind}” is not a kind of block ({string.Join(", ", Kinds)}).");
            switch (kind)
            {
                case "repeat" when (string?)b["over"] is not { Length: > 0 }: throw new CommandException("A repeat block says what it repeats over.");
                case "if" when (string?)b["field"] is not { Length: > 0 }: throw new CommandException("An if block says which field it tests.");
                case "input" when (string?)b["key"] is not { Length: > 0 }: throw new CommandException("An input block says which input it shows.");
                case "table" when (string?)b["source"] is not { Length: > 0 }: throw new CommandException("A table block says which table it shows.");
                case "image" when (string?)b["view"] is not { Length: > 0 }: throw new CommandException("An image block says which view it shows.");
            }
            foreach (var key in new[] { "blocks", "else" })
                if (b[key] is { } inner)
                {
                    if (inner is not JsonArray a) throw new CommandException($"A block's {key} is a list of blocks.");
                    Check(a, depth + 1);
                }
        }
    }
}
