using Lapin.Analysis.Solver;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// S5's solver (docs/plan-fem.md §14) against closed forms: Euler's columns (pinned, cantilever, fixed, fixed–pinned), the
/// portal frame's sway buckling with pinned and fixed bases (Timoshenko and Gere, "Theory of Elastic Stability", ch. 2), the
/// cantilever's second-order deflection under a lateral and an axial force (the beam-column's exact solution), supports on
/// springs (a vertical spring, a footing turning on its soil, Hetényi's long beam on a Winkler bed), tension-only X bracing
/// and a support that lifts.
/// </summary>
public class SecondOrderTests
{
    // Steel, an IPE 300's values (kPa, m², m⁴); no shear area: Euler–Bernoulli, as the closed forms.
    const double E = 210e6, G = 80.77e6, A = 53.8e-4, I33 = 8356e-8, I22 = 604e-8, J = 20.1e-8;
    static readonly bool[] Fixed = [true, true, true, true, true, true];
    static readonly bool[] Pinned = [true, true, true, false, false, true];

    static FrameElement Bar(int i, int j, Vec3 local2, double i22 = I22, double i33 = I33, double a = A, bool[]? relI = null, bool[]? relJ = null, string? only = null) => new()
    {
        Id = $"b{i}-{j}", I = i, J = j, Local2 = local2, E = E, G = G, A = a, I22 = i22, I33 = i33, Jt = J,
        ReleaseI = relI ?? new bool[6], ReleaseJ = relJ ?? new bool[6], Only = only,
    };

    static double Rel(double expected, double actual) => Math.Abs(actual - expected) / Math.Abs(expected);

    /// <summary>A vertical column of n elements from (x, 0, 0), h high; the nodes' indices bottom to top.</summary>
    static int[] Column(FrameProblem p, double x, double h, int n, bool[]? bottom, bool[]? top, double i22 = I22, double i33 = I33)
    {
        var ids = new int[n + 1];
        for (var k = 0; k <= n; k++) ids[k] = p.AddNode(new Vec3(x, 0, h * k / n), $"c{x}-{k}", k == 0 ? bottom : k == n ? top : null);
        for (var k = 0; k < n; k++) p.Elements.Add(Bar(ids[k], ids[k + 1], new Vec3(1, 0, 0), i22, i33));
        return ids;
    }

    static double Lowest(FrameProblem p, CaseLoads loads, int modes = 3)
    {
        var b = Buckling.Of(p, loads, modes);
        Assert.True(b.Ok, b.Message);
        Assert.True(b.Converged);
        return b.Alpha[0];
    }

    // ------------------------------------------------------------ Euler

    [Fact]
    public void A_pinned_column_buckles_at_pi2_EI_over_L2_about_its_weak_axis()
    {
        const double L = 5, P = 100;
        var p = new FrameProblem();
        var c = Column(p, 0, L, 8, Pinned, [true, true, false, false, false, false]);
        var loads = new CaseLoads("P");
        loads.AddNodal(c[^1], new Vec3(0, 0, -P));
        Assert.True(Rel(Math.PI * Math.PI * E * I22 / (L * L), Lowest(p, loads) * P) < 1e-4);
    }

    [Fact]
    public void A_cantilever_column_buckles_at_pi2_EI_over_4L2_and_its_second_mode_at_nine_times_that()
    {
        const double L = 4, P = 50;
        var p = new FrameProblem();
        var c = Column(p, 0, L, 16, Fixed, null);
        var loads = new CaseLoads("P");
        loads.AddNodal(c[^1], new Vec3(0, 0, -P));
        var b = Buckling.Of(p, loads, 3);
        Assert.True(b.Ok, b.Message);
        var pcr = Math.PI * Math.PI * E * I22 / (4 * L * L);
        Assert.True(Rel(pcr, b.Alpha[0] * P) < 1e-4);
        // The strong axis' first (13.8 × the weak's) comes after the weak axis' second (9 ×).
        Assert.True(Rel(9 * pcr, b.Alpha[1] * P) < 2e-3);
        Assert.True(Rel(pcr * I33 / I22, b.Alpha[2] * P) < 2e-3);
        // The mode's shape: the top moves, the foot does not.
        Assert.Equal(0, b.Modes[0][c[0] * 6], 12);
        Assert.True(Math.Abs(b.Modes[0][c[^1] * 6]) + Math.Abs(b.Modes[0][c[^1] * 6 + 1]) > 0);
    }

