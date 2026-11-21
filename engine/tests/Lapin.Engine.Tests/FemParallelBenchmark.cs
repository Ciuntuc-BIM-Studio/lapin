using System.Diagnostics;
using System.Runtime;
using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;
using Lapin.Model;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The timings of §14.20, one combination at a time against side by side: the guide's hall (linear, P-Δ, P-Δ with the
/// imperfections, buckling of every ultimate combination), the 30 m hall, and a three-storey concrete frame with its floors
/// as shells. A measure, not a test: it runs only with LAPIN_FEM_BENCH=1 (dotnet test -c Release --filter FemParallelBenchmark;
/// DOTNET_gcServer=1 as the desktop host runs, Lapin.Host.csproj) and asserts nothing of the times.
/// </summary>
public class FemParallelBenchmark(ITestOutputHelper output)
{
    static bool On => Environment.GetEnvironmentVariable("LAPIN_FEM_BENCH") == "1";

    static void Climate(Fixture f) =>
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 }, ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "III" } });

    /// <summary>The analysis worked out anew (by reflection, so this file measures the code before §14.20 as well), how many at once by LAPIN_FEM_THREADS.</summary>
    static AnalysisRun Fresh(Fixture f) =>
        (AnalysisRun)typeof(AnalysisRun).GetMethod("Compute", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [f.Session.Reader])!;

    void Time(string what, Fixture f, bool buckling = false)
    {
        var all = Math.Max(1, Environment.ProcessorCount - 1);
        double Once(int degree)
        {
            Environment.SetEnvironmentVariable("LAPIN_FEM_THREADS", degree.ToString());
            GC.Collect();
            var before = GC.GetTotalAllocatedBytes();
            // The largest working set while it runs, sampled every 20 ms.
            var peak = Environment.WorkingSet;
            using var sampler = new Timer(_ => { var w = Environment.WorkingSet; if (w > Interlocked.Read(ref peak)) Interlocked.Exchange(ref peak, w); }, null, 0, 20);
            var sw = Stopwatch.StartNew();
            var run = Fresh(f);
            Assert.True(run.Ok);
            if (buckling) run.BucklingJson(null, 6);
            var ms = sw.Elapsed.TotalMilliseconds;
            output.WriteLine($"  {what}, {(degree == 1 ? "one at a time" : $"up to {degree} at once")}: {ms:0} ms ({run.NonlinearSets.Count} nonlinear sets, {run.Solution!.Dofs} dofs, {run.Solution.Profile:N0} entries; {(GC.GetTotalAllocatedBytes() - before) / 1e6:0} MB allocated; largest working set {Interlocked.Read(ref peak) / 1e6:0} MB, GC {(GCSettings.IsServerGC ? "server" : "workstation")})");
            return ms;
        }
        try
        {
            Once(all); // warm up the code
            var one = Once(1);
            var many = Once(all);
            output.WriteLine($"{what}: {one:0} → {many:0} ms (× {one / many:0.0})");
        }
        finally { Environment.SetEnvironmentVariable("LAPIN_FEM_THREADS", null); }
    }

    [Fact]
    public void Timings()
    {
        if (!On) return;
        output.WriteLine($"{Environment.ProcessorCount} cores, {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1e9:0.0} GB for the process");
        var f = AnalysisRunTests.Hall();
        Climate(f);
        Time("hall, linear", f);
        f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = true, ["imperfections"] = false });
        Time("hall, P-Δ", f);
        f.Run("analysis.setOptions", new JsonObject { ["imperfections"] = true });
        Time("hall, P-Δ + imperfections", f);
        f.Run("analysis.setSheeting", new JsonObject { ["purlins"] = true, ["sheeting"] = true });
        Time("hall, P-Δ + imperfections + sheeting, with buckling", f, buckling: true);

        var big = new Fixture(new Lapin.Structure.StructureModule(), new Lapin.Steel.SteelModule(), new Lapin.Drafting.DraftingModule(), new Lapin.Studio.StudioModule(), new Lapin.Analysis.AnalysisModule());
        big.Run("steel.addHall", new JsonObject { ["storeyId"] = big.Storey, ["style"] = "hall-30", ["attrs"] = new JsonObject { ["secondary"] = true } });
        big.Run("analysis.defaultCases", []);
        var steel = big.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Steel");
        big.Run("material.setData", new JsonObject
        {
            ["id"] = steel.Id,
            ["params"] = new JsonArray(
                new JsonObject { ["pset"] = "Pset_MaterialMechanical", ["name"] = "YoungModulus", ["value"] = 210e9 },
                new JsonObject { ["pset"] = "Pset_MaterialMechanical", ["name"] = "PoissonRatio", ["value"] = 0.3 },
                new JsonObject { ["pset"] = "Pset_MaterialCommon", ["name"] = "MassDensity", ["value"] = 7850 }),
        });
        Climate(big);
        big.Run("analysis.setSheeting", new JsonObject { ["purlins"] = true, ["sheeting"] = true });
        big.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = true, ["imperfections"] = true });
        Time("30 m hall, P-Δ + imperfections", big);

        var frame = Storeys(3);
        Time("3-storey frame, slabs as shells (0.5 m), linear", frame);
        frame.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = true, ["imperfections"] = true });
        Time("3-storey frame, slabs as shells (0.5 m), P-Δ + imperfections, with buckling", frame, buckling: true);
        frame.Run("analysis.setOptions", new JsonObject { ["meshSize"] = 0.25 });
        Time("3-storey frame, slabs as shells (0.25 m), P-Δ + imperfections", frame);
    }

    /// <summary>A concrete frame of 3 × 2 bays (6 × 5 m), <paramref name="n"/> storeys of 3 m, 30×30 columns, 30×50 beams, 20 cm floors, pad footings; 3 kN/m² on each floor.</summary>
    static Fixture Storeys(int n)
    {
        var f = LoadCaseTests.F();
        const string concrete = AnalysisRunTests.Concrete;
        var storeys = new List<string> { f.Storey };
        for (var i = 1; i <= n; i++)
            storeys.Add((string)f.Run("storey.create", new JsonObject { ["name"] = $"Floor {i}", ["elevation"] = 3.0 * i, ["height"] = 3.0 })!["id"]!);
        var colType = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30x30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 }, ["material"] = concrete })!["id"]!;
        var beamType = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "B30x50", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.5 }, ["material"] = concrete })!["id"]!;
        var slabType = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Floor 20", ["layers"] = new JsonArray(new JsonObject { ["material"] = concrete, ["thickness"] = 0.2, ["core"] = true }),
        })!["id"]!;
        f.Run("material.setData", new JsonObject
        {
            ["id"] = f.Model.OfClass(IfcClass.Material).First(m => m.Name == concrete).Id,
            ["params"] = new JsonArray(new JsonObject { ["pset"] = "Pset_MaterialCommon", ["name"] = "MassDensity", ["value"] = 2500 }),
        });
        double[] xs = [0, 6, 12, 18], ys = [0, 5, 10];
        for (var s = 0; s < n; s++)
        {
            foreach (var x in xs) foreach (var y in ys) f.Run("structure.addColumn", new JsonObject { ["storeyId"] = storeys[s], ["x"] = x, ["y"] = y, ["typeId"] = colType });
            foreach (var y in ys) for (var i = 0; i + 1 < xs.Length; i++)
                f.Run("structure.addBeam", new JsonObject { ["storeyId"] = storeys[s], ["x1"] = xs[i], ["y1"] = y, ["x2"] = xs[i + 1], ["y2"] = y, ["typeId"] = beamType });
            foreach (var x in xs) for (var i = 0; i + 1 < ys.Length; i++)
                f.Run("structure.addBeam", new JsonObject { ["storeyId"] = storeys[s], ["x1"] = x, ["y1"] = ys[i], ["x2"] = x, ["y2"] = ys[i + 1], ["typeId"] = beamType });
            f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = storeys[s + 1], ["points"] = AnalysisRunTests.Rect(-0.15, -0.15, 18.15, 10.15), ["typeId"] = slabType });
        }
        foreach (var x in xs) foreach (var y in ys) f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = storeys[0], ["x"] = x, ["y"] = y });
        f.Run("analysis.defaultCases", []);
        for (var s = 1; s <= n; s++)
            f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = storeys[s], ["points"] = AnalysisRunTests.Rect(0, 0, 18, 10), ["value"] = 3 });
        Climate(f);
        f.Run("analysis.setOptions", new JsonObject { ["meshSize"] = 0.5 });
        return f;
    }
}
