using System.Diagnostics;
using Xunit.Abstractions;
using Lapin.Geometry.Constraints;
using C = Lapin.Geometry.Constraints.SketchConstraint;

namespace Lapin.Engine.Tests;

/// <summary>Stage C1 of decision 517/520: Lapin's own sketch constraint solver (Lapin.Geometry/Constraints).</summary>
public class SketchSolverTests(ITestOutputHelper output)
{
    private const double Eps = 1e-8;

    private static SketchPoint Pt(double x, double y, bool fixedPoint = false) => new(x, y, fixedPoint);

    private static void Near(double expected, double actual, double eps = Eps) =>
        Assert.True(Math.Abs(expected - actual) <= eps, $"expected {expected}, got {actual}");

    private static void At(SolveResult r, int p, double x, double y, double eps = Eps)
    {
        Near(x, r.Points[p].X, eps);
        Near(y, r.Points[p].Y, eps);
    }

    // A rectangle drawn roughly, 2 × 1, each line with its own two end points (8 points): bottom 0→1, right 2→3,
    // top 4→5, left 6→7.
    private static readonly SketchPoint[] LooseRectangle =
    [
        Pt(0, 0), Pt(2.1, 0.1), Pt(2.05, -0.05), Pt(1.95, 1.1), Pt(2, 1.05), Pt(0.05, 0.95), Pt(-0.1, 1), Pt(0.02, 0.03),
    ];

    private static readonly SketchEntity[] LooseRectangleLines =
        [new SketchLine(0, 1), new SketchLine(2, 3), new SketchLine(4, 5), new SketchLine(6, 7)];

    private static Sketch Loose(params C[] constraints) => new(LooseRectangle, LooseRectangleLines, constraints);

    private static readonly C[] Corners = [C.Coincident(1, 2), C.Coincident(3, 4), C.Coincident(5, 6), C.Coincident(7, 0)];
    private static readonly C[] Axes = [C.Horizontal(0), C.Horizontal(2), C.Vertical(1), C.Vertical(3)];
    private static readonly C[] Sizes = [C.DistanceXOfLine(0, 2), C.DistanceYOfLine(1, 1)];

    // A rectangle with shared corners (4 points): bottom 0→1, right 1→2, top 2→3, left 3→0.
    private static Sketch Rectangle(SketchPoint[] points, params C[] constraints) =>
        new(points, [new SketchLine(0, 1), new SketchLine(1, 2), new SketchLine(2, 3), new SketchLine(3, 0)], constraints);

    private static SketchPoint[] RoughRectangle => [Pt(0, 0), Pt(2.1, 0.1), Pt(1.9, 1.2), Pt(0.1, 0.9)];

    private static C[] FullRectangle(double width = 2, double height = 1) =>
        [C.Horizontal(0), C.Horizontal(2), C.Vertical(1), C.Vertical(3), C.DistanceXOfLine(0, width), C.DistanceYOfLine(1, height), C.FixPoint(0)];

    [Fact]
    public void Rectangle_degrees_of_freedom_after_each_constraint()
    {
        var free = SketchSolver.Solve(Loose());
        Assert.Equal(16, free.DegreesOfFreedom);
        Assert.Equal(SolveStatus.Underconstrained, free.Status);

        var joined = SketchSolver.Solve(Loose([.. Corners]));
        Assert.Equal(8, joined.DegreesOfFreedom);
        At(joined, 1, joined.Points[2].X, joined.Points[2].Y);

        var square = SketchSolver.Solve(Loose([.. Corners, .. Axes]));
        Assert.Equal(4, square.DegreesOfFreedom);
        Assert.Equal(SolveStatus.Underconstrained, square.Status);

        var sized = SketchSolver.Solve(Loose([.. Corners, .. Axes, .. Sizes]));
        Assert.Equal(2, sized.DegreesOfFreedom);
        Assert.All(sized.PointIsFree, Assert.True); // free to slide as a whole
        Assert.Equal(16, sized.FreeUnknowns.Count);

        var held = SketchSolver.Solve(Loose([.. Corners, .. Axes, .. Sizes, C.FixPoint(0)]));
        Assert.Equal(0, held.DegreesOfFreedom);
        Assert.Equal(SolveStatus.Solved, held.Status);
        Assert.Empty(held.FreeUnknowns);
        Assert.All(held.PointIsFree, Assert.False);
        Assert.All(held.EntityIsFree, Assert.False);
        Assert.True(held.MaxResidual <= 1e-10);
        At(held, 0, 0, 0);
        At(held, 1, 2, 0);
        At(held, 3, 2, 1);
        At(held, 5, 0, 1);
        Near(2, held.Values[^3]);
        Near(1, held.Values[^2]);
    }

