using System.Text.RegularExpressions;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The project's drafting tables as one formula reads them (docs/plan-drafting-tables.md §3, stage 2): another table's cell by
/// its name and address (<c>table.Areas.B4</c>, a token <c>{table.Areas.B4}</c> too), a range of it among a function's values
/// (<c>sum(table.Areas.B2:B9)</c>, or <c>table.Areas.B2:table.Areas.B9</c>). A table is found by its name, case aside: one of
/// that name on the same view (or sheet) first, so a sheet duplicated with its tables reads its own; else the only one of the
/// project. Each table read is worked out once per drawing (<see cref="Of"/>), and the guard against formulas going round in a
/// circle is kept across tables (<see cref="Busy"/>): a cell reading back into the cell that asked for it, through another table or not, does not read.
/// </summary>
public sealed partial class TableScope(IModelView m, Func<Entity, Func<string, (bool Known, string? Value)>?>? outerFor = null, Func<DraftingTables.Grid, DraftingTables.CellNames?>? modelFor = null)
{
    public const string Prefix = "table.";

    /// <summary>The cells being worked out now, of every table: (table id, row, column).</summary>
    internal HashSet<(string, int, int)> Busy { get; } = [];

    readonly Dictionary<string, DraftingTables.Values> made = [];

    public IModelView Model => m;

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]{0,63}$")]
    private static partial Regex Word();

    /// <summary>Whether a table's name can be referenced: a word of letters, digits and _ (not starting with a digit), 64 at most.</summary>
    public static bool IsName(string? name) => name != null && Word().IsMatch(name);

    /// <summary>Whether a name is a reference to another table's cell (<c>table.&lt;name&gt;.&lt;cell&gt;</c>).</summary>
    public static bool IsRef(string name) => Split(name) != null;

    /// <summary>A reference's table name and what follows it (its cell's address), or null when it is not one.</summary>
    public static (string Table, string Cell)? Split(string name)
    {
        if (!name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var dot = name.IndexOf('.', Prefix.Length);
        if (dot < 0) return null;
        var table = name[Prefix.Length..dot];
        return IsName(table) && dot + 1 < name.Length ? (table, name[(dot + 1)..]) : null;
    }

    /// <summary>The project's drafting tables.</summary>
    public static IEnumerable<Entity> Tables(IModelView m) => m.OfClass(IfcClass.Annotation).Where(e => e.PredefinedType == DraftingTables.Predefined);

    /// <summary>
    /// The table a name reads from <paramref name="from"/>: the one of that name on the same view, else the only one in the project;
    /// null with why (none, or several on other views).
    /// </summary>
    public static Entity? Find(IModelView m, string name, Entity? from, out string? why)
    {
        why = null;
        var named = Tables(m).Where(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (named.Count == 1) return named[0];
        if (named.Count == 0) { why = $"there is no table „{name}”"; return null; }
        var here = from?.Str("viewId") is { } v ? named.Where(e => e.Str("viewId") == v).ToList() : [];
        if (here.Count == 1) return here[0];
        why = $"{named.Count} tables are called „{name}”: rename one";
        return null;
    }

    /// <summary>A table worked out (its sheet's or the model's tokens for it), once per scope.</summary>
    public DraftingTables.Values Of(Entity table)
    {
        if (made.TryGetValue(table.Id, out var v)) return v;
        var g = DraftingTables.Read(table);
        return made[table.Id] = new DraftingTables.Values(g, outerFor?.Invoke(table), this, table, null, modelFor?.Invoke(g));
    }

    /// <summary>Another table's cell as a reference names it: its text as shown and its value (an empty cell 0); null, with why in <paramref name="errors"/>, when it does not read.</summary>
    internal (string Shown, XValue Value)? Cell(Entity? from, string reference, List<string> errors)
    {
        if (Split(reference) is not { } s) return null;
        if (DraftingTables.Parse(s.Cell) is not { } at) { errors.Add($"{reference}: „{s.Cell}” is not a cell."); return null; }
        if (Find(m, s.Table, from, out var why) is not { } t) { errors.Add($"{reference}: {why}."); return null; }
        var v = Of(t);
        if (v.Busy(at.R, at.C))
        {
            // Said on both sides: where the circle closes, and in the table it comes back to.
            errors.Add($"{reference} reads back into the cell asking for it.");
            if (!ReferenceEquals(v.Errors, errors)) v.Errors.Add($"{DraftingTables.Address(at.R, at.C)} reads back into itself through another table.");
            return null;
        }
        if (v.Local(at.R, at.C, reference) is not { } x) return null;
        return (v.Shown(at.R, at.C), x);
    }

    /// <summary>A range of another table among a function's values: its non-empty cells, row by row; null when it does not read.</summary>
    internal IReadOnlyList<XValue>? Range(Entity? from, string first, string last, List<string> errors)
    {
        if (Split(first) is not { } a) return null;
        var end = Split(last) is { } b ? (string.Equals(b.Table, a.Table, StringComparison.OrdinalIgnoreCase) ? b.Cell : null) : last;
        if (end == null || DraftingTables.ParseRange(a.Cell + ":" + end) is not { } box) { errors.Add($"{first}:{last} is not a range of one table."); return null; }
        if (Find(m, a.Table, from, out var why) is not { } t) { errors.Add($"{first}:{last}: {why}."); return null; }
        return Of(t).Cells(box, Prefix + a.Table + ".");
    }
}
