using System.Globalization;
using System.Text;

namespace Lapin.Drawing.Import;

/// <summary>
/// AutoCAD's line type files (<c>.lin</c>, its public format): a header <c>*NAME, description</c>, then a line <c>A, 12.7, -6.35, 0, -6.35</c>
/// — a dash (positive), a gap (negative), a dot (0) — in the file's unit (acadiso.lin: mm; acad.lin: inches). Complex types' text
/// and shape elements (<c>[ "GAS", STANDARD, … ]</c>) are not drawn by Lapin: the dashes around them are kept, the element
/// read as a gap of its length when it gives one, and the type marked. Written back the same way.
/// </summary>
public static class LinFile
{
    /// <summary>A line type read: its name, description, dashes on paper (mm: dash, gap, dash…; a dot 0.1 mm), and whether it had text or shapes left out.</summary>
    public sealed record Read(string Name, string? Description, double[] Dashes, bool Complex);

    public const double Dot = 0.1;

    /// <summary>The line types of a file, <paramref name="mmPerUnit"/> mm a file unit (1 for acadiso.lin, 25.4 for acad.lin); what cannot be read is listed in <paramref name="skipped"/>.</summary>
    public static List<Read> Parse(string text, double mmPerUnit, List<string>? skipped = null)
    {
        var o = new List<Read>();
        string? name = null, description = null;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(";;", StringComparison.Ordinal)) continue;
            if (line.StartsWith('*'))
            {
                var comma = line.IndexOf(',');
                name = (comma > 0 ? line[1..comma] : line[1..]).Trim();
                description = comma > 0 ? line[(comma + 1)..].Trim() : null;
                if (description is { Length: 0 }) description = null;
                continue;
            }
            if (name == null || !line.StartsWith("A", StringComparison.OrdinalIgnoreCase)) continue;
            var complex = false;
            var values = new List<double>();
            // The elements after "A,": numbers, or bracketed text/shape elements.
            var body = line[1..].TrimStart().TrimStart(',');
            var parts = new List<string>();
            var depth = 0;
            var cur = new StringBuilder();
            foreach (var ch in body)
            {
                if (ch == '[') depth++;
                if (ch == ']') depth--;
                if (ch == ',' && depth == 0) { parts.Add(cur.ToString().Trim()); cur.Clear(); continue; }
                cur.Append(ch);
            }
            if (cur.Length > 0) parts.Add(cur.ToString().Trim());
            foreach (var part in parts)
            {
                if (part.StartsWith('['))
                {
                    complex = true;
                    continue;
                }
                if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) values.Add(v * mmPerUnit);
            }
            var dashes = Dashes(values);
            if (dashes == null) skipped?.Add(name);
            else o.Add(new Read(name, description, dashes, complex));
            name = null;
        }
        return o;
    }

    /// <summary>
    /// A .lin pattern (dash +, gap −, dot 0) as Lapin's dashes: dash, gap, dash, gap…, starting with a dash, alike neighbours joined,
    /// a dot a short dash; null when it has no dash or no gap (a continuous line is Lapin's own).
    /// </summary>
    public static double[]? Dashes(IReadOnlyList<double> values)
    {
        var runs = new List<(bool Dash, double Length)>();
        foreach (var v in values)
        {
            var (dash, length) = v >= 0 ? (true, Math.Max(v, Dot)) : (false, -v);
            if (length <= 0) continue;
            if (runs.Count > 0 && runs[^1].Dash == dash) runs[^1] = (dash, runs[^1].Length + length);
            else runs.Add((dash, length));
        }
        if (runs.Count < 2 || runs.All(r => r.Dash) || runs.All(r => !r.Dash)) return null;
        // Start with a dash: a leading gap goes to the end (joined with a trailing one).
        if (!runs[0].Dash) { var g = runs[0]; runs.RemoveAt(0); if (!runs[^1].Dash) runs[^1] = (false, runs[^1].Length + g.Length); else runs.Add(g); }
        // End with a gap: a trailing dash joins the leading one.
        if (runs[^1].Dash) { runs[0] = (true, runs[0].Length + runs[^1].Length); runs.RemoveAt(runs.Count - 1); }
        if (runs.Count < 2) return null;
        return [.. runs.Select(r => Math.Round(Math.Min(r.Length, 99.9), 4))];
    }

    /// <summary>Line types as a .lin file in mm (acadiso's unit): each its header and its pattern.</summary>
    public static string Write(IEnumerable<(string Name, string? Description, double[] Dashes)> types)
    {
        var sb = new StringBuilder();
        sb.AppendLine(";; Line types written by Lapin (mm)");
        foreach (var (name, description, dashes) in types)
        {
            sb.Append('*').Append(name).Append(',').AppendLine(description ?? name);
            sb.Append("A,").AppendLine(string.Join(",", dashes.Select((d, i) => (i % 2 == 0 ? d : -d).ToString("0.####", CultureInfo.InvariantCulture))));
        }
        return sb.ToString();
    }
}
