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
/// condition), is not done; open edges hold their place by a plane across them.
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

    /// <summary>The levels worth having (finest first); null when none is.</summary>
    public static IReadOnlyList<Level>? Levels(TriMesh m)
    {
        var tris = m.Indices.Length / 3;
        if (tris < From) return null;
        var s = new State(m.Positions, m.Indices);
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

    sealed class State
    {
        readonly double[] _p;          // x y z per welded vertex
        readonly double[] _p0;         // where each was before any collapse
        readonly int[] _into;          // the vertex each was collapsed into (itself while it stands)
        readonly double[] _q;          // the quadric per vertex: a² ab ac ad b² bc bd c² cd d², weighted by area
        readonly double[] _w;          // the area of the triangles in each vertex's quadric
        readonly int[] _f;             // a b c per triangle
        readonly bool[] _dead;
        readonly bool[] _gone;         // a vertex collapsed into another
        readonly int[] _stamp;
        readonly List<int>[] _faces;   // the triangles at each vertex (dead ones dropped as met)
        readonly PriorityQueue<(int U, int V, int Su, int Sv), double> _heap = new();
        public int Alive;
        public readonly double Diagonal;

        public State(float[] positions, int[] indices)
        {
            // Welded by position: a flat-shaded part repeats each corner per face.
            var at = new Dictionary<(float, float, float), int>();
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
            _gone = new bool[n];
            _stamp = new int[n];
            _q = new double[10 * n];
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
            // Each triangle's plane on its corners; each open edge's plane across it, heavier, so the outline stays.
            var onEdge = new Dictionary<long, int>(EdgeKeys.Comparer);
            for (var t = 0; t < T; t++)
            {
                for (var e = 0; e < 3; e++)
                {
                    _faces[_f[3 * t + e]].Add(t);
                    var key = EdgeKeys.Undirected(_f[3 * t + e], _f[3 * t + (e + 1) % 3]);
                    onEdge[key] = onEdge.TryGetValue(key, out var c) ? c + 1 : 1;
                }
                if (Normal(t, -1, 0, 0, 0, 0) is not { } nn) continue;
                var (nx, ny, nz) = nn;
                var a = _f[3 * t];
                var d = -(nx * _p[3 * a] + ny * _p[3 * a + 1] + nz * _p[3 * a + 2]);
                var area = Area(t);
                for (var e = 0; e < 3; e++) { AddPlane(_f[3 * t + e], nx, ny, nz, d, area); _w[_f[3 * t + e]] += area; }
            }
            for (var t = 0; t < T; t++)
            {
                if (Normal(t, -1, 0, 0, 0, 0) is not { } nn) continue;
                for (var e = 0; e < 3; e++)
                {
                    int u = _f[3 * t + e], v = _f[3 * t + (e + 1) % 3];
                    if (onEdge[EdgeKeys.Undirected(u, v)] != 1) continue;
                    double ex = _p[3 * v] - _p[3 * u], ey = _p[3 * v + 1] - _p[3 * u + 1], ez = _p[3 * v + 2] - _p[3 * u + 2];
                    // Across the edge: perpendicular to both the edge and the triangle.
                    double cx = ey * nn.Z - ez * nn.Y, cy = ez * nn.X - ex * nn.Z, cz = ex * nn.Y - ey * nn.X;
                    var len = Math.Sqrt(cx * cx + cy * cy + cz * cz);
                    if (len < 1e-18) continue;
                    (cx, cy, cz) = (cx / len, cy / len, cz / len);
                    var d = -(cx * _p[3 * u] + cy * _p[3 * u + 1] + cz * _p[3 * u + 2]);
                    // As heavy as a strip of ten times the edge's length beside it.
                    var w = 10 * (ex * ex + ey * ey + ez * ez);
                    AddPlane(u, cx, cy, cz, d, w); AddPlane(v, cx, cy, cz, d, w);
                }
            }
            foreach (var key in onEdge.Keys) { var (u, v) = EdgeKeys.Ends(key); Push(u, v); }
        }

        void AddPlane(int v, double a, double b, double c, double d, double w)
        {
            var q = _q.AsSpan(10 * v, 10);
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

        /// <summary>The mean squared distance from x y z to the planes of u's and v's triangles, by their area.</summary>
        double Cost(int u, int v, double x, double y, double z)
        {
            double C(int k) => _q[10 * u + k] + _q[10 * v + k];
            var e = C(0) * x * x + 2 * C(1) * x * y + 2 * C(2) * x * z + 2 * C(3) * x + C(4) * y * y
                + 2 * C(5) * y * z + 2 * C(6) * y + C(7) * z * z + 2 * C(8) * z + C(9);
            return e / Math.Max(_w[u] + _w[v], 1e-18);
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

        /// <summary>Collapses edges while the cheapest costs at most <paramref name="limit"/>.</summary>
        public void Run(double limit)
        {
            var seen = new HashSet<int>();
            while (_heap.TryPeek(out var top, out var cost) && cost <= limit)
            {
                _heap.Dequeue();
                var (u, v, su, sv) = top;
                if (_gone[u] || _gone[v] || _stamp[u] != su || _stamp[v] != sv) continue;
                _faces[u].RemoveAll(t => _dead[t]);
                _faces[v].RemoveAll(t => _dead[t]);
                // The link condition: u and v share as many neighbours as triangles.
                var shared = 0;
                foreach (var t in _faces[u]) if (Has(t, v)) shared++;
                if (shared == 0) continue;
                seen.Clear();
                foreach (var t in _faces[u]) for (var k = 0; k < 3; k++) { var w = _f[3 * t + k]; if (w != u && w != v) seen.Add(w); }
                var common = 0;
                foreach (var t in _faces[v]) for (var k = 0; k < 3; k++) { var w = _f[3 * t + k]; if (w != u && w != v && seen.Remove(w)) common++; }
                if (common != shared) continue;
                var (x, y, z, c) = Target(u, v);
                if (c > limit) continue;
                // No triangle may fold over or lose its area.
                var ok = true;
                foreach (var list in (List<int>[])[_faces[u], _faces[v]])
                {
                    foreach (var t in list)
                    {
                        if (Has(t, u) && Has(t, v)) continue;
                        if (Normal(t, -1, 0, 0, 0, -1) is not { } before || Normal(t, u, x, y, z, v) is not { } after
                            || before.X * after.X + before.Y * after.Y + before.Z * after.Z < 0.3) { ok = false; break; }
                    }
                    if (!ok) break;
                }
                if (!ok) continue;
                // v into u.
                _p[3 * u] = x; _p[3 * u + 1] = y; _p[3 * u + 2] = z;
                for (var k = 0; k < 10; k++) _q[10 * u + k] += _q[10 * v + k];
                _w[u] += _w[v];
                foreach (var t in _faces[v])
                {
                    if (Has(t, u)) { _dead[t] = true; Alive--; continue; }
                    for (var k = 0; k < 3; k++) if (_f[3 * t + k] == v) _f[3 * t + k] = u;
                    _faces[u].Add(t);
                }
                _faces[v].Clear();
                _gone[v] = true;
                _into[v] = u;
                _stamp[u]++;
                _faces[u].RemoveAll(t => _dead[t]);
                seen.Clear();
                foreach (var t in _faces[u]) for (var k = 0; k < 3; k++) { var w = _f[3 * t + k]; if (w != u && seen.Add(w)) Push(u, w); }
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
        /// into (and their neighbours'), so a chord's sag across a curve counts, not only the corners' own moves.
        /// </summary>
        public double Stray()
        {
            var worst = 0.0;
            var near = new HashSet<int>();
            for (var v = 0; v < _into.Length; v++)
            {
                var r = Root(v);
                _faces[r].RemoveAll(t => _dead[t]);
                near.Clear();
                foreach (var t in _faces[r])
                    for (var k = 0; k < 3; k++) { var w = _f[3 * t + k]; _faces[w].RemoveAll(x => _dead[x]); foreach (var x in _faces[w]) near.Add(x); }
                if (near.Count == 0) continue;
                var best = double.MaxValue;
                foreach (var t in near) best = Math.Min(best, PointTriangle(_p0[3 * v], _p0[3 * v + 1], _p0[3 * v + 2], t));
                worst = Math.Max(worst, best);
            }
            return worst;
        }

        /// <summary>The distance from a point to triangle t (Ericson's closest point on a triangle).</summary>
        double PointTriangle(double px, double py, double pz, int t)
        {
            int ia = _f[3 * t], ib = _f[3 * t + 1], ic = _f[3 * t + 2];
            var a = new Vec3(_p[3 * ia], _p[3 * ia + 1], _p[3 * ia + 2]);
            var b = new Vec3(_p[3 * ib], _p[3 * ib + 1], _p[3 * ib + 2]);
            var c = new Vec3(_p[3 * ic], _p[3 * ic + 1], _p[3 * ic + 2]);
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
