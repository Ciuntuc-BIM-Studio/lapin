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
    /// <paramref name="envelope"/> is the solid it cuts with, from its attributes; null: the class ranks in the cut order but
    /// cuts by its own means (an MEP run cuts provision openings, decision 293, read through <see cref="Coordination.IProvisionOpenings"/>).
    /// </summary>
    void Cutter(string ifcClass, int priority, EnvelopeProducer? envelope);

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

    /// <summary>
    /// Another module's command wrapped, by its full id ("architecture.addWall"), for an edition that replaces what Lapin does
    /// (docs/overrides.md): <paramref name="handler"/> may change the args, the result, or do the work itself; its
    /// <c>next</c> calls the command as it was (or the override loaded before this one). It runs inside the command's own
    /// transaction (one undo step), under its name, label and owner, so who may call it is as before. A command no module of
    /// the product registers is refused when the product loads.
    /// </summary>
    void Override(string command, CommandOverride handler);

    /// <summary>Another module's query wrapped, by its full id ("core.faceAt"), as <see cref="Override"/> wraps a command.</summary>
    void OverrideQuery(string query, QueryOverride handler);
}

public delegate JsonNode? CommandHandler(ICommandContext context, JsonObject args);

public delegate JsonNode? QueryHandler(IQueryContext context, JsonObject args);

/// <summary>A command's override: the context and args the command got, and <paramref name="next"/>, the command as it was.</summary>
public delegate JsonNode? CommandOverride(ICommandContext context, JsonObject args, Func<JsonObject, JsonNode?> next);

/// <summary>A query's override: the context and args the query got, and <paramref name="next"/>, the query as it was.</summary>
public delegate JsonNode? QueryOverride(IQueryContext context, JsonObject args, Func<JsonObject, JsonNode?> next);

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

/// <summary>
/// What the interface is told of an entity besides its attributes: derived, never stored, worked out each time the
/// entity is sent (a roof window's frame on its slope, so its grips stand on its edges). A failure leaves the props as they are.
/// </summary>
public interface INodeProps
{
    void Add(Entity entity, IModelView model, JsonObject props);
}

/// <summary>
/// An element standing on several storeys (a lift's shaft, docs/plan-elevators.md): contained in one, drawn by the plans
/// of the others it reaches too, with the symbols it has on each (its landing doors only where it stops).
/// </summary>
public interface IStoreySpans
{
    bool Handles(Entity e);
    /// <summary>Whether a plan of <paramref name="storey"/> draws it.</summary>
    bool On(IModelView m, Entity e, Entity storey);
    /// <summary>Its plan symbols on <paramref name="storey"/>; null: its geometry's own.</summary>
    IReadOnlyList<PlanItem>? PlanOn(IModelView m, Entity e, Entity storey) => null;
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
    /// <summary>An element's priority in this project: its own, its material's, its class's; null when it takes no part.</summary>
    int? PriorityOf(Entity e, IModelView model) => ClassPriority(e.Class, model);
    /// <summary>The envelopes of the entities of higher priority that reach into <paramref name="bounds"/> (the target's own, of its body); never of one joined to the target (its join settles their meeting).</summary>
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
    /// <summary>Where a result worked out in the background goes once ready (<see cref="IDeliveries"/>); null where there is no session.</summary>
    IDeliveries? Deliveries => null;
}

