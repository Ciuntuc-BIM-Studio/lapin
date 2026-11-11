using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Move, Copy, Rotate and Mirror in a section or an elevation (decision 453, a tester's report: "items disappear or it
/// simply doesn't work"). The command's points are the view's (x along it, y up); with the view's frame, the model's
/// elements move along the view in plan and up by their offset from the level, while shapes drafted on the view move in
/// it. A turn of elements there is refused; a mirror about a vertical line mirrors them across the plane through it.
/// </summary>
public sealed class SectionTransformTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());
    static JsonArray Ids(params string[] ids) => new([.. ids.Select(x => (JsonNode)x)]);
    static string Id(JsonNode? n) => (string)n!["id"]!;
    static JsonArray J(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);

    /// <summary>A view's frame as its drawing gives it to the interface.</summary>
    static JsonObject FrameOf(Fixture f, string view) =>
        (JsonObject)((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!)["frame"]!.DeepClone();

    static string Elevation(Fixture f, string direction) => Id(f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = direction }));

    static JsonObject Transform(Fixture f, JsonObject frame, JsonObject args, params string[] ids)
    {
        args["ids"] = Ids(ids);
        args["frame"] = frame.DeepClone();
        return (JsonObject)f.Run("element.transform", args)!;
    }

    static Vec2 P(Entity e, string key) => Args.PointOf(e.Attrs[key]);

    [Fact]
    public void A_wall_moved_in_the_south_elevation_slides_along_it_and_goes_up_by_its_base_offset()
    {
        var f = F();
        var wall = f.Wall(0, 0, 4, 0);
        var south = Elevation(f, "south");
        var frame = FrameOf(f, south);
        // The view's right is the model's +x (looking north): 1 m right and 0.5 m up in the view.
        Transform(f, frame, new JsonObject { ["op"] = "move", ["by"] = J(1, 0.5) }, wall);
        var e = f.Model.Get(wall)!;
        Assert.Equal(1, P(e, "start").X, 6);
        // The bug: the view's 0.5 up went to the plan's y, out of the elevation's sight.
        Assert.Equal(0, P(e, "start").Y, 6);
        Assert.Equal(5, P(e, "end").X, 6);
        Assert.Equal(0.5, e.Num("baseOffset"), 6);
    }

    [Fact]
    public void In_the_east_elevation_a_beam_moves_along_y_and_its_top_offset_follows()
    {
        var f = F();
        var beam = Id(f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 0, ["y2"] = 5 }));
        var east = Elevation(f, "east");
        var frame = FrameOf(f, east);
        var right = Args.TryPoint3(frame["right"])!.Value;
        Transform(f, frame, new JsonObject { ["op"] = "move", ["by"] = J(2, -0.3) }, beam);
        var e = f.Model.Get(beam)!;
        var start = P(e, "start");
        Assert.Equal(right.X * 2, start.X, 6);
        Assert.Equal(right.Y * 2, start.Y, 6);
        Assert.Equal(-0.3, e.Num("topOffset"), 6);
        // The bounds follow the offset: the beam's top went down 0.3 m.
        Assert.False(e.Attrs.ContainsKey("baseOffset"));
    }

    [Fact]
    public void A_drafted_shape_moves_in_the_view_while_the_element_with_it_moves_in_the_model()
    {
        var f = F();
        var wall = f.Wall(0, 0, 4, 0);
        var south = Elevation(f, "south");
        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = south, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = J(0, 1), ["b"] = J(2, 1) }) })!["ids"]![0]!;
        var r = Transform(f, FrameOf(f, south), new JsonObject { ["op"] = "move", ["by"] = J(1, 0.25) }, wall, line);
        Assert.False(r.ContainsKey("kept"));
        var s = (Line)Drafts.ShapeOf(f.Model.Get(line)!)!;
        Assert.Equal(1, s.A.X, 6);
        Assert.Equal(1.25, s.A.Y, 6);
        Assert.Equal(1, P(f.Model.Get(wall)!, "start").X, 6);
        Assert.Equal(0, P(f.Model.Get(wall)!, "start").Y, 6);
        Assert.Equal(0.25, f.Model.Get(wall)!.Num("baseOffset"), 6);
    }

    [Fact]
    public void A_copy_in_a_section_lands_along_it_and_up_and_the_original_stays()
    {
        var f = F();
        var column = Id(f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 1, ["y"] = 1 }));
        var section = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(-1, 1), ["end"] = J(6, 1) }));
        var frame = FrameOf(f, section);
        var right = Args.TryPoint3(frame["right"])!.Value;
        var r = Transform(f, frame, new JsonObject { ["op"] = "copy", ["by"] = J(3, 1) }, column);
        var copy = f.Model.Get((string)r["ids"]![0]!)!;
        Assert.Equal(1 + right.X * 3, P(copy, "at").X, 6);
        Assert.Equal(1 + right.Y * 3, P(copy, "at").Y, 6);
        Assert.Equal(1, copy.Num("baseOffset"), 6);
        Assert.Equal(new Vec2(1, 1), P(f.Model.Get(column)!, "at"));
        Assert.Equal(0, f.Model.Get(column)!.Num("baseOffset"), 6);
    }

    [Fact]
    public void Elements_are_not_turned_in_a_section_but_drafted_shapes_are()
    {
        var f = F();
        var wall = f.Wall(0, 0, 4, 0);
        var south = Elevation(f, "south");
        var frame = FrameOf(f, south);
        var ex = Assert.Throws<CommandException>(() => Transform(f, frame, new JsonObject { ["op"] = "rotate", ["center"] = J(0, 0), ["angle"] = 90 }, wall));
        Assert.Contains("tip them over", ex.Message);
        Assert.Equal(new Vec2(4, 0), P(f.Model.Get(wall)!, "end"));
        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = south, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = J(0, 0), ["b"] = J(2, 0) }) })!["ids"]![0]!;
        Transform(f, frame, new JsonObject { ["op"] = "rotate", ["center"] = J(0, 0), ["angle"] = 90 }, line);
        var s = (Line)Drafts.ShapeOf(f.Model.Get(line)!)!;
        Assert.Equal(0, s.B.X, 6);
        Assert.Equal(2, s.B.Y, 6);
    }

    [Fact]
    public void A_mirror_about_a_vertical_line_mirrors_across_the_plane_square_to_the_view_any_other_is_refused()
    {
        var f = F();
        var column = Id(f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 1, ["y"] = 2 }));
        var south = Elevation(f, "south");
        var frame = FrameOf(f, south);
        var origin = Args.TryPoint3(frame["origin"])!.Value;
        // The vertical line at the view's x = 3 - origin.x stands at the model's x = 3.
        var x = 3 - origin.X;
        var r = Transform(f, frame, new JsonObject { ["op"] = "mirror", ["a"] = J(x, 0), ["b"] = J(x, 2), ["copy"] = true }, column);
        var copy = f.Model.Get((string)r["ids"]![0]!)!;
        Assert.Equal(5, P(copy, "at").X, 6);
        Assert.Equal(2, P(copy, "at").Y, 6);
        var ex = Assert.Throws<CommandException>(() => Transform(f, frame, new JsonObject { ["op"] = "mirror", ["a"] = J(0, 1), ["b"] = J(4, 1) }, column));
        Assert.Contains("vertical line", ex.Message);
    }

    [Fact]
    public void A_pinned_element_is_not_moved_in_a_section_its_copy_is()
    {
        var f = F();
        var wall = f.Wall(0, 0, 4, 0);
        f.Run("element.pin", new JsonObject { ["ids"] = Ids(wall), ["pinned"] = true });
        var frame = FrameOf(f, Elevation(f, "south"));
        var ex = Assert.Throws<CommandException>(() => Transform(f, frame, new JsonObject { ["op"] = "move", ["by"] = J(0, 0.5) }, wall));
        Assert.Contains("pinned", ex.Message);
        Assert.Equal(0, f.Model.Get(wall)!.Num("baseOffset"), 6);
        var r = Transform(f, frame, new JsonObject { ["op"] = "copy", ["by"] = J(0, 3) }, wall);
        Assert.Equal(3, f.Model.Get((string)r["ids"]![0]!)!.Num("baseOffset"), 6);
    }

    [Fact]
    public void The_move_is_one_undo_step()
    {
        var f = F();
        var wall = f.Wall(0, 0, 4, 0);
        Transform(f, FrameOf(f, Elevation(f, "south")), new JsonObject { ["op"] = "move", ["by"] = J(1, 0.5) }, wall);
        f.Session.Undo();
        Assert.Equal(new Vec2(0, 0), P(f.Model.Get(wall)!, "start"));
        Assert.Equal(0, f.Model.Get(wall)!.Num("baseOffset"), 6);
    }
}
