using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// A task's times as the critical path gives them (stage 5: its status, done, started or planned, and the working days it
/// has left): early and late start (the start of a day) and early and late finish
/// (exclusive: the day after the last working day), the floats in working days, and whether it is critical. Its
/// <see cref="Start"/> and <see cref="Finish"/> are the dates the Gantt shows (the first and the last working day;
/// a milestone's both its day).
/// </summary>
public sealed record TaskTimes(DateOnly EarlyStart, DateOnly EarlyFinish, DateOnly LateStart, DateOnly LateFinish, int TotalFloat, int FreeFloat, bool Critical, int Duration,
    string Status = "planned", int Remaining = 0)
{
    public DateOnly Start => EarlyStart;
    public DateOnly Finish => EarlyFinish > EarlyStart ? EarlyFinish.AddDays(-1) : EarlyStart;
}

/// <summary>What the critical path found: each task's times, the project's finish (exclusive), the links that drive their successor on the critical path.</summary>
public sealed record CpmResult(IReadOnlyDictionary<string, TaskTimes> Times, DateOnly Finish, IReadOnlySet<string> CriticalLinks);

/// <summary>
/// The critical path method on the work calendar (docs/plan-4d5d.md: worked out by the engine on every change, never
/// stored): a forward pass for the early dates, a backward pass for the late ones, the total and free floats, the
/// critical tasks (no total float). Links run between leaves: a summary task as a successor passes its constraint to every
/// task under it; as a predecessor it starts at its first task's start and ends at its last one's finish. Lags count
/// working days of the project's calendar; durations count the task's own (a wet task skips the winter break).
/// With progress (stage 5): a finished task stands on its actual dates (its finish worked out from its start when it has
/// none), off the critical path; a started one keeps its actual start and does what is left of it (its duration × the part
/// not done, rounded up) from the working day after the status date, or straight after the part done when there is no
/// status date; one not started starts no earlier than the day after the status date. The status date is the last day the
/// progress is reported through (MS Project's status date); the work left starts after it.
/// A task already started or done holds nothing back in the backward pass. A task not started keeps the start a resource
/// levelling gave it (<see cref="PlanTask.LevelStart"/>) unless asked for the schedule without it (<c>levelled: false</c>).
/// <para>
/// As late as possible (<c>constraint: "alap"</c>): once both passes are done, each such task not started is moved, the
/// last first, as late as its successors (at the dates they now have) and the project's finish let it, so it delays
/// nothing: a task with nothing after it goes to its late dates, one before others as far as their starts allow (its free
/// float used up: its free float is 0 then, its predecessors' grows). Its total float and critical flag stay those of the
/// as-soon-as-possible schedule, so the project's finish and its critical path do not change.
/// </para>
/// <para>
/// Level of effort (a hammock, <see cref="PlanTask.Loe"/>): left out of both passes (it drives nothing and nothing it
/// is linked to waits for it); then it starts at the start of its "from" task (<see cref="PlanTask.EffortFrom"/>; else the
/// earliest its SS or FS predecessors give, with their lags) and finishes at the finish of its "to" task
/// (<see cref="PlanTask.EffortTo"/>; else the latest its FF or SF predecessors give), at the dates those have once placed
/// (so it follows them when they move); with no finish given, its own duration on from its start; with no start, the
/// project's. It has no float and is never critical; its duration is the working days it spans. A summary over it spans it
/// too, but takes its floats and critical flag from its other tasks. A plan without either is worked out as before.
/// </para>
/// </summary>
public static class Cpm
{
    /// <summary>
    /// The links as leaf-to-leaf edges: every leaf under the predecessor to every leaf under the successor. A link between a
    /// summary and a task under it (never made by a command, but a file or an old project may hold one) means nothing: left out.
    /// </summary>
    static List<(PlanLink Link, string From, string To)> Edges(Plan p)
    {
        var o = new List<(PlanLink, string, string)>();
        foreach (var l in p.Links)
            if (p.ById.ContainsKey(l.From) && p.ById.ContainsKey(l.To) && !Plan.Nested(p, l))
                foreach (var a in Leaves(p, l.From))
                    foreach (var b in Leaves(p, l.To))
                        if (a != b) o.Add((l, a, b));
        return o;
    }

