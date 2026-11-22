using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// B-spline and NURBS curves and surfaces (docs/plan-studio-freeform.md §9, F2a), checked against the worked examples of Piegl &amp;
/// Tiller, <i>The NURBS Book</i> (2nd ed.): the basis functions of Ex2.3 and their derivatives (Ex2.4), knot insertion (Ex5.1), global
/// interpolation (Ex9.1); the exact circle of chapters 1 and 7; degree elevation against the Bézier formula (eq. 5.36).
/// </summary>
public class BSplineTests
{
    static readonly double[] Ex23 = [0, 0, 0, 1, 2, 3, 4, 4, 5, 5, 5];

    static void Near(double a, double b, double tol = 1e-12) => Assert.True(Math.Abs(a - b) <= tol, $"{a} is not {b} (within {tol})");
    static void Near(Vec3 a, Vec3 b, double tol = 1e-12) => Assert.True((a - b).Length <= tol, $"{a} is not {b} (within {tol})");

    // ------------------------------------------------------------ chapter 2

    [Fact]
    public void Ex2_3_the_quadratic_basis_at_u_five_halves_is_one_eighth_six_eighths_one_eighth()
    {
        // U = {0,0,0,1,2,3,4,4,5,5,5}, p = 2, u = 5/2: span 4, N[2,2] = 1/8, N[3,2] = 6/8, N[4,2] = 1/8.
        var span = BSpline.FindSpan(7, 2, 2.5, Ex23);
        Assert.Equal(4, span);
        var N = BSpline.BasisFuns(span, 2.5, 2, Ex23);
        Near(N[0], 1.0 / 8);
        Near(N[1], 6.0 / 8);
        Near(N[2], 1.0 / 8);
    }

    [Fact]
    public void Ex2_4_the_derivatives_at_u_five_halves_are_minus_one_half_zero_one_half()
    {
        var ders = BSpline.DersBasisFuns(4, 2.5, 2, 2, Ex23);
        Near(ders[1, 0], -0.5);
        Near(ders[1, 1], 0);
        Near(ders[1, 2], 0.5);
        // The second derivatives of (3 − u)²/2, the middle piece −(u − 5/2)² + 3/4 and (u − 2)²/2: 1, −2, 1.
        Near(ders[2, 0], 1);
        Near(ders[2, 1], -2);
        Near(ders[2, 2], 1);
    }

    [Fact]
    public void The_spans_at_the_ends_and_at_repeated_knots_are_the_last_non_empty_ones()
    {
        Assert.Equal(2, BSpline.FindSpan(7, 2, 0, Ex23));
        Assert.Equal(7, BSpline.FindSpan(7, 2, 5, Ex23));
        // u = 4, a double knot: U[6] = U[7] = 4 ≤ u < U[8] = 5.
        Assert.Equal(7, BSpline.FindSpan(7, 2, 4, Ex23));
        // The basis sums to one everywhere.
        for (var u = 0.0; u <= 5; u += 0.125)
        {
            var s = BSpline.FindSpan(7, 2, u, Ex23);
            Near(BSpline.BasisFuns(s, u, 2, Ex23).Sum(), 1);
        }
    }

    // ------------------------------------------------------------ chapters 3 and 4

    static Vec3[] Eight() => [new(0, 0, 0), new(1, 2, 0), new(3, 3, 1), new(4, 1, 2), new(6, 0, 1), new(7, 2, 0), new(8, 4, 1), new(9, 3, 0)];

    [Fact]
    public void A_curve_point_is_the_basis_blend_of_its_control_points()
    {
        var p = Eight();
        var c = new BSplineCurve(2, Ex23, p);
        Near(c.At(2.5), p[2] * (1.0 / 8) + p[3] * (6.0 / 8) + p[4] * (1.0 / 8));
        Near(c.At(0), p[0]);
        Near(c.At(5), p[7]);
    }

    [Fact]
    public void Derivatives_agree_with_differences_for_plain_and_rational_curves()
    {
        var plain = new BSplineCurve(2, Ex23, Eight());
        var rational = new BSplineCurve(2, Ex23, Eight(), [1, 0.5, 2, 1, 0.7, 1.3, 1, 1]);
        foreach (var c in new[] { plain, rational })
            foreach (var u in new[] { 0.3, 1.7, 2.5, 3.9 })
            {
                var d = c.Derivatives(u, 2);
                const double h = 1e-5;
                var d1 = (c.At(u + h) - c.At(u - h)) * (1 / (2 * h));
                var d2 = (c.At(u + h) - c.At(u) * 2 + c.At(u - h)) * (1 / (h * h));
                Near(d[0], c.At(u));
                Near(d[1], d1, 1e-6);
                Near(d[2], d2, 1e-3);
            }
    }

