using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting.Cad;

/// <summary>
/// A title block edited on the shared sketch canvas (audit S11 stage 2, docs/plan-sketch-canvas.md §4): a board in paper mm from
/// the block's bottom-left corner, y up (the paper view of decision 620 without a sheet: 1:1, a length typed in mm is paper mm),
/// its box drawn dashed under what is drawn; lines, rectangles, polylines, hatches and fills, texts with their live tokens (shown
/// as typed), the logo as a picture (Import image puts another) and the revision table as its box (<see cref="ItemBoards"/>).
///
/// <para><c>drafting.editTitleBlock {id}</c> opens a project title block's board (Lapin's own stay as they are; opened again, the
/// same board); <c>drafting.setTitleBlockBoard</c> changes the fields beside it (name, the box's width and height, its corner, the
/// frame, where the revision table goes); <c>drafting.titleBlockBoard</c> answers the template as Finish would write it;
/// <c>drafting.finishTitleBlock</c> writes it through <c>titleBlock.update</c> (one undo step; unchanged, the title block stays as
/// it was, byte for byte); <c>drafting.cancelTitleBlock</c> drops the board.</para>
/// </summary>
public static class TitleBlockBoards
{
    /// <summary>The attribute marking a title block's board: {id, name, template (as it was opened, its fields as set beside)}.</summary>
    public const string Key = "titleBlockOf";

    const string What = "a title block";

    static JsonArray Guides(TitleBlockTemplate t) => new(ItemBoards.Box(t.W, t.H, "#6b9bd1"));

    static CommandEntry Catalogued(ICommandContext ctx, string name) =>
        (ctx.Services as ModuleCatalog)?.Command(name) ?? throw new CommandException($"Editing a title block needs {name}, not in this product.");

    /// <summary>{id} → {viewId}: the board, one undo step.</summary>
    public static JsonNode? Edit(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        var id = (string?)a["id"] ?? "";
        if (BuiltinTitleBlocks.Is(id)) throw new CommandException("Lapin's own title blocks stay as they are: duplicate one to change it.");
        if (m.Get(id) is not { Class: TitleBlocks.Class } e || TitleBlockTemplate.From(e.Attrs["template"]) is not { } t) throw new CommandException("There is no such title block.");
        if (ItemBoards.Find(m, Key, o => (string?)o["id"] == id) is { } open) return new JsonObject { ["viewId"] = open.Id };
        var of = new JsonObject { ["id"] = id, ["name"] = e.Name, ["template"] = t.ToJson() };
        var view = ItemBoards.NewView(ctx, $"Title block: {e.Name}", Key, of, Guides(t));
        ItemBoards.Lay(ctx, view, t.Items);
        return new JsonObject { ["viewId"] = view.Id };
    }

    /// <summary>The template the board holds now: its fields, then its items.</summary>
    public static (TitleBlockTemplate Template, string Name) Read(IModelView m, Entity view, JsonObject of)
    {
        var was = TitleBlockTemplate.From(of["template"]) ?? new TitleBlockTemplate(180, 50, "bottomRight", new TitleBlockFrame(), "above", []);
        var c = ItemBoards.Collect(m, view, was.Items, curves: false, "A title block");
        var items = c.Items;
        if (c.Fresh.Count > 0) foreach (var it in ItemBoards.Fresh(m, view, c.Fresh, curves: false).Items) items.Add(it?.DeepClone());
        foreach (var it in ItemBoards.FreshImages(c.Images)) items.Add((JsonNode)it);
        return (was with { Items = items }, (string?)of["name"] ?? "Title block");
    }

    /// <summary>{viewId} → {id, name}: the title block written, the board gone.</summary>
    public static JsonNode? Finish(ICommandContext ctx, JsonObject a)
    {
        var (view, of) = ItemBoards.Board(ctx.Model, a, Key, What);
        var (t, name) = Read(ctx.Model, view, of);
        var json = t.ToJson();
        // Checked as every template is: refused, the board stays open.
        TitleBlockTemplate.Parse(json);
        var e = ctx.Model.Get((string?)of["id"] ?? "") is { Class: TitleBlocks.Class } x ? x : throw new CommandException("The title block this board is of is gone.");
        var args = new JsonObject { ["id"] = e.Id };
        if (!JsonNode.DeepEquals(e.Attrs["template"], json)) args["template"] = json;
        if (name != e.Name) args["name"] = name;
        if (args.Count > 1) Catalogued(ctx, "titleBlock.update").Handler(ctx, args);
        ItemBoards.Drop(ctx, view);
        return new JsonObject { ["id"] = e.Id, ["name"] = ctx.Model.Get(e.Id)?.Name };
    }

    /// <summary>{viewId, name?, w?, h?, anchor?, frame?, revisions?}: the fields beside the board, one undo step; its box follows.</summary>
    public static JsonNode? Set(ICommandContext ctx, JsonObject a)
    {
        var (view, of) = ItemBoards.Board(ctx.Model, a, Key, What);
        var next = (JsonObject)of.DeepClone();
        var tj = (JsonObject)(of["template"] as JsonObject ?? new JsonObject()).DeepClone();
        if (a.ContainsKey("name")) next["name"] = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A title block needs a name.");
        foreach (var k in new[] { "w", "h", "anchor", "revisions" }) if (a.ContainsKey(k)) tj[k] = a[k]?.DeepClone();
        if (a["frame"] is JsonObject fr)
        {
            var frame = tj["frame"] as JsonObject ?? new JsonObject();
            foreach (var (k, v) in fr) frame[k] = v?.DeepClone();
            tj["frame"] = frame;
        }
        // The fields checked as a template's are (its items aside).
        var probe = (JsonObject)tj.DeepClone();
        probe["items"] = new JsonArray();
        var t = TitleBlockTemplate.Parse(probe);
        tj["w"] = t.W; tj["h"] = t.H; tj["anchor"] = t.Anchor; tj["revisions"] = t.Revisions; tj["frame"] = t.Frame.ToJson();
        next["template"] = tj;
        ItemBoards.SetMark(ctx, view, Key, next, Guides(t));
        if (a.ContainsKey("name")) ctx.Replace(ctx.Model.Get(view.Id)!.With(name: $"Title block: {(string?)next["name"]}"));
        return null;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Command("drafting.editTitleBlock", "Edit title block", Edit);
        r.Command("drafting.finishTitleBlock", "Finish title block", Finish);
        r.Command("drafting.cancelTitleBlock", "Cancel title block", (ctx, a) => { ItemBoards.Drop(ctx, ItemBoards.Board(ctx.Model, a, Key, What).View); return null; });
        r.Command("drafting.setTitleBlockBoard", "Title block fields", Set);
        // {viewId} → {id, name, template}: the board's title block as Finish would write it (an error when it would refuse).
        r.Query("drafting.titleBlockBoard", (q, a) =>
        {
            var (view, of) = ItemBoards.Board(q.Model, a, Key, What);
            var (t, name) = Read(q.Model, view, of);
            return new JsonObject { ["id"] = (string?)of["id"], ["name"] = name, ["template"] = t.ToJson() };
        });
    }
}
