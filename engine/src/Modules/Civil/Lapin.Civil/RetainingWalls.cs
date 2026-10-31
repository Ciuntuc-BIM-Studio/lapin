using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A retaining wall along a path: a stem on a footing, the high ground on one
/// side. Following the ground, each segment's crown is the high ground's
/// height plus the wall's height (or an absolute elevation) and its footing is
/// embedded under the low ground, so on a slope the wall steps; with a
/// designed profile the crown follows the red line. Its corners are mitred,
/// so it runs on unbroken except at its joints (<see cref="Corridors"/>).
/// A Lapin class exported as IfcWall RETAININGWALL.
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

    /// <summary>One segment's levels: the crown following the ground, the footing's underside, the high ground, its length.</summary>
    public sealed record Level(double Top, double Bottom, double Ground, double Length);

    /// <summary>The levels of every segment, and the crown at a station of a segment (the red line when designed).</summary>
    public sealed record Built(IReadOnlyList<Level> Levels, Func<double, int, double> Top);

    public static Built Levels(IModelView m, Entity e, Chain c)
    {
        var s = Of(e);
        var ground = Corridors.GroundOf(m, e, CorridorSpec.Of(e).Ground);
        var fallback = Corridors.Fallback(m, e);
        var levels = new List<Level>();
        for (var i = 0; i < c.Segments; i++)
        {
            var (a, b) = (c.StationOf(i), c.StationOf(i + 1));
            var midStation = (a + b) / 2;
            var left = c.DirectionAt(midStation).Perp;
            var high = s.HighOnLeft ? 1.0 : -1.0;
            var mid = c.PointAt(midStation);
            var probe = s.Thickness / 2 + 0.5;
            var zHigh = ground(mid + left * (high * probe)) ?? fallback;
            var zLow = ground(mid - left * (high * probe)) ?? fallback;
            var bottom = Math.Min(zLow, zHigh) - s.Embed - s.FootingThickness;
            levels.Add(new(s.TopElevation ?? zHigh + s.Height, bottom, zHigh, b - a));
        }
        var spec = CorridorSpec.Of(e);
        Func<double, double>? red = spec.Mode == "design" && s.TopElevation == null ? Corridors.RedLine(m, e, c) : null;
        double Top(double station, int seg)
        {
            var l = levels[seg];
            var top = red?.Invoke(station) ?? l.Top;
            return Math.Max(top, l.Bottom + s.FootingThickness + 0.05);
        }
        return new(levels, Top);
    }

    /// <summary>The section at a station: the inverted T (a plain stem without a footing), in (offset, elevation).</summary>
    public static List<SectionPart> SectionAt(Spec s, Built b, double station, int seg)
    {
        var bottom = b.Levels[seg].Bottom;
        var top = b.Top(station, seg);
        var (t, wf, tf) = (s.Thickness / 2, s.FootingWidth / 2, s.FootingThickness);
        List<Vec2> ring = tf <= 0
            ? [new(-t, bottom), new(t, bottom), new(t, top), new(-t, top)]
            : [new(-wf, bottom), new(wf, bottom), new(wf, bottom + tf), new(t, bottom + tf), new(t, top), new(-t, top), new(-t, bottom + tf), new(-wf, bottom + tf)];
        return [new("wall", ring)];
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var c = Corridors.ChainOf(e);
        if (c.Segments == 0) return new ElementGeometry([], Diagnostics: ["The retaining wall needs two points at least."]);
        var s = Of(e);
        var built = Levels(m, e, c);
        var spec = CorridorSpec.Of(e);
        var solids = Corridors.Sweep(c, Corridors.Runs(c, spec), (st, seg) => SectionAt(s, built, st, seg), spec.Mode == "design" ? 1.0 : 1e9);
        if (!solids.TryGetValue("wall", out var list) || list.Count == 0) return new ElementGeometry([], Diagnostics: ["The retaining wall could not be built."]);
        var material = e.Str("material");
        var parts = new List<GeometryPart> { new("Retaining wall", material != null ? Materials.ColorOf(m, material) : Color, Paths.Combine(list, "retaining"), material, "wall") };
        var plan = new List<PlanItem> { new("cut", true, Corridors.Outline(c, s.Thickness / 2, -s.Thickness / 2)) };
        foreach (var j in Corridors.Joints(c, spec))
        {
            var (a, b) = Corridors.LineAcross(c, j, s.Thickness / 2, -s.Thickness / 2);
            plan.Add(new("cut", false, [a, b]));
        }
        return new ElementGeometry(parts, plan, [c.Path[0], c.Path[^1]], c.Problem is { } why ? [why] : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null) yield break;
        var c = Corridors.ChainOf(e);
        if (c.Segments == 0) yield break;
        var s = Of(e);
        var built = Levels(m, e, c);
        var spec = CorridorSpec.Of(e);
        var joints = Corridors.Joints(c, spec).Count;
        var length = c.Length;
        // The stem's height on each segment: the crown's mean over it, less the footing.
        double Stem(int i)
        {
            var (a, b) = (c.StationOf(i), c.StationOf(i + 1));
            var mean = Enumerable.Range(0, 11).Average(k => built.Top(a + (b - a) * k / 10, i));
            return Math.Max(0, mean - built.Levels[i].Bottom - s.FootingThickness);
        }
        var net = (double i) => built.Levels[(int)i].Length * (1 - joints * spec.JointWidth / Math.Max(length, 1e-9));
        var stem = Enumerable.Range(0, c.Segments).Sum(i => s.Thickness * Stem(i) * net(i));
        var footing = Enumerable.Range(0, c.Segments).Sum(i => s.FootingWidth * s.FootingThickness * net(i));
        var formwork = Enumerable.Range(0, c.Segments).Sum(i => 2 * Stem(i) * net(i) + 2 * s.FootingThickness * net(i));
        yield return new QuantitySet("Lapin_RetainingWallQuantities",
        [
            new("Length", length, "LENGTH"), new("MaxHeight", Enumerable.Range(0, c.Segments).Max(i => Stem(i) + s.FootingThickness), "LENGTH", "Height, footing included"),
            new("ConcreteVolume", g.Parts.Sum(p => Measure.Volume(p.Solid)), "VOLUME", "Concrete"),
            new("StemVolume", stem, "VOLUME", "Stem"), new("FootingVolume", footing, "VOLUME", "Footing"),
            new("FormworkArea", formwork, "AREA", "Formwork"), new("Joints", joints, "COUNT", "Joints"),
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
