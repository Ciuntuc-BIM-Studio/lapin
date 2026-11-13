using Lapin.Geometry;
using static Lapin.Engine.Tests.StudioFlushCutTests;

namespace Lapin.Engine.Tests;

/// <summary>
/// <see cref="Flush.Sunk"/> sinks a union's tool face only when it lies within the host's face, tested exactly in the plane
/// (decision 521): a tool spanning a U's or an L's notch, or lying over a hole, stays as it is (nothing would poke out),
/// even where every corner of it and every centroid of its triangles lies on the host; a tool inside an L, or the host's
/// face itself, sinks and joins cleanly. Each square to the axes and turned 17° off the origin.
/// </summary>
public class FlushWithinTests
{
    static readonly Lapin.Booleans.ManifoldBooleans Mb = new();

    /// <summary>A prism on the outline (x, y) from z0 to z1, turned rot° about (0, 0) and moved to (12.345, −7.77) when turned.</summary>
    static Solid Prism2(double rot, double z0, double z1, (double X, double Y)[] outline, params (double X, double Y)[][] holes)
    {
        var r = rot * Math.PI / 180;
        var (ox, oy) = rot == 0 ? (0.0, 0.0) : (12.345, -7.77);
        List<Vec3> Ring((double X, double Y)[] pts) =>
            pts.Select(p => new Vec3(ox + p.X * Math.Cos(r) - p.Y * Math.Sin(r), oy + p.X * Math.Sin(r) + p.Y * Math.Cos(r), z0)).ToList();
        return Prism.Extrude(Ring(outline), holes.Length == 0 ? null : holes.Select(h => (IReadOnlyList<Vec3>)Ring(h)).ToList(), new Vec3(0, 0, z1 - z0))!;
    }

    static (double, double)[] Rect(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];

    static readonly (double, double)[] U = [(0, 0), (3, 0), (3, 2), (2, 2), (2, 1), (1, 1), (1, 2), (0, 2)];
    static readonly (double, double)[] L = [(0, 0), (2, 0), (2, 1), (1, 1), (1, 2), (0, 2)];

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void A_tool_spanning_a_U_shaped_faces_notch_is_not_sunk(double rot)
    {
        var host = Prism2(rot, 0, 1, U);
        // Across the arms' tops; across the base and a strip of the notch, every corner and triangle centroid on the base;
        // and one whose edges run along the notch's mouth and floor, so only the area it shares tells.
        foreach (var (rect, area) in new[] { (Rect(0.2, 1.2, 2.8, 1.8), 2.6 * 0.6), (Rect(0.1, 0.2, 2.9, 1.1), 2.8 * 0.9), (Rect(0.5, 1, 2.5, 2), 2.0) })
        {
            var tool = Prism2(rot, 1, 1.5, rect);
            Assert.Same(tool, Flush.Sunk(tool, host));
            // The union as it was: the tool on the host, nothing of it pushed down through the notch.
            Assert.Equal(5 + area * 0.5, Measure.Volume(Mb.Union([host, Flush.Sunk(tool, host)])!), 6);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void A_tool_inside_an_L_shaped_face_is_sunk_and_joins_cleanly(double rot)
    {
        var host = Prism2(rot, 0, 1, L);
        var tool = Prism2(rot, 1, 1.5, [(0.2, 0.2), (1.8, 0.2), (1.8, 0.8), (0.8, 0.8), (0.8, 1.8), (0.2, 1.8)]);
        var sunk = Flush.Sunk(tool, host);
        Assert.NotSame(tool, sunk);
        Assert.Equal(1 - Flush.By, sunk.Vertices.Min(v => v.Z), 9);
        // The host's eight faces (its top round the tool's footprint, a hole) and the tool's six sides and top.
        Clean(Mb.Union([host, sunk])!, 15, 3 + (1.6 * 0.6 + 0.6 * 1.0) * 0.5);
        // A square over the L's notch is not within it.
        var over = Prism2(rot, 1, 1.5, Rect(0.2, 0.2, 1.8, 1.8));
        Assert.Same(over, Flush.Sunk(over, host));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void A_tool_over_a_hole_is_not_sunk(double rot)
    {
        var host = Prism2(rot, 0, 1, Rect(0, 0, 3, 3), Rect(1, 1, 2, 2));
        // Over the whole hole; over a strip of it, its triangles' centroids on the solid part.
        foreach (var rect in new[] { Rect(0.5, 0.5, 2.5, 2.5), Rect(0.1, 0.1, 2.9, 1.2) })
        {
            var tool = Prism2(rot, 1, 1.5, rect);
            Assert.Same(tool, Flush.Sunk(tool, host));
        }
        // Beside the hole, on the solid part: sunk.
        var beside = Prism2(rot, 1, 1.5, Rect(0.1, 0.1, 2.9, 0.9));
        Assert.NotSame(beside, Flush.Sunk(beside, host));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void A_tool_the_hosts_face_itself_is_sunk_and_joins_into_one_box(double rot)
    {
        var host = Prism2(rot, 0, 1, Rect(0, 0, 2, 2));
        var tool = Prism2(rot, 1, 1.5, Rect(0, 0, 2, 2));
        var sunk = Flush.Sunk(tool, host);
        Assert.NotSame(tool, sunk);
        Clean(Mb.Union([host, sunk])!, 6, 4 * 1.5);
        // The L's own face on the L: sunk too.
        var l = Prism2(rot, 0, 1, L);
        var lt = Prism2(rot, 1, 1.5, L);
        var ls = Flush.Sunk(lt, l);
        Assert.NotSame(lt, ls);
        Clean(Mb.Union([l, ls])!, 8, 3 * 1.5);
    }
}
