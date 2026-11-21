using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Platform;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The sheeting's hold on purlins and rails (docs/plan-fem.md §14.15): Euler's pinned column braced across its weak axis
/// buckles about its strong one (π²EI33/L²), a rigid motion is not held, a beam's sideways deflection under a lateral load goes
/// (its reactions the same), a purlin over three rafters is held in two spans (a fly brace does not break them), and the
/// steel hall's purlins: their local modes leave its first buckling modes, one undo step.
/// </summary>
public class SheetingTests(ITestOutputHelper output)
{
    const double E = 210e6, G = 80.77e6, A = 53.8e-4, I33 = 8356e-8, I22 = 604e-8, J = 20.1e-8;

    static FrameElement Bar(int i, int j, Vec3 local2, Vec3 offI = default, Vec3 offJ = default, string id = "") => new()
    {
        Id = id.Length > 0 ? id : $"b{i}-{j}", I = i, J = j, Local2 = local2, E = E, G = G, A = A, I22 = I22, I33 = I33, Jt = J, OffI = offI, OffJ = offJ,
    };

    static double Rel(double expected, double actual) => Math.Abs(actual - expected) / Math.Abs(expected);

    [Fact]
    public void A_pinned_column_its_sheeting_braces_across_its_weak_axis_buckles_about_its_strong_one()
    {
        const double L = 5, P = 100;
        var p = new FrameProblem();
        const int n = 8;
        var ids = new int[n + 1];
        for (var k = 0; k <= n; k++) ids[k] = p.AddNode(new Vec3(0, 0, L * k / n), $"c{k}", k == 0 ? [true, true, true, false, false, true] : k == n ? [true, true, false, false, false, false] : null);
        for (var k = 0; k < n; k++) p.Elements.Add(Bar(ids[k], ids[k + 1], new Vec3(1, 0, 0)));
        var loads = new CaseLoads("P");
        loads.AddNodal(ids[n], new Vec3(0, 0, -P));
        var weak = Buckling.Of(p, loads).Alpha[0] * P;
        Assert.True(Rel(Math.PI * Math.PI * E * I22 / (L * L), weak) < 1e-4);

        var s = Sheeting.Restrain(p, [("column", Enumerable.Range(0, n).ToList())]);
        Assert.Equal((1, n - 1, 1), (s.Members, s.Nodes, s.Spans));
        var b = Buckling.Of(p, loads);
        Assert.True(b.Ok && b.Converged, b.Message);
        var strong = b.Alpha[0] * P;
        output.WriteLine($"weak {weak} kN, braced {strong} kN, π²EI33/L² = {Math.PI * Math.PI * E * I33 / (L * L)}");
        Assert.True(Rel(Math.PI * Math.PI * E * I33 / (L * L), strong) < 1e-3);
        // The mode moves along local 2 (x) only: its sideways (y, local 3) motion is the sheeting's to hold.
        var u = b.Modes[0];
        for (var k = 1; k < n; k++) Assert.True(Math.Abs(u[ids[k] * 6 + 1]) <= 1e-6 * Math.Abs(u[ids[n / 2] * 6]), $"node {k}: {u[ids[k] * 6 + 1]}");
    }

