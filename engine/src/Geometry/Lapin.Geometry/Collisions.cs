namespace Lapin.Geometry;

/// <summary>
/// A triangle made ready for collision tests: its corners, its box and its unit normal (zero when it has no area).
/// Clash detection keeps corners relative to a pair's centre, so the arithmetic stays well conditioned far from the origin.
/// </summary>
public readonly record struct Tri(Vec3 A, Vec3 B, Vec3 C, Vec3 Min, Vec3 Max, Vec3 Normal)
{
    public static Tri Make(Vec3 a, Vec3 b, Vec3 c)
    {
        var n = Vec3.Cross(b - a, c - a);
        var l = n.Length;
        return new(a, b, c, Collisions.Min(a, Collisions.Min(b, c)), Collisions.Max(a, Collisions.Max(b, c)), l > 1e-12 ? n * (1 / l) : Vec3.Zero);
    }

    public bool IsDegenerate => Normal == Vec3.Zero;

    /// <summary>The triangle moved by <paramref name="by"/> (its corners less a centre, say).</summary>
    public Tri Shifted(Vec3 by) => new(A + by, B + by, C + by, Min + by, Max + by, Normal);
}

/// <summary>
/// Collisions between triangle meshes (docs/plan-visualisation.md, clash detection), after BimGo's ClashGeometry
/// (MIT-0), from the published methods it names: Möller's interval test for two triangles crossing ("A Fast
/// Triangle-Triangle Intersection Test", 1997), the closest point on a triangle and between two segments (Ericson,
/// "Real-Time Collision Detection", 2005, ch. 5), Möller–Trumbore rays (1997) and the even–odd rule for a point inside
/// a closed mesh. Pure functions, no allocation, safe on any thread.
/// </summary>
public static class Collisions
{
    /// <summary>Distances below this (m) count as none: on the plane, touching.</summary>
    public const double Epsilon = 1e-6;

