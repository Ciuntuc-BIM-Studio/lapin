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
/// on its own side, never a clipped one; where two bands of one material drawn together meet at a corner, the chain goes
/// round it instead (the loops turning about the inner corner, decision 754). Each loop is its head and the two straight
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
    /// Where a chain may start and end in a band (its outline <paramref name="band"/> in its frame): the nearest position to
    /// each end where the end loop and the loops beside it, as far as the end's shape (a mitre, a notch) runs into the band,
    /// are all whole.
    /// </summary>
    sealed class Ends
    {
        readonly List<Vec2> _band;
        readonly double _s0, _s1, _tol, _half, _d0, _d1;
        readonly Func<Vec2, bool>? _free;
        readonly Func<int, double, List<Vec2>> _shape;

        public Ends(List<Vec2> band, double s0, double s1, double h, double tol, Func<Vec2, bool>? free, Func<int, double, List<Vec2>> shape)
        {
            (_band, _s0, _s1, _tol, _free, _shape) = (band, s0, s1, tol, free, shape);
            _half = (s0 + s1) / 2;
            // How far each end's shape (a mitre, a notch) runs into the band: its corners off the faces' straight runs.
            for (var i = 0; i < band.Count; i++)
            {
                Vec2 p = band[(i + band.Count - 1) % band.Count], v = band[i], q = band[(i + 1) % band.Count];
                bool OnFace(Vec2 x) => Math.Abs(Math.Abs(x.Y) - h) <= tol && Math.Sign(x.Y) == Math.Sign(v.Y);
                if (OnFace(v) && OnFace(p) && OnFace(q)) continue;
                if (v.X < _half) _d0 = Math.Max(_d0, v.X - s0); else _d1 = Math.Max(_d1, s1 - v.X);
            }
        }

        /// <summary>
        /// The loops at an end, from the end loop inwards (w/2 apart, alternating faces) as far as the end's shape reaches,
        /// never into the other end's: all whole, so none beside the end loop is cut.
        /// </summary>
        bool EndFits(double c, int side, double r, int dir)
        {
            // Past this a loop is clear of the end's shape (along the faces' straight runs).
            var clear = dir > 0 ? _s0 + _d0 : _s1 - _d1;
            // Up to where the other end's shape starts (the loops there are that end's to fit).
            var far = dir > 0 ? Math.Max(_s1 - _d1, _half) : Math.Min(_s0 + _d0, _half);
            for (var j = 0; ; j++)
            {
                var cj = c + dir * j * r;
                if (j > 0 && (dir * (cj - dir * r - clear) >= 0 || dir * (cj + dir * r - far) > 0)) return true;
                if (!Fits(_band, cj, _shape(j % 2 == 0 ? side : -side, r), _tol, _free)) return false;
            }
        }

        /// <summary>Where the first loop's head (on the face <paramref name="side"/>) begins, as near the start as it fits; NaN when none does.</summary>
        public double Start(int side, double r, double? hint = null) =>
            Nearest(_s0 + r, _s1 - r, r / 4, _tol, c => EndFits(c, side, r, 1), hint + r) is { } c ? c - r : double.NaN;

        /// <summary>Where the last loop's head (on the face <paramref name="side"/>) ends, as near the end as it fits, not before <paramref name="from"/>; NaN when none does.</summary>
        public double End(int side, double r, double from, double? hint = null) =>
            Nearest(_s1 - r, from + r, r / 4, _tol, c => EndFits(c, side, r, -1), hint - r) is { } c ? c + r : double.NaN;
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
        var ends = new Ends(band, s0, s1, h, tol, free, shape);
        double Start(double r, double? hint = null) => ends.Start(first, r, hint);
        double End(int side, double r, double from, double? hint = null) => ends.End(side, r, from, hint);
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
    /// loop the band's outline would cut (a notch, a hole) is left out whole, never clipped to a stub. A band alone keeps its
    /// loops on its side of a mitre; bands drawn together go round the corners they share (the overload taking bands).
    /// </remarks>
    public static List<HatchSegment> Segments(IReadOnlyList<IReadOnlyList<Vec2>> loops, double minWidth = 0, int maxSegments = 200_000, BandAxis? axis = null,
        double scale = 1, bool flip = false) => Segments([new BattBand(loops, axis, scale, flip)], minWidth, maxSegments)[0];

    /// <summary>
    /// The batts of several bands drawn together (the cut bands of one material in a drawing), one list of segments per band.
    /// Where two of them meet at a corner (an L, mitred or butted, turning 4°–135°: their faces meet in an inner and an outer
    /// corner, neither runs on past the corner, the two of one thickness) the chain goes round it as one batt
    /// (<see cref="Corner"/>): each band's chain runs to the inner corner and ends there on a loop whose head lies against the
    /// outer face, and between the two the loops turn about the inner corner, their tips round it, their heads against the
    /// outer faces, each loop's sides parallel to its neighbours' (<see cref="Corner.Lay"/>): at a right angle one loop on the
    /// diagonal, its head in the outer corner. A corner whose chains or turning loops do not fit whole is left as two ends
    /// (each band ends on whole loops, as alone); so is a T, a join of two thicknesses, a sharper corner, and a band with
    /// holes. A material's flip still picks the face of a free end's first head; at a corner the turn decides. Elsewhere each
    /// band is laid as alone, to the same strokes.
    /// </summary>
    /// <param name="joins">Whether two bands (by index) may go on into one another (one material, joined in the drawing); null: any.</param>
    public static List<List<HatchSegment>> Segments(IReadOnlyList<BattBand> bands, double minWidth = 0, int maxSegments = 200_000, Func<int, int, bool>? joins = null)
    {
        var o = bands.Select(_ => new List<HatchSegment>()).ToList();
        var strips = new List<Strip>();
        for (var b = 0; b < bands.Count; b++)
            for (var li = 0; li < bands[b].Loops.Count; li++)
                if (Strip.Of(bands[b], b, li, minWidth) is { } st) strips.Add(st);
        var corners = bands.Count > 1 ? Corners(strips, joins) : [];
        // Laid with the corners' pins; a corner whose chains or fan do not fit whole is given up, and the bands laid again without it.
        while (true)
        {
            foreach (var s in strips) (s.Pin0, s.Pin1) = (null, null);
            foreach (var c in corners) c.Pin();
            var failed = new HashSet<Corner>();
            foreach (var s in strips)
            {
                s.Laid = s.Pin0 == null && s.Pin1 == null
                    ? Lay(s.Local, s.S0, s.S1, s.H, s.Scale, s.First, s.Tol, s.Free, s.Shape) is var (c0, n, r, step) ? (c0, n, r, step, s.First) : null
                    : LayPinned(s);
                if (s.Laid == null) { if (s.Pin0 is { } p0) failed.Add(p0.Corner); if (s.Pin1 is { } p1) failed.Add(p1.Corner); }
            }
            foreach (var c in corners) if (!failed.Contains(c) && !c.Lay()) failed.Add(c);
            if (failed.Count == 0) break;
            corners = [.. corners.Where(c => !failed.Contains(c))];
        }
        var count = 0;
        foreach (var s in strips)
        {
            if (s.Laid is not var (c0, n, r, step, first)) continue;
            if (!(r > 0) || !(s.H - r > 0)) continue;
            var seg = o[s.Band];
            for (var j = 0; j < n && count < maxSegments; j++)
            {
                // A pinned end's loop is its corner's (the fan's first or last).
                if ((j == 0 && s.Pin0 != null) || (j == n - 1 && s.Pin1 != null)) continue;
                var c = c0 + j * step;
                var side = Side(j, first);
                // A loop the outline would cut (a notch, a hole; the ends are laid to fit) is left out whole.
                if (j > 0 && j < n - 1 && !s.Fits(c, side, r)) continue;
                foreach (var stroke in Loop(c, side, r, s.H)) count += s.Add(stroke, seg, maxSegments - count);
            }
        }
        // The fans, with the first band of each corner.
        foreach (var c in corners)
            foreach (var stroke in c.Strokes!)
                for (var i = 1; i < stroke.Count && count < maxSegments; i++, count++) o[c.A.Band].Add(new HatchSegment(stroke[i - 1], stroke[i]));
        return o;
    }

    /// <summary>A band's region (even-odd), its frame (null: its own) and its material's hatch scale and flip.</summary>
    public sealed record BattBand(IReadOnlyList<IReadOnlyList<Vec2>> Loops, BandAxis? Axis = null, double Scale = 1, bool Flip = false);

    /// <summary>One loop of a band's region, in its own frame, and how its loops are laid.</summary>
    sealed class Strip
    {
        public int Band;
        public IReadOnlyList<IReadOnlyList<Vec2>> Loops = [];
        public List<Vec2> Poly = [];
        public BandAxis Frame = null!;
        public double S0, S1, H, Mid, Tol, Scale;
        public int First;
        /// <summary>The outline in the frame, y from the mid-line.</summary>
        public List<Vec2> Local = [];
        public List<IReadOnlyList<Vec2>> Others = [];
        public Func<Vec2, bool>? Free;
        readonly Dictionary<(int, double), List<Vec2>> _shapes = [];
        /// <summary>The corner its start (its end) is pinned to: the last loop there at <c>S</c>, its head on the face <c>Side</c>.</summary>
        public (double S, int Side, Corner Corner)? Pin0, Pin1;
        public (double C0, int N, double R, double Step, int First)? Laid;

        /// <summary>A loop's outline at s = 0 for each face and size tried (the same at any s, moved along).</summary>
        public List<Vec2> Shape(int side, double r) => _shapes.TryGetValue((side, r), out var l) ? l : _shapes[(side, r)] = Outline(0, side, r, H);

        public static Strip? Of(BattBand band, int b, int li, double minWidth)
        {
            var poly = Clean(band.Loops[li]);
            if (poly.Count < 3 || Math.Abs(Polygon2.SignedArea(poly)) < 1e-12) return null;
            var frame = band.Axis ?? FrameOf(poly);
            if (frame == null) return null;
            var pts = poly.Select(frame.ToBand).ToList();
            double s0 = pts.Min(p => p.S), s1 = pts.Max(p => p.S);
            var (y0, y1) = Faces(pts);
            var h = (y1 - y0) / 2;
            if (2 * h < minWidth || h <= 0 || s1 - s0 <= 0) return null;
            var mid = (y0 + y1) / 2;
            var s = new Strip
            {
                Band = b, Loops = band.Loops, Poly = poly, Frame = frame, S0 = s0, S1 = s1, H = h, Mid = mid, Tol = 1e-7 * h,
                Scale = band.Scale, First = band.Flip ? -1 : 1,
                // The band in its own frame, y from the mid-line: its faces straight lines at ±h even round a curved axis.
                Local = [.. pts.Select(p => new Vec2(p.S, p.Y - mid))],
                // The region's other loops (holes): a loop's point must be inside an even number of them.
                Others = [.. Enumerable.Range(0, band.Loops.Count).Where(k => k != li).Select(k => band.Loops[k])],
            };
            if (s.Others.Count > 0) s.Free = p => s.Others.Count(l => Polygon2.Contains(l, frame.FromBand(p.X, mid + p.Y))) % 2 == 0;
            return s;
        }

        /// <summary>
        /// Whether a whole loop fits where the chain puts it (<see cref="BattHatch.Fits"/>, a little more tolerance than the
        /// ends were found with: the loops beside an end sit a hair off where they were tried). Beside a corner the other band
        /// counts too: a loop may cross where the two meet (a butt join's line, slanted at an obtuse corner), and the corners
        /// of either band on that line are no notch.
        /// </summary>
        public bool Fits(double c, int side, double r)
        {
            var shape = Shape(side, r);
            var tol = 10 * Tol;
            if (Pin0 == null && Pin1 == null) return BattHatch.Fits(Local, c, shape, tol, Free);
            var others = new[] { Pin0?.Corner.Other(this).Poly, Pin1?.Corner.Other(this).Poly }.OfType<List<Vec2>>().ToList();
            bool Joined(Vec2 p) => others.Any(o => Polygon2.Contains(o, Frame.FromBand(p.X, Mid + p.Y), 1e-6 * H));
            var at = new Vec2(c, 0);
            foreach (var q in shape)
            {
                var p = q + at;
                if (!(Polygon2.Contains(Local, p) || Polygon2.Contains(Local, p, tol) || Joined(p)) || (Free != null && !Free(p))) return false;
            }
            foreach (var v in Local) if (Polygon2.Contains(shape, v - at) && LoopDistance(shape, v - at) > tol && !Joined(v)) return false;
            return true;
        }

        /// <summary>
        /// A stroke in the frame drawn in the drawing: straight strokes in pieces of at most half the band's width, so they bend
        /// with a curved frame; clipped to the region only where a chord of a curved face cuts a hair off a head (the loops
        /// themselves fit the band). Returns how many segments it added.
        /// </summary>
        public int Add(List<Vec2> stroke, List<HatchSegment> o, int budget)
        {
            var added = 0;
            var pieces = new List<Vec2> { stroke[0] };
            for (var k = 1; k < stroke.Count; k++)
            {
                var (a, b) = (stroke[k - 1], stroke[k]);
                var m = Math.Max(1, (int)Math.Ceiling((b - a).Length / H));
                for (var i = 1; i <= m; i++) pieces.Add(a + (b - a) * ((double)i / m));
            }
            var prev = Frame.FromBand(pieces[0].X, Mid + pieces[0].Y);
            var prevIn = In(prev);
            for (var i = 1; i < pieces.Count && added < budget; i++)
            {
                var cur = Frame.FromBand(pieces[i].X, Mid + pieces[i].Y);
                var curIn = In(cur);
                if (prevIn && curIn) { o.Add(new HatchSegment(prev, cur)); added++; }
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
                    added++;
                }
                prev = cur; prevIn = curIn;
            }
            return added;
        }

        /// <summary>Inside the region, or on its own outline (a head touching a face, a loop touching a mitre), or in a
        /// corner's other band (its loops beside the corner may cross where the two meet, a butt join's line).</summary>
        bool In(Vec2 p) => Inside(Loops, p) || (Polygon2.Contains(Poly, p, 1e-6 * H) && (Others.Count == 0 || Others.Count(l => Polygon2.Contains(l, p)) % 2 == 0))
            || (Pin0 is { } a && Polygon2.Contains(a.Corner.Other(this).Poly, p, 1e-6 * H)) || (Pin1 is { } b && Polygon2.Contains(b.Corner.Other(this).Poly, p, 1e-6 * H));
    }

    /// <summary>
    /// The loops of a band with one end or both pinned to corners (<see cref="Strip.Pin0"/>, <see cref="Strip.Pin1"/>): a pinned
    /// end's loop centred where the corner wants it, on the face it wants; a free end as near its end as a whole loop fits (as
    /// <see cref="Lay"/>, on whichever face lets the loops come nearest their nominal width; a free start on the material's
    /// face first when it does within 15 %). The loops evenly spread between, their heads touching along each face (w/2 apart), as near the
    /// nominal width as a whole number of them allows; null when they would come out under 0.4 or over 1.15 of it.
    /// </summary>
    static (double C0, int N, double R, double Step, int First)? LayPinned(Strip s)
    {
        var r0 = Head * s.H * Math.Clamp(double.IsFinite(s.Scale) && s.Scale > 0 ? s.Scale : 1, 0.5, MaxScale);
        var rMax = Head * MaxScale * s.H;
        // The steps between the two end loops' centres (one loop to the next is a step): of the parity the faces ask for, the
        // radius nearest the nominal one.
        double? Radius(double len, bool even)
        {
            if (!(len > 0)) return null;
            double? best = null;
            for (var k = even ? 2 : 1; k <= (int)Math.Ceiling(len / (0.4 * r0)) + 2; k += 2)
            {
                var r = len / k;
                if (r > rMax * (1 + 1e-9) || r < 0.4 * r0) continue;
                if (best is not { } b || Math.Abs(Math.Log(r / r0)) < Math.Abs(Math.Log(b / r0))) best = r;
            }
            return best;
        }
        if (s.Pin0 is { } a && s.Pin1 is { } z)
            return Radius(z.S - a.S, a.Side == z.Side) is { } r ? (a.S, (int)Math.Round((z.S - a.S) / r) + 1, r, r, a.Side) : null;
        var ends = new Ends(s.Local, s.S0, s.S1, s.H, s.Tol, s.Free, s.Shape);
        var cands = new List<((double C0, int N, double R, double Step, int First) Laid, bool Own, double Off)>();
        foreach (var free in new[] { s.First, -s.First })
        {
            // The free end's loop found for a radius, the radius for the length that leaves, until they agree.
            var pin = (s.Pin0 ?? s.Pin1)!.Value;
            double r = r0, c = double.NaN;
            var ok = false;
            for (var it = 0; it < 30; it++)
            {
                c = s.Pin1 != null ? ends.Start(free, r, double.IsNaN(c) ? null : c - r) + r : ends.End(free, r, pin.S, double.IsNaN(c) ? null : c + r) - r;
                if (double.IsNaN(c) || Radius(s.Pin1 != null ? pin.S - c : c - pin.S, free == pin.Side) is not { } nr) break;
                // Agreed to a hair (the free end's search finds its position to the tolerance, so the radius jitters by as much;
                // the head drawn at the radius agreed stays within the outline's own tolerance).
                ok = Math.Abs(nr - r) <= 4 * s.Tol;
                r = nr;
                if (ok) break;
            }
            if (!ok) continue;
            var len = Math.Abs(pin.S - c);
            var n = (int)Math.Round(len / r) + 1;
            var cand = s.Pin1 != null ? (c, n, r, r, free) : (pin.S, n, r, r, pin.Side);
            // The material's flip names the face of the first head, at the band's start: only a free start's.
            cands.Add((cand, s.Pin1 != null && free == s.First, Math.Abs(Math.Log(r / r0))));
        }
        // The material's face first when it lets the loops come within 15 % of their width; else the nearer the width.
        if (cands.FirstOrDefault(x => x.Own && x.Off <= Math.Log(1.15)) is { Laid.N: > 0 } own) return own.Laid;
        return cands.Count == 0 ? null : cands.MinBy(x => x.Off).Laid;
    }

    /// <summary>
    /// Where two bands meet at a corner: the first runs into it (towards <see cref="I"/>, its end <see cref="AEnd"/>), the second
    /// out of it; their inner faces meet in <see cref="I"/>, their outer ones in <see cref="O"/>. Each chain ends at the inner
    /// corner (the band's frame position of <see cref="I"/>) on a loop whose head lies against the outer face; the fan between
    /// (<see cref="Lay"/>) turns those loops about the inner corner.
    /// </summary>
    sealed class Corner
    {
        public Strip A = null!, B = null!;
        public bool AEnd, BEnd;
        public Vec2 I, O, TA, TB;
        /// <summary>+1 a turn to the left (anticlockwise), −1 to the right.</summary>
        public int Turn;
        public double SA, SB;
        public int SideA, SideB;
        public List<List<Vec2>>? Strokes;

        public Strip Other(Strip s) => s == A ? B : A;

        public void Pin()
        {
            if (AEnd) A.Pin1 = (SA, SideA, this); else A.Pin0 = (SA, SideA, this);
            if (BEnd) B.Pin1 = (SB, SideB, this); else B.Pin0 = (SB, SideB, this);
        }

        /// <summary>
        /// The fan, once both chains are laid (their heads' radii known). Its loops lie on rays from the inner corner, evenly
        /// spaced in angle from the first band's outward normal to the second's, each tip on its ray at the chain's head
        /// radius from the corner (the end loops' tips where the chains put them), each head tangent to the outer face its ray
        /// meets first (to both on the diagonal). A loop seen from its tip spans ±α (sin α = r / its length); the end loops'
        /// α is the chains' own, and between them α alternates so each loop's side runs parallel to its neighbour's facing
        /// side, as in the straight chain: the count is even, the loops between two of the chains' size (on rays at the
        /// even places) and, at the odd places, the angle left over: at a right angle one loop on the diagonal, its head
        /// tangent to both outer faces, a third larger than the others; under about 55° one loop alone, on the diagonal,
        /// both chains' last. A head is held to 1.7 times the chains' (its sides then leave a sliver, never cross).
        /// False when a loop would leave the two bands or a corner of either.
        /// </summary>
        public bool Lay()
        {
            if (A.Laid is not { } la || B.Laid is not { } lb) return false;
            double ra = la.R, rb = lb.R, h = A.H;
            var outA = TA.Perp * -Turn;
            var outB = TB.Perp * -Turn;
            var turn = Math.Acos(Math.Clamp(Vec2.Dot(TA, TB), -1, 1));
            // The chains' loops' half-angle seen from their tips: the head of radius r, 2(h − r) from its tip.
            double Alpha(double r) => Math.Asin(Math.Min(1, r / (2 * (h - r))));
            double aa = Alpha(ra), ab = Alpha(rb), ae = Math.Max(aa, ab);
            // The count: none between (one loop alone) at a slight turn; else the even count whose odd loops come nearest
            // the chains' own half-angle, none thinner than 0.4 of it.
            var m = 0;
            if (turn >= 2.4 * ae)
            {
                var best = double.MaxValue;
                for (var k = 2; k <= 2 + (int)(turn / (0.7 * ae)); k += 2)
                {
                    var odd = turn / k - ae;
                    if (odd < 0.4 * ae) break;
                    var off = Math.Abs(Math.Log(odd / ae));
                    if (off < best) { best = off; m = k; }
                }
                // Too slight a turn for two loops' worth between: one loop alone, on the diagonal.
            }
            var step = m == 0 ? 0 : turn / m;
            Vec2 Ray(double th) { var (c, sn) = (Math.Cos(th * Turn), Math.Sin(th * Turn)); return new Vec2(outA.X * c - outA.Y * sn, outA.X * sn + outA.Y * c); }
            var heads = new List<(Vec2 C, double R, Vec2 Tip)>();
            var even = new double[m + 1];
            for (var k = 0; k <= m; k += 2) even[k] = m == 0 ? (aa + ab) / 2 : aa + (ab - aa) * k / m;
            for (var k = 0; k <= m; k++)
            {
                var f = m == 0 ? 0.5 : (double)k / m;
                var u = Ray(m == 0 ? turn / 2 : k * step);
                var rho = ra + (rb - ra) * f;
                var alpha = k % 2 == 0 ? even[k] : step - Math.Max(even[k - 1], even[k + 1]);
                // The head on its ray tangent to the face the ray meets first: the centre (2h − r) / cos β out, β the angle to
                // that face's normal; its tip ρ out, so r = (2h / cos β − ρ) sin α / (1 + sin α / cos β).
                var cb = Math.Max(Vec2.Dot(u, outA), Vec2.Dot(u, outB));
                if (!(cb > 1e-6)) return false;
                var sa = Math.Sin(alpha);
                var r = Math.Min(1.7 * (ra + (rb - ra) * f), (2 * h / cb - rho) * sa / (1 + sa / cb));
                // The end loops are the chains' own, to a hair.
                if (k == 0 && m > 0) r = ra;
                if (k == m && m > 0) r = rb;
                if (!(r > 0)) return false;
                heads.Add((I + u * ((2 * h - r) / cb), r, I + u * rho));
            }
            var strokes = new List<List<Vec2>>();
            var bands = new[] { A.Poly, B.Poly };
            foreach (var (c, r, tip) in heads)
            {
                if (!((c - tip).Length > 1.05 * r)) return false;
                var loop = LoopAt(c, r, tip);
                // Whole, in the two bands, and round no corner of either.
                var outline = new List<Vec2>(loop[0]);
                outline.AddRange(loop[1].Skip(1));
                var (from, to) = (outline[^1], outline[0]);
                for (var i = 1; i < 8; i++) outline.Add(from + (tip - from) * (i / 8.0));
                outline.Add(tip);
                for (var i = 1; i < 8; i++) outline.Add(tip + (to - tip) * (i / 8.0));
                foreach (var p in outline)
                    if (!bands.Any(b => Polygon2.Contains(b, p, 1e-6 * h))) return false;
                foreach (var b in bands)
                    foreach (var v in b)
                        if (Polygon2.Contains(outline, v) && LoopDistance(outline, v) > 1e-6 * h) return false;
                strokes.AddRange(loop);
            }
            Strokes = strokes;
            return true;
        }
    }

    /// <summary>A loop drawn anywhere: its head of radius <paramref name="r"/> about <paramref name="c"/>, its sides tangent to it meeting at <paramref name="tip"/>.</summary>
    static List<List<Vec2>> LoopAt(Vec2 c, double r, Vec2 tip)
    {
        var up = (c - tip).Normalized();
        var along = new Vec2(up.Y, -up.X);
        var alpha = Math.Asin(Math.Min(1, r / (c - tip).Length));
        Vec2 At(double a) => c + along * (r * Math.Cos(a)) + up * (r * Math.Sin(a));
        var o = new List<List<Vec2>>(4);
        foreach (var (a0, a1) in new[] { (-alpha, Math.PI / 2), (Math.PI / 2, Math.PI + alpha) })
            o.Add([.. Chords.Arc(new Vec2(0, 0), r, a0, a1 - a0).Select(p => c + along * p.X + up * p.Y)]);
        o.Add([At(-alpha), tip]);
        o.Add([At(Math.PI + alpha), tip]);
        return o;
    }

    /// <summary>
    /// The corners the bands share: two bands (that may join) of one thickness, an end of each meeting the other's at an L
    /// (mitred or butted: the faces meet in an inner and an outer corner, which both bands reach, neither runs past the other's
    /// outer face, and the square between them is all theirs), turning between 4° and 135°. Each end in one corner at most.
    /// </summary>
    static List<Corner> Corners(List<Strip> strips, Func<int, int, bool>? joins)
    {
        var o = new List<Corner>();
        var used = new HashSet<(Strip, bool)>();
        var boxes = strips.Select(s => (Min: new Vec2(s.Poly.Min(p => p.X), s.Poly.Min(p => p.Y)), Max: new Vec2(s.Poly.Max(p => p.X), s.Poly.Max(p => p.Y)))).ToList();
        for (var i = 0; i < strips.Count; i++)
            for (var j = i + 1; j < strips.Count; j++)
            {
                var (a, b) = (strips[i], strips[j]);
                if (a.Band == b.Band || a.Others.Count > 0 || b.Others.Count > 0) continue;
                if (joins != null && !joins(a.Band, b.Band)) continue;
                if (Math.Abs(a.H - b.H) > 0.01 * Math.Max(a.H, b.H)) continue;
                var gap = 0.05 * a.H;
                if (boxes[i].Min.X > boxes[j].Max.X + gap || boxes[j].Min.X > boxes[i].Max.X + gap || boxes[i].Min.Y > boxes[j].Max.Y + gap || boxes[j].Min.Y > boxes[i].Max.Y + gap) continue;
                foreach (var ae in new[] { true, false })
                    foreach (var be in new[] { true, false })
                    {
                        if (used.Contains((a, ae)) || used.Contains((b, be))) continue;
                        if (Make(a, ae, b, be) is not { } c) continue;
                        o.Add(c);
                        used.Add((a, ae)); used.Add((b, be));
                    }
            }
        return o;
    }

    static Corner? Make(Strip a, bool ae, Strip b, bool be)
    {
        var h = a.H;
        var tol = 0.02 * h;
        // Each band's direction at its end, its faces there: the first's towards the corner, the second's away from it.
        (Vec2 T, Vec2 Left, Vec2 Right) End(Strip s, bool end, bool into)
        {
            var se = end ? s.S1 : s.S0;
            var p0 = s.Frame.FromBand(se, s.Mid);
            var p1 = s.Frame.FromBand(se - (end ? 1 : -1) * s.H, s.Mid);
            var t = (p0 - p1).Normalized();
            Vec2 fl = s.Frame.FromBand(se, s.Mid + s.H), fr = s.Frame.FromBand(se, s.Mid - s.H);
            // Facing along t, the frame's left face is on the left when t runs with the frame.
            var with = end == into;
            return (into ? t : -t, with ? fl : fr, with ? fr : fl);
        }
        var (ta, la, rta) = End(a, ae, true);
        var (tb, lb, rtb) = End(b, be, false);
        var turn = Math.Atan2(Vec2.Cross(ta, tb), Vec2.Dot(ta, tb));
        if (!(Math.Abs(turn) >= 4 * Math.PI / 180 && Math.Abs(turn) <= 135 * Math.PI / 180)) return null;
        var sg = Math.Sign(turn);
        var (ia, oa) = sg > 0 ? (la, rta) : (rta, la);
        var (ib, ob) = sg > 0 ? (lb, rtb) : (rtb, lb);
        if (Polyline2.LineIntersection(ia, ta, ib, tb) is not { } i || Polyline2.LineIntersection(oa, ta, ob, tb) is not { } o) return null;
        // Both bands reach the inner corner, one of them the outer (a butt join: the band that runs through).
        if (!Polygon2.Contains(a.Poly, i, tol) || !Polygon2.Contains(b.Poly, i, tol) || !(Polygon2.Contains(a.Poly, o, tol) || Polygon2.Contains(b.Poly, o, tol))) return null;
        var p = i + ta.Perp * (-sg * 2 * h);
        var q = i + tb.Perp * (-sg * 2 * h);
        // Each band ends at the corner: along it no further than the outer corner, or than where its inner face meets the
        // other's outer face (a butt join at an obtuse corner: the band that runs through).
        double Past(Strip s, bool end, Vec2 inner, Vec2 t, Vec2 otherOuter, Vec2 ot)
        {
            var far = s.Frame.ToBand(o).S;
            if (Polyline2.LineIntersection(inner, t, otherOuter, ot) is { } x) far = end ? Math.Max(far, s.Frame.ToBand(x).S) : Math.Min(far, s.Frame.ToBand(x).S);
            return end ? s.S1 - far : far - s.S0;
        }
        if (Past(a, ae, ia, ta, ob, tb) > tol || Past(b, be, ib, tb, oa, ta) > tol) return null;
        // The square between the corners is theirs.
        foreach (var x in new[] { (i + o) * 0.5, (i + p + o + q) * 0.25, (p + o) * 0.45 + i * 0.1, (q + o) * 0.45 + i * 0.1 })
            if (!Polygon2.Contains(a.Poly, x, tol) && !Polygon2.Contains(b.Poly, x, tol)) return null;
        // The inner corner along each band, with room before it.
        double sa = a.Frame.ToBand(i).S, sb = b.Frame.ToBand(i).S;
        if (ae ? sa - a.S0 < h : a.S1 - sa < h) return null;
        if (be ? sb - b.S0 < h : b.S1 - sb < h) return null;
        return new Corner
        {
            A = a, B = b, AEnd = ae, BEnd = be, I = i, O = o, TA = ta, TB = tb, Turn = sg, SA = sa, SB = sb,
            // The outer face (the right of the way round a left turn) in each band's frame.
            SideA = -sg * (ae ? 1 : -1),
            SideB = -sg * (be ? -1 : 1),
        };
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
