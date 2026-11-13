namespace Lapin.Analysis.Solver;

/// <summary>
/// A fill-reducing ordering for a symmetric sparse matrix: approximate minimum degree on the quotient graph (docs/plan-fem.md
/// §10.6). Each step eliminates a vertex of least degree; the vertices it joins become one <em>element</em> (the clique the
/// elimination makes), so the graph never grows. A vertex's degree is bounded the way Amestoy, Davis and Duff bound it
/// ("An approximate minimum degree ordering algorithm", SIAM J. Matrix Anal. Appl. 17(4), 1996, §3):
/// d̄ᵢ = min(n − k, dᵢ + |Lₚ \ i|, |Aᵢ \ i| + |Lₚ \ i| + Σ_{e ∈ Eᵢ \ p} |Lₑ \ Lₚ|), the last sum taken in one pass over the
/// new element; an element whose variables all lie in the new one is absorbed into it. The quotient graph itself is
/// George and Liu's ("Computer Solution of Large Sparse Positive Definite Systems", Prentice-Hall 1981, ch. 5; "The evolution
/// of the minimum degree ordering algorithm", SIAM Review 31(1), 1989). Written for Lapin from those descriptions.
/// </summary>
public static class MinimumDegree
{
    /// <summary>The elimination order: the k-th vertex to eliminate. <paramref name="adjacent"/> is symmetric, without self loops.</summary>
    public static int[] Order(int n, IReadOnlyList<int>[] adjacent)
    {
        var order = new int[n];
        if (n == 0) return order;
        var adj = new List<int>[n];
        var elems = new List<int>[n];
        var members = new int[n][];
        var degree = new int[n];
        var eliminated = new bool[n];
        var dead = new bool[n];
        var mark = new int[n];
        var wMark = new int[n];
        var w = new int[n];
        Array.Fill(mark, -1);
        Array.Fill(wMark, -1);
        // Degree buckets: doubly linked lists, a vertex's latest entered at the head.
        var head = new int[n + 1];
        var next = new int[n];
        var prev = new int[n];
        Array.Fill(head, -1);
        void Insert(int v, int d)
        {
            degree[v] = d;
            prev[v] = -1;
            next[v] = head[d];
            if (head[d] >= 0) prev[head[d]] = v;
            head[d] = v;
        }
        void Remove(int v)
        {
            if (prev[v] >= 0) next[prev[v]] = next[v]; else head[degree[v]] = next[v];
            if (next[v] >= 0) prev[next[v]] = prev[v];
        }
        for (var v = 0; v < n; v++)
        {
            adj[v] = adjacent[v].Where(u => u != v).Distinct().ToList();
            elems[v] = [];
            Insert(v, Math.Min(adj[v].Count, n - 1));
        }

        var minDeg = 0;
        var lp = new List<int>();
        for (var k = 0; k < n; k++)
        {
            while (head[minDeg] < 0) minDeg++;
            var p = head[minDeg];
            Remove(p);
            var stamp = k;

            // The new element: p's variables and the variables of the elements it meets, these absorbed.
            lp.Clear();
            mark[p] = stamp;
            foreach (var v in adj[p])
                if (!eliminated[v] && mark[v] != stamp) { mark[v] = stamp; lp.Add(v); }
            foreach (var e in elems[p])
            {
                if (dead[e]) continue;
                foreach (var v in members[e])
                    if (!eliminated[v] && mark[v] != stamp) { mark[v] = stamp; lp.Add(v); }
                dead[e] = true;
                members[e] = [];
            }
            eliminated[p] = true;
            members[p] = lp.ToArray();
            adj[p] = [];
            elems[p] = [];
            order[k] = p;
            var lpCount = lp.Count;
            var remaining = n - k - 1;

            // |Lₑ \ Lₚ| for every live element next to the new one.
            foreach (var i in lp)
                foreach (var e in elems[i])
                {
                    if (dead[e]) continue;
                    if (wMark[e] != stamp) { wMark[e] = stamp; w[e] = members[e].Length - 1; }
                    else w[e]--;
                }

            foreach (var i in lp)
            {
                var es = elems[i];
                var ext = 0;
                var kept = 0;
                for (var x = 0; x < es.Count; x++)
                {
                    var e = es[x];
                    if (dead[e]) continue;
                    if (w[e] == 0) { dead[e] = true; members[e] = []; continue; }
                    ext += w[e];
                    es[kept++] = e;
                }
                es.RemoveRange(kept, es.Count - kept);
                es.Add(p);
                var av = adj[i];
                kept = 0;
                for (var x = 0; x < av.Count; x++)
                {
                    var v = av[x];
                    if (eliminated[v] || mark[v] == stamp) continue;
                    av[kept++] = v;
                }
                av.RemoveRange(kept, av.Count - kept);
                var d = Math.Min(Math.Max(remaining - 1, 0), Math.Min(degree[i] + lpCount - 1, av.Count + lpCount - 1 + ext));
                if (d < 0) d = 0;
                Remove(i);
                Insert(i, d);
                if (d < minDeg) minDeg = d;
            }
        }
        return order;
    }
}
