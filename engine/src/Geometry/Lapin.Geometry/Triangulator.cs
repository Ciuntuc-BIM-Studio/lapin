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
    /// Ear-clip a simple CCW ring. Ids may repeat (bridging duplicates two
    /// vertices), so every test is positional rather than by identity.
    /// </summary>
    static List<(int, int, int)> EarClip(List<int> poly, Func<int, Vec2> at, double tol)
    {
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
        return o.Where(t => Math.Abs(Cross2(at(t.Item1), at(t.Item2), at(t.Item3))) > tol).ToList();
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
    static List<int>? BridgeHole(List<int> outer, List<int> hole, IReadOnlyList<List<int>> waiting, Func<int, Vec2> at)
    {
        int hi = 0;
        for (int i = 1; i < hole.Count; i++)
        {
            Vec2 p = at(hole[i]), q = at(hole[hi]);
            if (p.X < q.X || (p.X == q.X && p.Y < q.Y)) hi = i;
        }
        var m = at(hole[hi]);
        var ringPoly = outer.Select(at).ToList();
        var edges = new List<(Vec2, Vec2)>();
        foreach (var loop in new[] { outer, hole }.Concat(waiting))
            for (int i = 0; i < loop.Count; i++) edges.Add((at(loop[i]), at(loop[(i + 1) % loop.Count])));
        bool Visible(Vec2 p)
        {
            if (edges.Any(e => Crosses(m, p, e.Item1, e.Item2))) return false;
            var mid = (m + p) * 0.5;
            return Polygon2.Contains(ringPoly, mid) && !waiting.Any(w => Polygon2.Contains(w.Select(at).ToList(), mid));
        }
        // A vertex can occur twice in the ring (where an earlier bridge left from it): the bridge must leave from the
        // occurrence whose corner it runs into — inside the angle between its two edges (the ring is CCW).
        bool LocallyInside(int i)
        {
            int n = outer.Count;
            Vec2 a = at(outer[(i - 1 + n) % n]), p = at(outer[i]), b = at(outer[(i + 1) % n]);
            return Cross2(a, p, b) >= 0 ? Cross2(a, p, m) >= 0 && Cross2(p, b, m) >= 0 : Cross2(a, p, m) >= 0 || Cross2(p, b, m) >= 0;
        }
        var best = Enumerable.Range(0, outer.Count)
            .OrderBy(i => (at(outer[i]) - m).Length)
            .Cast<int?>()
            .FirstOrDefault(i => (at(outer[i!.Value]) - m).Length > 1e-12 && LocallyInside(i.Value) && Visible(at(outer[i.Value])));
        if (best is not { } bestPos) return null;

        var rotated = hole.Skip(hi).Concat(hole.Take(hi)).ToList();
        var merged = new List<int>();
        merged.AddRange(outer.Take(bestPos + 1));
        merged.AddRange(rotated);
        merged.Add(rotated[0]);
        merged.Add(outer[bestPos]);
        merged.AddRange(outer.Skip(bestPos + 1));
        return merged;
    }

    /// <summary>Triangulate one planar face; triangles are vertex ids, CCW around <paramref name="normal"/>.</summary>
    public static List<(int A, int B, int C)> Face(IReadOnlyList<Vec3> points, int[] outer, int[][]? holes, Vec3 normal, double tol = 1e-14)
    {
        if (outer.Length < 3) return [];
        var (u, v) = VecMath.PlaneBasis(normal);
        var flat = new Dictionary<int, Vec2>();
        Vec2 At(int id)
        {
            if (!flat.TryGetValue(id, out var p)) { p = VecMath.Project(points[id], u, v); flat[id] = p; }
            return p;
        }

        var ring = Polygon2.SignedArea(outer.Select(At).ToArray()) < 0 ? outer.Reverse().ToList() : outer.ToList();
        // Holes wound clockwise, the leftmost first (as earcut); each bridge sees the ring and the holes still waiting.
        var queue = (holes ?? []).Where(h => h.Length >= 3)
            .Select(h => Polygon2.SignedArea(h.Select(At).ToArray()) > 0 ? h.Reverse().ToList() : h.ToList())
            .OrderBy(h => h.Min(i => At(i).X)).ToList();
        for (int k = 0; k < queue.Count; k++)
        {
            var merged = BridgeHole(ring, queue[k], queue.Skip(k + 1).ToList(), At);
            if (merged != null) ring = merged;
        }
        return EarClip(ring, At, tol);
    }
}
