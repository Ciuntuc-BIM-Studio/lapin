using Lapin.Alignments;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// The IFC 4.3 alignment core (docs/plan-rail.md, R1), ported from BubbleBIM
/// Standalone with its tests: the transition shapes, the horizontal, vertical
/// and cant evaluators, continuity, the inverse (station of a point), adaptive
/// sampling, the PI model's round trip and the STEP reader.
/// </summary>
public class AlignmentTests
{
    static void Close(double a, double b, double eps) => Assert.True(Math.Abs(a - b) < eps, $"|{a} − {b}| = {Math.Abs(a - b)} ≥ {eps}");

    static readonly HorizontalType[] Transitional = [HorizontalType.Clothoid, HorizontalType.HelmertCurve, HorizontalType.BlossCurve, HorizontalType.CosineCurve, HorizontalType.SineCurve, HorizontalType.VienneseBend];
    static readonly HorizontalType[] AllHorizontal = Enum.GetValues<HorizontalType>();

    /// <summary>A chain of segments, each placed at the computed end of the previous one.</summary>
    static List<HorizontalSegment> Chain((HorizontalType Type, double R1, double R2, double L)[] parts, Vec2? start = null, double dir = 0.3)
    {
        var o = new List<HorizontalSegment>();
        var p = start ?? new Vec2(10, 20); var d = dir;
        foreach (var (type, r1, r2, l) in parts)
        {
            var seg = new HorizontalSegment(type, p, d, r1, r2, l);
            o.Add(seg);
            var e = PreparedHorizontal.Evaluate(seg, l);
            p = e.P; d = e.Direction;
        }
        return o;
    }

    [Fact]
    public void Transition_shapes_rise_from_0_to_1_and_their_slope_and_integral_agree()
    {
        foreach (var k in Enum.GetValues<TransitionShape>())
        {
            Close(Transitions.Shape(k, 0), 0, 1e-15);
            Close(Transitions.Shape(k, 1), 1, 1e-15);
            for (var xi = 0.05; xi < 1; xi += 0.1)
            {
                const double h = 1e-6;
                Close(Transitions.Slope(k, xi), (Transitions.Shape(k, xi + h) - Transitions.Shape(k, xi - h)) / (2 * h), 1e-6);
                Close(Transitions.Integral(k, xi + h) - Transitions.Integral(k, xi - h), Transitions.Shape(k, xi) * 2 * h, 1e-11);
            }
        }
    }

    [Fact]
    public void Every_type_starts_at_its_start_point_along_its_direction_with_the_start_curvature()
    {
        foreach (var type in AllHorizontal)
        {
            double r1 = type == HorizontalType.Line ? 0 : 400, r2 = type == HorizontalType.Line ? 0 : type == HorizontalType.CircularArc ? 400 : 150;
            var p = PreparedHorizontal.Evaluate(new(type, new(5, -3), 1.1, r1, r2, 80), 0);
            Close(p.X, 5, 1e-12); Close(p.Y, -3, 1e-12);
            Close(p.Direction, 1.1, 1e-12);
            // The cubic parabola's true curvature is y″ / (1 + y′²)^1.5: its y″ is 1/R₁, the curvature only ≈ (here 0.5 %).
            Close(p.Curvature, Alignment.CurvatureOf(r1), type == HorizontalType.Cubic ? 0.01 / 400 : 1e-12);
        }
    }

    [Fact]
    public void Transitions_end_at_the_end_curvature_and_turn_by_the_integral_of_curvature()
    {
        foreach (var type in Transitional)
        {
            double L = 120, k1 = 1.0 / 2000, k2 = -1.0 / 300;
            var p = PreparedHorizontal.Evaluate(new(type, new(0, 0), 0, 2000, -300, L), L);
            Close(p.Curvature, k2, 1e-12);
            Close(p.Direction, k1 * L + (k2 - k1) * L * Transitions.Integral(Transitions.Of(type)!.Value, 1), 1e-12);
        }
    }

    [Fact]
    public void The_clothoid_closed_form_agrees_with_quadrature()
    {
        foreach (var (r1, r2, L) in new[] { (0.0, 300.0, 100.0), (1000, 300, 100), (300, 1000, 100), (-250, 0, 140), (0, -60, 200), (5000, 4000, 300) })
        {
            var seg = new HorizontalSegment(HorizontalType.Clothoid, new(0, 0), 0.4, r1, r2, L);
            double k1 = Alignment.CurvatureOf(r1), c = (Alignment.CurvatureOf(r2) - k1) / L;
            foreach (var s in new[] { 0.5, L / 3, L })
            {
                var p = PreparedHorizontal.Evaluate(seg, s);
                var q = Quadrature.IntegrateHeading(u => 0.4 + k1 * u + c * u * u / 2, 0, s);
                Close(p.X, q.X, 1e-10); Close(p.Y, q.Y, 1e-10);
            }
        }
    }

