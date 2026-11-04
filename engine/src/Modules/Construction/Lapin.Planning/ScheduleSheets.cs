using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// The Gantt on a sheet (docs/plan-4d5d.md, stage 4: the execution chart for the documentation): the project's
/// IfcWorkSchedule is a sheet content like a schedule, drawn by the engine as vectors — the task table (row, WBS and name,
/// duration, start, finish) and the bars on a time scale of months and weeks (or years and months when the works are long),
/// the critical tasks in red, summaries as brackets, milestones as diamonds, the links as elbowed lines, the status date
/// dashed — paged under a repeated header when it is taller than the frame, so the PDF prints what the Gantt shows.
/// </summary>
public sealed class GanttSheet : ISheetContent
{
    const double RowH = 5, HeadH = 10, TitleH = 9, Text = 2.2;
    const string Bar = "#5b85b3", Critical = "#d64545", Grid = "#c8c8c8";

    public bool Handles(Entity e) => e.Class == Plan.ScheduleClass;

    static string Dmy(DateOnly d) => d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>A text cut to what fits a column (2.2 mm text: about half a millimetre a character less than its height).</summary>
    static string Fit(string s, double width) { var n = Math.Max(3, (int)(width / (Text * 0.52))); return s.Length <= n ? s : s[..(n - 1)] + "…"; }

    public IReadOnlyList<(double W, double H, List<Pen> Pens)> Pages(IQueryContext q, Entity e, double maxWidth, double? maxHeight)
    {
        var p = PlanningModule.ReadResolved(q);
        var (r, _) = PlanningModule.Solve(p);
        var width = Math.Max(200, maxWidth);
        // The table's columns: row, task (WBS and name), duration, start, finish; the task column gives way on narrow paper.
        var taskW = Math.Clamp(width * 0.22, 40, 80);
        double[] cols = [8, taskW, 13, 19, 19];
        var tableW = cols.Sum();
        var chartW = width - tableW;
        var finish = r.Times.Count > 0 ? r.Times.Values.Max(t => t.Finish) : p.Start;
        var from = p.Start.AddDays(-2);
        var to = finish.AddDays(4);
        var days = Math.Max(7, to.DayNumber - from.DayNumber);
        var k = chartW / days;
        double X(DateOnly d) => tableW + (d.DayNumber - from.DayNumber) * k;
        var perPage = maxHeight is { } mh ? Math.Max(1, (int)((mh - TitleH - HeadH) / RowH)) : int.MaxValue;
        var rows = p.Rows.ToList();
        var pages = new List<(double, double, List<Pen>)>();
        var start = 0;
        do
        {
            var slice = rows.Skip(start).Take(perPage).ToList();
            pages.Add(Page(p, r, slice, start, pages.Count == 0, cols, tableW, width, from, to, k, X));
            start += perPage;
        } while (start < rows.Count);
        return pages;
    }

