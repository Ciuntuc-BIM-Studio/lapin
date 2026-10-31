using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The rebar labels and balloons (decision 221): a style resolved most-specific-wins (the project's settings, the type, the
/// element), its parts making the text; never a balloon without a mark; the generated labels stacked in the marks' order and
/// placed on the view by the user.
/// </summary>
public class RebarLabelTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonArray Pens(JsonObject d) => (JsonArray)d["pens"]!;
    static List<string?> Texts(JsonObject d) => Pens(d).SelectMany(p => (p!["texts"] as JsonArray)?.Select(t => (string?)t!["text"]) ?? []).ToList();
    /// <summary>The balloons drawn: circles of 32 segments on the annotation layer, with the balloon's pen.</summary>
    static int Balloons(JsonObject d) => Pens(d).Where(p => (string?)p!["layer"] == RebarShapes.AnnotationLayer && Math.Abs((double)p["width"]! - 0.35) < 1e-9).Sum(p => ((p!["segments"] as JsonArray)?.Count ?? 0) / 4) / 32;
    static JsonObject Mark(JsonObject d, string id) => (JsonObject)((JsonArray)d["marks"]!).First(m => (string?)m!["id"] == id)!;
    static JsonObject Label(JsonObject mark, string key) => (JsonObject)((JsonArray)mark["data"]!["labels"]!).First(x => (string?)x!["key"] == key)!;
    static (double X, double Y) XY(JsonNode? l) => ((double)l![0]!, (double)l[1]!);
    static (double X, double Y) LabelAt(JsonObject mark, string key) => XY(Label(mark, key)["at"]);
    /// <summary>The segments drawn with the leader's pen on the annotation layer, each as its two points.</summary>
    static List<((double X, double Y) A, (double X, double Y) B)> Thin(JsonObject d) =>
        Pens(d).Where(p => (string?)p!["layer"] == RebarShapes.AnnotationLayer && Math.Abs((double)p["width"]! - RebarLabels.LeaderWidth) < 1e-9)
            .SelectMany(p => (p!["segments"] as JsonArray ?? []).Chunk(4).Select(s => (((double)s[0]!, (double)s[1]!), ((double)s[2]!, (double)s[3]!)))).ToList();
    static double Len(((double X, double Y) A, (double X, double Y) B) s) => Math.Sqrt((s.B.X - s.A.X) * (s.B.X - s.A.X) + (s.B.Y - s.A.Y) * (s.B.Y - s.A.Y));
    /// <summary>A string's ticks: 2 mm on paper, across.</summary>
    static List<((double X, double Y) A, (double X, double Y) B)> Ticks(JsonObject d) => Thin(d).Where(s => Math.Abs(Len(s) - RebarLabels.TickMm * Mm(d)) < 1e-6).ToList();
    static JsonObject TextOf(JsonObject d, string text) => Pens(d).SelectMany(p => (p!["texts"] as JsonArray)?.OfType<JsonObject>() ?? []).First(t => (string?)t["text"] == text);
    static JsonObject Own(JsonObject mark) => (JsonObject)mark["data"]!["label"]!;
    static double Mm(JsonObject d) => (double)d["scale"]! / 1000;

    [Fact]
    public void The_parts_make_the_text_and_the_defaults_differ_by_kind()
    {
        Assert.Equal("3Ø16 L=6000", LabelStyle.Default("bars").Text(new LabelFacts("3", 0.016, null, 6.0, 1, "B500C")));
        Assert.Equal("41Ø8/150", LabelStyle.Default("stirrups").Text(new LabelFacts("41", 0.008, "150", 2.16, 1, "B500C")));
        Assert.Equal("25Ø8/100-200", LabelStyle.Default("runs").Text(new LabelFacts("25", 0.008, "100-200", 2.16, 1, "B500C")));
        Assert.Equal("8Ø16", LabelStyle.Default("columnBars").Text(new LabelFacts("8", 0.016, null, 4.0, 1, "B500C")));
        // Any order, with the mark, the steel and free text; a part without a value is left out.
        var custom = LabelStyle.Default("bars").Apply(LabelStyle.Patch(null, new JsonObject { ["parts"] = "mark text:– count diameter steel length" }));
        Assert.Equal("2 – 3Ø16 B500C L=6000", custom.Text(new LabelFacts("3", 0.016, null, 6.0, 2, "B500C")));
        Assert.Equal("– 3Ø16 B500C", custom.Text(new LabelFacts("3", 0.016, null, null, 0, "B500C")));
        Assert.Equal("along", LabelStyle.Default("stirrups").Direction);
        Assert.Equal("horizontal", LabelStyle.Default("bars").Direction);
        Assert.Throws<CommandException>(() => LabelStyle.Patch(null, new JsonObject { ["parts"] = "count weight" }));
        Assert.Throws<CommandException>(() => LabelStyle.Patch(null, new JsonObject { ["direction"] = "sideways" }));
        Assert.Equal(30, LabelStyle.AngleOf("angle:30"));
        // Never a balloon without a mark.
        Assert.False(LabelStyle.Default("bars").ShowsBalloon(0));
        Assert.True(LabelStyle.Default("bars").ShowsBalloon(1));
        Assert.False(LabelStyle.Default("bars").Apply(new JsonObject { ["balloon"] = false }).ShowsBalloon(1));
    }

    [Fact]
    public void The_style_comes_from_the_settings_then_the_type_then_the_element_the_most_specific_winning()
    {
        var f = F();
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(8, 0) })!["id"]!;
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shape"] = "straight", ["at"] = new JsonArray(1, 1), ["params"] = new JsonObject { ["length"] = 2.5 }, ["count"] = 4 })!["id"]!;
        Assert.Contains("4Ø12 L=2500", Texts(Drawing(f, view)));
        // The project's settings for bars: the count and the diameter alone, no balloon.
        f.Run("rebar.setSettings", new JsonObject { ["labels"] = new JsonObject { ["bars"] = new JsonObject { ["parts"] = "count diameter", ["balloon"] = false } } });
        var settings = (JsonObject)f.Session.Query("rebar.settings", new JsonObject())!;
        Assert.Equal("count,diameter", string.Join(",", ((JsonArray)settings["labels"]!["bars"]!["parts"]!).Select(x => (string?)x)));
        Assert.Equal("along", (string?)settings["labels"]!["stirrups"]!["direction"]);
        var d = Drawing(f, view);
        Assert.Contains("4Ø12", Texts(d));
        Assert.DoesNotContain("4Ø12 L=2500", Texts(d));
        Assert.Equal(0, Balloons(d));
        // The bar's own style wins over the project's, field by field: its parts back, the balloon still off.
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["label"] = new JsonObject { ["style"] = new JsonObject { ["parts"] = new JsonArray("count", "diameter", "length") } } });
        d = Drawing(f, view);
        Assert.Contains("4Ø12 L=2500", Texts(d));
        Assert.Equal(0, Balloons(d));
        // Its field taken back (null), the project's shows again; the project's taken back, the default.
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["label"] = new JsonObject { ["style"] = new JsonObject { ["parts"] = null } } });
        Assert.Contains("4Ø12", Texts(Drawing(f, view)));
        Assert.Null(f.Model.Get(bar)!.Attrs["label"]);
        f.Run("rebar.setSettings", new JsonObject { ["labels"] = new JsonObject { ["bars"] = null } });
        d = Drawing(f, view);
        Assert.Contains("4Ø12 L=2500", Texts(d));
        Assert.Equal(1, Balloons(d));
        // A type's stirrups take their style on the type: the label without its balloon, the count and the step only.
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var type = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["enabled"] = true, ["step"] = 0.15 });
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, -2), ["end"] = new JsonArray(7, -2) })!["id"]!;
        // Two balloons: the stirrups' and the first bar's, seen here from the side where it stands in space.
        d = Drawing(f, along);
        Assert.Contains("41Ø8/150", Texts(d));
        Assert.Equal(2, Balloons(d));
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["label"] = new JsonObject { ["balloon"] = false, ["parts"] = "count step" } });
        Assert.False((bool)f.Model.Get(type)!.Attrs["stirrups"]!["label"]!["balloon"]!);
        d = Drawing(f, along);
        Assert.Contains("41/150", Texts(d));
        Assert.Equal(1, Balloons(d));
        // The unknown is refused.
        Assert.Throws<CommandException>(() => f.Run("rebar.setSettings", new JsonObject { ["labels"] = new JsonObject { ["beams"] = new JsonObject() } }));
    }

    [Fact]
    public void The_labels_of_several_groups_in_a_cross_section_stack_in_the_marks_order_a_row_apart()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var top = Beams.Top(f.Model, f.Model.Get(beam)!);
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(6, 0), ["scale"] = 50 })!["id"]!;
        // Two bars at the same level, mark 3 drawn first, mark 2 after: in the cross-section their dots share a height, their labels cannot.
        var later = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(0.5, top - 0.36), ["params"] = new JsonObject { ["length"] = 5 }, ["diameter"] = 0.016, ["mark"] = 3, ["across"] = new JsonObject { ["count"] = 2, ["mode"] = "cover" } })!["id"]!;
        var first = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(0.5, top - 0.36), ["params"] = new JsonObject { ["length"] = 5 }, ["diameter"] = 0.012, ["mark"] = 2, ["array"] = new JsonObject { ["nx"] = 1, ["ny"] = 1 }, ["across"] = new JsonObject { ["count"] = 2, ["mode"] = "step", ["step"] = 0.06 } })!["id"]!;
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 3 })!["id"]!;
        var d = Drawing(f, across);
        var mm = (double)d["scale"]! / 1000;
        var a = LabelAt(Mark(d, $"{first}:projection"), "projection");
        var b = LabelAt(Mark(d, $"{later}:projection"), "projection");
        // Both to the right of their dots; mark 2 above, mark 3 a row (the text's height and 2 mm) under it.
        Assert.Equal(a.Y - (2.5 + 2) * mm, b.Y, 6);
        Assert.Equal(2, Balloons(d));
        var texts = Texts(d);
        Assert.Contains("2Ø12 L=5000", texts);
        Assert.Contains("2Ø16 L=5000", texts);
        // A third group with its own mark at the same height goes one more row under.
        var third = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(0.5, top - 0.36), ["params"] = new JsonObject { ["length"] = 5 }, ["diameter"] = 0.02, ["mark"] = 4, ["across"] = new JsonObject { ["count"] = 2, ["mode"] = "step", ["step"] = 0.12 } })!["id"]!;
        d = Drawing(f, across);
        var cc = LabelAt(Mark(d, $"{third}:projection"), "projection");
        Assert.Equal(b.Y - (2.5 + 2) * mm, cc.Y, 6);
    }

    [Fact]
    public void A_generated_label_is_placed_on_the_view_by_the_user_and_put_back_and_a_type_s_stirrups_have_theirs()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var type = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["enabled"] = true, ["step"] = 0.15 });
        var top = Beams.Top(f.Model, f.Model.Get(beam)!);
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, -2), ["end"] = new JsonArray(7, -2), ["scale"] = 50 })!["id"]!;
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(1.5, top - 0.36), ["params"] = new JsonObject { ["length"] = 5 }, ["diameter"] = 0.016, ["across"] = new JsonObject { ["count"] = 3, ["mode"] = "cover" } })!["id"]!;
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 3 })!["id"]!;
        // The stirrups' mark is the beam's, keyed; the bar's projection the bar's; each tells where its label stands.
        var d = Drawing(f, along);
        var stirrups = Mark(d, $"{beam}:stirrups");
        var auto = LabelAt(stirrups, "stirrups");
        Assert.Equal(beam, (string?)((JsonArray)stirrups["data"]!["labels"]!)[0]!["owner"]);
        d = Drawing(f, across);
        var before = LabelAt(Mark(d, $"{bar}:projection"), "projection");
        // Moved by its grip: kept on the view, drawn there, its leader reaching the bars.
        f.Run("rebar.placeLabel", new JsonObject { ["viewId"] = across, ["owner"] = bar, ["key"] = "projection", ["at"] = new JsonArray(1.2, 0.9) });
        Assert.Equal("{\"at\":[1.2,0.9]}", f.Model.Get(across)!.Attrs["rebarLabels"]![$"{bar}:projection"]!.ToJsonString());
        d = Drawing(f, across);
        Assert.Equal((1.2, 0.9), LabelAt(Mark(d, $"{bar}:projection"), "projection"));
        Assert.Contains(Pens(d), p => (string?)p!["layer"] == RebarShapes.AnnotationLayer && Math.Abs((double)p["width"]! - 0.25) < 1e-9);
        // Put back: the view forgets it and the engine's place returns.
        f.Run("rebar.placeLabel", new JsonObject { ["viewId"] = across, ["owner"] = bar, ["key"] = "projection", ["at"] = null });
        Assert.Null(f.Model.Get(across)!.Attrs["rebarLabels"]);
        Assert.Equal(before, LabelAt(Mark(Drawing(f, across), $"{bar}:projection"), "projection"));
        // The stirrups' label moved on the longitudinal section, and the beam's own undo step.
        f.Run("rebar.placeLabel", new JsonObject { ["viewId"] = along, ["owner"] = beam, ["key"] = "stirrups", ["at"] = new JsonArray(3, 5) });
        Assert.Equal((3.0, 5.0), LabelAt(Mark(Drawing(f, along), $"{beam}:stirrups"), "stirrups"));
        f.Session.Undo();
        Assert.Equal(auto, LabelAt(Mark(Drawing(f, along), $"{beam}:stirrups"), "stirrups"));
        Assert.Throws<CommandException>(() => f.Run("rebar.placeLabel", new JsonObject { ["viewId"] = along, ["owner"] = beam, ["at"] = new JsonArray(3, 5) }));
    }

    // ─── Decision 222: the distributions' string, the leader's elbow, a leader to each dot ───

    [Fact]
    public void A_distribution_is_a_dimension_string_with_a_tick_at_every_bar_the_text_along_it_and_the_balloon_at_its_start()
    {
        var f = F();
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(8, 0), ["scale"] = 50 })!["id"]!;
        var run = (string)f.Run("rebar.addRun", new JsonObject { ["viewId"] = view, ["start"] = new JsonArray(1, 0.3), ["end"] = new JsonArray(3, 0.3), ["step"] = 0.2, ["params"] = new JsonObject { ["a"] = 0.25, ["b"] = 0.4 } })!["id"]!;
        var d = Drawing(f, view);
        var mm = Mm(d);
        // Eleven positions, one at each end: eleven ticks across the line, no values; the line itself the path's length, under the bars.
        var ticks = Ticks(d);
        Assert.Equal(11, ticks.Count);
        Assert.All(ticks, t => Assert.Equal(t.A.X, t.B.X, 9));
        var line = Thin(d).Single(s => Math.Abs(Len(s) - 2) < 1e-6);
        Assert.True(line.A.Y < 0.3 - 0.2, "the string stands under the stirrups");
        Assert.Equal(line.A.Y, ticks[0].A.Y + 1 * mm, 9);
        // The text above the line, centred, along it; the balloon touching its start, so no leader is needed.
        var text = TextOf(d, "11Ø8/200");
        Assert.Equal(2, (double)text["x"]!, 9);
        Assert.Equal(line.A.Y + 1 * mm, (double)text["y"]!, 9);
        Assert.Equal(0, (double)text["angle"]!);
        Assert.Equal(1, Balloons(d));
        var label = Own(Mark(d, run));
        Assert.Equal(1 - 3.75 * mm, XY(label["at"]).X, 9);
        Assert.Equal(line.A.Y, XY(label["at"]).Y, 9);
        Assert.Equal(4, (double)label["string"]!["offset"]!);
        Assert.Equal(12, Thin(d).Count);
        // The string dragged off by its middle (the offset kept with the label's place): 6 mm farther, the text with it; the balloon moved joins it by a leader.
        f.Run("rebar.updateRun", new JsonObject { ["id"] = run, ["label"] = new JsonObject { ["offset"] = 10, ["at"] = new JsonArray(0.5, -0.5) } });
        d = Drawing(f, view);
        var moved = Thin(d).Single(s => Math.Abs(Len(s) - 2) < 1e-6);
        Assert.Equal(line.A.Y - 6 * mm, moved.A.Y, 9);
        Assert.Equal(moved.A.Y + 1 * mm, (double)TextOf(d, "11Ø8/200")["y"]!, 9);
        Assert.Equal(13, Thin(d).Count);
        Assert.Equal(10, (double)Own(Mark(d, run))["string"]!["offset"]!);
        // Without the line (the style's), the text and balloon stand beside the bars as before; the place is forgotten with null.
        f.Run("rebar.updateRun", new JsonObject { ["id"] = run, ["label"] = new JsonObject { ["at"] = null, ["offset"] = null, ["style"] = new JsonObject { ["line"] = "none" } } });
        d = Drawing(f, view);
        Assert.Empty(Ticks(d));
        Assert.Contains("11Ø8/200", Texts(d));
        Assert.Null(Own(Mark(d, run))["string"]);
        Assert.Equal("{\"style\":{\"line\":\"none\"}}", f.Model.Get(run)!.Attrs["label"]!.ToJsonString());
        Assert.Throws<CommandException>(() => f.Run("rebar.updateRun", new JsonObject { ["id"] = run, ["label"] = new JsonObject { ["style"] = new JsonObject { ["line"] = "dots" } } }));
    }

    [Fact]
    public void A_type_s_stirrups_seen_along_are_a_string_too_and_its_offset_is_kept_on_the_view()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var type = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["enabled"] = true, ["step"] = 0.15 });
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, -2), ["end"] = new JsonArray(7, -2), ["scale"] = 50 })!["id"]!;
        var d = Drawing(f, along);
        // 41 stations at 150 mm over 6 m, the ends among them: 41 ticks; the string 4 mm under the stirrups.
        Assert.Equal(41, Ticks(d).Count);
        var bottom = Pens(d).Where(p => (string?)p!["layer"] == RebarShapes.StirrupLayer).SelectMany(p => (p!["segments"] as JsonArray)!.Chunk(4).SelectMany(s => new[] { (double)s[1]!, (double)s[3]! })).Min();
        var line = Thin(d).Single(s => Math.Abs(Len(s) - 6) < 1e-6);
        Assert.Equal(bottom - 4 * Mm(d), line.A.Y, 6);
        var label = Label(Mark(d, $"{beam}:stirrups"), "stirrups");
        Assert.Equal(4, (double)label["string"]!["offset"]!);
        Assert.Equal((0, -1), XY(label["string"]!["dir"]));
        f.Run("rebar.placeLabel", new JsonObject { ["viewId"] = along, ["owner"] = beam, ["key"] = "stirrups", ["offset"] = 12 });
        Assert.Equal("{\"offset\":12}", f.Model.Get(along)!.Attrs["rebarLabels"]![$"{beam}:stirrups"]!.ToJsonString());
        d = Drawing(f, along);
        Assert.Equal(bottom - 12 * Mm(d), Thin(d).Single(s => Math.Abs(Len(s) - 6) < 1e-6).A.Y, 6);
        Assert.Equal(12, (double)Label(Mark(d, $"{beam}:stirrups"), "stirrups")["string"]!["offset"]!);
        // The type's style without the line: the label beside the stirrups, as decision 221 drew it.
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["label"] = new JsonObject { ["line"] = "none" } });
        Assert.Empty(Ticks(Drawing(f, along)));
    }

    [Fact]
    public void The_leader_bends_at_an_elbow_made_by_dragging_its_middle_and_an_old_place_is_still_read()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var top = Beams.Top(f.Model, f.Model.Get(beam)!);
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, -2), ["end"] = new JsonArray(7, -2), ["scale"] = 50 })!["id"]!;
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(1.5, top - 0.36), ["params"] = new JsonObject { ["length"] = 5 }, ["diameter"] = 0.016, ["across"] = new JsonObject { ["count"] = 3, ["mode"] = "cover" } })!["id"]!;
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 3 })!["id"]!;
        // The balloon put away from the dots: no elbow yet, but the leader's middle is told, where a drag makes one.
        f.Run("rebar.placeLabel", new JsonObject { ["viewId"] = across, ["owner"] = bar, ["key"] = "projection", ["at"] = new JsonArray(1.2, 0.9) });
        var d = Drawing(f, across);
        var label = Label(Mark(d, $"{bar}:projection"), "projection");
        Assert.Null(label["elbow"]);
        Assert.NotNull(label["leader"]);
        var dots = Mark(d, $"{bar}:projection")["snaps"]!.AsArray().Chunk(2).Select(p => ((double)p[0]!, (double)p[1]!)).Take(3).ToList();
        Assert.Equal(3, Thin(d).Count);
        // The elbow: one line from the balloon's edge to it, then one from it to each dot.
        f.Run("rebar.placeLabel", new JsonObject { ["viewId"] = across, ["owner"] = bar, ["key"] = "projection", ["elbow"] = new JsonArray(1.0, 0.8) });
        Assert.Equal("{\"at\":[1.2,0.9],\"elbow\":[1,0.8]}", f.Model.Get(across)!.Attrs["rebarLabels"]![$"{bar}:projection"]!.ToJsonString());
        d = Drawing(f, across);
        label = Label(Mark(d, $"{bar}:projection"), "projection");
        Assert.Equal((1.0, 0.8), XY(label["elbow"]));
        Assert.Null(label["leader"]);
        var thin = Thin(d);
        Assert.Equal(4, thin.Count);
        Assert.Equal(3, thin.Count(s => Math.Abs(s.A.X - 1.0) < 1e-9 && Math.Abs(s.A.Y - 0.8) < 1e-9));
        Assert.All(dots, p => Assert.Contains(thin, s => Math.Abs(s.B.X - p.Item1) < 1e-6 && Math.Abs(s.B.Y - p.Item2) < 1e-6));
        // Taken away with null, the balloon alone stays where it was.
        f.Run("rebar.placeLabel", new JsonObject { ["viewId"] = across, ["owner"] = bar, ["key"] = "projection", ["elbow"] = null });
        Assert.Equal("{\"at\":[1.2,0.9]}", f.Model.Get(across)!.Attrs["rebarLabels"]![$"{bar}:projection"]!.ToJsonString());
        Assert.Equal(3, Thin(Drawing(f, across)).Count);
        // A place kept the old way, [x, y], is the point alone, and takes an elbow like any other.
        f.Session.Edit("old place", e => { var v = e.Model.Get(across)!; var a = v.CloneAttrs(); a["rebarLabels"] = new JsonObject { [$"{bar}:projection"] = new JsonArray(1.4, 0.7) }; e.Replace(v.With(attrs: a)); });
        Assert.Equal((1.4, 0.7), LabelAt(Mark(Drawing(f, across), $"{bar}:projection"), "projection"));
        f.Run("rebar.placeLabel", new JsonObject { ["viewId"] = across, ["owner"] = bar, ["key"] = "projection", ["elbow"] = new JsonArray(1.1, 0.75) });
        Assert.Equal("{\"at\":[1.4,0.7],\"elbow\":[1.1,0.75]}", f.Model.Get(across)!.Attrs["rebarLabels"]![$"{bar}:projection"]!.ToJsonString());
        // A bar's own label on its view takes its elbow through update, and the elbow moves with the bar.
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["label"] = new JsonObject { ["at"] = new JsonArray(3, top + 0.5), ["elbow"] = new JsonArray(3, top) } });
        d = Drawing(f, along);
        Assert.Equal((3.0, top), XY(Own(Mark(d, bar))["elbow"]));
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(bar), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        Assert.Equal((4.0, top), XY(f.Model.Get(bar)!.Attrs["label"]!["elbow"]));
    }

    [Fact]
    public void Each_dot_gets_its_leader_one_for_one_and_only_the_ends_and_the_middle_over_sixteen()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var top = Beams.Top(f.Model, f.Model.Get(beam)!);
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, -2), ["end"] = new JsonArray(7, -2), ["scale"] = 50 })!["id"]!;
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(1.5, top - 0.36), ["params"] = new JsonObject { ["length"] = 5 }, ["diameter"] = 0.016, ["across"] = new JsonObject { ["count"] = 3, ["mode"] = "cover" } })!["id"]!;
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 3 })!["id"]!;
        int Leaders() => Thin(Drawing(f, across)).Count;
        Assert.Equal(3, Leaders());
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["across"] = new JsonObject { ["count"] = 1, ["mode"] = "cover" } });
        Assert.Equal(1, Leaders());
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["across"] = new JsonObject { ["count"] = 20, ["mode"] = "step", ["step"] = 0.01 } });
        Assert.Equal(3, Leaders());
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["across"] = new JsonObject { ["count"] = 16, ["mode"] = "step", ["step"] = 0.01 } });
        Assert.Equal(16, Leaders());
        // One leader to the nearest dot, or none, by the style; an old yes/no leader still reads.
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["label"] = new JsonObject { ["style"] = new JsonObject { ["leader"] = "one" } } });
        Assert.Equal(1, Leaders());
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["label"] = new JsonObject { ["style"] = new JsonObject { ["leader"] = false } } });
        Assert.Equal("none", (string?)f.Model.Get(bar)!.Attrs["label"]!["style"]!["leader"]);
        Assert.Equal(0, Leaders());
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["label"] = new JsonObject { ["style"] = new JsonObject { ["leader"] = true } } });
        Assert.Null(f.Model.Get(bar)!.Attrs["label"]);
        Assert.Equal(16, Leaders());
        Assert.Throws<CommandException>(() => f.Run("rebar.update", new JsonObject { ["id"] = bar, ["label"] = new JsonObject { ["style"] = new JsonObject { ["leader"] = "many" } } }));
    }

    [Fact]
    public void The_line_and_leader_defaults_differ_by_kind_and_an_old_boolean_leader_is_the_kind_s_default_or_none()
    {
        Assert.Equal(("string", "one"), (LabelStyle.Default("stirrups").Line, LabelStyle.Default("stirrups").Leader));
        Assert.Equal(("string", "one"), (LabelStyle.Default("runs").Line, LabelStyle.Default("runs").Leader));
        Assert.Equal(("none", "each"), (LabelStyle.Default("bars").Line, LabelStyle.Default("bars").Leader));
        Assert.Equal(("none", "each"), (LabelStyle.Default("columnBars").Line, LabelStyle.Default("columnBars").Leader));
        Assert.Equal("none", LabelStyle.Default("bars").Apply(new JsonObject { ["leader"] = false }).Leader);
        Assert.Equal("each", LabelStyle.Default("bars").Apply(new JsonObject { ["leader"] = "none" }).Apply(new JsonObject { ["leader"] = true }).Leader);
        Assert.Equal("one", LabelStyle.Default("stirrups").Apply(new JsonObject { ["leader"] = true }).Leader);
        Assert.Equal("each", LabelStyle.Default("stirrups").Apply(new JsonObject { ["leader"] = "each" }).Leader);
        Assert.Equal("none", LabelStyle.Default("runs").Apply(new JsonObject { ["line"] = "none" }).Line);
        Assert.Equal("string", LabelStyle.Default("runs").Apply(new JsonObject { ["line"] = "dots" }).Line);
        Assert.Equal("{\"leader\":\"none\"}", LabelStyle.Patch(null, new JsonObject { ["leader"] = false })!.ToJsonString());
        Assert.Null(LabelStyle.Patch(new JsonObject { ["leader"] = "none" }, new JsonObject { ["leader"] = true }));
        Assert.Equal("{\"line\":\"string\"}", LabelStyle.Patch(null, new JsonObject { ["line"] = "string" })!.ToJsonString());
        Assert.Throws<CommandException>(() => LabelStyle.Patch(null, new JsonObject { ["line"] = "dots" }));
        var f = F();
        var settings = (JsonObject)f.Session.Query("rebar.settings", new JsonObject())!;
        Assert.Equal("string", (string?)settings["labels"]!["runs"]!["line"]);
        Assert.Equal("each", (string?)settings["labels"]!["columnBars"]!["leader"]);
    }
}
