using System.Text.Json.Nodes;

namespace Lapin.Drawing;

/// <summary>A line's style: a pen (1–16), a colour, a pattern (solid, dashed, hidden, center, dotted). Absent fields come from the next level.</summary>
public sealed record LineStyleSpec(int? Pen = null, string? Color = null, string? Pattern = null)
{
    public static LineStyleSpec? From(JsonNode? n) => n is JsonObject o
        ? new LineStyleSpec(Lapin.Model.Json.Number(o["pen"]) is { } p ? (int)p : null, (string?)o["color"], (string?)o["pattern"])
        : null;
    public bool Empty => Pen == null && Color == null && Pattern == null;
}

/// <summary>A line as resolved: paper width (mm), colour, dashes (paper mm; empty: solid). Null fields: the drawing's own pen.</summary>
public sealed record ResolvedLine(double? WidthMm = null, string? Color = null, double[]? Dash = null);

/// <summary>
/// Line styles, as in Revit (the old Lapin's): a pen table (pens 1–16, paper
/// mm per scale), the project's object styles (each category's cut,
/// projection, hidden, overhead and beyond lines), a view's override by
/// category and by element. The narrowest wins, field by field. Nothing
/// styled resolves to nothing: the drawing keeps its default pens.
/// </summary>
public static class LineStyles
{
    public static readonly string[] Roles = ["cut", "projection", "hidden", "overhead", "beyond"];
    public static readonly string[] Patterns = ["solid", "dashed", "hidden", "center", "dotted"];
    public const int PenCount = 16;

    /// <summary>ISO 128 widths, then on in the same ratio (√2).</summary>
    static readonly double[] Iso = [0.1, 0.13, 0.18, 0.25, 0.35, 0.5, 0.7, 1, 1.4, 2, 2.8, 4, 5.6, 8, 11, 16, 22, 32];
    static readonly int[] DefaultScales = [20, 50, 100, 200];
    /// <summary>Pen n at 1:100 is the nth ISO width (pen 5 = 0.35 mm); one step heavier at 1:50, two at 1:20, one finer at 1:200.</summary>
    static readonly Dictionary<int, int> Shift = new() { [20] = 2, [50] = 1, [100] = 0, [200] = -1 };

    public sealed record PenTable(IReadOnlyList<int> Scales, IReadOnlyList<IReadOnlyList<double>> Widths);

    public static PenTable DefaultPenTable() => new(DefaultScales,
        Enumerable.Range(0, PenCount).Select(i => (IReadOnlyList<double>)DefaultScales.Select(s => Iso[Math.Clamp(i + Shift[s], 0, Iso.Length - 1)]).ToList()).ToList());

    /// <summary>Pen <paramref name="pen"/>'s width at 1:<paramref name="scale"/> (paper mm), at the nearest listed scale.</summary>
    public static double PenWidth(PenTable t, int pen, double scale)
    {
        var row = t.Widths[Math.Clamp(pen, 1, PenCount) - 1];
        var best = 0;
        for (var i = 1; i < t.Scales.Count; i++)
            if (Math.Abs(Math.Log(t.Scales[i] / scale)) < Math.Abs(Math.Log(t.Scales[best] / scale))) best = i;
        return row[best];
    }

    /// <summary>Dash patterns on paper (mm); solid: none.</summary>
    public static double[] DashOf(string? pattern) => pattern switch
    {
        "dashed" => [3, 1.5], "hidden" => [1.5, 1], "center" => [6, 1.5, 1, 1.5], "dotted" => [0.3, 1], _ => [],
    };

    /// <summary>The category of an IFC class in drawings (walls, slabs, roofs…), or null.</summary>
    public static string? CategoryOf(string ifcClass) => ifcClass.ToUpperInvariant() switch
    {
        "IFCWALL" or "IFCWALLSTANDARDCASE" or "IFCCURTAINWALL" => "walls",
        "IFCSLAB" => "slabs",
        "IFCROOF" => "roofs",
        "IFCCOLUMN" => "columns",
        "IFCBEAM" or "IFCMEMBER" => "beams",
        "IFCDOOR" => "doors",
        "IFCWINDOW" => "windows",
        "IFCSTAIR" or "IFCSTAIRFLIGHT" or "IFCRAMP" or "IFCRAMPFLIGHT" => "stairs",
        "IFCRAILING" => "railings",
        "IFCSPACE" => "spaces",
        "IFCGEOGRAPHICELEMENT" or "IFCEARTHWORKSCUT" or "IFCEARTHWORKSFILL" => "topography",
        "IFCFURNISHINGELEMENT" or "IFCFURNITURE" => "furniture",
        "IFCBUILDINGELEMENTPROXY" or "IFCCOVERING" or "IFCPLATE" or "IFCFOOTING" => "other",
        _ => null,
    };

    public static readonly string[] Categories = ["walls", "slabs", "roofs", "columns", "beams", "doors", "windows", "stairs", "railings", "spaces", "furniture", "topography", "other"];

    public static LineStyleSpec Merge(LineStyleSpec into, LineStyleSpec? from) => from == null ? into
        : new(into.Pen ?? from.Pen, into.Color ?? from.Color, into.Pattern ?? from.Pattern);

    public delegate ResolvedLine? Resolver(string role, string ifcClass, string? elementId);

    /// <summary>
    /// The resolver of a view: object styles ({category: {role: spec}}), the
    /// view's category overrides ({category: {lines: {role: spec}}}) and element
    /// overrides ({elementId: {role: spec}}). Null when nothing is styled.
    /// </summary>
    public static Resolver? For(JsonObject? objectStyles, JsonObject? viewCategories, JsonObject? viewElements, PenTable? table, double scale)
    {
        objectStyles ??= []; viewCategories ??= []; viewElements ??= [];
        var anything = objectStyles.Count > 0 || viewElements.Count > 0 || viewCategories.Any(c => c.Value?["lines"] is JsonObject { Count: > 0 });
        if (!anything) return null;
        table ??= DefaultPenTable();
        var cache = new Dictionary<string, ResolvedLine?>();
        return (role, ifcClass, elementId) =>
        {
            var category = CategoryOf(ifcClass);
            var element = elementId != null ? viewElements[elementId] as JsonObject : null;
            var key = $"{role}|{category}|{(element != null ? elementId : "")}";
            if (cache.TryGetValue(key, out var hit)) return hit;
            var spec = new LineStyleSpec();
            spec = Merge(spec, LineStyleSpec.From(element?[role]));
            if (category != null) spec = Merge(spec, LineStyleSpec.From(viewCategories[category]?["lines"]?[role]));
            if (category != null) spec = Merge(spec, LineStyleSpec.From(objectStyles[category]?[role]));
            ResolvedLine? r = spec.Empty ? null : new ResolvedLine(spec.Pen is { } p ? PenWidth(table, p, scale) : null, spec.Color, spec.Pattern != null ? DashOf(spec.Pattern) : null);
            cache[key] = r;
            return r;
        };
    }
}
