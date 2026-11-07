using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A wall's thickness typed on the wall (report #107): the type with the same build-up and that thickness, the project's
/// or one made from the wall's type, its core resized and its finishes kept; one undo step.
/// </summary>
public class WallThicknessTests
{
    static double Total(Fixture f, string typeId) => LayerSets.LayersOf(f.Model, typeId).Sum(l => l.Thickness);

    [Fact]
    public void A_new_thickness_makes_the_type_once_reuses_it_and_keeps_the_finishes()
    {
        var f = new Fixture(new ArchitectureModule());
        var partition = f.Type("Partition wall 130mm", ("Plaster", 0.015, false), ("Brick", 0.1, true), ("Plaster", 0.015, false));
        var a = f.Wall(0, 0, 4, 0, partition);
        var b = f.Wall(0, 3, 4, 3, partition);
        var c = f.Wall(6, 0, 6, 4, partition);
        var types = f.Model.OfClass(IfcClass.WallType).Count();

        f.Run("architecture.setWallThickness", new JsonObject { ["ids"] = new JsonArray(a), ["thickness"] = 0.15 });
        var made = Walls.TypeOf(f.Model, a)!;
        Assert.Equal("Partition wall 150mm", made.Name);
        Assert.Equal(0.15, Total(f, made.Id), 9);
        var layers = LayerSets.LayersOf(f.Model, made.Id);
        Assert.Equal([0.015, 0.12, 0.015], layers.Select(l => Math.Round(l.Thickness, 6)));
        Assert.True(layers[1].Core);
        Assert.Equal(LayerSets.LayersOf(f.Model, partition).Select(l => l.Material), layers.Select(l => l.Material));
        // The other walls keep their type: only the selected one changes.
        Assert.Equal(partition, Walls.TypeOf(f.Model, b)!.Id);
        Assert.Equal(types + 1, f.Model.OfClass(IfcClass.WallType).Count());

        // Two more walls to the same thickness take the type made, no other.
        f.Run("architecture.setWallThickness", new JsonObject { ["ids"] = new JsonArray(b, c), ["thickness"] = 0.15 });
        Assert.Equal(made.Id, Walls.TypeOf(f.Model, b)!.Id);
        Assert.Equal(made.Id, Walls.TypeOf(f.Model, c)!.Id);
        Assert.Equal(types + 1, f.Model.OfClass(IfcClass.WallType).Count());

        // Back to 130 mm finds the project's own type again; the wall's body follows.
        f.Run("architecture.setWallThickness", new JsonObject { ["ids"] = new JsonArray(c), ["thickness"] = 0.13 });
        Assert.Equal(partition, Walls.TypeOf(f.Model, c)!.Id);
        var box = f.Bounds(c);
        Assert.Equal(0.13, box.Max.X - box.Min.X, 6);

        // Finishes thicker than the wall leave no core: refused.
        Assert.Throws<CommandException>(() => f.Run("architecture.setWallThickness", new JsonObject { ["ids"] = new JsonArray(a), ["thickness"] = 0.02 }));
    }

    [Fact]
    public void It_is_one_undo_step_and_a_type_without_a_size_in_its_name_gets_one()
    {
        var f = new Fixture(new ArchitectureModule());
        var own = f.Type("Brick", ("Render", 0.02, false), ("Brick", 0.25, true));
        var w = f.Wall(0, 0, 5, 0, own);
        f.Run("architecture.setWallThickness", new JsonObject { ["ids"] = new JsonArray(w), ["thickness"] = 0.32 });
        var made = Walls.TypeOf(f.Model, w)!;
        Assert.Equal("Brick 320mm", made.Name);
        Assert.Equal(0.30, LayerSets.LayersOf(f.Model, made.Id).Single(l => l.Core).Thickness, 6);
        f.Session.Undo();
        Assert.Equal(own, Walls.TypeOf(f.Model, w)!.Id);
        Assert.DoesNotContain(f.Model.OfClass(IfcClass.WallType), t => t.Name == "Brick 320mm");
    }

    [Fact]
    public void A_walls_length_is_set_by_moving_its_end_along_it()
    {
        var f = new Fixture(new ArchitectureModule());
        var w = f.Wall(0, 0, 3, 4);
        // As the properties panel does (report #107): the end moved along the axis to the new length.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = w, ["x2"] = 6 * 0.6, ["y2"] = 6 * 0.8 });
        var e = f.Model.Get(w)!;
        Assert.Equal(6, (Walls.End(e) - Walls.Start(e)).Length, 9);
    }
}
