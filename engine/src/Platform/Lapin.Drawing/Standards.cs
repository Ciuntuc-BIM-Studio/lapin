using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Lapin.Drawing;

/// <summary>A text style: paper height (mm), font, bold, italic, a colour (none: the layer's).</summary>
public sealed record TextStyle(string Id, string Name, double Height, string Font, bool Bold, bool Italic, string? Color = null);

/// <summary>A dimension style: its text style, arrow and size, extension-line gap and overshoot, where the value sits and its gap, precision and unit (paper mm).</summary>
public sealed record DimStyle(string Id, string Name, string TextStyle, string Arrow, double ArrowSize, double ExtGap, double ExtOver, string Placement, double TextGap, int Precision, string Unit, string? Color = null);

/// <summary>An axis (grid line) type: its label's text style, bubble and which ends carry it, size (paper mm), line type, colour and the label new axes start from.</summary>
public sealed record AxisStyle(string Id, string Name, string TextStyle, string Bubble, string Ends, double Size, string LineType, string FirstLabel, string? Color = null);

/// <summary>A tag type: a text template whose {fields} read the element's parameters, and the IFC classes it tags (none: any).</summary>
public sealed record TagType(string Id, string Name, string Template, IReadOnlyList<string> Classes)
{
    public bool Equals(TagType? o) => o is not null && Id == o.Id && Name == o.Name && Template == o.Template && Classes.SequenceEqual(o.Classes);
    public override int GetHashCode() => HashCode.Combine(Id, Name, Template);
}

/// <summary>Layers gathered under a group that shows and locks them together.</summary>
public sealed record LayerGroup(string Id, string Name, bool Visible, bool Locked);

/// <summary>A drafting layer: its colour, pen (paper mm) and line type, shown or locked by itself or its group.</summary>
public sealed record DraftLayer(string Id, string Name, string Color, bool Visible, bool Locked, string? Group = null, double? LineWeight = null, string? LineType = null);

/// <summary>
/// The project's drafting standards (the old Lapin's): text and dimension
/// styles, axis types and the default one, tag types, layers and layer
/// groups. Sizes are paper millimetres; a view turns them into model units
/// with its scale. Each list always has its Standard entry (layer "0").
/// An annotation names its styles in its params and may override any field
/// of them in <c>o.&lt;field&gt;</c>.
/// </summary>
public sealed record StyleBook(
    IReadOnlyList<TextStyle> TextStyles, IReadOnlyList<DimStyle> DimStyles, IReadOnlyList<AxisStyle> AxisStyles,
    string DefaultAxisStyle, IReadOnlyList<TagType> TagTypes, IReadOnlyList<DraftLayer> Layers, IReadOnlyList<LayerGroup> LayerGroups)
{
    public static StyleBook Default => Standards.Read(null);
}

public static class Standards
{
    public const string Standard = "standard";
    public static readonly TextStyle DefaultText = new(Standard, "Standard", 2.5, "sans", false, false);
    public static readonly DimStyle DefaultDim = new(Standard, "Standard", Standard, "tick", 1.5, 1, 1.5, "above", 0.8, 2, "m");
    public static readonly AxisStyle DefaultAxis = new(Standard, "Standard", Standard, "circle", "both", 10, "dashdot", "1");
    public static readonly DraftLayer Layer0 = new("0", "0", "#18181b", true, false);

    public static readonly string[] Fonts = ["sans", "serif", "mono"];
    public static readonly string[] Arrows = ["tick", "arrow", "dot", "none"];
    public static readonly string[] Placements = ["above", "centered", "below"];
    public static readonly string[] Units = ["m", "cm", "mm"];
    public static readonly string[] Bubbles = ["circle", "square"];
    public static readonly string[] AxisEnds = ["both", "start", "end", "none"];
    public static readonly string[] LineTypes = ["continuous", "dashed", "dotted", "dashdot"];

    /// <summary>The fields an annotation may override, by kind (an axis overrides its line colour as lineColor).</summary>
    public static readonly string[] TextOverrides = ["height", "font", "bold", "italic", "color"];
    public static readonly string[] DimOverrides = ["arrow", "arrowSize", "placement", "textGap", "precision", "unit", "color"];
    public static readonly string[] AxisOverrides = ["bubble", "ends", "size", "lineType"];

    static string Str(JsonNode? v, string fallback) => v is JsonValue j && j.TryGetValue<string>(out var s) && s.Length > 0 ? s : fallback;
    static double Num(JsonNode? v, double fallback) => Lapin.Model.Json.Number(v) is { } d && double.IsFinite(d) && d >= 0 ? d : fallback;
    static bool True(JsonNode? v) => v is JsonValue j && j.TryGetValue<bool>(out var b) && b;
    static string OneOf(JsonNode? v, string[] values, string fallback) => v is JsonValue j && j.TryGetValue<string>(out var s) && values.Contains(s) ? s : fallback;
    static string? Color(JsonNode? v) => v is JsonValue j && j.TryGetValue<string>(out var s) && Regex.IsMatch(s, "^#[0-9a-fA-F]{6}$") ? s : null;
    static double Positive(JsonNode? v, double fallback) => Num(v, fallback) is var n && n > 0 ? n : fallback;

