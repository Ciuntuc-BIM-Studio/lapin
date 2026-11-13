using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;

namespace Lapin.Engine.Tests;

/// <summary>
/// Area loads on framed roofs (docs/plan-fem.md §9.2, the rule for roofs without a deck): each carrier takes the load over its
/// tributary strip within its own roof plane — an L-shaped roof leaving out what lies outside its planes, a hipped roof's hip
/// rafters taking what lies between the jack rafters' strips and the hip, each plane's share q × its plan area (snow) or over
/// its pitch's cosine (a load per true area) — and purlins taking it across their spacing where there are no rafters.
/// </summary>
public class FramedRoofLoadTests
{
    static Fixture House((double X, double Y)[] o, string style)
    {
        var f = LoadCaseTests.F();
        var walls = Enumerable.Range(0, o.Length).Select(i => f.Wall(o[i].X, o[i].Y, o[(i + 1) % o.Length].X, o[(i + 1) % o.Length].Y)).ToList();
        foreach (var w in walls) f.Run("element.setParam", new JsonObject { ["id"] = w, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        var pts = new JsonArray(o.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts })!["id"]!;
        f.Run("architecture.roofStyle", new JsonObject { ["id"] = roof, ["style"] = style });
        f.Run("analysis.defaultCases", []);
        return f;
    }

    static void Load(Fixture f, string code, JsonArray points, double q) =>
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = f.Storey, ["points"] = points, ["z"] = 4, ["value"] = q, ["caseId"] = LoadCaseTests.IdOf(f, code) });

    static Dictionary<string, JsonObject> Cases(JsonObject run) => run["cases"]!.AsArray().ToDictionary(c => (string)c!["code"]!, c => c!.AsObject());

    static double PlanArea(IReadOnlyList<Vec3> plane) => Math.Abs(Polygon2.SignedArea(plane.Select(p => p.Xy).ToList()));

    [Fact]
    public void An_L_shaped_roof_takes_what_lies_over_its_planes_and_says_what_lies_outside_them()
    {
        var f = House([(0, 0), (12, 0), (12, 8), (6, 8), (6, 14), (0, 14)], "ro_saxon");
        Load(f, "S", AnalysisRunTests.Rect(-2, -2, 14, 16), 1);
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString());
        var planes = AnalysisRun.Of(f.Session.Reader).Problem.Model.Roofs.Single().Planes;
        var roofArea = planes.Sum(PlanArea);
        // The L's planes, not the hull of its members: the square in the L's inner corner (6..12 × 8..14, less the eaves) is outside.
        var hull = Math.Abs(Polygon2.SignedArea(Polygon2.ConvexHull(planes.SelectMany(p => p.Select(q => q.Xy)))));
        Assert.True(roofArea < hull - 15, $"{roofArea} vs hull {hull}");
        var s = Cases(run)["S"];
        Assert.Equal(-roofArea, (double)s["sumLoads"]![2]!, 6);
        Assert.Equal(roofArea, (double)s["sumReactions"]![2]!, 6);
        Assert.Equal(16 * 18 - roofArea, (double)s["unmapped"]!, 6);
        Assert.True((bool)s["balanced"]!);
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("„S”:") && ((string)n!).Contains("find nothing under them"));
        // A load inside the L only: all of it mapped.
        var g = House([(0, 0), (12, 0), (12, 8), (6, 8), (6, 14), (0, 14)], "ro_saxon");
        Load(g, "S", new JsonArray(new JsonArray(0, 0), new JsonArray(12, 0), new JsonArray(12, 8), new JsonArray(6, 8), new JsonArray(6, 14), new JsonArray(0, 14)), 1.5);
        var inside = Cases(AnalysisRunTests.Run(g))["S"];
        Assert.Equal(0, (double)inside["unmapped"]!, 6);
        Assert.Equal(-1.5 * (12 * 8 + 6 * 6), (double)inside["sumLoads"]![2]!, 6);
        Assert.True((bool)inside["balanced"]!);
    }

    [Fact]
    public void A_hipped_roofs_planes_each_take_their_own_area_its_hips_what_lies_beside_the_jack_rafters()
    {
        var f = House([(0, 0), (12, 0), (12, 8), (0, 8)], "traditional_moldova");
        var planes = AnalysisRun.Of(f.Session.Reader).Problem.Model.Roofs.Single().Planes;
        Assert.Equal(4, planes.Count);
        // One case a plane, each loaded over that plane: snow per plan, the others per true area.
        string[] codes = ["S", "G2", "Q-A", "W"];
        for (var i = 0; i < 4; i++) Load(f, codes[i], new JsonArray(planes[i].Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray()), 1);
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString());
        var cases = Cases(run);
        for (var i = 0; i < 4; i++)
        {
            var c = cases[codes[i]];
            var n = VecMath.NewellNormal(planes[i].ToList()).Normalized();
            var expected = PlanArea(planes[i]) / (codes[i] == "S" ? 1 : Math.Abs(n.Z));
            Assert.Equal(0, (double)c["unmapped"]!, 6);
            // The planes as the roof solver gives them meet along the hips within a few micrometres: the share within 1e-5.
            Assert.True(Math.Abs(-(double)c["sumLoads"]![2]! - expected) < 1e-5 * expected, $"{codes[i]}: {c["sumLoads"]![2]} vs {expected}");
            Assert.True((bool)c["balanced"]!);
        }
        // The hip ends' planes: their hip rafters carry a share, beside the jack rafters'.
        var a = AnalysisRun.Of(f.Session.Reader);
        var hipEnd = Enumerable.Range(0, 4).First(i => Math.Abs(VecMath.NewellNormal(planes[i].ToList()).X) > 0.1);
        var mc = a.Problem.Cases.Single(c => c.Loads.Id == LoadCaseTests.IdOf(f, codes[hipEnd]));
        var byRole = mc.Loads.Span.GroupBy(x => a.Problem.Frames[x.Element].Source.Role).ToDictionary(g => g.Key!, g => -g.Sum(x => x.Load.Total.Z));
        Assert.True(byRole["hip"] > 0.5, string.Join(", ", byRole));
        Assert.True(byRole["rafter"] > byRole["hip"]);
        // Snow over the whole roof: the four planes' plan area, nothing left.
        var g = House([(0, 0), (12, 0), (12, 8), (0, 8)], "traditional_moldova");
        Load(g, "S", AnalysisRunTests.Rect(-3, -3, 15, 11), 2);
        var s = Cases(AnalysisRunTests.Run(g))["S"];
        Assert.True(Math.Abs(-(double)s["sumLoads"]![2]! - 2 * planes.Sum(PlanArea)) < 2e-5 * planes.Sum(PlanArea), $"{s["sumLoads"]![2]} vs {2 * planes.Sum(PlanArea)}");
        Assert.Equal(2 * 18 * 14, -(double)s["sumLoads"]![2]! + (double)s["unmapped"]!, 6);
    }

    /// <summary>A roof framed by purlins alone, as no roof style makes one: one plane rising 2 m over 4 m in y, three purlins along x at y = 0, 2, 4 on posts fixed at their feet.</summary>
    sealed class PurlinRoof : ILapinModule, IAnalyticalElements
    {
        public const string RoofId = "test-purlin-roof";
        public string Id => "testpurlinroof";
        public string Name => "Purlin roof";
        public void Register(IModuleRegistry r) => r.Service<IAnalyticalElements>(this);
        static double Z(double y) => 3 + 0.5 * y;

        public void Contribute(IModelView m, AnalyticalContribution into)
        {
            var storey = Spatial.Storeys(m).First().Id;
            var timber = new FrameSection("timber-100x200", "Timber 100×200", new SectionShape("rectangle", 0.1, 0.2), "test");
            var post = new FrameSection("timber-200x200", "Timber 200×200", new SectionShape("rectangle", 0.2, 0.2), "test");
            foreach (var y in new[] { 0.0, 2, 4 })
            {
                var z = Z(y) - 0.1;
                into.Frames.Add(new AnalyticalFrame(RoofId, $"Roof: Purlin {y}", "IfcMember", "beam", AnalyticalKinds.Roofs, [new Vec3(0, y, z), new Vec3(6, y, z)], Vec3.UnitZ, timber, "C24", storey, 0.2, 0.1, "purlin"));
                foreach (var x in new[] { 0.0, 6 })
                {
                    var id = $"post-{x}-{y}";
                    into.Frames.Add(new AnalyticalFrame(id, $"Post {x} {y}", "IfcColumn", "column", AnalyticalKinds.Columns, [new Vec3(x, y, 0), new Vec3(x, y, z)], new Vec3(1, 0, 0), post, "C24", storey, 0.2, 0.2));
                    into.Supports.Add(new AnalyticalSupport($"foot-{x}-{y}", "Foot", SupportSpec.Fixed("test"), Region: [new(x - 0.3, y - 0.3), new(x + 0.3, y - 0.3), new(x + 0.3, y + 0.3), new(x - 0.3, y + 0.3)], Bottom: -0.5, Top: 0.5));
                }
            }
            into.Roofs.Add(new AnalyticalRoof(RoofId, [[new Vec3(0, 0, Z(0)), new Vec3(6, 0, Z(0)), new Vec3(6, 4, Z(4)), new Vec3(0, 4, Z(4))]]));
        }
    }

    /// <summary>
    /// An L-shaped roof plane (12 × 8 m and 6 × 6 m over it, rising 0.2 m a metre in y) on rafters along y every 0.6 m whose ends
    /// do not meet exactly: each rafter a few millimetres short of or past the plane's edges and off its line, those of the L's
    /// long leg in two pieces at y = 8 that overlap or leave a gap of a few millimetres; each piece on a post fixed at its foot.
    /// </summary>
    sealed class LRoof : ILapinModule, IAnalyticalElements
    {
        public const string RoofId = "test-l-roof";
        public string Id => "testlroof";
        public string Name => "L roof";
        public void Register(IModuleRegistry r) => r.Service<IAnalyticalElements>(this);
        static double Z(double y) => 3 + 0.2 * y;
        public const double PlanArea = 12 * 8 + 6 * 6;

        public void Contribute(IModelView m, AnalyticalContribution into)
        {
            var storey = Spatial.Storeys(m).First().Id;
            var timber = new FrameSection("timber-80x200", "Timber 80×200", new SectionShape("rectangle", 0.08, 0.2), "test");
            var post = new FrameSection("timber-200x200", "Timber 200×200", new SectionShape("rectangle", 0.2, 0.2), "test");
            var n = 0;
            void Rafter(double x, double y0, double y1)
            {
                var k = n++;
                var (a, b) = (new Vec3(x, y0, Z(y0) - 0.1), new Vec3(x + 0.002 * ((k % 3) - 1), y1, Z(y1) - 0.1));
                into.Frames.Add(new AnalyticalFrame(RoofId, $"Roof: Rafter {k}", "IfcMember", "beam", AnalyticalKinds.Roofs, [a, b], Vec3.UnitZ, timber, "C24", storey, 0.2, 0.08, "rafter"));
                into.Frames.Add(new AnalyticalFrame($"post-{k}", $"Post {k}", "IfcColumn", "column", AnalyticalKinds.Columns, [a with { Z = 0 }, a], new Vec3(1, 0, 0), post, "C24", storey, 0.2, 0.2));
                into.Supports.Add(new AnalyticalSupport($"foot-{k}", "Foot", SupportSpec.Fixed("test"), Region: [new(a.X - 0.1, a.Y - 0.1), new(a.X + 0.1, a.Y - 0.1), new(a.X + 0.1, a.Y + 0.1), new(a.X - 0.1, a.Y + 0.1)], Bottom: -0.5, Top: 0.5));
            }
            for (var i = 0; i <= 20; i++)
            {
                // Off the plane's edges by up to 7 mm either way, and off the grid across by up to 3 mm.
                var x = Math.Clamp(i * 0.6 + 0.003 * ((i % 3) - 1), 0, 12);
                var (lo, hi) = (0.007 * ((i % 2) * 2 - 1), (i % 4 - 1.5) * 0.004);
                if (x <= 6 + 1e-9)
                {
                    // The long leg's rafters in two pieces at y = 8: overlapping (+) or apart (−) by 5 mm.
                    var joint = 0.005 * ((i % 2) * 2 - 1);
                    Rafter(x, lo, 8 + joint / 2);
                    Rafter(x + 0.001, 8 - joint / 2, 14 + hi);
                }
                else Rafter(x, lo, 8 + hi);
            }
            into.Roofs.Add(new AnalyticalRoof(RoofId, [[new Vec3(0, 0, Z(0)), new Vec3(12, 0, Z(0)), new Vec3(12, 8, Z(8)), new Vec3(6, 8, Z(8)), new Vec3(6, 14, Z(14)), new Vec3(0, 14, Z(14))]]));
        }
    }

    [Fact]
    public void An_L_shaped_roof_whose_members_do_not_meet_exactly_takes_its_load_times_its_area()
    {
        // The guard for decision 512's report: a 148 m² L roof at 1 kN/m² once placed 436 kN when its members did not meet at nodes.
        var f = new Fixture(new Lapin.Analysis.AnalysisModule(), new LRoof());
        f.Run("analysis.defaultCases", []);
        var plan = new JsonArray(new JsonArray(0, 0), new JsonArray(12, 0), new JsonArray(12, 8), new JsonArray(6, 8), new JsonArray(6, 14), new JsonArray(0, 14));
        Load(f, "S", plan, 1);
        Load(f, "G2", AnalysisRunTests.Rect(-1, -1, 13, 15), 1);
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString() ?? run.ToJsonString());
        var cases = Cases(run);
        // Snow per plan: the L's area; nothing left.
        var s = cases["S"];
        Assert.True(Math.Abs(-(double)s["sumLoads"]![2]! - LRoof.PlanArea) < 0.01 * LRoof.PlanArea, $"{s["sumLoads"]![2]} vs {LRoof.PlanArea}");
        Assert.Equal(0, (double)s["unmapped"]!, 6);
        // Per true area over a larger rectangle: the L's area over the pitch's cosine, the rest said as unmapped.
        var cos = 1 / Math.Sqrt(1 + 0.2 * 0.2);
        var g = cases["G2"];
        Assert.True(Math.Abs(-(double)g["sumLoads"]![2]! - LRoof.PlanArea / cos) < 0.01 * LRoof.PlanArea / cos, $"{g["sumLoads"]![2]} vs {LRoof.PlanArea / cos}");
        Assert.True(Math.Abs((double)g["unmapped"]! - (14 * 16 - LRoof.PlanArea)) < 0.01 * LRoof.PlanArea);
        Assert.True((bool)s["balanced"]! && (bool)g["balanced"]!);
        Assert.DoesNotContain(run["notes"]!.AsArray(), x => ((string)x!).Contains("placed on the framed roofs"));
        // The run's own guard (a note beyond 1 %): what the members were given against what the areas call for.
        foreach (var mc in AnalysisRun.Of(f.Session.Reader).Problem.Cases.Where(c => c.RoofsExpected > 0))
            Assert.True(Math.Abs(mc.RoofsPlaced - mc.RoofsExpected) < 1e-6 * mc.RoofsExpected, $"{mc.RoofsPlaced} vs {mc.RoofsExpected}");
    }

    [Fact]
    public void Without_rafters_the_purlins_take_a_roofs_load_across_their_spacing()
    {
        var f = new Fixture(new Lapin.Analysis.AnalysisModule(), new PurlinRoof());
        f.Run("analysis.defaultCases", []);
        Load(f, "S", AnalysisRunTests.Rect(0, 0, 6, 4), 1);
        Load(f, "G2", AnalysisRunTests.Rect(0, 0, 6, 4), 1);
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString() ?? run.ToJsonString());
        var a = AnalysisRun.Of(f.Session.Reader);
        double Y(int k) => a.Problem.Frames[k].I.At.Y;
        // Snow per plan: the edge purlins half a spacing to the roof's edge (6 m²), the middle one a spacing (12 m²).
        var snow = a.Problem.Cases.Single(c => c.Loads.Id == LoadCaseTests.IdOf(f, "S"));
        var by = snow.Loads.Span.GroupBy(x => Y(x.Element)).ToDictionary(g => g.Key, g => -g.Sum(x => x.Load.Total.Z));
        Assert.Equal(6, by[0], 9);
        Assert.Equal(12, by[2], 9);
        Assert.Equal(6, by[4], 9);
        Assert.All(snow.Loads.Span, x => Assert.Equal("purlin", a.Problem.Frames[x.Element].Source.Role));
        // Per true area: over the plane's cosine, 1/√1.25.
        var g2 = Cases(run)["G2"];
        Assert.Equal(-24 * Math.Sqrt(1.25), (double)g2["sumLoads"]![2]!, 6);
        Assert.True((bool)g2["balanced"]!);
        Assert.Equal(0, (double)Cases(run)["S"]["unmapped"]!, 9);
    }
}
