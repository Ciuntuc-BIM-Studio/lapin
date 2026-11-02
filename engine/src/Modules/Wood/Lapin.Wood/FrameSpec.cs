using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Wood;

/// <summary>A header over an opening: none (a flat member, on a wall carrying nothing), sawn timber, LVL or glulam; its section and how many plies side by side.</summary>
public sealed record HeaderRule(string Kind, string Section, int Plies);

/// <summary>
/// How the openings of one width band are framed (docs/plan-wood.md, rules 3–5): the full-height king studs and the jack
/// studs under the header on each side, the header, the rough sill's plies (cripples under it on the module).
/// </summary>
public sealed record OpeningRule(double MaxWidth, int Kings, int Jacks, HeaderRule Header, int Sill)
{
    public JsonObject ToJson() => new()
    {
        ["maxWidth"] = MaxWidth, ["kings"] = Kings, ["jacks"] = Jacks, ["sill"] = Sill,
        ["header"] = new JsonObject { ["kind"] = Header.Kind, ["section"] = Header.Section, ["plies"] = Header.Plies },
    };

    /// <summary>A rule read from JSON, what it does not say taken from <paramref name="d"/>.</summary>
    public static OpeningRule From(JsonNode? n, OpeningRule d)
    {
        if (n is not JsonObject o) return d;
        var h = o["header"] as JsonObject;
        return new OpeningRule(
            Json.Number(o["maxWidth"]) ?? d.MaxWidth, (int)(Json.Number(o["kings"]) ?? d.Kings), (int)(Json.Number(o["jacks"]) ?? d.Jacks),
            new HeaderRule((string?)h?["kind"] ?? d.Header.Kind, (string?)h?["section"] ?? d.Header.Section, (int)(Json.Number(h?["plies"]) ?? d.Header.Plies)),
            (int)(Json.Number(o["sill"]) ?? d.Sill));
    }
}

