using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A bare opening is picked in every 2D view (forum topic 72): a section and an elevation carry a pick mark of its outline
/// where they cut it or see it (none where the view hides it), and a plan a hit outline whatever its host.
/// </summary>
public class OpeningPickTests
{
    static (Fixture F, string Wall, string Opening) WallWithOpening()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var wall = f.Wall(0, 0, 6, 0);
        var opening = (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["sill"] = 0.8, ["width"] = 1.2, ["height"] = 1.0 })!["id"]!;
        return (f, wall, opening);
    }

    static JsonObject? MarkOf(Fixture f, string view, string id) =>
        Views.Drawing(f.Session.Reader, f.Model.Get(view)!)["marks"]!.AsArray().OfType<JsonObject>().FirstOrDefault(m => (string?)m["id"] == id);

    /// <summary>The mark's loops' bounds (x, y in the view).</summary>
    static (double X0, double Y0, double X1, double Y1) LoopBounds(JsonObject mark)
    {
        var v = mark["data"]!["loops"]!.AsArray().SelectMany(l => l!.AsArray().Select(x => (double)x!)).ToList();
        var xs = v.Where((_, i) => i % 2 == 0).ToList();
        var ys = v.Where((_, i) => i % 2 == 1).ToList();
        return (xs.Min(), ys.Min(), xs.Max(), ys.Max());
    }

    [Fact]
    public void A_section_through_a_wall_opening_picks_it_by_its_cut_outline()
    {
        var (f, wall, opening) = WallWithOpening();
        // Across the wall, through the opening's middle.
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 2), ["name"] = "A" })!["id"]!;
        var mark = MarkOf(f, section, opening);
        Assert.NotNull(mark);
        Assert.True((bool?)mark!["pick"]);
        Assert.NotEmpty(mark["segments"]!.AsArray());
        var (_, y0, _, y1) = LoopBounds(mark);
        // Its outline from the sill to the head, over the storey's floor.
        Assert.Equal(0.8, y0, 3);
        Assert.Equal(1.8, y1, 3);
        // The wall keeps its own.
        Assert.NotNull(MarkOf(f, section, wall));
    }

    [Fact]
    public void An_elevation_sees_a_wall_opening_and_picks_it_by_its_outline()
    {
        var (f, _, opening) = WallWithOpening();
        var elevation = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["start"] = new JsonArray(-2, -4), ["end"] = new JsonArray(8, -4), ["name"] = "South" })!["id"]!;
        var mark = MarkOf(f, elevation, opening);
        Assert.NotNull(mark);
        var (x0, y0, x1, y1) = LoopBounds(mark!);
        Assert.Equal(1.2, x1 - x0, 3);
        Assert.Equal(0.8, y0, 3);
        Assert.Equal(1.8, y1, 3);
    }

    [Fact]
    public void An_opening_the_view_does_not_show_has_no_pick_mark()
    {
        var (f, _, opening) = WallWithOpening();
        // A floor, so the elevation still draws something once its walls are hidden.
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 3), new JsonArray(0, 3)) });
        var elevation = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["start"] = new JsonArray(-2, -4), ["end"] = new JsonArray(8, -4), ["name"] = "South" })!["id"]!;
        Assert.NotNull(MarkOf(f, elevation, opening));
        f.Run("view.graphics", new JsonObject { ["id"] = elevation, ["category"] = "walls", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Null(MarkOf(f, elevation, opening));
        // Behind a wall in front of it: not seen, no mark.
        var g = WallWithOpening();
        g.F.Wall(-1, -1, 7, -1);
        var e2 = (string)g.F.Run("view.create", new JsonObject { ["kind"] = "elevation", ["start"] = new JsonArray(-2, -4), ["end"] = new JsonArray(8, -4), ["name"] = "South" })!["id"]!;
        Assert.Null(MarkOf(g.F, e2, g.Opening));
    }

    static PlanItem? Hit(Fixture f, string id) => f.Session.Geometry(id)?.Plan?.FirstOrDefault(p => p.Kind == "hit");

    [Fact]
    public void A_floor_opening_has_a_hit_outline_in_plan()
    {
        var f = new Fixture(new ArchitectureModule());
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 4), new JsonArray(0, 4)) })!["id"]!;
        var hole = (string)f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["points"] = new JsonArray(new JsonArray(1, 1), new JsonArray(3, 1), new JsonArray(3, 2), new JsonArray(1, 2)) })!["id"]!;
        var hit = Hit(f, hole);
        Assert.NotNull(hit);
        // Its edge picks it (what stands in the hole, a stair, keeps its own clicks): the outline closed on itself.
        Assert.Equal(hit!.Points[0], hit.Points[^1]);
        Assert.Equal(5, hit.Points.Count);
        Assert.Contains(new Vec2(3, 2), hit.Points);
    }

    [Fact]
    public void An_opening_with_a_contour_in_a_host_that_is_no_wall_or_floor_has_a_hit_outline()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 6, 0);
        string id = "";
        f.Session.Edit("test", edit =>
        {
            var host = new Entity(edit.NewId(), IfcClass.Beam, "Beam", new JsonObject());
            edit.Add(host);
            var o = new Entity(edit.NewId(), IfcClass.OpeningElement, "Opening", new JsonObject { ["contour"] = new JsonArray(new JsonArray(0, 0), new JsonArray(1, 0), new JsonArray(1, 1)) }, "OPENING");
            edit.Add(o);
            edit.AddRelation(new Relation(edit.NewId(), IfcClass.RelVoidsElement, host.Id, [o.Id]));
            id = o.Id;
        });
        Assert.NotNull(Hit(f, id));
        // A wall opening keeps its gap as before.
        var bare = (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["width"] = 1 })!["id"]!;
        Assert.True(Hit(f, bare)!.Closed);
    }
}
