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
            var table = DraftingTables.New(rows, cols, Json.Number(a["colWidth"]) ?? 25, Json.Number(a["rowHeight"]) ?? 7, preset);
            if (a["cells"] is JsonArray cells) table = TableEdits.Apply(table, new JsonArray(new JsonObject { ["op"] = "set", ["cell"] = "A1", ["values"] = cells.DeepClone() }));
            var attrs = new JsonObject { ["viewId"] = viewId, ["at"] = J(P(a["at"])), ["angle"] = Json.Number(a["angle"]) ?? 0, ["table"] = table };
            if ((string?)a["textStyle"] is { } ts) attrs["textStyle"] = ts;
            var e = new Entity(ctx.NewId(), IfcClass.Annotation, (string?)a["name"] is { Length: > 0 } name ? name : "Table", attrs, DraftingTables.Predefined);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        // {id, ops: [{op, …}]} (TableEdits.Ops) → {rows, cols}: every op in order, one undo step; one that does not read refuses them all.
        r.Command("editTable", "Edit table", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var ops = a["ops"] as JsonArray ?? (a["op"] != null ? [a.DeepClone()] : throw new CommandException("Which edits? ops: [{op, …}]."));
            var table = TableEdits.Apply(e.Attrs["table"] as JsonObject, ops);
            var attrs = e.CloneAttrs();
            attrs["table"] = table;
            ctx.Replace(e.With(attrs: attrs));
            var g = DraftingTables.Read(table);
            return new JsonObject { ["rows"] = g.R, ["cols"] = g.C };
        });

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

    /// <summary>A table read, its cells worked out (with its sheet's tokens when it is on a sheet's paper) and laid out.</summary>
    public static (DraftingTables.Grid Grid, DraftingTables.Values Values, DraftingTables.Layout Layout) Evaluate(IQueryContext? q, Entity e, Entity? view)
    {
        var g = DraftingTables.Read(e);
        var outer = q != null && view != null && PaperViews.Is(view) ? PaperViews.Tokens(q, view) : null;
        var v = new DraftingTables.Values(g, outer);
        var book = q != null ? DraftingResources.Book(q.Model) : null;
        var text = book != null ? TextOf(book, e) : ("sans", false, false, "#000000");
        return (g, v, DraftingTables.Lay(g, v, text));
    }

    /// <summary>The tables on a view, as drawn: their fills, then their lines, then their texts, in paper mm at the view's scale.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var e in c.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == DraftingTables.Predefined && x.Str("viewId") == c.View.Id))
        {
            var g = DraftingTables.Read(e);
            var outer = c.Query != null && PaperViews.Is(c.View) ? PaperViews.Tokens(c.Query, c.View) : null;
            var v = new DraftingTables.Values(g, outer);
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
            for (var i = 0; i < g.R; i++)
                for (var j = 0; j < g.C; j++)
                {
                    var cell = g.Cells[i, j];
                    if (cell.Covered || (cell.Str("fill") ?? (string?)g.Style["fill"]) is not { } fill || !HexColor.TryParse(fill, out _, out _, out _)) continue;
                    mk.Fills.Add(([Box(l.X(j), l.Y(i), l.X(j + cell.ColSpan), l.Y(i + cell.RowSpan))], fill));
                }
            foreach (var (x0, y0, x1, y1, pen) in DraftingTables.Lines(g, l)) mk.Line(To(x0, y0), To(x1, y1), "TABLE", pen.W, pen.C);
            for (var i = 0; i < g.R; i++)
                for (var j = 0; j < g.C; j++)
                {
                    if (l.Texts[i, j] is not { Lines.Count: > 0 } t) continue;
                    var cell = g.Cells[i, j];
                    var (x0, x1, y0, y1) = (l.X(j), l.X(j + cell.ColSpan), l.Y(i), l.Y(i + cell.RowSpan));
                    var block = DraftingTables.TextHeight(t);
                    var top = t.VAlign switch { "top" => y0 + g.Pad, "bottom" => y1 - g.Pad - block, _ => (y0 + y1) / 2 - block / 2 };
                    var (x, anchor) = t.Align switch { "center" => ((x0 + x1) / 2, "middle"), "right" => (x1 - g.Pad, "end"), _ => (x0 + g.Pad, "start") };
                    for (var k = 0; k < t.Lines.Count; k++)
                        mk.Text(new PenText(To(x, top + t.Size + k * t.Size * DraftingTables.LineStep), t.Lines[k], t.Size, angle, anchor, "bottom", t.Bold, text.Font, t.Italic), t.Color);
                }
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
