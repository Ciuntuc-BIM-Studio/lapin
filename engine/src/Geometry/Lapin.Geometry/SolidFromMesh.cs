namespace Lapin.Geometry;

/// <summary>
/// Booleans between solids, whatever computes them. The kernel builds most
/// elements constructively (exact, no boolean); this is for what cannot be:
/// any solid through any other. A missing implementation is a missing service,
/// not a crash — callers degrade with a diagnostic.
/// </summary>
public interface ISolidBooleans
{
    /// <summary><paramref name="a"/> less every cutter; null when nothing is left.</summary>
    Solid? Difference(Solid a, IReadOnlyList<Solid> cutters, string? tag = null);
    Solid? Union(IReadOnlyList<Solid> solids, string? tag = null);
    Solid? Intersection(Solid a, Solid b, string? tag = null);
}

/// <summary>
/// A closed triangle mesh back into a B-rep with planar faces: triangles that
/// touch and lie in one plane become one face, its boundary loops the outer
/// ring and the holes. Coplanar faces of different inputs that meet become one
/// face too — after a union, one body reads as one body.
/// </summary>
public static class SolidFromMesh
{
    public static Solid? Build(IReadOnlyList<Vec3> vertices, IReadOnlyList<(int A, int B, int C)> triangles, string? tag = null,
        double angleTol = 1e-6, double distTol = 1e-7)
    {
        // Plain loops and edges packed in a long, not LINQ and tuple keys: the browser's AOT build interprets those.
        if (triangles.Count < 4) return null;
        var tris = new (int A, int B, int C)[triangles.Count];
        for (int i = 0; i < tris.Length; i++) tris[i] = triangles[i];
        var normals = new Vec3[tris.Length];
        for (int i = 0; i < tris.Length; i++)
        {
            var t = tris[i];
            normals[i] = Vec3.Cross(vertices[t.B] - vertices[t.A], vertices[t.C] - vertices[t.A]);
        }

        // Triangles sharing an edge, by the undirected edge.
        var byEdge = new Dictionary<long, List<int>>();
        for (int i = 0; i < tris.Length; i++)
            for (int e = 0; e < 3; e++)
            {
                var k = Undirected(Edge(tris[i], e));
                if (!byEdge.TryGetValue(k, out var l)) byEdge[k] = l = [];
                l.Add(i);
            }

        // Faces: flood fill across edges whose two triangles share a plane.
        var faceOf = new int[tris.Length];
        Array.Fill(faceOf, -1);
        var groups = new List<List<int>>();
        for (int seed = 0; seed < tris.Length; seed++)
        {
            if (faceOf[seed] >= 0 || normals[seed].Length < 1e-20) continue;
            var n = normals[seed].Normalized();
            var d = Vec3.Dot(n, vertices[tris[seed].A]);
            var group = new List<int>();
            var stack = new Stack<int>();
            stack.Push(seed);
            faceOf[seed] = groups.Count;
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                group.Add(t);
                for (int e = 0; e < 3; e++)
                    foreach (var o in byEdge[Undirected(Edge(tris[t], e))])
                    {
                        if (faceOf[o] >= 0 || normals[o].Length < 1e-20) continue;
                        var on = normals[o].Normalized();
                        if (Vec3.Dot(on, n) < 1 - angleTol) continue;
                        var tri = tris[o];
                        if (Math.Abs(Vec3.Dot(n, vertices[tri.A]) - d) > distTol || Math.Abs(Vec3.Dot(n, vertices[tri.B]) - d) > distTol
                            || Math.Abs(Vec3.Dot(n, vertices[tri.C]) - d) > distTol) continue;
                        faceOf[o] = groups.Count;
                        stack.Push(o);
                    }
            }
            groups.Add(group);
        }

