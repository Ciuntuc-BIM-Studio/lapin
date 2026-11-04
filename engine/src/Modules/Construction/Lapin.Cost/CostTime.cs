using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>
/// Cost over time (docs/plan-4d5d.md, stage 7): the estimate laid on the work schedule, with no linking of its own. Each
/// line's value is shared by its elements (by what each gives it) and each element's share by the tasks that build the
/// part measured (a layer by the task that takes that layer, a whole element by every task that takes some of it, alike);
/// a line of the user's by the tasks it names. A task's cost is spread evenly over its working days (a wet task's skip the
/// winter break; a milestone's falls on its day), giving the planned curve; the same on the baseline's dates gives the
/// baseline's; the part of each task done (its cost × its percentage), spread from its actual start to its actual finish
/// or the status date, gives the curve of the work done. What no task builds is "not scheduled". All in direct costs, the
/// estimate's own; the month table adds the value without VAT and with it, in proportion.
/// </summary>
public sealed class CostOverTime
{
    public required EstimateResult Estimate { get; init; }
    public required ScheduleTimes Times { get; init; }
    /// <summary>Each task's cost and the part of it done (a summary's: its tasks').</summary>
    public Dictionary<string, double> Cost { get; } = [];
    public Dictionary<string, double> Done { get; } = [];
    /// <summary>The leaf tasks each line is done in, by its key.</summary>
    public Dictionary<string, List<string>> LineTasks { get; } = [];
    /// <summary>Each chapter's cost that no task builds.</summary>
    public Dictionary<string, double> Unscheduled { get; } = [];
    /// <summary>Each chapter's cost by day: planned (the schedule's dates), done (from progress); the baseline's, all chapters.</summary>
    public Dictionary<string, SortedDictionary<DateOnly, double>> Planned { get; } = [];
    public Dictionary<string, SortedDictionary<DateOnly, double>> DoneByDay { get; } = [];
    public SortedDictionary<DateOnly, double> Baseline { get; } = [];

    public double Budget => Estimate.Totals.Direct.Direct;
    public double Scheduled => Planned.Values.Sum(d => d.Values.Sum());
    public double ToDate => Times.Tasks.Where(t => !t.Summary).Sum(t => Done.GetValueOrDefault(t.Id));
    public string Currency => Estimate.Settings.Currency;

    /// <summary>The first and last days the curves cover: the schedule's, its baseline's and the work done's.</summary>
    public (DateOnly From, DateOnly To) Span
    {
        get
        {
            var days = Planned.Values.Concat(DoneByDay.Values).Append(Baseline).SelectMany(d => d.Keys).ToList();
            var from = days.Count > 0 ? days.Min() : Times.Start;
            var to = days.Count > 0 ? days.Max() : Times.Finish;
            return (from < Times.Start ? from : Times.Start, to > Times.Finish ? to : Times.Finish);
        }
    }

    /// <summary>The day the work done is reported through: the status date, else the last day some work was done on.</summary>
    public DateOnly? DoneThrough => Times.DataDate ?? (DoneByDay.Values.SelectMany(d => d.Keys).DefaultIfEmpty().Max() is var last && last != default ? last : null);

    static void Spread(SortedDictionary<DateOnly, double> into, IReadOnlyList<DateOnly> days, double value)
    {
        if (value == 0 || days.Count == 0) return;
        var each = value / days.Count;
        foreach (var d in days) into[d] = into.GetValueOrDefault(d) + each;
    }

