namespace Lapin.Alignments;

/// <summary>
/// The transition functions of IFC 4.3, normalised on ξ = s / L ∈ [0, 1]:
/// each rises from 0 to 1 and is used two ways — as the curvature of a
/// horizontal transition (κ = κ₁ + f(ξ) Δκ) and as the cant of a cant
/// transition (D = D₁ + f(ξ) ΔD). The integrals F(ξ) = ∫₀^ξ f give the
/// tangent angle in closed form, so only x, y need quadrature.
///
///   LINEAR   (clothoid, linear cant)  f = ξ
///   HELMERT  (Schramm)                f = 2ξ² | 1 − 2(1 − ξ)²   (halves)
///   BLOSS                              f = 3ξ² − 2ξ³
///   COSINE                             f = ½ (1 − cos πξ)
///   SINE     (Klein)                   f = ξ − sin(2πξ) / 2π
///   VIENNESE (Viennese Bend®)          f = ξ⁴ (35 − 84ξ + 70ξ² − 20ξ³)
///
/// The Viennese Bend's curvature has one more term, −420 h Δψ / L² · g(ξ)
/// with g = ξ² (1 − 4ξ + 5ξ² − 2ξ³), the second derivative of its cant angle
/// ψ lifted to the gravity centre line height h. ∫₀¹ g = 0, so it bends the
/// curve without changing its total turning.
/// </summary>
public enum TransitionShape { Linear, Helmert, Bloss, Cosine, Sine, Viennese }

public static class Transitions
{
    /// <summary>f(ξ).</summary>
    public static double Shape(TransitionShape kind, double xi) => kind switch
    {
        TransitionShape.Linear => xi,
        TransitionShape.Helmert => xi <= 0.5 ? 2 * xi * xi : 1 - 2 * (1 - xi) * (1 - xi),
        TransitionShape.Bloss => xi * xi * (3 - 2 * xi),
        TransitionShape.Cosine => 0.5 * (1 - Math.Cos(Math.PI * xi)),
        TransitionShape.Sine => xi - Math.Sin(2 * Math.PI * xi) / (2 * Math.PI),
        _ => Pow(xi, 4) * (35 - 84 * xi + 70 * xi * xi - 20 * Pow(xi, 3)),
    };

    /// <summary>f′(ξ) = df/dξ.</summary>
    public static double Slope(TransitionShape kind, double xi) => kind switch
    {
        TransitionShape.Linear => 1,
        TransitionShape.Helmert => xi <= 0.5 ? 4 * xi : 4 * (1 - xi),
        TransitionShape.Bloss => 6 * xi * (1 - xi),
        TransitionShape.Cosine => 0.5 * Math.PI * Math.Sin(Math.PI * xi),
        TransitionShape.Sine => 1 - Math.Cos(2 * Math.PI * xi),
        _ => 140 * Pow(xi, 3) * (1 - 3 * xi + 3 * xi * xi - Pow(xi, 3)),
    };

    /// <summary>F(ξ) = ∫₀^ξ f.</summary>
    public static double Integral(TransitionShape kind, double xi) => kind switch
    {
        TransitionShape.Linear => xi * xi / 2,
        TransitionShape.Helmert => xi <= 0.5 ? (2.0 / 3) * Pow(xi, 3) : xi + (2.0 / 3) * Pow(1 - xi, 3) - 0.5,
        TransitionShape.Bloss => Pow(xi, 3) - Pow(xi, 4) / 2,
        TransitionShape.Cosine => 0.5 * (xi - Math.Sin(Math.PI * xi) / Math.PI),
        TransitionShape.Sine => xi * xi / 2 + (Math.Cos(2 * Math.PI * xi) - 1) / (4 * Math.PI * Math.PI),
        _ => Pow(xi, 5) * (7 - 14 * xi + 10 * xi * xi - 2.5 * Pow(xi, 3)),
    };

    /// <summary>The Viennese Bend's cant-angle term g(ξ) = ξ²(1 − 4ξ + 5ξ² − 2ξ³).</summary>
    public static double ViennesePsiTerm(double xi) => xi * xi * (1 - 4 * xi + 5 * xi * xi - 2 * Pow(xi, 3));

    /// <summary>∫₀^ξ g.</summary>
    public static double ViennesePsiTermIntegral(double xi) => Pow(xi, 3) * (1.0 / 3 - xi + xi * xi - Pow(xi, 3) / 3);

    /// <summary>The shape a horizontal transition type follows; null for a line or an arc.</summary>
    public static TransitionShape? Of(HorizontalType t) => t switch
    {
        HorizontalType.Clothoid => TransitionShape.Linear,
        HorizontalType.HelmertCurve => TransitionShape.Helmert,
        HorizontalType.BlossCurve => TransitionShape.Bloss,
        HorizontalType.CosineCurve => TransitionShape.Cosine,
        HorizontalType.SineCurve => TransitionShape.Sine,
        HorizontalType.VienneseBend => TransitionShape.Viennese,
        _ => null,
    };

    /// <summary>The shape a cant transition type follows; null for a constant cant.</summary>
    public static TransitionShape? Of(CantType t) => t switch
    {
        CantType.LinearTransition => TransitionShape.Linear,
        CantType.HelmertCurve => TransitionShape.Helmert,
        CantType.BlossCurve => TransitionShape.Bloss,
        CantType.CosineCurve => TransitionShape.Cosine,
        CantType.SineCurve => TransitionShape.Sine,
        CantType.VienneseBend => TransitionShape.Viennese,
        _ => null,
    };

    /// <summary>Largest x² for which <see cref="FresnelE"/> is used.</summary>
    public const double FresnelMaxX2 = 8;

    /// <summary>
    /// E(x) = ∫₀ˣ exp(i t²) dt as (re, im), by its power series
    /// Σ iⁿ x²ⁿ⁺¹ / (n! (2n + 1)). Exact to ~1e-15 relative while x² stays
    /// below ~8 (the largest term is then e^8 / √(16π) ≈ 400 times the sum);
    /// callers fall back to quadrature beyond.
    /// </summary>
    public static (double Re, double Im) FresnelE(double x)
    {
        var x2 = x * x;
        var term = x; // xⁿ… / n!, without the 1 / (2n + 1)
        double re = 0, im = 0;
        for (var n = 0; n < 200; n++)
        {
            var t = term / (2 * n + 1);
            switch (n & 3)
            {
                case 0: re += t; break;
                case 1: im += t; break;
                case 2: re -= t; break;
                default: im -= t; break;
            }
            if (Math.Abs(t) < 1e-18 * (Math.Abs(re) + Math.Abs(im) + 1e-300) && n > 2) break;
            term *= x2 / (n + 1);
        }
        return (re, im);
    }

    static double Pow(double x, int n) => n switch { 3 => x * x * x, 4 => x * x * x * x, 5 => x * x * x * x * x, _ => Math.Pow(x, n) };
}
