using System.Globalization;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>
/// Cost over time on paper and in files (docs/plan-4d5d.md, stage 7): the month table as a form of the estimate (a
/// schedule to place on sheets, a sheet of the workbook, pages of the PDF), the S-curve drawn by the engine as vectors
/// (the estimate placed on a sheet is its curve), and the workbook and PDF of both.
/// </summary>
public static class CostTimeOutputs
{
    static string Money2(double v, bool raw) => raw ? Math.Round(v, 2).ToString(CultureInfo.InvariantCulture) : v.ToString("0.00", CultureInfo.InvariantCulture);

    static string Month(DateOnly m) => m.ToString("MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>The month table as a form: planned by chapter and month, then the work done; a line when there is no schedule.</summary>
    public static CostForm Monthly(IQueryContext q, EstimateResult r, bool raw)
    {
        const string Title = "Cost per month";
        if (q.Services.Get<IWorkSchedules>()?.Times(q) is not { } times)
            return new CostForm("monthly", Title, "", [Title], [new FormRow(["No work schedule yet: Planning → Generate schedule… makes one from the model."])], new HashSet<int>());
        var o = CostOverTime.Of(r, times);
        var t = o.Table();
        var n = t.Months.Count;
        var columns = new List<string> { "Chapter" };
        columns.AddRange(t.Months.Select(Month));
        columns.Add("Not scheduled");
        columns.Add("Total");
        var numeric = Enumerable.Range(1, n + 2).ToHashSet();
        string M(double v) => Money2(v, raw);
        string[] Row(string label, IEnumerable<double> months, double? notScheduled, double? total) =>
            [label, .. months.Select(M), notScheduled is { } ns ? M(ns) : "", total is { } tt ? M(tt) : ""];
        string[] Heading(string label) => [label, .. Enumerable.Repeat("", n + 2)];
        var rows = new List<FormRow> { new(Heading("Planned"), null, "chapter") };
        foreach (var c in t.Chapters) rows.Add(new FormRow(Row(c.Name, c.Months, c.NotScheduled, c.Total)));
        var direct = t.Direct;
        rows.Add(new FormRow(Row("Direct costs", direct.Take(n), direct[n], r.Totals.Direct.Direct), null, "subtotal"));
        rows.Add(new FormRow(Row("Total without VAT", t.Net.Take(n), t.Net[n], r.Totals.Net), null, "subtotal"));
        rows.Add(new FormRow(Row("Total with VAT", t.WithVat.Take(n), t.WithVat[n], r.Totals.Total), null, "total"));
        double run = 0;
        rows.Add(new FormRow(Row("Cumulative direct costs", direct.Take(n).Select(v => run = Money.Round(run + v)), null, null), null, "subtotal"));
        if (t.DoneTotals.Any(v => v != 0))
        {
            rows.Add(new FormRow(Heading(times.DataDate is { } dd ? $"Done (status date {dd.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)})" : "Done"), null, "chapter"));
            foreach (var c in t.Done.Where(c => c.Total != 0)) rows.Add(new FormRow(Row(c.Name, c.Months, null, c.Total)));
            rows.Add(new FormRow(Row("Direct costs done", t.DoneTotals, null, Money.Round(t.DoneTotals.Sum())), null, "total"));
            double done = 0;
            rows.Add(new FormRow(Row("Cumulative done", t.DoneTotals.Select(v => done = Money.Round(done + v)), null, null), null, "subtotal"));
        }
        var subtitle = $"{Sheets.ProjectName(q)} · {r.Entity.Name} · {times.Name} · {r.Settings.Currency}, direct costs by chapter";
        return new CostForm("monthly", Title, subtitle, columns, rows, numeric);
    }

    /// <summary>The curve and the months as a workbook: the S-curve a day, the month table, the tasks' costs.</summary>
    public static byte[] Xlsx(IQueryContext q, CostOverTime o)
    {
        var (from, to) = o.Span;
        var planned = CostOverTime.Cumulative(o.Planned.Values, from, to);
        var baseline = o.Times.Baseline ? CostOverTime.Cumulative([o.Baseline], from, to) : null;
        var through = o.DoneThrough;
        var done = through is { } dt ? CostOverTime.Cumulative(o.DoneByDay.Values, from, dt < to ? dt : to) : [];
        var curve = new List<IReadOnlyList<string>> { new[] { "Date", "Planned (cumulative)", "Baseline (cumulative)", "Done (cumulative)" } };
        for (var i = 0; i < planned.Length; i++)
            curve.Add([CostOverTime.WorkDate(from.AddDays(i)), Money2(planned[i], true), baseline != null ? Money2(baseline[i], true) : "", i < done.Length ? Money2(done[i], true) : ""]);
        var tasks = new List<IReadOnlyList<string>> { new[] { "WBS", "Task", "Start", "Finish", "Cost", "% complete", "Cost to date" } };
        foreach (var t in o.Times.Tasks)
            tasks.Add([t.Wbs, new string(' ', 2 * t.Level) + t.Name, CostOverTime.WorkDate(t.Start), CostOverTime.WorkDate(t.Finish),
                Money2(o.Cost.GetValueOrDefault(t.Id), true), Math.Round(t.Complete, 1).ToString(CultureInfo.InvariantCulture), Money2(o.Done.GetValueOrDefault(t.Id), true)]);
        var month = Monthly(q, o.Estimate, true);
        return XlsxWriter.Write([("S-curve", curve), ("Monthly", [[month.Title], [month.Subtitle], .. month.Table]), ("Tasks", tasks)]);
    }

    /// <summary>The curve on an A4 landscape page, then the month table's pages.</summary>
    public static byte[] Pdf(IQueryContext q, CostOverTime o)
    {
        const double W = 297, H = 210, M = 12;
        var pens = CostCurveSheet.Draw(o, W - 2 * M, H - 2 * M, M, M, Sheets.ProjectName(q));
        return PdfWriter.Write([new PaperPage(W, H, pens, "S-curve"), .. CostForms.PdfPages([Monthly(q, o.Estimate, false)])]);
    }
}

/// <summary>
/// The S-curve on a sheet (an estimate placed on a sheet is its curve over time): the planned cumulative cost as a line,
/// the baseline's dashed, the work done's thick to the status date, the months' planned and done costs as bars against the
/// right-hand scale, the status date dashed, a legend; the totals over it.
/// </summary>
public sealed class CostCurveSheet : ISheetContent
{
    public const string PlannedColor = "#2f6db3", BaselineColor = "#8a949e", DoneColor = "#2e8b57", BarColor = "#c9d9ec", DoneBarColor = "#8fc9a8";

