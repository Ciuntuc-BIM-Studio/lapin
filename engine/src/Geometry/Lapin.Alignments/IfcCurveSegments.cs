using Lapin.Geometry;

namespace Lapin.Alignments;

/// <summary>
/// A curve segment's parent curve, at its own origin: "line"; "circle" (Radius,
/// Centre); "clothoid" (A); "polynomial" (X, Y coefficients by power); "spiral"
/// (Terms A₀…Aₙ of an IfcSecondOrder- (3), IfcThirdOrder- (4) or
/// IfcSeventhOrderPolynomialSpiral (8), null = absent); "cosine" (Constant,
/// Trig = the cosine term); "sine" (Constant, Linear, Trig = the sine term).
/// </summary>
public sealed record IfcParentCurve(string Kind, double Radius = 0, Vec2 Centre = default, double A = 0, IReadOnlyList<double>? X = null, IReadOnlyList<double>? Y = null, IReadOnlyList<double?>? Terms = null, double? Constant = null, double? Linear = null, double Trig = 0)
{
    public static readonly IfcParentCurve Line = new("line");
}

/// <summary>
/// One IfcCurveSegment of an alignment's geometric representation: the layer
/// ("horizontal", "vertical", "cant"), the semantic segment it represents
/// (Source; −1 for the closing zero-length segment) and which piece of it
/// (a Helmert curve has two), the transition code to the next, its placement
/// (Location and RefDirection, 2D, or 3D with Axis for the cant), SegmentStart
/// and SegmentLength on the parent curve (negative: against its sense).
/// </summary>
public sealed record IfcCurveSegmentSpec(string Layer, int Source, int Piece, string Transition, IReadOnlyList<double> Location, IReadOnlyList<double> RefDirection, IReadOnlyList<double>? Axis, double Start, double Length, IfcParentCurve Parent);

/// <summary>The three layers as curve segments: the IfcCompositeCurve, the IfcGradientCurve over it (null without a vertical layer), the IfcSegmentedReferenceCurve over that (null without a cant or a vertical layer).</summary>
public sealed record IfcAlignmentCurves(IReadOnlyList<IfcCurveSegmentSpec> Horizontal, IReadOnlyList<IfcCurveSegmentSpec>? Vertical, IReadOnlyList<IfcCurveSegmentSpec>? Cant);
