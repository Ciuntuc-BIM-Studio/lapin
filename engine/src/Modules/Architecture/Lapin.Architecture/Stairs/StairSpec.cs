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
    /// <summary>A ramp's (decision 252): what counts is the slope (at most 8 %), the width (1.20 m) and the headroom; the steps' bounds are not used.</summary>
    public static readonly StairRules Ramp = Public with { Set = "ramp", MinWidth = 1.20, MaxSlope = 0.08 };
    public static StairRules Of(string? set) => set switch { "public" => Public, "entrance" => Entrance, "ramp" => Ramp, _ => Residential };
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

/// <summary>
/// The railing the type proposes: on which sides (left and right as one climbs the first flight), its
/// height over the nosing line, bar and post sizes, post spacing; and its design (report #45, decision
/// 189): <see cref="Style"/> "bar" (posts and a handrail), "balusters" (upright bars at most
/// <see cref="BalusterSpacing"/> apart under the handrail), "glass" (panes between the posts), "cable"
/// (taut cables along the slope), "panel" (a solid balustrade); the handrail's <see cref="Profile"/>
/// "square" or "round"; how far in from the stair's edge (<see cref="Inset"/>); how far the handrail runs
/// on level past the first and last steps (<see cref="Extension"/>); its colour.
/// </summary>
public sealed record StairRailing(string Sides, double Height, double BarSize, double PostSize, double PostSpacing,
    string Style = "bar", string Profile = "square", double BalusterSpacing = 0.11, double Inset = 0.05, double Extension = 0, string? Color = null);

/// <summary>
/// What a plan writes on the stair (report #46): each step's number in a circle, the label of its riser, tread and width by
/// its foot, "UP" at the foot of the arrow; and how it draws the stair where the plan's cut crosses it (report #143):
/// <see cref="AboveCut"/> "dashed" (over the cut in the overhead style), "hidden" (not drawn) or "whole" (no cut, the
/// stair entire), <see cref="Break"/> "zigzag" or "double" (<see cref="StairPlan"/>).
/// </summary>
public sealed record StairPlanSymbols(bool Numbers = false, bool Label = false, bool Up = true, string AboveCut = "dashed", string Break = "zigzag");

