using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Tags follow their elements (tester's report #93): a wall's, a door's and a room's tag move, turn and mirror with
/// them, follow a grip dragged, keep the offset a user gave them, tag a copy copied with them, and come back with an undo.
/// </summary>
public class TagFollowTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonObject Data(Fixture f, string view, string tag) =>
        (JsonObject)((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!)["marks"]!.AsArray().OfType<JsonObject>().First(m => (string)m["id"]! == tag)["data"]!;
    static Vec2 P(JsonNode? n) => new((double)n![0]!, (double)n[1]!);
    static string Tag(Fixture f, string view, string element, double x, double y) =>
        (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = element, ["field"] = "name", ["at"] = new JsonArray(x, y) })!["id"]!;
    static void Near(Vec2 want, Vec2 got) { Assert.Equal(want.X, got.X, 6); Assert.Equal(want.Y, got.Y, 6); }
    static JsonObject Move(string[] ids, double dx, double dy) => new() { ["ids"] = new JsonArray(ids.Select(i => (JsonNode)i).ToArray()), ["op"] = "move", ["by"] = new JsonArray(dx, dy) };

    [Fact]
    public void A_wall_s_tag_moves_turns_and_mirrors_with_the_wall_its_leader_s_end_on_it()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var view = "plan:" + f.Storey;
        var t = Tag(f, view, w, 2.5, 2);
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["end"] = new JsonArray(1, 0) });

        // The wall moved alone (report #93): the tag goes with it, its leader still on the wall.
        f.Run("element.transform", Move([w], 0, -3));
        var d = Data(f, view, t);
        Near(new(2.5, -1), P(d["at"]));
        Near(new(1, -3), P(d["end"]));

        // Turned a quarter about the origin: the tag turns round with it.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(w), ["op"] = "rotate", ["center"] = new JsonArray(0, 0), ["angle"] = 90 });
        d = Data(f, view, t);
        Near(new(1, 2.5), P(d["at"]));
        Near(new(3, 1), P(d["end"]));
        var wall = f.Model.Get(w)!;
        Assert.True(PathCurve.Of(wall).Distance(P(d["end"])) < 1e-6);

        // Mirrored about x = 5: the tag is mirrored, on the same side of the wall as it was seen in the mirror.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(w), ["op"] = "mirror", ["a"] = new JsonArray(5, 0), ["b"] = new JsonArray(5, 1) });
        d = Data(f, view, t);
        Near(new(9, 2.5), P(d["at"]));
        Near(new(7, 1), P(d["end"]));

        // Undone, step by step: the tag comes back with the wall.
        f.Session.Undo();
        Near(new(1, 2.5), P(Data(f, view, t)["at"]));
        f.Session.Undo();
        f.Session.Undo();
        Near(new(2.5, 2), P(Data(f, view, t)["at"]));
        Near(new(1, 0), P(Data(f, view, t)["end"]));
    }

    [Fact]
    public void A_wall_s_tag_follows_its_end_dragged_and_keeps_an_offset_given_by_hand()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var view = "plan:" + f.Storey;
        var t = Tag(f, view, w, 2.5, 2);
        // The wall's end dragged out (its grip): the tag holds to the wall's middle, which moved 2 m.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = w, ["x1"] = 0, ["y1"] = 0, ["x2"] = 9, ["y2"] = 0 });
        var d = Data(f, view, t);
        Near(new(4.5, 2), P(d["at"]));
        Assert.Equal(4.5, P(d["end"]).X, 3);
        // Dragged on to a slope: the tag keeps its place across the wall's middle.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = w, ["x1"] = 0, ["y1"] = 0, ["x2"] = 0, ["y2"] = 9 });
        Near(new(-2, 4.5), P(Data(f, view, t)["at"]));

        // Moved by hand: its own offset from the wall, kept when the wall moves again.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["at"] = new JsonArray(-3, 6) });
        Near(new(-3, 6), P(Data(f, view, t)["at"]));
        f.Run("element.transform", Move([w], 1, 1));
        Near(new(-2, 7), P(Data(f, view, t)["at"]));
        // A leader's end pointed along the wall stays there while the wall's other end is dragged.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["end"] = new JsonArray(1, 2) });
        f.Run("architecture.updateWall", new JsonObject { ["id"] = w, ["x1"] = 1, ["y1"] = 1, ["x2"] = 1, ["y2"] = 20 });
        d = Data(f, view, t);
        Near(new(1, 2), P(d["end"]));
        Near(new(-2, 7), P(d["at"]));
    }

    [Fact]
    public void A_tag_copied_with_its_wall_tags_the_copy_and_a_wall_copied_alone_takes_no_tag()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var view = "plan:" + f.Storey;
        var t = Tag(f, view, w, 2.5, 2);
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["end"] = new JsonArray(1, 0) });
        var tags = () => f.Model.OfClass(IfcClass.Annotation).Where(TagHold.IsTag).ToList();

        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(w), ["op"] = "copy", ["by"] = new JsonArray(0, 5) });
        Assert.Single(tags());
        Near(new(2.5, 2), P(Data(f, view, t)["at"]));

        var made = f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(w, t), ["op"] = "copy", ["by"] = new JsonArray(0, 10) })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        var copyTag = tags().Single(x => x.Id != t);
        var copyWall = made.Single(id => id != copyTag.Id && f.Model.Get(id)!.Class == IfcClass.Wall);
        Assert.Equal(copyWall, copyTag.Str("element"));
        var d = Data(f, view, copyTag.Id);
        Near(new(2.5, 12), P(d["at"]));
        Near(new(1, 10), P(d["end"]));
        // The copy's tag follows the copy, the first the first.
        f.Run("element.transform", Move([copyWall], 1, 0));
        Near(new(3.5, 12), P(Data(f, view, copyTag.Id)["at"]));
        Near(new(2.5, 2), P(Data(f, view, t)["at"]));
    }

    [Fact]
    public void A_door_s_tag_follows_the_door_along_its_wall_and_the_wall()
    {
        var f = F();
        var w = f.Wall(0, 0, 6, 0);
        var view = "plan:" + f.Storey;
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = w, ["along"] = 2 })!["id"]!;
        var opening = f.Model.RelatingOf(door, IfcClass.RelFillsElement)!.Id;
        var t = Tag(f, view, door, 2, 1.5);
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["end"] = new JsonArray(2, 0) });

        f.Run("element.transform", Move([w], 0, -2));
        var d = Data(f, view, t);
        Near(new(2, -0.5), P(d["at"]));
        Near(new(2, -2), P(d["end"]));

        // Dragged along its wall: the tag slides with it.
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = opening, ["along"] = 3.5 });
        d = Data(f, view, t);
        Near(new(3.5, -0.5), P(d["at"]));
        Near(new(3.5, -2), P(d["end"]));

        // The wall turned: the door's tag turns with it.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(w), ["op"] = "rotate", ["center"] = new JsonArray(0, -2), ["angle"] = 90 });
        Near(new(-1.5, 1.5), P(Data(f, view, t)["at"]));
    }

    [Fact]
    public void A_room_s_tag_follows_the_room_and_a_section_s_tag_follows_its_wall()
    {
        var f = F();
        var view = "plan:" + f.Storey;
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 10, ["y1"] = 0, ["x2"] = 13, ["y2"] = 2, ["name"] = "Terrace" })!["id"]!;
        var t = Tag(f, view, room, 11.5, 3);
        f.Run("element.transform", Move([room], 2, 1));
        Near(new(13.5, 4), P(Data(f, view, t)["at"]));

        var w = f.Wall(0, 0, 5, 0);
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, -2), ["end"] = new JsonArray(7, -2) })!["id"]!;
        var st = Tag(f, section, w, 2.5, 4);
        var before = P(Data(f, section, st)["at"]);
        f.Run("element.transform", Move([w], 1, 0.5));
        Near(before + new Vec2(1, 0), P(Data(f, section, st)["at"]));
    }

    [Fact]
    public void An_older_project_s_tags_take_their_element_s_frame_as_it_opens()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var t = Tag(f, "plan:" + f.Storey, w, 2.5, 2);
        var old = f.Model.Entities.Select(e => e.Id == t ? e.With(attrs: Without(e.CloneAttrs(), TagHold.Key)) : e).ToList();
        var (upgraded, _) = new TagHoldUpgrade().Upgrade(old, f.Model.Relations.ToList());
        var held = TagHold.Held(upgraded.Single(e => e.Id == t));
        Assert.NotNull(held);
        Assert.Equal(2.5, held!.Value.P.X, 6);
        Assert.Equal(new TagHold.Spot("f", 0.5), held.Value.On);
    }

    static JsonObject Without(JsonObject o, string key) { o.Remove(key); return o; }
}
