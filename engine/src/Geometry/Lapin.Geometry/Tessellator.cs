namespace Lapin.Geometry;

/// <summary>
/// Render-ready triangles: flat shaded (vertices duplicated per face), plus the
/// feature edges (where the surface folds, or ends) as line segments. Still project space, Z up.
/// </summary>
public sealed record TriMesh(float[] Positions, float[] Normals, int[] Indices, float[] Edges)
{
    public static readonly TriMesh Empty = new([], [], [], []);

    public static TriMesh Merge(IEnumerable<TriMesh> meshes)
    {
        var p = new List<float>(); var n = new List<float>(); var ix = new List<int>(); var e = new List<float>();
        foreach (var m in meshes)
        {
            int baseV = p.Count / 3;
            p.AddRange(m.Positions); n.AddRange(m.Normals); e.AddRange(m.Edges);
            ix.AddRange(m.Indices.Select(i => i + baseV));
        }
        return new TriMesh([.. p], [.. n], [.. ix], [.. e]);
    }
}

public static class Tessellator
{
    /// <summary>
    /// The fold under which an edge between two faces of one solid is not drawn
    /// (degrees): the triangles of a body read from a file, the facets of a
    /// curved surface and a slab's slight fall show as one surface; a fold of
    /// more, and an open edge, draw.
    /// </summary>
    public const double CreaseDeg = 20;

    /// <summary>The solid as triangles, flat shaded, with its crease edges (a fold of more than <paramref name="creaseDeg"/>, or an open edge).</summary>
    public static TriMesh Tessellate(Solid s, double creaseDeg = CreaseDeg)
    {
        var pos = new List<float>(s.Faces.Length * 9); var nor = new List<float>(s.Faces.Length * 9); var idx = new List<int>(s.Faces.Length * 3);
        var local = new Dictionary<int, int>();
        // Each edge once, by its two vertices, with the normals of the faces that use it.
        var edgeNormals = new Dictionary<(int, int), (Vec3 First, bool Crease, int Uses)>();
        var order = new List<(int, int)>();
        var cos = Math.Cos(creaseDeg * Math.PI / 180);
        void Use(int a, int b, Vec3 n)
        {
            if (a == b) return;
            var k = a < b ? (a, b) : (b, a);
            if (edgeNormals.TryGetValue(k, out var e)) edgeNormals[k] = (e.First, e.Crease || Vec3.Dot(e.First, n) < cos, e.Uses + 1);
            else { edgeNormals[k] = (n, false, 1); order.Add(k); }
        }
        foreach (var f in s.Faces)
        {
            // A triangle (a mesh read as it is, face by face): its three corners straight in, no triangulation.
            if (f.Outer.Length == 3 && f.Holes.Length == 0)
            {
                var k0 = pos.Count / 3;
                for (var c = 0; c < 3; c++)
                {
                    var p = s.Vertices[f.Outer[c]];
                    pos.Add((float)p.X); pos.Add((float)p.Y); pos.Add((float)p.Z);
                    nor.Add((float)f.Normal.X); nor.Add((float)f.Normal.Y); nor.Add((float)f.Normal.Z);
                    idx.Add(k0 + c);
                }
                for (var c = 0; c < 3; c++) Use(f.Outer[c], f.Outer[(c + 1) % 3], f.Normal);
                continue;
            }
            local.Clear();
            int Vi(int id)
            {
                if (local.TryGetValue(id, out var k)) return k;
                k = pos.Count / 3;
                var p = s.Vertices[id];
                pos.Add((float)p.X); pos.Add((float)p.Y); pos.Add((float)p.Z);
                nor.Add((float)f.Normal.X); nor.Add((float)f.Normal.Y); nor.Add((float)f.Normal.Z);
                local[id] = k;
                return k;
            }
            foreach (var (a, b, c) in Triangulator.Face(s.Vertices, f.Outer, f.Holes, f.Normal))
            {
                idx.Add(Vi(a)); idx.Add(Vi(b)); idx.Add(Vi(c));
            }
            for (int h = -1; h < f.Holes.Length; h++)
            {
                var loop = h < 0 ? f.Outer : f.Holes[h];
                for (int i = 0; i < loop.Length; i++) Use(loop[i], loop[(i + 1) % loop.Length], f.Normal);
            }
        }
        // An edge of one face only (the body is open there) or where the faces fold.
        var edges = new List<float>(order.Count * 6);
        foreach (var k in order)
        {
            var e = edgeNormals[k];
            if (e.Uses != 1 && !e.Crease) continue;
            var p = s.Vertices[k.Item1]; var q = s.Vertices[k.Item2];
            edges.Add((float)p.X); edges.Add((float)p.Y); edges.Add((float)p.Z); edges.Add((float)q.X); edges.Add((float)q.Y); edges.Add((float)q.Z);
        }
        return new TriMesh([.. pos], [.. nor], [.. idx], [.. edges]);
    }
}
