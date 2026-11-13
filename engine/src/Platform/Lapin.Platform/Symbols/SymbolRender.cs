using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A symbol's outline: its points (mm of the symbol, y up), closed or not, its pen (paper mm) and colour (null: the instance's).</summary>
public sealed record SymStroke(IReadOnlyList<Vec2> Pts, bool Closed, double Width, string? Color, double[]? Dash);
/// <summary>A filled area (even-odd loops) and its colour (null: the instance's).</summary>
public sealed record SymFill(IReadOnlyList<IReadOnlyList<Vec2>> Loops, string? Color);
/// <summary>A hatched area: its loops, the pattern, its scale and angle as a drafted hatch has them (on paper), its colour.</summary>
public sealed record SymHatch(IReadOnlyList<IReadOnlyList<Vec2>> Loops, string Pattern, double Scale, double Angle, string? Color);
/// <summary>A text of a symbol, its live text filled: where (mm), its height (mm), angle (°) and how it hangs on its point.</summary>
public sealed record SymText(Vec2 At, string Text, double Height, double Angle, string Anchor, string Baseline, bool Bold, string Font, bool Italic, string? Color);

/// <summary>A symbol drawn in its own mm, its texts filled: what an instance lays where it stands.</summary>
public sealed record SymbolDrawing(List<SymStroke> Strokes, List<SymFill> Fills, List<SymHatch> Hatches, List<SymText> Texts)
{
    /// <summary>The box of what it draws (mm), texts by their anchors and an estimate of their width.</summary>
    public (Vec2 Min, Vec2 Max) Bounds()
    {
        var pts = Strokes.SelectMany(s => s.Pts).Concat(Fills.SelectMany(f => f.Loops.SelectMany(l => l))).Concat(Hatches.SelectMany(h => h.Loops.SelectMany(l => l))).ToList();
        foreach (var t in Texts)
        {
            var w = t.Text.Length * t.Height * 0.6;
            var a = t.Angle * Math.PI / 180;
            var (u, n) = (new Vec2(Math.Cos(a), Math.Sin(a)), new Vec2(-Math.Sin(a), Math.Cos(a)));
            var x0 = t.Anchor == "end" ? -w : t.Anchor == "middle" ? -w / 2 : 0;
            var y0 = t.Baseline == "middle" ? -t.Height / 2 : 0;
            pts.AddRange([t.At + u * x0 + n * y0, t.At + u * (x0 + w) + n * (y0 + t.Height)]);
        }
        if (pts.Count == 0) return (default, default);
        return (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y)));
    }
}

/// <summary>
/// Where an instance lays its symbol: its insertion point, the direction its +x takes (radians), how much a symbol mm is where it
/// lands (metres of a view, or paper mm on a sheet) and how much a symbol mm of text is in paper mm, and whether it is turned over
/// across its x (left ↔ right, <see cref="FlipX"/>) or its y (up ↔ down, <see cref="FlipY"/>). Texts never mirror: they stay readable
/// left to right or bottom to top, whichever way the symbol lies (docs/plan-user-extensibility.md §3.1).
/// </summary>
public sealed record SymbolXf(Vec2 At, double Angle, double Unit, double TextUnit, bool FlipX = false, bool FlipY = false)
{
    public Vec2 Dir(Vec2 d)
    {
        var q = new Vec2(FlipX ? -d.X : d.X, FlipY ? -d.Y : d.Y);
        var (c, s) = (Math.Cos(Angle), Math.Sin(Angle));
        return new Vec2(q.X * c - q.Y * s, q.X * s + q.Y * c);
    }

    public Vec2 Point(Vec2 p) => At + Dir(p) * Unit;

    /// <summary>Whether it turns the symbol over (one flip, not two: two flips are a half turn).</summary>
    public bool Mirrors => FlipX != FlipY;
}

