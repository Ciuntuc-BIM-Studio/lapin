using Lapin.Geometry;

namespace Lapin.Platform.Analysis;

/// <summary>
/// A shell's surface cut into the pieces the exchange file can hold (docs/jg-structures.md, The analytical model): convex
/// polygons without holes. A non-convex outline is cut at each re-entrant corner, along a support line through it when one
/// passes there (a beam, a wall), else along the corner's own edge; then every support line crossing a piece from edge to
/// edge splits it; points that must be nodes inside a piece split it along a line through them; and each opening (a door,
/// a window, a floor opening) is taken out as the strips round it, cut along its edges. All in the shell's own plane (a
/// floor's plan, a wall's along × up), so the pieces meet edge to edge and share their nodes.
/// </summary>
public static class ShellPieces
{
    const double Eps = 1e-9;

    /// <summary>Whether a counter-clockwise polygon is convex (collinear corners allowed).</summary>
    public static bool Convex(IReadOnlyList<Vec2> ccw)
    {
        for (var i = 0; i < ccw.Count; i++)
            if (Turn(ccw[(i - 1 + ccw.Count) % ccw.Count], ccw[i], ccw[(i + 1) % ccw.Count]) < -1e-7) return false;
        return true;
    }

    /// <summary>The turn at b (sine of the angle between the unit edges): negative at a re-entrant corner of a counter-clockwise polygon.</summary>
    static double Turn(Vec2 a, Vec2 b, Vec2 c)
    {
        var (u, v) = (b - a, c - b);
        var (lu, lv) = (u.Length, v.Length);
        return lu < Eps || lv < Eps ? 0 : Vec2.Cross(u, v) / (lu * lv);
    }

    /// <summary>A polygon without repeated or collinear corners, counter-clockwise.</summary>
    public static List<Vec2> Clean(IEnumerable<Vec2> poly)
    {
        var o = new List<Vec2>();
        foreach (var v in poly) if (o.Count == 0 || (o[^1] - v).Length > 1e-7) o.Add(v);
        while (o.Count > 1 && (o[0] - o[^1]).Length <= 1e-7) o.RemoveAt(o.Count - 1);
        for (var changed = true; changed && o.Count >= 3;)
        {
            changed = false;
            for (var i = 0; i < o.Count && o.Count >= 3; i++)
            {
                var (a, b, c) = (o[(i - 1 + o.Count) % o.Count], o[i], o[(i + 1) % o.Count]);
                if (Math.Abs(Turn(a, b, c)) < 1e-9 && Vec2.Dot(b - a, c - b) > 0) { o.RemoveAt(i); changed = true; i--; }
            }
        }
        return o.Count >= 3 && Polygon2.SignedArea(o) < 0 ? Enumerable.Reverse(o).ToList() : o;
    }

    /// <summary>
    /// The polygon cut into convex pieces: at a re-entrant corner along a support line through it (within 2 cm) when that
    /// line runs into the corner's wedge, else along the extension of the corner's shorter-cutting edge.
    /// </summary>
    public static List<List<Vec2>> ConvexPieces(IReadOnlyList<Vec2> poly, IReadOnlyList<(Vec2 A, Vec2 B)> lines)
    {
        var done = new List<List<Vec2>>();
        var queue = new Stack<List<Vec2>>();
        queue.Push(Clean(poly));
        for (var guard = 0; queue.Count > 0 && guard < 2000; guard++)
        {
            var p = queue.Pop();
            if (p.Count < 3) continue;
            var iv = -1;
            for (var i = 0; i < p.Count && iv < 0; i++)
                if (Turn(p[(i - 1 + p.Count) % p.Count], p[i], p[(i + 1) % p.Count]) < -1e-7) iv = i;
            if (iv < 0) { done.Add(p); continue; }
            var (u, v, w) = (p[(iv - 1 + p.Count) % p.Count], p[iv], p[(iv + 1) % p.Count]);
            bool InWedge(Vec2 d) => Vec2.Cross(w - v, d) >= -1e-9 && Vec2.Cross(d, u - v) >= -1e-9;
            (Vec2 H, int Edge, double T)? best = null;
            foreach (var (a, b) in lines)
            {
                if (DistToSegment(v, a, b) > 0.02 || (b - a).Length < Eps) continue;
                var d = (b - a).Normalized();
                foreach (var dd in new[] { d, -d })
                    if (InWedge(dd) && Ray(p, iv, dd) is { } hit && (best == null || hit.T < best.Value.T)) best = hit;
            }
            if (best == null)
                foreach (var d in new[] { (v - u).Normalized(), (v - w).Normalized() })
                    if (Ray(p, iv, d) is { } hit && (best == null || hit.T < best.Value.T)) best = hit;
            if (best is not { } h) { done.Add(p); continue; }
            var (one, two) = (new List<Vec2> { v }, new List<Vec2> { h.H });
            for (var k = (iv + 1) % p.Count; ; k = (k + 1) % p.Count) { one.Add(p[k]); if (k == h.Edge) break; }
            one.Add(h.H);
            for (var k = (h.Edge + 1) % p.Count; ; k = (k + 1) % p.Count) { two.Add(p[k]); if (k == iv) break; }
            var (c1, c2) = (Clean(one), Clean(two));
            if (c1.Count < 3 || c2.Count < 3 || Math.Abs(Polygon2.SignedArea(c1)) < 1e-9 || Math.Abs(Polygon2.SignedArea(c2)) < 1e-9) { done.Add(p); continue; }
            queue.Push(c1);
            queue.Push(c2);
        }
        return done;
    }

