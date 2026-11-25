using Lapin.Geometry;

namespace Lapin.RoofPanels;

/// <summary>
/// A roof plane in plan less what other roofs take away where they meet it (decision 847, <c>RoofJunctions.Cuts</c>): the
/// plane cut into triangles, each less every convex region over it (a convex piece less a convex region is the convex pieces
/// outside each of the region's sides in turn), and the pieces joined again by the edges they do not share. What is left is
/// one ring or several (a plane a roof cuts across in two), counter-clockwise; a region wholly inside the plane leaves a hole,
/// returned apart (the layout does not cut holes yet).
/// </summary>
public static class PlanTrim
{
    /// <summary>Corners this close are one (m).</summary>
    const double Snap = 1e-6;

    public static (List<List<Vec2>> Rings, List<List<Vec2>> Holes) Difference(IReadOnlyList<Vec2> ring, IReadOnlyList<IReadOnlyList<Vec2>> cuts)
    {
        var outline = Ccw(ring);
        var (min, max) = (new Vec2(outline.Min(p => p.X), outline.Min(p => p.Y)), new Vec2(outline.Max(p => p.X), outline.Max(p => p.Y)));
        var mine = cuts.Where(c => c.Count >= 3 && Math.Abs(Polygon2.SignedArea(c)) > 1e-9 && Overlaps(c, min, max)).Select(Ccw).ToList();
        if (mine.Count == 0) return ([outline], []);

        // The plane as triangles, each less every region.
        var flat = outline.Select(p => new Vec3(p.X, p.Y, 0)).ToList();
        var pieces = Triangulator.Face(flat, [.. Enumerable.Range(0, outline.Count)], null, Vec3.UnitZ)
            .Select(t => Ccw([outline[t.A], outline[t.B], outline[t.C]])).Where(t => Math.Abs(Polygon2.SignedArea(t)) > 1e-12).ToList();
        foreach (var cut in mine)
        {
            var next = new List<List<Vec2>>();
            foreach (var piece in pieces)
            {
                var rest = piece;
                for (var i = 0; i < cut.Count && rest.Count >= 3; i++)
                {
                    var (p, q) = (cut[i], cut[(i + 1) % cut.Count]);
                    var inward = (q - p).Perp;
                    var outside = Clip.Trim(rest, -inward, -Vec2.Dot(p, inward));
                    if (outside.Count >= 3 && Clip.Area(outside) > 1e-10) next.Add(outside);
                    rest = Clip.Trim(rest, inward, Vec2.Dot(p, inward));
                }
            }
            pieces = next;
        }
        return Join(pieces);
    }

    static bool Overlaps(IReadOnlyList<Vec2> c, Vec2 min, Vec2 max) =>
        c.Min(p => p.X) < max.X - 1e-7 && c.Max(p => p.X) > min.X + 1e-7 && c.Min(p => p.Y) < max.Y - 1e-7 && c.Max(p => p.Y) > min.Y + 1e-7;

    static List<Vec2> Ccw(IReadOnlyList<Vec2> r)
    {
        // Corners repeated (a cut region's side of no length) dropped: a side with no direction would keep every piece outside it.
        var o = new List<Vec2>();
        foreach (var p in r) if (o.Count == 0 || (o[^1] - p).Length >= 1e-9) o.Add(p);
        while (o.Count > 1 && (o[0] - o[^1]).Length < 1e-9) o.RemoveAt(o.Count - 1);
        if (Polygon2.SignedArea(o) < 0) o.Reverse();
        return o;
    }

    /// <summary>The outline of convex pieces that touch along their sides: every side split at the corners on it, the sides two pieces share dropped, the rest chained.</summary>
    static (List<List<Vec2>> Rings, List<List<Vec2>> Holes) Join(List<List<Vec2>> pieces)
    {
        var points = new List<Vec2>();
        int Id(Vec2 p)
        {
            for (var i = 0; i < points.Count; i++) if ((points[i] - p).Length < Snap) return i;
            points.Add(p);
            return points.Count - 1;
        }
        var loops = pieces.Select(pc => pc.Select(Id).ToList()).ToList();
        var edges = new List<(int A, int B)>();
        foreach (var loop in loops)
            for (var i = 0; i < loop.Count; i++)
            {
                var (a, b) = (loop[i], loop[(i + 1) % loop.Count]);
                if (a == b) continue;
                var (pa, pb) = (points[a], points[b]);
                var d = pb - pa;
                var len2 = Vec2.Dot(d, d);
                var on = new List<(double T, int Id)>();
                for (var k = 0; k < points.Count; k++)
                {
                    if (k == a || k == b) continue;
                    var t = Vec2.Dot(points[k] - pa, d) / len2;
                    if (t <= 1e-9 || t >= 1 - 1e-9) continue;
                    if ((pa + d * t - points[k]).Length < Snap) on.Add((t, k));
                }
                on.Sort((x, y) => x.T.CompareTo(y.T));
                var prev = a;
                foreach (var (_, k) in on) { edges.Add((prev, k)); prev = k; }
                edges.Add((prev, b));
            }
        // Sides two pieces share run both ways: they cancel.
        var count = new Dictionary<(int, int), int>();
        foreach (var e in edges) count[e] = count.GetValueOrDefault(e) + 1;
        var boundary = new List<(int A, int B)>();
        foreach (var e in edges)
        {
            var back = (e.B, e.A);
            if (count.GetValueOrDefault(back) > 0 && count[e] > 0) { count[back]--; count[e]--; continue; }
            if (count[e] > 0) { boundary.Add(e); count[e]--; }
        }
        var from = new Dictionary<int, List<int>>();
        foreach (var (a, b) in boundary) { if (!from.TryGetValue(a, out var l)) from[a] = l = []; l.Add(b); }
        var rings = new List<List<Vec2>>();
        var holes = new List<List<Vec2>>();
        while (from.Count > 0)
        {
            var start = from.Keys.First();
            var loop = new List<int> { start };
            var at = start;
            while (true)
            {
                if (!from.TryGetValue(at, out var outs) || outs.Count == 0) break;
                var to = outs[^1];
                outs.RemoveAt(outs.Count - 1);
                if (outs.Count == 0) from.Remove(at);
                if (to == start) break;
                loop.Add(to);
                at = to;
                if (loop.Count > boundary.Count + 1) break;
            }
            var ring = Simplify(loop.Select(i => points[i]).ToList());
            if (ring.Count < 3 || Math.Abs(Polygon2.SignedArea(ring)) < 1e-8) continue;
            (Polygon2.SignedArea(ring) > 0 ? rings : holes).Add(ring);
        }
        return (rings, holes);
    }

    /// <summary>Corners in line with their neighbours dropped.</summary>
    static List<Vec2> Simplify(List<Vec2> o)
    {
        var changed = true;
        while (changed && o.Count > 3)
        {
            changed = false;
            for (var i = 0; i < o.Count; i++)
            {
                var (a, p, b) = (o[(i - 1 + o.Count) % o.Count], o[i], o[(i + 1) % o.Count]);
                var ab = b - a;
                if (ab.Length < 1e-12 || Math.Abs(Vec2.Cross(ab, p - a)) / ab.Length < 1e-7) { o.RemoveAt(i); changed = true; break; }
            }
        }
        return o;
    }
}
