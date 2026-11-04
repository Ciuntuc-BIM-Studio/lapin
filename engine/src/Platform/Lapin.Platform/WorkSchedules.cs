namespace Lapin.Platform;

/// <summary>
/// A task of the work schedule as another module reads it (docs/plan-4d5d.md, stage 7: cost over time): its place in the
/// WBS, its dates as the critical path has them (the finish the last day worked), whether it is wet (it keeps the
/// calendar's winter break) or a milestone, how much of it is done and its actual dates, its baseline's dates.
/// </summary>
public sealed record ScheduledTask(string Id, string Name, string? Parent, bool Summary, string Wbs, int Level, DateOnly Start, DateOnly Finish,
    bool Wet, bool Milestone, double Complete, DateOnly? ActualStart, DateOnly? ActualFinish, DateOnly? BaselineStart, DateOnly? BaselineFinish);

/// <summary>
/// The work schedule as the planning module offers it to others, without a dependency on it: the tasks in row order, the
/// schedule's start and finish, the status date, whether a baseline is saved, the calendar as a test of a day (wet or
/// not), and the leaf tasks that build an element or a part of it (<c>layer:2</c>, <c>zone:…</c>; null: the whole).
/// </summary>
public sealed class ScheduleTimes(string? id, string name, DateOnly start, DateOnly finish, DateOnly? dataDate, bool baseline,
    IReadOnlyList<ScheduledTask> tasks, Func<DateOnly, bool, bool> working, Func<string, string?, IReadOnlyList<string>> tasksOf)
{
    public string? Id => id;
    public string Name => name;
    public DateOnly Start => start;
    public DateOnly Finish => finish;
    public DateOnly? DataDate => dataDate;
    public bool Baseline => baseline;
    public IReadOnlyList<ScheduledTask> Tasks => tasks;
    /// <summary>Whether a day is worked (a wet task's: not in the winter break either).</summary>
    public bool Working(DateOnly day, bool wet = false) => working(day, wet);
    /// <summary>The leaf tasks that build an element's part (null: any part of it), in row order.</summary>
    public IReadOnlyList<string> TasksOf(string element, string? part) => tasksOf(element, part);

    /// <summary>The days worked from <paramref name="from"/> to <paramref name="to"/>, both included (none when none is; a milestone's day when asked).</summary>
    public List<DateOnly> WorkedDays(DateOnly from, DateOnly to, bool wet = false)
    {
        var o = new List<DateOnly>();
        for (var d = from; d <= to; d = d.AddDays(1)) if (working(d, wet)) o.Add(d);
        return o;
    }
}

/// <summary>The project's work schedule read for others (the planning module's service); null when the project has no tasks.</summary>
public interface IWorkSchedules
{
    ScheduleTimes? Times(IQueryContext query);
}

/// <summary>A task's cost from the estimate (its elements' share of the lines that price them) and the part of it done.</summary>
public sealed record TaskCost(double Cost, double Done);

/// <summary>
/// The estimate laid on the schedule (docs/plan-4d5d.md, stage 7), as the Gantt shows it: each task's cost (a summary's
/// its tasks'), the currency, the budget at completion (the estimate's direct costs), what the tasks carry of it, and the
/// cost to date (the tasks' cost times how much of each is done).
/// </summary>
public sealed record ScheduleCosts(string Currency, IReadOnlyDictionary<string, TaskCost> Tasks, double Budget, double Scheduled, double ToDate);

/// <summary>The cost module's service: the schedule's tasks priced by the project's estimate; null when there is none.</summary>
public interface IScheduleCosts
{
    ScheduleCosts? Costs(IQueryContext query, ScheduleTimes times);
}
