using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// A part's own sketch edited on a placed part whose values are not the defaults (decision 520): the editor edits the definition, so
/// the part tree gives the sketch as stored (<c>stored</c>, solved with the defaults) beside the instance's, and the edit sent from it
/// goes through; a dimension's place kept on a part sketch; constraints drawing recorded added with the shapes in one step; a body's
/// condition cleared by <c>when: null</c>.
/// </summary>
public sealed class PartSketchStoredTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(params JsonNode?[] v) => new(v);

    static string Part(Fixture f, JsonObject def) => (string)f.Run("studio.createPart", new JsonObject { ["name"] = "Slab", ["def"] = def })!["id"]!;
    static string Place(Fixture f, string part, JsonObject? values = null) =>
        (string)f.Run("studio.placePart", new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(0, 0), ["values"] = values })!["id"]!;
    static JsonObject Sketch(Fixture f, string part) => PartSketches.Find(f.Model.Get(part)!.Attrs["def"]!.AsObject(), "base")!;
    static JsonObject TreeSketch(Fixture f, string instance) =>
        f.Session.Query("studio.partTree", new JsonObject { ["id"] = instance })!["sketches"]!.AsArray().Single(s => (string?)s!["id"] == "base")!.AsObject();
    static JsonArray Pt(JsonObject holder, string shape, int vertex) =>
        holder["shapes"]!.AsArray().Single(s => (string)s!["id"]! == shape)!["pts"]![vertex]!.AsArray();

    static JsonObject Dim(string kind, string part, JsonNode v)
    {
        var o = new JsonObject { ["kind"] = kind, ["refs"] = J(new JsonObject { ["shape"] = "sq", ["part"] = part }) };
        if (v is JsonValue jv && jv.TryGetValue<string>(out var s)) o["formula"] = s; else o["value"] = v.DeepClone();
        return o;
    }

    // A W × 1 square, W = 1 by default, pushed by D.
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
                Dim("distanceX", "vertex:2", "W"),
                Dim("distanceX", "vertex:3", 0), Dim("distanceY", "vertex:3", 1)),
        }),
        ["bodies"] = J(new JsonObject { ["name"] = "Slab", ["sketch"] = "base", ["depth"] = "D" }),
    };

    static JsonObject Line(double ax, double ay, double bx, double by) => new() { ["type"] = "line", ["a"] = J(ax, ay), ["b"] = J(bx, by) };

    [Fact]
    public void A_sketch_on_a_placed_part_with_its_own_values_is_edited_as_stored()
    {
        var f = F();
        var part = Part(f, Square());
        var wide = Place(f, part, new JsonObject { ["W"] = 1.6 });
        var sk = TreeSketch(f, wide);
        // The instance's shapes (its body) and the stored ones (the definition, the defaults), each with its constraints' values.
        Assert.Equal(1.6, (double)Pt(sk, "sq", 1)[0]!, 9);
        var stored = sk["stored"]!.AsObject();
        Assert.Equal(1, (double)Pt(stored, "sq", 1)[0]!, 9);
        Assert.Equal(1, stored["constraints"]!.AsArray().Select(c => c!.AsObject()).First(c => (string?)c["formula"] == "W")["value"]!.GetValue<double>(), 9);
        Assert.Equal("underconstrained", (string)stored["status"]!);
        Assert.Equal((int)sk["dof"]!, (int)stored["dof"]!);
        Assert.Equal((double)sk["frame"]!["origin"]![0]!, (double)stored["frame"]!["origin"]![0]!, 9);
        Assert.True((bool)stored["shapes"]![0]!["inProfile"]!);

        // The instance's shapes sent back look moved against the stored ones: refused (what the editor did before).
        var instanceShapes = sk["shapes"]!.AsArray().Select(s => (JsonNode)Strip(s!.AsObject())).ToArray();
        Assert.Throws<CommandException>(() => f.Run("studio.partSketchSetShapes", new JsonObject { ["partId"] = wide, ["sketchId"] = "base", ["shapes"] = new JsonArray(instanceShapes) }));

        // The stored ones and a new line: kept, the placed part still W = 1.6.
        var shapes = new JsonArray([.. stored["shapes"]!.AsArray().Select(s => (JsonNode)Strip(s!.AsObject())), Line(0, 0, 0.5, 0.5)]);
        var r = f.Run("studio.partSketchSetShapes", new JsonObject { ["partId"] = wide, ["sketchId"] = "base", ["shapes"] = shapes })!.AsObject();
        Assert.Equal(2, r["ids"]!.AsArray().Count);
        Assert.Equal(2, Sketch(f, part)["shapes"]!.AsArray().Count);
        Assert.Equal(1.6, (double)Pt(TreeSketch(f, wide), "sq", 1)[0]!, 9);
        f.Session.Undo();
        Assert.Single(Sketch(f, part)["shapes"]!.AsArray());

        // A placed part with the defaults: stored is the same as its shapes.
        var plain = TreeSketch(f, Place(f, part));
        Assert.Equal(plain["shapes"]!.ToJsonString(), plain["stored"]!["shapes"]!.ToJsonString());

        // The drag preview with the defaults: what partSketchDrag keeps; with the instance's: today's.
        JsonObject Drag(string? values) => new()
        {
            ["partId"] = wide, ["sketchId"] = "base", ["shape"] = "sq", ["part"] = "vertex:2", ["to"] = J(values == "defaults" ? 1 : 1.6, 1.5),
            ["values"] = values,
        };
        var mine = f.Session.Query("studio.partSketchDragPreview", Drag(null))!.AsObject();
        Assert.True((bool)mine["ok"]!);
        Assert.Equal(1.6, (double)Pt(mine, "sq", 2)[0]!, 6);
        var defaults = f.Session.Query("studio.partSketchDragPreview", Drag("defaults"))!.AsObject();
        Assert.True((bool)defaults["ok"]!);
        Assert.Equal(1, (double)Pt(defaults, "sq", 2)[0]!, 6);
        var kept = f.Run("studio.partSketchDrag", Drag("defaults"))!.AsObject();
        Assert.Equal(defaults["shapes"]!.ToJsonString(), kept["shapes"]!.ToJsonString());
        Assert.Throws<CommandException>(() => f.Session.Query("studio.partSketchDragPreview", Drag("other")));
    }

    static JsonObject Strip(JsonObject s)
    {
        var o = (JsonObject)s.DeepClone();
        o.Remove("inProfile");
        o.Remove("inLoop");
        return o;
    }

    [Fact]
    public void A_part_sketch_dimension_keeps_its_place()
    {
        var f = F();
        var part = Part(f, Square());
        var a = Place(f, part);
        var id = (string)f.Run("studio.partSketchAddConstraint", new JsonObject
        {
            ["partId"] = a, ["sketchId"] = "base", ["kind"] = "distance", ["refs"] = J(new JsonObject { ["shape"] = "sq", ["part"] = "edge:0" }),
            ["driving"] = false, ["place"] = new JsonObject { ["offset"] = -0.25 },
        })!["id"]!;
        JsonObject? Placed() => Sketch(f, part)["constraints"]!.AsArray().Single(c => (string?)c!["id"] == id)!["place"] as JsonObject;
        Assert.Equal(-0.25, (double)Placed()!["offset"]!, 9);

        f.Run("studio.partSketchUpdateConstraint", new JsonObject { ["partId"] = a, ["sketchId"] = "base", ["id"] = id, ["place"] = new JsonObject { ["offset"] = 0.4 } });
        Assert.Equal(0.4, (double)Placed()!["offset"]!, 9);
        // The tree's constraints carry it, for the overlay to draw it there.
        var resolved = TreeSketch(f, a)["constraints"]!.AsArray().Single(c => (string?)c!["id"] == id)!;
        Assert.Equal(0.4, (double)resolved["place"]!["offset"]!, 9);
        Assert.Equal(0.4, (double)TreeSketch(f, a)["stored"]!["constraints"]!.AsArray().Single(c => (string?)c!["id"] == id)!["place"]!["offset"]!, 9);
        f.Session.Undo();
        Assert.Equal(-0.25, (double)Placed()!["offset"]!, 9);

        // Null: placed automatically again.
        f.Run("studio.partSketchUpdateConstraint", new JsonObject { ["partId"] = a, ["sketchId"] = "base", ["id"] = id, ["place"] = null });
        Assert.Null(Placed());
        f.Session.Undo();
        Assert.NotNull(Placed());

        // A place that does not read, or on a constraint that is no dimension: refused.
        Assert.Throws<CommandException>(() => f.Run("studio.partSketchUpdateConstraint", new JsonObject { ["partId"] = a, ["sketchId"] = "base", ["id"] = id, ["place"] = new JsonObject() }));
        Assert.Throws<CommandException>(() => f.Run("studio.partSketchAddConstraint", new JsonObject
        {
            ["partId"] = a, ["sketchId"] = "base", ["kind"] = "horizontal", ["refs"] = J(new JsonObject { ["shape"] = "sq", ["part"] = "edge:0" }),
            ["place"] = new JsonObject { ["offset"] = 1 },
        }));
    }

    [Fact]
    public void Constraints_drawing_recorded_are_added_with_the_shapes_in_one_step()
    {
        var f = F();
        var part = Part(f, Square());
        var a = Place(f, part);
        JsonArray Kept() => new([.. Sketch(f, part)["shapes"]!.AsArray().Select(s => s!.DeepClone())]);
        var before = Sketch(f, part)["constraints"]!.AsArray().Count;

        // A line from the square's top right corner, and a rectangle with its sides held.
        var shapes = Kept();
        shapes.Add((JsonNode)Line(1, 1, 2, 2));
        shapes.Add((JsonNode)new JsonObject { ["type"] = "polyline", ["pts"] = J(J(3, 0), J(4, 0), J(4, 1), J(3, 1)), ["closed"] = true });
        JsonObject Ref(int n, string p) => new() { ["new"] = n, ["part"] = p };
        var r = f.Run("studio.partSketchSetShapes", new JsonObject
        {
            ["partId"] = a, ["sketchId"] = "base", ["shapes"] = shapes,
            ["constraints"] = J(
                new JsonObject { ["kind"] = "coincident", ["refs"] = J(Ref(0, "start"), new JsonObject { ["shape"] = "sq", ["part"] = "vertex:2" }) },
                new JsonObject { ["kind"] = "horizontal", ["refs"] = J(Ref(1, "edge:0")) },
                new JsonObject { ["kind"] = "vertical", ["refs"] = J(Ref(1, "edge:1")) },
                new JsonObject { ["kind"] = "coincident", ["refs"] = J(Ref(5, "start"), new JsonObject { ["shape"] = "sq", ["part"] = "vertex:0" }) }),
        })!.AsObject();
        Assert.Equal(3, r["constraints"]!.AsArray().Count);
        var refused = Assert.Single(r["refused"]!.AsArray())!;
        Assert.Equal(3, (int)refused["index"]!);
        var cs = Sketch(f, part)["constraints"]!.AsArray();
        Assert.Equal(before + 3, cs.Count);
        var lineId = (string)r["ids"]![1]!;
        Assert.Contains(cs, c => (string?)c!["kind"] == "coincident" && c["refs"]!.AsArray().Any(x => (string?)x!["shape"] == lineId && (string?)x["part"] == "start"));
        // The corner held by the coincidence: a drag of the line's start takes the square's corner (free upwards) with it.
        var moved = f.Session.Query("studio.partSketchDragPreview", new JsonObject
        {
            ["partId"] = a, ["sketchId"] = "base", ["shape"] = lineId, ["part"] = "start", ["to"] = J(1, 1.3),
        })!.AsObject();
        Assert.True((bool)moved["ok"]!);
        Assert.Equal(1.3, (double)Pt(moved, "sq", 2)[1]!, 6);

        // One undo step takes the shapes and their constraints together.
        f.Session.Undo();
        Assert.Single(Sketch(f, part)["shapes"]!.AsArray());
        Assert.Equal(before, Sketch(f, part)["constraints"]!.AsArray().Count);

        // A recorded constraint the sketch cannot have (the line's end held on two corners apart) is reported; the drawing stays.
        shapes = Kept();
        shapes.Add((JsonNode)Line(1, 0, 1, 1));
        r = f.Run("studio.partSketchSetShapes", new JsonObject
        {
            ["partId"] = a, ["sketchId"] = "base", ["shapes"] = shapes,
            ["constraints"] = J(
                new JsonObject { ["kind"] = "coincident", ["refs"] = J(Ref(0, "start"), new JsonObject { ["shape"] = "sq", ["part"] = "vertex:1" }) },
                new JsonObject { ["kind"] = "coincident", ["refs"] = J(Ref(0, "start"), new JsonObject { ["shape"] = "sq", ["part"] = "vertex:3" }) }),
        })!.AsObject();
        Assert.Single(r["constraints"]!.AsArray());
        Assert.Equal(1, (int)Assert.Single(r["refused"]!.AsArray())!["index"]!);
        Assert.Equal(2, Sketch(f, part)["shapes"]!.AsArray().Count);
    }

    [Fact]
    public void A_body_s_condition_is_cleared_by_when_null()
    {
        var f = F();
        var part = Part(f, Square());
        var a = Place(f, part);
        f.Run("studio.updatePartBody", new JsonObject { ["partId"] = a, ["name"] = "Slab", ["when"] = "W - 0.5" });
        JsonObject Body() => f.Model.Get(part)!.Attrs["def"]!["bodies"]![0]!.AsObject();
        JsonObject Step() => f.Session.Query("studio.partTree", new JsonObject { ["id"] = a })!["steps"]!.AsArray().Single(s => (string?)s!["label"] == "Slab")!.AsObject();
        Assert.Equal("W - 0.5", (string)Body()["when"]!);
        Assert.Equal("W - 0.5", (string)Step()["when"]!);

        f.Run("studio.updatePartBody", new JsonObject { ["partId"] = a, ["name"] = "Slab", ["when"] = null });
        Assert.Null(Body()["when"]);
        Assert.Null(Step()["when"]);
        Assert.Null(Step()["suppressed"]);
        // Leaving it out keeps it as it is.
        f.Session.Undo();
        f.Run("studio.updatePartBody", new JsonObject { ["partId"] = a, ["name"] = "Slab", ["depth"] = 0.3 });
        Assert.Equal("W - 0.5", (string)Body()["when"]!);
    }
}
