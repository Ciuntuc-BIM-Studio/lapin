namespace Lapin.Model;

/// <summary>
/// A file name made from a name people typed (a document, a table, a schedule, a library, an export), one rule for every
/// such file (audit 22.11.2026: nine copies with different rules, six of which let <c>"*?&lt;&gt;|</c> through on a Mac, so a
/// file saved there onto a shared drive did not open on Windows): what Windows refuses — <c>&lt;&gt;:"/\|?*</c> and control
/// characters — becomes <paramref name="replacement"/>, trailing dots and spaces go, a reserved device name (CON, NUL, COM1…)
/// takes a trailing underscore, the stem is at most 120 characters and never empty.
/// </summary>
public static class FileNames
{
    static readonly HashSet<char> Refused = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Whether a character is one Windows refuses in a file name.</summary>
    public static bool IsRefused(char c) => Refused.Contains(c) || char.IsControl(c);

    /// <summary>The name's stem, safe on every file system (no extension added); <paramref name="fallback"/> when nothing is left.</summary>
    public static string Stem(string? name, string fallback, string replacement = "_")
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in (name ?? "").Trim()) { if (IsRefused(c)) sb.Append(replacement); else sb.Append(c); }
        var stem = sb.ToString().Trim().TrimEnd('.', ' ');
        if (stem.Length > 120) stem = stem[..120].TrimEnd().TrimEnd('.', ' ');
        if (stem.Length == 0) return fallback;
        var dot = stem.IndexOf('.');
        if (Reserved.Contains(dot < 0 ? stem : stem[..dot])) stem += "_";
        return stem;
    }

    /// <summary>The name with <paramref name="ext"/> (no dot) after its safe stem.</summary>
    public static string Of(string? name, string ext, string fallback, string replacement = "_") => Stem(name, fallback, replacement) + "." + ext;
}
