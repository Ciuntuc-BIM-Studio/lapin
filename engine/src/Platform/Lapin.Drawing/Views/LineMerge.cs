using Lapin.Geometry;

namespace Lapin.Drawing.Views;

/// <summary>
/// Cleans a pen's segments: collinear pieces that overlap or touch become one
/// line, duplicates go, and so do pieces shorter than the tolerance. The hidden
/// line removal hands an edge back in as many pieces as faces split it; a
/// drawing (and a DXF) reads better with whole lines.
/// </summary>
public static class LineMerge
{
    public static List<(Vec2 A, Vec2 B)> Merge(IReadOnlyList<(Vec2 A, Vec2 B)> segments, double tol = 1e-5)
    {
        const double angTol = 1e-6;
        // Each segment as (angle in [-π/2, π/2), offset from the origin, interval along its direction).
        var lines = new List<(double Th, double C, double T0, double T1)>(segments.Count);
        foreach (var (a, b) in segments)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, len = Math.Sqrt(dx * dx + dy * dy);
            if (len < tol) continue;
            dx /= len; dy /= len;
            if (dx < 0 || (dx == 0 && dy < 0)) { dx = -dx; dy = -dy; }
            var th = Math.Atan2(dy, dx);
            if (th > Math.PI / 2 - angTol) { th -= Math.PI; dx = -dx; dy = -dy; }
            double t0 = a.X * dx + a.Y * dy, t1 = b.X * dx + b.Y * dy;
            lines.Add((th, -a.X * dy + a.Y * dx, Math.Min(t0, t1), Math.Max(t0, t1)));
        }
        lines.Sort((p, q) => p.Th.CompareTo(q.Th));
        var result = new List<(Vec2, Vec2)>();
        void Emit(double th, double c, double t0, double t1)
        {
            double dx = Math.Cos(th), dy = Math.Sin(th);
            result.Add((new Vec2(t0 * dx - c * dy, t0 * dy + c * dx), new Vec2(t1 * dx - c * dy, t1 * dy + c * dx)));
        }
        for (var i = 0; i < lines.Count;)
        {
            var j = i;
            while (j + 1 < lines.Count && lines[j + 1].Th - lines[j].Th < angTol) j++;
            var group = lines.GetRange(i, j - i + 1);
            group.Sort((p, q) => p.C.CompareTo(q.C));
            for (var k = 0; k < group.Count;)
            {
                var m = k;
                while (m + 1 < group.Count && group[m + 1].C - group[m].C < tol) m++;
                var run = group.GetRange(k, m - k + 1);
                var th = run.Average(r => r.Th); var c = run.Average(r => r.C);
                run.Sort((p, q) => p.T0.CompareTo(q.T0));
                double s = run[0].T0, e = run[0].T1;
                foreach (var r in run.Skip(1))
                {
                    if (r.T0 <= e + tol) { e = Math.Max(e, r.T1); continue; }
                    Emit(th, c, s, e); (s, e) = (r.T0, r.T1);
                }
                Emit(th, c, s, e);
                k = m + 1;
            }
            i = j + 1;
        }
        return result;
    }
}