    [Fact]
    public void A_column_fixed_at_both_ends_buckles_at_4_pi2_EI_over_L2_and_fixed_pinned_at_20_19_EI_over_L2()
    {
        const double L = 6, P = 100;
        var p = new FrameProblem();
        var c = Column(p, 0, L, 12, Fixed, [true, true, false, true, true, true]);
        var loads = new CaseLoads("P");
        loads.AddNodal(c[^1], new Vec3(0, 0, -P));
        Assert.True(Rel(4 * Math.PI * Math.PI * E * I22 / (L * L), Lowest(p, loads) * P) < 1e-3);

        var q = new FrameProblem();
        var d = Column(q, 0, L, 12, Fixed, [true, true, false, false, false, true]);
        var l2 = new CaseLoads("P");
        l2.AddNodal(d[^1], new Vec3(0, 0, -P));
        // (kL)² of tan kL = kL: 4.4934² = 20.1907.
        Assert.True(Rel(20.1907 * E * I22 / (L * L), Lowest(q, l2) * P) < 1e-3);
    }

    [Fact]
    public void A_tension_gives_no_buckling_and_says_so()
    {
        var p = new FrameProblem();
        var c = Column(p, 0, 3, 4, Fixed, null);
        var loads = new CaseLoads("T");
        loads.AddNodal(c[^1], new Vec3(0, 0, 100));
        var b = Buckling.Of(p, loads, 3);
        Assert.True(b.Ok);
        Assert.Empty(b.Alpha);
        Assert.False(b.Compressed);
    }

    // ------------------------------------------------------------ the portal frame

    /// <summary>The root of f on (a, b) by bisection.</summary>
    static double Root(Func<double, double> f, double a, double b)
    {
        for (var k = 0; k < 200; k++)
        {
            var m = (a + b) / 2;
            if (f(a) * f(m) <= 0) b = m; else a = m;
        }
        return (a + b) / 2;
    }

    /// <summary>A portal in the x–z plane: columns h, beam l, the bases held as given, P down on each column's top; buckling in its plane (out of it held).</summary>
    static double Portal(bool[] bases, double h, double l, double beamI, double P)
    {
        var p = new FrameProblem();
        // Out of the frame's plane held at every node: uy, rx, rz (the frame bends about global y: the columns' and beam's I33 with local 2 in the plane).
        bool[] Plane(bool[] b) => [b[0], true, b[2], true, b[4], true];
        var free = new[] { false, true, false, true, false, true };
        const int n = 8;
        var left = new int[n + 1];
        var right = new int[n + 1];
        for (var k = 0; k <= n; k++) left[k] = p.AddNode(new Vec3(0, 0, h * k / n), $"l{k}", k == 0 ? Plane(bases) : free);
        for (var k = 0; k <= n; k++) right[k] = p.AddNode(new Vec3(l, 0, h * k / n), $"r{k}", k == 0 ? Plane(bases) : free);
        var beam = new int[n + 1];
        beam[0] = left[n];
        beam[n] = right[n];
        for (var k = 1; k < n; k++) beam[k] = p.AddNode(new Vec3(l * k / n, 0, h), $"b{k}", free);
        // In-plane bending is about local 3 when local 2 lies in the plane: +X for the columns, +Z for the beam. The closed forms
        // take the members as inextensible: their areas × 10⁴ (else the beam's shear in the sway mode stretches one column and
        // shortens the other, the frame rocks and its αcr is 0.3 % lower).
        for (var k = 0; k < n; k++) p.Elements.Add(Bar(left[k], left[k + 1], new Vec3(1, 0, 0), a: A * 1e4));
        for (var k = 0; k < n; k++) p.Elements.Add(Bar(right[k], right[k + 1], new Vec3(1, 0, 0), a: A * 1e4));
        for (var k = 0; k < n; k++) p.Elements.Add(Bar(beam[k], beam[k + 1], Vec3.UnitZ, i33: beamI, a: A * 1e4));
        var loads = new CaseLoads("P");
        loads.AddNodal(left[n], new Vec3(0, 0, -P));
        loads.AddNodal(right[n], new Vec3(0, 0, -P));
        return Lowest(p, loads) * P;
    }

