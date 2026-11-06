using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing.Export;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>One productivity norm: its code, its text, the unit it is given per, the man-hours one unit takes, and the crew it suggests.</summary>
public sealed record Norm(string Code, string Name, string Unit, double Hours, int? Crew = null)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["code"] = Code, ["name"] = Name, ["unit"] = Unit, ["hours"] = Hours };
        if (Crew is { } c) o["crew"] = c;
        return o;
    }

    public static Norm? Read(JsonNode? n) =>
        n is JsonObject o && (string?)o["code"] is { Length: > 0 } code && Json.Number(o["hours"]) is { } h && h >= 0 && double.IsFinite(h)
            ? new(code, (string?)o["name"] ?? "", (string?)o["unit"] ?? "", h, Json.Number(o["crew"]) is { } c && c >= 1 ? (int)Math.Round(c) : null)
            : null;
}

/// <summary>
/// The productivity norms the generator turns quantities into durations with (docs/plan-4d5d.md, stage 3). Norms are the
/// user's: Lapin ships none, it imports the user's table (CSV or XLSX: a code, a text, a unit, the man-hours per unit,
/// maybe a crew) into the project, one table (<c>LapinNormTable</c>), and writes a template of a few rows of its own
/// rough values, marked indicative, to start from or to show the columns.
/// </summary>
public static class Norms
{
    public const string Class = "LapinNormTable";

    /// <summary>
    /// Lapin's own rough values, not taken from any norm book: orders of magnitude for a first schedule, to be replaced by
    /// the user's. Each code is a step's default in the catalogue's templates.
    /// </summary>
    public static readonly IReadOnlyList<Norm> Indicative =
    [
        new("LAP-DEMO", "Demolition", "m3", 3.0, 4),
        new("LAP-EARTH", "Earthworks, by machine", "m3", 0.15, 2),
        new("LAP-FOUND", "Foundations, reinforced concrete", "m3", 8.0, 6),
        new("LAP-FRAME", "Reinforced concrete frame: columns, beams, floors", "m3", 12.0, 8),
        new("LAP-FLOOR", "Reinforced concrete floors and ring beams", "m3", 10.0, 6),
        new("LAP-MASON", "Masonry walls", "m3", 5.0, 4),
        new("LAP-TIMBER", "Timber frame walls", "m2", 1.2, 4),
        new("LAP-JOIST", "Timber floors", "m2", 0.8, 4),
        new("LAP-STEEL", "Steel structure, erected", "kg", 0.03, 6),
        new("LAP-ROOF", "Roof", "m2", 1.0, 4),
        new("LAP-CLAD", "Cladding", "m2", 0.6, 4),
        new("LAP-OPEN", "Doors and windows", "pcs", 2.5, 2),
        new("LAP-PLAST", "Wall finishes", "m2", 0.6, 4),
        new("LAP-FINISH", "Floors, ceilings and fittings", "m2", 0.8, 4),
        new("LAP-MEP", "Installations", "m", 0.5, 4),
        new("LAP-EXT", "External works", "m2", 0.4, 4),
    ];

