namespace Lapin.Geometry;

/// <summary>
/// A point in homogeneous coordinates (w·x, w·y, w·z, w): what the rational algorithms of a NURBS work on, so a rational curve
/// or surface is a plain B-spline one dimension up (Piegl &amp; Tiller, <i>The NURBS Book</i>, 2nd ed., §4.2).
/// </summary>
public readonly record struct Vec4(double X, double Y, double Z, double W)
{
    public static Vec4 Of(Vec3 p, double w) => new(p.X * w, p.Y * w, p.Z * w, w);
    public Vec3 Xyz => new(X, Y, Z);
    public Vec3 Point => new(X / W, Y / W, Z / W);
    public static Vec4 operator +(Vec4 a, Vec4 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
    public static Vec4 operator -(Vec4 a, Vec4 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);
    public static Vec4 operator *(Vec4 a, double s) => new(a.X * s, a.Y * s, a.Z * s, a.W * s);
    public static Vec4 operator *(double s, Vec4 a) => a * s;
    public static readonly Vec4 Zero = new(0, 0, 0, 0);
}

/// <summary>
/// The algorithms of B-spline basis functions, after Piegl &amp; Tiller, <i>The NURBS Book</i> (2nd ed., Springer 1997), chapter 2:
/// the knot span (A2.1), the non-zero basis functions (A2.2) and their derivatives (A2.3), with the helpers knot vectors need.
/// A knot vector U has m + 1 = n + p + 2 knots for n + 1 control points of degree p; the curve lives on [U[p], U[n + 1]].
/// Public mathematics only.
/// </summary>
public static class BSpline
{
    /// <summary>
    /// A2.1: the span i with U[i] ≤ u &lt; U[i + 1] (the last non-empty one at the domain's end), <paramref name="n"/> the last control
    /// point's index. Outside the domain, the first or last span.
    /// </summary>
    public static int FindSpan(int n, int p, double u, IReadOnlyList<double> U)
    {
        if (u >= U[n + 1]) return LastSpan(n, U);
        if (u <= U[p]) return FirstSpan(p, U);
        int low = p, high = n + 1, mid = (low + high) / 2;
        while (u < U[mid] || u >= U[mid + 1])
        {
            if (u < U[mid]) high = mid; else low = mid;
            mid = (low + high) / 2;
        }
        return mid;
    }

    static int LastSpan(int n, IReadOnlyList<double> U)
    {
        var i = n;
        while (i > 0 && U[i] >= U[n + 1]) i--;
        return i;
    }

    static int FirstSpan(int p, IReadOnlyList<double> U)
    {
        var i = p;
        while (i + 1 < U.Count - 1 && U[i + 1] <= U[p]) i++;
        return i;
    }

    /// <summary>A2.2: the p + 1 basis functions N[i−p, p](u) … N[i, p](u) that are not zero in span <paramref name="i"/>.</summary>
    public static double[] BasisFuns(int i, double u, int p, IReadOnlyList<double> U)
    {
        var N = new double[p + 1];
        var left = new double[p + 1];
        var right = new double[p + 1];
        N[0] = 1;
        for (var j = 1; j <= p; j++)
        {
            left[j] = u - U[i + 1 - j];
            right[j] = U[i + j] - u;
            var saved = 0.0;
            for (var r = 0; r < j; r++)
            {
                var den = right[r + 1] + left[j - r];
                var temp = den == 0 ? 0 : N[r] / den;
                N[r] = saved + right[r + 1] * temp;
                saved = left[j - r] * temp;
            }
            N[j] = saved;
        }
        return N;
    }

    /// <summary>
    /// A2.3: the non-zero basis functions and their derivatives up to <paramref name="d"/> in span <paramref name="i"/>:
    /// ders[k, j] is the k-th derivative of N[i−p+j, p] at u. Derivatives above p are zero.
    /// </summary>
    public static double[,] DersBasisFuns(int i, double u, int p, int d, IReadOnlyList<double> U)
    {
        var ders = new double[d + 1, p + 1];
        var ndu = new double[p + 1, p + 1];
        var left = new double[p + 1];
        var right = new double[p + 1];
        ndu[0, 0] = 1;
        for (var j = 1; j <= p; j++)
        {
            left[j] = u - U[i + 1 - j];
            right[j] = U[i + j] - u;
            var saved = 0.0;
            for (var r = 0; r < j; r++)
            {
                ndu[j, r] = right[r + 1] + left[j - r];
                var temp = ndu[j, r] == 0 ? 0 : ndu[r, j - 1] / ndu[j, r];
                ndu[r, j] = saved + right[r + 1] * temp;
                saved = left[j - r] * temp;
            }
            ndu[j, j] = saved;
        }
        for (var j = 0; j <= p; j++) ders[0, j] = ndu[j, p];
        var a = new double[2, p + 1];
        var top = Math.Min(d, p);
        for (var r = 0; r <= p; r++)
        {
            int s1 = 0, s2 = 1;
            a[0, 0] = 1;
            for (var k = 1; k <= top; k++)
            {
                var dd = 0.0;
                int rk = r - k, pk = p - k;
                if (r >= k)
                {
                    a[s2, 0] = Div(a[s1, 0], ndu[pk + 1, rk]);
                    dd = a[s2, 0] * ndu[rk, pk];
                }
                var j1 = rk >= -1 ? 1 : -rk;
                var j2 = r - 1 <= pk ? k - 1 : p - r;
                for (var j = j1; j <= j2; j++)
                {
                    a[s2, j] = Div(a[s1, j] - a[s1, j - 1], ndu[pk + 1, rk + j]);
                    dd += a[s2, j] * ndu[rk + j, pk];
                }
                if (r <= pk)
                {
                    a[s2, k] = Div(-a[s1, k - 1], ndu[pk + 1, r]);
                    dd += a[s2, k] * ndu[r, pk];
                }
                ders[k, r] = dd;
                (s1, s2) = (s2, s1);
            }
        }
        var f = (double)p;
        for (var k = 1; k <= top; k++)
        {
            for (var j = 0; j <= p; j++) ders[k, j] *= f;
            f *= p - k;
        }
        return ders;
    }

    static double Div(double a, double b) => b == 0 ? 0 : a / b;

    /// <summary>The binomial coefficients up to <paramref name="n"/> (the rational derivatives, A4.2 and A4.4, need them).</summary>
    internal static double[,] Binomials(int n)
    {
        var b = new double[n + 1, n + 1];
        for (var i = 0; i <= n; i++)
        {
            b[i, 0] = 1;
            for (var j = 1; j <= i; j++) b[i, j] = b[i - 1, j - 1] + (j <= i - 1 ? b[i - 1, j] : 0);
        }
        return b;
    }

    /// <summary>A clamped knot vector of degree p through the interior knots given: p + 1 copies of each end.</summary>
    public static double[] Clamped(int p, IEnumerable<double> interior, double start = 0, double end = 1) =>
        [.. Enumerable.Repeat(start, p + 1), .. interior, .. Enumerable.Repeat(end, p + 1)];

    /// <summary>The knots' distinct values and how many times each comes (as STEP and IFC write them).</summary>
    public static (double[] Values, int[] Multiplicities) Distinct(IReadOnlyList<double> U, double tol = 1e-12)
    {
        var v = new List<double>();
        var m = new List<int>();
        foreach (var k in U)
        {
            if (v.Count > 0 && Math.Abs(k - v[^1]) <= tol) m[^1]++;
            else { v.Add(k); m.Add(1); }
        }
        return ([.. v], [.. m]);
    }

    /// <summary>A knot vector from distinct values and their multiplicities.</summary>
    public static double[] FromDistinct(IReadOnlyList<double> values, IReadOnlyList<int> multiplicities) =>
        [.. values.SelectMany((x, i) => Enumerable.Repeat(x, multiplicities[i]))];

    /// <summary>How many times <paramref name="u"/> comes in U.</summary>
    public static int Multiplicity(IReadOnlyList<double> U, double u, double tol = 1e-12) => U.Count(k => Math.Abs(k - u) <= tol);

    /// <summary>
    /// The averaged knots of a global interpolation of degree p at parameters <paramref name="t"/> (eq. 9.8): the interior knot j
    /// the mean of p parameters, so every span holds a parameter and the system is never singular (Schoenberg–Whitney).
    /// </summary>
    public static double[] Averaged(IReadOnlyList<double> t, int p)
    {
        var n = t.Count - 1;
        var interior = new List<double>();
        for (var j = 1; j <= n - p; j++)
        {
            var s = 0.0;
            for (var i = j; i <= j + p - 1; i++) s += t[i];
            interior.Add(s / p);
        }
        return Clamped(p, interior, t[0], t[n]);
    }

    /// <summary>The chord-length parameters of points (eq. 9.5), from 0 to 1; equal steps when the points all coincide.</summary>
    public static double[] ChordLength(IReadOnlyList<Vec3> q)
    {
        var n = q.Count - 1;
        var t = new double[n + 1];
        var total = 0.0;
        for (var k = 1; k <= n; k++) total += (q[k] - q[k - 1]).Length;
        if (total <= 1e-15) { for (var k = 0; k <= n; k++) t[k] = n == 0 ? 0 : (double)k / n; return t; }
        for (var k = 1; k < n; k++) t[k] = t[k - 1] + (q[k] - q[k - 1]).Length / total;
        t[n] = 1;
        return t;
    }

    /// <summary>The collocation matrix N[j, p](t[k]): row k the basis functions at t[k], the system a global interpolation solves.</summary>
    internal static double[,] Collocation(IReadOnlyList<double> t, int p, IReadOnlyList<double> U, int count)
    {
        var a = new double[t.Count, count];
        for (var k = 0; k < t.Count; k++)
        {
            var span = FindSpan(count - 1, p, t[k], U);
            var N = BasisFuns(span, t[k], p, U);
            for (var j = 0; j <= p; j++) a[k, span - p + j] = N[j];
        }
        return a;
    }

    /// <summary>A square system factored once (LU with partial pivoting), solved for as many right-hand sides as asked.</summary>
    internal sealed class Lu
    {
        readonly double[,] _a;
        readonly int[] _perm;
        readonly int _n;
        public bool Singular { get; }

        public Lu(double[,] a)
        {
            _n = a.GetLength(0);
            _a = (double[,])a.Clone();
            _perm = Enumerable.Range(0, _n).ToArray();
            for (var k = 0; k < _n; k++)
            {
                var piv = k;
                for (var i = k + 1; i < _n; i++) if (Math.Abs(_a[i, k]) > Math.Abs(_a[piv, k])) piv = i;
                if (Math.Abs(_a[piv, k]) < 1e-14) { Singular = true; return; }
                if (piv != k)
                {
                    for (var j = 0; j < _n; j++) (_a[k, j], _a[piv, j]) = (_a[piv, j], _a[k, j]);
                    (_perm[k], _perm[piv]) = (_perm[piv], _perm[k]);
                }
                for (var i = k + 1; i < _n; i++)
                {
                    var f = _a[i, k] / _a[k, k];
                    _a[i, k] = f;
                    for (var j = k + 1; j < _n; j++) _a[i, j] -= f * _a[k, j];
                }
            }
        }

        public Vec4[] Solve(IReadOnlyList<Vec4> b)
        {
            var y = new Vec4[_n];
            for (var i = 0; i < _n; i++)
            {
                var s = b[_perm[i]];
                for (var j = 0; j < i; j++) s -= y[j] * _a[i, j];
                y[i] = s;
            }
            for (var i = _n - 1; i >= 0; i--)
            {
                var s = y[i];
                for (var j = i + 1; j < _n; j++) s -= y[j] * _a[i, j];
                y[i] = s * (1 / _a[i, i]);
            }
            return y;
        }
    }

    /// <summary>
    /// How many straight pieces a stretch of a curve needs by the chord rule (<see cref="Chords"/>): a chord of a parameter step h strays
    /// about |C''|·h²/8 from its curve, so h is held to √(8·tolerance/|C''|), and the curve turns at most <see cref="Chords.MaxStep"/>
    /// a piece. <paramref name="second"/> and <paramref name="turn"/> are the largest |C''| and |C' × C''| / |C'|² sampled on it.
    /// </summary>
    internal static int Pieces(double length, double second, double turn)
    {
        if (!(length > 0)) return 1;
        var n = 0.0;
        if (second > 1e-15) n = Math.Max(n, length / Math.Sqrt(8 * Chords.Tolerance / second));
        if (turn > 1e-15) n = Math.Max(n, length * turn / Chords.MaxStep);
        // A little over what the samples say: the largest bend may lie between them.
        return (int)Math.Clamp(Math.Ceiling(n * 1.15 - 1e-9), 1, 4096);
    }
}

/// <summary>
/// A B-spline or NURBS curve: its degree, knots (clamped or not), control points and, when rational, their weights. Evaluation
/// (A3.1, A4.1), derivatives (A3.2, A4.2), knot insertion (A5.1), refinement, splitting, degree elevation, global interpolation
/// (A9.1) and the exact conic arcs (A7.1), after <i>The NURBS Book</i>. Immutable.
/// </summary>
public sealed class BSplineCurve
{
    public BSplineCurve(int degree, IReadOnlyList<double> knots, IReadOnlyList<Vec3> points, IReadOnlyList<double>? weights = null)
    {
        if (degree < 1) throw new ArgumentException("A B-spline curve has a degree of 1 or more.");
        if (points.Count < degree + 1) throw new ArgumentException($"A curve of degree {degree} needs {degree + 1} control points at least.");
        if (knots.Count != points.Count + degree + 1) throw new ArgumentException($"{points.Count} control points of degree {degree} need {points.Count + degree + 1} knots, not {knots.Count}.");
        for (var i = 1; i < knots.Count; i++) if (knots[i] < knots[i - 1]) throw new ArgumentException("The knots must not decrease.");
        if (weights != null && (weights.Count != points.Count || weights.Any(w => !(w > 0)))) throw new ArgumentException("Each control point needs a weight above 0.");
        Degree = degree;
        Knots = [.. knots];
        Points = [.. points];
        Weights = weights is { } w0 && w0.Any(w => Math.Abs(w - 1) > 1e-15) ? [.. w0] : null;
    }

    public int Degree { get; }
    public double[] Knots { get; }
    public Vec3[] Points { get; }
    /// <summary>The weights, or null when every one is 1 (a plain, non-rational B-spline).</summary>
    public double[]? Weights { get; }
    public bool Rational => Weights != null;
    public double Weight(int i) => Weights?[i] ?? 1;
    int N => Points.Length - 1;
    public double Start => Knots[Degree];
    public double End => Knots[N + 1];

    Vec4[] Homogeneous() => [.. Points.Select((p, i) => Vec4.Of(p, Weight(i)))];

    static BSplineCurve FromHomogeneous(int p, IReadOnlyList<double> U, IReadOnlyList<Vec4> pw) =>
        new(p, U, [.. pw.Select(q => q.Point)], [.. pw.Select(q => q.W)]);

    /// <summary>A3.1 / A4.1: the point at <paramref name="u"/>.</summary>
    public Vec3 At(double u)
    {
        var p = Degree;
        var span = BSpline.FindSpan(N, p, u, Knots);
        var Nb = BSpline.BasisFuns(span, u, p, Knots);
        var c = Vec4.Zero;
        for (var j = 0; j <= p; j++) c += Vec4.Of(Points[span - p + j], Weight(span - p + j)) * Nb[j];
        return c.Point;
    }

    /// <summary>A3.2 with A4.2: the point and its derivatives up to <paramref name="d"/> at <paramref name="u"/> (index k the k-th).</summary>
    public Vec3[] Derivatives(double u, int d)
    {
        var p = Degree;
        var span = BSpline.FindSpan(N, p, u, Knots);
        var nd = BSpline.DersBasisFuns(span, u, p, d, Knots);
        var aw = new Vec4[d + 1];
        for (var k = 0; k <= Math.Min(d, p); k++)
        {
            var s = Vec4.Zero;
            for (var j = 0; j <= p; j++) s += Vec4.Of(Points[span - p + j], Weight(span - p + j)) * nd[k, j];
            aw[k] = s;
        }
        var bin = BSpline.Binomials(d);
        var ck = new Vec3[d + 1];
        for (var k = 0; k <= d; k++)
        {
            var v = aw[k].Xyz;
            for (var i = 1; i <= k; i++) v -= ck[k - i] * (bin[k, i] * aw[i].W);
            ck[k] = v * (1 / aw[0].W);
        }
        return ck;
    }

    /// <summary>The unit tangent at <paramref name="u"/>.</summary>
    public Vec3 Tangent(double u) => Derivatives(u, 1)[1].Normalized();

    /// <summary>A5.1: <paramref name="u"/> inserted <paramref name="times"/> times (no more than the degree less its multiplicity); the same curve.</summary>
    public BSplineCurve InsertKnot(double u, int times = 1)
    {
        var p = Degree;
        var n = N;
        var UP = Knots;
        var s = BSpline.Multiplicity(UP, u);
        var r = Math.Min(times, p - s);
        if (r <= 0 || u < Start - 1e-15 || u > End + 1e-15) return this;
        var k = BSpline.FindSpan(n, p, u, UP);
        // At an existing knot the span starts there: its multiplicity is counted below it.
        var mp = n + p + 1;
        var UQ = new double[mp + r + 1];
        for (var i = 0; i <= k; i++) UQ[i] = UP[i];
        for (var i = 1; i <= r; i++) UQ[k + i] = u;
        for (var i = k + 1; i <= mp; i++) UQ[i + r] = UP[i];
        var Pw = Homogeneous();
        var Qw = new Vec4[n + r + 1];
        for (var i = 0; i <= k - p; i++) Qw[i] = Pw[i];
        for (var i = k - s; i <= n; i++) Qw[i + r] = Pw[i];
        var Rw = new Vec4[p - s + 1];
        for (var i = 0; i <= p - s; i++) Rw[i] = Pw[k - p + i];
        var L = 0;
        for (var j = 1; j <= r; j++)
        {
            L = k - p + j;
            for (var i = 0; i <= p - j - s; i++)
            {
                var alpha = (u - UP[L + i]) / (UP[i + k + 1] - UP[L + i]);
                Rw[i] = Rw[i + 1] * alpha + Rw[i] * (1 - alpha);
            }
            Qw[L] = Rw[0];
            Qw[k + r - j - s] = Rw[p - j - s];
        }
        for (var i = L + 1; i < k - s; i++) Qw[i] = Rw[i - L];
        return FromHomogeneous(p, UQ, Qw);
    }

    /// <summary>Knot refinement: each knot given inserted once (in turn, by A5.1); the same curve with more control points.</summary>
    public BSplineCurve Refine(IEnumerable<double> knots)
    {
        var c = this;
        foreach (var u in knots) c = c.InsertKnot(u, 1);
        return c;
    }

    /// <summary>The curve cut at <paramref name="u"/> into the piece before and the piece after, each clamped.</summary>
    public (BSplineCurve Before, BSplineCurve After) Split(double u)
    {
        var p = Degree;
        var c = InsertKnot(u, p);
        var U = c.Knots;
        // The p + 1 copies' first index: the control point at u is the last of the first piece and the first of the second.
        var first = Array.FindIndex(U, k => Math.Abs(k - u) <= 1e-12);
        var last = first + p - 1;
        while (last + 1 < U.Length && Math.Abs(U[last + 1] - u) <= 1e-12) last++;
        var at = last - p; // the control point on u
        var pa = c.Points.Take(at + 1).ToArray();
        var wa = Enumerable.Range(0, at + 1).Select(c.Weight).ToArray();
        var ua = U.Take(at + 1).Concat(Enumerable.Repeat(u, p + 1)).ToArray();
        var pb = c.Points.Skip(at).ToArray();
        var wb = Enumerable.Range(at, c.Points.Length - at).Select(c.Weight).ToArray();
        var ub = Enumerable.Repeat(u, p + 1).Concat(U.Skip(last + 1)).ToArray();
        return (new BSplineCurve(p, ua, pa, wa), new BSplineCurve(p, ub, pb, wb));
    }

    /// <summary>The same curve run the other way, on the same domain.</summary>
    public BSplineCurve Reversed()
    {
        var (a, b) = (Start, End);
        var U = Knots.Reverse().Select(k => a + b - k).ToArray();
        return new BSplineCurve(Degree, U, [.. Points.Reverse()], Weights?.Reverse().ToArray());
    }

    /// <summary>The same curve on another domain (its knots mapped linearly).</summary>
    public BSplineCurve Reparametrised(double start, double end)
    {
        var (a, b) = (Start, End);
        var s = (end - start) / (b - a);
        return new BSplineCurve(Degree, [.. Knots.Select(k => start + (k - a) * s)], Points, Weights);
    }

    /// <summary>The curve with every control point mapped (an affine map moves a NURBS exactly).</summary>
    public BSplineCurve Transformed(Func<Vec3, Vec3> map) => new(Degree, Knots, [.. Points.Select(map)], Weights);

    /// <summary>
    /// Degree elevation by <paramref name="t"/>: the same curve one degree (or t) higher, every distinct knot's multiplicity raised by t so
    /// its continuity stays. Found in the elevated space by collocation at its Greville abscissae (homogeneous, so a NURBS stays exact):
    /// the curve lies in that space, so the solution is the curve itself, not an approximation.
    /// </summary>
    public BSplineCurve ElevateDegree(int t = 1)
    {
        if (t <= 0) return this;
        var p = Degree;
        var q = p + t;
        var (values, mults) = BSpline.Distinct(Knots);
        var U2 = BSpline.FromDistinct(values, [.. mults.Select(m => m + t)]);
        var count = U2.Length - q - 1;
        var greville = new double[count];
        for (var i = 0; i < count; i++)
        {
            var s = 0.0;
            for (var j = 1; j <= q; j++) s += U2[i + j];
            greville[i] = s / q;
        }
        var basis = BSpline.Collocation(greville, q, U2, count);
        var lu = new BSpline.Lu(basis);
        if (lu.Singular) throw new InvalidOperationException("The degree could not be raised: the collocation is singular.");
        var rhs = greville.Select(HomogeneousAt).ToArray();
        return FromHomogeneous(q, U2, lu.Solve(rhs));
    }

    /// <summary>The curve's homogeneous point at u (w·C(u), w(u)).</summary>
    internal Vec4 HomogeneousAt(double u)
    {
        var p = Degree;
        var span = BSpline.FindSpan(N, p, u, Knots);
        var Nb = BSpline.BasisFuns(span, u, p, Knots);
        var c = Vec4.Zero;
        for (var j = 0; j <= p; j++) c += Vec4.Of(Points[span - p + j], Weight(span - p + j)) * Nb[j];
        return c;
    }

    /// <summary>
    /// The parameters at which the curve is cut into straight pieces by the chord rule (<see cref="Chords"/>): every distinct knot (a
    /// corner stays a corner) and, between them, as many as its bending needs, both ends included.
    /// </summary>
    public List<double> Samples()
    {
        var (values, _) = BSpline.Distinct(Knots);
        var inside = values.Where(k => k >= Start - 1e-15 && k <= End + 1e-15).ToList();
        var o = new List<double> { inside[0] };
        for (var s = 0; s + 1 < inside.Count; s++)
        {
            var (a, b) = (inside[s], inside[s + 1]);
            if (b - a <= 1e-15) continue;
            double second = 0, turn = 0;
            for (var k = 0; k <= 6; k++)
            {
                var u = a + (b - a) * (0.02 + 0.96 * k / 6);
                var d = Derivatives(u, 2);
                second = Math.Max(second, d[2].Length);
                var l2 = Vec3.Dot(d[1], d[1]);
                if (l2 > 1e-24) turn = Math.Max(turn, Vec3.Cross(d[1], d[2]).Length / l2);
            }
            var n = BSpline.Pieces(b - a, second, turn);
            for (var k = 1; k <= n; k++) o.Add(a + (b - a) * k / n);
        }
        return o;
    }

    /// <summary>The curve as points by the chord rule (<see cref="Samples"/>).</summary>
    public List<Vec3> Polyline() => [.. Samples().Select(At)];

    // ------------------------------------------------------------ curves made exactly

    /// <summary>A straight segment from <paramref name="a"/> to <paramref name="b"/> (degree 1, on [0, 1]).</summary>
    public static BSplineCurve Line(Vec3 a, Vec3 b) => new(1, [0, 0, 1, 1], [a, b]);

    /// <summary>
    /// A7.1: an arc of a circle exactly, as a rational quadratic: about <paramref name="centre"/> of <paramref name="radius"/> in the plane
    /// of the unit vectors <paramref name="x"/> and <paramref name="y"/>, from angle <paramref name="start"/> turning <paramref name="sweep"/>
    /// (radians, towards y when positive), in as many pieces of at most 90° as it needs; on [0, 1], the pieces' joins at equal steps.
    /// </summary>
    public static BSplineCurve Arc(Vec3 centre, Vec3 x, Vec3 y, double radius, double start, double sweep)
    {
        var arcs = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2) - 1e-9));
        var dt = sweep / arcs;
        var w1 = Math.Cos(dt / 2);
        Vec3 P(double a, double r) => centre + x * (r * Math.Cos(a)) + y * (r * Math.Sin(a));
        var pts = new List<Vec3> { P(start, radius) };
        var ws = new List<double> { 1 };
        for (var i = 0; i < arcs; i++)
        {
            var a0 = start + dt * i;
            pts.Add(P(a0 + dt / 2, radius / w1));
            ws.Add(w1);
            pts.Add(P(a0 + dt, radius));
            ws.Add(1);
        }
        var interior = new List<double>();
        for (var i = 1; i < arcs; i++) { interior.Add((double)i / arcs); interior.Add((double)i / arcs); }
        return new BSplineCurve(2, BSpline.Clamped(2, interior), pts, ws);
    }

    /// <summary>
    /// A9.1: the curve of degree <paramref name="degree"/> (lower when there are too few points) through the points given, at their
    /// chord-length parameters (eq. 9.5) with averaged knots (eq. 9.8), or at the parameters given. It passes through every point.
    /// </summary>
    public static BSplineCurve Interpolate(IReadOnlyList<Vec3> points, int degree, IReadOnlyList<double>? parameters = null)
    {
        if (points.Count < 2) throw new ArgumentException("An interpolation needs two points at least.");
        var p = Math.Min(degree, points.Count - 1);
        var t = parameters ?? BSpline.ChordLength(points);
        var U = BSpline.Averaged(t, p);
        var a = BSpline.Collocation(t, p, U, points.Count);
        var lu = new BSpline.Lu(a);
        if (lu.Singular) throw new InvalidOperationException("The points could not be interpolated.");
        var ctrl = lu.Solve([.. points.Select(q => Vec4.Of(q, 1))]);
        return new BSplineCurve(p, U, [.. ctrl.Select(c => c.Xyz)]);
    }

    /// <summary>
    /// Pieces that meet end to start joined into one clamped curve (of the highest degree among them), each on its share of [0, 1] by its
    /// length, the joins knots of full multiplicity (corners stay corners). A rational piece's weights are scaled so it meets the last one's.
    /// </summary>
    public static BSplineCurve Join(IReadOnlyList<BSplineCurve> pieces)
    {
        if (pieces.Count == 0) throw new ArgumentException("Nothing to join.");
        var p = pieces.Max(c => c.Degree);
        var cs = pieces.Select(c => c.ElevateDegree(p - c.Degree)).ToList();
        var lengths = cs.Select(c => { var l = c.Polyline(); var s = 0.0; for (var i = 0; i + 1 < l.Count; i++) s += (l[i + 1] - l[i]).Length; return Math.Max(s, 1e-12); }).ToList();
        var total = lengths.Sum();
        var knots = new List<double>(Enumerable.Repeat(0.0, p + 1));
        var pts = new List<Vec3>();
        var ws = new List<double>();
        var at = 0.0;
        for (var k = 0; k < cs.Count; k++)
        {
            var end = k == cs.Count - 1 ? 1 : at + lengths[k] / total;
            var c = cs[k].Reparametrised(at, end);
            var scale = ws.Count > 0 ? ws[^1] / c.Weight(0) : 1;
            for (var i = ws.Count > 0 ? 1 : 0; i < c.Points.Length; i++) { pts.Add(c.Points[i]); ws.Add(c.Weight(i) * scale); }
            knots.AddRange(c.Knots.Skip(p + 1).Take(c.Knots.Length - 2 * (p + 1)));
            knots.AddRange(Enumerable.Repeat(end, k == cs.Count - 1 ? p + 1 : p));
            at = end;
        }
        return new BSplineCurve(p, knots, pts, ws);
    }

    /// <summary>
    /// Curves made compatible (<i>The NURBS Book</i> §10.3): the same degree (the highest, by <see cref="ElevateDegree"/>), the same domain
    /// [0, 1] and the same knots (each one's knots inserted into the others): ready to be skinned or blended control point by control point.
    /// </summary>
    public static List<BSplineCurve> Compatible(IReadOnlyList<BSplineCurve> curves)
    {
        var p = curves.Max(c => c.Degree);
        var cs = curves.Select(c => c.Reparametrised(0, 1)).Select(c => c.ElevateDegree(p - c.Degree)).ToList();
        // The union of the interior knots, each at its highest multiplicity.
        var need = new SortedDictionary<double, int>();
        foreach (var c in cs)
        {
            var (values, mults) = BSpline.Distinct(c.Knots);
            for (var i = 1; i + 1 < values.Length; i++)
            {
                var key = need.Keys.FirstOrDefault(k => Math.Abs(k - values[i]) <= 1e-10, double.NaN);
                if (double.IsNaN(key)) need[values[i]] = mults[i];
                else need[key] = Math.Max(need[key], mults[i]);
            }
        }
        return cs.Select(c =>
        {
            foreach (var (u, m) in need)
            {
                var have = c.Knots.Count(k => Math.Abs(k - u) <= 1e-10);
                if (have < m) c = c.InsertKnot(c.Knots.FirstOrDefault(k => Math.Abs(k - u) <= 1e-10, u), m - have);
            }
            return c;
        }).ToList();
    }
}

