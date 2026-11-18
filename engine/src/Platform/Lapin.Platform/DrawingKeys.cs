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
/// on any view (at the view's). Both are IfcAnnotation of the view they are on (<c>SHEETLEGEND</c> {viewId, at, kind:
/// materials | lines, title}; <c>SCALEBAR</c> {viewId, at, length (m, none: a round length about 80 mm long on paper),
/// divisions, scale, viewportId}); <c>at</c> is the legend's top-left corner, the bar's left end, in the view's coordinates.
/// </summary>
public static class DrawingKeys
{
    public const string Legend = "SHEETLEGEND";
    public const string ScaleBar = "SCALEBAR";
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
                    : (bands.TryGetValue(cut.Element, out var b) ? b : bands[cut.Element] = LayerSets.BandsOf(m, cut.Element))?.Select(x => x.Material) ?? [];
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

    static ViewMark ScaleBarMark(ViewContext c, Entity e)
    {
        var at = Args.PointOf(e.Attrs["at"]);
        var scale = ScaleOf(c, e);
        var mm = c.Mm;
        // Drawing units for a metre of the model: one on a view; on the paper (m of paper), what a metre is at the bar's scale.
        var perMetre = PaperViews.Is(c.View) ? 1 / scale : 1;
        var length = e.NumOrNull("length") is double l && l > 0 ? l : NiceLength(scale);
        var n = Math.Clamp((int)(e.NumOrNull("divisions") ?? DivisionsOf(length)), 1, 20);
        var h = 2 * mm;
        var text = Standards.ResolveText(c.Book, []);
        var mk = new ViewMark(e.Id);
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
        mk.Data = new JsonObject { ["scale"] = scale, ["length"] = length, ["divisions"] = n };
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
    }

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

        // A scale bar: {viewId, at (its left end), length (m), divisions, scale, viewportId (on paper: at that view's scale)}.
        r.Command("sheet.addScaleBar", "Scale bar", (ctx, a) =>
        {
            var view = ViewOf(ctx, a);
            var attrs = new JsonObject { ["viewId"] = view.Id, ["at"] = At(a["at"]) };
            Apply(attrs, a);
            var e = new Entity(ctx.NewId(), IfcClass.Annotation, "Scale bar", attrs, ScaleBar);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        // A legend's or a scale bar's settings: {id | ids, kind, title, length, divisions, scale, viewportId}; null clears one.
        r.Command("sheet.updateKey", "Change legend", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) throw new CommandException("Which legend or scale bar?");
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) is { Class: IfcClass.Annotation, PredefinedType: Legend or ScaleBar } x ? x : throw new CommandException("Not a legend or a scale bar.");
                var attrs = e.CloneAttrs();
                if (a["at"] != null) attrs["at"] = At(a["at"]);
                Apply(attrs, a);
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });
    }
}