    public bool Handles(Entity e) => e.Class == Costing.Class;

    public IReadOnlyList<(double W, double H, List<Pen> Pens)> Pages(IQueryContext q, Entity e, double maxWidth, double? maxHeight)
    {
        var w = Math.Max(150, maxWidth);
        var h = Math.Min(maxHeight ?? w * 0.55, w * 0.62);
        var (o, why) = CostOverTime.For(q, e.Id);
        if (o == null)
        {
            var text = new Pen("TEXT", "#000000", 0, null, null);
            text.Texts.Add(new PenText(new Vec2(0, 6), e.Name, 5, Anchor: "start", Bold: true));
            text.Texts.Add(new PenText(new Vec2(0, 14), why ?? "", 3, Anchor: "start"));
            return [(w, 20, [text])];
        }
        return [(w, h, Draw(o, w, h, 0, 0, Sheets.ProjectName(q)))];
    }

    /// <summary>A step for an axis of <paramref name="max"/>: 1, 2 or 5 times a power of ten, about <paramref name="ticks"/> of them.</summary>
    public static double NiceStep(double max, int ticks = 5)
    {
        if (!(max > 0)) return 1;
        var raw = max / ticks;
        var p = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var f = raw / p;
        return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10) * p;
    }

    /// <summary>A sum on an axis: thousands as k, millions as M.</summary>
    public static string Short(double v) =>
        Math.Abs(v) >= 1e6 ? (v / 1e6).ToString("0.##", CultureInfo.InvariantCulture) + " M"
        : Math.Abs(v) >= 1e3 ? (v / 1e3).ToString("0.##", CultureInfo.InvariantCulture) + " k"
        : v.ToString("0.##", CultureInfo.InvariantCulture);

    static string Money(double v) => v.ToString("N2", CultureInfo.InvariantCulture).Replace(",", " ");

