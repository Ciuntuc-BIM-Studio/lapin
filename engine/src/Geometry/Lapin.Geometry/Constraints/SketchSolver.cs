namespace Lapin.Geometry.Constraints;

/// <summary>
/// Lapin's own 2D sketch constraint solver (decision 517, docs/plan-studio-parametric.md §4.3), written from the
/// textbook numerical method: MIT, clean-room.
///
/// <para>The unknowns are the free points' x and y and the free circles' and arcs' radii; every driving constraint
/// gives one or more residual rows with analytic derivatives. The system is split into connected clusters (unknowns
/// joined by some row), each solved on its own by damped least squares (Levenberg–Marquardt) from the current
/// geometry: the step Δ = −Jᵀ(JJᵀ + λI)⁻¹r is the least-norm step that drives the linearised residuals to zero, so
/// what moves moves least. Dragged points start at their goal and weigh more in that norm.</para>
///
/// <para>At the end, the rows of the Jacobian (each scaled to unit length) are taken in order through a Cholesky
/// factorisation of their Gram matrix: a row whose pivot vanishes is dependent on the rows before it (the same verdict
/// as a QR with the columns of Jᵀ pivoted in order), so its constraint is redundant;
/// the rank gives the degrees of freedom left, and an unknown whose unit vector does not lie in the rows' span is
/// still free. A cluster whose residuals cannot reach zero stops at the least-squares point (Jᵀr = 0); the constraints
/// still unsatisfied there are the conflicting ones, and the solve is refused.</para>
///
/// <para>Sided constraints (a point's distance from a line, a circle tangent to a line, two circles tangent outside or
/// inside) keep the side they start on; angles are signed (atan2); a step that would turn a radius negative or carry
/// an arc's sweep through zero is refused. Deterministic: fixed orders throughout, no randomness, no threads.</para>
/// </summary>
public static class SketchSolver
{
    /// <summary>A unit row closer than this to the span of the rows before it depends on them.</summary>
    private const double RankTolerance = 1e-7;
    /// <summary>An unknown whose unit vector is farther than this (squared) from the rows' span is free.</summary>
    private const double FreeTolerance = 1e-8;

