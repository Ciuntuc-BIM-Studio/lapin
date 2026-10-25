using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A path, an alley, a paved way: a strip along a polyline, level across,
/// its surface at the ground along the centre line, a wearing course over a
/// base course, kerbs along the edges when asked. The terrain it voids is
/// cut by its courses, so the paving sits in the ground. IfcPavement (IFC4X3;
/// a slab named Pavement in IFC4).
/// </summary>
public static class Paths
{
    public const string Color = "#b8ad9c";

    public static IReadOnlyList<Vec2> Path(Entity e) => Terrains.Contour(e, "path");

    public sealed record Courses(double Width, double Surface, double Base, bool Kerb, double KerbHeight, double KerbWidth);

    public static Courses Of(Entity e) => new(
        Math.Max(0.2, e.Num("width", 1.5)), Math.Max(0, e.Num("surfaceThickness", 0.06)), Math.Max(0, e.Num("baseThickness", 0.2)),
        e.Bool("kerb"), Math.Max(0.02, e.Num("kerbHeight", 0.1)), Math.Max(0.02, e.Num("kerbWidth", 0.1)));

    /// <summary>The terrain the path lies in: the one it voids, else the first on its storey.</summary>
    public static Entity? TerrainEntity(IModelView m, Entity e) =>
        Terrains.TerrainOf(m, e.Id) ?? m.OfClass(IfcClass.GeographicElement).FirstOrDefault(t => t.PredefinedType == Terrains.Predefined && Spatial.StoreyOf(m, t.Id)?.Id == Spatial.StoreyOf(m, e.Id)?.Id);

    /// <summary>The ground along the path: the terrain's height at each point, else the storey's elevation.</summary>
    public static List<Vec3> Profile(IModelView m, Entity e, IReadOnlyList<Vec2> path)
    {
        var terrain = TerrainEntity(m, e) is { } te ? Terrains.Of(te) : null;
        var fallback = Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;
        double? last = null;
        var o = new List<Vec3>();
        foreach (var p in path)
        {
            var z = terrain?.ElevationAt(p.X, p.Y) ?? last ?? fallback;
            last = z;
            o.Add(p.At(z));
        }
        return o;
    }

