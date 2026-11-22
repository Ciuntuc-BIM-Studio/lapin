using System.Security.Cryptography;
using Lapin.Analysis;
using Lapin.Analysis.Solver;

namespace Lapin.Engine.Tests;

/// <summary>
/// The factors' values handed from one system to the next (docs/plan-fem.md §14.22): a factor on values given up is the
/// one on a new array, bit for bit; the one that gave them up can no longer be used; what a nonlinear solve returns is
/// never changed by the solves after it on the same store (no result holds a value array handed on).
/// </summary>
public class FactorStoreTests
{
    /// <summary>A banded, diagonally dominant matrix in cliques of three equations: its entries by clique.</summary>
    static (int N, int[][] Cliques, double[][] Values) Banded(int n)
    {
        var rng = new Random(7);
        var cliques = new List<int[]>();
        var values = new List<double[]>();
        for (var i = 0; i + 2 < n; i++)
        {
            cliques.Add([i, i + 1, i + 2]);
            var v = new double[9];
            for (var r = 0; r < 3; r++)
                for (var c = r; c < 3; c++) v[r * 3 + c] = v[c * 3 + r] = r == c ? 10 + rng.NextDouble() : rng.NextDouble() - 0.5;
            values.Add(v);
        }
        return (n, [.. cliques], [.. values]);
    }

    static double[] FactorAndSolve(ISymmetricFactor f, int[][] cliques, double[][] values, int n)
    {
        for (var k = 0; k < cliques.Length; k++)
            for (var r = 0; r < 3; r++)
                for (var c = r; c < 3; c++) f.Add(cliques[k][r], cliques[k][c], values[k][r * 3 + c]);
        Assert.Equal(-1, f.Factor());
        var b = Enumerable.Range(0, n).Select(i => Math.Sin(i + 1.0)).ToArray();
        f.Solve(b);
        return b;
    }

    static void SameBits(double[] a, double[] b)
    {
        Assert.Equal(a.Length, b.Length);
        for (var i = 0; i < a.Length; i++) Assert.Equal(BitConverter.DoubleToInt64Bits(a[i]), BitConverter.DoubleToInt64Bits(b[i]));
    }

    [Theory]
    [InlineData(FactorKind.Sparse)]
    [InlineData(FactorKind.Skyline)]
    public void A_factor_on_values_given_up_is_the_one_on_a_new_array_and_the_one_that_gave_them_cannot_be_used(FactorKind kind)
    {
        var (n, cliques, values) = Banded(300);
        ISymmetricFactor structure = kind == FactorKind.Sparse
            ? new SparseCholesky(n, cliques)
            : new Skyline(Enumerable.Range(0, n).Select(j => Math.Max(0, j - 2)).ToArray());
        var fresh = FactorAndSolve(structure.Fresh(), cliques, values, n);

        var store = new FactorStore();
        var first = structure.Fresh(store);
        SameBits(fresh, FactorAndSolve(first, cliques, values, n));
        first.Release(store);
        Assert.Equal(1, store.Count);
        Assert.Throws<InvalidOperationException>(() => first.Solve(new double[n]));
        Assert.Throws<InvalidOperationException>(() => first.Add(0, 0, 1));
        Assert.Throws<InvalidOperationException>(() => first.Factor());

        // Its values taken (zeroed) by the next one on the structure: the same bits.
        var second = structure.Fresh(store);
        Assert.Equal(0, store.Count);
        SameBits(fresh, FactorAndSolve(second, cliques, values, n));
        // Another structure never takes them.
        second.Release(store);
        var other = kind == FactorKind.Sparse ? new SparseCholesky(n, cliques) : (ISymmetricFactor)new Skyline(Enumerable.Range(0, n).Select(j => Math.Max(0, j - 2)).ToArray());
        SameBits(fresh, FactorAndSolve(other.Fresh(store), cliques, values, n));
        Assert.Equal(1, store.Count);
        // At most Kept sets, the oldest dropped.
        for (var i = 0; i < 5; i++)
        {
            var x = structure.Fresh();
            x.Add(0, 0, 1);
            x.Release(store);
        }
        Assert.Equal(FactorStore.Kept, store.Count);
    }

