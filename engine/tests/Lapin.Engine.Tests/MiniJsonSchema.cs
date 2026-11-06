using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Engine.Tests;

/// <summary>
/// The part of JSON Schema (draft 2020-12) docs/jg-structures/schema-1.0.0.json uses, for the tests only (no production
/// dependency): type (one or a list), required, properties, additionalProperties (a schema), items, minItems, maxItems,
/// minLength, enum, const, oneOf and $ref into #/$defs. Each failure is the path and what it breaks.
/// </summary>
static class MiniJsonSchema
{
    public static List<string> Validate(JsonNode schema, JsonNode? doc)
    {
        var errors = new List<string>();
        Check(schema, schema, doc, "$", errors);
        return errors;
    }

    static void Check(JsonNode root, JsonNode schema, JsonNode? v, string path, List<string> errors)
    {
        if (schema is not JsonObject s) return;
        if ((string?)s["$ref"] is { } r)
        {
            var target = r.TrimStart('#').Split('/', StringSplitOptions.RemoveEmptyEntries).Aggregate((JsonNode?)root, (n, k) => n?[k]);
            if (target == null) errors.Add($"{path}: unresolved $ref {r}");
            else Check(root, target, v, path, errors);
        }
        if (s["type"] is { } t)
        {
            var types = t is JsonArray ta ? ta.Select(x => (string)x!).ToList() : [(string)t!];
            if (!types.Any(x => Is(x, v))) { errors.Add($"{path}: not {string.Join("|", types)}"); return; }
        }
        if (s["const"] is { } c && !JsonNode.DeepEquals(c, v)) errors.Add($"{path}: not {c.ToJsonString()}");
        if (s["enum"] is JsonArray e && !e.Any(x => JsonNode.DeepEquals(x, v))) errors.Add($"{path}: not one of {e.ToJsonString()}");
        if (s["oneOf"] is JsonArray one)
        {
            var passing = one.Count(alt => { var sub = new List<string>(); Check(root, alt!, v, path, sub); return sub.Count == 0; });
            if (passing != 1) errors.Add($"{path}: matches {passing} of oneOf, not exactly one");
        }
        if (v is JsonValue sv && sv.TryGetValue<string>(out var str) && Json(s["minLength"]) is { } ml && str.Length < ml) errors.Add($"{path}: shorter than {ml}");
        if (v is JsonObject o)
        {
            foreach (var req in (s["required"] as JsonArray)?.Select(x => (string)x!) ?? [])
                if (!o.ContainsKey(req)) errors.Add($"{path}: {req} is required");
            var props = s["properties"] as JsonObject;
            foreach (var (k, val) in o)
            {
                if (props?[k] is { } ps) Check(root, ps, val, $"{path}.{k}", errors);
                else if (s["additionalProperties"] is JsonObject extra) Check(root, extra, val, $"{path}.{k}", errors);
            }
        }
        if (v is JsonArray a)
        {
            if (Json(s["minItems"]) is { } min && a.Count < min) errors.Add($"{path}: fewer than {min} items");
            if (Json(s["maxItems"]) is { } max && a.Count > max) errors.Add($"{path}: more than {max} items");
            if (s["items"] is { } items) for (var i = 0; i < a.Count; i++) Check(root, items, a[i], $"{path}[{i}]", errors);
        }
    }

    static int? Json(JsonNode? n) => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

    static bool Is(string type, JsonNode? v) => type switch
    {
        "null" => v is null,
        "object" => v is JsonObject,
        "array" => v is JsonArray,
        "string" => v is JsonValue s && s.GetValueKind() == JsonValueKind.String,
        "boolean" => v is JsonValue b && b.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
        "number" => v is JsonValue n && n.GetValueKind() == JsonValueKind.Number,
        "integer" => v is JsonValue i && i.GetValueKind() == JsonValueKind.Number && i.TryGetValue<double>(out var d) && Math.Floor(d) == d,
        _ => false,
    };
}
