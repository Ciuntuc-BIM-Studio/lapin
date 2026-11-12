using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A 2D symbol of the user's own (docs/plan-user-extensibility.md §3.1): a small drawing in mm with its insertion point at the
/// origin and its direction along +x, y up. Items as a title block's (line, rect, polyline, text) plus circles and arcs;
/// texts carry live text (<c>{attr.number}</c>, <c>{view.scale}</c>, <c>{project.name}</c>) and formulas (<c>{= attr.width * 2}</c>).
/// <see cref="Scale"/>: <c>annotation</c>, its mm are paper mm (a north arrow reads the same at any scale), or <c>model</c>, its mm are
/// the building's (a bathtub drawn at its size). Attributes: the fields an instance fills (<c>{attr.key}</c>), with their defaults.
/// </summary>
public sealed record SymbolDef(string Scale, string Category, JsonArray Items, IReadOnlyList<SymbolAttribute> Attributes, string? Description = null)
{
    public static readonly string[] Scales = ["annotation", "model"];
    public static readonly string[] ItemTypes = ["line", "rect", "polyline", "circle", "arc", "text"];
    public const int MaxItems = 4000;
    /// <summary>A symbol's reach from its insertion point (mm): a model symbol up to 100 m, an annotation one up to a metre of paper.</summary>
    public const double MaxReach = 100_000;

    /// <summary>The categories offered (any other text is taken too: the user's own).</summary>
    public static readonly string[] Categories = ["annotation", "north", "section", "elevation", "level", "grid", "callout", "revision", "break", "furniture", "sanitary", "electrical", "fire", "landscape", "other"];

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["scale"] = Scale, ["category"] = Category, ["items"] = Items.DeepClone(),
            ["attributes"] = new JsonArray(Attributes.Select(a => (JsonNode)a.ToJson()).ToArray()),
        };
        if (Description is { Length: > 0 }) o["description"] = Description;
        return o;
    }

    /// <summary>A definition as kept; null when it is not one.</summary>
    public static SymbolDef? From(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        var scale = (string?)o["scale"] is "model" ? "model" : "annotation";
        var category = (string?)o["category"] is { Length: > 0 } c ? c : "other";
        var attrs = (o["attributes"] as JsonArray ?? []).Select(SymbolAttribute.From).OfType<SymbolAttribute>().ToList();
        return new(scale, category, o["items"] is JsonArray items ? (JsonArray)items.DeepClone() : [], attrs, (string?)o["description"]);
    }

    /// <summary>A definition as a command gives it, checked item by item; what is not a symbol's is refused with why.</summary>
    public static SymbolDef Parse(JsonNode? n)
    {
        if (n is not JsonObject o) throw new CommandException("A symbol is an object: its scale, category and items.");
        if (o["scale"] is { } s && !Scales.Contains((string?)s)) throw new CommandException("A symbol's scale is annotation (paper mm) or model (the building's mm).");
        var items = o["items"] as JsonArray ?? throw new CommandException("A symbol needs its items (a list).");
        if (items.Count == 0) throw new CommandException("A symbol draws something: it needs one item at least.");
        if (items.Count > MaxItems) throw new CommandException($"A symbol has at most {MaxItems} items.");
        var checkedItems = new JsonArray();
        for (var i = 0; i < items.Count; i++) checkedItems.Add((JsonNode)CheckItem(items[i], i + 1));
        var attrs = new List<SymbolAttribute>();
        foreach (var a in o["attributes"] as JsonArray ?? [])
        {
            var attr = SymbolAttribute.From(a) ?? throw new CommandException("An attribute is {key, label?, default?}: its key letters, digits and _ only.");
            if (attrs.Any(x => x.Key.Equals(attr.Key, StringComparison.OrdinalIgnoreCase))) throw new CommandException($"The attribute „{attr.Key}” is there twice.");
            attrs.Add(attr);
        }
        var d = From(o)!;
        return d with { Items = checkedItems, Attributes = attrs, Category = d.Category.Length > 40 ? d.Category[..40] : d.Category };
    }

    static JsonObject CheckItem(JsonNode? n, int index)
    {
        if (n is not JsonObject item) throw new CommandException($"Item {index} is not an object.");
        var type = (string?)item["type"];
        if (type == null || !ItemTypes.Contains(type)) throw new CommandException($"Item {index} is not a kind of item ({string.Join(", ", ItemTypes)}).");
        double Num(string k)
        {
            if (Json.Number(item[k]) is not { } v || !double.IsFinite(v)) throw new CommandException($"Item {index} ({type}) needs its {k} (mm).");
            if (Math.Abs(v) > MaxReach) throw new CommandException($"Item {index} ({type}) reaches too far ({k}).");
            return v;
        }
        void Pt(JsonNode? p, string k)
        {
            if (p is not JsonArray { Count: 2 } a || Json.Number(a[0]) is not { } x || Json.Number(a[1]) is not { } y || !double.IsFinite(x) || !double.IsFinite(y))
                throw new CommandException($"Item {index} ({type}) needs its {k} as [x, y] (mm).");
            if (Math.Abs(x) > MaxReach || Math.Abs(y) > MaxReach) throw new CommandException($"Item {index} ({type}) reaches too far.");
        }
        switch (type)
        {
            case "line": Pt(item["a"], "a"); Pt(item["b"], "b"); break;
            case "rect":
                Num("x"); Num("y");
                if (Num("w") <= 0 || Num("h") <= 0) throw new CommandException($"Item {index} (rect) has a width and a height above 0.");
                break;
            case "polyline":
                if (item["pts"] is not JsonArray { Count: >= 2 } pts) throw new CommandException($"Item {index} (polyline) needs two points at least.");
                foreach (var p in pts) Pt(p, "points");
                break;
            case "circle":
                Pt(item["c"], "centre");
                if (Num("r") <= 0) throw new CommandException($"Item {index} (circle) has a radius above 0.");
                break;
            case "arc":
                Pt(item["c"], "centre");
                if (Num("r") <= 0) throw new CommandException($"Item {index} (arc) has a radius above 0.");
                Num("start"); Num("end");
                break;
            case "text":
                Pt(item["at"], "at");
                if (item["text"] is not JsonValue) throw new CommandException($"Item {index} (text) needs its text.");
                if (item["h"] is { } th && Json.Number(th) is not (> 0 and <= 10_000)) throw new CommandException($"Item {index} (text): a height is above 0.");
                break;
        }
        if (item["dash"] is JsonArray dash && dash.Any(x => Json.Number(x) is not (> 0 and < 1000))) throw new CommandException($"Item {index}: a dash is lengths above 0 (mm).");
        return (JsonObject)item.DeepClone();
    }
}

/// <summary>A field an instance of a symbol fills: its key (<c>{attr.key}</c>), its label and its default.</summary>
public sealed record SymbolAttribute(string Key, string Label, string Default)
{
    public JsonObject ToJson() => new() { ["key"] = Key, ["label"] = Label, ["default"] = Default };

    public static SymbolAttribute? From(JsonNode? n)
    {
        if (n is not JsonObject o || ((string?)o["key"])?.Trim() is not { Length: > 0 and <= 40 } key || !key.All(c => char.IsLetterOrDigit(c) || c == '_')) return null;
        return new(key, ((string?)o["label"])?.Trim() is { Length: > 0 } l ? l : key, o["default"]?.ToString() ?? "");
    }
}
