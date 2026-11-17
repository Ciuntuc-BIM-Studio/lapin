using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Documents;

namespace Lapin.Drafting;

/// <summary>
/// What fits where the caret is while a table's cell is typed (docs/plan-drafting-tables.md §3b): inside <c>{= …}</c> the
/// functions with how they are called and the names a cell reads; inside a token <c>{…}</c> the names alone. A name is offered
/// as the evaluator reads it (<see cref="ModelCells"/>, <see cref="TableScope"/>): <c>el.</c> and the linked element's fields
/// with their values now (its quantities and their common names too, <see cref="FieldCatalog.Available"/>); <c>schedule.</c>
/// and the project's schedules, what of one (<c>count</c>, <c>total.</c>, <c>sum.</c>…) and its columns; <c>filter.</c> and the
/// view filters, <c>count</c> and the sums of their elements' number fields; <c>table.</c> and the other tables by name, then
/// their cells with what they read. Worked out on every ask; the word typed before the caret filters the list and is what an
/// accepted suggestion replaces (<c>from</c>, <c>to</c>).
/// </summary>
public static class TableSuggest
{
    public const int Max = 200;

    sealed record Item(string Insert, string Label, string? Detail, string Kind);

    static readonly string[] Roots = ["el.", "schedule.", "filter.", "table."];
    static readonly Dictionary<string, string> RootDetail = new()
    {
        ["el."] = "a field of the element the cell is linked to",
        ["schedule."] = "a schedule of the project, by its name",
        ["filter."] = "the elements a view filter takes",
        ["table."] = "another table's cell, by its name",
    };

