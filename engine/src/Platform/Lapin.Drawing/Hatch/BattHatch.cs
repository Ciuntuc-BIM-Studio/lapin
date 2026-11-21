using Lapin.Geometry;

namespace Lapin.Drawing.Hatch;

/// <summary>
/// The frame a band is laid out in: <c>s</c> along an axis, <c>y</c> across it (to its left). A wall's own axis in a plan
/// (straight, or the arc of a curved wall), so a batt runs along the wall whatever its direction and bends with a curved one;
/// <see cref="Straight"/> otherwise.
/// </summary>
public sealed class BandAxis(Func<Vec2, (double S, double Y)> toBand, Func<double, double, Vec2> fromBand)
{
    /// <summary>Where a point lies: how far along the axis, how far to its left.</summary>
    public (double S, double Y) ToBand(Vec2 p) => toBand(p);
    /// <summary>The point <paramref name="s"/> along the axis and <paramref name="y"/> to its left.</summary>
    public Vec2 FromBand(double s, double y) => fromBand(s, y);

    /// <summary>A straight axis through <paramref name="origin"/> along <paramref name="direction"/>.</summary>
    public static BandAxis Straight(Vec2 origin, Vec2 direction)
    {
        var d = direction.Normalized();
        var n = d.Perp;
        return new BandAxis(p => (Vec2.Dot(p - origin, d), Vec2.Dot(p - origin, n)), (s, y) => origin + d * s + n * y);
    }
}

/// <summary>
/// Batt insulation (mineral wool), as draughtsmen draw it (the classic batt symbol of AutoCAD's batt routines and Revit's batt
/// insulation): a chain of symmetric teardrops, one with its round head against the one face of the layer, the next against the
/// other, each pointing across to the far face. Not a .pat family (a line family cannot draw a curve): a construction laid for
/// each band in the band's own frame, <c>s</c> along it and <c>y</c> across it (the mid-line y = 0, the faces at ±h), clipped
/// to the band's polygon (mitred ends, corners).
/// <para>
/// The construction (<see cref="Fit"/>, <see cref="Strokes"/>): heads are circles of radius R = w/2, w the loop width, each
/// tangent to its face (its centre at y = ±(h − R)). The heads on one face touch each other (centres w apart), those on the
/// other face too, offset by w/2: the heads alternate faces every w/2 along the band. w is <see cref="Head"/>·2h nominally
/// (× the material's hatch scale), adjusted so a whole number n of loops fills the band: the first head touches the band's
/// start, the last its end, L = 2R + (n − 1)·w/2 = (n + 1)·w/2, nothing partial at either end (L the band's length along its
/// mid-line, so a mitred end is met half-way and two batts meet on the mitre). Each loop is its head and the two straight
/// tangents from the head to its tip, on the loop's own axis across the band at the height of the opposite heads' centres,
/// y = ∓(h − R): exactly where the two opposite heads beside it touch. The tangents leave the head at α below its sides,
/// sin α = R / 2(h − R), and the head is drawn as its arc over its face between them (half a turn plus 2α). A loop's side and
/// its neighbour's facing side are parallel (the loops are half-turn copies of each other): they run side by side across the
/// band, a zigzag of parallel pairs, none crossing.
/// </para>
/// The first head (at the band's start) lies against the face to the left of the frame (a band seen running left to right:
/// its top face; running up the page: its left face); <c>flip</c> (a material's hatch angle of half a turn) puts it on the
/// other. The frame is the element's axis when the drawing gives one (<see cref="BandAxis"/>: a wall in a plan, so the loops
/// follow the wall at any angle and round a curved one, from the wall's start), else the band's longest edge run left to right
/// (or upwards). The faces are where the band's sides along the frame lie (the edges that run along it), so a mitred end or a
/// corner piece does not shift the line off the layer; a band with no side along the frame takes its whole extent across.
/// </summary>
public static class BattHatch
{
    public const string Name = "BATT";
    public static readonly HatchPattern Pattern = new(Name, "Batt insulation: teardrops with round heads on alternate faces", []);

