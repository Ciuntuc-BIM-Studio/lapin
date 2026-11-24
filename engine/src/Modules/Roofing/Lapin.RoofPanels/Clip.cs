using Lapin.Geometry;

namespace Lapin.RoofPanels;

/// <summary>Polygons on a roof plane (u along the eave, v up the slope, metres): the half-plane clips the layout is made of.</summary>
public static class Clip
{
    public static IEnumerable<(Vec2 A, Vec2 B)> Edges(IReadOnlyList<Vec2> p)
    {
        for (var i = 0; i < p.Count; i++) yield return (p[i], p[(i + 1) % p.Count]);
    }

    public static double Area(IReadOnlyList<Vec2> p)
    {
        if (p.Count < 3) return 0;
        double s = 0;
        foreach (var (a, b) in Edges(p)) s += a.X * b.Y - a.Y * b.X;
        return Math.Abs(s) / 2;
    }

    /// <summary>The part of the polygon where direction · point ≥ limit.</summary>
    public static List<Vec2> Trim(IReadOnlyList<Vec2> poly, Vec2 direction, double limit)
    {
        var o = new List<Vec2>();
        if (poly.Count == 0) return o;
        foreach (var (a, b) in Edges(poly))
        {
            double ua = Vec2.Dot(a, direction) - limit, ub = Vec2.Dot(b, direction) - limit;
            if (ua >= -1e-9 != ub >= -1e-9) o.Add(a + (b - a) * (ua / (ua - ub)));
            if (ub >= -1e-9) o.Add(b);
        }
        return o;
    }

    /// <summary>The part between u = x0 and x1 (and v = y0 and y1, when given).</summary>
    public static List<Vec2> Strip(IReadOnlyList<Vec2> poly, double x0, double x1, double? y0 = null, double? y1 = null)
    {
        var r = Trim(Trim(poly, new Vec2(1, 0), x0), new Vec2(-1, 0), -x1);
        if (y0 is { } lo && y1 is { } hi) r = Trim(Trim(r, new Vec2(0, 1), lo), new Vec2(0, -1), -hi);
        return r;
    }

    public static double MinX(IReadOnlyList<Vec2> p) => p.Min(q => q.X);
    public static double MaxX(IReadOnlyList<Vec2> p) => p.Max(q => q.X);
    public static double MinY(IReadOnlyList<Vec2> p) => p.Min(q => q.Y);
    public static double MaxY(IReadOnlyList<Vec2> p) => p.Max(q => q.Y);

    public static List<Vec2> Shift(IReadOnlyList<Vec2> p, double dx, double dy) => [.. p.Select(q => new Vec2(q.X + dx, q.Y + dy))];

    /// <summary>
    /// Whether two polygons are apart by at least 4.9 mm across one of their edges' normals: a separating axis with clearance
    /// is enough, convex or not (an offcut fits beside the piece cut from the same panel).
    /// </summary>
    public static bool Separated(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b)
    {
        foreach (var poly in new[] { a, b })
            foreach (var (s, e) in Edges(poly))
            {
                var d = e - s;
                var mag = d.Length;
                if (mag < 1e-8) continue;
                var axis = new Vec2(-d.Y / mag, d.X / mag);
                double pmin = double.MaxValue, pmax = double.MinValue, qmin = double.MaxValue, qmax = double.MinValue;
                foreach (var p in a) { var t = Vec2.Dot(p, axis); pmin = Math.Min(pmin, t); pmax = Math.Max(pmax, t); }
                foreach (var q in b) { var t = Vec2.Dot(q, axis); qmin = Math.Min(qmin, t); qmax = Math.Max(qmax, t); }
                if (Math.Max(qmin - pmax, pmin - qmax) >= 0.0049) return true;
            }
        return false;
    }
}
