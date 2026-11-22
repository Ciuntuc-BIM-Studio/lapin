using System.Globalization;
using Lapin.Geometry;

namespace Lapin.Civil;

/// <summary>A triangulated irregular network: points (x, y in plan, z the elevation) and counter-clockwise triangles.</summary>
public sealed record Tin(IReadOnlyList<Vec3> Points, IReadOnlyList<(int A, int B, int C)> Triangles)
{
    /// <summary>Points the triangulations made on this thread have inserted, counted from its start: how much work a build took.</summary>
    [ThreadStatic] static long _inserted;

    /// <summary>Points the triangulations made on this thread have inserted (a measure of work, for tests and timing).</summary>
    public static long Inserted => _inserted;

    /// <summary>
    /// Delaunay in plan (Bowyer–Watson): points that coincide in plan (to 6
    /// decimals) count once, the first one kept; the triangles cover the convex
    /// hull, counter-clockwise seen from above. <paramref name="constraints"/>
    /// (pairs of input indices: a breakline's segments) are kept as edges
    /// exactly, split where a point lies on them; the rest stays Delaunay.
    /// </summary>
    public static Tin Delaunay(IReadOnlyList<Vec3> input, IReadOnlyList<(int A, int B)>? constraints = null) => Delaunay(input, constraints, null);

