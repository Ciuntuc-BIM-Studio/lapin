using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Align (forum topics 109, 170, 171; Revit's Align): an element moved square to a reference line until the line picked on it
/// lies on the reference, by element.transform's move; one undo step; refused when the lines are not parallel; with the lock
/// on a grid axis or a reference plane, linked to it and following it.
/// </summary>
public class AlignTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => new(x, y);
    static JsonArray L(double x1, double y1, double x2, double y2) => new(J(x1, y1), J(x2, y2));
    static Lapin.Geometry.Vec2 P(Fixture f, string id, string key) => Args.PointOf(f.Model.Get(id)!.Attrs[key]);

    static JsonNode? Align(Fixture f, string id, JsonArray reference, JsonArray line, string? referenceId = null, bool locked = false)
    {
        var a = new JsonObject { ["ids"] = new JsonArray(id), ["reference"] = reference, ["line"] = line };
        if (referenceId != null) a["referenceId"] = referenceId;
        if (locked) a["lock"] = true;
        return f.Run("element.align", a);
    }

    [Fact]
    public void A_walls_face_aligned_to_another_walls_face_moves_the_whole_wall_in_one_undo_step()
    {
        var f = F();
        f.Wall(0, 0, 0, 6);          // the reference wall, along y at x = 0
        var w = f.Wall(3, 1, 3, 4);  // a parallel wall to bring over
        // Its face at x = 3.1 (wherever its thickness puts it: the picked line), onto the reference wall's face at x = 0.15.
        Align(f, w, L(0.15, 0, 0.15, 6), L(3.1, 1, 3.1, 4));
        Assert.Equal(3 - 2.95, P(f, w, "start").X, 9);
        Assert.Equal(3 - 2.95, P(f, w, "end").X, 9);
        // Only across the reference: along it, nothing moves.
        Assert.Equal(1, P(f, w, "start").Y, 9);
        Assert.Equal(4, P(f, w, "end").Y, 9);
        f.Session.Undo();
        Assert.Equal(3, P(f, w, "start").X, 9);
    }

    [Fact]
    public void A_wall_aligned_onto_a_grid_axis_by_its_axis_and_locked_follows_the_axis()
    {
        var f = F();
        var grid = (string)f.Run("drafting.addAxis", new JsonObject { ["tag"] = "A", ["x1"] = 2, ["y1"] = -1, ["x2"] = 2, ["y2"] = 10 })!["gridId"]!;
        var w = f.Wall(5, 0, 5, 4);
        var r = Align(f, w, L(2, -1, 2, 10), L(5, 0, 5, 4), $"{grid}#A", locked: true)!;
        Assert.Equal(2, P(f, w, "start").X, 9);
        Assert.Equal("axis", (string?)r["locked"]!["kind"]);
        Assert.True(Pins.Is(f.Model.Get(w)));
        // The axis moves: the wall goes with it.
        f.Run("drafting.updateAxis", new JsonObject { ["gridId"] = grid, ["tag"] = "A", ["x1"] = 3, ["y1"] = -1, ["x2"] = 3, ["y2"] = 10 });
        Assert.Equal(3, P(f, w, "start").X, 9);
        Assert.Equal(3, P(f, w, "end").X, 9);
    }

    [Fact]
    public void A_column_aligned_by_its_face_to_a_slanted_drafted_line()
    {
        var f = F();
        var col = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 })!["id"]!;
        // The reference: the line y = x + 4 (45°); the column's face picked along the same direction through (0.2, 0).
        Align(f, col, L(0, 4, 1, 5), L(0.2, 0, 1.2, 1));
        // The face moves onto the line square to it: by n·(ref − face) with n = (−1, 1)/√2, i.e. (−2.1, 2.1).
        var at = P(f, col, "at");
        Assert.Equal(-2.1, at.X, 9);
        Assert.Equal(2.1, at.Y, 9);
    }

    [Fact]
    public void A_drafted_line_aligned_to_a_wall_axis()
    {
        var f = F();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(0, 2), ["b"] = J(3, 2) } })!["ids"]![0]!;
        var w = f.Wall(-1, 5, 6, 5);
        Align(f, line, L(-1, 5, 6, 5), L(0, 2, 3, 2), w);
        var s = f.Model.Get(line)!.Attrs["shape"]!;
        Assert.Equal(5, (double)s["a"]![1]!, 9);
        Assert.Equal(5, (double)s["b"]![1]!, 9);
        Assert.Equal(0, (double)s["a"]![0]!, 9);
        // The wall stays.
        Assert.Equal(5, P(f, w, "start").Y, 9);
    }

    [Fact]
    public void Lines_not_parallel_are_refused_and_nothing_moves()
    {
        var f = F();
        var w = f.Wall(3, 0, 3, 4);
        var ex = Assert.Throws<CommandException>(() => Align(f, w, L(0, 0, 10, 0), L(3, 0, 3, 4)));
        Assert.Contains("not parallel", ex.Message);
        Assert.Equal(3, P(f, w, "start").X, 9);
        // Half a degree off is still refused; a hair (1e-5 rad) is taken.
        Assert.Throws<CommandException>(() => Align(f, w, L(0, 0, Math.Sin(0.5 * Math.PI / 180), 1), L(3, 0, 3, 4)));
        Align(f, w, L(0, 0, 1e-5, 1), L(3, 0, 3, 4));
        Assert.Equal(0, P(f, w, "start").X, 4);
    }

    [Fact]
    public void The_reference_itself_a_pinned_element_and_a_lock_without_an_axis_or_plane_are_refused()
    {
        var f = F();
        var a = f.Wall(0, 0, 0, 4);
        var b = f.Wall(3, 0, 3, 4);
        Assert.Throws<CommandException>(() => Align(f, a, L(0, 0, 0, 4), L(0.1, 0, 0.1, 4), a));
        var ex = Assert.Throws<CommandException>(() => Align(f, b, L(0, 0, 0, 4), L(3, 0, 3, 4), a, locked: true));
        Assert.Contains("grid axis or a reference plane", ex.Message);
        f.Run("element.pin", new JsonObject { ["ids"] = new JsonArray(b), ["pinned"] = true });
        Assert.Throws<CommandException>(() => Align(f, b, L(0, 0, 0, 4), L(3, 0, 3, 4)));
        Assert.Equal(3, P(f, b, "start").X, 9);
    }
}
