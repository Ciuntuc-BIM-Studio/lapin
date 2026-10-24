using System.Globalization;
using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Architecture.Schedules;

/// <summary>A schedule's own rows, typed cells, row order and card order (what its table settings say).</summary>
public sealed record ScheduleTable(IReadOnlyList<string>? Rows, IReadOnlyList<(string Id, string Label)> CustomRows,
    IReadOnlyDictionary<string, Dictionary<string, string>> Cells, string? Sort)
{
    public static ScheduleTable Of(Entity schedule)
    {
        var rows = schedule.Attrs["rows"] is JsonArray r ? r.Select(x => (string?)x).OfType<string>().ToList() : null;
        var custom = schedule.Attrs["customRows"] is JsonArray c
            ? c.OfType<JsonObject>().Where(o => (string?)o["id"] != null).Select(o => ((string)o["id"]!, (string?)o["label"] ?? "")).ToList() : [];
        var cells = new Dictionary<string, Dictionary<string, string>>();
        if (schedule.Attrs["cells"] is JsonObject co)
            foreach (var (k, v) in co)
                if (v is JsonObject row) cells[k] = row.Where(x => x.Value is JsonValue).ToDictionary(x => x.Key, x => (string?)x.Value ?? "");
        return new ScheduleTable(rows, custom, cells, schedule.Str("sort"));
    }
}

/// <summary>A row of the table: its id and label, its value per entry, whether the model gives it, bold.</summary>
public sealed record ScheduleRow(string Id, string Label, Func<ScheduleEntry, string> Value, bool FromModel, bool Strong = false);

/// <summary>A band of cards (paper mm, y down from its top): its size and pens.</summary>
public sealed record ScheduleBand(double W, double H, List<Pen> Pens);

/// <summary>
/// A schedule as cards (the old Lapin's schedule layout, in paper mm): a label
/// column, then a card per entry with its mark, its drawing (the type in
/// elevation, dimensioned; a lintel in elevation and section) and its values,
/// row by row; cards wrap into bands no wider than asked, and bands into pages.
/// </summary>
public static class ScheduleLayout
{
    const double Gap = 6, DimBelow = 7, DimLeft = 6, LabelW = 34, Row = 6, MarkRow = 10, Text = 2.5;
    const double FramePen = 0.35, GridPen = 0.18;
    static readonly Dictionary<string, double> DrawingPen = new() { ["cut"] = 0.35, ["outline"] = 0.25, ["thin"] = 0.13 };
    public const double TitleH = 10, BandGap = 8, TableGap = 10;

    public static readonly string[] JoineryRows = ["name", "size", "operation", "sill", "levels", "total", "uValue", "fire", "lintel"];
    public static readonly string[] LintelRows = ["lintelKind", "product", "section", "length", "levels", "total", "openings", "concrete", "steel"];

    static string Mm(double m) => Math.Round(m * 1000).ToString(CultureInfo.InvariantCulture);

    static readonly Dictionary<string, string> OperationLabel = new()
    {
        ["fixed"] = "fixed", ["side-left"] = "side hung left", ["side-right"] = "side hung right", ["tilt-turn-left"] = "tilt & turn left", ["tilt-turn-right"] = "tilt & turn right",
        ["bottom-hung"] = "bottom hung", ["top-hung"] = "top hung", ["pivot-horizontal"] = "horizontal pivot", ["pivot-vertical"] = "vertical pivot",
        ["sliding-left"] = "sliding left", ["sliding-right"] = "sliding right", ["sliding-vertical"] = "vertical sliding",
        ["swing-left"] = "swing left", ["swing-right"] = "swing right", ["double-acting-left"] = "double acting left", ["double-acting-right"] = "double acting right",
        ["door-sliding-left"] = "sliding left", ["door-sliding-right"] = "sliding right", ["folding-left"] = "folding left", ["folding-right"] = "folding right",
    };

