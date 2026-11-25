using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lapin.Platform.Cde.References;

/// <summary>
/// Dates as the reference documents keep them (ISO 8601, a day: <c>2026-11-25</c>), read from what people type and what
/// documents write: 25.11.2026, 25/11/2026, 25-11-2026, 2026-11-25, "25 noiembrie 2026", "25 November 2026", "November 25, 2026".
/// </summary>
public static class ReferenceDates
{
    static readonly string[] Months =
    [
        "ianuarie|january|jan|ian", "februarie|february|feb", "martie|march|mar", "aprilie|april|apr", "mai|may", "iunie|june|jun|iun",
        "iulie|july|jul|iul", "august|aug", "septembrie|september|sep|sept", "octombrie|october|oct", "noiembrie|november|nov", "decembrie|december|dec",
    ];

    static readonly Dictionary<string, int> MonthOf = Months.SelectMany((names, i) => names.Split('|').Select(n => (n, i + 1)))
        .ToDictionary(x => x.n, x => x.Item2, StringComparer.OrdinalIgnoreCase);

    static readonly string MonthNames = string.Join("|", Months.SelectMany(m => m.Split('|')).OrderByDescending(n => n.Length));

    /// <summary>A date as one of the forms above, in text (no anchors).</summary>
    public static readonly string Pattern =
        $@"(?:\d{{4}}-\d{{1,2}}-\d{{1,2}}|\d{{1,2}}\s*[./-]\s*\d{{1,2}}\s*[./-]\s*\d{{4}}|\d{{1,2}}\s+(?:{MonthNames})\.?\s+\d{{4}}|(?:{MonthNames})\.?\s+\d{{1,2}},?\s+\d{{4}})";

