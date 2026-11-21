namespace Lapin.Geometry;

/// <summary>
/// A crease on the edge between points <see cref="A"/> and <see cref="B"/> of a cage: <see cref="Sharpness"/> 0 is smooth,
/// a whole number n stays sharp for n subdivisions and then softens (semi-sharp), a fraction blends the smooth and the sharp
/// rule, and <see cref="SubD.Infinite"/> or more is sharp for good.
/// </summary>
public readonly record struct SubDCrease(int A, int B, double Sharpness);

/// <summary>
/// A subdivision cage: points, polygonal faces of any side count (indices into the points, counter-clockwise seen from
/// outside), and creases on some of its edges. Check it with <see cref="SubD.Problem"/> before use.
/// </summary>
public sealed class SubDCage
{
    public SubDCage(IReadOnlyList<Vec3> points, IReadOnlyList<int[]> faces, IReadOnlyList<SubDCrease>? creases = null)
    {
        Points = [.. points];
        Faces = new int[faces.Count][];
        for (var i = 0; i < faces.Count; i++) Faces[i] = (int[])faces[i].Clone();
        Creases = creases == null ? [] : [.. creases];
    }

    public Vec3[] Points { get; }
    public int[][] Faces { get; }
    public SubDCrease[] Creases { get; }

    /// <summary>Whether every edge has two faces (no border): a closed cage gives a closed solid.</summary>
    public bool IsClosed
    {
        get
        {
            var uses = new Dictionary<long, int>();
            foreach (var f in Faces)
                for (var i = 0; i < f.Length; i++)
                {
                    var k = SubD.Key(f[i], f[(i + 1) % f.Length]);
                    uses[k] = uses.GetValueOrDefault(k) + 1;
                }
            return uses.Count > 0 && uses.Values.All(n => n == 2);
        }
    }

    /// <summary>The sharpness the cage gives the edge a–b (0 when it has no crease).</summary>
    public double SharpnessOf(int a, int b)
    {
        var k = SubD.Key(a, b);
        var s = 0.0;
        foreach (var c in Creases) if (SubD.Key(c.A, c.B) == k) s = c.Sharpness;
        return s;
    }

    /// <summary>Every edge once (its two points, the lower first).</summary>
    public List<(int A, int B)> Edges()
    {
        var seen = new HashSet<long>();
        var o = new List<(int, int)>();
        foreach (var f in Faces)
            for (var i = 0; i < f.Length; i++)
            {
                var (a, b) = (f[i], f[(i + 1) % f.Length]);
                if (seen.Add(SubD.Key(a, b))) o.Add((Math.Min(a, b), Math.Max(a, b)));
            }
        return o;
    }
}

/// <summary>Where the thickness of an open SubD surface lies: along its normal (out), against it (in), or half each way.</summary>
public enum SubDSide { Out, In, Centre }

/// <summary>
/// How a cage is made into a body: at most <see cref="MaxLevel"/> subdivisions, fewer when the triangles already lie within
/// <see cref="Tolerance"/> of the limit surface (the chord rule, <see cref="Chords.Tolerance"/>), never more quads than
/// <see cref="FaceBudget"/>; an open cage thickened by <see cref="Thickness"/> on its <see cref="Side"/>.
/// </summary>
public sealed record SubDOptions(int MaxLevel = SubD.DefaultLevels, double Tolerance = Chords.Tolerance, int FaceBudget = SubD.DefaultBudget,
    double Thickness = 0, SubDSide Side = SubDSide.Out);

/// <summary>
/// A cage made into a body: the <see cref="Solid"/> (closed for a closed or a thickened cage, an open shell otherwise), the
/// <see cref="Level"/> of subdivision it took, how far its triangles stray from the limit surface (<see cref="Deviation"/>, m;
/// NaN when it could not be measured), whether it is <see cref="Closed"/>, or the <see cref="Problem"/> that stopped it.
/// </summary>
public sealed record SubDResult(Solid? Solid, int Level, double Deviation, bool Closed, string? Problem);

