using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Analysis.Solver;
using Lapin.Model;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The combinations solved in parallel (docs/plan-fem.md §14.20): the same problem's nonlinear combinations and buckling
/// solved on many threads at once give, bit for bit, what one thread gives (its bars and shells shared between the
/// problems, prepared once); the whole analysis with its combinations in parallel equals the sequential one, on the guide's
/// steel hall (P-Δ, global imperfections, tension-only X bracing) and on the guide's frame with its slab as shells (P-Δ,
/// imperfections, footings that lift).
/// </summary>
public class FemParallelTests(ITestOutputHelper output)
{
    static JsonObject Q(Fixture f, string name, JsonObject? a = null) => f.Session.Query($"analysis.{name}", a ?? [])!.AsObject();

    static void Climate(Fixture f) =>
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 }, ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "III" } });

    /// <summary>The guide's hall with P-Δ, the global imperfections and its X bracing in tension only.</summary>
    internal static Fixture Hall()
    {
        var f = AnalysisRunTests.Hall();
        Climate(f);
        var braces = f.Model.Entities.Where(e => e.PredefinedType == "BRACE" && e.Str("objectType") is not "Fly brace" && !(e.Name ?? "").Contains("Fly", StringComparison.OrdinalIgnoreCase)).Select(e => e.Id).ToList();
        f.Run("analysis.setOnly", new JsonObject { ["ids"] = new JsonArray(braces.Select(x => (JsonNode)x).ToArray()), ["only"] = "tension" });
        f.Run("analysis.setSheeting", new JsonObject { ["purlins"] = true, ["sheeting"] = true });
        f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = true, ["imperfections"] = true });
        return f;
    }

    /// <summary>The guide's frame, its slab as shells, P-Δ and imperfections, its footings on Winkler's bed lifting in compression only.</summary>
    internal static Fixture ShellFrame()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        Climate(f);
        var footings = f.Model.OfClass(IfcClass.Footing).Select(e => (JsonNode)e.Id).ToArray();
        f.Run("analysis.setSupport", new JsonObject { ["ids"] = new JsonArray(footings), ["winkler"] = 40000, ["compressionOnly"] = true });
        f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = true, ["imperfections"] = true, ["meshSize"] = 0.5 });
        return f;
    }

    static void Same(double[] a, double[] b, string what)
    {
        Assert.Equal(a.Length, b.Length);
        for (var i = 0; i < a.Length; i++)
            if (BitConverter.DoubleToInt64Bits(a[i]) != BitConverter.DoubleToInt64Bits(b[i]))
                Assert.Fail($"{what}[{i}]: {a[i]:R} ≠ {b[i]:R}");
    }

    /// <summary>Two results bit for bit: displacements, reactions, every bar's and shell's.</summary>
    static void Same(CaseResult a, CaseResult b, string what)
    {
        Same(a.U, b.U, $"{what}.U");
        Assert.Equal(a.Reactions.Keys.Order(), b.Reactions.Keys.Order());
        foreach (var (n, r) in a.Reactions) Same(r, b.Reactions[n], $"{what}.R{n}");
        Assert.Equal(a.Elements.Length, b.Elements.Length);
        for (var k = 0; k < a.Elements.Length; k++)
        {
            Same(a.Elements[k].ULocal, b.Elements[k].ULocal, $"{what}.bar{k}.u");
            Same(a.Elements[k].Forces, b.Elements[k].Forces, $"{what}.bar{k}.f");
        }
        Assert.Equal(a.Shells.Length, b.Shells.Length);
        for (var k = 0; k < a.Shells.Length; k++)
        {
            Same(a.Shells[k].ULocal, b.Shells[k].ULocal, $"{what}.shell{k}.u");
            Assert.Equal(a.Shells[k].Centre, b.Shells[k].Centre);
            Assert.Equal(a.Shells[k].Corners, b.Shells[k].Corners);
        }
    }

    static void Same(NonlinearResult a, NonlinearResult b, string what)
    {
        Assert.Equal(a.Ok, b.Ok);
        Assert.Equal(a.Iterations, b.Iterations);
        Assert.Equal(a.Converged, b.Converged);
        Assert.Equal(a.Message, b.Message);
        Assert.Equal(a.Slack, b.Slack);
        Assert.Equal(a.Lifted, b.Lifted);
        Assert.Equal(BitConverter.DoubleToInt64Bits(a.Amplification), BitConverter.DoubleToInt64Bits(b.Amplification));
        Same(a.Axial, b.Axial, $"{what}.axial");
        if (a.Result != null) Same(a.Result, b.Result!, what);
    }

    static void Same(BucklingResult a, BucklingResult b, string what)
    {
        Assert.Equal(a.Ok, b.Ok);
        Assert.Equal(a.Iterations, b.Iterations);
        Same(a.Alpha, b.Alpha, $"{what}.alpha");
        Assert.Equal(a.Modes.Length, b.Modes.Length);
        for (var i = 0; i < a.Modes.Length; i++) Same(a.Modes[i], b.Modes[i], $"{what}.mode{i}");
    }

    /// <summary>
    /// The race the shared elements had: the same problem's combinations solved on many threads at once, again and again,
    /// each result bit for bit the one thread's (its bars and shells are the same objects in every problem).
    /// </summary>
    [Theory]
    [InlineData("hall")]
    [InlineData("shells")]
    public void Many_threads_solving_one_problems_combinations_at_once_give_the_one_threads_results(string model)
    {
        var f = model == "hall" ? Hall() : ShellFrame();
        var run = AnalysisRun.Of(f.Session.Reader);
        Assert.True(run.Ok);
        var p = run.Problem.Problem;
        var options = new NonlinearOptions(true);
        var sets = run.NonlinearSets.Take(model == "hall" ? 12 : 24).ToList();
        Assert.NotEmpty(sets);
        if (model == "shells") Assert.NotEmpty(p.Shells);
        var reference = sets.Select(s => Nonlinear.Solve(p, s.Loads, options)).ToList();
        var buckling = sets.Where(s => s.Result.States != null).Take(4).ToList();
        var bReference = buckling.Select(s => Buckling.Solve(FrameSystem.Build(s.Result.States!).System!, s.Result.Axial, 6, tolerance: 1e-6)).ToList();
        var jobs = Enumerable.Range(0, 3).SelectMany(_ => Enumerable.Range(0, sets.Count + buckling.Count)).ToList();
        var failures = new ConcurrentBag<string>();
        Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount) }, i =>
        {
            try
            {
                if (i < sets.Count) Same(reference[i], Nonlinear.Solve(p, sets[i].Loads, options), sets[i].Key);
                else
                {
                    var s = buckling[i - sets.Count];
                    Same(bReference[i - sets.Count], Buckling.Solve(FrameSystem.Build(s.Result.States!).System!, s.Result.Axial, 6, tolerance: 1e-6), s.Key);
                }
            }
            catch (Exception ex) { failures.Add($"{i}: {ex.GetType().Name}: {ex.Message}"); }
        });
        output.WriteLine($"{model}: {jobs.Count} solves on {Environment.ProcessorCount} cores, {failures.Count} differ");
        Assert.True(failures.IsEmpty, string.Join("\n", failures.Take(10)));
    }

    /// <summary>
    /// The whole analysis one combination at a time and side by side: the same JSON, every nonlinear set's and every
    /// case's result bit for bit, the same buckling of every ultimate combination.
    /// </summary>
    [Theory]
    [InlineData("hall")]
    [InlineData("shells")]
    public void The_analysis_with_its_combinations_side_by_side_is_the_sequential_one_bit_for_bit(string model)
    {
        var f = model == "hall" ? Hall() : ShellFrame();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        AnalysisRun one;
        JsonObject oneBuckling;
        using (FemParallel.Limit(1))
        {
            one = AnalysisRun.Fresh(f.Session.Reader);
            oneBuckling = one.BucklingJson(null, 6);
        }
        var tOne = sw.ElapsedMilliseconds;
        sw.Restart();
        AnalysisRun many;
        JsonObject manyBuckling;
        using (FemParallel.Limit(8))
        {
            many = AnalysisRun.Fresh(f.Session.Reader);
            manyBuckling = many.BucklingJson(null, 6);
        }
        var tMany = sw.ElapsedMilliseconds;
        output.WriteLine($"{model}: {one.NonlinearSets.Count} nonlinear sets, {one.Solution!.Dofs} dofs; one at a time {tOne} ms, side by side {tMany} ms");
        Assert.True(one.Ok);
        Assert.NotEmpty(one.NonlinearSets);
        Assert.Contains(one.NonlinearSets, s => s.Variant != null);
        if (model == "shells") Assert.NotEmpty(one.Problem.Problem.Shells);
        else Assert.Contains(one.NonlinearSets, s => s.Result.Slack.Count > 0);
        Assert.Equal(one.NonlinearSets.Count, many.NonlinearSets.Count);
        for (var i = 0; i < one.NonlinearSets.Count; i++)
        {
            Assert.Equal(one.NonlinearSets[i].Key, many.NonlinearSets[i].Key);
            Same(one.NonlinearSets[i].Result, many.NonlinearSets[i].Result, one.NonlinearSets[i].Key);
        }
        for (var i = 0; i < one.Solution.Cases.Count; i++) Same(one.Solution.Cases[i], many.Solution!.Cases[i], one.Solution.Cases[i].Id);
        static string NoTimes(JsonObject o)
        {
            var c = o.DeepClone().AsObject();
            c.Remove("solveMs");
            c.Remove("totalMs");
            return c.ToJsonString();
        }
        Assert.Equal(NoTimes(one.RunJson()), NoTimes(many.RunJson()));
        Assert.Equal(oneBuckling.ToJsonString(), manyBuckling.ToJsonString());
        foreach (var c in one.Combinations.Where(c => UserCombinations.Ultimate(c.Kind)))
            Same(one.BucklingOf(c, 6), many.BucklingOf(c, 6), c.Key);
    }

    [Fact]
    public void Jobs_come_back_in_their_order_the_lowest_failure_is_thrown_and_a_stop_ends_them()
    {
        using (FemParallel.Limit(4))
        {
            var r = FemParallel.Map(Enumerable.Range(0, 100).ToList(), i => { Thread.SpinWait(1000 * (i % 7)); return i * i; });
            Assert.Equal(Enumerable.Range(0, 100).Select(i => i * i), r);
            var ex = Assert.Throws<InvalidOperationException>(() => FemParallel.Map(Enumerable.Range(0, 50).ToList(), i => i is 17 or 33 ? throw new InvalidOperationException($"job {i}") : i));
            Assert.Equal("job 17", ex.Message);
            using var stop = new CancellationTokenSource();
            stop.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() => FemParallel.Map(Enumerable.Range(0, 50).ToList(), i => i, cancel: stop.Token));
        }
        // One at a time: the same.
        using (FemParallel.Limit(1))
        {
            Assert.Equal(1, FemParallel.Degree(10, 0));
            Assert.Equal(Enumerable.Range(0, 10), FemParallel.Map(Enumerable.Range(0, 10).ToList(), i => i));
        }
        // The memory bound: jobs too large for a quarter of the memory go one at a time.
        using (FemParallel.Limit(8)) Assert.Equal(1, FemParallel.Degree(10, long.MaxValue / 2));
    }
}
