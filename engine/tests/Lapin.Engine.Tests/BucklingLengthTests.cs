using Lapin.Analysis.Design;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The buckling lengths from αcr (docs/plan-fem.md §14.24) against closed forms: the portal frame's columns with pinned and
/// fixed bases (λ tan λ = 6 Ib h/(Ic L), λ cot λ = −6 Ib h/(Ic L): Timoshenko and Gere §2.8), a cantilever column (2 L), a
/// braced frame's columns (about their own length), a two-storey frame against the sway alignment chart (Julian and Lawrence;
/// AISC 360-16 Commentary eq. C-A-7-2), a beam with no compression and the participation's threshold; and the rule that
/// chooses the length a check uses (the user's k·L, the αcr length, their bounds; second order).
/// </summary>
public class BucklingLengthTests(ITestOutputHelper output)
{
    // Steel, an IPE 300's values (kPa, m², m⁴); no shear area: Euler–Bernoulli, as the closed forms.
    const double E = 210e6, G = 80.77e6, A = 53.8e-4, I33 = 8356e-8, I22 = 604e-8, J = 20.1e-8;
    static readonly bool[] Fixed = [true, true, true, true, true, true];
    static readonly bool[] Pinned = [true, true, true, false, false, true];

    static double Rel(double expected, double actual) => Math.Abs(actual - expected) / Math.Abs(expected);

    static double Root(Func<double, double> f, double a, double b)
    {
        for (var k = 0; k < 200; k++)
        {
            var m = (a + b) / 2;
            if (f(a) * f(m) <= 0) b = m; else a = m;
        }
        return (a + b) / 2;
    }

    /// <summary>The sway alignment chart's K for the joints' G at either end (AISC 360-16 Commentary eq. C-A-7-2).</summary>
    internal static double ChartK(double ga, double gb)
    {
        var x = Root(x => (ga * gb * x * x - 36) / (6 * (ga + gb)) - x / Math.Tan(x), 1e-6, Math.PI - 1e-9);
        return Math.PI / x;
    }

    /// <summary>Builds members of n pieces each in an x–z plane frame (out of the plane held at every node).</summary>
    sealed class Plane
    {
        public readonly FrameProblem P = new();
        public readonly List<int[]> Members = [];
        static readonly bool[] Free = [false, true, false, true, false, true];
        readonly Dictionary<(double, double), int> _at = [];

        public int Node(double x, double z, bool[]? held = null)
        {
            if (_at.TryGetValue((Math.Round(x, 9), Math.Round(z, 9)), out var had)) return had;
            bool[] h = held == null ? Free : [held[0], true, held[2], true, held[4], true];
            return _at[(Math.Round(x, 9), Math.Round(z, 9))] = P.AddNode(new Vec3(x, 0, z), $"n{P.Nodes.Count}", h);
        }

        /// <summary>A member from a to b in n pieces, local 2 in the plane (bending in the plane about local 3), its ends released about y as said.</summary>
        public int Member(int a, int b, int n, double i33, double area, bool pinI = false, bool pinJ = false)
        {
            var (pa, pb) = (P.Nodes[a], P.Nodes[b]);
            var d = (pb - pa).Normalized();
            var local2 = Math.Abs(d.Z) > 0.9 ? new Vec3(1, 0, 0) : Vec3.UnitZ;
            var ids = new int[n + 1];
            ids[0] = a;
            ids[n] = b;
            for (var k = 1; k < n; k++) ids[k] = Node(pa.X + (pb.X - pa.X) * k / n, pa.Z + (pb.Z - pa.Z) * k / n);
            var list = new int[n];
            var name = $"m{Members.Count}";
            for (var k = 0; k < n; k++)
            {
                bool[] ri = new bool[6], rj = new bool[6];
                if (k == 0 && pinI) ri[5] = true;
                if (k == n - 1 && pinJ) rj[5] = true;
                P.Elements.Add(new FrameElement
                {
                    Id = $"{name}~{k}", SourceId = name, Name = name, I = ids[k], J = ids[k + 1], Local2 = local2, E = E, G = G, A = area, I22 = I22, I33 = i33, Jt = J,
                    ReleaseI = ri, ReleaseJ = rj,
                });
                list[k] = P.Elements.Count - 1;
            }
            Members.Add(list);
            return Members.Count - 1;
        }

        public (BucklingResult B, MemberLength[] L) Solve(CaseLoads loads, int modes = 6)
        {
            var b = Buckling.Of(P, loads, modes);
            Assert.True(b.Ok, b.Message);
            return (b, BucklingLengths.Of(P, Members, b));
        }
    }