    public static Vec3 Min(Vec3 a, Vec3 b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
    public static Vec3 Max(Vec3 a, Vec3 b) => new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));

    /// <summary>
    /// Whether two triangles meet across their planes (each reaches the other's plane, and their spans along the planes'
    /// common line overlap); triangles in one plane only touch and never count. <paramref name="p"/>–<paramref name="q"/>
    /// is the segment they share (one point when they meet in one). <paramref name="proper"/>: a real crossing, each with
    /// corners strictly on both sides of the other's plane; false when they only touch (an edge or a corner on a face).
    /// </summary>
    public static bool Crosses(in Tri t, in Tri u, out Vec3 p, out Vec3 q, out bool proper)
    {
        p = q = Vec3.Zero;
        proper = false;
        if (t.IsDegenerate || u.IsDegenerate) return false;
        double a0 = Snap(Vec3.Dot(u.Normal, t.A - u.A)), a1 = Snap(Vec3.Dot(u.Normal, t.B - u.A)), a2 = Snap(Vec3.Dot(u.Normal, t.C - u.A));
        if (SameSide(a0, a1, a2) || a0 == 0 && a1 == 0 && a2 == 0) return false;
        double b0 = Snap(Vec3.Dot(t.Normal, u.A - t.A)), b1 = Snap(Vec3.Dot(t.Normal, u.B - t.A)), b2 = Snap(Vec3.Dot(t.Normal, u.C - t.A));
        if (SameSide(b0, b1, b2) || b0 == 0 && b1 == 0 && b2 == 0) return false;
        // Where each meets the other's plane (a segment or a point), then how those overlap along the common line.
        if (!PlanePoints(t, a0, a1, a2, out var t0, out var t1) || !PlanePoints(u, b0, b1, b2, out var u0, out var u1)) return false;
        var d = Vec3.Cross(t.Normal, u.Normal);
        if (Vec3.Dot(d, d) < 1e-12) return false;
        double s0 = Vec3.Dot(d, t0), s1 = Vec3.Dot(d, t1);
        if (s1 < s0) { (s0, s1) = (s1, s0); (t0, t1) = (t1, t0); }
        double r0 = Vec3.Dot(d, u0), r1 = Vec3.Dot(d, u1);
        if (r1 < r0) { (r0, r1) = (r1, r0); (u0, u1) = (u1, u0); }
        var slack = Epsilon * Math.Max(1, d.Length);
        if (s0 > r1 + slack || r0 > s1 + slack) return false;
        p = s0 >= r0 ? t0 : u0;
        q = s1 <= r1 ? t1 : u1;
        proper = BothSides(a0, a1, a2) && BothSides(b0, b1, b2) && s1 - s0 > slack && r1 - r0 > slack && Math.Min(s1, r1) - Math.Max(s0, r0) > slack;
        return true;
    }

    public static bool Crosses(in Tri t, in Tri u, out Vec3 p, out Vec3 q) => Crosses(t, u, out p, out q, out _);

    static bool BothSides(double a, double b, double c) => Math.Max(a, Math.Max(b, c)) > 0 && Math.Min(a, Math.Min(b, c)) < 0;
    static double Snap(double d) => Math.Abs(d) < Epsilon ? 0 : d;
    static bool SameSide(double a, double b, double c) => a > 0 && b > 0 && c > 0 || a < 0 && b < 0 && c < 0;

    /// <summary>The (up to two) points where a triangle meets a plane, from its corners' signed distances to it.</summary>
    static bool PlanePoints(in Tri t, double d0, double d1, double d2, out Vec3 first, out Vec3 second)
    {
        first = second = Vec3.Zero;
        var n = 0;
        void Add(bool when, Vec3 at, ref Vec3 f, ref Vec3 s)
        {
            if (!when) return;
            if (n == 0) f = at; else if (n == 1) s = at;
            n++;
        }
        Add(d0 == 0, t.A, ref first, ref second);
        Add(d1 == 0, t.B, ref first, ref second);
        Add(d2 == 0, t.C, ref first, ref second);
        if (d0 * d1 < 0) Add(true, t.A + (t.B - t.A) * (d0 / (d0 - d1)), ref first, ref second);
        if (d1 * d2 < 0) Add(true, t.B + (t.C - t.B) * (d1 / (d1 - d2)), ref first, ref second);
        if (d2 * d0 < 0) Add(true, t.C + (t.A - t.C) * (d2 / (d2 - d0)), ref first, ref second);
        if (n == 1) second = first;
        return n > 0;
    }

    /// <summary>The shortest distance between two triangles (0 when they cross or touch), with the closest points.</summary>
    public static double Distance(in Tri t, in Tri other, out Vec3 onT, out Vec3 onU)
    {
        var u = other;
        if (Crosses(t, u, out var p, out var q))
        {
            onT = onU = (p + q) * 0.5;
            return 0;
        }
        var best = double.MaxValue;
        Vec3 bt = Vec3.Zero, bu = Vec3.Zero;
        void Corner(Vec3 corner, Vec3 closest, bool onSecond)
        {
            var d = Vec3.DistanceSquared(corner, closest);
            if (d >= best) return;
            best = d;
            if (onSecond) { bu = corner; bt = closest; } else { bt = corner; bu = closest; }
        }
        Corner(t.A, ClosestOnTriangle(t.A, u), false);
        Corner(t.B, ClosestOnTriangle(t.B, u), false);
        Corner(t.C, ClosestOnTriangle(t.C, u), false);
        Corner(u.A, ClosestOnTriangle(u.A, t), true);
        Corner(u.B, ClosestOnTriangle(u.B, t), true);
        Corner(u.C, ClosestOnTriangle(u.C, t), true);
        void Edges(Vec3 p1, Vec3 q1)
        {
            foreach (var (p2, q2) in new[] { (u.A, u.B), (u.B, u.C), (u.C, u.A) })
            {
                var d = ClosestBetweenSegments(p1, q1, p2, q2, out var c1, out var c2);
                if (d < best) { best = d; bt = c1; bu = c2; }
            }
        }
        Edges(t.A, t.B);
        Edges(t.B, t.C);
        Edges(t.C, t.A);
        onT = bt;
        onU = bu;
        return Math.Sqrt(best);
    }

    /// <summary>The point of a triangle closest to a point (by the region of the triangle it falls in).</summary>
    public static Vec3 ClosestOnTriangle(Vec3 p, in Tri t)
    {
        Vec3 a = t.A, b = t.B, c = t.C, ab = b - a, ac = c - a, ap = p - a;
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
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / (d4 - d3 + (d5 - d6)));
        var sum = va + vb + vc;
        if (Math.Abs(sum) < 1e-30) return a;
        return a + ab * (vb / sum) + ac * (vc / sum);
    }

    /// <summary>The squared distance between two segments, with their closest points (parameters held to the segments).</summary>
    public static double ClosestBetweenSegments(Vec3 p1, Vec3 q1, Vec3 p2, Vec3 q2, out Vec3 c1, out Vec3 c2)
    {
        Vec3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        double a = Vec3.Dot(d1, d1), e = Vec3.Dot(d2, d2), f = Vec3.Dot(d2, r), s, t;
        const double Tiny = 1e-20;
        if (a <= Tiny && e <= Tiny) { c1 = p1; c2 = p2; return Vec3.DistanceSquared(c1, c2); }
        if (a <= Tiny) { s = 0; t = Math.Clamp(f / e, 0, 1); }
        else
        {
            var c = Vec3.Dot(d1, r);
            if (e <= Tiny) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
            else
            {
                var b = Vec3.Dot(d1, d2);
                var denom = a * e - b * b;
                s = denom > Tiny ? Math.Clamp((b * f - c * e) / denom, 0, 1) : 0;
                t = (b * s + f) / e;
                if (t < 0) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
                else if (t > 1) { t = 1; s = Math.Clamp((b - c) / a, 0, 1); }
            }
        }
        c1 = p1 + d1 * s;
        c2 = p2 + d2 * t;
        return Vec3.DistanceSquared(c1, c2);
    }

    // Three directions a little off the axes, so a ray seldom runs along an edge or a face of square geometry.
    static readonly Vec3[] Rays = [new Vec3(0.0131, 0.0217, 1).Normalized(), new Vec3(1, 0.0173, 0.0291).Normalized(), new Vec3(0.0237, 1, 0.0119).Normalized()];

    /// <summary>The skewed directions <see cref="Inside"/> casts along (for meshes with their own ray queries).</summary>
    public static Vec3 InsideRay(int i) => Rays[Math.Clamp(i, 0, 2)];

    /// <summary>
    /// Whether a point is inside a closed mesh: the even–odd rule along three skewed rays, two of three agreeing (one ray
    /// grazing an edge does not decide). An open mesh gives no reliable answer, as with any parity test.
    /// </summary>
    public static bool Inside(Vec3 point, ReadOnlySpan<Tri> mesh)
    {
        var votes = 0;
        if ((RayCrossings(point, Rays[0], mesh) & 1) == 1) votes++;
        if ((RayCrossings(point, Rays[1], mesh) & 1) == 1) votes++;
        if (votes == 1 && (RayCrossings(point, Rays[2], mesh) & 1) == 1) votes++;
        return votes >= 2;
    }

    public static int RayCrossings(Vec3 origin, Vec3 direction, ReadOnlySpan<Tri> mesh)
    {
        var n = 0;
        foreach (ref readonly var t in mesh) if (RayHits(origin, direction, t)) n++;
        return n;
    }

    /// <summary>Whether a ray from <paramref name="origin"/> along <paramref name="direction"/> crosses a triangle in front of it (Möller–Trumbore).</summary>
    public static bool RayHits(Vec3 origin, Vec3 direction, in Tri t) => RayDistance(origin, direction, t) is not null;

    /// <summary>How far along a ray it crosses a triangle in front of its origin; null when it misses.</summary>
    public static double? RayDistance(Vec3 origin, Vec3 direction, in Tri t)
    {
        Vec3 e1 = t.B - t.A, e2 = t.C - t.A, h = Vec3.Cross(direction, e2);
        var det = Vec3.Dot(e1, h);
        if (Math.Abs(det) < 1e-20) return null;
        var inv = 1 / det;
        var s = origin - t.A;
        var uu = Vec3.Dot(s, h) * inv;
        if (uu < 0 || uu > 1) return null;
        var q = Vec3.Cross(s, e1);
        var v = Vec3.Dot(direction, q) * inv;
        if (v < 0 || uu + v > 1) return null;
        var at = Vec3.Dot(e2, q) * inv;
        return at > Epsilon ? at : null;
    }

    /// <summary>Whether two boxes meet, each grown by <paramref name="grow"/>.</summary>
    public static bool Overlap(Vec3 aMin, Vec3 aMax, Vec3 bMin, Vec3 bMax, double grow = 0) =>
        aMin.X <= bMax.X + grow && aMax.X + grow >= bMin.X && aMin.Y <= bMax.Y + grow && aMax.Y + grow >= bMin.Y && aMin.Z <= bMax.Z + grow && aMax.Z + grow >= bMin.Z;
}

