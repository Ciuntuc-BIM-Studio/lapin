namespace Lapin.Geometry;

/// <summary>
/// A half-space a section keeps: the points where Normal·p + Constant ≥ 0
/// (three.js's clipping convention; the rest is cut away).
/// </summary>
public readonly record struct ClipPlane(Vec3 Normal, double Constant)
{
    public double Distance(Vec3 p) => Vec3.Dot(Normal, p) + Constant;

    /// <summary>Keeps what lies on <paramref name="normal"/>'s side of the plane through <paramref name="point"/>.</summary>
    public static ClipPlane Through(Vec3 point, Vec3 normal) => new(normal, -Vec3.Dot(normal, point));
}

/// <summary>
/// One cap of a solid as a planar face: a point <see cref="Origin"/> of its plane and the
/// plane's axes <see cref="U"/>, <see cref="V"/>; <see cref="Normal"/> faces out of what is kept.
/// A loop point (x, y) is Origin + U·x + V·y. Loops nest even-odd (inside an odd number of others: a hole).
/// </summary>
public sealed record CapFace(Vec3 Origin, Vec3 U, Vec3 V, Vec3 Normal, Vec2[][] Loops)
{
    public Vec3 At(Vec2 p) => Origin + U * p.X + V * p.Y;
}

/// <summary>
/// The caps of a section seen in 3D: where the planes of a section (one
/// plane, two for a depth, six for a box) cut a closed solid, the faces that
/// close it, so a cut wall reads as a solid and not as a hollow shell. Each
/// solid (a wall's layer, a slab's) gets its own caps, so they take its
/// material. A cap on one plane is clipped to the other planes' kept sides,
/// so a box's caps meet at its edges. The outlines are the caps' edges less
/// the seams: where a cap goes on, on the same plane, into another solid's
/// cap joining by the same key (its material), no line is drawn between
/// them, as in the 2D sections (the seams).
/// </summary>
public static class SectionCaps
{
    const double Tol = 1e-6;     // m: endpoints closer than this are one
    const double SeamTol = 1e-5; // m: segments this close to a line lie on it

    /// <summary>
    /// The caps of each solid (in order) inside the region all <paramref name="planes"/> keep:
    /// triangles facing out of the kept side, their outlines as edges. <paramref name="lift"/>
    /// moves the caps that far into the kept side (and in from the other planes), so a renderer
    /// clipping at the planes keeps them. <paramref name="keys"/>: what each solid's caps join by
    /// (null joins nothing); without keys, no seams are taken out.
    /// </summary>
    public static TriMesh[] Make(IReadOnlyList<Solid> solids, IReadOnlyList<ClipPlane> planes, IReadOnlyList<string?>? keys = null, double lift = 0) =>
        MakeWithFaces(solids, planes, keys, lift).Meshes;

    /// <summary>
    /// As <see cref="Make"/>, with each solid's caps also as faces: their plane's axes and their
    /// loops in it (outlines and holes, even-odd), for what is laid on a cap in its plane (a hatch).
    /// </summary>
    public static (TriMesh[] Meshes, List<CapFace>[] Faces) MakeWithFaces(IReadOnlyList<Solid> solids, IReadOnlyList<ClipPlane> planes, IReadOnlyList<string?>? keys = null, double lift = 0)
    {
        var n = solids.Count;
        var parts = new List<Piece>[n];
        for (var s = 0; s < n; s++) parts[s] = [];
        // Unit normals, so distances are metres.
        var unit = new ClipPlane[planes.Count];
        for (var i = 0; i < planes.Count; i++)
        {
            var len = planes[i].Normal.Length;
            unit[i] = len < 1e-12 ? new ClipPlane(Vec3.UnitZ, 1) : new ClipPlane(planes[i].Normal * (1 / len), planes[i].Constant / len);
        }
        // What every plane leaves of each solid: wholly cut away, or crossed by it.
        var gone = new bool[n];
        var crossed = new bool[n, unit.Length];
        for (var s = 0; s < n; s++)
            for (var i = 0; i < unit.Length; i++)
            {
                double lo = double.MaxValue, hi = double.MinValue;
                foreach (var v in solids[s].Vertices) { var d = unit[i].Distance(v); lo = Math.Min(lo, d); hi = Math.Max(hi, d); }
                if (hi < 0) gone[s] = true;
                crossed[s, i] = lo < 0 && hi >= 0;
            }

        for (var i = 0; i < unit.Length; i++)
        {
            var plane = new PlaneFrame(unit[i]);
            var lines = new List<Line>();
            var cutAway = false;
            for (var j = 0; j < unit.Length; j++)
            {
                if (j == i) continue;
                if (plane.LineOf(unit[j], lift) is { } l) lines.Add(l);
                // Parallel: this plane is wholly kept by that one, or wholly cut away.
                else if (Vec3.Dot(unit[j].Normal, plane.O) + unit[j].Constant < -Tol) cutAway = true;
            }
            if (cutAway) continue;
            var outlines = new List<(int Solid, List<Vec2[]> Loops)>();
            for (var s = 0; s < n; s++)
            {
                if (gone[s] || !crossed[s, i]) continue;
                var loops = Loops(solids[s], unit[i], plane, lines);
                if (loops.Count > 0) outlines.Add((s, loops));
            }
            foreach (var (s, loops) in outlines) parts[s].Add(new Piece(plane, loops, Edges(s, loops, outlines, keys)));
        }

        var result = new TriMesh[n];
        var faces = new List<CapFace>[n];
        for (var s = 0; s < n; s++)
        {
            result[s] = parts[s].Count == 0 ? TriMesh.Empty : Mesh(parts[s], lift);
            faces[s] = [];
            foreach (var piece in parts[s])
            {
                var f = piece.Plane;
                faces[s].Add(new CapFace(f.O + f.N * lift, f.U, f.V, -f.N, [.. piece.Loops]));
            }
        }
        return (result, faces);
    }

