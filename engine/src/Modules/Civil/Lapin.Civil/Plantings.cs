using System.Text.Json;
using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>A species as the planting draws and counts it: a tree (trunk and crown), a bush (a low crown) or grass (ground cover).</summary>
public sealed record Species(string Name, string Type, double Height, double Crown, string Color, double Share = 1);

/// <summary>
/// Planting: one plant at a point, or a zone sown with a mix of species at a
/// density. The plants stand on the ground (the terrain, else the storey),
/// placed by a seeded random so the same zone always grows the same way,
/// drawn stylised and counted by species. A Lapin class exported as
/// IfcGeographicElement VEGETATION.
/// </summary>
public static class Plantings
{
    public const string Class = "LapinPlanting";

    public static readonly IReadOnlyList<Species> Catalog =
    [
        new("Lime (Tilia)", "tree", 9, 6, "#4f8a3a"),
        new("Birch (Betula)", "tree", 11, 4.5, "#7fb35a"),
        new("Maple (Acer)", "tree", 10, 7, "#5a9440"),
        new("Pine (Pinus)", "tree", 10, 4, "#3f6b3a"),
        new("Fruit tree", "tree", 4.5, 4, "#6ca04e"),
        new("Thuja", "bush", 2.5, 1.0, "#2f6b45"),
        new("Lilac (Syringa)", "bush", 3, 2.5, "#5c8a4e"),
        new("Boxwood (Buxus)", "bush", 0.8, 0.8, "#3d7a3f"),
        new("Lawn", "grass", 0.05, 0.3, "#7fb04f"),
    ];

    public static IReadOnlyList<Species> DefaultMix =>
        [Catalog[0] with { Share = 0.4 }, Catalog[1] with { Share = 0.3 }, Catalog[6] with { Share = 0.3 }];

    public static List<Species> SpeciesOf(Entity e) =>
        (e.Attrs["species"] as JsonArray)?.Select(Parse).OfType<Species>().ToList() is { Count: > 0 } list ? list : [.. DefaultMix];

    public static Species? Parse(JsonNode? n)
    {
        if (n is not JsonObject o || ((string?)o["name"])?.Trim() is not { Length: > 0 } name) return null;
        var type = (string?)o["type"] is "bush" or "grass" ? (string)o["type"]! : "tree";
        var known = Catalog.FirstOrDefault(c => c.Name == name);
        return new Species(name, type, Math.Max(0.05, Json.Number(o["height"]) ?? known?.Height ?? 5), Math.Max(0.1, Json.Number(o["crown"]) ?? known?.Crown ?? 3),
            (string?)o["color"] ?? known?.Color ?? "#5a9440", Math.Max(0, Json.Number(o["share"]) ?? 1));
    }

    public static JsonArray SpeciesJson(IEnumerable<Species> species) =>
        new(species.Select(s => (JsonNode)JsonSerializer.SerializeToNode(s, new JsonSerializerOptions(JsonSerializerDefaults.Web))!).ToArray());

    public static bool IsZone(Entity e) => e.Attrs["contour"] is JsonArray;

    public sealed record Plant(Species Species, Vec2 At, double Z, double Scale);

    static Terrain? TerrainFor(IModelView m, Entity e) =>
        m.OfClass(IfcClass.GeographicElement).FirstOrDefault(t => t.PredefinedType == Terrains.Predefined && Spatial.StoreyOf(m, t.Id)?.Id == Spatial.StoreyOf(m, e.Id)?.Id) is { } te ? Terrains.Of(te) : null;

