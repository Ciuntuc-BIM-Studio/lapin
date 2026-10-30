using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;

namespace Lapin.Platform.Cde;

/// <summary>
/// The project's naming convention after ISO 19650-2 (docs/plan-cde.md):
/// <c>Project-Originator-Volume-Level-Type-Role-Number</c>, each field
/// upper-case letters and digits, e.g. <c>PRJ-ORG-ZZ-00-DR-A-0101</c>. The
/// project's own values (the first four and the default role) are kept on the
/// IfcProject (<c>cde.naming</c>); a sheet's level comes from the storey its
/// plans draw, its role from its discipline, its number from its sheet
/// number. The revision and the status follow the name after underscores
/// (<c>…-0101_S2_P01.pdf</c>), so the seven fields stay parseable.
/// </summary>
public sealed record CdeNaming(string Project = "PRJ", string Originator = "ORG", string Volume = "ZZ", string Level = "ZZ", string Role = "A", string Separator = "-")
{
    public const string Drawing = "DR", Model = "M3", Exchange = "IE";

    /// <summary>The container types the convention knows, with what they are.</summary>
    public static readonly (string Code, string Label)[] Types =
    [
        ("DR", "Drawing"), ("M3", "3D model"), ("SH", "Schedule"), ("SP", "Specification"), ("RP", "Report"), ("CA", "Calculations"), ("IE", "Information exchange"),
    ];

    /// <summary>The role codes (ISO 19650-2, the UK annex's table), with the party each stands for.</summary>
    public static readonly (string Code, string Label)[] Roles =
    [
        ("A", "Architect"), ("B", "Building surveyor"), ("C", "Civil engineer"), ("D", "Drainage, highways engineer"), ("E", "Electrical engineer"),
        ("F", "Facilities manager"), ("G", "Geographical and land surveyor"), ("H", "Heating and ventilation designer"), ("I", "Interior designer"),
        ("K", "Client"), ("L", "Landscape architect"), ("M", "Mechanical engineer"), ("P", "Public health engineer"), ("Q", "Quantity surveyor"),
        ("S", "Structural engineer"), ("T", "Town and country planner"), ("W", "Contractor"), ("X", "Subcontractor"), ("Y", "Specialist designer"), ("Z", "General (non-disciplinary)"),
    ];

    static readonly Regex Field = new("^[A-Z0-9]+$", RegexOptions.Compiled);

    /// <summary>The role a discipline (a sheet's, a view's) files under; the project's default for any other.</summary>
    public string RoleOf(string? discipline) => discipline?.Trim().ToLowerInvariant() switch
    {
        "architecture" or "architectural" => "A",
        "structure" or "structural" => "S",
        "mep" or "mechanical" => "M",
        "electrical" => "E",
        "civil" => "C",
        "landscape" => "L",
        _ => Role,
    };

    /// <summary>
    /// A storey's level code: <c>00</c> for the storey at ±0.00 (else the lowest at or above it), <c>01</c>, <c>02</c>… above, <c>B1</c>, <c>B2</c>… below.
    /// </summary>
    public static string LevelOf(IModelView m, string? storeyId)
    {
        var storeys = Spatial.Storeys(m).ToList();
        var i = storeys.FindIndex(s => s.Id == storeyId);
        if (i < 0) return "ZZ";
        var ground = storeys.FindIndex(s => s.Num("elevation") >= -1e-6);
        if (ground < 0) ground = storeys.Count - 1;
        var k = i - ground;
        return k >= 0 ? k.ToString("00") : $"B{-k}";
    }

    /// <summary>A sheet's level: its plan views' storey when they all draw one, <c>ZZ</c> when several or none (sections and elevations alone).</summary>
    public static string LevelOfSheet(IModelView m, Entity sheet)
    {
        var storeys = new HashSet<string>();
        var others = false;
        foreach (var vp in Sheets.Viewports(sheet))
        {
            var view = Views.Find(m, vp.ViewId);
            if (view == null) continue;
            if (view.Str("kind") is "plan" or null && view.Str("storeyId") is { } s) storeys.Add(s); else others = true;
        }
        return storeys.Count == 1 && !others ? LevelOf(m, storeys.First()) : "ZZ";
    }

    /// <summary>The number field from a sheet number: its trailing digits, four wide (<c>A-101</c> → <c>0101</c>); null when it has none.</summary>
    public static string? NumberOf(string? sheetNumber)
    {
        var digits = Regex.Match(sheetNumber ?? "", @"(\d+)\s*$");
        return digits.Success ? digits.Groups[1].Value.PadLeft(4, '0') : null;
    }

    string Join(params string[] fields) => string.Join(Separator, fields);