    [Fact]
    public void The_quarter_circle_of_chapter_one_weights_one_one_two_is_exact()
    {
        // P = (1,0), (1,1), (0,1), w = 1, 1, 2: C(u) = ((1 − u²), 2u) / (1 + u²).
        var c = new BSplineCurve(2, [0, 0, 0, 1, 1, 1], [new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], [1, 1, 2]);
        for (var u = 0.0; u <= 1 + 1e-12; u += 1.0 / 64)
        {
            var q = c.At(u);
            Near(q.Length, 1);
            Near(q, new Vec3((1 - u * u) / (1 + u * u), 2 * u / (1 + u * u), 0));
        }
    }

    [Fact]
    public void A7_1_arcs_are_exact_circles_to_1e_12_with_the_cosine_weights_of_chapter_seven()
    {
        var quarter = BSplineCurve.Arc(Vec3.Zero, new Vec3(1, 0, 0), new Vec3(0, 1, 0), 1, 0, Math.PI / 2);
        Assert.Equal(3, quarter.Points.Length);
        Near(quarter.Weight(1), Math.Sqrt(2) / 2);
        Near(quarter.Points[1], new Vec3(1, 1, 0));
        foreach (var (arc, centre, radius) in new[] { (quarter, Vec3.Zero, 1.0), (BSplineCurve.Arc(new Vec3(2, -1, 3), new Vec3(0, 1, 0), new Vec3(0, 0, 1), 2.5, 0.3, 2 * Math.PI), new Vec3(2, -1, 3), 2.5),
            (BSplineCurve.Arc(Vec3.Zero, new Vec3(1, 0, 0), new Vec3(0, 1, 0), 0.75, 1, -2.2), Vec3.Zero, 0.75) })
        {
            for (var k = 0; k <= 200; k++)
            {
                var u = k / 200.0;
                Near((arc.At(u) - centre).Length, radius);
                // The tangent stays at right angles to the radius.
                Near(Vec3.Dot(arc.Tangent(u), (arc.At(u) - centre).Normalized()), 0, 1e-10);
            }
        }
        // A whole circle closes on itself, in four quarters.
        var whole = BSplineCurve.Arc(Vec3.Zero, new Vec3(1, 0, 0), new Vec3(0, 1, 0), 1, 0, 2 * Math.PI);
        Assert.Equal(9, whole.Points.Length);
        Near(whole.At(0), whole.At(1));
        Near(whole.At(0.25), new Vec3(0, 1, 0));
    }

    // ------------------------------------------------------------ chapter 5

    [Fact]
    public void Ex5_1_inserting_u_five_halves_in_a_cubic_blends_three_control_points()
    {
        // p = 3, U = {0,0,0,0,1,2,3,4,5,5,5,5}: Q3 = 5/6 P3 + 1/6 P2, Q4 = 1/2 P4 + 1/2 P3, Q5 = 1/6 P5 + 5/6 P4; the rest kept.
        double[] U = [0, 0, 0, 0, 1, 2, 3, 4, 5, 5, 5, 5];
        var p = Eight();
        var c = new BSplineCurve(3, U, p);
        var q = c.InsertKnot(2.5);
        Assert.Equal([0, 0, 0, 0, 1, 2, 2.5, 3, 4, 5, 5, 5, 5], q.Knots);
        Assert.Equal(9, q.Points.Length);
        for (var i = 0; i <= 2; i++) Near(q.Points[i], p[i]);
        Near(q.Points[3], p[3] * (5.0 / 6) + p[2] * (1.0 / 6));
        Near(q.Points[4], p[4] * 0.5 + p[3] * 0.5);
        Near(q.Points[5], p[5] * (1.0 / 6) + p[4] * (5.0 / 6));
        for (var i = 6; i <= 8; i++) Near(q.Points[i], p[i - 1]);
        for (var u = 0.0; u <= 5; u += 0.1) Near(q.At(u), c.At(u), 1e-12);
    }