    static TextStyle ReadText(JsonObject v)
    {
        var d = DefaultText;
        return new(Str(v["id"], d.Id), Str(v["name"], d.Name), Positive(v["height"], d.Height), OneOf(v["font"], Fonts, d.Font), True(v["bold"]), True(v["italic"]), Color(v["color"]));
    }

    static DimStyle ReadDim(JsonObject v)
    {
        var d = DefaultDim;
        return new(Str(v["id"], d.Id), Str(v["name"], d.Name), Str(v["textStyle"], d.TextStyle), OneOf(v["arrow"], Arrows, d.Arrow), Num(v["arrowSize"], d.ArrowSize),
            Num(v["extGap"], d.ExtGap), Num(v["extOver"], d.ExtOver), OneOf(v["placement"], Placements, d.Placement), Num(v["textGap"], d.TextGap),
            (int)Math.Round(Num(v["precision"], d.Precision)), OneOf(v["unit"], Units, d.Unit), Color(v["color"]));
    }

    static AxisStyle ReadAxis(JsonObject v)
    {
        var d = DefaultAxis;
        var first = v["firstLabel"] is JsonValue j && j.TryGetValue<string>(out var fl) && fl.Trim().Length > 0 ? fl.Trim() : d.FirstLabel;
        return new(Str(v["id"], d.Id), Str(v["name"], d.Name), Str(v["textStyle"], d.TextStyle), OneOf(v["bubble"], Bubbles, d.Bubble), OneOf(v["ends"], AxisEnds, d.Ends),
            Positive(v["size"], d.Size), OneOf(v["lineType"], LineTypes, d.LineType), first, Color(v["color"]));
    }

    static TagType? ReadTag(JsonObject v)
    {
        if (v["id"] is not JsonValue idv || !idv.TryGetValue<string>(out var id) || id.Length == 0) return null;
        if (v["template"] is not JsonValue tv || !tv.TryGetValue<string>(out var template)) return null;
        var classes = v["classes"] is JsonArray arr
            ? arr.Select(c => c is JsonValue cv && cv.TryGetValue<string>(out var s) ? s.Trim() : "").Where(s => s.Length > 0).ToList()
            : [];
        return new TagType(id, Str(v["name"], "Tag"), template, classes);
    }

    static DraftLayer ReadLayer(JsonObject v) => new(
        Str(v["id"], "0"), Str(v["name"], Str(v["id"], "0")), Color(v["color"]) ?? Layer0.Color, v["visible"] is not JsonValue vv || !vv.TryGetValue<bool>(out var vis) || vis,
        True(v["locked"]), v["group"] is JsonValue g && g.TryGetValue<string>(out var gs) && gs.Length > 0 ? gs : null,
        Num(v["lineWeight"], -1) is var w && w > 0 ? w : null, OneOf(v["lineType"], LineTypes, "") is { Length: > 0 } lt ? lt : null);

    static List<T> WithStandard<T>(List<T> list, T standard, Func<T, string> id, string key = Standard) =>
        list.Any(s => id(s) == key) ? list : [standard, .. list];

    /// <summary>The standards from a project's JSON, tolerantly: a malformed field falls back to the Standard style's, an unreadable entry is dropped, the Standard entries are always there.</summary>
    public static StyleBook Read(JsonNode? raw)
    {
        var o = raw as JsonObject ?? [];
        IEnumerable<JsonObject> List(string key) => o[key] is JsonArray a ? a.OfType<JsonObject>() : [];
        var axes = WithStandard(List("axisStyles").Select(ReadAxis).ToList(), DefaultAxis, a => a.Id);
        var def = o["defaultAxisStyle"] is JsonValue dv && dv.TryGetValue<string>(out var ds) && axes.Any(a => a.Id == ds) ? ds : Standard;
        return new StyleBook(
            WithStandard(List("textStyles").Select(ReadText).ToList(), DefaultText, t => t.Id),
            WithStandard(List("dimStyles").Select(ReadDim).ToList(), DefaultDim, d => d.Id),
            axes, def,
            List("tagTypes").Select(ReadTag).OfType<TagType>().ToList(),
            WithStandard(List("layers").Select(ReadLayer).ToList(), Layer0, l => l.Id, "0"),
            List("layerGroups").Where(g => g["id"] is JsonValue).Select(g => new LayerGroup(Str(g["id"], ""), Str(g["name"], "Group"), g["visible"] is not JsonValue gv || !gv.TryGetValue<bool>(out var b) || b, True(g["locked"]))).Where(g => g.Id.Length > 0).ToList());
    }

    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static JsonObject ToJson(StyleBook b) => (JsonObject)JsonSerializer.SerializeToNode(b, Web)!;

