namespace Lapin.Geometry.Constraints;

// The solver's own picture of a 2D sketch: points, the entities built on them by index, and the constraints between
// them. Plain records with no tie to the model: whoever calls the solver (the sketch commands) maps its shapes onto
// these and the result back. Lengths in metres, angles in radians, measured counter-clockwise.

/// <summary>A sketch point. The solver's unknowns are the x and y of every point that is not <see cref="Fixed"/>.</summary>
public sealed record SketchPoint(double X, double Y, bool Fixed = false);

/// <summary>
/// An entity built on points by their index in <see cref="Sketch.Points"/>. Two entities that share a point index are
/// joined there with no constraint needed. A <see cref="Fixed"/> entity holds its points and radius where they are.
/// </summary>
public abstract record SketchEntity(bool Fixed);

/// <summary>A line segment from <see cref="P1"/> to <see cref="P2"/>.</summary>
public sealed record SketchLine(int P1, int P2, bool Fixed = false) : SketchEntity(Fixed);

/// <summary>A circle; its radius is an unknown unless the circle is fixed.</summary>
public sealed record SketchCircle(int Center, double Radius, bool Fixed = false) : SketchEntity(Fixed);

/// <summary>
/// An arc counter-clockwise from <see cref="Start"/> to <see cref="End"/> about <see cref="Center"/>. Both end points lie
/// on the arc: the solver holds |start − center| = |end − center| = radius by itself (two rows of its own), so an arc
/// has five degrees of freedom. The radius is an unknown unless the arc is fixed.
/// </summary>
public sealed record SketchArc(int Center, int Start, int End, double Radius, bool Fixed = false) : SketchEntity(Fixed);

/// <summary>What a constraint reference points at.</summary>
public enum SketchRefKind { Point, Entity }

/// <summary>A constraint's reference: a point or an entity, by its index in the sketch.</summary>
public readonly record struct SketchRef(SketchRefKind Kind, int Index)
{
    public static SketchRef Point(int index) => new(SketchRefKind.Point, index);
    public static SketchRef Entity(int index) => new(SketchRefKind.Entity, index);
}

/// <summary>
/// The constraint kinds (decision 517, docs/plan-studio-parametric.md §4.1) and the references each takes, in order.
/// "P" is a point reference, "L" a line entity, "C" a circle or an arc, "E" any entity. Dimensions carry
/// <see cref="SketchConstraint.Value"/>.
/// </summary>
public enum ConstraintKind
{
    /// <summary>[P, P]: one point.</summary>
    Coincident,
    /// <summary>[P, L]: on the line's infinite support; [P, C]: on the circle (an arc's whole circle).</summary>
    PointOn,
    /// <summary>[L] or [P, P].</summary>
    Horizontal,
    /// <summary>[L] or [P, P].</summary>
    Vertical,
    /// <summary>[L, L].</summary>
    Parallel,
    /// <summary>[L, L].</summary>
    Perpendicular,
    /// <summary>
    /// [L, C], [C, L] or [C, C]: tangent along (anywhere), on the side each is on when the solve starts (a line keeps
    /// the circle on its side; two circles stay externally or internally tangent, whichever is nearer).
    /// [E, E, P, P]: tangent where they join, at the first entity's end point P1 and the second's P2 (made one point
    /// by this constraint: no separate coincidence needed).
    /// </summary>
    Tangent,
    /// <summary>[L, L]: equal lengths; [C, C]: equal radii.</summary>
    Equal,
    /// <summary>[P, P, L]: mirrored about the line; [P, P, P]: about the third point.</summary>
    Symmetric,
    /// <summary>[P, L]: the point at the line's middle.</summary>
    Midpoint,
    /// <summary>[P]: the point held where it is at the solve's start; [E]: the entity's points and radius held.</summary>
    Fix,
    /// <summary>[P, P]: distance; [P, L]: from the point to the line's support; [L]: the line's length.</summary>
    Distance,
    /// <summary>[P, P]: x₂ − x₁ (signed); [L]: x(P2) − x(P1); [P]: the point's x.</summary>
    DistanceX,
    /// <summary>[P, P]: y₂ − y₁ (signed); [L]: y(P2) − y(P1); [P]: the point's y.</summary>
    DistanceY,
    /// <summary>[C].</summary>
    Radius,
    /// <summary>[C].</summary>
    Diameter,
    /// <summary>
    /// [L, L]: the angle from the first line's direction (P1→P2) to the second's, counter-clockwise, in radians;
    /// [L]: from the x axis to the line.
    /// </summary>
    Angle,
}

/// <summary>
/// A constraint. A <see cref="Driving"/> one enters the system; a driven one (a reference dimension) does not, and
/// its value is measured after the solve (<see cref="SolveResult.Values"/>). Results name constraints by their
/// index in <see cref="Sketch.Constraints"/>.
/// </summary>
public sealed record SketchConstraint(ConstraintKind Kind, IReadOnlyList<SketchRef> Refs, double Value = 0, bool Driving = true)
{
    private static SketchRef P(int i) => SketchRef.Point(i);
    private static SketchRef E(int i) => SketchRef.Entity(i);

