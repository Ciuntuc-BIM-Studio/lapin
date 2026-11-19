using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Keys to a drawing (decision 620): a legend on a sheet's paper of what its views draw, live (the materials their cuts are
/// hatched and filled with, or the lines they are drawn in), and a scale bar, on the paper (at a view's scale or its own) or
/// on any view (at the view's), and a north arrow (issue #111) on a plan or a sheet's paper. Each is an IfcAnnotation of the
/// view it is on (<c>SHEETLEGEND</c> {viewId, at, kind: materials | lines, title}; <c>SCALEBAR</c> {viewId, at, length (m,
/// none: a round length about 80 mm long on paper), divisions, scale, viewportId, look: site}; <c>NORTHARROW</c> {viewId, at,
/// size (paper mm, none: 14), viewportId, symbolSlots.northArrow}); <c>at</c> is the legend's top-left corner, the bar's left
/// end, the arrow's middle, in the view's coordinates. A bar or an arrow on a view without <c>at</c> stands where a site plan
/// drew its own (<see cref="AutoAt"/>: the arrow right of the drawing's top, the bar under its bottom left), until it is moved.
/// </summary>
public static class DrawingKeys
{
    public const string Legend = "SHEETLEGEND";
    public const string ScaleBar = "SCALEBAR";
    public const string NorthArrow = "NORTHARROW";
    /// <summary>A scale bar's looks: Lapin's (none), or a site plan's (<c>site</c>: about 60 mm in five parts, its length's half and whole written).</summary>
    public static readonly string[] Looks = ["site"];
    /// <summary>A north arrow's size when it gives none: its circle's diameter on paper (mm).</summary>
    public const double ArrowSize = 14;
    public static readonly string[] LegendKinds = ["materials", "lines"];

