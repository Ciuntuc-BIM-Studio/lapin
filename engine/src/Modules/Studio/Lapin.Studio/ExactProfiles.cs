using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Studio;

/// <summary>
/// A sketch's outline as it was drawn (docs/plan-studio-freeform.md §9, F2b): its lines and arcs end to end, in the view's coordinates
/// (z = 0), rather than the polygon the chord rule makes of it. What an exact body is built from.
/// </summary>
public static class ExactProfiles
{
    const double Join = 1e-6;

    /// <summary>A drafted shape's pieces: a line, an open polyline's lines, an arc; a closed polyline or a circle a whole loop already.</summary>
    static (List<ExactSeg> Segs, bool Closed)? Pieces(Shape? s)
    {
        static Vec3 P(Vec2 p) => new(p.X, p.Y, 0);
        var (x, y) = (new Vec3(1, 0, 0), new Vec3(0, 1, 0));
        return s switch
        {
            Line l => ([new LineSeg(P(l.A), P(l.B))], false),
            Polyline { Closed: false } p => ([.. p.Pts.Zip(p.Pts.Skip(1), (a, b) => (ExactSeg)new LineSeg(P(a), P(b)))], false),
            Polyline { Closed: true } p => ([.. p.Pts.Select((a, i) => (ExactSeg)new LineSeg(P(a), P(p.Pts[(i + 1) % p.Pts.Count])))], true),
            Circle c => ([new ArcSeg(P(c.C), x, y, c.R, 0, 2 * Math.PI)], true),
            Arc a => ([new ArcSeg(P(a.C), x, y, a.R, a.Start, a.Sweep)], false),
            _ => null,
        };
    }

    /// <summary>
    /// An outline's loop of lines and arcs (its shapes chained end to end as <see cref="Sketches.Closed"/> chains them), or null when one
    /// of them is not a line, a polyline, a circle or an arc, or they do not close.
    /// </summary>
    public static List<ExactSeg>? Of(IModelView m, Sketches.Outline outline)
    {
        var pieces = new List<List<ExactSeg>>();
        foreach (var id in outline.Ids)
        {
            if (m.Get(id) is not { } e || Pieces(Drafts.ShapeOf(e)) is not { } p) return null;
            if (p.Closed) return outline.Ids.Count == 1 ? Clean(p.Segs) : null;
            pieces.Add(p.Segs);
        }
        if (pieces.Count == 0) return null;
        var loop = new List<ExactSeg>(pieces[0]);
        var used = new bool[pieces.Count];
        used[0] = true;
        for (var n = 1; n < pieces.Count; n++)
        {
            var end = loop[^1].End;
            var next = -1;
            var reverse = false;
            for (var k = 0; k < pieces.Count && next < 0; k++)
            {
                if (used[k]) continue;
                if ((pieces[k][0].Start - end).Length <= Join) next = k;
                else if ((pieces[k][^1].End - end).Length <= Join) (next, reverse) = (k, true);
            }
            if (next < 0) return null;
            used[next] = true;
            loop.AddRange(reverse ? pieces[next].AsEnumerable().Reverse().Select(s => s.Reversed()) : pieces[next]);
        }
        return (loop[^1].End - loop[0].Start).Length <= Join ? Clean(loop) : null;
    }

    static List<ExactSeg> Clean(List<ExactSeg> loop) => [.. loop.Where(s => s.Length > 1e-12)];

    /// <summary>A polygon's loop of lines (an inset outline, which has no arcs of its own).</summary>
    public static List<ExactSeg> FromPolygon(IReadOnlyList<Vec2> ring) =>
        Clean([.. ring.Select((a, i) => (ExactSeg)new LineSeg(new Vec3(a.X, a.Y, 0), new Vec3(ring[(i + 1) % ring.Count].X, ring[(i + 1) % ring.Count].Y, 0)))]);

    /// <summary>The loop as points by the chord rule (each corner once).</summary>
    public static List<Vec2> Polygon(IReadOnlyList<ExactSeg> loop) => [.. loop.SelectMany(s => s.Points().SkipLast(1)).Select(p => p.Xy)];

