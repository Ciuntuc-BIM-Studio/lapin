using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Host;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The testers' reports #80 and #81 (decision 294): walls joined to each other never cut each other by their element
/// priority (a partition of priority 10 no longer runs to the axis of a brick wall of 1, notching its core); where they
/// meet, each layer goes on through the other's layers of a lower priority and stops at the first as high or higher (a
/// layer's own priority, else its material's in the cut order, else 50 for a core and 0 for the rest), so cores meet
/// cores and finishes stop at faces by default; the element priority decides only between cores of equal priority; a
/// room placed inside such walls, and its skirting, follow their faces round the corners and the T's whatever the
/// priorities. The layer's priority goes to IFC as IfcMaterialLayer.Priority and comes back.
/// </summary>
public sealed class JoinPriorityTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-joins").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    const double H = 3, Render = 0.02, Brick = 0.25, Plaster = 0.015, Board = 0.0125, Studs = 0.1;
    /// <summary>The partition's thickness: two boards each side of the studs.</summary>
    const double Gb = 4 * Board + Studs;

    /// <summary>The tester's two types: Brick wall 285 (render 20, brick 250 core, plaster 15) and partition gb (two boards of 12.5 each side of 100 studs).</summary>
    static (Fixture F, string Brick, string Gb) Types()
    {
        var f = new Fixture(new ArchitectureModule());
        var brick = f.Type("Brick wall 285", ("Render", Render, false), ("Brick", Brick, true), ("Plaster", Plaster, false));
        var gb = f.Type("partition gb", ("Plasterboard", Board, false), ("Plasterboard", Board, false), ("UW/CW", Studs, true), ("Plasterboard", Board, false), ("Plasterboard", Board, false));
        return (f, brick, gb);
    }

    static void Priority(Fixture f, string id, int? priority) =>
        f.Run("element.update", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["cutPriority"] = priority } });

    static List<GeometryPart> Layer(Fixture f, string wall, int index) => f.Session.Geometry(wall)!.Parts.Where(p => p.Key == $"layer:{index}").ToList();
    static double Volume(Fixture f, string wall, int index) => Layer(f, wall, index).Sum(p => Measure.Volume(p.Solid));
    static double Volume(Fixture f, string wall) => f.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid));

    static (Vec3 Min, Vec3 Max) Bounds(Fixture f, string wall, int index)
    {
        var v = Layer(f, wall, index).SelectMany(p => p.Solid.Vertices).ToList();
        return (new(v.Min(q => q.X), v.Min(q => q.Y), v.Min(q => q.Z)), new(v.Max(q => q.X), v.Max(q => q.Y), v.Max(q => q.Z)));
    }

    /// <summary>The wall's plan as cut: one polygon a piece of a layer.</summary>
    static List<IReadOnlyList<Vec2>> Cut(Fixture f, string wall) => f.Session.Geometry(wall)!.Plan!.Where(p => p.Kind == "cut").Select(p => p.Points).ToList();

    static JsonObject TypeLayers(params (string Material, double Thickness, bool Core, int? Priority)[] layers) => new()
    {
        ["layers"] = new JsonArray(layers.Select(l =>
        {
            var o = new JsonObject { ["material"] = l.Material, ["thickness"] = l.Thickness, ["core"] = l.Core };
            if (l.Priority is { } p) o["priority"] = p;
            return (JsonNode)o;
        }).ToArray()),
    };

    static string Type(Fixture f, string name, params (string Material, double Thickness, bool Core, int? Priority)[] layers)
    {
        var a = TypeLayers(layers);
        a["name"] = name;
        return (string)f.Run("architecture.createWallType", a)!["id"]!;
    }

    [Theory]
    [InlineData(10, 1)]
    [InlineData(1, 10)]
    [InlineData(10, 10)]
    [InlineData(null, null)]
    public void A_partition_ending_on_a_brick_wall_stops_at_its_core_whatever_their_element_priorities(int? partition, int? brickWall)
    {
        // Report #81: the partition (10) ran into the brick wall (1) to its axis, notching its core.
        var (f, brick, gb) = Types();
        var through = f.Wall(5, 0, 5, 6, brick);      // render on the right (+x), plaster on the left
        var ending = f.Wall(0, 3, 5, 3, gb);          // from the plaster's side, ending on the axis
        Priority(f, ending, partition);
        Priority(f, through, brickWall);

        // The brick wall: its brick and render whole, its plaster interrupted by the studs only.
        Assert.Equal(6 * Brick * H, Volume(f, through, 1), 9);
        Assert.Single(Layer(f, through, 1));
        Assert.Equal(6 * Render * H, Volume(f, through, 0), 9);
        Assert.Equal((6 - Studs) * Plaster * H, Volume(f, through, 2), 9);
        Assert.Equal(2, Layer(f, through, 2).Count);
        // The partition: its studs to the brick's face, its boards to the plaster's face.
        var brickFace = 5 - Brick / 2;
        Assert.Equal(brickFace, Bounds(f, ending, 2).Max.X, 9);
        Assert.Equal(brickFace * Studs * H, Volume(f, ending, 2), 9);
        foreach (var board in new[] { 0, 1, 3, 4 })
        {
            Assert.Equal(brickFace - Plaster, Bounds(f, ending, board).Max.X, 9);
            Assert.Equal((brickFace - Plaster) * Board * H, Volume(f, ending, board), 9);
        }
        // Nothing built twice, nothing left open: the two fill the union of their bodies.
        Assert.Equal(6 * (Render + Brick + Plaster) * H + (brickFace - Plaster) * Gb * H, Volume(f, through) + Volume(f, ending), 9);
        // In plan: the brick's core one rectangle from end to end, never notched; its plaster in two.
        var core = Cut(f, through).Where(p => p.All(q => q.X >= brickFace - 1e-9 && q.X <= 5 + Brick / 2 + 1e-9)).ToList();
        var piece = Assert.Single(core);
        Assert.Equal(6 * Brick, Math.Abs(Polygon2.SignedArea(piece)), 9);
        Assert.Equal(4, Cut(f, through).Count);
    }

    [Fact]
    public void A_core_of_a_higher_priority_goes_on_through_a_lower_one_and_stops_at_an_equal_or_higher()
    {
        // The tester's example: a concrete core (100, by its material in the cut order) is not interrupted by metal studs (99, the layer's own).
        var f = new Fixture(new ArchitectureModule());
        var concrete = Type(f, "Concrete 200", ("Plaster", Plaster, false, null), ("Concrete", 0.2, true, null), ("Plaster", Plaster, false, null));
        var studs = Type(f, "Studs 100", ("Plasterboard", Board, false, null), ("Metal studs", Studs, true, 99), ("Plasterboard", Board, false, null));
        var concreteId = f.Model.OfClass(IfcClass.Material).Single(x => x.Name == "Concrete").Id;
        f.Run("cutOrder.set", new JsonObject { ["materials"] = new JsonObject { [concreteId] = 100 } });

        // The stud wall through, the concrete wall ending on it from below.
        var stud = f.Wall(0, 3, 8, 3, studs);
        var wall = f.Wall(4, 0, 4, 3, concrete);
        // The concrete core goes on through the boards and the studs to the stud wall's far face; its plaster stops at the near face.
        Assert.Equal(3 + Studs / 2 + Board, Bounds(f, wall, 1).Max.Y, 9);
        Assert.Equal(3 - Studs / 2 - Board, Bounds(f, wall, 0).Max.Y, 9);
        Assert.Equal(3 - Studs / 2 - Board, Bounds(f, wall, 2).Max.Y, 9);
        // The stud wall is interrupted in every layer where the concrete passes: two pieces of each, 200 mm less.
        foreach (var i in new[] { 0, 1, 2 })
        {
            Assert.Equal(2, Layer(f, stud, i).Count);
            Assert.Equal(8 - 0.2, Volume(f, stud, i) / (H * (i == 1 ? Studs : Board)), 9);
        }

        // Studs ranked as high as the concrete: the concrete stops at the studs' face, the studs run whole.
        var update = TypeLayers(("Plasterboard", Board, false, null), ("Metal studs", Studs, true, 100), ("Plasterboard", Board, false, null));
        update["id"] = studs;
        f.Run("architecture.updateWallType", update);
        Assert.Equal(3 - Studs / 2, Bounds(f, wall, 1).Max.Y, 9);
        Assert.Single(Layer(f, stud, 1));
        Assert.Equal(8 * Studs * H, Volume(f, stud, 1), 9);

        // The other way round: the concrete wall through, the studs (99) stop at its core's face whatever the element priorities say.
        var g = new Fixture(new ArchitectureModule());
        var c2 = Type(g, "Concrete 200", ("Plaster", Plaster, false, null), ("Concrete", 0.2, true, 100), ("Plaster", Plaster, false, null));
        var s2 = Type(g, "Studs 100", ("Plasterboard", Board, false, null), ("Metal studs", Studs, true, 99), ("Plasterboard", Board, false, null));
        var through = g.Wall(0, 3, 8, 3, c2);
        var partition = g.Wall(4, 0, 4, 3, s2);
        Priority(g, partition, 90);
        Assert.Equal(3 - 0.1, Bounds(g, partition, 1).Max.Y, 9);
        Assert.Equal(3 - 0.1 - Plaster, Bounds(g, partition, 0).Max.Y, 9);
        Assert.Single(Layer(g, through, 1));
        Assert.Equal(8 * 0.2 * H, Volume(g, through, 1), 9);
    }

    [Fact]
    public void A_finish_of_a_higher_priority_holds_the_core_that_would_come_through_it()
    {
        var (f, _, gb) = Types();
        // The brick wall's plaster ranked over cores (60): the partition's studs stop at it and it runs whole.
        var brick = Type(f, "Brick wall 285 hard plaster", ("Render", Render, false, null), ("Brick", Brick, true, null), ("Plaster", Plaster, false, 60));
        var through = f.Wall(5, 0, 5, 6, brick);
        var ending = f.Wall(0, 3, 5, 3, gb);
        Assert.Equal(5 - Brick / 2 - Plaster, Bounds(f, ending, 2).Max.X, 9);
        Assert.Single(Layer(f, through, 2));
        Assert.Equal(6 * Plaster * H, Volume(f, through, 2), 9);
    }

    [Theory]
    [InlineData("mitre")]
    [InlineData("butt")]
    public void At_a_corner_the_core_of_the_higher_priority_runs_through_whatever_the_style(string style)
    {
        var (f, brick, _) = Types();
        var hard = Type(f, "Brick wall 285 hard core", ("Render", Render, false, null), ("Brick", Brick, true, 60), ("Plaster", Plaster, false, null));
        // Anticlockwise from (0, 0): the render outside, the corner at (5, 0); the second wall's core ranks higher.
        var first = f.Wall(0, 0, 5, 0, brick, style);
        var second = f.Wall(5, 0, 5, 5, hard, style);
        // The second's core runs on to the first's outer core face; the first's stops at the second's inner core face.
        Assert.Equal(-Brick / 2, Bounds(f, second, 1).Min.Y, 9);
        Assert.Equal((5 + Brick / 2) * Brick * H, Volume(f, second, 1), 9);
        Assert.Equal(5 - Brick / 2, Bounds(f, first, 1).Max.X, 9);
        Assert.Equal((5 - Brick / 2) * Brick * H, Volume(f, first, 1), 9);
        // The finishes still turn the corner: together the two fill the L of their bodies.
        var t = Render + Brick + Plaster;
        var (outside, inside) = (Brick / 2 + Render, Brick / 2 + Plaster);
        Assert.Equal(((5 + outside) * t + (5 - inside) * t) * H, Volume(f, first) + Volume(f, second), 9);
    }

    [Fact]
    public void At_a_crossing_the_core_of_the_higher_priority_runs_through_and_of_equal_ones_the_higher_element()
    {
        var (f, brick, _) = Types();
        var through = f.Wall(0, 3, 8, 3, brick);
        var crossing = f.Wall(4, 0, 4, 6, brick);
        Assert.Equal(through, Assert.Single(f.Model.Relations, r => r.Class == IfcClass.RelConnectsPathElements).Relating);
        Assert.Equal(2, Layer(f, crossing, 1).Count);
        // The crossing wall ranked higher as an element: with cores of equal priority, its core now runs through (no longer cut by its envelope).
        Priority(f, crossing, 60);
        Assert.Single(Layer(f, crossing, 1));
        Assert.Equal(6 * Brick * H, Volume(f, crossing, 1), 9);
        Assert.Equal(2, Layer(f, through, 1).Count);
        Assert.Equal((8 - Brick) * Brick * H, Volume(f, through, 1), 9);
        // Its core ranked under the other's: the core's priority decides before the element's.
        var soft = Type(f, "Brick wall 285 soft core", ("Render", Render, false, null), ("Brick", Brick, true, 40), ("Plaster", Plaster, false, null));
        f.Run("architecture.setWallType", new JsonObject { ["ids"] = new JsonArray(crossing), ["typeId"] = soft });
        Assert.Equal(2, Layer(f, crossing, 1).Count);
        Assert.Single(Layer(f, through, 1));
        // Nothing built twice either way.
        var t = Render + Brick + Plaster;
        Assert.Equal((8 * t + 6 * t - t * t) * H, Volume(f, through) + Volume(f, crossing), 9);
    }

    /// <summary>A box of brick walls (anticlockwise, plaster inside) cut in two by a partition gb ending on both sides at y = 3.</summary>
    static (Fixture F, string[] Box, string Partition) Halls(int? partition, int? brickWalls)
    {
        var (f, brick, gb) = Types();
        string[] box = [f.Wall(0, 0, 8, 0, brick), f.Wall(8, 0, 8, 6, brick), f.Wall(8, 6, 0, 6, brick), f.Wall(0, 6, 0, 0, brick)];
        var p = f.Wall(0, 3, 8, 3, gb);
        Priority(f, p, partition);
        foreach (var w in box) Priority(f, w, brickWalls);
        return (f, box, p);
    }

    [Theory]
    [InlineData(10, 1)]
    [InlineData(1, 10)]
    [InlineData(10, 10)]
    public void A_room_and_its_skirting_follow_the_faces_at_the_corners_and_the_tees_whatever_the_priorities(int partition, int brickWalls)
    {
        // Reports #80 and #81: the room's outline on the faces as joined, the skirting one piece round it, mitred at its corners.
        var (f, _, _) = Halls(partition, brickWalls);
        var inner = Brick / 2 + Plaster;
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 1, ["y"] = 1 })!["id"]!;
        var o = Rooms.OfGeometry(f.Session.Geometry(room))!;
        Assert.Equal(4, o.Outer.Count);
        foreach (var c in new Vec2[] { new(inner, inner), new(8 - inner, inner), new(8 - inner, 3 - Gb / 2), new(inner, 3 - Gb / 2) })
            Assert.Contains(o.Outer, q => (q - c).Length < 1e-6);
        Assert.Equal((8 - 2 * inner) * (3 - Gb / 2 - inner), o.NetArea, 6);
        var skirting = (string)f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["roomId"] = room })!["id"]!;
        var g = f.Session.Geometry(skirting)!;
        Assert.Single(g.Plan!, p => p.Kind == "hit");
        Assert.Equal(2 * ((8 - 2 * inner) + (3 - Gb / 2 - inner)), Skirtings.LengthOf(g), 6);
        Assert.Single(g.Parts);
    }

    [Theory]
    [InlineData("mitre", 10, 10)]
    [InlineData("butt", 10, 10)]
    [InlineData("mitre", 10, 1)]
    [InlineData("butt", 1, 10)]
    public void A_room_round_a_box_of_partitions_turns_its_outer_corners_on_their_faces(string style, int boxWalls, int outerWalls)
    {
        // Report #80: the testing room wraps round the corners of a store of partitions (both 10): the room's outline
        // there, its island, is the store's outer faces, and the skirting runs round it unbroken.
        var (f, brick, gb) = Types();
        string[] outer = [f.Wall(0, 0, 10, 0, brick, style), f.Wall(10, 0, 10, 10, brick, style), f.Wall(10, 10, 0, 10, brick, style), f.Wall(0, 10, 0, 0, brick, style)];
        string[] store = [f.Wall(3, 3, 6, 3, gb, style), f.Wall(6, 3, 6, 6, gb, style), f.Wall(6, 6, 3, 6, gb, style), f.Wall(3, 6, 3, 3, gb, style)];
        foreach (var w in store) Priority(f, w, boxWalls);
        foreach (var w in outer) Priority(f, w, outerWalls);
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 1, ["y"] = 1 })!["id"]!;
        var o = Rooms.OfGeometry(f.Session.Geometry(room))!;
        var island = Assert.Single(o.Islands);
        Assert.Equal(4, island.Count);
        var (lo, hi) = (3 - Gb / 2, 6 + Gb / 2);
        foreach (var c in new Vec2[] { new(lo, lo), new(hi, lo), new(hi, hi), new(lo, hi) })
            Assert.Contains(island, q => (q - c).Length < 1e-6);
        var skirting = (string)f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["roomId"] = room })!["id"]!;
        var g = f.Session.Geometry(skirting)!;
        var inner = Brick / 2 + Plaster;
        Assert.Equal(2, g.Plan!.Count(p => p.Kind == "hit"));
        Assert.Equal(4 * (10 - 2 * inner) + 4 * (hi - lo), Skirtings.LengthOf(g), 6);
    }

    [Fact]
    public void A_layers_priority_is_kept_in_the_type_and_goes_to_IFC_and_the_package_and_back()
    {
        var f = new Fixture(new ArchitectureModule());
        var type = Type(f, "Studs 100", ("Plasterboard", Board, false, 5), ("Metal studs", Studs, true, 99), ("Plasterboard", Board, false, null));
        f.Wall(0, 0, 4, 0, type);
        Assert.Equal(new int?[] { 5, 99, null }, LayerSets.LayersOf(f.Model, type).Select(l => l.Priority));
        Assert.Throws<CommandException>(() => Type(f, "Bad", ("Plasterboard", Board, false, 101), ("Metal studs", Studs, true, null)));

        var ifc = Path.Combine(_dir, "studs.ifc");
        IfcExport.Write(f.Session, ifc, null);
        Assert.Contains(",'Core',99);", File.ReadAllText(ifc));
        var b = new Fixture(new ArchitectureModule());
        IfcImport.Run(b.Session, ifc, null, out _);
        var back = b.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "Studs 100");
        Assert.Equal(new int?[] { 5, 99, null }, LayerSets.LayersOf(b.Model, back.Id).Select(l => l.Priority));

        var lapin = Path.Combine(_dir, "studs.lapin");
        PackageFiles.Save(f.Session, lapin);
        var c = new Fixture(new ArchitectureModule());
        PackageFiles.Open(c.Session, lapin);
        Assert.Equal(new int?[] { 5, 99, null }, LayerSets.LayersOf(c.Model, type).Select(l => l.Priority));
    }
}
