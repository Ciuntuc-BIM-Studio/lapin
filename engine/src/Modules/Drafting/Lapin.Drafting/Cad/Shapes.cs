using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Drafting.Cad;

/// <summary>
/// A drafted shape, in a view's coordinates (metres): a line, a polyline (a
/// rectangle is a closed one), a circle, or an arc running counter-clockwise
/// from <c>Start</c> to <c>End</c> (radians).
/// </summary>
public abstract record Shape
{
    public static Shape? From(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        static Vec2? P(JsonNode? x) => x is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } px && Json.Number(a[1]) is { } py ? new Vec2(px, py) : null;
        return (string?)o["type"] switch
        {
            "line" when P(o["a"]) is { } a && P(o["b"]) is { } b => new Line(a, b),
            "polyline" when o["pts"] is JsonArray pts && pts.Select(P).ToList() is var l && l.All(p => p != null) && l.Count >= 2
                => new Polyline(l.Select(p => p!.Value).ToList(), o["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c),
            "circle" when P(o["c"]) is { } c && Json.Number(o["r"]) is { } r && r > 0 => new Circle(c, r),
            "arc" when P(o["c"]) is { } c && Json.Number(o["r"]) is { } r && r > 0 => new Arc(c, r, Json.Number(o["start"]) ?? 0, Json.Number(o["end"]) ?? 0),
            "ellipse" when P(o["c"]) is { } c && P(o["m"]) is { } m && m.Length > 1e-12 && Json.Number(o["ratio"]) is { } q && q > 1e-9 && q <= 1 + 1e-9
                => new Ellipse(c, m, q, Json.Number(o["start"]) ?? 0, Json.Number(o["end"]) ?? 0),
            _ => null,
        };
    }

    public abstract JsonObject ToJson();
    protected static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));
}

public sealed record Line(Vec2 A, Vec2 B) : Shape
{
    public override JsonObject ToJson() => new() { ["type"] = "line", ["a"] = J(A), ["b"] = J(B) };
}

public sealed record Polyline(IReadOnlyList<Vec2> Pts, bool Closed) : Shape
{
    public override JsonObject ToJson() => new() { ["type"] = "polyline", ["pts"] = new JsonArray(Pts.Select(p => (JsonNode)J(p)).ToArray()), ["closed"] = Closed };
    public bool Equals(Polyline? o) => o != null && Closed == o.Closed && Pts.SequenceEqual(o.Pts);
    public override int GetHashCode() => HashCode.Combine(Closed, Pts.Count);
}

public sealed record Circle(Vec2 C, double R) : Shape
{
    public override JsonObject ToJson() => new() { ["type"] = "circle", ["c"] = J(C), ["r"] = R };
}

public sealed record Arc(Vec2 C, double R, double Start, double End) : Shape
{
    public override JsonObject ToJson() => new() { ["type"] = "arc", ["c"] = J(C), ["r"] = R, ["start"] = Start, ["end"] = End };
    public double Sweep => Geo.Sweep(Start, End);
    public Vec2 At(double angle) => C + new Vec2(Math.Cos(angle), Math.Sin(angle)) * R;
}

/// <summary>
/// An ellipse (audit S4): its centre, its major semi-axis <c>M</c> (a vector from the centre to the major axis's end), the ratio of its
/// minor semi-axis to it (0 &lt; ratio ≤ 1, the minor one a quarter turn anticlockwise from M), and, for an elliptical arc, the
/// parameters it runs between anticlockwise (P(t) = C + M cos t + N sin t); start = end: the whole ellipse. As DXF's ELLIPSE.
/// Laid by the one rule for curves (<see cref="Chords.EllipseArc"/>).
/// </summary>
public sealed record Ellipse(Vec2 C, Vec2 M, double Ratio, double Start, double End) : Shape
{
    public override JsonObject ToJson()
    {
        var o = new JsonObject { ["type"] = "ellipse", ["c"] = J(C), ["m"] = J(M), ["ratio"] = Math.Round(Ratio, 12) };
        if (!Whole) { o["start"] = Start; o["end"] = End; }
        return o;
    }
    public bool Whole => Math.Abs(Geo.NormAngle(End - Start)) < 1e-12;
    public Vec2 N => new Vec2(-M.Y, M.X) * Ratio;
    public double A => M.Length;
    public double B => M.Length * Ratio;
    public double Sweep => Whole ? 2 * Math.PI : Geo.Sweep(Start, End);
    public Vec2 At(double t) => C + M * Math.Cos(t) + N * Math.Sin(t);
    /// <summary>Its points by the one rule, both ends included (a whole one closed: its first point again at the end).</summary>
    public List<Vec2> Points() => Chords.EllipseArc(C, M, Ratio, Whole ? 0 : Start, Sweep, Whole ? 4 : 2);
}

