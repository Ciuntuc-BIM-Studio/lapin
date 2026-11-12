using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lapin.Analysis.Solver;

/// <summary>A symmetric matrix the solver assembles, factors once and solves for every load case.</summary>
public interface ISymmetricFactor
{
    int Size { get; }
    /// <summary>Entries the factor holds.</summary>
    long Stored { get; }
    void Add(int i, int j, double v);
    /// <summary>The first equation whose pivot is not positive, or −1; with <c>hold</c>, a spring added there instead.</summary>
    int Factor(Func<int, double>? hold = null);
    void Solve(double[] b);
}

/// <summary>
/// Lapin's sparse Cholesky factorization (docs/plan-fem.md §10.6), L·Lᵀ = P·A·Pᵀ for a symmetric matrix that is positive
/// definite once its supports hold it. The equations come in groups (a node's six degrees of freedom), ordered as groups by
/// approximate minimum degree (<see cref="MinimumDegree"/>); the elimination tree (Liu, "The role of elimination trees in
/// sparse factorization", SIAM J. Matrix Anal. Appl. 11(1), 1990), postordered, gives the column counts by row subtrees and
/// the fundamental supernodes: runs of columns with one structure, stored as dense row-major blocks. The numeric factorization
/// is left-looking and supernodal (Ng and Peyton, "Block sparse Cholesky algorithms on advanced uniprocessor computers", SIAM
/// J. Sci. Comput. 14(5), 1993; Davis, "Direct Methods for Sparse Linear Systems", SIAM 2006, ch. 4 and 9): each supernode
/// gathers the updates of the supernodes below it through linked lists keyed by their next row, then factors its own dense
/// block. A pivot not clearly positive (at most <see cref="PivotTolerance"/> of the equation's own diagonal) is a mechanism,
/// as with <see cref="Skyline"/>. Written for Lapin from those descriptions; no code from any library.
/// </summary>
public sealed class SparseCholesky : ISymmetricFactor
{
    public const double PivotTolerance = Skyline.PivotTolerance;

    readonly int _n;
    readonly int[] _perm;   // new equation → old
    readonly int[] _iperm;  // old → new
    readonly int _ns;
    readonly int[] _first;  // a supernode's first (new) equation; _first[_ns] = n
    readonly int[][] _rows; // a supernode's rows (new equations): its own columns, then the rows below, rising
    readonly int[] _of;     // new equation → its supernode
    double[][]? _val;       // row-major, rows × columns
    readonly long _stored;
    /// <summary>Floating-point operations the numeric factorization takes (multiplications and additions).</summary>
    public double Flops { get; }

    public int Size => _n;
    public long Stored => _stored;
    public int Supernodes => _ns;

