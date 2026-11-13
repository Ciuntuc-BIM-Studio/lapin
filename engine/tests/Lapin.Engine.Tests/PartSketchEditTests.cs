using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// A part's own sketches and bodies edited (decision 520, docs/plan-studio-parametric.md §9): a point dragged, previewed with a
/// placed part's values and kept with the defaults, refused with the constraints that hold it; sketches added, renamed, removed
/// while no body uses them; bodies added, changed and removed; an outline made of lines and arcs meeting end to end. One undo step each.
/// </summary>
public sealed class PartSketchEditTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(params JsonNode?[] v) => new(v);

    static double Volume(Fixture f, string id) => f.Session.Quantities(id).SelectMany(q => q.Quantities).Single(q => q.Name == "NetVolume").Value;
    static string Part(Fixture f, JsonObject def) => (string)f.Run("studio.createPart", new JsonObject { ["name"] = "Slab", ["def"] = def })!["id"]!;
    static string Place(Fixture f, string part, JsonObject? values = null, double x = 0) =>
        (string)f.Run("studio.placePart", new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(x, 0), ["values"] = values })!["id"]!;
    static JsonObject Def(Fixture f, string part) => f.Model.Get(part)!.Attrs["def"]!.AsObject();
    static JsonObject Sketch(Fixture f, string part, string id) => PartSketches.Find(Def(f, part), id)!;
    static JsonObject TreeSketch(Fixture f, string instance, string id) =>
        f.Session.Query("studio.partTree", new JsonObject { ["id"] = instance })!["sketches"]!.AsArray().Single(s => (string?)s!["id"] == id)!.AsObject();
    static JsonArray Pt(JsonObject shapesHolder, string shape, int vertex) =>
        shapesHolder["shapes"]!.AsArray().Single(s => (string)s!["id"]! == shape)!["pts"]![vertex]!.AsArray();

    static JsonObject Dim(string kind, string part, JsonNode v)
    {
        var o = new JsonObject { ["kind"] = kind, ["refs"] = J(new JsonObject { ["shape"] = "sq", ["part"] = part }) };
        if (v is JsonValue jv && jv.TryGetValue<string>(out var s)) o["formula"] = s; else o["value"] = v.DeepClone();
        return o;
    }

    // A W × 1 square whose top right corner is free upwards (no distanceY on it), pushed by D.
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

    static JsonObject DragArgs(string partId, string part, double u, double v) => new()
    {
        ["partId"] = partId, ["sketchId"] = "base", ["shape"] = "sq", ["part"] = part, ["to"] = J(u, v),
    };

    [Fact]
    public void A_dragged_point_is_previewed_with_a_placed_part_s_values_and_kept_with_the_defaults()
    {
        var f = F();
        var part = Part(f, Square());
        var wide = Place(f, part, new JsonObject { ["W"] = 2 });
        Assert.Equal(1 * 1 * 0.2, Volume(f, Place(f, part, x: 4)), 9);

        // The placed part's preview: its own W = 2 holds the corner's x, the drag takes it up; nothing is kept.
        var preview = f.Session.Query("studio.partSketchDragPreview", DragArgs(wide, "vertex:2", 2, 1.5))!.AsObject();
        Assert.True((bool)preview["ok"]!);
        Assert.Equal(2, (double)Pt(preview, "sq", 2)[0]!, 6);
        Assert.Equal(1.5, (double)Pt(preview, "sq", 2)[1]!, 6);
        Assert.Equal(2, (double)Pt(preview, "sq", 1)[0]!, 6);
        Assert.Equal(1, (double)Pt(Sketch(f, part, "base"), "sq", 2)[1]!, 9);

        // A point its constraints hold: no preview, the reason naming the constraints; the command refused the same way.
        var held = f.Session.Query("studio.partSketchDragPreview", DragArgs(part, "vertex:0", 0.3, 0.3))!.AsObject();
        Assert.False((bool)held["ok"]!);
        Assert.Contains("distanceX", (string)held["reason"]!);
        Assert.Empty(held["shapes"]!.AsArray());
        var ex = Assert.Throws<CommandException>(() => f.Run("studio.partSketchDrag", DragArgs(part, "vertex:0", 0.3, 0.3)));
        Assert.Contains("distanceX", ex.Message);
        // Not a point: refused.
        Assert.Throws<CommandException>(() => f.Session.Query("studio.partSketchDragPreview", DragArgs(part, "edge:1", 1, 1)));

        // Kept: solved with the defaults (W = 1), every placed part rebuilding with its own values; the same as the preview of the part.
        var partPreview = f.Session.Query("studio.partSketchDragPreview", DragArgs(part, "vertex:2", 1, 1.5))!.AsObject();
        var kept = f.Run("studio.partSketchDrag", DragArgs(wide, "vertex:2", 1, 1.5))!.AsObject();
        Assert.True((bool)kept["ok"]!);
        Assert.Equal(partPreview["shapes"]!.ToJsonString(), kept["shapes"]!.ToJsonString());
        Assert.Equal(1.5, (double)Pt(Sketch(f, part, "base"), "sq", 2)[1]!, 6);
        Assert.Equal((2 * 1 + 2 * 0.5 / 2) * 0.2, Volume(f, wide), 6);
        // A drag to where the defaults cannot go is refused, nothing changed.
        Assert.Throws<CommandException>(() => f.Run("studio.partSketchDrag", DragArgs(part, "vertex:2", 2, 1.5)));
        // One undo step back.
        f.Session.Undo();
        Assert.Equal(1, (double)Pt(Sketch(f, part, "base"), "sq", 2)[1]!, 9);
        Assert.Equal(2 * 0.2, Volume(f, wide), 9);
    }

    [Fact]
    public void Sketches_are_added_renamed_and_removed_while_no_body_uses_them()
    {
        var f = F();
        var part = Part(f, Square());
        var a = Place(f, part);
        var id = (string)f.Run("studio.addPartSketch", new JsonObject { ["partId"] = a, ["plane"] = "xz", ["offset"] = J(0, 0, "D") })!["id"]!;
        Assert.Equal("s1", id);
        var sk = TreeSketch(f, a, id);
        Assert.True((bool)sk["local"]!);
        Assert.Equal("xz", (string)sk["plane"]!);
        Assert.Equal("Sketch 2", (string)sk["name"]!);
        Assert.Empty(sk["shapes"]!.AsArray());
        Assert.Empty(sk["bodies"]!.AsArray());
        Assert.Equal(0.2, (double)sk["frame"]!["origin"]![2]!, 9);
        Assert.Equal(1, (double)sk["frame"]!["up"]![2]!, 9);
        Assert.Equal(["Slab"], TreeSketch(f, a, "base")["bodies"]!.AsArray().Select(x => (string)x!));
        // Bad planes and offsets are refused.
        Assert.Throws<CommandException>(() => f.Run("studio.addPartSketch", new JsonObject { ["partId"] = part, ["plane"] = "zz" }));
        Assert.Throws<CommandException>(() => f.Run("studio.addPartSketch", new JsonObject { ["partId"] = part, ["offset"] = J(0, 0, "Q") }));

        f.Run("studio.renamePartSketch", new JsonObject { ["partId"] = part, ["sketchId"] = id, ["name"] = "Front" });
        Assert.Equal("Front", (string)Sketch(f, part, id)["name"]!);
        Assert.Throws<CommandException>(() => f.Run("studio.renamePartSketch", new JsonObject { ["partId"] = part, ["sketchId"] = id, ["name"] = " " }));
        f.Session.Undo();
        Assert.Equal("Sketch 2", (string)Sketch(f, part, id)["name"]!);

        // The body's sketch stays while the body does; the message names it.
        var ex = Assert.Throws<CommandException>(() => f.Run("studio.removePartSketch", new JsonObject { ["partId"] = part, ["sketchId"] = "base" }));
        Assert.Contains("„Slab”", ex.Message);
        f.Run("studio.removePartSketch", new JsonObject { ["partId"] = part, ["sketchId"] = id });
        Assert.Null(Sketch(f, part, id));
        f.Session.Undo();
        Assert.NotNull(Sketch(f, part, id));
        f.Session.Undo();
        Assert.Null(Sketch(f, part, id));
        Assert.Equal(0.2, Volume(f, a), 9);
    }

    [Fact]
    public void Bodies_are_added_changed_and_removed()
    {
        var f = F();
        var part = Part(f, Square());
        var a = Place(f, part);
        var b = Place(f, part, new JsonObject { ["W"] = 2 }, 4);
        var sid = (string)f.Run("studio.addPartSketch", new JsonObject { ["partId"] = part, ["plane"] = "xy", ["offset"] = J(0, 0, "D") })!["id"]!;
        f.Run("studio.partSketchSetShapes", new JsonObject
        {
            ["partId"] = part, ["sketchId"] = sid,
            ["shapes"] = J(new JsonObject { ["id"] = "box", ["type"] = "polyline", ["pts"] = J(J(0, 0), J(0.5, 0), J(0.5, 0.5), J(0, 0.5)), ["closed"] = true }),
        });
        // A depth formula and a condition: made only where W - 1 is not nought.
        var name = (string)f.Run("studio.addPartBody", new JsonObject { ["partId"] = part, ["sketchId"] = sid, ["depth"] = "2*D", ["when"] = "W - 1" })!["name"]!;
        Assert.Equal("Body 2", name);
        Assert.Equal(0.2, Volume(f, a), 9);
        Assert.Equal(2 * 0.2 + 0.25 * 0.4, Volume(f, b), 9);
        Assert.Equal([name], TreeSketch(f, a, sid)["bodies"]!.AsArray().Select(x => (string)x!));
        Assert.True((bool)TreeSketch(f, a, sid)["shapes"]![0]!["inProfile"]!);
        // Refused: a name taken, a depth naming no parameter, an outline that is not closed.
        Assert.Throws<CommandException>(() => f.Run("studio.addPartBody", new JsonObject { ["partId"] = part, ["sketchId"] = sid, ["depth"] = 1, ["name"] = "slab" }));
        Assert.Throws<CommandException>(() => f.Run("studio.addPartBody", new JsonObject { ["partId"] = part, ["sketchId"] = sid, ["depth"] = "Q" }));
        Assert.Throws<CommandException>(() => f.Run("studio.addPartBody", new JsonObject { ["partId"] = part, ["sketchId"] = sid, ["depth"] = 1, ["shape"] = "nope" }));

        // Changed: its condition cleared, a plain depth, a new name.
        f.Run("studio.updatePartBody", new JsonObject { ["partId"] = a, ["name"] = name, ["when"] = null, ["depth"] = 0.1, ["newName"] = "Plinth" });
        Assert.Equal(0.2 + 0.025, Volume(f, a), 9);
        var body = Def(f, part)["bodies"]![1]!;
        Assert.Equal("Plinth", (string)body["name"]!);
        Assert.Null(body["when"]);
        Assert.Throws<CommandException>(() => f.Run("studio.updatePartBody", new JsonObject { ["partId"] = part, ["name"] = "Plinth", ["newName"] = "Slab" }));
        f.Session.Undo();
        Assert.Equal(0.2, Volume(f, a), 9);
        Assert.Equal(name, (string)Def(f, part)["bodies"]![1]!["name"]!);

        // Removed; the last body stays.
        f.Run("studio.removePartBody", new JsonObject { ["partId"] = part, ["name"] = name });
        Assert.Equal(2 * 0.2, Volume(f, b), 9);
        Assert.Throws<CommandException>(() => f.Run("studio.removePartBody", new JsonObject { ["partId"] = part, ["name"] = "Slab" }));
        // ... and the sketch no body uses may go.
        f.Run("studio.removePartSketch", new JsonObject { ["partId"] = part, ["sketchId"] = sid });
        f.Session.Undo();
        f.Session.Undo();
        Assert.Equal(2 * 0.2 + 0.25 * 0.4, Volume(f, b), 9);
        f.Session.Undo();
        Assert.Single(Def(f, part)["bodies"]!.AsArray());
    }

    [Fact]
    public void A_slot_of_two_lines_and_two_arcs_is_pushed_into_a_body()
    {
        const double r = 0.1, L = 0.4, depth = 0.05;
        static JsonObject Line(string id, double ax, double ay, double bx, double by) => new() { ["id"] = id, ["type"] = "line", ["a"] = J(ax, ay), ["b"] = J(bx, by) };
        static JsonObject Arc(string id, double cx, double start, double end) => new() { ["id"] = id, ["type"] = "arc", ["c"] = J(cx, 0), ["r"] = r, ["start"] = start, ["end"] = end };
        var def = new JsonObject
        {
            ["params"] = J(new JsonObject { ["key"] = "D", ["default"] = depth }),
            ["sketches"] = J(new JsonObject
            {
                ["id"] = "slot", ["plane"] = "xz",
                // In no order, the bottom line drawn backwards; and a hole.
                ["shapes"] = J(
                    Arc("right", L, -Math.PI / 2, Math.PI / 2), Line("top", 0, r, L, r),
                    Line("bottom", L, -r, 0, -r), Arc("left", 0, Math.PI / 2, 3 * Math.PI / 2),
                    new JsonObject { ["id"] = "pin", ["type"] = "circle", ["c"] = J(L / 2, 0), ["r"] = 0.02 }),
            }),
            ["bodies"] = J(new JsonObject { ["name"] = "Slot", ["sketch"] = "slot", ["depth"] = "D" }),
        };
        var f = F();
        var part = Part(f, def);
        // The outline found: the first closed loop, named by its first shape.
        Assert.Equal("right", (string)Def(f, part)["bodies"]![0]!["shape"]!);
        var a = Place(f, part);
        // Two half circles of the chords' rule, and the rectangle between them.
        var n = Chords.Count(r, Math.PI);
        var faceted = 2 * (n * 0.5 * r * r * Math.Sin(Math.PI / n)) + 2 * r * L;
        var exact = Math.PI * r * r + 2 * r * L;
        Assert.Equal(faceted * depth, Volume(f, a), 9);
        Assert.InRange(Volume(f, a), (exact - 2 * Math.PI * r * Chords.Tolerance) * depth, exact * depth);

        var tree = TreeSketch(f, a, "slot");
        Assert.Equal(["Slot"], tree["bodies"]!.AsArray().Select(x => (string)x!));
        bool In(string id, string flag) => (bool)tree["shapes"]!.AsArray().Single(s => (string)s!["id"]! == id)![flag]!;
        Assert.All(new[] { "right", "top", "bottom", "left" }, id => Assert.True(In(id, "inProfile")));
        Assert.False(In("pin", "inProfile"));
        Assert.True(In("pin", "inLoop"));

        // The pin a hole, named by one shape; a hole on the outline's own chain is refused.
        f.Run("studio.updatePartBody", new JsonObject { ["partId"] = part, ["name"] = "Slot", ["shape"] = "top", ["holes"] = J("pin") });
        var pin = Chords.Circle(0.02);
        Assert.Equal((faceted - pin * 0.5 * 0.02 * 0.02 * Math.Sin(2 * Math.PI / pin)) * depth, Volume(f, a), 9);
        Assert.Throws<CommandException>(() => f.Run("studio.updatePartBody", new JsonObject { ["partId"] = part, ["name"] = "Slot", ["holes"] = J("left") }));
        // An open chain is no outline: the top line moved off the arcs' ends.
        Assert.Throws<CommandException>(() => f.Run("studio.partSketchSetShapes", new JsonObject
        {
            ["partId"] = part, ["sketchId"] = "slot",
            ["shapes"] = J(Arc("right", L, -Math.PI / 2, Math.PI / 2), Line("top", 0, r + 0.01, L, r), Line("bottom", L, -r, 0, -r), Arc("left", 0, Math.PI / 2, 3 * Math.PI / 2)),
        }));
        f.Session.Undo();
        Assert.Equal(faceted * depth, Volume(f, a), 9);
    }
}
