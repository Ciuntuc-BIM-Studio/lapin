using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// Drafting tables on views and sheets (issue #71, docs/plan-drafting-tables.md, stage 1): placed by Annotate → Table, edited
/// cell by cell in the table editor (one undo step per edit), drawn here, so a view, its sheet, the PDF and the DXF print the
/// same table.
/// </summary>
public static class TableCommands
{
    static Vec2 P(JsonNode? n) => Args.PointOf(n);
    static JsonArray J(Vec2 p) => new(p.X, p.Y);

    public static void Register(IModuleRegistry r)
    {
        // {viewId, at (its top-left corner), rows, cols, preset?, colWidth? (mm), rowHeight? (mm), angle?, textStyle?, cells? (rows of texts)} → {id}.
        r.Command("addTable", "Table", (ctx, a) =>
        {
            var viewId = (string?)a["viewId"] ?? throw new CommandException("A table is placed on a view or a sheet.");
            if (Views.Find(ctx.Model, viewId) == null) throw new CommandException("There is no such view.");
            var rows = Json.Number(a["rows"]) is double rn ? (int)rn : 4;
            var cols = Json.Number(a["cols"]) is double cn ? (int)cn : 3;
            var preset = (string?)a["preset"] ?? "header";
            var styles = DraftingResources.Book(ctx.Model).TableStyles;
            var table = DraftingTables.New(rows, cols, Json.Number(a["colWidth"]) ?? 25, Json.Number(a["rowHeight"]) ?? 7, preset, styles);
            if (a["cells"] is JsonArray cells) table = TableEdits.Apply(table, new JsonArray(new JsonObject { ["op"] = "set", ["cell"] = "A1", ["values"] = cells.DeepClone() }), styles);
            var attrs = new JsonObject { ["viewId"] = viewId, ["at"] = J(P(a["at"])), ["angle"] = Json.Number(a["angle"]) ?? 0, ["table"] = table };
            if ((string?)a["textStyle"] is { } ts) attrs["textStyle"] = ts;
            var name = (string?)a["name"] is { Length: > 0 } given ? CheckName(ctx.Model, given, null) : "Table";
            var e = new Entity(ctx.NewId(), IfcClass.Annotation, name, attrs, DraftingTables.Predefined);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        // {id, ops: [{op, …}]} (TableEdits.Ops) → {rows, cols}: every op in order, one undo step; one that does not read refuses them all.
        r.Command("editTable", "Edit table", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var ops = a["ops"] as JsonArray ?? (a["op"] != null ? [a.DeepClone()] : throw new CommandException("Which edits? ops: [{op, …}]."));
            var (table, shifts) = TableEdits.ApplyTracked(e.Attrs["table"] as JsonObject, ops, DraftingResources.Book(ctx.Model).TableStyles);
            var attrs = e.CloneAttrs();
            attrs["table"] = table;
            ctx.Replace(e.With(attrs: attrs));
            // Other tables' references to its cells follow its rows and columns put in or taken out (its own followed already).
            foreach (var sh in shifts)
                Rewrite(ctx, e, (r, from) => from.Id != e.Id || r.Table != null ? TableEdits.Shift(r, sh.Rows, sh.At, sh.Count, sh.Insert) : null);
            var g = DraftingTables.Read(table);
            return new JsonObject { ["rows"] = g.R, ["cols"] = g.C };
        });

        // {id, name}: a table renamed (a word: letters, digits, _), unique among the project's tables; the references to it in every
        // table's cells follow (table.<old>.B4 → table.<new>.B4). One undo step.
        r.Command("renameTable", "Rename table", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            return new JsonObject { ["name"] = Rename(ctx, e, (string?)a["name"] ?? "") };
        });
        // Renamed in its properties (element.rename) the same way.
        r.Service<IDrivenElements>(new TableNames());

        // {id, name, cells?, styleId?} → {id}: the table saved as a table style of the drafting standards (stage 3): its look, its
        // columns' and rows' sizes and header rows, and with cells its texts too (a template a new table copies). A style of the
        // same name (or styleId) is replaced. One undo step.
        r.Command("saveTableStyle", "Save table style", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 and <= 80 } n ? n : throw new CommandException("A table style needs a name (80 characters at most).");
            var withCells = a["cells"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) && cb;
            var raw = Standards.ToJson(DraftingResources.Book(ctx.Model));
            var list = raw["tableStyles"] as JsonArray ?? [];
            var same = list.OfType<JsonObject>().FirstOrDefault(x => (string?)a["styleId"] is { } sid ? (string?)x["id"] == sid : string.Equals((string?)x["name"], name, StringComparison.OrdinalIgnoreCase));
            var id = (string?)same?["id"] ?? "table-" + ctx.NewId()[..8];
            var table = DraftingTables.StyleFrom(TableEdits.Normal(e.Attrs["table"] as JsonObject), withCells);
            table.Remove("preset");
            var entry = new JsonObject { ["id"] = id, ["name"] = name, ["table"] = table, ["cells"] = withCells };
            if (same != null) list[list.IndexOf(same)] = entry; else list.Add((JsonNode)entry);
            raw["tableStyles"] = list;
            DraftingResources.Write(ctx, raw);
            // The table is of that style now.
            var now = ctx.Model.Get(e.Id)!;
            var attrs = now.CloneAttrs();
            if (attrs["table"] is JsonObject tj) { tj["preset"] = id; ctx.Replace(now.With(attrs: attrs)); }
            return new JsonObject { ["id"] = id, ["name"] = name };
        });

        // A table deleted: what read its cells reads REF, so a table later given its name does not take its place.
        r.Service<IDeletionFollowers>(new TableDeletions());

        // {id} → the table as it reads now: its cells' texts as typed and as shown, spans, its columns' and rows' sizes as laid (mm), what does not read.
        r.Query("drafting.table", (q, a) =>
        {
            var e = Require(q.Model, (string?)a["id"]);
            var view = Views.Find(q.Model, e.Str("viewId") ?? "");
            var (g, v, l) = Evaluate(q, e, view);
            var cells = new JsonArray();
            for (var i = 0; i < g.R; i++)
            {
                var row = new JsonArray();
                for (var j = 0; j < g.C; j++)
                {
                    var c = g.Cells[i, j];
                    var o = new JsonObject { ["t"] = c.Text, ["shown"] = v.Shown(i, j) };
                    if (c.Covered) { var (mr, mc) = g.Master[i, j]; o["covered"] = DraftingTables.Address(mr, mc); }
                    if (c.RowSpan > 1) o["rs"] = c.RowSpan;
                    if (c.ColSpan > 1) o["cs"] = c.ColSpan;
                    row.Add((JsonNode)o);
                }
                cells.Add((JsonNode)row);
            }
            return new JsonObject
            {
                ["name"] = e.Name, ["referable"] = TableScope.IsName(e.Name),
                ["rows"] = g.R, ["cols"] = g.C, ["cells"] = cells,
                ["colW"] = new JsonArray([.. l.ColW.Select(x => (JsonNode)Math.Round(x, 3))]),
                ["rowH"] = new JsonArray([.. l.RowH.Select(x => (JsonNode)Math.Round(x, 3))]),
                ["errors"] = new JsonArray([.. v.Errors.Distinct().Select(x => (JsonNode)x)]),
            };
        });
    }

    public static Entity Require(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: IfcClass.Annotation, PredefinedType: DraftingTables.Predefined } e ? e : throw new CommandException("Not a table.");

    static (string Font, bool Bold, bool Italic, string Color) TextOf(StyleBook book, Entity e)
    {
        var s = Standards.ResolveText(book, Dimensions.ParamsOf(e));
        return (s.Font, s.Bold, s.Italic, s.Color ?? "#000000");
    }

    /// <summary>What a table's tokens and names read besides its cells: its sheet's on a sheet's paper.</summary>
    public static Func<string, (bool Known, string? Value)>? OuterOf(IQueryContext? q, Entity table)
    {
        if (q == null) return null;
        var view = Views.Find(q.Model, table.Str("viewId") ?? "");
        return view != null && PaperViews.Is(view) ? PaperViews.Tokens(q, view) : null;
    }

    /// <summary>The project's tables as formulas read them, each with its own sheet's tokens, worked out once each.</summary>
    public static TableScope Scope(IQueryContext? q, IModelView m) => new(m, t => OuterOf(q, t));

    /// <summary>A table read, its cells worked out (its sheet's tokens, other tables' cells) and laid out.</summary>
    public static (DraftingTables.Grid Grid, DraftingTables.Values Values, DraftingTables.Layout Layout) Evaluate(IQueryContext? q, Entity e, Entity? view, TableScope? scope = null)
    {
        scope ??= Scope(q, q?.Model ?? throw new CommandException("A table is worked out in a model."));
        var v = scope.Of(e);
        var book = q != null ? DraftingResources.Book(q.Model) : null;
        var text = book != null ? TextOf(book, e) : ("sans", false, false, "#000000");
        return (v.Grid, v, DraftingTables.Lay(v.Grid, v, text));
    }

    /// <summary>A table's name as given, checked: a word (letters, digits, _) no other table of the project has.</summary>
    static string CheckName(IModelView m, string name, string? self)
    {
        name = name.Trim();
        if (!TableScope.IsName(name)) throw new CommandException("A table's name is one word of letters, digits and _ (not starting with a digit), so a formula can name it: table.<name>.B4.");
        if (TableScope.Tables(m).Any(t => t.Id != self && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new CommandException($"There is a table „{name}” already.");
        return name;
    }

    /// <summary>
    /// Every table's references to <paramref name="target"/> (as each table finds that name, from where it stands) rewritten by
    /// <paramref name="map"/> (given the reference and the table it is in; null: kept). Each table changed once, in this step.
    /// </summary>
    internal static void Rewrite(ICommandContext ctx, Entity target, Func<TableEdits.Ref, Entity, string?> map)
    {
        var m = ctx.Model;
        foreach (var t in TableScope.Tables(m).ToList())
        {
            if (t.Attrs["table"] is not JsonObject json) continue;
            var copy = (JsonObject)json.DeepClone();
            var found = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            bool Reads(string name) => found.TryGetValue(name, out var b) ? b : found[name] = TableScope.Find(m, name, t, out _)?.Id == target.Id;
            if (!TableEdits.MapCells(copy, text => TableEdits.MapRefs(text, r => r.Table != null && Reads(r.Table) ? map(r, t) : null))) continue;
            var attrs = t.CloneAttrs();
            attrs["table"] = copy;
            ctx.Replace(t.With(attrs: attrs));
        }
    }

    /// <summary>A table renamed, the references to it following (one undo step with the command): its new name.</summary>
    static string Rename(ICommandContext ctx, Entity e, string given)
    {
        var name = CheckName(ctx.Model, given, e.Id);
        if (name == e.Name) return name;
        // Which references read this table is decided before it changes its name.
        Rewrite(ctx, e, (r, _) => TableEdits.Write(name, r.A, r.B));
        ctx.Replace(ctx.Model.Get(e.Id)!.With(name: name));
        return name;
    }

    /// <summary>element.rename on a table: its name checked and the references to it following, as drafting.renameTable does.</summary>
    sealed class TableNames : IDrivenElements
    {
        public bool Drives(IModelView m, Entity e) => e.Class == IfcClass.Annotation && e.PredefinedType == DraftingTables.Predefined;

        public void Before(ICommandContext ctx, DrivenEdit edit, DrivenOutcome outcome)
        {
            if (edit.Command != "element.rename") return;
            foreach (var id in edit.Ids)
                if (ctx.Model.Get(id) is { } e)
                {
                    Rename(ctx, e, (string?)edit.Args["name"] ?? "");
                    outcome.Handled.Add(id);
                }
        }
    }

    /// <summary>Tables deleted: the references other tables make to them read REF (one undo step with the deletion).</summary>
    sealed class TableDeletions : IDeletionFollowers
    {
        public void Deleting(ICommandContext ctx, IReadOnlySet<string> deleting)
        {
            foreach (var id in deleting)
                if (ctx.Model.Get(id) is { PredefinedType: DraftingTables.Predefined } t && t.Class == IfcClass.Annotation)
                    Rewrite(ctx, t, (_, from) => deleting.Contains(from.Id) ? null : TableEdits.Gone);
        }
    }

    /// <summary>The tables on a view, as drawn: their fills, then their lines, then their texts, in paper mm at the view's scale.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        var scope = Scope(c.Query, c.Model);
        foreach (var e in c.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == DraftingTables.Predefined && x.Str("viewId") == c.View.Id))
        {
            var v = scope.Of(e);
            var g = v.Grid;
            var text = TextOf(c.Book, e);
            var l = DraftingTables.Lay(g, v, text);
            var at = P(e.Attrs["at"]);
            var angle = e.Num("angle");
            var rad = angle * Math.PI / 180;
            var (u, n) = (new Vec2(Math.Cos(rad), Math.Sin(rad)), new Vec2(-Math.Sin(rad), Math.Cos(rad)));
            var mm = c.Mm;
            // Paper mm from the top-left corner, x across and y down the table, to the view.
            Vec2 To(double x, double y) => at + u * (x * mm) - n * (y * mm);
            var mk = new ViewMark(e.Id);
            Vec2[] Box(double x0, double y0, double x1, double y1) => [To(x0, y1), To(x1, y1), To(x1, y0), To(x0, y0)];
            foreach (var (x0, y0, x1, y1, fill) in DraftingTables.Fills(g, l)) mk.Fills.Add(([Box(x0, y0, x1, y1)], fill));
            foreach (var (x0, y0, x1, y1, pen) in DraftingTables.Lines(g, l)) mk.Line(To(x0, y0), To(x1, y1), "TABLE", pen.W, pen.C);
            foreach (var p in DraftingTables.Placements(g, l))
                mk.Text(new PenText(To(p.X, p.Y), p.Text, p.Style.Size, angle + p.Turn, p.Anchor, "bottom", p.Style.Bold, text.Font, p.Style.Italic), p.Style.Color);
            // What a click takes: the whole table; its corners snap.
            var outline = Box(0, 0, l.Width, l.Height);
            for (var k = 0; k < 4; k++) mk.Lines.Add((outline[k], outline[(k + 1) % 4], "PICK", 0, "none", null));
            mk.Hit = ([outline], true);
            mk.Snaps.AddRange(outline);
            mk.Data = new JsonObject
            {
                ["table"] = true,
                ["cols"] = new JsonArray([.. l.ColW.Select(x => (JsonNode)Math.Round(x, 3))]),
                ["rows"] = new JsonArray([.. l.RowH.Select(x => (JsonNode)Math.Round(x, 3))]),
            };
            yield return mk;
        }
    }
}
