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

namespace Lapin.Engine.Tests;

/// <summary>
/// The planning module, stage 2 of docs/plan-4d5d.md: elements linked to tasks by a rule (a filter) and exceptions, so an
/// element drawn later joins its task by itself; parts of layered elements (the core, the finishes, a material) in
/// different tasks; every change one undo step; the CDE check for elements without a task; the links kept in the package
/// and carried through IFC (IfcRelAssignsToProduct, IfcRelAssignsToProcess) and MS Project XML.
/// </summary>
public class PlanningLinkTests
{
    static Fixture F() => new(new ArchitectureModule(), new PhasesModule(), new PlanningModule());

    static JsonArray Pts(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());

    static string Task(Fixture f, string name, JsonObject? extra = null)
    {
        var id = (string)f.Run("planning.addTask", new JsonObject { ["name"] = name, ["duration"] = 5 })!["id"]!;
        if (extra != null) { extra["id"] = id; f.Run("planning.updateTask", extra); }
        return id;
    }

    static JsonObject Schedule(Fixture f) => (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!;

    static JsonObject Row(JsonObject s, string id) => (JsonObject)s["tasks"]!.AsArray().First(t => (string)t!["id"]! == id)!;

    /// <summary>A task's elements, sorted.</summary>
    static List<string> Elements(Fixture f, string task) =>
        (Row(Schedule(f), task)["elements"] as JsonArray)?.Select(x => (string)x!).Order(StringComparer.Ordinal).ToList() ?? [];

    static JsonObject Rule(params (string Key, string[] Values)[] conditions)
    {
        var o = new JsonObject();
        foreach (var (k, v) in conditions) o[k] = new JsonArray(v.Select(x => (JsonNode)x).ToArray());
        return o;
    }

    static string Slab(Fixture f, double x) =>
        (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((x, 0), (x + 4, 0), (x + 4, 4), (x, 4)) })!["id"]!;