/// <summary>
/// Catmull–Clark subdivision surfaces (Catmull &amp; Clark 1978) with semi-sharp creases (DeRose, Kass &amp; Truong 1998), written
/// from the published rules (docs/plan-studio-freeform.md §8, decision 772):
/// <list type="bullet">
/// <item>A step makes a face point at each face's centroid, an edge point on each edge, and moves each old point; each n-sided
/// face becomes n quads.</item>
/// <item>Smooth edge point: the average of its two ends and the two face points beside it. Smooth vertex point:
/// (Q + 2R + (n − 3)S) / n, Q the average of the face points round it, R of the middles of its n edges, S the point itself.</item>
/// <item>A sharp edge (sharpness ≥ 1, or a border) puts its edge point at its middle. A point on two sharp edges goes to
/// (A + 6S + B) / 8 (A, B their other ends: the cubic B-spline of the crease); on three or more it stays (a corner). A
/// sharpness s between 0 and 1 blends the two rules by s, a point's sharpness being the average of its creases'. Each step
/// takes 1 off a crease's sharpness; <see cref="Infinite"/> or more never softens.</item>
/// <item>An open border is a sharp crease, so it stays the cubic B-spline of its border points; a border point of one face
/// only (a sheet's corner) stays where it is.</item>
/// <item>After the last step every point goes to its limit (the standard masks: (n²S + 4Σeᵢ + Σdᵢ) / (n(n + 5)) for a smooth
/// point of valence n, eᵢ its edge neighbours and dᵢ the far corners of its quads; (A + 4S + B) / 6 on a crease; a corner
/// stays), and its normal is the cross of the two limit tangent masks (smooth points) or the mean of its faces' normals.</item>
/// </list>
/// Levels follow the chord rule: the cage is subdivided until the next level's limit points (edge and face middles) lie within
/// the tolerance of the present level's triangles, at least once and as often as the sharpest semi-sharp crease needs, at most
/// <see cref="SubDOptions.MaxLevel"/> times and never past the face budget.
/// </summary>
public static class SubD
{
    /// <summary>A sharpness at least this is sharp for good (never softened by a step).</summary>
    public const double Infinite = 10;

    /// <summary>Subdivisions at most, unless asked otherwise.</summary>
    public const int DefaultLevels = 4;

    /// <summary>The most quads a body is made of.</summary>
    public const int DefaultBudget = 200_000;

    /// <summary>The most levels anyone may ask for (each takes four times the faces of the last).</summary>
    public const int MaxLevels = 6;

    internal static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

    // ------------------------------------------------------------ validity

    /// <summary>Why the cage cannot be subdivided (not manifold, faces facing different ways, bad indices…), or null when it can.</summary>
    public static string? Problem(SubDCage cage) => Net.Make(cage.Points, cage.Faces, cage.Creases, validate: true).Problem;

    // ------------------------------------------------------------ one step, limits

    /// <summary>One Catmull–Clark step with creases: the cage's points first, then one point per edge (in <see cref="SubDCage.Edges"/>' order of first use), then one per face.</summary>
    public static SubDCage Step(SubDCage cage)
    {
        var (net, problem) = Net.Make(cage.Points, cage.Faces, cage.Creases, validate: true);
        if (net == null) throw new ArgumentException(problem);
        var next = Step(net);
        return next.ToCage();
    }

    /// <summary>The limit position and normal of every point of a cage made of quads (one subdivided at least once).</summary>
    public static (Vec3[] Points, Vec3[] Normals) Limit(SubDCage cage)
    {
        var (net, problem) = Net.Make(cage.Points, cage.Faces, cage.Creases, validate: true);
        if (net == null) throw new ArgumentException(problem);
        var l = Limits(net);
        return (l, Normals(net, l));
    }

    // ------------------------------------------------------------ the body

