using System.Text.Json.Nodes;

namespace Lapin.Platform;

// What the engine sends the interface. The shapes match the BubbleBIM Pro
// protocol (ui/src/platform/types.ts there), so the same client reads both:
// an entity travels as a node, its IFC class as the node's type.

/// <summary>An entity as the interface sees it.</summary>
public sealed record NodeDto(string Id, string Type, string Name, string? ParentId, double Px, double Py, JsonObject Props, bool Editable, string? Module, string? StoreyId);

/// <summary>Unused by Lapin (its relationships travel as <see cref="RelationDto"/>); kept for the protocol's shape.</summary>
public sealed record EdgeDto(string Id, string From, string To, string Type, int FromGrip, int ToGrip);

/// <summary>An IFC relationship: one relating entity, the entities it relates.</summary>
public sealed record RelationDto(string Id, string Class, string Relating, IReadOnlyList<string> Related);

public sealed record PlanItemDto(string Kind, bool Closed, double[] Points, string? Text = null);

public sealed record RenderPart(string Name, string Color, float[] Positions, float[] Normals, int[] Indices, float[] Edges, string? Material = null);

public sealed record RenderItem(
    string NodeId,
    string Type,
    string? StoreyId,
    bool Editable,
    string Color,
    float[] Positions,
    float[] Normals,
    int[] Indices,
    float[] Edges,
    IReadOnlyList<PlanItemDto> Plan,
    double[] Snaps,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<RenderPart>? Parts = null);

public sealed record DocState(bool CanUndo, bool CanRedo, string? UndoLabel, string? RedoLabel, bool Dirty, string? Path, int NodeCount, int EdgeCount);

public sealed record Diff(
    long Version,
    bool Reset,
    IReadOnlyList<NodeDto> Nodes,
    IReadOnlyList<string> RemovedNodes,
    IReadOnlyList<EdgeDto> Edges,
    IReadOnlyList<string> RemovedEdges,
    IReadOnlyList<RenderItem> Render,
    IReadOnlyList<string> RenderRemoved,
    DocState State,
    IReadOnlyList<RelationDto> Relations,
    IReadOnlyList<string> RemovedRelations);

public sealed record ModuleInfo(string Id, string Name, int SchemaVersion);

public sealed record NodeTypeInfo(string Type, string Label, string Module, string Color, string GraphColor, string Category, string Description, bool Placeable, IReadOnlyList<FieldDescriptor> Fields, IReadOnlyList<object> Relations, bool Reference);

public sealed record CommandInfo(string Name, string Label);

/// <summary>A product: an edition, the modules it is made of (UI and engine alike).</summary>
public sealed record ProductManifest(string Id, string Name, string[] Modules, string[]? Extensions = null);

public sealed record SessionInfo(
    ProductManifest Product,
    IReadOnlyList<ModuleInfo> Modules,
    IReadOnlyList<object> Categories,
    IReadOnlyList<NodeTypeInfo> NodeTypes,
    IReadOnlyList<object> EdgeTypes,
    IReadOnlyList<CommandInfo> Commands,
    string Units,
    string EngineVersion,
    IReadOnlyDictionary<string, JsonNode> Catalogs);
