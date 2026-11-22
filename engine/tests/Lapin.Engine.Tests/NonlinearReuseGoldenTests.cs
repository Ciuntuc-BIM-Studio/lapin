using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Analysis.Solver;

namespace Lapin.Engine.Tests;

/// <summary>
/// The nonlinear loop's results as the code before §14.21 gave them (docs/plan-fem.md §14.21: the numbering, the ordering,
/// the symbolic factorization and the bars' transforms kept between iterations and combinations; §14.22: the factors'
/// values), in two halves (§14.23):
/// <list type="bullet">
/// <item>Bit for bit, in one process: the reuse against solves with nothing kept (<see cref="FemReuse.Without"/>) and
/// against the jobs side by side; a SHA-256 of the bits of every result (displacements, reactions, bars, shells, axial
/// forces, first order), every case and every ultimate combination's buckling. The same machine and JIT, so any bit the
/// reuse changed shows.</item>
/// <item>Against the code before §14.21 (Golden/), portably: every nonlinear set's iterations, states, messages and
/// amplification, the analysis JSON, the buckling's factors, and of every array of numbers a sketch (<see cref="Sketch(IEnumerable{double}, bool)"/>)
/// compared within the arch noise (<see cref="GoldenFile"/>). The files hold what the code before §14.21 gave (written from
/// it at 4ca213403, whose own SHA-256 goldens it passed on the arm64 Mac they were taken on).</item>
/// </list>
/// On the guide's hall (P-Δ, imperfections, tension-only bracing, sheeting), the same without sheeting (unstable variants:
/// the mechanism path), its bracing alone (states, first order), and the guide's frame with its slab as shells on footings
/// that lift. The fixtures' ids come from a seed (<see cref="Fixture.Seeded"/>), so the model, its order and its bits are the
/// same at every run.
/// </summary>
public class NonlinearReuseGoldenTests
{
    /// <summary>The bits of numbers, hashed in their order (one process: no portability needed).</summary>
    sealed class Bits
    {
        readonly IncrementalHash _h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        readonly byte[] _b = new byte[8];
        public Bits Add(double x) { BitConverter.TryWriteBytes(_b, BitConverter.DoubleToInt64Bits(x)); _h.AppendData(_b); return this; }
        public Bits Add(IEnumerable<double> xs) { foreach (var x in xs) Add(x); return this; }
        public Bits Add(int x) { BitConverter.TryWriteBytes(_b, (long)x); _h.AppendData(_b); return this; }
        public Bits Add(bool x) => Add(x ? 1 : 0);
        public Bits Add(string? s) { if (s != null) _h.AppendData(System.Text.Encoding.UTF8.GetBytes(s)); return Add(s?.Length ?? -1); }

