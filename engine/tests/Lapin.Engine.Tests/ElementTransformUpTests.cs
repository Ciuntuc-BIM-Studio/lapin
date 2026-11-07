using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Drafting;
using Lapin.Furniture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// A 3D move's vertical part (docs/plan-3d-modelling.md, testers' report #104): element.transform's <c>up</c> raises or
/// lowers what has a height of its own (a stair keeps its rise, a wall its height), a copy too; a Studio solid takes a
/// move step; what has none keeps its height and is named in the answer. The bodies follow.
/// </summary>
public sealed class ElementTransformUpTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new FurnitureModule(), new DraftingModule(), new StudioModule());

    static string Id(JsonNode? n) => (string)n!["id"]!;

    static JsonObject Move(Fixture f, string id, double dx, double dy, double up, string op = "move") =>
        (JsonObject)f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = op, ["by"] = new JsonArray(dx, dy), ["up"] = up })!;

    [Fact]
    public void A_stair_goes_up_onto_a_slab_keeping_its_rise()
    {
        var f = F();
        f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 });
        var type = Id(f.Run("architecture.createStairType", new JsonObject { ["spec"] = (StairSpec.Default() with { Name = "Small" }).ToJson() }));
        var stair = Id(f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0 }));
        var (min0, max0) = f.Bounds(stair);
        Move(f, stair, 0.5, 0.25, 0.2);
        var e = f.Model.Get(stair)!;
        Assert.Equal(0.2, e.Num("baseOffset"), 6);
        Assert.Equal(0.2, e.Num("topOffset"), 6);
        var (min1, max1) = f.Bounds(stair);
        Assert.Equal(min0.Z + 0.2, min1.Z, 3);
        Assert.Equal(max0.Z - min0.Z, max1.Z - min1.Z, 3);
        Assert.Equal(min0.X + 0.5, min1.X, 3);
        Assert.Equal(min0.Y + 0.25, min1.Y, 3);
    }

    [Fact]
    public void Walls_columns_beams_floors_and_furniture_take_their_own_height()
    {
        var f = F();
        var wall = f.Wall(0, 0, 4, 0);
        var column = Id(f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 6, ["y"] = 0 }));
        var beam = Id(f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 3, ["x2"] = 5, ["y2"] = 3 }));
        var slab = Id(f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 5), new JsonArray(4, 5), new JsonArray(4, 8), new JsonArray(0, 8)) }));
        var chair = Id(f.Run("furniture.place", new JsonObject { ["storeyId"] = f.Storey, ["kind"] = "armchair", ["x"] = 8, ["y"] = 6, ["against"] = false }));
        var height = f.Model.Get(wall)!.Num("height", 0);
        var (w0, _) = f.Bounds(wall);
        foreach (var id in new[] { wall, column, beam, slab, chair }) Assert.False(Move(f, id, 0, 0, 0.3).ContainsKey("kept"));
        Assert.Equal(0.3, f.Model.Get(wall)!.Num("baseOffset"), 6);
        Assert.Equal(height, f.Model.Get(wall)!.Num("height", 0), 6);
        Assert.Equal(w0.Z + 0.3, f.Bounds(wall).Min.Z, 3);
        Assert.Equal(0.3, f.Model.Get(column)!.Num("baseOffset"), 6);
        Assert.Equal(0.3, f.Model.Get(beam)!.Num("topOffset"), 6);
        Assert.Equal(0.3, f.Model.Get(slab)!.Num("offset"), 6);
        Assert.Equal(0.3, f.Model.Get(chair)!.Num("offset"), 6);
        // Down again: back where they were.
        Move(f, wall, 0, 0, -0.3);
        Assert.Equal(0, f.Model.Get(wall)!.Num("baseOffset"), 6);
    }

    [Fact]
    public void A_copy_goes_up_and_the_original_stays()
    {
        var f = F();
        var column = Id(f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 }));
        var r = Move(f, column, 2, 0, 1.5, "copy");
        var copy = (string)((JsonArray)r["ids"]!)[0]!;
        Assert.Equal(0, f.Model.Get(column)!.Num("baseOffset"), 6);
        Assert.Equal(1.5, f.Model.Get(copy)!.Num("baseOffset"), 6);
    }

    [Fact]
    public void A_studio_solid_takes_one_move_step_and_a_drafted_shape_keeps_its_height()
    {
        var f = F();
        var block = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = new JsonArray(0, 0, 0), ["width"] = 1, ["length"] = 1, ["height"] = 1, ["storeyId"] = f.Storey }));
        var z0 = f.Bounds(block).Min.Z;
        Move(f, block, 0, 0, 0.5);
        Move(f, block, 1, 0, 0.25);
        var steps = (JsonArray)f.Model.Get(block)!.Attrs["features"]!;
        Assert.Single(steps);
        Assert.Equal(0.75, (double)steps[0]!["by"]![2]!, 6);
        Assert.Equal(z0 + 0.75, f.Bounds(block).Min.Z, 3);
        Move(f, block, 0, 0, -0.75);
        Assert.Null(f.Model.Get(block)!.Attrs["features"]);

        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 3), ["b"] = new JsonArray(2, 3) } })!["ids"]![0]!;
        var kept = Move(f, line, 1, 0, 0.5)["kept"] as JsonArray;
        Assert.Equal([line], kept!.Select(k => (string)k!).ToArray());
    }
}
