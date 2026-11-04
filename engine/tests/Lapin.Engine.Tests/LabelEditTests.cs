using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Coordination;

namespace Lapin.Engine.Tests;

/// <summary>
/// Labels edited on screen, part by part (decision 292): a door's or window's
/// marker (its texts moved and turned, its line's ends drawn out), the texts
/// along or across the marker's line as its tag type says, always upright; an
/// opening void's label placed clear of the door's swing and of the marker,
/// moved and turned by the user and kept when the wall moves.
/// </summary>
public sealed class LabelEditTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    /// <summary>A wall along X from (0, 0) to (6, 0) with a door at 1.5 m, its storey's plan (1:100), the door tagged.</summary>
    static (Fixture F, string Wall, string Door, string Plan, string Tag) Project()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        var wall = f.Wall(0, 0, 6, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("architecture.tagOpenings", new JsonObject { ["viewId"] = plan });
        var tag = f.Model.OfClass(IfcClass.Annotation).Single(t => t.Str("element") == door).Id;
        return (f, wall, door, plan, tag);
    }

    static Views.Rendered Render(Fixture f, string view) => Views.Render(new Q(f), Views.Find(f.Model, view)!, print: true);
    static List<PenText> Texts(Views.Rendered r) => r.Pens.SelectMany(p => p.Texts).ToList();
    static List<(Vec2 A, Vec2 B)> TagLines(Views.Rendered r) => r.Pens.Where(p => p.Layer == "TAG").SelectMany(p => p.Segments).ToList();
    static List<JsonObject> Marks(Fixture f, string view) => Views.Drawing(new Q(f), Views.Find(f.Model, view)!)["marks"]!.AsArray().OfType<JsonObject>().ToList();
    static Vec2 P(JsonNode? n) => new((double)n![0]!, (double)n[1]!);
    static string MarkOf(Fixture f, string door) => TagText.Mark(f.Model, f.Model.Get(door)!);

    [Fact]
    public void A_markers_texts_move_and_turn_and_its_line_ends_reach_out_each_on_its_own()
    {
        var (f, _, door, plan, tag) = Project();
        var mark = MarkOf(f, door);
        var before = Texts(Render(f, plan)).Single(t => t.Text == mark);
        var line = TagLines(Render(f, plan)).Single();
        // The marker tells the interface its parts: both ends, then each text line, with where they would be unmoved.
        var data = Marks(f, plan).Single(m => (string)m["id"]! == tag)["data"]!;
        Assert.True((bool)data["marker"]!);
        Assert.Equal(["a", "b", "o0", "i0"], data["parts"]!.AsArray().Select(p => (string)p!["key"]!).ToArray());

        // The mark 3 mm along the wall and 2 mm out (paper; 1:100: 0.3 m and 0.2 m), turned by 30°.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["part"] = "o0", ["offset"] = new JsonArray(3, 2) });
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["part"] = "o0", ["rot"] = 30 });
        var parts = (JsonObject)f.Model.Get(tag)!.Attrs["parts"]!;
        Assert.Equal(30, (double)parts["o0"]!["rot"]!);
        var after = Texts(Render(f, plan)).Single(t => t.Text == mark);
        var o = data["parts"]!.AsArray().Single(p => (string)p!["key"]! == "o0")!;
        var (u, v) = (P(o["u"]), P(o["v"]));
        var moved = after.At - before.At;
        Assert.Equal(0.3, Vec2.Dot(moved, u), 6);
        Assert.Equal(0.2, Vec2.Dot(moved, v), 6);
        Assert.Equal(LabelParts.Upright(before.Angle + 30), after.Angle, 6);
        // The inner text stays where it was.
        var inner = Texts(Render(f, plan)).Single(t => t.Text.Contains('/'));
        Assert.Equal(before.Angle, inner.Angle, 6);

        // The exterior end 4 mm further (0.4 m), the interior one 2 mm shorter; the mark goes with its end.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["part"] = "b", ["ext"] = 4 });
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["part"] = "a", ["ext"] = -2 });
        var longer = TagLines(Render(f, plan)).Single();
        Assert.Equal((line.B - line.A).Length + 0.4 - 0.2, (longer.B - longer.A).Length, 6);
        var further = Texts(Render(f, plan)).Single(t => t.Text == mark);
        Assert.Equal(0.4, (further.At - after.At).Length, 6);

        // Reset one part, then all: the default layout again, and nothing kept.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["part"] = "o0", ["reset"] = true });
        Assert.Null(f.Model.Get(tag)!.Attrs["parts"]!["o0"]);
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["reset"] = true });
        Assert.Null(f.Model.Get(tag)!.Attrs["parts"]);
        var again = Texts(Render(f, plan)).Single(t => t.Text == mark);
        Assert.Equal(before.At.X, again.At.X, 9);
        Assert.Equal(before.At.Y, again.At.Y, 9);
        Assert.Equal(line.A.Y, TagLines(Render(f, plan)).Single().A.Y, 9);
    }

    [Fact]
    public void A_markers_edits_follow_the_door_when_its_wall_turns()
    {
        var (f, wall, door, plan, tag) = Project();
        var mark = MarkOf(f, door);
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["part"] = "o0", ["offset"] = new JsonArray(5, 0) });
        var before = Texts(Render(f, plan)).Single(t => t.Text == mark);
        var lineBefore = TagLines(Render(f, plan)).Single();
        // A quarter turn of the wall about the origin: the mark's offset turns with it (5 mm along the wall, now along Y).
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(wall), ["op"] = "rotate", ["center"] = new JsonArray(0, 0), ["angle"] = 90 });
        var after = Texts(Render(f, plan)).Single(t => t.Text == mark);
        var lineAfter = TagLines(Render(f, plan)).Single();
        static Vec2 Turn(Vec2 p) => new(-p.Y, p.X);
        var end = new[] { lineAfter.A, lineAfter.B }.MinBy(p => (p - Turn(lineBefore.B)).Length);
        Assert.True((end - Turn(lineBefore.B)).Length < 1e-6 || (end - Turn(lineBefore.A)).Length < 1e-6);
        Assert.Equal(Turn(before.At).X, after.At.X, 6);
        Assert.Equal(Turn(before.At).Y, after.At.Y, 6);
        Assert.InRange(after.Angle, -90 + 1e-9, 90);
    }

    [Fact]
    public void A_tag_types_texts_lie_along_or_across_the_markers_line_always_upright()
    {
        var (f, _, door, plan, _) = Project();
        var mark = MarkOf(f, door);
        // Across the line by default: along the wall (0°).
        Assert.Equal(0, Texts(Render(f, plan)).Single(t => t.Text == mark).Angle, 6);
        Assert.Equal("across", Standards.TagTypeOf(DraftingResources.Book(f.Model), "door")!.TextOrientation);
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "door", ["textOrientation"] = "along" } });
        Assert.Equal("along", ((JsonObject)f.Session.Query("standards.get", new JsonObject())!)["tagTypes"]!.AsArray().Single(t => (string)t!["id"]! == "door")!["textOrientation"]!.GetValue<string>());
        var r = Render(f, plan);
        var line = TagLines(r).Single();
        foreach (var t in Texts(r).Where(t => t.Text == mark || t.Text.Contains('/')))
        {
            // Along the line (across the wall: 90°), read bottom to top; starting at the line's end, running away from it.
            Assert.Equal(90, t.Angle, 6);
            var end = new[] { line.A, line.B }.MinBy(p => (p - t.At).Length);
            Assert.True(Math.Abs(t.At.X - end.X) < 1e-9, $"{t.At} {end}");
            Assert.Equal(t.At.Y > 0 ? "start" : "end", t.Anchor);
        }
        // A wall drawn the other way round reads upright too.
        var g = new Fixture(new ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        var w = g.Wall(6, 3, 0, 3);
        var d2 = (string)g.Run("architecture.addDoor", new JsonObject { ["wallId"] = w, ["along"] = 1.5 })!["id"]!;
        var p2 = (string)g.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = g.Storey })!["id"]!;
        g.Run("architecture.tagOpenings", new JsonObject { ["viewId"] = p2 });
        Assert.Equal(0, Texts(Render(g, p2)).Single(t => t.Text == MarkOf(g, d2)).Angle, 6);
        g.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "door", ["textOrientation"] = "along" } });
        Assert.Equal(90, Texts(Render(g, p2)).Single(t => t.Text == MarkOf(g, d2)).Angle, 6);
        // A turn that would read upside down is folded upright.
        var tag2 = g.Model.OfClass(IfcClass.Annotation).Single(t => t.Str("element") == d2).Id;
        g.Run("drafting.updateTag", new JsonObject { ["id"] = tag2, ["part"] = "o0", ["rot"] = 120 });
        Assert.Equal(30, Texts(Render(g, p2)).Single(t => t.Text == MarkOf(g, d2)).Angle, 6);
    }

    /// <summary>The door's drawn lines (its leaf and swing) and the marker's, as the plan draws them.</summary>
    static List<(Vec2 A, Vec2 B)> Obstacles(Fixture f, string plan, string door)
    {
        var view = Views.Find(f.Model, plan)!;
        var marks = Marks(f, plan);
        var o = new List<(Vec2, Vec2)>();
        foreach (var m in marks.Where(m => m["data"]?["void"] == null && ((string)m["id"]! == door || m["data"]?["of"]?.GetValue<string>() == door)))
        {
            var s = m["segments"]!.AsArray().Select(x => (double)x!).ToArray();
            for (var i = 0; i + 3 < s.Length; i += 4) o.Add((new Vec2(s[i], s[i + 1]), new Vec2(s[i + 2], s[i + 3])));
        }
        Assert.NotEmpty(o);
        return o;
    }

    static (Vec2 C, double Hw, double Hh, double Deg) LabelBox(Fixture f, string plan, out JsonObject label)
    {
        var mk = Marks(f, plan).Single(m => m["data"]?["void"] != null);
        label = (JsonObject)mk["data"]!["label"]!;
        var texts = Texts(Render(f, plan)).Where(t => t.Text == "V1" || t.Text.Contains('×')).ToList();
        var mm = (double)label["mm"]!;
        var boxes = texts.Select(t => LabelParts.BoxOf(t, mm)).ToList();
        var (x0, x1) = (boxes.Min(b => b.C.X - b.Hw), boxes.Max(b => b.C.X + b.Hw));
        var (y0, y1) = (boxes.Min(b => b.C.Y - b.Hh), boxes.Max(b => b.C.Y + b.Hh));
        return (new Vec2((x0 + x1) / 2, (y0 + y1) / 2), (x1 - x0) / 2, (y1 - y0) / 2, 0);
    }

    [Fact]
    public void A_void_label_stands_clear_of_the_doors_swing_and_marker_whichever_way_the_door_opens()
    {
        var (f, _, door, plan, _) = Project();
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["voids"] = true });
        foreach (var flip in new[] { false, true, false })
        {
            if (flip) f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = new JsonArray(door), ["axis"] = "y" });
            var box = LabelBox(f, plan, out _);
            var hits = Obstacles(f, plan, door).Where(l => LabelParts.Crosses(box, l.Item1, l.Item2)).ToList();
            Assert.True(hits.Count == 0, $"flip {flip}: the label at {box.C} crosses {string.Join(", ", hits)}");
        }
    }

    [Fact]
    public void A_moved_and_turned_void_label_keeps_its_place_by_the_void_when_the_wall_moves_and_resets()
    {
        var (f, wall, _, plan, _) = Project();
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["voids"] = true });
        LabelBox(f, plan, out var label);
        var key = Marks(f, plan).Single(m => m["data"]?["void"] != null)["data"]!["key"]!.GetValue<string>();
        var middle = P(label["base"]);
        // 8 mm along the wall and 6 mm across it from the void's middle (0.8 m, 0.6 m at 1:100), turned 90°.
        f.Run("view.voidLabel", new JsonObject { ["viewId"] = plan, ["key"] = key, ["offset"] = new JsonArray(8, 6) });
        f.Run("view.voidLabel", new JsonObject { ["viewId"] = plan, ["key"] = key, ["rot"] = 90 });
        LabelBox(f, plan, out label);
        Assert.Equal(middle.X + 0.8, P(label["at"]).X, 6);
        Assert.Equal(middle.Y + 0.6, P(label["at"]).Y, 6);
        Assert.Equal(90, Texts(Render(f, plan)).Single(t => t.Text == "V1").Angle, 6);
        // The wall moved: the label goes with the void.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(wall), ["op"] = "move", ["by"] = new JsonArray(1, 2) });
        LabelBox(f, plan, out label);
        Assert.Equal(middle.X + 1 + 0.8, P(label["at"]).X, 6);
        Assert.Equal(middle.Y + 2 + 0.6, P(label["at"]).Y, 6);
        // Reset: where it stands by default again, nothing kept on the view.
        f.Run("view.voidLabel", new JsonObject { ["viewId"] = plan, ["key"] = key, ["reset"] = true });
        Assert.Null(f.Model.Get(plan)!.Attrs["voidLabels"]);
        LabelBox(f, plan, out label);
        Assert.False((bool)label["moved"]!);
        Assert.Equal(0, Texts(Render(f, plan)).Single(t => t.Text == "V1").Angle, 6);
    }
}
