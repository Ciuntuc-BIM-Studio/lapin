namespace Lapin.Mep;

/// <summary>A pipe, duct or cable material a run may be of, and the services it carries; its laying rules come with mep-pro (<see cref="ILayingRules"/>).</summary>
public sealed record RunMaterial(string Id, string Label, string[] Services, string[]? Aliases = null);

/// <summary>The materials runs are made of, by service; the system's default is the usual one (multilayer pipe, PVC drain, galvanised duct, cable, steel for gas, copper for fuel oil).</summary>
public static class RunMaterials
{
    public static readonly IReadOnlyList<RunMaterial> All =
    [
        new("steel", "Black steel", [Services.Heating, Services.Gas, Services.FuelOil, Services.FireWater], ["Steel"]),
        new("copper", "Copper", [Services.Heating, Services.ColdWater, Services.HotWater, Services.Gas, Services.FuelOil]),
        new("pex-al-pex", "PEX-AL-PEX multilayer", [Services.Heating, Services.ColdWater, Services.HotWater, Services.Gas], ["PEX", "Pexal"]),
        // Gas buried in the ground, the street's main and a service pipe: polyethylene (PE 100, SDR 11), laid bent and joined by electrofusion.
        new("pe", "PE 100 (buried)", [Services.Gas, Services.ColdWater], ["PE", "Polyethylene"]),
        new("pp-r", "PP-R", [Services.ColdWater, Services.HotWater]),
        new("pvc", "PVC drainage", [Services.Drainage], ["PVC"]),
        new("pp-drain", "PP drainage", [Services.Drainage]),
        new("galvanised", "Galvanised steel duct", [Services.SupplyAir, Services.ExtractAir], ["Galvanised steel"]),
        new("flex-duct", "Flexible duct", [Services.SupplyAir, Services.ExtractAir]),
        new("cable", "Cable in conduit", [Services.Electrical], ["Copper"]),
        // Fire protection: the detection loop's fire-resistant cable (its circuit integrity class is the designer's), the hydrants' galvanised pipe.
        new("fire-cable", "Fire-resistant cable", [Services.FireAlarm], ["Fire cable"]),
        new("galvanised-pipe", "Galvanised steel pipe", [Services.FireWater, Services.ColdWater], ["Galvanised pipe"]),
    ];

    public static RunMaterial? Get(string? id) =>
        id == null ? null : All.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase) || (x.Aliases?.Any(a => string.Equals(a, id, StringComparison.OrdinalIgnoreCase)) ?? false));

    /// <summary>A run's material: its own, its system's, else the first its service takes.</summary>
    public static RunMaterial Of(string service, string? id) => Get(id) is { } m && m.Services.Contains(service) ? m : Get(id) ?? All.First(x => x.Services.Contains(service));

    public static IEnumerable<RunMaterial> For(string service) => All.Where(x => x.Services.Contains(service));
}
