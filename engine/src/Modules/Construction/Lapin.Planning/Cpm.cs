using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// A task's times as the critical path gives them: early and late start (the start of a day) and early and late finish
/// (exclusive: the day after the last working day), the floats in working days, and whether it is critical. Its
/// <see cref="Start"/> and <see cref="Finish"/> are the dates the Gantt shows (the first and the last working day;
/// a milestone's both its day).
/// </summary>
public sealed record TaskTimes(DateOnly EarlyStart, DateOnly EarlyFinish, DateOnly LateStart, DateOnly LateFinish, int TotalFloat, int FreeFloat, bool Critical, int Duration)
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
/// </summary>
public static class Cpm
{
    /// <summary>The links as leaf-to-leaf edges: every leaf under the predecessor to every leaf under the successor.</summary>
    static List<(PlanLink Link, string From, string To)> Edges(Plan p)
    {
        var o = new List<(PlanLink, string, string)>();
        foreach (var l in p.Links)
            if (p.ById.ContainsKey(l.From) && p.ById.ContainsKey(l.To))
                foreach (var a in p.LeavesOf(l.From))
                    foreach (var b in p.LeavesOf(l.To))
                        if (a != b) o.Add((l, a, b));
        return o;
    }

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
            foreach (var a in p.LeavesOf(extra.From))
                foreach (var b in p.LeavesOf(extra.To))
                    edges.Add((extra, a, b));
        if (edges.Any(e => e.From == e.To)) return true;
        var leaves = p.Rows.Where(t => !p.IsSummary(t.Id)).Select(t => t.Id).ToList();
        return Topological(leaves, edges) == null;
    }

    public static CpmResult Solve(Plan p)
    {
        var cal = p.Calendar;
        var leaves = p.Rows.Where(t => !p.IsSummary(t.Id)).Select(t => t.Id).ToList();
        var edges = Edges(p);
        var order = Topological(leaves, edges) ?? throw new CommandException("The links make a loop: a task waits for itself.");
        var incoming = leaves.ToDictionary(x => x, _ => new List<(PlanLink Link, string From)>());
        var outgoing = leaves.ToDictionary(x => x, _ => new List<(PlanLink Link, string To)>());
        foreach (var (l, a, b) in edges) { incoming[b].Add((l, a)); outgoing[a].Add((l, b)); }

        int Dur(string id) => p.ById[id] is var t && t.Milestone ? 0 : Math.Max(0, t.Duration);
        bool Wet(string id) => p.ById[id].Wet;

        var es = new Dictionary<string, DateOnly>();
        var ef = new Dictionary<string, DateOnly>();
        var start = cal.NextWorking(p.Start);

        // A predecessor's start and finish as a link reads them: a summary's are its first leaf's start and its last leaf's finish.
        (DateOnly S, DateOnly F) Early(string task) =>
            p.IsSummary(task) ? (p.LeavesOf(task).Min(x => es[x]), p.LeavesOf(task).Max(x => ef[x])) : (es[task], ef[task]);

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
            var s = cal.NextWorking(earliest, wet);
            es[id] = s;
            ef[id] = d == 0 ? s : cal.Offset(s, d, wet);
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
            var d = Dur(id);
            var wet = Wet(id);
            var latest = finish;
            foreach (var (l, to) in outgoing[id])
            {
                var c = l.Type switch
                {
                    "SS" => Fwd(cal.Offset(ls[to], -l.Lag), d, wet),
                    "FF" => cal.Offset(lf[to], -l.Lag),
                    "SF" => Fwd(cal.Offset(lf[to], -l.Lag), d, wet),
                    _ => cal.Offset(ls[to], -l.Lag),
                };
                if (c < latest) latest = c;
            }
            if (t.Constraint == "mso") { ls[id] = es[id]; lf[id] = ef[id]; continue; }
            if (d == 0) { ls[id] = latest; lf[id] = latest; continue; }
            var end = cal.PrevEnd(latest, wet);
            lf[id] = end;
            ls[id] = cal.Offset(end, -d, wet);
        }

        // A latest start as the latest finish it allows: the start moved back onto a working day, then the duration on.
        DateOnly Fwd(DateOnly latestStart, int d, bool wet)
        {
            if (d == 0) return latestStart;
            var s = cal.PrevWorking(latestStart, wet);
            return cal.Offset(s, d, wet);
        }

        var times = new Dictionary<string, TaskTimes>();
        var criticalLinks = new HashSet<string>();
        foreach (var id in order)
        {
            var wet = Wet(id);
            var total = cal.WorkingDays(es[id], ls[id], wet);
            // Free float: how far it may slip before any successor does (or the project's finish, with none).
            var free = int.MaxValue;
            foreach (var (l, to) in outgoing[id])
            {
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
            if (outgoing[id].Count == 0) free = cal.WorkingDays(ef[id], finish, wet);
            free = Math.Max(0, Math.Min(free, Math.Max(total, 0)));
            times[id] = new TaskTimes(es[id], ef[id], ls[id], lf[id], total, free, total <= 0, Dur(id));
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
        // Summaries span their leaves.
        foreach (var t in p.Rows.Where(t => p.IsSummary(t.Id)))
        {
            var under = p.LeavesOf(t.Id).Where(times.ContainsKey).ToList();
            if (under.Count == 0) { times[t.Id] = new TaskTimes(start, start, start, start, 0, 0, false, 0); continue; }
            var s = under.Min(x => times[x].EarlyStart);
            var f = under.Max(x => times[x].EarlyFinish);
            var tf = under.Min(x => times[x].TotalFloat);
            times[t.Id] = new TaskTimes(s, f, under.Min(x => times[x].LateStart), under.Max(x => times[x].LateFinish), tf,
                under.Min(x => times[x].FreeFloat), tf <= 0, cal.WorkingDays(s, f));
        }
        return new CpmResult(times, finish, criticalLinks);
    }
}
