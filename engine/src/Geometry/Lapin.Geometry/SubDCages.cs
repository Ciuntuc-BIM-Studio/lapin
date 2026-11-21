namespace Lapin.Geometry;

/// <summary>
/// Cages to start from (a box, a cylinder, a sheet, a solid's faces) and the edits that shape them: points moved, faces
/// extruded, an edge loop inserted across a ring of quads, faces deleted, creases set (docs/plan-studio-freeform.md §8).
/// Every edit gives a new cage; the caller checks it with <see cref="SubD.Problem"/>.
/// </summary>
public static class SubDCages
{
    // ------------------------------------------------------------ shapes

    /// <summary>
    /// A box cage <paramref name="w"/> × <paramref name="l"/> × <paramref name="h"/>, its base centred on the origin, each side
    /// cut into <paramref name="nx"/> × <paramref name="ny"/> × <paramref name="nz"/> quads. Smooth: it subdivides into a
    /// rounded block.
    /// </summary>
    public static SubDCage Box(double w, double l, double h, int nx = 1, int ny = 1, int nz = 1)
    {
        (nx, ny, nz) = (Math.Max(1, nx), Math.Max(1, ny), Math.Max(1, nz));
        var index = new Dictionary<(int, int, int), int>();
        var pts = new List<Vec3>();
        int P(int i, int j, int k)
        {
            if (index.TryGetValue((i, j, k), out var at)) return at;
            index[(i, j, k)] = pts.Count;
            pts.Add(new Vec3(-w / 2 + w * i / nx, -l / 2 + l * j / ny, h * k / nz));
            return pts.Count - 1;
        }
        var faces = new List<int[]>();
        for (var i = 0; i < nx; i++)
            for (var j = 0; j < ny; j++)
            {
                faces.Add([P(i, j, 0), P(i, j + 1, 0), P(i + 1, j + 1, 0), P(i + 1, j, 0)]);
                faces.Add([P(i, j, nz), P(i + 1, j, nz), P(i + 1, j + 1, nz), P(i, j + 1, nz)]);
            }
        for (var i = 0; i < nx; i++)
            for (var k = 0; k < nz; k++)
            {
                faces.Add([P(i, 0, k), P(i + 1, 0, k), P(i + 1, 0, k + 1), P(i, 0, k + 1)]);
                faces.Add([P(i, ny, k), P(i, ny, k + 1), P(i + 1, ny, k + 1), P(i + 1, ny, k)]);
            }
        for (var j = 0; j < ny; j++)
            for (var k = 0; k < nz; k++)
            {
                faces.Add([P(0, j, k), P(0, j, k + 1), P(0, j + 1, k + 1), P(0, j + 1, k)]);
                faces.Add([P(nx, j, k), P(nx, j + 1, k), P(nx, j + 1, k + 1), P(nx, j, k + 1)]);
            }
        return new SubDCage(pts, faces);
    }

    /// <summary>
    /// A cylinder cage of radius <paramref name="r"/> and height <paramref name="h"/>, its base centred on the origin:
    /// <paramref name="sides"/> sides, <paramref name="nz"/> rows of quads up, an n-sided cap at each end. With
    /// <paramref name="creasedCaps"/> the caps' rims are sharp, so the ends stay flat.
    /// </summary>
    public static SubDCage Cylinder(double r, double h, int sides = 8, int nz = 1, bool creasedCaps = true)
    {
        sides = Math.Max(3, sides);
        nz = Math.Max(1, nz);
        var pts = new List<Vec3>();
        for (var k = 0; k <= nz; k++)
            for (var s = 0; s < sides; s++)
            {
                var a = 2 * Math.PI * s / sides;
                pts.Add(new Vec3(r * Math.Cos(a), r * Math.Sin(a), h * k / nz));
            }
        int P(int s, int k) => k * sides + (s % sides);
        var faces = new List<int[]>();
        for (var k = 0; k < nz; k++)
            for (var s = 0; s < sides; s++)
                faces.Add([P(s, k), P(s + 1, k), P(s + 1, k + 1), P(s, k + 1)]);
        faces.Add(Enumerable.Range(0, sides).Reverse().Select(s => P(s, 0)).ToArray());
        faces.Add(Enumerable.Range(0, sides).Select(s => P(s, nz)).ToArray());
        var creases = new List<SubDCrease>();
        if (creasedCaps)
            for (var s = 0; s < sides; s++)
            {
                creases.Add(new SubDCrease(P(s, 0), P(s + 1, 0), SubD.Infinite));
                creases.Add(new SubDCrease(P(s, nz), P(s + 1, nz), SubD.Infinite));
            }
        return new SubDCage(pts, faces, creases);
    }

