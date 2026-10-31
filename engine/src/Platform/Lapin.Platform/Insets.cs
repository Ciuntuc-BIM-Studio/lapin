using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A view drawn inside another (docs/plan-rebar.md, decision 211): a <c>LapinInset</c> on its host view
/// (<c>viewId</c>) shows the <c>source</c> view rendered, centred at <c>at</c>, by a <c>factor</c> (host metres per
/// source metre; by default the one that keeps the source's paper size: host scale / source scale), in a thin frame
/// with the source's name and scale under it. A cross-section of a beam laid beside its longitudinal section, with
/// the context round it, in one viewport. An inset never draws the insets of its source.
/// </summary>
public static class Insets
{
    public const string Class = "LapinInset";
    public const string Layer = "INSET";

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Inset view", [new("factor", "Factor", "number", Min: 0.001), new("frame", "Frame", "bool")], "#555555", Kind: "annotation"));

        // A view inside another: {viewId (the host), source (the view shown), at: [x, y] (host coordinates), factor?, frame?}.
        r.Command("view.inset", "Inset view", (ctx, a) =>
        {
            var host = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("An inset is drawn on a view.");
            var source = Views.Find(ctx.Model, (string?)a["source"] ?? "") ?? throw new CommandException("An inset shows a view.");
            var e = Create(ctx, host.Id, source.Id, Args.Point(a["at"], "where the inset goes"), Json.Number(a["factor"]), !(a["frame"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && !f));
            return new JsonObject { ["id"] = e.Id };
        });
        r.Command("view.updateInset", "Inset view", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Not an inset.");
            var attrs = e.CloneAttrs();
            if (a.ContainsKey("at")) attrs["at"] = J(Args.Point(a["at"], "where the inset goes"));
            if (a.ContainsKey("factor")) { if (Json.Number(a["factor"]) is { } k && k > 0) attrs["factor"] = k; else attrs.Remove("factor"); }
            if (a.ContainsKey("frame")) { if (a["frame"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && !f) attrs["frame"] = false; else attrs.Remove("frame"); }
            if ((string?)a["source"] is { Length: > 0 } src)
            {
                var source = Views.Find(ctx.Model, src) ?? throw new CommandException("There is no such view.");
                if (source.Id == e.Str("viewId")) throw new CommandException("A view cannot show itself.");
                attrs["source"] = source.Id;
            }
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });
    }

    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));

    public static Entity Create(ICommandContext ctx, string hostId, string sourceId, Vec2 at, double? factor = null, bool frame = true)
    {
        if (hostId == sourceId) throw new CommandException("A view cannot show itself.");
        var source = ctx.Model.Get(sourceId);
        var attrs = new JsonObject { ["viewId"] = hostId, ["source"] = sourceId, ["at"] = J(at) };
        if (factor is { } k && k > 0) attrs["factor"] = k;
        if (!frame) attrs["frame"] = false;
        var e = new Entity(ctx.NewId(), Class, source?.Name ?? "Inset", attrs);
        ctx.Add(e);
        return e;
    }

    /// <summary>The host view's insets, each its source rendered and laid at its point.</summary>
    public static IEnumerable<ViewMark> Marks(IQueryContext q, ViewContext c)
    {
        foreach (var e in c.Model.OfClass(Class).Where(x => x.Str("viewId") == c.View.Id))
        {
            if (Views.Find(c.Model, e.Str("source") ?? "") is not { } source || source.Id == c.View.Id) continue;
            var r = Views.Render(q, source, print: false, insets: false);
            var mk = new ViewMark(e.Id);
            var at = P(e.Attrs["at"]);
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
            var factor = e.NumOrNull("factor") ?? c.Scale / r.Scale;
            var centre = (b.Min + b.Max) * 0.5;
            Vec2 Map(Vec2 p) => at + (p - centre) * factor;
            foreach (var pen in r.Pens)
            {
                if (pen.Layer is "PICK" or Views.ConstructionLayer) continue;
                var color = pen.Stroke ?? "#000000";
                foreach (var (p0, p1) in pen.Segments) mk.Line(Map(p0), Map(p1), pen.Layer, pen.Width, color, pen.Dash);
                if (pen.Loops.Count > 0)
                {
                    var loops = pen.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(Map).ToList()).ToList();
                    if (pen.Fill != null) mk.Fills.Add((loops, pen.Fill));
                    if (pen.Stroke != null && pen.Width > 0) foreach (var l in loops) mk.Polyline(l, true, pen.Layer, pen.Width, color, pen.Dash);
                }
                foreach (var t in pen.Texts) mk.Text(t with { At = Map(t.At), Height = t.Height }, color);
                // The cut lines' ends snap, so bars are drawn on the concrete's outline.
                if (pen.Width >= Pens.Default.Cut - 1e-9) foreach (var (p0, p1) in pen.Segments) { mk.Snaps.Add(Map(p0)); mk.Snaps.Add(Map(p1)); }
            }
            var pad = 3 * c.Mm;
            var (lo, hi) = (Map(b.Min) - new Vec2(pad, pad), Map(b.Max) + new Vec2(pad, pad));
            if (!(e.Attrs["frame"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && !f))
            {
                mk.Polyline([lo, new Vec2(hi.X, lo.Y), hi, new Vec2(lo.X, hi.Y)], true, Layer, 0.18, "#555555");
                var scale = r.Scale / factor * (c.Scale / r.Scale);
                mk.Text(new PenText(new Vec2((lo.X + hi.X) / 2, lo.Y - 1.5 * c.Mm), $"{source.Name}  1:{Math.Round(scale).ToString(CultureInfo.InvariantCulture)}", 2.5, 0, "middle", "top"), "#555555");
            }
            else
            {
                // Without a frame, the inset is still picked by its outline.
                foreach (var (s0, s1) in new[] { (lo, new Vec2(hi.X, lo.Y)), (new Vec2(hi.X, lo.Y), hi), (hi, new Vec2(lo.X, hi.Y)), (new Vec2(lo.X, hi.Y), lo) })
                    mk.Lines.Add((s0, s1, "PICK", 0, "none", null));
            }
            mk.Snaps.Add(at);
            mk.Data = new JsonObject { ["at"] = J(at), ["source"] = source.Id, ["bounds"] = new JsonArray(Math.Round(lo.X, 5), Math.Round(lo.Y, 5), Math.Round(hi.X, 5), Math.Round(hi.Y, 5)) };
            yield return mk;
        }
    }
}