    static readonly Regex Iso = new(@"^(\d{4})-(\d{1,2})-(\d{1,2})$", RegexOptions.CultureInvariant);
    static readonly Regex Dotted = new(@"^(\d{1,2})\s*[./-]\s*(\d{1,2})\s*[./-]\s*(\d{4})$", RegexOptions.CultureInvariant);
    static readonly Regex DayMonth = new($@"^(\d{{1,2}})\s+({MonthNames})\.?\s+(\d{{4}})$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    static readonly Regex MonthDay = new($@"^({MonthNames})\.?\s+(\d{{1,2}}),?\s+(\d{{4}})$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>The date as ISO (<c>yyyy-MM-dd</c>); null when it is not one of the forms, or not a day of the calendar.</summary>
    public static string? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        int y, m, d;
        if (Iso.Match(t) is { Success: true } a) (y, m, d) = (Int(a, 1), Int(a, 2), Int(a, 3));
        else if (Dotted.Match(t) is { Success: true } b) (y, m, d) = (Int(b, 3), Int(b, 2), Int(b, 1));
        else if (DayMonth.Match(t) is { Success: true } c) (y, m, d) = (Int(c, 3), MonthOf[c.Groups[2].Value], Int(c, 1));
        else if (MonthDay.Match(t) is { Success: true } e) (y, m, d) = (Int(e, 3), MonthOf[e.Groups[1].Value], Int(e, 2));
        else if (DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var any) && t.Contains('T'))
            return any.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        else return null;
        if (y < 1900 || y > 2200 || m < 1 || m > 12 || d < 1 || d > DateTime.DaysInMonth(y, m)) return null;
        return $"{y:D4}-{m:D2}-{d:D2}";
    }

    static int Int(Match m, int g) => int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);

    public static DateOnly? Day(string? iso) => iso != null && DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public static string Iso8601(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// Suggestions for a reference document's metadata from its text (docs/plan-cde-references.md §2: text is read, never guessed):
/// its number and date ("Nr. 123 din 15.03.2025", "Nr. 123/15.03.2025", "No. 45 of 2 May 2025"), its validity ("valabil până la
/// …", "valabil 12 luni", "valid until …", "valid for 24 months"), its issuer ("Emitent: …", "Issued by: …", a heading that names
/// an authority) and its type (from the title it carries: "CERTIFICAT DE URBANISM"). Conservative: only what a pattern finds, the
/// first such, each with the words it was read from for the user to confirm. A scan without a text layer gets none.
/// </summary>
public static class ReferenceSuggest
{
    const RegexOptions O = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
    static readonly string D = ReferenceDates.Pattern;

    // "Nr. 123 din 15.03.2025", "nr. 123/15.03.2025", "Număr: 45", "No. 45 of 2 May 2025", "Number: A-12/2025".
    static readonly Regex NumberRx = new($@"\b(?:nr|număr|numar|no|number)\b\.?\s*[:.]?\s*(?<num>[A-Z0-9][A-Z0-9\-_.]*(?:/(?!\s*{D})[A-Z0-9\-_.]+)*)(?:\s*/\s*(?<date>{D})|\s*,?\s+(?:din|from|of|dated|issued on)\s+(?<date>{D}))?", O);

    // "Data: 15.03.2025", "Data emiterii: …", "Date: …", "Date of issue: …", "Issued on …", "eliberat la data de …".
    static readonly Regex DateRx = new($@"\b(?:data(?:\s+emiterii|\s+eliber[aă]rii)?|date(?:\s+of\s+issue)?|issued\s+on|eliberat[aă]?\s+(?:la\s+data\s+de|în\s+data\s+de|la))\s*[:.]?\s*(?<date>{D})", O);

    // "valabil până la 15.03.2027", "valabilitate: până la data de …", "valid until …", "expires on …", "expiry date: …".
    static readonly Regex UntilRx = new($@"\b(?:valabil[aă]?|valabilitate(?:a)?|valid|expires?|expiry\s+date|expiration\s+date)\b[^\n.]{{0,40}}?(?:p[aâ]n[aă]\s+(?:la\s+)?(?:data\s+de\s+)?|until\s+|on\s+|:\s*)(?<date>{D})", O);

    // "valabil 12 luni", "valabilitate 24 de luni", "valabil pe o perioadă de 12 luni", "valid for 24 months".
    static readonly Regex MonthsRx = new(@"\b(?:valabil[aă]?|valabilitate(?:a)?|valid)\b[^\n.]{0,40}?\b(?<n>\d{1,3})\s+(?:de\s+)?(?:luni|months?)\b", O);

    static readonly Regex IssuerRx = new(@"\b(?:emitent|emis\s+de|eliberat\s+de|issuer|issued\s+by)\s*[:.]?\s*(?<who>[^\n,;]{2,80})", O);

    // A heading that names an authority, as Romanian documents start: "PRIMĂRIA MUNICIPIULUI IAȘI".
    static readonly Regex AuthorityRx = new(@"^\s*(?<who>(?:PRIM[ĂA]RIA|CONSILIUL\s+JUDE[ȚŢT]EAN|CONSILIUL\s+LOCAL|INSPECTORATUL|DIREC[ȚŢT]IA|AGEN[ȚŢT]IA|OFICIUL|MINISTERUL|COMPANIA|SOCIETATEA)\b[^\n]{0,80})$", RegexOptions.CultureInvariant | RegexOptions.Multiline);

    static readonly (string Type, Regex Title)[] Titles =
    [
        ("cu", new(@"\bcertificat(?:ul)?\s+de\s+urbanism\b|\burbanism\s+certificate\b|\bzoning\s+certificate\b", O)),
        ("autorizatie", new(@"\bautoriza[țţt]i[ea]\s+de\s+(?:construire|desfiin[țţt]are)\b|\bbuilding\s+permit\b|\bdemolition\s+permit\b", O)),
        ("cf", new(@"\bextras\s+de\s+carte\s+funciar[aă]\b|\bland[\s-]?book\s+extract\b", O)),
        ("studiu-geo", new(@"\bstudiu(?:l)?\s+geotehnic\b|\bgeotechnical\s+(?:study|report)\b", O)),
        ("topo", new(@"\bridicare\s+topografic[aă]\b|\btopographic(?:al)?\s+survey\b", O)),
        ("expertiza", new(@"\bexpertiz[aă]\s+tehnic[aă]\b|\baudit\s+energetic\b|\bexpert\s+report\b", O)),
        ("aviz", new(@"^\s*aviz\b|\baviz(?:ul)?\s+(?:de\s+)?(?:principiu|favorabil|tehnic|de\s+amplasament|sanitar|de\s+securitate)", O | RegexOptions.Multiline)),
        ("acord", new(@"^\s*acord\b|\bacord(?:ul)?\s+(?:de\s+)?(?:mediu|unic|vecin)", O | RegexOptions.Multiline)),
    ];

    /// <summary>The suggestions, each with the text it was read from (<c>evidence</c>); an empty object when the text has none.</summary>
    public static JsonObject From(string text, JsonArray? types = null)
    {
        var o = new JsonObject();
        var evidence = new JsonObject();
        if (string.IsNullOrWhiteSpace(text)) { o["evidence"] = evidence; return o; }
        var t = text.Replace(' ', ' ');

        // The first "nr." followed by something with a digit in it ("Nr. crt." in a table's head is not a number).
        if (NumberRx.Matches(t).FirstOrDefault(x => x.Groups["num"].Value.Any(char.IsAsciiDigit)) is { } n && n.Groups["num"].Value is var num)
        {
            o["number"] = num.TrimEnd('.', '-');
            evidence["number"] = Snippet(t, n);
            if (n.Groups["date"].Success && ReferenceDates.Parse(Squeeze(n.Groups["date"].Value)) is { } nd) { o["date"] = nd; evidence["date"] = Snippet(t, n); }
        }
        if (o["date"] == null && DateRx.Match(t) is { Success: true } dm && ReferenceDates.Parse(Squeeze(dm.Groups["date"].Value)) is { } dd)
        {
            o["date"] = dd;
            evidence["date"] = Snippet(t, dm);
        }
        if (UntilRx.Match(t) is { Success: true } um && ReferenceDates.Parse(Squeeze(um.Groups["date"].Value)) is { } until)
        {
            o["validUntil"] = until;
            evidence["validUntil"] = Snippet(t, um);
        }
        else if (MonthsRx.Match(t) is { Success: true } mm && int.TryParse(mm.Groups["n"].Value, CultureInfo.InvariantCulture, out var months) && months is > 0 and <= 240)
        {
            o["validityMonths"] = months;
            evidence["validityMonths"] = Snippet(t, mm);
            if (ReferenceDates.Day((string?)o["date"]) is { } from) o["validUntil"] = ReferenceDates.Iso8601(from.AddMonths(months));
        }
        if (IssuerRx.Match(t) is { Success: true } im)
        {
            o["issuer"] = im.Groups["who"].Value.Trim().TrimEnd('.');
            evidence["issuer"] = Snippet(t, im);
        }
        else if (AuthorityRx.Match(t) is { Success: true } am)
        {
            o["issuer"] = Collapse(am.Groups["who"].Value).TrimEnd('.');
            evidence["issuer"] = Snippet(t, am);
        }
        foreach (var (type, rx) in Titles)
            if (rx.Match(t) is { Success: true } tm)
            {
                if (types == null || types.OfType<JsonObject>().Any(x => (string?)x["id"] == type)) { o["type"] = type; evidence["type"] = Snippet(t, tm); }
                break;
            }
        o["evidence"] = evidence;
        return o;
    }

    /// <summary>The type a title names ("Certificat de urbanism nr. 12.pdf": <c>cu</c>); null when none of the patterns finds one.</summary>
    public static string? TypeOf(string text)
    {
        var t = text.Replace('_', ' ');
        foreach (var (type, rx) in Titles) if (rx.IsMatch(t)) return type;
        return null;
    }

    /// <summary>A date's text without the spaces a PDF puts between its parts ("15 . 03 . 2025").</summary>
    static string Squeeze(string s) => Regex.Replace(s.Trim(), @"\s*([./-])\s*", "$1");

    static string Collapse(string s) => Regex.Replace(s.Trim(), @"\s+", " ");

    /// <summary>The words a suggestion was read from, on one line, at most 160 characters.</summary>
    static string Snippet(string t, Match m)
    {
        var s = Collapse(t.Substring(m.Index, m.Length));
        return s.Length > 160 ? s[..160] + "…" : s;
    }
}
