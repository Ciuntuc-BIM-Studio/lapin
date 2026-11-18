using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The Floor tool's "Trace building" (forum topic 158): one floor for the whole storey, on the exterior face of the core of the
/// walls that make the building, leaving out finishes and insulation, stubs, fences and free walls; refused, naming the gap,
/// when the walls do not close.
/// </summary>
public sealed class BuildingTraceTests
{
    /// <summary>Insulation 100, brick 250 (the core), plaster 15: the core's exterior face 125 mm off the line drawn, the wall's 225.</summary>
    static string Insulated(Fixture f) => f.Type("Insulated brick", ("Insulation", 0.10, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));

    static JsonNode Outline(Fixture f, JsonArray? at = null) =>
        f.Session.Query("architecture.buildingOutline", new JsonObject { ["storeyId"] = f.Storey, ["at"] = at })!;

    static List<Vec2> Points(JsonNode o) => o["points"]!.AsArray().Select(p => new Vec2((double)p![0]!, (double)p[1]!)).ToList();

    static void Corners(IEnumerable<(double X, double Y)> expected, List<Vec2> actual)
    {
        var want = expected.ToList();
        Assert.Equal(want.Count, actual.Count);
        foreach (var (x, y) in want)
            Assert.Contains(actual, p => Math.Abs(p.X - x) < 1e-6 && Math.Abs(p.Y - y) < 1e-6);
    }

    /// <summary>A rectangle 10 × 8 drawn on the cores' centre lines, anticlockwise.</summary>
    static List<string> Rectangle(Fixture f, string type) =>
        [f.Wall(0, 0, 10, 0, type), f.Wall(10, 0, 10, 8, type), f.Wall(10, 8, 0, 8, type), f.Wall(0, 8, 0, 0, type)];

    [Fact]
    public void A_rectangle_with_a_stub_and_a_free_wall_traces_the_rectangles_outer_core_face_only()
    {
        var f = new Fixture();
        var type = Insulated(f);
        var shell = Rectangle(f, type);
        var stub = f.Wall(10, 4, 11, 4, type);
        var fence = f.Wall(14, 0, 14, 5, type);
        var o = Outline(f);
        Corners([(-0.125, -0.125), (10.125, -0.125), (10.125, 8.125), (-0.125, 8.125)], Points(o));
        Assert.Equal(10.25 * 8.25, (double)o["area"]!, 9);
        var traced = o["walls"]!.AsArray().Select(x => (string)x!).ToHashSet();
        Assert.True(traced.SetEquals(shell));
        Assert.DoesNotContain(stub, traced);
        Assert.DoesNotContain(fence, traced);
    }