public static class SymbolRender
{
    /// <summary>
    /// The symbol's items as strokes, fills, hatches and texts in its own mm, its live text filled by <paramref name="resolve"/>;
    /// circles and arcs in chords for a symbol mm of <paramref name="metresPerMm"/> where it lands (the one rule for every curve);
    /// <paramref name="fields"/>, when given, fills a text's own fields first (a tag symbol's {number}, decision 551).
    /// </summary>
    public static SymbolDrawing Render(IModelView m, SymbolDef d, Func<string, (bool Known, string? Value)> resolve, double metresPerMm, List<string>? unknown = null, Func<string, string>? fields = null)
    {
        var o = new SymbolDrawing([], [], [], []);
        var book = DraftingResources.Book(m);
        static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
        static double[]? Dash(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? [.. a.Select(x => Math.Max(0.05, Json.Number(x) ?? 1))] : null;
        List<Vec2> Round(Vec2 c, double r, double start, double sweep)
        {
            var n = Chords.Count(r * Math.Max(metresPerMm, 1e-9), sweep, 3);
            return [.. Enumerable.Range(0, n + 1).Select(i => { var a = start + sweep * i / n; return c + new Vec2(Math.Cos(a), Math.Sin(a)) * r; })];
        }
        void Area(JsonObject item, List<Vec2> loop, bool closed)
        {
            if (closed && (string?)item["fill"] is { Length: > 0 } fill && fill != "none") o.Fills.Add(new SymFill([loop], fill == "byInstance" ? null : fill));
            if (closed && item["hatch"] is JsonObject h && (string?)h["pattern"] is { Length: > 0 } pattern)
                o.Hatches.Add(new SymHatch([loop], pattern, Json.Number(h["scale"]) ?? 1, Json.Number(h["angle"]) ?? 0, (string?)h["color"]));
            var w = Json.Number(item["stroke"]) ?? Json.Number(item["w"]) ?? 0.25;
            if (w > 0) o.Strokes.Add(new SymStroke(loop, closed, w, (string?)item["color"], Dash(item["dash"])));
        }
        foreach (var item in d.Items.OfType<JsonObject>())
        {
            switch ((string?)item["type"])
            {
                case "line":
                {
                    var w = Json.Number(item["w"]) ?? 0.25;
                    if (w > 0) o.Strokes.Add(new SymStroke([P(item["a"]), P(item["b"])], false, w, (string?)item["color"], Dash(item["dash"])));
                    break;
                }
                case "rect":
                {
                    double x = Json.Number(item["x"]) ?? 0, y = Json.Number(item["y"]) ?? 0, w = Json.Number(item["w"]) ?? 0, h = Json.Number(item["h"]) ?? 0;
                    Area(item, [new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h)], true);
                    break;
                }
                case "polyline":
                {
                    var pts = (item["pts"] as JsonArray ?? []).Select(P).ToList();
                    if (pts.Count >= 2) Area(item, pts, item["closed"] is JsonValue c && c.TryGetValue<bool>(out var closed) && closed);
                    break;
                }
                case "circle":
                {
                    var r = Json.Number(item["r"]) ?? 0;
                    if (r <= 0) break;
                    var loop = Round(P(item["c"]), r, 0, 2 * Math.PI);
                    loop.RemoveAt(loop.Count - 1);
                    Area(item, loop, true);
                    break;
                }
                case "arc":
                {
                    var r = Json.Number(item["r"]) ?? 0;
                    if (r <= 0) break;
                    var (start, end) = ((Json.Number(item["start"]) ?? 0) * Math.PI / 180, (Json.Number(item["end"]) ?? 0) * Math.PI / 180);
                    var sweep = end - start;
                    sweep %= 2 * Math.PI;
                    if (sweep <= 1e-12) sweep += 2 * Math.PI;
                    var w = Json.Number(item["w"]) ?? Json.Number(item["stroke"]) ?? 0.25;
                    if (w > 0) o.Strokes.Add(new SymStroke(Round(P(item["c"]), r, start, sweep), false, w, (string?)item["color"], Dash(item["dash"])));
                    break;
                }
                case "text":
                {
                    var raw = item["text"]?.ToString() ?? "";
                    if (fields != null) raw = fields(raw);
                    var value = LiveText.Fill(raw, resolve, unknown);
                    var style = (string?)item["style"] is { Length: > 0 } sid ? Standards.TextOf(book, sid) : null;
                    var height = Json.Number(item["h"]) ?? style?.Height ?? 2.5;
                    var bold = item["bold"] is JsonValue b ? b.TryGetValue<bool>(out var bb) && bb : style?.Bold ?? false;
                    var italic = item["italic"] is JsonValue it ? it.TryGetValue<bool>(out var ii) && ii : style?.Italic ?? false;
                    var font = (string?)item["font"] ?? style?.Font ?? "sans";
                    var anchor = (string?)item["align"] is "middle" or "end" ? (string)item["align"]! : "start";
                    var baseline = (string?)item["valign"] == "middle" ? "middle" : "bottom";
                    var angle = Json.Number(item["angle"]) ?? 0;
                    var rows = value.Split('\n');
                    var a = angle * Math.PI / 180;
                    var down = new Vec2(Math.Sin(a), -Math.Cos(a));
                    for (var r = 0; r < rows.Length; r++)
                        if (rows[r].Length > 0)
                            o.Texts.Add(new SymText(P(item["at"]) + down * (r * height * 1.4), rows[r], height, angle, anchor, baseline, bold, font, italic, (string?)item["color"] ?? style?.Color));
                    break;
                }
            }
        }
        return o;
    }