    /// <summary>Whether a task is a level of effort leaf (a summary marked so is a summary).</summary>
    public static bool IsLoe(Plan p, string id) => p.ById.TryGetValue(id, out var t) && t.Loe && !p.IsSummary(id);

    /// <summary>The leaves under a task that the passes schedule: level of effort tasks left out.</summary>
    static IEnumerable<string> Leaves(Plan p, string id) => p.LeavesOf(id).Where(x => !IsLoe(p, x));

    /// <summary>The leaves in an order where every predecessor comes first; null when the links make a loop.</summary>
    static List<string>? Topological(IReadOnlyList<string> leaves, List<(PlanLink Link, string From, string To)> edges)
    {
        var indeg = leaves.ToDictionary(x => x, _ => 0);
        var outs = leaves.ToDictionary(x => x, _ => new List<string>());
        foreach (var (_, a, b) in edges)
        {
            if (!indeg.ContainsKey(a) || !indeg.ContainsKey(b)) continue;
            outs[a].Add(b);
            indeg[b]++;
        }
        var queue = new Queue<string>(leaves.Where(x => indeg[x] == 0));
        var order = new List<string>(leaves.Count);
        while (queue.TryDequeue(out var x))
        {
            order.Add(x);
            foreach (var y in outs[x]) if (--indeg[y] == 0) queue.Enqueue(y);
        }
        return order.Count == leaves.Count ? order : null;
    }

    /// <summary>Whether the plan's links (with one more, when given) make a loop.</summary>
    public static bool HasLoop(Plan p, PlanLink? extra = null)
    {
        var edges = Edges(p);
        if (extra != null)
            foreach (var a in Leaves(p, extra.From))
                foreach (var b in Leaves(p, extra.To))
                    edges.Add((extra, a, b));
        if (edges.Any(e => e.From == e.To)) return true;
        var leaves = p.Rows.Where(t => !p.IsSummary(t.Id) && !IsLoe(p, t.Id)).Select(t => t.Id).ToList();
        return Topological(leaves, edges) == null;
    }

