using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>The services a system carries; each has its own line type on the plan and its own defaults.</summary>
public static class Services
{
    public const string Heating = "heating", ColdWater = "coldWater", HotWater = "hotWater", Drainage = "drainage",
        SupplyAir = "supplyAir", ExtractAir = "extractAir", Electrical = "electrical", Gas = "gas", FuelOil = "fuelOil",
        FireAlarm = "fireAlarm", FireWater = "fireWater";
    public static readonly string[] All = [Heating, ColdWater, HotWater, Drainage, SupplyAir, ExtractAir, Electrical, Gas, FuelOil, FireAlarm, FireWater];

    public static string Label(string service) => service switch
    {
        Heating => "Heating", ColdWater => "Cold water", HotWater => "Hot water", Drainage => "Drainage",
        SupplyAir => "Supply air", ExtractAir => "Extract air", Electrical => "Electrical",
        Gas => "Gas", FuelOil => "Fuel oil", FireAlarm => "Fire detection and alarm", FireWater => "Fire hydrants", _ => service,
    };

    /// <summary>What a run of the service is in IFC: a pipe, a duct or a cable.</summary>
    public static string RunClass(string service) => service switch
    {
        SupplyAir or ExtractAir => "IfcDuctSegment",
        Electrical or FireAlarm => "IfcCableSegment",
        _ => "IfcPipeSegment",
    };

    public static bool IsAir(string service) => service is SupplyAir or ExtractAir;

    /// <summary>A fuel the building burns (gas, fuel oil): its pipes go through walls in sleeves, and its list is the gas and fuel oil bill of materials.</summary>
    public static bool IsFuel(string service) => service is Gas or FuelOil;

    /// <summary>Fire protection (a tester's request, report #89): the detection and alarm loop, and the hydrants' water; their list is the fire protection bill of materials.</summary>
    public static bool IsFire(string service) => service is FireAlarm or FireWater;

    /// <summary>Whether the service's runs are cables (IfcCableSegment, ports of kind CABLE).</summary>
    public static bool IsCable(string service) => service is Electrical or FireAlarm;

    /// <summary>What a gas system carries: natural gas or LPG (data for the list and for mep-pro's sizing).</summary>
    public static readonly string[] Gases = ["natural", "lpg"];

    /// <summary>A gas installation's pressure class: low (the house's, after the regulator) or medium (the street's, before it).</summary>
    public static readonly string[] Pressures = ["low", "medium"];
}

