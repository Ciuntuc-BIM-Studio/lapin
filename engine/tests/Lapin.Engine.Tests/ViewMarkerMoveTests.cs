using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Markers moved in plans (decision 460, the tester's lot B and the user's answer): an elevation's marker anywhere (its
/// line and symbol; left outside the building, the elevation sees the same: ElevationClipTests for inside), a
/// section by the Move tool as Revit moves one (its cut goes with it), a section's head moved off its line's end without
/// changing the cut. One undo step each; a pinned view refuses.
/// </summary>
public sealed class ViewMarkerMoveTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonObject? Mark(JsonObject d, string id) => ((JsonArray)d["marks"]!).Select(m => (JsonObject)m!).FirstOrDefault(m => (string?)m["id"] == id);
    static List<(double X, double Y)> Ends(JsonObject plan, string id) => ((JsonArray)Mark(plan, id)!["data"]!["ends"]!).Select(p => ((double)p![0]!, (double)p[1]!)).ToList();
    static (double X, double Y) TextAt(JsonObject d, string text) =>
        ((JsonArray)d["pens"]!).SelectMany(p => (JsonArray?)p!["texts"] ?? []).Where(t => (string?)t!["text"] == text).Select(t => ((double)t!["x"]!, (double)t["y"]!)).First();
    static string Bounds(JsonObject d) => d["bounds"]!.ToJsonString();

    static (string Section, string Elevation, string Plan) House(Fixture f)
    {
        f.Wall(0, 0, 10, 0); f.Wall(10, 0, 10, 8); f.Wall(10, 8, 0, 8); f.Wall(0, 8, 0, 0);
        var a = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["name"] = "A-A", ["start"] = new JsonArray(6, -2), ["end"] = new JsonArray(6, 10) })!["id"]!;
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        return (a, south, plan);
    }

    static void Move(Fixture f, string id, double dx, double dy) =>
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = new JsonArray(dx, dy) });

    [Fact]
    public void An_elevation_s_marker_stands_clear_of_the_sections_heads()
    {
        var f = F();
        var (_, south, plan) = House(f);
        // A-A's head is 2 m south of the house; the elevation's line and its symbol further out still.
        var d = Drawing(f, plan);
        Assert.All(Ends(d, south), e => Assert.True(e.Y < -3.5, $"{e.Y}"));
        Assert.True(TextAt(d, "South elevation").Y < -4);
    }

    [Fact]
    public void An_elevation_s_marker_moves_and_the_elevation_sees_the_same()
    {
        var f = F();
        var (_, south, plan) = House(f);
        var before = Ends(Drawing(f, plan), south);
        var seen = Bounds(Drawing(f, south));
        // Pushed 2 m further out and 1 m east: the line with it, its symbol 1 m along it, its reach as it was.
        Move(f, south, 1, -2);
        var d = Drawing(f, plan);
        var after = Ends(d, south);
        Assert.Equal(before[0].Y - 2, after[0].Y, 6);
        Assert.Equal(before[0].X, after[0].X, 6);
        Assert.Equal(before[1].X, after[1].X, 6);
        // Kept as where it stands now (decision 460): its symbol 1 m along, its line 2 m further out.
        Assert.Equal(1, f.Model.Get(south)!.Num("markerAlong"), 6);
        Assert.Equal(before[0].Y - 2, f.Model.Get(south)!.Num("markerDepth"), 6);
        Assert.Equal(seen, Bounds(Drawing(f, south)));
        // One undo step.
        f.Session.Undo();
        Assert.Null(f.Model.Get(south)!.Attrs["markerDepth"]);
        // An elevation looks north, south, east or west: it is not turned or mirrored, and pinned it stays.
        Assert.Throws<CommandException>(() => f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(south), ["op"] = "rotate", ["center"] = new JsonArray(0, 0), ["angle"] = 30 }));
        f.Run("element.pin", new JsonObject { ["ids"] = new JsonArray(south) });
        Assert.Throws<CommandException>(() => Move(f, south, 0, -1));
    }

    [Fact]
    public void A_section_moved_cuts_where_it_goes_and_its_head_moves_alone()
    {
        var f = F();
        var (a, _, plan) = House(f);
        // The Move tool moves the section's line, as Revit's does: it cuts 1 m further east.
        Move(f, a, 1, 0);
        var s = f.Model.Get(a)!;
        Assert.Equal(7, (double)s.Attrs["start"]![0]!, 9);
        Assert.Equal(7, (double)s.Attrs["end"]![0]!, 9);
        f.Session.Undo();
        Assert.Equal(6, (double)f.Model.Get(a)!.Attrs["start"]![0]!, 9);
        // A jogged one moves every point.
        var b = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["name"] = "B-B", ["pts"] = new JsonArray(new JsonArray(2, -1), new JsonArray(2, 4), new JsonArray(9, 4), new JsonArray(9, 9)) })!["id"]!;
        Move(f, b, 0, 1);
        Assert.Equal(new[] { 0.0, 5, 5, 10 }, ((JsonArray)f.Model.Get(b)!.Attrs["pts"]!).Select(p => (double)p![1]!).ToArray());

        // Its head at the start moved off the line's end: the name with it, a fine leader back; the cut as it was.
        var cut = Bounds(Drawing(f, a));
        var name = TextAt(Drawing(f, plan), "A-A");
        f.Run("view.moveHead", new JsonObject { ["id"] = a, ["end"] = "a", ["offset"] = new JsonArray(-1, -0.5) });
        var moved = Drawing(f, plan);
        var texts = ((JsonArray)moved["pens"]!).SelectMany(p => (JsonArray?)p!["texts"] ?? []).Where(t => (string?)t!["text"] == "A-A").Select(t => ((double)t!["x"]!, (double)t["y"]!)).ToList();
        Assert.Contains(texts, t => Math.Abs(t.Item1 - (name.X - 1)) < 1e-6 && Math.Abs(t.Item2 - (name.Y - 0.5)) < 1e-6);
        var heads = (JsonArray)Mark(moved, a)!["data"]!["heads"]!;
        Assert.Equal(2, heads.Count);
        Assert.Equal(cut, Bounds(Drawing(f, a)));
        f.Session.Undo();
        Assert.Null(f.Model.Get(a)!.Attrs["headA"]);
        Assert.Throws<CommandException>(() => f.Run("view.moveHead", new JsonObject { ["id"] = a, ["end"] = "c", ["offset"] = new JsonArray(1, 1) }));
        f.Run("element.pin", new JsonObject { ["ids"] = new JsonArray(a) });
        Assert.Throws<CommandException>(() => f.Run("view.moveHead", new JsonObject { ["id"] = a, ["end"] = "b", ["offset"] = new JsonArray(1, 1) }));
        Assert.Throws<CommandException>(() => Move(f, a, 1, 0));
    }
}