    [Fact]
    public void A_pinned_base_portal_sways_at_lambda_tan_lambda_equal_to_6_Ib_h_over_Ic_L()
    {
        const double h = 4, l = 6, P = 100;
        var lambda = Root(x => x * Math.Tan(x) - 6 * h / l, 0.01, Math.PI / 2 - 1e-9);
        Assert.Equal(1.26459, lambda, 4);
        var pp = Portal(Pinned, h, l, I33, P);
        Assert.True(Rel(lambda * lambda * E * I33 / (h * h), pp) < 1e-3, $"{pp} vs {lambda * lambda * E * I33 / (h * h)}");
        // A very stiff beam: each column a cantilever turned over, π²EI/4h².
        Assert.True(Rel(Math.PI * Math.PI * E * I33 / (4 * h * h), Portal(Pinned, h, l, I33 * 1e5, P)) < 1e-3);
    }

    [Fact]
    public void A_fixed_base_portal_sways_at_lambda_cot_lambda_equal_to_minus_6_Ib_h_over_Ic_L()
    {
        const double h = 4, l = 6, P = 100;
        var lambda = Root(x => x / Math.Tan(x) + 6 * h / l, Math.PI / 2 + 1e-9, Math.PI - 1e-9);
        Assert.Equal(2.57043, lambda, 4);
        var pf = Portal(Fixed, h, l, I33, P);
        Assert.True(Rel(lambda * lambda * E * I33 / (h * h), pf) < 1e-3, $"{pf} vs {lambda * lambda * E * I33 / (h * h)}");
        Assert.True(Rel(Math.PI * Math.PI * E * I33 / (h * h), Portal(Fixed, h, l, I33 * 1e5, P)) < 1e-3);
    }

    // ------------------------------------------------------------ P-Δ

    [Fact]
    public void A_cantilever_under_half_its_critical_load_deflects_as_the_exact_beam_column()
    {
        const double L = 4, H = 2;
        var pcr = Math.PI * Math.PI * E * I22 / (4 * L * L);
        var P = 0.5 * pcr;
        var p = new FrameProblem();
        var c = Column(p, 0, L, 16, Fixed, null);
        var loads = new CaseLoads("PH");
        // H along +Y: local 3 of a vertical bar (local 2 is +X), so it bends about local 2, the weak axis.
        loads.AddNodal(c[^1], new Vec3(0, H, -P));
        var first = Nonlinear.Solve(p, loads, new NonlinearOptions(SecondOrder: false));
        var second = Nonlinear.Solve(p, loads, new NonlinearOptions(SecondOrder: true));
        Assert.True(first.Ok && second.Ok, second.Message);
        Assert.True(second.Converged);
        Assert.InRange(second.Iterations, 2, 4);
        var d1 = first.Result!.U[c[^1] * 6 + 1];
        var d2 = second.Result!.U[c[^1] * 6 + 1];
        Assert.True(Rel(H * L * L * L / (3 * E * I22), d1) < 1e-9);
        var k = Math.Sqrt(P / (E * I22));
        var exact = H * (Math.Tan(k * L) - k * L) / (P * k);
        Assert.Equal(1.98629, exact / d1, 4);
        Assert.True(Rel(exact, d2) < 5e-3, $"{d2} vs {exact}");
        // The amplification 1/(1 − P/Pcr) = 2 within 1 %.
        Assert.True(Rel(2, d2 / d1) < 1e-2);
        Assert.True(Rel(d2 / d1, second.Amplification) < 1e-9);
        // The base moment balances the deflected column: H L + P δ.
        var r = second.Result.Reactions[c[0]];
        Assert.True(Rel(H * L + P * d2, Math.Abs(r[3])) < 1e-6, $"{r[3]} vs {H * L + P * d2}");
        Assert.True(second.Result.Balance < 1e-9);
    }

