using System.Globalization;
using Lapin.Geometry;

namespace Lapin.Civil;

/// <summary>A triangulated irregular network: points (x, y in plan, z the elevation) and counter-clockwise triangles.</summary>
public sealed record Tin(IReadOnlyList<Vec3> Points, IReadOnlyList<(int A, int B, int C)> Triangles)
{
    /// <summary>
    /// Delaunay in plan (Bowyer–Watson): points that coincide in plan (to 6
    /// decimals) count once, the first one kept; the triangles cover the convex
    /// hull, counter-clockwise seen from above. <paramref name="constraints"/>
    /// (pairs of input indices: a breakline's segments) are kept as edges
    /// exactly, split where a point lies on them; the rest stays Delaunay.
    /// </summary>
    public static Tin Delaunay(IReadOnlyList<Vec3> input, IReadOnlyList<(int A, int B)>? constraints = null)
    {
        var seen = new Dictionary<(long, long), int>();
        var pts = new List<Vec3>();
        var map = new int[input.Count];
        for (var k = 0; k < input.Count; k++)
        {
            var p = input[k];
            map[k] = -1;
            if (!(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z))) continue;
            var key = ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6));
            if (!seen.TryGetValue(key, out var at)) { seen[key] = at = pts.Count; pts.Add(p); }
            map[k] = at;
        }
        var n = pts.Count;
        if (n < 3) return new Tin(pts, []);
        double minX = pts.Min(p => p.X), minY = pts.Min(p => p.Y), maxX = pts.Max(p => p.X), maxY = pts.Max(p => p.Y);
        var span = Math.Max(Math.Max(maxX - minX, maxY - minY), 1e-9) * 20;
        var (cx, cy) = ((minX + maxX) / 2, (minY + maxY) / 2);
        var xy = pts.Select(p => (p.X, p.Y)).ToList();
        xy.Add((cx - span, cy - span)); xy.Add((cx + span, cy - span)); xy.Add((cx, cy + span));

        // Each triangle with its circumcircle; dead ones are compacted from time to time.
        var tris = new List<(int A, int B, int C, double X, double Y, double R2, bool Alive)>();
        void Add(int a, int b, int c)
        {
            var (ax, ay) = xy[a]; var (bx, by) = xy[b]; var (qx, qy) = xy[c];
            if ((bx - ax) * (qy - ay) - (by - ay) * (qx - ax) < 0) (b, c) = (c, b);
            (ax, ay) = xy[a]; (bx, by) = xy[b]; (qx, qy) = xy[c];
            var d = 2 * (ax * (by - qy) + bx * (qy - ay) + qx * (ay - by));
            if (Math.Abs(d) < 1e-300) { tris.Add((a, b, c, 0, 0, double.PositiveInfinity, true)); return; }
            double a2 = ax * ax + ay * ay, b2 = bx * bx + by * by, c2 = qx * qx + qy * qy;
            var ox = (a2 * (by - qy) + b2 * (qy - ay) + c2 * (ay - by)) / d;
            var oy = (a2 * (qx - bx) + b2 * (ax - qx) + c2 * (bx - ax)) / d;
            tris.Add((a, b, c, ox, oy, (ax - ox) * (ax - ox) + (ay - oy) * (ay - oy), true));
        }
        Add(n, n + 1, n + 2);
        var dead = 0;
        foreach (var i in Enumerable.Range(0, n).OrderBy(i => xy[i].X).ThenBy(i => xy[i].Y))
        {
            var (px, py) = xy[i];
            var edges = new Dictionary<(int, int), int>();
            for (var t = 0; t < tris.Count; t++)
            {
                var tr = tris[t];
                if (!tr.Alive) continue;
                var dx = px - tr.X; var dy = py - tr.Y;
                if (dx * dx + dy * dy > tr.R2 * (1 + 1e-12)) continue;
                tris[t] = tr with { Alive = false };
                dead++;
                foreach (var (a, b) in new[] { (tr.A, tr.B), (tr.B, tr.C), (tr.C, tr.A) })
                {
                    var k = a < b ? (a, b) : (b, a);
                    edges[k] = edges.GetValueOrDefault(k) + 1;
                }
            }
            // The cavity's boundary: edges only one removed triangle had.
            foreach (var ((a, b), count) in edges) if (count == 1) Add(a, b, i);
            if (dead > tris.Count / 2 && tris.Count > 4 * n + 64) { tris.RemoveAll(t => !t.Alive); dead = 0; }
        }
        var result = tris.Where(t => t.Alive && t.A < n && t.B < n && t.C < n).Select(t => (t.A, t.B, t.C)).ToList();
        if (constraints is { Count: > 0 })
            result = Constrained.Apply(pts, result, constraints.Where(c => c.A >= 0 && c.B >= 0 && c.A < map.Length && c.B < map.Length)
                .Select(c => (map[c.A], map[c.B])).Where(c => c.Item1 >= 0 && c.Item2 >= 0 && c.Item1 != c.Item2).ToList());
        return new Tin(pts, result);
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

    static double Cross(Vec3 a, Vec3 b, Vec3 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    /// <summary>
    /// Survey points, one a line, split on spaces, commas, semicolons or tabs (with ';' or tabs between them, the commas are decimals);
    /// lines starting with # or // skipped. Four leading numbers are PENZD
    /// (id, northing, easting, elevation, description): x is the easting.
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
            if (N(0) is not null && N(1) is { } n && N(2) is { } e && N(3) is { } z) o.Add(new Vec3(e, n, z));
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
    public double? ElevationAt(double x, double y)
    {
        if (Tin.Triangles.Count == 0 || x < Bounds.Min.X - 1e-9 || x > Bounds.Max.X + 1e-9 || y < Bounds.Min.Y - 1e-9 || y > Bounds.Max.Y + 1e-9) return null;
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
            if (l1 >= e && l2 >= e && l3 >= e) return l1 * a.Z + l2 * b.Z + l3 * c.Z;
        }
        return null;
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
        if (interval <= 0 || Tin.Triangles.Count == 0) yield break;
        for (var z = Math.Ceiling(Bounds.Min.Z / interval - 1e-9) * interval; z <= Bounds.Max.Z + 1e-9; z += interval)
            yield return Math.Round(z / interval) * interval;
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
/// condition everywhere but across a constraint.
/// </summary>
static class Constrained
{
    public static List<(int A, int B, int C)> Apply(IReadOnlyList<Vec3> pts, List<(int A, int B, int C)> input, IReadOnlyList<(int A, int B)> constraints)
    {
        var tris = input.Select(t => new[] { t.A, t.B, t.C }).ToList();
        var owner = new Dictionary<(int, int), int>();
        void Own(int t) { var x = tris[t]; owner[(x[0], x[1])] = t; owner[(x[1], x[2])] = t; owner[(x[2], x[0])] = t; }
        for (var t = 0; t < tris.Count; t++) Own(t);
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
            return (w1, w2);
        }
        var touched = new List<(int, int)>();
        void Insert(int a, int b, int depth)
        {
            if (Exists(a, b)) { fixedEdges.Add(Key(a, b)); return; }
            // A point on the segment splits it.
            var ab = new Vec2(pts[b].X - pts[a].X, pts[b].Y - pts[a].Y);
            var len2 = ab.X * ab.X + ab.Y * ab.Y;
            var on = Enumerable.Range(0, pts.Count).Where(p => p != a && p != b && Math.Abs(Orient(a, b, p)) <= 1e-9 * len2)
                .Select(p => (p, s: ((pts[p].X - pts[a].X) * ab.X + (pts[p].Y - pts[a].Y) * ab.Y) / len2)).Where(x => x.s > 1e-9 && x.s < 1 - 1e-9)
                .OrderBy(x => x.s).Select(x => x.p).FirstOrDefault(-1);
            if (on >= 0 && depth < 1000) { Insert(a, on, depth + 1); Insert(on, b, depth + 1); return; }
            var queue = new Queue<(int, int)>(owner.Keys.Where(e => e.Item1 < e.Item2 || !owner.ContainsKey((e.Item2, e.Item1))).Where(e => Crosses(e.Item1, e.Item2, a, b)));
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
