using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Snow and wind from the site's climate (docs/plan-fem.md §9.9): the site's values the user's and checked, the cases they go in
/// made in one undo step; the snow on a duo-pitch roof by its pitch (μ1), drifted with half on the plane facing the wind, on a
/// floor nothing covers 0.8; the wind's exposure factor, the walls' coefficients by h/d, the zones of the façades and of a flat
/// roof, a duo-pitch roof's by Table 7.4a; the façades carried by the beams along them and the ground, everything placed and
/// balanced. The numbers are worked out by hand from EN 1991-1-3 and EN 1991-1-4.
/// </summary>
public class ClimateLoadTests
{
    static JsonObject Climate(Fixture f) => f.Session.Query("analysis.climate", [])!.AsObject();
    static LoadsReport Report(Fixture f) => AnalysisRun.Of(f.Session.Reader).Report;
    static List<DerivedLoad> Of(Fixture f, string code) { var r = Report(f); var id = r.Cases.Single(c => c.Code == code).Id; return r.Derived.Where(d => d.CaseId == id).ToList(); }
    static double PlanArea(IReadOnlyList<Vec2> p) => Math.Abs(Polygon2.SignedArea(p));
    static double TrueArea(DerivedLoad d) => VecMath.NewellNormal(d.Points3!.ToList()).Length;
    static Vec3 Force(DerivedLoad d) => (d.Direction ?? new Vec3(0, 0, -1)) * (d.Value * (d.Projected == true ? PlanArea(d.Points) : TrueArea(d)));
    static Vec3 Sum(IEnumerable<Vec3> vs) => vs.Aggregate(Vec3.Zero, (a, b) => a + b);
    static Vec3 V(JsonNode? n) => new((double)n![0]!, (double)n[1]!, (double)n[2]!);

    static Fixture House(string style)
    {
        var f = LoadCaseTests.F();
        (double X, double Y)[] o = [(0, 0), (12, 0), (12, 8), (0, 8)];
        var walls = Enumerable.Range(0, o.Length).Select(i => f.Wall(o[i].X, o[i].Y, o[(i + 1) % o.Length].X, o[(i + 1) % o.Length].Y)).ToList();
        foreach (var w in walls) f.Run("element.setParam", new JsonObject { ["id"] = w, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(o.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray()) })!["id"]!;
        f.Run("architecture.roofStyle", new JsonObject { ["id"] = roof, ["style"] = style });
        f.Run("analysis.defaultCases", []);
        return f;
    }

    [Fact]
    public void The_coefficients_are_the_norms()
    {
        // EN 1991-1-3 Table 5.2.
        Assert.Equal(0.8, ClimateLoads.Mu1(0), 12);
        Assert.Equal(0.8, ClimateLoads.Mu1(30), 12);
        Assert.Equal(0.4, ClimateLoads.Mu1(45), 12);
        Assert.Equal(0, ClimateLoads.Mu1(60), 12);
        // EN 1991-1-4 4.5: terrain II at 10 m, ce = (1 + 7/ln 200) · (0.19 ln 200)² = 2.3523; terrain III under zmin = 5 m is at 5 m.
        var ln = Math.Log(200);
        Assert.Equal((1 + 7 / ln) * Math.Pow(0.19 * ln, 2), new WindSite(0.5, "II").Ce(10), 12);
        Assert.Equal(2.3523, new WindSite(0.5, "II").Ce(10), 4);
        Assert.Equal(new WindSite(0.5, "III").Ce(5), new WindSite(0.5, "III").Ce(2), 12);
        Assert.Equal(0.5 * new WindSite(0.5, "II").Ce(10), new WindSite(0.5, "II").Qp(10), 12);
        // Table 7.1, linear in h/d between 0.25, 1 and 5; the lack of correlation 7.2.2(3).
        Assert.Equal(0.7, ClimateLoads.WallCpe("D", 0.1), 12);
        Assert.Equal(0.75, ClimateLoads.WallCpe("D", 0.625), 12);
        Assert.Equal(-0.4, ClimateLoads.WallCpe("E", 0.625), 12);
        Assert.Equal(-0.6, ClimateLoads.WallCpe("E", 3), 12);
        Assert.Equal(-1.2, ClimateLoads.WallCpe("A", 3), 12);
        Assert.Equal(0.85, ClimateLoads.Correlation(0.5), 12);
        Assert.Equal(0.925, ClimateLoads.Correlation(3), 12);
        Assert.Equal(1, ClimateLoads.Correlation(7), 12);
        // s = γIs·μ·Ce·Ct·s0,k.
        Assert.Equal(1.1 * 0.8 * 1.2 * 1.0 * 2.5, new SnowSite(2.5, 1.2, 1.0, 1.1).S(0.8), 12);
    }