    public static CpmResult Solve(Plan p, bool levelled = true)
    {
        var cal = p.Calendar;
        var leaves = p.Rows.Where(t => !p.IsSummary(t.Id) && !IsLoe(p, t.Id)).Select(t => t.Id).ToList();
        var loes = p.Rows.Where(t => IsLoe(p, t.Id)).Select(t => t.Id).ToList();
        var edges = Edges(p);
        var order = Topological(leaves, edges) ?? throw new CommandException("The links make a loop: a task waits for itself.");
        var incoming = leaves.ToDictionary(x => x, _ => new List<(PlanLink Link, string From)>());
        var outgoing = leaves.ToDictionary(x => x, _ => new List<(PlanLink Link, string To)>());
        foreach (var (l, a, b) in edges) { incoming[b].Add((l, a)); outgoing[a].Add((l, b)); }

        int Dur(string id) => p.ById[id] is var t && t.Milestone ? 0 : Math.Max(0, t.Duration);
        bool Wet(string id) => p.ById[id].Wet;

        var es = new Dictionary<string, DateOnly>();
        var ef = new Dictionary<string, DateOnly>();
        // Stage 5: each leaf's status, the working days it has left and the day the work left starts (its start when not started).
        var status = new Dictionary<string, string>();
        var remaining = new Dictionary<string, int>();
        var resume = new Dictionary<string, DateOnly>();
        int Left(string id) => remaining.TryGetValue(id, out var r) ? r : Dur(id);
        var start = cal.NextWorking(p.Start);
        var dataDate = p.DataDate;

        // A predecessor's start and finish as a link reads them: a summary's are its first leaf's start and its last leaf's finish.
        (DateOnly S, DateOnly F) Early(string task) =>
            p.IsSummary(task) ? (Leaves(p, task).Min(x => es[x]), Leaves(p, task).Max(x => ef[x])) : (es[task], ef[task]);

        // ── Forward: each leaf as early as its links, its constraint and the project's start let it.
        foreach (var id in order)
        {
            var t = p.ById[id];
            var d = Dur(id);
            var wet = Wet(id);
            var earliest = start;
            foreach (var (l, from) in incoming[id])
            {
                // A link from a summary reads the summary as a whole (its leaves all came before: they are among this leaf's predecessors).
                var src = p.IsSummary(l.From) ? l.From : from;
                var (ps, pf) = src == from ? (es[from], ef[from]) : Early(src);
                var c = l.Type switch
                {
                    "SS" => cal.Offset(ps, l.Lag),
                    "FF" => Back(cal.Offset(pf, l.Lag), d, wet),
                    "SF" => Back(cal.Offset(ps, l.Lag), d, wet),
                    _ => cal.Offset(pf, l.Lag),
                };
                if (c > earliest) earliest = c;
            }
            if (t.Constraint == "snet" && t.ConstraintDate is { } snet && snet > earliest) earliest = snet;
            if (t.Constraint == "mso" && t.ConstraintDate is { } mso) earliest = mso;
            var pr = p.ProgressOf(id);
            if (pr.Finished)
            {
                // Done: on its actual dates (the finish from its start and duration when only the percentage says so).
                var s0 = pr.ActualStart ?? cal.NextWorking(earliest, wet);
                var f0 = pr.ActualFinish is { } af ? af.AddDays(1) : d == 0 ? s0 : cal.Offset(cal.NextWorking(s0, wet), d, wet);
                if (pr.ActualStart == null && f0 <= s0) s0 = f0.AddDays(-1);
                if (f0 < s0) f0 = s0;
                es[id] = s0; ef[id] = f0; resume[id] = f0;
                status[id] = "done"; remaining[id] = 0;
                continue;
            }
            if (pr.Begun)
            {
                // Started: from its actual start; what is left of it from the data date (or after the part done).
                var s0 = pr.ActualStart ?? cal.NextWorking(earliest, wet);
                var left = d == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(d * (100 - pr.Complete) / 100 - 1e-9));
                var r0 = dataDate is { } dd && dd >= s0 ? cal.NextWorking(dd.AddDays(1), wet) : cal.NextWorking(cal.Offset(cal.NextWorking(s0, wet), d - left, wet), wet);
                if (d == 0) r0 = s0;
                es[id] = s0; resume[id] = r0;
                ef[id] = left == 0 ? r0 : cal.Offset(r0, left, wet);
                status[id] = "started"; remaining[id] = left;
                continue;
            }
            // Not started: no earlier than the levelling's start, nor than the day after the status date.
            if (levelled && t.LevelStart is { } lv && lv > earliest) earliest = lv;
            if (dataDate is { } ddate && ddate.AddDays(1) > earliest) earliest = ddate.AddDays(1);
            var s = cal.NextWorking(earliest, wet);
            es[id] = s;
            ef[id] = d == 0 ? s : cal.Offset(s, d, wet);
            resume[id] = s;
            status[id] = "planned";
        }

        DateOnly Back(DateOnly end, int d, bool wet) => d == 0 ? end : cal.Offset(end, -d, wet);

        var finish = leaves.Count == 0 ? start : leaves.Max(x => ef[x]);
        if (finish < start) finish = start;

        // ── Backward: each leaf as late as its successors and the project's finish let it.
        var ls = new Dictionary<string, DateOnly>();
        var lf = new Dictionary<string, DateOnly>();
        for (var i = order.Count - 1; i >= 0; i--)
        {
            var id = order[i];
            var t = p.ById[id];
            var d = Left(id);
            var wet = Wet(id);
            if (status[id] == "done") { ls[id] = es[id]; lf[id] = ef[id]; continue; }
            var latest = finish;
            foreach (var (l, to) in outgoing[id])
            {
                // A successor already started or done holds nothing back.
                if (status[to] != "planned") continue;
                // A task under way has started: what ties its start (SS, SF) holds nothing back either.
                if (status[id] == "started" && l.Type is "SS" or "SF") continue;
                var c = l.Type switch
                {
                    "SS" => Fwd(cal.Offset(ls[to], -l.Lag), d, wet),
                    "FF" => cal.Offset(lf[to], -l.Lag),
                    "SF" => Fwd(cal.Offset(lf[to], -l.Lag), d, wet),
                    _ => cal.Offset(ls[to], -l.Lag),
                };
                if (c < latest) latest = c;
            }
            if (t.Constraint == "mso" && status[id] == "planned") { ls[id] = es[id]; lf[id] = ef[id]; continue; }
            if (d == 0) { ls[id] = latest; lf[id] = latest; continue; }
            var end = cal.PrevEnd(latest, wet);
            lf[id] = end;
            ls[id] = cal.Offset(end, -d, wet);
        }