    /// <summary>Solves a sketch from its current geometry.</summary>
    /// <exception cref="ArgumentException">A constraint or entity refers to what is not there, or its references do
    /// not fit its kind (see <see cref="ConstraintKind"/>).</exception>
    public static SolveResult Solve(Sketch sketch, SolverOptions? options = null)
    {
        options ??= SolverOptions.Default;
        var sys = new SketchSystem(sketch);
        int np = sys.ParamCount;
        var q = (double[])sys.Start.Clone();
        var weight = new double[np];
        Array.Fill(weight, 1.0);
        var goal = new double[np];
        Array.Fill(goal, double.NaN);
        foreach (var g in options.DragGoals)
        {
            if ((uint)g.Point >= (uint)sys.PointCount) throw new ArgumentException($"Drag goal on point {g.Point}, which does not exist.");
            if (sys.Unknown[2 * g.Point]) { q[2 * g.Point] = goal[2 * g.Point] = g.X; weight[2 * g.Point] = options.DragWeight; }
            if (sys.Unknown[2 * g.Point + 1]) { q[2 * g.Point + 1] = goal[2 * g.Point + 1] = g.Y; weight[2 * g.Point + 1] = options.DragWeight; }
        }

        // Clusters: unknowns joined by a row (union–find over parameters, in item order).
        var parent = new int[np];
        for (int i = 0; i < np; i++) parent[i] = i;
        var itemParams = new List<int>[sys.Items];
        var discover = new RowWriter { ColOf = new int[np] };
        for (int i = 0; i < np; i++) discover.ColOf[i] = sys.Unknown[i] ? i : -1;
        for (int item = 0; item < sys.Items; item++)
        {
            discover.Clear();
            sys.Eval(item, q, discover);
            var ps = discover.Cols.Distinct().ToList();
            ps.Sort();
            itemParams[item] = ps;
            for (int k = 1; k < ps.Count; k++) Union(parent, ps[0], ps[k]);
        }
        var clusterOf = new Dictionary<int, int>();
        var clusters = new List<(List<int> Items, List<int> Params)>();
        var standalone = new List<int>();
        for (int item = 0; item < sys.Items; item++)
        {
            if (itemParams[item].Count == 0) { standalone.Add(item); continue; }
            int root = Find(parent, itemParams[item][0]);
            if (!clusterOf.TryGetValue(root, out int ci)) { ci = clusters.Count; clusterOf[root] = ci; clusters.Add(([], [])); }
            clusters[ci].Items.Add(item);
        }
        var freeParam = new bool[np];
        int dof = 0;
        for (int p = 0; p < np; p++)
        {
            if (!sys.Unknown[p] || IsRadiusOfLine(sys, p)) continue;
            if (clusterOf.TryGetValue(Find(parent, p), out int ci)) clusters[ci].Params.Add(p);
            else { freeParam[p] = true; dof++; } // touched by no constraint
        }

        var redundant = new SortedSet<int>();
        var conflicting = new SortedSet<int>();
        bool notConverged = false;
        int iterations = 0;
        double maxResidual = 0;
        foreach (var (items, ps) in clusters)
        {
            var outcome = SolveCluster(sys, items, ps, q, weight, goal, options);
            iterations += outcome.Iterations;
            maxResidual = Math.Max(maxResidual, outcome.MaxResidual);
            dof += outcome.Dof;
            foreach (int p in outcome.Free) freeParam[p] = true;
            foreach (int c in outcome.Redundant) redundant.Add(c);
            foreach (int c in outcome.Conflicting) conflicting.Add(c);
            notConverged |= outcome.NotConverged;
        }
        // Rows with no unknown at all: between held geometry, either already true (redundant) or false (conflicting).
        var w = new RowWriter { Derivatives = false };
        foreach (int item in standalone)
        {
            int c = sys.ConstraintOf(item);
            if (c < 0) continue;
            w.Clear();
            sys.Eval(item, q, w);
            double m = w.Residual.Max(Math.Abs);
            if (m > options.Tolerance) { conflicting.Add(c); maxResidual = Math.Max(maxResidual, m); }
            else redundant.Add(c);
        }
        foreach (int c in conflicting) redundant.Remove(c);

        var status = conflicting.Count > 0 ? SolveStatus.Conflicting
            : notConverged ? SolveStatus.NotConverged
            : redundant.Count > 0 ? SolveStatus.Redundant
            : dof > 0 ? SolveStatus.Underconstrained
            : SolveStatus.Solved;
        bool succeeded = status is SolveStatus.Solved or SolveStatus.Underconstrained or SolveStatus.Redundant;
        var final = succeeded ? q : sys.Start;

        var points = new SketchPoint[sys.PointCount];
        for (int p = 0; p < points.Length; p++) points[p] = sketch.Points[p] with { X = final[2 * p], Y = final[2 * p + 1] };
        var radii = new double[sketch.Entities.Count];
        for (int e = 0; e < radii.Length; e++) radii[e] = sys.IsCircular(e) ? final[sys.R(e)] : double.NaN;
        var free = new List<SketchUnknown>();
        var pointFree = new bool[sys.PointCount];
        for (int p = 0; p < sys.PointCount; p++)
        {
            if (freeParam[2 * p]) free.Add(new(SketchUnknownKind.PointX, p));
            if (freeParam[2 * p + 1]) free.Add(new(SketchUnknownKind.PointY, p));
            pointFree[p] = freeParam[2 * p] || freeParam[2 * p + 1];
        }
        var entityFree = new bool[sketch.Entities.Count];
        for (int e = 0; e < entityFree.Length; e++)
        {
            if (sys.IsCircular(e) && freeParam[sys.R(e)]) { free.Add(new(SketchUnknownKind.Radius, e)); entityFree[e] = true; }
            foreach (int p in sys.PointsOf(e)) entityFree[e] |= pointFree[p];
        }
        var values = new double[sketch.Constraints.Count];
        for (int c = 0; c < values.Length; c++) values[c] = sys.Measure(c, final);

        return new SolveResult(status, points, radii, dof, free, pointFree, entityFree,
            redundant.ToArray(), conflicting.ToArray(), values, iterations, maxResidual);
    }

