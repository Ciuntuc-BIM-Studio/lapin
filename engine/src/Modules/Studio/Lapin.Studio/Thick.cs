using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Studio's thick solids (docs/plan-studio-thick.md): a drafted shape given a thickness in its plane, then a height along the
/// plane's normal.
/// <list type="bullet">
/// <item><c>ring</c>: a circle, an arc or a closed outline becomes a ring (a tube): its thickness inside, outside or centred on it;</item>
/// <item><c>box</c>: a rectangle, a closed polyline or a circle becomes the walls round it, with an optional bottom and top (separate slabs);</item>
/// <item><c>wall</c>: a line, a polyline or an arc becomes a wall strip, its thickness to the left, to the right or centred, its corners mitred.</item>
/// </list>
/// Left and right are seen walking along the shape as it was drawn; an arc runs counter-clockwise, so its left is towards its centre.
/// </summary>
public static class Thick
{
    public const string Ring = "ring", Box = "box", Wall = "wall", BoxSlab = "boxSlab";
    public static readonly string[] Ops = [Ring, Box, Wall];
    public static readonly string[] Justifications = ["inside", "outside", "centre", "left", "right"];

    public static bool Is(string? op) => op is Ring or Box or Wall;

    public static string DefaultJustify(string op) => op == Wall ? "centre" : "inside";

    /// <summary>Why the shape cannot be this solid's; null when it can.</summary>
    public static string? ShapeProblem(string op, Shape? s) => (op, s) switch
    {
        (_, null) => "Pick a drafted shape.",
        (Ring, Circle or Arc or Polyline { Closed: true }) => null,
        (Ring, _) => "A ring is made from a circle, an arc or a closed outline.",
        (Box, Circle or Polyline { Closed: true }) => null,
        (Box, _) => "A box is made from a rectangle, a closed polyline or a circle.",
        (Wall, Line or Polyline or Arc or Circle) => null,
        _ => "A wall strip is made from a line, a polyline or an arc.",
    };

    /// <summary>Why the justification does not fit the solid; null when it does.</summary>
    public static string? JustifyProblem(string op, string j) => op == Wall
        ? j is "left" or "right" or "centre" ? null : "A wall strip lies left of its line, right of it or centred on it."
        : j is "inside" or "outside" or "centre" ? null : "A ring or a box lies inside its outline, outside it or centred on it.";

    /// <summary>The band in the plane: its outline and holes, the inner outline a box's bottom and top fill, the length along its middle; or why there is none.</summary>
    public sealed record Band(IReadOnlyList<Vec2> Outer, IReadOnlyList<IReadOnlyList<Vec2>> Holes, IReadOnlyList<Vec2>? Inner, double Length, string? Problem = null)
    {
        public static Band Fail(string why) => new([], [], null, 0, why);
    }

    const int CircleSides = 64;

    static List<Vec2> CirclePts(Vec2 c, double r) =>
        Enumerable.Range(0, CircleSides).Select(i => c + new Vec2(Math.Cos(2 * Math.PI * i / CircleSides), Math.Sin(2 * Math.PI * i / CircleSides)) * r).ToList();

    /// <summary>How far outward and inward of the line the band reaches (left counted as inward, for a counter-clockwise line).</summary>
    static (double Out, double In) Split(string justify, double t) => justify switch
    {
        "inside" or "left" => (0, t),
        "outside" or "right" => (t, 0),
        _ => (t / 2, t / 2),
    };

    static double Perimeter(IReadOnlyList<Vec2> pts, bool closed)
    {
        var s = 0.0;
        for (var i = 0; i + 1 < pts.Count; i++) s += (pts[i + 1] - pts[i]).Length;
        return closed && pts.Count > 2 ? s + (pts[0] - pts[^1]).Length : s;
    }

