using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// S2b (docs/plan-fem.md §10.10): the slabs and walls of the model analysed as shells in analysis.run. The guide's frame
/// with its floor a shell (balanced, its moments near the plate's, the beams working with it as a T), a floor on four line
/// supports against Navier's series for the simply supported plate and its convergence with the mesh, a flat slab on four
/// columns, the beams cut at the slab's nodes and recomposed whole, masonry walls as shells (§11.1) and a masonry wall as a deep cantilever, a timber floor still
/// by the 45° rule, concrete walls as shells on their footings, the options (one undo step), and analysis.contours' shape.
/// </summary>
public class ShellAnalysisTests(ITestOutputHelper output)
{
    const string Concrete = AnalysisRunTests.Concrete;

    static JsonObject Results(Fixture f, JsonObject a) => f.Session.Query("analysis.results", a)!.AsObject();
    static JsonObject Contours(Fixture f, JsonObject a) => f.Session.Query("analysis.contours", a)!.AsObject();

    /// <summary>A shell's node nearest a point, with its values.</summary>
    static JsonObject NodeAt(JsonObject results, string shellId, Vec3 at) =>
        results["shells"]!.AsArray().Select(s => s!.AsObject()).First(s => (string?)s["sourceId"] == shellId || (string?)s["id"] == shellId)["nodes"]!.AsArray()
            .Select(n => n!.AsObject()).MinBy(n => (new Vec3((double)n["at"]![0]!, (double)n["at"]![1]!, (double)n["at"]![2]!) - at).Length)!;

    static string SlabType(Fixture f, string material = Concrete, double t = 0.2) => (string)f.Run("architecture.createSlabType", new JsonObject
    {
        ["name"] = $"Floor {material} {t}", ["layers"] = new JsonArray(new JsonObject { ["material"] = material, ["thickness"] = t, ["core"] = true }),
    })!["id"]!;

    static string WallType(Fixture f, string material, double t) => (string)f.Run("architecture.createWallType", new JsonObject
    {
        ["name"] = $"Wall {material} {t}", ["layers"] = new JsonArray(new JsonObject { ["material"] = material, ["thickness"] = t, ["core"] = true }),
    })!["id"]!;

