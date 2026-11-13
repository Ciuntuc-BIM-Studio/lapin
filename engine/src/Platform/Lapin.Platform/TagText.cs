using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A tag's text from its type's template: each {field} read from the element
/// (mark, name, type, class), from what the caller adds (a door's {width},
/// {height}, {sill}, already written in the tag's unit), else from the user's
/// parameters (the element's own over its type's: {Fire rating}, or one of a
/// standard set {Pset_WallCommon.FireRating}), from the property sets an
/// imported element came with, or from the element's or its type's attribute
/// of that name; an unknown field is left empty. Fields are case-free and a
/// parameter is found without its spaces too ({FireRating} reads "Fire
/// rating" and Pset_WallCommon.FireRating, the user's own first), else from
/// the project's own parameters by their key. A scope before a dot reads
/// another entity as the element (decision 551, <see cref="ParamScopes"/>):
/// {storey.code}, {project.number}, {host.FireRating}, {room.name}. A field
/// {= …} is a formula (decision 550): {= upper(storey.code) &amp; "." &amp; number},
/// {= area * 1.1 | 0.00}. "|" separates lines.
/// </summary>
public static partial class TagText
{
    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex Field();

    /// <summary>The fields every element has.</summary>
    public static readonly string[] Builtin = ["mark", "name", "class", "type"];

    /// <summary>The element's mark: its own, its type's, else its type's name, else its name.</summary>
    public static string Mark(IModelView m, Entity e)
    {
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        return e.Str("mark") ?? type?.Str("mark") ?? type?.Name ?? e.Name;
    }

    /// <summary>A name as fields compare it: no case, no spaces, dashes or underscores ("Fire rating" = "FireRating").</summary>
    public static string Squash(string s) => new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    static IEnumerable<JsonObject> Stored(Entity? e) => e?.Attrs["params"] is JsonArray a ? a.OfType<JsonObject>() : [];

    /// <summary>A stored parameter of <paramref name="e"/> the field names: "Set.Name" in that set, else a name in any set, the user's own first.</summary>
    static JsonObject? Param(Entity? e, string key) => Param(Stored(e), key);

    /// <summary>The same among parameters given (with a value, or a formula to work it out).</summary>
    static JsonObject? Param(IEnumerable<JsonObject> stored, string key)
    {
        var list = stored.Where(p => p["value"] != null || ParamFormulas.IsFormula(p)).ToList();
        var dot = key.IndexOf('.');
        if (dot > 0)
        {
            var (set, name) = (key[..dot].Trim(), Squash(key[(dot + 1)..]));
            var setOwn = string.Equals(set, StandardPsets.Own, StringComparison.OrdinalIgnoreCase);
            var hit = list.FirstOrDefault(p => Squash((string?)p["name"] ?? "") == name
                && (setOwn ? Params.SetOf(p) == null : string.Equals(Params.SetOf(p), set, StringComparison.OrdinalIgnoreCase)));
            if (hit != null) return hit;
        }
        var k = Squash(key);
        return list.Where(p => Squash((string?)p["name"] ?? "") == k).OrderBy(p => Params.SetOf(p) == null ? 0 : 1).FirstOrDefault();
    }

