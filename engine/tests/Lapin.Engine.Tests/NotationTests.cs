using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;
using Xunit;

namespace Lapin.Engine.Tests;

/// <summary>
/// The project's notations (decision 861): the symbols, prefixes and suffixes a drawing writes are local standards, so each
/// domain's texts are templates with <c>{…}</c> tokens, Lapin's coded ones the default, country presets (indicative) and the
/// user's own over them, per project. Rebar is the first domain: the diameter's symbol, the count, step, length and steel as
/// labels write them, and the bar bending schedule's headings.
/// </summary>
public class NotationTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static readonly NotationDomain Demo = new("demo", "Demo",
        [new NotationKey("symbol", "Symbol", "Ø", [], NotationKey.Symbol), new NotationKey("size", "Size", "{symbol}{d}", ["symbol", "d"])],
        [new NotationPreset("x", "X", "Somewhere", new Dictionary<string, string> { ["symbol"] = "H" })]);

    [Fact]
    public void A_template_fills_its_tokens_and_formulas_and_keeps_what_it_does_not_know()
    {
        var n = Notation.Default(Demo);
        Assert.Equal("Ø12", n.Format("size", k => k == "d" ? "12" : null));
        Assert.Equal("H12", Notation.Fill("H{d}", k => k == "d" ? "12" : null));
        Assert.Equal("#4", Notation.Fill("#{= round(d / 3.175; 0)}", k => k == "d" ? "12.7" : null));
        Assert.Equal("{x}12", Notation.Fill("{x}{d}", k => k == "d" ? "12" : null));
        // A key it does not have: its name, never a throw.
        Assert.Equal("", n.Format("nothing", _ => null));
        Assert.Equal("Ø", n["symbol"]);
    }

    [Fact]
    public void A_project_without_notations_writes_today_s_texts()
    {
        var f = F();
        var n = RebarSettings.Of(f.Model).Notation;
        Assert.Equal("Ø", n["symbol"]);
        Assert.True(n.IsDefault);
        Assert.Equal("Ø12", RebarShapes.Dia(0.012, n));
        Assert.Equal("3Ø16 L=6000", LabelStyle.Default("bars").Text(new LabelFacts("3", 0.016, null, 6.0, "1", "B500C"), n));
        Assert.Equal("41Ø8/150", LabelStyle.Default("stirrups").Text(new LabelFacts("41", 0.008, "150", 2.16, "1", "B500C"), n));
        Assert.Equal(RebarSchedule.Columns, RebarSchedule.ColumnsOf(n));
        // Nothing is stored until the user changes it.
        Assert.Null(Spatial.Project(f.Model)!.Attrs[Notations.Key]);
    }

    [Fact]
    public void The_domains_their_keys_defaults_and_presets_are_described()
    {
        var f = F();
        var all = (JsonArray)f.Session.Query("notation.describe", new JsonObject())!;
        var rebar = all.OfType<JsonObject>().Single(d => (string?)d["id"] == "rebar");
        var keys = ((JsonArray)rebar["keys"]!).OfType<JsonObject>().ToList();
        Assert.Equal("Ø", (string?)keys.Single(k => (string?)k["key"] == "symbol")["value"]);
        Assert.Equal("symbol", (string?)keys.Single(k => (string?)k["key"] == "symbol")["kind"]);
        Assert.Equal("{symbol}{d}", (string?)keys.Single(k => (string?)k["key"] == "diameter")["default"]);
        Assert.Contains("d", ((JsonArray)keys.Single(k => (string?)k["key"] == "diameter")["tokens"]!).Select(x => (string?)x));
        var presets = ((JsonArray)rebar["presets"]!).OfType<JsonObject>().ToList();
        Assert.Contains(presets, p => (string?)p["id"] == "uk");
        Assert.Contains(presets, p => (string?)p["id"] == "us");
        Assert.All(presets, p => Assert.True((bool)p["indicative"]!));
        Assert.Null((string?)rebar["preset"]);
    }

    [Fact]
    public void A_preset_changes_the_labels_and_the_schedule_and_the_user_s_own_values_go_over_it()
    {
        var f = F();
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(8, 0) })!["id"]!;
        f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shape"] = "straight", ["at"] = new JsonArray(1, 1), ["params"] = new JsonObject { ["length"] = 2.5 }, ["count"] = 4 });
        f.Run("rebar.addRun", new JsonObject { ["viewId"] = view, ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(1, 0), ["step"] = 0.1, ["params"] = new JsonObject { ["a"] = 0.4, ["b"] = 0.6 } });
        Assert.Contains("4Ø12 L=2500", RebarLabelTexts(f, view));
        Assert.Contains("11Ø8/100", RebarLabelTexts(f, view));

        // The UK's (BS 8666, indicative): H for high-yield bars, the spacing after a dash.
        f.Run("notation.set", new JsonObject { ["domain"] = "rebar", ["preset"] = "uk" });
        Assert.Contains("4H12 L=2500", RebarLabelTexts(f, view));
        Assert.Contains("11H8-100", RebarLabelTexts(f, view));
        var grid = Grid(f);
        Assert.Contains("Bar mark", grid);
        Assert.Contains("Size (mm)", grid);

        // The user's own over the preset: φ for the symbol, the step c/c.
        f.Run("notation.set", new JsonObject { ["domain"] = "rebar", ["values"] = new JsonObject { ["symbol"] = "φ", ["step"] = " c/c {step}", ["heading.mass"] = "Weight (kg)" } });
        Assert.Contains("4φ12 L=2500", RebarLabelTexts(f, view));
        Assert.Contains("11φ8 c/c 100", RebarLabelTexts(f, view));
        Assert.Contains("Weight (kg)", Grid(f));
        var described = ((JsonArray)f.Session.Query("notation.describe", new JsonObject { ["domain"] = "rebar" })!).OfType<JsonObject>().Single();
        Assert.Equal("uk", (string?)described["preset"]);
        Assert.True((bool)((JsonArray)described["keys"]!).OfType<JsonObject>().Single(k => (string?)k["key"] == "symbol")["own"]!);

        // A value set to null goes back to the preset's, then everything back to Lapin's.
        f.Run("notation.set", new JsonObject { ["domain"] = "rebar", ["values"] = new JsonObject { ["symbol"] = null } });
        Assert.Contains("4H12 L=2500", RebarLabelTexts(f, view));
        f.Run("notation.set", new JsonObject { ["domain"] = "rebar", ["reset"] = true });
        Assert.Contains("4Ø12 L=2500", RebarLabelTexts(f, view));
        Assert.Contains("11Ø8/100", RebarLabelTexts(f, view));
        Assert.True(RebarSettings.Of(f.Model).Notation.IsDefault);
    }

    [Fact]
    public void A_notation_is_one_undo_step_and_what_cannot_be_written_is_refused()
    {
        var f = F();
        f.Run("notation.set", new JsonObject { ["domain"] = "rebar", ["values"] = new JsonObject { ["symbol"] = "⌀" } });
        Assert.Equal("⌀12", RebarShapes.Dia(0.012, RebarSettings.Of(f.Model).Notation));
        f.Session.Undo();
        Assert.Equal("Ø12", RebarShapes.Dia(0.012, RebarSettings.Of(f.Model).Notation));
        Assert.Throws<CommandException>(() => f.Run("notation.set", new JsonObject { ["domain"] = "nowhere", ["preset"] = "uk" }));
        Assert.Throws<CommandException>(() => f.Run("notation.set", new JsonObject { ["domain"] = "rebar", ["preset"] = "mars" }));
        Assert.Throws<CommandException>(() => f.Run("notation.set", new JsonObject { ["domain"] = "rebar", ["values"] = new JsonObject { ["colour"] = "red" } }));
        // A token the key does not know.
        Assert.Throws<CommandException>(() => f.Run("notation.set", new JsonObject { ["domain"] = "rebar", ["values"] = new JsonObject { ["diameter"] = "{symbol}{weight}" } }));
        Assert.Throws<CommandException>(() => f.Run("notation.set", new JsonObject { ["domain"] = "rebar", ["values"] = new JsonObject { ["symbol"] = new string('x', 20) } }));
    }

    [Fact]
    public void A_type_s_stirrups_and_a_set_s_tag_take_the_notation_too()
    {
        // Each label of the module goes through LabelStyle.Text with the project's notation; a custom one changes them all.
        var n = Notation.Of(new Dictionary<string, string> { ["symbol"] = "T", ["step"] = "-{step}" }, RebarNotation.Domain);
        Assert.Equal("41T8-150", LabelStyle.Default("stirrups").Text(new LabelFacts("41", 0.008, "150", 2.16, "1", "B500C"), n));
        Assert.Equal("12T12-150 B1", LabelStyle.Default("sets").Text(new LabelFacts("12", 0.012, "150", null, "1", "B500C", "B1"), n));
        Assert.Equal("8T16", LabelStyle.Default("columnBars").Text(new LabelFacts("8", 0.016, null, 4.0, "1", "B500C"), n));
    }

    static List<string?> RebarLabelTexts(Fixture f, string view)
    {
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
        return ((JsonArray)d["pens"]!).SelectMany(p => (p!["texts"] as JsonArray)?.Select(t => (string?)t!["text"]) ?? []).ToList();
    }

    static string Grid(Fixture f)
    {
        var schedule = f.Model.OfClass("LapinSchedule").FirstOrDefault()?.Id ?? (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "rebar" })!["id"]!;
        return f.Session.Query("schedule.layout", new JsonObject { ["id"] = schedule })?.ToJsonString(new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) ?? "";
    }
}
