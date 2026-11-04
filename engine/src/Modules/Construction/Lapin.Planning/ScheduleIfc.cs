using System.Globalization;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// The work schedule in IFC 4 (docs/plan-4d5d.md, Schimbul): the module gives the export its schedule, calendar, tasks
/// with their times as the critical path works them out, and sequences; and makes its own schedule of what an import read.
/// Lapin's fields IFC has no place for (a task's constraint, whether it is wet; the calendar's holiday rule and winter
/// break) go as the property sets <c>Lapin_Task</c> and <c>Lapin_Calendar</c>.
/// </summary>
public sealed class ScheduleIfc : IIfcProcesses
{
    public const string TaskSet = "Lapin_Task", CalendarSet = "Lapin_Calendar";

    static string SequenceType(string t) => t switch { "SS" => "START_START", "FF" => "FINISH_FINISH", "SF" => "START_FINISH", _ => "FINISH_START" };
    static string LinkType(string t) => t switch { "START_START" => "SS", "FINISH_FINISH" => "FF", "START_FINISH" => "SF", _ => "FS" };

    public IEnumerable<IfcScheduleData> Schedules(IModelView model)
    {
        var p = Plan.Read(model);
        if (p.Schedule == null) yield break;
        CpmResult r;
        try { r = Cpm.Solve(p); }
        catch (CommandException) { r = new CpmResult(new Dictionary<string, TaskTimes>(), p.Start, new HashSet<string>()); }
        var cal = p.Calendar;
        var dayStart = TimeOnly.ParseExact(cal.DayStart, "HH:mm", CultureInfo.InvariantCulture);
        var dayEnd = dayStart.AddMinutes(cal.Hours * 60);
        DateTime At(DateOnly d, TimeOnly t) => d.ToDateTime(t);
        var finish = r.Times.Count > 0 ? r.Times.Values.Max(t => t.Finish) : p.Start;
        var from = new DateOnly(p.Start.Year, 1, 1);
        var to = new DateOnly(finish.Year + 1, 12, 31);
        IfcCalendarData? calendar = p.CalendarEntity is { } ce
            ? new IfcCalendarData(ce.Id, ce.Name, cal.Days, dayStart, dayEnd,
                cal.NonWorking(from, to).Select(x => (x.Day, x.Day, x.Why)).ToList(), CalendarSet,
                [("Holidays", cal.Holidays), ("WinterBreak", cal.Winter ? "true" : "false"), ("WinterFrom", cal.WinterFrom), ("WinterTo", cal.WinterTo),
                 ("Exceptions", cal.ToJson()["exceptions"]!.ToJsonString())])
            : null;
        var tasks = new List<IfcTaskData>();
        foreach (var t in p.Rows)
        {
            var tm = r.Times.GetValueOrDefault(t.Id);
            var milestone = !p.IsSummary(t.Id) && (t.Milestone || t.Duration == 0);
            IfcTaskTimeData? time = tm == null ? null : new IfcTaskTimeData(milestone ? 0 : tm.Duration,
                At(tm.Start, dayStart), At(tm.Finish, milestone ? dayStart : dayEnd),
                At(tm.LateStart, dayStart), At(tm.LateFinish > tm.LateStart ? tm.LateFinish.AddDays(-1) : tm.LateStart, milestone ? dayStart : dayEnd),
                tm.FreeFloat, tm.TotalFloat, tm.Critical);
            var props = new List<(string, string)>();
            if (t.Constraint != null && t.ConstraintDate is { } cd) { props.Add(("Constraint", t.Constraint)); props.Add(("ConstraintDate", WorkCalendar.Date(cd))); }
            if (t.Wet) props.Add(("Wet", "true"));
            props.Add(("Duration", $"{(milestone ? 0 : t.Duration)}"));
            tasks.Add(new IfcTaskData(t.Id, t.Name, t.Notes, p.Wbs[t.Id], t.Parent is { } par && p.ById.ContainsKey(par) ? par : null, milestone, t.Kind, time, TaskSet, props));
        }
        var sequences = p.Links.Select(l => new IfcSequenceData(l.Id, l.From, l.To, SequenceType(l.Type), l.Lag)).ToList();
        yield return new IfcScheduleData(p.Schedule.Id, p.Name, At(p.Start, dayStart), At(finish, dayEnd), "PLANNED", calendar, tasks, sequences);
    }