    /// <summary>The rows the data takes (lintel rows when every entry is a lintel), in the table's order, typed text over the model's.</summary>
    public static List<ScheduleRow> Rows(ScheduleData data, ScheduleTable table)
    {
        var lintel = data.Entries.Count > 0 && data.Entries.All(e => e.Kind == "lintel");
        var builtIn = lintel ? LintelRows : JoineryRows;
        var order = table.Rows ?? [.. builtIn, .. table.CustomRows.Select(c => "custom:" + c.Id)];
        var rows = new List<ScheduleRow>();
        foreach (var id in order)
        {
            if (id.StartsWith("custom:", StringComparison.Ordinal))
            {
                var c = table.CustomRows.FirstOrDefault(x => x.Id == id[7..]);
                if (c.Id != null) rows.Add(new ScheduleRow(id, c.Label, e => table.Cells.GetValueOrDefault(e.Key)?.GetValueOrDefault(id) ?? "", false));
                continue;
            }
            if (!builtIn.Contains(id)) continue;
            if (id == "levels")
            {
                foreach (var level in data.Levels) rows.Add(new ScheduleRow("level:" + level, level, e => e.Counts.TryGetValue(level, out var n) ? n.ToString(CultureInfo.InvariantCulture) : "", true));
                continue;
            }
            ScheduleRow? row = id switch
            {
                "name" => new(id, "Name", e => e.Name, true),
                "size" => new(id, "Size W × H (mm)", e => $"{Mm(e.Width)} × {Mm(e.Height)}", true),
                "operation" => new(id, "Opening", e => e.Spec == null ? "—" : string.Join(" + ", Joinery.Normalised(e.Spec).Select(p => p.Operation).Distinct().Select(o => OperationLabel.GetValueOrDefault(o, o))), true),
                "sill" => data.Entries.Any(e => e.Kind == "window") ? new(id, "Sill height (mm)", e => e.Kind == "window" && e.Spec != null ? Mm(e.Spec.SillHeight) : "—", true) : null,
                "total" => new(id, "Total", e => e.Total.ToString(CultureInfo.InvariantCulture), true, true),
                "uValue" => new(id, "U (W/m²K)", e => e.Spec?.ThermalTransmittance is { } u ? u.ToString(CultureInfo.InvariantCulture) : "—", true),
                "fire" => new(id, "Fire rating", e => e.Spec == null ? "—" : "—", true),
                "lintel" => data.LintelMarks is { Count: > 0 } lm
                    ? new(id, "Lintel", e => e.Occurrences.Select(o => lm.GetValueOrDefault(o.Id)).OfType<string>().Distinct().Order().ToList() is { Count: > 0 } l ? string.Join(", ", l) : "—", true) : null,
                "lintelKind" => new(id, "Precast or cast", e => e.Lintel!.Precast ? "Precast" : "Cast in place", true),
                "product" => new(id, "Product", e => e.Lintel!.Product ?? "—", true),
                "section" => new(id, "Section W × H (mm)", e => $"{Mm(e.Lintel!.Width)} × {Mm(e.Lintel.Height)}", true),
                "length" => new(id, "Length (mm; ! no stock length)", e => Mm(e.Lintel!.Length) + (e.Lintel.Fits ? "" : " !"), true),
                "openings" => new(id, "Over", e => e.Lintel!.Openings.Count > 0 ? string.Join(", ", e.Lintel.Openings) : "—", true),
                "concrete" => new(id, "Concrete (m³)", e => e.Lintel!.Precast ? "—" : (e.Lintel.Volume * e.Total).ToString("0.000", CultureInfo.InvariantCulture), true),
                "steel" => new(id, "Steel (kg)", e => e.Lintel!.SteelKg > 0 ? (e.Lintel.SteelKg * e.Total).ToString("0.0", CultureInfo.InvariantCulture) : "—", true),
                _ => null,
            };
            if (row != null) rows.Add(row);
        }
        // Typed text wins over the model's value.
        return rows.Select(r => r.FromModel ? r with { Value = e => table.Cells.GetValueOrDefault(e.Key)?.GetValueOrDefault(r.Id) ?? r.Value(e) } : r).ToList();
    }

