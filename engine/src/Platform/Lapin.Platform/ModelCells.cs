using Lapin.Model;
using Lapin.Platform.Documents;

namespace Lapin.Platform;

/// <summary>
/// What a drafting table's cell reads of the model (docs/plan-drafting-tables.md §3b, stage 4), worked out on every ask and never
/// stored: <c>el.&lt;field&gt;</c>, a field of the element the cell is linked to (its own link, else its row's, else the table's),
/// read through the one field dictionary (<see cref="ElementFields"/>, as schedules and filters read fields: a token shows the
/// text a tag writes, a formula reads the number in SI); <c>schedule.&lt;name&gt;.…</c>, a schedule of the project as it is laid
/// out (<c>count</c>, <c>total.&lt;column&gt;</c>, <c>sum|average|min|max.&lt;column&gt;</c>, a cell <c>B3</c> with its headings
/// as row 1); <c>filter.&lt;name&gt;.count</c> and <c>filter.&lt;name&gt;.sum|average|min|max.&lt;field&gt;</c> over the elements
/// one of the project's view filters takes, through the one <see cref="ElementFilter"/>. One reader per drawing: what it reads
/// (an element's fields, a schedule's grid, a filter's elements) is read once.
/// </summary>
public sealed class ModelCells(IQueryContext q)
{
    public static readonly string[] Aggregates = ["sum", "average", "min", "max"];

    readonly Dictionary<string, ElementFields> fields = [];
    readonly Dictionary<string, ScheduleGrid?> schedules = [];
    readonly Dictionary<string, List<Entity>> filtered = [];
    List<DocTableChoice>? choices;

    /// <summary>The names this reader knows a cell's name by (its first word).</summary>
    public static bool Reads(string name) =>
        name.StartsWith("el.", StringComparison.OrdinalIgnoreCase) || name.StartsWith("schedule.", StringComparison.OrdinalIgnoreCase) || name.StartsWith("filter.", StringComparison.OrdinalIgnoreCase);

    /// <summary>A table's cells' reader of the model: a name in the cell (r, c) of <paramref name="g"/>, with why it does not read in <c>errors</c>.</summary>
    public DraftingTables.CellNames For(DraftingTables.Grid g) => (r, c, name, errors) => Read(g, r, c, name, errors);

    static (bool Known, string? Shown, XValue? Value) Unknown => (false, null, null);

    static (bool, string?, XValue?) Number(double n) => (true, Expr.Format(n, null), XValue.Of(n));

    (bool Known, string? Shown, XValue? Value) Read(DraftingTables.Grid g, int r, int c, string name, List<string> errors)
    {
        var dot = name.IndexOf('.');
        if (dot < 0 || dot == name.Length - 1) return Unknown;
        var (head, rest) = (name[..dot].ToLowerInvariant(), name[(dot + 1)..]);
        var at = DraftingTables.Address(r, c);
        switch (head)
        {
            case "el":
            {
                if (g.LinkOf(r, c) is not { } id) { errors.Add($"{at}: {name}: the cell reads no element (Link elements…)."); return Unknown; }
                if (q.Model.Get(id) is not { } e) { errors.Add($"{at}: {name}: its element is gone."); return Unknown; }
                if (!fields.TryGetValue(id, out var f)) fields[id] = f = new ElementFields(q, e);
                var v = f.Value(rest);
                // A number reads as itself in a formula (SI, as a schedule's formula reads it); a field it does not have is empty.
                return (true, v.Shown, v.Number is { } n ? XValue.Of(n) : XValue.Of(v.Shown));
            }
            case "schedule": return Schedule(rest, at, errors);
            case "filter": return Filter(rest, at, errors);
            default: return Unknown;
        }
    }

    /// <summary>A name as names are compared here: case, spaces and punctuation aside.</summary>
    static string Squash(string s) => TagText.Squash(s);

