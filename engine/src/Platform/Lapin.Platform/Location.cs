using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The project's location (as in Revit): the base point, the project's own
/// origin with its elevation and the angle to true north; and the survey
/// point, where the site's coordinates (easting, northing, elevation) are
/// known. Both show in plans when the project asks (<c>show</c>).
/// </summary>
public static class Location
{
    public sealed record Point(Vec2 At, double Elevation, double? Easting = null, double? Northing = null, double? NorthAngle = null);

    static JsonObject Of(IModelView m) => Spatial.Project(m)?.Attrs["location"] as JsonObject ?? [];

    public static Point Base(IModelView m)
    {
        var b = Of(m)["base"] as JsonObject;
        return new Point(new Vec2(Json.Number(b?["x"]) ?? 0, Json.Number(b?["y"]) ?? 0), Json.Number(b?["elevation"]) ?? 0, NorthAngle: Json.Number(b?["northAngle"]) ?? 0);
    }

    public static Point? Survey(IModelView m) => Of(m)["survey"] is JsonObject s
        ? new Point(new Vec2(Json.Number(s["x"]) ?? 0, Json.Number(s["y"]) ?? 0), Json.Number(s["elevation"]) ?? 0, Json.Number(s["easting"]) ?? 0, Json.Number(s["northing"]) ?? 0)
        : null;

    /// <summary>The project's coordinate system ("EPSG:3844"…), or null.</summary>
    public static string? Crs(IModelView m) => (string?)Of(m)["crs"] is { Length: > 0 } c ? c : (string?)Spatial.Project(m)?.Attrs["crs"] is { Length: > 0 } old ? old : null;

    /// <summary>
    /// Ties the project to the map when it is not yet (an import in real
    /// coordinates): the survey point at the plan's origin, standing on the
    /// map coordinates given, the system set when the project has none.
    /// </summary>
    public static void Georeference(ICommandContext ctx, string? crs, double easting, double northing)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        var loc = (attrs["location"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
        if (loc["survey"] is not JsonObject) loc["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = easting, ["northing"] = northing, ["elevation"] = 0 };
        if ((string?)loc["crs"] is not { Length: > 0 } && GeoRef.Get(crs) is { } known) loc["crs"] = known.Id;
        attrs["location"] = loc;
        ctx.Replace(project.With(attrs: attrs));
    }

    public static bool Shown(IModelView m) => Of(m)["show"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public static void Register(IModuleRegistry r)
    {
        // The base point and the survey point ({x, y, elevation, northAngle} and {x, y, easting, northing, elevation}; null: none), whether plans show them, the coordinate system.
        r.Command("project.location", "Project location", (ctx, a) =>
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = project.CloneAttrs();
            var loc = (attrs["location"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
            foreach (var k in new[] { "base", "survey" })
                if (a.ContainsKey(k))
                {
                    if (a[k] is JsonObject o) { var cur = loc[k] as JsonObject ?? new JsonObject(); foreach (var (f, v) in o) if (Json.Number(v) is { } n) cur[f] = n; loc[k] = cur.DeepClone(); }
                    else loc.Remove(k);
                }
            if (a["show"] is JsonValue sv && sv.TryGetValue<bool>(out var show)) loc["show"] = show;
            // The coordinate system eastings and northings are in ("EPSG:3844"…; null or empty: none).
            if (a.ContainsKey("crs"))
            {
                if ((string?)a["crs"] is { Length: > 0 } crs)
                    loc["crs"] = GeoRef.Get(crs)?.Id ?? throw new CommandException($"Lapin does not know the coordinate system {crs}: {string.Join(", ", GeoRef.Offered)}.");
                else loc.Remove("crs");
            }
            attrs["location"] = loc;
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });

        r.Query("project.location", (q, _) => Of(q.Model).DeepClone());
        // A plan point in map coordinates and in longitude and latitude, or map coordinates in the plan ({x, y} or {e, n}).
        r.Query("project.geo", (q, a) =>
        {
            var m = q.Model;
            var p = Json.Number(a["e"]) is { } e && Json.Number(a["n"]) is { } n ? GeoRef.ToPlan(m, e, n) : new Vec2(Json.Number(a["x"]) ?? 0, Json.Number(a["y"]) ?? 0);
            var (me, mn) = GeoRef.ToMap(m, p);
            var o = new JsonObject { ["x"] = p.X, ["y"] = p.Y, ["e"] = me, ["n"] = mn, ["crs"] = Crs(m), ["georeferenced"] = GeoRef.Georeferenced(m) };
            if (GeoRef.LonLat(m, p) is { } ll) { o["lon"] = ll.Lon; o["lat"] = ll.Lat; }
            return o;
        });
        r.Catalog("crs", new JsonArray(GeoRef.Offered.Select(id => (JsonNode)new JsonObject { ["id"] = id, ["name"] = GeoRef.Get(id)!.Name }).ToArray()));
    }

    /// <summary>In plans: the base point (a circle with a cross, its elevation) and the survey point (a triangle, its coordinates).</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan" || !Shown(c.Model)) yield break;
        var text = Standards.ResolveText(c.Book, []);
        var r = 3 * c.Mm;
        var b = Base(c.Model);
        var mk = new ViewMark(null);
        const string color = "#2e6db4";
        mk.Polyline(ViewMark.Circle(b.At, r, 24), true, "LOCATION", Pens.Default.Seen, color);
        mk.Line(b.At - new Vec2(r * 1.4, 0), b.At + new Vec2(r * 1.4, 0), "LOCATION", Pens.Default.Hatch, color);
        mk.Line(b.At - new Vec2(0, r * 1.4), b.At + new Vec2(0, r * 1.4), "LOCATION", Pens.Default.Hatch, color);
        mk.Text(new PenText(b.At + new Vec2(r * 1.6, r * 0.4), $"Base point {Standards.FormatLevel(b.Elevation)}", text.Height * 0.8, 0, "start", "bottom"), color);
        mk.Snaps.Add(b.At);
        yield return mk;
        if (Survey(c.Model) is not { } s) yield break;
        var sk = new ViewMark(null);
        sk.Polyline([s.At + new Vec2(0, r * 1.2), s.At + new Vec2(-r, -r * 0.6), s.At + new Vec2(r, -r * 0.6)], true, "LOCATION", Pens.Default.Seen, color);
        static string F(double? v) => (v ?? 0).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        sk.Text(new PenText(s.At + new Vec2(r * 1.6, r * 0.4), $"Survey point E {F(s.Easting)} N {F(s.Northing)} {Standards.FormatLevel(s.Elevation)}", text.Height * 0.8, 0, "start", "bottom"), color);
        sk.Snaps.Add(s.At);
        yield return sk;
    }
}
