using System.Text;
using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Level of effort (hammock) tasks and as-late-as-possible tasks (docs/plan-4d5d.md, "Level of effort, ALAP"): a LOE task
/// spans from the start of its "from" task (or its SS predecessors) to the finish of its "to" task (or its FF
/// predecessors), follows them when they move, is never critical and drives nothing; an ALAP task is placed as late as its
/// successors and the project's finish let it, the finish and the critical path unchanged. A plan with neither is
/// worked out exactly as before (the fingerprint).
/// </summary>
public class PlanningEffortTests
{
    static Fixture F() => new(new PhasesModule(), new PlanningModule());

    static readonly JsonObject EveryDay = new() { ["days"] = new JsonArray(1, 2, 3, 4, 5, 6, 7), ["holidays"] = "none" };

    static string Task(Fixture f, string name, int duration, string? parent = null)
    {
        var a = new JsonObject { ["name"] = name, ["duration"] = duration };
        if (parent != null) a["parent"] = parent;
        return (string)f.Run("planning.addTask", a)!["id"]!;
    }

    static void Link(Fixture f, string from, string to, string type = "FS", int lag = 0) =>
        f.Run("planning.link", new JsonObject { ["from"] = from, ["to"] = to, ["type"] = type, ["lag"] = lag });

    static JsonObject Schedule(Fixture f) => (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!;

    static JsonObject Row(JsonObject s, string id) => (JsonObject)s["tasks"]!.AsArray().First(t => (string)t!["id"]! == id)!;

    static void Start(Fixture f, string date, JsonObject? calendar = null)
    {
        f.Run("planning.updateSchedule", new JsonObject { ["start"] = date });
        if (calendar != null) f.Run("planning.setCalendar", (JsonObject)calendar.DeepClone());
    }

    /// <summary>Every field the critical path gives, by task name, then the finish and the critical links (by names).</summary>
    static string Fingerprint(Fixture f)
    {
        var p = Plan.Read(f.Model);
        var r = Cpm.Solve(p);
        var sb = new StringBuilder();
        foreach (var t in p.Rows)
        {
            var x = r.Times[t.Id];
            sb.Append($"{t.Name}|{x.EarlyStart:yyyy-MM-dd}|{x.EarlyFinish:yyyy-MM-dd}|{x.LateStart:yyyy-MM-dd}|{x.LateFinish:yyyy-MM-dd}|{x.TotalFloat}|{x.FreeFloat}|{x.Critical}|{x.Duration}|{x.Status}|{x.Remaining}\n");
        }
        sb.Append($"finish {r.Finish:yyyy-MM-dd}\n");
        foreach (var l in p.Links.Where(l => r.CriticalLinks.Contains(l.Id)).OrderBy(l => p.RowOf[l.From]).ThenBy(l => p.RowOf[l.To]))
            sb.Append($"{p.ById[l.From].Name}->{p.ById[l.To].Name}\n");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ no LOE, no ALAP: as before

    [Fact]
    public void A_plan_without_level_of_effort_or_ALAP_tasks_is_worked_out_exactly_as_before()
    {
        var (f, ids) = PlanningTests.Sample();
        Assert.Equal(SampleBefore, Fingerprint(f));
        // With progress and a status date: a task done, one under way.
        f.Run("planning.updateSchedule", new JsonObject { ["dataDate"] = "2026-12-04" });
        f.Run("planning.setProgress", new JsonObject { ["id"] = ids["site"], ["complete"] = 100 });
        f.Run("planning.setProgress", new JsonObject { ["id"] = ids["found"], ["complete"] = 40, ["actualStart"] = "2026-11-23" });
        Assert.Equal(ProgressBefore, Fingerprint(f));
    }

    // ------------------------------------------------------------------ level of effort

    static string Loe(Fixture f, string name, string? from, string? to, string? parent = null)
    {
        var a = new JsonObject { ["name"] = name, ["duration"] = 1, ["effort"] = "loe" };
        if (from != null) a["effortFrom"] = from;
        if (to != null) a["effortTo"] = to;
        if (parent != null) a["parent"] = parent;
        return (string)f.Run("planning.addTask", a)!["id"]!;
    }

    static (string Start, string Finish, int Duration, int Total, int Free, bool Critical) Times(JsonObject s, string id)
    {
        var r = Row(s, id);
        return ((string)r["start"]!, (string)r["finish"]!, (int)r["duration"]!, (int)r["totalFloat"]!, (int)r["freeFloat"]!, (bool)r["critical"]!);
    }

    [Fact]
    public void A_level_of_effort_task_spans_from_its_from_tasks_start_to_its_to_tasks_finish_and_follows_a_delay()
    {
        // A(3) → B(4); C(2) alone. Site management from A to B: days 0–7, every day worked from Monday 5 January 2026.
        var f = F();
        Start(f, "2026-01-05", EveryDay);
        var a = Task(f, "A", 3); var b = Task(f, "B", 4); var c = Task(f, "C", 2);
        Link(f, a, b);
        var pm = Loe(f, "Site management", a, b);
        var s = Schedule(f);
        Assert.Equal(("2026-01-05", "2026-01-11", 7, 0, 0, false), Times(s, pm));
        Assert.Equal("loe", (string)Row(s, pm)["effort"]!);
        Assert.False((bool)Row(s, pm)["milestone"]!);
        Assert.Equal("2026-01-11", (string)s["finish"]!);
        // A slips by two days: the hammock follows, the plan's finish with it.
        f.Run("planning.updateTask", new JsonObject { ["id"] = a, ["duration"] = 5 });
        s = Schedule(f);
        Assert.Equal(("2026-01-05", "2026-01-13", 9, 0, 0, false), Times(s, pm));
        Assert.Equal("2026-01-13", (string)s["finish"]!);
        // It drives nothing: a link from it to C leaves C at the start, and it is never on the critical path.
        Link(f, pm, c);
        s = Schedule(f);
        Assert.Equal("2026-01-05", (string)Row(s, c)["start"]!);
        Assert.DoesNotContain(s["links"]!.AsArray(), l => (string)l!["from"]! == pm && (bool)l["critical"]!);
        // Its dates are derived: dragging it is refused.
        Assert.ThrowsAny<Exception>(() => f.Run("planning.updateTask", new JsonObject { ["id"] = pm, ["start"] = "2026-01-07" }));
    }

    [Fact]
    public void A_level_of_effort_task_takes_its_start_from_SS_and_its_finish_from_FF_predecessors_with_their_lags()
    {
        // X(4) → Y(3) → Z(2). Supervision SS from Y (+1), FF from Z: Y 4–7 so start day 5; Z 7–9 so finish day 9.
        var f = F();
        Start(f, "2026-01-05", EveryDay);
        var x = Task(f, "X", 4); var y = Task(f, "Y", 3); var z = Task(f, "Z", 2);
        Link(f, x, y); Link(f, y, z);
        var sup = Loe(f, "Supervision", null, null);
        Link(f, y, sup, "SS", 1); Link(f, z, sup, "FF");
        var before = Fingerprint(f);
        var s = Schedule(f);
        Assert.Equal(("2026-01-10", "2026-01-13", 4, 0, 0, false), Times(s, sup));
        // Z takes three days more: the finish moves with it.
        f.Run("planning.updateTask", new JsonObject { ["id"] = z, ["duration"] = 5 });
        Assert.Equal(("2026-01-10", "2026-01-16", 7, 0, 0, false), Times(Schedule(f), sup));
        // Without the LOE flag the same task is an ordinary one again.
        f.Run("planning.updateTask", new JsonObject { ["id"] = z, ["duration"] = 2 });
        Assert.Equal(before, Fingerprint(f));
        f.Run("planning.updateTask", new JsonObject { ["id"] = sup, ["effort"] = null });
        Assert.Null(Row(Schedule(f), sup)["effort"]);
    }

    [Fact]
    public void A_level_of_effort_task_under_a_summary_widens_it_but_gives_it_no_float_or_critical_flag()
    {
        var f = F();
        Start(f, "2026-01-05", EveryDay);
        var a = Task(f, "A", 3); var b = Task(f, "B", 4);
        Link(f, a, b);
        var prelim = Task(f, "Preliminaries", 1);
        var c = Task(f, "Hoarding", 2, prelim);
        var pm = Loe(f, "Project management", a, b, prelim);
        var s = Schedule(f);
        Assert.Equal(("2026-01-05", "2026-01-11", 7, 0, 0, false), Times(s, pm));
        // The summary spans it, its float is the hoarding's (5 days), not critical.
        Assert.Equal(("2026-01-05", "2026-01-11", 7, 5, 5, false), Times(s, prelim));
        // A summary is not made a level of effort task, and a LOE task does not span itself or another LOE task.
        Assert.ThrowsAny<Exception>(() => f.Run("planning.updateTask", new JsonObject { ["id"] = prelim, ["effort"] = "loe" }));
        Assert.ThrowsAny<Exception>(() => f.Run("planning.updateTask", new JsonObject { ["id"] = pm, ["effortFrom"] = pm }));
        var other = Loe(f, "QA", a, null);
        Assert.ThrowsAny<Exception>(() => f.Run("planning.updateTask", new JsonObject { ["id"] = other, ["effortTo"] = pm }));
        Assert.Equal(c, (string)Row(s, c)["id"]!);
    }

    [Fact]
    public void A_level_of_effort_task_added_to_a_plan_changes_no_other_tasks_dates()
    {
        var (f, ids) = PlanningTests.Sample();
        var pm = Loe(f, "Site management", ids["site"], ids["done"]);
        var lines = Fingerprint(f).Split('\n').Where(l => !l.StartsWith("Site management|")).ToList();
        Assert.Equal(SampleBefore, string.Join('\n', lines));
        var r = Row(Schedule(f), pm);
        Assert.Equal("2026-11-16", (string)r["start"]!);
        // Up to the handover, a milestone on Friday 9 April 2027: the work it spans ends the working day before.
        Assert.Equal("2027-04-09", (string)Row(Schedule(f), ids["done"])["finish"]!);
        Assert.Equal("2027-04-08", (string)r["finish"]!);
    }

    // ------------------------------------------------------------------ as late as possible

    [Fact]
    public void An_ALAP_task_moves_to_its_late_dates_and_the_finish_and_critical_path_stay()
    {
        // The textbook network of PlanningTests: A(3) → B(4) → D(5) → F(2); A → C(2) → E(1) → F. C: float 6, free 0; E: float 6, free 6.
        var f = F();
        Start(f, "2026-01-05", EveryDay);
        var a = Task(f, "A", 3); var b = Task(f, "B", 4); var c = Task(f, "C", 2);
        var d = Task(f, "D", 5); var e = Task(f, "E", 1); var g = Task(f, "F", 2);
        Link(f, a, b); Link(f, a, c); Link(f, b, d); Link(f, c, e); Link(f, d, g); Link(f, e, g);
        string Critical(JsonObject s) => string.Join(",", s["links"]!.AsArray().Where(l => (bool)l!["critical"]!).Select(l => $"{l!["from"]}>{l["to"]}"));
        var asap = Schedule(f);
        // E alone as late as possible: it goes to its late start (day 11), its free float used up; its total float stays.
        f.Run("planning.updateTask", new JsonObject { ["id"] = e, ["constraint"] = "alap" });
        var s = Schedule(f);
        Assert.Equal(("2026-01-16", "2026-01-16", 1, 6, 0, false), Times(s, e));
        Assert.Equal("alap", (string)Row(s, e)["constraint"]!);
        Assert.Null(Row(s, e)["constraintDate"]);
        // C, before it, now has free float (E moved away), and the finish and critical path are as before.
        Assert.Equal(("2026-01-08", "2026-01-09", 2, 6, 6, false), Times(s, c));
        Assert.Equal((string)asap["finish"]!, (string)s["finish"]!);
        Assert.Equal(Critical(asap), Critical(s));
        // C as late as possible too: it follows E to its own late dates (days 9–10).
        f.Run("planning.updateTask", new JsonObject { ["id"] = c, ["constraint"] = "alap" });
        s = Schedule(f);
        Assert.Equal(("2026-01-14", "2026-01-15", 2, 6, 0, false), Times(s, c));
        Assert.Equal(Critical(asap), Critical(s));
        // A critical task marked ALAP has nowhere to go.
        f.Run("planning.updateTask", new JsonObject { ["id"] = b, ["constraint"] = "alap" });
        Assert.Equal(Times(asap, b), Times(Schedule(f), b));
        // As soon as possible again: the schedule of before.
        foreach (var id in new[] { b, c, e }) f.Run("planning.updateTask", new JsonObject { ["id"] = id, ["constraint"] = "asap" });
        Assert.Equal(asap.ToJsonString(), Schedule(f).ToJsonString());
    }

    // ------------------------------------------------------------------ exchange

    static string Essence(JsonObject s) =>
        string.Join("\n", s["tasks"]!.AsArray().Select(t => $"{t!["id"]}|{t["name"]}|{t["wbs"]}|{t["start"]}|{t["finish"]}|{t["duration"]}|{t["totalFloat"]}|{t["milestone"]}|{t["critical"]}|{t["predecessors"]}|{t["constraint"]}|{t["constraintDate"]}|{t["effort"]}|{t["effortFrom"]}|{t["effortTo"]}"))
        + $"\n{s["start"]}|{s["finish"]}";

    static (Fixture F, Dictionary<string, string> Ids) WithEffort()
    {
        var (f, ids) = PlanningTests.Sample();
        f.Run("planning.setCalendar", new JsonObject { ["winter"] = new JsonObject { ["on"] = false } });
        ids["pm"] = Loe(f, "Site management", ids["site"], ids["done"]);
        ids["sup"] = Loe(f, "Supervision", null, null);
        Link(f, ids["found"], ids["sup"], "SS");
        Link(f, ids["masonry"], ids["sup"], "FF");
        f.Run("planning.updateTask", new JsonObject { ["id"] = ids["masonry"], ["constraint"] = "alap" });
        return (f, ids);
    }

    [Fact]
    public void Level_of_effort_and_ALAP_tasks_go_to_MS_Project_XML_and_come_back()
    {
        var (f, ids) = WithEffort();
        var expected = Schedule(f);
        var xml = Encoding.UTF8.GetString(Convert.FromBase64String((string)f.Session.Query("planning.exportMsp", new JsonObject())!));
        Assert.Contains("<ConstraintType>1</ConstraintType>", xml);
        Assert.Contains("<Alias>Lapin effort</Alias>", xml);
        Assert.Contains($"<Value>loe;from={(int)Row(expected, ids["site"])["row"]!};to={(int)Row(expected, ids["done"])["row"]!}</Value>", xml);
        var g = F();
        g.Run("planning.importMsp", new JsonObject { ["xml"] = xml });
        var back = Schedule(g);
        Assert.Equal(Essence(expected), Essence(back));
        Assert.Equal("loe", (string)Row(back, ids["sup"])["effort"]!);
    }

    [Fact]
    public void Level_of_effort_and_ALAP_tasks_go_to_IFC_in_Lapin_Task_and_come_back()
    {
        var (f, ids) = WithEffort();
        var expected = Schedule(f);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "plan.ifc");
        var bytes = ms.ToArray();
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("'Effort'", text);
        Assert.DoesNotContain(".LEVELOFEFFORT.", text);
        var g = F();
        IfcImport.Run(g.Session, bytes, "plan.ifc", new IfcImportOptions(), out _);
        var back = Schedule(g);
        Assert.Equal(Essence(expected), Essence(back));
        Assert.Equal(ids["site"], (string)Row(back, ids["pm"])["effortFrom"]!);
    }

    /// <summary>Taken from the critical path before level of effort and ALAP were added (26.11.2026).</summary>
    static readonly string SampleBefore = """
        Site set-up|2026-11-16|2026-11-19|2027-03-09|2027-03-12|75|0|False|3|planned|3
        Structure|2026-11-19|2026-12-19|2027-03-12|2027-04-09|39|0|False|20|planned|10
        Foundations|2026-11-19|2026-12-03|2027-03-12|2027-03-24|75|0|False|8|planned|8
        Structure GF|2026-12-07|2026-12-19|2027-03-26|2027-04-09|39|0|False|10|planned|10
        Masonry GF|2026-12-10|2026-12-18|2027-04-02|2027-04-10|77|76|False|6|planned|6
        Roof|2027-04-05|2027-04-10|2027-04-05|2027-04-10|0|0|True|5|planned|5
        Handover|2027-04-09|2027-04-09|2027-04-10|2027-04-10|1|1|False|0|planned|0
        finish 2027-04-10
        
        """;
    static readonly string ProgressBefore = """
        Site set-up|2026-11-16|2026-11-19|2026-11-16|2026-11-19|0|0|False|3|done|0
        Structure|2026-11-23|2027-02-24|2027-03-17|2027-04-09|32|0|False|61|started|10
        Foundations|2026-11-23|2026-12-12|2027-03-17|2027-03-24|68|0|False|13|started|5
        Structure GF|2026-12-16|2027-02-24|2027-03-26|2027-04-09|32|0|False|10|planned|10
        Masonry GF|2026-12-21|2026-12-30|2027-04-02|2027-04-10|70|69|False|6|planned|6
        Roof|2027-04-05|2027-04-10|2027-04-05|2027-04-10|0|0|True|5|planned|5
        Handover|2027-04-09|2027-04-09|2027-04-10|2027-04-10|1|1|False|0|planned|0
        finish 2027-04-10
        
        """;
}
