using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>
/// The audit's second stage, the smaller shared helpers: lines meeting and segments' distance by one rule whatever their
/// scale, the roof's edge offset by the polygon's, one oriented extrusion, a volume that holds at georeferenced coordinates,
/// and one way for new code to derive a stable GlobalId.
/// </summary>
public class SharedHelperTests
{
    [Fact]
    public void Lines_meet_by_one_rule_whatever_their_length_and_parallel_ones_do_not()
    {
        Assert.Equal(new Vec2(1, 1), Polyline2.LineIntersection(new Vec2(0, 1), new Vec2(1, 0), new Vec2(1, 0), new Vec2(0, 1)));
        // The same lines given by long or short vectors meet at the same point.
        Assert.Equal(new Vec2(1, 1), Polyline2.LineIntersection(new Vec2(0, 1), new Vec2(1e-6, 0), new Vec2(1, 0), new Vec2(0, 1e5)));
        Assert.Null(Polyline2.LineIntersection(new Vec2(0, 0), new Vec2(1e-6, 0), new Vec2(0, 1), new Vec2(2e-6, 1e-17)));
        Assert.Null(Polyline2.LineIntersection(new Vec2(0, 0), new Vec2(1, 1), new Vec2(0, 1), new Vec2(3, 3)));
    }

    [Fact]
    public void Two_segments_are_apart_by_their_nearest_points_and_crossing_ones_not_at_all()
    {
        Assert.Equal(0, Polyline2.SegmentsDistance(new Vec2(0, 0), new Vec2(2, 2), new Vec2(0, 2), new Vec2(2, 0)));
        Assert.Equal(1, Polyline2.SegmentsDistance(new Vec2(0, 0), new Vec2(2, 0), new Vec2(1, 1), new Vec2(1, 3)), 12);
        Assert.Equal(0, Polyline2.SegmentsDistance(new Vec2(0, 0), new Vec2(2, 0), new Vec2(1, 0), new Vec2(1, 3)), 12);
        Assert.Equal(Math.Sqrt(2), Polyline2.SegmentsDistance(new Vec2(0, 0), new Vec2(1, 0), new Vec2(2, 1), new Vec2(3, 1)), 12);
    }

    [Fact]
    public void An_oriented_extrusion_is_positive_either_way_and_leaves_the_ring_as_it_was()
    {
        List<Vec3> ring = [new(0, 0, 0), new(2, 0, 0), new(2, 0, 1), new(0, 0, 1)];
        var copy = ring.ToList();
        foreach (var along in new[] { new Vec3(0, 3, 0), new Vec3(0, -3, 0) })
        {
            var s = Prism.ExtrudeOriented(ring, along)!;
            Assert.Equal(6, Measure.SignedVolume(s), 9);
        }
        Assert.Equal(copy, ring);
    }

    [Fact]
    public void A_small_body_far_from_the_origin_measures_its_volume()
    {
        // A 0.1 m cube in Stereo 70 coordinates, at a height above sea level.
        var at = new Vec3(589_123.37, 412_987.61, 312.4);
        List<Vec3> square = [at, at + new Vec3(0.1, 0, 0), at + new Vec3(0.1, 0.1, 0), at + new Vec3(0, 0.1, 0)];
        var cube = Prism.Extrude(square, null, new Vec3(0, 0, 0.1))!;
        Assert.Equal(0.001, Measure.SignedVolume(cube), 12);
        var near = Prism.Extrude([.. square.Select(p => p - at)], null, new Vec3(0, 0, 0.1))!;
        Assert.Equal(Measure.Volume(near), Measure.Volume(cube), 12);
    }

    [Fact]
    public void A_derived_global_id_is_valid_stable_and_its_own_for_each_seed_and_key()
    {
        var a = GlobalId.Derive("2aRb$K8Qn1Bf9zYh0TWl3p", "body");
        Assert.True(GlobalId.IsValid(a));
        Assert.Equal(a, GlobalId.Derive("2aRb$K8Qn1Bf9zYh0TWl3p", "body"));
        Assert.NotEqual(a, GlobalId.Derive("2aRb$K8Qn1Bf9zYh0TWl3p", "axis"));
        Assert.NotEqual(a, GlobalId.Derive("2aRb$K8Qn1Bf9zYh0TWl3q", "body"));
        // Pinned: the same on every machine, desktop and browser, from build to build.
        Assert.Equal(GlobalId.FromGuid(new Guid(System.Security.Cryptography.SHA256.HashData("seed\nkey"u8)[..16], bigEndian: true)), GlobalId.Derive("seed", "key"));
    }
}
