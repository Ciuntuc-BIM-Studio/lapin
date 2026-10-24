using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Floors, and the cut priorities between them and walls (a slab over a wall's head cuts it).</summary>
public class SlabTests
{
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    static string Slab(Fixture f, string storey, double x1, double y1, double x2, double y2, double? offset = null)
    {
        var a = new JsonObject { ["storeyId"] = storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 };
        if (offset != null) a["offset"] = offset;
        return (string)f.Run("architecture.addSlab", a)!["id"]!;
    }

    [Fact]
    public void A_floor_is_its_layers_from_its_top_down_over_its_outline()
    {
        var f = new Fixture();
        var slab = Slab(f, f.Storey, 0, 0, 6, 4);
        var g = f.Session.Geometry(slab)!;
        // Default floor: finish 20, screed 50, reinforced concrete 150, from the top at the storey's level.
        Assert.Equal(["Floor finish", "Screed", "Reinforced concrete"], g.Parts.Select(p => p.Name));
        var finish = Measure.Bounds(g.Parts[0].Solid)!.Value;
        Assert.Equal(0, finish.Max.Z, 9);
        Assert.Equal(-0.02, finish.Min.Z, 9);
        Assert.Equal(-0.22, Measure.Bounds(g.Parts[2].Solid)!.Value.Min.Z, 9);
        Assert.Equal(6 * 4 * 0.22, Vol(f, slab), 6);
        var q = f.Session.Quantities(slab)[0];
        Assert.Equal("Qto_SlabBaseQuantities", q.Name);
        Assert.Equal(24, q.Quantities.Single(x => x.Name == "GrossArea").Value, 9);
        Assert.Equal(20, q.Quantities.Single(x => x.Name == "Perimeter").Value, 9);
        // An outline drawn clockwise is kept counter-clockwise.
        var cw = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(10, 0), new JsonArray(10, 4), new JsonArray(14, 4), new JsonArray(14, 0)) })!["id"]!;
        Assert.True(Polygon2.SignedArea(Slabs.Contour(f.Model.Get(cw)!)) > 0);
    }

    [Fact]
    public void An_opening_goes_through_every_layer_and_goes_with_its_floor()
    {
        var f = new Fixture();
        var slab = Slab(f, f.Storey, 0, 0, 6, 4);
        var hole = (string)f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["x1"] = 1, ["y1"] = 1, ["x2"] = 3, ["y2"] = 2 })!["id"]!;
        Assert.Equal((24 - 2) * 0.22, Vol(f, slab), 6);
        Assert.Equal(2, f.Session.Geometry(slab)!.Plan!.Count);
        f.Run("architecture.updateSlabOpening", new JsonObject { ["id"] = hole, ["x1"] = 1, ["y1"] = 1, ["x2"] = 2, ["y2"] = 2 });
        Assert.Equal((24 - 1) * 0.22, Vol(f, slab), 6);
        var net = f.Session.Quantities(slab)[0].Quantities.Single(x => x.Name == "NetArea").Value;
        Assert.Equal(23, net, 6);
        f.Run("element.delete", new JsonObject { ["id"] = slab });
        Assert.Null(f.Model.Get(hole));
        f.Session.Undo();
        Assert.Equal((24 - 1) * 0.22, Vol(f, slab), 6);
    }

    [Fact]
    public void A_changed_floor_type_changes_its_floors_and_wrong_outlines_are_refused()
    {
        var f = new Fixture();
        var slab = Slab(f, f.Storey, 0, 0, 5, 5);
        var type = f.Model.RelatingOf(slab, IfcClass.RelDefinesByType)!.Id;
        f.Run("architecture.updateSlabType", new JsonObject { ["id"] = type, ["layers"] = new JsonArray(new JsonObject { ["material"] = "Reinforced concrete", ["thickness"] = 0.25 }) });
        Assert.Equal(25 * 0.25, Vol(f, slab), 6);
        Assert.Throws<CommandException>(() => f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(1, 1)) }));
        Assert.Throws<CommandException>(() => f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(1, 1), new JsonArray(2, 2)) }));
    }

    [Fact]
    public void The_floor_above_cuts_the_head_of_a_wall_that_reaches_into_it()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 6, 0);
        var full = Vol(f, wall);
        var upper = (string)f.Run("storey.create", [])!["id"]!;   // at +3.00
        var slab = Slab(f, upper, -1, -1, 7, 5);
        // The 220 mm floor of the level above sits from 2.78 to 3.00: the wall's head goes.
        Assert.Equal(full * (2.78 / 3), Vol(f, wall), 6);
        Assert.Equal(2.78, f.Bounds(wall).Max.Z, 9);
        // The floor is not cut by the wall below it (the wall's priority is lower).
        Assert.Equal(8 * 6 * 0.22, Vol(f, slab), 6);
        // Raised over the wall, it no longer cuts it.
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["offset"] = 0.5 });
        Assert.Equal(full, Vol(f, wall), 6);
        // A wall told to have the higher priority is not cut either.
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["offset"] = 0 });
        f.Run("element.update", new JsonObject { ["id"] = slab, ["attrs"] = new JsonObject { ["cutPriority"] = 40 } });
        Assert.Equal(full, Vol(f, wall), 6);
    }
}