    public static SketchConstraint Coincident(int p, int q) => new(ConstraintKind.Coincident, [P(p), P(q)]);
    public static SketchConstraint PointOn(int point, int entity) => new(ConstraintKind.PointOn, [P(point), E(entity)]);
    public static SketchConstraint Horizontal(int line) => new(ConstraintKind.Horizontal, [E(line)]);
    public static SketchConstraint HorizontalPoints(int p, int q) => new(ConstraintKind.Horizontal, [P(p), P(q)]);
    public static SketchConstraint Vertical(int line) => new(ConstraintKind.Vertical, [E(line)]);
    public static SketchConstraint VerticalPoints(int p, int q) => new(ConstraintKind.Vertical, [P(p), P(q)]);
    public static SketchConstraint Parallel(int line1, int line2) => new(ConstraintKind.Parallel, [E(line1), E(line2)]);
    public static SketchConstraint Perpendicular(int line1, int line2) => new(ConstraintKind.Perpendicular, [E(line1), E(line2)]);
    public static SketchConstraint Tangent(int entity1, int entity2) => new(ConstraintKind.Tangent, [E(entity1), E(entity2)]);
    public static SketchConstraint TangentAt(int entity1, int end1, int entity2, int end2) =>
        new(ConstraintKind.Tangent, [E(entity1), E(entity2), P(end1), P(end2)]);
    public static SketchConstraint Equal(int entity1, int entity2) => new(ConstraintKind.Equal, [E(entity1), E(entity2)]);
    public static SketchConstraint Symmetric(int p, int q, int line) => new(ConstraintKind.Symmetric, [P(p), P(q), E(line)]);
    public static SketchConstraint SymmetricAboutPoint(int p, int q, int center) => new(ConstraintKind.Symmetric, [P(p), P(q), P(center)]);
    public static SketchConstraint Midpoint(int point, int line) => new(ConstraintKind.Midpoint, [P(point), E(line)]);
    public static SketchConstraint FixPoint(int point) => new(ConstraintKind.Fix, [P(point)]);
    public static SketchConstraint FixEntity(int entity) => new(ConstraintKind.Fix, [E(entity)]);
    public static SketchConstraint Distance(int p, int q, double value, bool driving = true) => new(ConstraintKind.Distance, [P(p), P(q)], value, driving);
    public static SketchConstraint DistancePointLine(int point, int line, double value, bool driving = true) => new(ConstraintKind.Distance, [P(point), E(line)], value, driving);
    public static SketchConstraint Length(int line, double value, bool driving = true) => new(ConstraintKind.Distance, [E(line)], value, driving);
    public static SketchConstraint DistanceX(int p, int q, double value, bool driving = true) => new(ConstraintKind.DistanceX, [P(p), P(q)], value, driving);
    public static SketchConstraint DistanceXOfLine(int line, double value, bool driving = true) => new(ConstraintKind.DistanceX, [E(line)], value, driving);
    public static SketchConstraint DistanceXFromOrigin(int point, double value, bool driving = true) => new(ConstraintKind.DistanceX, [P(point)], value, driving);
    public static SketchConstraint DistanceY(int p, int q, double value, bool driving = true) => new(ConstraintKind.DistanceY, [P(p), P(q)], value, driving);
    public static SketchConstraint DistanceYOfLine(int line, double value, bool driving = true) => new(ConstraintKind.DistanceY, [E(line)], value, driving);
    public static SketchConstraint DistanceYFromOrigin(int point, double value, bool driving = true) => new(ConstraintKind.DistanceY, [P(point)], value, driving);
    public static SketchConstraint Radius(int entity, double value, bool driving = true) => new(ConstraintKind.Radius, [E(entity)], value, driving);
    public static SketchConstraint Diameter(int entity, double value, bool driving = true) => new(ConstraintKind.Diameter, [E(entity)], value, driving);
    public static SketchConstraint Angle(int line1, int line2, double radians, bool driving = true) => new(ConstraintKind.Angle, [E(line1), E(line2)], radians, driving);
    public static SketchConstraint AngleToX(int line, double radians, bool driving = true) => new(ConstraintKind.Angle, [E(line)], radians, driving);
}

/// <summary>A sketch for the solver: its points, entities and constraints. The current geometry is the solve's start.</summary>
public sealed record Sketch(IReadOnlyList<SketchPoint> Points, IReadOnlyList<SketchEntity> Entities, IReadOnlyList<SketchConstraint> Constraints);

/// <summary>A point pulled toward (<see cref="X"/>, <see cref="Y"/>) while it is dragged; the constraints still hold.</summary>
public sealed record DragGoal(int Point, double X, double Y);

