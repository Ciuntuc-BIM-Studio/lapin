using Lapin.Geometry;

namespace Lapin.Analysis.Solver;

/// <summary>The internal forces at a section of a bar (kN, kN·m), on its positive face (the part after the section acting on the part before it), local axes: N (tension positive), Vy, Vz, T, My (about local 2), Mz (about local 3; a level beam's sagging moment positive).</summary>
public readonly record struct Section(double X, double N, double Vy, double Vz, double T, double My, double Mz)
{
    public double this[int q] => q switch { 0 => N, 1 => Vy, 2 => Vz, 3 => T, 4 => My, _ => Mz };
    public static readonly string[] Names = ["N", "Vy", "Vz", "T", "My", "Mz"];
    public static int IndexOf(string name) => Array.IndexOf(Names, name);
}

/// <summary>A quantity's largest and smallest value along a bar and where.</summary>
public readonly record struct Extreme(double XMax, double Max, double XMin, double Min);

/// <summary>
/// A bar's internal forces and displacements along its span, from its end forces and span loads by statics (exact for linear
/// loads and point forces): 11 stations, and each quantity's extremes, found exactly between the loads' ends (a cubic's
/// stationary points: the moment's largest under a uniform load where the shear is nought). The displacement at a station
/// integrates the curvature (and shear's slip) from the bar's own i end, its released rotations recovered.
/// </summary>
public static class Stations
{
    public const int Count = 11;

    /// <summary>The internal forces at x (a point force exactly at x not yet passed: the value just before it).</summary>
    public static Section At(FrameElement e, ElementResult r, double x)
    {
        var f = r.Forces;
        double ix = 0, iy = 0, iz = 0, my = 0, mz = 0;
        foreach (var l in r.Loads)
        {
            if (l.Point)
            {
                if (l.A >= x) continue;
                ix += l.Xa; iy += l.Ya; iz += l.Za;
                my += l.Za * (x - l.A); mz += l.Ya * (x - l.A);
                continue;
            }
            var (a, b) = (Math.Max(0, l.A), Math.Min(Math.Min(e.L, l.B), x));
            if (b - a <= 1e-12) continue;
            var (mid, half) = ((a + b) / 2, (b - a) / 2);
            foreach (var (g, w) in FrameElement.Gauss3)
            {
                var s = mid + half * g;
                var (qx, qy, qz) = l.At(s);
                var ww = w * half;
                ix += qx * ww; iy += qy * ww; iz += qz * ww;
                my += qz * (x - s) * ww; mz += qy * (x - s) * ww;
            }
        }
        return new Section(x, -(f[0] + ix), -(f[1] + iy), -(f[2] + iz), -f[3], -(f[4] + f[2] * x + my), -(f[5] - f[1] * x - mz));
    }

    public static List<Section> Along(FrameElement e, ElementResult r, int count = Count) =>
        Enumerable.Range(0, count).Select(k => At(e, r, e.L * k / (count - 1))).ToList();

    /// <summary>The points where the loads start, end or act: between them every quantity is a polynomial of degree three at most.</summary>
    public static List<double> Breaks(FrameElement e, ElementResult r)
    {
        var xs = new List<double> { 0, e.L };
        foreach (var l in r.Loads) { xs.Add(Math.Clamp(l.A, 0, e.L)); xs.Add(Math.Clamp(l.B, 0, e.L)); }
        xs.Sort();
        var o = new List<double>();
        foreach (var x in xs) if (o.Count == 0 || x - o[^1] > 1e-9) o.Add(x);
        return o;
    }

