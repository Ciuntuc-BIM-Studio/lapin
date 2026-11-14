using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The Gantt configured by the office's own naming (docs/plan-user-extensibility.md §12, the user's request „configuring the
/// Gantt charts should let users configure the parameters when they have other internal standards of property naming”): a
/// task's rule with conditions on any field (the user's parameters, the property sets a file gave its references, the
/// office's aliases), carried through IFC and MS Project; the generator's steps taking elements by conditions, split into a
/// task a value of a field with stable ids, timed by a field (days, or a quantity under a norm); a task timed by a field and
/// retimed; the Gantt coloured and given columns by a field in the window's data and on the sheet; the tasks' values query.
/// </summary>
public class PlanningFieldTests
{
    static Fixture F() => new(new ArchitectureModule(), new PhasesModule(), new PlanningModule());

    static JsonArray A(params JsonNode[] items) => new(items);
    static JsonObject Cond(string field, string op, JsonNode? value = null) => new() { ["field"] = field, ["op"] = op, ["value"] = value };

    /// <summary>A wall with the office's set „Firma”: its sector, its stage and its days, any of them left out.</summary>
    static string Wall(Fixture f, double y, string? sector, string? stage = null, double? days = null, string? storey = null)
    {
        var a = new JsonObject { ["storeyId"] = storey ?? f.Storey, ["x1"] = 0, ["y1"] = y, ["x2"] = 6, ["y2"] = y };
        var id = (string)f.Run("architecture.addWall", a)!["id"]!;
        void Set(string name, string kind, JsonNode value) =>
            f.Run("element.setParam", new JsonObject { ["ids"] = A(id), ["name"] = name, ["kind"] = kind, ["value"] = value, ["pset"] = "Firma" });
        if (sector != null) Set("Sector", "text", sector);
        if (stage != null) Set("Etapa", "text", stage);
        if (days != null) Set("Durata", "number", days.Value);
        return id;
    }

    static string NewTask(Fixture f, string name, JsonObject? extra = null)
    {
        var id = (string)f.Run("planning.addTask", new JsonObject { ["name"] = name, ["duration"] = 5 })!["id"]!;
        if (extra != null) { extra["id"] = id; f.Run("planning.updateTask", extra); }
        return id;
    }

