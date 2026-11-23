namespace Lapin.Geometry;

/// <summary>
/// A region in plan cut into triangles that carry values at their corners, linear over each triangle: what a shaped top is
/// built over (a floor's falls, docs/plan-floor-slopes.md). It starts from an outline with holes (<see cref="Triangulator.Face"/>),
/// takes points (each in the triangle it lies in, the edges round it turned until they meet the empty-circle rule again, never
/// an outline's edge), takes straight segments (every triangle the segment passes through split along it), and is split where a
/// function linear on each triangle changes sign (a crease where two planes meet, where a fall reaches its limit). Every split
/// point is shared by the triangles on both sides of the edge it lies on, so the triangles always meet edge to edge, and each
/// value is carried to it linearly along that edge. Triangles run counter-clockwise seen from above.
/// </summary>
public sealed class PlanMesh
{
    public readonly List<Vec2> Points = [];
    /// <summary>The values each point carries (as many per point as the mesh was made with).</summary>
    public readonly List<double[]> Values = [];
    public readonly List<(int A, int B, int C)> Triangles = [];
    readonly int _channels;
    // Edges no flip may turn: the outline's and the segments taken.
    readonly HashSet<(int, int)> _fixed = [];

    static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

    public PlanMesh(int channels) => _channels = Math.Max(1, channels);

    /// <summary>
    /// The mesh of an outline (counter-clockwise or not) with holes: its corners and the holes' as points, triangulated by ear
    /// clipping; null when it has no area.
    /// </summary>
    public static PlanMesh? Of(IReadOnlyList<Vec2> outline, IReadOnlyList<IReadOnlyList<Vec2>>? holes, int channels = 1)
    {
        var pts = new List<Vec3>();
        var outer = new int[outline.Count];
        for (var i = 0; i < outline.Count; i++) { outer[i] = pts.Count; pts.Add(outline[i].At(0)); }
        var hs = new List<int[]>();
        foreach (var h in holes ?? [])
        {
            if (h.Count < 3) continue;
            var ids = new int[h.Count];
            for (var i = 0; i < h.Count; i++) { ids[i] = pts.Count; pts.Add(h[i].At(0)); }
            hs.Add(ids);
        }
        var tris = Triangulator.Face(pts, outer, hs.Count > 0 ? [.. hs] : null, Vec3.UnitZ);
        if (tris.Count == 0) return null;
        var m = new PlanMesh(channels);
        foreach (var p in pts) m.AddPoint(new Vec2(p.X, p.Y));
        foreach (var (a, b, c) in tris)
        {
            var t = Orient(m.Points[a], m.Points[b], m.Points[c]) >= 0 ? (a, b, c) : (a, c, b);
            if (Math.Abs(Orient(m.Points[a], m.Points[b], m.Points[c])) < 1e-18) continue;
            m.Triangles.Add(t);
        }
        foreach (var (a, b) in m.BoundaryEdges()) m._fixed.Add(Key(a, b));
        return m;
    }

    int AddPoint(Vec2 p, double[]? values = null)
    {
        Points.Add(p);
        Values.Add(values ?? new double[_channels]);
        return Points.Count - 1;
    }

