using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Phases, ported from the old Lapin's phase-model.test.ts (same cases), then on a model: views by phase, undo.</summary>
public class PhaseTests
{
    static readonly PhaseSettings S = PhaseSettings.Default with
    {
        Phases = [new("ex", "Existing", "existing"), new("demo", "Demolition", "construction"), new("struct", "Structure", "execution"), new("finish", "Finishes", "execution")],
    };

    [Fact]
    public void An_element_s_state_from_the_view_s_phase()
    {
        Assert.Equal("existing", S.State("ex", null, "struct"));
        Assert.Equal("new", S.State("struct", null, "struct"));
        Assert.Null(S.State("finish", null, "struct"));          // not built yet
        Assert.Equal("demolished", S.State("ex", "demo", "demo"));
        Assert.Null(S.State("ex", "demo", "struct"));            // gone
        Assert.Equal("temporary", S.State("struct", "struct", "struct"));   // formwork
        Assert.Null(S.State("struct", "struct", "finish"));
        Assert.Equal("new", S.State(null, null, "demo"));        // no phase: the first construction phase
        Assert.Null(S.State(null, null, "ex"));
    }

    [Fact]
    public void A_filter_per_state_and_no_filter_shows_everything()
    {
        var d = PhaseSettings.Default;
        Assert.Equal("hide", d.Presentation("show-new", "existing"));
        Assert.Equal("override", d.Presentation("show-all", "demolished"));
        Assert.Equal("show", d.Presentation(null, "demolished"));
        Assert.Equal("new-construction", d.ActivePhase?.Id);
    }

    [Fact]
    public void Settings_are_read_dropping_what_is_malformed()
    {
        var read = PhaseSettings.Read(JsonNode.Parse("""
            { "phases": [{ "id": "a", "name": "A", "kind": "execution" }, { "name": "no id" }, { "id": "b", "name": "B", "kind": "odd" }],
              "filters": [{ "id": "f", "name": "F", "states": { "new": "hide", "existing": "nonsense" } }],
              "overrides": { "demolished": { "color": "#112233", "opacity": 2 } },
              "active": "b", "activeFilter": "missing" }
            """))!;
        Assert.Equal([new ProjectPhase("a", "A", "execution"), new ProjectPhase("b", "B", "construction")], read.Phases);
        Assert.Equal(new Dictionary<string, string> { ["new"] = "hide", ["existing"] = "show", ["demolished"] = "show", ["temporary"] = "show" }, read.Filters[0].States);
        Assert.Equal(("#112233", 0.45), (read.Overrides["demolished"].Color, read.Overrides["demolished"].Opacity));
        Assert.Equal("b", read.Active);
        Assert.Null(read.ActiveFilter);
        Assert.Null(PhaseSettings.Read(JsonValue.Create("nope")));
    }

    static Fixture F() => new(new ArchitectureModule(), new PhasesModule());
    static int Cut(Fixture f, string view) =>
        ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!)["pens"]!.AsArray().OfType<JsonObject>()
            .Where(p => (string)p["layer"]! == "CUT").Sum(p => p["segments"]!.AsArray().Count / 4);

    [Fact]
    public void A_view_shows_what_its_phase_sees_and_its_filter_lets_through()
    {
        var f = F();
        var old = f.Wall(0, 0, 6, 0);
        var added = f.Wall(0, 4, 6, 4);
        f.Run("phases.assign", new JsonObject { ["id"] = old, ["created"] = "existing" });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var both = Cut(f, plan);
        // The existing phase: the new wall is not built yet.
        f.Run("phases.viewPhase", new JsonObject { ["id"] = plan, ["phase"] = "existing" });
        Assert.True(Cut(f, plan) < both);
        // New construction, "show new": the existing wall goes.
        f.Run("phases.viewPhase", new JsonObject { ["id"] = plan, ["phase"] = "new-construction", ["filter"] = "show-new" });
        Assert.True(Cut(f, plan) < both);
        // "Show all": the existing one drawn in its override's grey.
        f.Run("phases.viewPhase", new JsonObject { ["id"] = plan, ["filter"] = "show-all" });
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        Assert.Contains(d["pens"]!.AsArray().OfType<JsonObject>(), p => (string?)p["stroke"] == "#a6a6a6");
        // Demolished before it is built: refused.
        f.Run("phases.addPhase", new JsonObject { ["name"] = "Demolition", ["after"] = "existing" });
        Assert.Throws<CommandException>(() => f.Run("phases.assign", new JsonObject { ["id"] = added, ["demolished"] = "existing" }));
        f.Run("phases.assign", new JsonObject { ["id"] = old, ["demolished"] = "demolition" });
        var look = (JsonObject)f.Session.Query("phases.look", new JsonObject { ["phase"] = "demolition", ["filter"] = "show-all" })!;
        Assert.Equal("demolished", (string)look[old]!["state"]!);
        // Removing the phase: the wall is no longer demolished, and undo brings both back.
        f.Run("phases.removePhase", new JsonObject { ["id"] = "demolition" });
        Assert.Null(f.Model.Get(old)!.Str("phaseDemolished"));
        f.Session.Undo();
        Assert.Equal("demolition", f.Model.Get(old)!.Str("phaseDemolished"));
    }
}