    static JsonObject Schedule(Fixture f) => (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!;
    static List<JsonObject> Tasks(Fixture f) => Schedule(f)["tasks"]!.AsArray().OfType<JsonObject>().ToList();
    static JsonObject Row(Fixture f, string id) => Tasks(f).First(t => (string)t["id"]! == id);
    static JsonObject Named(Fixture f, string name) => Tasks(f).First(t => (string)t["name"]! == name);
    static List<string> Elements(JsonObject row) => (row["elements"] as JsonArray)?.Select(x => (string)x!).Order(StringComparer.Ordinal).ToList() ?? [];
    static List<string> Sorted(params string[] ids) => [.. ids.Order(StringComparer.Ordinal)];

    [Fact]
    public void A_task_rule_takes_elements_by_conditions_on_the_offices_own_fields_and_aliases()
    {
        var f = F();
        var a1 = Wall(f, 0, "A", "E1");
        var a2 = Wall(f, 2, "a", "E2");
        var b1 = Wall(f, 4, "B", "E2");
        var none = Wall(f, 6, null);
        // Sector A's walls: the classes, then a condition on the office's parameter (texts compared case aside).
        var t = NewTask(f, "Masonry sector A", new JsonObject
        {
            ["rule"] = new JsonObject { ["classes"] = A("IfcWall"), ["conditions"] = A(Cond("Firma.Sector", "=", "A")) },
        });
        Assert.Equal(Sorted(a1, a2), Elements(Row(f, t)));
        Assert.Equal("""[{"field":"Firma.Sector","op":"=","value":"A"}]""", Row(f, t)["rule"]!["conditions"]!.ToJsonString());
        // A wall drawn later with that sector joins the task by itself (the rule is worked out on every ask).
        var a3 = Wall(f, 8, "A");
        Assert.Equal(Sorted(a1, a2, a3), Elements(Row(f, t)));
        // Every condition must hold: sector A and stage E2; a wall missing the field, by „missing”.
        f.Run("planning.updateTask", new JsonObject { ["id"] = t, ["rule"] = new JsonObject { ["conditions"] = A(Cond("Firma.Sector", "=", "A"), Cond("Firma.Etapa", "=", "E2")) } });
        Assert.Equal([a2], Elements(Row(f, t)));
        // One undo step: the rule as it was, then again.
        f.Session.Undo();
        Assert.Equal(Sorted(a1, a2, a3), Elements(Row(f, t)));
        f.Session.Redo();
        Assert.Equal([a2], Elements(Row(f, t)));
        var orphan = NewTask(f, "No sector", new JsonObject { ["rule"] = new JsonObject { ["classes"] = A("IfcWall"), ["conditions"] = A(Cond("Firma.Sector", "missing")) } });
        Assert.Equal([none], Elements(Row(f, orphan)));

        // The office's own name for the field (an alias read in order: its sector, else its stage) is what the rule names.
        f.Run("fields.setAlias", new JsonObject { ["name"] = "Zona", ["sources"] = A("Firma.Sector", "Firma.Etapa") });
        var zone = NewTask(f, "Zone B", new JsonObject { ["rule"] = new JsonObject { ["conditions"] = A(Cond("Zona", "=", "b")) } });
        Assert.Equal([b1], Elements(Row(f, zone)));
        // A condition Lapin does not know is refused, and nothing changes.
        Assert.Throws<CommandException>(() => f.Run("planning.updateTask", new JsonObject { ["id"] = zone, ["rule"] = new JsonObject { ["conditions"] = A(Cond("Zona", "like", "b")) } }));
        Assert.Throws<CommandException>(() => f.Run("planning.updateTask", new JsonObject { ["id"] = zone, ["rule"] = new JsonObject { ["conditions"] = A(Cond("Zona", "=")) } }));
        Assert.Equal([b1], Elements(Row(f, zone)));
        // The workbook says what the rule takes.
        var p = Plan.Read(f.Model);
        var table = PlanningModule.Table(p, Cpm.Solve(p), f.Session.Reader);
        Assert.Contains(table, row => row[^1] == "Firma.Sector = A, Firma.Etapa = E2");
    }

    [Fact]
    public void A_rule_reads_the_property_sets_of_a_files_references_and_travels_through_IFC_and_MS_Project()
    {
        var f = F();
        var a = Wall(f, 0, "A", "E1");
        var b = Wall(f, 2, "B", "E2");
        var t = NewTask(f, "Stage E2", new JsonObject { ["rule"] = new JsonObject { ["classes"] = A("IfcWall"), ["conditions"] = A(Cond("Firma.Etapa", "=", "E2")) } });
        Assert.Equal([b], Elements(Row(f, t)));
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "fields.ifc");
        var bytes = ms.ToArray();
        Assert.Contains("Firma.Etapa", Encoding.UTF8.GetString(bytes));

        // Read into a new project: the rule comes back with its condition, and takes the same wall.
        var g = F();
        IfcImport.Run(g.Session, bytes, "fields.ifc", new IfcImportOptions(), out _);
        var back = Tasks(g).Single(x => (string)x["name"]! == "Stage E2");
        Assert.Equal("E2", (string)back["rule"]!["conditions"]![0]!["value"]!);
        Assert.Equal([b], Elements(back));

        // The file as references in another project (the property sets its file gave them): a task by their stage takes them.
        var h = F();
        IfcImport.Run(h.Session, bytes, "fields.ifc", new IfcImportOptions("reference"), out _);
        Assert.True(ReferenceLayers.IsReference(h.Model.Get(b)!));
        if (h.Model.OfClass(Plan.TaskClass).Any()) h.Run("planning.deleteTasks", new JsonObject { ["ids"] = new JsonArray(h.Model.OfClass(Plan.TaskClass).Select(x => (JsonNode)x.Id).ToArray()) });
        var e2 = NewTask(h, "References E2", new JsonObject { ["rule"] = new JsonObject { ["conditions"] = A(Cond("Firma.Etapa", "=", "E2")) } });
        Assert.Equal([b], Elements(Row(h, e2)));
        var e1 = NewTask(h, "References E1", new JsonObject { ["rule"] = new JsonObject { ["conditions"] = A(Cond("Etapa", "starts with", "e1")) } });
        Assert.Equal([a], Elements(Row(h, e1)));

        // MS Project XML: the rule with its condition in the named field, back on import.
        var xml = Encoding.UTF8.GetString(Convert.FromBase64String((string)f.Session.Query("planning.exportMsp", new JsonObject())!));
        Assert.Contains("Firma.Etapa", xml);
        f.Run("planning.deleteTasks", new JsonObject { ["ids"] = A(t) });
        f.Run("planning.importMsp", new JsonObject { ["xml"] = xml });
        Assert.Equal([b], Elements(Named(f, "Stage E2")));
    }