/// <summary>
/// A timber-frame wall type's rules (decision 239), kept on the wall type as <c>wood</c>: the studs' section, spacing and
/// grade; the plates; the end studs; the noggin rows; where the module starts (the wall's start, or the project's origin
/// so a facade's walls share it); whether it carries load; the openings' fitting gaps, the least gap between two studs
/// before they merge, the narrowest pier between two openings before they share a header; the opening rules by width.
/// </summary>
public sealed record FrameSpec(
    string StudSection, double Spacing, string Grade, int BottomPlates, int TopPlates, int EndStuds, double[] Noggins,
    string LayoutFrom, bool LoadBearing, double GapSide, double GapTop, double GapBottom, double MinGap, double MinPier, OpeningRule[] Openings)
{
    public const string Key = "wood";
    public static readonly string[] Constructions = ["timberFrame"];
    public static readonly string[] HeaderKinds = ["none", "solid", "lvl", "glulam"];
    public static readonly string[] Sections = ["45x95 mm", "45x120 mm", "45x145 mm", "45x170 mm", "45x195 mm", "45x220 mm", "45x245 mm", "50x100 mm", "50x150 mm", "50x200 mm", "70x220 mm", "90x315 mm", "90x360 mm", "115x360 mm"];
    public static readonly string[] Grades = ["C14", "C16", "C18", "C24", "C30", "GL24h", "GL28h", "GL30c"];

    /// <summary>The rules for a load-bearing wall, by width: up to 900 mm, up to 1800 mm, beyond.</summary>
    public static OpeningRule[] Bearing =>
    [
        new(0.9, 1, 1, new("solid", "45x195 mm", 2), 1),
        new(1.8, 2, 2, new("solid", "45x245 mm", 2), 2),
        new(99, 2, 2, new("glulam", "90x360 mm", 1), 2),
    ];

    /// <summary>A partition carries nothing: a flat member over the opening, one king and one jack.</summary>
    public static OpeningRule[] Partition =>
    [
        new(99, 1, 1, new("none", "45x95 mm", 1), 1),
    ];

    /// <summary>The Nordic practice: 45 × 145 at 600, C24, a double top plate.</summary>
    public static FrameSpec Nordic => new("45x145 mm", 0.6, "C24", 1, 2, 2, [], "start", true, 0.01, 0.01, 0.01, 0.05, 0.2, Bearing);

    /// <summary>The Romanian practice: 50 × 150 at 625, C24, a double top plate, a noggin row at mid-height.</summary>
    public static FrameSpec Romanian => new("50x150 mm", 0.625, "C24", 1, 2, 2, [1.25], "start", true, 0.015, 0.015, 0.015, 0.05, 0.2,
    [
        new(0.9, 1, 1, new("solid", "50x200 mm", 2), 1),
        new(1.8, 2, 2, new("solid", "50x200 mm", 3), 2),
        new(99, 2, 2, new("glulam", "115x360 mm", 1), 2),
    ]);

    public static FrameSpec Default => Nordic;

    /// <summary>A section written "45x145 mm" (or "4.5x14.5" cm): its width (along the wall) and depth (across it), metres.</summary>
    public static (double W, double D) Size(string? text, (double W, double D) fallback)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text ?? "", @"^\s*(\d+(?:\.\d+)?)\s*x\s*(\d+(?:\.\d+)?)\s*(mm|cm)?\s*$");
        if (!m.Success) return fallback;
        var unit = m.Groups[3].Value == "cm" ? 0.01 : 0.001;
        return (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * unit, double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * unit);
    }

    public (double W, double D) Stud => Size(StudSection, (0.045, 0.145));

    /// <summary>The rule for an opening of this rough width: the first band it fits in, else the last.</summary>
    public OpeningRule RuleFor(double width) => Openings.FirstOrDefault(r => width <= r.MaxWidth + 1e-9) ?? Openings[^1];

    public JsonObject ToJson() => new()
    {
        ["construction"] = "timberFrame", ["studSection"] = StudSection, ["spacing"] = Spacing, ["grade"] = Grade,
        ["bottomPlates"] = BottomPlates, ["topPlates"] = TopPlates, ["endStuds"] = EndStuds,
        ["noggins"] = new JsonArray([.. Noggins.Select(x => (JsonNode)x)]), ["layoutFrom"] = LayoutFrom, ["loadBearing"] = LoadBearing,
        ["gapSide"] = GapSide, ["gapTop"] = GapTop, ["gapBottom"] = GapBottom, ["minGap"] = MinGap, ["minPier"] = MinPier,
        ["openings"] = new JsonArray([.. Openings.Select(r => (JsonNode)r.ToJson())]),
    };

    public static FrameSpec From(JsonNode? n)
    {
        var d = Default;
        if (n is not JsonObject o) return d;
        bool B(string k, bool def) => o[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : def;
        var rules = o["openings"] is JsonArray a && a.Count > 0
            ? a.Select((x, i) => OpeningRule.From(x, i < d.Openings.Length ? d.Openings[i] : d.Openings[^1])).OrderBy(r => r.MaxWidth).ToArray()
            : d.Openings;
        return new FrameSpec(
            (string?)o["studSection"] ?? d.StudSection, Json.Number(o["spacing"]) ?? d.Spacing, (string?)o["grade"] ?? d.Grade,
            (int)(Json.Number(o["bottomPlates"]) ?? d.BottomPlates), (int)(Json.Number(o["topPlates"]) ?? d.TopPlates), (int)(Json.Number(o["endStuds"]) ?? d.EndStuds),
            (o["noggins"] as JsonArray)?.Select(x => Json.Number(x) ?? 0).Where(x => x > 0).OrderBy(x => x).ToArray() ?? d.Noggins,
            (string?)o["layoutFrom"] == "project" ? "project" : "start", B("loadBearing", d.LoadBearing),
            Json.Number(o["gapSide"]) ?? d.GapSide, Json.Number(o["gapTop"]) ?? d.GapTop, Json.Number(o["gapBottom"]) ?? d.GapBottom,
            Json.Number(o["minGap"]) ?? d.MinGap, Json.Number(o["minPier"]) ?? d.MinPier, rules);
    }

    /// <summary>Refused when it cannot build: the spacing, the counts, the gaps, the sections.</summary>
    public string? Problem()
    {
        if (Spacing < 0.2 || Spacing > 1.5) return "The studs are 200 to 1500 mm apart.";
        if (BottomPlates is < 1 or > 3 || TopPlates is < 1 or > 3) return "A wall has 1 to 3 bottom plates and 1 to 3 top plates.";
        if (EndStuds is < 1 or > 3) return "A wall ends in 1 to 3 studs.";
        if (Size(StudSection, (0, 0)).W <= 0) return $"„{StudSection}” is not a section (write it as 45x145 mm).";
        if (new[] { GapSide, GapTop, GapBottom }.Any(g => g < 0 || g > 0.1)) return "A fitting gap is 0 to 100 mm.";
        foreach (var r in Openings)
        {
            if (r.Kings is < 0 or > 4 || r.Jacks is < 0 or > 4 || r.Sill is < 0 or > 3) return "An opening has 0 to 4 king and jack studs and 0 to 3 sill plies.";
            if (!HeaderKinds.Contains(r.Header.Kind)) return "A header is none, solid, LVL or glulam.";
            if (Size(r.Header.Section, (0, 0)).W <= 0 || r.Header.Plies is < 1 or > 4) return $"„{r.Header.Section}” × {r.Header.Plies} is not a header.";
        }
        return null;
    }

    public static FrameSpec? Of(Entity? wallType) => wallType?.Attrs[Key] is JsonObject o && (string?)o["construction"] == "timberFrame" ? From(o) : null;
}
