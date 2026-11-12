using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Platform.Analysis;

namespace Lapin.Engine.Tests;

/// <summary>
/// The linear static solver on bars (docs/plan-fem.md §9.3, §9.5) against closed-form solutions from beam theory: a
/// cantilever with a force at its tip (δ = PL³/3EI, M = PL) about the strong and the weak axis, a simply supported beam under
/// a uniform load (5wL⁴/384EI, wL²/8) and a central force (PL³/48EI), a two-span continuous beam (wL²/8 over the support,
/// reactions 3/8, 10/8, 3/8 wL), a fixed-base portal under a horizontal force (slope–deflection), a cantilever in torsion
/// (TL/GJ), a beam pinned at both ends in a frame, an eccentric end (the arm's moment), a mechanism said, and each case's
/// balance.
/// </summary>
public class FrameSolverTests
{
    // Steel, an IPE 300's published values (kPa, m², m⁴): enough for the closed forms, whatever the section.
    const double E = 210e6, G = 80.77e6, A = 53.8e-4, I33 = 8356e-8, I22 = 604e-8, J = 20.1e-8;
    static readonly bool[] Fixed = [true, true, true, true, true, true];
    static readonly bool[] Pin = [true, true, true, true, false, false];
    static readonly bool[] Roller = [false, true, true, false, false, false];
    static readonly Vec3 Down = new(0, 0, -1);

    static FrameElement Bar(int i, int j, Vec3? local2 = null, bool[]? relI = null, bool[]? relJ = null, Vec3 offI = default, double a = A, double i33 = I33) => new()
    {
        Id = $"b{i}-{j}", I = i, J = j, Local2 = local2 ?? Vec3.UnitZ, E = E, G = G, A = a, I22 = I22, I33 = i33, Jt = J,
        ReleaseI = relI ?? new bool[6], ReleaseJ = relJ ?? new bool[6], OffI = offI,
    };

    static FrameSolution Solve(FrameProblem p)
    {
        var s = FrameSolver.Solve(p);
        Assert.True(s.Ok, s.Mechanism is { } m ? $"mechanism at {m.Node} {FrameSolver.DofName(m.Dof)}" : s.Refused);
        foreach (var c in s.Cases) Assert.True(c.Balance < 1e-9, $"case {c.Id} off balance by {c.Balance}");
        return s;
    }

    static double Rel(double expected, double actual) => Math.Abs(actual - expected) / Math.Abs(expected);

    [Fact]
    public void A_cantilever_with_a_force_at_its_tip_deflects_PL3_over_3EI_about_its_strong_axis()
    {
        const double L = 3, P = 10;
        var p = new FrameProblem();
        p.AddNode(new Vec3(0, 0, 0), "N1", Fixed);
        p.AddNode(new Vec3(L, 0, 0), "N2");
        p.Elements.Add(Bar(0, 1));
        var c = new CaseLoads("P");
        c.AddNodal(1, Down * P);
        p.Cases.Add(c);
        var s = Solve(p);
        var r = s.Cases[0];
        Assert.True(Rel(-P * L * L * L / (3 * E * I33), r.U[6 + 2]) < 1e-9);
        // The moment at the fixed end is PL, hogging (negative); nothing at the tip.
        var e = p.Elements[0];
        Assert.True(Rel(-P * L, Stations.At(e, r.Elements[0], 0).Mz) < 1e-9);
        Assert.True(Math.Abs(Stations.At(e, r.Elements[0], L).Mz) < 1e-9);
        var ex = Stations.Extremes(e, r.Elements[0])[5];
        Assert.Equal(-P * L, ex.Min, 9);
        Assert.Equal(0, ex.XMin, 9);
        // The tip's deflection along the bar by the curvature integrated: the same.
        Assert.True(Rel(-P * L * L * L / (3 * E * I33), Stations.Deflection(e, r.Elements[0], L).V) < 1e-9);
        // The reaction: P up, PL about +y (the force down at +x turns the bar about −y).
        Assert.Equal(P, r.Reactions[0][2], 9);
        Assert.Equal(-P * L, r.Reactions[0][4], 9);
    }

