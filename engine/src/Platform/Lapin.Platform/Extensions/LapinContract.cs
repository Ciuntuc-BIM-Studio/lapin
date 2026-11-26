using System.Text.RegularExpressions;

namespace Lapin.Platform;

/// <summary>
/// The version of the platform's public surface an extension module is built against (docs/plan-extension-modules.md §1.3).
/// <see cref="Minor"/> goes up when the platform gains something an extension may use; <see cref="Major"/> when a public type an
/// extension may use changes or goes away. An extension's module.json says <c>"contract": "1.x"</c> (or <c>"1.2"</c>): the
/// loader refuses a Major it does not have and a Minor newer than its own.
/// </summary>
public static partial class LapinContract
{
    public const int Major = 1;
    public const int Minor = 0;

    /// <summary>"1.0": what <c>extensions.modules</c> tells the interface.</summary>
    public static string Version => $"{Major}.{Minor}";

    [GeneratedRegex(@"^\s*(\d{1,4})(?:\.(\d{1,4}|x|\*))?\s*$")]
    private static partial Regex ContractPattern();

    /// <summary>Why an extension asking for <paramref name="contract"/> cannot load on this engine; null when it can.</summary>
    public static string? Problem(string? contract, int major = Major, int minor = Minor)
    {
        if (string.IsNullOrWhiteSpace(contract)) return "Its module.json names no contract version (\"contract\": \"1.x\").";
        var m = ContractPattern().Match(contract);
        if (!m.Success) return $"Its contract version „{contract}” is not one Lapin reads (\"1.x\" or \"1.2\").";
        var wantMajor = int.Parse(m.Groups[1].Value);
        var wantMinor = m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out var n) ? n : 0;
        if (wantMajor != major) return $"It is built for Lapin's contract {wantMajor}.x; this Lapin has {major}.{minor}.";
        if (wantMinor > minor) return $"It needs Lapin's contract {wantMajor}.{wantMinor}; this Lapin has {major}.{minor}: update Lapin.";
        return null;
    }

    [GeneratedRegex(@"^\s*(\^|>=|=)?\s*(\d{1,6})\s*$")]
    private static partial Regex ApiPattern();

    /// <summary>
    /// Whether a module's <c>api</c> number meets a requirement: <c>"^N"</c> or <c>"N"</c> (that api exactly: a break raises
    /// it), <c>"&gt;=N"</c>, or <c>"*"</c>/empty (any). Null when it does; else why not.
    /// </summary>
    public static string? ApiProblem(string module, string? range, int? api)
    {
        if (string.IsNullOrWhiteSpace(range) || range.Trim() == "*") return null;
        var m = ApiPattern().Match(range);
        if (!m.Success) return $"Its requirement „{module}: {range}” is not one Lapin reads (\"^1\", \">=1\" or \"*\").";
        var want = int.Parse(m.Groups[2].Value);
        var have = api ?? 1;
        var ok = m.Groups[1].Value == ">=" ? have >= want : have == want;
        return ok ? null : $"It needs „{module}” api {range.Trim()}; this Lapin's „{module}” has api {have}.";
    }
}
