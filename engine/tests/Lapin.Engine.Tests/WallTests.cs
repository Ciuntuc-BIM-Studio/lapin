using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Layered walls. Ported from the old Lapin: authored-defaults.test.ts (the
/// axis on the core, also flipped) and element-flip.e2e.test.ts (a wall's
/// faces swap, a flip along it is refused), same cases and numbers.
/// </summary>
public class WallTests
{
    [Fact]
    public void Editing_a_wall_keeps_what_other_commands_wrote_on_it()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Phases.PhasesModule());
        var wall = f.Wall(0, 0, 5, 0);
        f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["cutPriority"] = 70 } });
        f.Run("phases.assign", new JsonObject { ["id"] = wall, ["created"] = "existing" });
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["height"] = 2.7, ["x2"] = 6 });
        var e = f.Model.Get(wall)!;
        Assert.Equal(2.7, e.Num("height"), 9);
        Assert.Equal(6, Walls.End(e).X, 9);
        Assert.Equal(70, e.Num("cutPriority"), 9);
        Assert.Equal("existing", e.Str("phaseCreated"));
    }

    [Fact]
    public void A_layered_walls_body_lies_on_its_core()
    {
        var f = new Fixture();
        var type = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, false), ("Insulation", 0.1, false));
        var wall = f.Wall(0, 0, 5, 0, type);
        var (min, max) = f.Bounds(wall);
        // Its faces: 20 + 125 mm right of the axis, 125 + 100 mm left of it.
        Assert.Equal(-0.145, min.Y, 6);
        Assert.Equal(0.225, max.Y, 6);
        Assert.Equal(0.04, (min.Y + max.Y) / 2, 6);   // the body 40 mm to the left of the axis
        Assert.Equal(0.37, max.Y - min.Y, 6);
        Assert.Equal(-0.145, f.Usage(wall).Offset, 9);
        Assert.False(f.Usage(wall).Negative);
    }

    [Fact]
    public void A_wall_flipped_across_its_axis_keeps_its_core_on_the_axis_on_the_mirrored_side()
    {
        var f = new Fixture();
        var type = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, false), ("Insulation", 0.1, false));
        var wall = f.Wall(0, 0, 5, 0, type);
        f.Run("architecture.flipWall", new JsonObject { ["id"] = wall });
        var (min, max) = f.Bounds(wall);
        Assert.Equal(-0.04, (min.Y + max.Y) / 2, 6);   // flipped: the body 40 mm to the right
        Assert.True(f.Usage(wall).Negative);

        // A new thickness (the last layer, insulation, takes it): the core stays on the axis, still mirrored.
        f.Run("architecture.updateWallType", new JsonObject
        {
            ["id"] = type,
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.02 },
                new JsonObject { ["material"] = "Brick", ["thickness"] = 0.25 },
                new JsonObject { ["material"] = "Insulation", ["thickness"] = 0.15 }),
        });
        (min, max) = f.Bounds(wall);
        Assert.Equal(-0.065, (min.Y + max.Y) / 2, 6);  // 20 + 125 mm one side, 125 + 150 mm the other, mirrored
        Assert.True(f.Usage(wall).Negative, "its usage still runs the negative way");
    }

    [Fact]
    public void Flipping_swaps_a_walls_faces_and_refuses_a_flip_along_it()
    {
        // element-flip.e2e.test.ts: a wall on y = 10, brick 200 + insulation 100.
        var f = new Fixture();
        var type = f.Type("Brick + insulation", ("Brick", 0.2, false), ("Insulation", 0.1, false));
        var wall = f.Wall(0, 10, 5, 10, type);
        var start = f.Bounds(wall);
        var before = f.Usage(wall);

        Assert.Throws<CommandException>(() => f.Run("architecture.flipWall", new JsonObject { ["id"] = wall, ["axis"] = "x" }));
        f.Run("architecture.flipWall", new JsonObject { ["id"] = wall, ["axis"] = "y" });
        var flipped = f.Bounds(wall);
        // The body went to the other side of the axis (y = 10): mirrored across it.
        Assert.Equal(20 - start.Max.Y, flipped.Min.Y, 6);
        Assert.Equal(20 - start.Min.Y, flipped.Max.Y, 6);
        Assert.Equal(start.Min.X, flipped.Min.X, 6);
        var after = f.Usage(wall);
        Assert.True(after.Negative);
        Assert.Equal(-before.Offset, after.Offset, 12);   // the offset changes sign

        // Flipped back: where it started.
        f.Run("architecture.flipWall", new JsonObject { ["id"] = wall });
        var back = f.Bounds(wall);
        Assert.Equal(start.Min.Y, back.Min.Y, 9);
        Assert.Equal(start.Max.Y, back.Max.Y, 9);
    }

    [Fact]
    public void A_wall_is_an_ifc_wall_on_its_storey_typed_with_its_own_usage()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 4, 0);   // no type chosen: the default type
        var m = f.Model;
        Assert.Equal(IfcClass.Wall, m.Get(wall)!.Class);
        Assert.Equal(f.Storey, Spatial.StoreyOf(m, wall)!.Id);
        var type = Walls.TypeOf(m, wall)!;
        Assert.Equal(IfcClass.WallType, type.Class);
        var set = WallTypes.LayerSetOf(m, type.Id)!;
        Assert.Equal(3, Layers.Read(set).Count);
        var usage = Walls.UsageOf(m, wall)!;
        Assert.Equal(set.Id, usage.Str("forLayerSet"));
        Assert.Equal("AXIS2", usage.Str("layerSetDirection"));
        // Render 20, brick 250 (the core), plaster 15: the core's middle 145 mm from the right face.
        Assert.Equal(-0.145, f.Usage(wall).Offset, 9);
        // The type is shared, the usage is the wall's own.
        var second = f.Wall(0, 2, 4, 2);
        Assert.Equal(type.Id, Walls.TypeOf(m, second)!.Id);
        Assert.NotEqual(usage.Id, Walls.UsageOf(m, second)!.Id);
    }

    [Fact]
    public void A_walls_geometry_has_a_part_per_layer_in_its_materials_colour_on_its_storey()
    {
        var f = new Fixture();
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["elevation"] = 2.5 });
        var type = f.Type("Two layers", ("Concrete", 0.2, true), ("XPS insulation", 0.1, false));
        var wall = f.Wall(0, 0, 3, 0, type);
        var g = f.Session.Geometry(wall)!;
        Assert.Equal(2, g.Parts.Count);
        Assert.Equal(["Concrete", "XPS insulation"], g.Parts.Select(p => p.Name));
        Assert.Equal(Materials.DefaultColor("Concrete"), g.Parts[0].Color);
        var (min, max) = f.Bounds(wall);
        Assert.Equal(2.5, min.Z, 9);
        Assert.Equal(5.5, max.Z, 9);
        Assert.Equal(0.3 * 3 * 3, g.Parts.Sum(p => Lapin.Geometry.Measure.Volume(p.Solid)), 6);
    }

    [Fact]
    public void Retyping_a_wall_lays_the_new_types_layers_and_keeps_its_flip()
    {
        var f = new Fixture();
        var a = f.Type("A", ("Brick", 0.25, false), ("Plaster", 0.015, false));
        var b = f.Type("B", ("Plaster", 0.02, false), ("Concrete", 0.3, true));
        var wall = f.Wall(0, 0, 4, 0, a);
        var usage = Walls.UsageOf(f.Model, wall)!.Id;
        f.Run("architecture.flipWall", new JsonObject { ["id"] = wall });
        f.Run("architecture.setWallType", new JsonObject { ["id"] = wall, ["typeId"] = b });
        Assert.Equal(b, Walls.TypeOf(f.Model, wall)!.Id);
        Assert.Equal(usage, Walls.UsageOf(f.Model, wall)!.Id);   // the same usage, re-laid
        Assert.True(f.Usage(wall).Negative);
        Assert.Equal(0.17, f.Usage(wall).Offset, 9);             // 20 + 150 mm, mirrored
    }

    [Fact]
    public void Wrong_input_is_refused_and_changes_nothing()
    {
        var f = new Fixture();
        var count = ((ModelStore)f.Model).EntityCount;
        Assert.Throws<CommandException>(() => f.Wall(1, 1, 1, 1));
        Assert.Throws<CommandException>(() => f.Type("Empty"));
        Assert.Throws<CommandException>(() => f.Type("Two cores", ("A", 0.1, true), ("B", 0.1, true)));
        Assert.Throws<CommandException>(() => f.Type("Thin", ("A", 0, false)));
        Assert.Equal(count, ((ModelStore)f.Model).EntityCount);
        Assert.False(f.Session.History.CanUndo);
    }
}
