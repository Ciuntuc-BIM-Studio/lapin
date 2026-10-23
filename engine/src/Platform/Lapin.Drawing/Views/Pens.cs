using System.Text.Json.Nodes;
using BubbleBim.Geometry;
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

/// <summary>A view's per-category graphics: line colour and weight (heavy, medium, light, hairline), fill colour, a cut hatch over the materials'.</summary>
public sealed record CategoryGraphics(string? LineColor = null, string? LineWeight = null, string? FillColor = null, string? CutHatch = null, bool Hidden = false)
{
    public static CategoryGraphics? From(JsonNode? n) => n is JsonObject o
        ? new((string?)o["lineColor"], (string?)o["lineWeight"], (string?)o["fillColor"], (string?)o["cutHatch"], o["hidden"] is JsonValue h && h.TryGetValue<bool>(out var b) && b)
        : null;
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
    static readonly Dictionary<string, double> WeightMm = new() { ["heavy"] = 0.7, ["medium"] = 0.5, ["light"] = 0.25, ["hairline"] = 0.13 };

    public sealed record Options(double Scale = 100, bool HiddenLines = true, LineStyles.Resolver? LineStyle = null,
        IReadOnlyDictionary<string, CategoryGraphics>? Categories = null, IReadOnlyList<HatchPattern>? Patterns = null, int MaxHatchSegments = 60_000);

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
            var fill = Cat(p.Class)?.FillColor ?? (m != null && m.Fills.TryGetValue(i, out var f) ? f : null);
            if (fill == null) continue;
            var pen = Get("fill:" + fill, () => new Pen("FILL", null, 0, fill, null));
            pen.Loops.AddRange(p.Loops);
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
            if (pattern.Solid)
            {
                Get("solid:" + color, () => new Pen("HATCH", null, 0, color, null)).Loops.AddRange(h.Loops);
                continue;
            }
            // Patterns are paper millimetres at 1:1: at 1:scale a millimetre is scale/1000 m.
            var (segs, _) = HatchFill.Segments(h.Loops.Select(l => (IReadOnlyList<Vec2>)l).ToList(), pattern, new HatchOptions(h.Scale * o.Scale / 1000, MaxSegments: Math.Max(0, budget)));
            budget -= segs.Count;
            var width = h.Pen is { } pn ? LineStyles.PenWidth(LineStyles.DefaultPenTable(), pn, o.Scale) : Default.Hatch;
            Get($"hatch:{color}:{width}", () => new Pen("HATCH", color, width, null, null)).Segments.AddRange(segs.Select(s => (s.A, s.B)));
        }

        for (var i = 0; i < d.Lines.Count; i++)
        {
            if (m?.DropLines.Contains(i) == true) continue;
            var l = d.Lines[i];
            var g = Cat(l.Class);
            if (g?.Hidden == true) continue;
            if (!o.HiddenLines && l.Role == "hidden") continue;
            var styled = o.LineStyle?.Invoke(l.Role, l.Class, l.Element);
            var color = styled?.Color ?? g?.LineColor;
            Pen pen;
            switch (l.Role)
            {
                case "hidden":
                {
                    var c = color ?? "#666666"; var w = styled?.WidthMm ?? Default.Hidden; var dash = styled?.Dash ?? [1.5, 1];
                    pen = Get($"hidden:{c}:{w}:{DashKey(dash)}", () => new Pen("HIDDEN", c, w, null, dash));
                    break;
                }
                case "overhead":
                {
                    var c = color ?? "#888888"; var w = styled?.WidthMm ?? Default.Seen * 0.7; var dash = styled?.Dash ?? [2, 1];
                    pen = Get($"overhead:{c}:{w}:{DashKey(dash)}", () => new Pen("OVERHEAD", c, w, null, dash));
                    break;
                }
                case "beyond":
                {
                    var c = color ?? "#808080"; var w = styled?.WidthMm ?? Default.Hatch; var dash = styled?.Dash;
                    pen = Get($"beyond:{c}:{w}:{DashKey(dash)}", () => new Pen("BEYOND", c, w, null, dash is { Length: > 0 } ? dash : null));
                    break;
                }
                default:
                {
                    var cut = l.Role == "cut";
                    var c = color ?? "#000000";
                    var w = styled?.WidthMm ?? (g?.LineWeight is { } lw && WeightMm.TryGetValue(lw, out var mm) ? mm * (cut ? 1 : 0.5) : cut ? Default.Cut : Default.Seen);
                    var dash = styled?.Dash is { Length: > 0 } sd ? sd : null;
                    pen = Get($"{(cut ? "cut" : "seen")}:{c}:{w}:{DashKey(dash)}", () => new Pen(cut ? "CUT" : "SEEN", c, w, null, dash));
                    break;
                }
            }
            pen.Segments.Add((l.A, l.B));
        }
        if (m != null && m.Membranes.Count > 0)
            Get("membrane", () => new Pen("MEMBRANE", "#000000", Default.Membrane, null, [3, 1.5])).Segments.AddRange(m.Membranes);

        // Fills first, then hatches, then lines: beyond and hidden, overhead, seen, membranes, cut.
        static int Order(Pen p) => p.Layer switch
        {
            "FILL" => 0, "HATCH" => p.Fill != null ? 1 : 2, "BEYOND" or "HIDDEN" => 3, "OVERHEAD" => 4, "SEEN" => 5, "MEMBRANE" => 6, _ => 7,
        };
        return pens.Values.OrderBy(Order).ToList();
    }
}