    /// <summary>
    /// The cage made into a body: subdivided by the chord rule, pushed to its limit surface, as triangles (planar quads kept
    /// whole) in a <see cref="Solid"/>. A closed cage gives a closed solid (turned outward if the cage faced in); an open one an
    /// open shell, or with a thickness a closed solid: the surface and its offset along the limit normals, the borders stitched.
    /// </summary>
    public static SubDResult Build(SubDCage cage, SubDOptions? options = null, string? tag = null)
    {
        var o = options ?? new SubDOptions();
        var (net, problem) = Net.Make(cage.Points, cage.Faces, cage.Creases, validate: true);
        if (net == null) return new SubDResult(null, 0, double.NaN, false, problem);
        var closed = net.Boundary == 0;
        var maxLevel = Math.Clamp(o.MaxLevel, 1, MaxLevels);
        // A semi-sharp crease of sharpness s shows only after s steps: at least that many, within the cap.
        var sharpest = net.ES.Where(s => s > 0 && s < Infinite).DefaultIfEmpty(0).Max();
        var minLevel = Math.Clamp((int)Math.Ceiling(sharpest - 1e-9), 1, maxLevel);
        var budget = Math.Max(1, o.FaceBudget);
        if (net.QuadsNext > budget) return new SubDResult(null, 0, double.NaN, closed, $"The cage is too big to subdivide ({net.QuadsNext} faces after one step, at most {budget}).");
        var tol = o.Tolerance > 0 ? o.Tolerance : Chords.Tolerance;

        var cur = Step(net);
        var level = 1;
        var lim = Limits(cur);
        double deviation;
        var last = double.NaN;
        while (true)
        {
            if (cur.QuadsNext > budget) { deviation = last / 4; break; }
            var next = Step(cur);
            var nextLim = Limits(next);
            var d = Deviation(cur, lim, nextLim);
            if (level >= maxLevel || level >= minLevel && d <= tol) { deviation = d; break; }
            (cur, lim, last) = (next, nextLim, d);
            level++;
        }
        var normals = Normals(cur, lim);
        var solid = Surface(cur, lim, normals, closed, o, tag);
        if (solid == null) return new SubDResult(null, level, deviation, closed, "The surface could not be made into a body.");
        return new SubDResult(solid, level, deviation, closed || o.Thickness > 0, null);
    }

    /// <summary>
    /// How far the triangles of a level stray from the limit surface, measured where the next level puts points on it: at
    /// each edge's middle (the chord's sag) and at each face's middle (its corners' mean, the quad's two triangles meeting
    /// there within a hair). An estimate of the chord error, exact on the sample points.
    /// </summary>
    static double Deviation(Net cur, Vec3[] lim, Vec3[] nextLim)
    {
        var (v, e) = (cur.P.Length, cur.EA.Length);
        var worst = 0.0;
        for (var i = 0; i < e; i++)
            worst = Math.Max(worst, (nextLim[v + i] - (lim[cur.EA[i]] + lim[cur.EB[i]]) * 0.5).Length);
        for (var f = 0; f < cur.F.Length; f++)
        {
            var c = Vec3.Zero;
            foreach (var k in cur.F[f]) c += lim[k];
            worst = Math.Max(worst, (nextLim[v + e + f] - c * (1.0 / cur.F[f].Length)).Length);
        }
        return worst;
    }

    /// <summary>The limit surface as faces of a solid: a quad whole where it is flat, else two triangles on its shorter diagonal; thickened when asked.</summary>
    static Solid? Surface(Net n, Vec3[] lim, Vec3[] normals, bool closed, SubDOptions o, string? tag)
    {
        var faces = new List<FaceInput>(n.F.Length * 2);
        void Quad(IReadOnlyList<Vec3> pts, int a, int b, int c, int d, bool flip)
        {
            int[] q = flip ? [d, c, b, a] : [a, b, c, d];
            if (Flat(pts[q[0]], pts[q[1]], pts[q[2]], pts[q[3]])) { faces.Add(new FaceInput(q)); return; }
            if ((pts[q[0]] - pts[q[2]]).Length <= (pts[q[1]] - pts[q[3]]).Length)
            {
                faces.Add(new FaceInput([q[0], q[1], q[2]]));
                faces.Add(new FaceInput([q[0], q[2], q[3]]));
            }
            else
            {
                faces.Add(new FaceInput([q[1], q[2], q[3]]));
                faces.Add(new FaceInput([q[1], q[3], q[0]]));
            }
        }
        if (closed || !(o.Thickness > 0))
        {
            foreach (var f in n.F) Quad(lim, f[0], f[1], f[2], f[3], false);
            var s = SolidBuilder.Make(lim, faces, tag);
            if (s.Faces.Length == 0) return null;
            return closed && Measure.SignedVolume(s) < 0 ? SolidBuilder.Flip(s) : s;
        }
        var (hi, lo) = o.Side switch
        {
            SubDSide.In => (0.0, -o.Thickness),
            SubDSide.Centre => (o.Thickness / 2, -o.Thickness / 2),
            _ => (o.Thickness, 0.0),
        };
        var v = lim.Length;
        var pts = new Vec3[2 * v];
        for (var i = 0; i < v; i++)
        {
            pts[i] = lim[i] + normals[i] * hi;
            pts[v + i] = lim[i] + normals[i] * lo;
        }
        foreach (var f in n.F)
        {
            Quad(pts, f[0], f[1], f[2], f[3], false);
            Quad(pts, v + f[0], v + f[1], v + f[2], v + f[3], true);
        }
        // The borders stitched: along each border edge a→b of the surface, a band b, a, a′, b′ (′ the other side).
        for (var e = 0; e < n.EA.Length; e++)
        {
            if (n.EF1[e] >= 0) continue;
            var (a, b) = n.Directed(e);
            Quad(pts, b, a, v + a, v + b, false);
        }
        var solid = SolidBuilder.Make(pts, faces, tag);
        if (solid.Faces.Length == 0) return null;
        return Measure.SignedVolume(solid) < 0 ? SolidBuilder.Flip(solid) : solid;
    }

