using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A column a table target reads (docs/plan-user-extensibility.md §7.1): its key, label, kind (text, number, length, force,
/// areaLoad, lineLoad, load — a value whose unit follows the row's kind —, choice, bool), whether a row needs it, the headings it
/// is known by (any language; matched without case, accents, units or punctuation) and, for a choice, its values.
/// </summary>
/// <see cref="Option"/>: a choice for the whole table (the dialog asks it, its default applies), not a heading of the file: no guess, no template column.
public sealed record TableColumn(string Key, string Label, string Kind = "text", bool Required = false, string[]? Aliases = null, string[]? Choices = null, string? Help = null, bool Option = false)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["key"] = Key, ["label"] = Label, ["kind"] = Kind, ["required"] = Required };
        if (Option) o["option"] = true;
        if (Choices != null) o["choices"] = new JsonArray(Choices.Select(c => (JsonNode)c).ToArray());
        if (Help != null) o["help"] = Help;
        if (TableUnits.Of(Kind) is { Count: > 0 } units) o["units"] = new JsonArray(units.Select(u => (JsonNode)u.Name).ToArray());
        return o;
    }
}

/// <summary>
/// A step an imported row makes: a command of the catalogue and its arguments, and what it does in words (the preview). <see cref="Tally"/>:
/// what the row is, counted in the preview (a material matched, new, skipped). A <see cref="Note"/> runs nothing: it only says what the row does not do.
/// </summary>
public sealed record TableStep(string Command, JsonObject Args, string Describe, string? Tally = null)
{
    public static TableStep Note(string describe, string? tally = null) => new("", [], describe, tally);
    public bool IsNote => Command.Length == 0;
}

/// <summary>
/// A target of the table import, registered by its module as a service: its columns, and how a row becomes steps (commands, one
/// undo step for the whole table). <see cref="Open"/>: the columns no key names are the target's too (a property each).
/// A row that cannot be read throws <see cref="CommandException"/> with why: the preview lists it, the import leaves it out.
/// </summary>
public sealed record TableTarget(string Id, string Label, string Module, IReadOnlyList<TableColumn> Columns, Func<IQueryContext, TableRow, IReadOnlyList<TableStep>> Steps,
    bool Open = false, string? Description = null, IReadOnlyList<string[]>? Example = null)
{
    /// <summary>On an open target: where a heading of its own goes, in words, or why it cannot (the preview lists the columns).</summary>
    public Func<IQueryContext, string, (string Target, string? Error)>? OpenColumn { get; init; }
}

/// <summary>A row as a target reads it: its cells by column key in the units chosen, the open columns by their heading, its number in the file.</summary>
public sealed class TableRow(int number, IReadOnlyDictionary<string, string> cells, IReadOnlyDictionary<string, string> units, IReadOnlyList<(string Heading, string Value)> open, Dictionary<string, object>? shared = null)
{
    public int Number { get; } = number;
    /// <summary>What the rows of one table share while it is read (a load case one row makes and the next ones use).</summary>
    public Dictionary<string, object> Shared { get; } = shared ?? [];
    public IReadOnlyList<(string Heading, string Value)> OpenCells { get; } = open;

    public bool Has(string key) => cells.TryGetValue(key, out var v) && v.Trim().Length > 0;
    public string? Text(string key) => cells.TryGetValue(key, out var v) && v.Trim().Length > 0 ? v.Trim() : null;
    public string Need(string key, string label) => Text(key) ?? throw new CommandException($"{label} is empty.");
    public string? Unit(string key) => units.TryGetValue(key, out var u) ? u : null;

    /// <summary>A number cell in base units (m, kN, kN/m, kN/m²), its column's unit (<paramref name="kind"/>'s, else the column's) applied.</summary>
    public double? Num(string key, string? kind = null)
    {
        if (Text(key) is not { } t) return null;
        // A unit written in the cell itself („250 mm”) over the column's.
        var (n, own) = TableUnits.Split(t) ?? throw new CommandException($"„{t}” is not a number.");
        var unit = own ?? Unit(key);
        return kind != null || unit != null ? n * TableUnits.Factor(kind, unit) : n;
    }
}

