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
    /// <summary>
    /// The exact curves some of the segments were laid along (a circle, an arc, an ellipse): only DXF reads them, writing the
    /// chords that still lie on one as that curve; the screen, PDF and SVG draw the chords (one rule: Chords).
    /// </summary>
    public List<PenCurve> Curves { get; } = [];
    /// <summary>Pictures laid on the drawing by their corners (an orthophoto under a site plan).</summary>
    public List<PenImage> Images { get; } = [];
    /// <summary>The box the pen is clipped to (x0, y0, x1, y1), as a sheet's viewport clips its view; null: none.</summary>
    public (double X0, double Y0, double X1, double Y1)? Clip { get; set; }
    /// <summary>On a sheet, the viewport the pen draws for (its id), so the interface moves that view's pens alone while it is dragged; null: the page's own (frame, title block).</summary>
    public string? Viewport { get; set; }
    /// <summary>On a sheet, the DXF block the pen belongs to (a title block's, decision 340); null: none. Only DXF reads it.</summary>
    public string? Block { get; set; }

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["layer"] = Layer, ["stroke"] = Stroke, ["width"] = Width, ["fill"] = Fill };
        if (Images.Count > 0) o["images"] = new JsonArray(Images.Select(i => (JsonNode)i.ToJson()).ToArray());
        if (Viewport != null) o["viewport"] = Viewport;
        if (Clip is { } c) o["clip"] = new JsonArray(Math.Round(c.X0, 4), Math.Round(c.Y0, 4), Math.Round(c.X1, 4), Math.Round(c.Y1, 4));
        if (Dash is { Length: > 0 }) o["dash"] = new JsonArray(Dash.Select(x => (JsonNode)x).ToArray());
        if (Segments.Count > 0) o["segments"] = new JsonArray(Segments.SelectMany(s => new[] { s.A.X, s.A.Y, s.B.X, s.B.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray());
        if (Texts.Count > 0) o["texts"] = new JsonArray(Texts.Select(t => (JsonNode)t.ToJson()).ToArray());
        if (Loops.Count > 0) o["loops"] = new JsonArray(Loops.Select(l => (JsonNode)new JsonArray(l.SelectMany(p => new[] { p.X, p.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray())).ToArray());
        return o;
    }
}

/// <summary>
/// An exact curve under a pen's chords: the points C + M cos t + N sin t for t from T0 over Sweep (anticlockwise in t; a
/// whole one when Sweep is 2π). A circle has M and N perpendicular and equal, an ellipse perpendicular; kept as conjugate
/// axes so any affine map (a sheet's scale and shift, a turn, a mirror, the paper's flip) maps it exactly (<see cref="Map"/>).
/// It draws nothing itself: the pen's chords do, and DXF writes the chords that lie on it as it.
/// </summary>
public sealed record PenCurve(Vec2 C, Vec2 M, Vec2 N, double T0, double Sweep)
{
    public bool Whole => Sweep >= 2 * Math.PI - 1e-9;

    public static PenCurve Circle(Vec2 c, double r) => new(c, new Vec2(r, 0), new Vec2(0, r), 0, 2 * Math.PI);
    /// <summary>An arc from <paramref name="start"/> anticlockwise over <paramref name="sweep"/> (radians).</summary>
    public static PenCurve Arc(Vec2 c, double r, double start, double sweep) => new(c, new Vec2(r, 0), new Vec2(0, r), start, sweep);
    /// <summary>An ellipse by its major axis (a vector) and its ratio, from parameter <paramref name="start"/> over <paramref name="sweep"/>.</summary>
    public static PenCurve Ellipse(Vec2 c, Vec2 major, double ratio, double start, double sweep) => new(c, major, new Vec2(-major.Y, major.X) * ratio, start, sweep);

    public Vec2 At(double t) => C + M * Math.Cos(t) + N * Math.Sin(t);

    /// <summary>The curve through an affine map (exact); through any other map only approximately, which DXF then finds off its chords.</summary>
    public PenCurve Map(Func<Vec2, Vec2> f)
    {
        var c = f(C);
        return this with { C = c, M = f(C + M) - c, N = f(C + N) - c };
    }

    /// <summary>
    /// The parameter of a point on the curve (within <paramref name="tol"/> of it, relative to its size), in [T0, T0 + 2π);
    /// null when it is off the curve. Whether it is inside the sweep is the caller's question.
    /// </summary>
    public double? ParamOf(Vec2 p, double tol = 1e-6)
    {
        var det = M.X * N.Y - M.Y * N.X;
        if (Math.Abs(det) < 1e-18) return null;
        var d = p - C;
        var u = (d.X * N.Y - d.Y * N.X) / det;
        var v = (M.X * d.Y - M.Y * d.X) / det;
        if (Math.Abs(Math.Sqrt(u * u + v * v) - 1) > tol) return null;
        var t = (Math.Atan2(v, u) - T0) % (2 * Math.PI);
        if (t < 0) t += 2 * Math.PI;
        return T0 + t;
    }
}

/// <summary>
/// A picture on a drawing: the image (a LapinImage's id) and where its corners
/// fall (top left, top right, bottom right, bottom left; view m, or paper mm on
/// a sheet), how opaque it is.
/// </summary>
public sealed record PenImage(string ImageId, Vec2[] Corners, double Opacity = 1, byte[]? Data = null, string? Mime = null)
{
    /// <summary>The picture carried with it (<see cref="Data"/>: a point cloud's slice, drawn for this view) rather than a LapinImage of the model.</summary>
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["id"] = ImageId, ["corners"] = new JsonArray(Corners.SelectMany(p => new[] { p.X, p.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray()), ["opacity"] = Opacity };
        if (Data != null) { o["data"] = Convert.ToBase64String(Data); o["mime"] = Mime ?? "image/png"; }
        return o;
    }
}

/// <summary>A text on a drawing: where (view coordinates, m), its height (paper mm), angle (°, counter-clockwise), anchor (start, middle, end) and baseline (bottom, middle).</summary>
public sealed record PenText(Vec2 At, string Text, double Height, double Angle = 0, string Anchor = "middle", string Baseline = "bottom", bool Bold = false, string Font = "sans", bool Italic = false)
{
    /// <summary>A live text's attribute tag in its pen's DXF block (PROJECT_NAME); null: a plain text.</summary>
    public string? Tag { get; init; }

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["x"] = Math.Round(At.X, 5), ["y"] = Math.Round(At.Y, 5), ["text"] = Text, ["height"] = Height, ["angle"] = Math.Round(Angle, 3),
            ["anchor"] = Anchor, ["baseline"] = Baseline, ["bold"] = Bold, ["font"] = Font,
        };
        if (Italic) o["italic"] = true;
        return o;
    }
}

