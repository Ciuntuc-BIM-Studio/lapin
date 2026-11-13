namespace Lapin.Platform;

/// <summary>
/// The IFC classes an element made from an external body may take (docs/plan-external-geometry.md): the instantiable
/// subtypes of IfcElement in IFC4 (ADD2 TC1) and IFC 4.3 (ADD2), after the schemas' public specifications, without the
/// feature elements (openings, projections, surface features: not bodies of their own). Matched without regard to case;
/// the schema's spelling is what is kept.
/// </summary>
public static class IfcElementClasses
{
    static readonly string[] Names =
    [
        // Built elements (IfcBuildingElement in IFC4, IfcBuiltElement in 4.3).
        "IfcBeam", "IfcBeamStandardCase", "IfcBearing", "IfcBuildingElementProxy", "IfcBuiltElement", "IfcChimney", "IfcColumn", "IfcColumnStandardCase",
        "IfcCourse", "IfcCovering", "IfcCurtainWall", "IfcDeepFoundation", "IfcCaissonFoundation", "IfcDoor", "IfcDoorStandardCase", "IfcEarthworksElement",
        "IfcEarthworksFill", "IfcFooting", "IfcKerb", "IfcMember", "IfcMemberStandardCase", "IfcMooringDevice", "IfcNavigationElement", "IfcPavement",
        "IfcPile", "IfcPlate", "IfcPlateStandardCase", "IfcRail", "IfcRailing", "IfcRamp", "IfcRampFlight", "IfcReinforcedSoil", "IfcRoof",
        "IfcShadingDevice", "IfcSlab", "IfcSlabElementedCase", "IfcSlabStandardCase", "IfcStair", "IfcStairFlight", "IfcTrackElement", "IfcWall",
        "IfcWallElementedCase", "IfcWallStandardCase", "IfcWindow", "IfcWindowStandardCase",
        // Civil, geographic, geotechnical, transport, furnishing, virtual, assemblies.
        "IfcCivilElement", "IfcGeographicElement", "IfcGeotechnicalElement", "IfcGeotechnicalStratum", "IfcBorehole", "IfcGeomodel", "IfcGeoslice",
        "IfcSolidStratum", "IfcVoidStratum", "IfcWaterStratum", "IfcTransportElement", "IfcTransportationDevice", "IfcVehicle",
        "IfcFurnishingElement", "IfcFurniture", "IfcSystemFurnitureElement", "IfcVirtualElement", "IfcElementAssembly",
        // Element components.
        "IfcBuildingElementPart", "IfcDiscreteAccessory", "IfcFastener", "IfcMechanicalFastener", "IfcReinforcingBar", "IfcReinforcingMesh",
        "IfcTendon", "IfcTendonAnchor", "IfcTendonConduit", "IfcVibrationIsolator", "IfcVibrationDamper", "IfcImpactProtectionDevice", "IfcSign",
        // Distribution elements.
        "IfcDistributionElement", "IfcDistributionControlElement", "IfcActuator", "IfcAlarm", "IfcController", "IfcFlowInstrument",
        "IfcProtectiveDeviceTrippingUnit", "IfcSensor", "IfcUnitaryControlElement", "IfcDistributionFlowElement", "IfcDistributionChamberElement",
        "IfcEnergyConversionDevice", "IfcAirToAirHeatRecovery", "IfcBoiler", "IfcBurner", "IfcChiller", "IfcCoil", "IfcCondenser", "IfcCooledBeam",
        "IfcCoolingTower", "IfcElectricGenerator", "IfcElectricMotor", "IfcEngine", "IfcEvaporativeCooler", "IfcEvaporator", "IfcHeatExchanger",
        "IfcHumidifier", "IfcMotorConnection", "IfcSolarDevice", "IfcTransformer", "IfcTubeBundle", "IfcUnitaryEquipment",
        "IfcFlowController", "IfcAirTerminalBox", "IfcDamper", "IfcElectricDistributionBoard", "IfcElectricTimeControl", "IfcFlowMeter",
        "IfcProtectiveDevice", "IfcSwitchingDevice", "IfcValve",
        "IfcFlowFitting", "IfcCableCarrierFitting", "IfcCableFitting", "IfcDuctFitting", "IfcJunctionBox", "IfcPipeFitting",
        "IfcFlowMovingDevice", "IfcCompressor", "IfcFan", "IfcPump",
        "IfcFlowSegment", "IfcCableCarrierSegment", "IfcCableSegment", "IfcConveyorSegment", "IfcDuctSegment", "IfcPipeSegment",
        "IfcFlowStorageDevice", "IfcElectricFlowStorageDevice", "IfcTank", "IfcElectricFlowTreatmentDevice",
        "IfcFlowTerminal", "IfcAirTerminal", "IfcAudioVisualAppliance", "IfcCommunicationsAppliance", "IfcElectricAppliance", "IfcFireSuppressionTerminal",
        "IfcLamp", "IfcLightFixture", "IfcLiquidTerminal", "IfcMedicalDevice", "IfcMobileTelecommunicationsAppliance", "IfcOutlet", "IfcSanitaryTerminal",
        "IfcSignal", "IfcSpaceHeater", "IfcStackTerminal", "IfcWasteTerminal",
        "IfcFlowTreatmentDevice", "IfcDuctSilencer", "IfcFilter", "IfcInterceptor",
    ];

    static readonly Dictionary<string, string> ByUpper = Names.Distinct().ToDictionary(n => n.ToUpperInvariant(), n => n);

    /// <summary>
    /// The schema's spelling of an IfcElement subtype (any case given), or null when it is none. IFC4's standard and elemented
    /// cases (IfcWallStandardCase) are their class (IfcWall): IFC 4.3 has no such classes, and a body is no case of either.
    /// </summary>
    public static string? Resolve(string? name)
    {
        if (name is not { Length: > 3 } || !ByUpper.TryGetValue(name.Trim().ToUpperInvariant(), out var n)) return null;
        foreach (var suffix in (string[])["StandardCase", "ElementedCase"])
            if (n.EndsWith(suffix, StringComparison.Ordinal)) return n[..^suffix.Length];
        return n;
    }

    public static IReadOnlyCollection<string> All => ByUpper.Values;
}
