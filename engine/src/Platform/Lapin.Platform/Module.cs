using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A Lapin module (architecture, structure, civil, MEP…). The platform owns
/// the model, the spatial structure (project, site, building, storeys) and the
/// materials; a module owns the IFC classes it authors (IfcWall,
/// IfcWallType), the commands that make and change them, and how they look.
/// A product is a list of modules: what a product does not list is not in
/// its package at all.
/// </summary>
public interface ILapinModule
{
    /// <summary>Short id, the same as its folder and its UI half: "architecture".</summary>
    string Id { get; }
    string Name { get; }
    /// <summary>Version of the module's part of the project file.</summary>
    int SchemaVersion => 1;
    void Register(IModuleRegistry registry);
}

public interface IModuleRegistry
{
    /// <summary>An IFC class this module authors. A class has exactly one owner across the product.</summary>
    void Class(ClassDescriptor descriptor);

    /// <summary>A command, named under the module: "architecture.addWall". One undo step.</summary>
    void Command(string name, string label, CommandHandler handler);

    /// <summary>A question the module answers about the model: read-only, never an undo step.</summary>
    void Query(string name, QueryHandler handler);

    /// <summary>Builds the geometry of every entity of <paramref name="ifcClass"/>.</summary>
    void Geometry(string ifcClass, GeometryProducer producer);

    /// <summary>Data the module's interface reads (a catalogue of types), published as "module.name".</summary>
    void Catalog(string name, JsonNode data);

    /// <summary>A service other modules may use through its contract; they must work without it.</summary>
    void Service<T>(T implementation) where T : class;
}

public delegate JsonNode? CommandHandler(ICommandContext context, JsonObject args);

public delegate JsonNode? QueryHandler(IQueryContext context, JsonObject args);

/// <summary>An entity's geometry, read from the model (never stored in it).</summary>
public delegate ElementGeometry? GeometryProducer(Entity entity, IModelView model, IServices services);

public interface IServices
{
    T? Get<T>() where T : class;
    IReadOnlyList<T> All<T>() where T : class;
}

/// <summary>What a command can do: edit the model (one transaction) and use the services.</summary>
public interface ICommandContext : IEdit
{
    IServices Services { get; }
}

public interface IQueryContext
{
    IModelView Model { get; }
    IServices Services { get; }
    ElementGeometry? Geometry(string entityId);
}

/// <summary>How the inspector shows and edits one attribute. Lengths are metres in the engine; the UI shows mm.</summary>
public sealed record FieldDescriptor(
    string Key,
    string Label,
    string Kind = "number",          // number | length | angle | text | bool | choice | ref | color | table
    double? Min = null,
    double? Max = null,
    string[]? Choices = null,
    bool ReadOnly = false,
    IReadOnlyList<FieldDescriptor>? Columns = null);

/// <summary>An IFC class a module authors: its label, inspector fields and what kind of thing it is.</summary>
public sealed record ClassDescriptor(
    string Class,
    string Label,
    IReadOnlyList<FieldDescriptor> Fields,
    string Color = "#c9ced6",
    /// <summary>"element" (placed on a storey), "type", "resource".</summary>
    string Kind = "element");

/// <summary>One coloured part of an element: a wall's layer, a window's frame.</summary>
public sealed record GeometryPart(string Name, string Color, Solid Solid, string? Material = null);

/// <summary>Plan linework of an element: its cut outline, a symbol.</summary>
public sealed record PlanItem(string Kind, bool Closed, IReadOnlyList<Vec2> Points);

public sealed record ElementGeometry(IReadOnlyList<GeometryPart> Parts, IReadOnlyList<PlanItem>? Plan = null, IReadOnlyList<Vec2>? Snaps = null, IReadOnlyList<string>? Diagnostics = null);

/// <summary>A command refused for a reason the user should read.</summary>
public sealed class CommandException(string message) : ModelException(message);