/// <summary>
/// What a system's runs and nodes start from (the system's defaults, each
/// overridable on the run or the node): the size (a pipe's or duct's
/// diameter, a cable's), the plane runs lie on and the elevation of each
/// plane over the storey, the slope (drainage falls), whether runs are drawn
/// as a pair (heating: supply and return) and how far apart, the material,
/// the insulation, the least bend radius as a multiple of the size.
/// </summary>
public sealed record SystemDefaults(double Size, string Plane, double FloorElevation, double CeilingElevation, double WallElevation,
    double Slope, bool Pair, double Separation, string? Material, double Insulation, double BendRadius)
{
    /// <summary>A gas system's gas: natural or LPG (<see cref="Services.Gases"/>); null for the other services.</summary>
    public string? Gas { get; init; }
    /// <summary>A gas system's pressure class: low or medium (<see cref="Services.Pressures"/>); null for the other services.</summary>
    public string? Pressure { get; init; }

    public double ElevationOf(string? plane) => (plane ?? Plane) switch { "ceiling" => CeilingElevation, "wall" => WallElevation, _ => FloorElevation };

    public static SystemDefaults For(string service) => service switch
    {
        Services.Heating => new(0.02, "floor", 0.2, 2.4, 1.1, 0, true, 0.1, "pex-al-pex", 0.02, 1.5),
        Services.ColdWater => new(0.02, "floor", 0.2, 2.4, 1.1, 0, false, 0.08, "pex-al-pex", 0.01, 1.5),
        Services.HotWater => new(0.02, "floor", 0.2, 2.4, 1.1, 0, false, 0.08, "pex-al-pex", 0.02, 1.5),
        Services.Drainage => new(0.11, "floor", 0.1, 2.3, 0.5, 0.02, false, 0, "pvc", 0, 1.5),
        Services.SupplyAir or Services.ExtractAir => new(0.125, "ceiling", 0.2, 2.5, 2.2, 0, false, 0, "galvanised", 0.03, 1.5),
        Services.Electrical => new(0.016, "ceiling", 0.05, 2.6, 1.1, 0, false, 0, "cable", 0, 3),
        // Gas: steel on the walls, high (2.1 m), low pressure after the regulator; natural gas.
        Services.Gas => new(0.022, "wall", 0.2, 2.4, 2.1, 0, false, 0, "steel", 0, 1.5) { Gas = "natural", Pressure = "low" },
        // Fuel oil: one copper pipe to the burner's filter (a single-pipe system), on the floor.
        Services.FuelOil => new(0.012, "floor", 0.2, 2.4, 1.1, 0, false, 0.05, "copper", 0, 3),
        // Fire detection: the loop's fire-resistant cable on the ceiling, by the detectors.
        Services.FireAlarm => new(0.016, "ceiling", 0.05, 2.6, 1.4, 0, false, 0, "fire-cable", 0, 3),
        // Fire hydrants: galvanised steel DN 50 under the ceiling, the hydrants fed from it.
        Services.FireWater => new(0.05, "ceiling", 0.2, 2.5, 1.5, 0, false, 0, "galvanised-pipe", 0, 1.5),
        _ => new(0.02, "floor", 0.2, 2.4, 1.1, 0, false, 0.1, null, 0, 1.5),
    };

    public JsonObject ToJson() => new()
    {
        ["size"] = Size, ["plane"] = Plane, ["floorElevation"] = FloorElevation, ["ceilingElevation"] = CeilingElevation, ["wallElevation"] = WallElevation,
        ["slope"] = Slope, ["pair"] = Pair, ["separation"] = Separation, ["material"] = Material, ["insulation"] = Insulation, ["bendRadius"] = BendRadius,
        ["gas"] = Gas, ["pressure"] = Pressure,
    };

    /// <summary>The defaults with what a JSON object gives over them (a system's own attributes, a command's patch).</summary>
    public SystemDefaults With(JsonNode? n)
    {
        if (n is not JsonObject o) return this;
        double N(string k, double d) => Json.Number(o[k]) ?? d;
        return new(N("size", Size), (string?)o["plane"] is "floor" or "ceiling" or "wall" ? (string)o["plane"]! : Plane,
            N("floorElevation", FloorElevation), N("ceilingElevation", CeilingElevation), N("wallElevation", WallElevation), N("slope", Slope),
            o["pair"] is JsonValue pv && pv.TryGetValue<bool>(out var pair) ? pair : Pair, N("separation", Separation),
            o.ContainsKey("material") ? (string?)o["material"] : Material, N("insulation", Insulation), N("bendRadius", BendRadius))
        {
            // Data on a gas system only: a fuel and a pressure class the lists name.
            Gas = Gas != null && (string?)o["gas"] is { } g && Services.Gases.Contains(g) ? g : Gas,
            Pressure = Pressure != null && (string?)o["pressure"] is { } p && Services.Pressures.Contains(p) ? p : Pressure,
        };
    }
}

/// <summary>
/// How one line of a system draws in plan: its colour, its line type (one of the drafting standards' line types: solid,
/// dashed, center, dashdot…), its pen (1–16; null: a seen line's), and its line type where it lies out of the cut's reach
/// (<see cref="Above"/>: over the plan's cut, under its level; null or <c>overhead</c>: the overhead line's dashes).
/// </summary>
public sealed record LineLook(string Color, string Pattern, string? Above = null, int? Pen = null)
{
    public Lapin.Drawing.LineStyleSpec Spec => new(Pen, Color, Pattern == "solid" ? null : Pattern);
    public Lapin.Drawing.LineStyleSpec AboveSpec => new(Pen, Color, Above is null or "solid" or Overhead ? null : Above);

    /// <summary>What <see cref="Above"/> names for the overhead line's own dashes (as null does).</summary>
    public const string Overhead = "overhead";

    public JsonObject ToJson() => new() { ["color"] = Color, ["pattern"] = Pattern, ["above"] = Above, ["pen"] = Pen };

    /// <summary>This look with what a JSON object gives over it (a colour #rrggbb, a line type's name, a pen 1–16; null: the default's again).</summary>
    public LineLook With(JsonNode? n, LineLook fallback)
    {
        if (n is not JsonObject o) return this;
        string? Text(string k) => (string?)o[k] is { } v && v.Trim().Length > 0 ? v.Trim() : null;
        var color = o.ContainsKey("color") ? (Text("color") is { Length: 7 } c && c[0] == '#' ? c : fallback.Color) : Color;
        var pattern = o.ContainsKey("pattern") ? Text("pattern") ?? fallback.Pattern : Pattern;
        var above = o.ContainsKey("above") ? Text("above") ?? fallback.Above : Above;
        var pen = o.ContainsKey("pen") ? (Json.Number(o["pen"]) is { } p && p >= 1 && p <= 16 ? (int)p : fallback.Pen) : Pen;
        return new(color, pattern, above, pen);
    }
}