/// <summary>Units a column's numbers are written in, each with its factor to the base unit (m, kN, kN/m, kN/m²).</summary>
public static class TableUnits
{
    public sealed record Unit(string Name, double Factor, string[] Spellings);

    static readonly Dictionary<string, Unit[]> Kinds = new()
    {
        ["length"] = [new("m", 1, ["m", "metri", "meter", "metres", "meters"]), new("cm", 0.01, ["cm"]), new("mm", 0.001, ["mm"]), new("ft", 0.3048, ["ft", "feet"]), new("in", 0.0254, ["in", "inch"])],
        ["force"] = [new("kN", 1, ["kn"]), new("N", 0.001, ["n"]), new("daN", 0.01, ["dan"]), new("kgf", 0.00980665, ["kgf", "kg"]), new("tf", 9.80665, ["tf", "t"]), new("kip", 4.4482216, ["kip", "kips"])],
        ["lineLoad"] = [new("kN/m", 1, ["kn/m", "knm-1"]), new("N/m", 0.001, ["n/m"]), new("daN/m", 0.01, ["dan/m"]), new("kgf/m", 0.00980665, ["kgf/m", "kg/m"]), new("tf/m", 9.80665, ["tf/m", "t/m"]), new("plf", 0.0145939, ["plf", "lb/ft"]), new("klf", 14.5939, ["klf", "kip/ft"])],
        ["areaLoad"] = [new("kN/m²", 1, ["kn/m2", "kn/m²", "kpa"]), new("N/m²", 0.001, ["n/m2", "n/m²", "pa"]), new("daN/m²", 0.01, ["dan/m2", "dan/m²"]), new("kgf/m²", 0.00980665, ["kgf/m2", "kgf/m²", "kg/m2", "kg/m²"]), new("tf/m²", 9.80665, ["tf/m2", "t/m2", "tf/m²"]), new("psf", 0.04788026, ["psf", "lb/ft2"]), new("ksf", 47.88026, ["ksf"])],
    };

    /// <summary>A kind's units; <c>load</c>: a force's, per metre and per square metre (a row's kind says which).</summary>
    public static IReadOnlyList<Unit> Of(string kind) => kind == "load" ? [.. Kinds["force"], .. Kinds["lineLoad"], .. Kinds["areaLoad"]] : Kinds.TryGetValue(kind, out var u) ? u : [];

    /// <summary>The factor of <paramref name="unit"/> (a name or a spelling) in <paramref name="kind"/>; any unit of any kind when no kind is given; 1 when unknown.</summary>
    public static double Factor(string? kind, string? unit)
    {
        if (unit == null) return 1;
        var u = Norm(unit);
        foreach (var (k, list) in Kinds)
        {
            if (kind != null && k != kind) continue;
            if (list.FirstOrDefault(x => Norm(x.Name) == u || x.Spellings.Contains(u)) is { } found) return found.Factor;
        }
        return 1;
    }

    /// <summary>The kind a unit belongs to (a row's own unit says whether its value is a force, per metre or per square metre).</summary>
    public static (string Kind, Unit Unit)? Find(string text)
    {
        var u = Norm(text);
        foreach (var (k, list) in Kinds)
            if (list.FirstOrDefault(x => Norm(x.Name) == u || x.Spellings.Contains(u)) is { } found) return (k, found);
        return null;
    }

    static string Norm(string s) => s.Trim().ToLowerInvariant().Replace(" ", "").Replace("^2", "2").Replace("²", "2").Replace("·", "");