    [Fact]
    public void Inserting_and_splitting_keep_a_rational_curve_where_it_was()
    {
        var c = new BSplineCurve(2, Ex23, Eight(), [1, 0.5, 2, 1, 0.7, 1.3, 1, 1]);
        var twice = c.InsertKnot(1.5, 2);
        Assert.Equal(2, twice.Knots.Count(k => k == 1.5));
        var (a, b) = c.Split(2.25);
        for (var u = 0.0; u <= 5; u += 0.05)
        {
            Near(twice.At(u), c.At(u), 1e-12);
            Near(u <= 2.25 ? a.At(u) : b.At(u), c.At(u), 1e-12);
        }
        Near(a.At(2.25), b.At(2.25), 1e-12);
        // At a knot already there (u = 4, double): split once more to the degree.
        var (c1, c2) = c.Split(4);
        Near(c1.At(4), c.At(4), 1e-12);
        Near(c2.At(4.5), c.At(4.5), 1e-12);
    }

    [Fact]
    public void Eq5_36_a_bezier_curve_raised_a_degree_blends_its_neighbours()
    {
        // Q_i = (i / (p + 1)) P_(i−1) + (1 − i / (p + 1)) P_i.
        Vec3[] p = [new(0, 0, 0), new(1, 3, 0), new(4, 3, 2), new(5, 0, 1)];
        var c = new BSplineCurve(3, [0, 0, 0, 0, 1, 1, 1, 1], p);
        var e = c.ElevateDegree();
        Assert.Equal(4, e.Degree);
        Assert.Equal(5, e.Points.Length);
        for (var i = 0; i <= 4; i++)
        {
            var a = i / 4.0;
            var expect = (i > 0 ? p[i - 1] * a : Vec3.Zero) + (i < 4 ? p[i] * (1 - a) : Vec3.Zero);
            Near(e.Points[i], expect, 1e-12);
        }
    }

    [Fact]
    public void Degree_elevation_keeps_multi_span_and_rational_curves_and_their_knots_continuity()
    {
        var c = new BSplineCurve(2, Ex23, Eight());
        var e = c.ElevateDegree(2);
        Assert.Equal(4, e.Degree);
        // Every distinct knot's multiplicity raised by 2: {0×5, 1×3, 2×3, 3×3, 4×4, 5×5}.
        var (values, mults) = BSpline.Distinct(e.Knots);
        Assert.Equal([0, 1, 2, 3, 4, 5], values);
        Assert.Equal([5, 3, 3, 3, 4, 5], mults);
        for (var u = 0.0; u <= 5; u += 0.05) Near(e.At(u), c.At(u), 1e-10);
        var circle = BSplineCurve.Arc(Vec3.Zero, new Vec3(1, 0, 0), new Vec3(0, 1, 0), 2, 0, 2 * Math.PI).ElevateDegree();
        Assert.True(circle.Rational);
        for (var u = 0.0; u <= 1; u += 0.01) Near(circle.At(u).Length, 2, 1e-10);
    }

    // ------------------------------------------------------------ chapter 9

    [Fact]
    public void Ex9_1_the_cubic_through_five_points_has_chord_length_parameters_and_the_knot_28_over_51()
    {
        Vec3[] q = [new(0, 0, 0), new(3, 4, 0), new(-1, 4, 0), new(-4, 0, 0), new(-4, -3, 0)];
        var t = BSpline.ChordLength(q);
        Near(t[1], 5.0 / 17);
        Near(t[2], 9.0 / 17);
        Near(t[3], 14.0 / 17);
        var c = BSplineCurve.Interpolate(q, 3);
        Assert.Equal(3, c.Degree);
        Assert.Equal(9, c.Knots.Length);
        Near(c.Knots[4], 28.0 / 51);
        for (var k = 0; k < q.Length; k++) Near(c.At(t[k]), q[k], 1e-12);
    }

    [Fact]
    public void An_interpolation_of_few_points_lowers_its_degree_and_still_passes_through_them()
    {
        Vec3[] q = [new(0, 0, 0), new(1, 1, 0), new(2, 0, 1)];
        var c = BSplineCurve.Interpolate(q, 3);
        Assert.Equal(2, c.Degree);
        var t = BSpline.ChordLength(q);
        for (var k = 0; k < q.Length; k++) Near(c.At(t[k]), q[k], 1e-12);
    }

