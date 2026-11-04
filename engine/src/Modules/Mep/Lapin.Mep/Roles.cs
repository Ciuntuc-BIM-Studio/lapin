using Lapin.Geometry;

namespace Lapin.Mep;

/// <summary>Which way a port carries its service, seen from the node: into it, out of it, or either.</summary>
public enum Flow { In, Out, Both }

/// <summary>
/// A port of a node's role, in the node's own frame (X across the node,
/// Y to its back, Z up from its bottom): its key, the service it takes, its
/// flow, where it is and which way it faces, its size (0: the system's).
/// </summary>
public sealed record RolePort(string Key, string Service, Flow Flow, Vec3 At, Vec3 Dir, double Size = 0)
{
    /// <summary>The port this one pairs with for a paired run (supply with return).</summary>
    public string? Pair { get; init; }

    /// <summary>
    /// What the port is for beyond its service, so an endpoint joins the right
    /// port of its source: a tank's fill takes the fill point's pipe, its vent
    /// the vent cap's; null for the plain supply (the ports without a tag meet).
    /// </summary>
    public string? Link { get; init; }
}

/// <summary>What a role is in the model: its IFC class, how it opens, its box, its ports, its symbol, its duty.</summary>
public sealed record Role(string Id, string Label, string IfcClass, string Predefined, Vec3 Size, double Elevation, IReadOnlyList<RolePort> Ports,
    string Symbol, string? Kind = null, double Duty = 0, string DutyUnit = "")
{
    public bool IsSource => Kind == "source";
    public bool IsEndpoint => Kind == "endpoint";
    /// <summary>A fitting sits on a run: dropped onto one, it splits it and takes both halves.</summary>
    public bool IsInline => Kind == "inline";
    public bool IsRise => Id is "rise";

    /// <summary>
    /// The service in which this source is an endpoint: a gas boiler is the
    /// heating's source and the gas installation's endpoint (an appliance).
    /// </summary>
    public string? EndpointFor { get; init; }

    /// <summary>What the role is in a system of this service: its kind, or an endpoint where it is one (<see cref="EndpointFor"/>).</summary>
    public string? KindIn(string service) => EndpointFor == service ? "endpoint" : Kind;

    /// <summary>A gas or fuel oil appliance's consumption, m³/h of natural gas (L/h of fuel oil): data the lists sum and mep-pro's sizing reads.</summary>
    public double Consumption { get; init; }

    /// <summary>What the element is when its IFC predefined type does not say it (USERDEFINED, or a fitting's ENTRY that is a fill point): its ObjectType.</summary>
    public string? ObjectType { get; init; }

    /// <summary>A gas appliance's type by how it takes its air and gives its flue gas (EN 1749): A no flue, B open flue, C room-sealed; null for the rest.</summary>
    public string? Flue { get; init; }
}

/// <summary>
/// The catalogue of node roles (docs/plan-mep.md): what the engineer places
/// on the schematic, by service. Sizes and duties are planning figures;
/// a library element mapped later replaces the box, not the role.
/// </summary>
public static class Roles
{
    static readonly Vec3 Up = new(0, 0, 1), Down = new(0, 0, -1), Back = new(0, 1, 0), Front = new(0, -1, 0), Left = new(-1, 0, 0), Right = new(1, 0, 0);

    // Heating: supply enters a terminal, return leaves it; a source is the other way round.
    static RolePort[] HeatingPair(double x, double y, double z, Vec3 dir, bool source, double gap = 0.05) =>
    [
        new(source ? "supply" : "supply", Services.Heating, source ? Flow.Out : Flow.In, new(x - gap / 2, y, z), dir) { Pair = "return" },
        new("return", Services.Heating, source ? Flow.In : Flow.Out, new(x + gap / 2, y, z), dir) { Pair = "supply" },
    ];

