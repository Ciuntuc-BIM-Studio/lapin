using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;

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
        if (ProfileOf(m, e) is { } ring) return ProfileGeometry(e, m, ring);
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

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m) =>
        g == null ? [] : ProfileOf(m, e) != null ? Profiled(e, g, m) : BatteredWalls.Is(e) ? BatteredWalls.Quantities(e, g, m) : Plain(e, g, m);

    // ------------------------------------------------------------------ a drawn profile (docs/plan-wing-walls.md, S3)

    /// <summary>
    /// The stem's section drawn and saved as a profile (<c>profileId</c>, a project's copy of an outline): the user's own section beside
    /// the coded one. Its outside ring, as drawn (x across, + towards the high ground; y up), is stretched at each station between the
    /// footing's top and the crown, its width kept; the footing under it reaches the toe and the heel past its foot.
    /// </summary>
    public static IReadOnlyList<Vec2>? ProfileOf(IModelView m, Entity e) =>
        e.Str("profileId") is { Length: > 0 } id && m.Get(id) is { Class: ProjectProfiles.Class } p && ProjectProfiles.Read(p)?.Rings is { Count: > 0 } rings
            && rings.FirstOrDefault(r => Polygon2.SignedArea(r) > 1e-9) is { Count: >= 3 } outside ? outside : null;

    /// <summary>The crown and the footing's underside of segment <paramref name="seg"/> at a station: its own (from the configurator) where it has them.</summary>
    static (double Top, double Bottom) ProfileLevels(Entity e, Built b, Chain c, double station, int seg)
    {
        var g = BatteredWalls.SegmentOf(e, seg);
        var bottom = g.FootingLevel ?? b.Levels[seg].Bottom;
        var (a, z) = (c.StationOf(seg), c.StationOf(seg + 1));
        var f = z - a < 1e-9 ? 0 : Math.Clamp((station - a) / (z - a), 0, 1);
        var top = g.TopStart is { } t0 ? t0 + ((g.TopEnd ?? t0) - t0) * f : b.Top(station, seg);
        return (top, bottom);
    }

    /// <summary>A drawn-profile wall's section at a station (offset + left, elevation).</summary>
    public static List<SectionPart> ProfileSectionAt(IModelView m, Entity e, IReadOnlyList<Vec2> ring, double station, int seg)
    {
        var c = Corridors.ChainOf(e);
        return ProfileSection(e, ring, Of(e), Levels(m, e, c), c, station, Math.Clamp(seg, 0, Math.Max(0, c.Segments - 1)));
    }

    static List<SectionPart> ProfileSection(Entity e, IReadOnlyList<Vec2> ring, Spec s, Built b, Chain c, double station, int seg)
    {
        var (top, bottom) = ProfileLevels(e, b, c, station, seg);
        var zf = bottom + s.FootingThickness;
        top = Math.Max(top, zf + 0.05);
        var (x0, x1) = (ring.Min(p => p.X), ring.Max(p => p.X));
        var (y0, y1) = (ring.Min(p => p.Y), ring.Max(p => p.Y));
        var xm = (x0 + x1) / 2;
        // Offsets + towards the high ground; the corridor's are + left: mirrored (and turned back anticlockwise) when the high ground is on the right.
        var side = s.HighOnLeft ? 1.0 : -1.0;
        var stem = ring.Select(p => new Vec2(side * (p.X - xm), zf + (p.Y - y0) / Math.Max(y1 - y0, 1e-9) * (top - zf))).ToList();
        if (!s.HighOnLeft) stem.Reverse();
        var parts = new List<SectionPart> { new("wall", stem) };
        if (s.FootingThickness > 1e-6)
        {
            var toe = e.NumOrNull("toe") ?? Math.Max(0.05, (s.FootingWidth - (x1 - x0)) / 2);
            var heel = e.NumOrNull("heel") ?? toe;
            var (fa, fb) = (-(x1 - x0) / 2 - toe, (x1 - x0) / 2 + heel);
            var (l, r) = s.HighOnLeft ? (fa, fb) : (-fb, -fa);
            parts.Add(new("wall", [new(l, bottom), new(r, bottom), new(r, zf), new(l, zf)]));
        }
        return parts;
    }

    static ElementGeometry? ProfileGeometry(Entity e, IModelView m, IReadOnlyList<Vec2> ring)
    {
        var c = Corridors.ChainOf(e);
        if (c.Segments == 0) return new ElementGeometry([], Diagnostics: ["The retaining wall needs two points at least."]);
        var s = Of(e);
        var built = Levels(m, e, c);
        var spec = CorridorSpec.Of(e);
        var solids = Corridors.Sweep(c, Corridors.Runs(c, spec), (st, seg) => ProfileSection(e, ring, s, built, c, st, seg), spec.Mode == "design" ? 1.0 : 1e9);
        if (!solids.TryGetValue("wall", out var list) || list.Count == 0) return new ElementGeometry([], Diagnostics: ["The retaining wall could not be built."]);
        var material = e.Str("material");
        var parts = new List<GeometryPart> { new("Retaining wall", material != null ? Materials.ColorOf(m, material) : Color, Paths.Combine(list, "retaining"), material, "wall") };
        var half = (ring.Max(p => p.X) - ring.Min(p => p.X)) / 2;
        var plan = new List<PlanItem> { new("cut", true, Corridors.Outline(c, half, -half)) };
        return new ElementGeometry(parts, plan, [c.Path[0], c.Path[^1]], c.Problem is { } why ? [why] : null);
    }

    /// <summary>A drawn-profile wall's measures: its concrete, its formwork (its faces that stand, more upright than 60°), its crown.</summary>
    static IEnumerable<QuantitySet> Profiled(Entity e, ElementGeometry g, IModelView m)
    {
        var solid = g.Parts.FirstOrDefault(p => p.Key == "wall")?.Solid;
        if (solid == null) yield break;
        var concrete = Measure.Volume(solid);
        double formwork = 0, crown = 0;
        for (var f = 0; f < solid.Faces.Length; f++)
        {
            var n = solid.Faces[f].Normal;
            if (Math.Abs(n.Z) < 0.5) formwork += Measure.FaceArea(solid, f);
            else if (n.Z > 0.5) crown += Measure.FaceArea(solid, f);
        }
        var material = e.Str("material") is { } mid && m.Get(mid) is { Class: IfcClass.Material } ? mid : null;
        yield return new QuantitySet("Lapin_RetainingWallQuantities",
        [
            new("Length", Corridors.ChainOf(e).Length, "LENGTH"),
            new("ConcreteVolume", concrete, "VOLUME", "Concrete"), new("GrossVolume", concrete, "VOLUME"), new("NetVolume", concrete, "VOLUME"),
            new("FormworkArea", formwork, "AREA", "Formwork"), new("CrownArea", crown, "AREA", "Crown, footing's top too"),
        ], Material: material);
    }

    static IEnumerable<QuantitySet> Plain(Entity e, ElementGeometry g, IModelView m)
    {
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
        // Its concrete as its volume too, with the material's id (what a takeoff and the estimate read; audit 15.11.2026, M2).
        var concrete = g.Parts.Sum(p => Measure.Volume(p.Solid));
        var material = e.Str("material") is { } mid && m.Get(mid) is { Class: IfcClass.Material } ? mid : null;
        yield return new QuantitySet("Lapin_RetainingWallQuantities",
        [
            new("Length", length, "LENGTH"), new("MaxHeight", Enumerable.Range(0, c.Segments).Max(i => Stem(i) + s.FootingThickness), "LENGTH", "Height, footing included"),
            new("ConcreteVolume", concrete, "VOLUME", "Concrete"), new("GrossVolume", concrete, "VOLUME"), new("NetVolume", concrete, "VOLUME"),
            new("StemVolume", stem, "VOLUME", "Stem"), new("FootingVolume", footing, "VOLUME", "Footing"),
            new("FormworkArea", formwork, "AREA", "Formwork"), new("Joints", joints, "COUNT", "Joints"),
        ], Material: material);
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