/// <summary>
/// A drawing turned on its paper (a plan's orientation, decision 526): its points turned about the drawing's origin, its
/// texts with it, each kept readable (upright within ±90°, as drafting conventions do: read from the bottom or the right).
/// </summary>
public static class Turning
{
    /// <summary>A point turned anticlockwise by <paramref name="radians"/> about the origin.</summary>
    public static Vec2 Turn(Vec2 p, double radians)
    {
        if (radians == 0) return p;
        var (c, s) = (Math.Cos(radians), Math.Sin(radians));
        return new Vec2(p.X * c - p.Y * s, p.X * s + p.Y * c);
    }

    /// <summary>An angle (degrees) brought into (−180°, 180°].</summary>
    public static double Normalize(double deg)
    {
        var a = deg % 360;
        if (a <= -180) a += 360;
        if (a > 180) a -= 360;
        return a;
    }

    /// <summary>Whether a text at <paramref name="deg"/> (anticlockwise) reads upright: from the bottom, or from the right when vertical.</summary>
    public static bool Readable(double deg)
    {
        var a = Normalize(deg);
        return a > -90 + 1e-6 && a <= 90 + 1e-6;
    }

    /// <summary>
    /// A text as a drawing turned by <paramref name="turnDeg"/> shows it, still in the drawing's own coordinates (its
    /// point is placed by the caller's map, which turns it): its angle the turned one; one that would read upside down is
    /// turned over on its own footprint (anchor start ↔ end, its point moved across by its height), so it stays where it was.
    /// <paramref name="unitsPerMm"/>: drawing units per paper millimetre (the text's height is paper mm).
    /// </summary>
    public static PenText Upright(PenText t, double turnDeg, double unitsPerMm)
    {
        if (turnDeg == 0) return t;
        var a = t.Angle + turnDeg;
        if (Readable(a)) return t with { Angle = Normalize(a) };
        var r = t.Angle * Math.PI / 180;
        var up = new Vec2(-Math.Sin(r), Math.Cos(r));
        var at = t.Baseline == "middle" ? t.At : t.At + up * (t.Height * unitsPerMm);
        var anchor = t.Anchor switch { "start" => "end", "end" => "start", _ => t.Anchor };
        return t with { At = at, Angle = Normalize(a + 180), Anchor = anchor };
    }

