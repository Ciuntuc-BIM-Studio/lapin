using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// AutoSum in a table, as a spreadsheet has it (the Σ button, Alt+=): <c>{op: "autoSum", range, fn?}</c> (fn sum, average,
/// count, min or max) worked out on the table as it reads now, into the edits it makes (<see cref="TableEdits"/>), one undo
/// step with the others. One cell: an empty one takes the function of the numbers right above it — the block of cells reading
/// a number, stopping at a header row, an empty cell or a text — else of those right left of it; a cell reading a number
/// stands for its block. Several cells: under each of their columns that reads numbers, the function of them goes into the
/// first row below them empty across the columns, a row added at the foot when there is none. The range written follows rows
/// put in or taken out as every reference does, and a total takes in rows put in right above it (a list linked a row each).
/// </summary>
public static class TableAutoSum
{
    public static readonly string[] Functions = ["sum", "average", "count", "min", "max"];

    /// <summary>Whether the cell (r, c) reads a number (a header row's do not count).</summary>
    static bool Numeric(DraftingTables.Grid g, DraftingTables.Values v, int r, int c) =>
        r >= 0 && c >= 0 && r < g.R && c < g.C && !g.Rows[r].Header && !g.Cells[r, c].Covered && g.Cells[r, c].Text.Trim().Length > 0 && v.Value(r, c).Number != null;

    static string Formula(string fn, int r0, int c0, int r1, int c1) => $"{{= {fn}({TableEdits.Write(null, (r0, c0), (r1, c1))})}}";

    static JsonObject Set(int r, int c, string text) => new() { ["op"] = "set", ["cell"] = DraftingTables.Address(r, c), ["text"] = text };

    /// <summary>The edits an autoSum op makes of the table as it reads (<paramref name="g"/>, <paramref name="v"/>).</summary>
    public static List<JsonObject> Ops(DraftingTables.Grid g, DraftingTables.Values v, JsonObject op)
    {
        var fn = ((string?)op["fn"] ?? "sum").ToLowerInvariant();
        if (!Functions.Contains(fn)) throw new CommandException($"AutoSum takes {string.Join(", ", Functions)}.");
        var b = DraftingTables.ParseRange((string?)op["range"] ?? (string?)op["cell"]) ?? throw new CommandException("Which cells? A range, such as B5 or B2:D9.");
        if (b.R0 >= g.R || b.C0 >= g.C) throw new CommandException("The cells are past the table.");
        var (r1, c1) = (Math.Min(b.R1, g.R - 1), Math.Min(b.C1, g.C - 1));
        var (mr, mc) = g.Master[b.R0, b.C0];
        var one = (b.R0, b.C0) == (r1, c1) || g.Cells[mr, mc].RowSpan - 1 + mr >= r1 && g.Cells[mr, mc].ColSpan - 1 + mc >= c1;
        if (one && g.Cells[mr, mc].Text.Trim().Length == 0)
        {
            // Up the numbers right above it, else left along those right left of it.
            var top = mr;
            while (Numeric(g, v, top - 1, mc)) top--;
            if (top < mr) return [Set(mr, mc, Formula(fn, top, mc, mr - 1, mc))];
            var left = mc;
            while (Numeric(g, v, mr, left - 1)) left--;
            if (left < mc) return [Set(mr, mc, Formula(fn, mr, left, mr, mc - 1))];
            throw new CommandException($"No numbers right above {DraftingTables.Address(mr, mc)} or left of it to take the {fn} of.");
        }
        var (r0, c0) = (b.R0, b.C0);
        if (one)
        {
            // A cell reading a number: its block of numbers down its column.
            if (!Numeric(g, v, mr, mc)) throw new CommandException($"{DraftingTables.Address(mr, mc)} reads no number to take the {fn} of.");
            (r0, c0, r1, c1) = (mr, mc, mr, mc);
            while (Numeric(g, v, r0 - 1, mc)) r0--;
            while (Numeric(g, v, r1 + 1, mc)) r1++;
        }
        // Each column's numbers among the cells.
        var cols = new List<(int C, int From, int To)>();
        for (var c = c0; c <= c1; c++)
        {
            var rows = Enumerable.Range(r0, r1 - r0 + 1).Where(r => Numeric(g, v, r, c)).ToList();
            if (rows.Count > 0) cols.Add((c, rows[0], rows[^1]));
        }
        if (cols.Count == 0) throw new CommandException($"No numbers in {TableEdits.Write(null, (r0, c0), (r1, c1))} to take the {fn} of.");
        // The first row below them empty across those columns; a row at the foot when none is.
        var ops = new List<JsonObject>();
        var at = r1 + 1;
        while (at < g.R && cols.Any(x => g.Cells[at, x.C].Covered || g.Cells[at, x.C].Text.Trim().Length > 0)) at++;
        if (at >= g.R) ops.Add(new JsonObject { ["op"] = "insertRows", ["at"] = g.R, ["count"] = 1 });
        foreach (var (c, from, to) in cols) ops.Add(Set(at, c, Formula(fn, from, c, to, c)));
        return ops;
    }

    /// <summary>
    /// The edits asked with each autoSum among them worked out on the table as the edits before it leave it (<paramref name="read"/>
    /// gives a table's JSON as it reads): only edits TableEdits.Apply knows.
    /// </summary>
    public static JsonArray Expand(JsonObject? table, JsonArray ops, IReadOnlyList<TableStyleDef>? styles, Func<string, bool>? exists, Func<JsonObject, (DraftingTables.Grid, DraftingTables.Values)> read)
    {
        if (!ops.OfType<JsonObject>().Any(o => (string?)o["op"] == "autoSum")) return ops;
        var o = new JsonArray();
        foreach (var n in ops)
        {
            if (n is JsonObject op && (string?)op["op"] == "autoSum")
            {
                var (g, v) = read(TableEdits.Apply(table, (JsonArray)o.DeepClone(), styles, exists));
                foreach (var x in Ops(g, v, op)) o.Add((JsonNode)x);
            }
            else o.Add(n?.DeepClone());
        }
        return o;
    }
}