    (bool, string?, XValue?) Schedule(string rest, string at, List<string> errors)
    {
        var parts = rest.Split('.');
        if (parts.Length < 2) { errors.Add($"{at}: schedule.{rest}: say what of it (count, total.<column>, sum.<column>, a cell)."); return Unknown; }
        var tables = q.Services.Get<IDocumentTables>() ?? new ScheduleSourceTables();
        choices ??= [.. tables.Choices(q).Where(x => x.Source.StartsWith("schedule:", StringComparison.Ordinal))];
        var key = parts[0];
        var hit = choices.Where(x => x.Source == "schedule:" + key || Squash(x.Label) == Squash(key)).ToList();
        if (hit.Count != 1) { errors.Add(hit.Count == 0 ? $"{at}: there is no schedule „{key}”." : $"{at}: {hit.Count} schedules are called „{key}”: rename one."); return Unknown; }
        var source = hit[0].Source;
        if (!schedules.TryGetValue(source, out var grid)) schedules[source] = grid = tables.Table(q, source);
        if (grid == null) { errors.Add($"{at}: the schedule „{key}” does not read."); return Unknown; }
        var what = parts[1].ToLowerInvariant();
        var keys = grid.Keys;
        bool Element(int i) => keys == null || i < keys.Count && keys[i] != null;
        var items = Enumerable.Range(0, grid.Rows.Count).Where(Element).ToList();
        if (what == "count" && parts.Length == 2) return Number(items.Count);
        // A cell of the schedule as printed: its headings row 1.
        if (parts.Length == 2 && DraftingTables.Parse(parts[1]) is { } cell)
        {
            var text = cell.R == 0 ? (cell.C < grid.Columns.Count ? grid.Columns[cell.C] : null)
                : cell.R - 1 < grid.Rows.Count && cell.C < grid.Rows[cell.R - 1].Count ? grid.Rows[cell.R - 1][cell.C] : null;
            if (text == null) { errors.Add($"{at}: the schedule „{key}” has no cell {parts[1]}."); return Unknown; }
            return (true, text, XValue.Read(text));
        }
        if (parts.Length < 3 || (what != "total" && !Aggregates.Contains(what))) { errors.Add($"{at}: schedule.{rest}: count, total.<column>, sum|average|min|max.<column> or a cell (B3)."); return Unknown; }
        var col = string.Join('.', parts[2..]);
        var ci = ColumnOf(grid.Columns, col);
        if (ci < 0) { errors.Add($"{at}: the schedule „{key}” has no column „{col}”."); return Unknown; }
        double? Num(int i) => ci < grid.Rows[i].Count ? XValue.Read(grid.Rows[i][ci]).Number : null;
        if (what == "total" && keys != null)
        {
            // Its own totals line as printed (the last one: the whole schedule's), else the column's sum.
            var totals = Enumerable.Range(0, grid.Rows.Count).Where(i => !Element(i)).ToList();
            if (totals.Count > 0 && ci < grid.Rows[totals[^1]].Count && grid.Rows[totals[^1]][ci] is { Length: > 0 } printed) return (true, printed, XValue.Read(printed));
        }
        var nums = items.Select(Num).OfType<double>().ToList();
        return Aggregate(what == "total" ? "sum" : what, nums, $"{at}: schedule.{rest}", errors);
    }

    static (bool, string?, XValue?) Aggregate(string what, List<double> nums, string where, List<string> errors)
    {
        if (what != "sum" && nums.Count == 0) { errors.Add($"{where}: no numbers to take the {what} of."); return Unknown; }
        return Number(what switch { "average" => nums.Average(), "min" => nums.Min(), "max" => nums.Max(), _ => nums.Sum() });
    }

    /// <summary>A column of a schedule by its heading (case, accents, units in brackets and punctuation aside) or its letter; -1 when none.</summary>
    static int ColumnOf(IReadOnlyList<string> columns, string col)
    {
        var want = TableImport.Norm(col);
        for (var i = 0; i < columns.Count; i++) if (TableImport.Norm(columns[i]) == want && want.Length > 0) return i;
        if (col.Length is >= 1 and <= 3 && col.All(char.IsLetter) && DraftingTables.Parse(col + "1") is { } p && p.C < columns.Count) return p.C;
        return -1;
    }

    (bool, string?, XValue?) Filter(string rest, string at, List<string> errors)
    {
        var parts = rest.Split('.');
        if (parts.Length < 2) { errors.Add($"{at}: filter.{rest}: count, or sum|average|min|max.<field>."); return Unknown; }
        var key = parts[0];
        var hit = ViewFilters.All(q.Model).Where(f => f.Id == key || Squash(f.Name) == Squash(key)).ToList();
        if (hit.Count != 1) { errors.Add(hit.Count == 0 ? $"{at}: there is no filter „{key}”." : $"{at}: {hit.Count} filters are called „{key}”: rename one."); return Unknown; }
        var f = hit[0];
        if (!filtered.TryGetValue(f.Id, out var els)) filtered[f.Id] = els = ElementFilter.Elements(q, ViewFilters.ToFilter(f.Rules, f.Any));
        var what = parts[1].ToLowerInvariant();
        if (what == "count" && parts.Length == 2) return Number(els.Count);
        if (parts.Length < 3 || !Aggregates.Contains(what)) { errors.Add($"{at}: filter.{rest}: count, or sum|average|min|max.<field>."); return Unknown; }
        var field = string.Join('.', parts[2..]);
        var nums = els.Select(e => (fields.TryGetValue(e.Id, out var ef) ? ef : fields[e.Id] = new ElementFields(q, e)).Value(field).Number).OfType<double>().ToList();
        return Aggregate(what, nums, $"{at}: filter.{rest}", errors);
    }
}
