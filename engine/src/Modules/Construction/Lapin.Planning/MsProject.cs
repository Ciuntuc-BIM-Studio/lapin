using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// MS Project's XML (MSPDI, written after Microsoft's public schema, no library): the project's start and calendar (its
/// weekdays, working hours and the days not worked as exceptions), the tasks in row order with their WBS, outline level,
/// dates, duration, milestone and summary flags, constraint, floats and critical flag, their GUID (Lapin's GlobalId), and
/// the predecessor links with their type and lag; the progress (stage 5: the status date, each task's percentage, actual
/// start and finish, the duration left) and the baseline (Baseline 0). MS Project, ProjectLibre and GanttProject read it;
/// Primavera P6 imports it.
/// Read back, a schedule written here gives the same tasks, links, ids and dates.
/// </summary>
public static class MsProject
{
    const string Ns = "http://schemas.microsoft.com/project";

    /// <summary>MS Project's custom field Text1 (its field id), which carries a task's elements under the name <see cref="LinksAlias"/>.</summary>
    const string LinksField = "188743731", LinksAlias = "Lapin elements";

    static string Dt(DateOnly d, string time) => $"{WorkCalendar.Date(d)}T{time}:00";

    static string Time(TimeOnly t) => t.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>A duration in MSPDI's form: hours, minutes and seconds of work (PT40H0M0S for five days of eight hours).</summary>
    static string Duration(int days, double hours)
    {
        var minutes = (long)Math.Round(days * hours * 60);
        return $"PT{minutes / 60}H{minutes % 60}M0S";
    }

