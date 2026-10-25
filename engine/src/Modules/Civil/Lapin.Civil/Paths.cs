using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A path, an alley or a simple road: a carriageway along a polyline (one
/// strip, or two lanes with a crown), its crossfall, a wearing course over a
/// base course, and on each side, outwards, a gutter, a kerb and a sidewalk
/// when asked. Its red line follows the ground under its axis or a designed
/// profile (<see cref="Corridors"/>); its corners are mitred, so the courses
/// run on unbroken except at joints. The terrain it voids is cut by it.
/// IfcPavement (IFC4X3; a slab named Pavement in IFC4).
/// </summary>
public static class Paths
{
    public const string Color = "#b8ad9c";
    const double GutterSlab = 0.12;
    const double SidewalkFall = 0.02;

    public static IReadOnlyList<Vec2> Path(Entity e) => Terrains.Contour(e, "path");

    public sealed record Courses(double Width, double Surface, double Base, bool Kerb, double KerbHeight, double KerbWidth);

    public static Courses Of(Entity e) => new(
        Math.Max(0.2, e.Num("width", 1.5)), Math.Max(0, e.Num("surfaceThickness", 0.06)), Math.Max(0, e.Num("baseThickness", 0.2)),
        e.Bool("kerb"), Math.Max(0.02, e.Num("kerbHeight", 0.1)), Math.Max(0.02, e.Num("kerbWidth", 0.1)));

    /// <summary>What stands at one edge, outwards: a gutter (a shallow channel), a kerb, a sidewalk behind it.</summary>
    public sealed record Side(bool Gutter, bool Kerb, bool Sidewalk, double GutterWidth, double GutterDepth, double KerbWidth, double KerbHeight, double KerbDepth, double SidewalkWidth, double SidewalkThickness)
    {
        public double Extent => (Gutter ? GutterWidth : 0) + (Kerb || Sidewalk ? KerbWidth : 0) + (Sidewalk ? SidewalkWidth : 0);
    }

    /// <summary>
    /// The cross-section: an alley (one strip of <see cref="Width"/>) or a road
    /// (two lanes); the crossfall (ratio) falling to the right, to the left or
    /// from a crown on the axis; the courses; the two sides.
    /// </summary>
    public sealed record Template(string Kind, double Width, double Crossfall, string Fall, double Surface, double Base, Side L, Side R)
    {
        public double Half => Width / 2;
        public double Left => Half + L.Extent;
        public double Right => Half + R.Extent;
    }

    static Side SideOf(Entity e, string key, Courses c)
    {
        var o = e.Attrs[key] as JsonObject;
        double N(string k, double d) => Json.Number(o?[k]) ?? d;
        bool B(string k, bool d) => o?[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : d;
        // Old paths: one "kerb" switch for both sides, sunk as deep as the surface course.
        return new(B("gutter", false), B("kerb", o == null && c.Kerb), B("sidewalk", false),
            Math.Max(0.1, N("gutterWidth", 0.5)), Math.Max(0, N("gutterDepth", 0.05)),
            Math.Max(0.02, N("kerbWidth", o == null ? c.KerbWidth : 0.15)), Math.Max(0, N("kerbHeight", o == null ? c.KerbHeight : 0.12)), Math.Max(0, N("kerbDepth", o == null ? c.Surface : 0.15)),
            Math.Max(0.3, N("sidewalkWidth", 1.5)), Math.Max(0.02, N("sidewalkThickness", 0.1)));
    }

    public static Template TemplateOf(Entity e)
    {
        var c = Of(e);
        var road = e.Str("template") == "road";
        var width = road ? 2 * Math.Max(1, e.Num("laneWidth", 3)) : c.Width;
        var fall = e.Str("fall") is "crown" or "left" or "right" ? e.Str("fall")! : road ? "crown" : "right";
        return new(road ? "road" : "alley", width, Math.Clamp(e.Num("crossfall", road ? 2.5 : 0), 0, 20) / 100, fall, c.Surface, c.Base, SideOf(e, "left", c), SideOf(e, "right", c));
    }

    /// <summary>The cross-section at a station whose red line (the carriageway's top on the axis) is <paramref name="z0"/>: its parts as (offset, elevation) polygons.</summary>
    public static List<SectionPart> SectionAt(Template t, double z0)
    {
        var h = t.Half;
        double Top(double o) => t.Fall switch { "crown" => z0 - t.Crossfall * Math.Abs(o), "left" => z0 - t.Crossfall * o, _ => z0 + t.Crossfall * o };
        var across = t.Fall == "crown" ? new[] { -h, 0, h } : [-h, h];
        List<Vec2> Strip(double down, double thick) =>
            [.. across.Select(o => new Vec2(o, Top(o) - down)), .. across.Reverse().Select(o => new Vec2(o, Top(o) - down - thick))];
        var parts = new List<SectionPart>();
        if (t.Surface > 0) parts.Add(new("surface", Strip(0, t.Surface)));
        if (t.Base > 0) parts.Add(new("base", Strip(t.Surface, t.Base)));
        foreach (var (side, sign) in new[] { (t.L, 1.0), (t.R, -1.0) })
        {
            var e = sign * h;
            var z = Top(e);
            if (side.Gutter)
            {
                var (x1, mid) = (e + sign * side.GutterWidth, e + sign * side.GutterWidth / 2);
                var bottom = z - side.GutterDepth - GutterSlab;
                parts.Add(new("gutter", [new(e, z), new(mid, z - side.GutterDepth), new(x1, z), new(x1, bottom), new(e, bottom)]));
                e = x1;
            }
            if (side.Kerb || side.Sidewalk)
            {
                var x1 = e + sign * side.KerbWidth;
                parts.Add(new("kerb", [new(e, z + side.KerbHeight), new(x1, z + side.KerbHeight), new(x1, z - side.KerbDepth), new(e, z - side.KerbDepth)]));
                e = x1;
                z += side.KerbHeight;
            }
            if (side.Sidewalk)
            {
                var x1 = e + sign * side.SidewalkWidth;
                // Draining to the road: the far edge a little higher.
                var z1 = z + SidewalkFall * side.SidewalkWidth;
                parts.Add(new("sidewalk", [new(e, z), new(x1, z1), new(x1, z1 - side.SidewalkThickness), new(e, z - side.SidewalkThickness)]));
            }
        }
        return parts;
    }

    /// <summary>The courses and the side pieces as solids, by part key (surface, base, gutter, kerb, sidewalk).</summary>
    public static Dictionary<string, List<Solid>> Solids(IModelView m, Entity e)
    {
        var c = Corridors.ChainOf(e);
        if (c.Segments == 0) return [];
        var t = TemplateOf(e);
        var red = Corridors.RedLine(m, e, c);
        return Corridors.Sweep(c, Corridors.Runs(c, CorridorSpec.Of(e)), (s, _) => SectionAt(t, red(s)), 2.0);
    }

    static readonly (string Key, string Name, string Color)[] PartNames =
        [("surface", "Surface course", Color), ("base", "Base course", "#a39888"), ("gutter", "Gutters", "#9a9a9a"), ("kerb", "Kerbs", "#8f8f8f"), ("sidewalk", "Sidewalks", "#c3bdb2")];

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var c = Corridors.ChainOf(e);
        if (c.Segments == 0) return new ElementGeometry([], Diagnostics: ["The path needs two points at least."]);
        var solids = Solids(m, e);
        var parts = new List<GeometryPart>();
        var sm = e.Str("surfaceMaterial"); var bm = e.Str("baseMaterial");
        foreach (var (key, name, color) in PartNames)
        {
            if (!solids.TryGetValue(key, out var list) || list.Count == 0) continue;
            var material = key == "surface" ? sm : key == "base" ? bm : null;
            parts.Add(new GeometryPart(name, material != null ? Materials.ColorOf(m, material) : color, Combine(list, key), material, key));
        }
        var t = TemplateOf(e);
        var plan = new List<PlanItem> { new("heavy", true, Corridors.Outline(c, t.Left, -t.Right)), new("symbol", false, c.Path) };
        // The carriageway's edges inside the outline, where something stands beside it.
        if (t.L.Extent > 0) plan.Add(new("symbol", false, OffsetLine(c, t.Half)));
        if (t.R.Extent > 0) plan.Add(new("symbol", false, OffsetLine(c, -t.Half)));
        var spec = CorridorSpec.Of(e);
        foreach (var j in Corridors.Joints(c, spec))
        {
            var (a, b) = Corridors.LineAcross(c, j, t.Left, -t.Right);
            plan.Add(new("symbol", false, [a, b]));
        }
        return new ElementGeometry(parts, plan, [c.Path[0], c.Path[^1]]);
    }

    static List<Vec2> OffsetLine(Chain c, double offset) =>
        c.Path.Select((p, i) => p + c.Across(c.StationOf(i), Math.Min(i, c.Segments - 1)) * offset).ToList();

    /// <summary>Several solids as one part (a path's segments, a planting's trees).</summary>
    public static Solid Combine(IReadOnlyList<Solid> solids, string tag)
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

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null) yield break;
        var c = Corridors.ChainOf(e);
        var t = TemplateOf(e);
        var length = c.Length;
        double Vol(string key) => g.Parts.Where(p => p.Key == key).Sum(p => Measure.Volume(p.Solid));
        var sides = new[] { t.L, t.R };
        yield return new QuantitySet("Lapin_PavementQuantities",
        [
            new("Length", length, "LENGTH"), new("Width", t.Width, "LENGTH"), new("GrossArea", length * t.Width, "AREA", "Paved area"),
            new("SurfaceVolume", Vol("surface"), "VOLUME", "Surface course"), new("BaseVolume", Vol("base"), "VOLUME", "Base course"),
            new("KerbLength", sides.Count(s => s.Kerb || s.Sidewalk) * length, "LENGTH", "Kerbs"),
            new("GutterLength", sides.Count(s => s.Gutter) * length, "LENGTH", "Gutters"),
            new("SidewalkArea", sides.Where(s => s.Sidewalk).Sum(s => s.SidewalkWidth) * length, "AREA", "Sidewalks"),
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

    /// <summary>A side as stored: only what was given, merged over what the side had.</summary>
    public static JsonObject MergeSide(JsonNode? had, JsonNode? given)
    {
        var o = had is JsonObject h ? (JsonObject)h.DeepClone() : new JsonObject();
        if (given is JsonObject g)
            foreach (var (k, v) in g)
            {
                if (k is "gutter" or "kerb" or "sidewalk") { if (v is JsonValue b && b.TryGetValue<bool>(out var on)) o[k] = on; }
                else if (k is "gutterWidth" or "gutterDepth" or "kerbWidth" or "kerbHeight" or "kerbDepth" or "sidewalkWidth" or "sidewalkThickness")
                {
                    if (Json.Number(v) is { } n && double.IsFinite(n)) o[k] = Math.Max(0, n);
                }
            }
        return o;
    }
}