    /// <summary>
    /// <see cref="Delaunay(IReadOnlyList{Vec3}, IReadOnlyList{ValueTuple{int, int}}?)"/> clipped to <paramref name="boundary"/>
    /// in the same triangulation: its corners take the height the TIN of the points has there (outside it, the nearest
    /// point's), they are inserted after the points and its edges are constraints; the triangles outside it go, and the
    /// points no triangle uses. Kept whole when nothing would be left.
    /// </summary>
    public static Tin Delaunay(IReadOnlyList<Vec3> input, IReadOnlyList<(int A, int B)>? constraints, IReadOnlyList<Vec2>? boundary)
    {
        var seen = new Dictionary<(long, long), int>();
        var pts = new List<Vec3>();
        var map = new int[input.Count];
        static (long, long) Key(double x, double y) => ((long)Math.Round(x * 1e6), (long)Math.Round(y * 1e6));
        for (var k = 0; k < input.Count; k++)
        {
            var p = input[k];
            map[k] = -1;
            if (!(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z))) continue;
            var key = Key(p.X, p.Y);
            if (!seen.TryGetValue(key, out var at)) { seen[key] = at = pts.Count; pts.Add(p); }
            map[k] = at;
        }
        var n = pts.Count;
        if (n < 3) return new Tin(pts, []);
        var constraintList = constraints is { Count: > 0 }
            ? constraints.Where(c => c.A >= 0 && c.B >= 0 && c.A < map.Length && c.B < map.Length)
                .Select(c => (map[c.A], map[c.B])).Where(c => c.Item1 >= 0 && c.Item2 >= 0 && c.Item1 != c.Item2).ToList()
            : [];
        // The boundary's corners: a point already there is that point; the others come after the survey's.
        var clip = boundary is { Count: >= 3 } && boundary.All(v => double.IsFinite(v.X) && double.IsFinite(v.Y));
        var corner = new int[clip ? boundary!.Count : 0];
        if (clip)
            for (var i = 0; i < boundary!.Count; i++)
            {
                var key = Key(boundary[i].X, boundary[i].Y);
                if (!seen.TryGetValue(key, out var at)) { seen[key] = at = pts.Count; pts.Add(new Vec3(boundary[i].X, boundary[i].Y, double.NaN)); }
                corner[i] = at;
            }
        var total = pts.Count;
        var bw = new BowyerWatson(pts);
        bw.Insert(Enumerable.Range(0, n));
        var result = bw.Triangles();
        if (!clip)
        {
            if (constraintList.Count > 0) result = Constrained.Apply(pts, result, constraintList);
            return new Tin(pts, result);
        }
        // The heights of the new corners: the ground the points make there, breaklines held.
        if (total > n)
        {
            var survey = pts.GetRange(0, n);
            var baseTin = new Tin(survey, constraintList.Count > 0 ? Constrained.Apply(survey, result, constraintList) : result);
            if (baseTin.Triangles.Count == 0) return baseTin;
            var ground = new Terrain(baseTin);
            for (var i = n; i < total; i++)
            {
                var v = pts[i];
                var z = ground.ElevationAt(v.X, v.Y) ?? survey.MinBy(p => (p.X - v.X) * (p.X - v.X) + (p.Y - v.Y) * (p.Y - v.Y)).Z;
                pts[i] = v with { Z = z };
            }
            bw.Insert(Enumerable.Range(n, total - n));
            result = bw.Triangles();
        }
        else if (result.Count == 0) return new Tin(pts, result);
        var all = new List<(int, int)>(constraintList);
        for (var i = 0; i < corner.Length; i++) if (corner[i] != corner[(i + 1) % corner.Length]) all.Add((corner[i], corner[(i + 1) % corner.Length]));
        var constrained = Constrained.Apply(pts, result, all);
        var kept = new List<(int A, int B, int C)>();
        foreach (var (a, b, c) in constrained)
        {
            var (pa, pb, pc) = (pts[a], pts[b], pts[c]);
            if (Polygon2.Contains(boundary!, new Vec2((pa.X + pb.X + pc.X) / 3, (pa.Y + pb.Y + pc.Y) / 3))) kept.Add((a, b, c));
        }
        return kept.Count == 0 ? new Tin(pts, constrained) : Compact(pts, kept);
    }

    /// <summary>A TIN of only the points its triangles use, in the order they first come.</summary>
    public static Tin Compact(IReadOnlyList<Vec3> points, IEnumerable<(int A, int B, int C)> triangles)
    {
        var remap = new Dictionary<int, int>();
        var compact = new List<Vec3>();
        int Map(int idx)
        {
            if (!remap.TryGetValue(idx, out var n)) { n = compact.Count; remap[idx] = n; compact.Add(points[idx]); }
            return n;
        }
        var tris = triangles.Select(t => (Map(t.A), Map(t.B), Map(t.C))).ToList();
        return new Tin(compact, tris);
    }

    /// <summary>
    /// Bowyer–Watson over points given in batches (each batch inserted in plan order, x then y), measured from the
    /// middle of them all inside a super triangle twenty times their span.
    /// </summary>
    sealed class BowyerWatson
    {
        readonly int _n;
        readonly List<(double X, double Y)> _xy;
        readonly List<(int A, int B, int C, double X, double Y, double R2, bool Alive)> _tris = [];
        // Who owns each directed edge (the neighbour across a–b owns b–a).
        readonly Dictionary<(int, int), int> _owner = [];
        // Triangles of three points in a line: their circle holds every point, so the next insertion takes them away wherever it is.
        readonly List<int> _flat = [];

        public BowyerWatson(IReadOnlyList<Vec3> pts)
        {
            _n = pts.Count;
            double minX = pts.Min(p => p.X), minY = pts.Min(p => p.Y), maxX = pts.Max(p => p.X), maxY = pts.Max(p => p.Y);
            var span = Math.Max(Math.Max(maxX - minX, maxY - minY), 1e-9) * 20;
            var (cx, cy) = ((minX + maxX) / 2, (minY + maxY) / 2);
            // Measured from the middle: a survey's coordinates (hundreds of km) would round the circles of small triangles away.
            _xy = pts.Select(p => (X: p.X - cx, Y: p.Y - cy)).ToList();
            _xy.Add((-span, -span)); _xy.Add((span, -span)); _xy.Add((0, span));
            Add(_n, _n + 1, _n + 2);
        }

        void Add(int a, int b, int c)
        {
            var (ax, ay) = _xy[a]; var (bx, by) = _xy[b]; var (qx, qy) = _xy[c];
            if ((bx - ax) * (qy - ay) - (by - ay) * (qx - ax) < 0) (b, c) = (c, b);
            (ax, ay) = _xy[a]; (bx, by) = _xy[b]; (qx, qy) = _xy[c];
            var t = _tris.Count;
            _owner[(a, b)] = t; _owner[(b, c)] = t; _owner[(c, a)] = t;
            var d = 2 * (ax * (by - qy) + bx * (qy - ay) + qx * (ay - by));
            if (Math.Abs(d) < 1e-300) { _tris.Add((a, b, c, 0, 0, double.PositiveInfinity, true)); _flat.Add(t); return; }
            double a2 = ax * ax + ay * ay, b2 = bx * bx + by * by, c2 = qx * qx + qy * qy;
            var ox = (a2 * (by - qy) + b2 * (qy - ay) + c2 * (ay - by)) / d;
            var oy = (a2 * (qx - bx) + b2 * (ax - qx) + c2 * (bx - ax)) / d;
            _tris.Add((a, b, c, ox, oy, (ax - ox) * (ax - ox) + (ay - oy) * (ay - oy), true));
        }

        bool InCircle(int t, double px, double py)
        {
            var tr = _tris[t];
            var dx = px - tr.X; var dy = py - tr.Y;
            return !(dx * dx + dy * dy > tr.R2 * (1 + 1e-12));
        }

        double Side(int a, int b, double px, double py) => (_xy[b].X - _xy[a].X) * (py - _xy[a].Y) - (_xy[b].Y - _xy[a].Y) * (px - _xy[a].X);

        // The triangle the point is in, walked to from the last one made; -1 when the walk gets lost.
        int Locate(double px, double py)
        {
            var t = _tris.Count - 1;
            for (var steps = 0; steps <= _tris.Count; steps++)
            {
                var tr = _tris[t];
                var next = -1;
                foreach (var (a, b) in new[] { (tr.A, tr.B), (tr.B, tr.C), (tr.C, tr.A) })
                    if (Side(a, b, px, py) < 0 && _owner.TryGetValue((b, a), out var nb)) { next = nb; break; }
                if (next < 0) return t;
                t = next;
            }
            return -1;
        }

        public void Insert(IEnumerable<int> batch)
        {
            var cavity = new List<int>();
            var queued = new HashSet<int>();
            var stack = new Stack<int>();
            foreach (var i in batch.OrderBy(i => _xy[i].X).ThenBy(i => _xy[i].Y))
            {
                _inserted++;
                var (px, py) = _xy[i];
                // The triangles whose circle holds the point: from the one it is in, across their edges (a star around it), and those in a line.
                cavity.Clear(); queued.Clear(); stack.Clear();
                // The one it is in always goes, even when the rounding of its circle (far from the origin) says the point is just outside.
                var seed = Locate(px, py);
                if (seed < 0) for (var t = 0; t < _tris.Count; t++) if (_tris[t].Alive && InCircle(t, px, py)) { seed = t; break; }
                if (seed >= 0) { stack.Push(seed); queued.Add(seed); }
                foreach (var t in _flat) if (_tris[t].Alive && queued.Add(t)) stack.Push(t);
                while (stack.TryPop(out var t))
                {
                    if (t != seed && !InCircle(t, px, py)) continue;
                    cavity.Add(t);
                    var tr = _tris[t];
                    foreach (var (a, b) in new[] { (tr.A, tr.B), (tr.B, tr.C), (tr.C, tr.A) })
                        if (_owner.TryGetValue((b, a), out var nb) && _tris[nb].Alive && queued.Add(nb)) stack.Push(nb);
                }
                // In the order they were made, so the triangles come out as a scan of them all would make them.
                cavity.Sort();
                var edges = new Dictionary<(int, int), int>();
                foreach (var t in cavity)
                {
                    var tr = _tris[t];
                    _tris[t] = tr with { Alive = false };
                    foreach (var (a, b) in new[] { (tr.A, tr.B), (tr.B, tr.C), (tr.C, tr.A) })
                    {
                        if (_owner.TryGetValue((a, b), out var o) && o == t) _owner.Remove((a, b));
                        var k = a < b ? (a, b) : (b, a);
                        edges[k] = edges.GetValueOrDefault(k) + 1;
                    }
                }
                _flat.RemoveAll(t => !_tris[t].Alive);
                // The cavity's boundary: edges only one removed triangle had.
                foreach (var ((a, b), count) in edges) if (count == 1) Add(a, b, i);
            }
        }

        /// <summary>The live triangles between the points (none on the super triangle's corners).</summary>
        public List<(int A, int B, int C)> Triangles() =>
            _tris.Where(t => t.Alive && t.A < _n && t.B < _n && t.C < _n).Select(t => (t.A, t.B, t.C)).ToList();
    }

    /// <summary>
    /// Whether the points span an area in plan (three that are not in a line,
    /// counted as <see cref="Delaunay"/> counts them): what a triangulation
    /// needs, found without making one.
    /// </summary>
    public static bool Spans(IReadOnlyList<Vec3> input)
    {
        var pts = input.Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z))
            .DistinctBy(p => ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6))).ToList();
        if (pts.Count < 3) return false;
        var a = pts[0];
        var far = pts.MaxBy(p => (p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y))!;
        var len2 = (far.X - a.X) * (far.X - a.X) + (far.Y - a.Y) * (far.Y - a.Y);
        return pts.Any(p => Math.Abs(Cross(a, far, p)) > 1e-12 * len2);
    }

    /// <summary>A mesh (flat xyz, triangle indices) as a TIN: vertices merged to 5 decimals, degenerate triangles dropped, all counter-clockwise.</summary>
    public static Tin FromMesh(IReadOnlyList<double> positions, IReadOnlyList<int> indices)
    {
        var points = new List<Vec3>();
        var index = new Dictionary<(long, long, long), int>();
        int Id(int v)
        {
            var p = new Vec3(positions[v * 3], positions[v * 3 + 1], positions[v * 3 + 2]);
            var k = ((long)Math.Round(p.X * 1e5), (long)Math.Round(p.Y * 1e5), (long)Math.Round(p.Z * 1e5));
            if (!index.TryGetValue(k, out var i)) { index[k] = i = points.Count; points.Add(p); }
            return i;
        }
        var tris = new List<(int, int, int)>();
        for (var t = 0; t + 2 < indices.Count; t += 3)
        {
            int a = Id(indices[t]), b = Id(indices[t + 1]), c = Id(indices[t + 2]);
            var area2 = Cross(points[a], points[b], points[c]);
            if (Math.Abs(area2) < 1e-10) continue;
            tris.Add(area2 > 0 ? (a, b, c) : (a, c, b));
        }
        return new Tin(points, tris);
    }

    static double Cross(Vec3 a, Vec3 b, Vec3 c) => Vec2.Cross(b.Xy - a.Xy, c.Xy - a.Xy);

    /// <summary>
    /// Survey points, one a line, split on spaces, commas, semicolons or tabs (with ';' or tabs between them, the commas are decimals);
    /// lines starting with # or // skipped. Four leading numbers, or a point's
    /// name and three numbers, are PENZD (id, northing, easting, elevation,
    /// description): x is the easting.
    /// Otherwise three are x, y, z.
    /// </summary>
    public static List<Vec3> ParseSurveyPoints(string text)
    {
        var o = new List<Vec3>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//")) continue;
            // ';' or tabs between the fields: the commas are decimals (a European export).
            var f = line.Contains(';') || line.Contains('\t')
                ? line.Split([';', '\t'], StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().Replace(',', '.')).ToArray()
                : line.Split([' ', ',', ';', '\t'], StringSplitOptions.RemoveEmptyEntries);
            double? N(int i) => i < f.Length && double.TryParse(f[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
            // PENZD with the point named rather than numbered (P1, ST12, B3) as total stations export it.
            if ((N(0) is not null || f.Length > 0 && f[0].Any(char.IsLetter)) && N(1) is { } n && N(2) is { } e && N(3) is { } z) o.Add(new Vec3(e, n, z));
            else if (N(0) is { } x && N(1) is { } y && N(2) is { } zz) o.Add(new Vec3(x, y, zz));
        }
        return o;
    }
}

