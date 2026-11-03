using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Schedules;

/// <summary>A column of an editable table: its key (<c>c:</c> and the model's label, or <c>custom:</c> and the schedule's own id) and label.</summary>
public sealed record GridColumn(string Key, string Label, bool Custom);

/// <summary>A row of an editable table: its key (<c>r:</c> what it lists, or its first cells, and which of that; <c>m:</c> a row of the schedule's own), its cells, the ones typed over the model's.</summary>
public sealed record GridRow(string Key, bool Manual, List<string> Cells, HashSet<string> Typed);

/// <summary>A module's table as the schedule shows it: its columns and rows with the schedule's own added and typed over.</summary>
public sealed record EditableGrid(string Title, List<GridColumn> Columns, List<GridRow> Rows)
{
    public ScheduleGrid ToGrid() => new(Title, Columns.Select(c => c.Label).ToList(), Rows.Select(r => (IReadOnlyList<string>)r.Cells).ToList());
}

/// <summary>
/// A module's table (a room, rebar or timber schedule) made the user's own (decision 251): columns of its own after the
/// module's, rows of its own before the totals line, any cell typed over the model's value (kept by what the row lists, so
/// it stays with its room while the model changes), and its rows sorted by a column.
/// </summary>
public static class ScheduleGrids
{
    public const string ManualRow = "m:";

    public static List<(string Id, string Label)> CustomColumns(Entity s) => s.Attrs["customColumns"] is JsonArray a
        ? a.OfType<JsonObject>().Where(o => (string?)o["id"] != null).Select(o => ((string)o["id"]!, (string?)o["label"] ?? "")).ToList() : [];

    public static List<string> ManualRows(Entity s) => s.Attrs["manualRows"] is JsonArray a ? a.Select(x => (string?)x).OfType<string>().ToList() : [];

    public static EditableGrid Apply(ScheduleGrid grid, Entity s)
    {
        var table = ScheduleTable.Of(s);
        var columns = grid.Columns.Select(c => new GridColumn("c:" + c, c, false)).ToList();
        columns.AddRange(CustomColumns(s).Select(c => new GridColumn("custom:" + c.Id, c.Label, true)));
        GridRow Row(string key, bool manual, IReadOnlyList<string> model)
        {
            var typed = table.Cells.GetValueOrDefault(key);
            var set = new HashSet<string>();
            var cells = columns.Select((c, i) =>
            {
                if (typed?.TryGetValue(c.Key, out var t) == true) { if (!c.Custom && !manual) set.Add(c.Key); return t; }
                return !c.Custom && i < model.Count ? model[i] : "";
            }).ToList();
            return new GridRow(key, manual, cells, set);
        }
        var seen = new Dictionary<string, int>();
        var rows = new List<GridRow>();
        GridRow? total = null;
        for (var i = 0; i < grid.Rows.Count; i++)
        {
            var r = grid.Rows[i];
            // What the row lists, when the module says (a room's id); else its first cells, and which of that name.
            var first = grid.Keys is { } ks && i < ks.Count && ks[i] is { } id ? id : string.Join("|", r.Take(3));
            var n = seen[first] = seen.GetValueOrDefault(first) + 1;
            var row = Row($"r:{first}#{n}", false, r);
            if (i == grid.Rows.Count - 1 && grid.Rows.Count > 1 && ScheduleCharts.IsTotal(r)) total = row; else rows.Add(row);
        }
        rows.AddRange(ManualRows(s).Select(id => Row(ManualRow + id, true, [])));
        if (s.Str("sortBy") is { } by && columns.FindIndex(c => c.Key == by) is var ci and >= 0)
        {
            var desc = s.Attrs["sortDesc"] is JsonValue d && d.TryGetValue<bool>(out var b) && b;
            int Compare(GridRow x, GridRow y)
            {
                var (a, c) = (x.Cells[ci], y.Cells[ci]);
                var r = ScheduleCharts.Number(a) is { } na && ScheduleCharts.Number(c) is { } nc ? na.CompareTo(nc)
                    : string.Compare(a, c, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.NumericOrdering);
                return desc ? -r : r;
            }
            rows = [.. rows.Order(Comparer<GridRow>.Create(Compare))];
        }
        if (total != null) rows.Add(total);
        return new EditableGrid(grid.Title, columns, rows);
    }

    public static JsonObject ToJson(EditableGrid g) => new()
    {
        ["columns"] = new JsonArray(g.Columns.Select(c => (JsonNode)new JsonObject { ["key"] = c.Key, ["label"] = c.Label, ["custom"] = c.Custom }).ToArray()),
        ["rows"] = new JsonArray(g.Rows.Select(r => (JsonNode)new JsonObject
        {
            ["key"] = r.Key, ["manual"] = r.Manual,
            ["cells"] = new JsonArray(r.Cells.Select(c => (JsonNode)c).ToArray()),
            ["typed"] = new JsonArray(r.Typed.Select(t => (JsonNode)t).ToArray()),
        }).ToArray()),
    };
}
