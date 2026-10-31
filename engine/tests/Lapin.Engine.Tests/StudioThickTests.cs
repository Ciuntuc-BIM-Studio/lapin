using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>Studio's thick solids (Ring, Box, Wall strip) on every kind of plane, reference planes, and native walls along a shape (docs/plan-studio-thick.md).</summary>
public class StudioThickTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static JsonArray J3(double x, double y, double z) => [x, y, z];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static string Draw(Fixture f, string view, JsonObject shape) => (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;
    static JsonObject Rect(double x0, double y0, double x1, double y1) =>
        new() { ["type"] = "polyline", ["pts"] = new JsonArray(J(x0, y0), J(x1, y0), J(x1, y1), J(x0, y1)), ["closed"] = true };
    static JsonObject Poly(bool closed, params (double X, double Y)[] pts) =>
        new() { ["type"] = "polyline", ["pts"] = new JsonArray(pts.Select(p => (JsonNode)J(p.X, p.Y)).ToArray()), ["closed"] = closed };
    static JsonObject Circle(double x, double y, double r) => new() { ["type"] = "circle", ["c"] = J(x, y), ["r"] = r };
    static JsonObject Arc(double x, double y, double r, double start, double end) => new() { ["type"] = "arc", ["c"] = J(x, y), ["r"] = r, ["start"] = start, ["end"] = end };
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static string Make(Fixture f, string op, string profile, JsonObject? more = null)
    {
        var a = new JsonObject { ["profileId"] = profile };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("studio." + op, a)!["id"]!;
    }
    static void Update(Fixture f, string id, JsonObject attrs) => f.Run("element.update", new JsonObject { ["id"] = id, ["attrs"] = attrs });

    /// <summary>A circle as Thick builds it: a 64-sided polygon of that radius.</summary>
    static double Disc(double r) => 32 * r * r * Math.Sin(2 * Math.PI / 64);

    [Fact]
    public void A_ring_from_a_circle_and_an_arc_inside_outside_or_centred_on_a_plan()
    {
        var f = F();
        var plan = Plan(f);
        // A circle of radius 2, a ring 0.2 thick inside it, 3 m high: the 64-gons of 2 and 1.8.
        var ring = Make(f, "ring", Draw(f, plan, Circle(0, 0, 2)), new() { ["thickness"] = 0.2, ["height"] = 3 });
        Assert.Equal((Disc(2) - Disc(1.8)) * 3, Vol(f, ring), 6);
        var e = f.Model.Get(ring)!;
        Assert.Equal("IfcWall", e.Str("exportAs"));
        Assert.Equal("inside", e.Str("justify"));
        var b = f.Bounds(ring);
        Assert.Equal(2, b.Max.X, 6);
        Assert.Equal(0, b.Min.Z, 9);
        Assert.Equal(3, b.Max.Z, 9);
        // Outside, then centred: the ring grows out of the circle, then straddles it.
        Update(f, ring, new() { ["justify"] = "outside" });
        Assert.Equal((Disc(2.2) - Disc(2)) * 3, Vol(f, ring), 6);
        Update(f, ring, new() { ["justify"] = "centre" });
        Assert.Equal((Disc(2.1) - Disc(1.9)) * 3, Vol(f, ring), 6);
        // A quarter arc of radius 5, centred, 0.3 thick, 2 high: a quarter of the annulus (polygonised: within 0.1 %).
        var arc = Make(f, "ring", Draw(f, plan, Arc(10, 0, 5, 0, Math.PI / 2)), new() { ["thickness"] = 0.3, ["height"] = 2, ["justify"] = "centre" });
        var exact = Math.PI / 4 * (5.15 * 5.15 - 4.85 * 4.85) * 2;
        Assert.InRange(Vol(f, arc), exact * 0.998, exact);
        // A thickness that leaves no hole is refused; a ring is not made from a line.
        Assert.Throws<CommandException>(() => Make(f, "ring", Draw(f, plan, Circle(20, 0, 0.1)), new() { ["thickness"] = 0.2 }));
        Assert.Throws<CommandException>(() => Make(f, "ring", Draw(f, plan, Poly(false, (0, 10), (5, 10)))));
    }

    [Fact]
    public void A_wall_strip_is_mitred_at_its_corners_and_lies_left_right_or_centred()
    {
        var f = F();
        var plan = Plan(f);
        // An L, 4 m then 3 m turning left, 0.2 thick, 3 high: mitred, its band is the thickness times its middle line.
        var line = Draw(f, plan, Poly(false, (0, 0), (4, 0), (4, 3)));
        var wall = Make(f, "wall", line, new() { ["thickness"] = 0.2, ["height"] = 3 });
        Assert.Equal(0.2 * 7 * 3, Vol(f, wall), 9);
        var b = f.Bounds(wall);
        Assert.Equal(4.1, b.Max.X, 9);
        Assert.Equal(-0.1, b.Min.Y, 9);
        Assert.Equal(0, b.Min.X, 9);
        Assert.Equal(3, b.Max.Y, 9);
        // One solid of eight planar faces: the two ends, the two caps, two sides inside and two outside the corner.
        var solid = f.Session.Geometry(wall)!.Parts.Single().Solid;
        Assert.Equal(8, solid.Faces.Length);
        // Left of the line (inside the turn): its middle line is 3.9 + 2.9; right: 4.1 + 3.1.
        Update(f, wall, new() { ["justify"] = "left" });
        Assert.Equal(0.2 * 6.8 * 3, Vol(f, wall), 9);
        Assert.Equal(0, f.Bounds(wall).Min.Y, 9);
        Update(f, wall, new() { ["justify"] = "right" });
        Assert.Equal(0.2 * 7.2 * 3, Vol(f, wall), 9);
        Assert.Equal(4.2, f.Bounds(wall).Max.X, 9);
        // A wall strip lies left, right or centred: inside is a ring's.
        Assert.Throws<CommandException>(() => Update(f, wall, new() { ["justify"] = "inside" }));
        // Measured as a wall: its middle line, height and width.
        var q = f.Session.Quantities(wall).Single();
        Assert.Equal("Qto_WallBaseQuantities", q.Name);
        Assert.Equal(7.2, q.Quantities.Single(x => x.Name == "Length").Value, 9);
        Assert.Equal(0.2, q.Quantities.Single(x => x.Name == "Width").Value, 9);
        Assert.Equal(3, q.Quantities.Single(x => x.Name == "Height").Value, 9);
        // The line edited: the same wall (its GlobalId), rebuilt.
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = line, ["shape"] = Poly(false, (0, 0), (6, 0), (6, 3)) });
        Assert.Equal(0.2 * 9.2 * 3, Vol(f, wall), 9);
        Assert.Equal(wall, f.Model.Get(wall)!.Id);
        // A negative height goes down from the plan.
        Update(f, wall, new() { ["height"] = -1 });
        Assert.Equal(-1, f.Bounds(wall).Min.Z, 9);
        Assert.Equal(0, f.Bounds(wall).Max.Z, 9);
    }

    [Fact]
    public void A_box_makes_its_bottom_and_top_as_slabs_grouped_with_its_walls()
    {
        var f = F();
        var plan = Plan(f);
        // A 4 × 3 m box, walls 0.2 inside the outline, 2 m high, no bottom nor top: only the walls.
        var outline = Draw(f, plan, Rect(0, 0, 4, 3));
        var box = (JsonObject)f.Run("studio.box", new JsonObject { ["profileId"] = outline, ["thickness"] = 0.2, ["height"] = 2 })!;
        var id = (string)box["id"]!;
        Assert.Equal((12 - 3.6 * 2.6) * 2, Vol(f, id), 9);
        Assert.Empty(box["slabs"]!.AsArray());
        Assert.Null(Boxes.AssemblyOf(f.Model, id));
        // Outside the outline instead.
        Update(f, id, new() { ["justify"] = "outside" });
        Assert.Equal((4.4 * 3.4 - 12) * 2, Vol(f, id), 9);
        Update(f, id, new() { ["justify"] = "inside" });
        // A bottom 0.15 and a top 0.1: two slabs inside the walls, flush with their bottom and top, in an assembly with them.
        Update(f, id, new() { ["bottom"] = true, ["bottomThickness"] = 0.15, ["top"] = true, ["topThickness"] = 0.1 });
        var slabs = Boxes.SlabsOf(f.Model, id).ToList();
        Assert.Equal(2, slabs.Count);
        var bottom = slabs.Single(s => s.Str("side") == "bottom").Id;
        var top = slabs.Single(s => s.Str("side") == "top").Id;
        Assert.Equal("IfcSlab", f.Model.Get(bottom)!.Str("exportAs"));
        Assert.Equal(3.6 * 2.6 * 0.15, Vol(f, bottom), 9);
        Assert.Equal(3.6 * 2.6 * 0.1, Vol(f, top), 9);
        Assert.Equal(0, f.Bounds(bottom).Min.Z, 9);
        Assert.Equal(2, f.Bounds(top).Max.Z, 9);
        Assert.Equal(1.9, f.Bounds(top).Min.Z, 9);
        var asm = Boxes.AssemblyOf(f.Model, id)!;
        Assert.Equal("IfcElementAssembly", asm.Class);
        Assert.Equal([id, bottom, top], f.Model.RelatedOf(asm.Id, IfcClass.RelAggregates).Select(e => e.Id).OrderBy(x => x == id ? 0 : x == bottom ? 1 : 2));
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, bottom)!.Id);
        // The bottom's thickness edited on the box: the slab follows.
        Update(f, id, new() { ["bottomThickness"] = 0.25 });
        Assert.Equal(3.6 * 2.6 * 0.25, Vol(f, bottom), 9);
        // The top off: its slab goes, the assembly keeps the walls and the bottom; the bottom off too: no assembly.
        Update(f, id, new() { ["top"] = false });
        Assert.Null(f.Model.Get(top));
        Assert.Equal(2, f.Model.RelatedOf(asm.Id, IfcClass.RelAggregates).Count());
        Update(f, id, new() { ["bottom"] = false });
        Assert.Empty(Boxes.SlabsOf(f.Model, id));
        Assert.Null(f.Model.Get(asm.Id));
        // Undo brings the bottom and its assembly back; a slab thicker than the box is high is refused.
        f.Session.Undo();
        Assert.Single(Boxes.SlabsOf(f.Model, id));
        Assert.NotNull(Boxes.AssemblyOf(f.Model, id));
        Assert.Throws<CommandException>(() => Update(f, id, new() { ["top"] = true, ["topThickness"] = 1.9 }));
        // Deleting the bottom switches it off on the box; deleting the box takes its slabs and assembly with it.
        f.Run("element.delete", new JsonObject { ["id"] = Boxes.SlabsOf(f.Model, id).Single().Id });
        Assert.False((bool)f.Model.Get(id)!.Attrs["bottom"]!);
        Assert.Null(Boxes.AssemblyOf(f.Model, id));
        Update(f, id, new() { ["top"] = true });
        f.Run("element.delete", new JsonObject { ["id"] = id });
        Assert.Empty(f.Model.OfClass("IfcElementAssembly"));
        Assert.DoesNotContain(f.Model.Entities, e => e.Str("operation") == "boxSlab");
    }

    [Fact]
    public void A_box_on_a_section_stands_on_the_cut_and_follows_the_section_line()
    {
        var f = F();
        // A section along x from (0, 0) to (10, 0), looking +y: a 2 × 2 m outline drawn on it, a box 0.5 deep toward the viewer (−y).
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(0, 0), ["end"] = J(10, 0) })!["id"]!;
        var box = Make(f, "box", Draw(f, section, Rect(1, 0, 3, 2)), new() { ["thickness"] = 0.2, ["height"] = 0.5 });
        Assert.Equal((4 - 1.6 * 1.6) * 0.5, Vol(f, box), 9);
        var b = f.Bounds(box);
        Assert.Equal(1, b.Min.X, 9);
        Assert.Equal(3, b.Max.X, 9);
        Assert.Equal(-0.5, b.Min.Y, 9);
        Assert.Equal(0, b.Max.Y, 9);
        Assert.Equal(0, b.Min.Z, 9);
        Assert.Equal(2, b.Max.Z, 9);
        // The section line moved 4 m north: the box goes with it.
        f.Run("view.setLine", new JsonObject { ["id"] = section, ["start"] = J(0, 4), ["end"] = J(10, 4) });
        Assert.Equal(3.5, f.Bounds(box).Min.Y, 9);
        Assert.Equal(4, f.Bounds(box).Max.Y, 9);
    }

    [Fact]
    public void A_developed_section_lays_a_sketch_on_the_piece_it_is_drawn_on()
    {
        var f = F();
        // A jogged section: 0,0 → 4,0, then 4,0 → 4,6; its second piece starts at station 4 and runs north, looking −x.
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["pts"] = new JsonArray(J(0, 0), J(4, 0), J(4, 6)) })!["id"]!;
        // A wall strip drawn on the second piece, from station 5 to 7 at the height 1: along y from 1 to 3 on x = 4.
        var strip = Make(f, "wall", Draw(f, section, Poly(false, (5, 1), (7, 1))), new() { ["thickness"] = 0.2, ["height"] = 0.3, ["justify"] = "left" });
        var b = f.Bounds(strip);
        Assert.Equal(1, b.Min.Y, 9);
        Assert.Equal(3, b.Max.Y, 9);
        Assert.Equal(1, b.Min.Z, 9);
        Assert.Equal(1.2, b.Max.Z, 9);
        // Toward the viewer, who looks −x: from x = 4 out to 4.3.
        Assert.Equal(4, b.Min.X, 9);
        Assert.Equal(4.3, b.Max.X, 9);
    }

    [Fact]
    public void An_elevation_takes_a_plane_at_the_front_of_the_model_and_its_solids_follow_its_depth()
    {
        var f = F();
        // A wall from (0, 2) to (8, 2): the south elevation looks north at its south face.
        var face = f.Bounds(f.Wall(0, 2, 8, 2)).Min.Y;
        var elevation = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var drawing = f.Session.Query("view.drawing", new JsonObject { ["id"] = elevation });
        var circle = Draw(f, elevation, Circle(0, 1.5, 0.5));
        var ring = Make(f, "ring", circle, new() { ["thickness"] = 0.1, ["height"] = 0.2 });
        var view = f.Model.Get(elevation)!;
        Assert.Equal(face, view.Num("planeDepth"), 9);
        Assert.NotNull(view.NumOrNull("originX"));
        // Standing on the face, 0.2 toward the viewer (−y); its middle where the elevation's x 0 is.
        var b = f.Bounds(ring);
        Assert.Equal(face, b.Max.Y, 9);
        Assert.Equal(face - 0.2, b.Min.Y, 9);
        Assert.Equal(1.5, (b.Min.Z + b.Max.Z) / 2, 6);
        Assert.Equal(view.Num("originX"), (b.Min.X + b.Max.X) / 2, 6);
        Assert.Equal((Disc(0.5) - Disc(0.4)) * 0.2, Vol(f, ring), 9);
        // The plane's depth edited: the ring moves with it; another wall in front does not move the drawing's x.
        Update(f, elevation, new() { ["planeDepth"] = 1 });
        Assert.Equal(1, f.Bounds(ring).Max.Y, 9);
        var x = (f.Bounds(ring).Min.X + f.Bounds(ring).Max.X) / 2;
        f.Wall(20, -5, 30, -5);
        Assert.Equal(x, (f.Bounds(ring).Min.X + f.Bounds(ring).Max.X) / 2, 9);
        Assert.NotNull(drawing);
    }

    [Fact]
    public void A_tilted_reference_plane_carries_its_solids_when_it_moves_and_turns()
    {
        var f = F();
        // Through (0,0,0), (1,0,0) and (0,1,1): a plane tilted 45° about x, facing (0, −1, 1)/√2.
        var plane = (string)f.Run("view.create", new JsonObject { ["kind"] = "plane", ["points"] = new JsonArray(J3(0, 0, 0), J3(1, 0, 0), J3(0, 1, 1)) })!["id"]!;
        var v = f.Model.Get(plane)!;
        Assert.Equal("plane", v.Str("kind"));
        Assert.Equal(45, v.Num("tilt"), 9);
        Assert.Equal(-90, v.Num("bearing"), 9);
        var p = RefPlanes.Of(v);
        Assert.Equal(1, p.Right.X, 9);
        Assert.Equal(1 / Math.Sqrt(2), p.Up.Y, 9);
        Assert.Equal(1 / Math.Sqrt(2), p.Up.Z, 9);
        // A 2 × 1 box on it, 0.1 thick, 0.3 high along its normal: the same volume as on a level, every corner within 0..0.3 of the plane.
        var box = Make(f, "box", Draw(f, plane, Rect(0, 0, 2, 1)), new() { ["thickness"] = 0.1, ["height"] = 0.3 });
        Assert.Equal((2 - 1.8 * 0.8) * 0.3, Vol(f, box), 9);
        double Lo() => f.Session.Geometry(box)!.Parts[0].Solid.Vertices.Min(q => RefPlanes.Of(f.Model.Get(plane)!).Local(q).Off);
        double Hi() => f.Session.Geometry(box)!.Parts[0].Solid.Vertices.Max(q => RefPlanes.Of(f.Model.Get(plane)!).Local(q).Off);
        Assert.Equal(0, Lo(), 9);
        Assert.Equal(0.3, Hi(), 9);
        var n = p.Normal;
        double Along(Vec3 dir) => f.Session.Geometry(box)!.Parts[0].Solid.Vertices.Min(q => Vec3.Dot(q, dir));
        var before = Along(n);
        // Moved 1 m along its normal: the box goes 1 m that way.
        f.Run("view.movePlane", new JsonObject { ["id"] = plane, ["along"] = 1 });
        Assert.Equal(before + 1, Along(n), 9);
        // Raised by its z in the properties panel: the box rises.
        var (z0, oz) = (f.Bounds(box).Min.Z, RefPlanes.Of(f.Model.Get(plane)!).Origin.Z);
        Update(f, plane, new() { ["z"] = 2 });
        Assert.Equal(z0 + 2 - oz, f.Bounds(box).Min.Z, 9);
        // Turned 90° about the vertical through its origin: the box now runs along y.
        f.Run("view.movePlane", new JsonObject { ["id"] = plane, ["turn"] = 90 });
        var b = f.Bounds(box);
        Assert.Equal(2, b.Max.Y - b.Min.Y, 6);
        Assert.Equal((2 - 1.8 * 0.8) * 0.3, Vol(f, box), 9);
        // Its own drawing: a section on the plane.
        var r = Views.Render(f.Session.Reader, f.Model.Get(plane)!);
        Assert.Null(r.Problem);
        Assert.Equal("section", r.Kind);
    }

    [Fact]
    public void A_reference_plane_on_a_face_or_off_a_view_and_three_points_on_a_line_refused()
    {
        var f = F();
        var plan = Plan(f);
        // On a wall's south face (picked at (2, −0.125, 1) with its normal −y): standing, facing south, x along +x.
        var face = (string)f.Run("view.create", new JsonObject { ["kind"] = "plane", ["point"] = J3(2, -0.125, 1), ["normal"] = J3(0, -1, 0) })!["id"]!;
        var p = RefPlanes.Of(f.Model.Get(face)!);
        Assert.Equal(-1, p.Normal.Y, 9);
        Assert.Equal(1, p.Right.X, 9);
        Assert.Equal(1, p.Up.Z, 9);
        // A plane 1.2 m over the plan: level, facing up, at its storey's level + 1.2.
        var over = (string)f.Run("view.create", new JsonObject { ["kind"] = "plane", ["fromView"] = plan, ["offset"] = 1.2 })!["id"]!;
        var q = RefPlanes.Of(f.Model.Get(over)!);
        Assert.Equal(1, q.Normal.Z, 9);
        Assert.Equal(1.2, q.Origin.Z, 9);
        Assert.Equal(1, q.Right.X, 9);
        Assert.Equal(1, q.Up.Y, 9);
        // An extrusion on it stands on that level.
        var solid = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = Draw(f, over, Rect(0, 0, 1, 1)), ["depth"] = 0.5 })!["id"]!;
        Assert.Equal(1.2, f.Bounds(solid).Min.Z, 9);
        Assert.Throws<CommandException>(() => f.Run("view.create", new JsonObject { ["kind"] = "plane", ["points"] = new JsonArray(J3(0, 0, 0), J3(1, 0, 0), J3(2, 0, 0)) }));
    }

    [Fact]
    public void Native_walls_along_a_closed_polyline_join_at_its_corners_and_along_an_arc_are_curved()
    {
        var f = F();
        var type = f.Type("Brick 30", ("Brick", 0.3, true));
        // A 6 × 4 outline, the walls inside it: four walls, their outer faces on the outline, joined at the four corners.
        var r = (JsonObject)f.Run("architecture.wallsAlong", new JsonObject { ["shape"] = Rect(0, 0, 6, 4), ["storeyId"] = f.Storey, ["typeId"] = type, ["justify"] = "inside", ["height"] = 2.8 })!;
        var walls = r["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(4, walls.Count);
        Assert.All(walls, w => Assert.Equal(type, f.Model.RelatingOf(w, IfcClass.RelDefinesByType)!.Id));
        var joins = f.Model.Relations.Where(x => x.Class == IfcClass.RelConnectsPathElements).ToList();
        Assert.Equal(4, joins.Count);
        var bs = walls.Select(f.Bounds).ToList();
        Assert.Equal(0, bs.Min(b => b.Min.X), 9);
        Assert.Equal(6, bs.Max(b => b.Max.X), 9);
        Assert.Equal(0, bs.Min(b => b.Min.Y), 9);
        Assert.Equal(4, bs.Max(b => b.Max.Y), 9);
        Assert.Equal(2.8, bs.Max(b => b.Max.Z), 9);
        // Mitred corners, nothing doubled: the walls hold the outline less its inside, 2.8 high.
        Assert.Equal((24 - 5.4 * 3.4) * 2.8, walls.Sum(w => Vol(f, w)), 6);
        // Drawn clockwise, outside: the same loop of walls round the outline's outside.
        var g = F();
        var t2 = g.Type("Brick 30", ("Brick", 0.3, true));
        var cw = Poly(true, (0, 0), (0, 4), (6, 4), (6, 0));
        var outside = g.Run("architecture.wallsAlong", new JsonObject { ["shape"] = cw, ["storeyId"] = g.Storey, ["typeId"] = t2, ["justify"] = "outside", ["height"] = 3, ["bottom"] = true })!;
        var ow = outside["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal((6.6 * 4.6 - 24) * 3, ow.Sum(w => Vol(g, w)), 6);
        Assert.Single(outside["slabs"]!.AsArray());
        Assert.Equal(IfcClass.Slab, g.Model.Get((string)outside["slabs"]![0]!)!.Class);
        // A quarter arc of radius 5, centred: one curved wall from (5, 0) to (0, 5), bulging out (to the right of its chord).
        var h = F();
        var arc = h.Run("architecture.wallsAlong", new JsonObject { ["shape"] = Arc(0, 0, 5, 0, Math.PI / 2), ["storeyId"] = h.Storey })!;
        var aw = h.Model.Get((string)arc["ids"]![0]!)!;
        Assert.Single(arc["ids"]!.AsArray());
        Assert.True(aw.Num("arc") < 0);
        // Centred on the arc by the type's whole thickness (its axis on the core may sit off the middle).
        var rr = Walls.Start(aw).Length;
        Assert.InRange(rr, 4.8, 5.2);
        Assert.Equal(rr * (1 - Math.Cos(Math.PI / 4)), -aw.Num("arc"), 9);
        Assert.Equal(0, Walls.Start(aw).Y, 9);
        Assert.Equal(0, Walls.End(aw).X, 9);
        // A circle: four curved walls joined end to end.
        var c = h.Run("architecture.wallsAlong", new JsonObject { ["shape"] = Circle(20, 0, 3), ["storeyId"] = h.Storey })!;
        var cs = c["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(4, cs.Count);
        Assert.All(cs, w => Assert.True(h.Model.Get(w)!.Num("arc") < 0));
        Assert.Equal(4, h.Model.Relations.Count(x => x.Class == IfcClass.RelConnectsPathElements && cs.Contains(x.Relating)));
    }

    [Fact]
    public void Thick_solids_export_as_walls_and_a_box_as_an_assembly_of_its_walls_and_slabs()
    {
        var f = F();
        var plan = Plan(f);
        Make(f, "wall", Draw(f, plan, Poly(false, (0, 10), (5, 10))), new() { ["thickness"] = 0.2, ["height"] = 1 });
        var box = Make(f, "box", Draw(f, plan, Rect(0, 0, 2, 2)), new() { ["thickness"] = 0.1, ["height"] = 1, ["bottom"] = true, ["top"] = true });
        var asm = Boxes.AssemblyOf(f.Model, box)!;
        var dir = Directory.CreateTempSubdirectory("lapin-thick").FullName;
        try
        {
            var path = Path.Combine(dir, "t.ifc");
            IfcExport.Write(f.Session, path);
            var lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Count(l => l.Contains("=IFCWALL(")));
            Assert.Equal(2, lines.Count(l => l.Contains("=IFCSLAB(")));
            var assembly = lines.Single(l => l.Contains("=IFCELEMENTASSEMBLY("));
            Assert.Contains("'Box'", assembly);
            var asmRef = assembly[..assembly.IndexOf('=')];
            var agg = lines.Single(l => l.Contains("=IFCRELAGGREGATES(") && l.Contains("," + asmRef + ",("));
            Assert.Equal(3, agg[(agg.LastIndexOf('(') + 1)..].Split(',').Length);
            // The parts are in the assembly, the assembly in the storey; the box's parts are not contained twice.
            var contains = lines.Single(l => l.Contains("=IFCRELCONTAINEDINSPATIALSTRUCTURE("));
            var list = contains[(contains.IndexOf(",(") + 2)..contains.IndexOf(')', contains.IndexOf(",("))].Split(',');
            Assert.Contains(asmRef, list);
            var partRefs = agg[(agg.LastIndexOf('(') + 1)..agg.LastIndexOf(')')].Split(',');
            Assert.All(partRefs, pr => Assert.DoesNotContain(pr, list));
            Assert.Contains(asm.Id, File.ReadAllText(path));
        }
        finally { Directory.Delete(dir, true); }
    }
}
