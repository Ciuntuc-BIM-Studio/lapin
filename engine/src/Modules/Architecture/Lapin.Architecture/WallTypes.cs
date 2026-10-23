using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>A layer as a command gives it: a material by name, a thickness in metres, the core mark.</summary>
public sealed record LayerSpec(string Material, double Thickness, bool Core = false, string? Color = null, string? Name = null);

/// <summary>
/// Wall types: an IfcWallType carrying its IfcMaterialLayerSet (through an
/// IfcRelAssociatesMaterial). The walls of a type each lay its layers with
/// their own IfcMaterialLayerSetUsage, the axis on the core.
/// </summary>
public static class WallTypes
{
    /// <summary>The type new walls get when none is chosen: render, brick (the core), plaster.</summary>
    public static readonly IReadOnlyList<LayerSpec> DefaultLayers =
    [
        new("Render", 0.02),
        new("Brick", 0.25, Core: true),
        new("Plaster", 0.015),
    ];

    public static Entity? LayerSetOf(IModelView m, string typeId) =>
        m.RelatingOf(typeId, IfcClass.RelAssociatesMaterial) is { Class: IfcClass.MaterialLayerSet } set ? set : null;

    public static IReadOnlyList<MaterialLayer> LayersOf(IModelView m, string typeId) =>
        LayerSetOf(m, typeId) is { } set ? Layers.Read(set) : [];

    public static IReadOnlyList<LayerSpec> ParseLayers(JsonNode? node)
    {
        if (node is not JsonArray arr) throw new CommandException("A wall type needs its layers.");
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
        Validate(o);
        return o;
    }

    public static void Validate(IReadOnlyList<LayerSpec> layers)
    {
        if (layers.Count == 0) throw new CommandException("A wall type needs at least one layer.");
        if (layers.Any(l => !(l.Thickness > 0))) throw new CommandException("Every layer needs a thickness.");
        if (layers.Count(l => l.Core) > 1) throw new CommandException("Only one layer can be the core.");
    }

    static List<MaterialLayer> Resolve(IEdit edit, IReadOnlyList<LayerSpec> layers) =>
        layers.Select(l => new MaterialLayer(Materials.Ensure(edit, l.Material, l.Color).Id, l.Thickness, l.Core, l.Name)).ToList();

    public static string DefaultName(IReadOnlyList<LayerSpec> layers)
    {
        var core = layers.FirstOrDefault(l => l.Core) ?? layers.MaxBy(l => l.Thickness)!;
        return $"{core.Material} wall {Math.Round(layers.Sum(l => l.Thickness) * 1000)}";
    }

    public static Entity Create(IEdit edit, string? name, IReadOnlyList<LayerSpec> layers, string? mark = null, JsonNode? zones = null)
    {
        Validate(layers);
        var typeName = string.IsNullOrWhiteSpace(name) ? DefaultName(layers) : name.Trim();
        var set = new Entity(edit.NewId(), IfcClass.MaterialLayerSet, typeName, Layers.Write(Resolve(edit, layers)));
        edit.Add(set);
        var attrs = new JsonObject();
        if (mark != null) attrs["mark"] = mark;
        if (zones is JsonArray { Count: > 0 }) attrs["zones"] = WallZones.Parse(edit, zones);
        var type = new Entity(edit.NewId(), IfcClass.WallType, typeName, attrs, "STANDARD");
        edit.Add(type);
        edit.Relate(IfcClass.RelAssociatesMaterial, set.Id, type.Id);
        return type;
    }

    /// <summary>The first wall type of the project, or the default one, made on first use.</summary>
    public static Entity EnsureDefault(IEdit edit) =>
        edit.Model.OfClass(IfcClass.WallType).FirstOrDefault() ?? Create(edit, null, DefaultLayers);

    /// <summary>
    /// A changed type: its name, and its layers rewritten in its own layer set.
    /// Every wall of the type keeps its axis on the (new) core, mirrored for a
    /// flipped wall.
    /// </summary>
    public static void Update(IEdit edit, Entity type, string? name, IReadOnlyList<LayerSpec>? layers, JsonNode? zones = null)
    {
        if (name != null)
        {
            var trimmed = name.Trim();
            if (trimmed.Length == 0) throw new CommandException("A type's name cannot be empty.");
            edit.Replace(type.With(name: trimmed));
        }
        if (zones != null)
        {
            var current = edit.Model.Get(type.Id)!;
            var attrs = current.CloneAttrs();
            var parsed = WallZones.Parse(edit, zones);
            if (parsed.Count == 0) attrs.Remove("zones"); else attrs["zones"] = parsed;
            edit.Replace(current.With(attrs: attrs));
        }
        if (layers == null) return;
        Validate(layers);
        var resolved = Resolve(edit, layers);
        if (LayerSetOf(edit.Model, type.Id) is { } set) edit.Replace(set.With(attrs: Layers.Write(resolved, set.CloneAttrs())));
        else
        {
            var made = new Entity(edit.NewId(), IfcClass.MaterialLayerSet, type.Name, Layers.Write(resolved));
            edit.Add(made);
            edit.Relate(IfcClass.RelAssociatesMaterial, made.Id, type.Id);
        }
        foreach (var wall in edit.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(w => w.Class == IfcClass.Wall).ToList())
            Walls.LayOut(edit, wall, type);
    }
}