    [Fact]
    public void The_sites_snow_and_wind_are_the_users_checked_and_make_their_cases_in_one_undo_step()
    {
        var f = LoadCaseTests.F();
        f.Run("analysis.defaultCases", []);
        Assert.Contains("ground snow load", Assert.Throws<CommandException>(() => f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject() })).Message);
        Assert.Contains("0, I, II, III or IV", Assert.Throws<CommandException>(() => f.Run("analysis.setClimate", new JsonObject { ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "V" } })).Message);
        Assert.Contains("exposure", Assert.Throws<CommandException>(() => f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2, ["ce"] = 5 } })).Message);
        var before = LoadCaseTests.Cases(f)["cases"]!.AsArray().Count;
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0, ["ce"] = 0.8 }, ["wind"] = new JsonObject { ["qb"] = 0.4, ["terrain"] = "III" } });
        var c = Climate(f);
        Assert.Equal(2.0, (double)c["snow"]!["s0k"]!, 12);
        Assert.Equal(0.8, (double)c["snow"]!["ce"]!, 12);
        Assert.Equal(1.0, (double)c["snow"]!["importance"]!, 12);
        Assert.Equal("III", (string?)c["wind"]!["terrain"]);
        Assert.Equal(5, c["terrains"]!.AsArray().Count);
        var cases = LoadCaseTests.Cases(f)["cases"]!.AsArray().Select(x => x!.AsObject()).ToList();
        var codes = cases.Select(x => (string)x["code"]!).ToList();
        // The default S takes the undrifted snow; its drifts by the four winds; the default W becomes the first wind.
        Assert.Equal(["G1", "G2", "Q-A", "S", "W+X/s", "S+X", "S-X", "S+Y", "S-Y", "W+X/p", "W-X/s", "W-X/p", "W+Y/s", "W+Y/p", "W-Y/s", "W-Y/p"], codes);
        Assert.Equal(before + 11, cases.Count);
        var w = cases.Single(x => (string)x["code"]! == "W+X/s");
        Assert.Equal("+x", (string?)w["windDirection"]);
        Assert.Equal("min", (string?)w["roofCpe"]);
        Assert.Equal(["snow"], cases.Single(x => (string)x["code"]! == "S")["derives"]!.AsArray().Select(x => (string)x!));
        // A second wind case of the same direction and set is refused.
        Assert.Contains("already", Assert.Throws<CommandException>(() => f.Run("analysis.addCase", new JsonObject
        {
            ["name"] = "Mine", ["actionType"] = "VARIABLE_Q", ["category"] = "wind", ["derives"] = new JsonArray("wind"), ["windDirection"] = "+x", ["roofCpe"] = "min",
        })).Message);
        // One undo step: the climate and its cases go together.
        f.Session.Undo();
        Assert.Null(Climate(f)["snow"]);
        Assert.Equal(before, LoadCaseTests.Cases(f)["cases"]!.AsArray().Count);
        // Null takes one off; absent leaves it.
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 1.5 } });
        f.Run("analysis.setClimate", new JsonObject { ["wind"] = new JsonObject { ["qb"] = 0.5 } });
        Assert.Equal(1.5, (double)Climate(f)["snow"]!["s0k"]!, 12);
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = null });
        Assert.Null(Climate(f)["snow"]);
        Assert.Equal("II", (string?)Climate(f)["wind"]!["terrain"]);
    }

    [Fact]
    public void Snow_on_a_duo_pitch_roof_is_mu1_by_its_pitch_drifted_with_half_on_the_plane_facing_the_wind()
    {
        // The Saxon house: a gable roof at 45° over 12 × 8 m: μ1 = 0.8 (60 − 45)/30 = 0.4.
        var f = House("ro_saxon");
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0, ["ce"] = 1.2, ["importance"] = 1.15 } });
        var planes = AnalysisRun.Of(f.Session.Reader).Problem.Model.Roofs.Single().Planes;
        Assert.Equal(2, planes.Count);
        var plan = planes.Sum(p => PlanArea(p.Select(q => q.Xy).ToList()));
        var s = 1.15 * 0.4 * 1.2 * 1.0 * 2.0;
        var undrifted = Of(f, "S");
        Assert.Equal(2, undrifted.Count);
        Assert.All(undrifted, d => { Assert.Equal(s, d.Value, 9); Assert.True(d.Projected); });
        Assert.Equal(s * plan, undrifted.Sum(d => d.Total), 6);
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString());
        var byCode = run["cases"]!.AsArray().ToDictionary(c => (string)c!["code"]!, c => c!.AsObject());
        Assert.Equal(-s * plan, (double)byCode["S"]["sumLoads"]![2]!, 6);
        Assert.Equal(0, (double)byCode["S"]["unmapped"]!, 6);
        Assert.True((bool)byCode["S"]["balanced"]!);
        // Its ridge runs along the long side (x): the wind across it (±y) drifts it, half on the plane facing the wind; along it, nothing.
        var ridgeAlongX = planes.All(p => Math.Abs(VecMath.NewellNormal(p.ToList()).Normalized().X) < 1e-6);
        var (across, along) = ridgeAlongX ? ("S+Y", "S+X") : ("S+X", "S+Y");
        var drifted = Of(f, across);
        Assert.Equal(2, drifted.Count);
        Assert.Equal(s, drifted.Max(d => d.Value), 9);
        Assert.Equal(0.5 * s, drifted.Min(d => d.Value), 9);
        Assert.Equal(0.75 * s * plan, drifted.Sum(d => d.Total), 6);
        Assert.Empty(Of(f, along));
        Assert.Contains(Report(f).Notes, n => n.StartsWith($"„{along}” is the undrifted snow here"));
        // Undrifted and drifted are alternatives: no combination holds two of them.
        var (combos, _) = Combinations.Of(Report(f));
        var snows = Report(f).Cases.Where(c => c.Takes("snow")).Select(c => c.Id).ToHashSet();
        Assert.All(combos, c => Assert.True(c.Factors.Count(x => snows.Contains(x.Case.Id)) <= 1, c.Name));
        Assert.Contains(combos, c => c.Key == $"uls/{LoadCaseTests.IdOf(f, across)}");
    }

    [Fact]
    public void Snow_on_a_floor_nothing_covers_is_0_8_and_a_covered_floor_has_none()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.5, ["ce"] = 1.2, ["importance"] = 1.1 } });
        var snow = Of(f, "S");
        var s = 1.1 * 0.8 * 1.2 * 2.5;
        Assert.All(snow, d => Assert.Equal(s, d.Value, 9));
        // On the analytical floor (its corners on the columns): 6 × 4 m.
        Assert.Equal(s * 24, snow.Sum(d => d.Total), 6);
        var run = AnalysisRunTests.Run(f);
        var sc = run["cases"]!.AsArray().Single(c => (string?)c!["code"] == "S")!;
        Assert.Equal(-s * 24, (double)sc["sumLoads"]![2]!, 6);
        Assert.True((bool)sc["balanced"]!);
        // A floor over it covers it: the snow is on the upper one only.
        var top = (string)f.Run("storey.create", new JsonObject { ["name"] = "Roof", ["elevation"] = 6.0, ["height"] = 3.0 })!["id"]!;
        var roofSlab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = top, ["points"] = AnalysisRunTests.Rect(-0.15, -0.15, 6.15, 4.15) })!["id"]!;
        Assert.All(Of(f, "S"), d => Assert.Equal(roofSlab, d.SourceId));
    }

    [Fact]
    public void Wind_on_a_frame_its_facades_zones_by_h_over_d_its_flat_roofs_zones_and_all_of_it_placed()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.setClimate", new JsonObject { ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "II", ["importance"] = 1.0 } });
        var w = Of(f, "W+X/s");
        var facades = w.Where(d => d.SourceName.StartsWith("Façade")).ToList();
        // The building: the columns' hull 6 × 4 m, from the ground to the floor's mid-plane (its beams and slab there).
        var h = facades.Max(d => d.Points3!.Max(p => p.Z)) - facades.Min(d => d.Points3!.Min(p => p.Z));
        Assert.InRange(h, 2.8, 3.0);
        var qp = new WindSite(0.5, "II").Qp(h);
        var hd = h / 6;
        // Windward x = 0 (D), leeward x = 6 (E), each 4 × h, times 0.85 (h/d ≤ 1).
        var d = facades.Single(x => x.SourceName.EndsWith("zone D"));
        var e = facades.Single(x => x.SourceName.EndsWith("zone E"));
        Assert.Equal(qp * (0.7 + 0.1 * (hd - 0.25) / 0.75) * 0.85, d.Value, 9);
        Assert.Equal(qp * (0.3 + 0.2 * (hd - 0.25) / 0.75) * 0.85, e.Value, 9);
        Assert.Equal(4 * h, TrueArea(d), 6);
        Assert.Equal(new Vec3(1, 0, 0), d.Direction);
        Assert.Equal(new Vec3(1, 0, 0), e.Direction);
        // The sides: e = min(b = 4, 2h) = 4, A to e/5 = 0.8 m, B to 4 m, C past.
        Assert.Equal(2 * 0.8 * h, facades.Where(x => x.SourceName.EndsWith("zone A")).Sum(TrueArea), 6);
        Assert.Equal(2 * 3.2 * h, facades.Where(x => x.SourceName.EndsWith("zone B")).Sum(TrueArea), 6);
        Assert.Equal(2 * 2.0 * h, facades.Where(x => x.SourceName.EndsWith("zone C")).Sum(TrueArea), 6);
        Assert.All(facades.Where(x => x.SourceName.EndsWith("zone A")), x => Assert.Equal(1.2 * qp, x.Value, 9));
        // The floor is the roof (Table 7.2): F (e/4 × e/10 at the corners), G between, H to e/2, I past; suction, I's least −0.2.
        var roof = w.Where(x => !x.SourceName.StartsWith("Façade")).ToList();
        double Zone(string z) => roof.Where(x => x.SourceName.EndsWith($"zone {z}")).Sum(x => PlanArea(x.Points));
        Assert.Equal(2 * 0.4 * 1.0, Zone("F"), 6);
        Assert.Equal(0.4 * 2.0, Zone("G"), 6);
        Assert.Equal(1.6 * 4, Zone("H"), 6);
        Assert.Equal(4.0 * 4, Zone("I"), 6);
        Assert.Equal(-qp * (1.8 * 0.8 + 1.2 * 0.8 + 0.7 * 6.4 + 0.2 * 16), roof.Sum(x => x.Total), 6);
        // The pressure set: I is +0.2.
        Assert.Equal(-qp * (1.8 * 0.8 + 1.2 * 0.8 + 0.7 * 6.4 - 0.2 * 16), Of(f, "W+X/p").Where(x => !x.SourceName.StartsWith("Façade")).Sum(x => x.Total), 6);
        // Everything placed: on the beams along the façades and the floor, the façades' foot strip to the ground, half of
        // their height (the ground and the beams at h: the strip splits at h/2); balanced.
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString());
        var wc = run["cases"]!.AsArray().Single(c => (string?)c!["code"] == "W+X/s")!;
        Assert.Equal(0, (double)wc["unmapped"]!, 6);
        Assert.True((bool)wc["balanced"]!);
        var total = Sum(w.Select(Force));
        var mapped = V(wc["sumLoads"]) + V(wc["toGround"]);
        Assert.Equal(total.X, mapped.X, 5);
        Assert.Equal(total.Y, mapped.Y, 5);
        Assert.Equal(total.Z, mapped.Z, 5);
        Assert.Equal(0.5 * Sum(facades.Select(Force)).X, V(wc["toGround"]).X, 5);
        Assert.Equal(0, V(wc["toGround"]).Y, 6);
    }

    [Fact]
    public void Wind_on_a_steel_halls_duo_pitch_roof_takes_table_7_4a_and_its_purlins_carry_it()
    {
        var f = AnalysisRunTests.Hall();
        f.Run("analysis.setClimate", new JsonObject { ["wind"] = new JsonObject { ["qb"] = 0.5 }, ["snow"] = new JsonObject { ["s0k"] = 2.0 } });
        var surfaces = RoofSurfaces.Of(AnalysisRun.Of(f.Session.Reader).Problem.Model, f.Model);
        var hall = surfaces.Single();
        Assert.Equal("duo", hall.Shape);
        var pitch = hall.Planes[0].Slope;
        var w = Of(f, "W+X/s");
        var top = w.Max(d => d.Points3!.Max(p => p.Z));
        var qp = new WindSite(0.5, "II").Qp(top - w.Min(d => d.Points3!.Min(p => p.Z)));
        // The frames span along x: the wind along x is square to the ridge; F, G, H on the plane facing it, J by the ridge, I.
        double Cpe(double[] ys) { var a = Math.Clamp(pitch, 5, 75); return a <= 15 ? ys[0] + (ys[1] - ys[0]) * (a - 5) / 10 : double.NaN; }
        var roof = w.Where(d => d.SourceId == hall.SourceId).ToList();
        Assert.Equal(-Cpe([-1.7, -0.9]) * qp, roof.First(d => d.SourceName.EndsWith("zone F")).Value, 9);
        Assert.Equal(-Cpe([-0.6, -1.0]) * qp, roof.Single(d => d.SourceName.EndsWith("zone J")).Value, 9);
        Assert.Equal(-Cpe([-0.6, -0.4]) * qp, roof.Single(d => d.SourceName.EndsWith("zone I")).Value, 9);
        Assert.All(roof, d => Assert.True(d.Direction!.Value.Z > 0.9));
        // The pressure set of the wind along the ridge (Table 7.4b) is the suction set: left out, said.
        Assert.Empty(Of(f, "W+Y/p"));
        Assert.Contains(Report(f).Notes, n => n.StartsWith("„W+Y/p” is „W+Y/s” here"));
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString());
        foreach (var c in run["cases"]!.AsArray().Select(c => c!.AsObject()).Where(c => ((string)c["code"]!).StartsWith('W') || ((string)c["code"]!).StartsWith('S')))
        {
            Assert.Equal(0, (double)c["unmapped"]!, 6);
            Assert.True((bool)(c["balanced"] ?? true));
            var code = (string)c["code"]!;
            var total = Sum(Of(f, code).Select(Force));
            var mapped = V(c["sumLoads"]) + (c["toGround"] is { } g ? V(g) : Vec3.Zero);
            Assert.True((total - mapped).Length < 1e-6 * Math.Max(1, total.Length), $"{code}: {total} vs {mapped}");
        }
        // The roof's snow goes on its purlins only.
        var a = AnalysisRun.Of(f.Session.Reader);
        var s = a.Problem.Cases.Single(c => c.Loads.Id == LoadCaseTests.IdOf(f, "S"));
        Assert.NotEmpty(s.Loads.Span);
        Assert.All(s.Loads.Span, l => Assert.StartsWith("pur:", a.Problem.Frames[l.Element].Source.Role));
    }
}
