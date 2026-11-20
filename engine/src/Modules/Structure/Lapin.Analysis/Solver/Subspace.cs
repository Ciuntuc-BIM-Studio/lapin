namespace Lapin.Analysis.Solver;

/// <summary>The lowest eigenpairs of K φ = λ M φ: λ = ω² ascending, φ by equation, normalized to the mass (φᵀ M φ = 1).</summary>
public sealed record EigenResult(double[] Lambda, double[][] Vectors, int Iterations, bool Converged);

/// <summary>
/// The generalized symmetric eigenproblem K φ = ω² M φ with a lumped (diagonal) mass by subspace iteration (Bathe and Wilson,
/// "Large eigenvalue problems in dynamic analysis", J. Eng. Mech. Div. ASCE 98, 1972; Bathe, "Finite Element Procedures",
/// §11.6): q = min(2p, p + 8) vectors (the mass' diagonal, unit vectors where mᵢ/kᵢᵢ is largest, one mixed vector); each step
/// solves K X̄ = M X with K's factor, projects K and M onto it (Kr = X̄ᵀ K X̄ = X̄ᵀ M X, Mr = X̄ᵀ M X̄), turns the small
/// problem into a standard one by Mr's Cholesky factor (a vector its pivot lost is dropped) and solves it by cyclic Jacobi
/// rotations; X = X̄ L⁻ᵀ Z, normalized to M. It
/// stops when the first p eigenvalues change by less than the tolerance. Degrees of freedom without mass stay in K: the
/// iteration condenses them. Written for Lapin from those descriptions; no code from any library.
/// </summary>
public static class Subspace
{
    /// <param name="solve">Solves K·x = b in place (by equation).</param>
    /// <param name="mass">M's diagonal by equation (t; nought where there is none).</param>
    /// <param name="kDiag">K's diagonal by equation, to choose the starting vectors.</param>
    /// <param name="start">Vectors to start from (a smaller run's modes, when more are asked): they take the first places.</param>
    public static EigenResult Solve(Action<double[]> solve, double[] mass, double[] kDiag, int p, int maxIterations = 200, double tolerance = 1e-8, IReadOnlyList<double[]>? start = null)
    {
        var n = mass.Length;
        var withMass = Enumerable.Range(0, n).Where(i => mass[i] > 0).ToList();
        p = Math.Min(p, withMass.Count);
        if (p <= 0) return new EigenResult([], [], 0, true);
        var q = Math.Min(Math.Min(2 * p, p + 8), withMass.Count);
        // The starting vectors.
        var x = new double[q][];
        x[0] = (double[])mass.Clone();
        var ratios = withMass.OrderByDescending(i => mass[i] / Math.Max(kDiag[i], 1e-300)).ToList();
        var given = start?.Count ?? 0;
        for (var j = 1; j < q; j++)
        {
            if (j - 1 < given && j < q - 1) { x[j] = (double[])start![j - 1].Clone(); continue; }
            x[j] = new double[n];
            if (j < q - 1 || q < 3) x[j][ratios[(j - 1 - Math.Min(given, q - 2)) % ratios.Count]] = 1;
            else
            {
                // A mixed vector: every massive degree of freedom, its sign by a fixed sequence (Bathe's random vector, repeatable).
                var seed = 12345u;
                foreach (var i in withMass) { seed = seed * 1664525u + 1013904223u; x[j][i] = (seed >> 16) % 2 == 0 ? 1 : -1; }
            }
        }
        var previous = new double[p];
        Array.Fill(previous, double.NaN);
        double[] lambda = [];
        double[][] vectors = [];
        var it = 0;
        var converged = false;
        while (it < maxIterations)
        {
            it++;
            // Y = M X; X̄ = K⁻¹ Y.
            var y = new double[q][];
            var xb = new double[q][];
            for (var j = 0; j < q; j++)
            {
                y[j] = new double[n];
                for (var i = 0; i < n; i++) y[j][i] = mass[i] * x[j][i];
                xb[j] = (double[])y[j].Clone();
                solve(xb[j]);
            }
            // The projections: Kr = X̄ᵀ K X̄ = X̄ᵀ Y, Mr = X̄ᵀ M X̄ (over the massive degrees of freedom only).
            var kr0 = new double[q, q];
            var mr0 = new double[q, q];
            for (var a = 0; a < q; a++)
                for (var b = a; b < q; b++)
                {
                    double sk = 0, sm = 0;
                    var (va, ya, vb, yb) = (xb[a], y[a], xb[b], y[b]);
                    for (var i = 0; i < n; i++) sk += va[i] * yb[i] + vb[i] * ya[i];
                    foreach (var i in withMass) sm += va[i] * mass[i] * vb[i];
                    kr0[a, b] = kr0[b, a] = sk / 2;
                    mr0[a, b] = mr0[b, a] = sm;
                }
            // Mr = L Lᵀ (a vector dependent on the others, its pivot lost, is dropped); the standard problem L⁻¹ Kr L⁻ᵀ z = λ z.
            var keep = new List<int>();
            double[,] l = new double[q, q];
            var maxDiag = Enumerable.Range(0, q).Max(i => mr0[i, i]);
            for (var j = 0; j < q; j++)
            {
                var c = keep.Count;
                var row = new double[c + 1];
                for (var k = 0; k < c; k++)
                {
                    var sum = mr0[j, keep[k]];
                    for (var t = 0; t < k; t++) sum -= row[t] * l[k, t];
                    row[k] = sum / l[k, k];
                }
                var d = mr0[j, j];
                for (var t = 0; t < c; t++) d -= row[t] * row[t];
                if (!(d > 1e-12 * maxDiag)) continue;
                for (var t = 0; t < c; t++) l[c, t] = row[t];
                l[c, c] = Math.Sqrt(d);
                keep.Add(j);
            }
            var m = keep.Count;
            // A = L⁻¹ Kr L⁻ᵀ: first B = L⁻¹ Kr (forward substitution on each column), then A = B L⁻ᵀ.
            var bm = new double[m, m];
            for (var col = 0; col < m; col++)
                for (var r = 0; r < m; r++)
                {
                    var sum = kr0[keep[r], keep[col]];
                    for (var t = 0; t < r; t++) sum -= l[r, t] * bm[t, col];
                    bm[r, col] = sum / l[r, r];
                }
            var kr = new double[m, m];
            for (var r = 0; r < m; r++)
                for (var col = 0; col < m; col++)
                {
                    var sum = bm[r, col];
                    for (var t = 0; t < col; t++) sum -= kr[r, t] * l[col, t];
                    kr[r, col] = sum / l[col, col];
                }
            for (var r = 0; r < m; r++) for (var col = r + 1; col < m; col++) kr[r, col] = kr[col, r] = (kr[r, col] + kr[col, r]) / 2;
            var (values, z) = Jacobi(kr);
            var order = Enumerable.Range(0, m).OrderBy(i => values[i]).ToArray();
            lambda = order.Select(i => values[i]).ToArray();
            // Q = L⁻ᵀ Z (back substitution), X = X̄ Q: M-orthonormal.
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
                var dst = nx[c];
                for (var a = 0; a < m; a++)
                {
                    var w = qm[a, col];
                    if (w == 0) continue;
                    var v = xb[keep[a]];
                    for (var i = 0; i < n; i++) dst[i] += w * v[i];
                }
            }
            vectors = nx;
            var pp = Math.Min(p, m);
            var done = true;
            for (var i = 0; i < pp; i++)
            {
                if (!(Math.Abs(lambda[i] - previous[i]) <= tolerance * Math.Abs(lambda[i]))) done = false;
                previous[i] = lambda[i];
            }
            if (done && it > 1) { converged = true; break; }
            // The next vectors; fill up with the starting ones if some were dropped.
            x = new double[q][];
            for (var c = 0; c < q; c++) x[c] = c < m ? nx[c] : x0(c);
        }
        var count = Math.Min(p, lambda.Length);
        return new EigenResult(lambda.Take(count).ToArray(), vectors.Take(count).ToArray(), it, converged);