    /// <summary>The unit a heading names in brackets or after its last space (<c>Length (mm)</c>, <c>q [kN/m2]</c>, <c>x m</c>) within <paramref name="kind"/>.</summary>
    public static string? FromHeading(string heading, string kind)
    {
        var list = Of(kind);
        if (list.Count == 0) return null;
        var candidates = new List<string>();
        var open = heading.IndexOfAny(['(', '[']);
        if (open >= 0)
        {
            var close = heading.IndexOfAny([')', ']'], open);
            if (close > open) candidates.Add(heading[(open + 1)..close]);
        }
        var parts = heading.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1) candidates.Add(parts[^1]);
        foreach (var c in candidates)
        {
            var u = Norm(c);
            if (list.FirstOrDefault(x => Norm(x.Name) == u || x.Spellings.Contains(u)) is { } found) return found.Name;
        }
        return null;
    }

    /// <summary>A cell that holds a number and its unit („4.5 kN/m”, „250mm”, „1,2 kPa”): the number and the unit; no unit, null.</summary>
    public static (double Value, string? Unit)? Split(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text.Trim(), @"^([-+−]?[\d.,' \u00a0\u202f]*\d(?:[eE][-+]?\d+)?)\s*([^\d\s].*)?$");
        if (!m.Success || Parse(m.Groups[1].Value) is not { } n) return null;
        var unit = m.Groups[2].Success ? m.Groups[2].Value.Trim() : null;
        return (n, unit is { Length: > 0 } ? unit : null);
    }

    /// <summary>A number as people write it: a dot or a comma for decimals, spaces or the other mark grouping thousands, a minus of any kind.</summary>
    public static double? Parse(string text) => Numbers.Parse(text, NumberMode.Grouped);
}

/// <summary>
/// The table import (docs/plan-user-extensibility.md §7.1): one way into the model for every table a user has — loads, property
/// values, load cases — read from CSV or a workbook, the columns guessed from their headings and confirmed by the user, a preview
/// that says row by row what will be made or changed and what cannot be read, then the whole table as one undo step through the
/// modules' own commands (each keeps its rules).
/// </summary>
public static class TableImport
{
    public const int PreviewRows = 200;
    public const int MaxRows = 50_000;

    public static IEnumerable<TableTarget> Targets(IServices s) => s.All<TableTarget>();

    static TableTarget Target(IServices s, string? id) =>
        Targets(s).FirstOrDefault(t => t.Id == id) ?? throw new CommandException($"There is no table import „{id}” in this edition.");