    [Fact]
    public void The_hold_lets_a_rigid_motion_through_arms_and_all()
    {
        var p = new FrameProblem();
        var a = p.AddNode(new Vec3(0, 0, 0), "a", null);
        var m1 = p.AddNode(new Vec3(0, 2, 0.1), "m1", null);
        var m2 = p.AddNode(new Vec3(0, 4, 0.1), "m2", null);
        var z = p.AddNode(new Vec3(0, 6, 0), "z", null);
        // A purlin along y on a rafter's axis 0.1 m below it at both ends (eccentric, by arms), its depth up a pitched roof.
        var up = new Vec3(-0.45, 0, 0.89).Normalized();
        p.Elements.Add(Bar(a, m1, up, offI: new Vec3(0, 0, 0.1)));
        p.Elements.Add(Bar(m1, m2, up));
        p.Elements.Add(Bar(m2, z, up, offJ: new Vec3(0, 0, 0.1)));
        var s = Sheeting.Restrain(p, [("purlin", [0, 1, 2])]);
        Assert.Equal(2, s.Nodes);
        var t = new Vec3(0.3, -0.2, 0.7);
        var w = new Vec3(0.05, -0.08, 0.11);
        foreach (var l in p.Links)
        {
            var u = new double[18];
            for (var i = 0; i < 3; i++)
            {
                var d = t + Vec3.Cross(w, p.Nodes[l.Nodes[i]]);
                (u[i * 6], u[i * 6 + 1], u[i * 6 + 2], u[i * 6 + 3], u[i * 6 + 4], u[i * 6 + 5]) = (d.X, d.Y, d.Z, w.X, w.Y, w.Z);
            }
            var top = 0.0;
            var f = 0.0;
            for (var r = 0; r < 18; r++)
            {
                var sum = 0.0;
                for (var c = 0; c < 18; c++) { sum += l.K[r * 18 + c] * u[c]; top = Math.Max(top, Math.Abs(l.K[r * 18 + c])); }
                f = Math.Max(f, Math.Abs(sum));
            }
            Assert.True(f <= 1e-9 * top, $"{l.Id}: {f} of {top}");
        }
    }

    [Fact]
    public void A_beams_sideways_deflection_goes_when_its_sheeting_braces_it_and_its_reactions_stay()
    {
        const double L = 6, P = 10;
        FrameProblem Beam()
        {
            var p = new FrameProblem();
            for (var k = 0; k <= 6; k++) p.AddNode(new Vec3(L * k / 6, 0, 0), $"n{k}", k == 0 ? [true, true, true, true, false, false] : k == 6 ? [false, true, true, false, false, false] : null);
            for (var k = 0; k < 6; k++) p.Elements.Add(Bar(k, k + 1, new Vec3(0, 0, 1)));
            return p;
        }
        var loads = new CaseLoads("P");
        loads.AddNodal(3, new Vec3(0, P, 0));
        var free = FrameSystem.Build(Beam()).System!.SolveCase(loads);
        Assert.True(Rel(P * L * L * L / (48 * E * I22), free.U[3 * 6 + 1]) < 1e-6);
        var p = Beam();
        Sheeting.Restrain(p, [("beam", Enumerable.Range(0, 6).ToList())]);
        var held = FrameSystem.Build(p).System!.SolveCase(loads);
        output.WriteLine($"mid-span sideways: {free.U[3 * 6 + 1]} m free, {held.U[3 * 6 + 1]} m braced");
        Assert.True(Math.Abs(held.U[3 * 6 + 1]) <= 1e-6 * free.U[3 * 6 + 1]);
        Assert.Equal(-P, held.SumReactions.Y, 6);
        Assert.Equal(free.SumReactions.Y, held.SumReactions.Y, 9);
    }

