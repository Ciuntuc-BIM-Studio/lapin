using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A window type's outline of its own, drawn on the shared sketch canvas (audit S11 stage 2, docs/plan-sketch-canvas.md §4): the
/// board in the type's elevation with its outline as one closed polyline; finished, the type takes the drawn outline (its size the
/// outline's, its loop in the unit box), a frame ring following it and one fixed light, every window of it rebuilt, in one undo
/// step; a rectangle drawn back gives the rectangle again; a door, an open or a double outline are refused.
/// </summary>
public sealed class JoineryOutlineBoardTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(params JsonNode?[] v) => new(v);

    static (Fixture F, string Type, string Window) Setup()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);
        var spec = JoinerySpec.DefaultWindow() with { Width = 1.2, Height = 1.4 };
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["typeId"] = type })!["id"]!;
        return (f, type, window);
    }

    static string Open(Fixture f, string type) => (string)f.Run("architecture.editJoineryOutline", new JsonObject { ["typeId"] = type })!["viewId"]!;
    static IEnumerable<Entity> On(Fixture f, string view) => f.Model.OfClass(IfcClass.Annotation).Where(e => e.Str("viewId") == view);
    static JoinerySpec Spec(Fixture f, string type) => JoinerySpec.From(f.Model.Get(type)!.Attrs["spec"])!;

    [Fact]
    public void An_outline_drawn_on_the_board_becomes_the_type_s_frame_and_light_in_one_step()
    {
        var (f, type, window) = Setup();
        var view = Open(f, type);
        Assert.Equal("Edit outline", f.Session.History.UndoLabel);
        var v = f.Model.Get(view)!;
        Assert.True(PaperViews.IsBoard(f.Model, v));
        Assert.Equal(3, v.Attrs[PaperViews.UnderlayKey]!.AsArray().Count);
        Assert.Equal(view, Open(f, type));
        // The outline as it is: one closed polyline, its rectangle.
        var outline = Assert.Single(On(f, view));
        Assert.Equal(4, outline.Attrs["shape"]!["pts"]!.AsArray().Count);
        // A pentagon drawn instead (a gable 1.6 m wide, 2 m high), a construction line beside it.
        f.Run("element.delete", new JsonObject { ["ids"] = J(outline.Id) });
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = new JsonObject { ["type"] = "polyline", ["closed"] = true, ["pts"] = J(J(-0.8, 0), J(0.8, 0), J(0.8, 1.4), J(0, 2), J(-0.8, 1.4)) } });
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["role"] = "draft", ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(-1, 1), ["b"] = J(1, 1) } });
        f.Run("architecture.finishJoineryOutline", new JsonObject { ["viewId"] = view });
        Assert.Equal("Finish outline", f.Session.History.UndoLabel);
        Assert.Null(f.Model.Get(view));
        Assert.Empty(On(f, view));
        var s = Spec(f, type);
        Assert.Equal(1.6, s.Width, 6);
        Assert.Equal(2, s.Height, 6);
        Assert.Equal("outline", JoineryShapes.KindOf(s));
        Assert.True(JoineryShapes.Ringed(s));
        Assert.Contains(s.Shape!.Profile!, p => Math.Abs(p.X) < 1e-9 && Math.Abs(p.Y - 1) < 1e-9);
        // A frame ring with the outline as its hole, and the glass: the window built, its outline the drawn one.
        var boxes = Joinery.Boxes(s);
        Assert.Contains(boxes, b => b.Role == "frame" && b.Holes is { Count: 1 });
        Assert.Contains(boxes, b => b.Role == "glass");
        var b = f.Bounds(window);
        Assert.Equal(2, b.Max.Z - b.Min.Z, 2);
        // The stored spec reads back with its outline.
        Assert.Equal(5, JoinerySpec.From(Spec(f, type).ToJson())!.Shape!.Profile!.Count);

        // One undo: the type as it was, the board back with the drawing.
        f.Session.Undo();
        Assert.Equal(1.2, Spec(f, type).Width, 6);
        Assert.NotNull(f.Model.Get(view));

        // A rectangle drawn back: the type's rectangle again.
        f.Run("architecture.cancelJoineryOutline", new JsonObject { ["viewId"] = view });
        f.Run("architecture.finishJoineryOutline", new JsonObject { ["viewId"] = Open(f, type) });
        Assert.Equal("rect", JoineryShapes.KindOf(Spec(f, type)));
    }

    [Fact]
    public void Lines_and_an_arc_meeting_end_to_end_make_one_outline_and_what_is_not_one_is_refused()
    {
        var (f, type, _) = Setup();
        var view = Open(f, type);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray([.. On(f, view).Select(e => (JsonNode)e.Id)]) });
        // An arched head: three lines and a half circle on top, drawn in any order and direction.
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shapes"] = J(
            new JsonObject { ["type"] = "line", ["a"] = J(0.5, 1), ["b"] = J(0.5, 0) },
            new JsonObject { ["type"] = "arc", ["c"] = J(0, 1), ["r"] = 0.5, ["start"] = 0, ["end"] = Math.PI },
            new JsonObject { ["type"] = "line", ["a"] = J(-0.5, 0), ["b"] = J(0.5, 0) },
            new JsonObject { ["type"] = "line", ["a"] = J(-0.5, 0), ["b"] = J(-0.5, 1) }) });
        var loop = JoineryOutlineBoard.LoopOf(f.Model, view);
        Assert.True(Polygon2.SignedArea(loop) > 0);
        Assert.Equal(1 + Math.PI * 0.25 / 2, Polygon2.SignedArea(loop), 2);
        // An open outline is refused, the board left open.
        var arc = On(f, view).Single(e => (string?)e.Attrs["shape"]!["type"] == "arc").Id;
        f.Run("element.delete", new JsonObject { ["ids"] = J(arc) });
        Assert.Throws<CommandException>(() => f.Run("architecture.finishJoineryOutline", new JsonObject { ["viewId"] = view }));
        Assert.NotNull(f.Model.Get(view));
        // Two closed shapes are refused.
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shapes"] = J(
            new JsonObject { ["type"] = "circle", ["c"] = J(0, 1), ["r"] = 0.4 }, new JsonObject { ["type"] = "circle", ["c"] = J(0, 2), ["r"] = 0.2 }) });
        Assert.Throws<CommandException>(() => JoineryOutlineBoard.LoopOf(f.Model, view));
        // A door has its own heads.
        var door = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = JoinerySpec.DefaultDoor().ToJson() })!["id"]!;
        Assert.Throws<CommandException>(() => Open(f, door));
    }
}