        var faces = new List<FaceInput>();
        foreach (var group in groups)
        {
            var sum = Vec3.Zero;
            foreach (var t in group) sum += normals[t];
            var n = sum.Normalized();
            // The group's boundary: directed edges whose reverse is not in the group.
            var inGroup = new HashSet<long>();
            foreach (var t in group) for (int e = 0; e < 3; e++) inGroup.Add(Edge(tris[t], e));
            var boundary = new List<long>();
            foreach (var e in inGroup) if (!inGroup.Contains(Pack(To(e), From(e)))) boundary.Add(e);
            var (u, v) = VecMath.PlaneBasis(n);
            var plane = new Plane(vertices, u, v);
            var loops = Loops(boundary, plane);
            var flat = new List<Vec2[]>(loops.Count);
            foreach (var l in loops) flat.Add(plane.Of(l));
            var outers = new List<int>();
            var holes = new List<int>();
            for (int i = 0; i < loops.Count; i++) (Polygon2.SignedArea(flat[i]) > 0 ? outers : holes).Add(i);
            foreach (var outer in outers)
            {
                var poly = flat[outer];
                var mine = new List<int[]>();
                foreach (var h in holes)
                {
                    var inside = Inside(flat[h]);
                    if (!Polygon2.Contains(poly, inside)) continue;
                    // A hole inside a nested outer belongs to the innermost one.
                    var nested = false;
                    foreach (var o2 in outers)
                        if (o2 != outer && Polygon2.Contains(poly, flat[o2][0]) && Polygon2.Contains(flat[o2], inside)) { nested = true; break; }
                    if (!nested) mine.Add([.. loops[h]]);
                }
                faces.Add(new FaceInput([.. loops[outer]], [.. mine], n));
            }
        }
        if (faces.Count < 4) return null;
        return SolidBuilder.Make(vertices, faces, tag, 1e-9);
    }

    /// <summary>A directed edge as one number: its start in the high half, its end in the low.</summary>
    static long Pack(int a, int b) => ((long)a << 32) | (uint)b;
    static int From(long e) => (int)(e >> 32);
    static int To(long e) => (int)e;
    static long Edge((int A, int B, int C) t, int i) => i switch { 0 => Pack(t.A, t.B), 1 => Pack(t.B, t.C), _ => Pack(t.C, t.A) };
    static long Undirected(long e) => From(e) < To(e) ? e : Pack(To(e), From(e));

    /// <summary>The mesh's vertices projected on a face's plane.</summary>
    sealed class Plane(IReadOnlyList<Vec3> vertices, Vec3 u, Vec3 v)
    {
        public Vec2 At(int i) => VecMath.Project(vertices[i], u, v);

        public Vec2[] Of(List<int> ids)
        {
            var o = new Vec2[ids.Count];
            for (int i = 0; i < o.Length; i++) o[i] = At(ids[i]);
            return o;
        }
    }

    /// <summary>A point just inside a loop's first edge, on its left (holes run the other way: inside the hole).</summary>
    static Vec2 Inside(Vec2[] loop)
    {
        var (a, b) = (loop[0], loop[1]);
        var d = (b - a).Normalized();
        return (a + b) * 0.5 - d.Perp * 1e-6;
    }

    /// <summary>
    /// Chain directed boundary edges into closed loops. Where a vertex has more
    /// than one way on (two loops touching at a point), the sharpest left turn is
    /// taken, which keeps every loop simple.
    /// </summary>
    static List<List<int>> Loops(List<long> edges, Plane p)
    {
        var from = new Dictionary<int, List<int>>();
        foreach (var e in edges) { if (!from.TryGetValue(From(e), out var l)) from[From(e)] = l = []; l.Add(To(e)); }
        var used = new HashSet<long>();
        var loops = new List<List<int>>();
        var options = new List<int>();
        foreach (var start in edges)
        {
            if (used.Contains(start)) continue;
            var loop = new List<int> { From(start) };
            used.Add(start);
            int prev = From(start), cur = To(start);
            for (int guard = 0; cur != From(start) && guard < edges.Count + 1; guard++)
            {
                loop.Add(cur);
                options.Clear();
                foreach (var n in from[cur]) if (!used.Contains(Pack(cur, n))) options.Add(n);
                if (options.Count == 0) break;
                var next = options[0];
                if (options.Count > 1)
                {
                    var dIn = (p.At(cur) - p.At(prev)).Normalized();
                    double Turn(int n)
                    {
                        var dOut = (p.At(n) - p.At(cur)).Normalized();
                        return Math.Atan2(Vec2.Cross(dIn, dOut), Vec2.Dot(dIn, dOut));
                    }
                    // The first of the sharpest, as MaxBy.
                    var best = Turn(next);
                    for (int i = 1; i < options.Count; i++)
                    {
                        var turn = Turn(options[i]);
                        if (turn.CompareTo(best) > 0) { best = turn; next = options[i]; }
                    }
                }
                used.Add(Pack(cur, next));
                (prev, cur) = (cur, next);
            }
            if (loop.Count >= 3) loops.Add(loop);
        }
        return loops;
    }
}
