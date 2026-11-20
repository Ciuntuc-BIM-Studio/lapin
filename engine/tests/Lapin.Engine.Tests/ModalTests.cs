using Lapin.Analysis.Seismic;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The modal analysis (docs/plan-fem.md §11.2, §11.6) against closed forms: the shear building of N equal storeys
/// (ωn = 2 √(k/m) sin((2n − 1)π / 2(2N + 1))), the cantilever (ω1 = 1.8751² √(EI/m̄L⁴)) and the simply supported beam
/// (ωn = (nπ)² √(EI/m̄L⁴)) with their mass lumped at the nodes; the effective masses summing to the total, the Rayleigh
/// quotient equal to ω², the mass-normalized shapes, the subspace iteration's small problem by Jacobi.
/// </summary>
public class ModalTests(ITestOutputHelper output)
{
    const double E = 30e6, I = 0.0054, A = 0.18;
    static readonly bool[] Fixed = [true, true, true, true, true, true];

    static FrameElement Bar(int i, int j, Vec3 local2) => new() { Id = $"b{i}-{j}", I = i, J = j, Local2 = local2, E = E, G = E / 2.4, A = A, I22 = I, I33 = I, Jt = 2 * I };

    static FrameSystem System(FrameProblem p)
    {
        var (sys, failure) = FrameSystem.Build(p);
        Assert.True(sys != null, failure?.Refused ?? failure?.Mechanism?.ToString());
        return sys!;
    }

    [Fact]
    public void A_shear_building_has_the_closed_form_periods_and_its_effective_masses_sum_to_the_total()
    {
        const int N = 3;
        const double h = 3, m = 20;
        var p = new FrameProblem();
        p.AddNode(new Vec3(0, 0, 0), "base", Fixed);
        // Each floor free only along X: the columns fixed-fixed between floors, k = 12 EI / h³.
        for (var s = 1; s <= N; s++) p.AddNode(new Vec3(0, 0, h * s), $"f{s}", [false, true, true, true, true, true]);
        for (var s = 0; s < N; s++) p.Elements.Add(Bar(s, s + 1, new Vec3(1, 0, 0)));
        var mass = new double[N + 1];
        for (var s = 1; s <= N; s++) mass[s] = m;
        var sys = System(p);
        var r = Modal.Solve(sys, mass, limit: 10);
        Assert.Equal(N, r.Modes.Count);
        var k = 12 * E * I / (h * h * h);
        for (var n = 1; n <= N; n++)
        {
            var w = 2 * Math.Sqrt(k / m) * Math.Sin((2 * n - 1) * Math.PI / (2 * (2 * N + 1)));
            output.WriteLine($"mode {n}: ω {r.Modes[n - 1].Omega:0.000000} theory {w:0.000000}");
            Assert.Equal(1, r.Modes[n - 1].Omega / w, 8);
            // Rayleigh: φᵀ K φ = ω² for a mass-normalized φ.
            Assert.Equal(1, r.Modes[n - 1].Rayleigh / (w * w), 8);
            Assert.True(r.Modes[n - 1].Residual < 1e-8, $"{r.Modes[n - 1].Residual}");
        }
        Assert.Equal(N * m, r.TotalMass[0], 9);
        Assert.Equal(N * m, r.Modes.Sum(x => x.Mass[0]), 6);
        Assert.Equal(1, r.Cumulative(0), 9);
        Assert.True(r.Reached);
        // The fundamental mode takes most of the mass: Γ1² for the three-storey building (91.4 %).
        Assert.InRange(r.Modes[0].Mass[0] / (N * m), 0.90, 0.93);
    }

    [Fact]
    public void A_cantilever_beam_vibrates_at_1_8751_squared_root_EI_over_mL4()
    {
        const double L = 4, mbar = 0.5;
        const int n = 20;
        var p = new FrameProblem();
        for (var i = 0; i <= n; i++) p.AddNode(new Vec3(L * i / n, 0, 0), $"n{i}", i == 0 ? Fixed : [true, true, false, true, false, true]);
        for (var i = 0; i < n; i++) p.Elements.Add(Bar(i, i + 1, Vec3.UnitZ));
        var mass = new double[n + 1];
        for (var i = 1; i <= n; i++) mass[i] = mbar * L / n * (i == n ? 0.5 : 1);
        var r = Modal.Solve(System(p), mass, limit: 3, initial: 3);
        var w1 = 1.8751 * 1.8751 * Math.Sqrt(E * I / (mbar * Math.Pow(L, 4)));
        var w2 = 4.6941 * 4.6941 * Math.Sqrt(E * I / (mbar * Math.Pow(L, 4)));
        output.WriteLine($"cantilever: ω1 {r.Modes[0].Omega / w1:0.00000}, ω2 {r.Modes[1].Omega / w2:0.00000} of theory");
        Assert.InRange(r.Modes[0].Omega / w1, 0.99, 1.01);
        Assert.InRange(r.Modes[1].Omega / w2, 0.98, 1.02);
        // Mass-normalized: φᵀ M φ = 1.
        var phi = r.Modes[0].Phi;
        Assert.Equal(1, Enumerable.Range(0, n + 1).Sum(i => mass[i] * phi[i * 6 + 2] * phi[i * 6 + 2]), 9);
    }

    [Fact]
    public void A_simply_supported_beam_has_its_first_three_frequencies_n_pi_squared()
    {
        const double L = 6, mbar = 0.8;
        const int n = 40;
        var p = new FrameProblem();
        for (var i = 0; i <= n; i++)
            p.AddNode(new Vec3(L * i / n, 0, 0), $"n{i}", i == 0 ? [true, true, true, true, false, true] : i == n ? [false, true, true, true, false, true] : [true, true, false, true, false, true]);
        for (var i = 0; i < n; i++) p.Elements.Add(Bar(i, i + 1, Vec3.UnitZ));
        var mass = new double[n + 1];
        for (var i = 1; i < n; i++) mass[i] = mbar * L / n;
        var r = Modal.Solve(System(p), mass, limit: 3, initial: 3);
        for (var k = 1; k <= 3; k++)
        {
            var w = Math.Pow(k * Math.PI, 2) * Math.Sqrt(E * I / (mbar * Math.Pow(L, 4)));
            output.WriteLine($"simply supported: ω{k} {r.Modes[k - 1].Omega / w:0.00000} of theory");
            Assert.InRange(r.Modes[k - 1].Omega / w, 0.99, 1.01);
        }
        // The second mode is antisymmetric: no participation along Z.
        Assert.True(Math.Abs(r.Modes[1].Gamma[2]) < 1e-6 * Math.Abs(r.Modes[0].Gamma[2]));
    }

    [Fact]
    public void Jacobi_rotations_diagonalize_a_symmetric_matrix()
    {
        var a = new double[,] { { 4, 1, 2 }, { 1, 3, 0 }, { 2, 0, 5 } };
        var (values, v) = Subspace.Jacobi(a);
        for (var k = 0; k < 3; k++)
            for (var i = 0; i < 3; i++)
            {
                var av = 0.0;
                for (var j = 0; j < 3; j++) av += a[i, j] * v[j, k];
                Assert.Equal(values[k] * v[i, k], av, 10);
            }
        Assert.Equal(12, values.Sum(), 10);
    }
}
