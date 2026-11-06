using Lapin.Geometry;

namespace Lapin.Platform;

/// <summary>
/// A part of a foreign wall (a layer as an IfcBuildingElementPart) read from its triangles in the wall's axis frame
/// (docs/spec-ifc.md, Pereții din piese): along the axis from its start (u), across it to the left (v), and up (world z).
/// <see cref="Top"/> and <see cref="Bottom"/>: its body's upper and lower outline seen from the side, as (u, z) points
/// from its start to its end (a step is two points at one u), straight across where the body has a gap along the axis
/// (<see cref="Gaps"/>: a door through the whole layer). Its two faces
/// across (<see cref="V0"/>, <see cref="V1"/>) and where each ends along the axis (<see cref="Right"/> on V0,
/// <see cref="Left"/> on V1: a square end has both at one u, a mitre or a cut not). <see cref="Band"/>: every vertex lies
/// on one of the two faces (a layer, not a notched or stepped block). <see cref="Volume"/>: the body's own, from its triangles.
/// </summary>
public sealed record PartProfile(IReadOnlyList<Vec2>? Top, IReadOnlyList<Vec2>? Bottom, double V0, double V1, (double Start, double End) Right, (double Start, double End) Left, bool Band, double Volume,
    IReadOnlyList<(double From, double To)>? Gaps = null)
{
    public double Thickness => V1 - V0;

    /// <summary>The body's volume as a band would have it: its side outline's area across its thickness, with its ends as its faces say (the mean of the two faces' lengths over the outline's).</summary>
    public double? BandVolume()
    {
        if (Top is not { Count: >= 2 } top || Bottom is not { Count: >= 2 } bottom) return null;
        var length = top[^1].X - top[0].X;
        if (!(length > 1e-9)) return null;
        var area = PartProfiles.Area(top) - PartProfiles.Area(bottom);
        var faces = (Right.End - Right.Start + Left.End - Left.Start) / 2;
        return area * Thickness * faces / length;
    }
}

/// <summary>Reading <see cref="PartProfile"/>s from triangles, and the outlines' arithmetic the recognisers share.</summary>
public static class PartProfiles
{
    /// <summary>Beyond so many triangles a part is no layer (a curved or detailed body): not read.</summary>
    const int MaxTriangles = 4000;
    const double FaceTol = 1e-3;

    /// <summary>The part's profile in the frame of the line from <paramref name="origin"/> along <paramref name="dir"/> (a unit vector), or null without triangles.</summary>
    public static PartProfile? Of(IEnumerable<(IReadOnlyList<Vec3> V, IReadOnlyList<(int A, int B, int C)> T)> meshes, Vec2 origin, Vec2 dir)
    {
        var n = dir.Perp;
        var tris = new List<(Vec2 A, Vec2 B, Vec2 C)>();
        var us = new List<double>();
        double v0 = double.MaxValue, v1 = double.MinValue, volume = 0;
        var verts = new List<(double U, double V)>();
        var count = 0;
        foreach (var (vs, ts) in meshes)
        {
            count += ts.Count;
            if (count > MaxTriangles) return null;
            foreach (var p in vs)
            {
                var q = new Vec2(p.X, p.Y) - origin;
                var (u, v) = (Vec2.Dot(q, dir), Vec2.Dot(q, n));
                verts.Add((u, v));
                v0 = Math.Min(v0, v); v1 = Math.Max(v1, v);
            }
            foreach (var (a, b, c) in ts)
            {
                var (pa, pb, pc) = (vs[a], vs[b], vs[c]);
                volume += Vec3.Dot(pa, Vec3.Cross(pb, pc)) / 6;
                Vec2 S(Vec3 p) => new(Vec2.Dot(new Vec2(p.X, p.Y) - origin, dir), p.Z);
                var (sa, sb, sc) = (S(pa), S(pb), S(pc));
                // Seen from the side, a face across the part (its top, its bottom, an end) is a line: its edges stand for it.
                if (Math.Abs(Vec2.Cross(sb - sa, sc - sa)) < 1e-10) continue;
                tris.Add((sa, sb, sc));
                us.Add(sa.X); us.Add(sb.X); us.Add(sc.X);
            }
        }
        if (verts.Count == 0) return null;
        (double, double) Ends(Func<double, bool> on)
        {
            var at = verts.Where(x => on(x.V)).Select(x => x.U).ToList();
            return at.Count == 0 ? (double.NaN, double.NaN) : (at.Min(), at.Max());
        }
        var right = Ends(v => v <= v0 + FaceTol);
        var left = Ends(v => v >= v1 - FaceTol);
        var band = verts.All(x => x.V <= v0 + FaceTol || x.V >= v1 - FaceTol);
        var breaks = Breakpoints(us);
        var gaps = new List<(double, double)>();
        var top = Envelope(tris, breaks, top: true, gaps);
        return new PartProfile(top, Envelope(tris, breaks, top: false, null), v0, v1, right, left, band, Math.Abs(volume), gaps);
    }

    static List<double> Breakpoints(List<double> us)
    {
        us.Sort();
        var o = new List<double>();
        foreach (var u in us) if (o.Count == 0 || u - o[^1] > 1e-5) o.Add(u);
        return o;
    }