    /// <summary>A cutting plane's own axes: its origin (the point nearest the world's), U and V in it, N its kept side's normal.</summary>
    sealed class PlaneFrame
    {
        public readonly Vec3 O, U, V, N;

        public PlaneFrame(ClipPlane p)
        {
            N = p.Normal;
            O = N * -p.Constant;
            (U, V) = VecMath.PlaneBasis(N);
        }

        public Vec2 Map(Vec3 p) { var d = p - O; return new Vec2(Vec3.Dot(d, U), Vec3.Dot(d, V)); }
        public Vec3 Back(Vec2 p, double lift) => O + U * p.X + V * p.Y + N * lift;

        /// <summary>Where another plane meets this one, as the line in this plane's axes that its kept side lies on the positive side of (moved in by <paramref name="inset"/>); null when they are parallel.</summary>
        public Line? LineOf(ClipPlane other, double inset)
        {
            double a = Vec3.Dot(other.Normal, U), b = Vec3.Dot(other.Normal, V);
            var len = Math.Sqrt(a * a + b * b);
            if (len < 1e-9) return null;
            var k = Vec3.Dot(other.Normal, O) + other.Constant;
            return new Line(a / len, b / len, k / len - inset);
        }
    }

    /// <summary>A line a·x + b·y + c = 0 (a, b a unit normal), its kept side where that is ≥ 0.</summary>
    readonly record struct Line(double A, double B, double C)
    {
        public double Side(Vec2 p) => A * p.X + B * p.Y + C;
        public Vec2 Dir => new(-B, A);
        public Vec2 Origin => new(-A * C, -B * C);
    }

    sealed record Piece(PlaneFrame Plane, List<Vec2[]> Loops, List<(Vec2 A, Vec2 B)> Edges);

    static Vec2 Cross(Vec2 a, Vec2 b, double fa, double fb) => a + (b - a) * (fa / (fa - fb));

    /// <summary>
    /// The loops a plane cuts out of a solid, clipped to the other planes' lines: the solid's
    /// section (each face crossing the plane gives segments along its line, as
    /// <see cref="Projection.Section"/>), clipped to each line's kept side, and closed along
    /// each line where it runs inside the section.
    /// </summary>
    static List<Vec2[]> Loops(Solid s, ClipPlane cut, PlaneFrame plane, List<Line> lines)
    {
        var raw = new List<(Vec2 A, Vec2 B)>();
        var depth = new double[s.Vertices.Length];
        for (var k = 0; k < depth.Length; k++) depth[k] = cut.Distance(s.Vertices[k]);
        var hits = new List<(double T, Vec2 P)>();
        foreach (var f in s.Faces)
        {
            var dir = Vec3.Cross(f.Normal, cut.Normal);
            if (dir.Length < 1e-9) continue; // in the plane, or parallel to it
            hits.Clear();
            AddHits(s, f.Outer, depth, dir, plane, hits);
            foreach (var h in f.Holes) AddHits(s, h, depth, dir, plane, hits);
            hits.Sort((x, y) => x.T.CompareTo(y.T));
            for (var k = 0; k + 1 < hits.Count; k += 2) raw.Add((hits[k].P, hits[k + 1].P));
        }
        if (raw.Count == 0) return [];

        var segs = new List<(Vec2 A, Vec2 B)>();
        foreach (var r in raw)
        {
            Vec2 a = r.A, b = r.B;
            var keep = true;
            foreach (var l in lines)
            {
                double fa = l.Side(a), fb = l.Side(b);
                if (fa < 0 && fb < 0) { keep = false; break; }
                if (fa < 0) a = Cross(a, b, fa, fb);
                else if (fb < 0) b = Cross(a, b, fa, fb);
            }
            if (keep && (b - a).Length > Tol) segs.Add((a, b));
        }
        // Along each line, where it runs inside the section: between its crossings with the section's edges, taken in pairs.
        var along = new List<(double T, Vec2 P)>();
        for (var j = 0; j < lines.Count; j++)
        {
            var l = lines[j];
            along.Clear();
            foreach (var r in raw)
            {
                double fa = l.Side(r.A), fb = l.Side(r.B);
                if ((fa >= 0) == (fb >= 0)) continue;
                var p = Cross(r.A, r.B, fa, fb);
                along.Add((Vec2.Dot(p - l.Origin, l.Dir), p));
            }
            along.Sort((x, y) => x.T.CompareTo(y.T));
            for (var k = 0; k + 1 < along.Count; k += 2)
            {
                Vec2 a = along[k].P, b = along[k + 1].P;
                var keep = true;
                for (var m = 0; m < lines.Count && keep; m++)
                {
                    if (m == j) continue;
                    double fa = lines[m].Side(a), fb = lines[m].Side(b);
                    if (fa < 0 && fb < 0) keep = false;
                    else if (fa < 0) a = Cross(a, b, fa, fb);
                    else if (fb < 0) b = Cross(a, b, fa, fb);
                }
                if (keep && (b - a).Length > Tol) segs.Add((a, b));
            }
        }
        return Chain(segs);
    }

