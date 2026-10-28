namespace Lapin.Mep;

/// <summary>A pipe, duct or cable material a run may be of, and the services it carries; its laying rules come with mep-pro (<see cref="ILayingRules"/>).</summary>
public sealed record RunMaterial(string Id, string Label, string[] Services, string[]? Aliases = null);

/// <summary>The materials runs are made of, by service; the system's default is the usual one (multilayer pipe, PVC drain, galvanised duct, cable).</summary>
public static class RunMaterials
{
    public static readonly IReadOnlyList<RunMaterial> All =
    [
        new("steel", "Black steel", [Services.Heating], ["Steel"]),
        new("copper", "Copper", [Services.Heating, Services.ColdWater, Services.HotWater]),
        new("pex-al-pex", "PEX-AL-PEX multilayer", [Services.Heating, Services.ColdWater, Services.HotWater], ["PEX", "Pexal"]),
        new("pp-r", "PP-R", [Services.ColdWater, Services.HotWater]),
        new("pvc", "PVC drainage", [Services.Drainage], ["PVC"]),
        new("pp-drain", "PP drainage", [Services.Drainage]),
        new("galvanised", "Galvanised steel duct", [Services.SupplyAir, Services.ExtractAir], ["Galvanised steel"]),
        new("flex-duct", "Flexible duct", [Services.SupplyAir, Services.ExtractAir]),
        new("cable", "Cable in conduit", [Services.Electrical], ["Copper"]),
    ];

    public static RunMaterial? Get(string? id) =>
        id == null ? null : All.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase) || (x.Aliases?.Any(a => string.Equals(a, id, StringComparison.OrdinalIgnoreCase)) ?? false));

    /// <summary>A run's material: its own, its system's, else the first its service takes.</summary>
    public static RunMaterial Of(string service, string? id) => Get(id) is { } m && m.Services.Contains(service) ? m : Get(id) ?? All.First(x => x.Services.Contains(service));

    public static IEnumerable<RunMaterial> For(string service) => All.Where(x => x.Services.Contains(service));
}