    [Fact]
    public void A_changed_dimension_moves_the_far_side_only()
    {
        var first = SketchSolver.Solve(Rectangle(RoughRectangle, FullRectangle()));
        Assert.Equal(SolveStatus.Solved, first.Status);
        var solved = first.Apply(Rectangle(RoughRectangle, FullRectangle()));
        var wider = SketchSolver.Solve(solved with { Constraints = FullRectangle(width: 3) });
        Assert.Equal(SolveStatus.Solved, wider.Status);
        At(wider, 0, 0, 0);
        At(wider, 3, 0, 1);
        At(wider, 1, 3, 0);
        At(wider, 2, 3, 1);
    }

    [Fact]
    public void Least_movement_when_underconstrained()
    {
        // Only the bottom is held horizontal: the solve moves the two ends of the bottom line toward each other's
        // height, half each, and nothing else.
        var points = new[] { Pt(0, 0), Pt(2, 0.2), Pt(2, 1), Pt(0, 1) };
        var r = SketchSolver.Solve(Rectangle(points, C.Horizontal(0)));
        Assert.Equal(SolveStatus.Underconstrained, r.Status);
        Assert.Equal(7, r.DegreesOfFreedom);
        At(r, 0, 0, 0.1);
        At(r, 1, 2, 0.1);
        At(r, 2, 2, 1);
        At(r, 3, 0, 1);
    }

    // A slot: two arcs of radius 1 joined by tangent lines. Points: 0 right centre, 1 right start (bottom), 2 right
    // end (top), 3 left centre, 4 left start (top), 5 left end (bottom). Entities: 0 right arc, 1 top line (2→4),
    // 2 left arc, 3 bottom line (5→1).
    private static Sketch Slot(SketchPoint[] points) => new(points,
        [new SketchArc(0, 1, 2, 1.2), new SketchLine(2, 4), new SketchArc(3, 4, 5, 0.9), new SketchLine(5, 1)],
        [
            C.TangentAt(0, 2, 1, 2), C.TangentAt(1, 4, 2, 4), C.TangentAt(2, 5, 3, 5), C.TangentAt(3, 1, 0, 1),
            C.Horizontal(1), C.Radius(0, 1), C.Equal(0, 2), C.Distance(0, 3, 4), C.FixPoint(0),
        ]);

    private static SketchPoint[] RoughSlot => [Pt(2, 0), Pt(2.1, -1.1), Pt(1.9, 1.2), Pt(-2.2, 0.1), Pt(-2, 1.1), Pt(-1.9, -0.8)];

    [Fact]
    public void A_slot_of_tangent_arcs_and_lines_solves_on_the_same_side()
    {
        var r = SketchSolver.Solve(Slot(RoughSlot));
        Assert.Equal(SolveStatus.Solved, r.Status);
        Assert.Equal(0, r.DegreesOfFreedom);
        At(r, 0, 2, 0);
        At(r, 3, -2, 0);
        At(r, 2, 2, 1);
        At(r, 4, -2, 1);
        At(r, 5, -2, -1);
        At(r, 1, 2, -1);
        Near(1, r.Radii[0]);
        Near(1, r.Radii[2]);
        Assert.True(double.IsNaN(r.Radii[1]));
    }

    [Fact]
    public void Tangent_along_keeps_the_side_it_starts_on()
    {
        // A circle above a horizontal fixed line and one below: each touches the line on its own side.
        var points = new[] { Pt(0, 0, true), Pt(10, 0, true), Pt(2, 0.7), Pt(6, -1.6) };
        var sketch = new Sketch(points,
            [new SketchLine(0, 1), new SketchCircle(2, 0.5), new SketchCircle(3, 1)],
            [C.Tangent(0, 1), C.Tangent(2, 0), C.Radius(1, 0.5), C.Radius(2, 1)]);
        var r = SketchSolver.Solve(sketch);
        Assert.True(r.Succeeded, r.Status.ToString());
        Near(0.5, r.Points[2].Y);
        Near(-1, r.Points[3].Y);
        Near(2, r.Points[2].X); // moved straight down, the least it could
        Near(6, r.Points[3].X);
    }