    static double Orient(Vec2 a, Vec2 b, Vec2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    /// <summary>The plan area the triangles cover.</summary>
    public double Area => Triangles.Sum(t => Orient(Points[t.A], Points[t.B], Points[t.C]) / 2);

    /// <summary>The edges only one triangle has, each the way its triangle runs (counter-clockwise round the region, clockwise round a hole).</summary>
    public List<(int A, int B)> BoundaryEdges()
    {
        var all = new HashSet<(int, int)>();
        foreach (var (a, b, c) in Triangles) { all.Add((a, b)); all.Add((b, c)); all.Add((c, a)); }
        return [.. all.Where(e => !all.Contains((e.Item2, e.Item1)))];
    }

    /// <summary>The triangle a point lies in (on its edge counts), and its barycentric weights; null outside.</summary>
    public (int Triangle, double U, double V, double W)? Locate(Vec2 p, double tol = 1e-9)
    {
        (int, double, double, double)? best = null;
        var bestMin = double.NegativeInfinity;
        for (var t = 0; t < Triangles.Count; t++)
        {
            var (a, b, c) = Triangles[t];
            var (pa, pb, pc) = (Points[a], Points[b], Points[c]);
            var area = Orient(pa, pb, pc);
            if (area <= 0) continue;
            var u = Orient(pb, pc, p) / area;
            var v = Orient(pc, pa, p) / area;
            var w = 1 - u - v;
            var min = Math.Min(u, Math.Min(v, w));
            if (min > bestMin) { bestMin = min; best = (t, u, v, w); }
        }
        // Scaled by the triangle's size: a point a hair outside an outline's edge is still on it.
        return best is { } r && bestMin >= -tol * 1e3 ? r : null;
    }

    /// <summary>The values at a point in plan, linear over the triangle it lies in; null outside the mesh.</summary>
    public double[]? ValuesAt(Vec2 p)
    {
        if (Locate(p) is not var (t, u, v, w)) return null;
        var (a, b, c) = Triangles[t];
        var o = new double[_channels];
        for (var k = 0; k < _channels; k++) o[k] = u * Values[a][k] + v * Values[b][k] + w * Values[c][k];
        return o;
    }

    /// <summary>
    /// A point put in: an existing point within <paramref name="snap"/> is that point; on an edge, both triangles there are
    /// split; inside one, it is split in three; then the edges round it are turned (Lawson) where the circle rule asks, never a
    /// fixed edge. Its values are the mesh's there unless given. The point's index, or -1 when it lies outside.
    /// </summary>
    public int Insert(Vec2 p, double[]? values = null, double snap = 1e-6)
    {
        for (var i = 0; i < Points.Count; i++)
            if ((Points[i] - p).Length <= snap)
            {
                if (values != null) Values[i] = (double[])values.Clone();
                return i;
            }
        if (Locate(p) is not var (t, u, v, w)) return -1;
        var vals = values != null ? (double[])values.Clone() : ValuesAt(p)!;
        var (a, b, c) = Triangles[t];
        var k = AddPoint(p, vals);
        var stack = new Stack<(int, int)>();
        // On an edge (its weight there next to nothing): the two triangles on it split.
        var edgeTol = 1e-9;
        (int, int)? onEdge = u < edgeTol ? (b, c) : v < edgeTol ? (c, a) : w < edgeTol ? (a, b) : null;
        if (onEdge is var (e0, e1))
        {
            var fixedEdge = _fixed.Remove(Key(e0, e1));
            var third = Triangles[t] switch { var x when x.A != e0 && x.A != e1 => x.A, var x when x.B != e0 && x.B != e1 => x.B, var x => x.C };
            Triangles[t] = (e0, k, third);
            Triangles.Add((k, e1, third));
            stack.Push((e1, third)); stack.Push((third, e0));
            var other = Triangles.FindIndex(x => Has(x, e1, e0));
            if (other >= 0)
            {
                var far = Third(Triangles[other], e0, e1);
                Triangles[other] = (e1, k, far);
                Triangles.Add((k, e0, far));
                stack.Push((e0, far)); stack.Push((far, e1));
            }
            if (fixedEdge) { _fixed.Add(Key(e0, k)); _fixed.Add(Key(k, e1)); }
        }
        else
        {
            Triangles[t] = (a, b, k);
            Triangles.Add((b, c, k));
            Triangles.Add((c, a, k));
            stack.Push((a, b)); stack.Push((b, c)); stack.Push((c, a));
        }
        Legalise(k, stack);
        return k;
    }

    static bool Has((int A, int B, int C) t, int u, int v) =>
        (t.A == u && t.B == v) || (t.B == u && t.C == v) || (t.C == u && t.A == v);

    static int Third((int A, int B, int C) t, int u, int v) => t.A != u && t.A != v ? t.A : t.B != u && t.B != v ? t.B : t.C;

    void Legalise(int k, Stack<(int, int)> stack)
    {
        var guard = 0;
        while (stack.Count > 0 && guard++ < 100_000)
        {
            var (u, v) = stack.Pop();
            if (_fixed.Contains(Key(u, v))) continue;
            // The triangle with k on this side (u→v) and the one across (v→u).
            var near = Triangles.FindIndex(x => Has(x, u, v) && Third(x, u, v) == k);
            var across = Triangles.FindIndex(x => Has(x, v, u));
            if (near < 0 || across < 0) continue;
            var far = Third(Triangles[across], u, v);
            if (!InCircle(Points[u], Points[v], Points[k], Points[far])) continue;
            // Turned only when the four make a convex quad.
            if (!(Orient(Points[k], Points[far], Points[u]) * Orient(Points[k], Points[far], Points[v]) < 0)) continue;
            Triangles[near] = (u, far, k);
            Triangles[across] = (far, v, k);
            stack.Push((u, far)); stack.Push((far, v));
        }
    }

    static bool InCircle(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        double ax = a.X - d.X, ay = a.Y - d.Y, bx = b.X - d.X, by = b.Y - d.Y, cx = c.X - d.X, cy = c.Y - d.Y;
        return (ax * ax + ay * ay) * (bx * cy - cx * by) - (bx * bx + by * by) * (ax * cy - cx * ay) + (cx * cx + cy * cy) * (ax * by - bx * ay) > 1e-12;
    }

    /// <summary>
    /// A straight segment taken into the mesh: its ends put in, then every triangle it passes through split along it, so it
    /// runs along edges from end to end (fixed from then on). False when an end lies outside.
    /// </summary>
    public bool InsertSegment(Vec2 a, Vec2 b)
    {
        var ia = Insert(a);
        var ib = Insert(b);
        if (ia < 0 || ib < 0) return false;
        var (pa, pb) = (Points[ia], Points[ib]);
        var d = pb - pa;
        var len2 = Vec2.Dot(d, d);
        if (len2 < 1e-18) return true;
        // Only the triangles the open segment passes through.
        bool Crossed(int t)
        {
            var (x, y, z) = Triangles[t];
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            foreach (var (i, j) in new[] { (x, y), (y, z), (z, x) })
            {
                var (fi, fj) = (Orient(pa, pb, Points[i]), Orient(pa, pb, Points[j]));
                void Take(Vec2 q) { var s = Vec2.Dot(q - pa, d) / len2; lo = Math.Min(lo, s); hi = Math.Max(hi, s); }
                if (Math.Abs(fi) <= 1e-12 * len2) Take(Points[i]);
                if ((fi > 1e-12 * len2 && fj < -1e-12 * len2) || (fi < -1e-12 * len2 && fj > 1e-12 * len2)) Take(Points[i] + (Points[j] - Points[i]) * (fi / (fi - fj)));
            }
            return hi - lo > 1e-9 && hi > 1e-9 && lo < 1 - 1e-9;
        }
        Split(i => Orient(pa, pb, Points[i]) / Math.Sqrt(len2), Crossed);
        // The segment's pieces: the edges along it.
        foreach (var (x, y, z) in Triangles)
            foreach (var (i, j) in new[] { (x, y), (y, z), (z, x) })
                if (OnSegment(Points[i], pa, pb) && OnSegment(Points[j], pa, pb)) _fixed.Add(Key(i, j));
        return true;
    }

    static bool OnSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var d = b - a;
        var len = d.Length;
        if (len < 1e-12) return (p - a).Length < 1e-9;
        if (Math.Abs(Vec2.Cross(d, p - a)) / len > 1e-7) return false;
        var s = Vec2.Dot(p - a, d) / (len * len);
        return s > -1e-9 && s < 1 + 1e-9;
    }

