using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The section and elevation markers' size (decision 455): a symbol size and a text height in the project's drafting
/// standards, each marker's own over them, drawn by the engine in plans and in the views a section crosses. The elevation's
/// marker in plans and its line's ends, which set how far the elevation reaches (decision 456).
/// </summary>
public sealed class ViewMarkerSizeTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonObject? Mark(JsonObject d, string id) => ((JsonArray)d["marks"]!).Select(m => (JsonObject)m!).FirstOrDefault(m => (string?)m["id"] == id);
    static List<(double X0, double Y0, double X1, double Y1)> Segments(JsonObject mark)
    {
        var s = ((JsonArray)mark["segments"]!).Select(x => (double)x!).ToArray();
        return Enumerable.Range(0, s.Length / 4).Select(k => (s[4 * k], s[4 * k + 1], s[4 * k + 2], s[4 * k + 3])).ToList();
    }
    static double TextHeight(JsonObject d, string text) =>
        ((JsonArray)d["pens"]!).SelectMany(p => (JsonArray?)p!["texts"] ?? []).Where(t => (string?)t!["text"] == text).Select(t => (double)t!["height"]!).First();

    static (string Section, string Elevation, string Plan) House(Fixture f)
    {
        f.Wall(0, 0, 10, 0); f.Wall(10, 0, 10, 8); f.Wall(10, 8, 0, 8); f.Wall(0, 8, 0, 0);
        // A-A up through the house, looking west (left of its line).
        var a = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["name"] = "A-A", ["start"] = new JsonArray(6, -2), ["end"] = new JsonArray(6, 10) })!["id"]!;
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        return (a, south, plan);
    }

    /// <summary>The arrow at the section's start: the stroke from (6, −2) across the line, its length the arrow's.</summary>
    static double Arrow(JsonObject plan, string section) =>
        Segments(Mark(plan, section)!).Where(s => Math.Abs(s.X0 - 6) < 1e-6 && Math.Abs(s.Y0 + 2) < 1e-6 && Math.Abs(s.Y1 + 2) < 1e-6).Select(s => Math.Abs(s.X1 - s.X0)).Max();

    [Fact]
    public void A_marker_is_sized_by_the_drafting_standards_and_by_its_own_fields()
    {
        var f = F();
        var (a, south, plan) = House(f);
        // As before: a 7 mm arrow (0.7 m at 1:100) and the Standard text's 2.5 mm.
        var d = Drawing(f, plan);
        Assert.Equal(0.7, Arrow(d, a), 6);
        Assert.Equal(2.5, TextHeight(d, "A-A"), 6);
        Assert.Equal(2.5, TextHeight(d, "South elevation"), 6);
        Assert.Equal(2.5, TextHeight(Drawing(f, south), "A-A"), 6);

        // The project's: a 20 mm symbol (the arrow 0.7 of it) and 4 mm texts, in plans and in the views a section crosses.
        f.Run("standards.options", new JsonObject { ["markSize"] = 20, ["markText"] = 4 });
        var book = (JsonObject)f.Session.Query("standards.get", new JsonObject())!;
        Assert.Equal(20, (double)book["markSize"]!);
        Assert.Equal(4, (double)book["markText"]!);
        d = Drawing(f, plan);
        Assert.Equal(1.4, Arrow(d, a), 6);
        Assert.Equal(4, TextHeight(d, "A-A"), 6);
        Assert.Equal(4, TextHeight(d, "South elevation"), 6);
        Assert.Equal(4, TextHeight(Drawing(f, south), "A-A"), 6);

        // A marker's own over the project's; cleared, the project's again.
        f.Run("element.update", new JsonObject { ["id"] = a, ["attrs"] = new JsonObject { ["markSize"] = 5, ["markTextSize"] = 3 } });
        d = Drawing(f, plan);
        Assert.Equal(0.35, Arrow(d, a), 6);
        Assert.Equal(3, TextHeight(d, "A-A"), 6);
        Assert.Equal(3, TextHeight(Drawing(f, south), "A-A"), 6);
        Assert.Equal(4, TextHeight(d, "South elevation"), 6);
        f.Run("element.update", new JsonObject { ["id"] = south, ["attrs"] = new JsonObject { ["markTextSize"] = 6 } });
        Assert.Equal(6, TextHeight(Drawing(f, plan), "South elevation"), 6);
        f.Run("element.update", new JsonObject { ["id"] = a, ["attrs"] = new JsonObject { ["markSize"] = null, ["markTextSize"] = null } });
        Assert.Equal(1.4, Arrow(Drawing(f, plan), a), 6);

        // Out of range is refused; empty in the standards is the default again.
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["markSize"] = 0 }));
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = a, ["attrs"] = new JsonObject { ["markSize"] = 500 } }));
        f.Run("standards.options", new JsonObject { ["markSize"] = null, ["markText"] = null });
        d = Drawing(f, plan);
        Assert.Equal(0.7, Arrow(d, a), 6);
        Assert.Equal(2.5, TextHeight(d, "A-A"), 6);
    }

    [Fact]
    public void An_elevation_is_marked_in_plans_in_front_of_the_building_looking_at_it()
    {
        var f = F();
        var (_, south, plan) = House(f);
        var mk = Mark(Drawing(f, plan), south);
        Assert.NotNull(mk);
        var data = (JsonObject)mk!["data"]!;
        // Its line runs across the house's width (x, its right), south of it (y < 0), the symbol beyond it.
        var ends = ((JsonArray)data["ends"]!).Select(p => ((double)p![0]!, (double)p[1]!)).ToList();
        Assert.True(ends[0].Item2 < 0 && Math.Abs(ends[0].Item2 - ends[1].Item2) < 1e-9);
        Assert.True(ends.Min(e => e.Item1) < 0 && ends.Max(e => e.Item1) > 10);
        Assert.Equal(1.0, (double)data["right"]![0]!, 9);
        Assert.False((bool)data["own"]!);
        Assert.True(Segments(mk).Min(s => Math.Min(s.Y0, s.Y1)) < ends[0].Item2 - 0.4);
        // Hidden with the elevation marks' category.
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["annotation"] = "elevations", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Null(Mark(Drawing(f, plan), south));
    }

    [Fact]
    public void An_elevation_s_line_ends_set_how_far_it_reaches_in_one_undo_step()
    {
        var f = F();
        var (_, south, plan) = House(f);
        var whole = Drawing(f, south);
        var ox = (double)whole["frame"]!["origin"]![0]!;
        double Width(JsonObject d) { var b = (JsonArray)d["bounds"]!; return (double)b[2]! - (double)b[0]!; }
        var before = Width(whole);

        f.Run("view.setExtent", new JsonObject { ["id"] = south, ["min"] = 2, ["max"] = 6 });
        var cropped = Drawing(f, south);
        var b = (JsonArray)cropped["bounds"]!;
        // The level lines run 1 m before and 1.5 m past what is drawn at 1:100, their markers and names a little further.
        Assert.True((double)b[0]! >= 2 - ox - 1.0 - 1e-6, cropped["bounds"]!.ToJsonString());
        Assert.True((double)b[2]! <= 6 - ox + 2.5, cropped["bounds"]!.ToJsonString());
        Assert.True(Width(cropped) < before - 5);
        // The plan's marker follows: its line from x = 2 to x = 6.
        var data = (JsonObject)Mark(Drawing(f, plan), south)!["data"]!;
        Assert.Equal(2, (double)data["min"]!, 9);
        Assert.Equal(6, (double)data["max"]!, 9);
        Assert.True((bool)data["own"]!);
        var xs = ((JsonArray)data["ends"]!).Select(p => (double)p![0]!).Order().ToList();
        Assert.Equal([2.0, 6.0], xs);

        // One end alone: the other stays.
        f.Run("view.setExtent", new JsonObject { ["id"] = south, ["max"] = 8 });
        Assert.Equal(2, f.Model.Get(south)!.Num("extentMin"), 9);
        Assert.Equal(8, f.Model.Get(south)!.Num("extentMax"), 9);
        f.Session.Undo();
        Assert.Equal(6, f.Model.Get(south)!.Num("extentMax"), 9);
        Assert.Throws<CommandException>(() => f.Run("view.setExtent", new JsonObject { ["id"] = south, ["min"] = 6, ["max"] = 6 }));
        Assert.Throws<CommandException>(() => f.Run("view.setExtent", new JsonObject { ["id"] = plan, ["min"] = 0, ["max"] = 1 }));
        // Both cleared: the whole building again.
        f.Run("view.setExtent", new JsonObject { ["id"] = south, ["min"] = null, ["max"] = null });
        Assert.Null(f.Model.Get(south)!.NumOrNull("extentMin"));
        Assert.Equal(before, Width(Drawing(f, south)), 6);
    }
}