public interface IQueryContext
{
    IModelView Model { get; }
    IServices Services { get; }
    ElementGeometry? Geometry(string entityId);
    /// <summary>
    /// The element as a view at LOD 400 draws it (<see cref="ElementGeometry.FineShape"/>: a steel section's root fillets, toe
    /// radii and sloping flanges), built when first asked for; the element's own geometry when it has no finer shape.
    /// </summary>
    ElementGeometry? FineGeometry(string entityId) => Geometry(entityId);
    /// <summary>The project's file name without its extension ("House"), what the interface calls the project; null before it has a file.</summary>
    string? Title => null;
    /// <summary>The elements built from what <paramref name="id"/> is (its attributes, or its relationships): built again when it changes (<see cref="Dependencies"/>).</summary>
    IReadOnlyList<string> DependentsOf(string id) => [];
    /// <summary>What the element <paramref name="id"/> was built from: the entities its geometry read (its type, its host, the walls it joins).</summary>
    IReadOnlyList<string> DependenciesOf(string id) => [];
    /// <summary>What the entity <paramref name="id"/> measures (its quantity sets and its parts'), as the properties panel shows it; none when its class has no measures.</summary>
    IReadOnlyList<QuantitySet> Quantities(string id) => [];
    /// <summary>What a class is in this product (its label), whichever module owns it; null when none does.</summary>
    ClassDescriptor? Describe(string ifcClass) => null;
    /// <summary>Where a result worked out in the background goes once ready (<see cref="IDeliveries"/>); null where there is no session.</summary>
    IDeliveries? Deliveries => null;
}

/// <summary>
/// How the inspector shows and edits one attribute. Lengths are metres in the engine; the UI shows mm.
/// <see cref="ShowIf"/> (decision 333): when the field applies, as alternatives (any one holding shows it), each a set of
/// attributes and the values they may have (all must hold); a value written "!x" excludes x, a missing attribute is "".
/// A roof's deck shows as <c>[{shape: [flat, barrel]}, {structure: [none]}]</c>, its rafters as
/// <c>[{shape: [!flat, !barrel], structure: [!none]}]</c>. Null: always shown.
/// </summary>
public sealed record FieldDescriptor(
    string Key,
    string Label,
    string Kind = "number",          // number | length | angle | text | bool | choice | ref | color | table | material
    double? Min = null,
    double? Max = null,
    string[]? Choices = null,
    bool ReadOnly = false,
    IReadOnlyList<FieldDescriptor>? Columns = null,
    IReadOnlyList<IReadOnlyDictionary<string, string[]>>? ShowIf = null)
{
    /// <summary>Whether the field applies to an entity whose attributes read as <paramref name="value"/> (null or missing: "").</summary>
    public bool Shown(Func<string, string?> value) => ShowIf is not { Count: > 0 } any || any.Any(all => all.All(c => Holds(c.Value, value(c.Key) ?? "")));

    /// <summary>Whether the field applies to an entity, by its attributes (a string, a number or a bool, as the interface reads them).</summary>
    public bool Shown(Lapin.Model.Entity e) => Shown(k => e.Attrs[k] is System.Text.Json.Nodes.JsonValue v
        ? v.TryGetValue<string>(out var s) ? s : v.TryGetValue<bool>(out var b) ? (b ? "true" : "false") : v.ToJsonString() : null);

    static bool Holds(string[] values, string v)
    {
        var positives = values.Where(x => !x.StartsWith('!')).ToList();
        return (positives.Count == 0 || positives.Contains(v)) && !values.Any(x => x.StartsWith('!') && x[1..] == v);
    }

    /// <summary>A condition of one alternative: <paramref name="key"/> among <paramref name="values"/>.</summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, string[]>> When(string key, params string[] values) => [new Dictionary<string, string[]> { [key] = values }];
}

/// <summary>An IFC class a module authors: its label, inspector fields and what kind of thing it is.</summary>
public sealed record ClassDescriptor(
    string Class,
    string Label,
    IReadOnlyList<FieldDescriptor> Fields,
    string Color = "#c9ced6",
    /// <summary>"element" (placed on a storey), "type", "resource".</summary>
    string Kind = "element");