    /// <summary>
    /// The triangles (those <paramref name="which"/> takes; every one by default) split where <paramref name="f"/>, a value at
    /// each point linear over each triangle, crosses zero: a point on each edge it crosses (one per edge, shared by both
    /// triangles there), the values carried along the edge. Points at zero stay as they are. How many points were added.
    /// </summary>
    public int Split(Func<int, double> f, Func<int, bool>? which = null, double tol = 1e-12)
    {
        var fv = new List<double>(Points.Count);
        for (var i = 0; i < Points.Count; i++) fv.Add(f(i));
        int Sign(int i) => fv[i] > tol ? 1 : fv[i] < -tol ? -1 : 0;
        var cut = new Dictionary<(int, int), int>();
        var added = 0;
        int Cut(int i, int j)
        {
            var key = Key(i, j);
            if (cut.TryGetValue(key, out var k)) return k;
            var s = fv[i] / (fv[i] - fv[j]);
            var vals = new double[_channels];
            for (var c = 0; c < _channels; c++) vals[c] = Values[i][c] + (Values[j][c] - Values[i][c]) * s;
            k = AddPoint(Points[i] + (Points[j] - Points[i]) * s, vals);
            fv.Add(0);
            cut[key] = k;
            added++;
            if (_fixed.Remove(key)) { _fixed.Add(Key(i, k)); _fixed.Add(Key(k, j)); }
            return k;
        }
        // Which edges are cut, first: a triangle not taken but sharing a cut edge is split there too, so the mesh stays conforming.
        var taken = new HashSet<int>();
        for (var t = 0; t < Triangles.Count; t++)
        {
            if (which != null && !which(t)) continue;
            var (a, b, c) = Triangles[t];
            if (Sign(a) * Sign(b) < 0 || Sign(b) * Sign(c) < 0 || Sign(c) * Sign(a) < 0) taken.Add(t);
        }
        if (taken.Count == 0) return 0;
        foreach (var t in taken)
        {
            var (a, b, c) = Triangles[t];
            foreach (var (i, j) in new[] { (a, b), (b, c), (c, a) }) if (Sign(i) * Sign(j) < 0) Cut(i, j);
        }
        var count = Triangles.Count;
        for (var t = 0; t < count; t++)
        {
            var (a, b, c) = Triangles[t];
            // The cuts on its edges (made for it or for its neighbour).
            int? On(int i, int j) => cut.TryGetValue(Key(i, j), out var k) ? k : null;
            var (ab, bc, ca) = (On(a, b), On(b, c), On(c, a));
            var n = (ab != null ? 1 : 0) + (bc != null ? 1 : 0) + (ca != null ? 1 : 0);
            if (n == 0) continue;
            // Rotated so the cuts come first on a–b (and b–c).
            (int, int, int, int?, int?, int?) R(int x, int y, int z, int? xy, int? yz, int? zx) => (x, y, z, xy, yz, zx);
            var r = R(a, b, c, ab, bc, ca);
            for (var turn = 0; turn < 3 && !(r.Item4 != null && (n == 1 || r.Item5 != null)); turn++)
                r = R(r.Item2, r.Item3, r.Item1, r.Item5, r.Item6, r.Item4);
            var (x0, y0, z0, m0, m1, _) = r;
            if (n == 1)
            {
                // One cut on x–y: two triangles from it to z.
                Triangles[t] = (x0, m0!.Value, z0);
                Triangles.Add((m0.Value, y0, z0));
            }
            else if (n == 2)
            {
                // Cuts on x–y and y–z: the corner y cut off, the quad left in two.
                Triangles[t] = (m0!.Value, y0, m1!.Value);
                Triangles.Add((x0, m0.Value, m1.Value));
                Triangles.Add((x0, m1.Value, z0));
            }
            else
            {
                // Three (a neighbour's cut on each edge): four triangles.
                var m2 = r.Item6!.Value;
                Triangles[t] = (x0, m0!.Value, m2);
                Triangles.Add((m0.Value, y0, m1!.Value));
                Triangles.Add((m1.Value, z0, m2));
                Triangles.Add((m0.Value, m1.Value, m2));
            }
        }
        return added;
    }