    static bool Flat(Vec3 a, Vec3 b, Vec3 c, Vec3 d)
    {
        var nrm = VecMath.NewellNormal([a, b, c, d]);
        var len = nrm.Length;
        if (len < 1e-18) return false;
        nrm *= 1 / len;
        var m = (a + b + c + d) * 0.25;
        return Math.Abs(Vec3.Dot(a - m, nrm)) <= Tolerance.Planar * 0.1 && Math.Abs(Vec3.Dot(b - m, nrm)) <= Tolerance.Planar * 0.1
            && Math.Abs(Vec3.Dot(c - m, nrm)) <= Tolerance.Planar * 0.1 && Math.Abs(Vec3.Dot(d - m, nrm)) <= Tolerance.Planar * 0.1;
    }

    // ------------------------------------------------------------ symmetry

    /// <summary>
    /// A cage given for one half and a mirror plane (<paramref name="point"/>, <paramref name="normal"/>): the half on the
    /// normal's side (faces with a point behind the plane are left out; a face lying on the plane is the cut and goes) and
    /// its mirror image, welded along the plane (points within <paramref name="tol"/> of it are shared, and put exactly on
    /// it). The mirrored faces are wound the other way, so the whole faces one way. Refused when a face crosses the plane.
    /// </summary>
    public static (SubDCage? Cage, string? Problem) Mirror(SubDCage half, Vec3 point, Vec3 normal, double tol = 1e-6)
    {
        if (normal.Length < 1e-12) return (null, "The symmetry plane needs a direction.");
        var n = normal.Normalized();
        var pts = half.Points;
        double D(int i) => Vec3.Dot(pts[i] - point, n);
        var keep = new List<int>();
        for (var fi = 0; fi < half.Faces.Length; fi++)
        {
            var f = half.Faces[fi];
            bool pos = false, neg = false;
            foreach (var k in f)
            {
                if (k < 0 || k >= pts.Length) return (null, $"Face {fi} names point {k}, which the cage does not have.");
                var d = D(k);
                if (d > tol) pos = true;
                else if (d < -tol) neg = true;
            }
            if (pos && neg) return (null, $"Face {fi} crosses the symmetry plane: put an edge loop on the plane first.");
            if (pos) keep.Add(fi);
        }
        if (keep.Count == 0) return (null, "Nothing of the cage lies on the kept side of the symmetry plane.");
        var map = new Dictionary<int, int>();
        var mirror = new Dictionary<int, int>();
        var outPts = new List<Vec3>();
        foreach (var fi in keep)
            foreach (var k in half.Faces[fi])
                if (!map.ContainsKey(k))
                {
                    var d = D(k);
                    map[k] = outPts.Count;
                    outPts.Add(Math.Abs(d) <= tol ? pts[k] - n * d : pts[k]);
                }
        foreach (var (k, i) in map.OrderBy(x => x.Value))
        {
            var d = D(k);
            if (Math.Abs(d) <= tol) { mirror[k] = i; continue; }
            mirror[k] = outPts.Count;
            outPts.Add(pts[k] - n * (2 * d));
        }
        var faces = new List<int[]>();
        foreach (var fi in keep) faces.Add(half.Faces[fi].Select(k => map[k]).ToArray());
        foreach (var fi in keep) faces.Add(half.Faces[fi].Reverse().Select(k => mirror[k]).ToArray());
        var creases = new List<SubDCrease>();
        var seen = new HashSet<long>();
        void Add(int a, int b, double s) { if (seen.Add(Key(a, b))) creases.Add(new SubDCrease(a, b, s)); }
        foreach (var c in half.Creases)
        {
            if (!map.TryGetValue(c.A, out var a) || !map.TryGetValue(c.B, out var b)) continue;
            Add(a, b, c.Sharpness);
            Add(mirror[c.A], mirror[c.B], c.Sharpness);
        }
        var whole = new SubDCage(outPts, faces, creases);
        // Creases on edges the kept faces no longer have (the cut face's) are dropped.
        var edges = whole.Edges().Select(x => Key(x.A, x.B)).ToHashSet();
        whole = new SubDCage(whole.Points, whole.Faces, whole.Creases.Where(c => edges.Contains(Key(c.A, c.B))).ToList());
        return Problem(whole) is { } p ? (null, p) : (whole, null);
    }

