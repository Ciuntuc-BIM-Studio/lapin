using Lapin.Geometry;

namespace Lapin.Drawing.Hatch;

/// <summary>
/// Batt insulation: the looped line drawn along a layer, on its thickness,
/// as draughtsmen draw mineral wool. Not a .pat family: a curve built for
/// each band from its own axis and width. The band is read from the polygon
/// (its longest edge gives the direction, its extent across gives the width);
/// the curve x = k·t − A·sin 2t, y = h·cos t loops at both edges and crosses
/// itself between them; what falls outside the polygon (mitred ends,
/// corners) is clipped off.
/// </summary>
public static class BattHatch
{
    public const string Name = "BATT";
    public static readonly HatchPattern Pattern = new(Name, "Batt insulation, the looped line along the layer", []);

    public static bool Is(HatchPattern? p) => p != null && p.Name.Equals(Name, StringComparison.OrdinalIgnoreCase);

    /// <param name="loops">The region, even-odd (metres, or whatever unit the drawing is in).</param>
    /// <param name="minWidth">Bands thinner than this (same unit) get no line: too thin to read.</param>
    public static List<HatchSegment> Segments(IReadOnlyList<IReadOnlyList<Vec2>> loops, double minWidth = 0, int maxSegments = 200_000)
    {
        var o = new List<HatchSegment>();
        foreach (var raw in loops)
        {
            var poly = Clean(raw);
            if (poly.Count < 3 || Math.Abs(Area(poly)) < 1e-12) continue;
            // The band: along its longest edge, across it.
            Vec2 dir = default; double best = 0;
            for (var i = 0; i < poly.Count; i++) { var e = poly[(i + 1) % poly.Count] - poly[i]; if (e.Length > best) { best = e.Length; dir = e; } }
            if (best <= 0) continue;
            dir = dir.Normalized();
            var nrm = new Vec2(-dir.Y, dir.X);
            double a0 = double.MaxValue, a1 = double.MinValue, c0 = double.MaxValue, c1 = double.MinValue;
            foreach (var p in poly) { var a = Vec2.Dot(p, dir); var c = Vec2.Dot(p, nrm); a0 = Math.Min(a0, a); a1 = Math.Max(a1, a); c0 = Math.Min(c0, c); c1 = Math.Max(c1, c); }
            var h = (c1 - c0) / 2;
            if (2 * h < minWidth || h <= 0) continue;
            // A band longer across than along is drawn across: the loops run the short way, as on a wall seen in section.
            if (a1 - a0 < c1 - c0) { (dir, nrm) = (nrm, dir * -1); (a0, a1, c0, c1) = (c0, c1, -a1, -a0); h = (c1 - c0) / 2; }
            var mid = (c0 + c1) / 2;
            var origin = dir * a0 + nrm * mid;
            double k = 0.45 * h, amp = 0.7 * h;
            // Start a loop's length before the band and run a loop past it; the clip trims the rest.
            var tEnd = (a1 - a0 + 4 * h) / k;
            var dt = Math.PI / 24;
            Vec2 At(double t) => origin + dir * (k * t - amp * Math.Sin(2 * t) - 2 * h) + nrm * (h * Math.Cos(t));
            var prev = At(0);
            var prevIn = Inside(loops, prev);
            for (var t = dt; t <= tEnd && o.Count < maxSegments; t += dt)
            {
                var cur = At(t);
                var curIn = Inside(loops, cur);
                if (prevIn && curIn) o.Add(new HatchSegment(prev, cur));
                else if (prevIn != curIn)
                {
                    // One end inside: the crossing found by halving, the inside part kept.
                    var (a, b) = (prev, cur);
                    var aIn = prevIn;
                    for (var i = 0; i < 12; i++)
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

    static bool Inside(IReadOnlyList<IReadOnlyList<Vec2>> loops, Vec2 p)
    {
        var inside = false;
        foreach (var loop in loops)
            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
            {
                var (a, b) = (loop[i], loop[j]);
                if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }
        return inside;
    }

    static double Area(IReadOnlyList<Vec2> poly)
    {
        double a = 0;
        for (var i = 0; i < poly.Count; i++) { var p = poly[i]; var q = poly[(i + 1) % poly.Count]; a += p.X * q.Y - q.X * p.Y; }
        return a / 2;
    }

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
