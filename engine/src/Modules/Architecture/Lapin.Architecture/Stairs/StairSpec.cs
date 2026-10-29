using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Architecture.Stairs;

/// <summary>
/// The rules a stair is checked against: a set for dwellings and one for
/// public buildings (STAS 2965, NP 063, P118), editable on the type. Metres;
/// the step rule is 2h + b. A third set is for the steps outside an entrance:
/// lower risers, deeper treads, and a single riser allowed (a porch's step).
/// </summary>
public sealed record StairRules(
    string Set, double RiserMin, double RiserMax, double RiserTarget, double TreadMin, double TreadMax, double TreadTarget,
    double StepMin, double StepMax, double MinWidth, int MaxRisers, double Headroom, double MaxSlope, int MinRisers = 3)
{
    public static readonly StairRules Residential = new("residential", 0.15, 0.18, 0.17, 0.26, 0.32, 0.28, 0.60, 0.64, 0.90, 18, 2.00, 0.08);
    public static readonly StairRules Public = new("public", 0.13, 0.16, 0.16, 0.28, 0.34, 0.30, 0.60, 0.64, 1.20, 16, 2.10, 0.06);
    public static readonly StairRules Entrance = new("entrance", 0.12, 0.17, 0.15, 0.30, 0.40, 0.33, 0.60, 0.70, 1.20, 10, 2.10, 0.06, MinRisers: 1);
    public static StairRules Of(string? set) => set switch { "public" => Public, "entrance" => Entrance, _ => Residential };
}

/// <summary>
/// What the engineer sees: the construction. Monolithic concrete, either
/// <c>rc</c> (a waist slab under the steps, slabs for the landings) or
/// <c>solid</c> (the steps filled down to the stair's base, as entrance steps
/// cast on the ground are).
/// </summary>
public sealed record StairConstruction(string Kind, double Waist, double LandingThickness, string Concrete, double? LandingBeamWidth = null, double? LandingBeamDepth = null);

/// <summary>What the architect sees: the finish on the treads (the riser is measured on the finished surfaces, what people walk on) and the nosing.</summary>
public sealed record StairFinish(double TreadThickness, string TreadMaterial, double Nosing);

/// <summary>The railing the type proposes: on which sides (left and right as one climbs the first flight), its height over the nosing line, bar and post sizes, post spacing.</summary>
public sealed record StairRailing(string Sides, double Height, double BarSize, double PostSize, double PostSpacing);

/// <summary>
/// A stair type as the configurator edits it: the shape (a straight run, a
/// straight run with a landing, an L or a U, the turn made as a landing or as
/// winders), the rules, the construction, the finish and the railing. The
/// instance adds where it stands, how high it climbs and what it fixes.
/// </summary>
public sealed record StairSpec(
    string Kind,
    string Name,
    string Shape,
    string Turn,
    int Winders,
    double Width,
    double Gap,
    double LandingLength,
    StairRules Rules,
    StairConstruction Construction,
    StairFinish Finish,
    StairRailing Railing,
    double Slope = 0.06,
    double? Rise = null)
{
    public static readonly string[] Shapes = ["straight", "straight-landing", "l", "u"];
    public static readonly string[] Constructions = ["rc", "solid"];
    public static readonly string[] Turns = ["landing", "winders"];
    public static readonly string[] Sides = ["none", "left", "right", "both"];

    public static StairSpec Default(string name = "Concrete stair 100") => new(
        "stair", name, "straight", "landing", 3, 1.0, 0, 0,
        StairRules.Residential,
        new StairConstruction("rc", 0.15, 0.15, "Reinforced concrete"),
        new StairFinish(0.03, "Stone", 0.02),
        new StairRailing("left", 0.9, 0.04, 0.03, 1.2));

    /// <summary>
    /// Entrance steps: the few steps from the ground up to a porch or a door,
    /// wide, solid concrete on the ground, with a rise of their own (three
    /// risers of 150 mm) instead of a storey's, and no railing.
    /// </summary>
    public static StairSpec Entrance(string name = "Entrance steps") => new(
        "stair", name, "straight", "landing", 3, 2.0, 0, 0,
        StairRules.Entrance,
        new StairConstruction("solid", 0.15, 0.15, "Concrete"),
        new StairFinish(0.03, "Stone", 0.02),
        new StairRailing("none", 0.9, 0.04, 0.03, 1.2),
        Rise: 0.45);

    /// <summary>The stair types Lapin offers ready made, by key; a project gets its copy the first time one is asked for.</summary>
    public static readonly IReadOnlyDictionary<string, Func<StairSpec>> Library = new Dictionary<string, Func<StairSpec>>
    {
        ["concrete"] = () => Default(),
        ["entrance"] = () => Entrance(),
    };

    /// <summary>The IFC predefined type of the shape, as the turn is made.</summary>
    public string PredefinedType => Shape switch
    {
        "straight" => "STRAIGHT_RUN_STAIR",
        "straight-landing" => "TWO_STRAIGHT_RUN_STAIR",
        "l" => Turn == "winders" ? "QUARTER_WINDING_STAIR" : "QUARTER_TURN_STAIR",
        "u" => Turn == "winders" ? "HALF_WINDING_STAIR" : "HALF_TURN_STAIR",
        _ => "NOTDEFINED",
    };

    public bool Turns_ => Shape is "l" or "u";

    public JsonObject ToJson() => (JsonObject)JsonSerializer.SerializeToNode(this, ArchitectureJson.Default.StairSpec)!;

    /// <summary>A stored spec read back and made sane; null when unreadable.</summary>
    public static StairSpec? From(JsonNode? node)
    {
        try
        {
            var s = node?.Deserialize(ArchitectureJson.Default.StairSpec);
            if (s is null || s.Name is null || s.Rules is null || s.Construction is null || s.Finish is null || s.Railing is null) return null;
            if (!Shapes.Contains(s.Shape)) s = s with { Shape = "straight" };
            if (!Turns.Contains(s.Turn)) s = s with { Turn = "landing" };
            if (!Sides.Contains(s.Railing.Sides)) s = s with { Railing = s.Railing with { Sides = "none" } };
            if (!Constructions.Contains(s.Construction.Kind)) s = s with { Construction = s.Construction with { Kind = "rc" } };
            if (s.Rules.MinRisers < 1) s = s with { Rules = s.Rules with { MinRisers = 1 } };
            return s with
            {
                Kind = s.Kind == "ramp" ? "ramp" : "stair",
                Winders = Math.Clamp(s.Winders, 1, 6),
                Width = Math.Clamp(s.Width, 0.5, 5),
                Gap = Math.Clamp(s.Gap, 0, 2),
                LandingLength = Math.Max(0, s.LandingLength),
                Rise = s.Rise is { } rise && rise > 0 ? Math.Clamp(rise, 0.05, 10) : null,
            };
        }
        catch (JsonException) { return null; }
    }
}
