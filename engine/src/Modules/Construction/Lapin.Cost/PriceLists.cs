using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing.Export;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>
/// One row of a price list: an article (its code, text, unit and unit price split into material, labour, equipment and
/// transport; maybe its man-hours per unit and the trade that does it) or, when <see cref="Article"/> names one, a
/// resource of that article (Romanian estimating's „consumuri”): its kind (material, labour, equipment, transport), its
/// consumption per unit of the article and its own unit price, in the column of its kind.
/// </summary>
public sealed record PriceRow(string Code, string Name, string Unit, double Material, double Labour, double Equipment, double Transport,
    double? Hours = null, string? Trade = null, string? Article = null, string? Kind = null, double? Consumption = null)
{
    public double Total => Material + Labour + Equipment + Transport;
    public bool IsResource => Article != null;

    /// <summary>A resource's own unit price: the value in its kind's column.</summary>
    public double ResourcePrice => Kind switch { "labour" => Labour, "equipment" => Equipment, "transport" => Transport, _ => Material };

    static double R(double v) => Math.Round(v, 6);

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["c"] = Code, ["n"] = Name, ["u"] = Unit };
        if (Material != 0) o["m"] = R(Material);
        if (Labour != 0) o["l"] = R(Labour);
        if (Equipment != 0) o["e"] = R(Equipment);
        if (Transport != 0) o["t"] = R(Transport);
        if (Hours is { } h) o["h"] = R(h);
        if (Trade is { Length: > 0 } tr) o["tr"] = tr;
        if (Article is { Length: > 0 } a) o["a"] = a;
        if (Kind is { Length: > 0 } k) o["k"] = k;
        if (Consumption is { } q) o["q"] = R(q);
        return o;
    }

    public static PriceRow? Read(JsonNode? n)
    {
        if (n is not JsonObject o || (string?)o["c"] is not { Length: > 0 } code) return null;
        double N(string k) => Json.Number(o[k]) ?? 0;
        return new(code, (string?)o["n"] ?? "", (string?)o["u"] ?? "", N("m"), N("l"), N("e"), N("t"), Json.Number(o["h"]), (string?)o["tr"],
            (string?)o["a"], (string?)o["k"], Json.Number(o["q"]));
    }
}

/// <summary>
/// The user's price lists (docs/plan-4d5d.md, stage 6). Prices are the user's: Lapin ships no catalogue, it imports the
/// user's table (CSV or XLSX, the columns matched in a dialog, headings in Romanian or English guessed) into the project
/// as a <c>LapinPriceList</c> (one entity a list, its rows a compact table, kept in the cost module's Arrow table of the
/// package), and writes a small template of its own rough values, marked indicative, to show the columns.
/// </summary>
public static class PriceLists
{
    public const string Class = "LapinPriceList";

    /// <summary>The columns a price list can have, in the order the matching dialog shows them.</summary>
    public static readonly string[] Fields = ["code", "name", "unit", "material", "labour", "equipment", "transport", "price", "hours", "trade", "article", "kind", "consumption"];

    public sealed record List(Entity Entity, string Currency, IReadOnlyList<PriceRow> Rows, bool Indicative)
    {
        public string Id => Entity.Id;
        public string Name => Entity.Name;
        IReadOnlyDictionary<string, PriceRow>? _articles;
        ILookup<string, PriceRow>? _resources;
        public IReadOnlyDictionary<string, PriceRow> Articles => _articles ??= Rows.Where(r => !r.IsResource).GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        public ILookup<string, PriceRow> Resources => _resources ??= Rows.Where(r => r.IsResource).ToLookup(r => r.Article!, StringComparer.OrdinalIgnoreCase);
    }

    public static List Read(Entity e) =>
        new(e, e.Str("currency") is { Length: > 0 } c ? c : "RON", (e.Attrs["rows"] as JsonArray)?.Select(PriceRow.Read).OfType<PriceRow>().ToList() ?? [], e.Bool("indicative"));

    /// <summary>The project's lists in the order they were made.</summary>
    public static List<List> All(IModelView m) => m.OfClass(Class).Select(Read).ToList();

