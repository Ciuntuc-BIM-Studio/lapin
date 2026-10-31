using Lapin.Geometry;

namespace Lapin.Platform;

/// <summary>
/// Lines offset in a plane with mitred corners (each corner where the two offset sides meet): a closed outline grown or
/// shrunk, an open polyline moved to its left or right. What Studio's rings, boxes and wall strips and the walls along a
/// drafted shape are built from (docs/plan-studio-thick.md).
/// </summary>
public static class Offsets
{
    /// <summary>The points without repeats (and, closed, without the last one repeating the first) or points on a straight run.</summary>
    public static List<Vec2> Clean(IReadOnlyList<Vec2> pts, bool closed)
    {
        var o = new List<Vec2>();
        foreach (var p in pts) if (o.Count == 0 || (p - o[^1]).Length > 1e-9) o.Add(p);
        if (closed && o.Count > 1 && (o[^1] - o[0]).Length <= 1e-9) o.RemoveAt(o.Count - 1);
        // Points in the middle of a straight run add nothing but a corner of zero turn.
        for (var changed = true; changed && o.Count > (closed ? 3 : 2);)
        {
            changed = false;
            var n = o.Count;
            for (var i = closed ? 0 : 1; i < (closed ? n : n - 1); i++)
            {
                var (a, b, c) = (o[(i - 1 + n) % n], o[i], o[(i + 1) % n]);
                var (u, v) = ((b - a).Normalized(), (c - b).Normalized());
                if (Math.Abs(u.X * v.Y - u.Y * v.X) < 1e-9 && Vec2.Dot(u, v) > 0) { o.RemoveAt(i); changed = true; break; }
            }
        }
        return o;
    }

    /// <summary>The mitre at a corner between two sides with unit normals <paramref name="n1"/> and <paramref name="n2"/>: the offset point is the corner plus this times the distance.</summary>
    static Vec2 Mitre(Vec2 n1, Vec2 n2)
    {
        var k = 1 + Vec2.Dot(n1, n2);
        return k < 1e-6 ? n2 : (n1 + n2) * (1 / k);
    }

    /// <summary>A closed outline (counter-clockwise) moved <paramref name="d"/> outward (inward when negative), its corners mitred.</summary>
    public static List<Vec2> Loop(IReadOnlyList<Vec2> ccw, double d)
    {
        var n = ccw.Count;
        var normals = Enumerable.Range(0, n).Select(i => { var e = (ccw[(i + 1) % n] - ccw[i]).Normalized(); return new Vec2(e.Y, -e.X); }).ToList();
        return Enumerable.Range(0, n).Select(i => ccw[i] + Mitre(normals[(i - 1 + n) % n], normals[i]) * d).ToList();
    }

    /// <summary>An open polyline moved <paramref name="left"/> to its left (to its right when negative), its corners mitred, its ends square.</summary>
    public static List<Vec2> Open(IReadOnlyList<Vec2> pts, double left)
    {
        var n = pts.Count;
        var normals = Enumerable.Range(0, n - 1).Select(i => (pts[i + 1] - pts[i]).Normalized().Perp).ToList();
        return Enumerable.Range(0, n).Select(i => pts[i] + (i == 0 ? normals[0] : i == n - 1 ? normals[^1] : Mitre(normals[i - 1], normals[i])) * left).ToList();
    }
}
