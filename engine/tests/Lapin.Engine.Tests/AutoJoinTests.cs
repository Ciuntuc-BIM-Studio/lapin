using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The forum's topic #50 (decision 546): each end of a wall, a beam or a strip footing joins automatically or not
/// (<see cref="Joins.AutoKey"/>); an end turned off keeps its joins and makes no new one, nor is another's end brought to it;
/// element.setAutoJoin sets it on a selection, one undo step. Recognised walls: IfcRecognitionTests.
/// </summary>
public sealed class AutoJoinTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule());

    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());

    static void Set(Fixture f, JsonObject a) => f.Run("element.setAutoJoin", a);

    [Fact]
    public void A_drawn_wall_with_its_end_off_joins_at_its_start_only()
    {
        var f = F();
        var a = f.Wall(0, 0, 5, 0);
        Assert.True(Joins.AutoAt(f.Model.Get(a)!, 0));
        Set(f, new JsonObject { ["ids"] = Ids(a), ["end"] = false });
        var b = f.Wall(5, 0, 5, 3);
        Assert.Null(Joins.Between(f.Model, a, b));
        Assert.Equal(new Vec2(5, 0), Walls.End(f.Model.Get(a)!));
        var c = f.Wall(0, 0, 0, 3);
        Assert.Equal("L", (string?)Joins.Between(f.Model, a, c)?.Attrs?["kind"]);
        // An edit of a's height or its start: its end still joins nothing.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = a, ["height"] = 2.7 });
        f.Run("architecture.updateWall", new JsonObject { ["id"] = b, ["y2"] = 3.5 });
        Assert.Null(Joins.Between(f.Model, a, b));
        Assert.Equal(new Vec2(5, 0), Walls.End(f.Model.Get(a)!));
        // Another's free end on its run still stops in it (a T leaves it whole).
        var d = f.Wall(2.5, 3, 2.5, 0);
        Assert.Equal("T", (string?)Joins.Between(f.Model, a, d)?.Attrs?["kind"]);
    }

    [Fact]
    public void An_end_turned_off_keeps_the_join_it_has()
    {
        var f = F();
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(5, 0, 5, 3);
        Assert.NotNull(Joins.Between(f.Model, a, b));
        Set(f, new JsonObject { ["ids"] = Ids(a, b), ["end"] = false, ["start"] = false });
        Assert.NotNull(Joins.Between(f.Model, a, b));
        // Dragged, the corner still goes with it (the join is the user's own, kept).
        f.Run("architecture.updateWall", new JsonObject { ["id"] = a, ["x2"] = 6 });
        Assert.NotNull(Joins.Between(f.Model, a, b));
        Assert.Equal(new Vec2(6, 0), Walls.Start(f.Model.Get(b)!));
        // Wholly off: drawn across it, no crossing is made.
        var c = f.Wall(3, -2, 3, 2);
        Assert.Null(Joins.Between(f.Model, a, c));
    }

    [Fact]
    public void The_toggles_set_each_end_of_a_selection_in_one_undo_step()
    {
        var f = F();
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(0, 4, 5, 4);
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 8, ["x2"] = 5, ["y2"] = 8 })!["id"]!;
        var r = f.Run("element.setAutoJoin", new JsonObject { ["ids"] = Ids(a, b, beam), ["start"] = false })!;
        Assert.Equal(3, (int)r["changed"]!);
        foreach (var id in new[] { a, b, beam })
        {
            Assert.False(Joins.AutoAt(f.Model.Get(id)!, 0));
            Assert.True(Joins.AutoAt(f.Model.Get(id)!, 1));
        }
        f.Session.Undo();
        foreach (var id in new[] { a, b, beam }) Assert.Null(f.Model.Get(id)!.Attrs[Joins.AutoKey]);
        Assert.Throws<CommandException>(() => f.Run("element.setAutoJoin", new JsonObject { ["ids"] = Ids(a) }));
        Assert.Throws<CommandException>(() => f.Run("element.setAutoJoin", new JsonObject { ["ids"] = Ids(f.Storey), ["end"] = false }));
    }

    [Fact]
    public void An_end_turned_on_again_joins_what_it_meets()
    {
        var f = F();
        var a = f.Wall(0, 0, 5, 0);
        Set(f, new JsonObject { ["ids"] = Ids(a), ["end"] = false });
        var b = f.Wall(5, 0, 5, 3);
        Assert.Null(Joins.Between(f.Model, a, b));
        Set(f, new JsonObject { ["ids"] = Ids(a), ["end"] = true });
        Assert.Equal("L", (string?)Joins.Between(f.Model, a, b)?.Attrs?["kind"]);
        // A move of a wall with an end off: that end joins nothing where it lands.
        var g = F();
        var p = g.Wall(0, 0, 5, 0);
        var q = g.Wall(10, 1, 10, 4);
        g.Run("element.setAutoJoin", new JsonObject { ["ids"] = Ids(q), ["start"] = false });
        g.Run("element.transform", new JsonObject { ["ids"] = Ids(q), ["op"] = "move", ["by"] = new JsonArray(-7.5, -1) });
        Assert.Null(Joins.Between(g.Model, p, q));
    }
}
