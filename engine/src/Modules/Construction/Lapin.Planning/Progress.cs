using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>An element marked on site in a task (docs/plan-4d5d.md, stage 5): the day it was started, the day it was done.</summary>
public sealed record ElementMark(DateOnly? Started, DateOnly? Done)
{
    public static ElementMark? Read(JsonNode? n) =>
        n is JsonObject o && new ElementMark(WorkCalendar.ParseDate((string?)o["started"]), WorkCalendar.ParseDate((string?)o["done"])) is var m && (m.Started != null || m.Done != null) ? m : null;

    /// <summary>A task's marks (<c>marks</c>: element id → started, done); null when it has none.</summary>
    public static Dictionary<string, ElementMark>? ReadAll(JsonNode? n)
    {
        if (n is not JsonObject o || o.Count == 0) return null;
        var d = new Dictionary<string, ElementMark>();
        foreach (var (id, v) in o) if (Read(v) is { } m) d[id] = m;
        return d.Count > 0 ? d : null;
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject();
        if (Started is { } s) o["started"] = WorkCalendar.Date(s);
        if (Done is { } d) o["done"] = WorkCalendar.Date(d);
        return o;
    }
}

/// <summary>A task's baseline: its start and finish (the first and last working days) and its duration when the baseline was saved.</summary>
public sealed record TaskBaseline(DateOnly Start, DateOnly Finish, int Duration)
{
    public static TaskBaseline? Read(JsonNode? n) =>
        n is JsonObject o && WorkCalendar.ParseDate((string?)o["start"]) is { } s && WorkCalendar.ParseDate((string?)o["finish"]) is { } f
            ? new TaskBaseline(s, f < s ? s : f, (int)Math.Round(Json.Number(o["duration"]) ?? 0))
            : null;

    public JsonObject ToJson() => new() { ["start"] = WorkCalendar.Date(Start), ["finish"] = WorkCalendar.Date(Finish), ["duration"] = Duration };

    /// <summary>How many working days a start moved from the baseline's (positive: later).</summary>
    public int StartVariance(WorkCalendar cal, DateOnly start) => cal.WorkingDays(Start, start);

    /// <summary>How many working days a finish moved from the baseline's (positive: later, the task is late).</summary>
    public int FinishVariance(WorkCalendar cal, DateOnly finish) => cal.WorkingDays(Finish.AddDays(1), finish.AddDays(1));
}

/// <summary>
/// A task's progress (docs/plan-4d5d.md, stage 5): how much is done (0–100), its actual start and finish, and where the
/// percentage came from — typed on the task (<c>manual</c>), its elements marked on site (<c>elements</c>: the quantity done ÷
/// the task's quantity, a count when it has no unit), its actual dates (<c>actual</c>), its tasks (<c>children</c>: a
/// summary, weighted by their durations). What is typed on the task wins; the marks give what it leaves out: the actual
/// start is the first mark's day, the actual finish the last element's done day once all are done. Derived, never stored.
/// </summary>
public sealed record TaskProgress(double Complete, DateOnly? ActualStart, DateOnly? ActualFinish, string From, int Done = 0, int Started = 0, int Count = 0)
{
    public static readonly TaskProgress None = new(0, null, null, "none");

    public bool Finished => ActualFinish != null || Complete >= 100 - 1e-9;
    public bool Begun => Finished || ActualStart != null || Complete > 1e-9;
    /// <summary>done, started or planned (not started).</summary>
    public string Status => Finished ? "done" : Begun ? "started" : "planned";

    /// <summary>
    /// A leaf's progress: its own fields, and its elements' marks when its elements are known (by id, with the part taken);
    /// <paramref name="measure"/> weighs an element's part in the task's unit (null, or nothing measured: each counts one).
    /// </summary>
    public static TaskProgress Of(PlanTask t, IReadOnlyDictionary<string, ulong>? elements, Func<PlanTask, string, ulong, double>? measure)
    {
        DateOnly? start = t.ActualStart, finish = t.ActualFinish;
        double? measured = null;
        int done = 0, started = 0;
        if (elements is { Count: > 0 } && t.Marks.Count > 0)
        {
            double all = 0, got = 0;
            DateOnly? first = null, last = null;
            var allDone = true;
            foreach (var (id, mask) in elements)
            {
                var w = measure?.Invoke(t, id, mask) ?? 1;
                if (!double.IsFinite(w) || w < 0) w = 0;
                all += w;
                if (!t.Marks.TryGetValue(id, out var m)) { allDone = false; continue; }
                if ((m.Started ?? m.Done) is { } s0 && (first == null || s0 < first)) first = s0;
                if (m.Done is { } d) { done++; got += w; if (last == null || d > last) last = d; }
                else { started++; allDone = false; }
            }
            if (done + started > 0)
            {
                measured = all > 1e-9 ? 100 * got / all : 100.0 * done / elements.Count;
                start ??= first;
                if (allDone) finish ??= last;
            }
        }
        var complete = t.ActualFinish != null ? 100 : t.Complete ?? (finish != null ? 100 : measured ?? 0);
        var from = t.ActualFinish != null ? "actual" : t.Complete != null ? "manual" : measured != null ? "elements" : t.ActualStart != null ? "actual" : "none";
        if (from == "none" && complete <= 0 && start == null && finish == null) return None with { Count = elements?.Count ?? 0 };
        return new TaskProgress(Math.Clamp(complete, 0, 100), start, finish, from, done, started, elements?.Count ?? 0);
    }

    /// <summary>
    /// The summaries' progress from their leaves: the percentage weighted by the leaves' durations (an average when all are
    /// milestones), the first actual start, the last actual finish once every leaf is finished.
    /// </summary>
    public static void Rollup(Plan p)
    {
        foreach (var s in p.Rows.Where(t => p.IsSummary(t.Id)))
        {
            var leaves = p.LeavesOf(s.Id).Where(p.ById.ContainsKey).ToList();
            if (leaves.Count == 0) { p.Progress[s.Id] = None; continue; }
            double sum = 0, weights = 0, plain = 0;
            DateOnly? start = null, finish = null;
            var allFinished = true;
            int done = 0, started = 0, of = 0;
            foreach (var id in leaves)
            {
                var t = p.ById[id];
                var pr = p.ProgressOf(id);
                var w = t.Milestone ? 0 : Math.Max(0, t.Duration);
                sum += pr.Complete * w;
                weights += w;
                plain += pr.Complete;
                if (pr.ActualStart is { } a && (start == null || a < start)) start = a;
                if (!pr.Finished || pr.ActualFinish == null) allFinished = false;
                else if (finish == null || pr.ActualFinish > finish) finish = pr.ActualFinish;
                done += pr.Done; started += pr.Started; of += pr.Count;
            }
            var complete = weights > 1e-9 ? sum / weights : plain / leaves.Count;
            var any = complete > 1e-9 || start != null;
            p.Progress[s.Id] = any ? new TaskProgress(complete, start, allFinished ? finish : null, "children", done, started, of) : None with { Count = of };
        }
    }
}
