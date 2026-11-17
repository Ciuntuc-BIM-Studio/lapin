using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// The edits of a drafting table (docs/plan-drafting-tables.md §4), each an op of <c>drafting.editTable</c>, all of one call
/// one undo step: a cell's text (or a block of them, pasted), a range's style, borders, merge and split, rows and columns
/// put in or taken out (the formulas of every cell following, as a spreadsheet's do), widths, heights and fitting to text,
/// the table's own style and its preset.
/// </summary>
public static class TableEdits
{
    public static readonly string[] Ops =
        ["set", "style", "borders", "merge", "split", "insertRows", "deleteRows", "insertCols", "deleteCols", "colWidth", "rowHeight", "fit", "tableStyle", "preset", "header", "link", "linkRows", "parts", "load", "autoSum"];

    public static readonly string[] BorderKinds = ["all", "outer", "inner", "top", "bottom", "left", "right", "none", "default"];

    static readonly string[] CellStyleKeys = ["fill", "color", "bold", "italic", "size", "align", "valign", "pad", "rot"];
    static readonly string[] TableStyleKeys = ["size", "pad", "color", "fill", "align", "valign", "grid", "outline"];
    static readonly string[] Sides = ["top", "bottom", "left", "right"];

    static JsonArray Rows(JsonObject t) => (JsonArray)t["rows"]!;
    static JsonArray Cols(JsonObject t) => (JsonArray)t["cols"]!;
    static JsonArray CellsOf(JsonObject t, int r) => (JsonArray)((JsonObject)Rows(t)[r]!)["cells"]!;

    /// <summary>A table's JSON with every row holding as many cells as there are columns (a short row filled, a long one cut).</summary>
    public static JsonObject Normal(JsonObject? table)
    {
        var t = table?.DeepClone() as JsonObject ?? DraftingTables.New(1, 1, 25, 7, "grid");
        if (t["cols"] is not JsonArray { Count: > 0 }) t["cols"] = new JsonArray(new JsonObject { ["w"] = 25 });
        if (t["rows"] is not JsonArray { Count: > 0 }) t["rows"] = new JsonArray(new JsonObject { ["h"] = 7 });
        if (t["style"] is not JsonObject) t["style"] = new JsonObject();
        var n = Cols(t).Count;
        foreach (var rn in Rows(t))
        {
            var row = (JsonObject)rn!;
            if (row["cells"] is not JsonArray cells) row["cells"] = cells = [];
            while (cells.Count < n) cells.Add(null);
            while (cells.Count > n) cells.RemoveAt(cells.Count - 1);
        }
        return t;
    }

    static (int R0, int C0, int R1, int C1) RangeOf(JsonObject op, DraftingTables.Grid g, string key = "range")
    {
        var s = (string?)op[key] ?? (string?)op["cell"] ?? throw new CommandException("Which cells? A range such as A1:C3, or a cell such as B2.");
        var b = DraftingTables.ParseRange(s) ?? throw new CommandException($"„{s}” is not a cell or a range (A1, A1:C3).");
        if (b.R1 >= g.R || b.C1 >= g.C) throw new CommandException($"„{s}” is outside the table ({DraftingTables.Address(g.R - 1, g.C - 1)} is its last cell).");
        return b;
    }

    static int Whole(JsonNode? n, string what)
    {
        var v = Json.Number(n) ?? throw new CommandException($"{what}?");
        return v == Math.Floor(v) && v >= 0 && v <= 100_000 ? (int)v : throw new CommandException($"{what} is a whole number.");
    }

    static JsonObject Own(JsonObject t, int r, int c)
    {
        var cells = CellsOf(t, r);
        if (cells[c] is JsonObject o) return o;
        o = [];
        cells[c] = o;
        return o;
    }

    /// <summary>An emptied cell is no cell (null): a table keeps only what is in its cells.</summary>
    static void Tidy(JsonObject t, int r, int c)
    {
        if (CellsOf(t, r)[c] is JsonObject { Count: 0 }) CellsOf(t, r)[c] = null;
    }

    static string Colour(JsonNode? n, string what) =>
        (string?)n is { } s && HexColor.TryParse(s, out _, out _, out _) ? s.Trim() : throw new CommandException($"{what} is a colour (#rrggbb, or #rrggbbaa with its opacity).");

    static JsonNode? StyleValue(string k, JsonNode? v)
    {
        if (v == null) return null;
        return k switch
        {
            "fill" or "color" => Colour(v, k == "fill" ? "A background" : "A text's colour"),
            "bold" or "italic" => v is JsonValue b && b.TryGetValue<bool>(out var x) ? x : throw new CommandException($"{k} is true or false."),
            "size" => Json.Number(v) is double s && s is >= 0.5 and <= 50 ? s : throw new CommandException("A text's height is 0.5 to 50 mm."),
            "pad" => Json.Number(v) is double p && p is >= 0 and <= 50 ? p : throw new CommandException("A cell's padding is 0 to 50 mm."),
            "rot" => Json.Number(v) is double r && DraftingTables.Rotations.Contains((int)r) && r == Math.Floor(r) ? (r == 0 ? null : (int)r) : throw new CommandException("A cell's text turns 0, 90 or 270°."),
            "align" => (string?)v is { } a && DraftingTables.Aligns.Contains(a) ? a : throw new CommandException($"A text aligns {string.Join(", ", DraftingTables.Aligns)}."),
            "valign" => (string?)v is { } a && DraftingTables.VAligns.Contains(a) ? a : throw new CommandException($"A text stands {string.Join(", ", DraftingTables.VAligns)}."),
            "grid" or "outline" => SideValue(v),
            _ => throw new CommandException($"„{k}” is not a style."),
        };
    }

