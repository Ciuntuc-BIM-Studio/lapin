using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.SiteWorks;

/// <summary>
/// An area in plan to lay formwork over: its rings (an outline and its holes,
/// even-odd) less the union of its obstacles (what stands in it: the beams
/// under a floor, the columns and walls it rests on). Lines are clipped to it
/// and its area is measured exactly.
/// </summary>
public sealed class Region(IReadOnlyList<Vec2[]> rings, IReadOnlyList<Vec2[]> obstacles)
{
    public IReadOnlyList<Vec2[]> Rings { get; } = rings;
    public IReadOnlyList<Vec2[]> Obstacles { get; } = obstacles;

    /// <summary>The parts of segment a→b inside the region, as parameters in [0, 1], in order.</summary>
    public List<(double A, double B)> Intervals(Vec2 a, Vec2 b)
    {
        var inside = new List<(double, double)>();
        Polygon2.InsideIntervals(Rings, a, b, 0, 1, inside);
        if (Obstacles.Count == 0 || inside.Count == 0) return Sorted(inside);
        var blocked = new List<(double, double)>();
        foreach (var o in Obstacles) Polygon2.InsideIntervals([o], a, b, 0, 1, blocked);
        return Intersect(Sorted(inside), Polygon2.Outside(blocked));
    }

    static List<(double A, double B)> Sorted(List<(double A, double B)> l) { l.Sort((x, y) => x.A.CompareTo(y.A)); return l; }

    public static List<(double A, double B)> Intersect(List<(double A, double B)> a, List<(double A, double B)> b)
    {
        var o = new List<(double, double)>();
        foreach (var (a0, a1) in a)
            foreach (var (b0, b1) in b)
            {
                var (s, e) = (Math.Max(a0, b0), Math.Min(a1, b1));
                if (e - s > 1e-9) o.Add((s, e));
            }
        return Sorted(o);
    }

    /// <summary>
    /// The region's area, exact: between consecutive event heights (every
    /// vertex, every crossing of two edges) the length inside varies linearly,
    /// so its value at the middle of each band, times the band, sums it.
    /// </summary>
    public double Area()
    {
        var all = Rings.Concat(Obstacles).ToList();
        if (all.Count == 0) return 0;
        var edges = all.SelectMany(r => r.Select((p, i) => (A: p, B: r[(i + 1) % r.Length]))).ToList();
        var ys = new List<double>(edges.Select(e => e.A.Y));
        for (var i = 0; i < edges.Count; i++)
            for (var j = i + 1; j < edges.Count; j++)
                if (Cross(edges[i].A, edges[i].B, edges[j].A, edges[j].B) is { } y) ys.Add(y);
        ys.Sort();
        var (x0, x1) = (all.Min(r => r.Min(p => p.X)) - 1, all.Max(r => r.Max(p => p.X)) + 1);
        var area = 0.0;
        for (var k = 0; k + 1 < ys.Count; k++)
        {
            var (y0, y1) = (ys[k], ys[k + 1]);
            if (y1 - y0 < 1e-12) continue;
            var ym = (y0 + y1) / 2;
            area += Intervals(new(x0, ym), new(x1, ym)).Sum(t => t.B - t.A) * (x1 - x0) * (y1 - y0);
        }
        return area;
    }

    /// <summary>The height where two segments cross, if they do (strictly inside both).</summary>
    static double? Cross(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        var (r, s) = (b - a, d - c);
        var den = Vec2.Cross(r, s);
        if (Math.Abs(den) < 1e-15) return null;
        var t = Vec2.Cross(c - a, s) / den;
        var u = Vec2.Cross(c - a, r) / den;
        return t > 1e-9 && t < 1 - 1e-9 && u > 1e-9 && u < 1 - 1e-9 ? a.Y + r.Y * t : null;
    }

    public bool Contains(Vec2 p) => Polygon2.Inside(Rings, p) && !Obstacles.Any(o => Polygon2.Contains(o, p));
}