    /// <summary>
    /// The structure: <paramref name="cliques"/> are the equations each element joins (any order; negative ones skipped);
    /// <paramref name="groupOf"/> (optional) gives each equation's group, ordered together (a node's degrees of freedom).
    /// </summary>
    public SparseCholesky(int n, IReadOnlyList<int[]> cliques, int[]? groupOf = null)
    {
        _n = n;
        // Groups numbered densely, in the order their equations first come.
        var gOf = new int[n];
        var ng = 0;
        if (groupOf == null) { for (var i = 0; i < n; i++) gOf[i] = i; ng = n; }
        else
        {
            var dense = new Dictionary<int, int>();
            for (var i = 0; i < n; i++)
            {
                if (!dense.TryGetValue(groupOf[i], out var g)) dense[groupOf[i]] = g = ng++;
                gOf[i] = g;
            }
        }
        // The groups' equations and their graph.
        var count = new int[ng + 1];
        foreach (var g in gOf) count[g + 1]++;
        for (var g = 0; g < ng; g++) count[g + 1] += count[g];
        var eqs = new int[n];
        var fill = (int[])count.Clone();
        for (var i = 0; i < n; i++) eqs[fill[gOf[i]]++] = i;
        var adjSets = new List<int>[ng];
        for (var g = 0; g < ng; g++) adjSets[g] = [];
        var gs = new List<int>();
        foreach (var c in cliques)
        {
            gs.Clear();
            foreach (var e in c) if (e >= 0) gs.Add(gOf[e]);
            if (gs.Count < 2) continue;
            for (var a = 0; a < gs.Count; a++)
                for (var b = 0; b < gs.Count; b++)
                    if (gs[a] != gs[b]) adjSets[gs[a]].Add(gs[b]);
        }
        var adj = new int[ng][];
        for (var g = 0; g < ng; g++)
        {
            var l = adjSets[g];
            l.Sort();
            var o = new List<int>(l.Count);
            foreach (var x in l) if (o.Count == 0 || o[^1] != x) o.Add(x);
            adj[g] = o.ToArray();
            adjSets[g] = null!;
        }

        // Order (minimum degree, or nested dissection when it predicts less work), elimination tree, postorder.
        var size = new int[ng];
        for (var g = 0; g < ng; g++) size[g] = count[g + 1] - count[g];
        var order = MinimumDegree.Order(ng, adj);
        Ordering = "minimum degree";
        if (ng > NestedDissection.Leaf)
        {
            var nd = NestedDissection.Order(ng, adj);
            if (Work(nd, adj, size) < Work(order, adj, size)) (order, Ordering) = (nd, "nested dissection");
        }
        var pos = new int[ng];
        for (var k = 0; k < ng; k++) pos[order[k]] = k;
        var parent = Tree(order, pos, adj);
        var post = Postorder(parent);
        var newPos = new int[ng];
        for (var j = 0; j < ng; j++) newPos[post[j]] = j;
        var gOrder = new int[ng];       // new group position → group
        var gParent = new int[ng];
        for (var j = 0; j < ng; j++)
        {
            gOrder[j] = order[post[j]];
            var pa = parent[post[j]];
            gParent[j] = pa < 0 ? -1 : newPos[pa];
        }
        var gPos = new int[ng];
        for (var j = 0; j < ng; j++) gPos[gOrder[j]] = j;

        // Column counts (in groups, below the diagonal) by row subtrees.
        var cc = new int[ng];
        var mark = new int[ng];
        Array.Fill(mark, -1);
        for (var i = 0; i < ng; i++)
        {
            mark[i] = i;
            foreach (var nb in adj[gOrder[i]])
            {
                var r = gPos[nb];
                if (r >= i) continue;
                while (mark[r] != i) { cc[r]++; mark[r] = i; r = gParent[r]; }
            }
        }
        var children = new int[ng];
        foreach (var pa in gParent) if (pa >= 0) children[pa]++;

        // Fundamental supernodes over the groups.
        var snOfGroup = new int[ng];
        var snStart = new List<int>();
        for (var j = 0; j < ng; j++)
        {
            var joins = j > 0 && gParent[j - 1] == j && cc[j - 1] == cc[j] + 1 && children[j] == 1;
            if (!joins) snStart.Add(j);
            snOfGroup[j] = snStart.Count - 1;
        }
        _ns = snStart.Count;
        snStart.Add(ng);

        // The new equation numbering: groups in order, each group's equations rising.
        _perm = new int[n];
        _iperm = new int[n];
        var gFirstEq = new int[ng + 1];
        var at = 0;
        for (var j = 0; j < ng; j++)
        {
            gFirstEq[j] = at;
            var g = gOrder[j];
            for (var x = count[g]; x < count[g + 1]; x++) { _perm[at] = eqs[x]; _iperm[eqs[x]] = at; at++; }
        }
        gFirstEq[ng] = at;

        // Each supernode's rows (groups), bottom-up: its own adjacency below it and its children's rows past it.
        _first = new int[_ns + 1];
        _rows = new int[_ns][];
        _of = new int[n];
        var snGroups = new int[_ns][];
        var snParent = new int[_ns];
        var childLists = new List<int>[_ns];
        for (var s = 0; s < _ns; s++) childLists[s] = [];
        for (var s = 0; s < _ns; s++)
        {
            var last = snStart[s + 1] - 1;
            snParent[s] = gParent[last] < 0 ? -1 : snOfGroup[gParent[last]];
            if (snParent[s] >= 0) childLists[snParent[s]].Add(s);
        }
        Array.Fill(mark, -1);
        var rowGroups = new List<int>();
        long stored = 0;
        var flops = 0.0;
        for (var s = 0; s < _ns; s++)
        {
            int f = snStart[s], l = snStart[s + 1] - 1;
            rowGroups.Clear();
            for (var j = f; j <= l; j++) { rowGroups.Add(j); mark[j] = s; }
            var own = rowGroups.Count;
            for (var j = f; j <= l; j++)
                foreach (var nb in adj[gOrder[j]])
                {
                    var r = gPos[nb];
                    if (r > l && mark[r] != s) { mark[r] = s; rowGroups.Add(r); }
                }
            foreach (var c in childLists[s])
                foreach (var r in snGroups[c])
                    if (r > l && mark[r] != s) { mark[r] = s; rowGroups.Add(r); }
            rowGroups.Sort(own, rowGroups.Count - own, null);
            snGroups[s] = rowGroups.Skip(own).ToArray();
            foreach (var c in childLists[s]) snGroups[c] = [];
            var rows = new List<int>();
            foreach (var g in rowGroups) for (var e = gFirstEq[g]; e < gFirstEq[g + 1]; e++) rows.Add(e);
            _rows[s] = rows.ToArray();
            _first[s] = gFirstEq[f];
            var nc = gFirstEq[l + 1] - gFirstEq[f];
            for (var e = gFirstEq[f]; e < gFirstEq[l + 1]; e++) _of[e] = s;
            stored += (long)rows.Count * nc;
            for (var j = 0; j < nc; j++) { double below = rows.Count - j - 1; flops += below * below + below; }
        }
        _first[_ns] = n;
        _stored = stored;
        Flops = flops;
    }