    static JsonObject SideValue(JsonNode? v)
    {
        if (v is not JsonObject o) throw new CommandException("A line is {w, c}: its width (mm, 0: none) and colour.");
        var w = Json.Number(o["w"]) is double x && x is >= 0 and <= 5 ? x : throw new CommandException("A line's width is 0 to 5 mm.");
        return new JsonObject { ["w"] = w, ["c"] = o["c"] is null ? "#000000" : Colour(o["c"], "A line's colour") };
    }

    /// <summary>The ops applied in order to a table's JSON (a copy, returned): any that does not read refuses them all.</summary>
    public static JsonObject Apply(JsonObject? table, JsonArray ops, IReadOnlyList<TableStyleDef>? styles = null, Func<string, bool>? exists = null) => Apply(table, ops, null, styles, exists);

    static JsonObject Apply(JsonObject? table, JsonArray ops, List<Shifted>? shifts, IReadOnlyList<TableStyleDef>? styles, Func<string, bool>? exists)
    {
        var t = Normal(table);
        foreach (var n in ops)
        {
            var op = n as JsonObject ?? throw new CommandException("An edit of a table is {op, …}.");
            var name = (string?)op["op"] ?? throw new CommandException($"Which edit? One of {string.Join(", ", Ops)}.");
            var g = DraftingTables.Read(t);
            switch (name)
            {
                case "set": Set(t, g, op); break;
                case "style":
                {
                    var b = RangeOf(op, g);
                    var style = op["style"] as JsonObject ?? throw new CommandException("A style is {fill, color, bold, italic, size, align, valign}.");
                    foreach (var (k, v) in style)
                    {
                        if (!CellStyleKeys.Contains(k)) throw new CommandException($"„{k}” is not a cell's style ({string.Join(", ", CellStyleKeys)}).");
                        var value = StyleValue(k, v);
                        foreach (var (r, c) in Masters(g, b))
                        {
                            var o = Own(t, r, c);
                            if (value == null) o.Remove(k); else o[k] = value.DeepClone();
                            Tidy(t, r, c);
                        }
                    }
                    break;
                }
                case "borders": Borders(t, g, op); break;
                case "merge": Merge(t, g, RangeOf(op, g)); break;
                case "split":
                {
                    var b = RangeOf(op, g);
                    foreach (var (r, c) in Masters(g, b))
                    {
                        if (CellsOf(t, r)[c] is not JsonObject o) continue;
                        o.Remove("rs");
                        o.Remove("cs");
                        Tidy(t, r, c);
                    }
                    break;
                }
                case "insertRows" or "insertCols":
                {
                    var rows = name == "insertRows";
                    var at = Whole(op["at"], "Where");
                    var count = op["count"] is null ? 1 : Whole(op["count"], "How many");
                    Insert(t, g, rows, at, count);
                    shifts?.Add(new Shifted(rows, at, count, true));
                    break;
                }
                case "deleteRows" or "deleteCols":
                {
                    var rows = name == "deleteRows";
                    var at = Whole(op["at"], "Which");
                    var count = op["count"] is null ? 1 : Whole(op["count"], "How many");
                    Delete(t, g, rows, at, count);
                    shifts?.Add(new Shifted(rows, at, count, false));
                    break;
                }
                case "colWidth" or "rowHeight":
                {
                    var b = RangeOf(op, g);
                    var v = Json.Number(op[name == "colWidth" ? "w" : "h"]) is double x && x is > 0 and <= 1000 ? x : throw new CommandException("A width or a height is more than 0 and up to 1000 mm.");
                    var (list, from, to) = name == "colWidth" ? (Cols(t), b.C0, b.C1) : (Rows(t), b.R0, b.R1);
                    for (var i = from; i <= to; i++) { var o = (JsonObject)list[i]!; o[name == "colWidth" ? "w" : "h"] = v; o.Remove("fit"); }
                    break;
                }
                case "fit":
                {
                    var b = RangeOf(op, g);
                    var axis = (string?)op["axis"] ?? "rows";
                    if (axis is not ("rows" or "cols" or "both")) throw new CommandException("Fit rows, cols or both.");
                    var on = op["on"] is not JsonValue ov || !ov.TryGetValue<bool>(out var onb) || onb;
                    void Fit(JsonArray list, int from, int to)
                    {
                        for (var i = from; i <= to; i++)
                        {
                            var o = (JsonObject)list[i]!;
                            if (on) o["fit"] = true; else o.Remove("fit");
                            foreach (var k in new[] { "min", "max" })
                                if (op.ContainsKey(k))
                                {
                                    if (Json.Number(op[k]) is double v && v is > 0 and <= 1000) o[k] = v;
                                    else if (op[k] == null) o.Remove(k);
                                    else throw new CommandException("A min or a max is more than 0 and up to 1000 mm.");
                                }
                        }
                    }
                    if (axis is "rows" or "both") Fit(Rows(t), b.R0, b.R1);
                    if (axis is "cols" or "both") Fit(Cols(t), b.C0, b.C1);
                    break;
                }
                // The rows of a range marked header rows or not (stage 3): a style's header, the rows a long table repeats (stage 5).
                case "header":
                {
                    var b = RangeOf(op, g);
                    var on = op["on"] is not JsonValue hv || !hv.TryGetValue<bool>(out var hb) || hb;
                    for (var r = b.R0; r <= b.R1; r++) { var row = (JsonObject)Rows(t)[r]!; if (on) row["header"] = true; else row.Remove("header"); }
                    break;
                }
                // Cells, rows or the whole table linked to an element (stage 4: el.<field> reads it), or unlinked (el null).
                case "link":
                {
                    var el = Element(op["el"], exists);
                    var on = (string?)op["on"] ?? "cells";
                    if (on == "table") { if (el == null) t.Remove("el"); else t["el"] = el; break; }
                    var b = RangeOf(op, g);
                    if (on == "rows")
                        for (var r = b.R0; r <= b.R1; r++) { var row = (JsonObject)Rows(t)[r]!; if (el == null) row.Remove("el"); else row["el"] = el; }
                    else if (on == "cells")
                        foreach (var (r, c) in Masters(g, b)) { var o = Own(t, r, c); if (el == null) o.Remove("el"); else o["el"] = el; Tidy(t, r, c); }
                    else throw new CommandException("Link cells, rows or the table.");
                    break;
                }
                // Elements one a row from a row down (stage 4): the rows from it that are empty or linked already take them, then
                // rows go in before the first that holds a text of its own (a total, a note: kept under them, its sums taking the
                // rows in) or past the table's end; the first row's texts copied into the empty cells of the others, their
                // references moved down as a spreadsheet's fill does.
                case "linkRows":
                {
                    var at = DraftingTables.Parse((string?)op["at"] ?? (string?)op["cell"]) ?? throw new CommandException("From which row? A cell of it, such as A2.");
                    if (at.R >= g.R) throw new CommandException($"Row {at.R + 1} is past the table.");
                    var els = (op["els"] as JsonArray ?? throw new CommandException("Which elements? els: [ids].")).Select(x => Element(x, exists) ?? throw new CommandException("An element's id is missing.")).ToList();
                    if (els.Count == 0) throw new CommandException("Which elements? els: [ids].");
                    var stop = at.R + 1;
                    while (stop < g.R && (Rows(t)[stop] is JsonObject sr && sr["el"] != null || Enumerable.Range(0, g.C).All(c => g.Cells[stop, c].Text.Length == 0))) stop++;
                    var need = at.R + els.Count - stop;
                    if (need > 0) { Insert(t, g, rows: true, stop, need); shifts?.Add(new Shifted(true, stop, need, true)); }
                    var copy = op["copy"] is not JsonValue cv || !cv.TryGetValue<bool>(out var cb) || cb;
                    var first = CellsOf(t, at.R);
                    for (var k = 0; k < els.Count; k++)
                    {
                        var row = (JsonObject)Rows(t)[at.R + k]!;
                        row["el"] = els[k];
                        if (!copy || k == 0) continue;
                        var cells = CellsOf(t, at.R + k);
                        for (var c = 0; c < cells.Count && c < first.Count; c++)
                        {
                            var target = cells[c] as JsonObject;
                            if (first[c] is not JsonObject src || (string?)src["t"] is not { Length: > 0 } text || target != null && target.ContainsKey("t")) continue;
                            var down = k;
                            var moved = MapRefs(text, r => r.Table == null ? Write(null, (r.A.R + down, r.A.C), r.B is { } z ? (z.R + down, z.C) : null) : null);
                            if (target != null) { target["t"] = moved; continue; }
                            var o = (JsonObject)src.DeepClone();
                            o.Remove("rs"); o.Remove("cs"); o.Remove("el");
                            o["t"] = moved;
                            cells[c] = o;
                        }
                    }
                    break;
                }
                // A long table broken into parts (stage 5): at most `rows` body rows a part, or parts at most `height` mm tall, laid
                // `dir` (right, down) `gap` mm apart, each with the header rows again; off: one table.
                case "parts":
                {
                    if (op["off"] is JsonValue ov && ov.TryGetValue<bool>(out var off) && off) { t.Remove("split"); break; }
                    var sp = new JsonObject();
                    if (op["rows"] != null) sp["rows"] = Json.Number(op["rows"]) is double pr && pr >= 1 && pr <= DraftingTables.MaxRows && pr == Math.Floor(pr) ? (int)pr : throw new CommandException("A part holds 1 row or more.");
                    if (op["height"] != null) sp["height"] = Json.Number(op["height"]) is double ph && ph is > 0 and <= 5000 ? ph : throw new CommandException("A part's height is more than 0 and up to 5000 mm.");
                    if (sp.Count == 0) throw new CommandException("Break the table after how many rows (rows), or at what height (height, mm)?");
                    sp["gap"] = op["gap"] is null ? 10 : Json.Number(op["gap"]) is double pg && pg is >= 0 and <= 1000 ? pg : throw new CommandException("The gap between parts is 0 to 1000 mm.");
                    sp["dir"] = (string?)op["dir"] is "down" ? "down" : (string?)op["dir"] is null or "right" ? "right" : throw new CommandException("The parts go right or down.");
                    t["split"] = sp;
                    break;
                }
                // Cells from a CSV text or a workbook (base64 data), from a cell on (stage 5): rows and columns added as needed.
                case "load":
                {
                    var rowsIn = Loaded(op);
                    var at = DraftingTables.Parse((string?)op["at"] ?? (string?)op["cell"] ?? "A1") ?? throw new CommandException("From which cell? Its address, such as A1.");
                    var needR = at.R + rowsIn.Count - g.R;
                    var needC = at.C + (rowsIn.Count == 0 ? 0 : rowsIn.Max(x => x.Count)) - g.C;
                    if (at.R + rowsIn.Count > DraftingTables.MaxRows || at.C + (rowsIn.Count == 0 ? 0 : rowsIn.Max(x => x.Count)) > DraftingTables.MaxCols)
                        throw new CommandException($"A table has up to {DraftingTables.MaxRows} rows and {DraftingTables.MaxCols} columns.");
                    if (needR > 0) { Insert(t, g, rows: true, g.R, needR); shifts?.Add(new Shifted(true, g.R, needR, true)); }
                    if (needC > 0) { var g2 = DraftingTables.Read(t); Insert(t, g2, rows: false, g2.C, needC); shifts?.Add(new Shifted(false, g2.C, needC, true)); }
                    var g3 = DraftingTables.Read(t);
                    for (var i = 0; i < rowsIn.Count; i++)
                        for (var j = 0; j < rowsIn[i].Count; j++)
                            if (!g3.Cells[at.R + i, at.C + j].Covered) Write(t, at.R + i, at.C + j, rowsIn[i][j]);
                    break;
                }
                case "tableStyle":
                {
                    var style = op["style"] as JsonObject ?? throw new CommandException($"A table's style is {{{string.Join(", ", TableStyleKeys)}}}.");
                    var st = (JsonObject)t["style"]!;
                    foreach (var (k, v) in style)
                    {
                        if (!TableStyleKeys.Contains(k)) throw new CommandException($"„{k}” is not a table's style ({string.Join(", ", TableStyleKeys)}).");
                        if (StyleValue(k, v) is { } value) st[k] = value; else st.Remove(k);
                    }
                    break;
                }
                case "preset":
                {
                    var preset = (string?)op["preset"] ?? throw new CommandException($"Which preset? One of {string.Join(", ", DraftingTables.Presets)}, or a table style.");
                    // A table style of the user's (stage 3): its look laid on the table's cells, their texts and sizes kept.
                    if (DraftingTables.StyleOf(styles, preset) is { } st) DraftingTables.ApplyStyle(t, st, sizes: false);
                    else DraftingTables.ApplyPreset(t, preset);
                    break;
                }
                // Worked out on the table as it reads by the command (TableAutoSum), never here.
                case "autoSum": throw new CommandException("AutoSum reads the table's values: drafting.editTable works it out.");
                default: throw new CommandException($"„{name}” is not an edit of a table ({string.Join(", ", Ops)}).");
            }
        }
        return t;
    }

