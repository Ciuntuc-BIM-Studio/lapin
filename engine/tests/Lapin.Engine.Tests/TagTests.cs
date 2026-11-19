using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Tags after the tester's report #64: any parameter shown (the user's own on the element or its type, a standard set's,
/// in the project's units), and a leader that can be bent, pointed elsewhere and switched off, drawn by the engine.
/// </summary>
public class TagTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonObject Draw(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static List<string> Texts(JsonObject d) => d["pens"]!.AsArray().OfType<JsonObject>().Where(p => p["texts"] != null)
        .SelectMany(p => p["texts"]!.AsArray()).Select(t => (string)t!["text"]!).ToList();
    static JsonObject Mark(JsonObject d, string id) => d["marks"]!.AsArray().OfType<JsonObject>().First(m => (string)m["id"]! == id);
    static double[] Pt(JsonNode? n) => n!.AsArray().Select(x => (double)x!).ToArray();
    static string Tag(Fixture f, string view, string element, string? field, double x, double y) =>
        (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = element, ["field"] = field, ["at"] = new JsonArray(x, y) })!["id"]!;

    [Fact]
    public void A_tag_shows_a_type_parameter_and_an_element_s_own_value_over_it()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var view = "plan:" + f.Storey;
        var type = f.Model.RelatingOf(w, IfcClass.RelDefinesByType)!.Id;
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(type), ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "REI 180" });
        // Named without its space, in braces as a template writes it: the same parameter.
        Tag(f, view, w, "{FireRating}", 2.5, 2);
        Assert.Contains("REI 180", Texts(Draw(f, view)));
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(w), ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "REI 90" });
        Assert.Contains("REI 90", Texts(Draw(f, view)));
        Assert.DoesNotContain("REI 180", Texts(Draw(f, view)));
    }

    [Fact]
    public void A_tag_shows_a_standard_set_s_property_and_a_length_in_the_project_s_units()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var view = "plan:" + f.Storey;
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(w), ["pset"] = "Pset_WallCommon", ["name"] = "AcousticRating", ["value"] = "Rw 52 dB" });
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(w), ["name"] = "Insulation", ["kind"] = "length", ["value"] = 0.1 });
        Tag(f, view, w, "Pset_WallCommon.AcousticRating", 2.5, 2);
        var ins = Tag(f, view, w, "Insulation", 2.5, -2);
        var texts = Texts(Draw(f, view));
        Assert.Contains("Rw 52 dB", texts);
        Assert.Contains("100", texts);
        f.Run("standards.options", new JsonObject { ["units"] = "ft-in" });
        Assert.Contains("3 15/16\"", Texts(Draw(f, view)));
        // A template of its own, two lines: the box grows to hold them.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = ins, ["template"] = "{name}|{Insulation}" });
        var after = Texts(Draw(f, view));
        Assert.Contains("3 15/16\"", after);
        Assert.Contains(f.Model.Get(w)!.Name, after);
    }

    [Fact]
    public void The_fields_a_tag_can_show_list_the_parameters_and_the_standard_set_s_properties()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var type = f.Model.RelatingOf(w, IfcClass.RelDefinesByType)!.Id;
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(type), ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "REI 180" });
        var fields = ((JsonArray)f.Session.Query("drafting.tagFields", new JsonObject { ["element"] = w })!).OfType<JsonObject>().ToList();
        var fire = fields.First(x => (string)x["field"]! == "Fire rating");
        Assert.Equal("type", (string)fire["group"]!);
        Assert.Equal("REI 180", (string)fire["value"]!);
        Assert.Contains(fields, x => (string)x["field"]! == "Pset_WallCommon.FireRating" && (string)x["group"]! == "standard");
        Assert.Contains(fields, x => (string)x["field"]! == "mark");
        Assert.DoesNotContain(fields, x => ((string)x["field"]!).StartsWith("Pset_DoorCommon"));
    }

    [Fact]
    public void A_leader_bends_at_its_elbow_points_where_it_is_told_and_can_be_switched_off()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var view = "plan:" + f.Storey;
        var t = Tag(f, view, w, "name", 2.5, 3);
        var data = Mark(Draw(f, view), t)["data"]!;
        Assert.Equal(2.5, Pt(data["end"])[0], 3);                                   // the wall's middle
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["elbow"] = new JsonArray(1, 3), ["end"] = new JsonArray(1, 0) });
        data = Mark(Draw(f, view), t)["data"]!;
        Assert.Equal([1.0, 0.0], Pt(data["end"]));
        Assert.Equal([1.0, 3.0], Pt(data["elbow"]));
        Assert.True(Pt(data["from"])[0] < 2.5);                                     // it leaves the box's left side, towards the elbow
        // The tag's head moved alone: the elbow goes with it, the end stays on the wall.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(t), ["op"] = "move", ["by"] = new JsonArray(0, 1) });
        var e = f.Model.Get(t)!;
        Assert.Equal([1.0, 4.0], Pt(e.Attrs["elbow"]));
        Assert.Equal([1.0, 0.0], Pt(e.Attrs["end"]));
        // Moved with the wall: all of it.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(t, w), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        Assert.Equal([2.0, 0.0], Pt(f.Model.Get(t)!.Attrs["end"]));
        // Straight again, then off.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["elbow"] = null });
        Assert.Null(Mark(Draw(f, view), t)["data"]!["elbow"]);
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["leader"] = false });
        data = Mark(Draw(f, view), t)["data"]!;
        Assert.False((bool)data["leader"]!);
        Assert.Null(data["end"]);
        Assert.Throws<CommandException>(() => f.Run("drafting.updateTag", new JsonObject { ["id"] = w, ["leader"] = true }));
        Assert.Throws<CommandException>(() => f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["field"] = "  " }));
    }

    [Fact]
    public void A_text_tag_says_what_was_typed_its_fields_live_on_a_tags_leader_and_follows_its_element()
    {
        // Issue #92 (report #162): a tag with a text box instead of a field.
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var view = "plan:" + f.Storey;
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(w), ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "REI 90" });
        var t = (string)f.Run("drafting.addTag", new JsonObject
        {
            ["viewId"] = view, ["element"] = w, ["at"] = new JsonArray(2.5, 2), ["text"] = "Check on site | twice\n\nFire: {FireRating}\n",
        })!["id"]!;
        Assert.Equal("Text tag", f.Model.Get(t)!.Name);
        var d = Draw(f, view);
        var texts = Texts(d);
        // Its lines as typed (a bar kept, an empty line between them), its field read from the wall.
        Assert.Contains("Check on site | twice", texts);
        Assert.Contains("Fire: REI 90", texts);
        Assert.Equal(["Check on site | twice", "", "Fire: REI 90"], Tags.FreeLines(f.Model, "\nCheck on site | twice\n\nFire: {FireRating}\n\n", f.Model.Get(w), "mm"));
        // A tag's leader to the wall, drawn by the engine.
        Assert.NotNull(Mark(d, t)["data"]!["end"]);
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(w), ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "REI 120" });
        Assert.Contains("Fire: REI 120", Texts(Draw(f, view)));
        // It follows the wall as any tag.
        var before = Pt(Mark(Draw(f, view), t)["data"]!["at"]);
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(w), ["op"] = "move", ["by"] = new JsonArray(0, -3) });
        var after = Pt(Mark(Draw(f, view), t)["data"]!["at"]);
        Assert.Equal(before[1] - 3, after[1], 6);
        // Edited (a double-click): the new words; emptied: a tag that reads its field again; a field chosen takes its text off.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["text"] = "Kept" });
        Assert.Contains("Kept", Texts(Draw(f, view)));
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["text"] = "" });
        Assert.Null(f.Model.Get(t)!.Attrs["text"]);
        Assert.DoesNotContain("Kept", Texts(Draw(f, view)));
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["text"] = "Again" });
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["field"] = "name" });
        Assert.Null(f.Model.Get(t)!.Attrs["text"]);
        // A text tag says something.
        Assert.Throws<CommandException>(() => f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = w, ["at"] = new JsonArray(1, 1), ["text"] = " \n " }));
    }

    [Fact]
    public void A_text_tag_of_a_build_up_type_writes_its_own_words_in_the_types_style()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var view = "plan:" + f.Storey;
        var t = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = w, ["tagType"] = "buildup", ["at"] = new JsonArray(2.5, 2), ["text"] = "See detail 4" })!["id"]!;
        var texts = Texts(Draw(f, view));
        Assert.Contains("See detail 4", texts);
        Assert.Equal("buildup", f.Model.Get(t)!.Str("tagType"));
    }
}