    [Fact]
    public void Turned_90_degrees_the_cantilever_bends_about_its_weak_axis()
    {
        const double L = 3, P = 10;
        var p = new FrameProblem();
        p.AddNode(new Vec3(0, 0, 0), "N1", Fixed);
        p.AddNode(new Vec3(L, 0, 0), "N2");
        // Its depth horizontal (local 2 along +Y): a load down bends it about local 2, I22.
        p.Elements.Add(Bar(0, 1, local2: new Vec3(0, 1, 0)));
        var c = new CaseLoads("P");
        c.AddNodal(1, Down * P);
        p.Cases.Add(c);
        var r = Solve(p).Cases[0];
        Assert.True(Rel(-P * L * L * L / (3 * E * I22), r.U[6 + 2]) < 1e-9);
        var e = p.Elements[0];
        Assert.True(Rel(P * L, Math.Abs(Stations.At(e, r.Elements[0], 0).My)) < 1e-9);
        Assert.True(Math.Abs(Stations.At(e, r.Elements[0], 0).Mz) < 1e-9);
        Assert.True(Rel(-P * L * L * L / (3 * E * I22), Stations.Deflection(e, r.Elements[0], L).W) < 1e-9);
    }

    static FrameProblem Simple(double l, int pieces = 1)
    {
        var p = new FrameProblem();
        for (var k = 0; k <= pieces; k++)
            p.AddNode(new Vec3(l * k / pieces, 0, 0), $"N{k + 1}", k == 0 ? Pin : k == pieces ? Roller : null);
        for (var k = 0; k < pieces; k++) p.Elements.Add(Bar(k, k + 1));
        return p;
    }

    [Fact]
    public void A_simply_supported_beam_under_a_uniform_load_sags_5wL4_over_384EI_and_carries_wL2_over_8()
    {
        const double L = 6, w = 10;
        var p = Simple(L);
        var c = new CaseLoads("w");
        c.Span.Add((0, SpanLoad.Uniform(0, L, Down * w)));
        p.Cases.Add(c);
        var r = Solve(p).Cases[0];
        var e = p.Elements[0];
        Assert.True(Rel(-5 * w * Math.Pow(L, 4) / (384 * E * I33), Stations.Deflection(e, r.Elements[0], L / 2).V) < 1e-9);
        // The largest moment exactly at midspan, sagging; the shear wL/2 at the supports.
        var ex = Stations.Extremes(e, r.Elements[0])[5];
        Assert.Equal(w * L * L / 8, ex.Max, 9);
        Assert.Equal(L / 2, ex.XMax, 9);
        Assert.Equal(w * L / 2, Math.Abs(Stations.At(e, r.Elements[0], 0).Vy), 9);
        Assert.Equal(w * L / 2, r.Reactions[0][2], 9);
        Assert.Equal(w * L / 2, r.Reactions[1][2], 9);
        Assert.True(Rel(w * L * L / 8, Stations.Along(e, r.Elements[0])[5].Mz) < 1e-9);
    }

    [Fact]
    public void A_force_at_midspan_deflects_the_beam_PL3_over_48EI()
    {
        const double L = 5, P = 20;
        // Once as a node in the middle, once as a force on the span: the same.
        var p = Simple(L, 2);
        var c = new CaseLoads("P");
        c.AddNodal(1, Down * P);
        p.Cases.Add(c);
        var r = Solve(p).Cases[0];
        Assert.True(Rel(-P * L * L * L / (48 * E * I33), r.U[6 + 2]) < 1e-9);

        var q = Simple(L);
        var d = new CaseLoads("P");
        d.Span.Add((0, SpanLoad.Force(L / 2, Down * P)));
        q.Cases.Add(d);
        var s = Solve(q).Cases[0];
        var e = q.Elements[0];
        Assert.True(Rel(-P * L * L * L / (48 * E * I33), Stations.Deflection(e, s.Elements[0], L / 2).V) < 1e-9);
        var ex = Stations.Extremes(e, s.Elements[0])[5];
        Assert.Equal(P * L / 4, ex.Max, 9);
        Assert.Equal(L / 2, ex.XMax, 9);
    }