    /// <summary>The plants: one at the point, or as many as the density gives over the zone's area, scattered by the seed, each on the ground.</summary>
    public static List<Plant> Plants(IModelView m, Entity e)
    {
        var species = SpeciesOf(e);
        var terrain = TerrainFor(m, e);
        var fallback = Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;
        double Ground(Vec2 p) => terrain?.ElevationAt(p.X, p.Y) ?? fallback;
        var rng = new SiteGeometry.Random((int)Math.Round(e.Num("seed", 1)));
        var o = new List<Plant>();
        if (!IsZone(e))
        {
            var at = Args.Point(e.Attrs["at"], "the plant");
            o.Add(new Plant(species[0], at, Ground(at), 1));
            return o;
        }
        var contour = Terrains.Contour(e);
        if (contour.Count < 3) return o;
        var area = Math.Abs(Polygon2.SignedArea(contour));
        var count = (int)Math.Round(e.Num("density", 3) * area / 100);
        var total = species.Sum(s => s.Share);
        double x0 = contour.Min(p => p.X), x1 = contour.Max(p => p.X), y0 = contour.Min(p => p.Y), y1 = contour.Max(p => p.Y);
        var tries = 0;
        while (o.Count < count && tries++ < count * 50 + 100)
        {
            var p = new Vec2(x0 + rng.Next() * (x1 - x0), y0 + rng.Next() * (y1 - y0));
            if (!Polygon2.Contains(contour, p)) continue;
            var pick = rng.Next() * total;
            var s = species[^1];
            foreach (var sp in species) { pick -= sp.Share; if (pick <= 0) { s = sp; break; } }
            if (o.Any(q => q.Species.Type != "grass" && s.Type != "grass" && (q.At - p).Length < Math.Min(q.Species.Crown, s.Crown) * 0.4)) continue;
            o.Add(new Plant(s, p, Ground(p), 0.8 + rng.Next() * 0.4));
        }
        return o;
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var plants = Plants(m, e);
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        var species = SpeciesOf(e);
        for (var i = 0; i < species.Count; i++)
        {
            var sp = species[i];
            var solids = new List<Solid>();
            foreach (var p in plants.Where(p => p.Species.Name == sp.Name))
            {
                var h = sp.Height * p.Scale; var r = sp.Crown / 2 * p.Scale;
                switch (sp.Type)
                {
                    case "tree":
                        if (Prism.Cylinder(p.At, p.Z, Math.Max(0.06, r * 0.08), h * 0.4, 8, "trunk") is { } trunk) solids.Add(trunk);
                        if (Prism.Cylinder(p.At, p.Z + h * 0.35, r, h * 0.45, 10, "crown") is { } crown) solids.Add(crown);
                        if (Prism.Cylinder(p.At, p.Z + h * 0.8, r * 0.6, h * 0.2, 8, "crown") is { } top) solids.Add(top);
                        plan.Add(new PlanItem("symbol", true, Circle(p.At, r)));
                        break;
                    case "bush":
                        if (Prism.Cylinder(p.At, p.Z, r, h, 10, "bush") is { } bush) solids.Add(bush);
                        plan.Add(new PlanItem("symbol", true, Circle(p.At, r, 8)));
                        break;
                    default:
                        if (Prism.Cylinder(p.At, p.Z, r, Math.Max(0.02, h), 6, "grass") is { } tuft) solids.Add(tuft);
                        break;
                }
            }
            if (solids.Count > 0) parts.Add(new GeometryPart(sp.Name, sp.Color, Paths.Combine(solids, "planting"), null, $"species:{i}"));
        }
        if (IsZone(e)) plan.Add(new PlanItem("symbol", true, Terrains.Contour(e)));
        if (parts.Count == 0) return new ElementGeometry([], plan, null, ["No plant fits: enlarge the zone or raise the density."]);
        return new ElementGeometry(parts, plan, IsZone(e) ? Terrains.Contour(e) : [plants[0].At]);
    }

    static Vec2[] Circle(Vec2 c, double r, int n = 12) => [.. Enumerable.Range(0, n).Select(k => c + new Vec2(Math.Cos(2 * Math.PI * k / n), Math.Sin(2 * Math.PI * k / n)) * r)];

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null) yield break;
        var plants = Plants(m, e);
        var species = SpeciesOf(e);
        var area = IsZone(e) ? Math.Abs(Polygon2.SignedArea(Terrains.Contour(e))) : 0;
        var own = new List<Quantity> { new("Count", plants.Count, "COUNT", "Plants") };
        if (area > 0) own.Add(new("Area", area, "AREA", "Planted area"));
        yield return new QuantitySet("Lapin_PlantingQuantities", own);
        for (var i = 0; i < species.Count; i++)
        {
            var sp = species[i];
            var n = plants.Count(p => p.Species.Name == sp.Name);
            if (n == 0) continue;
            yield return new QuantitySet("Lapin_PlantingQuantities", [new("Count", n, "COUNT"), new("Height", sp.Height, "LENGTH"), new("Crown", sp.Crown, "LENGTH")], $"species:{i}", sp.Name);
        }
    }
}