    public static JsonObject Attrs(IReadOnlyList<PriceRow> rows, string currency, string? source, bool indicative)
    {
        var o = new JsonObject { ["currency"] = currency, ["rows"] = new JsonArray(rows.Select(r => (JsonNode)r.ToJson()).ToArray()) };
        if (source != null) o["source"] = source;
        if (indicative) o["indicative"] = true;
        return o;
    }

    // ------------------------------------------------------------------ reading a file

    /// <summary>A file's table: a workbook's first sheet, or CSV (semicolons, commas or tabs; a decimal comma allowed).</summary>
    public static List<List<string>> Table(byte[] bytes)
    {
        if (XlsxReader.IsZip(bytes))
        {
            try { return XlsxReader.Read(bytes); }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException) { throw new CommandException("The workbook could not be read: " + ex.Message); }
        }
        return Csv(Encoding.UTF8.GetString(bytes).TrimStart('﻿'));
    }

    /// <summary>Lower case, without diacritics or dots, spaces single: how headings and words are compared.</summary>
    public static string Fold(string s)
    {
        // Letter by letter (the engine runs without ICU in the browser, where Normalize does not take diacritics apart).
        var sb = new StringBuilder();
        foreach (var ch in s.Trim().Trim('"').ToLowerInvariant())
            sb.Append(ch switch
            {
                'ă' or 'â' or 'á' or 'à' or 'ä' or 'ã' or 'å' or 'ą' => "a",
                'î' or 'í' or 'ì' or 'ï' => "i",
                'ș' or 'ş' or 'ś' or 'š' => "s",
                'ț' or 'ţ' => "t",
                'é' or 'è' or 'ë' or 'ê' or 'ę' => "e",
                'ó' or 'ò' or 'ö' or 'ô' or 'õ' or 'ø' => "o",
                'ú' or 'ù' or 'ü' or 'û' => "u",
                'ç' or 'ć' or 'č' => "c",
                'ñ' or 'ń' => "n",
                'ł' => "l",
                'ź' or 'ż' or 'ž' => "z",
                'æ' => "ae",
                'ß' => "ss",
                '.' or '_' => " ",
                _ => ch.ToString(),
            });
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>The headings each column is known by, in English and Romanian (folded: no diacritics, no dots).</summary>
    static readonly Dictionary<string, string[]> Heads = new()
    {
        ["code"] = ["code", "cod", "simbol", "symbol", "cod articol", "simbol articol", "articol", "article", "article code", "item code", "id"],
        ["name"] = ["name", "description", "denumire", "descriere", "denumire articol", "denumirea lucrarii", "denumire lucrare", "lucrare", "item", "work", "text"],
        ["unit"] = ["unit", "um", "u m", "unitate", "unitatea", "unitate de masura", "uom", "unit of measure"],
        ["material"] = ["material", "materials", "materiale", "pret material", "material price", "mat"],
        ["labour"] = ["labour", "labor", "manopera", "pret manopera", "man", "labour price"],
        ["equipment"] = ["equipment", "plant", "utilaj", "utilaje", "pret utilaj", "util", "machinery"],
        ["transport"] = ["transport", "transp", "pret transport", "haulage"],
        ["price"] = ["price", "unit price", "rate", "pret", "pret unitar", "pret total", "total", "valoare unitara", "cost"],
        ["hours"] = ["hours", "man-hours", "man hours", "manhours", "ore", "ore-om", "ore om", "norma de timp", "norma timp", "ore/um", "h/unit"],
        ["trade"] = ["trade", "meserie", "meseria", "specialitate", "crew"],
        ["article"] = ["parent", "parent article", "parent code", "articol parinte", "cod articol parinte", "for article", "pentru articol"],
        ["kind"] = ["kind", "resource kind", "tip", "tip resursa", "type", "categorie", "category"],
        ["consumption"] = ["consumption", "consum", "consumul", "norma de consum", "quantity per unit", "cantitate pe um"],
    };

    /// <summary>
    /// Where the headings are and which column is which: the first row (of the first twenty) naming a code and a price or
    /// a split column; each field the first column whose heading it knows (null when none). "article" is taken only when
    /// the code is another column, a resource's parent.
    /// </summary>
    public static (int Header, Dictionary<string, int?> Map) Guess(List<List<string>> table)
    {
        for (var i = 0; i < Math.Min(20, table.Count); i++)
        {
            var heads = table[i].Select(Fold).ToList();
            var map = new Dictionary<string, int?>();
            var used = new HashSet<int>();
            // Exact headings first (the code before an article column called „Cod articol”), then those that start with one.
            foreach (var exact in new[] { true, false })
                foreach (var f in Fields)
                {
                    if (map.GetValueOrDefault(f) != null) continue;
                    for (var at = 0; at < heads.Count; at++)
                    {
                        var h = heads[at];
                        if (used.Contains(at) || !Heads[f].Any(k => exact ? h == k : h.StartsWith(k + " ", StringComparison.Ordinal) || h.StartsWith(k + "(", StringComparison.Ordinal))) continue;
                        map[f] = at; used.Add(at);
                        break;
                    }
                }
            foreach (var f in Fields) map.TryAdd(f, null);
            var priced = new[] { "material", "labour", "equipment", "transport", "price" }.Any(f => map[f] != null);
            if (map["code"] != null && priced) return (i, map);
        }
        return (-1, Fields.ToDictionary(f => f, _ => (int?)null));
    }

    /// <summary>A resource's kind from what a table writes (M, Man, U, T; material, labour, equipment, transport…); null when it is none.</summary>
    public static string? KindOf(string? s)
    {
        var k = Fold(s ?? "");
        return k switch
        {
            "m" or "mat" or "material" or "materiale" or "materials" => "material",
            "man" or "manopera" or "l" or "labour" or "labor" or "mo" => "labour",
            "u" or "util" or "utilaj" or "utilaje" or "e" or "equipment" or "plant" => "equipment",
            "t" or "transp" or "transport" => "transport",
            _ => null,
        };
    }

    public sealed record ImportResult(List<PriceRow> Rows, int Skipped, List<string> Notes);

    /// <summary>
    /// The rows of a table under the columns given (<paramref name="map"/>: field → column), the headings in row
    /// <paramref name="header"/>. A row needs a code and a price (a split or a total); a total alone counts as material,
    /// said in a note. A row naming an article is a resource of it: its kind and consumption, its price in its kind's
    /// column (or the total column).
    /// </summary>
    public static ImportResult Import(List<List<string>> table, int header, IReadOnlyDictionary<string, int?> map)
    {
        if (map.GetValueOrDefault("code") is not int code) throw new CommandException("Say which column holds the code.");
        string Cell(List<string> r, string f) => map.GetValueOrDefault(f) is int i && i >= 0 && i < r.Count ? r[i].Trim() : "";
        double? Num(List<string> r, string f) => Cell(r, f) is { Length: > 0 } c ? XlsxReader.Number(c) : null;
        var rows = new List<PriceRow>();
        var notes = new List<string>();
        var skipped = 0;
        var totalOnly = 0;
        foreach (var r in table.Skip(header + 1))
        {
            if (r.All(string.IsNullOrWhiteSpace)) continue;
            var c = code < r.Count ? r[code].Trim() : "";
            double? m = Num(r, "material"), l = Num(r, "labour"), e = Num(r, "equipment"), t = Num(r, "transport"), p = Num(r, "price");
            var article = Cell(r, "article") is { Length: > 0 } a && !string.Equals(a, c, StringComparison.OrdinalIgnoreCase) ? a : null;
            // A row needs a price; an article with a unit and none is priced by its resources (rows naming it).
            if (c.Length == 0 || m == null && l == null && e == null && t == null && p == null && article == null && Cell(r, "unit").Length == 0) { skipped++; continue; }
            if (article != null)
            {
                var kind = KindOf(Cell(r, "kind")) ?? (l != null && m == null ? "labour" : e != null && m == null ? "equipment" : t != null && m == null ? "transport" : "material");
                var price = kind switch { "labour" => l, "equipment" => e, "transport" => t, _ => m } ?? p ?? 0;
                rows.Add(new PriceRow(c, Cell(r, "name"), Cell(r, "unit"), kind == "material" ? price : 0, kind == "labour" ? price : 0, kind == "equipment" ? price : 0,
                    kind == "transport" ? price : 0, Num(r, "hours"), Cell(r, "trade") is { Length: > 0 } tr ? tr : null, article, kind, Num(r, "consumption") ?? 1));
                continue;
            }
            if (m == null && l == null && e == null && t == null && p != null) { totalOnly++; m = p; }
            rows.Add(new PriceRow(c, Cell(r, "name"), Cell(r, "unit"), m ?? 0, l ?? 0, e ?? 0, t ?? 0, Num(r, "hours"), Cell(r, "trade") is { Length: > 0 } tr2 ? tr2 : null));
        }
        if (rows.Count == 0) throw new CommandException("No price could be read from the table: each row needs a code and a price.");
        if (totalOnly > 0) notes.Add($"{totalOnly} row(s) have only a total price: it counts as material.");
        var units = rows.Where(r => !r.IsResource && r.Unit.Length > 0 && Units.BasisOf(r.Unit) == null).Select(r => r.Unit).Distinct().Take(5).ToList();
        if (units.Count > 0) notes.Add($"Lapin does not measure in {string.Join(", ", units.Select(u => $"„{u}”"))}: those articles count pieces unless their measure is chosen.");
        return new(rows, skipped, notes);
    }

    /// <summary>CSV split into rows and cells: the separator the first line uses most (; , or a tab), quotes honoured.</summary>
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
            else if (ch is '\n' or '\r')
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

    // ------------------------------------------------------------------ the template

    /// <summary>
    /// Lapin's own rough prices, in RON, not taken from any catalogue: orders of magnitude to try the estimate with and to
    /// show the columns, to be replaced by the user's prices. Every name says it is indicative.
    /// </summary>
    public static readonly IReadOnlyList<PriceRow> Indicative =
    [
        new("LAP-EXC", "Excavation, by machine", "m3", 0, 8, 22, 6, 0.1, "Labourer"),
        new("LAP-FDN", "Concrete in foundations", "m3", 420, 90, 20, 18, 3.5, "Concreter"),
        new("LAP-RC", "Reinforced concrete in columns, beams and floors", "m3", 480, 140, 25, 20, 6, "Concreter"),
        new("LAP-FORM", "Formwork, plywood", "m2", 35, 48, 4, 2, 1.2, "Carpenter"),
        new("LAP-MAS", "Masonry, blocks", "m3", 380, 85, 6, 14, 4, "Mason"),
        new("LAP-INS", "Thermal insulation, boards", "m2", 75, 30, 1, 3, 0.6, "Insulation fitter"),
        new("LAP-PLA", "Plaster", "m2", 18, 22, 1, 1, 0.5, "Plasterer"),
        new("LAP-PNT", "Paint, two coats", "m2", 9, 12, 0, 0, 0.25, "Painter"),
        new("LAP-SCR", "Floor screed", "m2", 28, 18, 2, 2, 0.35, "Mason"),
        new("LAP-TIL", "Tiles", "m2", 70, 45, 1, 2, 0.9, "Tiler"),
        new("LAP-ROOF", "Roof covering", "m2", 95, 40, 2, 4, 0.8, "Roofer"),
        new("LAP-WIN", "Window, fitted", "m2", 650, 80, 2, 10, 1.5, "Joiner"),
        new("LAP-DOOR", "Door, fitted", "pcs", 900, 120, 0, 15, 3, "Joiner"),
        new("LAP-STEEL", "Structural steel, erected", "kg", 6.5, 2.2, 0.6, 0.3, 0.03, "Steel erector"),
        new("LAP-SCAF", "Scaffolding, facade", "m2", 4, 6, 1, 1, 0.2, "Scaffolder"),
    ];

    /// <summary>The template: the columns and Lapin's indicative rows, as CSV (semicolons, a decimal point), each row saying it is indicative.</summary>
    public static string TemplateCsv()
    {
        static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        var sb = new StringBuilder("code;name;unit;material;labour;equipment;transport;hours;trade\n");
        foreach (var r in Indicative)
            sb.Append($"{r.Code};{r.Name} (indicative, Lapin's rough value: replace it with your price);{r.Unit};{N(r.Material)};{N(r.Labour)};{N(r.Equipment)};{N(r.Transport)};{N(r.Hours ?? 0)};{r.Trade}\n");
        return sb.ToString();
    }

    /// <summary>The indicative rows with their names marked, as the project keeps them when the template is taken as a list.</summary>
    public static List<PriceRow> IndicativeRows() => Indicative.Select(r => r with { Name = r.Name + " (indicative)" }).ToList();
}
