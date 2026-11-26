using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Archicad;

/// <summary>
/// Archicad's location into the project's georeference (docs/plan-archicad.md A2; Lapin's location: docs/site-plans.md), from Tapir's
/// <c>GetGeoLocation</c>: its survey point (map eastings, northings and elevation at a point of the project, the coordinate system's
/// name, the project's X axis on the map) ties the plan to the map as an IFC file's map conversion does (the survey point at the plan's
/// origin, the angle to true north from the X axis, the elevation of ±0.00); its project location (latitude, longitude) places the base
/// point when no survey point is set. Only into a project that has no georeference yet, and never from Archicad's untouched default:
/// a new Archicad project stands in Budapest (longitude 19.05477, latitude 47.5597) with its survey point at the origin, no coordinate
/// system and no map coordinates, which tells nothing of the model. A turned survey point is taken (the turn is the base point's angle
/// to true north); the model itself is received in Archicad's project coordinates, or moved to its survey point when it is not turned.
/// </summary>
public static class ArchicadGeo
{
    /// <summary>Archicad's default project location, which a new project has until someone sets it.</summary>
    public const double DefaultLongitude = 19.05477, DefaultLatitude = 47.5597;

    /// <summary>Archicad's location as read: the place, the survey point, its system and the X axis on the map.</summary>
    public sealed record Read(double Longitude, double Latitude, double Altitude, double Eastings, double Northings, double Elevation, Vec3 SurveyInProject,
        string? Crs, double XAxisAbscissa, double XAxisOrdinate, double Scale)
    {
        public bool DefaultPlace => Math.Abs(Longitude - DefaultLongitude) < 5e-4 && Math.Abs(Latitude - DefaultLatitude) < 5e-4;

        /// <summary>Whether the survey point ties the project to the map: map coordinates, or a coordinate system named.</summary>
        public bool SurveySet => Math.Abs(Eastings) > 1e-6 || Math.Abs(Northings) > 1e-6 || Crs is { Length: > 0 };

        /// <summary>The angle to true north (degrees) the project's X axis on the map makes (cos θ, −sin θ), as an IFC map conversion's.</summary>
        public double NorthAngle => Math.Abs(XAxisAbscissa) < 1e-12 && Math.Abs(XAxisOrdinate) < 1e-12 ? 0 : Math.Atan2(-XAxisOrdinate, XAxisAbscissa) * 180 / Math.PI;

        /// <summary>Whether it is Archicad's untouched default: the default place and no survey point set.</summary>
        public bool Untouched => DefaultPlace && !SurveySet && SurveyInProject.Length < 1e-6;
    }

    public static Read? Parse(JsonObject? g)
    {
        if (g?["projectLocation"] is not JsonObject pl) return null;
        var sp = g["surveyPoint"] as JsonObject;
        var pos = sp?["position"] as JsonObject;
        var inProject = sp?["positionInProject"] as JsonObject;
        var gr = sp?["geoReferencingParameters"] as JsonObject;
        double N(JsonNode? n, double fallback = 0) => Json.Number(n) is { } v && double.IsFinite(v) ? v : fallback;
        var crs = (string?)gr?["crsName"] is { } c && c.Trim().Length > 0 ? c.Trim() : null;
        return new Read(N(pl["longitude"]), N(pl["latitude"]), N(pl["altitude"]), N(pos?["eastings"]), N(pos?["northings"]), N(pos?["elevation"]),
            new Vec3(N(inProject?["x"]), N(inProject?["y"]), N(inProject?["z"])), crs, N(gr?["xAxisAbscissa"], 1), N(gr?["xAxisOrdinate"]), N(gr?["scale"], 1));
    }

    /// <summary>Whether the project has a georeference of its own (a survey point, or the base point's latitude and longitude).</summary>
    static bool Has(IModelView m) => GeoRef.Georeferenced(m) || Location.Base(m).Latitude != null;

