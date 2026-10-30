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

    /// <summary>
    /// One render mesh for all the solids of ONE element, read as a single body:
    /// <list type="bullet">
    /// <item>faces where two of its pieces touch (the same face, opposite ways) are inside it and dropped;</item>
    /// <item>an edge is drawn only where the faces meeting there turn by more than <paramref name="creaseDeg"/> —
    /// so the seam between two coplanar pieces (a ring's side and its corner) is not a line.</item>
    /// </list>
    /// This is the rule every view of an element follows — 3D now, elevations and HLR next —
    /// so building an element from several exact pieces never shows as false joints.
    /// </summary>
    public static TriMesh TessellateMany(IReadOnlyList<Solid> solids, double creaseDeg = 1)
    {
        if (solids.Count == 1) return Tessellate(solids[0]);
        var pos = new List<float>(); var nor = new List<float>(); var idx = new List<int>(); var edges = new List<float>();

        // Weld across pieces: a shared corner is one vertex whichever piece it came from.
        var weld = new Dictionary<(long, long, long), int>();
        var points = new List<Vec3>();
        int W(Vec3 p)
        {
            var k = ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6), (long)Math.Round(p.Z * 1e6));
            if (!weld.TryGetValue(k, out var id)) { id = points.Count; points.Add(p); weld[k] = id; }
            return id;
        }
        var faces = new List<(Solid S, Face F, int[] Outer)>();
        foreach (var s in solids)
            foreach (var f in s.Faces)
                faces.Add((s, f, f.Outer.Select(i => W(s.Vertices[i])).ToArray()));

        // Faces two pieces share (same corners, opposite normals) are internal.
        static string Key(int[] loop) => string.Join(",", loop.Order());
        var byKey = faces.Select((f, i) => (Key(f.Outer), i)).GroupBy(x => x.Item1).ToDictionary(x => x.Key, x => x.Select(y => y.i).ToList());
        var drop = new HashSet<int>();
        foreach (var group in byKey.Values.Where(v => v.Count > 1))
            foreach (var a in group)
                foreach (var b in group)
                    if (a < b && faces[a].F.Holes.Length == 0 && faces[b].F.Holes.Length == 0 && Vec3.Dot(faces[a].F.Normal, faces[b].F.Normal) < -0.999)
                    { drop.Add(a); drop.Add(b); }

        var edgeNormals = new Dictionary<(int, int), List<Vec3>>();
        for (int fi = 0; fi < faces.Count; fi++)
        {
            if (drop.Contains(fi)) continue;
            var (s, f, _) = faces[fi];
            var local = new Dictionary<int, int>();
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
            foreach (var (a, b, c) in Triangulator.Face(s.Vertices, f.Outer, f.Holes, f.Normal)) { idx.Add(Vi(a)); idx.Add(Vi(b)); idx.Add(Vi(c)); }
            foreach (var loop in new[] { f.Outer }.Concat(f.Holes))
                for (int i = 0; i < loop.Length; i++)
                {
                    int a = W(s.Vertices[loop[i]]), b = W(s.Vertices[loop[(i + 1) % loop.Length]]);
                    if (a == b) continue;
                    var k = a < b ? (a, b) : (b, a);
                    if (!edgeNormals.TryGetValue(k, out var l)) edgeNormals[k] = l = [];
                    l.Add(f.Normal);
                }
        }
        var cos = Math.Cos(creaseDeg * Math.PI / 180);
        var creases = new List<(int A, int B)>();
        foreach (var ((a, b), normals) in edgeNormals)
        {
            bool crease = normals.Count == 1;
            for (int i = 0; i < normals.Count && !crease; i++)
                for (int j = i + 1; j < normals.Count && !crease; j++)
                    if (Vec3.Dot(normals[i], normals[j]) < cos) crease = true;
            if (crease) creases.Add((a, b));
        }
        // One line per straight edge: segments split where two pieces met are joined back.
        foreach (var (a, b) in JoinCollinear(creases, points))
        {
            var p = points[a]; var q = points[b];
            edges.AddRange([(float)p.X, (float)p.Y, (float)p.Z, (float)q.X, (float)q.Y, (float)q.Z]);
        }
        return new TriMesh([.. pos], [.. nor], [.. idx], [.. edges]);
    }

    /// <summary>Merge chains of collinear segments through vertices where only those two meet.</summary>
    static List<(int, int)> JoinCollinear(List<(int A, int B)> segs, List<Vec3> pts)
    {
        var at = new Dictionary<int, List<int>>();
        for (int i = 0; i < segs.Count; i++)
            foreach (var v in new[] { segs[i].A, segs[i].B })
                (at.TryGetValue(v, out var l) ? l : at[v] = []).Add(i);
        var alive = Enumerable.Repeat(true, segs.Count).ToArray();
        var cur = segs.ToArray();
        foreach (var (v, list) in at)
        {
            var live = list.Where(i => alive[i]).Distinct().ToList();
            if (live.Count != 2) continue;
            int i0 = live[0], i1 = live[1];
            int a = cur[i0].A == v ? cur[i0].B : cur[i0].A;
            int b = cur[i1].A == v ? cur[i1].B : cur[i1].A;
            var d0 = (pts[v] - pts[a]).Normalized();
            var d1 = (pts[b] - pts[v]).Normalized();
            if (Vec3.Dot(d0, d1) < 1 - 1e-9) continue;
            // Replace the pair by one segment a–b, re-pointing b's list at it.
            cur[i0] = (a, b);
            alive[i1] = false;
            if (at.TryGetValue(b, out var lb)) { lb.Remove(i1); lb.Add(i0); }
        }
        return [.. Enumerable.Range(0, cur.Length).Where(i => alive[i]).Select(i => cur[i])];
    }
}
