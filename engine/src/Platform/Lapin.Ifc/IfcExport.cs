using Lapin.Alignments;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using static Lapin.Ifc.Step;

namespace Lapin.Ifc;

/// <summary>
/// What an export writes: the schema (IFC4, read everywhere; IFC4X3 for the
/// terrain and the platforms as 4.3 names them), the detail ("group": an
/// element with parts of its own, zones and lintels, roof planes and timber,
/// is a parent over elements of their own; "simple": one body per element),
/// a phase (what exists at its end; null: the whole project) and how bodies
/// are written: "brep" (a faceted B-rep, a face entity per face, as always) or
/// "faceset" (the same faces as one IfcTriangulatedFaceSet or
/// IfcPolygonalFaceSet over a shared point list, a much smaller file; coplanar
/// triangles merged into one face), and a tolerance (metres; 0: exact) within
/// which a body's triangles are simplified (<see cref="Simplifier"/>).
/// </summary>
/// <summary><paramref name="Exclude"/>: elements left out (the kinds and storeys not chosen, what the 3D view hides: <see cref="ModelSurvey.Excluded"/>).</summary>
/// <summary><paramref name="Voids"/>: the openings also as solid voids (IfcBuildingElementProxy PROVISIONFORVOID, decision 289), of the kinds asked, their sides moved by the offset; off by default.</summary>
public sealed record IfcExportOptions(string Schema = "IFC4", string Detail = "group", string? Phase = null, string Geometry = "brep", double Tolerance = 0, IReadOnlySet<string>? Exclude = null,
    bool Voids = false, IReadOnlySet<string>? VoidKinds = null, double VoidOffset = 0, bool BonsaiStyles = false, bool Structural = false, bool TypePropertiesOnOccurrences = false, bool SpaceBrep = false,
    bool AdvancedBrep = false, bool DerivedLoadBearing = false, bool RoofSlopes = false)
{
    // RoofSlopes (issue #139): a sloped roof's covering (its deck, its layers) one IfcSlab ROOF per slope (each a part of the IfcRoof,
    // its GlobalId from the roof's and the slope's), for readers that fit one plane to a slab (CYPECAD MEP); a roof written over its
    // parts then even with one body each. Off: the covering one slab of all its slopes, as decisions 328 and 329 have it.
    // DerivedLoadBearing (docs/plan-structural-usage.md, the user's answer 4): Pset_…Common.LoadBearing also on the elements nothing
    // says it of, from the views' rule (StructuralUsage.ForViews: a wall unless a partition, floors, roofs, columns, beams); off by
    // default, on for a CDE issue and an IDS check. Off: written only where it is said (the Structural tick, a Pset value), and on
    // columns, beams and footings (true), as before.
    // AdvancedBrep (docs/plan-studio-freeform.md §9, F2c): a body known exactly (IIfcExactBodies: a Studio cylinder, a revolution, a smooth
    // loft, a SubD's regular patches) written as an IfcAdvancedBrep on its exact surfaces instead of its facets. Off: every body as it was.
    // TypePropertiesOnOccurrences (issue #30, decision 667): the type's own properties (its property sets, not its instance defaults) also
    // on each of its elements, in the sets of the same names, for readers that look at the occurrence only; the element's own value of a
    // property wins over its type's. Off: the type's properties on the type only, as IFC has them (IfcRelDefinesByType).
    // Structural: IFC's structural analysis view beside the building (docs/plan-fem.md §8.7): the analytical model, the load cases, their actions and combinations.
    // BonsaiStyles: the project's drawing styles for Bonsai beside the file (<see cref="Ifc.BonsaiStyles"/>), named in BBIM_Documentation on IfcProject.
    public bool Ifc43 => Schema.StartsWith("IFC4X3", StringComparison.OrdinalIgnoreCase);
    public bool Grouped => !string.Equals(Detail, "simple", StringComparison.OrdinalIgnoreCase);
    public bool FaceSet => string.Equals(Geometry, "faceset", StringComparison.OrdinalIgnoreCase);
    public string SchemaName => Ifc43 ? "IFC4X3_ADD2" : "IFC4";
}

/// <summary>What IfcStairFlight says of itself: NumberOfRisers, NumberOfTreads, RiserHeight, TreadLength.</summary>
public sealed record StairFlightNumbers(int? Risers, int? Treads, double? RiserHeight, double? TreadLength);

public sealed record IfcExportResult(string Path, string Schema, int Elements, int Parts, int Storeys, IReadOnlyList<string> Notes)
{
    public JsonObject ToJson() => new()
    {
        ["path"] = Path, ["schema"] = Schema, ["elements"] = Elements, ["parts"] = Parts, ["storeys"] = Storeys,
        ["notes"] = new JsonArray(Notes.Select(n => (JsonNode)n).ToArray()),
    };
}

/// <summary>
/// A session written as IFC (docs/spec-ifc.md): project, site, building and
/// storeys; every element that has a body, placed relative to its storey, as
/// faceted B-rep in its colours, with its type, its material (layer set and
/// usage for layered elements), the IFC common property set it can answer,
/// the Lapin intent set (its attributes, so Lapin and BubbleBIM rebuild the
/// element), its quantities with their IFC names and its phases. Openings are
/// written as IfcOpeningElement with their body over the uncut host, as IFC
/// asks; doors and windows fill them. GlobalIds are the model's; what the
/// export derives (parts, relationships, property sets) gets ids derived from
/// its parent's id and a key, the same at every export.
/// </summary>
public static partial class IfcExport
{
    // Lapin's own sets: no "Pset_" prefix, which IFC keeps for the sets its specification declares (IfcPropertySet).
    public const string IntentSetName = "Lapin_Intent";

    /// <summary>
    /// The elements that name another one they stand by, written also as IfcRelConnectsElements (the one named relating, the
    /// element related; the relationship's Name the connection's) and read back from it when the element's Lapin_Intent does not
    /// name it: a kerb (<c>LapinKerb</c>, its <c>hardscape</c>) by its hardscape (site grading T4).
    /// </summary>
    public static readonly (string Class, string Key, string Name)[] Connections = [("LapinKerb", "hardscape", "Kerb")];

    /// <summary>
    /// The Mode an IfcSurfaceTexture is written with for each of a material's maps (docs/plan-render.md, S1): the colour as X3D's
    /// MODULATE (multiplied by the surface's colour, as most readers take an unnamed texture), the others by what they are for.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> TextureModes = new Dictionary<string, string>
    {
        ["color"] = "MODULATE", ["normal"] = "NORMAL", ["bump"] = "BUMP", ["roughness"] = "ROUGHNESS", ["ao"] = "OCCLUSION",
    };

    /// <summary>The names of a material's IfcFillAreaStyle for each kind of view (decision 678), which the import reads back.</summary>
    public static readonly IReadOnlyDictionary<string, string> FillAreaStyleNames = new Dictionary<string, string> { ["section"] = "Section hatch", ["plan"] = "Plan hatch", ["elevation"] = "Elevation hatch" };
    /// <summary>A provision opening's set (decision 293): the run that cut it, its shape and size.</summary>
    public const string ProvisionSetName = "Lapin_ProvisionOpening";
    public const string PhaseSetName = "Lapin_Phase";
    public const string ParamsSetName = "Lapin_Parameters";
    /// <summary>The project's information on IfcProject (decision 340).</summary>
    public const string ProjectInfoSetName = "Lapin_ProjectInformation";
    /// <summary>An opening void's own set (decision 289): its mark, kind, host, opening, fill and offset.</summary>
    public const string VoidSetName = "Lapin_OpeningVoid";
    /// <summary>A wall layer's extension into the floors' layers (the forum's topic #30): ExtendToFloorBelow, ExtendToFloorAbove, on its IfcMaterialLayer.</summary>
    public const string LayerJoinsSetName = "Lapin_LayerJoins";
    /// <summary>The intent set's name until 25.10.2026: files written before still carry it.</summary>
    public const string OldIntentSetName = "Pset_Lapin_Intent";
    public static bool IsIntentSet(string? name) => name is IntentSetName or OldIntentSetName;
    // The keys the sets' GlobalIds derive from stay the old names: a file exported again keeps its sets' ids.
    /// <summary>The key a type's mirror image (doors and windows flipped on one axis) is derived from.</summary>
    const string MirrorKey = "#mirrored";
    /// <summary>An apartment type's instance: its type's name and id, where it stands (docs/plan-units.md).</summary>
    public const string UnitSetName = "Lapin_Unit";
    /// <summary>The same as <see cref="UnitSetName"/> on an instance of a component that is not an apartment type.</summary>
    public const string ComponentSetName = "Lapin_Component";
    /// <summary>A SubD solid's cage (decision 780): its points, faces and creases as JSON in <see cref="SubDCageProperty"/>, read back into its <c>cage</c>.</summary>
    public const string SubDSetName = "Lapin_SubD";
    public const string SubDCageProperty = "Cage";
    const string IntentKey = "Pset_Lapin_Intent", PhaseKey = "Pset_Lapin_Phase", ParamsKey = "Pset_Lapin_Parameters";
    public const string Version = "1";

