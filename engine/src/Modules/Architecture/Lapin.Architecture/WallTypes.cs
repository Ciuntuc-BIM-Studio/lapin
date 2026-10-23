using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

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

    public static Entity? LayerSetOf(IModelView m, string typeId) => LayerSets.Of(m, typeId);

    public static IReadOnlyList<MaterialLayer> LayersOf(IModelView m, string typeId) => LayerSets.LayersOf(m, typeId);

    /// <summary>A lintel rule as a command gives it, checked.</summary>
    public static JsonObject ParseLintels(JsonNode node)
    {
        var rule = LintelRule.From(node) ?? throw new CommandException("The lintel rule is unreadable.");
        if (Lintels.Problems(rule) is { Count: > 0 } problems) throw new CommandException($"Lintels: {string.Join("; ", problems)}.");
        return rule.ToJson();
    }

    public static IReadOnlyList<LayerSpec> ParseLayers(JsonNode? node) => LayerSets.Parse(node, "A wall type");

    public static void Validate(IReadOnlyList<LayerSpec> layers) => LayerSets.Validate(layers, "A wall type");

    public static string DefaultName(IReadOnlyList<LayerSpec> layers)
    {
        var core = layers.FirstOrDefault(l => l.Core) ?? layers.MaxBy(l => l.Thickness)!;
        return $"{core.Material} wall {Math.Round(layers.Sum(l => l.Thickness) * 1000)}";
    }

    public static Entity Create(IEdit edit, string? name, IReadOnlyList<LayerSpec> layers, string? mark = null, JsonNode? zones = null, JsonNode? lintels = null)
    {
        Validate(layers);
        var typeName = string.IsNullOrWhiteSpace(name) ? DefaultName(layers) : name.Trim();
        var attrs = new JsonObject();
        if (mark != null) attrs["mark"] = mark;
        if (zones is JsonArray { Count: > 0 }) attrs["zones"] = WallZones.Parse(edit, zones);
        if (lintels != null) attrs["lintels"] = ParseLintels(lintels);
        var type = new Entity(edit.NewId(), IfcClass.WallType, typeName, attrs, "STANDARD");
        edit.Add(type);
        LayerSets.Attach(edit, type, layers);
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
    public static void Update(IEdit edit, Entity type, string? name, IReadOnlyList<LayerSpec>? layers, JsonNode? zones = null, JsonNode? lintels = null)
    {
        if (name != null)
        {
            var trimmed = name.Trim();
            if (trimmed.Length == 0) throw new CommandException("A type's name cannot be empty.");
            edit.Replace(type.With(name: trimmed));
        }
        if (lintels != null)
        {
            var current = edit.Model.Get(type.Id)!;
            var attrs = current.CloneAttrs();
            attrs["lintels"] = ParseLintels(lintels);
            edit.Replace(current.With(attrs: attrs));
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
        LayerSets.Rewrite(edit, edit.Model.Get(type.Id)!, layers);
        foreach (var wall in edit.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(w => w.Class == IfcClass.Wall).ToList())
            Walls.LayOut(edit, wall, type);
    }
}
