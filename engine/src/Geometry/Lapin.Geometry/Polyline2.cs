namespace Lapin.Geometry;

/// <summary>
/// Open or closed polylines in plan, as the modules draw and lay them (a pipe,
/// a path, a drafted line): their length, repeated points dropped, the
/// distance to a segment, two segments crossing, and an offset with mitred
/// corners. One routine each, so the same polyline gives the same answer in
/// every module.
/// </summary>
public static class Polyline2
{
    /// <summary>The length along the points (a closed one's closing side not counted: <see cref="Polygon2.Perimeter"/> counts it).</summary>
    public static double Length(IReadOnlyList<Vec2> pts)
    {
        double l = 0;
        for (var i = 1; i < pts.Count; i++) l += (pts[i] - pts[i - 1]).Length;
        return l;
    }

    /// <summary>
    /// The points without those on the point before them (within
    /// <paramref name="tolerance"/>, a micrometre by default) and, for a loop,
    /// without a last one back on the first.
    /// </summary>
    public static List<Vec2> Dedupe(IReadOnlyList<Vec2> pts, bool closed = false, double tolerance = Tolerance.Distance)
    {
        var o = new List<Vec2>(pts.Count);
        foreach (var p in pts) if (o.Count == 0 || (p - o[^1]).Length > tolerance) o.Add(p);
        if (closed && o.Count > 2 && (o[0] - o[^1]).Length <= tolerance) o.RemoveAt(o.Count - 1);
        return o;
    }

    /// <summary>How far a point lies from the segment a–b (from a, when the segment has no length).</summary>
    public static double SegmentDistance(Vec2 p, Vec2 a, Vec2 b)
    {
        var d = b - a;
        var l2 = Vec2.Dot(d, d);
        var t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a, d) / l2, 0, 1);
        return (p - (a + d * t)).Length;
    }

    /// <summary>
    /// Whether the segments a–b and c–d cross strictly inside both (a shared
    /// end, a touch or a parallel overlap is no crossing); <paramref name="t"/>
    /// is where along a–b (0 at a, 1 at b).
    /// </summary>
    public static bool Crosses(Vec2 a, Vec2 b, Vec2 c, Vec2 d, out double t)
    {
        t = double.NaN;
        var (r, s) = (b - a, d - c);
        var den = Vec2.Cross(r, s);
        if (Math.Abs(den) < 1e-15 * Math.Max(1, r.Length * s.Length)) return false;
        var ta = Vec2.Cross(c - a, s) / den;
        var u = Vec2.Cross(c - a, r) / den;
        if (!(ta > 1e-9 && ta < 1 - 1e-9 && u > 1e-9 && u < 1 - 1e-9)) return false;
        t = ta;
        return true;
    }

    /// <inheritdoc cref="Crosses(Vec2, Vec2, Vec2, Vec2, out double)"/>
    public static bool Crosses(Vec2 a, Vec2 b, Vec2 c, Vec2 d) => Crosses(a, b, c, d, out _);

    /// <summary>
    /// The polyline shifted to its left by <paramref name="d"/> (right when
    /// negative), each corner on its bisector, mitred: the corner goes out
    /// d / cos(half the turn), at most <paramref name="maxMitre"/> times d (a
    /// pipe keeps a sharp turn's corner near it; a drafted offset does not
    /// limit it). A point where the line turns straight back is moved square
    /// to the leg before it. Open: the ends square to their legs; closed:
    /// every point a corner. Repeated points are the caller's to drop first.
    /// </summary>
    public static List<Vec2> Offset(IReadOnlyList<Vec2> pts, double d, bool closed = false, double maxMitre = double.PositiveInfinity)
    {
        var n = pts.Count;
        if (n < 2 || Math.Abs(d) < 1e-12) return [.. pts];
        var o = new List<Vec2>(n);
        for (var i = 0; i < n; i++)
        {
            var hasIn = closed || i > 0;
            var hasOut = closed || i < n - 1;
            var dIn = hasIn ? (pts[i] - pts[(i - 1 + n) % n]).Normalized() : (pts[i + 1] - pts[i]).Normalized();
            var dOut = hasOut ? (pts[(i + 1) % n] - pts[i]).Normalized() : dIn;
            var bis = (dIn + dOut).Normalized();
            var cos = Vec2.Dot(dIn, bis);
            if (bis.Length < 1e-9) (bis, cos) = (dIn, 1);
            var left = new Vec2(-bis.Y, bis.X);
            o.Add(pts[i] + left * (d / Math.Max(cos, 1 / maxMitre)));
        }
        return o;
    }
}
