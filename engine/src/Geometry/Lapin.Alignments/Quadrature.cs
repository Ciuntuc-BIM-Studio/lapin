using Lapin.Geometry;

namespace Lapin.Alignments;

/// <summary>
/// Adaptive Gauss–Legendre quadrature for the plane curves whose tangent
/// angle θ(s) is known in closed form: x = ∫cos θ, y = ∫sin θ. A panel is
/// accepted when one 12-point rule agrees with the two rules on its halves;
/// θ is smooth (polynomial or trigonometric) on every segment, so a 100 m
/// transition converges to ~1e-13 m in a handful of panels.
/// </summary>
public static class Quadrature
{
    const int Order = 12;
    static readonly double[] Nodes, Weights;

    static Quadrature()
    {
        // Nodes and weights on [-1, 1], from Newton on the Legendre polynomial.
        Nodes = new double[Order]; Weights = new double[Order];
        for (var i = 1; i <= Order; i++)
        {
            var t = Math.Cos(Math.PI * (i - 0.25) / (Order + 0.5));
            double dp = 0;
            for (var it = 0; it < 100; it++)
            {
                double p0 = 1, p1 = t;
                for (var k = 2; k <= Order; k++) { var p2 = ((2 * k - 1) * t * p1 - (k - 1) * p0) / k; p0 = p1; p1 = p2; }
                dp = Order * (t * p1 - p0) / (t * t - 1);
                var dt = p1 / dp;
                t -= dt;
                if (Math.Abs(dt) < 1e-16) break;
            }
            Nodes[i - 1] = t;
            Weights[i - 1] = 2 / ((1 - t * t) * dp * dp);
        }
    }

    /// <summary>∫ₐᵇ (cos θ, sin θ) with one fixed rule.</summary>
    static Vec2 Panel(Func<double, double> theta, double a, double b)
    {
        double h = (b - a) / 2, m = (a + b) / 2;
        double cx = 0, cy = 0;
        for (var i = 0; i < Order; i++)
        {
            var t = theta(m + h * Nodes[i]);
            cx += Weights[i] * Math.Cos(t);
            cy += Weights[i] * Math.Sin(t);
        }
        return new(cx * h, cy * h);
    }

    static Vec2 Adaptive(Func<double, double> theta, double a, double b, Vec2 whole, double tol, int depth)
    {
        var m = (a + b) / 2;
        Vec2 left = Panel(theta, a, m), right = Panel(theta, m, b);
        var sum = left + right;
        if (depth >= 24 || (sum - whole).Length <= tol) return sum;
        return Adaptive(theta, a, m, left, tol / 2, depth + 1) + Adaptive(theta, m, b, right, tol / 2, depth + 1);
    }

    /// <summary>
    /// The displacement ∫ₐᵇ (cos θ(s), sin θ(s)) ds. <paramref name="tol"/> is absolute, metres.
    /// The interval is split first into panels no longer than <paramref name="maxPanel"/> and no
    /// wider than ~0.5 rad of turning (<paramref name="turnRate"/> bounds |dθ/ds|), so the
    /// adaptive step starts from a well-resolved guess.
    /// </summary>
    public static Vec2 IntegrateHeading(Func<double, double> theta, double a, double b, double tol = 1e-12, double maxPanel = 50, double turnRate = 0)
    {
        if (b == a) return new(0, 0);
        var len = Math.Abs(b - a);
        var n = Math.Max(1, Math.Max((int)Math.Ceiling(len / maxPanel), (int)Math.Ceiling(len * turnRate / 0.5)));
        double x = 0, y = 0;
        for (var i = 0; i < n; i++)
        {
            double p = a + (b - a) * i / n, q = a + (b - a) * (i + 1) / n;
            var d = Adaptive(theta, p, q, Panel(theta, p, q), tol / n, 0);
            x += d.X; y += d.Y;
        }
        return new(x, y);
    }

    /// <summary>Plain adaptive quadrature of a scalar function (used for the vertical clothoid).</summary>
    public static double Integrate(Func<double, double> f, double a, double b, double tol = 1e-12)
    {
        double Rule(double p, double q)
        {
            double h = (q - p) / 2, m = (p + q) / 2, sum = 0;
            for (var i = 0; i < Order; i++) sum += Weights[i] * f(m + h * Nodes[i]);
            return sum * h;
        }
        double Rec(double p, double q, double whole, double t, int depth)
        {
            var m = (p + q) / 2;
            double l = Rule(p, m), r = Rule(m, q);
            if (depth >= 24 || Math.Abs(l + r - whole) <= t) return l + r;
            return Rec(p, m, l, t / 2, depth + 1) + Rec(m, q, r, t / 2, depth + 1);
        }
        return Rec(a, b, Rule(a, b), tol, 0);
    }
}