    private static bool IsRadiusOfLine(SketchSystem sys, int p) => p >= 2 * sys.PointCount && !sys.IsCircular(p - 2 * sys.PointCount);

    private sealed record ClusterOutcome(int Iterations, double MaxResidual, int Dof, List<int> Free,
        List<int> Redundant, List<int> Conflicting, bool NotConverged);

    /// <summary>
    /// How many times a drag is solved again from its goal: a dragged point weighs DragWeight times more, so each
    /// round leaves it about 1/DragWeight of the way it was pulled back short of where the constraints let it be.
    /// </summary>
    private const int DragRounds = 8;

    private static ClusterOutcome SolveCluster(SketchSystem sys, List<int> items, List<int> ps, double[] q, double[] weight, double[] goal, SolverOptions options)
    {
        int n = ps.Count;
        var w = new RowWriter { ColOf = new int[sys.ParamCount] };
        Array.Fill(w.ColOf, -1);
        for (int j = 0; j < n; j++) w.ColOf[ps[j]] = j;
        var scale = new double[n];
        for (int j = 0; j < n; j++) scale[j] = 1 / Math.Sqrt(weight[ps[j]]);

        // What a step may not do: turn a radius to zero or below, or carry an arc's sweep through zero (its ends
        // passing each other), which would flip the arc inside out.
        var radii = new List<int>();
        var arcs = new List<SketchArc>();
        for (int e = 0; e < sys.Sketch.Entities.Count; e++)
        {
            if (sys.IsCircular(e) && w.ColOf[sys.R(e)] >= 0) radii.Add(sys.R(e));
            if (sys.Sketch.Entities[e] is SketchArc a && (w.ColOf[sys.R(e)] >= 0 || sys.PointsOf(e).Any(p => w.ColOf[2 * p] >= 0 || w.ColOf[2 * p + 1] >= 0)))
                arcs.Add(a);
        }

        var dragged = new List<int>();
        for (int j = 0; j < n; j++) if (!double.IsNaN(goal[ps[j]])) dragged.Add(j);
        var saved = new double[n];
        var sweeps = new double[arcs.Count];
        var probe = new RowWriter { ColOf = w.ColOf, Derivatives = false };
        int it = 0;
        bool converged = Iterate();
        var last = new double[dragged.Count];
        for (int round = 1; converged && round < DragRounds && dragged.Count > 0; round++)
        {
            double moved = 0;
            for (int k = 0; k < dragged.Count; k++)
            {
                int p = ps[dragged[k]];
                if (round > 1) moved = Math.Max(moved, Math.Abs(q[p] - last[k]));
                last[k] = q[p];
            }
            if (round > 1 && moved <= options.Tolerance) break;
            var before = new double[n];
            for (int j = 0; j < n; j++) before[j] = q[ps[j]];
            foreach (int j in dragged) q[ps[j]] = goal[ps[j]];
            if (Iterate()) continue;
            // A later round that fails keeps the round before, which held.
            for (int j = 0; j < n; j++) q[ps[j]] = before[j];
            break;
        }

        // Damped least squares from the current q until the residuals vanish, stall or the iterations run out.
        bool Iterate()
        {
            double f = Evaluate(sys, items, q, w, true);
            int m = w.Rows;
            double lambda = 0;
            int stagnant = 0;
            bool converged = MaxAbs(w.Residual) <= options.Tolerance;
            while (!converged && it < options.MaxIterations)
            {
                it++;
                var r = w.Residual.ToArray();
                bool rowsForm = m <= n;
                var a = NormalMatrix(w, m, n, scale, rowsForm);
                int size = rowsForm ? m : n;
                double maxDiag = 0;
                for (int i = 0; i < size; i++) maxDiag = Math.Max(maxDiag, a[i * size + i]);
                if (maxDiag <= 0) break;
                var rhs = new double[size];
                if (rowsForm) for (int i = 0; i < m; i++) rhs[i] = -r[i];
                else
                    for (int i = 0; i < m; i++)
                        for (int k = w.RowStart[i]; k < w.RowEnd(i); k++) rhs[w.Cols[k]] -= w.Vals[k] * scale[w.Cols[k]] * r[i];
                for (int j = 0; j < n; j++) saved[j] = q[ps[j]];
                for (int k = 0; k < arcs.Count; k++) sweeps[k] = Sweep(arcs[k], q);

                bool accepted = false, stall = false;
                while (true)
                {
                    double damping = lambda + 1e-10 * maxDiag;
                    var y = Cholesky.Solve(a, size, rhs, damping);
                    if (y is null)
                    {
                        lambda = Math.Max(lambda * 10, 1e-8 * maxDiag);
                        if (lambda > 1e10 * maxDiag) { stall = true; break; }
                        continue;
                    }
                    var step = new double[n];
                    if (rowsForm)
                        for (int i = 0; i < m; i++)
                            for (int k = w.RowStart[i]; k < w.RowEnd(i); k++) step[w.Cols[k]] += w.Vals[k] * scale[w.Cols[k]] * y[i];
                    else Array.Copy(y, step, n);
                    for (int j = 0; j < n; j++) q[ps[j]] = saved[j] + scale[j] * step[j];

                    bool allowed = true;
                    foreach (int rp in radii) if (!(q[rp] > 0)) { allowed = false; break; }
                    for (int k = 0; allowed && k < arcs.Count; k++)
                        if (Math.Abs(Sweep(arcs[k], q) - sweeps[k]) > Math.PI) allowed = false;
                    double fNew = allowed ? Evaluate(sys, items, q, probe, false) : double.PositiveInfinity;
                    if (fNew < f)
                    {
                        stagnant = f - fNew <= 1e-14 * f ? stagnant + 1 : 0;
                        f = fNew;
                        lambda = lambda / 10 < 1e-8 * maxDiag ? 0 : lambda / 10;
                        accepted = true;
                        break;
                    }
                    for (int j = 0; j < n; j++) q[ps[j]] = saved[j];
                    lambda = Math.Max(lambda * 10, 1e-6 * maxDiag);
                    if (lambda > 1e10 * maxDiag) { stall = true; break; }
                }
                Evaluate(sys, items, q, w, true);
                converged = MaxAbs(w.Residual) <= options.Tolerance;
                if (stall || !accepted || stagnant >= 3) break;
            }
            return converged;
        }

        // Rank, free unknowns and dependent rows at the end point.
        Evaluate(sys, items, q, w, true);
        var (rank, dependent, free) = Rank(w, n);
        var freeParams = new List<int>();
        for (int j = 0; j < n; j++) if (free[j]) freeParams.Add(ps[j]);
        double maxResidual = MaxAbs(w.Residual);

        var redundant = new List<int>();
        var conflicting = new List<int>();
        bool notConverged = false;
        if (!converged)
        {
            // Stopped short: at a least-squares point (the residual orthogonal to every column) the constraints
            // contradict each other; elsewhere the iterations simply ran out or every step was refused.
            double g2 = 0, jf2 = 0, r2 = 0;
            var g = new double[n];
            for (int i = 0; i < w.Rows; i++)
            {
                r2 += w.Residual[i] * w.Residual[i];
                for (int k = w.RowStart[i]; k < w.RowEnd(i); k++)
                {
                    g[w.Cols[k]] += w.Vals[k] * w.Residual[i];
                    jf2 += w.Vals[k] * w.Vals[k];
                }
            }
            foreach (double v in g) g2 += v * v;
            if (Math.Sqrt(g2) <= 1e-6 * Math.Sqrt(jf2 * r2))
            {
                double threshold = Math.Max(100 * options.Tolerance, 1e-4 * maxResidual);
                for (int i = 0; i < w.Rows; i++)
                {
                    int c = sys.ConstraintOf(w.RowItem[i]);
                    if (c >= 0 && Math.Abs(w.Residual[i]) > threshold && !conflicting.Contains(c)) conflicting.Add(c);
                }
            }
            if (conflicting.Count == 0) notConverged = true;
        }
        for (int i = 0; i < w.Rows; i++)
        {
            int c = sys.ConstraintOf(w.RowItem[i]);
            if (dependent[i] && c >= 0 && !conflicting.Contains(c) && !redundant.Contains(c)) redundant.Add(c);
        }
        return new ClusterOutcome(it, maxResidual, n - rank, freeParams, redundant, conflicting, notConverged);
    }

