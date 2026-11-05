using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// A task as the scheduler reads it from its IfcTask: its place in the WBS (parent and order among its siblings), its
/// duration in working days (0: a milestone), its constraint (start no earlier than, must start on, with a date), and
/// whether it is wet (it keeps the calendar's winter break); and its elements (stage 2): the rule they are found by, those
/// added and taken off by hand (ids), and the part of them it takes (<see cref="TaskParts"/>); its progress (stage 5): the
/// actual start and finish and the percentage typed on it, its elements marked started or done, and its baseline; the start
/// a resource levelling gave it (<c>levelStart</c>: no earlier than that day while it has not started; another module levels,
/// MS Project's leveling delay as a date), cleared when its start is set by hand.
/// </summary>
public sealed record PlanTask(string Id, string Name, string? Parent, double Order, int Duration, bool Milestone, string? Constraint, DateOnly? ConstraintDate, bool Wet, string? Kind, string? Notes,
    TaskRule? Rule = null, string? Part = null, IReadOnlyList<string>? AddIds = null, IReadOnlyList<string>? RemoveIds = null,
    DateOnly? ActualStart = null, DateOnly? ActualFinish = null, double? Complete = null, IReadOnlyDictionary<string, ElementMark>? MarkMap = null, TaskBaseline? Baseline = null, DateOnly? LevelStart = null)
{
    /// <summary>Its elements marked on site (stage 5): started, done, by element id.</summary>
    public IReadOnlyDictionary<string, ElementMark> Marks => MarkMap ?? EmptyMarks;
    static readonly Dictionary<string, ElementMark> EmptyMarks = [];
    public IReadOnlyList<string> Add => AddIds ?? [];
    public IReadOnlyList<string> Remove => RemoveIds ?? [];
    /// <summary>Whether the task takes elements at all (a rule, or elements added by hand).</summary>
    public bool Links => Rule != null || Add.Count > 0;
}

/// <summary>An IfcRelSequence: the predecessor, the successor, FS / SS / FF / SF, and the lag in working days (negative: a lead).</summary>
public sealed record PlanLink(string Id, string From, string To, string Type, int Lag);

/// <summary>
/// The project's work schedule read from the model (docs/plan-4d5d.md): the IfcWorkSchedule with its start, its
/// IfcWorkCalendar, its tasks in the order the Gantt lists them (depth first, each level by its order), their levels and
/// WBS numbers, and the links. A project with no schedule reads as an empty one starting next Monday.
/// </summary>
public sealed class Plan
{
    public const string ScheduleClass = "IfcWorkSchedule";
    public const string TaskClass = "IfcTask";
    public const string SequenceClass = "IfcRelSequence";
    public static readonly string[] LinkTypes = ["FS", "SS", "FF", "SF"];
    public static readonly string[] Constraints = ["snet", "mso"];
    /// <summary>IFC4's IfcTaskTypeEnum, the kinds a task may be.</summary>
    public static readonly string[] Kinds = ["ATTENDANCE", "CONSTRUCTION", "DEMOLITION", "DISMANTLE", "DISPOSAL", "INSTALLATION", "LOGISTIC", "MAINTENANCE", "MOVE", "OPERATION", "REMOVAL", "RENOVATION"];

    public Entity? Schedule { get; init; }
    public Entity? CalendarEntity { get; init; }
    public WorkCalendar Calendar { get; init; } = WorkCalendar.Default;
    public DateOnly Start { get; init; }
    public DateOnly? DataDate { get; init; }
    /// <summary>When the baseline was saved (null: none).</summary>
    public DateOnly? BaselineSaved { get; init; }
    /// <summary>Each task's progress (stage 5): its own fields, its elements' marks once <see cref="ProgressOf"/> was given them (<see cref="ApplyElements"/>), the summaries from their tasks.</summary>
    public Dictionary<string, TaskProgress> Progress { get; private set; } = [];

    public TaskProgress ProgressOf(string id) => Progress.TryGetValue(id, out var p) ? p : TaskProgress.None;

