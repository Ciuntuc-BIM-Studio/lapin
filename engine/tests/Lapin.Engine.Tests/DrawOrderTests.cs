using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Report #44, decision 339: a view's drafted elements (lines, hatches and fills, texts, dimensions, tags) brought to
/// the front, a step forward past what they overlap, a step backward, to the back of the drafting or behind the model,
/// above or under chosen ones (AutoCAD's DRAWORDER); per view, kept in the model, one undo step; drawn and picked in
/// that order on screen, on sheets and in the DXF.
/// </summary>
public class DrawOrderTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static string Line(Fixture f, string view, double x1, double y1, double x2, double y2) =>
        (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) }) })!["ids"]![0]!;
    static string Square(Fixture f, string view, double x, double y, double s) =>
        (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(x, y), J(x + s, y), J(x + s, y + s), J(x, y + s)), ["closed"] = true }) })!["ids"]![0]!;
    static string Fill(Fixture f, string view, double x, double y) =>
        (string)f.Run("drafting.hatch", new JsonObject { ["viewId"] = view, ["at"] = J(x, y), ["pattern"] = "SOLID", ["color"] = "#ffffff" })!["id"]!;
    static string Text(Fixture f, string view, double x, double y, string body) =>
        (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = view, ["at"] = J(x, y), ["text"] = body })!["id"]!;
    static void Order(Fixture f, string move, string[] ids, string[]? refs = null)
    {
        var a = new JsonObject { ["ids"] = new JsonArray(ids.Select(i => (JsonNode)i).ToArray()), ["move"] = move };
        if (refs != null) a["refs"] = new JsonArray(refs.Select(i => (JsonNode)i).ToArray());
        f.Run("drafting.drawOrder", a);
    }

    /// <summary>The view's stack bottom up, and how many lie behind the model.</summary>
    static (List<string> Ids, int Behind) Stack(Fixture f, string view) { var (s, b) = DrawOrder.Stack(f.Model, view); return (s.Select(e => e.Id).ToList(), b); }

    /// <summary>Where in the drawn pens (index) each element's ink lies: the white fill's, a text's, a line's.</summary>
    static int PenOf(Fixture f, string view, Func<Lapin.Drawing.Views.Pen, bool> which) =>
        Views.Render(f.Session.Reader, Views.Find(f.Model, view)!).Pens.FindIndex(p => which(p));

    [Fact]
    public void A_view_draws_what_was_drafted_in_the_order_it_always_did_until_an_order_is_set()
    {
        var f = F();
        var plan = Plan(f);
        var sq = Square(f, plan, 0, 0, 4);
        var fill = Fill(f, plan, 1, 1);
        var text = Text(f, plan, 1, 1, "Note");
        // Shapes first, then texts; nothing numbered.
        Assert.Equal([sq, fill, text], Stack(f, plan).Ids);
        Assert.All(new[] { sq, fill, text }, id => Assert.Null(f.Model.Get(id)!.NumOrNull(DrawOrder.Key)));
        // As always: the drafting's fills under its lines and texts.
        Assert.True(PenOf(f, plan, p => p.Fill == "#ffffff") < PenOf(f, plan, p => p.Texts.Count > 0));
    }

    [Fact]
    public void Bring_to_front_puts_a_fill_over_the_text_it_covers_and_send_to_back_under_it_in_one_undo_step()
    {
        var f = F();
        var plan = Plan(f);
        var sq = Square(f, plan, 0, 0, 4);
        var fill = Fill(f, plan, 1, 1);
        var text = Text(f, plan, 1, 1, "Note");
        Order(f, "front", [fill]);
        Assert.Equal([sq, text, fill], Stack(f, plan).Ids);
        // Every element of the view numbered from 1, over the model.
        Assert.Equal([1.0, 2, 3], new[] { sq, text, fill }.Select(id => f.Model.Get(id)!.Num(DrawOrder.Key)));
        // The white fill now covers the text: its pen after the text's.
        Assert.True(PenOf(f, plan, p => p.Fill == "#ffffff") > PenOf(f, plan, p => p.Texts.Count > 0));
        // A click takes the top one first.
        var marks = Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!).Marks.Where(m => m.Owner is { } o && new[] { sq, fill, text }.Contains(o)).Select(m => m.Owner).ToList();
        Assert.Equal([fill, text, sq], marks);
        Order(f, "back", [fill]);
        Assert.Equal([fill, sq, text], Stack(f, plan).Ids);
        // One step to undo.
        f.Session.Undo();
        Assert.Equal([sq, text, fill], Stack(f, plan).Ids);
        // New drafting comes on top of the numbered.
        var later = Line(f, plan, 0, 2, 4, 2);
        Assert.Equal(later, Stack(f, plan).Ids[^1]);
    }

    [Fact]
    public void Forward_and_backward_step_past_the_next_element_they_overlap()
    {
        var f = F();
        var plan = Plan(f);
        var a = Square(f, plan, 0, 0, 2);
        var far = Line(f, plan, 50, 50, 60, 50);
        var b = Square(f, plan, 1, 1, 2);
        Order(f, "front", [a, far, b]);
        // a goes past b (it overlaps it), not just past the far line, which is next but elsewhere.
        Order(f, "forward", [a]);
        Assert.Equal([far, b, a], Stack(f, plan).Ids);
        Order(f, "backward", [a]);
        Assert.Equal([far, a, b], Stack(f, plan).Ids);
        // Nothing it overlaps under it: it stays.
        Order(f, "backward", [a]);
        Assert.Equal([far, a, b], Stack(f, plan).Ids);
        // Above and under chosen elements (DRAWORDER's options).
        Order(f, "above", [far], [b]);
        Assert.Equal([a, b, far], Stack(f, plan).Ids);
        Order(f, "under", [far], [a]);
        Assert.Equal([far, a, b], Stack(f, plan).Ids);
        Assert.Throws<CommandException>(() => Order(f, "above", [far], [far]));
        Assert.Throws<CommandException>(() => Order(f, "sideways", [far]));
    }

    [Fact]
    public void Behind_the_model_a_hatch_draws_under_a_walls_cut_and_a_click_takes_the_wall()
    {
        var f = F();
        var type = f.Type("Core 25", ("Concrete", 0.25, true));
        var wall = f.Wall(0, 2, 6, 2, type);
        var plan = Plan(f);
        var sq = Square(f, plan, 1, 0, 4);
        var fill = Fill(f, plan, 2, 1);
        var render = () => Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!).Pens;
        var cut = (List<Lapin.Drawing.Views.Pen> pens) => pens.FindIndex(p => p.Layer == "CUT");
        // Over the model by default.
        Assert.True(render().FindIndex(p => p.Fill == "#ffffff") > cut(render()));
        Order(f, "behind", [fill]);
        Assert.Equal(1, Stack(f, plan).Behind);
        Assert.True(f.Model.Get(fill)!.Num(DrawOrder.Key) < 0);
        Assert.True(render().FindIndex(p => p.Fill == "#ffffff") < cut(render()));
        // The interface hears it lies behind, so a click on the wall there takes the wall.
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        Assert.True((bool?)d["marks"]!.AsArray().OfType<JsonObject>().Single(m => (string)m["id"]! == fill)["behind"] == true);
        // Forward does not cross the model; to the front (or back) brings it over it again.
        Order(f, "forward", [fill]);
        Assert.Equal(1, Stack(f, plan).Behind);
        Order(f, "back", [fill]);
        Assert.Equal(0, Stack(f, plan).Behind);
        Assert.Equal([fill, sq], Stack(f, plan).Ids);
        Assert.NotNull(wall);
    }

    [Fact]
    public void The_order_is_the_views_own_and_the_DXF_writes_its_entities_in_it()
    {
        var f = F();
        var plan = Plan(f);
        var other = Plan(f);
        var mine = Line(f, plan, 0, 0, 1, 0);
        var theirs = Line(f, other, 0, 0, 1, 0);
        var text = Text(f, plan, 0, 0, "ZZTOP");
        Order(f, "front", [mine, theirs]);
        Assert.Equal([text, mine], Stack(f, plan).Ids);
        Assert.Equal([theirs], Stack(f, other).Ids);
        var dxf = System.Text.Encoding.Latin1.GetString(Views.Dxf(f.Session.Reader, Views.Find(f.Model, plan)!));
        // The text now drawn under the line: written before it.
        var (t, l) = (dxf.IndexOf("ZZTOP", StringComparison.Ordinal), dxf.LastIndexOf("\nLINE", StringComparison.Ordinal));
        Assert.True(t > 0 && l > t, "the text before the line in the DXF's entities");
    }
}
