using System.Text;

namespace Lapin.Platform;

/// <summary>
/// CSV as Lapin reads and writes it, in one place (audit 15.11.2026, D7). Reading: the separator the first non-blank line
/// uses most (a semicolon, a tab or a comma; a semicolon on a tie), a cell quoted when its first character is a quote (doubled
/// quotes inside it are one), a quote anywhere else kept as it is; rows end at \n, \r\n or \r outside quotes.
/// Writing: a cell quoted when it holds a separator (any of the three), a quote, \r or \n, so a name on two lines or with a
/// comma stays one cell whichever separator a spreadsheet guesses.
/// </summary>
public static class CsvText
{
    static readonly char[] Separators = [';', '\t', ','];
    static readonly char[] Special = [';', '\t', ',', '"', '\r', '\n'];

    /// <summary>The separator <paramref name="text"/>'s first non-blank line uses most: ; a tab or a comma (a semicolon when it has none).</summary>
    public static char SeparatorOf(string text)
    {
        var first = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "";
        return Separators.OrderByDescending(c => first.Count(x => x == c)).First();
    }

    /// <summary>The text's rows and cells, split by <paramref name="separator"/> (else the one <see cref="SeparatorOf"/> finds); a blank line is a row of one empty cell.</summary>
    public static List<List<string>> Read(string text, char? separator = null)
    {
        var sep = separator ?? SeparatorOf(text);
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var opened = false;   // the cell started with a quote: what follows its closing quote is still its text
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cell.Append(ch);
            }
            else if (ch == '"' && !opened && cell.ToString().Trim().Length == 0) { cell.Clear(); quoted = opened = true; }
            else if (ch == sep) { row.Add(cell.ToString()); cell.Clear(); opened = false; }
            else if (ch is '\n' or '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear(); opened = false;
                rows.Add(row); row = [];
            }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0 || opened) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }

    /// <summary>One cell as CSV writes it: as it is, or quoted (its quotes doubled) when it holds a separator, a quote or a line break; null is empty.</summary>
    public static string Field(string? s) => s == null ? "" : s.IndexOfAny(Special) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

    /// <summary>A row's cells as one CSV line, each written by <see cref="Field"/>, joined by <paramref name="separator"/> (no line end).</summary>
    public static string Line(IEnumerable<string?> cells, char separator = ';') => string.Join(separator, cells.Select(Field));
}