    /// <summary>Writes the cluster's rows at q; returns the sum of squared residuals.</summary>
    private static double Evaluate(SketchSystem sys, List<int> items, double[] q, RowWriter w, bool derivatives)
    {
        w.Clear();
        w.Derivatives = derivatives;
        foreach (int item in items) sys.Eval(item, q, w);
        double f = 0;
        foreach (double r in w.Residual) f += r * r;
        return f;
    }

    private static double MaxAbs(List<double> v)
    {
        double m = 0;
        foreach (double x in v) m = Math.Max(m, Math.Abs(x));
        return m;
    }

    /// <summary>JJᵀ (m×m, rows form) or JᵀJ (n×n), with J's columns scaled; built from the sparse rows.</summary>
    private static double[] NormalMatrix(RowWriter w, int m, int n, double[] scale, bool rowsForm)
    {
        if (rowsForm)
        {
            // Per column, the rows touching it; every pair of those rows meets in JJᵀ.
            var byCol = new List<(int Row, double Val)>[n];
            for (int j = 0; j < n; j++) byCol[j] = [];
            for (int i = 0; i < m; i++)
                for (int k = w.RowStart[i]; k < w.RowEnd(i); k++) byCol[w.Cols[k]].Add((i, w.Vals[k] * scale[w.Cols[k]]));
            var a = new double[m * m];
            foreach (var col in byCol)
                for (int x = 0; x < col.Count; x++)
                    for (int y = 0; y <= x; y++)
                    {
                        var (ri, vi) = col[x];
                        var (rj, vj) = col[y];
                        // Rows are listed in increasing order, so ri ≥ rj: the lower triangle.
                        a[ri * m + rj] += vi * vj;
                        if (ri != rj) a[rj * m + ri] += vi * vj;
                    }
            return a;
        }
        var b = new double[n * n];
        for (int i = 0; i < m; i++)
        {
            int s = w.RowStart[i], e = w.RowEnd(i);
            for (int x = s; x < e; x++)
                for (int y = s; y < e; y++)
                    b[w.Cols[x] * n + w.Cols[y]] += w.Vals[x] * scale[w.Cols[x]] * w.Vals[y] * scale[w.Cols[y]];
        }
        return b;
    }

