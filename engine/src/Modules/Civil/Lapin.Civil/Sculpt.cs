using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Civil;

/// <summary>One stroke of the terrain brush: where, how wide, and what it does (raise or lower by an amount, flatten to a level, smooth with a strength).</summary>
public sealed record SculptDab(string Kind, Vec2 At, double Radius, double Amount, double Level, double Strength)
{
    public static readonly string[] Kinds = ["raise", "lower", "flatten", "smooth"];

    public JsonObject ToJson() => new() { ["kind"] = Kind, ["x"] = At.X, ["y"] = At.Y, ["radius"] = Radius, ["amount"] = Amount, ["level"] = Level, ["strength"] = Strength };

    public static SculptDab? From(JsonNode? n) =>
        n is JsonObject o && Kinds.Contains((string?)o["kind"]) && Json.Number(o["radius"]) is { } r && r > 0.05
            ? new((string)o["kind"]!, new Vec2(Json.Number(o["x"]) ?? 0, Json.Number(o["y"]) ?? 0), r, Json.Number(o["amount"]) ?? 0, Json.Number(o["level"]) ?? 0, Math.Clamp(Json.Number(o["strength"]) ?? 0.5, 0, 1))
            : null;
}

/// <summary>
/// The terrain brush on a TIN. A stroke is kept as data on the terrain and
/// applied again whenever the terrain is built, in order, over the survey:
/// it can be undone and cleared. Each stroke makes the TIN dense inside its
/// circle (a grid at a quarter of the radius) and moves the heights there by
/// a weight that fades smoothly to nothing at the rim, so the ground stays
/// continuous around it. The ground a stroke reads is the triangulation of
/// the points so far, made only around the stroke: a triangle of it is taken
/// when its circle lies inside the points gathered (so it is the whole
/// triangulation's too), else more are gathered, up to all of them.
/// </summary>
public static class Sculpt
{
    public static List<SculptDab> Of(Entity terrain) =>
        (terrain.Attrs["sculpts"] as JsonArray)?.Select(SculptDab.From).OfType<SculptDab>().ToList() ?? [];

    /// <summary>The points (model heights) after the strokes; <paramref name="segments"/> index the breaklines, which keep their indices (points are only appended or moved).</summary>
    public static List<Vec3> Apply(List<Vec3> points, IReadOnlyList<(int A, int B)> segments, IReadOnlyList<SculptDab> dabs)
    {
        if (dabs.Count == 0 || !Tin.Spans(points)) return points;
        // Strokes only add points inside the TIN and move none in plan: its outline stays the survey's.
        var hull = Hull(points);
        foreach (var dab in dabs) points = One(points, segments, dab, hull);
        return points;
    }

