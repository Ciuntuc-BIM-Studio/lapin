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
/// known. Both show in plans, with the internal origin, when the project asks
/// (<c>show</c>); the 3D view draws them too. The user coordinate system
/// (<c>ucs</c>) the 2D views draft in is kept here as well.
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
            // The user coordinate system the 2D views draft in ({x, y, angle}: its origin in the plan and its X axis's angle, degrees; null: the plan's own).
            if (a.ContainsKey("ucs"))
            {
                if (a["ucs"] is JsonObject u)
                {
                    var x = Json.Number(u["x"]) ?? 0; var y = Json.Number(u["y"]) ?? 0; var ang = Json.Number(u["angle"]) ?? 0;
                    if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(ang)) throw new CommandException("The UCS needs finite numbers.");
                    ang = Math.IEEERemainder(ang, 360);
                    if (Math.Abs(x) < 1e-12 && Math.Abs(y) < 1e-12 && Math.Abs(ang) < 1e-9) loc.Remove("ucs");
                    else loc["ucs"] = new JsonObject { ["x"] = x, ["y"] = y, ["angle"] = ang };
                }
                else loc.Remove("ucs");
            }
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

    /// <summary>The user coordinate system the 2D views draft in (its origin in the plan, its X axis's angle in degrees), or null: the plan's own.</summary>
    public static (Vec2 Origin, double Angle)? Ucs(IModelView m) => Of(m)["ucs"] is JsonObject u
        ? (new Vec2(Json.Number(u["x"]) ?? 0, Json.Number(u["y"]) ?? 0), Json.Number(u["angle"]) ?? 0)
        : null;

    /// <summary>
    /// In plans, as Revit's: the internal origin (the plan's 0,0: its X and Y axes), the base point (a circle with a
    /// cross, its elevation, its map coordinates when the project is tied to the map, and an arrow to true north when
    /// the project is turned from it) and the survey point (a triangle, its coordinates).
    /// </summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan" || !Shown(c.Model)) yield break;
        var text = Standards.ResolveText(c.Book, []);
        var r = 3 * c.Mm;
        const string color = "#2e6db4";
        static string F(double? v) => (v ?? 0).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        // The internal origin: never moves; the X axis in red, the Y axis in green, as the 3D view's.
        var om = new ViewMark(null);
        var o = new Vec2(0, 0);
        var ax = r * 3;
        om.Line(o, new Vec2(ax, 0), "LOCATION", Pens.Default.Seen, "#c0392b");
        om.Polyline([new Vec2(ax, 0), new Vec2(ax - r * 0.7, r * 0.35), new Vec2(ax - r * 0.7, -r * 0.35)], true, "LOCATION", Pens.Default.Seen, "#c0392b");
        om.Line(o, new Vec2(0, ax), "LOCATION", Pens.Default.Seen, "#27ae60");
        om.Polyline([new Vec2(0, ax), new Vec2(r * 0.35, ax - r * 0.7), new Vec2(-r * 0.35, ax - r * 0.7)], true, "LOCATION", Pens.Default.Seen, "#27ae60");
        om.Text(new PenText(new Vec2(r * 0.4, -r * 0.4), "Internal origin", text.Height * 0.7, 0, "start", "top"), color);
        om.Snaps.Add(o);
        yield return om;

        var b = Base(c.Model);
        var mk = new ViewMark(null);
        mk.Polyline(ViewMark.Circle(b.At, r, 24), true, "LOCATION", Pens.Default.Seen, color);
        mk.Line(b.At - new Vec2(r * 1.4, 0), b.At + new Vec2(r * 1.4, 0), "LOCATION", Pens.Default.Hatch, color);
        mk.Line(b.At - new Vec2(0, r * 1.4), b.At + new Vec2(0, r * 1.4), "LOCATION", Pens.Default.Hatch, color);
        mk.Text(new PenText(b.At + new Vec2(r * 1.6, r * 0.4), $"Base point {Standards.FormatLevel(b.Elevation, c.Book)}", text.Height * 0.8, 0, "start", "bottom"), color);
        var line = 1;
        if (Survey(c.Model) != null)
        {
            var (e, n) = GeoRef.ToMap(c.Model, b.At);
            mk.Text(new PenText(b.At + new Vec2(r * 1.6, r * 0.4 - text.Height * 1.1 * line++), $"E {F(e)} N {F(n)}", text.Height * 0.7, 0, "start", "bottom"), color);
        }
        // True north, when the project is turned from it: an arrow from the base point (the map's north in the plan).
        var th = b.NorthAngle ?? 0;
        if (Math.Abs(th) > 1e-9)
        {
            var rad = th * Math.PI / 180;
            var north = new Vec2(-Math.Sin(rad), Math.Cos(rad));
            var side = new Vec2(north.Y, -north.X);
            var tip = b.At + north * (r * 4);
            mk.Line(b.At + north * r, tip, "LOCATION", Pens.Default.Seen, color);
            mk.Polyline([tip, tip - north * (r * 0.8) + side * (r * 0.35), tip - north * (r * 0.8) - side * (r * 0.35)], true, "LOCATION", Pens.Default.Seen, color);
            mk.Text(new PenText(tip + north * (r * 0.3), "N", text.Height * 0.8, 0, "middle", "bottom"), color);
            mk.Text(new PenText(b.At + new Vec2(r * 1.6, r * 0.4 - text.Height * 1.1 * line), $"True north {th.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}°", text.Height * 0.7, 0, "start", "bottom"), color);
        }
        mk.Snaps.Add(b.At);
        yield return mk;
        if (Survey(c.Model) is not { } s) yield break;
        var sk = new ViewMark(null);
        sk.Polyline([s.At + new Vec2(0, r * 1.2), s.At + new Vec2(-r, -r * 0.6), s.At + new Vec2(r, -r * 0.6)], true, "LOCATION", Pens.Default.Seen, color);
        sk.Text(new PenText(s.At + new Vec2(r * 1.6, r * 0.4), $"Survey point E {F(s.Easting)} N {F(s.Northing)} {Standards.FormatLevel(s.Elevation, c.Book)}", text.Height * 0.8, 0, "start", "bottom"), color);
        sk.Snaps.Add(s.At);
        yield return sk;
    }
}