    /// <summary>Where a ray from corner <paramref name="iv"/> along d first meets the polygon's boundary: the point, the edge (from its corner), how far.</summary>
    static (Vec2 H, int Edge, double T)? Ray(IReadOnlyList<Vec2> p, int iv, Vec2 d)
    {
        var v = p[iv];
        (Vec2, int, double)? best = null;
        for (var j = 0; j < p.Count; j++)
        {
            var k = (j + 1) % p.Count;
            if (j == iv || k == iv) continue;
            var (a, b) = (p[j], p[k]);
            var e = b - a;
            var den = Vec2.Cross(d, e);
            if (Math.Abs(den) < 1e-14) continue;
            var t = Vec2.Cross(a - v, e) / den;
            var s = Vec2.Cross(a - v, d) / den;
            if (t <= 1e-9 || s < -1e-9 || s > 1 + 1e-9) continue;
            if (best == null || t < best.Value.Item3) best = (v + d * t, j, t);
        }
        return best;
    }

    public static double DistToSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        var l2 = Vec2.Dot(ab, ab);
        var t = l2 > 0 ? Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1) : 0;
        return (a + ab * t - p).Length;
    }

    /// <summary>
    /// A convex piece cut in two along the line a–b when the segment runs inside it (over more than twice the tolerance): along
    /// the whole chord, so a support that stops short of the piece's edge (a wall under a roof's overhang, a wall standing on a
    /// floor) still becomes an edge of its pieces; else the piece as it is.
    /// </summary>
    public static IEnumerable<List<Vec2>> Split(List<Vec2> piece, Vec2 a, Vec2 b, double tolerance)
    {
        var ab = b - a;
        var len = ab.Length;
        if (len <= tolerance) return [piece];
        var dir = ab * (1 / len);
        var n = dir.Perp;
        double Side(Vec2 p) => Vec2.Dot(p - a, n);
        if (piece.All(p => Side(p) >= -tolerance) || piece.All(p => Side(p) <= tolerance)) return [piece];
        var ts = new List<double>();
        for (var i = 0; i < piece.Count; i++)
        {
            var (p, q) = (piece[i], piece[(i + 1) % piece.Count]);
            var (sp, sq) = (Side(p), Side(q));
            if ((sp < 0) != (sq < 0) && Math.Abs(sp - sq) > 1e-12) ts.Add(Vec2.Dot(p + (q - p) * (sp / (sp - sq)) - a, dir));
        }
        if (ts.Count < 2 || Math.Min(ts.Max(), len) - Math.Max(ts.Min(), 0) <= tolerance * 2) return [piece];
        var (left, right) = (Clean(Clip(piece, a, n, 1)), Clean(Clip(piece, a, n, -1)));
        return left.Count >= 3 && right.Count >= 3 && Math.Abs(Polygon2.SignedArea(left)) > 1e-4 && Math.Abs(Polygon2.SignedArea(right)) > 1e-4 ? [left, right] : [piece];
    }

    /// <summary>A convex polygon clipped to the side of the line through a (normal n) where sign·(p − a)·n ≥ 0.</summary>
    static List<Vec2> Clip(IReadOnlyList<Vec2> poly, Vec2 a, Vec2 n, int sign)
    {
        var o = new List<Vec2>();
        for (var i = 0; i < poly.Count; i++)
        {
            var (p, q) = (poly[i], poly[(i + 1) % poly.Count]);
            var (sp, sq) = (Vec2.Dot(p - a, n) * sign, Vec2.Dot(q - a, n) * sign);
            if (sp >= 0) o.Add(p);
            if ((sp >= 0) != (sq >= 0) && Math.Abs(sp - sq) > 1e-15) o.Add(p + (q - p) * (sp / (sp - sq)));
        }
        return o;
    }

    /// <summary>
    /// A convex piece less a convex hole, as convex strips round it: what lies outside each of the hole's edges in turn, its
    /// longest edge first and the one facing it next, so a rectangular opening leaves two strips across and two beside it.
    /// </summary>
    public static List<List<Vec2>> Minus(List<Vec2> piece, IReadOnlyList<Vec2> hole)
    {
        var h = Clean(hole);
        if (h.Count < 3 || !Overlap(piece, h)) return [piece];
        var n = h.Count;
        var first = Enumerable.Range(0, n).MaxBy(i => (h[(i + 1) % n] - h[i]).Length);
        var d0 = (h[(first + 1) % n] - h[first]).Normalized();
        var facing = Enumerable.Range(0, n).Where(i => i != first).MinBy(i => Vec2.Dot((h[(i + 1) % n] - h[i]).Normalized(), d0));
        var order = new List<int> { first, facing };
        for (var k = 1; k < n; k++) if (!order.Contains((first + k) % n)) order.Add((first + k) % n);
        var o = new List<List<Vec2>>();
        var rest = piece;
        foreach (var i in order)
        {
            var (a, b) = (h[i], h[(i + 1) % n]);
            var normal = (b - a).Normalized().Perp;   // the hole's inside is to the left of a counter-clockwise edge
            var outside = Clean(Clip(rest, a, normal, -1));
            if (outside.Count >= 3 && Math.Abs(Polygon2.SignedArea(outside)) > 1e-6) o.Add(outside);
            rest = Clean(Clip(rest, a, normal, 1));
            if (rest.Count < 3) break;
        }
        return o;
    }

    /// <summary>Whether two convex polygons overlap by more than a touch (no separating edge).</summary>
    static bool Overlap(IReadOnlyList<Vec2> p, IReadOnlyList<Vec2> q)
    {
        bool Separates(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b)
        {
            for (var i = 0; i < a.Count; i++)
            {
                var (s, e) = (a[i], a[(i + 1) % a.Count]);
                var nrm = (e - s).Normalized().Perp;
                if (b.All(v => Vec2.Dot(v - s, nrm) <= 1e-6)) return true;
            }
            return false;
        }
        return !Separates(p, q) && !Separates(q, p);
    }

    /// <summary>How wide a piece is: twice its area over its perimeter (a strip's width; a square's half side).</summary>
    public static double Width(IReadOnlyList<Vec2> p)
    {
        var perimeter = 0.0;
        for (var i = 0; i < p.Count; i++) perimeter += (p[(i + 1) % p.Count] - p[i]).Length;
        return perimeter > 0 ? 2 * Math.Abs(Polygon2.SignedArea(p)) / perimeter : 0;
    }

    /// <summary>
    /// A shell's pieces: the outline cut into convex pieces at its re-entrant corners (support lines first), split by every
    /// support line crossing a piece, by a line along <paramref name="through"/> at each point that must be a node inside a
    /// piece, and with its holes taken out; pieces narrower than <paramref name="minWidth"/> (a hole's edge a centimetre off the
    /// piece's) go with the hole.
    /// </summary>
    public static List<List<Vec2>> Pieces(IReadOnlyList<Vec2> outline, IReadOnlyList<(Vec2 A, Vec2 B)> lines, IReadOnlyList<Vec2> points, Vec2 through,
        IReadOnlyList<IReadOnlyList<Vec2>> holes, double tolerance, double minWidth)
    {
        var pieces = ConvexPieces(outline, lines);
        foreach (var (a, b) in lines) pieces = pieces.SelectMany(p => Split(p, a, b, tolerance)).ToList();
        var holeParts = holes.SelectMany(h => ConvexPieces(h, [])).ToList();
        var far = 1e4;
        foreach (var q in points)
        {
            if (holeParts.Any(h => Polygon2.Contains(h, q))) continue;
            var k = pieces.FindIndex(p => Polygon2.Contains(p, q) && Boundary(p, q) > tolerance);
            if (k < 0) continue;
            var split = Split(pieces[k], q - through * far, q + through * far, tolerance).ToList();
            if (split.Count < 2) continue;
            pieces.RemoveAt(k);
            pieces.AddRange(split);
        }
        foreach (var h in holeParts) pieces = pieces.SelectMany(p => Minus(p, h)).ToList();
        return pieces.Where(p => p.Count >= 3 && Width(p) >= minWidth).ToList();
    }

    /// <summary>How far a point is from a polygon's boundary.</summary>
    public static double Boundary(IReadOnlyList<Vec2> p, Vec2 q)
    {
        var best = double.PositiveInfinity;
        for (var i = 0; i < p.Count; i++) best = Math.Min(best, DistToSegment(q, p[i], p[(i + 1) % p.Count]));
        return best;
    }
}