/// <summary>
/// One coloured part of an element: a wall's layer or zone, a window's frame. <see cref="Key"/> is stable within the element ("layer:0", "zone:ring-beam").
/// <para>
/// A part read from a file as a big triangle mesh keeps it as <see cref="Mesh"/> (its coordinates and triangles, world) and
/// makes its <see cref="Solid"/> only when a consumer asks for it (a plan, a section, an export): the 3D view draws the mesh
/// (docs/plan-instancing.md, "A real model"). A solid given anew (a boolean's cut) stands on its own: the mesh goes.
/// </para>
/// </summary>
public sealed record GeometryPart
{
    public string Name { get; init; }
    public string Color { get; init; }
    public string? Material { get; init; }
    public string? Key { get; init; }
    /// <summary>A part made of copies of one body (a track's sleepers): the body once, in its own frame, and where each copy stands; IFC writes it as a representation map.</summary>
    public Solid? Unit { get; init; }
    public IReadOnlyList<PartInstance>? Instances { get; init; }
    /// <summary>The part as a triangle mesh (x y z …; a b c …), when it came as one: drawn as such, its solid made on demand.</summary>
    public (double[] Xyz, int[] Tris)? Mesh { get; private init; }
    /// <summary>The part's own properties (a roof timber member's role, section, grade and length), written to IFC as the set <see cref="PropertySet"/>.</summary>
    public IReadOnlyList<(string Name, string Value)>? Properties { get; init; }
    public string? PropertySet { get; init; }
    /// <summary>A part plans cut as any other although its class draws by its symbols there (a lift's shaft walls round its car's symbol).</summary>
    public bool CutInPlan { get; init; }

    Solid? _solid;
    Func<Solid>? _make;

    public GeometryPart(string Name, string Color, Solid Solid, string? Material = null, string? Key = null, Solid? Unit = null, IReadOnlyList<PartInstance>? Instances = null)
    {
        this.Name = Name; this.Color = Color; _solid = Solid; this.Material = Material; this.Key = Key; this.Unit = Unit; this.Instances = Instances;
    }

    /// <summary>A part that is a triangle mesh: its solid made by <paramref name="solid"/> the first time it is asked for.</summary>
    public GeometryPart(string Name, string Color, (double[] Xyz, int[] Tris) mesh, Func<Solid> solid, string? Material = null, string? Key = null)
    {
        this.Name = Name; this.Color = Color; Mesh = mesh; _make = solid; this.Material = Material; this.Key = Key;
    }

    public Solid Solid
    {
        get
        {
            if (_solid != null) return _solid;
            lock (_make!) return _solid ??= _make();
        }
        init { _solid = value; _make = null; Mesh = null; }
    }

    /// <summary>Whether the solid is there (made, or given): a mesh part's is not until asked for.</summary>
    public bool HasSolid => _solid != null;

    /// <summary>The part's box: from its mesh, or its solid.</summary>
    public (Vec3 Min, Vec3 Max)? Bounds() => Mesh is { } m ? MeshShading.Bounds(m.Xyz) : Measure.Bounds(Solid);

    /// <summary>How many faces the part has, without making a mesh part's solid: its triangles.</summary>
    public int FaceCount => Mesh is { } m ? m.Tris.Length / 3 : Solid.Faces.Length;

    /// <summary>The part's render: a mesh drawn as one (shared vertices, smooth, crease edges), a solid tessellated face by face.</summary>
    public TriMesh Tessellate() => Mesh is { } m ? MeshShading.Smooth(m.Xyz, m.Tris) : Tessellator.Tessellate(Solid);
}

/// <summary>
/// Where one copy of a part's <see cref="GeometryPart.Unit"/> stands (a
/// track's sleepers: one box, thousands of poses): its origin and its unit
/// axes, world metres. The part's <see cref="GeometryPart.Solid"/> stays the
/// copies joined, for the views; the IFC export writes the unit once
/// (IfcRepresentationMap) and each copy as an IfcMappedItem.
/// </summary>
public sealed record PartInstance(Vec3 Origin, Vec3 X, Vec3 Y, Vec3 Z);