    /// <summary>The cards' order: the data's (absent), by mark, name, width, height, or most first.</summary>
    public static List<ScheduleEntry> Sort(IEnumerable<ScheduleEntry> entries, string? sort) => sort switch
    {
        "mark" => entries.OrderBy(e => e.Mark, Comparer<string>.Create((a, b) => string.Compare(a, b, CultureInfo.InvariantCulture, CompareOptions.NumericOrdering))).ToList(),
        "name" => entries.OrderBy(e => e.Name, StringComparer.CurrentCulture).ToList(),
        "width" => entries.OrderBy(e => e.Width).ToList(),
        "height" => entries.OrderBy(e => e.Height).ToList(),
        "total" => entries.OrderByDescending(e => e.Total).ToList(),
        _ => entries.ToList(),
    };

    sealed class Sink
    {
        public readonly Dictionary<string, Pen> Pens = [];
        public void Line(Vec2 a, Vec2 b, double width, double[]? dash = null, string layer = "SCHEDULE")
        {
            var key = $"{layer}:{width}:{(dash == null ? "" : string.Join(" ", dash))}";
            if (!Pens.TryGetValue(key, out var p)) Pens[key] = p = new Pen(layer, "#000000", width, null, dash);
            p.Segments.Add((a, b));
        }
        public void Text(Vec2 at, string text, double size, bool bold = false, string anchor = "start", double angle = 0, string baseline = "bottom")
        {
            if (!Pens.TryGetValue("TEXT", out var p)) Pens["TEXT"] = p = new Pen("TEXT", "#000000", 0, null, null);
            p.Texts.Add(new PenText(at, text, size, angle, anchor, baseline, bold));
        }
    }