    [Fact]
    public void A_circular_arc_stays_on_its_circle()
    {
        var seg = new HorizontalSegment(HorizontalType.CircularArc, new(3, 4), 2, -75, -75, 300);
        // Centre: to the right of the start tangent for a CW arc.
        double cx = 3 + 75 * Math.Sin(2), cy = 4 - 75 * Math.Cos(2);
        for (double s = 0; s <= 300; s += 17)
        {
            var p = PreparedHorizontal.Evaluate(seg, s);
            Close(Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)), 75, 1e-11);
        }
    }

    [Fact]
    public void Flipping_the_radius_signs_mirrors_the_curve_about_the_start_tangent()
    {
        foreach (var type in Transitional.Append(HorizontalType.Cubic).Append(HorizontalType.CircularArc))
        {
            double r1 = type == HorizontalType.CircularArc ? 250 : 900, r2 = 250, L = 110, d = 0.7;
            var left = PreparedHorizontal.Prepare(new(type, new(0, 0), d, r1, r2, L, 1.8), 0.06);
            var right = PreparedHorizontal.Prepare(new(type, new(0, 0), d, -r1, -r2, L, 1.8), -0.06);
            for (double s = 0; s <= L; s += 11)
            {
                var a = left.At(s); var b = right.At(s);
                // Local frame of the start tangent: along u, across v.
                double ua = a.X * Math.Cos(d) + a.Y * Math.Sin(d), va = -a.X * Math.Sin(d) + a.Y * Math.Cos(d);
                double ub = b.X * Math.Cos(d) + b.Y * Math.Sin(d), vb = -b.X * Math.Sin(d) + b.Y * Math.Cos(d);
                Close(ua, ub, 1e-10); Close(va, -vb, 1e-10);
                Close(a.Direction - d, -(b.Direction - d), 1e-12);
                Close(a.Curvature, -b.Curvature, 1e-12);
            }
        }
    }

    [Fact]
    public void The_Viennese_bend_leans_out_first_under_a_cant_increase_and_turns_no_more_in_total()
    {
        var seg = new HorizontalSegment(HorizontalType.VienneseBend, new(0, 0), 0, 0, 300, 100, 1.8);
        var plain = PreparedHorizontal.Prepare(seg); var banked = PreparedHorizontal.Prepare(seg, 0.1);
        Assert.True(banked.At(5).Curvature < 0);
        Close(banked.At(100).Direction, plain.At(100).Direction, 1e-12);
        Close(banked.At(100).Curvature, plain.At(100).Curvature, 1e-12);
    }

    [Fact]
    public void A_vertical_circular_arc_is_a_true_circle_with_the_radius_from_the_gradients()
    {
        var v = new List<VerticalSegment> { new(VerticalType.CircularArc, 50, 100, 10, 0, 0.5) };
        var R = 100 / Math.Sin(Math.Atan(0.5));
        for (double d = 50; d <= 150; d += 10)
        {
            var p = PreparedVertical.Evaluate(v, d)!.Value;
            Close(Math.Sqrt((d - 50) * (d - 50) + (p.Z - (10 + R)) * (p.Z - (10 + R))), R, 1e-9);
            Close(p.Curvature, 1 / R, 1e-15);
        }
        Close(PreparedVertical.Evaluate(v, 150)!.Value.Gradient, 0.5, 1e-12);
        Assert.Null(PreparedVertical.Evaluate(v, 151));
    }

    [Fact]
    public void A_vertical_clothoid_reaches_the_end_gradient_after_exactly_the_horizontal_length()
    {
        foreach (var (g1, g2, r) in new (double, double, double?)[] { (0, 0.04, null), (0.02, -0.03, null), (0.5, 1, null), (0.01, 0.03, 4000) })
        {
            var v = new List<VerticalSegment> { new(VerticalType.Clothoid, 0, 120, 5, g1, g2, r) };
            Close(PreparedVertical.Evaluate(v, 0)!.Value.Gradient, g1, 1e-12);
            Close(PreparedVertical.Evaluate(v, 120)!.Value.Gradient, g2, 1e-9);
            Close(PreparedVertical.Evaluate(v, 0)!.Value.Curvature, Alignment.CurvatureOf(r ?? 0), 1e-12);
            // The gradient is dz/dd.
            foreach (var d in new[] { 10.0, 60, 110 })
            {
                const double h = 1e-3;
                Close((PreparedVertical.Evaluate(v, d + h)!.Value.Z - PreparedVertical.Evaluate(v, d - h)!.Value.Z) / (2 * h), PreparedVertical.Evaluate(v, d)!.Value.Gradient, 1e-8);
            }
        }
    }

    [Fact]
    public void Each_rail_follows_the_cant_transition_and_the_bank_angle_comes_from_right_minus_left()
    {
        var layer = new CantLayer(1.5, [
            new(CantType.LinearTransition, 0, 100, 0, 0, 0, 0.15),
            new(CantType.ConstantCant, 100, 50, 0, null, 0.15, null),
        ]);
        Close(Cant.Evaluate(layer, 50)!.Value.CantRight, 0.075, 1e-15);
        Close(Cant.Evaluate(layer, 120)!.Value.CantRight, 0.15, 1e-15);
        Close(Cant.Evaluate(layer, 120)!.Value.Angle, Math.Asin(0.1), 1e-15);
        Assert.Null(Cant.Evaluate(layer, 151));
    }

    static Alignment Sample()
    {
        var horizontal = Chain([
            (HorizontalType.Line, 0, 0, 80),
            (HorizontalType.BlossCurve, 0, 350, 90),
            (HorizontalType.CircularArc, 350, 350, 120),
            (HorizontalType.Clothoid, 350, -500, 140),
            (HorizontalType.SineCurve, -500, 0, 70),
            (HorizontalType.Line, 0, 0, 60),
        ]);
        return new Alignment(horizontal, [
            new(VerticalType.ConstantGradient, 0, 200, 100, 0.01, 0.01),
            new(VerticalType.ParabolicArc, 200, 160, 102, 0.01, -0.02),
            new(VerticalType.ConstantGradient, 360, 200, 102 + 0.01 * 160 - 0.03 * 160 / 2, -0.02, -0.02),
        ], null, 2000);
    }

    [Fact]
    public void An_alignment_built_continuous_is_continuous()
    {
        Assert.DoesNotContain(Continuity.Check(Sample()), d => d.Severity != "info");
    }

    [Fact]
    public void Continuity_reports_G0_G1_and_gaps_with_the_segment_index_and_size()
    {
        var a = Sample();
        var broken = new Alignment(
            a.Horizontal.Select((s, i) => i == 2 ? s with { Start = new(s.Start.X + 0.01, s.Start.Y) } : i == 4 ? s with { StartDirection = s.StartDirection + 0.002 } : s).ToList(),
            a.Vertical!.Select((v, i) => i == 2 ? v with { StartDistAlong = v.StartDistAlong + 0.5 } : v).ToList());
        var d = Continuity.Check(broken);
        var g0 = d.First(x => x.Layer == AlignmentLayer.Horizontal && x.Kind == ContinuityKind.G0);
        Assert.Equal(2, g0.Index);
        Close(g0.Size, 0.01, 1e-9);
        Assert.Equal("error", g0.Severity);
        Assert.Contains(4, d.Where(x => x.Layer == AlignmentLayer.Horizontal && x.Kind == ContinuityKind.G1).Select(x => x.Index));
        var gap = d.First(x => x.Layer == AlignmentLayer.Vertical && x.Kind == ContinuityKind.Gap);
        Assert.Equal(2, gap.Index);
        Close(gap.Size, 0.5, 1e-9);
    }

    [Fact]
    public void StationOf_inverts_evaluate_with_offsets_on_both_sides()
    {
        var a = Sample();
        var p = a.Prepared();
        for (var s = 1.0; s < p.Length; s += 13.7)
            foreach (var off in new[] { -4.0, 0, 2.5 })
            {
                var q = p.At(s);
                double x = q.X - Math.Sin(q.Direction) * off, y = q.Y + Math.Cos(q.Direction) * off;
                var r = p.StationOf(x, y);
                Close(r.S, s, 1e-8);
                Close(r.Station, 2000 + s, 1e-8);
                Close(r.Offset, off, 1e-8);
            }
    }

    [Fact]
    public void Evaluate_combines_plan_and_profile()
    {
        var p = Sample().Evaluate(280);
        Close(p.Station, 2280, 1e-12);
        Close(p.Gradient!.Value, 0.01 - 0.03 * 80 / 160, 1e-12);
        Assert.Null(p.Cant);
    }

    [Fact]
    public void Adaptive_sampling_keeps_the_chord_error_finer_where_curved_and_hits_every_joint()
    {
        var a = Sample();
        var p = a.Prepared();
        var stations = p.SampleStations(new(Chord: 0.002, MaxStep: 25));
        foreach (var j in p.Horizontal.Starts) Assert.Contains(stations, s => Math.Abs(s - j) < 1e-9);
        double worst = 0;
        for (var i = 1; i < stations.Count; i++)
        {
            double s0 = stations[i - 1], s1 = stations[i];
            var a0 = p.At(s0); var a1 = p.At(s1); var m = p.At((s0 + s1) / 2);
            double dx = a1.X - a0.X, dy = a1.Y - a0.Y;
            worst = Math.Max(worst, Math.Abs((m.X - a0.X) * dy - (m.Y - a0.Y) * dx) / Math.Sqrt(dx * dx + dy * dy));
            Assert.True(s1 - s0 <= 25 + 1e-9);
        }
        Assert.True(worst < 0.0025);
        Assert.True(stations.Count(s => s > 170 && s < 290) > stations.Count(s => s < 80));
    }

    [Fact]
    public void The_STEP_reader_reads_the_three_layers_in_nest_order_with_units()
    {
        const string ifc = """
            ISO-10303-21;
            DATA;
            #7 = IFCSIUNIT(*, .LENGTHUNIT., $, .METRE.);
            #8 = IFCCONVERSIONBASEDUNIT(#12, .PLANEANGLEUNIT., 'DEGREE', #11);
            #11 = IFCMEASUREWITHUNIT(IFCPLANEANGLEMEASURE(0.0174532925199433), #13);
            #20 = IFCALIGNMENT('g', $, 'Main', $, $, $, $, $);
            #21 = IFCALIGNMENTHORIZONTAL('h', $, $, $, $, $, $);
            #23 = IFCRELNESTS('n', $, $, $, #20, (#21, #41, #61));
            #28 = IFCCARTESIANPOINT((100., 200.));
            #29 = IFCALIGNMENTHORIZONTALSEGMENT($, $, #28, 90., 0., 0., 50., $, .LINE.);
            #30 = IFCALIGNMENTSEGMENT('a', $, $, $, $, $, $, #29);
            #31 = IFCCARTESIANPOINT((100., 250.));
            #32 = IFCALIGNMENTHORIZONTALSEGMENT($, $, #31, 90., 0., -300., 60., $, .CLOTHOID.);
            #33 = IFCALIGNMENTSEGMENT('b', $, $, $, $, $, $, #32);
            #34 = IFCRELNESTS('m', $, $, $, #21, (#33, #30));
            #41 = IFCALIGNMENTVERTICAL('v', $, $, $, $, $, $);
            #42 = IFCALIGNMENTSEGMENT('c', $, $, $, $, $, $, #44);
            #43 = IFCRELNESTS('o', $, $, $, #41, (#42));
            #44 = IFCALIGNMENTVERTICALSEGMENT($, $, 0., 110., 10., 1.E-2, 1.E-2, $, .CONSTANTGRADIENT.);
            #61 = IFCALIGNMENTCANT('c', $, $, $, $, $, $, 1.5);
            #62 = IFCALIGNMENTSEGMENT('d', $, $, $, $, $, $, #64);
            #63 = IFCRELNESTS('p', $, $, $, #61, (#62));
            #64 = IFCALIGNMENTCANTSEGMENT($, $, 0., 110., 0., $, 0., 1.E-1, .LINEARTRANSITION.);
            ENDSEC;
            END-ISO-10303-21;
            """;
        var r = Assert.Single(IfcAlignmentReader.Read(ifc));
        Assert.Equal("Main", r.Name);
        var h = r.Alignment.Horizontal;
        // Nest order wins over file order.
        Assert.Equal([HorizontalType.Clothoid, HorizontalType.Line], h.Select(s => s.Type));
        Close(h[0].StartDirection, Math.PI / 2, 1e-12);
        Assert.Equal(-300, h[0].EndRadius);
        var v = Assert.Single(r.Alignment.Vertical!);
        Assert.Equal((VerticalType.ConstantGradient, 110.0, 10.0, 0.01), (v.Type, v.HorizontalLength, v.StartHeight, v.StartGradient));
        Assert.Equal(1.5, r.Alignment.Cant!.RailHeadDistance);
        var c = Assert.Single(r.Alignment.Cant.Segments);
        Assert.Equal((CantType.LinearTransition, 0.0, 0.1), (c.Type, c.StartCantRight, c.EndCantRight));
        Assert.Null(c.EndCantLeft);
    }

    // ─── The PI model ───────────────────────────────────────────────────────

    static readonly List<AlignmentPi>[] Specs =
    [
        [new(0, 0), new(200, 0, 100), new(200, 200)],
        [new(0, 0), new(300, 0, 150, 60, 60), new(300, -300)],
        [new(0, 0), new(250, 50, 120, 40, 80), new(300, 300, 60, 0, 30), new(100, 500, 80), new(-100, 500)],
        [new(500, 500), new(1500, 700, 900, 120, 120), new(2400, 100, 650, 90, 150), new(3500, 300)],
    ];

    [Fact]
    public void The_PI_chain_and_its_IFC_segments_give_the_same_points_within_a_tenth_of_a_millimetre()
    {
        foreach (var pis in Specs)
        {
            var pi = PiAlignment.Build(pis);
            var a = new Alignment(pi.ToSegments()).Prepared();
            Close(a.Length, pi.Length, 1e-6);
            var joints = pi.CriticalStations().ToList();
            double worst = 0;
            for (var s = 0.0; s <= pi.Length; s += 0.5)
            {
                var old = pi.At(s); var p = a.At(s);
                worst = Math.Max(worst, (p.Plan.P - old.P).Length);
                Assert.True(Math.Abs(Math.Sin(p.Direction - old.Direction)) < 1e-9);
                // At a joint the PI model reports the earlier segment's curvature, the IFC evaluator the later one's.
                if (!joints.Any(j => Math.Abs(j - s) < 1e-6)) Assert.True(Math.Abs(p.Curvature - old.Curvature) < 1e-9);
            }
            Assert.True(worst < 1e-4, $"worst {worst}");
        }
    }

    [Fact]
    public void A_PI_chain_is_G0_and_G1_continuous_and_jumps_in_curvature_only_where_a_line_meets_an_arc()
    {
        Assert.DoesNotContain(Continuity.Check(new Alignment(PiAlignment.Build(Specs[2]).ToSegments())), x => x.Severity != "info");
        Assert.Empty(Continuity.Check(new Alignment(PiAlignment.Build(Specs[1]).ToSegments())));
    }

    [Fact]
    public void A_PVI_profile_becomes_grades_and_parabolas_giving_the_same_heights_and_grades_and_back()
    {
        var pvis = new List<Pvi> { new(1000, 100), new(1200, 106, 100), new(1500, 98, 160), new(1700, 101) };
        var old = VerticalProfile.Build(pvis);
        var vertical = PiAlignment.VerticalFromPvis(pvis, 1000);
        Assert.Equal([VerticalType.ConstantGradient, VerticalType.ParabolicArc, VerticalType.ConstantGradient, VerticalType.ParabolicArc, VerticalType.ConstantGradient], vertical.Select(v => v.Type));
        var a = new Alignment(PiAlignment.Build([new(0, 0), new(700, 0)]).ToSegments(), vertical, null, 1000);
        for (var st = 1000.0; st <= 1700; st += 0.25)
        {
            var p = a.EvaluateAtStation(st);
            Assert.True(Math.Abs(p.Z!.Value - old.ElevationAt(st)) < 1e-9);
            Assert.True(Math.Abs(p.Gradient!.Value - old.GradeAt(st)) < 1e-9);
        }
        Assert.DoesNotContain(Continuity.Check(a), x => x.Severity != "info");
        var back = PiAlignment.PvisFromVertical(vertical, 1000)!;
        Assert.Equal(pvis.Select(p => (p.Station, p.Elevation, p.Length)), back.Select(p => (Math.Round(p.Station, 9), Math.Round(p.Elevation, 9), Math.Round(p.Length, 9))));
    }

    [Fact]
    public void Curves_that_touch_leave_no_zero_length_tangent()
    {
        var vertical = PiAlignment.VerticalFromPvis([new(0, 0), new(100, 2, 100), new(200, 0, 100), new(400, 1)]);
        Assert.Equal([VerticalType.ConstantGradient, VerticalType.ParabolicArc, VerticalType.ParabolicArc, VerticalType.ConstantGradient], vertical.Select(v => v.Type));
    }

    [Fact]
    public void A_PI_chain_refuses_what_cannot_be_built_and_says_why()
    {
        Assert.Contains("no radius", PiAlignment.Problem([new(0, 0), new(100, 0), new(100, 100)]));
        Assert.Contains("overlaps", PiAlignment.Problem([new(0, 0), new(20, 0, 500), new(20, 20)]));
        Assert.Null(PiAlignment.Problem([new(0, 0), new(100, 0, 50), new(100, 100)]));
        // A straight polyline is a chain of lines, whatever it is asked.
        var straight = PiAlignment.Build([new(0, 0), new(10, 0), new(20, 0)]);
        Assert.All(straight.Segments, s => Assert.Equal(PiSegmentKind.Line, s.Kind));
        Assert.Equal(20, straight.Length, 9);
    }
}
