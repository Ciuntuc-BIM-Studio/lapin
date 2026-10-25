namespace Lapin.Geometry;

/// <summary>
/// Where one material's surface goes on from one solid into another — a wall's
/// render over the storey above, the next wall of a straight facade — the edge
/// between them is a seam, not a line: the surface reads as one. The rule the
/// drawings follow (<see cref="HiddenLines"/>, the plan's material drawing),
/// here for the edges of the 3D view. Who joins whom (the same material) is the
/// caller's: it hands over the neighbours' faces that qualify.
/// </summary>
public static class Seams
{
    const double CoplanarCos = 0.99999; // ~0.26°
    const double PlaneTol = 1e-5;       // m, as the hidden lines' depth
    const double Beside = 1e-4;         // m: how far across the edge "the other side" is tested
    const double BoxTol = 1e-3;         // m: faces whose boxes miss by more cannot touch

    /// <summary>A planar face ready to be tested against: its plane, a basis in it and its rings in that basis.</summary>
    public sealed class Plane
    {
        public required Vec3 Normal;
        public required double D;
        public required Vec3 U, V;
        public required Vec2[][] Rings;
        public required Vec3 Min, Max;

        public Vec2 Map(Vec3 p) => VecMath.Project(p, U, V);

        /// <summary>The same plane, facing the same way: a surface that goes on, not one that meets this one face to face.</summary>
        public bool Coplanar(Plane f) => Vec3.Dot(Normal, f.Normal) > CoplanarCos && Math.Abs(D - f.D) < PlaneTol;

        public bool Touches(Plane f) =>
            Min.X <= f.Max.X + BoxTol && f.Min.X <= Max.X + BoxTol && Min.Y <= f.Max.Y + BoxTol && f.Min.Y <= Max.Y + BoxTol && Min.Z <= f.Max.Z + BoxTol && f.Min.Z <= Max.Z + BoxTol;
    }

    /// <summary>Every face of the solid, ready to be tested against.</summary>
    public static List<Plane> Planes(Solid s)
    {
        var o = new List<Plane>(s.Faces.Length);
        foreach (var f in s.Faces)
        {
            if (f.Outer.Length < 3) continue;
            var n = f.Normal;
            var (u, v) = VecMath.PlaneBasis(n);
            var loops = new List<int[]> { f.Outer };
            loops.AddRange(f.Holes);
            var rings = new Vec2[loops.Count][];
            Vec3 min = s.Vertices[f.Outer[0]], max = min;
            for (var li = 0; li < loops.Count; li++)
            {
                var loop = loops[li];
                var ring = new Vec2[loop.Length];
                for (var i = 0; i < loop.Length; i++)
                {
                    var p = s.Vertices[loop[i]];
                    ring[i] = VecMath.Project(p, u, v);
                    min = new Vec3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                    max = new Vec3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
                }
                rings[li] = ring;
            }
            o.Add(new Plane { Normal = n, D = Vec3.Dot(n, s.Vertices[f.Outer[0]]), U = u, V = v, Rings = rings, Min = min, Max = max });
        }
        return o;
    }

    /// <summary>
    /// The solid's edges to draw — each once, as the tessellator emits them —
    /// less the stretches where a face of the edge goes on, in its own plane,
    /// into one of the <paramref name="neighbours"/>' faces: the surface
    /// continues there, so the edge is a seam. Where only part of the edge is
    /// covered (the wall above is shorter), the rest stays a line.
    /// </summary>
    public static List<(Vec3 A, Vec3 B)> Edges(Solid s, IReadOnlyList<Plane> neighbours)
    {
        var own = new Plane?[s.Faces.Length];
        var planes = Planes(s);
        // Planes() skips degenerate faces: match them back by index.
        for (int fi = 0, pi = 0; fi < s.Faces.Length; fi++)
            if (s.Faces[fi].Outer.Length >= 3) own[fi] = planes[pi++];
        // Per face, the neighbour faces its plane goes on into.
        var onward = new List<Plane>?[s.Faces.Length];
        for (var fi = 0; fi < own.Length; fi++)
        {
            if (own[fi] is not { } f) continue;
            List<Plane>? list = null;
            foreach (var n in neighbours)
                if (n.Touches(f) && n.Coplanar(f)) (list ??= []).Add(n);
            onward[fi] = list;
        }

        var topo = Topology.Build(s);
        var o = new List<(Vec3, Vec3)>(topo.ByEdge.Count);
        var covered = new List<(double, double)>();
        foreach (var uses in topo.ByEdge.Values)
        {
            var h = topo.HalfEdges[uses[0]];
            Vec3 a = s.Vertices[h.From], b = s.Vertices[h.To];
            covered.Clear();
            foreach (var use in uses)
            {
                var fi = topo.HalfEdges[use].Face;
                if (onward[fi] is { } list && own[fi] is { } f) Cover(f, list, a, b, covered);
            }
            if (covered.Count == 0) { o.Add((a, b)); continue; }
            foreach (var (t0, t1) in Polygon2.Outside(covered))
                if (t1 - t0 > 1e-9) o.Add((a + (b - a) * t0, a + (b - a) * t1));
        }
        return o;
    }

    /// <summary>The stretches of edge a→b of face <paramref name="f"/> whose other side (across the edge, in the face's plane) lies inside one of the faces the plane goes on into.</summary>
    static void Cover(Plane f, List<Plane> onward, Vec3 a, Vec3 b, List<(double, double)> covered)
    {
        var across = Vec3.Cross(f.Normal, b - a);
        var l = across.Length;
        if (l < 1e-12) return;
        var off = across * (Beside / l);
        var mid = (a + b) * 0.5;
        // Across the edge means away from the face itself.
        if (Polygon2.Inside(f.Rings, f.Map(mid + off))) off = -off;
        foreach (var n in onward) Polygon2.InsideIntervals(n.Rings, n.Map(a + off), n.Map(b + off), 0, 1, covered);
    }

    /// <summary>The edges as the render mesh carries them: six floats a segment.</summary>
    public static float[] Flat(List<(Vec3 A, Vec3 B)> edges)
    {
        var o = new float[edges.Count * 6];
        for (var i = 0; i < edges.Count; i++)
        {
            var (a, b) = edges[i];
            o[i * 6] = (float)a.X; o[i * 6 + 1] = (float)a.Y; o[i * 6 + 2] = (float)a.Z;
            o[i * 6 + 3] = (float)b.X; o[i * 6 + 4] = (float)b.Y; o[i * 6 + 5] = (float)b.Z;
        }
        return o;
    }
}
