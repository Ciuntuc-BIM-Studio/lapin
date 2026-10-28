using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>The services a system carries; each has its own line type on the plan and its own defaults.</summary>
public static class Services
{
    public const string Heating = "heating", ColdWater = "coldWater", HotWater = "hotWater", Drainage = "drainage",
        SupplyAir = "supplyAir", ExtractAir = "extractAir", Electrical = "electrical";
    public static readonly string[] All = [Heating, ColdWater, HotWater, Drainage, SupplyAir, ExtractAir, Electrical];

    public static string Label(string service) => service switch
    {
        Heating => "Heating", ColdWater => "Cold water", HotWater => "Hot water", Drainage => "Drainage",
        SupplyAir => "Supply air", ExtractAir => "Extract air", Electrical => "Electrical", _ => service,
    };

    /// <summary>What a run of the service is in IFC: a pipe, a duct or a cable.</summary>
    public static string RunClass(string service) => service switch
    {
        SupplyAir or ExtractAir => "IfcDuctSegment",
        Electrical => "IfcCableSegment",
        _ => "IfcPipeSegment",
    };

    public static bool IsAir(string service) => service is SupplyAir or ExtractAir;
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
    public double ElevationOf(string? plane) => (plane ?? Plane) switch { "ceiling" => CeilingElevation, "wall" => WallElevation, _ => FloorElevation };

    public static SystemDefaults For(string service) => service switch
    {
        Services.Heating => new(0.02, "floor", 0.2, 2.4, 1.1, 0, true, 0.1, "pex-al-pex", 0.02, 1.5),
        Services.ColdWater => new(0.02, "floor", 0.2, 2.4, 1.1, 0, false, 0.08, "pex-al-pex", 0.01, 1.5),
        Services.HotWater => new(0.02, "floor", 0.2, 2.4, 1.1, 0, false, 0.08, "pex-al-pex", 0.02, 1.5),
        Services.Drainage => new(0.11, "floor", 0.1, 2.3, 0.5, 0.02, false, 0, "pvc", 0, 1.5),
        Services.SupplyAir or Services.ExtractAir => new(0.125, "ceiling", 0.2, 2.5, 2.2, 0, false, 0, "galvanised", 0.03, 1.5),
        Services.Electrical => new(0.016, "ceiling", 0.05, 2.6, 1.1, 0, false, 0, "cable", 0, 3),
        _ => new(0.02, "floor", 0.2, 2.4, 1.1, 0, false, 0.1, null, 0, 1.5),
    };

    public JsonObject ToJson() => new()
    {
        ["size"] = Size, ["plane"] = Plane, ["floorElevation"] = FloorElevation, ["ceilingElevation"] = CeilingElevation, ["wallElevation"] = WallElevation,
        ["slope"] = Slope, ["pair"] = Pair, ["separation"] = Separation, ["material"] = Material, ["insulation"] = Insulation, ["bendRadius"] = BendRadius,
    };

    /// <summary>The defaults with what a JSON object gives over them (a system's own attributes, a command's patch).</summary>
    public SystemDefaults With(JsonNode? n)
    {
        if (n is not JsonObject o) return this;
        double N(string k, double d) => Json.Number(o[k]) ?? d;
        return new(N("size", Size), (string?)o["plane"] is "floor" or "ceiling" or "wall" ? (string)o["plane"]! : Plane,
            N("floorElevation", FloorElevation), N("ceilingElevation", CeilingElevation), N("wallElevation", WallElevation), N("slope", Slope),
            o["pair"] is JsonValue pv && pv.TryGetValue<bool>(out var pair) ? pair : Pair, N("separation", Separation),
            o.ContainsKey("material") ? (string?)o["material"] : Material, N("insulation", Insulation), N("bendRadius", BendRadius));
    }
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
            var name = ((string?)a["name"])?.Trim();
            ctx.Replace(system.With(name: string.IsNullOrEmpty(name) ? null : name, attrs: attrs));
            return null;
        });

        r.Query("systems", (q, _) => new JsonArray(All(q.Model).Select(s => (JsonNode)new JsonObject
        {
            ["id"] = s.Id, ["name"] = s.Name, ["service"] = Service(s), ["defaults"] = Of(s).ToJson(),
            ["nodes"] = Nodes.Of(q.Model, s.Id).Count(), ["runs"] = Runs.Of(q.Model, s.Id).Count(),
        }).ToArray()));
    }
}
