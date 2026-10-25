using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A retaining wall along a path: a stem on a footing, the high ground on one
/// side. On each segment the crown is the high ground's height plus the
/// wall's height (or an absolute elevation) and the footing is embedded under
/// the low ground, so on a slope the wall steps. A Lapin class exported as
/// IfcWall RETAININGWALL.
/// </summary>
public static class RetainingWalls
{
    public const string Class = "LapinRetainingWall";
    public const string Color = "#a8a8a8";

    public static IReadOnlyList<Vec2> Path(Entity e) => Terrains.Contour(e, "path");

    public sealed record Spec(double Thickness, double Height, double? TopElevation, double Embed, double FootingWidth, double FootingThickness, bool HighOnLeft);

    public static Spec Of(Entity e) => new(
        Math.Max(0.1, e.Num("thickness", 0.3)), Math.Max(0, e.Num("height", 1.0)), e.NumOrNull("topElevation"),
        Math.Max(0, e.Num("embed", 0.3)), Math.Max(e.Num("thickness", 0.3), e.Num("footingWidth", 0.8)), Math.Max(0, e.Num("footingThickness", 0.3)), e.Str("side") != "right");

    static Terrain? TerrainFor(IModelView m, Entity e) =>
        m.OfClass(IfcClass.GeographicElement).FirstOrDefault(t => t.PredefinedType == Terrains.Predefined && Spatial.StoreyOf(m, t.Id)?.Id == Spatial.StoreyOf(m, e.Id)?.Id) is { } te ? Terrains.Of(te) : null;

    /// <summary>One segment of the wall: its section (the inverted T, in the left-up plane at its start) extruded along it, with the levels it was built to.</summary>
    public sealed record Segment(Solid Solid, double Length, double Top, double Bottom, double Ground);

    public static List<Segment> Segments(IModelView m, Entity e)
    {
        var path = Path(e);
        var s = Of(e);
        var terrain = TerrainFor(m, e);
        var fallback = Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;
        var o = new List<Segment>();
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var (p, q) = (path[i], path[i + 1]);
            var along = q - p;
            if (along.Length < 1e-6) continue;
            var dir = along.Normalized();
            var left = dir.Perp;
            var high = s.HighOnLeft ? 1.0 : -1.0;
            var mid = (p + q) * 0.5;
            var probe = s.Thickness / 2 + 0.5;
            var zHigh = terrain?.ElevationAt(mid.X + left.X * high * probe, mid.Y + left.Y * high * probe) ?? fallback;
            var zLow = terrain?.ElevationAt(mid.X - left.X * high * probe, mid.Y - left.Y * high * probe) ?? fallback;
            var bottom = Math.Min(zLow, zHigh) - s.Embed - s.FootingThickness;
            var top = s.TopElevation ?? zHigh + s.Height;
            if (top <= bottom + s.FootingThickness + 0.05) top = bottom + s.FootingThickness + 0.05;
            var (t, wf, tf) = (s.Thickness / 2, s.FootingWidth / 2, s.FootingThickness);
            Vec3 P(double n, double z) => (p + left * n).At(z);
            List<Vec3> ring = [P(-wf, bottom), P(wf, bottom), P(wf, bottom + tf), P(t, bottom + tf), P(t, top), P(-t, top), P(-t, bottom + tf), P(-wf, bottom + tf)];
            if (tf <= 0) ring = [P(-t, bottom), P(t, bottom), P(t, top), P(-t, top)];
            if (SiteGeometry.Oriented(ring, new Vec3(along.X, along.Y, 0), "retaining") is { } solid) o.Add(new Segment(solid, along.Length, top, bottom, zHigh));
        }
        return o;
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var segments = Segments(m, e);
        if (segments.Count == 0) return new ElementGeometry([], Diagnostics: ["The retaining wall needs two points at least."]);
        var material = e.Str("material");
        var parts = new List<GeometryPart> { new("Retaining wall", material != null ? Materials.ColorOf(m, material) : Color, Paths.Combine(segments.Select(x => x.Solid).ToList(), "retaining"), material, "wall") };
        var path = Path(e);
        var s = Of(e);
        var dirs = SiteGeometry.Directions(path);
        var left = path.Select((p, i) => p + dirs[i].Perp * (s.Thickness / 2)).ToList();
        var right = path.Select((p, i) => p - dirs[i].Perp * (s.Thickness / 2)).Reverse().ToList();
        return new ElementGeometry(parts, [new PlanItem("cut", true, [.. left, .. right])], [path[0], path[^1]]);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null) yield break;
        var segments = Segments(m, e);
        var s = Of(e);
        var length = segments.Sum(x => x.Length);
        var stem = segments.Sum(x => s.Thickness * Math.Max(0, x.Top - x.Bottom - s.FootingThickness) * x.Length);
        var footing = segments.Sum(x => s.FootingWidth * s.FootingThickness * x.Length);
        var formwork = segments.Sum(x => 2 * Math.Max(0, x.Top - x.Bottom - s.FootingThickness) * x.Length + 2 * s.FootingThickness * x.Length);
        yield return new QuantitySet("Lapin_RetainingWallQuantities",
        [
            new("Length", length, "LENGTH"), new("MaxHeight", segments.Max(x => x.Top - x.Bottom), "LENGTH", "Height, footing included"),
            new("ConcreteVolume", g.Parts.Sum(p => Measure.Volume(p.Solid)), "VOLUME", "Concrete"),
            new("StemVolume", stem, "VOLUME", "Stem"), new("FootingVolume", footing, "VOLUME", "Footing"),
            new("FormworkArea", formwork, "AREA", "Formwork"),
        ]);
    }

    public static JsonObject Attrs(IReadOnlyList<Vec2> path, Spec s, string? material)
    {
        var o = new JsonObject
        {
            ["path"] = Terrains.ContourJson(path), ["thickness"] = s.Thickness, ["height"] = s.Height, ["embed"] = s.Embed,
            ["footingWidth"] = s.FootingWidth, ["footingThickness"] = s.FootingThickness, ["side"] = s.HighOnLeft ? "left" : "right",
            ["exportAs"] = IfcClass.Wall,
        };
        if (s.TopElevation is { } top) o["topElevation"] = top;
        if (material != null) o["material"] = material;
        return o;
    }
}
