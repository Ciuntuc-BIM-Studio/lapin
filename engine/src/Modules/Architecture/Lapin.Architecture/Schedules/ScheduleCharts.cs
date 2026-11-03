using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Schedules;

/// <summary>
/// A chart of a schedule (decision 251): what it shows (bars across, columns, a pie or a line), the column its
/// categories come from, the column it adds up (none: it counts the rows) and how (sum, count, average, least, most),
/// its title and how many categories at most (the largest, the rest as "Other").
/// </summary>
public sealed record ScheduleChart(string Id, string Kind, string Title, string Category, string? Value, string Aggregate, int? Top)
{
    public static readonly string[] Kinds = ["bar", "column", "pie", "line"];
    public static readonly string[] Aggregates = ["sum", "count", "avg", "min", "max"];

    public static ScheduleChart? From(JsonNode? n) => n is JsonObject o && (string?)o["id"] is { } id && (string?)o["category"] is { Length: > 0 } cat
        ? new ScheduleChart(id, Kinds.Contains((string?)o["kind"]) ? (string)o["kind"]! : "column", (string?)o["title"] ?? "", cat,
            (string?)o["value"] is { Length: > 0 } v ? v : null, Aggregates.Contains((string?)o["aggregate"]) ? (string)o["aggregate"]! : "sum",
            Json.Number(o["top"]) is { } t && t >= 1 ? (int)t : null)
        : null;

    public static List<ScheduleChart> Of(Lapin.Model.Entity schedule) =>
        schedule.Attrs["charts"] is JsonArray a ? a.Select(From).OfType<ScheduleChart>().ToList() : [];

    /// <summary>The chart's own title, or what it shows (Total by Name).</summary>
    public string Heading => Title.Trim() is { Length: > 0 } t ? t
        : Value == null || Aggregate == "count" ? $"Count by {Category}" : $"{AggregateLabel(Aggregate)} of {Value} by {Category}";

    public static string AggregateLabel(string a) => a switch { "count" => "Count", "avg" => "Average", "min" => "Least", "max" => "Most", _ => "Sum" };
}

/// <summary>A chart's values: one per category, in the table's order (or the largest first when it keeps only the top ones).</summary>
public static class ScheduleCharts
{
    /// <summary>A number in a cell: decimal point or comma, a unit or a percent after it ("12,5 m²", "40 %"); none when it does not start with one.</summary>
    public static double? Number(string text)
    {
        var t = text.Trim().Replace(" ", "").Replace(" ", "");
        var end = 0;
        while (end < t.Length && (char.IsDigit(t[end]) || t[end] is '.' or ',' || (end == 0 && t[end] is '-' or '+'))) end++;
        var num = t[..end].Replace(',', '.').TrimEnd('.');
        if (num.Length == 0 || !num.Any(char.IsDigit) || num.Count(c => c == '.') > 1) return null;
        return double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
    }

    /// <summary>Whether a grid's row is its totals line, left out of the charts.</summary>
    public static bool IsTotal(IReadOnlyList<string> row) => row.Count > 0 && row[0].Trim().StartsWith("Total", StringComparison.OrdinalIgnoreCase);

    /// <summary>The categories and their values from a grid; refused when it has no such column.</summary>
    public static List<(string Label, double Value)> Values(ScheduleChart c, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var ci = IndexOf(columns, c.Category) ?? throw new CommandException($"The table has no column „{c.Category}”.");
        int? vi = c.Value == null || c.Aggregate == "count" ? null : IndexOf(columns, c.Value) ?? throw new CommandException($"The table has no column „{c.Value}”.");
        var groups = new List<(string Label, List<double> Values, int Count)>();
        foreach (var r in rows)
        {
            if (IsTotal(r)) continue;
            var label = ci < r.Count && r[ci].Trim() is { Length: > 0 } l ? l : "—";
            var at = groups.FindIndex(g => g.Label == label);
            if (at < 0) { groups.Add((label, [], 0)); at = groups.Count - 1; }
            var g = groups[at];
            if (vi is { } i && i < r.Count && Number(r[i]) is { } v) g.Values.Add(v);
            groups[at] = (g.Label, g.Values, g.Count + 1);
        }
        var list = groups.Select(g => (g.Label, Value: vi == null ? g.Count : c.Aggregate switch
        {
            "avg" => g.Values.Count > 0 ? g.Values.Average() : 0,
            "min" => g.Values.Count > 0 ? g.Values.Min() : 0,
            "max" => g.Values.Count > 0 ? g.Values.Max() : 0,
            _ => g.Values.Sum(),
        })).ToList();
        if (c.Top is { } top && list.Count > top)
        {
            var sorted = list.OrderByDescending(x => x.Value).ToList();
            var rest = sorted.Skip(top - 1).ToList();
            list = [.. sorted.Take(top - 1), ("Other", c.Aggregate is "sum" or "count" ? rest.Sum(x => x.Value) : rest.Average(x => x.Value))];
        }
        return list;
    }

