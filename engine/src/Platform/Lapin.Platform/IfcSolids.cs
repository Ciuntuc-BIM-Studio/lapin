using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A profile swept straight (IfcExtrudedAreaSolid): its outline and holes in the plane of <see cref="XDir"/> and
/// <see cref="Axis"/> × <see cref="XDir"/>, standing at <see cref="Origin"/>, swept along <see cref="Axis"/> (a unit
/// vector) by <see cref="Depth"/>. World metres.
/// </summary>
public sealed record IfcSweep(IReadOnlyList<Vec2> Profile, IReadOnlyList<IReadOnlyList<Vec2>> Holes, Vec3 Origin, Vec3 XDir, Vec3 Axis, double Depth);

/// <summary>A plane cutting a sweep: what lies on the side <see cref="Normal"/> points to (out of the material) is taken off.</summary>
public sealed record IfcClip(Vec3 Point, Vec3 Normal);

/// <summary>
/// An element's body as IFC's constructive solids (decision 268): a sweep cut by planes (IfcBooleanClippingResult of
/// IfcHalfSpaceSolid), and the openings voiding it (IfcOpeningElement, each a sweep), as Tekla, Advance Steel and
/// Revit read a steel member's cuts and copes. The body is the same as the element's geometry, which the views draw.
/// </summary>
public sealed record IfcSweptBody(IfcSweep Body, IReadOnlyList<IfcClip> Clips, IReadOnlyList<IfcSweep> Openings);

/// <summary>A module's elements whose bodies the IFC export writes as constructive solids rather than as B-reps; null for the others.</summary>
public interface IIfcSweptBodies
{
    IfcSweptBody? BodyOf(IModelView model, Entity element);
}
