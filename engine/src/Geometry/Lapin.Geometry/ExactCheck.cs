namespace Lapin.Geometry;

/// <summary>
/// An exact body held against the body it stands for (the tessellation the model uses): its faces turned the way that body's faces face,
/// and refused unless it is that body — every face and edge on the body within the tolerance, every facet of the body on one of its
/// surfaces, its shell closed. So an exact surface is never written that is not the body's (a cut, a step or a fault in what made it
/// shows here, and the body is written as its facets instead).
/// </summary>
public static class ExactCheck
{
    /// <summary>A solid's triangles in a grid, for the nearest of them to a point within a radius.</summary>
    public sealed class Near
    {
        readonly List<(Vec3 A, Vec3 B, Vec3 C, Vec3 N)> _t = [];
        readonly Dictionary<(int, int, int), List<int>> _cells = [];
        readonly Vec3 _min;
        readonly double _size;

        public Near(Solid s)
        {
            foreach (var f in s.Faces)
                foreach (var (a, b, c) in Triangulator.Face(s.Vertices, f.Outer, f.Holes, f.Normal))
                    _t.Add((s.Vertices[a], s.Vertices[b], s.Vertices[c], f.Normal.Normalized()));
            if (Measure.Bounds(s) is not { } box) { _size = 1; return; }
            _min = box.Min;
            var diag = (box.Max - box.Min).Length;
            _size = Math.Max(diag / 48, 1e-3);
            for (var i = 0; i < _t.Count; i++)
            {
                var (a, b, c, _) = _t[i];
                var (lo, hi) = (Cell(new Vec3(Math.Min(a.X, Math.Min(b.X, c.X)), Math.Min(a.Y, Math.Min(b.Y, c.Y)), Math.Min(a.Z, Math.Min(b.Z, c.Z)))),
                    Cell(new Vec3(Math.Max(a.X, Math.Max(b.X, c.X)), Math.Max(a.Y, Math.Max(b.Y, c.Y)), Math.Max(a.Z, Math.Max(b.Z, c.Z)))));
                for (var x = lo.Item1; x <= hi.Item1; x++)
                    for (var y = lo.Item2; y <= hi.Item2; y++)
                        for (var z = lo.Item3; z <= hi.Item3; z++)
                        {
                            if (!_cells.TryGetValue((x, y, z), out var l)) _cells[(x, y, z)] = l = [];
                            l.Add(i);
                        }
            }
        }

        public int Triangles => _t.Count;
        public IEnumerable<Vec3> Centroids => _t.Select(t => (t.A + t.B + t.C) * (1.0 / 3));

        (int, int, int) Cell(Vec3 p) => ((int)Math.Floor((p.X - _min.X) / _size), (int)Math.Floor((p.Y - _min.Y) / _size), (int)Math.Floor((p.Z - _min.Z) / _size));

        /// <summary>The nearest triangle's distance and outward normal, if one lies within <paramref name="radius"/>.</summary>
        public (double Distance, Vec3 Normal)? Nearest(Vec3 p, double radius)
        {
            var (lo, hi) = (Cell(p - new Vec3(radius, radius, radius)), Cell(p + new Vec3(radius, radius, radius)));
            var best = double.PositiveInfinity;
            var normal = Vec3.Zero;
            for (var x = lo.Item1; x <= hi.Item1; x++)
                for (var y = lo.Item2; y <= hi.Item2; y++)
                    for (var z = lo.Item3; z <= hi.Item3; z++)
                    {
                        if (!_cells.TryGetValue((x, y, z), out var l)) continue;
                        foreach (var i in l)
                        {
                            var (a, b, c, n) = _t[i];
                            var d = (Closest(p, a, b, c) - p).Length;
                            if (d < best) (best, normal) = (d, n);
                        }
                    }
            return best <= radius ? (best, normal) : null;
        }

        /// <summary>The point of triangle abc nearest p (Ericson, Real-Time Collision Detection, §5.1.5).</summary>
        static Vec3 Closest(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
        {
            Vec3 ab = b - a, ac = c - a, ap = p - a;
            double d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return a;
            var bp = p - b;
            double d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return b;
            var vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
            var cp = p - c;
            double d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return c;
            var vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
            var va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / (d4 - d3 + d5 - d6));
            var den = 1 / (va + vb + vc);
            return a + ab * (vb * den) + ac * (vc * den);
        }
    }

