using System.Globalization;
using System.Text.RegularExpressions;
using Lapin.Model;
using Lapin.Platform.ProfileLibraries;
using Lapin.Structure;

namespace Lapin.Steel.Sdnf;

/// <summary>
/// A section's name in an SDNF file, and a name read back as a section (docs/sdnf.md, Profiles). SDNF names a section by a
/// string and leaves the names to the programs (each maps them through its own tables): Lapin writes a library profile's
/// name as the library has it (IPE 300, HEA 240, L 100x100x10), and a section with no name by its kind and sizes in
/// millimetres (I300x150x7.1x10.7, RHS200x100x8, CHS114.3x5, 300x400 a rectangle, D300 a circle). Read back: the project's
/// own profiles by name, then Lapin's catalogue and built-in library, then those generic names; anything else is kept by
/// its name on a placeholder box, said in the import's report, until its profile is chosen.
/// </summary>
public static partial class SdnfProfiles
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string Mm(double m) => Math.Round(m * 1000, 2).ToString("0.##", Inv);

    /// <summary>The placeholder an unknown profile is drawn as: a 100 mm square, named after the file's section.</summary>
    public const double PlaceholderSize = 0.1;

    public static string NameOf(Section s)
    {
        if (s.Name is { Length: > 0 } n) return SdnfWriter.Ascii(n);
        return s.Kind switch
        {
            "rectangle" => $"{Mm(s.Width)}x{Mm(s.Depth)}",
            "circle" => $"D{Mm(s.Width)}",
            "polygon" => $"POLYGON{s.Sides}x{Mm(s.Width)}",
            "I" or "U" or "T" => $"{s.Kind}{Mm(s.Depth)}x{Mm(s.Width)}x{Mm(s.Web)}x{Mm(s.Flange)}",
            "L" => $"L{Mm(s.Width)}x{Mm(s.Depth)}x{Mm(s.Flange)}",
            "RHS" => $"RHS{Mm(s.Depth)}x{Mm(s.Width)}x{Mm(s.Web)}",
            "CHS" => $"CHS{Mm(s.Width)}x{Mm(s.Web)}",
            "C" or "Z" or "SIGMA" => $"{s.Kind}{Mm(s.Depth)}x{Mm(s.Width)}x{Mm(s.Web)}" + (s.Lip > 0 ? $"x{Mm(s.Lip)}" : ""),
            _ => SdnfWriter.Ascii(s.Label),
        };
    }

    [GeneratedRegex(@"^(?:RECT|R|PL|FL)?\s*(\d+(?:\.\d+)?)\s*[x*X×]\s*(\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase)] private static partial Regex Rect();
    [GeneratedRegex(@"^(?:D|Ø|ROUND|RD)\s*(\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase)] private static partial Regex Round();
    [GeneratedRegex(@"^(I|U|T)\s*(\d+(?:\.\d+)?)\s*[x*×]\s*(\d+(?:\.\d+)?)\s*[x*×]\s*(\d+(?:\.\d+)?)\s*[x*×]\s*(\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase)] private static partial Regex Rolled();
    [GeneratedRegex(@"^L\s*(\d+(?:\.\d+)?)\s*[x*×]\s*(\d+(?:\.\d+)?)\s*[x*×]\s*(\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase)] private static partial Regex Angle();
    [GeneratedRegex(@"^(?:RHS|SHS)\s*(\d+(?:\.\d+)?)\s*[x*×]\s*(\d+(?:\.\d+)?)\s*[x*×]\s*(\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase)] private static partial Regex Box();
    [GeneratedRegex(@"^(?:CHS|PIPE)\s*(\d+(?:\.\d+)?)\s*[x*×]\s*(\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase)] private static partial Regex Tube();
    [GeneratedRegex(@"^(C|Z|SIGMA)\s*(\d+(?:\.\d+)?)\s*[x*×]\s*(\d+(?:\.\d+)?)\s*[x*×]\s*(\d+(?:\.\d+)?)(?:\s*[x*×]\s*(\d+(?:\.\d+)?))?$", RegexOptions.IgnoreCase)] private static partial Regex Cold();
    [GeneratedRegex(@"^POLYGON(\d+)x(\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase)] private static partial Regex Gon();

    static string Key(string s) => new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant().Replace('×', 'X').Replace("Σ", "SIGMA");

    /// <summary>
    /// The section a name gives, and whether it was found: the project's copies of library profiles first (so a project's
    /// AISC or own profiles come back), then Lapin's catalogue and built-in library, then the generic names Lapin writes; null
    /// when none has it.
    /// </summary>
    public static Section? Resolve(IModelView m, string name)
    {
        var n = name.Trim();
        if (n.Length == 0) return null;
        var key = Key(n);
        foreach (var copy in m.OfClass(ProjectProfiles.Class))
            if (Key(copy.Name) == key && ProjectProfiles.Read(copy) is { } p && Section.From(p.SectionJson(copy.Id)) is { } own) return own;
        if (Section.Catalogue.Values.FirstOrDefault(c => Key(c.Name!) == key) is { } cat) return cat;
        if (Section.Named(n) is { } named) return named;
        double D(Group g) => double.Parse(g.Value, Inv) / 1000;
        if (Rolled().Match(n) is { Success: true } r)
            return new Section(r.Groups[1].Value.ToUpperInvariant(), D(r.Groups[3]), D(r.Groups[2]), D(r.Groups[4]), D(r.Groups[5]));
        if (Angle().Match(n) is { Success: true } a) return new Section("L", D(a.Groups[1]), D(a.Groups[2]), D(a.Groups[3]), D(a.Groups[3]));
        if (Box().Match(n) is { Success: true } b) return new Section("RHS", D(b.Groups[2]), D(b.Groups[1]), D(b.Groups[3]), D(b.Groups[3]));
        if (Tube().Match(n) is { Success: true } t) return new Section("CHS", D(t.Groups[1]), D(t.Groups[1]), D(t.Groups[2]), D(t.Groups[2]));
        if (Cold().Match(n) is { Success: true } c)
            return new Section(c.Groups[1].Value.ToUpperInvariant(), D(c.Groups[3]), D(c.Groups[2]), D(c.Groups[4]), D(c.Groups[4]), null, c.Groups[5].Success ? D(c.Groups[5]) : 0);
        if (Gon().Match(n) is { Success: true } g) return Section.Polygon(D(g.Groups[2]), int.Parse(g.Groups[1].Value, Inv));
        if (Round().Match(n) is { Success: true } d) return Section.Circle(D(d.Groups[1]));
        if (Rect().Match(n) is { Success: true } re) return Section.Rectangle(D(re.Groups[1]), D(re.Groups[2]));
        return null;
    }

    /// <summary>An unknown profile kept by its name on a placeholder box.</summary>
    public static Section Placeholder(string name) => Section.Rectangle(PlaceholderSize, PlaceholderSize) with { Name = name.Trim() };

    /// <summary>Whether a section is a placeholder for a profile no library has.</summary>
    public static bool IsPlaceholder(Section s) => s.Kind == "rectangle" && s.Name != null && s.Width == PlaceholderSize && s.Depth == PlaceholderSize;
}
