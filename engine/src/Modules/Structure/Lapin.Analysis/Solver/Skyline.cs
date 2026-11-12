using System.Numerics;

namespace Lapin.Analysis.Solver;

/// <summary>
/// A symmetric matrix in profile (skyline) form, factored as L·D·Lᵀ (the active column method): column j holds its entries
/// from its first non-zero row <c>first[j]</c> down to the diagonal, contiguous, so the factor fills only inside the profile.
/// One factorization solves every load case. A pivot that is not clearly positive (at most <see cref="PivotTolerance"/> of
/// the equation's own diagonal) is a mechanism: <see cref="Factor"/> says which equation.
/// </summary>
public sealed class Skyline : ISymmetricFactor
{
    public const double PivotTolerance = 1e-11;

    readonly int _n;
    readonly int[] _first;
    readonly long[] _ptr;
    readonly double[] _a;
    double[] _diag0 = [];

    public int Size => _n;
    public long Stored => _a.LongLength;

    public Skyline(int[] first)
    {
        _n = first.Length;
        _first = first;
        _ptr = new long[_n + 1];
        for (var j = 0; j < _n; j++) _ptr[j + 1] = _ptr[j] + (j - first[j] + 1);
        _a = new double[_ptr[_n]];
    }

    long Index(int i, int j) => _ptr[j] + (i - _first[j]);

    /// <summary>Adds to the entry (i, j), either triangle.</summary>
    public void Add(int i, int j, double v)
    {
        if (i > j) (i, j) = (j, i);
        if (i < _first[j]) throw new InvalidOperationException("An entry outside the profile.");
        _a[Index(i, j)] += v;
    }

    public double Diagonal(int j) => _a[Index(j, j)];

    /// <summary>
    /// Factors in place; the first equation whose pivot is not positive, or −1. With <paramref name="hold"/>, such an
    /// equation gets the stiffness it returns added to its diagonal (a spring) and the factoring goes on.
    /// </summary>
    public int Factor(Func<int, double>? hold = null)
    {
        _diag0 = new double[_n];
        for (var j = 0; j < _n; j++) _diag0[j] = _a[Index(j, j)];
        var a = _a.AsSpan();
        for (var j = 0; j < _n; j++)
        {
            var fj = _first[j];
            var colJ = _ptr[j];
            for (var i = fj + 1; i < j; i++)
            {
                // g_ij = a_ij − Σ_k l_ki g_kj over the rows both columns hold.
                var k0 = Math.Max(_first[i], fj);
                var len = i - k0;
                if (len <= 0) continue;
                var si = _ptr[i] + (k0 - _first[i]);
                var sj = colJ + (k0 - fj);
                a[(int)(colJ + (i - fj))] -= Dot(_a, si, sj, len);
            }
            // l_ij = g_ij / d_i;  d_j = a_jj − Σ l_ij g_ij.
            var d = a[(int)(colJ + (j - fj))];
            for (var i = fj; i < j; i++)
            {
                var at = (int)(colJ + (i - fj));
                var g = a[at];
                if (g == 0) continue;
                var l = g / a[(int)Index(i, i)];
                d -= l * g;
                a[at] = l;
            }
            var scale = Math.Abs(_diag0[j]);
            if (!(d > PivotTolerance * scale) || scale == 0)
            {
                if (hold == null) { a[(int)(colJ + (j - fj))] = d; return j; }
                d += hold(j);
            }
            a[(int)(colJ + (j - fj))] = d;
        }
        return -1;
    }

    static double Dot(double[] a, long si, long sj, int len)
    {
        var x = a.AsSpan((int)si, len);
        var y = a.AsSpan((int)sj, len);
        var s = 0.0;
        var k = 0;
        if (Vector.IsHardwareAccelerated && len >= Vector<double>.Count * 2)
        {
            var acc = Vector<double>.Zero;
            var w = Vector<double>.Count;
            for (; k <= len - w; k += w) acc += new Vector<double>(x.Slice(k, w)) * new Vector<double>(y.Slice(k, w));
            s = Vector.Dot(acc, Vector<double>.One);
        }
        for (; k < len; k++) s += x[k] * y[k];
        return s;
    }

    /// <summary>Solves L·D·Lᵀ x = b in place (after <see cref="Factor"/>).</summary>
    public void Solve(double[] b)
    {
        for (var j = 0; j < _n; j++)
        {
            var fj = _first[j];
            var len = j - fj;
            if (len > 0) b[j] -= DotWith(_ptr[j], b, fj, len);
        }
        for (var j = 0; j < _n; j++) b[j] /= _a[Index(j, j)];
        for (var j = _n - 1; j >= 0; j--)
        {
            var fj = _first[j];
            var xj = b[j];
            if (xj == 0) continue;
            var col = _ptr[j];
            for (var i = fj; i < j; i++) b[i] -= _a[col + (i - fj)] * xj;
        }
    }

    double DotWith(long start, double[] b, int from, int len)
    {
        var s = 0.0;
        for (var k = 0; k < len; k++) s += _a[start + k] * b[from + k];
        return s;
    }
}

/// <summary>
/// Reverse Cuthill–McKee: the nodes renumbered so that joined nodes get close numbers (a narrow profile). Each part of the
/// graph from a pseudo-peripheral node (the far end of a breadth-first search, repeated while it gets deeper), its
/// neighbours taken by rising degree, the whole order reversed.
/// </summary>
public static class Rcm
{
    public static int[] Order(int n, IReadOnlyList<int>[] adjacent)
    {
        var order = new List<int>(n);
        var seen = new bool[n];
        var degree = adjacent.Select(a => a.Count).ToArray();
        var byDegree = Enumerable.Range(0, n).OrderBy(i => degree[i]).ToList();
        foreach (var seed in byDegree)
        {
            if (seen[seed]) continue;
            var start = Peripheral(seed, adjacent);
            var queue = new Queue<int>();
            queue.Enqueue(start);
            seen[start] = true;
            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                order.Add(v);
                foreach (var w in adjacent[v].Where(w => !seen[w]).OrderBy(w => degree[w]))
                {
                    seen[w] = true;
                    queue.Enqueue(w);
                }
            }
        }
        order.Reverse();
        return order.ToArray();
    }

    static int Peripheral(int seed, IReadOnlyList<int>[] adjacent)
    {
        var (node, depth) = (seed, -1);
        for (var pass = 0; pass < 8; pass++)
        {
            var (far, d) = Farthest(node, adjacent);
            if (d <= depth) break;
            (node, depth) = (far, d);
        }
        return node;
    }

    static (int Node, int Depth) Farthest(int from, IReadOnlyList<int>[] adjacent)
    {
        var level = new Dictionary<int, int> { [from] = 0 };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        var (last, depth) = (from, 0);
        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            var lv = level[v];
            if (lv > depth || (lv == depth && adjacent[v].Count < adjacent[last].Count)) (last, depth) = (v, lv);
            foreach (var w in adjacent[v])
                if (level.TryAdd(w, lv + 1)) queue.Enqueue(w);
        }
        return (last, depth);
    }
}