    [Fact]
    public void Arcs_joined_tangent_end_to_end_and_circles_tangent_along()
    {
        // An S: arc A about (0,0) from (1,0) up to (0,1) and arc B about (0,2) from (0,1) back to... the S's top half,
        // drawn roughly and joined tangent at A's end and B's start.
        var points = new[] { Pt(0, 0, true), Pt(1, 0), Pt(0.1, 1.05), Pt(0.05, 2.1), Pt(0, 0.95), Pt(-1, 2) };
        var sketch = new Sketch(points,
            [new SketchArc(0, 1, 2, 1), new SketchArc(3, 5, 4, 1)],
            [C.TangentAt(0, 2, 1, 4), C.Radius(0, 1), C.Radius(1, 1), C.HorizontalPoints(0, 1), C.VerticalPoints(0, 3)]);
        var r = SketchSolver.Solve(sketch);
        Assert.True(r.Succeeded, r.Status.ToString());
        At(r, 2, r.Points[4].X, r.Points[4].Y);
        // Both centres and the joint on one line; the centres 2 apart (outside each other, as drawn).
        var (cx, cy) = (r.Points[3].X - r.Points[0].X, r.Points[3].Y - r.Points[0].Y);
        var (jx, jy) = (r.Points[2].X - r.Points[0].X, r.Points[2].Y - r.Points[0].Y);
        Near(0, cx * jy - cy * jx);
        Near(2, Math.Sqrt(cx * cx + cy * cy));
        At(r, 3, 0, 2);
        At(r, 2, 0, 1);

        var circles = new Sketch([Pt(0, 0, true), Pt(2.5, 0.3)],
            [new SketchCircle(0, 1), new SketchCircle(1, 0.5)],
            [C.Tangent(0, 1), C.Radius(0, 1), C.Radius(1, 1), C.DistanceY(0, 1, 0)]);
        var t = SketchSolver.Solve(circles);
        Assert.True(t.Succeeded, t.Status.ToString());
        At(t, 1, 2, 0); // outside, as drawn
    }

    [Fact]
    public void Symmetric_midpoint_and_signed_angle()
    {
        // About a fixed vertical line x = 1.
        var sym = SketchSolver.Solve(new Sketch([Pt(1, -1), Pt(1, 3), Pt(0.3, 0.5, true), Pt(1.8, 0.7)],
            [new SketchLine(0, 1, Fixed: true)], [C.Symmetric(2, 3, 0)]));
        Assert.Equal(SolveStatus.Solved, sym.Status);
        At(sym, 3, 1.7, 0.5);

        var aboutPoint = SketchSolver.Solve(new Sketch([Pt(0, 0, true), Pt(3, 1, true), Pt(2, 2)], [],
            [C.SymmetricAboutPoint(0, 2, 1)]));
        At(aboutPoint, 2, 6, 2);

        var mid = SketchSolver.Solve(new Sketch([Pt(0, 0), Pt(4, 2), Pt(1, 1)], [new SketchLine(0, 1, Fixed: true)],
            [C.Midpoint(2, 0)]));
        Assert.Equal(SolveStatus.Solved, mid.Status);
        At(mid, 2, 2, 1);

        // 30° counter-clockwise from a fixed horizontal line, and −30° (330°) from the same start.
        Sketch Fan(double angle) => new([Pt(0, 0, true), Pt(1, 0, true), Pt(1, 0.3)],
            [new SketchLine(0, 1), new SketchLine(0, 2)], [C.Angle(0, 1, angle), C.Length(1, 2)]);
        var up = SketchSolver.Solve(Fan(Math.PI / 6));
        Assert.Equal(SolveStatus.Solved, up.Status);
        At(up, 2, 2 * Math.Cos(Math.PI / 6), 1);
        Near(Math.PI / 6, up.Values[0]);
        var down = SketchSolver.Solve(Fan(-Math.PI / 6));
        Assert.Equal(SolveStatus.Solved, down.Status);
        At(down, 2, 2 * Math.Cos(Math.PI / 6), -1);
        Near(2 * Math.PI - Math.PI / 6, down.Values[0]);
    }

