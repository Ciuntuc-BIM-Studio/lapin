using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing.Views;
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

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    /// <summary>The plan's cut areas with their materials' names.</summary>
    static List<(Vec2[][] Loops, string Material)> PlanCuts(Fixture f)
    {
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["layers"] = "all" });
        var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, plan)!);
        Assert.True(d != null, problem);
        return d!.Cuts.Select(c => (c.Loops.ToArray(), f.Model.Get(c.Material ?? "")?.Name ?? "")).ToList();
    }

    static bool InCut(Vec2[][] loops, Vec2 p) => loops.Count(l => Inside(l, p)) % 2 == 1;

    /// <summary>Whether a point lies on an edge of a cut (a sample on the line two areas share, counted in neither).</summary>
    static bool OnEdge(IEnumerable<(Vec2[][] Loops, string Material)> cuts, Vec2 p) =>
        cuts.Any(c => c.Loops.Any(l => l.Select((a, i) => Polyline2.SegmentDistance(p, a, l[(i + 1) % l.Length])).Min() < 1e-4));

    /// <summary>
    /// Every point of the walls' bodies round the node (each wall's band from face to face along its run, within
    /// <paramref name="r"/>) is cut by exactly one area: no hole at the node, no area over another; the node itself is brick.
    /// </summary>
    static void CleanNode(Fixture f, Vec2 node, double r)
    {
        var cuts = PlanCuts(f);
        var holes = new List<Vec2>();
        var overlaps = new List<Vec2>();
        foreach (var w in f.Model.OfClass(IfcClass.Wall))
        {
            var (a, b) = (Joins.Start(w), Joins.End(w));
            var frame = Walls.Frame(w, f.Model)!;
            var d = (b - a).Normalized();
            for (var t = frame.YMin + 0.003; t < frame.YMax; t += 0.006)
                for (var s = -r + 0.0013; s <= r; s += 0.006)
                {
                    var p = node + d * s + d.Perp * t;
                    // Only what lies on this wall's run (the node and on along it), inside the plan.
                    var along = Vec2.Dot(p - a, d);
                    if (along < -1e-9 || along > (b - a).Length + 1e-9) continue;
                    var n = cuts.Count(c => InCut(c.Loops, p));
                    if (n == 0 && !OnEdge(cuts, p)) holes.Add(p);
                    if (n > 1 && !OnEdge(cuts, p)) overlaps.Add(p);
                }
        }
        Assert.True(holes.Count == 0, $"{holes.Count} points round the node are not cut, e.g. {string.Join(" ", holes.Take(3))}");
        Assert.True(overlaps.Count == 0, $"{overlaps.Count} points round the node are cut twice, e.g. {string.Join(" ", overlaps.Take(3))}");
        Assert.Equal("Brick", cuts.Single(c => InCut(c.Loops, node + new Vec2(1e-4, 2e-4))).Material);
    }

    static string PlasteredBrick(Fixture f) => f.Type("Plastered brick", ("Plaster", 0.015, false), ("Brick", 0.2, true), ("Plaster", 0.015, false));

    /// <summary>
    /// The forum's topic 164: two walls join clean, a third at the same point did not: a T of three walls each ending at the
    /// node (the through wall drawn in two), an L corner with a third wall into it, four walls ending at one point, layered
    /// walls of two types, two walls ending on a run at one point, a wall drawn through an L corner: the layers' cleanup
    /// holds, the cores meeting at the node, no hole nor overlap.
    /// </summary>
    [Theory]
    [InlineData("tee")]
    [InlineData("teeOtherOrder")]
    [InlineData("cornerAndDiagonal")]
    [InlineData("cross")]
    [InlineData("teeMixedTypes")]
    [InlineData("y")]
    [InlineData("twoOnARun")]
    [InlineData("runThroughACorner")]
    public void Three_walls_or_more_ending_at_one_point_leave_no_hole_and_their_layers_clean_up(string layout)
    {
        var f = new Fixture();
        var t = PlasteredBrick(f);
        var node = new Vec2(5, 0);
        void W(double x1, double y1, double x2, double y2, string type) => f.Wall(x1, y1, x2, y2, type);
        switch (layout)
        {
            case "tee": W(0, 0, 5, 0, t); W(5, 0, 10, 0, t); W(5, 0, 5, 4, t); break;
            case "teeOtherOrder": W(5, 4, 5, 0, t); W(5, 0, 0, 0, t); W(5, 0, 10, 0, t); break;
            case "cornerAndDiagonal": W(0, 0, 5, 0, t); W(5, 0, 5, 4, t); W(5, 0, 9, 3, t); break;
            case "cross": W(0, 0, 5, 0, t); W(5, 0, 10, 0, t); W(5, 0, 5, 4, t); W(5, 0, 5, -4, t); break;
            case "teeMixedTypes":
                var thick = f.Type("Insulated brick", ("Render", 0.01, false), ("Mineral wool", 0.1, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
                W(0, 0, 5, 0, thick); W(5, 0, 10, 0, thick); W(5, 0, 5, 4, t);
                break;
            case "twoOnARun": W(0, 0, 10, 0, t); W(5, 0, 5, 4, t); W(5, 0, 9, 3, t); break;
            // An L corner first, then a wall drawn on through its point.
            case "runThroughACorner": W(5, 0, 5, 4, t); W(5, 0, 9, 3, t); W(0, 0, 10, 0, t); break;
            default: W(5, 0, 0, 0, t); W(5, 0, 8, 4, t); W(5, 0, 8, -4, t); break;
        }
        var paths = f.Session.Catalog.Get<IPathElements>()!;
        foreach (var w in f.Model.OfClass(IfcClass.Wall))
            for (var end = 0; end < 2; end++)
                if (((end == 0 ? Joins.Start(w) : Joins.End(w)) - node).Length < 1e-6)
                    Assert.True(Joins.NodeOf(f.Model, w, end) != null, $"{w.Name} is not joined at the node");
        CleanNode(f, node, 0.6);
    }

    /// <summary>At a T drawn as three walls, the through wall's plaster on the third wall's side turns into the third wall's plaster: plaster meets plaster at each inner corner.</summary>
    [Fact]
    public void At_a_tee_of_three_walls_the_plasters_turn_the_inner_corners()
    {
        var f = new Fixture();
        var t = PlasteredBrick(f);
        f.Wall(0, 0, 5, 0, t);
        f.Wall(5, 0, 10, 0, t);
        f.Wall(5, 0, 5, 4, t);
        var cuts = PlanCuts(f);
        string At(double x, double y) => cuts.Single(c => InCut(c.Loops, new Vec2(x, y))).Material;
        // The inner corners' plaster, both sides of the third wall; the free face's plaster goes on across the node.
        Assert.Equal("Plaster", At(4.89, 0.11));
        Assert.Equal("Plaster", At(5.11, 0.11));
        Assert.Equal("Plaster", At(5.0, -0.11));
        // The cores meet: brick from wall to wall across the node.
        Assert.Equal("Brick", At(5.0, 0.09));
        Assert.Equal("Brick", At(5.0, -0.09));
        Assert.Equal("Brick", At(4.95, 0.05));
    }
}
