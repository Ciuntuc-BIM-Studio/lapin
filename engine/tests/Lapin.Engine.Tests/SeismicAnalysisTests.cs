using System.Text.Json.Nodes;
using Lapin.Analysis.Seismic;
using Lapin.Analysis.Solver;
using Lapin.Model;
using Lapin.Platform;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The modal and seismic analysis of a model (docs/plan-fem.md §11): the guide's frame with P100-1/2013's spectrum (its mass
/// that of G + ψ2 Q, its modes' Rayleigh quotients, the base shear each mode's M* Sd(T) combined by CQC, the storey's shear,
/// the seismic combination's envelope about the gravity loads, the ULS envelope taking it), the parameters set in one undo
/// step and refused out of range, a masonry house (its walls shells, cracked), a rigid diaphragm only where no shell floor
/// gives one, a mode's shape for the 3D view.
/// </summary>
public class SeismicAnalysisTests(ITestOutputHelper output)
{
    static JsonObject Q(Fixture f, string name, JsonObject? a = null) => f.Session.Query($"analysis.{name}", a ?? [])!.AsObject();

    static void Seismic(Fixture f, JsonObject? more = null)
    {
        var a = new JsonObject { ["ag"] = 0.30, ["tc"] = 0.7, ["q"] = 3 };
        if (more != null) foreach (var (k, v) in more) a[k] = v?.DeepClone();
        f.Run("analysis.setSeismic", a);
    }

    [Fact]
    public void The_guides_frame_has_its_modes_its_mass_and_a_base_shear_of_its_modes_combined()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 2 });
        Seismic(f);
        var modal = Q(f, "modal");
        Assert.True((bool)modal["ok"]!, modal.ToJsonString());
        var modes = modal["modes"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.NotEmpty(modes);
        foreach (var m in modes)
        {
            Assert.Equal(1, (double)m["rayleigh"]!, 6);
            Assert.True((double)m["residual"]! < 1e-4, m.ToJsonString());
        }
        Assert.True((bool)modal["reached"]!);
        // The mass: G1 + G2 + 0.3 Q-A (ψ2 of category A), the vertical loads over g; what is on the supports apart.
        var run = AnalysisRunTests.Run(f);
        var sum = run["cases"]!.AsArray().Select(c => c!.AsObject()).ToDictionary(c => (string)c["code"]!, c => -(double)c["sumLoads"]![2]!);
        var expected = (sum["G1"] + sum.GetValueOrDefault("G2") + 0.3 * sum["Q-A"]) / Spectrum.G;
        var total = (double)modal["totalMass"]![0]!;
        output.WriteLine($"mass {total:0.###} t free + {(double)modal["supportMass"]!:0.###} t on supports, expected {expected:0.###} t; T1 {(double)modes[0]["T"]!:0.0000} s");
        Assert.Equal(expected, total + (double)modal["supportMass"]!, 4);

        // Each mode's base shear is M*ₖ Sd(Tₖ): the X one, the modes combined by CQC (ξ 5 %).
        var sp = Spectrum.Of(new SeismicSettings { Ag = 0.30, Tc = 0.7, Q = 3 });
        var vx = modes.Select(m => (double)m["mass"]![0]! * sp.Design((double)m["T"]!)).ToList();
        var w = modes.Select(m => (double)m["omega"]!).ToList();
        var seismic = Q(f, "seismic");
        Assert.True((bool)seismic["ok"]!, seismic.ToJsonString());
        Assert.Equal(Spectrum.Cqc(vx, w, 0.05), (double)seismic["base"]![0]!, 2);
        // One storey: its shear is the base shear; the equivalent static force F = Sd(T1) m (λ = 1, one storey) near it.
        var storey = seismic["storeys"]!.AsArray().Select(s => s!.AsObject()).Single(s => (string?)s["id"] == t.Ground);
        Assert.Equal((double)seismic["base"]![0]!, (double)storey["shear"]![0]!, 2);
        Assert.InRange((double)seismic["base"]![0]! / (double)seismic["equivalent"]![0]!, 0.8, 1.01);
        Assert.True((double)storey["drift"]![0]! > 0);
        Assert.Equal(0.015, (double)storey["slsLimit"]!, 6);

        // The seismic combination: G + ψ2 Q ± AEd about the gravity loads' values.
        var env = f.Session.Query("analysis.results", new JsonObject { ["envelope"] = "seismic" })!.AsObject();
        Assert.Equal("envelope", (string?)env["kind"]);
        Assert.Equal(["seismic/+", "seismic/-"], env["combinations"]!.AsArray().Select(x => (string)x!).ToArray());
        var col = env["frames"]!.AsArray().Select(x => x!.AsObject()).Single(x => (string?)x["sourceId"] == t.Columns[0]);
        var foot = col["stations"]![0]!.AsObject();
        var (lo, hi) = ((double)foot["My"]![0]!, (double)foot["My"]![1]!);
        Assert.True(hi - lo > 1, $"{lo} {hi}");
        var a = AnalysisRun.Of(f.Session.Reader);
        var terms = SeismicRun.Of(f.Session.Reader).Gravity;
        // G1 and 0.3 Q-A (G2 holds nothing here).
        Assert.Equal([1.0, 0.3], terms.Select(x => x.Factor).ToArray());
        // The column's axial force about its gravity value: the mean of the bounds is it.
        var gravity = f.Session.Query("analysis.results", new JsonObject { ["combination"] = "slsq" })!.AsObject();
        var gcol = gravity["frames"]!.AsArray().Select(x => x!.AsObject()).Single(x => (string?)x["sourceId"] == t.Columns[0]);
        Assert.Equal((double)gcol["stations"]![0]!["N"]!, ((double)foot["N"]![0]! + (double)foot["N"]![1]!) / 2, 6);
        // The ULS envelope takes the seismic combination with the fundamental ones.
        var uls = f.Session.Query("analysis.results", new JsonObject { ["envelope"] = "uls" })!.AsObject();
        Assert.Contains(uls["combinations"]!.AsArray(), x => (string?)x == "seismic/+");
        // The shells too: the floor's values within the seismic bounds.
        var c = f.Session.Query("analysis.contours", new JsonObject { ["envelope"] = "seismic", ["quantity"] = "mx" })!.AsObject();
        var s0 = c["surfaces"]![0]!;
        Assert.All(Enumerable.Range(0, s0["min"]!.AsArray().Count), i => Assert.True((double)s0["min"]![i]! <= (double)s0["max"]![i]!));
        // A diagram of it.
        var d = f.Session.Query("analysis.diagram", new JsonObject { ["envelope"] = "seismic", ["quantity"] = "My" })!.AsObject();
        Assert.Equal(16, d["polylines"]!.AsArray().Count);
    }

    [Fact]
    public void The_seismic_parameters_take_one_undo_step_and_refuse_what_is_out_of_range()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        Assert.False((bool)Q(f, "seismicOptions")["given"]!);
        Assert.False((bool)Q(f, "modal")["ok"]!);
        Assert.Throws<CommandException>(() => f.Session.Query("analysis.results", new JsonObject { ["envelope"] = "seismic" }));
        Seismic(f, new JsonObject { ["importance"] = "II", ["modal"] = "srss" });
        var o = Q(f, "seismicOptions");
        Assert.Equal(0.3, (double)o["ag"]!);
        Assert.Equal(1.2, (double)o["gamma"]!);
        Assert.Equal("srss", (string?)o["modal"]);
        Assert.Equal("P100-1/2013", (string?)o["code"]);
        Assert.Equal(4, o["masonryQ"]!.AsArray().Count);
        f.Session.Undo();
        Assert.False((bool)Q(f, "seismicOptions")["given"]!);
        Assert.Throws<CommandException>(() => f.Run("analysis.setSeismic", new JsonObject { ["tc"] = 0.8 }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setSeismic", new JsonObject { ["q"] = 0 }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setSeismic", new JsonObject { ["importance"] = "V" }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setSeismic", new JsonObject { ["zone"] = 3 }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setSeismic", []));
        Seismic(f, new JsonObject { ["cracked"] = new JsonObject { ["concrete"] = 1.0 } });
        Assert.Equal(1.0, (double)Q(f, "seismicOptions")["cracked"]!["concrete"]!);
        Assert.Equal(0.5, (double)Q(f, "seismicOptions")["cracked"]!["masonry"]!);
        f.Run("analysis.setSeismic", new JsonObject { ["ag"] = null });
        Assert.False((bool)Q(f, "seismicOptions")["given"]!);
        // EN 1998-1 instead.
        Seismic(f, new JsonObject { ["code"] = "EN 1998-1", ["ground"] = "B", ["spectrum"] = 2 });
        var sp = Q(f, "spectrum");
        Assert.Equal("EN 1998-1", (string?)sp["code"]);
        Assert.Equal(1.35, (double)sp["S"]!);
        Assert.NotEmpty(sp["points"]!.AsArray());
        Assert.NotEmpty(sp["modes"]!.AsArray());
    }

    [Fact]
    public void Cracked_stiffness_lengthens_the_periods_by_the_root_of_its_factor()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        Seismic(f, new JsonObject { ["cracked"] = new JsonObject { ["concrete"] = 1.0 } });
        var t1 = (double)Q(f, "modal")["modes"]![0]!["T"]!;
        Seismic(f, new JsonObject { ["cracked"] = new JsonObject { ["concrete"] = 0.5 } });
        var t2 = (double)Q(f, "modal")["modes"]![0]!["T"]!;
        // Everything is concrete: K halves, the mass stays, T grows by √2.
        Assert.Equal(Math.Sqrt(2), t2 / t1, 4);
    }

    [Fact]
    public void A_masonry_house_s_walls_are_cracked_shells_and_its_storey_drifts_are_checked()
    {
        var f = LoadCaseTests.F();
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var wt = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Brick 25", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.25, ["core"] = true }),
        })!["id"]!;
        (double, double)[] o = [(0, 0), (6, 0), (6, 4), (0, 4)];
        for (var i = 0; i < 4; i++)
        {
            var w = f.Wall(o[i].Item1, o[i].Item2, o[(i + 1) % 4].Item1, o[(i + 1) % 4].Item2, wt);
            f.Run("element.setParam", new JsonObject { ["id"] = w, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
            f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = o[i].Item1, ["y1"] = o[i].Item2, ["x2"] = o[(i + 1) % 4].Item1, ["y2"] = o[(i + 1) % 4].Item2 });
        }
        var slabType = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Floor 15", ["layers"] = new JsonArray(new JsonObject { ["material"] = AnalysisRunTests.Concrete, ["thickness"] = 0.15, ["core"] = true }),
        })!["id"]!;
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = first, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["typeId"] = slabType });
        f.Run("analysis.defaultCases", []);
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = first, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 2 });
        Seismic(f, new JsonObject { ["q"] = 2.0, ["diaphragm"] = "rigid" });
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var s = SeismicRun.Of(f.Session.Reader);
        output.WriteLine($"{sw.ElapsedMilliseconds} ms ({s.Timings}; {s.System?.Neq} dofs, {s.Modal?.Iterations} steps, {s.Modal?.MassDofs} with mass): {string.Join(" | ", s.Notes)}");
        Assert.True(s.Ok, string.Join("\n", s.Notes));
        Assert.Contains(s.Problem!.Shells, x => x.Material == "masonry");
        var src = AnalysisRun.Of(f.Session.Reader).Problem.Problem;
        for (var k = 0; k < src.Shells.Count; k++) Assert.Equal(0.5 * src.Shells[k].E, s.Problem.Shells[k].E, 6);
        // The floor is a shell: no diaphragm imposed.
        Assert.Empty(s.Problem.Links);
        Assert.Contains(s.Notes, n => n.Contains("have a shell floor") || n.Contains("has a shell floor"));
        var seismic = Q(f, "seismic");
        var storey = seismic["storeys"]!.AsArray().Select(x => x!.AsObject()).First();
        output.WriteLine(storey.ToJsonString());
        Assert.True((double)storey["mass"]! > 0);
        Assert.True((double)storey["shear"]![0]! > 0 && (double)storey["shear"]![1]! > 0);
        // Masonry walls are stiff: the drift is far within 0.5 % of the storey's height.
        Assert.True((bool)storey["slsOk"]!);
        Assert.True((bool)storey["ulsOk"]!);
        Assert.Contains(seismic["notes"]!.AsArray(), n => ((string)n!).StartsWith("Masonry is linear elastic"));
    }

    [Fact]
    public void A_rigid_diaphragm_ties_a_level_without_a_shell_floor()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        var timber = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Timber floor", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Timber C24", ["thickness"] = 0.2, ["core"] = true }),
        })!["id"]!;
        f.Run("architecture.setSlabType", new JsonObject { ["ids"] = new JsonArray(t.Slab), ["typeId"] = timber });
        Seismic(f);
        var free = SeismicRun.Of(f.Session.Reader);
        Assert.True(free.Ok, string.Join("\n", free.Notes));
        Assert.Empty(free.Problem!.Links);
        Seismic(f, new JsonObject { ["diaphragm"] = "rigid" });
        var tied = SeismicRun.Of(f.Session.Reader);
        Assert.True(tied.Ok, string.Join("\n", tied.Notes));
        Assert.Equal(4, tied.Problem!.Links.Count);
        Assert.Contains(tied.Notes, n => n.StartsWith("Rigid diaphragm on 1 level"));
        // The four column heads move together along X in the first mode.
        var phi = tied.Modal!.Modes.First(m => m.Mass[0] > 0.5 * tied.Modal.TotalMass[0]).Phi;
        var heads = Enumerable.Range(0, tied.Problem.Nodes.Count).Where(n => tied.Problem.Nodes[n].Z > 2.5 && !tied.Problem.NodeIds[n].StartsWith("diaphragm")).ToList();
        var ux = heads.Select(n => phi[n * 6]).ToList();
        Assert.True(ux.Max() - ux.Min() < 1e-3 * Math.Abs(ux.Max()), string.Join(", ", ux));
    }

    [Fact]
    public void A_modes_shape_is_given_for_the_3D_view()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        Seismic(f);
        var m = Q(f, "mode", new JsonObject { ["mode"] = 1 });
        Assert.Equal(1, (int)m["mode"]!);
        Assert.Equal(8, m["polylines"]!.AsArray().Count);
        Assert.Equal(11, m["polylines"]![0]!["points"]!.AsArray().Count);
        var sh = m["shells"]![0]!;
        Assert.Equal(sh["positions"]!.AsArray().Count, sh["displaced"]!.AsArray().Count);
        Assert.True((double)m["scale"]! > 0);
        Assert.Throws<CommandException>(() => Q(f, "mode", new JsonObject { ["mode"] = 999 }));
    }
}
