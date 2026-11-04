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

    public IEnumerable<IfcScheduleData> Schedules(IQueryContext query)
    {
        var p = Plan.Read(query.Model);
        if (p.Schedule == null) yield break;
        var links = p.Rows.Any(t => t.Links) ? TaskElements.Resolve(query, p) : null;
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
            // Its elements: the ones it takes now, as IFC's assignments; how it finds them (rule, part, exceptions) in the set, so Lapin reads the rule back.
            if (t.Rule != null) props.Add(("Rule", t.Rule.ToJson().ToJsonString()));
            if (t.Part != null) props.Add(("Part", t.Part));
            if (t.Add.Count > 0) props.Add(("Add", new System.Text.Json.Nodes.JsonArray(t.Add.Select(x => (System.Text.Json.Nodes.JsonNode)x).ToArray()).ToJsonString()));
            if (t.Remove.Count > 0) props.Add(("Remove", new System.Text.Json.Nodes.JsonArray(t.Remove.Select(x => (System.Text.Json.Nodes.JsonNode)x).ToArray()).ToJsonString()));
            List<string>? elements = links?.ByTask.TryGetValue(t.Id, out var own) == true ? [.. own.Keys] : null;
            var acts = t.Kind is "DEMOLITION" or "REMOVAL" or "DISMANTLE" or "DISPOSAL";
            tasks.Add(new IfcTaskData(t.Id, t.Name, t.Notes, p.Wbs[t.Id], t.Parent is { } par && p.ById.ContainsKey(par) ? par : null, milestone, t.Kind, time, TaskSet, props,
                acts ? null : elements, acts ? elements : null));
        }
        var sequences = p.Links.Select(l => new IfcSequenceData(l.Id, l.From, l.To, SequenceType(l.Type), l.Lag)).ToList();
        yield return new IfcScheduleData(p.Schedule.Id, p.Name, At(p.Start, dayStart), At(finish, dayEnd), "PLANNED", calendar, tasks, sequences);
    }

    public IfcProcessImport Import(IEdit edit, IReadOnlyList<IfcScheduleData> schedules) => Import(edit, schedules, id => edit.Model.Get(id) != null ? id : null);

    public IfcProcessImport Import(IEdit edit, IReadOnlyList<IfcScheduleData> schedules, Func<string, string?> idOf)
    {
        // One schedule a project: the file's first (the one with the most tasks when it has several).
        var s = schedules.OrderByDescending(x => x.Tasks.Count).FirstOrDefault();
        if (s == null || s.Tasks.Count == 0) return new IfcProcessImport(0, 0, 0, []);
        var notes = new List<string>();
        if (schedules.Count > 1) notes.Add($"The file has {schedules.Count} work schedules: the one of {s.Tasks.Count} tasks came in.");
        var calendar = Calendar(s.Calendar);
        var hours = calendar.Hours;
        var tasks = new List<ImportedTask>();
        var missing = 0;
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
                t.Time?.ScheduleStart is { } st ? DateOnly.FromDateTime(st) : null, Prop("Wet") == "true", t.Predefined, t.Description, LinksOf(idOf, t, Prop, ref missing)));
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
        if (missing > 0) notes.Add($"{missing} element(s) assigned to tasks in the file are not in the project: left out of their tasks.");
        return new IfcProcessImport(1, r.Tasks, r.Links, notes);
    }

    /// <summary>
    /// A task's elements as a file gives them: Lapin's own rule, part and exceptions when it wrote them (Lapin_Task), else the
    /// elements the file assigns to it (its outputs and inputs) that the project has, added by hand.
    /// </summary>
    static System.Text.Json.Nodes.JsonObject? LinksOf(Func<string, string?> idOf, IfcTaskData t, Func<string, string?> prop, ref int missing)
    {
        static System.Text.Json.Nodes.JsonArray Ids(IEnumerable<string> ids) => new(ids.Select(x => (System.Text.Json.Nodes.JsonNode)x).ToArray());
        if (prop("Rule") != null || prop("Part") != null || prop("Add") != null || prop("Remove") != null)
        {
            var o = new System.Text.Json.Nodes.JsonObject();
            System.Text.Json.Nodes.JsonNode? Json(string? text) { try { return text == null ? null : System.Text.Json.Nodes.JsonNode.Parse(text); } catch (System.Text.Json.JsonException) { return null; } }
            List<string> Strings(System.Text.Json.Nodes.JsonNode? n) => n is System.Text.Json.Nodes.JsonArray a ? a.Select(x => (string?)x).OfType<string>().ToList() : [];
            // The ids the rule names (storeys, types, materials, groups) as the project has them now; an element no longer there dropped.
            if (Json(prop("Rule")) is System.Text.Json.Nodes.JsonObject rule)
            {
                foreach (var key in TaskRule.Keys.Where(k => k != "classes" && k != "phases"))
                    if (rule[key] is System.Text.Json.Nodes.JsonArray) rule[key] = Ids(Strings(rule[key]).Select(id => idOf(id) ?? id).Distinct());
                o["rule"] = rule;
            }
            if (prop("Part") is { } part) o["part"] = part.StartsWith(TaskParts.MaterialPrefix, StringComparison.Ordinal) && idOf(part[TaskParts.MaterialPrefix.Length..]) is { } mat ? TaskParts.MaterialPrefix + mat : part;
            foreach (var key in new[] { "Add", "Remove" })
            {
                var given = Strings(Json(prop(key)));
                var found = given.Select(idOf).OfType<string>().Distinct().ToList();
                missing += given.Count - found.Count;
                if (found.Count > 0) o[key.ToLowerInvariant()] = Ids(found);
            }
            return o;
        }
        var ids = (t.Outputs ?? []).Concat(t.Inputs ?? []).Distinct().ToList();
        if (ids.Count == 0) return null;
        var elements = ids.Select(idOf).OfType<string>().Distinct().ToList();
        missing += ids.Count - elements.Count;
        return elements.Count == 0 ? null : new System.Text.Json.Nodes.JsonObject { ["add"] = Ids(elements) };
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