    /// <summary>
    /// The loop anticlockwise, starting where the ray east of its box's middle crosses it (the farthest crossing), the piece there cut:
    /// how <see cref="Solids.Matched"/> lines up the profiles of a loft, so the smooth loft keeps the look the straight one has.
    /// </summary>
    public static List<ExactSeg> Normalized(IReadOnlyList<ExactSeg> loop0)
    {
        var loop = loop0.ToList();
        if (Polygon2.SignedArea(Polygon(loop)) < 0) loop = [.. loop.AsEnumerable().Reverse().Select(s => s.Reversed())];
        var poly = Polygon(loop);
        var mid = new Vec2((poly.Min(p => p.X) + poly.Max(p => p.X)) / 2, (poly.Min(p => p.Y) + poly.Max(p => p.Y)) / 2);
        // The farthest crossing east of the middle, on the pieces themselves.
        var (best, at, frac) = (double.NegativeInfinity, -1, 0.0);
        for (var i = 0; i < loop.Count; i++)
            foreach (var (x, t) in Crossings(loop[i], mid.Y))
                if (x > mid.X && x > best) (best, at, frac) = (x, i, t);
        if (at < 0) return loop;
        var o = new List<ExactSeg>();
        var s0 = loop[at];
        if (frac > 1e-9 && frac < 1 - 1e-9)
        {
            var (a, b) = s0.Split(frac);
            o.Add(b);
            o.AddRange(loop.Skip(at + 1));
            o.AddRange(loop.Take(at));
            o.Add(a);
        }
        else
        {
            var start = frac >= 1 - 1e-9 ? (at + 1) % loop.Count : at;
            o.AddRange(loop.Skip(start));
            o.AddRange(loop.Take(start));
        }
        return o;
    }

    /// <summary>Where a piece crosses the line y = <paramref name="y"/>: x there and the fraction of the piece's length.</summary>
    static IEnumerable<(double X, double T)> Crossings(ExactSeg s, double y)
    {
        if (s is LineSeg l)
        {
            var (a, b) = (l.A, l.B);
            if (Math.Abs(b.Y - a.Y) < 1e-12 || (a.Y - y) * (b.Y - y) > 0) yield break;
            var t = (y - a.Y) / (b.Y - a.Y);
            yield return (a.X + (b.X - a.X) * t, t);
            yield break;
        }
        if (s is not ArcSeg c) yield break;
        // In the arc's own frame (its X, Y in the view's plane).
        var dy = y - c.Centre.Y;
        foreach (var sign in new[] { 1.0, -1.0 })
        {
            var h = c.Radius * c.Radius - dy * dy;
            if (h < 0) yield break;
            var x = c.Centre.X + sign * Math.Sqrt(h);
            var p = new Vec3(x, y, 0) - c.Centre;
            var ang = Math.Atan2(Vec3.Dot(p, c.Y), Vec3.Dot(p, c.X));
            var t = Fraction(c, ang);
            if (t is { } f) yield return (x, f);
        }
    }

    /// <summary>The fraction of an arc at which it passes angle <paramref name="a"/>, or null when it does not.</summary>
    static double? Fraction(ArcSeg c, double a)
    {
        var d = a - c.From;
        if (c.Sweep >= 0) { while (d < -1e-12) d += 2 * Math.PI; while (d > 2 * Math.PI + 1e-12) d -= 2 * Math.PI; }
        else { while (d > 1e-12) d -= 2 * Math.PI; while (d < -2 * Math.PI - 1e-12) d += 2 * Math.PI; }
        var t = d / c.Sweep;
        return t >= -1e-12 && t <= 1 + 1e-12 ? Math.Clamp(t, 0, 1) : null;
    }

    /// <summary>A loop in the view's coordinates placed in the world by the view's (affine) map.</summary>
    public static List<ExactSeg> Placed(IReadOnlyList<ExactSeg> loop, Func<Vec2, Vec3> world)
    {
        var o = world(new Vec2(0, 0));
        return [.. loop.Select(s => s.Moved(p => world(p.Xy), v => world(v.Xy) - o))];
    }
}
