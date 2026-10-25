namespace Lapin.Geometry;

/// <summary>
/// Where a drawing looks from: the drawing's axes (<see cref="Right"/>,
/// <see cref="Up"/>) and the direction into the scene (<see cref="Dir"/>).
/// A point's depth is its distance in front of the origin's plane, along Dir.
/// </summary>
public sealed record ViewFrame(Vec3 Origin, Vec3 Right, Vec3 Up, Vec3 Dir)
{
    public Vec2 Map(Vec3 p) => new(Vec3.Dot(p - Origin, Right), Vec3.Dot(p - Origin, Up));
    public double Depth(Vec3 p) => Vec3.Dot(p - Origin, Dir);

    /// <summary>Looking horizontally along <paramref name="dir"/> (a plan direction), up being up.</summary>
    public static ViewFrame Horizontal(Vec3 origin, Vec2 dir)
    {
        var d = new Vec3(dir.X, dir.Y, 0).Normalized();
        return new(origin, Vec3.Cross(d, Vec3.UnitZ), Vec3.UnitZ, d);
    }
}

/// <summary>A drawn line of an element (<see cref="Owner"/> is the caller's index for it).</summary>
public readonly record struct Seg2(Vec2 A, Vec2 B, int Owner);

/// <summary>What a section plane cuts out of an element: closed loops, filled even-odd.</summary>
public sealed record CutArea(List<Vec2[]> Loops, int Owner);

/// <summary>
/// Orthographic drawings of planar solids with the hidden lines removed,
/// exactly (no raster): every feature edge is projected and the parts of it
/// some face covers in front of it are taken out — inside that face's outline
/// in the drawing AND nearer along the view. A section first cuts the solids
/// at the view's plane: what is in front goes, the cut areas are drawn (and
/// hide what is behind them), the rest is drawn as seen.
/// </summary>
public static class HiddenLines
{
    const double DepthEps = 1e-5;
    const double FeatureCos = 0.99996; // ~0.5°: coplanar faces of one solid show no edge between them

    sealed class Occluder
    {
        public required List<Vec2[]> Rings;
        public required double MinX, MinY, MaxX, MaxY, MinDepth;
        // depth = C + A·x + B·y over the face's plane
        public required double C, A, B;
        public required int Solid, Face;
    }

