using Lapin.Geometry;

namespace Lapin.Analysis.Solver;

/// <summary>A piece of a shell to mesh: its corners (nodes of the model, in order round it, convex and plane) and its surface (its element's), and the normal its faces take (null: the corners' own turn).</summary>
public sealed record MeshPiece(IReadOnlyList<int> Corners, string Group, Vec3? Normal = null);

/// <summary>The faces of a meshed piece: its index, and its 3 or 4 nodes, counter-clockwise about the piece's normal.</summary>
public sealed record MeshFace(int Piece, int[] Nodes);

/// <summary>What <see cref="ShellMesher"/> makes: the model's nodes followed by the new ones, the faces, and the edges' subdivisions.</summary>
public sealed class ShellMesh
{
    public required List<Vec3> Nodes { get; init; }
    public required List<MeshFace> Faces { get; init; }
    /// <summary>How many of <see cref="Nodes"/> were the model's (they keep their indices).</summary>
    public int Given { get; init; }
    public int Quads => Faces.Count(f => f.Nodes.Length == 4);
    public int Triangles => Faces.Count(f => f.Nodes.Length == 3);
    internal Dictionary<(int, int), int[]> Edges { get; init; } = [];

    /// <summary>
    /// The mesh nodes on the straight line from node <paramref name="a"/> to node <paramref name="b"/>, from a to b (both
    /// included): where a bar lying along shell edges is cut so that its pieces share the shells' nodes.
    /// </summary>
    public List<int> NodesAlong(int a, int b, double tolerance = 1e-6)
    {
        var (pa, pb) = (Nodes[a], Nodes[b]);
        var d = pb - pa;
        var len = d.Length;
        var o = new List<(double T, int N)> { (0, a), (len, b) };
        if (len < tolerance) return [a];
        var dir = d * (1 / len);
        var seen = new HashSet<int> { a, b };
        foreach (var (key, inner) in Edges)
            foreach (var n in inner.Append(key.Item1).Append(key.Item2))
            {
                if (seen.Contains(n)) continue;
                var v = Nodes[n] - pa;
                var t = Vec3.Dot(v, dir);
                if (t <= tolerance || t >= len - tolerance || (v - dir * t).Length > tolerance) continue;
                seen.Add(n);
                o.Add((t, n));
            }
        return o.OrderBy(x => x.T).Select(x => x.N).ToList();
    }
}

/// <summary>
/// The mesher of Lapin's shells (docs/plan-fem.md §10.3): each convex plane piece of a slab or a wall (as
/// <c>ShellPieces</c> cuts them, without holes) into quadrilaterals, with triangles only where they do not pair. Every piece
/// edge is first cut at the hard points on it (bar nodes, other pieces' corners), then each part into equal segments no
/// longer than the target size; a part is cut the same way whichever piece asks, so pieces and bars sharing an edge share
/// its nodes. A four-cornered piece whose opposite sides have as many segments is meshed by transfinite interpolation (Gordon
/// and Hall, "Construction of curvilinear co-ordinate systems and applications to mesh generation", IJNME 7, 1973): all
/// quadrilaterals. Any other piece: a grid of inner points at the target size, a Delaunay triangulation of them and of its
/// boundary by Lawson's flips (Lawson, "Software for C¹ surface interpolation", 1977; the boundary, being convex, needs no
/// constraint), Laplacian smoothing that never folds a triangle, and the triangles paired into quadrilaterals best first
/// (the shape of each pair's quadrilateral: its angles' distance from right angles), as Lo and Lee's triangle-merging does
/// ("Generating quadrilateral elements on plane and over curved surfaces", Comput. Struct. 31, 1989). No general 3D mesher:
/// the pieces are plane and convex. Written for Lapin.
/// </summary>
public static class ShellMesher
{
    /// <summary>The worst quadrilateral two triangles may pair into: each angle within this share of 90° of a right angle.</summary>
    public const double MinQuality = 0.35;

