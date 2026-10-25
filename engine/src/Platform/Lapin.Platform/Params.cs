using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>The user's own parameters: their kinds, and a value checked against its kind (lengths in metres, angles in degrees).</summary>
public static class Params
{
    public static readonly string[] Kinds = ["text", "number", "length", "area", "angle", "bool", "choice"];

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
