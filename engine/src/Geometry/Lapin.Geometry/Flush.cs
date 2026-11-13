namespace Lapin.Geometry;

/// <summary>
/// A boolean's tool whose faces lie on its target's faces, moved a hair so the two never share a plane. A boolean on
/// faces only nearly coplanar (a tool built through a plane's sines and cosines, a box turned 17°) keeps slivers
/// of zero thickness, which come back as pairs of faces back to back (membranes) and loose edges. Only the faces
/// that lie on one of the target's, overlapping it, move; a vertex they share with faces that stay goes where its
/// faces' planes meet again, so the faces that stay keep their planes and the result's volume is exact.
/// </summary>
public static class Flush
{
    /// <summary>How far a face is moved off the target's (0.1 mm).</summary>
    public const double By = 1e-4;

    /// <summary>How near a face's corners must lie to the target's face for the two to count as one plane.</summary>
    public const double On = 1e-6;

    /// <summary>
    /// A cutter (a difference's tool, an intersection's other operand) with each face lying on a face of
    /// <paramref name="host"/> that faces the same way moved out of the host by <paramref name="by"/>.
    /// </summary>
    public static Solid Proud(Solid tool, Solid host, double by = By, double tol = On) => Move(tool, host, same: true, by, tol);

    /// <summary>
    /// A union's tool with each face lying on a face of <paramref name="host"/> that faces the other way (the two touching)
    /// moved into the host by <paramref name="by"/>, when the face lies within the host's, so nothing sticks out.
    /// </summary>
    public static Solid Sunk(Solid tool, Solid host, double by = By, double tol = On) => Move(tool, host, same: false, by, tol);

    static Solid Move(Solid t, Solid host, bool same, double by, double tol)
    {
        if (t.Faces.Length == 0 || host.Faces.Length == 0) return t;
        var n = t.Faces.Select(f => f.Normal.Normalized()).ToArray();
        var hn = host.Faces.Select(f => f.Normal.Normalized()).ToArray();
        var hd = host.Faces.Select((f, i) => Vec3.Dot(hn[i], host.Vertices[f.Outer[0]])).ToArray();
        var moved = new bool[t.Faces.Length];
        for (var i = 0; i < t.Faces.Length; i++)
        {
            var f = t.Faces[i];
            for (var j = 0; j < host.Faces.Length && !moved[i]; j++)
            {
                var dot = Vec3.Dot(hn[j], n[i]);
                if (same ? dot < 1 - 1e-9 : dot > -1 + 1e-9) continue;
                if (!Loops(f).SelectMany(l => l).All(v => Math.Abs(Vec3.Dot(hn[j], t.Vertices[v]) - hd[j]) < tol)) continue;
                moved[i] = same ? Overlap(t, f, host, host.Faces[j], hn[j]) : Within(t, f, host, host.Faces[j], hn[j], tol);
            }
        }
        if (!moved.Any(x => x)) return t;
        var inc = Enumerable.Range(0, t.Vertices.Length).Select(_ => new List<int>()).ToArray();
        for (var i = 0; i < t.Faces.Length; i++)
            foreach (var v in Loops(t.Faces[i]).SelectMany(l => l))
                if (!inc[v].Contains(i)) inc[v].Add(i);
        var verts = t.Vertices.ToArray();
        for (var v = 0; v < verts.Length; v++)
        {
            if (!inc[v].Any(i => moved[i])) continue;
            // δ minimising Σ (n_i·δ − Δd_i)² (+ a whisker of |δ|²): exact where three planes meet, the least move on an edge.
            double a00 = 1e-12, a01 = 0, a02 = 0, a11 = 1e-12, a12 = 0, a22 = 1e-12, b0 = 0, b1 = 0, b2 = 0;
            foreach (var i in inc[v])
            {
                var q = n[i];
                var dd = moved[i] ? by : 0;
                a00 += q.X * q.X; a01 += q.X * q.Y; a02 += q.X * q.Z; a11 += q.Y * q.Y; a12 += q.Y * q.Z; a22 += q.Z * q.Z;
                b0 += q.X * dd; b1 += q.Y * dd; b2 += q.Z * dd;
            }
            var det = a00 * (a11 * a22 - a12 * a12) - a01 * (a01 * a22 - a12 * a02) + a02 * (a01 * a12 - a11 * a02);
            var dx = b0 * (a11 * a22 - a12 * a12) - a01 * (b1 * a22 - a12 * b2) + a02 * (b1 * a12 - a11 * b2);
            var dy = a00 * (b1 * a22 - b2 * a12) - b0 * (a01 * a22 - a12 * a02) + a02 * (a01 * b2 - b1 * a02);
            var dz = a00 * (a11 * b2 - a12 * b1) - a01 * (a01 * b2 - b1 * a02) + b0 * (a01 * a12 - a11 * a02);
            var d = new Vec3(dx / det, dy / det, dz / det);
            // Faces meeting at a hairline angle would send the corner far: the tool is left as it was.
            if (!double.IsFinite(d.X + d.Y + d.Z) || d.Length > 20 * Math.Abs(by)) return t;
            verts[v] += d;
        }
        return new Solid(verts, t.Faces, t.Tag);
    }

