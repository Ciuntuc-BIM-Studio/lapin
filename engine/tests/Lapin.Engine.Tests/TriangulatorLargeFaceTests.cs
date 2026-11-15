using Lapin.Geometry;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// Audit 15.11.2026, P18: the ear clipper's guard (n² + 16 in an int) overflowed above 46 340 vertices, so a face that big
/// (a DXF outline, a terrain's boundary) came out with no triangle. Every pass of the clipper takes one corner away, so
/// the guard is the ring's count now. The clipper stays quadratic (each pass looks at every corner): 50 000 vertices took
/// about a minute for a convex ring and four for a star-like one on a loaded machine, so the test stays just above the
/// old limit, on a convex ring.
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
}
