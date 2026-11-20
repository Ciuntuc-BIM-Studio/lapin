using Lapin.Analysis.Solver;

namespace Lapin.Analysis.Seismic;

/// <summary>
/// One natural mode (docs/plan-fem.md §11.2): ω (rad/s), its shape φ on the nodes (node × 6, normalized to the mass:
/// φᵀ M φ = 1), the participation factors Γ = φᵀ M r along X, Y, Z (r the direction's unit translations) and the effective
/// masses Γ² (t); the Rayleigh quotient φᵀ K φ and the residual ‖K φ − ω² M φ‖ / ‖K φ‖ as its checks.
/// </summary>
public sealed record Mode(int N, double Omega, double[] Phi, double[] Gamma, double[] Mass, double Rayleigh, double Residual)
{
    public double T => 2 * Math.PI / Omega;
    public double F => Omega / (2 * Math.PI);
}

/// <summary>The modes found, the masses they were found for (t, along X, Y, Z; on the free nodes) and what fell on the supports.</summary>
public sealed record ModalResult(List<Mode> Modes, double[] TotalMass, double SupportMass, int Iterations, bool Converged, bool Reached, int MassDofs)
{
    /// <summary>The effective masses' share of the total, cumulative over the modes, along each direction.</summary>
    public double Cumulative(int d) => TotalMass[d] > 0 ? Modes.Sum(m => m.Mass[d]) / TotalMass[d] : 0;
}

/// <summary>
/// The modal analysis (§11.2) on an assembled, factored system: the lumped mass on the nodes' translations, K φ = ω² M φ by
/// subspace iteration (<see cref="Subspace"/>), the number of modes grown (12, 24, 48 …) until the effective masses reach
/// the target share of the total along both horizontal directions (P100-1/2013 §4.5.3.3.1, EN 1998-1 §4.3.3.3.1: 90 %), or
/// the limit.
/// </summary>
public static class Modal
{
    public const double Target = 0.9;

    /// <param name="nodeMass">Each node's mass (t), the same along X, Y and Z.</param>
    public static ModalResult Solve(FrameSystem sys, double[] nodeMass, int limit = 60, int initial = 12, double target = Target)
    {
        var p = sys.P;
        var nn = p.Nodes.Count;
        var mass = new double[sys.Neq];
        var total = new double[3];
        var support = 0.0;
        var r = new double[3][];
        for (var d = 0; d < 3; d++) r[d] = new double[sys.Neq];
        for (var n = 0; n < nn; n++)
        {
            var m = nodeMass[n];
            if (m <= 0) continue;
            var any = false;
            for (var d = 0; d < 3; d++)
            {
                var e = sys.Eq[n * 6 + d];
                if (e < 0) continue;
                mass[e] = m;
                r[d][e] = 1;
                total[d] += m;
                any = true;
            }
            if (!any || sys.Eq[n * 6] < 0 && sys.Eq[n * 6 + 1] < 0) support += m;
        }
        var massDofs = mass.Count(x => x > 0);
        var kd = sys.Diagonal();
        limit = Math.Clamp(limit, 1, 300);
        var want = Math.Min(initial, limit);
        EigenResult eig;
        double[][]? previous = null;
        var iterations = 0;
        while (true)
        {
            // Grown: from the modes found so far (Bathe's subspace, warm started), the rest new.
            eig = Subspace.Solve(sys.SolveInPlace, mass, kd, want, start: previous);
            previous = eig.Vectors;
            iterations += eig.Iterations;
            var cum = new double[2];
            foreach (var v in eig.Vectors)
                for (var d = 0; d < 2; d++)
                {
                    var g = 0.0;
                    for (var i = 0; i < v.Length; i++) g += v[i] * mass[i] * r[d][i];
                    cum[d] += g * g;
                }
            var reached = Enumerable.Range(0, 2).All(d => total[d] <= 0 || cum[d] >= target * total[d] - 1e-12);
            if (reached || want >= limit || eig.Lambda.Length < want || want >= massDofs) break;
            want = Math.Min(2 * want, limit);
        }
        var modes = new List<Mode>();
        for (var k = 0; k < eig.Lambda.Length; k++)
        {
            var v = eig.Vectors[k];
            var lambda = Math.Max(eig.Lambda[k], 1e-300);
            var gamma = new double[3];
            for (var d = 0; d < 3; d++) { var g = 0.0; for (var i = 0; i < v.Length; i++) g += v[i] * mass[i] * r[d][i]; gamma[d] = g; }
            var phi = sys.ToGlobal(v);
            var kphi = sys.Multiply(phi);
            double rq = 0, res = 0, kn = 0;
            for (var i = 0; i < phi.Length; i++)
            {
                if (sys.Eq[i] < 0) continue;
                rq += phi[i] * kphi[i];
                var x = kphi[i] - lambda * mass[sys.Eq[i]] * phi[i];
                res += x * x;
                kn += kphi[i] * kphi[i];
            }
            modes.Add(new Mode(k + 1, Math.Sqrt(lambda), phi, gamma, gamma.Select(g => g * g).ToArray(), rq, kn > 0 ? Math.Sqrt(res / kn) : 0));
        }
        var result = new ModalResult(modes, total, support, iterations, eig.Converged, false, massDofs);
        var ok = Enumerable.Range(0, 2).All(d => total[d] <= 0 || result.Cumulative(d) >= target - 1e-9);
        return result with { Reached = ok };
    }
}