    public static ShellMesh Mesh(IReadOnlyList<Vec3> nodes, IReadOnlyList<MeshPiece> pieces, IReadOnlyCollection<int> hard, double size, double tolerance = 1e-6)
    {
        if (!(size > 0)) throw new ArgumentOutOfRangeException(nameof(size));
        var all = nodes.ToList();
        var faces = new List<MeshFace>();
        var edges = new Dictionary<(int, int), int[]>();

        // The hard points in a grid, to find those on an edge.
        var cell = Math.Max(size, 0.25);
        var grid = new Dictionary<(long, long, long), List<int>>();
        (long, long, long) Cell(Vec3 p) => ((long)Math.Floor(p.X / cell), (long)Math.Floor(p.Y / cell), (long)Math.Floor(p.Z / cell));
        foreach (var h in hard.Concat(pieces.SelectMany(p => p.Corners)).Distinct())
        {
            var c = Cell(nodes[h]);
            if (!grid.TryGetValue(c, out var l)) grid[c] = l = [];
            l.Add(h);
        }
        List<int> HardOn(int a, int b)
        {
            var (pa, pb) = (all[a], all[b]);
            var d = pb - pa;
            var len = d.Length;
            var dir = d * (1 / len);
            var (c0, c1) = (Cell(new Vec3(Math.Min(pa.X, pb.X) - tolerance, Math.Min(pa.Y, pb.Y) - tolerance, Math.Min(pa.Z, pb.Z) - tolerance)),
                            Cell(new Vec3(Math.Max(pa.X, pb.X) + tolerance, Math.Max(pa.Y, pb.Y) + tolerance, Math.Max(pa.Z, pb.Z) + tolerance)));
            var o = new List<(double T, int N)>();
            // Long edges across many cells: walk the cells along the edge rather than the whole box.
            var cellsInBox = (c1.Item1 - c0.Item1 + 1) * (c1.Item2 - c0.Item2 + 1) * (c1.Item3 - c0.Item3 + 1);
            IEnumerable<(long, long, long)> cells = cellsInBox <= 4 * (len / cell + 2)
                ? Box(c0, c1)
                : Enumerable.Range(0, (int)(len / (cell / 2)) + 2).Select(s => pa + dir * Math.Min(len, s * cell / 2))
                    .SelectMany(q => Box(Cell(q - new Vec3(cell, cell, cell) * 0.5), Cell(q + new Vec3(cell, cell, cell) * 0.5))).Distinct();
            foreach (var c in cells)
            {
                if (!grid.TryGetValue(c, out var l)) continue;
                foreach (var h in l)
                {
                    if (h == a || h == b) continue;
                    var v = all[h] - pa;
                    var t = Vec3.Dot(v, dir);
                    if (t <= tolerance || t >= len - tolerance || (v - dir * t).Length > tolerance) continue;
                    o.Add((t, h));
                }
            }
            return o.OrderBy(x => x.T).Select(x => x.N).Distinct().ToList();
        }
        static IEnumerable<(long, long, long)> Box((long, long, long) c0, (long, long, long) c1)
        {
            for (var x = c0.Item1; x <= c1.Item1; x++)
                for (var y = c0.Item2; y <= c1.Item2; y++)
                    for (var z = c0.Item3; z <= c1.Item3; z++)
                        yield return (x, y, z);
        }
        // A part between two hard points, cut in equal segments: its inner nodes from the lower index to the higher.
        int[] Part(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (!edges.TryGetValue(key, out var inner))
            {
                var (p0, p1) = (all[key.Item1], all[key.Item2]);
                var m = Math.Max(1, (int)Math.Ceiling((p1 - p0).Length / size - 1e-6));
                inner = new int[m - 1];
                for (var s = 1; s < m; s++)
                {
                    all.Add(p0 + (p1 - p0) * ((double)s / m));
                    inner[s - 1] = all.Count - 1;
                }
                edges[key] = inner;
            }
            return a < b ? inner : inner.Reverse().ToArray();
        }

        for (var pi = 0; pi < pieces.Count; pi++)
        {
            var piece = pieces[pi];
            if (piece.Corners.Count < 3) continue;
            // The piece's plane: its normal its own or the corners' turn, the corners taken round it counter-clockwise, x
            // along the longest side.
            var turn = VecMath.NewellNormal(piece.Corners.Select(c => all[c]).ToList());
            var normal = (piece.Normal ?? turn).Normalized();
            var corners = Vec3.Dot(turn, normal) < 0 ? piece.Corners.Reverse().ToList() : piece.Corners.ToList();
            var pts = corners.Select(c => all[c]).ToList();
            var longest = Enumerable.Range(0, corners.Count).MaxBy(i => (pts[(i + 1) % pts.Count] - pts[i]).Length);
            var ex = pts[(longest + 1) % pts.Count] - pts[longest];
            ex = (ex - normal * Vec3.Dot(ex, normal)).Normalized();
            var ey = Vec3.Cross(normal, ex);
            var origin = pts[0];
            Vec2 To2(Vec3 p) => new(Vec3.Dot(p - origin, ex), Vec3.Dot(p - origin, ey));
            Vec3 To3(Vec2 q) => origin + ex * q.X + ey * q.Y;

            // The boundary: each side from its corner (its end left to the next), through its hard points and its parts' nodes.
            var sides = new List<List<int>>();
            for (var i = 0; i < corners.Count; i++)
            {
                var (a, b) = (corners[i], corners[(i + 1) % corners.Count]);
                var chain = new List<int> { a };
                chain.AddRange(HardOn(a, b));
                chain.Add(b);
                var side = new List<int> { a };
                for (var x = 0; x + 1 < chain.Count; x++)
                {
                    side.AddRange(Part(chain[x], chain[x + 1]));
                    side.Add(chain[x + 1]);
                }
                side.RemoveAt(side.Count - 1);
                sides.Add(side);
            }

            List<int[]>? made = null;
            if (corners.Count == 4 && sides[0].Count == sides[2].Count && sides[1].Count == sides[3].Count)
                made = Transfinite(sides, all, To2, To3);
            made ??= Unstructured(sides, all, To2, To3, size);
            foreach (var f in made) faces.Add(new MeshFace(pi, f));
        }
        return new ShellMesh { Nodes = all, Faces = faces, Given = nodes.Count, Edges = edges };
    }

