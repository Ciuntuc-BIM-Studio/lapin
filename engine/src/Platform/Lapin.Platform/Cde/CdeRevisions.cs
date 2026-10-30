using System.Text.RegularExpressions;

namespace Lapin.Platform.Cde;

/// <summary>
/// Revisions and status codes after ISO 19650-2: <c>P01, P02…</c> while the
/// information is preliminary, <c>C01, C02…</c> once contractual; the status
/// (suitability) codes <c>S0</c>–<c>S7</c>, <c>A1</c>–<c>A9</c>, <c>B1</c>–<c>B9</c>
/// and <c>CR</c>; and the ISO 19650 container a status suggests.
/// </summary>
public static class CdeRevisions
{
    public const string Wip = "WIP", Shared = "Shared", Published = "Published", Archived = "Archived";
    public static readonly string[] Containers = [Wip, Shared, Published, Archived];

    static readonly Regex Revision = new("^([PC])(\\d{2,3})$", RegexOptions.Compiled);
    static readonly Regex Status = new("^(S[0-7]|A[1-9]|B[1-9]|CR)$", RegexOptions.Compiled);

    /// <summary>The status codes with what each means.</summary>
    public static readonly (string Code, string Label)[] Statuses =
    [
        ("S0", "Initial status (work in progress)"), ("S1", "Suitable for coordination"), ("S2", "Suitable for information"), ("S3", "Suitable for review and comment"),
        ("S4", "Suitable for stage approval"), ("S5", "Withdrawn"), ("S6", "Suitable for PIM authorization"), ("S7", "Suitable for AIM acceptance"),
        ("A1", "Authorized and accepted"), ("A2", "Authorized and accepted"), ("A3", "Authorized and accepted"), ("A4", "Authorized and accepted"), ("A5", "Authorized and accepted"),
        ("A6", "Authorized and accepted"), ("A7", "Authorized and accepted"), ("A8", "Authorized and accepted"), ("A9", "Authorized and accepted"),
        ("B1", "Partial sign-off (with comments)"), ("B2", "Partial sign-off (with comments)"), ("B3", "Partial sign-off (with comments)"), ("B4", "Partial sign-off (with comments)"),
        ("B5", "Partial sign-off (with comments)"), ("B6", "Partial sign-off (with comments)"), ("B7", "Partial sign-off (with comments)"), ("B8", "Partial sign-off (with comments)"), ("B9", "Partial sign-off (with comments)"),
        ("CR", "As constructed record"),
    ];

    public static bool IsRevision(string? s) => s != null && Revision.IsMatch(s);
    public static bool IsStatus(string? s) => s != null && Status.IsMatch(s);

    /// <summary>Whether a revision is preliminary (P) rather than contractual (C).</summary>
    public static bool Preliminary(string revision) => revision.StartsWith('P');

    /// <summary>
    /// The revision after <paramref name="last"/>: P02 after P01, C02 after C01;
    /// the first contractual one is C01 whatever the preliminary count; without a
    /// last one, P01 (or C01). <paramref name="contractual"/> null: the last's kind.
    /// </summary>
    public static string Next(string? last, bool? contractual = null)
    {
        var m = last != null ? Revision.Match(last) : Match.Empty;
        var wasContractual = m.Success && m.Groups[1].Value == "C";
        var toContractual = contractual ?? wasContractual;
        if (!m.Success) return toContractual ? "C01" : "P01";
        if (toContractual && !wasContractual) return "C01";
        if (!toContractual && wasContractual) return last!;   // back from contractual to preliminary: not a step; the same revision is kept
        return $"{m.Groups[1].Value}{(int.Parse(m.Groups[2].Value) + 1).ToString().PadLeft(m.Groups[2].Value.Length, '0')}";
    }

    /// <summary>Whether <paramref name="a"/> comes before <paramref name="b"/> (P before C, then by number).</summary>
    public static int Compare(string a, string b)
    {
        var (ma, mb) = (Revision.Match(a), Revision.Match(b));
        if (!ma.Success || !mb.Success) return string.CompareOrdinal(a, b);
        var kind = (ma.Groups[1].Value == "C" ? 1 : 0) - (mb.Groups[1].Value == "C" ? 1 : 0);
        return kind != 0 ? kind : int.Parse(ma.Groups[2].Value) - int.Parse(mb.Groups[2].Value);
    }

    /// <summary>The container a status goes to by default: S0 in work in progress, the other S codes shared, A, B and CR published.</summary>
    public static string ContainerFor(string status) => status switch
    {
        "S0" => Wip,
        _ when status.StartsWith('S') => Shared,
        _ => Published,
    };
}