    /// <param name="hidden">Also return the parts of the edges that are hidden (for dashed hidden lines); otherwise that list stays empty.</param>
    /// <param name="keys">
    /// What each solid's surface joins by (its material): a face going on in the same plane into another solid's face is
    /// a seam, no line, only when both join by one key (null joins nothing). Without keys, every coplanar continuation is a seam.
    /// </param>
    public static (List<Seg2> Lines, List<CutArea> Cuts, List<Seg2> Hidden) Draw(IReadOnlyList<(Solid Solid, int Owner)> solids, ViewFrame view, bool cut = false,
        double maxDepth = double.PositiveInfinity, bool hidden = false, IReadOnlyList<string?>? keys = null)
    {
        bool Joins(int i, int j) => i == j || keys == null || keys[i] != null && keys[i] == keys[j];
        var occluders = new List<Occluder>();
        var cuts = new List<CutArea>();
        var edges = new List<(Vec3 A, Vec3 B, int Solid, int FaceA, int FaceB, int Owner)>();
        var front = new Dictionary<(int Solid, int Face), Occluder>();

        for (var si = 0; si < solids.Count; si++)
        {
            var (s, owner) = solids[si];
            if (cut)
            {
                var loops = Section(s, view);
                if (loops.Count > 0)
                {
                    cuts.Add(new CutArea(loops, owner));
                    // The cut area faces the eye at depth 0: it hides what is inside and behind the element.
                    AddOccluder(occluders, loops, 0, 0, 0, si, -1);
                }
            }
            for (var fi = 0; fi < s.Faces.Length; fi++)
            {
                var f = s.Faces[fi];
                var nd = Vec3.Dot(f.Normal, view.Dir);
                if (nd > -1e-6) continue; // turned away or edge-on: hides nothing a front face does not
                var rings = new[] { f.Outer }.Concat(f.Holes)
                    .Select(r => r.Select(i => s.Vertices[i]).ToList())
                    .Select(r => cut ? ClipFront(r, view) : r)
                    .Where(r => r.Count >= 3)
                    .Select(r => r.Select(view.Map).ToArray())
                    .ToList();
                if (rings.Count == 0) continue;
                // depth over the plane, as a function of the drawing's x, y
                var p0 = s.Vertices[f.Outer[0]];
                var n = f.Normal;
                var a = -Vec3.Dot(n, view.Right) / nd;
                var b = -Vec3.Dot(n, view.Up) / nd;
                var c = -Vec3.Dot(n, view.Origin - p0) / nd;
                front[(si, fi)] = AddOccluder(occluders, rings, c, a, b, si, fi);
            }

            var topo = Topology.Build(s);
            foreach (var (_, uses) in topo.ByEdge)
            {
                var h = topo.HalfEdges[uses[0]];
                int fa = h.Face, fb = uses.Count > 1 ? topo.HalfEdges[uses[1]].Face : -1;
                var na = s.Faces[fa].Normal;
                if (fb >= 0 && Vec3.Dot(na, s.Faces[fb].Normal) > FeatureCos) continue;
                // Only an edge of a face turned to the eye can be seen: the others lie behind the element's own
                // front (or on its outline, where the front face's edge is drawn already).
                if (!(Vec3.Dot(na, view.Dir) < -1e-6 || fb >= 0 && Vec3.Dot(s.Faces[fb].Normal, view.Dir) < -1e-6)) continue;
                edges.Add((s.Vertices[h.From], s.Vertices[h.To], si, fa, fb, owner));
            }
        }

        var grid = new Grid(occluders);
        var lines = new List<Seg2>();
        var behind = new List<Seg2>();
        var covered = new List<(double, double)>();
        foreach (var e in edges)
        {
            double da = view.Depth(e.A), db = view.Depth(e.B);
            // Keep the part between the cut (or the eye) and the far limit.
            var (t0, t1) = (0.0, 1.0);
            if (cut && !ClipInterval(da, db, 0, ref t0, ref t1)) continue;
            if (!double.IsPositiveInfinity(maxDepth) && !ClipInterval(-da, -db, -maxDepth, ref t0, ref t1)) continue;
            Vec3 A = Lerp(e.A, e.B, t0), B = Lerp(e.A, e.B, t1);
            double dA = view.Depth(A), dB = view.Depth(B);
            Vec2 a = view.Map(A), b = view.Map(B);
            if ((b - a).Length < 1e-7) continue;

            covered.Clear();
            // A seam: the edge's one visible face goes on, in the same plane, as another face of the same material (the
            // next wall of the same facade, the storey above). Where the other side of the edge lies in such a face, it is no edge.
            var face = (front.GetValueOrDefault((e.Solid, e.FaceA)), e.FaceB >= 0 ? front.GetValueOrDefault((e.Solid, e.FaceB)) : null) switch
            {
                ({ } x, null) => x, (null, { } y) => y, _ => null,
            };
            Vec2 outA = a, outB = b;
            if (face != null)
            {
                var off = (b - a).Perp.Normalized() * 1e-4;
                if (Inside(face.Rings, (a + b) * 0.5 + off)) off = -off;
                (outA, outB) = (a + off, b + off);
            }
            foreach (var o in grid.Near(a, b))
            {
                if (o.Solid == e.Solid && (o.Face == e.FaceA || o.Face == e.FaceB)) continue;
                if (face != null && Joins(e.Solid, o.Solid) && Coplanar(o, face, (a + b) * 0.5)) { InsideIntervals(o.Rings, outA, outB, 0, 1, covered); continue; }
                if (o.MinDepth > Math.Max(dA, dB) - DepthEps) continue;
                Covered(o, a, b, dA, dB, covered);
            }
            // The seams first in the list: they are no edge at all, hidden or not.
            var shown = Visible(covered).ToList();
            foreach (var (s0, s1) in shown)
                if (s1 - s0 > 1e-9) lines.Add(new Seg2(a + (b - a) * s0, a + (b - a) * s1, e.Owner));
            if (hidden)
            {
                // What is neither shown nor a seam: the same edge's covered parts, minus where a coplanar face goes on.
                var seam = new List<(double, double)>();
                if (face != null)
                    foreach (var o in grid.Near(a, b))
                        if (!(o.Solid == e.Solid && (o.Face == e.FaceA || o.Face == e.FaceB)) && Joins(e.Solid, o.Solid) && Coplanar(o, face, (a + b) * 0.5)) InsideIntervals(o.Rings, outA, outB, 0, 1, seam);
                var notShown = Complement(shown);
                foreach (var (s0, s1) in notShown)
                    foreach (var (h0, h1) in Visible([.. seam]).Select(v => (Math.Max(v.Item1, s0), Math.Min(v.Item2, s1))))
                        if (h1 - h0 > 1e-9) behind.Add(new Seg2(a + (b - a) * h0, a + (b - a) * h1, e.Owner));
            }
        }
        return (lines, cuts, behind);
    }