    /// <summary>
    /// A text laid by <paramref name="x"/>: its point carried, its direction turned (and turned over when the symbol is), then
    /// kept readable — left to right, or bottom to top when it stands upright — by a half turn when it would read upside down
    /// or downwards, its anchor and its baseline moved so it covers the same place.
    /// </summary>
    public static (Vec2 At, double Angle, string Anchor, double Height) LayText(SymText t, SymbolXf x)
    {
        var a = t.Angle * Math.PI / 180;
        var dir = x.Dir(new Vec2(Math.Cos(a), Math.Sin(a))).Normalized();
        // The side the letters rise to: the text's up carried (turned over when the symbol is).
        var up = x.Dir(new Vec2(-Math.Sin(a), Math.Cos(a))).Normalized();
        var at = x.Point(t.At);
        var height = t.Height * x.TextUnit;
        var anchor = t.Anchor;
        // Readable: an angle in (-90°, 90°] (90°: read bottom to top).
        var deg = Math.Atan2(dir.Y, dir.X) * 180 / Math.PI;
        if (deg <= -90 + 1e-6 || deg > 90 + 1e-6)
        {
            dir = -dir;
            anchor = anchor switch { "start" => "end", "end" => "start", _ => anchor };
            deg = Math.Atan2(dir.Y, dir.X) * 180 / Math.PI;
        }
        // The letters rise along the direction's left; when the text should rise the other way, its baseline moves across
        // the text's height so the letters cover the same place (a middle baseline is the same either way).
        var rise = new Vec2(-dir.Y, dir.X);
        if (t.Baseline == "bottom" && Vec2.Dot(rise, up) < 0) at += up * (t.Height * x.Unit);
        return (at, Math.Round(deg, 6), anchor, height);
    }