/// <summary>
/// A tensor-product B-spline or NURBS surface: degrees and knots in u and v, a net of control points [i, j] (i along u) and, when
/// rational, their weights. Evaluation (A3.5, A4.3), derivatives and normals (A3.6, A4.4), knot insertion and splitting in either
/// direction, iso-curves, the surface of revolution (A8.1), skinning through compatible sections (§10.3) and the grid it is cut
/// into by the chord rule. After <i>The NURBS Book</i>. Immutable.
/// </summary>
public sealed class BSplineSurface
{
    public BSplineSurface(int degreeU, int degreeV, IReadOnlyList<double> knotsU, IReadOnlyList<double> knotsV, Vec3[,] points, double[,]? weights = null)
    {
        var (nu, nv) = (points.GetLength(0), points.GetLength(1));
        if (degreeU < 1 || degreeV < 1) throw new ArgumentException("A B-spline surface has degrees of 1 or more.");
        if (nu < degreeU + 1 || nv < degreeV + 1) throw new ArgumentException("Too few control points for the degrees.");
        if (knotsU.Count != nu + degreeU + 1 || knotsV.Count != nv + degreeV + 1) throw new ArgumentException("The knots do not fit the control points and degrees.");
        if (weights != null && (weights.GetLength(0) != nu || weights.GetLength(1) != nv)) throw new ArgumentException("The weights do not fit the control points.");
        DegreeU = degreeU;
        DegreeV = degreeV;
        KnotsU = [.. knotsU];
        KnotsV = [.. knotsV];
        Points = (Vec3[,])points.Clone();
        var rational = false;
        if (weights != null)
            foreach (var w in weights)
            {
                if (!(w > 0)) throw new ArgumentException("Each control point needs a weight above 0.");
                if (Math.Abs(w - 1) > 1e-15) rational = true;
            }
        Weights = rational ? (double[,])weights!.Clone() : null;
    }