    /// <summary>A card's drawing: its size (paper mm) and how to draw it with its top-left at a point.</summary>
    static (double W, double H, Action<Sink, Vec2> Draw) Block(ScheduleEntry e, double k, IReadOnlyList<string> drawings)
    {
        void Dim(Sink s, Vec2 a, Vec2 b, string text, bool vertical)
        {
            s.Line(a, b, GridPen, layer: "SCHEDULE-SYMBOLS");
            foreach (var p in new[] { a, b }) s.Line(p + new Vec2(-0.8, 0.8), p + new Vec2(0.8, -0.8), DrawingPen["outline"], layer: "SCHEDULE-SYMBOLS");
            var mid = (a + b) * 0.5;
            if (vertical) s.Text(mid + new Vec2(-1, 0), text, 2, anchor: "middle", angle: 90);
            else s.Text(mid + new Vec2(0, -0.8), text, 2, anchor: "middle");
        }
        if (e.Kind == "lintel" && e.Lintel is { } l)
        {
            var (lw, lh, sw) = (l.Length * k, l.Height * k, l.Width * k);
            var w = 6 + lw + 6 + sw;
            var h = lh + 7;
            return (w, h, (s, at) =>
            {
                void Box(double x, double y, double bw, double bh)
                {
                    var (a, b, c, d) = (at + new Vec2(x, y), at + new Vec2(x + bw, y), at + new Vec2(x + bw, y + bh), at + new Vec2(x, y + bh));
                    s.Line(a, b, DrawingPen["cut"], layer: "SCHEDULE-SYMBOLS"); s.Line(b, c, DrawingPen["cut"], layer: "SCHEDULE-SYMBOLS");
                    s.Line(c, d, DrawingPen["cut"], layer: "SCHEDULE-SYMBOLS"); s.Line(d, a, DrawingPen["cut"], layer: "SCHEDULE-SYMBOLS");
                }
                Box(6, 0, lw, lh);
                Box(6 + lw + 6, 0, sw, lh);
                Dim(s, at + new Vec2(6, lh + 4.5), at + new Vec2(6 + lw, lh + 4.5), Mm(l.Length), false);
                Dim(s, at + new Vec2(2, 0), at + new Vec2(2, lh), Mm(l.Height), true);
                Dim(s, at + new Vec2(12 + lw, lh + 4.5), at + new Vec2(12 + lw + sw, lh + 4.5), Mm(l.Width), false);
            });
        }
        var views = drawings.Where(d => d is "interior" or "exterior").DefaultIfEmpty("interior").ToList();
        var (ew, eh) = (e.Width * k, e.Height * k);
        var rowW = views.Count * ew + (views.Count - 1) * Gap;
        return (DimLeft + rowW, eh + DimBelow, (s, at) =>
        {
            for (var i = 0; i < views.Count; i++)
            {
                var x0 = at.X + DimLeft + i * (ew + Gap);
                var bottom = at.Y + eh;
                Vec2 P(Vec2 q) => new(x0 + (q.X + e.Width / 2) * k, bottom - q.Y * k);
                if (e.Spec != null)
                    foreach (var st in JoineryElevation.Of(e.Spec, views[i] == "exterior"))
                        s.Line(P(st.A), P(st.B), DrawingPen[st.Weight], st.Dashed ? [1.2, 0.8] : null, "SCHEDULE-SYMBOLS");
                else
                {
                    var (a, b) = (new Vec2(-e.Width / 2, 0), new Vec2(e.Width / 2, e.Height));
                    foreach (var (p, q) in new[] { (a, new Vec2(b.X, a.Y)), (new Vec2(b.X, a.Y), b), (b, new Vec2(a.X, b.Y)), (new Vec2(a.X, b.Y), a) })
                        s.Line(P(p), P(q), DrawingPen["outline"], layer: "SCHEDULE-SYMBOLS");
                }
            }
            var x = at.X + DimLeft;
            Dim(s, new Vec2(x, at.Y + eh + 4.5), new Vec2(x + ew, at.Y + eh + 4.5), Mm(e.Width), false);
            Dim(s, new Vec2(x - 4, at.Y), new Vec2(x - 4, at.Y + eh), Mm(e.Height), true);
        });
    }

    /// <summary>A table's cards in bands no wider than <paramref name="maxWidth"/> (paper mm), each band's pens from its own top.</summary>
    public static List<ScheduleBand> Bands(ScheduleData data, ScheduleTable table, double scale = 50, double maxWidth = 380, Func<ScheduleEntry, IReadOnlyList<string>>? drawingsOf = null)
    {
        var k = 1000 / scale;
        var rows = Rows(data, table);
        var entries = Sort(data.Entries, table.Sort);
        var blocks = entries.Select(e => Block(e, k, drawingsOf?.Invoke(e) ?? ["interior"])).ToList();
        double CardW(int i) => Math.Max(30, blocks[i].W + 10);
        var drawingH = Math.Max(30, blocks.Count == 0 ? 0 : blocks.Max(b => b.H) + 7);
        var bandH = MarkRow + drawingH + rows.Count * Row;

        var groups = new List<List<int>> { new() };
        var used = LabelW;
        for (var i = 0; i < entries.Count; i++)
        {
            if (groups[^1].Count > 0 && used + CardW(i) > maxWidth) { groups.Add([]); used = LabelW; }
            groups[^1].Add(i);
            used += CardW(i);
        }
        var bands = new List<ScheduleBand>();
        foreach (var g in groups)
        {
            var s = new Sink();
            var w = LabelW + g.Sum(CardW);
            const double top = 0;
            foreach (var (a, b) in new[] { (new Vec2(0, top), new Vec2(w, top)), (new Vec2(w, top), new Vec2(w, top + bandH)), (new Vec2(w, top + bandH), new Vec2(0, top + bandH)), (new Vec2(0, top + bandH), new Vec2(0, top)) })
                s.Line(a, b, FramePen, layer: "SCHEDULE-SYMBOLS");
            s.Line(new Vec2(LabelW, top), new Vec2(LabelW, top + bandH), FramePen);
            s.Line(new Vec2(0, top + MarkRow), new Vec2(w, top + MarkRow), GridPen);
            s.Text(new Vec2(2, top + 6.5), "Mark", Text, bold: true);
            var yRows = top + MarkRow + drawingH;
            for (var r = 0; r < rows.Count; r++)
            {
                var y = yRows + r * Row;
                s.Line(new Vec2(0, y), new Vec2(w, y), r == 0 ? FramePen : GridPen);
                s.Text(new Vec2(2, y + 4.2), rows[r].Label, Text, bold: rows[r].Strong);
            }
            var x = LabelW;
            foreach (var i in g)
            {
                var cw = CardW(i);
                var cx = x + cw / 2;
                s.Line(new Vec2(x + cw, top), new Vec2(x + cw, top + bandH), GridPen);
                s.Text(new Vec2(cx, top + 7), entries[i].Mark, 4, bold: true, anchor: "middle");
                var (bw, bh, draw) = blocks[i];
                draw(s, new Vec2(cx - bw / 2, top + MarkRow + drawingH - bh));
                for (var r = 0; r < rows.Count; r++)
                {
                    var text = rows[r].Value(entries[i]);
                    var size = text.Length > cw / 1.6 ? Math.Max(1.6, (cw - 2) / text.Length * 1.7) : Text;
                    s.Text(new Vec2(cx, yRows + r * Row + 4.2), text, size, rows[r].Strong, "middle");
                }
                x += cw;
            }
            bands.Add(new ScheduleBand(w, bandH, s.Pens.Values.ToList()));
        }
        return bands;
    }

