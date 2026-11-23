using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Floor slopes and falls (docs/plan-floor-slopes.md, site grading T7, forum topic 157 / issue #96): the slope engine
/// (<see cref="TopSurface"/>) on a floor's top; bent at constant thickness or by a sculpted layer with its minimum; edge slopes
/// with their limits, points, lines, drains (a point, a grate, a channel), regions that step, a room's floor; quantities, plan
/// marks, IFC, undo, split and moves; a floor without slopes exactly as before.
/// </summary>
public sealed class FloorSlopeTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());
    static JsonArray P(double x, double y) => new(x, y);
    static JsonArray Rect(double x0, double y0, double x1, double y1) => new(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1));

    /// <summary>A floor type: tiles 10, screed (thick) and a concrete core.</summary>
    static string Type(Fixture f, double screed = 0.06, double core = 0.20) => (string)f.Run("architecture.createSlabType", new JsonObject
    {
        ["name"] = "Tiled floor",
        ["layers"] = new JsonArray(
            new JsonObject { ["material"] = "Ceramic tiles", ["thickness"] = 0.01 },
            new JsonObject { ["material"] = "Screed", ["thickness"] = screed },
            new JsonObject { ["material"] = "Concrete", ["thickness"] = core, ["core"] = true }),
    })!["id"]!;

    static string Floor(Fixture f, JsonArray pts, string type, double offset = 0) =>
        (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts, ["typeId"] = type, ["offset"] = offset })!["id"]!;

    static double Vol(Fixture f, string id, string? key = null) => f.Session.Geometry(id)!.Parts.Where(p => key == null || p.Key == key).Sum(p => Measure.Volume(p.Solid));

    static double Q(Fixture f, string id, string set, string name, string? part = null) =>
        f.Session.Quantities(id).First(s => s.Name == set && s.Part == part).Quantities.First(q => q.Name == name).Value;

    /// <summary>The shaped top's height above the floor's reference at a point.</summary>
    static double TopAt(Fixture f, string id, double x, double y)
    {
        var b = FloorSlopes.Solve(f.Model, f.Model.Get(id)!, f.Session.Catalog)!;
        foreach (var p in b.Pieces)
            if (p.Piece.Mesh.Locate(new Vec2(x, y)) is var (t, u, v, w))
            {
                var (a, bb, c) = p.Piece.Mesh.Triangles[t];
                return u * p.H(a) + v * p.H(bb) + w * p.H(c);
            }
        throw new InvalidOperationException("Not on the floor.");
    }

    static JsonNode? Run(Fixture f, string cmd, JsonObject a) => f.Run("architecture." + cmd, a);

    [Fact]
    public void Bent_one_percent_from_an_edge_moves_every_layer_at_its_thickness()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 4, 5), Type(f));
        Run(f, "setFloorSlope", new JsonObject { ["id"] = id, ["how"] = "bend" });
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 0, ["grade"] = "1%", ["fall"] = "in" });
        Assert.Equal(-0.05, TopAt(f, id, 2, 5), 9);
        Assert.Equal(-0.025, TopAt(f, id, 1, 2.5), 9);
        Assert.Equal(0.2, Vol(f, id, "layer:0"), 6);
        Assert.Equal(1.2, Vol(f, id, "layer:1"), 6);
        Assert.Equal(4.0, Vol(f, id, "layer:2"), 6);
        Assert.Equal(5.4, Q(f, id, "Qto_SlabBaseQuantities", "GrossVolume"), 6);
        Assert.Equal(20 * Math.Sqrt(1 + 0.01 * 0.01), Q(f, id, "Lapin_SlabSlopeQuantities", "SurfaceArea"), 6);
        Assert.Equal(0.05, Q(f, id, "Lapin_SlabSlopeQuantities", "Fall"), 9);
        Assert.Equal(0.01, Q(f, id, "Lapin_SlabSlopeQuantities", "Slope"), 9);
        // The concrete's bottom follows: at y = 5 it is 50 mm lower than at y = 0.
        var core = f.Session.Geometry(id)!.Parts.Single(p => p.Key == "layer:2").Solid;
        var bounds = Measure.Bounds(core)!.Value;
        Assert.Equal(-0.27 - 0.05, bounds.Min.Z, 9);
        Assert.Equal(-0.07, bounds.Max.Z, 9);
    }

    [Fact]
    public void A_sculpted_screed_takes_the_fall_in_its_thickness_and_the_rest_stays_level()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 4, 5), Type(f));
        Run(f, "setFloorSlope", new JsonObject { ["id"] = id, ["how"] = "layer", ["minThickness"] = 0.03 });
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 0, ["grade"] = 0.005, ["fall"] = "in" });
        // Screed 60 → 35 mm, average 47.5 mm.
        Assert.Equal(0.95, Vol(f, id, "layer:1"), 6);
        Assert.Equal(4.0, Vol(f, id, "layer:2"), 6);
        Assert.Equal(0.2, Vol(f, id, "layer:0"), 6);
        Assert.Equal(0.0475, Q(f, id, "Lapin_SlabLayerThickness", "AverageThickness", "layer:1"), 9);
        Assert.Equal(0.035, Q(f, id, "Lapin_SlabLayerThickness", "MinThickness", "layer:1"), 9);
        Assert.Equal(0.06, Q(f, id, "Lapin_SlabLayerThickness", "MaxThickness", "layer:1"), 9);
        Assert.Equal(0.2, Q(f, id, "Lapin_SlabLayerThickness", "AverageThickness", "layer:2"), 9);
        // The core's top is level.
        var core = Measure.Bounds(f.Session.Geometry(id)!.Parts.Single(p => p.Key == "layer:2").Solid)!.Value;
        Assert.Equal(-0.07, core.Max.Z, 9);
        Assert.Equal(-0.27, core.Min.Z, 9);
        Assert.Null(f.Session.Geometry(id)!.Diagnostics);
    }

    [Fact]
    public void A_screed_is_held_at_its_minimum_and_the_floor_says_over_how_much()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 4, 5), Type(f));
        Run(f, "setFloorSlope", new JsonObject { ["id"] = id, ["minThickness"] = 0.03 });
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 0, ["grade"] = "1 %", ["fall"] = "in" });
        // The screed reaches 30 mm at y = 3 m: 4 · 3 · 0.045 + 4 · 2 · 0.030.
        Assert.Equal(0.78, Vol(f, id, "layer:1"), 6);
        Assert.Equal(-0.03, TopAt(f, id, 2, 5), 9);
        var d = f.Session.Geometry(id)!.Diagnostics!;
        Assert.Contains(d, x => x.Contains("held at its 30 mm minimum over 8.0 m²"));
    }

    [Fact]
    public void An_edge_slope_stops_at_its_limit_an_offset_or_a_distance_in_plan_or_along_the_slope()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 6, 4), Type(f, 0.08));
        Run(f, "setFloorSlope", new JsonObject { ["id"] = id, ["how"] = "bend" });
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 0, ["grade"] = "2%", ["until"] = new JsonObject { ["offset"] = -0.03 } });
        // The plateau begins 1.5 m in.
        Assert.Equal(-0.02, TopAt(f, id, 3, 1), 9);
        Assert.Equal(-0.03, TopAt(f, id, 3, 1.5), 9);
        Assert.Equal(-0.03, TopAt(f, id, 3, 3.5), 9);
        // 1.5 m along the slope is 1.5 · cos(atan 0.02) in plan.
        Run(f, "updateFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["index"] = 0, ["until"] = new JsonObject { ["distance"] = 1.5, ["along"] = "slope" } });
        var plan = 1.5 * Math.Cos(Math.Atan(0.02));
        Assert.Equal(1.49970, plan, 5);
        Assert.Equal(-0.02 * plan, TopAt(f, id, 3, 3), 9);
        // Two edges falling inward meet in a valley; the edge itself keeps its height.
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 2, ["grade"] = "2%" });
        Run(f, "updateFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["index"] = 0, ["until"] = null });
        Assert.Equal(-0.04, TopAt(f, id, 3, 2), 9);
        Assert.Equal(0, TopAt(f, id, 3, 4), 9);
        Assert.Equal(0, TopAt(f, id, 3, 0), 9);
        // A rise towards an edge (it falls out, to a gutter): the edge is the low line.
        Run(f, "removeFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["index"] = 1 });
        Run(f, "updateFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["index"] = 0, ["fall"] = "out", ["offset"] = -0.06 });
        Assert.Equal(-0.06, TopAt(f, id, 3, 0), 9);
        Assert.Equal(-0.06 + 0.08, TopAt(f, id, 3, 4), 9);
    }

    [Fact]
    public void A_plane_by_an_arrow_or_by_the_corners_heights()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 4, 5), Type(f));
        Run(f, "setFloorSlope", new JsonObject { ["id"] = id, ["how"] = "bend", ["arrow"] = new JsonObject { ["from"] = P(0, 0), ["to"] = P(4, 0), ["fromZ"] = 0, ["toZ"] = -0.04 } });
        Assert.Equal(-0.04, TopAt(f, id, 4, 3), 9);
        Assert.Equal(-0.02, TopAt(f, id, 2, 5), 9);
        Run(f, "setFloorSlope", new JsonObject { ["id"] = id, ["cornerZ"] = new JsonArray(0.0, -0.04, -0.06, -0.02) });
        Assert.Equal(-0.03, TopAt(f, id, 2, 2.5), 9);
        Assert.Equal(-0.06, TopAt(f, id, 4, 5), 9);
        Assert.Equal(4.0, Vol(f, id, "layer:2"), 6);
    }

    [Fact]
    public void Wrong_slopes_are_refused_with_the_reason()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 4, 5), Type(f));
        Assert.Contains("more than 0", Assert.Throws<CommandException>(() => Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 0, ["grade"] = 0 })).Message);
        Assert.Contains("Which edge", Assert.Throws<CommandException>(() => Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 7, ["grade"] = 0.01 })).Message);
        Assert.Contains("outside the floor", Assert.Throws<CommandException>(() => Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["at"] = P(9, 9) })).Message);
        Assert.Contains("no such layer", Assert.Throws<CommandException>(() => Run(f, "setFloorSlope", new JsonObject { ["id"] = id, ["layer"] = 9 })).Message);
        Assert.Contains("200 mm", Assert.Throws<CommandException>(() => Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["at"] = P(1, 1), ["depth"] = 0 })).Message);
        Assert.Null(f.Model.Get(id)!.Attrs[FloorSlopes.Key]);
    }

    [Fact]
    public void A_drain_with_the_outline_held_makes_four_falls_and_a_channel_two()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 2, 2), Type(f, 0.05, 0.15));
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["at"] = P(1, 1), ["depth"] = 0.02 });
        // The screed sculpted by default (named so, above the core): a pyramid 4 · 0.02 / 3 taken out of it.
        Assert.Equal(0.2 - 4 * 0.02 / 3, Vol(f, id, "layer:1"), 6);
        Assert.Equal(0.6, Vol(f, id, "layer:2"), 6);
        Assert.Equal(-0.02, TopAt(f, id, 1, 1), 9);
        Assert.Equal(-0.01, TopAt(f, id, 0.5, 1), 9);
        Assert.Equal(0, TopAt(f, id, 0, 2), 9);
        Assert.Equal((0.2 - 4 * 0.02 / 3) / 4, Q(f, id, "Lapin_SlabLayerThickness", "AverageThickness", "layer:1"), 9);
        Assert.Null(f.Session.Geometry(id)!.Diagnostics);
        // A channel across the middle instead: two planes, 4 · 0.02 / 2.
        Run(f, "removeFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["index"] = 0 });
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["at"] = P(1, 0), ["to"] = P(1, 2), ["depth"] = 0.02 });
        Assert.Equal(0.2 - 0.04, Vol(f, id, "layer:1"), 6);
        Assert.Equal(-0.02, TopAt(f, id, 1, 0.3), 9);
        // A grate 150 mm across at the drain's depth: more taken than the point's pyramid.
        Run(f, "removeFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["index"] = 0 });
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["at"] = P(1, 1), ["depth"] = 0.02, ["grate"] = 0.15 });
        var withGrate = Vol(f, id, "layer:1");
        Assert.True(withGrate < 0.2 - 4 * 0.02 / 3 - 1e-6);
        Assert.Equal(-0.02, TopAt(f, id, 1.05, 0.97), 9);
    }

    [Fact]
    public void Points_lines_and_a_plane_made_by_the_mesh_are_exact()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 4, 4), Type(f));
        Run(f, "setFloorSlope", new JsonObject { ["id"] = id, ["how"] = "bend" });
        // A ridge line at +10 mm across the middle: the surface passes through it everywhere along it.
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "line", ["a"] = P(0.5, 2), ["b"] = P(3.5, 2), ["offset"] = new JsonArray(0.01, 0.01) });
        Assert.Equal(0.01, TopAt(f, id, 2, 2), 9);
        Assert.Equal(0.01, TopAt(f, id, 1.234, 2), 9);
        // A point at an absolute level stays there when the floor moves up; one at an offset goes with it.
        var top = Slabs.Top(f.Model, f.Model.Get(id)!);
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "point", ["at"] = P(1, 1), ["level"] = top - 0.015 });
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "point", ["at"] = P(3, 3), ["offset"] = -0.01 });
        Assert.Equal(-0.015, TopAt(f, id, 1, 1), 9);
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = id, ["offset"] = 0.1 });
        Assert.Equal(-0.115, TopAt(f, id, 1, 1), 9);
        Assert.Equal(-0.01, TopAt(f, id, 3, 3), 9);
    }

    [Fact]
    public void A_region_steps_from_the_rest_and_a_room_makes_its_own_falling_to_a_drain_at_its_middle()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 6, 4), Type(f));
        var r = (int)Run(f, "addFloorSlopeRegion", new JsonObject { ["id"] = id, ["points"] = Rect(1, 1, 3, 3), ["elevation"] = -0.02 })!["region"]!;
        Assert.Equal(0, r);
        // Sculpted: the screed is 20 mm thinner over the region's 4 m².
        Assert.Equal(24 * 0.06 - 4 * 0.02, Vol(f, id, "layer:1"), 6);
        Assert.Equal(-0.02, TopAt(f, id, 2, 2), 9);
        Assert.Equal(0, TopAt(f, id, 5, 2), 9);
        // A region across the outline is refused: that is a split.
        Assert.Contains("split the floor", Assert.Throws<CommandException>(() => Run(f, "addFloorSlopeRegion", new JsonObject { ["id"] = id, ["points"] = Rect(5, 1, 7, 3) })).Message);
        // A room's floor: a region on its outline with a drain at its middle, following the room.
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 3.5, ["y1"] = 0.5, ["x2"] = 5.5, ["y2"] = 3.5, ["name"] = "Bath", ["function"] = "Bath" })!["id"]!;
        var made = Run(f, "slopeRoom", new JsonObject { ["roomId"] = room, ["drain"] = new JsonObject { ["depth"] = 0.015 } })!;
        Assert.Equal(id, (string)made["id"]!);
        Assert.Equal(1, (int)made["region"]!);
        Assert.Equal(-0.015, TopAt(f, id, 4.5, 2), 9);
        Assert.Equal(0, TopAt(f, id, 3.5, 2), 9);
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(room), ["op"] = "move", ["by"] = P(0, -0.25) });
        Assert.Equal(-0.015, TopAt(f, id, 4.5, 1.75), 9);
    }

    [Fact]
    public void A_room_that_is_the_whole_floor_shapes_the_floor_itself()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 2, 3), Type(f));
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 2, ["y2"] = 3, ["name"] = "Bath" })!["id"]!;
        var made = Run(f, "slopeRoom", new JsonObject { ["roomId"] = room })!;
        Assert.Null(made["region"]);
        Assert.Equal(-0.02, TopAt(f, id, 1, 1.5), 9);
    }

    [Fact]
    public void The_plan_shows_the_falls_their_grades_and_the_drain_s_level()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 4, 5), Type(f));
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 0, ["grade"] = "1.5%" });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        var texts = Views.Render(new Ctx(f), Views.Find(f.Model, plan)!).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        Assert.Contains("1.5 %", texts);
        var id2 = Floor(f, Rect(10, 0, 12, 2), Type(f));
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id2, ["kind"] = "drain", ["at"] = P(11, 1), ["depth"] = 0.02 });
        texts = Views.Render(new Ctx(f), Views.Find(f.Model, plan)!).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        Assert.Contains("2.0 %", texts);
        Assert.Contains(Standards.FormatLevel(Slabs.Top(f.Model, f.Model.Get(id2)!) - 0.02), texts);
    }

    [Fact]
    public void A_room_s_floor_marks_follow_the_room_where_its_walls_now_put_it()
    {
        // The bathroom's region and drain in plan are the room's live outline, as its floor's body is: not the outline the room
        // stored when it was placed (they were drawn where the bathroom had been, over another floor, once its walls moved).
        var f = F();
        string Wall(double x1, double y1, double x2, double y2) => (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 })!["id"]!;
        foreach (var (x1, y1, x2, y2) in new[] { (0.0, 0.0, 5.0, 0.0), (5, 0, 5, 4), (5, 4, 0, 4), (0, 4, 0, 0) }) Wall(x1, y1, x2, y2);
        var partition = Wall(2.6, 0, 2.6, 4);
        var id = Floor(f, Rect(-0.15, -0.15, 5.15, 4.15), Type(f));
        var bath = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 1.3, ["y"] = 2, ["name"] = "Bath", ["function"] = "Bath" })!["id"]!;
        Run(f, "slopeRoom", new JsonObject { ["roomId"] = bath });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        double Reach() => Views.Render(new Ctx(f), Views.Find(f.Model, plan)!).Pens.Where(p => p.Layer == FloorSlopes.Layer)
            .SelectMany(p => p.Segments.SelectMany(s => new[] { s.A, s.B }).Concat(p.Loops.SelectMany(l => l))).Max(v => v.X);
        // The region's outline and the drain inside the bathroom (its walls 0.2 m thick round the partition at 2.6).
        Assert.InRange(Reach(), 2.3, 2.6);
        // The partition moved 1 m east: the bathroom is wider, its floor's region and its marks with it.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = partition, ["x1"] = 3.6, ["x2"] = 3.6 });
        Assert.Equal(-0.02, TopAt(f, id, 1.8, 2), 9);
        Assert.InRange(Reach(), 3.3, 3.6);
    }

    sealed class Ctx(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    [Fact]
    public void Each_command_is_one_undo_step()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 4, 5), Type(f));
        var before = f.Model.Get(id)!.Attrs.ToJsonString();
        var flat = Vol(f, id, "layer:1");
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["at"] = P(2, 2) });
        var one = f.Model.Get(id)!.Attrs.ToJsonString();
        Run(f, "setFloorSlope", new JsonObject { ["id"] = id, ["how"] = "bend" });
        f.Session.Undo();
        Assert.Equal(one, f.Model.Get(id)!.Attrs.ToJsonString());
        f.Session.Undo();
        Assert.Equal(before, f.Model.Get(id)!.Attrs.ToJsonString());
        Assert.Equal(flat, Vol(f, id, "layer:1"), 9);
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["at"] = P(2, 2) });
        Run(f, "resetFloorSlope", new JsonObject { ["id"] = id });
        Assert.Equal(before, f.Model.Get(id)!.Attrs.ToJsonString());
    }

    [Fact]
    public void A_floor_without_slopes_is_built_exactly_as_before()
    {
        var f = F();
        var type = Type(f);
        var a = Floor(f, Rect(0, 0, 4, 5), type);
        var b = Floor(f, Rect(0, 0, 4, 5), type);
        Run(f, "addFloorSlope", new JsonObject { ["id"] = b, ["kind"] = "drain", ["at"] = P(2, 2) });
        Run(f, "resetFloorSlope", new JsonObject { ["id"] = b });
        var (ga, gb) = (f.Session.Geometry(a)!, f.Session.Geometry(b)!);
        Assert.Equal(ga.Parts.Count, gb.Parts.Count);
        for (var i = 0; i < ga.Parts.Count; i++)
        {
            Assert.Equal(ga.Parts[i].Key, gb.Parts[i].Key);
            Assert.Equal(ga.Parts[i].Solid.Vertices, gb.Parts[i].Solid.Vertices);
            Assert.Equal(ga.Parts[i].Solid.Faces.Length, gb.Parts[i].Solid.Faces.Length);
        }
        Assert.DoesNotContain(f.Session.Quantities(a), s => s.Name is "Lapin_SlabSlopeQuantities" or "Lapin_SlabLayerThickness");
        Assert.Equal(20 * 0.27, Q(f, a, "Qto_SlabBaseQuantities", "GrossVolume"), 9);
    }

    [Fact]
    public void Split_shares_the_slopes_out_and_the_parts_meet_as_before()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 4, 5), Type(f));
        Run(f, "setFloorSlope", new JsonObject { ["id"] = id, ["how"] = "bend" });
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 0, ["grade"] = "1%" });
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["at"] = P(3, 3), ["depth"] = 0.01 });
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "line", ["a"] = P(0.5, 4), ["b"] = P(3.5, 4), ["offset"] = new JsonArray(-0.04, -0.01) });
        var ids = f.Run("architecture.splitOutline", new JsonObject { ["id"] = id, ["a"] = P(2, -1), ["b"] = P(2, 6), ["keep"] = P(1, 1) })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(2, ids.Count);
        var (left, right) = (f.Model.Get(ids[0])!, f.Model.Get(ids[1])!);
        var ls = (JsonObject)left.Attrs[FloorSlopes.Key]!;
        var rs = (JsonObject)right.Attrs[FloorSlopes.Key]!;
        Assert.Single(ls["edges"]!.AsArray());
        Assert.Single(rs["edges"]!.AsArray());
        Assert.Null(ls["drains"]?.AsArray().FirstOrDefault());
        Assert.Single(rs["drains"]!.AsArray());
        // The line cut at x = 2, its height there kept on both.
        Assert.Equal(-0.025, TopAt(f, ids[0], 2, 4), 9);
        Assert.Equal(-0.025, TopAt(f, ids[1], 2, 4), 9);
        Assert.Equal(-0.05, TopAt(f, ids[0], 1, 5), 9);
        Assert.Equal(-0.05, TopAt(f, ids[1], 3.9, 5), 9);
    }

    [Fact]
    public void Moved_and_mirrored_the_slopes_go_with_the_floor()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 4, 2), Type(f));
        Run(f, "setFloorSlope", new JsonObject { ["id"] = id, ["how"] = "bend" });
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 1, ["grade"] = "1%" });
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["at"] = P(1, 1), ["depth"] = 0.01 });
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = P(10, 0) });
        Assert.Equal(-0.03 - 0.01, TopAt(f, id, 11, 1), 9);
        Assert.Equal(0, TopAt(f, id, 14, 1), 9);
        // Mirrored about x = 12: the slope's edge (the east one) is now the west one.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "mirror", ["a"] = P(12, -5), ["b"] = P(12, 5) });
        Assert.Equal(0, TopAt(f, id, 10, 1), 9);
        Assert.Equal(-0.04, TopAt(f, id, 14, 0.5), 9);
        Assert.Equal(-0.03 - 0.01, TopAt(f, id, 13, 1), 9);
    }

    [Fact]
    public void Ifc_writes_the_shaped_bodies_and_reads_the_slopes_back()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 2, 2), Type(f, 0.05, 0.15));
        Run(f, "addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["at"] = P(1, 1), ["depth"] = 0.02 });
        var screed = Vol(f, id, "layer:1");
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "bath.ifc", new IfcExportOptions());
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("IFCMATERIALLAYERSETUSAGE(", text);
        Assert.Contains("Lapin_Intent", text);
        Assert.DoesNotContain("'Pset_Lapin", text);
        var g = F();
        IfcImport.Run(g.Session, ms.ToArray(), "bath.ifc", null, out _);
        var back = g.Model.Get(id)!;
        Assert.Equal(f.Model.Get(id)!.Attrs[FloorSlopes.Key]!.ToJsonString(), back.Attrs[FloorSlopes.Key]!.ToJsonString());
        Assert.Equal(screed, g.Session.Geometry(id)!.Parts.Where(p => p.Key == "layer:1").Sum(p => Measure.Volume(p.Solid)), 6);
    }

    [Fact]
    public void The_slope_engine_s_mesh_takes_points_segments_and_creases_exactly()
    {
        var m = PlanMesh.Of([new(0, 0), new(2, 0), new(2, 2), new(0, 2)], null)!;
        Assert.Equal(4, m.Area, 12);
        var k = m.Insert(new Vec2(1, 1));
        Assert.True(k >= 0);
        Assert.Equal(4, m.Triangles.Count);
        Assert.True(m.InsertSegment(new Vec2(0.2, 0.5), new Vec2(1.8, 0.5)));
        Assert.Equal(4, m.Area, 12);
        // Every piece of the segment is an edge.
        var edges = m.Triangles.SelectMany(t => new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A) }).ToList();
        var on = edges.Where(e => Math.Abs(m.Points[e.Item1].Y - 0.5) < 1e-12 && Math.Abs(m.Points[e.Item2].Y - 0.5) < 1e-12).Sum(e => (m.Points[e.Item2] - m.Points[e.Item1]).Length);
        Assert.Equal(1.6 * 2, on, 9);
        // Split along x = 1.3: no triangle crosses it after.
        m.Split(i => m.Points[i].X - 1.3);
        Assert.All(m.Triangles, t =>
        {
            var xs = new[] { m.Points[t.A].X, m.Points[t.B].X, m.Points[t.C].X };
            Assert.False(xs.Min() < 1.3 - 1e-9 && xs.Max() > 1.3 + 1e-9);
        });
        Assert.Equal(4, m.Area, 12);
    }
}
