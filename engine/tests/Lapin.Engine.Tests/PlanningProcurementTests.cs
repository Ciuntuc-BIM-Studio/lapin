using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.SiteWorks;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Procurement chains in the schedule generator (docs/plan-4d5d.md, procurement): per package a summary under
/// „Procurement” with the chain (design and shop drawings → client approval → manufacturing → transport to site, 5 working
/// days each by default), after the milestone „Drawings received”, each step FS after the one before, the transport FS
/// (with its lag) before the first task of the package's step; off, the generator makes what it made before. The task
/// attribute <c>chart</c> (false: left out of location-based charts) round trips through IFC and MS Project.
/// </summary>
public class PlanningProcurementTests
{
    static readonly string[] Names = ["Ground floor", "First floor", "Second floor"];

    /// <summary>A three-storey concrete frame: a column, a slab and a wall on each storey; the schedule starting Monday 1 March 2027.</summary>
    static Fixture Frame()
    {
        var f = new Fixture([new ArchitectureModule(), new StructureModule(), new PhasesModule(), new SiteWorksModule(), new PlanningModule()]);
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["name"] = Names[0], ["elevation"] = 0, ["height"] = 3 });
        f.Run("storey.create", new JsonObject { ["name"] = Names[1], ["elevation"] = 3, ["height"] = 3 });
        f.Run("storey.create", new JsonObject { ["name"] = Names[2], ["elevation"] = 6, ["height"] = 3 });
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        var type = f.Type("Brick 250", ("Brick", 0.25, true));
        foreach (var name in Names)
        {
            var s = Spatial.Storeys(f.Model).First(x => x.Name == name).Id;
            f.Run("structure.addColumn", new JsonObject { ["storeyId"] = s, ["x"] = 10, ["y"] = 10 });
            f.Run("architecture.addSlab", new JsonObject
            {
                ["storeyId"] = s, ["points"] = new JsonArray(new JsonArray(0, 10), new JsonArray(6, 10), new JsonArray(6, 14), new JsonArray(0, 14)),
            });
            f.Run("architecture.addWall", new JsonObject { ["storeyId"] = s, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["typeId"] = type });
        }
        return f;
    }

    static JsonObject Schedule(Fixture f) => (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!;
    static List<JsonObject> Tasks(Fixture f) => Schedule(f)["tasks"]!.AsArray().OfType<JsonObject>().ToList();
    static JsonObject Task(Fixture f, string name) => Tasks(f).First(t => (string)t["name"]! == name);
    static JsonObject Preview(Fixture f, JsonObject a) => (JsonObject)f.Session.Query("planning.preview", a)!;
    static JsonObject Generate(Fixture f, JsonObject a) => (JsonObject)f.Run("planning.generate", a)!;

    static List<string> Links(Fixture f)
    {
        var s = Schedule(f);
        var names = s["tasks"]!.AsArray().ToDictionary(t => (string)t!["id"]!, t => (string)t!["name"]!);
        return s["links"]!.AsArray().Select(l =>
        {
            var type = (string)l!["type"]!;
            var lag = (int)l["lag"]!;
            return $"{names[(string)l["from"]!]} → {names[(string)l["to"]!]}" + (type == "FS" && lag == 0 ? "" : $" {type}{lag:+0;-0}");
        }).ToList();
    }

    static JsonObject Package(string before, string? name = null, JsonObject? days = null, int lag = 0, string? key = null)
    {
        var o = new JsonObject { ["key"] = key ?? before, ["name"] = name ?? "", ["before"] = before, ["on"] = true };
        if (days != null) o["days"] = days;
        if (lag != 0) o["lag"] = lag;
        return o;
    }

    static JsonObject Proc(params JsonObject[] packages) => new() { ["on"] = true, ["packages"] = new JsonArray(packages) };

    static JsonObject With(JsonObject? procurement, string style = "concrete") =>
        procurement == null ? new JsonObject { ["style"] = style } : new JsonObject { ["style"] = style, ["spec"] = new JsonObject { ["procurement"] = procurement } };

    [Fact]
    public void A_package_is_a_chain_after_the_drawings_milestone_each_step_FS_the_transport_before_its_steps_first_task()
    {
        var f = Frame();
        Generate(f, With(Proc(Package("structure"), Package("masonry", "Facade bricks", new JsonObject { ["manufacturing"] = 15 }, lag: 2, key: "bricks"))));
        var names = Tasks(f).Select(t => (string)t["name"]!).ToList();
        // The milestone and the Procurement summary first, a summary a package holding its chain.
        Assert.Equal(["Drawings received", "Procurement", "Structure",
            "Design and shop drawings · Structure", "Client approval · Structure", "Manufacturing · Structure", "Transport to site · Structure",
            "Facade bricks", "Design and shop drawings · Facade bricks", "Client approval · Facade bricks", "Manufacturing · Facade bricks", "Transport to site · Facade bricks"],
            names.Take(12));
        Assert.Equal("Site set-up", names[12]);
        var ms = Task(f, "Drawings received");
        Assert.True((bool)ms["milestone"]!);
        Assert.Equal(0, (int)ms["duration"]!);
        // The days: Lapin's 5 a step, the package's own 15 for its manufacturing.
        Assert.Equal(5, (int)Task(f, "Design and shop drawings · Structure")["duration"]!);
        Assert.Equal(15, (int)Task(f, "Manufacturing · Facade bricks")["duration"]!);
        Assert.Equal(5, (int)Task(f, "Manufacturing · Structure")["duration"]!);
        // The transport is IFC's LOGISTIC; the others ordinary tasks; all of the procurement phase, left out of location charts.
        Assert.Equal("LOGISTIC", (string)Task(f, "Transport to site · Structure")["kind"]!);
        Assert.Null(Task(f, "Client approval · Structure")["kind"]);
        foreach (var t in Tasks(f).Take(12))
        {
            Assert.Equal("procurement", (string)t["phase"]!);
            Assert.False((bool)t["chart"]!);
        }
        Assert.Null(Task(f, "Structure · Ground floor")["chart"]);
        var links = Links(f);
        Assert.Contains("Drawings received → Design and shop drawings · Structure", links);
        Assert.Contains("Design and shop drawings · Structure → Client approval · Structure", links);
        Assert.Contains("Client approval · Structure → Manufacturing · Structure", links);
        Assert.Contains("Manufacturing · Structure → Transport to site · Structure", links);
        Assert.Contains("Transport to site · Structure → Structure · Ground floor", links);
        Assert.Contains("Drawings received → Design and shop drawings · Facade bricks", links);
        Assert.Contains("Transport to site · Facade bricks → Masonry · Ground floor FS+2", links);
        // Only the lowest storey's task waits for the delivery; the storeys above follow it.
        Assert.DoesNotContain(links, l => l.StartsWith("Transport to site · Structure → Structure · First floor", StringComparison.Ordinal));
        // The chain is written in the spec the schedule was made with, so the configurator opens on it.
        var stored = f.Model.OfClass(Plan.ScheduleClass).First().Attrs["generator"]!["procurement"]!;
        Assert.Equal(4, stored["chain"]!.AsArray().Count);
        // Dates: drawings received on the start, the structure's chain of 20 working days, then its first task.
        Assert.Equal("2027-03-01", (string)ms["start"]!);
        Assert.Equal("2027-03-29", (string)Task(f, "Structure · Ground floor")["start"]!);
    }

    [Fact]
    public void Off_the_generator_makes_what_it_made_before_for_every_template()
    {
        var f = Frame();
        foreach (var template in Generator.Templates)
        {
            // The templates carry no procurement: their specs are as before.
            Assert.Null(template.Spec.Procurement);
            Assert.False(template.ToJson()["spec"]!.AsObject().ContainsKey("procurement"));
            var before = Preview(f, With(null, template.Id));
            // Switched off (its packages kept): the same tasks, dates and durations.
            var off = Preview(f, With(new JsonObject { ["on"] = false, ["packages"] = new JsonArray(Package("site")) }, template.Id));
            Assert.Equal(Rows(before), Rows(off));
            Assert.Equal(before["summary"]!.ToJsonString(), off["summary"]!.ToJsonString());
            // On with no package: nothing either.
            Assert.Equal(Rows(before), Rows(Preview(f, With(new JsonObject { ["on"] = true }, template.Id))));
        }
        // Generated, the stored spec has no procurement.
        Generate(f, With(null));
        Assert.False(f.Model.OfClass(Plan.ScheduleClass).First().Attrs["generator"]!.AsObject().ContainsKey("procurement"));
    }

    static string Rows(JsonObject preview) => string.Join("\n", preview["tasks"]!.AsArray().Select(t =>
        $"{t!["id"]}|{t["name"]}|{t["level"]}|{t["start"]}|{t["finish"]}|{t["duration"]}|{t["critical"]}|{t["count"]}"));

    [Fact]
    public void On_the_critical_path_the_chain_moves_the_finish_by_its_length()
    {
        var f = Frame();
        var off = (int)Preview(f, With(null))["summary"]!["workingDays"]!;
        // Before the site set-up, which everything follows: 4 × 5 = 20 working days more.
        var on = Preview(f, With(Proc(Package("site"))));
        Assert.Equal(off + 20, (int)on["summary"]!["workingDays"]!);
        Assert.Equal(4, (int)on["summary"]!["procurement"]!);
        Assert.True((bool)on["summary"]!["procurementIndicative"]!);
        Assert.True(on["tasks"]!.AsArray().Where(t => (string?)t!["phase"] == "procurement" && !(bool)t["summary"]! && (string)t["name"]! != "Drawings received").All(t => (bool)t!["critical"]!));
        // The package's own manufacturing of 10 days: 25 more.
        Assert.Equal(off + 25, (int)Preview(f, With(Proc(Package("site", days: new JsonObject { ["manufacturing"] = 10 }))))["summary"]!["workingDays"]!);
        // The user's own chain (two steps, 3 and 4 days, a delivery 0): 7 more, and not indicative.
        var chain = new JsonArray(new JsonObject { ["key"] = "order", ["name"] = "Order", ["days"] = 3 }, new JsonObject { ["key"] = "make", ["name"] = "Make", ["days"] = 4 },
            new JsonObject { ["key"] = "deliver", ["name"] = "Delivery", ["days"] = 0, ["kind"] = "LOGISTIC" });
        var own = Preview(f, With(new JsonObject { ["on"] = true, ["chain"] = chain, ["packages"] = new JsonArray(Package("site")) }));
        Assert.Equal(off + 7, (int)own["summary"]!["workingDays"]!);
        Assert.False((bool)own["summary"]!["procurementIndicative"]!);
        // Off the critical path (a chain of 4 days before the masonry, which waits 15 for the site and the structure), the finish stays.
        var short4 = new JsonObject { ["design"] = 1, ["approval"] = 1, ["manufacturing"] = 1, ["transport"] = 1 };
        Assert.Equal(off, (int)Preview(f, With(Proc(Package("masonry", days: short4))))["summary"]!["workingDays"]!);
    }

    [Fact]
    public void Generated_again_an_edited_chain_task_keeps_its_days_and_the_ids_stay()
    {
        var f = Frame();
        var spec = With(Proc(Package("structure")));
        Generate(f, spec);
        var make = (string)Task(f, "Manufacturing · Structure")["id"]!;
        var ms = (string)Task(f, "Drawings received")["id"]!;
        var transport = (string)Task(f, "Transport to site · Structure")["id"]!;
        f.Run("planning.updateTask", new JsonObject { ["id"] = make, ["duration"] = 12 });
        // Shown in the location charts by hand: that stays too.
        f.Run("planning.updateTask", new JsonObject { ["id"] = transport, ["chart"] = true });
        Generate(f, spec);
        Assert.Equal(make, (string)Task(f, "Manufacturing · Structure")["id"]!);
        Assert.Equal(12, (int)Task(f, "Manufacturing · Structure")["duration"]!);
        Assert.Equal(ms, (string)Task(f, "Drawings received")["id"]!);
        Assert.Equal(transport, (string)Task(f, "Transport to site · Structure")["id"]!);
        Assert.Null(Task(f, "Transport to site · Structure")["chart"]);
        Assert.Contains("Transport to site · Structure → Structure · Ground floor", Links(f));
        // The milestone, the two summaries and the four steps: nothing made twice.
        Assert.Equal(7, f.Model.OfClass(Plan.TaskClass).Count(t => t.Str("phase") == "procurement"));
        // Switched off, the generated chain goes; the task edited by hand stays (as any edited task).
        Generate(f, With(new JsonObject { ["on"] = false }));
        Assert.DoesNotContain("Drawings received", Tasks(f).Select(t => (string)t["name"]!));
        Assert.Contains("Manufacturing · Structure", Tasks(f).Select(t => (string)t["name"]!));
    }

    [Fact]
    public void The_users_own_drawings_milestone_is_the_chains_start_and_a_package_before_a_step_with_no_task_says_so()
    {
        var f = Frame();
        var mine = (string)f.Run("planning.addTask", new JsonObject { ["name"] = "Drawings received", ["milestone"] = true })!["id"]!;
        var p = Preview(f, With(Proc(Package("structure"), Package("interior", "Kitchens", key: "kitchen"))));
        Assert.Single(p["tasks"]!.AsArray(), t => (string)t!["name"]! == "Drawings received");
        Assert.Contains(p["notes"]!.AsArray(), n => ((string)n!).Contains("Kitchens"));
        Generate(f, With(Proc(Package("structure"))));
        Assert.Contains("Drawings received → Design and shop drawings · Structure", Links(f));
        Assert.Equal(mine, (string)Task(f, "Drawings received")["id"]!);
    }

    [Fact]
    public void A_procurement_chain_is_saved_in_a_schedule_style()
    {
        var f = Frame();
        var chain = new JsonArray(new JsonObject { ["key"] = "order", ["name"] = "Order", ["days"] = 3 });
        var id = (string)f.Run("planning.saveScheduleStyle", new JsonObject
        {
            ["style"] = "steel", ["name"] = "Hall with steel package",
            ["spec"] = new JsonObject { ["procurement"] = new JsonObject { ["on"] = true, ["chain"] = chain, ["packages"] = new JsonArray(Package("steel", lag: 1)) } },
        })!["styleId"]!;
        var spec = Generator.StyleSpec(f.Model, id)!;
        Assert.True(spec.Procurement!.On);
        Assert.Equal("Order", spec.Procurement.Chain.Single().Name);
        Assert.Equal(1, spec.Procurement.Packages.Single().Lag);
        Assert.Throws<CommandException>(() => f.Run("planning.generate", new JsonObject { ["spec"] = new JsonObject { ["procurement"] = "yes" } }));
    }

    [Fact]
    public void Left_out_of_location_charts_goes_through_IFC_and_MS_Project_and_back()
    {
        var f = Frame();
        Generate(f, With(Proc(Package("structure"))));
        var site = (string)Task(f, "Site set-up")["id"]!;
        f.Run("planning.updateTask", new JsonObject { ["id"] = site, ["chart"] = false });
        Assert.False((bool)Task(f, "Site set-up")["chart"]!);
        var hidden = Tasks(f).Where(t => t["chart"] is JsonValue v && !(bool)v).Select(t => (string)t["id"]!).Order().ToList();
        Assert.Equal(8, hidden.Count);

        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "plan.ifc");
        var g = new Fixture([new PhasesModule(), new PlanningModule()]);
        IfcImport.Run(g.Session, ms.ToArray(), "plan.ifc", new IfcImportOptions(), out _);
        Assert.Equal(hidden, Tasks(g).Where(t => t["chart"] is JsonValue v && !(bool)v).Select(t => (string)t["id"]!).Order());
        Assert.Equal("procurement", (string)Task(g, "Manufacturing · Structure")["phase"]!);
        Assert.Equal("LOGISTIC", (string)Task(g, "Transport to site · Structure")["kind"]!);

        var xml = Encoding.UTF8.GetString(Convert.FromBase64String((string)f.Session.Query("planning.exportMsp", new JsonObject())!));
        Assert.Contains("<Alias>Lapin not in charts</Alias>", xml);
        var h = new Fixture([new PhasesModule(), new PlanningModule()]);
        h.Run("planning.importMsp", new JsonObject { ["xml"] = xml });
        Assert.Equal(hidden, Tasks(h).Where(t => t["chart"] is JsonValue v && !(bool)v).Select(t => (string)t["id"]!).Order());

        // Back in the charts by hand; a schedule with none hidden writes no flag field.
        var p = new Fixture([new PhasesModule(), new PlanningModule()]);
        var t1 = (string)p.Run("planning.addTask", new JsonObject { ["name"] = "A", ["duration"] = 2 })!["id"]!;
        p.Run("planning.updateTask", new JsonObject { ["id"] = t1, ["chart"] = false });
        p.Run("planning.updateTask", new JsonObject { ["id"] = t1, ["chart"] = true });
        Assert.False(p.Model.Get(t1)!.Attrs.ContainsKey("chart"));
        Assert.DoesNotContain("Flag1", Encoding.UTF8.GetString(Convert.FromBase64String((string)p.Session.Query("planning.exportMsp", new JsonObject())!)));
    }
}
