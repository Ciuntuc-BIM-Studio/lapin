using Lapin.Clash;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// Clash detection's geometry and engine (docs/plan-visualisation.md, stage 2) on closed boxes, ported from BimGo's
/// ClashTests (MIT-0) with their cases and numbers: crossings, touching and distances, inside a box, a duct through a
/// beam, a 3 mm overlap at 5 mm and 1 mm, a pipe sunk 40 mm into a slab (also through a triangle tree), a body inside
/// another, clearance gaps, a test of a set against itself, the limit.
/// </summary>
public class ClashEngineTests
{
    const double Hard = 0.005;

    /// <summary>A closed box's triangles (outward normals), each face split n × n.</summary>
    internal static List<Tri> Box(Vec3 min, Vec3 max, int n = 1)
    {
        var o = new List<Tri>();
        var d = max - min;
        void Face(Vec3 origin, Vec3 u, Vec3 v)
        {
            for (var i = 0; i < n; i++)
                for (var j = 0; j < n; j++)
                {
                    var p = origin + u * (i / (double)n) + v * (j / (double)n);
                    Vec3 a = p + u * (1.0 / n), c = p + v * (1.0 / n), b = a + v * (1.0 / n);
                    o.Add(Tri.Make(p, a, b));
                    o.Add(Tri.Make(p, b, c));
                }
        }
        Face(min, new Vec3(0, d.Y, 0), new Vec3(d.X, 0, 0));
        Face(new Vec3(min.X, min.Y, max.Z), new Vec3(d.X, 0, 0), new Vec3(0, d.Y, 0));
        Face(min, new Vec3(d.X, 0, 0), new Vec3(0, 0, d.Z));
        Face(new Vec3(min.X, max.Y, min.Z), new Vec3(0, 0, d.Z), new Vec3(d.X, 0, 0));
        Face(min, new Vec3(0, 0, d.Z), new Vec3(0, d.Y, 0));
        Face(new Vec3(max.X, min.Y, min.Z), new Vec3(0, d.Y, 0), new Vec3(0, 0, d.Z));
        return o;
    }

    static ClashBody Body(string id, Vec3 min, Vec3 max, int n = 1) { var t = Box(min, max, n); return new(id, min, max, () => t); }

    static ClashRun Run(ClashBody[] bodies, int[] a, int[]? b, bool clearance = false, double tolerance = Hard, Func<int, int, bool>? skip = null, int max = ClashEngine.MaxClashesPerTest) =>
        ClashEngine.Run(new ClashJob(bodies, a, b, clearance, tolerance, max, skip, Threads: 2));

    static readonly (Vec3, Vec3) Beam = (new(0, 0, 2.6), new(4, 0.3, 3.0));
    static readonly (Vec3, Vec3) Duct = (new(1.8, -2, 2.7), new(2.2, 2, 2.9));

    [Fact]
    public void Two_triangles_cross_touch_or_lie_apart()
    {
        var t = Tri.Make(new(0, 0, 0), new(2, 0, 0), new(0, 2, 0));
        var crossing = Tri.Make(new(0.5, 0.5, -1), new(0.5, 0.5, 1), new(1.5, 0.5, 0));
        Assert.True(Collisions.Crosses(t, crossing, out _, out _, out var proper));
        Assert.True(proper);
        // Standing on the face: touching, never a proper crossing.
        var standing = Tri.Make(new(0.5, 0.5, 0), new(0.6, 0.5, 1), new(0.5, 0.6, 1));
        if (Collisions.Crosses(t, standing, out _, out _, out proper)) Assert.False(proper);
        // In one plane: never crossing.
        Assert.False(Collisions.Crosses(t, Tri.Make(new(0.2, 0.2, 0), new(1, 0.2, 0), new(0.2, 1, 0)), out _, out _));
        // Parallel, 0.3 apart.
        Assert.Equal(0.3, Collisions.Distance(t, Tri.Make(new(0, 0, 0.3), new(2, 0, 0.3), new(0, 2, 0.3)), out _, out _), 6);
    }

    [Fact]
    public void A_point_is_inside_a_closed_box_or_not()
    {
        var mesh = Box(Vec3.Zero, new(1, 1, 1), 3).ToArray();
        Assert.True(Collisions.Inside(new(0.5, 0.5, 0.5), mesh));
        Assert.True(Collisions.Inside(new(0.999, 0.999, 0.999), mesh));
        Assert.False(Collisions.Inside(new(1.5, 0.5, 0.5), mesh));
        var tree = new TriangleTree(mesh);
        Assert.True(tree.Inside(new(0.5, 0.5, 0.5)));
        Assert.False(tree.Inside(new(1.5, 0.5, 0.5)));
        Assert.Equal(0.5, tree.Cast(new(0.5, 0.5, 0.5), new(0, 0, 1))!.Value, 6);
    }

    [Fact]
    public void A_duct_through_a_beam_is_as_deep_as_the_duct_is_high()
    {
        var run = Run([Body("beam", Beam.Item1, Beam.Item2), Body("duct", Duct.Item1, Duct.Item2)], [0], [1]);
        var hit = Assert.Single(run.Hits);
        Assert.Equal(0, hit.A);
        Assert.True(hit.Crossing);
        Assert.Equal(0.2, hit.Distance, 0.002);
        Assert.Equal(2, hit.Point.X, 0.05);
        Assert.Equal(0.15, hit.Point.Y, 0.05);
        Assert.Equal(2.8, hit.Point.Z, 0.05);
    }