    public int DegreeU { get; }
    public int DegreeV { get; }
    public double[] KnotsU { get; }
    public double[] KnotsV { get; }
    public Vec3[,] Points { get; }
    public double[,]? Weights { get; }
    public bool Rational => Weights != null;
    public int CountU => Points.GetLength(0);
    public int CountV => Points.GetLength(1);
    public double Weight(int i, int j) => Weights?[i, j] ?? 1;
    public (double Start, double End) DomainU => (KnotsU[DegreeU], KnotsU[CountU]);
    public (double Start, double End) DomainV => (KnotsV[DegreeV], KnotsV[CountV]);

    Vec4 Pw(int i, int j) => Vec4.Of(Points[i, j], Weight(i, j));

    /// <summary>A3.5 / A4.3: the point at (u, v).</summary>
    public Vec3 At(double u, double v)
    {
        var (p, q) = (DegreeU, DegreeV);
        var su = BSpline.FindSpan(CountU - 1, p, u, KnotsU);
        var sv = BSpline.FindSpan(CountV - 1, q, v, KnotsV);
        var Nu = BSpline.BasisFuns(su, u, p, KnotsU);
        var Nv = BSpline.BasisFuns(sv, v, q, KnotsV);
        var s = Vec4.Zero;
        for (var l = 0; l <= q; l++)
        {
            var t = Vec4.Zero;
            for (var k = 0; k <= p; k++) t += Pw(su - p + k, sv - q + l) * Nu[k];
            s += t * Nv[l];
        }
        return s.Point;
    }