    [Fact]
    public void Loads_past_the_critical_one_are_said_unstable()
    {
        const double L = 4;
        var pcr = Math.PI * Math.PI * E * I22 / (4 * L * L);
        var p = new FrameProblem();
        var c = Column(p, 0, L, 8, Fixed, null);
        var loads = new CaseLoads("P");
        loads.AddNodal(c[^1], new Vec3(1, 0, -1.2 * pcr));
        var r = Nonlinear.Solve(p, loads, new NonlinearOptions(SecondOrder: true));
        Assert.False(r.Ok);
        Assert.True(r.Unstable);
        Assert.Contains("unstable", r.Message);
    }

    // ------------------------------------------------------------ springs

    [Fact]
    public void A_column_on_a_vertical_spring_settles_P_over_k_and_the_spring_is_its_reaction()
    {
        const double L = 3, P = 120, kz = 6000;
        var p = new FrameProblem();
        var c = Column(p, 0, L, 2, [true, true, false, true, true, true], null);
        p.Links.Add(Link.Spring(c[0], [0, 0, kz, 0, 0, 0], "spring"));
        var loads = new CaseLoads("P");
        loads.AddNodal(c[^1], new Vec3(0, 0, -P));
        p.Cases.Add(loads);
        var s = FrameSolver.Solve(p);
        Assert.True(s.Ok);
        var r = s.Cases[0];
        Assert.True(Rel(-P / kz, r.U[c[0] * 6 + 2]) < 1e-9);
        Assert.True(Rel(-P / kz - P * L / (E * A), r.U[c[^1] * 6 + 2]) < 1e-9);
        Assert.True(Rel(P, r.Reactions[c[0]][2]) < 1e-9);
        Assert.True(r.Balance < 1e-9);
    }

    [Fact]
    public void A_cantilever_on_a_footing_turning_on_its_soil_moves_H_h3_over_3EI_plus_H_h2_over_k_theta()
    {
        const double h = 3, H = 10, ks = 30000, B = 1.5, Lf = 2.0;
        // The footing's soil, turning about global y (the force along x): ks·I of the footprint about y, B along y and Lf along x.
        var kTheta = ks * B * Lf * Lf * Lf / 12;
        var p = new FrameProblem();
        var c = Column(p, 0, h, 6, [true, true, false, true, false, true], null);
        p.Links.Add(Link.Spring(c[0], [0, 0, ks * B * Lf, 0, kTheta, 0], "footing"));
        var loads = new CaseLoads("H");
        loads.AddNodal(c[^1], new Vec3(H, 0, 0));
        p.Cases.Add(loads);
        var s = FrameSolver.Solve(p);
        Assert.True(s.Ok);
        var r = s.Cases[0];
        // Along +X, local 2 of a vertical bar: about local 3, the strong axis.
        var i = I33;
        Assert.True(Rel(H * h * h * h / (3 * E * i) + H * h * h / kTheta, r.U[c[^1] * 6]) < 1e-6, $"{r.U[c[^1] * 6]}");
        // The reactions: H back, the soil's moment H h.
        Assert.True(Rel(-H, r.Reactions[c[0]][0]) < 1e-9);
        Assert.True(Rel(H * h, Math.Abs(r.Reactions[c[0]][4])) < 1e-9);
    }

