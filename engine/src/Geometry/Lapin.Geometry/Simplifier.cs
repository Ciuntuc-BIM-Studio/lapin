namespace Lapin.Geometry;

/// <summary>
/// A triangle mesh with fewer triangles, never further from the original than a tolerance: edges collapse
/// one by one, the cheapest first by the error of quadrics (Garland and Heckbert, "Surface simplification
/// using quadric error metrics", 1997). Each vertex keeps the planes of the original triangles it stands
/// for (each plane once: the coplanar triangles of a face are one plane); a collapse is taken only where the
/// vertex it leaves is within the tolerance of every one of them, the quadric only ordering the collapses
/// (it sums a plane as often as vertices brought it, which would stop a long straight run early). An open edge (a mesh read from a
/// file has many: faces that meet without sharing their vertices) adds the plane square to its triangle
/// through it, so a vertex on it moves only along it, and only by collapsing an open edge; a vertex on an
/// edge more than two triangles share stays where it is. A collapse that would fold a triangle over, or tear
/// the surface (the edge's triangles' worth of neighbours shared, no more), is not taken. Flat areas lose their
/// inner vertices at no cost; curved surfaces keep as many facets as the tolerance asks.
/// </summary>
public static class Simplifier
{
    /// <summary>How many triangles a mesh has before its separate pieces are simplified apart, on every core.</summary>
    const int SplitFrom = 20_000;

    /// <summary>
    /// The mesh simplified; the vertices it keeps renumbered, in their order. A big mesh made of pieces that
    /// share no vertex (a manufacturer's model: its screws, fins and casings) is simplified piece by piece on
    /// every core, which gives the same as all at once (a collapse never reaches from one piece into another).
    /// </summary>
    public static (Vec3[] Vertices, List<(int A, int B, int C)> Triangles) Simplify(IReadOnlyList<Vec3> vertices, IReadOnlyList<(int A, int B, int C)> triangles, double tolerance)
    {
        if (triangles.Count < SplitFrom) return SimplifyOne(vertices, triangles, tolerance);
        var pieces = Pieces(vertices.Count, triangles);
        if (pieces.Count < 2) return SimplifyOne(vertices, triangles, tolerance);
        var done = new (Vec3[] Vertices, List<(int A, int B, int C)> Triangles)[pieces.Count];
        Parallel.For(0, pieces.Count, i =>
        {
            // A piece with its own numbering.
            var map = new Dictionary<int, int>();
            var pv = new List<Vec3>();
            var pt = new List<(int A, int B, int C)>(pieces[i].Count);
            int Of(int x) { if (!map.TryGetValue(x, out var y)) { map[x] = y = pv.Count; pv.Add(vertices[x]); } return y; }
            foreach (var k in pieces[i]) { var t = triangles[k]; pt.Add((Of(t.A), Of(t.B), Of(t.C))); }
            done[i] = SimplifyOne(pv, pt, tolerance);
        });
        var outV = new List<Vec3>();
        var outT = new List<(int A, int B, int C)>();
        foreach (var (pv, pt) in done)
        {
            var at = outV.Count;
            outV.AddRange(pv);
            foreach (var (a, b, c) in pt) outT.Add((a + at, b + at, c + at));
        }
        return ([.. outV], outT);
    }

