using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Analysis.Solver;

namespace Lapin.Engine.Tests;

/// <summary>
/// The nonlinear loop's results bit for bit as the code before §14.21 gave them (docs/plan-fem.md §14.21: the numbering,
/// the ordering, the symbolic factorization and the bars' transforms kept between iterations and combinations): every
/// nonlinear set's iterations, states, amplification and a SHA-256 of the bits of its result, axial forces and first-order
/// result; every case's; the analysis JSON; every ultimate combination's buckling. On the guide's hall (P-Δ, imperfections,
/// tension-only bracing, sheeting), the same without sheeting (unstable variants: the mechanism path), its bracing alone
/// (states, first order), and the guide's frame with its slab as shells on footings that lift. Golden/ holds what the code
/// before §14.21 wrote (LAPIN_WRITE_GOLDEN=1 writes them again); the fixtures' ids come from a seed (<see cref="Fixture.Seeded"/>),
/// so the model, its order and its bits are the same at every run.
/// </summary>
public class NonlinearReuseGoldenTests
{
    static string Dir([CallerFilePath] string here = "") => Path.Combine(Path.GetDirectoryName(here)!, "Golden");

    static void Golden(string name, JsonObject actual)
    {
        var path = Path.Combine(Dir(), name + ".json");
        var text = actual.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        if (Environment.GetEnvironmentVariable("LAPIN_WRITE_GOLDEN") == "1") { Directory.CreateDirectory(Dir()); File.WriteAllText(path, text); return; }
        Assert.True(File.Exists(path), $"{path} is missing: LAPIN_WRITE_GOLDEN=1 writes it.");
        Assert.Equal(File.ReadAllText(path), text);
    }

    /// <summary>The bits of numbers, hashed in their order.</summary>
    sealed class Bits
    {
        readonly IncrementalHash _h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        readonly byte[] _b = new byte[8];
        public Bits Add(double x) { BitConverter.TryWriteBytes(_b, BitConverter.DoubleToInt64Bits(x)); _h.AppendData(_b); return this; }
        public Bits Add(IEnumerable<double> xs) { foreach (var x in xs) Add(x); return this; }
        public Bits Add(int x) { BitConverter.TryWriteBytes(_b, (long)x); _h.AppendData(_b); return this; }
        public Bits Add(string s) { _h.AppendData(System.Text.Encoding.UTF8.GetBytes(s)); return Add(s.Length); }

        public Bits Add(CaseResult r)
        {
            Add(r.Id).Add(r.U);
            foreach (var n in r.Reactions.Keys.Order()) Add(n).Add(r.Reactions[n]);
            Add(r.Elements.Length);
            foreach (var e in r.Elements) Add(e.ULocal).Add(e.Forces);
            Add(r.Shells.Length);
            foreach (var s in r.Shells)
            {
                Add(s.ULocal);
                foreach (var f in s.Corners.Prepend(s.Centre)) Add([f.Nx, f.Ny, f.Nxy, f.Mx, f.My, f.Mxy, f.Qx, f.Qy]);
            }
            return Add(r.SumLoads.X).Add(r.SumLoads.Y).Add(r.SumLoads.Z).Add(r.SumReactions.X).Add(r.SumReactions.Y).Add(r.SumReactions.Z).Add(r.Balance);
        }

        public string Hex() => Convert.ToHexString(_h.GetHashAndReset());
    }

