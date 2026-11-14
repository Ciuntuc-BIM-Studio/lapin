using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// A tester's report (forum topic 82): a room's area did not take its finish into account. A room is measured to its finishes:
/// its net floor area, perimeter and volume to the finished face of its walls (the walls' finish at the floor taken in along
/// every edge, out round every island) and to its finished ceiling; the gross ones stay the structural outline. Derived on
/// every ask: laying, changing or taking off the finish changes them, with no "Update rooms".
/// </summary>
public sealed class RoomFinishAreaTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-room-finish-area").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    // The default wall type's inner face is 0.125 + 0.015 m in from its axis: walls drawn anticlockwise round axes that far
    // out of a 5 × 4 m rectangle leave a room of 5 × 4 m between their faces.
    const double Inside = 0.125 + 0.015;

    static (Fixture F, string Room, string[] Walls) Room()
    {
        var f = new Fixture();
        var (x0, y0, x1, y1) = (-Inside, -Inside, 5 + Inside, 4 + Inside);
        var walls = new[] { ((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0)) }
            .Select(x => f.Wall(x.Item1.Item1, x.Item1.Item2, x.Item2.Item1, x.Item2.Item2)).ToArray();
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        return (f, room, walls);
    }

    /// <summary>The finish of the report: the walls' waterproofing (no thickness), tile adhesive 5 mm and tiles 10 mm (1.5 cm); the floor's 1.5 cm; the ceiling's plaster 1 cm and paint.</summary>
    static string Tiled(Fixture f, bool thresholds = true) => (string)f.Run("architecture.createRoomFinishType", new JsonObject
    {
        ["spec"] = new RoomFinishSpec("Bath",
            Floor: [new("Ceramic tiles", 0.01), new("Tile adhesive", 0.005), new("Waterproofing", 0)],
            Walls: [new("Waterproofing", 0), new("Tile adhesive", 0.005), new("Ceramic tiles", 0.01)],
            Ceiling: [new("Plaster", 0.01), new("Paint", 0)], Thresholds: thresholds).ToJson(),
    })!["id"]!;

    static Dictionary<string, double> Q(Fixture f, string room) =>
        f.Session.Quantities(room).Single(s => s.Name == "Qto_SpaceBaseQuantities").Quantities.ToDictionary(q => q.Name, q => q.Value);

    [Fact]
    public void A_finished_room_measures_its_net_area_perimeter_and_volume_to_its_finishes_and_its_gross_ones_to_its_walls()
    {
        var (f, room, _) = Room();
        var h = Rooms.HeightOf(f.Model.Get(room)!, f.Model);
        var bare = Q(f, room);
        Assert.Equal(20, bare["NetFloorArea"], 6);
        Assert.Equal(20, bare["GrossFloorArea"], 6);
        Assert.Equal(18, bare["NetPerimeter"], 6);
        Assert.Equal(20 * h, bare["NetVolume"], 6);
        Assert.False(bare.ContainsKey("FinishCeilingHeight"));

        var type = Tiled(f);
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = type });
        var q = Q(f, room);
        const double w = 5 - 0.03, d = 4 - 0.03;
        Assert.Equal(w * d, q["NetFloorArea"], 6);
        Assert.Equal(2 * (w + d), q["NetPerimeter"], 6);
        // The finished floor is the room's level (the floor's layers go down from it); the ceiling hangs 1 cm under the room's height.
        Assert.Equal(h - 0.01, q["FinishCeilingHeight"], 6);
        Assert.Equal(0.015, q["FinishFloorHeight"], 6);
        Assert.Equal(w * d * (h - 0.01), q["NetVolume"], 6);
        // The structural measures stay: the walls' faces, the room's height.
        Assert.Equal(20, q["GrossFloorArea"], 6);
        Assert.Equal(18, q["GrossPerimeter"], 6);
        Assert.Equal(h, q["Height"], 6);
        Assert.Equal(20 * h, q["GrossVolume"], 6);

        // The stamp, the room schedule and the documents' usable area read the finished area.
        var r = f.Model.Get(room)!;
        var o = Rooms.Of(r, f.Model, f.Session.Geometry)!;
        Assert.Equal((w * d).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " m²", Rooms.Values(r, o, f.Model, "m")["area"]);
        var row = new RoomSchedule().Grid(f.Session.Reader).Rows[0];
        Assert.Equal((w * d).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), row[5]);
        Assert.Equal((2 * (w + d)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), row[6]);

        // A thicker finish: the area follows it at once.
        var spec = RoomFinishes.SpecOf(f.Model, f.Model.Get(type)!)! with { Walls = [new FinishLayer("Plaster", 0.02), new FinishLayer("Ceramic tiles", 0.01)] };
        f.Run("architecture.updateRoomFinishType", new JsonObject { ["id"] = type, ["spec"] = spec.ToJson() });
        Assert.Equal((5 - 0.06) * (4 - 0.06), Q(f, room)["NetFloorArea"], 6);

        // Taken off: the room measures to its walls again.
        f.Run("architecture.removeRoomFinish", new JsonObject { ["roomId"] = room });
        var again = Q(f, room);
        Assert.Equal(20, again["NetFloorArea"], 6);
        Assert.Equal(18, again["NetPerimeter"], 6);
        Assert.Equal(20 * h, again["NetVolume"], 6);
        Assert.False(again.ContainsKey("FinishCeilingHeight"));
    }

    [Fact]
    public void The_walls_finish_at_the_floor_is_what_counts_and_a_column_grows_by_it()
    {
        var (f, room, _) = Room();
        // Tiles 13 mm on the plaster up to 2.10 m, paint 1 mm over the plaster above: at the floor the face is 15 + 13 mm in.
        var type = (string)f.Run("architecture.createRoomFinishType", new JsonObject
        {
            ["spec"] = new RoomFinishSpec("Bath", Walls: [new("Plaster", 0.015), new("Paint", 0.001, [new FinishPart("Ceramic tiles", 2.1, 0.013)])]).ToJson(),
        })!["id"]!;
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = type });
        const double t = 0.028;
        Assert.Equal((5 - 2 * t) * (4 - 2 * t), Q(f, room)["NetFloorArea"], 6);
        // No ceiling in the finish: the room's height, as before.
        Assert.False(Q(f, room).ContainsKey("FinishCeilingHeight"));
        Assert.Equal((5 - 2 * t) * (4 - 2 * t) * Rooms.HeightOf(f.Model.Get(room)!, f.Model), Q(f, room)["NetVolume"], 6);
    }

    [Fact]
    public void A_door_leaves_the_finished_area_as_it_is_whether_the_floor_runs_through_it_or_not()
    {
        foreach (var thresholds in new[] { true, false })
        {
            var (f, room, walls) = Room();
            f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["x"] = 2, ["y"] = -Inside });
            f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = Tiled(f, thresholds) });
            // The door's gap is not the room's (the finished face runs on across it): the threshold is the floor finish's, not the room's.
            Assert.Equal(4.97 * 3.97, Q(f, room)["NetFloorArea"], 6);
            Assert.Equal(20, Q(f, room)["GrossFloorArea"], 6);
        }
    }

    [Fact]
    public void The_ifc_file_writes_the_finished_net_and_the_structural_gross()
    {
        var (f, room, _) = Room();
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = Tiled(f) });
        var path = Path.Combine(_dir, "room.ifc");
        IfcExport.Write(f.Session, path);
        var file = StepFile.Read(File.ReadAllBytes(path));
        double Area(string name) => file.OfType("IFCQUANTITYAREA").Where(x => (string?)x[0] == name).Select(x => IfcModel.Num(x[3])).Single();
        double Length(string name) => file.OfType("IFCQUANTITYLENGTH").Where(x => (string?)x[0] == name).Select(x => IfcModel.Num(x[3])).Single();
        Assert.Equal(4.97 * 3.97, Area("NetFloorArea"), 6);
        Assert.Equal(20, Area("GrossFloorArea"), 6);
        Assert.Equal(2 * (4.97 + 3.97), Length("NetPerimeter"), 6);
        Assert.Equal(Rooms.HeightOf(f.Model.Get(room)!, f.Model) - 0.01, Length("FinishCeilingHeight"), 6);
    }
}