    [Fact]
    public void Conflicts_are_found_named_and_refused()
    {
        // A fully constrained rectangle told its top is also vertical.
        var constraints = FullRectangle().Append(C.Vertical(2)).ToArray();
        var r = SketchSolver.Solve(Rectangle(RoughRectangle, constraints));
        Assert.Equal(SolveStatus.Conflicting, r.Status);
        output.WriteLine($"rectangle + vertical top: conflicting {string.Join(", ", r.Conflicting)}");
        Assert.False(r.Succeeded);
        Assert.Contains(constraints.Length - 1, r.Conflicting);
        Assert.Equal(RoughRectangle, r.Points); // refused: nothing moved

        // Two different distances between the same two points.
        var two = SketchSolver.Solve(new Sketch([Pt(0, 0, true), Pt(1.5, 0)], [], [C.Distance(0, 1, 1), C.Distance(0, 1, 2)]));
        Assert.Equal(SolveStatus.Conflicting, two.Status);
        Assert.Equal(new[] { 0, 1 }, two.Conflicting);
        Near(1.5, two.Points[1].X);

        // Two held points told to be 3 apart while they are 1 apart.
        var held = SketchSolver.Solve(new Sketch([Pt(0, 0, true), Pt(1, 0, true)], [], [C.Distance(0, 1, 3)]));
        Assert.Equal(SolveStatus.Conflicting, held.Status);
        Assert.Equal(new[] { 0 }, held.Conflicting);
    }

    [Fact]
    public void A_redundant_constraint_is_named_and_the_solve_succeeds()
    {
        var constraints = FullRectangle().Append(C.Parallel(1, 3)).ToArray();
        var r = SketchSolver.Solve(Rectangle(RoughRectangle, constraints));
        Assert.Equal(SolveStatus.Redundant, r.Status);
        Assert.True(r.Succeeded);
        Assert.Equal(new[] { constraints.Length - 1 }, r.Redundant);
        Assert.Empty(r.Conflicting);
        Assert.Equal(0, r.DegreesOfFreedom);
        At(r, 2, 2, 1);
    }

    [Fact]
    public void A_bad_start_converges_to_the_constrained_shape()
    {
        // A 4 × 2 rectangle with a diagonal, a circle at the diagonal's middle touching the bottom: drawn with every
        // point thrown off by up to half the sizes.
        var points = new[] { Pt(0, 0), Pt(5.9, 0.8), Pt(2.6, 2.9), Pt(-1.2, 1.1), Pt(1.1, 0.4) };
        var sketch = new Sketch(points,
            [new SketchLine(0, 1), new SketchLine(1, 2), new SketchLine(2, 3), new SketchLine(3, 0), new SketchLine(0, 2), new SketchCircle(4, 0.3)],
            [
                C.Horizontal(0), C.Horizontal(2), C.Vertical(1), C.Vertical(3), C.Length(0, 4), C.Length(3, 2), C.FixPoint(0),
                C.Midpoint(4, 4), C.Tangent(0, 5),
            ]);
        var r = SketchSolver.Solve(sketch);
        Assert.Equal(SolveStatus.Solved, r.Status);
        At(r, 0, 0, 0);
        At(r, 1, 4, 0);
        At(r, 2, 4, 2);
        At(r, 3, 0, 2);
        At(r, 4, 2, 1);
        Near(1, r.Radii[5]);
    }

