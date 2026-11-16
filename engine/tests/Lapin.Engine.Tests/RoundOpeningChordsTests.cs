using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Coordination;

namespace Lapin.Engine.Tests;

/// <summary>
/// The audit's D11: round openings and provisions are made by <see cref="Chords"/> (one rule for every curve), not with a fixed
/// 32 sides, their sides outside the circle so what passes through still clears; their ids stay as they were.
/// </summary>
public class RoundOpeningChordsTests
{
    static void AutoCut(MepFixture f) => f.Run("cutOrder.setMep", new JsonObject { ["cut"] = true });

    /// <summary>Whether a circle about <paramref name="c"/> lies inside the convex outline (sampled finely, a nanometre spared).</summary>
    static bool Clears(IReadOnlyList<Vec2> outline, Vec2 c, double r) =>
        Enumerable.Range(0, 720).All(k => Polygon2.Contains(outline, c + new Vec2(Math.Cos(k * Math.PI / 360), Math.Sin(k * Math.PI / 360)) * (r - 1e-9)));

    [Fact]
    public void The_ring_has_the_sides_chords_gives_and_a_circumscribed_one_holds_the_circle()
    {
        foreach (var r in new[] { 0.02, 0.075, 0.5, 3.0 })
        {
            var inscribed = Chords.Ring(new Vec2(1, 2), r);
            var outside = Chords.Ring(new Vec2(1, 2), r, circumscribed: true);
            Assert.Equal(Chords.Circle(r), inscribed.Count);
            Assert.Equal(Chords.Circle(r), outside.Count);
            Assert.True(Polygon2.SignedArea(outside) > 0);
            Assert.All(inscribed, p => Assert.Equal(r, (p - new Vec2(1, 2)).Length, 12));
            Assert.True(Clears(outside, new Vec2(1, 2), r));
            Assert.False(Clears(inscribed, new Vec2(1, 2), r));
        }
        var arc = Chords.Arc(new Vec2(0, 0), 0.9, 0, Math.PI / 2, 8);
        Assert.Equal(Chords.Count(0.9, Math.PI / 2, 8) + 1, arc.Count);
        Assert.Equal(new Vec2(0.9, 0), arc[0]);
        Assert.Equal(0, arc[^1].X, 12);
    }

    [Fact]
    public void A_round_provision_in_a_wall_clears_its_pipe_and_one_in_a_floor_too()
    {
        var f = new MepFixture();
        AutoCut(f);
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -3, ["y1"] = 1, ["x2"] = 3, ["y2"] = 1, ["join"] = false })!["id"]!;
        var system = f.System(defaults: new JsonObject { ["insulation"] = 0 });
        var pipe = f.Draw(system, MepFixture.Pts((0, -1), (0, 3)), more: new JsonObject { ["pair"] = false, ["size"] = 0.11, ["elevation"] = 1.0 });
        var p = Assert.Single(Lapin.Platform.Coordination.Provisions.Of(f.Model, f.Session.Catalog));
        Assert.Equal((pipe, wall, OpeningVoids.Round), (p.SourceId, p.HostId, p.Shape));
        // Seen along the pipe (the wall's plane, x and z): the void holds the opening's whole circle, and so the pipe's.
        var section = Polygon2.ConvexHull(p.Void.Vertices.Select(v => new Vec2(v.X, v.Z)));
        Assert.Equal(Chords.Circle(p.Width / 2), section.Length);
        Assert.True(Clears(section, new Vec2(0, 1.0), p.Width / 2));
        Assert.True(Clears(section, new Vec2(0, 1.0), 0.055));
        var key = p.Key;
        // Built again, the same provision: its key (what its void's id comes from) stays.
        f.Session.Geometry(wall);
        Assert.Equal(key, Assert.Single(Lapin.Platform.Coordination.Provisions.Of(f.Model, f.Session.Catalog)).Key);

        var g = new MepFixture();
        AutoCut(g);
        g.Run("architecture.addSlab", new JsonObject { ["storeyId"] = g.Storey, ["points"] = MepFixture.Pts((0, 0), (4, 0), (4, 3), (0, 3)) });
        var vent = g.Draw(g.System(defaults: new JsonObject { ["insulation"] = 0 }), MepFixture.Pts((2, 1.5), (2.00002, 1.5)), more: new JsonObject { ["pair"] = false, ["size"] = 0.2 });
        g.Session.Edit("levels", e =>
        {
            var x = e.Model.Get(vent)!;
            var a = x.CloneAttrs();
            a["levels"] = new JsonArray(-1.0, 2.0);
            e.Replace(x.With(attrs: a));
        });
        var q = Assert.Single(Lapin.Platform.Coordination.Provisions.Of(g.Model, g.Session.Catalog));
        Assert.Equal(OpeningVoids.Round, q.Shape);
        var plan = Polygon2.ConvexHull(q.Void.Vertices.Select(v => v.Xy));
        Assert.Equal(Chords.Circle(q.Width / 2), plan.Length);
        // About the riser's own axis (its run's middle, a hair off the point drawn).
        var centre = Polygon2.Centroid(plan);
        Assert.True((centre - new Vec2(2, 1.5)).Length < 1e-4);
        Assert.True(Clears(plan, centre, q.Width / 2));
        Assert.True(Clears(plan, centre, 0.1));
    }

    [Fact]
    public void A_round_floor_opening_made_where_a_provision_was_holds_its_whole_circle()
    {
        var f = new Fixture(new ArchitectureModule());
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = MepFixture.Pts((0, 0), (4, 0), (4, 3), (0, 3)) })!["id"]!;
        var maker = f.Session.Catalog.All<IOpeningMaker>().Single();
        string? id = null;
        f.Session.Edit("open", e => id = maker.MakeAt(e, e.Model.Get(slab)!, new Vec3(2, 1.5, 0), 0.3, 0.3, round: true));
        Assert.NotNull(id);
        var hole = Assert.Single(Slabs.Holes(f.Model, slab)).Contour;
        Assert.Equal(Chords.Circle(0.15), hole.Count);
        Assert.True(Clears(hole, new Vec2(2, 1.5), 0.15));
    }
}