    /// <summary>The suggestions for <paramref name="text"/> with its caret at <paramref name="caret"/> in the cell (r, c) of table <paramref name="table"/>.</summary>
    public static JsonObject For(IQueryContext q, Entity table, int r, int c, string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        var before = text[..caret];
        var none = new JsonObject { ["from"] = caret, ["to"] = caret, ["items"] = new JsonArray() };
        // The innermost brace still open before the caret.
        var open = new Stack<int>();
        for (var i = 0; i < before.Length; i++)
            if (before[i] == '{') open.Push(i);
            else if (before[i] == '}' && open.Count > 0) open.Pop();
        if (open.Count == 0) return none;
        var inner = before[(open.Peek() + 1)..];
        var formula = inner.TrimStart().StartsWith('=');
        // Inside a quoted text, or past the bar of a format: nothing to suggest.
        if (formula && (inner.Count(ch => ch == '"') % 2 == 1 || inner.Count(ch => ch == '\'') % 2 == 1 || inner.Contains('|'))) return none;
        var start = caret;
        while (start > 0 && (char.IsLetterOrDigit(before[start - 1]) || before[start - 1] is '_' or '.')) start--;
        var word = before[start..];
        // A word that begins inside the braces only (a number typed is no name).
        if (start <= open.Peek() || word.Length > 0 && char.IsDigit(word[0])) return none;

        string? note = null;
        var items = new List<Item>();
        var dot = word.IndexOf('.');
        if (dot < 0)
        {
            foreach (var root in Roots) items.Add(new Item(root, root, RootDetail[root], "root"));
            if (formula)
                foreach (var f in Expr.FunctionNames) items.Add(new Item(f + "(", f, Expr.Signatures.TryGetValue(f, out var sig) ? sig : f + "(…)", "function"));
        }
        else
        {
            var head = word[..dot].ToLowerInvariant();
            switch (head)
            {
                case "el": note = Element(q, table, r, c, items); break;
                case "schedule": Schedule(q, word, items); break;
                case "filter": Filter(q, word, items); break;
                case "table": Table(q, table, word, items); break;
            }
        }
        // A formula reads names of letters, digits, _ and dots only: a field named otherwise is for a token.
        if (formula) items = [.. items.Where(x => x.Kind == "function" || x.Insert.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '.'))];
        var list = Filter(items, word);
        return new JsonObject
        {
            ["from"] = start, ["to"] = caret, ["word"] = word, ["formula"] = formula, ["note"] = list.Count == 0 ? note : null,
            ["items"] = new JsonArray([.. list.Take(Max).Select(x => (JsonNode)new JsonObject { ["insert"] = x.Insert, ["label"] = x.Label, ["detail"] = x.Detail, ["kind"] = x.Kind })]),
        };
    }

    /// <summary>The items the word typed starts (case aside), then those whose last part holds it, each once.</summary>
    static List<Item> Filter(List<Item> items, string word)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var o = items.Where(x => x.Insert.StartsWith(word, StringComparison.OrdinalIgnoreCase) && seen.Add(x.Insert)).ToList();
        var dot = word.LastIndexOf('.');
        if (dot >= 0 && dot < word.Length - 1)
        {
            var (stem, tail) = (word[..(dot + 1)], TagText.Squash(word[(dot + 1)..]));
            o.AddRange(items.Where(x => x.Insert.StartsWith(stem, StringComparison.OrdinalIgnoreCase) && TagText.Squash(x.Insert[stem.Length..]).Contains(tail, StringComparison.Ordinal) && seen.Add(x.Insert)));
        }
        return o;
    }

    /// <summary>A name as a formula can write it: its words joined, each from a capital (<c>Wall schedule</c> → <c>WallSchedule</c>), accents and units in brackets left out; null when nothing of it is left or it starts with a digit.</summary>
    public static string? Word(string s)
    {
        var open = s.IndexOfAny(['(', '[']);
        if (open > 0) s = s[..open];
        var sb = new StringBuilder();
        var up = false;
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch) || ch == '_') { sb.Append(up && sb.Length > 0 ? char.ToUpperInvariant(ch) : ch); up = false; }
            else up = true;
        }
        return sb.Length == 0 || char.IsDigit(sb[0]) ? null : sb.ToString();
    }

    static string? Element(IQueryContext q, Entity table, int r, int c, List<Item> items)
    {
        var g = DraftingTables.Read(table);
        if (r >= g.R || c >= g.C || g.LinkOf(r, c) is not { } id) return "The cell reads no element yet: link it first (Link elements…).";
        if (q.Model.Get(id) is not { } e) return "The element the cell reads is gone.";
        var available = FieldCatalog.Available(q, e);
        foreach (var group in (available["groups"] as JsonArray ?? []).OfType<JsonObject>())
            foreach (var f in (group["fields"] as JsonArray ?? []).OfType<JsonObject>())
                if ((string?)f["token"] is { Length: > 0 } token)
                {
                    var value = (string?)f["value"];
                    items.Add(new Item("el." + token, token, value is { Length: > 0 } ? value : (string?)group["label"], "field"));
                }
        return null;
    }

    static (IDocumentTables Tables, List<DocTableChoice> Schedules) Schedules(IQueryContext q)
    {
        var tables = q.Services.Get<IDocumentTables>() ?? new ScheduleSourceTables();
        return (tables, [.. tables.Choices(q).Where(x => x.Source.StartsWith("schedule:", StringComparison.Ordinal))]);
    }

    /// <summary>How a schedule, a filter or a table is named in a formula: its name as a word when that names it alone, else its id.</summary>
    static string Key(string label, string id, IEnumerable<string> others)
    {
        var w = Word(label);
        return w != null && others.Count(o => TagText.Squash(o) == TagText.Squash(w)) == 1 ? w : id;
    }

    static void Schedule(IQueryContext q, string word, List<Item> items)
    {
        var parts = word.Split('.');
        var (tables, schedules) = Schedules(q);
        var labels = schedules.Select(x => x.Label).ToList();
        if (parts.Length <= 2)
        {
            foreach (var s in schedules) items.Add(new Item($"schedule.{Key(s.Label, s.Source["schedule:".Length..], labels)}.", s.Label, "a schedule", "schedule"));
            return;
        }
        var key = parts[1];
        var hit = schedules.Where(x => x.Source == "schedule:" + key || TagText.Squash(x.Label) == TagText.Squash(key)).ToList();
        if (hit.Count != 1 || tables.Table(q, hit[0].Source) is not { } grid) return;
        var stem = $"schedule.{key}.";
        if (parts.Length == 3)
        {
            var count = grid.Keys?.Count(k => k != null) ?? grid.Rows.Count;
            items.Add(new Item(stem + "count", "count", count.ToString(CultureInfo.InvariantCulture), "what"));
            items.Add(new Item(stem + "total.", "total", "a column's total, as the schedule prints it", "what"));
            foreach (var a in ModelCells.Aggregates) items.Add(new Item(stem + a + ".", a, $"the {a} of a column", "what"));
            for (var i = 0; i < grid.Columns.Count; i++) items.Add(new Item(stem + DraftingTables.Address(0, i), DraftingTables.Address(0, i), grid.Columns[i], "cell"));
            return;
        }
        var what = parts[2].ToLowerInvariant();
        if (what != "total" && !ModelCells.Aggregates.Contains(what)) return;
        for (var i = 0; i < grid.Columns.Count; i++)
        {
            var heading = grid.Columns[i];
            var col = Word(heading) is { } w && TableImport.Norm(w) == TableImport.Norm(heading) ? w : DraftingTables.ColName(i);
            items.Add(new Item($"{stem}{parts[2]}.{col}", col, heading, "column"));
        }
    }

    static readonly string[] Numbers = ["Length", "Width", "Height", .. QuantityNames.Offered];

    static void Filter(IQueryContext q, string word, List<Item> items)
    {
        var parts = word.Split('.');
        var filters = ViewFilters.All(q.Model);
        var names = filters.Select(f => f.Name).ToList();
        if (parts.Length <= 2)
        {
            foreach (var f in filters) items.Add(new Item($"filter.{Key(f.Name, f.Id, names)}.", f.Name, "a view filter", "filter"));
            return;
        }
        var key = parts[1];
        var hit = filters.Where(f => f.Id == key || TagText.Squash(f.Name) == TagText.Squash(key)).ToList();
        if (hit.Count != 1) return;
        var stem = $"filter.{key}.";
        var filter = ViewFilters.ToFilter(hit[0].Rules, hit[0].Any);
        if (parts.Length == 3)
        {
            items.Add(new Item(stem + "count", "count", ElementFilter.Elements(q, filter).Count.ToString(CultureInfo.InvariantCulture), "what"));
            foreach (var a in ModelCells.Aggregates) items.Add(new Item(stem + a + ".", a, $"the {a} of a field over its elements", "what"));
            return;
        }
        var what = parts[2].ToLowerInvariant();
        if (!ModelCells.Aggregates.Contains(what)) return;
        // The number fields its elements have (a sample of them), after the common ones.
        foreach (var n in Numbers) items.Add(new Item($"{stem}{parts[2]}.{n}", n, "a number field", "field"));
        foreach (var g in (FieldCatalog.Across(q, filter, 200, 1)["groups"] as JsonArray ?? []).OfType<JsonObject>())
            foreach (var f in (g["fields"] as JsonArray ?? []).OfType<JsonObject>())
                if ((string?)f["kind"] == "number" && (string?)f["token"] is { Length: > 0 } token && !token.Contains(' '))
                    items.Add(new Item($"{stem}{parts[2]}.{token}", token, (string?)g["label"], "field"));
    }

    static void Table(IQueryContext q, Entity self, string word, List<Item> items)
    {
        var parts = word.Split('.');
        var tables = TableScope.Tables(q.Model).Where(t => t.Id != self.Id && TableScope.IsName(t.Name)).OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (parts.Length <= 2)
        {
            foreach (var t in tables) items.Add(new Item($"table.{t.Name}.", t.Name, "a table", "table"));
            return;
        }
        if (tables.FirstOrDefault(t => string.Equals(t.Name, parts[1], StringComparison.OrdinalIgnoreCase)) is not { } other) return;
        var (g, v, _) = TableCommands.Evaluate(q, other, null);
        for (var i = 0; i < g.R; i++)
            for (var j = 0; j < g.C; j++)
                if (!g.Cells[i, j].Covered)
                    items.Add(new Item($"table.{parts[1]}.{DraftingTables.Address(i, j)}", DraftingTables.Address(i, j), v.Shown(i, j), "cell"));
    }

    public static void Register(IModuleRegistry r)
    {
        // {id (the table), cell (its address), text, caret} → {from, to, word, formula, note?, items: [{insert, label, detail, kind}]}:
        // what fits where the caret is in the cell's text, as the evaluator reads it; an item accepted replaces text[from..to].
        r.Query("drafting.tableSuggest", (q, a) =>
        {
            var e = TableCommands.Require(q.Model, (string?)a["id"]);
            var (row, col) = DraftingTables.Parse((string?)a["cell"]) ?? (0, 0);
            var text = (string?)a["text"] ?? "";
            var caret = Json.Number(a["caret"]) is double cv ? (int)cv : text.Length;
            return For(q, e, row, col, text, caret);
        });

        // {id, cell, text} → {shown, errors}: a cell's text tried without keeping it (the editor's check once typing pauses): what
        // the cell would read and why it would not, that cell's errors alone.
        r.Query("drafting.tableTry", (q, a) =>
        {
            var e = TableCommands.Require(q.Model, (string?)a["id"]);
            var (row, col) = DraftingTables.Parse((string?)a["cell"]) ?? throw new CommandException("Which cell?");
            var at = DraftingTables.Address(row, col);
            var table = TableEdits.Apply(e.Attrs["table"] as JsonObject, new JsonArray(new JsonObject { ["op"] = "set", ["cell"] = at, ["text"] = (string?)a["text"] ?? "" }), DraftingResources.Book(q.Model).TableStyles);
            var attrs = e.CloneAttrs();
            attrs["table"] = table;
            var (g, v, _) = TableCommands.Evaluate(q, e.With(attrs: attrs), null);
            var shown = row < g.R && col < g.C ? v.Shown(row, col) : "";
            return new JsonObject
            {
                ["shown"] = shown,
                ["errors"] = new JsonArray([.. v.Errors.Distinct().Where(x => x.StartsWith(at + ":", StringComparison.Ordinal) || x.StartsWith(at + " ", StringComparison.Ordinal)).Select(x => (JsonNode)x)]),
            };
        });
    }
}
