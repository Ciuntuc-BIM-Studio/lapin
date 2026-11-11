using System.Collections.Concurrent;

namespace Lapin.Model;

/// <summary>
/// The part of IFC's class tree Lapin asks about by kind rather than by name: the building services, every subtype of
/// <c>IfcDistributionElement</c> (IFC2X3, IFC4 ADD2 TC1 and IFC4X3), so a schedule's Installations step or an estimate's
/// chapter takes a ventilation unit, a damper or a sensor without a list that has to name them all. A module's own class
/// that is one of them (Lapin's exhaust fan, written as an IfcFan) is told by <see cref="Subtype"/> when it registers.
/// The building elements' tree (IfcWall over IfcWallStandardCase) is not here: their rules name their classes.
/// </summary>
public static class IfcSchema
{
    public const string DistributionElement = "IfcDistributionElement";
    public const string DistributionFlowElement = "IfcDistributionFlowElement";
    public const string DistributionControlElement = "IfcDistributionControlElement";

    // Each class's supertype, the schema's names (an IFC2X3 file may still have the generic flow classes as its elements).
    static readonly Dictionary<string, string> Schema = Build(
        (DistributionElement, [DistributionFlowElement, DistributionControlElement]),
        (DistributionFlowElement,
        [
            "IfcDistributionChamberElement", "IfcEnergyConversionDevice", "IfcFlowController", "IfcFlowFitting", "IfcFlowMovingDevice",
            "IfcFlowSegment", "IfcFlowStorageDevice", "IfcFlowTerminal", "IfcFlowTreatmentDevice",
        ]),
        ("IfcEnergyConversionDevice",
        [
            "IfcAirToAirHeatRecovery", "IfcBoiler", "IfcBurner", "IfcChiller", "IfcCoil", "IfcCondenser", "IfcCooledBeam", "IfcCoolingTower",
            "IfcElectricGenerator", "IfcElectricMotor", "IfcEngine", "IfcEvaporativeCooler", "IfcEvaporator", "IfcHeatExchanger", "IfcHumidifier",
            "IfcMotorConnection", "IfcSolarDevice", "IfcTransformer", "IfcTubeBundle", "IfcUnitaryEquipment",
        ]),
        ("IfcFlowController",
        [
            "IfcAirTerminalBox", "IfcDamper", "IfcElectricDistributionBoard", "IfcElectricTimeControl", "IfcFlowMeter", "IfcProtectiveDevice",
            "IfcSwitchingDevice", "IfcValve", "IfcElectricDistributionPoint",
        ]),
        ("IfcFlowFitting", ["IfcCableCarrierFitting", "IfcCableFitting", "IfcDuctFitting", "IfcJunctionBox", "IfcPipeFitting"]),
        ("IfcFlowMovingDevice", ["IfcCompressor", "IfcFan", "IfcPump"]),
        ("IfcFlowSegment", ["IfcCableCarrierSegment", "IfcCableSegment", "IfcDuctSegment", "IfcPipeSegment", "IfcConveyorSegment"]),
        ("IfcFlowStorageDevice", ["IfcElectricFlowStorageDevice", "IfcTank"]),
        ("IfcFlowTerminal",
        [
            "IfcAirTerminal", "IfcAudioVisualAppliance", "IfcCommunicationsAppliance", "IfcElectricAppliance", "IfcFireSuppressionTerminal", "IfcLamp",
            "IfcLightFixture", "IfcLiquidTerminal", "IfcMedicalDevice", "IfcMobileTelecommunicationsAppliance", "IfcOutlet", "IfcSanitaryTerminal",
            "IfcSignal", "IfcSpaceHeater", "IfcStackTerminal", "IfcWasteTerminal",
        ]),
        ("IfcFlowTreatmentDevice", ["IfcDuctSilencer", "IfcElectricFlowTreatmentDevice", "IfcFilter", "IfcInterceptor"]),
        (DistributionControlElement,
        [
            "IfcActuator", "IfcAlarm", "IfcController", "IfcFlowInstrument", "IfcProtectiveDeviceTrippingUnit", "IfcSensor", "IfcUnitaryControlElement",
        ]),
        // IFC2X3's electrical element (IfcElement's, deprecated since IFC4 for the distribution classes): a service all the same.
        (DistributionElement, ["IfcElectricalElement"]));

    // The modules' own classes that are services (Lapin's exhaust fan), told when they register.
    static readonly ConcurrentDictionary<string, string> Own = new(StringComparer.OrdinalIgnoreCase);

    static Dictionary<string, string> Build(params (string Parent, string[] Children)[] tree)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (parent, children) in tree) foreach (var c in children) d[c] = parent;
        return d;
    }

    /// <summary>A module's own class made a subtype of an IFC class of this tree (the class it is written to IFC as).</summary>
    public static void Subtype(string cls, string of)
    {
        if (!string.Equals(cls, of, StringComparison.OrdinalIgnoreCase)) Own[cls] = of;
    }

    /// <summary>A class's supertype in this tree; null for a class outside it (and for IfcDistributionElement itself).</summary>
    public static string? Parent(string cls) => Own.TryGetValue(cls, out var own) ? own : Schema.GetValueOrDefault(cls);

    /// <summary>Whether <paramref name="cls"/> is <paramref name="of"/> or one of its subtypes (names as IFC spells them, any case).</summary>
    public static bool IsA(string cls, string of)
    {
        if (string.Equals(cls, of, StringComparison.OrdinalIgnoreCase)) return true;
        for (var (c, n) = (Parent(cls), 0); c != null && n < 16; c = Parent(c), n++)
            if (string.Equals(c, of, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>Whether a class is a building service: an IfcDistributionElement or one of its subtypes, a module's own among them.</summary>
    public static bool IsDistribution(string cls) => IsA(cls, DistributionElement);

    /// <summary>Every class of the schema's tree under IfcDistributionElement (the modules' own left out), with it.</summary>
    public static IEnumerable<string> DistributionClasses => Schema.Keys.Prepend(DistributionElement);
}
