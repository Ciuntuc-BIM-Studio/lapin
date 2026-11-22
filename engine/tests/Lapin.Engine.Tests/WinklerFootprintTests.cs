using Lapin.Analysis.Solver;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// Winkler's bed under a pad footing takes the footprint's own shape (audit 22.11.2026, G5): its area and second moments about
/// its centroid, not its bounding box's. Each number by hand in the comments.
/// </summary>
public class WinklerFootprintTests
{
    [Fact]
    public void A_2_by_2_pad_turned_45_degrees_keeps_its_area_and_inertia()
    {
        // A square of side 2 turned 45°: A = 4, I = 2·2³/12 = 1.3333 about any axis through its centre, Ixy = 0.
        // Its bounding box (2√2 × 2√2 = 8) gave twice the area, and I = 2.828⁴/12 = 5.3333, four times.
        var r = Math.Sqrt(2);
        var (a, ixx, iyy, ixy) = StructuralProblem.Footprint([new Vec2(5 + r, 3), new Vec2(5, 3 + r), new Vec2(5 - r, 3), new Vec2(5, 3 - r)]);
        Assert.Equal(4, a, 9);
        Assert.Equal(4.0 / 3, ixx, 9);
        Assert.Equal(4.0 / 3, iyy, 9);
        Assert.Equal(0, ixy, 9);
    }

    [Fact]
    public void A_round_pad_takes_its_circles_area_and_inertia()
    {
        // A circle of diameter 2 as a 720-gon: A = (n/2) r² sin(2π/n) → π r² = 3.1416 (the box: 4, 27 % more);
        // I = π r⁴/4 = 0.7854 (the polygon's: (n/24) r⁴ sin(2π/n)(2 + cos(2π/n)) = 0.785388).
        const int n = 720;
        var pts = Enumerable.Range(0, n).Select(i => new Vec2(1 + Math.Cos(2 * Math.PI * i / n), -2 + Math.Sin(2 * Math.PI * i / n))).ToList();
        var (a, ixx, iyy, ixy) = StructuralProblem.Footprint(pts);
        var s = Math.Sin(2 * Math.PI / n);
        Assert.Equal(n / 2.0 * s, a, 9);
        Assert.Equal(n / 24.0 * s * (2 + Math.Cos(2 * Math.PI / n)), ixx, 9);
        Assert.Equal(ixx, iyy, 9);
        Assert.Equal(0, ixy, 9);
        Assert.Equal(Math.PI, a, 4);
        Assert.Equal(Math.PI / 4, ixx, 4);
    }

    [Fact]
    public void An_L_pad_has_its_product_of_inertia_and_either_way_round_the_same()
    {
        // The L of a 2 × 1 (x 0–2, y 0–1) and a 1 × 1 over its left end (x 0–1, y 1–2): A = 3, centroid (2.5/3, 2.5/3) = (0.8333, 0.8333).
        // Ixx = [2·1³/12 + 2·(0.5 − 0.8333)²] + [1/12 + 1·(1.5 − 0.8333)²] = 0.38889 + 0.52778 = 0.91667; Iyy the same by symmetry;
        // Ixy = 2·(1 − 0.8333)(0.5 − 0.8333) + 1·(0.5 − 0.8333)(1.5 − 0.8333) = −0.11111 − 0.22222 = −0.33333 (so ks·Ixy couples θx and θy).
        Vec2[] l = [new(0, 0), new(2, 0), new(2, 1), new(1, 1), new(1, 2), new(0, 2)];
        foreach (var outline in new[] { l, l.Reverse().ToArray() })
        {
            var (a, ixx, iyy, ixy) = StructuralProblem.Footprint(outline);
            Assert.Equal(3, a, 9);
            Assert.Equal(0.916667, ixx, 6);
            Assert.Equal(0.916667, iyy, 6);
            Assert.Equal(-1.0 / 3, ixy, 9);
        }
    }
}
