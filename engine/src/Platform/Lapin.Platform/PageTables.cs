using System.Text.Json.Nodes;

namespace Lapin.Platform;

/// <summary>
/// A table a module puts in the page Lapin exports for the browser (decision 482, docs/html-export.md): the cost
/// module's estimate. The page shows it in a tab of its own, sortable, a row's elements highlighted when it is
/// clicked. Labels in English: the page translates them as the interface does.
/// </summary>
public interface IPageTables
{
    /// <summary>The table, worked out now; null when there is nothing to show (no estimate yet).</summary>
    PageTable? Table(IQueryContext q);
}

/// <summary>A column: its key in a row's cells, its label, and how it reads: text, number (three decimals), money (two).</summary>
public sealed record PageColumn(string Key, string Label, string Kind = "text");

/// <summary>A row: its cells by column (texts or numbers), the elements it counts, and whether it heads a group (a chapter).</summary>
public sealed record PageRow(IReadOnlyList<JsonNode?> Cells, IReadOnlyList<string> Elements, bool Heading = false);

/// <summary>A total under the table: its label, its value, and the rate it is worked out at (a percentage), if any.</summary>
public sealed record PageTotal(string Label, double Value, double? Percent = null);

/// <summary>
/// A module's table: its key, title and note (where it comes from), whether its values are indicative (Lapin's own,
/// not the user's: marked as the app marks them), its columns, rows and totals, the unit its money is in, and the user's
/// name for it (an estimate's), shown as it is.
/// </summary>
public sealed record PageTable(string Key, string Title, string? Note, bool Indicative, IReadOnlyList<PageColumn> Columns, IReadOnlyList<PageRow> Rows,
    IReadOnlyList<PageTotal> Totals, string? Currency = null, string? Name = null)
{
    public JsonObject ToJson() => new()
    {
        ["key"] = Key, ["title"] = Title, ["name"] = Name, ["note"] = Note, ["indicative"] = Indicative, ["currency"] = Currency,
        ["columns"] = new JsonArray(Columns.Select(c => (JsonNode)new JsonObject { ["key"] = c.Key, ["label"] = c.Label, ["kind"] = c.Kind }).ToArray()),
        ["rows"] = new JsonArray(Rows.Select(r => (JsonNode)new JsonObject
        {
            ["cells"] = new JsonArray(r.Cells.Select(c => c?.DeepClone()).ToArray()),
            ["elements"] = new JsonArray(r.Elements.Select(e => (JsonNode)e).ToArray()),
            ["heading"] = r.Heading ? true : null,
        }).ToArray()),
        ["totals"] = new JsonArray(Totals.Select(t => (JsonNode)new JsonObject { ["label"] = t.Label, ["value"] = t.Value, ["percent"] = t.Percent }).ToArray()),
    };
}