    public static CostOverTime Of(EstimateResult est, ScheduleTimes times)
    {
        var o = new CostOverTime { Estimate = est, Times = times };
        var leaves = times.Tasks.Where(t => !t.Summary).ToDictionary(t => t.Id);
        var kids = times.Tasks.Where(t => t.Parent != null).GroupBy(t => t.Parent!).ToDictionary(g => g.Key, g => g.Select(t => t.Id).ToList());
        IEnumerable<string> LeavesOf(string id) => leaves.ContainsKey(id) ? [id] : kids.TryGetValue(id, out var k) ? k.SelectMany(LeavesOf) : [];
        // Each leaf's cost by chapter.
        var byTask = new Dictionary<string, Dictionary<string, double>>();
        foreach (var c in est.Chapters)
            foreach (var l in c.Lines)
            {
                var parts = new List<(double Fraction, IReadOnlyList<string> Tasks)>();
                var named = l.Tasks.SelectMany(LeavesOf).Distinct().ToList();
                if (named.Count > 0) parts.Add((1, named));
                else if (l.Shares.Count > 0)
                {
                    var sum = l.Shares.Sum(x => x.Quantity);
                    foreach (var sh in l.Shares) parts.Add((sum > 0 ? sh.Quantity / sum : 1.0 / l.Shares.Count, times.TasksOf(sh.Element, sh.Part)));
                }
                else if (l.Elements.Count > 0)
                    foreach (var el in l.Elements) parts.Add((1.0 / l.Elements.Count, times.TasksOf(el, null)));
                else parts.Add((1, []));
                var used = new List<string>();
                foreach (var (fraction, tasks) in parts)
                {
                    var v = l.Value * fraction;
                    var ts = tasks.Where(leaves.ContainsKey).ToList();
                    if (ts.Count == 0) { o.Unscheduled[c.Name] = o.Unscheduled.GetValueOrDefault(c.Name) + v; continue; }
                    foreach (var t in ts)
                    {
                        var m = byTask.TryGetValue(t, out var x) ? x : byTask[t] = [];
                        m[c.Name] = m.GetValueOrDefault(c.Name) + v / ts.Count;
                        if (!used.Contains(t)) used.Add(t);
                    }
                }
                o.LineTasks[l.Key] = used;
            }
        foreach (var t in leaves.Values)
        {
            var chapters = byTask.GetValueOrDefault(t.Id) ?? [];
            var cost = chapters.Values.Sum();
            o.Cost[t.Id] = cost;
            var done = cost * Math.Clamp(t.Complete, 0, 100) / 100;
            o.Done[t.Id] = done;
            if (cost == 0) continue;
            var days = times.WorkedDays(t.Start, t.Finish, t.Wet);
            if (days.Count == 0 || t.Milestone) days = [t.Start];
            foreach (var (ch, v) in chapters) Spread(o.Planned.TryGetValue(ch, out var p) ? p : o.Planned[ch] = [], days, v);
            if (t.BaselineStart is { } bs && t.BaselineFinish is { } bf)
            {
                var bd = times.WorkedDays(bs, bf < bs ? bs : bf, t.Wet);
                Spread(o.Baseline, bd.Count > 0 ? bd : [bs], cost);
            }
            if (done > 0)
            {
                var from = t.ActualStart ?? t.Start;
                var to = t.ActualFinish ?? times.DataDate ?? t.Finish;
                if (to < from) to = from;
                var dd = times.WorkedDays(from, to, t.Wet);
                if (dd.Count == 0) dd = [to];
                foreach (var (ch, v) in chapters) Spread(o.DoneByDay.TryGetValue(ch, out var p) ? p : o.DoneByDay[ch] = [], dd, v * done / cost);
            }
        }
        // The summaries carry their tasks'.
        foreach (var t in times.Tasks.Where(t => t.Summary))
        {
            var under = LeavesOf(t.Id).ToList();
            o.Cost[t.Id] = under.Sum(x => o.Cost.GetValueOrDefault(x));
            o.Done[t.Id] = under.Sum(x => o.Done.GetValueOrDefault(x));
        }
        return o;
    }

    /// <summary>The project's estimate on its schedule; null (with why) when either is missing.</summary>
    public static (CostOverTime? Value, string? Why) For(IQueryContext q, string? estimateId = null)
    {
        if (Costing.EstimateOf(q.Model, estimateId) is not { } e) return (null, "No estimate yet: Cost → Estimate makes one.");
        if (q.Services.Get<IWorkSchedules>() is not { } ws) return (null, "Cost over time needs the work schedule: the Planning module is not in this edition.");
        if (ws.Times(q) is not { } times) return (null, "No work schedule yet: Planning → Generate schedule… makes one from the model.");
        return (Of(Costing.Compute(q, e), times), null);
    }

