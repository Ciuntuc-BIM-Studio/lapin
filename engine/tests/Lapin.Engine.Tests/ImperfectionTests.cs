using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Platform;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// S5d, the global sway imperfections (docs/plan-fem.md §14.14): EN 1993-1-1 5.3.2(3)'s φ = φ0·αh·αm and its limits, timber's
/// EN 1995-1-1 5.4.4(2), the rows and the 50 % rule for m, the equivalent forces on a pinned-base portal against the same frame
/// under H = ΣφP (first order, exactly) and the second-order amplification, then the guide's frame: four variants of every
/// ultimate combination, φ = 1/230.9, the envelope at least the one without, one undo step, second order bringing them.
/// </summary>
public class ImperfectionTests(ITestOutputHelper output)
{
    const double E = 210e6, G = 80.77e6, A = 53.8e-4, I33 = 8356e-8, I22 = 604e-8, J = 20.1e-8;

    static FrameElement Bar(int i, int j, Vec3 local2, string? material = null) => new()
    {
        Id = $"b{i}-{j}", I = i, J = j, Local2 = local2, E = E, G = G, A = A, I22 = I22, I33 = I33, Jt = J, Material = material,
    };

    // ------------------------------------------------------------ the formula

    [Fact]
    public void Phi_is_phi0_alpha_h_alpha_m_with_its_limits_and_timbers_own()
    {
        Assert.Equal(2 / Math.Sqrt(6), Imperfections.AlphaH(6), 12);
        Assert.Equal(0.816497, Imperfections.AlphaH(6), 6);
        Assert.Equal(Math.Sqrt(0.75), Imperfections.AlphaM(2), 12);
        Assert.Equal(1 / (200 * Math.Sqrt(2)), Imperfections.Phi(6, 2), 12);
        Assert.Equal(0.00353553, Imperfections.Phi(6, 2), 8);
        Assert.Equal(1, Imperfections.AlphaH(3), 12);
        Assert.Equal(2.0 / 3, Imperfections.AlphaH(16), 12);
        Assert.Equal(1, Imperfections.AlphaM(1), 12);
        Assert.Equal(1.0 / 200, Imperfections.Phi(3, 1), 12);
        Assert.Equal(0.005, Imperfections.PhiTimber(5), 12);
        Assert.Equal(0.0025, Imperfections.PhiTimber(20), 12);
    }

    // ------------------------------------------------------------ rows and m