    [Fact]
    public void A_two_span_continuous_beam_has_wL2_over_8_over_its_middle_support_and_reactions_3_10_3_eighths()
    {
        const double L = 4, w = 12;
        var p = new FrameProblem();
        p.AddNode(new Vec3(0, 0, 0), "N1", Pin);
        p.AddNode(new Vec3(L, 0, 0), "N2", Roller);
        p.AddNode(new Vec3(2 * L, 0, 0), "N3", Roller);
        p.Elements.Add(Bar(0, 1));
        p.Elements.Add(Bar(1, 2));
        var c = new CaseLoads("w");
        c.Span.Add((0, SpanLoad.Uniform(0, L, Down * w)));
        c.Span.Add((1, SpanLoad.Uniform(0, L, Down * w)));
        p.Cases.Add(c);
        var r = Solve(p).Cases[0];
        Assert.Equal(3.0 / 8 * w * L, r.Reactions[0][2], 9);
        Assert.Equal(10.0 / 8 * w * L, r.Reactions[1][2], 9);
        Assert.Equal(3.0 / 8 * w * L, r.Reactions[2][2], 9);
        Assert.Equal(-w * L * L / 8, Stations.At(p.Elements[0], r.Elements[0], L).Mz, 9);
        Assert.Equal(-w * L * L / 8, Stations.At(p.Elements[1], r.Elements[1], 0).Mz, 9);
        // The span's largest moment 9wL²/128 at 3L/8.
        var ex = Stations.Extremes(p.Elements[0], r.Elements[0])[5];
        Assert.Equal(9 * w * L * L / 128, ex.Max, 9);
        Assert.Equal(3 * L / 8, ex.XMax, 9);
    }

    [Fact]
    public void A_fixed_base_portal_under_a_horizontal_force_sways_and_bends_as_slope_deflection_says()
    {
        // Columns h, beam L; k = (Ib/L)/(Ic/h). Axial strains left out (a large area), as the closed form does.
        const double h = 4, L = 6, H = 50, Ib = 2 * I33;
        var k = (Ib / L) / (I33 / h);
        var p = new FrameProblem();
        p.AddNode(new Vec3(0, 0, 0), "A", Fixed);
        p.AddNode(new Vec3(0, 0, h), "B");
        p.AddNode(new Vec3(L, 0, h), "C");
        p.AddNode(new Vec3(L, 0, 0), "D", Fixed);
        // The columns' depth in the frame's plane (local 2 along X), the beam's vertical.
        p.Elements.Add(Bar(0, 1, local2: new Vec3(1, 0, 0), a: 1e3));
        p.Elements.Add(Bar(1, 2, a: 1e3, i33: Ib));
        p.Elements.Add(Bar(3, 2, local2: new Vec3(1, 0, 0), a: 1e3));
        var c = new CaseLoads("H");
        c.AddNodal(1, new Vec3(H, 0, 0));
        p.Cases.Add(c);
        var r = Solve(p).Cases[0];
        var sway = H * h * h * h * (2 + 3 * k) / (12 * E * I33 * (1 + 6 * k));
        Assert.True(Rel(sway, r.U[6 * 1]) < 1e-6);
        Assert.True(Rel(sway, r.U[6 * 2]) < 1e-6);
        var mBase = H * h * (1 + 3 * k) / (2 * (1 + 6 * k));
        var mTop = 3 * k * H * h / (2 * (1 + 6 * k));
        Assert.True(Rel(mBase, Math.Abs(Stations.At(p.Elements[0], r.Elements[0], 0).Mz)) < 1e-6);
        Assert.True(Rel(mTop, Math.Abs(Stations.At(p.Elements[0], r.Elements[0], h).Mz)) < 1e-6);
        Assert.True(Rel(mBase, Math.Abs(r.Reactions[0][4])) < 1e-6);
        Assert.True(Rel(mBase, Math.Abs(r.Reactions[3][4])) < 1e-6);
        Assert.Equal(-H, r.Reactions[0][0] + r.Reactions[3][0], 6);
    }

    [Fact]
    public void A_cantilever_twisted_at_its_tip_turns_TL_over_GJ()
    {
        const double L = 2.5, T = 3;
        var p = new FrameProblem();
        p.AddNode(new Vec3(0, 0, 0), "N1", Fixed);
        p.AddNode(new Vec3(0, L, 0), "N2");
        p.Elements.Add(Bar(0, 1));
        var c = new CaseLoads("T");
        c.AddMoment(1, new Vec3(0, T, 0));
        p.Cases.Add(c);
        var r = Solve(p).Cases[0];
        Assert.True(Rel(T * L / (G * J), r.U[6 + 4]) < 1e-9);
        Assert.Equal(T, Stations.At(p.Elements[0], r.Elements[0], L / 2).T, 9);
        Assert.Equal(-T, r.Reactions[0][4], 9);
    }

