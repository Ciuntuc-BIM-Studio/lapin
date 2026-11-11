using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing.Export;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Package;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.SiteWorks;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// The schedule generator, stage 3 of docs/plan-4d5d.md: a template's sequence made into tasks a step (and a storey) whose
/// rules take the model's elements, linked storey after storey; durations from the elements' quantities × the user's norms
/// ÷ (crew × the day's hours), worked out here by hand; the norms imported from CSV and XLSX; generated again, what was
/// edited by hand stays; the demolitions first, the formwork and scaffolds living by their hosts' tasks; one undo step.
/// </summary>
public class PlanningGeneratorTests
{
    /// <summary>A ground floor at ±0,00 and a first floor at +3,00, both 3 m high; the schedule starting Monday 1 March 2027.</summary>
    static Fixture F(params ILapinModule[] more)
    {
        var f = new Fixture([new ArchitectureModule(), new StructureModule(), new PhasesModule(), new SiteWorksModule(), new PlanningModule(), .. more]);
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["name"] = "Ground floor", ["elevation"] = 0, ["height"] = 3 });
        f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3, ["height"] = 3 });
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        return f;
    }

    static string Storey(Fixture f, string name) => Spatial.Storeys(f.Model).First(s => s.Name == name).Id;

    /// <summary>A 0,25 m wall of one layer (its core), 3 m high: 6 m long, 4,5 m³.</summary>
    static string Wall(Fixture f, string storey, double y, string? type = null)
    {
        type ??= f.Model.OfClass(IfcClass.WallType).FirstOrDefault(t => t.Name == "Brick 250")?.Id ?? f.Type("Brick 250", ("Brick", 0.25, true));
        return (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = Storey(f, storey), ["x1"] = 0, ["y1"] = y, ["x2"] = 6, ["y2"] = y, ["typeId"] = type })!["id"]!;
    }

    static string Slab(Fixture f, string storey) =>
        (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = Storey(f, storey), ["points"] = new JsonArray(new JsonArray(0, 10), new JsonArray(6, 10), new JsonArray(6, 14), new JsonArray(0, 14)),
        })!["id"]!;

    static string Column(Fixture f, string storey) =>
        (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = Storey(f, storey), ["x"] = 10, ["y"] = 10 })!["id"]!;

    static JsonObject Schedule(Fixture f) => (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!;
    static JsonObject Preview(Fixture f, JsonObject? a = null) => (JsonObject)f.Session.Query("planning.preview", a ?? new JsonObject())!;
    static JsonObject Generate(Fixture f, JsonObject? a = null) => (JsonObject)f.Run("planning.generate", a ?? new JsonObject())!;

    static List<JsonObject> Tasks(Fixture f) => Schedule(f)["tasks"]!.AsArray().OfType<JsonObject>().ToList();
    static JsonObject Task(Fixture f, string name) => Tasks(f).First(t => (string)t["name"]! == name);
    static string IdOf(Fixture f, string name) => (string)Task(f, name)["id"]!;

    /// <summary>The links as "predecessor → successor" by the tasks' names (with the type and lag when not FS 0).</summary>
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

    static JsonObject Spec(Fixture f) => (JsonObject)((JsonObject)f.Session.Query("planning.generatorOptions", new JsonObject())!)["spec"]!.DeepClone();

    [Fact]
    public void The_template_is_chosen_by_the_model_masonry_concrete_steel_or_timber()
    {
        var f = F();
        Wall(f, "Ground floor", 0);
        Assert.Equal("masonry", (string)((JsonObject)f.Session.Query("planning.generatorOptions", new JsonObject())!)["suggested"]!);
        Column(f, "Ground floor");
        Assert.Equal("concrete", (string)((JsonObject)f.Session.Query("planning.generatorOptions", new JsonObject())!)["suggested"]!);

        var s = F(new SteelModule(), new Lapin.Drafting.DraftingModule(), new Lapin.Studio.StudioModule());
        s.Run("steel.addHall", new JsonObject { ["storeyId"] = s.Storey, ["attrs"] = new JsonObject { ["secondary"] = false, ["nodes"] = false } });
        Assert.Equal("steel", Generator.Suggest(s.Model));
        // The hall's frames are the steel structure's task, timed by the default (no norms yet); every element has a task.
        Generate(s, new JsonObject { ["style"] = "steel" });
        var steel = Task(s, "Steel structure");
        Assert.True((int)steel["count"]! > 0);
        Assert.Equal("default", (string)steel["durationFrom"]!);
        Assert.Equal(0, (int)Schedule(s)["elements"]!["none"]!);

        var w = F(new WoodModule());
        var type = w.Type("Frame 145", ("Timber", 0.145, true));
        w.Run("wood.setWallFrame", new JsonObject { ["id"] = type, ["spec"] = FrameSpec.Nordic.ToJson() });
        Wall(w, "Ground floor", 0, type);
        Assert.Equal("timber", Generator.Suggest(w.Model));
        Generate(w, new JsonObject { ["style"] = "timber" });
        Assert.Equal(1, (int)Task(w, "Timber frame walls · Ground floor")["count"]!);
    }

    [Fact]
    public void A_concrete_frame_is_a_task_a_step_and_a_storey_linked_storey_after_storey_every_element_in_one_and_one_undo_step()
    {
        var f = F();
        var wGf = Wall(f, "Ground floor", 0);
        var wFf = Wall(f, "First floor", 0);
        var col = Column(f, "Ground floor");
        var slab = Slab(f, "First floor");
        // The preview shows what Apply makes, before anything is written.
        var p = Preview(f, new JsonObject { ["style"] = "concrete" });
        Assert.Equal(["Site set-up", "Structure", "Structure · Ground floor", "Structure · First floor", "Masonry", "Masonry · Ground floor", "Masonry · First floor"],
            p["tasks"]!.AsArray().Select(t => (string)t!["name"]!));
        Assert.Equal(4, (int)p["summary"]!["total"]!);
        Assert.Equal(4, (int)p["summary"]!["linked"]!);
        Assert.Empty(p["summary"]!["none"]!.AsArray());
        Assert.Empty(Tasks(f));

        var r = Generate(f, new JsonObject { ["style"] = "concrete" });
        Assert.Equal(7, (int)r["tasks"]!);
        Assert.Equal(["Site set-up", "Structure", "Structure · Ground floor", "Structure · First floor", "Masonry", "Masonry · Ground floor", "Masonry · First floor"],
            Tasks(f).Select(t => (string)t["name"]!));
        // The rules take the elements: the column on the ground floor, the slab on the first, the walls' cores.
        Assert.Equal([col], Task(f, "Structure · Ground floor")["elements"]!.AsArray().Select(x => (string)x!));
        Assert.Equal([slab], Task(f, "Structure · First floor")["elements"]!.AsArray().Select(x => (string)x!));
        Assert.Equal([wGf], Task(f, "Masonry · Ground floor")["elements"]!.AsArray().Select(x => (string)x!));
        Assert.Equal("core", (string)Task(f, "Masonry · First floor")["part"]!);
        Assert.Equal(0, (int)Schedule(f)["elements"]!["none"]!);
        // The sequence: each step on the storey below first, the step before on the same storey first.
        Assert.Equal(
            new[]
            {
                "Site set-up → Structure · Ground floor", "Structure · Ground floor → Structure · First floor", "Site set-up → Structure · First floor",
                "Structure · Ground floor → Masonry · Ground floor", "Masonry · Ground floor → Masonry · First floor", "Structure · First floor → Masonry · First floor",
            }.Order(), Links(f).Order());
        // A wall drawn later joins its storey's task by itself (its rule), so nothing needs generating again.
        var later = Wall(f, "First floor", 5);
        Assert.Contains(later, Task(f, "Masonry · First floor")["elements"]!.AsArray().Select(x => (string)x!));
        f.Session.Undo();
        // One step: undone, the schedule is as it was, empty.
        f.Session.Undo();
        Assert.Empty(Tasks(f));
        Assert.Contains(wFf, f.Model.Entities.Select(e => e.Id));
    }

    [Fact]
    public void Load_bearing_masonry_builds_a_storeys_walls_then_its_floor_and_without_storeys_a_step_is_one_task()
    {
        var f = F();
        Wall(f, "Ground floor", 0);
        Wall(f, "First floor", 0);
        Slab(f, "Ground floor");
        Slab(f, "First floor");
        Generate(f);
        Assert.Equal("masonry", (string)f.Model.OfClass(Plan.ScheduleClass).First().Attrs["generator"]!["template"]!);
        Assert.Contains("Load-bearing walls · Ground floor → Floors and ring beams · Ground floor", Links(f));
        Assert.Contains("Floors and ring beams · Ground floor → Load-bearing walls · First floor", Links(f));
        Assert.Contains("Load-bearing walls · Ground floor → Load-bearing walls · First floor", Links(f));
        // Ground floor: walls 10 days from 8 March (after the 5 days of site set-up), the floor over them 10 more.
        Assert.Equal("2027-03-08", (string)Task(f, "Load-bearing walls · Ground floor")["start"]!);
        Assert.Equal("2027-03-22", (string)Task(f, "Floors and ring beams · Ground floor")["start"]!);
        Assert.Equal("2027-04-05", (string)Task(f, "Load-bearing walls · First floor")["start"]!);

        // Not split by storey: a task a step, with all its storeys' elements.
        var spec = Spec(f);
        spec["byStorey"] = false;
        Generate(f, new JsonObject { ["spec"] = spec });
        Assert.Equal(["Site set-up", "Load-bearing walls", "Floors and ring beams"], Tasks(f).Select(t => (string)t["name"]!));
        Assert.Equal(2, (int)Task(f, "Floors and ring beams")["count"]!);
        Assert.Equal(["Site set-up → Load-bearing walls", "Load-bearing walls → Floors and ring beams"], Links(f));
    }

    [Fact]
    public void A_duration_is_the_quantity_times_the_norm_over_the_crew_and_the_days_hours()
    {
        var f = F();
        var wall = Wall(f, "Ground floor", 0);
        Wall(f, "First floor", 0);
        // The user's norms, a CSV with semicolons and decimal commas: masonry 4 man-hours a cubic metre, a crew of 2.
        var csv = "Cod;Denumire;UM;Ore-om;Echipa\nLAP-MASON;Zidărie BCA;mc;4,0;2\nX-1;Without a number;m2;;\n";
        var imp = (JsonObject)f.Run("planning.importNorms", new JsonObject { ["csv"] = csv, ["name"] = "Norme.csv" })!;
        Assert.Equal(1, (int)imp["rows"]!);
        Assert.Equal(1, (int)imp["skipped"]!);
        Assert.Equal(4.5, f.Session.Quantities(wall).SelectMany(s => s.Quantities).First(q => q.Name == "NetVolume").Value, 6);
        Generate(f);
        // 6 × 3 × 0,25 = 4,5 m³ × 4 h = 18 man-hours ÷ (2 × 8 h) = 1,125: 2 working days.
        var gf = Task(f, "Load-bearing walls · Ground floor");
        Assert.Equal(4.5, (double)gf["quantity"]!, 6);
        Assert.Equal("mc", (string)gf["unit"]!);
        Assert.Equal("norm", (string)gf["durationFrom"]!);
        Assert.Equal(2, (int)gf["duration"]!);
        Assert.Equal(2, (int)gf["crew"]!);

        // A crew of one given on the step: 18 ÷ 8 = 2,25: 3 days.
        var spec = Spec(f);
        foreach (var st in spec["steps"]!.AsArray().OfType<JsonObject>()) if ((string)st["key"]! == "walls") st["crew"] = 1;
        var p = Preview(f, new JsonObject { ["spec"] = spec });
        Assert.Equal(3, (int)p["tasks"]!.AsArray().First(t => (string)t!["name"]! == "Load-bearing walls · Ground floor")!["duration"]!);
        Assert.Equal(2, (int)p["summary"]!["fromNorms"]!);
        Assert.False((bool)p["summary"]!["indicative"]!);

        // The same table as a workbook, the norm given as a productivity (0,25 m³ an hour: 4 hours a cubic metre).
        var xlsx = XlsxWriter.Write([("Norms", [new[] { "Code", "Name", "Unit", "Productivity", "Crew" }, new[] { "LAP-MASON", "Masonry", "m3", "0.25", "3" }])]);
        f.Run("planning.importNorms", new JsonObject { ["data"] = Convert.ToBase64String(xlsx), ["name"] = "norms.xlsx" });
        Generate(f);
        // 18 man-hours ÷ (3 × 8) = 0,75: 1 day.
        Assert.Equal(1, (int)Task(f, "Load-bearing walls · Ground floor")["duration"]!);
        Assert.Equal(3, (int)Task(f, "Load-bearing walls · Ground floor")["crew"]!);
        // A step with no norm in the table takes its days, and says so.
        Slab(f, "First floor");
        var q = Preview(f);
        Assert.Contains("Floors and ring beams", q["summary"]!["noNorm"]!.AsArray().Select(x => (string)x!));

        // Lapin's own values, marked indicative wherever they show; the template says it on every row.
        f.Run("planning.useIndicativeNorms", new JsonObject());
        Assert.True((bool)Preview(f)["summary"]!["indicative"]!);
        var template = Encoding.UTF8.GetString(Convert.FromBase64String((string)f.Session.Query("planning.normTemplate", new JsonObject())!));
        Assert.StartsWith("﻿code;name;unit;hours;crew", template);
        Assert.All(template.Split('\n').Skip(1).Where(l => l.Length > 0), l => Assert.Contains("indicative", l));
        Assert.Equal(Norms.Indicative.Count, Norms.Import(Encoding.UTF8.GetBytes(template)).Rows.Count);
        // A table Lapin cannot read is refused.
        Assert.Throws<CommandException>(() => f.Run("planning.importNorms", new JsonObject { ["csv"] = "a;b\n1;2\n" }));
    }

    [Fact]
    public void Generated_again_it_keeps_what_was_edited_by_hand_and_remakes_the_rest_over_the_same_ids()
    {
        var f = F();
        Wall(f, "Ground floor", 0);
        Wall(f, "First floor", 0);
        var slab = Slab(f, "First floor");
        Generate(f);
        var walls1 = IdOf(f, "Load-bearing walls · First floor");
        var floors1 = IdOf(f, "Floors and ring beams · First floor");
        // By hand: a task renamed and given 9 days, an element added to a generated task, a task and a link of one's own.
        var gf = IdOf(f, "Load-bearing walls · Ground floor");
        f.Run("planning.updateTask", new JsonObject { ["id"] = gf, ["name"] = "Walls GF (blocks)", ["duration"] = 9 });
        Assert.True(f.Model.Get(gf)!.Bool("own"));
        f.Run("planning.assign", new JsonObject { ["task"] = walls1, ["ids"] = new JsonArray(slab) });
        var mine = (string)f.Run("planning.addTask", new JsonObject { ["name"] = "Inspection", ["duration"] = 1 })!["id"]!;
        f.Run("planning.link", new JsonObject { ["from"] = floors1, ["to"] = mine });

        // A new wall on the ground floor; generated again.
        var added = Wall(f, "Ground floor", 5);
        var p = Preview(f);
        Assert.Equal(2, (int)p["summary"]!["kept"]!);
        Generate(f);
        Assert.Equal(walls1, IdOf(f, "Load-bearing walls · First floor"));
        Assert.Equal(floors1, IdOf(f, "Floors and ring beams · First floor"));
        var own = Task(f, "Walls GF (blocks)");
        Assert.Equal(gf, (string)own["id"]!);
        Assert.Equal(9, (int)own["duration"]!);
        Assert.True((bool)own["own"]!);
        // The task edited by hand keeps its rule, so the new wall is in it.
        Assert.Contains(added, own["elements"]!.AsArray().Select(x => (string)x!));
        Assert.Equal(1, (int)Task(f, "Load-bearing walls · First floor")["added"]!);
        Assert.Contains("Floors and ring beams · First floor → Inspection", Links(f));
        Assert.Contains("Inspection", Tasks(f).Select(t => (string)t["name"]!));
        Assert.Contains("Walls GF (blocks) → Load-bearing walls · First floor", Links(f));

        // Without the tasks made by hand: the Inspection goes, the edited task stays.
        Generate(f, new JsonObject { ["keep"] = false });
        Assert.DoesNotContain("Inspection", Tasks(f).Select(t => (string)t["name"]!));
        Assert.Contains("Walls GF (blocks)", Tasks(f).Select(t => (string)t["name"]!));
        f.Session.Undo();
        Assert.Contains("Inspection", Tasks(f).Select(t => (string)t["name"]!));
        // Given back to the generator, the task is made again from its step.
        f.Run("planning.updateTask", new JsonObject { ["id"] = gf, ["own"] = false });
        Generate(f);
        Assert.Equal("Load-bearing walls · Ground floor", (string)f.Model.Get(gf)!.Name);
    }

    [Fact]
    public void Demolitions_come_first_and_formwork_and_scaffolds_live_by_their_hosts_tasks()
    {
        var f = F();
        var old = Wall(f, "Ground floor", 20);
        var kept = Wall(f, "Ground floor", 30);
        f.Run("phases.assign", new JsonObject { ["ids"] = new JsonArray(old), ["created"] = "existing", ["demolished"] = "new-construction" });
        f.Run("phases.assign", new JsonObject { ["ids"] = new JsonArray(kept), ["created"] = "existing" });
        var wall = Wall(f, "Ground floor", 0);
        Column(f, "Ground floor");
        var slab = Slab(f, "First floor");
        var formwork = ((JsonArray)f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(slab) })!["ids"]!)[0]!.ToString();
        var scaffold = (string)f.Run("sitework.scaffoldFromWalls", new JsonObject { ["wallIds"] = new JsonArray(wall) })!["id"]!;
        Generate(f, new JsonObject { ["style"] = "concrete" });
        var rows = Tasks(f);
        Assert.Equal("Demolition", (string)rows[0]["name"]!);
        Assert.Equal("DEMOLITION", (string)rows[0]["kind"]!);
        Assert.Equal([old], rows[0]["elements"]!.AsArray().Select(x => (string)x!));
        // The kept wall is no task's (it stays as it is), the new one is the masonry's; nothing is left without a task.
        Assert.DoesNotContain(kept, Tasks(f).SelectMany(t => t["elements"]?.AsArray().Select(x => (string)x!) ?? []));
        Assert.Equal(0, (int)Schedule(f)["elements"]!["none"]!);

        // Demolition 1–5 March, site set-up 8–12, the ground floor's structure 15–26, the first floor's 29 March – 9 April.
        Assert.Equal("2027-03-29", (string)Task(f, "Structure · First floor")["start"]!);
        Assert.Equal("2027-04-09", (string)Task(f, "Structure · First floor")["finish"]!);
        var temps = Schedule(f)["temporaries"]!.AsArray().OfType<JsonObject>().ToDictionary(t => (string)t["id"]!);
        // The slab's formwork: up when its casting starts, struck 21 working days after it ends: 12–16, 19–23, 26–29 April
        // (14 days), Good Friday 30 April and Easter Monday 3 May off, 4–7 May (18), then 10, 11, 12: the 21st on Wednesday 12 May.
        Assert.Equal("2027-03-29", (string)temps[formwork]["from"]!);
        Assert.Equal("2027-05-12", (string)temps[formwork]["to"]!);
        Assert.Equal("formwork", (string)temps[formwork]["kind"]!);
        // The scaffold along the new wall: while its masonry is built (no finishes on it), 29 March – 9 April.
        Assert.Equal("2027-03-29", (string)temps[scaffold]["from"]!);
        Assert.Equal("2027-04-09", (string)temps[scaffold]["to"]!);
        var p = Preview(f);
        Assert.Equal(1, (int)p["summary"]!["formwork"]!);
        Assert.Equal(1, (int)p["summary"]!["scaffolds"]!);
        Assert.Equal(1, (int)p["summary"]!["demolished"]!);

        // The type says how long its formwork stays; the schedule may untie the formwork.
        var type = f.Model.RelatingOf(formwork, IfcClass.RelDefinesByType)!;
        var spec = (JsonObject)type.Attrs["spec"]!.DeepClone();
        spec["stripAfter"] = 0;
        f.Run("sitework.updateFormworkType", new JsonObject { ["id"] = type.Id, ["spec"] = spec });
        Assert.Equal(0, Formwork.StripAfter(f.Model, f.Model.Get(formwork)!));
        Assert.Equal("2027-04-09", (string)Schedule(f)["temporaries"]!.AsArray().First(t => (string)t!["id"]! == formwork)!["to"]!);
        var g = Spec(f);
        g["formwork"] = false;
        g["demolition"] = false;
        Generate(f, new JsonObject { ["spec"] = g });
        Assert.DoesNotContain(formwork, Schedule(f)["temporaries"]!.AsArray().Select(t => (string)t!["id"]!));
        Assert.NotEqual("Demolition", (string)Tasks(f)[0]["name"]!);
        Assert.Equal(1, (int)Schedule(f)["elements"]!["none"]!);
    }

    [Fact]
    public void The_projects_schedule_styles_are_saved_renamed_and_deleted_and_old_projects_open()
    {
        var f = F();
        Wall(f, "Ground floor", 0);
        var spec = Spec(f);
        spec["byStorey"] = false;
        var id = (string)f.Run("planning.saveScheduleStyle", new JsonObject { ["spec"] = spec, ["name"] = "Bloc P+4" })!["styleId"]!;
        Assert.StartsWith("own:", id);
        Assert.False(Generator.StyleSpec(f.Model, id)!.ByStorey);
        f.Run("planning.renameScheduleStyle", new JsonObject { ["styleId"] = id, ["name"] = "Firm's" });
        Assert.Equal("Firm's", (string)Generator.OwnStyles(f.Model)[0]["label"]!);
        Generate(f, new JsonObject { ["style"] = id });
        Assert.Equal(["Site set-up", "Load-bearing walls"], Tasks(f).Select(t => (string)t["name"]!));
        f.Run("planning.deleteScheduleStyle", new JsonObject { ["styleId"] = id });
        Assert.Empty(Generator.OwnStyles(f.Model));
        Assert.Throws<CommandException>(() => f.Run("planning.generate", new JsonObject { ["style"] = id }));

        // A project from before the generator: tasks without gen, a rule without demolished; it reads and generates.
        var g = F();
        Wall(g, "Ground floor", 0);
        var t = (string)g.Run("planning.addTask", new JsonObject { ["name"] = "Old task" })!["id"]!;
        g.Run("planning.updateTask", new JsonObject { ["id"] = t, ["rule"] = new JsonObject { ["classes"] = new JsonArray("IfcWall") } });
        Assert.Null(g.Model.Get(t)!.Attrs["own"]);
        var opts = (JsonObject)g.Session.Query("planning.generatorOptions", new JsonObject())!;
        Assert.False((bool)opts["generated"]!);
        Assert.Equal("masonry", (string)opts["spec"]!["template"]!);
        using var ms = new MemoryStream();
        ProjectPackage.Write(g.Session, ms);
        ms.Position = 0;
        var content = LapinPackage.Read(ms);
        var h = new Fixture(new ArchitectureModule(), new StructureModule(), new PhasesModule(), new SiteWorksModule(), new PlanningModule());
        h.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Generate(h);
        Assert.Contains("Old task", Tasks(h).Select(x => (string)x["name"]!));
        Assert.Contains("Load-bearing walls · Ground floor", Tasks(h).Select(x => (string)x["name"]!));
    }

    /// <summary>A building service as an IFC file's reference brings it: the file's class, a body of its own, on a storey.</summary>
    static string Service(Fixture f, string storey, string cls, double x)
    {
        var corners = new List<Lapin.Geometry.Vec3> { new(x, 20, 0.5), new(x + 1, 20, 0.5), new(x, 21, 0.5), new(x, 20, 1.5) };
        var faces = new List<(int, int, int)> { (0, 2, 1), (0, 1, 3), (1, 2, 3), (2, 0, 3) };
        string? id = null;
        f.Session.Edit("Service", edit =>
        {
            var e = new Entity(edit.NewId(), cls, cls[3..], new JsonObject
            {
                [StoredBodies.Key] = StoredBodies.Write([("Body", "#888888", corners, faces)]), ["reference"] = true, ["ifcClass"] = cls,
            });
            edit.Add(e);
            Spatial.Contain(edit, Storey(f, storey), e.Id);
            id = e.Id;
        });
        return id!;
    }

    static List<string> ElementsOf(Fixture f, string task) => Task(f, task)["elements"]!.AsArray().Select(x => (string)x!).ToList();

    [Fact]
    public void Every_building_service_is_in_the_installations_a_ventilation_unit_its_recovery_dampers_ducts_and_Lapins_exhaust_fan()
    {
        // A tester's ventilation was in no task: the step listed the classes it knew, and a unitary equipment was not one of them.
        var f = F(new MepModule());
        var wall = Wall(f, "Ground floor", 0);
        var unit = Service(f, "Ground floor", "IfcUnitaryEquipment", 0);
        var recovery = Service(f, "Ground floor", "IfcAirToAirHeatRecovery", 2);
        var damper = Service(f, "Ground floor", "IfcDamper", 4);
        var silencer = Service(f, "Ground floor", "IfcDuctSilencer", 6);
        var sensor = Service(f, "Ground floor", "IfcSensor", 8);
        // IFC2X3's generic classes, as such a file's elements are.
        var segment = Service(f, "First floor", "IfcFlowSegment", 0);
        var control = Service(f, "First floor", "IfcDistributionControlElement", 2);
        var duct = Service(f, "First floor", "IfcDuctSegment", 4);
        // Lapin's own: an exhaust fan and the duct the mep module draws from it to an outlet in the wall.
        Wall(f, "First floor", 0);
        var fan = (string)f.Run("mep.placeFan", new JsonObject { ["storeyId"] = Storey(f, "First floor"), ["kind"] = "ceilingDuct", ["x"] = 2, ["y"] = 2, ["height"] = 2.5 })!["id"]!;
        var fanDuct = (string)f.Run("mep.fanDuct", new JsonObject { ["id"] = fan, ["x"] = 2, ["y"] = -0.6, ["outlet"] = "wall" })!["ductId"]!;
        Assert.Equal("LapinExhaustFan", f.Model.Get(fan)!.Class);
        Assert.Equal("IfcDuctSegment", f.Model.Get(fanDuct)!.Class);

        Generate(f, new JsonObject { ["style"] = "masonry" });
        Assert.Equal([unit, recovery, damper, silencer, sensor], ElementsOf(f, "Installations · Ground floor"));
        var first = ElementsOf(f, "Installations · First floor");
        foreach (var id in new[] { segment, control, duct, fan, fanDuct }) Assert.Contains(id, first);
        Assert.All(first, id => Assert.True(IfcSchema.IsDistribution(f.Model.Get(id)!.Class), f.Model.Get(id)!.Class));
        // Per storey, the storey below first; the walls stay the walls' (and nothing is left without a task).
        Assert.Contains("Installations · Ground floor → Installations · First floor", Links(f));
        Assert.Contains(wall, ElementsOf(f, "Load-bearing walls · Ground floor"));
        Assert.DoesNotContain(wall, ElementsOf(f, "Installations · Ground floor"));
        Assert.Equal(0, (int)Schedule(f)["elements"]!["none"]!);
        // The rule names the supertype, so a service drawn later joins its storey's task by itself.
        var later = Service(f, "Ground floor", "IfcHumidifier", 10);
        Assert.Contains(later, ElementsOf(f, "Installations · Ground floor"));
    }

    [Fact]
    public void The_installations_take_no_other_steps_elements_and_a_stored_spec_with_the_old_list_takes_every_service()
    {
        // No other step of any template names a service, so none of their elements moves (a downpipe, written as an
        // IfcPipeSegment, is the roof's: its class is Lapin's own, not a service's).
        foreach (var t in Generator.Templates)
            foreach (var step in t.Spec.Steps.Where(s => s.Key != "mep"))
                Assert.DoesNotContain(step.Classes, c => IfcSchema.IsDistribution(c));
        Assert.False(IfcSchema.IsDistribution("LapinDownpipe"));
        Assert.False(IfcSchema.IsDistribution(IfcClass.Wall));
        Assert.Equal([IfcSchema.DistributionElement], Generator.TemplateOf("concrete").Spec.Steps.Single(s => s.Key == "mep").Classes);
        // A rule's supertype takes its subtypes, IFC2X3's generic classes among them; a class by its name as before.
        var rule = new TaskRule(["IfcFlowTerminal"], [], [], [], [], []);
        Assert.True(rule.TakesClass("IfcAirTerminal"));
        Assert.True(rule.TakesClass("IfcFlowTerminal"));
        Assert.False(rule.TakesClass("IfcDuctSegment"));
        Assert.True(new TaskRule([IfcClass.Wall], [], [], [], [], []).TakesClass(IfcClass.Wall));
        Assert.False(new TaskRule([IfcClass.Wall], [], [], [], [], []).TakesClass("IfcWallStandardCase"));

        // A schedule generated by an older Lapin keeps its spec: the Installations' old list is read as every service.
        var old = new JsonArray(
            "IfcPipeSegment", "IfcPipeFitting", "IfcDuctSegment", "IfcDuctFitting", "IfcCableSegment", "IfcCableCarrierSegment", "IfcCableCarrierFitting",
            "IfcSanitaryTerminal", "IfcValve", "IfcBoiler", "IfcFlowMeter", "IfcAirTerminal", "IfcTank", "IfcSwitchingDevice", "IfcSpaceHeater", "IfcOutlet",
            "IfcLightFixture", "IfcFilter", "IfcFan", "IfcPump", "IfcElectricDistributionBoard", "IfcBurner", "IfcDistributionChamberElement", "IfcFlowTerminal");
        var read = GenStep.Read(new JsonObject { ["key"] = "mep", ["name"] = "Installations", ["classes"] = old.DeepClone(), ["split"] = "storey" }, 0)!;
        Assert.Equal([IfcSchema.DistributionElement], read.Classes);
        // A list of the user's own stays as it is.
        var own = GenStep.Read(new JsonObject { ["key"] = "plumbing", ["classes"] = new JsonArray("IfcPipeSegment", "IfcPipeFitting") }, 0)!;
        Assert.Equal(["IfcPipeSegment", "IfcPipeFitting"], own.Classes);

        var f = F(new MepModule());
        var unit = Service(f, "Ground floor", "IfcUnitaryEquipment", 0);
        var spec = Spec(f);
        foreach (var s in spec["steps"]!.AsArray().OfType<JsonObject>().Where(s => (string?)s["key"] == "mep")) s["classes"] = old.DeepClone();
        Generate(f, new JsonObject { ["spec"] = spec });
        Assert.Equal([unit], ElementsOf(f, "Installations · Ground floor"));
    }
}
