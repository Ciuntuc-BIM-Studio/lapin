using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Analysis.Design;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;

namespace Lapin.Engine.Tests;

/// <summary>
/// S4 on models (docs/plan-fem.md §12): analysis.checks, analysis.check, analysis.walls, analysis.reinforcement, the
/// reinforcement's contours and analysis.setDesign on the concrete frame of the S1 tests, an IPE 300 cantilever, a timber
/// beam, and brick walls, whose forces at the foot come from the elements' nodal forces in exact balance.
/// </summary>
public class DesignRunTests(Xunit.Abstractions.ITestOutputHelper output)
{
    static JsonObject Q(Fixture f, string q, JsonObject? a = null) => f.Session.Query($"analysis.{q}", a ?? [])!.AsObject();

    [Fact]
    public void The_concrete_frame_is_checked_bar_by_bar_and_its_slab_gets_its_reinforcement()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        var checks = Q(f, "checks");
        Assert.True((bool)checks["ok"]!, checks.ToJsonString());
        Assert.True((bool)checks["indicative"]!);
        var members = checks["members"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.Equal(8, members.Count);
        Assert.All(members, x => Assert.Equal("concrete", (string?)x["material"]));
        Assert.All(members, x => Assert.True((double)x["utilisation"]! is > 0 and < 1, x.ToJsonString()));
        Assert.All(members, x => Assert.NotNull((string?)x["governing"]!["key"]));
        // A beam: bending, weak axis, shear and deflection; its reinforcement asked by its element.
        var beam = Q(f, "check", new JsonObject { ["id"] = t.Beams[0] });
        var bm = beam["members"]!.AsArray()[0]!.AsObject();
        var keys = bm["checks"]!.AsArray().Select(c => (string?)c!["key"]).ToList();
        Assert.Contains("concrete.bending", keys);
        Assert.Contains("concrete.shear", keys);
        Assert.Contains("concrete.deflection", keys);
        Assert.True((double)bm["reinforcement"]!["bottom"]! > 0);
        // A column: N with biaxial bending (5.8.8), fixed on its footing and joined at its head: l0 = 0.85 L.
        var col = Q(f, "check", new JsonObject { ["id"] = t.Columns[0] })["members"]!.AsArray()[0]!.AsObject();
        var cc = col["checks"]!.AsArray().Select(c => c!.AsObject()).Single(c => (string?)c["key"] == "concrete.column");
        Assert.Contains("fixed at one end: k = 0.85", (string?)cc["note"]);
        // The slab (a shell): Wood–Armer's bars at every node, the bottom's in the field.
        var slab = checks["surfaces"]!.AsArray().Select(x => x!.AsObject()).Single(x => (string?)x["sourceId"] == t.Slab);
        Assert.True((double)slab["reinforcement"]!["asxBottom"]! > 0);
        // Its shear off the beams: 1.35·(5 kN/m² of slab + ...) + 1.5·5 on a 4 m span gives some 30 kN/m against VRd,c ≈ vmin·d = 0.495 MPa·154 mm = 76 kN/m.
        var sd = Q(f, "check", new JsonObject { ["id"] = t.Slab })["surfaces"]!.AsArray()[0]!["checks"]!.AsArray().Single(c => (string?)c!["key"] == "concrete.slabShear")!;
        output.WriteLine($"slab shear utilisation {(double)sd["utilisation"]!:0.###}");
        Assert.InRange((double)sd["utilisation"]!, 0.1, 1.0);
        var rein = Q(f, "reinforcement", new JsonObject { ["id"] = t.Slab });
        Assert.NotEmpty(rein["surfaces"]!.AsArray()[0]!["nodes"]!.AsArray());
        var contours = Q(f, "contours", new JsonObject { ["envelope"] = "uls", ["quantity"] = "asxBottom" });
        Assert.Equal("cm²/m", (string?)contours["unit"]);
        Assert.Equal((double)slab["reinforcement"]!["asxBottom"]!, (double)contours["max"]!, 3);
        // Kept for the model's revision.
        var run = DesignRun.Of(f.Session.Reader);
        Assert.Same(run, DesignRun.Of(f.Session.Reader));

        // The project's αcc (1.0 by default) reaches the bending: at 0.85 K' = 0.5667·0.8·0.45·0.82 = 0.16728 (1.0: 0.1968).
        JsonObject Bending() => Q(f, "check", new JsonObject { ["id"] = t.Beams[0] })["members"]!.AsArray()[0]!["checks"]!.AsArray().Select(c => c!.AsObject()).Single(c => (string?)c["key"] == "concrete.bending");
        double Kp() => (double)Bending()["values"]!.AsArray().Single(v => (string?)v!["name"] == "K'")!["value"]!;
        Assert.Equal(0.1968, Kp(), 4);
        f.Run("analysis.setDesign", new JsonObject { ["alphaCc"] = 0.85 });
        Assert.Equal(0.1673, Kp(), 4);
        // γc 1.5 → 1.0 takes the block to 0.85 fck: K' = 0.85·0.8·0.45·0.82 = 0.25092.
        f.Run("analysis.setDesign", new JsonObject { ["gammaC"] = 1.0 });
        Assert.Equal(0.2509, Kp(), 4);
    }