    /// <summary>A3.6 with A4.4: the point and its partial derivatives, skl[k, l] = ∂^(k+l) S / ∂u^k ∂v^l for k + l ≤ <paramref name="d"/>.</summary>
    public Vec3[,] Derivatives(double u, double v, int d)
    {
        var (p, q) = (DegreeU, DegreeV);
        var su = BSpline.FindSpan(CountU - 1, p, u, KnotsU);
        var sv = BSpline.FindSpan(CountV - 1, q, v, KnotsV);
        var Nu = BSpline.DersBasisFuns(su, u, p, d, KnotsU);
        var Nv = BSpline.DersBasisFuns(sv, v, q, d, KnotsV);
        var aw = new Vec4[d + 1, d + 1];
        for (var k = 0; k <= Math.Min(d, p); k++)
        {
            var temp = new Vec4[q + 1];
            for (var s = 0; s <= q; s++)
            {
                temp[s] = Vec4.Zero;
                for (var r = 0; r <= p; r++) temp[s] += Pw(su - p + r, sv - q + s) * Nu[k, r];
            }
            for (var l = 0; l <= Math.Min(d - k, q); l++)
            {
                var acc = Vec4.Zero;
                for (var s = 0; s <= q; s++) acc += temp[s] * Nv[l, s];
                aw[k, l] = acc;
            }
        }
        var bin = BSpline.Binomials(d);
        var skl = new Vec3[d + 1, d + 1];
        for (var k = 0; k <= d; k++)
            for (var l = 0; l <= d - k; l++)
            {
                var v0 = aw[k, l].Xyz;
                for (var j = 1; j <= l; j++) v0 -= skl[k, l - j] * (bin[l, j] * aw[0, j].W);
                for (var i = 1; i <= k; i++)
                {
                    v0 -= skl[k - i, l] * (bin[k, i] * aw[i, 0].W);
                    var v2 = Vec3.Zero;
                    for (var j = 1; j <= l; j++) v2 += skl[k - i, l - j] * (bin[l, j] * aw[i, j].W);
                    v0 -= v2 * bin[k, i];
                }
                skl[k, l] = v0 * (1 / aw[0, 0].W);
            }
        return skl;
    }