    /// <summary>
    /// The leaves' progress worked out again from their elements (each task's own, by id, with the part it takes) and the
    /// measure of a part of an element (null: a count), then the summaries from their leaves.
    /// </summary>
    public void ApplyElements(Func<string, IReadOnlyDictionary<string, ulong>?> elementsOf, Func<PlanTask, string, ulong, double>? measure)
    {
        foreach (var t in Rows)
            if (!IsSummary(t.Id)) Progress[t.Id] = TaskProgress.Of(t, elementsOf(t.Id), measure);
        TaskProgress.Rollup(this);
    }
    public string Name { get; init; } = "Work schedule";
    /// <summary>The tasks in row order: depth first, siblings by their order.</summary>
    public IReadOnlyList<PlanTask> Rows { get; private set; } = [];
    public IReadOnlyList<PlanLink> Links { get; private set; } = [];
    public Dictionary<string, PlanTask> ById { get; } = [];
    public Dictionary<string, List<string>> Children { get; } = [];
    public Dictionary<string, int> Level { get; } = [];
    public Dictionary<string, string> Wbs { get; } = [];
    /// <summary>A task's row number (1 first), as MS Project numbers its IDs and the predecessors column names them.</summary>
    public Dictionary<string, int> RowOf { get; } = [];

    public bool IsSummary(string id) => Children.TryGetValue(id, out var c) && c.Count > 0;

    /// <summary>The project's schedule: the first IfcWorkSchedule (one per project in stage 1).</summary>
    public static Entity? ScheduleOf(IModelView m) => m.OfClass(ScheduleClass).FirstOrDefault();

    /// <summary>The Monday after a day (the day itself when it is a Monday): where a new schedule starts.</summary>
    public static DateOnly NextMonday(DateOnly d)
    {
        var dow = (int)d.DayOfWeek;
        return d.AddDays(dow == 1 ? 0 : (8 - dow) % 7);
    }

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    public static PlanTask ReadTask(Entity e) => new(
        e.Id, e.Name, e.Str("parent"), e.Num("order"),
        Math.Max(0, (int)Math.Round(e.Num("duration", 1))), e.Bool("milestone"),
        e.Str("constraint") is { } c && Constraints.Contains(c) ? c : null, WorkCalendar.ParseDate(e.Str("constraintDate")),
        e.Bool("wet"), e.PredefinedType, e.Str("notes"),
        TaskRule.Read(e.Attrs["rule"]), PartOf(e.Str("part")), Ids(e.Attrs["add"]), Ids(e.Attrs["remove"]),
        WorkCalendar.ParseDate(e.Str("actualStart")), WorkCalendar.ParseDate(e.Str("actualFinish")),
        e.NumOrNull("complete") is { } pc && double.IsFinite(pc) ? Math.Clamp(pc, 0, 100) : null, ElementMark.ReadAll(e.Attrs["marks"]), TaskBaseline.Read(e.Attrs["baseline"]),
        WorkCalendar.ParseDate(e.Str("levelStart")));

    static string? PartOf(string? s) { try { return TaskParts.Parse(s); } catch (CommandException) { return null; } }

    static List<string>? Ids(JsonNode? n) => n is JsonArray a ? a.Select(x => (string?)x).OfType<string>().Distinct().ToList() : null;

    public static PlanLink? ReadLink(Relation r) =>
        r.Class == SequenceClass && r.Related.Count > 0
            ? new(r.Id, r.Relating, r.Related[0], (string?)r.Attrs?["type"] is { } t && LinkTypes.Contains(t) ? t : "FS", (int)Math.Round(Json.Number(r.Attrs?["lag"]) ?? 0))
            : null;

