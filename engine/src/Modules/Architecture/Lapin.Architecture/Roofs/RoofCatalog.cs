// Ported from BubbleBIM Pro (the user's own engine, engine/src/Modules/Building/BubbleBim.Building/Roofs), adapted to Lapin's model.
namespace Lapin.Architecture.Roofs;

/// <summary>
/// Roof coverings and roof STYLES — data, not code. A style is what a roof looks
/// like in an architectural tradition: its shape, pitch, overhang, covering,
/// gutters, snow guards. The roof configurator applies one to a roof node; the
/// architectural style generator applies the same styles (Standalone's style
/// packs, lib/style/packs.ts, whose roof parameters these are).
/// </summary>
public static class RoofCatalog
{
    /// <param name="MinPitch">Below it the covering does not shed water (a warning, not a refusal).</param>
    /// <param name="SnowGuards">Whether snow guards belong on it by default (it holds snow and sheds it in slabs).</param>
    public sealed record Covering(string Id, double Thickness, string Color, double MinPitch, bool SnowGuards);

    public static readonly IReadOnlyList<Covering> Coverings =
    [
        new("Clay tile", 0.05, "#b0563d", 22, true),
        new("Concrete tile", 0.05, "#7a5f52", 22, true),
        new("Metal sheet", 0.03, "#3c4247", 8, true),
        new("Timber shingle", 0.04, "#8a6a48", 30, false),
        new("Bitumen shingle", 0.03, "#4d4a47", 15, true),
        new("Slate", 0.04, "#4b5058", 22, true),
        new("Thatch", 0.30, "#b89c63", 40, false),
        new("Green roof", 0.20, "#6f8f4f", 1, false),
        new("Bitumen membrane", 0.02, "#3d3d3d", 1, false),
    ];

    public static Covering CoveringOf(string? id) => Coverings.FirstOrDefault(c => c.Id == id) ?? Coverings[0];

    public static readonly string[] Shapes = ["hip", "gable", "shed", "mansard", "barrel", "flat"];
    public static readonly string[] ShapeLabels = ["Hip", "Gable", "Shed", "Mansard", "Barrel", "Flat"];

    public static readonly string[] GutterProfiles = ["half_round", "box", "none"];
    public static readonly string[] GutterProfileLabels = ["Half-round", "Box", "No gutters"];
    public static readonly string[] GutterMaterials = ["galvanized", "painted", "copper", "zinc_titanium"];
    public static readonly string[] GutterMaterialLabels = ["Galvanized", "Painted", "Copper", "Zinc-titanium"];

    /// <summary>A roof style: everything a roof takes from its tradition.</summary>
    public sealed record Style(
        string Id, string Label, string Family, string Region,
        string Shape, double Pitch, double Overhang, string Covering,
        string GutterProfile = "half_round", string GutterMaterial = "galvanized", string GutterColor = "",
        double GableOverhang = 0.3, bool? SnowGuards = null, string Description = "", double UpperPitch = 30, double MansardInset = 1.0,
        /// <summary>A barrel vault's rise at the crown, as a share of its span.</summary>
        double BarrelRise = 0.3);