    public static bool Is(HatchPattern? p) => p != null && p.Name.Equals(Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>The loop width (a head's diameter) as a fraction of the band's width, before the fit: 0.4 (R = 0.4·h).</summary>
    public const double Head = 0.4;
    /// <summary>The largest hatch scale the loops take: a head of 0.46·h, its loop's tip still well past the mid-line.</summary>
    public const double MaxScale = 1.15;

    /// <summary>
    /// The loops of a band <paramref name="length"/> long and 2·<paramref name="h"/> wide: how many, their width w (heads on one
    /// face w apart, the faces alternating every w/2) and their heads' radius w/2. <paramref name="scale"/> stretches the loops
    /// (held to 0.5–1.15, so a head stays under half of half the band and its tip beyond the mid-line); one loop alone in a band
    /// too short for two.
    /// </summary>
    public static (int Count, double Width, double Radius) Fit(double length, double h, double scale = 1)
    {
        var w = Head * 2 * h * Math.Clamp(double.IsFinite(scale) && scale > 0 ? scale : 1, 0.5, MaxScale);
        var n = Math.Max(1, (int)Math.Round(2 * length / w - 1));
        w = 2 * length / (n + 1);
        // No head larger than the largest scale gives: one loop more (the fit still whole), or a lone loop held to that size.
        var most = 2 * Head * MaxScale * h;
        if (w > most) { n = Math.Max(1, (int)Math.Ceiling(2 * length / most - 1)); w = 2 * length / (n + 1); }
        if (w > most) w = most;
        return (n, w, w / 2);
    }

    /// <summary>
    /// The batt's strokes in the band's frame (s along, y across from the mid-line), each a polyline: per loop, its head's arc (by
    /// the one rule for curves, <see cref="Chords"/>, in two halves meeting where it touches its face) and its two sides.
    /// </summary>
    public static List<List<Vec2>> Strokes(double s0, double s1, double h, double scale = 1, bool flip = false)
    {
        var o = new List<List<Vec2>>();
        if (!(h > 0) || !(s1 > s0)) return o;
        var (n, w, r) = Fit(s1 - s0, h, scale);
        var big = h - r;
        if (!(r > 0) || !(big > 0)) return o;
        var first = flip ? -1 : 1;
        // The tangents' angle below the head's sides: from the tip, 2(h − R) from the centre, to a circle of radius R.
        var alpha = Math.Asin(Math.Min(1, r / (2 * big)));
        // Loops centred along the band from its start; a lone loop at the band's middle.
        var start = n == 1 ? (s0 + s1) / 2 : s0 + r;
        for (var j = 0; j < n; j++)
        {
            var side = j % 2 == 0 ? first : -first;
            var c = new Vec2(start + j * w / 2, side * big);
            var tip = new Vec2(c.X, -side * big);
            // In the head's own sense (its face up, angle 0 along +s).
            Vec2 At(double a) => new(c.X + r * Math.Cos(a), c.Y + side * r * Math.Sin(a));
            foreach (var (a0, a1) in new[] { (-alpha, Math.PI / 2), (Math.PI / 2, Math.PI + alpha) })
                o.Add([.. Chords.Arc(new Vec2(0, 0), r, a0, a1 - a0).Select(p => new Vec2(c.X + p.X, c.Y + side * p.Y))]);
            o.Add([At(-alpha), tip]);
            o.Add([At(Math.PI + alpha), tip]);
        }
        return o;
    }

    /// <param name="loops">The region, even-odd (metres, or whatever unit the drawing is in).</param>
    /// <param name="minWidth">Bands thinner than this (same unit) get no line: too thin to read.</param>
    /// <param name="axis">The frame the band runs in (an element's axis); null: the band's longest edge.</param>
    /// <param name="scale">Stretches the loops along the band, their heads with them (the material's hatch scale; 1 as designed).</param>
    /// <param name="flip">The first head against the band's right face instead of its left.</param>
    public static List<HatchSegment> Segments(IReadOnlyList<IReadOnlyList<Vec2>> loops, double minWidth = 0, int maxSegments = 200_000, BandAxis? axis = null,
        double scale = 1, bool flip = false)
    {
        var o = new List<HatchSegment>();
        foreach (var raw in loops)
        {
            var poly = Clean(raw);
            if (poly.Count < 3 || Math.Abs(Polygon2.SignedArea(poly)) < 1e-12) continue;
            var frame = axis ?? FrameOf(poly);
            if (frame == null) continue;
            var pts = poly.Select(frame.ToBand).ToList();
            double s0 = pts.Min(p => p.S), s1 = pts.Max(p => p.S);
            var (y0, y1) = Faces(pts);
            var h = (y1 - y0) / 2;
            if (2 * h < minWidth || h <= 0 || s1 - s0 <= 0) continue;
            var mid = (y0 + y1) / 2;
            // The heads fill the band's length along its mid-line (a mitred end half-way, so two batts meet on the mitre).
            var (m0, m1) = MidSpan(pts, mid);
            if (m1 - m0 > 1e-9 * (s1 - s0)) (s0, s1) = (m0, m1);
            foreach (var stroke in Strokes(s0, s1, h, scale, flip))
            {
                if (o.Count >= maxSegments) break;
                // Straight strokes in pieces of at most half the band's width, so they bend with a curved frame and each piece
                // crosses the region's outline at most once where it is clipped.
                var band = new List<Vec2> { stroke[0] };
                for (var k = 1; k < stroke.Count; k++)
                {
                    var (a, b) = (stroke[k - 1], stroke[k]);
                    var m = Math.Max(1, (int)Math.Ceiling((b - a).Length / h));
                    for (var i = 1; i <= m; i++) band.Add(a + (b - a) * ((double)i / m));
                }
                var prev = frame.FromBand(band[0].X, mid + band[0].Y);
                var prevIn = Inside(loops, prev);
                for (var i = 1; i < band.Count && o.Count < maxSegments; i++)
                {
                    var cur = frame.FromBand(band[i].X, mid + band[i].Y);
                    var curIn = Inside(loops, cur);
                    if (prevIn && curIn) o.Add(new HatchSegment(prev, cur));
                    else if (prevIn != curIn)
                    {
                        // One end inside: the crossing found by halving, the inside part kept.
                        var (a, b) = (prev, cur);
                        for (var j = 0; j < 20; j++)
                        {
                            var m = new Vec2((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                            if (Inside(loops, m) == prevIn) a = m; else b = m;
                        }
                        o.Add(prevIn ? new HatchSegment(prev, a) : new HatchSegment(b, cur));
                    }
                    prev = cur; prevIn = curIn;
                }
            }
        }
        return o;
    }

    /// <summary>Whether a material's hatch angle (degrees) flips the batt: half a turn (any angle more than a quarter turn from 0).</summary>
    public static bool FlipsAt(double angleDeg) => double.IsFinite(angleDeg) && Math.Cos(angleDeg * Math.PI / 180) < -1e-9;

    /// <summary>
    /// A band's own frame: along its longest edge, run left to right (upwards when the edge is upright) whatever the polygon's
    /// winding, so which face the first head touches does not depend on how the band was traced. A band longer across than along
    /// is drawn across (the heads run the long way).
    /// </summary>
    static BandAxis? FrameOf(List<Vec2> poly)
    {
        Vec2 dir = default; double best = 0;
        for (var i = 0; i < poly.Count; i++) { var e = poly[(i + 1) % poly.Count] - poly[i]; if (e.Length > best) { best = e.Length; dir = e; } }
        if (best <= 0) return null;
        dir = dir.Normalized();
        var nrm = dir.Perp;
        double a0 = double.MaxValue, a1 = double.MinValue, c0 = double.MaxValue, c1 = double.MinValue;
        foreach (var p in poly) { var a = Vec2.Dot(p, dir); var c = Vec2.Dot(p, nrm); a0 = Math.Min(a0, a); a1 = Math.Max(a1, a); c0 = Math.Min(c0, c); c1 = Math.Max(c1, c); }
        if (a1 - a0 < c1 - c0) dir = nrm;
        if (dir.X < -1e-9 || (Math.Abs(dir.X) <= 1e-9 && dir.Y < 0)) dir = -dir;
        return BandAxis.Straight(new Vec2(0, 0), dir);
    }

    /// <summary>
    /// The band's two faces across its frame: the lowest and highest of its sides that run along the frame (within 1:20), so
    /// a mitre, a corner piece or a notch at an end does not move them; the whole extent across when no side runs along.
    /// </summary>
    static (double Y0, double Y1) Faces(List<(double S, double Y)> pts)
    {
        double y0 = double.MaxValue, y1 = double.MinValue;
        double along = 0;
        for (var i = 0; i < pts.Count; i++)
        {
            var (a, b) = (pts[i], pts[(i + 1) % pts.Count]);
            var ds = Math.Abs(b.S - a.S);
            if (ds <= 0 || Math.Abs(b.Y - a.Y) > ds / 20) continue;
            along = Math.Max(along, ds);
        }
        // The sides along the frame that count: at least a tenth of the longest of them (a short step in a notch does not).
        for (var i = 0; i < pts.Count && along > 0; i++)
        {
            var (a, b) = (pts[i], pts[(i + 1) % pts.Count]);
            var ds = Math.Abs(b.S - a.S);
            if (ds < along / 10 || Math.Abs(b.Y - a.Y) > ds / 20) continue;
            y0 = Math.Min(y0, Math.Min(a.Y, b.Y)); y1 = Math.Max(y1, Math.Max(a.Y, b.Y));
        }
        if (y1 - y0 > 0) return (y0, y1);
        return (pts.Min(p => p.Y), pts.Max(p => p.Y));
    }

    /// <summary>Where the band's mid-line (y = <paramref name="mid"/>) first and last crosses its outline, along the frame; (0, 0) when it does not.</summary>
    static (double S0, double S1) MidSpan(List<(double S, double Y)> pts, double mid)
    {
        double lo = double.MaxValue, hi = double.MinValue;
        for (var i = 0; i < pts.Count; i++)
        {
            var (a, b) = (pts[i], pts[(i + 1) % pts.Count]);
            if ((a.Y - mid) * (b.Y - mid) > 0 || a.Y == b.Y) continue;
            var s = a.S + (b.S - a.S) * (mid - a.Y) / (b.Y - a.Y);
            lo = Math.Min(lo, s); hi = Math.Max(hi, s);
        }
        return hi > lo ? (lo, hi) : (0, 0);
    }

    /// <summary>Even-odd over all the loops: inside an odd number of them.</summary>
    static bool Inside(IReadOnlyList<IReadOnlyList<Vec2>> loops, Vec2 p) => loops.Count(l => Polygon2.Contains(l, p)) % 2 == 1;

    static List<Vec2> Clean(IReadOnlyList<Vec2> pts)
    {
        var o = new List<Vec2>();
        foreach (var p in pts) if (o.Count == 0 || (o[^1] - p).Length > 1e-9) o.Add(p);
        if (o.Count > 1 && (o[0] - o[^1]).Length <= 1e-9) o.RemoveAt(o.Count - 1);
        for (var i = 0; i < o.Count && o.Count > 3; i++)
        {
            Vec2 a = o[(i + o.Count - 1) % o.Count], b = o[i], c = o[(i + 1) % o.Count];
            var ac = c - a; var ab = b - a;
            if (ac.Length > 1e-9 && Math.Abs(ac.X * ab.Y - ac.Y * ab.X) / ac.Length <= 1e-7) { o.RemoveAt(i); i--; }
        }
        return o;
    }
}