        // A latest start as the latest finish it allows: the start moved onto a working day, then the duration on. A start
        // link with a negative lag gives the day after a working day (an exclusive end, a Saturday after a Friday): starting
        // then is starting on the next working day, so the day moves forward (a latest start already on a working day stays).
        DateOnly Fwd(DateOnly latestStart, int d, bool wet)
        {
            if (d == 0) return latestStart;
            var s = cal.NextWorking(latestStart, wet);
            return cal.Offset(s, d, wet);
        }

        // ── As late as possible: each task not started so marked, the last first, as late as its successors (where they are
        // now) and the project's finish let it. Its resume day (the total float's base) stays the early one.
        for (var i = order.Count - 1; i >= 0; i--)
        {
            var id = order[i];
            if (p.ById[id].Constraint != "alap" || status[id] != "planned") continue;
            var d = Dur(id);
            var wet = Wet(id);
            var latest = finish;
            foreach (var (l, to) in outgoing[id])
            {
                if (status[to] != "planned") continue;
                var c = l.Type switch
                {
                    "SS" => Fwd(cal.Offset(es[to], -l.Lag), d, wet),
                    "FF" => cal.Offset(ef[to], -l.Lag),
                    "SF" => Fwd(cal.Offset(ef[to], -l.Lag), d, wet),
                    _ => cal.Offset(es[to], -l.Lag),
                };
                if (c < latest) latest = c;
            }
            if (d == 0)
            {
                // A milestone on the last working day it may take (never before its early day).
                var m = latest;
                while (m > es[id] && !cal.Working(m, wet)) m = m.AddDays(-1);
                if (m > es[id]) { es[id] = m; ef[id] = m; }
                continue;
            }
            var end = cal.PrevEnd(latest, wet);
            var s = cal.Offset(end, -d, wet);
            if (s > es[id]) { es[id] = s; ef[id] = end; }
        }