    /// <summary>A symbol drawn into a view's mark: its strokes on <paramref name="layer"/>, fills, hatches (on paper, as a drafted hatch), texts readable.</summary>
    public static void Into(ViewMark mk, SymbolDrawing sd, SymbolXf x, string layer, string color, double paperMm, IEnumerable<HatchPattern>? patterns = null)
    {
        foreach (var s in sd.Strokes)
        {
            var pts = s.Pts.Select(x.Point).ToList();
            mk.Polyline(pts, s.Closed, layer, s.Width, s.Color ?? color, s.Dash);
        }
        foreach (var f in sd.Fills)
        {
            var loops = f.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(x.Point).ToList()).ToList();
            mk.Fills.Add((loops, f.Color ?? color));
            foreach (var l in loops) for (var i = 0; i < l.Count; i++) mk.Edges.Add((l[i], l[(i + 1) % l.Count]));
        }
        foreach (var h in sd.Hatches)
        {
            var loops = h.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(x.Point).ToList()).ToList();
            var pattern = HatchLibrary.Find(h.Pattern, patterns);
            if (pattern == null || pattern.Solid) { mk.Fills.Add((loops, h.Color ?? color)); continue; }
            var turn = x.Angle * 180 / Math.PI;
            var (segs, _) = HatchFill.Segments(loops, pattern, new HatchOptions(h.Scale * paperMm, h.Angle + turn, x.At, 20_000));
            foreach (var s in segs) mk.Line(s.A, s.B, layer, Pens.Default.Hatch, h.Color ?? color);
        }
        foreach (var t in sd.Texts)
        {
            var (at, angle, anchor, height) = LayText(t, x);
            mk.Text(new PenText(at, t.Text, height, angle, anchor, t.Baseline, t.Bold, t.Font, t.Italic), t.Color ?? color);
            // What a click on the text hits: its box (never drawn).
            var w = Math.Max(1, t.Text.Length) * height * 0.6 * paperMm;
            var hh = height * paperMm;
            var r = angle * Math.PI / 180;
            var (u, n) = (new Vec2(Math.Cos(r), Math.Sin(r)), new Vec2(-Math.Sin(r), Math.Cos(r)));
            var x0 = anchor == "end" ? -w : anchor == "middle" ? -w / 2 : 0;
            var y0 = t.Baseline == "middle" ? -hh / 2 : 0;
            Vec2[] box = [at + u * x0 + n * y0, at + u * (x0 + w) + n * y0, at + u * (x0 + w) + n * (y0 + hh), at + u * x0 + n * (y0 + hh)];
            for (var i = 0; i < 4; i++) mk.Lines.Add((box[i], box[(i + 1) % 4], "PICK", 0, "none", null));
        }
        mk.Snaps.Add(x.At);
    }

    /// <summary>
    /// A symbol drawn on paper (mm, y down, as a sheet's pens): its insertion point at <paramref name="at"/> on the page, its +x
    /// turned <paramref name="angle"/> radians counter-clockwise as the page is seen.
    /// </summary>
    public static List<Pen> ToPens(SymbolDrawing sd, Vec2 at, double angle, double scale, bool flipX, bool flipY, string color, IEnumerable<HatchPattern>? patterns = null, string layer = "SYMBOL")
    {
        // Laid in a y-up page, then turned to the page's y-down.
        var x = new SymbolXf(new Vec2(at.X, -at.Y), angle, scale, scale, flipX, flipY);
        static Vec2 Down(Vec2 p) => new(p.X, -p.Y);
        var lines = new Dictionary<string, Pen>();
        var fills = new List<Pen>();
        var texts = new Dictionary<string, Pen>();
        Pen Line(double w, string c, double[]? dash)
        {
            var key = $"{w}:{c}:{(dash == null ? "" : string.Join(" ", dash))}";
            if (!lines.TryGetValue(key, out var pen)) lines[key] = pen = new Pen(layer, c, w, null, dash);
            return pen;
        }
        foreach (var s in sd.Strokes)
        {
            var pts = s.Pts.Select(p => Down(x.Point(p))).ToList();
            var pen = Line(s.Width, s.Color ?? color, s.Dash);
            for (var i = 0; i + 1 < pts.Count; i++) pen.Segments.Add((pts[i], pts[i + 1]));
            if (s.Closed && pts.Count > 2) pen.Segments.Add((pts[^1], pts[0]));
        }
        foreach (var f in sd.Fills)
        {
            var pen = new Pen(layer + "-FILL", null, 0, f.Color ?? color, null);
            foreach (var l in f.Loops) pen.Loops.Add(l.Select(p => Down(x.Point(p))).ToList());
            fills.Add(pen);
        }
        foreach (var h in sd.Hatches)
        {
            var loops = h.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(p => Down(x.Point(p))).ToList()).ToList();
            var pattern = HatchLibrary.Find(h.Pattern, patterns);
            if (pattern == null || pattern.Solid) { var pen = new Pen(layer + "-FILL", null, 0, h.Color ?? color, null); pen.Loops.AddRange(loops); fills.Add(pen); continue; }
            var (segs, _) = HatchFill.Segments(loops, pattern, new HatchOptions(h.Scale, h.Angle - angle * 180 / Math.PI, Down(x.At), 20_000));
            var hp = Line(Pens.Default.Hatch, h.Color ?? color, null);
            foreach (var s in segs) hp.Segments.Add((s.A, s.B));
        }
        foreach (var t in sd.Texts)
        {
            var (p, a, anchor, height) = LayText(t, x);
            var c = t.Color ?? color;
            if (!texts.TryGetValue(c, out var pen)) texts[c] = pen = new Pen("TEXT", c, 0, null, null);
            pen.Texts.Add(new PenText(Down(p), t.Text, height, a, anchor, t.Baseline, t.Bold, t.Font, t.Italic));
        }
        return [.. fills, .. lines.Values, .. texts.Values];
    }
}
