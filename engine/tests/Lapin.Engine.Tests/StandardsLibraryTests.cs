using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Drawing;
using Lapin.Drawing.Import;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Line types of the project's own and the standards as a library (docs/plan-user-extensibility.md §3.6–3.7): AutoCAD's .lin read
/// (dashes, dots, gaps, complex types' dashes, inches), a line type named by a drafting layer and drawn with its dashes, renamed
/// and removed everywhere it is named; the .lapinstandards file carried from one project to another with its choices.
/// </summary>
public sealed class StandardsLibraryTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    const string Lin = """
        ;; a few of acadiso.lin's, and one of the user's
        *BORDER,Border __ __ . __ __ . __ __ . __ __ . __ __ .
        A,12.7,-6.35,12.7,-6.35,0,-6.35
        *DOTS,Dots . . . . .
        A,0,-6.35
        *GAS_LINE,Gas line ----GAS----GAS----
        A,12.7,-5.08,["GAS",STANDARD,S=2.54,R=0.0,X=-2.54,Y=-1.27],-6.35
        *LEADGAP,Starts with a gap
        A,-2,5
        *ONLYDASH,Not a line type
        A,5
        """;

    [Fact]
    public void A_lin_file_reads_dashes_dots_gaps_and_the_dashes_of_complex_types()
    {
        var skipped = new List<string>();
        var read = LinFile.Parse(Lin, 1, skipped);
        Assert.Equal(["BORDER", "DOTS", "GAS_LINE", "LEADGAP"], read.Select(r => r.Name).ToArray());
        Assert.Equal([12.7, 6.35, 12.7, 6.35, 0.1, 6.35], read[0].Dashes);
        Assert.Equal("Border __ __ . __ __ . __ __ . __ __ . __ __ .", read[0].Description);
        Assert.Equal([0.1, 6.35], read[1].Dashes);
        Assert.True(read[2].Complex);
        Assert.Equal([12.7, 11.43], read[2].Dashes);
        Assert.Equal([5, 2], read[3].Dashes);
        Assert.Equal(["ONLYDASH"], skipped);
        // acad.lin is in inches.
        Assert.Equal([25.4, 12.7], LinFile.Parse("*D,d\nA,1,-0.5", 25.4)[0].Dashes);
        // Written and read again: the same.
        var again = LinFile.Parse(LinFile.Write(read.Select(r => (r.Name, r.Description, r.Dashes))), 1);
        Assert.Equal(read.Select(r => r.Dashes), again.Select(r => r.Dashes));
    }

    static double[]? LayerDash(Fixture f, string view, string layer) =>
        f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!["pens"]!.AsArray().OfType<JsonObject>().FirstOrDefault(p => (string?)p["layer"] == layer)?["dash"]?.AsArray().Select(x => (double)x!).ToArray();

    [Fact]
    public void A_line_type_of_the_project_s_draws_a_layer_and_follows_a_rename_and_a_removal()
    {
        var f = F();
        var r = f.Run("standards.importLin", new JsonObject { ["text"] = Lin })!;
        Assert.Equal(4, (int)r["added"]!);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "A-BORDER", ["lineType"] = "BORDER" });
        f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["layer"] = "A-BORDER", ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(5, 0) }) });
        Assert.Equal([12.7, 6.35, 12.7, 6.35, 0.1, 6.35], LayerDash(f, plan, "A-BORDER")!);

        // Its dashes changed: the layer draws them.
        f.Run("standards.setLineType", new JsonObject { ["name"] = "BORDER", ["dashes"] = new JsonArray(4, 2) });
        Assert.Equal([4, 2], LayerDash(f, plan, "A-BORDER")!);
        // Renamed: the layer follows. Removed: continuous.
        f.Run("standards.setLineType", new JsonObject { ["name"] = "BORDER", ["newName"] = "Office border" });
        Assert.Equal("Office border", Drafts.LayerOf(f.Model, "A-BORDER").LineType);
        Assert.Equal([4, 2], LayerDash(f, plan, "A-BORDER")!);
        f.Run("standards.removeLineType", new JsonObject { ["name"] = "Office border" });
        Assert.Equal("continuous", Drafts.LayerOf(f.Model, "A-BORDER").LineType);
        Assert.Null(LayerDash(f, plan, "A-BORDER"));
        // Lapin's own names stay Lapin's.
        Assert.Throws<CommandException>(() => f.Run("standards.setLineType", new JsonObject { ["name"] = "dashed", ["dashes"] = new JsonArray(1, 1) }));
        // An object style may name one.
        f.Run("standards.setLineType", new JsonObject { ["name"] = "Fine", ["dashes"] = new JsonArray(1, 0.5) });
        f.Run("view.objectStyle", new JsonObject { ["category"] = LineStyles.Categories[0], ["role"] = "projection", ["spec"] = new JsonObject { ["pattern"] = "Fine" } });
        Assert.Throws<CommandException>(() => f.Run("view.objectStyle", new JsonObject { ["category"] = LineStyles.Categories[0], ["role"] = "projection", ["spec"] = new JsonObject { ["pattern"] = "Nope" } }));
    }

    [Fact]
    public void Standards_go_from_one_project_to_another_with_a_choice_for_what_differs()
    {
        var a = F();
        a.Run("standards.set", new JsonObject { ["kind"] = "text", ["entry"] = new JsonObject { ["id"] = "title", ["name"] = "Title", ["height"] = 5, ["bold"] = true } });
        a.Run("standards.setLineType", new JsonObject { ["name"] = "Office border", ["dashes"] = new JsonArray(6, 2) });
        a.Run("standards.set", new JsonObject { ["kind"] = "layer", ["entry"] = new JsonObject { ["id"] = "A-ANNO", ["name"] = "A-ANNO", ["lineType"] = "Office border" } });
        var symbol = (string)a.Run("symbols.create", new JsonObject { ["name"] = "Office head", ["def"] = new JsonObject { ["scale"] = "annotation", ["items"] = new JsonArray(new JsonObject { ["type"] = "circle", ["c"] = new JsonArray(0, 0), ["r"] = 5 }) } })!["id"]!;
        a.Run("symbols.setSlot", new JsonObject { ["slot"] = "sectionHead", ["symbolId"] = symbol });
        a.Run("standards.options", new JsonObject { ["units"] = "cm" });
        var text = (string)a.Session.Query("standards.export", new JsonObject { ["name"] = "Office" })!["text"]!;

        var b = F();
        b.Run("standards.set", new JsonObject { ["kind"] = "text", ["entry"] = new JsonObject { ["id"] = "title", ["name"] = "Title", ["height"] = 3.5 } });
        var read = b.Session.Query("standards.readPack", new JsonObject { ["text"] = text })!;
        var entries = read["entries"]!.AsArray().OfType<JsonObject>().ToList();
        var title = entries.Single(e => (string)e["kind"]! == "text" && (string)e["id"]! == "title");
        Assert.True((bool)title["exists"]!); Assert.False((bool)title["same"]!);
        Assert.Contains(entries, e => (string)e["kind"]! == "symbol" && (string)e["name"]! == "Office head" && !(bool)e["exists"]!);
        Assert.True((bool)read["options"]!);

        // Keep both for the title; the rest comes in; with the options, the units and the section heads' symbol too.
        b.Run("standards.importFrom", new JsonObject { ["text"] = text, ["choices"] = new JsonObject { ["text:title"] = "both" }, ["options"] = true });
        var book = DraftingResources.Book(b.Model);
        Assert.Equal(3.5, book.TextStyles.Single(t => t.Id == "title").Height);
        Assert.Equal(5, book.TextStyles.Single(t => t.Name == "Title 2").Height);
        Assert.Contains(book.LineTypes!, l => l.Name == "Office border");
        Assert.Equal("Office border", book.Layers.Single(l => l.Id == "A-ANNO").LineType);
        Assert.Equal("cm", book.Units);
        var mine = b.Model.OfClass(Symbols.Class).Single(e => e.Name == "Office head");
        Assert.Equal(mine.Id, SymbolSlots.SymbolFor(b.Model, "sectionHead"));

        // Brought in again with update: the same entries kept, the title the file's.
        var again = b.Run("standards.importFrom", new JsonObject { ["text"] = text, ["all"] = "update" })!;
        Assert.Equal(0, (int)again["added"]!);
        Assert.Equal(5, DraftingResources.Book(b.Model).TextStyles.Single(t => t.Id == "title").Height);
        Assert.Throws<CommandException>(() => b.Run("standards.importFrom", new JsonObject { ["text"] = "{\"format\":\"lapin-symbols\"}" }));
    }
}