    public static string Write(Plan p, CpmResult r)
    {
        var cal = p.Calendar;
        var dayStart = TimeOnly.ParseExact(cal.DayStart, "HH:mm", CultureInfo.InvariantCulture);
        var dayEnd = dayStart.AddMinutes(cal.Hours * 60);
        var (startTime, endTime) = (dayStart.ToString("HH:mm", CultureInfo.InvariantCulture), dayEnd.ToString("HH:mm", CultureInfo.InvariantCulture));
        var minutesPerDay = (int)Math.Round(cal.Hours * 60);
        var finish = r.Times.Count > 0 ? r.Times.Values.Max(t => t.Finish) : p.Start;

        var sb = new StringBuilder();
        using var w = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true });
        w.WriteStartElement("Project", Ns);
        void E(string name, string value) => w.WriteElementString(name, Ns, value);
        E("SaveVersion", "14");
        E("Name", p.Name);
        E("Title", p.Name);
        E("ScheduleFromStart", "1");
        E("StartDate", Dt(p.Start, startTime));
        E("FinishDate", Dt(finish, endTime));
        E("CalendarUID", "1");
        E("DefaultStartTime", Time(dayStart));
        E("DefaultFinishTime", Time(dayEnd));
        E("MinutesPerDay", $"{minutesPerDay}");
        E("MinutesPerWeek", $"{minutesPerDay * cal.Days.Count}");
        E("DaysPerMonth", "20");
        E("DurationFormat", "7");
        if (p.DataDate is { } statusDate) E("StatusDate", Dt(statusDate, endTime));

        // The tasks' elements (their rule, part and exceptions) in the custom field Text1, named so (stage 2).
        var withLinks = p.Rows.Any(t => TaskLinkData.Of(t) != null);
        if (withLinks)
        {
            w.WriteStartElement("ExtendedAttributes", Ns);
            w.WriteStartElement("ExtendedAttribute", Ns);
            E("FieldID", LinksField);
            E("FieldName", "Text1");
            E("Alias", LinksAlias);
            w.WriteEndElement();
            w.WriteEndElement();
        }

        // The calendar: Sunday is DayType 1 … Saturday 7; each day not worked between the schedule's first and last years an exception (DayType 0).
        w.WriteStartElement("Calendars", Ns);
        w.WriteStartElement("Calendar", Ns);
        E("UID", "1");
        E("Name", p.CalendarEntity?.Name ?? "Standard");
        E("IsBaseCalendar", "1");
        E("BaseCalendarUID", "-1");
        w.WriteStartElement("WeekDays", Ns);
        for (var type = 1; type <= 7; type++)
        {
            var iso = type == 1 ? 7 : type - 1;
            w.WriteStartElement("WeekDay", Ns);
            E("DayType", $"{type}");
            var works = cal.Days.Contains(iso);
            E("DayWorking", works ? "1" : "0");
            if (works)
            {
                w.WriteStartElement("WorkingTimes", Ns);
                w.WriteStartElement("WorkingTime", Ns);
                E("FromTime", Time(dayStart));
                E("ToTime", Time(dayEnd));
                w.WriteEndElement();
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        var from = new DateOnly(p.Start.Year, 1, 1);
        var to = new DateOnly(Math.Max(finish.Year, p.Start.Year) + 1, 12, 31);
        foreach (var x in cal.Exceptions.Where(x => x.Working))
        {
            w.WriteStartElement("WeekDay", Ns);
            E("DayType", "0");
            E("DayWorking", "1");
            w.WriteStartElement("TimePeriod", Ns);
            E("FromDate", Dt(x.From, "00:00"));
            E("ToDate", Dt(x.To, "23:59"));
            w.WriteEndElement();
            w.WriteStartElement("WorkingTimes", Ns);
            w.WriteStartElement("WorkingTime", Ns);
            E("FromTime", Time(dayStart));
            E("ToTime", Time(dayEnd));
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        }
        foreach (var (day, _) in cal.NonWorking(from, to))
        {
            w.WriteStartElement("WeekDay", Ns);
            E("DayType", "0");
            E("DayWorking", "0");
            w.WriteStartElement("TimePeriod", Ns);
            E("FromDate", Dt(day, "00:00"));
            E("ToDate", Dt(day, "23:59"));
            w.WriteEndElement();
            w.WriteEndElement();
        }
        w.WriteEndElement();   // WeekDays
        w.WriteEndElement();   // Calendar
        w.WriteEndElement();   // Calendars

        w.WriteStartElement("Tasks", Ns);
        // The project's own summary, task 0, as MS Project writes it.
        w.WriteStartElement("Task", Ns);
        E("UID", "0");
        E("ID", "0");
        E("Name", p.Name);
        E("Type", "1");
        E("IsNull", "0");
        E("OutlineNumber", "0");
        E("OutlineLevel", "0");
        E("Start", Dt(p.Start, startTime));
        E("Finish", Dt(finish, endTime));
        E("Summary", "1");
        w.WriteEndElement();

        var uid = new Dictionary<string, int>();
        foreach (var t in p.Rows) uid[t.Id] = p.RowOf[t.Id];
        var incoming = p.Links.GroupBy(l => l.To).ToDictionary(g => g.Key, g => g.ToList());
        long Tenths(int days) => (long)Math.Round(days * cal.Hours * 600);
        foreach (var t in p.Rows)
        {
            var tm = r.Times.GetValueOrDefault(t.Id);
            var summary = p.IsSummary(t.Id);
            var milestone = !summary && (t.Milestone || t.Duration == 0);
            // A task's own duration (one under way keeps it: what is left is its RemainingDuration); a summary's from its tasks.
            var dur = summary ? tm?.Duration ?? t.Duration : t.Duration;
            w.WriteStartElement("Task", Ns);
            E("UID", $"{uid[t.Id]}");
            if (GlobalId.IsValid(t.Id)) E("GUID", GlobalId.ToGuid(t.Id).ToString("D").ToUpperInvariant());
            E("ID", $"{uid[t.Id]}");
            E("Name", t.Name);
            E("Type", "1");
            E("IsNull", "0");
            E("WBS", p.Wbs[t.Id]);
            E("OutlineNumber", p.Wbs[t.Id]);
            E("OutlineLevel", $"{p.Level[t.Id] + 1}");
            if (tm != null)
            {
                E("Start", Dt(tm.Start, startTime));
                E("Finish", Dt(tm.Finish, milestone ? startTime : endTime));
            }
            E("Duration", Duration(milestone ? 0 : dur, cal.Hours));
            E("DurationFormat", "7");
            E("Milestone", milestone ? "1" : "0");
            E("Summary", summary ? "1" : "0");
            if (tm != null)
            {
                E("Critical", tm.Critical ? "1" : "0");
                E("EarlyStart", Dt(tm.EarlyStart, startTime));
                E("EarlyFinish", Dt(tm.Finish, milestone ? startTime : endTime));
                E("LateStart", Dt(tm.LateStart, startTime));
                E("LateFinish", Dt(tm.LateFinish > tm.LateStart ? tm.LateFinish.AddDays(-1) : tm.LateStart, milestone ? startTime : endTime));
                E("FreeSlack", $"{Tenths(tm.FreeFloat)}");
                E("TotalSlack", $"{Tenths(tm.TotalFloat)}");
            }
            var pr = p.ProgressOf(t.Id);
            E("PercentComplete", $"{(int)Math.Round(pr.Complete)}");
            if (pr.ActualStart is { } ast) E("ActualStart", Dt(ast, startTime));
            if (pr.ActualFinish is { } afi) E("ActualFinish", Dt(afi, milestone ? startTime : endTime));
            if (tm != null && tm.Status == "started") E("RemainingDuration", Duration(tm.Remaining, cal.Hours));
            // ConstraintType: 0 as soon as possible, 2 must start on, 4 start no earlier than.
            E("ConstraintType", t.Constraint switch { "mso" => "2", "snet" => "4", _ => "0" });
            if (t.Constraint != null && t.ConstraintDate is { } cd) E("ConstraintDate", Dt(cd, startTime));
            if (!string.IsNullOrWhiteSpace(t.Notes)) E("Notes", t.Notes!);
            foreach (var l in incoming.GetValueOrDefault(t.Id) ?? [])
            {
                if (!uid.TryGetValue(l.From, out var pred)) continue;
                w.WriteStartElement("PredecessorLink", Ns);
                E("PredecessorUID", $"{pred}");
                // Type: 0 FF, 1 FS, 2 SF, 3 SS; the lag in tenths of a minute, shown in days (format 7).
                E("Type", l.Type switch { "FF" => "0", "SF" => "2", "SS" => "3", _ => "1" });
                E("CrossProject", "0");
                E("LinkLag", $"{Tenths(l.Lag)}");
                E("LagFormat", "7");
                w.WriteEndElement();
            }
            if (TaskLinkData.Of(t) is { } links)
            {
                w.WriteStartElement("ExtendedAttribute", Ns);
                E("FieldID", LinksField);
                E("Value", links.ToJsonString());
                w.WriteEndElement();
            }
            if (t.Baseline is { } bl)
            {
                w.WriteStartElement("Baseline", Ns);
                E("Number", "0");
                E("Start", Dt(bl.Start, startTime));
                E("Finish", Dt(bl.Finish, bl.Duration == 0 ? startTime : endTime));
                E("Duration", Duration(bl.Duration, cal.Hours));
                E("DurationFormat", "7");
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();   // Tasks
        w.WriteEndElement();   // Project
        w.Flush();
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" + sb;
    }

    // ------------------------------------------------------------------ reading

    static XElement? El(XElement? e, string name) => e?.Elements().FirstOrDefault(x => x.Name.LocalName == name);
    static IEnumerable<XElement> Els(XElement? e, string name) => e?.Elements().Where(x => x.Name.LocalName == name) ?? [];
    static string? Val(XElement? e, string name) => El(e, name)?.Value.Trim();
    static int Int(XElement? e, string name, int fallback = 0) => int.TryParse(Val(e, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : fallback;
    static double Num(XElement? e, string name, double fallback = 0) => double.TryParse(Val(e, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fallback;

    static DateTime? When(string? s) =>
        s != null && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var d) ? d : null;

    /// <summary>MSPDI's duration (PT40H0M0S, PT8H, P2DT4H…) in hours of work.</summary>
    public static double Hours(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        try { return XmlConvert.ToTimeSpan(s.Trim()).TotalHours; }
        catch (FormatException) { return 0; }
    }

    /// <summary>A file's schedule: its calendar, its tasks (task 0 and empty rows left out) with their hierarchy from the outline levels, and their links.</summary>
    public static ImportedSchedule Read(string xml)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch (XmlException ex) { throw new CommandException($"This is not an MS Project XML file: {ex.Message}"); }
        var project = doc.Root;
        if (project == null || project.Name.LocalName != "Project") throw new CommandException("This is not an MS Project XML file (it has no Project).");
        var minutesPerDay = Num(project, "MinutesPerDay", 480);
        if (minutesPerDay <= 0) minutesPerDay = 480;
        var hoursPerDay = minutesPerDay / 60;

        // The project's calendar (else the first base calendar).
        var calendars = Els(El(project, "Calendars"), "Calendar").ToList();
        var calUid = Val(project, "CalendarUID");
        var calendar = calendars.FirstOrDefault(c => Val(c, "UID") == calUid) ?? calendars.FirstOrDefault(c => Val(c, "IsBaseCalendar") == "1") ?? calendars.FirstOrDefault();
        var days = new List<int>();
        var exceptions = new List<CalendarException>();
        var dayStart = "08:00";
        if (calendar != null)
        {
            foreach (var wd in Els(El(calendar, "WeekDays"), "WeekDay"))
            {
                var type = Int(wd, "DayType", -1);
                var working = Val(wd, "DayWorking") == "1";
                if (type is >= 1 and <= 7)
                {
                    if (working) days.Add(type == 1 ? 7 : type - 1);
                    if (working && El(El(El(wd, "WorkingTimes"), "WorkingTime"), "FromTime")?.Value is { } ft && TimeOnly.TryParse(ft, CultureInfo.InvariantCulture, out var t0))
                        dayStart = t0.ToString("HH:mm", CultureInfo.InvariantCulture);
                }
                else if (type == 0 && El(wd, "TimePeriod") is { } tp && When(Val(tp, "FromDate")) is { } f && When(Val(tp, "ToDate")) is { } tt)
                    exceptions.Add(new(DateOnly.FromDateTime(f), DateOnly.FromDateTime(tt), working, ""));
            }
            // MS Project 2007 and later: the exceptions apart.
            foreach (var x in Els(El(calendar, "Exceptions"), "Exception"))
                if (El(x, "TimePeriod") is { } tp && When(Val(tp, "FromDate")) is { } f && When(Val(tp, "ToDate")) is { } tt)
                    exceptions.Add(new(DateOnly.FromDateTime(f), DateOnly.FromDateTime(tt), Val(x, "DayWorking") == "1", Val(x, "Name") ?? ""));
        }
        if (days.Count == 0) days = [1, 2, 3, 4, 5];
        var cal = WorkCalendar.FromExplicit(days.Distinct().Order().ToList(), dayStart, hoursPerDay, exceptions);

        var start = When(Val(project, "StartDate")) is { } ps ? DateOnly.FromDateTime(ps) : Plan.NextMonday(Plan.Today());
        var rows = Els(El(project, "Tasks"), "Task")
            .Where(t => Val(t, "IsNull") != "1" && Int(t, "UID", -1) > 0 && Int(t, "OutlineLevel", 1) >= 1)
            .OrderBy(t => Int(t, "ID", int.MaxValue)).ThenBy(t => Int(t, "UID")).ToList();
        var tasks = new List<ImportedTask>();
        // The field Lapin wrote the tasks' elements in (by its name; Text1 when the file has the field without it).
        var fields = Els(El(project, "ExtendedAttributes"), "ExtendedAttribute").ToList();
        var linksField = fields.FirstOrDefault(x => Val(x, "Alias") == LinksAlias) is { } fa ? Val(fa, "FieldID") : fields.Count == 0 ? LinksField : null;
        var stack = new List<(int Level, string Key)>();
        var summaryKeys = new HashSet<string>();
        foreach (var t in rows)
        {
            var key = Val(t, "UID")!;
            var level = Math.Max(1, Int(t, "OutlineLevel", 1));
            while (stack.Count > 0 && stack[^1].Level >= level) stack.RemoveAt(stack.Count - 1);
            var parent = stack.Count > 0 ? stack[^1].Key : null;
            stack.Add((level, key));
            if (Val(t, "Summary") == "1") summaryKeys.Add(key);
            var milestone = Val(t, "Milestone") == "1";
            var hours = Hours(Val(t, "Duration"));
            var duration = milestone ? 0 : (int)Math.Round(hours / hoursPerDay);
            if (!milestone && duration == 0 && hours > 0) duration = 1;
            var constraint = Int(t, "ConstraintType") switch { 2 => "mso", 4 => "snet", _ => null };
            var constraintDate = When(Val(t, "ConstraintDate")) is { } cd ? DateOnly.FromDateTime(cd) : (DateOnly?)null;
            if (constraintDate == null) constraint = null;
            string? id = Guid.TryParse(Val(t, "GUID"), out var g) ? GlobalId.FromGuid(g) : null;
            var elementLinks = linksField == null ? null : TaskLinkData.Parse(Els(t, "ExtendedAttribute").FirstOrDefault(x => Val(x, "FieldID") == linksField) is { } xa ? Val(xa, "Value") : null);
            // Its progress, unless its elements' marks give it (Lapin wrote them with the elements); a summary's comes from its tasks.
            DateOnly? Day(string name) => When(Val(t, name)) is { } x ? DateOnly.FromDateTime(x) : null;
            var marked = elementLinks?["marks"] is System.Text.Json.Nodes.JsonObject { Count: > 0 };
            var summary = Val(t, "Summary") == "1";
            double? pc = !marked && !summary && Num(t, "PercentComplete", -1) is var pcv && pcv > 0 ? Math.Min(100, pcv) : null;
            var actualStart = marked || summary ? null : Day("ActualStart");
            var actualFinish = marked || summary ? null : Day("ActualFinish");
            // MS Project gives a finished task 100 % and its actual finish: the finish alone says so.
            if (actualFinish != null && pc >= 100) pc = null;
            TaskBaseline? baseline = null;
            if (Els(t, "Baseline").FirstOrDefault(b => Int(b, "Number") == 0) is { } bx && When(Val(bx, "Start")) is { } bs && When(Val(bx, "Finish")) is { } bf)
                baseline = new TaskBaseline(DateOnly.FromDateTime(bs), DateOnly.FromDateTime(bf), (int)Math.Round(Hours(Val(bx, "Duration")) / hoursPerDay));
            tasks.Add(new ImportedTask(key, id, Val(t, "Name") ?? "", parent, duration, milestone && duration == 0, constraint, constraintDate,
                When(Val(t, "Start")) is { } s ? DateOnly.FromDateTime(s) : null, Notes: Val(t, "Notes"), Links: elementLinks,
                ActualStart: actualStart, ActualFinish: actualFinish, Complete: pc, Baseline: baseline));
        }
        var links = new List<ImportedLink>();
        foreach (var t in rows)
            foreach (var pl in Els(t, "PredecessorLink"))
            {
                var type = Int(pl, "Type", 1) switch { 0 => "FF", 2 => "SF", 3 => "SS", _ => "FS" };
                // LinkLag is in tenths of a minute (an elapsed or percentage lag read as work time, its days rounded).
                var lag = (int)Math.Round(Num(pl, "LinkLag") / 10 / minutesPerDay);
                if (Val(pl, "PredecessorUID") is { } pred) links.Add(new ImportedLink(pred, Val(t, "UID")!, type, lag));
            }
        var name = Val(project, "Title") is { Length: > 0 } title ? title : Val(project, "Name") ?? "Work schedule";
        if (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".mpp", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return new ImportedSchedule(name, start, cal, tasks, links, CalendarName: calendar is { } c ? Val(c, "Name") : null,
            DataDate: When(Val(project, "StatusDate")) is { } sd ? DateOnly.FromDateTime(sd) : null);
    }
}
