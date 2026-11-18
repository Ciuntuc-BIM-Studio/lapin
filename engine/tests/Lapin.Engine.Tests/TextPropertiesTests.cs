using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A drafted text's properties changed from the properties panel (forum topic 148): its style, its alignment and its width,
/// each by <c>element.update</c> in one undo step; and a section's marker text of its own (in-app report #167).
/// </summary>
public class TextPropertiesTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonObject Draw(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static List<JsonObject> DrawnTexts(Fixture f, string view) =>
        Draw(f, view)["pens"]!.AsArray().OfType<JsonObject>().Where(p => p["texts"] != null).SelectMany(p => p["texts"]!.AsArray().OfType<JsonObject>()).ToList();
    static string Text(Fixture f, string view, string body, double x, double y, double? width = null)
    {
        var a = new JsonObject { ["viewId"] = view, ["at"] = new JsonArray(x, y), ["text"] = body };
        if (width != null) a["width"] = width;
        return (string)f.Run("drafting.addText", a)!["id"]!;
    }
    static void Update(Fixture f, string id, JsonObject attrs) => f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(id), ["attrs"] = attrs });

    [Fact]
    public void A_text_s_width_is_changed_and_taken_off_each_in_one_step()
    {
        var f = F();
        var view = "plan:" + f.Storey;
        var id = Text(f, view, "One two three four", 1, 1);
        Assert.Single(DrawnTexts(f, view), t => (string)t["text"]! == "One two three four");
        // Before the fix: "Annotation has no field „width”".
        Update(f, id, new JsonObject { ["width"] = 0.4 });
        Assert.Equal(0.4, f.Model.Get(id)!.Num("width"));
        Assert.True(DrawnTexts(f, view).Count(t => ((string)t["text"]!).Length < 18) >= 2);
        Update(f, id, new JsonObject { ["width"] = null });
        Assert.Null(f.Model.Get(id)!.NumOrNull("width"));
        f.Session.Undo();
        Assert.Equal(0.4, f.Model.Get(id)!.Num("width"));
        Assert.Throws<CommandException>(() => Update(f, id, new JsonObject { ["width"] = -1 }));
    }

    [Fact]
    public void A_text_s_style_is_changed_by_its_id_or_by_its_name()
    {
        var f = F();
        var view = "plan:" + f.Storey;
        // A style made in Drafting standards takes an id of its own ("text-…"), not its name.
        f.Run("standards.set", new JsonObject { ["kind"] = "text", ["entry"] = new JsonObject { ["name"] = "Title", ["height"] = 5, ["bold"] = true } });
        var book = (JsonObject)f.Session.Query("standards.get", [])!;
        var styleId = (string)book["textStyles"]!.AsArray().OfType<JsonObject>().Single(s => (string)s["name"]! == "Title")["id"]!;
        Assert.NotEqual("Title", styleId);
        var id = Text(f, view, "Living room", 1, 1);
        double Height() => (double)DrawnTexts(f, view).Single(t => (string)t["text"]! == "Living room")["height"]!;
        var before = Height();
        Update(f, id, new JsonObject { ["textStyle"] = styleId });
        Assert.Equal(5, Height());
        f.Session.Undo();
        Assert.Equal(before, Height());
        // A name typed (a script, an older panel) finds its style too.
        Update(f, id, new JsonObject { ["textStyle"] = "Title" });
        Assert.Equal(5, Height());
    }

    [Fact]
    public void A_text_s_alignment_puts_its_lines_left_centred_or_right_of_its_point_or_in_its_box()
    {
        var f = F();
        var view = "plan:" + f.Storey;
        var id = Text(f, view, "Note", 2, 1);
        JsonObject Drawn() => DrawnTexts(f, view).Single(t => (string)t["text"]! == "Note");
        double X() => (double)Drawn()["x"]!;
        // As before: hung from its start.
        Assert.Equal("start", (string)Drawn()["anchor"]!);
        Assert.Equal(2, X(), 6);
        Update(f, id, new JsonObject { ["align"] = "center" });
        Assert.Equal(("middle", 2.0), ((string)Drawn()["anchor"]!, Math.Round(X(), 6)));
        Update(f, id, new JsonObject { ["align"] = "right" });
        Assert.Equal(("end", 2.0), ((string)Drawn()["anchor"]!, Math.Round(X(), 6)));
        // In a box of its own width, the lines go to the box's middle or its right side; the box stays where it was drawn.
        Update(f, id, new JsonObject { ["width"] = 3 });
        Assert.Equal(("end", 5.0), ((string)Drawn()["anchor"]!, Math.Round(X(), 6)));
        Update(f, id, new JsonObject { ["align"] = "center" });
        Assert.Equal(("middle", 3.5), ((string)Drawn()["anchor"]!, Math.Round(X(), 6)));
        Update(f, id, new JsonObject { ["align"] = null });
        Assert.Equal(("start", 2.0), ((string)Drawn()["anchor"]!, Math.Round(X(), 6)));
        Assert.Throws<CommandException>(() => Update(f, id, new JsonObject { ["align"] = "justify" }));
    }

    [Fact]
    public void A_leader_note_s_leader_is_taken_off_from_its_properties()
    {
        var f = F();
        var view = "plan:" + f.Storey;
        var id = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = view, ["at"] = new JsonArray(2, 2), ["text"] = "See detail", ["leader"] = new JsonArray(new JsonArray(0, 0)) })!["id"]!;
        Update(f, id, new JsonObject { ["leader"] = null });
        Assert.Null(f.Model.Get(id)!.Attrs["leader"]);
    }

    // ------------------------------------------------------------ a section's marker text (report #167)

    static List<string> Texts(JsonObject d) => d["pens"]!.AsArray().OfType<JsonObject>().Where(p => p["texts"] != null)
        .SelectMany(p => p["texts"]!.AsArray()).Select(t => (string)t!["text"]!).ToList();

    [Fact]
    public void A_section_s_marker_writes_its_name_until_it_is_given_a_mark_of_its_own()
    {
        var f = F();
        f.Wall(0, 0, 6, 0);
        var s = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 2) })!["id"]!;
        var plan = "plan:" + f.Storey;
        Assert.Equal("Section 1", f.Model.Get(s)!.Name);
        Assert.Equal(2, Texts(Draw(f, plan)).Count(t => t == "Section 1"));
        Update(f, s, new JsonObject { ["detailNumber"] = "A" });
        var texts = Texts(Draw(f, plan));
        Assert.Equal(2, texts.Count(t => t == "A"));
        Assert.DoesNotContain("Section 1", texts);
        // Its name stays its own (the browser, the sheet's title).
        Assert.Equal("Section 1", f.Model.Get(s)!.Name);
        // Empty: automatic again.
        Update(f, s, new JsonObject { ["detailNumber"] = "" });
        Assert.Equal(2, Texts(Draw(f, plan)).Count(t => t == "Section 1"));
        f.Session.Undo();
        Assert.Equal(2, Texts(Draw(f, plan)).Count(t => t == "A"));
    }

    [Fact]
    public void A_section_head_symbol_writes_the_mark_given_over_its_number_on_the_sheet()
    {
        var f = F();
        f.Wall(0, 0, 6, 0);
        var s = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 2) })!["id"]!;
        var plan = "plan:" + f.Storey;
        f.Run("symbols.setSlot", new JsonObject { ["slot"] = "sectionHead", ["symbolId"] = "builtin:sym:section-head", ["ownerIds"] = new JsonArray(s) });
        Assert.DoesNotContain("B2", Texts(Draw(f, plan)));
        Update(f, s, new JsonObject { ["detailNumber"] = "B2" });
        Assert.Equal(2, Texts(Draw(f, plan)).Count(t => t == "B2"));
        // The properties' placeholder: what it writes without one.
        var slots = (JsonObject)f.Session.Query("symbols.slots", new JsonObject { ["ownerId"] = s })!;
        Assert.Equal("Section 1", (string)slots["mark"]!["name"]!);
        Assert.Equal("", (string)slots["mark"]!["number"]!);
        Assert.Equal("B2", (string)slots["mark"]!["own"]!);
    }
}