/// <summary>
/// A bounding-volume tree over one mesh's triangles (BimGo's TriangleTree, MIT-0): median splits on the centres'
/// longest axis, up to <see cref="LeafSize"/> triangles a leaf. Built once, then read by any number of threads: the
/// triangles near a box (a slab under a hundred pipes), whether a point is inside, the first face a ray meets.
/// </summary>
public sealed class TriangleTree
{
    public const int LeafSize = 8;

    readonly Tri[] _tris;
    Vec3[] _min, _max;
    int[] _first, _count, _right;
    int _nodes;

    public TriangleTree(IReadOnlyList<Tri> triangles)
    {
        _tris = [.. triangles];
        var capacity = Math.Max(1, 2 * (_tris.Length / LeafSize + 1));
        _min = new Vec3[capacity]; _max = new Vec3[capacity];
        _first = new int[capacity]; _count = new int[capacity]; _right = new int[capacity];
        if (_tris.Length > 0) Build(0, _tris.Length);
    }

    public int Count => _tris.Length;
    public (Vec3 Min, Vec3 Max) Bounds => _nodes == 0 ? (Vec3.Zero, Vec3.Zero) : (_min[0], _max[0]);

    int Build(int start, int count)
    {
        var node = _nodes++;
        if (node >= _min.Length)
        {
            var size = _min.Length * 2;
            Array.Resize(ref _min, size); Array.Resize(ref _max, size); Array.Resize(ref _first, size); Array.Resize(ref _count, size); Array.Resize(ref _right, size);
        }
        Vec3 min = new(double.MaxValue, double.MaxValue, double.MaxValue), max = -min, cmin = min, cmax = max;
        for (var i = start; i < start + count; i++)
        {
            min = Collisions.Min(min, _tris[i].Min);
            max = Collisions.Max(max, _tris[i].Max);
            var c = (_tris[i].Min + _tris[i].Max) * 0.5;
            cmin = Collisions.Min(cmin, c);
            cmax = Collisions.Max(cmax, c);
        }
        _min[node] = min;
        _max[node] = max;
        var spread = cmax - cmin;
        if (count <= LeafSize || Vec3.Dot(spread, spread) < 1e-12)
        {
            _first[node] = start;
            _count[node] = count;
            return node;
        }
        var axis = spread.X >= spread.Y && spread.X >= spread.Z ? 0 : spread.Y >= spread.Z ? 1 : 2;
        static double Centre(in Tri t, int axis) => axis == 0 ? t.Min.X + t.Max.X : axis == 1 ? t.Min.Y + t.Max.Y : t.Min.Z + t.Max.Z;
        Array.Sort(_tris, start, count, Comparer<Tri>.Create((a, b) => Centre(a, axis).CompareTo(Centre(b, axis))));
        var half = count / 2;
        _count[node] = 0;
        var left = Build(start, half);
        var right = Build(start + half, count - half);
        _first[node] = left;
        _right[node] = right;
        return node;
    }