    /// <summary>[0, 1] without the given (sorted, disjoint) intervals.</summary>
    static List<(double, double)> Complement(List<(double A, double B)> shown)
    {
        var o = new List<(double, double)>();
        var at = 0.0;
        foreach (var (a, b) in shown) { if (a > at + 1e-12) o.Add((at, a)); at = b; }
        if (at < 1 - 1e-12) o.Add((at, 1));
        return o;
    }

    static Vec3 Lerp(Vec3 a, Vec3 b, double t) => a + (b - a) * t;

    /// <summary>Narrows [t0, t1] on a segment to where its value (va → vb) is at least <paramref name="min"/>; false when nothing is left.</summary>
    static bool ClipInterval(double va, double vb, double min, ref double t0, ref double t1)
    {
        if (va < min && vb < min) return false;
        if (va >= min && vb >= min) return t1 - t0 > 1e-12;
        var t = (min - va) / (vb - va);
        if (va < min) t0 = Math.Max(t0, t); else t1 = Math.Min(t1, t);
        return t1 - t0 > 1e-12;
    }

    static Occluder AddOccluder(List<Occluder> list, List<Vec2[]> rings, double c, double a, double b, int solid, int face)
    {
        var all = rings.SelectMany(r => r).ToList();
        double minDepth = double.PositiveInfinity;
        foreach (var p in all) minDepth = Math.Min(minDepth, c + a * p.X + b * p.Y);
        var o = new Occluder
        {
            Rings = rings, C = c, A = a, B = b, Solid = solid, Face = face, MinDepth = minDepth,
            MinX = all.Min(p => p.X), MinY = all.Min(p => p.Y), MaxX = all.Max(p => p.X), MaxY = all.Max(p => p.Y),
        };
        list.Add(o);
        return o;
    }

    static double DepthAt(Occluder o, Vec2 p) => o.C + o.A * p.X + o.B * p.Y;

    static bool Coplanar(Occluder o, Occluder f, Vec2 at) =>
        Math.Abs(o.A - f.A) < 1e-6 && Math.Abs(o.B - f.B) < 1e-6 && Math.Abs(DepthAt(o, at) - DepthAt(f, at)) < DepthEps;

    /// <summary>Adds the parts of segment a→b the occluder hides: inside its outline (even-odd) and nearer than the segment.</summary>
    static void Covered(Occluder o, Vec2 a, Vec2 b, double da, double db, List<(double, double)> hidden)
    {
        // Where the occluder is in front: f(t) = depth_o(t) − depth_e(t) < −eps, linear in t.
        double f0 = DepthAt(o, a) - da, f1 = DepthAt(o, b) - db;
        double lo = 0, hi = 1;
        if (!ClipInterval(-f0, -f1, DepthEps, ref lo, ref hi)) return;
        InsideIntervals(o.Rings, a, b, lo, hi, hidden);
    }

    static void InsideIntervals(List<Vec2[]> rings, Vec2 a, Vec2 b, double lo, double hi, List<(double, double)> hidden) => Polygon2.InsideIntervals(rings, a, b, lo, hi, hidden);

    static bool Inside(List<Vec2[]> rings, Vec2 p) => Polygon2.Inside(rings, p);

    /// <summary>[0, 1] without the union of the hidden intervals.</summary>
    static IEnumerable<(double, double)> Visible(List<(double A, double B)> hidden)
    {
        hidden.Sort((x, y) => x.A.CompareTo(y.A));
        var at = 0.0;
        foreach (var (h0, h1) in hidden)
        {
            if (h0 > at) yield return (at, h0);
            at = Math.Max(at, h1);
        }
        if (at < 1) yield return (at, 1);
    }

    /// <summary>A polygon's part at or beyond the view's plane (depth ≥ 0), Sutherland–Hodgman.</summary>
    static List<Vec3> ClipFront(List<Vec3> ring, ViewFrame view)
    {
        var o = new List<Vec3>();
        for (var i = 0; i < ring.Count; i++)
        {
            Vec3 p = ring[i], q = ring[(i + 1) % ring.Count];
            double dp = view.Depth(p), dq = view.Depth(q);
            if (dp >= 0) o.Add(p);
            if ((dp >= 0) != (dq >= 0)) o.Add(Lerp(p, q, dp / (dp - dq)));
        }
        return o;
    }

