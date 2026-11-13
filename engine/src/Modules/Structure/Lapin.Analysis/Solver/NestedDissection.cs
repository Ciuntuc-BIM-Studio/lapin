namespace Lapin.Analysis.Solver;

/// <summary>
/// A fill-reducing ordering by nested dissection (George, "Nested dissection of a regular finite element mesh", SIAM J.
/// Numer. Anal. 10(2), 1973; George and Liu 1981, ch. 8, the automatic nested dissection by level structures): each
/// connected part is cut by a level of the breadth-first search from a pseudo-peripheral vertex, the smallest level near the
/// middle that leaves both sides at least a fifth of the part, trimmed to the vertices that touch the next level; both sides
/// are dissected the same way and the separator is numbered after them. Parts below <see cref="Leaf"/> vertices are ordered
/// by <see cref="MinimumDegree"/>. For meshes and frames that fill out three dimensions it does better than minimum degree
/// alone; <see cref="SparseCholesky"/> takes whichever predicts less work. Written for Lapin from those descriptions.
/// </summary>
public static class NestedDissection
{
    public const int Leaf = 200;

    public static int[] Order(int n, IReadOnlyList<int>[] adjacent)
    {
        var order = new List<int>(n);
        var inSet = new int[n];
        var level = new int[n];
        var local = new int[n];
        Array.Fill(inSet, -1);
        var stamp = 0;
        var queue = new int[n];

        // The part's vertices reached from a start, by levels: the vertices in reach order and where each level begins.
        (int Count, List<int> Starts) Bfs(int start, int tag)
        {
            var starts = new List<int> { 0 };
            var (h, t) = (0, 0);
            queue[t++] = start;
            level[start] = 0;
            inSet[start] = tag + 1; // seen in this search
            var current = 0;
            while (h < t)
            {
                var v = queue[h];
                if (level[v] != current) { starts.Add(h); current = level[v]; }
                h++;
                foreach (var w in adjacent[v])
                    if (inSet[w] == tag) { inSet[w] = tag + 1; level[w] = level[v] + 1; queue[t++] = w; }
            }
            // Back to members of the part.
            for (var i = 0; i < t; i++) inSet[queue[i]] = tag;
            starts.Add(t);
            return (t, starts);
        }

        void Dissect(int[] verts)
        {
            if (verts.Length == 0) return;
            if (verts.Length <= Leaf) { Leafs(verts); return; }
            var tag = stamp += 2;
            foreach (var v in verts) inSet[v] = tag;
            // Connected parts, each on its own.
            var (count, starts) = Bfs(verts[0], tag);
            if (count < verts.Length)
            {
                var first = queue[..count];
                foreach (var v in first) inSet[v] = -1;
                var rest = verts.Where(v => inSet[v] == tag).ToArray();
                foreach (var v in rest) inSet[v] = -1;
                Dissect(first);
                Dissect(rest);
                return;
            }
            // A pseudo-peripheral start: the far end of the search, while it gets deeper.
            var start = queue[count - 1];
            var depth = starts.Count - 2;
            for (var pass = 0; pass < 6; pass++)
            {
                (count, starts) = Bfs(start, tag);
                var far = queue[count - 1];
                var d = starts.Count - 2;
                if (d <= depth && pass > 0) break;
                if (d > depth) depth = d;
                // The last level's least connected vertex.
                var best = far;
                for (var i = starts[^2]; i < starts[^1]; i++) if (adjacent[queue[i]].Count < adjacent[best].Count) best = queue[i];
                if (best == start) break;
                start = best;
            }
            (count, starts) = Bfs(start, tag);
            var levels = starts.Count - 1;
            if (levels < 3) { foreach (var v in verts) inSet[v] = -1; Leafs(verts); return; }
            // The smallest level that leaves both sides a fifth of the part.
            var (cut, size) = (-1, int.MaxValue);
            for (var l = 1; l < levels - 1; l++)
            {
                var below = starts[l];
                var above = count - starts[l + 1];
                if (below < count / 5 || above < count / 5) continue;
                var s = starts[l + 1] - starts[l];
                if (s < size) (cut, size) = (l, s);
            }
            if (cut < 0)
            {
                // No balanced level: the middle one.
                cut = 1;
                while (cut < levels - 2 && starts[cut + 1] < count / 2) cut++;
            }
            var lower = new List<int>(starts[cut]);
            var upper = new List<int>(count - starts[cut + 1]);
            var sep = new List<int>();
            for (var i = 0; i < starts[cut]; i++) lower.Add(queue[i]);
            for (var i = starts[cut + 1]; i < count; i++) upper.Add(queue[i]);
            for (var i = starts[cut]; i < starts[cut + 1]; i++)
            {
                var v = queue[i];
                var touches = false;
                foreach (var w in adjacent[v]) if (inSet[w] == tag && level[w] == cut + 1) { touches = true; break; }
                (touches ? sep : lower).Add(v);
            }
            foreach (var v in verts) inSet[v] = -1;
            Dissect(lower.ToArray());
            Dissect(upper.ToArray());
            order.AddRange(sep);
        }

        void Leafs(int[] verts)
        {
            for (var i = 0; i < verts.Length; i++) local[verts[i]] = i;
            var tag = stamp += 2;
            foreach (var v in verts) inSet[v] = tag;
            var sub = new IReadOnlyList<int>[verts.Length];
            for (var i = 0; i < verts.Length; i++)
            {
                var l = new List<int>();
                foreach (var w in adjacent[verts[i]]) if (inSet[w] == tag) l.Add(local[w]);
                sub[i] = l;
            }
            foreach (var v in verts) inSet[v] = -1;
            foreach (var i in MinimumDegree.Order(verts.Length, sub)) order.Add(verts[i]);
        }

        Dissect(Enumerable.Range(0, n).ToArray());
        return order.ToArray();
    }
}
