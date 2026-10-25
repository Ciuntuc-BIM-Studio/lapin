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
        var buckets = new Dictionary<(long, long, long), List<int>>();
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
                if (!buckets.TryGetValue((ci + di, cj + dj, ck + dk), out var bucket)) continue;
                foreach (var vi in bucket)
                    if (Vec3.DistanceSquared(vertices[vi], p) <= tol2) { found = vi; break; }
            }
            if (found < 0)
            {
                found = vertices.Count;
                vertices.Add(p);
                var k = (ci, cj, ck);
                if (buckets.TryGetValue(k, out var b)) b.Add(found); else buckets[k] = [found];
            }
            remap[n] = found;
        }
        return (vertices, remap);
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
        foreach (var f in faces)
        {
            var outer = CleanLoop(f.Outer, remap);
            if (outer.Length < 3) continue;
            var raw = VecMath.NewellNormal(outer.Select(i => pool[i]).ToArray());
            var area = raw.Length;
            if (area < Tolerance.Area) continue;
            var normal = raw * (1 / area);
            var holes = (f.Holes ?? []).Select(h => CleanLoop(h, remap)).Where(h => h.Length >= 3).ToArray();

            // The caller's normal decides orientation: flip the winding to match it.
            if (f.Normal is { } want && Vec3.Dot(normal, want) < 0)
            {
                normal = -normal;
                outer = outer.Reverse().ToArray();
                holes = holes.Select(h => h.Reverse().ToArray()).ToArray();
            }
            outFaces.Add(new Face(outer, holes, normal, f.Tag));
        }
        return Compact(new Solid([.. pool], [.. outFaces], tag));
    }

    /// <summary>Drop vertices no face uses, renumbering the loops.</summary>
    static Solid Compact(Solid s)
    {
        var used = new Dictionary<int, int>();
        var verts = new List<Vec3>();
        int Id(int v)
        {
            if (!used.TryGetValue(v, out var n)) { n = verts.Count; verts.Add(s.Vertices[v]); used[v] = n; }
            return n;
        }
        var faces = s.Faces.Select(f => f with
        {
            Outer = f.Outer.Select(Id).ToArray(),
            Holes = f.Holes.Select(h => h.Select(Id).ToArray()).ToArray(),
        }).ToArray();
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