    (double W, double H, List<Pen> Pens) Page(Plan p, CpmResult r, List<PlanTask> rows, int first, bool firstPage, double[] cols, double tableW, double width,
        DateOnly from, DateOnly to, double k, Func<DateOnly, double> X)
    {
        var text = new Pen("TEXT", "#000000", 0, null, null);
        var rule = new Pen("GANTT", "#000000", 0.25, null, null);
        var thin = new Pen("GANTT-GRID", Grid, 0.1, null, null);
        var bars = new Pen("GANTT-BAR", "#000000", 0.1, Bar, null);
        var critical = new Pen("GANTT-BAR", "#000000", 0.1, Critical, null);
        var black = new Pen("GANTT-BAR", "#000000", 0.1, "#000000", null);
        var links = new Pen("GANTT-LINK", "#555555", 0.15, null, null);
        var status = new Pen("GANTT-STATUS", Critical, 0.25, null, [2, 1]);
        // Stage 5: the baseline as a thin grey bar under each task's, the part done as a dark band through its bar.
        var baselinePen = new Pen("GANTT-BASELINE", "#7d8791", 0.05, "#aab2bb", null);
        var donePen = new Pen("GANTT-PROGRESS", "#000000", 0, "#26313d", null);
        var y0 = TitleH;
        text.Texts.Add(new PenText(new Vec2(0, 6), firstPage ? p.Name : $"{p.Name} (continued)", 5, Anchor: "start", Bold: true));
        var finish = r.Times.Count > 0 ? r.Times.Values.Max(t => t.Finish) : p.Start;
        text.Texts.Add(new PenText(new Vec2(width, 6), $"{Dmy(p.Calendar.NextWorking(p.Start))} – {Dmy(finish)}", 3, Anchor: "end"));
        // The header: the columns' names, the time scale's two tiers.
        string[] names = ["#", "Task", "Days", "Start", "Finish"];
        var x = 0.0;
        for (var i = 0; i < cols.Length; i++)
        {
            text.Texts.Add(new PenText(new Vec2(x + 1, y0 + HeadH - 2), names[i], Text, Anchor: "start", Bold: true));
            x += cols[i];
            rule.Segments.Add((new Vec2(x, y0), new Vec2(x, y0 + HeadH + rows.Count * RowH)));
        }
        var weeks = k * 7 >= 7;
        // Top tier: months (weeks below) or years (months below).
        var bodyBottom = y0 + HeadH + rows.Count * RowH;
        for (var d = weeks ? new DateOnly(from.Year, from.Month, 1) : new DateOnly(from.Year, 1, 1); d <= to; d = weeks ? d.AddMonths(1) : d.AddYears(1))
        {
            var xs = Math.Max(tableW, X(d));
            if (xs > width) break;
            rule.Segments.Add((new Vec2(xs, y0), new Vec2(xs, y0 + HeadH / 2)));
            var label = weeks ? d.ToString("MM.yyyy", CultureInfo.InvariantCulture) : d.ToString("yyyy", CultureInfo.InvariantCulture);
            if (width - xs > 12) text.Texts.Add(new PenText(new Vec2(xs + 1, y0 + HeadH / 2 - 1.2), label, Text, Anchor: "start"));
        }
        var tick = weeks ? from.AddDays((8 - (int)from.DayOfWeek) % 7) : new DateOnly(from.Year, from.Month, 1).AddMonths(1);
        for (var d = tick; d <= to; d = weeks ? d.AddDays(7) : d.AddMonths(1))
        {
            var xs = X(d);
            if (xs > width) break;
            thin.Segments.Add((new Vec2(xs, y0 + HeadH / 2), new Vec2(xs, bodyBottom)));
            var label = weeks ? d.ToString("dd.MM", CultureInfo.InvariantCulture) : d.ToString("MM", CultureInfo.InvariantCulture);
            if ((weeks ? 7 : 30) * k >= label.Length * Text * 0.55 + 1) text.Texts.Add(new PenText(new Vec2(xs + 0.6, y0 + HeadH - 1.2), label, Text * 0.85, Anchor: "start"));
        }
        rule.Segments.Add((new Vec2(0, y0), new Vec2(width, y0)));
        rule.Segments.Add((new Vec2(tableW, y0 + HeadH / 2), new Vec2(width, y0 + HeadH / 2)));
        rule.Segments.Add((new Vec2(0, y0 + HeadH), new Vec2(width, y0 + HeadH)));
        rule.Segments.Add((new Vec2(0, bodyBottom), new Vec2(width, bodyBottom)));
        rule.Segments.Add((new Vec2(0, y0), new Vec2(0, bodyBottom)));
        rule.Segments.Add((new Vec2(width, y0), new Vec2(width, bodyBottom)));
        // The rows: the table's cells, the bars.
        var rowOf = new Dictionary<string, int>();
        for (var i = 0; i < rows.Count; i++)
        {
            var t = rows[i];
            rowOf[t.Id] = i;
            var tm = r.Times[t.Id];
            var top = y0 + HeadH + i * RowH;
            var baseLine = top + RowH - 1.4;
            var summary = p.IsSummary(t.Id);
            var milestone = !summary && (t.Milestone || t.Duration == 0);
            if (i > 0) thin.Segments.Add((new Vec2(0, top), new Vec2(tableW, top)));
            var indent = 1 + 2.5 * p.Level.GetValueOrDefault(t.Id);
            text.Texts.Add(new PenText(new Vec2(1, baseLine), $"{first + i + 1}", Text, Anchor: "start"));
            text.Texts.Add(new PenText(new Vec2(cols[0] + indent, baseLine), Fit($"{p.Wbs[t.Id]} {t.Name}", cols[1] - indent - 1), Text, Anchor: "start", Bold: summary));
            var c2 = cols[0] + cols[1];
            text.Texts.Add(new PenText(new Vec2(c2 + cols[2] - 1, baseLine), $"{(summary ? tm.Duration : milestone ? 0 : t.Duration)}", Text, Anchor: "end"));
            text.Texts.Add(new PenText(new Vec2(c2 + cols[2] + 1, baseLine), Dmy(tm.Start), Text, Anchor: "start"));
            text.Texts.Add(new PenText(new Vec2(c2 + cols[2] + cols[3] + 1, baseLine), Dmy(tm.Finish), Text, Anchor: "start"));
            var xa = X(tm.Start);
            var xb = X(tm.Finish.AddDays(1));
            var cy = top + RowH / 2;
            if (milestone)
                black.Loops.Add([new(xa, cy - 1.6), new(xa + 1.6, cy), new(xa, cy + 1.6), new(xa - 1.6, cy)]);
            else if (summary)
                black.Loops.Add([new(xa, cy - 1.2), new(xb, cy - 1.2), new(xb, cy + 1.4), new(xb - 1, cy + 0.4), new(xa + 1, cy + 0.4), new(xa, cy + 1.4)]);
            else
                (tm.Critical ? critical : bars).Loops.Add([new(xa, cy - 1.3), new(Math.Max(xb, xa + 0.4), cy - 1.3), new(Math.Max(xb, xa + 0.4), cy + 1.3), new(xa, cy + 1.3)]);
            if (!milestone && p.ProgressOf(t.Id).Complete is var pc && pc > 0)
            {
                var xd = xa + (Math.Max(xb, xa + 0.4) - xa) * Math.Min(100, pc) / 100;
                donePen.Loops.Add([new(xa, cy - 0.45), new(xd, cy - 0.45), new(xd, cy + 0.45), new(xa, cy + 0.45)]);
            }
            if (t.Baseline is { } bl)
            {
                var ba = X(bl.Start);
                var bb = Math.Max(X(bl.Finish.AddDays(1)), ba + 0.4);
                baselinePen.Loops.Add([new(ba, cy + 1.55), new(bb, cy + 1.55), new(bb, cy + 2.2), new(ba, cy + 2.2)]);
            }
        }
        // The links on this page: from the predecessor's end to the successor's (the elbow over the gap).
        foreach (var l in p.Links)
        {
            if (!rowOf.TryGetValue(l.From, out var i) || !rowOf.TryGetValue(l.To, out var j)) continue;
            var (a, b) = (r.Times[l.From], r.Times[l.To]);
            var xa = l.Type is "FS" or "FF" ? X(a.Finish.AddDays(1)) : X(a.Start);
            var xb = l.Type is "FS" or "SS" ? X(b.Start) : X(b.Finish.AddDays(1));
            var ya = y0 + HeadH + i * RowH + RowH / 2;
            var yb = y0 + HeadH + j * RowH + RowH / 2 + (j > i ? -1.3 : 1.3);
            var xm = l.Type is "FS" ? Math.Max(xa + 1, Math.Min(xb, xa + 1)) : l.Type == "SS" ? Math.Min(xa, xb) - 1.5 : Math.Max(xa, xb) + 1.5;
            links.Segments.Add((new Vec2(xa, ya), new Vec2(xm, ya)));
            links.Segments.Add((new Vec2(xm, ya), new Vec2(xm, yb)));
            if (l.Type is not "FS") links.Segments.Add((new Vec2(xm, yb), new Vec2(xb, yb)));
            var dir = j > i ? 1 : -1;
            links.Segments.Add((new Vec2(l.Type is "FS" ? xm : xb, yb), new Vec2((l.Type is "FS" ? xm : xb) - 0.6, yb - 0.9 * dir)));
            links.Segments.Add((new Vec2(l.Type is "FS" ? xm : xb, yb), new Vec2((l.Type is "FS" ? xm : xb) + 0.6, yb - 0.9 * dir)));
        }
        if (p.DataDate is { } dd && dd >= from && dd <= to) status.Segments.Add((new Vec2(X(dd), y0 + HeadH / 2), new Vec2(X(dd), bodyBottom)));
        return (width, bodyBottom, new List<Pen> { thin, rule, links, baselinePen, bars, critical, black, donePen, status, text }.Where(x => x.Segments.Count + x.Loops.Count + x.Texts.Count > 0).ToList());
    }
}