    public static List<Pen> Draw(CostOverTime o, double w, double h, double ox, double oy, string project)
    {
        var text = new Pen("TEXT", "#000000", 0, null, null);
        var axis = new Pen("COST-AXIS", "#000000", 0.25, null, null);
        var grid = new Pen("COST-GRID", "#cccccc", 0.1, null, null);
        var bars = new Pen("COST-BAR", null, 0, CostCurveSheet.BarColor, null);
        var doneBars = new Pen("COST-BAR", null, 0, DoneBarColor, null);
        var planned = new Pen("COST-PLANNED", PlannedColor, 0.5, null, null);
        var baseline = new Pen("COST-BASELINE", BaselineColor, 0.35, null, [2, 1]);
        var done = new Pen("COST-DONE", DoneColor, 0.8, null, null);
        var status = new Pen("COST-STATUS", "#d64545", 0.25, null, [2, 1]);
        Vec2 P(double x, double y) => new(ox + x, oy + y);
        void T(double x, double y, string s, double size, string anchor = "start", bool bold = false) => text.Texts.Add(new PenText(P(x, y), s, size, Anchor: anchor, Bold: bold));

        var cur = o.Currency;
        T(0, 6, $"Cost over time · {o.Estimate.Entity.Name}", 5, bold: true);
        T(w, 6, $"{project} · {o.Times.Name}", 3, "end");
        T(0, 12, $"Budget {Money(o.Budget)} {cur} (direct costs) · scheduled {Money(o.Scheduled)} · to date {Money(o.ToDate)}", 2.6);

        const double Left = 22, Right = 20, Top = 18, Bottom = 16;
        var x0 = Left; var x1 = w - Right; var y0 = Top; var y1 = h - Bottom;
        var (from, to) = o.Span;
        var days = Math.Max(1, to.DayNumber - from.DayNumber + 1);
        double X(DateOnly d) => x0 + (d.DayNumber - from.DayNumber) * (x1 - x0) / days;
        var plannedCum = CostOverTime.Cumulative(o.Planned.Values, from, to);
        var baseCum = o.Times.Baseline ? CostOverTime.Cumulative([o.Baseline], from, to) : null;
        var through = o.DoneThrough;
        var doneCum = through is { } th && th >= from ? CostOverTime.Cumulative(o.DoneByDay.Values, from, th < to ? th : to) : [];
        var top = Math.Max(o.Budget, new[] { plannedCum, baseCum ?? [], doneCum }.SelectMany(a => a).DefaultIfEmpty(0).Max());
        var step = NiceStep(top);
        var yMax = Math.Max(step, Math.Ceiling(top / step) * step);
        double Y(double v) => y1 - v / yMax * (y1 - y0);
        var table = o.Table();
        var months = table.Months;
        var monthMax = Math.Max(table.Direct.Take(months.Count).DefaultIfEmpty(0).Max(), table.DoneTotals.DefaultIfEmpty(0).Max());
        var mStep = NiceStep(monthMax);
        var mMax = Math.Max(mStep, Math.Ceiling(monthMax / mStep) * mStep);
        double YM(double v) => y1 - v / mMax * (y1 - y0);

        // The grid and the scales: the cumulative on the left, a month's on the right.
        for (var v = 0.0; v <= yMax + 1e-6; v += step)
        {
            grid.Segments.Add((P(x0, Y(v)), P(x1, Y(v))));
            T(x0 - 1.5, Y(v) + 0.8, Short(v), 2.2, "end");
        }
        for (var v = 0.0; v <= mMax + 1e-6; v += mStep) T(x1 + 1.5, YM(v) + 0.8, Short(v), 2.2);
        T(x0 - 1.5, y0 - 3, $"{cur}, cumulative", 2.2, "end");
        T(x1 + 1.5, y0 - 3, $"{cur} a month", 2.2);
        axis.Segments.Add((P(x0, y0), P(x0, y1)));
        axis.Segments.Add((P(x1, y0), P(x1, y1)));
        axis.Segments.Add((P(x0, y1), P(x1, y1)));

        // The months: their bars (planned, and done beside it) and their names.
        var every = Math.Max(1, (int)Math.Ceiling(months.Count * 12.0 / (x1 - x0)));
        for (var i = 0; i < months.Count; i++)
        {
            var a = X(months[i] < from ? from : months[i]);
            var end = months[i].AddMonths(1);
            var b = X(end > to.AddDays(1) ? to.AddDays(1) : end);
            var wBar = (b - a) * 0.36;
            var mid = (a + b) / 2;
            if (table.Direct[i] > 0) bars.Loops.Add([P(mid - wBar, YM(table.Direct[i])), P(mid, YM(table.Direct[i])), P(mid, y1), P(mid - wBar, y1)]);
            if (table.DoneTotals[i] > 0) doneBars.Loops.Add([P(mid, YM(table.DoneTotals[i])), P(mid + wBar, YM(table.DoneTotals[i])), P(mid + wBar, y1), P(mid, y1)]);
            grid.Segments.Add((P(a, y1), P(a, y1 + 1.5)));
            if (i % every == 0) T(mid, y1 + 4.5, months[i].ToString("MM.yy", CultureInfo.InvariantCulture), 2.2, "middle");
        }

        void Line(Pen pen, double[] cum)
        {
            Vec2? last = P(x0, Y(0));
            for (var i = 0; i < cum.Length; i++)
            {
                var p = P(X(from.AddDays(i + 1)), Y(cum[i]));
                if (last is { } l) pen.Segments.Add((l, p));
                last = p;
            }
        }
        if (baseCum != null) Line(baseline, baseCum);
        Line(planned, plannedCum);
        Line(done, doneCum);
        if (o.Times.DataDate is { } dd && dd >= from && dd <= to)
        {
            var xs = X(dd.AddDays(1));
            status.Segments.Add((P(xs, y0), P(xs, y1)));
            T(xs, y0 - 1, dd.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), 2.2, "middle");
        }

        // The legend, under the scale.
        var lx = x0;
        var ly = h - 3;
        void Key(Pen pen, string label, bool bar = false)
        {
            if (bar) pen.Loops.Add([P(lx, ly - 2.2), P(lx + 5, ly - 2.2), P(lx + 5, ly), P(lx, ly)]);
            else pen.Segments.Add((P(lx, ly - 1), P(lx + 6, ly - 1)));
            T(lx + 7.5, ly, label, 2.4);
            lx += 9 + label.Length * 1.3;
        }
        Key(planned, "Planned, cumulative");
        if (baseCum != null) Key(baseline, "Baseline, cumulative");
        Key(done, "Done, cumulative");
        Key(bars, "Planned a month", true);
        Key(doneBars, "Done a month", true);
        return [grid, bars, doneBars, axis, baseline, planned, done, status, text];
    }
}
