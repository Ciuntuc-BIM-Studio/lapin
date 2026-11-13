using System.Text.Json.Nodes;
using Lapin.Drawing.Documents;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform.Documents;

/// <summary>
/// A document's pictures (docs/plan-documents.md, Imaginile): a view drawn by the engine as a sheet prints it (its pens, its
/// annotations, no construction lines), at the block's scale or fitted to its width, never wider than the page's text; or a whole
/// sheet, scaled down to the text's width. The pens are in paper millimetres, y down, the drawing's top left at 0,0.
/// </summary>
public static class DocumentImages
{
    /// <summary>The paper left round a view's drawing (mm).</summary>
    const double Margin = 1;

    /// <summary>The scales a drawing too wide for the page steps down to (1 : n).</summary>
    static readonly double[] Scales = [1, 2, 5, 10, 20, 25, 50, 75, 100, 125, 150, 200, 250, 500, 750, 1000, 1250, 2000, 2500, 5000, 10000, 20000, 50000];

    /// <summary>
    /// The picture of <paramref name="viewId"/> (a view or a sheet): at <paramref name="scale"/> when given, else fitted to
    /// <paramref name="width"/> mm when given, else at the view's own scale; then, when wider than <paramref name="maxWidth"/>, at the
    /// next standard scale that fits (a width given is fitted exactly). With <paramref name="boundaryId"/>, only that boundary of the
    /// view, cropped as a sheet's viewport bound to it is. Null when there is no such view or it draws nothing.
    /// </summary>
    public static DocImage? Draw(IQueryContext q, string viewId, double? scale, double? width, double maxWidth, string key, string? caption, string? boundaryId = null)
    {
        var m = q.Model;
        var target = Math.Max(10, Math.Min(width is > 0 ? width.Value : maxWidth, maxWidth));
        if (m.Get(viewId) is { Class: Sheets.Class } sheet)
        {
            var page = Sheets.Compose(q, sheet);
            var s = Math.Min(1, target / page.W);
            return new DocImage(key, page.W * s, page.H * s, Transform(page.Pens, p => p * s, s), caption);
        }
        if (Views.Find(m, viewId) is not { } view) return null;
        var own = view.NumOrNull("scale") is { } vs && vs > 0 ? vs : 100;
        var sc = scale is > 0 ? scale.Value : own;
        if (boundaryId is { Length: > 0 }) return Cropped(q, view, Boundary(m, view.Id, boundaryId), scale, width, sc, own, target, maxWidth, key, caption);
        // A turned plan (decision 526) pictured as it shows: its pens turned, its texts readable.
        var turn = Views.Turn(m, view);
        (List<Pen> Pens, (Vec2 Min, Vec2 Max)? Bounds) Shown(Views.Rendered rr)
        {
            if (turn == 0) return (rr.Pens, rr.Bounds);
            var turned = rr.Pens.Select(p => Turning.Turned(p, turn, rr.Scale / 1000)).ToList();
            return (turned, Turning.Bounds(turned));
        }
        var (pensShown, boundsShown) = Shown(Render(q, view, sc, own));
        if (boundsShown is not { } b) return null;
        var extent = Math.Max(b.Max.X - b.Min.X, 1e-6);
        double Wide(double at) => extent * 1000 / at + 2 * Margin;
        double? refit = null;
        if (scale is not > 0 && width is > 0) refit = extent * 1000 / Math.Max(target - 2 * Margin, 1);
        else if (Wide(sc) > maxWidth + 1e-6) refit = Scales.FirstOrDefault(x => x >= sc && Wide(x) <= maxWidth + 1e-6, extent * 1000 / Math.Max(maxWidth - 2 * Margin, 1));
        if (refit is { } rs && Math.Abs(rs - sc) > 1e-9)
        {
            sc = rs;
            (pensShown, boundsShown) = Shown(Render(q, view, sc, own));
            if (boundsShown is not { } rb) return null;
            b = rb;
        }
        var k = 1000 / sc;
        var (minX, maxY) = (b.Min.X, b.Max.Y);
        var pens = Transform(pensShown,p => new Vec2((p.X - minX) * k + Margin, (maxY - p.Y) * k + Margin), 1);
        return new DocImage(key, (b.Max.X - b.Min.X) * k + 2 * Margin, (b.Max.Y - b.Min.Y) * k + 2 * Margin, pens, caption);
    }

