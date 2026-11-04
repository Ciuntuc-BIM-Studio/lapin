using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Architecture.Stairs;

/// <summary>
/// What fills the space under a stair (a tester's report, #71): the furnishing, not where it stands. The front on the open
/// side (<see cref="Front"/>: cupboard doors of about <see cref="DoorWidth"/>, a closed panel, or left open) with shelves
/// behind it; a bookcase at the high end; one feature (<see cref="Feature"/>: a desk with a shelf and a light, a bench with
/// cushions, a mudroom bench with cubbies under it and coat hooks over it, a toddler's playhouse door with a window, a dog's
/// bed behind a framed opening); the board's thickness and the materials of the carcass, the fronts and the fabric. Metres.
/// </summary>
public sealed record UnderStairSpec(
    string Name,
    string Front = "doors",
    double DoorWidth = 0.5,
    bool Shelves = true,
    double ShelfSpacing = 0.35,
    bool Bookcase = false,
    double BookcaseWidth = 0.5,
    string Feature = "none",
    double FeatureWidth = 1.2,
    double BenchHeight = 0.45,
    bool Cushion = true,
    double DeskHeight = 0.75,
    bool Light = false,
    double HookHeight = 1.65,
    double HookSpacing = 0.25,
    double OpeningWidth = 0.6,
    double OpeningHeight = 1.1,
    bool Window = false,
    double Thickness = 0.018,
    string Carcass = "Oak plywood",
    string Fronts = "Painted MDF",
    string Fabric = "Upholstery fabric")
{
    public static readonly string[] FrontKinds = ["doors", "panel", "open"];
    public static readonly string[] Features = ["none", "desk", "bench", "mudroom", "playhouse", "dogbed"];

    /// <summary>The colours the furnishing's materials take when the project does not have them yet.</summary>
    public static readonly IReadOnlyDictionary<string, string> Colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Oak plywood"] = "#c99a5b", ["Painted MDF"] = "#a3ae8c", ["Upholstery fabric"] = "#ddd0b8", ["Birch plywood"] = "#dcc39a", ["White lacquer"] = "#efece6",
    };

    /// <summary>The ready-made styles the configurator offers, after the tester's picture: each a front, a bookcase and one feature.</summary>
    public static readonly (string Id, string Description, UnderStairSpec Spec)[] Presets =
    [
        ("storage", "Cupboard doors on the open side, shelves behind them, open shelves at the high end",
            new("Storage cupboard", Front: "doors", Shelves: true, Bookcase: true, BookcaseWidth: 0.5)),
        ("office", "A desk at the high end with a shelf and a light over it, cupboards in the low part",
            new("Closet office", Front: "doors", Feature: "desk", FeatureWidth: 1.1, Light: true)),
        ("reading", "A bench with a cushion and back cushions, a bookcase at the high end, a reading light",
            new("Reading nook", Front: "panel", Shelves: false, Bookcase: true, BookcaseWidth: 0.5, Feature: "bench", FeatureWidth: 1.4, Light: true)),
        ("mudroom", "A bench with cubbies for shoes under it and coat hooks over it, the low part closed",
            new("Entry and mudroom", Front: "panel", Shelves: false, Feature: "mudroom", FeatureWidth: 1.4)),
        ("playhouse", "A closed front with a small door and a window, a play mat inside",
            new("Toddler playhouse", Front: "panel", Shelves: false, Feature: "playhouse", FeatureWidth: 1.2, OpeningWidth: 0.6, OpeningHeight: 1.1, Window: true)),
        ("dogbed", "A closed front with a framed opening and a dog's bed behind it",
            new("Dog bed house", Front: "panel", Shelves: false, Feature: "dogbed", FeatureWidth: 1.1, OpeningWidth: 0.7, OpeningHeight: 0.65)),
    ];

    public static UnderStairSpec Default() => Presets[0].Spec;

    public static UnderStairSpec? Preset(string? id) => Presets.FirstOrDefault(p => p.Id == id).Spec;

    public JsonObject ToJson() => (JsonObject)JsonSerializer.SerializeToNode(this, ArchitectureJson.Default.UnderStairSpec)!;

    /// <summary>A stored or given spec read back and made sane (sizes within reason, kinds known); null when unreadable.</summary>
    public static UnderStairSpec? From(JsonNode? node)
    {
        UnderStairSpec? s;
        try { s = node?.Deserialize(ArchitectureJson.Default.UnderStairSpec); }
        catch (JsonException) { return null; }
        if (s is null) return null;
        static string Name(string? v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
        return s with
        {
            Name = Name(s.Name, "Under-stair space"),
            Front = FrontKinds.Contains(s.Front) ? s.Front : "doors",
            Feature = Features.Contains(s.Feature) ? s.Feature : "none",
            DoorWidth = Math.Clamp(s.DoorWidth, 0.25, 1.2),
            ShelfSpacing = Math.Clamp(s.ShelfSpacing, 0.15, 1.0),
            BookcaseWidth = Math.Clamp(s.BookcaseWidth, 0.25, 1.5),
            FeatureWidth = Math.Clamp(s.FeatureWidth, 0.4, 4),
            BenchHeight = Math.Clamp(s.BenchHeight, 0.25, 0.7),
            DeskHeight = Math.Clamp(s.DeskHeight, 0.5, 1.1),
            HookHeight = Math.Clamp(s.HookHeight, 0.8, 2.2),
            HookSpacing = Math.Clamp(s.HookSpacing, 0.1, 1),
            OpeningWidth = Math.Clamp(s.OpeningWidth, 0.3, 1.5),
            OpeningHeight = Math.Clamp(s.OpeningHeight, 0.3, 2.2),
            Thickness = Math.Clamp(s.Thickness, 0.008, 0.05),
            Carcass = Name(s.Carcass, "Oak plywood"),
            Fronts = Name(s.Fronts, "Painted MDF"),
            Fabric = Name(s.Fabric, "Upholstery fabric"),
        };
    }

    public IEnumerable<string> Materials() => new[] { Carcass, Fronts, Fabric }.Distinct(StringComparer.OrdinalIgnoreCase);
}