    public sealed record Table(Entity? Entity, IReadOnlyList<Norm> Rows, string? Source, bool IsIndicative)
    {
        public Norm? Find(string? code) => code == null ? null : Rows.FirstOrDefault(n => string.Equals(n.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public static Table Of(IModelView m)
    {
        var e = m.OfClass(Class).FirstOrDefault();
        var rows = (e?.Attrs["rows"] as JsonArray)?.Select(Norm.Read).OfType<Norm>().ToList() ?? [];
        return new(e, rows, e?.Str("source"), e?.Bool("indicative") == true);
    }

    /// <summary>The project's table replaced by these rows (one undo step with the command).</summary>
    public static void Put(ICommandContext ctx, IReadOnlyList<Norm> rows, string source, bool indicative)
    {
        var attrs = new JsonObject { ["source"] = source, ["rows"] = new JsonArray(rows.Select(r => (JsonNode)r.ToJson()).ToArray()) };
        if (indicative) attrs["indicative"] = true;
        if (ctx.Model.OfClass(Class).FirstOrDefault() is { } old) ctx.Replace(old.With(name: "Productivity norms", attrs: attrs));
        else ctx.Add(new Entity(ctx.NewId(), Class, "Productivity norms", attrs));
    }

    /// <summary>A quantity's kind for a norm's unit (m3, m², mp, ml, buc, pcs, kg, t…) and what the model's SI value is multiplied by to be in it; null when Lapin cannot measure it.</summary>
    public static (string Basis, double Factor)? BasisOf(string? unit)
    {
        var u = (unit ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace(".", "");
        return u switch
        {
            "m3" or "m³" or "mc" or "cum" or "cbm" => ("volume", 1),
            "m2" or "m²" or "mp" or "sqm" => ("area", 1),
            "100m2" or "100mp" => ("area", 0.01),
            "m" or "ml" or "lm" or "rm" => ("length", 1),
            "buc" or "pcs" or "pc" or "nr" or "no" or "u" or "ea" or "each" or "stk" or "szt" or "un" or "pz" or "pièce" or "piece" => ("count", 1),
            "kg" => ("weight", 1),
            "t" or "to" or "tonne" or "tona" => ("weight", 0.001),
            _ => null,
        };
    }

    /// <summary>The unit a quantity's kind is shown in.</summary>
    public static string UnitOf(string basis) => basis switch { "volume" => "m³", "area" => "m²", "length" => "m", "weight" => "kg", _ => "pcs" };

    // ------------------------------------------------------------------ import

    public sealed record ImportResult(List<Norm> Rows, int Skipped, List<string> Notes);

    static readonly string[] CodeHeads = ["code", "cod", "symbol", "simbol", "norm", "norma", "article", "articol", "id"];
    static readonly string[] NameHeads = ["name", "description", "denumire", "descriere", "text", "work", "lucrare"];
    static readonly string[] UnitHeads = ["unit", "um", "u.m.", "u.m", "unitate", "unitatea", "uom"];
    static readonly string[] HoursHeads = ["hours", "man-hours", "manhours", "man hours", "ore", "ore-om", "ore om", "norma de timp", "normă de timp", "norma timp", "h/unit", "hours per unit", "ore/um", "manopera (ore)"];
    static readonly string[] RateHeads = ["productivity", "productivitate", "output", "units per hour", "um/ora", "um/oră", "rate"];
    static readonly string[] CrewHeads = ["crew", "echipa", "echipă", "team", "workers", "muncitori"];

    static string Head(string s) => s.Trim().Trim('"').ToLowerInvariant();

    /// <summary>
    /// A table of norms read from a file's bytes: a workbook's first sheet, or CSV (separated by semicolons, commas or tabs,
    /// a decimal comma allowed). The columns are found by their headings (code, name, unit, hours or a productivity, crew),
    /// in English or Romanian; a row without a code or a number is skipped.
    /// </summary>
    public static ImportResult Import(byte[] bytes)
    {
        List<List<string>> table;
        if (XlsxReader.IsZip(bytes))
        {
            try { table = XlsxReader.Read(bytes); }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException) { throw new CommandException("The workbook could not be read: " + ex.Message); }
        }
        else table = Csv(Encoding.UTF8.GetString(bytes).TrimStart('﻿'));
        var notes = new List<string>();
        var at = table.FindIndex(r => r.Any(c => CodeHeads.Contains(Head(c))) && r.Any(c => HoursHeads.Contains(Head(c)) || RateHeads.Contains(Head(c))));
        if (at < 0) throw new CommandException("The table has no headings Lapin knows: it needs a code column and an hours (or productivity) column, with a unit.");
        var heads = table[at].Select(Head).ToList();
        int Col(string[] names) => heads.FindIndex(names.Contains);
        int code = Col(CodeHeads), name = Col(NameHeads), unit = Col(UnitHeads), hours = Col(HoursHeads), rate = Col(RateHeads), crew = Col(CrewHeads);
        if (unit < 0) throw new CommandException("The table has no unit column (unit, UM).");
        var rows = new List<Norm>();
        var skipped = 0;
        string Cell(List<string> r, int i) => i >= 0 && i < r.Count ? r[i].Trim() : "";
        foreach (var r in table.Skip(at + 1))
        {
            if (r.All(string.IsNullOrWhiteSpace)) continue;
            var c = Cell(r, code);
            double? h = hours >= 0 ? XlsxReader.Number(Cell(r, hours)) : null;
            if (h == null && rate >= 0 && XlsxReader.Number(Cell(r, rate)) is { } per && per > 0) h = 1 / per;
            if (c.Length == 0 || h is not { } hv || hv < 0) { skipped++; continue; }
            var u = Cell(r, unit);
            if (BasisOf(u) == null && !notes.Any(n => n.Contains($"„{u}”"))) notes.Add($"Lapin does not measure in „{u}”: its rows count by default.");
            var crewN = crew >= 0 && XlsxReader.Number(Cell(r, crew)) is { } k && k >= 1 ? (int?)Math.Round(k) : null;
            rows.Add(new Norm(c, Cell(r, name), u, hv, crewN));
        }
        if (rows.Count == 0) throw new CommandException("No norm could be read from the table: each row needs a code and a number of hours.");
        return new(rows, skipped, notes);
    }

    /// <summary>CSV split into rows and cells: the separator the heading row uses most (; , or a tab), quotes honoured.</summary>
    public static List<List<string>> Csv(string text)
    {
        var first = text.Split('\n').FirstOrDefault() ?? "";
        var sep = new[] { ';', '\t', ',' }.OrderByDescending(c => first.Count(x => x == c)).First();
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                rows.Add(row); row = [];
            }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }

    /// <summary>The template: its columns and Lapin's indicative rows, as CSV (semicolons), each row saying it is indicative.</summary>
    public static string TemplateCsv()
    {
        var sb = new StringBuilder();
        sb.Append("code;name;unit;hours;crew\n");
        foreach (var n in Indicative)
            sb.Append($"{n.Code};{n.Name} (indicative, Lapin's rough value: replace it with your norm);{n.Unit};{n.Hours.ToString(CultureInfo.InvariantCulture)};{n.Crew}\n");
        return sb.ToString();
    }
}
