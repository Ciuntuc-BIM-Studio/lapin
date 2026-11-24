using System.Globalization;
using System.Xml;

namespace Lapin.Ids;

/// <summary>
/// Whether an IDS value accepts what the file holds. A simple value is read as the IFC value's own kind (type casting): a text
/// equals as written (case and all); a boolean only from true, false, 1 or 0; a whole number as a whole number (42, never 42.0);
/// any other number within IDS's tolerance, 1e-6 relative plus 1e-6 absolute. A restriction: one of its enumeration (cast the
/// same way), one of its patterns (texts only), its bounds (numbers, compared exactly, no tolerance) and its lengths (texts
/// only), each group it has. An object, a list, nothing, an unknown logical meet no value.
/// </summary>
public static class IdsMatch
{
    public const double Tolerance = 1e-6;

    public static bool Equal(double x, double v) => x > v - Math.Abs(v) * Tolerance - Tolerance && x < v + Math.Abs(v) * Tolerance + Tolerance;

    static readonly HashSet<string> IntegerTypes = ["IFCINTEGER", "IFCPOSITIVEINTEGER", "IFCCOUNTMEASURE", "IFCDIMENSIONCOUNT", "IFCDAYINMONTHNUMBER", "IFCDAYINWEEKNUMBER", "IFCMONTHINYEARNUMBER", "IFCTIMESTAMP", "IFCCARDINALPOINTREFERENCE"];

    /// <summary>Whether the value is a whole number as IFC typed it (a long of an integer type, or of no type).</summary>
    static bool IsInteger(IfcVal x) => x.Raw is long && (x.Type == null || IntegerTypes.Contains(x.Type));

    /// <summary>A text value (an enumeration's name too) accepted as text alone: class names, set and property names, systems, materials.</summary>
    public static bool AcceptsText(IdsValue v, string? s) => s != null && Accepts(v, new IfcVal(s));

    public static bool Accepts(IdsValue v, IfcVal x)
    {
        if (x.IsEmpty) return false;
        if (x.Raw is Lapin.Ifc.StepRef or List<object?>) return false;
        if (v.Simple != null) return Simple(v.Simple, x);
        var r = v.Restriction!;
        if (r.Enumeration.Count > 0 && !r.Enumeration.Any(e => Simple(e, x))) return false;
        if (r.Patterns.Count > 0)
        {
            if (x.Raw is not string s) return false;
            if (!r.Compiled.Any(p => XsdPattern.Matches(p, s))) return false;
        }
        if (r.MinInclusive != null || r.MinExclusive != null || r.MaxInclusive != null || r.MaxExclusive != null)
        {
            if (!Bounds(r, x)) return false;
        }
        if (r.Length != null || r.MinLength != null || r.MaxLength != null)
        {
            if (x.Raw is not string s) return false;
            var n = s.EnumerateRunes().Count();
            if (r.Length is { } len && n != len) return false;
            if (r.MinLength is { } min && n < min) return false;
            if (r.MaxLength is { } max && n > max) return false;
        }
        return true;
    }

    static bool Simple(string text, IfcVal x)
    {
        switch (x.Raw)
        {
            case string s: return string.Equals(s, text, StringComparison.Ordinal);
            case bool b: return text is "true" or "1" ? b : text is "false" or "0" && !b;
            case long l when IsInteger(x) && x.Si == null:
                return IdsTypes.TryInteger(text, out var i) && i == l;
            case double or long:
            {
                var n = x.Si ?? (x.Raw is double d ? d : (long)x.Raw);
                return IdsTypes.TryNumber(text, out var v) && (double.IsNaN(v) ? double.IsNaN(n) : double.IsInfinity(v) ? n == v : Equal(n, v));
            }
            default: return false;
        }
    }

    static bool Bounds(IdsRestriction r, IfcVal x)
    {
        // Dates, times and durations are texts in IFC: compared as what they are when the restriction is of that base.
        if (x.Raw is string s)
        {
            if (r.Base is "date" or "dateTime" or "time") return OrderedText(r, s, Instant);
            if (r.Base == "duration") return OrderedText(r, s, Duration);
            return false;
        }
        if (x.Raw is not (double or long)) return false;
        var n = x.Si ?? (x.Raw is double d ? d : (long)x.Raw);
        static bool Num(string? t, out double v) { v = 0; return t != null && IdsTypes.TryNumber(t, out v); }
        if (Num(r.MinInclusive, out var a) && !(n >= a)) return false;
        if (Num(r.MinExclusive, out var b) && !(n > b)) return false;
        if (Num(r.MaxInclusive, out var c) && !(n <= c)) return false;
        if (Num(r.MaxExclusive, out var e) && !(n < e)) return false;
        return true;
    }

    static double? Instant(string s)
    {
        string[] formats = ["yyyy-MM-dd", "yyyy-MM-ddK", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssK", "yyyy-MM-ddTHH:mm:ss.FFFFFFF", "yyyy-MM-ddTHH:mm:ss.FFFFFFFK", "HH:mm:ss", "HH:mm:ssK", "HH:mm:ss.FFFFFFF"];
        return DateTimeOffset.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t.UtcTicks : null;
    }

    static double? Duration(string s)
    {
        try { return XmlConvert.ToTimeSpan(s).Ticks; } catch (FormatException) { return null; } catch (OverflowException) { return null; }
    }

    static bool OrderedText(IdsRestriction r, string s, Func<string, double?> read)
    {
        if (read(s) is not { } n) return false;
        bool Cmp(string? t, Func<double, double, bool> ok) => t == null || (read(t) is { } v && ok(n, v));
        return Cmp(r.MinInclusive, (a, b) => a >= b) && Cmp(r.MinExclusive, (a, b) => a > b) && Cmp(r.MaxInclusive, (a, b) => a <= b) && Cmp(r.MaxExclusive, (a, b) => a < b);
    }
}