    /// <summary>Whether each side of the offset line still runs the way the line's side does (no side folded by its mitres).</summary>
    static bool Forward(IReadOnlyList<Vec2> line, IReadOnlyList<Vec2> moved, bool closed)
    {
        var n = line.Count;
        for (var i = 0; i < (closed ? n : n - 1); i++)
        {
            var j = (i + 1) % n;
            if (Vec2.Dot(line[j] - line[i], moved[j] - moved[i]) <= 1e-12) return false;
        }
        return true;
    }

    public static Band Build(Shape shape, double t, string justify)
    {
        if (!(t > 0)) return Band.Fail("The thickness must be positive.");
        switch (shape)
        {
            case Circle c:
            {
                var (o, i) = Split(justify, t);
                var (ro, ri) = (c.R + o, c.R - i);
                if (ri < 1e-6) return Band.Fail($"The thickness ({t:0.###} m) leaves no hole inside the circle (radius {c.R:0.###} m).");
                var inner = CirclePts(c.C, ri);
                return new Band(CirclePts(c.C, ro), [inner], inner, Math.PI * (ro + ri));
            }
            case Arc a:
            {
                var (o, i) = Split(justify, t);
                var (ro, ri) = (a.R + o, a.R - i);
                if (ri < 1e-6) return Band.Fail($"The thickness ({t:0.###} m) reaches past the arc's centre (radius {a.R:0.###} m).");
                var sweep = a.Sweep;
                var n = Math.Max(8, (int)Math.Ceiling(sweep / (Math.PI / 32)));
                var outer = new List<Vec2>();
                for (var k = 0; k <= n; k++) { var ang = a.Start + sweep * k / n; outer.Add(a.C + new Vec2(Math.Cos(ang), Math.Sin(ang)) * ro); }
                for (var k = n; k >= 0; k--) { var ang = a.Start + sweep * k / n; outer.Add(a.C + new Vec2(Math.Cos(ang), Math.Sin(ang)) * ri); }
                return new Band(outer, [], null, sweep * (ro + ri) / 2);
            }
            case Polyline { Closed: true } p:
            {
                var raw = Offsets.Clean(p.Pts, true);
                if (raw.Count < 3 || Math.Abs(Polygon2.SignedArea(raw)) < 1e-9) return Band.Fail("The outline has no area.");
                var ccw = Polygon2.SignedArea(raw) > 0;
                var loop = ccw ? raw : raw.AsEnumerable().Reverse().ToList();
                // Left and right as drawn: drawn clockwise, its left is outside.
                var j = !ccw && justify is "left" ? "right" : !ccw && justify is "right" ? "left" : justify;
                var (o, i) = Split(j, t);
                var outer = o == 0 ? loop : Offsets.Loop(loop, o);
                var inner = i == 0 ? loop : Offsets.Loop(loop, -i);
                if (!Forward(loop, inner, true) || Polygon2.SignedArea(inner) <= 1e-9)
                    return Band.Fail($"The thickness ({t:0.###} m) is too much for the outline: its inner side would fold over itself.");
                return new Band(outer, [inner], inner, Perimeter(Offsets.Loop(loop, (o - i) / 2), true));
            }
            case Line or Polyline:
            {
                var pts = Offsets.Clean(shape is Line l ? [l.A, l.B] : ((Polyline)shape).Pts, false);
                if (pts.Count < 2) return Band.Fail("The line has no length.");
                var (right, left) = Split(justify, t);
                var lp = left == 0 ? pts : Offsets.Open(pts, left);
                var rp = right == 0 ? pts : Offsets.Open(pts, -right);
                if (!Forward(pts, lp, false) || !Forward(pts, rp, false))
                    return Band.Fail($"A side of the line is too short for its mitred corners at {t:0.###} m thick: lengthen it or make the wall thinner.");
                return new Band(lp.Concat(rp.AsEnumerable().Reverse()).ToList(), [], null, Perimeter(Offsets.Open(pts, (left - right) / 2), false));
            }
        }
        return Band.Fail("That shape cannot be given a thickness.");
    }
}