/// <summary>Helpers the layouts share: reading a spec, covering a length with catalogue pieces, spreading points along a run.</summary>
public static class Lay
{
    public static double Num(JsonObject? o, string key, double fallback) => Json.Number(o?[key]) ?? fallback;

    public static double[] Nums(JsonObject? o, string key, double[] fallback) =>
        o?[key] is JsonArray a && a.Select(x => Json.Number(x)).OfType<double>().Where(x => x > 0).ToArray() is { Length: > 0 } l ? l : fallback;

    public static bool Bool(JsonObject? o, string key, bool fallback) => o?[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    public static string Str(JsonObject? o, string key, string fallback) => (string?)o?[key] is { Length: > 0 } s ? s : fallback;

    /// <summary>A spec over its defaults: the keys it has replace the defaults' (nested objects replaced whole).</summary>
    public static JsonObject Merge(JsonObject defaults, JsonObject? over)
    {
        var o = (JsonObject)defaults.DeepClone();
        if (over != null) foreach (var (k, v) in over) o[k] = v?.DeepClone();
        return o;
    }

    /// <summary>
    /// A length covered with catalogue pieces laid end to end: the fewest
    /// pieces whose sum reaches it, standing out by as little as that allows
    /// (<paramref name="cmPerPiece"/> centimetres out weigh as much as one piece
    /// more; by default the count comes first), the longest first.
    /// </summary>
    public static List<double> Cover(double length, double[] lengths, double cmPerPiece = 1e6)
    {
        var units = lengths.Select(b => (int)Math.Round(b * 100)).Where(u => u > 0).Distinct().ToArray();
        if (units.Length == 0 || length <= 1e-6) return [];
        var target = (int)Math.Ceiling(length * 100 - 1e-6);
        var max = target + units.Max();
        var count = Enumerable.Repeat(int.MaxValue, max + 1).ToArray();
        var from = new int[max + 1];
        count[0] = 0;
        for (var s = 1; s <= max; s++)
            foreach (var u in units)
                if (s >= u && count[s - u] != int.MaxValue && count[s - u] + 1 < count[s]) { count[s] = count[s - u] + 1; from[s] = u; }
        var best = -1;
        var score = double.MaxValue;
        for (var s = target; s <= max; s++)
        {
            if (count[s] == int.MaxValue) continue;
            var sc = count[s] + (s - target) / cmPerPiece;
            if (sc < score - 1e-12) { score = sc; best = s; }
        }
        var o = new List<double>();
        for (var s = best; s > 0; s -= from[s]) o.Add(from[s] / 100.0);
        return o.OrderByDescending(b => b).ToList();
    }

    /// <summary>Points from a to b, the first and last <paramref name="inset"/> in from the ends, no further apart than <paramref name="max"/>; one in the middle when the run is too short.</summary>
    public static List<double> Spread(double a, double b, double max, double inset)
    {
        var length = b - a;
        if (length <= 1e-6) return [];
        var i = Math.Min(inset, length / 2);
        var span = length - 2 * i;
        if (span < 1e-6) return [(a + b) / 2];
        var n = Math.Max(1, (int)Math.Ceiling(span / Math.Max(max, 1e-3) - 1e-9));
        return Enumerable.Range(0, n + 1).Select(k => a + i + span * k / n).ToList();
    }

    public static string M(double v) => FormattableString.Invariant($"{v:0.##}");

    /// <summary>An element's footprint in plan and its height span, from its named faces (its top face's outline, else its bottom's).</summary>
    public static (Vec2[] Footprint, double Bottom, double Top)? Extent(IReadOnlyList<ElementFace> faces)
    {
        var areas = faces.Where(f => !f.IsEdge).ToList();
        if (areas.Count == 0) return null;
        var flat = areas.FirstOrDefault(f => f.Role == "Top") ?? areas.FirstOrDefault(f => f.Role is "Bottom" or "Soffit");
        if (flat == null) return null;
        var zs = areas.SelectMany(f => f.Outline).Select(p => p.Z).ToList();
        return (Polygon2.EnsureCcw(flat.Outline.Select(p => p.Xy).ToList()).ToArray(), zs.Min(), zs.Max());
    }
}

/// <summary>
/// Where props and scaffold feet stand: the highest floor top under a point
/// (below a level), else the ground, else the highest storey level below,
/// else three metres down.
/// </summary>
public sealed class Support
{
    readonly List<(Vec2[] Outline, double Z)> floors = [];
    readonly IGround? ground;
    readonly IModelView model;
    readonly List<double> storeys;