    static JsonObject Spec(Fixture f) => (JsonObject)((JsonObject)f.Session.Query("planning.generatorOptions", new JsonObject())!)["spec"]!.DeepClone();
    static void Generate(Fixture f, JsonObject spec) => f.Run("planning.generate", new JsonObject { ["spec"] = spec.DeepClone() });
    static JsonObject Step(JsonObject spec, string key) => spec["steps"]!.AsArray().OfType<JsonObject>().First(s => (string)s["key"]! == key);

    [Fact]
    public void The_generator_splits_a_step_into_a_task_a_value_of_a_field_with_stable_ids()
    {
        var f = F();
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["name"] = "Ground floor", ["elevation"] = 0, ["height"] = 3 });
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        f.Run("fields.setAlias", new JsonObject { ["name"] = "Sector", ["sources"] = A("Firma.Sector") });
        var a1 = Wall(f, 0, "A");
        var b1 = Wall(f, 2, "Sector B");
        var a2 = Wall(f, 4, "a");
        var none = Wall(f, 6, null);
        var spec = Spec(f);
        Assert.Equal("masonry", (string)spec["template"]!);
        Step(spec, "walls")["splitBy"] = A("Sector");
        Generate(f, spec);
        // The walls' step: its storey, then a task a value (the alias's), the walls without one in a task of their own, last.
        Assert.Equal(["Load-bearing walls", "Load-bearing walls · Ground floor", "Load-bearing walls · Ground floor · Sector A",
            "Load-bearing walls · Ground floor · Sector B", "Load-bearing walls · Ground floor · no Sector"],
            Tasks(f).Select(t => (string)t["name"]!).Where(n => n.StartsWith("Load-bearing", StringComparison.Ordinal)));
        Assert.Equal(Sorted(a1, a2), Elements(Named(f, "Load-bearing walls · Ground floor · Sector A")));
        Assert.Equal([b1], Elements(Named(f, "Load-bearing walls · Ground floor · Sector B")));
        Assert.Equal([none], Elements(Named(f, "Load-bearing walls · Ground floor · no Sector")));
        Assert.Equal("core", (string)Named(f, "Load-bearing walls · Ground floor · Sector A")["part"]!);
        Assert.Equal(0, (int)Schedule(f)["elements"]!["none"]!);
        // Each by its rule: a wall of sector A drawn later joins its task by itself.
        var a3 = Wall(f, 8, "A");
        Assert.Contains(a3, Elements(Named(f, "Load-bearing walls · Ground floor · Sector A")));
        // The next step on the storey waits for every sector's walls (the link to the storey's summary).
        var sectorB = (string)Named(f, "Load-bearing walls · Ground floor · Sector B")["id"]!;
        var gfWalls = (string)Named(f, "Load-bearing walls · Ground floor")["id"]!;
        Assert.Contains(Schedule(f)["links"]!.AsArray(), l => (string)l!["from"]! == gfWalls);

        // Generated again (a sector added): the same tasks keep their ids (their GlobalIds), the new value gets one.
        var c1 = Wall(f, 10, "C");
        Generate(f, spec);
        Assert.Equal(sectorB, (string)Named(f, "Load-bearing walls · Ground floor · Sector B")["id"]!);
        Assert.Equal(gfWalls, (string)Named(f, "Load-bearing walls · Ground floor")["id"]!);
        Assert.Equal([c1], Elements(Named(f, "Load-bearing walls · Ground floor · Sector C")));
        // The spec keeps its split, the configurator opens on it again.
        Assert.Equal("Sector", (string)Step(Spec(f), "walls")["splitBy"]![0]!);

        // A step done once, split by two fields: a task a pair; a step taking only some elements by a condition.
        spec = Spec(f);
        var walls = Step(spec, "walls");
        walls["split"] = "once";
        walls["splitBy"] = A("Sector", "Firma.Etapa");
        walls["conditions"] = A(Cond("Sector", "exists"));
        Generate(f, spec);
        var names = Tasks(f).Select(t => (string)t["name"]!).Where(n => n.StartsWith("Load-bearing", StringComparison.Ordinal)).ToList();
        // In the values' order (A, C, „Sector B”: as a person sorts them).
        Assert.Equal(["Load-bearing walls", "Load-bearing walls · Sector A · no Etapa", "Load-bearing walls · Sector C · no Etapa", "Load-bearing walls · Sector B · no Etapa"], names);
        Assert.Equal(1, (int)Schedule(f)["elements"]!["partial"]!);   // the wall with no sector: its core in no task now (its finishes are)
        // A field that does not read is refused.
        Step(spec, "walls")["conditions"] = A(Cond("Sector", "like", "A"));
        Assert.Throws<CommandException>(() => Generate(f, spec));
    }

    [Fact]
    public void A_steps_duration_comes_from_a_field_summed_or_its_quantity_from_a_field_under_its_norm()
    {
        var f = F();
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["name"] = "Ground floor", ["elevation"] = 0, ["height"] = 3 });
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        Wall(f, 0, "A", days: 2.5);
        Wall(f, 2, "A", days: 1);
        Wall(f, 4, "B", days: 4);
        var spec = Spec(f);
        var walls = Step(spec, "walls");
        walls["splitBy"] = A("Firma.Sector");
        walls["durationField"] = "Firma.Durata";
        Generate(f, spec);
        // 2,5 + 1 = 3,5: 4 working days; sector B 4.
        var a = Named(f, "Load-bearing walls · Ground floor · Sector A");
        Assert.Equal(4, (int)a["duration"]!);
        Assert.Equal("field", (string)a["durationFrom"]!);
        Assert.Equal("Firma.Durata", (string)a["durationField"]!);
        Assert.Equal(4, (int)Named(f, "Load-bearing walls · Ground floor · Sector B")["duration"]!);

        // The quantity from a field (a formula of the office's: the days × 2 m³) under the user's norm: 4 h/m³, a crew of 2.
        f.Run("planning.importNorms", new JsonObject { ["csv"] = "Cod;Denumire;UM;Ore-om;Echipa\nLAP-MASON;Zidărie;mc;4;2\n", ["name"] = "Norme.csv" });
        walls.Remove("durationField");
        walls["quantityField"] = "= Firma.Durata * 2";
        Generate(f, spec);
        // Sector A: (2,5 + 1) × 2 = 7 m³ × 4 h = 28 ÷ (2 × 8) = 1,75: 2 days.
        a = Named(f, "Load-bearing walls · Ground floor · Sector A");
        Assert.Equal(7, (double)a["quantity"]!, 6);
        Assert.Equal("norm", (string)a["durationFrom"]!);
        Assert.Equal(2, (int)a["duration"]!);
        // Without a quantity field, the built-in measure stays the default: 2 walls × 4,5 m³ = 9 m³ × 4 ÷ 16 = 2,25: 3 days.
        walls.Remove("quantityField");
        Generate(f, spec);
        a = Named(f, "Load-bearing walls · Ground floor · Sector A");
        Assert.Equal(9, (double)a["quantity"]!, 6);
        Assert.Equal(3, (int)a["duration"]!);
    }

    [Fact]
    public void A_task_timed_by_a_field_is_worked_out_again_after_the_model_changed()
    {
        var f = F();
        var w1 = Wall(f, 0, "A", days: 2);
        Wall(f, 2, "A", days: 3);
        var t = NewTask(f, "Sector A", new JsonObject
        {
            ["rule"] = new JsonObject { ["conditions"] = A(Cond("Firma.Sector", "=", "A")) },
            ["durationField"] = "Firma.Durata",
        });
        Assert.Equal(5, (int)Row(f, t)["duration"]!);
        Assert.Equal("field", (string)Row(f, t)["durationFrom"]!);
        // The model changes: Retime from fields works it out again, one undo step.
        f.Run("element.setParam", new JsonObject { ["ids"] = A(w1), ["name"] = "Durata", ["kind"] = "number", ["value"] = 6, ["pset"] = "Firma" });
        Assert.Equal(5, (int)Row(f, t)["duration"]!);
        var r = (JsonObject)f.Run("planning.retimeFromFields", new JsonObject())!;
        Assert.Equal(1, (int)r["timed"]!);
        Assert.Equal(1, (int)r["changed"]!);
        Assert.Equal(9, (int)Row(f, t)["duration"]!);
        f.Session.Undo();
        Assert.Equal(5, (int)Row(f, t)["duration"]!);
        // Taken off: the task keeps the duration it has; a summary has none of its own.
        f.Run("planning.updateTask", new JsonObject { ["id"] = t, ["durationField"] = null });
        Assert.Null(Row(f, t)["durationField"]);
        Assert.Equal(0, (int)((JsonObject)f.Run("planning.retimeFromFields", new JsonObject())!)["timed"]!);
        Assert.Throws<CommandException>(() => f.Run("planning.updateTask", new JsonObject { ["id"] = t, ["norm"] = "NO-SUCH" }));
    }

    [Fact]
    public void The_gantt_is_coloured_and_given_columns_by_a_field_in_the_window_and_on_the_sheet()
    {
        var f = F();
        Wall(f, 0, "A", "E1");
        Wall(f, 2, "B", "E2");
        Wall(f, 4, "B", "E1");
        var ta = NewTask(f, "A", new JsonObject { ["rule"] = new JsonObject { ["conditions"] = A(Cond("Firma.Sector", "=", "A")) } });
        var tb = NewTask(f, "B", new JsonObject { ["rule"] = new JsonObject { ["conditions"] = A(Cond("Firma.Sector", "=", "B")) } });
        var empty = NewTask(f, "Site", null);

        // The tasks' values of fields, for the window's grouping and colours: one value, or theirs listed when they differ.
        var q = (JsonObject)f.Session.Query("planning.taskFields", new JsonObject { ["fields"] = A("Firma.Etapa", "task.name") })!;
        JsonObject Of(string id, string field) => (JsonObject)q["tasks"]!.AsArray().First(x => (string)x!["id"]! == id)!["fields"]![field]!;
        Assert.Equal("E1", (string)Of(ta, "Firma.Etapa")["value"]!);
        Assert.False((bool)Of(ta, "Firma.Etapa")["mixed"]!);
        Assert.Equal("E1, E2", (string)Of(tb, "Firma.Etapa")["value"]!);
        Assert.True((bool)Of(tb, "Firma.Etapa")["mixed"]!);
        Assert.Equal([1, 1], Of(tb, "Firma.Etapa")["values"]!.AsArray().Select(v => (int)v!["count"]!));
        Assert.Null(Of(empty, "Firma.Etapa")["value"]);
        Assert.Equal("Site", (string)Of(empty, "task.name")["value"]!);

        // Kept on the schedule: colour by the sector (B chosen by hand), a column of the stage.
        f.Run("planning.updateSchedule", new JsonObject
        {
            ["gantt"] = new JsonObject { ["colourBy"] = "Firma.Sector", ["colours"] = new JsonObject { ["b"] = "#112233" }, ["columns"] = A("Firma.Etapa") },
        });
        var s = Schedule(f);
        Assert.Equal("Firma.Sector", (string)s["gantt"]!["colourBy"]!);
        Assert.Equal([("A", TaskFields.Palette[0]), ("B", "#112233")], s["legend"]!.AsArray().Select(l => ((string)l!["value"]!, (string)l["colour"]!)));
        Assert.Equal("#112233", (string)Row(f, tb)["colour"]!);
        Assert.Equal("E1, E2", (string)Row(f, tb)["fields"]!["Firma.Etapa"]!);
        Assert.Null(Row(f, empty)["colour"]);
        Assert.Throws<CommandException>(() => f.Run("planning.updateSchedule", new JsonObject { ["gantt"] = new JsonObject { ["colours"] = new JsonObject { ["A"] = "red" } } }));

        // The sheet draws what the window shows: the bars in the values' colours, the column, the legend.
        var schedule = Plan.ScheduleOf(f.Model)!;
        var pages = new GanttSheet().Pages(f.Session.Reader, schedule, 400, null);
        var pens = pages[0].Pens;
        Assert.Contains(pens, p => p.Fill == "#112233" && p.Loops.Count == 2);   // B's bar and its swatch
        Assert.Contains(pens, p => p.Fill == TaskFields.Palette[0]);
        var texts = pens.SelectMany(p => p.Texts).Select(x => x.Text).ToList();
        Assert.Contains("Etapa", texts);
        Assert.Contains("E1, E2", texts);
        Assert.Contains("Sector:", texts);
        // Taken off: the Gantt as it was.
        f.Run("planning.updateSchedule", new JsonObject { ["gantt"] = null });
        Assert.Null(Schedule(f)["gantt"]);
        Assert.Null(Row(f, tb)["colour"]);
    }

    [Fact]
    public void View_filters_already_read_the_offices_aliases_and_a_files_property_sets()
    {
        var f = F();
        var a = Wall(f, 0, "A");
        Wall(f, 2, "B");
        f.Run("fields.setAlias", new JsonObject { ["name"] = "Zona", ["sources"] = A("Firma.Sector") });
        var ids = (JsonObject)f.Session.Query("select.query", new JsonObject
        {
            ["rules"] = A(new JsonObject { ["field"] = "param", ["name"] = "Zona", ["op"] = "equals", ["value"] = "a" }),
        })!;
        Assert.Equal([a], ids["ids"]!.AsArray().Select(x => (string)x!));
    }
}
