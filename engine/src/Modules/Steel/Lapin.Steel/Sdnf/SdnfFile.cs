using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lapin.Geometry;

namespace Lapin.Steel.Sdnf;

/// <summary>
/// A Steel Detailing Neutral File (SDNF, docs/sdnf.md), as Intergraph defined it, version 3.0: an ASCII file of packets —
/// 00 the title, 10 the linear members, 20 the plates, 22 the holes, 30 the loads, 40 the connections, 50 the grids, 60 the
/// curved members — each a fixed header record (<c>Packet 10</c>) and free-format records of blank-delimited fields, strings
/// in double quotes, at most 132 characters, comments starting with <c>#</c>. Lengths here are metres, whatever the file's
/// units; the reader and the writer convert.
/// </summary>
public sealed record SdnfFile(SdnfTitle Title, IReadOnlyList<SdnfMember> Members, IReadOnlyList<SdnfPlate> Plates, IReadOnlyList<SdnfGridSet> Grids)
{
    /// <summary>What Lapin wrote in its comments: an SDNF number's element (<c>member 12</c> → its GlobalId, <c>plate 3</c> → <c>node:GlobalId:part</c>, <c>grid 1</c> → its GlobalId).</summary>
    public IReadOnlyDictionary<string, string> LapinIds { get; init; } = new Dictionary<string, string>();
    /// <summary>What the reader left out and why (holes, loads, connections, curved members).</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
    /// <summary>The members' linear unit as the file names it (<c>millimeters</c>, <c>feet</c>).</summary>
    public string Unit { get; init; } = "millimeters";
    /// <summary>Whether Lapin wrote the file (its issue code, its comments).</summary>
    public bool FromLapin => Title.IssueCode.StartsWith("Lapin", StringComparison.OrdinalIgnoreCase) || LapinIds.Count > 0;
}

/// <summary>Packet 00: the engineering firm, client, structure and project, when the file was made, its revision and issue code (the program that wrote it), the design code.</summary>
public sealed record SdnfTitle(string Version, string Firm, string Client, string Structure, string Project, string Date, string Time, int Revision, string IssueCode, string DesignCode)
{
    /// <summary>Where the file came from, to tell one source's member numbers from another's.</summary>
    public string Source => $"{Firm}/{Client}/{Structure}/{Project}";
}

/// <summary>
/// A linear member (packet 10): its number, the cardinal point its line runs through (1–9 the section's box: bottom, middle,
/// top rows, left to right; 10 its centroid), its status, class, type and piece mark; its section's name and grade, turned by
/// <see cref="Rotation"/> (degrees, right-handed about the member looking from start to end) from <see cref="Orientation"/>
/// (the section's up before the turn), mirrored; the line's ends, the cutbacks off them, the 2D offsets of the section, the 3D
/// offsets from each end to its node, and the end releases (Tx Ty Tz Rx Ry Rz at each end, 1 released).
/// </summary>
public sealed record SdnfMember(long Id, int Cardinal, string Type, string PieceMark, string Size, string Grade, double Rotation, bool MirrorX, bool MirrorY,
    Vec3 Orientation, Vec3 Start, Vec3 End, double StartCutback = 0, double EndCutback = 0)
{
    public int Status { get; init; }
    public int Class { get; init; }
    public int Revision { get; init; }
    public double OffsetX { get; init; }
    public double OffsetY { get; init; }
    public Vec3 StartOffset { get; init; }
    public Vec3 EndOffset { get; init; }
    public IReadOnlyList<int> Releases { get; init; } = new int[12];
}

/// <summary>
/// A plate (packet 20): its number, where it lies on its outline (<see cref="ConnectPoint"/>: 0 centred, 1 the positive face,
/// −1 the negative one), its type, piece mark, grade and thickness; version 3's two loops of vertices, the top face's and the
/// bottom's, each closed by its first vertex again, each vertex's flag saying the next segment is straight (1) or curved (2),
/// 0 on the closing one; a circle (<see cref="Periphery"/> 2) is its centre on each face, the flag its radius. A version 2
/// plate has one loop (<see cref="Bottom"/> empty).
/// </summary>
public sealed record SdnfPlate(long Id, int ConnectPoint, string Type, string PieceMark, string Grade, double Thickness, int Periphery,
    IReadOnlyList<(Vec3 P, double Flag)> Top, IReadOnlyList<(Vec3 P, double Flag)> Bottom)
{
    public int Status { get; init; }
    public int Class { get; init; }
    public int Revision { get; init; }
}

/// <summary>A set of grid lines (packet 50): its number, description and lines (a label, a start and an end).</summary>
public sealed record SdnfGridSet(int Number, string Description, IReadOnlyList<(string Label, Vec3 Start, Vec3 End)> Lines);

