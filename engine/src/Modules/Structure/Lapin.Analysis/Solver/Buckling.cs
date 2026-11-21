namespace Lapin.Analysis.Solver;

/// <summary>
/// The critical load factors of a load vector (§14.3), ascending: (K + αcr·Kg(N)) φ = 0; each mode's shape on the nodes
/// (global, node × 6), scaled to a largest translation of 1. <see cref="Compressed"/> is false when no bar is in compression
/// (no buckling: <see cref="Alpha"/> empty).
/// </summary>
public sealed record BucklingResult(bool Ok, double[] Alpha, double[][] Modes, int Iterations, bool Converged, bool Compressed, string? Message = null);

/// <summary>
/// Linear buckling (docs/plan-fem.md §14.3): the generalized eigenproblem G φ = μ K φ, G = −Kg(N) the bars' geometric
/// stiffness of their axial forces, K positive definite, by subspace iteration on K's factor (Bathe and Wilson 1972; Bathe,
/// "Finite Element Procedures", §11.6, as <see cref="Subspace"/>): X̄ = K⁻¹ G X, the projections Gr = X̄ᵀ G X̄ and
/// Kr = X̄ᵀ K X̄ = X̄ᵀ G X, the small problem by Kr's Cholesky factor and Jacobi rotations; the largest μ > 0 give
/// αcr = 1/μ. Written for Lapin from those descriptions.
/// </summary>
public static class Buckling
{
    /// <summary>The load vector solved to first order on the problem, then its buckling.</summary>
    public static BucklingResult Of(FrameProblem p, CaseLoads loads, int modes = 3)
    {
        var (sys, fail) = FrameSystem.Build(p);
        if (sys == null) return new BucklingResult(false, [], [], 0, false, false, fail?.Refused ?? "A mechanism: the problem has no stiffness to buckle.");
        var r = sys.SolveCase(loads);
        return Solve(sys, r.Elements.Select(Nonlinear.AxialOf).ToArray(), modes);
    }