    [Fact]
    public void A_long_beam_on_a_Winkler_bed_under_a_force_deflects_P_beta_over_2k_as_Hetenyi()
    {
        const double L = 24, P = 100, k = 10000;
        const int n = 240;
        var dx = L / n;
        var p = new FrameProblem();
        var nodes = new int[n + 1];
        for (var i = 0; i <= n; i++)
            nodes[i] = p.AddNode(new Vec3(i * dx, 0, 0), $"n{i}", i == 0 ? [true, true, false, true, false, true] : [false, true, false, true, false, true]);
        for (var i = 0; i < n; i++) p.Elements.Add(Bar(nodes[i], nodes[i + 1], Vec3.UnitZ));
        for (var i = 0; i <= n; i++) p.Links.Add(Link.Spring(nodes[i], [0, 0, k * dx * (i == 0 || i == n ? 0.5 : 1), 0, 0, 0], $"w{i}"));
        var loads = new CaseLoads("P");
        loads.AddNodal(nodes[n / 2], new Vec3(0, 0, -P));
        p.Cases.Add(loads);
        var s = FrameSolver.Solve(p);
        Assert.True(s.Ok);
        var r = s.Cases[0];
        var beta = Math.Pow(k / (4 * E * I33), 0.25);
        Assert.True(Rel(-P * beta / (2 * k), r.U[nodes[n / 2] * 6 + 2]) < 1e-2, $"{r.U[nodes[n / 2] * 6 + 2]} vs {-P * beta / (2 * k)}");
        var m0 = Stations.At(p.Elements[n / 2], r.Elements[n / 2], 0).Mz;
        Assert.True(Rel(P / (4 * beta), m0) < 1e-2, $"{m0} vs {P / (4 * beta)}");
        Assert.True(Rel(P, r.SumReactions.Z) < 1e-9);
    }

    // ------------------------------------------------------------ one way

    /// <summary>A pinned frame l × h in the x–z plane, its columns and beam axially stiff, two diagonals; H along +x at its top left.</summary>
    static (FrameProblem P, CaseLoads Loads, int Rising, int Falling) Braced(double l, double h, double H, string? only)
    {
        var p = new FrameProblem();
        bool[] baseHeld = [true, true, true, true, false, true];
        bool[] topHeld = [false, true, false, true, false, true];
        var bl = p.AddNode(new Vec3(0, 0, 0), "BL", baseHeld);
        var br = p.AddNode(new Vec3(l, 0, 0), "BR", baseHeld);
        var tl = p.AddNode(new Vec3(0, 0, h), "TL", topHeld);
        var tr = p.AddNode(new Vec3(l, 0, h), "TR", topHeld);
        bool[] pin = [false, false, false, false, true, true];
        FrameElement Member(int i, int j, Vec3 l2, double a, string? o = null) => Bar(i, j, l2, a: a, relI: pin, relJ: pin, only: o);
        p.Elements.Add(Member(bl, tl, new Vec3(1, 0, 0), A * 1e4));
        p.Elements.Add(Member(br, tr, new Vec3(1, 0, 0), A * 1e4));
        p.Elements.Add(Member(tl, tr, Vec3.UnitZ, A * 1e4));
        // BL–TR lengthens under +x; TL–BR shortens.
        p.Elements.Add(Member(bl, tr, new Vec3(0, 1, 0), A, only));
        p.Elements.Add(Member(tl, br, new Vec3(0, 1, 0), A, only));
        var loads = new CaseLoads("H");
        loads.AddNodal(tl, new Vec3(H, 0, 0));
        return (p, loads, 3, 4);
    }

    static double Axial(CaseResult r, int element) => Nonlinear.AxialOf(r.Elements[element]);

