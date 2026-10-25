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
        if (triangles.Count < 4) return null;
        var normals = triangles.Select(t => Vec3.Cross(vertices[t.B] - vertices[t.A], vertices[t.C] - vertices[t.A])).ToArray();

        // Triangles sharing an edge, by the undirected edge.
        var byEdge = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < triangles.Count; i++)
            foreach (var (a, b) in Edges(triangles[i]))
            {
                var k = a < b ? (a, b) : (b, a);
                if (!byEdge.TryGetValue(k, out var l)) byEdge[k] = l = [];
                l.Add(i);
            }

        // Faces: flood fill across edges whose two triangles share a plane.
        var faceOf = Enumerable.Repeat(-1, triangles.Count).ToArray();
        var groups = new List<List<int>>();
        for (int seed = 0; seed < triangles.Count; seed++)
        {
            if (faceOf[seed] >= 0 || normals[seed].Length < 1e-20) continue;
            var n = normals[seed].Normalized();
            var d = Vec3.Dot(n, vertices[triangles[seed].A]);
            var group = new List<int>();
            var stack = new Stack<int>([seed]);
            faceOf[seed] = groups.Count;
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                group.Add(t);
                foreach (var (a, b) in Edges(triangles[t]))
                    foreach (var o in byEdge[a < b ? (a, b) : (b, a)])
                    {
                        if (faceOf[o] >= 0 || normals[o].Length < 1e-20) continue;
                        var on = normals[o].Normalized();
                        if (Vec3.Dot(on, n) < 1 - angleTol) continue;
                        var tri = triangles[o];
                        if (new[] { tri.A, tri.B, tri.C }.Any(v => Math.Abs(Vec3.Dot(n, vertices[v]) - d) > distTol)) continue;
                        faceOf[o] = groups.Count;
                        stack.Push(o);
                    }
            }
            groups.Add(group);
        }

        var faces = new List<FaceInput>();
        foreach (var group in groups)
        {
            var n = group.Aggregate(Vec3.Zero, (s, t) => s + normals[t]).Normalized();
            // The group's boundary: directed edges whose reverse is not in the group.
            var inGroup = new HashSet<(int, int)>();
            foreach (var t in group) foreach (var e in Edges(triangles[t])) inGroup.Add(e);
            var boundary = inGroup.Where(e => !inGroup.Contains((e.Item2, e.Item1))).ToList();
            var (u, v) = VecMath.PlaneBasis(n);
            Vec2 P(int i) => VecMath.Project(vertices[i], u, v);
            var loops = Loops(boundary, P);
            var outers = loops.Where(l => Polygon2.SignedArea(l.Select(P).ToList()) > 0).ToList();
            var holes = loops.Where(l => Polygon2.SignedArea(l.Select(P).ToList()) <= 0).ToList();
            foreach (var outer in outers)
            {
                var poly = outer.Select(P).ToList();
                var mine = holes.Where(h => Polygon2.Contains(poly, Inside(h.Select(P).ToList()))).ToList();
                // A hole inside a nested outer belongs to the innermost one.
                mine = mine.Where(h => !outers.Any(o2 => o2 != outer && Polygon2.Contains(poly, o2.Select(P).First()) && Polygon2.Contains(o2.Select(P).ToList(), Inside(h.Select(P).ToList())))).ToList();
                faces.Add(new FaceInput([.. outer], mine.Select(h => h.ToArray()).ToArray(), n));
            }
        }
        if (faces.Count < 4) return null;
        return SolidBuilder.Make(vertices, faces, tag, 1e-9);
    }

    static IEnumerable<(int, int)> Edges((int A, int B, int C) t) => [(t.A, t.B), (t.B, t.C), (t.C, t.A)];

    /// <summary>A point just inside a loop's first edge, on its left (holes run the other way: inside the hole).</summary>
    static Vec2 Inside(List<Vec2> loop)
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
    static List<List<int>> Loops(List<(int, int)> edges, Func<int, Vec2> p)
    {
        var from = new Dictionary<int, List<int>>();
        foreach (var (a, b) in edges) { if (!from.TryGetValue(a, out var l)) from[a] = l = []; l.Add(b); }
        var used = new HashSet<(int, int)>();
        var loops = new List<List<int>>();
        foreach (var start in edges)
        {
            if (used.Contains(start)) continue;
            var loop = new List<int> { start.Item1 };
            used.Add(start);
            var (prev, cur) = start;
            for (int guard = 0; cur != start.Item1 && guard < edges.Count + 1; guard++)
            {
                loop.Add(cur);
                var options = from[cur].Where(n => !used.Contains((cur, n))).ToList();
                if (options.Count == 0) break;
                var dIn = (p(cur) - p(prev)).Normalized();
                var next = options.Count == 1 ? options[0] : options.MaxBy(n =>
                {
                    var dOut = (p(n) - p(cur)).Normalized();
                    return Math.Atan2(Vec2.Cross(dIn, dOut), Vec2.Dot(dIn, dOut));
                });
                used.Add((cur, next));
                (prev, cur) = (cur, next);
            }
            if (loop.Count >= 3) loops.Add(loop);
        }
        return loops;
    }
}