    /// <summary>The unit normal Su × Sv at (u, v); where one tangent vanishes (a pole), the cross of the other with the next derivative.</summary>
    public Vec3 Normal(double u, double v)
    {
        var d = Derivatives(u, v, 2);
        var n = Vec3.Cross(d[1, 0], d[0, 1]);
        if (n.Length > 1e-12 * Math.Max(1e-12, d[1, 0].Length * d[0, 1].Length)) return n.Normalized();
        // A degenerate edge: step a hair inside the domain.
        var (u0, u1) = DomainU;
        var (v0, v1) = DomainV;
        var uu = u + (u < (u0 + u1) / 2 ? 1 : -1) * (u1 - u0) * 1e-6;
        var vv = v + (v < (v0 + v1) / 2 ? 1 : -1) * (v1 - v0) * 1e-6;
        var e = Derivatives(uu, vv, 1);
        return Vec3.Cross(e[1, 0], e[0, 1]).Normalized();
    }

    /// <summary>A row of control points along u at index j as a curve's net (homogeneous), or along v at index i.</summary>
    BSplineCurve RowU(int j) => new(DegreeU, KnotsU, [.. Enumerable.Range(0, CountU).Select(i => Points[i, j])], [.. Enumerable.Range(0, CountU).Select(i => Weight(i, j))]);
    BSplineCurve RowV(int i) => new(DegreeV, KnotsV, [.. Enumerable.Range(0, CountV).Select(j => Points[i, j])], [.. Enumerable.Range(0, CountV).Select(j => Weight(i, j))]);

