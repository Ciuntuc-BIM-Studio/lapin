using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>A masonry zone's block: its length along the wall, its course height and the joint, metres.</summary>
public sealed record Block(double Length, double Height, double Joint);

/// <summary>
/// A zone in a wall's height (docs/pereti-compusi-si-executie.md): a first
/// course, the masonry, a ring beam. Its height is fixed, or the rest of
/// the wall's (one zone at most). Its material is an IfcMaterial id.
/// </summary>
public sealed record WallZone(string Key, string Name, string Role, double? Height, string Material, Block? Block = null, double? Rebar = null)
{
    public bool Rest => Height == null;
}

/// <summary>A zone placed on a wall: from <see cref="Z0"/> to <see cref="Z1"/> above its base.</summary>
public sealed record ZoneBand(WallZone Zone, double Z0, double Z1);

/// <summary>
/// Walls composed in their height as well as in their thickness. The zones
/// belong to the wall type; they divide the core only, the finish layers
/// run the whole height. A zone is a part of the wall (an
/// IfcBuildingElementPart, or an IfcBeam RING_BEAM for a ring beam) worked out
/// from the wall, so it follows its joins and its edits.
/// </summary>
public static class WallZones
{
    public static readonly string[] Roles = ["masonry", "concrete", "ringBeam"];

    /// <summary>A first course of AAC blocks, the masonry in the core's material, a 25 cm ring beam (the old Lapin's defaults).</summary>
    public static IReadOnlyList<JsonObject> Defaults(string coreMaterial) =>
    [
        new() { ["key"] = "first-course", ["name"] = "First course", ["role"] = "masonry", ["height"] = 0.25, ["material"] = "AAC block", ["color"] = "#e6e1d6", ["block"] = new JsonObject { ["length"] = 0.6, ["height"] = 0.25, ["joint"] = 0.003 } },
        new() { ["key"] = "masonry", ["name"] = "Masonry", ["role"] = "masonry", ["height"] = "rest", ["material"] = coreMaterial, ["block"] = new JsonObject { ["length"] = 0.375, ["height"] = 0.238, ["joint"] = 0.01 } },
        new() { ["key"] = "ring-beam", ["name"] = "Ring beam", ["role"] = "ringBeam", ["height"] = 0.25, ["material"] = "Reinforced concrete", ["color"] = "#a8a8a8", ["rebar"] = 100 },
    ];

    /// <summary>The zones of a type, as stored; none: not zoned.</summary>
    public static IReadOnlyList<WallZone> Of(Entity type) =>
        type.Attrs["zones"] is JsonArray arr ? arr.OfType<JsonObject>().Select(Read).ToList() : [];

    static WallZone Read(JsonObject o) => new(
        (string?)o["key"] ?? "", (string?)o["name"] ?? "", (string?)o["role"] ?? "masonry",
        Json.Number(o["height"]), (string?)o["material"] ?? "",
        o["block"] is JsonObject b ? new Block(Json.Number(b["length"]) ?? 0, Json.Number(b["height"]) ?? 0, Json.Number(b["joint"]) ?? 0) : null,
        Json.Number(o["rebar"]));

    static JsonObject Write(WallZone z)
    {
        var o = new JsonObject { ["key"] = z.Key, ["name"] = z.Name, ["role"] = z.Role, ["height"] = z.Height is { } h ? h : "rest", ["material"] = z.Material };
        if (z.Block is { } b) o["block"] = new JsonObject { ["length"] = b.Length, ["height"] = b.Height, ["joint"] = b.Joint };
        if (z.Rebar is { } r) o["rebar"] = r;
        return o;
    }

    /// <summary>
    /// Zones as a command gives them (material by name, height a number or
    /// "rest"), checked, their materials made when the project has none.
    /// An empty list takes the zones away.
    /// </summary>
    public static JsonArray Parse(IEdit edit, JsonNode? node)
    {
        if (node is not JsonArray arr) throw new CommandException("Zones are a list, bottom to top.");
        var zones = new List<WallZone>();
        foreach (var n in arr)
        {
            if (n is not JsonObject o) throw new CommandException("Every zone is an object.");
            var name = ((string?)o["name"])?.Trim();
            if (string.IsNullOrEmpty(name)) throw new CommandException("Every zone needs a name.");
            var key = ((string?)o["key"])?.Trim() is { Length: > 0 } k ? k : Slug(name);
            var role = (string?)o["role"] ?? "masonry";
            if (!Roles.Contains(role)) throw new CommandException($"Zone „{name}”: its role is masonry, concrete or ringBeam.");
            var rest = o["height"] is JsonValue hv && hv.TryGetValue<string>(out var hs) && hs == "rest";
            double? height = rest ? null : Json.Number(o["height"]) is { } h && h > 0 ? h : throw new CommandException($"Zone „{name}” needs a positive height (or the rest).");
            var material = ((string?)o["material"])?.Trim();
            if (string.IsNullOrEmpty(material)) throw new CommandException($"Zone „{name}” needs a material.");
            Block? block = null;
            if (o["block"] is JsonObject b)
            {
                block = new Block(Json.Number(b["length"]) ?? 0, Json.Number(b["height"]) ?? 0, Json.Number(b["joint"]) ?? 0);
                if (!(block.Length > 0 && block.Height > 0 && block.Joint >= 0)) throw new CommandException($"Zone „{name}”: a block needs a length and a height, and a joint not below zero.");
            }
            var rebar = Json.Number(o["rebar"]);
            if (rebar is < 0) throw new CommandException($"Zone „{name}”: the reinforcement cannot be negative.");
            zones.Add(new WallZone(key, name, role, height, Materials.Ensure(edit, material, (string?)o["color"]).Id, block, rebar));
        }
        if (zones.Select(z => z.Key).Distinct().Count() != zones.Count) throw new CommandException("Zone keys (names) must be unique.");
        if (zones.Count(z => z.Rest) > 1) throw new CommandException("Only one zone can take the rest of the height.");
        return new JsonArray(zones.Select(z => (JsonNode)Write(z)).ToArray());
    }

