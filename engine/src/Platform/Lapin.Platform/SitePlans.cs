using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The site's drawings (docs/site-plans.md): the project browser's Site, a
/// level of 2D drawings above the storeys. A site plan (Romanian "plan de
/// situație", 1:500) and a location plan ("plan de încadrare în zonă",
/// 1:2000) are plan views with <c>site</c> set: the whole model seen from
/// above, every storey, nothing cut and nothing hidden drawn, what lies wholly
/// below ±0.00 left out; the terrain, paths and plants by their symbols. Over
/// them a north arrow, the map's coordinate grid (when the project is tied to
/// the map) and a scale bar. A site plan stands on its host storey (the
/// terrain's, else the ground floor's), where what is drawn on it goes.
/// </summary>
public static class SitePlans
{
    public const string Situation = "situation";
    public const string LocationPlan = "location";
    public static readonly string[] Purposes = [Situation, LocationPlan];

    /// <summary>What lies wholly below this (m, ±0.00) is under the ground: a site plan leaves it out.</summary>
    public const double Underground = -0.01;

    /// <summary>A site view's purpose (situation or location), or null for any other view.</summary>
    public static string? Of(Entity? view) =>
        view != null && view.Str("kind") is "plan" or null && view.Str("site") is { } s && Purposes.Contains(s) ? s : null;

    public static double DefaultScale(string purpose) => purpose == LocationPlan ? 2000 : 500;
    public static string DefaultName(string purpose) => purpose == LocationPlan ? "Location plan" : "Site plan";

    /// <summary>The storey a site plan stands on: the first terrain's, else the one nearest ±0.00 (the ground floor), else the lowest.</summary>
    public static Entity? Host(IModelView m)
    {
        foreach (var t in m.OfClass(IfcClass.GeographicElement))
            if (Spatial.StoreyOf(m, t.Id) is { Class: IfcClass.BuildingStorey } s) return s;
        return Spatial.Storeys(m).OrderBy(s => Math.Abs(s.Num("elevation"))).ThenBy(s => s.Num("elevation")).FirstOrDefault();
    }

    static bool Flag(Entity view, string key) => !(view.Attrs[key] is JsonValue v && v.TryGetValue<bool>(out var b) && !b);
    public static bool NorthArrow(Entity view) => Flag(view, "northArrow");
    public static bool ScaleBar(Entity view) => Flag(view, "scaleBar");

    /// <summary>The coordinate grid's step (m of the map): the view's own (0: none), else a tenth of the scale (100 mm on paper).</summary>
    public static double GridStep(Entity view) => view.NumOrNull("gridStep") ?? (view.NumOrNull("scale") ?? 500) / 10;

    /// <summary>A site view's attributes on create: its purpose, scale and name.</summary>
    public static (JsonObject Attrs, string Name) New(string purpose, string storeyId, double? scale, string? name)
    {
        var attrs = new JsonObject { ["kind"] = "plan", ["site"] = purpose, ["storeyId"] = storeyId, ["scale"] = scale ?? DefaultScale(purpose), ["cutHeight"] = Views.DefaultCut };
        return (attrs, name ?? DefaultName(purpose));
    }