        public Bits Add(CaseResult? r)
        {
            if (r == null) return Add(-1);
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

    /// <summary>Everything a run gives, hashed bit for bit.</summary>
    static string AllBits(AnalysisRun run, bool buckling)
    {
        Assert.True(run.Ok);
        var b = new Bits();
        foreach (var s in run.NonlinearSets)
        {
            var r = s.Result;
            b.Add(s.Key).Add(r.Ok).Add(r.Iterations).Add(r.Converged).Add(r.Unstable).Add(r.Message).Add(string.Join(",", r.Slack)).Add(string.Join(",", r.Lifted))
                .Add(r.Amplification).Add(r.Axial).Add(r.Result).Add(r.FirstOrder).Add(r.Mechanism is { } m ? $"{m.Node}/{m.Dof}" : null);
        }
        foreach (var c in run.Solution!.Cases) b.Add(c);
        if (buckling)
            foreach (var c in run.Combinations.Where(c => UserCombinations.Ultimate(c.Kind)))
            {
                var x = run.BucklingOf(c, 6);
                b.Add(c.Key).Add(x.Ok).Add(x.Iterations).Add(x.Message).Add(x.Alpha);
                foreach (var m in x.Modes) b.Add(m);
                foreach (var sh in x.Shares) b.Add(sh);
            }
        return b.Hex();
    }

    /// <summary>
    /// An array of numbers in a few that survive the arch noise and move with any real change (§14.23): its length, its
    /// largest magnitude, the sum of its magnitudes and two projections Σ wᵢ xᵢ (weights in [−1, 1) from a hash of the
    /// index, the same on every machine) divided by that sum. A value changed by δ moves a projection by |wᵢ| δ / Σ|x|.
    /// A mode's sign is arbitrary (<paramref name="signFree"/>): the one that makes its first projection positive.
    /// </summary>
    public static JsonObject Sketch(IEnumerable<double> xs, bool signFree = false)
    {
        int n = 0;
        double max = 0, sum = 0, p1 = 0, p2 = 0;
        foreach (var x in xs)
        {
            max = Math.Max(max, Math.Abs(x));
            sum += Math.Abs(x);
            p1 += W(n, 1) * x;
            p2 += W(n, 2) * x;
            n++;
        }
        if (signFree && p1 < 0) (p1, p2) = (-p1, -p2);
        var o = new JsonObject { ["n"] = n, ["max"] = max, ["sum"] = sum };
        if (sum > 0) o["p"] = new JsonArray(p1 / sum, p2 / sum);
        return o;
    }

    /// <summary>A weight in [−1, 1) for index <paramref name="i"/> of projection <paramref name="k"/>: integer arithmetic, then exact.</summary>
    static double W(int i, int k)
    {
        var h = (ulong)(uint)i * 0x9E3779B97F4A7C15UL ^ (ulong)k * 0xC2B2AE3D27D4EB4FUL;
        h ^= h >> 33;
        h *= 0xFF51AFD7ED558CCDUL;
        h ^= h >> 33;
        return (h >> 11) * (1.0 / (1UL << 52)) - 1;
    }

    static JsonObject Sketch(CaseResult r) => new()
    {
        ["u"] = Sketch(r.U),
        ["reactionNodes"] = string.Join(",", r.Reactions.Keys.Order()),
        ["reactions"] = Sketch(r.Reactions.Keys.Order().SelectMany(n => r.Reactions[n])),
        ["barU"] = Sketch(r.Elements.SelectMany(e => e.ULocal)),
        ["barForces"] = Sketch(r.Elements.SelectMany(e => e.Forces)),
        ["shellU"] = Sketch(r.Shells.SelectMany(s => s.ULocal)),
        ["shellForces"] = Sketch(r.Shells.SelectMany(s => s.Corners.Prepend(s.Centre).SelectMany(f => new[] { f.Nx, f.Ny, f.Nxy, f.Mx, f.My, f.Mxy, f.Qx, f.Qy }))),
        ["sums"] = new JsonArray(r.SumLoads.X, r.SumLoads.Y, r.SumLoads.Z, r.SumReactions.X, r.SumReactions.Y, r.SumReactions.Z),
        ["balance"] = r.Balance,
    };

    /// <summary>What the golden holds of a run: its exact parts and the sketches of its numbers.</summary>
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
                ["amplification"] = r.Amplification, ["axial"] = Sketch(r.Axial),
            };
            if (r.Result != null) o["result"] = Sketch(r.Result);
            if (r.FirstOrder != null) o["first"] = Sketch(r.FirstOrder);
            if (r.Mechanism is { } m) o["mechanism"] = $"{m.Node}/{m.Dof}";
            sets.Add((JsonNode)o);
        }
        var json = run.RunJson().DeepClone().AsObject();
        json.Remove("solveMs");
        json.Remove("totalMs");
        var o2 = new JsonObject
        {
            ["dofs"] = run.Solution!.Dofs, ["profile"] = run.Solution.Profile,
            ["cases"] = new JsonArray(run.Solution.Cases.Select(c => (JsonNode)new JsonObject { ["id"] = c.Id, ["result"] = Sketch(c) }).ToArray()),
            ["run"] = json,
            ["sets"] = sets,
        };
        if (buckling)
        {
            var b = new JsonArray();
            foreach (var c in run.Combinations.Where(c => UserCombinations.Ultimate(c.Kind)))
            {
                var x = run.BucklingOf(c, 6);
                b.Add((JsonNode)new JsonObject
                {
                    ["key"] = c.Key, ["ok"] = x.Ok, ["iterations"] = x.Iterations, ["message"] = x.Message,
                    ["alpha"] = new JsonArray(x.Alpha.Select(a => (JsonNode)a).ToArray()),
                    ["modes"] = new JsonArray(x.Modes.Select(m => (JsonNode)Sketch(m, signFree: true)).ToArray()),
                    ["shares"] = new JsonArray(x.Shares.Select(sh => (JsonNode)Sketch(sh)).ToArray()),
                });
            }
            o2["buckling"] = b;
        }
        return o2;
    }

    /// <summary>
    /// The tolerances (§14.23), by what the noise was measured to do (arm64 against x64, each with and without vectors; the
    /// worst of the four models): the numbers <see cref="GoldenFile.Default"/> (a millionth: the amplifications and the
    /// buckling factors moved by up to 1.5·10⁻⁷, the arrays' sizes by 4·10⁻⁸); the projections, already divided by their
    /// array's size, 10⁻⁷ (moved by up to 3·10⁻⁹); the sums of the loads and the reactions 10⁻⁴ kN (their components that
    /// should be zero, ~10⁻⁶); a balance (|ΣR + ΣF| / |ΣF|, ~10⁻⁹) 10⁻⁷, far below <see cref="AnalysisRun.Balanced"/>; a
    /// buckling mode's shape and its shares 5·10⁻³ and 2·10⁻⁴ for their projections (subspace iteration stops at a
    /// tolerance: moved by up to 3.6·10⁻⁴ and 1.2·10⁻⁵), and its iterations by 2 (one more on x64 once).
    /// </summary>
    static GoldenFile.Tolerance? Tolerance(string shape) => shape switch
    {
        _ when shape.StartsWith("$.buckling[].modes[]") || shape.StartsWith("$.buckling[].shares[]") => shape.EndsWith(".p[]") ? new(0, 2e-4) : new(5e-3, 0),
        "$.buckling[].iterations" => new(0, 2),
        _ when shape.EndsWith(".p[]") => new(0, 1e-7),
        _ when shape.EndsWith(".sums[]") || shape.EndsWith(".sumReactions[]") || shape.EndsWith(".sumLoads[]") => new(1e-6, 1e-4),
        _ when shape.EndsWith(".balance") => new(0, 1e-7),
        _ => GoldenFile.Where(shape),
    };

    static void Golden(string name, JsonObject actual) => GoldenFile.Check(name, actual, Tolerance);

    static AnalysisRun Run(Fixture f)
    {
        using (FemParallel.Limit(1)) return AnalysisRun.Fresh(f.Session.Reader);
    }

    /// <summary>The run's bits with the reuse, one at a time and side by side, are those of solves with nothing kept.</summary>
    static void Reuse_is_bit_for_bit(Fixture f, bool buckling)
    {
        string without;
        using (FemReuse.Without()) without = AllBits(Run(f), buckling);
        Assert.Equal(without, AllBits(Run(f), buckling));
        Assert.Equal(without, AllBits(AnalysisRun.Fresh(f.Session.Reader), buckling));
    }

    /// <summary>A fixture with ids from a seed (random ids would order the model differently at every run, and so its last bits).</summary>
    static Fixture Seeded(Func<Fixture> make)
    {
        using (Fixture.Seeded(760)) return make();
    }

    static Fixture Unsheeted()
    {
        var f = Seeded(AnalysisRunTests.Hall);
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 }, ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "III" } });
        var braces = f.Model.Entities.Where(e => e.PredefinedType == "BRACE" && e.Str("objectType") is not "Fly brace" && !(e.Name ?? "").Contains("Fly", StringComparison.OrdinalIgnoreCase)).Select(e => e.Id).ToList();
        f.Run("analysis.setOnly", new JsonObject { ["ids"] = new JsonArray(braces.Select(x => (JsonNode)x).ToArray()), ["only"] = "tension" });
        f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = true, ["imperfections"] = true });
        return f;
    }

    static Fixture BracingAlone()
    {
        var f = Seeded(FemParallelTests.Hall);
        f.Run("analysis.setOptions", new JsonObject { ["secondOrder"] = false, ["imperfections"] = false });
        return f;
    }

    [Fact]
    public void The_halls_nonlinear_combinations_and_buckling_keep_their_results()
    {
        Golden("nonlinear-reuse-hall", Digest(Run(Seeded(FemParallelTests.Hall)), buckling: true));
    }

    [Fact]
    public void The_hall_without_sheeting_keeps_its_unstable_variants_and_results()
    {
        var d = Digest(Run(Unsheeted()), buckling: false);
        Assert.Contains(d["sets"]!.AsArray(), s => !(bool)s!["ok"]!);
        Golden("nonlinear-reuse-hall-unsheeted", d);
    }

    [Fact]
    public void The_halls_tension_only_bracing_alone_keeps_its_states_and_results()
    {
        var d = Digest(Run(BracingAlone()), buckling: true);
        Assert.Contains(d["sets"]!.AsArray(), s => s!["slack"]!.AsArray().Count > 0);
        Golden("nonlinear-reuse-hall-states", d);
    }

    [Fact]
    public void The_shell_frame_on_lifting_footings_keeps_its_results()
    {
        var d = Digest(Run(Seeded(FemParallelTests.LiftingFrame)), buckling: true);
        Assert.Contains(d["sets"]!.AsArray(), s => s!["lifted"]!.AsArray().Count > 0);
        Golden("nonlinear-reuse-shells", d);
    }

    [Fact]
    public void The_halls_reuse_gives_the_bits_of_solves_with_nothing_kept() => Reuse_is_bit_for_bit(Seeded(FemParallelTests.Hall), buckling: true);

    [Fact]
    public void The_unsheeted_halls_reuse_gives_the_bits_of_solves_with_nothing_kept() => Reuse_is_bit_for_bit(Unsheeted(), buckling: false);

    [Fact]
    public void The_bracings_reuse_gives_the_bits_of_solves_with_nothing_kept() => Reuse_is_bit_for_bit(BracingAlone(), buckling: true);

    [Fact]
    public void The_shell_frames_reuse_gives_the_bits_of_solves_with_nothing_kept() => Reuse_is_bit_for_bit(Seeded(FemParallelTests.LiftingFrame), buckling: true);
}