/// <summary>The linear units SDNF files name, in metres.</summary>
public static class SdnfUnits
{
    public static double? Metres(string unit) => unit.Trim().ToLowerInvariant().TrimEnd('.') switch
    {
        "millimeters" or "millimetres" or "millimeter" or "millimetre" or "mm" => 0.001,
        "centimeters" or "centimetres" or "centimeter" or "centimetre" or "cm" => 0.01,
        "meters" or "metres" or "meter" or "metre" or "m" => 1,
        "feet" or "foot" or "ft" => 0.3048,
        "inches" or "inch" or "in" => 0.0254,
        _ => null,
    };
}

/// <summary>Writes an <see cref="SdnfFile"/> as version 3.0, in millimetres, every record the version defines.</summary>
public static class SdnfWriter
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public const int MaxLine = 132;

    /// <summary>A string field: double quotes, a double quote inside made a single one (as the format says), cut to its width, ASCII.</summary>
    public static string Q(string? s, int width)
    {
        var t = Ascii(s ?? "").Replace('"', '\'');
        if (t.Length > width) t = t[..width];
        return $"\"{t}\"";
    }

    /// <summary>Lapin's × and Σ, and anything else past ASCII, as SDNF's plain characters.</summary>
    public static string Ascii(string s)
    {
        var o = new StringBuilder(s.Length);
        foreach (var c in s.Replace("×", "x").Replace("Σ", "SIGMA").Replace("Ø", "D").Replace("ø", "D").Normalize(NormalizationForm.FormD))
            if (c < 128 && !char.IsControl(c)) o.Append(c);
            else if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && !char.IsControl(c)) o.Append('?');
        return o.ToString();
    }

    /// <summary>A length in millimetres: to a thousandth, no trailing zeros, never −0.</summary>
    static string L(double metres, int decimals = 3)
    {
        var v = Math.Round(metres * 1000, decimals);
        if (v == 0) v = 0;
        return v.ToString(decimals == 3 ? "0.###" : "0." + new string('#', Math.Max(1, decimals)), Inv);
    }

    static string N(double v, string format = "0.######")
    {
        var r = Math.Round(v, 6);
        if (r == 0) r = 0;
        return r.ToString(format, Inv);
    }

    /// <summary>A unit vector's component, to nine places: the section's turn to a millionth of a degree.</summary>
    static string U(double v)
    {
        var r = Math.Round(v, 9);
        if (r == 0) r = 0;
        return r.ToString("0.#########", Inv);
    }

    static string P(Vec3 p, int decimals = 3) => $"{L(p.X, decimals)} {L(p.Y, decimals)} {L(p.Z, decimals)}";

    /// <summary>A record no longer than the format allows: the lengths to a hundredth, then a tenth, when a thousandth is too long.</summary>
    static string Fit(Func<int, string> record)
    {
        for (var d = 3; d >= 0; d--)
        {
            var s = record(d);
            if (s.Length <= MaxLine || d == 0) return s;
        }
        return record(0);
    }

    public static string Write(SdnfFile f, IReadOnlyList<string>? comments = null)
    {
        var o = new StringBuilder();
        void Line(string s) => o.Append(s).Append('\n');
        var t = f.Title;
        // Packet 00, the title.
        Line("Packet 00");
        Line(Q("SDNF Version 3.0", 24));
        Line(Q(t.Firm, 80));
        Line(Q(t.Client, 80));
        Line(Q(t.Structure, 80));
        Line(Q(t.Project, 80));
        Line($"{Q(t.Date, 30)} {Q(t.Time, 30)}");
        Line($"{t.Revision} {Q(t.IssueCode, 64)}");
        Line(Q(t.DesignCode, 80));
        Line("0");
        foreach (var c in comments ?? []) Line("# " + Ascii(c));
        var (date, time) = (Q(t.Date, 30), Q(t.Time, 30));

        // Packet 10, the linear members: ten records each.
        Line("Packet 10");
        Line($"{Q("millimeters", 24)} {f.Members.Count}");
        foreach (var m in f.Members)
        {
            Line($"{m.Id} {m.Cardinal} {m.Status} {m.Class} {Q(m.Type, 24)} {Q(m.PieceMark, 24)} {m.Revision}");
            Line($"{Q(m.Size, 24)} {Q(m.Grade, 24)} {N(m.Rotation)} {(m.MirrorX ? 1 : 0)} {(m.MirrorY ? 1 : 0)}");
            Line(Fit(d => $"{U(m.Orientation.X)} {U(m.Orientation.Y)} {U(m.Orientation.Z)} {P(m.Start, d)} {P(m.End, d)} {L(m.StartCutback, d)} {L(m.EndCutback, d)}"));
            Line($"{L(m.OffsetX)} {L(m.OffsetY)}");
            Line(Fit(d => $"{P(m.StartOffset, d)} {P(m.EndOffset, d)}"));
            Line(string.Join(' ', Enumerable.Range(0, 12).Select(i => i < m.Releases.Count ? m.Releases[i] : 0)));
            // Record 7: fabricator, prebuy mark, subtype, created and modified (date, time), updated, approval status.
            Line($"0 {Q("", 24)} 0 {date} {time} {date} {time} 0 0");
            // Record 8: the connections at each end, the configuration, the assemblies.
            Line("0 0 0 0 0 0 0 0 0 0 0 0");
            // Record 9: material, coatings, fireproofing thickness, description, type, start, end.
            Line("0 0 0 0 0 0 0");
            // Record 10: the fabricator's note, five user attributes.
            Line("0 0 0 0 0 0");
        }

        // Packet 20, the plates.
        Line("Packet 20");
        Line($"{Q("millimeters", 24)} {Q("millimeters", 24)} {f.Plates.Count}");
        foreach (var p in f.Plates)
        {
            var count = p.Periphery == 2 ? 1 : p.Top.Count;
            Line($"{p.Id} {p.ConnectPoint} {p.Status} {p.Class} {Q(p.Type, 24)} {p.Revision}");
            Line($"{Q(p.PieceMark, 24)} {Q(p.Grade, 24)} {L(p.Thickness)} {count} 0 {p.Periphery}");
            foreach (var (v, flag) in p.Top.Concat(p.Bottom))
                Line(Fit(d => $"{P(v, d)} {(p.Periphery == 2 ? L(flag, d) : ((int)flag).ToString(Inv))}"));
            Line($"0 {Q("", 24)} 0 {date} {time} {date} {time} 0 0");
            Line("0 0 0");
            Line("0 0 0 0 0");
            Line("0 0 0 0 0 0");
        }

        // Packet 50, the grids: in the members' unit (the packet names none).
        if (f.Grids.Count > 0)
        {
            Line("Packet 50");
            Line($"{f.Grids.Count}");
            foreach (var g in f.Grids)
            {
                Line($"{g.Number} {g.Lines.Count} {Q(g.Description, 64)}");
                foreach (var (label, a, b) in g.Lines) Line(Fit(d => $"{Q(label, 64)} {P(a, d)} {P(b, d)}"));
            }
        }
        return o.ToString();
    }
}

