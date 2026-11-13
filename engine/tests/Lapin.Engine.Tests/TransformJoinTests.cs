using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 47, item 9 (decision 531): walls, beams and strip footings moved, turned, mirrored, copied or arrayed join
/// what they now meet, as one drawn there would; a moved one's join that no longer meets goes; what is carried together
/// keeps its joins among them as they were; one undo step with the command. Recognised walls: IfcRecognitionTests.
/// </summary>
public sealed class TransformJoinTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule());

    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());

    static List<string> Run(Fixture f, JsonObject a) =>
        f.Run("element.transform", a)!["ids"]!.AsArray().Select(x => (string)x!).ToList();

    static string Copy(Fixture f, string id, double dx, double dy) =>
        Run(f, new JsonObject { ["ids"] = Ids(id), ["op"] = "copy", ["by"] = new JsonArray(dx, dy) }).Single();

    static void Move(Fixture f, string id, double dx, double dy) =>
        Run(f, new JsonObject { ["ids"] = Ids(id), ["op"] = "move", ["by"] = new JsonArray(dx, dy) });

    static string? Kind(Fixture f, string a, string b) => (string?)Joins.Between(f.Model, a, b)?.Attrs?["kind"];

    [Fact]
    public void A_wall_copied_so_its_end_lands_on_another_makes_a_T()
    {
        var f = F();
        var a = f.Wall(0, 0, 6, 0);
        var b = f.Wall(10, 1, 10, 4);
        var c = Copy(f, b, -7, -1);
        Assert.Equal("T", Kind(f, a, c));
        Assert.Null(Joins.Between(f.Model, a, b));
        // The copy runs from a's axis, as one drawn there.
        Assert.Equal(new Vec2(3, 0), Walls.Start(f.Model.Get(c)!));
    }

    [Fact]
    public void A_wall_copied_onto_anothers_end_makes_a_corner()
    {
        var f = F();
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(8, 1, 8, 4);
        var c = Copy(f, b, -3, -1);
        Assert.Equal("L", Kind(f, a, c));
        Assert.Equal("mitre", (string?)Joins.Between(f.Model, a, c)!.Attrs!["style"]);
    }

    [Fact]
    public void A_move_drops_the_join_it_left_and_makes_the_one_it_lands_on()
    {
        var f = F();
        var a = f.Wall(0, 0, 6, 0);
        var b = f.Wall(3, -2, 3, 2);
        Assert.Equal("X", Kind(f, a, b));
        var c = f.Wall(8, 3, 12, 3);
        Move(f, b, 6, 1);
        Assert.Null(Joins.Between(f.Model, a, b));
        Assert.Equal("T", Kind(f, c, b));
        // a is as it was: the crossing gone, nothing of it dragged.
        Assert.Equal((new Vec2(0, 0), new Vec2(6, 0)), (Walls.Start(f.Model.Get(a)!), Walls.End(f.Model.Get(a)!)));
    }

    [Fact]
    public void A_turn_or_a_mirror_joins_where_the_end_lands()
    {
        var f = F();
        var a = f.Wall(0, 0, 6, 0);
        var b = f.Wall(3, 1, 3, 4);
        Run(f, new JsonObject { ["ids"] = Ids(b), ["op"] = "rotate", ["center"] = new JsonArray(3, 0.5), ["angle"] = 180 });
        Assert.Equal("T", Kind(f, a, b));

        var g = F();
        var a2 = g.Wall(0, 0, 6, 0);
        var b2 = g.Wall(3, 1, 3, 4);
        var m = Run(g, new JsonObject { ["ids"] = Ids(b2), ["op"] = "mirror", ["a"] = new JsonArray(0, 0.5), ["b"] = new JsonArray(1, 0.5), ["copy"] = true }).Single();
        Assert.Equal("T", Kind(g, a2, m));
        Assert.Null(Joins.Between(g.Model, a2, b2));
    }

    [Fact]
    public void What_is_carried_together_keeps_its_joins_among_them()
    {
        var f = F();
        var a = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 3, ["y2"] = 0, ["join"] = false })!["id"]!;
        var b = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 3, ["y1"] = 0, ["x2"] = 3, ["y2"] = 3, ["join"] = false })!["id"]!;
        Assert.Null(Joins.Between(f.Model, a, b));
        Run(f, new JsonObject { ["ids"] = Ids(a, b), ["op"] = "move", ["by"] = new JsonArray(0, 5) });
        Assert.Null(Joins.Between(f.Model, a, b));
        var copies = Run(f, new JsonObject { ["ids"] = Ids(a, b), ["op"] = "copy", ["by"] = new JsonArray(10, 0) });
        Assert.Null(Joins.Between(f.Model, copies[0], copies[1]));
        // A copy in place makes no join with what it was copied from (in line over it, not at an end).
        var d = f.Wall(20, 0, 24, 0);
        var e = Copy(f, d, 0, 0);
        Assert.NotNull(f.Model.Get(e));
    }

    [Fact]
    public void An_array_joins_each_copy_where_it_lands()
    {
        var f = F();
        var a = f.Wall(0, 0, 10, 0);
        var b = f.Wall(1, 0, 1, 3);
        Assert.Equal("T", Kind(f, a, b));
        var made = f.Run("element.array", new JsonObject { ["ids"] = Ids(b), ["count"] = 3, ["step"] = new JsonArray(3, 0, 0) })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(2, made.Count);
        foreach (var c in made) Assert.Equal("T", Kind(f, a, c));
    }

    [Fact]
    public void Beams_and_strip_footings_copied_join_too()
    {
        var f = F();
        string Beam(double x1, double y1, double x2, double y2) =>
            (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 })!["id"]!;
        string Footing(double x1, double y1, double x2, double y2) =>
            (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 })!["id"]!;
        var b1 = Beam(0, 0, 5, 0);
        var b2 = Beam(9, 2, 9, 6);
        var bc = Copy(f, b2, -4, -2);
        Assert.Equal("L", Kind(f, b1, bc));
        var s1 = Footing(0, 20, 6, 20);
        var s2 = Footing(10, 22, 10, 25);
        var sc = Copy(f, s2, -7, -2);
        Assert.Equal("T", Kind(f, s1, sc));
    }

    [Fact]
    public void The_copy_and_its_joins_are_one_undo_step()
    {
        var f = F();
        var a = f.Wall(0, 0, 6, 0);
        var b = f.Wall(10, 1, 10, 4);
        var before = f.Model.Relations.Count();
        var c = Copy(f, b, -7, -1);
        Assert.Equal("T", Kind(f, a, c));
        f.Session.Undo();
        Assert.Null(f.Model.Get(c));
        Assert.Equal(before, f.Model.Relations.Count());
        Assert.Empty(Joins.Of(f.Model, a));
        Assert.Equal((new Vec2(0, 0), new Vec2(6, 0)), (Walls.Start(f.Model.Get(a)!), Walls.End(f.Model.Get(a)!)));
    }
}
