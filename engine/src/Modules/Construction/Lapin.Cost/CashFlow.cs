using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>A certificate (statement of works) as the cash flow makes it: what it certifies, holds back and recovers, what is paid for it and when.</summary>
public sealed record CashCertificate(int No, DateOnly Date, double Certified, double Retention, double Recovered, double Net, double Vat, DateOnly Due, bool Actual);

/// <summary>A period of the cash flow (a month or a week, by its first day).</summary>
public sealed record CashPeriod(DateOnly Start, double Cost, double Certified, double Retention, double Recovered, double In, double Out, double Net, double Cumulative,
    double RetentionHeld, double AdvanceLeft);

/// <summary>
/// The cash flow (docs/plan-4d5d.md, cash flow): the payments laid on the costs over time, worked out on every ask from
/// the planned schedule and the contract's terms (<see cref="CashFlowSettings"/>). Each day's direct cost is certified at
/// the contract's value (its share of it: the value certified over the direct costs) in the certificate of its period;
/// each certificate holds back the retention and recovers the advance pro rata (the last recovers what is left), and is
/// paid its net (and VAT) the payment term after it. The advance is paid on its day, the retention released in two parts
/// (at the taking-over, after the defects period), each paid the payment term after. The costs paid out are the costs
/// incurred (direct, or all but the profit) the suppliers' term after. In − out a period is its net, its running total
/// the cash-flow curve; the lowest it goes (when under nought) is the funding the contractor needs, and when.
/// A module measuring progress (<see cref="ICertifiedProgress"/>) may give the certificates actually made: those replace
/// the planned ones up to the last's date, the planned work after it carrying the rest of the value.
/// </summary>
public sealed class CashFlow
{
    public required CashFlowSettings Settings { get; init; }
    public required string Period { get; init; }
    public required string Currency { get; init; }
    public double ContractValue { get; init; }
    public double CostTotal { get; init; }
    public double Advance { get; init; }
    public DateOnly AdvanceDate { get; init; }
    public DateOnly Takeover { get; init; }
    public DateOnly DefectsEnd { get; init; }
    public List<CashCertificate> Certificates { get; } = [];
    /// <summary>The retention's releases: when paid, how much (VAT apart).</summary>
    public List<(DateOnly Due, double Value, string What)> Releases { get; } = [];
    public List<CashPeriod> Periods { get; } = [];
    /// <summary>The lowest running total (0 when it never goes under) and the period it is reached in.</summary>
    public double Peak { get; private set; }
    public DateOnly? PeakPeriod { get; private set; }
    public bool HasActual => Certificates.Any(c => c.Actual);

    static DateOnly MonthEnd(DateOnly d) => new DateOnly(d.Year, d.Month, 1).AddMonths(1).AddDays(-1);

    /// <summary>The certificate a day's work goes in: the first certificate day on or after it.</summary>
    public static DateOnly CertificateOf(DateOnly d, CashFlowSettings s)
    {
        if (s.Cycle == "weekly") return d.AddDays((7 - (int)d.DayOfWeek) % 7);
        if (s.CertificateDay <= 0) return MonthEnd(d);
        var here = new DateOnly(d.Year, d.Month, s.CertificateDay);
        return here >= d ? here : here.AddMonths(1);
    }

    /// <summary>A period's first day: the month's, or the week's Monday.</summary>
    public static DateOnly PeriodOf(DateOnly d, string period) =>
        period == "week" ? d.AddDays(-(((int)d.DayOfWeek + 6) % 7)) : new DateOnly(d.Year, d.Month, 1);

    static DateOnly NextPeriod(DateOnly p, string period) => period == "week" ? p.AddDays(7) : p.AddMonths(1);