    /// <summary>
    /// The rank of J, which rows depend on earlier ones, and which unknowns are free. The rows, scaled to unit length,
    /// are taken in order through a Cholesky factorisation of their Gram matrix G = JJᵀ: a row's pivot² is its squared
    /// distance from the span of the rows before it (what Gram–Schmidt would leave of it), so a pivot near zero marks a
    /// dependent row, which is then left out. Unknown j is fixed by the constraints when its unit vector lies in the
    /// rows' span: the projector's diagonal, ‖L⁻¹Jeⱼ‖² over the independent rows, is 1. Both run inside G's envelope,
    /// narrow for a sketch whose constraints are listed near their neighbours.
    /// </summary>
    private static (int Rank, bool[] Dependent, bool[] Free) Rank(RowWriter w, int n)
    {
        int m = w.Rows;
        var unit = new double[w.Vals.Count];
        var byCol = new List<(int Row, double Val)>[n];
        for (int j = 0; j < n; j++) byCol[j] = [];
        for (int i = 0; i < m; i++)
        {
            double norm = 0;
            for (int k = w.RowStart[i]; k < w.RowEnd(i); k++) norm += w.Vals[k] * w.Vals[k];
            norm = Math.Sqrt(norm);
            for (int k = w.RowStart[i]; k < w.RowEnd(i); k++)
            {
                unit[k] = norm > 0 ? w.Vals[k] / norm : 0;
                byCol[w.Cols[k]].Add((i, unit[k]));
            }
        }
        var g = new double[m * m];
        foreach (var col in byCol)
            for (int x = 0; x < col.Count; x++)
                for (int y = 0; y <= x; y++)
                {
                    var (ri, vi) = col[x];
                    var (rj, vj) = col[y];
                    g[ri * m + rj] += vi * vj;
                }
        var (l, first, dependent) = Cholesky.FactorDropping(g, m, RankTolerance * RankTolerance);
        int rank = 0;
        foreach (bool d in dependent) if (!d) rank++;

        var free = new bool[n];
        var z = new double[m];
        for (int j = 0; j < n; j++)
        {
            int r0 = m;
            foreach (var (row, _) in byCol[j]) r0 = Math.Min(r0, row);
            if (r0 == m) { free[j] = true; continue; }
            Array.Clear(z, r0, m - r0);
            foreach (var (row, val) in byCol[j]) z[row] = val;
            double span = 0;
            for (int i = r0; i < m; i++)
            {
                if (dependent[i]) { z[i] = 0; continue; }
                double s = z[i];
                for (int k = Math.Max(first[i], r0); k < i; k++) s -= l[i * m + k] * z[k];
                z[i] = s / l[i * m + i];
                span += z[i] * z[i];
            }
            free[j] = 1 - span > FreeTolerance;
        }
        return (rank, dependent, free);
    }