/// <summary>
/// Reads an SDNF file, version 2.0 or 3.0 (its title says which): the title, the linear members (six records each in version
/// 2, ten in version 3, as many as the packet holds per member when a program writes more), the plates, the grids; the holes,
/// loads, connections and curved members are counted in <see cref="SdnfFile.Notes"/>. Lapin's comments
/// (<c># Lapin member 12 GlobalId</c>) give back the elements it wrote.
/// </summary>
public static partial class SdnfReader
{
    [GeneratedRegex(@"^\s*Packet\s*(\d+)\s*$", RegexOptions.IgnoreCase)] private static partial Regex Header();
    [GeneratedRegex(@"^#\s*Lapin\s+(member|plate|grid)\s+(-?\d+)\s+(\S+)\s*$", RegexOptions.IgnoreCase)] private static partial Regex LapinComment();

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A record's fields: quoted strings (without their quotes) and the words between blanks.</summary>
    public static List<string> Fields(string line)
    {
        var o = new List<string>();
        var i = 0;
        while (i < line.Length)
        {
            if (char.IsWhiteSpace(line[i])) { i++; continue; }
            if (line[i] is '"' or '“' or '”')
            {
                var j = i + 1;
                while (j < line.Length && line[j] is not ('"' or '“' or '”')) j++;
                o.Add(line[(i + 1)..Math.Min(j, line.Length)]);
                i = j + 1;
                continue;
            }
            var k = i;
            while (k < line.Length && !char.IsWhiteSpace(line[k])) k++;
            o.Add(line[i..k]);
            i = k;
        }
        return o;
    }

    sealed class Cursor(List<string> lines, string packet)
    {
        int at;
        public bool More => at < lines.Count;
        public int Left => lines.Count - at;
        public List<string> Next() => More ? Fields(lines[at++]) : throw new FormatException($"Packet {packet} ends before its last record.");
        public void Skip(int n) => at = Math.Min(lines.Count, at + n);
    }

