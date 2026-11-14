using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Hide in view and the temporary hide and isolate of plans and sections (forum topic 73), and Quick select: elements by
/// class, type, storey, a parameter's value or range, in the project or in what a view shows.
/// </summary>
public class ViewHidingTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    /// <summary>Three walls on the lowest storey: two of Block 30 (one 2.5 m high), one of Block 20 (3 m high).</summary>
    static (Fixture F, string A, string B, string C) Walls()
    {
        var f = F();
        var t30 = f.Type("Block 30", ("Concrete", 0.3, true));
        var t20 = f.Type("Block 20", ("Concrete", 0.2, true));
        var a = f.Wall(0, 0, 10, 0, t30, height: 2.5);
        var b = f.Wall(0, 5, 10, 5, t30, height: 3.5);
        var c = f.Wall(0, 10, 10, 10, t20, height: 3);
        return (f, a, b, c);
    }

    /// <summary>The elements a drawing (asked as the interface asks) can pick.</summary>
    static HashSet<string> Shown(Fixture f, string view, JsonObject? extra = null)
    {
        var args = extra ?? new JsonObject();
        args["id"] = view;
        var d = (JsonObject)f.Session.Query("view.drawing", args)!;
        return [.. d["marks"]!.AsArray().Select(m => (string)m!["id"]!)];
    }

    [Fact]
    public void Hide_in_view_keeps_the_elements_on_the_view_a_default_plan_becoming_its_plan_view_in_one_step()
    {
        var (f, a, b, _) = Walls();
        var plan = "plan:" + f.Storey;
        Assert.Contains(a, Shown(f, plan));
        var id = (string)f.Run("view.hideElements", new JsonObject { ["viewId"] = plan, ["ids"] = new JsonArray(a, b) })!["id"]!;
        Assert.NotEqual(plan, id);
        Assert.Equal(id, StoreyPlans.Of(f.Model, f.Storey)!.Id);
        Assert.DoesNotContain(a, Shown(f, id));
        Assert.DoesNotContain(b, Shown(f, id));
        // Printed as shown.
        Assert.DoesNotContain(Views.Render(f.Session.Reader, f.Model.Get(id)!, print: true).Marks, m => m.Owner == a);
        // One back, one shown again, then all.
        f.Run("view.unhideElements", new JsonObject { ["viewId"] = id, ["ids"] = new JsonArray(a) });
        Assert.Contains(a, Shown(f, id));
        Assert.DoesNotContain(b, Shown(f, id));
        f.Run("view.unhideElements", new JsonObject { ["viewId"] = id, ["all"] = true });
        Assert.Contains(b, Shown(f, id));
        Assert.Null(f.Model.Get(id)!.Attrs[ViewHiding.Key]);
        // Each a step of its own: undone, b is hidden again; the first undone, the plan view is gone.
        f.Session.Undo();
        Assert.DoesNotContain(b, Shown(f, id));
        f.Session.Undo();
        f.Session.Undo();
        Assert.Null(StoreyPlans.Of(f.Model, f.Storey));
        Assert.Throws<CommandException>(() => f.Run("view.hideElements", new JsonObject { ["viewId"] = plan, ["ids"] = new JsonArray() }));
    }

    [Fact]
    public void Temporary_hide_and_isolate_are_asked_with_the_drawing_and_never_kept()
    {
        var (f, a, b, c) = Walls();
        var plan = "plan:" + f.Storey;
        var hidden = Shown(f, plan, new JsonObject { ["temp"] = new JsonObject { ["hide"] = new JsonArray(a) } });
        Assert.DoesNotContain(a, hidden);
        Assert.Contains(b, hidden);
        var isolated = Shown(f, plan, new JsonObject { ["temp"] = new JsonObject { ["isolate"] = new JsonArray(c) } });
        Assert.Contains(c, isolated);
        Assert.DoesNotContain(a, isolated);
        Assert.DoesNotContain(b, isolated);
        // Nothing kept: no view made, the plan as it was.
        Assert.Null(StoreyPlans.Of(f.Model, f.Storey));
        Assert.Contains(a, Shown(f, plan));
    }

    [Fact]
    public void Reveal_hidden_draws_what_the_view_hides_in_its_colour_so_it_can_be_picked()
    {
        var (f, a, _, _) = Walls();
        var id = (string)f.Run("view.hideElements", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["ids"] = new JsonArray(a) })!["id"]!;
        Assert.DoesNotContain(a, Shown(f, id));
        Assert.Contains(a, Shown(f, id, new JsonObject { ["reveal"] = true }));
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = id, ["reveal"] = true })!;
        Assert.Contains(d["pens"]!.AsArray(), p => (string?)p!["stroke"] == ViewHiding.RevealColor);
        var plain = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = id })!;
        Assert.DoesNotContain(plain["pens"]!.AsArray(), p => (string?)p!["stroke"] == ViewHiding.RevealColor);
    }

    static string[] Select(Fixture f, JsonObject a) => [.. ((JsonObject)f.Session.Query("select.query", a)!)["ids"]!.AsArray().Select(x => (string)x!)];

    static JsonObject Rule(string field, string op, string? value, string? name = null)
    {
        var o = new JsonObject { ["field"] = field, ["op"] = op };
        if (value != null) o["value"] = value;
        if (name != null) o["name"] = name;
        return o;
    }

    [Fact]
    public void Quick_select_takes_the_elements_matching_its_rules()
    {
        var (f, a, b, c) = Walls();
        Assert.Equal([a, b], Select(f, new JsonObject { ["rules"] = new JsonArray(Rule("type", "equals", "Block 30")) }));
        Assert.Equal([a, b, c], Select(f, new JsonObject { ["rules"] = new JsonArray(Rule("class", "equals", IfcClass.Wall), Rule("storey", "equals", Spatial.StoreyOf(f.Model, a)!.Name)) }));
        Assert.Equal([c], Select(f, new JsonObject { ["rules"] = new JsonArray(Rule("type", "contains", "20")) }));
        // A parameter in a range (both ends in it), or open at one end.
        Assert.Equal([b, c], Select(f, new JsonObject { ["rules"] = new JsonArray(Rule("param", "between", "3..3.5", "height")) }));
        Assert.Equal([a, c], Select(f, new JsonObject { ["rules"] = new JsonArray(Rule("param", "between", "..3", "height")) }));
        // Any of the rules.
        Assert.Equal([a, c], Select(f, new JsonObject { ["match"] = "any", ["rules"] = new JsonArray(Rule("type", "equals", "Block 20"), Rule("param", "less", "3", "height")) }));
        Assert.Throws<CommandException>(() => f.Session.Query("select.query", new JsonObject { ["rules"] = new JsonArray() }));
        Assert.Throws<CommandException>(() => f.Session.Query("select.query", new JsonObject { ["rules"] = new JsonArray(Rule("colour", "equals", "red")) }));
    }

    [Fact]
    public void Quick_select_in_a_view_takes_only_what_it_shows()
    {
        var (f, a, b, _) = Walls();
        var id = (string)f.Run("view.hideElements", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["ids"] = new JsonArray(a) })!["id"]!;
        var rules = new JsonArray(Rule("type", "equals", "Block 30"));
        Assert.Equal([b], Select(f, new JsonObject { ["rules"] = rules.DeepClone(), ["viewId"] = id }));
        Assert.Equal([a, b], Select(f, new JsonObject { ["rules"] = rules.DeepClone() }));
        // Cropped to the first wall's line: only it is shown, and it is hidden.
        f.Run("view.setCrop", new JsonObject { ["id"] = id, ["on"] = true, ["min"] = new JsonArray(-1, -1), ["max"] = new JsonArray(11, 1) });
        Assert.Empty(Select(f, new JsonObject { ["rules"] = rules.DeepClone(), ["viewId"] = id }));
        // Its temporary state too.
        Assert.Equal([a], Select(f, new JsonObject { ["rules"] = rules.DeepClone(), ["viewId"] = id, ["reveal"] = true }));
    }
}
