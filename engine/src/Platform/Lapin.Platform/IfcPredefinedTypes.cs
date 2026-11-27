using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The predefined types IFC gives its element classes, after the schemas' public specifications: what the IFC export writes a class's
/// PredefinedType from (a type the class lacks goes USERDEFINED, named by the element's object type), and what a script's element
/// (<c>element.createBody</c>) is checked against. The tables were the IFC export's own; they live here so the platform can ask them too.
/// </summary>
public static class IfcPredefinedTypes
{
    /// <summary>The predefined types IFC4 knows per class (USERDEFINED and NOTDEFINED always); null: the class has no predefined type.</summary>
    public static readonly IReadOnlyDictionary<string, string[]?> Ifc4 = new Dictionary<string, string[]?>()
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

    /// <summary>What IFC 4.3 adds: the railway's classes, and predefined types IFC4 lacks (a platform slab, a track panel).</summary>
    public static readonly IReadOnlyDictionary<string, string[]?> Ifc43 = new Dictionary<string, string[]?>()
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
        // A bridge's elements (IFC 4.3 ADD2's enumerations): a pier's stem and cap, a deck's edge beams and diaphragms, cables, bearings.
        [IfcClass.Beam] = ["BEAM", "CORNICE", "DIAPHRAGM", "EDGEBEAM", "GIRDER_SEGMENT", "HATSTONE", "HOLLOWCORE", "JOIST", "LINTEL", "PIERCAP", "SPANDREL", "T_BEAM"],
        [IfcClass.Column] = ["COLUMN", "PIERSTEM", "PIERSTEM_SEGMENT", "PILASTER", "STANDCOLUMN"],
        ["IfcMember"] = ["ARCH_SEGMENT", "BRACE", "CHORD", "COLLAR", "MEMBER", "MULLION", "PLATE", "POST", "PURLIN", "RAFTER", "STAY_CABLE", "STIFFENING_RIB", "STRINGER", "STRUCTURALCABLE", "STRUT", "STUD", "SUSPENDER", "SUSPENSION_CABLE", "TIEBAR"],
        ["IfcPlate"] = ["BASE_PLATE", "COVER_PLATE", "CURTAIN_PANEL", "FLANGE_PLATE", "GUSSET_PLATE", "SHEET", "SPLICE_PLATE", "STIFFENER_PLATE", "WEB_PLATE"],
        [IfcClass.Wall] = ["ELEMENTEDWALL", "MOVABLE", "PARAPET", "PARTITIONING", "PLUMBINGWALL", "POLYGONAL", "RETAININGWALL", "SHEAR", "SOLIDWALL", "STANDARD", "WAVEWALL"],
        ["IfcRailing"] = ["BALUSTRADE", "FENCE", "GUARDRAIL", "HANDRAIL"],
        ["IfcBearing"] = ["CYLINDRICAL", "DISK", "ELASTOMERIC", "GUIDE", "POT", "ROCKER", "ROLLER", "SPHERICAL"],
    };

    /// <summary>The element assembly in IFC4: its own enumeration, without the railway's panels.</summary>
    public static readonly string[] Assembly4 = ["ACCESSORY_ASSEMBLY", "ARCH", "BEAM_GRID", "BRACED_FRAME", "GIRDER", "REINFORCEMENT_UNIT", "RIGID_FRAME", "SLAB_FIELD", "TRUSS"];

    /// <summary>IFC 4.3's IfcBridgePartTypeEnum (without USERDEFINED and NOTDEFINED).</summary>
    public static readonly IReadOnlySet<string> BridgeParts = new HashSet<string>(["ABUTMENT", "DECK", "DECK_SEGMENT", "FOUNDATION", "PIER", "PIER_SEGMENT", "PYLON", "SUBSTRUCTURE", "SUPERSTRUCTURE", "SURFACESTRUCTURE"]);

    /// <summary>
    /// The predefined types a class has in IFC4 or IFC 4.3 (null: the class has a table in neither; empty: one with only
    /// USERDEFINED and NOTDEFINED), and whether the class has a PredefinedType at all (false: it has none, IfcFurnishingElement's way).
    /// </summary>
    public static (IReadOnlySet<string>? Types, bool HasPredefined) Of(string cls)
    {
        var in4 = Ifc4.TryGetValue(cls, out var a4);
        var in43 = Ifc43.TryGetValue(cls, out var a43);
        if (!in4 && !in43) return (null, true);
        if (in4 && a4 == null && (!in43 || a43 == null)) return (new HashSet<string>(), false);
        return (new HashSet<string>((a4 ?? []).Concat(a43 ?? [])), true);
    }
}
