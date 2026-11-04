using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>A day the calendar says otherwise of: a holiday of the project (not working) or a Saturday worked; From to To inclusive.</summary>
public sealed record CalendarException(DateOnly From, DateOnly To, bool Working, string Name);

/// <summary>
/// A work calendar (IfcWorkCalendar, docs/plan-4d5d.md): the weekdays worked (ISO: 1 Monday … 7 Sunday), the working
/// hours of a day, Romania's legal holidays when asked (computed each year, Orthodox Easter and Pentecost included), the
/// project's own exceptions, and a winter break that only the tasks marked wet (concrete, plaster) keep — off by default.
/// Every date the scheduler works with is the start of a day; a task's finish is the day after its last working day
/// (exclusive), so <see cref="Offset"/> is the one primitive the critical path needs.
/// </summary>
public sealed record WorkCalendar(
    IReadOnlyList<int> Days, string DayStart, double Hours, string Holidays, IReadOnlyList<CalendarException> Exceptions,
    bool Winter = false, string WinterFrom = "12-15", string WinterTo = "03-01")
{
    public const string Class = "IfcWorkCalendar";
    public const string Romania = "ro", NoHolidays = "none";

    /// <summary>Five days of eight hours from 08:00, Romania's legal holidays; the winter break there but off (the user's answer 6).</summary>
    public static WorkCalendar Default => new([1, 2, 3, 4, 5], "08:00", 8, Romania, []);

    // ------------------------------------------------------------------ holidays

    /// <summary>
    /// Orthodox Easter Sunday of a year (Gregorian date): Meeus's Julian computus, then the 13 days between the Julian and
    /// Gregorian calendars (1900–2099; 14 from 2100).
    /// </summary>
    public static DateOnly OrthodoxEaster(int year)
    {
        int a = year % 4, b = year % 7, c = year % 19;
        var d = (19 * c + 15) % 30;
        var e = (2 * a + 4 * b - d + 34) % 7;
        var month = (d + e + 114) / 31;
        var day = (d + e + 114) % 31 + 1;
        var julian = new DateOnly(year, month, day);
        var shift = year / 100 - year / 400 - 2;   // 13 for 1900–2099
        return julian.AddDays(shift);
    }

    /// <summary>Romania's legal holidays of a year (Codul muncii, art. 139, as of 2024: Epiphany and St John added).</summary>
    public static IReadOnlyList<(DateOnly Date, string Name)> RomanianHolidays(int year)
    {
        var easter = OrthodoxEaster(year);
        var list = new List<(DateOnly, string)>
        {
            (new(year, 1, 1), "Anul Nou"), (new(year, 1, 2), "Anul Nou"),
            (new(year, 1, 6), "Boboteaza"), (new(year, 1, 7), "Sfântul Ioan Botezătorul"),
            (new(year, 1, 24), "Ziua Unirii Principatelor Române"),
            (easter.AddDays(-2), "Vinerea Mare"), (easter, "Paștele"), (easter.AddDays(1), "Paștele"),
            (new(year, 5, 1), "Ziua Muncii"), (new(year, 6, 1), "Ziua Copilului"),
            (easter.AddDays(49), "Rusaliile"), (easter.AddDays(50), "Rusaliile"),
            (new(year, 8, 15), "Adormirea Maicii Domnului"), (new(year, 11, 30), "Sfântul Andrei"),
            (new(year, 12, 1), "Ziua Națională a României"), (new(year, 12, 25), "Crăciunul"), (new(year, 12, 26), "Crăciunul"),
        };
        // Two on one day (Whit Monday on Children's Day, 1 June 2026) are one day off, named after both.
        return list.GroupBy(h => h.Item1).OrderBy(g => g.Key).Select(g => (g.Key, string.Join(" / ", g.Select(h => h.Item2).Distinct()))).ToList();
    }

    static readonly System.Collections.Concurrent.ConcurrentDictionary<int, HashSet<int>> HolidayDays = new();

    static HashSet<int> RomanianSet(int year) => HolidayDays.GetOrAdd(year, y => RomanianHolidays(y).Select(h => h.Date.DayNumber).ToHashSet());

    /// <summary>The holiday a day is (its name), or null.</summary>
    public string? HolidayOf(DateOnly day) =>
        Holidays == Romania && RomanianSet(day.Year).Contains(day.DayNumber) ? RomanianHolidays(day.Year).First(h => h.Date == day).Name : null;

    // ------------------------------------------------------------------ working days

    /// <summary>Whether a weekday (ISO) is worked; the list is read each time (a record copied by <c>with</c> keeps no stale cache).</summary>
    bool WeekdayWorks(int iso)
    {
        for (var i = 0; i < Days.Count; i++) if (Days[i] == iso) return true;
        return false;
    }

    static bool[] MakeWeekdays(IReadOnlyList<int> days)
    {
        var w = new bool[8];
        foreach (var d in days) if (d is >= 1 and <= 7) w[d] = true;
        return w;
    }

    static int Iso(DateOnly d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

    /// <summary>Whether the winter break covers a day (it runs across the new year when its start comes after its end).</summary>
    public bool InWinter(DateOnly day)
    {
        if (!Winter || !TryMonthDay(WinterFrom, out var from) || !TryMonthDay(WinterTo, out var to)) return false;
        var md = day.Month * 100 + day.Day;
        return from <= to ? md >= from && md < to : md >= from || md < to;
    }

    static bool TryMonthDay(string s, out int md)
    {
        md = 0;
        var parts = s.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var m) || !int.TryParse(parts[1], out var d) || m is < 1 or > 12 || d is < 1 or > 31) return false;
        md = m * 100 + d;
        return true;
    }

    /// <summary>Whether a day is worked: an exception says so first, then a holiday, then the weekday; a wet task does not work in the winter break.</summary>
    public bool Working(DateOnly day, bool wet = false)
    {
        if (wet && InWinter(day)) return false;
        foreach (var x in Exceptions) if (day >= x.From && day <= x.To) return x.Working;
        if (Holidays == Romania && RomanianSet(day.Year).Contains(day.DayNumber)) return false;
        return WeekdayWorks(Iso(day));
    }

    const int Guard = 40_000;   // over a century of days: a calendar that never works

    void Check()
    {
        if (!Days.Any(d => d is >= 1 and <= 7) && Exceptions.All(x => !x.Working)) throw new CommandException("The calendar has no working day.");
    }

    /// <summary>The first working day on or after <paramref name="day"/>.</summary>
    public DateOnly NextWorking(DateOnly day, bool wet = false)
    {
        for (var i = 0; i < Guard; i++, day = day.AddDays(1)) if (Working(day, wet)) return day;
        Check();
        throw new CommandException("The calendar has no working day for a century: check its winter break.");
    }

    /// <summary>The last working day on or before <paramref name="day"/>.</summary>
    public DateOnly PrevWorking(DateOnly day, bool wet = false)
    {
        for (var i = 0; i < Guard; i++, day = day.AddDays(-1)) if (Working(day, wet)) return day;
        Check();
        throw new CommandException("The calendar has no working day for a century: check its winter break.");
    }

    /// <summary>
    /// <paramref name="n"/> working days from <paramref name="at"/> (the start of a day): forward, the day after the n-th
    /// working day counted from <paramref name="at"/> on; backward (n &lt; 0), the start of the |n|-th working day counted back
    /// from the day before <paramref name="at"/>; n = 0, <paramref name="at"/> itself. A task starting on a working day S
    /// with a duration d ends (exclusive) at Offset(S, d); one ending at F started at Offset(F, −d).
    /// </summary>
    public DateOnly Offset(DateOnly at, int n, bool wet = false)
    {
        if (n == 0) return at;
        var day = at;
        if (n > 0)
        {
            for (int left = n, i = 0; i < Guard * 4; i++, day = day.AddDays(1))
                if (Working(day, wet) && --left == 0) return day.AddDays(1);
        }
        else
        {
            day = at.AddDays(-1);
            for (int left = -n, i = 0; i < Guard * 4; i++, day = day.AddDays(-1))
                if (Working(day, wet) && --left == 0) return day;
        }
        Check();
        throw new CommandException("The calendar has no working day for a century: check its winter break.");
    }

    /// <summary>The working days in [from, to) (negative when to comes first).</summary>
    public int WorkingDays(DateOnly from, DateOnly to, bool wet = false)
    {
        if (to < from) return -WorkingDays(to, from, wet);
        var n = 0;
        for (var d = from; d < to; d = d.AddDays(1)) if (Working(d, wet)) n++;
        return n;
    }

    /// <summary>The latest exclusive end of a working day on or before <paramref name="end"/>: the day after the last working day before it.</summary>
    public DateOnly PrevEnd(DateOnly end, bool wet = false) => PrevWorking(end.AddDays(-1), wet).AddDays(1);

    /// <summary>The days not worked between two dates, with why (a holiday, an exception, the winter break), for the Gantt's shading and the calendar's list.</summary>
    public IEnumerable<(DateOnly Day, string Why)> NonWorking(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var x = Exceptions.FirstOrDefault(e => d >= e.From && d <= e.To);
            if (x != null) { if (!x.Working) yield return (d, x.Name.Length > 0 ? x.Name : "Not worked"); continue; }
            if (HolidayOf(d) is { } h) yield return (d, h);
        }
    }

    // ------------------------------------------------------------------ attributes

    public static string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (s.Length >= 10 && DateOnly.TryParseExact(s[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        foreach (var f in new[] { "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy" })
            if (DateOnly.TryParseExact(s, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
        return null;
    }

    public JsonObject ToJson() => new()
    {
        ["days"] = new JsonArray(Days.Select(d => (JsonNode)d).ToArray()),
        ["dayStart"] = DayStart,
        ["hours"] = Hours,
        ["holidays"] = Holidays,
        ["exceptions"] = new JsonArray(Exceptions.Select(x => (JsonNode)new JsonObject
        {
            ["from"] = Date(x.From), ["to"] = Date(x.To), ["working"] = x.Working, ["name"] = x.Name,
        }).ToArray()),
        ["winter"] = new JsonObject { ["on"] = Winter, ["from"] = WinterFrom, ["to"] = WinterTo },
    };

    /// <summary>A calendar from its attributes, what is missing or malformed taken from the default.</summary>
    public static WorkCalendar Read(JsonObject? a)
    {
        var d = Default;
        if (a == null) return d;
        var days = a["days"] is JsonArray da ? da.Select(x => (int)(Json.Number(x) ?? 0)).Where(x => x is >= 1 and <= 7).Distinct().Order().ToList() : d.Days.ToList();
        var start = (string?)a["dayStart"] is { } s && TimeOnly.TryParseExact(s, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? s : d.DayStart;
        var hours = Json.Number(a["hours"]) is { } h && h > 0 && h <= 24 ? h : d.Hours;
        var holidays = (string?)a["holidays"] is NoHolidays ? NoHolidays : Romania;
        var exceptions = new List<CalendarException>();
        if (a["exceptions"] is JsonArray xa)
            foreach (var x in xa.OfType<JsonObject>())
                if (ParseDate((string?)x["from"]) is { } from)
                {
                    var to = ParseDate((string?)x["to"]) ?? from;
                    if (to < from) (from, to) = (to, from);
                    exceptions.Add(new(from, to, x["working"] is JsonValue wv && wv.TryGetValue<bool>(out var w) && w, (string?)x["name"] ?? ""));
                }
        var winter = a["winter"] as JsonObject;
        var on = winter?["on"] is JsonValue ov && ov.TryGetValue<bool>(out var o) && o;
        string Md(string key, string fallback) => (string?)winter?[key] is { } v && TryMonthDay(v, out _) ? v : fallback;
        return new WorkCalendar(days, start, hours, holidays, exceptions, on, Md("from", d.WinterFrom), Md("to", d.WinterTo));
    }

    /// <summary>
    /// A calendar read from another program (MS Project, IFC), which lists every holiday as an exception: when the
    /// exceptions hold every Romanian legal holiday on a working weekday of the years they span, the calendar takes
    /// Romania's holidays as a rule again and keeps only the other exceptions, so a schedule moved to a later year keeps them.
    /// </summary>
    public static WorkCalendar FromExplicit(IReadOnlyList<int> days, string dayStart, double hours, IReadOnlyList<CalendarException> exceptions)
    {
        var plain = new WorkCalendar(days, dayStart, hours, NoHolidays, exceptions);
        var off = exceptions.Where(x => !x.Working).SelectMany(x => Enumerable.Range(0, x.To.DayNumber - x.From.DayNumber + 1).Select(i => x.From.AddDays(i))).ToHashSet();
        if (off.Count == 0) return plain;
        var years = off.Select(d => d.Year).Distinct().ToList();
        var weekdays = MakeWeekdays(days);
        var ro = years.SelectMany(y => RomanianHolidays(y)).Where(h => weekdays[Iso(h.Date)]).Select(h => h.Date).ToList();
        if (ro.Count == 0 || !ro.All(off.Contains)) return plain;
        var roSet = years.SelectMany(y => RomanianHolidays(y)).Select(h => h.Date).ToHashSet();
        var rest = exceptions.Where(x => x.Working || x.From != x.To || !roSet.Contains(x.From)).ToList();
        return new WorkCalendar(days, dayStart, hours, Romania, rest);
    }
}
