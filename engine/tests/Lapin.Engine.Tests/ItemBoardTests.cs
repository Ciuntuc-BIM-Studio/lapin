using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The symbol and the title block on the shared sketch canvas (audit S11 stage 2, docs/plan-sketch-canvas.md §4): each opened on a
/// paper board of its own in its millimetres (1:1), its items laid as drafted shapes, hatches, texts and pictures; finished
/// unchanged it is what it was, byte for byte; edited with the Draft commands, what changed keeps its look, what was drawn new is
/// converted as Save as symbol converts drafting, in one undo step; cancelled, as it was.
/// </summary>
public sealed class ItemBoardTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(params JsonNode?[] v) => new(v);

    // A symbol of every kind of item: a line, a filled rectangle, an open polyline, a circle, an arc, a centred text with a field,
    // two layers of its own (forum #120) and an item of a kind the board does not know (kept as it is).
    static JsonObject Def() => new()
    {
        ["scale"] = "annotation", ["category"] = "callout",
        ["items"] = J(
            new JsonObject { ["type"] = "line", ["a"] = J(-5, 0), ["b"] = J(5, 0), ["w"] = 0.35, ["layer"] = "l1" },
            new JsonObject { ["type"] = "rect", ["x"] = -2, ["y"] = -1, ["w"] = 4, ["h"] = 2, ["stroke"] = 0.18, ["fill"] = "#ff0000" },
            new JsonObject { ["type"] = "polyline", ["pts"] = J(J(0, 3), J(2, 4), J(4, 3)), ["closed"] = false, ["stroke"] = 0.25 },
            new JsonObject { ["type"] = "circle", ["c"] = J(0, 0), ["r"] = 6, ["stroke"] = 0.25 },
            new JsonObject { ["type"] = "arc", ["c"] = J(0, 0), ["r"] = 8, ["start"] = 0, ["end"] = 90, ["w"] = 0.25 },
            new JsonObject { ["type"] = "text", ["at"] = J(0, -4), ["text"] = "{attr.no}", ["h"] = 2.5, ["align"] = "middle", ["bold"] = true }),
        ["attributes"] = J(new JsonObject { ["key"] = "no", ["label"] = "Number", ["default"] = "1" }),
        ["layers"] = J(new JsonObject { ["id"] = "l1", ["name"] = "Outline" }, new JsonObject { ["id"] = "l2", ["name"] = "Text", ["visible"] = "{attr.no}" }),
        ["description"] = "A test symbol",
    };

    static (Fixture F, string Symbol) Symbol()
    {
        var f = F();
        var id = (string)f.Run("symbols.create", new JsonObject { ["name"] = "Callout", ["def"] = Def() })!["id"]!;
        return (f, id);
    }

    static string OpenSymbol(Fixture f, string id) => (string)f.Run("drafting.editSymbol", new JsonObject { ["symbolId"] = id })!["viewId"]!;
    static JsonObject DefOf(Fixture f, string id) => f.Model.Get(id)!.Attrs["def"]!.AsObject();
    static IEnumerable<Entity> On(Fixture f, string view) => f.Model.OfClass(IfcClass.Annotation).Where(e => e.Str("viewId") == view);

    [Fact]
    public void A_symbol_opens_on_a_paper_board_in_its_mm_and_finished_unchanged_is_byte_for_byte_what_it_was()
    {
        var (f, id) = Symbol();
        var before = DefOf(f, id).ToJsonString();
        var view = OpenSymbol(f, id);
        Assert.Equal("Edit symbol", f.Session.History.UndoLabel);
        var v = f.Model.Get(view)!;
        Assert.Equal(PaperViews.Kind, v.Str("kind"));
        Assert.True(PaperViews.IsBoard(f.Model, v));
        Assert.Equal(id, (string?)v.Attrs[SymbolBoards.Key]!["symbolId"]);
        // The layers are kept with the definition now (they were dropped before).
        Assert.Equal(2, DefOf(f, id)["layers"]!.AsArray().Count);
        // One drafted thing an item: the line in metres (mm / 1000), the filled rectangle a hatch, the text as typed.
        var on = On(f, view).ToList();
        Assert.Equal(6, on.Count);
        var line = Drafts.On(f.Model, view).Single(e => (int?)e.Attrs[ItemBoards.ItemKey] == 0);
        Assert.Equal(-0.005, (double)Drafts.ShapeOf(line)!.ToJson()["a"]![0]!, 9);
        Assert.NotNull(Drafts.On(f.Model, view).Single(e => (int?)e.Attrs[ItemBoards.ItemKey] == 1).Attrs["hatch"]);
        Assert.Equal("{attr.no}", on.Single(e => e.PredefinedType == "TEXT").Str("text"));
        // Its guides (the insertion point) are drawn and fitted.
        var r = Views.Render(f.Session.Reader, v);
        Assert.Contains(r.Pens, p => p.Layer == PaperViews.PaperLayer && p.Segments.Count > 0);
        Assert.NotNull(r.Bounds);
        // Opened again: the same board.
        Assert.Equal(view, OpenSymbol(f, id));
        // What Finish would write, asked first: the same definition.
        var read = f.Session.Query("drafting.symbolBoard", new JsonObject { ["viewId"] = view })!;
        Assert.Equal(before, read["def"]!.ToJsonString());

        f.Run("drafting.finishSymbol", new JsonObject { ["viewId"] = view });
        Assert.Equal("Finish symbol", f.Session.History.UndoLabel);
        Assert.Null(f.Model.Get(view));
        Assert.Empty(On(f, view));
        Assert.Equal(before, DefOf(f, id).ToJsonString());
    }

    [Fact]
    public void Edits_keep_each_item_s_look_new_drawing_is_converted_and_one_undo_takes_the_finish_back()
    {
        var (f, id) = Symbol();
        var view = OpenSymbol(f, id);
        var drafts = Drafts.On(f.Model, view).ToList();
        string Of(int i) => drafts.Single(e => (int?)e.Attrs[ItemBoards.ItemKey] == i).Id;
        // The line moved 1 mm up; the circle deleted; a new line drawn; a text with a new field written.
        f.Run("drafting.transform", new JsonObject { ["ids"] = J(Of(0)), ["op"] = "move", ["by"] = J(0, 0.001) });
        f.Run("element.delete", new JsonObject { ["ids"] = J(Of(3)) });
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(0, 0), ["b"] = J(0.01, 0.01) } });
        f.Run("drafting.addText", new JsonObject { ["viewId"] = view, ["at"] = J(0.002, 0.006), ["text"] = "{attr.room}" });
        // A construction line is a helper on the board only.
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["role"] = "draft", ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(-1, 0), ["b"] = J(1, 0) } });
        f.Run("drafting.setSymbolBoard", new JsonObject { ["viewId"] = view, ["layer"] = "l2", ["name"] = "Callout B" });
        // Nothing reaches the symbol before Finish.
        Assert.Equal(6, DefOf(f, id)["items"]!.AsArray().Count);

        f.Run("drafting.finishSymbol", new JsonObject { ["viewId"] = view });
        var d = DefOf(f, id);
        var items = d["items"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.Equal(["line", "rect", "polyline", "arc", "text", "line", "text"], items.Select(i => (string)i["type"]!));
        // The moved line keeps its width and its layer, one mm higher.
        Assert.Equal(0.35, (double)items[0]["w"]!);
        Assert.Equal("l1", (string?)items[0]["layer"]);
        Assert.Equal(1, (double)items[0]["a"]![1]!, 6);
        // The others as they were, byte for byte.
        Assert.Equal(Def()["items"]![1]!.ToJsonString(), items[1].ToJsonString());
        Assert.Equal(Def()["items"]![5]!.ToJsonString(), items[4].ToJsonString());
        // The new line in mm, on the layer new items take; the new text's field made an attribute.
        Assert.Equal(10, (double)items[5]["b"]![0]!, 6);
        Assert.Equal("l2", (string?)items[5]["layer"]);
        Assert.Contains(d["attributes"]!.AsArray(), a => (string?)a!["key"] == "room");
        Assert.Equal("Callout B", f.Model.Get(id)!.Name);
        Assert.Null(f.Model.Get(view));

        // One undo: the symbol as it was before Finish, the board back with its edits.
        f.Session.Undo();
        Assert.Equal(6, DefOf(f, id)["items"]!.AsArray().Count);
        Assert.NotNull(f.Model.Get(view));
    }

    [Fact]
    public void Cancel_drops_the_board_and_a_new_or_a_library_symbol_and_the_refusals()
    {
        var (f, id) = Symbol();
        var before = DefOf(f, id).ToJsonString();
        var view = OpenSymbol(f, id);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray([.. Drafts.On(f.Model, view).Select(e => (JsonNode)e.Id)]) });
        f.Run("drafting.cancelSymbol", new JsonObject { ["viewId"] = view });
        Assert.Null(f.Model.Get(view));
        Assert.Equal(before, DefOf(f, id).ToJsonString());

        // Lapin's own stay as they are.
        Assert.Throws<CommandException>(() => f.Run("drafting.editSymbol", new JsonObject { ["symbolId"] = BuiltinSymbols.All[0].Id }));

        // A new symbol is made at Finish; nothing drawn is refused, the board left open.
        var fresh = (string)f.Run("drafting.editSymbol", new JsonObject { ["name"] = "Mine", ["target"] = "new" })!["viewId"]!;
        Assert.Throws<CommandException>(() => f.Run("drafting.finishSymbol", new JsonObject { ["viewId"] = fresh }));
        Assert.NotNull(f.Model.Get(fresh));
        f.Run("drafting.draw", new JsonObject { ["viewId"] = fresh, ["shape"] = new JsonObject { ["type"] = "ellipse", ["c"] = J(0, 0), ["m"] = J(0.004, 0), ["ratio"] = 0.5 } });
        var made = f.Run("drafting.finishSymbol", new JsonObject { ["viewId"] = fresh })!;
        var mine = DefOf(f, (string)made["symbolId"]!)["items"]!.AsArray().Single()!;
        // An ellipse is its points by the one rule (a symbol has no ellipses), closed.
        Assert.Equal("polyline", (string?)mine["type"]);
        Assert.True((bool)mine["closed"]!);
        Assert.Equal(4, mine["pts"]!.AsArray().Max(p => (double)p![0]!), 6);

        // A library symbol: Finish gives the definition back and writes nothing in the project.
        var count = f.Model.OfClass(Symbols.Class).Count();
        var lib = (string)f.Run("drafting.editSymbol", new JsonObject { ["name"] = "Library", ["target"] = "library", ["def"] = Def() })!["viewId"]!;
        var back = f.Run("drafting.finishSymbol", new JsonObject { ["viewId"] = lib })!;
        Assert.Null(back["symbolId"]);
        Assert.Equal(6, back["def"]!["items"]!.AsArray().Count);
        Assert.Equal(count, f.Model.OfClass(Symbols.Class).Count());

        // A dimension on the board is refused at Finish, naming it.
        var v2 = OpenSymbol(f, id);
        f.Run("drafting.addDimension", new JsonObject { ["viewId"] = v2, ["a"] = J(0, 0), ["b"] = J(0.01, 0), ["at"] = J(0.005, 0.002) });
        var ex = Assert.Throws<CommandException>(() => f.Run("drafting.finishSymbol", new JsonObject { ["viewId"] = v2 }));
        Assert.Contains("none of them", ex.Message);
    }

    // ------------------------------------------------------------ title blocks

    static JsonObject Template() => new()
    {
        ["w"] = 180, ["h"] = 50, ["anchor"] = "bottomRight",
        ["items"] = J(
            new JsonObject { ["type"] = "rect", ["x"] = 0, ["y"] = 0, ["w"] = 180, ["h"] = 50, ["stroke"] = 0.5 },
            new JsonObject { ["type"] = "line", ["a"] = J(0, 25), ["b"] = J(180, 25), ["w"] = 0.25 },
            new JsonObject { ["type"] = "text", ["at"] = J(4, 30), ["text"] = "{project.name}", ["h"] = 5, ["bold"] = true },
            new JsonObject { ["type"] = "polyline", ["pts"] = J(J(150, 30), J(170, 30), J(160, 45)), ["closed"] = true, ["fill"] = "#202020", ["stroke"] = 0 },
            new JsonObject { ["type"] = "revisions", ["x"] = 100, ["y"] = 0, ["w"] = 80, ["h"] = 20 }),
    };

    [Fact]
    public void A_title_block_opens_on_a_paper_board_finished_unchanged_is_the_same_and_edits_reach_it_in_one_step()
    {
        var f = F();
        var id = (string)f.Run("titleBlock.create", new JsonObject { ["name"] = "Office", ["template"] = Template() })!["id"]!;
        var before = f.Model.Get(id)!.Attrs["template"]!.ToJsonString();
        var view = (string)f.Run("drafting.editTitleBlock", new JsonObject { ["id"] = id })!["viewId"]!;
        Assert.Equal(5, On(f, view).Count());
        // Its box is the board's guide; the revision table its box.
        var v = f.Model.Get(view)!;
        Assert.Single(v.Attrs[PaperViews.UnderlayKey]!.AsArray());
        Assert.Contains(Drafts.On(f.Model, view), e => e.Name.StartsWith("Revision table", StringComparison.Ordinal));
        f.Run("drafting.finishTitleBlock", new JsonObject { ["viewId"] = view });
        Assert.Equal(before, f.Model.Get(id)!.Attrs["template"]!.ToJsonString());

        // Edited: the revision table moved 10 mm left, a circle drawn (a title block's are polylines), the box made taller.
        view = (string)f.Run("drafting.editTitleBlock", new JsonObject { ["id"] = id })!["viewId"]!;
        var table = Drafts.On(f.Model, view).Single(e => (int?)e.Attrs[ItemBoards.ItemKey] == 4).Id;
        f.Run("drafting.transform", new JsonObject { ["ids"] = J(table), ["op"] = "move", ["by"] = J(-0.01, 0) });
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = new JsonObject { ["type"] = "circle", ["c"] = J(0.01, 0.01), ["r"] = 0.005 } });
        f.Run("drafting.setTitleBlockBoard", new JsonObject { ["viewId"] = view, ["h"] = 60 });
        Assert.Throws<CommandException>(() => f.Run("drafting.setTitleBlockBoard", new JsonObject { ["viewId"] = view, ["w"] = 5 }));
        f.Run("drafting.finishTitleBlock", new JsonObject { ["viewId"] = view });
        var t = f.Model.Get(id)!.Attrs["template"]!.AsObject();
        Assert.Equal(60, (double)t["h"]!);
        var items = t["items"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.Equal(["rect", "line", "text", "polyline", "revisions", "polyline"], items.Select(i => (string)i["type"]!));
        Assert.Equal(90, (double)items[4]["x"]!, 6);
        Assert.Equal(80, (double)items[4]["w"]!, 6);
        Assert.True((bool)items[5]["closed"]!);
        Assert.Equal(Template()["items"]![3]!.ToJsonString(), items[3].ToJsonString());
        f.Session.Undo();
        Assert.Equal(before, f.Model.Get(id)!.Attrs["template"]!.ToJsonString());
        f.Run("drafting.cancelTitleBlock", new JsonObject { ["viewId"] = view });
        Assert.Null(f.Model.Get(view));
        Assert.Throws<CommandException>(() => f.Run("drafting.editTitleBlock", new JsonObject { ["id"] = BuiltinTitleBlocks.All[0].Id }));
    }
}
