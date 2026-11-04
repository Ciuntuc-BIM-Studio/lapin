using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>A task read from a file (MS Project, IFC): its key in the file, its id when it carries one (a GlobalId), its parent's key, and what Lapin keeps of it (its elements: <see cref="TaskLinkData"/>).</summary>
public sealed record ImportedTask(string Key, string? Id, string Name, string? ParentKey, int Duration, bool Milestone, string? Constraint = null, DateOnly? ConstraintDate = null,
    DateOnly? Start = null, bool Wet = false, string? Kind = null, string? Notes = null, JsonObject? Links = null);

/// <summary>A link read from a file, between two tasks' keys.</summary>
public sealed record ImportedLink(string FromKey, string ToKey, string Type, int Lag, string? Id = null);

/// <summary>A whole schedule read from a file: its name and start, its calendar, its tasks in row order, its links; the ids it carries for the schedule and calendar.</summary>
public sealed record ImportedSchedule(string Name, DateOnly Start, WorkCalendar Calendar, IReadOnlyList<ImportedTask> Tasks, IReadOnlyList<ImportedLink> Links,
    string? ScheduleId = null, string? CalendarId = null, string? CalendarName = null);

/// <summary>What an import did.</summary>
public sealed record ImportResult(int Tasks, int Links, int Dropped, IReadOnlyList<string> Notes)
{
    public JsonObject ToJson() => new()
    {
        ["tasks"] = Tasks, ["links"] = Links, ["dropped"] = Dropped,
        ["notes"] = new JsonArray(Notes.Select(n => (JsonNode)n).ToArray()),
    };
}

/// <summary>
/// A schedule read from a file put in the project, as one undo step: it takes the place of the project's schedule (its
/// tasks and links go; the schedule and its calendar are kept and take the file's name, start and calendar), each task
/// with the GlobalId it came with when it carries one, so a schedule written and read again keeps its ids. A link that
/// would make a loop is left out and counted. A task the file places later than the start with nothing before it keeps
/// its date as a start-no-earlier-than constraint (programs that do not write links still place their tasks).
/// </summary>
public static class ScheduleImport
{
    public static ImportResult Apply(IEdit edit, ImportedSchedule s)
    {
        var notes = new List<string>();
        var (schedule, calendar) = PlanningModule.EnsureSchedule(edit, s.ScheduleId, s.CalendarId);
        foreach (var t in edit.Model.OfClass(Plan.TaskClass).Where(t => t.Str("schedule") is not { } sid || sid == schedule.Id).ToList()) edit.Remove(t.Id);

        var sa = schedule.CloneAttrs();
        sa["start"] = WorkCalendar.Date(s.Start);
        edit.Replace(schedule.With(name: s.Name.Length > 0 ? s.Name : schedule.Name, attrs: sa));
        var cal = edit.Model.Get(calendar.Id)!;
        edit.Replace(cal.With(name: s.CalendarName is { Length: > 0 } cn ? cn : cal.Name, attrs: s.Calendar.ToJson()));

        var idOf = new Dictionary<string, string>();
        foreach (var t in s.Tasks)
        {
            var id = t.Id is { } given && GlobalId.IsValid(given) && edit.Model.Get(given) == null && edit.Model.GetRelation(given) == null && !idOf.ContainsValue(given) ? given : edit.NewId();
            idOf[t.Key] = id;
        }
        var order = new Dictionary<string, int>();
        foreach (var t in s.Tasks)
        {
            var parent = t.ParentKey != null && idOf.TryGetValue(t.ParentKey, out var p) ? p : null;
            var key = parent ?? "";
            order[key] = order.GetValueOrDefault(key) + 1;
            var attrs = new JsonObject { ["schedule"] = schedule.Id, ["order"] = order[key], ["duration"] = t.Milestone ? 0 : Math.Max(0, t.Duration) };
            if (parent != null) attrs["parent"] = parent;
            if (t.Milestone) attrs["milestone"] = true;
            if (t.Constraint != null && t.ConstraintDate is { } cd) { attrs["constraint"] = t.Constraint; attrs["constraintDate"] = WorkCalendar.Date(cd); }
            if (t.Wet) attrs["wet"] = true;
            if (!string.IsNullOrWhiteSpace(t.Notes)) attrs["notes"] = t.Notes;
            if (t.Links != null) TaskLinkData.Write(attrs, t.Links);
            var kind = t.Kind is { } k && Plan.Kinds.Contains(k) ? k : null;
            edit.Add(new Entity(idOf[t.Key], Plan.TaskClass, t.Name.Length > 0 ? t.Name : "Task", attrs, kind));
        }

        int links = 0, dropped = 0;
        var plan = Plan.Read(edit.Model);
        var accepted = new List<PlanLink>();
        foreach (var l in s.Links)
        {
            if (!idOf.TryGetValue(l.FromKey, out var from) || !idOf.TryGetValue(l.ToKey, out var to) || from == to) { dropped++; continue; }
            if (plan.Ancestors(to).Contains(from) || plan.Ancestors(from).Contains(to)) { dropped++; continue; }
            var type = Plan.LinkTypes.Contains(l.Type) ? l.Type : "FS";
            var id = l.Id is { } given && GlobalId.IsValid(given) && edit.Model.Get(given) == null && edit.Model.GetRelation(given) == null ? given : edit.NewId();
            accepted.Add(new PlanLink(id, from, to, type, l.Lag));
        }
        // All at once when they make no loop (the common case); else one by one, each kept only when the ones before it allow it.
        var withAll = WithLinks(plan, accepted);
        if (!Cpm.HasLoop(withAll)) foreach (var l in accepted) { Add(edit, l); links++; }
        else
        {
            var kept = new List<PlanLink>();
            foreach (var l in accepted)
            {
                if (Cpm.HasLoop(WithLinks(plan, kept), l)) { dropped++; continue; }
                kept.Add(l);
                Add(edit, l);
                links++;
            }
        }
        if (dropped > 0) notes.Add($"{dropped} link(s) left out: they named a task the file does not have, or made a loop.");

        // Tasks placed later than the start with nothing driving them keep their date.
        plan = Plan.Read(edit.Model);
        var driven = new HashSet<string>();
        foreach (var l in plan.Links) foreach (var leaf in plan.LeavesOf(l.To)) driven.Add(leaf);
        foreach (var t in s.Tasks)
        {
            if (t.Start is not { } start || t.Constraint != null || !idOf.TryGetValue(t.Key, out var id) || plan.IsSummary(id)) continue;
            if (driven.Contains(id) || start <= s.Start || !plan.Calendar.Working(start, t.Wet) && plan.Calendar.NextWorking(start, t.Wet) <= plan.Calendar.NextWorking(s.Start)) continue;
            var e = edit.Model.Get(id)!;
            var attrs = e.CloneAttrs();
            attrs["constraint"] = "snet";
            attrs["constraintDate"] = WorkCalendar.Date(start);
            edit.Replace(e.With(attrs: attrs));
        }
        return new ImportResult(s.Tasks.Count, links, dropped, notes);
    }

    static void Add(IEdit edit, PlanLink l) =>
        edit.AddRelation(new Relation(l.Id, Plan.SequenceClass, l.From, [l.To], new JsonObject { ["type"] = l.Type, ["lag"] = l.Lag }));

    /// <summary>The plan as it would be with these links (for the loop check).</summary>
    static Plan WithLinks(Plan p, IReadOnlyList<PlanLink> links) => p.WithLinks(links);
}