    /// <summary>A four-sided piece with opposite sides cut alike: Coons' patch through its boundary nodes; null if a quadrilateral would fold.</summary>
    static List<int[]>? Transfinite(List<List<int>> sides, List<Vec3> all, Func<Vec3, Vec2> to2, Func<Vec2, Vec3> to3)
    {
        int m = sides[0].Count, n = sides[1].Count;
        var g = new int[m + 1, n + 1];
        var p = new Vec2[m + 1, n + 1];
        for (var i = 0; i < m; i++) { g[i, 0] = sides[0][i]; g[m - i, n] = sides[2][i]; }
        for (var j = 0; j < n; j++) { g[m, j] = sides[1][j]; g[0, n - j] = sides[3][j]; }
        for (var i = 0; i <= m; i++)
            for (var j = 0; j <= n; j++)
                if (i == 0 || j == 0 || i == m || j == n) p[i, j] = to2(all[g[i, j]]);
        var added = new List<(int I, int J)>();
        for (var i = 1; i < m; i++)
            for (var j = 1; j < n; j++)
            {
                double u = (double)i / m, v = (double)j / n;
                var q = p[i, 0] * (1 - v) + p[i, n] * v + p[0, j] * (1 - u) + p[m, j] * u
                        - (p[0, 0] * ((1 - u) * (1 - v)) + p[m, 0] * (u * (1 - v)) + p[m, n] * (u * v) + p[0, n] * ((1 - u) * v));
                p[i, j] = q;
                added.Add((i, j));
            }
        var faces = new List<int[]>();
        for (var i = 0; i < m; i++)
            for (var j = 0; j < n; j++)
                if (!Convex(p[i, j], p[i + 1, j], p[i + 1, j + 1], p[i, j + 1])) return null;
        foreach (var (i, j) in added) { all.Add(to3(p[i, j])); g[i, j] = all.Count - 1; }
        for (var i = 0; i < m; i++)
            for (var j = 0; j < n; j++)
                faces.Add([g[i, j], g[i + 1, j], g[i + 1, j + 1], g[i, j + 1]]);
        return faces;
    }