    [Fact]
    public void An_IPE_300_cantilever_column_is_checked_with_twice_its_length_and_setDesign_changes_it_in_one_step()
    {
        var f = LoadCaseTests.F();
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = "IPE 300", ["material"] = "S355" })!["id"]!;
        var col = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0, ["typeId"] = type })!["id"]!;
        f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 });
        f.Run("analysis.defaultCases", []);
        var a = AnalysisRun.Of(f.Session.Reader);
        var frame = Assert.Single(a.Problem.Model.Frames);
        var h = frame.J.At.Z - frame.I.At.Z;
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(0, 0), ["z"] = h, ["value"] = 50, ["caseId"] = LoadCaseTests.IdOf(f, "Q-A") });
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(0, 0), ["z"] = h, ["value"] = 5, ["direction"] = "+x", ["caseId"] = LoadCaseTests.IdOf(f, "W") });
        var d = Q(f, "check", new JsonObject { ["id"] = col })["members"]!.AsArray()[0]!.AsObject();
        Assert.Equal("steel", (string?)d["material"]);
        var cs = d["checks"]!.AsArray().Select(c => c!.AsObject()).ToList();
        var b = cs.Single(c => (string?)c["key"] == "steel.buckling");
        double Val(JsonObject c, string n) => (double)c["values"]!.AsArray().Single(v => (string?)v!["name"] == n)!["value"]!;
        // A cantilever (fixed foot, free head): Lcr = 2 h about both axes.
        Assert.Equal(2 * h, Val(b, "Lcr,y"), 3);
        Assert.Equal(2 * h, Val(b, "Lcr,z"), 3);
        // The section's class, shear, resistance, buckling, lateral torsional buckling (the column's own length) and the interaction.
        Assert.Contains(cs, c => (string?)c["key"] == "steel.section");
        Assert.Contains(cs, c => (string?)c["key"] == "steel.interaction");
        // 1.35 G1 + 1.5 Q-A: N = 1.35·weight + 75 kN; the buckling utilisation that over Nb,Rd.
        var props = SectionProperties.Of(frame.Source.Section.Shape)!;
        Assert.True(Val(b, "NEd") > 75);
        // The user's buckling length factor about the weak axis: one undo step.
        f.Run("analysis.setDesign", new JsonObject { ["ids"] = new JsonArray(col), ["kz"] = 0.7 });
        var b2 = Q(f, "check", new JsonObject { ["id"] = col })["members"]!.AsArray()[0]!["checks"]!.AsArray().Select(c => c!.AsObject()).Single(c => (string?)c["key"] == "steel.buckling");
        Assert.Equal(0.7 * h, Val(b2, "Lcr,z"), 3);
        f.Session.Undo();
        Assert.Null(f.Model.Get(col)!.Attrs[DesignSettings.Key]);
        f.Session.Redo();
        Assert.Throws<CommandException>(() => f.Run("analysis.setDesign", new JsonObject { ["ids"] = new JsonArray(col), ["kz"] = 50 }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setDesign", new JsonObject { ["ids"] = new JsonArray(col), ["cover"] = 0.03 }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setDesign", new JsonObject { ["serviceClass"] = 1.5 }));
        // Annex A by default (the Romanian annex's), Annex B the project's choice; the column looks unbraced (no wall, no bracing).
        Assert.Contains("Annex A", (string?)cs.Single(c => (string?)c["key"] == "steel.interaction")["clause"]);
        Assert.Contains(Q(f, "checks")["notes"]!.AsArray(), n => ((string)n!).StartsWith("No wall or bracing along X and Y"));
        f.Run("analysis.setDesign", new JsonObject { ["steelInteraction"] = "B" });
        var ib = Q(f, "check", new JsonObject { ["id"] = col })["members"]!.AsArray()[0]!["checks"]!.AsArray().Select(c => c!.AsObject()).Single(c => (string?)c["key"] == "steel.interaction");
        Assert.Contains("Annex B", (string?)ib["clause"]);
        Assert.Throws<CommandException>(() => f.Run("analysis.setDesign", new JsonObject { ["steelInteraction"] = "C" }));
        f.Run("analysis.setDesign", new JsonObject { ["serviceClass"] = 2 });
        Assert.Equal(2, (double)Q(f, "designOptions")["project"]!["serviceClass"]!);
        f.Run("analysis.setDesign", new JsonObject { ["ids"] = new JsonArray(col), ["kz"] = null });
        var b3 = Q(f, "check", new JsonObject { ["id"] = col })["members"]!.AsArray()[0]!["checks"]!.AsArray().Select(c => c!.AsObject()).Single(c => (string?)c["key"] == "steel.buckling");
        Assert.Equal(2 * h, Val(b3, "Lcr,z"), 3);
        _ = props;
        // The project's γM1 (EN 1993-1-1 6.1(1), 1.0 by default): Nb,Rd = χ A fy/γM1, so 1.1 takes the buckling's utilisation × 1.1.
        var u1 = (double)b3["utilisation"]!;
        f.Run("analysis.setDesign", new JsonObject { ["gammaM1"] = 1.1 });
        var b4 = Q(f, "check", new JsonObject { ["id"] = col })["members"]!.AsArray()[0]!["checks"]!.AsArray().Select(c => c!.AsObject()).Single(c => (string?)c["key"] == "steel.buckling");
        Assert.Equal(u1 * 1.1, (double)b4["utilisation"]!, 3);
        var opts = Q(f, "designOptions");
        Assert.True((bool)opts["indicative"]!);
        Assert.Equal(1.25, (double)opts["defaults"]!["gammaM2"]!, 9);
        Assert.Throws<CommandException>(() => f.Run("analysis.setDesign", new JsonObject { ["gammaM0"] = 2 }));
    }

    [Fact]
    public void Timber_beams_take_kmod_from_their_combinations_and_have_a_final_deflection()
    {
        var t = AnalysisRunTests.Frame6x4(beamMaterial: "Timber C24");
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 2 });
        var checks = Q(f, "checks", new JsonObject { ["material"] = "timber" });
        var beams = checks["members"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.Equal(4, beams.Count);
        var d = Q(f, "check", new JsonObject { ["id"] = t.Beams[0] })["members"]!.AsArray()[0]!.AsObject();
        var cs = d["checks"]!.AsArray().Select(c => c!.AsObject()).ToList();
        var sec = cs.Single(c => (string?)c["key"] == "timber.section");
        // kmod is the governing combination's shortest action's: Q-A (medium, 0.80) or G alone (permanent, 0.60).
        var kmod = (double)sec["values"]!.AsArray().Single(v => (string?)v!["name"] == "kmod")!["value"]!;
        Assert.Contains(kmod, new[] { 0.6, 0.8 });
        var defl = cs.Single(c => (string?)c["key"] == "timber.deflection");
        Assert.True((bool)defl["done"]!);
        Assert.StartsWith("slsc/", (string?)defl["combination"]);
        // Snow is a short-term action by default, medium-term as the project says (EN 1995-1-1 Table 2.2).
        var snowCase = new LoadCase("s", "S", "Snow", LoadCases.Variable, "SNOW_S", false, [LoadCases.Snow], Categories.Snow, null);
        Assert.Equal("short", AnalysisRun.DurationOf(snowCase));
        Assert.Equal("medium", AnalysisRun.DurationOf(snowCase, "medium"));
        f.Run("analysis.setDesign", new JsonObject { ["snowDuration"] = "medium" });
        Assert.Equal("medium", DesignRun.Of(f.Session.Reader).Run.SnowDuration);
        Assert.Contains("medium-term", (string?)Q(f, "check", new JsonObject { ["id"] = t.Beams[0] })["members"]!.AsArray()[0]!["checks"]!.AsArray().Single(c => (string?)c!["key"] == "timber.section")!["note"]);
        // The slab on the beams holds them sideways: no lateral torsional buckling.
        Assert.DoesNotContain(cs, c => (string?)c["key"] == "timber.ltb");

        // The deflection limits: L/300 and L/250 by default; the project's L/500 final halves the final limit, an element's own over it.
        double Lim(JsonObject c, string n) => (double)c["values"]!.AsArray().Single(v => (string?)v!["name"] == n)!["value"]!;
        JsonObject Defl() => Q(f, "check", new JsonObject { ["id"] = t.Beams[0] })["members"]!.AsArray()[0]!["checks"]!.AsArray().Select(c => c!.AsObject()).Single(c => (string?)c["key"] == "timber.deflection");
        var fin = Lim(Defl(), "limit fin");
        f.Run("analysis.setDesign", new JsonObject { ["timberFin"] = 500 });
        Assert.Equal(fin / 2, Lim(Defl(), "limit fin"), 6);
        f.Run("analysis.setDesign", new JsonObject { ["ids"] = new JsonArray(t.Beams[0]), ["timberFin"] = 125 });
        Assert.Equal(fin * 2, Lim(Defl(), "limit fin"), 6);
        Assert.Contains("L/125", (string?)Defl()["note"]);
    }

    [Fact]
    public void A_brick_walls_forces_at_its_foot_balance_what_is_on_it_exactly()
    {
        // A brick wall 3 m long, 6 m high, 25 cm, on a strip footing; its own weight (G1) and a force P along its head (W):
        // at the foot N = ρ g t L H, V = P, M = P H; at the head nothing; at mid-height the weight of what is above.
        var f = LoadCaseTests.F();
        var wt = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Brick 25", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.25, ["core"] = true }),
        })!["id"]!;
        var w = f.Wall(0, 0, 3, 0, wt, height: 6);
        f.Run("element.setParam", new JsonObject { ["id"] = w, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 3, ["y2"] = 0 });
        f.Run("analysis.defaultCases", []);
        f.Run("analysis.setOptions", new JsonObject { ["meshSize"] = 0.5 });
        var a = AnalysisRun.Of(f.Session.Reader);
        Assert.True(a.Ok, string.Join("\n", a.Notes));
        var p = a.Problem.Problem;
        var shells = Enumerable.Range(0, p.Shells.Count).ToList();
        var top = shells.SelectMany(s => p.Shells[s].Nodes).Max(n => p.Nodes[n].Z);
        var bottom = shells.SelectMany(s => p.Shells[s].Nodes).Min(n => p.Nodes[n].Z);
        var height = top - bottom;
        // The self weight first (the problem's cases are replaced below).
        var cuts = WallForces.Cuts(p, shells, p.Shells[0].Ex);
        Assert.Equal(["foot", "middle", "head"], cuts.Select(c => c.Where));
        Assert.Equal(3, cuts[0].Length, 6);
        WallSection At(string code, WallCut cut)
        {
            var ci = a.Solution!.Cases.FindIndex(c => c.Id == LoadCaseTests.IdOf(f, code));
            var loads = p.Cases[ci].Surface.GroupBy(x => x.Shell).ToDictionary(x => x.Key, x => x.Aggregate(Vec3.Zero, (s, y) => s + y.Q));
            return WallForces.Of(p, cut, a.Solution.Cases[ci].U, s => loads.TryGetValue(s, out var q) ? q : default, s => p.Shells[s].GlobalStiffness());
        }
        // The self weight: 1800 kg/m³ (the brick's indicative density) · g · 0.25 · 3 · H at the foot, nothing at the head.
        var weight = 1800 * 9.80665 * 0.25 * 3 * height / 1000;
        Assert.Equal(weight, At("G1", cuts[0]).N, 4);
        Assert.Equal(0, At("G1", cuts[2]).N, 6);
        Assert.Equal(weight * (top - cuts[1].Z) / height, At("G1", cuts[1]).N, 4);
        Assert.Equal(0, At("G1", cuts[0]).M, 5);
        // A force P along the head (consistent shares on its nodes): V = P, M = P·H at the foot, no axial force.
        const double P = 10;
        var head = shells.SelectMany(s => p.Shells[s].Nodes).Distinct().Where(n => p.Nodes[n].Z > top - 0.01).OrderBy(n => p.Nodes[n].X).ToList();
        var push = new CaseLoads("push");
        for (var i = 0; i < head.Count; i++) push.AddNodal(head[i], new Vec3(P / (head.Count - 1) * (i == 0 || i == head.Count - 1 ? 0.5 : 1), 0, 0));
        var walls = Q(f, "walls");
        p.Cases.Clear();
        p.Cases.Add(push);
        var sol = FrameSolver.Solve(p);
        Assert.True(sol.Ok);
        var wf = WallForces.Of(p, cuts[0], sol.Cases[0].U, null, s => p.Shells[s].GlobalStiffness());
        Assert.Equal(P, Math.Abs(wf.V), 6);
        Assert.Equal(P * height, Math.Abs(wf.M), 5);
        Assert.Equal(0, wf.N, 6);
        // The table: one storey, one wall along X, with its resistances.
        var row = walls["storeys"]!.AsArray()[0]!["walls"]!.AsArray()[0]!.AsObject();
        Assert.Equal("X", (string?)row["direction"]);
        Assert.Equal(3, (double)row["length"]!, 6);
        Assert.True((double)row["NRd"]! > 0);
        Assert.True((double)row["VRd"]! > 0);
        Assert.Contains("masonry.shear", row["utilisations"]!.AsObject().Select(x => x.Key));
        Assert.Equal(1, (int)walls["storeys"]!.AsArray()[0]!["sums"]!["X"]!["walls"]!);
    }

    [Fact]
    public void Masonry_walls_under_a_floor_are_tabled_storey_by_storey()
    {
        var (f, _) = ShellAnalysisTests.FloorOnWalls(shells: "both");
        var walls = Q(f, "walls");
        Assert.True((bool)walls["ok"]!, walls.ToJsonString());
        var rows = walls["storeys"]!.AsArray().SelectMany(s => s!["walls"]!.AsArray()).Select(x => x!.AsObject()).ToList();
        Assert.Equal(4, rows.Count);
        Assert.Equal(2, rows.Count(r => (string?)r["direction"] == "X"));
        Assert.All(rows, r => Assert.True((double)r["N"]!["value"]! > 0, r.ToJsonString()));
        var checks = Q(f, "checks", new JsonObject { ["material"] = "masonry" });
        Assert.Equal(4, checks["walls"]!.AsArray().Count);
        Assert.All(checks["walls"]!.AsArray(), w => Assert.True((double)w!["utilisation"]! is > 0 and < 1, w.ToJsonString()));
    }

    [Fact]
    public void The_guides_house_is_checked_quickly()
    {
        // As AnalysisRunTests builds it (the guide's house: 12 × 8 m, an inner wall, openings, a floor, a gable roof, its walls load-bearing).
        var f = LoadCaseTests.F();
        var s = f.Storey;
        (double, double)[] o = [(0, 0), (12, 0), (12, 8), (0, 8)];
        var walls = Enumerable.Range(0, 4).Select(i => f.Wall(o[i].Item1, o[i].Item2, o[(i + 1) % 4].Item1, o[(i + 1) % 4].Item2)).ToList();
        walls.Add(f.Wall(7, 0, 7, 8));
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["x"] = 3, ["y"] = 0 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = walls[0], ["x"] = 9.5, ["y"] = 0 });
        var pts = new JsonArray(o.Select(p => (JsonNode)new JsonArray(p.Item1, p.Item2)).ToArray());
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = s, ["points"] = pts.DeepClone() });
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = s, ["points"] = pts.DeepClone() })!["id"]!;
        f.Run("architecture.roofStyle", new JsonObject { ["id"] = roof, ["style"] = "ro_saxon" });
        foreach (var w in walls) f.Run("element.setParam", new JsonObject { ["id"] = w, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        f.Run("analysis.defaultCases", []);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var a = AnalysisRun.Of(f.Session.Reader);
        var analysis = sw.Elapsed.TotalMilliseconds;
        Assert.True(a.Ok, string.Join("\n", a.Notes));
        var checks = Q(f, "checks");
        var walls2 = Q(f, "walls");
        var total = sw.Elapsed.TotalMilliseconds;
        output.WriteLine($"guide house: analysis {analysis:0} ms, checks {(double)checks["ms"]!:0} ms, total {total:0} ms; {checks["members"]!.AsArray().Count} bars, {checks["surfaces"]!.AsArray().Count} surfaces, {walls2["storeys"]!.AsArray().Sum(x => x!["walls"]!.AsArray().Count)} walls");
        Assert.True((bool)checks["ok"]!, checks.ToJsonString());
        Assert.True((double)checks["ms"]! < 20000);
    }
}