    /// <summary>The name of a sheet's drawing container (no revision, no extension).</summary>
    public string For(IModelView m, Entity sheet) =>
        Join(Project, Originator, Volume, LevelOfSheet(m, sheet), Drawing, RoleOf(sheet.Str("discipline")), NumberOf(sheet.Str("number")) ?? "0000");

    /// <summary>The whole model's container (the IFC).</summary>
    public string ForModel() => Join(Project, Originator, Volume, Level, Model, Role, "0001");

    /// <summary>The set of every sheet in one file.</summary>
    public string ForSet() => Join(Project, Originator, Volume, Level, Drawing, Role, "0000");

    /// <summary>The transmittal (and its manifest) of issue number <paramref name="n"/>.</summary>
    public string ForTransmittal(int n) => Join(Project, Originator, Volume, Level, Exchange, Role, n.ToString("0000"));

    /// <summary>A file's name with its status and revision: <c>name_S2_P01.ext</c>.</summary>
    public static string WithRevision(string name, string status, string revision, string ext) => $"{name}_{status}_{revision}.{ext}";

    /// <summary>What is wrong with a name's fields (empty when it follows the convention).</summary>
    public List<string> Validate(string name)
    {
        var o = new List<string>();
        var fields = name.Split(Separator);
        if (fields.Length != 7) { o.Add($"{fields.Length} fields instead of 7 (project, originator, volume, level, type, role, number)"); return o; }
        string[] names = ["project", "originator", "volume", "level", "type", "role", "number"];
        (int Min, int Max)[] lengths = [(2, 6), (3, 6), (2, 3), (2, 3), (2, 2), (1, 2), (4, 6)];
        for (var i = 0; i < 7; i++)
        {
            var f = fields[i];
            if (!Field.IsMatch(f)) o.Add($"the {names[i]} „{f}” is not upper-case letters and digits");
            else if (f.Length < lengths[i].Min || f.Length > lengths[i].Max) o.Add($"the {names[i]} „{f}” is not {lengths[i].Min}–{lengths[i].Max} characters");
        }
        if (fields[4].Length == 2 && Types.All(t => t.Code != fields[4])) o.Add($"the type „{fields[4]}” is not one of {string.Join(", ", Types.Select(t => t.Code))}");
        if (fields[6].Length >= 4 && !fields[6].All(char.IsAsciiDigit)) o.Add($"the number „{fields[6]}” is not digits");
        return o;
    }

    /// <summary>A file name taken apart: its fields, status and revision; null when it is not of the convention.</summary>
    public (string Project, string Originator, string Volume, string Level, string Type, string Role, string Number, string? Status, string? Revision)? Parse(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var parts = stem.Split('_');
        if (Validate(parts[0]).Count > 0) return null;
        var f = parts[0].Split(Separator);
        return (f[0], f[1], f[2], f[3], f[4], f[5], f[6], parts.Length > 2 ? parts[1] : null, parts.Length > 1 ? parts[^1] : null);
    }

    public JsonObject ToJson() => new()
    {
        ["project"] = Project, ["originator"] = Originator, ["volume"] = Volume, ["level"] = Level, ["role"] = Role, ["separator"] = Separator,
    };

    /// <summary>The project's naming, as kept on the IfcProject (<c>cde.naming</c>); the defaults where nothing is set.</summary>
    public static CdeNaming Of(IModelView m)
    {
        var n = Cde.Settings(m)["naming"] as JsonObject;
        var d = new CdeNaming();
        string F(string k, string fallback) => (string?)n?[k] is { Length: > 0 } v ? v : fallback;
        return new CdeNaming(F("project", d.Project), F("originator", d.Originator), F("volume", d.Volume), F("level", d.Level), F("role", d.Role), F("separator", d.Separator));
    }

    /// <summary>The naming with the fields of <paramref name="a"/> set (each cleaned to upper-case letters and digits); a bad field is refused.</summary>
    public CdeNaming With(JsonObject a)
    {
        string Clean(string key, string current, int min, int max)
        {
            if ((string?)a[key] is not { } raw) return current;
            var v = Regex.Replace(raw.Trim().ToUpperInvariant(), "[^A-Z0-9]", "");
            if (v.Length < min || v.Length > max) throw new CommandException($"The {key} code is {min}–{max} upper-case letters and digits.");
            return v;
        }
        return this with
        {
            Project = Clean("project", Project, 2, 6), Originator = Clean("originator", Originator, 3, 6), Volume = Clean("volume", Volume, 2, 3),
            Level = Clean("level", Level, 2, 3), Role = Clean("role", Role, 1, 2),
        };
    }
}
