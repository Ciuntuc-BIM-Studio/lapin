namespace Lapin.Geometry;

/// <summary>
/// A point or direction in project space: X east, Y north, Z up, in metres.
/// The whole engine works in this one space; only the viewer converts (to Y-up).
/// </summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);
    public static readonly Vec3 UnitZ = new(0, 0, 1);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator *(double s, Vec3 a) => a * s;

    public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vec3 Cross(Vec3 a, Vec3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);

    public double Length => Math.Sqrt(Dot(this, this));

    /// <summary>Unit vector, or zero for a zero-length input (callers treat that as degenerate).</summary>
    public Vec3 Normalized()
    {
        var l = Length;
        return l > 0 ? new Vec3(X / l, Y / l, Z / l) : Zero;
    }

    public static double DistanceSquared(Vec3 a, Vec3 b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    public Vec2 Xy => new(X, Y);
    public override string ToString() => FormattableString.Invariant($"({X:0.######}, {Y:0.######}, {Z:0.######})");
}

/// <summary>A plan point (X east, Y north), metres.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
    public static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;
    public double Length => Math.Sqrt(X * X + Y * Y);
    public Vec2 Normalized() { var l = Length; return l > 0 ? new Vec2(X / l, Y / l) : this; }
    /// <summary>Rotated a quarter turn counter-clockwise.</summary>
    public Vec2 Perp => new(-Y, X);
    // Explicit: the generated one would print Perp, itself a Vec2, without end.
    public override string ToString() => FormattableString.Invariant($"({X:0.######}, {Y:0.######})");
    public Vec3 At(double z) => new(X, Y, z);
}

public static class VecMath
{
    /// <summary>
    /// Newell's method: the area-weighted normal of a 3D polygon. Uses every vertex,
    /// so it stays stable on slivers at tight mitres and on shallow roof planes.
    /// Its length is the polygon area.
    /// </summary>
    public static Vec3 NewellNormal(IReadOnlyList<Vec3> pts)
    {
        // Arrays and lists as spans: the browser's AOT build interprets interface calls on a struct list.
        if (pts is Vec3[] a) return NewellNormal(new ReadOnlySpan<Vec3>(a));
        if (pts is List<Vec3> l) return NewellNormal(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(l));
        double nx = 0, ny = 0, nz = 0;
        for (int i = 0, n = pts.Count; i < n; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % n];
            nx += (p.Y - q.Y) * (p.Z + q.Z);
            ny += (p.Z - q.Z) * (p.X + q.X);
            nz += (p.X - q.X) * (p.Y + q.Y);
        }
        return new Vec3(nx / 2, ny / 2, nz / 2);
    }

    public static Vec3 NewellNormal(ReadOnlySpan<Vec3> pts)
    {
        double nx = 0, ny = 0, nz = 0;
        for (int i = 0, n = pts.Length; i < n; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % n];
            nx += (p.Y - q.Y) * (p.Z + q.Z);
            ny += (p.Z - q.Z) * (p.X + q.X);
            nz += (p.X - q.X) * (p.Y + q.Y);
        }
        return new Vec3(nx / 2, ny / 2, nz / 2);
    }

    /// <summary>
    /// An orthonormal basis (u, v) of the plane of <paramref name="n"/>, with u × v = n.
    /// Projection onto it preserves winding: CCW around n in 3D is CCW in 2D.
    /// </summary>
    public static (Vec3 U, Vec3 V) PlaneBasis(Vec3 n)
    {
        var nn = n.Normalized();
        var seed = Math.Abs(nn.X) < 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 1, 0);
        var u = Vec3.Cross(nn, seed).Normalized();
        var v = Vec3.Cross(nn, u);
        return (u, v);
    }

    public static Vec2 Project(Vec3 p, Vec3 u, Vec3 v) => new(Vec3.Dot(p, u), Vec3.Dot(p, v));
}

