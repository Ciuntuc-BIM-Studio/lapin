using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// The shared plan routines of the audit's second stage (D10): one half-plane clip, one box clip, one point-in-polygon test
/// with a tolerance, one centroid. Each is checked against the copy it replaced, on random convex rings.
/// </summary>
public class Polygon2SharedTests
{
    /// <summary>The copy most modules had: a vertex kept at f ≥ 0, a crossing on every edge that changes side.</summary>
    static List<Vec2> OldClip(IReadOnlyList<Vec2> ring, Func<Vec2, double> inside)
    {
        var o = new List<Vec2>();
        for (var i = 0; i < ring.Count; i++)
        {
            Vec2 p = ring[i], q = ring[(i + 1) % ring.Count];
            double dp = inside(p), dq = inside(q);
            if (dp >= 0) o.Add(p);
            if ((dp >= 0) != (dq >= 0)) o.Add(p + (q - p) * (dp / (dp - dq)));
        }
        return o;
    }

    static List<Vec2> WithoutRepeats(List<Vec2> r)
    {
        var o = new List<Vec2>();
        foreach (var p in r) if (o.Count == 0 || (p - o[^1]).Length > 1e-12) o.Add(p);
        while (o.Count > 1 && (o[0] - o[^1]).Length <= 1e-12) o.RemoveAt(o.Count - 1);
        return o;
    }

    static Vec2[] RandomConvex(Random rnd, double scale, Vec2 at)
    {
        var n = rnd.Next(3, 12);
        var angles = Enumerable.Range(0, n).Select(_ => rnd.NextDouble() * 2 * Math.PI).Order().ToArray();
        return [.. angles.Select(a => at + new Vec2(Math.Cos(a), Math.Sin(a)) * scale)];
    }

    [Fact]
    public void The_shared_half_plane_clip_gives_the_old_copies_ring_without_the_repeated_vertex()
    {
        var rnd = new Random(42);
        for (var k = 0; k < 2000; k++)
        {
            var ring = RandomConvex(rnd, 1 + rnd.NextDouble() * 10, new Vec2(rnd.NextDouble() * 100, rnd.NextDouble() * 100));
            var n = new Vec2(rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5).Normalized();
            // Half the lines through a vertex: there the old copy repeated it.
            var c = k % 2 == 0 ? Vec2.Dot(n, ring[rnd.Next(ring.Length)]) : Vec2.Dot(n, ring[0]) + (rnd.NextDouble() - 0.5) * 4;
            double F(Vec2 p) => Vec2.Dot(n, p) - c;
            var before = WithoutRepeats(OldClip(ring, F));
            var now = Polygon2.ClipHalf(ring, F);
            Assert.Equal(before.Count, now.Count);
            for (var i = 0; i < now.Count; i++) Assert.True((now[i] - before[i]).Length < 1e-9);
            Assert.Equal(Math.Abs(Polygon2.SignedArea(OldClip(ring, F))), Math.Abs(Polygon2.SignedArea(now)), 9);
        }
    }

    [Fact]
    public void A_vertex_on_the_line_comes_once_and_a_ring_touching_it_at_a_corner_is_left_a_point()
    {
        Vec2[] square = [new(0, 0), new(2, 0), new(2, 2), new(0, 2)];
        Assert.Equal([new(1, 0), new(2, 0), new(2, 2), new(1, 2)], Polygon2.ClipHalf(square, p => p.X - 1));
        // Through two corners: the triangle on the kept side, its corners once each.
        Assert.Equal(3, Polygon2.ClipHalf(square, p => p.X + p.Y - 2).Count);
        // Touching at a corner only.
        Vec2[] tri = [new(0, 0), new(-1, -1), new(1, -1)];
        Assert.Single(Polygon2.ClipHalf(tri, p => p.Y));
        // The tolerance keeps a vertex a hair outside, the crossing found on the line itself.
        Vec2[] near = [new(0, -1e-10), new(2, -1e-10), new(2, 2), new(0, 2)];
        Assert.Equal(4, Polygon2.ClipHalf(near, p => p.Y, 1e-9).Count);
        Assert.Equal(4, Polygon2.ClipHalf(near, p => p.Y).Count);
        // A vertex out by so little the crossing's fraction rounds to the end: the crossing still comes (a structural
        // piece lost its corner, and its load half its area, when it did not).
        Vec2[] unit = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
        var hair = Polygon2.ClipHalf(unit, p => p.Y == 0 ? 1 : -1e-17);
        Assert.Equal(4, hair.Count);
        Assert.Equal(1, Polygon2.SignedArea(hair), 12);
    }

    [Fact]
    public void The_box_clip_cuts_by_its_four_sides_and_an_infinite_bound_cuts_nothing()
    {
        Vec2[] diamond = [new(0, -2), new(2, 0), new(0, 2), new(-2, 0)];
        var box = Polygon2.ClipBox(diamond, -1, -1, 1, 1);
        Assert.Equal(4, Math.Abs(Polygon2.SignedArea(box)), 12);
        var half = Polygon2.ClipBox(diamond, 0, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity);
        Assert.Equal(4, Math.Abs(Polygon2.SignedArea(half)), 12);
        Assert.Empty(Polygon2.ClipBox(diamond, 3, 3, 4, 4));
    }

    [Fact]
    public void Contains_with_a_tolerance_takes_the_boundary_in()
    {
        Vec2[] square = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
        Assert.True(Polygon2.Contains(square, new Vec2(0.5, 0.5), 1e-6));
        Assert.True(Polygon2.Contains(square, new Vec2(1 + 1e-7, 0.5), 1e-6));
        Assert.False(Polygon2.Contains(square, new Vec2(1 + 1e-7, 0.5)));
        Assert.False(Polygon2.Contains(square, new Vec2(1.1, 0.5), 1e-6));
    }

    [Fact]
    public void The_centroid_is_the_areas_and_holds_far_from_the_origin()
    {
        // An L: its centroid is not its corners' average.
        Vec2[] l = [new(0, 0), new(2, 0), new(2, 1), new(1, 1), new(1, 2), new(0, 2)];
        var c = Polygon2.Centroid(l);
        Assert.Equal(5.0 / 6, c.X, 12);
        Assert.Equal(5.0 / 6, c.Y, 12);
        // The same reversed, and moved to georeferenced coordinates (Stereo 70).
        var far = new Vec2(589_123.25, 412_987.75);
        var moved = Polygon2.Centroid([.. l.Reverse().Select(p => p + far)]);
        Assert.Equal(far.X + 5.0 / 6, moved.X, 7);
        Assert.Equal(far.Y + 5.0 / 6, moved.Y, 7);
        // No area: the points' average.
        Assert.Equal(new Vec2(1, 0), Polygon2.Centroid([new(0, 0), new(2, 0)]));
    }
}