    // ------------------------------------------------------------ resolving

    public static TextStyle TextOf(StyleBook b, string? id) => b.TextStyles.FirstOrDefault(s => s.Id == id) ?? b.TextStyles.FirstOrDefault(s => s.Id == Standard) ?? DefaultText;
    public static DimStyle DimOf(StyleBook b, string? id) => b.DimStyles.FirstOrDefault(s => s.Id == id) ?? b.DimStyles.FirstOrDefault(s => s.Id == Standard) ?? DefaultDim;
    public static AxisStyle AxisOf(StyleBook b, string? id) => b.AxisStyles.FirstOrDefault(s => s.Id == id) ?? b.AxisStyles.FirstOrDefault(s => s.Id == Standard) ?? DefaultAxis;

    /// <summary>A record with the overrides in <paramref name="p"/> (<c>o.field</c>) applied, each only when its type fits.</summary>
    static T Overridden<T>(T style, JsonObject p, string[] fields) where T : class
    {
        var o = (JsonObject)JsonSerializer.SerializeToNode(style, Web)!;
        foreach (var f in fields)
        {
            if (p["o." + f] is not JsonValue v) continue;
            var cur = o[f] as JsonValue;
            var fits = cur == null
                || (cur.GetValueKind() == JsonValueKind.Number && v.GetValueKind() == JsonValueKind.Number)
                || (cur.GetValueKind() is JsonValueKind.True or JsonValueKind.False && v.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
                || v.GetValueKind() == JsonValueKind.String;
            if (fits) o[f] = v.DeepClone();
        }
        return JsonSerializer.Deserialize<T>(o, Web)!;
    }

    /// <summary>The text style an annotation is drawn with: its own (or its axis or dimension style's), then its overrides.</summary>
    public static TextStyle ResolveText(StyleBook b, JsonObject p)
    {
        var baseStyle = p["textStyle"] is JsonValue t ? TextOf(b, (string?)t)
            : p["axisStyle"] is JsonValue a ? TextOf(b, AxisOf(b, (string?)a).TextStyle)
            : TextOf(b, DimOf(b, (string?)p["dimStyle"]).TextStyle);
        return Overridden(baseStyle, p, TextOverrides);
    }

    public static DimStyle ResolveDim(StyleBook b, JsonObject p) => Overridden(DimOf(b, (string?)p["dimStyle"]), p, DimOverrides);

    public static AxisStyle ResolveAxis(StyleBook b, JsonObject p)
    {
        var s = Overridden(AxisOf(b, (string?)p["axisStyle"]), p, AxisOverrides);
        return p["o.lineColor"] is JsonValue c && c.TryGetValue<string>(out var color) ? s with { Color = color } : s;
    }

    /// <summary>A length (metres) in a dimension style's unit and precision.</summary>
    public static string FormatDimension(double metres, string unit, int precision)
    {
        var factor = unit == "mm" ? 1000 : unit == "cm" ? 100 : 1;
        return (metres * factor).ToString("F" + Math.Clamp(precision, 0, 6), CultureInfo.InvariantCulture);
    }

    /// <summary>Text reads left to right, and bottom to top when it runs vertically: a screen angle (°, y down) folded into [−90°, 90°).</summary>
    public static double ReadableDeg(double deg)
    {
        var d = ((deg % 360) + 540) % 360 - 180;
        if (d >= 90 - 1e-6) d -= 180;
        else if (d < -90 - 1e-6) d += 180;
        return d + 0.0;
    }

    /// <summary>A line type's dashes on paper (mm); continuous: none.</summary>
    public static double[] DashOf(string? lineType) => lineType switch
    {
        "dashed" => [3, 1.5], "dotted" => [0.3, 1], "dashdot" => [4, 1, 0.3, 1], _ => [],
    };

    /// <summary>Whether a layer shows: its own switch, its group's, then the view's and the sheet viewport's.</summary>
    public static bool LayerShows(DraftLayer layer, IEnumerable<LayerGroup> groups, IEnumerable<string>? viewHides = null, IEnumerable<string>? viewportHides = null)
    {
        if (!layer.Visible) return false;
        if (layer.Group != null && groups.FirstOrDefault(g => g.Id == layer.Group) is { Visible: false }) return false;
        if (viewHides?.Contains(layer.Id) == true) return false;
        return viewportHides?.Contains(layer.Id) != true;
    }

    public static bool LayerLocked(DraftLayer layer, IEnumerable<LayerGroup> groups) =>
        layer.Locked || (layer.Group != null && groups.FirstOrDefault(g => g.Id == layer.Group)?.Locked == true);
}
