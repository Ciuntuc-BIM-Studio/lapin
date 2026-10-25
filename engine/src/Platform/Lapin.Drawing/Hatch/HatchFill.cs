using Lapin.Geometry;

namespace Lapin.Drawing.Hatch;

/// <summary>One hatch stroke; a dot has A equal to B.</summary>
public readonly record struct HatchSegment(Vec2 A, Vec2 B);

/// <summary>How a pattern lies on a region: its scale (all pattern lengths), its rotation (°) about its origin, a cap on strokes.</summary>
public sealed record HatchOptions(double Scale, double AngleDeg = 0, Vec2? Origin = null, int MaxSegments = 200_000);

/// <summary>
/// Hatch strokes (the old Lapin's fill): each line family of a pattern is
/// clipped against a region, even-odd over all its loops (islands are holes,
/// islands in holes fill again), and its dashes applied.
/// <para>
/// Per family, every edge is projected once into the family's frame (h across,
/// t along). Line k sits at h = k·deltaY; an edge crosses it under the
/// half-open rule min(h) ≤ h_k &lt; max(h), so a vertex exactly on a line never
/// flips the parity. Edges are swept in h order with an active list.
/// </para>
/// </summary>
public static class HatchFill
{
    sealed record FEdge(double H0, double T0, double H1, double T1)
    {
        public double HMin => Math.Min(H0, H1);
        public double HMax => Math.Max(H0, H1);
    }

    sealed class Emitter(int max)
    {
        public readonly List<HatchSegment> Out = [];
        public readonly int Max = Math.Max(0, max);
        public bool Truncated;
        public void Emit(double lx, double ly, double dx, double dy, double t0, double t1)
        {
            if (Out.Count >= Max) { Truncated = true; return; }
            Out.Add(new HatchSegment(new Vec2(lx + t0 * dx, ly + t0 * dy), new Vec2(lx + t1 * dx, ly + t1 * dy)));
        }
    }

    /// <summary>The strokes of a pattern inside the even-odd region of the loops. A solid pattern gives none (the region is filled instead).</summary>
    public static (List<HatchSegment> Segments, bool Truncated) Segments(IReadOnlyList<IReadOnlyList<Vec2>> loops, HatchPattern pattern, HatchOptions o)
    {
        var em = new Emitter(o.MaxSegments);
        if (pattern.Solid || !(o.Scale > 0) || !double.IsFinite(o.Scale)) return (em.Out, false);
        var usable = loops.Where(l => l.Count >= 3).ToList();
        if (usable.Count == 0) return (em.Out, false);
        foreach (var f in pattern.Families)
        {
            if (em.Truncated) break;
            Family(usable, f, o, em);
        }
        return (em.Out, em.Truncated);
    }

    static void Family(List<IReadOnlyList<Vec2>> loops, HatchLineFamily fam, HatchOptions o, Emitter em)
    {
        var s = o.Scale;
        var spacing = fam.DeltaY * s;
        if (!(Math.Abs(spacing) > 1e-12) || !double.IsFinite(spacing)) return;
        var shift = fam.DeltaX * s;
        var dashes = fam.Dashes.Select(v => v * s).ToArray();
        var period = dashes.Sum(Math.Abs);
        var draws = dashes.Length == 0 || dashes.Any(v => v >= 0);
        if (!draws) return;
        if (dashes.Length > 0 && !(period > 1e-12)) return;

        var rot = o.AngleDeg * Math.PI / 180;
        var (cr, sr) = (Math.Cos(rot), Math.Sin(rot));
        var origin = o.Origin ?? new Vec2(0, 0);
        var (fx, fy) = (fam.Origin.X * s, fam.Origin.Y * s);
        var bx = origin.X + fx * cr - fy * sr;
        var by = origin.Y + fx * sr + fy * cr;
        var a = (fam.AngleDeg + o.AngleDeg) * Math.PI / 180;
        var (dx, dy) = (Math.Cos(a), Math.Sin(a));

        var edges = new List<FEdge>();
        foreach (var loop in loops)
            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
            {
                var (px, py) = (loop[j].X - bx, loop[j].Y - by);
                var (qx, qy) = (loop[i].X - bx, loop[i].Y - by);
                var h0 = -px * dy + py * dx;
                var h1 = -qx * dy + qy * dx;
                if (h0 == h1) continue;
                edges.Add(new FEdge(h0, px * dx + py * dy, h1, qx * dx + qy * dy));
            }
        if (edges.Count == 0) return;
        edges.Sort((p, q) => p.HMin.CompareTo(q.HMin));
        var hLo = edges.Min(e => e.HMin);
        var hHi = edges.Max(e => e.HMax);
        var step = Math.Abs(spacing);
        var sign = spacing > 0 ? 1 : -1;
        var jFirst = (long)Math.Ceiling(hLo / step);
        var jLast = (long)Math.Floor(hHi / step);
        if (jLast < jFirst) return;
        if (jLast - jFirst > (long)em.Max * 10 + 1000) { em.Truncated = true; return; }

        var active = new List<FEdge>();
        var ts = new List<double>();
        var next = 0;
        for (var j = jFirst; j <= jLast; j++)
        {
            var h = j * step;
            var along = j * sign * shift;
            while (next < edges.Count && edges[next].HMin <= h) active.Add(edges[next++]);
            ts.Clear();
            active.RemoveAll(e => e.HMax <= h);
            foreach (var e in active)
            {
                var u = (h - e.H0) / (e.H1 - e.H0);
                ts.Add(e.T0 + u * (e.T1 - e.T0) - along);
            }
            if (ts.Count < 2) continue;
            ts.Sort();
            var lx = bx + along * dx - h * dy;
            var ly = by + along * dy + h * dx;
            for (var i = 0; i + 1 < ts.Count; i += 2)
            {
                var (t0, t1) = (ts[i], ts[i + 1]);
                if (!(t1 - t0 > 1e-12)) continue;
                if (dashes.Length == 0) em.Emit(lx, ly, dx, dy, t0, t1);
                else Dashed(em, lx, ly, dx, dy, t0, t1, dashes, period);
                if (em.Truncated) return;
            }
        }
    }

    static void Dashed(Emitter em, double lx, double ly, double dx, double dy, double t0, double t1, double[] dashes, double period)
    {
        var pos = Math.Floor(t0 / period) * period;
        while (pos <= t1)
            foreach (var v in dashes)
            {
                var len = Math.Abs(v);
                if (v > 0)
                {
                    var (a, b) = (Math.Max(pos, t0), Math.Min(pos + len, t1));
                    if (b > a) em.Emit(lx, ly, dx, dy, a, b);
                }
                else if (v == 0 && pos >= t0 && pos <= t1) em.Emit(lx, ly, dx, dy, pos, pos);
                if (em.Truncated) return;
                pos += len;
                if (pos > t1) return;
            }
    }
}