    [Fact]
    public void The_floor_is_an_ordinary_floor_on_the_traced_outline_its_area_the_core_faces_polygon()
    {
        var f = new Fixture();
        var type = Insulated(f);
        Rectangle(f, type);
        f.Wall(10, 4, 11, 4, type);
        var before = f.Model.OfClass(IfcClass.Slab).Count();
        var id = (string)f.Run("architecture.addFloorFromBuilding", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(5, 4), ["offset"] = 0.05 })!["id"]!;
        Assert.Equal(before + 1, f.Model.OfClass(IfcClass.Slab).Count());
        var slab = f.Model.Get(id)!;
        Assert.Equal(84.5625, ArcOutline.AreaOf(slab), 9);
        Assert.Equal(0.05, slab.Num("offset"), 9);
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, id)!.Id);
        // One undo step takes it away.
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
    }

    [Fact]
    public void An_L_of_walls_of_two_thicknesses_traces_each_walls_core_not_its_finish()
    {
        var f = new Fixture();
        // A: insulation 100, brick 300 core, plaster 15 (core face 150 off the line); B: concrete 200 core, finish 20 (100 off).
        var a = f.Type("Thick", ("Insulation", 0.10, false), ("Brick", 0.30, true), ("Plaster", 0.015, false));
        var b = f.Type("Thin", ("Concrete", 0.20, true), ("Finish", 0.02, false));
        f.Wall(0, 0, 12, 0, a);
        f.Wall(12, 0, 12, 5, a);
        f.Wall(12, 5, 6, 5, a);
        f.Wall(6, 5, 6, 10, b);
        f.Wall(6, 10, 0, 10, b);
        f.Wall(0, 10, 0, 0, b);
        // A partition inside, ending on the outer walls (a T at each end).
        f.Wall(6, 0, 6, 5, b);
        var o = Outline(f);
        Corners([(-0.10, -0.15), (12.15, -0.15), (12.15, 5.15), (6.10, 5.15), (6.10, 10.10), (-0.10, 10.10)], Points(o));
        Assert.Equal(12.25 * 5.30 + 6.20 * 4.95, (double)o["area"]!, 9);
    }

    [Fact]
    public void An_arc_wall_is_traced_as_an_arc_on_its_cores_exterior_face()
    {
        var f = new Fixture();
        var type = f.Type("Concrete 200", ("Concrete", 0.20, true));
        f.Wall(0, 0, 10, 0, type);
        f.Wall(10, 0, 10, 6, type);
        // Bulging 2 m out (to +y): its circle R 7.25 round (5, 0.75); the core's exterior face R 7.35.
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 10, ["y1"] = 6, ["x2"] = 0, ["y2"] = 6, ["arc"] = -2, ["typeId"] = type });
        f.Wall(0, 6, 0, 0, type);
        var o = Outline(f);
        var yc = 0.75 + Math.Sqrt(7.35 * 7.35 - 5.1 * 5.1);
        Assert.Equal(6.0426836293, yc, 9);
        Corners([(-0.1, -0.1), (10.1, -0.1), (10.1, yc), (-0.1, yc)], Points(o));
        var arcs = o["arcs"]!.AsArray().Select(x => (double)x!).ToList();
        Assert.Single(arcs, r => Math.Abs(r) > 1e-9);
        Assert.Equal(-2.0573163707, arcs.Single(r => Math.Abs(r) > 1e-9), 9);
        Assert.Equal(77.090379, (double)o["area"]!, 6);
        var id = (string)f.Run("architecture.addFloorFromBuilding", new JsonObject { ["storeyId"] = f.Storey })!["id"]!;
        Assert.Equal(77.090379, ArcOutline.AreaOf(f.Model.Get(id)!), 6);
    }

    [Fact]
    public void A_gap_in_the_outer_walls_traces_nothing_and_names_the_gap()
    {
        var f = new Fixture();
        var type = Insulated(f);
        f.Wall(0, 0, 10, 0, type);
        f.Wall(10, 0, 10, 8, type);
        f.Wall(10, 8, 5.5, 8, type);
        f.Wall(4.5, 8, 0, 8, type);
        f.Wall(0, 8, 0, 0, type);
        // 1 m: wider than an in-line join reaches (twice the wall), so the walls stay apart.
        // A partition closing a room on the left: without the gap seen, that room alone would be traced.
        f.Wall(3, 0, 3, 8, type);
        var ex = Assert.Throws<CommandException>(() => Outline(f));
        Assert.Contains("gap of 1 m", ex.Message);
        Assert.Contains("(5.5, 8)", ex.Message);
        Assert.Contains("(4.5, 8)", ex.Message);
        var before = f.Model.OfClass(IfcClass.Slab).Count();
        Assert.Throws<CommandException>(() => f.Run("architecture.addFloorFromBuilding", new JsonObject { ["storeyId"] = f.Storey }));
        Assert.Equal(before, f.Model.OfClass(IfcClass.Slab).Count());
    }

    [Fact]
    public void A_gap_inside_the_building_does_not_stop_the_trace()
    {
        var f = new Fixture();
        var type = Insulated(f);
        Rectangle(f, type);
        // A partition stopping 0.9 m short of the far wall: a passage, not a gap in the building.
        f.Wall(5, 0, 5, 7.1, type);
        Assert.Equal(84.5625, (double)Outline(f)["area"]!, 9);
    }

    [Fact]
    public void Two_buildings_the_one_clicked_is_traced()
    {
        var f = new Fixture();
        var type = Insulated(f);
        Rectangle(f, type);
        f.Wall(20, 0, 24, 0, type);
        f.Wall(24, 0, 24, 3, type);
        f.Wall(24, 3, 20, 3, type);
        f.Wall(20, 3, 20, 0, type);
        Assert.Equal(84.5625, (double)Outline(f)["area"]!, 9);
        Assert.Equal(4.25 * 3.25, (double)Outline(f, new JsonArray(22, 1))["area"]!, 9);
    }

    [Fact]
    public void No_closed_walls_say_so()
    {
        var f = new Fixture();
        f.Wall(0, 0, 10, 0);
        var ex = Assert.Throws<CommandException>(() => Outline(f));
        Assert.Contains("do not close", ex.Message);
    }
}
