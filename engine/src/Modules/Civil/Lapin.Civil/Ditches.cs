using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A ditch (a trench, a swale): a trapezoid section, its bottom a depth under
/// the existing ground, swept along a path and taken out of the terrain. An
/// IfcEarthworksCut TRENCH voiding its terrain, like a platform; its outline
/// in plan is drawn with the terrain's.
/// </summary>
public static class Ditches
{
    public const string Predefined = "TRENCH";
    const double Margin = 0.3;

    public static bool Is(Entity e) => e.Class == IfcClass.EarthworksCut && e.PredefinedType == Predefined;

    public static IReadOnlyList<Vec2> Path(Entity e) => Terrains.Contour(e, "path");

    public static (double Depth, double Bottom, double Slope) Section(Entity e) =>
        (Math.Max(0.05, e.Num("depth", 0.5)), Math.Max(0.05, e.Num("bottomWidth", 0.4)), Math.Max(0, e.Num("slope", 1)));

    /// <summary>The top width of the ditch at the ground: the bottom plus the two slopes.</summary>
    public static double TopWidth(Entity e) { var (d, b, s) = Section(e); return b + 2 * d * s; }

    /// <summary>What the ditch takes out of the ground: a solid per segment, lofted between trapezoids that follow the ground along the path, a little above it.</summary>
    public static List<Solid> Cutters(Entity e, Terrain t)
    {
        var path = SiteGeometry.Densify(Path(e), 1.0);
        if (path.Count < 2) return [];
        var (depth, bottom, slope) = Section(e);
        var dirs = SiteGeometry.Directions(path);
        var sections = new List<List<Vec3>>();
        double? last = null;
        for (var i = 0; i < path.Count; i++)
        {
            var z = t.ElevationAt(path[i].X, path[i].Y) ?? last ?? t.Bounds.Max.Z;
            last = z;
            var n = dirs[i].Perp;
            var top = bottom / 2 + (depth + Margin) * slope;
            sections.Add([(path[i] - n * (bottom / 2)).At(z - depth), (path[i] + n * (bottom / 2)).At(z - depth), (path[i] + n * top).At(z + Margin), (path[i] - n * top).At(z + Margin)]);
        }
        var o = new List<Solid>();
        for (var i = 0; i + 1 < sections.Count; i++)
            if (SiteGeometry.Loft(sections[i], sections[i + 1], "ditch") is { } s) o.Add(s);
        return o;
    }

    /// <summary>In plan: the ditch's centre line and its two edges at the ground.</summary>
    public static IEnumerable<PlanItem> Plan(Entity e)
    {
        var path = Path(e);
        if (path.Count < 2) yield break;
        yield return new PlanItem("symbol", false, path);
        var half = TopWidth(e) / 2;
        var dirs = SiteGeometry.Directions(path);
        yield return new PlanItem("heavy", false, path.Select((p, i) => p + dirs[i].Perp * half).ToList());
        yield return new PlanItem("heavy", false, path.Select((p, i) => p - dirs[i].Perp * half).ToList());
    }

    public static JsonObject Attrs(IReadOnlyList<Vec2> path, double depth, double bottom, double slope) => new()
    {
        ["path"] = Terrains.ContourJson(path), ["depth"] = depth, ["bottomWidth"] = bottom, ["slope"] = slope,
    };
}
