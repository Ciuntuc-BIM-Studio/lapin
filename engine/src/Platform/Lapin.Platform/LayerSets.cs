using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A layer as a command gives it: a material by name, a thickness in metres, the core mark.</summary>
public sealed record LayerSpec(string Material, double Thickness, bool Core = false, string? Color = null, string? Name = null);

/// <summary>
/// Layered types, whatever they are (a wall, a slab, a foundation's stem):
/// the type carries an IfcMaterialLayerSet through an IfcRelAssociatesMaterial;
/// its occurrences lay it with their own IfcMaterialLayerSetUsage. The
/// platform's, since several modules build on it.
/// </summary>
public static class LayerSets
{
    public static Entity? Of(IModelView m, string typeId) =>
        m.RelatingOf(typeId, IfcClass.RelAssociatesMaterial) is { Class: IfcClass.MaterialLayerSet } set ? set : null;

    public static IReadOnlyList<MaterialLayer> LayersOf(IModelView m, string typeId) =>
        Of(m, typeId) is { } set ? Layers.Read(set) : [];

    /// <summary>Layers as a command gives them (material by name, thickness, core), checked.</summary>
    public static IReadOnlyList<LayerSpec> Parse(JsonNode? node, string what = "A type")
    {
        if (node is not JsonArray arr) throw new CommandException($"{what} needs its layers.");
        var o = new List<LayerSpec>();
        foreach (var n in arr)
        {
            if (n is not JsonObject l) throw new CommandException("Every layer is an object: material and thickness.");
            var material = ((string?)l["material"])?.Trim();
            var thickness = Json.Number(l["thickness"]);
            if (string.IsNullOrEmpty(material)) throw new CommandException("Every layer needs a material.");
            if (thickness is not > 0) throw new CommandException("Every layer needs a thickness.");
            o.Add(new LayerSpec(material, thickness.Value, l["core"] is JsonValue c && c.TryGetValue<bool>(out var core) && core, (string?)l["color"], (string?)l["name"]));
        }
        Validate(o, what);
        return o;
    }

    public static void Validate(IReadOnlyList<LayerSpec> layers, string what = "A type")
    {
        if (layers.Count == 0) throw new CommandException($"{what} needs at least one layer.");
        if (layers.Any(l => !(l.Thickness > 0))) throw new CommandException("Every layer needs a thickness.");
        if (layers.Count(l => l.Core) > 1) throw new CommandException("Only one layer can be the core.");
    }

    /// <summary>The layers with their materials, made when the project has none of that name.</summary>
    public static List<MaterialLayer> Resolve(IEdit edit, IReadOnlyList<LayerSpec> layers) =>
        layers.Select(l => new MaterialLayer(Materials.Ensure(edit, l.Material, l.Color).Id, l.Thickness, l.Core, l.Name)).ToList();

    /// <summary>A new layer set, associated with the type.</summary>
    public static Entity Attach(IEdit edit, Entity type, IReadOnlyList<LayerSpec> layers)
    {
        var set = new Entity(edit.NewId(), IfcClass.MaterialLayerSet, type.Name, Layers.Write(Resolve(edit, layers)));
        edit.Add(set);
        edit.Relate(IfcClass.RelAssociatesMaterial, set.Id, type.Id);
        return set;
    }

    /// <summary>The type's layers rewritten in its own set (made when it has none).</summary>
    public static void Rewrite(IEdit edit, Entity type, IReadOnlyList<LayerSpec> layers)
    {
        var resolved = Resolve(edit, layers);
        if (Of(edit.Model, type.Id) is { } set) edit.Replace(set.With(attrs: Layers.Write(resolved, set.CloneAttrs())));
        else Attach(edit, type, layers);
    }

    /// <summary>
    /// The occurrence's own usage of the type's set: made when it has none,
    /// pointed at the set, its offset given by <paramref name="offset"/> (from
    /// the layers and whether it runs the negative way); none when the type has
    /// no layers.
    /// </summary>
    public static void LayOut(IEdit edit, Entity occurrence, Entity type, string direction, Func<IReadOnlyList<MaterialLayer>, bool, double> offset, bool? negativeWhenNew = null)
    {
        var set = Of(edit.Model, type.Id);
        var usage = UsageOf(edit.Model, occurrence.Id);
        if (set == null)
        {
            if (usage != null) edit.Remove(usage.Id);
            return;
        }
        var layers = Layers.Read(set);
        var negative = usage != null ? Layers.ReadUsage(usage) is { Negative: true } : negativeWhenNew ?? false;
        var attrs = Layers.WriteUsage(new LayerUsage(set.Id, negative, offset(layers, negative)));
        attrs["layerSetDirection"] = direction;
        if (usage == null)
        {
            var made = new Entity(edit.NewId(), IfcClass.MaterialLayerSetUsage, "", attrs);
            edit.Add(made);
            edit.Relate(IfcClass.RelAssociatesMaterial, made.Id, occurrence.Id);
        }
        else edit.Replace(usage.With(attrs: attrs));
    }

    /// <summary>An occurrence's own layer set usage, if it has one.</summary>
    public static Entity? UsageOf(IModelView m, string occurrenceId) =>
        m.RelatingOf(occurrenceId, IfcClass.RelAssociatesMaterial) is { Class: IfcClass.MaterialLayerSetUsage } u ? u : null;
}