    /// <summary>The courses as solids: the surface and the base, each lofted segment by segment; the kerbs as boxes along the edges.</summary>
    public static (List<Solid> Surface, List<Solid> Base, List<Solid> Kerbs) Solids(IModelView m, Entity e)
    {
        var path = SiteGeometry.Densify(Path(e), 2.0);
        var c = Of(e);
        var surface = new List<Solid>(); var @base = new List<Solid>(); var kerbs = new List<Solid>();
        if (path.Count < 2) return (surface, @base, kerbs);
        var profile = Profile(m, e, path);
        var dirs = SiteGeometry.Directions(path);
        List<Vec3> Ring(int i, double zTop, double zBottom, double half)
        {
            var n = dirs[i].Perp;
            var p = path[i];
            return [(p - n * half).At(profile[i].Z + zTop), (p + n * half).At(profile[i].Z + zTop), (p + n * half).At(profile[i].Z + zBottom), (p - n * half).At(profile[i].Z + zBottom)];
        }
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var half = c.Width / 2;
            if (c.Surface > 0 && SiteGeometry.Loft(Ring(i, 0, -c.Surface, half), Ring(i + 1, 0, -c.Surface, half), "surface") is { } s) surface.Add(s);
            if (c.Base > 0 && SiteGeometry.Loft(Ring(i, -c.Surface, -c.Surface - c.Base, half), Ring(i + 1, -c.Surface, -c.Surface - c.Base, half), "base") is { } b) @base.Add(b);
            if (c.Kerb)
                foreach (var side in new[] { -1.0, 1.0 })
                {
                    List<Vec3> K(int k)
                    {
                        var n = dirs[k].Perp * side;
                        var p = path[k];
                        var z = profile[k].Z;
                        return [(p + n * half).At(z + c.KerbHeight), (p + n * (half + c.KerbWidth)).At(z + c.KerbHeight), (p + n * (half + c.KerbWidth)).At(z - c.Surface), (p + n * half).At(z - c.Surface)];
                    }
                    if (SiteGeometry.Loft(K(i), K(i + 1), "kerb") is { } k) kerbs.Add(k);
                }
        }
        return (surface, @base, kerbs);
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var path = Path(e);
        if (path.Count < 2) return new ElementGeometry([], Diagnostics: ["The path needs two points at least."]);
        var (surface, @base, kerbs) = Solids(m, e);
        var parts = new List<GeometryPart>();
        var sm = e.Str("surfaceMaterial"); var bm = e.Str("baseMaterial");
        if (surface.Count > 0) parts.Add(new GeometryPart("Surface course", sm != null ? Materials.ColorOf(m, sm) : Color, Openings_Combine(surface, "surface"), sm, "surface"));
        if (@base.Count > 0) parts.Add(new GeometryPart("Base course", bm != null ? Materials.ColorOf(m, bm) : "#a39888", Openings_Combine(@base, "base"), bm, "base"));
        if (kerbs.Count > 0) parts.Add(new GeometryPart("Kerbs", "#8f8f8f", Openings_Combine(kerbs, "kerb"), null, "kerb"));
        var c = Of(e);
        var dirs = SiteGeometry.Directions(path);
        var half = c.Width / 2 + (c.Kerb ? c.KerbWidth : 0);
        var left = path.Select((p, i) => p + dirs[i].Perp * half).ToList();
        var right = path.Select((p, i) => p - dirs[i].Perp * half).Reverse().ToList();
        var plan = new List<PlanItem> { new("heavy", true, [.. left, .. right]), new("symbol", false, path) };
        return new ElementGeometry(parts, plan, [path[0], path[^1]]);
    }

    static Solid Openings_Combine(IReadOnlyList<Solid> solids, string tag)
    {
        if (solids.Count == 1) return solids[0];
        var vertices = new List<Vec3>();
        var faces = new List<Face>();
        foreach (var s in solids)
        {
            var at = vertices.Count;
            vertices.AddRange(s.Vertices);
            faces.AddRange(s.Faces.Select(f => f with { Outer = f.Outer.Select(i => i + at).ToArray(), Holes = f.Holes.Select(h => h.Select(i => i + at).ToArray()).ToArray() }));
        }
        return new Solid([.. vertices], [.. faces], tag);
    }

    /// <summary>Several solids as one part (a path's segments, a planting's trees).</summary>
    public static Solid Combine(IReadOnlyList<Solid> solids, string tag) => Openings_Combine(solids, tag);

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null) yield break;
        var path = Path(e);
        var c = Of(e);
        var length = SiteGeometry.Length(path);
        double Vol(string key) => g.Parts.Where(p => p.Key == key).Sum(p => Measure.Volume(p.Solid));
        yield return new QuantitySet("Lapin_PavementQuantities",
        [
            new("Length", length, "LENGTH"), new("Width", c.Width, "LENGTH"), new("GrossArea", length * c.Width, "AREA", "Paved area"),
            new("SurfaceVolume", Vol("surface"), "VOLUME", "Surface course"), new("BaseVolume", Vol("base"), "VOLUME", "Base course"),
            new("KerbLength", c.Kerb ? 2 * length : 0, "LENGTH", "Kerbs"),
        ]);
    }

    public static JsonObject Attrs(IReadOnlyList<Vec2> path, Courses c, string? surfaceMaterial, string? baseMaterial)
    {
        var o = new JsonObject
        {
            ["path"] = Terrains.ContourJson(path), ["width"] = c.Width, ["surfaceThickness"] = c.Surface, ["baseThickness"] = c.Base,
            ["kerb"] = c.Kerb, ["kerbHeight"] = c.KerbHeight, ["kerbWidth"] = c.KerbWidth,
        };
        if (surfaceMaterial != null) o["surfaceMaterial"] = surfaceMaterial;
        if (baseMaterial != null) o["baseMaterial"] = baseMaterial;
        return o;
    }
}