    [Fact]
    public void A_beam_pinned_at_both_ends_in_a_frame_carries_its_load_as_a_simple_span()
    {
        const double h = 3, L = 5, w = 8;
        var p = new FrameProblem();
        p.AddNode(new Vec3(0, 0, 0), "A", Fixed);
        p.AddNode(new Vec3(0, 0, h), "B");
        p.AddNode(new Vec3(L, 0, h), "C");
        p.AddNode(new Vec3(L, 0, 0), "D", Fixed);
        bool[] pinned = [false, false, false, false, true, true];
        p.Elements.Add(Bar(0, 1, local2: new Vec3(1, 0, 0)));
        p.Elements.Add(Bar(1, 2, relI: pinned, relJ: [false, false, false, true, true, true]));
        p.Elements.Add(Bar(3, 2, local2: new Vec3(1, 0, 0)));
        var c = new CaseLoads("w");
        c.Span.Add((1, SpanLoad.Uniform(0, L, Down * w)));
        p.Cases.Add(c);
        var s = Solve(p);
        var r = s.Cases[0];
        var beam = p.Elements[1];
        Assert.True(Math.Abs(Stations.At(beam, r.Elements[1], 0).Mz) < 1e-9);
        Assert.True(Math.Abs(Stations.At(beam, r.Elements[1], L).Mz) < 1e-9);
        Assert.True(Rel(w * L * L / 8, Stations.At(beam, r.Elements[1], L / 2).Mz) < 1e-9);
        // The columns take only its end shears, axially; no moment reaches them.
        Assert.True(Math.Abs(Stations.At(p.Elements[0], r.Elements[0], 0).Mz) < 1e-6);
        Assert.Equal(-w * L / 2, Stations.At(p.Elements[0], r.Elements[0], h / 2).N, 6);
        // The pinned end's rotation is the span's, recovered: wL³/24EI.
        Assert.True(Rel(w * L * L * L / (24 * E * I33), Math.Abs(r.Elements[1].ULocal[5])) < 1e-3);
    }

    [Fact]
    public void An_eccentric_end_brings_the_arms_moment_to_its_node()
    {
        // A bar 0.4 m above its fixed node on a rigid arm, pulled along its axis: the node takes P·e.
        const double L = 2, P = 30, e = 0.4;
        var p = new FrameProblem();
        p.AddNode(new Vec3(0, 0, 0), "N1", Fixed);
        p.AddNode(new Vec3(L, 0, e), "N2");
        p.Elements.Add(Bar(0, 1, offI: new Vec3(0, 0, e)));
        var c = new CaseLoads("P");
        c.AddNodal(1, new Vec3(P, 0, 0));
        p.Cases.Add(c);
        var r = Solve(p).Cases[0];
        Assert.Equal(-P, r.Reactions[0][0], 9);
        Assert.Equal(-P * e, r.Reactions[0][4], 9);
        // The bar itself is only stretched.
        var sec = Stations.At(p.Elements[0], r.Elements[0], L / 2);
        Assert.Equal(P, sec.N, 9);
        Assert.True(Math.Abs(sec.Mz) < 1e-9 && Math.Abs(sec.My) < 1e-9);
        Assert.True(Rel(P * L / (E * A), r.U[6]) < 1e-9);
    }

    [Fact]
    public void A_mechanism_is_said_with_its_node_and_degree_of_freedom()
    {
        // A beam on two rollers: nothing holds it along its axis.
        var p = new FrameProblem();
        p.AddNode(new Vec3(0, 0, 0), "N1", Roller);
        p.AddNode(new Vec3(4, 0, 0), "N2", [false, true, true, true, false, false]);
        p.Elements.Add(Bar(0, 1));
        var c = new CaseLoads("w");
        c.Span.Add((0, SpanLoad.Uniform(0, 4, Down * 5)));
        p.Cases.Add(c);
        var s = FrameSolver.Solve(p);
        Assert.False(s.Ok);
        var m = Assert.IsType<Mechanism>(s.Mechanism);
        Assert.Equal("ux", FrameSolver.DofName(m.Dof));
        Assert.Contains(0, m.Elements);
    }