/// <summary>A TIN to ask: elevations, where a line meets it, its contours, its hull.</summary>
public sealed class Terrain
{
    public Tin Tin { get; }
    readonly double _minX, _minY, _cell;
    readonly int _nx, _ny;
    readonly List<int>[] _cells;

    public (Vec3 Min, Vec3 Max) Bounds { get; }

    public Terrain(Tin tin)
    {
        Tin = tin;
        var p = tin.Points;
        Bounds = p.Count == 0 ? default : (new Vec3(p.Min(q => q.X), p.Min(q => q.Y), p.Min(q => q.Z)), new Vec3(p.Max(q => q.X), p.Max(q => q.Y), p.Max(q => q.Z)));
        (_minX, _minY) = (Bounds.Min.X, Bounds.Min.Y);
        var (w, h) = (Bounds.Max.X - _minX, Bounds.Max.Y - _minY);
        _cell = Math.Max(2 * Math.Sqrt(Math.Max(w * h, 1e-9) / Math.Max(tin.Triangles.Count, 1)), 1e-6);
        _nx = Math.Max(1, (int)Math.Ceiling(w / _cell) + 1);
        _ny = Math.Max(1, (int)Math.Ceiling(h / _cell) + 1);
        _cells = new List<int>[_nx * _ny];
        for (var t = 0; t < tin.Triangles.Count; t++)
        {
            var (a, b, c) = tin.Triangles[t];
            var (x0, x1) = (Math.Min(p[a].X, Math.Min(p[b].X, p[c].X)), Math.Max(p[a].X, Math.Max(p[b].X, p[c].X)));
            var (y0, y1) = (Math.Min(p[a].Y, Math.Min(p[b].Y, p[c].Y)), Math.Max(p[a].Y, Math.Max(p[b].Y, p[c].Y)));
            for (var i = Cx(x0); i <= Cx(x1); i++)
                for (var j = Cy(y0); j <= Cy(y1); j++)
                    (_cells[j * _nx + i] ??= []).Add(t);
        }
    }

