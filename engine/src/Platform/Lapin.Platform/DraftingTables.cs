using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A drafting table (issue #71, docs/plan-drafting-tables.md): an IfcAnnotation TABLE on a view or on a sheet's paper
/// (<c>paper:&lt;sheet&gt;</c>), its top-left corner at <c>at</c>, turned by <c>angle</c>, sized in paper mm as texts are, so
/// it reads the same at any scale. Its grid is <c>attrs.table</c>:
/// <code>
/// { "cols": [{ "w": 25, "fit": false, "min": 5, "max": 80 }],
///   "rows": [{ "h": 7, "fit": true, "header": true, "cells": [{ "t": "Room", "cs": 2, "bold": true, "fill": "#e6e6e6" }, null] }],
///   "style": { "size": 2.5, "color": "#000000", "fill": null, "pad": 1, "align": "left", "valign": "middle",
///              "grid": { "w": 0.18, "c": "#000000" }, "outline": { "w": 0.35, "c": "#000000" } },
///   "preset": "header" }
/// </code>
/// A cell holds text (lines parted by newlines) with <c>{= …}</c> formulas reading the table's cells by address (<c>A1</c>,
/// <c>sum(A1:A4)</c>) and, on a sheet's paper, the sheet's tokens; it spans <c>rs</c> rows and <c>cs</c> columns from its
/// top-left (the cells it covers are empty); its own style (fill, color, bold, italic, size, align, valign) and its sides
/// (<c>top</c>, <c>bottom</c>, <c>left</c>, <c>right</c>: <c>{ w, c }</c>, w 0 none) override the table's.
/// </summary>
public static partial class DraftingTables
{
    public const string Predefined = "TABLE";
    public const int MaxRows = 500;
    public const int MaxCols = 100;
    /// <summary>The longest text a cell holds.</summary>
    public const int MaxText = 10_000;
    /// <summary>How tall a line of text is, for its height (as a text note's lines).</summary>
    public const double LineStep = 1.35;
    /// <summary>A character's width for its height (as a text note's box), to fit a column and wrap its lines.</summary>
    public const double CharWidth = 0.6;

    /// <summary>The ready-made looks of a new table: every line, a header row, lines under the header only, none.</summary>
    public static readonly string[] Presets = ["header", "grid", "minimal", "none"];

    public static readonly string[] Aligns = ["left", "center", "right"];
    public static readonly string[] VAligns = ["top", "middle", "bottom"];

    // ------------------------------------------------------------ addresses

    /// <summary>A column's letters: 0 → A, 25 → Z, 26 → AA.</summary>
    public static string ColName(int c)
    {
        var sb = new StringBuilder();
        for (c++; c > 0; c = (c - 1) / 26) sb.Insert(0, (char)('A' + (c - 1) % 26));
        return sb.ToString();
    }

    public static string Address(int r, int c) => ColName(c) + (r + 1).ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^([A-Za-z]{1,3})([0-9]{1,5})$")]
    private static partial Regex CellRef();

    /// <summary>A cell's row and column (from 0) by its address (<c>B3</c> → 2, 1; case aside); null when it is not one.</summary>
    public static (int R, int C)? Parse(string? address)
    {
        if (address == null || CellRef().Match(address.Trim()) is not { Success: true } m) return null;
        var c = 0;
        foreach (var ch in m.Groups[1].Value.ToUpperInvariant()) c = c * 26 + (ch - 'A' + 1);
        var r = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        return r < 1 ? null : (r - 1, c - 1);
    }

    /// <summary>A range's rows and columns, first to last (<c>A1:C4</c>, <c>C4:A1</c>, or one cell <c>B2</c>); null when it does not read.</summary>
    public static (int R0, int C0, int R1, int C1)? ParseRange(string? range)
    {
        if (range == null) return null;
        var parts = range.Split(':');
        if (parts.Length is < 1 or > 2 || Parse(parts[0]) is not { } a) return null;
        var b = parts.Length == 2 ? Parse(parts[1]) : a;
        if (b is not { } z) return null;
        return (Math.Min(a.R, z.R), Math.Min(a.C, z.C), Math.Max(a.R, z.R), Math.Max(a.C, z.C));
    }

    // ------------------------------------------------------------ reading

    public sealed record Side(double W, string C);
    public sealed record Col(double W, bool Fit, double? Min, double? Max);
    public sealed record Row(double H, bool Fit, double? Min, double? Max, bool Header);

    /// <summary>A cell as stored: its text, spans and own style (null: the table's); <see cref="Covered"/> when a merged cell hides it.</summary>
    public sealed record Cell(string Text, int RowSpan, int ColSpan, JsonObject Own)
    {
        public bool Covered { get; init; }
        public string? Str(string k) => Own[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        public double? Num(string k) => Json.Number(Own[k]);
        public bool? Bool(string k) => Own[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;
        public Side? SideOf(string k) => Own[k] is JsonObject o ? new Side(Math.Max(0, Json.Number(o["w"]) ?? 0), (string?)o["c"] ?? "#000000") : null;
    }

    /// <summary>A table as read: its columns, rows, cells (row by row, every one there, covered ones marked) and style.</summary>
    public sealed class Grid
    {
        public required List<Col> Cols { get; init; }
        public required List<Row> Rows { get; init; }
        public required Cell[,] Cells { get; init; }
        public required JsonObject Style { get; init; }
        public int R => Rows.Count;
        public int C => Cols.Count;
        /// <summary>The cell whose region holds (r, c): itself, or the merged cell covering it.</summary>
        public required (int R, int C)[,] Master { get; init; }

        public double Size => Json.Number(Style["size"]) is double s && s > 0 ? s : 2.5;
        public double Pad => Json.Number(Style["pad"]) is double p && p >= 0 ? p : 1;
        public Side GridSide => SideIn(Style["grid"], new Side(0.18, "#000000"));
        public Side Outline => SideIn(Style["outline"], new Side(0.35, "#000000"));
        static Side SideIn(JsonNode? n, Side d) => n is JsonObject o ? new Side(Math.Max(0, Json.Number(o["w"]) ?? d.W), (string?)o["c"] ?? d.C) : d;
    }

    static readonly JsonObject NoStyle = [];

    /// <summary>The table an entity holds (an empty one-cell table when it holds none that reads).</summary>
    public static Grid Read(Entity e) => Read(e.Attrs["table"] as JsonObject);

    public static Grid Read(JsonObject? t)
    {
        var cols = (t?["cols"] as JsonArray ?? []).Select(n => n as JsonObject ?? []).Select(o =>
            new Col(Json.Number(o["w"]) is double w && w > 0 ? w : 25, o["fit"] is JsonValue f && f.TryGetValue<bool>(out var fb) && fb, Json.Number(o["min"]), Json.Number(o["max"]))).ToList();
        if (cols.Count == 0) cols.Add(new Col(25, false, null, null));
        var rowsJson = (t?["rows"] as JsonArray ?? []).Select(n => n as JsonObject ?? []).ToList();
        if (rowsJson.Count == 0) rowsJson.Add([]);
        var rows = rowsJson.Select(o => new Row(Json.Number(o["h"]) is double h && h > 0 ? h : 7,
            o["fit"] is JsonValue f && f.TryGetValue<bool>(out var fb) && fb, Json.Number(o["min"]), Json.Number(o["max"]),
            o["header"] is JsonValue hv && hv.TryGetValue<bool>(out var hb) && hb)).ToList();
        var (nr, nc) = (rows.Count, cols.Count);
        var cells = new Cell[nr, nc];
        var master = new (int, int)[nr, nc];
        for (var r = 0; r < nr; r++)
            for (var c = 0; c < nc; c++) { cells[r, c] = new Cell("", 1, 1, NoStyle); master[r, c] = (r, c); }
        for (var r = 0; r < nr; r++)
        {
            var row = rowsJson[r]["cells"] as JsonArray ?? [];
            for (var c = 0; c < nc && c < row.Count; c++)
            {
                if (row[c] is not JsonObject o || cells[r, c].Covered) continue;
                var rs = Math.Clamp((int)(Json.Number(o["rs"]) ?? 1), 1, nr - r);
                var cs = Math.Clamp((int)(Json.Number(o["cs"]) ?? 1), 1, nc - c);
                // A span reaching into a cell already covered (a file put together by hand) stops short of it.
                for (var k = 1; k < cs; k++) if (cells[r, c + k].Covered || cells[r, c + k].RowSpan > 1 || cells[r, c + k].ColSpan > 1) { cs = k; break; }
                cells[r, c] = new Cell((string?)o["t"] ?? "", rs, cs, o);
                for (var i = r; i < r + rs; i++)
                    for (var j = c; j < c + cs; j++)
                        if (i != r || j != c) { cells[i, j] = new Cell("", 1, 1, NoStyle) { Covered = true }; master[i, j] = (r, c); }
            }
        }
        return new Grid { Cols = cols, Rows = rows, Cells = cells, Master = master, Style = t?["style"] as JsonObject ?? [] };
    }

    // ------------------------------------------------------------ values

    /// <summary>
    /// A table's cells as they read: each one's text with its formulas worked out (<c>{= A1 * 2}</c>, <c>{= sum(B2:B9) | 0.00}</c>)
    /// and its tokens filled by <paramref name="outer"/> (a sheet's on its paper, a symbol's in a symbol, the model's), once each,
    /// in any order. A formula reading the cell it is in, through others or not, does not read (<c>&lt;A1 + 1&gt;</c>) and says
    /// so in <see cref="Errors"/>. An empty cell reads as nothing in a range (<c>sum</c>, <c>count</c> leave it out) and as 0
    /// alone; a covered one as empty. With a <paramref name="scope"/> (stage 2), a cell reads other tables' cells by their
    /// names (<c>table.Areas.B4</c>, <c>sum(table.Areas.B2:B9)</c>, <c>{table.Areas.B4}</c>), the guard against cycles kept
    /// across them. <paramref name="fields"/>, when given, fills a cell's own fields first (a tag symbol's <c>{number}</c>).
    /// </summary>
    public sealed class Values(Grid g, Func<string, (bool Known, string? Value)>? outer = null, TableScope? scope = null, Entity? self = null, Func<string, string>? fields = null)
    {
        readonly Dictionary<(int, int), (string Shown, XValue Value)> done = [];
        readonly HashSet<(string, int, int)> busy = scope?.Busy ?? [];
        readonly string key = self?.Id ?? "";
        public List<string> Errors { get; } = [];
        public Grid Grid => g;

        public string Shown(int r, int c) => Get(r, c).Shown;
        public XValue Value(int r, int c) => Get(r, c).Value;

        /// <summary>Whether the cell is being worked out now (a formula reaching it again goes round in a circle).</summary>
        internal bool Busy(int r, int c) => busy.Contains((key, r, c));

        (string Shown, XValue Value) Get(int r, int c)
        {
            if (done.TryGetValue((r, c), out var v)) return v;
            var cell = g.Cells[r, c];
            if (cell.Covered || cell.Text.Length == 0) return done[(r, c)] = ("", new XValue(null, ""));
            busy.Add((key, r, c));
            try
            {
                var unknown = new List<string>();
                var text = cell.Text;
                if (fields != null && text.Contains('{')) text = fields(text);
                XValue? whole = null;
                if (text.Contains("{=", StringComparison.Ordinal))
                {
                    // A cell that is one formula and nothing else keeps its value as a number, not as the text it is written as.
                    var t = text.Trim();
                    if (t.StartsWith("{=", StringComparison.Ordinal) && LiveText.CloseOf(t, 2) == t.Length - 1)
                    {
                        var (formula, format) = LiveText.SplitFormat(t[2..^1]);
                        if (format == null && Expr.TryEvalValue(formula, Name, Range) is ({ } x, null)) whole = x;
                    }
                    text = LiveText.Formulas(text, Name, unknown, Range);
                }
                if ((outer != null || scope != null) && text.Contains('{')) text = TitleBlocks.Fill(text, Token, unknown);
                foreach (var u in unknown) Errors.Add($"{Address(r, c)}: {u}");
                return done[(r, c)] = (text, whole ?? XValue.Read(text));
            }
            finally { busy.Remove((key, r, c)); }
        }

        (bool Known, string? Value) Token(string name)
        {
            if (scope != null && TableScope.IsRef(name) && scope.Cell(self, name, Errors) is { } x) return (true, x.Shown);
            return outer?.Invoke(name) ?? (false, null);
        }

        /// <summary>One of this table's cells by its row and column, as a formula reads it (an empty one 0); null past its edge or in a circle.</summary>
        internal XValue? Local(int r, int c, string? from = null)
        {
            if (r >= g.R || c >= g.C) return null;
            if (Busy(r, c)) { Errors.Add($"{from ?? Address(r, c)} reads itself."); return null; }
            var v = Value(r, c);
            // Alone, an empty cell is 0 (as a spreadsheet reads it).
            return v.Text is "" && v.Number == null ? new XValue(0, "") : v;
        }

        XValue? Name(string name)
        {
            if (Parse(name) is { } at) return Local(at.R, at.C);
            if (scope != null && TableScope.IsRef(name)) return scope.Cell(self, name, Errors)?.Value;
            return outer?.Invoke(name) is { Known: true } k ? XValue.Read(k.Value ?? "") : null;
        }

        /// <summary>A range's values, row by row, empty cells left out; null when a cell in it is being worked out (a circle).</summary>
        internal IReadOnlyList<XValue>? Cells((int R0, int C0, int R1, int C1) b, string? table = null)
        {
            var o = new List<XValue>();
            for (var r = b.R0; r <= Math.Min(b.R1, g.R - 1); r++)
                for (var c = b.C0; c <= Math.Min(b.C1, g.C - 1); c++)
                {
                    if (Busy(r, c)) { Errors.Add($"{table}{Address(r, c)} reads itself."); return null; }
                    var v = Value(r, c);
                    if (!(v.Number == null && string.IsNullOrWhiteSpace(v.Text))) o.Add(v);
                }
            return o;
        }

        IReadOnlyList<XValue>? Range(string from, string to)
        {
            if (scope != null && TableScope.IsRef(from)) return scope.Range(self, from, to, Errors);
            return ParseRange(from + ":" + to) is { } b ? Cells(b) : null;
        }
    }

    // ------------------------------------------------------------ layout

    /// <summary>A cell's text laid: its lines, height and the style it is drawn in.</summary>
    public sealed record CellText(List<string> Lines, double Size, bool Bold, bool Italic, string Color, string Align, string VAlign);

    /// <summary>A table laid out in paper mm: each column's width and row's height (fit to their text where asked), each master cell's text.</summary>
    public sealed record Layout(double[] ColW, double[] RowH, CellText?[,] Texts)
    {
        public double Width => ColW.Sum();
        public double Height => RowH.Sum();
        public double X(int c) => ColW.Take(c).Sum();
        public double Y(int r) => RowH.Take(r).Sum();
    }

    static string Pick(string? v, string[] allowed, string d) => v != null && allowed.Contains(v) ? v : d;

    public static CellText Style(Grid g, Cell cell, string shown, double? width, string font, bool bold, bool italic, string color)
    {
        var size = cell.Num("size") is double s && s > 0 ? s : g.Size;
        var maxW = width is { } w ? Math.Max(size * CharWidth, w - 2 * g.Pad) : (double?)null;
        var lines = shown.Length == 0 ? [] : TextWrap.Lines(shown, size * CharWidth, maxW);
        return new CellText(lines, size, cell.Bool("bold") ?? bold, cell.Bool("italic") ?? italic, cell.Str("color") ?? (string?)g.Style["color"] ?? color,
            Pick(cell.Str("align") ?? (string?)g.Style["align"], Aligns, "left"), Pick(cell.Str("valign") ?? (string?)g.Style["valign"], VAligns, "middle"));
    }

    /// <summary>How tall a cell's text stands (mm): its first line, then a line step for each next.</summary>
    public static double TextHeight(CellText t) => t.Lines.Count == 0 ? 0 : t.Size + (t.Lines.Count - 1) * t.Size * LineStep;

    static double TextWidth(string shown, double size) =>
        shown.Length == 0 ? 0 : shown.Replace("\r\n", "\n").Split('\n').Max(l => l.Length) * size * CharWidth;

    /// <summary>
    /// The table laid out: a column that fits its text is as wide as its widest single-column cell (padding both sides), within
    /// its min and max; a fixed one wraps its text at its width; a row that fits is as tall as its tallest single-row cell
    /// (a merged cell does not size what it spans), within its min and max.
    /// </summary>
    public static Layout Lay(Grid g, Values v, (string Font, bool Bold, bool Italic, string Color) text)
    {
        var colW = new double[g.C];
        for (var c = 0; c < g.C; c++)
        {
            var col = g.Cols[c];
            if (!col.Fit) { colW[c] = col.W; continue; }
            var need = 0.0;
            for (var r = 0; r < g.R; r++)
            {
                var cell = g.Cells[r, c];
                if (cell.Covered || cell.ColSpan != 1) continue;
                var size = cell.Num("size") is double s && s > 0 ? s : g.Size;
                need = Math.Max(need, TextWidth(v.Shown(r, c), size) + 2 * g.Pad);
            }
            colW[c] = Math.Clamp(need > 0 ? need : col.W, col.Min ?? 2, Math.Max(col.Min ?? 2, col.Max ?? 1000));
        }
        var texts = new CellText?[g.R, g.C];
        for (var r = 0; r < g.R; r++)
            for (var c = 0; c < g.C; c++)
            {
                var cell = g.Cells[r, c];
                if (cell.Covered) continue;
                var w = 0.0;
                for (var j = c; j < c + cell.ColSpan; j++) w += colW[j];
                texts[r, c] = Style(g, cell, v.Shown(r, c), w, text.Font, text.Bold, text.Italic, text.Color);
            }
        var rowH = new double[g.R];
        for (var r = 0; r < g.R; r++)
        {
            var row = g.Rows[r];
            if (!row.Fit) { rowH[r] = row.H; continue; }
            var need = 0.0;
            for (var c = 0; c < g.C; c++)
                if (texts[r, c] is { } t && g.Cells[r, c].RowSpan == 1) need = Math.Max(need, TextHeight(t) + 2 * g.Pad);
            // A row that fits is never shorter than its height (its min when it has one).
            rowH[r] = Math.Clamp(Math.Max(need, row.Min ?? row.H), 1, Math.Max(1, row.Max ?? 1000));
        }
        return new Layout(colW, rowH, texts);
    }

    // ------------------------------------------------------------ lines

    /// <summary>
    /// The line between two cells (or a cell and the table's edge): the heavier of the two cells' own sides when either has one
    /// (w 0: none), else the table's outline at its edge and its grid inside. Inside a merged cell there is none.
    /// </summary>
    public static Side? Edge(Grid g, (int R, int C)? a, string aSide, (int R, int C)? b, string bSide)
    {
        var ma = a is { } x ? g.Master[x.R, x.C] : ((int, int)?)null;
        var mb = b is { } y ? g.Master[y.R, y.C] : ((int, int)?)null;
        if (ma != null && ma == mb) return null;
        var sa = ma is { } p ? g.Cells[p.Item1, p.Item2].SideOf(aSide) : null;
        var sb = mb is { } q ? g.Cells[q.Item1, q.Item2].SideOf(bSide) : null;
        var own = sa == null ? sb : sb == null ? sa : sa.W >= sb.W ? sa : sb;
        var side = own ?? (ma == null || mb == null ? g.Outline : g.GridSide);
        return side.W > 0 ? side : null;
    }

    /// <summary>Every line of the table (mm, x right and y down from its top-left), joined where the next goes on with the same pen.</summary>
    public static List<(double X0, double Y0, double X1, double Y1, Side Pen)> Lines(Grid g, Layout l)
    {
        var o = new List<(double, double, double, double, Side)>();
        void Run(List<(double A, double B, Side? S)> parts, Func<double, double, (double, double, double, double)> at)
        {
            (double A, double B, Side S)? cur = null;
            foreach (var (a, b, s) in parts)
            {
                if (cur is { } k && s != null && s == k.S && Math.Abs(k.B - a) < 1e-9) { cur = (k.A, b, k.S); continue; }
                if (cur is { } done) { var (x0, y0, x1, y1) = at(done.A, done.B); o.Add((x0, y0, x1, y1, done.S)); }
                cur = s != null ? (a, b, s) : null;
            }
            if (cur is { } last) { var (x0, y0, x1, y1) = at(last.A, last.B); o.Add((x0, y0, x1, y1, last.S)); }
        }
        for (var i = 0; i <= g.R; i++)
        {
            var y = l.Y(i);
            var parts = new List<(double, double, Side?)>();
            for (var c = 0; c < g.C; c++)
                parts.Add((l.X(c), l.X(c + 1), Edge(g, i > 0 ? (i - 1, c) : null, "bottom", i < g.R ? (i, c) : null, "top")));
            Run(parts, (a, b) => (a, y, b, y));
        }
        for (var j = 0; j <= g.C; j++)
        {
            var x = l.X(j);
            var parts = new List<(double, double, Side?)>();
            for (var r = 0; r < g.R; r++)
                parts.Add((l.Y(r), l.Y(r + 1), Edge(g, j > 0 ? (r, j - 1) : null, "right", j < g.C ? (r, j) : null, "left")));
            Run(parts, (a, b) => (x, a, x, b));
        }
        return o;
    }

    // ------------------------------------------------------------ the JSON of a new table

    static JsonObject SideJson(double w, string c = "#000000") => new() { ["w"] = w, ["c"] = c };

    /// <summary>A preset's style for the table, and what it does to its header row (the first): bold, shaded.</summary>
    public static (JsonObject Style, bool Header) PresetStyle(string preset) => preset switch
    {
        "grid" => (new JsonObject { ["grid"] = SideJson(0.18), ["outline"] = SideJson(0.35) }, false),
        "minimal" => (new JsonObject { ["grid"] = SideJson(0), ["outline"] = SideJson(0) }, true),
        "none" => (new JsonObject { ["grid"] = SideJson(0), ["outline"] = SideJson(0) }, false),
        _ => (new JsonObject { ["grid"] = SideJson(0.18), ["outline"] = SideJson(0.35) }, true),
    };

    public const string HeaderFill = "#e6e6e6";

    /// <summary>A preset applied to a table's JSON: its lines, and its first row a header (bold; shaded, or ruled under in "minimal").</summary>
    public static void ApplyPreset(JsonObject t, string preset)
    {
        if (!Presets.Contains(preset)) throw new CommandException($"A table's preset is one of {string.Join(", ", Presets)}.");
        var (style, header) = PresetStyle(preset);
        var st = t["style"] as JsonObject ?? [];
        foreach (var (k, v) in style) st[k] = v?.DeepClone();
        t["style"] = st;
        t["preset"] = preset;
        var rows = (JsonArray)t["rows"]!;
        for (var r = 0; r < rows.Count; r++)
        {
            var row = (JsonObject)rows[r]!;
            var first = r == 0 && header;
            if (first) row["header"] = true; else row.Remove("header");
            if (r != 0) continue;
            var cells = (JsonArray)row["cells"]!;
            for (var c = 0; c < cells.Count; c++)
            {
                var cell = cells[c] as JsonObject;
                if (cell == null) { if (!first) continue; cells[c] = cell = new JsonObject(); }
                if (first)
                {
                    cell["bold"] = true;
                    if (preset == "minimal") { cell["bottom"] = SideJson(0.35); cell.Remove("fill"); }
                    else { cell["fill"] = HeaderFill; cell.Remove("bottom"); }
                }
                else { cell.Remove("bold"); cell.Remove("fill"); cell.Remove("bottom"); }
            }
        }
    }

    /// <summary>A new table's JSON: rows × columns of empty cells of a width and a height, rows fitting their text above that height.</summary>
    public static JsonObject New(int rows, int cols, double colWidth, double rowHeight, string preset)
    {
        if (rows < 1 || rows > MaxRows) throw new CommandException($"A table has 1 to {MaxRows} rows.");
        if (cols < 1 || cols > MaxCols) throw new CommandException($"A table has 1 to {MaxCols} columns.");
        if (colWidth is not (> 0 and <= 1000) || rowHeight is not (> 0 and <= 1000)) throw new CommandException("A column's width and a row's height are more than 0 and up to 1000 mm.");
        var t = new JsonObject
        {
            ["cols"] = new JsonArray([.. Enumerable.Range(0, cols).Select(_ => (JsonNode)new JsonObject { ["w"] = colWidth })]),
            ["rows"] = new JsonArray([.. Enumerable.Range(0, rows).Select(_ => (JsonNode)new JsonObject
            {
                ["h"] = rowHeight, ["fit"] = true,
                ["cells"] = new JsonArray([.. Enumerable.Range(0, cols).Select(_ => (JsonNode?)null)]),
            })]),
            ["style"] = new JsonObject { ["size"] = 2.5, ["pad"] = 1 },
        };
        ApplyPreset(t, preset);
        return t;
    }
}
