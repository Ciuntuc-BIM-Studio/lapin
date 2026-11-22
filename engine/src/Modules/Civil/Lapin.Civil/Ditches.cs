using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A ditch (a trench, a swale): a trapezoid section swept along a path and
/// taken out of the terrain. Its bottom is a depth under the ground, or the
/// red line of a designed profile (a ditch with a fall of its own); the slopes
/// rise from it to the ground. Its corners are mitred (<see cref="Corridors"/>).
/// An IfcEarthworksCut TRENCH voiding its terrain, like a platform; its
/// outline in plan is drawn with the terrain's.
/// </summary>
public static class Ditches
{
    public const string Predefined = "TRENCH";
    const double Margin = 0.3;

    public static bool Is(Entity e) => e.Class == IfcClass.EarthworksCut && e.PredefinedType == Predefined;

    public static IReadOnlyList<Vec2> Path(Entity e) => Terrains.Contour(e, "path");

    public static (double Depth, double Bottom, double Slope) Section(Entity e) =>
        (Math.Max(0.05, e.Num("depth", 0.5)), Math.Max(0.05, e.Num("bottomWidth", 0.4)), Math.Max(0, e.Num("slope", 1)));

    /// <summary>The top width of the ditch at the ground, at its nominal depth: the bottom plus the two slopes.</summary>
    public static double TopWidth(Entity e) { var (d, b, s) = Section(e); return b + 2 * d * s; }

    /// <summary>The trapezoid at a station: the bottom at the red line (designed) or a depth under the ground on the axis, the slopes up to the ground (+ <paramref name="margin"/>).</summary>
    static List<SectionPart> Trapezoid(Entity e, double zGround, double? zDesign, double margin)
    {
        var (depth, bottom, slope) = Section(e);
        var zb = zDesign ?? zGround - depth;
        var top = bottom / 2 + Math.Max(0, zGround + margin - zb) * slope;
        var zt = Math.Max(zGround, zb) + margin;
        return [new("ditch", [new(-bottom / 2, zb), new(bottom / 2, zb), new(top, zt), new(-top, zt)])];
    }

    public static Func<double, double>? DesignOf(IModelView? m, Entity e, Chain c)
    {
        var spec = CorridorSpec.Of(e);
        if (spec.Mode != "design" || m == null) return null;
        return Corridors.RedLine(m, e, c);
    }

    /// <summary>
    /// The ground as the ditch leaves it at <paramref name="p"/>, the ground being <paramref name="g"/> there: its section
    /// at the nearest station, as <see cref="Cutters"/> cuts it (the bottom at the red line <paramref name="design"/>, else
    /// a depth under the ground on the axis, the slopes rising 1 : n from it), where that is under the ground; null where
    /// it leaves the ground as it is (beside it, past its ends).
    /// </summary>
    public static double? DesignedAt(Entity e, Chain c, Func<double, double>? design, Terrain t, Vec2 p, double g)
    {
        if (c.Segments == 0) return null;
        var (s, off) = c.Project(p);
        var axis = c.PointAt(s);
        // Past an end the nearest station is the end, the point not square to it.
        if ((p - axis).Length > Math.Abs(off) + 1e-6) return null;
        var (depth, bottom, slope) = Section(e);
        var zb = design?.Invoke(s) ?? (t.ElevationAt(axis.X, axis.Y) ?? g) - depth;
        var across = Math.Abs(off) - bottom / 2;
        var h = across <= 0 ? zb : slope > 0 ? zb + across / slope : double.PositiveInfinity;
        return h < g ? h : null;
    }

    /// <summary>The ditch's section at a station, cut at the ground (what the configurator shows).</summary>
    public static List<SectionPart> SectionAt(IModelView m, Entity e, Chain c, double station)
    {
        var te = Corridors.TerrainEntity(m, e);
        var t = te != null ? Terrains.Of(te) : null;
        var p = c.PointAt(station);
        var z = t?.ElevationAt(p.X, p.Y) ?? Corridors.Fallback(m, e);
        return Trapezoid(e, z, DesignOf(m, e, c)?.Invoke(station), 0);
    }

    /// <summary>What the ditch takes out of the ground: lofted between trapezoids that follow the ground along the path, a little above it.</summary>
    public static List<Solid> Cutters(Entity e, Terrain t, IModelView? m = null)
    {
        var c = Corridors.ChainOf(e);
        if (c.Segments == 0) return [];
        var design = DesignOf(m, e, c);
        double? last = null;
        double Ground(double s)
        {
            var p = c.PointAt(s);
            var z = t.ElevationAt(p.X, p.Y) ?? last ?? t.Bounds.Max.Z;
            last = z;
            return z;
        }
        var solids = Corridors.Sweep(c, [(c.Start, c.End)], (s, _) => Trapezoid(e, Ground(s), design?.Invoke(s), Margin), 1.0);
        return solids.TryGetValue("ditch", out var list) ? list : [];
    }

    /// <summary>In plan: the ditch's centre line and its two edges at the ground (at its nominal depth), mitred.</summary>
    public static IEnumerable<PlanItem> Plan(Entity e)
    {
        var c = Corridors.ChainOf(e);
        if (c.Segments == 0) yield break;
        yield return new PlanItem("symbol", false, c.Axis());
        var half = TopWidth(e) / 2;
        foreach (var off in new[] { half, -half })
            yield return new PlanItem("heavy", false, c.Offset(off));
    }

    public static JsonObject Attrs(IReadOnlyList<Vec2> path, double depth, double bottom, double slope) => new()
    {
        ["path"] = Terrains.ContourJson(path), ["depth"] = depth, ["bottomWidth"] = bottom, ["slope"] = slope,
    };
}