    static double Cross(Vec2 a, Vec2 b, Vec2 c) => Vec2.Cross(b - a, c - a);

    static bool Convex(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        var s = (b - a).Length + (c - b).Length + (d - c).Length + (a - d).Length;
        var eps = 1e-9 * s * s;
        return Cross(a, b, c) > eps && Cross(b, c, d) > eps && Cross(c, d, a) > eps && Cross(d, a, b) > eps;
    }

    /// <summary>A quadrilateral's shape: 1 with four right angles, 0 when an angle is flat or null.</summary>
    static double Quality(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        if (!Convex(a, b, c, d)) return 0;
        Vec2[] q = [a, b, c, d];
        var worst = 1.0;
        for (var i = 0; i < 4; i++)
        {
            var (u, v) = (q[(i + 3) % 4] - q[i], q[(i + 1) % 4] - q[i]);
            var ang = Math.Acos(Math.Clamp(Vec2.Dot(u, v) / (u.Length * v.Length), -1, 1));
            worst = Math.Min(worst, 1 - Math.Abs(ang - Math.PI / 2) / (Math.PI / 2));
        }
        return worst;
    }

    /// <summary>Any convex piece: its boundary and a grid of inner points triangulated (Delaunay), smoothed, paired into quadrilaterals.</summary>
    static List<int[]> Unstructured(List<List<int>> sides, List<Vec3> all, Func<Vec3, Vec2> to2, Func<Vec2, Vec3> to3, double size)
    {
        var boundary = sides.SelectMany(x => x).ToList();
        // Each boundary point's side of the piece (a corner starts its side and ends the one before).
        var side = sides.SelectMany((x, i) => x.Select(_ => i)).ToArray();
        var pts = boundary.Select(b => to2(all[b])).ToList();
        var nb = pts.Count;
        if (Area(pts) < 0) throw new InvalidOperationException("A piece turning against its normal.");
        // Inner points: a grid at the target size, centred in the piece, at least 0.45 size from its boundary; a tiny
        // deterministic shift keeps them off the triangulation's edges.
        var (minX, maxX, minY, maxY) = (pts.Min(q => q.X), pts.Max(q => q.X), pts.Min(q => q.Y), pts.Max(q => q.Y));
        var (nx, ny) = ((int)Math.Floor((maxX - minX) / size), (int)Math.Floor((maxY - minY) / size));
        var (ox, oy) = (minX + (maxX - minX - nx * size) / 2, minY + (maxY - minY - ny * size) / 2);
        for (var i = 0; i <= nx; i++)
            for (var j = 0; j <= ny; j++)
            {
                var q = new Vec2(ox + i * size + 1.3e-4 * size * ((i * 7 + j * 3) % 5 - 2), oy + j * size + 1.1e-4 * size * ((i * 3 + j * 5) % 7 - 3));
                if (Inside(pts, nb, q, 0.45 * size)) pts.Add(q);
            }

        var t = new Triangulation(pts);
        t.Ears(nb, side, sides.Count);
        t.Delaunay();
        for (var i = nb; i < pts.Count; i++) t.Insert(i);
        for (var pass = 0; pass < 4; pass++)
        {
            t.Smooth(nb);
            t.Delaunay();
        }
        var faces = t.Pair(MinQuality);
        // Back into the model: the boundary's nodes are the model's, the inner ones new.
        var index = new int[pts.Count];
        for (var i = 0; i < nb; i++) index[i] = boundary[i];
        for (var i = nb; i < pts.Count; i++) { all.Add(to3(t.Points[i])); index[i] = all.Count - 1; }
        return faces.Select(f => f.Select(v => index[v]).ToArray()).ToList();
    }

    static double Area(List<Vec2> p)
    {
        var a = 0.0;
        for (var i = 0; i < p.Count; i++) a += Vec2.Cross(p[i], p[(i + 1) % p.Count]);
        return a / 2;
    }

