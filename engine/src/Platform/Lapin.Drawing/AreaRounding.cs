using System.Globalization;

namespace Lapin.Drawing;

/// <summary>
/// How the project shows the areas of its spaces (report #76, decision 471): as measured (to the hundredth), or rounded to
/// the nearest 0.1, 0.5 or 1 m² (ft² in an imperial project), half up, as the Nordic countries write room areas (SFS 5139:
/// x,0 or x,5 m²). Presentation only: the model, quantities, IFC and every computation keep the exact area. Each space is
/// rounded on its own and a total is the sum of what its rows show, so a schedule adds up as it reads.
/// </summary>
public static class AreaRounding
{
    /// <summary>The steps a project may choose (none: <c>null</c>).</summary>
    public static readonly double[] Steps = [0.1, 0.5, 1];

    const double SquareFeet = 10.763910417;

    static bool Imperial(string units) => units is "ft-in" or "in";

    /// <summary>A step from JSON when it is one of <see cref="Steps"/>, else null.</summary>
    public static double? Read(double? v) => v is { } d ? Steps.Cast<double?>().FirstOrDefault(s => Math.Abs(s!.Value - d) < 1e-9) : null;

    /// <summary>A value rounded to the nearest <paramref name="step"/>, half up (12.25 at 0.5 is 12.5; 12.5 at 1 is 13).</summary>
    public static double ToStep(double v, double step)
    {
        // The area to a ten-thousandth of a square metre first (the noise of a corner's coordinates, never what is shown), then
        // the quotient to nine places, so 12.25 / 0.1 (122.4999…) counts as the half it is.
        var n = Math.Round(Math.Round(v, 4) / step, 9);
        return Math.Round(Math.Floor(n + 0.5) * step, 9);
    }

    /// <summary>
    /// An area (m²) as the project shows it, still in m²: rounded in the unit it is written in (m², or ft² in an imperial
    /// project); the area itself without a step.
    /// </summary>
    public static double Shown(double m2, double? step, string units = "mm")
    {
        if (step is not > 0 || !double.IsFinite(m2)) return m2;
        return Imperial(units) ? ToStep(m2 * SquareFeet, step.Value) / SquareFeet : ToStep(m2, step.Value);
    }

    /// <summary>The sum of the areas as each is shown: what a total under rounded rows reads.</summary>
    public static double Sum(IEnumerable<double> m2, double? step, string units = "mm") => m2.Sum(a => Shown(a, step, units));

    /// <summary>The decimals an area is written with: none at a whole step, one at 0.5 or 0.1 (12.0, 12.5), else <paramref name="fallback"/>.</summary>
    public static int Decimals(double? step, int fallback) => step switch { >= 1 => 0, > 0 => 1, _ => fallback };

    /// <summary>
    /// An area (m²) as a stamp, a legend or a schedule writes it, with its unit: m² to the hundredth (ft² to the tenth in an
    /// imperial project), or to the project's step.
    /// </summary>
    public static string Text(double m2, double? step, string units, bool withUnit = true)
    {
        var imperial = Imperial(units);
        var v = Shown(m2, step, units) * (imperial ? SquareFeet : 1);
        var text = Math.Round(v, 6).ToString("F" + Decimals(step, imperial ? 1 : 2), CultureInfo.InvariantCulture);
        return withUnit ? text + (imperial ? " ft²" : " m²") : text;
    }
}
