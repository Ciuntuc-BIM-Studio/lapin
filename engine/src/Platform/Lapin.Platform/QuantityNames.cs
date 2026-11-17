namespace Lapin.Platform;

/// <summary>
/// The common names of a measure (area, perimeter, volume, weight) and the IFC quantities that give it on each class — a wall's
/// area is its NetSideArea, a room's its NetFloorArea, a slab's its NetArea, a door's its Area — the first the element has. The
/// quantity takeoff's columns read them, and a name a formula, a tag, a schedule column, a filter or a table cell gives
/// (<c>Area</c>, <c>NetArea</c>, <c>area</c>, <c>Perimeter</c>, <c>Volume</c>) reads through them when the element has no quantity
/// of that very name (<see cref="ParamFormulas.Quantities"/>): one table, so a room's <c>{area}</c> is the stamp's and the room
/// schedule's area everywhere.
/// </summary>
public static class QuantityNames
{
    public static readonly string[] Length = ["Length"];
    public static readonly string[] GrossArea = ["GrossSideArea", "GrossArea", "GrossFloorArea", "GrossSurfaceArea", "Area"];
    public static readonly string[] NetArea = ["NetSideArea", "NetArea", "NetFloorArea", "NetSurfaceArea"];
    public static readonly string[] GrossVolume = ["GrossVolume"];
    public static readonly string[] NetVolume = ["NetVolume", "Volume"];
    public static readonly string[] Weight = ["NetWeight", "GrossWeight", "Weight"];
    public static readonly string[] Perimeter = ["NetPerimeter", "Perimeter", "GrossPerimeter"];

    /// <summary>
    /// What a common name stands for, in order (squashed names: case and spaces aside): an area is the net one, else the
    /// gross one (a door's Area is both); a net area the net one, else Area; a perimeter, a volume, a weight likewise.
    /// </summary>
    static readonly Dictionary<string, string[]> Common = new()
    {
        ["area"] = [.. NetArea, "Area", .. GrossArea],
        ["netarea"] = [.. NetArea, "Area"],
        ["grossarea"] = GrossArea,
        ["perimeter"] = Perimeter,
        ["netperimeter"] = Perimeter,
        ["grossperimeter"] = ["GrossPerimeter", "Perimeter"],
        ["volume"] = [.. NetVolume, .. GrossVolume],
        ["netvolume"] = NetVolume,
        ["grossvolume"] = [.. GrossVolume, "Volume"],
        ["weight"] = Weight,
    };

    /// <summary>The names offered in the fields' lists, as people write them.</summary>
    public static readonly string[] Offered = ["Area", "NetArea", "GrossArea", "Perimeter", "Volume", "NetVolume", "GrossVolume", "Weight"];

    /// <summary>The quantities <paramref name="name"/> stands for (squashed: case and spaces aside), the first an element has winning; empty when it is no common name.</summary>
    public static IReadOnlyList<string> Of(string name) => Common.TryGetValue(TagText.Squash(name), out var l) ? l : [];

    /// <summary>A common name read through <paramref name="byName"/> (an element's quantities by their squashed name): the first of its quantities it has.</summary>
    public static (string Name, double Value)? Resolve(string name, Func<string, double?> byName)
    {
        foreach (var q in Of(name))
            if (byName(TagText.Squash(q)) is { } v) return (q, v);
        return null;
    }
}