    /// <summary>
    /// Each point's mirror partner across the plane (itself for a point on it), or −1 where the mirror image is no point of
    /// the cage: what an edit of a symmetric cage moves together.
    /// </summary>
    public static int[] Partners(SubDCage cage, Vec3 point, Vec3 normal, double tol = 1e-6)
    {
        var n = normal.Normalized();
        var o = new int[cage.Points.Length];
        Array.Fill(o, -1);
        var images = cage.Points.Select(p => p - n * (2 * Vec3.Dot(p - point, n))).ToList();
        for (var i = 0; i < cage.Points.Length; i++)
        {
            if (Math.Abs(Vec3.Dot(cage.Points[i] - point, n)) <= tol) { o[i] = i; continue; }
            for (var j = 0; j < cage.Points.Length; j++)
                if ((cage.Points[j] - images[i]).Length <= tol * 10) { o[i] = j; break; }
        }
        return o;
    }

    // ------------------------------------------------------------ the subdivision itself

    static Net Step(Net n)
    {
        var (v, e, fc) = (n.P.Length, n.EA.Length, n.F.Length);
        var pts = new Vec3[v + e + fc];
        var fp = new Vec3[fc];
        for (var f = 0; f < fc; f++)
        {
            var c = Vec3.Zero;
            foreach (var k in n.F[f]) c += n.P[k];
            fp[f] = c * (1.0 / n.F[f].Length);
            pts[v + e + f] = fp[f];
        }
        for (var i = 0; i < e; i++)
        {
            var mid = (n.P[n.EA[i]] + n.P[n.EB[i]]) * 0.5;
            var s = n.Sharp(i);
            if (s >= 1) { pts[v + i] = mid; continue; }
            var smooth = (n.P[n.EA[i]] + n.P[n.EB[i]] + fp[n.EF0[i]] + fp[n.EF1[i]]) * 0.25;
            pts[v + i] = s <= 0 ? smooth : smooth + (mid - smooth) * s;
        }
        for (var k = 0; k < v; k++)
        {
            var S = n.P[k];
            var edges = n.VE[k];
            var deg = edges.Count;
            if (deg == 0) { pts[k] = S; continue; }
            var (count, sharp, a, b, corner) = n.Creases(k);
            Vec3 Smooth()
            {
                var q = Vec3.Zero;
                foreach (var f in n.VF[k]) q += fp[f];
                q *= 1.0 / n.VF[k].Count;
                var r = Vec3.Zero;
                foreach (var ei in edges) r += (n.P[n.EA[ei]] + n.P[n.EB[ei]]) * 0.5;
                r *= 1.0 / deg;
                return (q + r * 2 + S * (deg - 3)) * (1.0 / deg);
            }
            if (count < 2 && !corner) { pts[k] = Smooth(); continue; }
            var hard = corner || count > 2 ? S : (n.P[a] + S * 6 + n.P[b]) * 0.125;
            pts[k] = sharp >= 1 ? hard : Smooth() is var sm ? sm + (hard - sm) * sharp : hard;
        }
        var faces = new int[n.F.Sum(f => f.Length)][];
        var at = 0;
        for (var f = 0; f < fc; f++)
        {
            var loop = n.F[f];
            var m = loop.Length;
            for (var i = 0; i < m; i++)
            {
                var (prev, here, nxt) = (loop[(i + m - 1) % m], loop[i], loop[(i + 1) % m]);
                faces[at++] = [here, v + n.Edge[Key(here, nxt)], v + e + f, v + n.Edge[Key(prev, here)]];
            }
        }
        var creases = new List<SubDCrease>();
        for (var i = 0; i < e; i++)
        {
            var s = n.ES[i];
            if (!(s > 0) || n.EF1[i] < 0) continue;
            var child = s >= Infinite ? s : s - 1;
            if (child <= 1e-12) continue;
            creases.Add(new SubDCrease(n.EA[i], v + i, child));
            creases.Add(new SubDCrease(v + i, n.EB[i], child));
        }
        return Net.Make(pts, faces, creases, validate: false).Net!;
    }