    /// <summary>The bits of a result, hashed: its displacements, reactions, bars' and shells'.</summary>
    static string Digest(CaseResult? r)
    {
        if (r == null) return "";
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(double[] xs) { foreach (var x in xs) h.AppendData(BitConverter.GetBytes(x)); }
        Add(r.U);
        foreach (var n in r.Reactions.Keys.Order()) Add(r.Reactions[n]);
        foreach (var e in r.Elements) { Add(e.ULocal); Add(e.Forces); }
        foreach (var s in r.Shells) { Add(s.ULocal); Add([s.Centre.Mx, s.Centre.My, s.Centre.Nx, s.Centre.Qx]); }
        return Convert.ToHexString(h.GetHashAndReset());
    }

    static string Digest(NonlinearResult r) =>
        $"{r.Ok}/{r.Iterations}/{string.Join(",", r.Slack)}/{string.Join(",", r.Lifted)}/{Digest(r.Result)}/{Digest(r.FirstOrder)}/{Convert.ToHexString(SHA256.HashData(r.Axial.SelectMany(BitConverter.GetBytes).ToArray()))}";

    /// <summary>
    /// The aliasing guard: a solve's result, first-order result, axial forces and states, taken on a store, stay what they
    /// were while the store's values go through every other combination's iterations; and they are a solve's on its own.
    /// On the frame whose footings lift (several layouts) and on the hall (tension-only bracing, P-Δ).
    /// </summary>
    [Theory]
    [InlineData("hall")]
    [InlineData("lifting")]
    public void A_returned_result_is_not_changed_by_later_solves_on_the_same_store(string model)
    {
        var f = model == "hall" ? FemParallelTests.Hall() : FemParallelTests.LiftingFrame();
        var run = AnalysisRun.Of(f.Session.Reader);
        Assert.True(run.Ok);
        var p = run.Problem.Problem;
        var options = new NonlinearOptions(true);
        var sets = run.NonlinearSets.Take(16).ToList();
        if (model == "lifting") Assert.Contains(sets, s => s.Result.Lifted.Count > 0);

        var store = new FactorStore();
        var kept = sets.Select(s => Nonlinear.Solve(p, s.Loads, options, store)).ToList();
        var digests = kept.Select(Digest).ToList();
        Assert.InRange(store.Count, 1, FactorStore.Kept);
        // Every other solve on the same store, twice over.
        for (var round = 0; round < 2; round++)
            foreach (var s in sets) Nonlinear.Solve(p, s.Loads, options, store);
        for (var i = 0; i < sets.Count; i++)
        {
            Assert.Equal(digests[i], Digest(kept[i]));
            Assert.Equal(Digest(Nonlinear.Solve(p, sets[i].Loads, options)), digests[i]);
            Assert.Equal(Digest(sets[i].Result), digests[i]);
        }
        // The states buckling is worked out on build their own systems: never on the store's values.
        var states = kept.First(r => r.States != null).States!;
        var (sys, _) = FrameSystem.Build(states);
        var before = Digest(sys!.SolveCase(sets[0].Loads));
        foreach (var s in sets.Take(4)) Nonlinear.Solve(p, s.Loads, options, store);
        Assert.Equal(before, Digest(sys.SolveCase(sets[0].Loads)));
    }

    [Fact]
    public void A_retired_system_cannot_be_solved_on_and_its_values_go_to_the_next_one()
    {
        var f = FemParallelTests.Hall();
        var run = AnalysisRun.Of(f.Session.Reader);
        var p = run.Problem.Problem;
        var loads = run.NonlinearSets[0].Loads;
        var reference = Digest(FrameSystem.Build(p).System!.SolveCase(loads));
        var store = new FactorStore();
        var a = FrameSystem.Build(p, store: store).System!;
        // The rotations' factor, done with inside the build, is there already.
        var afterBuild = store.Count;
        Assert.Equal(reference, Digest(a.SolveCase(loads)));
        a.Retire(store);
        Assert.Equal(afterBuild + 1, store.Count);
        Assert.Throws<InvalidOperationException>(() => a.SolveCase(loads));
        var b = FrameSystem.Build(p, store: store).System!;
        Assert.Equal(reference, Digest(b.SolveCase(loads)));
    }
}
