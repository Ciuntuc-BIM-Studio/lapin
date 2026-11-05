namespace Lapin.Geometry;

/// <summary>
/// Coarser copies of a mesh for the screen (docs/plan-display-precision.md): the
/// interface draws one of them when the part is small enough on screen that the
/// difference is under a pixel or so. The model, its drawings, exports and
/// quantities never see them.
/// <para>
/// Garland and Heckbert's edge collapse by quadric error: the vertices welded,
/// each with the planes of its triangles; the edge whose collapse moves the
/// surface least goes first, to the best of its two ends and its middle. A
/// collapse that would fold a triangle over, or join two sheets (the link
/// condition), is not done. Open edges and folds (an arched opening's edge between
/// the wall's face and its reveal) hold their place by planes across them, counted
/// whole, not averaged by area with the faces beside them: averaged, a big flat face
/// let the arch's edge be cut into a few straight pieces (a tester's report, #70).
/// One pass makes every level: it stops at each level's error, keeps what it
/// has, and goes on.
/// </para>
/// </summary>
public static class MeshSimplify
{
    /// <summary>A coarser copy and the most the mesh's corners stray from it (m), measured.</summary>
    public sealed record Level(float Error, TriMesh Mesh);

    /// <summary>A part with fewer triangles is drawn as it is.</summary>
    public const int From = 256;

    /// <summary>Each level's error, as a share of the part's box diagonal.</summary>
    static readonly double[] Shares = [1.0 / 400, 1.0 / 100, 1.0 / 25];

    /// <summary>A level is kept only when it has at most this share of the last one's triangles.</summary>
    const double Worth = 0.6;

    /// <summary>The fold past which a level's edge is drawn (a coarse curve shows its facets less than its outline).</summary>
    const double CreaseDeg = 40;

    /// <summary>The fold past which an edge is held in place like an open one (the model's drawn edges: <see cref="Tessellator.CreaseDeg"/>).</summary>
    const double FeatureDeg = Tessellator.CreaseDeg;

    /// <summary>The levels worth having (finest first); null when none is.</summary>
    public static IReadOnlyList<Level>? Levels(TriMesh m)
    {
        var tris = m.Indices.Length / 3;
        if (tris < From) return null;
        var s = new State(Array.ConvertAll(m.Positions, v => (double)v), m.Indices);
        if (s.Diagonal <= 0) return null;
        var o = new List<Level>();
        var last = s.Alive;
        foreach (var share in Shares)
        {
            var error = s.Diagonal * share;
            s.Run(error * error);
            if (s.Alive > last * Worth || s.Alive < 2) continue;
            // What it says it strays is measured, not the target: the quadric's error is a mean, the screen needs the most.
            o.Add(new Level((float)s.Stray(), s.Mesh()));
            last = s.Alive;
        }
        return o.Count > 0 ? o : null;
    }

    /// <summary>A mesh made coarser within an error: its welded corners (x y z each), its triangles, and the most it strays from the original, measured.</summary>
    public sealed record Reduced(double[] Positions, int[] Indices, double Stray);

    /// <summary>
    /// The mesh with as few triangles as it can have while no point of it strays more than <paramref name="maxError"/>
    /// (in the positions' unit) from the original, and no original corner from it: the collapses stop at that error, the
    /// result is measured, and when the measure is over (the quadric's error is a mean) it is done again from the start
    /// at half the error, then a quarter. Open edges and folds past <see cref="Tessellator.CreaseDeg"/> hold their place.
    /// Null when no triangle could go within the error (IFC optimisation: docs/ifc-optimize.md).
    /// </summary>
    public static Reduced? Reduce(double[] positions, int[] indices, double maxError)
    {
        if (!(maxError > 0) || indices.Length < 6) return null;
        for (var share = 1.0; share >= 0.5; share /= 2)
        {
            var s = new State(positions, indices, strict: true);
            var before = s.Alive;
            var limit = maxError * share;
            s.Run(limit * limit);
            if (s.Alive >= before) return null;
            var stray = s.Measure(maxError);
            var (p, f) = s.Indexed();
            if (stray <= maxError) return new Reduced(p, f, stray);
        }
        return null;
    }

