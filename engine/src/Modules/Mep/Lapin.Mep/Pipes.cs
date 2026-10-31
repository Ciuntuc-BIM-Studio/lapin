using Lapin.Geometry;

namespace Lapin.Mep;

/// <summary>The bodies of runs: tubes along a centreline, its corners rounded.</summary>
public static class Pipes
{
    /// <summary>A tube from a to b: a regular polygon of <paramref name="sides"/> (by the chord rule when 0) swept along the segment; null when too short.</summary>
    public static Solid? Tube(Vec3 a, Vec3 b, double radius, int sides = 0, string? tag = null)
    {
        var d = b - a;
        if (d.Length < 1e-6 || radius < 1e-6) return null;
        if (sides <= 0) sides = Chords.Circle(radius);
        var (u, v) = VecMath.PlaneBasis(d);
        var profile = new List<Vec3>(sides);
        for (var i = 0; i < sides; i++)
        {
            var t = 2 * Math.PI * i / sides;
            profile.Add(a + u * (radius * Math.Cos(t)) + v * (radius * Math.Sin(t)));
        }
        return Prism.Extrude(profile, null, d, tag);
    }

    /// <summary>A box of a rectangular duct from a to b, <paramref name="width"/> across and <paramref name="height"/> up.</summary>
    public static Solid? Box(Vec3 a, Vec3 b, double width, double height, string? tag = null)
    {
        var d = b - a;
        if (d.Length < 1e-6) return null;
        var flat = new Vec3(d.X, d.Y, 0);
        var across = flat.Length > 1e-9 ? Vec3.Cross(Vec3.UnitZ, flat).Normalized() : new Vec3(1, 0, 0);
        var up = Vec3.Cross(d.Normalized(), across).Normalized();
        if (up.Z < 0) up = -up;
        Vec3[] profile = [a - across * (width / 2) - up * (height / 2), a + across * (width / 2) - up * (height / 2), a + across * (width / 2) + up * (height / 2), a - across * (width / 2) + up * (height / 2)];
        return Prism.Extrude(profile, null, d, tag);
    }

    /// <summary>
    /// The centreline with its corners rounded: at each corner the two legs are
    /// shortened by the tangent length and an arc of <paramref name="arcSteps"/> (by the chord rule when 0)
    /// segments joins them; a corner too tight for the radius keeps the sharp turn.
    /// Returns the legs (straight stretches) and the bends (point chains), in order.
    /// </summary>
    public static (List<(Vec3 A, Vec3 B)> Legs, List<List<Vec3>> Bends) Fillet(IReadOnlyList<Vec3> pts, double radius, int arcSteps = 0)
    {
        var legs = new List<(Vec3, Vec3)>();
        var bends = new List<List<Vec3>>();
        if (pts.Count < 2) return (legs, bends);
        var cur = pts[0];
        for (var i = 1; i < pts.Count - 1; i++)
        {
            Vec3 p = pts[i], a = cur, b = pts[i + 1];
            var d1 = (p - a).Normalized();
            var d2 = (b - p).Normalized();
            var cos = Math.Clamp(Vec3.Dot(d1, d2), -1, 1);
            var angle = Math.Acos(cos); // the turn
            if (angle < 1e-6 || radius < 1e-9) { legs.Add((cur, p)); cur = p; continue; }
            var tangent = radius * Math.Tan(angle / 2);
            var lenIn = (p - a).Length;
            var lenOut = (b - p).Length;
            if (tangent > lenIn - 1e-9 || tangent > lenOut / 2 - 1e-9 || angle > Math.PI - 1e-6) { legs.Add((cur, p)); cur = p; continue; }
            var t1 = p - d1 * tangent;
            var t2 = p + d2 * tangent;
            legs.Add((cur, t1));
            // The arc: a quadratic bezier through the corner is close to a circle for these turns.
            var arc = new List<Vec3> { t1 };
            var steps = arcSteps > 0 ? arcSteps : Chords.Count(radius, angle, 2);
            for (var k = 1; k < steps; k++)
            {
                var s = (double)k / steps;
                arc.Add(t1 * ((1 - s) * (1 - s)) + p * (2 * s * (1 - s)) + t2 * (s * s));
            }
            arc.Add(t2);
            bends.Add(arc);
            cur = t2;
        }
        legs.Add((cur, pts[^1]));
        return (legs, bends);
    }

    /// <summary>The plan polyline shifted sideways by <paramref name="offset"/> (left of the drawn direction positive), corners mitred.</summary>
    public static List<Vec2> Offset(IReadOnlyList<Vec2> pts, double offset)
    {
        if (pts.Count < 2 || Math.Abs(offset) < 1e-12) return [.. pts];
        var o = new List<Vec2>(pts.Count);
        for (var i = 0; i < pts.Count; i++)
        {
            var dIn = i > 0 ? (pts[i] - pts[i - 1]).Normalized() : (pts[i + 1] - pts[i]).Normalized();
            var dOut = i < pts.Count - 1 ? (pts[i + 1] - pts[i]).Normalized() : dIn;
            var n = (dIn + dOut).Normalized();
            if (n.Length < 1e-9) n = dIn;
            var left = new Vec2(-n.Y, n.X);
            var cos = Vec2.Dot(dIn, n);
            o.Add(pts[i] + left * (offset / Math.Max(cos, 0.2)));
        }
        return o;
    }

    /// <summary>Length of a polyline.</summary>
    /// <summary>Where the line through a along u meets the line through b along v (null when they run parallel).</summary>
    public static Vec2? Intersect(Vec2 a, Vec2 u, Vec2 b, Vec2 v)
    {
        var den = u.X * v.Y - u.Y * v.X;
        if (Math.Abs(den) < 1e-9 * Math.Max(1e-12, u.Length * v.Length)) return null;
        var t = ((b.X - a.X) * v.Y - (b.Y - a.Y) * v.X) / den;
        return a + u * t;
    }

    public static double Length(IReadOnlyList<Vec2> pts)
    {
        double l = 0;
        for (var i = 1; i < pts.Count; i++) l += (pts[i] - pts[i - 1]).Length;
        return l;
    }

    /// <summary>The point at a station along a polyline (clamped), and the leg it is on.</summary>
    public static (Vec2 At, int Leg, Vec2 Dir) AtStation(IReadOnlyList<Vec2> pts, double station)
    {
        var s = Math.Max(0, station);
        for (var i = 1; i < pts.Count; i++)
        {
            var d = pts[i] - pts[i - 1];
            var l = d.Length;
            if (s <= l || i == pts.Count - 1) { var t = l < 1e-12 ? 0 : Math.Min(1, s / l); return (pts[i - 1] + d * t, i - 1, l < 1e-12 ? new Vec2(1, 0) : d * (1 / l)); }
            s -= l;
        }
        return (pts[0], 0, new Vec2(1, 0));
    }

    /// <summary>The station of the point of a polyline nearest to <paramref name="p"/>, and how far it is.</summary>
    public static (double Station, double Distance) Nearest(IReadOnlyList<Vec2> pts, Vec2 p)
    {
        double best = double.PositiveInfinity, at = 0, acc = 0;
        for (var i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var d = pts[i] - a; var l2 = Vec2.Dot(d, d);
            var t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a, d) / l2, 0, 1);
            var dist = (p - (a + d * t)).Length;
            if (dist < best) { best = dist; at = acc + t * Math.Sqrt(l2); }
            acc += Math.Sqrt(l2);
        }
        return (at, best);
    }
}