/// <summary>A piece of a shape: a segment, or an arc (a whole circle when <c>Full</c>).</summary>
public abstract record Prim;
public sealed record SegP(Vec2 A, Vec2 B) : Prim;
public sealed record ArcP(Vec2 C, double R, double Start, double End, bool Full) : Prim;

/// <summary>Where a shape's key points are, for snapping.</summary>
public sealed record KeyPoints(List<Vec2> Endpoints, List<Vec2> Midpoints, List<Vec2> Centers, List<Vec2> Quadrants);

/// <summary>The geometry of drafted shapes (ported from the old Lapin's drafting: vec, curves, intersect, transform).</summary>
public static class Geo
{
    public const double Eps = 1e-9;
    const double TwoPi = 2 * Math.PI;

    public static double NormAngle(double a) { a %= TwoPi; if (a < 0) a += TwoPi; return a >= TwoPi - 1e-15 ? 0 : a; }

    /// <summary>The counter-clockwise sweep from start to end, in (0, 2π].</summary>
    public static double Sweep(double start, double end) { var s = NormAngle(end - start); return s < 1e-12 ? TwoPi : s; }

    /// <summary>Whether an angle lies on the counter-clockwise sweep from start to end (ends included).</summary>
    public static bool AngleInArc(double a, double start, double end) => NormAngle(a - start) <= NormAngle(end - start) + 1e-9 || NormAngle(a - start) >= TwoPi - 1e-9;

    public static Vec2 Rotate(Vec2 p, Vec2 c, double angle)
    {
        var (s, co) = (Math.Sin(angle), Math.Cos(angle));
        var d = p - c;
        return new Vec2(c.X + d.X * co - d.Y * s, c.Y + d.X * s + d.Y * co);
    }

    public static Vec2 Mirror(Vec2 p, Vec2 a, Vec2 b)
    {
        var d = b - a;
        var l2 = Vec2.Dot(d, d);
        if (l2 < 1e-18) return p;
        var f = a + d * (Vec2.Dot(p - a, d) / l2);
        return f * 2 - p;
    }

    public static Vec2 Norm(Vec2 v) => v.Length < 1e-15 ? new Vec2(0, 0) : v * (1 / v.Length);

    public static List<Prim> Prims(Shape s) => s switch
    {
        Line l => [new SegP(l.A, l.B)],
        Polyline p => Enumerable.Range(0, p.Closed ? p.Pts.Count : p.Pts.Count - 1).Select(i => (Prim)new SegP(p.Pts[i], p.Pts[(i + 1) % p.Pts.Count])).ToList(),
        Circle c => [new ArcP(c.C, c.R, 0, 0, true)],
        Arc a => [new ArcP(a.C, a.R, a.Start, a.End, false)],
        // An ellipse as its chords (within a millimetre): what snaps, picks and intersections meet.
        Ellipse e => e.Points() is var pts ? Enumerable.Range(0, pts.Count - 1).Select(i => (Prim)new SegP(pts[i], pts[i + 1])).ToList() : [],
        _ => [],
    };

