using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A tester's GitHub issues #142, #144 and #149, checking Lapin's IFC in CYPECAD MEP: a room separation line bounds the floor
/// but gets no wall finish and no skirting; a skirting may start at its room's finished floor and stand in front of the walls'
/// finish (options, the old placing the default); a door measured from the finished floor may have its hole go down to the
/// structure (an option, decision 749's strip the default).
/// </summary>
public class RoomBoundaryFinishTests
{
    /// <summary>Four walls 0.2 m thick whose inner faces close a 6 × 4 m room from (0, 0) to (6, 4), split at x = 3 by a separation line.</summary>
    static (Fixture F, string West, string East, string[] Walls) Split()
    {
        var f = new Fixture();
        var t = f.Type("Block 200", ("Block", 0.2, true));
        string[] walls = [f.Wall(-0.1, -0.1, 6.1, -0.1, t), f.Wall(6.1, -0.1, 6.1, 4.1, t), f.Wall(6.1, 4.1, -0.1, 4.1, t), f.Wall(-0.1, 4.1, -0.1, -0.1, t)];
        f.Run("architecture.addRoomDivider", new JsonObject
        {
            ["storeyId"] = f.Storey, ["kind"] = "line", ["points"] = new JsonArray(new JsonArray(3, 0), new JsonArray(3, 4)),
        });
        return (f, Room(f, 1.5, 2), Room(f, 4.5, 2), walls);
    }

