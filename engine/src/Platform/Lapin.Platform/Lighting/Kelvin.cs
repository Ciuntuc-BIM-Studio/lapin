namespace Lapin.Platform.Lighting;

/// <summary>
/// A colour temperature as a colour (docs/plan-render.md, S2): the Planckian locus by Krystek's rational approximation in
/// CIE 1960 (u, v) (1985; within 10⁻⁴ of Planck's law from 1000 K to 15000 K), to CIE 1931 (x, y), to linear sRGB at the
/// brightest (the largest channel 1). The interface has the same formula (ui/src/viewport/lights3d.ts).
/// </summary>
public static class Kelvin
{
    public const double Min = 1000, Max = 12000;

    /// <summary>The locus's chromaticity (CIE 1931 x, y) at a temperature (held to 1000–12000 K).</summary>
    public static (double X, double Y) Chromaticity(double kelvin)
    {
        var t = Math.Clamp(double.IsFinite(kelvin) ? kelvin : 6500, Min, Max);
        var u = (0.860117757 + 1.54118254e-4 * t + 1.28641212e-7 * t * t) / (1 + 8.42420235e-4 * t + 7.08145163e-7 * t * t);
        var v = (0.317398726 + 4.22806245e-5 * t + 4.20481691e-8 * t * t) / (1 - 2.89741816e-5 * t + 1.61456053e-7 * t * t);
        var d = 2 * u - 8 * v + 4;
        return (3 * u / d, 2 * v / d);
    }

    /// <summary>The colour in linear sRGB, its largest channel 1 (the others clamped at 0).</summary>
    public static (double R, double G, double B) Rgb(double kelvin)
    {
        var (x, y) = Chromaticity(kelvin);
        double X = x / y, Z = (1 - x - y) / y;
        var r = 3.2404542 * X - 1.5371385 - 0.4985314 * Z;
        var g = -0.9692660 * X + 1.8760108 + 0.0415560 * Z;
        var b = 0.0556434 * X - 0.2040259 + 1.0572252 * Z;
        r = Math.Max(0, r); g = Math.Max(0, g); b = Math.Max(0, b);
        var top = Math.Max(r, Math.Max(g, b));
        return top > 0 ? (r / top, g / top, b / top) : (1, 1, 1);
    }
}