    static int? IndexOf(IReadOnlyList<string> columns, string name)
    {
        for (var i = 0; i < columns.Count; i++) if (string.Equals(columns[i], name, StringComparison.OrdinalIgnoreCase)) return i;
        return null;
    }

    static readonly string[] Palette = ["#4e79a7", "#f28e2b", "#59a14f", "#e15759", "#76b7b2", "#edc948", "#b07aa1", "#ff9da7", "#9c755f", "#bab0ac"];

    /// <summary>A value as the chart writes it: at most two decimals, none for whole numbers.</summary>
    public static string Text(double v) => Math.Abs(v - Math.Round(v)) < 1e-9 ? Math.Round(v).ToString(CultureInfo.InvariantCulture) : v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>A round step for an axis up to <paramref name="max"/>: 1, 2 or 5 times a power of ten, about four of them.</summary>
    static double Step(double max)
    {
        if (max <= 0) return 1;
        var raw = max / 4;
        var p = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        return new[] { 1, 2, 5, 10 }.Select(f => f * p).First(s => s >= raw);
    }

    sealed class Pens
    {
        public readonly List<Pen> All = [];
        readonly Dictionary<string, Pen> byKey = [];
        Pen Get(string key, Func<Pen> make) { if (!byKey.TryGetValue(key, out var p)) { byKey[key] = p = make(); All.Add(p); } return p; }
        public void Line(Vec2 a, Vec2 b, double w, string colour = "#000000") => Get($"l:{colour}:{w}", () => new Pen("SCHEDULE-CHART", colour, w, null, null)).Segments.Add((a, b));
        public void Fill(IReadOnlyList<Vec2> loop, string colour) => Get($"f:{colour}", () => new Pen("SCHEDULE-CHART", null, 0, colour, null)).Loops.Add(loop);
        public void Text(Vec2 at, string text, double size, string anchor = "middle", double angle = 0, bool bold = false, string baseline = "bottom") =>
            Get("t", () => new Pen("TEXT", "#000000", 0, null, null)).Texts.Add(new PenText(at, text, size, angle, anchor, baseline, bold));
    }