    /// <summary>An arc's sweep from start to end, counter-clockwise, in [0, 2π).</summary>
    private static double Sweep(SketchArc a, double[] q)
    {
        double sx = q[2 * a.Start] - q[2 * a.Center], sy = q[2 * a.Start + 1] - q[2 * a.Center + 1];
        double ex = q[2 * a.End] - q[2 * a.Center], ey = q[2 * a.End + 1] - q[2 * a.Center + 1];
        double t = Math.Atan2(sx * ey - sy * ex, sx * ex + sy * ey);
        return t < 0 ? t + 2 * Math.PI : t;
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
        return i;
    }

    private static void Union(int[] parent, int a, int b)
    {
        a = Find(parent, a);
        b = Find(parent, b);
        if (a == b) return;
        if (a < b) parent[b] = a; else parent[a] = b;
    }

    /// <summary>
    /// A diagnostic: the largest difference between the analytic derivatives of every driving row and central
    /// differences, at the sketch's geometry (for tests of the residual forms).
    /// </summary>
    public static double JacobianError(Sketch sketch, double h = 1e-7)
    {
        var sys = new SketchSystem(sketch);
        var q = (double[])sys.Start.Clone();
        var w = new RowWriter { ColOf = new int[sys.ParamCount] };
        for (int i = 0; i < sys.ParamCount; i++) w.ColOf[i] = sys.Unknown[i] ? i : -1;
        var plus = new RowWriter { ColOf = w.ColOf, Derivatives = false };
        var minus = new RowWriter { ColOf = w.ColOf, Derivatives = false };
        double worst = 0;
        for (int item = 0; item < sys.Items; item++)
        {
            w.Clear();
            sys.Eval(item, q, w);
            for (int p = 0; p < sys.ParamCount; p++)
            {
                if (w.ColOf[p] < 0) continue;
                double keep = q[p];
                q[p] = keep + h; plus.Clear(); sys.Eval(item, q, plus);
                q[p] = keep - h; minus.Clear(); sys.Eval(item, q, minus);
                q[p] = keep;
                for (int i = 0; i < w.Rows; i++)
                {
                    double numeric = SketchSystem.Wrap(plus.Residual[i] - minus.Residual[i]) / (2 * h);
                    double analytic = 0;
                    for (int k = w.RowStart[i]; k < w.RowEnd(i); k++) if (w.Cols[k] == p) analytic = w.Vals[k];
                    worst = Math.Max(worst, Math.Abs(numeric - analytic));
                }
            }
        }
        return worst;
    }
}