    /// <summary>
    /// The loops the view's plane (depth 0) cuts out of a closed solid, in the
    /// drawing's coordinates. Each face crossing the plane gives segments along
    /// its line; they chain into loops. A vertex exactly on the plane counts as
    /// in front, the same for every face, so the loops close.
    /// </summary>
    public static List<Vec2[]> Section(Solid s, ViewFrame view)
    {
        var segs = new List<(Vec2 A, Vec2 B)>();
        var depth = s.Vertices.Select(view.Depth).ToArray();
        foreach (var f in s.Faces)
        {
            var dir = Vec3.Cross(f.Normal, view.Dir);
            if (dir.Length < 1e-9) continue; // parallel to the plane
            var hits = new List<(double T, Vec2 P)>();
            foreach (var ring in new[] { f.Outer }.Concat(f.Holes))
                for (var i = 0; i < ring.Length; i++)
                {
                    int p = ring[i], q = ring[(i + 1) % ring.Length];
                    if ((depth[p] >= 0) == (depth[q] >= 0)) continue;
                    var x = Lerp(s.Vertices[p], s.Vertices[q], depth[p] / (depth[p] - depth[q]));
                    hits.Add((Vec3.Dot(x, dir), view.Map(x)));
                }
            hits.Sort((x, y) => x.T.CompareTo(y.T));
            for (var k = 0; k + 1 < hits.Count; k += 2) segs.Add((hits[k].P, hits[k + 1].P));
        }
        return Chain(segs);
    }

    static List<Vec2[]> Chain(List<(Vec2 A, Vec2 B)> segs)
    {
        static (long, long) Key(Vec2 p) => ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6));
        var at = new Dictionary<(long, long), List<int>>();
        for (var i = 0; i < segs.Count; i++)
            foreach (var p in new[] { segs[i].A, segs[i].B })
            {
                if (!at.TryGetValue(Key(p), out var l)) at[Key(p)] = l = [];
                l.Add(i);
            }
        var used = new bool[segs.Count];
        var loops = new List<Vec2[]>();
        for (var i = 0; i < segs.Count; i++)
        {
            if (used[i]) continue;
            used[i] = true;
            var loop = new List<Vec2> { segs[i].A };
            var cur = segs[i].B;
            while (Key(cur) != Key(loop[0]))
            {
                var next = at[Key(cur)].FirstOrDefault(j => !used[j], -1);
                if (next < 0) break; // open: a solid that is not closed
                used[next] = true;
                loop.Add(cur);
                cur = Key(segs[next].A) == Key(cur) ? segs[next].B : segs[next].A;
            }
            if (Key(cur) == Key(loop[0]) && loop.Count >= 3 && Math.Abs(Polygon2.SignedArea(loop)) > 1e-10) loops.Add([.. loop]);
        }
        return loops;
    }

    /// <summary>The occluders bucketed on a grid over the drawing, so an edge only meets those near it.</summary>
    sealed class Grid
    {
        readonly List<Occluder>[,] _cells;
        readonly double _x0, _y0, _size;
        readonly int _nx, _ny;
        readonly HashSet<Occluder> _seen = [];

        public Grid(List<Occluder> all)
        {
            if (all.Count == 0) { _cells = new List<Occluder>[1, 1]; _cells[0, 0] = []; _nx = _ny = 1; _size = 1; return; }
            _x0 = all.Min(o => o.MinX); _y0 = all.Min(o => o.MinY);
            double w = Math.Max(all.Max(o => o.MaxX) - _x0, 1e-6), h = Math.Max(all.Max(o => o.MaxY) - _y0, 1e-6);
            var n = Math.Clamp((int)Math.Sqrt(all.Count), 1, 128);
            _size = Math.Max(w, h) / n;
            _nx = (int)(w / _size) + 1; _ny = (int)(h / _size) + 1;
            _cells = new List<Occluder>[_nx, _ny];
            for (var i = 0; i < _nx; i++) for (var j = 0; j < _ny; j++) _cells[i, j] = [];
            foreach (var o in all)
                for (var i = Cx(o.MinX); i <= Cx(o.MaxX); i++)
                    for (var j = Cy(o.MinY); j <= Cy(o.MaxY); j++) _cells[i, j].Add(o);
        }

        int Cx(double x) => Math.Clamp((int)((x - _x0) / _size), 0, _nx - 1);
        int Cy(double y) => Math.Clamp((int)((y - _y0) / _size), 0, _ny - 1);

        public IEnumerable<Occluder> Near(Vec2 a, Vec2 b)
        {
            _seen.Clear();
            double minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X), minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);
            for (var i = Cx(minX); i <= Cx(maxX); i++)
                for (var j = Cy(minY); j <= Cy(maxY); j++)
                    foreach (var o in _cells[i, j])
                        if (o.MaxX >= minX && o.MinX <= maxX && o.MaxY >= minY && o.MinY <= maxY && _seen.Add(o)) yield return o;
        }
    }
}
