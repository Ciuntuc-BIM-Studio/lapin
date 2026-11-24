using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// App report #186: a dimension drawn on a drafted line's ends stayed where it was when the line's points moved. Its ends now
/// hold on the line's (or polyline's) points on the same view ("vertex"): they follow a grip moved, a move or a turn, one undo
/// puts it back, typing the value moves the line's end, and a point that is gone leaves an orphan.
/// </summary>
public class DimensionDraftVertexTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f) => "plan:" + f.Storey;
    static JsonArray J(double x, double y) => new(x, y);
    static void Near(Vec2 want, Vec2 got, double tol = 1e-6) => Assert.True((want - got).Length < tol, $"want {want}, got {got}");

    static string Line(Fixture f, Vec2 a, Vec2 b) =>
        (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = Plan(f), ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(a.X, a.Y), ["b"] = J(b.X, b.Y) } })!["ids"]![0]!;

    static string Dim(Fixture f, Vec2 a, Vec2 b) =>
        (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = Plan(f), ["pts"] = new JsonArray(J(a.X, a.Y), J(b.X, b.Y)), ["at"] = J((a.X + b.X) / 2, a.Y - 1), ["dir"] = J(1, 0) })!["id"]!;

    static JsonObject Info(Fixture f, string dim) => (JsonObject)f.Session.Query("drafting.dimension", new JsonObject { ["id"] = dim })!;
    static Vec2 Pt(Fixture f, string dim, int i) => Args.PointOf(Info(f, dim)["points"]![i]);
    static double Value(Fixture f, string dim) => (double)Info(f, dim)["value"]!;

    [Fact]
    public void A_dimension_on_a_lines_ends_follows_its_grips_a_move_and_a_turn_and_undo()
    {
        var f = F();
        var line = Line(f, new Vec2(0, 0), new Vec2(3, 0));
        var dim = Dim(f, new Vec2(0, 0), new Vec2(3, 0));
        var info = Info(f, dim);
        Assert.Equal("vertex", (string?)info["holds"]![0]);
        Assert.Equal("vertex", (string?)info["holds"]![1]);
        Assert.Equal(3, Value(f, dim), 6);

        // A grip moved: the line's end, made longer.
        f.Run("drafting.updateDraft", new JsonObject { ["ids"] = new JsonArray(line), ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(0, 0), ["b"] = J(4.5, 0) } });
        Near(new Vec2(4.5, 0), Pt(f, dim, 1));
        Assert.Equal(4.5, Value(f, dim), 6);

        // The line moved as a whole: both ends go with it.
        f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(line), ["op"] = "move", ["by"] = J(1, 2) });
        Near(new Vec2(1, 2), Pt(f, dim, 0));
        Near(new Vec2(5.5, 2), Pt(f, dim, 1));

        f.Session.Undo();
        Near(new Vec2(4.5, 0), Pt(f, dim, 1));
        f.Session.Undo();
        Near(new Vec2(3, 0), Pt(f, dim, 1));
    }

    [Fact]
    public void Typing_the_value_moves_the_lines_end_and_a_point_gone_leaves_an_orphan()
    {
        var f = F();
        var line = Line(f, new Vec2(0, 0), new Vec2(3, 0));
        var dim = Dim(f, new Vec2(0, 0), new Vec2(3, 0));
        Assert.True((bool)Info(f, dim)["driving"]!);
        f.Run("drafting.setDimensionValue", new JsonObject { ["id"] = dim, ["value"] = 5 });
        var shape = f.Model.Get(line)!.Attrs["shape"]!;
        Near(new Vec2(0, 0), Args.PointOf(shape["a"]));
        Near(new Vec2(5, 0), Args.PointOf(shape["b"]));
        Assert.Equal(5, Value(f, dim), 6);

        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(line) });
        Assert.True((bool)Info(f, dim)["orphan"]!);
    }

    [Fact]
    public void A_polylines_corner_is_held_by_its_index_and_a_point_off_the_line_stays_free()
    {
        var f = F();
        var poly = (string)f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = Plan(f),
            ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(2, 0), J(2, 2)), ["closed"] = false },
        })!["ids"]![0]!;
        var dim = Dim(f, new Vec2(2, 0), new Vec2(7, 0));
        var info = Info(f, dim);
        Assert.Equal("vertex", (string?)info["holds"]![0]);
        Assert.Null(info["holds"]![1]);
        f.Run("drafting.updateDraft", new JsonObject { ["ids"] = new JsonArray(poly), ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(3, 0), J(3, 2)), ["closed"] = false } });
        Near(new Vec2(3, 0), Pt(f, dim, 0));
        Near(new Vec2(7, 0), Pt(f, dim, 1));
        // A point held by an index the shape no longer has: an orphan, drawn where it was.
        var top = Dim(f, new Vec2(3, 2), new Vec2(8, 2));
        Assert.Equal("vertex", (string?)Info(f, top)["holds"]![0]);
        f.Run("drafting.updateDraft", new JsonObject { ["ids"] = new JsonArray(poly), ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(0, 5), ["b"] = J(1, 5) } });
        Assert.True((bool)Info(f, top)["orphan"]!);
        Near(new Vec2(3, 2), Pt(f, top, 0));
    }
}