/// <summary>
/// A stages sheet (docs/plan-4d5d.md, stage 4: Planning → Stages sheet…): one view drawn by the engine at several dates
/// (each frame the view as State at date draws it), or pictures of the 3D taken by the interface at those dates; each frame
/// labelled with its date and the tasks under way that day. A <c>LapinStages</c> entity laid out as a sheet content, so it
/// prints in the PDF like a schedule.
/// </summary>
public sealed class StagesSheet : ISheetContent
{
    public const string Class = "LapinStages";
    const double LabelH = 13, Gap = 6;

    public bool Handles(Entity e) => e.Class == Class;

    public sealed record Frame(DateOnly Date, string? Image);

    public static List<Frame> Frames(Entity e) => e.Attrs["frames"] is JsonArray a
        ? a.OfType<JsonObject>().Select(o => WorkCalendar.ParseDate((string?)o["date"]) is { } d ? new Frame(d, (string?)o["image"]) : null).OfType<Frame>().ToList()
        : [];

    /// <summary>How many columns a number of frames is laid in.</summary>
    public static int Columns(int n, int? asked = null) => asked is > 0 ? Math.Min(asked.Value, Math.Max(1, n)) : n <= 1 ? 1 : n <= 4 ? 2 : n <= 9 ? 3 : 4;

