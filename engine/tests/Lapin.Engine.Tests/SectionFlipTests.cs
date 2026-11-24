using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A section turned to look the other way (forum topic 247): view.flipSection runs its line the other way round, so it looks
/// to the other side; its heads go with their ends; one undo step; a pinned one and one following an element are refused.
/// </summary>
public sealed class SectionFlipTests
{
    static string Section(Fixture f, JsonObject args)
    {
        args["kind"] = "section";
        return (string)f.Run("view.create", args)!["id"]!;
    }

    static List<Vec2> Pts(Fixture f, string id) => SectionPaths.Points(f.Model.Get(id)!);

    [Fact]
    public void A_straight_section_flipped_looks_the_other_way_and_keeps_its_heads_on_their_ends()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Wall(0, 0, 10, 0);
        var id = Section(f, new JsonObject { ["start"] = new JsonArray(5, -2), ["end"] = new JsonArray(5, 2) });
        // Left of its line, drawn north: it looks west.
        Assert.Equal(-1, Views.Look(f.Model.Get(id)!)!.Value.X, 9);
        f.Run("view.moveHead", new JsonObject { ["id"] = id, ["end"] = "a", ["offset"] = new JsonArray(0.5, 0) });
        f.Run("view.flipSection", new JsonObject { ["id"] = id });
        var v = f.Model.Get(id)!;
        Assert.Equal(1, Views.Look(v)!.Value.X, 9);
        Assert.Equal(new Vec2(5, 2), Pts(f, id)[0]);
        Assert.Equal(new Vec2(5, -2), Pts(f, id)[1]);
        // The head moved at (5, −2) stays there: that end is now the line's end (b).
        Assert.Null(v.Attrs["headA"]);
        Assert.Equal(0.5, (double)v.Attrs["headB"]![0]!, 9);
        // Drawn again, it still cuts the wall.
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = id })!;
        Assert.Null((string?)d["problem"]);
        // One undo step back to how it looked.
        f.Session.Undo();
        Assert.Equal(-1, Views.Look(f.Model.Get(id)!)!.Value.X, 9);
        Assert.Equal(0.5, (double)f.Model.Get(id)!.Attrs["headA"]![0]!, 9);
    }

    [Fact]
    public void A_jogged_section_reverses_its_points_and_a_curved_one_swaps_its_ends_round_its_middle()
    {
        var f = new Fixture(new ArchitectureModule());
        var jog = Section(f, new JsonObject { ["pts"] = new JsonArray(new JsonArray(0, 0), new JsonArray(4, 0), new JsonArray(4, 2), new JsonArray(8, 2)) });
        f.Run("view.flipSection", new JsonObject { ["id"] = jog });
        Assert.Equal([new Vec2(8, 2), new Vec2(4, 2), new Vec2(4, 0), new Vec2(0, 0)], Pts(f, jog));

        var arc = Section(f, new JsonObject { ["pts"] = new JsonArray(new JsonArray(0, 0), new JsonArray(5, 2), new JsonArray(10, 0)), ["curve"] = "arc" });
        f.Run("view.flipSection", new JsonObject { ["id"] = arc });
        Assert.Equal([new Vec2(10, 0), new Vec2(5, 2), new Vec2(0, 0)], Pts(f, arc));
        var v = f.Model.Get(arc)!;
        Assert.Equal(new Vec2(10, 0), Args.PointOf(v.Attrs["start"]));
        Assert.Equal(new Vec2(0, 0), Args.PointOf(v.Attrs["end"]));
    }

    [Fact]
    public void A_pinned_section_or_one_following_an_element_is_not_flipped()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 10, 0);
        var along = Section(f, new JsonObject { ["along"] = wall });
        Assert.Throws<CommandException>(() => f.Run("view.flipSection", new JsonObject { ["id"] = along }));
        var id = Section(f, new JsonObject { ["start"] = new JsonArray(5, -2), ["end"] = new JsonArray(5, 2) });
        f.Run("element.pin", new JsonObject { ["ids"] = new JsonArray(id) });
        Assert.Throws<CommandException>(() => f.Run("view.flipSection", new JsonObject { ["id"] = id }));
    }

    [Fact]
    public void A_section_mirrored_by_the_move_tools_keeps_an_arc_through_its_middle()
    {
        var f = new Fixture(new ArchitectureModule());
        var arc = Section(f, new JsonObject { ["pts"] = new JsonArray(new JsonArray(0, 0), new JsonArray(5, 2), new JsonArray(10, 0)), ["curve"] = "arc" });
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(arc), ["op"] = "mirror", ["a"] = new JsonArray(0, -5), ["b"] = new JsonArray(0, 5) });
        // Mirrored across x = 0: (0,0) (−5,2) (−10,0), run the other way round, the middle point still the middle.
        var p = Pts(f, arc);
        Assert.Equal(-5, p[1].X, 9);
        Assert.Equal(2, p[1].Y, 9);
    }
}
