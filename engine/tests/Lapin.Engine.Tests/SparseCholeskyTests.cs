using System.Diagnostics;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// Lapin's sparse Cholesky (docs/plan-fem.md §10.6): random symmetric positive definite matrices built from element-like
/// cliques against a dense factorization, with and without groups, many right-hand sides; a singular matrix said with its
/// equation, and a spring holding it; the frame solver's results the same as the skyline's; and (with LAPIN_FEM_BENCH set)
/// the timings on the big frames of §9.7.
/// </summary>
public class SparseCholeskyTests(ITestOutputHelper output)
{
    /// <summary>A random SPD matrix: n equations in groups of up to 6, elements joining 2–4 groups, each adding Bᵀ·B (positive semidefinite) plus a diagonal.</summary>
    static (double[,] A, int[] Group, List<int[]> Cliques) Random(int n, int seed, bool grouped)
    {
        var rnd = new Random(seed);
        var group = new int[n];
        var g = 0;
        for (var i = 0; i < n;)
        {
            var size = grouped ? rnd.Next(1, 7) : 1;
            for (var k = 0; k < size && i < n; k++) group[i++] = g;
            g++;
        }
        var members = Enumerable.Range(0, g).Select(x => Enumerable.Range(0, n).Where(i => group[i] == x).ToArray()).ToArray();
        var a = new double[n, n];
        var cliques = new List<int[]>();
        for (var e = 0; e < g * 2; e++)
        {
            // Mostly near neighbours (a mesh), sometimes far ones (fill).
            var g0 = rnd.Next(g);
            var gs = new HashSet<int> { g0 };
            var parts = rnd.Next(2, 5);
            while (gs.Count < Math.Min(parts, g)) gs.Add(rnd.NextDouble() < 0.9 ? Math.Clamp(g0 + rnd.Next(-5, 6), 0, g - 1) : rnd.Next(g));
            var eqs = gs.SelectMany(x => members[x]).ToArray();
            cliques.Add(eqs);
            var m = eqs.Length;
            var b = new double[m + 2, m];
            for (var r = 0; r < m + 2; r++) for (var c = 0; c < m; c++) b[r, c] = rnd.NextDouble() * 2 - 1;
            for (var r = 0; r < m; r++)
                for (var c = 0; c < m; c++)
                {
                    var s = 0.0;
                    for (var k = 0; k < m + 2; k++) s += b[k, r] * b[k, c];
                    a[eqs[r], eqs[c]] += s;
                }
        }
        for (var i = 0; i < n; i++) a[i, i] += 0.1;
        return (a, group, cliques);
    }

    static double[] DenseSolve(double[,] a0, double[] b0)
    {
        var n = b0.Length;
        var a = (double[,])a0.Clone();
        var b = (double[])b0.Clone();
        for (var j = 0; j < n; j++)
        {
            var d = a[j, j];
            for (var k = 0; k < j; k++) d -= a[j, k] * a[j, k];
            a[j, j] = Math.Sqrt(d);
            for (var i = j + 1; i < n; i++)
            {
                var s = a[i, j];
                for (var k = 0; k < j; k++) s -= a[i, k] * a[j, k];
                a[i, j] = s / a[j, j];
            }
        }
        for (var i = 0; i < n; i++) { for (var k = 0; k < i; k++) b[i] -= a[i, k] * b[k]; b[i] /= a[i, i]; }
        for (var i = n - 1; i >= 0; i--) { for (var k = i + 1; k < n; k++) b[i] -= a[k, i] * b[k]; b[i] /= a[i, i]; }
        return b;
    }

    static SparseCholesky Build(double[,] a, List<int[]> cliques, int[]? group)
    {
        var n = a.GetLength(0);
        var f = new SparseCholesky(n, cliques, group);
        for (var i = 0; i < n; i++)
            for (var j = 0; j <= i; j++)
                if (a[i, j] != 0) f.Add(i, j, a[i, j]);
        return f;
    }