    [Fact]
    public void Touching_faces_never_clash_and_a_shallow_overlap_follows_the_tolerance()
    {
        var a = Body("a", Vec3.Zero, new(1, 1, 1));
        Assert.Empty(Run([a, Body("b", new(1, 0, 0), new(2, 1, 1))], [0], [1]).Hits);
        ClashBody[] overlap3mm = [a, Body("b", new(0.997, 0, 0), new(2, 1, 1))];
        Assert.Empty(Run(overlap3mm, [0], [1]).Hits);
        var tight = Run(overlap3mm, [0], [1], tolerance: 0.001);
        Assert.Equal(0.003, Assert.Single(tight.Hits).Distance, 0.0005);
    }

    [Fact]
    public void A_pipe_sunk_into_a_slab_counts_by_its_buried_corners()
    {
        var pipe = (new Vec3(0.5, 1, 0.16), new Vec3(3.5, 1.1, 0.26));
        // 8: 768 triangles, through the triangle tree.
        foreach (var n in new[] { 1, 8 })
        {
            var run = Run([Body("slab", Vec3.Zero, new(4, 4, 0.2), n), Body("pipe", pipe.Item1, pipe.Item2)], [0], [1]);
            Assert.Equal(0.04, Assert.Single(run.Hits).Distance, 0.002);
        }
        Assert.Empty(Run([Body("slab", Vec3.Zero, new(4, 4, 0.2)), Body("pipe", new(0.5, 1, 0.197), new(3.5, 1.1, 0.3))], [0], [1]).Hits);
    }

    [Fact]
    public void A_body_wholly_inside_another_is_found_as_deep_as_it_is_narrow_unless_asked_as_BimGo_does()
    {
        ClashBody[] bodies = [Body("slab", Vec3.Zero, new(4, 4, 4)), Body("pipe", new(1, 1, 1), new(2, 2, 2.5))];
        var hit = Assert.Single(Run(bodies, [0], [1]).Hits);
        Assert.True(hit.Crossing);
        Assert.Equal(1, hit.Distance, 6);
        Assert.Equal(1.5, hit.Point.X, 6);
        // BimGo leaves it out (its own test, Hard_ContainedElementIsLeftOut).
        Assert.Empty(ClashEngine.Run(new ClashJob(bodies, [0], [1], Threads: 2, Contained: false)).Hits);
        // Inside the other's box but not inside the other (an L of two boxes round it): no clash.
        var l = Box(Vec3.Zero, new(4, 1, 4)).Concat(Box(new(0, 1, 0), new(1, 4, 4))).ToList();
        Assert.Empty(Run([new ClashBody("L", Vec3.Zero, new(4, 4, 4), () => l), Body("pipe", new(2, 2, 1), new(3, 3, 2))], [0], [1]).Hits);
    }

    [Fact]
    public void Clearance_finds_gaps_under_the_distance_crossings_and_touching()
    {
        ClashBody[] gap = [Body("a", Vec3.Zero, new(1, 1, 1)), Body("b", new(1.03, 0, 0), new(2, 1, 1))];
        var near = Assert.Single(Run(gap, [0], [1], true, 0.05).Hits);
        Assert.False(near.Crossing);
        Assert.Equal(0.03, near.Distance, 0.001);
        Assert.Empty(Run(gap, [0], [1], true, 0.02).Hits);
        Assert.True(Assert.Single(Run([Body("beam", Beam.Item1, Beam.Item2), Body("duct", Duct.Item1, Duct.Item2)], [0], [1], true, 0.05).Hits).Crossing);
        var touching = Assert.Single(Run([Body("a", Vec3.Zero, new(1, 1, 1)), Body("b", new(1, 0, 0), new(2, 1, 1))], [0], [1], true, 0.05).Hits);
        Assert.False(touching.Crossing);
        Assert.Equal(0, touching.Distance, 5);
    }

    [Fact]
    public void Pairs_meant_to_meet_are_skipped_and_a_set_against_itself_counts_each_pair_once()
    {
        ClashBody[] wallDoor = [Body("wall", Vec3.Zero, new(4, 0.2, 3)), Body("door", new(1, -0.1, 0), new(2, 0.3, 2.1))];
        Assert.Empty(Run(wallDoor, [0], [1], skip: (i, j) => true).Hits);
        Assert.Single(Run(wallDoor, [0], [1]).Hits);
        ClashBody[] three = [Body("d1", Vec3.Zero, new(4, 0.4, 0.4)), Body("d2", new(1, -1, 0.1), new(1.4, 1, 0.3)), Body("d3", new(8, 8, 8), new(9, 9, 9))];
        Assert.Single(Run(three, [0, 1, 2], null).Hits);
        Assert.Single(Run(three, [0, 1], [0, 1, 2]).Hits);
    }

    [Fact]
    public void A_run_stops_at_its_limit()
    {
        var bodies = new List<ClashBody> { Body("floor", Vec3.Zero, new(10, 10, 0.2)) };
        for (var i = 0; i < 6; i++) bodies.Add(Body($"pipe{i}", new(i + 0.2, 1, 0.1), new(i + 0.5, 2, 0.5)));
        var run = ClashEngine.Run(new ClashJob(bodies, [0], [1, 2, 3, 4, 5, 6], MaxClashes: 3, Threads: 1));
        Assert.True(run.Truncated);
        Assert.Equal(3, run.Hits.Count);
    }

    [Fact]
    public void Numbers_are_read_as_people_write_them()
    {
        Assert.True(SearchSets.TryNumber("600 mm", out var a) && a == 600);
        Assert.True(SearchSets.TryNumber("1,200", out var b) && b == 1200);
        Assert.True(SearchSets.TryNumber("2,5", out var c) && c == 2.5);
        Assert.True(SearchSets.TryNumber("DN −3", out var d) && d == -3);
        Assert.False(SearchSets.TryNumber("none", out _));
    }
}
