using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Extend between kinds (forum topic 235, Archicad's and Revit's way): a drafted line brought to a wall (its axis or a face), a
/// wall to a drafted line or a grid axis, and each to the other's line carried on when they do not meet as they lie. One undo
/// step each.
/// </summary>
public sealed class ExtendAcrossKindsTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => new(x, y);
    static JsonArray L(double x1, double y1, double x2, double y2) => new(J(x1, y1), J(x2, y2));

    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;

    static string Draw(Fixture f, string view, JsonObject shape) =>
        (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;

    static string Line(Fixture f, string view, double x1, double y1, double x2, double y2) =>
        Draw(f, view, new JsonObject { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) });

    static (Vec2 A, Vec2 B) Ends(Fixture f, string id)
    {
        var s = f.Model.Get(id)!.Attrs["shape"]!;
        return (Args.PointOf(s["a"]), Args.PointOf(s["b"]));
    }

    static JsonNode? TrimExtend(Fixture f, string id, double atX, double atY, JsonObject boundary)
    {
        var a = new JsonObject { ["id"] = id, ["at"] = J(atX, atY) };
        foreach (var (k, v) in boundary) a[k] = v?.DeepClone();
        return f.Run("drafting.trimExtend", a);
    }

    [Fact]
    public void A_drafted_line_is_carried_on_to_a_walls_axis_line_it_would_not_reach_as_the_wall_lies_in_one_undo_step()
    {
        var f = F();
        var plan = Plan(f);
        var w = f.Wall(5, 3, 5, 6);              // beside the line's way, not across it
        var line = Line(f, plan, 0, 0, 2, 0);
        TrimExtend(f, line, 1, 0, new JsonObject { ["boundary"] = w, ["face"] = "axis" });
        var (a, b) = Ends(f, line);
        Assert.Equal(0, a.X, 9);
        Assert.Equal(5, b.X, 9);
        Assert.Equal(0, b.Y, 9);
        f.Session.Undo();
        Assert.Equal(2, Ends(f, line).B.X, 9);
    }

    [Fact]
    public void A_drafted_line_goes_to_the_walls_near_or_far_face_as_asked()
    {
        var f = F();
        var plan = Plan(f);
        var w = f.Wall(5, -1, 5, 1);
        var near = Line(f, plan, 0, 0, 2, 0);
        var far = Line(f, plan, 0, 0.5, 2, 0.5);
        TrimExtend(f, near, 1, 0, new JsonObject { ["boundary"] = w, ["face"] = "near" });
        TrimExtend(f, far, 1, 0.5, new JsonObject { ["boundary"] = w, ["face"] = "far" });
        var (xn, xf) = (Ends(f, near).B.X, Ends(f, far).B.X);
        Assert.True(xn < 5 && xf > 5);
        // The faces lie either side of the axis, the wall's thickness apart.
        var frame = f.Session.Catalog.Get<IPathElements>()!.FrameOf(f.Model.Get(w)!, f.Model)!;
        Assert.Equal(frame.YMax - frame.YMin, xf - xn, 9);
    }

    [Fact]
    public void A_drafted_line_crossing_a_wall_is_trimmed_to_it_on_the_part_picked()
    {
        var f = F();
        var plan = Plan(f);
        var w = f.Wall(5, -1, 5, 1);
        var line = Line(f, plan, 0, 0, 8, 0);
        TrimExtend(f, line, 7, 0, new JsonObject { ["boundary"] = w, ["face"] = "axis" });
        var (a, b) = Ends(f, line);
        Assert.Equal(5, a.X, 9);
        Assert.Equal(8, b.X, 9);
    }

    [Fact]
    public void A_polylines_end_segment_goes_to_a_grid_axis_given_as_a_line_and_its_other_segments_stay()
    {
        var f = F();
        var plan = Plan(f);
        var pl = Draw(f, plan, new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 3), J(0, 0), J(2, 0)), ["closed"] = false });
        TrimExtend(f, pl, 1.8, 0, new JsonObject { ["line"] = L(6, -5, 6, -4) });
        var pts = ((JsonArray)f.Model.Get(pl)!.Attrs["shape"]!["pts"]!).Select(Args.PointOf).ToList();
        Assert.Equal(3, pts.Count);
        Assert.Equal(0, pts[0].X, 9);
        Assert.Equal(3, pts[0].Y, 9);
        Assert.Equal(6, pts[2].X, 9);
        Assert.Equal(0, pts[2].Y, 9);
    }

    [Fact]
    public void A_line_parallel_to_the_boundary_or_an_arc_is_refused_and_nothing_changes()
    {
        var f = F();
        var plan = Plan(f);
        var w = f.Wall(0, 2, 5, 2);
        var line = Line(f, plan, 0, 0, 2, 0);
        var ex = Assert.Throws<CommandException>(() => TrimExtend(f, line, 1, 0, new JsonObject { ["boundary"] = w }));
        Assert.Contains("never meets", ex.Message);
        Assert.Equal(2, Ends(f, line).B.X, 9);
        var arc = Draw(f, plan, new JsonObject { ["type"] = "arc", ["c"] = J(0, 0), ["r"] = 1, ["start"] = 0, ["end"] = 90 });
        Assert.Throws<CommandException>(() => TrimExtend(f, arc, 1, 0, new JsonObject { ["line"] = L(6, -5, 6, 5) }));
    }

    [Fact]
    public void A_wall_is_carried_on_to_a_drafted_lines_and_a_grid_axis_line_it_does_not_meet_as_they_lie()
    {
        var f = F();
        var w = f.Wall(0, 0, 2, 0);
        // A drafted line (given by its points) at x = 5, beside the wall's way.
        f.Run("element.trimExtend", new JsonObject { ["id"] = w, ["at"] = J(1, 0), ["line"] = L(5, 3, 5, 6) });
        Assert.Equal(5, Walls.End(f.Model.Get(w)!).X, 9);
        // A grid axis at y = 4, running past the wall's start: the start goes to it.
        var v = f.Wall(-3, 1, -3, 2);
        f.Run("element.trimExtend", new JsonObject { ["id"] = v, ["at"] = J(-3, 1.5), ["line"] = L(10, 4, 11, 4) });
        Assert.Equal(4, Walls.End(f.Model.Get(v)!).Y, 9);
        Assert.Equal(1, Walls.Start(f.Model.Get(v)!).Y, 9);
    }

    [Fact]
    public void Extend_wall_with_no_wall_ahead_goes_to_the_nearest_walls_line_carried_on()
    {
        var f = F();
        var w = f.Wall(0, 0, 2, 0);
        f.Wall(5, 3, 5, 6);   // its line x = 5 lies ahead; the wall itself does not
        f.Wall(9, 3, 9, 6);   // farther
        f.Run("architecture.extendWall", new JsonObject { ["id"] = w, ["at"] = J(1.8, 0) });
        Assert.Equal(5, Walls.End(f.Model.Get(w)!).X, 9);
        Assert.Equal(0, Walls.Start(f.Model.Get(w)!).X, 9);
        // A wall physically ahead still wins over a nearer line carried on.
        var g = F();
        var w2 = g.Wall(0, 0, 2, 0);
        g.Wall(4, 3, 4, 6);
        g.Wall(7, -1, 7, 1);
        g.Run("architecture.extendWall", new JsonObject { ["id"] = w2, ["at"] = J(1.8, 0) });
        Assert.Equal(7, Walls.End(g.Model.Get(w2)!).X, 9);
    }
}