/// <summary>
/// How a system draws in plan (MEP drawings' conventions, colour and line type per service): its line, and a paired run's
/// return. The defaults by service (<see cref="For"/>) read in black and white by their line types alone within one plan
/// (heating supply solid and return dashed; cold water centre line, hot water dash dot; drainage solid and heavier; gas and
/// fuel oil dash dot dot; electrical short dashes; supply air solid, extract air dashed, both heavier; fire alarm dash dot,
/// fire water centre line, heavier); over the plan's cut (on the ceiling) a solid line takes the overhead dashes and a
/// patterned one dots or a dash dot dot. Editable per system (Systems…); a view's Visibility / Graphics (the MEP category's
/// or an element's lines) still wins over it.
/// </summary>
public sealed record SystemStyle(LineLook Line, LineLook Return)
{
    public static SystemStyle For(string service) => service switch
    {
        Services.Heating => new(new("#d32f2f", "solid"), new("#1565c0", "dashed", "dashdotdot")),
        Services.ColdWater => new(new("#2e7d32", "center", "dotted"), new("#2e7d32", "dashed", "dashdotdot")),
        Services.HotWater => new(new("#c2185b", "dashdot", "dotted"), new("#ad1457", "dashed", "dashdotdot")),
        Services.Drainage => new(new("#6d4c41", "solid", null, 5), new("#6d4c41", "dashed", "dashdotdot", 5)),
        Services.Gas => new(new("#d4a106", "dashdotdot", "dotted"), new("#d4a106", "dashed", "dotted")),
        Services.FuelOil => new(new("#8d5a2b", "dashdotdot", "dotted"), new("#8d5a2b", "dashed", "dotted")),
        Services.Electrical => new(new("#ef6c00", "hidden", "dotted"), new("#ef6c00", "dashed", "dotted")),
        Services.SupplyAir => new(new("#1e88e5", "solid", null, 4), new("#1e88e5", "dashed", "dashdotdot", 4)),
        Services.ExtractAir => new(new("#7cb342", "dashed", "dashdotdot", 4), new("#7cb342", "dashed", "dashdotdot", 4)),
        Services.FireAlarm => new(new("#e53935", "dashdot", "dotted"), new("#e53935", "dashed", "dotted")),
        Services.FireWater => new(new("#b71c1c", "center", "dotted", 4), new("#b71c1c", "dashed", "dashdotdot", 4)),
        _ => new(new("#455a64", "solid"), new("#455a64", "dashed", "dashdotdot")),
    };

    public JsonObject ToJson() => new() { ["line"] = Line.ToJson(), ["return"] = Return.ToJson() };

    /// <summary>The style with what a JSON object gives over it ({line: {color, pattern, above, pen}, return: {…}}; a field null: the service's again).</summary>
    public SystemStyle With(JsonNode? n, SystemStyle fallback) => n is JsonObject o
        ? new(Line.With(o["line"], fallback.Line), Return.With(o["return"], fallback.Return))
        : this;

    /// <summary>The look of a run's line: the supply's (or a single run's), or the return's of a pair.</summary>
    public LineLook Of(string line) => line == "return" ? Return : Line;
}

/// <summary>What a system's devices' coverage circles draw by (<see cref="Systems.Coverage"/>): in every view, on the selected devices only (on the screen), or in none.</summary>
public static class CoverageModes
{
    public const string All = "all", Selected = "selected", None = "none";
    public static readonly string[] Each = [All, Selected, None];
    public static string? Checked(JsonNode? n) => (string?)n is { } v && Each.Contains(v) ? v : null;
}

/// <summary>
/// A system: one service of the building (IfcDistributionSystem), the
/// defaults its runs and nodes take, and what it serves (the building,
/// through IfcRelServicesBuildings).
/// </summary>
public static class Systems
{
    public const string Class = "IfcDistributionSystem";
    public const string Serves = "IfcRelServicesBuildings";

    public static string Service(Entity system) => system.Str("service") ?? Services.Heating;

    /// <summary>The system's defaults: the service's, with the system's own values over them.</summary>
    public static SystemDefaults Of(Entity system) => SystemDefaults.For(Service(system)).With(system.Attrs["defaults"]);

    /// <summary>How the system draws in plan: the service's colours and line types, with the system's own over them.</summary>
    public static SystemStyle StyleOf(Entity system)
    {
        var d = SystemStyle.For(Service(system));
        return d.With(system.Attrs["style"], d);
    }

    /// <summary>
    /// Whether a view draws a system's coverage circles in every view (<c>all</c>), on the selected devices only (<c>selected</c>:
    /// the interface draws them over the plan, never printed) or in none: the view's own (<c>mepCoverage</c>), else the system's, else selected.
    /// </summary>
    public static string Coverage(Entity? view, Entity? system) =>
        CoverageModes.Checked(view?.Attrs["mepCoverage"]) ?? CoverageModes.Checked(system?.Attrs["coverage"]) ?? CoverageModes.Selected;