    public static readonly IReadOnlyList<Role> All =
    [
        // Heating
        new("boiler", "Boiler", "IfcBoiler", "WATER", new(0.45, 0.35, 0.75), 1.0, HeatingPair(0, -0.175, 0, Front, source: true), "boiler", "source", 24000, "W"),
        new("manifold", "Manifold", "IfcPipeFitting", "JUNCTION", new(0.4, 0.1, 0.12), 0.4,
            [.. HeatingPair(-0.15, 0.05, 0.06, Back, source: false), .. HeatingPair(0.15, -0.05, 0.06, Front, source: true)], "manifold", "source"),
        new("radiator", "Radiator", "IfcSpaceHeater", "RADIATOR", new(1.0, 0.1, 0.6), 0.15, HeatingPair(0, -0.05, 0, Down, source: false), "radiator", "endpoint", 1200, "W"),
        // Water
        new("waterMeter", "Water meter", "IfcFlowMeter", "WATERMETER", new(0.2, 0.08, 0.08), 0.5,
            [new("in", "coldWater", Flow.In, new(-0.1, 0, 0.04), Left), new("out", "coldWater", Flow.Out, new(0.1, 0, 0.04), Right)], "meter", "inline"),
        new("valve", "Valve", "IfcValve", "ISOLATING", new(0.08, 0.06, 0.06), 0.5,
            [new("in", "*", Flow.In, new(-0.04, 0, 0.03), Left), new("out", "*", Flow.Out, new(0.04, 0, 0.03), Right)], "valve", "inline"),
        new("basin", "Wash basin", "IfcSanitaryTerminal", "WASHHANDBASIN", new(0.55, 0.45, 0.2), 0.8,
            [new("cold", "coldWater", Flow.In, new(-0.05, 0.15, 0), Down), new("hot", "hotWater", Flow.In, new(0.05, 0.15, 0), Down), new("waste", "drainage", Flow.Out, new(0, 0, 0), Down, 0.04)], "basin", "endpoint", 0.1, "L/s"),
        new("sink", "Kitchen sink", "IfcSanitaryTerminal", "SINK", new(0.8, 0.5, 0.2), 0.85,
            [new("cold", "coldWater", Flow.In, new(-0.05, 0.2, 0), Down), new("hot", "hotWater", Flow.In, new(0.05, 0.2, 0), Down), new("waste", "drainage", Flow.Out, new(0, 0, 0), Down, 0.04)], "sink", "endpoint", 0.2, "L/s"),
        new("shower", "Shower", "IfcSanitaryTerminal", "SHOWER", new(0.9, 0.9, 0.05), 0,
            [new("cold", "coldWater", Flow.In, new(-0.05, 0.4, 1.1), Back), new("hot", "hotWater", Flow.In, new(0.05, 0.4, 1.1), Back), new("waste", "drainage", Flow.Out, new(0, 0, 0), Down, 0.05)], "shower", "endpoint", 0.2, "L/s"),
        new("bath", "Bath", "IfcSanitaryTerminal", "BATH", new(1.7, 0.75, 0.55), 0,
            [new("cold", "coldWater", Flow.In, new(-0.05, 0.3, 0.55), Up), new("hot", "hotWater", Flow.In, new(0.05, 0.3, 0.55), Up), new("waste", "drainage", Flow.Out, new(-0.6, 0, 0), Down, 0.05)], "bath", "endpoint", 0.3, "L/s"),
        new("wc", "WC", "IfcSanitaryTerminal", "TOILETPAN", new(0.4, 0.65, 0.4), 0,
            [new("cold", "coldWater", Flow.In, new(0, 0.3, 0.4), Up), new("waste", "drainage", Flow.Out, new(0, 0.1, 0), Down, 0.11)], "wc", "endpoint", 0.1, "L/s"),
        // Drainage
        new("stack", "Stack", "IfcPipeSegment", "RIGIDSEGMENT", new(0.11, 0.11, 3.0), 0,
            [new("in", "drainage", Flow.In, new(0, -0.055, 0.5), Front, 0.11), new("out", "drainage", Flow.Out, new(0, 0, 0), Down, 0.11), new("vent", "drainage", Flow.Out, new(0, 0, 3.0), Up, 0.11)], "stack", "source"),
        new("cleanout", "Cleanout", "IfcPipeFitting", "USERDEFINED", new(0.15, 0.11, 0.11), 0,
            [new("in", "drainage", Flow.In, new(-0.075, 0, 0.055), Left), new("out", "drainage", Flow.Out, new(0.075, 0, 0.055), Right)], "cleanout", "inline"),
        // Electrical
        new("panel", "Consumer unit", "IfcElectricDistributionBoard", "CONSUMERUNIT", new(0.35, 0.1, 0.3), 1.4,
            [new("in", "electrical", Flow.In, new(0, 0, 0.3), Up), new("out", "electrical", Flow.Out, new(0, 0, 0), Down)], "panel", "source"),
        new("outlet", "Outlet", "IfcOutlet", "POWEROUTLET", new(0.08, 0.04, 0.08), 0.3, [new("in", "electrical", Flow.In, new(0, 0, 0.08), Up)], "outlet", "endpoint", 100, "W"),
        new("light", "Light", "IfcLightFixture", "POINTSOURCE", new(0.2, 0.2, 0.1), 2.4, [new("in", "electrical", Flow.In, new(0, 0, 0.1), Up)], "light", "endpoint", 10, "W"),
        new("switch", "Switch", "IfcSwitchingDevice", "TOGGLESWITCH", new(0.08, 0.04, 0.08), 1.1,
            [new("in", "electrical", Flow.In, new(-0.02, 0, 0.08), Up), new("out", "electrical", Flow.Out, new(0.02, 0, 0.08), Up)], "switch", "inline"),
        // Ventilation
        new("fan", "Fan", "IfcFan", "TUBEAXIAL", new(0.3, 0.15, 0.15), 2.2,
            [new("in", "supplyAir", Flow.In, new(-0.15, 0, 0.075), Left, 0.125), new("out", "supplyAir", Flow.Out, new(0.15, 0, 0.075), Right, 0.125)], "fan", "source", 120, "L/s"),
        new("diffuser", "Diffuser", "IfcAirTerminal", "DIFFUSER", new(0.3, 0.3, 0.05), 2.4, [new("in", "supplyAir", Flow.In, new(0, 0, 0.05), Up, 0.125)], "diffuser", "endpoint", 30, "L/s"),
        new("grille", "Extract grille", "IfcAirTerminal", "GRILLE", new(0.3, 0.15, 0.05), 2.4, [new("out", "extractAir", Flow.Out, new(0, 0, 0.05), Up, 0.125)], "grille", "endpoint", 30, "L/s"),
        // Gas (a residence's installation, decision 285): the meter box where the service enters, the regulator, the meter, the main shut-off and
        // the appliance valves, flexible connectors, and the appliances; consumption in m³/h of natural gas (data for mep-pro's sizing).
        new("meterBox", "Meter box", "IfcDistributionChamberElement", "METERCHAMBER", new(0.6, 0.25, 0.6), 0.5,
            [new("in", Services.Gas, Flow.In, new(-0.15, 0, 0), Down), new("out", Services.Gas, Flow.Out, new(0.15, 0.125, 0.45), Back)], "meterBox", "source"),
        new("regulator", "Gas regulator", "IfcValve", "PRESSUREREDUCING", new(0.16, 0.1, 0.14), 1.5,
            [new("in", Services.Gas, Flow.In, new(-0.08, 0, 0.04), Left), new("out", Services.Gas, Flow.Out, new(0.08, 0, 0.04), Right)], "regulator", "inline"),
        new("gasMeter", "Gas meter", "IfcFlowMeter", "GASMETER", new(0.25, 0.17, 0.3), 1.5,
            [new("in", Services.Gas, Flow.In, new(-0.06, 0, 0.3), Up), new("out", Services.Gas, Flow.Out, new(0.06, 0, 0.3), Up)], "gasMeter", "inline"),
        new("gasShutoff", "Main gas shut-off valve", "IfcValve", "GASCOCK", new(0.1, 0.06, 0.06), 1.5,
            [new("in", Services.Gas, Flow.In, new(-0.05, 0, 0.03), Left), new("out", Services.Gas, Flow.Out, new(0.05, 0, 0.03), Right)], "gasCock", "inline"),
        new("gasTap", "Appliance gas valve", "IfcValve", "GASTAP", new(0.07, 0.05, 0.05), 1.0,
            [new("in", Services.Gas, Flow.In, new(-0.035, 0, 0.025), Left), new("out", Services.Gas, Flow.Out, new(0.035, 0, 0.025), Right)], "gasCock", "inline"),
        new("flexConnector", "Flexible connector", "IfcPipeSegment", "FLEXIBLESEGMENT", new(0.5, 0.03, 0.03), 0.9,
            [new("in", "*", Flow.In, new(-0.25, 0, 0.015), Left), new("out", "*", Flow.Out, new(0.25, 0, 0.015), Right)], "flex", "inline"),
        new("gasBoiler", "Gas boiler", "IfcBoiler", "WATER", new(0.45, 0.35, 0.75), 1.0,
            [.. HeatingPair(0, -0.175, 0, Front, source: true), new("gas", Services.Gas, Flow.In, new(0.12, -0.12, 0), Down)], "boiler", "source", 24000, "W")
            { EndpointFor = Services.Gas, Consumption = 2.8, Flue = "C" },
        new("cooker", "Gas cooker", "IfcBurner", "USERDEFINED", new(0.6, 0.6, 0.85), 0,
            [new("gas", Services.Gas, Flow.In, new(0.2, 0.3, 0.5), Back)], "cooker", "endpoint", 7500, "W") { Consumption = 0.8, ObjectType = "Gas cooker", Flue = "A" },
        new("waterHeater", "Gas water heater", "IfcBoiler", "WATER", new(0.35, 0.22, 0.6), 1.2,
            [new("gas", Services.Gas, Flow.In, new(0, -0.05, 0), Down), new("cold", Services.ColdWater, Flow.In, new(-0.08, -0.05, 0), Down), new("hot", Services.HotWater, Flow.Out, new(0.08, -0.05, 0), Down)],
            "waterHeater", "endpoint", 19000, "W") { Consumption = 2.2, ObjectType = "Gas water heater", Flue = "B" },
        // The street's supply: the tapping tee on the main the service pipe leaves from (its outlet up, so either side of the main takes it).
        new("tappingTee", "Tapping tee", "IfcPipeFitting", "JUNCTION", new(0.14, 0.1, 0.1), 0,
            [new("in", Services.Gas, Flow.In, new(-0.07, 0, 0), Left), new("out", Services.Gas, Flow.Out, new(0.07, 0, 0), Right), new("branch", Services.Gas, Flow.Out, new(0, 0, 0.12), Up)], "tappingTee", "inline"),
        // Fuel oil: the tank (indoor, outdoor or buried) with its fill, vent, supply and return; the fill point and the vent cap outside;
        // the filter before the burner; the oil boiler, the heating's source and the fuel oil's endpoint. Consumption in L/h.
        new("oilTank", "Fuel oil tank", "IfcTank", "STORAGE", new(1.6, 0.7, 1.5), 0,
            [new("fill", Services.FuelOil, Flow.In, new(-0.6, 0, 1.5), Up) { Link = "fill" }, new("vent", Services.FuelOil, Flow.Out, new(-0.4, 0, 1.5), Up) { Link = "vent" },
             new("supply", Services.FuelOil, Flow.Out, new(0.4, 0, 1.5), Up), new("return", Services.FuelOil, Flow.In, new(0.6, 0, 1.5), Up) { Link = "return" }], "oilTank", "source"),
        new("fillPoint", "Fill point", "IfcPipeFitting", "ENTRY", new(0.12, 0.12, 0.12), 0.6, [new("out", Services.FuelOil, Flow.Out, new(0, 0, 0), Down) { Link = "fill" }], "fillPoint", "endpoint") { ObjectType = "Fill point" },
        new("ventCap", "Vent cap", "IfcPipeFitting", "EXIT", new(0.1, 0.1, 0.1), 2.5, [new("in", Services.FuelOil, Flow.In, new(0, 0, 0), Down) { Link = "vent" }], "ventCap", "endpoint") { ObjectType = "Vent cap" },
        new("oilFilter", "Oil filter", "IfcFilter", "OILFILTER", new(0.1, 0.08, 0.18), 0.5,
            [new("in", Services.FuelOil, Flow.In, new(-0.05, 0, 0.14), Left), new("out", Services.FuelOil, Flow.Out, new(0.05, 0, 0.14), Right)], "oilFilter", "inline"),
        new("oilBoiler", "Oil boiler", "IfcBoiler", "WATER", new(0.6, 0.8, 0.9), 0,
            [.. HeatingPair(0, 0.4, 0.8, Back, source: true), new("oil", Services.FuelOil, Flow.In, new(-0.2, -0.4, 0.4), Front), new("oilReturn", Services.FuelOil, Flow.Out, new(-0.1, -0.4, 0.4), Front) { Link = "return" }],
            "boiler", "source", 25000, "W") { EndpointFor = Services.FuelOil, Consumption = 2.5, Flue = "B" },
        // Any service
        new("rise", "Rise / drop", "IfcPipeFitting", "USERDEFINED", new(0.1, 0.1, 0.1), 0,
            [new("bottom", "*", Flow.Both, new(0, 0, 0), Down), new("top", "*", Flow.Both, new(0, 0, 0), Up)], "rise", "rise"),
        new("cap", "Cap", "IfcPipeFitting", "USERDEFINED", new(0.05, 0.05, 0.05), 0, [new("in", "*", Flow.Both, new(0, 0, 0.025), Left)], "cap", "endpoint"),
    ];

    public static Role? Get(string? id) => All.FirstOrDefault(r => r.Id == id);

    public static Role Require(string? id) => Get(id) ?? throw new Platform.CommandException($"„{id}” is not a role: {string.Join(", ", All.Select(r => r.Id))}.");

    /// <summary>Whether a port takes a service: its own, or any ("*").</summary>
    public static bool Takes(RolePort p, string service) => p.Service == "*" || p.Service == service;
}
