using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A path built of a hardscape type (docs/plan-site-grading.md, T4, the user's answer of 23.11.2026 to §6's first question): a
/// per-path choice, off by default. On, the path is related to a <see cref="Hardscapes.TypeClass"/> by IfcRelDefinesByType and its
/// carriageway's build-up is that type's layers, top down (<c>layer:i</c> parts in their materials, each layer's quantities as a
/// hardscape's), its IFC pavement kind the type's (RIGID, FLEXIBLE); a type edited, every path of it follows. Off, the relation goes
/// and the path is built of its own courses exactly as before, which it kept all along; a path that never had a type is as it was.
/// Its kerbs, gutters and sidewalks stay the path's own (its corridor's section).
/// </summary>
public static class PathTypes
{
    public const string LayerSet = "Lapin_PavementLayerQuantities";
    /// <summary>A path's own IFC pavement kind (every path's before it could take a type).</summary>
    public const string OwnPavement = "FLEXIBLE";

    public static string LayerKey(int i) => "layer:" + i;

    /// <summary>The hardscape type a path uses; null when it keeps its own courses.</summary>
    public static Entity? TypeOf(IModelView m, Entity e) => e.Class == IfcClass.Pavement ? Types.Of(m, e, Hardscapes.TypeClass) : null;

    /// <summary>The layers of the type a path uses, top down; null when it uses none.</summary>
    public static IReadOnlyList<MaterialLayer>? LayersOf(IModelView m, Entity e) => TypeOf(m, e) is { } t ? LayerSets.LayersOf(m, t.Id) : null;

    public static string LayerName(IModelView m, IReadOnlyList<MaterialLayer> layers, int i) => layers[i].Name ?? m.Get(layers[i].Material)?.Name ?? $"Layer {i + 1}";

    /// <summary>The material of a part of the path's section when it is one of its type's layers; null for any other part.</summary>
    public static string? LayerMaterial(IModelView m, Entity e, string key) =>
        key.StartsWith("layer:", StringComparison.Ordinal) && int.TryParse(key.AsSpan(6), out var i) && LayersOf(m, e) is { } layers && i >= 0 && i < layers.Count ? layers[i].Material : null;

    /// <summary>
    /// What a path command says of its type: <c>useHardscapeType: false</c> back to its own courses; a type (<c>hardscapeTypeId</c>,
    /// or <c>hardscapeType</c>: a ready-made one's key or a type's name) or <c>useHardscapeType: true</c> (the one it had, else the
    /// project's first, else the pavers) to build it of that type. Nothing said, nothing changes.
    /// </summary>
    public static void Apply(ICommandContext ctx, JsonObject a, string id)
    {
        bool? use = a["useHardscapeType"] is JsonValue v && v.TryGetValue<bool>(out var on) ? on : null;
        var named = a["hardscapeTypeId"] != null || a["hardscapeType"] != null;
        if (use == null && !named) return;
        var e = Spatial.Require(ctx.Model, id, IfcClass.Pavement);
        if (use == false) { Clear(ctx, e); return; }
        var type = named
            ? Hardscapes.TypeArg(ctx, new JsonObject { ["typeId"] = a["hardscapeTypeId"]?.DeepClone(), ["type"] = a["hardscapeType"]?.DeepClone() })
            : TypeOf(ctx.Model, e) ?? Hardscapes.TypeArg(ctx, []);
        Set(ctx, e, type);
    }

    /// <summary>The path built of the type: related to it, its pavement kind the type's.</summary>
    public static void Set(ICommandContext ctx, Entity e, Entity type)
    {
        ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        Follow(ctx, ctx.Model.Get(e.Id)!, type);
    }

    /// <summary>The path back to its own courses: no type, its own pavement kind.</summary>
    public static void Clear(ICommandContext ctx, Entity e)
    {
        if (TypeOf(ctx.Model, e) != null) ctx.Unrelate(IfcClass.RelDefinesByType, e.Id);
        if (ctx.Model.Get(e.Id) is { } now && now.PredefinedType != OwnPavement) ctx.Replace(now.With(predefinedType: OwnPavement));
    }

    /// <summary>A path of a type its pavement kind changed in: the type's (its layers it reads from the type as it is).</summary>
    public static void Follow(ICommandContext ctx, Entity e, Entity type)
    {
        var kind = type.PredefinedType ?? "RIGID";
        if (e.PredefinedType != kind) ctx.Replace(e.With(predefinedType: kind));
    }

    public static void Register(IModuleRegistry r) => r.Service<INodeProps>(new Props());

    /// <summary>What the interface draws the path's choice from: the type it uses, its name and its layers.</summary>
    sealed class Props : INodeProps
    {
        public void Add(Entity e, IModelView m, JsonObject props)
        {
            if (TypeOf(m, e) is not { } t) return;
            props["hardscapeTypeId"] = t.Id;
            props["hardscapeTypeName"] = t.Name;
            props["hardscapeLayers"] = new JsonArray([.. LayerSets.LayersOf(m, t.Id).Select(l => (JsonNode)new JsonObject { ["material"] = m.Get(l.Material)?.Name ?? l.Material, ["thickness"] = l.Thickness })]);
        }
    }
}