    /// <summary>The triangles in groups that share no vertex with each other (their indices).</summary>
    static List<List<int>> Pieces(int n, IReadOnlyList<(int A, int B, int C)> triangles)
    {
        var parent = new int[n];
        for (var i = 0; i < n; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Join(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
        foreach (var (a, b, c) in triangles) { Join(a, b); Join(b, c); }
        var of = new Dictionary<int, List<int>>();
        for (var k = 0; k < triangles.Count; k++)
        {
            var r = Find(triangles[k].A);
            if (!of.TryGetValue(r, out var l)) of[r] = l = [];
            l.Add(k);
        }
        // Small pieces gathered, so a thousand screws are a handful of tasks, not a thousand.
        var o = new List<List<int>>();
        List<int>? bin = null;
        foreach (var l in of.Values)
        {
            if (l.Count >= 2000) { o.Add(l); continue; }
            if (bin == null || bin.Count >= 2000) o.Add(bin = []);
            bin.AddRange(l);
        }
        return o;
    }

    static (Vec3[] Vertices, List<(int A, int B, int C)> Triangles) SimplifyOne(IReadOnlyList<Vec3> vertices, IReadOnlyList<(int A, int B, int C)> triangles, double tolerance)
    {
        var n = vertices.Count;
        var v = new Vec3[n];
        for (var i = 0; i < n; i++) v[i] = vertices[i];
        var t = new int[triangles.Count * 3];
        for (var i = 0; i < triangles.Count; i++) { t[3 * i] = triangles[i].A; t[3 * i + 1] = triangles[i].B; t[3 * i + 2] = triangles[i].C; }
        var tc = triangles.Count;
        var deadT = new bool[tc];
        var deadV = new bool[n];
        var locked = new bool[n];
        var open = new bool[n];
        var stamp = new int[n];
        var q = new double[n * 10];
        var around = new List<int>[n];
        for (var i = 0; i < n; i++) around[i] = [];
        // The original planes, each once, and the ones each vertex stands for (sorted ids).
        var pn = new List<Vec3>();
        var pd = new List<double>();
        var planeIds = new Dictionary<long, int>(EdgeKeys.Comparer);
        var planes = new List<int>[n];
        for (var i = 0; i < n; i++) planes[i] = [];
        int Plane(Vec3 nrm, double d)
        {
            // Quantised to find the same plane again (a face's triangles); a different plane on the same key gets an id of its own.
            long Qn(double x) => (long)Math.Round(x * 1e8);
            var key = ((Qn(nrm.X) * 73856093) ^ (Qn(nrm.Y) * 19349663) ^ (Qn(nrm.Z) * 83492791)) * 31 + Qn(d);
            if (planeIds.TryGetValue(key, out var id) && Vec3.Dot(pn[id], nrm) > 1 - 1e-12 && Math.Abs(pd[id] - d) < 1e-9) return id;
            pn.Add(nrm); pd.Add(d);
            planeIds[key] = pn.Count - 1;
            return pn.Count - 1;
        }
        void Stand(int x, int plane) { var l = planes[x]; var at = l.BinarySearch(plane); if (at < 0) l.Insert(~at, plane); }

        // The planes' quadrics, and which triangles each vertex has.
        for (var k = 0; k < tc; k++)
        {
            int a = t[3 * k], b = t[3 * k + 1], c = t[3 * k + 2];
            if (a == b || b == c || a == c) { deadT[k] = true; continue; }
            var nn = Vec3.Cross(v[b] - v[a], v[c] - v[a]);
            var len = nn.Length;
            if (len < 1e-20) { deadT[k] = true; continue; }
            var p = nn * (1 / len);
            var d = -Vec3.Dot(p, v[a]);
            var plane = Plane(p, d);
            for (var j = 0; j < 3; j++)
            {
                var x = t[3 * k + j];
                AddPlane(q, x, p.X, p.Y, p.Z, d);
                Stand(x, plane);
                around[x].Add(k);
            }
        }
        // Open and shared edges lock their vertices: the outline stays as it is.
        var uses = new Dictionary<long, int>(EdgeKeys.Comparer);
        for (var k = 0; k < tc; k++)
        {
            if (deadT[k]) continue;
            for (var j = 0; j < 3; j++)
            {
                var e = EdgeKeys.Undirected(t[3 * k + j], t[3 * k + (j + 1) % 3]);
                uses[e] = uses.TryGetValue(e, out var u) ? u + 1 : 1;
            }
        }
        foreach (var (e, u) in uses)
        {
            var (a, b) = EdgeKeys.Ends(e);
            if (u > 2) locked[a] = locked[b] = true;
            else if (u == 1) open[a] = open[b] = true;
        }
        // An open edge's plane: square to its triangle, through the edge.
        for (var k = 0; k < tc; k++)
        {
            if (deadT[k]) continue;
            var nt = Vec3.Cross(v[t[3 * k + 1]] - v[t[3 * k]], v[t[3 * k + 2]] - v[t[3 * k]]).Normalized();
            for (var j = 0; j < 3; j++)
            {
                int a = t[3 * k + j], b = t[3 * k + (j + 1) % 3];
                if (uses[EdgeKeys.Undirected(a, b)] != 1) continue;
                var side = Vec3.Cross(v[b] - v[a], nt);
                if (side.Length < 1e-20) continue;
                var sn = side.Normalized();
                var sd = -Vec3.Dot(sn, v[a]);
                AddPlane(q, a, sn.X, sn.Y, sn.Z, sd);
                AddPlane(q, b, sn.X, sn.Y, sn.Z, sd);
                var sp = Plane(sn, sd);
                Stand(a, sp); Stand(b, sp);
            }
        }

        var heap = new PriorityQueue<(int A, int B, int SA, int SB, Vec3 At), double>();
        // Which end must stay: one that cannot move (on a shared edge; on an open edge, unless the edge itself is open).
        // Offered where the vertex is within the tolerance of every plane both ends stand for.
        void Offer(int a, int b)
        {
            var edgeOpen = Triangles(t, deadT, around[a], b) == 1;
            bool Fixed(int x) => locked[x] || open[x] && !edgeOpen;
            if (Fixed(a) && Fixed(b)) return;
            foreach (var (cost, at) in Candidates(q, v, Fixed(a), Fixed(b), a, b))
                if (Within(pn, pd, planes[a], at, tolerance) && Within(pn, pd, planes[b], at, tolerance)) { heap.Enqueue((a, b, stamp[a], stamp[b], at), cost); return; }
        }
        foreach (var (e, _) in uses) { var (a, b) = EdgeKeys.Ends(e); Offer(a, b); }

        var alive = 0;
        for (var k = 0; k < tc; k++) if (!deadT[k]) alive++;
        var shared = new List<int>();
        var na = new HashSet<int>();
        while (alive > 4 && heap.TryDequeue(out var c, out _))
        {
            var (a, b, sa, sb, at) = c;
            if (deadV[a] || deadV[b] || stamp[a] != sa || stamp[b] != sb) continue;
            if (!CanCollapse(t, v, deadT, around, a, b, at, shared, na)) continue;
            // b goes into a, at the point chosen: the triangles they share go, b's others take a.
            foreach (var k in around[b])
            {
                if (deadT[k]) continue;
                if (t[3 * k] == a || t[3 * k + 1] == a || t[3 * k + 2] == a) { deadT[k] = true; alive--; continue; }
                for (var j = 0; j < 3; j++) if (t[3 * k + j] == b) t[3 * k + j] = a;
                around[a].Add(k);
            }
            around[b].Clear();
            around[a].RemoveAll(k => deadT[k]);
            deadV[b] = true;
            v[a] = at;
            open[a] |= open[b];
            locked[a] |= locked[b];
            for (var j = 0; j < 10; j++) q[a * 10 + j] += q[b * 10 + j];
            planes[a] = Union(planes[a], planes[b]);
            planes[b] = [];
            stamp[a]++;
            na.Clear();
            foreach (var k in around[a]) for (var j = 0; j < 3; j++) { var x = t[3 * k + j]; if (x != a) na.Add(x); }
            foreach (var x in na) { stamp[x]++; }
            foreach (var x in na)
            {
                Offer(a, x);
                // The neighbour's other edges were offered with its old stamp: again with the new.
                foreach (var k in around[x])
                    if (!deadT[k]) for (var j = 0; j < 3; j++) { var y = t[3 * k + j]; if (y != x && y != a) Offer(Math.Min(x, y), Math.Max(x, y)); }
            }
        }

        var map = new int[n];
        Array.Fill(map, -1);
        var outV = new List<Vec3>();
        var outT = new List<(int A, int B, int C)>(alive);
        for (var k = 0; k < tc; k++)
        {
            if (deadT[k]) continue;
            int Of(int x) { if (map[x] < 0) { map[x] = outV.Count; outV.Add(v[x]); } return map[x]; }
            outT.Add((Of(t[3 * k]), Of(t[3 * k + 1]), Of(t[3 * k + 2])));
        }
        return ([.. outV], outT);
    }

    /// <summary>The triangles of a solid: a triangle as it is, any other face triangulated in its plane.</summary>
    public static List<(int A, int B, int C)> Triangles(Solid s)
    {
        var o = new List<(int A, int B, int C)>(s.Faces.Length);
        foreach (var f in s.Faces)
        {
            if (f.Outer.Length < 3) continue;
            if (f.Outer.Length == 3 && f.Holes.Length == 0) { o.Add((f.Outer[0], f.Outer[1], f.Outer[2])); continue; }
            o.AddRange(Triangulator.Face(s.Vertices, f.Outer, f.Holes, f.Normal));
        }
        return o;
    }

    /// <summary>
    /// A solid simplified within <paramref name="tolerance"/>, its coplanar triangles merged into faces again;
    /// the solid itself when nothing would change or the result would not close.
    /// </summary>
    public static Solid Simplify(Solid s, double tolerance)
    {
        var tris = Triangles(s);
        var (v, t) = tolerance > 0 ? Simplify(s.Vertices, tris, tolerance) : (s.Vertices, tris);
        Solid? merged = null;
        try { merged = SolidFromMesh.Build(v, t, s.Tag); } catch (InvalidOperationException) { merged = null; }
        return merged != null && merged.Faces.Length > 0 && merged.Faces.Length <= s.Faces.Length ? merged : s;
    }

    static void AddPlane(double[] q, int x, double a, double b, double c, double d)
    {
        var o = x * 10;
        q[o] += a * a; q[o + 1] += a * b; q[o + 2] += a * c; q[o + 3] += a * d;
        q[o + 4] += b * b; q[o + 5] += b * c; q[o + 6] += b * d;
        q[o + 7] += c * c; q[o + 8] += c * d; q[o + 9] += d * d;
    }

    static double Error(double[] q, int a, int b, Vec3 p)
    {
        double E(int o) => q[o] * p.X * p.X + 2 * q[o + 1] * p.X * p.Y + 2 * q[o + 2] * p.X * p.Z + 2 * q[o + 3] * p.X
            + q[o + 4] * p.Y * p.Y + 2 * q[o + 5] * p.Y * p.Z + 2 * q[o + 6] * p.Y + q[o + 7] * p.Z * p.Z + 2 * q[o + 8] * p.Z + q[o + 9];
        return Math.Max(0, E(a * 10)) + Math.Max(0, E(b * 10));
    }

    /// <summary>Where the collapse of a–b may put its vertex, the cheapest first: a fixed end stays; otherwise the ends and the middle.</summary>
    static List<(double Cost, Vec3 At)> Candidates(double[] q, Vec3[] v, bool fixedA, bool fixedB, int a, int b)
    {
        if (fixedA) return [(Error(q, a, b, v[a]), v[a])];
        if (fixedB) return [(Error(q, a, b, v[b]), v[b])];
        var mid = (v[a] + v[b]) * 0.5;
        List<(double Cost, Vec3 At)> o = [(Error(q, a, b, v[a]), v[a]), (Error(q, a, b, v[b]), v[b]), (Error(q, a, b, mid), mid)];
        o.Sort((x, y) => x.Cost.CompareTo(y.Cost));
        return o;
    }

    /// <summary>Whether <paramref name="p"/> is within <paramref name="tol"/> of every plane of <paramref name="ids"/>.</summary>
    static bool Within(List<Vec3> pn, List<double> pd, List<int> ids, Vec3 p, double tol)
    {
        foreach (var i in ids) if (Math.Abs(Vec3.Dot(pn[i], p) + pd[i]) > tol) return false;
        return true;
    }

    /// <summary>Two sorted id lists as one, each id once.</summary>
    static List<int> Union(List<int> a, List<int> b)
    {
        var o = new List<int>(a.Count + b.Count);
        int i = 0, j = 0;
        while (i < a.Count || j < b.Count)
        {
            var x = j >= b.Count || i < a.Count && a[i] <= b[j] ? a[i] : b[j];
            if (i < a.Count && a[i] == x) i++;
            if (j < b.Count && b[j] == x) j++;
            o.Add(x);
        }
        return o;
    }

    /// <summary>
    /// Whether a–b may become one vertex at <paramref name="at"/>: they share exactly the neighbours of the two
    /// triangles on their edge (else the surface would pinch), and no triangle left turns over or flattens.
    /// </summary>
    static bool CanCollapse(int[] t, Vec3[] v, bool[] deadT, List<int>[] around, int a, int b, Vec3 at, List<int> shared, HashSet<int> seen)
    {
        seen.Clear();
        shared.Clear();
        var onEdge = 0;
        foreach (var k in around[a])
        {
            if (deadT[k]) continue;
            var hasB = t[3 * k] == b || t[3 * k + 1] == b || t[3 * k + 2] == b;
            if (hasB) onEdge++;
            for (var j = 0; j < 3; j++) { var x = t[3 * k + j]; if (x != a) seen.Add(x); }
        }
        if (onEdge is not (1 or 2)) return false;
        foreach (var k in around[b])
        {
            if (deadT[k]) continue;
            for (var j = 0; j < 3; j++) { var x = t[3 * k + j]; if (x != b && x != a && seen.Contains(x) && !shared.Contains(x)) shared.Add(x); }
        }
        if (shared.Count != onEdge) return false;
        return Keeps(t, v, deadT, around[a], a, b, at) && Keeps(t, v, deadT, around[b], b, a, at);
    }

    /// <summary>How many live triangles of <paramref name="tris"/> have the vertex <paramref name="other"/> too.</summary>
    static int Triangles(int[] t, bool[] deadT, List<int> tris, int other)
    {
        var c = 0;
        foreach (var k in tris) if (!deadT[k] && (t[3 * k] == other || t[3 * k + 1] == other || t[3 * k + 2] == other)) c++;
        return c;
    }

    /// <summary>Whether the triangles of <paramref name="x"/> not on the edge keep their side when it moves to <paramref name="at"/>.</summary>
    static bool Keeps(int[] t, Vec3[] v, bool[] deadT, List<int> tris, int x, int other, Vec3 at)
    {
        foreach (var k in tris)
        {
            if (deadT[k]) continue;
            int p = t[3 * k], r = t[3 * k + 1], s = t[3 * k + 2];
            if (p == other || r == other || s == other) continue;
            var before = Vec3.Cross(v[r] - v[p], v[s] - v[p]);
            Vec3 P(int i) => i == x ? at : v[i];
            var after = Vec3.Cross(P(r) - P(p), P(s) - P(p));
            var lb = before.Length;
            var la = after.Length;
            if (la < 1e-12 * Math.Max(1, lb) || Vec3.Dot(before, after) < 0.2 * lb * la) return false;
        }
        return true;
    }
}