    public static Plan Read(IModelView m)
    {
        var schedule = ScheduleOf(m);
        var calendarEntity = schedule?.Str("calendar") is { } cid ? m.Get(cid) : null;
        calendarEntity ??= m.OfClass(WorkCalendar.Class).FirstOrDefault();
        var tasks = m.OfClass(TaskClass).Where(t => schedule == null || t.Str("schedule") is not { } s || s == schedule.Id).Select(ReadTask).ToList();
        var ids = tasks.Select(t => t.Id).ToHashSet();
        var links = new List<PlanLink>();
        foreach (var t in tasks)
            foreach (var r in m.RelationsOf(t.Id, SequenceClass))
                if (r.Relating == t.Id && ReadLink(r) is { } l && ids.Contains(l.To)) links.Add(l);
        var plan = new Plan
        {
            Schedule = schedule,
            CalendarEntity = calendarEntity,
            Calendar = WorkCalendar.Read(calendarEntity?.Attrs),
            Start = WorkCalendar.ParseDate(schedule?.Str("start")) ?? NextMonday(Today()),
            DataDate = WorkCalendar.ParseDate(schedule?.Str("dataDate")),
            BaselineSaved = WorkCalendar.ParseDate(schedule?.Str("baselineSaved")),
            Name = schedule?.Name ?? "Work schedule",
            Links = links,
        };
        foreach (var t in tasks) plan.ById[t.Id] = t;
        // A parent that is gone (or would make a loop) leaves its task at the top.
        string? ParentOf(PlanTask t)
        {
            var seen = new HashSet<string> { t.Id };
            for (var p = t.Parent; p != null; p = plan.ById.TryGetValue(p, out var pt) ? pt.Parent : null)
            {
                if (!plan.ById.ContainsKey(p) || !seen.Add(p)) return null;
            }
            return t.Parent;
        }
        var roots = new List<PlanTask>();
        foreach (var t in tasks)
        {
            var parent = ParentOf(t);
            if (parent == null) roots.Add(t);
            else (plan.Children.TryGetValue(parent, out var l) ? l : plan.Children[parent] = []).Add(t.Id);
        }
        var order = new Dictionary<string, int>();
        var i = 0;
        foreach (var t in tasks) order[t.Id] = i++;
        var rows = new List<PlanTask>();
        void Walk(IEnumerable<PlanTask> level, int depth, string prefix)
        {
            var n = 0;
            foreach (var t in level.OrderBy(x => x.Order).ThenBy(x => order[x.Id]))
            {
                n++;
                var wbs = prefix.Length == 0 ? $"{n}" : $"{prefix}.{n}";
                plan.Level[t.Id] = depth;
                plan.Wbs[t.Id] = wbs;
                rows.Add(t);
                plan.RowOf[t.Id] = rows.Count;
                if (plan.Children.TryGetValue(t.Id, out var kids)) Walk(kids.Select(k => plan.ById[k]), depth + 1, wbs);
            }
        }
        Walk(roots, 0, "");
        foreach (var kids in plan.Children.Values) kids.Sort((a, b) => plan.RowOf[a].CompareTo(plan.RowOf[b]));
        plan.Rows = rows;
        foreach (var t in rows) if (!plan.IsSummary(t.Id)) plan.Progress[t.Id] = TaskProgress.Of(t, null, null);
        TaskProgress.Rollup(plan);
        return plan;
    }

    /// <summary>The same plan with other links (the tasks shared): what a link would do, checked before it is made.</summary>
    public Plan WithLinks(IReadOnlyList<PlanLink> links)
    {
        var p = (Plan)MemberwiseClone();
        p.Links = links;
        p.Progress = new Dictionary<string, TaskProgress>(Progress);
        return p;
    }

    /// <summary>The tasks under a task, at any depth (itself excluded).</summary>
    public IEnumerable<string> Descendants(string id)
    {
        if (!Children.TryGetValue(id, out var kids)) yield break;
        foreach (var k in kids)
        {
            yield return k;
            foreach (var d in Descendants(k)) yield return d;
        }
    }

    /// <summary>The tasks above a task, its parent first.</summary>
    public IEnumerable<string> Ancestors(string id)
    {
        for (var p = ById.GetValueOrDefault(id)?.Parent; p != null && ById.ContainsKey(p); p = ById[p].Parent) yield return p;
    }

    /// <summary>The leaves under a task (itself when it has no children).</summary>
    public List<string> LeavesOf(string id)
    {
        if (!IsSummary(id)) return [id];
        return Descendants(id).Where(d => !IsSummary(d)).ToList();
    }

    /// <summary>A link as the predecessors column writes it: the predecessor's row, its type unless FS, its lag ("3", "5SS+2d", "7FF-1d").</summary>
    public string LinkText(PlanLink l)
    {
        var row = RowOf.GetValueOrDefault(l.From);
        var type = l.Type == "FS" && l.Lag == 0 ? "" : l.Type;
        var lag = l.Lag == 0 ? "" : l.Lag > 0 ? $"+{l.Lag}d" : $"{l.Lag}d";
        return $"{row}{type}{lag}";
    }
}