    int Cx(double x) => Math.Clamp((int)Math.Floor((x - _minX) / _cell), 0, _nx - 1);
    int Cy(double y) => Math.Clamp((int)Math.Floor((y - _minY) / _cell), 0, _ny - 1);

    /// <summary>The ground's elevation at a point in plan, or null outside the TIN.</summary>
    public double? ElevationAt(double x, double y) => TriangleAt(x, y).Z;

    /// <summary>The triangle a point in plan is in (its index, −1 outside the TIN) and the ground's elevation there.</summary>
    public (int Triangle, double? Z) TriangleAt(double x, double y)
    {
        if (Tin.Triangles.Count == 0 || x < Bounds.Min.X - 1e-9 || x > Bounds.Max.X + 1e-9 || y < Bounds.Min.Y - 1e-9 || y > Bounds.Max.Y + 1e-9) return (-1, null);
        var p = Tin.Points;
        foreach (var t in _cells[Cy(y) * _nx + Cx(x)] ?? [])
        {
            var (ia, ib, ic) = Tin.Triangles[t];
            var (a, b, c) = (p[ia], p[ib], p[ic]);
            var d = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(d) < 1e-300) continue;
            var l1 = ((b.Y - c.Y) * (x - c.X) + (c.X - b.X) * (y - c.Y)) / d;
            var l2 = ((c.Y - a.Y) * (x - c.X) + (a.X - c.X) * (y - c.Y)) / d;
            var l3 = 1 - l1 - l2;
            const double e = -1e-9;
            if (l1 >= e && l2 >= e && l3 >= e) return (t, l1 * a.Z + l2 * b.Z + l3 * c.Z);
        }
        return (-1, null);
    }

    /// <summary>The triangles whose box in plan meets the box from <paramref name="min"/> to <paramref name="max"/> (from the grid: each once).</summary>
    public IEnumerable<int> TrianglesIn(Vec2 min, Vec2 max)
    {
        if (Tin.Triangles.Count == 0 || max.X < Bounds.Min.X || min.X > Bounds.Max.X || max.Y < Bounds.Min.Y || min.Y > Bounds.Max.Y) yield break;
        var seen = new HashSet<int>();
        var p = Tin.Points;
        for (var i = Cx(min.X); i <= Cx(max.X); i++)
            for (var j = Cy(min.Y); j <= Cy(max.Y); j++)
                foreach (var t in _cells[j * _nx + i] ?? [])
                {
                    if (!seen.Add(t)) continue;
                    var (a, b, c) = Tin.Triangles[t];
                    if (Math.Max(p[a].X, Math.Max(p[b].X, p[c].X)) < min.X || Math.Min(p[a].X, Math.Min(p[b].X, p[c].X)) > max.X
                        || Math.Max(p[a].Y, Math.Max(p[b].Y, p[c].Y)) < min.Y || Math.Min(p[a].Y, Math.Min(p[b].Y, p[c].Y)) > max.Y) continue;
                    yield return t;
                }
    }

    /// <summary>The lowest and the highest corner of the triangles meeting a box in plan (what the ground there reaches, at most); null when none does.</summary>
    public (double Min, double Max)? HeightsIn(Vec2 min, Vec2 max)
    {
        double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
        foreach (var t in TrianglesIn(min, max))
        {
            var (a, b, c) = Tin.Triangles[t];
            foreach (var v in new[] { a, b, c }) { lo = Math.Min(lo, Tin.Points[v].Z); hi = Math.Max(hi, Tin.Points[v].Z); }
        }
        return lo <= hi ? (lo, hi) : null;
    }

    /// <summary>Where a line from <paramref name="origin"/> along <paramref name="dir"/> (its plan run at most <paramref name="maxRun"/>) first meets the ground; null when it leaves the TIN or never does.</summary>
    public Vec3? Hit(Vec3 origin, Vec3 dir, double maxRun)
    {
        var run = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
        if (run < 1e-12) return null;
        var u = dir * (1 / run); // a unit of plan run
        double F(double s) { var q = origin + u * s; return ElevationAt(q.X, q.Y) is { } z ? q.Z - z : double.NaN; }
        var step = Math.Max(_cell / 4, 0.05);
        var (s0, f0) = (0.0, F(0));
        if (double.IsNaN(f0)) return null;
        for (var s = step; s <= maxRun + 1e-9; s += step)
        {
            var f = F(Math.Min(s, maxRun));
            if (double.IsNaN(f)) return null;
            if (Math.Sign(f) != Math.Sign(f0) || f == 0)
            {
                double lo = s0, hi = Math.Min(s, maxRun);
                for (var k = 0; k < 40; k++)
                {
                    var mid = (lo + hi) / 2;
                    if (Math.Sign(F(mid)) == Math.Sign(f0)) lo = mid; else hi = mid;
                }
                return origin + u * ((lo + hi) / 2);
            }
            (s0, f0) = (s, f);
        }
        return null;
    }

    /// <summary>The contour at <paramref name="z"/>: one segment through each triangle it crosses.</summary>
    public List<(Vec2 A, Vec2 B)> Contour(double z)
    {
        var o = new List<(Vec2, Vec2)>();
        var p = Tin.Points;
        foreach (var (ia, ib, ic) in Tin.Triangles)
        {
            var cross = new List<Vec2>(2);
            foreach (var (i, j) in new[] { (ia, ib), (ib, ic), (ic, ia) })
            {
                var (a, b) = (p[i], p[j]);
                // Half-open at the top, so a contour through a vertex is counted once.
                if ((a.Z < z) == (b.Z < z)) continue;
                var t = (z - a.Z) / (b.Z - a.Z);
                cross.Add(new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
            }
            if (cross.Count == 2 && (cross[1] - cross[0]).Length > 1e-9) o.Add((cross[0], cross[1]));
        }
        return o;
    }

    /// <summary>The contour levels from the lowest to the highest point, every <paramref name="interval"/>.</summary>
    public IEnumerable<double> Levels(double interval)
    {
        if (!(interval > 0) || Tin.Triangles.Count == 0) yield break;
        // An interval too fine to move z at this height (z + interval == z) would loop for ever: stop there.
        for (var z = Math.Ceiling(Bounds.Min.Z / interval - 1e-9) * interval; z <= Bounds.Max.Z + 1e-9; z += interval)
        {
            yield return Math.Round(z / interval) * interval;
            if (z + interval == z) yield break;
        }
    }

    /// <summary>The TIN's boundary edges (each in one triangle only), directed counter-clockwise around it.</summary>
    public List<(int A, int B)> Boundary()
    {
        var all = new HashSet<(int, int)>();
        foreach (var (a, b, c) in Tin.Triangles) { all.Add((a, b)); all.Add((b, c)); all.Add((c, a)); }
        return all.Where(e => !all.Contains((e.Item2, e.Item1))).ToList();
    }
}

