namespace Lapin.Geometry;

/// <summary>
/// A face split by the lines sketched on it (docs/plan-studio-sketch.md, stage B, decision 375): the face's outline and
/// the sketch's lines, arcs and shapes (flattened) make a planar graph; the region round a point is the smallest of its
/// faces holding it. Segments are split where they cross or touch, ends within the tolerance are one vertex, and lines
/// that stop short (a dangling end) bound nothing. Islands (a loop not touching the rest) are regions of their own; the
/// region round them does not leave them out. Shared by Studio's faces and the finish painter's (docs/plan-finish-painter.md).
/// </summary>
public static class SketchRegions
{
    /// <summary>The smallest region the segments bound round <paramref name="p"/> (counter-clockwise), or null when none holds it.</summary>
    public static List<Vec2>? RegionAt(IReadOnlyList<(Vec2 A, Vec2 B)> segments, Vec2 p, double tol = 1e-6) =>
        Regions(segments, tol).Where(r => Inside(r, p)).OrderBy(r => Math.Abs(Polygon2.SignedArea(r))).FirstOrDefault();

    /// <summary>Every bounded region (counter-clockwise loops) of the planar graph the segments make.</summary>
    public static List<List<Vec2>> Regions(IReadOnlyList<(Vec2 A, Vec2 B)> segments, double tol = 1e-6)
    {
        // Vertices, merged within the tolerance.
        var verts = new List<Vec2>();
        int V(Vec2 q)
        {
            for (var i = 0; i < verts.Count; i++) if ((verts[i] - q).Length <= tol) return i;
            verts.Add(q);
            return verts.Count - 1;
        }
        var segs = segments.Where(s => (s.B - s.A).Length > tol).ToList();
        // Each segment split where any other crosses or touches it.
        var edges = new HashSet<(int, int)>();
        for (var i = 0; i < segs.Count; i++)
        {
            var (a, b) = segs[i];
            var d = b - a;
            var len2 = d.X * d.X + d.Y * d.Y;
            var ts = new List<double> { 0, 1 };
            for (var j = 0; j < segs.Count; j++)
            {
                if (j == i) continue;
                var (c, e) = segs[j];
                foreach (var q in new[] { c, e })
                {
                    var t = ((q.X - a.X) * d.X + (q.Y - a.Y) * d.Y) / len2;
                    if (t > 0 && t < 1 && (a + d * t - q).Length <= tol) ts.Add(t);
                }
                var f = e - c;
                var den = d.X * f.Y - d.Y * f.X;
                if (Math.Abs(den) < 1e-15) continue;
                var w = c - a;
                var u = (w.X * f.Y - w.Y * f.X) / den;
                var v = (w.X * d.Y - w.Y * d.X) / den;
                if (u > 0 && u < 1 && v >= -1e-9 && v <= 1 + 1e-9) ts.Add(u);
            }
            ts.Sort();
            var idx = ts.Select(t => V(a + d * t)).ToList();
            for (var k = 0; k + 1 < idx.Count; k++)
                if (idx[k] != idx[k + 1]) edges.Add(idx[k] < idx[k + 1] ? (idx[k], idx[k + 1]) : (idx[k + 1], idx[k]));
        }
        // Dangling ends bound nothing: taken away until none is left.
        var adj = Enumerable.Range(0, verts.Count).Select(_ => new HashSet<int>()).ToList();
        foreach (var (u, v) in edges) { adj[u].Add(v); adj[v].Add(u); }
        var queue = new Queue<int>(Enumerable.Range(0, verts.Count).Where(i => adj[i].Count == 1));
        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            foreach (var j in adj[i].ToList())
            {
                adj[i].Remove(j); adj[j].Remove(i);
                if (adj[j].Count == 1) queue.Enqueue(j);
            }
        }
        // Around each vertex, its neighbours by angle; a face goes on by the next edge clockwise from the one it came by.
        var around = adj.Select((n, i) => n.OrderBy(j => Math.Atan2(verts[j].Y - verts[i].Y, verts[j].X - verts[i].X)).ToList()).ToList();
        var used = new HashSet<(int, int)>();
        var o = new List<List<Vec2>>();
        for (var s = 0; s < verts.Count; s++)
            foreach (var t0 in around[s])
            {
                if (used.Contains((s, t0))) continue;
                var loop = new List<int>();
                var (u, v) = (s, t0);
                var guard = 0;
                while (used.Add((u, v)) && guard++ < 100000)
                {
                    loop.Add(u);
                    var list = around[v];
                    var k = list.IndexOf(u);
                    // The neighbour before u in counter-clockwise order: the sharpest left turn keeps the face on the left.
                    var w = list[(k - 1 + list.Count) % list.Count];
                    (u, v) = (v, w);
                }
                if (loop.Count < 3) continue;
                var pts = loop.Select(i => verts[i]).ToList();
                if (Polygon2.SignedArea(pts) > 1e-12) o.Add(pts);
            }
        return o;
    }

    static bool Inside(IReadOnlyList<Vec2> poly, Vec2 p)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var (a, b) = (poly[i], poly[j]);
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }
}
