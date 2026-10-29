using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The standard IFC property sets a user (or the model assistant,
/// docs/plan-assistant.md) may write values into: each set with the classes it
/// applies to (and their types) and its properties with the kind of parameter
/// they are held as. The names are IFC 4's (Pset_…Common). A set that starts
/// with „Pset_” is IFC's own: only one of these, on its classes, with one of
/// its property names. Any other set is the user's own and must not start with
/// „Pset_” or „Qto_” (IFC keeps those prefixes), nor be one of Lapin's own sets.
/// </summary>
public static class StandardPsets
{
    public sealed record Set(string Name, string[] Classes, IReadOnlyDictionary<string, string> Properties);

    static Dictionary<string, string> P(params (string Name, string Kind)[] props) =>
        props.ToDictionary(p => p.Name, p => p.Kind, StringComparer.Ordinal);

    const string T = "text", B = "bool", N = "number", L = "length", A = "area", G = "angle";

    public static readonly IReadOnlyList<Set> All =
    [
        new("Pset_WallCommon", [IfcClass.Wall, "IfcCurtainWall"], P(("Reference", T), ("Status", T), ("AcousticRating", T), ("FireRating", T), ("Combustible", B), ("SurfaceSpreadOfFlame", T),
            ("ThermalTransmittance", N), ("IsExternal", B), ("ExtendToStructure", B), ("LoadBearing", B), ("Compartmentation", B))),
        new("Pset_CurtainWallCommon", ["IfcCurtainWall"], P(("Reference", T), ("Status", T), ("AcousticRating", T), ("FireRating", T), ("Combustible", B), ("SurfaceSpreadOfFlame", T),
            ("ThermalTransmittance", N), ("IsExternal", B))),
        new("Pset_SlabCommon", ["IfcSlab"], P(("Reference", T), ("Status", T), ("AcousticRating", T), ("FireRating", T), ("Combustible", B), ("SurfaceSpreadOfFlame", T),
            ("ThermalTransmittance", N), ("IsExternal", B), ("LoadBearing", B), ("PitchAngle", G), ("Compartmentation", B))),
        new("Pset_RoofCommon", ["IfcRoof"], P(("Reference", T), ("Status", T), ("AcousticRating", T), ("FireRating", T), ("ThermalTransmittance", N), ("IsExternal", B),
            ("ProjectedArea", A), ("TotalArea", A))),
        new("Pset_ColumnCommon", ["IfcColumn"], P(("Reference", T), ("Status", T), ("Slope", G), ("Roll", G), ("IsExternal", B), ("ThermalTransmittance", N), ("LoadBearing", B), ("FireRating", T))),
        new("Pset_BeamCommon", ["IfcBeam"], P(("Reference", T), ("Status", T), ("Span", L), ("Slope", G), ("Roll", G), ("IsExternal", B), ("ThermalTransmittance", N), ("LoadBearing", B), ("FireRating", T))),
        new("Pset_MemberCommon", ["IfcMember"], P(("Reference", T), ("Status", T), ("Span", L), ("Slope", G), ("Roll", G), ("IsExternal", B), ("ThermalTransmittance", N), ("LoadBearing", B), ("FireRating", T))),
        new("Pset_PlateCommon", ["IfcPlate"], P(("Reference", T), ("Status", T), ("AcousticRating", T), ("FireRating", T), ("ThermalTransmittance", N), ("IsExternal", B), ("LoadBearing", B))),
        new("Pset_FootingCommon", ["IfcFooting"], P(("Reference", T), ("Status", T), ("LoadBearing", B))),
        new("Pset_DoorCommon", ["IfcDoor"], P(("Reference", T), ("Status", T), ("FireRating", T), ("AcousticRating", T), ("SecurityRating", T), ("DurabilityRating", T),
            ("HygrothermalRating", T), ("WaterTightnessRating", T), ("MechanicalLoadRating", T), ("WindLoadRating", T), ("Infiltration", N), ("IsExternal", B),
            ("ThermalTransmittance", N), ("GlazingAreaFraction", N), ("HandicapAccessible", B), ("FireExit", B), ("HasDrive", B), ("SelfClosing", B), ("SmokeStop", B))),
        new("Pset_WindowCommon", ["IfcWindow"], P(("Reference", T), ("Status", T), ("AcousticRating", T), ("FireRating", T), ("SecurityRating", T), ("IsExternal", B),
            ("Infiltration", N), ("ThermalTransmittance", N), ("GlazingAreaFraction", N), ("HasSillExternal", B), ("HasSillInternal", B), ("HasDrive", B), ("SmokeStop", B),
            ("FireExit", B), ("WaterTightnessRating", T), ("MechanicalLoadRating", T), ("WindLoadRating", T))),
        new("Pset_CoveringCommon", ["IfcCovering"], P(("Reference", T), ("Status", T), ("AcousticRating", T), ("FlammabilityRating", T), ("FragilityRating", T), ("Combustible", B),
            ("SurfaceSpreadOfFlame", T), ("Finish", T), ("IsExternal", B), ("ThermalTransmittance", N), ("FireRating", T))),
        new("Pset_RailingCommon", ["IfcRailing"], P(("Reference", T), ("Status", T), ("Height", L), ("Diameter", L), ("IsExternal", B))),
        new("Pset_StairCommon", ["IfcStair"], P(("Reference", T), ("Status", T), ("NumberOfRiser", N), ("NumberOfTreads", N), ("RiserHeight", L), ("TreadLength", L),
            ("NosingLength", L), ("WalkingLineOffset", L), ("TreadLengthAtOffset", L), ("TreadLengthAtInnerSide", L), ("WaistThickness", L), ("RequiredHeadroom", L),
            ("HandicapAccessible", B), ("HasNonSkidSurface", B), ("IsExternal", B), ("ThermalTransmittance", N), ("LoadBearing", B), ("FireRating", T), ("FireExit", B))),
        new("Pset_RampCommon", ["IfcRamp"], P(("Reference", T), ("Status", T), ("RequiredHeadroom", L), ("RequiredSlope", G), ("HandicapAccessible", B), ("HasNonSkidSurface", B),
            ("IsExternal", B), ("ThermalTransmittance", N), ("LoadBearing", B), ("FireRating", T), ("FireExit", B))),
        new("Pset_ChimneyCommon", ["IfcChimney"], P(("Reference", T), ("Status", T), ("NumberOfDrafts", N), ("AcousticRating", T), ("FireRating", T), ("LoadBearing", B),
            ("IsExternal", B), ("ThermalTransmittance", N))),
        new("Pset_BuildingElementProxyCommon", [IfcClass.BuildingElementProxy], P(("Reference", T), ("Status", T), ("IsExternal", B), ("ThermalTransmittance", N), ("LoadBearing", B), ("FireRating", T))),
        new("Pset_SpaceCommon", [IfcClass.Space], P(("Reference", T), ("IsExternal", B), ("GrossPlannedArea", A), ("NetPlannedArea", A), ("PubliclyAccessible", B), ("HandicapAccessible", B))),
    ];

    /// <summary>Lapin's own sets, which only Lapin writes.</summary>
    public static readonly string[] Reserved = ["Lapin_Intent", "Lapin_Phase"];

    /// <summary>The set the user's parameters go to when none is named.</summary>
    public const string Own = "Lapin_Parameters";

    public static bool IsStandard(string pset) => pset.StartsWith("Pset_", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a set applies to an entity of this class (an element's, or its type's: IfcDoorType takes Pset_DoorCommon).</summary>
    public static bool AppliesTo(Set set, string ifcClass) =>
        set.Classes.Any(c => c == ifcClass || c + "Type" == ifcClass || (c == "IfcCurtainWall" && ifcClass == "IfcCurtainWallType"));

    /// <summary>
    /// The set a parameter is written to (null: the user's own Lapin_Parameters)
    /// and, for a standard set, the kind its property is held as; or why it may not be written.
    /// </summary>
    public static (string? Pset, string? Kind) Check(string ifcClass, string? pset, string name)
    {
        var p = pset?.Trim();
        if (string.IsNullOrEmpty(p) || p == Own) return (null, null);
        if (IsStandard(p))
        {
            var set = All.FirstOrDefault(s => s.Name == p)
                ?? throw new CommandException($"„{p}” is not a standard property set Lapin knows; a set of your own must not start with „Pset_”.");
            if (!AppliesTo(set, ifcClass)) throw new CommandException($"{set.Name} is not for {ifcClass}.");
            return set.Properties.TryGetValue(name, out var kind)
                ? (set.Name, kind)
                : throw new CommandException($"{set.Name} has no property „{name}” ({string.Join(", ", set.Properties.Keys)}).");
        }
        if (p.StartsWith("Qto_", StringComparison.OrdinalIgnoreCase)) throw new CommandException("„Qto_” sets are IFC's quantities: Lapin computes them.");
        if (Reserved.Contains(p, StringComparer.OrdinalIgnoreCase) || p.StartsWith("Pset_Lapin", StringComparison.OrdinalIgnoreCase))
            throw new CommandException($"„{p}” is Lapin's own set: it cannot be written by hand.");
        return (p, null);
    }

    /// <summary>The table, for the interface (the assistant checks its proposals with it).</summary>
    public static JsonObject ToJson() => new()
    {
        ["own"] = Own,
        ["reserved"] = new JsonArray(Reserved.Select(r => (JsonNode)r).ToArray()),
        ["sets"] = new JsonArray(All.Select(s => (JsonNode)new JsonObject
        {
            ["name"] = s.Name,
            ["classes"] = new JsonArray(s.Classes.Select(c => (JsonNode)c).ToArray()),
            ["properties"] = new JsonObject(s.Properties.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
        }).ToArray()),
    };
}
