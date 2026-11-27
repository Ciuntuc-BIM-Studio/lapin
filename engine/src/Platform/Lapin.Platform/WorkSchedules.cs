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
/// not), the working hours of a day, and the leaf tasks that build an element or a part of it (<c>layer:2</c>, <c>zone:…</c>; null: the whole).
/// </summary>
public sealed class ScheduleTimes(string? id, string name, DateOnly start, DateOnly finish, DateOnly? dataDate, bool baseline,
    IReadOnlyList<ScheduledTask> tasks, Func<DateOnly, bool, bool> working, Func<string, string?, IReadOnlyList<string>> tasksOf, double hoursPerDay = 8)
{
    Dictionary<string, ScheduledTask>? _byId;

    public string? Id => id;
    public string Name => name;
    public DateOnly Start => start;
    public DateOnly Finish => finish;
    public DateOnly? DataDate => dataDate;
    public bool Baseline => baseline;
    public IReadOnlyList<ScheduledTask> Tasks => tasks;
    /// <summary>The working hours of a day, as the calendar has them.</summary>
    public double HoursPerDay => hoursPerDay;
    /// <summary>A task by its id, null when the schedule has none of it.</summary>
    public ScheduledTask? Task(string id) => (_byId ??= tasks.ToDictionary(t => t.Id)).GetValueOrDefault(id);
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

    /// <summary>
    /// When an element is on site (docs/plan-4d5d.md, "× duration"): from the start of the first leaf task that builds any
    /// part of it to the finish of the last, worked out on every ask; null when no task takes it.
    /// </summary>
    public (DateOnly Start, DateOnly Finish)? Presence(string element)
    {
        DateOnly? from = null, to = null;
        foreach (var id in tasksOf(element, null))
            if (Task(id) is { } t)
            {
                if (from == null || t.Start < from) from = t.Start;
                if (to == null || t.Finish > to) to = t.Finish;
            }
        return from is { } f && to is { } e ? (f, e < f ? f : e) : null;
    }

    /// <summary>
    /// How long a span lasts in a time unit (<see cref="MeasureUnits"/>): calendar days (both ends counted), working days by
    /// the calendar (its weekdays, holidays and exceptions), working hours (those days times the day's hours), weeks (calendar
    /// days over seven), or months (whole calendar months from its start, the rest as the part of its month: 1 March to 31 May is 3).
    /// </summary>
    public double Length(DateOnly from, DateOnly to, string unit) => unit switch
    {
        MeasureUnits.Days => to.DayNumber - from.DayNumber + 1,
        MeasureUnits.WorkingDays => WorkedDays(from, to).Count,
        MeasureUnits.Hours => WorkedDays(from, to).Count * hoursPerDay,
        MeasureUnits.Months => Months(from, to.AddDays(1)),
        MeasureUnits.Weeks => (to.DayNumber - from.DayNumber + 1) / 7.0,
        _ => throw new ArgumentException($"No such time unit: {unit}."),
    };

    /// <summary>The months from a day to another (exclusive): the whole months, then the rest over the length of the month it falls in.</summary>
    public static double Months(DateOnly from, DateOnly until)
    {
        if (until <= from) return 0;
        var whole = (until.Year - from.Year) * 12 + until.Month - from.Month;
        var at = from.AddMonths(whole);
        while (at > until) at = from.AddMonths(--whole);
        var month = from.AddMonths(whole + 1).DayNumber - at.DayNumber;
        return whole + (double)(until.DayNumber - at.DayNumber) / month;
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