    /// <summary>What a receive would do with Archicad's location: <c>state</c> none | default | place | survey, and whether it is <c>taken</c> (and why not).</summary>
    public static JsonObject? Survey(IModelView m, ArchicadModel a, ArchicadReceiveOptions o)
    {
        if (Parse(a.GeoLocation) is not { } g) return null;
        var state = g.SurveySet ? "survey" : g.Untouched || g.DefaultPlace ? "default" : "place";
        var why = !o.Georeference ? "not asked" : Has(m) ? "the project is georeferenced already" : state == "default" ? "Archicad's location is its untouched default" : null;
        return new JsonObject
        {
            ["state"] = state, ["taken"] = why == null, ["why"] = why, ["latitude"] = g.Latitude, ["longitude"] = g.Longitude,
            ["eastings"] = g.SurveySet ? g.Eastings : null, ["northings"] = g.SurveySet ? g.Northings : null, ["crs"] = g.Crs,
            ["northAngle"] = g.SurveySet ? Math.Round(g.NorthAngle, 9) : null,
        };
    }

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>The project georeferenced from Archicad's location when it has none and Archicad's is not its default, in the caller's undo step; the report says what was done.</summary>
    public static JsonObject? Apply(IEdit edit, ArchicadModel a, ArchicadReceiveOptions o, Vec3 shift, List<string> notes)
    {
        if (Survey(edit.Model, a, o) is not { } report || Parse(a.GeoLocation) is not { } g) return null;
        if (!(bool)report["taken"]!) return report;
        var project = Spatial.Project(edit.Model);
        if (project == null) { Spatial.EnsureBuilding(edit); project = Spatial.Project(edit.Model)!; }
        var attrs = project.CloneAttrs();
        var loc = (attrs["location"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
        var basePoint = loc["base"] as JsonObject ?? new JsonObject();
        if (g.SurveySet)
        {
            // The map at the plan's origin: the survey point's map coordinates less its plan position turned onto the map.
            var th = g.NorthAngle * Math.PI / 180;
            var (c, sn) = (Math.Cos(th), -Math.Sin(th));
            var at = g.SurveyInProject + shift;
            var e0 = g.Eastings - (c * at.X - sn * at.Y);
            var n0 = g.Northings - (sn * at.X + c * at.Y);
            var h0 = g.Elevation - at.Z;
            loc["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = Math.Round(e0, 6), ["northing"] = Math.Round(n0, 6), ["elevation"] = Math.Round(h0, 6) };
            if (Math.Abs(Json.Number(basePoint["northAngle"]) ?? 0) < 1e-12 && Math.Abs(g.NorthAngle) > 1e-9) basePoint["northAngle"] = Math.Round(g.NorthAngle, 9);
            if (Math.Abs(Json.Number(basePoint["elevation"]) ?? 0) < 1e-12 && Math.Abs(h0) > 1e-9) basePoint["elevation"] = Math.Round(h0, 6);
            var known = GeoRef.Get(g.Crs);
            if (known != null && (string?)loc["crs"] is not { Length: > 0 }) loc["crs"] = known.Id;
            if (known == null && g.Crs is { } name) notes.Add($"Archicad's coordinate system {name} is not one Lapin knows: the survey point keeps its eastings and northings, with no system (Manage → Location).");
            if (Math.Abs(g.Scale - 1) > 1e-9 && Math.Abs(g.Scale) > 1e-12) notes.Add($"Archicad's map conversion has a scale of {g.Scale.ToString("0.######", CultureInfo.InvariantCulture)}: Lapin keeps the model at its own size (scale 1).");
            notes.Add($"The project is tied to the map as Archicad's survey point is: E {F(e0)} N {F(n0)}{(known != null ? $" in {known.Id}" : "")}{(Math.Abs(g.NorthAngle) > 1e-9 ? $", turned {g.NorthAngle.ToString("0.####", CultureInfo.InvariantCulture)}° to true north" : "")}.");
        }
        else
        {
            basePoint["latitude"] = g.Latitude;
            basePoint["longitude"] = g.Longitude;
            if (Math.Abs(Json.Number(basePoint["elevation"]) ?? 0) < 1e-12 && Math.Abs(g.Altitude) > 1e-9) basePoint["elevation"] = g.Altitude;
            notes.Add($"The project stands where Archicad's project does: latitude {g.Latitude.ToString("0.######", CultureInfo.InvariantCulture)}, longitude {g.Longitude.ToString("0.######", CultureInfo.InvariantCulture)} (Manage → Location).");
        }
        loc["base"] = basePoint;
        attrs["location"] = loc;
        edit.Replace(project.With(attrs: attrs));
        report["applied"] = true;
        return report;
    }
}
