using System.Text;
using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The planning module, stage 1 of docs/plan-4d5d.md: the calendar (Romania's holidays, Orthodox Easter), the critical
/// path on textbook networks worked by hand, the commands that edit the WBS and the links, and the schedule kept in the
/// package and carried through IFC and MS Project XML to the same dates.
/// </summary>
public class PlanningTests
{
    static Fixture F() => new(new PhasesModule(), new PlanningModule());

    static DateOnly D(string s) => DateOnly.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Every day worked, no holiday: working days are calendar days, as in the textbooks.</summary>
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

    // ------------------------------------------------------------------ calendar

    [Theory]
    [InlineData(2021, "2021-05-02")]
    [InlineData(2022, "2022-04-24")]
    [InlineData(2023, "2023-04-16")]
    [InlineData(2024, "2024-05-05")]
    [InlineData(2025, "2025-04-20")]
    [InlineData(2026, "2026-04-12")]
    [InlineData(2027, "2027-05-02")]
    [InlineData(2028, "2028-04-16")]
    [InlineData(2029, "2029-04-08")]
    [InlineData(2030, "2030-04-28")]
    public void Orthodox_Easter_falls_on_the_known_Sunday(int year, string easter)
    {
        var d = WorkCalendar.OrthodoxEaster(year);
        Assert.Equal(D(easter), d);
        Assert.Equal(DayOfWeek.Sunday, d.DayOfWeek);
    }

    [Fact]
    public void Romanias_legal_holidays_of_2026_include_Epiphany_Good_Friday_and_Pentecost_on_Childrens_Day()
    {
        var h = WorkCalendar.RomanianHolidays(2026);
        var days = h.Select(x => x.Date).ToList();
        Assert.Contains(D("2026-01-06"), days);
        Assert.Contains(D("2026-01-07"), days);
        Assert.Contains(D("2026-01-24"), days);
        Assert.Contains(D("2026-04-10"), days);   // Good Friday
        Assert.Contains(D("2026-04-13"), days);   // Easter Monday
        Assert.Contains(D("2026-05-31"), days);   // Pentecost
        Assert.Contains(D("2026-11-30"), days);
        Assert.Contains(D("2026-12-26"), days);
        // Whit Monday falls on Children's Day: one day off, both names.
        Assert.Equal(16, h.Count);
        Assert.Contains("Rusaliile", h.Single(x => x.Date == D("2026-06-01")).Name);
        Assert.Contains("Ziua Copilului", h.Single(x => x.Date == D("2026-06-01")).Name);
        Assert.Equal(17, WorkCalendar.RomanianHolidays(2027).Count);
    }

    [Fact]
    public void Working_days_skip_weekends_and_holidays_and_a_wet_task_the_winter_break()
    {
        var c = WorkCalendar.Default;
        // Thursday 26.11.2026, five days: Thu, Fri, (St Andrew's Monday, National Day Tuesday), Wed, Thu, Fri.
        Assert.Equal(D("2026-12-05"), c.Offset(D("2026-11-26"), 5));
        Assert.Equal(D("2026-11-26"), c.Offset(D("2026-12-05"), -5));
        Assert.Equal(5, c.WorkingDays(D("2026-11-26"), D("2026-12-05")));
        Assert.False(c.Working(D("2026-04-10")));
        Assert.Equal(D("2026-04-14"), c.NextWorking(D("2026-04-10")));
        var winter = c with { Winter = true };
        Assert.True(winter.Working(D("2027-01-12")));
        Assert.False(winter.Working(D("2027-01-12"), wet: true));
        Assert.Equal(D("2027-03-01"), winter.NextWorking(D("2026-12-15"), wet: true));
        // An exception: a Saturday worked, a Monday off.
        var own = c with { Exceptions = [new(D("2026-11-21"), D("2026-11-21"), true, "Saturday worked"), new(D("2026-11-23"), D("2026-11-23"), false, "Site closed")] };
        Assert.True(own.Working(D("2026-11-21")));
        Assert.False(own.Working(D("2026-11-23")));
    }

    [Fact]
    public void A_calendar_read_with_every_holiday_listed_takes_Romanias_rule_back()
    {
        var c = WorkCalendar.Default;
        var listed = c.NonWorking(D("2026-01-01"), D("2027-12-31")).Select(x => new CalendarException(x.Day, x.Day, false, x.Why)).ToList();
        listed.Add(new(D("2026-08-14"), D("2026-08-14"), false, "Bridge day"));
        var read = WorkCalendar.FromExplicit([1, 2, 3, 4, 5], "08:00", 8, listed);
        Assert.Equal(WorkCalendar.Romania, read.Holidays);
        Assert.Single(read.Exceptions);
        Assert.False(read.Working(D("2031-12-01")));
    }

    // ------------------------------------------------------------------ critical path

    [Fact]
    public void The_textbook_network_has_its_hand_worked_dates_floats_and_critical_path()
    {
        // A(3) → B(4) → D(5) → F(2); A → C(2) → E(1) → F. Every day worked from Monday 5 January 2026 (day 0).
        var f = F();
        Start(f, "2026-01-05", EveryDay);
        var a = Task(f, "A", 3); var b = Task(f, "B", 4); var c = Task(f, "C", 2);
        var d = Task(f, "D", 5); var e = Task(f, "E", 1); var g = Task(f, "F", 2);
        Link(f, a, b); Link(f, a, c); Link(f, b, d); Link(f, c, e); Link(f, d, g); Link(f, e, g);
        var s = Schedule(f);
        void Is(string id, string start, string finish, int total, int free, bool critical)
        {
            var r = Row(s, id);
            Assert.Equal(start, (string)r["start"]!);
            Assert.Equal(finish, (string)r["finish"]!);
            Assert.Equal(total, (int)r["totalFloat"]!);
            Assert.Equal(free, (int)r["freeFloat"]!);
            Assert.Equal(critical, (bool)r["critical"]!);
        }
        // ES/EF by hand: A 0–3, B 3–7, C 3–5, D 7–12, E 5–6, F 12–14; floats C 6 (free 0), E 6 (free 6).
        Is(a, "2026-01-05", "2026-01-07", 0, 0, true);
        Is(b, "2026-01-08", "2026-01-11", 0, 0, true);
        Is(c, "2026-01-08", "2026-01-09", 6, 0, false);
        Is(d, "2026-01-12", "2026-01-16", 0, 0, true);
        Is(e, "2026-01-10", "2026-01-10", 6, 6, false);
        Is(g, "2026-01-17", "2026-01-18", 0, 0, true);
        Assert.Equal("2026-01-18", (string)s["finish"]!);
        Assert.Equal(14, (int)s["workingDays"]!);
        Assert.Equal("2026-01-14", (string)Row(s, c)["lateStart"]!);   // LS 9
        var critical = s["links"]!.AsArray().Where(l => (bool)l!["critical"]!).Select(l => ((string)l!["from"]!, (string)l["to"]!)).ToList();
        Assert.Equal([(a, b), (b, d), (d, g)], critical);
    }

    [Fact]
    public void Start_to_start_finish_to_finish_and_start_to_finish_links_with_lags()
    {
        // X(4); Y(3) SS+2; Z(2) FF+1; W(2) SF+3. By hand: X 0–4, Y 2–5, Z 3–5, W 1–3; W's total float 2, the others none.
        var f = F();
        Start(f, "2026-01-05", EveryDay);
        var x = Task(f, "X", 4); var y = Task(f, "Y", 3); var z = Task(f, "Z", 2); var w = Task(f, "W", 2);
        Link(f, x, y, "SS", 2); Link(f, x, z, "FF", 1); Link(f, x, w, "SF", 3);
        var s = Schedule(f);
        Assert.Equal(("2026-01-05", "2026-01-08", 0), ((string)Row(s, x)["start"]!, (string)Row(s, x)["finish"]!, (int)Row(s, x)["totalFloat"]!));
        Assert.Equal(("2026-01-07", "2026-01-09", 0), ((string)Row(s, y)["start"]!, (string)Row(s, y)["finish"]!, (int)Row(s, y)["totalFloat"]!));
        Assert.Equal(("2026-01-08", "2026-01-09", 0), ((string)Row(s, z)["start"]!, (string)Row(s, z)["finish"]!, (int)Row(s, z)["totalFloat"]!));
        Assert.Equal(("2026-01-06", "2026-01-07", 2), ((string)Row(s, w)["start"]!, (string)Row(s, w)["finish"]!, (int)Row(s, w)["totalFloat"]!));
        Assert.Equal("1SS+2d", (string)Row(s, y)["predecessors"]!);
        Assert.Equal("1FF+1d", (string)Row(s, z)["predecessors"]!);
    }

    [Fact]
    public void A_negative_lag_overlaps_and_the_calendar_moves_work_past_holidays()
    {
        // Five-day weeks with Romania's holidays: Masonry (8 d) from Monday 16.11.2026 ends Wednesday 25.11; Plaster FS−2 starts
        // two working days before that ends, Tuesday 24.11, and its 5 days skip St Andrew's and National Day: 24, 25, 26, 27 November, 2 December.
        var f = F();
        Start(f, "2026-11-16");
        var m = Task(f, "Masonry", 8);
        var p = Task(f, "Plaster", 5);
        Link(f, m, p, "FS", -2);
        var s = Schedule(f);
        Assert.Equal("2026-11-25", (string)Row(s, m)["finish"]!);
        Assert.Equal("2026-11-24", (string)Row(s, p)["start"]!);
        Assert.Equal("2026-12-02", (string)Row(s, p)["finish"]!);
        Assert.Equal("1FS-2d", (string)Row(s, p)["predecessors"]!);
    }

    [Fact]
    public void Constraints_hold_a_start_and_a_must_start_on_date_takes_float_from_what_precedes_it()
    {
        var f = F();
        Start(f, "2026-01-05", EveryDay);
        var a = Task(f, "A", 5);
        var b = Task(f, "B", 2);
        Link(f, a, b);
        // Start no earlier than the 12th.
        f.Run("planning.updateTask", new JsonObject { ["id"] = b, ["constraint"] = "snet", ["constraintDate"] = "2026-01-12" });
        var s = Schedule(f);
        Assert.Equal("2026-01-12", (string)Row(s, b)["start"]!);
        Assert.Equal(2, (int)Row(s, a)["totalFloat"]!);
        // Must start on the 8th, before A ends: A runs late by two days.
        f.Run("planning.updateTask", new JsonObject { ["id"] = b, ["constraint"] = "mso", ["constraintDate"] = "2026-01-08" });
        s = Schedule(f);
        Assert.Equal("2026-01-08", (string)Row(s, b)["start"]!);
        Assert.Equal(-2, (int)Row(s, a)["totalFloat"]!);
        Assert.True((bool)Row(s, a)["critical"]!);
        // Dragging a bar to a date sets start-no-earlier-than; dragging its end, its duration in working days.
        f.Run("planning.updateTask", new JsonObject { ["id"] = a, ["start"] = "2026-01-07" });
        f.Run("planning.updateTask", new JsonObject { ["id"] = a, ["finish"] = "2026-01-09" });
        s = Schedule(f);
        Assert.Equal(("snet", "2026-01-07"), ((string)Row(s, a)["constraint"]!, (string)Row(s, a)["constraintDate"]!));
        Assert.Equal(3, (int)Row(s, a)["duration"]!);
    }

    [Fact]
    public void A_summary_spans_its_tasks_and_a_link_to_it_holds_every_task_under_it()
    {
        var f = F();
        Start(f, "2026-01-05", EveryDay);
        var site = Task(f, "Site set-up", 2);
        var structure = Task(f, "Structure", 1);
        var gf = Task(f, "Ground floor", 4, structure);
        var ff = Task(f, "First floor", 3, structure);
        Link(f, gf, ff);
        Link(f, site, structure);
        var roof = Task(f, "Roof", 2);
        Link(f, structure, roof);
        var s = Schedule(f);
        Assert.True((bool)Row(s, structure)["summary"]!);
        Assert.Equal("2", (string)Row(s, structure)["wbs"]!);
        Assert.Equal("2.2", (string)Row(s, ff)["wbs"]!);
        Assert.Equal("2026-01-07", (string)Row(s, gf)["start"]!);
        Assert.Equal(("2026-01-07", "2026-01-13"), ((string)Row(s, structure)["start"]!, (string)Row(s, structure)["finish"]!));
        Assert.Equal(7, (int)Row(s, structure)["duration"]!);
        Assert.Equal("2026-01-14", (string)Row(s, roof)["start"]!);
        // A summary takes its dates from its tasks; a link into its own task is refused.
        Assert.Throws<CommandException>(() => f.Run("planning.updateTask", new JsonObject { ["id"] = structure, ["duration"] = 3 }));
        Assert.Throws<CommandException>(() => Link(f, structure, gf));
    }

    // ------------------------------------------------------------------ commands

    [Fact]
    public void Tasks_are_indented_outdented_moved_and_deleted_with_what_is_under_them_each_one_undo_step()
    {
        var f = F();
        var a = Task(f, "A", 1); var b = Task(f, "B", 1); var c = Task(f, "C", 1); var d = Task(f, "D", 1);
        f.Run("planning.indent", new JsonObject { ["ids"] = new JsonArray(b, c) });
        var s = Schedule(f);
        Assert.Equal(["1", "1.1", "1.2", "2"], s["tasks"]!.AsArray().Select(t => (string)t!["wbs"]!));
        // Outdenting B takes C (after it, under A) along: the rows keep their order.
        f.Run("planning.outdent", new JsonObject { ["ids"] = new JsonArray(b) });
        s = Schedule(f);
        Assert.Equal([a, b, c, d], s["tasks"]!.AsArray().Select(t => (string)t!["id"]!));
        Assert.Equal(["1", "2", "2.1", "3"], s["tasks"]!.AsArray().Select(t => (string)t!["wbs"]!));
        f.Run("planning.moveTask", new JsonObject { ["id"] = d, ["offset"] = -1 });
        Assert.Equal([a, d, b, c], Schedule(f)["tasks"]!.AsArray().Select(t => (string)t!["id"]!));
        // Insert before a task: the new one takes its place.
        var x = (string)f.Run("planning.addTask", new JsonObject { ["before"] = d, ["name"] = "X" })!["id"]!;
        Assert.Equal([a, x, d, b, c], Schedule(f)["tasks"]!.AsArray().Select(t => (string)t!["id"]!));
        f.Run("planning.deleteTasks", new JsonObject { ["ids"] = new JsonArray(b) });
        Assert.Equal([a, x, d], Schedule(f)["tasks"]!.AsArray().Select(t => (string)t!["id"]!));
        f.Session.Undo();
        Assert.Equal(5, Schedule(f)["tasks"]!.AsArray().Count);
        // The core's Delete on a task takes its subtask too.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(b) });
        Assert.Null(f.Model.Get(c));
    }

    [Fact]
    public void A_link_that_makes_a_loop_is_refused_and_the_predecessors_column_is_typed_as_in_MS_Project()
    {
        var f = F();
        Start(f, "2026-01-05", EveryDay);
        var a = Task(f, "A", 2); var b = Task(f, "B", 2); var c = Task(f, "C", 2);
        Link(f, a, b); Link(f, b, c);
        var ex = Assert.Throws<CommandException>(() => Link(f, c, a));
        Assert.Contains("loop", ex.Message);
        Assert.Throws<CommandException>(() => Link(f, a, a));
        // C after A by two days start to start, and after B: replaces C's links in.
        f.Run("planning.setPredecessors", new JsonObject { ["id"] = c, ["text"] = "1SS+2, 2" });
        var s = Schedule(f);
        Assert.Equal("1SS+2d, 2", (string)Row(s, c)["predecessors"]!);
        Assert.Equal(3, s["links"]!.AsArray().Count);
        Assert.Throws<CommandException>(() => f.Run("planning.setPredecessors", new JsonObject { ["id"] = a, ["text"] = "3" }));
        Assert.Throws<CommandException>(() => f.Run("planning.setPredecessors", new JsonObject { ["id"] = a, ["text"] = "9" }));
        Assert.Equal([(1, "SS", 2), (2, "FS", 0), (4, "FF", -1)], PlanningModule.ParsePredecessors("1SS+2; 2, 4FF-1d"));
        f.Run("planning.unlink", new JsonObject { ["ids"] = new JsonArray(b, c) });
        Assert.Equal(2, Schedule(f)["links"]!.AsArray().Count);
        // A chain of the tasks selected.
        var d = Task(f, "D", 1); var e = Task(f, "E", 1);
        f.Run("planning.linkChain", new JsonObject { ["ids"] = new JsonArray(e, d) });
        Assert.Equal("4", (string)Row(Schedule(f), e)["predecessors"]!);
    }

    [Fact]
    public void The_schedule_is_kept_in_the_package_and_opens_again_the_same()
    {
        var f = F();
        Start(f, "2026-03-02");
        f.Run("planning.setCalendar", new JsonObject { ["winter"] = new JsonObject { ["on"] = true }, ["exceptions"] = new JsonArray(new JsonObject { ["from"] = "2026-03-06", ["name"] = "Site closed" }) });
        var a = Task(f, "Foundations", 10);
        var b = Task(f, "Walls", 12);
        f.Run("planning.updateTask", new JsonObject { ["id"] = b, ["wet"] = true });
        Link(f, a, b, "SS", 3);
        var before = Schedule(f).ToJsonString();
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        ms.Position = 0;
        var content = LapinPackage.Read(ms);
        var g = F();
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Equal(before, Schedule(g).ToJsonString());
    }

    // ------------------------------------------------------------------ exchange

    /// <summary>A small schedule of every kind of thing: a summary, a milestone, a wet task, a constraint, links of each type with lags, holidays crossed.</summary>
    static (Fixture F, Dictionary<string, string> Ids) Sample()
    {
        var f = F();
        Start(f, "2026-11-16");
        f.Run("planning.updateSchedule", new JsonObject { ["name"] = "Bloc P+4" });
        f.Run("planning.setCalendar", new JsonObject { ["winter"] = new JsonObject { ["on"] = true, ["from"] = "12-20", ["to"] = "02-15" } });
        var ids = new Dictionary<string, string>();
        ids["site"] = Task(f, "Site set-up", 3);
        ids["structure"] = Task(f, "Structure", 1);
        ids["found"] = Task(f, "Foundations", 8, ids["structure"]);
        ids["gf"] = Task(f, "Structure GF", 10, ids["structure"]);
        f.Run("planning.updateTask", new JsonObject { ["id"] = ids["gf"], ["wet"] = true, ["kind"] = "CONSTRUCTION" });
        ids["masonry"] = Task(f, "Masonry GF", 6);
        ids["roof"] = Task(f, "Roof", 5);
        f.Run("planning.updateTask", new JsonObject { ["id"] = ids["roof"], ["constraint"] = "snet", ["constraintDate"] = "2027-04-05" });
        ids["done"] = (string)f.Run("planning.addTask", new JsonObject { ["name"] = "Handover", ["milestone"] = true })!["id"]!;
        Link(f, ids["site"], ids["structure"]);
        Link(f, ids["found"], ids["gf"], "FS", 2);
        Link(f, ids["gf"], ids["masonry"], "SS", 3);
        Link(f, ids["gf"], ids["roof"], "FF", 1);
        Link(f, ids["masonry"], ids["done"]);
        Link(f, ids["roof"], ids["done"], "FS", -1);
        return (f, ids);
    }

    /// <summary>What must survive an exchange: each task's name, WBS, dates, duration, floats, milestone and critical flags; the links.</summary>
    static string Essence(JsonObject s) =>
        string.Join("\n", s["tasks"]!.AsArray().Select(t => $"{t!["id"]}|{t["name"]}|{t["wbs"]}|{t["start"]}|{t["finish"]}|{t["duration"]}|{t["totalFloat"]}|{t["milestone"]}|{t["critical"]}|{t["predecessors"]}|{t["constraint"]}|{t["constraintDate"]}"))
        + $"\n{s["start"]}|{s["finish"]}";

    [Fact]
    public void The_schedule_goes_to_IFC_as_processes_and_comes_back_to_the_same_dates()
    {
        var (f, ids) = Sample();
        var expected = Schedule(f);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "plan.ifc");
        var bytes = ms.ToArray();
        var step = StepFile.Read(bytes);
        Assert.Single(step.OfType("IFCWORKSCHEDULE"));
        Assert.Single(step.OfType("IFCWORKCALENDAR"));
        Assert.Equal(7, step.OfType("IFCTASK").Count());
        Assert.Equal(6, step.OfType("IFCRELSEQUENCE").Count());
        Assert.Contains(step.OfType("IFCRELSEQUENCE"), r => r[7] is StepEnum { Value: "START_START" });
        Assert.Contains(step.OfType("IFCTASK"), t => t[9] is StepEnum { Value: "T" } && (string?)t[2] == "Handover");
        Assert.Single(step.OfType("IFCRELNESTS"), r => step.Get(r[4])?[2] is "Structure");
        Assert.Contains(step.OfType("IFCTASKTIME"), t => t[4] is "P10D");
        Assert.Contains(step.OfType("IFCRELDECLARES"), r => step.Get(r[4])?.Type == "IFCPROJECT");
        // Read into a new project: the same schedule, ids, dates, floats, the calendar's rules.
        var g = F();
        IfcImport.Run(g.Session, bytes, "plan.ifc", new IfcImportOptions(), out _);
        var back = Schedule(g);
        Assert.Equal(Essence(expected), Essence(back));
        Assert.Equal("Bloc P+4", (string)back["name"]!);
        Assert.True((bool)back["calendar"]!["winter"]!["on"]!);
        Assert.Equal("ro", (string)back["calendar"]!["holidays"]!);
        Assert.Equal("CONSTRUCTION", g.Model.Get(ids["gf"])!.PredefinedType);
        Assert.True(g.Model.Get(ids["gf"])!.Bool("wet"));
    }

    [Fact]
    public void The_schedule_goes_to_MS_Project_XML_and_comes_back_to_the_same_dates_and_ids()
    {
        var (f, _) = Sample();
        var expected = Schedule(f);
        var xml = Encoding.UTF8.GetString(Convert.FromBase64String((string)f.Session.Query("planning.exportMsp", new JsonObject())!));
        Assert.Contains("<Project xmlns=\"http://schemas.microsoft.com/project\">", xml);
        Assert.Contains("<Duration>PT80H0M0S</Duration>", xml);
        Assert.Contains("<LinkLag>4800</LinkLag>", xml);
        Assert.Contains("<ConstraintType>4</ConstraintType>", xml);
        var g = F();
        var r = g.Run("planning.importMsp", new JsonObject { ["xml"] = xml })!;
        Assert.Equal(7, (int)r["tasks"]!);
        Assert.Equal(6, (int)r["links"]!);
        var back = Schedule(g);
        // The winter break is Lapin's own (MS Project has no wet tasks): the wet task's dates aside, all the same.
        Assert.Equal("ro", (string)back["calendar"]!["holidays"]!);
        var g2 = F();
        var (f2, _) = Sample();
        f2.Run("planning.setCalendar", new JsonObject { ["winter"] = new JsonObject { ["on"] = false } });
        var xml2 = Encoding.UTF8.GetString(Convert.FromBase64String((string)f2.Session.Query("planning.exportMsp", new JsonObject())!));
        g2.Run("planning.importMsp", new JsonObject { ["xml"] = xml2 });
        Assert.Equal(Essence(Schedule(f2)), Essence(Schedule(g2)));
        Assert.Equal("Bloc P+4", (string)Schedule(g2)["name"]!);
    }

    [Fact]
    public void A_schedule_written_by_another_program_is_read_with_its_outline_lags_and_calendar()
    {
        // MS Project 2016's form: no GUIDs needed, 2007-style exceptions, a lag in elapsed tenths of minutes.
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Project xmlns="http://schemas.microsoft.com/project">
              <Name>Casa.xml</Name><StartDate>2027-03-01T08:00:00</StartDate><CalendarUID>1</CalendarUID><MinutesPerDay>480</MinutesPerDay>
              <Calendars><Calendar><UID>1</UID><Name>Standard</Name><IsBaseCalendar>1</IsBaseCalendar>
                <WeekDays>
                  <WeekDay><DayType>1</DayType><DayWorking>0</DayWorking></WeekDay>
                  <WeekDay><DayType>2</DayType><DayWorking>1</DayWorking><WorkingTimes><WorkingTime><FromTime>07:00:00</FromTime><ToTime>15:00:00</ToTime></WorkingTime></WorkingTimes></WeekDay>
                  <WeekDay><DayType>3</DayType><DayWorking>1</DayWorking></WeekDay><WeekDay><DayType>4</DayType><DayWorking>1</DayWorking></WeekDay>
                  <WeekDay><DayType>5</DayType><DayWorking>1</DayWorking></WeekDay><WeekDay><DayType>6</DayType><DayWorking>1</DayWorking></WeekDay>
                  <WeekDay><DayType>7</DayType><DayWorking>0</DayWorking></WeekDay>
                </WeekDays>
                <Exceptions><Exception><Name>Inventar</Name><TimePeriod><FromDate>2027-03-04T00:00:00</FromDate><ToDate>2027-03-05T23:59:00</ToDate></TimePeriod><DayWorking>0</DayWorking></Exception></Exceptions>
              </Calendar></Calendars>
              <Tasks>
                <Task><UID>0</UID><ID>0</ID><Name>Casa</Name><OutlineLevel>0</OutlineLevel><Summary>1</Summary></Task>
                <Task><UID>7</UID><ID>1</ID><Name>Fundații</Name><OutlineLevel>1</OutlineLevel><Summary>1</Summary></Task>
                <Task><UID>8</UID><ID>2</ID><Name>Săpătură</Name><OutlineLevel>2</OutlineLevel><Duration>PT16H0M0S</Duration></Task>
                <Task><UID>9</UID><ID>3</ID><Name>Turnare</Name><OutlineLevel>2</OutlineLevel><Duration>PT24H0M0S</Duration>
                  <PredecessorLink><PredecessorUID>8</PredecessorUID><Type>1</Type><LinkLag>4800</LinkLag><LagFormat>7</LagFormat></PredecessorLink></Task>
                <Task><UID>10</UID><ID>4</ID><Name>Recepție</Name><OutlineLevel>1</OutlineLevel><Duration>PT0H0M0S</Duration><Milestone>1</Milestone>
                  <PredecessorLink><PredecessorUID>7</PredecessorUID><Type>1</Type></PredecessorLink></Task>
              </Tasks>
            </Project>
            """;
        var f = F();
        f.Run("planning.importMsp", new JsonObject { ["xml"] = xml });
        var s = Schedule(f);
        Assert.Equal("Casa", (string)s["name"]!);
        Assert.Equal(["1", "1.1", "1.2", "2"], s["tasks"]!.AsArray().Select(t => (string)t!["wbs"]!));
        Assert.Equal("07:00", (string)s["calendar"]!["dayStart"]!);
        // Săpătură Mon 1–Tue 2.3; a day's lag (Wed 3); the inventory Thu–Fri; Turnare Mon 8–Wed 10; Recepție Thu 11.
        var rows = s["tasks"]!.AsArray();
        Assert.Equal(("2027-03-01", "2027-03-02"), ((string)rows[1]!["start"]!, (string)rows[1]!["finish"]!));
        Assert.Equal(("2027-03-08", "2027-03-10"), ((string)rows[2]!["start"]!, (string)rows[2]!["finish"]!));
        Assert.True((bool)rows[3]!["milestone"]!);
        Assert.Equal("2027-03-11", (string)rows[3]!["start"]!);
    }

    [Fact]
    public void The_schedule_is_exported_as_a_workbook_and_its_durations_read_from_IFC()
    {
        var (f, _) = Sample();
        var bytes = Convert.FromBase64String((string)f.Session.Query("planning.exportXlsx", new JsonObject())!);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        Assert.Contains(zip.Entries, e => e.FullName == "xl/worksheets/sheet1.xml");
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var sheet = reader.ReadToEnd();
        Assert.Contains("Structure GF", sheet);
        Assert.Contains("Total float", sheet);
        Assert.Equal(5, IfcImport.DurationDays("P5D"));
        Assert.Equal(5, IfcImport.DurationDays("PT40H"));
        Assert.Equal(2.5, IfcImport.DurationDays("P2DT4H"));
        Assert.Equal(5, IfcImport.DurationDays("P1W"));
        Assert.Equal(-2, IfcImport.DurationDays("-P2D"));
        Assert.Null(IfcImport.DurationDays("five days"));
    }
}
