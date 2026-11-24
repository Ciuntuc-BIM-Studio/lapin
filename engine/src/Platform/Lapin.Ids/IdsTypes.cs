using System.Globalization;
using System.Text.RegularExpressions;

namespace Lapin.Ids;

/// <summary>
/// The XML base types IDS restrictions are written in, which facets each takes, and the lexical form a value must have
/// (IDS implementers' documentation, "XML base types"); and the base type of an IFC defined type named as a property's dataType
/// (its "Restriction base type" column: measures are doubles, counts and numbers integers, labels and enumerations strings).
/// </summary>
public static class IdsTypes
{
    public static readonly string[] Bases = ["string", "boolean", "double", "decimal", "float", "integer", "date", "dateTime", "time", "duration"];

    static readonly Dictionary<string, Regex> LexicalForms = new()
    {
        ["boolean"] = new(@"^(true|false|0|1)$", RegexOptions.CultureInvariant),
        ["date"] = new(@"^\d{4}-\d{2}-\d{2}(Z|([+-]\d{2}:\d{2}))?$", RegexOptions.CultureInvariant),
        ["dateTime"] = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|([+-]\d{2}:\d{2}))?$", RegexOptions.CultureInvariant),
        ["double"] = new(@"^([-+]?[0-9]*\.?[0-9]*([eE][-+]?[0-9]+)?|NaN|\+INF|-INF)$", RegexOptions.CultureInvariant),
        ["duration"] = new(@"^[-+]?P(\d+Y)?(\d+M)?(\d+D)?(T(\d+H)?(\d+M)?(\d+S)?)?$", RegexOptions.CultureInvariant),
        ["integer"] = new(@"^[+-]?(\d+)$", RegexOptions.CultureInvariant),
        ["time"] = new(@"^\d{2}:\d{2}:\d{2}(\.\d+)?(Z|([+-]\d{2}:\d{2}))?$", RegexOptions.CultureInvariant),
    };

    static string Family(string b) => b is "decimal" or "float" ? "double" : b;

    /// <summary>Whether the text is a value of the base type (any text is a string).</summary>
    public static bool Lexical(string b, string text)
    {
        var f = Family(b);
        if (f == "string") return true;
        if (!LexicalForms.TryGetValue(f, out var r) || !r.IsMatch(text)) return false;
        if (f == "double") return text is "NaN" or "+INF" or "-INF" || text.Any(char.IsAsciiDigit);
        return true;
    }

    public static string Describe(string b) => Family(b) switch
    {
        "boolean" => "boolean (true or false, in lower case)",
        "double" => "number (a dot for decimals, no thousands separator)",
        "integer" => "whole number",
        "date" => "date (YYYY-MM-DD)",
        "dateTime" => "date and time (YYYY-MM-DDThh:mm:ss)",
        "time" => "time (hh:mm:ss)",
        "duration" => "duration (P1Y2M3DT4H5M6S)",
        _ => "text",
    };

    /// <summary>Which facets a base type takes: patterns every one; enumerations all but booleans; bounds the ordered ones; lengths strings.</summary>
    public static bool Allows(string b, string facet)
    {
        var f = Family(b);
        return facet switch
        {
            "pattern" => true,
            "enumeration" => f != "boolean",
            "minInclusive" or "minExclusive" or "maxInclusive" or "maxExclusive" => f is not ("string" or "boolean"),
            "length" or "minLength" or "maxLength" => f == "string",
            _ => false,
        };
    }

    /// <summary>Whether a restriction of base <paramref name="restriction"/> fits a dataType whose base is <paramref name="typeBase"/>.</summary>
    public static bool Compatible(string typeBase, string restriction) => Family(typeBase) == Family(restriction);

    static readonly HashSet<string> Integers =
    [
        "IFCINTEGER", "IFCPOSITIVEINTEGER", "IFCCOUNTMEASURE", "IFCDIMENSIONCOUNT", "IFCDAYINMONTHNUMBER", "IFCDAYINWEEKNUMBER", "IFCMONTHINYEARNUMBER",
        "IFCHOURINDAY", "IFCMINUTEINHOUR", "IFCCARDINALPOINTREFERENCE", "IFCTIMESTAMP", "IFCDAYLIGHTSAVINGHOUR", "IFCYEARNUMBER",
    ];
    static readonly HashSet<string> Strings =
    [
        "IFCLABEL", "IFCTEXT", "IFCIDENTIFIER", "IFCGLOBALLYUNIQUEID", "IFCURIREFERENCE", "IFCDESCRIPTIVEMEASURE", "IFCBOXALIGNMENT", "IFCFONTSTYLE",
        "IFCFONTVARIANT", "IFCFONTWEIGHT", "IFCPRESENTABLETEXT", "IFCTEXTALIGNMENT", "IFCTEXTDECORATION", "IFCTEXTFONTNAME", "IFCTEXTTRANSFORMATION",
        "IFCWELLKNOWNTEXTLITERAL", "IFCLANGUAGEID", "IFCSPECULAREXPONENT", "IFCSPECULARROUGHNESS",
    ];

    /// <summary>The XML base type of an IFC defined type (upper case); strings for what the table does not name a number of.</summary>
    public static string BaseOf(string dataType) => dataType switch
    {
        "IFCBOOLEAN" or "IFCLOGICAL" => "boolean",
        "IFCDATE" => "date",
        "IFCDATETIME" => "dateTime",
        "IFCTIME" => "time",
        "IFCDURATION" => "duration",
        "IFCREAL" or "IFCNUMERICMEASURE" or "IFCPARAMETERVALUE" or "IFCRATIOMEASURE" or "IFCPOSITIVERATIOMEASURE" or "IFCNORMALISEDRATIOMEASURE" => "double",
        _ when Integers.Contains(dataType) => "integer",
        _ when Strings.Contains(dataType) || dataType.EndsWith("ENUM", StringComparison.Ordinal) => "string",
        _ when dataType.EndsWith("MEASURE", StringComparison.Ordinal) => "double",
        _ => "string",
    };

    /// <summary>A number as XSD writes one (a dot, an exponent, INF, NaN).</summary>
    public static bool TryNumber(string text, out double v)
    {
        switch (text)
        {
            case "INF": case "+INF": v = double.PositiveInfinity; return true;
            case "-INF": v = double.NegativeInfinity; return true;
            case "NaN": v = double.NaN; return true;
        }
        if (!LexicalForms["double"].IsMatch(text) || !text.Any(char.IsAsciiDigit)) { v = 0; return false; }
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }

    public static bool TryInteger(string text, out long v)
    {
        v = 0;
        return LexicalForms["integer"].IsMatch(text) && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v);
    }
}
