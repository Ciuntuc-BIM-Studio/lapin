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
/// Batt insulation: the looped line drawn along a layer, between its two faces, as draughtsmen draw mineral wool (AutoCAD's
/// BATTING, Revit's batt insulation): a loop on one face, a stroke across, a loop on the other face, again and again along
/// the layer. Not a .pat family (a line family cannot draw a curve): a curve laid for each band in the band's own frame,
/// <c>s</c> along it and <c>y</c> across it, the curve s = k·t − A·sin 2t, y = h·cos t (h half the band's width, k 0.45 h,
/// A 0.7 h: each face touched at its loops, the strokes crossing between them), clipped to the band's polygon (mitred ends,
/// corners). The frame is the element's axis when the drawing gives one (<see cref="BandAxis"/>: a wall in a plan, so the
/// line follows the wall at any angle and round a curved one), else the band's longest edge. The faces are where the band's
/// sides along the frame lie (the edges that run along it), so a mitred end or a corner piece does not shift the line off
/// the layer; a band with no side along the frame takes its whole extent across.
/// </summary>
public static class BattHatch
{
    public const string Name = "BATT";
    public static readonly HatchPattern Pattern = new(Name, "Batt insulation, the looped line along the layer", []);

    public static bool Is(HatchPattern? p) => p != null && p.Name.Equals(Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>The curve's pitch along the band (k) and the reach of its loops (A), as fractions of half the band's width.</summary>
    public const double Pitch = 0.45, Loop = 0.7;
    /// <summary>The steps one turn of the curve (one loop on each face) is drawn in.</summary>
    const int StepsPerTurn = 48;

    /// <param name="loops">The region, even-odd (metres, or whatever unit the drawing is in).</param>
    /// <param name="minWidth">Bands thinner than this (same unit) get no line: too thin to read.</param>
    /// <param name="axis">The frame the band runs in (an element's axis); null: the band's longest edge.</param>
    public static List<HatchSegment> Segments(IReadOnlyList<IReadOnlyList<Vec2>> loops, double minWidth = 0, int maxSegments = 200_000, BandAxis? axis = null)
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
            double k = Pitch * h, amp = Loop * h;
            // The curve's phase is the frame's (s = 0 at t = 0), so a band's line does not move when its ends do; it starts a
            // turn before the band and runs a loop past it, the clip trimming the rest.
            var turn = 2 * Math.PI;
            var tStart = Math.Floor((s0 - 2 * h) / (k * turn)) * turn;
            var tEnd = (s1 + 2 * h) / k;
            var dt = turn / StepsPerTurn;
            Vec2 At(double t) => frame.FromBand(k * t - amp * Math.Sin(2 * t), mid + h * Math.Cos(t));
            var prev = At(tStart);
            var prevIn = Inside(loops, prev);
            for (var i = 1; tStart + i * dt <= tEnd && o.Count < maxSegments; i++)
            {
                var cur = At(tStart + i * dt);
                var curIn = Inside(loops, cur);
                if (prevIn && curIn) o.Add(new HatchSegment(prev, cur));
                else if (prevIn != curIn)
                {
                    // One end inside: the crossing found by halving, the inside part kept.
                    var (a, b) = (prev, cur);
                    var aIn = prevIn;
                    for (var j = 0; j < 12; j++)
                    {
                        var m = new Vec2((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                        if (Inside(loops, m) == aIn) a = m; else b = m;
                    }
                    o.Add(prevIn ? new HatchSegment(prev, a) : new HatchSegment(b, cur));
                }
                prev = cur; prevIn = curIn;
            }
        }
        return o;
    }

    /// <summary>
    /// A band's own frame: along its longest edge, from a band's width before where the band starts along it. A band longer
    /// across than along is drawn across (the loops run the short way, as on a wall seen in section).
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
        if (a1 - a0 < c1 - c0) { (dir, nrm) = (nrm, dir * -1); (a0, a1, c0, c1) = (c0, c1, -a1, -a0); }
        // Its start a band's width before the band's (the curve's phase there), as the batt was laid before decision 725.
        return BandAxis.Straight(dir * (a0 - (c1 - c0)) + nrm * c0, dir);
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