    /// <summary>Each point pushed to the limit surface (the mesh made of quads).</summary>
    static Vec3[] Limits(Net n)
    {
        var o = new Vec3[n.P.Length];
        for (var k = 0; k < n.P.Length; k++)
        {
            var S = n.P[k];
            var edges = n.VE[k];
            var deg = edges.Count;
            if (deg == 0) { o[k] = S; continue; }
            var (count, sharp, a, b, corner) = n.Creases(k);
            Vec3 Smooth()
            {
                var e = Vec3.Zero;
                foreach (var ei in edges) e += n.P[n.EA[ei] == k ? n.EB[ei] : n.EA[ei]];
                var d = Vec3.Zero;
                foreach (var f in n.VF[k])
                {
                    var loop = n.F[f];
                    if (loop.Length != 4) throw new InvalidOperationException("Limit positions need a mesh of quads: subdivide once first.");
                    d += n.P[loop[(Array.IndexOf(loop, k) + 2) % 4]];
                }
                return (S * (deg * deg) + e * 4 + d) * (1.0 / (deg * (deg + 5)));
            }
            if (count < 2 && !corner) { o[k] = Smooth(); continue; }
            var hard = corner || count > 2 ? S : (n.P[a] + S * 4 + n.P[b]) * (1.0 / 6);
            o[k] = sharp >= 1 ? hard : Smooth() is var sm ? sm + (hard - sm) * sharp : hard;
        }
        return o;
    }

    /// <summary>
    /// Each limit point's normal: at a smooth point the cross of the two tangent masks (Halstead et al. 1993) on the control
    /// points, turned to agree with its faces; on a crease, a border or a corner (or where the masks give nothing) the mean
    /// of the normals of its faces at the limit.
    /// </summary>
    static Vec3[] Normals(Net n, Vec3[] lim)
    {
        var fn = new Vec3[n.F.Length];
        for (var f = 0; f < n.F.Length; f++) fn[f] = VecMath.NewellNormal(n.F[f].Select(k => lim[k]).ToArray());
        var o = new Vec3[n.P.Length];
        for (var k = 0; k < n.P.Length; k++)
        {
            var mean = Vec3.Zero;
            foreach (var f in n.VF[k]) mean += fn[f];
            mean = mean.Length > 1e-18 ? mean.Normalized() : Vec3.UnitZ;
            o[k] = mean;
            var (count, _, _, _, corner) = n.Creases(k);
            if (count > 0 || corner || n.VF[k].Count < 3) continue;
            if (Tangents(n, k) is not { } t) continue;
            var c = Vec3.Cross(t.U, t.V);
            if (c.Length < 1e-18) continue;
            c = c.Normalized();
            o[k] = Vec3.Dot(c, mean) < 0 ? -c : c;
        }
        return o;
    }