    /// <summary>Whether a point lies inside the convex boundary (its first nb points, counter-clockwise), at least <paramref name="margin"/> from each side.</summary>
    static bool Inside(List<Vec2> p, int nb, Vec2 q, double margin)
    {
        for (var i = 0; i < nb; i++)
        {
            var (a, b) = (p[i], p[(i + 1) % nb]);
            var d = b - a;
            var len = d.Length;
            if (len < 1e-12) continue;
            if (Vec2.Cross(d, q - a) / len < margin) return false;
        }
        return true;
    }

    /// <summary>A triangulation of points in the plane: each triangle counter-clockwise, with its neighbour across the side opposite each corner (−1: the boundary).</summary>
    sealed class Triangulation(List<Vec2> points)
    {
        public readonly List<Vec2> Points = points;
        readonly List<int[]> _v = [];
        readonly List<int[]> _n = [];
        int _last;

        double Scale => 1e-12 * Math.Max(1, Points.Max(p => Math.Abs(p.X) + Math.Abs(p.Y)));

        /// <summary>The convex boundary cut into triangles, best ear first (the largest smallest angle); its collinear points are never ears.</summary>
        public void Ears(int nb, int[] side, int count)
        {
            // Two ring points on one side of the piece: a chord between them would lie along it, over the points between.
            bool Corner(int x) => x == 0 || side[x - 1] != side[x];
            bool Has(int x, int s) => side[x] == s || (Corner(x) && (side[x] - 1 + count) % count == s);
            bool OneSide(int a, int b) => Has(b, side[a]) || (Corner(a) && Has(b, (side[a] - 1 + count) % count));
            var ring = Enumerable.Range(0, nb).ToList();
            var tris = new List<int[]>();
            while (ring.Count > 3)
            {
                var (best, quality) = (-1, -1.0);
                for (var i = 0; i < ring.Count; i++)
                {
                    var (ia, ic) = (ring[(i - 1 + ring.Count) % ring.Count], ring[(i + 1) % ring.Count]);
                    var (a, b, c) = (Points[ia], Points[ring[i]], Points[ic]);
                    if (Cross(a, b, c) <= Scale * ((b - a).Length + (c - b).Length)) continue;
                    if (ring.Count > 3 && OneSide(ia, ic)) continue;
                    var q = MinAngle(a, b, c);
                    if (q > quality) (best, quality) = (i, q);
                }
                if (best < 0) break;
                tris.Add([ring[(best - 1 + ring.Count) % ring.Count], ring[best], ring[(best + 1) % ring.Count]]);
                ring.RemoveAt(best);
            }
            if (ring.Count == 3 && Cross(Points[ring[0]], Points[ring[1]], Points[ring[2]]) > 0) tris.Add([ring[0], ring[1], ring[2]]);
            foreach (var tr in tris) { _v.Add(tr); _n.Add([-1, -1, -1]); }
            Link();
        }

        static double MinAngle(Vec2 a, Vec2 b, Vec2 c)
        {
            double Ang(Vec2 p, Vec2 q, Vec2 r) { var (u, v) = (q - p, r - p); return Math.Acos(Math.Clamp(Vec2.Dot(u, v) / (u.Length * v.Length), -1, 1)); }
            return Math.Min(Ang(a, b, c), Math.Min(Ang(b, c, a), Ang(c, a, b)));
        }

        void Link()
        {
            var side = new Dictionary<(int, int), (int T, int K)>();
            for (var t = 0; t < _v.Count; t++)
                for (var k = 0; k < 3; k++)
                {
                    var (a, b) = (_v[t][(k + 1) % 3], _v[t][(k + 2) % 3]);
                    if (side.TryGetValue((b, a), out var o)) { _n[t][k] = o.T; _n[o.T][o.K] = t; }
                    else side[(a, b)] = (t, k);
                }
        }