    static BSplineSurface FromRowsU(int q, IReadOnlyList<double> V, IReadOnlyList<BSplineCurve> rows)
    {
        // Each row a curve along u at a v index: the same knots for all (they were refined alike).
        var c0 = rows[0];
        var pts = new Vec3[c0.Points.Length, rows.Count];
        var w = new double[c0.Points.Length, rows.Count];
        for (var j = 0; j < rows.Count; j++)
            for (var i = 0; i < c0.Points.Length; i++) { pts[i, j] = rows[j].Points[i]; w[i, j] = rows[j].Weight(i); }
        return new BSplineSurface(c0.Degree, q, c0.Knots, V, pts, w);
    }

    static BSplineSurface FromRowsV(int p, IReadOnlyList<double> U, IReadOnlyList<BSplineCurve> rows)
    {
        var c0 = rows[0];
        var pts = new Vec3[rows.Count, c0.Points.Length];
        var w = new double[rows.Count, c0.Points.Length];
        for (var i = 0; i < rows.Count; i++)
            for (var j = 0; j < c0.Points.Length; j++) { pts[i, j] = rows[i].Points[j]; w[i, j] = rows[i].Weight(j); }
        return new BSplineSurface(p, c0.Degree, U, c0.Knots, pts, w);
    }

    /// <summary>A5.3 by rows: <paramref name="u"/> inserted <paramref name="times"/> times in the u direction; the same surface.</summary>
    public BSplineSurface InsertKnotU(double u, int times = 1) =>
        FromRowsU(DegreeV, KnotsV, [.. Enumerable.Range(0, CountV).Select(j => RowU(j).InsertKnot(u, times))]);