        double[] x0(int c)
        {
            var v = new double[n];
            v[ratios[c % ratios.Count]] = 1;
            return v;
        }
    }

    /// <summary>The eigenvalues and eigenvectors (columns) of a small symmetric matrix by cyclic Jacobi rotations (Golub and Van Loan, "Matrix Computations", §8.5).</summary>
    public static (double[] Values, double[,] Vectors) Jacobi(double[,] a0)
    {
        var n = a0.GetLength(0);
        var a = (double[,])a0.Clone();
        var v = new double[n, n];
        for (var i = 0; i < n; i++) v[i, i] = 1;
        for (var sweep = 0; sweep < 100; sweep++)
        {
            double off = 0, diag = 0;
            for (var i = 0; i < n; i++) { diag += a[i, i] * a[i, i]; for (var j = i + 1; j < n; j++) off += a[i, j] * a[i, j]; }
            if (off <= 1e-30 * diag || off == 0) break;
            for (var pi = 0; pi < n - 1; pi++)
                for (var qi = pi + 1; qi < n; qi++)
                {
                    if (Math.Abs(a[pi, qi]) < 1e-300) continue;
                    var theta = (a[qi, qi] - a[pi, pi]) / (2 * a[pi, qi]);
                    var t = Math.Sign(theta == 0 ? 1 : theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    var c = 1 / Math.Sqrt(t * t + 1);
                    var s = t * c;
                    for (var k = 0; k < n; k++)
                    {
                        var (akp, akq) = (a[k, pi], a[k, qi]);
                        a[k, pi] = c * akp - s * akq;
                        a[k, qi] = s * akp + c * akq;
                    }
                    for (var k = 0; k < n; k++)
                    {
                        var (apk, aqk) = (a[pi, k], a[qi, k]);
                        a[pi, k] = c * apk - s * aqk;
                        a[qi, k] = s * apk + c * aqk;
                    }
                    for (var k = 0; k < n; k++)
                    {
                        var (vkp, vkq) = (v[k, pi], v[k, qi]);
                        v[k, pi] = c * vkp - s * vkq;
                        v[k, qi] = s * vkp + c * vkq;
                    }
                }
        }
        var values = new double[n];
        for (var i = 0; i < n; i++) values[i] = a[i, i];
        return (values, v);
    }
}