/// <summary>How a solve runs.</summary>
public sealed record SolverOptions
{
    /// <summary>The largest residual accepted as solved: metres for lengths, radians for angles, sines for parallel/perpendicular.</summary>
    public double Tolerance { get; init; } = 1e-10;

    /// <summary>The most damped least-squares iterations per cluster.</summary>
    public int MaxIterations { get; init; } = 100;

    /// <summary>
    /// Points being dragged. Each starts at its goal and is weighted <see cref="DragWeight"/> times heavier than the other
    /// unknowns in the least-movement step, so the others give way first and the dragged point leaves its goal only as
    /// far as the constraints require.
    /// </summary>
    public IReadOnlyList<DragGoal> DragGoals { get; init; } = [];

    /// <summary>A dragged point's weight against 1 for every other unknown.</summary>
    public double DragWeight { get; init; } = 1000;

    public static SolverOptions Default { get; } = new();
}

/// <summary>A solve's outcome. The first three succeed; the last two refuse and leave the geometry as it was.</summary>
public enum SolveStatus
{
    /// <summary>Every constraint holds and nothing is left free.</summary>
    Solved,
    /// <summary>Every constraint holds; some degrees of freedom are left (<see cref="SolveResult.DegreesOfFreedom"/>).</summary>
    Underconstrained,
    /// <summary>Every constraint holds, but some repeat what others already say (<see cref="SolveResult.Redundant"/>).</summary>
    Redundant,
    /// <summary>The constraints contradict each other (<see cref="SolveResult.Conflicting"/>): no geometry satisfies them all.</summary>
    Conflicting,
    /// <summary>The iterations ran out (or a step would have flipped an arc) before the constraints held.</summary>
    NotConverged,
}

/// <summary>An unknown of the system: a point's x or y, or a circle's or arc's radius.</summary>
public enum SketchUnknownKind { PointX, PointY, Radius }

/// <summary>An unknown by kind and the index of its point (<see cref="SketchUnknownKind.PointX"/>, <see cref="SketchUnknownKind.PointY"/>) or entity (<see cref="SketchUnknownKind.Radius"/>).</summary>
public readonly record struct SketchUnknown(SketchUnknownKind Kind, int Index);

/// <summary>
/// What <see cref="SketchSolver.Solve"/> found. <see cref="Points"/> and <see cref="Radii"/> are the solved geometry
/// when the solve <see cref="Succeeded"/>, the input's otherwise.
/// </summary>
/// <param name="Radii">Per entity: the radius of a circle or arc, NaN for a line.</param>
/// <param name="DegreesOfFreedom">Unknowns minus the rank of the constraints' Jacobian at the end of the solve.</param>
/// <param name="FreeUnknowns">The unknowns the constraints leave free (points' x and y by point, then radii by entity).</param>
/// <param name="PointIsFree">Per point: some coordinate still free (blue); false for a fully constrained or fixed one (ink).</param>
/// <param name="EntityIsFree">Per entity: some point of it or its radius still free.</param>
/// <param name="Redundant">Indices of driving constraints that repeat what earlier ones already fix (consistent).</param>
/// <param name="Conflicting">Indices of driving constraints that cannot all hold together.</param>
/// <param name="Values">
/// Per constraint: the measured value of a dimension (driving or driven) on the returned geometry — a distance, a
/// signed distanceX/Y, a radius, a diameter, an angle in [0, 2π) — NaN for a constraint that has none.
/// </param>
/// <param name="Iterations">Damped least-squares iterations, summed over the clusters.</param>
/// <param name="MaxResidual">The largest residual of a driving constraint at the end of the solve.</param>
public sealed record SolveResult(
    SolveStatus Status,
    IReadOnlyList<SketchPoint> Points,
    IReadOnlyList<double> Radii,
    int DegreesOfFreedom,
    IReadOnlyList<SketchUnknown> FreeUnknowns,
    IReadOnlyList<bool> PointIsFree,
    IReadOnlyList<bool> EntityIsFree,
    IReadOnlyList<int> Redundant,
    IReadOnlyList<int> Conflicting,
    IReadOnlyList<double> Values,
    int Iterations,
    double MaxResidual)
{
    public bool Succeeded => Status is SolveStatus.Solved or SolveStatus.Underconstrained or SolveStatus.Redundant;

    /// <summary>The sketch with this result's points and radii.</summary>
    public Sketch Apply(Sketch sketch)
    {
        var entities = new SketchEntity[sketch.Entities.Count];
        for (int i = 0; i < entities.Length; i++)
            entities[i] = sketch.Entities[i] switch
            {
                SketchCircle c => c with { Radius = Radii[i] },
                SketchArc a => a with { Radius = Radii[i] },
                var e => e,
            };
        return sketch with { Points = Points.ToArray(), Entities = entities };
    }
}
