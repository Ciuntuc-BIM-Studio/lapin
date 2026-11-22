using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Analysis.Design;
using Lapin.Analysis.Solver;
using Lapin.Platform;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The buckling lengths from αcr in the model (docs/plan-fem.md §14.23): a steel portal frame on fixed footings (its columns'
/// in-plane lengths those of λ cot λ = −6 Ib h/(Ic L), out of its plane a cantilever's 2 h), a cantilever column (k·L = 2 h
/// kept, the αcr one beside it), the project's option and a member's own over it (one undo step each), second order taking the
/// non-sway lengths back, concrete showing its l0 only, and the steel hall of the guide (k·L against αcr, member by member).
/// </summary>
public class BucklingLengthAnalysisTests(ITestOutputHelper output)
{
    static JsonObject Q(Fixture f, string name, JsonObject? a = null) => f.Session.Query($"analysis.{name}", a ?? [])!.AsObject();
    static JsonObject Member(Fixture f, string id) => Q(f, "check", new JsonObject { ["id"] = id })["members"]!.AsArray()[0]!.AsObject();
    static JsonObject Check(JsonObject member, string key) => member["checks"]!.AsArray().Select(c => c!.AsObject()).Single(c => (string?)c["key"] == key);
    static double Val(JsonObject check, string name) => (double)check["values"]!.AsArray().Single(v => (string?)v!["name"] == name)!["value"]!;

    /// <summary>A portal of two HEB 200 S235 columns on pad footings (x = 0 and 6), an IPE 300 S235 beam between their heads, 200 kN on each head (Q-A).</summary>
    internal static (Fixture F, string Left, string Right, string Beam, double H) Portal(object? column = null, string material = "S235", object? beamSection = null, double load = 200)
    {
        var f = LoadCaseTests.F();
        f.Run("storey.create", new JsonObject { ["name"] = "Roof", ["elevation"] = 4.0, ["height"] = 3.0 });
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["height"] = 4.0 });
        var colType = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = JsonValue2(column ?? "HEB 200"), ["material"] = material })!["id"]!;
        var beamType = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = JsonValue2(beamSection ?? "IPE 300"), ["material"] = material })!["id"]!;
        var left = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0, ["typeId"] = colType })!["id"]!;
        var right = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 6, ["y"] = 0, ["typeId"] = colType })!["id"]!;
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["typeId"] = beamType })!["id"]!;
        f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 });
        f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 6, ["y"] = 0 });
        f.Run("analysis.defaultCases", []);
        var a = AnalysisRun.Of(f.Session.Reader);
        var col = a.Problem.Model.Frames.First(x => x.Source.SourceId == left);
        var h = col.J.At.Z - col.I.At.Z;
        foreach (var x in new[] { 0.0, 6.0 })
            f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(x, 0), ["z"] = h, ["value"] = load, ["caseId"] = LoadCaseTests.IdOf(f, "Q-A") });
        return (f, left, right, beam, h);
    }

    static JsonNode JsonValue2(object o) => o is JsonNode n ? n.DeepClone() : JsonValue.Create((string)o)!;

    static double Root(Func<double, double> f, double a, double b)
    {
        for (var k = 0; k < 200; k++)
        {
            var m = (a + b) / 2;
            if (f(a) * f(m) <= 0) b = m; else a = m;
        }
        return (a + b) / 2;
    }

    [Fact]
    public void A_steel_portals_columns_take_their_sway_lengths_from_alpha_cr_and_the_project_option_and_a_members_own_are_one_undo_step_each()
    {
        var (f, left, right, beam, h) = Portal();
        var a = AnalysisRun.Of(f.Session.Reader);
        var col = a.Problem.Members.First(x => x.SourceId == left);
        var bm = a.Problem.Members.First(x => x.SourceId == beam);
        // The column's depth along Y: the frame (X–Z) sways about its weak axis (I22), the beam bends about its strong one.
        var lambda = Root(x => x / Math.Tan(x) + 6 * bm.I33 * h / (col.I22 * bm.L), Math.PI / 2 + 1e-9, Math.PI - 1e-9);
        var inPlane = Math.PI * h / lambda;
        var before = Member(f, left);
        var lk = before["lengths"]!.AsObject();
        Assert.False((bool)lk["fromAlpha"]!);
        Assert.Equal(0.85 * h, (double)lk["y"]!["used"]!, 3);
        Assert.Equal("k", (string?)lk["z"]!["source"]);
        Assert.Null(lk["z"]!["alpha"]);

        f.Run("analysis.setDesign", new JsonObject { ["bucklingLengths"] = "alpha" });
        Assert.Equal("alpha", (string?)Q(f, "designOptions")["project"]!["bucklingLengths"]);
        var after = Member(f, left);
        var la = after["lengths"]!.AsObject();
        output.WriteLine($"portal h = {h:0.###}: in-plane (z-z) Lcr {la["z"]!["used"]} m (λ cot λ: {inPlane:0.####}), out of plane (y-y) {la["y"]!["used"]} m (2h {2 * h:0.###}); utilisation {before["utilisation"]} → {after["utilisation"]}");
        Assert.True((bool)la["fromAlpha"]!);
        Assert.Equal("alpha", (string?)la["z"]!["source"]);
        Assert.Equal(inPlane, (double)la["z"]!["used"]!, inPlane * 0.01);
        // Out of its plane the frame is two cantilevers (fixed footings, nothing holding the heads): 2 h about the strong axis.
        Assert.Equal("alpha", (string?)la["y"]!["source"]);
        Assert.Equal(2 * h, (double)la["y"]!["used"]!, 2 * h * 0.01);
        Assert.Equal(0.85 * h, (double)la["y"]!["kL"]!, 3);
        var b = Check(after, "steel.buckling");
        Assert.Equal((double)la["y"]!["used"]!, Val(b, "Lcr,y"), 2);
        Assert.Equal((double)la["z"]!["used"]!, Val(b, "Lcr,z"), 2);
        Assert.Contains("from αcr", (string?)b["note"]);
        Assert.True((double)after["utilisation"]! > (double)before["utilisation"]!);
        Assert.Contains(Q(f, "checks")["notes"]!.AsArray(), n => ((string)n!).StartsWith("Buckling lengths from αcr (3 members"));
        // The checks table has the lengths too.
        var row = Q(f, "checks")["members"]!.AsArray().Select(x => x!.AsObject()).Single(x => (string?)x["sourceId"] == left);
        Assert.Equal("alpha", (string?)row["lengths"]!["z"]!["source"]);

        // Annex B: a sway mode's length about an axis makes its Cm 0.9 (Table B.3, the footnote); undone with the choice.
        f.Run("analysis.setDesign", new JsonObject { ["steelInteraction"] = "B" });
        var ib = Check(Member(f, left), "steel.interaction");
        Assert.Equal(0.9, Val(ib, "Cmy"), 9);
        Assert.Equal(0.9, Val(ib, "Cmz"), 9);
        Assert.Contains("Cm = 0.9", (string?)ib["note"]);
        f.Session.Undo();

        // A member's own choice over the project's: one undo step.
        f.Run("analysis.setDesign", new JsonObject { ["ids"] = new JsonArray(right), ["bucklingLengths"] = "k" });
        Assert.Equal("k", (string?)Member(f, right)["lengths"]!["z"]!["source"]);
        Assert.Equal("alpha", (string?)Member(f, left)["lengths"]!["z"]!["source"]);
        f.Session.Undo();
        Assert.Equal("alpha", (string?)Member(f, right)["lengths"]!["z"]!["source"]);
        // A length given wins over both.
        f.Run("analysis.setDesign", new JsonObject { ["ids"] = new JsonArray(left), ["lcrZ"] = 5.0 });
        var given = Member(f, left)["lengths"]!["z"]!.AsObject();
        Assert.Equal(("given", 5.0), ((string?)given["source"], (double)given["used"]!));
        Assert.Equal(inPlane, (double)given["alpha"]!, inPlane * 0.01);
        f.Session.Undo();
        Assert.Throws<CommandException>(() => f.Run("analysis.setDesign", new JsonObject { ["bucklingLengths"] = "beta" }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setDesign", new JsonObject { ["ids"] = new JsonArray(left), ["bucklingLengths"] = 2 }));
        // Back to the user's k: exactly the checks of before.
        f.Session.Undo();
        Assert.Null(Q(f, "designOptions")["project"]);
        Assert.Equal(before.ToJsonString(), Member(f, left).ToJsonString());
    }

    [Fact]
    public void With_second_order_on_the_members_keep_their_non_sway_lengths()
    {
        var (f, left, _, _, h) = Portal();
        f.Run("analysis.setDesign", new JsonObject { ["bucklingLengths"] = "alpha" });
        f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = true });
        var l = Member(f, left)["lengths"]!.AsObject();
        output.WriteLine(l.ToJsonString());
        Assert.Equal("k", (string?)l["z"]!["source"]);
        Assert.Equal(0.85 * h, (double)l["z"]!["used"]!, 3);
        Assert.StartsWith("second order on", (string?)l["z"]!["how"]);
        Assert.NotNull(l["z"]!["alpha"]);
        Assert.Contains(Q(f, "checks")["notes"]!.AsArray(), n => ((string)n!).Contains("5.2.2(3)b"));
    }

    [Fact]
    public void A_cantilever_column_keeps_twice_its_length_with_the_alpha_cr_one_beside_it()
    {
        var f = LoadCaseTests.F();
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = "HEB 200", ["material"] = "S235" })!["id"]!;
        var col = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0, ["typeId"] = type })!["id"]!;
        f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 });
        f.Run("analysis.defaultCases", []);
        var frame = Assert.Single(AnalysisRun.Of(f.Session.Reader).Problem.Model.Frames);
        var h = frame.J.At.Z - frame.I.At.Z;
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(0, 0), ["z"] = h, ["value"] = 300, ["caseId"] = LoadCaseTests.IdOf(f, "Q-A") });
        var before = Member(f, col);
        f.Run("analysis.setDesign", new JsonObject { ["bucklingLengths"] = "alpha" });
        var l = Member(f, col)["lengths"]!.AsObject();
        output.WriteLine($"cantilever h = {h}: {l.ToJsonString()}");
        foreach (var axis in new[] { "y", "z" })
        {
            // One element: its Ncr 0.75 % over π²EI/4h² (the αcr length 0.4 % short), the column's weight lowering its mean N
            // (the length a little longer): within 1 % of 2 h either way, k·L = 2 h the floor.
            Assert.Equal(2 * h, (double)l[axis]!["alpha"]!, 2 * h * 0.01);
            Assert.Equal(Math.Max(2 * h, (double)l[axis]!["alpha"]!), (double)l[axis]!["used"]!, 3);
            Assert.True((double)l[axis]!["used"]! >= 2 * h - 1e-3);
        }
        Assert.InRange((double)Member(f, col)["utilisation"]!, (double)before["utilisation"]!, (double)before["utilisation"]! * 1.01);
    }

    [Fact]
    public void Concrete_columns_show_their_l0_from_alpha_cr_only()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        var before = Member(f, t.Columns[0]);
        f.Run("analysis.setDesign", new JsonObject { ["bucklingLengths"] = "alpha" });
        var after = Member(f, t.Columns[0]);
        output.WriteLine(after["lengths"]!.ToJsonString());
        output.WriteLine(string.Join("\n", after["notes"]!.AsArray().Select(x => (string)x!)));
        Assert.True((bool)after["lengths"]!["informative"]!);
        Assert.Equal("k", (string?)after["lengths"]!["y"]!["source"]);
        Assert.NotNull(after["lengths"]!["y"]!["alpha"]);
        Assert.Contains(after["notes"]!.AsArray(), n => ((string)n!).StartsWith("From αcr (shown only, not used)"));
        Assert.Equal(before["checks"]!.ToJsonString(), after["checks"]!.ToJsonString());
    }

    [Fact]
    public void A_timber_portals_columns_take_their_sway_lengths_too()
    {
        var (f, left, _, _, h) = Portal(new JsonObject { ["kind"] = "rectangle", ["width"] = 0.2, ["depth"] = 0.2 }, "Timber GL24h", new JsonObject { ["kind"] = "rectangle", ["width"] = 0.16, ["depth"] = 0.4 }, load: 30);
        var before = Member(f, left);
        Assert.Equal("timber", (string?)before["material"]);
        f.Run("analysis.setDesign", new JsonObject { ["bucklingLengths"] = "alpha" });
        var after = Member(f, left);
        output.WriteLine($"timber portal: {after["lengths"]!.ToJsonString()}; utilisation {before["utilisation"]} → {after["utilisation"]}");
        Assert.Equal("alpha", (string?)after["lengths"]!["z"]!["source"]);
        Assert.True((double)after["lengths"]!["z"]!["used"]! > h);
        var tb = Check(after, "timber.buckling");
        Assert.Contains("from αcr", (string?)tb["note"]);
        Assert.True(tb["utilisation"]!.GetValue<double>() > Check(before, "timber.buckling")["utilisation"]!.GetValue<double>());
    }

    [Fact]
    public void The_guide_hall_k_from_the_user_against_alpha_cr_member_by_member()
    {
        var f = AnalysisRunTests.Hall();
        // S355 (the hall's steel has no grade: no fy, no checks).
        f.Run("material.setData", new JsonObject
        {
            ["id"] = f.Model.OfClass(Lapin.Model.IfcClass.Material).Single(m => m.Name == "Steel").Id,
            ["params"] = new JsonArray(
                new JsonObject { ["pset"] = "Pset_MaterialMechanical", ["name"] = "YoungModulus", ["value"] = 210e9 },
                new JsonObject { ["pset"] = "Pset_MaterialMechanical", ["name"] = "PoissonRatio", ["value"] = 0.3 },
                new JsonObject { ["pset"] = "Pset_MaterialCommon", ["name"] = "MassDensity", ["value"] = 7850 },
                new JsonObject { ["pset"] = "Pset_MaterialSteel", ["name"] = "YieldStress", ["value"] = 355e6 }),
        });
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 }, ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "III" } });
        static Dictionary<string, JsonObject> Rows(Fixture f) => Q(f, "checks")["members"]!.AsArray().Select(x => x!.AsObject()).GroupBy(x => (string)x["id"]!).ToDictionary(g => g.Key, g => g.First());
        var k = Rows(f);
        var sw = Stopwatch.StartNew();
        f.Run("analysis.setDesign", new JsonObject { ["bucklingLengths"] = "alpha" });
        var al = Rows(f);
        output.WriteLine($"hall: checks with the lengths from αcr in {sw.ElapsedMilliseconds} ms; {al.Count} rows");
        var bySource = al.Values.Where(x => x["lengths"] != null).SelectMany(x => new[] { (string)x["lengths"]!["y"]!["source"]!, (string)x["lengths"]!["z"]!["source"]! }).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        output.WriteLine("axes by source: " + string.Join(", ", bySource.Select(kv => $"{kv.Key} {kv.Value}")));
        foreach (var g in al.Values.Where(x => x["lengths"] != null).GroupBy(x => (string)x["name"]!))
        {
            var changed = g.Where(x => Math.Abs((double)x["utilisation"]! - (double)k[(string)x["id"]!]["utilisation"]!) > 1e-4).ToList();
            var worst = g.MaxBy(x => (double)x["utilisation"]! - (double)k[(string)x["id"]!]["utilisation"]!)!;
            output.WriteLine($"{g.Key}: {g.Count()} members, {changed.Count} changed; most: {worst["name"]} {k[(string)worst["id"]!]["utilisation"]} → {worst["utilisation"]} (Lcr,y {worst["lengths"]!["y"]!["kL"]} → {worst["lengths"]!["y"]!["used"]} {worst["lengths"]!["y"]!["source"]}, Lcr,z {worst["lengths"]!["z"]!["kL"]} → {worst["lengths"]!["z"]!["used"]} {worst["lengths"]!["z"]!["source"]}; {worst["lengths"]!["combinationName"]})");
        }
        // Never shorter than k·L; some columns take the frames' sway.
        foreach (var x in al.Values.Where(x => x["lengths"] != null))
            foreach (var axis in new[] { "y", "z" })
                Assert.True((double)x["lengths"]![axis]!["used"]! >= (double)x["lengths"]![axis]!["kL"]! - 1e-3 || (string)x["lengths"]![axis]!["source"]! == "given");
        Assert.True(bySource.GetValueOrDefault("alpha") > 0);
    }
}
