using Lapin.Analysis.Solver;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// What the nonlinear loop keeps between its iterations and combinations (docs/plan-fem.md §14.21): the layout (numbering,
/// ordering, symbolic factorization) while no support lifts, whatever goes slack or carries an axial force; a new one when a
/// support lifts or the problem changes; a bar's copy with an axial force or taken out equal, bit for bit, to one prepared
/// from nothing; a system on a kept layout solving bit for bit as one on a new layout.
/// </summary>
public class FrameLayoutTests
{
    const double E = 210e6, G = 80.77e6, A = 53.8e-4, I33 = 8356e-8, I22 = 604e-8, J = 20.1e-8;
    static readonly bool[] Pin = [false, false, false, false, true, true];

    static FrameElement Bar(int i, int j, Vec3 local2, bool[]? relI = null, bool[]? relJ = null, string? only = null, Vec3 offI = default, double a = A) => new()
    {
        Id = $"b{i}-{j}", I = i, J = j, Local2 = local2, E = E, G = G, A = a, I22 = I22, I33 = I33, Jt = J, As2 = 0.002, As3 = 0.003,
        ReleaseI = relI ?? new bool[6], ReleaseJ = relJ ?? new bool[6], Only = only, OffI = offI,
    };

    /// <summary>A braced bay of two storeys (pinned diagonals, tension only), its feet on supports that lift; a lateral load and gravity.</summary>
    static (FrameProblem P, CaseLoads Loads) Bay()
    {
        var p = new FrameProblem();
        bool[] foot = [true, true, true, true, true, true];
        bool[] plane = [false, true, false, true, false, true];
        var n = new int[6];
        for (var k = 0; k < 3; k++)
        {
            n[2 * k] = p.AddNode(new Vec3(0, 0, 3 * k), $"L{k}", k == 0 ? foot : plane);
            n[2 * k + 1] = p.AddNode(new Vec3(5, 0, 3 * k), $"R{k}", k == 0 ? foot : plane);
        }
        for (var k = 0; k < 2; k++)
        {
            p.Elements.Add(Bar(n[2 * k], n[2 * k + 2], new Vec3(1, 0, 0)));
            p.Elements.Add(Bar(n[2 * k + 1], n[2 * k + 3], new Vec3(1, 0, 0), offI: new Vec3(0.1, 0, 0)));
            p.Elements.Add(Bar(n[2 * k + 2], n[2 * k + 3], Vec3.UnitZ, relJ: Pin));
            p.Elements.Add(Bar(n[2 * k], n[2 * k + 3], new Vec3(0, 1, 0), Pin, Pin, "tension", a: A / 4));
            p.Elements.Add(Bar(n[2 * k + 1], n[2 * k + 2], new Vec3(0, 1, 0), Pin, Pin, "tension", a: A / 4));
        }
        p.Uplift.Add(n[0]);
        p.Uplift.Add(n[1]);
        var loads = new CaseLoads("W");
        loads.AddNodal(n[4], new Vec3(400, 0, -20));
        loads.AddNodal(n[5], new Vec3(0, 0, -20));
        loads.AddNodal(n[2], new Vec3(200, 0, -10));
        return (p, loads);
    }

    static void Same(double[] a, double[] b, string what)
    {
        Assert.Equal(a.Length, b.Length);
        for (var i = 0; i < a.Length; i++)
            if (BitConverter.DoubleToInt64Bits(a[i]) != BitConverter.DoubleToInt64Bits(b[i])) Assert.Fail($"{what}[{i}]: {a[i]:R} ≠ {b[i]:R}");
    }

    /// <summary>The same problem without its kept layouts: a system built from nothing.</summary>
    static FrameProblem Anew(FrameProblem p)
    {
        var q = new FrameProblem();
        for (var n = 0; n < p.Nodes.Count; n++) q.AddNode(p.Nodes[n], p.NodeIds[n], p.Held[n]);
        q.Elements.AddRange(p.Elements);
        q.Shells.AddRange(p.Shells);
        q.Links.AddRange(p.Links);
        foreach (var u in p.Uplift) q.Uplift.Add(u);
        return q;
    }