    /// <summary>The houses' styles of Standalone (packs.ts): their roof parameters, base hip 35° / 600 mm / ceramic tile where a pack keeps the base.</summary>
    public static readonly IReadOnlyList<Style> Styles =
    [
        new("traditional_campie", "Plains traditional", "traditional", "Romania", "hip", 28, 0.6, "Clay tile", Description: "A low hip roof, wide eaves over the porch."),
        new("traditional_maramures", "Maramureș traditional", "traditional", "Romania", "hip", 50, 0.7, "Timber shingle", GutterMaterial: "painted", GutterColor: "#4A3426", Description: "A tall, steep shingle roof that dominates the house."),
        new("traditional_bucovina", "Bukovina traditional", "traditional", "Romania", "hip", 42, 0.7, "Timber shingle", GutterMaterial: "painted", GutterColor: "#4A3426"),
        new("traditional_bucovina_modern", "Bukovina modern", "traditional", "Romania", "hip", 42, 0.6, "Bitumen shingle", GutterMaterial: "painted", GutterColor: "#2E3236"),
        new("traditional_dobrogea", "Dobruja traditional", "traditional", "Romania", "hip", 25, 0.5, "Clay tile", Description: "A low pitch on a low, sunny house."),
        new("traditional_moldova", "Moldavian traditional", "traditional", "Romania", "hip", 40, 0.8, "Timber shingle"),
        new("traditional_basarabia", "Bessarabian traditional", "traditional", "Romania", "hip", 32, 0.6, "Metal sheet"),
        new("ro_saxon", "Saxon house", "traditional", "Romania", "gable", 45, 0.3, "Clay tile", Description: "A gable to the street, a steep pitch."),
        new("ro_neoromanesc", "Neo-Romanian", "traditional", "Romania", "hip", 40, 0.9, "Clay tile", Description: "Very wide eaves on brackets."),
        new("modern_minimalist", "Minimalist", "modern", "International", "flat", 2, 0, "Bitumen membrane", "none"),
        new("modern_barn", "Barn house", "modern", "International", "gable", 42, 0, "Metal sheet", "box", "painted", "#2E3236", GableOverhang: 0, Description: "No eaves: a clean volume, standing-seam metal."),
        new("modern_barrel", "Barrel vault", "modern", "International", "barrel", 30, 0.15, "Metal sheet", "box", "painted", "#2E3236", Description: "A curved roof between two gable ends, standing-seam metal, rising 0.3 of its span; the eaves go on round the curve a little past the walls."),
        new("modern_monopitch", "Contemporary mono-pitch", "modern", "International", "shed", 10, 0.5, "Metal sheet", "box", "painted", "#2E3236"),
        new("nordic_modern", "Nordic modern", "modern", "Scandinavia", "gable", 35, 0.15, "Metal sheet", "box", "painted", "#25282B", GableOverhang: 0.1),
        new("de_bauhaus", "Bauhaus", "modern", "Germany", "flat", 2, 0, "Bitumen membrane", "none"),
        new("us_prairie", "Prairie", "modern", "USA", "hip", 16, 1.5, "Clay tile", Description: "A low pitch, deep eaves, horizontal lines."),
        new("classic_neoclassical", "Neoclassical villa", "classic", "Europa", "hip", 26, 0.5, "Metal sheet", GutterMaterial: "copper"),
        new("classic_mansard", "French mansard", "classic", "France", "mansard", 70, 0.2, "Slate", GutterMaterial: "zinc_titanium", UpperPitch: 25, MansardInset: 0.9, Description: "A near-vertical lower slope with a whole storey under it; a low pitch above."),
        new("de_stadtvilla", "Stadtvilla", "classic", "Germany", "hip", 22, 0.8, "Clay tile", GutterMaterial: "painted", GutterColor: "#3B3F44"),
        new("en_georgian", "Georgian", "classic", "England", "hip", 30, 0.3, "Slate", GutterMaterial: "painted", GutterColor: "#2B2D30"),
        new("med_tuscan", "Tuscan", "traditional", "Italy", "hip", 22, 0.5, "Clay tile"),
        new("fr_provence", "Provence", "traditional", "France", "gable", 24, 0.3, "Clay tile"),
        new("dk_parcelhus", "Danish parcelhus", "traditional", "Denmark", "gable", 30, 0.35, "Slate", GutterMaterial: "zinc_titanium"),
        new("dk_thatched", "Danish thatched house", "traditional", "Denmark", "hip", 48, 0.45, "Thatch", "none"),
        new("dk_murermester", "Danish 1930s villa", "traditional", "Denmark", "hip", 45, 0.3, "Clay tile", GutterMaterial: "zinc_titanium"),
        new("se_falu_red", "Scandinavian red house", "traditional", "Sweden", "gable", 38, 0.4, "Metal sheet", GutterMaterial: "painted", GutterColor: "#25282B"),
        new("no_white_house", "Norwegian white house", "traditional", "Norway", "gable", 40, 0.4, "Slate", GutterMaterial: "painted", GutterColor: "#F2F2EE"),
        new("no_turf_cabin", "Norwegian turf cabin", "traditional", "Norway", "gable", 27, 0.5, "Green roof", "none"),
        new("alpine_chalet", "Alpine chalet", "traditional", "Alps", "gable", 24, 1.2, "Metal sheet", GutterMaterial: "painted", GutterColor: "#3A2E26", GableOverhang: 1.0, SnowGuards: true, Description: "A low pitch, large overhangs all round, snow guards."),
        new("de_fachwerk", "Fachwerk", "traditional", "Germany", "gable", 50, 0.4, "Clay tile"),
        new("en_tudor", "Tudor", "traditional", "England", "gable", 50, 0.35, "Slate", GutterMaterial: "painted", GutterColor: "#2B2D30"),
        new("us_modern_farmhouse", "Modern farmhouse", "traditional", "USA", "gable", 40, 0.3, "Metal sheet", GutterMaterial: "painted", GutterColor: "#1F2124"),
    ];

    public static Style? StyleOf(string? id) => Styles.FirstOrDefault(s => s.Id == id);
}