    /// <summary>Which ordering the structure took.</summary>
    public string Ordering { get; } = "";

    /// <summary>The elimination tree of the groups in this order (Liu 1990, with path compression): each column's parent, or −1.</summary>
    static int[] Tree(int[] order, int[] pos, int[][] adj)
    {
        var ng = order.Length;
        var parent = new int[ng];
        var ancestor = new int[ng];
        Array.Fill(parent, -1);
        Array.Fill(ancestor, -1);
        for (var k = 0; k < ng; k++)
            foreach (var nb in adj[order[k]])
            {
                var r = pos[nb];
                if (r >= k) continue;
                while (ancestor[r] != -1 && ancestor[r] != k)
                {
                    var t = ancestor[r];
                    ancestor[r] = k;
                    r = t;
                }
                if (ancestor[r] == -1) { ancestor[r] = k; parent[r] = k; }
            }
        return parent;
    }

    /// <summary>The work an order predicts: Σ over the columns of (the rows below them)², from the column counts by row subtrees, in equations.</summary>
    static double Work(int[] order, int[][] adj, int[] size)
    {
        var ng = order.Length;
        var pos = new int[ng];
        for (var k = 0; k < ng; k++) pos[order[k]] = k;
        var parent = Tree(order, pos, adj);
        var cc = new long[ng];
        var mark = new int[ng];
        Array.Fill(mark, -1);
        for (var i = 0; i < ng; i++)
        {
            mark[i] = i;
            var si = size[order[i]];
            foreach (var nb in adj[order[i]])
            {
                var r = pos[nb];
                if (r >= i) continue;
                while (mark[r] != i) { cc[r] += si; mark[r] = i; r = parent[r]; }
            }
        }
        var work = 0.0;
        for (var j = 0; j < ng; j++) { double below = cc[j] + size[order[j]]; work += size[order[j]] * below * below; }
        return work;
    }

