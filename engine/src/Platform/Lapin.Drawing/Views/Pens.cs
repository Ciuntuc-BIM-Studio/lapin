using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Geometry;
using Lapin.Drawing.Hatch;

namespace Lapin.Drawing.Views;

/// <summary>
/// One pen of a drawing: its layer (CUT, SEEN, HIDDEN, BEYOND, OVERHEAD, HATCH,
/// FILL, MEMBRANE: what DXF exports on), stroke colour and paper width (mm),
/// fill, dashes on paper (mm), and what it draws, in the view's coordinates
/// (metres): segments, or closed loops for a fill.
/// </summary>
public sealed class Pen(string layer, string? stroke, double width, string? fill, double[]? dash)
{
    public string Layer { get; } = layer;
    public string? Stroke { get; } = stroke;
    public double Width { get; } = width;
    public string? Fill { get; } = fill;
    public double[]? Dash { get; } = dash;
    public List<(Vec2 A, Vec2 B)> Segments { get; } = [];
    public List<IReadOnlyList<Vec2>> Loops { get; } = [];
    public List<PenText> Texts { get; } = [];

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["layer"] = Layer, ["stroke"] = Stroke, ["width"] = Width, ["fill"] = Fill };
        if (Dash is { Length: > 0 }) o["dash"] = new JsonArray(Dash.Select(x => (JsonNode)x).ToArray());
        if (Segments.Count > 0) o["segments"] = new JsonArray(Segments.SelectMany(s => new[] { s.A.X, s.A.Y, s.B.X, s.B.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray());
        if (Texts.Count > 0) o["texts"] = new JsonArray(Texts.Select(t => (JsonNode)t.ToJson()).ToArray());
        if (Loops.Count > 0) o["loops"] = new JsonArray(Loops.Select(l => (JsonNode)new JsonArray(l.SelectMany(p => new[] { p.X, p.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray())).ToArray());
        return o;
    }
}

/// <summary>A text on a drawing: where (view coordinates, m), its height (paper mm), angle (°, counter-clockwise), anchor (start, middle, end) and baseline (bottom, middle).</summary>
public sealed record PenText(Vec2 At, string Text, double Height, double Angle = 0, string Anchor = "middle", string Baseline = "bottom", bool Bold = false, string Font = "sans")
{
    public JsonObject ToJson() => new()
    {
        ["x"] = Math.Round(At.X, 5), ["y"] = Math.Round(At.Y, 5), ["text"] = Text, ["height"] = Height, ["angle"] = Math.Round(Angle, 3),
        ["anchor"] = Anchor, ["baseline"] = Baseline, ["bold"] = Bold, ["font"] = Font,
    };
}

/// <summary>
/// A view's per-category graphics: line colour and weight, fill colour, a cut
/// hatch over the materials'. The weight is a pen of the ISO 128 series in mm
/// ("0.13" … "1.0", see <see cref="Pens.IsoWeights"/>); the older names heavy,
/// medium, light and hairline still read as 0.7, 0.5, 0.25 and 0.13.
/// </summary>
public sealed record CategoryGraphics(string? LineColor = null, string? LineWeight = null, string? FillColor = null, string? CutHatch = null, bool Hidden = false)
{
    public static CategoryGraphics? From(JsonNode? n) => n is JsonObject o
        ? new((string?)o["lineColor"], Weight(o["lineWeight"]), (string?)o["fillColor"], (string?)o["cutHatch"], o["hidden"] is JsonValue h && h.TryGetValue<bool>(out var b) && b)
        : null;

    /// <summary>The weight as written: a name or a number (as text or as a JSON number).</summary>
    static string? Weight(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s
        : Json.Number(n) is { } mm ? mm.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
}

/// <summary>
/// A drawing as paper pens (the old Lapin's viewport pens): fills, then
/// hatches, then lines (beyond and hidden, seen, cut), each grouped by pen.
/// Defaults: cut 0.5, seen 0.25, hidden 0.18 dashed, overhead dashed grey,
/// beyond 0.13 grey, hatch 0.13 mm; the view's line styles and category
/// graphics over them.
/// </summary>
public static class Pens
{
    public static readonly (double Cut, double Seen, double Hidden, double Hatch, double Membrane) Default = (0.5, 0.25, 0.18, 0.13, 0.5);
    /// <summary>The line between two bands of one element (its layers' separation): two pens under the contour (0.5 → 0.25).</summary>
    public const double Separation = 0.25;
    /// <summary>The ISO 128 pen series (mm) a category's lines can take.</summary>
    public static readonly double[] IsoWeights = [0.13, 0.18, 0.25, 0.35, 0.5, 0.7, 1.0];
    static readonly Dictionary<string, double> Named = new() { ["heavy"] = 0.7, ["medium"] = 0.5, ["light"] = 0.25, ["hairline"] = 0.13 };

    /// <summary>A category weight as a pen of the series (the nearest one for a number in between); null when it is not one.</summary>
    public static double? WeightMm(string? weight)
    {
        if (weight == null) return null;
        if (Named.TryGetValue(weight, out var named)) return named;
        if (!double.TryParse(weight, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var mm) || !(mm > 0) || !double.IsFinite(mm)) return null;
        return IsoWeights.MinBy(w => Math.Abs(w - mm));
    }

    /// <summary>The weight a category's seen lines take when its cut lines take <paramref name="cut"/>: two pens down the series (0.5 → 0.25, 1.0 → 0.5), never under 0.13.</summary>
    public static double SeenWeight(double cut) => IsoWeights[Math.Max(0, Array.IndexOf(IsoWeights, cut) - 2)];

    public sealed record Options(double Scale = 100, bool HiddenLines = true, LineStyles.Resolver? LineStyle = null,
        IReadOnlyDictionary<string, CategoryGraphics>? Categories = null, IReadOnlyList<HatchPattern>? Patterns = null, int MaxHatchSegments = 60_000,
        IReadOnlyDictionary<string, string>? ElementColors = null, LineStyles.PenTable? Table = null);

    /// <summary>A colour washed out towards white (a cut filled with a phase's colour stays readable under its hatch).</summary>
    static string Pale(string css)
    {
        if (css is not { Length: 7 } || !int.TryParse(css[1..], System.Globalization.NumberStyles.HexNumber, null, out var n)) return css;
        int Mix(int c) => (int)Math.Round(c + (255 - c) * 0.6);
        return $"#{Mix((n >> 16) & 255):x2}{Mix((n >> 8) & 255):x2}{Mix(n & 255):x2}";
    }

    public static List<Pen> Of(ViewDrawing d, MaterialDrawingResult? m, Options o)
    {
        var pens = new Dictionary<string, Pen>();
        Pen Get(string key, Func<Pen> make) => pens.TryGetValue(key, out var p) ? p : pens[key] = make();
        CategoryGraphics? Cat(string cls) => LineStyles.CategoryOf(cls) is { } c && o.Categories != null && o.Categories.TryGetValue(c, out var g) ? g : null;
        string DashKey(double[]? dash) => dash == null ? "" : string.Join(" ", dash);

        // Fills: the material's (or the category's), none when neither gives one.
        for (var i = 0; i < d.Cuts.Count; i++)
        {
            if (m?.HidePolygons.Contains(i) == true) continue;
            var p = d.Cuts[i];
            var fill = (o.ElementColors?.GetValueOrDefault(p.Element) is { } ec ? Pale(ec) : null) ?? Cat(p.Class)?.FillColor ?? (m != null && m.Fills.TryGetValue(i, out var f) ? f : null);
            if (fill == null) continue;
            var pen = Get("fill:" + fill, () => new Pen(PenLayer.Fill, null, 0, fill, null));
            pen.Loops.AddRange(p.Loops);
        }

        // The fills of elements' own 2D (an IFC file's annotation fill areas): their colour, under the category's and a module's.
        foreach (var f in d.Fills ?? [])
        {
            var fill = (o.ElementColors?.GetValueOrDefault(f.Element) is { } ec ? Pale(ec) : null) ?? Cat(f.Class)?.FillColor ?? f.Color;
            if (fill == null || Cat(f.Class)?.Hidden == true) continue;
            Get("fill:" + fill, () => new Pen(PenLayer.Fill, null, 0, fill, null)).Loops.AddRange(f.Loops);
        }

        // Hatches: the materials', unless the category lays its own over the cut.
        var hatches = new List<MaterialHatch>(m?.Hatches ?? []);
        if (o.Categories != null)
        {
            hatches = hatches.Select(h => Cat(h.Class)?.CutHatch is { } ch ? h with { Pattern = ch, Scale = 1 } : h).ToList();
            for (var i = 0; i < d.Cuts.Count; i++)
                if (Cat(d.Cuts[i].Class)?.CutHatch is { } ch && !(m?.Hatches.Any(h => ReferenceEquals(h.Loops, d.Cuts[i].Loops)) ?? false))
                    hatches.Add(new MaterialHatch(d.Cuts[i].Loops, ch, 1, null, d.Cuts[i].Class));
        }
        var budget = o.MaxHatchSegments;
        foreach (var h in hatches)
        {
            var pattern = HatchLibrary.Find(h.Pattern, o.Patterns);
            if (pattern == null) continue;
            var color = Cat(h.Class)?.LineColor ?? "#000000";
            if (BattHatch.Is(pattern))
            {
                // The looped line along the layer, metres; a band under 1.2 mm on paper gets none.
                var batt = BattHatch.Segments(h.Loops.Select(l => (IReadOnlyList<Vec2>)l).ToList(), 1.2 * o.Scale / 1000, Math.Max(0, budget));
                budget -= batt.Count;
                var bw = h.Pen is { } bp ? LineStyles.PenWidth(o.Table ?? LineStyles.DefaultPenTable(), bp, o.Scale) : Default.Hatch;
                Get($"hatch:{color}:{bw}", () => new Pen(PenLayer.Hatch, color, bw, null, null)).Segments.AddRange(batt.Select(s => (s.A, s.B)));
                continue;
            }
            if (pattern.Solid)
            {
                Get("solid:" + color, () => new Pen(PenLayer.Hatch, null, 0, color, null)).Loops.AddRange(h.Loops);
                continue;
            }
            // Patterns are paper millimetres at 1:1: at 1:scale a millimetre is scale/1000 m.
            var (segs, _) = HatchFill.Segments(h.Loops.Select(l => (IReadOnlyList<Vec2>)l).ToList(), pattern, new HatchOptions(h.Scale * o.Scale / 1000, h.Angle, h.Origin, Math.Max(0, budget)));
            budget -= segs.Count;
            var width = h.Pen is { } pn ? LineStyles.PenWidth(o.Table ?? LineStyles.DefaultPenTable(), pn, o.Scale) : Default.Hatch;
            Get($"hatch:{color}:{width}", () => new Pen(PenLayer.Hatch, color, width, null, null)).Segments.AddRange(segs.Select(s => (s.A, s.B)));
        }

        var separations = Separations(d);
        for (var i = 0; i < d.Lines.Count; i++)
        {
            if (m?.DropLines.Contains(i) == true) continue;
            var l = d.Lines[i];
            var g = Cat(l.Class);
            var separation = l.Role == LineRole.Cut && separations.Contains(i);
            if (g?.Hidden == true) continue;
            if (!o.HiddenLines && l.Role == LineRole.Hidden) continue;
            var styled = separation ? o.LineStyle?.Invoke(LineRole.Separation, l.Class, l.Element) ?? o.LineStyle?.Invoke(l.Role, l.Class, l.Element) : o.LineStyle?.Invoke(l.Role, l.Class, l.Element);
            // A line edit's style (Linework) wins over everything else.
            if (l.Style is { Empty: false } ls)
                styled = new ResolvedLine(ls.Pen is { } lsPen ? LineStyles.PenWidth(o.Table ?? LineStyles.DefaultPenTable(), lsPen, o.Scale) : styled?.WidthMm, ls.Color ?? styled?.Color, ls.Pattern != null ? LineStyles.DashOf(ls.Pattern) : styled?.Dash);
            // The element's own style (an IFC file's colour and pattern) under everything else.
            if (l.Own is { Empty: false } own)
                styled = new ResolvedLine(styled?.WidthMm, styled?.Color ?? (g?.LineColor == null ? own.Color : null), styled?.Dash ?? (own.Pattern != null ? LineStyles.DashOf(own.Pattern) : null));
            var color = o.ElementColors?.GetValueOrDefault(l.Element) ?? styled?.Color ?? g?.LineColor;
            Pen pen;
            switch (l.Role)
            {
                case LineRole.Hidden:
                {
                    var c = color ?? "#666666"; var w = styled?.WidthMm ?? Default.Hidden; var dash = styled?.Dash ?? [1.5, 1];
                    pen = Get($"hidden:{c}:{w}:{DashKey(dash)}", () => new Pen(PenLayer.Hidden, c, w, null, dash));
                    break;
                }
                case LineRole.Overhead:
                {
                    var c = color ?? "#888888"; var w = styled?.WidthMm ?? Default.Seen * 0.7; var dash = styled?.Dash ?? [2, 1];
                    pen = Get($"overhead:{c}:{w}:{DashKey(dash)}", () => new Pen(PenLayer.Overhead, c, w, null, dash));
                    break;
                }
                case LineRole.Beyond:
                {
                    var c = color ?? "#9ca3af"; var w = styled?.WidthMm ?? Default.Hatch; var dash = styled?.Dash;
                    pen = Get($"beyond:{c}:{w}:{DashKey(dash)}", () => new Pen(PenLayer.Beyond, c, w, null, dash is { Length: > 0 } ? dash : null));
                    break;
                }
                default:
                {
                    var cut = l.Role == LineRole.Cut;
                    var c = color ?? "#000000";
                    var w = styled?.WidthMm ?? (WeightMm(g?.LineWeight) is { } mm ? (separation ? SeenWeight(mm) : cut ? mm : SeenWeight(mm)) : separation ? Separation : cut ? Default.Cut : Default.Seen);
                    var dash = styled?.Dash is { Length: > 0 } sd ? sd : null;
                    pen = Get($"{(cut ? "cut" : "seen")}:{c}:{w}:{DashKey(dash)}", () => new Pen(cut ? PenLayer.Cut : PenLayer.Seen, c, w, null, dash));
                    break;
                }
            }
            pen.Segments.Add((l.A, l.B));
        }
        // The texts of elements' own 2D: their height on the model as paper millimetres at the view's scale (2.5 mm without one).
        foreach (var t in d.Texts ?? [])
        {
            if (Cat(t.Class)?.Hidden == true) continue;
            var c = o.ElementColors?.GetValueOrDefault(t.Element) ?? Cat(t.Class)?.LineColor ?? t.Color ?? "#000000";
            var mm = t.Height is { } h && h > 0 ? h * 1000 / o.Scale : 2.5;
            Get("text:" + c, () => new Pen("TEXT", c, 0, null, null)).Texts.Add(new PenText(t.At, t.Text, Math.Round(mm, 3), t.Angle, "start", "bottom"));
        }
        if (m != null && m.Membranes.Count > 0)
            Get("membrane", () => new Pen(PenLayer.Membrane, "#000000", Default.Membrane, null, [3, 1.5])).Segments.AddRange(m.Membranes);
        return Ordered(pens);
    }

    /// <summary>
    /// The cut lines that run between two bands of one element (a wall's
    /// layers, a floor's), or of two joined elements of one class (a footing's
    /// core against the next one's waterproofing round a corner): a line on the
    /// edge of another cut polygon of the same class. They draw with the
    /// separation pen; the contour keeps the cut pen.
    /// </summary>
    public static HashSet<int> Separations(ViewDrawing d)
    {
        var o = new HashSet<int>();
        // The cut edges of each class's elements, by polygon: the layers of one element, and of the elements of its kind
        // it is joined to (a footing's core running on past the end of another's, against that one's waterproofing).
        var edges = new Dictionary<string, List<(int Poly, Vec2 A, Vec2 B)>>();
        for (var i = 0; i < d.Cuts.Count; i++)
            foreach (var loop in d.Cuts[i].Loops)
                for (var k = 0; k < loop.Length; k++)
                    (edges.TryGetValue(d.Cuts[i].Class, out var l) ? l : edges[d.Cuts[i].Class] = []).Add((i, loop[k], loop[(k + 1) % loop.Length]));
        // A line of a polygon: its own polygon is the one whose edge holds it; the first found.
        for (var i = 0; i < d.Lines.Count; i++)
        {
            var l = d.Lines[i];
            if (l.Role != LineRole.Cut || !edges.TryGetValue(l.Class, out var es)) continue;
            var on = new HashSet<int>();
            foreach (var (poly, a, b) in es) if (OnEdge(l.A, l.B, a, b)) on.Add(poly);
            if (on.Count >= 2) o.Add(i);
        }
        return o;
    }

    /// <summary>Whether the segment pq lies along the edge ab (collinear, within it, 0.1 mm).</summary>
    static bool OnEdge(Vec2 p, Vec2 q, Vec2 a, Vec2 b)
    {
        const double tol = 1e-4;
        var d = b - a;
        var len = d.Length;
        if (len < 1e-9) return false;
        var u = d * (1 / len);
        foreach (var x in new[] { p, q })
        {
            var r = x - a;
            if (Math.Abs(Vec2.Cross(u, r)) > tol) return false;
            var t = Vec2.Dot(u, r);
            if (t < -tol || t > len + tol) return false;
        }
        return true;
    }

    static List<Pen> Ordered(Dictionary<string, Pen> pens)
    {

        // Whole lines: collinear pieces the hidden-line removal split are joined again.
        foreach (var p in pens.Values.Where(p => p.Layer != PenLayer.Hatch && p.Segments.Count > 1))
        {
            var merged = LineMerge.Merge(p.Segments);
            p.Segments.Clear();
            p.Segments.AddRange(merged);
        }

        // Fills first, then hatches, then lines: beyond and hidden, overhead, seen, membranes, cut.
        static int Order(Pen p) => p.Layer switch
        {
            PenLayer.Fill => 0, PenLayer.Hatch => p.Fill != null ? 1 : 2, PenLayer.Beyond or PenLayer.Hidden => 3, PenLayer.Overhead => 4, PenLayer.Seen => 5, PenLayer.Membrane => 6, _ => 7,
        };
        return pens.Values.OrderBy(Order).ToList();
    }
}
