using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// A part's own sketch edited on the shared sketch canvas (audit S11 stage 1, docs/plan-sketch-canvas.md): opened on a reference
/// plane of its own where the placed part puts it, its shapes drafted there with their constraints and formulas, edited with the
/// drafting commands, then written back in one undo step keeping the shapes' and constraints' ids; cancelled, the part as it was.
/// </summary>
public sealed class PartSketchBoardTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(params JsonNode?[] v) => new(v);

    static double Volume(Fixture f, string id) => f.Session.Quantities(id).SelectMany(q => q.Quantities).Single(q => q.Name == "NetVolume").Value;
    static JsonObject Def(Fixture f, string part) => f.Model.Get(part)!.Attrs["def"]!.AsObject();
    static JsonObject Sketch(Fixture f, string part) => PartSketches.Find(Def(f, part), "base")!;

    static JsonObject Dim(string kind, string part, JsonNode v)
    {
        var o = new JsonObject { ["kind"] = kind, ["refs"] = J(new JsonObject { ["shape"] = "sq", ["part"] = part }) };
        if (v is JsonValue jv && jv.TryGetValue<string>(out var s)) o["formula"] = s; else o["value"] = v.DeepClone();
        return o;
    }

    // A W × 1 square pushed by D, its corners held by distances (two of them W).
    static JsonObject Square() => new()
    {
        ["params"] = J(new JsonObject { ["key"] = "W", ["default"] = 1 }, new JsonObject { ["key"] = "D", ["default"] = 0.2 }),
        ["sketches"] = J(new JsonObject
        {
            ["id"] = "base", ["name"] = "Base", ["plane"] = "xy",
            ["shapes"] = J(new JsonObject { ["id"] = "sq", ["type"] = "polyline", ["pts"] = J(J(0, 0), J(1, 0), J(1, 1), J(0, 1)), ["closed"] = true }),
            ["constraints"] = J(
                Dim("distanceX", "vertex:0", 0), Dim("distanceY", "vertex:0", 0),
                Dim("distanceX", "vertex:1", "W"), Dim("distanceY", "vertex:1", 0),
                Dim("distanceX", "vertex:2", "W"), Dim("distanceY", "vertex:2", 1),
                Dim("distanceX", "vertex:3", 0), Dim("distanceY", "vertex:3", 1)),
        }),
        ["bodies"] = J(new JsonObject { ["name"] = "Slab", ["sketch"] = "base", ["depth"] = "D" }),
    };

    static (Fixture F, string Part, string Placed) Setup()
    {
        var f = F();
        var part = (string)f.Run("studio.createPart", new JsonObject { ["name"] = "Slab", ["def"] = Square() })!["id"]!;
        var placed = (string)f.Run("studio.placePart", new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(4, 2), ["values"] = new JsonObject { ["W"] = 2 } })!["id"]!;
        f.Run("element.transform", new JsonObject { ["ids"] = J(placed), ["op"] = "rotate", ["angle"] = 90, ["center"] = J(4, 2) });
        return (f, part, placed);
    }

    static string Open(Fixture f, string placed) => (string)f.Run("studio.editPartSketch", new JsonObject { ["partId"] = placed, ["sketchId"] = "base" })!["viewId"]!;

    [Fact]
    public void Opened_on_a_plane_where_the_placed_part_puts_it_with_its_constraints_and_written_back_the_same()
    {
        var (f, part, placed) = Setup();
        var before = Sketch(f, part).DeepClone();
        var volume = Volume(f, placed);
        var view = Open(f, placed);
        Assert.Equal("Edit part sketch", f.Session.History.UndoLabel);
        var v = f.Model.Get(view)!;
        Assert.Equal(RefPlanes.Kind, v.Str("kind"));
        Assert.Equal(placed, (string?)v.Attrs[PartSketches.Key]!["partId"]);
        // Its plane's x and y are the sketch's u and v as this placed part has them (the tree's frame).
        var frame = f.Session.Query("studio.partTree", new JsonObject { ["id"] = placed })!["sketches"]!.AsArray().Single(s => (string?)s!["id"] == "base")!["stored"]!["frame"]!;
        var plane = RefPlanes.Of(v);
        Vec3 P(JsonNode n) => new((double)n[0]!, (double)n[1]!, (double)n[2]!);
        Assert.True((plane.Origin - P(frame["origin"]!)).Length < 1e-6);
        Assert.True((plane.Right - P(frame["right"]!)).Length < 1e-6);
        Assert.True((plane.Up - P(frame["up"]!)).Length < 1e-6);
        // One draft per shape, remembering it; every constraint, the formulas kept and worked out with the defaults.
        var drafts = Drafts.On(f.Model, view).ToList();
        Assert.Single(drafts);
        Assert.Equal("sq", drafts[0].Str(PartSketches.ShapeKey));
        var cs = f.Session.Query("studio.constraints", new JsonObject { ["viewId"] = view })!["constraints"]!.AsArray();
        Assert.Equal(8, cs.Count);
        Assert.Equal(2, cs.Count(c => (string?)c!["formula"] == "W"));
        // Opened again: the same board.
        Assert.Equal(view, Open(f, placed));

        // Finished with nothing changed: the sketch as it was, ids and all; the view gone; the placed part the same.
        var done = f.Run("studio.finishPartSketch", new JsonObject { ["viewId"] = view })!.AsObject();
        Assert.Equal("Finish sketch", f.Session.History.UndoLabel);
        Assert.Equal("base", (string?)done["sketchId"]);
        Assert.Null(f.Model.Get(view));
        Assert.Empty(Drafts.On(f.Model, view));
        Assert.True(JsonNode.DeepEquals(before, Sketch(f, part)), Sketch(f, part).ToJsonString());
        Assert.Equal(volume, Volume(f, placed), 9);
    }

    [Fact]
    public void Edits_on_the_board_reach_the_part_in_one_step_keeping_ids_and_undo_brings_the_board_back()
    {
        var (f, part, placed) = Setup();
        var view = Open(f, placed);
        var draft = Drafts.On(f.Model, view).Single().Id;
        // A dimension's formula changed on the board, and a line drawn: each its own step while editing.
        var height = f.Session.Query("studio.constraints", new JsonObject { ["viewId"] = view })!["constraints"]!.AsArray()
            .First(c => (string?)c!["kind"] == "distanceY" && (string?)c["refs"]![0]!["part"] == "vertex:2")!;
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = (string?)height["id"], ["formula"] = "W / 2" });
        f.Run("studio.updateConstraint", new JsonObject
        {
            ["id"] = f.Session.Query("studio.constraints", new JsonObject { ["viewId"] = view })!["constraints"]!.AsArray()
                .First(c => (string?)c!["kind"] == "distanceY" && (string?)c["refs"]![0]!["part"] == "vertex:3")!["id"]!.GetValue<string>(),
            ["formula"] = "W / 2",
        });
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(0, 0), ["b"] = J(1, 0.5) } });
        // The part is not changed yet.
        Assert.Single(Sketch(f, part)["shapes"]!.AsArray());
        var ids = Sketch(f, part)["constraints"]!.AsArray().Select(c => (string)c!["id"]!).ToList();

        f.Run("studio.finishPartSketch", new JsonObject { ["viewId"] = view });
        var sk = Sketch(f, part);
        var shapes = sk["shapes"]!.AsArray().Select(s => (string)s!["id"]!).ToList();
        Assert.Equal(["sq", "p1"], shapes);
        // The constraints keep their ids; the two changed carry their new formulas.
        Assert.Equal(ids.Order(), sk["constraints"]!.AsArray().Select(c => (string)c!["id"]!).Order());
        Assert.Equal(2, sk["constraints"]!.AsArray().Count(c => (string?)c!["formula"] == "W / 2"));
        // This placed part (W = 2) is now 2 × 1.
        Assert.Equal(2 * 1 * 0.2, Volume(f, placed), 6);
        Assert.Null(f.Model.Get(view));

        // Undo: the part as it was, the board back with its edits.
        f.Session.Undo();
        Assert.Single(Sketch(f, part)["shapes"]!.AsArray());
        Assert.NotNull(f.Model.Get(view));
        Assert.Equal(2, Drafts.On(f.Model, view).Count());
        Assert.NotNull(f.Model.Get(draft));
    }

    [Fact]
    public void Cancelled_the_part_is_as_it_was_and_what_a_part_s_sketch_cannot_take_is_refused_at_finish()
    {
        var (f, part, placed) = Setup();
        var before = Sketch(f, part).DeepClone();
        var view = Open(f, placed);
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(0, 0), ["b"] = J(1, 1) } });
        f.Run("studio.cancelPartSketch", new JsonObject { ["viewId"] = view });
        Assert.Null(f.Model.Get(view));
        Assert.True(JsonNode.DeepEquals(before, Sketch(f, part)));

        // An ellipse: refused, the board left open.
        view = Open(f, placed);
        var ell = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = new JsonObject { ["type"] = "ellipse", ["c"] = J(0.5, 0.5), ["m"] = J(0.3, 0), ["ratio"] = 0.5 } })!["ids"]![0]!;
        var ex = Assert.Throws<CommandException>(() => f.Run("studio.finishPartSketch", new JsonObject { ["viewId"] = view }));
        Assert.Contains("lines, polylines, circles and arcs", ex.Message);
        Assert.NotNull(f.Model.Get(view));
        f.Run("element.delete", new JsonObject { ["ids"] = J(ell) });
        // The body's outline deleted: the part refuses it, the board stays.
        f.Run("element.delete", new JsonObject { ["ids"] = J(Drafts.On(f.Model, view).Single().Id) });
        Assert.Throws<CommandException>(() => f.Run("studio.finishPartSketch", new JsonObject { ["viewId"] = view }));
        Assert.NotNull(f.Model.Get(view));
        Assert.True(JsonNode.DeepEquals(before, Sketch(f, part)));
        // Not a part's board: refused.
        Assert.Throws<CommandException>(() => f.Run("studio.finishPartSketch", new JsonObject { ["viewId"] = f.Storey }));
    }
}