    /// <summary>The same in the v direction.</summary>
    public BSplineSurface InsertKnotV(double v, int times = 1) =>
        FromRowsV(DegreeU, KnotsU, [.. Enumerable.Range(0, CountU).Select(i => RowV(i).InsertKnot(v, times))]);

    /// <summary>The surface cut across u at <paramref name="u"/>: the piece before and the piece after, each clamped there.</summary>
    public (BSplineSurface Before, BSplineSurface After) SplitU(double u)
    {
        var pieces = Enumerable.Range(0, CountV).Select(j => RowU(j).Split(u)).ToList();
        return (FromRowsU(DegreeV, KnotsV, [.. pieces.Select(x => x.Before)]), FromRowsU(DegreeV, KnotsV, [.. pieces.Select(x => x.After)]));
    }

    /// <summary>The surface cut across v at <paramref name="v"/>.</summary>
    public (BSplineSurface Before, BSplineSurface After) SplitV(double v)
    {
        var pieces = Enumerable.Range(0, CountU).Select(i => RowV(i).Split(v)).ToList();
        return (FromRowsV(DegreeU, KnotsU, [.. pieces.Select(x => x.Before)]), FromRowsV(DegreeU, KnotsU, [.. pieces.Select(x => x.After)]));
    }

    /// <summary>
    /// The iso-curve at a fixed <paramref name="v"/> (running along u): exact, its control points the net's columns blended by the v
    /// basis functions there (in homogeneous space).
    /// </summary>
    public BSplineCurve IsoV(double v)
    {
        var q = DegreeV;
        var sv = BSpline.FindSpan(CountV - 1, q, v, KnotsV);
        var Nv = BSpline.BasisFuns(sv, v, q, KnotsV);
        var pw = new Vec4[CountU];
        for (var i = 0; i < CountU; i++)
        {
            var s = Vec4.Zero;
            for (var l = 0; l <= q; l++) s += Pw(i, sv - q + l) * Nv[l];
            pw[i] = s;
        }
        return new BSplineCurve(DegreeU, KnotsU, [.. pw.Select(x => x.Point)], [.. pw.Select(x => x.W)]);
    }

    /// <summary>The iso-curve at a fixed <paramref name="u"/> (running along v).</summary>
    public BSplineCurve IsoU(double u)
    {
        var p = DegreeU;
        var su = BSpline.FindSpan(CountU - 1, p, u, KnotsU);
        var Nu = BSpline.BasisFuns(su, u, p, KnotsU);
        var pw = new Vec4[CountV];
        for (var j = 0; j < CountV; j++)
        {
            var s = Vec4.Zero;
            for (var k = 0; k <= p; k++) s += Pw(su - p + k, j) * Nu[k];
            pw[j] = s;
        }
        return new BSplineCurve(DegreeV, KnotsV, [.. pw.Select(x => x.Point)], [.. pw.Select(x => x.W)]);
    }

    /// <summary>The surface with every control point mapped (an affine map moves a NURBS exactly).</summary>
    public BSplineSurface Transformed(Func<Vec3, Vec3> map)
    {
        var pts = new Vec3[CountU, CountV];
        for (var i = 0; i < CountU; i++) for (var j = 0; j < CountV; j++) pts[i, j] = map(Points[i, j]);
        return new BSplineSurface(DegreeU, DegreeV, KnotsU, KnotsV, pts, Weights);
    }

    /// <summary>The surface with u and v exchanged (its normal turned over).</summary>
    public BSplineSurface Transposed()
    {
        var pts = new Vec3[CountV, CountU];
        var w = new double[CountV, CountU];
        for (var i = 0; i < CountU; i++) for (var j = 0; j < CountV; j++) { pts[j, i] = Points[i, j]; w[j, i] = Weight(i, j); }
        return new BSplineSurface(DegreeV, DegreeU, KnotsV, KnotsU, pts, w);
    }

    /// <summary>
    /// The parameters the surface is cut at by the chord rule (<see cref="Chords"/>): every distinct knot in each direction and, between
    /// them, as many as the surface bends there (its second derivatives sampled across the whole other direction).
    /// </summary>
    public (List<double> U, List<double> V) Grid()
    {
        List<double> Along(double[] knots, int degree, int count, bool alongU)
        {
            var (values, _) = BSpline.Distinct(knots);
            var (lo, hi) = (knots[degree], knots[count]);
            var inside = values.Where(k => k >= lo - 1e-15 && k <= hi + 1e-15).ToList();
            var (o0, o1) = alongU ? DomainV : DomainU;
            var o = new List<double> { inside[0] };
            for (var s = 0; s + 1 < inside.Count; s++)
            {
                var (a, b) = (inside[s], inside[s + 1]);
                if (b - a <= 1e-15) continue;
                double second = 0, turn = 0;
                for (var k = 0; k <= 4; k++)
                    for (var m = 0; m <= 6; m++)
                    {
                        var t = a + (b - a) * (0.02 + 0.96 * k / 4);
                        var w = o0 + (o1 - o0) * m / 6;
                        var d = alongU ? Derivatives(t, w, 2) : Derivatives(w, t, 2);
                        var (d1, d2) = alongU ? (d[1, 0], d[2, 0]) : (d[0, 1], d[0, 2]);
                        second = Math.Max(second, d2.Length);
                        var l2 = Vec3.Dot(d1, d1);
                        if (l2 > 1e-24) turn = Math.Max(turn, Vec3.Cross(d1, d2).Length / l2);
                    }
                var n = BSpline.Pieces(b - a, second, turn);
                for (var k = 1; k <= n; k++) o.Add(a + (b - a) * k / n);
            }
            return o;
        }
        return (Along(KnotsU, DegreeU, CountU, true), Along(KnotsV, DegreeV, CountV, false));
    }