    static void AddHits(Solid s, int[] ring, double[] depth, Vec3 dir, PlaneFrame plane, List<(double T, Vec2 P)> hits)
    {
        for (var k = 0; k < ring.Length; k++)
        {
            int p = ring[k], q = ring[(k + 1) % ring.Length];
            if ((depth[p] >= 0) == (depth[q] >= 0)) continue;
            var t = depth[p] / (depth[p] - depth[q]);
            var x = s.Vertices[p] + (s.Vertices[q] - s.Vertices[p]) * t;
            hits.Add((Vec3.Dot(x, dir), plane.Map(x)));
        }
    }

    /// <summary>Segments joined end to end into closed loops; endpoints within <see cref="Tol"/> are one.</summary>
    static List<Vec2[]> Chain(List<(Vec2 A, Vec2 B)> segs)
    {
        var grid = new Dictionary<long, List<int>>();
        long Key(long x, long y) => (x * 73856093L) ^ (y * 19349663L);
        long Cell(double v) => (long)Math.Floor(v / (Tol * 4));
        void Put(Vec2 p, int end)
        {
            var key = Key(Cell(p.X), Cell(p.Y));
            if (!grid.TryGetValue(key, out var l)) grid[key] = l = [];
            l.Add(end);
        }
        Vec2 End(int e) => e % 2 == 0 ? segs[e / 2].A : segs[e / 2].B;
        for (var i = 0; i < segs.Count; i++) { Put(segs[i].A, 2 * i); Put(segs[i].B, 2 * i + 1); }
        var used = new bool[segs.Count];
        // An unused segment's end at p, other than segment `except`.
        int Find(Vec2 p, int except)
        {
            long cx = Cell(p.X), cy = Cell(p.Y);
            for (var dx = -1L; dx <= 1; dx++)
                for (var dy = -1L; dy <= 1; dy++)
                {
                    if (!grid.TryGetValue(Key(cx + dx, cy + dy), out var l)) continue;
                    foreach (var e in l)
                        if (!used[e / 2] && e / 2 != except && (End(e) - p).Length <= Tol) return e;
                }
            return -1;
        }
        var loops = new List<Vec2[]>();
        for (var i = 0; i < segs.Count; i++)
        {
            if (used[i]) continue;
            used[i] = true;
            var loop = new List<Vec2> { segs[i].A };
            var cur = segs[i].B;
            var closed = false;
            while (true)
            {
                if ((cur - loop[0]).Length <= Tol && loop.Count >= 2) { closed = true; break; }
                var e = Find(cur, i);
                if (e < 0) break; // open: a solid that is not closed
                used[e / 2] = true;
                loop.Add(cur);
                cur = e % 2 == 0 ? segs[e / 2].B : segs[e / 2].A;
            }
            if (closed && loop.Count >= 3 && Math.Abs(Polygon2.SignedArea(loop)) > 1e-10) loops.Add([.. loop]);
        }
        return loops;
    }

