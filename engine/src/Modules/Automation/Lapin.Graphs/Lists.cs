using System.Globalization;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Graphs;

/// <summary>
/// The rules of the list nodes (docs/plan-visual-graphs.md §4, decision 527): a list typed by hand, sorting (numbers by
/// size, texts in natural order), and values once each.
/// </summary>
public static class Lists
{
    /// <summary>Numbers a billionth apart or less are the same value (as Compare's „=”).</summary>
    public const double Same = 1e-9;

    /// <summary>
    /// A list typed by hand: its items between semicolons or on lines of their own, trimmed, empty ones left out. Kind
    /// „Numbers”: every item a number (a decimal comma reads as a point); „Texts”: every item a text; „Auto”: numbers when
    /// every item reads as one, else texts.
    /// </summary>
    public static List<object?> Typed(string text, string kind)
    {
        var items = text.Split([';', '\n', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (items.Length > GraphLimits.MaxItems) throw new CommandException($"A list holds at most {GraphLimits.MaxItems} items ({items.Length} asked).");
        var numbers = items.Select(Number).ToList();
        switch (kind)
        {
            case "Texts":
                return [.. items];
            case "Numbers":
                var bad = numbers.FindIndex(n => n == null);
                if (bad >= 0) throw new CommandException($"„{items[bad]}” is not a number.");
                return [.. numbers.Select(n => (object?)n!.Value)];
            default:
                return numbers.All(n => n != null) ? [.. numbers.Select(n => (object?)n!.Value)] : [.. items];
        }
    }

    /// <summary>A typed item as a number: a decimal comma or point, finite; else null.</summary>
    public static double? Number(string s) =>
        double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? d : null;

    /// <summary>
    /// <paramref name="list"/> sorted by its own items, or by <paramref name="by"/>'s place for place (as many as the list).
    /// The keys compare as numbers when every one is a number (or a text that reads as one), else as texts in natural order;
    /// equal keys keep their order, a key of nothing goes last either way. The sorted list and where each item was.
    /// </summary>
    public static (List<object?> Sorted, List<object?> Order) Sort(List<object?> list, List<object?>? by, bool descending)
    {
        var keys = by ?? list;
        if (by != null && by.Count != list.Count)
            throw new CommandException($"By has {by.Count} values for {list.Count} items: one each.");
        foreach (var k in keys)
            if (k is Vec3 or Polyline or List<object?> or System.Text.Json.Nodes.JsonNode)
                throw new CommandException($"{Values.Describe(k)} cannot be sorted: sort by numbers or texts (By).");
        var asNumbers = keys.All(k => k == null || ReadNumber(k) != null);
        var numbers = asNumbers ? keys.Select(k => k == null ? (double?)null : ReadNumber(k)).ToArray() : null;
        var texts = asNumbers ? null : keys.Select(k => k == null ? null : Values.Text(k)).ToArray();
        var sign = descending ? -1 : 1;
        int Compare(int i, int j)
        {
            bool ni = keys[i] == null, nj = keys[j] == null;
            if (ni || nj) return ni == nj ? 0 : ni ? 1 : -1;
            return sign * (numbers != null ? numbers[i]!.Value.CompareTo(numbers[j]!.Value) : Natural(texts![i]!, texts[j]!));
        }
        // A stable sort: equal keys by their place.
        var order = Enumerable.Range(0, list.Count).ToArray();
        Array.Sort(order, (i, j) => Compare(i, j) is var c && c != 0 ? c : i.CompareTo(j));
        return ([.. order.Select(i => list[i])], [.. order.Select(i => (object?)(double)i)]);
    }

    static double? ReadNumber(object? v) => v switch
    {
        double d => d,
        int i => i,
        bool b => b ? 1 : 0,
        string s => Number(s.Trim()),
        _ => null,
    };

    /// <summary>Texts in natural order: ignoring case, the runs of digits compared as numbers („W2” before „W10”).</summary>
    public static int Natural(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsAsciiDigit(a[i])) i++;
                while (j < b.Length && char.IsAsciiDigit(b[j])) j++;
                var da = a[si..i].TrimStart('0');
                var db = b[sj..j].TrimStart('0');
                if (da.Length != db.Length) return da.Length.CompareTo(db.Length);
                var c = string.CompareOrdinal(da, db);
                if (c != 0) return c;
                continue;
            }
            int ei = i, ej = j;
            while (ei < a.Length && !char.IsAsciiDigit(a[ei])) ei++;
            while (ej < b.Length && !char.IsAsciiDigit(b[ej])) ej++;
            var t = string.Compare(a[i..ei], b[j..ej], CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
            if (t != 0) return t;
            i = ei;
            j = ej;
        }
        var rest = (a.Length - i).CompareTo(b.Length - j);
        return rest != 0 ? rest : string.CompareOrdinal(a, b);
    }

    /// <summary>
    /// Each value once, in the order first seen, and how many times it came. Numbers (and points, coordinate by coordinate)
    /// a billionth apart or less are the same; texts as written (case counts); a number and a text never the same.
    /// </summary>
    public static (List<object?> Values, List<object?> Times) Unique(List<object?> list)
    {
        var values = new List<object?>();
        var times = new List<int>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var v in list)
        {
            var found = -1;
            foreach (var key in Keys(v))
                if (seen.TryGetValue(key, out found)) break;
                else found = -1;
            if (found >= 0) { times[found]++; continue; }
            seen[Keys(v).First()] = values.Count;
            values.Add(v);
            times.Add(1);
        }
        return (values, [.. times.Select(t => (object?)(double)t)]);
    }

    /// <summary>
    /// The keys a value is found by: its own first (what it is stored under), then its neighbours' for numbers and points,
    /// so that two values either side of a rounding step still meet.
    /// </summary>
    static IEnumerable<string> Keys(object? v)
    {
        static double Step(double x) => Math.Round(x / Same);
        static string N(double s) => s.ToString("R", CultureInfo.InvariantCulture);
        switch (v)
        {
            case double d:
                var s = Step(d);
                yield return "n" + N(s);
                yield return "n" + N(s - 1);
                yield return "n" + N(s + 1);
                break;
            case Vec3 p:
                double x = Step(p.X), y = Step(p.Y), z = Step(p.Z);
                yield return $"p{N(x)},{N(y)},{N(z)}";
                for (var dx = -1; dx <= 1; dx++)
                    for (var dy = -1; dy <= 1; dy++)
                        for (var dz = -1; dz <= 1; dz++)
                            if (dx != 0 || dy != 0 || dz != 0) yield return $"p{N(x + dx)},{N(y + dy)},{N(z + dz)}";
                break;
            case null: yield return "0"; break;
            case string t: yield return "t" + t; break;
            case bool b: yield return b ? "b1" : "b0"; break;
            default: yield return "j" + (Values.ToArg(v)?.ToJsonString() ?? ""); break;
        }
    }
}