    /// <summary>A flat sheet <paramref name="w"/> × <paramref name="l"/> centred on the origin, facing up, of <paramref name="nx"/> × <paramref name="ny"/> quads: an open cage.</summary>
    public static SubDCage Plane(double w, double l, int nx = 4, int ny = 4)
    {
        (nx, ny) = (Math.Max(1, nx), Math.Max(1, ny));
        var pts = new List<Vec3>();
        for (var j = 0; j <= ny; j++)
            for (var i = 0; i <= nx; i++)
                pts.Add(new Vec3(-w / 2 + w * i / nx, -l / 2 + l * j / ny, 0));
        int P(int i, int j) => j * (nx + 1) + i;
        var faces = new List<int[]>();
        for (var j = 0; j < ny; j++)
            for (var i = 0; i < nx; i++)
                faces.Add([P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1)]);
        return new SubDCage(pts, faces);
    }

    /// <summary>A solid's faces as a cage (their outer loops; a face with a hole is refused), at most <paramref name="maxFaces"/> of them.</summary>
    public static (SubDCage? Cage, string? Problem) FromSolid(Solid s, int maxFaces = 2000)
    {
        if (s.Faces.Length == 0) return (null, "The solid has no faces.");
        if (s.Faces.Length > maxFaces) return (null, $"The solid has {s.Faces.Length} faces: a cage is made of at most {maxFaces}.");
        if (s.Faces.Any(f => f.Holes.Length > 0)) return (null, "A face of the solid has a hole: a cage's faces have none.");
        var cage = Compact(new SubDCage(s.Vertices, s.Faces.Select(f => f.Outer).ToList()));
        return SubD.Problem(cage) is { } p ? (null, p) : (cage, null);
    }

    // ------------------------------------------------------------ edits

    /// <summary>The cage without the points no face uses, renumbered; creases on edges that are gone dropped.</summary>
    public static SubDCage Compact(SubDCage c)
    {
        var map = new int[c.Points.Length];
        Array.Fill(map, -1);
        var pts = new List<Vec3>();
        var faces = new List<int[]>();
        foreach (var f in c.Faces)
        {
            var g = new int[f.Length];
            for (var i = 0; i < f.Length; i++)
            {
                var k = f[i];
                if (map[k] < 0) { map[k] = pts.Count; pts.Add(c.Points[k]); }
                g[i] = map[k];
            }
            faces.Add(g);
        }
        var cage = new SubDCage(pts, faces);
        var edges = cage.Edges().Select(x => SubD.Key(x.A, x.B)).ToHashSet();
        var creases = c.Creases.Where(x => x.A >= 0 && x.B >= 0 && x.A < map.Length && x.B < map.Length && map[x.A] >= 0 && map[x.B] >= 0)
            .Select(x => x with { A = map[x.A], B = map[x.B] }).Where(x => edges.Contains(SubD.Key(x.A, x.B))).ToList();
        return new SubDCage(pts, faces, creases);
    }

    /// <summary>The points of the given points, edges (pairs of points) and faces, once each.</summary>
    public static List<int> PointsOf(SubDCage c, IEnumerable<int>? points, IEnumerable<(int A, int B)>? edges, IEnumerable<int>? faces)
    {
        var o = new List<int>();
        var seen = new HashSet<int>();
        void Add(int k)
        {
            if (k < 0 || k >= c.Points.Length) throw new ArgumentException($"The cage has no point {k}.");
            if (seen.Add(k)) o.Add(k);
        }
        foreach (var k in points ?? []) Add(k);
        var known = edges == null ? null : c.Edges().Select(x => SubD.Key(x.A, x.B)).ToHashSet();
        foreach (var (a, b) in edges ?? [])
        {
            if (!known!.Contains(SubD.Key(a, b))) throw new ArgumentException($"The cage has no edge {a}–{b}.");
            Add(a); Add(b);
        }
        foreach (var f in faces ?? [])
        {
            if (f < 0 || f >= c.Faces.Length) throw new ArgumentException($"The cage has no face {f}.");
            foreach (var k in c.Faces[f]) Add(k);
        }
        return o;
    }

    /// <summary>The given points moved by <paramref name="move"/>.</summary>
    public static SubDCage Move(SubDCage c, IEnumerable<int> points, Func<Vec3, Vec3> move)
    {
        var pts = (Vec3[])c.Points.Clone();
        foreach (var k in points.Distinct()) pts[k] = move(pts[k]);
        return new SubDCage(pts, c.Faces, c.Creases);
    }

    /// <summary>
    /// Faces extruded by <paramref name="distance"/> along their normal (a region's points along the mean of its faces'
    /// normals there): the region is lifted, a band of quads joins it to the faces round it (or stands on an open border).
    /// The faces keep their indices; the band's are added after the others.
    /// </summary>
    public static (SubDCage? Cage, string? Problem) Extrude(SubDCage c, IReadOnlyCollection<int> faces, double distance)
    {
        if (faces.Count == 0) return (null, "Pick the faces to extrude.");
        if (!double.IsFinite(distance) || Math.Abs(distance) < 1e-9) return (null, "Extrude by more than nothing.");
        var sel = faces.ToHashSet();
        if (sel.Any(f => f < 0 || f >= c.Faces.Length)) return (null, "A face to extrude is not in the cage.");
        var normal = new Dictionary<int, Vec3>();
        foreach (var f in sel)
        {
            var n = VecMath.NewellNormal(c.Faces[f].Select(k => c.Points[k]).ToArray());
            if (n.Length > 0) n = n.Normalized();
            foreach (var k in c.Faces[f]) normal[k] = normal.GetValueOrDefault(k) + n;
        }
        var pts = c.Points.ToList();
        var lifted = new Dictionary<int, int>();
        foreach (var (k, n) in normal)
        {
            lifted[k] = pts.Count;
            pts.Add(c.Points[k] + (n.Length > 1e-12 ? n.Normalized() : Vec3.Zero) * distance);
        }
        // Each edge's faces, to find the region's border.
        var faceCount = new Dictionary<long, int>();
        foreach (var f in sel)
        {
            var loop = c.Faces[f];
            for (var i = 0; i < loop.Length; i++)
            {
                var key = SubD.Key(loop[i], loop[(i + 1) % loop.Length]);
                faceCount[key] = faceCount.GetValueOrDefault(key) + 1;
            }
        }
        var outFaces = new List<int[]>();
        var band = new List<int[]>();
        for (var f = 0; f < c.Faces.Length; f++)
        {
            if (!sel.Contains(f)) { outFaces.Add(c.Faces[f]); continue; }
            var loop = c.Faces[f];
            outFaces.Add(loop.Select(k => lifted[k]).ToArray());
            for (var i = 0; i < loop.Length; i++)
            {
                var (a, b) = (loop[i], loop[(i + 1) % loop.Length]);
                if (faceCount[SubD.Key(a, b)] == 1) band.Add([a, b, lifted[b], lifted[a]]);
            }
        }
        outFaces.AddRange(band);
        var creases = new List<SubDCrease>();
        foreach (var x in c.Creases)
        {
            var inner = lifted.ContainsKey(x.A) && lifted.ContainsKey(x.B) && faceCount.GetValueOrDefault(SubD.Key(x.A, x.B)) == 2;
            creases.Add(inner ? x with { A = lifted[x.A], B = lifted[x.B] } : x);
        }
        var cage = Compact(new SubDCage(pts, outFaces, creases));
        return SubD.Problem(cage) is { } p ? (null, p) : (cage, null);
    }

    /// <summary>
    /// An edge loop inserted across the ring of quads that edge <paramref name="a"/>–<paramref name="b"/> starts, each crossed
    /// edge cut at <paramref name="fraction"/> of the way from the side of <paramref name="a"/>. The ring runs both ways until it
    /// closes, meets a border or a face that is not a quad (that face takes the new point on its edge).
    /// </summary>
    public static (SubDCage? Cage, string? Problem) InsertLoop(SubDCage c, int a, int b, double fraction = 0.5)
    {
        if (!(fraction > 0 && fraction < 1)) return (null, "A loop cuts its edges between their ends: a fraction above 0 and below 1.");
        var byEdge = new Dictionary<long, List<int>>();
        for (var f = 0; f < c.Faces.Length; f++)
        {
            var loop = c.Faces[f];
            for (var i = 0; i < loop.Length; i++)
            {
                var key = SubD.Key(loop[i], loop[(i + 1) % loop.Length]);
                if (!byEdge.TryGetValue(key, out var l)) byEdge[key] = l = [];
                l.Add(f);
            }
        }
        if (!byEdge.TryGetValue(SubD.Key(a, b), out var first)) return (null, $"The cage has no edge {a}–{b}.");
        if (first.All(f => c.Faces[f].Length != 4)) return (null, "The edge has no quad beside it for a loop to run across.");
        var pts = c.Points.ToList();
        var split = new Dictionary<long, int>();
        void Cut(int sideA, int sideB)
        {
            var key = SubD.Key(sideA, sideB);
            if (split.ContainsKey(key)) return;
            split[key] = pts.Count;
            pts.Add(c.Points[sideA] + (c.Points[sideB] - c.Points[sideA]) * fraction);
        }
        var ring = new Dictionary<int, int>();
        void Walk(int f, int pa, int pb)
        {
            while (true)
            {
                var loop = c.Faces[f];
                if (loop.Length != 4 || ring.ContainsKey(f)) return;
                var i = 0;
                while (!(SubD.Key(loop[i], loop[(i + 1) % 4]) == SubD.Key(pa, pb))) i++;
                var (x, y, z, w) = (loop[i], loop[(i + 1) % 4], loop[(i + 2) % 4], loop[(i + 3) % 4]);
                var (oa, ob) = x == pa ? (w, z) : (z, w);
                ring[f] = i;
                Cut(pa, pb);
                Cut(oa, ob);
                var across = byEdge[SubD.Key(z, w)].Where(g => g != f).ToList();
                if (across.Count != 1) return;
                (f, pa, pb) = (across[0], oa, ob);
            }
        }
        Cut(a, b);
        foreach (var f in first) Walk(f, a, b);
        var faces = new List<int[]>();
        for (var f = 0; f < c.Faces.Length; f++)
        {
            var loop = c.Faces[f];
            if (ring.TryGetValue(f, out var i))
            {
                var (x, y, z, w) = (loop[i], loop[(i + 1) % 4], loop[(i + 2) % 4], loop[(i + 3) % 4]);
                var (mxy, mzw) = (split[SubD.Key(x, y)], split[SubD.Key(z, w)]);
                faces.Add([x, mxy, mzw, w]);
                faces.Add([mxy, y, z, mzw]);
                continue;
            }
            // A face beside the ring that is not cut by it takes the new points on its edges.
            var g = new List<int>(loop.Length + 2);
            for (var j = 0; j < loop.Length; j++)
            {
                g.Add(loop[j]);
                if (split.TryGetValue(SubD.Key(loop[j], loop[(j + 1) % loop.Length]), out var m)) g.Add(m);
            }
            faces.Add([.. g]);
        }
        var creases = new List<SubDCrease>();
        foreach (var x in c.Creases)
        {
            if (split.TryGetValue(SubD.Key(x.A, x.B), out var m)) { creases.Add(x with { B = m }); creases.Add(x with { A = m }); }
            else creases.Add(x);
        }
        var cage = new SubDCage(pts, faces, creases);
        return SubD.Problem(cage) is { } p ? (null, p) : (cage, null);
    }

    /// <summary>Faces deleted (the cage opens there); the points no face uses any more go.</summary>
    public static (SubDCage? Cage, string? Problem) DeleteFaces(SubDCage c, IReadOnlyCollection<int> faces)
    {
        var gone = faces.ToHashSet();
        if (gone.Count == 0) return (null, "Pick the faces to delete.");
        if (gone.Any(f => f < 0 || f >= c.Faces.Length)) return (null, "A face to delete is not in the cage.");
        if (gone.Count >= c.Faces.Length) return (null, "That would leave no face: delete the solid instead.");
        var cage = Compact(new SubDCage(c.Points, c.Faces.Where((_, i) => !gone.Contains(i)).ToList(), c.Creases));
        return SubD.Problem(cage) is { } p ? (null, p) : (cage, null);
    }

    /// <summary>Creases of <paramref name="sharpness"/> set on edges (0 takes them off).</summary>
    public static (SubDCage? Cage, string? Problem) SetCreases(SubDCage c, IReadOnlyCollection<(int A, int B)> edges, double sharpness)
    {
        if (edges.Count == 0) return (null, "Pick the edges to crease.");
        if (!(sharpness >= 0)) return (null, "A crease's sharpness is 0 (smooth) or more.");
        var known = c.Edges().Select(x => SubD.Key(x.A, x.B)).ToHashSet();
        var set = new HashSet<long>();
        foreach (var (a, b) in edges)
        {
            if (!known.Contains(SubD.Key(a, b))) return (null, $"The cage has no edge {a}–{b}.");
            set.Add(SubD.Key(a, b));
        }
        var creases = c.Creases.Where(x => !set.Contains(SubD.Key(x.A, x.B))).ToList();
        if (sharpness > 0) creases.AddRange(edges.Select(x => new SubDCrease(Math.Min(x.A, x.B), Math.Max(x.A, x.B), Math.Min(sharpness, SubD.Infinite))).DistinctBy(x => SubD.Key(x.A, x.B)));
        return (new SubDCage(c.Points, c.Faces, creases), null);
    }
}