    /// <summary>The portal frame of S5's tests (§14.8): columns h, beam l, the same I, P down on each column's top; n pieces a member; inextensible as the closed form.</summary>
    static (Plane F, int Left, int Right, int Beam, CaseLoads Loads) Portal(bool[] bases, double h, double l, int n, double area = A * 1e4, double p = 100)
    {
        var f = new Plane();
        int b0 = f.Node(0, 0, bases), b1 = f.Node(l, 0, bases), t0 = f.Node(0, h), t1 = f.Node(l, h);
        var left = f.Member(b0, t0, n, I33, area);
        var right = f.Member(b1, t1, n, I33, area);
        var beam = f.Member(t0, t1, n, I33, area);
        var loads = new CaseLoads("P");
        loads.AddNodal(t0, new Vec3(0, 0, -p));
        loads.AddNodal(t1, new Vec3(0, 0, -p));
        return (f, left, right, beam, loads);
    }

    [Fact]
    public void A_pinned_base_portals_columns_take_pi_h_over_lambda_of_lambda_tan_lambda_equal_to_6_Ib_h_over_Ic_L()
    {
        const double h = 4, l = 6;
        var lambda = Root(x => x * Math.Tan(x) - 6 * h / l, 0.01, Math.PI / 2 - 1e-9);
        var exact = Math.PI * h / lambda; // K = 2.48428
        foreach (var n in new[] { 8, 1 })
        {
            var (f, left, right, beam, loads) = Portal(Pinned, h, l, n);
            var (b, lengths) = f.Solve(loads);
            output.WriteLine($"pinned bases, {n} piece(s) a member: αcr {b.Alpha[0]:0.#####}; left Lcr,y {lengths[left].Y?.Lcr:0.####} m (K {lengths[left].Y?.Lcr / h:0.####}), exact {exact:0.####} m (K {exact / h:0.####}); beam: {lengths[beam].Why}");
            Assert.True(Rel(exact, lengths[left].Y!.Lcr) < (n == 8 ? 1e-3 : 1e-2));
            Assert.True(Rel(exact, lengths[right].Y!.Lcr) < (n == 8 ? 1e-3 : 1e-2));
            Assert.Equal(1, lengths[left].Y!.Mode);
            Assert.InRange(lengths[left].Y!.Participation, 0.9, 1.1);
            // The frame's plane only: nothing about the weak axis; the beam carries no compression.
            Assert.Null(lengths[left].Z);
            Assert.Null(lengths[beam].Y);
            Assert.Equal("not in compression under the combination", lengths[beam].Why);
        }
    }

    [Fact]
    public void A_fixed_base_portals_columns_take_pi_h_over_lambda_of_lambda_cot_lambda_equal_to_minus_6_Ib_h_over_Ic_L()
    {
        const double h = 4, l = 6;
        var lambda = Root(x => x / Math.Tan(x) + 6 * h / l, Math.PI / 2 + 1e-9, Math.PI - 1e-9);
        var exact = Math.PI * h / lambda; // K = 1.22221
        Assert.True(Rel(exact / h, ChartK(1e-12, (I33 / h) / (I33 / l))) < 1e-5);
        foreach (var n in new[] { 8, 1 })
        {
            var (f, left, _, _, loads) = Portal(Fixed, h, l, n);
            var (b, lengths) = f.Solve(loads);
            output.WriteLine($"fixed bases, {n} piece(s): αcr {b.Alpha[0]:0.#####}; Lcr,y {lengths[left].Y?.Lcr:0.####} m (K {lengths[left].Y?.Lcr / h:0.####}), exact {exact:0.####} (K {exact / h:0.####})");
            Assert.True(Rel(exact, lengths[left].Y!.Lcr) < (n == 8 ? 1e-3 : 1e-2));
        }
    }