        bool InCircle(int t, Vec2 d)
        {
            var (a, b, c) = (Points[_v[t][0]] - d, Points[_v[t][1]] - d, Points[_v[t][2]] - d);
            var det = (a.X * a.X + a.Y * a.Y) * Vec2.Cross(b, c) - (b.X * b.X + b.Y * b.Y) * Vec2.Cross(a, c) + (c.X * c.X + c.Y * c.Y) * Vec2.Cross(a, b);
            var s = a.Length + b.Length + c.Length;
            return det > 1e-10 * s * s * s * s;
        }

        void Rotate(int t, int k)
        {
            if (k == 0) return;
            var (v, n) = (_v[t], _n[t]);
            _v[t] = [v[k], v[(k + 1) % 3], v[(k + 2) % 3]];
            _n[t] = [n[k], n[(k + 1) % 3], n[(k + 2) % 3]];
        }

        void Repoint(int t, int from, int to)
        {
            if (t < 0) return;
            for (var k = 0; k < 3; k++) if (_n[t][k] == from) _n[t][k] = to;
        }

        /// <summary>Whether the side opposite corner k of t is not Delaunay (and can be flipped: its quadrilateral convex).</summary>
        bool Illegal(int t, int k)
        {
            var u = _n[t][k];
            if (u < 0) return false;
            var (q, r) = (_v[t][(k + 1) % 3], _v[t][(k + 2) % 3]);
            var s = _v[u].First(x => x != q && x != r);
            if (!InCircle(t, Points[s])) return false;
            var p = _v[t][k];
            // Strictly convex, past round-off: a flip must never lay a side along collinear boundary points.
            var (pp, pq, pr, ps) = (Points[p], Points[q], Points[r], Points[s]);
            var span = (pq - pp).Length + (pr - pp).Length + (ps - pq).Length + (ps - pr).Length;
            var eps = 1e-10 * span * span;
            return Cross(pp, pq, ps) > eps && Cross(pp, ps, pr) > eps;
        }

        /// <summary>Flips the side opposite corner k of t; t and its neighbour become (p, q, s) and (p, s, r).</summary>
        (int, int) Flip(int t, int k)
        {
            Rotate(t, k);
            var u = _n[t][0];
            var (p, q, r) = (_v[t][0], _v[t][1], _v[t][2]);
            var ku = Array.FindIndex(_v[u], x => x != q && x != r);
            Rotate(u, ku);
            var s = _v[u][0];
            int nTq = _n[t][1], nTr = _n[t][2];
            // u = (s, r, q): opposite r the side (q, s), opposite q the side (s, r).
            int nUr = _n[u][1], nUq = _n[u][2];
            _v[t] = [p, q, s];
            _n[t] = [nUr, u, nTr];
            _v[u] = [p, s, r];
            _n[u] = [nUq, nTq, t];
            Repoint(nUr, u, t);
            Repoint(nTq, t, u);
            return (t, u);
        }

        /// <summary>Lawson's flips until every side is Delaunay.</summary>
        public void Delaunay()
        {
            var stack = new Stack<(int, int)>();
            for (var t = 0; t < _v.Count; t++) for (var k = 0; k < 3; k++) stack.Push((t, k));
            var guard = 50 * (_v.Count + 10) * 3;
            while (stack.Count > 0 && guard-- > 0)
            {
                var (t, k) = stack.Pop();
                if (!Illegal(t, k)) continue;
                var (a, b) = Flip(t, k);
                for (var x = 0; x < 3; x++) { stack.Push((a, x)); stack.Push((b, x)); }
            }
        }

        int Locate(Vec2 p)
        {
            var t = _last < _v.Count ? _last : 0;
            for (var steps = 0; steps < 4 * _v.Count + 10; steps++)
            {
                var moved = false;
                for (var k = 0; k < 3; k++)
                {
                    var (a, b) = (Points[_v[t][(k + 1) % 3]], Points[_v[t][(k + 2) % 3]]);
                    if (Cross(a, b, p) < 0 && _n[t][k] >= 0) { t = _n[t][k]; moved = true; break; }
                }
                if (!moved) return t;
            }
            for (var x = 0; x < _v.Count; x++)
                if (Enumerable.Range(0, 3).All(k => Cross(Points[_v[x][(k + 1) % 3]], Points[_v[x][(k + 2) % 3]], p) >= 0)) return x;
            return -1;
        }