    sealed class State
    {
        readonly double[] _p;          // x y z per welded vertex
        readonly double[] _p0;         // where each was before any collapse
        readonly int[] _into;          // the vertex each was collapsed into (itself while it stands)
        readonly double[] _q;          // the quadric per vertex: a² ab ac ad b² bc bd c² cd d², weighted by area
        readonly double[] _w;          // the area of the triangles in each vertex's quadric
        readonly double[] _e;          // the planes across each vertex's open edges and folds, one each, not averaged
        readonly int[] _f;             // a b c per triangle
        readonly int[] _f0;            // the triangles as they were
        readonly bool[] _dead;
        /// <summary>A triangle a collapse changed (a corner moved or replaced): only these differ from the original.</summary>
        readonly bool[] _touched;
        readonly bool[] _gone;         // a vertex collapsed into another
        readonly int[] _stamp;
        readonly List<int>[] _faces;   // the triangles at each vertex (dead ones dropped as met)
        readonly PriorityQueue<(int U, int V, int Su, int Sv), double> _heap = new();
        public int Alive;
        public readonly double Diagonal;

        /// <summary>
        /// Strict (<see cref="Reduce"/>): each triangle's plane counted whole, not by its area, and the cost their sum, not their
        /// mean, so a collapse's cost is at least its farthest plane's distance squared: what a tolerance needs, not a screen.
        /// </summary>
        readonly bool _strict;