    /// <summary>The rows of a file (base64 <c>data</c>, a workbook or CSV) or of a <c>text</c> (CSV, or cells pasted from a spreadsheet).</summary>
    public static List<List<string>> Rows(JsonObject a)
    {
        List<List<string>> rows;
        if ((string?)a["data"] is { Length: > 0 } data)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(data); } catch (FormatException) { throw new CommandException("The file is not base64."); }
            rows = Tables.Read(bytes);
        }
        else rows = Tables.Csv(((string?)a["text"] ?? "").TrimStart('﻿'));
        // Trailing empty rows and cells left out.
        rows = rows.Select(r => { var c = r.ToList(); while (c.Count > 0 && c[^1].Trim().Length == 0) c.RemoveAt(c.Count - 1); return c; }).ToList();
        while (rows.Count > 0 && rows[^1].Count == 0) rows.RemoveAt(rows.Count - 1);
        if (rows.Count > MaxRows) throw new CommandException($"A table has at most {MaxRows} rows.");
        return rows;
    }

    /// <summary>The row most like a heading among the first ten: the most cells that are text, not numbers.</summary>
    public static int HeaderRow(List<List<string>> rows) =>
        rows.Take(10).Select((r, i) => (i, Score: r.Count(c => c.Trim().Length > 0 && TableUnits.Parse(c) == null))).OrderByDescending(x => x.Score).ThenBy(x => x.i).FirstOrDefault().i;

    /// <summary>A heading as compared: lower case, no accents, units in brackets and punctuation left out.</summary>
    public static string Norm(string s)
    {
        var open = s.IndexOfAny(['(', '[']);
        if (open > 0) s = s[..open];
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    /// <summary>The columns a heading row maps to: each heading to the target's column it names (its key, label or an alias), its unit from the heading.</summary>
    public static List<(int Index, string? Key, string? Unit)> Guess(TableTarget t, IReadOnlyList<string> header)
    {
        var used = new HashSet<string>();
        var o = new List<(int, string?, string?)>();
        for (var i = 0; i < header.Count; i++)
        {
            var h = Norm(header[i]);
            var col = h.Length == 0 ? null : t.Columns.FirstOrDefault(c => !c.Option && !used.Contains(c.Key) && (Norm(c.Key) == h || Norm(c.Label) == h || (c.Aliases ?? []).Any(al => Norm(al) == h)));
            if (col != null) used.Add(col.Key);
            o.Add((i, col?.Key, col != null ? TableUnits.FromHeading(header[i], col.Kind) : null));
        }
        return o;
    }

    /// <summary>The mapping a command gives: [{column, key, unit?}] (key null or "": left out; on an open target, "open": a column of its own).</summary>
    static List<(int Index, string Key, string? Unit)> Mapping(TableTarget t, JsonObject a, IReadOnlyList<string> header)
    {
        // None given: the guess, and on an open target every other heading a column of its own.
        if (a["mapping"] is not JsonArray arr)
            return [.. Guess(t, header).Select(g => (g.Index, Key: g.Key ?? (t.Open && header[g.Index].Trim().Length > 0 ? "open" : null), g.Unit)).Where(g => g.Key != null).Select(g => (g.Index, g.Key!, g.Unit))];
        var o = new List<(int, string, string?)>();
        foreach (var n in arr.OfType<JsonObject>())
        {
            if (Json.Number(n["column"]) is not { } ci || ci < 0) throw new CommandException("Each mapping names its column (0, 1, …).");
            var key = (string?)n["key"];
            if (string.IsNullOrEmpty(key)) continue;
            if (key != "open" && t.Columns.All(c => c.Key != key)) throw new CommandException($"„{key}” is not a column of {t.Label}.");
            if (key == "open" && !t.Open) throw new CommandException($"{t.Label} takes only its own columns.");
            o.Add(((int)ci, key, (string?)n["unit"] is { Length: > 0 } u ? u : null));
        }
        return o;
    }

    /// <summary>Each data row of the table read into its steps, or why not: (row number in the file, its steps, its error).</summary>
    public static List<(int Row, IReadOnlyList<TableStep> Steps, string? Error)> Plan(IQueryContext q, TableTarget t, JsonObject a)
    {
        var rows = Rows(a);
        if (rows.Count == 0) throw new CommandException("The table is empty.");
        var headerRow = Json.Number(a["headerRow"]) is { } hr ? Math.Clamp((int)hr, 0, rows.Count - 1) : HeaderRow(rows);
        var header = rows[headerRow];
        var mapping = Mapping(t, a, header);
        var defaults = a["defaults"] as JsonObject;
        var missing = t.Columns.Where(c => c.Required && mapping.All(m => m.Key != c.Key) && defaults?[c.Key] == null).ToList();
        if (missing.Count > 0) throw new CommandException($"{t.Label} needs a column for {string.Join(", ", missing.Select(c => c.Label))}.");
        var o = new List<(int, IReadOnlyList<TableStep>, string?)>();
        var shared = new Dictionary<string, object>();
        for (var r = headerRow + 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.All(c => c.Trim().Length == 0)) continue;
            var cells = new Dictionary<string, string>();
            var units = new Dictionary<string, string>();
            var open = new List<(string, string)>();
            foreach (var (k, v) in defaults ?? []) if (v != null) cells[k] = v.ToString();
            foreach (var (ci, key, unit) in mapping)
            {
                var value = ci < row.Count ? row[ci] : "";
                if (key == "open") { if (value.Trim().Length > 0 && ci < header.Count) open.Add((header[ci].Trim(), value.Trim())); continue; }
                if (value.Trim().Length > 0 || !cells.ContainsKey(key)) cells[key] = value;
                if (unit != null) units[key] = unit;
            }
            try
            {
                foreach (var c in t.Columns.Where(c => c.Required))
                    if (!cells.TryGetValue(c.Key, out var v) || v.Trim().Length == 0) throw new CommandException($"{c.Label} is empty.");
                foreach (var c in t.Columns.Where(c => c.Kind == "choice" && c.Choices != null))
                    if (cells.TryGetValue(c.Key, out var v) && v.Trim().Length > 0 && Choice(c, v) == null) throw new CommandException($"„{v.Trim()}” is not one of {string.Join(", ", c.Choices!)} ({c.Label}).");
                o.Add((r + 1, t.Steps(q, new TableRow(r + 1, cells, units, open, shared)), null));
            }
            catch (CommandException ex) { o.Add((r + 1, [], ex.Message)); }
        }
        return o;
    }

    /// <summary>The headings of the columns an open target takes as its own, as <see cref="Plan"/> reads them.</summary>
    public static List<string> OpenHeadings(TableTarget t, JsonObject a)
    {
        var rows = Rows(a);
        if (rows.Count == 0) return [];
        var header = rows[Json.Number(a["headerRow"]) is { } hr ? Math.Clamp((int)hr, 0, rows.Count - 1) : HeaderRow(rows)];
        return [.. Mapping(t, a, header).Where(m => m.Key == "open" && m.Index < header.Count && header[m.Index].Trim().Length > 0).Select(m => header[m.Index].Trim())];
    }

    /// <summary>A choice cell as one of its column's values: the value, its words without case or accents.</summary>
    public static string? Choice(TableColumn c, string value)
    {
        var n = Norm(value);
        return c.Choices?.FirstOrDefault(x => Norm(x) == n);
    }

    /// <summary>A template: the target's headings and its example rows, as CSV with semicolons.</summary>
    public static string Template(TableTarget t)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CsvText.Line(t.Columns.Where(c => !c.Option).Select(c => TableUnits.Of(c.Kind) is { Count: > 0 } u ? $"{c.Label} ({u[0].Name})" : c.Label)));
        foreach (var ex in t.Example ?? []) sb.AppendLine(CsvText.Line(ex));
        return sb.ToString();
    }

    public static void Register(IModuleRegistry r)
    {
        // The tables this edition can import, with their columns.
        r.Query("tables.targets", (q, _) => new JsonObject
        {
            ["targets"] = new JsonArray(Targets(q.Services).OrderBy(t => t.Label, StringComparer.OrdinalIgnoreCase).Select(t => (JsonNode)new JsonObject
            {
                ["id"] = t.Id, ["label"] = t.Label, ["module"] = t.Module, ["open"] = t.Open, ["description"] = t.Description,
                ["columns"] = new JsonArray(t.Columns.Select(c => (JsonNode)c.ToJson()).ToArray()),
            }).ToArray()),
        });

        // A table read: its first rows, how many there are, the row that looks like the headings, and for a target the columns guessed.
        r.Query("tables.read", (q, a) =>
        {
            var rows = Rows(a);
            var header = rows.Count > 0 ? HeaderRow(rows) : 0;
            var o = new JsonObject
            {
                ["count"] = rows.Count, ["headerRow"] = header,
                ["rows"] = new JsonArray(rows.Take(PreviewRows).Select(r => (JsonNode)new JsonArray(r.Select(c => (JsonNode)c).ToArray())).ToArray()),
            };
            if ((string?)a["target"] is { Length: > 0 } tid && rows.Count > 0)
            {
                var t = Target(q.Services, tid);
                var hr = Json.Number(a["headerRow"]) is { } given ? Math.Clamp((int)given, 0, rows.Count - 1) : header;
                o["mapping"] = new JsonArray(Guess(t, rows[hr]).Select(g => (JsonNode)new JsonObject
                {
                    ["column"] = g.Index, ["key"] = g.Key ?? (t.Open && rows[hr][g.Index].Trim().Length > 0 ? "open" : null), ["unit"] = g.Unit,
                }).ToArray());
            }
            return o;
        });

        // What importing would do, row by row: its steps in words, or why the row cannot be read.
        r.Query("tables.preview", (q, a) =>
        {
            var t = Target(q.Services, (string?)a["target"]);
            var plan = Plan(q, t, a);
            var o = new JsonObject
            {
                ["rows"] = new JsonArray(plan.Take(PreviewRows * 5).Select(p => (JsonNode)new JsonObject
                {
                    ["row"] = p.Row, ["error"] = p.Error, ["steps"] = new JsonArray(p.Steps.Select(s => (JsonNode)s.Describe).ToArray()),
                }).ToArray()),
                ["ok"] = plan.Count(p => p.Error == null), ["errors"] = plan.Count(p => p.Error != null), ["steps"] = plan.Sum(p => p.Steps.Count(s => !s.IsNote)),
            };
            // What the rows are (a target that counts them: matched, new, skipped), each row by its first step that says.
            var tally = plan.Where(p => p.Error == null).Select(p => p.Steps.FirstOrDefault(s => s.Tally != null)?.Tally).OfType<string>()
                .GroupBy(x => x).ToList();
            if (tally.Count > 0) o["tally"] = new JsonObject(tally.Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count())));
            if (t.OpenColumn != null)
                o["columns"] = new JsonArray(OpenHeadings(t, a).Select(h =>
                {
                    var (target, error) = t.OpenColumn(q, h);
                    var c = new JsonObject { ["heading"] = h, ["target"] = target };
                    if (error != null) c["error"] = error;
                    return (JsonNode)c;
                }).ToArray());
            return o;
        });

        r.Query("tables.template", (q, a) =>
        {
            var t = Target(q.Services, (string?)a["target"]);
            return new JsonObject { ["text"] = Template(t), ["file"] = t.Id.Replace('.', '-') + "-template.csv" };
        });

        // The table imported, one undo step: each readable row's steps through the modules' commands. Rows that cannot be read
        // are refused with their numbers unless `skipErrors`; a step a command refuses stops the import, nothing kept.
        r.Command("tables.import", "Import table", (ctx, a) =>
        {
            var t = Target(ctx.Services, (string?)a["target"]);
            var plan = Plan(ctx.Query, t, a);
            var bad = plan.Where(p => p.Error != null).ToList();
            var skip = a["skipErrors"] is JsonValue sv && sv.TryGetValue<bool>(out var s) && s;
            if (bad.Count > 0 && !skip)
                throw new CommandException($"{bad.Count} row{(bad.Count == 1 ? "" : "s")} cannot be read: " + string.Join("; ", bad.Take(5).Select(b => $"row {b.Row}: {b.Error}")) + (bad.Count > 5 ? "…" : ""));
            var catalog = ctx.Services as ModuleCatalog ?? throw new CommandException("No commands to import through here.");
            var made = new JsonArray();
            var count = 0;
            foreach (var (row, steps, error) in plan)
            {
                if (error != null) continue;
                foreach (var step in steps)
                {
                    if (step.IsNote) continue;
                    var entry = catalog.Command(step.Command) ?? throw new CommandException($"Row {row}: this edition has no „{step.Command}”.");
                    JsonNode? result;
                    try { result = entry.Handler(ctx, step.Args); }
                    catch (CommandException ex) { throw new CommandException($"Row {row}: {ex.Message}"); }
                    if ((string?)result?["id"] is { } id) made.Add((JsonNode)id);
                    count++;
                }
            }
            return new JsonObject { ["steps"] = count, ["rows"] = plan.Count(p => p.Error == null), ["skipped"] = bad.Count, ["ids"] = made };
        });
    }
}