    /// <summary>A site view's settings: {id, site?, gridStep? (0: none; null: by the scale), northArrow?, scaleBar?}.</summary>
    public static void Set(ICommandContext ctx, JsonObject a)
    {
        var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Views.Class } x && Of(x) != null ? x : throw new CommandException("Not a site plan.");
        var attrs = v.CloneAttrs();
        if ((string?)a["site"] is { } site)
            attrs["site"] = Purposes.Contains(site) ? site : throw new CommandException("A site view is a site plan (situation) or a location plan (location).");
        if (a.ContainsKey("gridStep"))
        {
            if (Json.Number(a["gridStep"]) is { } g) attrs["gridStep"] = g >= 0 && double.IsFinite(g) ? g : throw new CommandException("The grid's step is 0 (none) or more.");
            else attrs.Remove("gridStep");
        }
        foreach (var k in new[] { "northArrow", "scaleBar" })
            if (a[k] is JsonValue fv && fv.TryGetValue<bool>(out var on)) { if (on) attrs.Remove(k); else attrs[k] = false; }
        ctx.Replace(v.With(attrs: attrs));
    }

    /// <summary>A step of 1, 2 or 5 × 10ⁿ nearest <paramref name="x"/> from below.</summary>
    public static double Nice(double x)
    {
        if (!(x > 0)) return 1;
        var p = Math.Pow(10, Math.Floor(Math.Log10(x)));
        var f = x / p;
        return (f >= 5 ? 5 : f >= 2 ? 2 : 1) * p;
    }

    const string Layer = "SITE";
    const string Ink = "#000000";
    static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
    static string Grouped(double v) => Math.Round(v).ToString("#,0", CultureInfo.InvariantCulture).Replace(",", " ");

    /// <summary>A site view's north arrow, coordinate grid and scale bar, round what it draws.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan" || Of(c.View) == null) yield break;
        if (c.Drawing.Bounds is not { } box) yield break;
        var text = Standards.ResolveText(c.Book, []);
        var mm = c.Mm;
        var (lo, hi) = (box.Min - new Vec2(8 * mm, 8 * mm), box.Max + new Vec2(8 * mm, 8 * mm));

        if (SitePlans.GridStep(c.View) is var step && step > 0 && GeoRef.Georeferenced(c.Model) && Grid(c, lo, hi, step, text) is { } grid) yield return grid;

        if (NorthArrow(c.View))
        {
            // True north: the project's +Y turned by the base point's angle (as the IFC export writes it).
            var th = (Location.Base(c.Model).NorthAngle ?? 0) * Math.PI / 180;
            var n = new Vec2(-Math.Sin(th), Math.Cos(th));
            var e = new Vec2(n.Y, -n.X);
            var r = 7 * mm;
            var at = new Vec2(hi.X + 4 * mm + r, hi.Y - r - 6 * mm);
            var mk = new ViewMark(null);
            mk.Polyline(ViewMark.Circle(at, r, 48), true, Layer, Pens.Default.Seen, Ink);
            var tip = at + n * (r * 1.25);
            var tail = at - n * (r * 0.8);
            Vec2 left = at - n * (r * 0.45) - e * (r * 0.45), right = at - n * (r * 0.45) + e * (r * 0.45);
            mk.Polyline([tip, right, tail, left], true, Layer, Pens.Default.Seen, Ink);
            // The east half filled, as the usual arrow.
            mk.Fills.Add(([[tip, right, tail]], Ink));
            mk.Text(new PenText(tip + n * (2.5 * mm), "N", text.Height * 1.2, 0, "middle", "bottom", true, text.Font), Ink);
            yield return mk;
        }

        if (ScaleBar(c.View))
        {
            // About 60 mm on paper, in five parts, alternately filled.
            var length = Nice(60 * mm);
            var part = length / 5;
            var h = 1.5 * mm;
            var o = new Vec2(lo.X, lo.Y - 6 * mm);
            var mk = new ViewMark(null);
            for (var k = 0; k < 5; k++)
            {
                Vec2 a = o + new Vec2(k * part, 0), b = o + new Vec2((k + 1) * part, h);
                var rect = new[] { a, new Vec2(b.X, a.Y), b, new Vec2(a.X, b.Y) };
                mk.Polyline(rect, true, Layer, Pens.Default.Seen, Ink);
                if (k % 2 == 0) mk.Fills.Add(([rect], Ink));
            }
            foreach (var (x, label) in new[] { (0.0, "0"), (length / 2, F0(length / 2)), (length, $"{F0(length)} m") })
                mk.Text(new PenText(o + new Vec2(x, h + 1.2 * mm), label, text.Height * 0.8, 0, "middle", "bottom", false, text.Font), Ink);
            mk.Text(new PenText(o + new Vec2(0, -2.5 * mm), $"1:{F0(c.Scale)}", text.Height * 0.8, 0, "start", "top", false, text.Font), Ink);
            yield return mk;
        }
    }

    /// <summary>The map's grid as crosses where its lines meet, round eastings and northings, the values on the bottom row and the left column.</summary>
    static ViewMark? Grid(ViewContext c, Vec2 lo, Vec2 hi, double step, TextStyle text)
    {
        var m = c.Model;
        var corners = new[] { lo, new Vec2(hi.X, lo.Y), hi, new Vec2(lo.X, hi.Y) }.Select(p => GeoRef.ToMap(m, p)).ToList();
        double e0 = Math.Ceiling(corners.Min(p => p.E) / step) * step, e1 = corners.Max(p => p.E);
        double n0 = Math.Ceiling(corners.Min(p => p.N) / step) * step, n1 = corners.Max(p => p.N);
        if ((e1 - e0) / step > 60 || (n1 - n0) / step > 60) return null;
        var east = GeoRef.ToPlan(m, 1, 0) - GeoRef.ToPlan(m, 0, 0);
        var north = new Vec2(-east.Y, east.X);
        var arm = 2.5 * c.Mm;
        var mk = new ViewMark(null);
        var inside = new List<(Vec2 P, double E, double N)>();
        for (var e = e0; e <= e1 + 1e-9; e += step)
            for (var n = n0; n <= n1 + 1e-9; n += step)
            {
                var p = GeoRef.ToPlan(m, e, n);
                if (p.X < lo.X || p.Y < lo.Y || p.X > hi.X || p.Y > hi.Y) continue;
                mk.Line(p - east * arm, p + east * arm, Layer, Pens.Default.Hatch, Ink);
                mk.Line(p - north * arm, p + north * arm, Layer, Pens.Default.Hatch, Ink);
                inside.Add((p, e, n));
            }
        if (inside.Count == 0) return null;
        var h = text.Height * 0.7;
        var gap = 1.2 * c.Mm;
        // Each easting under its lowest cross, each northing beside its leftmost one.
        foreach (var g in inside.GroupBy(x => x.E)) { var p = g.MinBy(x => x.P.Y); mk.Text(new PenText(p.P + new Vec2(gap, -gap), $"E {Grouped(g.Key)}", h, 0, "start", "top", false, text.Font), Ink); }
        foreach (var g in inside.GroupBy(x => x.N)) { var p = g.MinBy(x => x.P.X); mk.Text(new PenText(p.P + new Vec2(gap, gap), $"N {Grouped(g.Key)}", h, 0, "start", "bottom", false, text.Font), Ink); }
        return mk;
    }
}
