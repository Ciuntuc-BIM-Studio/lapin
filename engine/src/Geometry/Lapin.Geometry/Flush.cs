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
        // A face on one plane with a moved face it shares a corner with (an outline with a point midway along an edge
        // makes two side faces in line) moves with it, so the corner between them has one place to go.
        var plane = t.Faces.Select((f, i) => Vec3.Dot(n[i], t.Vertices[f.Outer[0]])).ToArray();
        bool OnePlane(int i, int j) => Vec3.Dot(n[i], n[j]) > 1 - 1e-9 && Math.Abs(plane[i] - plane[j]) < tol;
        for (var grew = true; grew;)
        {
            grew = false;
            for (var v = 0; v < inc.Length; v++)
                foreach (var i in inc[v])
                    if (!moved[i] && inc[v].Any(j => moved[j] && OnePlane(i, j)))
                    {
                        // A union's tool only sinks a face that lies within the host's: one beside it would stick out.
                        if (!same) return t;
                        moved[i] = grew = true;
                    }
        }
        var verts = t.Vertices.ToArray();
        for (var v = 0; v < verts.Length; v++)
        {
            if (!inc[v].Any(i => moved[i])) continue;
            // The planes meeting at the corner, each once (faces in line are one), and how far each moves.
            var planes = new List<(Vec3 N, double D)>();
            foreach (var i in inc[v])
                if (!planes.Any(p => Vec3.Dot(p.N, n[i]) > 1 - 1e-9)) planes.Add((n[i], moved[i] ? by : 0));
            var d = Corner(planes);
            // Faces meeting at a hairline angle would send the corner far: the tool is left as it was.
            if (d is not { } dv || !double.IsFinite(dv.X + dv.Y + dv.Z) || dv.Length > 20 * Math.Abs(by)) return t;
            verts[v] += dv;
        }
        return new Solid(verts, t.Faces, t.Tag);
    }

    /// <summary>
    /// The least move δ of a corner putting it on each plane moved (n·δ = D): exact for one plane, two (in their span)
    /// or three; more, the least squares. Exact solutions keep corners in line along an edge in line, so a face's
    /// outline with a point midway along an edge stays straight there.
    /// </summary>
    static Vec3? Corner(List<(Vec3 N, double D)> p)
    {
        if (p.Count == 1) return p[0].N * p[0].D;
        if (p.Count == 2)
        {
            var g = Vec3.Dot(p[0].N, p[1].N);
            var det2 = 1 - g * g;
            if (det2 < 1e-12) return null;
            return p[0].N * ((p[0].D - g * p[1].D) / det2) + p[1].N * ((p[1].D - g * p[0].D) / det2);
        }
        foreach (var w in new[] { 0.0, 1e-12 })
        {
            // Σ n nᵀ δ = Σ n D; a whisker of |δ|² only when the normals all lie in one plane (round an axis).
            double a00 = w, a01 = 0, a02 = 0, a11 = w, a12 = 0, a22 = w, b0 = 0, b1 = 0, b2 = 0;
            foreach (var (q, dd) in p)
            {
                a00 += q.X * q.X; a01 += q.X * q.Y; a02 += q.X * q.Z; a11 += q.Y * q.Y; a12 += q.Y * q.Z; a22 += q.Z * q.Z;
                b0 += q.X * dd; b1 += q.Y * dd; b2 += q.Z * dd;
            }
            var det = a00 * (a11 * a22 - a12 * a12) - a01 * (a01 * a22 - a12 * a02) + a02 * (a01 * a12 - a11 * a02);
            if (w == 0 && Math.Abs(det) < 1e-9) continue;
            var dx = b0 * (a11 * a22 - a12 * a12) - a01 * (b1 * a22 - a12 * b2) + a02 * (b1 * a12 - a11 * b2);
            var dy = a00 * (b1 * a22 - b2 * a12) - b0 * (a01 * a22 - a12 * a02) + a02 * (a01 * b2 - b1 * a02);
            var dz = a00 * (a11 * b2 - a12 * b1) - a01 * (a01 * b2 - b1 * a02) + b0 * (a01 * a12 - a11 * a02);
            return new Vec3(dx / det, dy / det, dz / det);
        }
        return null;
    }

    static IEnumerable<int[]> Loops(Face f) => f.Holes.Prepend(f.Outer);

    /// <summary>How deep two faces on one plane must overlap, across every edge of a pair of their triangles, to share area (1 µm).</summary>
    const double Share = 1e-6;

    /// <summary>
    /// The two faces on one plane share some area: a triangle of one and a triangle of the other overlap by more than
    /// <see cref="Share"/> across each of the six edges (a separating-axis test, exact for triangles), so faces that only
    /// touch along an edge or at a corner do not count, and two crossing faces do, though neither holds a point of the other's middle.
    /// </summary>
    static bool Overlap(Solid a, Face fa, Solid b, Face fb, Vec3 normal)
    {
        var (u, v) = VecMath.PlaneBasis(normal);
        var ta = Tris(a, fa, normal, u, v);
        var tb = Tris(b, fb, normal, u, v);
        if (ta.Count == 0 || tb.Count == 0 || !Boxes(Box(ta), Box(tb))) return false;
        foreach (var x in ta)
        {
            var bx = Box([x]);
            foreach (var y in tb)
                if (Boxes(bx, Box([y])) && Overlap(x, y)) return true;
        }
        return false;
    }

    /// <summary>A face's triangles in the plane's (u, v).</summary>
    static List<Vec2[]> Tris(Solid s, Face f, Vec3 normal, Vec3 u, Vec3 v) =>
        Triangulator.Face(s.Vertices, f.Outer, f.Holes, normal)
            .Select(t => new[] { VecMath.Project(s.Vertices[t.A], u, v), VecMath.Project(s.Vertices[t.B], u, v), VecMath.Project(s.Vertices[t.C], u, v) })
            .Where(t => Math.Abs(Vec2.Cross(t[1] - t[0], t[2] - t[0])) > 1e-18).ToList();

    static (Vec2 Min, Vec2 Max) Box(IEnumerable<Vec2[]> tris)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var t in tris)
            foreach (var p in t) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
        return (new Vec2(x0, y0), new Vec2(x1, y1));
    }

    static bool Boxes((Vec2 Min, Vec2 Max) a, (Vec2 Min, Vec2 Max) b) =>
        a.Min.X < b.Max.X - Share && b.Min.X < a.Max.X - Share && a.Min.Y < b.Max.Y - Share && b.Min.Y < a.Max.Y - Share;

    /// <summary>Two triangles overlap by more than <see cref="Share"/> across each of their edges: their insides meet.</summary>
    static bool Overlap(Vec2[] a, Vec2[] b)
    {
        static bool Apart(Vec2[] s, Vec2[] t)
        {
            for (var i = 0; i < 3; i++)
            {
                var e = s[(i + 1) % 3] - s[i];
                var len = e.Length;
                if (len < 1e-15) continue;
                var axis = new Vec2(-e.Y / len, e.X / len);
                double s0 = double.MaxValue, s1 = double.MinValue, t0 = double.MaxValue, t1 = double.MinValue;
                foreach (var p in s) { var d = Vec2.Dot(p, axis); s0 = Math.Min(s0, d); s1 = Math.Max(s1, d); }
                foreach (var p in t) { var d = Vec2.Dot(p, axis); t0 = Math.Min(t0, d); t1 = Math.Max(t1, d); }
                if (Math.Min(s1, t1) - Math.Max(s0, t0) <= Share) return true;
            }
            return false;
        }
        return !Apart(a, b) && !Apart(b, a);
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
