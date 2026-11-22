using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;
using Lapin.Model;
using Lapin.Platform;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// S5 in the model (docs/plan-fem.md §14): second order on the guide's frame (each combination solved on its own, the sway
/// amplified about as 1/(1 − 1/αcr) says, the envelope above the first-order one, undone in one step), αcr of every ultimate
/// combination of the steel hall, the hall's X bracing in tension only (a diagonal slack under the wind), the pad footings
/// on Winkler's bed (the frame settles q/ks·…, the soil's reactions balance), a slab on the ground (w = q/ks), a support that
/// lifts, and the commands refusing what they cannot take.
/// </summary>
public class SecondOrderAnalysisTests(ITestOutputHelper output)
{
    static JsonObject Q(Fixture f, string name, JsonObject? a = null) => f.Session.Query($"analysis.{name}", a ?? [])!.AsObject();

    static void Climate(Fixture f) =>
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 }, ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "III" } });

    static AnalysisRunTests.Frame GuideFrame()
    {
        var t = AnalysisRunTests.Frame6x4();
        t.F.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        Climate(t.F);
        return t;
    }

    [Fact]
    public void Second_order_solves_each_combination_amplifies_its_sway_and_is_one_undo_step()
    {
        var t = GuideFrame();
        var f = t.F;
        var linear = Q(f, "run");
        Assert.Null(linear["nonlinear"]);
        var buckling = Q(f, "buckling");
        Assert.True((bool)buckling["ok"]!);
        var combos = buckling["combinations"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.NotEmpty(combos);
        foreach (var c in combos)
        {
            Assert.True((bool)c["ok"]!, c.ToJsonString());
            Assert.True((bool)c["compressed"]!);
            Assert.True((double)c["alpha"]![0]! > 1);
            Assert.Equal(AnalysisRun.ClassOf((double?)c["global"] ?? (double?)c["globalAbove"]), (string)c["class"]!);
        }
        var lowest = buckling["lowest"]!.AsObject();
        output.WriteLine($"guide frame: lowest αcr {(double)lowest["alpha"]!} in {(string)lowest["key"]!}");

        // P-Δ alone, as S5 made it (the imperfections, §14.14, said off: ImperfectionTests has them).
        f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = true, ["imperfections"] = false });
        Assert.True((bool)Q(f, "options")["secondOrder"]!);
        var run = Q(f, "run");
        Assert.True((bool)run["ok"]!);
        var nl = run["nonlinear"]!.AsObject();
        Assert.True((bool)nl["secondOrder"]!);
        var sets = nl["combinations"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.Equal(run["combinations"]!.AsArray().Count, sets.Count);
        foreach (var s in sets)
        {
            Assert.True((bool)s["ok"]!, s.ToJsonString());
            Assert.True((bool)s["converged"]!);
            Assert.InRange((int)s["iterations"]!, 2, 6);
            Assert.True((double)s["amplification"]! >= 0.999, s.ToJsonString());
        }
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("Second order (P-Δ)"));
        // A wind combination sways: its amplification about 1/(1 − 1/αcr) of its own αcr (the loads proportional, a frame's sway mode).
        var windy = sets.Where(s => ((string)s["name"]!).Contains('W')).MaxBy(s => (double)s["amplification"]!)!;
        var alpha = (double)combos.Single(c => (string)c["key"]! == (string)windy["key"]!)["global"]!;
        var amp = (double)windy["amplification"]!;
        output.WriteLine($"„{windy["name"]}”: amplification {amp}, αcr {alpha}, 1/(1 − 1/αcr) = {1 / (1 - 1 / alpha)}");
        Assert.InRange(amp, 1.0, 1 / (1 - 1 / alpha) * 1.05);
        Assert.True(amp > 1 + 0.2 * (1 / (1 - 1 / alpha) - 1));
        // The windy combination sways more than to first order, by its amplification (looked at again below, undone).
        static double Sway(JsonObject r) => r["nodes"]!.AsArray().Max(x => Math.Sqrt(Math.Pow((double)x!["d"]![0]!, 2) + Math.Pow((double)x["d"]![1]!, 2)));
        var key = (string)windy["key"]!;
        var one = Q(f, "results", new JsonObject { ["combination"] = key });
        Assert.Equal("combination", (string?)one["kind"]);
        var second = Sway(one);
        Assert.NotEmpty(Q(f, "results", new JsonObject { ["envelope"] = "uls" })["frames"]!.AsArray());
        // The checks read the second-order forces (they run).
        Assert.NotEmpty(Q(f, "checks")["members"]!.AsArray());

        f.Session.Undo();
        Assert.False((bool)Q(f, "options")["secondOrder"]!);
        Assert.Null(Q(f, "run")["nonlinear"]);
        var first = Sway(Q(f, "results", new JsonObject { ["combination"] = key }));
        output.WriteLine($"its largest sway: {first} m first order, {second} m second order");
        Assert.Equal(amp, second / first, 5);
        Assert.Throws<CommandException>(() => f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = "yes" }));
    }

    [Fact]
    public void The_steel_halls_ultimate_combinations_each_have_an_alpha_cr()
    {
        var f = AnalysisRunTests.Hall();
        Climate(f);
        Assert.True((bool)Q(f, "run")["ok"]!);
        var sw = Stopwatch.StartNew();
        var b = Q(f, "buckling");
        output.WriteLine($"hall: buckling of {b["combinations"]!.AsArray().Count} combinations in {sw.ElapsedMilliseconds} ms; lowest {b["lowest"]?.ToJsonString()}");
        foreach (var c in b["combinations"]!.AsArray().Select(x => x!.AsObject()))
        {
            Assert.True((bool)c["ok"]!, c.ToJsonString());
            if (!(bool)c["compressed"]!) continue;
            Assert.True(c["alpha"]!.AsArray().Count > 0, c.ToJsonString());
            Assert.True((double)c["alpha"]![0]! > 0);
        }
        var low = (string)b["lowest"]!["key"]!;
        output.WriteLine(b["combinations"]!.AsArray().Single(c => (string)c!["key"]! == low)!.ToJsonString());
        var mode = Q(f, "bucklingMode", new JsonObject { ["combination"] = low });
        Assert.Equal(1, (int)mode["mode"]!);
        // A mode's class is its combination's, by the frame's first global mode (not mode 1's, which may be one member's).
        Assert.Equal((string)b["combinations"]!.AsArray().Single(c => (string)c!["key"]! == low)!["class"]!, (string)mode["class"]!);
        Assert.NotEmpty(mode["polylines"]!.AsArray());
        Assert.Throws<CommandException>(() => Q(f, "bucklingMode", new JsonObject { ["combination"] = low, ["mode"] = 99 }));
    }

    [Fact]
    public void The_halls_X_bracing_in_tension_only_goes_slack_on_one_diagonal_under_the_wind()
    {
        var f = AnalysisRunTests.Hall();
        Climate(f);
        var braces = f.Model.Entities.Where(e => e.PredefinedType == "BRACE" && e.Str("objectType") is not "Fly brace" && !(e.Name ?? "").Contains("Fly", StringComparison.OrdinalIgnoreCase)).Select(e => e.Id).ToList();
        Assert.NotEmpty(braces);
        f.Run("analysis.setOnly", new JsonObject { ["ids"] = new JsonArray(braces.Select(x => (JsonNode)x).ToArray()), ["only"] = "tension" });
        Assert.Equal("tension", f.Model.Get(braces[0])!.Str(Lapin.Analysis.OneWay.Key));
        var sw = Stopwatch.StartNew();
        var run = Q(f, "run");
        output.WriteLine($"hall with {braces.Count} tension-only braces: run in {sw.ElapsedMilliseconds} ms");
        Assert.True((bool)run["ok"]!, run["notes"]!.ToJsonString());
        var sets = run["nonlinear"]!["combinations"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.All(sets, s => Assert.True((bool)s["ok"]!, s.ToJsonString()));
        Assert.False((bool)run["nonlinear"]!["secondOrder"]!);
        // Some combination has a slack diagonal, and every slack one is a brace.
        var slack = sets.SelectMany(s => s["slack"]!.AsArray().Select(x => (string)x!["sourceId"]!)).Distinct().ToList();
        Assert.NotEmpty(slack);
        Assert.All(slack, id => Assert.Contains(id, braces));
        // A slack brace carries nothing in its combination.
        var withSlack = sets.First(s => s["slack"]!.AsArray().Count > 0);
        var r = Q(f, "results", new JsonObject { ["combination"] = (string)withSlack["key"]!, ["elementId"] = (string)withSlack["slack"]![0]!["sourceId"]! });
        foreach (var st in r["frames"]!.AsArray()[0]!["stations"]!.AsArray()) Assert.True(Math.Abs((double)st!["N"]!) < 1.0, st.ToJsonString());
        // The cases stay linear: both diagonals work in a case.
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("Each static combination solved on its own"));
        f.Run("analysis.setOnly", new JsonObject { ["ids"] = new JsonArray(braces.Select(x => (JsonNode)x).ToArray()), ["only"] = null });
        Assert.Null(f.Model.Get(braces[0])!.Attrs[Lapin.Analysis.OneWay.Key]);
        Assert.Throws<CommandException>(() => f.Run("analysis.setOnly", new JsonObject { ["ids"] = new JsonArray(braces[0]), ["only"] = "bending" }));
    }

    static string[] Footings(Fixture f) => f.Model.OfClass(IfcClass.Footing).Select(e => e.Id).ToArray();

    [Fact]
    public void Pad_footings_on_Winklers_bed_settle_and_their_soil_takes_the_loads()
    {
        var t = GuideFrame();
        var f = t.F;
        var ids = Footings(f);
        Assert.Equal(4, ids.Length);
        const double ks = 40000;
        f.Run("analysis.setSupport", new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()), ["winkler"] = ks });
        var run = Q(f, "run");
        Assert.True((bool)run["ok"]!, run["notes"]!.ToJsonString());
        Assert.Equal(4, (int)run["supports"]!["supports"]!);
        Assert.Equal(4, (int)run["supports"]!["winkler"]!);
        foreach (var c in run["cases"]!.AsArray()) Assert.True((bool)c!["balanced"]!, c.ToJsonString());
        var opts = Q(f, "supportOptions", new JsonObject { ["id"] = ids[0] });
        var node = opts["nodes"]!.AsArray().Single()!.AsObject();
        // The footing's footprint (its box in plan) × ks; held across, free to settle and turn.
        var fp = f.Model.Get(ids[0])!;
        var kz = (double)node["k"]![2]!;
        Assert.True(kz > 0);
        Assert.False((bool)node["held"]![2]!);
        Assert.True((bool)node["held"]![0]!);
        Assert.NotEmpty(opts["typical"]!.AsArray());
        // Each column's foot settles its reaction over kz.
        var g1 = Q(f, "results", new JsonObject { ["case"] = "G1" });
        foreach (var r in g1["reactions"]!.AsArray().Select(x => x!.AsObject()))
        {
            var at = r["at"]!.AsArray();
            var nd = g1["nodes"]!.AsArray().Select(x => x!.AsObject()).First(x => x["at"]!.ToJsonString() == at.ToJsonString());
            Assert.Equal(-(double)r["f"]![2]! / kz, (double)nd["d"]![2]!, 9);
        }
        // Compression only on the footings: nothing pulls under gravity, nothing lifts.
        f.Run("analysis.setSupport", new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()), ["compressionOnly"] = true });
        var run2 = Q(f, "run");
        Assert.True((bool)run2["ok"]!);
        Assert.All(run2["nonlinear"]!["combinations"]!.AsArray(), s => Assert.True((bool)s!["ok"]!, s.ToJsonString()));
        // Undo twice: rigid again, the results as before.
        f.Session.Undo();
        f.Session.Undo();
        Assert.Null(Q(f, "run")["supports"]);
        Assert.Throws<CommandException>(() => f.Run("analysis.setSupport", new JsonObject { ["ids"] = new JsonArray(ids[0]), ["winkler"] = -1 }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setSupport", new JsonObject { ["ids"] = new JsonArray(ids[0]), ["springs"] = new JsonArray(1, 2, 3) }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setSupport", new JsonObject { ["ids"] = new JsonArray(t.Beams[0]), ["winkler"] = ks }));
    }

    [Fact]
    public void A_footings_given_springs_replace_its_holds()
    {
        var t = GuideFrame();
        var f = t.F;
        var ids = Footings(f);
        const double kz = 50000;
        f.Run("analysis.setSupport", new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()), ["springs"] = new JsonArray(null, null, kz, null, null, null) });
        var run = Q(f, "run");
        Assert.True((bool)run["ok"]!);
        var q = Q(f, "results", new JsonObject { ["case"] = "Q-A" });
        // The 120 kN of Q-A on four springs: Σ settlement × kz = 120.
        var sum = 0.0;
        foreach (var r in q["reactions"]!.AsArray().Select(x => x!.AsObject()))
        {
            var nd = q["nodes"]!.AsArray().Select(x => x!.AsObject()).First(x => x["at"]!.ToJsonString() == r["at"]!.ToJsonString());
            sum += -(double)nd["d"]![2]! * kz;
            Assert.Equal((double)r["f"]![2]!, -(double)nd["d"]![2]! * kz, 9);
        }
        Assert.Equal(120, sum, 6);
    }

    [Fact]
    public void A_slab_on_the_ground_on_Winklers_bed_settles_q_over_ks()
    {
        var f = LoadCaseTests.F();
        var slabType = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Ground 20", ["layers"] = new JsonArray(new JsonObject { ["material"] = AnalysisRunTests.Concrete, ["thickness"] = 0.2, ["core"] = true }),
        })!["id"]!;
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["typeId"] = slabType })!["id"]!;
        f.Run("analysis.defaultCases", []);
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = f.Storey, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        const double ks = 20000;
        f.Run("analysis.setSupport", new JsonObject { ["ids"] = new JsonArray(slab), ["winkler"] = ks });
        var run = Q(f, "run");
        Assert.True((bool)run["ok"]!, run.ToJsonString());
        Assert.True(run["supports"] != null, run["notes"]!.ToJsonString());
        Assert.Equal(1, (int)run["supports"]!["slabs"]!);
        var q = Q(f, "results", new JsonObject { ["case"] = "Q-A" });
        Assert.Equal(120, q["reactions"]!.AsArray().Sum(r => (double)r!["f"]![2]!), 6);
        var w = q["shells"]!.AsArray()[0]!["extremes"]!["w"]!;
        Assert.Equal(-5.0 / ks, (double)w["min"]!, 9);
        Assert.Equal(-5.0 / ks, (double)w["max"]!, 9);
        Assert.Throws<CommandException>(() => f.Run("analysis.setSupport", new JsonObject { ["ids"] = new JsonArray(slab), ["springs"] = new JsonArray(0, 0, 1, 0, 0, 0) }));
    }
}
