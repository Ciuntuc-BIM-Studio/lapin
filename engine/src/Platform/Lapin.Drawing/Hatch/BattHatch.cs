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
/// start, the last its end, L = 2R + (n − 1)·w/2 = (n + 1)·w/2, nothing partial at either end. In a drawn band
/// (<see cref="Segments"/>) the ends are where whole loops fit: at a mitre, a corner or a cut the chain ends on a whole loop
/// on its own side (two batts meet on the mitre), never a clipped one. Each loop is its head and the two straight
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
        if (!(r > 0) || !(h - r > 0)) return o;
        // Loops centred along the band from its start; a lone loop at the band's middle.
        var start = n == 1 ? (s0 + s1) / 2 : s0 + r;
        for (var j = 0; j < n; j++) o.AddRange(Loop(start + j * w / 2, Side(j, flip ? -1 : 1), r, h));
        return o;
    }

    /// <summary>The face the j-th loop's head lies against (+1 the left, −1 the right), the first's being <paramref name="first"/>.</summary>
    static int Side(int j, int first) => j % 2 == 0 ? first : -first;

    /// <summary>One loop's four strokes: its head centred at s = <paramref name="c"/> against the face <paramref name="side"/>.</summary>
    static List<List<Vec2>> Loop(double c, int side, double r, double h)
    {
        var big = h - r;
        // The tangents' angle below the head's sides: from the tip, 2(h − R) from the centre, to a circle of radius R.
        var alpha = Math.Asin(Math.Min(1, r / (2 * big)));
        var tip = new Vec2(c, -side * big);
        // In the head's own sense (its face up, angle 0 along +s).
        Vec2 At(double a) => new(c + r * Math.Cos(a), side * big + side * r * Math.Sin(a));
        var o = new List<List<Vec2>>(4);
        foreach (var (a0, a1) in new[] { (-alpha, Math.PI / 2), (Math.PI / 2, Math.PI + alpha) })
            o.Add([.. Chords.Arc(new Vec2(0, 0), r, a0, a1 - a0).Select(p => new Vec2(c + p.X, side * big + side * p.Y))]);
        o.Add([At(-alpha), tip]);
        o.Add([At(Math.PI + alpha), tip]);
        return o;
    }

    /// <summary>
    /// A loop's outline as a closed polygon, through every point its strokes are drawn by (its head's two arcs over its face,
    /// then its tip), its sides cut in eight so a point on each stands for it when the loop is tested against the band.
    /// </summary>
    static List<Vec2> Outline(double c, int side, double r, double h)
    {
        var s = Loop(c, side, r, h);
        var o = new List<Vec2>(s[0]);
        o.AddRange(s[1].Skip(1));
        var (from, tip, to) = (o[^1], s[3][^1], o[0]);
        for (var i = 1; i < 8; i++) o.Add(from + (tip - from) * (i / 8.0));
        o.Add(tip);
        for (var i = 1; i < 8; i++) o.Add(tip + (to - tip) * (i / 8.0));
        return o;
    }

    /// <summary>
    /// Whether a whole loop fits the band: every point of its outline inside the band (in the band's frame, within
    /// <paramref name="tol"/>, so a head touching a face fits) and no corner of the band inside the loop (a notch). The band's
    /// other loops (holes, even-odd) are tested in the drawing by <paramref name="free"/>.
    /// </summary>
    static bool Fits(List<Vec2> band, double c, List<Vec2> shape, double tol, Func<Vec2, bool>? free)
    {
        var at = new Vec2(c, 0);
        foreach (var q in shape)
        {
            var p = q + at;
            if (!(Polygon2.Contains(band, p) || Polygon2.Contains(band, p, tol)) || (free != null && !free(p))) return false;
        }
        foreach (var v in band) if (Polygon2.Contains(shape, v - at) && LoopDistance(shape, v - at) > tol) return false;
        return true;
    }

    static double LoopDistance(List<Vec2> loop, Vec2 p)
    {
        var d = double.MaxValue;
        for (var i = 0; i < loop.Count; i++) d = Math.Min(d, Polyline2.SegmentDistance(p, loop[i], loop[(i + 1) % loop.Count]));
        return d;
    }

    /// <summary>
    /// The nearest position to <paramref name="from"/>, going towards <paramref name="to"/>, that <paramref name="fits"/>, the
    /// positions past it fitting too (an end's shape only gets in the way nearer the end): searched by <paramref name="step"/>,
    /// or about <paramref name="hint"/> (the answer for a loop a hair larger or smaller), then halved down to the tolerance,
    /// kept on the side that fits; null when none fits before <paramref name="to"/>.
    /// </summary>
    static double? Nearest(double from, double to, double step, double tol, Func<double, bool> fits, double? hint = null)
    {
        var dir = Math.Sign(to - from);
        double good, bad;
        if (hint is { } at && dir * (at - from) > 0 && dir * (to - at) > 0)
        {
            // Bracketed about the hint, the bracket doubled until it holds the answer.
            var d = tol * 16;
            if (fits(at))
            {
                good = at; bad = at - dir * d;
                while (dir * (bad - from) > 0 && fits(bad)) { good = bad; d *= 2; bad = good - dir * d; }
                if (dir * (bad - from) <= 0) { if (fits(from)) return from; bad = from; }
            }
            else
            {
                bad = at; good = at + dir * d;
                while (dir * (to - good) > 0 && !fits(good)) { bad = good; d *= 2; good = bad + dir * d; }
                if (dir * (to - good) <= 0) { if (!fits(to)) return null; good = to; }
            }
        }
        else
        {
            if (fits(from)) return from;
            if (dir == 0) return null;
            // Steps doubling from the first, the last not past the far end.
            bad = from; good = double.NaN;
            for (var d = step; ; d *= 2)
            {
                var c = dir * (to - (bad + dir * d)) > 0 ? bad + dir * d : to;
                if (fits(c)) { good = c; break; }
                if (c == to) break;
                bad = c;
            }
            if (double.IsNaN(good)) return null;
        }
        for (var k = 0; k < 80 && Math.Abs(good - bad) > tol; k++)
        {
            var m = (good + bad) / 2;
            if (fits(m)) good = m; else bad = m;
        }
        return good;
    }

    /// <summary>
    /// How the loops are laid in a band (its outline <paramref name="band"/> in its frame, s along and y from the mid-line, its
    /// faces at ±h): the first head's centre, how many loops, their width. The first loop's head lies against the face
    /// <paramref name="first"/> and as near the band's start as a whole loop fits; the last as near its end as a whole loop fits,
    /// on whichever face gives a chain of loops closest to their nominal width (within 15 %) reaching furthest; the loops in
    /// between evenly spread (the width stretched, never a gap). Square ends: the heads touch them, as <see cref="Fit"/> lays them.
    /// Returns the first head's centre, the count, the heads' radius and the step from one loop to the next (w/2, to a hair).
    /// </summary>
    static (double C0, int N, double R, double Step)? Lay(List<Vec2> band, double s0, double s1, double h, double scale, int first, double tol, Func<Vec2, bool>? free,
        Func<int, double, List<Vec2>> shape)
    {
        var w0 = Head * 2 * h * Math.Clamp(double.IsFinite(scale) && scale > 0 ? scale : 1, 0.5, MaxScale);
        var most = 2 * Head * MaxScale * h;
        var half = (s0 + s1) / 2;
        // How far each end's shape (a mitre, a notch) runs into the band: its corners off the faces' straight runs.
        double d0 = 0, d1 = 0;
        for (var i = 0; i < band.Count; i++)
        {
            Vec2 p = band[(i + band.Count - 1) % band.Count], v = band[i], q = band[(i + 1) % band.Count];
            bool OnFace(Vec2 x) => Math.Abs(Math.Abs(x.Y) - h) <= tol && Math.Sign(x.Y) == Math.Sign(v.Y);
            if (OnFace(v) && OnFace(p) && OnFace(q)) continue;
            if (v.X < half) d0 = Math.Max(d0, v.X - s0); else d1 = Math.Max(d1, s1 - v.X);
        }
        // The loops at an end, from the end loop inwards (w/2 apart, alternating faces) as far as the end's shape reaches,
        // never into the other end's: all whole, so none beside the end loop is cut.
        bool EndFits(double c, int side, double r, int dir)
        {
            // Past this a loop is clear of the end's shape (along the faces' straight runs).
            var clear = dir > 0 ? s0 + d0 : s1 - d1;
            // Up to where the other end's shape starts (the loops there are that end's to fit).
            var far = dir > 0 ? Math.Max(s1 - d1, half) : Math.Min(s0 + d0, half);
            for (var j = 0; ; j++)
            {
                var cj = c + dir * j * r;
                if (j > 0 && (dir * (cj - dir * r - clear) >= 0 || dir * (cj + dir * r - far) > 0)) return true;
                if (!Fits(band, cj, shape(j % 2 == 0 ? side : -side, r), tol, free)) return false;
            }
        }
        double Start(double r, double? hint = null) => Nearest(s0 + r, s1 - r, r / 4, tol, c => EndFits(c, first, r, 1), hint + r) is { } c ? c - r : double.NaN;
        double End(int side, double r, double from, double? hint = null) => Nearest(s1 - r, from + r, r / 4, tol, c => EndFits(c, side, r, -1), hint - r) is { } c ? c + r : double.NaN;
        var w = Math.Min(w0, s1 - s0);
        var a = Start(w / 2);
        if (double.IsNaN(a)) return null;
        // The count and the last loop's face: of the chains whose width is within 15 % of the nominal one, the one reaching
        // furthest, then the nearest that width (as Fit rounds); else the nearest that width.
        int n = 0; var best = (Ok: false, Reach: double.MinValue, Off: double.MaxValue);
        foreach (var side in new[] { first, -first })
        {
            var b = End(side, w / 2, a);
            if (double.IsNaN(b) || b - a <= 0) continue;
            var len = b - a;
            for (var k = 1; k <= Math.Max(1, (int)Math.Ceiling(4 * len / w0)) + 1; k++)
            {
                if (Side(k - 1, first) != side) continue;
                var wk = 2 * len / (k + 1);
                if (wk > most * (1 + 1e-9) && k > 1) continue;
                var ok = Math.Abs(wk / w0 - 1) <= 0.15;
                var off = Math.Abs(2 * len / w0 - 1 - k);
                var reach = ok ? Math.Round(b / tol) * tol : 0;
                if ((ok, reach, -off).CompareTo((best.Ok, best.Reach, -best.Off)) > 0) { best = (ok, reach, off); n = k; }
            }
        }
        if (n == 0) return null;
        var last = Side(n - 1, first);
        // The width and the ends found again for it, until they agree (the ends of a mitre depend on the heads' size); the
        // loops then laid at the size the ends were found for, from the first head to the last.
        var (ra, aa, bb) = (w / 2, a, End(last, w / 2, a));
        if (double.IsNaN(bb)) return null;
        for (var it = 0; it < 30; it++)
        {
            var nw = 2 * (bb - aa) / (n + 1);
            if (Math.Abs(nw - 2 * ra) <= tol || nw <= 0) break;
            var r = nw / 2;
            var na = Start(r, aa);
            if (double.IsNaN(na)) break;
            var nb = End(last, r, na, bb);
            if (double.IsNaN(nb) || nb <= na) break;
            (ra, aa, bb) = (r, na, nb);
        }
        // A lone loop no larger than the largest scale gives, at the middle of where it may lie.
        if (n == 1) { var lone = Math.Min(bb - aa, most) / 2; return ((aa + bb) / 2, 1, Math.Min(ra, lone), 0); }
        return (aa + ra, n, ra, (bb - aa - 2 * ra) / (n - 1));
    }

    /// <param name="loops">The region, even-odd (metres, or whatever unit the drawing is in).</param>
    /// <param name="minWidth">Bands thinner than this (same unit) get no line: too thin to read.</param>
    /// <param name="axis">The frame the band runs in (an element's axis); null: the band's longest edge.</param>
    /// <param name="scale">Stretches the loops along the band, their heads with them (the material's hatch scale; 1 as designed).</param>
    /// <param name="flip">The first head against the band's right face instead of its left.</param>
    /// <remarks>
    /// Only whole loops are drawn (as ArchiCAD's and Revit's batt insulation): at a mitre, a corner, a cut by an opening or a
    /// free end the chain ends on a whole loop, the loops spread evenly over the length they fit in (<see cref="Lay"/>), and any
    /// loop the band's outline would cut (a notch, a hole) is left out whole, never clipped to a stub; a corner may keep a little
    /// empty space. Each band's loops stay on its side of a mitre, so two batts meet on it.
    /// </remarks>
    public static List<HatchSegment> Segments(IReadOnlyList<IReadOnlyList<Vec2>> loops, double minWidth = 0, int maxSegments = 200_000, BandAxis? axis = null,
        double scale = 1, bool flip = false)
    {
        var o = new List<HatchSegment>();
        for (var li = 0; li < loops.Count; li++)
        {
            var poly = Clean(loops[li]);
            if (poly.Count < 3 || Math.Abs(Polygon2.SignedArea(poly)) < 1e-12) continue;
            var frame = axis ?? FrameOf(poly);
            if (frame == null) continue;
            var pts = poly.Select(frame.ToBand).ToList();
            double s0 = pts.Min(p => p.S), s1 = pts.Max(p => p.S);
            var (y0, y1) = Faces(pts);
            var h = (y1 - y0) / 2;
            if (2 * h < minWidth || h <= 0 || s1 - s0 <= 0) continue;
            var mid = (y0 + y1) / 2;
            // The band in its own frame, y from the mid-line: its faces straight lines at ±h even round a curved axis.
            var band = pts.Select(p => new Vec2(p.S, p.Y - mid)).ToList();
            var tol = 1e-7 * h;
            // The region's other loops (holes): a loop's point must be inside an even number of them.
            var others = Enumerable.Range(0, loops.Count).Where(k => k != li).Select(k => loops[k]).ToList();
            Func<Vec2, bool>? free = others.Count == 0 ? null : p => others.Count(l => Polygon2.Contains(l, frame.FromBand(p.X, mid + p.Y))) % 2 == 0;
            var first = flip ? -1 : 1;
            // A loop's outline at s = 0 for each face and size tried (the same at any s, moved along).
            var shapes = new Dictionary<(int, double), List<Vec2>>();
            List<Vec2> Shape(int side, double r) => shapes.TryGetValue((side, r), out var l) ? l : shapes[(side, r)] = Outline(0, side, r, h);
            if (Lay(band, s0, s1, h, scale, first, tol, free, Shape) is not var (c0, n, r, step)) continue;
            if (!(r > 0) || !(h - r > 0)) continue;
            for (var j = 0; j < n && o.Count < maxSegments; j++)
            {
                var c = c0 + j * step;
                var side = Side(j, first);
                // A loop the outline would cut (a notch, a hole; the ends are laid to fit) is left out whole. A little more
                // tolerance than the ends were found with: the loops beside an end sit a hair off where they were tried.
                if (j > 0 && j < n - 1 && !Fits(band, c, Shape(side, r), 10 * tol, free)) continue;
                foreach (var stroke in Loop(c, side, r, h)) Add(stroke);
            }

            void Add(List<Vec2> stroke)
            {
                // Straight strokes in pieces of at most half the band's width, so they bend with a curved frame. Clipped to the
                // region only where a chord of a curved face cuts a hair off a head (the loops themselves fit the band).
                var pieces = new List<Vec2> { stroke[0] };
                for (var k = 1; k < stroke.Count; k++)
                {
                    var (a, b) = (stroke[k - 1], stroke[k]);
                    var m = Math.Max(1, (int)Math.Ceiling((b - a).Length / h));
                    for (var i = 1; i <= m; i++) pieces.Add(a + (b - a) * ((double)i / m));
                }
                var prev = frame.FromBand(pieces[0].X, mid + pieces[0].Y);
                var prevIn = In(prev);
                for (var i = 1; i < pieces.Count && o.Count < maxSegments; i++)
                {
                    var cur = frame.FromBand(pieces[i].X, mid + pieces[i].Y);
                    var curIn = In(cur);
                    if (prevIn && curIn) o.Add(new HatchSegment(prev, cur));
                    else if (prevIn != curIn)
                    {
                        // One end inside: the crossing found by halving, the inside part kept.
                        var (a, b) = (prev, cur);
                        for (var j = 0; j < 20; j++)
                        {
                            var m = new Vec2((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                            if (In(m) == prevIn) a = m; else b = m;
                        }
                        o.Add(prevIn ? new HatchSegment(prev, a) : new HatchSegment(b, cur));
                    }
                    prev = cur; prevIn = curIn;
                }
            }

            // Inside the region, or on its own outline (a head touching a face, a loop touching a mitre).
            bool In(Vec2 p) => Inside(loops, p) || (Polygon2.Contains(poly, p, 1e-6 * h) && (others.Count == 0 || others.Count(l => Polygon2.Contains(l, p)) % 2 == 0));
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