    /// <param name="sys">The assembled, factored first-order problem (its inactive bars take no part).</param>
    /// <param name="axial">Each bar's axial force (kN, tension positive).</param>
    public static BucklingResult Solve(FrameSystem sys, double[] axial, int p = 3, int maxIterations = 120, double tolerance = 1e-8)
    {
        var prob = sys.P;
        var n = sys.Neq;
        var eq = sys.Eq;
        // Each bar's G = −Kg(N) on its nodes, global.
        var mats = new List<(int[] Dofs, double[] G)>();
        var maxCompression = 0.0;
        for (var k = 0; k < prob.Elements.Count; k++)
        {
            var e = prob.Elements[k];
            if (e.Inactive || Math.Abs(axial[k]) < 1e-12) continue;
            maxCompression = Math.Max(maxCompression, -axial[k]);
            var g = e.GeometricGlobal(axial[k]);
            for (var t = 0; t < 144; t++) g[t] = -g[t];
            var dofs = new int[12];
            for (var d = 0; d < 6; d++) { dofs[d] = eq[e.I * 6 + d]; dofs[6 + d] = eq[e.J * 6 + d]; }
            mats.Add((dofs, g));
        }
        if (maxCompression <= 1e-9) return new BucklingResult(true, [], [], 0, true, false);
        double[] MulG(double[] x)
        {
            var y = new double[n];
            foreach (var (ds, g) in mats)
                for (var r = 0; r < 12; r++)
                {
                    if (ds[r] < 0) continue;
                    var t = 0.0;
                    for (var c = 0; c < 12; c++) if (ds[c] >= 0) t += g[r * 12 + c] * x[ds[c]];
                    y[ds[r]] += t;
                }
            return y;
        }
        var gDiag = new double[n];
        foreach (var (ds, g) in mats) for (var r = 0; r < 12; r++) if (ds[r] >= 0) gDiag[ds[r]] += g[r * 12 + r];
        var kDiag = sys.Diagonal();
        var candidates = Enumerable.Range(0, n).Where(i => gDiag[i] > 0).OrderByDescending(i => gDiag[i] / Math.Max(kDiag[i], 1e-300)).ToList();
        if (candidates.Count == 0) candidates = Enumerable.Range(0, n).Where(i => gDiag[i] != 0).ToList();
        p = Math.Max(1, Math.Min(p, candidates.Count));
        var q = Math.Min(Math.Min(2 * p, p + 8), Math.Max(candidates.Count, 1));
        q = Math.Max(q, Math.Min(p + 1, n));

        double[] Unit(int c)
        {
            var v = new double[n];
            v[candidates[c % candidates.Count]] = 1;
            return v;
        }
        var x = new double[q][];
        x[0] = gDiag.Select(v => Math.Max(v, 0)).ToArray();
        for (var j = 1; j < q; j++)
        {
            if (j < q - 1 || q < 3) { x[j] = Unit(j - 1); continue; }
            x[j] = new double[n];
            var seed = 12345u;
            foreach (var i in candidates) { seed = seed * 1664525u + 1013904223u; x[j][i] = (seed >> 16) % 2 == 0 ? 1 : -1; }
        }
        var previous = Enumerable.Repeat(double.NaN, p).ToArray();
        double[] mu = [];
        double[][] vectors = [];
        var it = 0;
        var converged = false;
        while (it < maxIterations)
        {
            it++;
            var y = new double[q][];
            var xb = new double[q][];
            var gy = new double[q][];
            for (var j = 0; j < q; j++)
            {
                y[j] = MulG(x[j]);
                xb[j] = (double[])y[j].Clone();
                sys.SolveInPlace(xb[j]);
                gy[j] = MulG(xb[j]);
            }
            var kr0 = new double[q, q];
            var gr0 = new double[q, q];
            for (var a = 0; a < q; a++)
                for (var b = a; b < q; b++)
                {
                    double sk = 0, sg = 0;
                    for (var i = 0; i < n; i++) { sk += xb[a][i] * y[b][i] + xb[b][i] * y[a][i]; sg += xb[a][i] * gy[b][i] + xb[b][i] * gy[a][i]; }
                    kr0[a, b] = kr0[b, a] = sk / 2;
                    gr0[a, b] = gr0[b, a] = sg / 2;
                }
            // Kr = L Lᵀ, a dependent vector dropped; the standard problem L⁻¹ Gr L⁻ᵀ z = μ z.
            var keep = new List<int>();
            var l = new double[q, q];
            var maxDiag = Enumerable.Range(0, q).Max(i => kr0[i, i]);
            if (!(maxDiag > 0)) return new BucklingResult(true, [], [], it, true, true, "No buckling mode: the compressed bars are held by what is round them.");
            for (var j = 0; j < q; j++)
            {
                var c = keep.Count;
                var row = new double[c + 1];
                for (var k = 0; k < c; k++)
                {
                    var sum = kr0[j, keep[k]];
                    for (var t = 0; t < k; t++) sum -= row[t] * l[k, t];
                    row[k] = sum / l[k, k];
                }
                var d = kr0[j, j];
                for (var t = 0; t < c; t++) d -= row[t] * row[t];
                if (!(d > 1e-12 * maxDiag)) continue;
                for (var t = 0; t < c; t++) l[c, t] = row[t];
                l[c, c] = Math.Sqrt(d);
                keep.Add(j);
            }
            var m = keep.Count;
            var bm = new double[m, m];
            for (var col = 0; col < m; col++)
                for (var r = 0; r < m; r++)
                {
                    var sum = gr0[keep[r], keep[col]];
                    for (var t = 0; t < r; t++) sum -= l[r, t] * bm[t, col];
                    bm[r, col] = sum / l[r, r];
                }
            var cm = new double[m, m];
            for (var r = 0; r < m; r++)
                for (var col = 0; col < m; col++)
                {
                    var sum = bm[r, col];
                    for (var t = 0; t < col; t++) sum -= cm[r, t] * l[col, t];
                    cm[r, col] = sum / l[col, col];
                }
            for (var r = 0; r < m; r++) for (var col = r + 1; col < m; col++) cm[r, col] = cm[col, r] = (cm[r, col] + cm[col, r]) / 2;
            var (values, z) = Subspace.Jacobi(cm);
            var order = Enumerable.Range(0, m).OrderByDescending(i => values[i]).ToArray();
            mu = order.Select(i => values[i]).ToArray();
            var qm = new double[m, m];
            for (var col = 0; col < m; col++)
                for (var r = m - 1; r >= 0; r--)
                {
                    var sum = z[r, col];
                    for (var t = r + 1; t < m; t++) sum -= l[t, r] * qm[t, col];
                    qm[r, col] = sum / l[r, r];
                }
            var nx = new double[m][];
            for (var c = 0; c < m; c++)
            {
                nx[c] = new double[n];
                var col = order[c];
                for (var a = 0; a < m; a++)
                {
                    var w = qm[a, col];
                    if (w == 0) continue;
                    var v = xb[keep[a]];
                    for (var i = 0; i < n; i++) nx[c][i] += w * v[i];
                }
            }
            vectors = nx;
            var positive = mu.TakeWhile(v => v > 0).Count();
            var pp = Math.Min(p, positive);
            var done = pp > 0;
            for (var i = 0; i < pp; i++)
            {
                if (!(Math.Abs(mu[i] - previous[i]) <= tolerance * Math.Abs(mu[i]))) done = false;
                previous[i] = mu[i];
            }
            if (done && it > 1) { converged = true; break; }
            if (positive == 0 && it > 3) { converged = true; break; }
            x = new double[q][];
            for (var c = 0; c < q; c++) x[c] = c < m ? nx[c] : Unit(c);
        }
        var count = Math.Min(p, mu.TakeWhile(v => v > 0).Count());
        var alpha = mu.Take(count).Select(v => 1 / v).ToArray();
        var shapes = vectors.Take(count).Select(v =>
        {
            var u = sys.ToGlobal(v);
            var top = 0.0;
            var at = 0;
            for (var i = 0; i < u.Length; i += 6)
            {
                var t = Math.Sqrt(u[i] * u[i] + u[i + 1] * u[i + 1] + u[i + 2] * u[i + 2]);
                if (t > top) (top, at) = (t, i);
            }
            // Its largest translation 1, the sign so that it is positive along its own largest component.
            var big = Enumerable.Range(0, 3).MaxBy(d => Math.Abs(u[at + d]));
            var s = top > 0 ? Math.Sign(u[at + big]) / top : 1;
            for (var i = 0; i < u.Length; i++) u[i] *= s;
            return u;
        }).ToArray();
        return new BucklingResult(true, alpha, shapes, it, converged, true, count == 0 ? "No buckling under these loads: the geometric stiffness only stiffens." : null);
    }
}
