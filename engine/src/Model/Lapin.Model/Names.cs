using System.Globalization;
using System.Text.RegularExpressions;

namespace Lapin.Model;

/// <summary>
/// Free names, one convention for every list (audit 15.11.2026, D12): the name asked when no other has it, else „Name 2”,
/// „Name 3”…; a name that already ends in a digit takes its number in brackets, „Brick 25 (2)”, „C30 (2)”, so the number
/// never reads as part of it („Brick 25 2”). A copy of a numbered copy goes on from its number („Brick 25 (2)” → „Brick 25 (3)”).
/// </summary>
public static partial class Names
{
    /// <summary>The first free name for <paramref name="name"/> by the convention above; <paramref name="taken"/> says whether one is taken.</summary>
    public static string Free(Func<string, bool> taken, string name)
    {
        if (!taken(name)) return name;
        var (stem, next) = (name, 2);
        if (Numbered().Match(name) is { Success: true } m && taken(m.Groups[1].Value))
            (stem, next) = (m.Groups[1].Value, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) + 1);
        for (var n = next; ; n++)
            if (Numbered(stem, n) is var candidate && !taken(candidate)) return candidate;
    }

    /// <summary><see cref="Free(Func{string, bool}, string)"/> among <paramref name="taken"/>, compared without case.</summary>
    public static string Free(IEnumerable<string?> taken, string name)
    {
        var set = taken as HashSet<string> ?? taken.OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Free(set.Contains, name);
    }

    /// <summary><paramref name="name"/> with the number <paramref name="n"/> after it, as <see cref="Free(Func{string, bool}, string)"/> writes it.</summary>
    public static string Numbered(string name, int n) =>
        name.Length > 0 && char.IsAsciiDigit(name[^1]) ? $"{name} ({n})" : $"{name} {n}";

    [GeneratedRegex(@"^(.*\S) \((\d{1,6})\)$")]
    private static partial Regex Numbered();
}
