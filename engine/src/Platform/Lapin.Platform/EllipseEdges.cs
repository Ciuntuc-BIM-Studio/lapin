using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// An outline's edge that runs along an ellipse (audit S4: elliptical floors, openings, ceilings and rooms): the ellipse's centre,
/// its major semi-axis (a vector), the ratio of its minor semi-axis to it (the minor one a quarter turn anticlockwise from the
/// major), and which way the edge runs round it from its start corner to its end corner. Both corners lie on the ellipse.
/// P(t) = C + M·cos t + N·sin t, N = M turned a quarter ·ratio.
/// </summary>
public sealed record EllipseEdge(Vec2 C, Vec2 M, double Ratio, bool Ccw)
{
    public Vec2 N => new Vec2(-M.Y, M.X) * Ratio;
    public double A => M.Length;
    public double B => A * Math.Abs(Ratio);

    public Vec2 At(double t) => C + M * Math.Cos(t) + N * Math.Sin(t);

    /// <summary>The parameter of a point on (or near) the ellipse.</summary>
    public double Param(Vec2 p)
    {
        var d = p - C;
        var (u, v) = (Vec2.Dot(d, M) / Vec2.Dot(M, M), Vec2.Dot(d, N) / Vec2.Dot(N, N));
        return Math.Atan2(v, u);
    }

    /// <summary>How far a point lies off the ellipse, measured in the ellipse's own scale (0 on it).</summary>
    public double Off(Vec2 p)
    {
        var d = p - C;
        var (u, v) = (Vec2.Dot(d, M) / Vec2.Dot(M, M), Vec2.Dot(d, N) / Vec2.Dot(N, N));
        return Math.Abs(Math.Sqrt(u * u + v * v) - 1) * A;
    }

    static double Norm(double t) => t - 2 * Math.PI * Math.Floor(t / (2 * Math.PI));

    /// <summary>The edge's start parameter and its signed sweep (anticlockwise positive) from <paramref name="a"/> to <paramref name="b"/>; a whole turn when they meet.</summary>
    public (double From, double Sweep) Span(Vec2 a, Vec2 b)
    {
        var (t0, t1) = (Param(a), Param(b));
        var s = Norm(Ccw ? t1 - t0 : t0 - t1);
        if (s < 1e-12) s = 2 * Math.PI;
        return (t0, Ccw ? s : -s);
    }

    /// <summary>The edge laid in chords by the one rule (<see cref="Chords.EllipseArc"/>), both ends included.</summary>
    public List<Vec2> Points(Vec2 a, Vec2 b)
    {
        var (t0, s) = Span(a, b);
        var pts = Chords.EllipseArc(C, M, Ratio, t0, s, 2);
        pts[0] = a;
        pts[^1] = b;
        return pts;
    }

    /// <summary>
    /// The edge's share of the area its outline encloses (positive anticlockwise), exactly: ½∮(x dy − y dx) along
    /// P(t) is ½[C × (P(t₁) − P(t₀)) + (M × N)(t₁ − t₀)].
    /// </summary>
    public double AreaTerm(Vec2 a, Vec2 b)
    {
        var (_, s) = Span(a, b);
        return (Vec2.Cross(C, b - a) + Vec2.Cross(M, N) * s) / 2;
    }

    /// <summary>The edge's length, by Gauss–Legendre on |P'(t)| over pieces of the sweep (to well under a micrometre).</summary>
    public double Length(Vec2 a, Vec2 b)
    {
        var (t0, s) = Span(a, b);
        var pieces = Math.Max(8, (int)Math.Ceiling(Math.Abs(s) / (Math.PI / 16)));
        double[] x = [-0.9061798459386640, -0.5384693101056831, 0, 0.5384693101056831, 0.9061798459386640];
        double[] w = [0.2369268850561891, 0.4786286704993665, 0.5688888888888889, 0.4786286704993665, 0.2369268850561891];
        var h = s / pieces;
        double total = 0;
        for (var k = 0; k < pieces; k++)
        {
            var mid = t0 + h * (k + 0.5);
            for (var i = 0; i < 5; i++)
            {
                var t = mid + h / 2 * x[i];
                total += w[i] * (M * -Math.Sin(t) + N * Math.Cos(t)).Length;
            }
        }
        return Math.Abs(total * h / 2);
    }

    /// <summary>The same edge run the other way.</summary>
    public EllipseEdge Reversed => this with { Ccw = !Ccw };

    public JsonObject ToJson() => new() { ["c"] = new JsonArray(Math.Round(C.X, 9), Math.Round(C.Y, 9)), ["m"] = new JsonArray(Math.Round(M.X, 9), Math.Round(M.Y, 9)), ["ratio"] = Math.Round(Ratio, 12), ["ccw"] = Ccw };