    [Fact]
    public void M_counts_a_rows_columns_carrying_at_least_half_its_mean_load()
    {
        var p = new FrameProblem();
        var axial = new List<double>();
        void Column(double x, double y, double n)
        {
            var a = p.AddNode(new Vec3(x, y, 0), $"a{x}-{y}", [true, true, true, true, true, true]);
            var b = p.AddNode(new Vec3(x, y, 4), $"b{x}-{y}", null);
            p.Elements.Add(Bar(a, b, new Vec3(1, 0, 0)));
            axial.Add(-n);
        }
        // Row y = 0: 100, 100, 100, 20 kN (mean 80, half 40: m = 3); row y = 6: two of 100 (m = 2).
        Column(0, 0, 100); Column(5, 0, 100); Column(10, 0, 100); Column(15, 0, 20);
        Column(0, 6, 100); Column(5, 6, 100);
        // A beam (horizontal) and a column in tension are not columns.
        p.Elements.Add(Bar(1, 3, Vec3.UnitZ)); axial.Add(-50);
        var t = p.AddNode(new Vec3(20, 0, 0), "t0", [true, true, true, true, true, true]);
        var tt = p.AddNode(new Vec3(20, 0, 4), "t1", null);
        p.Elements.Add(Bar(t, tt, new Vec3(1, 0, 0))); axial.Add(30);
        var x = Imperfections.Of(p, [.. axial], new Vec3(1, 0, 0), "x");
        Assert.Equal(4, x.H, 12);
        Assert.Equal(1, x.AlphaH, 12);
        Assert.Equal([(4, 3), (2, 2)], x.Rows.Select(r => (r.Lines, r.M)).OrderByDescending(r => r.Lines).ToArray());
        Assert.Equal(Imperfections.Phi(4, 3), x.PhiOf[0], 12);
        Assert.Equal(Imperfections.Phi(4, 2), x.PhiOf[4], 12);
        Assert.Equal((Imperfections.Phi(4, 2), 2), x.Governing);
        Assert.Equal(6, x.PhiOf.Count);
        // ΣφN, and each column's pair: +φN at its top, −φN at its foot.
        Assert.Equal(Imperfections.Phi(4, 3) * 320 + Imperfections.Phi(4, 2) * 200, x.Force, 9);
        Assert.Equal(Imperfections.Phi(4, 3) * 100, x.Loads.Nodal[p.Elements[0].J].X, 12);
        Assert.Equal(-Imperfections.Phi(4, 3) * 100, x.Loads.Nodal[p.Elements[0].I].X, 12);
        // Along Y the rows are the columns of one X: x = 0 and x = 5 two each (m = 2), x = 10 and x = 15 one (m = 1).
        var y = Imperfections.Of(p, [.. axial], new Vec3(0, 1, 0), "y");
        Assert.Equal([(1, 1), (1, 1), (2, 2), (2, 2)], y.Rows.Select(r => (r.Lines, r.M)).OrderBy(r => r.Lines).ToArray());
        Assert.Equal(Imperfections.Phi(4, 1), y.PhiOf[3], 12);
        Assert.Equal(0, y.Loads.Nodal[p.Elements[0].J].X, 12);
        Assert.True(y.Loads.Nodal[p.Elements[0].J].Y > 0);
        // Timber columns take EN 1995-1-1's φ.
        foreach (var k in new[] { 0, 1, 2, 3, 4, 5 }) p.Elements[k] = new FrameElement
        {
            Id = p.Elements[k].Id, I = p.Elements[k].I, J = p.Elements[k].J, Local2 = new Vec3(1, 0, 0), E = E, G = G, A = A, I22 = I22, I33 = I33, Jt = J, Material = "timber",
        };
        var w = Imperfections.Of(p, [.. axial], new Vec3(1, 0, 0), "w");
        Assert.Equal(0.005, w.PhiOf[0], 12);
        Assert.True(w.Timber);
        Assert.Equal((0.005, 0), w.Governing);
    }

    // ------------------------------------------------------------ the portal frame

    /// <summary>A pinned-base portal in the x–z plane (columns h, beam l, 8 elements a bar, out of its plane held), its top nodes.</summary>
    static (FrameProblem P, int Left, int Right) Portal(double h, double l)
    {
        var p = new FrameProblem();
        bool[] baseHold = [true, true, true, true, false, true];
        var free = new[] { false, true, false, true, false, true };
        const int n = 8;
        var left = new int[n + 1];
        var right = new int[n + 1];
        for (var k = 0; k <= n; k++) left[k] = p.AddNode(new Vec3(0, 0, h * k / n), $"l{k}", k == 0 ? baseHold : free);
        for (var k = 0; k <= n; k++) right[k] = p.AddNode(new Vec3(l, 0, h * k / n), $"r{k}", k == 0 ? baseHold : free);
        var beam = new int[n + 1];
        beam[0] = left[n];
        beam[n] = right[n];
        for (var k = 1; k < n; k++) beam[k] = p.AddNode(new Vec3(l * k / n, 0, h), $"b{k}", free);
        for (var k = 0; k < n; k++) p.Elements.Add(Bar(left[k], left[k + 1], new Vec3(1, 0, 0)));
        for (var k = 0; k < n; k++) p.Elements.Add(Bar(right[k], right[k + 1], new Vec3(1, 0, 0)));
        for (var k = 0; k < n; k++) p.Elements.Add(Bar(beam[k], beam[k + 1], Vec3.UnitZ));
        return (p, left[n], right[n]);
    }

