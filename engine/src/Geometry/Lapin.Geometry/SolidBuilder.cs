namespace Lapin.Geometry;

public static class SolidBuilder
{
    /// <summary>
    /// Merge points within <paramref name="tol"/> into one vertex via a uniform
    /// spatial hash; the 3×3×3 neighbour sweep catches points straddling a cell edge.
    /// </summary>
    public static (List<Vec3> Vertices, int[] Remap) Weld(IReadOnlyList<Vec3> pts, double tol)
    {
        var cell = Math.Max(tol, 1e-12);
        // A concrete key and comparer: the browser's AOT build compiles them, where a tuple key falls back to its interpreter.
        var buckets = new Dictionary<Cell, List<int>>(CellComparer.Instance);
        var vertices = new List<Vec3>();
        var remap = new int[pts.Count];
        var tol2 = tol * tol;

        for (int n = 0; n < pts.Count; n++)
        {
            var p = pts[n];
            long ci = (long)Math.Floor(p.X / cell), cj = (long)Math.Floor(p.Y / cell), ck = (long)Math.Floor(p.Z / cell);
            int found = -1;
            for (long di = -1; di <= 1 && found < 0; di++)
            for (long dj = -1; dj <= 1 && found < 0; dj++)
            for (long dk = -1; dk <= 1 && found < 0; dk++)
            {
                if (!buckets.TryGetValue(new Cell(ci + di, cj + dj, ck + dk), out var bucket)) continue;
                for (int b = 0; b < bucket.Count; b++)
                {
                    var vi = bucket[b];
                    if (Vec3.DistanceSquared(vertices[vi], p) <= tol2) { found = vi; break; }
                }
            }
            if (found < 0)
            {
                found = vertices.Count;
                vertices.Add(p);
                var k = new Cell(ci, cj, ck);
                if (buckets.TryGetValue(k, out var b)) b.Add(found); else buckets[k] = [found];
            }
            remap[n] = found;
        }
        return (vertices, remap);
    }

    readonly struct Cell(long i, long j, long k)
    {
        public readonly long I = i, J = j, K = k;
    }

    sealed class CellComparer : IEqualityComparer<Cell>
    {
        public static readonly CellComparer Instance = new();
        public bool Equals(Cell a, Cell b) => a.I == b.I && a.J == b.J && a.K == b.K;
        public int GetHashCode(Cell c) => unchecked((int)(c.I * 73856093 ^ c.J * 19349663 ^ c.K * 83492791) ^ (int)((c.I ^ c.J ^ c.K) >> 32));
    }

    static int[] CleanLoop(int[] loop, int[] remap)
    {
        var o = new List<int>(loop.Length);
        foreach (var id in loop)
        {
            if (id < 0 || id >= remap.Length) continue;
            var v = remap[id];
            if (o.Count > 0 && o[^1] == v) continue;
            o.Add(v);
        }
        while (o.Count > 1 && o[0] == o[^1]) o.RemoveAt(o.Count - 1);
        return [.. o];
    }

    /// <summary>
    /// Build a solid from a vertex pool and face loops. Faces that collapse after
    /// welding are dropped silently (zero-length segments are expected input).
    /// Welding renumbers vertices: ids mean something only inside the result.
    /// </summary>
    public static Solid Make(IReadOnlyList<Vec3> vertices, IEnumerable<FaceInput> faces, string? tag = null, double tol = Tolerance.Distance)
    {
        var (pool, remap) = Weld(vertices, tol);
        var outFaces = new List<Face>();
        // Plain loops, not LINQ: the browser's AOT build runs these through its interpreter otherwise.
        foreach (var f in faces)
        {
            var outer = CleanLoop(f.Outer, remap);
            if (outer.Length < 3) continue;
            var ring = new Vec3[outer.Length];
            for (int i = 0; i < outer.Length; i++) ring[i] = pool[outer[i]];
            var raw = VecMath.NewellNormal(ring);
            var area = raw.Length;
            if (area < Tolerance.Area) continue;
            var normal = raw * (1 / area);
            var holes = new List<int[]>();
            if (f.Holes != null)
                foreach (var h in f.Holes)
                {
                    var c = CleanLoop(h, remap);
                    if (c.Length >= 3) holes.Add(c);
                }

            // The caller's normal decides orientation: flip the winding to match it.
            if (f.Normal is { } want && Vec3.Dot(normal, want) < 0)
            {
                normal = -normal;
                Array.Reverse(outer);
                foreach (var h in holes) Array.Reverse(h);
            }
            outFaces.Add(new Face(outer, [.. holes], normal, f.Tag));
        }
        return Compact(new Solid([.. pool], [.. outFaces], tag));
    }

    /// <summary>Drop vertices no face uses, renumbering the loops.</summary>
    static Solid Compact(Solid s)
    {
        var used = new int[s.Vertices.Length];
        Array.Fill(used, -1);
        var verts = new List<Vec3>();
        int[] Renumber(int[] loop)
        {
            var o = new int[loop.Length];
            for (int i = 0; i < loop.Length; i++)
            {
                var v = loop[i];
                if (used[v] < 0) { used[v] = verts.Count; verts.Add(s.Vertices[v]); }
                o[i] = used[v];
            }
            return o;
        }
        var faces = new Face[s.Faces.Length];
        for (int i = 0; i < faces.Length; i++)
        {
            var f = s.Faces[i];
            var outer = Renumber(f.Outer);
            var holes = new int[f.Holes.Length][];
            for (int h = 0; h < holes.Length; h++) holes[h] = Renumber(f.Holes[h]);
            faces[i] = f with { Outer = outer, Holes = holes };
        }
        return s with { Vertices = [.. verts], Faces = faces };
    }

    public static Solid Translate(Solid s, Vec3 d) => s with { Vertices = s.Vertices.Select(p => p + d).ToArray() };

    /// <summary>Map every vertex and re-derive normals; a mirroring map is repaired by flipping.</summary>
    public static Solid Transform(Solid s, Func<Vec3, Vec3> fn)
    {
        var verts = s.Vertices.Select(fn).ToArray();
        var faces = s.Faces.Select(f =>
        {
            var raw = VecMath.NewellNormal(f.Outer.Select(i => verts[i]).ToArray());
            var a = raw.Length;
            return f with { Normal = a > 0 ? raw * (1 / a) : f.Normal };
        }).ToArray();
        var o = s with { Vertices = verts, Faces = faces };
        return Measure.SignedVolume(o) < 0 ? Flip(o) : o;
    }

    /// <summary>Reverse every face: turns the solid inside out.</summary>
    public static Solid Flip(Solid s) => s with
    {
        Faces = s.Faces.Select(f => f with
        {
            Outer = f.Outer.Reverse().ToArray(),
            Holes = f.Holes.Select(h => h.Reverse().ToArray()).ToArray(),
            Normal = -f.Normal,
        }).ToArray(),
    };
}