    [Fact]
    public void The_same_input_gives_the_same_output_bit_for_bit()
    {
        var a = SketchSolver.Solve(Slot(RoughSlot));
        var b = SketchSolver.Solve(Slot(RoughSlot));
        Assert.Equal(a.Iterations, b.Iterations);
        for (int i = 0; i < a.Points.Count; i++)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Points[i].X), BitConverter.DoubleToInt64Bits(b.Points[i].X));
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Points[i].Y), BitConverter.DoubleToInt64Bits(b.Points[i].Y));
        }
        for (int i = 0; i < a.Radii.Count; i++)
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Radii[i]), BitConverter.DoubleToInt64Bits(b.Radii[i]));
        Assert.Equal(BitConverter.DoubleToInt64Bits(a.MaxResidual), BitConverter.DoubleToInt64Bits(b.MaxResidual));
    }

    [Fact]
    public void A_dragged_corner_follows_the_cursor_as_far_as_the_constraints_allow()
    {
        var square = new[] { Pt(0, 0), Pt(2, 0), Pt(2, 1), Pt(0, 1) };
        C[] axes = [C.Horizontal(0), C.Horizontal(2), C.Vertical(1), C.Vertical(3), C.FixPoint(0)];

        // Free in both directions: the corner lands on the cursor, its neighbours follow, the held corner stays.
        var free = SketchSolver.Solve(Rectangle(square, axes), new SolverOptions { DragGoals = [new DragGoal(2, 5, 3)] });
        Assert.True(free.Succeeded);
        At(free, 2, 5, 3);
        At(free, 1, 5, 0);
        At(free, 3, 0, 3);
        At(free, 0, 0, 0);

        // With the width dimensioned the corner can only go up and down: it follows the cursor's height.
        var sized = SketchSolver.Solve(Rectangle(square, [.. axes, C.DistanceXOfLine(0, 2)]),
            new SolverOptions { DragGoals = [new DragGoal(2, 5, 3)] });
        Assert.True(sized.Succeeded);
        At(sized, 2, 2, 3, 1e-6);
        At(sized, 1, 2, 0);
        At(sized, 3, 0, 3, 1e-6);
    }

    [Fact]
    public void A_driven_dimension_is_measured_not_enforced()
    {
        var constraints = FullRectangle().Append(C.Distance(0, 2, 99, driving: false)).ToArray();
        var r = SketchSolver.Solve(Rectangle(RoughRectangle, constraints));
        Assert.Equal(SolveStatus.Solved, r.Status);
        Near(Math.Sqrt(5), r.Values[^1]);
        At(r, 2, 2, 1);
        Assert.True(double.IsNaN(r.Values[0])); // horizontal: no value
    }

    [Fact]
    public void Every_kind_holds_after_a_solve_and_its_derivatives_are_exact()
    {
        // Points: 0..1 line A, 2..3 line B, 4 circle centre, 5..7 arc (centre, start, end), 8 loose, 9..10 line C.
        var points = new[]
        {
            Pt(0.1, 0.2), Pt(3.2, 0.5), Pt(0.4, 1.1), Pt(2.9, 2.3), Pt(5.1, 1.2), Pt(1.1, -3.2), Pt(2.2, -3.1), Pt(0.9, -2.0),
            Pt(4.4, 4.3), Pt(-1, 3), Pt(-0.8, 5.1),
        };
        var entities = new SketchEntity[]
        {
            new SketchLine(0, 1), new SketchLine(2, 3), new SketchCircle(4, 0.7), new SketchArc(5, 6, 7, 1.05), new SketchLine(9, 10),
        };
        C[] constraints =
        [
            C.FixPoint(0), C.AngleToX(0, 0.1), C.Length(0, 3), C.Parallel(0, 1), C.DistancePointLine(2, 0, 1),
            C.Equal(0, 1), C.DistanceX(0, 2, 0.5), C.PointOn(4, 1), C.Diameter(2, 1.4), C.PointOn(8, 2),
            C.DistanceY(4, 8, 0.4), C.Tangent(4, 3), C.Radius(3, 1), C.DistanceXFromOrigin(5, 1), C.DistanceYFromOrigin(5, -3),
            C.Angle(0, 4, 1.4), C.Equal(4, 0), C.Coincident(9, 9), C.DistanceYFromOrigin(9, -3), C.Perpendicular(4, 4) with { Driving = false },
            C.DistanceYOfLine(4, 0, driving: false), C.FixEntity(2) with { Driving = false },
        ];
        var sketch = new Sketch(points, entities, constraints);
        Assert.True(SketchSolver.JacobianError(sketch) < 1e-6);
        var r = SketchSolver.Solve(sketch);
        Assert.True(r.Succeeded, $"{r.Status}: conflicting {string.Join(",", r.Conflicting)}");
        output.WriteLine($"every kind: {r.Status}, DOF {r.DegreesOfFreedom}, redundant {string.Join(", ", r.Redundant)}, {r.Iterations} iterations");
        Assert.True(r.MaxResidual <= 1e-10);
        Near(3, r.Values[2]);
        Near(1.4, r.Values[8]);
        Near(1, r.Radii[3]);

        // The derivatives of the remaining forms (symmetric, midpoint, tangent at joints, perpendicular, fix entity).
        var more = new Sketch(points, entities,
        [
            C.Symmetric(8, 4, 0), C.SymmetricAboutPoint(8, 4, 2), C.Midpoint(8, 1), C.Perpendicular(0, 4), C.FixEntity(3),
            C.TangentAt(0, 1, 3, 6), C.TangentAt(3, 7, 2, 4), C.Tangent(2, 3), C.Distance(8, 9, 2), C.Vertical(1), C.HorizontalPoints(8, 9),
        ]);
        Assert.True(SketchSolver.JacobianError(more) < 1e-6);
    }

    [Fact]
    public void Bad_references_are_refused()
    {
        Assert.Throws<ArgumentException>(() => SketchSolver.Solve(new Sketch([Pt(0, 0)], [], [C.Coincident(0, 3)])));
        Assert.Throws<ArgumentException>(() => SketchSolver.Solve(new Sketch([Pt(0, 0), Pt(1, 0)], [new SketchLine(0, 1)], [C.Radius(0, 1)])));
        Assert.Throws<ArgumentException>(() => SketchSolver.Solve(new Sketch([Pt(0, 0), Pt(1, 0), Pt(1, 1)],
            [new SketchLine(0, 1), new SketchLine(1, 2)], [C.Tangent(0, 1)])));
    }

    [Fact]
    public void Five_hundred_lines_in_one_cluster_solve_quickly()
    {
        // A strip of 167 cells sharing their sides: 502 lines, 336 points (672 unknowns), all one cluster.
        const int cells = 167;
        var points = new List<SketchPoint>();
        for (int i = 0; i <= cells; i++)
        {
            double wobble = 0.03 * Math.Sin(i * 1.7);
            points.Add(Pt(i + wobble, 0.02 * Math.Sin(i)));
            points.Add(Pt(i - wobble, 1 + 0.03 * Math.Sin(i * 0.9)));
        }
        var lines = new List<SketchEntity>();
        var constraints = new List<C> { C.FixPoint(0) };
        for (int i = 0; i <= cells; i++)
        {
            lines.Add(new SketchLine(2 * i, 2 * i + 1));
            constraints.Add(C.Vertical(lines.Count - 1));
            if (i == 0) constraints.Add(C.Length(lines.Count - 1, 1));
            if (i == cells) break;
            lines.Add(new SketchLine(2 * i, 2 * i + 2));
            constraints.Add(C.Horizontal(lines.Count - 1));
            constraints.Add(C.DistanceXOfLine(lines.Count - 1, 1 + 0.01 * i));
            lines.Add(new SketchLine(2 * i + 1, 2 * i + 3));
            constraints.Add(C.Horizontal(lines.Count - 1));
        }
        Assert.Equal(502, lines.Count);
        var sketch = new Sketch(points, lines, constraints);
        SketchSolver.Solve(sketch); // warm up the JIT
        var clock = Stopwatch.StartNew();
        var r = SketchSolver.Solve(sketch);
        clock.Stop();
        Assert.Equal(SolveStatus.Solved, r.Status);
        Assert.Equal(0, r.DegreesOfFreedom);
        output.WriteLine($"{clock.Elapsed.TotalMilliseconds:F0} ms, {r.Iterations} iterations");
        Assert.True(clock.Elapsed.TotalSeconds < 2, $"{clock.Elapsed.TotalMilliseconds:F0} ms");
        double x = 0;
        for (int i = 0; i < cells; i++) x += 1 + 0.01 * i;
        At(r, 2 * cells + 1, x, 1);
    }

    [Fact]
    public void A_thousand_lines_in_separate_rectangles_solve_quickly()
    {
        var points = new List<SketchPoint>();
        var lines = new List<SketchEntity>();
        var constraints = new List<C>();
        for (int k = 0; k < 250; k++)
        {
            int p = points.Count, l = lines.Count;
            double ox = 3 * (k % 25), oy = 2 * (k / 25), s = 0.05 * Math.Sin(k);
            points.AddRange([Pt(ox, oy), Pt(ox + 2 + s, oy + s), Pt(ox + 2 - s, oy + 1 + s), Pt(ox + s, oy + 1 - s)]);
            for (int i = 0; i < 4; i++) lines.Add(new SketchLine(p + i, p + (i + 1) % 4));
            constraints.AddRange([C.Horizontal(l), C.Horizontal(l + 2), C.Vertical(l + 1), C.Vertical(l + 3),
                C.DistanceXOfLine(l, 2), C.DistanceYOfLine(l + 1, 1), C.FixPoint(p)]);
        }
        var sketch = new Sketch(points, lines, constraints);
        SketchSolver.Solve(sketch);
        var clock = Stopwatch.StartNew();
        var r = SketchSolver.Solve(sketch);
        clock.Stop();
        Assert.Equal(SolveStatus.Solved, r.Status);
        output.WriteLine($"{clock.Elapsed.TotalMilliseconds:F0} ms, {r.Iterations} iterations");
        Assert.True(clock.Elapsed.TotalSeconds < 2, $"{clock.Elapsed.TotalMilliseconds:F0} ms");
        At(r, 4 * 249 + 2, 3 * 24 + 2, 2 * 9 + 1);
    }
}
