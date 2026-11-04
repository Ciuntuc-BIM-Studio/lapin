using System.Globalization;
using System.Text.RegularExpressions;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Ifc;

public static partial class IfcImport
{
    /// <summary>
    /// An ISO 8601 duration (an IfcDuration: P5D, PT40H, P1W, P2DT4H, -P2D) in working days, a week of five days and a day
    /// of <paramref name="hoursPerDay"/> hours; null when it is not one.
    /// </summary>
    public static double? DurationDays(string? s, double hoursPerDay = 8)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = Regex.Match(s.Trim(), @"^(-)?P(?:(\d+(?:\.\d+)?)Y)?(?:(\d+(?:\.\d+)?)M)?(?:(\d+(?:\.\d+)?)W)?(?:(\d+(?:\.\d+)?)D)?(?:T(?:(\d+(?:\.\d+)?)H)?(?:(\d+(?:\.\d+)?)M)?(?:(\d+(?:\.\d+)?)S)?)?$");
        if (!m.Success) return null;
        double G(int i) => m.Groups[i].Success ? double.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture) : 0;
        var days = G(2) * 250 + G(3) * 20 + G(4) * 5 + G(5) + (G(6) + G(7) / 60 + G(8) / 3600) / (hoursPerDay > 0 ? hoursPerDay : 8);
        return m.Groups[1].Success ? -days : days;
    }

    static DateTime? IfcWhen(object? v) =>
        IfcModel.Str(v) is { } s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var d) ? d : null;

    static TimeOnly? IfcClock(object? v) =>
        IfcModel.Str(v) is { } s && TimeOnly.TryParse(s, CultureInfo.InvariantCulture, out var t) ? t : null;

    sealed partial class Reader
    {
        /// <summary>A property set's single values as text, by name.</summary>
        List<(string Name, string Value)> PropertiesOf(int id, string setName)
        {
            var o = new List<(string, string)>();
            foreach (var sid in definitions.GetValueOrDefault(id) ?? [])
                if (f.Entities.GetValueOrDefault(sid) is { Type: "IFCPROPERTYSET" } set && Str(set[2]) == setName)
                    foreach (var p in Items(set[4]))
                        if (p.Type == "IFCPROPERTYSINGLEVALUE" && Str(p[0]) is { } name)
                            o.Add((name, Value(p[2]) switch { bool b => b ? "true" : "false", double d => d.ToString(CultureInfo.InvariantCulture), System.Text.Json.Nodes.JsonNode n => n.ToJsonString(), var x => x?.ToString() ?? "" }));
            return o;
        }

        /// <summary>
        /// The file's work schedules (IFC 4: IfcWorkSchedule, the IfcTasks it controls and those nested in them, their
        /// IfcTaskTime, the IfcRelSequences between them, the IfcWorkCalendar controlling the schedule or its tasks), handed to
        /// the module that keeps processes (IIfcProcesses). Tasks no schedule controls make one of their own. IFC2x3's tasks
        /// come with their names, nesting and links.
        /// </summary>
        void Processes(IEdit edit)
        {
            var services = s.Catalog.All<IIfcProcesses>();
            if (services.Count == 0) return;
            var tasks = f.OfType("IFCTASK").ToList();
            if (tasks.Count == 0) return;
            var ifc2x3 = f.Schema.StartsWith("IFC2X3", StringComparison.OrdinalIgnoreCase);
            var byId = tasks.ToDictionary(t => t.Id);
            var controls = f.OfType("IFCRELASSIGNSTOCONTROL").ToList();
            var nests = f.OfType("IFCRELNESTS").Where(r => byId.ContainsKey(Ref(r[4]))).ToList();
            var parentOf = new Dictionary<int, int>();
            var childOrder = new Dictionary<int, int>();
            foreach (var r in nests)
            {
                var i = 0;
                foreach (var c in Refs(r[5])) if (byId.ContainsKey(c)) { parentOf.TryAdd(c, Ref(r[4])); childOrder.TryAdd(c, i++); }
            }
            // The elements each task builds (IfcRelAssignsToProduct: the product, the tasks among its related objects) and acts on
            // (IfcRelAssignsToProcess: the task, its related objects): by their GlobalIds; programs write one or the other.
            var outputs = new Dictionary<int, List<string>>();
            var inputs = new Dictionary<int, List<string>>();
            foreach (var r in f.OfType("IFCRELASSIGNSTOPRODUCT"))
                if (f.Get(r[6]) is { } product && Str(product[0]) is { } pg)
                    foreach (var t in Refs(r[4])) if (byId.ContainsKey(t)) (outputs.TryGetValue(t, out var l) ? l : outputs[t] = []).Add(pg);
            foreach (var r in f.OfType("IFCRELASSIGNSTOPROCESS"))
                if (byId.ContainsKey(Ref(r[6])))
                    foreach (var o in Refs(r[4]))
                        if (f.Get(o) is { } obj && obj.Type is not ("IFCTASK" or "IFCTASKTYPE") && Str(obj[0]) is { } og)
                            (inputs.TryGetValue(Ref(r[6]), out var l) ? l : inputs[Ref(r[6])] = []).Add(og);
            var calendars = f.OfType("IFCWORKCALENDAR").ToList();
            var schedules = f.OfType("IFCWORKSCHEDULE").ToList();
            var claimed = new HashSet<int>();
            var result = new List<IfcScheduleData>();

            IfcCalendarData? CalendarOf(StepEntity? c)
            {
                if (c == null) return null;
                var weekdays = new List<int>();
                TimeOnly start = new(8, 0), end = new(16, 0);
                foreach (var wt in Items(c[6]))
                    if (f.Get(wt[3]) is { } pattern)
                    {
                        if (pattern[2] is List<object?> days) weekdays.AddRange(days.Select(d => (int)Num(d)));
                        if (Items(pattern[7]).FirstOrDefault() is { } period && IfcClock(period[0]) is { } a && IfcClock(period[1]) is { } b) (start, end) = (a, b);
                    }
                var exceptions = new List<(DateOnly, DateOnly, string)>();
                foreach (var wt in Items(c[7]))
                    if (IfcWhen(wt[4]) is { } from)
                        exceptions.Add((DateOnly.FromDateTime(from), DateOnly.FromDateTime(IfcWhen(wt[5]) ?? from), Str(wt[0]) ?? ""));
                return new IfcCalendarData(Str(c[0]) ?? Step.Guid("calendar", c.Id.ToString(CultureInfo.InvariantCulture)), Str(c[2]) ?? "Calendar",
                    weekdays.Distinct().ToList(), start, end, exceptions, "Lapin_Calendar", PropertiesOf(c.Id, "Lapin_Calendar"));
            }

            IfcScheduleData Make(string id, string name, DateTime? start, string predefined, List<StepEntity> top)
            {
                // The schedule's tasks: those it controls and everything nested in them, parents first, siblings in the nesting's order.
                var list = new List<StepEntity>();
                var seen = new HashSet<int>();
                var children = parentOf.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).OrderBy(c => childOrder.GetValueOrDefault(c)).ToList());
                void Walk(StepEntity t)
                {
                    if (!seen.Add(t.Id)) return;
                    list.Add(t);
                    foreach (var c in children.GetValueOrDefault(t.Id) ?? []) if (byId.TryGetValue(c, out var ct)) Walk(ct);
                }
                foreach (var t in top) Walk(t);
                claimed.UnionWith(seen);
                // The calendar controlling the schedule's tasks (or any, when one alone is in the file).
                var calendar = calendars.FirstOrDefault(c => controls.Any(r => Ref(r[6]) == c.Id && Refs(r[4]).Any(seen.Contains))) ?? (calendars.Count == 1 ? calendars[0] : null);
                var cal = CalendarOf(calendar);
                var hours = cal is { } cc && (cc.DayEnd - cc.DayStart).TotalHours is var h && h > 0 ? h : 8;
                var gidOf = list.ToDictionary(t => t.Id, t => Str(t[0]) ?? Step.Guid("task", t.Id.ToString(CultureInfo.InvariantCulture)));
                var data = new List<IfcTaskData>();
                foreach (var t in list)
                {
                    IfcTaskTimeData? time = null;
                    if (!ifc2x3 && f.Get(t[11]) is { Type: "IFCTASKTIME" } tt)
                        time = new IfcTaskTimeData(DurationDays(Str(tt[4]), hours), IfcWhen(tt[5]) ?? IfcWhen(tt[7]), IfcWhen(tt[6]) ?? IfcWhen(tt[8]), IfcWhen(tt[9]), IfcWhen(tt[10]),
                            DurationDays(Str(tt[11]), hours), DurationDays(Str(tt[12]), hours), (tt[13] as StepEnum)?.Value == "T", tt[19] is double pc ? pc : null,
                            IfcWhen(tt[14]), IfcWhen(tt[16]), IfcWhen(tt[17]), DurationDays(Str(tt[18]), hours));
                    var milestone = ((ifc2x3 ? t[8] : t[9]) as StepEnum)?.Value == "T";
                    var parent = parentOf.TryGetValue(t.Id, out var pid) && seen.Contains(pid) ? gidOf[pid] : null;
                    data.Add(new IfcTaskData(gidOf[t.Id], Str(t[2]) ?? "Task", Str(t[3]), Str(ifc2x3 ? t[5] : t[5]), parent, milestone,
                        ifc2x3 ? null : (t[12] as StepEnum)?.Value is { } k && k is not ("NOTDEFINED" or "USERDEFINED") ? k : null, time, "Lapin_Task", PropertiesOf(t.Id, "Lapin_Task"),
                        outputs.GetValueOrDefault(t.Id), inputs.GetValueOrDefault(t.Id)));
                }
                var sequences = new List<IfcSequenceData>();
                foreach (var r in f.OfType("IFCRELSEQUENCE"))
                {
                    var (a, b) = (Ref(r[4]), Ref(r[5]));
                    if (!seen.Contains(a) || !seen.Contains(b)) continue;
                    var lag = f.Get(r[6]) is { Type: "IFCLAGTIME" } lt ? DurationDays(Str(lt[3]), hours) ?? 0 : (ifc2x3 ? Num(r[6]) / 86400 : 0);
                    var type = (r[7] as StepEnum)?.Value ?? "FINISH_START";
                    sequences.Add(new IfcSequenceData(Str(r[0]) ?? Step.Guid("sequence", r.Id.ToString(CultureInfo.InvariantCulture)), gidOf[a], gidOf[b], type, lag));
                }
                var first = data.Select(d => d.Time?.ScheduleStart).Where(d => d != null).Min();
                return new IfcScheduleData(id, name, start ?? first ?? DateTime.Today, null, predefined, cal, data, sequences);
            }

            foreach (var ws in schedules)
            {
                var top = controls.Where(r => Ref(r[6]) == ws.Id).SelectMany(r => Refs(r[4])).Where(byId.ContainsKey).Select(id => byId[id])
                    .Where(t => !parentOf.ContainsKey(t.Id) || !byId.ContainsKey(parentOf[t.Id])).ToList();
                // IFC2x3 has no StartTime on the schedule: the attributes differ; IFC 4's are read.
                var start = ifc2x3 ? null : IfcWhen(ws[11]);
                var predefined = ifc2x3 ? "PLANNED" : (ws[13] as StepEnum)?.Value ?? "PLANNED";
                if (top.Count > 0) result.Add(Make(Str(ws[0]) ?? Step.Guid("schedule", ws.Id.ToString(CultureInfo.InvariantCulture)), Str(ws[2]) ?? "Work schedule", start, predefined, top));
            }
            // Tasks no schedule controls: a schedule of their own.
            var loose = tasks.Where(t => !claimed.Contains(t.Id) && (!parentOf.TryGetValue(t.Id, out var p) || !byId.ContainsKey(p))).ToList();
            if (loose.Count > 0) result.Add(Make(Step.Guid(FileName ?? "ifc", "schedule"), "Work schedule", null, "PLANNED", loose));
            if (result.Count == 0) return;
            // What the file names by GlobalId as the project has it now: its storeys and types where they were matched to the project's, else the same id.
            var mapped = new Dictionary<string, string>();
            foreach (var (stepId, modelId) in storeyOf.Concat(typeIds))
                if (f.Get(new StepRef(stepId)) is { } e && Str(e[0]) is { } g) mapped.TryAdd(g, modelId);
            string? IdOf(string gid) => mapped.TryGetValue(gid, out var x) ? x : edit.Model.Get(gid) != null ? gid : null;
            foreach (var service in services)
            {
                var r = service.Import(edit, result, IdOf);
                if (r.Tasks > 0) notes.Add($"Work schedule: {r.Tasks} task(s), {r.Sequences} link(s) (Planning → Gantt).");
                notes.AddRange(r.Notes);
            }
        }
    }
}