    [Fact]
    public void A_masonry_or_concrete_wall_that_says_nothing_of_its_load_bears_it_unless_thinner_than_a_partition()
    {
        // §11.10: LoadBearing false → out; unset → in for masonry and concrete with a core of 10 cm or more, said in a note;
        // a thinner one or one of another material stays a partition; true → in, whatever it is.
        var f = LoadCaseTests.F();
        f.Run("analysis.defaultCases", []);
        var brick = WallType(f, "Brick", 0.25);
        var unsaid = f.Wall(0, 0, 6, 0, brick);
        var concrete = f.Wall(6, 0, 6, 4, WallType(f, Concrete, 0.2));
        var marked = f.Wall(6, 4, 0, 4, WallType(f, "Brick", 0.24));
        f.Run("element.setParam", new JsonObject { ["id"] = marked, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = false });
        var thin = f.Wall(0, 4, 0, 0, WallType(f, "Brick", 0.075));
        var board = f.Wall(3, 0, 3, 4, WallType(f, "Plasterboard", 0.125));
        var run = AnalysisRun.Of(f.Session.Reader);
        var am = run.Problem.Model;
        var taken = am.Shells.Select(x => x.Source.SourceId).ToHashSet();
        Assert.Contains(unsaid, taken);
        Assert.Contains(concrete, taken);
        Assert.DoesNotContain(marked, taken);
        Assert.DoesNotContain(thin, taken);
        Assert.DoesNotContain(board, taken);
        string Why(string id) => am.LeftOut.Single(l => l.SourceId == id).Reason;
        Assert.Equal("not load-bearing (Pset_WallCommon.LoadBearing)", Why(marked));
        Assert.Contains("thinner than 10 cm", Why(thin));
        Assert.Contains("not of masonry or concrete", Why(board));
        string Name(string id) => f.Model.Get(id)!.Name;
        var note = Assert.Single(run.Notes, n => n.StartsWith("Walls that say nothing of their load taken as load-bearing"));
        Assert.Contains(Name(unsaid), note);
        Assert.Contains(Name(concrete), note);
        Assert.DoesNotContain(Name(marked), note);
        Assert.Contains(run.Notes, n => n.StartsWith("Walls that say nothing of their load, thinner than 10 cm") && n.Contains(Name(thin)));
        // Its weight: the partitions left out are loads on the floors, the walls taken are not.
        var partitions = LoadsReport.Compute(f.Session.Reader).Derived.Where(d => d.Kind == "partitions").Select(d => d.SourceId).ToHashSet();
        Assert.DoesNotContain(unsaid, partitions);
        // Marked true, the thin wall bears load too, and the note no longer names it.
        f.Run("element.setParam", new JsonObject { ["id"] = thin, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        run = AnalysisRun.Of(f.Session.Reader);
        Assert.Contains(run.Problem.Model.Shells, x => x.Source.SourceId == thin);
        Assert.DoesNotContain(run.Notes, n => n.StartsWith("Walls that say nothing of their load, thinner"));
    }

    /// <summary>
    /// Navier's double series for a simply supported rectangular plate a × b under a uniform q (Timoshenko and
    /// Woinowsky-Krieger, "Theory of Plates and Shells", §28): w and the bending moments at (x, y), Kirchhoff's theory,
    /// sagging positive.
    /// </summary>
    static (double W, double Mx, double My) Navier(double a, double b, double q, double d, double nu, double x, double y)
    {
        double w = 0, mx = 0, my = 0;
        for (var m = 1; m < 400; m += 2)
            for (var n = 1; n < 400; n += 2)
            {
                var k = m * m / (a * a) + n * n / (b * b);
                var wmn = 16 * q / (Math.Pow(Math.PI, 6) * d * m * n * k * k);
                var s = Math.Sin(m * Math.PI * x / a) * Math.Sin(n * Math.PI * y / b);
                w += wmn * s;
                mx += d * wmn * Math.PI * Math.PI * (m * m / (a * a) + nu * n * n / (b * b)) * s;
                my += d * wmn * Math.PI * Math.PI * (nu * m * m / (a * a) + n * n / (b * b)) * s;
            }
        return (w, mx, my);
    }

    /// <summary>A concrete floor 6 × 4 m, 20 cm, at 3 m on four load-bearing walls (their axes its edges) of a material that is not a shell: line supports; 2 kN/m² in Q-A.</summary>
    static (Fixture F, string Slab) FloorOnWalls(string wallMaterial = "Brick", string? shells = "slabs", double? mesh = null)
    {
        var f = LoadCaseTests.F();
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var wt = WallType(f, wallMaterial, 0.25);
        (double, double)[] o = [(0, 0), (6, 0), (6, 4), (0, 4)];
        var walls = Enumerable.Range(0, 4).Select(i => f.Wall(o[i].Item1, o[i].Item2, o[(i + 1) % 4].Item1, o[(i + 1) % 4].Item2, wt)).ToList();
        foreach (var w in walls) f.Run("element.setParam", new JsonObject { ["id"] = w, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = first, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["typeId"] = SlabType(f) })!["id"]!;
        f.Run("analysis.defaultCases", []);
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = first, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 2 });
        var opts = new JsonObject();
        if (shells != null) opts["shells"] = shells;
        if (mesh != null) opts["meshSize"] = mesh;
        if (opts.Count > 0) f.Run("analysis.setOptions", opts);
        return (f, slab);
    }

    /// <summary>C25/30: Ecm = 31 GPa (EN 1992-1-1 Table 3.1), ν = 0.2; the floor 20 cm: D = E·t³ / 12(1 − ν²), kN·m.</summary>
    static readonly double D = 31e6 * 0.2 * 0.2 * 0.2 / (12 * (1 - 0.2 * 0.2));

    /// <summary>The floor's centre: w and the moments (the engine's sign: a moment positive when the top is in tension).</summary>
    static (double W, double Mx, double My) Centre(Fixture f, string slab)
    {
        var r = Results(f, new JsonObject { ["case"] = "Q-A", ["shellId"] = slab });
        var n = NodeAt(r, slab, new Vec3(3, 2, 2.9));
        Assert.Equal(3, (double)n["at"]![0]!, 6);
        Assert.Equal(2, (double)n["at"]![1]!, 6);
        var v = n["values"]!;
        return ((double)v["w"]!, (double)v["mx"]!, (double)v["my"]!);
    }

