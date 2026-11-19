using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// What a section or an elevation sees beyond its cut (issue #93, decision 721): its depth (the far clip, typed or dragged
/// in plan: view.setDepth, one undo step), and its style's or its own <c>beyond</c>: full (as before), near (what lies wholly
/// farther than <c>nearDepth</c> in the background pen) or cut (only what the plane cuts, for details).
/// </summary>
public sealed class SectionDepthTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonObject? Mark(JsonObject d, string id) => ((JsonArray)d["marks"]!).Select(m => (JsonObject)m!).FirstOrDefault(m => (string?)m["id"] == id);
    static List<JsonObject> Pens(JsonObject d, string layer) => ((JsonArray)d["pens"]!).Select(p => (JsonObject)p!).Where(p => (string?)p["layer"] == layer).ToList();
    static int Segments(JsonObject d, string layer) => Pens(d, layer).Sum(p => ((JsonArray?)p["segments"])?.Count ?? 0);

    /// <summary>A 10 × 8 m house of four walls and a section across it at y = 4, looking north (at the north wall, 4 m away).</summary>
    static (string South, string North, string West, string Section) House(Fixture f)
    {
        var south = f.Wall(0, 0, 10, 0); f.Wall(10, 0, 10, 8); var north = f.Wall(10, 8, 0, 8); var west = f.Wall(0, 8, 0, 0);
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, 4), ["end"] = new JsonArray(11, 4) })!["id"]!;
        return (south, north, west, section);
    }

    [Fact]
    public void Full_depth_draws_what_lies_beyond_as_before()
    {
        var f = F();
        var (_, north, west, section) = House(f);
        var d = Drawing(f, section);
        Assert.NotNull(Mark(d, north));
        Assert.NotNull(Mark(d, west)!["data"]?["loops"]);
        Assert.Equal(0, Segments(d, "BEYOND"));
    }

    [Fact]
    public void Cut_only_drops_the_lines_beyond_the_cut()
    {
        var f = F();
        var (_, north, west, section) = House(f);
        var full = Drawing(f, section);
        f.Run("view.setStyle", new JsonObject { ["id"] = section, ["beyond"] = "cut" });
        var cut = Drawing(f, section);
        // The north wall, seen beyond, is gone; the west wall, cut, stays with its cut lines.
        Assert.Null(Mark(cut, north));
        Assert.NotNull(Mark(cut, west)!["data"]?["loops"]);
        Assert.Equal(0, Segments(cut, "SEEN"));
        Assert.True(Segments(cut, "CUT") > 0);
        Assert.Equal(Segments(full, "CUT"), Segments(cut, "CUT"));
        // Back to its style's (full): as before.
        f.Run("view.setStyle", new JsonObject { ["id"] = section, ["beyond"] = null });
        Assert.Equal(full.ToJsonString(), Drawing(f, section).ToJsonString());
    }

    [Fact]
    public void Near_puts_what_lies_farther_in_the_background_pen()
    {
        var f = F();
        var (_, north, _, section) = House(f);
        var full = Drawing(f, section);
        var seenBefore = Segments(full, "SEEN");
        Assert.True(seenBefore > 0);
        // Near 2 m: the north wall, 4 m away, goes to the background: thin and grey, on its own layer.
        f.Run("view.setStyle", new JsonObject { ["id"] = section, ["beyond"] = "near", ["nearDepth"] = 2 });
        var near = Drawing(f, section);
        Assert.NotNull(Mark(near, north));
        Assert.True(Segments(near, "BEYOND") > 0);
        Assert.True(Segments(near, "SEEN") < seenBefore);
        var beyond = Pens(near, "BEYOND")[0];
        var seen = Pens(full, "SEEN")[0];
        Assert.Equal(Links.Halftone, (string?)beyond["stroke"]);
        Assert.True((double)beyond["width"]! < (double)seen["width"]!);
        // Near 5 m: the north wall is near again.
        f.Run("view.setStyle", new JsonObject { ["id"] = section, ["nearDepth"] = 5 });
        Assert.Equal(0, Segments(Drawing(f, section), "BEYOND"));
    }

    [Fact]
    public void A_view_style_says_it_for_its_views_and_a_view_sets_its_own()
    {
        var f = F();
        var (_, north, _, section) = House(f);
        var styles = (JsonArray)f.Session.Query("viewStyle.list", new JsonObject())!;
        var architectural = (string)styles.Select(s => (JsonObject)s!).First(s => (string?)s["discipline"] == "Architecture")["id"]!;
        // A style saved before decision 721 writes neither field: it draws everything.
        Assert.Null(styles[0]!["beyond"]);
        f.Run("viewStyle.update", new JsonObject { ["id"] = architectural, ["beyond"] = "cut" });
        Assert.Null(Mark(Drawing(f, section), north));
        // The view's own over its style's.
        f.Run("view.setStyle", new JsonObject { ["id"] = section, ["beyond"] = "full" });
        Assert.NotNull(Mark(Drawing(f, section), north));
        Assert.Throws<CommandException>(() => f.Run("view.setStyle", new JsonObject { ["id"] = section, ["beyond"] = "far" }));
        Assert.Throws<CommandException>(() => f.Run("viewStyle.update", new JsonObject { ["id"] = architectural, ["nearDepth"] = -1 }));
    }

    [Fact]
    public void The_far_clip_moves_by_view_setDepth_in_one_undo_step()
    {
        var f = F();
        var (_, north, west, section) = House(f);
        // Dragged (or typed) to 2 m: the north wall, 4 m away, is beyond it.
        f.Run("view.setDepth", new JsonObject { ["id"] = section, ["depth"] = 2 });
        Assert.Equal(2, f.Session.Model.Get(section)!.Num("depth"), 6);
        var shallow = Drawing(f, section);
        Assert.Null(Mark(shallow, north));
        Assert.NotNull(Mark(shallow, west));
        Assert.Equal(2, (double)shallow["depth"]!, 6);
        f.Session.Undo();
        Assert.Null(f.Session.Model.Get(section)!.NumOrNull("depth"));
        Assert.NotNull(Mark(Drawing(f, section), north));
        // Null: without end again.
        f.Run("view.setDepth", new JsonObject { ["id"] = section, ["depth"] = 2 });
        f.Run("view.setDepth", new JsonObject { ["id"] = section, ["depth"] = null });
        Assert.Null(f.Session.Model.Get(section)!.NumOrNull("depth"));
        Assert.Throws<CommandException>(() => f.Run("view.setDepth", new JsonObject { ["id"] = section, ["depth"] = 0 }));
    }

    [Fact]
    public void An_elevation_sees_as_deep_as_its_depth_from_its_front()
    {
        var f = F();
        var (south, _, _, _) = House(f);
        // A wall standing beside the house, 6 m behind its south facade, seen past it.
        var shed = f.Wall(12, 6, 15, 6);
        var elevation = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        Assert.NotNull(Mark(Drawing(f, elevation), shed));
        // 3 m from the south facade: the wall beside, 6 m in, is not drawn.
        f.Run("view.setDepth", new JsonObject { ["id"] = elevation, ["depth"] = 3 });
        var shallow = Drawing(f, elevation);
        Assert.NotNull(Mark(shallow, south));
        Assert.Null(Mark(shallow, shed));
        // The plan's marker tells its grip where the far clip is.
        var data = (JsonObject)Mark(Drawing(f, plan), elevation)!["data"]!;
        Assert.Equal(3, (double)data["far"]! - (double)data["seeFrom"]!, 6);
        // In the background beyond 1 m: the wall beside goes grey, the facade stays.
        f.Run("view.setDepth", new JsonObject { ["id"] = elevation, ["depth"] = null });
        Assert.Equal(0, Segments(Drawing(f, elevation), "BEYOND"));
        f.Run("view.setStyle", new JsonObject { ["id"] = elevation, ["beyond"] = "near", ["nearDepth"] = 1 });
        var near = Drawing(f, elevation);
        Assert.True(Segments(near, "BEYOND") > 0);
        Assert.True(Segments(near, "SEEN") > 0);
        // Cut only on an elevation that cuts nothing: it draws everything.
        f.Run("view.setStyle", new JsonObject { ["id"] = elevation, ["beyond"] = "cut" });
        Assert.NotNull(Mark(Drawing(f, elevation), shed));
    }
}