    [Fact]
    public void A_cantilever_column_takes_twice_its_length_about_both_axes()
    {
        const double h = 4;
        foreach (var n in new[] { 16, 1 })
        {
            var p = new FrameProblem();
            var ids = new int[n + 1];
            for (var k = 0; k <= n; k++) ids[k] = p.AddNode(new Vec3(0, 0, h * k / n), $"c{k}", k == 0 ? Fixed : null);
            for (var k = 0; k < n; k++)
                p.Elements.Add(new FrameElement { Id = $"c~{k}", SourceId = "c", I = ids[k], J = ids[k + 1], Local2 = new Vec3(1, 0, 0), E = E, G = G, A = A, I22 = I22, I33 = I33, Jt = J });
            var loads = new CaseLoads("P");
            loads.AddNodal(ids[^1], new Vec3(0, 0, -50));
            var b = Buckling.Of(p, loads, 6);
            var x = BucklingLengths.Of(p, [Enumerable.Range(0, n).ToArray()], b)[0];
            output.WriteLine($"cantilever, {n} piece(s): Lcr,z {x.Z?.Lcr:0.#####} (mode {x.Z?.Mode}), Lcr,y {x.Y?.Lcr:0.#####} (mode {x.Y?.Mode}); 2h = {2 * h}");
            Assert.True(Rel(2 * h, x.Z!.Lcr) < (n == 16 ? 1e-4 : 1e-2));
            Assert.True(Rel(2 * h, x.Y!.Lcr) < (n == 16 ? 1e-3 : 1e-2));
            Assert.Equal(1, x.Z.Mode);
        }
    }