    /// <summary>Adds the triangles whose boxes meet a box to <paramref name="output"/>, moved by <paramref name="shift"/>.</summary>
    public void Query(Vec3 min, Vec3 max, Vec3 shift, List<Tri> output)
    {
        if (_nodes == 0) return;
        Span<int> stack = stackalloc int[128];
        var top = 0;
        stack[top++] = 0;
        while (top > 0)
        {
            var node = stack[--top];
            if (!Collisions.Overlap(_min[node], _max[node], min, max)) continue;
            if (_count[node] > 0)
            {
                for (var i = _first[node]; i < _first[node] + _count[node]; i++)
                    if (Collisions.Overlap(_tris[i].Min, _tris[i].Max, min, max)) output.Add(_tris[i].Shifted(shift));
                continue;
            }
            if (top + 2 > stack.Length) continue;
            stack[top++] = _first[node];
            stack[top++] = _right[node];
        }
    }

    /// <summary>Whether a point is inside the mesh (the even–odd rule along <see cref="Collisions.Inside"/>'s three rays).</summary>
    public bool Inside(Vec3 point)
    {
        var votes = 0;
        if ((Crossings(point, Collisions.InsideRay(0)) & 1) == 1) votes++;
        if ((Crossings(point, Collisions.InsideRay(1)) & 1) == 1) votes++;
        if (votes == 1 && (Crossings(point, Collisions.InsideRay(2)) & 1) == 1) votes++;
        return votes >= 2;
    }

