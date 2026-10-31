using Lapin.Geometry;

namespace Lapin.Alignments;

/// <summary>
/// An alignment as IFC 4.3 describes it: three layers of semantic segments —
/// horizontal (plan), vertical (profile) and cant (superelevation of one rail
/// over the other) — each segment carrying exactly the attributes of
/// IfcAlignmentHorizontalSegment, IfcAlignmentVerticalSegment and
/// IfcAlignmentCantSegment. Ported from BubbleBIM Standalone
/// (lib/civil/align), verified there against the buildingSMART reference lists.
///
/// Metres and radians. Distances along (s, d) are measured along the
/// horizontal layer from its first segment's start; a station is
/// StartStation + s. Radius conventions are IFC's: 0 means infinite (a
/// straight), a positive radius turns counter-clockwise (left), a negative one
/// clockwise (right). The station equations, where the alignment has them,
/// turn a station into the kilometrage the railway keeps (<see cref="Equated"/>).
/// </summary>
public sealed record Alignment(IReadOnlyList<HorizontalSegment> Horizontal, IReadOnlyList<VerticalSegment>? Vertical = null, CantLayer? Cant = null, double StartStation = 0, IReadOnlyList<StationEquation>? Equations = null)
{
    /// <summary>1/R with IFC's convention that a radius of 0 is infinite.</summary>
    public static double CurvatureOf(double radius) => radius == 0 ? 0 : 1 / radius;

    /// <summary>The horizontal length: the distance along at the end.</summary>
    public double Length => Horizontal.Sum(h => h.Length);

    /// <summary>The stationing over the alignment: its start station over its length, with its station equations.</summary>
    public Stationing Stationing => new(StartStation, Length, Equations);

    /// <summary>The equated station of a station (StartStation + distance along): the same without equations, the kilometrage the railway keeps with them.</summary>
    public double Equated(double station) => Equations is { Count: > 0 } ? Stationing.StationOf(station - StartStation).Station : station;

    /// <summary>A station as the findings write it, equated: 1234.5 → "1+234.50".</summary>
    public string Km(double station) => Stationing.FormatKm(Equated(station), 2, '.');
}

public enum HorizontalType { Line, CircularArc, Clothoid, Cubic, HelmertCurve, BlossCurve, CosineCurve, SineCurve, VienneseBend }
public enum VerticalType { ConstantGradient, CircularArc, ParabolicArc, Clothoid }
public enum CantType { ConstantCant, LinearTransition, HelmertCurve, BlossCurve, CosineCurve, SineCurve, VienneseBend }

public static class SegmentTypes
{
    /// <summary>IFC's spelling of a type (its enumeration item).</summary>
    public static string Ifc(HorizontalType t) => t.ToString().ToUpperInvariant();
    public static string Ifc(VerticalType t) => t.ToString().ToUpperInvariant();
    public static string Ifc(CantType t) => t.ToString().ToUpperInvariant();

    public static HorizontalType? Horizontal(string? ifc) => Enum.TryParse<HorizontalType>(ifc, true, out var t) ? t : null;
    public static VerticalType? Vertical(string? ifc) => Enum.TryParse<VerticalType>(ifc, true, out var t) ? t : null;
    public static CantType? Cant(string? ifc) => Enum.TryParse<CantType>(ifc, true, out var t) ? t : null;

    /// <summary>Transitions: every horizontal type but the line and the arc.</summary>
    public static bool IsTransition(HorizontalType t) => t is not (HorizontalType.Line or HorizontalType.CircularArc);
}

/// <summary>
/// IfcAlignmentHorizontalSegment. <paramref name="StartDirection"/> is the tangent at the
/// start, radians counter-clockwise from +X. <paramref name="Length"/> is along the curve —
/// for CUBIC along the parent cubic's x axis (its parameter), as IFC's IfcPolynomialCurve
/// parameterisation and the buildingSMART reference lists have it.
/// <paramref name="GravityCenterLineHeight"/>: VIENNESEBEND only, the height h of the gravity
/// centre line above the track centre line.
/// </summary>
public sealed record HorizontalSegment(HorizontalType Type, Vec2 Start, double StartDirection, double StartRadius, double EndRadius, double Length, double? GravityCenterLineHeight = null)
{
    /// <summary>The radius the segment ends on (IFC's 0 = a straight).</summary>
    public double EndRadiusOf => Type == HorizontalType.Line ? 0 : Type == HorizontalType.CircularArc ? (StartRadius != 0 ? StartRadius : EndRadius) : EndRadius;
}

/// <summary>
/// IfcAlignmentVerticalSegment. <paramref name="StartDistAlong"/> is measured along the horizontal
/// layer, <paramref name="HorizontalLength"/> too (not the 3D curve length). Gradients are ratios,
/// uphill positive (0.03 = 3 %); for CONSTANTGRADIENT the end gradient equals the start one.
/// <paramref name="RadiusOfCurvature"/>: CIRCULARARC / PARABOLICARC: redundant with the gradients
/// and the length, which win; CLOTHOID: the radius at the start (absent: the clothoid leaves a
/// constant grade).
/// </summary>
public sealed record VerticalSegment(VerticalType Type, double StartDistAlong, double HorizontalLength, double StartHeight, double StartGradient, double EndGradient, double? RadiusOfCurvature = null)
{
    public double EndDistAlong => StartDistAlong + HorizontalLength;
}

/// <summary>IfcAlignmentCantSegment. An absent end cant means the start value holds (constant).</summary>
public sealed record CantSegment(CantType Type, double StartDistAlong, double HorizontalLength, double StartCantLeft, double? EndCantLeft, double StartCantRight, double? EndCantRight)
{
    public double EndDistAlong => StartDistAlong + HorizontalLength;
    public double EndLeft => EndCantLeft ?? StartCantLeft;
    public double EndRight => EndCantRight ?? StartCantRight;
}

/// <summary>IfcAlignmentCant: the rail head distance b (about 1.5 m for standard gauge) and the segments.</summary>
public sealed record CantLayer(double RailHeadDistance, IReadOnlyList<CantSegment> Segments);

/// <summary>A point of the horizontal layer: position, tangent direction (radians CCW from +X, continuous, not wrapped) and signed curvature (1/m, positive turning left).</summary>
public readonly record struct PlanPoint(double X, double Y, double Direction, double Curvature)
{
    public Vec2 P => new(X, Y);
    /// <summary>The unit vector to the left of the direction of travel.</summary>
    public Vec2 Left => new(-Math.Sin(Direction), Math.Cos(Direction));
    public Vec2 Tangent => new(Math.Cos(Direction), Math.Sin(Direction));
}

/// <summary>A point of the vertical layer: height, dz/d(distance along) and the geometric curvature of the profile curve (1/m, positive sagging).</summary>
public readonly record struct ProfilePoint(double Z, double Gradient, double Curvature);

/// <summary>A point of the cant layer: each rail's height over the profile, their difference (positive when the right rail is higher) and the bank angle ψ = asin(D / b).</summary>
public readonly record struct CantPoint(double CantLeft, double CantRight, double Superelevation, double Angle);

/// <summary>The whole alignment at a distance along: plan, and the profile and cant values where their layers cover it.</summary>
public sealed record AlignmentPoint(double S, double Station, PlanPoint Plan, ProfilePoint? Profile, CantPoint? Cant)
{
    public double X => Plan.X;
    public double Y => Plan.Y;
    public double Direction => Plan.Direction;
    public double Curvature => Plan.Curvature;
    public double? Z => Profile?.Z;
    public double? Gradient => Profile?.Gradient;
    public double? Superelevation => Cant?.Superelevation;
}