    /// <summary>The two limit tangents at a smooth inner point of a quad mesh, its quads taken in turn round it.</summary>
    static (Vec3 U, Vec3 V)? Tangents(Net n, int k)
    {
        var quads = n.VF[k];
        var m = quads.Count;
        var byNext = new Dictionary<int, (int Diag, int Prev)>();
        foreach (var f in quads)
        {
            var loop = n.F[f];
            if (loop.Length != 4) return null;
            var i = Array.IndexOf(loop, k);
            byNext[loop[(i + 1) % 4]] = (loop[(i + 2) % 4], loop[(i + 3) % 4]);
        }
        if (byNext.Count != m) return null;
        var e = new Vec3[m];
        var d = new Vec3[m];
        var cur = byNext.Keys.First();
        for (var i = 0; i < m; i++)
        {
            if (!byNext.TryGetValue(cur, out var x)) return null;
            e[i] = n.P[cur];
            d[i] = n.P[x.Diag];
            cur = x.Prev;
        }
        var an = 1 + Math.Cos(2 * Math.PI / m) + Math.Cos(Math.PI / m) * Math.Sqrt(2 * (9 + Math.Cos(2 * Math.PI / m)));
        Vec3 u = Vec3.Zero, w = Vec3.Zero;
        for (var i = 0; i < m; i++)
        {
            double t0 = 2 * Math.PI * i / m, t1 = 2 * Math.PI * (i + 1) / m;
            u += e[i] * (an * Math.Cos(t0)) + d[i] * (Math.Cos(t0) + Math.Cos(t1));
            w += e[i] * (an * Math.Sin(t0)) + d[i] * (Math.Sin(t0) + Math.Sin(t1));
        }
        return (u, w);
    }

    // ------------------------------------------------------------ the mesh with its edges

    /// <summary>A cage with its edges found: each edge's two points, its faces (the second −1 on a border) and its sharpness.</summary>
    sealed class Net
    {
        public Vec3[] P = [];
        public int[][] F = [];
        public int[] EA = [], EB = [], EF0 = [], EF1 = [];
        public double[] ES = [];
        public List<int>[] VE = [], VF = [];
        public Dictionary<long, int> Edge = [];
        public int Boundary;

        /// <summary>The quads one more step makes.</summary>
        public int QuadsNext => F.Sum(f => f.Length);

        /// <summary>An edge's sharpness, a border's sharp for good.</summary>
        public double Sharp(int e) => EF1[e] < 0 ? double.PositiveInfinity : ES[e];

        /// <summary>A border edge as its one face runs along it.</summary>
        public (int A, int B) Directed(int e)
        {
            var loop = F[EF0[e]];
            var i = Array.IndexOf(loop, EA[e]);
            return loop[(i + 1) % loop.Length] == EB[e] ? (EA[e], EB[e]) : (EB[e], EA[e]);
        }

        /// <summary>
        /// The creases at a point: how many of its edges are sharp (borders too), its sharpness (their mean), the other ends of
        /// the first two, and whether it is a corner that stays put (on three creases or more, or a border point of one face).
        /// </summary>
        public (int Count, double Sharp, int A, int B, bool Corner) Creases(int k)
        {
            int count = 0, a = -1, b = -1;
            var sum = 0.0;
            var border = false;
            foreach (var e in VE[k])
            {
                var s = Sharp(e);
                if (EF1[e] < 0) border = true;
                if (!(s > 0)) continue;
                count++;
                sum += Math.Min(s, Infinite);
                var other = EA[e] == k ? EB[e] : EA[e];
                if (a < 0) a = other; else if (b < 0) b = other;
            }
            var corner = count > 2 || border && VF[k].Count == 1;
            return (count, count > 0 ? sum / count : 0, a, b, corner);
        }

        public SubDCage ToCage()
        {
            var creases = new List<SubDCrease>();
            for (var e = 0; e < EA.Length; e++) if (ES[e] > 0) creases.Add(new SubDCrease(EA[e], EB[e], ES[e]));
            return new SubDCage(P, F, creases);
        }