    [Fact]
    public void Compatible_curves_share_degree_and_knots_and_stay_where_they_were()
    {
        var line = BSplineCurve.Line(new Vec3(0, 0, 0), new Vec3(2, 0, 0));
        var arc = BSplineCurve.Arc(Vec3.Zero, new Vec3(1, 0, 0), new Vec3(0, 1, 0), 1, 0, Math.PI);
        var cubic = new BSplineCurve(3, [0, 0, 0, 0, 0.3, 1, 1, 1, 1], [new(0, 0, 1), new(1, 1, 1), new(2, 1, 1), new(3, 0, 1), new(4, 0, 1)]);
        var cs = BSplineCurve.Compatible([line, arc, cubic]);
        Assert.All(cs, c => Assert.Equal(3, c.Degree));
        Assert.All(cs, c => Assert.Equal(cs[0].Knots, c.Knots));
        for (var u = 0.0; u <= 1; u += 0.05)
        {
            Near(cs[0].At(u), line.At(u), 1e-10);
            Near(cs[1].At(u), arc.At(u), 1e-10);
            Near(cs[2].At(u), cubic.At(u), 1e-10);
        }
    }

    // ------------------------------------------------------------ the chord rule

    [Fact]
    public void A_circle_sampled_by_the_chord_rule_strays_no_more_than_the_tolerance()
    {
        foreach (var r in new[] { 0.05, 0.5, 3.0, 40.0 })
        {
            var c = BSplineCurve.Arc(Vec3.Zero, new Vec3(1, 0, 0), new Vec3(0, 1, 0), r, 0, 2 * Math.PI);
            var pts = c.Polyline();
            var worst = 0.0;
            for (var i = 0; i + 1 < pts.Count; i++) worst = Math.Max(worst, r - ((pts[i] + pts[i + 1]) * 0.5).Length);
            Assert.True(worst <= Chords.Tolerance, $"r = {r}: a chord strays {worst * 1000:0.###} mm");
            // Not much finer than the circle Chords makes (the parameter is not the angle, so a little more).
            Assert.InRange(pts.Count - 1, Chords.Circle(r) - 1, Chords.Circle(r) * 2.2);
        }
        // A straight line stays one piece; its corners stay.
        Assert.Equal(2, BSplineCurve.Line(Vec3.Zero, new Vec3(5, 0, 0)).Polyline().Count);
    }

    // ------------------------------------------------------------ surfaces