    static string Room(Fixture f, double x, double y) =>
        (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y })!["id"]!;

    static string Finish(Fixture f, string room, string name)
    {
        var type = (string)f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = name })!["id"]!;
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomIds"] = new JsonArray(room), ["typeId"] = type });
        return f.Model.OfClass(RoomFinishes.Class).Single(e => e.Str("roomId") == room && e.Str("part") == "walls").Id;
    }

    static string Skirting(Fixture f, string room, JsonObject? more = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["roomId"] = room };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("architecture.addSkirting", a)!["id"]!;
    }

    static double Length(Fixture f, string skirting) =>
        f.Session.Quantities(skirting).Single(s => s.Name == "Lapin_SkirtingQuantities").Quantities.Single(x => x.Name == "Length").Value;

    // ------------------------------------------------------------ #142

    [Fact]
    public void A_separation_line_gets_no_wall_finish_its_quantities_leave_it_out_and_the_room_still_meets_it()
    {
        var (f, west, _, _) = Split();
        var walls = Finish(f, west, "Living room");
        var g = f.Session.Geometry(walls)!;
        var height = Rooms.HeightOf(f.Model.Get(west)!, f.Model);
        // The plaster runs along the three walls (3 + 4 + 3 m), none along the line at x = 3.
        var plaster = RoomFinishes.Measures(g).Single(x => x.Material == "Plaster");
        var face = height - 0.011;
        Assert.Equal(10 * face, plaster.Gross, 6);
        Assert.DoesNotContain(g.Parts.SelectMany(p => p.Solid.Vertices), v => v.X > 3 - 0.05 && v.X < 3 + 1e-6 && v.Y > 0.05 && v.Y < 3.95);
        // Its bands end on the line: the finish reaches x = 3 along the south and north walls.
        Assert.Contains(g.Parts.SelectMany(p => p.Solid.Vertices), v => Math.Abs(v.X - 3) < 1e-6 && v.Y < 0.02);
        Assert.All(g.Parts.SelectMany(p => p.Solid.Vertices), v => Assert.True(v.X <= 3 + 1e-6));
        // The room's finished outline keeps its edge on the line: the finished floor stays 3 m wide less the plaster on one side.
        var net = Rooms.NetAreaOf(f.Model.Get(west)!, f.Model, f.Session.Geometry);
        Assert.Equal((3 - 0.016) * (4 - 2 * 0.016), net, 6);
        // The room's own perimeter is unchanged (decision 567: the halves add up to the whole).
        Assert.Equal(14, f.Session.Quantities(west).SelectMany(s => s.Quantities).First(q => q.Name == "GrossPerimeter").Value, 6);
    }

    [Fact]
    public void A_room_without_separation_lines_keeps_its_finish_as_before()
    {
        var f = new Fixture();
        var t = f.Type("Block 200", ("Block", 0.2, true));
        f.Wall(-0.1, -0.1, 6.1, -0.1, t); f.Wall(6.1, -0.1, 6.1, 4.1, t); f.Wall(6.1, 4.1, -0.1, 4.1, t); f.Wall(-0.1, 4.1, -0.1, -0.1, t);
        var room = Room(f, 1.5, 2);
        var walls = Finish(f, room, "Living room");
        var g = f.Session.Geometry(walls)!;
        var plaster = RoomFinishes.Measures(g).Single(x => x.Material == "Plaster");
        Assert.Equal(20 * (Rooms.HeightOf(f.Model.Get(room)!, f.Model) - 0.011), plaster.Gross, 6);
        // A closed ring, one solid per layer piece, round the whole room.
        var box = Measure.Bounds(g.Parts.First(p => p.Material == Materials.Find(f.Model, "Plaster")!.Id).Solid)!.Value;
        Assert.Equal(0, box.Min.X, 6);
        Assert.Equal(6, box.Max.X, 6);
    }

    [Fact]
    public void A_skirting_round_a_split_room_stops_at_the_separation_line()
    {
        var (f, west, east, _) = Split();
        var w = Skirting(f, west);
        var e = Skirting(f, east);
        // Three walls each: 3 + 4 + 3 m, none along the 4 m line.
        Assert.Equal(10, Length(f, w), 6);
        Assert.Equal(10, Length(f, e), 6);
        var g = f.Session.Geometry(w)!;
        Assert.Equal(1, g.Plan!.Count(p => p.Kind == "hit"));
        Assert.DoesNotContain(g.Parts.SelectMany(p => p.Solid.Vertices), v => v.X > 3 - 0.05 && v.Y > 0.05 && v.Y < 3.95);
    }

    // ------------------------------------------------------------ #144

    static (Fixture F, string Room) Raised(double baseOffset)
    {
        var f = new Fixture();
        var t = f.Type("Block 200", ("Block", 0.2, true));
        f.Wall(-0.1, -0.1, 6.1, -0.1, t); f.Wall(6.1, -0.1, 6.1, 4.1, t); f.Wall(6.1, 4.1, -0.1, 4.1, t); f.Wall(-0.1, 4.1, -0.1, -0.1, t);
        var room = Room(f, 1.5, 2);
        f.Run("element.update", new JsonObject { ["id"] = room, ["attrs"] = new JsonObject { ["baseOffset"] = baseOffset } });
        Finish(f, room, "Living room");
        return (f, room);
    }

    [Fact]
    public void By_default_a_skirting_stands_on_the_storey_against_the_walls_as_before()
    {
        var (f, room) = Raised(0.07);
        var s = Skirting(f, room);
        var box = Measure.Bounds(f.Session.Geometry(s)!.Parts.Single().Solid)!.Value;
        Assert.Equal(0, box.Min.Z, 6);
        Assert.Equal(0, box.Min.X, 6);
        Assert.Null(f.Model.Get(s)!.Attrs[Skirtings.StartKey]);
        Assert.Null(f.Model.Get(s)!.Attrs[Skirtings.FaceKey]);
    }

    [Fact]
    public void A_skirting_may_start_at_its_rooms_finished_floor_and_stand_in_front_of_the_plaster()
    {
        var (f, room) = Raised(0.07);
        var s = Skirting(f, room, new JsonObject { ["startAt"] = "finishedFloor", ["face"] = "finish" });
        var box = Measure.Bounds(f.Session.Geometry(s)!.Parts.Single().Solid)!.Value;
        Assert.Equal(0.07, box.Min.Z, 6);
        Assert.Equal(0.07 + 0.08, box.Max.Z, 6);
        // In front of the living room's plaster and paint, 16 mm off the walls' faces.
        Assert.Equal(0.016, box.Min.X, 6);
        Assert.Equal(6 - 0.016, box.Max.X, 6);
        Assert.Equal(2 * (6 + 4 - 4 * 0.016), Length(f, s), 6);
        // Its own base offset over that floor; back to the storey's in one step, undone in one.
        f.Run("architecture.updateSkirting", new JsonObject { ["id"] = s, ["baseOffset"] = 0.01 });
        Assert.Equal(0.08, Measure.Bounds(f.Session.Geometry(s)!.Parts.Single().Solid)!.Value.Min.Z, 6);
        f.Run("architecture.updateSkirting", new JsonObject { ["id"] = s, ["startAt"] = "storey", ["face"] = "wall" });
        Assert.Equal(0.01, Measure.Bounds(f.Session.Geometry(s)!.Parts.Single().Solid)!.Value.Min.Z, 6);
        Assert.Null(f.Model.Get(s)!.Attrs[Skirtings.StartKey]);
        f.Session.Undo(f.Session.Author);
        Assert.Equal(Skirtings.FinishedFloor, f.Model.Get(s)!.Str(Skirtings.StartKey));
        Assert.Throws<CommandException>(() => f.Run("architecture.updateSkirting", new JsonObject { ["id"] = s, ["startAt"] = "slab" }));
    }

    [Fact]
    public void A_drawn_skirting_takes_the_floor_and_the_finish_of_the_room_it_faces()
    {
        var f = new Fixture();
        var t = f.Type("Block 200", ("Block", 0.2, true));
        f.Wall(-0.1, -0.1, 6.1, -0.1, t); f.Wall(6.1, -0.1, 6.1, 4.1, t); f.Wall(6.1, 4.1, -0.1, 4.1, t); f.Wall(-0.1, 4.1, -0.1, -0.1, t);
        // A partition at x = 3 (its faces 2.95 and 3.05) between rooms of 0.07 and 0.06 finished floors.
        f.Wall(3, 0, 3, 4, f.Type("Partition 100", ("Block", 0.1, true)));
        var west = Room(f, 1.5, 2);
        var east = Room(f, 4.5, 2);
        f.Run("element.update", new JsonObject { ["id"] = west, ["attrs"] = new JsonObject { ["baseOffset"] = 0.07 } });
        f.Run("element.update", new JsonObject { ["id"] = east, ["attrs"] = new JsonObject { ["baseOffset"] = 0.06 } });
        Finish(f, west, "Living room");
        // Along the partition's east face, drawn northwards: the partition on its left, so the board lies right, in the east room.
        var s = (string)f.Run("architecture.addSkirting", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(3.05, 0.5), new JsonArray(3.05, 3.5)),
            ["startAt"] = "finishedFloor", ["face"] = "finish",
        })!["id"]!;
        var box = Measure.Bounds(f.Session.Geometry(s)!.Parts.Single().Solid)!.Value;
        Assert.Equal(0.06, box.Min.Z, 6);
        // The east room has no walls finish: on the partition's face.
        Assert.Equal(3.05, box.Min.X, 6);
        // The same along the west face, drawn southwards so its board lies west: the west room's floor and plaster.
        var w = (string)f.Run("architecture.addSkirting", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(2.95, 3.5), new JsonArray(2.95, 0.5)),
            ["startAt"] = "finishedFloor", ["face"] = "finish",
        })!["id"]!;
        var wb = Measure.Bounds(f.Session.Geometry(w)!.Parts.Single().Solid)!.Value;
        Assert.Equal(0.07, wb.Min.Z, 6);
        Assert.Equal(2.95 - 0.016, wb.Max.X, 6);
    }

    // ------------------------------------------------------------ #149

    static (Fixture F, string Wall, string Door) DoorOnFinishedFloor()
    {
        var f = new Fixture();
        var type = f.Type("Brick 30", ("Brick", 0.3, true));
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["finishedFloor"] = 0.07 });
        var wall = f.Wall(0, 0, 8, 0, type);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["sill"] = 0, ["sillFrom"] = "finished" })!["id"]!;
        return (f, wall, door);
    }

    static Openings.Placed Placed(Fixture f, string fill) => Openings.Place(f.Model, Openings.OpeningOf(f.Model, fill)!);

    static double WallVolume(Fixture f, string wall) => f.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void By_default_a_door_on_the_finished_floor_leaves_the_strip_under_it_as_decision_749_has_it()
    {
        var (f, _, door) = DoorOnFinishedFloor();
        var p = Placed(f, door);
        Assert.Equal(0.07, p.Sill, 9);
        Assert.Equal(0.07, p.FillSill, 9);
        Assert.Equal(0, p.Drop);
    }

    [Fact]
    public void A_door_may_have_its_hole_go_down_to_the_structure_while_it_stays_on_the_finished_floor()
    {
        var (f, wall, door) = DoorOnFinishedFloor();
        var before = Placed(f, door);
        var doorBox = f.Bounds(door);
        var volume = WallVolume(f, wall);
        f.Run("architecture.setReachStructure", new JsonObject { ["ids"] = new JsonArray(door), ["on"] = true });
        var p = Placed(f, door);
        // The hole from the wall's base, as high again as the floor; the door where it stood, its sill still 0 over the floor.
        Assert.Equal(0, p.Sill, 9);
        Assert.Equal(before.Height + 0.07, p.Height, 9);
        Assert.Equal(0.07, p.FillSill, 9);
        Assert.Equal(0, p.OwnSill, 9);
        Assert.Equal(0.07, p.Drop, 9);
        var after = f.Bounds(door);
        Assert.Equal(doorBox.Min.Z, after.Min.Z, 9);
        Assert.Equal(doorBox.Max.Z, after.Max.Z, 9);
        // The strip under the door is gone from the wall.
        Assert.Equal(volume - before.Width * 0.3 * 0.07, WallVolume(f, wall), 6);
        // Moved up, its own sill is kept over the floor and the hole still reaches down by the floor.
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = door, ["sill"] = 0.02 });
        Assert.Equal(0.02, Placed(f, door).OwnSill, 9);
        Assert.Equal(0.02, Placed(f, door).Sill, 9);
        Assert.Equal(0.09, Placed(f, door).FillSill, 9);
        // Measured from the structure again, it stays where it stands and the hole is its own size.
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = door, ["sillFrom"] = "structural" });
        Assert.Equal(0.09, Placed(f, door).FillSill, 9);
        Assert.Equal(0.09, Placed(f, door).Sill, 9);
        Assert.Equal(before.Height, Placed(f, door).Height, 9);
        // One step each.
        f.Session.Undo(f.Session.Author);
        f.Session.Undo(f.Session.Author);
        f.Session.Undo(f.Session.Author);
        Assert.Equal(before.Sill, Placed(f, door).Sill, 9);
        Assert.Null(Openings.OpeningOf(f.Model, door)!.Attrs[OpeningSills.ReachKey]);
    }

    [Fact]
    public void A_door_down_to_the_structure_goes_to_IFC_with_its_hole_and_comes_back_the_same()
    {
        var (f, _, door) = DoorOnFinishedFloor();
        f.Run("architecture.setReachStructure", new JsonObject { ["id"] = door, ["on"] = true });
        var dir = Directory.CreateTempSubdirectory("lapin-reach").FullName;
        try
        {
            var path = Path.Combine(dir, "reach.ifc");
            IfcExport.Write(f.Session, path);
            var b = new Fixture();
            var result = IfcImport.Run(b.Session, path, null, out _);
            Assert.True(result.References == 0, string.Join("; ", result.Notes));
            var (p, q) = (Placed(f, door), Placed(b, door));
            Assert.Equal(p.Sill, q.Sill, 6);
            Assert.Equal(p.Height, q.Height, 6);
            Assert.Equal(p.FillSill, q.FillSill, 6);
        }
        finally { Directory.Delete(dir, true); }
    }
}
