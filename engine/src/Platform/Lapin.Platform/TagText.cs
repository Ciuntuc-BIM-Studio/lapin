using System.Globalization;
using System.Text.RegularExpressions;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A tag's text from its type's template: each {field} read from the element
/// (mark, name, type, class), from what the caller adds (a door's {width},
/// {height}, {sill}, already written in the tag's unit), else from the
/// element's or its type's attribute of that name; an unknown field is left
/// empty. Fields are case-free. "|" separates lines.
/// </summary>
public static partial class TagText
{
    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex Field();

    /// <summary>The element's mark: its own, its type's, else its type's name, else its name.</summary>
    public static string Mark(IModelView m, Entity e)
    {
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        return e.Str("mark") ?? type?.Str("mark") ?? type?.Name ?? e.Name;
    }

    public static string Value(IModelView m, Entity e, string field, IReadOnlyDictionary<string, string>? extra = null)
    {
        var key = field.Trim();
        if (extra != null && extra.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) is { Key: not null } x) return x.Value;
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        switch (key.ToLowerInvariant())
        {
            case "mark": case "tag": return Mark(m, e);
            case "name": return e.Name;
            case "type": return type?.Name ?? e.Name;
            case "class": return e.Class.StartsWith("Ifc", StringComparison.Ordinal) ? e.Class[3..] : e.Class;
        }
        string? Of(Entity? x) => x?.Attrs.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)).Value switch
        {
            System.Text.Json.Nodes.JsonValue v when v.TryGetValue<string>(out var s) => s,
            System.Text.Json.Nodes.JsonValue v when Json.Number(v) is { } n => n.ToString("0.###", CultureInfo.InvariantCulture),
            _ => null,
        };
        return Of(e) ?? Of(type) ?? "";
    }

    /// <summary>The template filled in, one string per line ("|"), empty lines dropped.</summary>
    public static IReadOnlyList<string> Lines(string template, IModelView m, Entity e, IReadOnlyDictionary<string, string>? extra = null) =>
        Field().Replace(template, x => Value(m, e, x.Groups[1].Value, extra))
            .Split('|').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    /// <summary>A length (metres) as a tag writes it: whole centimetres or millimetres (a tenth where it is not whole), metres to the centimetre.</summary>
    public static string Length(double metres, string unit)
    {
        var (v, f) = unit switch { "mm" => (metres * 1000, "0.#"), "m" => (metres, "0.00"), _ => (metres * 100, "0.#") };
        return Math.Round(v, 3).ToString(f, CultureInfo.InvariantCulture);
    }
}