    static double D(List<string> f, int i) => i < f.Count && double.TryParse(f[i], NumberStyles.Float, Inv, out var v) ? v : 0;
    static int I(List<string> f, int i) => i < f.Count && int.TryParse(f[i], NumberStyles.Integer, Inv, out var v) ? v
        : i < f.Count && double.TryParse(f[i], NumberStyles.Float, Inv, out var d) ? (int)Math.Round(d) : 0;
    static long Id(List<string> f, int i) => i < f.Count && long.TryParse(f[i], NumberStyles.Integer, Inv, out var v) ? v : throw new FormatException($"„{(i < f.Count ? f[i] : "")}” is not a member or plate number.");
    static string S(List<string> f, int i) => i < f.Count ? f[i].Trim() : "";

    public static SdnfFile Read(string text)
    {
        var packets = new List<(string Id, List<string> Lines)>();
        var lapin = new Dictionary<string, string>();
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0) continue;
            if (trimmed.StartsWith('#'))
            {
                if (LapinComment().Match(trimmed) is { Success: true } c) lapin[$"{c.Groups[1].Value.ToLowerInvariant()} {c.Groups[2].Value}"] = c.Groups[3].Value;
                continue;
            }
            if (Header().Match(trimmed) is { Success: true } h) { packets.Add((h.Groups[1].Value.PadLeft(2, '0'), [])); continue; }
            if (packets.Count == 0) throw new FormatException("The file does not start with a packet (Packet 00): it is no SDNF file.");
            packets[^1].Lines.Add(line);
        }
        if (packets.Count == 0 || packets[0].Id != "00") throw new FormatException("The file has no title packet (Packet 00) first: it is no SDNF file.");

        var title = Title(packets[0].Lines);
        var v3 = title.Version.Contains('3');
        var members = new List<SdnfMember>();
        var plates = new List<SdnfPlate>();
        var grids = new List<SdnfGridSet>();
        var notes = new List<string>();
        string? unit = null;
        double scale = 1;
        foreach (var (id, lines) in packets.Skip(1))
            switch (id)
            {
                case "10":
                {
                    var (u, s, list) = Members(lines, v3);
                    (unit, scale) = (unit ?? u, unit == null ? s : scale);
                    members.AddRange(list);
                    break;
                }
                case "20":
                {
                    var (u, s, list) = Plates(lines, v3);
                    if (unit == null) (unit, scale) = (u, s);
                    plates.AddRange(list);
                    break;
                }
                case "50": grids.AddRange(Grids(lines, scale)); break;
                case "22": notes.Add($"The holes (packet 22, {Count(lines)}) were left out: Lapin's plates take none yet."); break;
                case "30": notes.Add("The member loads (packet 30) were left out."); break;
                case "40": notes.Add("The connection details (packet 40, text only) were left out."); break;
                case "60": notes.Add($"The curved members (packet 60, {Count(lines)}) were left out: only straight members come in."); break;
                default: notes.Add($"Packet {id} is not one SDNF defines: left out."); break;
            }
        return new SdnfFile(title, members, plates, grids) { LapinIds = lapin, Notes = notes, Unit = unit ?? "meters" };
    }

    /// <summary>A packet's count of items, from its count record (the second with units, the first for the others).</summary>
    static string Count(List<string> lines) => lines.Count > 0 ? $"{Fields(lines[0]).LastOrDefault() ?? "?"} given" : "none";

    static SdnfTitle Title(List<string> lines)
    {
        var c = new Cursor(lines, "00");
        var first = c.Next();
        // Version 3 begins with its version; version 2 with the engineering firm.
        string version = "SDNF Version 2.0";
        if (S(first, 0).StartsWith("SDNF", StringComparison.OrdinalIgnoreCase)) { version = S(first, 0); first = c.More ? c.Next() : []; }
        var client = c.More ? S(c.Next(), 0) : "";
        var structure = c.More ? S(c.Next(), 0) : "";
        var project = c.More ? S(c.Next(), 0) : "";
        var when = c.More ? c.Next() : [];
        var issue = c.More ? c.Next() : [];
        var design = c.More ? S(c.Next(), 0) : "";
        return new SdnfTitle(version, S(first, 0), client, structure, project, S(when, 0), S(when, 1), I(issue, 0), S(issue, 1), design);
    }

    static (string Unit, double Scale) Units(List<string> f, string packet)
    {
        var name = S(f, 0);
        return (name, SdnfUnits.Metres(name) ?? throw new FormatException($"Packet {packet} is in „{name}”, a unit Lapin does not know (millimeters, centimeters, meters, inches, feet)."));
    }

    static (string Unit, double Scale, List<SdnfMember> Members) Members(List<string> lines, bool v3)
    {
        if (lines.Count == 0) return ("meters", 1, []);
        var head = Fields(lines[0]);
        var (unit, k) = Units(head, "10");
        var n = I(head, head.Count - 1);
        var body = lines.Skip(1).ToList();
        // Ten records a member in version 3, six in version 2; as many as the packet holds per member when a program writes more.
        var per = n > 0 && body.Count % n == 0 && body.Count / n >= 6 ? body.Count / n : v3 ? 10 : 6;
        var c = new Cursor(body, "10");
        var o = new List<SdnfMember>();
        for (var i = 0; i < n && c.Left >= 6; i++)
        {
            var r1 = c.Next();
            var r2 = c.Next();
            var r3 = c.Next();
            var r4 = c.Next();
            var r5 = c.Next();
            var r6 = c.Next();
            c.Skip(per - 6);
            Vec3 V(List<string> f, int at, double s) => new(D(f, at) * s, D(f, at + 1) * s, D(f, at + 2) * s);
            o.Add(new SdnfMember(Id(r1, 0), I(r1, 1), S(r1, 4), S(r1, 5), S(r2, 0), S(r2, 1), D(r2, 2), I(r2, 3) != 0, I(r2, 4) != 0,
                V(r3, 0, 1), V(r3, 3, k), V(r3, 6, k), D(r3, 9) * k, D(r3, 10) * k)
            {
                Status = I(r1, 2), Class = I(r1, 3), Revision = I(r1, 6),
                OffsetX = D(r4, 0) * k, OffsetY = D(r4, 1) * k,
                StartOffset = V(r5, 0, k), EndOffset = V(r5, 3, k),
                Releases = Enumerable.Range(0, 12).Select(j => I(r6, j)).ToArray(),
            });
        }
        return (unit, k, o);
    }

    static (string Unit, double Scale, List<SdnfPlate> Plates) Plates(List<string> lines, bool v3)
    {
        if (lines.Count == 0) return ("meters", 1, []);
        var head = Fields(lines[0]);
        var (unit, k) = Units(head, "20");
        var tk = head.Count >= 3 ? SdnfUnits.Metres(S(head, 1)) ?? k : k;
        var n = I(head, head.Count - 1);
        var c = new Cursor(lines.Skip(1).ToList(), "20");
        var o = new List<SdnfPlate>();
        for (var i = 0; i < n && c.More; i++)
        {
            var r1 = c.Next();
            var r2 = c.Next();
            // Version 3: piece mark, grade, thickness, vertices per face, offset, periphery; version 2: piece mark, grade, thickness, vertices.
            var three = v3 && r2.Count >= 6;
            var count = I(r2, 3);
            var periphery = three ? I(r2, 5) : 0;
            List<(Vec3, double)> Loop(int m, bool scaledFlag)
            {
                var l = new List<(Vec3, double)>();
                for (var j = 0; j < m; j++)
                {
                    var f = c.Next();
                    l.Add((new Vec3(D(f, 0) * k, D(f, 1) * k, D(f, 2) * k), f.Count > 3 ? D(f, 3) * (scaledFlag ? k : 1) : 1));
                }
                return l;
            }
            List<(Vec3, double)> top, bottom;
            if (!three) { top = Loop(count, false); bottom = []; }
            else if (periphery == 2) { top = Loop(1, true); bottom = Loop(1, true); }
            else { top = Loop(count, false); bottom = Loop(count, false); }
            if (three) c.Skip(4);
            o.Add(new SdnfPlate(Id(r1, 0), I(r1, 1), S(r1, 4), S(r2, 0), S(r2, 1), D(r2, 2) * tk, periphery, top, bottom)
            {
                Status = I(r1, 2), Class = I(r1, 3), Revision = I(r1, 5),
            });
        }
        return (unit, k, o);
    }

    static List<SdnfGridSet> Grids(List<string> lines, double k)
    {
        if (lines.Count == 0) return [];
        var c = new Cursor(lines, "50");
        var sets = I(c.Next(), 0);
        var o = new List<SdnfGridSet>();
        for (var i = 0; i < sets && c.More; i++)
        {
            var r = c.Next();
            var n = I(r, 1);
            var list = new List<(string, Vec3, Vec3)>();
            for (var j = 0; j < n && c.More; j++)
            {
                var f = c.Next();
                list.Add((S(f, 0), new Vec3(D(f, 1) * k, D(f, 2) * k, D(f, 3) * k), new Vec3(D(f, 4) * k, D(f, 5) * k, D(f, 6) * k)));
            }
            o.Add(new SdnfGridSet(I(r, 0), S(r, 2), list));
        }
        return o;
    }
}
