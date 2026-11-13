using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// A part's own sketches (docs/plan-studio-parametric.md §9 point 2): a formula corner becomes a point with driving distanceX and
/// distanceY from the sketch's origin carrying its formula; such points mix with points the solver moves; each placed part solves
/// the sketch with its values, once per set of values; edits of the sketch are one step each, solved with the defaults, refused
/// when they conflict or when a placed part's values would not build.
/// </summary>
public sealed class PartSketchTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(params JsonNode?[] v) => new(v);

    // The angle bracket of FamilyTests.
    static JsonObject Bracket() => new()
    {
        ["ifcClass"] = "IfcFurnishingElement",
        ["params"] = J(
            new JsonObject { ["key"] = "W", ["default"] = 0.4 }, new JsonObject { ["key"] = "H", ["default"] = 0.3 },
            new JsonObject { ["key"] = "T", ["default"] = 0.02 }, new JsonObject { ["key"] = "L", ["default"] = 0.1 },
            new JsonObject { ["key"] = "HasShelf", ["kind"] = "bool", ["default"] = 0 }),
        ["bodies"] = J(
            new JsonObject { ["name"] = "Angle", ["plane"] = "xz", ["points"] = J(J(0, 0), J("W", 0), J("W", "T"), J("T", "T"), J("T", "H"), J(0, "H")), ["depth"] = "L" },
            new JsonObject { ["name"] = "Shelf", ["plane"] = "xy", ["when"] = "HasShelf", ["offset"] = J(0, 0, "H"), ["points"] = J(J(0, 0), J("W", 0), J("W", "L"), J(0, "L")), ["depth"] = "T" }),
    };

    static double Volume(Fixture f, string id) => f.Session.Quantities(id).SelectMany(q => q.Quantities).Single(q => q.Name == "NetVolume").Value;
    static string Part(Fixture f, JsonObject def) => (string)f.Run("studio.createPart", new JsonObject { ["name"] = "Bracket", ["def"] = def })!["id"]!;
    static string Place(Fixture f, string part, JsonObject? values = null, double x = 0) =>
        (string)f.Run("studio.placePart", new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(x, 0), ["values"] = values })!["id"]!;
    static JsonObject Def(Fixture f, string part) => f.Model.Get(part)!.Attrs["def"]!.AsObject();
    static JsonObject Sketch(Fixture f, string part, string id) => PartSketches.Find(Def(f, part), id)!;
    static JsonObject TreeSketch(Fixture f, string instance, string id) =>
        f.Session.Query("studio.partTree", new JsonObject { ["id"] = instance })!["sketches"]!.AsArray().Single(s => (string?)s!["id"] == id)!.AsObject();
    static string Constraint(JsonObject sketch, string kind, string part, string shape = "outline") =>
        (string)sketch["constraints"]!.AsArray().Single(c => (string)c!["kind"]! == kind && (string)c["refs"]![0]!["part"]! == part && (string)c["refs"]![0]!["shape"]! == shape)!["id"]!;

    static readonly JsonObject[] Sets =
    [
        new(),
        new() { ["W"] = 0.6 },
        new() { ["W"] = 0.25, ["H"] = 0.5 },
        new() { ["W"] = 0.7, ["T"] = 0.03, ["HasShelf"] = true },
    ];

    [Fact]
    public void A_part_converted_to_constrained_sketches_makes_the_same_bodies()
    {
        var f = F();
        var part = Part(f, Bracket());
        var placed = Sets.Select((v, i) => Place(f, part, (JsonObject)v.DeepClone(), i * 2)).ToList();
        var before = placed.Select(id => (f.Bounds(id), Volume(f, id), f.Session.Geometry(id)!.Parts.Count)).ToList();

        var made = f.Run("studio.constrainPart", new JsonObject { ["id"] = part })!["sketches"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(["s1", "s2"], made);
        var def = Def(f, part);
        Assert.All(def["bodies"]!.AsArray(), b => { Assert.NotNull(b!["sketch"]); Assert.Null(b["points"]); });
        // Each corner a driving distanceX and distanceY from the origin with its formula or number: the Angle's six corners, the Shelf's four.
        var angle = Sketch(f, part, "s1");
        Assert.Equal("xz", (string)angle["plane"]!);
        Assert.Equal(12, angle["constraints"]!.AsArray().Count);
        Assert.Equal("W", (string)angle["constraints"]!.AsArray().Single(c => (string)c!["id"]! == Constraint(angle, "distanceX", "vertex:1"))!["formula"]!);
        Assert.Equal(0, (double)angle["constraints"]!.AsArray().Single(c => (string)c!["id"]! == Constraint(angle, "distanceY", "vertex:1"))!["value"]!);
        Assert.Equal(8, Sketch(f, part, "s2")["constraints"]!.AsArray().Count);
        Assert.NotNull(Sketch(f, part, "s2")["offset"]);

        for (var i = 0; i < placed.Count; i++)
        {
            var ((min0, max0), v0, n0) = before[i];
            var (min, max) = f.Bounds(placed[i]);
            Assert.Equal(v0, Volume(f, placed[i]), 9);
            Assert.Equal(n0, f.Session.Geometry(placed[i])!.Parts.Count);
            Assert.Equal(min0.X, min.X, 9); Assert.Equal(min0.Y, min.Y, 9); Assert.Equal(min0.Z, min.Z, 9);
            Assert.Equal(max0.X, max.X, 9); Assert.Equal(max0.Y, max.Y, 9); Assert.Equal(max0.Z, max.Z, 9);
        }
        // Nothing is left to convert; the IFC goes as before.
        Assert.Throws<CommandException>(() => f.Run("studio.constrainPart", new JsonObject { ["id"] = part }));
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "part.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("IFCFURNISHINGELEMENT(", ifc);
        // The constraints are no IFC entity: they ride only inside the part's definition in Lapin_Intent, as its formulas always did.
        Assert.DoesNotContain("IFCPROPERTYSINGLEVALUE('constraints'", ifc);
        // One undo step takes it back to formula corners.
        f.Session.Undo();
        Assert.NotNull(Def(f, part)["bodies"]![0]!["points"]);
    }

    [Fact]
    public void A_corner_s_dimension_replaced_by_a_geometric_constraint_still_solves_for_every_instance()
    {
        var f = F();
        var part = Part(f, Bracket());
        var placed = Sets.Select((v, i) => Place(f, part, (JsonObject)v.DeepClone(), i * 2)).ToList();
        var volumes = placed.Select(id => Volume(f, id)).ToList();
        f.Run("studio.constrainPart", new JsonObject { ["id"] = placed[1] });
        var angle = Sketch(f, part, "s1");
        Assert.Equal("solved", (string)TreeSketch(f, placed[1], "s1")["status"]!);
        Assert.Equal(0, (int)TreeSketch(f, placed[1], "s1")["dof"]!);

        // The corner (W, T) loses its x: one degree of freedom, that x free.
        f.Run("studio.partSketchRemoveConstraint", new JsonObject { ["partId"] = part, ["sketchId"] = "s1", ["id"] = Constraint(angle, "distanceX", "vertex:2") });
        var loose = TreeSketch(f, placed[1], "s1");
        Assert.Equal("underconstrained", (string)loose["status"]!);
        Assert.Equal(1, (int)loose["dof"]!);
        Assert.Contains(loose["freeShapes"]!.AsArray(), x => (string)x!["part"]! == "vertex:2");

        // A side that would be level cannot be: refused, nothing changed.
        var count = Sketch(f, part, "s1")["constraints"]!.AsArray().Count;
        Assert.Throws<CommandException>(() => f.Run("studio.partSketchAddConstraint", new JsonObject
        {
            ["partId"] = part, ["sketchId"] = "s1", ["kind"] = "horizontal", ["refs"] = J(new JsonObject { ["shape"] = "outline", ["part"] = "edge:1" }),
        }));
        Assert.Equal(count, Sketch(f, part, "s1")["constraints"]!.AsArray().Count);

        // The side from (W, 0) to (W, T) made vertical: the corner follows W again, as a geometric constraint.
        var id = (string)f.Run("studio.partSketchAddConstraint", new JsonObject
        {
            ["partId"] = placed[2], ["sketchId"] = "s1", ["kind"] = "vertical", ["refs"] = J(new JsonObject { ["shape"] = "outline", ["part"] = "edge:1" }),
        })!["id"]!;
        Assert.StartsWith("c", id);
        Assert.Equal("solved", (string)TreeSketch(f, placed[1], "s1")["status"]!);
        for (var i = 0; i < placed.Count; i++) Assert.Equal(volumes[i], Volume(f, placed[i]), 9);
        // A placed part's own width moves the free corner with it.
        f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(placed[0]), ["values"] = new JsonObject { ["W"] = 0.9 } });
        Assert.Equal((0.9 * 0.02 + 0.28 * 0.02) * 0.1, Volume(f, placed[0]), 9);
        var corner = TreeSketch(f, placed[0], "s1")["shapes"]!.AsArray().Single(s => (string)s!["id"]! == "outline")!["pts"]![2]!;
        Assert.Equal(0.9, (double)corner[0]!, 9);
    }

    [Fact]
    public void Instances_sharing_their_values_solve_once()
    {
        var f = F();
        var part = Part(f, Bracket());
        f.Run("studio.constrainPart", new JsonObject { ["id"] = part });
        var a = Place(f, part);
        var b = Place(f, part, new JsonObject { ["W"] = 0.6 }, 2);
        var c = Place(f, part, new JsonObject { ["W"] = 0.6 }, 4);
        // HasShelf is not named by the Angle's formulas: the same solve.
        var d = Place(f, part, new JsonObject { ["W"] = 0.6, ["HasShelf"] = true }, 6);
        foreach (var id in new[] { a, b, c, d, a, b, c, d }) { f.Session.Geometry(id); Volume(f, id); }
        // The defaults (solved when the part was checked) and W = 0.6.
        Assert.Equal(2, PartSketches.SolveCount(Sketch(f, part, "s1")));
        Assert.Equal(Volume(f, b), Volume(f, c), 12);
    }

    // A square whose right side is placed by W and whose bottom is L long: redundant while W = L, a conflict otherwise.
    static JsonObject Square() => new()
    {
        ["params"] = J(new JsonObject { ["key"] = "W", ["default"] = 1 }, new JsonObject { ["key"] = "L", ["default"] = 1 }, new JsonObject { ["key"] = "D", ["default"] = 0.2 }),
        ["sketches"] = J(new JsonObject
        {
            ["id"] = "base", ["name"] = "Base", ["plane"] = "xy",
            ["shapes"] = J(new JsonObject { ["id"] = "sq", ["type"] = "polyline", ["pts"] = J(J(0, 0), J(1, 0), J(1, 1), J(0, 1)), ["closed"] = true }),
            ["constraints"] = J(
                Dim("distanceX", "vertex:0", 0), Dim("distanceY", "vertex:0", 0),
                Dim("distanceX", "vertex:1", "W"), Dim("distanceY", "vertex:1", 0),
                Dim("distanceX", "vertex:2", "W"), Dim("distanceY", "vertex:2", 1),
                Dim("distanceX", "vertex:3", 0), Dim("distanceY", "vertex:3", 1),
                new JsonObject { ["kind"] = "distance", ["refs"] = J(new JsonObject { ["shape"] = "sq", ["part"] = "edge:0" }), ["formula"] = "L" }),
        }),
        ["bodies"] = J(new JsonObject { ["name"] = "Slab", ["sketch"] = "base", ["depth"] = "D" }),
    };

    static JsonObject Dim(string kind, string part, JsonNode v)
    {
        var o = new JsonObject { ["kind"] = kind, ["refs"] = J(new JsonObject { ["shape"] = "sq", ["part"] = part }) };
        if (v is JsonValue jv && jv.TryGetValue<string>(out var s)) o["formula"] = s; else o["value"] = v.DeepClone();
        return o;
    }

    [Fact]
    public void A_part_written_with_sketches_builds_and_values_it_cannot_take_are_refused()
    {
        var f = F();
        var part = Part(f, Square());
        var sketch = Sketch(f, part, "base");
        // Ids given to the constraints, the body's outline found.
        Assert.Equal("c1", (string)sketch["constraints"]![0]!["id"]!);
        Assert.Equal("sq", (string)Def(f, part)["bodies"]![0]!["shape"]!);
        var a = Place(f, part, new JsonObject { ["W"] = 2, ["L"] = 2 });
        Assert.Equal(2 * 1 * 0.2, Volume(f, a), 9);
        Assert.Equal("redundant", (string)TreeSketch(f, a, "base")["status"]!);
        // W without L: the sketch cannot be both; the values are refused and the part keeps its body.
        Assert.Throws<CommandException>(() => f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(a), ["values"] = new JsonObject { ["W"] = 3 } }));
        Assert.Equal(2, (double)f.Model.Get(a)!.Attrs["values"]!["W"]!);
        Assert.Equal(2 * 1 * 0.2, Volume(f, a), 9);
        // An edit that a placed part's values could not take is refused too: L's dimension made a plain 2 breaks the defaults (W = 1).
        var lid = (string)sketch["constraints"]!.AsArray().Single(c => (string)c!["kind"]! == "distance")!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("studio.partSketchUpdateConstraint", new JsonObject { ["partId"] = part, ["sketchId"] = "base", ["id"] = lid, ["value"] = 2 }));
        // ... a formula all can take goes through.
        f.Run("studio.partSketchUpdateConstraint", new JsonObject { ["partId"] = part, ["sketchId"] = "base", ["id"] = lid, ["formula"] = "W" });
        var tree = TreeSketch(f, a, "base");
        var dim = tree["constraints"]!.AsArray().Single(c => (string)c!["id"]! == lid)!;
        Assert.Equal(2, (double)dim["value"]!, 9);
        Assert.Equal(2, (double)dim["measured"]!, 9);
        Assert.Equal(2 * 1 * 0.2, Volume(f, a), 9);
        // A part file carries its sketches.
        var text = (string)f.Session.Query("studio.exportPart", new JsonObject { ["id"] = part })!["text"]!;
        var g = F();
        var copy = (string)g.Run("studio.importPart", new JsonObject { ["text"] = text })!["id"]!;
        Assert.Equal(1 * 1 * 0.2, Volume(g, Place(g, copy)), 9);
    }

    [Fact]
    public void A_sketch_s_shapes_are_edited_within_its_constraints()
    {
        var f = F();
        var part = Part(f, Square());
        var a = Place(f, part);
        JsonObject Sq(double x2, double y2) => new() { ["id"] = "sq", ["type"] = "polyline", ["pts"] = J(J(0, 0), J(1, 0), J(x2, y2), J(0, 1)), ["closed"] = true };
        // A corner held by its dimensions cannot be moved.
        Assert.Throws<CommandException>(() => f.Run("studio.partSketchSetShapes", new JsonObject { ["partId"] = part, ["sketchId"] = "base", ["shapes"] = J(Sq(1.5, 1)) }));
        // Its y freed, the corner may move up; a circle added (a hole for the slab).
        var y2 = Constraint(Sketch(f, part, "base"), "distanceY", "vertex:2", "sq");
        f.Run("studio.partSketchRemoveConstraint", new JsonObject { ["partId"] = part, ["sketchId"] = "base", ["ids"] = J(y2) });
        var r = f.Run("studio.partSketchSetShapes", new JsonObject
        {
            ["partId"] = part, ["sketchId"] = "base",
            ["shapes"] = J(Sq(1, 1.5), new JsonObject { ["type"] = "circle", ["c"] = J(0.5, 0.5), ["r"] = 0.1 }),
        })!;
        var ids = r["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal("sq", ids[0]);
        Assert.Equal(1.25 * 0.2, Volume(f, a), 9);
        // The square made a triangle of the same id: the dimensions of its fourth corner are dropped, and told.
        var tri = f.Run("studio.partSketchSetShapes", new JsonObject
        {
            ["partId"] = part, ["sketchId"] = "base",
            ["shapes"] = J(new JsonObject { ["id"] = "sq", ["type"] = "polyline", ["pts"] = J(J(0, 0), J(1, 0), J(1, 1.5)), ["closed"] = true }, new JsonObject { ["id"] = ids[1], ["type"] = "circle", ["c"] = J(0.5, 0.5), ["r"] = 0.1 }),
        })!;
        Assert.Equal(2, tri["dropped"]!.AsArray().Count);
        Assert.Equal(0.75 * 0.2, Volume(f, a), 9);
        f.Run("studio.updatePart", new JsonObject
        {
            ["id"] = part,
            ["def"] = Def(f, part).DeepClone() is JsonObject d && d["bodies"]![0] is JsonObject b ? Hole(d, b, ids[1]) : null,
        });
        Assert.InRange(Volume(f, a), 0.75 * 0.2 - Math.PI * 0.01 * 0.2, 0.75 * 0.2 - 0.006);
        // The square gone: its constraints are dropped and told; the body has no outline, so the edit is refused.
        Assert.Throws<CommandException>(() => f.Run("studio.partSketchSetShapes", new JsonObject { ["partId"] = part, ["sketchId"] = "base", ["shapes"] = J(new JsonObject { ["id"] = ids[1], ["type"] = "circle", ["c"] = J(0.5, 0.5), ["r"] = 0.1 }) }));
    }

    static JsonObject Hole(JsonObject d, JsonObject b, string hole)
    {
        b["holes"] = J(hole);
        return d;
    }
}