        /// <summary>Inserts point i: the triangle holding it split in three, then the sides round it made Delaunay.</summary>
        public void Insert(int i)
        {
            var p = Points[i];
            var t = Locate(p);
            if (t < 0) return;
            var (a, b, c) = (_v[t][0], _v[t][1], _v[t][2]);
            // On a side (or as good as): nudged into the triangle.
            var eps = Scale * 10;
            if (Cross(Points[b], Points[c], p) <= eps || Cross(Points[c], Points[a], p) <= eps || Cross(Points[a], Points[b], p) <= eps)
            {
                var centre = (Points[a] + Points[b] + Points[c]) * (1.0 / 3);
                p = p + (centre - p) * 0.01;
                Points[i] = p;
                if (Cross(Points[b], Points[c], p) <= 0 || Cross(Points[c], Points[a], p) <= 0 || Cross(Points[a], Points[b], p) <= 0) return;
            }
            int na = _n[t][0], nB = _n[t][1], nc = _n[t][2];
            var t1 = _v.Count;
            var t2 = t1 + 1;
            _v[t] = [i, b, c]; _n[t] = [na, t1, t2];
            _v.Add([i, c, a]); _n.Add([nB, t2, t]);
            _v.Add([i, a, b]); _n.Add([nc, t, t1]);
            Repoint(nB, t, t1);
            Repoint(nc, t, t2);
            _last = t;
            var stack = new Stack<int>([t, t1, t2]);
            while (stack.Count > 0)
            {
                var x = stack.Pop();
                var k = Array.IndexOf(_v[x], i);
                if (k < 0 || !Illegal(x, k)) continue;
                var (f, g) = Flip(x, k);
                stack.Push(f);
                stack.Push(g);
            }
        }

        /// <summary>Each inner point to the mean of its neighbours, unless that would fold a triangle round it.</summary>
        public void Smooth(int nb)
        {
            var round = new List<int>[Points.Count];
            for (var x = 0; x < Points.Count; x++) round[x] = [];
            for (var t = 0; t < _v.Count; t++) foreach (var x in _v[t]) round[x].Add(t);
            for (var i = nb; i < Points.Count; i++)
            {
                if (round[i].Count == 0) continue;
                var nbrs = round[i].SelectMany(t => _v[t]).Where(x => x != i).Distinct().ToList();
                var target = nbrs.Aggregate(new Vec2(0, 0), (s, x) => s + Points[x]) * (1.0 / nbrs.Count);
                var old = Points[i];
                Points[i] = target;
                if (round[i].Any(t => Cross(Points[_v[t][0]], Points[_v[t][1]], Points[_v[t][2]]) <= Scale)) Points[i] = old;
            }
        }

        /// <summary>Pairs neighbouring triangles into quadrilaterals, best shape first, none worse than <paramref name="minQuality"/>; the rest stay triangles.</summary>
        public List<int[]> Pair(double minQuality)
        {
            var cands = new List<(double Q, int T, int U, int[] Quad)>();
            for (var t = 0; t < _v.Count; t++)
                for (var k = 0; k < 3; k++)
                {
                    var u = _n[t][k];
                    if (u < t) continue;
                    var (p, q, r) = (_v[t][k], _v[t][(k + 1) % 3], _v[t][(k + 2) % 3]);
                    var s = _v[u].First(x => x != q && x != r);
                    int[] quad = [p, q, s, r];
                    var qual = Quality(Points[p], Points[q], Points[s], Points[r]);
                    if (qual >= minQuality) cands.Add((qual, t, u, quad));
                }
            var used = new bool[_v.Count];
            var o = new List<int[]>();
            foreach (var c in cands.OrderByDescending(c => c.Q))
            {
                if (used[c.T] || used[c.U]) continue;
                used[c.T] = used[c.U] = true;
                o.Add(c.Quad);
            }
            for (var t = 0; t < _v.Count; t++) if (!used[t]) o.Add(_v[t]);
            return o;
        }
    }
}