    public IReadOnlyList<(double W, double H, List<Pen> Pens)> Pages(IQueryContext q, Entity e, double maxWidth, double? maxHeight)
    {
        var frames = Frames(e);
        var timeline = Timeline.Of(q);
        var view = e.Str("viewId") is { } vid ? Views.Find(q.Model, vid) : null;
        // What each frame shows and its natural size: a picture's pixels, a drawing's extent at its date.
        var content = frames.Select(f =>
        {
            if (f.Image != null && q.Model.Get(f.Image) is { Class: Images.Class } img)
            {
                var (iw, ih) = PictureSize(Images.Bytes(img)) ?? (4, 3);
                return (W: iw, H: ih, Image: f.Image, Drawing: (Views.Rendered?)null);
            }
            if (view == null) return (W: 4.0, H: 3.0, Image: (string?)null, Drawing: (Views.Rendered?)null);
            var attrs = view.CloneAttrs();
            attrs["stateDate"] = WorkCalendar.Date(f.Date);
            var r = Views.Render(q, view.With(attrs: attrs), print: true);
            return r.Bounds is { } b ? (W: Math.Max(1e-6, b.Max.X - b.Min.X), H: Math.Max(1e-6, b.Max.Y - b.Min.Y), Image: null, Drawing: r) : (W: 4.0, H: 3.0, Image: null, Drawing: null);
        }).ToList();
        var cols = Columns(frames.Count, (int?)e.NumOrNull("columns"));
        var rows = Math.Max(1, (frames.Count + cols - 1) / cols);
        var cellW = maxWidth / cols;
        // As tall as the frames' proportions ask (the tallest of them), within the frame's height.
        var aspect = content.Count > 0 ? content.Max(c => c.H / c.W) : 0.75;
        var cellH = (cellW - Gap) * aspect + LabelH + Gap;
        if (maxHeight is { } mh && cellH * rows > mh) cellH = mh / rows;
        var pens = new List<Pen>();
        var text = new Pen("TEXT", "#000000", 0, null, null);
        var rule = new Pen("STAGES", "#000000", 0.25, null, null);
        for (var i = 0; i < frames.Count; i++)
        {
            var f = frames[i];
            var c = content[i];
            var (x0, y0) = (i % cols * cellW, i / cols * cellH);
            var box = (X0: x0 + Gap / 2, Y0: y0 + Gap / 2, X1: x0 + cellW - Gap / 2, Y1: y0 + cellH - LabelH - Gap / 2);
            // As large as fits, its proportions kept, centred across and at the box's top: the label right under it.
            var k = Math.Min((box.X1 - box.X0) / c.W, (box.Y1 - box.Y0) / c.H);
            var (cx, cy) = ((box.X0 + box.X1) / 2, box.Y0 + c.H * k / 2);
            if (c.Image != null)
            {
                var (hw, hh) = (c.W * k / 2, c.H * k / 2);
                var pic = new Pen("STAGES-IMAGE", null, 0, null, null);
                pic.Images.Add(new PenImage(c.Image, [new(cx - hw, cy - hh), new(cx + hw, cy - hh), new(cx + hw, cy + hh), new(cx - hw, cy + hh)]));
                pens.Add(pic);
            }
            else if (c.Drawing != null)
                pens.AddRange(Fitted(c.Drawing, k, cx, cy));
            // The label: the date, then the tasks under way.
            var under = timeline.TasksOn(f.Date).Select(t => t.Name).ToList();
            var ly = box.Y0 + c.H * k + 1;
            rule.Segments.Add((new Vec2(x0 + Gap / 2, ly + 1), new Vec2(x0 + cellW - Gap / 2, ly + 1)));
            text.Texts.Add(new PenText(new Vec2(x0 + Gap / 2, ly + 5.5), $"{i + 1}. {f.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}", 3.5, Anchor: "start", Bold: true));
            var tasks = under.Count == 0 ? "—" : string.Join(", ", under);
            var room = (int)((cellW - Gap) / (2.5 * 0.52));
            text.Texts.Add(new PenText(new Vec2(x0 + Gap / 2, ly + 10.5), tasks.Length <= room ? tasks : tasks[..Math.Max(1, room - 1)] + "…", 2.5, Anchor: "start"));
        }
        pens.Add(rule);
        pens.Add(text);
        return [(cols * cellW, rows * cellH, pens)];
    }

