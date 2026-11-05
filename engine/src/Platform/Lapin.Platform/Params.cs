using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>The user's own parameters: their kinds, and a value checked against its kind (lengths in metres, angles in degrees).</summary>
public static class Params
{
    public static readonly string[] Kinds = ["text", "number", "length", "area", "angle", "bool", "choice"];

    /// <summary>A stored parameter's set: null for the user's own (Lapin_Parameters).</summary>
    public static string? SetOf(JsonObject p) => (string?)p["pset"] is { Length: > 0 } s && s != StandardPsets.Own ? s : null;

    /// <summary>Whether a stored parameter is the one named: the same name (case aside) in the same set.</summary>
    public static bool Same(JsonObject p, string? pset, string name) =>
        string.Equals((string?)p["name"], name, StringComparison.OrdinalIgnoreCase) && string.Equals(SetOf(p), pset, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A whole list of parameters as a command gives it ([{name, kind, value, pset?, options?}], a material's), each checked as
    /// element.setParam checks one: a standard set's property only by its own name and held as its kind, a set of the user's
    /// never „Pset_”; the same name twice in one set refused. Stored as given, in order.
    /// </summary>
    public static JsonArray ParseList(JsonNode? node, string ifcClass)
    {
        if (node is null) return [];
        if (node is not JsonArray arr) throw new CommandException("Parameters are a list.");
        var o = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in arr)
        {
            if (n is not JsonObject p) throw new CommandException("Every parameter is an object: name, kind and value.");
            var name = ((string?)p["name"])?.Trim() is { Length: > 0 } nm ? nm : throw new CommandException("A parameter needs a name.");
            var (pset, standardKind) = StandardPsets.Check(ifcClass, (string?)p["pset"], name);
            if (!seen.Add($"{pset}\u001f{name}")) throw new CommandException($"„{name}” is there twice.");
            var kind = standardKind ?? (string?)p["kind"] ?? throw new CommandException($"„{name}” needs its kind ({string.Join(", ", Kinds)}).");
            var options = p["options"] as JsonArray;
            var entry = new JsonObject { ["name"] = name, ["kind"] = kind, ["value"] = Check(kind, p["value"], options) };
            if (pset != null) entry["pset"] = pset;
            if (kind == "choice" && options != null) entry["options"] = options.DeepClone();
            o.Add((JsonNode)entry);
        }
        return o;
    }

    public static JsonNode? Check(string kind, JsonNode? value, JsonArray? options)
    {
        if (!Kinds.Contains(kind)) throw new CommandException($"„{kind}” is not a kind of parameter ({string.Join(", ", Kinds)}).");
        if (value is null) return null;
        switch (kind)
        {
            case "text": return JsonValue.Create(value.ToString());
            case "bool": return value is JsonValue b && b.TryGetValue<bool>(out var v) ? JsonValue.Create(v) : throw new CommandException("Yes or no.");
            case "choice":
            {
                var text = value.ToString();
                if (options is { Count: > 0 } && !options.Any(o => (string?)o == text)) throw new CommandException($"„{text}” is not one of the choices.");
                return JsonValue.Create(text);
            }
            default:
            {
                var n = Json.Number(value) ?? throw new CommandException("A number.");
                if (double.IsNaN(n) || double.IsInfinity(n)) throw new CommandException("A number.");
                if (kind is "length" or "area" && n < 0) throw new CommandException("Not below zero.");
                return JsonValue.Create(n);
            }
        }
    }
}
