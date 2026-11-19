using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 173 (decision 732): a dimension's values take the colour of the text style its dimension style names; a
/// dimension style's own text colour goes before it, and a dimension's own colour before both; with none of them, the
/// style's colour (black) as before. The lines keep theirs. What the view draws, the drawing sheets, PDF and DXF are
/// made from, so all of them follow.
/// </summary>
public class DimensionTextColourTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f) => "plan:" + f.Storey;

    static string String(Fixture f) => (string)f.Run("drafting.addDimension", new JsonObject
    {
        ["viewId"] = Plan(f), ["pts"] = new JsonArray(new JsonArray(0, 0), new JsonArray(3, 0)),
        ["at"] = new JsonArray(0, 2), ["dir"] = new JsonArray(1, 0),
    })!["id"]!;

    static ViewMark Mark(Fixture f, string dim) => Views.Render(f.Session.Reader, Views.Find(f.Model, Plan(f))!, false).Marks.Single(m => m.Owner == dim);
    static string TextColour(Fixture f, string dim) => Mark(f, dim).Texts.Single().Color;
    static void Set(Fixture f, string kind, JsonObject entry) => f.Run("standards.set", new JsonObject { ["kind"] = kind, ["entry"] = entry });

    /// <summary>The user's case: a text style of its own colour, a dimension style of its own line colours naming it.</summary>
    static string Styled(Fixture f)
    {
        Set(f, "text", new JsonObject { ["id"] = "blue-text", ["name"] = "Blue text", ["height"] = 2.5, ["color"] = "#1f4fd1" });
        Set(f, "dim", new JsonObject { ["id"] = "mine", ["name"] = "Mine", ["textStyle"] = "blue-text", ["dimLineColor"] = "#c0392b", ["extColor"] = "#27ae60" });
        var dim = String(f);
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(dim), ["attrs"] = new JsonObject { ["dimStyle"] = "mine" } });
        return dim;
    }

    [Fact]
    public void A_dimension_writes_its_values_in_its_text_styles_colour()
    {
        var f = F();
        var dim = Styled(f);
        var m = Mark(f, dim);
        Assert.Equal("#1f4fd1", m.Texts.Single().Color);
        // The lines keep their own colours.
        Assert.Contains(m.Lines, l => l.Color == "#c0392b");
        Assert.Contains(m.Lines, l => l.Color == "#27ae60");
        Assert.DoesNotContain(m.Lines, l => l.Color == "#1f4fd1");
        // The view's drawing (what sheets, the PDF and DXF are made of) carries it.
        Assert.Contains("#1f4fd1", f.Session.Query("view.drawing", new JsonObject { ["id"] = Plan(f) })!.ToJsonString());
    }

    [Fact]
    public void The_dimension_styles_own_text_colour_goes_before_its_text_styles_and_the_dimensions_own_before_both()
    {
        var f = F();
        var dim = Styled(f);
        Set(f, "dim", new JsonObject { ["id"] = "mine", ["textColor"] = "#8e44ad" });
        Assert.Equal("#8e44ad", TextColour(f, dim));
        Assert.Equal("#8e44ad", DraftingResources.Book(f.Model).DimStyles.Single(s => s.Id == "mine").TextColor);
        // A dimension's own colour (its override, as a DXF's dimension brings it) goes before all of them, its lines too.
        var book = DraftingResources.Book(f.Model);
        var own = new JsonObject { ["dimStyle"] = "mine", ["o.color"] = "#ff8800" };
        Assert.Equal("#ff8800", Standards.DimTextColor(own, Standards.ResolveDim(book, own), Standards.ResolveText(book, own)));
        // The style's text colour cleared: the text style's again.
        Set(f, "dim", new JsonObject { ["id"] = "mine", ["textColor"] = null });
        Assert.Equal("#1f4fd1", TextColour(f, dim));
    }

    [Fact]
    public void Without_any_text_colour_the_values_are_the_styles_colour_as_before()
    {
        var f = F();
        var dim = String(f);
        Assert.Equal("#000000", TextColour(f, dim));
        Set(f, "dim", new JsonObject { ["id"] = Standards.Standard, ["color"] = "#336699" });
        Assert.Equal("#336699", TextColour(f, dim));
        // Nothing new is written for a style that sets no text colour.
        Assert.DoesNotContain("textColor", Standards.ToJson(DraftingResources.Book(f.Model)).ToJsonString());
    }

    [Fact]
    public void An_angular_dimension_follows_the_same_rule()
    {
        var f = F();
        Set(f, "text", new JsonObject { ["id"] = "blue-text", ["name"] = "Blue text", ["height"] = 2.5, ["color"] = "#1f4fd1" });
        Set(f, "dim", new JsonObject { ["id"] = Standards.Standard, ["textStyle"] = "blue-text" });
        var a = (string)f.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = Plan(f), ["dimKind"] = "angular", ["c"] = new JsonArray(0, 0), ["a"] = new JsonArray(2, 0), ["b"] = new JsonArray(0, 2), ["at"] = new JsonArray(1, 1),
        })!["id"]!;
        var m = Mark(f, a);
        Assert.Equal("#1f4fd1", m.Texts.Single().Color);
        Assert.All(m.Lines, l => Assert.Equal("#000000", l.Color));
    }
}