/// <summary>
/// Plan linework of an element: its cut outline, a symbol. It may be drawn only between two levels of detail
/// (<see cref="PlanDetail"/>: a plan's scale says which — a track is a thick axis at 1:2000, its two rails at 1:500,
/// every rail edge and sleeper at 1:100) and in a style of its own (a centre line's dash-dot). <see cref="Mode"/>: the
/// entourage mode it draws in (<see cref="ViewStyles.EntourageModes"/>: a person's, car's or plant's symbol); null, every one.
/// </summary>
public sealed record PlanItem(string Kind, bool Closed, IReadOnlyList<Vec2> Points, int MinDetail = PlanDetail.Coarse, int MaxDetail = PlanDetail.Fine, Lapin.Drawing.LineStyleSpec? Style = null, string? Mode = null)
{
    public bool At(int detail) => detail >= MinDetail && detail <= MaxDetail;
    /// <summary>Whether it draws in a view whose entourage mode is <paramref name="mode"/>.</summary>
    public bool In(string mode) => Mode == null || Mode == mode;
}

/// <summary>
/// How much a plan draws, by its scale: coarse at 1:1000 and smaller (an overview: axes), medium down to 1:200
/// (a site plan: outlines), fine at 1:200 and larger (a detail plan: every piece). Drawn symbols choose by it.
/// </summary>
public static class PlanDetail
{
    public const int Coarse = 0, Medium = 1, Fine = 2;
    /// <summary>The scales where the levels change (1 : n).</summary>
    public const double CoarseFrom = 1000, FineTo = 200;

    public static int Of(double scale) => scale >= CoarseFrom - 1e-9 ? Coarse : scale <= FineTo + 1e-9 ? Fine : Medium;

    /// <summary>
    /// The levels of development a view draws the building at (decision 404, after the LOD Specification's meaning; never by
    /// its scale): 100 the massing (an element one outline hatched as its core, steel members as their axes, a door or a
    /// window as its opening and swing); 200 the layers and the doors' and windows' frames and sashes, simplified; 300 the
    /// details of doors and windows (reveals, sills, surrounds, thresholds, boards, hardware, shutters) and the openings'
    /// linings; 350 the connections (steel nodes, fasteners); 400 the fabrication: a linked model's reinforcing bars. A view
    /// style gives its views theirs (350; a structural one 400), a view may set its own (<c>styleOverrides.detail</c>).
    /// </summary>
    public const int Lod100 = 100, Lod200 = 200, Lod300 = 300, Lod350 = 350, Lod400 = 400;

    public static readonly int[] Lods = [Lod100, Lod200, Lod300, Lod350, Lod400];

    public const string LodProblem = "A level of development is 100, 200, 300, 350 or 400.";

    /// <summary>A level as stored or given (100…400, "lod300", "300"; the old coarse, medium and fine of decision 394 as 100, 200, 400); null when it is none of them.</summary>
    public static int? LodOf(System.Text.Json.Nodes.JsonNode? n)
    {
        if (n is not System.Text.Json.Nodes.JsonValue v) return null;
        if (v.TryGetValue<string>(out var s))
        {
            s = s.Trim().ToLowerInvariant();
            switch (s) { case "coarse": return Lod100; case "medium": return Lod200; case "fine": return Lod400; }
            if (s.StartsWith("lod", StringComparison.Ordinal)) s = s[3..].Trim();
            return int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i) && Lods.Contains(i) ? i : null;
        }
        return Json.Number(v) is { } d && d == Math.Floor(d) && Lods.Contains((int)d) ? (int)d : null;
    }

    /// <summary>How much of a plan's drawn symbols a level draws (their <see cref="PlanItem.MinDetail"/>): 100 coarse, 200 medium, 300 and up fine.</summary>
    public static int SymbolsOf(int lod) => lod <= Lod100 ? Coarse : lod <= Lod200 ? Medium : Fine;

    /// <summary>The parts of doors and windows only a view at LOD 300 or more draws: sills, surrounds, thresholds, window boards, hardware, shutters, grilles, boxes, canopies.</summary>
    static readonly string[] JoineryDetails = ["sill", "surround", "threshold", "board", "hardware", "shutters", "grille", "windowbox", "canopy"];

    /// <summary>
    /// Whether a part is a detail a view under LOD 300 leaves out (decisions 401, 404): a door's or a window's reveals and the
    /// pieces round it (<see cref="JoineryDetails"/>), an opening's lining.
    /// </summary>
    /// <summary>
    /// Whether a view at a level draws a part (decisions 401, 404, 405): the details from LOD 300 (<see cref="IsDetailPart"/>);
    /// a door's or a window's glazing bars and handles from LOD 300 too, its sashes from LOD 200 (at LOD 100 a door or a
    /// window is its frame, its leaf and its glass: the opening); any other part at every level.
    /// </summary>
    public static bool Shows(string ifcClass, string? key, int lod)
    {
        if (lod >= Lod300 || key == null) return true;
        if (IsDetailPart(ifcClass, key)) return false;
        if (ifcClass is not ("IfcDoor" or "IfcWindow")) return true;
        if (key is "muntin" or "handle") return false;
        return !(lod <= Lod100 && key == "sash");
    }

    public static bool IsDetailPart(string ifcClass, string? key)
    {
        if (key == null) return false;
        if (key.StartsWith("lining:", StringComparison.Ordinal)) return true;
        if (ifcClass is not ("IfcDoor" or "IfcWindow")) return false;
        if (key.StartsWith("reveal", StringComparison.Ordinal)) return true;
        foreach (var d in JoineryDetails)
            if (key == d || key.StartsWith(d + ":", StringComparison.Ordinal)) return true;
        return false;
    }
}