        public State(double[] positions, int[] indices, bool strict = false)
        {
            _strict = strict;
            // Welded by position: a flat-shaded part repeats each corner per face.
            var at = new Dictionary<(double, double, double), int>();
            var map = new int[positions.Length / 3];
            var pts = new List<double>();
            for (var i = 0; i < map.Length; i++)
            {
                var key = (positions[3 * i], positions[3 * i + 1], positions[3 * i + 2]);
                if (!at.TryGetValue(key, out var k)) { k = at.Count; at[key] = k; pts.Add(key.Item1); pts.Add(key.Item2); pts.Add(key.Item3); }
                map[i] = k;
            }
            _p = [.. pts];
            _p0 = [.. pts];
            var n = at.Count;
            _into = new int[n];
            for (var i = 0; i < n; i++) _into[i] = i;
            var faces = new List<int>(indices.Length);
            for (var t = 0; t + 2 < indices.Length; t += 3)
            {
                int a = map[indices[t]], b = map[indices[t + 1]], c = map[indices[t + 2]];
                if (a != b && b != c && a != c) { faces.Add(a); faces.Add(b); faces.Add(c); }
            }
            _f = [.. faces];
            var T = _f.Length / 3;
            Alive = T;
            _dead = new bool[T];
            _touched = new bool[T];
            _gone = new bool[n];
            _stamp = new int[n];
            _q = new double[10 * n];
            _e = new double[10 * n];
            _w = new double[n];
            _faces = new List<int>[n];
            for (var i = 0; i < n; i++) _faces[i] = new List<int>(6);
            double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
            for (var i = 0; i < n; i++)
            {
                x0 = Math.Min(x0, _p[3 * i]); y0 = Math.Min(y0, _p[3 * i + 1]); z0 = Math.Min(z0, _p[3 * i + 2]);
                x1 = Math.Max(x1, _p[3 * i]); y1 = Math.Max(y1, _p[3 * i + 1]); z1 = Math.Max(z1, _p[3 * i + 2]);
            }
            Diagonal = n == 0 ? 0 : Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0));
            // Each triangle's plane on its corners; each open edge's and fold's plane across it, so the outline stays.
            var onEdge = new Dictionary<long, int>(EdgeKeys.Comparer);
            var firstOn = new Dictionary<long, int>(EdgeKeys.Comparer);
            for (var t = 0; t < T; t++)
            {
                for (var e = 0; e < 3; e++)
                {
                    _faces[_f[3 * t + e]].Add(t);
                    var key = EdgeKeys.Undirected(_f[3 * t + e], _f[3 * t + (e + 1) % 3]);
                    onEdge[key] = onEdge.TryGetValue(key, out var c) ? c + 1 : 1;
                    firstOn.TryAdd(key, t);
                }
                if (Normal(t, -1, 0, 0, 0, 0) is not { } nn) continue;
                var (nx, ny, nz) = nn;
                var a = _f[3 * t];
                var d = -(nx * _p[3 * a] + ny * _p[3 * a + 1] + nz * _p[3 * a + 2]);
                var area = Area(t);
                for (var e = 0; e < 3; e++) { AddPlane(_q, _f[3 * t + e], nx, ny, nz, d, _strict ? 1 : area); _w[_f[3 * t + e]] += area; }
            }
            _f0 = [.. _f];
            var fold = Math.Cos(FeatureDeg * Math.PI / 180);
            for (var t = 0; t < T; t++)
            {
                if (Normal(t, -1, 0, 0, 0, 0) is not { } nn) continue;
                for (var e = 0; e < 3; e++)
                {
                    int u = _f[3 * t + e], v = _f[3 * t + (e + 1) % 3];
                    var key = EdgeKeys.Undirected(u, v);
                    if (onEdge[key] == 2)
                    {
                        // A fold: the other triangle's normal turns from this one's by more than FeatureDeg.
                        var other = firstOn[key];
                        if (other == t) foreach (var o in _faces[u]) if (o != t && Has(o, v)) { other = o; break; }
                        if (other == t || Normal(other, -1, 0, 0, 0, 0) is not { } on || nn.X * on.X + nn.Y * on.Y + nn.Z * on.Z >= fold) continue;
                    }
                    double ex = _p[3 * v] - _p[3 * u], ey = _p[3 * v + 1] - _p[3 * u + 1], ez = _p[3 * v + 2] - _p[3 * u + 2];
                    // Across the edge: perpendicular to both the edge and the triangle.
                    double cx = ey * nn.Z - ez * nn.Y, cy = ez * nn.X - ex * nn.Z, cz = ex * nn.Y - ey * nn.X;
                    var len = Math.Sqrt(cx * cx + cy * cy + cz * cz);
                    if (len < 1e-18) continue;
                    (cx, cy, cz) = (cx / len, cy / len, cz / len);
                    var d = -(cx * _p[3 * u] + cy * _p[3 * u + 1] + cz * _p[3 * u + 2]);
                    AddPlane(_e, u, cx, cy, cz, d, 1); AddPlane(_e, v, cx, cy, cz, d, 1);
                }
            }
            foreach (var key in onEdge.Keys) { var (u, v) = EdgeKeys.Ends(key); Push(u, v); }
        }

        static void AddPlane(double[] into, int v, double a, double b, double c, double d, double w)
        {
            var q = into.AsSpan(10 * v, 10);
            q[0] += w * a * a; q[1] += w * a * b; q[2] += w * a * c; q[3] += w * a * d; q[4] += w * b * b;
            q[5] += w * b * c; q[6] += w * b * d; q[7] += w * c * c; q[8] += w * c * d; q[9] += w * d * d;
        }

        double Area(int t)
        {
            int a = _f[3 * t], b = _f[3 * t + 1], c = _f[3 * t + 2];
            double ax = _p[3 * b] - _p[3 * a], ay = _p[3 * b + 1] - _p[3 * a + 1], az = _p[3 * b + 2] - _p[3 * a + 2];
            double bx = _p[3 * c] - _p[3 * a], by = _p[3 * c + 1] - _p[3 * a + 1], bz = _p[3 * c + 2] - _p[3 * a + 2];
            double nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
            return Math.Sqrt(nx * nx + ny * ny + nz * nz) / 2;
        }

        /// <summary>
        /// The mean squared distance from x y z to the planes of u's and v's triangles, by their area, and the
        /// whole squared distance to the planes across their open edges and folds (at least the farthest one's).
        /// </summary>
        double Cost(int u, int v, double x, double y, double z)
        {
            static double Q(double[] q, int u, int v, double x, double y, double z)
            {
                double C(int k) => q[10 * u + k] + q[10 * v + k];
                return C(0) * x * x + 2 * C(1) * x * y + 2 * C(2) * x * z + 2 * C(3) * x + C(4) * y * y
                    + 2 * C(5) * y * z + 2 * C(6) * y + C(7) * z * z + 2 * C(8) * z + C(9);
            }
            return (_strict ? Q(_q, u, v, x, y, z) : Q(_q, u, v, x, y, z) / Math.Max(_w[u] + _w[v], 1e-18)) + Q(_e, u, v, x, y, z);
        }

        /// <summary>The best place for u and v together: u, v or their middle, and what it costs.</summary>
        (double X, double Y, double Z, double Cost) Target(int u, int v)
        {
            double ux = _p[3 * u], uy = _p[3 * u + 1], uz = _p[3 * u + 2], vx = _p[3 * v], vy = _p[3 * v + 1], vz = _p[3 * v + 2];
            var best = (ux, uy, uz, Cost(u, v, ux, uy, uz));
            var cv = Cost(u, v, vx, vy, vz);
            if (cv < best.Item4) best = (vx, vy, vz, cv);
            double mx = (ux + vx) / 2, my = (uy + vy) / 2, mz = (uz + vz) / 2;
            var cm = Cost(u, v, mx, my, mz);
            if (cm < best.Item4) best = (mx, my, mz, cm);
            return best;
        }

        void Push(int u, int v) => _heap.Enqueue((u, v, _stamp[u], _stamp[v]), Math.Max(0, Target(u, v).Cost));

        /// <summary>Triangle t's unit normal with vertex <paramref name="moved"/> (if one) at x y z; null when it has no area.</summary>
        (double X, double Y, double Z)? Normal(int t, int moved, double x, double y, double z, int also)
        {
            Span<double> c = stackalloc double[9];
            for (var k = 0; k < 3; k++)
            {
                var i = _f[3 * t + k];
                if (i == moved || i == also) { c[3 * k] = x; c[3 * k + 1] = y; c[3 * k + 2] = z; }
                else { c[3 * k] = _p[3 * i]; c[3 * k + 1] = _p[3 * i + 1]; c[3 * k + 2] = _p[3 * i + 2]; }
            }
            double ax = c[3] - c[0], ay = c[4] - c[1], az = c[5] - c[2], bx = c[6] - c[0], by = c[7] - c[1], bz = c[8] - c[2];
            double nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
            var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            return len < 1e-14 ? null : (nx / len, ny / len, nz / len);
        }

        bool Has(int t, int v) => _f[3 * t] == v || _f[3 * t + 1] == v || _f[3 * t + 2] == v;

        /// <summary>A vertex's triangles without the dead ones.</summary>
        void Compact(int v)
        {
            var list = _faces[v];
            var k = 0;
            for (var i = 0; i < list.Count; i++) if (!_dead[list[i]]) list[k++] = list[i];
            if (k < list.Count) list.RemoveRange(k, list.Count - k);
        }

        int[]? _mark;
        int _token;

        /// <summary>The original corners each standing corner holds (strict): itself, and those collapsed into it.</summary>
        List<int>[]? _members;

        /// <summary>At most this many original corners in one: past it the corner stays (the check below stays cheap).</summary>
        const int MaxMembers = 256;

        /// <summary>
        /// Whether every original corner u and v hold lies within <paramref name="d"/> of the triangles left round them with both
        /// at x y z: a collapse never takes away a part of the surface (a thin piece folded into a line), which planes alone allow.
        /// </summary>
        bool Near(int u, int v, double x, double y, double z, double d)
        {
            if (_members == null)
            {
                _members = new List<int>[_gone.Length];
                for (var i = 0; i < _members.Length; i++) _members[i] = [i];
            }
            if (_members[u].Count + _members[v].Count > MaxMembers) return false;
            Span<double> c = stackalloc double[9];
            var fan = new List<double>(48);
            foreach (var list in (ReadOnlySpan<List<int>>)[_faces[u], _faces[v]])
                foreach (var t in list)
                {
                    if (_dead[t] || (Has(t, u) && Has(t, v))) continue;
                    for (var k = 0; k < 3; k++)
                    {
                        var i = _f[3 * t + k];
                        if (i == u || i == v) { fan.Add(x); fan.Add(y); fan.Add(z); }
                        else { fan.Add(_p[3 * i]); fan.Add(_p[3 * i + 1]); fan.Add(_p[3 * i + 2]); }
                    }
                }
            if (fan.Count == 0) return false;
            var pts = fan.ToArray();
            var tris = new int[pts.Length / 3];
            for (var i = 0; i < tris.Length; i++) tris[i] = i;
            foreach (var list in (ReadOnlySpan<List<int>>)[_members[u], _members[v]])
                foreach (var m in list)
                {
                    var best = double.MaxValue;
                    for (var t = 0; t < tris.Length / 3 && best > d; t++) best = Math.Min(best, PointTriangle(_p0[3 * m], _p0[3 * m + 1], _p0[3 * m + 2], t, tris, pts));
                    if (best > d) return false;
                }
            return true;
        }

        /// <summary>Whether no triangle round <paramref name="v"/> (but those on the edge) folds over or loses its area with v at x y z.</summary>
        bool Keeps(int v, int u, int w, double x, double y, double z)
        {
            foreach (var t in _faces[v])
            {
                if (Has(t, u) && Has(t, w)) continue;
                if (Normal(t, -1, 0, 0, 0, -1) is not { } before || Normal(t, u, x, y, z, w) is not { } after
                    || before.X * after.X + before.Y * after.Y + before.Z * after.Z < 0.3) return false;
            }
            return true;
        }

        /// <summary>Collapses edges while the cheapest costs at most <paramref name="limit"/>.</summary>
        public void Run(double limit)
        {
            _mark ??= new int[_gone.Length];
            while (_heap.TryPeek(out var top, out var cost) && cost <= limit)
            {
                _heap.Dequeue();
                var (u, v, su, sv) = top;
                if (_gone[u] || _gone[v] || _stamp[u] != su || _stamp[v] != sv) continue;
                Compact(u);
                Compact(v);
                // The link condition: u and v share as many neighbours as triangles.
                var shared = 0;
                foreach (var t in _faces[u]) if (Has(t, v)) shared++;
                if (shared == 0) continue;
                if (++_token == int.MaxValue) { Array.Clear(_mark); _token = 1; }
                foreach (var t in _faces[u]) for (var k = 0; k < 3; k++) { var w = _f[3 * t + k]; if (w != u && w != v) _mark[w] = _token; }
                var common = 0;
                if (++_token == int.MaxValue) { Array.Clear(_mark); _token = 1; }
                var second = _token;
                foreach (var t in _faces[v])
                    for (var k = 0; k < 3; k++)
                    {
                        var w = _f[3 * t + k];
                        if (w != u && w != v && _mark[w] == second - 1) { _mark[w] = second; common++; }
                    }
                if (common != shared) continue;
                var (x, y, z, c) = Target(u, v);
                if (c > limit) continue;
                // No triangle may fold over or lose its area.
                if (!Keeps(u, u, v, x, y, z) || !Keeps(v, u, v, x, y, z)) continue;
                // Strict: the triangles left round the new corner stay on the original surface (corners, edge middles, centres).
                if (_strict && !Near(u, v, x, y, z, Math.Sqrt(limit))) continue;
                // v into u.
                _p[3 * u] = x; _p[3 * u + 1] = y; _p[3 * u + 2] = z;
                for (var k = 0; k < 10; k++) { _q[10 * u + k] += _q[10 * v + k]; _e[10 * u + k] += _e[10 * v + k]; }
                _w[u] += _w[v];
                foreach (var t in _faces[v])
                {
                    if (Has(t, u)) { _dead[t] = true; Alive--; continue; }
                    for (var k = 0; k < 3; k++) if (_f[3 * t + k] == v) _f[3 * t + k] = u;
                    _faces[u].Add(t);
                }
                _faces[v].Clear();
                if (_members != null) { _members[u].AddRange(_members[v]); _members[v].Clear(); }
                _gone[v] = true;
                _into[v] = u;
                _stamp[u]++;
                foreach (var t in _faces[u]) _touched[t] = true;
                Compact(u);
                if (++_token == int.MaxValue) { Array.Clear(_mark); _token = 1; }
                foreach (var t in _faces[u]) for (var k = 0; k < 3; k++) { var w = _f[3 * t + k]; if (w != u && _mark[w] != _token) { _mark[w] = _token; Push(u, w); } }
            }
        }

        int Root(int v)
        {
            var r = v;
            while (_into[r] != r) r = _into[r];
            while (_into[v] != r) { var next = _into[v]; _into[v] = r; v = next; }
            return r;
        }

        /// <summary>
        /// The most an original corner lies from what is left: each measured to the triangles round the vertex it went
        /// into, so a chord's sag across a curve counts, not only the corners' own moves; and the most what is left lies
        /// from the original (its triangles' corners, middles of edges and centres, each to the original triangles round
        /// the corners that went into its own): a face spread over a hole holds every original corner, yet strays.
        /// </summary>
        public double Stray()
        {
            var worst = Back();
            for (var v = 0; v < _into.Length; v++)
            {
                var r = Root(v);
                var best = double.MaxValue;
                foreach (var t in _faces[r])
                {
                    if (_dead[t]) continue;
                    best = Math.Min(best, PointTriangle(_p0[3 * v], _p0[3 * v + 1], _p0[3 * v + 2], t));
                    if (best <= worst) break;
                }
                if (best < double.MaxValue) worst = Math.Max(worst, best);
            }
            return worst;
        }

        Grid? _grid;

        /// <summary>
        /// The most what is left and the original lie apart where a collapse changed them: each triangle changed or gone sampled
        /// at its corners, edge middles and centres and measured to the other's nearest triangle (what no collapse touched is
        /// the original's own). Past <paramref name="enough"/> the search stops: the answer is over.
        /// </summary>
        public double Measure(double enough)
        {
            _grid ??= new Grid(_f0, _p0);
            var alive = new List<int>(Alive * 3);
            for (var t = 0; t < _dead.Length; t++) if (!_dead[t]) { alive.Add(_f[3 * t]); alive.Add(_f[3 * t + 1]); alive.Add(_f[3 * t + 2]); }
            if (alive.Count == 0) return double.MaxValue;
            var left = new Grid([.. alive], _p);
            var worst = 0.0;
            Span<double> s = stackalloc double[21];
            void Samples(int[] f, double[] p, int t, Span<double> into)
            {
                int a = f[3 * t], b = f[3 * t + 1], c = f[3 * t + 2];
                for (var k = 0; k < 3; k++)
                {
                    double pa = p[3 * a + k], pb = p[3 * b + k], pc = p[3 * c + k];
                    into[k] = pa; into[3 + k] = pb; into[6 + k] = pc;
                    into[9 + k] = (pa + pb) / 2; into[12 + k] = (pb + pc) / 2; into[15 + k] = (pc + pa) / 2; into[18 + k] = (pa + pb + pc) / 3;
                }
            }
            for (var t = 0; t < _dead.Length; t++)
            {
                if (!_dead[t] && !_touched[t]) continue;
                // What is left, to the original; the original, to what is left.
                if (!_dead[t])
                {
                    Samples(_f, _p, t, s);
                    for (var i = 0; i < 7; i++) { worst = Math.Max(worst, _grid.Distance(s[3 * i], s[3 * i + 1], s[3 * i + 2], worst)); if (worst > enough) return worst; }
                }
                Samples(_f0, _p0, t, s);
                for (var i = 0; i < 7; i++) { worst = Math.Max(worst, left.Distance(s[3 * i], s[3 * i + 1], s[3 * i + 2], worst)); if (worst > enough) return worst; }
            }
            return worst;
        }

        /// <summary>The most a point of what is left lies from the original surface.</summary>
        double Back()
        {
            _grid ??= new Grid(_f0, _p0);
            var worst = 0.0;
            Span<double> s = stackalloc double[21];
            for (var t = 0; t < _dead.Length; t++)
            {
                if (_dead[t]) continue;
                int a = _f[3 * t], b = _f[3 * t + 1], c = _f[3 * t + 2];
                for (var k = 0; k < 3; k++)
                {
                    double pa = _p[3 * a + k], pb = _p[3 * b + k], pc = _p[3 * c + k];
                    s[k] = pa; s[3 + k] = pb; s[6 + k] = pc;
                    s[9 + k] = (pa + pb) / 2; s[12 + k] = (pb + pc) / 2; s[15 + k] = (pc + pa) / 2; s[18 + k] = (pa + pb + pc) / 3;
                }
                for (var i = 0; i < 7; i++) worst = Math.Max(worst, _grid.Distance(s[3 * i], s[3 * i + 1], s[3 * i + 2], worst));
            }
            return worst;
        }

        /// <summary>The original triangles in a grid of cells, for the distance from a point to the nearest.</summary>
        sealed class Grid
        {
            readonly int[] _f;
            readonly double[] _p;
            readonly double _x0, _y0, _z0, _h;
            readonly int _nx, _ny, _nz;
            readonly Dictionary<long, List<int>> _cells = [];
            readonly int[] _seen;
            int _token;

            public Grid(int[] f, double[] p)
            {
                (_f, _p) = (f, p);
                var T = f.Length / 3;
                _seen = new int[T];
                double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
                for (var i = 0; i < p.Length; i += 3)
                {
                    x0 = Math.Min(x0, p[i]); y0 = Math.Min(y0, p[i + 1]); z0 = Math.Min(z0, p[i + 2]);
                    x1 = Math.Max(x1, p[i]); y1 = Math.Max(y1, p[i + 1]); z1 = Math.Max(z1, p[i + 2]);
                }
                (_x0, _y0, _z0) = (x0, y0, z0);
                // About as many cells along the box's longest side as the cube root of the triangles, at most 256.
                var span = Math.Max(Math.Max(x1 - x0, y1 - y0), Math.Max(z1 - z0, 1e-9));
                _h = span / Math.Clamp(Math.Ceiling(Math.Pow(T, 1.0 / 3) * 2), 1, 256);
                (_nx, _ny, _nz) = (Cell(x1, x0) + 1, Cell(y1, y0) + 1, Cell(z1, z0) + 1);
                for (var t = 0; t < T; t++)
                {
                    int a = 3 * f[3 * t], b = 3 * f[3 * t + 1], c = 3 * f[3 * t + 2];
                    int i0 = Cell(Math.Min(p[a], Math.Min(p[b], p[c])), x0), i1 = Cell(Math.Max(p[a], Math.Max(p[b], p[c])), x0);
                    int j0 = Cell(Math.Min(p[a + 1], Math.Min(p[b + 1], p[c + 1])), y0), j1 = Cell(Math.Max(p[a + 1], Math.Max(p[b + 1], p[c + 1])), y0);
                    int k0 = Cell(Math.Min(p[a + 2], Math.Min(p[b + 2], p[c + 2])), z0), k1 = Cell(Math.Max(p[a + 2], Math.Max(p[b + 2], p[c + 2])), z0);
                    for (var i = i0; i <= i1; i++) for (var j = j0; j <= j1; j++) for (var k = k0; k <= k1; k++)
                    {
                        var key = Key(i, j, k);
                        if (!_cells.TryGetValue(key, out var list)) _cells[key] = list = [];
                        list.Add(t);
                    }
                }
            }

            int Cell(double v, double v0) => (int)Math.Floor((v - v0) / _h);
            static long Key(int i, int j, int k) => ((long)i * 1_000_003 + j) * 1_000_003 + k;

            /// <summary>The distance from x y z to the nearest triangle; any answer at most <paramref name="enough"/> is as good (the search stops).</summary>
            public double Distance(double x, double y, double z, double enough)
            {
                if (++_token == int.MaxValue) { Array.Clear(_seen); _token = 1; }
                int ci = Math.Clamp(Cell(x, _x0), 0, _nx - 1), cj = Math.Clamp(Cell(y, _y0), 0, _ny - 1), ck = Math.Clamp(Cell(z, _z0), 0, _nz - 1);
                var best = double.MaxValue;
                var reach = Math.Max(_nx, Math.Max(_ny, _nz));
                for (var r = 0; r <= reach; r++)
                {
                    // Every cell on the ring r cells out (a cube's shell), each triangle once.
                    for (var i = ci - r; i <= ci + r; i++)
                    for (var j = cj - r; j <= cj + r; j++)
                    for (var k = ck - r; k <= ck + r; k++)
                    {
                        if (Math.Max(Math.Abs(i - ci), Math.Max(Math.Abs(j - cj), Math.Abs(k - ck))) != r) continue;
                        if (!_cells.TryGetValue(Key(i, j, k), out var list)) continue;
                        foreach (var t in list)
                        {
                            if (_seen[t] == _token) continue;
                            _seen[t] = _token;
                            best = Math.Min(best, PointTriangle(x, y, z, t, _f, _p));
                        }
                    }
                    // Past this ring a triangle is at least r cells away (the point may lie outside the box: its cell is clamped).
                    if (best <= enough || best <= r * _h) break;
                }
                return best;
            }
        }

        /// <summary>The distance from a point to triangle t (Ericson's closest point on a triangle).</summary>
        double PointTriangle(double px, double py, double pz, int t) => PointTriangle(px, py, pz, t, _f, _p);

        /// <summary>The distance from a point to triangle t of <paramref name="f"/> over the points <paramref name="pts"/>.</summary>
        static double PointTriangle(double px, double py, double pz, int t, int[] f, double[] pts)
        {
            int ia = f[3 * t], ib = f[3 * t + 1], ic = f[3 * t + 2];
            var a = new Vec3(pts[3 * ia], pts[3 * ia + 1], pts[3 * ia + 2]);
            var b = new Vec3(pts[3 * ib], pts[3 * ib + 1], pts[3 * ib + 2]);
            var c = new Vec3(pts[3 * ic], pts[3 * ic + 1], pts[3 * ic + 2]);
            var p = new Vec3(px, py, pz);
            Vec3 ab = b - a, ac = c - a, ap = p - a;
            double d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return (p - a).Length;
            var bp = p - b;
            double d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return (p - b).Length;
            var vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) return (p - (a + ab * (d1 / (d1 - d3)))).Length;
            var cp = p - c;
            double d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return (p - c).Length;
            var vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) return (p - (a + ac * (d2 / (d2 - d6)))).Length;
            var va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return (p - (b + (c - b) * ((d4 - d3) / (d4 - d3 + (d5 - d6))))).Length;
            var den = 1 / (va + vb + vc);
            return (p - (a + ab * (vb * den) + ac * (vc * den))).Length;
        }

        /// <summary>What is left as an indexed mesh: the corners still standing (in their first use's order) and the live triangles.</summary>
        public (double[] Positions, int[] Indices) Indexed()
        {
            var remap = new int[_gone.Length];
            Array.Fill(remap, -1);
            var pos = new List<double>();
            var idx = new List<int>(Alive * 3);
            for (var t = 0; t < _dead.Length; t++)
            {
                if (_dead[t]) continue;
                for (var k = 0; k < 3; k++)
                {
                    var v = _f[3 * t + k];
                    if (remap[v] < 0) { remap[v] = pos.Count / 3; pos.Add(_p[3 * v]); pos.Add(_p[3 * v + 1]); pos.Add(_p[3 * v + 2]); }
                    idx.Add(remap[v]);
                }
            }
            return ([.. pos], [.. idx]);
        }

        /// <summary>
        /// What is left, with its open edges and its folds past <see cref="CreaseDeg"/>: three corners per triangle and
        /// nothing else (no normals, no indices), so a copy weighs half; the interface shades each triangle flat.
        /// </summary>
        public TriMesh Mesh()
        {
            var pos = new List<float>(Alive * 9);
            var onEdge = new Dictionary<long, (int First, int Second)>(EdgeKeys.Comparer);
            var normals = new Dictionary<int, (double X, double Y, double Z)>();
            for (var t = 0; t < _dead.Length; t++)
            {
                if (_dead[t] || Normal(t, -1, 0, 0, 0, -1) is not { } n) continue;
                normals[t] = n;
                for (var k = 0; k < 3; k++)
                {
                    var i = _f[3 * t + k];
                    pos.Add((float)_p[3 * i]); pos.Add((float)_p[3 * i + 1]); pos.Add((float)_p[3 * i + 2]);
                    var key = EdgeKeys.Undirected(i, _f[3 * t + (k + 1) % 3]);
                    onEdge[key] = onEdge.TryGetValue(key, out var had) ? (had.First, had.Second < 0 ? t : had.Second) : (t, -1);
                }
            }
            var cos = Math.Cos(CreaseDeg * Math.PI / 180);
            var edges = new List<float>();
            foreach (var (key, (first, second)) in onEdge)
            {
                if (second >= 0) { var (p, q) = (normals[first], normals[second]); if (p.X * q.X + p.Y * q.Y + p.Z * q.Z >= cos) continue; }
                var (a, b) = EdgeKeys.Ends(key);
                edges.Add((float)_p[3 * a]); edges.Add((float)_p[3 * a + 1]); edges.Add((float)_p[3 * a + 2]);
                edges.Add((float)_p[3 * b]); edges.Add((float)_p[3 * b + 1]); edges.Add((float)_p[3 * b + 2]);
            }
            return new TriMesh([.. pos], [], [], [.. edges]);
        }
    }
}