    [Fact]
    public void X_bracing_shares_a_force_when_both_work_and_the_tension_diagonal_takes_it_all_when_only_tension_counts()
    {
        const double l = 6, h = 4, H = 30;
        var cos = l / Math.Sqrt(l * l + h * h);
        var (p, loads, up, down) = Braced(l, h, H, null);
        var both = Nonlinear.Solve(p, loads, new NonlinearOptions());
        Assert.True(both.Ok, both.Message);
        Assert.True(Rel(H / (2 * cos), Axial(both.Result!, up)) < 1e-3);
        Assert.True(Rel(-H / (2 * cos), Axial(both.Result!, down)) < 1e-3);

        var (q, l2, up2, down2) = Braced(l, h, H, "tension");
        var one = Nonlinear.Solve(q, l2, new NonlinearOptions());
        Assert.True(one.Ok, one.Message);
        Assert.True(one.Converged);
        Assert.Equal([down2], one.Slack);
        Assert.True(Rel(H / cos, Axial(one.Result!, up2)) < 1e-9);
        Assert.Equal(0, Axial(one.Result!, down2), 9);
        Assert.True(one.Result!.Balance < 1e-9);

        // Turned round: the other diagonal.
        var back = new CaseLoads("-H");
        back.AddNodal(2, new Vec3(-H, 0, 0));
        var other = Nonlinear.Solve(q, back, new NonlinearOptions());
        Assert.Equal([up2], other.Slack);
        Assert.True(Rel(H / cos, Axial(other.Result!, down2)) < 1e-9);
    }

    [Fact]
    public void Both_diagonals_slack_is_a_mechanism_said()
    {
        var (p, loads, _, _) = Braced(6, 4, 30, "compression");
        // Compression-only diagonals under a pull: the one stretched goes, the one shortened stays; with gravity pulling the top up both go.
        var lift = new CaseLoads("up");
        lift.AddNodal(2, new Vec3(0, 0, 50));
        lift.AddNodal(3, new Vec3(0, 0, 50));
        var r = Nonlinear.Solve(p, lift, new NonlinearOptions());
        Assert.False(r.Ok);
        Assert.NotNull(r.Mechanism);
        Assert.False(r.Unstable);
    }

    [Fact]
    public void A_support_that_would_pull_lifts_and_the_beam_works_as_a_span_with_an_overhang()
    {
        const double l = 4, w = 10;
        var p = new FrameProblem();
        var a = p.AddNode(new Vec3(0, 0, 0), "A", [true, true, true, true, false, true]);
        var b = p.AddNode(new Vec3(l, 0, 0), "B", [false, true, true, true, false, true]);
        var c = p.AddNode(new Vec3(2 * l, 0, 0), "C", [true, true, true, true, false, true]);
        p.Elements.Add(Bar(a, b, Vec3.UnitZ));
        p.Elements.Add(Bar(b, c, Vec3.UnitZ));
        var loads = new CaseLoads("w");
        loads.Span.Add((1, SpanLoad.Uniform(0, l, new Vec3(0, 0, -w))));
        // Linear: the far end pulls, −wl/16.
        var linear = Nonlinear.Solve(p, loads, new NonlinearOptions());
        Assert.True(Rel(-w * l / 16, linear.Result!.Reactions[a][2]) < 1e-9);
        p.Uplift.Add(a);
        var r = Nonlinear.Solve(p, loads, new NonlinearOptions());
        Assert.True(r.Ok, r.Message);
        Assert.Equal([a], r.Lifted);
        Assert.False(r.Result!.Reactions.ContainsKey(a) && Math.Abs(r.Result.Reactions[a][2]) > 1e-9);
        Assert.True(Rel(w * l / 2, r.Result.Reactions[b][2]) < 1e-9);
        Assert.True(Rel(w * l / 2, r.Result.Reactions[c][2]) < 1e-9);
        // A lifts: the overhang turns with B, up.
        Assert.True(r.Result.U[a * 6 + 2] > 0);
    }
}