    /// <summary>The rows of what <c>load</c> brings: a workbook's first sheet or a CSV file (base64 <c>data</c>), or a CSV <c>text</c>; trailing empty cells and rows left out.</summary>
    static List<List<string>> Loaded(JsonObject op)
    {
        List<List<string>> rows;
        if ((string?)op["data"] is { Length: > 0 } data)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(data); } catch (FormatException) { throw new CommandException("The file is not base64."); }
            rows = Tables.Read(bytes);
        }
        else if ((string?)op["text"] is { } text) rows = Tables.Csv(text.TrimStart('\uFEFF'));
        else throw new CommandException("What to load? A CSV text, or a file's data (base64).");
        rows = [.. rows.Select(r => { var c = r.ToList(); while (c.Count > 0 && c[^1].Trim().Length == 0) c.RemoveAt(c.Count - 1); return c; })];
        while (rows.Count > 0 && rows[^1].Count == 0) rows.RemoveAt(rows.Count - 1);
        if (rows.Count == 0) throw new CommandException("The file holds no cells.");
        return rows;
    }

    /// <summary>An element's id a link names (null: unlinked); refused when the model has no such element.</summary>
    static string? Element(JsonNode? n, Func<string, bool>? exists)
    {
        if (n == null) return null;
        var id = ((string?)n)?.Trim() is { Length: > 0 } x ? x : throw new CommandException("An element's id, or null to unlink.");
        return exists == null || exists(id) ? id : throw new CommandException("There is no such element.");
    }

    /// <summary>The master cells whose regions meet a range, once each.</summary>
    static List<(int R, int C)> Masters(DraftingTables.Grid g, (int R0, int C0, int R1, int C1) b)
    {
        var o = new List<(int, int)>();
        for (var r = b.R0; r <= b.R1; r++)
            for (var c = b.C0; c <= b.C1; c++)
                if (!o.Contains(g.Master[r, c])) o.Add(g.Master[r, c]);
        return o;
    }

    static void Set(JsonObject t, DraftingTables.Grid g, JsonObject op)
    {
        var at = DraftingTables.Parse((string?)op["cell"]) ?? throw new CommandException("Which cell? Its address, such as B2.");
        if (op["values"] is JsonArray block)
        {
            // A block pasted from its top-left cell: rows of texts.
            for (var i = 0; i < block.Count; i++)
            {
                var line = block[i] as JsonArray ?? new JsonArray(block[i]?.DeepClone());
                for (var j = 0; j < line.Count; j++)
                {
                    var (r, c) = (at.R + i, at.C + j);
                    if (r >= g.R || c >= g.C) throw new CommandException($"The pasted cells reach past the table ({DraftingTables.Address(g.R - 1, g.C - 1)} is its last cell).");
                    if (g.Cells[r, c].Covered) continue;
                    Write(t, r, c, line[j]?.ToString());
                }
            }
            return;
        }
        if (at.R >= g.R || at.C >= g.C) throw new CommandException($"{DraftingTables.Address(at.R, at.C)} is outside the table.");
        if (g.Cells[at.R, at.C].Covered)
        {
            var (mr, mc) = g.Master[at.R, at.C];
            throw new CommandException($"{DraftingTables.Address(at.R, at.C)} is inside a merged cell: write in {DraftingTables.Address(mr, mc)}.");
        }
        Write(t, at.R, at.C, (string?)op["text"]);
    }

    static void Write(JsonObject t, int r, int c, string? text)
    {
        text = text?.Replace("\r\n", "\n");
        if (text is { Length: > DraftingTables.MaxText }) throw new CommandException($"A cell holds at most {DraftingTables.MaxText} characters.");
        var o = Own(t, r, c);
        if (string.IsNullOrEmpty(text)) o.Remove("t"); else o["t"] = text;
        Tidy(t, r, c);
    }

    static void Borders(JsonObject t, DraftingTables.Grid g, JsonObject op)
    {
        var b = RangeOf(op, g);
        var kind = (string?)op["edges"] ?? "all";
        if (!BorderKinds.Contains(kind)) throw new CommandException($"Which lines? One of {string.Join(", ", BorderKinds)}.");
        var side = kind is "default" ? null : kind is "none" ? new JsonObject { ["w"] = 0, ["c"] = "#000000" }
            : SideValue(new JsonObject { ["w"] = op["w"]?.DeepClone() ?? 0.25, ["c"] = op["c"]?.DeepClone() });
        if (kind == "none") kind = "all";
        foreach (var (r, c) in Masters(g, b))
        {
            var cell = g.Cells[r, c];
            var (r0, c0, r1, c1) = (r, c, r + cell.RowSpan - 1, c + cell.ColSpan - 1);
            var on = new Dictionary<string, bool>
            {
                ["top"] = kind switch { "all" or "default" => true, "outer" or "top" => r0 <= b.R0, "inner" => r0 > b.R0, _ => false },
                ["bottom"] = kind switch { "all" or "default" => true, "outer" or "bottom" => r1 >= b.R1, "inner" => r1 < b.R1, _ => false },
                ["left"] = kind switch { "all" or "default" => true, "outer" or "left" => c0 <= b.C0, "inner" => c0 > b.C0, _ => false },
                ["right"] = kind switch { "all" or "default" => true, "outer" or "right" => c1 >= b.C1, "inner" => c1 < b.C1, _ => false },
            };
            foreach (var s in Sides)
            {
                if (!on[s]) continue;
                var o = Own(t, r, c);
                if (side == null) o.Remove(s); else o[s] = side.DeepClone();
                Tidy(t, r, c);
                // The cells across that line say nothing of it any more: this side alone decides it.
                var across = s switch { "top" => "bottom", "bottom" => "top", "left" => "right", _ => "left" };
                var (fr, fc, tr, tc) = s switch
                {
                    "top" => (r0 - 1, c0, r0 - 1, c1),
                    "bottom" => (r1 + 1, c0, r1 + 1, c1),
                    "left" => (r0, c0 - 1, r1, c0 - 1),
                    _ => (r0, c1 + 1, r1, c1 + 1),
                };
                for (var i = fr; i <= tr; i++)
                    for (var j = fc; j <= tc; j++)
                    {
                        if (i < 0 || j < 0 || i >= g.R || j >= g.C) continue;
                        var (mr, mc) = g.Master[i, j];
                        if (CellsOf(t, mr)[mc] is JsonObject n && n.Remove(across)) Tidy(t, mr, mc);
                    }
            }
        }
    }

    static void Merge(JsonObject t, DraftingTables.Grid g, (int R0, int C0, int R1, int C1) b)
    {
        if (b.R0 == b.R1 && b.C0 == b.C1) throw new CommandException("Merge two cells or more.");
        foreach (var (r, c) in Masters(g, b))
        {
            var cell = g.Cells[r, c];
            if (r < b.R0 || c < b.C0 || r + cell.RowSpan - 1 > b.R1 || c + cell.ColSpan - 1 > b.C1)
                throw new CommandException($"The range cuts through the merged cell {DraftingTables.Address(r, c)}: take it in whole, or split it first.");
        }
        // The top-left cell's text and style stay (a spreadsheet's merge); the cells it now covers are emptied.
        for (var r = b.R0; r <= b.R1; r++)
            for (var c = b.C0; c <= b.C1; c++)
                if (r != b.R0 || c != b.C0) CellsOf(t, r)[c] = null;
        var o = Own(t, b.R0, b.C0);
        o["rs"] = b.R1 - b.R0 + 1;
        o["cs"] = b.C1 - b.C0 + 1;
        if ((int)o["rs"]! == 1) o.Remove("rs");
        if ((int)o["cs"]! == 1) o.Remove("cs");
    }

    static void Insert(JsonObject t, DraftingTables.Grid g, bool rows, int at, int count)
    {
        var n = rows ? g.R : g.C;
        if (at > n) throw new CommandException($"Insert at 0 to {n}.");
        if (count < 1 || n + count > (rows ? DraftingTables.MaxRows : DraftingTables.MaxCols)) throw new CommandException(rows ? $"A table has up to {DraftingTables.MaxRows} rows." : $"A table has up to {DraftingTables.MaxCols} columns.");
        // A merged cell across where they go spans them too.
        for (var r = 0; r < g.R; r++)
            for (var c = 0; c < g.C; c++)
            {
                var cell = g.Cells[r, c];
                if (cell.Covered || CellsOf(t, r)[c] is not JsonObject o) continue;
                var (start, span, key) = rows ? (r, cell.RowSpan, "rs") : (c, cell.ColSpan, "cs");
                if (start < at && start + span > at) o[key] = span + count;
            }
        if (rows)
        {
            var like = (JsonObject)Rows(t)[Math.Max(0, Math.Min(at, g.R) - 1)]!;
            for (var k = 0; k < count; k++)
            {
                var row = new JsonObject { ["h"] = like["h"]?.DeepClone(), ["cells"] = new JsonArray([.. Enumerable.Range(0, g.C).Select(_ => (JsonNode?)null)]) };
                if (like["fit"] is JsonValue f) row["fit"] = f.DeepClone();
                Rows(t).Insert(at, row);
            }
        }
        else
        {
            var like = (JsonObject)Cols(t)[Math.Max(0, Math.Min(at, g.C) - 1)]!;
            for (var k = 0; k < count; k++)
            {
                Cols(t).Insert(at, new JsonObject { ["w"] = like["w"]?.DeepClone() });
                for (var r = 0; r < g.R; r++) CellsOf(t, r).Insert(at, null);
            }
        }
        ShiftAll(t, rows, at, count, insert: true);
        // A total under rows put in right below its range takes them in (a spreadsheet's sum over a list growing at its foot).
        if (rows && at > 0) Extend(t, at, count);
    }

    /// <summary>Ranges of the table's own ending on row <paramref name="at"/> - 1, in cells below the <paramref name="count"/> rows put in at <paramref name="at"/>, reaching over them.</summary>
    static void Extend(JsonObject t, int at, int count)
    {
        var rows = Rows(t);
        for (var r = at + count; r < rows.Count; r++)
            foreach (var cn in (rows[r] as JsonObject)?["cells"] as JsonArray ?? [])
                if (cn is JsonObject o && (string?)o["t"] is { } text && text.Contains("{=", StringComparison.Ordinal))
                {
                    var now = MapRefs(text, x => x.Table == null && x.B is { } z && Math.Max(x.A.R, z.R) == at - 1
                        ? Write(null, (Math.Min(x.A.R, z.R), Math.Min(x.A.C, z.C)), (at - 1 + count, Math.Max(x.A.C, z.C))) : null);
                    if (now != text) o["t"] = now;
                }
    }

    static void Delete(JsonObject t, DraftingTables.Grid g, bool rows, int at, int count)
    {
        var n = rows ? g.R : g.C;
        if (count < 1 || at + count > n) throw new CommandException(rows ? $"The table has rows 1 to {n}." : $"The table has columns A to {DraftingTables.ColName(n - 1)}.");
        if (count >= n) throw new CommandException(rows ? "A table keeps one row at least." : "A table keeps one column at least.");
        var end = at + count;
        for (var r = 0; r < g.R; r++)
            for (var c = 0; c < g.C; c++)
            {
                var cell = g.Cells[r, c];
                if (cell.Covered || CellsOf(t, r)[c] is not JsonObject o) continue;
                var (start, span, key) = rows ? (r, cell.RowSpan, "rs") : (c, cell.ColSpan, "cs");
                if (span == 1 || start + span <= at || start >= end) continue;
                var overlap = Math.Min(start + span, end) - Math.Max(start, at);
                if (start >= at)
                {
                    // Its first row (column) goes: what is left of it starts on the first that stays, with its text and style.
                    var left = start + span - end;
                    var moved = (JsonObject)o.DeepClone();
                    moved[key] = left;
                    if (rows) CellsOf(t, end)[c] = moved; else CellsOf(t, r)[end] = moved;
                }
                else o[key] = span - overlap;
            }
        if (rows) for (var k = 0; k < count; k++) Rows(t).RemoveAt(at);
        else
            for (var k = 0; k < count; k++)
            {
                Cols(t).RemoveAt(at);
                foreach (var rn in Rows(t)) ((JsonArray)((JsonObject)rn!)["cells"]!).RemoveAt(at);
            }
        foreach (var rn in Rows(t))
            foreach (var cn in (JsonArray)((JsonObject)rn!)["cells"]!)
                if (cn is JsonObject o) foreach (var k in new[] { "rs", "cs" }) if (Json.Number(o[k]) is <= 1) o.Remove(k);
        ShiftAll(t, rows, at, count, insert: false);
    }

    static void ShiftAll(JsonObject t, bool rows, int at, int count, bool insert) =>
        MapCells(t, text => ShiftRefs(text, rows, at, count, insert));

    /// <summary>Every cell's text mapped by <paramref name="f"/> where it holds a formula or a token; whether any changed.</summary>
    public static bool MapCells(JsonObject t, Func<string, string> f)
    {
        var changed = false;
        foreach (var rn in t["rows"] as JsonArray ?? [])
            foreach (var cn in (rn as JsonObject)?["cells"] as JsonArray ?? [])
                if (cn is JsonObject o && (string?)o["t"] is { } text && text.Contains('{'))
                {
                    var now = f(text);
                    if (now != text) { o["t"] = now; changed = true; }
                }
        return changed;
    }

    /// <summary>The cell a reference names after rows (columns) went in or out at <paramref name="at"/>; null when it went out.</summary>
    static int? Moved(int i, int at, int count, bool insert) =>
        insert ? (i >= at ? i + count : i) : i < at ? i : i >= at + count ? i - count : null;

    /// <summary>What a reference to a cell taken out (or to a table deleted) reads as: it has no value, so the formula says so.</summary>
    public const string Gone = "REF";

    /// <summary>
    /// A reference to a cell or a range in a cell's text: of its own table (<see cref="Table"/> null: <c>A3</c>, <c>A1:B4</c>) or
    /// of another by its name (<c>table.Areas.B4</c>, <c>table.Areas.B2:B9</c>); <see cref="B"/> the range's other end.
    /// </summary>
    public sealed record Ref(string? Table, (int R, int C) A, (int R, int C)? B);

    /// <summary>A reference written back: its cell or its range (first to last), qualified by its table's name when it has one.</summary>
    public static string Write(string? table, (int R, int C) a, (int R, int C)? b)
    {
        var q = table != null ? TableScope.Prefix + table + "." : "";
        if (b is not { } z) return q + DraftingTables.Address(a.R, a.C);
        var (r0, c0, r1, c1) = (Math.Min(a.R, z.R), Math.Min(a.C, z.C), Math.Max(a.R, z.R), Math.Max(a.C, z.C));
        return q + DraftingTables.Address(r0, c0) + ":" + DraftingTables.Address(r1, c1);
    }

    /// <summary>
    /// A text's cell references rewritten by <paramref name="map"/> (null: kept as written): in its <c>{= …}</c> formulas (texts in
    /// quotes, functions and other dotted names left alone) and its tokens naming another table's cell (<c>{table.Areas.B4}</c>).
    /// </summary>
    public static string MapRefs(string text, Func<Ref, string?> map)
    {
        var sb = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            var open = text.IndexOf('{', i);
            if (open < 0) { sb.Append(text, i, text.Length - i); break; }
            if (open + 1 < text.Length && text[open + 1] == '=')
            {
                var close = LiveText.CloseOf(text, open + 2);
                if (close < 0) { sb.Append(text, i, text.Length - i); break; }
                sb.Append(text, i, open + 2 - i);
                sb.Append(MapBody(text[(open + 2)..close], map));
                sb.Append('}');
                i = close + 1;
                continue;
            }
            var end = text.IndexOf('}', open + 1);
            var inner = end < 0 ? -1 : text.IndexOf('{', open + 1, end - open - 1);
            if (end < 0 || inner >= 0) { sb.Append(text, i, open + 1 - i); i = open + 1; continue; }
            var key = text[(open + 1)..end];
            sb.Append(text, i, open + 1 - i);
            var k = key.Trim();
            sb.Append(TableScope.Split(k) is { } s && DraftingTables.Parse(s.Cell) is { } a && map(new Ref(s.Table, a, null)) is { } repl ? repl : key);
            sb.Append('}');
            i = end + 1;
        }
        return sb.ToString();
    }

    static string MapBody(string s, Func<Ref, string?> map)
    {
        var sb = new StringBuilder();
        var i = 0;
        static bool WordChar(char ch) => char.IsLetterOrDigit(ch) || ch is '_' or '.';
        int WordEnd(int k) { while (k < s.Length && WordChar(s[k])) k++; return k; }
        int Space(int k) { while (k < s.Length && char.IsWhiteSpace(s[k])) k++; return k; }
        while (i < s.Length)
        {
            var ch = s[i];
            if (ch is '"' or '\'' or '“' or '„')
            {
                var q = ch is '“' or '„' ? '”' : ch;
                var e = s.IndexOf(q, i + 1);
                e = e < 0 ? s.Length : e + 1;
                sb.Append(s, i, e - i);
                i = e;
                continue;
            }
            if (!(char.IsLetter(ch) || ch == '_') || i > 0 && WordChar(s[i - 1])) { sb.Append(ch); i++; continue; }
            var end = WordEnd(i);
            var word = s[i..end];
            var after = Space(end);
            // A cell of its own table (no dot), or of another by its name (table.<name>.<cell>); a function's name is neither.
            string? table = null;
            (int R, int C)? first = null;
            if (!(after < s.Length && s[after] == '('))
            {
                if (TableScope.Split(word) is { } q && DraftingTables.Parse(q.Cell) is { } qa) (table, first) = (q.Table, qa);
                else if (!word.Contains('.') && DraftingTables.Parse(word) is { } la) first = la;
            }
            if (first is not { } a) { sb.Append(word); i = end; continue; }
            // A range: its other end a cell of the same table (written bare, or with the same table's name).
            (int R, int C)? other = null;
            var stop = end;
            if (after < s.Length && s[after] == ':' && Space(after + 1) is var b0 && b0 < s.Length && WordEnd(b0) is var b1 && b1 > b0)
            {
                var w2 = s[b0..b1];
                if (!w2.Contains('.') && DraftingTables.Parse(w2) is { } z) (other, stop) = (z, b1);
                else if (table != null && TableScope.Split(w2) is { } q2 && string.Equals(q2.Table, table, StringComparison.OrdinalIgnoreCase) && DraftingTables.Parse(q2.Cell) is { } z2) (other, stop) = (z2, b1);
            }
            sb.Append(map(new Ref(table, a, other)) ?? s[i..stop]);
            i = stop;
        }
        return sb.ToString();
    }

    /// <summary>A reference after rows (columns) went in or out at <paramref name="at"/>: moved, a range narrowed, <see cref="Gone"/> when all of it went.</summary>
    public static string Shift(Ref r, bool rows, int at, int count, bool insert)
    {
        if (r.B is not { } z)
        {
            var v = Moved(rows ? r.A.R : r.A.C, at, count, insert);
            return v is { } x ? Write(r.Table, rows ? (x, r.A.C) : (r.A.R, x), null) : Gone;
        }
        var a = r.A;
        var (lo, hi) = rows ? (Math.Min(a.R, z.R), Math.Max(a.R, z.R)) : (Math.Min(a.C, z.C), Math.Max(a.C, z.C));
        int from, to;
        if (insert) (from, to) = (Moved(lo, at, count, true)!.Value, Moved(hi, at, count, true)!.Value);
        else
        {
            from = lo < at ? lo : lo >= at + count ? lo - count : at;
            to = hi < at ? hi : hi >= at + count ? hi - count : at - 1;
        }
        if (to < from) return Gone;
        return rows ? Write(r.Table, (from, Math.Min(a.C, z.C)), (to, Math.Max(a.C, z.C))) : Write(r.Table, (Math.Min(a.R, z.R), from), (Math.Max(a.R, z.R), to));
    }

    /// <summary>
    /// A text's formulas with their cell references following rows (or columns) put in or taken out at <paramref name="at"/>:
    /// <c>{= A3 * 2}</c> reads <c>A4</c> after a row went in above it, a range narrows when some of its rows go, a reference to
    /// a cell taken out reads <see cref="Gone"/>. Texts in quotes, functions and names with a dot (<c>sheet.number</c>) stay.
    /// </summary>
    public static string ShiftRefs(string text, bool rows, int at, int count, bool insert) =>
        MapRefs(text, r => r.Table == null ? Shift(r, rows, at, count, insert) : null);

    /// <summary>A row or column change of a table, kept to carry other tables' references to it along (stage 2).</summary>
    public sealed record Shifted(bool Rows, int At, int Count, bool Insert);

    /// <summary>The ops applied in order (as <see cref="Apply(JsonObject?, JsonArray)"/>), and the rows and columns they put in or took out, in order.</summary>
    public static (JsonObject Table, List<Shifted> Shifts) ApplyTracked(JsonObject? table, JsonArray ops, IReadOnlyList<TableStyleDef>? styles = null, Func<string, bool>? exists = null)
    {
        var shifts = new List<Shifted>();
        var t = Apply(table, ops, shifts, styles, exists);
        return (t, shifts);
    }
}
