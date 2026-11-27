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
/// them the map's coordinate grid (when the project is tied to the map), and a
/// north arrow and a scale bar of its own (decision 722: annotations of the view,
/// <see cref="DrawingKeys"/>, which can be moved, sized and deleted; a project
/// saved before has them made when it opens, <see cref="KeysUpgrade"/>). A site
/// plan stands on its host storey (the terrain's, else the ground floor's), where
/// what is drawn on it goes.
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
    /// <summary>Whether a view has a north arrow of its own (an annotation, decision 722).</summary>
    public static bool NorthArrow(IModelView m, Entity view) => DrawingKeys.On(m, view.Id, DrawingKeys.NorthArrow).Any();
    /// <summary>Whether a view has a scale bar of its own.</summary>
    public static bool ScaleBar(IModelView m, Entity view) => DrawingKeys.On(m, view.Id, DrawingKeys.ScaleBar).Any();

    /// <summary>On a site view, the flag that says its keys are annotations of its own (decision 722); a view without it is brought up to date.</summary>
    public const string KeysKey = "siteKeys";

    /// <summary>
    /// A project saved before decision 722 (its site plans drew their north arrow and scale bar themselves, <c>northArrow</c> and
    /// <c>scaleBar</c> false leaving one out): each site view's keys made annotations of its own, standing where they stood.
    /// </summary>
    public sealed class KeysUpgrade : IProjectUpgrade
    {
        public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
        {
            if (!entities.Any(e => Of(e) != null && e.Attrs[KeysKey] == null)) return (entities, relations);
            var list = new List<Entity>(entities.Count + 4);
            var added = new List<Entity>();
            foreach (var e in entities)
            {
                if (Of(e) == null || e.Attrs[KeysKey] != null) { list.Add(e); continue; }
                var attrs = e.CloneAttrs();
                var (arrow, bar) = (Flag(e, "northArrow"), Flag(e, "scaleBar"));
                attrs.Remove("northArrow");
                attrs.Remove("scaleBar");
                attrs[KeysKey] = true;
                list.Add(e.With(attrs: attrs));
                added.AddRange(DrawingKeys.SiteKeys(GlobalId.New, e.Id, arrow, bar));
            }
            list.AddRange(added);
            return (list, relations);
        }
    }

    /// <summary>Whether the project stands out (its buildings filled, the parcel in red): a location plan's default, a site plan's on request.</summary>
    public static bool Highlight(Entity view) => view.Attrs["highlight"] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : Of(view) == LocationPlan;

    /// <summary>The project's colour on a site view that highlights it.</summary>
    public const string ProjectColor = "#c62828";

    /// <summary>The coordinate grid's step (m of the map): the view's own (0: none), else a tenth of the scale (100 mm on paper).</summary>
    public static double GridStep(Entity view) => view.NumOrNull("gridStep") ?? (view.NumOrNull("scale") ?? 500) / 10;

    /// <summary>A site view's attributes on create: its purpose, scale and name.</summary>
    public static (JsonObject Attrs, string Name) New(string purpose, string storeyId, double? scale, string? name)
    {
        var attrs = new JsonObject { ["kind"] = "plan", ["site"] = purpose, ["storeyId"] = storeyId, ["scale"] = scale ?? DefaultScale(purpose), ["cutHeight"] = Views.DefaultCut, [KeysKey] = true };
        return (attrs, name ?? DefaultName(purpose));
    }

    /// <summary>A site view's settings: {id, site?, gridStep? (0: none; null: by the scale), northArrow?, scaleBar?, highlight? (null: by the purpose)}.</summary>
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
        if (a.ContainsKey("highlight"))
        {
            if (a["highlight"] is JsonValue hv && hv.TryGetValue<bool>(out var h)) attrs["highlight"] = h; else attrs.Remove("highlight");
        }
        ctx.Replace(v.With(attrs: attrs));
        // Its north arrow and scale bar (decision 722): one added where a site plan draws it when switched on, every one taken off when off.
        foreach (var (k, kind) in new[] { ("northArrow", DrawingKeys.NorthArrow), ("scaleBar", DrawingKeys.ScaleBar) })
        {
            if (a[k] is not JsonValue fv || !fv.TryGetValue<bool>(out var on)) continue;
            var have = DrawingKeys.On(ctx.Model, v.Id, kind).ToList();
            if (on && have.Count == 0 && ctx.Describe(IfcClass.Annotation) != null) foreach (var key in DrawingKeys.SiteKeys(ctx.NewId, v.Id, kind == DrawingKeys.NorthArrow, kind == DrawingKeys.ScaleBar)) ctx.Add(key);
            if (!on) foreach (var key in have) ctx.Remove(key.Id);
        }
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
    static string Grouped(double v) => Math.Round(v).ToString("#,0", CultureInfo.InvariantCulture).Replace(",", " ");

    /// <summary>A site view's highlight and coordinate grid, round what it draws.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan" || Of(c.View) == null) yield break;
        if (c.Drawing.Bounds is not { } box) yield break;
        var text = Standards.ResolveText(c.Book, []);
        var mm = c.Mm;
        var (lo, hi) = (box.Min - new Vec2(8 * mm, 8 * mm), box.Max + new Vec2(8 * mm, 8 * mm));

        if (Highlight(c.View) && Footprints(c) is { } project) yield return project;

        if (SitePlans.GridStep(c.View) is var step && step > 0 && GeoRef.Georeferenced(c.Model) && Grid(c, lo, hi, step, text) is { } grid) yield return grid;

        // The north arrow and the scale bar are annotations of the view (decision 722, DrawingKeys).
    }

    /// <summary>What a site view draws by its symbols, not as the project's buildings: the terrain, paths, plants, people and cars, and the site's features (the neighbours among them).</summary>
    public static readonly ClassSet SiteClasses = [IfcClass.GeographicElement, "IfcPavement", "LapinPlanting", "LapinSiteFeature", "LapinEntourage", "LapinFeatureLine"];

    /// <summary>
    /// The project's buildings filled in its colour, under the drawing's lines: every face turned up of every element
    /// on a storey above the ground (filled together they make the footprint seen from above), the site's own left out.
    /// </summary>
    static ViewMark? Footprints(ViewContext c)
    {
        if (c.Geometry == null) return null;
        var mk = new ViewMark(null) { Under = true };
        foreach (var e in c.Model.Entities)
        {
            if (e.Class is IfcClass.OpeningElement or IfcClass.Space || SiteClasses.Contains(e.Class) || Spatial.StoreyOf(c.Model, e.Id) == null) continue;
            if (c.Geometry(e.Id) is not { } g) continue;
            foreach (var part in g.Parts)
            {
                var solid = part.Solid;
                if (Measure.Bounds(solid) is not { } b || b.Max.Z <= Underground) continue;
                foreach (var f in solid.Faces)
                {
                    if (f.Normal.Z < 0.05 || f.Outer.Length < 3) continue;
                    var loops = new List<IReadOnlyList<Vec2>>(1 + f.Holes.Length) { f.Outer.Select(i => solid.Vertices[i].Xy).ToArray() };
                    foreach (var h in f.Holes) loops.Add(h.Select(i => solid.Vertices[i].Xy).ToArray());
                    mk.Fills.Add((loops, ProjectColor));
                }
            }
        }
        return mk.Fills.Count > 0 ? mk : null;
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
