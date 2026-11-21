using System.Text.Json.Nodes;
using Lapin.Architecture;
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
/// Resources and their levelling in the open planning module (decision 772; ported with the same cases and numbers from
/// Planning Pro's LevellingTests, where they were written, decision 308), on small networks worked out by hand. The
/// schedules start on Monday 02.11.2026 (no holiday before 30.11): a working week Mon 02 – Fri 06, then Mon 09. Then the
/// formwork of Site works as a resource, a project Planning Pro saved opening with its resources, and a guest who joined
/// to look refused the levelling.
/// </summary>
public sealed class PlanningResourceTests
{
    static Fixture F() => new(new ArchitectureModule(), new PhasesModule(), new PlanningModule());

    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());
    static double N(JsonNode? n) => Json.Number(n) ?? throw new InvalidOperationException("Not a number.");

    static string Task(Fixture f, string name, int days, JsonObject? extra = null)
    {
        var id = (string)f.Run("planning.addTask", new JsonObject { ["name"] = name, ["duration"] = days })!["id"]!;
        if (extra != null) { extra["id"] = id; f.Run("planning.updateTask", extra); }
        return id;
    }

    static JsonObject Row(Fixture f, string id) => (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!["tasks"]!.AsArray().First(t => (string)t!["id"]! == id)!;
    static string Start(Fixture f, string id) => (string)Row(f, id)["start"]!;
    static string Finish(Fixture f, string id) => (string)Row(f, id)["finish"]!;
    static JsonObject First(Fixture f) => (JsonObject)f.Session.Query("planning.resources", new JsonObject())!["resources"]![0]!;

    static (Fixture F, string Res) Crew(double capacity, string kind = "labour")
    {
        var f = F();
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2026-11-02", ["name"] = "Șantier" });
        return (f, (string)f.Run("planning.addResource", new JsonObject { ["name"] = "Zidari", ["kind"] = kind, ["capacity"] = capacity })!["id"]!);
    }

    static void Put(Fixture f, string res, double units, int hold, params string[] tasks) =>
        f.Run("planning.assignResource", new JsonObject { ["resource"] = res, ["tasks"] = Ids(tasks), ["units"] = units, ["hold"] = hold });

    [Fact]
    public void Three_tasks_on_one_crew_of_one_go_one_after_the_other_the_longest_first()
    {
        var (f, crew) = Crew(1);
        var a = Task(f, "A", 3);
        var b = Task(f, "B", 2);
        var c = Task(f, "C", 2);
        Put(f, crew, 1, 0, a, b, c);
        // Unlevelled all three start on Monday: the crew is asked for 3 on 02.11 and 03.11 (two days short), 1 on 04.11.
        var res = First(f);
        Assert.Equal(3, N(res["peak"]));
        Assert.Equal(2, N(res["over"]));
        Assert.Equal([3.0, 3, 1], res["demand"]!.AsArray().Take(3).Select(N));
        var lv = (JsonObject)f.Run("planning.level", new JsonObject())!;
        // A (no float) Mon–Wed, then B (row 2) Thu–Fri, then C Mon–Tue: finished on 10.11, four working days later.
        Assert.Equal(2, N(lv["moved"]));
        Assert.Equal("2026-11-04", (string)lv["finishBefore"]!);
        Assert.Equal("2026-11-10", (string)lv["finishAfter"]!);
        Assert.Equal(4, N(lv["delay"]));
        Assert.Equal(("2026-11-02", "2026-11-04"), (Start(f, a), Finish(f, a)));
        Assert.Equal(("2026-11-05", "2026-11-06"), (Start(f, b), Finish(f, b)));
        Assert.Equal(("2026-11-09", "2026-11-10"), (Start(f, c), Finish(f, c)));
        Assert.Equal("2026-11-05", (string)Row(f, b)["levelStart"]!);
        res = First(f);
        Assert.Equal(1, N(res["peak"]));
        Assert.Equal(0, N(res["over"]));
        Assert.Equal(2, N(f.Session.Query("planning.resources", new JsonObject())!["levelled"]));
        // Undone in one step, and cleared as a command.
        f.Session.Undo();
        Assert.Equal("2026-11-02", Start(f, b));
        f.Run("planning.level", new JsonObject());
        Assert.Equal(2, N(f.Run("planning.clearLevelling", new JsonObject())!["cleared"]));
        Assert.Equal("2026-11-02", Start(f, c));
    }

    [Fact]
    public void Units_count_against_the_limit_and_the_critical_task_goes_first()
    {
        var (f, crew) = Crew(2);
        var a = Task(f, "A", 2);
        var b = Task(f, "B", 3);
        var c = Task(f, "C", 1);
        Put(f, crew, 2, 0, a);
        Put(f, crew, 1, 0, b, c);
        f.Run("planning.level", new JsonObject());
        // B is critical (no float) and takes 1 of 2 on 02–04.11; A needs both units, so it waits for B; C fits beside B.
        Assert.Equal(("2026-11-02", "2026-11-04"), (Start(f, b), Finish(f, b)));
        Assert.Equal(("2026-11-02", "2026-11-02"), (Start(f, c), Finish(f, c)));
        Assert.Equal(("2026-11-05", "2026-11-06"), (Start(f, a), Finish(f, a)));
    }

    [Fact]
    public void Links_hold_and_the_task_that_can_start_first_goes_first()
    {
        var (f, crew) = Crew(1);
        var a = Task(f, "A", 2);
        var b = Task(f, "B", 2);
        var c = Task(f, "C", 3);
        f.Run("planning.link", new JsonObject { ["from"] = a, ["to"] = b });
        Put(f, crew, 1, 0, a, b, c);
        f.Run("planning.level", new JsonObject());
        // A 02–03; C could start on Monday (B only after A), so it goes next, 04–06; B after it, 09–10.
        Assert.Equal(("2026-11-02", "2026-11-03"), (Start(f, a), Finish(f, a)));
        Assert.Equal(("2026-11-04", "2026-11-06"), (Start(f, c), Finish(f, c)));
        Assert.Equal(("2026-11-09", "2026-11-10"), (Start(f, b), Finish(f, b)));
        // A link added later still holds: the levelling's start is a floor, the links can push past it.
        f.Run("planning.updateTask", new JsonObject { ["id"] = a, ["duration"] = 6 });
        Assert.Equal("2026-11-10", Start(f, b));
    }

    [Fact]
    public void Formwork_is_held_after_its_task_until_it_is_struck()
    {
        var (f, set) = Crew(1, "formwork");
        var a = Task(f, "Placă etaj 1", 2);
        var b = Task(f, "Placă etaj 2", 1);
        Put(f, set, 1, 2, a);
        Put(f, set, 1, 0, b);
        var r = First(f);
        Assert.Equal("formwork", (string)r["kind"]!);
        Assert.Equal("sets", (string)r["unit"]!);
        f.Run("planning.level", new JsonObject());
        // The first slab's set stands 02–03 and two working days more (04–05): the second slab gets it on Friday.
        Assert.Equal("2026-11-06", Start(f, b));
    }

    [Fact]
    public void A_task_under_way_stays_and_takes_its_units_first()
    {
        var (f, crew) = Crew(1);
        var a = Task(f, "A", 2);
        var b = Task(f, "B", 4);
        Put(f, crew, 1, 0, a, b);
        f.Run("planning.updateSchedule", new JsonObject { ["dataDate"] = "2026-11-02" });
        f.Run("planning.setProgress", new JsonObject { ["id"] = a, ["complete"] = 50, ["actualStart"] = "2026-11-02" });
        f.Run("planning.level", new JsonObject());
        // A started (on 02.11, one day left from 03.11): B, though longer, waits for it.
        Assert.Equal("2026-11-02", Start(f, a));
        Assert.Equal("2026-11-04", Start(f, b));
    }

    [Fact]
    public void A_task_asking_for_more_than_there_is_is_placed_by_its_links_and_named()
    {
        var (f, crew) = Crew(2);
        var a = Task(f, "A", 2);
        Put(f, crew, 3, 0, a);
        var lv = (JsonObject)f.Run("planning.level", new JsonObject())!;
        Assert.Equal([a], lv["tooBig"]!.AsArray().Select(x => (string)x!));
        Assert.Equal(["A"], lv["tooBigNames"]!.AsArray().Select(x => (string)x!));
        Assert.Equal("2026-11-02", Start(f, a));
        Assert.Throws<CommandException>(() => F().Run("planning.level", new JsonObject()));
    }

    [Fact]
    public void A_resource_on_a_summary_goes_on_its_tasks_and_a_deleted_task_leaves_it()
    {
        var (f, crew) = Crew(3);
        var s = Task(f, "Structură", 1);
        var a = Task(f, "Stâlpi", 2);
        var b = Task(f, "Grinzi", 2);
        f.Run("planning.indent", new JsonObject { ["ids"] = Ids(a, b) });
        f.Run("planning.assignResource", new JsonObject { ["resource"] = crew, ["tasks"] = Ids(s), ["units"] = 2 });
        var res = First(f);
        Assert.Equal(["Stâlpi", "Grinzi"], res["assign"]!.AsArray().Select(x => (string)x!["name"]!));
        f.Run("planning.deleteTasks", new JsonObject { ["ids"] = Ids(b) });
        res = First(f);
        Assert.Single(res["assign"]!.AsArray());
        // Taken off with 0 units; the rate prices the days: 2 units × 2 days × 150.
        f.Run("planning.updateResource", new JsonObject { ["id"] = crew, ["rate"] = 150 });
        Assert.Equal(600, N(First(f)["cost"]));
        f.Run("planning.assignResource", new JsonObject { ["resource"] = crew, ["tasks"] = Ids(a), ["units"] = 0 });
        Assert.Empty(First(f)["assign"]!.AsArray());
        // A resource is deleted as anything else is.
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(crew) });
        Assert.Empty(f.Session.Query("planning.resources", new JsonObject())!["resources"]!.AsArray());
    }

    [Fact]
    public void The_formwork_of_site_works_becomes_a_resource_held_until_it_is_struck()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new PhasesModule(), new SiteWorksModule(), new PlanningModule());
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 4), new JsonArray(0, 4)),
        })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("planning.formworkResource", new JsonObject()));
        f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = Ids(slab) });
        var cast = Task(f, "Slab", 5, new JsonObject { ["rule"] = new JsonObject { ["classes"] = Ids("IfcSlab") } });
        var r = (JsonObject)f.Run("planning.formworkResource", new JsonObject())!;
        Assert.Equal(1, N(r["formworks"]));
        Assert.Equal(1, N(r["tasks"]));
        var res = First(f);
        Assert.Equal("formwork", (string)res["kind"]!);
        Assert.Equal(1, N(res["capacity"]));
        // Cast 01–05.03.2027, the formwork struck 21 working days after (its type's), held by the casting all that time.
        var on = (JsonObject)res["assign"]![0]!;
        Assert.Equal(cast, (string)on["task"]!);
        Assert.Equal(1, N(on["units"]));
        Assert.Equal(21, N(on["hold"]));
        // Made again: the same resource, not a second one.
        Assert.Equal((string)r["id"]!, (string)f.Run("planning.formworkResource", new JsonObject())!["id"]!);
        Assert.Single(f.Session.Query("planning.resources", new JsonObject())!["resources"]!.AsArray());
    }

    /// <summary>
    /// A project as a build before decision 772 saved it with Planning Pro: the crew an entity of Planning Pro's (its class
    /// written under model/planning-pro/ and named so in the manifest), with its attributes as Planning Pro wrote them.
    /// </summary>
    static byte[] SavedByPlanningPro(Session s)
    {
        string ModuleOf(string cls) => Resources.Classes.Contains(cls) ? "planning-pro" : s.ModuleOf(cls);
        var build = LapinPackage.Build(s.Model, ModuleOf, id => Spatial.StoreyOf(s.Model, id)?.Id, "Lapin 0.1.1557", "lapin-planning",
            [.. s.Catalog.Modules.Select(m => new PackageModule(m.Id, m.SchemaVersion)), new PackageModule("planning-pro", 1)]);
        using var ms = new MemoryStream();
        LapinPackage.Write(ms, build);
        return ms.ToArray();
    }

    static byte[] Save(Session s)
    {
        using var ms = new MemoryStream();
        ProjectPackage.Write(s, ms);
        return ms.ToArray();
    }

    [Fact]
    public void A_project_planning_pro_saved_opens_with_its_resources_and_levels()
    {
        var old = F();
        old.Run("planning.createSchedule", new JsonObject { ["start"] = "2026-11-02" });
        var a = Task(old, "A", 2);
        var b = Task(old, "B", 2);
        var crew = "crew-of-planning-pro";
        old.Session.Edit("Old crew", edit => edit.Add(new Entity(crew, "IfcLaborResource", "Zidari", new JsonObject
        {
            ["capacity"] = 1, ["unit"] = "workers", ["rate"] = 100,
            ["assign"] = new JsonObject { [a] = 1, [b] = new JsonObject { ["units"] = 1, ["hold"] = 1 } },
        })));
        var bytes = SavedByPlanningPro(old.Session);
        var content = LapinPackage.Read(new MemoryStream(bytes));
        Assert.Equal("planning-pro", content.Classes["IfcLaborResource"]);
        Assert.Contains(content.Manifest.Parts, x => x.Path.StartsWith("model/planning-pro/IfcLaborResource/", StringComparison.Ordinal));

        // Opened by the free edition: the crew is the planning module's, with its tasks, and levels.
        var f = F();
        f.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Empty(f.Session.AbsentModules());
        var res = First(f);
        Assert.Equal(crew, (string)res["id"]!);
        Assert.Equal("labour", (string)res["kind"]!);
        Assert.Equal(100, N(res["rate"]));
        Assert.Equal([(1.0, 0.0), (1.0, 1.0)], res["assign"]!.AsArray().Select(x => (N(x!["units"]), N(x["hold"]))));
        f.Run("planning.level", new JsonObject());
        Assert.Equal("2026-11-04", Start(f, b));
        // Saved again, the class is written as the planning module's.
        Assert.Equal("planning", LapinPackage.Read(new MemoryStream(Save(f.Session))).Classes["IfcLaborResource"]);
    }

    static JsonObject Call(string command) => new() { ["command"] = command, ["args"] = new JsonObject() };

    [Fact]
    public void A_guest_who_joined_to_look_does_not_level_or_change_resources_an_editor_does()
    {
        foreach (var c in new[] { "planning.level", "planning.clearLevelling", "planning.addResource", "planning.assignResource" })
        {
            Assert.NotNull(EngineDispatcher.AllowedRemote("doc.execute", Call(c), "viewer"));
            Assert.Null(EngineDispatcher.AllowedRemote("doc.execute", Call(c), "editor"));
        }
        Assert.Null(EngineDispatcher.AllowedRemote("doc.query", new JsonObject { ["name"] = "planning.resources" }, "viewer"));
    }
}