    public Support(IModelView m, IServices services, string? excludeId = null)
    {
        model = m;
        ground = services.Get<IGround>();
        foreach (var slab in m.OfClass(IfcClass.Slab))
        {
            if (slab.Id == excludeId) continue;
            if (ElementFaces.Find(services, slab, m, "Top") is { } top)
                floors.Add((top.Outline.Select(p => p.Xy).ToArray(), top.Outline[0].Z));
        }
        foreach (var footing in m.OfClass(IfcClass.Footing))
            if (ElementFaces.Find(services, footing, m, "Top") is { } top)
                floors.Add((top.Outline.Select(p => p.Xy).ToArray(), top.Outline[0].Z));
        storeys = Spatial.Storeys(m).Select(s => s.Num("elevation")).ToList();
    }

    /// <summary>What stands under (x, y) below <paramref name="under"/> (with half a metre clear at least).</summary>
    public double At(Vec2 p, double under)
    {
        var limit = under - 0.5;
        var best = double.NegativeInfinity;
        foreach (var (outline, z) in floors)
            if (z <= limit && z > best && Polygon2.Contains(outline, p)) best = z;
        if (double.IsFinite(best)) return best;
        if (ground?.ElevationAt(model, p.X, p.Y) is { } g && g <= limit) return g;
        var below = storeys.Where(z => z <= limit).DefaultIfEmpty(double.NaN).Max();
        return double.IsFinite(below) ? below : under - 3;
    }

    /// <summary>The ground or floor under a point, wherever it is (a scaffold's foot), else <paramref name="fallback"/>.</summary>
    public double Ground(Vec2 p, double fallback) =>
        ground?.ElevationAt(model, p.X, p.Y) ?? fallback;
}

/// <summary>Solids gathered by part, made one solid each at the end.</summary>
public sealed class Parts
{
    readonly Dictionary<string, (string Name, string Color, List<Solid> Solids)> parts = [];
    readonly List<string> order = [];

    public void Add(string key, string name, string color, Solid? s)
    {
        if (s == null) return;
        if (!parts.TryGetValue(key, out var p)) { parts[key] = p = (name, color, []); order.Add(key); }
        p.Solids.Add(s);
    }

    public int Count(string key) => parts.TryGetValue(key, out var p) ? p.Solids.Count : 0;

    public List<GeometryPart> Build() =>
        order.Select(k => (Key: k, Part: parts[k])).Select(x => new GeometryPart(x.Part.Name, x.Part.Color, Combine(x.Part.Solids, x.Key), null, x.Key)).ToList();

    public static Solid Combine(IReadOnlyList<Solid> solids, string tag)
    {
        if (solids.Count == 1) return solids[0];
        var vertices = new List<Vec3>();
        var faces = new List<Face>();
        foreach (var s in solids)
        {
            var at = vertices.Count;
            vertices.AddRange(s.Vertices);
            faces.AddRange(s.Faces.Select(f => f with { Outer = f.Outer.Select(i => i + at).ToArray(), Holes = f.Holes.Select(h => h.Select(i => i + at).ToArray()).ToArray() }));
        }
        return new Solid([.. vertices], [.. faces], tag);
    }
}