    /// <summary>A pen turned by <paramref name="radians"/> about the drawing's origin, its texts readable (<see cref="Upright"/>).</summary>
    public static Pen Turned(Pen pen, double radians, double unitsPerMm)
    {
        if (radians == 0) return pen;
        var deg = radians * 180 / Math.PI;
        var o = new Pen(pen.Layer, pen.Stroke, pen.Width, pen.Fill, pen.Dash) { Viewport = pen.Viewport, Block = pen.Block, Clip = pen.Clip };
        o.Segments.AddRange(pen.Segments.Select(s => (Turn(s.A, radians), Turn(s.B, radians))));
        o.Curves.AddRange(pen.Curves.Select(c => c.Map(p => Turn(p, radians))));
        o.Loops.AddRange(pen.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(p => Turn(p, radians)).ToList()));
        o.Texts.AddRange(pen.Texts.Select(t => { var u = Upright(t, deg, unitsPerMm); return u with { At = Turn(u.At, radians) }; }));
        o.Images.AddRange(pen.Images.Select(i => i with { Corners = [.. i.Corners.Select(p => Turn(p, radians))] }));
        return o;
    }

    /// <summary>The bounds of pens' points (segments, loops, texts' points; not pictures, which reach far past the model); null when they draw nothing.</summary>
    /// <paramref name="radians"/>: as the pens show turned by it about the origin.
    public static (Vec2 Min, Vec2 Max)? Bounds(IEnumerable<Pen> pens, double radians = 0)
    {
        double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
        void Add(Vec2 p) { p = Turn(p, radians); x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
        foreach (var pen in pens)
        {
            foreach (var (a, b) in pen.Segments) { Add(a); Add(b); }
            foreach (var l in pen.Loops) foreach (var p in l) Add(p);
            foreach (var t in pen.Texts) Add(t.At);
        }
        return x0 <= x1 ? (new Vec2(x0, y0), new Vec2(x1, y1)) : null;
    }
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
        IReadOnlyDictionary<string, string>? ElementColors = null, LineStyles.PenTable? Table = null, IReadOnlyList<LineTypeDef>? LineTypes = null,
        double MinHatchSpacing = HatchScale.DefaultMinSpacing, double TextHeight = 2.5);

    /// <summary>A colour washed out towards white (a cut filled with a phase's colour stays readable under its hatch).</summary>
    static string Pale(string css) => HexColor.Mix(css, 0.6);

    /// <summary>
    /// The batts of the hatches that take one (<see cref="BattHatch"/>), by hatch: those of one material whose bands go on
    /// into one another (<see cref="MaterialHatch.Joins"/>) laid together, so the chain turns the corners they share; a band
    /// under 1.2 mm on paper gets none; the material's hatch scale stretches the heads' spacing, its angle of half a turn
    /// puts the first head on the other face.
    /// </summary>
    static Dictionary<int, List<HatchSegment>> Batts(List<MaterialHatch> hatches, Options o, int budget)
    {
        var batts = new Dictionary<int, List<HatchSegment>>();
        var mine = Enumerable.Range(0, hatches.Count).Where(i => BattHatch.Is(HatchLibrary.Find(hatches[i].Pattern, o.Patterns))).ToList();
        bool Flip(MaterialHatch h) => !h.Background && h.Look is { } bl && BattHatch.FlipsAt(bl.HatchAngle);
        foreach (var group in mine.GroupBy(i => (hatches[i].Background, hatches[i].Scale, Flip(hatches[i]), hatches[i].Join?.Key)))
        {
            var ids = group.ToList();
            var bands = ids.Select(i => new BattHatch.BattBand(hatches[i].Loops.Select(l => (IReadOnlyList<Vec2>)l).ToList(), hatches[i].Axis, hatches[i].Scale, Flip(hatches[i]))).ToList();
            var laid = BattHatch.Segments(bands, 1.2 * o.Scale / 1000, Math.Max(0, budget), (a, b) => hatches[ids[a]].Joins(hatches[ids[b]]));
            for (var k = 0; k < ids.Count; k++) { batts[ids[k]] = laid[k]; budget -= laid[k].Count; }
        }
        return batts;
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
            // A subcategory's own fill (decision 682) over the category's.
            var look = m != null && m.Graphics.TryGetValue(i, out var kg) ? kg : null;
            var keeps = look?.KeepsFill == true;
            // A view filter's own fill (stage 5) over its colour for the element's lines, paled.
            var fill = (look?.FilterFill != true && o.ElementColors?.GetValueOrDefault(p.Element) is { } ec ? Pale(ec) : null) ?? (keeps ? null : Cat(p.Class)?.FillColor) ?? (m != null && m.Fills.TryGetValue(i, out var f) ? f : null);
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

        // Hatches: the materials', unless the category lays its own over the cut (and then no background hatch either).
        var hatches = new List<MaterialHatch>(m?.Hatches ?? []);
        if (o.Categories != null)
        {
            // A band whose subcategory gives its own cut hatch (decision 682) keeps it: the subcategory is the more particular.
            hatches = hatches.Where(h => !(h.Background && h.Look?.KeepsHatch != true && Cat(h.Class)?.CutHatch != null))
                .Select(h => h.Look?.KeepsHatch != true && Cat(h.Class)?.CutHatch is { } ch ? h with { Pattern = ch, Scale = 1, Units = null } : h).ToList();
            for (var i = 0; i < d.Cuts.Count; i++)
                if (Cat(d.Cuts[i].Class)?.CutHatch is { } ch && !(m?.Hatches.Any(h => ReferenceEquals(h.Loops, d.Cuts[i].Loops)) ?? false)
                    && !(m != null && m.Graphics.TryGetValue(i, out var kg) && kg.KeepsHatch))
                    hatches.Add(new MaterialHatch(d.Cuts[i].Loops, ch, 1, null, d.Cuts[i].Class));
        }
        var budget = o.MaxHatchSegments;
        var batts = Batts(hatches, o, budget);
        for (var hi = 0; hi < hatches.Count; hi++)
        {
            var h = hatches[hi];
            var pattern = HatchLibrary.Find(h.Pattern, o.Patterns);
            if (pattern == null) continue;
            var category = Cat(h.Class)?.LineColor ?? "#000000";
            // The material's own colour, weight, line type, opacity and halftone (decision 678) over the category's and Lapin's;
            // a background hatch its own colour and weight, under the hatch (its pens keyed apart: Ordered lays them first).
            var look = h.Look;
            var bg = h.Background ? look?.Background : null;
            var color = look == null ? category : bg != null ? ResolvedMaterial.Ink(bg.HatchColor ?? category, look.Halftone, look.HatchOpacity) : look.HatchInk(category);
            var dash = !h.Background && look?.HatchLineType is { } lt && Dashes.Of(lt, o.LineTypes) is { Length: > 0 } ld ? ld : null;
            double WidthOf(int? pen) => bg?.HatchWeight ?? (h.Background ? null : look?.HatchWeight) ?? (pen is { } pn ? LineStyles.PenWidth(o.Table ?? LineStyles.DefaultPenTable(), pn, o.Scale) : Default.Hatch);
            var prefix = h.Background ? "hatchbg" : "hatch";
            if (BattHatch.Is(pattern))
            {
                // The batt's heads and strokes along the layer (its element's axis when the drawing gives one), metres; a band under
                // 1.2 mm on paper gets none. The material's hatch scale stretches the heads' spacing; its angle of half a turn
                // puts the first head on the other face (BattHatch).
                // Laid with the other bands of its material it goes on into (Batts), so it turns the corners they share.
                var batt = batts[hi];
                if (batt.Count > Math.Max(0, budget)) batt = batt.GetRange(0, Math.Max(0, budget));
                budget -= batt.Count;
                var bw = WidthOf(h.Pen);
                Get($"{prefix}:{color}:{bw}:{DashKey(dash)}", () => new Pen(PenLayer.Hatch, color, bw, null, dash)).Segments.AddRange(batt.Select(s => (s.A, s.B)));
                continue;
            }
            if (pattern.Solid)
            {
                // A solid pattern is the band's fill: in its fill colour (the category's, else the material's), else in ink (forum topic 88);
                // a hatch colour the material gives over all of them (decision 678).
                var solid = look?.HatchColor != null || bg?.HatchColor != null ? color : ((look?.KeepsFill == true ? null : Cat(h.Class)?.FillColor) ?? h.Fill ?? category);
                if (look != null && look.HatchColor == null && bg?.HatchColor == null) solid = ResolvedMaterial.Ink(solid, look.Halftone, look.HatchOpacity);
                Get($"{(h.Background ? "solidbg" : "solid")}:" + solid, () => new Pen(PenLayer.Hatch, null, 0, solid, null)).Loops.AddRange(h.Loops);
                continue;
            }
            // Paper patterns are paper millimetres at 1:1 (at 1:scale a millimetre is scale/1000 m), model ones the object's
            // (decision 679); a model pattern too dense on paper for the view's style is not drawn.
            if (HatchScale.Lay(pattern, o.Scale, h.Scale, h.Angle, h.Origin, Math.Max(0, budget), h.Units, o.MinHatchSpacing) is not { } laid) continue;
            var (segs, _) = HatchFill.Segments(h.Loops.Select(l => (IReadOnlyList<Vec2>)l).ToList(), pattern, laid);
            budget -= segs.Count;
            var width = WidthOf(h.Pen);
            Get($"{prefix}:{color}:{width}:{DashKey(dash)}", () => new Pen(PenLayer.Hatch, color, width, null, dash)).Segments.AddRange(segs.Select(s => (s.A, s.B)));
        }

        var separations = Separations(d);
        // The materials' own cut and layer lines (decision 678), by the line they fall on; none set, none looked for.
        var materialLines = m != null && m.Graphics.Values.Any(g => g.CutLine != null || g.SeparatorLine != null) ? MaterialLines(d, m, separations) : null;
        for (var i = 0; i < d.Lines.Count; i++)
        {
            if (m?.DropLines.Contains(i) == true) continue;
            var l = d.Lines[i];
            var g = Cat(l.Class);
            var separation = l.Role == LineRole.Cut && separations.Contains(i);
            if (g?.Hidden == true) continue;
            if (!o.HiddenLines && l.Role == LineRole.Hidden) continue;
            var styled = o.LineStyle?.Invoke(l.Role, l.Class, l.Element);
            // A layer line: its own style (Layer lines), field by field over the cut lines' (what it leaves unset is drawn as the cut).
            if (separation && o.LineStyle?.Invoke(LineRole.Separation, l.Class, l.Element) is { } sep)
                styled = new ResolvedLine(sep.WidthMm ?? styled?.WidthMm, sep.Color ?? styled?.Color, sep.Dash ?? styled?.Dash);
            // The material's own line, field by field over the Object styles' (a line edit and a filter's colour still win).
            if (materialLines != null && materialLines.TryGetValue(i, out var ml))
                styled = new ResolvedLine(ml.Weight ?? styled?.WidthMm, ml.Color ?? styled?.Color, ml.LineType != null ? Dashes.Of(ml.LineType, o.LineTypes) : styled?.Dash);
            // A line edit's style (Linework) wins over everything else.
            if (l.Style is { Empty: false } ls)
                styled = new ResolvedLine(ls.Pen is { } lsPen ? LineStyles.PenWidth(o.Table ?? LineStyles.DefaultPenTable(), lsPen, o.Scale) : styled?.WidthMm, ls.Color ?? styled?.Color, ls.Pattern != null ? Dashes.Of(ls.Pattern, o.LineTypes) : styled?.Dash);
            // The element's own style (an IFC file's colour and pattern) under everything else.
            if (l.Own is { Empty: false } own)
                styled = new ResolvedLine(styled?.WidthMm ?? (g?.LineWeight == null && own.Pen is { } ownPen ? LineStyles.PenWidth(o.Table ?? LineStyles.DefaultPenTable(), ownPen, o.Scale) : null),
                    styled?.Color ?? (g?.LineColor == null ? own.Color : null), styled?.Dash ?? (own.Pattern != null ? Dashes.Of(own.Pattern, o.LineTypes) : null));
            var color = o.ElementColors?.GetValueOrDefault(l.Element) ?? styled?.Color ?? g?.LineColor;
            Pen pen;
            switch (l.Role)
            {
                case LineRole.Hidden:
                {
                    var c = color ?? "#666666"; var w = styled?.WidthMm ?? Default.Hidden; var dash = styled?.Dash ?? Dashes.Of("hidden");
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
        // The texts of elements' own 2D: their height on the model as paper millimetres at the view's scale (without one, the
        // project's Standard text style's height, o.TextHeight: 2.5 mm unless the project changed it).
        foreach (var t in d.Texts ?? [])
        {
            if (Cat(t.Class)?.Hidden == true) continue;
            var c = o.ElementColors?.GetValueOrDefault(t.Element) ?? Cat(t.Class)?.LineColor ?? t.Color ?? "#000000";
            var mm = t.Height is { } h && h > 0 ? h * 1000 / o.Scale : o.TextHeight;
            Get("text:" + c, () => new Pen("TEXT", c, 0, null, null)).Texts.Add(new PenText(t.At, t.Text, Math.Round(mm, 3), t.Angle, "start", "bottom"));
        }
        if (m != null && m.Membranes.Count > 0)
            Get("membrane", () => new Pen(PenLayer.Membrane, "#000000", Default.Membrane, null, Dashes.Of("dashed"))).Segments.AddRange(m.Membranes);
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
        // On a grid in plan: every line against every edge took most of a drawing of timber-frame walls (report #125).
        var edges = new Dictionary<string, EdgeGrid>();
        for (var i = 0; i < d.Cuts.Count; i++)
        {
            var cut = d.Cuts[i];
            if (!edges.TryGetValue(cut.Class, out var grid)) edges[cut.Class] = grid = new EdgeGrid();
            foreach (var loop in cut.Loops)
                for (var k = 0; k < loop.Length; k++) grid.Add(i, loop[k], loop[(k + 1) % loop.Length]);
        }
        // A line of a polygon: its own polygon is the one whose edge holds it; the first found.
        var on = new HashSet<int>();
        for (var i = 0; i < d.Lines.Count; i++)
        {
            var l = d.Lines[i];
            if (l.Role != LineRole.Cut || !edges.TryGetValue(l.Class, out var grid)) continue;
            on.Clear();
            grid.Holding(l.A, l.B, on);
            if (on.Count >= 2) o.Add(i);
        }
        return o;
    }

    /// <summary>
    /// The line each cut line takes from the materials of the bands it bounds (decision 678): a line between two bands of
    /// one element (or of two joined elements of one class, <see cref="Separations"/>) their <see cref="ResolvedMaterial.SeparatorLine"/>,
    /// any other cut line their <see cref="ResolvedMaterial.CutLine"/>. Where two materials meet and both set one, the
    /// heavier line wins (a weight set over none), then the material whose name sorts first, so a line is drawn once, the
    /// same whichever side is read first. A line no material sets is left to the Object styles.
    /// </summary>
    public static Dictionary<int, MaterialLine> MaterialLines(ViewDrawing d, MaterialDrawingResult m, HashSet<int> separations)
    {
        var o = new Dictionary<int, MaterialLine>();
        var grid = new EdgeGrid();
        for (var i = 0; i < d.Cuts.Count; i++)
        {
            if (!m.Graphics.ContainsKey(i)) continue;
            foreach (var loop in d.Cuts[i].Loops)
                for (var k = 0; k < loop.Length; k++) grid.Add(i, loop[k], loop[(k + 1) % loop.Length]);
        }
        var on = new HashSet<int>();
        for (var i = 0; i < d.Lines.Count; i++)
        {
            var l = d.Lines[i];
            if (l.Role != LineRole.Cut) continue;
            on.Clear();
            grid.Holding(l.A, l.B, on);
            if (on.Count == 0) continue;
            var separator = separations.Contains(i);
            MaterialLine? best = null;
            string? bestName = null;
            foreach (var poly in on)
            {
                var g = m.Graphics[poly];
                if ((separator ? g.SeparatorLine : g.CutLine) is not { } spec) continue;
                var w = spec.Weight ?? -1;
                var bw = best?.Weight ?? -1;
                if (best == null || w > bw || (w == bw && string.CompareOrdinal(g.Name, bestName) < 0)) (best, bestName) = (spec, g.Name);
            }
            if (best != null) o[i] = best;
        }
        return o;
    }

    /// <summary>Polygons' edges on a grid in plan: an edge in the cells of its points every half cell, found from the middle of a line along it.</summary>
    sealed class EdgeGrid
    {
        const double Size = 0.5;
        readonly List<(int Poly, Vec2 A, Vec2 B)> _edges = [];
        readonly Dictionary<(long, long), List<int>> _cells = [];
        static (long, long) Key(Vec2 p) => ((long)Math.Floor(p.X / Size), (long)Math.Floor(p.Y / Size));

        public void Add(int poly, Vec2 a, Vec2 b)
        {
            var at = _edges.Count;
            _edges.Add((poly, a, b));
            var n = (int)Math.Min(1e6, Math.Ceiling((b - a).Length / (Size / 2)));
            (long, long)? last = null;
            for (var i = 0; i <= n; i++)
            {
                var key = Key(n == 0 ? a : a + (b - a) * ((double)i / n));
                if (key == last) continue;
                last = key;
                if (!_cells.TryGetValue(key, out var list)) _cells[key] = list = [];
                if (list.Count == 0 || list[^1] != at) list.Add(at);
            }
        }

        /// <summary>The polygons with an edge holding the segment pq: its middle lies on such an edge, so one of the edge's points is in that cell or next to it.</summary>
        public void Holding(Vec2 p, Vec2 q, HashSet<int> polys)
        {
            var (cx, cy) = Key((p + q) * 0.5);
            for (var dx = -1L; dx <= 1; dx++)
                for (var dy = -1L; dy <= 1; dy++)
                    if (_cells.TryGetValue((cx + dx, cy + dy), out var list))
                        foreach (var e in list)
                            if (!polys.Contains(_edges[e].Poly) && OnEdge(p, q, _edges[e].A, _edges[e].B)) polys.Add(_edges[e].Poly);
        }
    }

    /// <summary>Whether the segment pq lies along the edge ab (collinear, within it, 0.1 mm).</summary>
    static bool OnEdge(Vec2 p, Vec2 q, Vec2 a, Vec2 b)
    {
        const double tol = 1e-4;
        var d = b - a;
        var len = d.Length;
        if (len < 1e-9) return false;
        var u = d * (1 / len);
        return Holds(p) && Holds(q);
        bool Holds(Vec2 x)
        {
            var r = x - a;
            if (Math.Abs(Vec2.Cross(u, r)) > tol) return false;
            var t = Vec2.Dot(u, r);
            return t >= -tol && t <= len + tol;
        }
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

        // Fills first, then hatches (the materials' background hatches under their own, decision 678), then lines: beyond and
        // hidden, overhead, seen, membranes, cut.
        static double Order(string key, Pen p) => p.Layer switch
        {
            PenLayer.Fill => 0, PenLayer.Hatch => key.StartsWith("solidbg:", StringComparison.Ordinal) ? 0.5 : p.Fill != null ? 1 : key.StartsWith("hatchbg:", StringComparison.Ordinal) ? 1.5 : 2,
            PenLayer.Beyond or PenLayer.Hidden => 3, PenLayer.Overhead => 4, PenLayer.Seen => 5, PenLayer.Membrane => 6, _ => 7,
        };
        return pens.OrderBy(kv => Order(kv.Key, kv.Value)).Select(kv => kv.Value).ToList();
    }
}