    /// <summary>A copy of the triangles <paramref name="which"/> takes, their points numbered afresh.</summary>
    public PlanMesh Sub(Func<int, bool> which)
    {
        var o = new PlanMesh(_channels);
        var map = new Dictionary<int, int>();
        int Map(int i)
        {
            if (!map.TryGetValue(i, out var k)) { k = o.AddPoint(Points[i], (double[])Values[i].Clone()); map[i] = k; }
            return k;
        }
        for (var t = 0; t < Triangles.Count; t++)
            if (which(t)) { var (a, b, c) = Triangles[t]; o.Triangles.Add((Map(a), Map(b), Map(c))); }
        foreach (var (a, b) in o.BoundaryEdges()) o._fixed.Add(Key(a, b));
        return o;
    }

    /// <summary>
    /// A closed solid over the mesh between two heights at each point (<paramref name="bottom"/> below <paramref name="top"/>):
    /// its top and bottom the mesh's triangles, plumb sides along its boundary. Null when it has no thickness anywhere.
    /// </summary>
    public Solid? Body(Func<int, double> bottom, Func<int, double> top, string tag)
    {
        var n = Points.Count;
        if (Triangles.Count == 0) return null;
        var any = false;
        for (var i = 0; i < n && !any; i++) any = top(i) - bottom(i) > 1e-9;
        if (!any) return null;
        var verts = new List<Vec3>(n * 2);
        for (var i = 0; i < n; i++) verts.Add(Points[i].At(bottom(i)));
        for (var i = 0; i < n; i++) verts.Add(Points[i].At(top(i)));
        var tris = new List<(int, int, int)>();
        foreach (var (a, b, c) in Triangles) { tris.Add((n + a, n + b, n + c)); tris.Add((a, c, b)); }
        foreach (var (a, b) in BoundaryEdges())
        {
            tris.Add((a, b, n + b));
            tris.Add((a, n + b, n + a));
        }
        if (SolidFromMesh.Build(verts, tris, tag) is not { } s) return null;
        return Measure.SignedVolume(s) < 0 ? SolidBuilder.Flip(s) : s;
    }
}