public static class Polygon2
{
    /// <summary>Signed area; positive = counter-clockwise.</summary>
    public static double SignedArea(IReadOnlyList<Vec2> pts)
    {
        if (pts is Vec2[] arr) return SignedArea(new ReadOnlySpan<Vec2>(arr));
        if (pts is List<Vec2> l) return SignedArea(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(l));
        double a = 0;
        for (int i = 0, n = pts.Count; i < n; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % n];
            a += p.X * q.Y - q.X * p.Y;
        }
        return a / 2;
    }

    public static double SignedArea(ReadOnlySpan<Vec2> pts)
    {
        double a = 0;
        for (int i = 0, n = pts.Length; i < n; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % n];
            a += p.X * q.Y - q.X * p.Y;
        }
        return a / 2;
    }

    public static IReadOnlyList<Vec2> EnsureCcw(IReadOnlyList<Vec2> pts) =>
        SignedArea(pts) < 0 ? pts.Reverse().ToArray() : pts;

    /// <summary>
    /// Offset every edge of a CCW polygon outward by its own distance (negative =
    /// inward) and re-join consecutive edges at their intersection — a mitred
    /// offset, as Standalone's insetPolygon does for contours. A single distance
    /// applies to all edges.
    /// </summary>
    public static Vec2[] Offset(IReadOnlyList<Vec2> ccw, IReadOnlyList<double> distances)
    {
        int n = ccw.Count;
        if (n < 3) return [.. ccw];
        double D(int i) => distances.Count == 0 ? 0 : distances[Math.Min(i, distances.Count - 1)];
        // Edge i runs ccw[i] → ccw[i+1]; outward normal of a CCW ring is its right-hand side.
        var lines = new (Vec2 P, Vec2 Dir)[n];
        for (int i = 0; i < n; i++)
        {
            var a = ccw[i]; var b = ccw[(i + 1) % n];
            var dir = (b - a).Normalized();
            var outward = new Vec2(dir.Y, -dir.X);
            lines[i] = (a + outward * D(i), dir);
        }
        var o = new Vec2[n];
        for (int i = 0; i < n; i++)
        {
            var (p1, d1) = lines[(i - 1 + n) % n];
            var (p2, d2) = lines[i];
            var den = Vec2.Cross(d1, d2);
            if (Math.Abs(den) < 1e-12) { o[i] = p2; continue; } // collinear edges: no corner to mitre
            var t = Vec2.Cross(p2 - p1, d2) / den;
            o[i] = p1 + d1 * t;
        }
        return o;
    }

    /// <summary>Convex hull, counter-clockwise (Andrew's monotone chain).</summary>
    public static Vec2[] ConvexHull(IEnumerable<Vec2> points)
    {
        // Snapped to a nanometre first: points meant to coincide (5.9 vs 5.8999999…) must sort and merge as one.
        var p = points.Select(v => new Vec2(Math.Round(v.X, 9), Math.Round(v.Y, 9))).Distinct().OrderBy(v => v.X).ThenBy(v => v.Y).ToList();
        if (p.Count < 3) return [.. p];
        var h = new List<Vec2>();
        foreach (var pass in new[] { p, Enumerable.Reverse(p).ToList() })
        {
            int start = h.Count;
            foreach (var v in pass)
            {
                while (h.Count >= start + 2 && Vec2.Cross(h[^1] - h[^2], v - h[^2]) <= 1e-12) h.RemoveAt(h.Count - 1);
                h.Add(v);
            }
            h.RemoveAt(h.Count - 1);
        }
        return [.. h];
    }

    public static double Perimeter(IReadOnlyList<Vec2> pts)
    {
        double p = 0;
        for (int i = 0; i < pts.Count; i++) p += (pts[(i + 1) % pts.Count] - pts[i]).Length;
        return p;
    }

    /// <summary>Even-odd point-in-polygon.</summary>
    public static bool Contains(IReadOnlyList<Vec2> poly, Vec2 p)
    {
        if (poly is Vec2[] arr) return Contains(new ReadOnlySpan<Vec2>(arr), p);
        if (poly is List<Vec2> l) return Contains(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(l), p);
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i];
            var b = poly[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    public static bool Contains(ReadOnlySpan<Vec2> poly, Vec2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var a = poly[i];
            var b = poly[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }
}
