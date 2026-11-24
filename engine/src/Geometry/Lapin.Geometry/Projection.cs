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

    /// <summary>
    /// <paramref name="s"/> with its corners at one place made one (to a micrometre) and every pair of faces that lie on each other
    /// back to back (the joint between two pieces of one part) left empty, the faces' indices and normals kept: its edges are then
    /// those of the one body the pieces make, a barrel roof's strips one curve, two slopes one ridge. The solid itself when no corner
    /// repeats (its map null). Linear in its corners and faces.
    /// </summary>
    public static (Solid Whole, int[]? Map) Welded(Solid s)
    {
        var at = new Dictionary<(long, long, long), int>(s.Vertices.Length);
        var map = new int[s.Vertices.Length];
        var verts = new List<Vec3>(s.Vertices.Length);
        static long Q(double v) => (long)Math.Round(v * 1e6);
        for (var i = 0; i < s.Vertices.Length; i++)
        {
            var p = s.Vertices[i];
            var k = (Q(p.X), Q(p.Y), Q(p.Z));
            if (!at.TryGetValue(k, out var j)) { j = verts.Count; verts.Add(p); at[k] = j; }
            map[i] = j;
        }
        if (verts.Count == s.Vertices.Length) return (s, null);
        var faces = s.Faces.Select(f => f with { Outer = f.Outer.Select(i => map[i]).ToArray(), Holes = f.Holes.Select(h => h.Select(i => map[i]).ToArray()).ToArray() }).ToArray();
        // Faces by their corners (sorted, hashed; the same corners checked on a match).
        var bySet = new Dictionary<int, List<(int[] Corners, int Face)>>();
        for (var fi = 0; fi < faces.Length; fi++)
        {
            if (faces[fi].Holes.Length > 0) continue;
            var corners = faces[fi].Outer.Distinct().Order().ToArray();
            var hash = new HashCode();
            foreach (var c in corners) hash.Add(c);
            var h = hash.ToHashCode();
            if (!bySet.TryGetValue(h, out var same)) bySet[h] = same = [];
            var k = same.FindIndex(x => x.Corners.AsSpan().SequenceEqual(corners) && Vec3.Dot(faces[x.Face].Normal, faces[fi].Normal) < -FeatureCos);
            if (k >= 0)
            {
                faces[fi] = faces[fi] with { Outer = [] };
                faces[same[k].Face] = faces[same[k].Face] with { Outer = [] };
                same.RemoveAt(k);
            }
            else same.Add((corners, fi));
        }
        return (new Solid([.. verts], faces, s.Tag), map);
    }

    static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

    /// <summary>How far apart two seams of one curve may point (rad): a revolved or swept curve's seams converge by its step.</summary>
    const double SeamParallelCos = 0.9063; // cos 25°, over Chords.MaxStep (15°)

    /// <summary>
    /// The edges of <paramref name="s"/> that are seams between the chords of a curve, not folds someone drew. A seam turns gently
    /// (between coplanar and the crease) and one of its two faces has another such edge beside it that turns the same way (both
    /// convex or both concave), by a like angle (within ×3: the first chord off a tangent flat turns half a step, an ellipse's steps
    /// vary) and nearly parallel to it (within 25°): a curve's flats are strips between parallel seams, while a ridge, a slab's fall
    /// or a bent plate is one fold, and a hip meets the ridge or the other hip at an angle. Linear in the edges.
    /// </summary>
    public static HashSet<(int, int)> ChordSeams(Solid s, Topology topo, double creaseCos)
    {
        var gentle = new List<((int, int) Key, Vec3 Dir, double Angle, int Sense, int FaceA, int FaceB)>();
        foreach (var (key, uses) in topo.ByEdge)
        {
            if (uses.Count != 2) continue;
            var h = topo.HalfEdges[uses[0]];
            int fa = h.Face, fb = topo.HalfEdges[uses[1]].Face;
            if (fa == fb) continue;
            Vec3 na = s.Faces[fa].Normal, nb = s.Faces[fb].Normal;
            var dot = Vec3.Dot(na, nb);
            if (dot > FeatureCos || dot <= creaseCos) continue;
            var d = s.Vertices[h.To] - s.Vertices[h.From];
            if (d.Length < 1e-12) continue;
            d = d.Normalized();
            gentle.Add((key, d, Math.Acos(Math.Clamp(dot, -1, 1)), Math.Sign(Vec3.Dot(Vec3.Cross(na, nb), d)), fa, fb));
        }
        var seams = new HashSet<(int, int)>();
        if (gentle.Count < 2) return seams;
        var byFace = new Dictionary<int, List<int>>();
        for (var i = 0; i < gentle.Count; i++)
            foreach (var f in new[] { gentle[i].FaceA, gentle[i].FaceB })
                if (byFace.TryGetValue(f, out var l)) l.Add(i); else byFace[f] = [i];
        bool Alike(int i, int j)
        {
            var (a, b) = (gentle[i], gentle[j]);
            return a.Sense == b.Sense && a.Sense != 0 && Math.Abs(Vec3.Dot(a.Dir, b.Dir)) >= SeamParallelCos
                && Math.Max(a.Angle, b.Angle) <= 3 * Math.Min(a.Angle, b.Angle);
        }
        for (var i = 0; i < gentle.Count; i++)
        {
            if (seams.Contains(gentle[i].Key)) continue;
            foreach (var f in new[] { gentle[i].FaceA, gentle[i].FaceB })
            {
                var near = byFace[f];
                // A face with very many gentle edges (a big flat beside a curved one) is checked only so far.
                foreach (var j in near.Count > 64 ? near.Take(64) : near)
                    if (j != i && Alike(i, j)) { seams.Add(gentle[i].Key); seams.Add(gentle[j].Key); goto next; }
            }
            next:;
        }
        return seams;
    }

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
    /// <param name="span">
    /// How far the drawing reaches sideways (along <see cref="ViewFrame.Right"/>, from the origin): one piece of a
    /// section developed along a polyline sees only between its two ends. Lines and cut areas are clipped there,
    /// nothing is drawn at the clip, and a solid wholly outside is left out.
    /// </param>
    /// <param name="rise">The same up and down (along <see cref="ViewFrame.Up"/>): a section across an element sees only its window.</param>
    /// <param name="smooth">Solids whose curves are made of flats (a member's fillets at LOD 400): an edge between two faces that both face the eye and turn less than the 3D's crease angle draws no line; the outline, where a face turns away, still does.</param>
    /// <param name="chords">
    /// The same for every solid, but only on the seams between the chords of a curve (<see cref="ChordSeams"/>: a curved wall, an
    /// arch, a barrel roof, a round column, a pipe); a gentle fold that was drawn (a low ridge, a slab's fall, a hip) keeps its line.
    /// </param>
    public static (List<Seg2> Lines, List<CutArea> Cuts, List<Seg2> Hidden) Draw(IReadOnlyList<(Solid Solid, int Owner)> solids, ViewFrame view, bool cut = false,
        double maxDepth = double.PositiveInfinity, bool hidden = false, IReadOnlyList<string?>? keys = null, (double Min, double Max)? span = null, (double Min, double Max)? rise = null,
        IReadOnlyList<bool>? smooth = null, bool chords = false)
    {
        var creaseCos = Math.Cos(Tessellator.CreaseDeg * Math.PI / 180);
        bool Joins(int i, int j) => i == j || keys == null || keys[i] != null && keys[i] == keys[j];
        var occluders = new List<Occluder>();
        var cuts = new List<CutArea>();
        var edges = new List<(Vec3 A, Vec3 B, int Solid, int FaceA, int FaceB, int Owner)>();
        var front = new Dictionary<(int Solid, int Face), Occluder>();
        double X(Vec3 p) => Vec3.Dot(p - view.Origin, view.Right);
        double Y(Vec3 p) => Vec3.Dot(p - view.Origin, view.Up);

        for (var si = 0; si < solids.Count; si++)
        {
            var (s, owner) = solids[si];
            if (span is { } sp && s.Vertices.Length > 0)
            {
                double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                foreach (var p in s.Vertices) { var x = X(p); lo = Math.Min(lo, x); hi = Math.Max(hi, x); }
                if (hi < sp.Min - 1e-9 || lo > sp.Max + 1e-9) continue;
            }
            if (rise is { } ry && s.Vertices.Length > 0)
            {
                double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                foreach (var p in s.Vertices) { var y = Y(p); lo = Math.Min(lo, y); hi = Math.Max(hi, y); }
                if (hi < ry.Min - 1e-9 || lo > ry.Max + 1e-9) continue;
            }
            if (cut)
            {
                var loops = Section(s, view);
                if (span is { } sx) loops = ClipBand(loops, sx.Min, sx.Max, p => p.X);
                if (rise is { } sy) loops = ClipBand(loops, sy.Min, sy.Max, p => p.Y);
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
            // The chords' seams, found on the one body a part's pieces make when they meet face to face (a barrel roof's strips):
            // the joint between two strips is then a seam like any other, drawn by neither strip's edge along it.
            var (whole, map) = chords && smooth?[si] != true ? Welded(s) : (s, null);
            var wtopo = map == null ? topo : Topology.Build(whole);
            var seams = chords && smooth?[si] != true ? ChordSeams(whole, wtopo, creaseCos) : null;
            foreach (var (key, uses) in topo.ByEdge)
            {
                var h = topo.HalfEdges[uses[0]];
                int fa = h.Face, fb = uses.Count > 1 ? topo.HalfEdges[uses[1]].Face : -1;
                var na = s.Faces[fa].Normal;
                if (fb >= 0 && Vec3.Dot(na, s.Faces[fb].Normal) > FeatureCos) continue;
                // Only an edge of a face turned to the eye can be seen: the others lie behind the element's own
                // front (or on its outline, where the front face's edge is drawn already).
                if (!(Vec3.Dot(na, view.Dir) < -1e-6 || fb >= 0 && Vec3.Dot(s.Faces[fb].Normal, view.Dir) < -1e-6)) continue;
                if (smooth?[si] == true && fb >= 0 && Vec3.Dot(na, s.Faces[fb].Normal) > creaseCos && Vec3.Dot(na, view.Dir) < -1e-6 && Vec3.Dot(s.Faces[fb].Normal, view.Dir) < -1e-6) continue;
                if (seams is { Count: > 0 })
                {
                    var wk = map == null ? key : Key(map[h.From], map[h.To]);
                    // Both faces across the seam (on the whole body) turned to the eye: no line; one turned away, it is the outline.
                    if (seams.Contains(wk) && wtopo.ByEdge.TryGetValue(wk, out var across) && across.Count == 2
                        && Vec3.Dot(whole.Faces[wtopo.HalfEdges[across[0]].Face].Normal, view.Dir) < -1e-6
                        && Vec3.Dot(whole.Faces[wtopo.HalfEdges[across[1]].Face].Normal, view.Dir) < -1e-6) continue;
                }
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
            if (span is { } sr)
            {
                double xa = X(e.A), xb = X(e.B);
                if (!ClipInterval(xa, xb, sr.Min, ref t0, ref t1) || !ClipInterval(-xa, -xb, -sr.Max, ref t0, ref t1)) continue;
            }
            if (rise is { } rr)
            {
                double ya = Y(e.A), yb = Y(e.B);
                if (!ClipInterval(ya, yb, rr.Min, ref t0, ref t1) || !ClipInterval(-ya, -yb, -rr.Max, ref t0, ref t1)) continue;
            }
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
                if (Polygon2.Inside(face.Rings, (a + b) * 0.5 + off)) off = -off;
                (outA, outB) = (a + off, b + off);
            }
            foreach (var o in grid.Near(a, b))
            {
                if (o.Solid == e.Solid && (o.Face == e.FaceA || o.Face == e.FaceB)) continue;
                if (face != null && Joins(e.Solid, o.Solid) && Coplanar(o, face, (a + b) * 0.5)) { Polygon2.InsideIntervals(o.Rings, outA, outB, 0, 1, covered); continue; }
                if (o.MinDepth > Math.Max(dA, dB) - DepthEps) continue;
                Covered(o, a, b, dA, dB, covered);
            }
            // The seams first in the list: they are no edge at all, hidden or not.
            var shown = Polygon2.Outside(covered);
            foreach (var (s0, s1) in shown)
                if (s1 - s0 > 1e-9) lines.Add(new Seg2(a + (b - a) * s0, a + (b - a) * s1, e.Owner));
            if (hidden)
            {
                // What is neither shown nor a seam: the same edge's covered parts, minus where a coplanar face goes on.
                var seam = new List<(double, double)>();
                if (face != null)
                    foreach (var o in grid.Near(a, b))
                        if (!(o.Solid == e.Solid && (o.Face == e.FaceA || o.Face == e.FaceB)) && Joins(e.Solid, o.Solid) && Coplanar(o, face, (a + b) * 0.5)) Polygon2.InsideIntervals(o.Rings, outA, outB, 0, 1, seam);
                var notShown = Polygon2.Outside([.. shown]);
                foreach (var (s0, s1) in notShown)
                    foreach (var (h0, h1) in Polygon2.Outside(seam).Select(v => (Math.Max(v.Item1, s0), Math.Min(v.Item2, s1))))
                        if (h1 - h0 > 1e-9) behind.Add(new Seg2(a + (b - a) * h0, a + (b - a) * h1, e.Owner));
            }
        }
        return (lines, cuts, behind);
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
        Polygon2.InsideIntervals(o.Rings, a, b, lo, hi, hidden);
    }

    /// <summary>
    /// The loops' parts where <paramref name="along"/> (x or y) is between <paramref name="min"/> and <paramref name="max"/>
    /// (each loop clipped on its own: even-odd stays even-odd under a convex clip).
    /// </summary>
    static List<Vec2[]> ClipBand(List<Vec2[]> loops, double min, double max, Func<Vec2, double> along)
    {
        var o = new List<Vec2[]>(loops.Count);
        foreach (var loop in loops)
        {
            var kept = ClipHalf(ClipHalf(loop, p => along(p) - min), p => max - along(p));
            if (kept.Count >= 3) o.Add([.. kept]);
        }
        return o;
    }

    /// <summary>A polygon's part where <paramref name="inside"/> ≥ 0, Sutherland–Hodgman.</summary>
    static List<Vec2> ClipHalf(IReadOnlyList<Vec2> ring, Func<Vec2, double> inside)
    {
        var o = new List<Vec2>();
        for (var i = 0; i < ring.Count; i++)
        {
            Vec2 p = ring[i], q = ring[(i + 1) % ring.Count];
            double dp = inside(p), dq = inside(q);
            if (dp >= 0) o.Add(p);
            if ((dp >= 0) != (dq >= 0)) o.Add(p + (q - p) * (dp / (dp - dq)));
        }
        return o;
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
                    // From the edge's lower vertex: the two faces that share it walk it opposite ways, and must find the same point
                    // to the last bit, or at site coordinates their segments fall either side of a key and the loop stays open.
                    if (p > q) (p, q) = (q, p);
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
