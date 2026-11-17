using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// What was left of the sheet annotations (audit §16): a note's leader points moved by their grips, a legend's own words in its
/// language, a sheet's symbols taken by a click on its paper and moved there in paper mm, and the issue's drawing hash counting
/// what is drawn on the paper and the sheet's symbols.
/// </summary>
public class SheetAnnotationRestTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => new(x, y);
    static bool Near(Vec2 a, Vec2 b) => (a - b).Length < 1e-6;

    static (Fixture F, string Sheet, string Paper) A3()
    {
        var f = F();
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        return (f, s, PaperViews.IdOf(s));
    }

    [Fact]
    public void A_leader_s_arrow_tip_and_bends_move_by_their_grips_one_undo_step_each()
    {
        var (f, _, paper) = A3();
        var note = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = paper, ["at"] = J(0.1, 0.2), ["text"] = "Check", ["leader"] = new JsonArray(J(0.06, 0.15), J(0.08, 0.18)) })!["id"]!;
        f.Run("drafting.moveLeaderPoint", new JsonObject { ["id"] = note, ["index"] = 0, ["at"] = J(0.05, 0.12) });
        var leader = Texts.Leader(f.Model.Get(note)!);
        Assert.True(Near(leader[0], new Vec2(0.05, 0.12)));
        Assert.True(Near(leader[1], new Vec2(0.08, 0.18)));
        // The note itself stays.
        Assert.True(Near(Args.PointOf(f.Model.Get(note)!.Attrs["at"]), new Vec2(0.1, 0.2)));
        f.Session.Undo();
        Assert.True(Near(Texts.Leader(f.Model.Get(note)!)[0], new Vec2(0.06, 0.15)));
        Assert.Throws<CommandException>(() => f.Run("drafting.moveLeaderPoint", new JsonObject { ["id"] = note, ["index"] = 2, ["at"] = J(0, 0) }));
        var plain = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = paper, ["at"] = J(0.2, 0.2), ["text"] = "No leader" })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("drafting.moveLeaderPoint", new JsonObject { ["id"] = plain, ["index"] = 0, ["at"] = J(0, 0) }));
    }

    [Fact]
    public void A_legend_writes_its_own_words_in_its_language_and_English_by_default()
    {
        var (f, s, paper) = A3();
        f.Wall(0, 0, 8, 0);
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = "plan:" + f.Storey, ["x"] = 150, ["y"] = 140 });
        var legend = (string)f.Run("sheet.addLegend", new JsonObject { ["viewId"] = paper, ["at"] = J(0.3, 0.25), ["kind"] = "lines", ["lang"] = "ro" })!["id"]!;
        List<string> Texts() => [.. Sheets.Compose(f.Session.Reader, f.Model.Get(s)!).Pens.SelectMany(p => p.Texts).Select(t => t.Text)];
        var texts = Texts();
        Assert.Contains("LINII", texts);
        Assert.Contains(texts, t => t.StartsWith("Secționat · continuă · ", StringComparison.Ordinal));
        f.Run("sheet.updateKey", new JsonObject { ["id"] = legend, ["kind"] = "materials", ["lang"] = "de" });
        Assert.Contains("LEGENDE", Texts());
        // Cleared: English, as a legend placed before.
        f.Run("sheet.updateKey", new JsonObject { ["id"] = legend, ["lang"] = null });
        Assert.Null(f.Model.Get(legend)!.Attrs["lang"]);
        Assert.Contains("LEGEND", Texts());
        Assert.Throws<CommandException>(() => f.Run("sheet.updateKey", new JsonObject { ["id"] = legend, ["lang"] = "xx" }));
        // A title typed is kept as typed.
        f.Run("sheet.updateKey", new JsonObject { ["id"] = legend, ["lang"] = "ro", ["title"] = "Materiale" });
        Assert.Contains("Materiale", Texts());
    }

    [Fact]
    public void A_sheet_s_symbol_is_picked_on_its_paper_and_moved_there_in_paper_mm()
    {
        var (f, s, paper) = A3();
        var sym = (string)f.Run("drafting.placeSymbol", new JsonObject { ["sheetId"] = s, ["symbolId"] = BuiltinSymbols.Prefix + "north-arrow", ["at"] = J(30, 30) })!["id"]!;
        var r = Views.Render(f.Session.Reader, Views.Find(f.Model, paper)!);
        // A pick mark of its own on the paper: never drawn, its insertion point (30 mm in, 30 mm down of 297) a snap.
        var mk = Assert.Single(r.Marks, x => x.Owner == sym);
        Assert.True(mk.Pick);
        Assert.Contains(mk.Snaps, p => Near(p, new Vec2(0.03, 0.267)));
        Assert.NotNull(mk.Hit);
        Assert.DoesNotContain(r.Pens, p => p.Layer == "PICK");
        // Moved 10 mm right and 10 mm up on the paper: 10 mm right and 10 mm down the sheet.
        f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(sym), ["op"] = "move", ["by"] = J(0.01, 0.01) });
        Assert.True(Near(Args.PointOf(f.Model.Get(sym)!.Attrs["at"]), new Vec2(40, 20)));
    }

    [Fact]
    public void The_issue_s_drawing_hash_counts_what_is_drawn_on_the_paper_and_the_sheet_s_symbols()
    {
        var (f, s, paper) = A3();
        f.Wall(0, 0, 5, 0);
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = "plan:" + f.Storey, ["x"] = 150, ["y"] = 140 });
        string Hash() => Sheets.DrawingHash(f.Session.Reader, f.Model.Get(s)!);
        var bare = Hash();
        // A note on the paper alone changes it; so does moving it.
        var note = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = paper, ["at"] = J(0.1, 0.2), ["text"] = "Rev. {sheet.date}" })!["id"]!;
        var noted = Hash();
        Assert.NotEqual(bare, noted);
        // A live text reads the date, the hash does not: a new date changes nothing.
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = s, ["fields"] = new JsonObject { ["date"] = "01.01.2030" } });
        Assert.Equal(noted, Hash());
        f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(note), ["op"] = "move", ["by"] = J(0.01, 0) });
        Assert.NotEqual(noted, Hash());
        var moved = Hash();
        // A symbol of the sheet's own too.
        f.Run("drafting.placeSymbol", new JsonObject { ["sheetId"] = s, ["symbolId"] = BuiltinSymbols.Prefix + "north-arrow", ["at"] = J(30, 30) });
        Assert.NotEqual(moved, Hash());
        // Issued, then changed only by an annotation: told changed since its issue.
        var hash = Hash();
        f.Run("cde.recordIssue", new JsonObject
        {
            ["name"] = "Stage 2", ["revision"] = "P01", ["status"] = "S2", ["container"] = "Shared",
            ["files"] = new JsonArray(new JsonObject { ["name"] = "x.pdf", ["kind"] = "pdf", ["sheetId"] = s, ["sha256"] = "00", ["size"] = 1, ["drawingHash"] = hash }),
        });
        Assert.DoesNotContain(Lapin.Platform.Cde.CdeChecks.Run(f.Session.Reader, null, "P01"), x => x.Rule == "revisionUnchanged");
        f.Run("drafting.addText", new JsonObject { ["viewId"] = paper, ["at"] = J(0.2, 0.1), ["text"] = "Added after" });
        Assert.Contains(Lapin.Platform.Cde.CdeChecks.Run(f.Session.Reader, null, "P01"), x => x.Rule == "revisionUnchanged" && x.SheetId == s);
    }
}