    [Fact]
    public void A_braced_frames_columns_take_about_their_own_length()
    {
        // Columns pinned at both ends (on fixed nodes at the foot, under a beam at the head), a pinned diagonal: the columns buckle
        // between their ends (K = 1), the frame does not sway.
        const double h = 4, l = 6;
        foreach (var n in new[] { 8, 1 })
        {
            var f = new Plane();
            int b0 = f.Node(0, 0, Fixed), b1 = f.Node(l, 0, Fixed), t0 = f.Node(0, h), t1 = f.Node(l, h);
            var left = f.Member(b0, t0, n, I33, A * 1e4, pinI: true, pinJ: true);
            var right = f.Member(b1, t1, n, I33, A * 1e4, pinI: true, pinJ: true);
            f.Member(t0, t1, 1, I33, A * 1e4);
            f.Member(b0, t1, 1, I33 / 100, A, pinI: true, pinJ: true);
            var loads = new CaseLoads("P");
            loads.AddNodal(t0, new Vec3(0, 0, -100));
            loads.AddNodal(t1, new Vec3(0, 0, -100));
            var (b, lengths) = f.Solve(loads);
            output.WriteLine($"braced, {n} piece(s): α {string.Join(", ", b.Alpha.Select(a => a.ToString("0.###")))}; left {lengths[left].Y?.Lcr:0.####} m (mode {lengths[left].Y?.Mode}), right {lengths[right].Y?.Lcr:0.####} m; h = {h}");
            // Each column buckles on its own between its ends, in its own mode or (the two columns' equal modes mixed by the
            // iteration) one of both: at its own length h; the later modes (two and three half-waves) come after. One piece a column pinned at both ends cannot bend
            // between them at all (the modes found are the frame's stiff ones, their lengths short): the rule's k·L floor keeps h.
            foreach (var c in new[] { left, right })
            {
                if (n == 8)
                {
                    Assert.True(Rel(h, lengths[c].Y!.Lcr) < 1e-3, $"{lengths[c].Y!.Lcr} vs {h}");
                    Assert.True(Rel(b.Alpha[0], lengths[c].Y!.Alpha) < 1e-9);
                }
                var used = BucklingLengthRule.Choose(h, 1.0 * h, null, lengths[c].Y, secondOrder: false);
                Assert.Equal(h, used.Lcr, 9);
                Assert.Equal("k", used.Source);
            }
        }
    }

    [Fact]
    public void A_two_storey_frames_columns_lie_within_the_sway_alignment_charts_values()
    {
        // Fixed bases, two storeys of h, a bay of l; the upper columns half the lower's I, P on each joint of each floor: every
        // column at the same h√(N/EI), as the chart assumes. G at the first floor (ΣIc/h)/(Ib/l) = 2.25, at the roof 0.75.
        const double h = 4, l = 6, ic = I33, ib = I33;
        var f = new Plane();
        int b0 = f.Node(0, 0, Fixed), b1 = f.Node(l, 0, Fixed), m0 = f.Node(0, h), m1 = f.Node(l, h), r0 = f.Node(0, 2 * h), r1 = f.Node(l, 2 * h);
        var lower = f.Member(b0, m0, 8, ic, A * 1e4);
        f.Member(b1, m1, 8, ic, A * 1e4);
        var upper = f.Member(m0, r0, 8, ic / 2, A * 1e4);
        f.Member(m1, r1, 8, ic / 2, A * 1e4);
        f.Member(m0, m1, 8, ib, A * 1e4);
        f.Member(r0, r1, 8, ib, A * 1e4);
        var loads = new CaseLoads("P");
        foreach (var x in new[] { m0, m1, r0, r1 }) loads.AddNodal(x, new Vec3(0, 0, -100));
        var (b, lengths) = f.Solve(loads);
        var gMid = (ic / h + ic / 2 / h) / (ib / l);
        var gTop = (ic / 2 / h) / (ib / l);
        double kLower = ChartK(1e-12, gMid), kUpper = ChartK(gMid, gTop);
        double fLower = lengths[lower].Y!.Lcr / h, fUpper = lengths[upper].Y!.Lcr / h;
        output.WriteLine($"two storeys: αcr {b.Alpha[0]:0.####}; K lower {fLower:0.####} (chart {kLower:0.####}), upper {fUpper:0.####} (chart {kUpper:0.####})");
        // The frame buckles as a whole: one K for both storeys (the same h√(N/EI)), between the chart's two (each storey alone).
        Assert.True(Rel(fLower, fUpper) < 1e-6);
        Assert.InRange(fLower, kLower, kUpper);
        Assert.True(Rel(kLower, fLower) < 0.10 && Rel(kUpper, fUpper) < 0.10);
    }

    [Fact]
    public void The_rule_takes_the_given_length_k_L_under_second_order_or_a_small_force_and_else_the_longer_of_k_L_and_the_alpha_length()
    {
        const double l = 4;
        var sway = new AxisLength(9.937, 17.54, 1, 1.0);
        // Given: as it is, the αcr length shown beside it.
        var g = BucklingLengthRule.Choose(l, l, 5.5, sway, false);
        Assert.Equal((5.5, "given", 9.937), (g.Lcr, g.Source, g.FromAlpha!.Value));
        // Second order on: k·L, the non-sway length (5.2.2(3)b).
        Assert.Equal((l, "k"), (BucklingLengthRule.Choose(l, l, null, sway, true).Lcr, BucklingLengthRule.Choose(l, l, null, sway, true).Source));
        // A sway column: the αcr length above k·L.
        var used = BucklingLengthRule.Choose(l, l, null, sway, false);
        Assert.Equal((9.937, "alpha", 17.54, 1), (used.Lcr, used.Source, used.Alpha!.Value, used.Mode!.Value));
        Assert.DoesNotContain("over 3", used.How);
        // Past 3 L: used, and said.
        Assert.Contains("over 3 L", BucklingLengthRule.Choose(l, l, null, sway with { Lcr = 12.5 }, false).How);
        // Under k·L: k·L (the floor).
        Assert.Equal((2.8, "k"), (BucklingLengthRule.Choose(l, 2.8, null, sway with { Lcr = 2.5 }, false).Lcr, BucklingLengthRule.Choose(l, 2.8, null, sway with { Lcr = 2.5 }, false).Source));
        // None: k·L with the reason.
        var none = BucklingLengthRule.Choose(l, l, null, null, false, "not in compression under the combination");
        Assert.Equal(("k", "not in compression under the combination"), (none.Source, none.How));
    }

    [Fact]
    public void A_beam_with_a_little_thrust_is_carried_by_the_sway_mode_and_one_with_more_takes_its_length()
    {
        // The pinned-base portal, its beam squeezed by H at each head (inwards): at 2 % of the columns' P it is carried by the sway
        // (under 5 % of their compression), at 20 % it takes the mode's length, π √(EIb/(αcr H)).
        const double h = 4, l = 6, p = 100;
        foreach (var (share, takes) in new[] { (0.02, false), (0.2, true) })
        {
            var (f, left, _, beam, loads) = Portal(Pinned, h, l, 8, p: p);
            var t0 = f.P.Elements[f.Members[beam][0]].I;
            var t1 = f.P.Elements[f.Members[beam][^1]].J;
            loads.AddNodal(t0, new Vec3(share * p, 0, 0));
            loads.AddNodal(t1, new Vec3(-share * p, 0, 0));
            var (b, lengths) = f.Solve(loads);
            var x = lengths[beam];
            output.WriteLine($"beam thrust {share * p} kN: αcr {b.Alpha[0]:0.###}; beam N {x.N:0.###}, Lcr,y {x.Y?.Lcr:0.###} ({x.Why}); column Lcr,y {lengths[left].Y?.Lcr:0.###}");
            Assert.True(Rel(share * p, -x.N) < 1e-6);
            if (takes)
            {
                Assert.True(Rel(Math.PI * Math.Sqrt(E * I33 / (b.Alpha[x.Y!.Mode - 1] * -x.N)), x.Y.Lcr) < 1e-12);
                Assert.Null(x.Z);
            }
            else
            {
                Assert.Null(x.Y);
                Assert.StartsWith("its compression under 0.05", x.Why);
            }
            Assert.NotNull(lengths[left].Y);
        }
    }
}