    [Fact]
    public void Pinned_ends_meeting_at_a_node_hold_its_free_rotation_and_a_tripod_of_pinned_bars_solves()
    {
        // Three legs pinned at both ends (their twist held at the foot only), turned in plan so no axis lines up: the
        // apex's and the feet's rotations meet no stiffness at all; each leg carries P / (3 sin α).
        const double r = 2, h = 3, P = 12;
        var p = new FrameProblem();
        var apex = p.AddNode(new Vec3(0.3, -0.2, h), "C");
        bool[] pinI = [false, false, false, false, true, true], pinJ = [false, false, false, true, true, true];
        for (var k = 0; k < 3; k++)
        {
            var a = (17 + 120 * k) * Math.PI / 180;
            var foot = p.AddNode(new Vec3(0.3 + r * Math.Cos(a), -0.2 + r * Math.Sin(a), 0), $"F{k}", Pin);
            p.Elements.Add(Bar(foot, apex, local2: new Vec3(0, 0, 1), relI: pinI, relJ: pinJ));
        }
        var c = new CaseLoads("P");
        c.AddNodal(apex, Down * P);
        p.Cases.Add(c);
        var s = Solve(p);
        Assert.Equal(4, s.Springs);
        var res = s.Cases[0];
        var sin = h / Math.Sqrt(r * r + h * h);
        for (var k = 0; k < 3; k++)
        {
            Assert.True(Rel(-P / (3 * sin), Stations.At(p.Elements[k], res.Elements[k], 1).N) < 1e-9);
            Assert.True(Math.Abs(Stations.At(p.Elements[k], res.Elements[k], 1).Mz) < 1e-9);
        }
    }
    [Fact]
    public void A_deep_short_cantilever_deflects_by_shear_too_PL_over_G_Av()
    {
        // A 1.2 m cantilever 30 × 90 cm (Timoshenko, As = 5/6·A), its tip pushed down, then sideways.
        const double L = 1.2, P = 100, b = 0.3, h = 0.9, e = 30e6, g = 12.5e6;
        var (a, i33, i22) = (b * h, b * h * h * h / 12, h * b * b * b / 12);
        foreach (var (dir, inertia, dof) in new[] { (Down, i33, 2), (new Vec3(0, 1, 0), i22, 1) })
        {
            var p = new FrameProblem();
            p.AddNode(Vec3.Zero, "N1", Fixed);
            p.AddNode(new Vec3(L, 0, 0), "N2");
            p.Elements.Add(new FrameElement { Id = "c", I = 0, J = 1, Local2 = Vec3.UnitZ, E = e, G = g, A = a, I33 = i33, I22 = i22, Jt = 0.01, As2 = 5.0 / 6 * a, As3 = 5.0 / 6 * a });
            var c = new CaseLoads("P");
            c.AddNodal(1, dir * P);
            p.Cases.Add(c);
            var r = Solve(p).Cases[0];
            var bending = P * L * L * L / (3 * e * inertia);
            var shear = P * L / (g * 5.0 / 6 * a);
            Assert.True(Rel(bending + shear, Math.Abs(r.U[6 + dof])) < 1e-9);
            // The shear's share is not small on a bar this deep, bent about its strong axis: about 30 % here.
            if (dof == 2) Assert.InRange(shear / (bending + shear), 0.2, 0.6);
            // The deflection along it, integrated, ends where the node is.
            var (_, v, w) = Stations.Deflection(p.Elements[0], r.Elements[0], L);
            Assert.True(Rel(r.U[6 + dof], dof == 2 ? v : -w) < 1e-9, $"node {r.U[6 + dof]} integrated v {v} w {w} bending {bending} shear {shear}");
        }
    }

    [Fact]
    public void A_force_on_a_Timoshenko_span_gives_what_a_node_there_would()
    {
        // A propped cantilever 3 m, its force at 0.9 m: once on the span (its fixed-end forces from the interdependent
        // interpolation), once at a node splitting it there; the same reactions and deflection.
        const double L = 3, a = 0.9, P = 40;
        FrameElement El(int i, int j) => new() { Id = $"e{i}", I = i, J = j, Local2 = Vec3.UnitZ, E = E, G = G, A = 0.02, I33 = 4e-4, I22 = 1e-4, Jt = 1e-5, As2 = 0.012, As3 = 0.01 };
        var one = new FrameProblem();
        one.AddNode(Vec3.Zero, "A", Fixed);
        one.AddNode(new Vec3(L, 0, 0), "B", Roller);
        one.Elements.Add(El(0, 1));
        var c1 = new CaseLoads("P");
        c1.Span.Add((0, SpanLoad.Force(a, Down * P)));
        one.Cases.Add(c1);
        var two = new FrameProblem();
        two.AddNode(Vec3.Zero, "A", Fixed);
        two.AddNode(new Vec3(a, 0, 0), "M");
        two.AddNode(new Vec3(L, 0, 0), "B", Roller);
        two.Elements.Add(El(0, 1));
        two.Elements.Add(El(1, 2));
        var c2 = new CaseLoads("P");
        c2.AddNodal(1, Down * P);
        two.Cases.Add(c2);
        var (r1, r2) = (Solve(one).Cases[0], Solve(two).Cases[0]);
        for (var d = 0; d < 6; d++) Assert.Equal(r2.Reactions[0][d], r1.Reactions[0][d], 9);
        Assert.Equal(r2.Reactions[2][2], r1.Reactions[1][2], 9);
        Assert.True(Rel(r2.U[6 + 2], Stations.Deflection(one.Elements[0], r1.Elements[0], a).V) < 1e-9, $"{r2.U[6 + 2]} vs {Stations.Deflection(one.Elements[0], r1.Elements[0], a).V}");
    }