        var times = new Dictionary<string, TaskTimes>();
        var criticalLinks = new HashSet<string>();
        foreach (var id in order)
        {
            var wet = Wet(id);
            if (status[id] == "done")
            {
                times[id] = new TaskTimes(es[id], ef[id], ls[id], lf[id], 0, 0, false, cal.WorkingDays(es[id], ef[id], wet), "done", 0);
                continue;
            }
            var total = cal.WorkingDays(resume[id], ls[id], wet);
            // Free float: how far it may slip before any successor does (or the project's finish, with none).
            var free = int.MaxValue;
            foreach (var (l, to) in outgoing[id])
            {
                if (status[to] != "planned") continue;
                var d = Dur(to);
                var need = l.Type switch
                {
                    "SS" => cal.Offset(es[id], l.Lag),
                    "FF" => Back(cal.Offset(ef[id], l.Lag), d, Wet(to)),
                    "SF" => Back(cal.Offset(es[id], l.Lag), d, Wet(to)),
                    _ => cal.Offset(ef[id], l.Lag),
                };
                var slack = cal.WorkingDays(need, es[to]);
                if (slack < free) free = slack;
            }
            if (free == int.MaxValue) free = cal.WorkingDays(ef[id], finish, wet);
            free = Math.Max(0, Math.Min(free, Math.Max(total, 0)));
            var started = status[id] == "started";
            times[id] = new TaskTimes(es[id], ef[id], ls[id], lf[id], total, free, total <= 0, started ? cal.WorkingDays(es[id], ef[id], wet) : Dur(id), status[id], Left(id));
        }
        // A link is critical when it drives a critical successor from a critical predecessor (no slack between them).
        foreach (var (l, a, b) in edges)
        {
            if (!times[a].Critical || !times[b].Critical) continue;
            var d = Dur(b);
            var need = l.Type switch
            {
                "SS" => cal.Offset(es[a], l.Lag),
                "FF" => Back(cal.Offset(ef[a], l.Lag), d, Wet(b)),
                "SF" => Back(cal.Offset(es[a], l.Lag), d, Wet(b)),
                _ => cal.Offset(ef[a], l.Lag),
            };
            if (cal.WorkingDays(need, es[b]) <= 0) criticalLinks.Add(l.Id);
        }
        // Level of effort: from its "from" task's start (or its SS / FS predecessors') to its "to" task's finish (or its FF / SF predecessors').
        if (loes.Count > 0)
        {
            (DateOnly S, DateOnly F)? Span(string task)
            {
                if (!p.ById.ContainsKey(task) || IsLoe(p, task)) return null;
                var under = Leaves(p, task).Where(es.ContainsKey).ToList();
                return under.Count == 0 ? null : (under.Min(x => es[x]), under.Max(x => ef[x]));
            }
            foreach (var id in loes)
            {
                var t = p.ById[id];
                var wet = Wet(id);
                var starts = new List<DateOnly>();
                var finishes = new List<DateOnly>();
                var drivers = new List<string>();
                if (t.EffortFrom is { } fromId && fromId != id && Span(fromId) is { } fs) { starts.Add(fs.S); drivers.Add(fromId); }
                if (t.EffortTo is { } toId && toId != id && Span(toId) is { } ts) { finishes.Add(ts.F); drivers.Add(toId); }
                foreach (var l in p.Links)
                {
                    if (l.To != id || l.From == id || Span(l.From) is not { } sp) continue;
                    drivers.Add(l.From);
                    switch (l.Type)
                    {
                        case "SS" when t.EffortFrom == null: starts.Add(cal.Offset(sp.S, l.Lag)); break;
                        case "FS" when t.EffortFrom == null: starts.Add(cal.Offset(sp.F, l.Lag)); break;
                        case "FF" when t.EffortTo == null: finishes.Add(cal.Offset(sp.F, l.Lag)); break;
                        case "SF" when t.EffortTo == null: finishes.Add(cal.Offset(sp.S, l.Lag)); break;
                    }
                }
                var s = cal.NextWorking(starts.Count > 0 ? starts.Min() : start, wet);
                var f = finishes.Count > 0 ? finishes.Max() : Dur(id) == 0 ? s : cal.Offset(s, Dur(id), wet);
                if (f < s) f = s;
                var under = drivers.SelectMany(x => Leaves(p, x)).Where(status.ContainsKey).Distinct().ToList();
                var st = under.Count > 0 && under.All(x => status[x] == "done") ? "done" : under.Any(x => status[x] != "planned") ? "started" : "planned";
                var days = cal.WorkingDays(s, f, wet);
                var left = st == "done" ? 0 : st == "started" && dataDate is { } dd && dd >= s ? Math.Max(0, cal.WorkingDays(dd.AddDays(1), f, wet)) : days;
                times[id] = new TaskTimes(s, f, s, f, 0, 0, false, days, st, left);
            }
        }
        // Summaries span their leaves.
        foreach (var t in p.Rows.Where(t => p.IsSummary(t.Id)))
        {
            var under = p.LeavesOf(t.Id).Where(times.ContainsKey).ToList();
            if (under.Count == 0) { times[t.Id] = new TaskTimes(start, start, start, start, 0, 0, false, 0); continue; }
            var s = under.Min(x => times[x].EarlyStart);
            var f = under.Max(x => times[x].EarlyFinish);
            var open = under.Where(x => times[x].Status != "done" && !IsLoe(p, x)).ToList();
            var tf = open.Count > 0 ? open.Min(x => times[x].TotalFloat) : 0;
            var st = under.All(x => times[x].Status == "done") ? "done" : under.Any(x => times[x].Status != "planned") ? "started" : "planned";
            times[t.Id] = new TaskTimes(s, f, under.Min(x => times[x].LateStart), under.Max(x => times[x].LateFinish), tf,
                open.Count > 0 ? open.Min(x => times[x].FreeFloat) : 0, open.Count > 0 && tf <= 0, cal.WorkingDays(s, f), st, open.Count > 0 ? open.Max(x => times[x].Remaining) : 0);
        }
        return new CpmResult(times, finish, criticalLinks);
    }
}