/// <summary>
/// Cholesky factorisation of a dense symmetric matrix with a shift (A + δI), skipping each row's leading zeros: the
/// envelope of a sketch's normal matrix is narrow when its constraints are listed near their neighbours, and fill-in
/// never leaves it.
/// </summary>
internal static class Cholesky
{
    /// <summary>
    /// Factors a symmetric positive semi-definite A (lower triangle read) as LLᵀ in order, leaving out each row whose
    /// pivot² is at most <paramref name="dropBelow"/> (dependent on the rows before it): its row and column of L are zero.
    /// </summary>
    public static (double[] L, int[] First, bool[] Dropped) FactorDropping(double[] a, int n, double dropBelow)
    {
        var l = new double[n * n];
        var first = new int[n];
        var dropped = new bool[n];
        for (int i = 0; i < n; i++)
        {
            int f = 0;
            while (f < i && a[i * n + f] == 0) f++;
            first[i] = f;
        }
        for (int i = 0; i < n; i++)
        {
            int fi = first[i], ri = i * n;
            for (int j = fi; j <= i; j++)
            {
                if (j < i && dropped[j]) continue;
                double s = a[ri + j];
                int rj = j * n;
                for (int k = Math.Max(fi, first[j]); k < j; k++) s -= l[ri + k] * l[rj + k];
                if (j < i) l[ri + j] = s / l[rj + j];
                else if (s > dropBelow) l[ri + i] = Math.Sqrt(s);
                else
                {
                    dropped[i] = true;
                    Array.Clear(l, ri, i + 1);
                }
            }
        }
        return (l, first, dropped);
    }

    /// <summary>Solves (A + δI)x = b, or null when the shifted matrix is not positive definite.</summary>
    public static double[]? Solve(double[] a, int n, double[] b, double shift)
    {
        var l = new double[n * n];
        var first = new int[n];
        for (int i = 0; i < n; i++)
        {
            int f = 0;
            while (f < i && a[i * n + f] == 0) f++;
            first[i] = f;
        }
        for (int i = 0; i < n; i++)
        {
            int fi = first[i];
            for (int j = fi; j <= i; j++)
            {
                double s = a[i * n + j] + (i == j ? shift : 0);
                int k0 = Math.Max(fi, first[j]);
                int ri = i * n, rj = j * n;
                for (int k = k0; k < j; k++) s -= l[ri + k] * l[rj + k];
                if (j < i) l[ri + j] = s / l[rj + j];
                else
                {
                    if (!(s > 0)) return null;
                    l[ri + i] = Math.Sqrt(s);
                }
            }
        }
        var z = (double[])b.Clone();
        for (int i = 0; i < n; i++)
        {
            double s = z[i];
            for (int k = first[i]; k < i; k++) s -= l[i * n + k] * z[k];
            z[i] = s / l[i * n + i];
        }
        for (int i = n - 1; i >= 0; i--)
        {
            z[i] /= l[i * n + i];
            for (int k = first[i]; k < i; k++) z[k] -= l[i * n + k] * z[i];
        }
        return z;
    }
}