    const double Row = 7, Swatch = 14, SwatchH = 5, Label = 2.5, TitleH = 3.5;

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var e in c.Model.OfClass(IfcClass.Annotation).Where(x => x.Str("viewId") == c.View.Id))
        {
            ViewMark? mk = null;
            try
            {
                if (e.PredefinedType == Legend) mk = LegendMark(c, e);
                else if (e.PredefinedType == ScaleBar) mk = ScaleBarMark(c, e);
                else if (e.PredefinedType == NorthArrow) mk = NorthArrowMark(c, e);
            }
            catch (CommandException) { }
            if (mk != null) yield return mk;
        }
    }

    static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------ the legend

    /// <summary>One row of a legend: its name, and what its swatch shows (a fill, a hatch, or a line).</summary>
    /// <param name="Look">A material's graphics, which its swatch is drawn with (decision 678: its hatch colour and weight, background hatch, fill opacity).</param>
    public sealed record Entry(string Name, string? Fill = null, string? Hatch = null, double HatchScale = 1, double HatchAngle = 0, string? Stroke = null, double Width = 0, double[]? Dash = null,
        ResolvedMaterial? Look = null);

    /// <summary>What a legend lists: the materials the sheet's views cut, by name; or the lines they draw (role, type and width).</summary>
    public static List<Entry> Entries(IQueryContext q, Entity view, string kind, string? lang = null)
    {
        var m = q.Model;
        if (PaperViews.SheetOf(m, view) is not { } sheet) return [];
        var placed = PaperViews.Placements(q, sheet).Where(p => !p.Content).ToList();
        if (kind == "lines")
        {
            var book = DraftingResources.Book(m);
            var rows = new Dictionary<string, Entry>();
            foreach (var pen in placed.SelectMany(p => p.Drawing.Pens))
            {
                if (pen.Segments.Count == 0 || pen.Stroke == null || pen.Width <= 0 || !Roles.TryGetValue(pen.Layer, out var role)) continue;
                var type = LineTypeName(book, pen.Dash);
                // Lapin's own role and type names in the legend's language (a project's own line type keeps its name).
                var name = $"{DrawingWords.Of(role, lang)} · {DrawingWords.Of(type, lang)} · {Num(pen.Width)} mm";
                rows.TryAdd(name, new Entry(name, Stroke: pen.Stroke, Width: pen.Width, Dash: pen.Dash));
            }
            return [.. rows.Values.OrderByDescending(r => r.Width).ThenBy(r => r.Name, StringComparer.Ordinal).Take(30)];
        }
        var o = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var pl in placed)
        {
            if (pl.Drawing.Generated is not { } drawing) continue;
            var look = pl.Drawing.Kind == "plan" ? "plan" : "section";
            // At the placed view's scale (decision 680): a material's step for it, as the view draws it, with the view's material
            // graphics set (decision 682), without the small-scale cut; and its filters' hatches and fills (stage 5) as rows of their own.
            var vg = VisibilityGraphics.Of(m, pl.View);
            var viewMaterials = new ViewMaterialGraphics(m, look, pl.Drawing.Scale, ViewStyles.Of(m, pl.View), vg, smallScale: false);
            var bands = new Dictionary<string, IReadOnlyList<LayerBand>?>();
            foreach (var cut in drawing.Cuts)
            {
                if (vg.HasFilterGraphics && m.Get(cut.Element) is { } el && vg.FilterGraphics(el) is { } fl
                    && (fl.Gives("cutHatch") || fl.Gives("cutFill")) && !o.ContainsKey(fl.Names) && viewMaterials.Band(cut) is { Filters: { } names } fr)
                {
                    // What the filters themselves draw, over a band of no material of its own.
                    var r = fr with { Name = names, Hatch = fl.Gives("cutHatch") ? fr.Hatch : null, Fill = fl.Gives("cutFill") ? fr.Fill : null, Background = null };
                    o[names] = new Entry(names, r.Fill, r.Hatch, r.HatchScale <= 0 ? 1 : r.HatchScale, r.HatchAngle, Look: r);
                }
                IEnumerable<string> mats = cut.Material is { } one ? [one]
                    : (bands.TryGetValue(cut.Element, out var b) ? b : bands[cut.Element] = LayerSets.DrawnBandsOf(m, cut.Element))?.Select(x => x.Material) ?? [];
                foreach (var id in mats.Where(x => !string.IsNullOrEmpty(x)))
                {
                    var r = viewMaterials.Material(id);
                    if (r == null || r.Membrane || o.ContainsKey(r.Name)) continue;
                    o[r.Name] = new Entry(r.Name, r.Fill, r.Hatch, r.HatchScale <= 0 ? 1 : r.HatchScale, r.HatchAngle, Look: r);
                }
            }
        }
        return [.. o.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).Take(40)];
    }

    static readonly Dictionary<string, string> Roles = new()
    {
        ["CUT"] = "Cut", ["SEEN"] = "Seen", ["HIDDEN"] = "Hidden", ["OVERHEAD"] = "Overhead", ["BEYOND"] = "Beyond", ["MEMBRANE"] = "Membrane",
    };

    /// <summary>The line type whose dashes a pen draws with: Lapin's or the project's own, by name; "dashes" when none matches.</summary>
    static string LineTypeName(StyleBook book, double[]? dash)
    {
        if (dash is not { Length: > 0 }) return "continuous";
        foreach (var name in Standards.LineTypes.Concat(book.LineTypes?.Select(l => l.Name) ?? []))
            if (Standards.DashOf(book, name) is { Length: > 0 } d && d.Length == dash.Length && d.Zip(dash).All(z => Math.Abs(z.First - z.Second) < 1e-6)) return name;
        return "dashes";
    }

    static ViewMark? LegendMark(ViewContext c, Entity e)
    {
        if (c.Query == null) return null;
        var at = Args.PointOf(e.Attrs["at"]);
        var kind = e.Str("kind") == "lines" ? "lines" : "materials";
        // Its words in its own language (audit §16 leftovers), set from the interface's when it was placed.
        var lang = DrawingWords.Known(e.Str("lang"));
        var rows = Entries(c.Query, c.View, kind, lang);
        var mm = c.Mm;
        var text = Standards.ResolveText(c.Book, []);
        var mk = new ViewMark(e.Id);
        var title = e.Str("title") is { Length: > 0 } t ? PaperViews.Fill(c, t) : DrawingWords.Of(kind == "lines" ? "LINES" : "LEGEND", lang);
        mk.Text(new PenText(at - new Vec2(0, TitleH * mm), title, TitleH, 0, "start", "bottom", true, text.Font));
        var patterns = DraftingResources.Patterns(c.Model);
        var y = at.Y - (TitleH + 4) * mm;
        var width = Swatch + 4 + Label * 0.6 * Math.Max(12, rows.Count == 0 ? 0 : rows.Max(r => r.Name.Length));
        if (rows.Count == 0)
        {
            mk.Text(new PenText(new Vec2(at.X, y - SwatchH * mm), DrawingWords.Of("(none on this sheet's views)", lang), Label, 0, "start", "bottom", false, text.Font), "#808080");
            y -= Row * mm;
        }
        foreach (var r in rows)
        {
            var (x0, x1, y1, y0) = (at.X, at.X + Swatch * mm, y, y - SwatchH * mm);
            IReadOnlyList<Vec2> box = [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];
            if (kind == "lines")
            {
                var mid = (y0 + y1) / 2;
                mk.Line(new Vec2(x0, mid), new Vec2(x1, mid), "LEGEND", r.Width, r.Stroke ?? "#000000", r.Dash);
            }
            else
            {
                // The swatch drawn as the drawings draw the material (MaterialSample): its fill, background hatch and hatch.
                var look = r.Look ?? new ResolvedMaterial(r.Name, r.Hatch, r.HatchScale, r.Fill, null, false, false, null, "own", r.HatchAngle);
                foreach (var pen in MaterialSample.Swatch(look, box, mm, patterns))
                {
                    if (pen.Fill != null && pen.Loops.Count > 0) mk.Fills.Add((pen.Loops, pen.Fill));
                    if (pen.Stroke != null) foreach (var (a, b) in pen.Segments) mk.Line(a, b, "LEGEND", pen.Width, pen.Stroke, pen.Dash);
                }
                mk.Polyline(box, true, "LEGEND", 0.18);
            }
            mk.Text(new PenText(new Vec2(x1 + 4 * mm, y0 + 1 * mm), r.Name, Label, 0, "start", "bottom", false, text.Font));
            y -= Row * mm;
        }
        // What a click takes: the whole legend.
        var right = at.X + width * mm;
        IReadOnlyList<Vec2> all = [new(at.X, y + 2 * mm), new(right, y + 2 * mm), new(right, at.Y), new(at.X, at.Y)];
        mk.Hit = ([all], true);
        for (var i = 0; i < 4; i++) mk.Edges.Add((all[i], all[(i + 1) % 4]));
        mk.Snaps.Add(at);
        mk.Data = new JsonObject { ["legend"] = kind, ["rows"] = rows.Count };
        return mk;
    }

    // ------------------------------------------------------------ the scale bar

    /// <summary>The scale a bar is drawn at: on paper, its view's (a viewport of the sheet), its own, or the sheet's first view's; on a view, the view's.</summary>
    public static double ScaleOf(ViewContext c, Entity e)
    {
        if (!PaperViews.Is(c.View)) return c.Scale;
        if (c.Query != null && PaperViews.SheetOf(c.Model, c.View) is { } sheet)
        {
            var placed = PaperViews.Placements(c.Query, sheet).Where(p => !p.Content).ToList();
            if (e.Str("viewportId") is { } vp && placed.FirstOrDefault(p => p.Viewport.Id == vp) is { } pl) return pl.Drawing.Scale;
            if (e.NumOrNull("scale") is double own && own > 0) return own;
            if (placed.Count > 0) return placed[0].Drawing.Scale;
        }
        return e.NumOrNull("scale") is double s && s > 0 ? s : 100;
    }

    /// <summary>A round length (1, 2 or 5 × 10ⁿ m) near what is about <paramref name="paperMm"/> long on paper at 1:<paramref name="scale"/>.</summary>
    public static double NiceLength(double scale, double paperMm = 80)
    {
        var raw = paperMm * scale / 1000;
        var p = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        foreach (var k in new[] { 5.0, 2, 1 }) if (k * p <= raw * 1.0001) return k * p;
        return p;
    }

    /// <summary>A bar's divisions when none are set: round steps (5 m in five, 2 m in four, 1 m in five).</summary>
    public static int DivisionsOf(double length)
    {
        var lead = length / Math.Pow(10, Math.Floor(Math.Log10(length)));
        return Math.Abs(lead - 2) < 1e-6 ? 4 : Math.Abs(lead - 5) < 1e-6 || Math.Abs(lead - 1) < 1e-6 ? 5 : 4;
    }

    /// <summary>
    /// Where a scale bar's left end or a north arrow's middle stands on a view when it has no point of its own: where a site plan
    /// drew its own (8 mm round the drawing; the arrow right of its top, the bar 6 mm under its bottom left); null on a sheet's
    /// paper (a key there is always placed) or with nothing drawn.
    /// </summary>
    public static Vec2? AutoAt(ViewContext c, Entity e)
    {
        if (PaperViews.Is(c.View) || c.Drawing.Bounds is not { } box) return null;
        var mm = c.Mm;
        var (lo, hi) = (box.Min - new Vec2(8 * mm, 8 * mm), box.Max + new Vec2(8 * mm, 8 * mm));
        if (e.PredefinedType == NorthArrow)
        {
            var r = SizeOf(e) / 2 * mm;
            return new Vec2(hi.X + 4 * mm + r, hi.Y - r - 6 * mm);
        }
        return new Vec2(lo.X, lo.Y - 6 * mm);
    }

    /// <summary>A key's point: its own, else where it stands by itself (<see cref="AutoAt"/>); null: nowhere to draw it.</summary>
    static Vec2? AtOf(ViewContext c, Entity e) => Args.TryPoint(e.Attrs["at"]) ?? AutoAt(c, e);

    /// <summary>A north arrow's size (its circle's diameter on paper, mm).</summary>
    public static double SizeOf(Entity e) => e.NumOrNull("size") is double s && s > 0 ? s : ArrowSize;

    /// <summary>The site plan's look of a bar: its first scale bar's, as a site plan drew it before (decision 722).</summary>
    static bool SiteLook(Entity e) => e.Str("look") == "site";

    /// <summary>What the mark tells the interface of a key: its point as drawn, and whether it stands by itself.</summary>
    static JsonObject KeyData(Entity e, Vec2 at) => new() { ["at"] = new JsonArray(at.X, at.Y), ["auto"] = e.Attrs["at"] is not JsonArray };

    static ViewMark? ScaleBarMark(ViewContext c, Entity e)
    {
        if (AtOf(c, e) is not { } at) return null;
        var scale = ScaleOf(c, e);
        var mm = c.Mm;
        // Drawing units for a metre of the model: one on a view; on the paper (m of paper), what a metre is at the bar's scale.
        var perMetre = PaperViews.Is(c.View) ? 1 / scale : 1;
        var text = Standards.ResolveText(c.Book, []);
        var mk = new ViewMark(e.Id);
        if (SiteLook(e))
        {
            // A site plan's bar (as SitePlans drew it): about 60 mm on paper, in five parts alternately filled, its half and its length written.
            var length0 = e.NumOrNull("length") is double l0 && l0 > 0 ? l0 : SitePlans.Nice(60 * scale / 1000);
            var parts = Math.Clamp((int)(e.NumOrNull("divisions") ?? 5), 1, 20);
            var part = length0 / parts * perMetre;
            var h0 = 1.5 * mm;
            const string layer = "SITE";
            for (var k = 0; k < parts; k++)
            {
                Vec2 a = at + new Vec2(k * part, 0), b = at + new Vec2((k + 1) * part, h0);
                var rect = new[] { a, new Vec2(b.X, a.Y), b, new Vec2(a.X, b.Y) };
                mk.Polyline(rect, true, layer, Pens.Default.Seen, "#000000");
                if (k % 2 == 0) mk.Fills.Add(([rect], "#000000"));
            }
            static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
            foreach (var (x, label) in new[] { (0.0, "0"), (length0 / 2, F0(length0 / 2)), (length0, $"{F0(length0)} m") })
                mk.Text(new PenText(at + new Vec2(x * perMetre, h0 + 1.2 * mm), label, text.Height * 0.8, 0, "middle", "bottom", false, text.Font), "#000000");
            mk.Text(new PenText(at + new Vec2(0, -2.5 * mm), $"1:{F0(scale)}", text.Height * 0.8, 0, "start", "top", false, text.Font), "#000000");
            var end0 = at.X + length0 * perMetre;
            IReadOnlyList<Vec2> box0 = [new(at.X, at.Y - 5 * mm), new(end0, at.Y - 5 * mm), new(end0, at.Y + h0 + 4 * mm), new(at.X, at.Y + h0 + 4 * mm)];
            mk.Hit = ([box0], true);
            mk.Snaps.AddRange([at, new Vec2(end0, at.Y)]);
            mk.Data = KeyData(e, at);
            mk.Data["scale"] = scale; mk.Data["length"] = length0; mk.Data["divisions"] = parts;
            return mk;
        }
        var length = e.NumOrNull("length") is double l && l > 0 ? l : NiceLength(scale);
        var n = Math.Clamp((int)(e.NumOrNull("divisions") ?? DivisionsOf(length)), 1, 20);
        var h = 2 * mm;
        var step = length / n * perMetre;
        for (var i = 0; i < n; i++)
        {
            var (x0, x1) = (at.X + i * step, at.X + (i + 1) * step);
            IReadOnlyList<Vec2> box = [new(x0, at.Y), new(x1, at.Y), new(x1, at.Y + h), new(x0, at.Y + h)];
            if (i % 2 == 0) mk.Fills.Add(([box], "#000000"));
            mk.Polyline(box, true, "SCALEBAR", 0.18);
            mk.Text(new PenText(new Vec2(x0, at.Y + h + 1 * mm), Num(length / n * i), 2, 0, "middle", "bottom", false, text.Font));
        }
        var end = at.X + length * perMetre;
        mk.Text(new PenText(new Vec2(end, at.Y + h + 1 * mm), $"{Num(length)} m", 2, 0, "middle", "bottom", false, text.Font));
        mk.Text(new PenText(new Vec2(at.X, at.Y - 3.5 * mm), $"1:{scale:0}", 2.5, 0, "start", "bottom", true, text.Font));
        IReadOnlyList<Vec2> all = [new(at.X, at.Y - 4 * mm), new(end, at.Y - 4 * mm), new(end, at.Y + h + 4 * mm), new(at.X, at.Y + h + 4 * mm)];
        mk.Hit = ([all], true);
        mk.Snaps.AddRange([at, new Vec2(end, at.Y)]);
        mk.Data = KeyData(e, at);
        mk.Data["scale"] = scale; mk.Data["length"] = length; mk.Data["divisions"] = n;
        return mk;
    }

    // ------------------------------------------------------------ the north arrow

    /// <summary>
    /// True north as drawn (a unit vector, the view's coordinates): the project's +Y turned by the base point's angle (as the IFC
    /// export writes it); on a sheet's paper, as its viewport (<c>viewportId</c>, else the sheet's first plan) lays that view out.
    /// </summary>
    public static Vec2 NorthOf(ViewContext c, Entity e)
    {
        var th = (Location.Base(c.Model).NorthAngle ?? 0) * Math.PI / 180;
        var n = new Vec2(-Math.Sin(th), Math.Cos(th));
        if (!PaperViews.Is(c.View) || c.Query == null || PaperViews.SheetOf(c.Model, c.View) is not { } sheet) return n;
        var placed = PaperViews.Placements(c.Query, sheet).Where(p => !p.Content).ToList();
        var pl = e.Str("viewportId") is { } vp ? placed.FirstOrDefault(p => p.Viewport.Id == vp) : null;
        pl ??= placed.FirstOrDefault(p => p.Drawing.Kind == "plan");
        if (pl == null) return n;
        // Paper mm, y down: the north of the view's middle as the sheet lays it.
        var d = pl.ToPaper(pl.Middle + n) - pl.ToPaper(pl.Middle);
        var v = new Vec2(d.X, -d.Y);
        return v.Length < 1e-12 ? n : v * (1 / v.Length);
    }

    /// <summary>The symbol an arrow is drawn with: its own, else its view's, else the project's (Marker symbols); null: Lapin's own arrow.</summary>
    static string? ArrowSymbol(IModelView m, Entity e, Entity view)
    {
        foreach (var owner in new[] { e, view })
            if ((string?)owner.Attrs[SymbolSlots.Key]?["northArrow"] is { Length: > 0 } id && Symbols.DefOf(m, id) != null) return id;
        return SymbolSlots.SymbolFor(m, "northArrow");
    }

    static ViewMark? NorthArrowMark(ViewContext c, Entity e)
    {
        if (AtOf(c, e) is not { } at) return null;
        var mm = c.Mm;
        var size = SizeOf(e);
        var n = NorthOf(c, e);
        var east = new Vec2(n.Y, -n.X);
        var r = size / 2 * mm;
        const string ink = "#000000";
        var layer = SitePlans.Of(c.View) != null ? "SITE" : "NORTHARROW";
        var mk = new ViewMark(e.Id);
        // A symbol of the user's: where Lapin's arrow is, its +y to true north, as large against the default as the arrow.
        if (ArrowSymbol(c.Model, e, c.View) is { } symbol) SymbolSlots.Draw(c, mk, symbol, at, n, false, size / ArrowSize, layer, ink, _ => (false, null));
        else
        {
            var text = Standards.ResolveText(c.Book, []);
            mk.Polyline(ViewMark.Circle(at, r, 48), true, layer, Pens.Default.Seen, ink);
            var tip = at + n * (r * 1.25);
            var tail = at - n * (r * 0.8);
            Vec2 left = at - n * (r * 0.45) - east * (r * 0.45), right = at - n * (r * 0.45) + east * (r * 0.45);
            mk.Polyline([tip, right, tail, left], true, layer, Pens.Default.Seen, ink);
            // The east half filled, as the usual arrow.
            mk.Fills.Add(([[tip, right, tail]], ink));
            mk.Text(new PenText(tip + n * (2.5 * mm), "N", text.Height * 1.2 * size / ArrowSize, 0, "middle", "bottom", true, text.Font), ink);
        }
        // What a click takes: the arrow's square, with its N.
        var k = r * 1.6;
        IReadOnlyList<Vec2> hit = [at + new Vec2(-k, -k), at + new Vec2(k, -k), at + new Vec2(k, k + 3 * mm), at + new Vec2(-k, k + 3 * mm)];
        mk.Hit = ([hit], true);
        mk.Snaps.Add(at);
        mk.Data = KeyData(e, at);
        mk.Data["size"] = size;
        mk.Data["north"] = new JsonArray(Math.Round(n.X, 6), Math.Round(n.Y, 6));
        return mk;
    }

    // ------------------------------------------------------------ commands

    static Entity ViewOf(ICommandContext ctx, JsonObject a) =>
        Views.Find(ctx.Model, (string?)a["viewId"] ?? "") is { } v && v.Str("kind") != "3d" ? v : throw new CommandException("Which view? A plan, a section, an elevation or a sheet's paper.");

    static JsonArray At(JsonNode? n) => Args.TryPoint(n) is { } p ? new JsonArray(p.X, p.Y) : throw new CommandException("Where? [x, y].");

    static void Apply(JsonObject attrs, JsonObject a)
    {
        if ((string?)a["kind"] is { } k) attrs["kind"] = LegendKinds.Contains(k) ? k : throw new CommandException($"„{k}” is not a legend's kind ({string.Join(", ", LegendKinds)}).");
        if (a.ContainsKey("title")) { if ((string?)a["title"] is { Length: > 0 } t) attrs["title"] = t.Trim(); else attrs.Remove("title"); }
        // The language its own words are written in (DrawingWords); null: English.
        if (a.ContainsKey("lang"))
        {
            if ((string?)a["lang"] is { Length: > 0 } l) attrs["lang"] = DrawingWords.Known(l) ?? throw new CommandException($"„{l}” is not a language a legend is written in ({string.Join(", ", DrawingWords.Languages)}).");
            else attrs.Remove("lang");
        }
        foreach (var key in new[] { "length", "scale" })
            if (a.ContainsKey(key))
            {
                if (Json.Number(a[key]) is { } v && v > 0 && double.IsFinite(v) && v < 1e6) attrs[key] = v;
                else if (a[key] == null) attrs.Remove(key);
                else throw new CommandException($"The {key} is a positive number.");
            }
        if (a.ContainsKey("divisions"))
        {
            if (Json.Number(a["divisions"]) is { } d && d >= 1 && d <= 20) attrs["divisions"] = (int)d;
            else if (a["divisions"] == null) attrs.Remove("divisions");
            else throw new CommandException("A scale bar has 1 to 20 divisions.");
        }
        if (a.ContainsKey("viewportId")) { if ((string?)a["viewportId"] is { Length: > 0 } vp) attrs["viewportId"] = vp; else attrs.Remove("viewportId"); }
        // A north arrow's size (its circle's diameter on paper, mm; null: 14); a bar's look (site: a site plan's; null: Lapin's).
        if (a.ContainsKey("size"))
        {
            if (Json.Number(a["size"]) is { } z && z >= 3 && z <= 200) attrs["size"] = z;
            else if (a["size"] == null) attrs.Remove("size");
            else throw new CommandException("A north arrow is 3 to 200 mm across on paper.");
        }
        if (a.ContainsKey("look"))
        {
            if ((string?)a["look"] is { Length: > 0 } lk) attrs["look"] = Looks.Contains(lk) ? lk : throw new CommandException($"„{lk}” is not a scale bar's look (site, or none: Lapin's).");
            else attrs.Remove("look");
        }
    }

    /// <summary>
    /// A view's north arrow and scale bar as a site plan has them (decision 722), standing by themselves until moved: the
    /// annotations to add for the keys asked (an arrow, a bar in the site's look).
    /// </summary>
    public static IEnumerable<Entity> SiteKeys(Func<string> newId, string viewId, bool arrow = true, bool bar = true)
    {
        if (arrow) yield return new Entity(newId(), IfcClass.Annotation, "North arrow", new JsonObject { ["viewId"] = viewId }, NorthArrow);
        if (bar) yield return new Entity(newId(), IfcClass.Annotation, "Scale bar", new JsonObject { ["viewId"] = viewId, ["look"] = "site" }, ScaleBar);
    }

    /// <summary>The view's own keys of a kind (north arrows or scale bars).</summary>
    public static IEnumerable<Entity> On(IModelView m, string viewId, string predefined) =>
        m.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == predefined && x.Str("viewId") == viewId);

    public static void Register(IModuleRegistry r)
    {
        // A legend on a sheet's paper: {viewId: paper:<sheet>, at (its top-left corner), kind: materials | lines, title, lang (its own words' language)}.
        r.Command("sheet.addLegend", "Legend", (ctx, a) =>
        {
            var view = ViewOf(ctx, a);
            if (!PaperViews.Is(view)) throw new CommandException("A legend goes on a sheet: open the sheet and annotate it.");
            var attrs = new JsonObject { ["viewId"] = view.Id, ["at"] = At(a["at"]), ["kind"] = "materials" };
            Apply(attrs, a);
            var e = new Entity(ctx.NewId(), IfcClass.Annotation, (string?)attrs["kind"] == "lines" ? "Line legend" : "Material legend", attrs, Legend);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        // A scale bar: {viewId, at (its left end), length (m), divisions, scale, viewportId (on paper: at that view's scale), look (site)}.
        r.Command("sheet.addScaleBar", "Scale bar", (ctx, a) =>
        {
            var view = ViewOf(ctx, a);
            var attrs = new JsonObject { ["viewId"] = view.Id };
            // On a view it may stand by itself (none: under the drawing's bottom left, where a site plan draws its own).
            if (a["at"] != null || PaperViews.Is(view)) attrs["at"] = At(a["at"]);
            Apply(attrs, a);
            var e = new Entity(ctx.NewId(), IfcClass.Annotation, "Scale bar", attrs, ScaleBar);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        // A north arrow (issue #111): {viewId (a plan or a sheet's paper), at? (its middle; none on a view: right of the drawing's
        // top, where a site plan draws it), size? (paper mm), viewportId? (on paper: north as that view is laid out)}.
        r.Command("sheet.addNorthArrow", "North arrow", (ctx, a) =>
        {
            var view = ViewOf(ctx, a);
            if (!PaperViews.Is(view) && (view.Str("kind") ?? "plan") != "plan") throw new CommandException("A north arrow goes on a plan or a sheet.");
            var attrs = new JsonObject { ["viewId"] = view.Id };
            if (a["at"] != null) attrs["at"] = At(a["at"]);
            else if (PaperViews.Is(view)) throw new CommandException("Where? [x, y].");
            Apply(attrs, a);
            var e = new Entity(ctx.NewId(), IfcClass.Annotation, "North arrow", attrs, NorthArrow);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        // A legend's, a scale bar's or a north arrow's settings: {id | ids, at (null on a view: back where it stands by itself), kind, title,
        // length, divisions, scale, viewportId, size, look}; null clears one.
        r.Command("sheet.updateKey", "Change legend", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) throw new CommandException("Which legend, scale bar or north arrow?");
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) is { Class: IfcClass.Annotation, PredefinedType: Legend or ScaleBar or NorthArrow } x ? x : throw new CommandException("Not a legend, a scale bar or a north arrow.");
                var attrs = e.CloneAttrs();
                if (a["at"] != null) attrs["at"] = At(a["at"]);
                else if (a.ContainsKey("at"))
                {
                    if (e.PredefinedType == Legend || Views.Find(ctx.Model, e.Str("viewId") ?? "") is not { } kv || PaperViews.Is(kv)) throw new CommandException("On a sheet it stays where it is placed: give it a point.");
                    attrs.Remove("at");
                }
                Apply(attrs, a);
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });
    }
}