    public static (Vec2 Min, Vec2 Max) Bounds(Shape s)
    {
        var pts = new List<Vec2>();
        switch (s)
        {
            case Line l: pts.AddRange([l.A, l.B]); break;
            case Polyline p: pts.AddRange(p.Pts); break;
            case Circle c: pts.AddRange([c.C - new Vec2(c.R, c.R), c.C + new Vec2(c.R, c.R)]); break;
            case Arc a:
                pts.AddRange([a.At(a.Start), a.At(a.End)]);
                for (var k = 0; k < 4; k++) if (AngleInArc(k * Math.PI / 2, a.Start, a.End)) pts.Add(a.At(k * Math.PI / 2));
                break;
            case Ellipse e: pts.AddRange(e.Points()); break;
        }
        return (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y)));
    }

    public static (Vec2 Point, double Dist) NearestOnSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var d = b - a;
        var t = Math.Clamp(Vec2.Dot(p - a, d) / Math.Max(Vec2.Dot(d, d), 1e-18), 0, 1);
        var q = a + d * t;
        return (q, (p - q).Length);
    }

    public static (Vec2 Point, double Dist) NearestOnPrim(Prim pr, Vec2 p)
    {
        switch (pr)
        {
            case SegP s: return NearestOnSegment(p, s.A, s.B);
            case ArcP a:
            {
                var ang = Math.Atan2(p.Y - a.C.Y, p.X - a.C.X);
                if (a.Full || AngleInArc(ang, a.Start, a.End))
                {
                    var q = a.C + new Vec2(Math.Cos(ang), Math.Sin(ang)) * a.R;
                    return (q, (p - q).Length);
                }
                var e1 = a.C + new Vec2(Math.Cos(a.Start), Math.Sin(a.Start)) * a.R;
                var e2 = a.C + new Vec2(Math.Cos(a.End), Math.Sin(a.End)) * a.R;
                return (p - e1).Length <= (p - e2).Length ? (e1, (p - e1).Length) : (e2, (p - e2).Length);
            }
        }
        return (p, double.PositiveInfinity);
    }

    public static (Vec2 Point, double Dist) Nearest(Shape s, Vec2 p) =>
        Prims(s).Select(pr => NearestOnPrim(pr, p)).MinBy(x => x.Dist);

    public static KeyPoints Keys(Shape s)
    {
        var k = new KeyPoints([], [], [], []);
        switch (s)
        {
            case Line l: k.Endpoints.AddRange([l.A, l.B]); k.Midpoints.Add((l.A + l.B) * 0.5); break;
            case Polyline p:
                k.Endpoints.AddRange(p.Pts);
                foreach (var pr in Prims(p).Cast<SegP>()) k.Midpoints.Add((pr.A + pr.B) * 0.5);
                break;
            case Circle c:
                k.Centers.Add(c.C);
                for (var i = 0; i < 4; i++) k.Quadrants.Add(c.C + new Vec2(Math.Cos(i * Math.PI / 2), Math.Sin(i * Math.PI / 2)) * c.R);
                break;
            case Arc a:
                k.Endpoints.AddRange([a.At(a.Start), a.At(a.End)]);
                k.Midpoints.Add(a.At(a.Start + a.Sweep / 2));
                k.Centers.Add(a.C);
                for (var i = 0; i < 4; i++) if (AngleInArc(i * Math.PI / 2, a.Start, a.End)) k.Quadrants.Add(a.At(i * Math.PI / 2));
                break;
            case Ellipse e:
                // Its centre and its axes' four ends (those on an elliptical arc), and an arc's ends.
                k.Centers.Add(e.C);
                for (var i = 0; i < 4; i++) if (e.Whole || AngleInArc(i * Math.PI / 2, e.Start, e.End)) k.Quadrants.Add(e.At(i * Math.PI / 2));
                if (!e.Whole) { k.Endpoints.AddRange([e.At(e.Start), e.At(e.End)]); k.Midpoints.Add(e.At(e.Start + e.Sweep / 2)); }
                break;
        }
        return k;
    }

    // ------------------------------------------------------------ intersections

    static void AddUnique(List<Vec2> list, Vec2 p) { if (!list.Any(q => (q - p).Length < 1e-9)) list.Add(p); }

    public static List<Vec2> Intersect(Prim p, Prim q)
    {
        var o = new List<Vec2>();
        switch (p, q)
        {
            case (SegP a, SegP b): SegSeg(a, b, o); break;
            case (SegP a, ArcP b): SegArc(a, b, o); break;
            case (ArcP a, SegP b): SegArc(b, a, o); break;
            case (ArcP a, ArcP b): ArcArc(a, b, o); break;
        }
        return o;
    }

    static void SegSeg(SegP s1, SegP s2, List<Vec2> o)
    {
        var (d1, d2) = (s1.B - s1.A, s2.B - s2.A);
        var den = Vec2.Cross(d1, d2);
        var scale = Math.Max(d1.Length * d2.Length, 1e-18);
        if (Math.Abs(den) / scale < 1e-12)
        {
            // Parallel: collinear overlaps give their ends.
            if (Math.Abs(Vec2.Cross(s2.A - s1.A, d1)) / Math.Max(d1.Length, 1e-18) > 1e-9) return;
            foreach (var (p, a, b) in new[] { (s1.A, s2.A, s2.B), (s1.B, s2.A, s2.B), (s2.A, s1.A, s1.B), (s2.B, s1.A, s1.B) })
                if (NearestOnSegment(p, a, b).Dist < 1e-9) AddUnique(o, p);
            return;
        }
        var t = Vec2.Cross(s2.A - s1.A, d2) / den;
        var u = Vec2.Cross(s2.A - s1.A, d1) / den;
        if (t >= -1e-9 && t <= 1 + 1e-9 && u >= -1e-9 && u <= 1 + 1e-9) AddUnique(o, s1.A + d1 * t);
    }

    static void SegArc(SegP s, ArcP a, List<Vec2> o)
    {
        var d = s.B - s.A;
        var f = s.A - a.C;
        var A = Vec2.Dot(d, d);
        if (A < 1e-18) return;
        var B = 2 * Vec2.Dot(f, d);
        var C = Vec2.Dot(f, f) - a.R * a.R;
        var disc = B * B - 4 * A * C;
        var tol = 1e-9 * Math.Max(1, B * B);
        if (disc < -tol) return;
        var ts = Math.Abs(disc) <= tol ? new[] { -B / (2 * A) } : [(-B - Math.Sqrt(disc)) / (2 * A), (-B + Math.Sqrt(disc)) / (2 * A)];
        foreach (var t in ts)
        {
            if (t < -1e-9 || t > 1 + 1e-9) continue;
            var p = s.A + d * t;
            if (a.Full || AngleInArc(Math.Atan2(p.Y - a.C.Y, p.X - a.C.X), a.Start, a.End)) AddUnique(o, p);
        }
    }

    static void ArcArc(ArcP a, ArcP b, List<Vec2> o)
    {
        var dv = b.C - a.C;
        var d = dv.Length;
        if (d < 1e-12) return; // concentric (or the same circle)
        if (d > a.R + b.R + 1e-9 || d < Math.Abs(a.R - b.R) - 1e-9) return;
        var x = (d * d + a.R * a.R - b.R * b.R) / (2 * d);
        var h2 = a.R * a.R - x * x;
        var u = dv * (1 / d);
        var mid = a.C + u * x;
        var pts = h2 <= 1e-12 ? new[] { mid } : [mid + u.Perp * Math.Sqrt(h2), mid - u.Perp * Math.Sqrt(h2)];
        foreach (var p in pts)
        {
            if (!a.Full && !AngleInArc(Math.Atan2(p.Y - a.C.Y, p.X - a.C.X), a.Start, a.End)) continue;
            if (!b.Full && !AngleInArc(Math.Atan2(p.Y - b.C.Y, p.X - b.C.X), b.Start, b.End)) continue;
            AddUnique(o, p);
        }
    }

    public static List<Vec2> Intersect(Shape a, Shape b)
    {
        var o = new List<Vec2>();
        foreach (var p in Prims(a)) foreach (var q in Prims(b)) foreach (var x in Intersect(p, q)) AddUnique(o, x);
        return o;
    }

    /// <summary>Where a ray from <paramref name="origin"/> along <paramref name="dir"/> meets a shape, ahead of it, nearest first (t in units of <paramref name="dir"/>).</summary>
    public static List<(Vec2 Point, double T)> Ray(Vec2 origin, Vec2 dir, Shape s)
    {
        var len = dir.Length;
        if (len < 1e-15) return [];
        var far = new SegP(origin, origin + dir * (1e6 / len));
        var hits = new List<(Vec2, double)>();
        foreach (var pr in Prims(s))
            foreach (var p in Intersect(far, pr))
            {
                var t = Vec2.Dot(p - origin, dir) / (len * len);
                if (t > 1e-9 && !hits.Any(h => Math.Abs(h.Item2 - t) < 1e-9)) hits.Add((p, t));
            }
        return hits.OrderBy(h => h.Item2).ToList();
    }

    // ------------------------------------------------------------ transforms

    public static Shape Map(Shape s, Func<Vec2, Vec2> f) => s switch
    {
        Line l => new Line(f(l.A), f(l.B)),
        Polyline p => new Polyline(p.Pts.Select(f).ToList(), p.Closed),
        _ => s,
    };

    public static Shape Translate(Shape s, Vec2 d) => s switch
    {
        Circle c => c with { C = c.C + d },
        Arc a => a with { C = a.C + d },
        Ellipse e => e with { C = e.C + d },
        _ => Map(s, p => p + d),
    };

    public static Shape Rotate(Shape s, Vec2 c, double angle) => s switch
    {
        Circle ci => ci with { C = Rotate(ci.C, c, angle) },
        Arc a => new Arc(Rotate(a.C, c, angle), a.R, NormAngle(a.Start + angle), NormAngle(a.End + angle)),
        // Its parameters are measured from its own major axis, which turns with it.
        Ellipse e => e with { C = Rotate(e.C, c, angle), M = Rotate(e.M, new Vec2(0, 0), angle) },
        _ => Map(s, p => Rotate(p, c, angle)),
    };

    public static Shape Scale(Shape s, Vec2 c, double k) => s switch
    {
        Circle ci => new Circle(c + (ci.C - c) * k, ci.R * Math.Abs(k)),
        Arc a => new Arc(c + (a.C - c) * k, a.R * Math.Abs(k), a.Start, a.End),
        Ellipse e => e with { C = c + (e.C - c) * k, M = e.M * k },
        _ => Map(s, p => c + (p - c) * k),
    };

    /// <summary>Mirrored about the line through a and b; an arc keeps its points (its sweep runs the other way round, so its ends swap).</summary>
    public static Shape Mirror(Shape s, Vec2 a, Vec2 b)
    {
        var th = Math.Atan2(b.Y - a.Y, b.X - a.X);
        return s switch
        {
            Circle ci => ci with { C = Mirror(ci.C, a, b) },
            Arc ar => new Arc(Mirror(ar.C, a, b), ar.R, NormAngle(2 * th - ar.End), NormAngle(2 * th - ar.Start)),
            // The major axis taken the other way round keeps the minor one a quarter turn anticlockwise: t goes to π − t, so the
            // arc runs from π − end to π − start.
            Ellipse e => new Ellipse(Mirror(e.C, a, b), (Mirror(e.C + e.M, a, b) - Mirror(e.C, a, b)) * -1, e.Ratio,
                e.Whole ? 0 : NormAngle(Math.PI - e.End), e.Whole ? 0 : NormAngle(Math.PI - e.Start)),
            _ => Map(s, p => Mirror(p, a, b)),
        };
    }

    // ------------------------------------------------------------ measures

    public static double Length(Shape s) => s switch
    {
        Line l => (l.B - l.A).Length,
        Polyline p => Prims(p).Cast<SegP>().Sum(x => (x.B - x.A).Length),
        Circle c => 2 * Math.PI * c.R,
        Arc a => a.R * a.Sweep,
        Ellipse e => EllipseLength(e),
        _ => 0,
    };

    /// <summary>An ellipse's (or elliptical arc's) length, by Gauss–Legendre on |P'(t)|.</summary>
    static double EllipseLength(Ellipse e)
    {
        var edge = new Lapin.Platform.EllipseEdge(e.C, e.M, e.Ratio, true);
        if (e.Whole) return edge.Length(e.At(0), e.At(0));
        return edge.Length(e.At(e.Start), e.At(e.End));
    }

    /// <summary>The area a closed shape encloses (null for an open one).</summary>
    public static double? Area(Shape s) => s switch
    {
        Polyline { Closed: true } p => Math.Abs(Polygon2.SignedArea(p.Pts)),
        Circle c => Math.PI * c.R * c.R,
        Ellipse { Whole: true } e => Math.PI * e.A * e.B,
        _ => null,
    };

    /// <summary>
    /// A shape as points along it, for drawing and for the solids made on it: arcs by the chord rule
    /// (<see cref="Chords"/>: no chord strays more than a millimetre), or every <paramref name="maxAngle"/> when one is given.
    /// </summary>
    public static List<List<Vec2>> Polylines(Shape s, double maxAngle = 0)
    {
        switch (s)
        {
            case Line l: return [[l.A, l.B]];
            case Polyline p: return [p.Closed ? [.. p.Pts, p.Pts[0]] : p.Pts.ToList()];
            case Circle c:
            {
                var n = maxAngle > 0 ? Math.Max(24, (int)Math.Ceiling(2 * Math.PI / maxAngle)) : Chords.Circle(c.R);
                return [Enumerable.Range(0, n + 1).Select(i => c.C + new Vec2(Math.Cos(2 * Math.PI * i / n), Math.Sin(2 * Math.PI * i / n)) * c.R).ToList()];
            }
            case Arc a:
            {
                var n = maxAngle > 0 ? Math.Max(4, (int)Math.Ceiling(a.Sweep / maxAngle)) : Chords.Count(a.R, a.Sweep, 2);
                return [Enumerable.Range(0, n + 1).Select(i => a.At(a.Start + a.Sweep * i / n)).ToList()];
            }
            case Ellipse e: return [e.Points()];
        }
        return [];
    }
}
