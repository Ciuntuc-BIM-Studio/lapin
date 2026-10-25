using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>The project's cut order: class priorities from the modules, changed in Manage, overridden per element; roofs and Studio solids take part.</summary>
public class CutOrderTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static JsonArray J(double x, double y) => [x, y];

    static (Fixture F, string Wall, string Slab, double Full) WallUnderSlab()
    {
        var f = F();
        var wall = f.Wall(0, 2, 6, 2);
        var full = Vol(f, wall);
        var upper = (string)f.Run("storey.create", [])!["id"]!;   // +3.00
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = upper, ["x1"] = -1, ["y1"] = -1, ["x2"] = 7, ["y2"] = 5 })!["id"]!;
        return (f, wall, slab, full);
    }

    [Fact]
    public void Lists_the_classes_with_their_defaults_highest_first()
    {
        var f = F();
        var rows = ((JsonArray)f.Session.Query("cutOrder.list", [])!).Select(r => ((string)r!["class"]!, (int)r["default"]!, (int)r["priority"]!)).ToList();
        Assert.Contains(("IfcSlab", 60, 60), rows);
        Assert.Contains(("IfcRoof", 55, 55), rows);
        Assert.Contains(("IfcWall", 50, 50), rows);
        Assert.Contains(("IfcBuildingElementProxy", 40, 40), rows);
        Assert.Contains(("IfcFurnishingElement", 10, 10), rows);
        Assert.Equal(rows.OrderByDescending(r => r.Item2).ToList(), rows);
    }

    [Fact]
    public void The_project_order_decides_until_an_element_says_otherwise_and_reset_brings_the_defaults_back()
    {
        var (f, wall, slab, full) = WallUnderSlab();
        Assert.Equal(full * (2.78 / 3), Vol(f, wall), 6);
        // Walls above floors in this project: the wall keeps its head, the floor is cut round it.
        f.Run("cutOrder.set", new JsonObject { ["priorities"] = new JsonObject { ["IfcWall"] = 65 } });
        Assert.Equal(full, Vol(f, wall), 6);
        Assert.True(Vol(f, slab) < 8 * 6 * 0.22 - 1e-6);
        Assert.Equal(65, (int)((JsonArray)f.Session.Query("cutOrder.list", [])!).Single(r => (string)r!["class"]! == "IfcWall")!["priority"]!);
        // This one floor still ranks over walls: its own priority wins.
        f.Run("element.update", new JsonObject { ["id"] = slab, ["attrs"] = new JsonObject { ["cutPriority"] = 70 } });
        Assert.Equal(full * (2.78 / 3), Vol(f, wall), 6);
        f.Run("element.update", new JsonObject { ["id"] = slab, ["attrs"] = new JsonObject { ["cutPriority"] = null } });
        Assert.Equal(full, Vol(f, wall), 6);
        // Back to the defaults; undo brings the project's order back.
        f.Run("cutOrder.reset", []);
        Assert.Equal(full * (2.78 / 3), Vol(f, wall), 6);
        f.Session.Undo();
        Assert.Equal(full, Vol(f, wall), 6);
        Assert.Throws<Lapin.Platform.CommandException>(() => f.Run("cutOrder.set", new JsonObject { ["priorities"] = new JsonObject { ["IfcDoor"] = 10 } }));
        Assert.Throws<Lapin.Platform.CommandException>(() => f.Run("cutOrder.set", new JsonObject { ["priorities"] = new JsonObject { ["IfcWall"] = 101 } }));
    }

    [Fact]
    public void A_Studio_solid_through_a_wall_gives_way_to_it()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);                                  // 0.2 m thick (default), 3 m high
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var rect = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(2, -1), J(3, -1), J(3, 1), J(2, 1)), ["closed"] = true };
        var profile = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = rect })!["ids"]![0]!;
        var solid = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = profile, ["depth"] = 0.5 })!["id"]!;
        var wallFull = 6 * 3 * Thickness(f, wall);
        // The 1 × 2 × 0.5 block loses the wall's slice; the wall stays whole.
        Assert.Equal(1 * (2 - Thickness(f, wall)) * 0.5, Vol(f, solid), 6);
        Assert.Equal(wallFull, Vol(f, wall), 6);
        // Ranked over walls, the block cuts the wall instead.
        f.Run("cutOrder.set", new JsonObject { ["priorities"] = new JsonObject { ["IfcBuildingElementProxy"] = 75 } });
        Assert.Equal(1 * 2 * 0.5, Vol(f, solid), 6);
        Assert.Equal(wallFull - 1 * Thickness(f, wall) * 0.5, Vol(f, wall), 6);
    }

    static double Thickness(Fixture f, string wall)
    {
        var b = f.Bounds(wall);
        return b.Max.Y - b.Min.Y;
    }

    [Fact]
    public void A_material_ranks_its_elements_over_their_class_until_the_element_says_otherwise()
    {
        var (f, wall, slab, full) = WallUnderSlab();
        var slabFull = Vol(f, slab);
        Assert.True(Vol(f, wall) < full - 1e-6);   // the slab (60) cuts the wall's head (50)
        var core = Lapin.Platform.CutOrder.MainMaterial(f.Model.Get(wall)!, f.Model)!;
        var rows = (JsonArray)f.Session.Query("cutOrder.materials", [])!;
        Assert.Contains(rows, r => (string?)r!["id"] == core && r["priority"] == null);
        // The wall's core material over floors: the wall now cuts the slab.
        f.Run("cutOrder.set", new JsonObject { ["materials"] = new JsonObject { [core] = 70 } });
        Assert.Equal(full, Vol(f, wall), 6);
        Assert.True(Vol(f, slab) < slabFull - 1e-6);
        Assert.Contains((JsonArray)f.Session.Query("cutOrder.materials", [])!, r => (string?)r!["id"] == core && (double?)r["priority"] == 70);
        // The element's own priority still decides.
        f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["cutPriority"] = 10 } });
        Assert.True(Vol(f, wall) < full - 1e-6);
        f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["cutPriority"] = null } });
        // Back to its class.
        f.Run("cutOrder.set", new JsonObject { ["materials"] = new JsonObject { [core] = null } });
        Assert.True(Vol(f, wall) < full - 1e-6);
        Assert.Equal(slabFull, Vol(f, slab), 6);
        Assert.Throws<Lapin.Platform.CommandException>(() => f.Run("cutOrder.set", new JsonObject { ["materials"] = new JsonObject { ["nope"] = 5 } }));
    }
}
