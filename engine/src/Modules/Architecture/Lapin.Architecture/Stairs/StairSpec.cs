using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Architecture.Stairs;

/// <summary>
/// The rules a stair is checked against: a set for dwellings and one for
/// public buildings (STAS 2965, NP 063, P118), editable on the type. Metres;
/// the step rule is 2h + b.
/// </summary>
public sealed record StairRules(
    string Set, double RiserMin, double RiserMax, double RiserTarget, double TreadMin, double TreadMax, double TreadTarget,
    double StepMin, double StepMax, double MinWidth, int MaxRisers, double Headroom, double MaxSlope)
{
    public static readonly StairRules Residential = new("residential", 0.15, 0.18, 0.17, 0.26, 0.32, 0.28, 0.60, 0.64, 0.90, 18, 2.00, 0.08);
    public static readonly StairRules Public = new("public", 0.13, 0.16, 0.16, 0.28, 0.34, 0.30, 0.60, 0.64, 1.20, 16, 2.10, 0.06);
    public static StairRules Of(string? set) => set == "public" ? Public : Residential;
}

/// <summary>What the engineer sees: the construction. Stage 1: monolithic reinforced concrete, a waist slab under the steps and slabs for the landings.</summary>
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
    double Slope = 0.06)
{
    public static readonly string[] Shapes = ["straight", "straight-landing", "l", "u"];
    public static readonly string[] Turns = ["landing", "winders"];
    public static readonly string[] Sides = ["none", "left", "right", "both"];

    public static StairSpec Default(string name = "Concrete stair 100") => new(
        "stair", name, "straight", "landing", 3, 1.0, 0, 0,
        StairRules.Residential,
        new StairConstruction("rc", 0.15, 0.15, "Reinforced concrete"),
        new StairFinish(0.03, "Stone", 0.02),
        new StairRailing("left", 0.9, 0.04, 0.03, 1.2));

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

    public JsonObject ToJson() => (JsonObject)JsonSerializer.SerializeToNode(this, Json.Options)!;

    /// <summary>A stored spec read back and made sane; null when unreadable.</summary>
    public static StairSpec? From(JsonNode? node)
    {
        try
        {
            var s = node?.Deserialize<StairSpec>(Json.Options);
            if (s is null || s.Name is null || s.Rules is null || s.Construction is null || s.Finish is null || s.Railing is null) return null;
            if (!Shapes.Contains(s.Shape)) s = s with { Shape = "straight" };
            if (!Turns.Contains(s.Turn)) s = s with { Turn = "landing" };
            if (!Sides.Contains(s.Railing.Sides)) s = s with { Railing = s.Railing with { Sides = "none" } };
            return s with
            {
                Kind = s.Kind == "ramp" ? "ramp" : "stair",
                Winders = Math.Clamp(s.Winders, 1, 6),
                Width = Math.Clamp(s.Width, 0.5, 5),
                Gap = Math.Clamp(s.Gap, 0, 2),
                LandingLength = Math.Max(0, s.LandingLength),
            };
        }
        catch (JsonException) { return null; }
    }
}
