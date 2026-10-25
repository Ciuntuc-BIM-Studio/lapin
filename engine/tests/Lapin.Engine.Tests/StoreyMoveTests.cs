using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>Move to storey (the old Lapin's element-relocate): with the level, or kept in place.</summary>
public class StoreyMoveTests
{
    static string Upper(Fixture f, double height = 3) =>
        (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["height"] = height })!["id"]!;

    [Fact]
    public void A_wall_moved_to_another_storey_takes_its_doors_leaves_its_joins_and_moves_with_the_level()
    {
        var f = new Fixture();
        var ground = f.Storey;
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(5, 0, 5, 4);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["along"] = 2.5 })!["id"]!;
        Assert.Single(f.Model.Relations, r => r.Class == Joins.Class);
        var first = Upper(f);
        var moved = f.Run("element.moveToStorey", new JsonObject { ["ids"] = new JsonArray(a), ["storeyId"] = first })!["moved"]!.AsArray();
        Assert.Equal(2, moved.Count);
        Assert.Equal(first, Spatial.StoreyOf(f.Model, a)!.Id);
        Assert.Equal(first, Spatial.StoreyOf(f.Model, door)!.Id);
        Assert.Equal(ground, Spatial.StoreyOf(f.Model, b)!.Id);
        Assert.DoesNotContain(f.Model.Relations, r => r.Class == Joins.Class);
        // With the level: its base offset stays, its body stands 3 m higher.
        Assert.Equal(0, f.Model.Get(a)!.Num("baseOffset"), 9);
        Assert.Equal(3, f.Bounds(a).Min.Z, 6);
        // A door alone does not move: it goes with its wall.
        Assert.Throws<CommandException>(() => f.Run("element.moveToStorey", new JsonObject { ["ids"] = new JsonArray(door), ["storeyId"] = ground }));
        // Back, kept where it is in space: its base offset makes up the difference.
        f.Run("element.moveToStorey", new JsonObject { ["ids"] = new JsonArray(a, door), ["storeyId"] = ground, ["keep"] = "world" });
        Assert.Equal(ground, Spatial.StoreyOf(f.Model, door)!.Id);
        Assert.Equal(3, f.Model.Get(a)!.Num("baseOffset"), 9);
        Assert.Equal(3, f.Bounds(a).Min.Z, 6);
    }

    [Fact]
    public void Kept_in_place_a_beam_keeps_its_top_under_a_storey_of_another_height()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0 })!["id"]!;
        Assert.Equal(3, f.Bounds(beam).Max.Z, 6);
        var first = Upper(f, 2.8);
        f.Run("element.moveToStorey", new JsonObject { ["ids"] = new JsonArray(beam), ["storeyId"] = first, ["keep"] = "world" });
        // The first floor's top is at 5.80: the beam's top 2.80 under it.
        Assert.Equal(-2.8, f.Model.Get(beam)!.Num("topOffset"), 9);
        Assert.Equal(3, f.Bounds(beam).Max.Z, 6);
        Assert.Throws<CommandException>(() => f.Run("element.moveToStorey", new JsonObject { ["ids"] = new JsonArray(beam), ["storeyId"] = "nowhere" }));
    }
}
