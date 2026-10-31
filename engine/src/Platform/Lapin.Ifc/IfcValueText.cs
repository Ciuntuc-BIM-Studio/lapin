using System.Globalization;
using System.Text.RegularExpressions;

namespace Lapin.Ifc;

/// <summary>
/// A number another program wrote as text, read as the number it is (an import's rule, <see cref="IfcImportOptions.Numbers"/>):
/// Navisworks' typed text ("DoubleLength:0,01", "DoubleArea:3,549", "DoubleVolume:3E-05", "DoubleAngle:3,14159",
/// "Int32:4"), a number with its unit ("1.15395 kg", "250 mm", "3.5 m²") and a plain number with a decimal point or a
/// sign ("+106.059", "-12"). A whole number alone stays text ("800293", "001": a code, a mark), and a decimal comma is
/// read as one only after a type prefix, where nothing else can be meant ("1,234" alone may be a thousand). Lengths, areas and volumes in metres; angles in radians, as the prefixes give them.
/// </summary>
public static partial class IfcValueText
{
    /// <summary>The value as IFC types it (a STEP literal) and as the inspector shows it; null when the text is not a number.</summary>
    public static (string Step, string Display)? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        if (Prefixed().Match(t) is { Success: true } p)
        {
            var number = p.Groups["n"].Value.Trim();
            if (!number.Contains('.')) number = number.Replace(',', '.');
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return null;
            var type = p.Groups["kind"].Value switch
            {
                "Length" => "IFCLENGTHMEASURE", "Area" => "IFCAREAMEASURE", "Volume" => "IFCVOLUMEMEASURE", "Angle" => "IFCPLANEANGLEMEASURE",
                _ => p.Groups["base"].Value.StartsWith("Int", StringComparison.Ordinal) ? "IFCINTEGER" : "IFCREAL",
            };
            return type == "IFCINTEGER" && Math.Abs(v - Math.Round(v)) < 1e-9 ? (Typed(type, ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture)), Show(v)) : (Typed(type, Real(v)), Show(v));
        }
        if (WithUnit().Match(t) is { Success: true } u && double.TryParse(u.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
        {
            var (type, factor) = u.Groups["u"].Value switch
            {
                "kg" => ("IFCMASSMEASURE", 1.0), "g" => ("IFCMASSMEASURE", 0.001), "t" => ("IFCMASSMEASURE", 1000.0),
                "m" => ("IFCLENGTHMEASURE", 1.0), "mm" => ("IFCLENGTHMEASURE", 0.001), "cm" => ("IFCLENGTHMEASURE", 0.01),
                "m2" or "m²" => ("IFCAREAMEASURE", 1.0), "m3" or "m³" => ("IFCVOLUMEMEASURE", 1.0),
                _ => ("", 0.0),
            };
            if (type.Length == 0) return null;
            return (Typed(type, Real(x * factor)), t);
        }
        if (Plain().IsMatch(t) && double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var plain))
            return (Typed("IFCREAL", Real(plain)), Show(plain));
        return null;
    }

    static string Typed(string type, string value) => $"{type}({value})";

    static string Real(double v)
    {
        var s = v.ToString("R", CultureInfo.InvariantCulture);
        if (s.Contains('.')) return s;
        var e = s.IndexOfAny(['E', 'e']);
        return e < 0 ? s + "." : s[..e] + "." + s[e..];
    }

    static string Show(double v) => Math.Round(v, 6).ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(?<base>Double|Single|Float|Int16|Int32|Int64|Int|Integer|Number)(?<kind>Length|Area|Volume|Angle)?:(?<n>\s*[-+]?[0-9]+(?:[.,][0-9]+)?(?:[eE][-+]?[0-9]+)?\s*)$")]
    private static partial Regex Prefixed();

    [GeneratedRegex(@"^(?<n>[-+]?[0-9]+(?:\.[0-9]+)?(?:[eE][-+]?[0-9]+)?)\s*(?<u>kg|g|t|mm|cm|m|m2|m²|m3|m³)$")]
    private static partial Regex WithUnit();

    [GeneratedRegex(@"^(?:[-+][0-9]+(?:\.[0-9]+)?|[0-9]+\.[0-9]+)(?:[eE][-+]?[0-9]+)?$")]
    private static partial Regex Plain();
}