    // ------------------------------------------------------------ surfaces made exactly

    /// <summary>
    /// A8.1: the surface a curve sweeps turning about an axis (through <paramref name="axisPoint"/> along <paramref name="axisDir"/>) by
    /// <paramref name="angle"/> radians: u round the axis (exact rational arcs, A7.1), v along the curve.
    /// </summary>
    public static BSplineSurface Revolve(BSplineCurve profile, Vec3 axisPoint, Vec3 axisDir, double angle)
    {
        var z = axisDir.Normalized();
        var arc = BSplineCurve.Arc(Vec3.Zero, new Vec3(1, 0, 0), new Vec3(0, 1, 0), 1, 0, angle);
        var nu = arc.Points.Length;
        var nv = profile.Points.Length;
        var pts = new Vec3[nu, nv];
        var w = new double[nu, nv];
        for (var j = 0; j < nv; j++)
        {
            var p = profile.Points[j];
            var o = axisPoint + z * Vec3.Dot(p - axisPoint, z);
            var xv = p - o;
            var r = xv.Length;
            var x = r > 1e-15 ? xv * (1 / r) : PerpendicularTo(z);
            var y = Vec3.Cross(z, x);
            for (var i = 0; i < nu; i++)
            {
                // The unit arc's control point, scaled to this radius in this point's own plane.
                var c = arc.Points[i];
                pts[i, j] = o + x * (c.X * r) + y * (c.Y * r);
                w[i, j] = arc.Weight(i) * profile.Weight(j);
            }
        }
        return new BSplineSurface(2, profile.Degree, arc.Knots, profile.Knots, pts, w);
    }

    static Vec3 PerpendicularTo(Vec3 z) => (Math.Abs(z.X) < 0.9 ? Vec3.Cross(z, new Vec3(1, 0, 0)) : Vec3.Cross(z, new Vec3(0, 1, 0))).Normalized();

    /// <summary>
    /// Skinning (<i>The NURBS Book</i> §10.3): the surface through sections that are already compatible (<see cref="BSplineCurve.Compatible"/>),
    /// u along each section, v across them, of degree <paramref name="degreeV"/> (lower for fewer sections): each column of homogeneous control
    /// points interpolated (A9.1) at the sections' parameters, the mean over the columns of their chord-length parameters (§10.3's
    /// averaging), with averaged knots. It passes through every section exactly. <paramref name="vParameters"/>, when given, are used instead.
    /// </summary>
    public static BSplineSurface Skin(IReadOnlyList<BSplineCurve> sections, int degreeV, IReadOnlyList<double>? vParameters = null)
    {
        var k = sections.Count;
        if (k < 2) throw new ArgumentException("Skinning needs two sections at least.");
        var c0 = sections[0];
        var n = c0.Points.Length;
        if (sections.Any(s => s.Points.Length != n || s.Degree != c0.Degree || !s.Knots.SequenceEqual(c0.Knots))) throw new ArgumentException("The sections are not compatible.");
        var q = Math.Min(degreeV, k - 1);
        double[] t;
        if (vParameters != null) t = [.. vParameters];
        else
        {
            t = new double[k];
            for (var i = 0; i < n; i++)
            {
                var col = BSpline.ChordLength([.. sections.Select(s => s.Points[i])]);
                for (var j = 0; j < k; j++) t[j] += col[j] / n;
            }
            t[0] = 0;
            t[k - 1] = 1;
        }
        var V = BSpline.Averaged(t, q);
        var lu = new BSpline.Lu(BSpline.Collocation(t, q, V, k));
        if (lu.Singular) throw new InvalidOperationException("The sections could not be skinned.");
        var pts = new Vec3[n, k];
        var w = new double[n, k];
        for (var i = 0; i < n; i++)
        {
            var col = lu.Solve([.. sections.Select(s => Vec4.Of(s.Points[i], s.Weight(i)))]);
            for (var j = 0; j < k; j++)
            {
                if (!(col[j].W > 1e-9)) throw new InvalidOperationException("The sections' weights could not be blended (a weight fell to 0).");
                pts[i, j] = col[j].Point;
                w[i, j] = col[j].W;
            }
        }
        return new BSplineSurface(c0.Degree, q, c0.Knots, V, pts, w);
    }

    /// <summary>
    /// A uniform bicubic B-spline patch (the limit surface of a regular Catmull–Clark face, its 4 × 4 control net) in Bézier form: a
    /// clamped bicubic on [0, 1]², the same surface. Each direction by the uniform-to-Bézier matrix (1/6)[1 4 1 0; 0 4 2 0; 0 2 4 0; 0 1 4 1].
    /// </summary>
    public static BSplineSurface UniformBicubic(Vec3[,] net)
    {
        static Vec3[] Row(Vec3 a, Vec3 b, Vec3 c, Vec3 d) => [(a + b * 4 + c) * (1.0 / 6), (b * 4 + c * 2) * (1.0 / 6), (b * 2 + c * 4) * (1.0 / 6), (b + c * 4 + d) * (1.0 / 6)];
        var half = new Vec3[4, 4];
        for (var j = 0; j < 4; j++)
        {
            var r = Row(net[0, j], net[1, j], net[2, j], net[3, j]);
            for (var i = 0; i < 4; i++) half[i, j] = r[i];
        }
        var bez = new Vec3[4, 4];
        for (var i = 0; i < 4; i++)
        {
            var r = Row(half[i, 0], half[i, 1], half[i, 2], half[i, 3]);
            for (var j = 0; j < 4; j++) bez[i, j] = r[j];
        }
        double[] U = [0, 0, 0, 0, 1, 1, 1, 1];
        return new BSplineSurface(3, 3, U, U, bez);
    }
}