    static IReadOnlyList<Vec2> Box(double x0, double y0, double x1, double y1) => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];

    static string Short(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

    /// <summary>A chart that cannot be drawn: why, in its place.</summary>
    public static ScheduleBand Message(string text)
    {
        var p = new Pens();
        p.Text(new Vec2(0, 6), text, 2.5, "start");
        return new ScheduleBand(Math.Max(40, text.Length * 1.7), 10, p.All);
    }

    /// <summary>The chart drawn (paper mm, y down from its top), no wider than <paramref name="maxWidth"/>.</summary>
    public static ScheduleBand Draw(ScheduleChart c, List<(string Label, double Value)> values, double maxWidth = 380)
    {
        var p = new Pens();
        const double t = 2.5;
        if (values.Count == 0) return Message("Nothing to show: the table has no rows.");
        if (c.Kind == "pie") return Pie(p, values, t);
        if (c.Kind == "bar") return Bars(p, values, t, maxWidth);
        // Columns or a line: categories along, values up.
        var max = Math.Max(values.Max(v => v.Value), 0);
        var min = Math.Min(values.Min(v => v.Value), 0);
        var step = Step(Math.Max(max - min, 1e-9));
        var top = Math.Ceiling(max / step) * step;
        var bottom = Math.Floor(min / step) * step;
        if (top - bottom < 1e-12) top = bottom + step;
        var slot = Math.Clamp((maxWidth - 30) / values.Count, 8, 24);
        const double plotH = 60, left = 18, upper = 6;
        var plotW = slot * values.Count;
        var rotate = values.Any(v => v.Label.Length * 1.5 > slot);
        double Y(double v) => upper + plotH * (top - v) / (top - bottom);
        for (var g = bottom; g <= top + step / 2; g += step)
        {
            var y = Y(g);
            p.Line(new Vec2(left, y), new Vec2(left + plotW, y), 0.13, "#bbbbbb");
            p.Text(new Vec2(left - 1.5, y + 0.9), Text(g), 2, "end");
        }
        p.Line(new Vec2(left, upper), new Vec2(left, upper + plotH), 0.25);
        p.Line(new Vec2(left, Y(0)), new Vec2(left + plotW, Y(0)), 0.25);
        Vec2? last = null;
        for (var i = 0; i < values.Count; i++)
        {
            var (label, v) = values[i];
            var cx = left + slot * (i + 0.5);
            if (c.Kind == "line")
            {
                var pt = new Vec2(cx, Y(v));
                if (last is { } l) p.Line(l, pt, 0.5, Palette[0]);
                p.Fill(Box(cx - 0.9, pt.Y - 0.9, cx + 0.9, pt.Y + 0.9), Palette[0]);
                last = pt;
            }
            else p.Fill(Box(cx - slot * 0.35, Math.Min(Y(v), Y(0)), cx + slot * 0.35, Math.Max(Y(v), Y(0))), Palette[i % Palette.Length]);
            p.Text(new Vec2(cx, Math.Min(Y(v), Y(0)) - 1), Text(v), 2);
            if (rotate) p.Text(new Vec2(cx + 0.8, upper + plotH + 2), Short(label, 24), 2, "end", 60, baseline: "middle");
            else p.Text(new Vec2(cx, upper + plotH + 4), label, 2);
        }
        var below = rotate ? Math.Min(values.Max(v => Short(v.Label, 24).Length), 24) * 1.1 + 4 : 6;
        return new ScheduleBand(left + plotW + 4, upper + plotH + below, p.All);
    }

    static ScheduleBand Bars(Pens p, List<(string Label, double Value)> values, double t, double maxWidth)
    {
        const double row = 6, labelW = 40;
        var plotW = Math.Min(120, maxWidth - labelW - 20);
        var max = Math.Max(values.Max(v => Math.Abs(v.Value)), 1e-9);
        for (var i = 0; i < values.Count; i++)
        {
            var (label, v) = values[i];
            var y = i * row;
            var w = plotW * Math.Abs(v) / max;
            p.Text(new Vec2(labelW - 2, y + 4.2), Short(label, 26), t, "end");
            p.Fill(Box(labelW, y + 1, labelW + Math.Max(w, 0.3), y + row - 1), Palette[i % Palette.Length]);
            p.Text(new Vec2(labelW + w + 1.5, y + 4.2), Text(v), t, "start");
        }
        p.Line(new Vec2(labelW, 0), new Vec2(labelW, values.Count * row), 0.25);
        return new ScheduleBand(labelW + plotW + 20, values.Count * row, p.All);
    }

    static ScheduleBand Pie(Pens p, List<(string Label, double Value)> values, double t)
    {
        const double r = 30;
        var c = new Vec2(r + 2, r + 2);
        var total = values.Sum(v => Math.Max(v.Value, 0));
        var a0 = -Math.PI / 2;
        for (var i = 0; i < values.Count; i++)
        {
            var v = Math.Max(values[i].Value, 0);
            if (total <= 0 || v <= 0) continue;
            var sweep = 2 * Math.PI * v / total;
            var n = Math.Max(2, (int)Math.Ceiling(sweep / (Math.PI / 36)));
            var loop = new List<Vec2> { c };
            for (var k = 0; k <= n; k++) { var a = a0 + sweep * k / n; loop.Add(c + new Vec2(Math.Cos(a), Math.Sin(a)) * r); }
            p.Fill(loop, Palette[i % Palette.Length]);
            a0 += sweep;
        }
        // The legend beside it: each colour, its label, its value and share.
        var lx = 2 * r + 10;
        for (var i = 0; i < values.Count; i++)
        {
            var y = 4 + i * 5.5;
            p.Fill(Box(lx, y, lx + 3.5, y + 3.5), Palette[i % Palette.Length]);
            var share = total > 0 ? Math.Max(values[i].Value, 0) / total * 100 : 0;
            p.Text(new Vec2(lx + 5.5, y + 3.2), $"{Short(values[i].Label, 30)}  {Text(values[i].Value)}  ({share.ToString("0.#", CultureInfo.InvariantCulture)} %)", t, "start");
        }
        var legendW = 5.5 + Math.Min(values.Max(v => Short(v.Label, 30).Length + Text(v.Value).Length + 12), 60) * 1.7;
        return new ScheduleBand(lx + legendW, Math.Max(2 * r + 4, 4 + values.Count * 5.5), p.All);
    }
}
