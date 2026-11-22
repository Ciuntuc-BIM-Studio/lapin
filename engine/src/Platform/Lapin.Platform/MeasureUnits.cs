namespace Lapin.Platform;

/// <summary>
/// The one reading of a unit of measure (a price list's, a productivity norm's) and of the quantity names a measure is
/// read by, shared by the cost and planning modules so an estimate and a schedule measure an element alike.
/// </summary>
public static class MeasureUnits
{
    /// <summary>A unit's measure (volume, area, length, count, weight) and the factor the model's SI value is multiplied by to be in it; null when Lapin cannot measure it.</summary>
    public static (string Basis, double Factor)? BasisOf(string? unit)
    {
        var u = (unit ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace(".", "");
        return u switch
        {
            "m3" or "m³" or "mc" or "cum" or "cbm" => ("volume", 1),
            "m2" or "m²" or "mp" or "sqm" => ("area", 1),
            "100m2" or "100mp" or "100m²" => ("area", 0.01),
            "m" or "ml" or "lm" or "rm" => ("length", 1),
            "100m" or "100ml" => ("length", 0.01),
            "buc" or "pcs" or "pc" or "nr" or "no" or "u" or "ea" or "each" or "stk" or "szt" or "un" or "pz" or "piece" or "pièce" or "set" => ("count", 1),
            "kg" => ("weight", 1),
            "t" or "to" or "tonne" or "tona" or "tone" => ("weight", 0.001),
            _ => null,
        };
    }

    /// <summary>The unit a measure is shown in.</summary>
    public static string UnitOf(string basis) => basis switch { "volume" or "grossVolume" => "m³", "area" or "grossArea" => "m²", "length" => "m", "weight" => "kg", _ => "pcs" };
}

/// <summary>The quantities an estimate's or a schedule's measure is read from, in the order they are looked for (the first an element's set has wins); <see cref="QuantityNames"/> is the takeoff's and the formulas' common names.</summary>
public static class MeasureQuantities
{
    static readonly Dictionary<string, string[]> Names = new()
    {
        ["volume"] = ["NetVolume", "Volume", "GrossVolume"],
        ["grossVolume"] = ["GrossVolume", "NetVolume", "Volume"],
        ["area"] = ["NetSideArea", "NetArea", "NetFloorArea", "NetSurfaceArea", "ContactArea", "Area", "GrossSideArea", "GrossArea", "GrossFloorArea", "GrossSurfaceArea", "OuterSurfaceArea"],
        ["grossArea"] = ["GrossSideArea", "GrossArea", "GrossFloorArea", "GrossSurfaceArea", "ContactArea", "Area", "NetSideArea", "NetArea", "NetFloorArea", "NetSurfaceArea"],
        ["length"] = ["Length", "Perimeter", "FormedLength"],
        ["weight"] = ["NetWeight", "GrossWeight", "Weight"],
    };

    /// <summary>The names a measure reads (a measure Lapin does not name is a quantity's own name).</summary>
    public static IReadOnlyList<string> Of(string basis) => Names.TryGetValue(basis, out var n) ? n : [basis];

    /// <summary>The value of the first of the measure's names the quantities have; null when they have none.</summary>
    public static double? Pick(IReadOnlyList<Quantity> quantities, string basis)
    {
        foreach (var name in Of(basis)) if (quantities.FirstOrDefault(q => q.Name == name) is { } hit) return hit.Value;
        return null;
    }
}