        public static (Net? Net, string? Problem) Make(IReadOnlyList<Vec3> points, IReadOnlyList<int[]> faces, IReadOnlyList<SubDCrease> creases, bool validate)
        {
            var n = new Net { P = points as Vec3[] ?? [.. points], F = faces as int[][] ?? [.. faces] };
            var vc = n.P.Length;
            if (validate)
            {
                if (n.F.Length == 0) return (null, "The cage has no faces.");
                for (var i = 0; i < vc; i++)
                    if (!double.IsFinite(n.P[i].X) || !double.IsFinite(n.P[i].Y) || !double.IsFinite(n.P[i].Z)) return (null, $"Point {i} of the cage is not a finite point.");
                for (var fi = 0; fi < n.F.Length; fi++)
                {
                    var f = n.F[fi];
                    if (f == null || f.Length < 3) return (null, $"Face {fi} has fewer than 3 corners.");
                    var seen = new HashSet<int>();
                    foreach (var k in f)
                    {
                        if (k < 0 || k >= vc) return (null, $"Face {fi} names point {k}, which the cage does not have.");
                        if (!seen.Add(k)) return (null, $"Face {fi} passes through point {k} twice.");
                    }
                }
            }
            var ea = new List<int>();
            var eb = new List<int>();
            var ef0 = new List<int>();
            var ef1 = new List<int>();
            for (var fi = 0; fi < n.F.Length; fi++)
            {
                var f = n.F[fi];
                for (var i = 0; i < f.Length; i++)
                {
                    var (a, b) = (f[i], f[(i + 1) % f.Length]);
                    var key = Key(a, b);
                    if (!n.Edge.TryGetValue(key, out var e))
                    {
                        n.Edge[key] = ea.Count;
                        ea.Add(a); eb.Add(b); ef0.Add(fi); ef1.Add(-1);
                        continue;
                    }
                    if (validate)
                    {
                        if (ef1[e] >= 0) return (null, $"The edge between points {Math.Min(a, b)} and {Math.Max(a, b)} has three faces or more: a cage is manifold, each edge in one or two faces.");
                        if (ea[e] == a && eb[e] == b) return (null, $"Faces {ef0[e]} and {fi} run the same way along their edge {a}–{b}: the cage's faces must all face the same way.");
                    }
                    ef1[e] = fi;
                }
            }
            (n.EA, n.EB, n.EF0, n.EF1) = ([.. ea], [.. eb], [.. ef0], [.. ef1]);
            n.ES = new double[n.EA.Length];
            foreach (var c in creases)
            {
                if (!n.Edge.TryGetValue(Key(c.A, c.B), out var e))
                {
                    if (validate) return (null, $"A crease names the edge {c.A}–{c.B}, which the cage does not have.");
                    continue;
                }
                if (validate && !(c.Sharpness >= 0)) return (null, $"The crease on the edge {c.A}–{c.B} has a sharpness below 0.");
                n.ES[e] = Math.Min(c.Sharpness, Infinite);
            }
            n.VE = new List<int>[vc];
            n.VF = new List<int>[vc];
            for (var i = 0; i < vc; i++) { n.VE[i] = []; n.VF[i] = []; }
            for (var e = 0; e < n.EA.Length; e++)
            {
                n.VE[n.EA[e]].Add(e);
                n.VE[n.EB[e]].Add(e);
                if (n.EF1[e] < 0) n.Boundary++;
            }
            for (var fi = 0; fi < n.F.Length; fi++) foreach (var k in n.F[fi]) n.VF[k].Add(fi);
            if (validate)
                for (var k = 0; k < vc; k++)
                    if (n.VF[k].Count > 0 && Fans(n, k) > 1) return (null, $"Faces meet at point {k} by their corners only: a cage is manifold, the faces round each point one fan.");
            return (n, null);
        }

        /// <summary>How many separate fans of faces meet at a point (one in a manifold cage).</summary>
        static int Fans(Net n, int k)
        {
            var faces = n.VF[k];
            var parent = new Dictionary<int, int>();
            foreach (var f in faces) parent[f] = f;
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            foreach (var e in n.VE[k])
                if (n.EF1[e] >= 0) parent[Find(n.EF0[e])] = Find(n.EF1[e]);
            return faces.Select(Find).Distinct().Count();
        }
    }
}