    /// <summary>A named boundary of the view (decision 236); refused when it is another view's or not a boundary.</summary>
    public static Entity Boundary(IModelView m, string viewId, string boundaryId) =>
        m.Get(boundaryId) is { Class: ViewBoundaries.Class } b && b.Str("viewId") == viewId ? b : throw new CommandException("The view has no such boundary.");

    /// <summary>The image blocks' boundaries checked against their views, nested blocks too (a document's blocks as given).</summary>
    public static void CheckBoundaries(IModelView m, JsonArray blocks)
    {
        foreach (var b in blocks.OfType<JsonObject>())
        {
            if ((string?)b["kind"] == "image" && (string?)b["boundary"] is { Length: > 0 } bid && (string?)b["view"] is { } view && !view.Contains("{{"))
                Boundary(m, view, bid);
            foreach (var inner in new[] { b["blocks"], b["else"] }.OfType<JsonArray>()) CheckBoundaries(m, inner);
        }
    }

    /// <summary>
    /// The view cropped to one of its boundaries as a sheet's viewport bound to it shows it: the boundary's box at the scale, its
    /// middle the picture's, the pens clipped to it (<see cref="Sheets.ClipToBox"/>), no margin. The scale steps down as a whole view's does.
    /// </summary>
    static DocImage? Cropped(IQueryContext q, Entity view, Entity boundary, double? scale, double? width, double sc, double own, double target, double maxWidth, string key, string? caption)
    {
        var (min, max) = ViewBoundaries.Box(boundary);
        var extent = max.X - min.X;
        if (scale is not > 0 && width is > 0) sc = extent * 1000 / target;
        else if (extent * 1000 / sc > maxWidth + 1e-6) sc = Scales.FirstOrDefault(x => x >= sc && extent * 1000 / x <= maxWidth + 1e-6, extent * 1000 / maxWidth);
        var r = Render(q, view, sc, own);
        var k = 1000 / sc;
        var (w, h) = (extent * k, (max.Y - min.Y) * k);
        Vec2 ToPaper(Vec2 p) => new((p.X - min.X) * k, (max.Y - p.Y) * k);
        var pens = new List<Pen>();
        foreach (var pen in r.Pens)
            if (pen.Layer != "PICK" && Sheets.ClipToBox(pen, ToPaper, 0, 0, w, h) is { } c) pens.Add(c);
        return new DocImage(key, w, h, pens, caption);
    }

    /// <summary>A view drawn for print at a scale: its own entity given the scale, so its pens, texts and hatches are sized for it.</summary>
    static Views.Rendered Render(IQueryContext q, Entity view, double scale, double own)
    {
        if (Math.Abs(scale - own) > 1e-9)
        {
            var attrs = view.CloneAttrs();
            attrs["scale"] = scale;
            view = view.With(attrs: attrs);
        }
        return Views.Render(q, view, print: true);
    }

    /// <summary>Pens copied through a map of their points; <paramref name="s"/> scales their widths, dashes and text heights (a sheet made smaller).</summary>
    static List<Pen> Transform(IEnumerable<Pen> pens, Func<Vec2, Vec2> map, double s)
    {
        var o = new List<Pen>();
        foreach (var pen in pens)
        {
            if (pen.Layer == "PICK") continue;
            var c = new Pen(pen.Layer, pen.Stroke, pen.Width * s, pen.Fill, pen.Dash?.Select(d => d * s).ToArray());
            foreach (var (a, b) in pen.Segments) c.Segments.Add((map(a), map(b)));
            foreach (var loop in pen.Loops) c.Loops.Add(loop.Select(map).ToList());
            foreach (var t in pen.Texts) c.Texts.Add(t with { At = map(t.At), Height = t.Height * s });
            foreach (var img in pen.Images) c.Images.Add(img with { Corners = [.. img.Corners.Select(map)] });
            if (pen.Clip is { } clip)
            {
                var (p0, p1) = (map(new Vec2(clip.X0, clip.Y0)), map(new Vec2(clip.X1, clip.Y1)));
                c.Clip = (Math.Min(p0.X, p1.X), Math.Min(p0.Y, p1.Y), Math.Max(p0.X, p1.X), Math.Max(p0.Y, p1.Y));
            }
            if (c.Segments.Count + c.Loops.Count + c.Texts.Count + c.Images.Count > 0) o.Add(c);
        }
        return o;
    }
}