    /// <summary>
    /// The exact body made to stand for <paramref name="reference"/>, within <paramref name="tolerance"/> (metres): oriented against it
    /// (<see cref="Orient"/>) and checked (<see cref="Problem"/>); null with the reason when it is not that body.
    /// </summary>
    public static (ExactBody? Body, string? Problem) Fit(ExactBody raw, Solid reference, double tolerance)
    {
        var near = new Near(reference);
        var (body, why) = Orient(raw, near, tolerance);
        if (body == null) return (null, why);
        var problem = Problem(body, near, tolerance);
        return problem == null ? (body, null) : (null, problem);
    }

    /// <summary>
    /// Each face turned as the body's own face there faces: its outer loop anticlockwise seen from outside (its holes the other way), its
    /// same sense whether the surface's normal points out of the body.
    /// </summary>
    public static (ExactBody? Body, string? Problem) Orient(ExactBody b, Near near, double tolerance)
    {
        var faces = new List<ExactFace>(b.Faces.Count);
        for (var i = 0; i < b.Faces.Count; i++)
        {
            var f = b.Faces[i];
            if (near.Nearest(f.Inside, Math.Max(4 * tolerance, 0.005)) is not { } hit) return (null, $"face {i} does not lie on the body.");
            var outward = hit.Normal;
            var same = Vec3.Dot(f.Surface.NormalAt(f.Inside), outward) > 0;
            var loops = f.Loops.Select((l, k) =>
            {
                var turn = Vec3.Dot(VecMath.NewellNormal(b.LoopPoints(l)), outward);
                return (k == 0) == (turn >= 0) ? l : l.Reversed();
            }).ToList();
            faces.Add(f with { SameSense = same, Loops = loops });
        }
        return (b with { Faces = faces }, null);
    }

    /// <summary>Why the exact body is not the body <paramref name="near"/> holds, or null when it is.</summary>
    public static string? Problem(ExactBody b, Near near, double tolerance)
    {
        // A closed shell: each edge in two loops, once each way.
        var uses = new int[b.Edges.Count, 2];
        foreach (var f in b.Faces)
            foreach (var l in f.Loops)
                foreach (var (e, fwd) in l.Edges) uses[e, fwd ? 0 : 1]++;
        for (var e = 0; e < b.Edges.Count; e++)
            if (uses[e, 0] != 1 || uses[e, 1] != 1) return $"the shell is not closed at edge {e} (used {uses[e, 0]} times forward, {uses[e, 1]} back).";
        // Every face on the body: its inside point and its edges.
        for (var i = 0; i < b.Faces.Count; i++)
        {
            var f = b.Faces[i];
            if (near.Nearest(f.Inside, tolerance) == null) return $"face {i} strays from the body.";
            foreach (var l in f.Loops)
                foreach (var p in b.LoopPoints(l))
                    if (near.Nearest(p, tolerance) == null) return $"an edge of face {i} strays from the body.";
        }
        // Every facet of the body on a surface of it (a cut or a step adds faces that are on none).
        var boxes = b.Faces.Select(f => Box(b, f, tolerance)).ToList();
        foreach (var c in near.Centroids)
        {
            var on = false;
            for (var i = 0; i < b.Faces.Count && !on; i++)
            {
                var (lo, hi) = boxes[i];
                if (c.X < lo.X || c.Y < lo.Y || c.Z < lo.Z || c.X > hi.X || c.Y > hi.Y || c.Z > hi.Z) continue;
                on = b.Faces[i].Surface.Distance(c) <= tolerance;
            }
            if (!on) return $"the body has a face near {c} that lies on none of the exact surfaces.";
        }
        return null;
    }

    /// <summary>A face's box, grown by the tolerance and a little: a B-spline face's by its control net (it lies within it), another's by its edges and inside point.</summary>
    static (Vec3 Min, Vec3 Max) Box(ExactBody b, ExactFace f, double tol)
    {
        var pts = f.Loops.SelectMany(b.LoopPoints).Append(f.Inside).ToList();
        if (f.Surface is SplineSurface s) foreach (var p in s.Surface.Points) pts.Add(p);
        var lo = new Vec3(pts.Min(p => p.X), pts.Min(p => p.Y), pts.Min(p => p.Z));
        var hi = new Vec3(pts.Max(p => p.X), pts.Max(p => p.Y), pts.Max(p => p.Z));
        var grow = tol + (hi - lo).Length * 0.02;
        var g = new Vec3(grow, grow, grow);
        return (lo - g, hi + g);
    }
}
