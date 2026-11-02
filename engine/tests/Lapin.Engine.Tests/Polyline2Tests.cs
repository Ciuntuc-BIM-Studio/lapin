using Lapin.Geometry;
using Lapin.Mep;

namespace Lapin.Engine.Tests;

/// <summary>The plan polyline routines the modules share: one offset for a pipe and a drafted offset, one crossing test, one dedupe.</summary>
public class Polyline2Tests
{
    [Fact]
    public void An_offset_mitres_its_corners_a_pipe_keeping_a_sharp_turn_within_five_offsets()
    {
        // An L, 1 to the left: the corner on the bisector, out √2.
        var l = Polyline2.Offset([new(0, 0), new(10, 0), new(10, 10)], 1);
        Assert.Equal([new Vec2(0, 1), new Vec2(9, 1), new Vec2(9, 10)], l.Select(p => new Vec2(Math.Round(p.X, 9), Math.Round(p.Y, 9))));
        // A hairpin of 10°: the drafted offset goes far out (1 / sin 5°), the pipe's stops at five.
        Vec2[] hairpin = [new(0, 0), new(10, 0), new(0, Math.Tan(10 * Math.PI / 180) * 10)];
        Assert.InRange((Polyline2.Offset(hairpin, 1)[1] - hairpin[1]).Length, 11.4, 11.5);
        Assert.Equal(5, (Pipes.Offset(hairpin, 1)[1] - hairpin[1]).Length, 9);
        // Closed: every point a corner; a counter-clockwise loop's left is its inside.
        var square = Polyline2.Offset([new(0, 0), new(4, 0), new(4, 4), new(0, 4)], 1, closed: true);
        Assert.Equal(new Vec2(1, 1), new Vec2(Math.Round(square[0].X, 9), Math.Round(square[0].Y, 9)));
    }

    [Fact]
    public void Segments_cross_only_strictly_inside_both_and_repeated_points_go()
    {
        Assert.True(Polyline2.Crosses(new(0, 0), new(2, 2), new(0, 2), new(2, 0), out var t));
        Assert.Equal(0.5, t, 12);
        Assert.False(Polyline2.Crosses(new(0, 0), new(2, 0), new(2, 0), new(3, 1)));   // a shared end
        Assert.False(Polyline2.Crosses(new(0, 0), new(2, 0), new(1, 0), new(3, 0)));   // overlapping, parallel
        Assert.Equal([new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 1)], Polyline2.Dedupe([new(0, 0), new(1, 0), new(1, 1e-7), new(1, 1), new(0, 1e-8)], closed: true));
        Assert.Equal(2, Polyline2.Length([new(0, 0), new(1, 0), new(1, 1)]));
        Assert.Equal(1, Polyline2.SegmentDistance(new(5, 1), new(0, 0), new(10, 0)));
    }
}