    static int[] Postorder(int[] parent)
    {
        var n = parent.Length;
        var head = new int[n];
        var next = new int[n];
        Array.Fill(head, -1);
        for (var j = n - 1; j >= 0; j--)
            if (parent[j] >= 0) { next[j] = head[parent[j]]; head[parent[j]] = j; }
        var post = new int[n];
        var k = 0;
        var stack = new Stack<int>();
        for (var root = 0; root < n; root++)
        {
            if (parent[root] >= 0) continue;
            stack.Push(root);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                var child = head[top];
                if (child < 0) { stack.Pop(); post[k++] = top; }
                else { head[top] = next[child]; stack.Push(child); }
            }
        }
        return post;
    }

    int Columns(int s) => _first[s + 1] - _first[s];

    double[][] Values()
    {
        if (_val != null) return _val;
        _val = new double[_ns][];
        for (var s = 0; s < _ns; s++) _val[s] = new double[(long)_rows[s].Length * Columns(s)];
        return _val;
    }

    /// <summary>Adds to the entry (i, j) (either triangle), in the caller's numbering.</summary>
    public void Add(int i, int j, double v)
    {
        var val = Values();
        int a = _iperm[i], b = _iperm[j];
        if (a < b) (a, b) = (b, a);
        var s = _of[b];
        var nc = Columns(s);
        var col = b - _first[s];
        int row;
        if (a < _first[s + 1]) row = a - _first[s];
        else
        {
            row = Array.BinarySearch(_rows[s], nc, _rows[s].Length - nc, a);
            if (row < 0) throw new InvalidOperationException("An entry outside the structure.");
        }
        val[s][(long)row * nc + col] += v;
    }

    /// <summary>
    /// Factors; the first equation (caller's numbering) whose pivot is not positive, or −1. With <paramref name="hold"/>,
    /// such an equation gets the stiffness it returns added to its pivot (a spring) and the factoring goes on.
    /// </summary>
    public int Factor(Func<int, double>? hold = null)
    {
        var val = Values();
        var diag0 = new double[_n];
        for (var s = 0; s < _ns; s++)
        {
            var nc = Columns(s);
            for (var j = 0; j < nc; j++) diag0[_first[s] + j] = val[s][j * nc + j];
        }
        var head = new int[_ns];
        var link = new int[_ns];
        var ptr = new int[_ns];
        Array.Fill(head, -1);
        var rel = new int[_n];
        for (var s = 0; s < _ns; s++)
        {
            int f = _first[s], nc = Columns(s);
            var rows = _rows[s];
            var ls = val[s];
            for (var x = 0; x < rows.Length; x++) rel[rows[x]] = x;

            // The updates of the supernodes below that reach these columns.
            var d = head[s];
            head[s] = -1;
            while (d >= 0)
            {
                var nextD = link[d];
                var rd = _rows[d];
                var ld = val[d];
                var ncd = Columns(d);
                var p0 = ptr[d];
                var p1 = p0;
                while (p1 < rd.Length && rd[p1] < f + nc) p1++;
                var below = rd.Length - p0;
                if ((double)below * (p1 - p0) * ncd < ParallelWork || below < 64) Update(ld, ncd, rd, p0, p1, p0, rd.Length, ls, nc, f, rel);
                else
                {
                    var (pp0, pp1, lsS, ncS, fS) = (p0, p1, ls, nc, f);
                    var chunks = Math.Min(below / 32, Environment.ProcessorCount * 4);
                    var step = (below + chunks - 1) / chunks;
                    Parallel.For(0, chunks, x => Update(ld, ncd, rd, pp0, pp1, pp0 + x * step, Math.Min(rd.Length, pp0 + (x + 1) * step), lsS, ncS, fS, rel));
                }
                ptr[d] = p1;
                if (p1 < rd.Length) { var t = _of[rd[p1]]; link[d] = head[t]; head[t] = d; }
                d = nextD;
            }

            // The supernode's own dense block: the diagonal, then the rows below.
            for (var j = 0; j < nc; j++)
            {
                var rj = j * nc;
                for (var k = 0; k < j; k++)
                    ls[rj + k] = (ls[rj + k] - Dot(ls, rj, k * nc, k)) / ls[k * nc + k];
                var piv = ls[rj + j] - Dot(ls, rj, rj, j);
                var scale = Math.Abs(diag0[f + j]);
                if (!(piv > PivotTolerance * scale) || scale == 0)
                {
                    if (hold == null) return _perm[f + j];
                    piv += hold(_perm[f + j]);
                    if (!(piv > 0)) return _perm[f + j];
                }
                ls[rj + j] = Math.Sqrt(piv);
            }
            var off = rows.Length - nc;
            if ((double)off * nc * nc < ParallelWork || off < 64) Below(ls, nc, nc, rows.Length);
            else
            {
                var (lsS, ncS, end) = (ls, nc, rows.Length);
                var chunks = Math.Min(off / 32, Environment.ProcessorCount * 4);
                var step = (off + chunks - 1) / chunks;
                Parallel.For(0, chunks, x => Below(lsS, ncS, ncS + x * step, Math.Min(end, ncS + (x + 1) * step)));
            }
            ptr[s] = nc;
            if (nc < rows.Length) { var t = _of[rows[nc]]; link[s] = head[t]; head[t] = s; }
        }
        return -1;
    }

    /// <summary>Work (multiply–adds) past which a block's rows are shared between the processor's cores; the browser's engine runs them in turn.</summary>
    const double ParallelWork = 4e6;

    /// <summary>A supernode's rows below its diagonal block, from..to: L₂₁ = A₂₁·L₁₁⁻ᵀ, row by row.</summary>
    static void Below(double[] ls, int nc, int from, int to)
    {
        for (var r = from; r < to; r++)
        {
            var rr = r * nc;
            for (var k = 0; k < nc; k++)
                ls[rr + k] = (ls[rr + k] - Dot(ls, rr, k * nc, k)) / ls[k * nc + k];
        }
    }

    /// <summary>
    /// The update of supernode d onto supernode s: d's rows ra..rb (from p0 on) against its rows p0..p1 (those in s's columns),
    /// L_s(r, c) −= L_d(r, :)·L_d(c, :), two rows by two columns at a time. Each row of d writes its own row of s.
    /// </summary>
    static void Update(double[] ld, int ncd, int[] rd, int p0, int p1, int ra, int rb, double[] ls, int nc, int f, int[] rel)
    {
        var nr = rb;
        var r = ra;
        for (; r + 1 < nr; r += 2)
        {
            int tr0 = rel[rd[r]] * nc, tr1 = rel[rd[r + 1]] * nc;
            var cmax = Math.Min(p1, r + 2);
            var c = p0;
            for (; c + 1 < cmax; c += 2)
            {
                Dot4(ld, r * ncd, (r + 1) * ncd, c * ncd, (c + 1) * ncd, ncd, out var s00, out var s01, out var s10, out var s11);
                int tc0 = rd[c] - f, tc1 = rd[c + 1] - f;
                ls[tr0 + tc0] -= s00;
                if (c + 1 <= r) ls[tr0 + tc1] -= s01;
                ls[tr1 + tc0] -= s10;
                ls[tr1 + tc1] -= s11;
            }
            if (c < cmax)
            {
                var tc = rd[c] - f;
                ls[tr0 + tc] -= c <= r ? Dot(ld, r * ncd, c * ncd, ncd) : 0;
                ls[tr1 + tc] -= Dot(ld, (r + 1) * ncd, c * ncd, ncd);
            }
        }
        for (; r < nr; r++)
        {
            var tr = rel[rd[r]] * nc;
            var cmax = Math.Min(p1, r + 1);
            for (var c = p0; c < cmax; c++) ls[tr + rd[c] - f] -= Dot(ld, r * ncd, c * ncd, ncd);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static double Dot(double[] a, int x, int y, int len)
    {
        if (len == 0) return 0;
        ref var ra = ref MemoryMarshal.GetArrayDataReference(a);
        var s = 0.0;
        var k = 0;
        var w = Vector<double>.Count;
        if (Vector.IsHardwareAccelerated && len >= w * 2)
        {
            var acc = Vector<double>.Zero;
            for (; k <= len - w; k += w)
                acc += Vector.LoadUnsafe(ref ra, (nuint)(x + k)) * Vector.LoadUnsafe(ref ra, (nuint)(y + k));
            s = Vector.Sum(acc);
        }
        for (; k < len; k++) s += Unsafe.Add(ref ra, x + k) * Unsafe.Add(ref ra, y + k);
        return s;
    }

    /// <summary>Four dot products of two rows against two rows, sharing their loads.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Dot4(double[] a, int r0, int r1, int c0, int c1, int len, out double s00, out double s01, out double s10, out double s11)
    {
        ref var ra = ref MemoryMarshal.GetArrayDataReference(a);
        s00 = s01 = s10 = s11 = 0;
        var k = 0;
        var w = Vector<double>.Count;
        if (Vector.IsHardwareAccelerated && len >= w)
        {
            Vector<double> a00 = default, a01 = default, a10 = default, a11 = default;
            for (; k <= len - w; k += w)
            {
                var x0 = Vector.LoadUnsafe(ref ra, (nuint)(r0 + k));
                var x1 = Vector.LoadUnsafe(ref ra, (nuint)(r1 + k));
                var y0 = Vector.LoadUnsafe(ref ra, (nuint)(c0 + k));
                var y1 = Vector.LoadUnsafe(ref ra, (nuint)(c1 + k));
                a00 += x0 * y0; a01 += x0 * y1; a10 += x1 * y0; a11 += x1 * y1;
            }
            s00 = Vector.Sum(a00); s01 = Vector.Sum(a01); s10 = Vector.Sum(a10); s11 = Vector.Sum(a11);
        }
        for (; k < len; k++)
        {
            double x0 = Unsafe.Add(ref ra, r0 + k), x1 = Unsafe.Add(ref ra, r1 + k), y0 = Unsafe.Add(ref ra, c0 + k), y1 = Unsafe.Add(ref ra, c1 + k);
            s00 += x0 * y0; s01 += x0 * y1; s10 += x1 * y0; s11 += x1 * y1;
        }
    }

    /// <summary>Solves A·x = b in place (after <see cref="Factor"/>), b in the caller's numbering.</summary>
    public void Solve(double[] b)
    {
        var val = Values();
        var y = new double[_n];
        for (var i = 0; i < _n; i++) y[i] = b[_perm[i]];
        // L·z = y.
        for (var s = 0; s < _ns; s++)
        {
            int f = _first[s], nc = Columns(s);
            var ls = val[s];
            var rows = _rows[s];
            for (var j = 0; j < nc; j++)
            {
                var t = y[f + j];
                for (var k = 0; k < j; k++) t -= ls[j * nc + k] * y[f + k];
                y[f + j] = t / ls[j * nc + j];
            }
            for (var r = nc; r < rows.Length; r++)
            {
                var t = 0.0;
                var rr = r * nc;
                for (var k = 0; k < nc; k++) t += ls[rr + k] * y[f + k];
                y[rows[r]] -= t;
            }
        }
        // Lᵀ·x = z.
        for (var s = _ns - 1; s >= 0; s--)
        {
            int f = _first[s], nc = Columns(s);
            var ls = val[s];
            var rows = _rows[s];
            for (var r = nc; r < rows.Length; r++)
            {
                var yr = y[rows[r]];
                if (yr == 0) continue;
                var rr = r * nc;
                for (var k = 0; k < nc; k++) y[f + k] -= ls[rr + k] * yr;
            }
            for (var j = nc - 1; j >= 0; j--)
            {
                var t = y[f + j] / ls[j * nc + j];
                y[f + j] = t;
                for (var k = 0; k < j; k++) y[f + k] -= ls[j * nc + k] * t;
            }
        }
        for (var i = 0; i < _n; i++) b[_perm[i]] = y[i];
    }
}