/// <summary>
/// Constraint edges forced into a triangulation by flipping the edges that
/// cross them (Sloan, 1993), then Lawson's flips to bring back the Delaunay
/// condition everywhere but across a constraint. The points and the triangles
/// are kept in a plan grid, so a segment looks only at the cells it passes
/// through (a flip keeps its two triangles inside the four points it turns).
/// </summary>
static class Constrained
{
    public static List<(int A, int B, int C)> Apply(IReadOnlyList<Vec3> pts, List<(int A, int B, int C)> input, IReadOnlyList<(int A, int B)> constraints)
    {
        var tris = input.Select(t => new[] { t.A, t.B, t.C }).ToList();
        if (constraints.Count == 0 || tris.Count == 0) return input;
        var owner = new Dictionary<(int, int), int>();
        void Own(int t) { var x = tris[t]; owner[(x[0], x[1])] = t; owner[(x[1], x[2])] = t; owner[(x[2], x[0])] = t; }
        for (var t = 0; t < tris.Count; t++) Own(t);

        // The grid: over the points the triangles use, about two triangles a cell.
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var x in tris) foreach (var v in x) { minX = Math.Min(minX, pts[v].X); maxX = Math.Max(maxX, pts[v].X); minY = Math.Min(minY, pts[v].Y); maxY = Math.Max(maxY, pts[v].Y); }
        var cell = Math.Max(Math.Sqrt(Math.Max((maxX - minX) * (maxY - minY), 1e-12) / Math.Max(tris.Count, 1)) * 1.5, 1e-6);
        var nx = Math.Clamp((int)Math.Ceiling((maxX - minX) / cell) + 1, 1, 4096);
        var ny = Math.Clamp((int)Math.Ceiling((maxY - minY) / cell) + 1, 1, 4096);
        double cw = Math.Max((maxX - minX) / Math.Max(nx - 1, 1), 1e-9), ch = Math.Max((maxY - minY) / Math.Max(ny - 1, 1), 1e-9);
        int Cx(double x) => Math.Clamp((int)Math.Floor((x - minX) / cw), 0, nx - 1);
        int Cy(double y) => Math.Clamp((int)Math.Floor((y - minY) / ch), 0, ny - 1);
        var triCells = new List<int>?[nx * ny];
        var pointCells = new List<int>?[nx * ny];
        void Register(int t, int a, int b, int c, int d = -1)
        {
            var xs = d >= 0 ? new[] { pts[a].X, pts[b].X, pts[c].X, pts[d].X } : [pts[a].X, pts[b].X, pts[c].X];
            var ys = d >= 0 ? new[] { pts[a].Y, pts[b].Y, pts[c].Y, pts[d].Y } : [pts[a].Y, pts[b].Y, pts[c].Y];
            for (int i = Cx(xs.Min()), i1 = Cx(xs.Max()); i <= i1; i++)
                for (int j = Cy(ys.Min()), j1 = Cy(ys.Max()); j <= j1; j++)
                    (triCells[j * nx + i] ??= []).Add(t);
        }
        for (var t = 0; t < tris.Count; t++) Register(t, tris[t][0], tris[t][1], tris[t][2]);
        var used = new HashSet<int>(tris.SelectMany(x => x));
        foreach (var p in used) (pointCells[Cy(pts[p].Y) * nx + Cx(pts[p].X)] ??= []).Add(p);
        // The cells a segment passes through: in each row of cells, those between where it enters and leaves the row.
        IEnumerable<int> Along(int a, int b)
        {
            var (p, q) = (pts[a], pts[b]);
            for (int j = Cy(Math.Min(p.Y, q.Y)), j1 = Cy(Math.Max(p.Y, q.Y)); j <= j1; j++)
            {
                double x0, x1;
                if (Math.Abs(q.Y - p.Y) < 1e-12) (x0, x1) = (Math.Min(p.X, q.X), Math.Max(p.X, q.X));
                else
                {
                    var (ya, yb) = (Math.Max(Math.Min(p.Y, q.Y), minY + j * ch), Math.Min(Math.Max(p.Y, q.Y), minY + (j + 1) * ch));
                    if (j == 0) ya = Math.Min(p.Y, q.Y);
                    if (j == ny - 1) yb = Math.Max(p.Y, q.Y);
                    var (xa, xb) = (p.X + (q.X - p.X) * (ya - p.Y) / (q.Y - p.Y), p.X + (q.X - p.X) * (yb - p.Y) / (q.Y - p.Y));
                    (x0, x1) = (Math.Min(xa, xb), Math.Max(xa, xb));
                }
                for (int i = Cx(x0 - 1e-9), i1 = Cx(x1 + 1e-9); i <= i1; i++) yield return j * nx + i;
            }
        }

