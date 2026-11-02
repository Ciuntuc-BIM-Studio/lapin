using System.Diagnostics;
using Lapin.Civil;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// The Delaunay triangulation finds each point's cavity by walking to it
/// rather than testing every triangle: the same triangles, in the same
/// order, as the scan it replaced (kept here as the reference), and fast on
/// a big survey.
/// </summary>
public class TinWalkTests
{
    /// <summary>The Bowyer–Watson scan Tin.Delaunay was before (no constraints): every triangle tested for every point.</summary>
    static List<(int, int, int)> Scan(IReadOnlyList<Vec3> input)
    {
        var seen = new Dictionary<(long, long), int>();
        var pts = new List<Vec3>();
        foreach (var p in input)
        {
            if (!(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z))) continue;
            var key = ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6));
            if (!seen.ContainsKey(key)) { seen[key] = pts.Count; pts.Add(p); }
        }
        var n = pts.Count;
        if (n < 3) return [];
        double minX = pts.Min(p => p.X), minY = pts.Min(p => p.Y), maxX = pts.Max(p => p.X), maxY = pts.Max(p => p.Y);
        var span = Math.Max(Math.Max(maxX - minX, maxY - minY), 1e-9) * 20;
        var (cx, cy) = ((minX + maxX) / 2, (minY + maxY) / 2);
        var xy = pts.Select(p => (p.X, p.Y)).ToList();
        xy.Add((cx - span, cy - span)); xy.Add((cx + span, cy - span)); xy.Add((cx, cy + span));
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
                foreach (var (a, b) in new[] { (tr.A, tr.B), (tr.B, tr.C), (tr.C, tr.A) })
                {
                    var k = a < b ? (a, b) : (b, a);
                    edges[k] = edges.GetValueOrDefault(k) + 1;
                }
            }
            foreach (var ((a, b), count) in edges) if (count == 1) Add(a, b, i);
        }
        return tris.Where(t => t.Alive && t.A < n && t.B < n && t.C < n).Select(t => (t.A, t.B, t.C)).ToList();
    }

    static List<Vec3> Random(int count, int seed, double size)
    {
        var r = new Random(seed);
        return Enumerable.Range(0, count).Select(_ => new Vec3(r.NextDouble() * size, r.NextDouble() * size, r.NextDouble() * 5)).ToList();
    }

    public static TheoryData<string> Samples => ["scatter", "grid", "survey", "line"];

    static List<Vec3> Sample(string name) => name switch
    {
        "scatter" => Random(600, 11, 100),
        // A regular grid: four points on every circle, the cases a walk could see differently from a scan.
        "grid" => [.. Enumerable.Range(0, 20).SelectMany(i => Enumerable.Range(0, 20).Select(j => new Vec3(i * 2.5, j * 2.5, (i + j) * 0.1)))],
        // Real coordinates (Stereo 70), points repeated.
        "survey" => [.. Random(300, 5, 40).Select(p => new Vec3(512300 + p.X, 634200 + p.Y, 245 + p.Z)), .. Random(50, 5, 40).Select(p => new Vec3(512300 + p.X, 634200 + p.Y, 0))],
        "cluster" => [.. Sample("survey"), .. Random(100, 9, 0.5).Select(p => new Vec3(512320 + p.X, 634220 + p.Y, 246))],
        // Points in lines, with a few off them.
        _ => [.. Enumerable.Range(0, 40).Select(i => new Vec3(i, 0, 0)), .. Enumerable.Range(0, 40).Select(i => new Vec3(i, i * 0.5, 1)), new(3, 7, 2), new(20, -4, 1)],
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void The_walk_makes_the_same_triangles_as_the_scan(string sample)
    {
        var pts = Sample(sample);
        Assert.Equal(Scan(pts), Tin.Delaunay(pts).Triangles.Select(t => (t.A, t.B, t.C)).ToList());
    }

    static int Twice(IEnumerable<(int, int, int)> tris)
    {
        var edges = new Dictionary<(int, int), int>();
        foreach (var (x, y, z) in tris) foreach (var e in new[] { (x, y), (y, z), (z, x) }) edges[e] = edges.GetValueOrDefault(e) + 1;
        return edges.Count(e => e.Value > 1);
    }

    [Fact]
    public void Where_the_scan_overlapped_triangles_in_a_tight_cluster_at_real_coordinates_the_walk_makes_a_clean_triangulation()
    {
        // 100 points in half a metre, 600 km from the origin: the circles' rounding gave the scan a cavity in two pieces.
        var pts = Sample("cluster");
        Assert.True(Twice(Scan(pts)) > 0);
        var tin = Tin.Delaunay(pts);
        Assert.Equal(0, Twice(tin.Triangles.Select(t => (t.A, t.B, t.C))));
        Assert.Equal(2 * tin.Points.Count - 2 - new Terrain(tin).Boundary().Count, tin.Triangles.Count);
    }

    [Fact]
    public void Thirty_thousand_survey_points_triangulate_in_seconds_and_a_line_spans_no_area()
    {
        var pts = Random(30000, 3, 1000);
        var sw = Stopwatch.StartNew();
        var tin = Tin.Delaunay(pts);
        sw.Stop();
        Assert.True(tin.Triangles.Count > 2 * 30000 - 100, $"{tin.Triangles.Count} triangles");
        Assert.True(sw.Elapsed.TotalSeconds < 10, $"{sw.Elapsed.TotalSeconds:F1} s");
        Assert.True(Tin.Spans(pts));
        Assert.False(Tin.Spans([new(0, 0, 0), new(1, 1, 5), new(2, 2, 1), new(1, 1, 3)]));
        Assert.False(Tin.Spans([new(0, 0, 0), new(0, 0, 1), new(1e-8, 0, 1)]));
        Assert.True(Tin.Spans([new(0, 0, 0), new(10, 0, 0), new(5, 0.01, 0)]));
    }
}