    [Fact]
    public void A_portals_imperfection_forces_sway_it_as_sum_phi_P_at_its_top_and_second_order_amplifies_them()
    {
        const double h = 6, l = 6, P = 500;
        var (p, left, right) = Portal(h, l);
        var loads = new CaseLoads("P");
        loads.AddNodal(left, new Vec3(0, 0, -P));
        loads.AddNodal(right, new Vec3(0, 0, -P));
        var (sys, _) = FrameSystem.Build(p);
        var first = sys!.SolveCase(loads);
        var axial = first.Elements.Select(Nonlinear.AxialOf).ToArray();
        var imp = Imperfections.Of(p, axial, new Vec3(1, 0, 0), "imp");
        var phi = 1 / (200 * Math.Sqrt(2));
        Assert.Equal(h, imp.H, 9);
        Assert.Equal([(2, 2)], imp.Rows.Select(r => (r.Lines, r.M)).ToArray());
        Assert.Equal(phi * 2 * P, imp.Force, 6);
        Assert.Equal(phi * P, imp.Loads.Nodal[left].X, 9);
        Assert.Equal(1.76777, imp.Loads.Nodal[left].X, 5);

        // The variant: the loads and the pairs; the same frame under H = φP at each top.
        var variant = new CaseLoads("P+X");
        foreach (var (n, f) in loads.Nodal) variant.AddNodal(n, f);
        foreach (var (n, f) in imp.Loads.Nodal) variant.AddNodal(n, f);
        var equivalent = new CaseLoads("P+H");
        foreach (var (n, f) in loads.Nodal) equivalent.AddNodal(n, f);
        equivalent.AddNodal(left, new Vec3(phi * P, 0, 0));
        equivalent.AddNodal(right, new Vec3(phi * P, 0, 0));
        var rv = sys.SolveCase(variant);
        var re = sys.SolveCase(equivalent);
        var sway = rv.U[left * 6];
        Assert.True(sway > 0);
        Assert.True(Math.Abs(sway - re.U[left * 6]) <= 1e-9 * Math.Abs(sway));
        // The pairs are in balance: the horizontal reactions sum to nought.
        Assert.True(Math.Abs(rv.SumReactions.X) < 1e-9, rv.SumReactions.ToString());
        // Each column carries φP of shear: the moment at its top φP·h.
        var top = p.Elements.FindIndex(e => e.J == left);
        output.WriteLine($"φ = {phi}, forces {phi * P} kN, sway {sway} m, moment at the top {rv.Elements[top].Forces[10]} / {rv.Elements[top].Forces[11]}");
        Assert.Equal(phi * P * h, Math.Abs(rv.Elements[top].Forces[10]) + Math.Abs(rv.Elements[top].Forces[11]), 3);

        // Second order: the variant's sway over its first-order one about 1/(1 − 1/αcr).
        var alpha = Buckling.Of(p, loads).Alpha[0];
        var second = Nonlinear.Solve(p, variant, new NonlinearOptions(true));
        Assert.True(second.Ok && second.Converged);
        var amp = second.Result!.U[left * 6] / sway;
        output.WriteLine($"αcr = {alpha}, amplification {amp}, 1/(1 − 1/αcr) = {1 / (1 - 1 / alpha)}");
        Assert.True(Math.Abs(amp - 1 / (1 - 1 / alpha)) <= 0.05 * (1 / (1 - 1 / alpha)));
    }

    // ------------------------------------------------------------ the guide's frame

    static JsonObject Q(Fixture f, string name, JsonObject? a = null) => f.Session.Query($"analysis.{name}", a ?? [])!.AsObject();