/// <summary>
/// A stair type as the configurator edits it: the shape (a straight run, a
/// straight run with a landing, an L or a U, the turn made as a landing or as
/// winders), the rules, the construction, the finish and the railing. The
/// instance adds where it stands, how high it climbs and what it fixes.
/// <see cref="Kind"/> "ramp" (decision 252) makes it a ramp's type: it climbs
/// by <see cref="Slope"/> instead of risers, in flights of at most
/// <see cref="MaxRun"/> with landings of <see cref="LandingLength"/> between.
/// A rise of its own (<see cref="Rise"/>) is climbed from below the storey's
/// level up to it (<see cref="RiseFrom"/> "below": entrance steps, an entrance
/// ramp) or from the level up (<c>"level"</c>: to a terrace, a loading dock).
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
    double? Rise = null,
    /// <summary>A spiral's or curved stair's inner radius (a spiral's round its pole), metres.</summary>
    double InnerRadius = 0.1,
    /// <summary>How far round a spiral or curved stair turns, in degrees; null: as the risers and the tread make it.</summary>
    double? Sweep = null,
    /// <summary>The width of a split stair's two return flights; null: 60 % of the main flight's, never under the rules' least.</summary>
    double? SideWidth = null,
    /// <summary>What the plan writes on it; null: the arrow's "UP" alone.</summary>
    StairPlanSymbols? Symbols = null,
    /// <summary>Where a rise of its own starts: "below" the storey's level, ending at it, or at the "level", climbing from it.</summary>
    string RiseFrom = "below",
    /// <summary>A ramp's longest flight in plan, metres, before a landing; 0: no limit.</summary>
    double MaxRun = 9.0,
    /// <summary>A U turned by winders (report #109): the winders at its second corner; null, as many as at the first (<see cref="Winders"/>).</summary>
    int? WindersEnd = null,
    /// <summary>
    /// A U turned by winders (report #109): the straight treads along its short side between the two corners' winders. With
    /// some (or a different count at each corner) each corner turns on its own, the well between the flights as wide as these
    /// treads; 0 with the same count at both, the winders fan round the whole half turn, as before.
    /// </summary>
    int Between = 0)
{
    public static readonly string[] Shapes = ["straight", "straight-landing", "l", "u", "spiral", "curved", "split"];
    /// <summary>The shapes a ramp takes: straight, straight with landings, an L, a switchback.</summary>
    public static readonly string[] RampShapes = ["straight", "straight-landing", "l", "u"];
    public static readonly string[] RiseAnchors = ["below", "level"];
    public static readonly string[] Constructions = ["rc", "solid", "treads", "stringers"];
    public static readonly string[] RailingStyles = ["bar", "balusters", "glass", "cable", "panel"];
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

    /// <summary>
    /// A ramp (decision 252): 1.20 m wide at 6 %, flights of at most 9 m with landings of 1.50 m between,
    /// a slab of reinforced concrete under a finish, handrails on both sides; up to the storey above.
    /// </summary>
    public static StairSpec Ramp(string name = "Ramp 6 %") => new(
        "ramp", name, "straight-landing", "landing", 3, 1.2, 0.1, 1.5,
        StairRules.Ramp,
        new StairConstruction("rc", 0.2, 0.2, "Reinforced concrete"),
        new StairFinish(0.03, "Stone", 0),
        new StairRailing("both", 0.9, 0.04, 0.03, 1.2, Extension: 0.3),
        Slope: 0.06);

    /// <summary>The stair types Lapin offers ready made, by key; a project gets its copy the first time one is asked for.</summary>
    public static readonly IReadOnlyDictionary<string, Func<StairSpec>> Library = new Dictionary<string, Func<StairSpec>>
    {
        ["concrete"] = () => Default(),
        ["entrance"] = () => Entrance(),
        // Report #45: the shapes asked for, each a type to start from.
        ["dogleg"] = () => Default("Dog-leg stair") with { Shape = "u", Gap = 0 },
        ["winder"] = () => Default("Winder stair") with { Shape = "l", Turn = "winders", Winders = 3 },
        // Report #109: a U with two winders in each corner and a straight tread between them.
        ["winder-u"] = () => Default("U stair with winders") with { Shape = "u", Turn = "winders", Winders = 2, WindersEnd = 2, Between = 1, Width = 0.9 },
        ["spiral"] = () => Default("Spiral stair") with
        {
            Shape = "spiral", Width = 0.8, InnerRadius = 0.12,
            Construction = new StairConstruction("treads", 0.05, 0.05, "Steel"),
            Finish = new StairFinish(0, "Oak", 0.02),
            Railing = new StairRailing("right", 0.9, 0.04, 0.02, 1.2, "balusters", "round"),
        },
        ["curved"] = () => Default("Curved stair") with { Shape = "curved", InnerRadius = 1.5, Width = 1.2, Railing = new StairRailing("both", 0.9, 0.05, 0.03, 1.2, "glass", "round") },
        ["cantilever"] = () => Default("Cantilever stair") with
        {
            Construction = new StairConstruction("treads", 0.08, 0.15, "Oak"),
            Finish = new StairFinish(0, "Oak", 0),
            Railing = new StairRailing("right", 0.9, 0.04, 0.03, 1.2, "glass", "round"),
        },
        ["split"] = () => Default("Split stair") with { Shape = "split", Width = 1.6, Gap = 0.1, Railing = new StairRailing("both", 0.9, 0.05, 0.04, 1.2, "balusters", "round") },
        ["ramp"] = () => Ramp(),
        ["switchback-ramp"] = () => Ramp("Switchback ramp") with { Shape = "u", MaxRun = 6 },
        // An entrance ramp (report #53): from the ground up to the storey's level, solid, as the entrance steps are.
        ["entrance-ramp"] = () => Ramp("Entrance ramp") with { Shape = "straight", Rise = 0.45, Construction = new StairConstruction("solid", 0.2, 0.2, "Concrete") },
    };

    public bool IsRamp => Kind == "ramp";

    /// <summary>The IFC class of the type and of its elements: IfcStairType and IfcStair, or IfcRampType and IfcRamp.</summary>
    public string TypeClass => IsRamp ? IfcClass.RampType : IfcClass.StairType;
    public string ElementClass => IsRamp ? IfcClass.Ramp : IfcClass.Stair;

    /// <summary>The IFC predefined type of the shape, as the turn is made.</summary>
    public string PredefinedType => IsRamp ? Shape switch
    {
        "straight" => "STRAIGHT_RUN_RAMP",
        "straight-landing" => "TWO_STRAIGHT_RUN_RAMP",
        "l" => "QUARTER_TURN_RAMP",
        "u" => "HALF_TURN_RAMP",
        _ => "NOTDEFINED",
    } : Shape switch
    {
        "straight" => "STRAIGHT_RUN_STAIR",
        "straight-landing" => "TWO_STRAIGHT_RUN_STAIR",
        "l" => Turn == "winders" ? "QUARTER_WINDING_STAIR" : "QUARTER_TURN_STAIR",
        "u" => Turn == "winders" ? "HALF_WINDING_STAIR" : "HALF_TURN_STAIR",
        "spiral" => "SPIRAL_STAIR",
        "curved" => "CURVED_RUN_STAIR",
        "split" => "DOUBLE_RETURN_STAIR",
        _ => "NOTDEFINED",
    };

    public bool Turns_ => Shape is "l" or "u";
    /// <summary>A spiral or curved stair: its steps fan round a centre, not along flights.</summary>
    public bool Round => Shape is "spiral" or "curved";

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
            if (s.Railing.Style is null || !RailingStyles.Contains(s.Railing.Style)) s = s with { Railing = s.Railing with { Style = "bar" } };
            if (s.Railing.Profile is not ("square" or "round")) s = s with { Railing = s.Railing with { Profile = "square" } };
            if (!Constructions.Contains(s.Construction.Kind)) s = s with { Construction = s.Construction with { Kind = "rc" } };
            if (s.Rules.MinRisers < 1) s = s with { Rules = s.Rules with { MinRisers = 1 } };
            if (s.Symbols is { } sym && (!StairPlan.AboveModes.Contains(sym.AboveCut) || !StairPlan.BreakStyles.Contains(sym.Break)))
                s = s with { Symbols = sym with { AboveCut = StairPlan.AboveModes.Contains(sym.AboveCut) ? sym.AboveCut : "dashed", Break = StairPlan.BreakStyles.Contains(sym.Break) ? sym.Break : "zigzag" } };
            if (s.Kind == "ramp" && !RampShapes.Contains(s.Shape)) s = s with { Shape = "straight-landing" };
            if (s.Kind == "ramp" && s.Construction.Kind is not ("rc" or "solid")) s = s with { Construction = s.Construction with { Kind = "rc" } };
            return s with
            {
                Kind = s.Kind == "ramp" ? "ramp" : "stair",
                RiseFrom = RiseAnchors.Contains(s.RiseFrom) ? s.RiseFrom : "below",
                MaxRun = Math.Clamp(s.MaxRun, 0, 100),
                Slope = Math.Clamp(s.Slope, 0.005, 0.5),
                Winders = Math.Clamp(s.Winders, 1, 6),
                WindersEnd = s.WindersEnd is { } we ? Math.Clamp(we, 1, 6) : null,
                Between = Math.Clamp(s.Between, 0, 12),
                Width = Math.Clamp(s.Width, 0.5, 5),
                Gap = Math.Clamp(s.Gap, 0, 2),
                LandingLength = Math.Max(0, s.LandingLength),
                Rise = s.Rise is { } rise && rise > 0 ? Math.Clamp(rise, 0.05, 10) : null,
                InnerRadius = Math.Clamp(s.InnerRadius, 0.03, 20),
                Sweep = s.Sweep is { } sw && sw > 0 ? Math.Clamp(sw, 10, 1440) : null,
                SideWidth = s.SideWidth is { } sd && sd > 0 ? Math.Clamp(sd, 0.5, 5) : null,
            };
        }
        catch (JsonException) { return null; }
    }
}