public sealed record ElementGeometry(IReadOnlyList<GeometryPart> Parts, IReadOnlyList<PlanItem>? Plan = null, IReadOnlyList<Vec2>? Snaps = null, IReadOnlyList<string>? Diagnostics = null, SharedInstance? Instance = null,
    IReadOnlyList<IReadOnlyList<Vec3>>? Lines = null, string? LineColor = null, IReadOnlyList<Lapin.Drawing.Views.Silhouette>? Silhouettes = null)
{
    // Lines: polylines in space with no body (CAD linework with heights), drawn as such in 3D, in LineColor.
    // Silhouettes: what elevations and sections draw instead of the body's edges (people, cars, plants: decision 341).

    /// <summary>
    /// Its producer builds a finer body when asked (<see cref="GeometryOptions.Fine"/>): a steel section's real outline, which
    /// only a view at LOD 400 draws (decision 415); everything else, the quantities, exports and every lower level, keep this one.
    /// </summary>
    public bool FineShape { get; init; }
}

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
/// <param name="Fine">The real section of a member (<see cref="ElementGeometry.FineShape"/>), as a view at LOD 400 draws it.</param>
public sealed record GeometryOptions(bool CutOpenings = true, bool Fine = false);

/// <summary>The body of a void (an IfcOpeningElement) in its host, for whoever writes it out; a service of the module that owns the host.</summary>
public interface IVoidShapes
{
    Solid? Shape(Entity opening, IModelView model);
}

/// <summary>
/// A table a module offers as a schedule: its title, its columns and its rows (text); optionally what each row lists (an
/// element's id, null for a totals line), so what the user types in a row stays with it while the model changes (decision 251).
/// </summary>
public sealed record ScheduleGrid(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<string?>? Keys = null);

/// <summary>A schedule another module fills (a planting schedule): the kind a schedule entity names, its label, and the table as the model is now.</summary>
public interface IScheduleSource
{
    string Kind { get; }
    string Label { get; }
    ScheduleGrid Grid(IQueryContext query);
    /// <summary>The table as <paramref name="schedule"/> (the schedule entity showing it) asks for it: its own options (a takeoff's grouping); by default the plain table.</summary>
    ScheduleGrid Grid(IQueryContext query, Entity schedule) => Grid(query);
    /// <summary>A value typed in a row's cell written into what the row lists (<paramref name="key"/>, from the grid's keys) under <paramref name="column"/>; false when that column is not the model's to write (the value is then kept on the schedule).</summary>
    bool Write(ICommandContext ctx, string key, string column, string? text) => false;
}

/// <summary>
/// What a module draws as a single line at LOD 100 instead of its body (decision 405): a steel member's axis, from its start
/// to its end, world metres; null when the element is none of its.
/// </summary>
public interface IStickAxes
{
    (Vec3 A, Vec3 B)? StickOf(IModelView model, Entity element);
}
