namespace Lapin.Planning;

/// <summary>What the levelling did: the start it gives each task it moved, the tasks that ask for more of a resource than there is (placed anyway), the finish before and after.</summary>
public sealed record LevelResult(IReadOnlyDictionary<string, DateOnly> Starts, IReadOnlyList<string> TooBig, DateOnly FinishBefore, DateOnly FinishAfter);

/// <summary>
/// Resource levelling (docs/plan-4d5d.md, stage 8; in the open module since decision 772), the serial way: the tasks not started are placed one by one, each once
/// every task before it is placed, the one that can start first going first (then the one with less float, then the row),
/// each at the first working day its links allow, as the critical path reads them, on which every resource it takes has
/// the units it needs on every day it holds them (its own days and the days after it keeps formwork). The tasks under way
/// or done, those that must start on a date and level of effort tasks stay where they are and take their units first. A task asking for more
/// than a resource has at all is placed by its links alone and named. The result is a start for each task it moved,
/// written as the task's levelling start, which the critical path then keeps: the same dates come out of it.
/// </summary>
public static class Levelling
{
    public static LevelResult Level(Plan p, CpmResult unlevelled, IReadOnlyList<ResourceDef> resources)
    {
        var cal = p.Calendar;
        var leaves = p.Rows.Where(t => !p.IsSummary(t.Id)).Select(t => t.Id).ToList();
        var t0 = unlevelled.Times;
        var need = leaves.ToDictionary(x => x, _ => new List<(ResourceDef R, Assignment A)>());
        foreach (var r in resources)
            foreach (var (task, a) in r.Assign)
                if (need.TryGetValue(task, out var l) && a.Units > 0) l.Add((r, a));
        var use = resources.ToDictionary(r => r.Id, _ => new Dictionary<DateOnly, double>());

        int Dur(string id) => p.ById[id] is var t && t.Milestone ? 0 : Math.Max(0, t.Duration);
        bool Wet(string id) => p.ById[id].Wet;

        // The edges between leaves as the critical path has them.
        var incoming = leaves.ToDictionary(x => x, _ => new List<(PlanLink Link, string From)>());
        var outgoing = leaves.ToDictionary(x => x, _ => new List<string>());
        foreach (var l in p.Links)
            if (p.ById.ContainsKey(l.From) && p.ById.ContainsKey(l.To))
                foreach (var a in p.LeavesOf(l.From))
                    foreach (var b in p.LeavesOf(l.To))
                        if (a != b && incoming.ContainsKey(b) && !Cpm.IsLoe(p, a) && !Cpm.IsLoe(p, b)) { incoming[b].Add((l, a)); outgoing[a].Add(b); }

        var es = new Dictionary<string, DateOnly>();
        var ef = new Dictionary<string, DateOnly>();
        var starts = new Dictionary<string, DateOnly>();
        var tooBig = new List<string>();

        void Take(string id, DateOnly start, int days)
        {
            foreach (var (r, a) in need[id])
                foreach (var d in Resources.Held(cal, start, days, Wet(id), a.Hold))
                    use[r.Id][d] = use[r.Id].GetValueOrDefault(d) + a.Units;
        }

        // Fixed first: under way, done, or due on a date.
        var open = new List<string>();
        foreach (var id in leaves)
        {
            var tm = t0[id];
            // A level of effort task stays on the dates it spans (it drives nothing).
            var fixedTask = tm.Status != "planned" || p.ById[id].Constraint == "mso" || Cpm.IsLoe(p, id);
            if (!fixedTask) { open.Add(id); continue; }
            es[id] = tm.EarlyStart; ef[id] = tm.EarlyFinish;
            Take(id, tm.EarlyStart, Resources.DaysOf(p, tm, id));
        }

        (DateOnly S, DateOnly F) Span(string task) =>
            p.IsSummary(task) ? (p.LeavesOf(task).Min(x => es[x]), p.LeavesOf(task).Max(x => ef[x])) : (es[task], ef[task]);
        DateOnly Back(DateOnly end, int d, bool wet) => d == 0 ? end : cal.Offset(end, -d, wet);

        var waiting = open.ToDictionary(x => x, x => incoming[x].Select(e => e.From).Where(f => !es.ContainsKey(f)).Distinct().Count());
        var ready = open.Where(x => waiting[x] == 0).ToList();
        var guard = 0;
        while (ready.Count > 0 && guard++ < 100_000)
        {
            var id = ready.OrderBy(x => t0[x].EarlyStart).ThenBy(x => t0[x].TotalFloat).ThenBy(x => p.RowOf[x]).First();
            ready.Remove(id);
            var d = Dur(id);
            var wet = Wet(id);
            var earliest = t0[id].EarlyStart;
            foreach (var (l, from) in incoming[id])
            {
                var src = p.IsSummary(l.From) && p.LeavesOf(l.From).All(es.ContainsKey) ? l.From : from;
                var (ps, pf) = Span(src);
                var c = l.Type switch
                {
                    "SS" => cal.Offset(ps, l.Lag),
                    "FF" => Back(cal.Offset(pf, l.Lag), d, wet),
                    "SF" => Back(cal.Offset(ps, l.Lag), d, wet),
                    _ => cal.Offset(pf, l.Lag),
                };
                if (c > earliest) earliest = c;
            }
            var s = cal.NextWorking(earliest, wet);
            var big = need[id].Any(x => x.A.Units > x.R.Capacity + 1e-9);
            if (big) tooBig.Add(id);
            else if (need[id].Count > 0)
            {
                for (var tries = 0; tries < 5000; tries++)
                {
                    var fits = need[id].All(x => Resources.Held(cal, s, d, wet, x.A.Hold).All(day => use[x.R.Id].GetValueOrDefault(day) + x.A.Units <= x.R.Capacity + 1e-9));
                    if (fits) break;
                    s = cal.NextWorking(s.AddDays(1), wet);
                }
            }
            es[id] = s;
            ef[id] = d == 0 ? s : cal.Offset(s, d, wet);
            Take(id, s, d);
            if (s > cal.NextWorking(t0[id].EarlyStart, wet)) starts[id] = s;
            foreach (var next in outgoing[id].Distinct())
                if (waiting.ContainsKey(next) && --waiting[next] == 0) ready.Add(next);
        }
        var before = unlevelled.Finish;
        var after = leaves.Count == 0 ? before : leaves.Max(x => ef.TryGetValue(x, out var f) ? f : t0[x].EarlyFinish);
        return new LevelResult(starts, tooBig, before, after > before ? after : before);
    }
}
