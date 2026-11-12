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

/// <summary>A plan item for the interface: its kind, points (x, y, …), text; the levels of detail it is drawn at when not all (<see cref="PlanDetail"/>) and its dash pattern when it has one of its own;
/// a layered element's cut piece, its layer's subcategory (<see cref="PlanItem.Sub"/>).</summary>
public sealed record PlanItemDto(string Kind, bool Closed, double[] Points, string? Text = null, int? MinDetail = null, int? MaxDetail = null, string? Pattern = null, string? Sub = null);

/// <summary>
/// One part of an element's render. <see cref="HiddenBy"/>: the layers modes of a 3D view
/// (docs/view-styles.md) that leave it out, noFinishes and/or core (a wall's plaster: both;
/// its insulation: core); null when every mode shows it. Every part is a closed solid of its own,
/// so what is kept stays closed.
/// <para><see cref="Lods"/>: coarser copies for the screen (docs/plan-display-precision.md), finest first, each with the
/// most it strays from the part (m); the interface draws one when that is under a pixel or so. Only a part of many triangles has them.</para>
/// <para><see cref="Key"/>: the part's key (a layered element's <c>layer:&lt;n&gt;</c>), so a look may show one layer and not another (the 4D simulation).</para>
/// </summary>
public sealed record RenderPart(string Name, string Color, float[] Positions, float[] Normals, int[] Indices, float[] Edges, string? Material = null, string[]? HiddenBy = null, IReadOnlyList<RenderLod>? Lods = null, string? Key = null);

/// <summary>A coarser copy of a part (<see cref="Lapin.Geometry.MeshSimplify"/>): its triangles as three corners each (no normals or indices: shaded flat), its edges, the most it strays (m).</summary>
public sealed record RenderLod(float Error, float[] Positions, float[] Normals, int[] Indices, float[] Edges)
{
    public static IReadOnlyList<RenderLod>? Of(Lapin.Geometry.TriMesh m) =>
        Lapin.Geometry.MeshSimplify.Levels(m)?.Select(l => new RenderLod(l.Error, l.Mesh.Positions, l.Mesh.Normals, l.Mesh.Indices, l.Mesh.Edges)).ToList();
}

/// <summary>
/// A part's caps where a section cuts it in 3D (<see cref="Session.Caps"/>): <see cref="Part"/> is its index in the element's parts.
/// Drawn as on a sheet (the section's style), its cut <see cref="Fill"/> (white when the material has none) and its
/// <see cref="Hatch"/> as segments on the caps (x y z pairs), the material's section hatch at the drawing's scale.
/// </summary>
public sealed record CapPart(int Part, string Color, string? Material, float[] Positions, float[] Normals, int[] Indices, float[] Edges, string? Fill = null, float[]? Hatch = null);

/// <summary>An element's caps, part by part.</summary>
public sealed record CapItem(string NodeId, IReadOnlyList<CapPart> Parts);

/// <summary>An element's parts as a 3D view at LOD 400 draws them (<see cref="Session.FineBodies"/>), in place of its render's.</summary>
public sealed record FineBody(string NodeId, IReadOnlyList<RenderPart> Parts);

/// <summary>A 3D view's level of development and, from LOD 400, the bodies it draws finer than the render has them.</summary>
public sealed record FineBodies(int Lod, IReadOnlyList<FineBody> Items);

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
    IReadOnlyList<RenderPart>? Parts = null,
    RenderInstance? Instance = null,
    /// <summary>Lines in space with no body (CAD linework with heights): x y z pairs, a segment each six numbers, drawn in <see cref="Color"/>.</summary>
    float[]? Lines = null);

/// <summary>
/// An element drawn as an instance of a shared mesh (<see cref="SharedMesh"/>, docs/plan-instancing.md):
/// its item has no parts of its own; <see cref="Matrix"/> places the mesh, 12 numbers (the images of X, Y, Z and the origin).
/// </summary>
public sealed record RenderInstance(string Mesh, float[] Matrix);

/// <summary>A mesh many elements are drawn with, in its own coordinates, part by part.</summary>
public sealed record SharedMesh(string Id, IReadOnlyList<RenderPart> Parts, float[] Box);

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
    IReadOnlyList<string> RemovedRelations,
    IReadOnlyList<SharedMesh>? Meshes = null,
    IReadOnlyList<string>? MeshesRemoved = null)
{
    /// <summary>
    /// This reset followed by <paramref name="next"/> as one reset (a new project made from a file, then the file
    /// imported into it): what <paramref name="next"/> adds or changes over this, without what it removes.
    /// </summary>
    public Diff Then(Diff next)
    {
        if (!Reset || next.Reset) return next;
        static List<T> Over<T>(IEnumerable<T> first, IEnumerable<T> then, IEnumerable<string> removed, Func<T, string> id)
        {
            var gone = removed.ToHashSet();
            var o = new Dictionary<string, T>();
            var order = new List<string>();
            foreach (var x in first.Concat(then))
            {
                var k = id(x);
                if (!o.ContainsKey(k)) order.Add(k);
                o[k] = x;
            }
            return [.. order.Where(k => !gone.Contains(k)).Select(k => o[k])];
        }
        return new Diff(next.Version, true,
            Over(Nodes, next.Nodes, next.RemovedNodes, n => n.Id), [],
            Over(Edges, next.Edges, next.RemovedEdges, e => e.Id), [],
            Over(Render, next.Render, next.RenderRemoved, r => r.NodeId), [],
            next.State,
            Over(Relations, next.Relations, next.RemovedRelations, r => r.Id), [],
            Over(Meshes ?? [], next.Meshes ?? [], next.MeshesRemoved ?? [], m => m.Id), []);
    }
}

public sealed record ModuleInfo(string Id, string Name, int SchemaVersion);

public sealed record NodeTypeInfo(string Type, string Label, string Module, string Color, string GraphColor, string Category, string Description, bool Placeable, IReadOnlyList<FieldDescriptor> Fields, IReadOnlyList<object> Relations, bool Reference);

public sealed record CommandInfo(string Name, string Label);

/// <summary>A command or query another module wraps (docs/overrides.md): its kind ("command", "query"), its full id and the module that wraps it.</summary>
public sealed record OverrideInfo(string Kind, string Name, string Module);

/// <summary>
/// A product: an edition, the modules it is made of (UI and engine alike); what a new project of it starts with
/// (its storeys; none said: foundations, ground and first floor) and the disciplines its browser groups views by
/// (none said: architecture, structure, MEP) — a civil edition starts on one site level, its views civil.
/// </summary>
public sealed record ProductManifest(string Id, string Name, string[] Modules, string[]? Extensions = null, ProductStorey[]? Storeys = null, string[]? Disciplines = null);

/// <summary>A storey a new project of a product starts with: its name, base level and height (m).</summary>
public sealed record ProductStorey(string Name, double Elevation, double Height);

public sealed record SessionInfo(
    ProductManifest Product,
    IReadOnlyList<ModuleInfo> Modules,
    IReadOnlyList<object> Categories,
    IReadOnlyList<NodeTypeInfo> NodeTypes,
    IReadOnlyList<object> EdgeTypes,
    IReadOnlyList<CommandInfo> Commands,
    string Units,
    string EngineVersion,
    IReadOnlyDictionary<string, JsonNode> Catalogs,
    /// <summary>What the product's modules override (docs/overrides.md); null, written as nothing, when none.</summary>
    IReadOnlyList<OverrideInfo>? Overrides = null);
