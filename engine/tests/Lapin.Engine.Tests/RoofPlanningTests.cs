using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;
using Lapin.SiteWorks;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// A roof in planning is its build-up (decision 332, the open item of 329): its structure — the deck of a roof without
/// framing, the rafters or trusses of a framed one — is its core, its covering (with the dormers' roofs) and the battens its
/// finishes, so a task "Roof structure" (part core) and another "Roof covering" (part finishes) each take their part; a rule
/// by material finds the layer of that material; the 4D timeline gives each layer's days by the keys of the parts it is
/// drawn as (deck, covering, timber: for every rafter), the simulation and a view at a date show the covering only once it
/// is laid; the generator makes a roof in two steps, each measured by its own part.
/// </summary>
public class RoofPlanningTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new PhasesModule(), new SiteWorksModule(), new PlanningModule());

    static string AddRoof(Fixture f, string style, double x = 0)
    {
        var pts = new JsonArray(new JsonArray(x, 0.0), new JsonArray(x + 10, 0.0), new JsonArray(x + 10, 6.0), new JsonArray(x, 6.0));
        return (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts, ["style"] = style })!["id"]!;
    }

    static string Task(Fixture f, string name, int days, JsonObject? extra = null)
    {
        var id = (string)f.Run("planning.addTask", new JsonObject { ["name"] = name, ["duration"] = days })!["id"]!;
        if (extra != null) { extra["id"] = id; f.Run("planning.updateTask", extra); }
        return id;
    }

    static JsonObject Classes(params string[] c) => new() { ["classes"] = new JsonArray(c.Select(x => (JsonNode)x).ToArray()) };

    static JsonObject Timeline(Fixture f, string id) =>
        ((JsonObject)f.Session.Query("planning.timeline", new JsonObject())!)["elements"]!.AsArray().OfType<JsonObject>().First(e => (string)e["id"]! == id);

    static List<string> Days(JsonNode n) => n.AsArray().Select(x => (string)x!).ToList();

    static List<string> LayerStates(Fixture f, string date, string id) =>
        ((JsonObject)f.Session.Query("planning.stateAt", new JsonObject { ["date"] = date })!)["elements"]![id]!["layers"]!.AsArray().Select(x => (string)x!).ToList();

    /// <summary>A flat roof and a framed gable roof; the roof structure 1–5 March 2027, the covering 8–12 March.</summary>
    static (Fixture F, string Flat, string Gable, string Structure, string Covering) Sample()
    {
        var f = F();
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        var flat = AddRoof(f, "modern_minimalist");
        var gable = AddRoof(f, "traditional_campie", 20);
        var structure = Task(f, "Roof structure", 5, new JsonObject { ["rule"] = Classes(IfcClass.Roof), ["part"] = "core" });
        var covering = Task(f, "Roof covering", 5, new JsonObject { ["rule"] = Classes(IfcClass.Roof), ["part"] = "finishes" });
        f.Run("planning.link", new JsonObject { ["from"] = structure, ["to"] = covering });
        return (f, flat, gable, structure, covering);
    }

    [Fact]
    public void A_roofs_structure_is_its_core_and_its_covering_a_finish_so_two_tasks_take_them_apart()
    {
        var (f, flat, gable, structure, covering) = Sample();
        Assert.True(Roof.Resolve(f.Model.Get(gable)!, f.Model)!.Framed);
        Assert.False(Roof.Resolve(f.Model.Get(flat)!, f.Model)!.Framed);
        var links = TaskElements.Resolve(f.Session.Reader);
        // The flat roof: the covering (0) a finish, the deck (1) the core; the gable: covering, battens, rafters (the core).
        var fl = links.Facts(flat)!;
        Assert.Equal([false, true], fl.Layers.Select(l => l.Core));
        Assert.Equal(["covering", "dormer-roofs"], fl.Layers[0].PartKeys);
        Assert.Contains("deck", fl.Layers[1].PartKeys);
        var gl = links.Facts(gable)!;
        Assert.Equal([false, false, true], gl.Layers.Select(l => l.Core));
        Assert.Equal(["timber:", "truss:"], gl.Layers[2].PartKeys);
        // Each task takes both roofs, each its part of them; nothing left without a task.
        Assert.Equal(0b10UL, links.ByTask[structure][flat]);
        Assert.Equal(0b01UL, links.ByTask[covering][flat]);
        Assert.Equal(0b100UL, links.ByTask[structure][gable]);
        Assert.Equal(0b011UL, links.ByTask[covering][gable]);
        var (none, partial) = links.Unlinked();
        Assert.Empty(none);
        Assert.Empty(partial);
        // A rule by material: the deck's concrete takes the flat roof's deck only.
        var concrete = f.Model.OfClass(IfcClass.Material).First(m => m.Name == "Reinforced concrete").Id;
        var byMaterial = Task(f, "Concrete", 3, new JsonObject { ["rule"] = new JsonObject { ["materials"] = new JsonArray(concrete) }, ["part"] = "material:" + concrete });
        links = TaskElements.Resolve(f.Session.Reader);
        Assert.Equal(0b10UL, links.ByTask[byMaterial][flat]);
        Assert.False(links.ByTask[byMaterial].ContainsKey(gable));
    }

    [Fact]
    public void The_timeline_gives_each_layers_days_by_its_parts_keys_and_the_covering_shows_only_once_it_is_laid()
    {
        var (f, flat, gable, _, _) = Sample();
        var t = Timeline(f, flat);
        Assert.Equal(["2027-03-01", "2027-03-05"], Days(t["layers"]!["deck"]!));
        Assert.Equal(["2027-03-08", "2027-03-12"], Days(t["layers"]!["covering"]!));
        Assert.Equal(["2027-03-08", "2027-03-12"], Days(t["layers"]!["dormer-roofs"]!));
        Assert.Null(t["layers"]!["layer:0"]);
        var g = Timeline(f, gable);
        Assert.Equal(["2027-03-01", "2027-03-05"], Days(g["layers"]!["timber:"]!));
        Assert.Equal(["2027-03-01", "2027-03-05"], Days(g["layers"]!["truss:"]!));
        Assert.Equal(["2027-03-08", "2027-03-12"], Days(g["layers"]!["covering"]!));
        // 3 March: the structure under way, the covering not started; 10 March: the covering under way on the built structure.
        Assert.Equal([Status.Planned, Status.Active], LayerStates(f, "2027-03-03", flat));
        Assert.Equal([Status.Active, Status.Done], LayerStates(f, "2027-03-10", flat));
        Assert.Equal([Status.Planned, Status.Planned, Status.Active], LayerStates(f, "2027-03-03", gable));

        // A view at 3 March leaves out the covering of both roofs, the deck and the rafters drawn.
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(5, -2), ["end"] = new JsonArray(5, 8) })!["id"]!;
        f.Run("planning.viewDate", new JsonObject { ["id"] = view, ["date"] = "2027-03-03" });
        var filter = new StateAtDate();
        var q = f.Session.Reader;
        var v = f.Model.Get(view)!;
        Assert.True(filter.Hides(q, v, f.Model.Get(flat)!, "covering"));
        Assert.False(filter.Hides(q, v, f.Model.Get(flat)!, "deck"));
        Assert.True(filter.Hides(q, v, f.Model.Get(gable)!, "dormer-roofs"));
        Assert.False(filter.Hides(q, v, f.Model.Get(gable)!, "timber:rafter:3"));
        Assert.False(filter.Hides(q, v, f.Model.Get(gable)!, "gutter:1"));
        f.Run("planning.viewDate", new JsonObject { ["id"] = view, ["date"] = "2027-03-15" });
        Assert.False(filter.Hides(q, f.Model.Get(view)!, f.Model.Get(flat)!, "covering"));
    }

    [Fact]
    public void The_generator_makes_a_roof_in_two_steps_its_structure_then_its_covering_each_measured_by_its_part()
    {
        var f = F();
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        var flat = AddRoof(f, "modern_minimalist");
        f.Run("planning.useIndicativeNorms", new JsonObject());
        f.Run("planning.generate", new JsonObject { ["style"] = "masonry" });
        var tasks = ((JsonObject)f.Session.Query("planning.schedule", new JsonObject())!)["tasks"]!.AsArray().OfType<JsonObject>().ToList();
        var s = tasks.First(t => (string)t["name"]! == "Roof structure");
        var c = tasks.First(t => (string)t["name"]! == "Roof covering");
        Assert.Equal("core", (string)s["part"]!);
        Assert.Equal("finishes", (string)c["part"]!);
        Assert.Equal([flat], s["elements"]!.AsArray().Select(x => (string)x!));
        Assert.Equal([flat], c["elements"]!.AsArray().Select(x => (string)x!));
        // Each measured by its own part: the deck's and the covering's net areas, both the eaves' outline here.
        var sets = f.Session.Quantities(flat);
        var deck = sets.First(x => x.Part == "deck").Quantities.First(x => x.Name == "NetArea").Value;
        var cover = sets.First(x => x.Part == "covering").Quantities.First(x => x.Name == "NetArea").Value;
        Assert.Equal(deck, (double)s["quantity"]!, 3);
        Assert.Equal(cover, (double)c["quantity"]!, 3);
        // The covering after the structure.
        Assert.True(string.CompareOrdinal((string)c["start"]!, (string)s["finish"]!) > 0);
        Assert.Equal(0, (int)((JsonObject)f.Session.Query("planning.schedule", new JsonObject())!)["elements"]!["none"]!);
    }
}