    [Fact]
    public void Shear_areas_come_from_the_sections_geometry()
    {
        // EN 1993-1-1 §6.2.6(3)(a) for an IPE 300: Av,z = A − 2b·tf + (tw + 2r)·tf = 25.68 cm² (EN 10365's table).
        var ipe = SectionProperties.Of(new SectionShape("I", 0.15, 0.3, 0.0071, 0.0107, Designation: "IPE 300"))!;
        Assert.True(Math.Abs(ipe.Av2!.Value * 1e4 - 25.68) / 25.68 < 0.01);
        Assert.Equal(2 * 0.15 * 0.0107, ipe.Av3!.Value, 12);
        var rect = SectionProperties.Of(new SectionShape("rectangle", 0.3, 0.5))!;
        Assert.Equal(5.0 / 6 * 0.15, rect.Av2!.Value, 12);
        Assert.Equal(5.0 / 6 * 0.15, rect.Av3!.Value, 12);
        var circle = SectionProperties.Of(new SectionShape("circle", 0.4, 0.4))!;
        Assert.Equal(0.9 * circle.Area, circle.Av2!.Value, 12);
        var rhs = SectionProperties.Of(new SectionShape("RHS", 0.1, 0.2, 0.008, Designation: "RHS 200×100×8"))!;
        Assert.Equal(rhs.Area * 0.2 / 0.3, rhs.Av2!.Value, 12);
        var chs = SectionProperties.Of(new SectionShape("CHS", 0.1143, 0.1143, 0.005, Designation: "CHS 114.3×5"))!;
        Assert.Equal(2 * chs.Area / Math.PI, chs.Av2!.Value, 12);
        Assert.Contains("6.2.6", ipe.AvMethod);
        Assert.Null(SectionProperties.Of(new SectionShape("outline", 0.2, 0.2) { Rings = [[new(-0.1, -0.1), new(0.1, -0.1), new(0, 0.1)]] })!.Av2);
    }

    [Fact]
    public void A_points_weights_on_a_pieces_corners_are_statically_consistent()
    {
        List<Vec2>[] pieces =
        [
            [new(0, 0), new(4, 0), new(1, 3)],
            [new(0, 0), new(6, 0), new(5, 4), new(0.5, 3)],
            [new(0, 0), new(5, 0), new(6, 2), new(3, 5), new(-1, 3)],
        ];
        foreach (var c in pieces)
        {
            var at = new Vec2(c.Average(p => p.X) * 0.8 + 0.2, c.Average(p => p.Y) * 0.9);
            var w = Lapin.Analysis.Solver.StructuralProblem.CornerWeights(c, at);
            Assert.Equal(1, w.Sum(), 12);
            Assert.All(w, x => Assert.True(x > 0));
            var back = c.Select((p, i) => p * w[i]).Aggregate(default(Vec2), (x, y) => x + y);
            Assert.Equal(at.X, back.X, 9);
            Assert.Equal(at.Y, back.Y, 9);
        }
        // On a rectangle the bilinear weights: (2, 1) in 6 × 4.
        var r = Lapin.Analysis.Solver.StructuralProblem.CornerWeights([new(0, 0), new(6, 0), new(6, 4), new(0, 4)], new Vec2(2, 1));
        Assert.Equal([0.5, 0.25, 1.0 / 12, 1.0 / 6], r, (x, y) => Math.Abs(x - y) < 1e-9);
    }
}