    /// <summary>A solid's cap outlines on one plane, less the stretches another solid's cap of the same key runs along.</summary>
    static List<(Vec2 A, Vec2 B)> Edges(int solid, List<Vec2[]> loops, List<(int Solid, List<Vec2[]> Loops)> all, IReadOnlyList<string?>? keys)
    {
        var o = new List<(Vec2 A, Vec2 B)>();
        var key = keys != null && solid < keys.Count ? keys[solid] : null;
        var others = new List<(Vec2 A, Vec2 B)>();
        if (key != null)
            foreach (var (s, ls) in all)
                if (s != solid && s < keys!.Count && keys[s] == key)
                    foreach (var l in ls)
                        for (var k = 0; k < l.Length; k++) others.Add((l[k], l[(k + 1) % l.Length]));
        var covered = new List<(double, double)>();
        foreach (var l in loops)
            for (var k = 0; k < l.Length; k++)
            {
                Vec2 a = l[k], b = l[(k + 1) % l.Length];
                var len = (b - a).Length;
                if (len < Tol) continue;
                if (others.Count == 0) { o.Add((a, b)); continue; }
                var e = (b - a) * (1 / len);
                covered.Clear();
                foreach (var (c, d) in others)
                {
                    if (Math.Abs(Vec2.Cross(e, c - a)) > SeamTol || Math.Abs(Vec2.Cross(e, d - a)) > SeamTol) continue;
                    double tc = Vec2.Dot(c - a, e), td = Vec2.Dot(d - a, e);
                    double lo = Math.Max(0, Math.Min(tc, td)), hi = Math.Min(len, Math.Max(tc, td));
                    if (hi - lo > SeamTol) covered.Add((lo, hi));
                }
                if (covered.Count == 0) { o.Add((a, b)); continue; }
                covered.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                var at = 0.0;
                foreach (var (lo, hi) in covered)
                {
                    if (lo - at > SeamTol) o.Add((a + e * at, a + e * lo));
                    at = Math.Max(at, hi);
                }
                if (len - at > SeamTol) o.Add((a + e * at, b));
            }
        return o;
    }

    /// <summary>A solid's caps as one mesh: each plane's loops nested even-odd (a loop inside an odd number of others is a hole), triangulated facing out of the kept side.</summary>
    static TriMesh Mesh(List<Piece> pieces, double lift)
    {
        var pos = new List<float>();
        var nor = new List<float>();
        var idx = new List<int>();
        var edges = new List<float>();
        void Put(List<float> into, Vec3 p) { into.Add((float)p.X); into.Add((float)p.Y); into.Add((float)p.Z); }
        foreach (var piece in pieces)
        {
            var plane = piece.Plane;
            var outward = -plane.N;
            var loops = piece.Loops;
            var area = new double[loops.Count];
            for (var k = 0; k < loops.Count; k++) area[k] = Math.Abs(Polygon2.SignedArea(loops[k]));
            // Each loop's enclosing loops (larger, holding its first point).
            var depth = new int[loops.Count];
            var host = new int[loops.Count];
            for (var k = 0; k < loops.Count; k++)
            {
                host[k] = -1;
                for (var m = 0; m < loops.Count; m++)
                {
                    if (m == k || area[m] <= area[k] || !Polygon2.Contains(loops[m], loops[k][0])) continue;
                    depth[k]++;
                }
            }
            for (var k = 0; k < loops.Count; k++)
            {
                if (depth[k] % 2 == 0) continue;
                // A hole of the smallest outline round it.
                for (var m = 0; m < loops.Count; m++)
                    if (m != k && depth[m] % 2 == 0 && depth[m] == depth[k] - 1 && Polygon2.Contains(loops[m], loops[k][0]) && (host[k] < 0 || area[m] < area[host[k]])) host[k] = m;
            }
            for (var k = 0; k < loops.Count; k++)
            {
                if (depth[k] % 2 != 0) continue;
                var pts = new List<Vec3>();
                int[] Ring(Vec2[] loop) { var r = new int[loop.Length]; for (var q = 0; q < loop.Length; q++) { r[q] = pts.Count; pts.Add(plane.Back(loop[q], lift)); } return r; }
                var outer = Ring(loops[k]);
                var holes = new List<int[]>();
                for (var m = 0; m < loops.Count; m++) if (host[m] == k) holes.Add(Ring(loops[m]));
                var tris = Triangulator.Face(pts, outer, [.. holes], outward);
                var at = pos.Count / 3;
                foreach (var p in pts) { Put(pos, p); Put(nor, outward); }
                foreach (var (a, b, c) in tris) { idx.Add(a + at); idx.Add(b + at); idx.Add(c + at); }
            }
            foreach (var (a, b) in piece.Edges) { Put(edges, plane.Back(a, lift)); Put(edges, plane.Back(b, lift)); }
        }
        return new TriMesh([.. pos], [.. nor], [.. idx], [.. edges]);
    }
}