    public static EllipseEdge? From(JsonNode? n)
    {
        if (n is not JsonObject o || o["c"] is not JsonArray { Count: >= 2 } c || o["m"] is not JsonArray { Count: >= 2 } m) return null;
        var (cx, cy, mx, my, r) = (Json.Number(c[0]), Json.Number(c[1]), Json.Number(m[0]), Json.Number(m[1]), Json.Number(o["ratio"]));
        if (cx is null || cy is null || mx is null || my is null || r is not { } ratio || !(ratio > 1e-9) || !(Math.Sqrt(mx.Value * mx.Value + my.Value * my.Value) > 1e-9)) return null;
        return new EllipseEdge(new Vec2(cx.Value, cy.Value), new Vec2(mx.Value, my.Value), ratio, o["ccw"] is not JsonValue v || !v.TryGetValue<bool>(out var ccw) || ccw);
    }

    /// <summary>The attribute holding an outline's elliptical edges: one per corner, null for an edge that is not one.</summary>
    public const string Key = "ellipses";

    /// <summary>The elliptical edges an attribute holds, one per corner (null where not one); null when none is.</summary>
    public static EllipseEdge?[]? Read(JsonNode? n, int corners)
    {
        if (n is not JsonArray arr || corners == 0) return null;
        var o = new EllipseEdge?[corners];
        for (var i = 0; i < corners && i < arr.Count; i++) o[i] = From(arr[i]);
        return o.Any(x => x != null) ? o : null;
    }

    public static EllipseEdge?[]? Of(Entity e, string key = "contour") => Read(e.Attrs[Key], Args.StoredPoints(e.Attrs[key]).Count);

    public static void Write(JsonObject attrs, IReadOnlyList<EllipseEdge?>? edges)
    {
        if (edges == null || edges.All(x => x == null)) { attrs.Remove(Key); return; }
        attrs[Key] = new JsonArray([.. edges.Select(x => (JsonNode?)x?.ToJson())]);
    }

    /// <summary>A whole ellipse as an outline: four corners at its axes' ends, each edge a quarter of it, anticlockwise.</summary>
    public static (List<Vec2> Corners, EllipseEdge?[] Edges) Whole(Vec2 c, Vec2 major, double ratio)
    {
        if (!(major.Length > 1e-4) || !(ratio > 1e-6)) throw new CommandException("An ellipse's semi-axes are lengths above nothing.");
        var e = new EllipseEdge(c, major, ratio, true);
        var pts = Enumerable.Range(0, 4).Select(k => e.At(k * Math.PI / 2)).ToList();
        return (pts, [e, e, e, e]);
    }

    /// <summary>
    /// The elliptical edges that go with an outline as it was checked (<paramref name="corners"/>, counter-clockwise): those given
    /// for the corners as given (<paramref name="given"/> run the same way, or the other way when the check turned the outline
    /// round), each edge's ends on its ellipse. Refused when an edge's ends are not on its ellipse.
    /// </summary>
    public static EllipseEdge?[]? Fit(IReadOnlyList<Vec2> corners, IReadOnlyList<EllipseEdge?>? given)
    {
        if (given == null || given.All(x => x == null)) return null;
        var n = corners.Count;
        bool Fits(EllipseEdge?[] edges) => edges.Select((x, i) => x == null || (x.Off(corners[i]) < 1e-6 && x.Off(corners[(i + 1) % n]) < 1e-6)).All(ok => ok);
        if (given.Count == n)
        {
            var same = given.ToArray();
            if (Fits(same)) return same;
            // Turned round by the check: edge j of the new outline is edge n-2-j of the old, run back.
            var back = Enumerable.Range(0, n).Select(j => given[((n - 2 - j) % n + n) % n]?.Reversed).ToArray();
            if (Fits(back)) return back;
        }
        throw new CommandException("An elliptical edge's corners must lie on its ellipse.");
    }

    /// <summary>The elliptical edges a command gives (<c>ellipses</c>), else those the element had while their corners still lie on them.</summary>
    public static EllipseEdge?[]? Arg(JsonObject a, IReadOnlyList<Vec2> corners, Entity? was)
    {
        if (a[Key] is JsonArray arr) return Fit(corners, [.. arr.Select(From)]);
        if (was == null || Of(was) is not { } old || old.Length != corners.Count) return null;
        var n = corners.Count;
        var kept = old.Select((x, i) => x != null && x.Off(corners[i]) < 1e-6 && x.Off(corners[(i + 1) % n]) < 1e-6 ? x : null).ToArray();
        return kept.Any(x => x != null) ? kept : null;
    }
}