    [Theory]
    [InlineData(1, false, 40)]
    [InlineData(2, true, 60)]
    [InlineData(3, true, 300)]
    [InlineData(4, false, 250)]
    public void A_random_positive_definite_matrix_solves_as_the_dense_factorization_does(int seed, bool grouped, int n)
    {
        var (a, group, cliques) = Random(n, seed, grouped);
        var f = Build(a, cliques, grouped ? group : null);
        Assert.Equal(-1, f.Factor());
        var rnd = new Random(seed + 100);
        for (var rhs = 0; rhs < 3; rhs++)
        {
            var b = Enumerable.Range(0, n).Select(_ => rnd.NextDouble() - 0.5).ToArray();
            var expected = DenseSolve(a, b);
            var x = (double[])b.Clone();
            f.Solve(x);
            var scale = expected.Max(Math.Abs);
            for (var i = 0; i < n; i++) Assert.True(Math.Abs(x[i] - expected[i]) <= 1e-9 * scale, $"x[{i}] = {x[i]}, dense {expected[i]}");
        }
        // The factor holds fewer entries than the dense lower triangle.
        Assert.True(f.Stored <= (long)n * (n + 1) / 2 + n * 6);
    }

    [Fact]
    public void A_shell_grid_ordered_by_nested_dissection_solves_to_its_residual()
    {
        // 40 × 40 four-node elements, six equations a node, random element matrices: big enough for nested dissection.
        const int nx = 40;
        var n = (nx + 1) * (nx + 1) * 6;
        var rnd = new Random(7);
        var elements = new List<(int[] Eqs, double[,] K)>();
        for (var j = 0; j < nx; j++)
            for (var i = 0; i < nx; i++)
            {
                int[] nodes = [j * (nx + 1) + i, j * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i];
                var eqs = nodes.SelectMany(x => Enumerable.Range(x * 6, 6)).ToArray();
                var k = new double[24, 24];
                for (var r = 0; r < 30; r++)
                {
                    var b = Enumerable.Range(0, 24).Select(_ => rnd.NextDouble() - 0.5).ToArray();
                    for (var x = 0; x < 24; x++) for (var y = 0; y < 24; y++) k[x, y] += b[x] * b[y];
                }
                elements.Add((eqs, k));
            }
        var f = new SparseCholesky(n, elements.Select(e => e.Eqs).ToList(), Enumerable.Range(0, n).Select(i => i / 6).ToArray());
        Assert.Equal("nested dissection", f.Ordering);
        foreach (var (eqs, k) in elements)
            for (var x = 0; x < 24; x++) for (var y = 0; y <= x; y++) f.Add(eqs[x], eqs[y], k[x, y]);
        for (var i = 0; i < n; i++) f.Add(i, i, 1e-3);
        Assert.Equal(-1, f.Factor());
        var bvec = Enumerable.Range(0, n).Select(_ => rnd.NextDouble() - 0.5).ToArray();
        var xv = (double[])bvec.Clone();
        f.Solve(xv);
        var ax = new double[n];
        foreach (var (eqs, k) in elements)
            for (var x = 0; x < 24; x++) for (var y = 0; y < 24; y++) ax[eqs[x]] += k[x, y] * xv[eqs[y]];
        for (var i = 0; i < n; i++) ax[i] += 1e-3 * xv[i];
        var res = Math.Sqrt(Enumerable.Range(0, n).Sum(i => (ax[i] - bvec[i]) * (ax[i] - bvec[i])));
        var norm = Math.Sqrt(bvec.Sum(v => v * v));
        Assert.True(res < 1e-9 * norm, $"residual {res / norm:E2}");
    }