    [Fact]
    public void A_floor_on_four_line_supports_is_Navier_s_simply_supported_plate()
    {
        var (f, slab) = FloorOnWalls();
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run.ToJsonString());
        var q = run["cases"]!.AsArray().Single(c => (string?)c!["code"] == "Q-A")!;
        Assert.Equal(-48, (double)q["sumLoads"]![2]!, 9);
        Assert.True((bool)q["balanced"]!);
        Assert.Equal(96, (int)run["shells"]!["elements"]!);
        var (w, mx, my) = Centre(f, slab);
        var (nw, nmx, nmy) = Navier(6, 4, 2, D, 0.2, 3, 2);
        output.WriteLine($"w {w / -nw:0.0000}, mx {mx / -nmx:0.0000}, my {my / -nmy:0.0000}");
        // Downwards, sagging (the bottom in tension: negative in the engine's sign); a/t = 20, so Reissner–Mindlin's shear adds a little.
        Assert.InRange(-w / nw, 0.99, 1.04);
        Assert.InRange(-mx / nmx, 0.97, 1.03);
        Assert.InRange(-my / nmy, 0.97, 1.03);
    }

    [Fact]
    public void The_mesh_size_is_an_option_and_the_plate_converges_as_it_gets_finer()
    {
        var (nw, _, nmy) = Navier(6, 4, 2, D, 0.2, 3, 2);
        var (ws, ms) = (new List<double>(), new List<double>());
        foreach (var size in new[] { 1.0, 0.5, 0.25 })
        {
            var (f, slab) = FloorOnWalls(mesh: size);
            var run = AnalysisRunTests.Run(f);
            Assert.Equal(size, (double)run["shells"]!["meshSize"]!);
            Assert.Equal((int)Math.Round(6 / size) * (int)Math.Round(4 / size), (int)run["shells"]!["elements"]!);
            var (w, _, my) = Centre(f, slab);
            output.WriteLine($"{size} m: w {w / -nw:0.00000}, my {my / -nmy:0.00000}");
            ws.Add(w / -nw);
            ms.Add(my / -nmy);
        }
        // Each halving moves the values less than the one before (1 m: w 0.977, my 0.897; 0.5 m: 1.013, 0.985; 0.25 m:
        // 1.028, 1.012): they converge to the thick plate's (a/t = 20; its edges free to twist, so a little above Kirchhoff's).
        Assert.True(Math.Abs(ws[2] - ws[1]) < Math.Abs(ws[1] - ws[0]), string.Join(", ", ws));
        Assert.True(Math.Abs(ms[2] - ms[1]) < Math.Abs(ms[1] - ms[0]), string.Join(", ", ms));
        Assert.InRange(ms[2], 0.985, 1.015);
        Assert.InRange(ws[2], 1.0, 1.04);
    }

    [Fact]
    public void The_options_take_one_undo_step_and_refuse_what_is_not_an_option()
    {
        var (f, _) = FloorOnWalls(shells: null);
        Assert.Equal("both", (string?)f.Session.Query("analysis.options", [])!["shells"]);
        Assert.Equal(0.5, (double)f.Session.Query("analysis.options", [])!["meshSize"]!);
        f.Run("analysis.setOptions", new JsonObject { ["shells"] = "none", ["meshSize"] = 0.25 });
        Assert.Equal("none", (string?)f.Session.Query("analysis.options", [])!["shells"]);
        Assert.Equal(0, (int)AnalysisRunTests.Run(f)["dofs"]!);
        f.Session.Undo();
        Assert.Equal("both", (string?)f.Session.Query("analysis.options", [])!["shells"]);
        f.Run("analysis.setOptions", new JsonObject { ["meshSize"] = 0.25 });
        f.Run("analysis.setOptions", new JsonObject { ["meshSize"] = null });
        Assert.Equal(0.5, (double)f.Session.Query("analysis.options", [])!["meshSize"]!);
        Assert.Throws<CommandException>(() => f.Run("analysis.setOptions", new JsonObject { ["shells"] = "plates" }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setOptions", new JsonObject { ["meshSize"] = 0.001 }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setOptions", []));
    }

    [Fact]
    public void Masonry_walls_are_shells_under_a_shell_floor()
    {
        // §11.1 (20.11.2026): masonry walls are shells now (they were line supports, §10.12.3); the floor rests on them, and
        // with nothing modelled under their foot that is held as a line support.
        var (f, _) = FloorOnWalls(shells: "both");
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run.ToJsonString());
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("Masonry walls as shells ("));
        Assert.Equal(5, (int)run["shells"]!["surfaces"]!);
        var a = AnalysisRun.Of(f.Session.Reader);
        var p = a.Problem.Problem;
        var walls = p.Shells.Where(s => a.Problem.Surfaces[s.Group!].Kind == "wall").ToList();
        Assert.NotEmpty(walls);
        // Brick: clay units of group 1, fb 10, fm 5 (indicative): fk = 0.55 · 10^0.7 · 5^0.3, E = 1000 fk (kPa in the solver); ν = 0.25 (G = 0.4 E).
        var fk = 0.55 * Math.Pow(10, 0.7) * Math.Pow(5, 0.3);
        Assert.All(walls, s => { Assert.Equal(fk * 1e6, s.E, 3); Assert.Equal(0.25, s.Nu, 12); });
        // The walls' heads are not held any more: the floor's load goes down them to their feet.
        var z0 = walls.SelectMany(s => s.Nodes).Min(n => p.Nodes[n].Z);
        Assert.All(walls.SelectMany(s => s.Nodes).Where(n => p.Nodes[n].Z > z0 + 0.01), n => Assert.Null(p.Held[n]));
        Assert.Equal(48, a.Solution!.Cases.Single(c => c.Id == LoadCaseTests.IdOf(f, "Q-A")).SumReactions.Z, 6);
        Assert.All(a.Solution.Cases.Single(c => c.Id == LoadCaseTests.IdOf(f, "Q-A")).Reactions.Keys, n => Assert.True(p.Nodes[n].Z <= z0 + 0.01));
        // Their own weight is in the sums now (ρ = 1800 kg/m³, indicative), no longer straight to the supports.
        var g1 = run["cases"]!.AsArray().Single(c => (string?)c!["code"] == "G1")!;
        Assert.True((bool)g1["balanced"]!, g1.ToJsonString());
        Assert.DoesNotContain(run["notes"]!.AsArray(), n => ((string)n!).Contains("the load-bearing walls' own weight"));
    }

    [Fact]
    public void A_masonry_wall_bends_and_shears_in_its_plane_as_a_deep_cantilever()
    {
        // A brick wall 3 m long, 6 m high, 25 cm, on a strip footing (fixed); a force P along its head, in its plane:
        // δ = P H³ / 3 E I + P H / (κ G A), I = t L³ / 12, A = t L, κ = 5/6, G = 0.4 E (EN 1996-1-1 §3.7.3).
        var f = LoadCaseTests.F();
        var wt = WallType(f, "Brick", 0.25);
        var w = f.Wall(0, 0, 3, 0, wt, height: 6);
        f.Run("element.setParam", new JsonObject { ["id"] = w, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 3, ["y2"] = 0 });
        f.Run("analysis.defaultCases", []);
        f.Run("analysis.setOptions", new JsonObject { ["meshSize"] = 0.25 });
        var a = AnalysisRun.Of(f.Session.Reader);
        Assert.True(a.Ok, string.Join("\n", a.Notes));
        var p = a.Problem.Problem;
        Assert.NotEmpty(p.Shells);
        var e = p.Shells[0].E;
        var top = p.Shells.SelectMany(s => s.Nodes).Distinct().Where(n => p.Nodes[n].Z > p.Shells.SelectMany(s => s.Nodes).Max(x => p.Nodes[x].Z) - 0.01).OrderBy(n => p.Nodes[n].X).ToList();
        var bottom = p.Shells.SelectMany(s => s.Nodes).Min(n => p.Nodes[n].Z);
        var h = p.Nodes[top[0]].Z - bottom;
        Assert.Equal(13, top.Count);
        const double P = 100;
        var c = new CaseLoads("push");
        // The force along the head, consistently: half shares at its two ends.
        for (var i = 0; i < top.Count; i++) c.AddNodal(top[i], new Vec3(P / (top.Count - 1) * (i == 0 || i == top.Count - 1 ? 0.5 : 1), 0, 0));
        p.Cases.Clear();
        p.Cases.Add(c);
        var sol = FrameSolver.Solve(p);
        Assert.True(sol.Ok);
        var u = top.Average(n => sol.Cases[0].U[n * 6]);
        var (t, l) = (0.25, 3.0);
        var expected = P * h * h * h / (3 * e * t * l * l * l / 12) + P * h / (5.0 / 6 * 0.4 * e * t * l);
        output.WriteLine($"masonry wall: E {e:0} kPa, h {h:0.###}, δ {u:0.000000} m, theory {expected:0.000000} m, ratio {u / expected:0.0000}");
        Assert.InRange(u / expected, 0.97, 1.03);
    }

    static JsonObject Bar(JsonObject results, string sourceId) => results["frames"]!.AsArray().Select(x => x!.AsObject()).Single(x => (string?)x["sourceId"] == sourceId);

    [Fact]
    public void The_guides_frame_with_its_floor_a_shell_balances_and_its_beams_work_with_the_floor()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run.ToJsonString());
        foreach (var c in run["cases"]!.AsArray()) Assert.True((bool)c!["balanced"]!, c.ToJsonString());
        var q = run["cases"]!.AsArray().Single(c => (string?)c!["code"] == "Q-A")!;
        Assert.Equal(-120, (double)q["sumLoads"]![2]!, 9);
        Assert.Equal(120, (double)q["sumReactions"]![2]!, 6);

        // The beams cut at the floor's nodes (every 0.5 m), recomposed whole: eight members, eleven stations each.
        var a = AnalysisRun.Of(f.Session.Reader);
        var beam = a.Problem.Members.FindIndex(m => m.SourceId == t.Beams[0]);
        Assert.Equal(12, a.Problem.PieceMap[beam].Count);
        Assert.Equal(6, a.Problem.PieceMap[beam][^1].X1, 9);
        var shells = Results(f, new JsonObject { ["case"] = "Q-A" });
        Assert.Equal(8, shells["frames"]!.AsArray().Count);
        var b = Bar(shells, t.Beams[0]);
        Assert.Equal(Stations.Count, b["stations"]!.AsArray().Count);
        Assert.Equal(6, (double)b["length"]!, 9);
        // Its four columns take the load by symmetry, a quarter each.
        foreach (var r in shells["reactions"]!.AsArray()) Assert.Equal(30, (double)r!["f"]![2]!, 6);

        // Against the 45° rule (S1): the long beam's moment in the span smaller, the floor working with it as a T (the beam in
        // tension, the floor in compression over it).
        var mz = (double)b["extremes"]!["Mz"]!["max"]!;
        var n = (double)b["stations"]![5]!["N"]!;
        AnalysisRunTests.NoShells(f);
        var s1 = Bar(Results(f, new JsonObject { ["case"] = "Q-A" }), t.Beams[0]);
        var mz1 = (double)s1["extremes"]!["Mz"]!["max"]!;
        output.WriteLine($"Mz shells {mz:0.000}, 45° {mz1:0.000}, N {n:0.000}");
        Assert.True(mz < mz1, $"{mz} vs {mz1}");
        Assert.True(n > 1, $"N {n}");
        f.Session.Undo();

        // The floor's moments: sagging at its centre, near the plate's on rigid supports (the beams bend a little).
        var (nw, nmx, nmy) = Navier(6, 4, 5, D, 0.2, 3, 2);
        var centre = NodeAt(Results(f, new JsonObject { ["case"] = "Q-A", ["shellId"] = t.Slab }), t.Slab, new Vec3(3, 2, 2.9))["values"]!;
        output.WriteLine($"floor: w {(double)centre["w"]! / -nw:0.000}, mx {(double)centre["mx"]! / -nmx:0.000}, my {(double)centre["my"]! / -nmy:0.000}");
        Assert.InRange((double)centre["my"]! / -nmy, 0.8, 1.6);
        Assert.InRange((double)centre["mx"]! / -nmx, 0.5, 1.6);
        Assert.True((double)centre["w"]! < -nw);
        // The largest values of the floor in the results' maxima.
        Assert.NotNull(shells["maxima"]!["shells"]!["my"]!["min"]);
        Assert.Equal(t.Slab, (string?)shells["maxima"]!["shells"]!["w"]!["min"]!["sourceId"]);
    }

    [Fact]
    public void A_flat_slab_on_four_columns_shares_their_nodes_and_balances()
    {
        var f = LoadCaseTests.F();
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var col = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30x30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 }, ["material"] = Concrete })!["id"]!;
        (double X, double Y)[] at = [(0, 0), (6, 0), (6, 6), (0, 6)];
        foreach (var (x, y) in at)
        {
            f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y, ["typeId"] = col });
            f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y });
        }
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = first, ["points"] = AnalysisRunTests.Rect(-0.5, -0.5, 6.5, 6.5), ["typeId"] = SlabType(f, t: 0.25) })!["id"]!;
        f.Run("analysis.defaultCases", []);
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = first, ["points"] = AnalysisRunTests.Rect(-0.5, -0.5, 6.5, 6.5), ["value"] = 5 });
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run.ToJsonString());
        var q = run["cases"]!.AsArray().Single(c => (string?)c!["code"] == "Q-A")!;
        Assert.Equal(-5 * 49, (double)q["sumLoads"]![2]!, 6);
        Assert.True((bool)q["balanced"]!);
        var res = Results(f, new JsonObject { ["case"] = "Q-A" });
        Assert.Equal(4, res["reactions"]!.AsArray().Count);
        foreach (var r in res["reactions"]!.AsArray()) Assert.Equal(5 * 49 / 4.0, (double)r!["f"]![2]!, 4);
        // The columns' heads are the floor's nodes: the floor deflects most in its middle, hogging over the columns.
        var s = Results(f, new JsonObject { ["case"] = "Q-A", ["shellId"] = slab });
        var mid = NodeAt(s, slab, new Vec3(3, 3, 0))["values"]!;
        var over = NodeAt(s, slab, new Vec3(0, 0, 0))["values"]!;
        Assert.Equal((double)s["shells"]![0]!["extremes"]!["w"]!["min"]!, (double)mid["w"]!, 9);
        output.WriteLine($"over {over.ToJsonString()} mid {mid.ToJsonString()}");
        Assert.True((double)over["mx"]! + (double)over["my"]! > 0, over.ToJsonString());
        Assert.True((double)mid["mx"]! < 0, mid.ToJsonString());
    }

    [Fact]
    public void A_timber_floor_keeps_the_45_degree_rule()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        var timber = SlabType(f, "Timber C24", 0.2);
        f.Run("architecture.setSlabType", new JsonObject { ["ids"] = new JsonArray(t.Slab), ["typeId"] = timber });
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run.ToJsonString());
        Assert.Null(run["shells"]);
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("Slabs not of concrete ("));
        var a = AnalysisRun.Of(f.Session.Reader);
        var mc = a.Problem.Cases.Single(c => c.Loads.Id == LoadCaseTests.IdOf(f, "Q-A"));
        Assert.Empty(mc.Loads.Surface);
        // The two long beams a trapezoid of 8 m² each, the short ones a triangle of 4 m² (× 5 kN/m²).
        var byBar = mc.Loads.Span.GroupBy(s => a.Problem.Frames[s.Element].Source.SourceId).ToDictionary(g => g.Key, g => g.Sum(s => -s.Load.Total.Z));
        Assert.Equal(40, byBar[t.Beams[0]], 6);
        Assert.Equal(20, byBar[t.Beams[1]], 6);
    }

    [Fact]
    public void Concrete_walls_are_shells_standing_on_their_footings_and_carry_a_floor_and_a_push()
    {
        var (f, slab) = FloorOnWalls(Concrete, shells: null);
        var walls = f.Model.OfClass(IfcClass.Wall).Select(w => w.Id).ToList();
        (double, double)[] o = [(0, 0), (6, 0), (6, 4), (0, 4)];
        for (var i = 0; i < 4; i++)
            f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = o[i].Item1, ["y1"] = o[i].Item2, ["x2"] = o[(i + 1) % 4].Item1, ["y2"] = o[(i + 1) % 4].Item2 });
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(6, 4), ["z"] = 2.9, ["value"] = 10, ["direction"] = "+x", ["caseId"] = LoadCaseTests.IdOf(f, "W") });
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run.ToJsonString());
        Assert.Equal(5, (int)run["shells"]!["surfaces"]!);
        var a = AnalysisRun.Of(f.Session.Reader);
        var p = a.Problem.Problem;
        // The walls' tops are not held (they are the floor's edges); their feet are, all along them, fixed by the footings.
        var wallNodes = p.Shells.Where(s => a.Problem.Surfaces[s.Group!].Kind == "wall").SelectMany(s => s.Nodes).Distinct().ToList();
        var z0 = wallNodes.Min(n => p.Nodes[n].Z);
        Assert.All(wallNodes.Where(n => p.Nodes[n].Z > z0 + 0.01), n => Assert.Null(p.Held[n]));
        var feet = wallNodes.Where(n => p.Nodes[n].Z <= z0 + 0.01).ToList();
        Assert.Equal(2 * 13 + 2 * 9 - 4, feet.Count);
        Assert.All(feet, n => Assert.Equal([true, true, true, true, true, true], p.Held[n]!));
        foreach (var c in run["cases"]!.AsArray()) Assert.True((bool)c!["balanced"]!, c.ToJsonString());
        var w = a.Solution!.Cases.Single(c => c.Id == LoadCaseTests.IdOf(f, "W"));
        Assert.Equal(-10, w.SumReactions.X, 6);
        // The push goes down the two walls along it (in their plane): nine tenths between their ends, the rest at the corners.
        var along = w.Reactions.Where(r => p.Nodes[r.Key].Y is < 0.01 or > 3.99 && p.Nodes[r.Key].X is > 0.01 and < 5.99).Sum(r => r.Value[0]);
        Assert.True(along < -8.5, $"{along}");
        // The floor rests on the walls: its load down them to the footings.
        Assert.Equal(48, a.Solution.Cases.Single(c => c.Id == LoadCaseTests.IdOf(f, "Q-A")).SumReactions.Z, 6);
        Assert.Contains(walls, x => a.Problem.Surfaces.Values.Any(s => s.SourceId == x && s.Kind == "wall"));
    }

    [Fact]
    public void Contours_give_each_surfaces_mesh_and_the_quantity_at_its_nodes()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        var c = Contours(f, new JsonObject { ["case"] = "Q-A", ["quantity"] = "my" });
        Assert.Equal("kN·m/m", (string?)c["unit"]);
        var s = Assert.Single(c["surfaces"]!.AsArray())!.AsObject();
        Assert.Equal(t.Slab, (string?)s["sourceId"]);
        Assert.Equal("slab", (string?)s["kind"]);
        var positions = s["positions"]!.AsArray();
        var values = s["values"]!.AsArray();
        Assert.Equal(13 * 9 * 3, positions.Count);
        Assert.Equal(13 * 9, values.Count);
        Assert.Equal(96 * 4, s["faces"]!.AsArray().Count);
        Assert.All(s["faces"]!.AsArray(), x => Assert.InRange((int)x!, 0, 13 * 9 - 1));
        Assert.Equal(values.Min(v => (double)v!), (double)c["min"]!, 6);
        Assert.True((double)c["min"]! < 0);
        // The same values as analysis.results' nodes.
        var r = Results(f, new JsonObject { ["case"] = "Q-A", ["shellId"] = t.Slab });
        Assert.Equal((double)r["shells"]![0]!["extremes"]!["my"]!["min"]!, (double)c["min"]!, 4);
        // An envelope: each node's least and largest.
        var env = Contours(f, new JsonObject { ["envelope"] = "uls", ["quantity"] = "w" });
        var e = env["surfaces"]![0]!;
        Assert.Equal("m", (string?)env["unit"]);
        Assert.Equal(13 * 9, e["min"]!.AsArray().Count);
        Assert.All(Enumerable.Range(0, 13 * 9), i => Assert.True((double)e["min"]![i]! <= (double)e["max"]![i]!));
        Assert.Throws<CommandException>(() => Contours(f, new JsonObject { ["case"] = "Q-A", ["quantity"] = "Mz" }));
        // No shells: no surfaces.
        AnalysisRunTests.NoShells(f);
        Assert.Empty(Contours(f, new JsonObject { ["case"] = "Q-A", ["quantity"] = "w" })["surfaces"]!.AsArray());
    }

    /// <summary>
    /// On request only (<c>LAPIN_SHELL_TIMING=storeys×bays</c>, e.g. 10x5): a concrete frame of that many storeys and bays of
    /// 6 m each way, its floors 20 cm shells, 3 kN/m² on each; prints the degrees of freedom, the times and the memory.
    /// </summary>
    [Fact]
    public void A_large_frame_with_shell_floors_is_timed_when_asked()
    {
        if (Environment.GetEnvironmentVariable("LAPIN_SHELL_TIMING") is not { Length: > 0 } spec) return;
        var parts = spec.Split('x');
        var (storeys, bays) = (int.Parse(parts[0]), int.Parse(parts[1]));
        var f = LoadCaseTests.F();
        var col = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C40x40", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.4, ["depth"] = 0.4 }, ["material"] = Concrete })!["id"]!;
        var beam = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "B30x60", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.6 }, ["material"] = Concrete })!["id"]!;
        var slabType = SlabType(f);
        var levels = new List<string> { f.Storey };
        for (var k = 1; k <= storeys; k++) levels.Add((string)f.Run("storey.create", new JsonObject { ["name"] = $"Level {k}", ["elevation"] = 3.0 * k, ["height"] = 3.0 })!["id"]!);
        var w = 6.0 * bays;
        var steps = new List<JsonObject>();
        for (var k = 0; k < storeys; k++)
        {
            for (var i = 0; i <= bays; i++)
                for (var j = 0; j <= bays; j++)
                    steps.Add(new JsonObject { ["command"] = "structure.addColumn", ["args"] = new JsonObject { ["storeyId"] = levels[k], ["x"] = 6.0 * i, ["y"] = 6.0 * j, ["typeId"] = col } });
            for (var i = 0; i <= bays; i++)
                for (var j = 0; j < bays; j++)
                {
                    steps.Add(new JsonObject { ["command"] = "structure.addBeam", ["args"] = new JsonObject { ["storeyId"] = levels[k], ["x1"] = 6.0 * i, ["y1"] = 6.0 * j, ["x2"] = 6.0 * i, ["y2"] = 6.0 * (j + 1), ["typeId"] = beam } });
                    steps.Add(new JsonObject { ["command"] = "structure.addBeam", ["args"] = new JsonObject { ["storeyId"] = levels[k], ["x1"] = 6.0 * j, ["y1"] = 6.0 * i, ["x2"] = 6.0 * (j + 1), ["y2"] = 6.0 * i, ["typeId"] = beam } });
                }
            steps.Add(new JsonObject { ["command"] = "architecture.addSlab", ["args"] = new JsonObject { ["storeyId"] = levels[k + 1], ["points"] = AnalysisRunTests.Rect(0, 0, w, w), ["typeId"] = slabType } });
            steps.Add(new JsonObject { ["command"] = "analysis.addAreaLoad", ["args"] = new JsonObject { ["storeyId"] = levels[k + 1], ["points"] = AnalysisRunTests.Rect(0, 0, w, w), ["value"] = 3 } });
        }
        for (var i = 0; i <= bays; i++)
            for (var j = 0; j <= bays; j++)
                steps.Add(new JsonObject { ["command"] = "structure.addPadFooting", ["args"] = new JsonObject { ["storeyId"] = f.Storey, ["x"] = 6.0 * i, ["y"] = 6.0 * j } });
        f.Run("analysis.defaultCases", []);
        foreach (var s in steps) f.Run((string)s["command"]!, s["args"]!.AsObject());
        GC.Collect();
        var before = GC.GetTotalMemory(true);
        var allocated = GC.GetTotalAllocatedBytes();
        System.Diagnostics.Process.GetCurrentProcess().Refresh();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var run = AnalysisRunTests.Run(f);
        var total = sw.Elapsed.TotalSeconds;
        var proc = System.Diagnostics.Process.GetCurrentProcess();
        proc.Refresh();
        Assert.True((bool)run["ok"]!, run["notes"]!.ToJsonString());
        foreach (var c in run["cases"]!.AsArray()) Assert.True((bool)c!["balanced"]!, c.ToJsonString());
        sw.Restart();
        var contours = Contours(f, new JsonObject { ["envelope"] = "uls", ["quantity"] = "my" });
        var contourS = sw.Elapsed.TotalSeconds;
        var size = contours.ToJsonString().Length;
        output.WriteLine($"{storeys} storeys × {bays}×{bays} bays: {run["dofs"]} dofs, {run["elements"]} bars, {run["shells"]!["elements"]} shells; " +
            $"analysis.run {total:0.00} s (solve {(double)run["solveMs"]! / 1000:0.00} s), contours {contourS:0.00} s, {size / 1e3:0} kB of JSON; " +
            $"working set {Environment.WorkingSet / 1e6:0} MB after, {(GC.GetTotalAllocatedBytes() - allocated) / 1e6:0} MB allocated, {(GC.GetTotalMemory(false) - before) / 1e6:0} MB held");
    }
}