        var fixedEdges = new HashSet<(int, int)>();
        static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);
        double Orient(int a, int b, int c) => (pts[b].X - pts[a].X) * (pts[c].Y - pts[a].Y) - (pts[b].Y - pts[a].Y) * (pts[c].X - pts[a].X);
        int Third(int t, int u, int v) => tris[t].First(x => x != u && x != v);
        bool Exists(int a, int b) => owner.ContainsKey((a, b)) || owner.ContainsKey((b, a));
        // Whether u–v crosses a–b inside both (not at a shared point).
        bool Crosses(int u, int v, int a, int b) =>
            u != a && u != b && v != a && v != b && Math.Sign(Orient(a, b, u)) * Math.Sign(Orient(a, b, v)) < 0 && Math.Sign(Orient(u, v, a)) * Math.Sign(Orient(u, v, b)) < 0;
        // The edge u–v (both triangles there) turned to w1–w2; the new edge, or null when the quad is not convex.
        (int, int)? Flip(int u, int v)
        {
            if (!owner.TryGetValue((u, v), out var t1) || !owner.TryGetValue((v, u), out var t2)) return null;
            int w1 = Third(t1, u, v), w2 = Third(t2, u, v);
            if (!(Orient(w1, w2, u) * Orient(w1, w2, v) < 0)) return null;
            foreach (var t in new[] { t1, t2 }) { var x = tris[t]; owner.Remove((x[0], x[1])); owner.Remove((x[1], x[2])); owner.Remove((x[2], x[0])); }
            tris[t1] = [u, w2, w1];
            tris[t2] = [w2, v, w1];
            Own(t1); Own(t2);
            Register(t1, u, v, w1, w2);
            Register(t2, u, v, w1, w2);
            return (w1, w2);
        }
        var touched = new List<(int, int)>();
        var candidates = new HashSet<int>();
        void Insert(int a, int b, int depth)
        {
            if (Exists(a, b)) { fixedEdges.Add(Key(a, b)); return; }
            // A point on the segment splits it.
            var ab = new Vec2(pts[b].X - pts[a].X, pts[b].Y - pts[a].Y);
            var len2 = ab.X * ab.X + ab.Y * ab.Y;
            var on = Along(a, b).SelectMany(c => pointCells[c] ?? []).Where(p => p != a && p != b && Math.Abs(Orient(a, b, p)) <= 1e-9 * len2)
                .Select(p => (p, s: ((pts[p].X - pts[a].X) * ab.X + (pts[p].Y - pts[a].Y) * ab.Y) / len2)).Where(x => x.s > 1e-9 && x.s < 1 - 1e-9)
                .OrderBy(x => x.s).Select(x => x.p).FirstOrDefault(-1);
            if (on >= 0 && depth < 1000) { Insert(a, on, depth + 1); Insert(on, b, depth + 1); return; }
            // The edges that cross it, from the triangles in the cells it passes through.
            candidates.Clear();
            foreach (var c in Along(a, b)) foreach (var t in triCells[c] ?? []) candidates.Add(t);
            var crossing = new List<(int, int)>();
            var listed = new HashSet<(int, int)>();
            foreach (var t in candidates.Order())
            {
                var x = tris[t];
                foreach (var e in new[] { (x[0], x[1]), (x[1], x[2]), (x[2], x[0]) })
                    if ((e.Item1 < e.Item2 || !owner.ContainsKey((e.Item2, e.Item1))) && Crosses(e.Item1, e.Item2, a, b) && listed.Add(Key(e.Item1, e.Item2))) crossing.Add(e);
            }
            var queue = new Queue<(int, int)>(crossing);
            var guard = 0;
            while (queue.Count > 0 && guard++ < 100000)
            {
                var (u, v) = queue.Dequeue();
                if (!Exists(u, v)) continue;
                if (!owner.ContainsKey((u, v))) (u, v) = (v, u);
                if (Flip(u, v) is not { } ne) { queue.Enqueue((u, v)); continue; }
                if (Crosses(ne.Item1, ne.Item2, a, b)) queue.Enqueue(ne); else touched.Add(ne);
            }
            if (Exists(a, b)) fixedEdges.Add(Key(a, b));
        }
        foreach (var (a, b) in constraints) Insert(a, b, 0);

        // Lawson: an edge whose far point lies inside the circle of the near triangle turns, unless it is a constraint.
        bool InCircle(int a, int b, int c, int d)
        {
            double ax = pts[a].X - pts[d].X, ay = pts[a].Y - pts[d].Y, bx = pts[b].X - pts[d].X, by = pts[b].Y - pts[d].Y, cx = pts[c].X - pts[d].X, cy = pts[c].Y - pts[d].Y;
            return (ax * ax + ay * ay) * (bx * cy - cx * by) - (bx * bx + by * by) * (ax * cy - cx * ay) + (cx * cx + cy * cy) * (ax * by - bx * ay) > 1e-12;
        }
        var stack = new Stack<(int, int)>(touched);
        var steps = 0;
        while (stack.Count > 0 && steps++ < 200000)
        {
            var (u, v) = stack.Pop();
            if (fixedEdges.Contains(Key(u, v)) || !owner.TryGetValue((u, v), out var t1) || !owner.TryGetValue((v, u), out var t2)) continue;
            int w1 = Third(t1, u, v), w2 = Third(t2, u, v);
            if (!InCircle(u, v, w1, w2) || Flip(u, v) is null) continue;
            foreach (var e in new[] { (u, w2), (w2, v), (v, w1), (w1, u) }) stack.Push(e);
        }
        return tris.Select(x => (x[0], x[1], x[2])).ToList();
    }
}