    [Fact]
    public void Surface_derivatives_and_normals_agree_with_differences_for_a_rational_surface()
    {
        var pts = new Vec3[4, 3];
        var w = new double[4, 3];
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 3; j++) { pts[i, j] = new Vec3(i, j, Math.Sin(i + 2 * j)); w[i, j] = 1 + 0.3 * ((i + j) % 3); }
        var s = new BSplineSurface(3, 2, [0, 0, 0, 0, 1, 1, 1, 1], [0, 0, 0, 1, 1, 1], pts, w);
        foreach (var (u, v) in new[] { (0.2, 0.3), (0.5, 0.5), (0.9, 0.1) })
        {
            var d = s.Derivatives(u, v, 2);
            const double h = 1e-5;
            Near(d[0, 0], s.At(u, v));
            Near(d[1, 0], (s.At(u + h, v) - s.At(u - h, v)) * (1 / (2 * h)), 1e-6);
            Near(d[0, 1], (s.At(u, v + h) - s.At(u, v - h)) * (1 / (2 * h)), 1e-6);
            Near(d[1, 1], (s.At(u + h, v + h) - s.At(u + h, v - h) - s.At(u - h, v + h) + s.At(u - h, v - h)) * (1 / (4 * h * h)), 1e-3);
            Near(d[2, 0], (s.At(u + h, v) - s.At(u, v) * 2 + s.At(u - h, v)) * (1 / (h * h)), 1e-3);
            Near(s.Normal(u, v), Vec3.Cross(d[1, 0], d[0, 1]).Normalized());
        }
        // Iso-curves are the surface's own lines; a split keeps both halves on it.
        var iso = s.IsoV(0.37);
        var isoU = s.IsoU(0.61);
        var (a, b) = s.SplitU(0.4);
        for (var t = 0.0; t <= 1; t += 0.1)
        {
            Near(iso.At(t), s.At(t, 0.37), 1e-12);
            Near(isoU.At(t), s.At(0.61, t), 1e-12);
            Near(t <= 0.4 ? a.At(t, 0.7) : b.At(t, 0.7), s.At(t, 0.7), 1e-12);
        }
    }

    [Fact]
    public void A8_1_revolving_a_line_and_an_arc_makes_an_exact_cylinder_cone_and_sphere()
    {
        var axis = (P: new Vec3(1, 2, 0), D: new Vec3(0, 0, 1));
        var cylinder = BSplineSurface.Revolve(BSplineCurve.Line(new Vec3(3, 2, 0), new Vec3(3, 2, 4)), axis.P, axis.D, 2 * Math.PI);
        var cone = BSplineSurface.Revolve(BSplineCurve.Line(new Vec3(3, 2, 0), new Vec3(1.5, 2, 3)), axis.P, axis.D, Math.PI);
        var sphere = BSplineSurface.Revolve(BSplineCurve.Arc(axis.P, new Vec3(0, 0, -1), new Vec3(1, 0, 0), 1.5, 0, Math.PI), axis.P, axis.D, 2 * Math.PI);
        static double Radial(Vec3 p) => new Vec2(p.X - 1, p.Y - 2).Length;
        for (var u = 0.0; u <= 1; u += 0.05)
            for (var v = 0.0; v <= 1; v += 0.1)
            {
                Near(Radial(cylinder.At(u, v)), 2);
                var c = cone.At(u, v);
                Near(Radial(c), 2 - 0.5 * c.Z);
                var s = sphere.At(u, v);
                Near((s - axis.P).Length, 1.5);
                if (v is > 0.05 and < 0.95) Near(Math.Abs(Vec3.Dot(sphere.Normal(u, v), (s - axis.P).Normalized())), 1, 1e-9);
            }
    }

    [Fact]
    public void Skinning_passes_through_every_section()
    {
        var sections = new[]
        {
            BSplineCurve.Arc(new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), 1, 0, 2 * Math.PI),
            BSplineCurve.Arc(new Vec3(0.3, 0, 1), new Vec3(1, 0, 0), new Vec3(0, 1, 0), 1.6, 0, 2 * Math.PI),
            BSplineCurve.Arc(new Vec3(0, 0.2, 2.5), new Vec3(1, 0, 0), new Vec3(0, 1, 0), 0.8, 0, 2 * Math.PI),
        };
        var s = BSplineSurface.Skin(BSplineCurve.Compatible(sections), 3);
        Assert.Equal(2, s.DegreeV);
        var (v0, v1) = s.DomainV;
        // The sections' parameters across: the first, the averaged middle, the last.
        var (values, _) = BSpline.Distinct(s.KnotsV);
        Assert.Equal(2, values.Length);
        // Each section is an iso-curve of the surface; the middle one at its own parameter (where the surface passes through it).
        for (var u = 0.0; u <= 1; u += 0.05)
        {
            Near(s.At(u, v0), sections[0].At(u), 1e-10);
            Near(s.At(u, v1), sections[2].At(u), 1e-10);
        }
        // Given parameters across, the middle section is the iso-curve there.
        var given = BSplineSurface.Skin(BSplineCurve.Compatible(sections), 3, [0, 0.4, 1]);
        for (var u = 0.0; u <= 1; u += 0.05) Near(given.At(u, 0.4), sections[1].At(u), 1e-10);
    }

    [Fact]
    public void A_uniform_bicubic_patch_in_bezier_form_is_the_same_surface()
    {
        var net = new Vec3[4, 4];
        var rnd = new Random(7);
        for (var i = 0; i < 4; i++) for (var j = 0; j < 4; j++) net[i, j] = new Vec3(i + rnd.NextDouble() * 0.3, j, rnd.NextDouble());
        var bez = BSplineSurface.UniformBicubic(net);
        double[] uniform = [0, 1, 2, 3, 4, 5, 6, 7];
        var direct = new BSplineSurface(3, 3, uniform, uniform, net);
        for (var u = 0.0; u <= 1; u += 0.125)
            for (var v = 0.0; v <= 1; v += 0.125) Near(bez.At(u, v), direct.At(3 + u, 3 + v), 1e-12);
    }

    [Fact]
    public void A_surface_grid_by_the_chord_rule_keeps_its_cells_within_the_tolerance()
    {
        var sphere = BSplineSurface.Revolve(BSplineCurve.Arc(Vec3.Zero, new Vec3(0, 0, -1), new Vec3(1, 0, 0), 2, 0, Math.PI), Vec3.Zero, Vec3.UnitZ, 2 * Math.PI);
        var (us, vs) = sphere.Grid();
        var worst = 0.0;
        for (var i = 0; i + 1 < us.Count; i++)
            for (var j = 0; j + 1 < vs.Count; j++)
            {
                var m = (sphere.At(us[i], vs[j]) + sphere.At(us[i + 1], vs[j]) + sphere.At(us[i + 1], vs[j + 1]) + sphere.At(us[i], vs[j + 1])) * 0.25;
                worst = Math.Max(worst, 2 - m.Length);
            }
        Assert.True(worst <= 2 * Chords.Tolerance, $"a cell strays {worst * 1000:0.###} mm");
        Assert.True(us.Count < 200 && vs.Count < 120);
    }
}
