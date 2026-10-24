using System.Text.Json.Nodes;

namespace Lapin.Drawing.Views;

/// <summary>An edge of a plan's view range: a level (the view's own, the next one up or down, or no limit) and an offset above it.</summary>
public sealed record RangeEdge(string Level, double Offset)
{
    public static readonly string[] Levels = ["self", "above", "below", "unlimited"];
    public static RangeEdge? From(JsonNode? n) => n is JsonObject o && (string?)o["level"] is { } l && Levels.Contains(l)
        ? new RangeEdge(l, Lapin.Model.Json.Number(o["offset"]) ?? 0) : null;
    public JsonObject ToJson() => new() { ["level"] = Level, ["offset"] = Offset };
}

/// <summary>A plan's view range (Revit's): Top, Bottom and Depth, each on a level.</summary>
public sealed record ViewRange(RangeEdge Top, RangeEdge Bottom, RangeEdge Depth)
{
    public static ViewRange? From(JsonNode? n) => n is JsonObject o && RangeEdge.From(o["top"]) is { } t && RangeEdge.From(o["bottom"]) is { } b && RangeEdge.From(o["depth"]) is { } d
        ? new ViewRange(t, b, d) : null;
    public JsonObject ToJson() => new() { ["top"] = Top.ToJson(), ["bottom"] = Bottom.ToJson(), ["depth"] = Depth.ToJson() };
}

/// <summary>A view range as elevations (m); null: no limit.</summary>
public sealed record ResolvedRange(double Cut, double? Top, double Bottom, double? Depth);

/// <summary>How a plan is generated: how far it projects down and up from the cut, and from where (below the cut) lines are beyond.</summary>
public sealed record RangeBands(double Below, double Above, double? BeyondFrom);

/// <summary>
/// A plan's view range (the old Lapin's, after Revit): what is above the cut up
/// to Top is drawn overhead (dashed), the primary range from the cut down to
/// Bottom as seen, and between Bottom and Depth beyond (faint). A plan with no
/// range of its own: overhead to the level above, down to its own floor.
/// </summary>
public static class ViewRanges
{
    /// <summary>No limit (m): deeper or higher than any building.</summary>
    public const double Unlimited = 1e4;

    public static ViewRange Default(double cutHeight, double? viewDepth = null)
    {
        var depth = viewDepth is { } d && d >= 0 ? cutHeight - d : 0;
        return new ViewRange(new("above", 0), new("self", depth), new("self", depth));
    }

    /// <summary>Levels closer than this to the plan's own are one with it (docs/transfer.md).</summary>
    public const double MergeLevels = 0.25;

    static double? LevelAt(RangeEdge edge, double own, IReadOnlyList<double> levels)
    {
        if (edge.Level == "unlimited") return null;
        var sorted = levels.OrderBy(x => x).ToList();
        if (edge.Level == "above") return sorted.FirstOrDefault(l => l >= own + MergeLevels - 1e-6, double.NaN) is var next && !double.IsNaN(next) ? next + edge.Offset : null;
        if (edge.Level == "below") return (sorted.LastOrDefault(l => l <= own - MergeLevels + 1e-6, double.NaN) is var prev && !double.IsNaN(prev) ? prev : own) + edge.Offset;
        return own + edge.Offset;
    }

    /// <summary>The range as elevations: the plan's level, its cut height, the levels of the project.</summary>
    public static ResolvedRange Resolve(double level, double cutHeight, ViewRange? range, IReadOnlyList<double> levels)
    {
        var r = range ?? Default(cutHeight);
        return new ResolvedRange(level + cutHeight, LevelAt(r.Top, level, levels), LevelAt(r.Bottom, level, levels) ?? level, LevelAt(r.Depth, level, levels));
    }

    /// <summary>What is wrong with a range: Revit's order, top ≥ cut ≥ bottom ≥ depth.</summary>
    public static List<string> Problems(ResolvedRange r)
    {
        var o = new List<string>();
        const double e = 1e-6;
        if (r.Top is { } t && t < r.Cut - e) o.Add("topBelowCut");
        if (r.Bottom > r.Cut + e) o.Add("bottomAboveCut");
        if (r.Depth is { } d && d > r.Bottom + e) o.Add("depthAboveBottom");
        return o;
    }

    public static RangeBands Bands(ResolvedRange r)
    {
        var depth = r.Depth ?? r.Cut - Unlimited;
        var bottom = Math.Min(r.Bottom, r.Cut);
        var below = Math.Max(0, r.Cut - Math.Min(depth, bottom));
        var above = r.Top is { } t ? Math.Max(0, t - r.Cut) : Unlimited;
        double? beyond = depth < bottom - 1e-6 ? r.Cut - bottom : null;
        return new RangeBands(below, above, beyond);
    }
}