    static string R(double x) => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    static JsonObject Digest(AnalysisRun run, bool buckling)
    {
        Assert.True(run.Ok);
        var sets = new JsonArray();
        foreach (var s in run.NonlinearSets)
        {
            var r = s.Result;
            var o = new JsonObject
            {
                ["key"] = s.Key, ["ok"] = r.Ok, ["iterations"] = r.Iterations, ["converged"] = r.Converged, ["unstable"] = r.Unstable, ["message"] = r.Message,
                ["slack"] = new JsonArray(r.Slack.Select(k => (JsonNode)k).ToArray()), ["lifted"] = new JsonArray(r.Lifted.Select(k => (JsonNode)k).ToArray()),
                ["amplification"] = R(r.Amplification), ["axial"] = new Bits().Add(r.Axial).Hex(),
            };
            if (r.Result != null) o["result"] = new Bits().Add(r.Result).Hex();
            if (r.FirstOrder != null) o["first"] = new Bits().Add(r.FirstOrder).Hex();
            if (r.Mechanism is { } m) o["mechanism"] = $"{m.Node}/{m.Dof}";
            sets.Add((JsonNode)o);
        }
        var cases = new Bits();
        foreach (var c in run.Solution!.Cases) cases.Add(c);
        var json = run.RunJson().DeepClone().AsObject();
        json.Remove("solveMs");
        json.Remove("totalMs");
        var o2 = new JsonObject
        {
            ["dofs"] = run.Solution.Dofs, ["profile"] = run.Solution.Profile, ["cases"] = cases.Hex(),
            ["run"] = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString()))),
            ["sets"] = sets,
        };
        if (buckling)
        {
            var b = new JsonArray();
            foreach (var c in run.Combinations.Where(c => UserCombinations.Ultimate(c.Kind)))
            {
                var x = run.BucklingOf(c, 6);
                var modes = new Bits();
                foreach (var m in x.Modes) modes.Add(m);
                foreach (var sh in x.Shares) modes.Add(sh);
                b.Add((JsonNode)new JsonObject
                {
                    ["key"] = c.Key, ["ok"] = x.Ok, ["iterations"] = x.Iterations, ["message"] = x.Message,
                    ["alpha"] = new JsonArray(x.Alpha.Select(a => (JsonNode)R(a)).ToArray()), ["modes"] = modes.Hex(),
                });
            }
            o2["buckling"] = b;
        }
        return o2;
    }

    static JsonObject Of(Fixture f, bool buckling)
    {
        using (FemParallel.Limit(1)) return Digest(AnalysisRun.Fresh(f.Session.Reader), buckling);
    }

    /// <summary>A fixture with ids from a seed (random ids would order the model differently at every run, and so its last bits).</summary>
    static Fixture Seeded(Func<Fixture> make)
    {
        using (Fixture.Seeded(760)) return make();
    }

    [Fact]
    public void The_halls_nonlinear_combinations_and_buckling_keep_their_bits()
    {
        Golden("nonlinear-reuse-hall", Of(Seeded(FemParallelTests.Hall), buckling: true));
    }

    [Fact]
    public void The_hall_without_sheeting_keeps_its_unstable_variants_and_results()
    {
        var f = Seeded(AnalysisRunTests.Hall);
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 }, ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "III" } });
        var braces = f.Model.Entities.Where(e => e.PredefinedType == "BRACE" && e.Str("objectType") is not "Fly brace" && !(e.Name ?? "").Contains("Fly", StringComparison.OrdinalIgnoreCase)).Select(e => e.Id).ToList();
        f.Run("analysis.setOnly", new JsonObject { ["ids"] = new JsonArray(braces.Select(x => (JsonNode)x).ToArray()), ["only"] = "tension" });
        f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = true, ["imperfections"] = true });
        var d = Of(f, buckling: false);
        Assert.Contains(d["sets"]!.AsArray(), s => !(bool)s!["ok"]!);
        Golden("nonlinear-reuse-hall-unsheeted", d);
    }

    [Fact]
    public void The_halls_tension_only_bracing_alone_keeps_its_states_and_results()
    {
        var f = Seeded(FemParallelTests.Hall);
        f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = false, ["imperfections"] = false });
        var d = Of(f, buckling: true);
        Assert.Contains(d["sets"]!.AsArray(), s => s!["slack"]!.AsArray().Count > 0);
        Golden("nonlinear-reuse-hall-states", d);
    }

    [Fact]
    public void The_shell_frame_on_lifting_footings_keeps_its_bits()
    {
        var f = Seeded(FemParallelTests.ShellFrame);
        // A corner pulled up (in Q-A, the area load's case) so that its footing lifts in the combinations that take it.
        var storey = f.Model.OfClass(Lapin.Model.IfcClass.BuildingStorey).Single(s => s.Name == "First floor").Id;
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = storey, ["at"] = new JsonArray(0.0, 0.0), ["value"] = 250, ["direction"] = "up" });
        var d = Of(f, buckling: true);
        Assert.Contains(d["sets"]!.AsArray(), s => s!["lifted"]!.AsArray().Count > 0);
        Golden("nonlinear-reuse-shells", d);
    }
}
