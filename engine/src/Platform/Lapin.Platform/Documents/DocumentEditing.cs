using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Documents;

/// <summary>A repeat's item a block is filled inside (the editor's): its collection, the item's index from 0, the repeat's alias and where.</summary>
public sealed record DocScope(string Collection, int Index = 0, string? As = null, JsonObject? Where = null)
{
    public static DocScope From(JsonNode? n)
    {
        if (n is not JsonObject o) throw new CommandException("A scope is {collection, index?, as?, where?}.");
        var c = ((string?)o["collection"] ?? (string?)o["over"])?.Trim() is { Length: > 0 } x ? x : throw new CommandException("A scope says its collection.");
        var index = Json.Number(o["index"]) is { } i && i >= 0 ? (int)i : 0;
        return new DocScope(c, index, (string?)o["as"], o["where"] as JsonObject);
    }

    /// <summary>One scope or a chain of them, outer first; none when not given.</summary>
    public static List<DocScope> Chain(JsonNode? n) => n switch
    {
        null => [],
        JsonArray a => a.Select(From).ToList(),
        _ => [From(n)],
    };
}

/// <summary>
/// The document editor's side of the engine (docs/plan-documents.md, Etapa 2): a document's own blocks and input definitions as
/// kept (<c>document.source</c>), one block filled in the document's context, inside a repeat's item when asked
/// (<c>document.block</c>), and the input definitions the editor saves with the blocks (<c>document.update {inputDefs}</c>).
/// </summary>
public static class DocumentEditing
{
    /// <summary>What an input's key may hold: letters, digits, '_', '-' and '.', as a {{field}} is written.</summary>
    static bool IsKey(string k) => k.Length is > 0 and <= 64 && k.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.');

    /// <summary>The input definitions an editor gives, checked (a key each, unique, a label) and written as the document keeps them.</summary>
    public static JsonArray InputDefs(JsonArray defs)
    {
        var o = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in defs)
        {
            if (n is not JsonObject d) throw new CommandException("An input definition is {key, label, multiline?, default?, group?}.");
            var key = ((string?)d["key"])?.Trim() ?? "";
            if (!IsKey(key)) throw new CommandException($"„{key}” is not an input's key (letters, digits, _ - .).");
            if (!seen.Add(key)) throw new CommandException($"The input „{key}” is defined twice.");
            if (d["label"] is { } l && l.GetValueKind() != System.Text.Json.JsonValueKind.String) throw new CommandException($"The input „{key}”'s label is text.");
            var label = ((string?)d["label"])?.Trim() is { Length: > 0 } x ? x : key;
            var group = ((string?)d["group"])?.Trim() is { Length: > 0 } g ? g : null;
            var multiline = d["multiline"] is not JsonValue v || !v.TryGetValue<bool>(out var ml) || ml;
            o.Add((JsonNode)new DocInput(key, label, multiline, (string?)d["default"], group).ToJson());
        }
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        // A document as kept, for the editor: its page, its blocks and inputs' definitions (its copy of the template's), its inputs.
        r.Query("document.source", (q, a) =>
        {
            var doc = Documents.Require(q.Model, (string?)a["id"]);
            return new JsonObject
            {
                ["id"] = doc.Id, ["name"] = doc.Name, ["lang"] = doc.Str("lang"),
                ["page"] = doc.Attrs["page"]?.DeepClone() ?? new JsonObject(),
                ["blocks"] = doc.Attrs["blocks"]?.DeepClone() ?? new JsonArray(),
                ["inputDefs"] = doc.Attrs["inputDefs"]?.DeepClone() ?? new JsonArray(),
                ["inputs"] = doc.Attrs["inputs"]?.DeepClone() ?? new JsonObject(),
            };
        });

        // One block (or a few) filled as document.preview fills them, inside the repeats' items its scope names (the first item
        // when no index is given): the filled blocks, the fields they leave to complete, the items' values by full key and how
        // many items each repeat of the scope keeps. Without a block, only the scope's values (the editor's field chips).
        r.Query("document.block", (q, a) =>
        {
            var doc = Documents.Require(q.Model, (string?)a["id"]);
            var blocks = a["blocks"] is JsonArray many ? (JsonArray)many.DeepClone()
                : a["block"] is JsonObject one ? new JsonArray(one.DeepClone())
                : a["block"] is null ? [] : throw new CommandException("A block is a JSON object.");
            DocumentBlocks.Check(blocks);
            var (filled, missing, scope, counts) = DocumentFill.FillPart(q, doc, blocks, DocScope.Chain(a["scope"]));
            var values = new JsonObject();
            foreach (var (k, v) in scope.OrderBy(x => x.Key, StringComparer.Ordinal)) values[k] = v;
            return new JsonObject
            {
                ["blocks"] = new JsonArray(filled.Select(b => (JsonNode)DocumentJson.Block(b)).ToArray()),
                ["missing"] = new JsonArray(missing.Select(m => (JsonNode)m).ToArray()),
                ["scope"] = values,
                ["items"] = new JsonArray(counts.Select(c => (JsonNode)c).ToArray()),
            };
        });
    }
}