    static double Weight(SculptDab d, Vec2 p)
    {
        var t = 1 - (p - d.At).Length / d.Radius;
        return t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);
    }

    /// <summary>A sampler that cannot tell the ground at a point from the points it was given.</summary>
    sealed class TooFew : Exception;

    static List<Vec3> One(List<Vec3> points, IReadOnlyList<(int A, int B)> segments, SculptDab d, IReadOnlyList<Vec2> hull)
    {
        var step = Math.Max(d.Radius / 4, 0.25);
        // What the stroke reads: the grid inside its circle, and a step around each (smoothing).
        var reach = d.Radius + step + 1e-6;
        for (var gather = reach * 2; ; gather *= 2)
        {
            var near = Enumerable.Range(0, points.Count).Where(i => (points[i].Xy - d.At).Length <= gather).ToList();
            if (near.Count == points.Count)
            {
                var whole = new Terrain(Tin.Delaunay(points, segments));
                return Stroke(points, d, step, (x, y) => whole.ElevationAt(x, y));
            }
            // A breakline reaching the gathered disc but not wholly in it would bend triangles the local one cannot see.
            var index = new Dictionary<int, int>();
            for (var k = 0; k < near.Count; k++) index[near[k]] = k;
            var local = new List<(int, int)>();
            var blind = false;
            foreach (var (a, b) in segments)
            {
                if (index.TryGetValue(a, out var la) && index.TryGetValue(b, out var lb)) { local.Add((la, lb)); continue; }
                var (pa, pb) = (points[a].Xy, points[b].Xy);
                if (Polyline2.SegmentDistance(d.At, pa, pb) <= gather) { blind = true; break; }
            }
            if (blind) continue;
            var tin = Tin.Delaunay(near.Select(i => points[i]).ToList(), local);
            var t = new Terrain(tin);
            double? Sample(double x, double y)
            {
                if (!Inside(hull, new Vec2(x, y))) return null;
                var (tri, z) = t.TriangleAt(x, y);
                if (tri < 0) throw new TooFew();
                var (ia, ib, ic) = tin.Triangles[tri];
                var (cc, r) = Circle(tin.Points[ia].Xy, tin.Points[ib].Xy, tin.Points[ic].Xy);
                if (!((cc - d.At).Length + r < gather * (1 - 1e-9))) throw new TooFew();
                return z;
            }
            try { return Stroke(points, d, step, Sample); }
            catch (TooFew) { }
        }
    }

    /// <summary>One stroke over the points, the ground before it read by <paramref name="before"/> (null outside the TIN).</summary>
    static List<Vec3> Stroke(List<Vec3> points, SculptDab d, double step, Func<double, double, double?> before)
    {
        double Target(Vec2 p, double z0, double w) => d.Kind switch
        {
            "raise" => z0 + d.Amount * w,
            "lower" => z0 - d.Amount * w,
            "flatten" => z0 + (d.Level - z0) * w,
            _ => z0 + (Around(before, p, step) - z0) * w * d.Strength,
        };
        var o = new List<Vec3>(points.Count);
        var taken = new HashSet<(long, long)>();
        foreach (var p in points)
        {
            var w = Weight(d, new Vec2(p.X, p.Y));
            o.Add(w > 0 ? p with { Z = Target(new Vec2(p.X, p.Y), p.Z, w) } : p);
            taken.Add(Key(p.X, p.Y, step));
        }
        for (var x = d.At.X - d.Radius; x <= d.At.X + d.Radius + 1e-9; x += step)
            for (var y = d.At.Y - d.Radius; y <= d.At.Y + d.Radius + 1e-9; y += step)
            {
                var p = new Vec2(x, y);
                var w = Weight(d, p);
                if (w <= 1e-6 || !taken.Add(Key(x, y, step)) || before(x, y) is not { } z0) continue;
                o.Add(new Vec3(x, y, Target(p, z0, w)));
            }
        return o;
    }

    /// <summary>The mean height of the ground a step away in four directions (what smoothing pulls towards).</summary>
    static double Around(Func<double, double, double?> t, Vec2 p, double step)
    {
        var sum = 0.0; var n = 0;
        foreach (var (dx, dy) in new[] { (step, 0.0), (-step, 0.0), (0.0, step), (0.0, -step) })
            if (t(p.X + dx, p.Y + dy) is { } z) { sum += z; n++; }
        return n == 0 ? t(p.X, p.Y) ?? 0 : sum / n;
    }

    static (long, long) Key(double x, double y, double step) => ((long)Math.Round(x / (step * 0.45)), (long)Math.Round(y / (step * 0.45)));

    /// <summary>The circle through three points: its centre and radius (infinite for points in a line).</summary>
    static (Vec2 Centre, double R) Circle(Vec2 a, Vec2 b, Vec2 c)
    {
        var (bx, by, cx, cy) = (b.X - a.X, b.Y - a.Y, c.X - a.X, c.Y - a.Y);
        var den = 2 * (bx * cy - by * cx);
        if (Math.Abs(den) < 1e-300) return (a, double.PositiveInfinity);
        var (b2, c2) = (bx * bx + by * by, cx * cx + cy * cy);
        var ux = (cy * b2 - by * c2) / den;
        var uy = (bx * c2 - cx * b2) / den;
        return (new Vec2(a.X + ux, a.Y + uy), Math.Sqrt(ux * ux + uy * uy));
    }

    /// <summary>The points' convex hull in plan, counter-clockwise (Andrew's monotone chain).</summary>
    static List<Vec2> Hull(IReadOnlyList<Vec3> points)
    {
        var p = points.Where(q => double.IsFinite(q.X) && double.IsFinite(q.Y)).Select(q => q.Xy).Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
        if (p.Count < 3) return p;
        var h = new List<Vec2>(p.Count * 2);
        foreach (var pass in new[] { p, Enumerable.Reverse(p).ToList() })
        {
            var start = h.Count;
            foreach (var q in pass)
            {
                while (h.Count >= start + 2 && Vec2.Cross(h[^1] - h[^2], q - h[^2]) <= 0) h.RemoveAt(h.Count - 1);
                h.Add(q);
            }
            h.RemoveAt(h.Count - 1);
        }
        return h;
    }

    /// <summary>Whether a point is inside a convex counter-clockwise outline or within a micrometre of it.</summary>
    static bool Inside(IReadOnlyList<Vec2> hull, Vec2 q)
    {
        for (var i = 0; i < hull.Count; i++)
        {
            var (a, b) = (hull[i], hull[(i + 1) % hull.Count]);
            var len = (b - a).Length;
            if (len > 0 && Vec2.Cross(b - a, q - a) / len < -1e-6) return false;
        }
        return true;
    }
}
