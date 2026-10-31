using System.Text.Json.Nodes;

namespace Lapin.Model;

/// <summary>
/// One layer of an IfcMaterialLayerSet: its material (an IfcMaterial id),
/// thickness in metres, and whether it is the structural core, which carries
/// the element's axis (IFC: Category "LoadBearing"); and how high it goes (report #44,
/// decision 191): <see cref="From"/> and <see cref="To"/> over the element's base, each null for
/// the whole height (a tiled dado to 1.50 m, a cladding stopping short of the floor).
/// </summary>
public sealed record MaterialLayer(string Material, double Thickness, bool Core = false, string? Name = null, double? From = null, double? To = null);

/// <summary>
/// An IfcMaterialLayerSetUsage: how an occurrence lays its type's layers
/// across its axis. Layers run along the axis's left normal (AXIS2) when the
/// sense is POSITIVE, the first starting <see cref="Offset"/> from the axis
/// (OffsetFromReferenceLine); NEGATIVE runs them the other way, from the
/// mirrored offset: a wall flipped across its axis.
/// </summary>
public sealed record LayerUsage(string ForLayerSet, bool Negative, double Offset)
{
    public string DirectionSense => Negative ? "NEGATIVE" : "POSITIVE";
}

/// <summary>Where an element's layers lie across its axis.</summary>
public readonly record struct LayerSection(double Thickness, double BodyOffset, double LayerOffset);

/// <summary>
/// Lapin's layer rules, the same for a wall and a foundation's stem: the axis
/// runs through the middle of the core (the layer marked so, else the
/// thickest), so the finishes on either face never move it. Layers are listed
/// from the face right of the drawn direction to the left face.
/// </summary>
public static class Layers
{
    /// <summary>The core: the layer marked so, else the thickest (the first of equals).</summary>
    public static int CoreIndex(IReadOnlyList<MaterialLayer> layers)
    {
        for (var i = 0; i < layers.Count; i++) if (layers[i].Core) return i;
        var best = 0;
        for (var i = 1; i < layers.Count; i++) if (layers[i].Thickness > layers[best].Thickness) best = i;
        return best;
    }

    /// <summary>
    /// The build-up's thickness; the body's shift to the left of a centred
    /// element; and the first layer's offset from the axis
    /// (OffsetFromReferenceLine), so that the core is centred on the axis.
    /// </summary>
    public static LayerSection Section(IReadOnlyList<MaterialLayer> layers)
    {
        if (layers.Count == 0) return default;
        var thickness = layers.Sum(l => l.Thickness);
        var core = CoreIndex(layers);
        var coreCentre = layers.Take(core).Sum(l => l.Thickness) + layers[core].Thickness / 2;
        return new(thickness, thickness / 2 - coreCentre, -coreCentre);
    }

    /// <summary>The usage's OffsetFromReferenceLine for an element with its axis on its core, mirrored for one running the negative way.</summary>
    public static double CoreOffset(IReadOnlyList<MaterialLayer> layers, bool negative) =>
        layers.Count == 0 ? 0 : (negative ? -1 : 1) * Section(layers).LayerOffset;

    /// <summary>
    /// The layers' bands across the axis (left of it positive), first to last:
    /// from the usage's offset, one way or the other.
    /// </summary>
    public static IReadOnlyList<(double From, double To)> Bands(IReadOnlyList<MaterialLayer> layers, LayerUsage usage)
    {
        var o = new List<(double, double)>(layers.Count);
        var at = usage.Offset;
        var sign = usage.Negative ? -1 : 1;
        foreach (var l in layers)
        {
            var next = at + sign * l.Thickness;
            o.Add((Math.Min(at, next), Math.Max(at, next)));
            at = next;
        }
        return o;
    }

    public static IReadOnlyList<MaterialLayer> Read(Entity layerSet) =>
        layerSet.Get("layers", ModelJson.Default.ListMaterialLayer) ?? [];

    public static JsonObject Write(IEnumerable<MaterialLayer> layers, JsonObject? into = null)
    {
        var o = into ?? [];
        o["layers"] = System.Text.Json.JsonSerializer.SerializeToNode(layers.ToList(), ModelJson.Default.ListMaterialLayer);
        return o;
    }

    public static LayerUsage? ReadUsage(Entity usage) =>
        usage.Str("forLayerSet") is { } set ? new LayerUsage(set, usage.Str("directionSense") == "NEGATIVE", usage.Num("offsetFromReferenceLine")) : null;

    public static JsonObject WriteUsage(LayerUsage u) => new()
    {
        ["forLayerSet"] = u.ForLayerSet,
        ["layerSetDirection"] = "AXIS2",
        ["directionSense"] = u.DirectionSense,
        ["offsetFromReferenceLine"] = u.Offset,
    };
}