    /// <summary>
    /// The cash flow from a series of direct costs a day: <paramref name="contractValue"/> certified in proportion to them
    /// over <paramref name="direct"/> (the estimate's direct costs, what no task builds included), <paramref name="costOut"/>
    /// paid out likewise.
    /// </summary>
    public static CashFlow Compute(CashFlowSettings s, IReadOnlyDictionary<DateOnly, double> costByDay, double direct, double contractValue, double costOut,
        double vatRate, DateOnly advanceDate, DateOnly takeover, string currency, string period = "month", IReadOnlyList<CertifiedValue>? actual = null)
    {
        var o = new CashFlow
        {
            Settings = s, Period = period == "week" ? "week" : "month", Currency = currency, ContractValue = Money.Round(contractValue), CostTotal = Money.Round(costOut),
            AdvanceDate = advanceDate, Takeover = takeover, DefectsEnd = takeover.AddMonths(s.DefectsMonths),
            Advance = Money.Round(contractValue * s.AdvancePercent / 100),
        };
        var vat = s.Vat ? vatRate / 100 : 0;
        var valueRate = direct > 0 ? contractValue / direct : 0;
        var outRate = direct > 0 ? costOut / direct : 0;

        // The certificates: planned ones by period, the actual ones (when given) in their stead up to the last's date.
        var planned = new SortedDictionary<DateOnly, double>();
        foreach (var (d, v) in costByDay) { var c = CertificateOf(d, s); planned[c] = planned.GetValueOrDefault(c) + v * valueRate; }
        var certs = new List<(DateOnly Date, double Value, bool Actual)>();
        if (actual is { Count: > 0 })
        {
            var last = actual.Max(a => a.Date);
            var done = actual.Sum(a => a.Value);
            var after = planned.Where(kv => kv.Key > last).ToList();
            var left = after.Sum(kv => kv.Value);
            var scale = left > 0 ? Math.Max(0, contractValue - done) / left : 0;
            certs.AddRange(actual.OrderBy(a => a.Date).Select(a => (a.Date, a.Value, true)));
            certs.AddRange(after.Select(kv => (kv.Key, kv.Value * scale, false)));
        }
        else certs.AddRange(planned.Select(kv => (kv.Key, kv.Value, false)));
        // Rounded on the running total, so the certificates add up to the value certified.
        var gross = CostOverTime.Rounded(certs.Select(c => c.Value).ToList());
        double recovered = 0, held = 0;
        var kept = Enumerable.Range(0, certs.Count).Where(i => gross[i] != 0).ToList();
        for (var k = 0; k < kept.Count; k++)
        {
            var i = kept[k];
            var g = gross[i];
            var hold = Money.Round(g * s.RetentionPercent / 100);
            var rec = k == kept.Count - 1 ? Money.Round(o.Advance - recovered)
                : Math.Min(Money.Round(o.Advance - recovered), contractValue > 0 ? Money.Round(g * o.Advance / contractValue) : 0);
            recovered = Money.Round(recovered + rec);
            held = Money.Round(held + hold);
            var net = Money.Round(g - hold - rec);
            o.Certificates.Add(new CashCertificate(k + 1, certs[i].Date, g, hold, rec, net, Money.Round(net * vat), certs[i].Date.AddDays(s.PaymentDays), certs[i].Actual));
        }
        if (held != 0)
        {
            var first = Money.Round(held * s.RetentionAtTakeover / 100);
            if (first != 0) o.Releases.Add((takeover.AddDays(s.PaymentDays), first, "takeover"));
            if (Money.Round(held - first) != 0) o.Releases.Add((o.DefectsEnd.AddDays(s.PaymentDays), Money.Round(held - first), "defects"));
        }

        // The money a day: in (advance, certificates, releases), out (the costs, the suppliers' term after), with VAT.
        var inDay = new SortedDictionary<DateOnly, double>();
        var outDay = new SortedDictionary<DateOnly, double>();
        var costDay = new SortedDictionary<DateOnly, double>();
        void Add(SortedDictionary<DateOnly, double> into, DateOnly d, double v) { if (v != 0) into[d] = into.GetValueOrDefault(d) + v; }
        if (o.Advance != 0) Add(inDay, advanceDate, o.Advance * (1 + vat));
        foreach (var c in o.Certificates) Add(inDay, c.Due, c.Net + c.Vat);
        foreach (var r in o.Releases) Add(inDay, r.Due, r.Value + Money.Round(r.Value * vat));
        foreach (var (d, v) in costByDay)
        {
            Add(costDay, d, v * outRate);
            if (s.CostSide) Add(outDay, d.AddDays(s.SupplierDays), v * outRate * (1 + vat));
        }

        // The periods, first to last: every day anything happens in.
        var days = inDay.Keys.Concat(outDay.Keys).Concat(costDay.Keys).Concat(o.Certificates.Select(c => c.Date)).ToList();
        if (days.Count == 0) return o;
        var from = PeriodOf(days.Min(), o.Period);
        var to = PeriodOf(days.Max(), o.Period);
        var starts = new List<DateOnly>();
        for (var p = from; p <= to; p = NextPeriod(p, o.Period)) starts.Add(p);
        var index = starts.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i);
        double[] Bucket(IEnumerable<KeyValuePair<DateOnly, double>> series)
        {
            var b = new double[starts.Count];
            foreach (var (d, v) in series) b[index[PeriodOf(d, o.Period)]] += v;
            return CostOverTime.Rounded(b);
        }
        var ins = Bucket(inDay);
        var outs = Bucket(outDay);
        var costs = Bucket(costDay);
        var cert = new double[starts.Count];
        var ret = new double[starts.Count];
        var rcv = new double[starts.Count];
        foreach (var c in o.Certificates)
        {
            var i = index[PeriodOf(c.Date, o.Period)];
            cert[i] = Money.Round(cert[i] + c.Certified);
            ret[i] = Money.Round(ret[i] + c.Retention);
            rcv[i] = Money.Round(rcv[i] + c.Recovered);
        }
        double cum = 0, heldRun = 0, advLeft = 0;
        o.Peak = 0;
        for (var i = 0; i < starts.Count; i++)
        {
            var end = NextPeriod(starts[i], o.Period);
            var net = Money.Round(ins[i] - outs[i]);
            cum = Money.Round(cum + net);
            heldRun = Money.Round(heldRun + ret[i] - o.Releases.Where(r => r.Due >= starts[i] && r.Due < end).Sum(r => r.Value));
            if (advanceDate >= starts[i] && advanceDate < end) advLeft = Money.Round(advLeft + o.Advance);
            advLeft = Money.Round(advLeft - rcv[i]);
            o.Periods.Add(new CashPeriod(starts[i], costs[i], cert[i], ret[i], rcv[i], ins[i], outs[i], net, cum, heldRun, advLeft));
            if (cum < o.Peak) { o.Peak = cum; o.PeakPeriod = starts[i]; }
        }
        return o;
    }

    /// <summary>The direct costs a day of the planned schedule, all chapters.</summary>
    public static SortedDictionary<DateOnly, double> PlannedByDay(CostOverTime c)
    {
        var o = new SortedDictionary<DateOnly, double>();
        foreach (var s in c.Planned.Values) foreach (var (d, v) in s) o[d] = o.GetValueOrDefault(d) + v;
        return o;
    }

    /// <summary>The cash flow of the costs over time on the estimate's terms.</summary>
    public static CashFlow Of(CostOverTime c, CashFlowSettings s, string period = "month", IReadOnlyList<CertifiedValue>? actual = null)
    {
        var t = c.Estimate.Totals;
        var value = s.Basis switch { "direct" => t.Direct.Direct, "contract" => s.ContractValue ?? t.Net, _ => t.Net };
        var costOut = s.CostBasis == "direct" ? t.Direct.Direct : Money.Round(t.Net - t.Profit);
        var times = c.Times;
        var advance = s.AdvanceTask is { } at && times.Task(at) is { } atk ? atk.Start : Day(s.AdvanceDate) ?? times.Start;
        var takeover = s.TakeoverTask is { } tt && times.Task(tt) is { } ttk ? ttk.Finish : Day(s.TakeoverDate) ?? times.Finish;
        return Compute(s, PlannedByDay(c), t.Direct.Direct, value, costOut, c.Estimate.Settings.Vat, advance, takeover, c.Currency, period, actual);
    }

    static DateOnly? Day(string? s) => s != null && DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    /// <summary>The project's cash flow (the estimate's terms on its schedule); null with why when there is no estimate or schedule.</summary>
    public static (CashFlow? Value, string? Why) For(IQueryContext q, string? estimateId = null, string period = "month")
    {
        var (c, why) = CostOverTime.For(q, estimateId);
        if (c == null) return (null, why);
        var actual = q.Services.Get<ICertifiedProgress>()?.Certificates(q, c.Estimate.Entity.Id);
        return (Of(c, CashFlowSettings.Of(c.Estimate.Entity), period, actual), null);
    }

    /// <summary>The cash flow as a form of the estimate (a schedule on a sheet); a line when there is no schedule.</summary>
    public static CostForm FormOf(IQueryContext q, EstimateResult r, bool raw)
    {
        if (q.Services.Get<IWorkSchedules>()?.Times(q) is not { } times)
            return new CostForm("cashflow", "Cash flow", "", ["Cash flow"], [new FormRow(["No work schedule yet: Planning → Generate schedule… makes one from the model."])], new HashSet<int>());
        var c = CostOverTime.Of(r, times);
        var actual = q.Services.Get<ICertifiedProgress>()?.Certificates(q, r.Entity.Id);
        return Of(c, CashFlowSettings.Of(r.Entity), "month", actual).Form(q, r, raw);
    }

    static string D(DateOnly d) => CostOverTime.WorkDate(d);

    public JsonObject ToJson(string estimate) => new()
    {
        ["estimate"] = estimate, ["currency"] = Currency, ["period"] = Period, ["settings"] = Settings.ToJson(),
        ["contractValue"] = ContractValue, ["costTotal"] = CostTotal, ["advance"] = Advance, ["advanceDate"] = D(AdvanceDate),
        ["takeover"] = D(Takeover), ["defectsEnd"] = D(DefectsEnd), ["actual"] = HasActual,
        ["certified"] = Money.Round(Certificates.Sum(c => c.Certified)), ["retention"] = Money.Round(Certificates.Sum(c => c.Retention)),
        ["cashIn"] = Money.Round(Periods.Sum(p => p.In)), ["cashOut"] = Money.Round(Periods.Sum(p => p.Out)),
        ["peak"] = Peak, ["peakPeriod"] = PeakPeriod is { } pp ? D(pp) : null,
        ["periods"] = new JsonArray(Periods.Select(p => (JsonNode)new JsonObject
        {
            ["start"] = D(p.Start), ["cost"] = p.Cost, ["certified"] = p.Certified, ["retention"] = p.Retention, ["recovered"] = p.Recovered,
            ["in"] = p.In, ["out"] = p.Out, ["net"] = p.Net, ["cumulative"] = p.Cumulative, ["retentionHeld"] = p.RetentionHeld, ["advanceLeft"] = p.AdvanceLeft,
        }).ToArray()),
        ["certificates"] = new JsonArray(Certificates.Select(c => (JsonNode)new JsonObject
        {
            ["no"] = c.No, ["date"] = D(c.Date), ["certified"] = c.Certified, ["retention"] = c.Retention, ["recovered"] = c.Recovered,
            ["net"] = c.Net, ["vat"] = c.Vat, ["due"] = D(c.Due), ["actual"] = c.Actual,
        }).ToArray()),
        ["releases"] = new JsonArray(Releases.Select(r => (JsonNode)new JsonObject { ["due"] = D(r.Due), ["value"] = r.Value, ["what"] = r.What }).ToArray()),
    };

    // ------------------------------------------------------------------ the form, the workbook, the CSV

    public string Label(DateOnly p) => Period == "week" ? p.ToString("dd.MM.yy", CultureInfo.InvariantCulture) : p.ToString("MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>The rows of the period table: a label and a value a period, then the total (none for running balances).</summary>
    public IEnumerable<(string Label, double[] Values, double? Total, string? Style)> Rows()
    {
        double[] Of(Func<CashPeriod, double> f) => Periods.Select(f).ToArray();
        (string, double[], double?, string?) Sum(string l, Func<CashPeriod, double> f, string? style = null) { var v = Of(f); return (l, v, Money.Round(v.Sum()), style); }
        yield return Sum("Cost incurred", p => p.Cost);
        yield return Sum("Certified", p => p.Certified);
        yield return Sum("Retention held", p => p.Retention);
        yield return Sum("Advance recovered", p => p.Recovered);
        yield return Sum("Cash in", p => p.In, "subtotal");
        if (Settings.CostSide) yield return Sum("Cash out", p => p.Out, "subtotal");
        yield return Sum("Net cash flow", p => p.Net, "total");
        yield return ("Cumulative", Of(p => p.Cumulative), null, "subtotal");
        yield return ("Retention balance", Of(p => p.RetentionHeld), null, null);
        if (Advance != 0) yield return ("Advance to recover", Of(p => p.AdvanceLeft), null, null);
    }

    /// <summary>The cash flow as a form of the estimate: the period table, then the certificates (a schedule on a sheet, a sheet of the workbook).</summary>
    public CostForm Form(IQueryContext q, EstimateResult r, bool raw)
    {
        var n = Periods.Count;
        var columns = new List<string> { "" };
        columns.AddRange(Periods.Select(p => Label(p.Start)));
        columns.Add("Total");
        string M(double v) => MoneyText.Plain(v, raw);
        var rows = new List<FormRow>();
        foreach (var (label, values, total, style) in Rows())
            rows.Add(new FormRow([label, .. values.Select(M), total is { } t ? M(t) : ""], null, style ?? "line"));
        rows.Add(new FormRow([PeakText(), .. Enumerable.Repeat("", n + 1)], null, "chapter"));
        rows.Add(new FormRow(["Certificates", .. Enumerable.Repeat("", n + 1)], null, "chapter"));
        foreach (var c in Certificates)
            rows.Add(new FormRow([$"No. {c.No} · {c.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}{(c.Actual ? " (actual)" : "")}: certified {M(c.Certified)}, retention {M(c.Retention)}, advance {M(c.Recovered)}, net {M(c.Net)}{(Settings.Vat ? $", VAT {M(c.Vat)}" : "")}, paid {c.Due.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}",
                .. Enumerable.Repeat("", n + 1)]));
        foreach (var x in Releases)
            rows.Add(new FormRow([$"Retention released ({(x.What == "takeover" ? "taking-over" : "end of the defects period")}): {M(x.Value)}, paid {x.Due.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}", .. Enumerable.Repeat("", n + 1)]));
        var subtitle = $"{Sheets.ProjectName(q)} · {r.Entity.Name} · {r.Settings.Currency}{(Settings.Vat ? ", with VAT" : ", without VAT")} · planned schedule{(Settings.Indicative ? " · indicative terms" : "")}";
        return new CostForm("cashflow", "Cash flow", subtitle, columns, rows, Enumerable.Range(1, n + 1).ToHashSet());
    }

    public string PeakText() => PeakPeriod is { } p
        ? $"Peak funding need: {MoneyText.Grouped(-Peak)} {Currency} in {Label(p)}"
        : "No funding needed: the cumulative cash flow never goes under nought.";

    /// <summary>The period table as CSV (a semicolon between fields, numbers with a point).</summary>
    public string Csv()
    {
        static string Q(string s) => s.Contains(';') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        var lines = new List<string> { string.Join(";", new[] { "Period" }.Concat(Rows().Select(r => r.Label)).Select(Q)) };
        var rows = Rows().ToList();
        for (var i = 0; i < Periods.Count; i++)
            lines.Add(string.Join(";", new[] { D(Periods[i].Start) }.Concat(rows.Select(r => MoneyText.Plain(r.Values[i], true)))));
        return string.Join("\n", lines) + "\n";
    }

    /// <summary>The cash flow as a workbook: the periods, the certificates.</summary>
    public byte[] Xlsx(IQueryContext q, EstimateResult r)
    {
        var form = Form(q, r, true);
        var certs = new List<IReadOnlyList<string>> { new[] { "No.", "Date", "Certified", "Retention", "Advance recovered", "Net", "VAT", "Paid", "Actual" } };
        foreach (var c in Certificates)
            certs.Add([c.No.ToString(CultureInfo.InvariantCulture), D(c.Date), MoneyText.Plain(c.Certified, true), MoneyText.Plain(c.Retention, true), MoneyText.Plain(c.Recovered, true),
                MoneyText.Plain(c.Net, true), MoneyText.Plain(c.Vat, true), D(c.Due), c.Actual ? "yes" : ""]);
        foreach (var x in Releases) certs.Add(["", "", "", x.What == "takeover" ? "Released at the taking-over" : "Released after the defects period", "", MoneyText.Plain(x.Value, true), "", D(x.Due), ""]);
        var periods = form.Table.Take(1 + Rows().Count()).ToList();
        return Lapin.Drawing.Export.XlsxWriter.Write([("Cash flow", [[form.Title], [form.Subtitle], .. periods, [PeakText()]]), ("Certificates", certs)]);
    }
}