    static string Slug(string name) => string.Concat(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');

    /// <summary>
    /// The zones on a wall <paramref name="height"/> tall, bottom to top: fixed
    /// heights, the rest to the rest zone (else to the topmost masonry zone),
    /// nothing past the top; a zone left with no height is dropped.
    /// </summary>
    public static IReadOnlyList<ZoneBand> Bands(IReadOnlyList<WallZone> zones, double height)
    {
        var fixedSum = zones.Sum(z => z.Height ?? 0);
        var left = Math.Max(0, height - fixedSum);
        var taker = zones.ToList().FindIndex(z => z.Rest);
        if (taker < 0) taker = zones.ToList().FindLastIndex(z => z.Role == "masonry");
        var o = new List<ZoneBand>();
        var at = 0.0;
        for (var i = 0; i < zones.Count; i++)
        {
            var h = (zones[i].Height ?? 0) + (i == taker ? left : 0);
            var top = Math.Min(at + h, height);
            if (top - at > 1e-9) o.Add(new ZoneBand(zones[i], at, top));
            at = Math.Max(at, top);
        }
        return o;
    }

    /// <summary>
    /// A zone part's quantities: Qto_BeamBaseQuantities for a ring beam,
    /// Lapin_WallZoneQuantities otherwise. <paramref name="netVolume"/> comes
    /// from the part itself (its openings out); blocks and mortar for a masonry
    /// zone with a block, the reinforcement for concrete with a ratio.
    /// </summary>
    public static QuantitySet Quantities(WallZone zone, double z0, double z1, double length, double thickness, double netVolume)
    {
        var height = z1 - z0;
        var gross = length * height * thickness;
        var key = "zone:" + zone.Key;
        if (zone.Role == "ringBeam")
        {
            var q = new List<Quantity>
            {
                new("Length", length, "LENGTH"),
                new("CrossSectionArea", height * thickness, "AREA", "Cross section"),
                new("GrossVolume", gross, "VOLUME", "Gross volume"),
                new("NetVolume", netVolume, "VOLUME", "Net volume"),
            };
            if (zone.Rebar is > 0) q.Add(new("ReinforcementWeight", netVolume * zone.Rebar.Value, "WEIGHT", "Reinforcement"));
            return new QuantitySet("Qto_BeamBaseQuantities", q, key, zone.Name, zone.Material);
        }
        var netSideArea = thickness > 0 ? netVolume / thickness : 0;
        var o = new List<Quantity>
        {
            new("Length", length, "LENGTH"),
            new("Height", height, "LENGTH"),
            new("Width", thickness, "LENGTH"),
            new("GrossSideArea", length * height, "AREA", "Gross side area"),
            new("NetSideArea", netSideArea, "AREA", "Net side area"),
            new("GrossVolume", gross, "VOLUME", "Gross volume"),
            new("NetVolume", netVolume, "VOLUME", "Net volume"),
        };
        if (zone.Role == "masonry" && zone.Block is { Length: > 0, Height: > 0 } u)
        {
            var blocks = Math.Ceiling(netSideArea / ((u.Length + u.Joint) * (u.Height + u.Joint)) - 1e-9);
            o.Add(new("BlockCount", blocks, "COUNT", "Blocks"));
            o.Add(new("MortarVolume", Math.Max(0, netVolume - blocks * u.Length * u.Height * thickness), "VOLUME", "Mortar"));
        }
        if (zone.Role == "concrete" && zone.Rebar is > 0) o.Add(new("ReinforcementWeight", netVolume * zone.Rebar.Value, "WEIGHT", "Reinforcement"));
        return new QuantitySet("Lapin_WallZoneQuantities", o, key, zone.Name, zone.Material);
    }
}
