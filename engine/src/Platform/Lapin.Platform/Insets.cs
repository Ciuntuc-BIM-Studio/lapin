using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A view drawn inside another (docs/plan-rebar.md, decision 211; any 2D view in any other, decision 574): a <c>LapinInset</c>
/// on its host view (<c>viewId</c>: a plan, a section, an elevation, a reference plane's view) shows the <c>source</c> view
/// rendered, its point <c>at</c> on the source's middle (the element's section, for a section across one), at a size of its
/// own: by <c>factor</c> (host metres per source metre) when one is kept, else at its own <c>scale</c> (1:N on paper), else
/// at the source's scale (host scale / source scale, the same size on paper as the source's own sheet). A <c>crop</c>
/// ({min, max}, in the source's coordinates) cuts it to a rectangle of its own, over the source's own crop region; its thin
/// frame (<c>frame</c>, on by default) carries the source's name and scale. The source's own annotations (its bars,
/// dimensions, drafted lines, what picks its elements) come through as marks of their own owners, mapped into the host,
/// each telling the interface the mapping it came by (<c>via</c>), so what is drawn on the inset is drawn on the source
/// view, at real size, and is edited there. The source follows the model; an inset never draws the insets of its source.
/// </summary>
public static class Insets
{
    public const string Class = "LapinInset";
    public const string Layer = "INSET";

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Inset view", [new("scale", "Scale (1:N)", "number", Min: 1), new("factor", "Factor", "number", Min: 0.001), new("frame", "Frame", "bool")], "#555555", Kind: "annotation"));

