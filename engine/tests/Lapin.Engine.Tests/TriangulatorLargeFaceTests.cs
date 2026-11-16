using Lapin.Geometry;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// Audit 15.11.2026, P18: the ear clipper's guard (n² + 16 in an int) overflowed above 46 340 vertices, so a face that big
/// (a DXF outline, a terrain's boundary) came out with no triangle. The clipper was quadratic too (each pass looked at every
/// corner): 50 000 vertices took a minute for a convex ring and four for a star-like one. It walks a linked ring now and finds
/// what could stand in an ear through a z-order index (as earcut does), so such faces take a fraction of a second (the tests allow five, for a loaded machine).
/// </summary>
public class TriangulatorLargeFaceTests(ITestOutputHelper output)
{
    static List<Vec3> Ring(int n, double inner) => Enumerable.Range(0, n).Select(i =>
    {
        var a = 2 * Math.PI * i / n;
        var r = i % 2 == 0 ? 1000.0 : inner;
        return new Vec3(r * Math.Cos(a), r * Math.Sin(a), 0);
    }).ToList();

    [Fact]
    public void A_face_of_more_than_46_340_vertices_is_triangulated_whole()
    {
        const int n = 46_400;
        const double inner = 1000;
        var pts = Ring(n, inner);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var tris = Triangulator.Face(pts, [.. Enumerable.Range(0, n)], [], Vec3.UnitZ);
        clock.Stop();
        output.WriteLine($"{n} vertices (inner radius {inner}): {tris.Count} triangles in {clock.ElapsedMilliseconds} ms");
        Assert.Equal(n - 2, tris.Count);
        var area = tris.Sum(t => Vec3.Cross(pts[t.B] - pts[t.A], pts[t.C] - pts[t.A]).Z / 2);
        Assert.Equal(Polygon2.SignedArea(pts.Select(p => new Vec2(p.X, p.Y)).ToArray()), area, 3);
    }

    /// <summary>
    /// The fastest of up to three runs (a loaded machine's pauses are not the triangulator's), stopping at the first under the
    /// limit, and its triangles.
    /// </summary>
    static (long Ms, List<(int A, int B, int C)> Tris) Timed(Func<List<(int A, int B, int C)>> run, long limit)
    {
        var best = long.MaxValue;
        List<(int A, int B, int C)> tris = [];
        for (var i = 0; i < 3 && best >= limit; i++)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            tris = run();
            best = Math.Min(best, clock.ElapsedMilliseconds);
        }
        return (best, tris);
    }

    /// <summary>Every triangle turning CCW (none flipped, none flat): their areas add up only to what they cover.</summary>
    static double Area(List<Vec3> pts, List<(int A, int B, int C)> tris)
    {
        var sum = 0.0;
        foreach (var t in tris)
        {
            var a = Vec3.Cross(pts[t.B] - pts[t.A], pts[t.C] - pts[t.A]).Z / 2;
            Assert.True(a > 0, $"triangle {t} turns clockwise");
            sum += a;
        }
        return sum;
    }

    [Theory]
    [InlineData(1000.0)]
    [InlineData(970.0)]
    public void A_ring_of_50_000_vertices_triangulates_in_seconds_not_minutes(double inner)
    {
        const int n = 50_000;
        var pts = Ring(n, inner);
        Triangulator.Face(Ring(200, inner), [.. Enumerable.Range(0, 200)], [], Vec3.UnitZ);   // warmed up
        var (ms, tris) = Timed(() => Triangulator.Face(pts, [.. Enumerable.Range(0, n)], [], Vec3.UnitZ), 5000);
        output.WriteLine($"{n} vertices (inner radius {inner}): {tris.Count} triangles in {ms} ms");
        Assert.Equal(n - 2, tris.Count);
        Assert.Equal(Polygon2.SignedArea(pts.Select(p => new Vec2(p.X, p.Y)).ToArray()), Area(pts, tris), 3);
        Assert.True(ms < 5000, $"{ms} ms");
    }

    [Fact]
    public void A_ring_of_thousands_of_vertices_with_holes_triangulates_whole_and_quickly()
    {
        // A star-like ring of 40 000 vertices and 25 round holes of 400 each, on a 5 × 5 grid inside it.
        const int n = 40_000, holes = 25, m = 400;
        var pts = Ring(n, 985);
        var loops = new List<int[]>();
        var holeArea = 0.0;
        for (var h = 0; h < holes; h++)
        {
            var (cx, cy) = (-400 + 200 * (h % 5), -400 + 200 * (h / 5));
            var first = pts.Count;
            var ring = new List<Vec2>();
            for (var i = 0; i < m; i++)
            {
                var a = 2 * Math.PI * i / m;
                var r = i % 2 == 0 ? 60.0 : 55.0;
                var p = new Vec2(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
                ring.Add(p);
                pts.Add(new Vec3(p.X, p.Y, 0));
            }
            holeArea += Polygon2.SignedArea([.. ring]);
            // Every other hole given clockwise, as a file may.
            var ids = Enumerable.Range(first, m).ToArray();
            loops.Add(h % 2 == 0 ? ids : [.. ids.Reverse()]);
        }
        var (ms, tris) = Timed(() => Triangulator.Face(pts, [.. Enumerable.Range(0, n)], [.. loops], Vec3.UnitZ), 5000);
        output.WriteLine($"{n} + {holes} × {m} vertices: {tris.Count} triangles in {ms} ms");
        // A face of v vertices and h holes: v + 2h − 2 triangles.
        Assert.Equal(n + holes * m + 2 * holes - 2, tris.Count);
        var outer = Polygon2.SignedArea(pts.Take(n).Select(p => new Vec2(p.X, p.Y)).ToArray());
        Assert.Equal(outer - holeArea, Area(pts, tris), 3);
        Assert.True(ms < 5000, $"{ms} ms");
    }
}