    public static IfcExportResult Write(Session session, string path, IfcExportOptions? options = null)
    {
        // Built in memory first, then written whole or not at all (AtomicFile): a failed export leaves no file behind, nor half of one.
        var (bytes, result) = Bytes(session, System.IO.Path.GetFileName(path), options);
        AtomicFile.Write(path, bytes);
        // Bonsai's drawing styles beside it, under the names the file gives them (BBIM_Documentation).
        if (options?.BonsaiStyles == true)
        {
            var sheet = BonsaiStyles.Of(session.Model);
            var (css, patterns) = BonsaiStyles.FileNames(path);
            var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) ?? ".";
            AtomicFile.WriteText(System.IO.Path.Combine(dir, css), sheet.Css);
            AtomicFile.WriteText(System.IO.Path.Combine(dir, patterns), sheet.Patterns);
            result = result with { Notes = [.. result.Notes, $"Bonsai drawing styles: {css} and {patterns} beside it."] };
        }
        return result with { Path = path };
    }

    /// <summary>The project as IFC into a stream, <paramref name="name"/> in its header (a browser hands the bytes to the user).</summary>
    public static IfcExportResult Write(Session session, Stream output, string name, IfcExportOptions? options = null)
    {
        var w = new Writer(session, options ?? new IfcExportOptions()) { IfcName = name };
        w.Run();
        w.File.Write(output, name, w.O.Ifc43 ? "ViewDefinition [ReferenceView]" : "ViewDefinition [ReferenceView_V1.2]", w.O.SchemaName);
        return new IfcExportResult(name, w.O.SchemaName, w.Elements, w.PartCount, w.StoreyCount, w.Notes);
    }

    /// <summary>
    /// The project as IFC, the file's bytes in one array of its size (report #156: a browser's engine holds a large file once,
    /// not in a growing stream and a copy of it too).
    /// </summary>
    public static (byte[] Bytes, IfcExportResult Result) Bytes(Session session, string name, IfcExportOptions? options = null)
    {
        var w = new Writer(session, options ?? new IfcExportOptions()) { IfcName = name };
        w.Run();
        var bytes = w.File.ToArray(name, w.O.Ifc43 ? "ViewDefinition [ReferenceView]" : "ViewDefinition [ReferenceView_V1.2]", w.O.SchemaName);
        return (bytes, new IfcExportResult(name, w.O.SchemaName, w.Elements, w.PartCount, w.StoreyCount, w.Notes));
    }

    /// <summary>An IFC class and what the element is as one: its predefined type (empty: the class has none) and, user-defined, the object type that names it.</summary>
    sealed record Kind(string Class, string Predefined, string? ObjectType);

    /// <summary>The predefined types IFC4 knows per class (USERDEFINED and NOTDEFINED always); null: the class has no predefined type.</summary>
    static readonly Dictionary<string, string[]?> Classes = new()
    {
        [IfcClass.Wall] = ["MOVABLE", "PARAPET", "PARTITIONING", "PLUMBINGWALL", "SHEAR", "SOLIDWALL", "STANDARD", "POLYGONAL", "ELEMENTEDWALL"],
        [IfcClass.Slab] = ["FLOOR", "ROOF", "LANDING", "BASESLAB"],
        [IfcClass.Roof] = ["FLAT_ROOF", "SHED_ROOF", "GABLE_ROOF", "HIP_ROOF", "HIPPED_GABLE_ROOF", "GAMBREL_ROOF", "MANSARD_ROOF", "BARREL_ROOF", "RAINBOW_ROOF", "BUTTERFLY_ROOF", "PAVILION_ROOF", "DOME_ROOF", "FREEFORM"],
        [IfcClass.Column] = ["COLUMN", "PILASTER"],
        [IfcClass.Beam] = ["BEAM", "JOIST", "HOLLOWCORE", "LINTEL", "SPANDREL", "T_BEAM"],
        [IfcClass.Footing] = ["CAISSON_FOUNDATION", "FOOTING_BEAM", "PAD_FOOTING", "PILE_CAP", "STRIP_FOOTING"],
        [IfcClass.Door] = ["DOOR", "GATE", "TRAPDOOR"],
        [IfcClass.Window] = ["WINDOW", "SKYLIGHT", "LIGHTDOME"],
        ["IfcMember"] = ["BRACE", "CHORD", "COLLAR", "MEMBER", "MULLION", "PLATE", "POST", "PURLIN", "RAFTER", "STRINGER", "STRUT", "STUD"],
        ["IfcPlate"] = ["CURTAIN_PANEL", "SHEET"],
        ["IfcCurtainWall"] = [],
        ["IfcCovering"] = ["CEILING", "FLOORING", "CLADDING", "ROOFING", "MOLDING", "SKIRTINGBOARD", "INSULATION", "MEMBRANE", "SLEEVING", "WRAPPING"],
        ["IfcRailing"] = ["HANDRAIL", "GUARDRAIL", "BALUSTRADE"],
        ["IfcStair"] = ["STRAIGHT_RUN_STAIR", "TWO_STRAIGHT_RUN_STAIR", "QUARTER_WINDING_STAIR", "QUARTER_TURN_STAIR", "HALF_WINDING_STAIR", "HALF_TURN_STAIR", "TWO_QUARTER_WINDING_STAIR", "TWO_QUARTER_TURN_STAIR", "THREE_QUARTER_WINDING_STAIR", "THREE_QUARTER_TURN_STAIR", "SPIRAL_STAIR", "DOUBLE_RETURN_STAIR", "CURVED_RUN_STAIR", "TWO_CURVED_RUN_STAIR"],
        [IfcClass.StairFlight] = ["STRAIGHT", "WINDER", "SPIRAL", "CURVED", "FREEFORM"],
        [IfcClass.RampFlight] = ["STRAIGHT", "SPIRAL"],
        ["IfcRamp"] = ["STRAIGHT_RUN_RAMP", "TWO_STRAIGHT_RUN_RAMP", "QUARTER_TURN_RAMP", "TWO_QUARTER_TURN_RAMP", "HALF_TURN_RAMP", "SPIRAL_RAMP"],
        ["IfcChimney"] = [],
        ["IfcFurnishingElement"] = null,
        // Furniture (docs/plan-furniture.md): IFC4's IfcFurnitureTypeEnum.
        ["IfcFurniture"] = ["CHAIR", "TABLE", "DESK", "BED", "FILECABINET", "SHELF", "SOFA"],
        // Sunshades (report #56): IFC4's IfcShadingDeviceTypeEnum (louvres are USERDEFINED there, LOUVER in IFC 4.3).
        ["IfcShadingDevice"] = ["JALOUSIE", "SHUTTER", "AWNING"],
        [IfcClass.BuildingElementProxy] = ["COMPLEX", "ELEMENT", "PARTIAL", "PROVISIONFORVOID", "PROVISIONFORSPACE"],
        [IfcClass.GeographicElement] = ["TERRAIN"],
        [IfcClass.Pavement] = ["FLEXIBLE", "RIGID"],
        [IfcClass.EarthworksCut] = ["BASE_EXCAVATION", "CUT", "DREDGING", "EXCAVATION", "OVEREXCAVATION", "PAVEMENTMILLING", "STEPEXCAVATION", "TOPSOILREMOVAL", "TRENCH"],
        ["IfcBuildingElementPart"] = ["INSULATION", "PRECASTPANEL"],
        ["IfcPipeSegment"] = ["CULVERT", "FLEXIBLESEGMENT", "RIGIDSEGMENT", "GUTTER", "SPOOL"],
        ["IfcDiscreteAccessory"] = ["ANCHORPLATE", "BRACKET", "SHOE"],
        // A steel connection's fasteners (decision 273): bolts and anchors, welds.
        ["IfcMechanicalFastener"] = ["ANCHORBOLT", "BOLT", "DOWEL", "NAIL", "NAILPLATE", "RIVET", "SCREW", "SHEARCONNECTOR", "STAPLE", "STUDSHEARCONNECTOR"],
        ["IfcFastener"] = ["GLUE", "MORTAR", "WELD"],
        // Formwork and scaffolding (the site works module): assemblies of members, plates and accessories.
        ["IfcElementAssembly"] = ["ACCESSORY_ASSEMBLY", "ARCH", "BEAM_GRID", "BRACED_FRAME", "GIRDER", "REINFORCEMENT_UNIT", "RIGID_FRAME", "SLAB_FIELD", "TRUSS"],
        // The distribution elements (the MEP module's nodes and runs), IFC4's enumerations.
        ["IfcDuctSegment"] = ["RIGIDSEGMENT", "FLEXIBLESEGMENT"],
        ["IfcCableSegment"] = ["BUSBARSEGMENT", "CABLESEGMENT", "CONDUCTORSEGMENT", "CORESEGMENT"],
        // Cable carriers (report #147): trays, ladders, trunking and conduit, their bends, tees, crosses and reducers.
        ["IfcCableCarrierSegment"] = ["CABLELADDERSEGMENT", "CABLETRAYSEGMENT", "CABLETRUNKINGSEGMENT", "CONDUITSEGMENT"],
        ["IfcCableCarrierFitting"] = ["BEND", "CROSS", "REDUCER", "TEE"],
        ["IfcPipeFitting"] = ["BEND", "CONNECTOR", "ENTRY", "EXIT", "JUNCTION", "OBSTRUCTION", "TRANSITION"],
        ["IfcDuctFitting"] = ["BEND", "CONNECTOR", "ENTRY", "EXIT", "JUNCTION", "OBSTRUCTION", "TRANSITION"],
        ["IfcBoiler"] = ["WATER", "STEAM"],
        // Lifts (docs/plan-elevators.md): IFC4's IfcTransportElementTypeEnum (IFC 4.3 adds HAULINGGEAR).
        ["IfcTransportElement"] = ["ELEVATOR", "ESCALATOR", "MOVINGWALKWAY", "CRANEWAY", "LIFTINGGEAR"],
        ["IfcSpaceHeater"] = ["CONVECTOR", "RADIATOR"],
        ["IfcPump"] = ["CIRCULATOR", "ENDSUCTION", "SPLITCASE", "SUBMERSIBLEPUMP", "SUMPPUMP", "VERTICALINLINE", "VERTICALTURBINE"],
        ["IfcValve"] = ["AIRRELEASE", "ANTIVACUUM", "CHANGEOVER", "CHECK", "COMMISSIONING", "DIVERTING", "DRAWOFFCOCK", "DOUBLECHECK", "DOUBLEREGULATING", "FAUCET", "FLUSHING", "GASCOCK", "GASTAP", "ISOLATING", "MIXING", "PRESSUREREDUCING", "PRESSURERELIEF", "REGULATING", "SAFETYCUTOFF", "STEAMTRAP", "STOPCOCK"],
        ["IfcFlowMeter"] = ["ENERGYMETER", "GASMETER", "OILMETER", "WATERMETER"],
        ["IfcTank"] = ["BASIN", "BREAKPRESSURE", "EXPANSION", "FEEDANDEXPANSION", "PRESSUREVESSEL", "STORAGE", "VESSEL"],
        // Gas and fuel oil (decision 285): a cooker's burner, an oil filter, the meter box a service enters.
        ["IfcBurner"] = [],
        ["IfcFilter"] = ["AIRPARTICLEFILTER", "COMPRESSEDAIRFILTER", "ODORFILTER", "OILFILTER", "STRAINER", "WATERFILTER"],
        ["IfcDistributionChamberElement"] = ["FORMEDDUCT", "INSPECTIONCHAMBER", "INSPECTIONPIT", "MANHOLE", "METERCHAMBER", "SUMP", "TRENCH", "VALVECHAMBER"],
        ["IfcSanitaryTerminal"] = ["BATH", "BIDET", "CISTERN", "SHOWER", "SINK", "SANITARYFOUNTAIN", "TOILETPAN", "URINAL", "WASHHANDBASIN", "WCSEAT"],
        ["IfcWasteTerminal"] = ["FLOORTRAP", "FLOORWASTE", "GULLYSUMP", "GULLYTRAP", "ROOFDRAIN", "WASTEDISPOSALUNIT", "WASTETRAP"],
        ["IfcFan"] = ["CENTRIFUGALFORWARDCURVED", "CENTRIFUGALRADIAL", "CENTRIFUGALBACKWARDINCLINEDCURVED", "CENTRIFUGALAIRFOIL", "TUBEAXIAL", "VANEAXIAL", "PROPELLORAXIAL"],
        ["IfcAirTerminal"] = ["DIFFUSER", "GRILLE", "LOUVRE", "REGISTER"],
        ["IfcElectricDistributionBoard"] = ["CONSUMERUNIT", "DISTRIBUTIONBOARD", "MOTORCONTROLCENTRE", "SWITCHBOARD"],
        ["IfcOutlet"] = ["AUDIOVISUALOUTLET", "COMMUNICATIONSOUTLET", "POWEROUTLET", "DATAOUTLET", "TELEPHONEOUTLET"],
        ["IfcLightFixture"] = ["POINTSOURCE", "DIRECTIONSOURCE", "SECURITYLIGHTING"],
        // Fire protection (report #89): the detection loop's panel, detectors, call points and sounders; hydrants, the fire brigade inlet, sprinklers.
        ["IfcUnitaryControlElement"] = ["ALARMPANEL", "CONTROLPANEL", "GASDETECTIONPANEL", "INDICATORPANEL", "MIMICPANEL", "HUMIDISTAT", "THERMOSTAT", "WEATHERSTATION"],
        ["IfcSensor"] = ["CO2SENSOR", "CONDUCTANCESENSOR", "CONTACTSENSOR", "COSENSOR", "FIRESENSOR", "FLOWSENSOR", "FROSTSENSOR", "GASSENSOR", "HEATSENSOR", "HUMIDITYSENSOR", "IDENTIFIERSENSOR", "IONCONCENTRATIONSENSOR", "LEVELSENSOR", "LIGHTSENSOR", "MOISTURESENSOR", "MOVEMENTSENSOR", "PHSENSOR", "PRESSURESENSOR", "RADIATIONSENSOR", "RADIOACTIVITYSENSOR", "SMOKESENSOR", "SOUNDSENSOR", "TEMPERATURESENSOR", "WINDSENSOR"],
        ["IfcAlarm"] = ["BELL", "BREAKGLASSBUTTON", "LIGHT", "MANUALPULLBOX", "SIREN", "WHISTLE"],
        ["IfcFireSuppressionTerminal"] = ["BREECHINGINLET", "FIREHYDRANT", "HOSEREEL", "SPRINKLER", "SPRINKLERDEFLECTOR"],
        // Room separation lines (forum topic 80): IFC4's IfcVirtualElement has no PredefinedType.
        [RoomDividers.Class] = null,
        ["IfcSwitchingDevice"] = ["CONTACTOR", "DIMMERSWITCH", "EMERGENCYSTOP", "KEYPAD", "MOMENTARYSWITCH", "SELECTORSWITCH", "STARTER", "SWITCHDISCONNECTOR", "TOGGLESWITCH"],
    };

    /// <summary>Whether IFC4 gives the class (upper case, as a file writes it) a PredefinedType after the eight attributes every element has.</summary>
    internal static bool HasPredefined(string upper) => UpperClasses.TryGetValue(upper, out var allowed) && allowed != null;
    static readonly Dictionary<string, string[]?> UpperClasses = Classes.ToDictionary(kv => kv.Key.ToUpperInvariant(), kv => kv.Value);
    /// <summary>What IFC 4.3 adds: the railway's classes, and predefined types IFC4 lacks (a platform slab, a track panel).</summary>
    static readonly Dictionary<string, string[]?> Classes43 = new()
    {
        ["IfcShadingDevice"] = ["JALOUSIE", "SHUTTER", "AWNING", "LOUVER"],
        [IfcClass.Slab] = ["FLOOR", "ROOF", "LANDING", "BASESLAB", "APPROACH_SLAB", "PAVING", "WEARING", "SIDEWALK", "TRACKSLAB"],
        ["IfcElementAssembly"] = ["ACCESSORY_ASSEMBLY", "ARCH", "BEAM_GRID", "BRACED_FRAME", "GIRDER", "REINFORCEMENT_UNIT", "RIGID_FRAME", "SLAB_FIELD", "TRUSS", "ABUTMENT", "PIER", "PYLON", "CROSS_BRACING", "DECK", "MAST", "SIGNALASSEMBLY", "GRID", "SHELTER", "SUPPORTINGASSEMBLY", "SUSPENSIONASSEMBLY", "TRACTION_SWITCHING_ASSEMBLY", "TRACKPANEL", "TURNOUTPANEL", "DILATATIONPANEL", "RAIL_MECHANICAL_EQUIPMENT_ASSEMBLY", "ENTRANCEWORKS", "SUMPBUSTER", "TRAFFIC_CALMING_DEVICE"],
        ["IfcRail"] = ["RACKRAIL", "BLADE", "GUARDRAIL", "STOCKRAIL", "CHECKRAIL", "RAIL"],
        ["IfcTrackElement"] = ["TRACKENDOFALIGNMENT", "BLOCKINGDEVICE", "VEHICLESTOP", "SLEEPER", "HALF_SET_OF_BLADES", "SPEEDREGULATOR", "DERAILER", "FROG"],
        ["IfcCourse"] = ["ARMOUR", "FILTER", "BALLASTBED", "CORE", "PAVEMENT", "PROTECTION"],
        // IfcKerbTypeEnum has only USERDEFINED and NOTDEFINED.
        ["IfcKerb"] = [],
        // A grading that mostly fills (site grading T5): IfcEarthworksFillTypeEnum.
        ["IfcEarthworksFill"] = ["BACKFILL", "COUNTERWEIGHT", "EMBANKMENT", "SLOPEFILL", "SUBGRADE", "SUBGRADEBED", "TRANSITIONSECTION"],
        ["IfcSignal"] = ["VISUAL", "AUDIO", "MIXED"],
        [RoomDividers.Class] = ["BOUNDARY", "CLEARANCE", "PROVISIONFORVOID"],
    };

    /// <summary>The element assembly in IFC4: its own enumeration, without the railway's panels.</summary>
    static readonly string[] AssemblyTypes4 = ["ACCESSORY_ASSEMBLY", "ARCH", "BEAM_GRID", "BRACED_FRAME", "GIRDER", "REINFORCEMENT_UNIT", "RIGID_FRAME", "SLAB_FIELD", "TRUSS"];

    /// <summary>The classes with a Pset_…Common the export can answer.</summary>
    static readonly HashSet<string> CommonSets =
    [
        IfcClass.Wall, IfcClass.Slab, IfcClass.Roof, IfcClass.Column, IfcClass.Beam, IfcClass.Footing, IfcClass.Door, IfcClass.Window,
        "IfcMember", "IfcPlate", "IfcCovering", "IfcRailing", "IfcStair", "IfcRamp", "IfcChimney", IfcClass.BuildingElementProxy, "IfcTransportElement",
    ];

    /// <summary>A number of working days as an IfcDuration, its part of a day in hours of a day of <paramref name="hoursPerDay"/> (the writer's).</summary>
    internal static string IfcDuration(double days, double hoursPerDay = 8) => Writer.IfcDuration(days, hoursPerDay);

    sealed partial class Writer(Session s, IfcExportOptions o)
    {
        // The application that wrote it: the product (Lapin, or an edition under its own name); its author and organization
        // the project information's (drawn by, the designer), as on the sheets' title blocks.
        public readonly Step File = new()
        {
            Application = s.Product.Name,
            Author = Lapin.Platform.Documents.ProjectInfo.Get(s.Model, "author"),
            Organization = Lapin.Platform.Documents.ProjectInfo.Get(s.Model, "designer"),
        };
        public IfcExportOptions O => o;
        /// <summary>The file's name, which names the drawing styles written beside it.</summary>
        public string IfcName { get; init; } = "project.ifc";
        public int Elements, PartCount, StoreyCount;
        public readonly List<string> Notes = [];

        readonly IModelView m = s.Model;
        /// <summary>What modules keep out of the file: their own groups and attribute keys (IIfcExportFilter).</summary>
        readonly IReadOnlyList<IIfcExportFilter> filters = s.Catalog.All<IIfcExportFilter>();
        readonly HashSet<string> privateKeys = [.. s.Catalog.All<IIfcExportFilter>().SelectMany(x => x.PrivateKeys)];
        Entity project = null!;
        string model = "", body = "", axis = "", footprint = "", metre = "", siteRef = "", sitePlace = "";
        readonly Dictionary<string, string> storeyRef = [], storeyPlace = [], refIdOf = [];
        readonly Dictionary<string, (string Place, double Z0)> placeOf = [];
        string buildingRef = "", projectRef = "";
        readonly Dictionary<string, double> storeyZ = [];
        readonly Dictionary<string, List<string>> contained = [];
        /// <summary>The rooms of each storey (by the storey's IFC entity), aggregated into it.</summary>
        readonly Dictionary<string, List<string>> spaces = [];
        readonly Dictionary<string, string> styles = [], materials = [], layerSets = [], written = [];
        /// <summary>The IfcTasks written, by their id: what a cost item's tasks are (stage 7 of docs/plan-4d5d.md).</summary>
        readonly Dictionary<string, string> tasksWritten = [];
        /// <summary>A style key naming a material (its id after it), as against a colour (#rrggbb).</summary>
        const string MaterialKey = "material:";
        readonly Dictionary<string, (string Ref, List<string> Occurrences)> types = [];
        readonly Dictionary<string, List<string>> materialUses = [];
        /// <summary>The doors' and windows' constituent sets by their parts and materials: the set, its association's GlobalId, what it is associated to.</summary>
        readonly Dictionary<string, (string Ref, string Gid, List<string> Users)> constituentSets = [];
        readonly HashSet<string> constituentTypes = [];
        readonly Dictionary<(string Element, string Key), string> partRefs = [];
        readonly List<Relation> pendingFills = [];
        /// <summary>The fills of a reference's kept openings: the IfcRelFillsElement's GlobalId, the opening written, the filler's id.</summary>
        readonly List<(string Gid, string Opening, string Filler)> pendingKeptFills = [];
        readonly List<(Relation Rel, string Host, string Void)> pendingVoids = [];

        Step f => File;
        IfcCarry.Writer? carry;
        /// <summary>What foreign elements carry from their file (<see cref="IfcSource"/>), written back as it came.</summary>
        IfcCarry.Writer Carry => carry ??= new IfcCarry.Writer(File);

        /// <summary>
        /// The classifications (docs/plan-user-extensibility.md §7.3): each system the elements and types written use an
        /// IfcClassification, each code an IfcClassificationReference (its title as the name, its system as the source), related to
        /// what carries it by one IfcRelAssociatesClassification.
        /// </summary>
        void Classified()
        {
            var groups = new Dictionary<(string System, string Code), List<string>>();
            foreach (var e in m.Entities)
            {
                var refs = Classifications.RefsOf(e);
                if (refs.Count == 0) continue;
                var target = written.TryGetValue(e.Id, out var w) ? w : types.TryGetValue(e.Id, out var t) ? t.Ref : null;
                if (target == null) continue;
                foreach (var r in refs) (groups.TryGetValue(r, out var l) ? l : groups[r] = []).Add(target);
            }
            if (groups.Count == 0) return;
            var systems = new Dictionary<string, string>();
            foreach (var ((systemId, code), objects) in groups.OrderBy(g => g.Key.System).ThenBy(g => g.Key.Code, StringComparer.Ordinal))
            {
                if (m.Get(systemId) is not { Class: Classifications.Class } system) continue;
                if (!systems.TryGetValue(systemId, out var sysRef))
                    systems[systemId] = sysRef = f.Add("IFCCLASSIFICATION", S(system.Str("source")), S(system.Str("edition")), Null, S(system.Name), S(system.Str("description")), Null, Null);
                var title = Classifications.Items(system).FirstOrDefault(i => i.Code == code)?.Title;
                var reference = f.Add("IFCCLASSIFICATIONREFERENCE", Null, S(code), S(title), sysRef, Null, Null);
                f.Add("IFCRELASSOCIATESCLASSIFICATION", Gid(systemId, "class:" + code), Null, Null, Null, L(objects.Distinct()), reference);
            }
        }

        public void Run()
        {
            project = Spatial.Project(m) ?? throw new CommandException("The model has no project.");
            WriteSpatial();
            foreach (var mat in m.OfClass(IfcClass.Material)) Material(mat.Id);
            Grids();
            Railway();
            // The alignments before the elements: an element of the railway is placed along its alignment's curve (IfcLinearPlacement).
            Alignments();
            WriteElements();
            if (ExactBodies > 0) Notes.Add($"{ExactBodies} bod{(ExactBodies == 1 ? "y" : "ies")} written exactly, as IfcAdvancedBrep.");
            ProvisionVoids();
            Assemblies();
            CurtainWallParts();
            Network();
            carry?.Finish(buildingRef);
            Finish();
            Processes();
            Controls();
            if (o.Structural) Structural();
        }

        // ─── Ids and small helpers ──────────────────────────────────────────

        /// <summary>The model's id as the GlobalId; one that is not a GlobalId (an old project) is mapped to one, the same every time.</summary>
        static string Gid(string id) => S(GlobalId.IsValid(id) ? id : Guid("lapin", id));
        static string Gid(string parent, string key) => S(Guid(parent, key));
        static string GlobalIdOf(string id) => GlobalId.IsValid(id) ? id : Guid("lapin", id);

        /// <summary>
        /// A spatial aggregation's or containment's GlobalId: the one the IFC file it came from gave it (<see cref="Spatial.FileGlobalId"/>:
        /// a project made from a file, a storey the file brought), else derived from its owner by <paramref name="key"/>.
        /// </summary>
        string SpatialGid(string relating, string relationClass, string? related, string owner, string key) =>
            Spatial.FileGlobalIdOf(m.RelationsOf(relating, relationClass).FirstOrDefault(r => r.Relating == relating && (related == null || r.Related.Contains(related)))) is { } g
                ? S(g) : Gid(owner, key);

        string Place(string? relTo, double z) =>
            f.Add("IFCLOCALPLACEMENT", relTo ?? Null, f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", L(new[] { R(0), R(0), R(z) })), Null, Null));

        string P2(Vec2 p) => f.Add("IFCCARTESIANPOINT", L(new[] { R(p.X), R(p.Y) }));

        static Vec2? Pt(JsonNode? n) =>
            n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : null;

        static string Opt(double? v) => v is { } x && x > 0 ? R(x) : Null;

        void Contain(string spatial, string element) => (contained.TryGetValue(spatial, out var l) ? l : contained[spatial] = []).Add(element);

        string Single(string name, string value, string? description = null) => f.Add("IFCPROPERTYSINGLEVALUE", S(name), S(description), value, Null);

        void Define(string entity, string definition, string gid) => f.Add("IFCRELDEFINESBYPROPERTIES", gid, Null, Null, Null, L(new[] { entity }), definition);

        // A part's style: its material's (named after it, with how it renders) when it has one, else its colour's.
        string? StyleKey(GeometryPart p) => p.Material != null && m.Get(p.Material) is { Class: IfcClass.Material } ? MaterialKey + p.Material : p.Color;

        string Style(string hex)
        {
            if (styles.TryGetValue(hex, out var st)) return st;
            if (hex.StartsWith(MaterialKey, StringComparison.Ordinal)) return styles[hex] = MaterialStyle(hex[MaterialKey.Length..]);
            var (r, g, b) = HexColor.Unit(hex, (0.75, 0.75, 0.75));
            var colour = f.Add("IFCCOLOURRGB", Null, R(r), R(g), R(b));
            return styles[hex] = f.Add("IFCSURFACESTYLE", S(hex), E("BOTH"), L(new[] { f.Add("IFCSURFACESTYLESHADING", colour, R(0)) }));
        }

        /// <summary>
        /// A material's surface style (report #76): named after it, as renderers reading IFC swap materials by name, an
        /// IfcSurfaceStyleRendering with its colour and transparency, its specular share and roughness, and how it reflects
        /// (METAL, GLASS, MATT or PLASTIC) after its look.
        /// </summary>
        string MaterialStyle(string id)
        {
            var mat = m.Get(id);
            var look = MaterialLook.Of(mat);
            var (r, g, b) = HexColor.Unit(Materials.ColorOf(m, id), (0.75, 0.75, 0.75));
            var colour = f.Add("IFCCOLOURRGB", Null, R(r), R(g), R(b));
            var method = look.Metalness >= 0.5 ? "METAL" : look.Transmission > 0 || look.Transparency > 0 ? "GLASS" : look.Roughness >= 0.8 ? "MATT" : "PLASTIC";
            var rendering = f.Add("IFCSURFACESTYLERENDERING", colour, R(Math.Clamp(look.Transparency, 0, 1)), Typed("IFCNORMALISEDRATIOMEASURE", R(1)),
                look.Transmission > 0 ? Typed("IFCNORMALISEDRATIOMEASURE", R(Math.Round(look.Transmission, 4))) : Null, Null, Null,
                Typed("IFCNORMALISEDRATIOMEASURE", R(Math.Round(0.04 + 0.96 * look.Metalness, 4))), Typed("IFCSPECULARROUGHNESS", R(Math.Round(look.Roughness, 4))), E(method));
            var textures = mat != null ? SurfaceTextures(mat, look) : null;
            return f.Add("IFCSURFACESTYLE", S(mat?.Name ?? id), E("BOTH"), L(textures != null ? new[] { rendering, textures } : new[] { rendering }));
        }

        /// <summary>
        /// A material's maps as IFC's textures (docs/plan-render.md, S1): an IfcSurfaceStyleWithTextures holding one IfcBlobTexture
        /// per map, the image inside the file (its format and bytes), repeated, its Mode saying what it is for
        /// (<see cref="TextureModes"/>: MODULATE for the colour, NORMAL, BUMP, ROUGHNESS, OCCLUSION), its transform the tile it
        /// lies on (metres wide and high, turned by the texture's rotation); null when the material has no map.
        /// </summary>
        string? SurfaceTextures(Entity mat, MaterialLook look)
        {
            var size = Images.SizeOf(mat);
            var a = look.TextureRotation * Math.PI / 180;
            var list = new List<string>();
            foreach (var (map, key) in Images.MapKeys)
            {
                if (Images.Of(m, mat, key) is not { } image) continue;
                byte[] bytes;
                try { bytes = Images.Bytes(image); }
                catch (FormatException) { continue; }
                var transform = f.Add("IFCCARTESIANTRANSFORMATIONOPERATOR2DNONUNIFORM",
                    f.Add("IFCDIRECTION", L(new[] { R(Math.Round(Math.Cos(a), 9)), R(Math.Round(Math.Sin(a), 9)) })),
                    f.Add("IFCDIRECTION", L(new[] { R(Math.Round(-Math.Sin(a), 9)), R(Math.Round(Math.Cos(a), 9)) })),
                    f.Add("IFCCARTESIANPOINT", L(new[] { R(0), R(0) })), R(size[0]), R(size[1]));
                list.Add(f.Add("IFCBLOBTEXTURE", True, True, S(TextureModes[map]), transform, Null,
                    S(image.Str("mime") == "image/jpeg" ? "JPG" : "PNG"), "\"0" + Convert.ToHexString(bytes) + "\""));
            }
            return list.Count == 0 ? null : f.Add("IFCSURFACESTYLEWITHTEXTURES", L(list));
        }

        /// <summary>A solid as a faceted B-rep, its vertices relative to the placement's height, in its colour.</summary>
        /// <summary>
        /// The frame a body is written in: the storey's (its level under the world,
        /// axes along it) or, for an element placed along an alignment, the
        /// alignment's point there heading along it. World → local: moved to the
        /// origin, turned back by the heading.
        /// </summary>
        readonly record struct Frame(Vec3 Origin, double Heading)
        {
            public static Frame AtZ(double z0) => new(new Vec3(0, 0, z0), 0);
            public Vec3 Local(Vec3 p) => Turn(p - Origin);
            public Vec3 Turn(Vec3 v)
            {
                if (Heading == 0) return v;
                var (c, s) = (Math.Cos(Heading), Math.Sin(Heading));
                return new Vec3(v.X * c + v.Y * s, -v.X * s + v.Y * c, v.Z);
            }
        }

        string Brep(Solid solid, string? hex, double z0) => Brep(solid, hex, v => new Vec3(v.X, v.Y, v.Z - z0));
        string Brep(Solid solid, string? hex, Frame fr) => Brep(solid, hex, fr.Local);

        /// <summary>
        /// A solid as the export writes bodies (<see cref="IfcExportOptions.Geometry"/>), simplified within the tolerance asked,
        /// its vertices taken into the placement's frame by <paramref name="local"/>, in its colour.
        /// </summary>
        string Brep(Solid solid, string? hex, Func<Vec3, Vec3> local)
        {
            // A body of few faces (what Lapin builds) is written as it is; a mesh's many triangles are merged where they are
            // coplanar (for a face set, which loses nothing by it) and simplified when a tolerance is asked.
            if (solid.Faces.Length > SimplifyFrom && (o.Tolerance > 0 || o.FaceSet))
                solid = simplified.TryGetValue(solid, out var ready) ? ready : Simplifier.Simplify(solid, Math.Max(0, o.Tolerance));
            var item = o.FaceSet ? FaceSet(solid, local) : FacetedBrep(solid, local);
            if (hex != null) f.Add("IFCSTYLEDITEM", item, L(new[] { Style(hex) }), Null);
            return item;
        }

        /// <summary>How many faces a body has before it is worth merging or simplifying.</summary>
        const int SimplifyFrom = 24;

        /// <summary>
        /// The faces over one point list: an IfcTriangulatedFaceSet when they are all triangles, else an
        /// IfcPolygonalFaceSet (a face with holes an IfcIndexedPolygonalFaceWithVoids); indices from 1.
        /// </summary>
        string FaceSet(Solid solid, Func<Vec3, Vec3> local)
        {
            var faces = solid.Faces.Where(x => x.Outer.Length >= 3).ToList();
            var used = new int[solid.Vertices.Length];
            var points = new List<string>();
            int Index(int i) { if (used[i] == 0) { var v = local(solid.Vertices[i]); points.Add(L(new[] { R(v.X), R(v.Y), R(v.Z) })); used[i] = points.Count; } return used[i]; }
            string Ring(int[] ring) => L(ring.Select(i => I(Index(i))));
            if (faces.All(x => x.Outer.Length == 3 && (x.Holes?.Length ?? 0) == 0))
            {
                var tris = faces.Select(x => Ring(x.Outer)).ToList();
                return f.Add("IFCTRIANGULATEDFACESET", f.Add("IFCCARTESIANPOINTLIST3D", L(points), Null), Null, Null, L(tris), Null);
            }
            var items = faces.Select(x => (x.Holes ?? []).Where(h => h.Length >= 3).ToList() is { Count: > 0 } holes
                ? (Outer: Ring(x.Outer), Holes: L(holes.Select(Ring)))
                : (Outer: Ring(x.Outer), Holes: (string?)null)).ToList();
            var refs = items.Select(x => x.Holes != null ? f.Add("IFCINDEXEDPOLYGONALFACEWITHVOIDS", x.Outer, x.Holes) : f.Add("IFCINDEXEDPOLYGONALFACE", x.Outer)).ToList();
            return f.Add("IFCPOLYGONALFACESET", f.Add("IFCCARTESIANPOINTLIST3D", L(points), Null), Null, L(refs), Null);
        }

        /// <summary>A solid as a faceted B-rep: a face, a bound and a loop for each of its faces.</summary>
        string FacetedBrep(Solid solid, Func<Vec3, Vec3> local)
        {
            var pts = solid.Vertices.Select(local).Select(v => f.Add("IFCCARTESIANPOINT", L(new[] { R(v.X), R(v.Y), R(v.Z) }))).ToArray();
            string Loop(int[] ring) => f.Add("IFCPOLYLOOP", L(ring.Select(i => pts[i])));
            var faces = solid.Faces.Where(x => x.Outer.Length >= 3).Select(face => f.Add("IFCFACE", L(
                new[] { f.Add("IFCFACEOUTERBOUND", Loop(face.Outer), True) }
                    .Concat((face.Holes ?? []).Where(h => h.Length >= 3).Select(h => f.Add("IFCFACEBOUND", Loop(h), True)))))).ToList();
            return f.Add("IFCFACETEDBREP", f.Add("IFCCLOSEDSHELL", L(faces)));
        }

        /// <summary>A door's or window's placement: its origin and axes (world) under the storey's, which stands at <paramref name="z0"/>.</summary>
        string JoineryPlace(string? relTo, IfcJoineryPlace at, double z0) =>
            f.Add("IFCLOCALPLACEMENT", relTo ?? Null, f.Add("IFCAXIS2PLACEMENT3D",
                f.Add("IFCCARTESIANPOINT", L(new[] { R(at.Origin.X), R(at.Origin.Y), R(at.Origin.Z - z0) })),
                f.Add("IFCDIRECTION", L(new[] { R(0), R(0), R(1) })),
                f.Add("IFCDIRECTION", L(new[] { R(at.X.X), R(at.X.Y), R(at.X.Z) }))));

        /// <summary>A world point in a door's or window's frame.</summary>
        static Vec3 Local(IfcJoineryPlace at, Vec3 v)
        {
            var d = v - at.Origin;
            return new Vec3(Vec3.Dot(d, at.X), Vec3.Dot(d, at.Y), d.Z);
        }

        /// <summary>
        /// A door or window type's predefined property sets: one IfcDoorPanelProperties or
        /// IfcWindowPanelProperties per panel (how it opens, where it is) and the lining's.
        /// </summary>
        IEnumerable<string> JoinerySets(string key, IfcJoineryType t)
        {
            static string Ratio(double v) => v > 0 && v <= 1 ? R(v) : Null;
            static string Length(double v) => v > 0 ? R(v) : Null;
            var i = 0;
            foreach (var p in t.Panels)
            {
                var gid = Gid(key, $"panel/{i}");
                var name = S($"Panel {++i}");
                yield return t.Door
                    ? f.Add("IFCDOORPANELPROPERTIES", gid, Null, name, Null, Null, E(p.Operation), p.Width is { } w ? Ratio(w) : Null, E(p.Position), Null)
                    : f.Add("IFCWINDOWPANELPROPERTIES", gid, Null, name, Null, E(p.Operation), E(p.Position), Length(t.Lining.Depth), Length(t.Lining.Thickness), Null);
            }
            var l = t.Lining;
            string Offset(IReadOnlyList<double> v, int n) => v.Count > n ? Ratio(v[n]) : Null;
            yield return t.Door
                ? f.Add("IFCDOORLININGPROPERTIES", Gid(key, "lining"), Null, S("Lining"), Null, Length(l.Depth), Length(l.Thickness), l.Threshold > 0 ? Length(l.Depth) : Null, Length(l.Threshold),
                    l.TransomOffsets.Count > 0 ? Length(l.Transom) : Null, Offset(l.TransomOffsets, 0), Null, Null, Null, Null, Null, Null, Null)
                : f.Add("IFCWINDOWLININGPROPERTIES", Gid(key, "lining"), Null, S("Lining"), Null, Length(l.Depth), Length(l.Thickness),
                    l.TransomOffsets.Count > 0 ? Length(l.Transom) : Null, l.MullionOffsets.Count > 0 ? Length(l.Mullion) : Null,
                    Offset(l.TransomOffsets, 0), Offset(l.TransomOffsets, 1), Offset(l.MullionOffsets, 0), Offset(l.MullionOffsets, 1), Null, Null, Null, Null);
        }

        readonly Dictionary<string, string?> maps = [];

        /// <summary>
        /// An instance's body as IFC shares one (docs/plan-instancing.md): its shared body written once as an
        /// IfcRepresentationMap, and here an IfcMappedItem placing it (relative to the storey, at <paramref name="z0"/>).
        /// Null when the placement is not a rotation and a uniform scale, which IFC's operator cannot say: then its own B-rep.
        /// Square within a millionth: what a file wrote its axes to (BIMCamel: 11 decimals) and an instance keeps of them.
        /// </summary>
        string? Mapped(SharedInstance inst, double z0)
        {
            var x = inst.Xf;
            Vec3 ax = new(x[0], x[1], x[2]), ay = new(x[3], x[4], x[5]), az = new(x[6], x[7], x[8]);
            var scale = ax.Length;
            const double tol = 1e-6;
            if (scale < 1e-12 || Math.Abs(ay.Length - scale) > tol * scale || Math.Abs(az.Length - scale) > tol * scale
                || Math.Abs(Vec3.Dot(ax, ay)) > tol * scale * scale || Math.Abs(Vec3.Dot(ay, az)) > tol * scale * scale || Math.Abs(Vec3.Dot(ax, az)) > tol * scale * scale
                || Vec3.Dot(Vec3.Cross(ax, ay), az) <= 0) return null;
            // Written square: x as it is, y made square to it, z their cross product.
            var ux = ax * (1 / scale);
            var uy = ay - ux * Vec3.Dot(ay, ux);
            uy = uy * (1 / uy.Length);
            var uz = Vec3.Cross(ux, uy);
            if (!maps.TryGetValue(inst.Body, out var map))
            {
                map = m.Get(inst.Body) is { } shared && StoredBodies.SharedParts(shared) is { Count: > 0 } parts
                    ? f.Add("IFCREPRESENTATIONMAP", f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null),
                        BodyOf(parts.Select(p => Brep(p.Solid, StyleKey(p), 0))))
                    : null;
                maps[inst.Body] = map;
            }
            if (map == null) return null;
            string Dir(Vec3 n) => f.Add("IFCDIRECTION", L(new[] { R(n.X), R(n.Y), R(n.Z) }));
            var op = f.Add("IFCCARTESIANTRANSFORMATIONOPERATOR3D", Dir(ux), Dir(uy), f.Add("IFCCARTESIANPOINT", L(new[] { R(x[9]), R(x[10]), R(x[11] - z0) })),
                Math.Abs(scale - 1) < 1e-12 ? Null : R(scale), Dir(uz));
            return f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S("MappedRepresentation"), L(new[] { f.Add("IFCMAPPEDITEM", map, op) }));
        }

        /// <summary>The representation map of each type written that has a body of its own (a part's: <see cref="IIfcTypeShapes"/>), by the type's id.</summary>
        readonly Dictionary<string, string?> typeMaps = [];

        /// <summary>
        /// A type's body as its one representation map (written once): the body a part's defaults build, its block definition. IFC4
        /// lists a type's RepresentationMaps as "a multi-view block definition" (one map per representation: Body, Axis…), never
        /// variants of one, so elements built with other values carry their own body (decision 520 P3, docs/spec-ifc.md).
        /// </summary>
        string? TypeMap(Entity type)
        {
            if (typeMaps.TryGetValue(type.Id, out var known)) return known;
            string? map = null;
            foreach (var src in s.Catalog.All<IIfcTypeShapes>())
            {
                if (src.TypeBody(m, type) is not { Parts.Count: > 0 } b) continue;
                map = f.Add("IFCREPRESENTATIONMAP", f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null),
                    BodyOf(b.Parts.Select(p => Brep(p.Solid, StyleKey(p), 0))));
                maps[b.Key] = map;
                break;
            }
            return typeMaps[type.Id] = map;
        }

        string BodyOf(IEnumerable<string> breps) => f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S(o.FaceSet ? "Tessellation" : "Brep"), L(breps));

        /// <summary>
        /// A part of many copies of one solid (a track's sleepers): the solid
        /// once, as a representation map at its own origin, and each copy as a
        /// mapped item — its axes and origin (relative to the placement's height).
        /// </summary>
        string Mapped(Solid unit, IReadOnlyList<PartInstance> copies, string? hex, Frame fr)
        {
            var source = f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S("Brep"), L(new[] { Brep(unit, hex, Frame.AtZ(0)) }));
            var map = f.Add("IFCREPRESENTATIONMAP", f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null), source);
            string D(Vec3 v) => f.Add("IFCDIRECTION", L(new[] { R(v.X), R(v.Y), R(v.Z) }));
            var items = copies.Select(c => { var o = fr.Local(c.Origin); return f.Add("IFCMAPPEDITEM", map,
                f.Add("IFCCARTESIANTRANSFORMATIONOPERATOR3D", D(fr.Turn(c.X)), D(fr.Turn(c.Y)), f.Add("IFCCARTESIANPOINT", L(new[] { R(o.X), R(o.Y), R(o.Z) })), R(1), D(fr.Turn(c.Z)))); }).ToList();
            return f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S("MappedRepresentation"), L(items));
        }

        // ─── Contexts, units, the spatial tree ──────────────────────────────

        void WriteSpatial()
        {
            var origin = f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
            var wcs = f.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null);
            // True north: the project's +Y turned by the base point's angle, counter-clockwise.
            var north = (Location.Base(m).NorthAngle ?? 0) * Math.PI / 180;
            var trueNorth = f.Add("IFCDIRECTION", L(new[] { R(-Math.Sin(north)), R(Math.Cos(north)) }));
            model = f.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Null, S("Model"), "3", R(1e-5), wcs, trueNorth);
            body = f.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("Body"), S("Model"), Derived, Derived, Derived, Derived, model, Null, E("MODEL_VIEW"), Null);
            axis = f.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("Axis"), S("Model"), Derived, Derived, Derived, Derived, model, Null, E("GRAPH_VIEW"), Null);
            metre = f.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), Null, E("METRE"));
            var unitList = new List<string>
            {
                metre,
                f.Add("IFCSIUNIT", Derived, E("AREAUNIT"), Null, E("SQUARE_METRE")),
                f.Add("IFCSIUNIT", Derived, E("VOLUMEUNIT"), Null, E("CUBIC_METRE")),
                f.Add("IFCSIUNIT", Derived, E("PLANEANGLEUNIT"), Null, E("RADIAN")),
            };
            // The structural analysis view's forces: N, N/m and N/m² (docs/plan-fem.md §8.7), only when it is written.
            if (o.Structural) unitList.AddRange(StructuralUnits());
            // The estimate's currency (docs/plan-4d5d.md, stage 6): the money its IfcCostValues are in.
            if (s.Catalog.All<IIfcControls>().SelectMany(c => c.CostSchedules(s.Reader)).Select(c => c.Currency).FirstOrDefault() is { Length: > 0 } currency)
                unitList.Add(f.Add("IFCMONETARYUNIT", S(currency)));
            var units = f.Add("IFCUNITASSIGNMENT", L(unitList));
            projectRef = f.Add("IFCPROJECT", Gid(project.Id), Null, S(project.Name), Null, Null, S(project.Str("longName")), S(project.Str("phase")), L(new[] { model }), units);
            // What the project itself holds that an import rebuilds: its location and its phases (the drafting standards and views stay in .lapin).
            // Its level datum's ±0.00 storey by the storey's GlobalId (decision 867), which an import finds again.
            var intentProject = project;
            if (project.Attrs[LevelDatum.Key] is System.Text.Json.Nodes.JsonObject ld && (string?)ld["zeroStorey"] is { } zs)
            {
                var pa = project.CloneAttrs();
                var lo = (System.Text.Json.Nodes.JsonObject)ld.DeepClone();
                lo["zeroStorey"] = GlobalIdOf(zs);
                pa[LevelDatum.Key] = lo;
                intentProject = project.With(attrs: pa);
            }
            Define(projectRef, IntentOf(intentProject, Gid(project.Id, IntentKey), null, only: ProjectIntent), Gid(project.Id, "intent-rel"));
            // The project's information and the user's own parameters of it (decision 340): Lapin's own set, never a Pset_.
            if (ProjectInformation() is { Count: > 0 } info)
                Define(projectRef, f.Add("IFCPROPERTYSET", Gid(project.Id, ProjectInfoSetName), Null, S(ProjectInfoSetName), Null, L(info)), Gid(project.Id, ProjectInfoSetName + "-rel"));
            // Bonsai's drawing styles beside the file: the stylesheet and patterns its new drawings take (BBIM_Documentation, paths relative to the file).
            if (o.BonsaiStyles)
            {
                var (css, patterns) = BonsaiStyles.FileNames(IfcName);
                var doc = new[] { Single(BonsaiStyles.StylesheetKey, Typed("IFCTEXT", S(css))), Single(BonsaiStyles.PatternsKey, Typed("IFCTEXT", S(patterns))) };
                Define(projectRef, f.Add("IFCPROPERTYSET", Gid(project.Id, BonsaiStyles.SetName), Null, S(BonsaiStyles.SetName), Null, L(doc)), Gid(project.Id, BonsaiStyles.SetName + "-rel"));
            }

            var site = m.OfClass(IfcClass.Site).FirstOrDefault();
            var building = Spatial.Building(m);
            var basePoint = Location.Base(m);
            sitePlace = Place(null, 0);
            var siteId = site?.Id ?? project.Id;
            // RefLatitude and RefLongitude: the base point's place, when the project knows it (its latitude and longitude, or its system and survey point).
            var latLon = GeoRef.BasePlace(m);
            siteRef = f.Add("IFCSITE", site != null ? Gid(site.Id) : Gid(project.Id, "site"), Null, S(site?.Name ?? "Site"), Null, Null, sitePlace, Null, Null, E("ELEMENT"),
                latLon is { } pl ? CompoundAngle(pl.Lat) : Null, latLon is { } pl2 ? CompoundAngle(pl2.Lon) : Null, R(basePoint.Elevation), Null, Null);
            refIdOf[siteRef] = siteId;
            var buildingPlace = Place(sitePlace, 0);
            var buildingId = building?.Id ?? project.Id;
            buildingRef = f.Add("IFCBUILDING", building != null ? Gid(building.Id) : Gid(project.Id, "building"), Null, S(building?.Name ?? "Building"), Null, Null, buildingPlace, Null, Null, E("ELEMENT"),
                // ElevationOfRefHeight: the altitude of the model's 0 (decision 867), when the project has one.
                Math.Abs(basePoint.Elevation) > 1e-12 ? R(basePoint.Elevation) : Null, Null, Null);
            f.Add("IFCRELAGGREGATES", SpatialGid(project.Id, IfcClass.RelAggregates, site?.Id ?? "", project.Id, "aggregates"), Null, Null, Null, projectRef, L(new[] { siteRef }));
            f.Add("IFCRELAGGREGATES", SpatialGid(siteId, IfcClass.RelAggregates, building?.Id ?? "", siteId, "aggregates/building"), Null, Null, Null, siteRef, L(new[] { buildingRef }));

            var storeys = Spatial.Storeys(m).ToList();
            foreach (var st in storeys)
            {
                var z = st.Num("elevation");
                var place = Place(buildingPlace, z);
                storeyPlace[st.Id] = place;
                storeyZ[st.Id] = z;
                var r = f.Add("IFCBUILDINGSTOREY", Gid(st.Id), Null, S(st.Name), Null, Null, place, Null, Null, E("ELEMENT"), R(z));
                Define(r, IntentOf(st, Gid(st.Id, IntentKey), null), Gid(st.Id, "intent-rel"));
                storeyRef[st.Id] = r;
                refIdOf[r] = st.Id;
            }
            if (storeys.Count > 0) f.Add("IFCRELAGGREGATES", SpatialGid(buildingId, IfcClass.RelAggregates, storeys[0].Id, buildingId, "aggregates/storeys"), Null, Null, Null, buildingRef, L(storeys.Select(st => storeyRef[st.Id])));
            StoreyCount = storeys.Count;
            MapConversion();
        }

        /// <summary>
        /// The project's tie to the map (<see cref="GeoRef.Tie"/>: the survey point, or the base point's latitude and longitude)
        /// as IfcMapConversion from the Model context to an IfcProjectedCRS, after buildingSMART's User Guide for
        /// Geo-referencing in IFC: the map coordinates and height of the plan's origin, the plan's X axis on the map (the
        /// base point's angle to true north), scale 1; the system by its EPSG name with its datum, projection, zone and
        /// unit (the metre). The geometry stays where it is. Nothing when nothing ties the project to the map.
        /// </summary>
        void MapConversion()
        {
            if (GeoRef.Tie(m) is not { } tie) return;
            var known = tie.Known;
            var crs = f.Add("IFCPROJECTEDCRS", S(tie.Name), known != null ? S(known.Name) : Null, known != null ? S(known.Datum) : Null, Null,
                known != null ? S(known.Projection) : Null, tie.Zone != null ? S(tie.Zone) : Null, metre);
            // IFC 4.3 adds ScaleY and ScaleZ (unset: the same as Scale).
            var scales = o.Ifc43 ? new[] { R(1), Null, Null } : new[] { R(1) };
            f.Add("IFCMAPCONVERSION", new[] { model, crs, R(tie.Eastings), R(tie.Northings), R(tie.Height), R(tie.XAxisAbscissa), R(tie.XAxisOrdinate) }.Concat(scales).ToArray());
            if (tie.Utm) Notes.Add($"The project has a latitude and longitude but no projected coordinate system: the map conversion is in its UTM zone ({tie.Name}).");
            else if (known is { Geographic: true }) Notes.Add($"The project's coordinate system {known.Id} is not a projected one: the map conversion needs eastings and northings (Manage → Location).");
            else if (!tie.Derived && known != null && Math.Abs(tie.Eastings) < 1 && Math.Abs(tie.Northings) < 1)
                Notes.Add("The survey point's eastings and northings are 0: the model is placed at the origin of the coordinate system (Manage → Location).");
        }

        /// <summary>An angle in degrees as IFC's IfcCompoundPlaneAngleMeasure: degrees, minutes, seconds and millionths of a second, all of the angle's sign.</summary>
        static string CompoundAngle(double degrees)
        {
            var micro = (long)Math.Round(Math.Abs(degrees) * 3600e6);
            var (d, rest) = Math.DivRem(micro, 3600L * 1000000);
            var (mi, rest2) = Math.DivRem(rest, 60L * 1000000);
            var (se, mu) = Math.DivRem(rest2, 1000000L);
            var sign = degrees < 0 ? -1 : 1;
            return L(new[] { d, mi, se, mu }.Select(v => (sign * v).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        /// <summary>The project's grid on the lowest storey: the axes that run up the plan as U, the others as V.</summary>
        void Grids()
        {
            var storey = Spatial.Storeys(m).FirstOrDefault();
            if (storey == null) return;
            foreach (var g in m.OfClass(IfcClass.Grid))
            {
                var axes = (g.Attrs["axes"] as JsonArray)?.OfType<JsonObject>()
                    .Select(o => (Tag: (string?)o["tag"] ?? "?", A: Pt(o["a"]), B: Pt(o["b"])))
                    .Where(x => x.A != null && x.B != null && (x.B!.Value - x.A!.Value).Length > 1e-9)
                    .Select(x => (x.Tag, A: x.A!.Value, B: x.B!.Value)).ToList() ?? [];
                var u = axes.Where(x => Math.Abs(x.B.Y - x.A.Y) >= Math.Abs(x.B.X - x.A.X)).ToList();
                var v = axes.Where(x => Math.Abs(x.B.Y - x.A.Y) < Math.Abs(x.B.X - x.A.X)).ToList();
                if (u.Count == 0 || v.Count == 0)
                {
                    if (axes.Count > 0) Notes.Add("The grid needs axes in two directions to be written as an IfcGrid.");
                    continue;
                }
                string Axis((string Tag, Vec2 A, Vec2 B) x) => f.Add("IFCGRIDAXIS", S(x.Tag), f.Add("IFCPOLYLINE", L(new[] { P2(x.A), P2(x.B) })), True);
                var rectangular = u.All(x => Math.Abs(x.B.X - x.A.X) < 1e-6) && v.All(x => Math.Abs(x.B.Y - x.A.Y) < 1e-6);
                var grid = f.Add("IFCGRID", Gid(g.Id), Null, S(g.Name), Null, Null, Place(storeyPlace[storey.Id], 0), Null, L(u.Select(Axis)), L(v.Select(Axis)), Null, E(rectangular ? "RECTANGULAR" : "IRREGULAR"));
                Contain(storeyRef[storey.Id], grid);
                Define(grid, IntentOf(g, Gid(g.Id, IntentKey), null), Gid(g.Id, "intent-rel"));
            }
        }

        // ─── Elements ───────────────────────────────────────────────────────

        bool IsElement(Entity e) => (s.Catalog.Geometry(e.Class) != null || StoredBodies.Has(e)) && IsElementClass(e);

        bool IsElementClass(Entity e) =>
            e.Class is not (IfcClass.OpeningElement or IfcClass.Grid or IfcClass.Annotation)
            && (s.Catalog.Class(e.Class)?.Kind ?? "element") == "element";

        /// <summary>Whether the element exists at the end of the phase asked for (as the phases module sees it: created before or in it, not demolished before it).</summary>
        bool InPhase(Entity e)
        {
            if (o.Phase == null) return true;
            var phases = ((project.Attrs["phases"] as JsonObject)?["phases"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            int Index(string? id) => id == null ? -1 : phases.FindIndex(p => (string?)p["id"] == id);
            var v = Index(o.Phase);
            if (v < 0) return true;
            var c = Index(e.Str("phaseCreated"));
            if (c < 0) c = Math.Max(0, phases.FindIndex(p => (string?)p["kind"] == "construction"));
            var d = Index(e.Str("phaseDemolished"));
            return c <= v && (d < 0 || d >= v);
        }

        /// <summary>
        /// The element's IFC Status (Pset_…Common.Status, IFC4: NEW, EXISTING, DEMOLISH, TEMPORARY) from its phases, as the
        /// phases module sees them: created and struck in one phase temporary; demolished (in the phase exported, or at all
        /// when none is) demolish; created in an existing phase, or before the phase exported, existing; else new. Null for an
        /// element without phases.
        /// </summary>
        string? PhaseStatus(Entity e)
        {
            var created = e.Str("phaseCreated");
            var demolished = e.Str("phaseDemolished");
            if (created == null && demolished == null) return null;
            if (created != null && created == demolished) return "TEMPORARY";
            var phases = ((project.Attrs["phases"] as JsonObject)?["phases"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            int Index(string? id) => id == null ? -1 : phases.FindIndex(p => (string?)p["id"] == id);
            var v = Index(o.Phase);
            var d = Index(demolished);
            if (d >= 0 && (v < 0 || d <= v)) return "DEMOLISH";
            var c = Index(created);
            if (c < 0) c = phases.FindIndex(p => (string?)p["kind"] == "construction");
            return c >= 0 && ((string?)phases[c]["kind"] == "existing" || v >= 0 && c < v) ? "EXISTING" : "NEW";
        }

        string PhaseName(string id) =>
            ((project.Attrs["phases"] as JsonObject)?["phases"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(p => (string?)p["id"] == id) is { } p ? (string?)p["name"] ?? id : id;

        void WriteElements()
        {
            var voids = s.Catalog.Get<IVoidShapes>();
            // Solids kept hidden behind the layered wall made of them (forum topic 101) are not written: the wall is.
            var elements = m.Entities.Where(e => IsElement(e) && InPhase(e) && o.Exclude?.Contains(e.Id) != true && !Superseded.Hidden(m, e)).ToList();
            // Bodies to merge or simplify are prepared a batch at a time on every core, then written in order.
            var prepare = o.Tolerance > 0 || o.FaceSet;
            const int batch = 1000;
            for (var from = 0; from < elements.Count; from += batch)
            {
                var some = elements.GetRange(from, Math.Min(batch, elements.Count - from));
                var geos = new ElementGeometry?[some.Count];
                if (prepare)
                {
                    for (var i = 0; i < some.Count; i++)
                        if (some[i].Class != IfcClass.Space)
                            try { geos[i] = s.Geometry(some[i].Id, new GeometryOptions(CutOpenings: false)); }
                            catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException) { geos[i] = null; }
                    var solids = geos.SelectMany(g => g?.Parts ?? []).Select(p => p.Solid).Where(x => x.Faces.Length > SimplifyFrom).Distinct(ReferenceEqualityComparer.Instance).Cast<Solid>().ToList();
                    Parallel.ForEach(solids, x => simplified[x] = Simplifier.Simplify(x, Math.Max(0, o.Tolerance)));
                }
                for (var i = 0; i < some.Count; i++)
                {
                    var e = some[i];
                    try { Element(e, voids, geos[i]); }
                    catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException)
                    {
                        Notes.Add($"„{e.Name}” was not exported: {ex.Message}");
                    }
                }
                simplified.Clear();
            }
        }

        /// <summary>
        /// Assemblies of separate elements (a Studio box with its bottom and top, docs/plan-studio-thick.md): an
        /// IfcElementAssembly without a body of its own, in the storey, aggregating its parts (IfcRelAggregates); the parts,
        /// written as elements already, are then no longer contained in the storey themselves, as IFC has parts.
        /// </summary>
        void Assemblies()
        {
            foreach (var asm in m.OfClass("IfcElementAssembly").Where(e => !written.ContainsKey(e.Id) && !StoredBodies.Has(e) && InPhase(e)).ToList())
            {
                var parts = m.RelatedOf(asm.Id, IfcClass.RelAggregates).Select(p => written.GetValueOrDefault(p.Id)).OfType<string>().ToList();
                if (parts.Count == 0) continue;
                var storey = Spatial.StoreyOf(m, asm.Id) ?? m.RelatedOf(asm.Id, IfcClass.RelAggregates).Select(p => Spatial.StoreyOf(m, p.Id)).FirstOrDefault(s => s != null);
                var (spatial, relTo) = storey != null && storeyRef.TryGetValue(storey.Id, out var sr) ? (sr, storeyPlace[storey.Id]) : (siteRef, sitePlace);
                var kind = new Kind("IfcElementAssembly", asm.PredefinedType ?? "USERDEFINED", asm.Str("objectType"));
                var entity = Product(kind, Gid(asm.Id), asm.Name, kind.ObjectType, Place(relTo, 0), Null, asm.Id);
                written[asm.Id] = entity;
                foreach (var list in contained.Values) list.RemoveAll(parts.Contains);
                Contain(spatial, entity);
                Elements++;
                f.Add("IFCRELAGGREGATES", Gid(asm.Id, "parts"), Null, Null, Null, entity, L(parts));
            }
        }

        /// <summary>
        /// A curtain wall's doors (report #133): elements of their own, written as such, are parts of their wall (IfcRelAggregates, as
        /// IFC decomposes a curtain wall) and no longer contained in the storey themselves. The same for a placed Studio part's native
        /// members (audit S6: its walls, floors, furniture…): each written as its own class, parts of the placed part.
        /// </summary>
        void CurtainWallParts()
        {
            foreach (var cw in m.OfClass("IfcCurtainWall").Concat(m.OfClass(IfcClass.BuildingElementProxy).Where(e => e.Str("operation") == "part" && e.Attrs["memberIds"] is JsonObject)))
            {
                var parts = m.RelatedOf(cw.Id, IfcClass.RelAggregates).Select(p => written.GetValueOrDefault(p.Id)).OfType<string>().ToList();
                if (parts.Count == 0) continue;
                if (!written.TryGetValue(cw.Id, out var whole))
                {
                    // A part of members only has no body of its own: an IfcElementAssembly over them, as a Studio box's.
                    if (cw.Class == "IfcCurtainWall" || !InPhase(cw)) continue;
                    var storey = Spatial.StoreyOf(m, cw.Id);
                    var (spatial, relTo) = storey != null && storeyRef.TryGetValue(storey.Id, out var sr) ? (sr, storeyPlace[storey.Id]) : (siteRef, sitePlace);
                    var kind = new Kind("IfcElementAssembly", "USERDEFINED", m.RelatingOf(cw.Id, IfcClass.RelDefinesByType)?.Name ?? cw.Name);
                    whole = Product(kind, Gid(cw.Id), cw.Name, kind.ObjectType, Place(relTo, 0), Null, cw.Id);
                    written[cw.Id] = whole;
                    Contain(spatial, whole);
                    Elements++;
                }
                foreach (var list in contained.Values) list.RemoveAll(parts.Contains);
                f.Add("IFCRELAGGREGATES", Gid(cw.Id, "elements"), Null, Null, Null, whole, L(parts));
            }
        }

        /// <summary>The bodies of the batch being written, merged or simplified ahead (<see cref="WriteElements"/>).</summary>
        readonly System.Collections.Concurrent.ConcurrentDictionary<Solid, Solid> simplified = new(ReferenceEqualityComparer.Instance);

        void Element(Entity e, IVoidShapes? voids, ElementGeometry? prepared = null)
        {
            if (e.Class == IfcClass.Space) { Space(e); return; }
            if (written.ContainsKey(e.Id)) return;
            var geo = prepared ?? s.Geometry(e.Id, new GeometryOptions(CutOpenings: false));
            var parts = geo?.Parts.Where(p => p.Solid.Faces.Length > 0).ToList() ?? [];
            // What an element hosts that is an element of its own (a wall sweep, decision 297) says its host, so an import puts it back on it.
            var hostedBy = e.Class != IfcClass.OpeningElement && m.RelatingOf(e.Id, IfcClass.RelVoidsElement) is { } hostOf ? hostOf : null;
            if (parts.Count == 0)
            {
                if (hostedBy != null) return;
                if (RoomDividers.Is(e) && geo?.Plan is { Count: > 0 } lines) { Virtual(e, lines); return; }
                // A feature line on the site (docs/plan-site-grading.md §4): an IfcAnnotation, its line in space as its axis.
                if (s.Catalog.All<IIfcAnnotationCurves>().Select(x => x.CurveOf(m, e)).FirstOrDefault(x => x != null) is { } curve) { Annotation(e, curve); return; }
                if (geo?.Diagnostics is { Count: > 0 } d) Notes.Add($"„{e.Name}” has no body and was not exported: {d[0]}");
                return;
            }
            var storey = Spatial.StoreyOf(m, e.Id) ?? HostStorey(e);
            var (spatial, relTo, z0) = storey != null && storeyRef.TryGetValue(storey.Id, out var sr)
                ? (sr, storeyPlace[storey.Id], storeyZ[storey.Id])
                : (siteRef, sitePlace, 0.0);
            // IFC 4.3: a track, a turnout, a platform stand in a railway facility's part, not on a storey (their heights absolute),
            // placed along their alignment's curve where the module says where (IfcLinearPlacement), their body in that frame.
            var railPart = o.Ifc43 ? RailwayPart(e) : null;
            if (railPart is { } part) (spatial, relTo, z0) = (part, sitePlace, 0.0);
            // IFC 4.3: an abutment stands in a bridge's part of its kind (IfcBridgePart ABUTMENT), its heights absolute.
            else if (o.Ifc43 && BridgePart(e) is { } bridgePart) (spatial, relTo, z0) = (bridgePart, sitePlace, 0.0);
            var fr = Frame.AtZ(z0);
            string? place = null;
            if (railPart != null && LinearPlace(e) is { } along) (place, fr) = along;
            var kind = KindOf(e);
            var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
            // A roof's slopes, each an element of its own when asked (issue #139): the pieces of its covering, deck and layers in their
            // part's place, keyed "covering:slope:1" (a GlobalId of the roof's and the slope's), of the part's material and colour.
            var slopes = o.RoofSlopes && kind.Class == IfcClass.Roof && e.Attrs[IfcSource.Key] == null && parts.Any(p => p.Pieces is { Count: > 1 });
            if (slopes) parts = [.. parts.SelectMany(p => p.Pieces is { Count: > 1 } pcs ? pcs.Select(pc => new GeometryPart(pc.Name, p.Color, pc.Solid, p.Material, $"{p.Key}:{pc.Key}")) : [p])];
            // A wall of layers only is one body with its layer set; one with zones, lintels or a ring beam (a roof with its planes and timber, a footing with its sole) is a parent over its parts.
            // A door's frame and leaf, a column's body, a terrain: one element. Only a wall, a roof and a footing hold parts that are elements of their own.
            var grouped = slopes || o.Grouped && (kind.Class is IfcClass.Wall or IfcClass.Roof or IfcClass.Footing or "IfcCurtainWall" or IfcClass.Stair or IfcClass.Ramp or "IfcElementAssembly"
                || kind.Class == IfcClass.Slab && parts.Any(p => p.Key?.StartsWith("clt:", StringComparison.Ordinal) == true || p.Key?.StartsWith("wood:", StringComparison.Ordinal) == true || p.Key?.StartsWith("bb-", StringComparison.Ordinal) == true)) && parts.Count > 1 && !parts.All(p => (p.Key ?? "").StartsWith("layer:", StringComparison.Ordinal));
            // A door or window: placed with its local +Y the way its panels open, which IFC's left and right are seen along.
            var joinery = e.Class is IfcClass.Door or IfcClass.Window && !grouped ? s.Catalog.Get<IJoineryIfc>() : null;
            var at = joinery?.Place(m, e);
            // A railway element already has its linear placement and frame; the rest stand on their storey (or in the joinery's frame).
            place ??= at is { } jp ? JoineryPlace(relTo, jp, z0) : Place(relTo, 0);
            // A cable carrier's fittings (report #147): its bends, tees, crosses and reducers, IfcCableCarrierFittings aggregated in it; its body the rest.
            var fittings = o.Grouped && !grouped && kind.Class == "IfcCableCarrierSegment" ? parts.Where(p => p.Key?.StartsWith("fitting:", StringComparison.Ordinal) == true).ToList() : [];
            if (fittings.Count > 0 && fittings.Count < parts.Count) parts = [.. parts.Except(fittings)];
            else fittings = [];
            var reps = new List<string>();
            // A type with a body of its own (a part): its map written before the type is, whatever this element's body.
            var typeMap = type != null ? TypeMap(type) : null;
            // A body its module gives as constructive solids (a steel member: its profile swept, cut by its ends' planes, decision 268).
            var swept = !grouped && at == null && railPart == null ? SweptOf(e) : null;
            if (swept != null && !SweepIsBody(swept, parts)) swept = null;
            if (swept != null) reps.Add(SweptRep(swept, parts[0].Color, fr));
            // An instance of a shared body: the body once, as a representation map, each instance mapping it where it stands.
            // A part's (decision 520 P3): the type's map when it is built with its type's values, else its own body.
            else if (!grouped && at == null && railPart == null && geo!.Instance is { } inst && (inst.Local == null || (typeMap != null && maps.GetValueOrDefault(inst.Body) == typeMap))
                && Mapped(inst, z0) is { } mapped) reps.Add(mapped);
            else if (!grouped && at == null && railPart == null && o.AdvancedBrep && parts.Count == 1 && ExactOf(e, parts[0].Solid) is { } exact)
                reps.Add(AdvancedRep(exact, StyleKey(parts[0]), fr));
            else if (!grouped) reps.Add(BodyOf(parts.Select(p => at is { } jp2 ? Brep(p.Solid, StyleKey(p), v => Local(jp2, v)) : Brep(p.Solid, StyleKey(p), fr))));
            // A wall over its parts (a layer, a lintel, a zone) keeps a body of its own too (issue #29): readers that colour or measure the
            // wall find faces on it. Its items are its parts' own (each written once, the parts placed where the wall is), a part of many
            // copies written whole here; Lapin's import takes the parts and leaves this body (IfcImport, a parent it wrote).
            List<string?>? partItems = null;
            if (grouped && kind.Class == IfcClass.Wall)
            {
                partItems = [.. parts.Select(p => p.Unit != null && p.Instances is { Count: > 0 } ? null : Brep(p.Solid, StyleKey(p), fr))];
                reps.Add(BodyOf(parts.Select((p, i) => partItems[i] ?? Brep(p.Solid, StyleKey(p), fr))));
            }
            if (AxisOf(e) is { } ax) reps.Add(ax);
            if (ArcFootprint(e) is { } fp) reps.Add(fp);
            else if (ArcAxis(e) is { } aa) reps.Add(aa);
            reps.AddRange(Plan2dOf(e));
            // A light fixture's light as IFC's light source (docs/plan-render.md, S2).
            if (LightRep(e, geo, fr) is { } light) reps.Add(light);
            var shape = reps.Count > 0 ? f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(reps)) : Null;
            var (height, width) = Overall(e, type);
            // Its type in IFC's terms (the mirrored type for an element that is its type's mirror image), which the element repeats.
            var mirrored = at?.Mirrored == true;
            var terms = type != null && joinery != null ? joinery.Type(m, type, mirrored) : null;
            // A foreign element: its class and attributes as its file wrote them.
            var src = e.Attrs[IfcSource.Key] as JsonObject;
            // Its Tag: its own mark (IFC's identifier at the occurrence, forum topic 177), else its category's rule's (decision 737), else its id as before.
            var entity = (src != null ? Carried(src, e, place, shape) : null) ?? Product(kind, Gid(e.Id), e.Name, kind.ObjectType ?? type?.Name, place, shape, (e.Str("mark") ?? MarkRules.Evaluate(m, e)) is { Length: > 0 } ownMark ? ownMark : e.Id, height, width, terms, operationOfType: type?.Str("ifcOperation"));
            written[e.Id] = entity;
            if (at == null) placeOf[e.Id] = (place, z0);
            Contain(spatial, entity);
            Elements++;
            if (type != null) TypeOf(type, kind, entity, mirrored, terms, retyped: e.Str("exportAs") is { Length: > 3 } ea && ea == kind.Class && ea != e.Class);
            if (src?["material"] == null) MaterialsOf(e, entity, parts, grouped, type != null && types.TryGetValue(mirrored ? type.Id + MirrorKey : type.Id, out var written2) ? (type.Id + (mirrored ? MirrorKey : ""), written2.Ref) : null);
            if (grouped) Parts(e, kind, parts, place, fr, entity, partItems);
            if (fittings.Count > 0) Parts(e, kind, fittings, place, fr, entity);
            Openings(e, entity, place, z0, voids);
            KeptOpenings(e, entity, place, z0);
            if (swept != null) SweptOpenings(e, swept, entity, place, fr);
            if (swept is { Parts.Count: > 0 }) SweptParts(e, swept, entity, spatial, place, fr, parts);
            Properties(e, kind, entity, type, hostedBy != null ? [("LapinHost", hostedBy.Id)] : null);
            Quantities(e, entity, grouped);
        }

        /// <summary>
        /// An outline with curved edges (a floor's, a ceiling's; forum topic 97) as its FootPrint beside the body's facets: an
        /// IfcIndexedPolyCurve of its corners (storey-local, the element's placement) and each arc's middle, its straight edges
        /// IfcLineIndex and its arcs IfcArcIndex (start, a point on the arc, end), the true curve the body was laid in chords from.
        /// </summary>
        string? ArcFootprint(Entity e)
        {
            // Its own holes (audit S4) too: each a curve of the footprint after the outline's.
            var holes = OutlineHoles.Of(e);
            if ((e.Attrs[ArcOutline.Key] is not JsonArray && e.Attrs[EllipseEdge.Key] is not JsonArray && holes.Count == 0) || e.Attrs["contour"] is not JsonArray) return null;
            var corners = Args.StoredPoints(e.Attrs["contour"]);
            var rises = ArcOutline.Rises(e.Attrs[ArcOutline.Key], corners.Count);
            var ellipses = EllipseEdge.Read(e.Attrs[EllipseEdge.Key], corners.Count);
            if (corners.Count < 3 || (rises == null && ellipses == null && holes.Count == 0)) return null;
            if (footprint == "") footprint = f.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("FootPrint"), S("Model"), Derived, Derived, Derived, Derived, model, Null, E("PLAN_VIEW"), Null);
            return f.Add("IFCSHAPEREPRESENTATION", footprint, S("FootPrint"), S("Curve2D"), L(holes.Select(h => OutlineLoop(h.Corners, h.Rises, h.Ellipses)).Prepend(OutlineLoop(corners, rises, ellipses))));
        }

        /// <summary>
        /// A closed outline as its true curve: an IfcIndexedPolyCurve when its edges are lines and arcs; with an edge along an ellipse
        /// (audit S4), an IfcCompositeCurve of its edges, each a polyline, or an IfcTrimmedCurve on an IfcCircle or an IfcEllipse
        /// trimmed by parameter (radians), run its own way.
        /// </summary>
        string OutlineLoop(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, IReadOnlyList<EllipseEdge?>? ellipses)
        {
            if (ellipses == null || ellipses.All(x => x == null)) return IndexedLoop(corners, rises);
            var segments = new List<string>();
            var n = corners.Count;
            for (var i = 0; i < n; i++)
            {
                var (a, b) = (corners[i], corners[(i + 1) % n]);
                string parent;
                if (i < ellipses.Count && ellipses[i] is { } el)
                {
                    var (t0, sweep) = el.Span(a, b);
                    var dir = el.M * (1 / el.A);
                    var place = f.Add("IFCAXIS2PLACEMENT2D", P2(el.C), f.Add("IFCDIRECTION", L(new[] { R(dir.X), R(dir.Y) })));
                    var basis = f.Add("IFCELLIPSE", place, R(el.A), R(el.B));
                    parent = f.Add("IFCTRIMMEDCURVE", basis, L(new[] { Typed("IFCPARAMETERVALUE", R(t0)) }), L(new[] { Typed("IFCPARAMETERVALUE", R(t0 + sweep)) }), sweep > 0 ? Step.True : False, E("PARAMETER"));
                }
                else
                {
                    var c = ArcOutline.Edge(corners, rises, i);
                    if (c.IsArc)
                    {
                        var (sa, ea) = (Math.Atan2(a.Y - c.Centre.Y, a.X - c.Centre.X), Math.Atan2(b.Y - c.Centre.Y, b.X - c.Centre.X));
                        var basis = f.Add("IFCCIRCLE", f.Add("IFCAXIS2PLACEMENT2D", P2(c.Centre), Null), R(c.Radius));
                        // A rise to the right turns anticlockwise.
                        parent = f.Add("IFCTRIMMEDCURVE", basis, L(new[] { Typed("IFCPARAMETERVALUE", R(sa)) }), L(new[] { Typed("IFCPARAMETERVALUE", R(ea)) }), c.Rise < 0 ? Step.True : False, E("PARAMETER"));
                    }
                    else parent = f.Add("IFCPOLYLINE", L(new[] { P2(a), P2(b) }));
                }
                segments.Add(f.Add("IFCCOMPOSITECURVESEGMENT", E("CONTINUOUS"), Step.True, parent));
            }
            return f.Add("IFCCOMPOSITECURVE", L(segments), False);
        }

        /// <summary>
        /// A closed outline whose edges may be arcs as an IfcIndexedPolyCurve: its corners and each arc's middle, its straight edges
        /// IfcLineIndex and its arcs IfcArcIndex (start, a point on the arc, end).
        /// </summary>
        string IndexedLoop(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises)
        {
            var pts = new List<Vec2>(corners);
            var segments = new List<string>();
            for (var i = 0; i < corners.Count; i++)
            {
                var (a, b) = (i + 1, (i + 1) % corners.Count + 1);
                var c = ArcOutline.Edge(corners, rises, i);
                if (!c.IsArc) { segments.Add(Typed("IFCLINEINDEX", L(new[] { a, b }))); continue; }
                pts.Add(c.PointAt(c.Length / 2));
                segments.Add(Typed("IFCARCINDEX", L(new[] { a, pts.Count, b })));
            }
            var list = f.Add("IFCCARTESIANPOINTLIST2D", L(pts.Select(p => L(new[] { R(p.X), R(p.Y) }))), Null);
            return f.Add("IFCINDEXEDPOLYCURVE", list, L(segments), False);
        }

        /// <summary>
        /// An open line with curved segments (a railing's, a user's request: <c>points</c> and each segment's rise in <c>arcs</c>)
        /// as its Axis beside the body's facets: an IfcIndexedPolyCurve of its points (storey-local) and each arc's middle, its
        /// straight segments IfcLineIndex and its arcs IfcArcIndex, the true curve the body was laid in chords from.
        /// </summary>
        string? ArcAxis(Entity e)
        {
            if (e.Attrs[ArcOutline.Key] is not JsonArray || e.Attrs["contour"] != null || e.Attrs["points"] is not JsonArray || s.Catalog.IsPath(e.Class)) return null;
            var corners = Args.StoredPoints(e.Attrs["points"]);
            if (corners.Count < 2 || ArcOutline.Rises(e.Attrs[ArcOutline.Key], corners.Count - 1) is not { } rises) return null;
            var pts = new List<Vec2>(corners);
            var segments = new List<string>();
            for (var i = 0; i + 1 < corners.Count; i++)
            {
                var c = new PathCurve(corners[i], corners[i + 1], rises[i]);
                if (!c.IsArc) { segments.Add(Typed("IFCLINEINDEX", L(new[] { i + 1, i + 2 }))); continue; }
                pts.Add(c.PointAt(c.Length / 2));
                segments.Add(Typed("IFCARCINDEX", L(new[] { i + 1, pts.Count, i + 2 })));
            }
            var list = f.Add("IFCCARTESIANPOINTLIST2D", L(pts.Select(p => L(new[] { R(p.X), R(p.Y) }))), Null);
            return f.Add("IFCSHAPEREPRESENTATION", axis, S("Axis"), S("Curve2D"), L(new[] { f.Add("IFCINDEXEDPOLYCURVE", list, L(segments), False) }));
        }

        /// <summary>
        /// An element with no body that is a curve in space (a feature line, T2): an IfcAnnotation in its storey, its curves as the Axis
        /// representation (Curve3D, storey-local metres), IFC 4.3's PredefinedType (CONTOURLINE, USERDEFINED with its object type; IFC4
        /// has none) and its Lapin_Intent, so Lapin reads it back as the element it was.
        /// </summary>
        void Annotation(Entity e, IfcAnnotationCurve curve)
        {
            var storey = Spatial.StoreyOf(m, e.Id);
            var (spatial, relTo, z0) = storey != null && storeyRef.TryGetValue(storey.Id, out var sr) ? (sr, storeyPlace[storey.Id], storeyZ[storey.Id]) : (siteRef, sitePlace, 0.0);
            var curves = curve.Curves.Where(c => c.Count >= 2)
                .Select(c => f.Add("IFCPOLYLINE", L(c.Select(p => f.Add("IFCCARTESIANPOINT", L(new[] { R(p.X), R(p.Y), R(p.Z - z0) }))).ToArray()))).ToArray();
            if (curves.Length == 0) return;
            var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { f.Add("IFCSHAPEREPRESENTATION", axis, S("Axis"), S("Curve3D"), L(curves)) }));
            var objectType = !o.Ifc43 || curve.Predefined == "USERDEFINED" ? curve.ObjectType : null;
            string[] common = [Gid(e.Id), Null, S(e.Name), Null, S(objectType), Place(relTo, 0), shape];
            var entity = f.Add("IFCANNOTATION", o.Ifc43 ? [.. common, E(curve.Predefined)] : common);
            written[e.Id] = entity;
            Contain(spatial, entity);
            Elements++;
            Define(entity, IntentOf(e, Gid(e.Id, IntentKey), new Kind(IfcClass.Annotation, "", objectType)), Gid(e.Id, "intent-rel"));
        }

        /// <summary>
        /// A room separation line (forum topic 80): an IfcVirtualElement in its storey (BOUNDARY in IFC 4.3), no body, its line as the
        /// FootPrint representation (Curve2D, the storey's plan metres), its shape in Lapin_Intent so Lapin reads it back as one.
        /// </summary>
        void Virtual(Entity e, IReadOnlyList<PlanItem> lines)
        {
            var storey = Spatial.StoreyOf(m, e.Id);
            var (spatial, relTo) = storey != null && storeyRef.TryGetValue(storey.Id, out var sr) ? (sr, storeyPlace[storey.Id]) : (siteRef, sitePlace);
            if (footprint == "") footprint = f.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("FootPrint"), S("Model"), Derived, Derived, Derived, Derived, model, Null, E("PLAN_VIEW"), Null);
            var curves = lines.Where(l => l.Points.Count >= 2)
                .Select(l => f.Add("IFCPOLYLINE", L((l.Closed ? [.. l.Points, l.Points[0]] : l.Points.ToList()).Select(P2).ToArray()))).ToArray();
            var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { f.Add("IFCSHAPEREPRESENTATION", footprint, S("FootPrint"), S("Curve2D"), L(curves)) }));
            var kind = KindOf(e);
            var entity = Product(kind, Gid(e.Id), e.Name, kind.ObjectType, Place(relTo, 0), shape, e.Id);
            written[e.Id] = entity;
            Contain(spatial, entity);
            Elements++;
            Properties(e, kind, entity, null);
            Quantities(e, entity, false);
        }

        /// <summary>A hosted element's body as a recess in its host, from the first module that gives one (<see cref="IIfcRecesses"/>).</summary>
        Solid? RecessOf(Entity e)
        {
            foreach (var src in s.Catalog.All<IIfcRecesses>())
                if (src.RecessOf(m, s.Catalog, e) is { } b) return b;
            return null;
        }

        /// <summary>The element's body as it is exactly, from the first module that knows it (<see cref="IIfcExactBodies"/>), checked against its geometry's body.</summary>
        ExactBody? ExactOfPart(Entity e, string key, Solid solid)
        {
            foreach (var src in s.Catalog.All<IIfcExactBodies>())
            {
                try { if (src.ExactOfPart(m, s.Catalog, e, key, solid) is { } b) return b; }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Notes.Add($"A part of „{e.Name}” was written as its facets: {ex.Message}"); }
            }
            return null;
        }

        ExactBody? ExactOf(Entity e, Solid solid)
        {
            foreach (var src in s.Catalog.All<IIfcExactBodies>())
            {
                try { if (src.ExactOf(m, s.Catalog, e, solid) is { } b) return b; }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { Notes.Add($"„{e.Name}” was written as its facets: {ex.Message}"); }
            }
            return null;
        }

        ExactWriter? exactWriter;
        public int ExactBodies;

        /// <summary>An exact body as an IfcAdvancedBrep in the placement's frame, in its colour: an "AdvancedBrep" representation.</summary>
        string AdvancedRep(ExactBody b, string? hex, Frame fr)
        {
            var item = (exactWriter ??= new ExactWriter(f, ifc: true)).Brep(b.Moved(fr.Local, fr.Turn));
            if (hex != null) f.Add("IFCSTYLEDITEM", item, L(new[] { Style(hex) }), Null);
            ExactBodies++;
            return f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S("AdvancedBrep"), L(new[] { item }));
        }

        /// <summary>The element's body as constructive solids, from the first module that gives one (<see cref="IIfcSweptBodies"/>).</summary>
        IfcSweptBody? SweptOf(Entity e)
        {
            foreach (var src in s.Catalog.All<IIfcSweptBodies>())
                if (src.BodyOf(m, s.Catalog, e) is { } b) return b;
            return null;
        }

        /// <summary>The profile definitions written, by what they say, and the SI units their values are in.</summary>
        readonly Dictionary<string, string> profileDefs = [];
        ProfileIfc.SiUnits? profileUnits;

        /// <summary>
        /// Whether a plain sweep is the element's body: one part of its volume and box (a member cut by the project's cut order, or
        /// mitred at a join, is not, and is written as the B-rep it is). Sweeps with cuts of their own say what they are.
        /// </summary>
        static bool SweepIsBody(IfcSweptBody b, List<GeometryPart> parts)
        {
            if (b.Clips.Count > 0 || b.Openings.Count > 0 || b.Parts.Count > 0) return true;
            if (parts.Count != 1 || Measure.Bounds(parts[0].Solid) is not { } box) return false;
            var w = b.Body;
            var y = Vec3.Cross(w.Axis, w.XDir);
            var sweep = w.Direction ?? w.Axis;
            var ends = new[] { w.Origin, w.Origin + sweep * w.Depth };
            var corners = w.Profile.SelectMany(q => ends.Select(o => o + w.XDir * q.X + y * q.Y)).ToList();
            const double Tol = 1e-4;
            bool Near(double a, double c) => Math.Abs(a - c) <= Tol;
            if (!Near(corners.Min(c => c.X), box.Min.X) || !Near(corners.Max(c => c.X), box.Max.X) || !Near(corners.Min(c => c.Y), box.Min.Y)
                || !Near(corners.Max(c => c.Y), box.Max.Y) || !Near(corners.Min(c => c.Z), box.Min.Z) || !Near(corners.Max(c => c.Z), box.Max.Z)) return false;
            var volume = (Math.Abs(Polygon2.SignedArea(w.Profile)) - w.Holes.Sum(h => Math.Abs(Polygon2.SignedArea(h)))) * w.Depth * Math.Abs(Vec3.Dot(sweep, w.Axis));
            return Math.Abs(volume - Measure.Volume(parts[0].Solid)) <= Math.Max(1e-9, volume * 1e-4);
        }

        /// <summary>
        /// A sweep as IFC has one: its profile (an IfcArbitraryClosedProfileDef, its holes as voids) placed on its origin, its
        /// x and its axis in the frame, extruded along the placement's z by its depth (IfcExtrudedAreaSolid).
        /// </summary>
        string Sweep(IfcSweep w, Frame fr)
        {
            string Curve(IReadOnlyList<Vec2> loop) => f.Add("IFCPOLYLINE", L(loop.Append(loop[0]).Select(P2)));
            string Dir(Vec3 n) => f.Add("IFCDIRECTION", L(new[] { R(n.X), R(n.Y), R(n.Z) }));
            var outer = Polygon2.EnsureCcw(w.Profile);
            string Arbitrary(string? name) => w.Holes.Count == 0
                ? f.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), S(name), Curve(outer))
                : f.Add("IFCARBITRARYPROFILEDEFWITHVOIDS", E("AREA"), S(name), Curve(outer), L(w.Holes.Select(h => Curve(Polygon2.EnsureCcw(h).Reverse().ToList()))));
            // Its profile by its parametric subtype, once for all that share it (decision 416); an outline, or a kind IFC has no form for, by its rings.
            string profile;
            if (w.Parametric is { } section)
            {
                var key = section.ToJson().ToJsonString() + (!section.HasIfcForm ? string.Join(' ', outer.Select(q => $"{q.X:0.######},{q.Y:0.######}")) : "");
                if (!profileDefs.TryGetValue(key, out var known))
                {
                    known = ProfileIfc.Parametric(f, section, 1) ?? Arbitrary(section.Name);
                    ProfileIfc.WriteProperties(f, section, known, profileUnits ??= new ProfileIfc.SiUnits(f));
                    profileDefs[key] = known;
                }
                profile = known;
            }
            else profile = Arbitrary(null);
            var o = fr.Local(w.Origin);
            var position = f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", L(new[] { R(o.X), R(o.Y), R(o.Z) })), Dir(fr.Turn(w.Axis)), Dir(fr.Turn(w.XDir)));
            // Oblique (a leaning wall): its direction in the position's own axes (x its XDir, z its Axis).
            if (w.Direction is { } d && Vec3.Cross(d, w.Axis).Length > 1e-9)
            {
                var yd = Vec3.Cross(w.Axis, w.XDir);
                return f.Add("IFCEXTRUDEDAREASOLID", profile, position, f.Add("IFCDIRECTION", L(new[] { R(Vec3.Dot(d, w.XDir)), R(Vec3.Dot(d, yd)), R(Vec3.Dot(d, w.Axis)) })), R(w.Depth));
            }
            return f.Add("IFCEXTRUDEDAREASOLID", profile, position, f.Add("IFCDIRECTION", "(0.,0.,1.)"), R(w.Depth));
        }

        /// <summary>
        /// A swept body cut by its planes: each an IfcHalfSpaceSolid on an IfcPlane whose normal points into the material
        /// (AgreementFlag true: the half-space is what lies behind it, the part taken off), taken off the sweep in turn
        /// (IfcBooleanClippingResult, DIFFERENCE); a "Clipping" representation, or "SweptSolid" with no cut.
        /// </summary>
        string SweptRep(IfcSweptBody b, string? hex, Frame fr)
        {
            string Dir(Vec3 n) => f.Add("IFCDIRECTION", L(new[] { R(n.X), R(n.Y), R(n.Z) }));
            var item = Sweep(b.Body, fr);
            foreach (var c in b.Clips)
            {
                var p = fr.Local(c.Point);
                var plane = f.Add("IFCPLANE", f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", L(new[] { R(p.X), R(p.Y), R(p.Z) })), Dir(fr.Turn(-c.Normal)), Null));
                item = f.Add("IFCBOOLEANCLIPPINGRESULT", E("DIFFERENCE"), item, f.Add("IFCHALFSPACESOLID", plane, True));
            }
            if (hex != null) f.Add("IFCSTYLEDITEM", item, L(new[] { Style(hex) }), Null);
            return f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S(b.Clips.Count > 0 ? "Clipping" : "SweptSolid"), L(new[] { item }));
        }

        /// <summary>A swept body's openings (a steel member's copes): each an IfcOpeningElement voiding the element, its body a sweep, placed where the element is.</summary>
        void SweptOpenings(Entity e, IfcSweptBody b, string entity, string place, Frame fr)
        {
            for (var i = 0; i < b.Openings.Count; i++)
            {
                var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S("SweptSolid"), L(new[] { Sweep(b.Openings[i], fr) })) }));
                var opening = f.Add("IFCOPENINGELEMENT", Gid(e.Id, $"cut{i}"), Null, S("Cope"), Null, Null, Place(place, 0), shape, Null, E("OPENING"));
                f.Add("IFCRELVOIDSELEMENT", Gid(e.Id, $"cut{i}-rel"), Null, Null, Null, entity, opening);
            }
        }

        /// <summary>
        /// A swept body's pieces (a rafter's haunches, decision 270): each an element of its own (an IfcPlate, an IfcMember)
        /// of sweeps, placed where the element is, in its material; the element and its pieces an IfcElementAssembly in the
        /// storey in place of the element.
        /// </summary>
        void SweptParts(Entity e, IfcSweptBody b, string entity, string spatial, string place, Frame fr, List<GeometryPart> parts)
        {
            var refs = new List<string> { entity };
            var color = parts[0].Color;
            var material = parts[0].Material;
            foreach (var p in b.Parts)
            {
                var items = p.Items.Select(w => Sweep(w, fr)).ToList();
                foreach (var item in items) f.Add("IFCSTYLEDITEM", item, L(new[] { Style(color) }), Null);
                var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S("SweptSolid"), L(items)) }));
                var r = Product(new Kind(p.Class, p.Predefined, p.ObjectType), Gid(e.Id, p.Key), p.Name, p.ObjectType, Place(place, 0), shape, $"{e.Id}/{p.Key}");
                if (material != null) UseMaterial(material, r);
                partRefs[(e.Id, p.Key)] = r;
                refs.Add(r);
                PartCount++;
            }
            var asm = f.Add("IFCELEMENTASSEMBLY", Gid(e.Id, "assembly"), Null, S(e.Name), Null, S(b.Assembly), Place(place, 0), Null, S($"{e.Id}/assembly"), E("FACTORY"), E("USERDEFINED"));
            foreach (var list in contained.Values) list.Remove(entity);
            Contain(spatial, asm);
            f.Add("IFCRELAGGREGATES", Gid(e.Id, "assembly-parts"), Null, Null, Null, asm, L(refs));
        }

        /// <summary>
        /// A room as IFC has one: an IfcSpace aggregated into its storey (not
        /// contained), its body its outline extruded up its height (an
        /// IfcExtrudedAreaSolid of an arbitrary profile, its islands as voids),
        /// Name its number and LongName its name (as most tools write them),
        /// ObjectType its function; Pset_SpaceCommon's Reference (its number)
        /// and IsExternal, its Qto_SpaceBaseQuantities, and Lapin's intent.
        /// </summary>
        void Space(Entity e)
        {
            var geo = s.Geometry(e.Id);
            var plan = geo?.Plan ?? [];
            var outer = plan.FirstOrDefault(p => p.Kind == "room")?.Points;
            if (outer is not { Count: >= 3 })
            {
                Notes.Add($"„{e.Name}” has no outline and was not exported.");
                return;
            }
            var islands = plan.Where(p => p.Kind == "roomIsland" && p.Points.Count >= 3).Select(p => p.Points).ToList();
            var storey = Spatial.StoreyOf(m, e.Id);
            var (spatial, relTo, z0) = storey != null && storeyRef.TryGetValue(storey.Id, out var sr) ? (sr, storeyPlace[storey.Id], storeyZ[storey.Id]) : (buildingRef, sitePlace, 0.0);
            var height = geo!.Parts.Select(p => Measure.Bounds(p.Solid)).OfType<(Vec3 Min, Vec3 Max)>().Select(b => b.Max.Z - b.Min.Z).DefaultIfEmpty(storey?.NumOrNull("height") ?? 3).Max();
            string shape;
            if (o.SpaceBrep && geo!.Parts.Count > 0)
            {
                var fr = Frame.AtZ(z0 + e.Num("baseOffset"));
                shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { BodyOf(geo.Parts.Select(p => Brep(p.Solid, StyleKey(p), fr))) }));
            }
            else
            {
                string Curve(IReadOnlyList<Vec2> loop) => f.Add("IFCPOLYLINE", L(loop.Append(loop[0]).Select(P2)));
                // A drawn room with curved edges (audit S4): its profile the true curve, an IfcIndexedPolyCurve with its arcs (kept counter-clockwise).
                var drawnArcs = !e.Bool("auto") ? ArcOutline.RisesOf(e) : null;
                var drawnEllipses = !e.Bool("auto") ? EllipseEdge.Of(e) : null;
                var outerCurve = drawnArcs != null || drawnEllipses != null ? OutlineLoop(ArcOutline.CornersOf(e), drawnArcs, drawnEllipses) : Curve(Polygon2.EnsureCcw(outer));
                var profile = islands.Count == 0
                    ? f.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), Null, outerCurve)
                    : f.Add("IFCARBITRARYPROFILEDEFWITHVOIDS", E("AREA"), Null, outerCurve, L(islands.Select(i => Curve(Polygon2.EnsureCcw(i).Reverse().ToList()))));
                var origin = f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null);
                var solid = f.Add("IFCEXTRUDEDAREASOLID", profile, origin, f.Add("IFCDIRECTION", "(0.,0.,1.)"), R(height));
                f.Add("IFCSTYLEDITEM", solid, L(new[] { Style(geo.Parts.FirstOrDefault()?.Color ?? "#9ec5e8") }), Null);
                shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S("SweptSolid"), L(new[] { solid })) }));
            }
            var place = Place(relTo, e.Num("baseOffset"));
            var number = e.Str("number") is { Length: > 0 } n ? n : null;
            var function = e.Str("function") is { Length: > 0 } fn ? fn : null;
            var entity = f.Add("IFCSPACE", Gid(e.Id), Null, S(number ?? e.Name), Null, S(function), place, shape, S(e.Name), E("ELEMENT"), E("SPACE"), Null);
            written[e.Id] = entity;
            placeOf[e.Id] = (place, z0);
            (spaces.TryGetValue(spatial, out var l) ? l : spaces[spatial] = []).Add(entity);
            Elements++;
            var common = new List<(string Name, string Value)> { ("IsExternal", Typed("IFCBOOLEAN", False)) };
            if (number != null) common.Insert(0, ("Reference", Typed("IFCIDENTIFIER", S(number))));
            Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, "common"), Null, S("Pset_SpaceCommon"), Null, L(CommonWith(e, "Pset_SpaceCommon", common))), Gid(e.Id, "common-rel"));
            Define(entity, IntentOf(e, Gid(e.Id, IntentKey), new Kind(IfcClass.Space, "SPACE", null)), Gid(e.Id, "intent-rel"));
            if (ParamsOf(e, Gid(e.Id, ParamsKey)) is { } set) Define(entity, set, Gid(e.Id, "params-rel"));
            NamedSets(e, entity, "Pset_SpaceCommon", new HashSet<string>());
            Quantities(e, entity, false);
        }

        /// <summary>A door or window stands on its wall's storey.</summary>
        Entity? HostStorey(Entity e) =>
            m.RelatingOf(e.Id, IfcClass.RelFillsElement) is { } opening && m.RelatingOf(opening.Id, IfcClass.RelVoidsElement) is { } host ? Spatial.StoreyOf(m, host.Id) : null;

        Kind KindOf(Entity e)
        {
            var cls = e.Str("exportAs") is { Length: > 3 } x && x.StartsWith("Ifc", StringComparison.Ordinal) ? x : e.Class;
            if (cls == IfcClass.EarthworksCut && !o.Ifc43) return new(IfcClass.BuildingElementProxy, "USERDEFINED", e.PredefinedType == "TRENCH" ? "Trench" : e.Str("objectType") ?? "Earthworks cut");
            // IFC 4.3's earthworks fill (a grading, T5); IFC4 has none: a proxy named by its object type.
            if (cls == "IfcEarthworksFill" && !o.Ifc43) return new(IfcClass.BuildingElementProxy, "USERDEFINED", e.Str("objectType") ?? "Earthworks fill");
            // IFC 4.3's pavement and vegetation; IFC4 has a slab named Pavement and a user-defined geographic element.
            if (cls == IfcClass.Pavement && !o.Ifc43) return new(IfcClass.Slab, "USERDEFINED", "Pavement");
            // A kerb (a hardscape's, site grading T4): IFC 4.3's IfcKerb; IFC4 has none, a proxy named Kerb.
            if (cls == "IfcKerb" && !o.Ifc43) return new(IfcClass.BuildingElementProxy, "USERDEFINED", "Kerb");
            if (cls == IfcClass.GeographicElement && e.PredefinedType == "VEGETATION" && o.Ifc43) return new(cls, "VEGETATION", null);
            string[]? allowed;
            if (o.Ifc43 && Classes43.TryGetValue(cls, out var allowed43)) allowed = allowed43;
            else if (cls == "IfcElementAssembly") allowed = AssemblyTypes4;
            else if (!Classes.TryGetValue(cls, out allowed)) return new(IfcClass.BuildingElementProxy, "USERDEFINED", cls);
            if (allowed == null) return new(cls, "", null);
            var pre = e.PredefinedType?.ToUpperInvariant();
            if (pre == null && cls == IfcClass.Roof)
                pre = e.Str("shape") switch { "flat" => "FLAT_ROOF", "gable" => "GABLE_ROOF", "hip" => "HIP_ROOF", "mansard" => "MANSARD_ROOF", "shed" => "SHED_ROOF", "barrel" => "BARREL_ROOF", _ => null };
            if (pre == null && cls == IfcClass.GeographicElement) pre = "TERRAIN";
            if (pre == null) return new(cls, "NOTDEFINED", null);
            // A user-defined type names itself through the element's objectType (a diamond crossing, a level crossing).
            if (pre == "USERDEFINED") return new(cls, pre, e.Str("objectType"));
            // A type this schema does not have (IFC4's assembly as an ABUTMENT): user-defined, named by the element's object type when it has one.
            if (!allowed.Contains(pre) && pre != "NOTDEFINED" && e.Str("objectType") is { Length: > 0 } named) return new(cls, "USERDEFINED", named);
            // A predefined type may come with the element's own object type too (a downpipe: a rigid segment, "Downpipe").
            if (allowed.Contains(pre) || pre == "NOTDEFINED") return new(cls, pre, e.Str("objectType"));
            return new(cls, "USERDEFINED", pre);
        }

        /// <summary>A stair flight's numbers as its module counts them (Qto_StairFlightBaseQuantities of that part): risers, treads, riser height, tread length.</summary>
        StairFlightNumbers? FlightOf(Entity e, string key)
        {
            var set = s.Quantities(e.Id).FirstOrDefault(q => q.Name == "Qto_StairFlightBaseQuantities" && q.Part == key);
            if (set == null) return null;
            double? Q(string n) => set.Quantities.FirstOrDefault(x => x.Name == n)?.Value;
            return new StairFlightNumbers((int?)Q("NumberOfRiser"), (int?)Q("NumberOfTreads"), Q("RiserHeight"), Q("TreadLength"));
        }

        /// <summary>
        /// A foreign element as its file wrote it: its class, Description, ObjectType, Tag and what its class adds
        /// (a PredefinedType), with its GlobalId and name as the model has them and its placement and body as
        /// written here; null when nothing of it was kept.
        /// </summary>
        string? Carried(JsonObject src, Entity e, string place, string shape)
        {
            if (IfcCarry.Writer.ClassOf(src) is not var (type, count) || count < 8) return null;
            var args = new string[count];
            for (var i = 0; i < count; i++)
                args[i] = i switch { 0 => Gid(e.Id), 1 => Null, 2 => S(e.Name), 5 => place, 6 => shape, _ => IfcCarry.Writer.ArgOf(src, i) ?? Null };
            return f.Add(type, args);
        }

        /// <summary>The product entity with the attributes its class has: the eight every element shares, then what the class adds.</summary>
        string Product(Kind k, string gid, string? name, string? objectType, string place, string shape, string tag, double? height = null, double? width = null, IfcJoineryType? terms = null, StairFlightNumbers? flight = null, string? operationOfType = null)
        {
            string[] common = [gid, Null, S(name), Null, S(objectType), place, shape, S(tag)];
            var up = k.Class.ToUpperInvariant();
            // A door's operation: the joinery's terms, else what its type says (a gate's, decision of report #57).
            var said = terms?.In(o.Ifc43);
            var operation = E(said?.Operation ?? operationOfType ?? "NOTDEFINED");
            var userDefined = S(said?.UserDefined);
            return k.Class switch
            {
                IfcClass.Door => f.Add(up, [.. common, Opt(height), Opt(width), E(k.Predefined), operation, userDefined]),
                IfcClass.Window => f.Add(up, [.. common, Opt(height), Opt(width), E(k.Predefined), operation, userDefined]),
                "IfcElementAssembly" => f.Add(up, [.. common, Null, E(k.Predefined)]),
                // NominalDiameter and NominalLength, left to the part's properties.
                "IfcMechanicalFastener" => f.Add(up, [.. common, Null, Null, E(k.Predefined)]),
                IfcClass.StairFlight => f.Add(up, [.. common, flight?.Risers is { } nr ? nr.ToString(System.Globalization.CultureInfo.InvariantCulture) : Null, flight?.Treads is { } nt ? nt.ToString(System.Globalization.CultureInfo.InvariantCulture) : Null, Opt(flight?.RiserHeight), Opt(flight?.TreadLength), E(k.Predefined)]),
                _ when k.Predefined.Length == 0 => f.Add(up, common),
                _ => f.Add(up, [.. common, E(k.Predefined)]),
            };
        }

        (double? Height, double? Width) Overall(Entity e, Entity? type)
        {
            // A door or a window, or what is written as one (a gate: its type's clear width and height).
            if (e.Class is not (IfcClass.Door or IfcClass.Window) && e.Str("exportAs") is not (IfcClass.Door or IfcClass.Window)) return (null, null);
            // A door at a size of its own (a curtain wall's, filling its cell).
            if (s.Catalog.Get<IJoineryIfc>()?.Size(m, e) is { } own) return (own.Height, own.Width);
            var spec = type?.Attrs["spec"] as JsonObject;
            var opening = m.RelatingOf(e.Id, IfcClass.RelFillsElement);
            return (Json.Number(spec?["height"]) ?? opening?.NumOrNull("height"), Json.Number(spec?["width"]) ?? opening?.NumOrNull("width"));
        }

        /// <summary>A path element's axis, start to end, as IFC's Axis representation.</summary>
        string? AxisOf(Entity e)
        {
            if (!s.Catalog.IsPath(e.Class) || Pt(e.Attrs["start"]) is not { } a || Pt(e.Attrs["end"]) is not { } b) return null;
            // An arc as the polyline it is drawn with (steps of at most 5°).
            var c = PathCurve.Of(e);
            var pts = c.IsArc ? c.Line(0, c.Length, 0) : [a, b];
            return f.Add("IFCSHAPEREPRESENTATION", axis, S("Axis"), S("Curve2D"), L(new[] { f.Add("IFCPOLYLINE", L(pts.Select(P2).ToArray())) }));
        }

        string? plan2dContext;

        /// <summary>
        /// A reference's own 2D, as the file it came from gave it (<see cref="Plan2d"/>), written back: its footprint lines as a
        /// FootPrint representation, the rest as an Annotation one, in a plan subcontext (colours and patterns are not written).
        /// </summary>
        List<string> Plan2dOf(Entity e)
        {
            var o = new List<string>();
            var items = Plan2d.Read(e);
            if (items.Count == 0) return o;
            plan2dContext ??= f.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("Annotation"), S("Plan"), Derived, Derived, Derived, Derived, model, Null, E("PLAN_VIEW"), Null);
            string Poly(IReadOnlyList<Vec2> pts, bool closed) => f.Add("IFCPOLYLINE", L((closed ? pts.Append(pts[0]) : pts).Select(P2).ToArray()));
            string? Item(Plan2dItem i) => i.Kind switch
            {
                Plan2dItem.Line when i.Points.Count >= 2 => Poly(i.Points, i.Closed),
                Plan2dItem.Fill => f.Add("IFCANNOTATIONFILLAREA", Poly(i.Loops[0], true), i.Loops.Count > 1 ? L(i.Loops.Skip(1).Select(l => Poly(l, true)).ToArray()) : Null),
                Plan2dItem.Label when i.Text != null => f.Add("IFCTEXTLITERAL", S(i.Text),
                    f.Add("IFCAXIS2PLACEMENT2D", P2(i.Points[0]), f.Add("IFCDIRECTION", L(new[] { R(Math.Cos(i.Angle * Math.PI / 180)), R(Math.Sin(i.Angle * Math.PI / 180)) }))), E("RIGHT")),
                _ => null,
            };
            foreach (var (id, type, footprint) in new[] { ("FootPrint", "GeometricCurveSet", true), ("Annotation", "Annotation2D", false) })
            {
                var written = items.Where(i => i.Footprint == footprint).Select(Item).OfType<string>().ToList();
                if (written.Count == 0) continue;
                var set = f.Add("IFCGEOMETRICCURVESET", L(written));
                o.Add(f.Add("IFCSHAPEREPRESENTATION", plan2dContext, S(id), S(type), L(new[] { set })));
            }
            return o;
        }

        // ─── Types and materials ────────────────────────────────────────────

        void TypeOf(Entity type, Kind kind, string entity, bool mirrored = false, IfcJoineryType? terms = null, bool retyped = false)
        {
            var key = mirrored ? type.Id + MirrorKey : type.Id;
            if (!types.TryGetValue(key, out var t))
            {
                // An element written as another class (exportAs: a sanitary fixture kept as furniture, forum topic #245) has its type written as that class's type.
                var typeClass = !retyped && type.Class.EndsWith("Type", StringComparison.Ordinal) && Classes.ContainsKey(type.Class[..^4]) ? type.Class : kind.Class + "Type";
                var gid = mirrored ? Gid(type.Id, MirrorKey) : Gid(type.Id);
                // The mirrored type carries the type's intent and says whose mirror it is, so Lapin reads it back as the type itself.
                // A type of Lapin's own class (an exhaust fan's, a gate's) says it, so it comes back as itself.
                var own = new List<(string, string)>();
                if (mirrored) own.Add(("LapinMirrorOf", GlobalIdOf(type.Id)));
                if (typeClass != type.Class) own.Add(("LapinClass", type.Class));
                foreach (var src in s.Catalog.All<IIfcTypeShapes>()) own.AddRange(src.TypeIntent(m, type));
                var intent = IntentOf(type, Gid(key, IntentKey), null, own.Count > 0 ? own : null);
                // The type's parameters, which every element of the type has.
                var sets = new List<string> { intent };
                if (ParamsOf(type, Gid(key, ParamsKey)) is { } ps) sets.Add(ps);
                if (LightSet(type, Gid(key, LightSetName)) is { } lightSet) sets.Add(lightSet);
                // A door's or window's own terms IFC cannot say (forum topic 34) join the user's Lapin_Joinery set, or make it.
                var joinery = (terms?.Properties ?? []).Select(p => (p.Name, Value: Typed("IFCLABEL", S(p.Value)))).ToList();
                foreach (var (setName, props) in NamedParams(type))
                {
                    var all = props;
                    if (joinery.Count > 0 && setName == IfcJoineryType.PropertySet) { all = [.. props, .. joinery.Where(j => !props.Any(p => p.Name == j.Name))]; joinery.Clear(); }
                    sets.Add(f.Add("IFCPROPERTYSET", Gid(key, "params:" + setName), Null, S(setName), Null, L(all.Select(p => Single(p.Name, p.Value)).ToArray())));
                }
                if (joinery.Count > 0) sets.Add(f.Add("IFCPROPERTYSET", Gid(key, "params:" + IfcJoineryType.PropertySet), Null, S(IfcJoineryType.PropertySet), Null, L(joinery.Select(p => Single(p.Name, p.Value)).ToArray())));
                if (terms != null) sets.AddRange(JoinerySets(key, terms));
                // Lapin's own sets a module gives the type (a part's type parameters, Lapin_PartParameters).
                foreach (var src in s.Catalog.All<IIfcTypeShapes>())
                    if (src.TypeSet(m, type) is { Properties.Count: > 0 } ts)
                        sets.Add(f.Add("IFCPROPERTYSET", Gid(key, "type-set:" + ts.Name), Null, S(ts.Name), Null, L(ts.Properties.Select(p => Single(p.Name, StandardValue(p), p.Description)).ToArray())));
                // An exhaust fan's type (report #55): Pset_FanTypeCommon with the names IFC gives them, its airflow in m³/s.
                if (kind.Class == "IfcFan" && type.Attrs["spec"] is JsonObject fan && Json.Number(fan["airflow"]) is { } flow)
                {
                    var fanProps = new List<string> { Single("NominalAirFlowRate", Typed("IFCVOLUMETRICFLOWRATEMEASURE", R(flow / 3600))) };
                    if (Json.Number(fan["power"]) is { } power) fanProps.Add(Single("NominalPowerRate", Typed("IFCPOWERMEASURE", R(power))));
                    sets.Add(f.Add("IFCPROPERTYSET", Gid(key, "fan-common"), Null, S("Pset_FanTypeCommon"), Null, L(fanProps)));
                }
                var pre = E(kind.Predefined.Length == 0 ? "NOTDEFINED" : kind.Predefined);
                var name = mirrored ? $"{type.Name} (mirrored)" : type.Name;
                // Its body as its representation map (a part's), the map its elements built with its values are mapped items of.
                var typeMap = !mirrored && typeMaps.GetValueOrDefault(type.Id) is { } tm ? L(new[] { tm }) : Null;
                string[] common = [gid, Null, S(name), Null, Null, L(sets), typeMap, S(type.Id), S(kind.Predefined == "USERDEFINED" ? kind.ObjectType : null)];
                var up = typeClass.ToUpperInvariant();
                // A door's operation in the schema written (a garage door's differs, decision 530).
                var said = terms?.In(o.Ifc43);
                var operation = E(said?.Operation ?? type.Str("ifcOperation") ?? "NOTDEFINED");
                var userDefined = S(said != null ? said.Value.UserDefined : type.Str("ifcOperation") == "USERDEFINED" ? type.Str("ifcUserOperation") : null);
                var r = kind.Class switch
                {
                    IfcClass.Door => f.Add(up, [.. common, pre, operation, False, userDefined]),
                    IfcClass.Window => f.Add(up, [.. common, pre, operation, False, userDefined]),
                    // IfcFurnitureType: its AssemblyPlace before its PredefinedType.
                    "IfcFurniture" => f.Add(up, [.. common, E("NOTDEFINED"), pre]),
                    // A class without predefined types (IfcFurnishingElementType ends at its ElementType).
                    _ when Classes.TryGetValue(kind.Class, out var allowed) && allowed == null => f.Add(up, common),
                    _ => f.Add(up, [.. common, pre]),
                };
                types[key] = t = (r, []);
                // Its material: the layer set of a layered type; the one material a column's or beam's type names.
                if (m.RelatingOf(type.Id, IfcClass.RelAssociatesMaterial) is { Class: IfcClass.MaterialLayerSet } set)
                    f.Add("IFCRELASSOCIATESMATERIAL", Gid(type.Id, "material"), Null, Null, Null, L(new[] { r }), LayerSet(set));
                else if (type.Str("material") is { } mat && m.Get(mat) is { Class: IfcClass.Material })
                    UseMaterial(mat, r);
            }
            t.Occurrences.Add(entity);
        }

        string LayerSet(Entity set)
        {
            if (layerSets.TryGetValue(set.Id, out var r)) return r;
            // Its Category: Core for the core, else the layer's own (Finish, Insulation…; the forum's topic #30) when its type gives one.
            var layers = Layers.Read(set).Select(l =>
            {
                var r = f.Add("IFCMATERIALLAYER", Material(l.Material), R(l.Thickness), Null, S(l.Name ?? m.Get(l.Material)?.Name), S(l.Note), S(l.Core ? "Core" : l.Category), l.Priority is { } p ? I(Math.Clamp(p, 0, 100)) : Null);
                // A wall's layer going on into the floors' layers ranked under it (topic #30): Lapin's own properties of the layer, the bodies built as they are.
                if (l.ExtendDown == true || l.ExtendUp == true)
                    f.Add("IFCMATERIALPROPERTIES", S(LayerJoinsSetName), Null,
                        L(new[] { Single("ExtendToFloorBelow", Typed("IFCBOOLEAN", l.ExtendDown == true ? True : False)), Single("ExtendToFloorAbove", Typed("IFCBOOLEAN", l.ExtendUp == true ? True : False)) }), r);
                return r;
            }).ToList();
            return layerSets[set.Id] = f.Add("IFCMATERIALLAYERSET", L(layers), S(set.Name), Null);
        }

        /// <summary>A material, with its colour as its representation (what a reader without the styled bodies shows).</summary>
        string Material(string id)
        {
            if (materials.TryGetValue(id, out var r)) return r;
            var mat = m.Get(id);
            // Its description (docs/plan-buildup-tags.md) as IfcMaterial.Description; its category, else the one its file gave (ifcCategory).
            r = f.Add("IFCMATERIAL", S(mat?.Name ?? id), S(mat?.Str("description") is { Length: > 0 } d ? d : null),
                S(mat?.Str("category") is { Length: > 0 } c ? c : mat?.Str("ifcCategory")));
            materials[id] = r;
            // Its Lapin id and attributes, so an import gives the material the same id and what refers to it (a column type, a zone) still finds it.
            if (mat != null) f.Add("IFCMATERIALPROPERTIES", S(IntentSetName), Null, L(IntentProperties(mat, null, [("LapinId", mat.Id)])), r);
            // A material that glows as a light (docs/plan-render.md, S2): its light data in words.
            if (mat?.Attrs[Lapin.Platform.Lighting.Lights.Key] is JsonObject glow)
            {
                var spec = Lapin.Platform.Lighting.LightSpec.Read(glow);
                f.Add("IFCMATERIALPROPERTIES", S(LightSetName), Null, L(new[]
                {
                    Single("Lumens", Typed("IFCLUMINOUSFLUXMEASURE", R(spec.Lumens))), Single("ColourTemperature", Typed("IFCTHERMODYNAMICTEMPERATUREMEASURE", R(spec.Kelvin))),
                    Single("On", Typed("IFCBOOLEAN", spec.On ? True : False)),
                }), r);
            }
            // Its parameters: the standard ones in IFC's own sets (Pset_MaterialThermal.ThermalConductivity, typed as IFC types them), the user's in Lapin_Material.
            var sets = mat != null ? MaterialSets(mat) : [];
            // With the structural analysis view, the values its analysis takes that the material does not state (E and ν by its
            // grade's standard, its density), each saying where it comes from (docs/plan-fem.md §8.7).
            var sources = o.Structural ? AnalysisValues(mat ?? Materials.Find(m, id) ?? new Entity($"lapin-material-{id}", IfcClass.Material, id, []), sets) : null;
            foreach (var (set, props) in sets)
                f.Add("IFCMATERIALPROPERTIES", S(set), Null, L(props.Select(p => Single(p.Name, p.Value, sources?.GetValueOrDefault((set, p.Name)))).ToArray()), r);
            var style = Style(MaterialKey + id);
            // Its drawing graphics set by hand (decision 678): an IfcFillAreaStyle per kind of view beside its surface style.
            string[] looks = [style, .. mat != null ? FillAreaStyles(mat) : []];
            f.Add("IFCMATERIALDEFINITIONREPRESENTATION", Null, Null,
                L(new[] { f.Add("IFCSTYLEDREPRESENTATION", model, Null, Null, L(new[] { f.Add("IFCSTYLEDITEM", Null, L(looks), Null) })) }), r);
            return r;
        }

        /// <summary>
        /// A material's drawing graphics as IFC says them (decision 678), only for a material that sets its own (one drawn
        /// as its name suggests writes none, so its file is as before): an IfcFillAreaStyle per kind of view it sets, named
        /// after the kind (<see cref="FillAreaStyleNames"/>), draughting, holding its fill as an IfcColourRgb, then its
        /// background hatch and its hatch, each an IfcFillAreaStyleHatching whose IfcCurveStyle is named after the pattern
        /// (the background's „Background PATTERN”) and carries its colour and width (paper metres), its line spacing and
        /// angle the pattern's first line family's at its scale. A reader that knows Lapin's patterns reads the names back.
        /// </summary>
        List<string> FillAreaStyles(Entity mat)
        {
            var o = new List<string>();
            var patterns = Lapin.Platform.DraftingResources.Patterns(m);
            string Colour(string hex) { var (r, g, b) = HexColor.Unit(hex); return f.Add("IFCCOLOURRGB", Null, R(r), R(g), R(b)); }
            string? Hatching(string? name, string curveName, double scale, double angleDeg, string? color, double weight)
            {
                if (name == null || Lapin.Drawing.Hatch.HatchLibrary.Find(name, patterns) is not { Solid: false } p) return null;
                // The batt is drawn by its own rule, no line family: written as the nearest one, INSULATION's zig-zag (as Bonsai's
                // stylesheet draws it), its curve style named BATT, which Lapin reads back.
                if (Lapin.Drawing.Hatch.BattHatch.Is(p) && Lapin.Drawing.Hatch.HatchLibrary.Find("INSULATION") is { } zigzag) p = zigzag;
                // A tile pattern (stage 7) as the nearest line hatching: along its longest straight piece (0° with none), its
                // cell's smaller side apart; the curve style named after it, which Lapin reads back.
                double famAngle, famSpacing;
                if (p.Families.Count > 0) (famAngle, famSpacing) = (p.Families[0].AngleDeg, p.Families[0].DeltaY);
                else if (p.Tile is { } tile)
                {
                    var longest = tile.Lines.Where(l => !l.Dot).OrderByDescending(l => (l.B - l.A).Length).Select(l => (Vec2?)(l.B - l.A)).FirstOrDefault();
                    (famAngle, famSpacing) = (longest is { } d ? Math.Atan2(d.Y, d.X) * 180 / Math.PI : 0, Math.Min(tile.Width, tile.Height));
                }
                else return null;
                var curve = f.Add("IFCCURVESTYLE", S(curveName), Null, Typed("IFCPOSITIVELENGTHMEASURE", R(weight / 1000)), Colour(color ?? "#000000"), False);
                var spacing = Math.Max(1e-6, Math.Abs(famSpacing) * scale / 1000);
                return f.Add("IFCFILLAREASTYLEHATCHING", curve, Typed("IFCPOSITIVELENGTHMEASURE", R(spacing)), Null, Null, R((famAngle + angleDeg) * Math.PI / 180));
            }
            foreach (var kind in Lapin.Drawing.MaterialGraphics.Kinds)
            {
                if (mat.Attrs[kind] is not JsonObject) continue;
                var g = Lapin.Drawing.MaterialGraphics.Resolve(mat, kind, standard: Lapin.Platform.MaterialStandards.Of(m));
                if (g.Membrane) continue;
                var fills = new List<string>();
                if (g.Fill is { } fill) fills.Add(Colour(fill));
                if (g.Background is { } bg && Hatching(bg.Hatch, "Background " + bg.Hatch, bg.HatchScale ?? g.HatchScale, bg.HatchAngle ?? 0, bg.HatchColor, bg.HatchWeight ?? Lapin.Drawing.Views.Pens.Default.Hatch) is { } bh) fills.Add(bh);
                if (Hatching(g.Hatch, g.Hatch ?? "", g.HatchScale, g.HatchAngle, g.HatchColor, g.HatchWeight ?? Lapin.Drawing.Views.Pens.Default.Hatch) is { } fh) fills.Add(fh);
                // A real-size hatch (decision 679) is a model fill area style: its spacings are lengths of the object.
                var model = g.Hatch != null && Lapin.Drawing.Hatch.HatchLibrary.Find(g.Hatch, patterns) is { } hp && Lapin.Drawing.Hatch.HatchScale.UnitsOf(hp, g.HatchUnits) == Lapin.Drawing.Hatch.HatchUnits.Model;
                if (fills.Count > 0) o.Add(f.Add("IFCFILLAREASTYLE", S(FillAreaStyleNames[kind]), L(fills.ToArray()), model ? True : False));
            }
            return o;
        }

        /// <summary>
        /// The IfcMaterialConstituentSet of a door's or window's parts (report #76): a constituent per part of the joinery that
        /// is made of a material, named after what it is (<see cref="JoineryConstituents"/>: Frame, Sash, Glazing, Panel, Board,
        /// Hardware), so a viewer or renderer reading IFC finds the material per part; one set per combination, shared. Null when
        /// fewer than two parts name a material (one material is associated as before).
        /// </summary>
        string? ConstituentsOf(Entity e, List<GeometryPart> parts)
        {
            var list = new List<(string Name, string Material)>();
            foreach (var p in parts)
                if (p.Key is { } k && JoineryConstituents.Name(k) is { } name && p.Material is { } mat && m.Get(mat) is { Class: IfcClass.Material } && !list.Any(x => x.Name == name))
                    list.Add((name, mat));
            if (list.Count < 2) return null;
            var signature = string.Join("|", list.Select(x => x.Name + "=" + x.Material));
            if (constituentSets.TryGetValue(signature, out var known)) return known.Ref;
            var constituents = list.Select(x => f.Add("IFCMATERIALCONSTITUENT", S(x.Name), Null, Material(x.Material), Null, Null)).ToArray();
            var set = f.Add("IFCMATERIALCONSTITUENTSET", S(e.Class == IfcClass.Door ? "Door" : "Window"), Null, L(constituents));
            constituentSets[signature] = (set, Gid(e.Id, "constituents"), []);
            return set;
        }

        /// <summary>
        /// A column's core and finish layers (decision 719) as an IfcMaterialConstituentSet: IFC4 gives a column a profile set
        /// (its core) and layers only to planar elements, so the layers laid round a column are constituents, one per material
        /// part (the core first, named "Core", then each layer by its name, its Category Core, Finish, Insulation…), the body's
        /// solids written as they are built; one set per combination, shared. Null for a column without finish layers.
        /// </summary>
        string? ColumnConstituentsOf(Entity e, List<GeometryPart> parts)
        {
            if (e.Class != IfcClass.Column || LayerSets.DrawnBandsOf(m, e.Id) is not { Count: > 1 } bands || !parts.Any(p => p.Key is { } k && k.StartsWith("layer:", StringComparison.Ordinal))) return null;
            var list = bands.Where(b => m.Get(b.Material) is { Class: IfcClass.Material })
                .Select(b => (Name: b.Core ? "Core" : b.Name, b.Material, Category: b.Core ? "Core" : LayerSets.SubOf(m, b) switch { "finishes" => "Finish", "insulation" => "Insulation", "membranes" => "Membrane", _ => "Other" })).ToList();
            if (list.Count < 2) return null;
            var signature = "column|" + string.Join("|", list.Select(x => x.Name + "=" + x.Material + "=" + x.Category));
            if (constituentSets.TryGetValue(signature, out var known)) return known.Ref;
            var constituents = list.Select(x => f.Add("IFCMATERIALCONSTITUENT", S(x.Name), Null, Material(x.Material), Null, S(x.Category))).ToArray();
            var set = f.Add("IFCMATERIALCONSTITUENTSET", S(Types.Of(m, e.Id)?.Name ?? "Column"), Null, L(constituents));
            constituentSets[signature] = (set, Gid(e.Id, "constituents"), []);
            return set;
        }

        void Associate(string set, string entity) => constituentSets.Values.First(x => x.Ref == set).Users.Add(entity);

        void UseMaterial(string materialId, string entity) => (materialUses.TryGetValue(materialId, out var l) ? l : materialUses[materialId] = []).Add(entity);

        /// <summary>A layered element's usage of its type's layer set; else the one material of its body (a grouped element's parts carry theirs).</summary>
        void MaterialsOf(Entity e, string entity, List<GeometryPart> parts, bool grouped, (string Key, string Ref)? type = null)
        {
            // A door or window of several materials (report #76): a constituent set, one constituent per part, on it and its type.
            if (!grouped && e.Class is IfcClass.Door or IfcClass.Window && ConstituentsOf(e, parts) is { } constituents)
            {
                Associate(constituents, entity);
                if (type is var (key, typeRef) && constituentTypes.Add(key)) Associate(constituents, typeRef);
                return;
            }
            // A column with finish layers round its core (decision 719): its core and layers as constituents, on it and its type.
            if (!grouped && ColumnConstituentsOf(e, parts) is { } columnSet)
            {
                Associate(columnSet, entity);
                if (type is var (ckey, ctypeRef) && constituentTypes.Add(ckey)) Associate(columnSet, ctypeRef);
                return;
            }
            if (LayerSets.UsageOf(m, e.Id) is { } u && Layers.ReadUsage(u) is { } usage && m.Get(usage.ForLayerSet) is { } set)
            {
                // A floor's, a roof's, or another element's laid through its thickness (a hardscape's, docs/plan-site-grading.md T4).
                var direction = e.Class is IfcClass.Slab or IfcClass.Roof || u.Str("layerSetDirection") == "AXIS3" ? "AXIS3" : "AXIS2";
                var usageRef = f.Add("IFCMATERIALLAYERSETUSAGE", LayerSet(set), E(direction), E(usage.DirectionSense), R(usage.Offset), Null);
                var rel = m.RelationsOf(e.Id, IfcClass.RelAssociatesMaterial).FirstOrDefault(r => r.Relating == u.Id);
                f.Add("IFCRELASSOCIATESMATERIAL", rel != null ? Gid(rel.Id) : Gid(e.Id, "material"), Null, Null, Null, L(new[] { entity }), usageRef);
                return;
            }
            if (grouped) return;
            var mats = parts.Select(p => p.Material).OfType<string>().Distinct().ToList();
            if (mats.Count == 1) UseMaterial(mats[0], entity);
        }

        // ─── Parts, openings ────────────────────────────────────────────────

        /// <summary>The element's parts, each an element of its own aggregated under it; <paramref name="items"/>: their bodies written already (the parent's body shares them), by index.</summary>
        void Parts(Entity e, Kind kind, List<GeometryPart> parts, string place, Frame fr, string parent, IReadOnlyList<string?>? items = null)
        {
            var refs = new List<string>();
            // A roof's trusses: each an element assembly of its members (keys "truss:<n>:<role>:<i>"), under the roof.
            var trusses = new Dictionary<string, List<string>>();
            var panels = new Dictionary<string, List<string>>();
            var groups = new Dictionary<string, (PartGroup Group, List<string> Members)>();
            var zones = (m.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.Attrs["zones"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            for (var i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                var key = p.Key ?? $"part:{i}";
                var pk = PartKind(kind.Class, key, p, zones);
                // A part its module knows exactly (an abutment's warped wing) as an IfcAdvancedBrep, when the export asks.
                var exactPart = o.AdvancedBrep && items?[i] == null && p.Unit == null ? ExactOfPart(e, key, p.Solid) : null;
                var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { p.Unit != null && p.Instances is { Count: > 0 } copies ? Mapped(p.Unit, copies, StyleKey(p), fr)
                    : exactPart != null ? AdvancedRep(exactPart, StyleKey(p), fr) : BodyOf([items?[i] ?? Brep(p.Solid, StyleKey(p), fr)]) }));
                var r = Product(pk, Gid(e.Id, key), p.Name, pk.ObjectType, Place(place, 0), shape, $"{e.Id}/{key}", flight: pk.Class == IfcClass.StairFlight ? FlightOf(e, key) : null);
                if (p.Material != null) UseMaterial(p.Material, r);
                // A part made of a type (a lintel of a beam type, report #135): typed by it, IfcBeamType LINTEL.
                if (p.TypeId != null && m.Get(p.TypeId) is { } partType) TypeOf(partType, pk, r);
                StandardSets(e, p, r, $"{e.Id}/{key}");
                if ((p.Properties?.Count ?? 0) + (p.Measures?.Count ?? 0) > 0)
                    Define(r, f.Add("IFCPROPERTYSET", Gid(e.Id, key + "/pset"), Null, S(p.PropertySet ?? "Lapin_Part"), Null, L(PartProperties(p.Properties, p.Measures))), Gid(e.Id, key + "/pset-rel"));
                // The part's own quantities (a roof panel's Qto_CoveringBaseQuantities), on its element only.
                foreach (var set in p.Quantities ?? [])
                    if (QuantityItems(set) is { Count: > 0 } q)
                        Define(r, f.Add("IFCELEMENTQUANTITY", Gid(e.Id, key + "/qto/" + set.Name), Null, S(set.Name), Null, Null, L(q)), Gid(e.Id, key + "/qto-rel/" + set.Name));
                // The group it is assigned to (a roof panel's plane): an IfcGroup per key, made once, its members in the parts' order.
                if (p.Group is { } pg)
                {
                    if (!groups.TryGetValue(pg.Key, out var g)) groups[pg.Key] = g = (pg, []);
                    g.Members.Add(r);
                }
                partRefs[(e.Id, key)] = r;
                if (kind.Class == IfcClass.Roof && key.Split(':') is ["truss", var n, ..]) { if (!trusses.TryGetValue(n, out var members)) trusses[n] = members = []; members.Add(r); }
                // A prefabricated timber-frame wall's members and boards (decision 245), by the panel they belong to.
                else if (kind.Class == IfcClass.Wall && p.Properties?.FirstOrDefault(x => x.Name == "Panel").Value is { Length: > 0 } mark) { if (!panels.TryGetValue(mark, out var members)) panels[mark] = members = []; members.Add(r); }
                else refs.Add(r);
                PartCount++;
            }
            foreach (var (n, members) in trusses)
            {
                var truss = f.Add("IFCELEMENTASSEMBLY", Gid(e.Id, "truss:" + n), Null, S($"Truss {n}"), Null, Null, Place(place, 0), Null, S($"{e.Id}/truss:{n}"), E("FACTORY"), E("TRUSS"));
                f.Add("IFCRELAGGREGATES", Gid(e.Id, "truss-parts:" + n), Null, Null, Null, truss, L(members));
                refs.Add(truss);
            }
            foreach (var (mark, members) in panels)
            {
                var panel = f.Add("IFCELEMENTASSEMBLY", Gid(e.Id, "panel:" + mark), Null, S(mark), Null, S("Timber frame panel"), Place(place, 0), Null, S(mark), E("FACTORY"), E("USERDEFINED"));
                f.Add("IFCRELAGGREGATES", Gid(e.Id, "panel-parts:" + mark), Null, Null, Null, panel, L(members));
                refs.Add(panel);
            }
            f.Add("IFCRELAGGREGATES", Gid(e.Id, "parts"), Null, Null, Null, parent, L(refs));
            foreach (var (gkey, (pg, members)) in groups)
            {
                var group = f.Add("IFCGROUP", Gid(e.Id, "group:" + gkey), Null, S(pg.Name), S(pg.Description), S(pg.ObjectType));
                f.Add("IFCRELASSIGNSTOGROUP", Gid(e.Id, "group-members:" + gkey), Null, Null, Null, L(members), Null, group);
                if ((pg.Properties?.Count ?? 0) + (pg.Measures?.Count ?? 0) > 0)
                    Define(group, f.Add("IFCPROPERTYSET", Gid(e.Id, "group:" + gkey + "/pset"), Null, S(pg.PropertySet ?? "Lapin_Group"), Null, L(PartProperties(pg.Properties, pg.Measures))), Gid(e.Id, "group:" + gkey + "/pset-rel"));
            }
        }

        /// <summary>A part's (or a part group's) own properties: its texts as labels, its numbers as the measures they are (an angle in the file's radians).</summary>
        string[] PartProperties(IReadOnlyList<(string Name, string Value)>? texts, IReadOnlyList<PartMeasure>? measures) =>
        [
            .. (texts ?? []).Select(x => Single(x.Name, Typed("IFCLABEL", S(x.Value)))),
            .. (measures ?? []).Where(x => double.IsFinite(x.Value)).Select(x => Single(x.Name, x.Kind switch
            {
                "LENGTH" => Typed("IFCLENGTHMEASURE", R(x.Value)),
                "AREA" => Typed("IFCAREAMEASURE", R(x.Value)),
                "ANGLE" => Typed("IFCPLANEANGLEMEASURE", R(x.Value * Math.PI / 180)),
                "COUNT" => Typed("IFCINTEGER", I((int)Math.Round(x.Value))),
                _ => Typed("IFCREAL", R(x.Value)),
            })),
        ];

        /// <summary>A quantity set's quantities as IFC's (a name met again numbered, as <see cref="Quantities"/> writes them).</summary>
        List<string> QuantityItems(QuantitySet set)
        {
            var seen = new Dictionary<string, int>();
            var q = new List<string>();
            foreach (var x in set.Quantities.Where(x => double.IsFinite(x.Value)))
            {
                var n = seen[x.Name] = seen.GetValueOrDefault(x.Name) + 1;
                var name = S(n == 1 ? x.Name : $"{x.Name} ({n})");
                var cls = x.Kind switch
                {
                    "LENGTH" => "IFCQUANTITYLENGTH", "AREA" => "IFCQUANTITYAREA", "VOLUME" => "IFCQUANTITYVOLUME",
                    "WEIGHT" => "IFCQUANTITYWEIGHT", "COUNT" => "IFCQUANTITYCOUNT", _ => null,
                };
                if (cls == null) continue;
                q.Add(f.Add(cls, name, S(x.Label), Null, cls == "IFCQUANTITYCOUNT" ? I((int)Math.Round(x.Value)) : R(x.Value), Null));
            }
            return q;
        }

        /// <summary>What a part is in IFC: a lintel or ring beam a beam, a roof plane a roof slab, the timber members, the sole of a strip footing a footing; else a building element part named as Lapin names it.</summary>
        Kind PartKind(string host, string key, GeometryPart p, List<JsonObject> zones)
        {
            var colon = key.IndexOf(':');
            var (prefix, rest) = colon < 0 ? (key, "") : (key[..colon], key[(colon + 1)..]);
            // The railway (a track, a turnout: an element assembly): rails, sleepers and bearers, the ballast bed and the sub-ballast — IFC 4.3's own classes, IFC4's nearest.
            if (host == "IfcElementAssembly")
                switch (prefix)
                {
                    // An abutment's or a tunnel portal's wings and face wall (a retaining wall written as an assembly, decision 789 S3).
                    case "leg": return new(IfcClass.Wall, "RETAININGWALL", null);
                    // A tunnel lining's casting blocks, its TBM rings and its outer shell (S4): IFC has no tunnel lining element, so each a
                    // proxy named for what it is (a wall or a slab would say a lining's vault and invert are what they are not).
                    case "block": return new(IfcClass.BuildingElementProxy, "USERDEFINED", "Lining block");
                    case "ring": return new(IfcClass.BuildingElementProxy, "USERDEFINED", "Lining ring");
                    case "primary": return new(IfcClass.BuildingElementProxy, "USERDEFINED", "Outer shell");
                    case "rail": return o.Ifc43 ? new("IfcRail", "RAIL", null) : new("IfcMember", "USERDEFINED", "Rail");
                    case "sleeper": return o.Ifc43 ? new("IfcTrackElement", "SLEEPER", null) : new("IfcBuildingElementPart", "USERDEFINED", "Sleeper");
                    case "bearer": return o.Ifc43 ? new("IfcTrackElement", "SLEEPER", null) : new("IfcBuildingElementPart", "USERDEFINED", "Bearer");
                    case "ballast": return o.Ifc43 ? new("IfcCourse", "BALLASTBED", null) : new("IfcBuildingElementPart", "USERDEFINED", "Ballast bed");
                    case "subballast": return o.Ifc43 ? new("IfcCourse", "USERDEFINED", "Sub-ballast") : new("IfcBuildingElementPart", "USERDEFINED", "Sub-ballast");
                    case "frog": return o.Ifc43 ? new("IfcTrackElement", "FROG", null) : new("IfcBuildingElementPart", "USERDEFINED", "Frog");
                    // A level crossing: its deck a rigid pavement, its barriers boom barriers, its signals signals — IFC 4.3; IFC4's nearest.
                    case "deck": return o.Ifc43 ? new(IfcClass.Pavement, "RIGID", null) : new(IfcClass.Slab, "USERDEFINED", "Level crossing deck");
                    case "barrier": return o.Ifc43 ? new(IfcClass.Door, "BOOM_BARRIER", null) : new("IfcBuildingElementPart", "USERDEFINED", "Barrier");
                    case "signal": return o.Ifc43 ? new("IfcSignal", "VISUAL", null) : new("IfcBuildingElementPart", "USERDEFINED", "Signal");
                }
            return (host, prefix) switch
            {
                ("IfcCableCarrierSegment", "fitting") => new("IfcCableCarrierFitting", rest.Split(':')[0].ToUpperInvariant(), null),
                (IfcClass.Wall, "lintel") => new(IfcClass.Beam, "LINTEL", null),
                (IfcClass.Wall, "downstand") => new(IfcClass.Beam, "USERDEFINED", "Ring beam"),
                (IfcClass.Wall, "zone") when zones.FirstOrDefault(z => (string?)z["key"] == rest) is { } zone && (string?)zone["role"] == "ringBeam" => new(IfcClass.Beam, "USERDEFINED", "Ring beam"),
                // A roof without framing: its deck (decision 329) and its covering, each an IfcSlab ROOF of its material.
                (IfcClass.Roof, "covering") or (IfcClass.Roof, "deck") or (IfcClass.Roof, "dormer-roofs") => new(IfcClass.Slab, "ROOF", null),
                (IfcClass.Roof, "roof-windows") => new(IfcClass.Window, "SKYLIGHT", null),
                // A roof's panels (the roofpanels module, docs/plan-roof-panels.md §2.6): each a covering of the roof, in place of its covering slab.
                (IfcClass.Roof, "panel") => new("IfcCovering", "ROOFING", null),
                (IfcClass.Roof, "glass") => new("IfcPlate", "SHEET", null),
                (IfcClass.Roof, "dormer-walls") => new(IfcClass.Wall, "NOTDEFINED", null),
                (IfcClass.Roof, "timber" or "truss") => TimberKind(rest),
                // A timber-frame wall's members (the wood module, decision 239) and the insulation between them.
                (IfcClass.Wall, "wood") => TimberKind(rest),
                (IfcClass.Wall, "wood-insulation") => new("IfcCovering", "INSULATION", null),
                // A CLT wall's or floor's panels (decision 241).
                (IfcClass.Wall or IfcClass.Slab or IfcClass.Roof, "clt") => new("IfcBuildingElementPart", "USERDEFINED", "CLT panel"),
                (IfcClass.Wall or IfcClass.Slab, "clt-spline") => new("IfcPlate", "USERDEFINED", "Spline"),
                // A joisted floor's members (decision 244): joists as IfcBeam JOIST.
                (IfcClass.Slab, "wood") => rest.Split(':')[0] switch
                {
                    "joist" or "tail" or "trimmer" => new(IfcClass.Beam, "JOIST", null),
                    "rim" => new(IfcClass.Beam, "USERDEFINED", "Rim joist"),
                    "header" => new(IfcClass.Beam, "USERDEFINED", "Header"),
                    _ => new("IfcMember", "MEMBER", null),
                },
                (IfcClass.Slab, "wood-insulation") => new("IfcCovering", "INSULATION", null),
                // A beam-and-block floor's parts (report #134): the precast joists, the infill blocks, the cast concrete.
                (IfcClass.Slab, "bb-joist") => new(IfcClass.Beam, "JOIST", null),
                (IfcClass.Slab, "bb-blocks" or "bb-cut-blocks") => new("IfcBuildingElementPart", "USERDEFINED", "Infill block"),
                (IfcClass.Slab, "bb-concrete") => new("IfcBuildingElementPart", "USERDEFINED", "Cast concrete topping"),
                (IfcClass.Roof, "drainage" or "gutter") => new("IfcPipeSegment", "GUTTER", null),
                (IfcClass.Roof, "downpipe") => new("IfcPipeSegment", "RIGIDSEGMENT", "Downpipe"),
                (IfcClass.Roof, "snow") => new("IfcDiscreteAccessory", "USERDEFINED", "Snow guard"),
                ("IfcElementAssembly", "standards" or "ledgers" or "transoms" or "guardrails") => new("IfcMember", "MEMBER", null),
                ("IfcElementAssembly", "diagonals") => new("IfcMember", "BRACE", null),
                ("IfcElementAssembly", "props") => new("IfcMember", "STRUT", null),
                ("IfcElementAssembly", "primary" or "secondary" or "bearers" or "chamfers") => new("IfcMember", "USERDEFINED", p.Name),
                ("IfcElementAssembly", "decks" or "toe" or "plywood" or "edges" or "bottom" or "sides" or "net") => new("IfcPlate", "USERDEFINED", p.Name),
                ("IfcElementAssembly", "jacks" or "anchors") => new("IfcDiscreteAccessory", "USERDEFINED", p.Name),
                // A steel connection's parts (decision 273): plates, bolts, anchors and their plates.
                ("IfcElementAssembly", "steel-plate") => new("IfcPlate", "USERDEFINED", p.Name),
                ("IfcElementAssembly", "steel-bolt") => new("IfcMechanicalFastener", "BOLT", null),
                ("IfcElementAssembly", "steel-anchor") => new("IfcMechanicalFastener", "ANCHORBOLT", null),
                ("IfcElementAssembly", "steel-anchorplate") => new("IfcDiscreteAccessory", "ANCHORPLATE", null),
                // A balcony's parts (a tester's report, #96): its slab a floor slab, its finish and soffit coverings, its railing a guardrail
                // (a parapet a wall), its posts columns, its brackets brackets, its tie rods members; a loggia's side walls walls.
                ("IfcElementAssembly", "balcony-slab") => new(IfcClass.Slab, "FLOOR", null),
                ("IfcElementAssembly", "balcony-ceiling") => new(IfcClass.Slab, "USERDEFINED", "Loggia ceiling"),
                ("IfcElementAssembly", "balcony-finish") => new("IfcCovering", "FLOORING", null),
                ("IfcElementAssembly", "balcony-soffit") => new("IfcCovering", "CEILING", null),
                ("IfcElementAssembly", "balcony-fascia") => new("IfcCovering", "CLADDING", null),
                ("IfcElementAssembly", "balcony-railing" or "balcony-glass") => new("IfcRailing", "GUARDRAIL", null),
                ("IfcElementAssembly", "balcony-parapet") => new(IfcClass.Wall, "PARAPET", null),
                ("IfcElementAssembly", "balcony-cheeks") => new(IfcClass.Wall, "NOTDEFINED", null),
                ("IfcElementAssembly", "balcony-posts") => new("IfcColumn", "COLUMN", null),
                ("IfcElementAssembly", "balcony-brackets") => new("IfcDiscreteAccessory", "BRACKET", null),
                ("IfcElementAssembly", "balcony-hangers") => new("IfcMember", "USERDEFINED", "Tie rod"),
                ("IfcElementAssembly", "balcony-break") => new("IfcBuildingElementPart", "USERDEFINED", "Thermal break"),
                ("IfcElementAssembly", "balcony-drip") => new("IfcPlate", "USERDEFINED", "Drip"),
                // The space under a stair (a tester's report, #71): its furniture, doors, boards, fittings and light.
                ("IfcElementAssembly", "nook-door") => new(IfcClass.Door, "DOOR", p.Name),
                ("IfcElementAssembly", "nook-panel" or "nook-trim") => new("IfcPlate", "USERDEFINED", p.Name),
                ("IfcElementAssembly", "nook-shelves" or "nook-bookcase" or "nook-cubbies") => new("IfcFurniture", "SHELF", p.Name),
                ("IfcElementAssembly", "nook-desk") => new("IfcFurniture", "DESK", null),
                ("IfcElementAssembly", "nook-bed") => new("IfcFurniture", "BED", p.Name),
                ("IfcElementAssembly", "nook-bench" or "nook-drawers" or "nook-cushion") => new("IfcFurniture", "USERDEFINED", p.Name),
                ("IfcElementAssembly", "nook-handles" or "nook-hooks") => new("IfcDiscreteAccessory", "USERDEFINED", p.Name),
                ("IfcElementAssembly", "nook-light") => new("IfcLightFixture", "POINTSOURCE", null),
                ("IfcCurtainWall", "mullion") => new("IfcMember", "MULLION", null),
                ("IfcCurtainWall", "transom") => new("IfcMember", "USERDEFINED", "TRANSOM"),
                ("IfcCurtainWall", "panel") => new("IfcPlate", "CURTAIN_PANEL", null),
                ("IfcCurtainWall", "frame") => new("IfcMember", "USERDEFINED", "Panel frame"),
                (IfcClass.Stair, "flight") => new(IfcClass.StairFlight, "STRAIGHT", null),
                (IfcClass.Stair, "winders") => new(IfcClass.StairFlight, "WINDER", null),
                (IfcClass.Stair, "landing") => new(IfcClass.Slab, "LANDING", null),
                // A stair's railing on each side (forum topic 102): a balustrade, or a handrail on the wall.
                (IfcClass.Stair, "railing") => new("IfcRailing", p.Properties?.FirstOrDefault(x => x.Name == "Kind").Value == "balustrade" ? "BALUSTRADE" : "HANDRAIL", null),
                (IfcClass.Stair, "railing-glass") => new("IfcPlate", "SHEET", null),
                (IfcClass.Stair, "footing") => new(IfcClass.Footing, "STRIP_FOOTING", null),
                (IfcClass.Stair, "finish") => new("IfcCovering", "FLOORING", null),
                (IfcClass.Ramp, "flight") => new(IfcClass.RampFlight, "STRAIGHT", null),
                (IfcClass.Ramp, "landing") => new(IfcClass.Slab, "LANDING", null),
                (IfcClass.Ramp, "railing") => new("IfcRailing", "HANDRAIL", null),
                (IfcClass.Ramp, "finish") => new("IfcCovering", "FLOORING", null),
                (IfcClass.Footing, "footing") => new(IfcClass.Footing, "STRIP_FOOTING", null),
                (IfcClass.Footing, "step") => new(IfcClass.Footing, "PAD_FOOTING", null),
                _ => new("IfcBuildingElementPart", "NOTDEFINED", p.Name),
            };
        }

        /// <summary>A roof's timber member as IFC types it, by its role ("rafter:3", or "1:web:2" in a truss).</summary>
        static Kind TimberKind(string rest)
        {
            var bits = rest.Split(':');
            var role = bits.Length >= 2 && int.TryParse(bits[0], out _) ? bits[1] : bits[0];
            return role switch
            {
                "rafter" => new("IfcMember", "RAFTER", null),
                "purlin" => new("IfcMember", "PURLIN", null),
                "plate" => new("IfcMember", "PLATE", null),
                "collar" => new("IfcMember", "COLLAR", null),
                "topChord" or "bottomChord" => new("IfcMember", "CHORD", null),
                "web" => new("IfcMember", "MEMBER", null),
                "stud" or "end" or "king" or "jack" or "cripple" or "corner" or "backer" => new("IfcMember", "STUD", null),
                "noggin" or "blocking" => new("IfcMember", "MEMBER", null),
                "header" => new("IfcMember", "USERDEFINED", "Header"),
                "sill" => new("IfcMember", "USERDEFINED", "Sill"),
                "ridge" => new("IfcMember", "USERDEFINED", "Ridge beam"),
                "hip" => new("IfcMember", "USERDEFINED", "Hip rafter"),
                "valley" => new("IfcMember", "USERDEFINED", "Valley rafter"),
                "trimmer" => new("IfcMember", "USERDEFINED", "Trimmer"),
                _ => new("IfcMember", "MEMBER", null),
            };
        }

        /// <summary>
        /// The openings as solid voids, for clash tools (decision 289): each an IfcBuildingElementProxy PROVISIONFORVOID with the
        /// opening's body through its host's whole thickness, Pset_ProvisionForVoid (Shape, Width, Height, Diameter, Depth) and
        /// Lapin_OpeningVoid (mark, kind, host, opening, fill, offset), interfering with its host (IfcRelInterferesElements). Its
        /// GlobalId is derived from the opening's, the same at every export. The IfcOpeningElements stay as they are.
        /// </summary>
        void ProvisionVoids()
        {
            if (!o.Voids) return;
            var voids = Lapin.Platform.Coordination.OpeningVoids.Of(m, s.Catalog, new Lapin.Platform.Coordination.VoidOptions(o.VoidKinds ?? Lapin.Platform.Coordination.OpeningVoids.Kinds.ToHashSet(), o.VoidOffset));
            var n = 0;
            foreach (var v in voids)
            {
                // Only where its host went out (not a kind, storey or phase left out).
                if (!written.TryGetValue(v.HostId, out var host)) continue;
                var storey = v.StoreyId is { } sid ? m.Get(sid) : null;
                var (spatial, relTo, z0) = storey != null && storeyRef.TryGetValue(storey.Id, out var sr) ? (sr, storeyPlace[storey.Id], storeyZ[storey.Id]) : (siteRef, sitePlace, 0.0);
                var place = Place(relTo, 0);
                var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { BodyOf([Brep(v.Solid, Lapin.Platform.Coordination.OpeningVoids.Color, Frame.AtZ(z0))]) }));
                var gid = S(v.GlobalId);
                var entity = Product(new Kind(IfcClass.BuildingElementProxy, "PROVISIONFORVOID", null), gid, $"Opening void {v.Mark}", "Opening void", place, shape, v.Key);
                Contain(spatial, entity);
                Elements++;
                n++;
                var props = new List<string> { Single("Shape", Typed("IFCLABEL", S(v.Shape))), Single("Width", Typed("IFCPOSITIVELENGTHMEASURE", R(v.Width))), Single("Height", Typed("IFCPOSITIVELENGTHMEASURE", R(v.Height))) };
                if (v.Diameter is { } d) props.Add(Single("Diameter", Typed("IFCPOSITIVELENGTHMEASURE", R(d))));
                props.Add(Single("Depth", Typed("IFCPOSITIVELENGTHMEASURE", R(v.Depth))));
                Define(entity, f.Add("IFCPROPERTYSET", Gid(v.Key, "void-pset"), Null, S("Pset_ProvisionForVoid"), Null, L(props)), Gid(v.Key, "void-pset-rel"));
                var own = new List<string>
                {
                    Single("Mark", Typed("IFCLABEL", S(v.Mark))), Single("Kind", Typed("IFCLABEL", S(v.Kind))), Single("Host", Typed("IFCIDENTIFIER", S(GlobalIdOf(v.HostId)))),
                    Single("Offset", Typed("IFCLENGTHMEASURE", R(o.VoidOffset))),
                };
                if (v.OpeningId is { } op) own.Add(Single("Opening", Typed("IFCIDENTIFIER", S(GlobalIdOf(op)))));
                if (v.FillId is { } fill) own.Add(Single("Fill", Typed("IFCIDENTIFIER", S(GlobalIdOf(fill)))));
                Define(entity, f.Add("IFCPROPERTYSET", Gid(v.Key, "void-lapin"), Null, S(VoidSetName), Null, L(own)), Gid(v.Key, "void-lapin-rel"));
                f.Add("IFCRELINTERFERESELEMENTS", Gid(v.Key, "void-interferes"), Null, S("Opening void"), Null, host, entity, Null, S("Provision for void"), ".U.");
            }
            if (n > 0) Notes.Add($"{n} opening void{(n == 1 ? "" : "s")} written as IfcBuildingElementProxy PROVISIONFORVOID.");
        }

        /// <summary>The element's openings with their bodies, voiding it; a void that is an element of its own (a platform in the terrain) is related at the end, in IFC 4.3.</summary>
        void Openings(Entity e, string entity, string place, double z0, IVoidShapes? voids)
        {
            foreach (var rel in m.RelationsOf(e.Id, IfcClass.RelVoidsElement).Where(r => r.Relating == e.Id).ToList())
                foreach (var oid in rel.Related)
                {
                    if (m.Get(oid) is not { } op) continue;
                    if (op.Class != IfcClass.OpeningElement && RecessOf(op) is { } recess)
                    {
                        // A recess the element hosts (a wall reveal, decision 297): an IfcOpeningElement of its kind, with its intent.
                        var body = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { BodyOf([Brep(recess, null, Frame.AtZ(z0))]) }));
                        var pre = op.PredefinedType ?? "RECESS";
                        var recessRef = f.Add("IFCOPENINGELEMENT", Gid(op.Id), Null, S(op.Name), Null, Null, Place(place, 0), body, S(op.Id), E(pre));
                        written[op.Id] = recessRef;
                        f.Add("IFCRELVOIDSELEMENT", Gid(rel.Id), Null, Null, Null, entity, recessRef);
                        Define(recessRef, IntentOf(op, Gid(op.Id, IntentKey), new Kind(IfcClass.OpeningElement, pre, null)), Gid(op.Id, "intent-rel"));
                        Quantities(op, recessRef, false);
                        continue;
                    }
                    if (op.Class != IfcClass.OpeningElement)
                    {
                        // A void that is an element of its own (a platform in the terrain): its host's body already shows it, so it has no
                        // producer of its own; it is written without a body, with its intent, where its host is.
                        if (!written.ContainsKey(oid) && s.Catalog.Geometry(op.Class) == null && IsElementClass(op))
                        {
                            var k = KindOf(op);
                            var r = Product(k, Gid(op.Id), op.Name, k.ObjectType, Place(place, 0), Null, op.Id);
                            written[oid] = r;
                            Contain(contained.First(c => c.Value.Contains(entity)).Key, r);
                            Properties(op, k, r, null, [("LapinHost", e.Id)]);
                            Elements++;
                        }
                        pendingVoids.Add((rel, e.Id, oid));
                        continue;
                    }
                    var solid = voids?.Shape(op, m);
                    var shape = solid != null ? f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { BodyOf([Brep(solid, null, Frame.AtZ(z0))]) })) : Null;
                    var opening = f.Add("IFCOPENINGELEMENT", Gid(op.Id), Null, S(op.Name), Null, Null, Place(place, 0), shape, S(op.Id), E("OPENING"));
                    written[op.Id] = opening;
                    f.Add("IFCRELVOIDSELEMENT", Gid(rel.Id), Null, Null, Null, entity, opening);
                    Define(opening, IntentOf(op, Gid(op.Id, IntentKey), null), Gid(op.Id, "intent-rel"));
                    pendingFills.AddRange(m.RelationsOf(op.Id, IfcClass.RelFillsElement).Where(r => r.Relating == op.Id));
                }
            // The openings other elements host that pass through this one (forum topic 64): IFC voids one element by one opening
            // element, so each is an IfcOpeningElement of its own voiding this one, its GlobalId derived from the opening's and this element's.
            if (voids != null)
                foreach (var (oid, body) in voids.Through(e, m))
                {
                    var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { BodyOf([Brep(body, null, Frame.AtZ(z0))]) }));
                    var opening = f.Add("IFCOPENINGELEMENT", Gid(oid, "through/" + e.Id), Null, S(m.Get(oid)?.Name ?? "Opening"), S("Through " + (m.Get(oid) is { } op && m.RelatingOf(op.Id, IfcClass.RelVoidsElement) is { } host ? host.Name : "another element")),
                        Null, Place(place, 0), shape, S(oid), E("OPENING"));
                    f.Add("IFCRELVOIDSELEMENT", Gid(oid, "through-voids/" + e.Id), Null, Null, Null, entity, opening);
                }
            // The provision openings MEP runs cut by the cut order (decision 293): IfcOpeningElements of their own, voiding the host,
            // with what made them (Lapin_ProvisionOpening); the voids option writes them as provisions for voids too.
            if (e.Class is IfcClass.Wall or IfcClass.Slab or IfcClass.Roof)
                foreach (var p in Lapin.Platform.Coordination.Provisions.Of(m, s.Catalog, e).Where(p => p.Cuts))
                {
                    var body = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { BodyOf([Brep(p.Cutter, null, Frame.AtZ(z0))]) }));
                    var opening = f.Add("IFCOPENINGELEMENT", S(p.GlobalId), Null, S("Provision opening"), Null, Null, Place(place, 0), body, S(p.Key), E("OPENING"));
                    f.Add("IFCRELVOIDSELEMENT", Gid(p.Key, "provision-voids"), Null, Null, Null, entity, opening);
                    var props = new List<string>
                    {
                        Single("Source", Typed("IFCIDENTIFIER", S(GlobalIdOf(p.SourceId)))), Single("Shape", Typed("IFCLABEL", S(p.Shape))),
                        Single("Width", Typed("IFCPOSITIVELENGTHMEASURE", R(p.Width))), Single("Height", Typed("IFCPOSITIVELENGTHMEASURE", R(p.Height))),
                        Single("Depth", Typed("IFCPOSITIVELENGTHMEASURE", R(p.Depth))),
                    };
                    Define(opening, f.Add("IFCPROPERTYSET", Gid(p.Key, "provision-pset"), Null, S(ProvisionSetName), Null, L(props)), Gid(p.Key, "provision-pset-rel"));
                }
        }

        /// <summary>
        /// A reference's openings as its file gave them (docs/spec-ifc.md, Golurile referințelor; <see cref="StoredBodies.OpeningsKey"/>):
        /// each an IfcOpeningElement (or IfcVoidingFeature) with its kept body, voiding the element (IfcRelVoidsElement), its fills
        /// related at the end (IfcRelFillsElement), all under the file's GlobalIds. The element's body is then its uncut one
        /// (<see cref="StoredBodies.UncutKey"/>); a project kept before (its body cut, no openings) writes none, as before.
        /// </summary>
        void KeptOpenings(Entity e, string entity, string place, double z0)
        {
            if (!StoredBodies.Has(e) || e.Attrs[StoredBodies.OpeningsKey] is not JsonArray openings) return;
            foreach (var op in openings.OfType<JsonObject>())
            {
                if ((string?)op["id"] is not { } id || op[StoredBodies.Key] is not JsonObject stored) continue;
                var breps = StoredBodies.PartsOf(stored).Where(p => p.Solid.Faces.Length > 0).Select(p => Brep(p.Solid, null, Frame.AtZ(z0))).ToList();
                if (breps.Count == 0) continue;
                var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { BodyOf(breps) }));
                var feature = (string?)op["class"] == "IfcVoidingFeature";
                var opening = f.Add(feature ? "IFCVOIDINGFEATURE" : "IFCOPENINGELEMENT", Gid(id), Null, S((string?)op["name"]), Null, S((string?)op["objectType"]), Place(place, 0), shape, Null,
                    (string?)op["predefined"] is { Length: > 0 } pre ? E(pre) : Null);
                f.Add("IFCRELVOIDSELEMENT", (string?)op["rel"] is { } rel && GlobalId.IsValid(rel) ? S(rel) : Gid(id, "voids"), Null, Null, Null, entity, opening);
                foreach (var fill in (op["fills"] as JsonArray ?? []).OfType<JsonObject>())
                    if ((string?)fill["id"] is { } filler)
                        pendingKeptFills.Add(((string?)fill["rel"] is { } fr && GlobalId.IsValid(fr) ? S(fr) : Gid(id, "fills/" + filler), opening, filler));
            }
        }

        // ─── Properties and quantities ──────────────────────────────────────

        void Properties(Entity e, Kind kind, string entity, Entity? type, IReadOnlyList<(string Name, string Value)>? extra = null)
        {
            var common = new List<(string Name, string Value)>();
            // Its type's reference in the project (as Pset_…Common defines it, "type 'A-1'"): the type's mark (its own, else its category's rule's, decision 737), else its name (forum topic 177).
            if (type != null) common.Add(("Reference", Typed("IFCIDENTIFIER", S(MarkRules.TypeMark(m, type) is { Length: > 0 } typeMark ? typeMark : type.Name))));
            // LoadBearing: what the element or its type says (the Structural tick, a Pset value; the user's own value in the set still
            // wins below), else true on columns, beams and footings as before, else the views' rule when asked (DerivedLoadBearing).
            if (StructuralUsage.Classes.Contains(e.Class) && StructuralUsage.SaidOf(m, e) is { } said) common.Add(("LoadBearing", Typed("IFCBOOLEAN", said.Value ? True : False)));
            else if (kind.Class is IfcClass.Column or IfcClass.Beam or IfcClass.Footing) common.Add(("LoadBearing", Typed("IFCBOOLEAN", True)));
            else if (o.DerivedLoadBearing && StructuralUsage.Classes.Contains(e.Class)) common.Add(("LoadBearing", Typed("IFCBOOLEAN", StructuralUsage.ForViews(m, e).Effective ? True : False)));
            if (e.Class is IfcClass.Door or IfcClass.Window && type?.Attrs["spec"] is JsonObject spec && spec["isExternal"] is JsonValue ext && ext.TryGetValue<bool>(out var isExternal))
                common.Add(("IsExternal", Typed("IFCBOOLEAN", isExternal ? True : False)));
            // A stair's numbers, as Pset_StairCommon names them: the whole stair's risers and treads, its riser and tread.
            if (kind.Class == IfcClass.Stair && s.Quantities(e.Id).FirstOrDefault(q => q.Name == "Lapin_StairQuantities") is { } sq)
            {
                double? Q(string n) => sq.Quantities.FirstOrDefault(x => x.Name == n)?.Value;
                var treads = s.Quantities(e.Id).Where(q => q.Name == "Qto_StairFlightBaseQuantities").Sum(q => q.Quantities.FirstOrDefault(x => x.Name == "NumberOfTreads")?.Value ?? 0);
                if (Q("NumberOfRiser") is { } nr) common.Add(("NumberOfRiser", Typed("IFCCOUNTMEASURE", ((int)nr).ToString(System.Globalization.CultureInfo.InvariantCulture))));
                common.Add(("NumberOfTreads", Typed("IFCCOUNTMEASURE", ((int)treads).ToString(System.Globalization.CultureInfo.InvariantCulture))));
                if (Q("RiserHeight") is { } rh && rh > 0) common.Add(("RiserHeight", Typed("IFCPOSITIVELENGTHMEASURE", R(rh))));
                if (Q("TreadLength") is { } tl && tl > 0) common.Add(("TreadLength", Typed("IFCPOSITIVELENGTHMEASURE", R(tl))));
            }
            // A foreign element's own sets, as its file wrote them, stand for what Lapin would answer.
            var src = e.Attrs[IfcSource.Key] as JsonObject;
            var carried = src != null ? IfcCarry.Writer.SetNames(src).ToHashSet() : [];
            // The common set: what Lapin knows, and what the user wrote into it (their value over Lapin's of the same name).
            var commonName = src == null && CommonSets.Contains(kind.Class) ? $"Pset_{kind.Class[3..]}Common" : null;
            // Its Status from its phases (the user's own value over it), where the common set has one.
            if (commonName != null && PhaseStatus(e) is { } status && StandardPsets.All.Any(x => x.Name == commonName && x.Properties.ContainsKey("Status")))
                common.Add(("Status", Typed("IFCLABEL", S(status))));
            // The standard sets the element's module answers (a lift's capacity): into the common set, or sets of their own.
            var moduleSets = src == null ? s.Catalog.All<IIfcElementSets>().SelectMany(x => x.Sets(m, e)).ToList() : [];
            foreach (var set in moduleSets.Where(x => x.Name == commonName))
                foreach (var p in set.Properties)
                    if (!common.Any(c => c.Name == p.Name)) common.Add((p.Name, StandardValue(p)));
            foreach (var set in moduleSets.Where(x => x.Name != commonName))
            {
                var props = set.Properties.Select(p => Single(p.Name, StandardValue(p), p.Description)).ToList();
                if (props.Count > 0) Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, "pset/" + set.Name), Null, S(set.Name), Null, L(props)), Gid(e.Id, "pset-rel/" + set.Name));
            }
            var commonProps = commonName != null ? CommonWith(e, commonName, common) : [];
            if (commonProps.Count > 0)
                Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, "common"), Null, S(commonName), Null, L(commonProps)), Gid(e.Id, "common-rel"));
            NamedSets(e, entity, commonName, carried);
            Define(entity, IntentOf(e, Gid(e.Id, IntentKey), kind, extra), Gid(e.Id, "intent-rel"));
            var phase = new List<string>();
            if (e.Str("phaseCreated") is { } created) phase.Add(Single("Created", Typed("IFCLABEL", S(PhaseName(created)))));
            if (e.Str("phaseDemolished") is { } demolished) phase.Add(Single("Demolished", Typed("IFCLABEL", S(PhaseName(demolished)))));
            // Created and struck in one phase (formwork, a scaffold): temporary, as IFC's Status says it.
            if (e.Str("phaseCreated") is { } c0 && c0 == e.Str("phaseDemolished")) phase.Add(Single("Status", Typed("IFCLABEL", S("TEMPORARY"))));
            if (phase.Count > 0) Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, PhaseKey), Null, S(PhaseSetName), Null, L(phase)), Gid(e.Id, "phase-rel"));
            if (LightSet(e, Gid(e.Id, LightSetName)) is { } lightSet) Define(entity, lightSet, Gid(e.Id, "light-rel"));
            // The user's own parameters (those of its type are on the type, which IFC readers pass on to it; also here when asked, WorkedOut).
            var own = ParamsOf(e, Gid(e.Id, ParamsKey));
            if (own != null) Define(entity, own, Gid(e.Id, "params-rel"));
            StandardSets(e, null, entity, e.Id);
            if (src == null) return;
            // The file's sets, with the user's values in the sets of the same name; a set of Lapin's that was written above, Lapin's.
            HashSet<string> lapins = [IntentSetName, OldIntentSetName];
            if (phase.Count > 0) lapins.Add(PhaseSetName);
            if (own != null) lapins.Add(ParamsSetName);
            var user = NamedParams(e).Where(x => carried.Contains(x.Set)).ToDictionary(x => x.Set, x => x.Props);
            Carry.Element(m, src, entity, GlobalIdOf(e.Id), user, lapins, (n, v) => Single(n, v));
        }

        /// <summary>A standard property's value, typed by its measure.</summary>
        string StandardValue(IfcStandardProperty p) => p.Value switch
        {
            bool b => Typed(p.Measure, b ? True : False),
            double d => Typed(p.Measure, R(d)),
            int i => Typed(p.Measure, i.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            _ => Typed(p.Measure, S(p.Value.ToString())),
        };

        /// <summary>The standard sets the modules give an element or one of its parts (IFC 4.3), each written as it is named.</summary>
        void StandardSets(Entity e, GeometryPart? part, string entity, string seed)
        {
            if (!o.Ifc43) return;
            foreach (var src in s.Catalog.All<IIfcAlignments>())
                foreach (var set in src.StandardSets(m, e, part))
                {
                    var props = set.Properties.Select(p => Single(p.Name, p.Value switch
                    {
                        bool b => Typed(p.Measure, b ? True : False),
                        double d => Typed(p.Measure, R(d)),
                        int i => Typed(p.Measure, R(i)),
                        _ => Typed(p.Measure, S(p.Value.ToString())),
                    })).ToList();
                    if (props.Count > 0) Define(entity, f.Add("IFCPROPERTYSET", Gid(seed, "pset/" + set.Name), Null, S(set.Name), Null, L(props)), Gid(seed, "pset-rel/" + set.Name));
                }
        }

        /// <summary>The project's information as properties: each field by its key (a text over lines as IfcText), each parameter of the user's as its type (IfcReal, IfcDate, IfcLabel).</summary>
        List<string> ProjectInformation()
        {
            var o = new List<string>();
            foreach (var (key, _) in Lapin.Platform.Documents.ProjectInfo.Fields)
                if (Lapin.Platform.Documents.ProjectInfo.Get(m, key) is { } v) o.Add(Single(key, Typed(v.Contains('\n') ? "IFCTEXT" : "IFCLABEL", S(v))));
            foreach (var c in Lapin.Platform.Documents.ProjectInfo.Customs(m))
            {
                if (c.Value is not { } v) continue;
                var value = c.Type switch
                {
                    "number" when double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) => Typed("IFCREAL", R(n)),
                    "date" => Typed("IFCDATE", S(v)),
                    _ => Typed("IFCLABEL", S(v)),
                };
                o.Add(Single(c.Key, value, c.Label));
            }
            return o;
        }

        /// <summary>
        /// An entity's parameters as the file writes them: a formula's value worked out (decision 550: a number, or a text from its
        /// storey's, its project's… parameters), as the model's tags and schedules show it; the formula itself is the model's alone.
        /// </summary>
        List<JsonObject> WorkedOut(Entity e)
        {
            var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
            var own = ParamFormulas.Resolved(m, e, type);
            if (!o.TypePropertiesOnOccurrences || type == null) return own;
            // Asked (issue #30): the type's own properties too, as the type writes them (its instance defaults are each element's
            // already), after the element's; a property the element has of its own (the same set and name) is the element's.
            var typeOwn = ParamFormulas.Resolved(m, type, m.RelatingOf(type.Id, IfcClass.RelDefinesByType))
                .Where(p => !Params.IsInstanceDefault(p) && (string?)p["name"] is { } name && !own.Any(x => Params.Same(x, Params.SetOf(p), name)));
            return [.. own, .. typeOwn];
        }

        /// <summary>An entity's own parameters (those in no named set) as a typed property set; null when it has none.</summary>
        string? ParamsOf(Entity e, string gid)
        {
            var own = new List<string>();
            foreach (var p in WorkedOut(e))
            {
                // A type's instance default is each element's, not the type's (TypeDefaults).
                if (Params.SetOf(p) != null || Params.IsInstanceDefault(p) || (string?)p["name"] is not { } pname || ParamValue(p, false) is not { } value) continue;
                own.Add(Single(pname, value));
            }
            return own.Count > 0 ? f.Add("IFCPROPERTYSET", gid, Null, S(ParamsSetName), Null, L(own)) : null;
        }

        /// <summary>
        /// A parameter's value as IFC types it, in any set (a standard one or the user's own): a length, an area, an angle as
        /// their measures in the file's units (metres, square metres, radians: Lapin holds an angle in degrees), a number a real,
        /// yes or no a boolean; a text in a standard set is a label, as IFC's Pset_…Common have them.
        /// </summary>
        static string? ParamValue(JsonObject p, bool standard) => p["value"] is not { } pv ? null : (string?)p["kind"] switch
        {
            "length" => Json.Number(pv) is { } l ? Typed("IFCLENGTHMEASURE", R(l)) : null,
            "area" => Json.Number(pv) is { } ar ? Typed("IFCAREAMEASURE", R(ar)) : null,
            "angle" => Json.Number(pv) is { } an ? Typed("IFCPLANEANGLEMEASURE", R(an * Math.PI / 180)) : null,
            "number" => Json.Number(pv) is { } nm ? Typed("IFCREAL", R(nm)) : null,
            "bool" => pv is JsonValue bv && bv.TryGetValue<bool>(out var yes) ? Typed("IFCBOOLEAN", yes ? True : False) : null,
            "choice" => Typed("IFCLABEL", S(pv.ToString())),
            _ => Typed(standard ? "IFCLABEL" : "IFCTEXT", S(pv.ToString())),
        };

        /// <summary>A material's parameters set by set: its own (no set) in Lapin_Material, a standard set's numbers as their IFC measures (W/(m·K), kg/m³).</summary>
        static List<(string Set, List<(string Name, string Value)> Props)> MaterialSets(Entity mat)
        {
            var sets = new List<(string Set, List<(string Name, string Value)> Props)>();
            foreach (var p in (mat.Attrs["params"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if ((string?)p["name"] is not { Length: > 0 } name) continue;
                var set = Params.SetOf(p) ?? StandardPsets.MaterialOwn;
                var standard = StandardPsets.IsStandard(set);
                var value = standard && (string?)p["kind"] == "number" && StandardPsets.MaterialMeasures.TryGetValue(name, out var measure) && Json.Number(p["value"]) is { } n
                    ? Typed(measure, R(n)) : ParamValue(p, standard);
                if (value is null) continue;
                var i = sets.FindIndex(x => string.Equals(x.Set, set, StringComparison.OrdinalIgnoreCase));
                if (i < 0) sets.Add((set, [(name, value)])); else sets[i].Props.Add((name, value));
            }
            return sets;
        }

        /// <summary>
        /// A material's mechanical values for the structural analysis view (<see cref="StructuralMaterials"/>, as Lapin's analysis
        /// takes them) added to its sets where it states none: Pset_MaterialMechanical's YoungModulus and PoissonRatio, and
        /// Pset_MaterialCommon's MassDensity, in IFC's SI units. Returns each added value's source, for its description.
        /// </summary>
        Dictionary<(string Set, string Name), string> AnalysisValues(Entity mat, List<(string Set, List<(string Name, string Value)> Props)> sets)
        {
            var sources = new Dictionary<(string, string), string>();
            var sm = StructuralMaterials.Of(m, mat);
            void Add(string set, string name, MaterialValue? v)
            {
                if (v == null) return;
                var i = sets.FindIndex(x => string.Equals(x.Set, set, StringComparison.OrdinalIgnoreCase));
                if (i >= 0 && sets[i].Props.Any(p => p.Name == name)) return;
                var value = Typed(StandardPsets.MaterialMeasures[name], R(v.Value));
                if (i < 0) sets.Add((set, [(name, value)])); else sets[i].Props.Add((name, value));
                sources[(i < 0 ? set : sets[i].Set, name)] = $"Lapin: {v.Source}";
            }
            Add("Pset_MaterialMechanical", "YoungModulus", sm.E);
            Add("Pset_MaterialMechanical", "PoissonRatio", sm.Poisson);
            Add("Pset_MaterialCommon", "MassDensity", sm.Density);
            return sources;
        }

        /// <summary>The user's parameters in named sets (Pset_DoorCommon, a set of their own), set by set.</summary>
        List<(string Set, List<(string Name, string Value)> Props)> NamedParams(Entity e)
        {
            var sets = new List<(string Set, List<(string Name, string Value)> Props)>();
            foreach (var p in WorkedOut(e))
            {
                if (Params.SetOf(p) is not { } set || Params.IsInstanceDefault(p) || (string?)p["name"] is not { } name || ParamValue(p, StandardPsets.IsStandard(set)) is not { } value) continue;
                var i = sets.FindIndex(x => string.Equals(x.Set, set, StringComparison.OrdinalIgnoreCase));
                if (i < 0) sets.Add((set, [(name, value)])); else sets[i].Props.Add((name, value));
            }
            return sets;
        }

        /// <summary>A common set's properties: Lapin's, each replaced by the user's value of the same name, then the user's others.</summary>
        List<string> CommonWith(Entity e, string setName, List<(string Name, string Value)> computed)
        {
            var user = NamedParams(e).FirstOrDefault(x => x.Set == setName).Props ?? [];
            var merged = computed.Where(c => !user.Any(u => u.Name == c.Name)).Concat(user);
            return merged.Select(p => Single(p.Name, p.Value)).ToList();
        }

        /// <summary>The user's named sets other than <paramref name="except"/> (the common set, written with Lapin's) and the <paramref name="carried"/> (written with the file's), each as its own property set.</summary>
        void NamedSets(Entity e, string entity, string? except, IReadOnlySet<string> carried)
        {
            foreach (var (set, props) in NamedParams(e))
            {
                if (set == except || carried.Contains(set)) continue;
                Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, "params:" + set), Null, S(set), Null, L(props.Select(p => Single(p.Name, p.Value)).ToArray())), Gid(e.Id, "params-rel:" + set));
            }
        }

        /// <summary>
        /// The intent set: the entity's attributes as Lapin holds them, typed by
        /// the class's fields (lengths as IfcLengthMeasure, angles as reals in
        /// degrees, lists and objects as JSON text), the Lapin class when the IFC
        /// one differs, and the version of this set.
        /// </summary>
        string IntentOf(Entity e, string gid, Kind? kind, IReadOnlyList<(string Name, string Value)>? extra = null, IReadOnlySet<string>? only = null) =>
            f.Add("IFCPROPERTYSET", gid, Null, S(IntentSetName), Null, L(IntentProperties(e, kind, extra, only)));

        static readonly HashSet<string> ProjectIntent = ["location", "phases", "lengthUnit", "longName", "phase", "crs", LevelDatum.Key];

        List<string> IntentProperties(Entity e, Kind? kind, IReadOnlyList<(string Name, string Value)>? extra = null, IReadOnlySet<string>? only = null)
        {
            var fields = (s.Catalog.Class(e.Class)?.Fields ?? []).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.First().Kind);
            var props = new List<string>();
            foreach (var (key, v) in e.Attrs)
            {
                // A stored body (a reference from IFC) is written as geometry, not as text; the project's standards stay in .lapin.
                if (v == null || key == StoredBodies.Key || key == StoredBodies.SolidBodyKey || key == StoredBodies.BodyOf || key == StoredBodies.XfKey || StoredBodies.ExportOnly.Contains(key) || key == Plan2d.Key || key == IfcSource.Key || key == ReferenceQuantities.Key || key == ReferenceLayers.AxisKey || key == ReferenceLayers.PartOfKey || privateKeys.Contains(key) || (only != null && !only.Contains(key)) || filters.Any(x => x.IsPrivate(e, key))) continue;
                // A foreign element's properties go as the sets its file had, not as text.
                if (key == "properties" && e.Attrs[IfcSource.Key] != null) continue;
                var field = fields.GetValueOrDefault(key);
                var value = v switch
                {
                    JsonValue jv when jv.TryGetValue<bool>(out var b) => Typed("IFCBOOLEAN", b ? True : False),
                    // Text that reads as a number stays text (a room's "001"): the import gives back what it is written as.
                    JsonValue jv when jv.GetValueKind() == System.Text.Json.JsonValueKind.String => Typed("IFCLABEL", S(jv.ToString())),
                    JsonValue jv when Json.Number(jv) is { } d => Typed(field == "length" ? "IFCLENGTHMEASURE" : "IFCREAL", R(d)),
                    JsonValue jv => Typed("IFCLABEL", S(jv.ToString())),
                    _ => Typed("IFCTEXT", S(v.ToJsonString(Json.Options))),
                };
                props.Add(Single(key, value, field == "angle" ? "degrees" : null));
            }
            // Whatever holds a stored body (a mesh's piece too, which never said so) comes back from the body the file gives it, not as a native element without one.
            if (StoredBodies.Has(e) && e.Attrs["reference"] == null) props.Add(Single("reference", Typed("IFCBOOLEAN", True)));
            if (kind != null && kind.Class != e.Class) props.Add(Single("LapinClass", Typed("IFCLABEL", S(e.Class))));
            if (e.PredefinedType != null) props.Add(Single("LapinPredefinedType", Typed("IFCLABEL", S(e.PredefinedType))));
            foreach (var (name, value) in extra ?? []) props.Add(Single(name, Typed("IFCLABEL", S(value))));
            props.Add(Single("LapinVersion", Typed("IFCLABEL", S(Version))));
            return props;
        }

        /// <summary>The element's quantity sets, by the kind IFC measures; a part's set goes on the part when it is an element of its own, else on the element, named after the part.</summary>
        void Quantities(Entity e, string entity, bool grouped)
        {
            var carried = e.Attrs[IfcSource.Key] is JsonObject src ? IfcCarry.Writer.SetNames(src).ToHashSet() : [];
            // A reference from an IFC file goes back with its file's quantity sets only (carried): what Lapin measured on its body is not the file's.
            if (e.Attrs[IfcSource.Key] != null && ReferenceQuantities.Applies(e)) return;
            foreach (var set in s.Quantities(e.Id))
            {
                if (carried.Contains(set.Name)) continue;
                var target = set.Part != null && grouped && partRefs.TryGetValue((e.Id, set.Part), out var pr) ? pr : entity;
                var q = QuantityItems(set);
                if (q.Count == 0) continue;
                var key = set.Part != null ? $"{set.Name}/{set.Part}" : set.Name;
                var description = target == entity && set.Part != null ? set.PartName ?? set.Part : null;
                Define(target, f.Add("IFCELEMENTQUANTITY", Gid(e.Id, "qto/" + key), Null, S(set.Name), S(description), Null, L(q)), Gid(e.Id, "qto-rel/" + key));
            }
        }

        // ─── The railway: its facility, its parts, its alignments (IFC 4.3) ──

        readonly Dictionary<string, string> railwayParts = [];
        string railwayRef = "";
        /// <summary>Each alignment written: the curve its distances run along (the gradient curve, else the horizontal) and its placement, by the alignment's id.</summary>
        readonly Dictionary<string, (string Basis, string Place)> alignmentCurves = [];

        /// <summary>
        /// An element placed along its alignment, as its module says (IIfcAlignments):
        /// an IfcLinearPlacement at a distance along the alignment's curve, heading
        /// along it, with the absolute frame as its CartesianPosition — and that
        /// frame, for the body to be written in. Null: placed by coordinates.
        /// </summary>
        (string Place, Frame Frame)? LinearPlace(Entity e)
        {
            foreach (var src in s.Catalog.All<IIfcAlignments>())
            {
                if (src.LinearPlacementOf(m, e) is not { } lp || !alignmentCurves.TryGetValue(lp.AlignmentId, out var curve)) continue;
                var point = f.Add("IFCPOINTBYDISTANCEEXPRESSION", Typed("IFCLENGTHMEASURE", R(lp.DistanceAlong)), Null, Null, Null, curve.Basis);
                var up = f.Add("IFCDIRECTION", "(0.,0.,1.)");
                var ahead = f.Add("IFCDIRECTION", L(new[] { R(Math.Cos(lp.Heading)), R(Math.Sin(lp.Heading)), R(0) }));
                var cartesian = f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", L(new[] { R(lp.Origin.X), R(lp.Origin.Y), R(lp.Origin.Z) })), up, ahead);
                return (f.Add("IFCLINEARPLACEMENT", sitePlace, f.Add("IFCAXIS2PLACEMENTLINEAR", point, up, ahead), cartesian), new Frame(lp.Origin, lp.Heading));
            }
            return null;
        }

        /// <summary>The railway part an element belongs to, as its module says; null: none, or not IFC 4.3.</summary>
        string? RailwayPart(Entity e)
        {
            if (!o.Ifc43) return null;
            foreach (var src in s.Catalog.All<IIfcAlignments>())
                if (src.RailwayPartOf(m, e) is { } part)
                {
                    if (!railwayParts.TryGetValue(part, out var r)) railwayParts[part] = r = RailwayPartRef(part);
                    return r;
                }
            return null;
        }

        /// <summary>IfcRailway under the site, once; each part (TRACK, TURNOUTTRACK, LINESIDE…) aggregated in it, once.</summary>
        string RailwayPartRef(string part)
        {
            if (railwayRef.Length == 0)
            {
                railwayRef = f.Add("IFCRAILWAY", Gid(project.Id, "railway"), Null, S("Railway"), Null, Null, Place(sitePlace, 0), Null, Null, E("ELEMENT"), E("NOTDEFINED"));
                refIdOf[railwayRef] = project.Id;
                f.Add("IFCRELAGGREGATES", Gid(project.Id, "aggregates/railway"), Null, Null, Null, siteRef, L(new[] { railwayRef }));
            }
            // IfcRailwayPartTypeEnum: the track as its structure, the turnouts as a superstructure, what stands beside the track as a
            // lineside structure; the usage along the line. IFC4X3_ADD2 renamed the items (TRACKSTRUCTURE → TRACK, TURNOUTSUPERSTRUCTURE
            // → TURNOUTTRACK, LINESIDESTRUCTURE → LINESIDE); the first IFC4X3 keeps the long names.
            var add2 = o.Schema.Contains("ADD", StringComparison.OrdinalIgnoreCase);
            var (predefined, name) = part switch
            {
                "TRACK" => (add2 ? "TRACK" : "TRACKSTRUCTURE", "Track"),
                "TURNOUTTRACK" => (add2 ? "TURNOUTTRACK" : "TURNOUTSUPERSTRUCTURE", "Turnouts"),
                "LINESIDE" => (add2 ? "LINESIDE" : "LINESIDESTRUCTURE", "Lineside"),
                _ => ("USERDEFINED", part),
            };
            var r = f.Add("IFCRAILWAYPART", Gid(project.Id, "railway/" + part), Null, S(name), Null, predefined == "USERDEFINED" ? S(part) : Null, Place(sitePlace, 0), Null, Null, E("ELEMENT"), E("LONGITUDINAL"), E(predefined));
            refIdOf[r] = project.Id + "/" + part;
            f.Add("IFCRELAGGREGATES", Gid(project.Id, "railway/" + part + "/aggregates"), Null, Null, Null, railwayRef, L(new[] { r }));
            // Lapin's track is ballasted (its section is a ballast bed on a sub-ballast).
            if (part == "TRACK")
                Define(r, f.Add("IFCPROPERTYSET", Gid(project.Id, "railway/" + part + "/pset"), Null, S("Pset_RailwayTrackStructurePart"), Null, L(new[] { Single("HasBallastTrack", Typed("IFCBOOLEAN", True)) })), Gid(project.Id, "railway/" + part + "/pset-rel"));
            return r;
        }

        string bridgeRef = "";

        /// <summary>
        /// IFC 4.3: the bridge part an element stands in (<c>ifcBridgePart</c>, its IfcBridgePartTypeEnum: an abutment written as an
        /// assembly of its wings and face wall, decision 789 S3), one per element, aggregated in the project's bridge under the site
        /// (made once); null when it has none.
        /// </summary>
        string? BridgePart(Entity e)
        {
            if (e.Str("ifcBridgePart") is not { Length: > 0 } kind) return null;
            if (bridgeRef.Length == 0)
            {
                bridgeRef = f.Add("IFCBRIDGE", Gid(project.Id, "bridge"), Null, S("Bridge"), Null, Null, Place(sitePlace, 0), Null, Null, E("ELEMENT"), E("NOTDEFINED"));
                refIdOf[bridgeRef] = project.Id;
                f.Add("IFCRELAGGREGATES", Gid(project.Id, "aggregates/bridge"), Null, Null, Null, siteRef, L(new[] { bridgeRef }));
            }
            var predefined = kind.ToUpperInvariant() is var k && BridgePartTypes.Contains(k) ? k : "USERDEFINED";
            var r = f.Add("IFCBRIDGEPART", Gid(e.Id, "bridge-part"), Null, S(e.Name), Null, predefined == "USERDEFINED" ? S(kind) : Null, Place(sitePlace, 0), Null, Null, E("ELEMENT"), E("NOTDEFINED"), E(predefined));
            refIdOf[r] = e.Id + "/bridge-part";
            f.Add("IFCRELAGGREGATES", Gid(e.Id, "bridge-part/aggregates"), Null, Null, Null, bridgeRef, L(new[] { r }));
            return r;
        }

        static readonly HashSet<string> BridgePartTypes = ["ABUTMENT", "DECK", "DECK_SEGMENT", "FOUNDATION", "PIER", "PIER_SEGMENT", "PYLON", "SUBSTRUCTURE", "SUPERSTRUCTURE", "SURFACESTRUCTURE"];

        /// <summary>Nothing of the railway's spatial tree is made ahead; the parts come as the elements ask for them.</summary>
        void Railway() { }

        /// <summary>
        /// The alignments the modules offer (IIfcAlignments), IFC 4.3 only: each an
        /// IfcAlignment aggregated by the project and referenced in the site, with
        /// its horizontal, vertical and cant layers nested in it, each layer's
        /// segments nested in order and closed by the zero-length segment the
        /// concept template asks for, every segment's design parameters as the
        /// module gives them; its plan drawn through as the Axis representation.
        /// </summary>
        void Alignments()
        {
            if (!o.Ifc43) return;
            var all = s.Catalog.All<IIfcAlignments>().SelectMany(src => src.Alignments(m)).ToList();
            if (all.Count == 0) return;
            var refs = new List<string>();
            foreach (var a in all)
            {
                string Gi(string key) => Gid(a.Id, "alignment/" + key);
                var reps = new List<string>();
                // The geometry: the horizontal as IfcCompositeCurve (FootPrint), the vertical as IfcGradientCurve over it and the
                // cant as IfcSegmentedReferenceCurve over that (Axis, 3D); each layer's segments remember their curve segments
                // for their own representation. Without the curves, the sampled plan as a polycurve.
                string? composite = null, gradient = null;
                var segmentItems = new Dictionary<(string Layer, int Source), List<string>>();
                if (a.Curves is { } curves)
                {
                    string Items(IReadOnlyList<IfcCurveSegmentSpec> specs, string layer)
                    {
                        var ids = new List<string>();
                        foreach (var sp in specs)
                        {
                            var cs = CurveSegment(sp);
                            ids.Add(cs);
                            (segmentItems.TryGetValue((layer, sp.Source), out var l) ? l : segmentItems[(layer, sp.Source)] = []).Add(cs);
                        }
                        return L(ids);
                    }
                    composite = f.Add("IFCCOMPOSITECURVE", Items(curves.Horizontal, "horizontal"), False);
                    if (curves.Vertical != null) gradient = f.Add("IFCGRADIENTCURVE", Items(curves.Vertical, "vertical"), False, composite, Null);
                    var reference = curves.Cant != null && gradient != null ? f.Add("IFCSEGMENTEDREFERENCECURVE", Items(curves.Cant, "cant"), False, gradient, Null) : null;
                    if (footprint == "") footprint = f.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("FootPrint"), S("Model"), Derived, Derived, Derived, Derived, model, Null, E("PLAN_VIEW"), Null);
                    reps.Add(f.Add("IFCSHAPEREPRESENTATION", footprint, S("FootPrint"), S("Curve2D"), L(new[] { composite })));
                    reps.Add((reference ?? gradient) is { } axisCurve
                        ? f.Add("IFCSHAPEREPRESENTATION", axis, S("Axis"), S("Curve3D"), L(new[] { axisCurve }))
                        : f.Add("IFCSHAPEREPRESENTATION", axis, S("Axis"), S("Curve2D"), L(new[] { composite })));
                }
                else if (a.Plan.Count >= 2)
                {
                    var points = f.Add("IFCCARTESIANPOINTLIST2D", L(a.Plan.Select(p => L(new[] { R(p.X), R(p.Y) }))), Null);
                    reps.Add(f.Add("IFCSHAPEREPRESENTATION", axis, S("Axis"), S("Curve2D"), L(new[] { f.Add("IFCINDEXEDPOLYCURVE", points, Null, False) })));
                }
                var shape = reps.Count > 0 ? f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(reps)) : Null;
                var place = Place(sitePlace, 0);
                if ((gradient ?? composite) is { } along) alignmentCurves[a.Id] = (along, place);
                var entity = f.Add("IFCALIGNMENT", Gid(a.Id, "alignment"), Null, S(a.Name), Null, Null, place, shape, Null);
                refs.Add(entity);
                var layers = new List<string>();

                var hs = new List<string>();
                foreach (var (sg, i) in a.Horizontal.Select((x, i) => (x, i)))
                    hs.Add(Seg(Gi($"h/{i}"), f.Add("IFCALIGNMENTHORIZONTALSEGMENT", Null, Null, P2(sg.Start), R(sg.Direction), R(sg.StartRadius), R(sg.EndRadius), R(sg.Length), sg.GravityCenterLineHeight is { } h ? R(h) : Null, E(sg.Type)), "horizontal", i));
                if (a.Horizontal.Count > 0)
                {
                    var last = a.Horizontal[^1];
                    hs.Add(Seg(Gi("h/end"), f.Add("IFCALIGNMENTHORIZONTALSEGMENT", Null, Null, P2(EndOf(a.Plan, last)), R(last.Direction), R(last.EndRadius), R(last.EndRadius), R(0), Null, E("LINE")), "horizontal", -1));
                }
                var horizontal = f.Add("IFCALIGNMENTHORIZONTAL", Gi("h"), Null, S("Horizontal"), Null, Null, Null, Null);
                f.Add("IFCRELNESTS", Gi("h/nests"), Null, Null, Null, horizontal, L(hs));
                layers.Add(horizontal);

                if (a.Vertical is { Count: > 0 } vs)
                {
                    var list = new List<string>();
                    foreach (var (v, i) in vs.Select((x, i) => (x, i)))
                        list.Add(Seg(Gi($"v/{i}"), f.Add("IFCALIGNMENTVERTICALSEGMENT", Null, Null, R(v.StartDistAlong), R(v.HorizontalLength), R(v.StartHeight), R(v.StartGradient), R(v.EndGradient), v.Radius is { } r ? R(r) : Null, E(v.Type)), "vertical", i));
                    var lv = vs[^1];
                    list.Add(Seg(Gi("v/end"), f.Add("IFCALIGNMENTVERTICALSEGMENT", Null, Null, R(lv.StartDistAlong + lv.HorizontalLength), R(0), R(EndHeight(lv)), R(lv.EndGradient), R(lv.EndGradient), Null, E("CONSTANTGRADIENT")), "vertical", -1));
                    var vertical = f.Add("IFCALIGNMENTVERTICAL", Gi("v"), Null, S("Vertical"), Null, Null, Null, Null);
                    f.Add("IFCRELNESTS", Gi("v/nests"), Null, Null, Null, vertical, L(list));
                    layers.Add(vertical);
                }

                if (a.Cant is { Segments.Count: > 0 } c)
                {
                    var list = new List<string>();
                    foreach (var (cs, i) in c.Segments.Select((x, i) => (x, i)))
                        list.Add(Seg(Gi($"c/{i}"), f.Add("IFCALIGNMENTCANTSEGMENT", Null, Null, R(cs.StartDistAlong), R(cs.HorizontalLength), R(cs.StartCantLeft), cs.EndCantLeft is { } el ? R(el) : Null, R(cs.StartCantRight), cs.EndCantRight is { } er ? R(er) : Null, E(cs.Type)), "cant", i));
                    var lc = c.Segments[^1];
                    list.Add(Seg(Gi("c/end"), f.Add("IFCALIGNMENTCANTSEGMENT", Null, Null, R(lc.StartDistAlong + lc.HorizontalLength), R(0), R(lc.EndCantLeft ?? lc.StartCantLeft), Null, R(lc.EndCantRight ?? lc.StartCantRight), Null, E("CONSTANTCANT")), "cant", -1));
                    var cant = f.Add("IFCALIGNMENTCANT", Gi("c"), Null, S("Cant"), Null, Null, Null, Null, R(c.RailHeadDistance));
                    f.Add("IFCRELNESTS", Gi("c/nests"), Null, Null, Null, cant, L(list));
                    layers.Add(cant);
                }
                // The referents (the start station, the kilometre posts), placed linearly on the alignment's curve and
                // nested in it after the layers, each with its Pset_Stationing.
                if (a.Referents is { Count: > 0 } referents && (gradient ?? composite) is { } basis)
                {
                    foreach (var (r, i) in referents.Select((x, i) => (x, i)))
                    {
                        var point = f.Add("IFCPOINTBYDISTANCEEXPRESSION", Typed("IFCLENGTHMEASURE", X(r.DistanceAlong)), Null, Null, Null, basis);
                        var linear = f.Add("IFCLINEARPLACEMENT", place, f.Add("IFCAXIS2PLACEMENTLINEAR", point, Null, Null), Null);
                        var referent = f.Add("IFCREFERENT", Gi($"referent/{i}"), Null, S(r.Name), Null, Null, linear, Null, E(r.Type));
                        var props = new List<string> { Single("Station", Typed("IFCLENGTHMEASURE", R(r.Station))) };
                        if (r.HasIncreasingStation is { } increasing) props.Add(Single("HasIncreasingStation", Typed("IFCBOOLEAN", increasing ? True : False)));
                        if (r.IncomingStation is { } incoming) props.Add(Single("IncomingStation", Typed("IFCLENGTHMEASURE", R(incoming))));
                        Define(referent, f.Add("IFCPROPERTYSET", Gi($"referent/{i}/stationing"), Null, S("Pset_Stationing"), Null, L(props)), Gi($"referent/{i}/stationing-rel"));
                        layers.Add(referent);
                    }
                }
                f.Add("IFCRELNESTS", Gi("layers"), Null, Null, Null, entity, L(layers));
                // Where its stationing starts, as the alignment's own property set.
                Define(entity, f.Add("IFCPROPERTYSET", Gi("stationing"), Null, S("Lapin_Stationing"), Null, L(new[] { Single("StartStation", $"IFCLENGTHMEASURE({R(a.StartStation)})") })), Gi("stationing-rel"));

                // A semantic segment's representation: its curve segment(s), placed as the alignment is (a product with a shape
                // needs a placement), or nothing without the curves.
                string Seg(string gid, string design, string layer, int source) =>
                    segmentItems.TryGetValue((layer, source), out var items)
                        ? f.Add("IFCALIGNMENTSEGMENT", gid, Null, Null, Null, Null, place,
                            f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { f.Add("IFCSHAPEREPRESENTATION", axis, S("Axis"), S("Segment"), L(items)) })), design)
                        : f.Add("IFCALIGNMENTSEGMENT", gid, Null, Null, Null, Null, Null, Null, design);
            }
            f.Add("IFCRELAGGREGATES", Gid(project.Id, "aggregates/alignments"), Null, Null, Null, projectRef, L(refs));
            f.Add("IFCRELREFERENCEDINSPATIALSTRUCTURE", Gid(project.Id, "site/alignments"), Null, Null, Null, L(refs), siteRef);

            // An IfcCurveSegment: its transition, its placement (2D, or 3D with the cant's tilted axis), SegmentStart and
            // SegmentLength on the parent curve, the parent at its own origin. Full precision: a cubic's A₃ or a parabola's
            // C are far below the nine decimals the rest of the file keeps.
            string CurveSegment(IfcCurveSegmentSpec sp)
            {
                var location = f.Add("IFCCARTESIANPOINT", L(sp.Location.Select(X)));
                var refDirection = f.Add("IFCDIRECTION", L(sp.RefDirection.Select(X)));
                var placement = sp.Location.Count >= 3
                    ? f.Add("IFCAXIS2PLACEMENT3D", location, sp.Axis != null ? f.Add("IFCDIRECTION", L(sp.Axis.Select(X))) : Null, refDirection)
                    : f.Add("IFCAXIS2PLACEMENT2D", location, refDirection);
                return f.Add("IFCCURVESEGMENT", E(sp.Transition), placement, Typed("IFCLENGTHMEASURE", X(sp.Start)), Typed("IFCLENGTHMEASURE", X(sp.Length)), Parent(sp.Parent));
            }

            string Parent(IfcParentCurve c)
            {
                string Origin() => f.Add("IFCAXIS2PLACEMENT2D", f.Add("IFCCARTESIANPOINT", "(0.,0.)"), Null);
                static string T(double? v) => v is { } x ? X(x) : Null;
                switch (c.Kind)
                {
                    case "circle": return f.Add("IFCCIRCLE", f.Add("IFCAXIS2PLACEMENT2D", f.Add("IFCCARTESIANPOINT", L(new[] { X(c.Centre.X), X(c.Centre.Y) })), Null), X(c.Radius));
                    case "clothoid": return f.Add("IFCCLOTHOID", Origin(), X(c.A));
                    case "polynomial": return f.Add("IFCPOLYNOMIALCURVE", Origin(), L(c.X!.Select(X)), L(c.Y!.Select(X)), Null);
                    case "spiral":
                    {
                        // IFC lists the terms from the highest power down.
                        var terms = c.Terms!;
                        var attrs = new[] { Origin() }.Concat(terms.Reverse().Select(T)).ToArray();
                        return f.Add(terms.Count switch { 3 => "IFCSECONDORDERPOLYNOMIALSPIRAL", 4 => "IFCTHIRDORDERPOLYNOMIALSPIRAL", _ => "IFCSEVENTHORDERPOLYNOMIALSPIRAL" }, attrs);
                    }
                    case "cosine": return f.Add("IFCCOSINESPIRAL", Origin(), X(c.Trig), T(c.Constant));
                    case "sine": return f.Add("IFCSINESPIRAL", Origin(), X(c.Trig), T(c.Linear), T(c.Constant));
                    default: return f.Add("IFCLINE", f.Add("IFCCARTESIANPOINT", "(0.,0.)"), f.Add("IFCVECTOR", f.Add("IFCDIRECTION", "(1.,0.)"), R(1)));
                }
            }

            // A real at full precision, in STEP's form (a decimal point in the mantissa, an exponent when short).
            static string X(double v)
            {
                if (!double.IsFinite(v)) v = 0;
                var t = v.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
                var e = t.IndexOf('E');
                var mantissa = e < 0 ? t : t[..e];
                if (!mantissa.Contains('.')) mantissa += ".";
                return e < 0 ? mantissa : mantissa + t[e..];
            }
            static double EndHeight(IfcVerticalSegment v) => v.Type == "PARABOLICARC" ? v.StartHeight + (v.StartGradient + v.EndGradient) / 2 * v.HorizontalLength : v.StartHeight + v.StartGradient * v.HorizontalLength;
            static Vec2 EndOf(IReadOnlyList<Vec2> plan, IfcHorizontalSegment last) => plan.Count > 0 ? plan[^1] : last.Start;
        }

        // ─── The relationships gathered on the way ──────────────────────────

        /// <summary>
        /// The distribution network the modules declare (IIfcNetwork): each
        /// element's ports nested in it, placed where they are and facing
        /// their way; the connections between ports; the systems grouping
        /// their elements and serving the building.
        /// </summary>
        void Network()
        {
            var nets = s.Catalog.All<IIfcNetwork>();
            if (nets.Count == 0) return;
            var portRef = new Dictionary<(string Element, string Port), string>();
            foreach (var (id, entity) in written.ToList())
            {
                if (m.Get(id) is not { } e || !placeOf.TryGetValue(id, out var at)) continue;
                var ports = new List<string>();
                foreach (var net in nets)
                    foreach (var p in net.PortsOf(m, e))
                    {
                        if (portRef.ContainsKey((id, p.Key))) continue;
                        var dir = p.Dir.Length > 1e-9 ? p.Dir.Normalized() : new Vec3(0, 0, 1);
                        var side = Vec3.Cross(new Vec3(0, 0, 1), dir);
                        var refDir = side.Length > 1e-6 ? side.Normalized() : new Vec3(1, 0, 0);
                        var place = f.Add("IFCLOCALPLACEMENT", at.Place, f.Add("IFCAXIS2PLACEMENT3D",
                            f.Add("IFCCARTESIANPOINT", L(new[] { R(p.At.X), R(p.At.Y), R(p.At.Z - at.Z0) })),
                            f.Add("IFCDIRECTION", L(new[] { R(dir.X), R(dir.Y), R(dir.Z) })),
                            f.Add("IFCDIRECTION", L(new[] { R(refDir.X), R(refDir.Y), R(refDir.Z) }))));
                        var port = f.Add("IFCDISTRIBUTIONPORT", Gid(id, "port/" + p.Key), Null, S(p.Key), Null, Null, place, Null,
                            E(p.Flow is "SOURCE" or "SINK" or "SOURCEANDSINK" ? p.Flow : "NOTDEFINED"), E(p.Kind.Length > 0 ? p.Kind : "NOTDEFINED"), E(p.SystemType.Length > 0 ? p.SystemType : "NOTDEFINED"));
                        portRef[(id, p.Key)] = port;
                        ports.Add(port);
                    }
                if (ports.Count > 0) f.Add("IFCRELNESTS", Gid(id, "ports"), Null, Null, Null, entity, L(ports));
            }
            foreach (var net in nets)
            {
                foreach (var c in net.Connections(m))
                    if (portRef.TryGetValue((c.ElementA, c.PortA), out var a) && portRef.TryGetValue((c.ElementB, c.PortB), out var b))
                        f.Add("IFCRELCONNECTSPORTS", Gid(c.ElementA, $"connects/{c.PortA}/{c.ElementB}/{c.PortB}"), Null, Null, Null, a, b, Null);
                foreach (var sys in net.Systems(m))
                {
                    var members = sys.Members.Where(written.ContainsKey).Select(x => written[x]).ToList();
                    var system = f.Add("IFCDISTRIBUTIONSYSTEM", Gid(sys.Id), Null, S(sys.Name), Null, Null, Null, E(sys.Predefined.Length > 0 ? sys.Predefined : "NOTDEFINED"));
                    if (members.Count > 0) f.Add("IFCRELASSIGNSTOGROUP", Gid(sys.Id, "members"), Null, Null, Null, L(members), Null, system);
                    f.Add("IFCRELSERVICESBUILDINGS", Gid(sys.Id, "serves"), Null, Null, Null, system, L(new[] { buildingRef }));
                }
            }
        }

        void Finish()
        {
            // A module's systems of elements (a steel hall, decision 270): an IfcBuildingSystem assigned the members written, serving the building.
            foreach (var src in s.Catalog.All<IIfcSystems>())
                foreach (var sys in src.Systems(m))
                {
                    var members = sys.Members.Where(written.ContainsKey).Select(x => written[x]).ToList();
                    if (members.Count == 0) continue;
                    var system = f.Add("IFCBUILDINGSYSTEM", Gid(sys.Id), Null, S(sys.Name), Null, Null, E(sys.Predefined), Null);
                    f.Add("IFCRELASSIGNSTOGROUP", Gid(sys.Id, "members"), Null, Null, Null, L(members), Null, system);
                    f.Add("IFCRELSERVICESBUILDINGS", Gid(sys.Id, "serves"), Null, Null, Null, system, L(new[] { buildingRef }));
                    if (sys.Properties.Count > 0)
                        Define(system, f.Add("IFCPROPERTYSET", Gid(sys.Id, "pset"), Null, S(sys.PropertySet), Null, L(sys.Properties.Select(x => Single(x.Name, Typed("IFCLABEL", S(x.Value)))).ToArray())), Gid(sys.Id, "pset-rel"));
                }
            // Connections an element realizes (a steel connection, decision 273): between the elements it joins, as it is made.
            foreach (var src in s.Catalog.All<IIfcConnections>())
                foreach (var c in src.Connections(m))
                    if (written.TryGetValue(c.Realizing, out var by) && written.TryGetValue(c.Relating, out var a) && written.TryGetValue(c.Related, out var b))
                        f.Add("IFCRELCONNECTSWITHREALIZINGELEMENTS", Gid(c.Realizing, $"connects/{c.Relating}/{c.Related}"), Null, S(c.Type), Null, Null, a, b, L(new[] { by }), S(c.Type));
            // Lapin's groups (docs/groups.md) as IFC's: an IfcGroup assigned the members written.
            foreach (var g in Groups.All(m))
            {
                // A module's own group stays the project's (its members are written as any element).
                if (filters.Any(x => x.SkipsGroup(m, g))) continue;
                var members = Groups.MembersOf(m, g.Id).Where(written.ContainsKey).Select(x => written[x]).ToList();
                if (members.Count == 0) continue;
                var group = f.Add("IFCGROUP", Gid(g.Id), Null, S(g.Name), Null, Null);
                f.Add("IFCRELASSIGNSTOGROUP", Gid(g.Id, "members"), Null, Null, Null, L(members), Null, group);
                // An instance of a component says of which, and where it stands (docs/plan-units.md); an apartment type's as Lapin_Unit.
                if (Components.InstanceOf(m, g) is { } unit && (unit.Unit ? UnitSetName : ComponentSetName) is var setName)
                    Define(group, f.Add("IFCPROPERTYSET", Gid(g.Id, setName), Null, S(setName), Null, L(new[]
                    {
                        Single("Type", Typed("IFCLABEL", S(unit.Type.Name))),
                        Single("TypeId", Typed("IFCIDENTIFIER", S(unit.Type.Id))),
                        Single("X", Typed("IFCLENGTHMEASURE", R(unit.X))),
                        Single("Y", Typed("IFCLENGTHMEASURE", R(unit.Y))),
                        Single("Z", Typed("IFCLENGTHMEASURE", R(unit.Z))),
                        Single("Degrees", Typed("IFCREAL", R(unit.Degrees))),
                        Single("Mirrored", Typed("IFCBOOLEAN", unit.Mirrored ? ".T." : ".F.")),
                    })), Gid(g.Id, "unit-rel"));
            }
            foreach (var (typeId, t) in types) f.Add("IFCRELDEFINESBYTYPE", Gid(typeId, "defines"), Null, Null, Null, L(t.Occurrences), t.Ref);
            foreach (var (materialId, list) in materialUses) f.Add("IFCRELASSOCIATESMATERIAL", Gid(materialId, "associates"), Null, Null, Null, L(list), Material(materialId));
            foreach (var (set, gid, users) in constituentSets.Values) f.Add("IFCRELASSOCIATESMATERIAL", gid, Null, Null, Null, L(users), set);
            Classified();
            foreach (var fill in pendingFills)
                if (written.TryGetValue(fill.Relating, out var opening) && fill.Related.Count > 0 && written.TryGetValue(fill.Related[0], out var filler))
                    f.Add("IFCRELFILLSELEMENT", Gid(fill.Id), Null, Null, Null, opening, filler);
            foreach (var (gid, opening, fillerId) in pendingKeptFills)
                if (written.TryGetValue(fillerId, out var filler)) f.Add("IFCRELFILLSELEMENT", gid, Null, Null, Null, opening, filler);
            // Joins: the element that runs through relates the other; the ends they meet at, the style as the description.
            foreach (var rel in m.Relations.Where(r => r.Class == IfcClass.RelConnectsPathElements))
                if (rel.Related.Count > 0 && written.TryGetValue(rel.Relating, out var a) && written.TryGetValue(rel.Related[0], out var b))
                    f.Add("IFCRELCONNECTSPATHELEMENTS", Gid(rel.Id), Null, S((string?)rel.Attrs?["kind"]), S(Joins.Stops(rel) ? Joins.StopsOn : (string?)rel.Attrs?["style"]), Null, a, b, "()", "()",
                        E(Connection((string?)rel.Attrs?["relatedConnection"])), E(Connection((string?)rel.Attrs?["relatingConnection"])));
            // Temporary works: the product resource they are (formwork, a scaffold), assigned to them; formwork connected to the element it forms.
            foreach (var e in m.Entities)
            {
                if (!written.TryGetValue(e.Id, out var element)) continue;
                if (e.Str("resource") is { Length: > 0 } res)
                {
                    var known = res is "FORMWORK" or "ASSEMBLY";
                    var resource = f.Add("IFCCONSTRUCTIONPRODUCTRESOURCE", Gid(e.Id, "resource"), Null, S(e.Name), Null, S(known ? null : res), Null, Null, Null, Null, Null, E(known ? res : "USERDEFINED"));
                    f.Add("IFCRELASSIGNSTOPRODUCT", Gid(e.Id, "resource-rel"), Null, Null, Null, L(new[] { resource }), Null, element);
                }
                if (e.Str("host") is { } host && written.TryGetValue(host, out var h))
                    f.Add("IFCRELCONNECTSELEMENTS", Gid(e.Id, "host-rel"), Null, S(e.Str("hostRelation") ?? "Formwork"), Null, Null, h, element);
                // An element standing by another it names (a kerb by its hardscape, site grading T4): connected to it, the named one relating.
                foreach (var c in Connections.Where(c => c.Class == e.Class))
                    if (e.Str(c.Key) is { } other && written.TryGetValue(other, out var by))
                        f.Add("IFCRELCONNECTSELEMENTS", Gid(e.Id, c.Key + "-rel"), Null, S(c.Name), Null, Null, by, element);
            }
            // Coverings on an element's faces (the finish painter, forum topic 83): one IfcRelCoversBldgElements per element covered.
            foreach (var g in m.Entities.Where(e => e.Str("covers") is { } c && written.ContainsKey(e.Id) && written.ContainsKey(c)).GroupBy(e => e.Str("covers")!).OrderBy(g => g.Key, StringComparer.Ordinal))
                f.Add("IFCRELCOVERSBLDGELEMENTS", Gid(g.Key, "covers"), Null, Null, Null, written[g.Key], L(g.Select(e => written[e.Id])));
            // A platform voiding the terrain: IFC 4.3 has it as a feature; in IFC4 it is a proxy and only stands there.
            if (o.Ifc43)
                foreach (var (rel, host, v) in pendingVoids)
                    if (m.Get(v)?.Class == IfcClass.EarthworksCut && written.TryGetValue(host, out var h) && written.TryGetValue(v, out var cut))
                        f.Add("IFCRELVOIDSELEMENT", Gid(rel.Id), Null, Null, Null, h, cut);
            // A spatial element whose every element went elsewhere (into an assembly, a part): no relationship, IFC wants one or more.
            foreach (var (spatial, list) in contained.Where(c => c.Value.Count > 0))
                f.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", SpatialGid(refIdOf[spatial], IfcClass.RelContainedInSpatialStructure, null, refIdOf[spatial], "contains"), Null, Null, Null, L(list), spatial);
            // Rooms are parts of their storey: aggregated, as IFC has spaces.
            foreach (var (spatial, list) in spaces)
                f.Add("IFCRELAGGREGATES", Gid(refIdOf.GetValueOrDefault(spatial) ?? project.Id, "aggregates/spaces"), Null, Null, Null, spatial, L(list));
        }

        // ─── Processes: the work schedule (docs/plan-4d5d.md) ─────────────────

        static string IfcDateTime(DateTime d) => S(d.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        static string IfcDate(DateOnly d) => S(d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        static string IfcTime(TimeOnly t) => S(t.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>
        /// A number of working days as an IfcDuration (ISO 8601: P5D, P0D; a negative float -P2D; a part of a day in the
        /// working hours of a day of <paramref name="hoursPerDay"/>, the calendar's, as the import reads it back).
        /// </summary>
        internal static string IfcDuration(double days, double hoursPerDay = 8)
        {
            if (!(hoursPerDay > 0) || !double.IsFinite(hoursPerDay)) hoursPerDay = 8;
            if (!double.IsFinite(days)) days = 0;
            var sign = days < 0 ? "-" : "";
            days = Math.Abs(days);
            var whole = Math.Floor(days);
            var hours = Math.Round((days - whole) * hoursPerDay, 3);
            if (hours >= hoursPerDay) { whole++; hours = 0; }
            var text = hours > 0 ? $"P{whole:0}DT{hours.ToString(System.Globalization.CultureInfo.InvariantCulture)}H" : $"P{whole:0}D";
            return S(sign + text);
        }

        /// <summary>
        /// The modules' work schedules (IIfcProcesses): each an IfcWorkSchedule declared by the project, its calendar an
        /// IfcWorkCalendar (the weekdays and hours a WEEKLY recurrence, each day not worked an exception) controlling the schedule
        /// and its tasks, the IfcTasks with their IfcTaskTime nested in one another (IfcRelNests) and the top ones controlled by the
        /// schedule (IfcRelAssignsToControl), the IfcRelSequences with their IfcLagTime, each task's own fields a property set.
        /// </summary>
        void Processes()
        {
            foreach (var src in s.Catalog.All<IIfcProcesses>())
                foreach (var ws in src.Schedules(s.Reader))
                {
                    var schedule = f.Add("IFCWORKSCHEDULE", Gid(ws.Id), Null, S(ws.Name), Null, Null, Null, IfcDateTime(DateTime.UtcNow), Null, Null, Null, Null,
                        IfcDateTime(ws.Start), ws.Finish is { } fin ? IfcDateTime(fin) : Null, E(ws.Predefined));
                    var declared = new List<string> { schedule };
                    string? calendar = null;
                    if (ws.Calendar is { } c)
                    {
                        var period = f.Add("IFCTIMEPERIOD", IfcTime(c.DayStart), IfcTime(c.DayEnd));
                        var pattern = f.Add("IFCRECURRENCEPATTERN", E("WEEKLY"), Null, L(c.Weekdays.Select(d => I(d))), Null, Null, Null, Null, L(new[] { period }));
                        var working = f.Add("IFCWORKTIME", S("Working days"), Null, Null, pattern, Null, Null);
                        var exceptions = c.Exceptions.Select(x => f.Add("IFCWORKTIME", S(x.Name), Null, Null, Null, IfcDate(x.From), IfcDate(x.To))).ToList();
                        calendar = f.Add("IFCWORKCALENDAR", Gid(c.Id), Null, S(c.Name), Null, Null, Null, L(new[] { working }), exceptions.Count > 0 ? L(exceptions) : Null, E("FIRSTSHIFT"));
                        declared.Add(calendar);
                        if (c.Properties.Count > 0)
                            Define(calendar, f.Add("IFCPROPERTYSET", Gid(c.Id, "pset"), Null, S(c.PropertySet), Null, L(c.Properties.Select(x => Single(x.Name, Typed("IFCLABEL", S(x.Value)))).ToArray())), Gid(c.Id, "pset-rel"));
                    }
                    f.Add("IFCRELDECLARES", Gid(ws.Id, "declares"), Null, Null, Null, projectRef, L(declared));
                    var taskRef = new Dictionary<string, string>();
                    // A day's working hours, the calendar's (8 without one): the import reads a duration's hours by them.
                    var hoursPerDay = ws.Calendar is { } wc && (wc.DayEnd - wc.DayStart).TotalHours is var dh && dh > 0 ? dh : 8;
                    foreach (var t in ws.Tasks)
                    {
                        string time = Null;
                        if (t.Time is { } tt)
                        {
                            string Dt(DateTime? d) => d is { } x ? IfcDateTime(x) : Null;
                            string Dur(double? d) => d is { } x ? IfcDuration(x, hoursPerDay) : Null;
                            time = f.Add("IFCTASKTIME", Null, Null, Null, E("WORKTIME"), Dur(tt.DurationDays), Dt(tt.ScheduleStart), Dt(tt.ScheduleFinish),
                                Dt(tt.ScheduleStart), Dt(tt.ScheduleFinish), Dt(tt.LateStart), Dt(tt.LateFinish), Dur(tt.FreeFloatDays), Dur(tt.TotalFloatDays),
                                tt.IsCritical is { } crit ? (crit ? Step.True : Step.False) : Null, Dt(tt.StatusTime), Null, Dt(tt.ActualStart), Dt(tt.ActualFinish),
                                Dur(tt.RemainingDays), tt.Completion is { } pc && pc > 0 ? R(pc) : Null);
                        }
                        var kind = t.Predefined is { Length: > 0 } k ? k : "NOTDEFINED";
                        var task = f.Add("IFCTASK", Gid(t.Id), Null, S(t.Name), S(t.Description), Null, S(t.Identification), Null, Null, Null,
                            t.IsMilestone ? Step.True : Step.False, Null, time, E(kind));
                        taskRef[t.Id] = task;
                        tasksWritten[t.Id] = task;
                        if (t.Properties.Count > 0)
                            Define(task, f.Add("IFCPROPERTYSET", Gid(t.Id, "pset"), Null, S(t.PropertySet), Null, L(t.Properties.Select(x => Single(x.Name, Typed("IFCLABEL", S(x.Value)))).ToArray())), Gid(t.Id, "pset-rel"));
                    }
                    var top = ws.Tasks.Where(t => t.Parent == null || !taskRef.ContainsKey(t.Parent)).Select(t => taskRef[t.Id]).ToList();
                    if (top.Count > 0) f.Add("IFCRELASSIGNSTOCONTROL", Gid(ws.Id, "controls"), Null, Null, Null, L(top), Null, schedule);
                    foreach (var g in ws.Tasks.Where(t => t.Parent != null && taskRef.ContainsKey(t.Parent)).GroupBy(t => t.Parent!))
                        f.Add("IFCRELNESTS", Gid(g.Key, "nests"), Null, Null, Null, taskRef[g.Key], L(g.Select(t => taskRef[t.Id])));
                    if (calendar != null)
                        f.Add("IFCRELASSIGNSTOCONTROL", Gid(ws.Calendar!.Id, "controls"), Null, Null, Null, L(taskRef.Values.Prepend(schedule)), Null, calendar);
                    foreach (var q in ws.Sequences)
                    {
                        if (!taskRef.TryGetValue(q.Relating, out var a) || !taskRef.TryGetValue(q.Related, out var b)) continue;
                        var lag = f.Add("IFCLAGTIME", Null, Null, Null, Typed("IFCDURATION", IfcDuration(q.LagDays, hoursPerDay)), E("WORKTIME"));
                        f.Add("IFCRELSEQUENCE", Gid(q.Id), Null, Null, Null, a, b, lag, E(q.Type), Null);
                    }
                    // The elements: what a task builds is its output (one IfcRelAssignsToProduct an element, every task building it
                    // among its related objects); what it acts on (a demolition) its input (one IfcRelAssignsToProcess a task).
                    var builtBy = new Dictionary<string, List<string>>();
                    foreach (var t in ws.Tasks)
                        foreach (var id in t.Outputs ?? [])
                            if (written.ContainsKey(id)) (builtBy.TryGetValue(id, out var l) ? l : builtBy[id] = []).Add(taskRef[t.Id]);
                    foreach (var (id, tasks) in builtBy)
                        f.Add("IFCRELASSIGNSTOPRODUCT", Gid(id, ws.Id + "/built-by"), Null, Null, Null, L(tasks), Null, written[id]);
                    foreach (var t in ws.Tasks)
                        if (t.Inputs?.Where(written.ContainsKey).Select(id => written[id]).ToList() is { Count: > 0 } inputs)
                            f.Add("IFCRELASSIGNSTOPROCESS", Gid(t.Id, "inputs"), Null, Null, Null, L(inputs), Null, taskRef[t.Id], Null);
                }
        }

        // ─── Controls: the estimate (docs/plan-4d5d.md, stage 6) ──────────────

        static string QuantityEntity(string kind) => kind switch
        {
            "VOLUME" => "IFCQUANTITYVOLUME", "AREA" => "IFCQUANTITYAREA", "LENGTH" => "IFCQUANTITYLENGTH", "WEIGHT" => "IFCQUANTITYWEIGHT", _ => "IFCQUANTITYCOUNT",
        };

        static string MeasureOf(string kind) => kind switch
        {
            "VOLUME" => "IFCVOLUMEMEASURE", "AREA" => "IFCAREAMEASURE", "LENGTH" => "IFCLENGTHMEASURE", "WEIGHT" => "IFCMASSMEASURE", _ => "IFCCOUNTMEASURE",
        };

        /// <summary>
        /// The modules' cost schedules (IIfcControls): each an IfcCostSchedule declared by the project, its chapters and articles
        /// IfcCostItems nested in one another (IfcRelNests), the top ones controlled by the schedule (IfcRelAssignsToControl);
        /// each article's quantity an IfcPhysicalQuantity, its unit rate one IfcCostValue a category (Material, Labour, Equipment,
        /// Transport) per unit of it (UnitBasis), the elements it measures controlled by it (IfcRelAssignsToControl); the
        /// schedule's and each item's own fields a property set.
        /// </summary>
        void Controls()
        {
            var units = new Dictionary<string, string>();
            string Unit(string kind) => units.TryGetValue(kind, out var u) ? u : units[kind] = kind switch
            {
                "VOLUME" => f.Add("IFCSIUNIT", Derived, E("VOLUMEUNIT"), Null, E("CUBIC_METRE")),
                "AREA" => f.Add("IFCSIUNIT", Derived, E("AREAUNIT"), Null, E("SQUARE_METRE")),
                "LENGTH" => metre,
                "WEIGHT" => f.Add("IFCSIUNIT", Derived, E("MASSUNIT"), E("KILO"), E("GRAM")),
                _ => f.Add("IFCCONTEXTDEPENDENTUNIT", Derived, E("USERDEFINED"), S("pcs")),
            };
            foreach (var src in s.Catalog.All<IIfcControls>())
                foreach (var cs in src.CostSchedules(s.Reader))
                {
                    var schedule = f.Add("IFCCOSTSCHEDULE", Gid(cs.Id), Null, S(cs.Name), Null, Null, Null, E(cs.Predefined), Null, Null, IfcDateTime(DateTime.UtcNow));
                    f.Add("IFCRELDECLARES", Gid(cs.Id, "declares"), Null, Null, Null, projectRef, L(new[] { schedule }));
                    if (cs.Properties.Count > 0)
                        Define(schedule, f.Add("IFCPROPERTYSET", Gid(cs.Id, "pset"), Null, S(cs.PropertySet), Null, L(cs.Properties.Select(x => Single(x.Name, Typed("IFCLABEL", S(x.Value)))).ToArray())), Gid(cs.Id, "pset-rel"));
                    var itemRef = new Dictionary<string, string>();
                    foreach (var it in cs.Items)
                    {
                        var kind = it.QuantityKind ?? "COUNT";
                        var values = it.Values.Select(v => f.Add("IFCCOSTVALUE", Null, Null, Typed("IFCMONETARYMEASURE", R(v.UnitValue)),
                            it.Quantity != null ? f.Add("IFCMEASUREWITHUNIT", Typed(MeasureOf(kind), R(1)), Unit(kind)) : Null, Null, Null, S(v.Category), Null, Null, Null)).ToList();
                        var quantities = it.Quantity is { } q
                            ? L(new[] { f.Add(QuantityEntity(kind), S("Quantity"), Null, Null, R(q), Null) })
                            : Null;
                        var item = f.Add("IFCCOSTITEM", Gid(it.Id), Null, S(it.Name), Null, Null, S(it.Identification), E("NOTDEFINED"), values.Count > 0 ? L(values) : Null, quantities);
                        itemRef[it.Id] = item;
                        if (it.Properties.Count > 0)
                            Define(item, f.Add("IFCPROPERTYSET", Gid(it.Id, "pset"), Null, S(it.PropertySet), Null, L(it.Properties.Select(x => Single(x.Name, Typed("IFCLABEL", S(x.Value)))).ToArray())), Gid(it.Id, "pset-rel"));
                        if (it.Elements.Where(written.ContainsKey).Select(id => written[id]).Distinct().ToList() is { Count: > 0 } measured)
                            f.Add("IFCRELASSIGNSTOCONTROL", Gid(it.Id, "controls"), Null, Null, Null, L(measured), Null, item);
                        // The tasks its work is done in (stage 7): controlled by it too, in a relationship of their own (RelatedObjectsType PROCESS).
                        if ((it.Tasks ?? []).Where(tasksWritten.ContainsKey).Select(id => tasksWritten[id]).Distinct().ToList() is { Count: > 0 } tasks)
                            f.Add("IFCRELASSIGNSTOCONTROL", Gid(it.Id, "controls-tasks"), Null, Null, Null, L(tasks), E("PROCESS"), item);
                    }
                    var top = cs.Items.Where(i => i.Parent == null || !itemRef.ContainsKey(i.Parent)).Select(i => itemRef[i.Id]).ToList();
                    if (top.Count > 0) f.Add("IFCRELASSIGNSTOCONTROL", Gid(cs.Id, "controls"), Null, Null, Null, L(top), Null, schedule);
                    foreach (var g in cs.Items.Where(i => i.Parent != null && itemRef.ContainsKey(i.Parent)).GroupBy(i => i.Parent!))
                        f.Add("IFCRELNESTS", Gid(g.Key, "nests"), Null, Null, Null, itemRef[g.Key], L(g.Select(i => itemRef[i.Id])));
                }
        }

        static string Connection(string? value)
        {
            var v = (value ?? "").ToUpperInvariant();
            return v.Contains("START") ? "ATSTART" : v.Contains("END") ? "ATEND" : v.Contains("PATH") ? "ATPATH" : "NOTDEFINED";
        }
    }
}