    int Crossings(Vec3 origin, Vec3 direction)
    {
        if (_nodes == 0) return 0;
        var n = 0;
        var inverse = new Vec3(1 / direction.X, 1 / direction.Y, 1 / direction.Z);
        var stack = new Stack<int>();
        stack.Push(0);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!RayMeetsBox(origin, inverse, _min[node], _max[node])) continue;
            if (_count[node] > 0)
            {
                for (var i = _first[node]; i < _first[node] + _count[node]; i++) if (Collisions.RayHits(origin, direction, _tris[i])) n++;
                continue;
            }
            stack.Push(_first[node]);
            stack.Push(_right[node]);
        }
        return n;
    }

    /// <summary>The nearest face a ray meets in front of its origin, no further than <paramref name="far"/>; null when none.</summary>
    public double? Cast(Vec3 origin, Vec3 direction, double far = double.MaxValue, Func<int, bool>? skip = null)
    {
        if (_nodes == 0) return null;
        var inverse = new Vec3(1 / direction.X, 1 / direction.Y, 1 / direction.Z);
        double? best = null;
        var stack = new Stack<int>();
        stack.Push(0);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!RayMeetsBox(origin, inverse, _min[node], _max[node], best ?? far)) continue;
            if (_count[node] > 0)
            {
                for (var i = _first[node]; i < _first[node] + _count[node]; i++)
                    if ((skip == null || !skip(i)) && Collisions.RayDistance(origin, direction, _tris[i]) is { } d && d <= (best ?? far)) best = d;
                continue;
            }
            stack.Push(_first[node]);
            stack.Push(_right[node]);
        }
        return best;
    }

    /// <summary>The slab test: whether a ray from its origin forwards (as far as <paramref name="far"/>) meets a box.</summary>
    static bool RayMeetsBox(Vec3 o, Vec3 inv, Vec3 min, Vec3 max, double far = double.MaxValue)
    {
        double tx1 = (min.X - o.X) * inv.X, tx2 = (max.X - o.X) * inv.X, ty1 = (min.Y - o.Y) * inv.Y, ty2 = (max.Y - o.Y) * inv.Y, tz1 = (min.Z - o.Z) * inv.Z, tz2 = (max.Z - o.Z) * inv.Z;
        var enter = Math.Max(Math.Max(Math.Min(tx1, tx2), Math.Min(ty1, ty2)), Math.Min(tz1, tz2));
        var leave = Math.Min(Math.Min(Math.Max(tx1, tx2), Math.Max(ty1, ty2)), Math.Max(tz1, tz2));
        return leave >= Math.Max(enter, 0) && enter <= far;
    }
}