    /// <summary>A view as drawn on a day, scaled by <paramref name="k"/> (paper mm a metre) about a point of the paper (its texts scaled with it).</summary>
    static IEnumerable<Pen> Fitted(Views.Rendered r, double k, double cx, double cy)
    {
        var b = r.Bounds!.Value;
        var mid = (b.Min + b.Max) * 0.5;
        Vec2 P(Vec2 v) => new(cx + (v.X - mid.X) * k, cy - (v.Y - mid.Y) * k);
        // Texts were sized for the view's scale on paper; here they shrink with the drawing.
        var t = Math.Min(1, k / (1000 / r.Scale));
        foreach (var pen in r.Pens)
        {
            var o = new Pen(pen.Layer, pen.Stroke, pen.Width, pen.Fill, pen.Dash);
            o.Segments.AddRange(pen.Segments.Select(s => (P(s.A), P(s.B))));
            o.Loops.AddRange(pen.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(P).ToList()));
            o.Texts.AddRange(pen.Texts.Select(x => x with { At = P(x.At), Height = x.Height * t }));
            o.Images.AddRange(pen.Images.Select(img => img with { Corners = [.. img.Corners.Select(P)] }));
            if (o.Segments.Count + o.Loops.Count + o.Texts.Count + o.Images.Count > 0) yield return o;
        }
    }

    /// <summary>A PNG's or JPEG's width and height in pixels, read from its header; null when it is neither.</summary>
    public static (double W, double H)? PictureSize(byte[] b)
    {
        if (Images.Sniff(b) == "image/png" && b.Length >= 24)
            return ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
        if (Images.Sniff(b) != "image/jpeg") return null;
        for (var i = 2; i + 9 < b.Length;)
        {
            if (b[i] != 0xFF) { i++; continue; }
            var marker = b[i + 1];
            var len = (b[i + 2] << 8) | b[i + 3];
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
            i += 2 + len;
        }
        return null;
    }
}
