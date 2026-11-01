using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A species as the planting draws and counts it: a tree (trunk and crown), a bush (a low crown) or grass (ground cover);
/// its Latin and Romanian names, and the family its plan symbol and body are drawn by (<see cref="PlantSymbols"/>).
/// </summary>
public sealed record Species(string Name, string Type, double Height, double Crown, string Color, double Share = 1, string? Latin = null, string? Ro = null, string? Family = null)
{
    /// <summary>The family it draws by: its own, else its type's.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string DrawnAs => Family is { Length: > 0 } f && PlantSymbols.Families.Contains(f) ? f : PlantSymbols.DefaultFamily(Type);
}

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

    /// <summary>
    /// The library (decision 228): the first nine are the old generic ones, kept by name so old projects resolve;
    /// then the fruit trees, conifers and shrubs native or traditional in Romania, each at its typical mature size.
    /// </summary>
    public static readonly IReadOnlyList<Species> Catalog =
    [
        new("Lime (Tilia)", "tree", 9, 6, "#4f8a3a", Latin: "Tilia cordata", Ro: "Tei", Family: "broadleaf"),
        new("Birch (Betula)", "tree", 11, 4.5, "#7fb35a", Latin: "Betula pendula", Ro: "Mesteacăn", Family: "broadleaf"),
        new("Maple (Acer)", "tree", 10, 7, "#5a9440", Latin: "Acer platanoides", Ro: "Arțar", Family: "broadleaf"),
        new("Pine (Pinus)", "tree", 10, 4, "#3f6b3a", Latin: "Pinus", Ro: "Pin", Family: "pine"),
        new("Fruit tree", "tree", 4.5, 4, "#6ca04e", Ro: "Pom fructifer", Family: "fruit"),
        new("Thuja", "bush", 2.5, 1.0, "#2f6b45", Latin: "Thuja occidentalis", Ro: "Tuia", Family: "hedge"),
        new("Lilac (Syringa)", "bush", 3, 2.5, "#5c8a4e", Latin: "Syringa vulgaris", Ro: "Liliac", Family: "shrub"),
        new("Boxwood (Buxus)", "bush", 0.8, 0.8, "#3d7a3f", Latin: "Buxus sempervirens", Ro: "Cimișir", Family: "hedge"),
        new("Lawn", "grass", 0.05, 0.3, "#7fb04f", Ro: "Gazon", Family: "ground"),
        // Fruit trees.
        new("Apple", "tree", 5, 5, "#6aa24c", Latin: "Malus domestica", Ro: "Măr", Family: "fruit"),
        new("Pear", "tree", 8, 5, "#5f9a48", Latin: "Pyrus communis", Ro: "Păr", Family: "fruit"),
        new("Plum", "tree", 6, 5, "#5b8f4a", Latin: "Prunus domestica", Ro: "Prun", Family: "fruit"),
        new("Sweet cherry", "tree", 10, 7, "#64a050", Latin: "Prunus avium", Ro: "Cireș", Family: "fruit"),
        new("Sour cherry", "tree", 4, 4, "#6aa856", Latin: "Prunus cerasus", Ro: "Vișin", Family: "fruit"),
        new("Apricot", "tree", 6, 5, "#72a852", Latin: "Prunus armeniaca", Ro: "Cais", Family: "fruit"),
        new("Walnut", "tree", 15, 12, "#4d8238", Latin: "Juglans regia", Ro: "Nuc", Family: "broadleaf"),
        new("Quince", "tree", 4, 4, "#7aa85a", Latin: "Cydonia oblonga", Ro: "Gutui", Family: "fruit"),
        // Conifers.
        new("Silver fir", "tree", 30, 7, "#2f5e3a", Latin: "Abies alba", Ro: "Brad", Family: "fir"),
        new("Norway spruce", "tree", 30, 7, "#2b5835", Latin: "Picea abies", Ro: "Molid", Family: "fir"),
        new("Scots pine", "tree", 20, 7, "#3f6b3a", Latin: "Pinus sylvestris", Ro: "Pin silvestru", Family: "pine"),
        new("Black pine", "tree", 20, 8, "#33573a", Latin: "Pinus nigra", Ro: "Pin negru", Family: "pine"),
        // Shrubs.
        new("Hazel", "bush", 4, 4, "#5f9448", Latin: "Corylus avellana", Ro: "Alun", Family: "shrub"),
        new("Cornelian cherry", "bush", 5, 4, "#62954a", Latin: "Cornus mas", Ro: "Corn", Family: "shrub"),
        new("Elder", "bush", 4, 4, "#5a8c46", Latin: "Sambucus nigra", Ro: "Soc", Family: "shrub"),
        new("Dog rose", "bush", 2.5, 2.5, "#6b9a4e", Latin: "Rosa canina", Ro: "Măceș", Family: "shrub"),
        new("Sea buckthorn", "bush", 3, 3, "#8aa86a", Latin: "Hippophae rhamnoides", Ro: "Cătină", Family: "shrub"),
        new("Redcurrant", "bush", 1.5, 1.5, "#6a9a50", Latin: "Ribes rubrum", Ro: "Coacăz", Family: "shrub"),
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
        // The names and the family from the planting's own copy, else from the library by name (an old project's species).
        string? Own(string key) => ((string?)o[key])?.Trim() is { Length: > 0 } v ? v : null;
        return new Species(name, type, Math.Max(0.05, Json.Number(o["height"]) ?? known?.Height ?? 5), Math.Max(0.1, Json.Number(o["crown"]) ?? known?.Crown ?? 3),
            (string?)o["color"] ?? known?.Color ?? "#5a9440", Math.Max(0, Json.Number(o["share"]) ?? 1),
            Own("latin") ?? known?.Latin, Own("ro") ?? known?.Ro, Own("family") ?? known?.Family);
    }

    public static JsonArray SpeciesJson(IEnumerable<Species> species) =>
        new(species.Select(s => (JsonNode)JsonSerializer.SerializeToNode(s, CivilJson.Default.Species)!).ToArray());

    public static bool IsZone(Entity e) => e.Attrs["contour"] is JsonArray;

    /// <summary>A plant: its species, where it stands and how big against the species', and the seed its symbol is drawn from.</summary>
    public sealed record Plant(Species Species, Vec2 At, double Z, double Scale, int Seed = 1);

    /// <summary>A plant's seed from the zone's and its place in it (apart from the scatter's, so old zones grow where they did).</summary>
    static int SeedOf(int zone, int index) => unchecked(zone * 7919 + index * 104729 + 17);

    static int StableHash(string s) { unchecked { var h = 2166136261u; foreach (var c in s) h = (h ^ c) * 16777619u; return (int)h; } }

    static Terrain? TerrainFor(IModelView m, Entity e) =>
        m.OfClass(IfcClass.GeographicElement).FirstOrDefault(t => t.PredefinedType == Terrains.Predefined && Spatial.StoreyOf(m, t.Id)?.Id == Spatial.StoreyOf(m, e.Id)?.Id) is { } te ? Terrains.Of(te) : null;

    /// <summary>The plants: one at the point, or as many as the density gives over the zone's area, scattered by the seed, each on the ground.</summary>
    public static List<Plant> Plants(IModelView m, Entity e)
    {
        var species = SpeciesOf(e);
        var terrain = TerrainFor(m, e);
        var fallback = Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;
        double Ground(Vec2 p) => terrain?.ElevationAt(p.X, p.Y) ?? fallback;
        var seed = (int)Math.Round(e.Num("seed", 1));
        var rng = new SiteGeometry.Random(seed);
        var o = new List<Plant>();
        if (!IsZone(e))
        {
            var at = Args.Point(e.Attrs["at"], "the plant");
            // A plant on its own: its seed, else its id's, so two plants side by side differ.
            o.Add(new Plant(species[0], at, Ground(at), 1, e.Attrs["seed"] != null ? SeedOf(seed, 0) : SeedOf(StableHash(e.Id), 0)));
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
            o.Add(new Plant(s, p, Ground(p), 0.8 + rng.Next() * 0.4, SeedOf(seed, o.Count)));
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
                solids.AddRange(Body(sp, p.At, p.Z, h, r));
                plan.AddRange(Symbol(sp.DrawnAs, p.At, r, p.Seed));
            }
            if (solids.Count > 0) parts.Add(new GeometryPart(sp.Name, sp.Color, Paths.Combine(solids, "planting"), null, $"species:{i}"));
        }
        if (IsZone(e)) plan.Add(new PlanItem("symbol", true, Terrains.Contour(e)));
        if (parts.Count == 0) return new ElementGeometry([], plan, null, ["No plant fits: enlarge the zone or raise the density."]);
        return new ElementGeometry(parts, plan, IsZone(e) ? Terrains.Contour(e) : [plants[0].At]);
    }

    /// <summary>
    /// A plant's plan symbol at its place (<see cref="PlantSymbols"/>): its outline at every scale, its branches, needles and
    /// core only from a site plan's scale up (1:1000 draws the crowns alone); the fills black, as the lines print.
    /// </summary>
    public static IEnumerable<PlanItem> Symbol(string family, Vec2 at, double r, int seed) =>
        PlantSymbols.Symbol(family, r, seed).Select(s => new PlanItem(s.Fill ? "fill" : "symbol", s.Closed || s.Fill, [.. s.Points.Select(q => q + at)],
            s.Detail ? PlanDetail.Medium : PlanDetail.Coarse, PlanDetail.Fine, s.Fill ? new Lapin.Drawing.LineStyleSpec(Color: "#000000") : null));

    /// <summary>
    /// A plant's body, low-poly (a zone may hold hundreds): a fir or spruce a narrow cone over a short trunk, a pine a
    /// rounded crown high on a long trunk, a fruit or broadleaf tree a rounded crown, a bush a low dome, grass a tuft.
    /// </summary>
    public static List<Solid> Body(Species sp, Vec2 at, double z, double h, double r)
    {
        var o = new List<Solid>();
        void Add(Solid? s) { if (s != null) o.Add(s); }
        var trunk = Math.Max(0.06, r * 0.08);
        switch (sp.Type)
        {
            case "tree":
                switch (sp.DrawnAs)
                {
                    case "fir":
                        Add(Prism.Cylinder(at, z, trunk, h * 0.15, 6, "trunk"));
                        Add(Lathe(at, z, [(h * 0.1, r * 0.55), (h * 0.16, r), (h * 0.55, r * 0.55), (h, r * 0.03)], 8, "crown"));
                        break;
                    case "pine":
                        Add(Prism.Cylinder(at, z, trunk, h * 0.62, 6, "trunk"));
                        Add(Lathe(at, z, [(h * 0.55, r * 0.35), (h * 0.66, r * 0.9), (h * 0.8, r), (h * 0.93, r * 0.7), (h, r * 0.15)], 8, "crown"));
                        break;
                    default:
                        // A rounded crown on a trunk: the fruit trees low, the broadleaves from a third of their height.
                        var c0 = h * (sp.DrawnAs == "fruit" ? 0.3 : 0.35);
                        Add(Prism.Cylinder(at, z, trunk, c0 + (h - c0) * 0.25, 6, "trunk"));
                        Add(Dome(at, z + c0, h - c0, r, 8, "crown"));
                        break;
                }
                break;
            case "bush":
                Add(Dome(at, z, h, r, 8, "bush"));
                break;
            default:
                Add(Prism.Cylinder(at, z, r, Math.Max(0.02, h), 6, "grass"));
                break;
        }
        return o;
    }

    /// <summary>A solid of revolution about the plant's axis: rings at (height over the ground, radius), bottom to top.</summary>
    static Solid? Lathe(Vec2 at, double z, IReadOnlyList<(double Z, double R)> profile, int sides, string tag) =>
        Lofts.Rings([.. profile.Select(p => (IReadOnlyList<Vec3>)[.. Enumerable.Range(0, sides).Select(k =>
            new Vec3(at.X + Math.Max(0.01, p.R) * Math.Cos(2 * Math.PI * k / sides), at.Y + Math.Max(0.01, p.R) * Math.Sin(2 * Math.PI * k / sides), z + p.Z))])], tag);

    /// <summary>An ellipsoid-like crown from <paramref name="z"/> up <paramref name="height"/>, <paramref name="r"/> wide: a lathe of six rings, its underside flattened where it sits.</summary>
    static Solid? Dome(Vec2 at, double z, double height, double r, int sides, string tag) =>
        Lathe(at, z, [.. new[] { 0, 0.2, 0.45, 0.7, 0.88, 1.0 }.Select(t => (height * t, r * Math.Max(t == 0 ? 0.4 : 0.03, Math.Sqrt(Math.Max(0, 1 - Math.Pow(2 * t - 1, 2))))))], sides, tag);

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
