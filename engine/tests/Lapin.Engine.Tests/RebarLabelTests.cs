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
    static (double X, double Y) LabelAt(JsonObject mark, string key)
    {
        var l = ((JsonArray)mark["data"]!["labels"]!).First(x => (string?)x!["key"] == key)!["at"]!;
        return ((double)l[0]!, (double)l[1]!);
    }

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
        Assert.Equal("[1.2,0.9]", f.Model.Get(across)!.Attrs["rebarLabels"]![$"{bar}:projection"]!.ToJsonString());
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
}