    /// <summary>Where the triangles seen from the side reach at <paramref name="u"/>: the highest (or lowest) point of any; null where none is.</summary>
    static double? Reach(List<(Vec2 A, Vec2 B, Vec2 C)> tris, double u, bool top)
    {
        double? best = null;
        foreach (var (a, b, c) in tris)
        {
            if (u < Math.Min(a.X, Math.Min(b.X, c.X)) || u > Math.Max(a.X, Math.Max(b.X, c.X))) continue;
            foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
            {
                var (lo, hi) = p.X <= q.X ? (p, q) : (q, p);
                if (u < lo.X || u > hi.X || hi.X - lo.X < 1e-12) continue;
                var z = lo.Y + (hi.Y - lo.Y) * (u - lo.X) / (hi.X - lo.X);
                if (best == null || (top ? z > best : z < best)) best = z;
            }
        }
        return best;
    }

    /// <summary>
    /// The triangles' upper (or lower) outline from the first breakpoint to the last: at each, its value just before and
    /// just after (a step between), and between two the middle where the outline is not straight. Across a gap (nothing
    /// there) straight from one side to the other, the gap noted in <paramref name="gaps"/>.
    /// </summary>
    static List<Vec2>? Envelope(List<(Vec2 A, Vec2 B, Vec2 C)> tris, List<double> breaks, bool top, List<(double, double)>? gaps)
    {
        if (breaks.Count < 2) return null;
        const double eps = 2e-6;
        var o = new List<Vec2>();
        double? gapFrom = null;
        for (var i = 0; i < breaks.Count; i++)
        {
            var u = breaks[i];
            double? before = i == 0 ? null : Reach(tris, u - eps, top);
            double? after = i == breaks.Count - 1 ? null : Reach(tris, u + eps, top);
            if (i > 0 && before == null && gapFrom == null) gapFrom = breaks[i - 1];
            if (i > 0 && before != null && o.Count > 0 && gapFrom == null)
            {
                // The middle of the span before: the outline turns there when two faces cross.
                var mid = (breaks[i - 1] + u) / 2;
                if (Reach(tris, mid, top) is { } zm)
                {
                    var (p, q) = (o[^1], new Vec2(u, before.Value));
                    var lin = p.Y + (q.Y - p.Y) * (mid - p.X) / Math.Max(q.X - p.X, 1e-12);
                    if (Math.Abs(zm - lin) > 2e-4) o.Add(new Vec2(mid, zm));
                }
            }
            if (before is { } b) o.Add(new Vec2(u, b));
            if (after != null && gapFrom is { } g) { gaps?.Add((g, u)); gapFrom = null; }
            if (after is { } a && (before == null || Math.Abs(a - before.Value) > 1e-4)) o.Add(new Vec2(u, a));
        }
        // Read a hair to either side of each point: rounded to 10 µm, a millimetre's outline comes back as drawn.
        return o.Count >= 2 ? Simplify([.. o.Select(q => new Vec2(Math.Round(q.X, 5), Math.Round(q.Y, 5)))]) : null;
    }

    /// <summary>An outline without the points that lie on the line between their neighbours.</summary>
    public static List<Vec2> Simplify(List<Vec2> p, double tol = 1e-4)
    {
        var o = new List<Vec2>();
        foreach (var q in p)
        {
            if (o.Count > 0 && (q - o[^1]).Length < 1e-9) continue;
            while (o.Count >= 2)
            {
                var (a, b) = (o[^2], o[^1]);
                var ac = q - a;
                if (ac.Length < 1e-12 || Math.Abs(Vec2.Cross(ac, b - a)) / ac.Length > tol || Vec2.Dot(b - a, q - b) < 0) break;
                o.RemoveAt(o.Count - 1);
            }
            o.Add(q);
        }
        return o;
    }

    /// <summary>The area under an outline (u, z), down to z = 0.</summary>
    public static double Area(IReadOnlyList<Vec2> p)
    {
        double a = 0;
        for (var i = 0; i + 1 < p.Count; i++) a += (p[i + 1].X - p[i].X) * (p[i].Y + p[i + 1].Y) / 2;
        return a;
    }

    /// <summary>An outline's value at <paramref name="u"/>: linear between its points, flat past its ends; at a step, the lower (<paramref name="low"/>) or the higher side.</summary>
    public static double At(IReadOnlyList<Vec2> p, double u, bool low = true)
    {
        if (u <= p[0].X) return p[0].Y;
        if (u >= p[^1].X) return p[^1].Y;
        double? found = null;
        for (var i = 0; i + 1 < p.Count; i++)
        {
            var (a, b) = (p[i], p[i + 1]);
            if (u < a.X - 1e-12 || u > b.X + 1e-12) continue;
            var z = b.X - a.X < 1e-12 ? (low ? Math.Min(a.Y, b.Y) : Math.Max(a.Y, b.Y)) : a.Y + (b.Y - a.Y) * (u - a.X) / (b.X - a.X);
            found = found == null ? z : low ? Math.Min(found.Value, z) : Math.Max(found.Value, z);
        }
        return found ?? p[^1].Y;
    }
}
