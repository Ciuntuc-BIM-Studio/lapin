using Lapin.Alignments;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>An IfcAlignmentHorizontalSegment: IFC's type name, the start point and direction (radians CCW from +X), the radii (0 straight, + left, − right), the length; the Viennese bend's gravity centre height.</summary>
public sealed record IfcHorizontalSegment(string Type, Vec2 Start, double Direction, double StartRadius, double EndRadius, double Length, double? GravityCenterLineHeight = null);

/// <summary>An IfcAlignmentVerticalSegment: the type, its start along the horizontal, its horizontal length, start height and gradients (ratios), the radius where the type has one.</summary>
public sealed record IfcVerticalSegment(string Type, double StartDistAlong, double HorizontalLength, double StartHeight, double StartGradient, double EndGradient, double? Radius = null);

/// <summary>An IfcAlignmentCantSegment: the type, its start and length along the horizontal, each rail's cant at its start and (transitions) its end, metres.</summary>
public sealed record IfcCantSegment(string Type, double StartDistAlong, double HorizontalLength, double StartCantLeft, double? EndCantLeft, double StartCantRight, double? EndCantRight);

/// <summary>The cant layer: the rail head distance the cant is measured over, and the segments.</summary>
public sealed record IfcCantLayer(double RailHeadDistance, IReadOnlyList<IfcCantSegment> Segments);

/// <summary>
/// Where an element of the railway stands along an alignment, for its
/// IfcLinearPlacement (IFC 4.3): the alignment's id (one the module offers in
/// Alignments), the distance along it, and the frame there — the point in 3D
/// and the heading in plan (radians, counter-clockwise from +X) — which the
/// export writes as the placement's CartesianPosition too and expresses the
/// element's body in. Metres.
/// </summary>
public sealed record IfcLinearPlacementData(string AlignmentId, double DistanceAlong, Vec3 Origin, double Heading);

/// <summary>An IfcReferent on the alignment: its distance along, name, IfcReferentTypeEnum, and its Pset_Stationing (Station, HasIncreasingStation, IncomingStation at a station equation).</summary>
public sealed record IfcReferentData(double DistanceAlong, string Name, string Type, double Station, bool? HasIncreasingStation = null, double? IncomingStation = null);

/// <summary>
/// An alignment for the IFC 4.3 export: the entity it belongs to (its GlobalId's
/// seed), its name, the station at its start, its three layers, and its plan
/// drawn through; the geometric representation of the layers as curve
/// segments (without it only the semantic segments go out) and the referents
/// along it. Metres, radians.
/// </summary>
public sealed record IfcAlignmentData(string Id, string Name, double StartStation, IReadOnlyList<IfcHorizontalSegment> Horizontal, IReadOnlyList<IfcVerticalSegment>? Vertical, IfcCantLayer? Cant, IReadOnlyList<Vec2> Plan, IfcAlignmentCurves? Curves = null, IReadOnlyList<IfcReferentData>? Referents = null);

/// <summary>
/// The alignments a module offers the IFC 4.3 export (a track's, a road's),
/// written as IfcAlignment with its semantic layers, aggregated by the project
/// and referenced in the site, and which of the module's elements belong to
/// a railway facility part ("TRACK", "TURNOUTTRACK", "LINESIDE": contained in
/// an IfcRailwayPart of an IfcRailway under the site, in IFC 4.3) rather than
/// to a storey. Offered as a service; the export works without any.
/// </summary>
public interface IIfcAlignments
{
    IEnumerable<IfcAlignmentData> Alignments(IModelView model);

    /// <summary>The railway part an element belongs to, or null (it stays on its storey).</summary>
    string? RailwayPartOf(IModelView model, Entity element);

    /// <summary>Where an element of a railway part stands along its alignment, for an IfcLinearPlacement; null: it is placed by coordinates. IFC 4.3 only; none by default.</summary>
    IfcLinearPlacementData? LinearPlacementOf(IModelView model, Entity element) => null;

    /// <summary>
    /// The standard property sets (IFC 4.3's own, named Pset_…) an element of the
    /// module carries — or, with <paramref name="part"/>, one of its parts (a
    /// track's rails and sleepers): the turnout panel's radius and diverging
    /// speed, a rail's position in the track. IFC 4.3 only; none by default.
    /// </summary>
    IEnumerable<IfcStandardSet> StandardSets(IModelView model, Entity element, GeometryPart? part) => [];
}

/// <summary>One standard property: its name, its IFC measure type (IFCLABEL, IFCBOOLEAN, IFCLENGTHMEASURE…) and its value (a string, a bool or a number in SI units).</summary>
public sealed record IfcStandardProperty(string Name, string Measure, object Value);

/// <summary>A standard property set (Pset_…) and its properties.</summary>
public sealed record IfcStandardSet(string Name, IReadOnlyList<IfcStandardProperty> Properties);