    public static Entity Require(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: Class } s ? s : throw new CommandException("Which system? (mep.createSystem makes one.)");

    public static IEnumerable<Entity> All(IModelView m) => m.OfClass(Class);

    /// <summary>A system of one service with its defaults (and what <paramref name="defaults"/> sets over them), serving the building.</summary>
    public static Entity Create(ICommandContext ctx, string service, string? name = null, JsonObject? defaults = null)
    {
        if (!Services.All.Contains(service)) throw new CommandException($"A system carries one of: {string.Join(", ", Services.All)}.");
        var count = All(ctx.Model).Count(s => Service(s) == service);
        var attrs = new JsonObject { ["service"] = service };
        if (defaults != null) attrs["defaults"] = SystemDefaults.For(service).With(defaults).ToJson();
        var system = new Entity(ctx.NewId(), Class, name is { Length: > 0 } ? name : count == 0 ? Services.Label(service) : $"{Services.Label(service)} {count + 1}", attrs);
        ctx.Add(system);
        ctx.Relate(Serves, system.Id, Spatial.EnsureBuilding(ctx).Id);
        return system;
    }

    /// <summary>Whether a line type's name is one a plan draws: Lapin's own, or the project's drafting standards'.</summary>
    static bool LineTypeKnown(IModelView m, string name) =>
        name is "solid" or LineLook.Overhead || Lapin.Drawing.LineStyles.Patterns.Contains(name) || Lapin.Drawing.Dashes.Of(name).Length > 0
        || (DraftingResources.Book(m).LineTypes ?? []).Any(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "System",
        [
            new("service", "Service", "choice", Choices: Services.All, ReadOnly: true),
        ], "#4a6fa5", Kind: "type"));

        // A system of one service, with the service's defaults (overridden by "defaults"), serving the building.
        r.Command("createSystem", "New system", (ctx, a) =>
        {
            var system = Create(ctx, (string?)a["service"] ?? Services.Heating, (string?)a["name"], a["defaults"] as JsonObject);
            return new JsonObject { ["id"] = system.Id };
        });

        // The defaults changed: every run and node without its own value follows.
        r.Command("updateSystem", "Change system", (ctx, a) =>
        {
            var system = Require(ctx.Model, (string?)a["id"]);
            var attrs = system.CloneAttrs();
            if (a["defaults"] is JsonObject d) attrs["defaults"] = Of(system).With(d).ToJson();
            // Whether its runs cut provision openings in the walls and floors they cross (decision 293): true, false, null as the project's cut order says.
            if (a.ContainsKey("cutHosts"))
            {
                if (a["cutHosts"] is JsonValue cv && cv.TryGetValue<bool>(out var cut)) attrs["cutHosts"] = cut;
                else attrs.Remove("cutHosts");
            }
            // How it draws in plan: {line, return}, each {color, pattern, above, pen}, a field null the service's again; null: the service's.
            if (a.ContainsKey("style"))
            {
                if (a["style"] is JsonObject so)
                {
                    var style = StyleOf(system).With(so, SystemStyle.For(Service(system)));
                    foreach (var look in new[] { style.Line, style.Return })
                        foreach (var p in new[] { look.Pattern, look.Above })
                            if (p != null && !LineTypeKnown(ctx.Model, p)) throw new CommandException($"There is no line type „{p}” (Drafting standards → Line types).");
                    attrs["style"] = style.ToJson();
                }
                else attrs.Remove("style");
            }
            // Its devices' coverage circles: all, selected, none (null: selected).
            if (a.ContainsKey("coverage"))
            {
                if (a["coverage"] is null) attrs.Remove("coverage");
                else attrs["coverage"] = CoverageModes.Checked(a["coverage"]) ?? throw new CommandException("Coverage is all, selected or none.");
            }
            var name = ((string?)a["name"])?.Trim();
            ctx.Replace(system.With(name: string.IsNullOrEmpty(name) ? null : name, attrs: attrs));
            return null;
        });

        r.Query("systems", (q, _) => new JsonArray(All(q.Model).Select(s => (JsonNode)new JsonObject
        {
            ["id"] = s.Id, ["name"] = s.Name, ["service"] = Service(s), ["defaults"] = Of(s).ToJson(), ["cutHosts"] = s.Attrs["cutHosts"]?.DeepClone(),
            ["style"] = StyleOf(s).ToJson(), ["coverage"] = s.Attrs["coverage"]?.DeepClone(),
            ["nodes"] = Nodes.Of(q.Model, s.Id).Count(), ["runs"] = Runs.Of(q.Model, s.Id).Count(),
        }).ToArray()));
    }
}
