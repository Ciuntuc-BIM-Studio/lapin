using System.Text.Json.Nodes;
using Lapin.Geometry;
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

    /// <summary>What an entity of <paramref name="ifcClass"/> measures: its quantity sets, from its geometry and the model.</summary>
    void Quantities(string ifcClass, QuantityProducer producer);

    /// <summary>
    /// Entities of <paramref name="ifcClass"/> take part in cut priorities: where
    /// two overlap, the one of higher priority cuts the other (a column through
    /// a wall, a slab over its head). <paramref name="priority"/> is the
    /// class's (an entity's <c>cutPriority</c> attribute overrides it, among
    /// the classes above the one it would cut: a wall never cuts a wall);
    /// <paramref name="envelope"/> is the solid it cuts with, from its attributes.
    /// </summary>
    void Cutter(string ifcClass, int priority, EnvelopeProducer envelope);

    /// <summary>
    /// Entities of <paramref name="ifcClass"/> are path elements: along an axis
    /// (<c>start</c>, <c>end</c>), joined at corners, in line and in T with
    /// others of their class (<see cref="Joins"/>). <paramref name="frame"/>
    /// gives an entity's axis and its body's extent across it.
    /// </summary>
    void PathElement(string ifcClass, PathFrame frame);

    /// <summary>Data the module's interface reads (a catalogue of types), published as "module.name".</summary>
    void Catalog(string name, JsonNode data);

    /// <summary>A service other modules may use through its contract; they must work without it.</summary>
    void Service<T>(T implementation) where T : class;
}

public delegate JsonNode? CommandHandler(ICommandContext context, JsonObject args);

public delegate JsonNode? QueryHandler(IQueryContext context, JsonObject args);

/// <summary>An entity's geometry, read from the model (never stored in it).</summary>
public delegate ElementGeometry? GeometryProducer(Entity entity, IModelView model, IServices services);

/// <summary>The solid an entity cuts others with: its body's envelope, from its attributes (never from built geometry).</summary>
public delegate Solid? EnvelopeProducer(Entity entity, IModelView model);

/// <summary>Cut priorities, offered by the platform as a service: what cuts an entity.</summary>
/// <summary>The geometry producers of the product's classes, for a module that builds on another element's shape (a boolean's tools).</summary>
public interface IGeometryProducers
{
    GeometryProducer? Geometry(string ifcClass);
}

/// <summary>
/// A change a module makes to an element's geometry once it is built (a live
/// boolean: the target with its tools, a union's tools hidden). It reads the
/// model through the same tracking view, so what it reads rebuilds the element.
/// </summary>
public interface IGeometryFilter
{
    ElementGeometry? Apply(Entity entity, ElementGeometry geometry, IModelView model, IServices services);
}

/// <summary>Deleting what is not an entity of its own (one axis of a grid, selected as grid#tag): true when it was handled.</summary>
public interface IDeletion
{
    bool Delete(ICommandContext ctx, string id);
}

public interface ICutters
{
    /// <summary>The classes that take part in cutting, with the priority their module gives them.</summary>
    IReadOnlyDictionary<string, int> CutDefaults { get; }
    /// <summary>A class's priority in this project (the cut order, else the default); null when it takes no part.</summary>
    int? ClassPriority(string ifcClass, IModelView model);
    /// <summary>The envelopes of the entities of higher priority that reach into <paramref name="bounds"/> (the target's own, of its body).</summary>
    IReadOnlyList<Solid> For(Entity target, IModelView model, (Vec3 Min, Vec3 Max) bounds);
    /// <summary>An element's envelope as its class gives it (its body from its attributes, without the openings); null when its class cuts nothing.</summary>
    Solid? EnvelopeOf(Entity element, IModelView model) => null;
}

/// <summary>An entity's quantity sets, from its built geometry (null: none) and the model.</summary>
public delegate IEnumerable<QuantitySet> QuantityProducer(Entity entity, ElementGeometry? geometry, IModelView model);

/// <summary>One measured value: its IFC name (NetVolume), the value in SI units, and what kind it is.</summary>
public sealed record Quantity(string Name, double Value, string Kind, string? Label = null)
{
    /// <summary>The unit the value is in: m, m², m³, pcs, kg.</summary>
    public string Unit => Kind switch { "LENGTH" => "m", "AREA" => "m²", "VOLUME" => "m³", "COUNT" => "pcs", "WEIGHT" => "kg", _ => "" };
}

/// <summary>
/// A quantity set (IFC: an IfcElementQuantity), of the element itself or of
/// one of its parts (a zone, a finish layer: <see cref="Part"/> is its key).
/// </summary>
public sealed record QuantitySet(string Name, IReadOnlyList<Quantity> Quantities, string? Part = null, string? PartName = null, string? Material = null);

public interface IServices
{
    T? Get<T>() where T : class;
    IReadOnlyList<T> All<T>() where T : class;
}

/// <summary>What a command can do: edit the model (one transaction) and use the services.</summary>
public interface ICommandContext : IEdit
{
    IServices Services { get; }
    /// <summary>What a class is in this product (its fields), whichever module owns it; null when none does.</summary>
    ClassDescriptor? Describe(string ifcClass);
    /// <summary>The model's geometry as it was when the command began (read before editing): what a view draws now.</summary>
    IQueryContext Query { get; }
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

/// <summary>One coloured part of an element: a wall's layer or zone, a window's frame. <see cref="Key"/> is stable within the element ("layer:0", "zone:ring-beam").</summary>
public sealed record GeometryPart(string Name, string Color, Solid Solid, string? Material = null, string? Key = null);

/// <summary>Plan linework of an element: its cut outline, a symbol.</summary>
public sealed record PlanItem(string Kind, bool Closed, IReadOnlyList<Vec2> Points);

public sealed record ElementGeometry(IReadOnlyList<GeometryPart> Parts, IReadOnlyList<PlanItem>? Plan = null, IReadOnlyList<Vec2>? Snaps = null, IReadOnlyList<string>? Diagnostics = null, SharedInstance? Instance = null);

/// <summary>
/// An element's parts are a shared body's (<see cref="StoredBodies"/>), placed by
/// <see cref="Xf"/>: the interface draws one mesh for all of them (docs/plan-instancing.md).
/// </summary>
public sealed record SharedInstance(string Body, double[] Xf);

/// <summary>A command refused for a reason the user should read.</summary>
public sealed class CommandException(string message) : ModelException(message);

/// <summary>
/// How geometry is built when a reader wants something other than what the
/// views show: an IFC export wants the bodies the openings have not cut, as
/// IFC writes the openings and subtracts them itself. Offered to producers
/// through <see cref="IServices"/>; absent, the views' geometry.
/// </summary>
public sealed record GeometryOptions(bool CutOpenings = true);

/// <summary>The body of a void (an IfcOpeningElement) in its host, for whoever writes it out; a service of the module that owns the host.</summary>
public interface IVoidShapes
{
    Solid? Shape(Entity opening, IModelView model);
}

/// <summary>A table a module offers as a schedule: its title, its columns and its rows (text).</summary>
public sealed record ScheduleGrid(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>A schedule another module fills (a planting schedule): the kind a schedule entity names, its label, and the table as the model is now.</summary>
public interface IScheduleSource
{
    string Kind { get; }
    string Label { get; }
    ScheduleGrid Grid(IQueryContext query);
}
