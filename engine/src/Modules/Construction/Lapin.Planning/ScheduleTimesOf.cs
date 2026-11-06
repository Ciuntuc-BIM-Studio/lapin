using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// The work schedule offered to the cost module (docs/plan-4d5d.md, stage 7), through the platform so neither needs the
/// other: the tasks with their dates (the critical path's), progress and baseline, the calendar, and which leaf tasks
/// build an element or a part of it — a layer (<c>layer:2</c>, <c>layer:2:&lt;material&gt;</c>) by the layers each task's
/// part takes, a zone of a zoned core by its core, the whole element by any task that takes some of it.
/// </summary>
public sealed class ScheduleTimesOf : IWorkSchedules
{
    public ScheduleTimes? Times(IQueryContext query)
    {
        var p = PlanningModule.ReadResolved(query, out var links);
        if (p.Rows.Count == 0) return null;
        var (r, _) = PlanningModule.Solve(p);
        return Of(p, r, links);
    }

    public static ScheduleTimes Of(Plan p, CpmResult r, TaskElements links)
    {
        var tasks = p.Rows.Select(t =>
        {
            var tm = r.Times[t.Id];
            var pr = p.ProgressOf(t.Id);
            var summary = p.IsSummary(t.Id);
            return new ScheduledTask(t.Id, t.Name, t.Parent is { } par && p.ById.ContainsKey(par) ? par : null, summary, p.Wbs[t.Id], p.Level[t.Id],
                tm.Start, tm.Finish, t.Wet, !summary && (t.Milestone || t.Duration == 0), pr.Complete, pr.ActualStart, pr.ActualFinish,
                t.Baseline?.Start, t.Baseline?.Finish);
        }).ToList();
        var leaves = p.Rows.Where(t => !p.IsSummary(t.Id) && links.ByTask.ContainsKey(t.Id)).Select(t => t.Id).ToList();
        IReadOnlyList<string> TasksOf(string element, string? part)
        {
            var taking = leaves.Where(t => links.ByTask[t].ContainsKey(element)).ToList();
            if (taking.Count == 0 || part == null || links.Facts(element) is not { } f) return taking;
            // The layer the part is of (layer:<n>, a zone of the core, a roof's deck or covering).
            if (TaskParts.LayerOf(f, part) is not { } li || li >= 64) return taking;
            return taking.Where(t => (links.ByTask[t][element] & (1UL << li)) != 0).ToList();
        }
        var finish = r.Times.Count > 0 ? r.Times.Values.Max(t => t.Finish) : p.Start;
        return new ScheduleTimes(p.Schedule?.Id, p.Name, p.Calendar.NextWorking(p.Start), finish, p.DataDate, p.Rows.Any(t => t.Baseline != null),
            tasks, (d, wet) => p.Calendar.Working(d, wet), TasksOf);
    }
}