    /// <summary>Each quantity's extremes along the bar, exact.</summary>
    public static Extreme[] Extremes(FrameElement e, ElementResult r)
    {
        var best = new Extreme[6];
        for (var q = 0; q < 6; q++) best[q] = new Extreme(0, double.NegativeInfinity, 0, double.PositiveInfinity);
        void Take(int q, double x, double v)
        {
            var b = best[q];
            if (v > b.Max) b = b with { Max = v, XMax = x };
            if (v < b.Min) b = b with { Min = v, XMin = x };
            best[q] = b;
        }
        var breaks = Breaks(e, r);
        for (var k = 0; k + 1 < breaks.Count; k++)
        {
            var (x0, x1) = (breaks[k], breaks[k + 1]);
            var h = x1 - x0;
            // Four samples inside fix each cubic; its ends and stationary points are then exact.
            double[] ts = [0.1, 0.4, 0.6, 0.9];
            var samples = ts.Select(t => At(e, r, x0 + h * t)).ToArray();
            for (var q = 0; q < 6; q++)
            {
                var ys = samples.Select(s => s[q]).ToArray();
                var c = Cubic(ts, ys);
                double P(double t) => ((c[3] * t + c[2]) * t + c[1]) * t + c[0];
                Take(q, x0, P(0));
                Take(q, x1, P(1));
                // P'(t) = 3c3 t² + 2c2 t + c1.
                var (qa, qb, qc) = (3 * c[3], 2 * c[2], c[1]);
                var roots = new List<double>();
                if (Math.Abs(qa) > 1e-12 * (Math.Abs(qb) + Math.Abs(qc) + 1e-300))
                {
                    var disc = qb * qb - 4 * qa * qc;
                    if (disc >= 0) { var sq = Math.Sqrt(disc); roots.Add((-qb + sq) / (2 * qa)); roots.Add((-qb - sq) / (2 * qa)); }
                }
                else if (Math.Abs(qb) > 1e-300) roots.Add(-qc / qb);
                foreach (var t in roots) if (t > 0 && t < 1) Take(q, x0 + h * t, P(t));
            }
        }
        return best;
    }

    /// <summary>The cubic c0 + c1 t + c2 t² + c3 t³ through four points (Lagrange, then expanded).</summary>
    static double[] Cubic(double[] t, double[] y)
    {
        var c = new double[4];
        for (var i = 0; i < 4; i++)
        {
            // Π_{j≠i} (t − tj) / (ti − tj), expanded.
            var poly = new double[] { 1, 0, 0, 0 };
            var den = 1.0;
            var deg = 0;
            for (var j = 0; j < 4; j++)
            {
                if (j == i) continue;
                den *= t[i] - t[j];
                for (var k = deg + 1; k >= 1; k--) poly[k] = poly[k - 1] - t[j] * poly[k];
                poly[0] = -t[j] * poly[0];
                deg++;
            }
            for (var k = 0; k < 4; k++) c[k] += y[i] * poly[k] / den;
        }
        return c;
    }

    /// <summary>
    /// The bar's displacement at x in local axes (along it, local 2, local 3), metres: u from the strain N/EA, v and w from the
    /// end section's rotation and the curvature Mz/EI33, −My/EI22 integrated, plus shear's slip V/(G·As) where the bar has shear
    /// areas (Timoshenko); Gauss, exact between the loads' ends.
    /// </summary>
    public static (double U, double V, double W) Deflection(FrameElement e, ElementResult r, double x)
    {
        var ul = r.ULocal;
        double u = ul[0], v = ul[1] + ul[5] * x, w = ul[2] - ul[4] * x;
        var breaks = Breaks(e, r);
        breaks.Add(x);
        foreach (var (a, b) in Pieces(breaks, x))
        {
            var (mid, half) = ((a + b) / 2, (b - a) / 2);
            foreach (var (g, wt) in FrameElement.Gauss3)
            {
                var s = mid + half * g;
                var sec = At(e, r, s);
                var ww = wt * half;
                if (e.E * e.A > 0) u += sec.N / (e.E * e.A) * ww;
                if (e.E * e.I33 > 0) v += (x - s) * sec.Mz / (e.E * e.I33) * ww;
                if (e.E * e.I22 > 0) w += (x - s) * -sec.My / (e.E * e.I22) * ww;
                // Shear's slip (Timoshenko): γ = V / (G·As).
                if (e.G * e.As2 > 0) v += sec.Vy / (e.G * e.As2) * ww;
                if (e.G * e.As3 > 0) w += sec.Vz / (e.G * e.As3) * ww;
            }
        }
        return (u, v, w);
    }

    static IEnumerable<(double, double)> Pieces(List<double> breaks, double x)
    {
        var xs = breaks.Where(b => b <= x + 1e-12).Distinct().OrderBy(b => b).ToList();
        if (xs.Count == 0 || xs[0] > 1e-12) xs.Insert(0, 0);
        for (var i = 0; i + 1 < xs.Count; i++) if (xs[i + 1] - xs[i] > 1e-12) yield return (xs[i], xs[i + 1]);
    }

    /// <summary>The displaced point at x of the bar's axis, world metres (the displacement scaled).</summary>
    public static Vec3 Displaced(FrameElement e, ElementResult r, double x, double scale)
    {
        var (u, v, w) = Deflection(e, r, x);
        return e.Pi + e.Ex * x + (e.Ex * u + e.Ey * v + e.Ez * w) * scale;
    }
}