    /// <summary>A value an imported element's property sets hold ({"Pset_WallCommon": {"FireRating": "REI 90"}}): "Set.Name", else the name in any set.</summary>
    static string? Imported(Entity? e, string key)
    {
        if (e?.Attrs["properties"] is not JsonObject sets) return null;
        var dot = key.IndexOf('.');
        string? set = dot > 0 ? key[..dot].Trim() : null;
        var name = Squash(dot > 0 ? key[(dot + 1)..] : key);
        foreach (var (s, props) in sets)
        {
            if (props is not JsonObject po || set != null && !string.Equals(s, set, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var (n, v) in po)
                if (Squash(n) == name && v != null)
                    return v is JsonValue jv && jv.TryGetValue<string>(out var str) ? str
                        : v is JsonValue b && b.TryGetValue<bool>(out var flag) ? (flag ? "yes" : "no")
                        : Json.Number(v) is { } num ? num.ToString("0.###", CultureInfo.InvariantCulture)
                        : v is JsonObject vo && vo["value"] is JsonValue inner ? inner.ToString() : v.ToJsonString();
        }
        return null;
    }

    /// <summary>A stored parameter's value as a tag writes it: a length in the project's units, an area in m², an angle in degrees, yes or no.</summary>
    public static string ParamText(JsonObject p, string units = "mm")
    {
        var v = p["value"];
        if (v is null) return "";
        var kind = (string?)p["kind"] ?? "text";
        if (kind == "bool") return v is JsonValue b && b.TryGetValue<bool>(out var x) ? (x ? "yes" : "no") : v.ToString();
        if (Json.Number(v) is { } n)
            return kind switch
            {
                "length" => Length(n, "project", units),
                "area" => n.ToString("0.##", CultureInfo.InvariantCulture) + " m²",
                "angle" => n.ToString("0.#", CultureInfo.InvariantCulture) + "°",
                _ => n.ToString("0.###", CultureInfo.InvariantCulture),
            };
        return v is JsonValue s && s.TryGetValue<string>(out var str) ? str : v.ToString();
    }

    public static string Value(IModelView m, Entity e, string field, IReadOnlyDictionary<string, string>? extra = null, string units = "mm", Func<string, double?>? more = null)
    {
        var key = field.Trim();
        // A formula as a field ({= area * 1.1 | 0.00}, {= upper(storey.code)}), its names read as a formula's (decision 550).
        if (key.StartsWith('=')) return FormulaText(m, e, key[1..], extra, more);
        if (extra != null && extra.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) is { Key: not null } x) return x.Value;
        // A scope before a dot (decision 551): the storey's {storey.code}, the project's {project.number}, the wall's {host.FireRating}.
        if (ParamScopes.Split(m, e, key) is { } scoped)
            return scoped.Owner is not { } owner ? ""
                : owner.Class == IfcClass.Project && ParamScopes.ProjectValue(m, scoped.Field) is { } pv ? pv
                : Value(m, owner, scoped.Field, null, units);
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        switch (key.ToLowerInvariant())
        {
            case "mark": case "tag": return Mark(m, e);
            case "name": return e.Name;
            case "type": return type?.Name ?? e.Name;
            case "class": return e.Class.StartsWith("Ifc", StringComparison.Ordinal) ? e.Class[3..] : e.Class;
        }
        if (e.Class == IfcClass.Project && ParamScopes.ProjectValue(m, key) is { } info) return info;
        // Its classification's code or title, or one system's code (docs/plan-user-extensibility.md §7.3: a keynote is a tag of it).
        if (Classifications.TagValue(m, e, type, key) is { } classified) return classified;
        // The user's parameters, the element's own over its type's (as the properties panel shows them).
        // A formula's value worked out from the element's other parameters and numbers (docs/plan-user-extensibility.md §5.3), only the one asked.
        if (Param(Stored(e), key) is { } own) return ParamText(ParamFormulas.IsFormula(own) ? ParamFormulas.ResolvedOne(m, e, type, own, more) : own, units);
        if (Param(Stored(type), key) is { } typed) return ParamText(ParamFormulas.IsFormula(typed) ? ParamFormulas.ResolvedOne(m, type!, null, typed) : typed, units);
        if ((Imported(e, key) ?? Imported(type, key)) is { } imported) return imported;
        string? Of(Entity? x) => x?.Attrs.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)).Value switch
        {
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            JsonValue v when Json.Number(v) is { } n => n.ToString("0.###", CultureInfo.InvariantCulture),
            _ => null,
        };
        if ((Of(e) ?? Of(type)) is { } attr) return attr;
        // The project's own parameters are global: read by their key from any element (decision 551).
        var k = Squash(key);
        return ParamScopes.Globals(m).FirstOrDefault(c => Squash(c.Key) == k || Squash(c.Label) == k) is { } g ? g.Printed ?? "" : "";
    }

    /// <summary>A formula field's text: its value written as its format says (after a bar), or &lt;formula&gt; when it does not read.</summary>
    static string FormulaText(IModelView m, Entity e, string body, IReadOnlyDictionary<string, string>? extra, Func<string, double?>? more)
    {
        var (formula, format) = LiveText.SplitFormat(body);
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        var (value, _) = Expr.TryEvalValue(formula, n =>
            extra?.FirstOrDefault(kv => string.Equals(kv.Key, n, StringComparison.OrdinalIgnoreCase)) is { Key: not null } x ? XValue.Read(x.Value)
            : ParamFormulas.Read(m, e, type, more, n));
        return value is { } v ? Expr.Format(v, format) : $"<{formula.Trim()}>";
    }

    /// <summary>A text with its {fields} (and {= formulas}) filled from <paramref name="e"/>, as a tag writes them.</summary>
    public static string Fill(string template, IModelView m, Entity e, IReadOnlyDictionary<string, string>? extra = null, string units = "mm", Func<string, double?>? more = null) =>
        Field().Replace(template, x => Value(m, e, x.Groups[1].Value, extra, units, more));

    /// <summary>
    /// What a tag of <paramref name="e"/> can show, for the interface's lists: the fields every element has, the user's
    /// parameters of the element and its type (with their values), the standard sets' properties for its class (empty ones
    /// too: filled later, the tag follows) and the property sets it came with from a file. Each: the field to write, its set
    /// (or null), its name, its value now.
    /// </summary>
    public static JsonArray Fields(IModelView m, Entity e, string units = "mm")
    {
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new JsonArray();
        void Add(string field, string? set, string name, string group)
        {
            if (!seen.Add(field)) return;
            list.Add((JsonNode)new JsonObject { ["field"] = field, ["set"] = set, ["name"] = name, ["group"] = group, ["value"] = Value(m, e, field, null, units) });
        }
        foreach (var b in Builtin) Add(b, null, b, "element");
        foreach (var (who, x) in new[] { ("element", (Entity?)e), ("type", type) })
            foreach (var p in Stored(x))
                if (((string?)p["name"])?.Trim() is { Length: > 0 } name)
                    Add(Params.SetOf(p) is { } set ? $"{set}.{name}" : name, Params.SetOf(p), name, who);
        foreach (var set in StandardPsets.All.Where(s => StandardPsets.AppliesTo(s, e.Class)))
            foreach (var name in set.Properties.Keys)
                Add($"{set.Name}.{name}", set.Name, name, "standard");
        foreach (var x in new[] { e, type })
            if (x?.Attrs["properties"] is JsonObject sets)
                foreach (var (set, props) in sets)
                    if (props is JsonObject po)
                        foreach (var (name, _) in po) Add($"{set}.{name}", set, name, "imported");
        // Its scopes' (decision 551): the storey's, the building's, the project's, its host's, its room's names and parameters.
        foreach (var scope in ParamScopes.Names.Where(s => s != "type"))
        {
            if (ParamScopes.Of(m, e, scope) is not { } owner || owner.Id == e.Id) continue;
            Add($"{scope}.name", null, "name", scope);
            if (owner.Class == IfcClass.Project)
            {
                foreach (var (key, label) in Documents.ProjectInfo.Fields) Add($"project.{key}", null, label, scope);
                foreach (var c in ParamScopes.Globals(m)) Add($"project.{c.Key}", null, c.Label, scope);
            }
            foreach (var p in Stored(owner))
                if (((string?)p["name"])?.Trim() is { Length: > 0 } name)
                    Add(Params.SetOf(p) is { } set ? $"{scope}.{set}.{name}" : $"{scope}.{name}", Params.SetOf(p), name, scope);
        }
        return list;
    }

    /// <summary>The template filled in, one string per line ("|"), empty lines dropped.</summary>
    public static IReadOnlyList<string> Lines(string template, IModelView m, Entity e, IReadOnlyDictionary<string, string>? extra = null, string units = "mm") =>
        Fill(template, m, e, extra, units).Split('|').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    /// <summary>A length (metres) as a tag writes it: whole centimetres or millimetres (a tenth where it is not whole), metres to the centimetre, feet and inches to 1/16"; "project" in the project's units.</summary>
    public static string Length(double metres, string unit, string projectUnits = "mm")
    {
        if (unit == "project") unit = projectUnits;
        if (unit is "ft-in" or "in") return Standards.FormatImperial(metres, unit == "ft-in", 2);
        var (v, f) = unit switch { "mm" => (metres * 1000, "0.#"), "m" => (metres, "0.00"), _ => (metres * 100, "0.#") };
        return Math.Round(v, 3).ToString(f, CultureInfo.InvariantCulture);
    }
}