    [Fact]
    public void A_slack_member_or_an_axial_force_keeps_the_layout_a_lifted_support_takes_another()
    {
        var (p, _) = Bay();
        var all = Enumerable.Repeat(true, p.Elements.Count).ToArray();
        var first = FrameSystem.Build(Nonlinear.InStates(p, all, new HashSet<int>(), null)).System!;
        var slack = (bool[])all.Clone();
        slack[3] = slack[8] = false;
        var axial = Enumerable.Range(0, p.Elements.Count).Select(k => -10.0 * (k + 1)).ToArray();
        var second = FrameSystem.Build(Nonlinear.InStates(p, slack, new HashSet<int>(), axial), layout: first.Layout).System!;
        Assert.Same(first.Layout, second.Layout);
        // Without the hint, the problem's kept one.
        Assert.Same(first.Layout, FrameSystem.Build(Nonlinear.InStates(p, slack, new HashSet<int>(), null)).System!.Layout);
        // A support lifted: its translations become equations, a layout of its own; seated again, the first one.
        var (liftedSys, why) = FrameSystem.Build(Nonlinear.InStates(p, all, new HashSet<int> { 0 }, null), layout: first.Layout);
        Assert.True(liftedSys != null, why?.Refused ?? $"mechanism at {why?.Mechanism?.Node} dof {why?.Mechanism?.Dof}");
        var lifted = liftedSys!;
        Assert.NotSame(first.Layout, lifted.Layout);
        Assert.True(lifted.Neq > first.Neq);
        Assert.Same(first.Layout, FrameSystem.Build(Nonlinear.InStates(p, all, new HashSet<int>(), null), layout: lifted.Layout).System!.Layout);
        Assert.Equal(2, p.Layouts.Count);
        // The skyline factor has layouts of its own.
        Assert.NotSame(first.Layout, FrameSystem.Build(p, FactorKind.Skyline).System!.Layout);
    }

    [Fact]
    public void A_problem_changed_after_a_solve_gets_a_new_layout_and_its_own_results()
    {
        var (p, loads) = Bay();
        var before = FrameSystem.Build(p).System!;
        p.Held[3] = [true, true, true, true, true, true];
        var after = FrameSystem.Build(p, layout: before.Layout).System!;
        Assert.NotSame(before.Layout, after.Layout);
        Assert.True(after.Neq < before.Neq);
        Same(FrameSystem.Build(Anew(p)).System!.SolveCase(loads).U, after.SolveCase(loads).U, "U");
        Assert.True(p.Layouts.Count <= FrameLayouts.Kept);
    }

    [Fact]
    public void A_bars_copy_with_an_axial_force_or_taken_out_is_the_one_prepared_from_nothing()
    {
        var (p, _) = Bay();
        foreach (var e in p.Elements) e.Prepare(p.Nodes);
        foreach (var (n, inactive) in new[] { (-37.5, false), (12.25, false), (0.0, true), (-8.0, true) })
            for (var k = 0; k < p.Elements.Count; k++)
            {
                var e = p.Elements[k];
                var copy = e.With(n, inactive);
                // The same bar made anew from its data, prepared from nothing (a base never prepared gives an unprepared copy).
                var fresh = Bar(e.I, e.J, e.Local2, e.ReleaseI, e.ReleaseJ, e.Only, e.OffI, e.A).With(n, inactive);
                fresh.Prepare(p.Nodes);
                copy.Prepare(p.Nodes);
                Same(fresh.T, copy.T, $"{k}.T");
                Same(fresh.K, copy.K, $"{k}.K");
                Same(fresh.GlobalStiffness(), copy.GlobalStiffness(), $"{k}.global");
                Same(fresh.Geometric(-100), copy.Geometric(-100), $"{k}.geometric");
                Same(fresh.GeometricGlobal(55), copy.GeometricGlobal(55), $"{k}.geometricGlobal");
                var pe = fresh.Equivalent([new LocalLoad(0.5, 2, 1, 2, 3, 4, 5, 6, false)]);
                Same(fresh.Condense(pe), copy.Condense(pe), $"{k}.condense");
                var u = Enumerable.Range(0, 12).Select(x => 1e-3 * (x + 1) * (x % 3 == 0 ? -1 : 1)).ToArray();
                var (ua, ub) = (fresh.ToLocal(u), copy.ToLocal(u));
                fresh.Recover(ua, pe);
                copy.Recover(ub, pe);
                Same(ua, ub, $"{k}.recover");
                Same(fresh.EndForces(ua, pe), copy.EndForces(ub, pe), $"{k}.forces");
            }
    }

    [Fact]
    public void The_nonlinear_solve_on_kept_layouts_is_the_one_on_new_ones_bit_for_bit()
    {
        var (p, loads) = Bay();
        var r = Nonlinear.Solve(p, loads, new NonlinearOptions(SecondOrder: true));
        Assert.True(r.Ok, r.Message);
        Assert.NotEmpty(r.Slack);
        Assert.NotEmpty(r.Lifted);
        // Each iteration again, by hand, on problems with no kept layout.
        var q = Anew(p);
        var last = FrameSystem.Build(Anew(Nonlinear.InStates(q, r.Slack.Aggregate(Enumerable.Repeat(true, q.Elements.Count).ToArray(), (a, k) => { a[k] = false; return a; }), r.Lifted.ToHashSet(), null))).System!;
        Assert.NotNull(last);
        // The whole solve on a problem whose layouts are new, and on one whose are kept: the same bits.
        var again = Nonlinear.Solve(Anew(p), loads, new NonlinearOptions(SecondOrder: true));
        var kept = Nonlinear.Solve(p, loads, new NonlinearOptions(SecondOrder: true));
        Assert.Equal(again.Iterations, kept.Iterations);
        Same(again.Result!.U, kept.Result!.U, "U");
        Same(again.Axial, kept.Axial, "axial");
        Same(r.Result!.U, kept.Result.U, "U again");
    }
}