        // A view inside another: {viewId (the host), source (the view shown), at: [x, y] (host coordinates), scale? (1:N), factor?, frame?, crop?: {min, max} | true}.
        r.Command("view.inset", "Inset view", (ctx, a) =>
        {
            var host = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("An inset is drawn on a view.");
            var source = Views.Find(ctx.Model, (string?)a["source"] ?? "") ?? throw new CommandException("An inset shows a view.");
            var e = Create(ctx, host.Id, source.Id, Args.Point(a["at"], "where the inset goes"), Json.Number(a["factor"]), !(a["frame"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && !f), Json.Number(a["scale"]));
            if (a["crop"] is { } c && CropArg(ctx, source, c) is { } box)
            {
                var attrs = e.CloneAttrs();
                attrs["crop"] = CropJson(box);
                ctx.Replace(e.With(attrs: attrs));
            }
            return new JsonObject { ["id"] = e.Id };
        });
        // {id, at?, scale? (1:N; null: the source's), factor? (null: by the scale), frame?, source?, crop? ({min, max} in the source's coordinates; true: round what it shows now; null or false: none)}: one step.
        r.Command("view.updateInset", "Inset view", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Not an inset.");
            var attrs = e.CloneAttrs();
            if (a.ContainsKey("at")) attrs["at"] = J(Args.Point(a["at"], "where the inset goes"));
            if (a.ContainsKey("factor")) { if (Json.Number(a["factor"]) is { } k && k > 0) { attrs["factor"] = k; attrs.Remove("scale"); } else attrs.Remove("factor"); }
            // Its own scale stands for the factor: the inset keeps it on paper whatever the host's scale.
            if (a.ContainsKey("scale")) { if (Json.Number(a["scale"]) is { } n && n >= 1) { attrs["scale"] = n; attrs.Remove("factor"); } else attrs.Remove("scale"); }
            if (a.ContainsKey("frame")) { if (a["frame"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && !f) attrs["frame"] = false; else attrs.Remove("frame"); }
            if ((string?)a["source"] is { Length: > 0 } src)
            {
                var source = Views.Find(ctx.Model, src) ?? throw new CommandException("There is no such view.");
                if (source.Id == e.Str("viewId")) throw new CommandException("A view cannot show itself.");
                if (source.Str("kind") == "3d") throw new CommandException("An inset shows a plan, a section or an elevation, not a 3D view.");
                // Another view: the crop of the one before means nothing to it.
                if (source.Id != e.Str("source")) attrs.Remove("crop");
                attrs["source"] = source.Id;
            }
            if (a.ContainsKey("crop"))
            {
                var source = Views.Find(ctx.Model, attrs["source"]?.GetValue<string>() ?? "") ?? throw new CommandException("The inset's view is gone.");
                if (CropArg(ctx, source, a["crop"]) is { } box) attrs["crop"] = CropJson(box); else attrs.Remove("crop");
            }
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });
    }

    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));
    static JsonObject CropJson((Vec2 Min, Vec2 Max) b) => new() { ["min"] = J(b.Min), ["max"] = J(b.Max) };

    /// <summary>An inset's own crop, in its source's coordinates (null: none).</summary>
    public static (Vec2 Min, Vec2 Max)? CropOf(Entity inset)
    {
        if (inset.Attrs["crop"] is not JsonObject o || Args.TryPoint(o["min"]) is not { } a || Args.TryPoint(o["max"]) is not { } b) return null;
        return (new Vec2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Vec2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
    }

    /// <summary>A crop as asked: {min, max}; true: round what the source shows now; null or false: none.</summary>
    static (Vec2 Min, Vec2 Max)? CropArg(ICommandContext ctx, Entity source, JsonNode? n)
    {
        if (n is JsonObject o)
        {
            var (a, b) = (Args.Point(o["min"], "the crop's corner"), Args.Point(o["max"], "the crop's corner"));
            var box = (new Vec2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Vec2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
            if (box.Item2.X - box.Item1.X < 0.01 || box.Item2.Y - box.Item1.Y < 0.01) throw new CommandException("An inset's crop is 1 cm across at least.");
            return box;
        }
        if (n is JsonValue v && v.TryGetValue<bool>(out var on) && on)
        {
            var r = Views.Render(ctx.Query, source, print: true, insets: false, withMarks: false);
            return Extent(r) ?? throw new CommandException(r.Problem ?? "The view draws nothing yet: give the crop's corners.");
        }
        return null;
    }

    /// <summary>What a rendered source shows: its drawing's bounds and its annotations' points (null when nothing).</summary>
    static (Vec2 Min, Vec2 Max)? Extent(Views.Rendered r)
    {
        if (r.Bounds is not { } b) return null;
        var pts = new List<Vec2> { b.Min, b.Max };
        foreach (var sm in r.Marks) { foreach (var l in sm.Lines) { pts.Add(l.A); pts.Add(l.B); } foreach (var (t, _) in sm.Texts) pts.Add(t.At); }
        return (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y)));
    }

    /// <summary>Host metres per source metre: the inset's factor, else its own scale's (host scale / its scale), else the source's.</summary>
    public static double FactorOf(Entity inset, double hostScale, double sourceScale) =>
        inset.NumOrNull("factor") is { } k && k > 0 ? k
        : inset.NumOrNull("scale") is { } n && n >= 1 ? hostScale / n
        : hostScale / sourceScale;

    public static Entity Create(ICommandContext ctx, string hostId, string sourceId, Vec2 at, double? factor = null, bool frame = true, double? scale = null)
    {
        if (hostId == sourceId) throw new CommandException("A view cannot show itself.");
        if (Views.Find(ctx.Model, hostId)?.Str("kind") == "3d") throw new CommandException("An inset is drawn on a plan, a section or an elevation, not in 3D.");
        if (Views.Find(ctx.Model, sourceId)?.Str("kind") == "3d") throw new CommandException("An inset shows a plan, a section or an elevation, not a 3D view.");
        var source = Views.Find(ctx.Model, sourceId);
        var attrs = new JsonObject { ["viewId"] = hostId, ["source"] = sourceId, ["at"] = J(at) };
        if (factor is { } k && k > 0) attrs["factor"] = k;
        else if (scale is { } n && n >= 1) attrs["scale"] = n;
        if (!frame) attrs["frame"] = false;
        var e = new Entity(ctx.NewId(), Class, source?.Name ?? "Inset", attrs);
        ctx.Add(e);
        return e;
    }

    /// <summary>
    /// The host view's insets: each its source's drawing laid at its point (cut to its crop), and the source's annotations as
    /// marks of their own owners, mapped the same way. <paramref name="print"/>: drawn for a sheet, PDF or DXF, so the source
    /// leaves out what it shows on the screen only (its reference planes, its boundaries), as it does on its own sheet.
    /// </summary>
    public static IEnumerable<ViewMark> Marks(IQueryContext q, ViewContext c, bool print = false)
    {
        foreach (var e in c.Model.OfClass(Class).Where(x => x.Str("viewId") == c.View.Id))
        {
            if (Views.Find(c.Model, e.Str("source") ?? "") is not { } source || source.Id == c.View.Id || source.Str("kind") == "3d") continue;
            var r = Views.Render(q, source, print: print, insets: false, withMarks: false);
            var mk = new ViewMark(e.Id);
            var at = Args.PointOf(e.Attrs["at"]);
            if (r.Problem != null || r.Bounds is not { } b)
            {
                // Nothing to show: a small frame with the reason, so the inset can still be found and moved.
                var k0 = 10 * c.Mm;
                mk.Polyline([at + new Vec2(-k0, -k0 / 2), at + new Vec2(k0, -k0 / 2), at + new Vec2(k0, k0 / 2), at + new Vec2(-k0, k0 / 2)], true, Layer, 0.18, "#8f98a3", [2, 1.5]);
                mk.Text(new PenText(at, r.Problem ?? source.Name, 2.0, 0, "middle", "middle"), "#8f98a3");
                mk.Data = new JsonObject { ["at"] = J(at) };
                yield return mk;
                continue;
            }
            var factor = FactorOf(e, c.Scale, r.Scale);
            // The point stands at the element's section when the source is a section across one (the middle of its window), else at the drawing's middle.
            var centre = r.Window is { } w ? (w.Min + w.Max) * 0.5 : (b.Min + b.Max) * 0.5;
            Vec2 Map(Vec2 p) => at + (p - centre) * factor;
            // The inset's own crop, in the source's coordinates, over the source's own crop region (Views.Render has cut to that).
            var cropBox = CropOf(e);
            var clip = cropBox is { } cb ? new ViewCrops.Active(new ViewCrops.Crop(true, false, cb.Min, cb.Max, false, [0, 0, 0, 0]), 0, cb, cb) : null;
            var via = new JsonObject { ["inset"] = e.Id, ["host"] = c.View.Id, ["source"] = source.Id, ["at"] = J(at), ["centre"] = J(centre), ["factor"] = factor };
            // The source's drawing: its pens as the inset's own lines, fills and texts.
            foreach (var pen0 in r.Pens)
            {
                if (pen0.Layer is "PICK" or Views.ConstructionLayer) continue;
                if ((clip == null ? pen0 : clip.Clip(pen0, clip.Model)) is not { } pen) continue;
                var color = pen.Stroke ?? "#000000";
                foreach (var (p0, p1) in pen.Segments) mk.Line(Map(p0), Map(p1), pen.Layer, pen.Width, color, pen.Dash);
                if (pen.Loops.Count > 0)
                {
                    var loops = pen.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(Map).ToList()).ToList();
                    if (pen.Fill != null) mk.Fills.Add((loops, pen.Fill));
                    if (pen.Stroke != null && pen.Width > 0) foreach (var l in loops) mk.Polyline(l, true, pen.Layer, pen.Width, color, pen.Dash);
                }
                foreach (var t in pen.Texts) mk.Text(t with { At = Map(t.At) }, color);
                // The cut lines' ends snap, so bars are drawn on the concrete's outline.
                if (pen.Width >= Pens.Default.Cut - 1e-9) foreach (var (p0, p1) in pen.Segments) { mk.Snaps.Add(Map(p0)); mk.Snaps.Add(Map(p1)); }
            }
            // The source's annotations left by the crop (each cut at its edge).
            var marks = clip == null ? r.Marks : r.Marks.Where(sm => clip.Clip(sm, clip.Model)).ToList();
            // The window's extent (the drawing's, with the source's annotations; cropped: the crop itself) and its frame.
            var (lo, hi) = cropBox is { } box ? (Map(box.Min), Map(box.Max))
                : Extent(r with { Marks = marks }) is { } ext ? (Map(ext.Min) - new Vec2(3 * c.Mm, 3 * c.Mm), Map(ext.Max) + new Vec2(3 * c.Mm, 3 * c.Mm))
                : (Map(b.Min), Map(b.Max));
            var scale = c.Scale / factor;
            if (!(e.Attrs["frame"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && !f))
            {
                mk.Polyline([lo, new Vec2(hi.X, lo.Y), hi, new Vec2(lo.X, hi.Y)], true, Layer, 0.18, "#555555");
                var scaleText = $"1:{Math.Round(scale).ToString(CultureInfo.InvariantCulture)}";
                // A symbol of the user's for its head (the inset's own, else the project's), on the frame's lower-left corner.
                if (SymbolSlots.SymbolFor(c.Model, "calloutHead", e) is { } headSymbol)
                    SymbolSlots.Draw(c, mk, headSymbol, lo, new Vec2(0, 1), false, 1, Layer, "#555555", SymbolSlots.MarkTokens(c.Model, source, key => key == "mark.scale" ? (true, scaleText) : (false, null)));
                else
                mk.Text(new PenText(new Vec2((lo.X + hi.X) / 2, lo.Y - 1.5 * c.Mm), $"{source.Name}  {scaleText}", 2.5, 0, "middle", "top"), "#555555");
            }
            else
            {
                // Without a frame, the inset is still picked by its outline.
                foreach (var (s0, s1) in new[] { (lo, new Vec2(hi.X, lo.Y)), (new Vec2(hi.X, lo.Y), hi), (hi, new Vec2(lo.X, hi.Y)), (new Vec2(lo.X, hi.Y), lo) })
                    mk.Lines.Add((s0, s1, "PICK", 0, "none", null));
            }
            mk.Snaps.Add(at);
            mk.Data = new JsonObject
            {
                ["at"] = J(at), ["source"] = source.Id, ["centre"] = J(centre), ["factor"] = factor, ["scale"] = Math.Round(scale, 6), ["sourceScale"] = r.Scale,
                ["bounds"] = new JsonArray(Math.Round(lo.X, 5), Math.Round(lo.Y, 5), Math.Round(hi.X, 5), Math.Round(hi.Y, 5)),
            };
            if (cropBox is { } kept) mk.Data["crop"] = CropJson(kept);
            yield return mk;
            // The source's annotations, each its own owner's, mapped the same way: a bar drawn on the cross-section, a line drafted on a detail, is picked and edited here.
            foreach (var sm in marks)
            {
                if (sm.Owner == null) continue;
                var own = new ViewMark(sm.Owner) { Under = sm.Under, Pick = sm.Pick };
                foreach (var (la, lb, layer, width, color, dash) in sm.Lines) own.Lines.Add((Map(la), Map(lb), layer, width, color, dash));
                foreach (var (t, color) in sm.Texts) own.Texts.Add((t with { At = Map(t.At) }, color));
                foreach (var (loops, color) in sm.Fills) own.Fills.Add((loops.Select(l => (IReadOnlyList<Vec2>)l.Select(Map).ToList()).ToList(), color));
                foreach (var (ea, eb) in sm.Edges) own.Edges.Add((Map(ea), Map(eb)));
                if (sm.Hit is { } hit) own.Hit = (hit.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(Map).ToList()).ToList(), hit.Solid);
                own.Snaps.AddRange(sm.Snaps.Select(Map));
                var data = sm.Data?.DeepClone() as JsonObject ?? new JsonObject();
                data["via"] = via.DeepClone();
                own.Data = data;
                yield return own;
            }
        }
    }
}
