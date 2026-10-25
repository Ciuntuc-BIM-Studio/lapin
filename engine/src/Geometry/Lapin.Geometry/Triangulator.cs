namespace Lapin.Geometry;

/// <summary>
/// Planar face triangulation: ear clipping with hole bridging, in the face's own
/// plane basis so triangles come out CCW around the face normal. Self-contained,
/// so the kernel depends on nothing.
/// </summary>
public static class Triangulator
{
    static double Cross2(Vec2 a, Vec2 b, Vec2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    static bool InTriangle(Vec2 a, Vec2 b, Vec2 c, Vec2 p) =>
        Cross2(a, b, p) >= 0 && Cross2(b, c, p) >= 0 && Cross2(c, a, p) >= 0;

    static bool Same(Vec2 a, Vec2 b, double tol) => Math.Abs(a.X - b.X) <= tol && Math.Abs(a.Y - b.Y) <= tol;

    /// <summary>
    /// The face's vertices in its plane, projected once each. A class and plain
    /// loops rather than a delegate and LINQ: the browser's AOT build runs those
    /// through its interpreter.
    /// </summary>
    sealed class Flat(IReadOnlyList<Vec3> points, Vec3 u, Vec3 v)
    {
        readonly Dictionary<int, int> index = [];
        readonly List<Vec2> flat = [];

        public Vec2 At(int id)
        {
            if (!index.TryGetValue(id, out var k))
            {
                k = flat.Count;
                flat.Add(VecMath.Project(points[id], u, v));
                index[id] = k;
            }
            return flat[k];
        }

        public Vec2[] Of(IReadOnlyList<int> ids)
        {
            var o = new Vec2[ids.Count];
            for (int i = 0; i < o.Length; i++) o[i] = At(ids[i]);
            return o;
        }
    }

    /// <summary>Indices 0..n-1 by ascending key, ties in index order (as a stable OrderBy).</summary>
    static int[] OrderBy(double[] key)
    {
        var order = new int[key.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        Array.Sort(order, (x, y) => { var c = key[x].CompareTo(key[y]); return c != 0 ? c : x.CompareTo(y); });
        return order;
    }

    /// <summary>
    /// Ear-clip a simple CCW ring. Ids may repeat (bridging duplicates two
    /// vertices), so every test is positional rather than by identity.
    /// </summary>
    static List<(int, int, int)> EarClip(List<int> poly, Flat flat, double tol)
    {
        Vec2 at(int id) => flat.At(id);
        var o = new List<(int, int, int)>();
        var ring = new List<int>(poly);
        int guard = ring.Count * ring.Count + 16;

        while (ring.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < ring.Count; i++)
            {
                int n = ring.Count, ip = (i - 1 + n) % n, inx = (i + 1) % n;
                Vec2 a = at(ring[ip]), b = at(ring[i]), c = at(ring[inx]);
                if (Cross2(a, b, c) <= tol) continue;
                bool blocked = false;
                for (int j = 0; j < n && !blocked; j++)
                {
                    if (j == ip || j == i || j == inx) continue;
                    var p = at(ring[j]);
                    if (Same(p, a, tol) || Same(p, b, tol) || Same(p, c, tol)) continue;
                    if (InTriangle(a, b, c, p)) blocked = true;
                }
                if (blocked) continue;
                o.Add((ring[ip], ring[i], ring[inx]));
                ring.RemoveAt(i);
                clipped = true;
                break;
            }
            if (clipped) continue;

            // No ear: self-intersecting or degenerate. Force-clip the most convex
            // corner so we still emit geometry instead of hanging.
            int best = -1;
            double bestCross = double.NegativeInfinity;
            for (int i = 0; i < ring.Count; i++)
            {
                int n = ring.Count;
                var x = Cross2(at(ring[(i - 1 + n) % n]), at(ring[i]), at(ring[(i + 1) % n]));
                if (x > bestCross) { bestCross = x; best = i; }
            }
            if (best < 0) break;
            {
                int n = ring.Count;
                o.Add((ring[(best - 1 + n) % n], ring[best], ring[(best + 1) % n]));
                ring.RemoveAt(best);
            }
        }
        if (ring.Count == 3) o.Add((ring[0], ring[1], ring[2]));
        var kept = new List<(int, int, int)>(o.Count);
        foreach (var t in o)
            if (Math.Abs(Cross2(at(t.Item1), at(t.Item2), at(t.Item3))) > tol) kept.Add(t);
        return kept;
    }

    /// <summary>
    /// Whether segments ab and cd cross at a point inside both, or run along each
    /// other (collinear, overlapping); touching at an end does not count.
    /// </summary>
    static bool Crosses(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        double d1 = Cross2(c, d, a), d2 = Cross2(c, d, b), d3 = Cross2(a, b, c), d4 = Cross2(a, b, d);
        const double e = 1e-12;
        if (Math.Abs(d1) <= e && Math.Abs(d2) <= e)
        {
            var u = b - a; var l2 = Vec2.Dot(u, u);
            if (l2 < e) return false;
            double t0 = Vec2.Dot(c - a, u) / l2, t1 = Vec2.Dot(d - a, u) / l2;
            return Math.Min(1, Math.Max(t0, t1)) - Math.Max(0, Math.Min(t0, t1)) > 1e-9;
        }
        return ((d1 > e && d2 < -e) || (d1 < -e && d2 > e)) && ((d3 > e && d4 < -e) || (d3 < -e && d4 > e));
    }

    /// <summary>
    /// Splice a hole into the outer ring through a bridge from the hole's leftmost
    /// vertex to the NEAREST ring vertex it can see: the bridge crosses no edge of
    /// the ring or of the holes still waiting, and runs inside the face. (A ray
    /// cast picks badly where edges are vertical or holes line up.)
    /// </summary>
    static List<int>? BridgeHole(List<int> outer, List<int> hole, List<List<int>> waiting, Flat flat)
    {
        Vec2 at(int id) => flat.At(id);
        int hi = 0;
        for (int i = 1; i < hole.Count; i++)
        {
            Vec2 p = at(hole[i]), q = at(hole[hi]);
            if (p.X < q.X || (p.X == q.X && p.Y < q.Y)) hi = i;
        }
        var m = at(hole[hi]);
        var ringPoly = flat.Of(outer);
        var waitingPolys = new List<Vec2[]>(waiting.Count);
        foreach (var w in waiting) waitingPolys.Add(flat.Of(w));
        var from = new List<Vec2>();
        var to = new List<Vec2>();
        void Edges(List<int> loop)
        {
            for (int i = 0; i < loop.Count; i++) { from.Add(at(loop[i])); to.Add(at(loop[(i + 1) % loop.Count])); }
        }
        Edges(outer);
        Edges(hole);
        foreach (var w in waiting) Edges(w);
        bool Visible(Vec2 p)
        {
            for (int e = 0; e < from.Count; e++)
                if (Crosses(m, p, from[e], to[e])) return false;
            var mid = (m + p) * 0.5;
            if (!Polygon2.Contains(ringPoly, mid)) return false;
            foreach (var w in waitingPolys)
                if (Polygon2.Contains(w, mid)) return false;
            return true;
        }
        // A vertex can occur twice in the ring (where an earlier bridge left from it): the bridge must leave from the
        // occurrence whose corner it runs into — inside the angle between its two edges (the ring is CCW).
        bool LocallyInside(int i)
        {
            int n = outer.Count;
            Vec2 a = at(outer[(i - 1 + n) % n]), p = at(outer[i]), b = at(outer[(i + 1) % n]);
            return Cross2(a, p, b) >= 0 ? Cross2(a, p, m) >= 0 && Cross2(p, b, m) >= 0 : Cross2(a, p, m) >= 0 || Cross2(p, b, m) >= 0;
        }
        var dist = new double[outer.Count];
        for (int i = 0; i < dist.Length; i++) dist[i] = (at(outer[i]) - m).Length;
        int bestPos = -1;
        foreach (var i in OrderBy(dist))
            if (dist[i] > 1e-12 && LocallyInside(i) && Visible(at(outer[i]))) { bestPos = i; break; }
        if (bestPos < 0) return null;

        var rotated = new List<int>(hole.Count);
        for (int i = 0; i < hole.Count; i++) rotated.Add(hole[(hi + i) % hole.Count]);
        var merged = new List<int>(outer.Count + hole.Count + 2);
        for (int i = 0; i <= bestPos; i++) merged.Add(outer[i]);
        merged.AddRange(rotated);
        merged.Add(rotated[0]);
        merged.Add(outer[bestPos]);
        for (int i = bestPos + 1; i < outer.Count; i++) merged.Add(outer[i]);
        return merged;
    }

    static List<int> Wound(int[] loop, bool reverse)
    {
        var o = new List<int>(loop);
        if (reverse) o.Reverse();
        return o;
    }

    /// <summary>Triangulate one planar face; triangles are vertex ids, CCW around <paramref name="normal"/>.</summary>
    public static List<(int A, int B, int C)> Face(IReadOnlyList<Vec3> points, int[] outer, int[][]? holes, Vec3 normal, double tol = 1e-14)
    {
        if (outer.Length < 3) return [];
        var (u, v) = VecMath.PlaneBasis(normal);
        var flat = new Flat(points, u, v);

        var ring = Wound(outer, Polygon2.SignedArea(flat.Of(outer)) < 0);
        // Holes wound clockwise, the leftmost first (as earcut); each bridge sees the ring and the holes still waiting.
        var wound = new List<List<int>>();
        var left = new List<double>();
        foreach (var h in holes ?? [])
        {
            if (h.Length < 3) continue;
            var pts = flat.Of(h);
            wound.Add(Wound(h, Polygon2.SignedArea(pts) > 0));
            var min = pts[0].X;
            for (int i = 1; i < pts.Length; i++) min = Math.Min(min, pts[i].X);
            left.Add(min);
        }
        var queue = new List<List<int>>(wound.Count);
        foreach (var i in OrderBy([.. left])) queue.Add(wound[i]);
        for (int k = 0; k < queue.Count; k++)
        {
            var merged = BridgeHole(ring, queue[k], queue.GetRange(k + 1, queue.Count - k - 1), flat);
            if (merged != null) ring = merged;
        }
        return EarClip(ring, flat, tol);
    }
}