    static string Assign(Fixture f, string task, bool remove, params string[] ids) =>
        f.Run("planning.assign", new JsonObject { ["task"] = task, ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()), ["remove"] = remove })!.ToJsonString();

    static List<JsonObject> Findings(Fixture f) =>
        ((JsonArray)((JsonObject)f.Session.Query("cde.check", new JsonObject())!)["findings"]!).OfType<JsonObject>().Where(x => (string?)x["rule"] == "planningTasks").ToList();

    [Fact]
    public void A_rule_takes_the_elements_it_finds_and_a_wall_drawn_later_joins_its_task_by_itself()
    {
        var f = F();
        var a = f.Wall(0, 0, 6, 0);
        var b = f.Wall(6, 0, 6, 5);
        var slab = Slab(f, 0);
        var masonry = Task(f, "Masonry", new JsonObject { ["rule"] = Rule(("classes", ["IfcWall"]), ("storeys", [f.Storey])) });
        var floors = Task(f, "Floor slabs", new JsonObject { ["rule"] = Rule(("classes", ["IfcSlab"])) });
        Assert.Equal(new[] { a, b }.Order(StringComparer.Ordinal), Elements(f, masonry));
        Assert.Equal([slab], Elements(f, floors));
        // Drawn after the rule was set: it is in the task without anyone putting it there; undone, it is gone from it.
        var c = f.Wall(6, 5, 0, 5);
        Assert.Contains(c, Elements(f, masonry));
        Assert.Equal(3, (int)Row(Schedule(f), masonry)["count"]!);
        f.Session.Undo();
        Assert.DoesNotContain(c, Elements(f, masonry));
        // A storey the wall is not on: nothing.
        f.Run("planning.updateTask", new JsonObject { ["id"] = masonry, ["rule"] = Rule(("classes", ["IfcWall"]), ("storeys", ["no-such-storey"])) });
        Assert.Empty(Elements(f, masonry));
        f.Session.Undo();
        Assert.Equal(2, Elements(f, masonry).Count);
        // A summary shows the elements of the tasks under it; the rule is kept as given.
        var structure = Task(f, "Structure");
        f.Run("planning.moveTask", new JsonObject { ["id"] = structure, ["offset"] = -5 });
        f.Run("planning.indent", new JsonObject { ["ids"] = new JsonArray(masonry, floors) });
        var s = Schedule(f);
        Assert.Equal(3, (int)Row(s, structure)["count"]!);
        Assert.Equal("IfcWall", (string)Row(s, masonry)["rule"]!["classes"]![0]!);
        // The rule's options: the model's classes and storeys, counted.
        var options = (JsonObject)f.Session.Query("planning.ruleOptions", new JsonObject())!;
        Assert.Equal(2, (int)options["classes"]!.AsArray().First(c => (string)c!["id"]! == "IfcWall")!["count"]!);
        Assert.Contains(options["storeys"]!.AsArray(), x => (string)x!["id"]! == f.Storey);
    }

    [Fact]
    public void Elements_added_and_taken_out_by_hand_are_exceptions_to_the_rule_each_one_undo_step()
    {
        var f = F();
        var a = f.Wall(0, 0, 6, 0);
        var b = f.Wall(6, 0, 6, 5);
        var slab = Slab(f, 10);
        var task = Task(f, "Ground floor", new JsonObject { ["rule"] = Rule(("classes", ["IfcWall"])) });
        // The slab is not the rule's: it is added. A wall the rule takes is taken out.
        Assign(f, task, false, slab);
        Assign(f, task, true, b);
        Assert.Equal(new[] { a, slab }.Order(StringComparer.Ordinal), Elements(f, task));
        var e = f.Model.Get(task)!;
        Assert.Equal([slab], e.Attrs["add"]!.AsArray().Select(x => (string)x!));
        Assert.Equal([b], e.Attrs["remove"]!.AsArray().Select(x => (string)x!));
        var row = Row(Schedule(f), task);
        Assert.Equal(1, (int)row["added"]!);
        Assert.Equal(1, (int)row["removed"]!);
        // Put back: the exception goes, the rule takes it again; taking the slab out drops it from the added.
        Assign(f, task, false, b);
        Assign(f, task, true, slab);
        Assert.Equal(new[] { a, b }.Order(StringComparer.Ordinal), Elements(f, task));
        Assert.Null(f.Model.Get(task)!.Attrs["add"]);
        Assert.Null(f.Model.Get(task)!.Attrs["remove"]);
        // Each one step: undone twice, the slab is in and the wall b out again.
        f.Session.Undo();
        f.Session.Undo();
        Assert.Equal(new[] { a, slab }.Order(StringComparer.Ordinal), Elements(f, task));
        // A task with no rule takes only what is put in it; a rule made from the selection takes their kinds and storeys.
        var loose = Task(f, "Loose");
        Assign(f, loose, false, a);
        Assert.Equal([a], Elements(f, loose));
        f.Run("planning.ruleFromSelection", new JsonObject { ["id"] = loose, ["ids"] = new JsonArray(a) });
        Assert.Equal(new[] { a, b }.Order(StringComparer.Ordinal), Elements(f, loose));
        Assert.Null(f.Model.Get(loose)!.Attrs["add"]);
        Assert.Throws<CommandException>(() => Assign(f, loose, false, task));
    }

    [Fact]
    public void A_layered_wall_is_built_in_one_task_and_plastered_in_another_and_the_check_says_what_is_left()
    {
        var f = F();
        var type = f.Type("Plastered brick", ("Lime plaster", 0.015, false), ("Brick", 0.25, true), ("Gypsum plaster", 0.015, false));
        var w1 = f.Wall(0, 0, 6, 0, type);
        var w2 = f.Wall(6, 0, 6, 5, type);
        var slab = Slab(f, 10);
        // No task takes elements yet: the check says nothing (a schedule without links is a schedule as in stage 1).
        Task(f, "Site set-up");
        Assert.Empty(Findings(f));
        var masonry = Task(f, "Masonry", new JsonObject { ["rule"] = Rule(("classes", ["IfcWall"])), ["part"] = "core" });
        Assert.Equal(new[] { w1, w2 }.Order(StringComparer.Ordinal), Elements(f, masonry));
        var s = Schedule(f);
        Assert.Equal(1, (int)s["elements"]!["none"]!);       // the slab
        Assert.Equal(2, (int)s["elements"]!["partial"]!);    // both walls, unplastered
        var found = Findings(f);
        Assert.Contains(found, x => (string?)x["entityId"] == slab && ((string)x["message"]!).Contains("has no task"));
        Assert.Contains(found, x => (string?)x["entityId"] == w1 && ((string)x["message"]!).Contains("Lime plaster, Gypsum plaster have no task"));
        Assert.All(found, x => Assert.Equal("warn", (string)x["severity"]!));
        // The plaster in a task of its own, the slab in another: nothing left.
        var plaster = Task(f, "Plaster", new JsonObject { ["rule"] = Rule(("classes", ["IfcWall"])), ["part"] = "finishes" });
        Task(f, "Slab", new JsonObject { ["add"] = new JsonArray(slab) });
        Assert.Equal(new[] { w1, w2 }.Order(StringComparer.Ordinal), Elements(f, plaster));
        Assert.Empty(Findings(f));
        Assert.Equal(0, (int)Schedule(f)["elements"]!["partial"]!);
        // One material only: the walls with gypsum; a door has no layers, so no finishes: it does not join a finishes task.
        var gypsum = Platform.Materials.Find(f.Model, "Gypsum plaster")!.Id;
        f.Run("planning.updateTask", new JsonObject { ["id"] = plaster, ["part"] = "material:" + gypsum });
        Assert.Equal(2, Elements(f, plaster).Count);
        Assert.Single(Findings(f).Where(x => ((string)x["message"]!).Contains("Lime plaster has no task")), x => (string?)x["entityId"] == w1);
        f.Run("planning.updateTask", new JsonObject { ["id"] = plaster, ["part"] = "finishes" });
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = w1, ["x"] = 2, ["y"] = 0 })!["id"]!;
        var r = JsonNode.Parse(Assign(f, plaster, false, door))!;
        Assert.Equal(1, (int)r["noPart"]!);
        Assert.DoesNotContain(door, Elements(f, plaster));
        // Its storey is its wall's: a rule by storey takes it.
        var byStorey = Task(f, "Doors", new JsonObject { ["rule"] = Rule(("classes", ["IfcDoor"]), ("storeys", [f.Storey])) });
        Assert.Equal([door], Elements(f, byStorey));
        // The category of material: plaster is a finish by its name.
        f.Run("planning.updateTask", new JsonObject { ["id"] = plaster, ["part"] = "category:Finish" });
        Assert.Equal(2, Elements(f, plaster).Count);
        Assert.Throws<CommandException>(() => f.Run("planning.updateTask", new JsonObject { ["id"] = plaster, ["part"] = "the nice bits" }));
    }

    /// <summary>A small schedule taking elements every way: a rule with a part, a task of exceptions only, a demolition.</summary>
    static (Fixture F, Dictionary<string, string> Ids) Sample()
    {
        var f = F();
        f.Run("planning.updateSchedule", new JsonObject { ["start"] = "2026-11-16" });
        var type = f.Type("Plastered brick", ("Lime plaster", 0.015, false), ("Brick", 0.25, true), ("Gypsum plaster", 0.015, false));
        var ids = new Dictionary<string, string>
        {
            ["w1"] = f.Wall(0, 0, 6, 0, type),
            ["w2"] = f.Wall(6, 0, 6, 5, type),
            ["w3"] = f.Wall(6, 5, 0, 5, type),
            ["slab"] = Slab(f, 0),
            ["old"] = Slab(f, 10),
        };
        ids["masonry"] = Task(f, "Masonry", new JsonObject { ["rule"] = Rule(("classes", ["IfcWall"]), ("storeys", [f.Storey])), ["part"] = "core" });
        ids["plaster"] = Task(f, "Plaster", new JsonObject { ["rule"] = Rule(("classes", ["IfcWall"])), ["part"] = "finishes" });
        Assign(f, ids["masonry"], true, ids["w3"]);
        ids["slab-task"] = Task(f, "Slab");
        Assign(f, ids["slab-task"], false, ids["slab"]);
        ids["demolish"] = Task(f, "Demolish the old slab", new JsonObject { ["kind"] = "DEMOLITION" });
        Assign(f, ids["demolish"], false, ids["old"]);
        return (f, ids);
    }

    /// <summary>What a link survives with: each task's elements, rule, part and exceptions.</summary>
    static string Essence(JsonObject s) => string.Join("\n", s["tasks"]!.AsArray().Select(t =>
        $"{t!["id"]}|{t["name"]}|{t["count"]}|{(t["elements"] as JsonArray)?.Select(x => (string)x!).Order(StringComparer.Ordinal).Aggregate("", (x, y) => x + "," + y)}|{t["rule"]?.ToJsonString()}|{t["part"]}|{t["added"]}|{t["removed"]}"))
        + $"\n{s["elements"]!.ToJsonString()}";

    [Fact]
    public void The_links_are_kept_in_the_package_and_an_old_schedule_opens_with_none()
    {
        var (f, _) = Sample();
        var before = Essence(Schedule(f));
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        ms.Position = 0;
        var content = LapinPackage.Read(ms);
        var g = F();
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Equal(before, Essence(Schedule(g)));
        // A task of stage 1 (no rule, no exceptions) reads as one taking nothing.
        var h = F();
        var t = Task(h, "Old task");
        Assert.Equal(0, (int)Row(Schedule(h), t)["count"]!);
        Assert.Null(Row(Schedule(h), t)["elements"]);
    }

    [Fact]
    public void The_links_go_to_IFC_as_assignments_and_come_back_with_their_rules()
    {
        var (f, ids) = Sample();
        var expected = Schedule(f);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "links.ifc");
        var bytes = ms.ToArray();
        var step = StepFile.Read(bytes);
        var task = step.OfType("IFCTASK").ToDictionary(t => (string)t[0]!, t => t.Id);
        string Gid(object? r) => (string)step.Get(r)![0]!;
        // What a task builds: an IfcRelAssignsToProduct an element, the tasks building it its related objects (a wall: masonry and plaster).
        var products = step.OfType("IFCRELASSIGNSTOPRODUCT").ToList();
        Assert.Equal(4, products.Count);   // w1, w2, w3, the slab
        var w1 = products.Single(r => Gid(r[6]) == ids["w1"]);
        Assert.Equal(new[] { task[ids["masonry"]], task[ids["plaster"]] }.Order(), ((List<object?>)w1[4]!).Select(x => ((StepRef)x!).Id).Order());
        var w3 = products.Single(r => Gid(r[6]) == ids["w3"]);
        Assert.Equal([task[ids["plaster"]]], ((List<object?>)w3[4]!).Select(x => ((StepRef)x!).Id));
        // What it acts on: the demolition's slab, an IfcRelAssignsToProcess of the task.
        var process = Assert.Single(step.OfType("IFCRELASSIGNSTOPROCESS"));
        Assert.Equal(task[ids["demolish"]], ((StepRef)process[6]!).Id);
        Assert.Equal([ids["old"]], ((List<object?>)process[4]!).Select(Gid));
        // Read into a new project: the same elements in the same tasks, the rules, parts and exceptions as they were.
        var g = F();
        IfcImport.Run(g.Session, bytes, "links.ifc", new IfcImportOptions(), out _);
        // The rule names the storey the new project matched the file's to (its own ground floor, by name).
        var storey = Spatial.StoreyOf(g.Model, ids["w1"])!.Id;
        Assert.Equal(storey, (string)Row(Schedule(g), ids["masonry"])["rule"]!["storeys"]![0]!);
        Assert.Equal(Essence(expected).Replace(f.Storey, storey), Essence(Schedule(g)));
        // A wall drawn in the new project joins its tasks by the rule read back.
        var w4 = g.Wall(0, 5, 0, 0, Walls.TypeOf(g.Model, ids["w1"])!.Id);
        Assert.Contains(w4, Elements(g, ids["masonry"]));
    }

    [Fact]
    public void A_file_that_only_assigns_elements_gives_tasks_holding_those_it_has()
    {
        var f = F();
        var a = f.Wall(0, 0, 6, 0);
        var b = f.Wall(6, 0, 6, 5);
        var schedule = new IfcScheduleData(GlobalId.New(), "Other program", new DateTime(2026, 11, 16, 8, 0, 0), null, "PLANNED", null,
        [
            new IfcTaskData(GlobalId.New(), "Walls", null, "1", null, false, null, null, "Lapin_Task", [], Outputs: [a, b, GlobalId.New()]),
            new IfcTaskData(GlobalId.New(), "Take down", null, "2", null, false, "DEMOLITION", null, "Lapin_Task", [], Inputs: [b]),
        ], []);
        IfcProcessImport result = null!;
        f.Session.Edit("Import", edit => result = new ScheduleIfc().Import(edit, [schedule]));
        Assert.Contains(result.Notes, n => n.Contains("1 element(s)"));
        var s = Schedule(f);
        Assert.Equal(new[] { a, b }.Order(StringComparer.Ordinal), Elements(f, (string)s["tasks"]![0]!["id"]!));
        Assert.Equal([b], Elements(f, (string)s["tasks"]![1]!["id"]!));
        Assert.Equal(2, (int)s["tasks"]![0]!["added"]!);
    }

    [Fact]
    public void The_links_go_to_MS_Project_XML_in_a_named_field_and_come_back()
    {
        var (f, ids) = Sample();
        var expected = Schedule(f);
        var xml = Encoding.UTF8.GetString(Convert.FromBase64String((string)f.Session.Query("planning.exportMsp", new JsonObject())!));
        Assert.Contains("<Alias>Lapin elements</Alias>", xml);
        Assert.Contains("188743731", xml);
        // Read into the same model (the elements are there): every task's elements again.
        var g = F();
        using (var ms = new MemoryStream())
        {
            ProjectPackage.Write(f.Session, ms);
            ms.Position = 0;
            var content = LapinPackage.Read(ms);
            g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        }
        g.Run("planning.deleteTasks", new JsonObject { ["ids"] = new JsonArray(ids.Where(kv => g.Model.Get(kv.Value) is { Class: "IfcTask" }).Select(kv => (JsonNode)kv.Value).ToArray()) });
        Assert.All(Schedule(g)["tasks"]!.AsArray(), t => Assert.Equal(0, (int)t!["count"]!));
        g.Run("planning.importMsp", new JsonObject { ["xml"] = xml });
        Assert.Equal(Essence(expected), Essence(Schedule(g)));
        // The workbook says what each task takes.
        var table = PlanningModule.Table(Plan.Read(f.Model), Cpm.Solve(Plan.Read(f.Model)), f.Session.Reader);
        Assert.Equal("Rule", table[0][^1]);
        Assert.Equal("Wall · Ground floor · core · −1", table.First(r => r[2] == "Masonry")[^1]);
    }
}
