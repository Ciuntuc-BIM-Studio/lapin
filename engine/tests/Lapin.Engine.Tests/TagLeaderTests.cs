using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Decision 331, a tester's idea: tag leaders as ArchiCAD's labels, an oblique segment from the element to a knee, then a
/// shoulder under the text. The points for each underline (first, each, last, none) and the extension; the text flipped
/// to the knee's side; an elbow by default, a tag bent by hand kept straight, a tag's own shape; a build-up tag's leaders
/// per layer with their knees in one column; the knee following its element; the tag type through JSON; the preview.
/// </summary>
public class TagLeaderTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonObject Draw(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonObject Mark(JsonObject d, string id) => d["marks"]!.AsArray().OfType<JsonObject>().First(m => (string)m["id"]! == id);
    static double[] Pt(JsonNode? n) => n!.AsArray().Select(x => (double)x!).ToArray();
    static readonly TextStyle Style = Standards.DefaultText;
    static readonly TagType Plain = new("t", "T", "{mark}", []);
    static readonly string[] Two = ["D1", "90/210"];
    const double H = 2.5, Pad = TagLeaders.Pad * H, Under = TagLeaders.Under * H, Pitch = TagLeaders.Pitch * H;

    /// <summary>The mark's drawn lines (not what a click hits), the dot's left out.</summary>
    static List<(Vec2 A, Vec2 B)> Lines(ViewMark m) => m.Lines.Where(l => l.Layer == "TAG" && l.Width == Pens.Default.Hatch).Select(l => (l.A, l.B)).ToList();
    static double Width(IEnumerable<string> lines) => lines.Max(l => BuildupText.Measure(l, H, false));
    static void Near(Vec2 want, Vec2 got) { Assert.Equal(want.X, got.X, 6); Assert.Equal(want.Y, got.Y, 6); }

    [Fact]
    public void An_elbow_runs_from_the_element_to_its_knee_then_under_the_first_row_with_no_box()
    {
        var (m, head, side) = TagLeaders.Plain("t", Two, Style, Plain, 1, new Vec2(8, 8), new Vec2(0, 0), true, "#000000");
        var far = 8 + Pad + Width(Two) + Pad;
        Assert.Equal(1, side);
        // The oblique segment and the shoulder, nothing else drawn: no box round the text.
        var lines = Lines(m);
        Assert.Equal(2, lines.Count);
        Near(new Vec2(0, 0), lines[0].A); Near(new Vec2(8, 8), lines[0].B);
        Near(new Vec2(8, 8), lines[1].A); Near(new Vec2(far, 8), lines[1].B);
        // The first row just over the shoulder, the second under it, both left-aligned after the knee.
        Assert.All(m.Texts, t => Assert.Equal("start", t.Text.Anchor));
        Near(new Vec2(8 + Pad, 8 + Under), m.Texts[0].Text.At);
        Near(new Vec2(8 + Pad, 8 + Under - Pitch), m.Texts[1].Text.At);
        Assert.Equal((8 + far) / 2, head.X, 6);
        // A circle at the end, as a straight leader's.
        Assert.Contains(m.Lines, l => l.Layer == "TAG" && l.Width == Pens.Default.Seen);
    }

    [Fact]
    public void The_shoulder_underlines_each_row_the_last_or_none_and_reaches_the_extension_further()
    {
        var w = Width(Two);
        // Each: the shoulder under the first, a line under the second from the knee's column to the same far end.
        var (each, _, _) = TagLeaders.Plain("t", Two, Style, Plain with { Underline = "each" }, 1, new Vec2(8, 8), new Vec2(0, 0), true, "#000000");
        var lines = Lines(each);
        Assert.Equal(3, lines.Count);
        var row2 = 8 + Under - Pitch - Under;
        Near(new Vec2(8, row2), lines[2].A); Near(new Vec2(8 + 2 * Pad + w, row2), lines[2].B);

        // Last: the knee under the last row, the others above it.
        var (last, _, _) = TagLeaders.Plain("t", Two, Style, Plain with { Underline = "last" }, 1, new Vec2(8, 8), new Vec2(0, 0), true, "#000000");
        Near(new Vec2(8 + Pad, 8 + Under), last.Texts[1].Text.At);
        Near(new Vec2(8 + Pad, 8 + Under + Pitch), last.Texts[0].Text.At);

        // None: the knee at the first row's middle, the shoulder no more than the extension, stopping at the text's side.
        var (none, _, _) = TagLeaders.Plain("t", Two, Style, Plain with { Underline = "none" }, 1, new Vec2(8, 8), new Vec2(0, 0), true, "#000000");
        Assert.Single(Lines(none));
        Near(new Vec2(8 + Pad, 8 - H / 2), none.Texts[0].Text.At);
        var (ext, _, _) = TagLeaders.Plain("t", Two, Style, Plain with { Underline = "none", Extension = 2 }, 1, new Vec2(8, 8), new Vec2(0, 0), true, "#000000");
        Near(new Vec2(10, 8), Lines(ext)[1].B);
        Near(new Vec2(10 + Pad, 8 - H / 2), ext.Texts[0].Text.At);

        // The extension on an underline: the text that much further from the knee, the shoulder that much longer.
        var (longer, _, _) = TagLeaders.Plain("t", Two, Style, Plain with { Extension = 3 }, 1, new Vec2(8, 8), new Vec2(0, 0), true, "#000000");
        Near(new Vec2(8 + 3 + 2 * Pad + w, 8), Lines(longer)[1].B);
        Near(new Vec2(8 + 3 + Pad, 8 + Under), longer.Texts[0].Text.At);
        // The paper's millimetres in the view's units: at 1:50 the same tag 0.05 m a millimetre.
        var (scaled, _, _) = TagLeaders.Plain("t", Two, Style, Plain with { Extension = 3 }, 0.05, new Vec2(8, 8), new Vec2(0, 0), true, "#000000");
        Near(new Vec2(8 + 0.05 * (3 + 2 * Pad + w), 8), Lines(scaled)[1].B);
    }

    [Fact]
    public void A_knee_on_the_other_side_of_the_end_flips_the_text_and_aligns_it_towards_the_knee()
    {
        var (m, head, side) = TagLeaders.Plain("t", Two, Style, Plain, 1, new Vec2(-8, 8), new Vec2(0, 0), true, "#000000");
        Assert.Equal(-1, side);
        var lines = Lines(m);
        Near(new Vec2(-8, 8), lines[0].B);
        Near(new Vec2(-8 - 2 * Pad - Width(Two), 8), lines[1].B);
        Assert.All(m.Texts, t => Assert.Equal("end", t.Text.Anchor));
        Near(new Vec2(-8 - Pad, 8 + Under), m.Texts[0].Text.At);
        Assert.True(head.X < -8);
        // Straight up over the end: still on the right.
        Assert.Equal(1, TagLeaders.Plain("t", Two, Style, Plain, 1, new Vec2(0, 8), new Vec2(0, 0), true, "#000000").Side);
        // No leader: the text keeps its side, nothing but the text is drawn.
        var (off, _, offSide) = TagLeaders.Plain("t", Two, Style, Plain, 1, new Vec2(-8, 8), new Vec2(0, 0), false, "#000000");
        Assert.Equal(-1, offSide);
        Assert.DoesNotContain(off.Lines, l => l.Layer == "TAG");
    }

    [Fact]
    public void A_new_tag_is_an_elbow_its_point_the_knee_and_the_knee_follows_its_wall()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var view = "plan:" + f.Storey;
        var t = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = w, ["field"] = "name", ["at"] = new JsonArray(4, 2) })!["id"]!;
        var data = Mark(Draw(f, view), t)["data"]!;
        Assert.Equal("elbow", (string)data["shape"]!);
        Assert.Equal([4.0, 2.0], Pt(data["knee"]));
        Assert.Equal(2.5, Pt(data["end"])[0], 3);                                     // the middle of the wall's cut
        Assert.Equal(0, Pt(data["end"])[1], 2);
        Assert.Equal(1, (int)data["side"]!);
        Assert.True(Pt(data["head"])[0] > 4);
        Assert.Null(data["from"]);
        // The knee dragged over the end's other side: the text goes to the left.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["at"] = new JsonArray(1, 2) });
        data = Mark(Draw(f, view), t)["data"]!;
        Assert.Equal(-1, (int)data["side"]!);
        Assert.True(Pt(data["head"])[0] < 1);
        // The wall moved: the knee and the end with it, the text's side kept.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(w), ["op"] = "move", ["by"] = new JsonArray(0, -2) });
        data = Mark(Draw(f, view), t)["data"]!;
        Assert.Equal(1, Pt(data["knee"])[0], 6);
        Assert.Equal(0, Pt(data["knee"])[1], 6);
        Assert.Equal(-2, Pt(data["end"])[1], 2);
        Assert.Equal(-1, (int)data["side"]!);
        // Its own shape straight: the box again, its leader from the box's side.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["leaderShape"] = "straight" });
        data = Mark(Draw(f, view), t)["data"]!;
        Assert.Equal("straight", (string)data["shape"]!);
        Assert.NotNull(data["from"]);
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["leaderShape"] = null });
        Assert.Equal("elbow", (string)Mark(Draw(f, view), t)["data"]!["shape"]!);
        Assert.Throws<CommandException>(() => f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["leaderShape"] = "spline" }));
    }

    [Fact]
    public void A_tag_bent_by_hand_keeps_its_straight_leader_until_it_is_made_an_elbow()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var view = "plan:" + f.Storey;
        // An older tag: bent at a free elbow, its type saying nothing of its leader.
        var t = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = w, ["tagType"] = "fire", ["at"] = new JsonArray(4, 3), ["elbow"] = new JsonArray(1, 3) })!["id"]!;
        var data = Mark(Draw(f, view), t)["data"]!;
        Assert.Equal("straight", (string)data["shape"]!);
        Assert.Equal([1.0, 3.0], Pt(data["elbow"]));
        // Even when its type says elbow: what the user bent stays.
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "fire", ["leaderShape"] = "elbow" } });
        Assert.Equal("straight", (string)Mark(Draw(f, view), t)["data"]!["shape"]!);
        // Made an elbow by hand: the free bend goes.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["leaderShape"] = "elbow" });
        Assert.Null(f.Model.Get(t)!.Attrs["elbow"]);
        Assert.Equal("elbow", (string)Mark(Draw(f, view), t)["data"]!["shape"]!);
        // A type made straight: its tags have boxes.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["leaderShape"] = null });
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "fire", ["leaderShape"] = "straight" } });
        Assert.Equal("straight", (string)Mark(Draw(f, view), t)["data"]!["shape"]!);
    }

    [Fact]
    public void A_tag_type_s_leader_goes_through_json_and_back_an_unknown_value_dropped()
    {
        var f = F();
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "fire", ["leaderShape"] = "straight", ["underline"] = "each", ["extension"] = 2.5 } });
        var fire = Standards.TagTypeOf(DraftingResources.Book(f.Model), "fire")!;
        Assert.Equal(("straight", "each", 2.5), (fire.LeaderShape, fire.Underline, fire.Extension));
        var read = Standards.Read(new JsonObject { ["tagTypes"] = new JsonArray(new JsonObject { ["id"] = "x", ["template"] = "{mark}", ["leaderShape"] = "spline", ["underline"] = "middle", ["extension"] = -1 }) }).TagTypes[0];
        Assert.Equal((null, null, null), (read.LeaderShape, read.Underline, read.Extension));
        Assert.Equal(("elbow", "first", 0.0), (read.LeaderShapeOrDefault, read.UnderlineOrDefault, read.ExtensionOrDefault));
        // What the standards give the interface: only what was set.
        var json = (JsonObject)f.Session.Query("standards.get", new JsonObject())!;
        var entry = json["tagTypes"]!.AsArray().OfType<JsonObject>().First(x => (string)x["id"]! == "fire");
        Assert.Equal("each", (string)entry["underline"]!);
        Assert.Null(json["tagTypes"]!.AsArray().OfType<JsonObject>().First(x => (string)x["id"]! == "acoustic")["leaderShape"]);
    }

    /// <summary>A wall of three layers 5 m along x, a build-up tag type with leaders per layer.</summary>
    static (Fixture F, string Wall, string View) LayeredWall(string underline)
    {
        var f = F();
        var type = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Three",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Render", ["thickness"] = 0.02 },
                new JsonObject { ["material"] = "Brick", ["thickness"] = 0.25, ["core"] = true },
                new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.015 }),
        })!["id"]!;
        var w = f.Wall(0, 0, 5, 0, type);
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "buildup", ["leaders"] = "per-layer", ["underline"] = underline } });
        return (f, w, "plan:" + f.Storey);
    }

    static List<(Vec2 A, Vec2 B)> Segments(JsonObject mark)
    {
        var s = mark["segments"]!.AsArray().Select(x => (double)x!).ToList();
        return Enumerable.Range(0, s.Count / 4).Select(i => (new Vec2(s[4 * i], s[4 * i + 1]), new Vec2(s[4 * i + 2], s[4 * i + 3]))).ToList();
    }

    /// <summary>What a drawing draws on the tags' layer (one tag in it): its lines, not what a click hits.</summary>
    static List<(Vec2 A, Vec2 B)> TagLayer(JsonObject d)
    {
        var s = d["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string?)p["layer"] == "TAG" && p["segments"] != null)
            .SelectMany(p => p["segments"]!.AsArray()).Select(x => (double)x!).ToList();
        return Enumerable.Range(0, s.Count / 4).Select(i => (new Vec2(s[4 * i], s[4 * i + 1]), new Vec2(s[4 * i + 2], s[4 * i + 3]))).ToList();
    }

    [Fact]
    public void A_build_up_tag_s_leaders_per_layer_end_under_their_rows_their_knees_one_column()
    {
        var (f, w, view) = LayeredWall("each");
        var t = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = w, ["tagType"] = "buildup", ["at"] = new JsonArray(3.5, 2) })!["id"]!;
        var mark = Mark(Draw(f, view), t);
        var data = mark["data"]!;
        Assert.Equal("elbow", (string)data["shape"]!);
        Assert.True((bool)data["buildup"]!);
        var snaps = mark["snaps"]!.AsArray().Select(x => (double)x!).ToList();
        var ends = Enumerable.Range(0, snaps.Count / 2 - 1).Select(i => new Vec2(snaps[2 * i], snaps[2 * i + 1])).ToList();
        Assert.Equal(3, ends.Count);
        var segs = Segments(mark);
        var knees = ends.Select(e => segs.First(s => (s.A - e).Length < 1e-4 && (s.B - e).Length > 1e-3).B).ToList();
        // Each from inside its layer to its knee, the knees one under the other on the tag's point's line, the first on it.
        Assert.All(knees, k => Assert.Equal(3.5, k.X, 4));
        Assert.Equal(2, knees[0].Y, 4);
        Assert.True(knees[0].Y > knees[1].Y && knees[1].Y > knees[2].Y);
        // Each on along its row to the block's far side, rightwards: the tag stands right of the wall's middle.
        foreach (var k in knees)
        {
            var shoulder = segs.First(s => (s.A - k).Length < 1e-4 && Math.Abs(s.B.Y - k.Y) < 1e-6 && s.B.X > k.X + 1e-3);
            Assert.True(shoulder.B.X > 3.5);
        }
        Assert.Equal(1, (int)data["side"]!);
        // The knees' column dragged left of the leaders' ends: the block goes to its left, the knees still one column.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["at"] = new JsonArray(1, 2) });
        mark = Mark(Draw(f, view), t);
        Assert.Equal(-1, (int)mark["data"]!["side"]!);
        segs = Segments(mark);
        var bands = LayerSets.BandsOf(f.Model, w)!;
        var moved = segs.Where(s => Math.Abs(s.A.X - 2.5) < 1e-4 && Math.Abs(s.B.X - 1) < 1e-4).ToList();
        Assert.Equal(3, moved.Count);
        for (var i = 0; i < 3; i++) Assert.InRange(moved[i].A.Y, bands[i].From, bands[i].To);
    }

    [Fact]
    public void A_build_up_tag_with_one_leader_underlines_its_first_row_and_with_none_stops_at_the_block()
    {
        var (f, w, view) = LayeredWall("first");
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "buildup", ["leaders"] = "one" } });
        var t = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = w, ["tagType"] = "buildup", ["at"] = new JsonArray(4, 2) })!["id"]!;
        var mark = Mark(Draw(f, view), t);
        var segs = Segments(mark);
        // The oblique from the wall's middle to the knee, then the shoulder under EXTERIOR, the header's row.
        Assert.Contains(segs, s => (s.A - new Vec2(2.5, 0)).Length < 1e-4 && (s.B - new Vec2(4, 2)).Length < 1e-4);
        Assert.Contains(segs, s => (s.A - new Vec2(4, 2)).Length < 1e-4 && Math.Abs(s.B.Y - 2) < 1e-6 && s.B.X > 4.5);
        var texts = Draw(f, view)["pens"]!.AsArray().OfType<JsonObject>().Where(p => p["texts"] != null).SelectMany(p => p["texts"]!.AsArray()).OfType<JsonObject>().ToList();
        var header = texts.First(x => (string)x["text"]! == "EXTERIOR");
        Assert.True((double)header["y"]! > 2 && (double)header["y"]! < 2 + 0.05 * 2.5);
        // Underline none: the knee at the header's middle, no line along the text.
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "buildup", ["underline"] = "none" } });
        segs = TagLayer(Draw(f, view));
        Assert.Contains(segs, s => (s.B - new Vec2(4, 2)).Length < 1e-4);
        Assert.DoesNotContain(segs, s => Math.Abs(s.A.Y - s.B.Y) < 1e-9 && Math.Abs(s.A.X - s.B.X) > 0.1);
    }

    [Fact]
    public void The_preview_lays_a_type_out_on_paper_as_views_draw_it()
    {
        var (f, _, _) = LayeredWall("each");
        var p = (JsonObject)f.Session.Query("drafting.tagPreview", new JsonObject { ["tagType"] = "buildup" })!;
        Assert.Equal("elbow", (string)p["shape"]!);
        Assert.Equal(3, p["strip"]!.AsArray().Count);
        Assert.True(p["segments"]!.AsArray().Count >= 6);
        Assert.Contains(p["texts"]!.AsArray().OfType<JsonObject>(), x => (string)x["text"]! == "EXTERIOR");
        // A draft not yet saved: a plain type, straight, its box and leader; elbow, its oblique and shoulder.
        var straight = (JsonObject)f.Session.Query("drafting.tagPreview", new JsonObject { ["entry"] = new JsonObject { ["id"] = "x", ["template"] = "{mark}|{FireRating}", ["leaderShape"] = "straight" } })!;
        Assert.Equal("straight", (string)straight["shape"]!);
        Assert.Equal(["mark", "FireRating"], straight["texts"]!.AsArray().Select(x => (string)x!["text"]!));
        var elbow = (JsonObject)f.Session.Query("drafting.tagPreview", new JsonObject { ["entry"] = new JsonObject { ["id"] = "x", ["template"] = "{mark}" } })!;
        Assert.Equal("elbow", (string)elbow["shape"]!);
        var b = Pt(elbow["bounds"]);
        Assert.True(b[2] > b[0] && b[3] > b[1]);
    }

    [Fact]
    public void Tags_an_older_Lapin_wrote_stay_straight_new_ones_are_untouched()
    {
        var old = new Entity("t1", IfcClass.Annotation, "Tag", new JsonObject { ["viewId"] = "v", ["element"] = "w", ["at"] = new JsonArray(0, 0) }, "TAG");
        var own = new Entity("t2", IfcClass.Annotation, "Tag", new JsonObject { ["viewId"] = "v", ["element"] = "w", ["at"] = new JsonArray(0, 0), ["leaderShape"] = "elbow" }, "TAG");
        var made = new Entity("t3", IfcClass.Annotation, "Tag", new JsonObject { ["viewId"] = "v", ["element"] = "w", ["at"] = new JsonArray(0, 0), [TagLeaders.RevKey] = TagLeaders.Rev }, "TAG");
        var (es, _) = new TagLeaderUpgrade().Upgrade([old, own, made], []);
        Assert.Equal("straight", es[0].Str("leaderShape"));
        Assert.Equal("elbow", es[1].Str("leaderShape"));
        Assert.Same(made, es[2]);
        var (again, _) = new TagLeaderUpgrade().Upgrade(es, []);
        Assert.Equal(es, again);
    }

    [Fact]
    public void A_new_tag_is_stamped_so_reopening_keeps_its_elbow()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var id = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["element"] = w, ["at"] = new JsonArray(2, 2) })!["id"]!;
        var tag = f.Session.Model.Get(id)!;
        Assert.Equal(TagLeaders.Rev, tag.Attrs[TagLeaders.RevKey]!.GetValue<int>());
        var (es, _) = new TagLeaderUpgrade().Upgrade([tag], []);
        Assert.Null(es[0].Str("leaderShape"));
    }
}