    [Fact]
    public void The_guide_frames_ultimate_combinations_take_four_imperfection_variants_with_phi_1_over_231()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 }, ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "III" } });
        var before = Q(f, "results", new JsonObject { ["envelope"] = "uls" });
        Assert.False((bool)Q(f, "options")["imperfections"]!);

        // Second order switched on brings the imperfections (the project has said nothing of them).
        f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = true });
        Assert.True((bool)Q(f, "options")["imperfections"]!);
        var run = Q(f, "run");
        Assert.True((bool)run["ok"]!, run["notes"]!.ToJsonString());
        var nl = run["nonlinear"]!.AsObject();
        var imp = nl["imperfections"]!.AsObject();
        output.WriteLine(imp.ToJsonString());
        Assert.Equal(1, (double)imp["alphaH"]!, 9);
        var uls = run["combinations"]!.AsArray().Where(c => (string)c!["kind"]! is "uls" or "accidental").Select(c => (string)c!["key"]!).ToHashSet();
        var rows = nl["combinations"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.Equal(run["combinations"]!.AsArray().Count, rows.Count);
        foreach (var r in rows)
        {
            Assert.True((bool)r["ok"]!, r.ToJsonString());
            if (!uls.Contains((string)r["key"]!)) { Assert.Null(r["variants"]); continue; }
            var vs = r["variants"]!.AsArray().Select(x => x!.AsObject()).ToList();
            Assert.Equal(["+X", "-X", "+Y", "-Y"], vs.Select(v => (string)v["variant"]!).ToArray());
            Assert.All(vs, v => Assert.True((bool)v["ok"]! && (bool)v["converged"]!, v.ToJsonString()));
            Assert.Contains((string)r["governing"]!, new[] { "+X", "-X", "+Y", "-Y" });
            Assert.EndsWith(" + φ(+X)", (string)vs[0]["name"]!);
        }
        // A gravity combination's columns carry alike: two to a row, m = 2, φ = √0.75 / 200 = 1/230.9.
        var gravity = rows.First(r => uls.Contains((string)r["key"]!) && !((string)r["name"]!).Contains('W'));
        foreach (var v in gravity["variants"]!.AsArray())
        {
            Assert.Equal(2, (int)v!["m"]!);
            Assert.Equal(Math.Sqrt(0.75) / 200, (double)v["phi"]!, 8);
            Assert.Equal(0.00433013, (double)v["phi"]!, 8);
        }
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("Global sway imperfections (EN 1993-1-1 5.3.2"));
        Assert.DoesNotContain(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("The global imperfections (EN 1993-1-1 5.3.2) are off"));

        // A combination's result is the envelope of its four; the ULS envelope sways at least as much as without them.
        var key = (string)gravity["key"]!;
        Assert.Equal("envelope", (string?)Q(f, "results", new JsonObject { ["combination"] = key })["kind"]);
        var with = Q(f, "results", new JsonObject { ["envelope"] = "uls" });
        Assert.True((double)with["maxima"]!["displacement"]!["value"]! >= (double)before["maxima"]!["displacement"]!["value"]! - 1e-9);
        // The checks run on the variants and name the one that governs.
        var checks = Q(f, "checks").ToJsonString();
        Assert.Matches("\"combination\":\"[^\"]*\\|[-+][XY]\"", checks);
        Assert.NotEmpty(Q(f, "buckling")["combinations"]!.AsArray());

        // Off, it stays off when second order is switched on again; P-Δ alone has one set a combination, as in S5.
        f.Run("analysis.setOptions", new JsonObject { ["imperfections"] = false });
        f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = false });
        f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = true });
        Assert.False((bool)Q(f, "options")["imperfections"]!);
        var plain = Q(f, "run");
        Assert.All(plain["nonlinear"]!["combinations"]!.AsArray(), r => Assert.Null(r!["variants"]));
        Assert.Contains(plain["notes"]!.AsArray(), n => ((string)n!).StartsWith("The global imperfections (EN 1993-1-1 5.3.2) are off"));
        Assert.Equal("combination", (string?)Q(f, "results", new JsonObject { ["combination"] = key })["kind"]);
        // Each a step back.
        f.Session.Undo();
        f.Session.Undo();
        f.Session.Undo();
        Assert.True((bool)Q(f, "options")["imperfections"]!);
        f.Session.Undo();
        Assert.False((bool)Q(f, "options")["imperfections"]!);
        Assert.False((bool)Q(f, "options")["secondOrder"]!);
        Assert.Null(Q(f, "run")["nonlinear"]);
        Assert.Throws<CommandException>(() => f.Run("analysis.setOptions", new JsonObject { ["imperfections"] = "yes" }));
    }

    [Fact]
    public void Imperfections_alone_solve_the_ultimate_combinations_to_first_order_and_leave_the_serviceability_ones()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        var sls = Q(f, "results", new JsonObject { ["envelope"] = "sls" }).ToJsonString();
        f.Run("analysis.setOptions", new JsonObject { ["imperfections"] = true });
        Assert.False((bool)Q(f, "options")["secondOrder"]!);
        var run = Q(f, "run");
        Assert.True((bool)run["ok"]!);
        var rows = run["nonlinear"]!["combinations"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, r => { Assert.NotNull(r["variants"]); Assert.Equal(1, (int)r["iterations"]!); });
        // The serviceability envelope is the superposition it was.
        Assert.Equal(sls, Q(f, "results", new JsonObject { ["envelope"] = "sls" }).ToJsonString());
        Assert.DoesNotContain(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("Second order (P-Δ)"));
    }
}
