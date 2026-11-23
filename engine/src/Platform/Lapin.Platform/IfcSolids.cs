using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A profile swept straight (IfcExtrudedAreaSolid): its outline and holes in the plane of <see cref="XDir"/> and
/// <see cref="Axis"/> × <see cref="XDir"/>, standing at <see cref="Origin"/>, swept along <see cref="Axis"/> (a unit
/// vector) by <see cref="Depth"/>. World metres.
/// </summary>
public sealed record IfcSweep(IReadOnlyList<Vec2> Profile, IReadOnlyList<IReadOnlyList<Vec2>> Holes, Vec3 Origin, Vec3 XDir, Vec3 Axis, double Depth)
{
    /// <summary>
    /// The profile this sweep is made of, when it has one: IFC writes it as its parametric subtype with its name and
    /// <c>Pset_ProfileMechanical</c> (decision 416), centred on its box as Lapin's sections and IFC's are, so <see cref="Origin"/>
    /// and <see cref="XDir"/> stand for it as they do for <see cref="Profile"/>. Kinds IFC has no form for keep <see cref="Profile"/>.
    /// </summary>
    public Lapin.Platform.ProfileLibraries.SectionProfile? Parametric { get; init; }
}

/// <summary>A plane cutting a sweep: what lies on the side <see cref="Normal"/> points to (out of the material) is taken off.</summary>
public sealed record IfcClip(Vec3 Point, Vec3 Normal);

/// <summary>
/// An element's body as IFC's constructive solids (decision 268): a sweep cut by planes (IfcBooleanClippingResult of
/// IfcHalfSpaceSolid), and the openings voiding it (IfcOpeningElement, each a sweep), as Tekla, Advance Steel and
/// Revit read a steel member's cuts and copes. The body is the same as the element's geometry, which the views draw.
/// </summary>
public sealed record IfcSweptBody(IfcSweep Body, IReadOnlyList<IfcClip> Clips, IReadOnlyList<IfcSweep> Openings)
{
    /// <summary>
    /// Pieces welded onto the element (a rafter's haunches, decision 270), each written as an element of its own; the
    /// element and its pieces then make an IfcElementAssembly named <see cref="Assembly"/>, as Tekla and Advance Steel write
    /// a main part with its plates.
    /// </summary>
    public IReadOnlyList<IfcSweptPart> Parts { get; init; } = [];
    public string? Assembly { get; init; }
}

/// <summary>A piece welded onto an element: its IFC class and type (an IfcPlate, an IfcMember), its name, and its body as sweeps.</summary>
public sealed record IfcSweptPart(string Key, string Name, string Class, string Predefined, string? ObjectType, IReadOnlyList<IfcSweep> Items);

/// <summary>
/// A system a module's entity stands for in IFC (a steel hall: an IfcBuildingSystem, LOADBEARING), its members assigned to it
/// (IfcRelAssignsToGroup) and serving the building, its own properties as a property set.
/// </summary>
public sealed record IfcSystem(string Id, string Name, string Predefined, IReadOnlyList<string> Members, string PropertySet, IReadOnlyList<(string Name, string Value)> Properties);

/// <summary>
/// What a module keeps out of the IFC export (docs/overrides.md, Driven elements): its own groups (they stay the project's,
/// in .lapin; their members are written as any element) and its own attribute keys (never written in an element's
/// Lapin_Intent set).
/// </summary>
public interface IIfcExportFilter
{
    /// <summary>Whether this group is left out (no IfcGroup, no IfcRelAssignsToGroup).</summary>
    bool SkipsGroup(IModelView model, Entity group) => false;
    /// <summary>The attribute keys never written.</summary>
    IEnumerable<string> PrivateKeys => [];
    /// <summary>Whether this element's attribute is left out of its intent set (a module writes it in a set of its own: a SubD solid's cage in Lapin_SubD).</summary>
    bool IsPrivate(Entity element, string key) => false;
}

/// <summary>A module's entities that IFC writes as systems of elements (IfcBuildingSystem).</summary>
public interface IIfcSystems
{
    IEnumerable<IfcSystem> Systems(IModelView model);
}

/// <summary>
/// A module's elements that are recesses in their host (a wall reveal, decision 297): related to it by IfcRelVoidsElement,
/// with no body of their own, written to IFC as an IfcOpeningElement of their predefined type (RECESS) voiding the host, its
/// body the one given here; null for the others.
/// </summary>
public interface IIfcRecesses
{
    Solid? RecessOf(IModelView model, IServices services, Entity element);
}

/// <summary>A module's elements whose bodies the IFC export writes as constructive solids rather than as B-reps; null for the others.</summary>
public interface IIfcSweptBodies
{
    IfcSweptBody? BodyOf(IModelView model, Entity element);
    /// <summary>The same with the session's services at hand: what reads other elements' bodies (a column's top attached to a slab's soffit) needs them.</summary>
    IfcSweptBody? BodyOf(IModelView model, IServices services, Entity element) => BodyOf(model, element);
}

/// <summary>
/// A module's elements whose bodies are known exactly (docs/plan-studio-freeform.md §9): faces on planes, cylinders, cones, spheres, tori
/// and B-spline surfaces, written to IFC4 as IfcAdvancedBrep (when the export asks) and to STEP as MANIFOLD_SOLID_BREP. Given the element's
/// body as its geometry has it (<paramref name="body"/>, in the world), the exact body that is that body, or null (then it is written as its
/// facets).
/// </summary>
public interface IIfcExactBodies
{
    ExactBody? ExactOf(IModelView model, IServices services, Entity element, Solid body);

    /// <summary>
    /// The exact body of one of an element's parts (<paramref name="key"/>, its body <paramref name="body"/>) written as an element of its
    /// own (a wing of an abutment, decision 789 S3), or null (written as its facets). Only what knows its parts says.
    /// </summary>
    ExactBody? ExactOfPart(IModelView model, IServices services, Entity element, string key, Solid body) => null;
}

/// <summary>
/// A connection between two elements an element realizes (a steel connection between a column and a rafter, decision 273):
/// written as IfcRelConnectsWithRealizingElements, <see cref="Realizing"/> the element (an assembly of plates and bolts)
/// that makes it, <see cref="Type"/> its ConnectionType.
/// </summary>
public sealed record IfcConnection(string Realizing, string Relating, string Related, string? Type);

/// <summary>A module's elements that connect others (steel connections).</summary>
public interface IIfcConnections
{
    IEnumerable<IfcConnection> Connections(IModelView model);
}

/// <summary>
/// An element with no body written as an IfcAnnotation (a feature line on the site, docs/plan-site-grading.md §4): its curves in
/// space (model metres, z the model's height), its IFC 4.3 PredefinedType and its ObjectType. Lapin_Intent goes with it, so an
/// import makes the element again.
/// </summary>
public sealed record IfcAnnotationCurve(IReadOnlyList<IReadOnlyList<Vec3>> Curves, string Predefined, string? ObjectType);

/// <summary>The module's elements written as IfcAnnotation with a 3D curve (<see cref="IfcAnnotationCurve"/>); null for the others.</summary>
public interface IIfcAnnotationCurves
{
    IfcAnnotationCurve? CurveOf(IModelView model, Entity element);
}