    /// <summary>
    /// Tables (each a title and its bands) as pages no taller than <paramref name="maxHeight"/>:
    /// a table's title over its first band, "(continued)" atop a new page; bands 8 mm
    /// apart, tables 10. Without a height, one page.
    /// </summary>
    public static List<(double W, double H, List<Pen> Pens)> Pages(IReadOnlyList<(string Title, List<ScheduleBand> Bands)> tables, double? maxHeight)
    {
        var pages = new List<(double W, double H, List<Pen> Pens)>();
        var cur = new List<Pen>();
        double y = 0, w = 0;
        var text = new Pen("TEXT", "#000000", 0, null, null);
        void Flush()
        {
            if (text.Texts.Count > 0) cur.Add(text);
            pages.Add((w, Math.Max(0, y - TableGap), cur));
            cur = []; text = new Pen("TEXT", "#000000", 0, null, null); y = 0; w = 0;
        }
        foreach (var (title, bands) in tables)
        {
            for (var i = 0; i < bands.Count; i++)
            {
                var band = bands[i];
                var needs = band.H + (i == 0 ? TitleH : 0);
                if (cur.Count > 0 && maxHeight is { } mh && y + needs > mh) Flush();
                if (i == 0 || cur.Count == 0)
                {
                    text.Texts.Add(new PenText(new Vec2(0, y + 6), i == 0 ? title : $"{title} (continued)", 5, Anchor: "start", Bold: true));
                    y += TitleH;
                }
                var dy = y;
                foreach (var p in band.Pens)
                {
                    var o = new Pen(p.Layer, p.Stroke, p.Width, p.Fill, p.Dash);
                    o.Segments.AddRange(p.Segments.Select(sg => (sg.A + new Vec2(0, dy), sg.B + new Vec2(0, dy))));
                    o.Texts.AddRange(p.Texts.Select(t => t with { At = t.At + new Vec2(0, dy) }));
                    cur.Add(o);
                }
                w = Math.Max(w, band.W);
                y += band.H + BandGap;
            }
            y += TableGap - BandGap;
        }
        if (cur.Count > 0 || text.Texts.Count > 0 || pages.Count == 0) Flush();
        return pages;
    }
}
