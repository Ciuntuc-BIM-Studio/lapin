using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Studio as a geometry editor (docs/plan-studio-editor.md): sketches on elevations and work planes, push/pull,
/// the extrusion's options, primitives, and the steps a solid takes (chamfer, fillet, shell, slice, arrays, 3D
/// placement), each measured by the volume it must have.
/// </summary>
public class StudioEditorTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static JsonArray J3(double x, double y, double z) => [x, y, z];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static string Draw(Fixture f, string view, JsonObject shape) => (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;
    static JsonObject Rect(double x0, double y0, double x1, double y1) =>
        new() { ["type"] = "polyline", ["pts"] = new JsonArray(J(x0, y0), J(x1, y0), J(x1, y1), J(x0, y1)), ["closed"] = true };
    static JsonObject Line(double x1, double y1, double x2, double y2) => new() { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) };
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static string Id(JsonNode? r) => (string)r!["id"]!;

    /// <summary>A 1 × 1 square on the plan at the origin, extruded 1 m up: a unit cube.</summary>
    static (Fixture F, string Plan, string Profile, string Cube) Cube()
    {
        var f = F();
        var plan = Plan(f);
        var profile = Draw(f, plan, Rect(0, 0, 1, 1));
        var cube = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = profile, ["depth"] = 1 }));
        return (f, plan, profile, cube);
    }

    static JsonObject Pick(string id, Vec3 p, Vec3 n) => new() { ["id"] = id, ["point"] = J3(p.X, p.Y, p.Z), ["normal"] = J3(n.X, n.Y, n.Z) };
    static JsonObject With(JsonObject o, string key, JsonNode? v) { o[key] = v; return o; }

    // ------------------------------------------------------------ where sketches lie

    [Fact]
    public void A_sketch_on_an_elevation_stands_on_the_plane_square_to_it_and_stays_put_as_the_model_grows()
    {
        var f = F();
        f.Wall(0, 5, 6, 5);
        var south = Id(f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" }));
        // Seen from the south (looking north), y the height: a 2 × 1 panel, 0.5 up; the elevation takes its sketch plane here.
        var panel = Draw(f, south, Rect(1, 0.5, 3, 1.5));
        var solid = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = panel, ["depth"] = 0.2 }));
        var b = f.Bounds(solid);
        Assert.Equal(2, b.Max.X - b.Min.X, 6);
        Assert.Equal(0.5, b.Min.Z, 6); Assert.Equal(1.5, b.Max.Z, 6);
        // Its depth runs towards the viewer (south, −y).
        Assert.Equal(0.2, b.Max.Y - b.Min.Y, 6);
        // A wall far to the east widens the elevation: the panel stays where it was.
        f.Wall(40, 5, 46, 5);
        var after = f.Bounds(solid);
        Assert.Equal(b.Min.X, after.Min.X, 6); Assert.Equal(b.Min.Y, after.Min.Y, 6);
    }

    [Fact]
    public void A_reference_plane_on_a_face_takes_a_sketch_that_stands_out_of_the_face()
    {
        var f = F();
        var type = f.Type("Block", ("Concrete", 0.3, true));
        var wall = f.Wall(0, 0, 4, 0, type);
        var box = f.Bounds(wall);
        // A plane on the wall's south face (−y), as Plane on face makes it from a 3D pick at its lower west corner.
        var plane = Id(f.Run("view.create", new JsonObject { ["kind"] = RefPlanes.Kind, ["point"] = new JsonArray(0, box.Min.Y, box.Min.Z), ["normal"] = new JsonArray(0, -1, 0) }));
        var wp = RefPlanes.Of(f.Model.Get(plane)!);
        Assert.Equal(-1, wp.Normal.Y, 9);
        Assert.Equal(1, Math.Abs(wp.Up.Z), 9);
        // A 1 × 0.5 plinth on it, 0.1 out of the face.
        var (a, b) = (wp.World(new Vec2(1, 0)), wp.World(new Vec2(2, 0.5)));
        var sketch = Draw(f, plane, Rect(1, 0, 2, 0.5));
        var plinth = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = sketch, ["depth"] = 0.1 }));
        var pb = f.Bounds(plinth);
        Assert.Equal(Math.Min(a.X, b.X), pb.Min.X, 6); Assert.Equal(Math.Max(a.X, b.X), pb.Max.X, 6);
        Assert.Equal(box.Min.Y - 0.1, pb.Min.Y, 6); Assert.Equal(box.Min.Y, pb.Max.Y, 6);
        Assert.Equal(0.5, pb.Max.Z - pb.Min.Z, 6);
        // The plane draws: the wall seen face on.
        var drawing = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plane })!;
        Assert.Null((string?)drawing["problem"]);
        // Push/pull on the plinth's front face: its depth, out of the wall.
        f.Run("studio.pushPull", With(Pick(plinth, (a + b) * 0.5 + wp.Normal * 0.1, wp.Normal), "distance", 0.1));
        Assert.Equal(box.Min.Y - 0.2, f.Bounds(plinth).Min.Y, 6);
    }

    // ------------------------------------------------------------ push/pull

    [Fact]
    public void Push_pull_on_an_extrusion_changes_its_depth_or_moves_the_sketch_edge_under_the_side()
    {
        var (f, _, profile, cube) = Cube();
        // The top pulled 0.5 up: 1.5 deep.
        Assert.Equal("depth", (string)f.Run("studio.pushPull", With(Pick(cube, new Vec3(0.5, 0.5, 1), Vec3.UnitZ), "distance", 0.5))!["how"]!);
        Assert.Equal(1.5, f.Model.Get(cube)!.Num("depth"), 9);
        // The bottom pulled 0.2 down: it starts lower.
        f.Run("studio.pushPull", With(Pick(cube, new Vec3(0.5, 0.5, 0), -Vec3.UnitZ), "distance", 0.2));
        var b = f.Bounds(cube);
        Assert.Equal(-0.2, b.Min.Z, 9); Assert.Equal(1.5, b.Max.Z, 9);
        Assert.Equal(1.7, Vol(f, cube), 6);
        // The east side pushed in 0.25: the sketch's edge moves, 0.75 wide.
        Assert.Equal("sketch", (string)f.Run("studio.pushPull", With(Pick(cube, new Vec3(1, 0.5, 0.5), new Vec3(1, 0, 0)), "distance", -0.25))!["how"]!);
        Assert.Equal(0.75 * 1.7, Vol(f, cube), 6);
        Assert.Equal(0.75, ((Lapin.Drafting.Cad.Polyline)Lapin.Drafting.Cad.Drafts.ShapeOf(f.Model.Get(profile)!)!).Pts.Max(p => p.X), 9);
        // Pushing the top through the bottom is refused.
        Assert.Throws<CommandException>(() => f.Run("studio.pushPull", With(Pick(cube, new Vec3(0.3, 0.5, 1.5), Vec3.UnitZ), "distance", -2)));
    }

    [Fact]
    public void Push_pull_on_a_face_no_number_holds_becomes_a_step_and_follows_the_sketch()
    {
        var f = F();
        var plan = Plan(f);
        // A 1 × 1 × 1 cube made by revolving nothing: a sweep of a 1 × 1 square 1 m along a line, so no depth to change.
        var sq = Draw(f, plan, Rect(10, 0, 11, 1));
        var path = Draw(f, plan, Line(0, 0.5, 1, 0.5));
        var bar = Id(f.Run("studio.sweep", new JsonObject { ["profileId"] = sq, ["pathId"] = path }));
        Assert.Equal(1, Vol(f, bar), 6);
        // Its end (east, x = 1) pulled 0.5: a step, the bar 1.5 long.
        Assert.Equal("step", (string)f.Run("studio.pushPull", With(Pick(bar, new Vec3(1, 0.5, 0.5), new Vec3(1, 0, 0)), "distance", 0.5))!["how"]!);
        Assert.Equal(1.5, Vol(f, bar), 6);
        // Its top pushed in 0.25 over the whole length: a second step.
        f.Run("studio.pushPull", With(Pick(bar, new Vec3(0.7, 0.5, 1), Vec3.UnitZ), "distance", -0.25));
        Assert.Equal(1.5 * 0.75, Vol(f, bar), 6);
        // The path lengthened to 2 m: the steps find their faces again.
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = path, ["shape"] = Line(0, 0.5, 2, 0.5) });
        Assert.Equal(2.5 * 0.75, Vol(f, bar), 6);
        // Steps listed, switched off, removed.
        var feature = (JsonObject)f.Session.Query("studio.feature", new JsonObject { ["id"] = bar })!;
        Assert.Equal(2, feature["features"]!.AsArray().Count);
        f.Run("studio.updateFeature", new JsonObject { ["id"] = bar, ["index"] = 1, ["set"] = new JsonObject { ["off"] = true } });
        Assert.Equal(2.5, Vol(f, bar), 6);
        f.Run("studio.moveFeature", new JsonObject { ["id"] = bar, ["index"] = 1, ["to"] = 0 });
        f.Run("studio.removeFeature", new JsonObject { ["id"] = bar, ["index"] = 1 });
        Assert.Equal(2, Vol(f, bar), 6);
    }

    [Fact]
    public void Push_pull_on_a_wall_adds_a_solid_of_its_material_or_cuts_into_it()
    {
        var f = F();
        var type = f.Type("Block", ("Concrete", 0.3, true));
        var wall = f.Wall(0, 0, 4, 0, type);
        var before = Vol(f, wall);
        var box = f.Bounds(wall);
        var face = Pick(wall, new Vec3(2, box.Min.Y, 1), new Vec3(0, -1, 0));
        var pulled = Id(f.Run("studio.pushPull", With((JsonObject)face.DeepClone(), "distance", 0.1)));
        Assert.Equal(4 * 0.1 * (box.Max.Z - box.Min.Z), Vol(f, pulled), 4);
        Assert.Equal(before, Vol(f, wall), 6);
        Assert.NotNull(f.Model.Get(pulled)!.Str("material"));
        // Pushed in 0.1 instead: a cut the wall is less, the cut itself not drawn.
        f.Run("studio.setPush", new JsonObject { ["id"] = pulled, ["distance"] = -0.1 });
        Assert.Equal(before - 4 * 0.1 * (box.Max.Z - box.Min.Z), Vol(f, wall), 4);
        Assert.Empty(f.Session.Geometry(pulled)!.Parts);
        // Pulled out again: the cut goes.
        f.Run("studio.setPush", new JsonObject { ["id"] = pulled, ["distance"] = 0.2 });
        Assert.Equal(before, Vol(f, wall), 6);
        Assert.NotEmpty(f.Session.Geometry(pulled)!.Parts);
    }

    // ------------------------------------------------------------ the extrusion's options

    [Fact]
    public void An_extrusion_both_ways_tapered_up_to_a_face_or_made_to_cut()
    {
        var f = F();
        var plan = Plan(f);
        var sym = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = Draw(f, plan, Rect(0, 0, 1, 1)), ["depth"] = 2, ["symmetric"] = true }));
        Assert.Equal(-1, f.Bounds(sym).Min.Z, 9); Assert.Equal(1, f.Bounds(sym).Max.Z, 9);
        // Tapered so the top is half as wide: a frustum, h/3 (A1 + A2 + √(A1 A2)) = (1 + 0.25 + 0.5) / 3.
        var taper = Math.Atan(0.25) * 180 / Math.PI;
        var frustum = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = Draw(f, plan, Rect(5, 0, 6, 1)), ["depth"] = 1, ["taper"] = taper }));
        Assert.Equal(1.75 / 3, Vol(f, frustum), 6);
        Assert.Throws<CommandException>(() => f.Run("studio.extrude", new JsonObject { ["profileId"] = Draw(f, plan, Rect(9, 0, 10, 1)), ["depth"] = 1, ["taper"] = 89.5 }));
        // A column 0.2 × 0.2 up to the underside of a sloping wedge: from 1 m high at its low end to 2 m at its high end.
        var wedge = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "wedge", ["at"] = J3(20, 0, 1), ["width"] = 4, ["length"] = 4, ["height"] = 1 }));
        var col = Draw(f, plan, Rect(19.9, -0.1, 20.1, 0.1));
        // The wedge's underside is level at z = 1: pick it there.
        var column = Id(f.Run("studio.extrude", new JsonObject
        {
            ["profileId"] = col, ["depth"] = 1,
            ["upTo"] = new JsonObject { ["id"] = wedge, ["point"] = J3(20, 0, 1), ["normal"] = J3(0, 0, -1) },
        }));
        Assert.Equal(0.04 * 1, Vol(f, column), 6);
        // Up to its sloping top instead (1 up over 4 along y): the column's top leans with it, 1.5 m high at its middle.
        var slope = new Vec3(0, -1, 4).Normalized();
        f.Run("studio.setUpTo", new JsonObject { ["id"] = column, ["upTo"] = new JsonObject { ["id"] = wedge, ["point"] = J3(20, 0, 1.5), ["normal"] = J3(slope.X, slope.Y, slope.Z) } });
        Assert.Equal(0.04 * 1.5, Vol(f, column), 6);
        // The wedge raised 1 m: the column follows its face.
        f.Run("studio.place", new JsonObject { ["id"] = wedge, ["at"] = J3(20, 0, 2) });
        Assert.Equal(0.04 * 2.5, Vol(f, column), 6);
        // Made to cut a wall: the wall is less, the cutter not drawn.
        var type = f.Type("Block", ("Concrete", 0.3, true));
        var wall = f.Wall(30, 0, 34, 0, type);
        var before = Vol(f, wall);
        var hole = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = Draw(f, plan, Rect(31, -1, 32, 1)), ["depth"] = 1, ["cut"] = new JsonArray(wall) }));
        Assert.Empty(f.Session.Geometry(hole)!.Parts);
        Assert.Equal(before - 1 * 0.3 * 1, Vol(f, wall), 6);
    }

    [Fact]
    public void A_profile_of_lines_and_an_arc_meeting_end_to_end_is_closed()
    {
        var f = F();
        var plan = Plan(f);
        // A D: a 2 m line along x, two 1 m legs up, and a half circle of radius 1 over them.
        var bottom = Draw(f, plan, Line(0, 0, 2, 0));
        Draw(f, plan, Line(2, 0, 2, 1));
        Draw(f, plan, new JsonObject { ["type"] = "arc", ["c"] = J(1, 1), ["r"] = 1, ["start"] = 0, ["end"] = Math.PI });
        Draw(f, plan, Line(0, 1, 0, 0));
        var d = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = bottom, ["depth"] = 1 }));
        Assert.InRange(Vol(f, d), 2 + Math.PI / 2 * 0.995, 2 + Math.PI / 2);
        var feature = (JsonObject)f.Session.Query("studio.feature", new JsonObject { ["id"] = d })!;
        Assert.Equal(4, feature["sketch"]!.AsArray().Count);
        // The arc moved off its ends: the outline is open, the solid says so.
        var open = Draw(f, plan, Line(10, 0, 12, 0));
        Assert.Throws<CommandException>(() => f.Run("studio.extrude", new JsonObject { ["profileId"] = open, ["depth"] = 1 }));
    }

    [Fact]
    public void Primitives_have_the_volumes_of_their_shapes()
    {
        var f = F();
        double P(string shape, JsonObject sizes)
        {
            sizes["shape"] = shape; sizes["at"] = J(0, 0);
            return Vol(f, Id(f.Run("studio.primitive", sizes)));
        }
        Assert.Equal(6, P("box", new JsonObject { ["width"] = 1, ["length"] = 2, ["height"] = 3 }), 9);
        Assert.Equal(3, P("wedge", new JsonObject { ["width"] = 1, ["length"] = 2, ["height"] = 3 }), 9);
        Assert.Equal(2, P("pyramid", new JsonObject { ["width"] = 1, ["length"] = 2, ["height"] = 3 }), 9);
        // Round ones as 32 sides: within a percent.
        Assert.InRange(P("cylinder", new JsonObject { ["radius"] = 1, ["height"] = 1 }), Math.PI * 0.99, Math.PI);
        Assert.InRange(P("cone", new JsonObject { ["radius"] = 1, ["height"] = 3 }), Math.PI * 0.99, Math.PI);
        Assert.InRange(P("cone", new JsonObject { ["radius"] = 1, ["radius2"] = 0.5, ["height"] = 3 }), Math.PI * 1.75 * 0.99, Math.PI * 1.75);
        Assert.InRange(P("sphere", new JsonObject { ["radius"] = 1 }), 4 * Math.PI / 3 * 0.97, 4 * Math.PI / 3);
        Assert.InRange(P("torus", new JsonObject { ["radius"] = 1, ["radius2"] = 0.25 }), 2 * Math.PI * Math.PI * 0.0625 * 0.96, 2 * Math.PI * Math.PI * 0.0625);
        // A box stands on its point, turned about it.
        var box = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = J3(10, 0, 2), ["width"] = 2, ["length"] = 1, ["height"] = 1, ["rotation"] = 90 }));
        var b = f.Bounds(box);
        Assert.Equal(9.5, b.Min.X, 9); Assert.Equal(10.5, b.Max.X, 9); Assert.Equal(2, b.Min.Z, 9);
        // Moved in plan: its point goes with it.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(box), ["op"] = "move", ["by"] = J(5, 0) });
        Assert.Equal(14.5, f.Bounds(box).Min.X, 9);
        Assert.Throws<CommandException>(() => f.Run("studio.primitive", new JsonObject { ["shape"] = "torus", ["at"] = J(0, 0), ["radius"] = 1, ["radius2"] = 2 }));
    }

    // ------------------------------------------------------------ steps

    [Fact]
    public void An_edge_chamfered_or_filleted_loses_its_corner()
    {
        var (f, _, _, cube) = Cube();
        // The top east edge picked on the top face near it: a 0.2 bevel takes 0.2² / 2 along 1 m.
        f.Run("studio.addFeature", new JsonObject { ["id"] = cube, ["feature"] = With(With(With(new JsonObject { ["op"] = "chamfer" }, "distance", 0.2), "point", J3(0.95, 0.5, 1)), "normal", J3(0, 0, 1)) });
        Assert.Equal(1 - 0.02, Vol(f, cube), 6);
        // A round of 0.2 instead: (1 − π/4) 0.2², the round in 12 steps.
        f.Run("studio.updateFeature", new JsonObject { ["id"] = cube, ["index"] = 0, ["set"] = new JsonObject { ["off"] = true } });
        f.Run("studio.addFeature", new JsonObject { ["id"] = cube, ["feature"] = With(With(With(new JsonObject { ["op"] = "fillet" }, "radius", 0.2), "point", J3(0.95, 0.5, 1)), "normal", J3(0, 0, 1)) });
        Assert.Equal(1 - (1 - Math.PI / 4) * 0.04, Vol(f, cube), 3);
        // The depth changed: the fillet finds its edge again (it lies along the top, which moved).
        f.Run("studio.pushPull", With(Pick(cube, new Vec3(0.5, 0.5, 1), Vec3.UnitZ), "distance", 1));
        Assert.Equal(2 - (1 - Math.PI / 4) * 0.04, Vol(f, cube), 3);
        // Every edge round the top: four bevels.
        var (g, _, _, other) = Cube();
        g.Run("studio.addFeature", new JsonObject { ["id"] = other, ["feature"] = new JsonObject { ["op"] = "chamfer", ["distance"] = 0.1, ["point"] = J3(0.5, 0.5, 1), ["normal"] = J3(0, 0, 1), ["face"] = true } });
        // Where two bevels meet at a corner they share what both take: 4 · 0.1² / 2 less the four corners' overlaps.
        Assert.InRange(Vol(g, other), 1 - 4 * 0.005, 1 - 4 * 0.005 + 4 * 0.001 / 3 + 1e-6);
        Assert.Throws<CommandException>(() => g.Run("studio.addFeature", new JsonObject { ["id"] = other, ["feature"] = new JsonObject { ["op"] = "fillet", ["radius"] = 0 } }));
    }

    [Fact]
    public void A_shell_hollows_the_operation_open_where_it_is_told()
    {
        var (f, _, _, cube) = Cube();
        f.Run("studio.addFeature", new JsonObject { ["id"] = cube, ["feature"] = new JsonObject { ["op"] = "shell", ["thickness"] = 0.1, ["open"] = "end" } });
        // Open at the top: the inside 0.8 × 0.8 × 0.9 is gone.
        Assert.Equal(1 - 0.64 * 0.9, Vol(f, cube), 6);
        f.Run("studio.updateFeature", new JsonObject { ["id"] = cube, ["index"] = 0, ["set"] = new JsonObject { ["open"] = "none" } });
        Assert.Equal(1 - 0.64 * 0.8, Vol(f, cube), 6);
        // A cylinder primitive shelled into a pipe, open at both ends.
        var pipe = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "cylinder", ["at"] = J(10, 0), ["radius"] = 0.5, ["height"] = 2 }));
        var full = Vol(f, pipe);
        f.Run("studio.addFeature", new JsonObject { ["id"] = pipe, ["feature"] = new JsonObject { ["op"] = "shell", ["thickness"] = 0.1, ["open"] = "both" } });
        Assert.Equal(full * (1 - 0.16 / 0.25), Vol(f, pipe), 3);
        // Too thick: it says so and the solid stays whole.
        f.Run("studio.updateFeature", new JsonObject { ["id"] = cube, ["index"] = 0, ["set"] = new JsonObject { ["thickness"] = 0.6 } });
        Assert.Equal(1, Vol(f, cube), 6);
        Assert.NotEmpty(f.Session.Geometry(cube)!.Diagnostics!);
    }

    [Fact]
    public void A_solid_sliced_split_or_repeated()
    {
        var (f, plan, _, cube) = Cube();
        // Split by the plane x = 0.25: two solids, a quarter and three quarters, sharing the sketch.
        var ids = f.Run("studio.split", new JsonObject { ["id"] = cube, ["point"] = J3(0.25, 0, 0), ["normal"] = J3(1, 0, 0) })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(0.25, Vol(f, ids[0]), 6);
        Assert.Equal(0.75, Vol(f, ids[1]), 6);
        Assert.Equal(f.Model.Get(ids[0])!.Str("profile"), f.Model.Get(ids[1])!.Str("profile"));
        // By a drafted line on the plan: the plane through it, upright.
        var bar = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = Draw(f, plan, Rect(10, 0, 12, 1)), ["depth"] = 1 }));
        var line = Draw(f, plan, Line(11, -1, 11, 2));
        f.Run("studio.addFeature", new JsonObject { ["id"] = bar, ["feature"] = new JsonObject { ["op"] = "slice", ["lineId"] = line } });
        Assert.Equal(1, Vol(f, bar), 6);
        // Arrays: three in a row 2 m apart, four round an upright 3 m away.
        var post = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = J(20, 0), ["width"] = 0.5, ["length"] = 0.5, ["height"] = 1 }));
        f.Run("studio.addFeature", new JsonObject { ["id"] = post, ["feature"] = new JsonObject { ["op"] = "array", ["count"] = 3, ["step"] = J3(2, 0, 0) } });
        Assert.Equal(0.75, Vol(f, post), 6);
        Assert.Equal(24.25, f.Bounds(post).Max.X, 6);
        var ring = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = J(33, 0), ["width"] = 0.5, ["length"] = 0.5, ["height"] = 1 }));
        f.Run("studio.addFeature", new JsonObject { ["id"] = ring, ["feature"] = new JsonObject { ["op"] = "array", ["kind"] = "polar", ["count"] = 4, ["centre"] = J3(30, 0, 0) } });
        Assert.Equal(1, Vol(f, ring), 6);
        Assert.Equal(26.75, f.Bounds(ring).Min.X, 6);
    }

    [Fact]
    public void A_solid_turned_mirrored_and_moved_in_3D_and_one_on_a_section_moved_in_plan()
    {
        var (f, _, _, cube) = Cube();
        // Turned 90° about its bottom south edge (along x): it lies on its side, south of it.
        f.Run("studio.addFeature", new JsonObject { ["id"] = cube, ["feature"] = new JsonObject { ["op"] = "rotate", ["centre"] = J3(0, 0, 0), ["axis"] = J3(1, 0, 0), ["angle"] = 90 } });
        var b = f.Bounds(cube);
        Assert.Equal(-1, b.Min.Y, 6); Assert.Equal(0, b.Max.Y, 6); Assert.Equal(0, b.Min.Z, 6); Assert.Equal(1, b.Max.Z, 6);
        // Mirrored in the level plane through the origin: below it.
        f.Run("studio.addFeature", new JsonObject { ["id"] = cube, ["feature"] = new JsonObject { ["op"] = "mirror", ["point"] = J3(0, 0, 0), ["normal"] = J3(0, 0, 1) } });
        Assert.Equal(-1, f.Bounds(cube).Min.Z, 6);
        f.Run("studio.addFeature", new JsonObject { ["id"] = cube, ["feature"] = new JsonObject { ["op"] = "move", ["by"] = J3(0, 0, 2) } });
        Assert.Equal(2, f.Bounds(cube).Max.Z, 6);
        Assert.Equal(1, Vol(f, cube), 6);
        // A solid drawn on a section, moved 3 m east in plan: its sketch stays on the section, the solid goes.
        var section = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(0, 10), ["end"] = J(10, 10) }));
        var s = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = Draw(f, section, Rect(1, 0, 2, 1)), ["depth"] = 0.5 }));
        var x0 = f.Bounds(s).Min.X;
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(s), ["op"] = "move", ["by"] = J(3, 0) });
        Assert.Equal(x0 + 3, f.Bounds(s).Min.X, 6);
        // A copy shares the sketch.
        var copy = (string)f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(s), ["op"] = "copy", ["by"] = J(0, 5) })!["ids"]![0]!;
        Assert.Equal(f.Model.Get(s)!.Str("profile"), f.Model.Get(copy)!.Str("profile"));
        Assert.Equal(0.5, Vol(f, copy), 6);
    }

    // ------------------------------------------------------------ sweeps and lofts that change along the way

    [Fact]
    public void A_sweep_twisted_scaled_or_rising_into_a_helix()
    {
        var f = F();
        var plan = Plan(f);
        // Scaled to 2× at the end over 3 m: the area grows from 1 to 4, ∫ (1 + t/3)² dt = 7.
        var sq = Draw(f, plan, Rect(20, 0, 21, 1));
        var grow = Id(f.Run("studio.sweep", new JsonObject { ["profileId"] = sq, ["pathId"] = Draw(f, plan, Line(0, 0, 3, 0)), ["scaleEnd"] = 2 }));
        Assert.Equal(7, Vol(f, grow), 6);
        // Twisted a quarter turn: the volume stays 3 (the section turns about its middle).
        var tw = Id(f.Run("studio.sweep", new JsonObject { ["profileId"] = Draw(f, plan, Rect(30, 0, 31, 1)), ["pathId"] = Draw(f, plan, Line(0, 5, 3, 5)), ["twist"] = 90 }));
        Assert.InRange(Vol(f, tw), 2.85, 3.15);
        // A circle of radius 2 rising 3 m over two turns: a helix, its top 3 m higher than its bottom's top.
        var tube = Draw(f, plan, new JsonObject { ["type"] = "circle", ["c"] = J(40, 0.1), ["r"] = 0.1 });
        var helix = Id(f.Run("studio.sweep", new JsonObject
        {
            ["profileId"] = tube, ["pathId"] = Draw(f, plan, new JsonObject { ["type"] = "circle", ["c"] = J(0, 20), ["r"] = 2 }), ["rise"] = 3, ["turns"] = 2,
        }));
        var b = f.Bounds(helix);
        Assert.Equal(0, b.Min.Z, 6); Assert.Equal(3.2, b.Max.Z, 6);
        Assert.InRange(Vol(f, helix), 0.9 * Math.PI * 0.01 * 2 * 2 * Math.PI * 2, 1.1 * Math.PI * 0.01 * 2 * 2 * Math.PI * 2);
        // A ring may not twist by part of a turn.
        var ring = Id(f.Run("studio.sweep", new JsonObject { ["profileId"] = Draw(f, plan, Rect(50, 0, 50.2, 0.2)), ["pathId"] = Draw(f, plan, Rect(0, 30, 4, 34)), ["twist"] = 45 }));
        Assert.Empty(f.Session.Geometry(ring)!.Parts);
    }

    [Fact]
    public void A_loft_smooth_through_its_profiles_or_bent_along_a_guide()
    {
        var f = F();
        while (Spatial.Storeys(f.Model).Count() < 3) f.Run("storey.create", new JsonObject());
        var storeys = Spatial.Storeys(f.Model).OrderBy(s => s.Num("elevation")).ToList();
        string PlanOf(string storey) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey })!["id"]!;
        // Three squares one over another (on three levels): 1, 2 and 1 m wide.
        var (p0, p1, p2) = (PlanOf(storeys[0].Id), PlanOf(storeys[1].Id), PlanOf(storeys[2].Id));
        var a = Draw(f, p0, Rect(-0.5, -0.5, 0.5, 0.5));
        var b = Draw(f, p1, Rect(-1, -1, 1, 1));
        var c = Draw(f, p2, Rect(-0.5, -0.5, 0.5, 0.5));
        var straight = Id(f.Run("studio.loft", new JsonObject { ["profileId"] = a, ["profileIds"] = new JsonArray(a, b, c) }));
        var s0 = Draw(f, p0, Rect(9.5, -0.5, 10.5, 0.5));
        var smooth = Id(f.Run("studio.loft", new JsonObject { ["profileId"] = s0, ["profileIds"] = new JsonArray(s0, Draw(f, p1, Rect(9, -1, 11, 1)), Draw(f, p2, Rect(9.5, -0.5, 10.5, 0.5))), ["smooth"] = true }));
        // Smooth bulges past the straight one's sides between the profiles: more volume.
        Assert.True(Vol(f, smooth) > Vol(f, straight) + 1e-3);
        // Bent along a guide on a section: two profiles that follow an arch sideways.
        var section = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(20, -5), ["end"] = J(30, -5) }));
        var h = storeys[1].Num("elevation") - storeys[0].Num("elevation");
        var guide = Draw(f, section, new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, storeys[0].Num("elevation")), J(2, storeys[0].Num("elevation") + h / 2), J(0, storeys[1].Num("elevation"))), ["closed"] = false });
        var g0 = Draw(f, p0, Rect(19.5, -0.5, 20.5, 0.5));
        var bent = Id(f.Run("studio.loft", new JsonObject { ["profileId"] = g0, ["profileIds"] = new JsonArray(g0, Draw(f, p1, Rect(19.5, -0.5, 20.5, 0.5))), ["railId"] = guide }));
        var bb = f.Bounds(bent);
        Assert.Equal(22.5, bb.Max.X, 6);
    }

    // ------------------------------------------------------------ elements made into Studio solids

    [Fact]
    public void An_element_made_into_a_Studio_solid_keeps_its_body_and_takes_steps()
    {
        var f = F();
        var type = f.Type("Block", ("Concrete", 0.3, true));
        var wall = f.Wall(0, 0, 4, 0, type);
        var before = Vol(f, wall);
        var box = f.Bounds(wall);
        var made = f.Run("studio.fromElement", new JsonObject { ["ids"] = new JsonArray(wall) })!["ids"]![0]!.ToString();
        var solid = f.Model.Get(made)!;
        Assert.Equal("body", solid.Str("operation"));
        Assert.Equal("IfcWall", solid.Str("exportAs"));
        Assert.Equal(before, Vol(f, made), 6);
        // Its top edge on the south chamfered.
        f.Run("studio.addFeature", new JsonObject { ["id"] = made, ["feature"] = new JsonObject { ["op"] = "chamfer", ["distance"] = 0.1, ["point"] = J3(2, box.Min.Y + 0.01, box.Max.Z), ["normal"] = J3(0, 0, 1) } });
        Assert.Equal(before - 0.005 * 4, Vol(f, made), 6);
        // Moved in plan.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(made), ["op"] = "move", ["by"] = J(1, 0) });
        Assert.Equal(box.Min.X + 1, f.Bounds(made).Min.X, 6);
        // Its triangles are not sent with its node.
        Assert.False(f.Session.Snapshot().Nodes.Single(n => n.Id == made).Props.ContainsKey(StudioModule.BodyKey));
    }
}
