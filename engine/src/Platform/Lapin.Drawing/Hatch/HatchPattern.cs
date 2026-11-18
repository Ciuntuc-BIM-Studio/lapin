using System.Globalization;
using System.Text;
using Lapin.Geometry;

namespace Lapin.Drawing.Hatch;

/// <summary>One family of parallel lines: its angle (°), origin, the shift along (DeltaX) and the spacing across (DeltaY) between lines, and its dashes (positive drawn, negative gap, 0 a dot; none continuous).</summary>
public sealed record HatchLineFamily(double AngleDeg, Vec2 Origin, double DeltaX, double DeltaY, IReadOnlyList<double> Dashes)
{
    public bool Equals(HatchLineFamily? o) => o is not null && AngleDeg == o.AngleDeg && Origin == o.Origin && DeltaX == o.DeltaX && DeltaY == o.DeltaY && Dashes.SequenceEqual(o.Dashes);
    public override int GetHashCode() => HashCode.Combine(AngleDeg, Origin, DeltaX, DeltaY, Dashes.Count);
}

/// <summary>
/// What a pattern's numbers measure (decision 671): <see cref="Paper"/>, millimetres as printed, the same on paper at any
/// scale (a cut's 45° lines); <see cref="Model"/>, millimetres of the real object, so the pattern keeps its real size and
/// gets finer on paper as the scale gets smaller (a brick's courses, a floor's tiles). <c>.pat</c>: <c>;%TYPE=MODEL</c>.
/// </summary>
public enum HatchUnits { Paper, Model }

/// <summary>A hatch pattern: its families of lines, or a solid fill when it has none; its numbers in <see cref="Units"/>.</summary>
public sealed record HatchPattern(string Name, string Description, IReadOnlyList<HatchLineFamily> Families, HatchUnits Units = HatchUnits.Paper)
{
    /// <summary>No line families: a solid fill, unless it is the batt, which draws by its own rule (BattHatch).</summary>
    public bool Solid => Families.Count == 0 && !Name.Equals(BattHatch.Name, StringComparison.OrdinalIgnoreCase);
    public bool Equals(HatchPattern? o) => o is not null && Name == o.Name && Description == o.Description && Units == o.Units && Families.SequenceEqual(o.Families);
    public override int GetHashCode() => HashCode.Combine(Name, Description, Families.Count, Units);
}

/// <summary>
/// The public <c>.pat</c> text format (the old Lapin's reader and writer):
/// <code>
/// *NAME, optional description
/// angle, x-origin, y-origin, delta-x, delta-y [, dash1, dash2, ...]
/// </code>
/// <c>;</c> starts a comment. A pattern without line families is a solid fill.
/// Two of the format's public comment headers are read (decision 671): <c>;%TYPE=MODEL</c> (or <c>DRAFTING</c>) after a
/// pattern's header, or before it, marks the pattern's units (<see cref="HatchUnits"/>; none: paper), and
/// <c>;%UNITS=INCH</c> anywhere before a pattern turns its lengths into millimetres (<c>MM</c>, the default, keeps them).
/// The writer writes <c>;%TYPE=MODEL</c> after a model pattern's header; paper patterns as before.
/// </summary>
public static class Pat
{
    public static (List<HatchPattern> Patterns, List<string> Errors) Parse(string text)
    {
        var patterns = new List<HatchPattern>();
        var errors = new List<string>();
        (string Name, string Description, List<HatchLineFamily> Families, HatchUnits Units)? current = null;
        void Finish() { if (current is { } c) patterns.Add(new HatchPattern(c.Name, c.Description, c.Families, c.Units)); }
        // A ;%TYPE= line before a header: the next pattern's units. ;%UNITS=INCH: lengths × 25.4 from there on.
        HatchUnits? pending = null;
        var unit = 1.0;
        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNo = i + 1;
            var raw = lines[i].TrimStart('﻿');
            if (raw.TrimStart() is var header && header.StartsWith(";%", StringComparison.Ordinal))
            {
                var kv = header[2..].Split('=', 2);
                var key = kv[0].Trim().ToUpperInvariant();
                var value = kv.Length > 1 ? kv[1].Trim().ToUpperInvariant() : "";
                if (key == "TYPE" && value is "MODEL" or "DRAFTING")
                {
                    var u = value == "MODEL" ? HatchUnits.Model : HatchUnits.Paper;
                    // Right after a header (before its first line): that pattern's; else the next one's.
                    if (current is { } c0 && c0.Families.Count == 0) current = c0 with { Units = u };
                    else pending = u;
                }
                else if (key == "UNITS" && value is "INCH" or "INCHES") unit = 25.4;
                else if (key == "UNITS" && value is "MM" or "MILLIMETERS" or "MILLIMETRES") unit = 1;
                continue;
            }
            var semi = raw.IndexOf(';');
            var line = (semi >= 0 ? raw[..semi] : raw).Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('*'))
            {
                Finish();
                var body = line[1..];
                var comma = body.IndexOf(',');
                var name = (comma >= 0 ? body[..comma] : body).Trim().ToUpperInvariant();
                var description = comma >= 0 ? body[(comma + 1)..].Trim() : "";
                if (name.Length == 0) { errors.Add($"line {lineNo}: pattern header without a name"); current = null; continue; }
                current = (name, description, [], pending ?? HatchUnits.Paper);
                pending = null;
                continue;
            }
            if (current is not { } cur) { errors.Add($"line {lineNo}: line family outside of a pattern (missing *NAME header)"); continue; }
            var tokens = line.Split(',').ToList();
            if (tokens.Count > 0 && tokens[^1].Trim().Length == 0) tokens.RemoveAt(tokens.Count - 1);
            var nums = new List<double>();
            string? bad = null;
            foreach (var tok in tokens)
            {
                var t = string.Concat(tok.Where(c => !char.IsWhiteSpace(c)));
                if (t.Length == 0 || !double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !double.IsFinite(v)) { bad = tok.Trim(); break; }
                nums.Add(v);
            }
            if (bad != null) { errors.Add($"line {lineNo}: invalid number \"{bad}\""); continue; }
            if (nums.Count < 5) { errors.Add($"line {lineNo}: expected at least 5 values (angle, x, y, delta-x, delta-y), got {nums.Count}"); continue; }
            cur.Families.Add(new HatchLineFamily(nums[0], new Vec2(nums[1] * unit, nums[2] * unit), nums[3] * unit, nums[4] * unit, nums.Skip(5).Select(v => v * unit).ToList()));
        }
        Finish();
        return (patterns, errors);
    }

    static string Fmt(double v) => v == 0 ? "0" : v.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>The patterns as <c>.pat</c> text (round-trips through <see cref="Parse"/>).</summary>
    public static string Serialize(IEnumerable<HatchPattern> patterns)
    {
        var sb = new StringBuilder();
        foreach (var p in patterns)
        {
            var desc = System.Text.RegularExpressions.Regex.Replace(p.Description, @"[\r\n;]+", " ").Trim();
            sb.Append(desc.Length > 0 ? $"*{p.Name.ToUpperInvariant()}, {desc}" : $"*{p.Name.ToUpperInvariant()}").Append('\n');
            if (p.Units == HatchUnits.Model) sb.Append(";%TYPE=MODEL\n");
            foreach (var f in p.Families)
                sb.Append(string.Join(", ", new[] { f.AngleDeg, f.Origin.X, f.Origin.Y, f.DeltaX, f.DeltaY }.Concat(f.Dashes).Select(Fmt))).Append('\n');
        }
        return sb.ToString();
    }
}