    // ------------------------------------------------------------------ curves and months

    /// <summary>A series' running total at the end of each day from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static double[] Cumulative(IEnumerable<SortedDictionary<DateOnly, double>> series, DateOnly from, DateOnly to)
    {
        var n = Math.Max(0, to.DayNumber - from.DayNumber + 1);
        var daily = new double[n];
        double before = 0;
        foreach (var s in series)
            foreach (var (d, v) in s)
            {
                var i = d.DayNumber - from.DayNumber;
                if (i < 0) before += v; else if (i < n) daily[i] += v;
            }
        var o = new double[n];
        var run = before;
        for (var i = 0; i < n; i++) { run += daily[i]; o[i] = Money.Round(run); }
        return o;
    }

    /// <summary>The months the curves cover, first to last (their first days).</summary>
    public List<DateOnly> Months()
    {
        var (from, to) = Span;
        var o = new List<DateOnly>();
        for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1)) o.Add(m);
        return o;
    }

    static double[] ByMonth(SortedDictionary<DateOnly, double>? s, IReadOnlyList<DateOnly> months)
    {
        var o = new double[months.Count];
        if (s == null || months.Count == 0) return o;
        foreach (var (d, v) in s)
        {
            var i = (d.Year - months[0].Year) * 12 + d.Month - months[0].Month;
            o[Math.Clamp(i, 0, months.Count - 1)] += v;
        }
        return o;
    }

    /// <summary>Exact values rounded to cents so that they add up to their total rounded: each the running total's step.</summary>
    public static double[] Rounded(IReadOnlyList<double> exact)
    {
        var o = new double[exact.Count];
        double run = 0, shown = 0;
        for (var i = 0; i < exact.Count; i++)
        {
            run += exact[i];
            var r = Money.Round(run);
            o[i] = Money.Round(r - shown);
            shown = r;
        }
        return o;
    }

    /// <summary>
    /// The month table: each chapter's cost a month (planned, then done), what no task builds, its total (the chapter's
    /// direct costs: the rows add up to the estimate); and the months' direct costs, value without VAT and with VAT, in
    /// proportion to the direct costs, each row adding up to the estimate's own total.
    /// </summary>
    public MonthTable Table()
    {
        var months = Months();
        var t = Estimate.Totals;
        var chapters = new List<MonthRow>();
        var done = new List<MonthRow>();
        foreach (var c in Estimate.Chapters)
        {
            var planned = ByMonth(Planned.GetValueOrDefault(c.Name), months);
            var cells = Rounded(planned);
            var scheduled = Money.Round(cells.Sum());
            var direct = c.Sums.Direct;
            chapters.Add(new MonthRow(c.Name, cells, Money.Round(direct - scheduled), direct));
            var d = Rounded(ByMonth(DoneByDay.GetValueOrDefault(c.Name), months));
            done.Add(new MonthRow(c.Name, d, 0, Money.Round(d.Sum())));
        }
        double[] Column(IEnumerable<MonthRow> rows, int i) => [Money.Round(rows.Sum(r => r.Months[i]))];
        var directs = Enumerable.Range(0, months.Count).Select(i => Column(chapters, i)[0]).Append(Money.Round(chapters.Sum(r => r.NotScheduled))).ToList();
        double[] Scaled(double total) => t.Direct.Direct > 0 ? Rounded(directs.Select(v => v * total / t.Direct.Direct).ToList()) : new double[directs.Count];
        var net = Scaled(t.Net);
        var vat = Scaled(t.Total);
        var doneTotals = Enumerable.Range(0, months.Count).Select(i => Column(done, i)[0]).ToArray();
        return new MonthTable(months, chapters, done, directs.ToArray(), net, vat, doneTotals);
    }

    /// <summary>The curves and the months as the view draws them.</summary>
    public JsonObject ToJson()
    {
        var (from, to) = Span;
        var planned = Cumulative(Planned.Values, from, to);
        var through = DoneThrough;
        var doneTo = through is { } dt ? (dt < from ? from.AddDays(-1) : dt > to ? to : dt) : from.AddDays(-1);
        var done = Cumulative(DoneByDay.Values, from, doneTo);
        var table = Table();
        static JsonArray Nums(IEnumerable<double> v) => new(v.Select(x => (JsonNode)Money.Round(x)).ToArray());
        double cumP = 0, cumD = 0;
        var baseMonths = ByMonth(Baseline, table.Months);
        double cumB = 0;
        return new JsonObject
        {
            ["currency"] = Currency, ["budget"] = Budget, ["scheduled"] = Money.Round(Scheduled), ["unscheduled"] = Money.Round(Unscheduled.Values.Sum()),
            ["toDate"] = Money.Round(ToDate), ["net"] = Estimate.Totals.Net, ["total"] = Estimate.Totals.Total,
            ["schedule"] = Times.Name, ["start"] = WorkDate(Times.Start), ["finish"] = WorkDate(Times.Finish),
            ["dataDate"] = Times.DataDate is { } d ? WorkDate(d) : null, ["doneThrough"] = through is { } th ? WorkDate(th) : null,
            ["from"] = WorkDate(from), ["to"] = WorkDate(to),
            ["planned"] = Nums(planned),
            ["baseline"] = Times.Baseline ? Nums(Cumulative([Baseline], from, to)) : null,
            ["done"] = Nums(done),
            ["months"] = new JsonArray(table.Months.Select((m, i) =>
            {
                cumP += table.Direct[i];
                cumD += table.DoneTotals[i];
                cumB += baseMonths[i];
                var o = new JsonObject
                {
                    ["month"] = m.ToString("yyyy-MM", CultureInfo.InvariantCulture), ["planned"] = table.Direct[i], ["done"] = table.DoneTotals[i],
                    ["plannedCum"] = Money.Round(cumP), ["doneCum"] = Money.Round(cumD),
                };
                if (Times.Baseline) o["baselineCum"] = Money.Round(cumB);
                return (JsonNode)o;
            }).ToArray()),
            ["chapters"] = new JsonArray(table.Chapters.Select((c, i) => (JsonNode)new JsonObject
            {
                ["name"] = c.Name, ["months"] = Nums(c.Months), ["done"] = Nums(table.Done[i].Months), ["notScheduled"] = c.NotScheduled, ["total"] = c.Total,
            }).ToArray()),
            ["tasks"] = new JsonArray(Times.Tasks.Select(t => (JsonNode)new JsonObject { ["id"] = t.Id, ["cost"] = Money.Round(Cost.GetValueOrDefault(t.Id)), ["done"] = Money.Round(Done.GetValueOrDefault(t.Id)) }).ToArray()),
        };
    }

    public static string WorkDate(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>A chapter's row of the month table: its value a month, what no task builds, its total.</summary>
public sealed record MonthRow(string Name, double[] Months, double NotScheduled, double Total);

/// <summary>The month table: the months, the chapters planned and done, and the months' direct costs (the last: not scheduled), without VAT and with it, and done.</summary>
public sealed record MonthTable(IReadOnlyList<DateOnly> Months, IReadOnlyList<MonthRow> Chapters, IReadOnlyList<MonthRow> Done, double[] Direct, double[] Net, double[] WithVat, double[] DoneTotals);

/// <summary>The cost module's answer to the Gantt (through the platform): each task's cost and the part of it done, the budget, the cost to date.</summary>
public sealed class ScheduleCostsOf : IScheduleCosts
{
    public ScheduleCosts? Costs(IQueryContext query, ScheduleTimes times)
    {
        if (Costing.EstimateOf(query.Model) is not { } e) return null;
        var o = CostOverTime.Of(Costing.Compute(query, e), times);
        return new ScheduleCosts(o.Currency, o.Cost.ToDictionary(kv => kv.Key, kv => new TaskCost(Money.Round(kv.Value), Money.Round(o.Done.GetValueOrDefault(kv.Key)))),
            o.Budget, Money.Round(o.Scheduled), Money.Round(o.ToDate));
    }
}