    public IfcProcessImport Import(IEdit edit, IReadOnlyList<IfcScheduleData> schedules)
    {
        // One schedule a project: the file's first (the one with the most tasks when it has several).
        var s = schedules.OrderByDescending(x => x.Tasks.Count).FirstOrDefault();
        if (s == null || s.Tasks.Count == 0) return new IfcProcessImport(0, 0, 0, []);
        var notes = new List<string>();
        if (schedules.Count > 1) notes.Add($"The file has {schedules.Count} work schedules: the one of {s.Tasks.Count} tasks came in.");
        var calendar = Calendar(s.Calendar);
        var hours = calendar.Hours;
        var tasks = new List<ImportedTask>();
        foreach (var t in s.Tasks)
        {
            string? Prop(string name) => t.Properties.FirstOrDefault(x => x.Name == name).Value;
            int duration;
            if (int.TryParse(Prop("Duration"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var own)) duration = own;
            else if (t.Time?.DurationDays is { } dd) duration = (int)Math.Round(dd);
            else if (t.Time?.ScheduleStart is { } a && t.Time.ScheduleFinish is { } b)
                duration = calendar.WorkingDays(DateOnly.FromDateTime(a), DateOnly.FromDateTime(b).AddDays(1));
            else duration = t.IsMilestone ? 0 : 1;
            var constraint = Prop("Constraint") is { } c && Plan.Constraints.Contains(c) ? c : null;
            var cdate = WorkCalendar.ParseDate(Prop("ConstraintDate"));
            tasks.Add(new ImportedTask(t.Id, t.Id, t.Name, t.Parent, t.IsMilestone ? 0 : Math.Max(0, duration), t.IsMilestone, cdate != null ? constraint : null, cdate,
                t.Time?.ScheduleStart is { } st ? DateOnly.FromDateTime(st) : null, Prop("Wet") == "true", t.Predefined, t.Description));
        }
        // Parents before their children, siblings in the file's order.
        var byKey = tasks.ToDictionary(t => t.Key);
        var ordered = new List<ImportedTask>();
        var placed = new HashSet<string>();
        var kids = tasks.Where(t => t.ParentKey != null && byKey.ContainsKey(t.ParentKey)).GroupBy(t => t.ParentKey!).ToDictionary(g => g.Key, g => g.ToList());
        void Place(ImportedTask t)
        {
            if (!placed.Add(t.Key)) return;
            ordered.Add(t);
            foreach (var k in kids.GetValueOrDefault(t.Key) ?? []) Place(k);
        }
        foreach (var t in tasks.Where(t => t.ParentKey == null || !byKey.ContainsKey(t.ParentKey))) Place(t);
        foreach (var t in tasks) Place(t);   // whatever a loop of parents left out
        var links = s.Sequences.Select(q => new ImportedLink(q.Relating, q.Related, LinkType(q.Type), (int)Math.Round(q.LagDays), q.Id)).ToList();
        var calendarId = s.Calendar?.Id is { } cid && GlobalId.IsValid(cid) ? cid : null;
        var imported = new ImportedSchedule(s.Name, DateOnly.FromDateTime(s.Start), calendar, ordered, links,
            GlobalId.IsValid(s.Id) ? s.Id : null, calendarId, s.Calendar?.Name);
        var r = ScheduleImport.Apply(edit, imported);
        notes.AddRange(r.Notes);
        return new IfcProcessImport(1, r.Tasks, r.Links, notes);
    }

    /// <summary>The calendar a file gave: Lapin's own rules back when it wrote them, else its weekdays and hours with the days it lists not worked.</summary>
    static WorkCalendar Calendar(IfcCalendarData? c)
    {
        if (c == null) return WorkCalendar.Default;
        var days = c.Weekdays.Where(d => d is >= 1 and <= 7).Distinct().Order().ToList();
        if (days.Count == 0) days = [1, 2, 3, 4, 5];
        var hours = (c.DayEnd - c.DayStart).TotalHours;
        if (hours <= 0 || hours > 24) hours = 8;
        var start = c.DayStart.ToString("HH:mm", CultureInfo.InvariantCulture);
        string? Prop(string name) => c.Properties.FirstOrDefault(x => x.Name == name).Value;
        if (c.PropertySet == CalendarSet && Prop("Holidays") is { } holidays)
        {
            var o = WorkCalendar.Default.ToJson();
            o["days"] = new System.Text.Json.Nodes.JsonArray(days.Select(d => (System.Text.Json.Nodes.JsonNode)d).ToArray());
            o["dayStart"] = start;
            o["hours"] = hours;
            o["holidays"] = holidays;
            if (Prop("Exceptions") is { } xs) try { o["exceptions"] = System.Text.Json.Nodes.JsonNode.Parse(xs); } catch (System.Text.Json.JsonException) { }
            o["winter"] = new System.Text.Json.Nodes.JsonObject { ["on"] = Prop("WinterBreak") == "true", ["from"] = Prop("WinterFrom") ?? "12-15", ["to"] = Prop("WinterTo") ?? "03-01" };
            return WorkCalendar.Read(o);
        }
        return WorkCalendar.FromExplicit(days, start, hours, c.Exceptions.Select(x => new CalendarException(x.From, x.To, false, x.Name)).ToList());
    }
}
