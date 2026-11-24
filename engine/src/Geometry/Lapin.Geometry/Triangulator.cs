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
    /// A point's place on a z-order (Morton) curve over the ring's bounds, 15 bits a coordinate: points in a box have their
    /// places between those of its lower left and upper right corners, so the points that could stand in an ear are found by
    /// walking the curve from the ear's corner both ways, between the places of its bounds' corners.
    /// </summary>
    static int ZOrder(double x, double y, double minX, double minY, double scale)
    {
        static int Spread(double t)
        {
            var v = (int)Math.Clamp(t, 0, 32767);
            v = (v | (v << 8)) & 0x00FF00FF;
            v = (v | (v << 4)) & 0x0F0F0F0F;
            v = (v | (v << 2)) & 0x33333333;
            v = (v | (v << 1)) & 0x55555555;
            return v;
        }
        return Spread((x - minX) * scale) | (Spread((y - minY) * scale) << 1);
    }

    /// <summary>
    /// Ear-clip a simple CCW ring. Ids may repeat (bridging duplicates two
    /// vertices), so every test is positional rather than by identity.
    /// <para>
    /// The ring is a doubly linked list of its corners, so a corner cut away costs nothing, and the search for the next ear goes on
    /// from where the last was cut (two corners on, which leaves fewer slivers). Only a reflex (or flat) corner can stand inside an
    /// ear of a simple ring, so only those are tested against it; above 80 corners they are found through a z-order index of the
    /// corners left (a second linked list, sorted by their places on the curve), only those within the ear's bounds, as the
    /// published earcut algorithm does (Mapbox, after Eberly's ear clipping). A face of 50 000 vertices took a minute or more,
    /// each pass looking at every corner (audit 15.11.2026); it takes a fraction of a second now.
    /// </para>
    /// </summary>
    static List<(int, int, int)> EarClip(Loop ring, double tol)
    {
        int n = ring.Ids.Count;
        var o = new List<(int, int, int)>(Math.Max(0, n - 2));
        if (n < 3) return o;
        // Each triangle by its corners' places in the ring, then by their ids once the flat ones are left out.
        var corners = new List<(int, int, int)>(Math.Max(0, n - 2));
        var id = ring.Ids.ToArray();
        var pos = ring.Pos.ToArray();
        var prev = new int[n];
        var next = new int[n];
        for (int i = 0; i < n; i++)
        {
            prev[i] = i == 0 ? n - 1 : i - 1;
            next[i] = i == n - 1 ? 0 : i + 1;
        }

        // The z-order index: each corner's place, and the corners left linked in the order of their places.
        bool hashed = n > 80;
        int[] z = [], prevZ = [], nextZ = [];
        double minX = 0, minY = 0, scale = 0;
        if (hashed)
        {
            double maxX = pos[0].X, maxY = pos[0].Y;
            minX = maxX; minY = maxY;
            for (int i = 1; i < n; i++)
            {
                var p = pos[i];
                if (p.X < minX) minX = p.X; else if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y; else if (p.Y > maxY) maxY = p.Y;
            }
            var size = Math.Max(maxX - minX, maxY - minY);
            scale = size > 0 ? 32767 / size : 0;
            z = new int[n];
            var order = new int[n];
            var keys = new int[n];
            for (int i = 0; i < n; i++) { z[i] = ZOrder(pos[i].X, pos[i].Y, minX, minY, scale); order[i] = i; keys[i] = z[i]; }
            Array.Sort(keys, order);
            prevZ = new int[n];
            nextZ = new int[n];
            for (int k = 0; k < n; k++)
            {
                prevZ[order[k]] = k == 0 ? -1 : order[k - 1];
                nextZ[order[k]] = k == n - 1 ? -1 : order[k + 1];
            }
        }

        void Remove(int k)
        {
            next[prev[k]] = next[k];
            prev[next[k]] = prev[k];
            if (!hashed) return;
            if (prevZ[k] >= 0) nextZ[prevZ[k]] = nextZ[k];
            if (nextZ[k] >= 0) prevZ[nextZ[k]] = prevZ[k];
        }

        double Corner(int k) => Cross2(pos[prev[k]], pos[k], pos[next[k]]);
        var relaxed = false;

        // Whether corner p, reflex or flat, stands in the ear a b c (its own corners and their twins left out).
        bool Blocks(int p, int ia, int ib, int ic, double x0, double x1, double y0, double y1)
        {
            if (p == ia || p == ib || p == ic) return false;
            var q = pos[p];
            if (q.X < x0 || q.X > x1 || q.Y < y0 || q.Y > y1) return false;
            Vec2 a = pos[ia], b = pos[ib], c = pos[ic];
            if (Same(q, a, tol) || Same(q, b, tol) || Same(q, c, tol)) return false;
            // Flat within the ear's own scale: a corner in a line with its neighbours, rounded a hair convex once projected, still
            // blocks (else the ear's edge passes by it, a T-junction).
            var span = x1 - x0 + (y1 - y0);
            var eps = 1e-12 * span * span;
            if (InTriangle(a, b, c, q)) return Corner(p) <= Math.Max(tol, eps);
            // A flat corner on the ear's new edge c–a, a hair outside it once projected, blocks it too (else a T-junction); a reflex
            // one there (a face of coplanar triangles whose loop turns back at a point on the edge, as a terrain's on a regular grid
            // of points) while an ear clear of it is still to be found: round the ring without one, it no longer blocks.
            return (relaxed ? Math.Abs(Corner(p)) : Corner(p)) <= eps && Math.Abs(Cross2(c, a, q)) <= eps && Vec2.Dot(q - c, a - c) > 0 && Vec2.Dot(q - a, c - a) > 0;
        }

        bool IsEar(int ib)
        {
            int ia = prev[ib], ic = next[ib];
            Vec2 a = pos[ia], b = pos[ib], c = pos[ic];
            if (Cross2(a, b, c) <= tol) return false;
            double x0 = Math.Min(a.X, Math.Min(b.X, c.X)), x1 = Math.Max(a.X, Math.Max(b.X, c.X));
            double y0 = Math.Min(a.Y, Math.Min(b.Y, c.Y)), y1 = Math.Max(a.Y, Math.Max(b.Y, c.Y));
            // The bounds a hair wider: a corner on the ear's edge, rounded just outside them once projected, is still looked at.
            var pad = 1e-9 * (x1 - x0 + (y1 - y0));
            x0 -= pad; x1 += pad; y0 -= pad; y1 += pad;
            if (!hashed)
            {
                for (int p = next[ic]; p != ia; p = next[p])
                    if (Blocks(p, ia, ib, ic, x0, x1, y0, y1)) return false;
                return true;
            }
            // Within the ear's bounds (widened by the tolerance, as the bounds test is): between its corners' places.
            int lo = ZOrder(x0 - tol, y0 - tol, minX, minY, scale), hi = ZOrder(x1 + tol, y1 + tol, minX, minY, scale);
            for (int p = nextZ[ib]; p >= 0 && z[p] <= hi; p = nextZ[p])
                if (Blocks(p, ia, ib, ic, x0, x1, y0, y1)) return false;
            for (int p = prevZ[ib]; p >= 0 && z[p] >= lo; p = prevZ[p])
                if (Blocks(p, ia, ib, ic, x0, x1, y0, y1)) return false;
            return true;
        }

        int count = n, ear = 0, stop = 0;
        while (count > 3)
        {
            if (IsEar(ear))
            {
                int after = next[ear];
                corners.Add((prev[ear], ear, after));
                Remove(ear);
                count--;
                ear = stop = next[after];
                relaxed = false;
                continue;
            }
            ear = next[ear];
            if (ear != stop) continue;
            // Round the ring without an ear clear of every reflex corner on its new edge: once more, letting those pass.
            if (!relaxed) { relaxed = true; continue; }
            relaxed = false;
            // Round the ring without an ear: self-intersecting or degenerate. Force-clip the most convex
            // corner so we still emit geometry instead of hanging.
            int best = ear;
            double bestCross = double.NegativeInfinity;
            int k = ear;
            do
            {
                var x = Corner(k);
                if (x > bestCross) { bestCross = x; best = k; }
                k = next[k];
            } while (k != ear);
            int nb = next[best];
            corners.Add((prev[best], best, nb));
            Remove(best);
            count--;
            ear = stop = nb;
        }
        if (count == 3) corners.Add((prev[ear], ear, next[ear]));
        foreach (var (a, b, c) in corners)
            if (Math.Abs(Cross2(pos[a], pos[b], pos[c])) > tol) o.Add((id[a], id[b], id[c]));
        return o;
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

    /// <summary>A ring: its vertex ids and, beside them, their points in the face's plane (read without a lookup).</summary>
    sealed record Loop(List<int> Ids, List<Vec2> Pos);

    /// <summary>
    /// Splice a hole into the outer ring through a bridge from the hole's leftmost
    /// vertex to the NEAREST ring vertex it can see: the bridge crosses no edge of
    /// the ring or of the holes still waiting, and runs inside the face. (A ray
    /// cast picks badly where edges are vertical or holes line up.) Every edge is
    /// passed over by its bounds first, so a face of thousands of vertices and many
    /// holes bridges in linear time a hole.
    /// </summary>
    static Loop? BridgeHole(Loop outer, Loop hole, List<Loop> waiting)
    {
        var hp = hole.Pos;
        int hi = 0;
        for (int i = 1; i < hp.Count; i++)
            if (hp[i].X < hp[hi].X || (hp[i].X == hp[hi].X && hp[i].Y < hp[hi].Y)) hi = i;
        var m = hp[hi];
        var ringPoly = outer.Pos.ToArray();
        int edges = ringPoly.Length + hp.Count;
        foreach (var w in waiting) edges += w.Pos.Count;
        var from = new Vec2[edges];
        var to = new Vec2[edges];
        int k = 0;
        void Edges(List<Vec2> loop)
        {
            for (int i = 0; i < loop.Count; i++, k++) { from[k] = loop[i]; to[k] = loop[i + 1 == loop.Count ? 0 : i + 1]; }
        }
        Edges(outer.Pos);
        Edges(hp);
        foreach (var w in waiting) Edges(w.Pos);
        var ex0 = new double[edges]; var ex1 = new double[edges]; var ey0 = new double[edges]; var ey1 = new double[edges];
        for (int e = 0; e < edges; e++)
        {
            Vec2 f = from[e], t = to[e];
            ex0[e] = Math.Min(f.X, t.X); ex1[e] = Math.Max(f.X, t.X); ey0[e] = Math.Min(f.Y, t.Y); ey1[e] = Math.Max(f.Y, t.Y);
        }
        Vec2[][]? waitingPolys = null;
        bool Visible(Vec2 p)
        {
            double x0 = Math.Min(m.X, p.X) - 1e-9, x1 = Math.Max(m.X, p.X) + 1e-9, y0 = Math.Min(m.Y, p.Y) - 1e-9, y1 = Math.Max(m.Y, p.Y) + 1e-9;
            for (int e = 0; e < edges; e++)
                if (ex1[e] >= x0 && ex0[e] <= x1 && ey1[e] >= y0 && ey0[e] <= y1 && Crosses(m, p, from[e], to[e])) return false;
            var mid = (m + p) * 0.5;
            if (!Polygon2.Contains(ringPoly, mid)) return false;
            waitingPolys ??= [.. waiting.Select(w => w.Pos.ToArray())];
            foreach (var w in waitingPolys)
                if (Polygon2.Contains(w, mid)) return false;
            return true;
        }
        // A vertex can occur twice in the ring (where an earlier bridge left from it): the bridge must leave from the
        // occurrence whose corner it runs into — inside the angle between its two edges (the ring is CCW).
        int n = ringPoly.Length;
        bool LocallyInside(int i)
        {
            Vec2 a = ringPoly[(i - 1 + n) % n], p = ringPoly[i], b = ringPoly[(i + 1) % n];
            return Cross2(a, p, b) >= 0 ? Cross2(a, p, m) >= 0 && Cross2(p, b, m) >= 0 : Cross2(a, p, m) >= 0 || Cross2(p, b, m) >= 0;
        }
        var dist = new double[n];
        double nearest = double.PositiveInfinity, farthest = 0;
        for (int i = 0; i < n; i++)
        {
            dist[i] = (ringPoly[i] - m).Length;
            if (dist[i] > 1e-12 && dist[i] < nearest) nearest = dist[i];
            if (dist[i] > farthest) farthest = dist[i];
        }
        // Nearest first, ties in ring order: the vertices within a reach sorted and tried, the reach doubled until one is
        // seen (most holes see one of the first few; sorting the whole ring for each hole cost more than all the rest).
        int bestPos = -1;
        var order = new List<int>();
        for (double done = 0, reach = nearest * 2; bestPos < 0 && done < farthest && !double.IsInfinity(nearest); done = reach, reach *= 2)
        {
            order.Clear();
            for (int i = 0; i < n; i++)
                if (dist[i] > 1e-12 && dist[i] > done && dist[i] <= reach) order.Add(i);
            order.Sort((x, y) => { var c = dist[x].CompareTo(dist[y]); return c != 0 ? c : x.CompareTo(y); });
            foreach (var i in order)
                if (LocallyInside(i) && Visible(ringPoly[i])) { bestPos = i; break; }
        }
        if (bestPos < 0) return null;

        var ids = new List<int>(n + hp.Count + 2);
        var pos = new List<Vec2>(n + hp.Count + 2);
        void Add(int id, Vec2 p) { ids.Add(id); pos.Add(p); }
        for (int i = 0; i <= bestPos; i++) Add(outer.Ids[i], ringPoly[i]);
        for (int i = 0; i < hp.Count; i++) { var j = (hi + i) % hp.Count; Add(hole.Ids[j], hp[j]); }
        Add(hole.Ids[hi], hp[hi]);
        Add(outer.Ids[bestPos], ringPoly[bestPos]);
        for (int i = bestPos + 1; i < n; i++) Add(outer.Ids[i], ringPoly[i]);
        return new Loop(ids, pos);
    }

    static Loop Wound(int[] loop, Flat flat, bool? clockwise)
    {
        var ids = new List<int>(loop);
        var pos = new List<Vec2>(flat.Of(loop));
        // The ring counter-clockwise (clockwise false), a hole clockwise (true).
        var area = Polygon2.SignedArea([.. pos]);
        if (clockwise == true ? area > 0 : area < 0) { ids.Reverse(); pos.Reverse(); }
        return new Loop(ids, pos);
    }

    /// <summary>Triangulate one planar face; triangles are vertex ids, CCW around <paramref name="normal"/>.</summary>
    public static List<(int A, int B, int C)> Face(IReadOnlyList<Vec3> points, int[] outer, int[][]? holes, Vec3 normal, double tol = 1e-14)
    {
        if (outer.Length < 3) return [];
        var (u, v) = VecMath.PlaneBasis(normal);
        var flat = new Flat(points, u, v);

        var ring = Wound(outer, flat, false);
        // Holes wound clockwise, the leftmost first (as earcut); each bridge sees the ring and the holes still waiting.
        var wound = new List<Loop>();
        var left = new List<double>();
        foreach (var h in holes ?? [])
        {
            if (h.Length < 3) continue;
            var w = Wound(h, flat, true);
            wound.Add(w);
            var min = w.Pos[0].X;
            for (int i = 1; i < w.Pos.Count; i++) min = Math.Min(min, w.Pos[i].X);
            left.Add(min);
        }
        var queue = new List<Loop>(wound.Count);
        foreach (var i in OrderBy([.. left])) queue.Add(wound[i]);
        for (int k = 0; k < queue.Count; k++)
        {
            var merged = BridgeHole(ring, queue[k], queue.GetRange(k + 1, queue.Count - k - 1));
            if (merged != null) ring = merged;
        }
        return EarClip(ring, tol);
    }
}
