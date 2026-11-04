using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing.Export;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.SiteWorks;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Progress, stage 5 of docs/plan-4d5d.md: the status date and the work left rescheduled from the day after it (numbers
/// worked out by hand on the default calendar, March 2027 having no holiday), a task's percentage from its elements marked
/// on site (counted, or weighed in its unit) and a summary's from its tasks (by their durations), the baseline and the
/// variances, the simulation's late and ahead, the marking open to a guest who joined to look, and the progress and
/// baseline through IFC, MS Project XML and the workbook, and in a comparison of versions.
/// </summary>
public sealed class PlanningProgressTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-progress-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new PhasesModule(), new SiteWorksModule(), new PlanningModule());

    static string Task(Fixture f, string name, int days, JsonObject? extra = null)
    {
        var id = (string)f.Run("planning.addTask", new JsonObject { ["name"] = name, ["duration"] = days })!["id"]!;
        if (extra != null) { extra["id"] = id; f.Run("planning.updateTask", extra); }
        return id;
    }

    static void Link(Fixture f, string from, string to) => f.Run("planning.link", new JsonObject { ["from"] = from, ["to"] = to });

    static JsonObject Schedule(Fixture f) => (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!;

    static JsonObject Row(JsonObject s, string id) => (JsonObject)s["tasks"]!.AsArray().First(t => (string)t!["id"]! == id)!;

    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());

    /// <summary>
    /// From Monday 1 March 2027: a summary over A (10 d) → B (5 d) → C (3 d); D (2 d) on its own; E (4 d) done 1–5 March.
    /// A started on Wednesday 3 March and is 60 % done.
    /// </summary>
    static (Fixture F, Dictionary<string, string> Ids) Sample()
    {
        var f = F();
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01", ["name"] = "Casa" });
        var ids = new Dictionary<string, string>
        {
            ["s"] = Task(f, "Structure", 1),
            ["a"] = Task(f, "A", 10),
            ["b"] = Task(f, "B", 5),
            ["c"] = Task(f, "C", 3),
            ["d"] = Task(f, "D", 2),
            ["e"] = Task(f, "E", 4),
        };
        f.Run("planning.indent", new JsonObject { ["ids"] = Ids(ids["a"], ids["b"], ids["c"]) });
        Link(f, ids["a"], ids["b"]);
        Link(f, ids["b"], ids["c"]);
        f.Run("planning.setProgress", new JsonObject { ["id"] = ids["a"], ["actualStart"] = "2027-03-03", ["complete"] = 60 });
        f.Run("planning.setProgress", new JsonObject { ["id"] = ids["e"], ["actualStart"] = "2027-03-01", ["actualFinish"] = "2027-03-05" });
        return (f, ids);
    }

    [Fact]
    public void The_work_left_is_rescheduled_from_the_day_after_the_status_date()
    {
        var (f, ids) = Sample();
        // No status date: A goes on straight after its 6 days done (3, 4, 5, 8, 9, 10 March), its 4 left 11–16 March.
        var s = Schedule(f);
        Assert.Equal("2027-03-03", (string)Row(s, ids["a"])["start"]!);
        Assert.Equal("2027-03-16", (string)Row(s, ids["a"])["finish"]!);
        Assert.Equal("started", (string)Row(s, ids["a"])["status"]!);
        Assert.Equal(4, (int)Row(s, ids["a"])["remaining"]!);

        // Status date Friday 12 March: the work left starts on Monday 15.
        f.Run("planning.updateSchedule", new JsonObject { ["dataDate"] = "2027-03-12" });
        s = Schedule(f);
        Assert.Equal("2027-03-12", (string)s["dataDate"]!);
        // A: from 3 March, its 4 days left 15–18 March; B 19, 22–25 March; C 26, 29–30 March: all critical.
        Assert.Equal(("2027-03-03", "2027-03-18"), ((string)Row(s, ids["a"])["start"]!, (string)Row(s, ids["a"])["finish"]!));
        Assert.Equal(("2027-03-19", "2027-03-25"), ((string)Row(s, ids["b"])["start"]!, (string)Row(s, ids["b"])["finish"]!));
        Assert.Equal(("2027-03-26", "2027-03-30"), ((string)Row(s, ids["c"])["start"]!, (string)Row(s, ids["c"])["finish"]!));
        Assert.All(new[] { "a", "b", "c" }, k => Assert.True((bool)Row(s, ids[k])["critical"]!));
        Assert.Equal(0, (int)Row(s, ids["a"])["totalFloat"]!);
        // D, not started, no earlier than Monday 15 March: 15–16 March, 10 working days of float (to 30 March).
        Assert.Equal(("2027-03-15", "2027-03-16"), ((string)Row(s, ids["d"])["start"]!, (string)Row(s, ids["d"])["finish"]!));
        Assert.Equal(10, (int)Row(s, ids["d"])["totalFloat"]!);
        Assert.Equal("planned", (string)Row(s, ids["d"])["status"]!);
        // E, done on its actual dates, off the critical path.
        Assert.Equal(("2027-03-01", "2027-03-05"), ((string)Row(s, ids["e"])["start"]!, (string)Row(s, ids["e"])["finish"]!));
        Assert.Equal("done", (string)Row(s, ids["e"])["status"]!);
        Assert.Equal(100, (double)Row(s, ids["e"])["complete"]!);
        Assert.False((bool)Row(s, ids["e"])["critical"]!);
        Assert.Equal("2027-03-30", (string)s["finish"]!);
        // The summary: (10 × 60 + 5 × 0 + 3 × 0) ÷ 18 = 33.3 %; the schedule: (600 + 4 × 100) ÷ 24 = 41.7 %.
        Assert.Equal(33.3, (double)Row(s, ids["s"])["complete"]!);
        Assert.Equal("children", (string)Row(s, ids["s"])["progressFrom"]!);
        Assert.Equal("2027-03-03", (string)Row(s, ids["s"])["actualStart"]!);
        Assert.Equal(41.7, (double)s["complete"]!);

        // Typed off again: back to the plan as it was.
        f.Run("planning.setProgress", new JsonObject { ["id"] = ids["a"], ["actualStart"] = "", ["complete"] = null });
        f.Run("planning.updateSchedule", new JsonObject { ["dataDate"] = "" });
        s = Schedule(f);
        Assert.Equal(("2027-03-01", "2027-03-12"), ((string)Row(s, ids["a"])["start"]!, (string)Row(s, ids["a"])["finish"]!));
        Assert.Throws<CommandException>(() => f.Run("planning.setProgress", new JsonObject { ["id"] = ids["s"], ["complete"] = 50 }));
        Assert.Throws<CommandException>(() => f.Run("planning.setProgress", new JsonObject { ["id"] = ids["a"], ["complete"] = 120 }));
        Assert.Throws<CommandException>(() => f.Run("planning.setProgress", new JsonObject { ["id"] = ids["e"], ["actualFinish"] = "2027-02-26" }));
    }

    /// <summary>A storey's four walls in a masonry task, two of them in a second task too (a 6 m wall and a 4 m one).</summary>
    static (Fixture F, string Masonry, string Short, string[] Walls) Walls()
    {
        var f = F();
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        var walls = new[] { f.Wall(0, 0, 6, 0), f.Wall(0, 3, 4, 3), f.Wall(10, 0, 15, 0), f.Wall(10, 3, 15, 3) };
        var masonry = Task(f, "Masonry", 10, new JsonObject { ["rule"] = new JsonObject { ["classes"] = Ids("IfcWall") } });
        var shortOne = Task(f, "Two walls", 5);
        f.Run("planning.assign", new JsonObject { ["task"] = shortOne, ["ids"] = Ids(walls[0], walls[1]) });
        Link(f, masonry, shortOne);
        return (f, masonry, shortOne, walls);
    }

    [Fact]
    public void Elements_marked_on_site_give_their_tasks_progress_counted_or_measured()
    {
        var (f, masonry, shortOne, walls) = Walls();
        f.Run("planning.updateSchedule", new JsonObject { ["dataDate"] = "2027-03-05" });
        // Done on the status date, in each task that takes it.
        var r = f.Run("planning.markDone", new JsonObject { ["ids"] = Ids(walls[0]) })!;
        Assert.Equal(2, (int)r["tasks"]!);
        Assert.Equal("2027-03-05", (string)r["date"]!);
        var s = Schedule(f);
        // Counted: 1 of 4 walls, 1 of 2.
        Assert.Equal(25, (double)Row(s, masonry)["complete"]!);
        Assert.Equal("elements", (string)Row(s, masonry)["progressFrom"]!);
        Assert.Equal(50, (double)Row(s, shortOne)["complete"]!);
        Assert.Equal("2027-03-05", (string)Row(s, masonry)["actualStart"]!);
        Assert.Equal(1, (int)Row(s, masonry)["marked"]!["done"]!);
        Assert.Equal("2027-03-05", (string)Row(s, masonry)["marks"]![walls[0]]!["done"]!);

        // Weighed in the task's unit (metres of wall): 6 m of 10 m.
        // (the generator writes a task's unit with its quantity)
        f.Session.Edit("unit", edit => { var t = edit.Model.Get(shortOne)!; var a = t.CloneAttrs(); a["unit"] = "m"; edit.Replace(t.With(attrs: a)); });
        Assert.Equal(60, (double)Row(Schedule(f), shortOne)["complete"]!, 0);

        // Started, then done on an earlier day; the second wall done: the task is finished on the last day marked.
        f.Run("planning.markStarted", new JsonObject { ["ids"] = Ids(walls[1]), ["date"] = "2027-03-02", ["task"] = shortOne });
        s = Schedule(f);
        Assert.Equal("2027-03-02", (string)Row(s, shortOne)["actualStart"]!);
        Assert.Equal(1, (int)Row(s, shortOne)["marked"]!["started"]!);
        // Only the task named: the masonry task does not have it started.
        Assert.False(Row(s, masonry)["marks"]!.AsObject().ContainsKey(walls[1]));
        f.Run("planning.markDone", new JsonObject { ["ids"] = Ids(walls[1]), ["date"] = "2027-03-04", ["task"] = shortOne });
        s = Schedule(f);
        Assert.Equal(100, (double)Row(s, shortOne)["complete"]!);
        Assert.Equal("done", (string)Row(s, shortOne)["status"]!);
        Assert.Equal("2027-03-05", (string)Row(s, shortOne)["actualFinish"]!);
        Assert.Equal("2027-03-02", (string)Row(s, shortOne)["marks"]![walls[1]]!["started"]!);

        // A percentage typed wins over the marks; taken off, the marks speak again.
        f.Run("planning.setProgress", new JsonObject { ["id"] = masonry, ["complete"] = 80 });
        Assert.Equal(80, (double)Row(Schedule(f), masonry)["complete"]!);
        f.Run("planning.setProgress", new JsonObject { ["id"] = masonry, ["complete"] = null });
        Assert.Equal(25, (double)Row(Schedule(f), masonry)["complete"]!);
        // Back to not started, in every task.
        f.Run("planning.unmark", new JsonObject { ["ids"] = Ids(walls[0], walls[1]) });
        s = Schedule(f);
        Assert.Equal(0, (double)Row(s, masonry)["complete"]!);
        Assert.Null(Row(s, shortOne)["marks"]);
        // An element no task takes, or nothing selected, is said so.
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(20, 0), new JsonArray(24, 0), new JsonArray(24, 4), new JsonArray(20, 4)) })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("planning.markDone", new JsonObject { ["ids"] = Ids(slab) }));
        Assert.Throws<CommandException>(() => f.Run("planning.markDone", new JsonObject { ["ids"] = new JsonArray() }));
    }

    [Fact]
    public void A_baseline_keeps_the_dates_and_the_variances_say_how_far_the_tasks_moved()
    {
        var (f, ids) = Sample();
        f.Run("planning.setProgress", new JsonObject { ["id"] = ids["a"], ["actualStart"] = "", ["complete"] = null });
        Assert.Equal(6, (int)f.Run("planning.saveBaseline", new JsonObject { ["date"] = "2027-02-26" })!["tasks"]!);
        var s = Schedule(f);
        Assert.Equal("2027-02-26", (string)s["baseline"]!["saved"]!);
        Assert.Equal("2027-03-12", (string)Row(s, ids["a"])["baselineFinish"]!);
        Assert.Equal(0, (int)Row(s, ids["c"])["finishVariance"]!);
        Assert.Equal(0, (int)s["baseline"]!["late"]!);

        // B two days longer: B finishes 2 working days late, C starts and finishes 2 late; the schedule's finish too.
        f.Run("planning.updateTask", new JsonObject { ["id"] = ids["b"], ["duration"] = 7 });
        s = Schedule(f);
        Assert.Equal(0, (int)Row(s, ids["b"])["startVariance"]!);
        Assert.Equal(2, (int)Row(s, ids["b"])["finishVariance"]!);
        Assert.Equal(2, (int)Row(s, ids["c"])["startVariance"]!);
        Assert.Equal(2, (int)Row(s, ids["c"])["finishVariance"]!);
        Assert.Equal(5, (int)Row(s, ids["b"])["baselineDuration"]!);
        Assert.Equal(2, (int)s["baseline"]!["late"]!);
        Assert.Equal(2, (int)s["baseline"]!["finishVariance"]!);
        // A two days shorter: ahead (negative) by two.
        f.Run("planning.updateTask", new JsonObject { ["id"] = ids["a"], ["duration"] = 8 });
        Assert.Equal(-2, (int)Row(Schedule(f), ids["a"])["finishVariance"]!);

        // Saved again, it is replaced; cleared, it is gone.
        f.Run("planning.saveBaseline", new JsonObject());
        Assert.Equal(0, (int)Row(Schedule(f), ids["c"])["finishVariance"]!);
        f.Run("planning.clearBaseline", new JsonObject());
        s = Schedule(f);
        Assert.Null(s["baseline"]);
        Assert.Null(Row(s, ids["c"])["baselineStart"]);
    }

    [Fact]
    public void The_simulation_shows_what_is_late_and_what_is_ahead_of_the_baseline()
    {
        var (f, masonry, shortOne, walls) = Walls();
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(20, 0), new JsonArray(24, 0), new JsonArray(24, 4), new JsonArray(20, 4)) })!["id"]!;
        f.Run("planning.assign", new JsonObject { ["task"] = shortOne, ["ids"] = Ids(slab) });
        // Baseline: masonry 1–12 March, the two walls and the slab 15–19 March.
        f.Run("planning.saveBaseline", new JsonObject());
        f.Run("planning.updateTask", new JsonObject { ["id"] = masonry, ["duration"] = 12 });
        // Now masonry runs to 16 March: on 15 March the walls should be built and are not; the second task's start is late too.
        var t = Timeline.Build(f.Session.Reader);
        var e = t.Elements[walls[2]];
        Assert.Equal(new DateOnly(2027, 3, 12), e.Base!.To);
        Assert.Equal(Status.Late, Timeline.Compared(e, new DateOnly(2027, 3, 15)));
        Assert.Equal(Status.Active, Timeline.Compared(e, new DateOnly(2027, 3, 10)));
        Assert.Equal(Status.Done, Timeline.Compared(e, new DateOnly(2027, 3, 17)));
        // A wall marked done early, on 3 March: built on its day, the rest of its task still under way.
        f.Run("planning.markDone", new JsonObject { ["ids"] = Ids(walls[3]), ["date"] = "2027-03-03", ["task"] = masonry });
        t = Timeline.Build(f.Session.Reader);
        Assert.Equal(Status.Done, t.StateOf(walls[3], new DateOnly(2027, 3, 4)));
        Assert.Equal(Status.Active, t.StateOf(walls[2], new DateOnly(2027, 3, 4)));
        // Masonry shortened to 3 days (started on 3 March with its wall marked done: 3–5 March): the second task runs 8–12
        // March, before its baseline's 15–19: its slab is ahead.
        f.Run("planning.updateTask", new JsonObject { ["id"] = masonry, ["duration"] = 3 });
        var s = (JsonObject)f.Session.Query("planning.stateAt", new JsonObject { ["date"] = "2027-03-09" })!;
        Assert.Equal(Status.Ahead, (string)s["elements"]![slab]!["shown"]!);
        Assert.Equal(Status.Active, (string)s["elements"]![slab]!["state"]!);
        // Switched off, the simulation shows the states as they are.
        f.Run("planning.setSimulation", new JsonObject { ["compare"] = false });
        s = (JsonObject)f.Session.Query("planning.stateAt", new JsonObject { ["date"] = "2027-03-09" })!;
        Assert.Null(s["elements"]![slab]!["shown"]);
        Assert.Contains("\"base\"", ((JsonObject)f.Session.Query("planning.timeline", new JsonObject())!).ToJsonString());
    }

    [Fact]
    public void A_guest_who_joined_to_look_may_mark_progress_and_nothing_else()
    {
        foreach (var c in new[] { "planning.markDone", "planning.markStarted", "planning.unmark", "planning.setProgress" })
            Assert.Null(EngineDispatcher.AllowedRemote("doc.execute", new JsonObject { ["command"] = c, ["args"] = new JsonObject { ["ids"] = Ids("x") } }, "viewer"));
        Assert.Equal("You joined to look: ask the host to let you edit.", EngineDispatcher.AllowedRemote("doc.execute", new JsonObject { ["command"] = "planning.addTask" }, "viewer"));
        Assert.Equal("You joined to look: ask the host to let you edit.", EngineDispatcher.AllowedRemote("doc.execute", new JsonObject { ["command"] = "planning.saveBaseline" }, "viewer"));
        Assert.NotNull(EngineDispatcher.AllowedRemote("doc.undo", new JsonObject(), "viewer"));
        Assert.Null(EngineDispatcher.AllowedRemote("doc.execute", new JsonObject { ["command"] = "planning.saveBaseline" }, "editor"));
        // A file path smuggled into a mark is still refused.
        Assert.Equal(HostFiles.Refused, EngineDispatcher.AllowedRemote("doc.execute", new JsonObject { ["command"] = "planning.markDone", ["args"] = new JsonObject { ["path"] = "/etc/passwd" } }, "viewer"));
    }

    /// <summary>What progress must survive an exchange: each task's percentage, actual dates, dates, status and baseline; the status date.</summary>
    static string Essence(JsonObject s) =>
        string.Join("\n", s["tasks"]!.AsArray().Select(t => $"{t!["id"]}|{t["name"]}|{t["start"]}|{t["finish"]}|{t["complete"]}|{t["status"]}|{t["actualStart"]}|{t["actualFinish"]}|{t["baselineStart"]}|{t["baselineFinish"]}|{t["baselineDuration"]}|{t["finishVariance"]}"))
        + $"\n{s["dataDate"]}|{s["finish"]}|{s["complete"]}";

    static (Fixture F, Dictionary<string, string> Ids) WithProgressAndBaseline()
    {
        var (f, ids) = Sample();
        f.Run("planning.setProgress", new JsonObject { ["id"] = ids["a"], ["actualStart"] = "", ["complete"] = null });
        f.Run("planning.saveBaseline", new JsonObject { ["date"] = "2027-02-26" });
        f.Run("planning.setProgress", new JsonObject { ["id"] = ids["a"], ["actualStart"] = "2027-03-03", ["complete"] = 60 });
        f.Run("planning.updateSchedule", new JsonObject { ["dataDate"] = "2027-03-12" });
        f.Run("planning.updateTask", new JsonObject { ["id"] = ids["c"], ["duration"] = 4 });
        return (f, ids);
    }

    [Fact]
    public void Progress_and_the_baseline_go_to_IFC_and_come_back()
    {
        var (f, ids) = WithProgressAndBaseline();
        var expected = Schedule(f);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "progress.ifc");
        var step = StepFile.Read(ms.ToArray());
        // The schedule PLANNED and its baseline BASELINE, the baseline's tasks copies of the six.
        var schedules = step.OfType("IFCWORKSCHEDULE").ToList();
        Assert.Equal(2, schedules.Count);
        Assert.Contains(schedules, w => w[13] is StepEnum { Value: "PLANNED" });
        Assert.Contains(schedules, w => w[13] is StepEnum { Value: "BASELINE" });
        Assert.Equal(12, step.OfType("IFCTASK").Count());
        // The task times: A's actual start and 0.6 done, the status time; E's actual finish.
        var times = step.OfType("IFCTASKTIME").ToList();
        Assert.Contains(times, t => t[19] is double c && Math.Abs(c - 0.6) < 1e-9 && t[16] is string a && a.StartsWith("2027-03-03") && t[14] is string st && st.StartsWith("2027-03-12"));
        Assert.Contains(times, t => t[17] is string fin && fin.StartsWith("2027-03-05"));
        var g = F();
        IfcImport.Run(g.Session, ms.ToArray(), "progress.ifc", new IfcImportOptions(), out _);
        var back = Schedule(g);
        Assert.Equal(Essence(expected), Essence(back));
        Assert.Equal("2027-02-26", (string)back["baseline"]!["saved"]!);
        Assert.Equal(60, g.Model.Get(ids["a"])!.Num("complete"));
    }

    [Fact]
    public void Progress_marks_and_the_baseline_go_to_MS_Project_XML_and_come_back()
    {
        var (f, ids) = WithProgressAndBaseline();
        var expected = Schedule(f);
        var xml = Encoding.UTF8.GetString(Convert.FromBase64String((string)f.Session.Query("planning.exportMsp", new JsonObject())!));
        Assert.Contains("<StatusDate>2027-03-12T16:00:00</StatusDate>", xml);
        Assert.Contains("<PercentComplete>60</PercentComplete>", xml);
        Assert.Contains("<ActualStart>2027-03-03T08:00:00</ActualStart>", xml);
        Assert.Contains("<ActualFinish>2027-03-05T16:00:00</ActualFinish>", xml);
        Assert.Contains("<RemainingDuration>PT32H0M0S</RemainingDuration>", xml);
        Assert.Contains("<Baseline><Number>0</Number><Start>2027-03-22T08:00:00</Start><Finish>2027-03-24T16:00:00</Finish><Duration>PT24H0M0S</Duration>", xml.Replace("\n", "").Replace(" ", ""));
        var g = F();
        g.Run("planning.importMsp", new JsonObject { ["xml"] = xml });
        Assert.Equal(Essence(expected), Essence(Schedule(g)));

        // Elements marked: the marks travel in the elements' field, the percentage comes from them again.
        var (w, masonry, _, walls) = Walls();
        w.Run("planning.markDone", new JsonObject { ["ids"] = Ids(walls[0]), ["date"] = "2027-03-03" });
        var before = Schedule(w);
        var wxml = Encoding.UTF8.GetString(Convert.FromBase64String((string)w.Session.Query("planning.exportMsp", new JsonObject())!));
        w.Run("planning.importMsp", new JsonObject { ["xml"] = wxml });
        var after = Schedule(w);
        Assert.Equal(Essence(before), Essence(after));
        Assert.Equal("elements", (string)Row(after, masonry)["progressFrom"]!);
        Assert.Null(w.Model.Get(masonry)!.NumOrNull("complete"));
    }

    [Fact]
    public void Progress_goes_out_in_the_workbook_and_comes_back_from_it()
    {
        var (f, ids) = WithProgressAndBaseline();
        var expected = Schedule(f);
        var bytes = Convert.FromBase64String((string)f.Session.Query("planning.exportXlsx", new JsonObject())!);
        var table = XlsxReader.Read(bytes);
        Assert.Contains("% complete", table[0]);
        Assert.Contains("Baseline finish", table[0]);
        Assert.Contains(table, r => r.Any(c => c.Trim() == "A") && r.Contains("60") && r.Contains("2027-03-03"));
        // Taken off, then read back from the workbook: the same.
        foreach (var k in new[] { "a", "e" })
            f.Run("planning.setProgress", new JsonObject { ["id"] = ids[k], ["actualStart"] = "", ["actualFinish"] = "", ["complete"] = null });
        Assert.NotEqual(Essence(expected), Essence(Schedule(f)));
        var r = f.Run("planning.importProgress", new JsonObject { ["data"] = Convert.ToBase64String(bytes) })!;
        Assert.Equal(2, (int)r["changed"]!);
        Assert.Equal(0, (int)r["unknown"]!);
        Assert.Equal(Essence(expected), Essence(Schedule(f)));

        // The site's own sheet: by WBS, a status date above, Excel's day numbers and a percentage typed.
        var site = XlsxWriter.Write([("Progress", new List<IReadOnlyList<string>>
        {
            new[] { "Status date", "2027-03-19" },
            new[] { "WBS", "Task", "% complete", "Actual start", "Actual finish" },
            new[] { "1.2", "B", "40", "46464", "" },
            new[] { "9.9", "?", "10", "", "" },
        })]);
        r = f.Run("planning.importProgress", new JsonObject { ["data"] = Convert.ToBase64String(site) })!;
        Assert.Equal(1, (int)r["changed"]!);
        Assert.Equal(1, (int)r["unknown"]!);
        Assert.Equal("2027-03-19", (string)r["dataDate"]!);
        var s = Schedule(f);
        Assert.Equal(40, (double)Row(s, ids["b"])["complete"]!);
        Assert.Equal("2027-03-18", (string)Row(s, ids["b"])["actualStart"]!);
        Assert.Equal(new DateOnly(2027, 3, 18), PlanningModule.CellDate("46464"));
        Assert.Equal(new DateOnly(2027, 3, 18), PlanningModule.CellDate("18.03.2027"));
    }

    [Fact]
    public void Versions_compared_say_which_tasks_moved_and_which_durations_changed()
    {
        var (f, ids) = Sample();
        var files = PackageFiles.Create();
        files.Save(f.Session, Path.Combine(_dir, "p.lapin"));
        files.SaveVersion(f.Session, "first", null, asBytes: false);
        f.Run("planning.updateTask", new JsonObject { ["id"] = ids["b"], ["duration"] = 7 });
        f.Run("planning.deleteTasks", new JsonObject { ["ids"] = Ids(ids["d"]) });
        Task(f, "F", 1);
        var diff = files.CompareVersions(f.Session, 1, "current");
        var section = (JsonObject)diff["sections"]!.AsArray().Single(x => (string)x!["key"]! == "schedule")!;
        Assert.Equal("Work schedule", (string)section["title"]!);
        var rows = section["rows"]!.AsArray().Select(x => ((string)x!["name"]!, (string)x["what"]!, (int?)x["days"])).ToList();
        Assert.Contains(("B", "duration", (int?)2), rows);
        // C moved two working days later; D gone; F new.
        Assert.Contains(("C", "moved", (int?)2), rows);
        Assert.Contains(rows, x => x.Item1 == "D" && x.Item2 == "removed");
        Assert.Contains(rows, x => x.Item1 == "F" && x.Item2 == "added");
        Assert.DoesNotContain(rows, x => x.Item1 == "A");
        var facts = section["facts"]!.AsArray().Select(x => ((string)x!["label"]!, (string?)x["from"], (string?)x["to"])).ToList();
        Assert.Contains(("Finish", "26.03.2027", "30.03.2027"), facts);
        // The same project: nothing about the schedule.
        files.SaveVersion(f.Session, "second", null, asBytes: false);
        Assert.Empty(files.CompareVersions(f.Session, 2, "current")["sections"]!.AsArray());
    }

    [Fact]
    public void An_old_schedule_without_progress_reads_as_not_started()
    {
        var f = F();
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        var id = Task(f, "Old", 5);
        var s = Schedule(f);
        Assert.Equal(0, (double)Row(s, id)["complete"]!);
        Assert.Equal("planned", (string)Row(s, id)["status"]!);
        Assert.Equal("none", (string)Row(s, id)["progressFrom"]!);
        Assert.Null(s["baseline"]);
        Assert.Equal(SimulationLook.Default, SimulationLook.Read(null));
    }
}