    static IEnumerable<int[]> Loops(Face f) => f.Holes.Prepend(f.Outer);

    /// <summary>The two faces on one plane share some area: a point inside one of them lies inside the other.</summary>
    static bool Overlap(Solid a, Face fa, Solid b, Face fb, Vec3 normal)
    {
        var (u, v) = VecMath.PlaneBasis(normal);
        return Inner(a, fa, normal).Any(p => Inside(b, fb, VecMath.Project(p, u, v), u, v))
            || Inner(b, fb, normal).Any(p => Inside(a, fa, VecMath.Project(p, u, v), u, v));
    }

    /// <summary>The tool's face lies within the host's: every corner of it in or on the host's face, and its inside too.</summary>
    static bool Within(Solid a, Face fa, Solid b, Face fb, Vec3 normal, double tol)
    {
        var (u, v) = VecMath.PlaneBasis(normal);
        var outer = fb.Outer.Select(i => VecMath.Project(b.Vertices[i], u, v)).ToList();
        bool OnOrIn(Vec2 p) => Polygon2.Contains(outer, p) || Near(outer, p, tol);
        return fa.Outer.All(i => OnOrIn(VecMath.Project(a.Vertices[i], u, v)))
            && Inner(a, fa, normal).All(p => Inside(b, fb, VecMath.Project(p, u, v), u, v));
    }

    /// <summary>Points strictly inside a face: its triangles' centroids.</summary>
    static IEnumerable<Vec3> Inner(Solid s, Face f, Vec3 normal) =>
        Triangulator.Face(s.Vertices, f.Outer, f.Holes, normal).Select(t => (s.Vertices[t.A] + s.Vertices[t.B] + s.Vertices[t.C]) * (1.0 / 3));

    static bool Inside(Solid s, Face f, Vec2 p, Vec3 u, Vec3 v)
    {
        List<Vec2> L(int[] loop) => loop.Select(i => VecMath.Project(s.Vertices[i], u, v)).ToList();
        return Polygon2.Contains(L(f.Outer), p) && !f.Holes.Any(h => Polygon2.Contains(L(h), p));
    }

    static bool Near(List<Vec2> ring, Vec2 p, double tol)
    {
        for (var i = 0; i < ring.Count; i++)
        {
            var (a, b) = (ring[i], ring[(i + 1) % ring.Count]);
            var ab = b - a;
            var len2 = ab.X * ab.X + ab.Y * ab.Y;
            var t = len2 < 1e-24 ? 0 : Math.Clamp(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2, 0, 1);
            if ((a + ab * t - p).Length <= tol) return true;
        }
        return false;
    }
}
