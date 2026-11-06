using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Three walls or more meeting at one point (a tester's report, 06.11.2026: a V's corner with a third wall going on from
/// it left its square end under the V's mitre): every end there is joined and cut as a fan, each face to its neighbour's.
/// </summary>
public class WallNodeTests
{
    /// <summary>A wall's body in plan, in world space, from its frame and its end cuts.</summary>
    static Vec2[] Body(JoinWall w)
    {
        var o = WallJoins.Outline(w);
        var x = (w.End - w.Start).Normalized();
        return o.Corners.Select(c => w.Start + x * c.X + x.Perp * c.Y).ToArray();
    }

    static bool Inside(Vec2[] poly, Vec2 p)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) inside = !inside;
        return inside;
    }

    /// <summary>The share of points round the node that two bodies both cover, and that none covers inside the walls' reach.</summary>
    static double Overlap(IReadOnlyList<Vec2[]> bodies, Vec2 node, double r)
    {
        int both = 0, all = 0;
        for (var x = -r; x <= r; x += 0.01)
            for (var y = -r; y <= r; y += 0.01)
            {
                var p = node + new Vec2(x, y);
                var n = bodies.Count(b => Inside(b, p));
                if (n > 0) all++;
                if (n > 1) both++;
            }
        return (double)both / Math.Max(1, all);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(40)]
    [InlineData(70)]
    public void Three_walls_at_a_point_are_cut_as_a_fan_with_no_body_over_another(double degrees)
    {
        var r = degrees * Math.PI / 180;
        var node = new Vec2(4, 0);
        // A V opening to the left, its corner at the node, and a third wall going on to the right; asymmetric bands (a finish on one side).
        var walls = new[]
        {
            (Wall: new JoinWall(new Vec2(0, -4 * Math.Tan(r / 2)), node, -0.14, 0.285), End: 1),
            (Wall: new JoinWall(new Vec2(0, 4 * Math.Tan(r / 2)), node, -0.14, 0.285), End: 1),
            (Wall: new JoinWall(node, new Vec2(9, 0), -0.14, 0.285), End: 0),
        };
        var bodies = new List<Vec2[]>();
        for (var i = 0; i < walls.Length; i++)
        {
            var (at, cut) = Joins.Fan(walls[i].Wall, walls[i].End, walls.Where((_, k) => k != i).Select(x => (x.Wall, x.End)).ToList());
            var w = walls[i].End == 0 ? walls[i].Wall with { Start = at, StartCut = cut } : walls[i].Wall with { End = at, EndCut = cut };
            bodies.Add(Body(w));
        }
        Assert.True(Overlap(bodies, node, 0.8) < 0.01, "the bodies overlap at the node");
    }

    [Fact]
    public void A_third_wall_drawn_from_a_corner_joins_both_and_its_end_is_no_longer_square()
    {
        var f = new Fixture();
        var a = f.Wall(0, -1, 4, 0);
        var b = f.Wall(0, 1, 4, 0);
        var c = f.Wall(4, 0, 9, 0);
        Assert.NotNull(Joins.Between(f.Model, c, a));
        Assert.NotNull(Joins.Between(f.Model, c, b));
        var paths = f.Session.Catalog.Get<IPathElements>()!;
        var ends = Joins.EndsOf(f.Model, f.Model.Get(c)!, paths, []);
        Assert.NotNull(ends[0]);
        Assert.NotEqual(ends[0]!.Value.Cut.Left, ends[0]!.Value.Cut.Right, 3);
    }
}
