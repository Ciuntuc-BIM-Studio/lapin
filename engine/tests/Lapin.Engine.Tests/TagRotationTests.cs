using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Report #129, a tester's request: a tag turned by an angle or aligned to its element, always readable, its leader still reaching the element.</summary>
public class TagRotationTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonObject Draw(Fixture f) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = "plan:" + f.Storey })!;
    static JsonObject Mark(JsonObject d, string id) => d["marks"]!.AsArray().OfType<JsonObject>().First(m => (string)m["id"]! == id);
    static string Tag(Fixture f, string wall, JsonObject? more = null)
    {
        var a = new JsonObject { ["viewId"] = "plan:" + f.Storey, ["element"] = wall, ["field"] = "name", ["at"] = new JsonArray(2, 3) };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("drafting.addTag", a)!["id"]!;
    }
    /// <summary>The angles of the texts drawn for a tag: those that carry its element's name.</summary>
    static List<double> TextAngles(Fixture f, string id)
    {
        var name = f.Model.Get(f.Model.Get(id)!.Str("element")!)!.Name;
        return Draw(f)["pens"]!.AsArray().OfType<JsonObject>().SelectMany(p => p["texts"]?.AsArray() ?? []).OfType<JsonObject>()
            .Where(t => (string)t["text"]! == name).Select(t => (double)t["angle"]!).ToList();
    }

    [Fact]
    public void A_tag_turned_by_an_angle_draws_its_text_at_that_angle_and_the_leader_still_reaches_the_element()
    {
        var f = F();
        var w = f.Wall(0, 0, 6, 0);
        var w2 = f.Wall(0, 5, 6, 5);
        f.Run("element.rename", new JsonObject { ["id"] = w, ["name"] = "Wall A" });
        f.Run("element.rename", new JsonObject { ["id"] = w2, ["name"] = "Wall B" });
        var plain = Tag(f, w);
        var turned = Tag(f, w2, new JsonObject { ["rotation"] = 45 });
        Assert.All(TextAngles(f, plain), a => Assert.Equal(0, a, 6));
        Assert.All(TextAngles(f, turned), a => Assert.Equal(45, a, 6));
        // Its data tells the interface the angle; the leader's end is the wall's middle as before.
        var d = Draw(f);
        Assert.Equal(45, (double)Mark(d, turned)["data"]!["angle"]!, 6);
        Assert.Equal(3, (double)Mark(d, turned)["data"]!["end"]![0]!, 6);
        Assert.Equal(5, (double)Mark(d, turned)["data"]!["end"]![1]!, 1);
        // Turned by hand later, and back upright; an angle is kept readable (200° is 20°).
        f.Run("drafting.updateTag", new JsonObject { ["id"] = turned, ["rotation"] = 200 });
        Assert.All(TextAngles(f, turned), a => Assert.Equal(20, a, 6));
        f.Run("drafting.updateTag", new JsonObject { ["id"] = turned, ["rotation"] = 0 });
        Assert.All(TextAngles(f, turned), a => Assert.Equal(0, a, 6));
        Assert.Null(f.Model.Get(turned)!.Attrs["rotation"]);
    }

    [Fact]
    public void Aligned_to_its_element_a_tag_follows_its_direction_and_is_never_upside_down()
    {
        var f = F();
        var along = f.Wall(0, 0, 6, 0);
        var up = f.Wall(8, 0, 8, 5);
        var down = f.Wall(10, 5, 10, 0);
        foreach (var (id, n) in new[] { (along, "Along"), (up, "Up"), (down, "Down") }) f.Run("element.rename", new JsonObject { ["id"] = id, ["name"] = n });
        var t1 = Tag(f, along, new JsonObject { ["align"] = true });
        var t2 = Tag(f, up, new JsonObject { ["align"] = true });
        var t3 = Tag(f, down, new JsonObject { ["align"] = true });
        Assert.All(TextAngles(f, t1), a => Assert.Equal(0, a, 6));
        Assert.All(TextAngles(f, t2), a => Assert.Equal(90, a, 6));
        // A wall drawn from top to bottom reads the same way up as one drawn from bottom to top.
        Assert.All(TextAngles(f, t3), a => Assert.Equal(90, a, 6));
        // A wall turned takes its tag round; a wall at 135° reads at −45°.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(along), ["op"] = "rotate", ["center"] = new JsonArray(0, 0), ["angle"] = 135 });
        Assert.All(TextAngles(f, t1), a => Assert.Equal(-45, a, 4));
        // An angle of its own is added to the element's direction.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t2, ["rotation"] = -90 });
        Assert.All(TextAngles(f, t2), a => Assert.Equal(0, a, 6));
        Assert.Equal(-90, (double)Mark(Draw(f), t3)["data"]!["base"]!, 4);
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t3, ["align"] = false });
        Assert.All(TextAngles(f, t3), a => Assert.Equal(0, a, 6));
    }

    [Fact]
    public void A_turned_elbow_tag_lays_its_text_and_shoulder_along_the_angle_from_the_knee()
    {
        var style = Standards.DefaultText;
        var knee = new Vec2(8, 8);
        var type = new TagType("t", "T", "{mark}", []);
        var (flat, _, _) = TagLeaders.Plain("t", ["D1"], style, type, 1, knee, new Vec2(0, 0), true, "#000000");
        var (m, head, side) = TagLeaders.Plain("t", ["D1"], style, type, 1, knee, new Vec2(0, 0), true, "#000000", Math.PI / 2);
        Assert.Equal(1, side);
        // Quarter turn: the shoulder runs up from the knee, the text's angle is 90°.
        var shoulder = m.Lines.Last(l => l.Layer == "TAG" && l.Width == Pens.Default.Hatch);
        Assert.Equal(8, shoulder.B.X, 6);
        Assert.True(shoulder.B.Y > 8);
        Assert.Equal(90, m.Texts[0].Text.Angle, 6);
        Assert.True(head.X < 8 && head.Y > 8);
        // The oblique leader still starts on the element.
        var oblique = m.Lines.First(l => l.Layer == "TAG" && l.Width == Pens.Default.Hatch);
        Assert.Equal(0, oblique.A.X, 6);
        Assert.Equal(flat.Lines.First(l => l.Layer == "TAG" && l.Width == Pens.Default.Hatch).B, oblique.B);
    }
}