    [Fact]
    public void A_purlin_over_three_rafters_is_held_in_two_spans_its_fly_brace_breaking_none()
    {
        var p = new FrameProblem();
        // The purlin along y at z = 3 (depth up: local 3 along x, the rafters' direction), nodes at y = 0, 3, 6, 9, 12.
        var pn = Enumerable.Range(0, 5).Select(k => p.AddNode(new Vec3(0, 3 * k, 3), $"p{k}", null)).ToArray();
        for (var k = 0; k < 4; k++) p.Elements.Add(Bar(pn[k], pn[k + 1], new Vec3(0, 0, 1), id: $"pur#{k}"));
        // Rafters along x through y = 0, 6, 12, each from x = −3 to 3 (their ends held).
        foreach (var k in new[] { 0, 2, 4 })
        {
            var r0 = p.AddNode(new Vec3(-3, 3 * k, 3), $"r{k}a", [true, true, true, true, true, true]);
            var r1 = p.AddNode(new Vec3(3, 3 * k, 3), $"r{k}b", [true, true, true, true, true, true]);
            p.Elements.Add(Bar(r0, pn[k], new Vec3(0, 0, 1)));
            p.Elements.Add(Bar(pn[k], r1, new Vec3(0, 0, 1)));
        }
        // A fly brace from the purlin's node at y = 3 down to a rafter's flange, nearly vertical.
        var f = p.AddNode(new Vec3(0.4, 3.2, 1.8), "fly", [true, true, true, true, true, true]);
        p.Elements.Add(Bar(pn[1], f, new Vec3(1, 0, 0)));
        var s = Sheeting.Restrain(p, [("purlin", [0, 1, 2, 3])]);
        Assert.Equal((1, 2, 2), (s.Members, s.Nodes, s.Spans));
        Assert.Equal(new[] { pn[1], pn[3] }, p.Links.Select(l => l.Nodes[1]).Order().ToArray());
        Assert.All(p.Links, l => Assert.DoesNotContain(pn[2], new[] { l.Nodes[1] }));
        Assert.Equal(new[] { pn[0], pn[2] }, p.Links.Single(l => l.Nodes[1] == pn[1]).Nodes.Where((_, i) => i != 1).ToArray());
    }

    // ------------------------------------------------------------ the steel hall

    static JsonObject Q(Fixture f, string name, JsonObject? a = null) => f.Session.Query($"analysis.{name}", a ?? [])!.AsObject();

    [Fact]
    public void The_halls_purlins_braced_by_their_sheeting_leave_its_first_buckling_modes()
    {
        var f = AnalysisRunTests.Hall();
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 }, ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "III" } });
        var combos = Q(f, "run")["combinations"]!.AsArray().Select(c => c!.AsObject()).ToList();
        var key = (string)combos.First(c => (string)c["name"]! == "1.35 G1 + 1.5 S-X + 0.9 W-Y/s")["key"]!;
        JsonObject Buckling() => Q(f, "buckling", new JsonObject { ["combination"] = key })["combinations"]![0]!.AsObject();
        var before = Buckling();
        output.WriteLine($"before: {before["alpha"]!.ToJsonString()} global {before["global"]} — {before["modes"]![0]!["local"]?.ToJsonString()}");
        Assert.True((bool)before["modes"]![0]!["local"]!["purlin"]!);
        Assert.InRange((double)before["alpha"]![0]!, 1.7, 1.95);

        var set = f.Run("analysis.setSheeting", new JsonObject { ["purlins"] = true, ["sheeting"] = true })!.AsObject();
        Assert.True((int)set["members"]! > 50);
        var run = Q(f, "run");
        Assert.True((bool)run["ok"]!);
        Assert.True((int)run["sheeting"]!["members"]! > 10);
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).Contains("braced by their sheeting"));
        var after = Buckling();
        var locals = after["modes"]!.AsArray().Select(m => m!["local"]).Where(l => l != null).Select(l => l!.AsObject()).ToList();
        output.WriteLine($"after: {after["alpha"]!.ToJsonString()} global {after["global"]} — locals {string.Join("; ", locals.Select(l => l.ToJsonString()))}");
        Assert.DoesNotContain(locals, l => (bool)l["purlin"]!);
        Assert.True((double)after["alpha"]![0]! > (double)before["alpha"]![0]!);
        // The linear results balance as before (the hold is internal).
        Assert.All(run["cases"]!.AsArray(), c => Assert.True((bool?)c!["balanced"] != false, c.ToJsonString()));

        f.Session.Undo();
        Assert.Null(Q(f, "run")["sheeting"]);
        Assert.Equal((double)before["alpha"]![0]!, (double)Buckling()["alpha"]![0]!, 9);
        Assert.Throws<CommandException>(() => f.Run("analysis.setSheeting", new JsonObject { ["purlins"] = true, ["sheeting"] = "yes" }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setSheeting", new JsonObject { ["purlins"] = true }));
    }
}