    [Fact]
    public void A_singular_matrix_is_said_by_its_equation_and_a_spring_holds_it()
    {
        // A chain of springs between 4 points along a line, nothing holding it: one rigid motion.
        var k = new double[4, 4];
        for (var e = 0; e < 3; e++) { k[e, e] += 1; k[e + 1, e + 1] += 1; k[e, e + 1] -= 1; k[e + 1, e] -= 1; }
        var cliques = new List<int[]> { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 } };
        var f = Build(k, cliques, null);
        var bad = f.Factor();
        Assert.InRange(bad, 0, 3);
        var held = Build(k, cliques, null);
        var springs = new List<int>();
        Assert.Equal(-1, held.Factor(i => { springs.Add(i); return 1; }));
        Assert.Single(springs);
        // Pulled apart at its ends, held by the one spring: the stretch is 3 (three unit springs in series).
        var b = new double[4];
        b[0] = -1;
        b[3] = 1;
        held.Solve(b);
        Assert.Equal(3, b[3] - b[0], 9);
    }

    /// <summary>A building frame: nx × ny columns on a 4 m × 5 m grid, nz storeys of 3 m, beams both ways, fixed bases; gravity on the beams and a push along x at each floor.</summary>
    public static FrameProblem Grid(int nx, int ny, int nz)
    {
        const double E = 30e6, G = 12.5e6;
        var p = new FrameProblem();
        int Node(int i, int j, int k) => (k * ny + j) * nx + i;
        for (var k = 0; k < nz; k++)
            for (var j = 0; j < ny; j++)
                for (var i = 0; i < nx; i++)
                    p.AddNode(new Vec3(4 * i, 5 * j, 3 * k), $"N{i}.{j}.{k}", k == 0 ? [true, true, true, true, true, true] : null);
        FrameElement Bar(int a, int b, Vec3 l2, double w, double h) => new()
        {
            Id = $"b{a}-{b}", I = a, J = b, Local2 = l2, E = E, G = G, A = w * h, I22 = h * w * w * w / 12, I33 = w * h * h * h / 12,
            Jt = 0.2 * w * w * w * h, As2 = 5.0 / 6 * w * h, As3 = 5.0 / 6 * w * h,
        };
        var c = new CaseLoads("G");
        var push = new CaseLoads("W");
        for (var k = 1; k < nz; k++)
            for (var j = 0; j < ny; j++)
                for (var i = 0; i < nx; i++)
                {
                    p.Elements.Add(Bar(Node(i, j, k - 1), Node(i, j, k), new Vec3(1, 0, 0), 0.4, 0.4));
                    if (i + 1 < nx) { p.Elements.Add(Bar(Node(i, j, k), Node(i + 1, j, k), Vec3.UnitZ, 0.3, 0.5)); c.Span.Add((p.Elements.Count - 1, SpanLoad.Uniform(0, 4, new Vec3(0, 0, -20)))); }
                    if (j + 1 < ny) { p.Elements.Add(Bar(Node(i, j, k), Node(i, j + 1, k), Vec3.UnitZ, 0.3, 0.5)); c.Span.Add((p.Elements.Count - 1, SpanLoad.Uniform(0, 5, new Vec3(0, 0, -15)))); }
                    push.AddNodal(Node(i, j, k), new Vec3(2 + 0.1 * j, 0.5, 0));
                }
        p.Cases.Add(c);
        p.Cases.Add(push);
        return p;
    }

    [Fact]
    public void The_frame_solver_gives_the_skylines_results_to_1e_9()
    {
        var p = Grid(6, 5, 5);
        var sparse = FrameSolver.Solve(p);
        var sky = FrameSolver.Solve(p, FactorKind.Skyline);
        Assert.True(sparse.Ok && sky.Ok);
        for (var c = 0; c < p.Cases.Count; c++)
        {
            var (a, b) = (sparse.Cases[c], sky.Cases[c]);
            var scale = b.U.Max(Math.Abs);
            for (var i = 0; i < a.U.Length; i++) Assert.True(Math.Abs(a.U[i] - b.U[i]) <= 1e-9 * scale, $"U[{i}]: {a.U[i]} vs {b.U[i]}");
            foreach (var (n, r) in b.Reactions)
                for (var d = 0; d < 6; d++) Assert.True(Math.Abs(a.Reactions[n][d] - r[d]) <= 1e-9 * Math.Max(1, Math.Abs(r[d])));
            Assert.True(a.Balance < 1e-9);
        }
        // Fewer entries in the factor than in the profile.
        Assert.True(sparse.Profile < sky.Profile, $"sparse {sparse.Profile}, skyline {sky.Profile}");
    }

    /// <summary>
    /// The factorization alone, timed (LAPIN_FEM_BENCH=1): a 3D frame grid (two-node elements) and a 2D shell grid (four-node
    /// elements), six equations a node, each element's matrix a random Bᵀ·B; the ordering, the factor's size and its rate.
    /// </summary>
    [Fact]
    public void Sparse_factor_timed()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAPIN_FEM_BENCH"))) return;
        foreach (var (name, nodes, elements) in new[] { Frame3d(31, 31, 15), Shell2d(150, 150), Shell2d(180, 180) })
        {
            var n = nodes * 6;
            var cliques = elements.Select(e => e.SelectMany(x => Enumerable.Range(x * 6, 6)).ToArray()).ToList();
            var group = Enumerable.Range(0, n).Select(i => i / 6).ToArray();
            var sw = Stopwatch.StartNew();
            var f = new SparseCholesky(n, cliques, group);
            var symbolic = sw.Elapsed.TotalSeconds;
            var rnd = new Random(1);
            foreach (var c in cliques)
            {
                var m = c.Length;
                var b = new double[m];
                for (var r = 0; r < 3; r++)
                {
                    for (var i = 0; i < m; i++) b[i] = rnd.NextDouble() - 0.5;
                    for (var i = 0; i < m; i++) for (var j = 0; j <= i; j++) f.Add(c[i], c[j], b[i] * b[j]);
                }
            }
            for (var i = 0; i < n; i++) f.Add(i, i, 1);
            var assembled = sw.Elapsed.TotalSeconds;
            Assert.Equal(-1, f.Factor());
            var factored = sw.Elapsed.TotalSeconds;
            var x = new double[n];
            x[0] = 1;
            f.Solve(x);
            var solved = sw.Elapsed.TotalSeconds;
            output.WriteLine($"{name}: {n} DOFs, {f.Ordering}, {f.Supernodes} supernodes, {f.Stored:N0} entries ({f.Stored * 8 / 1e6:N0} MB), {f.Flops / 1e9:N1} GFlop; " +
                $"symbolic {symbolic:N2} s, assembly {assembled - symbolic:N2} s, factor {factored - assembled:N2} s ({f.Flops / 1e9 / (factored - assembled):N2} GFlop/s), solve {solved - factored:N3} s");
        }

        static (string, int, List<int[]>) Frame3d(int nx, int ny, int nz)
        {
            int Node(int i, int j, int k) => (k * ny + j) * nx + i;
            var e = new List<int[]>();
            for (var k = 0; k < nz; k++)
                for (var j = 0; j < ny; j++)
                    for (var i = 0; i < nx; i++)
                    {
                        if (i + 1 < nx) e.Add([Node(i, j, k), Node(i + 1, j, k)]);
                        if (j + 1 < ny) e.Add([Node(i, j, k), Node(i, j + 1, k)]);
                        if (k + 1 < nz) e.Add([Node(i, j, k), Node(i, j, k + 1)]);
                    }
            return ($"frame {nx}×{ny}×{nz}", nx * ny * nz, e);
        }
        static (string, int, List<int[]>) Shell2d(int nx, int ny)
        {
            var e = new List<int[]>();
            for (var j = 0; j < ny; j++)
                for (var i = 0; i < nx; i++)
                    e.Add([j * (nx + 1) + i, j * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i]);
            return ($"shells {nx}×{ny}", (nx + 1) * (ny + 1), e);
        }
    }

    /// <summary>The big frames of §9.7 (26,460 and 86,490 degrees of freedom), timed: LAPIN_FEM_BENCH=1 (=skyline: the profile solver too).</summary>
    [Fact]
    public void Big_frames_timed()
    {
        var bench = Environment.GetEnvironmentVariable("LAPIN_FEM_BENCH");
        if (string.IsNullOrEmpty(bench)) return;
        foreach (var (nx, ny, nz) in new[] { (21, 21, 10), (31, 31, 15) })
        {
            var p = Grid(nx, ny, nz);
            foreach (var kind in bench == "skyline" ? new[] { FactorKind.Sparse, FactorKind.Skyline } : [FactorKind.Sparse])
            {
                GC.Collect();
                var sw = Stopwatch.StartNew();
                var s = FrameSolver.Solve(p, kind);
                output.WriteLine($"{kind} {nx}×{ny}×{nz}: {s.Dofs} DOFs, {s.Profile:N0} entries ({s.Profile * 8 / 1e6:N0} MB), {sw.Elapsed.TotalSeconds:N2} s, balance {s.Cases.Max(c => c.Balance):E1}");
            }
        }
    }
}
