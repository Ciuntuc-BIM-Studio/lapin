using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A room finish in a room bounded by a curved wall (forum topic 288): the doors and windows of the arc cut the walls' finish
/// (radially, as they cut the wall), their reveals are lined and the floor runs through the door to the wall's axis, as in a
/// straight wall; the quantities read what is built. A room of straight walls is laid exactly as before.
/// </summary>
public class RoomFinishCurvedWallTests
{
    // The default wall type: render 20 mm on the right, a 250 mm core, plaster 15 mm on the left (−0.145 … 0.14 across the axis).
    const double Face = 0.14, R = 4;

    /// <summary>
    /// A half-round room: a straight wall from (0, 0) to (8, 0), and an arc of radius 4 back from (8, 0) to (0, 0) over (4, 4),
    /// both anticlockwise so their plaster is inside; a door clicked at the top of the arc, half way along it.
    /// </summary>
    static (Fixture F, string Room, string Arc, Openings.Placed Door) HalfRound()
    {
        var f = new Fixture();
        f.Wall(0, 0, 8, 0);
        var arc = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 8, ["y1"] = 0, ["x2"] = 0, ["y2"] = 0, ["arc"] = -R })!["id"]!;
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = arc, ["x"] = 4, ["y"] = 3.9 });
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 4, ["y"] = 1.5 })!["id"]!;
        return (f, room, arc, Openings.PlacedOn(f.Model, arc).Single());
    }

    static string Library(Fixture f, string name) => (string)f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = name })!["id"]!;

    static List<Entity> Finishes(Fixture f, string room) => f.Model.OfClass(RoomFinishes.Class).Where(e => e.Str("roomId") == room).ToList();

    static RoomFinishes.FinishMeasure M(Fixture f, string room, string part, string material) =>
        RoomFinishes.Measures(f.Session.Geometry(Finishes(f, room).Single(e => e.Str("part") == part).Id)).Single(x => x.Material == material);

    /// <summary>Equal to a thousandth of a square metre: the arc is drawn by its chords (no more than a millimetre off it), the room's outline and the openings' bands each by their own.</summary>
    static void Near(double expected, double actual) => Assert.InRange(actual, expected - 1e-3, expected + 1e-3);

    [Fact]
    public void A_door_in_a_curved_wall_cuts_the_walls_finish_and_its_reveals_are_lined()
    {
        var (f, room, arc, door) = HalfRound();
        Assert.Equal(Math.PI * R / 2, door.Along, 6);
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = Library(f, "Bathroom") });
        var height = Rooms.HeightOf(f.Model.Get(room)!, f.Model);

        // The plaster, 15 mm on the walls' face: the door takes out its height by its width where the plaster is, the 900 mm
        // on the axis narrowed to the plaster's middle radius (the door's sides are radial, as its hole in the wall).
        var plaster = M(f, room, "walls", "Plaster");
        var middle = R - Face - 0.015 / 2;
        var doorInPlaster = door.Width * middle / R * door.Height;
        Assert.Equal(0.9 * 3.8525 / 4 * 2.1, doorInPlaster, 9);
        Near(plaster.Gross - doorInPlaster + plaster.Reveals, plaster.Net);
        // Its reveal lined from the room's face to the frame: both jambs to the door's height, and its head.
        Assert.InRange(plaster.Reveals, 2 * door.Height * 0.05, 2 * door.Height * 0.2);

        // The tiles to 2.10 m: the door (2.10 m high) all through them too.
        var tiles = M(f, room, "walls", "Ceramic tiles");
        var tilesMiddle = R - Face - 0.015 - 0.011 / 2;
        Near(tiles.Gross - door.Width * tilesMiddle / R * Math.Min(door.Height, 2.1) + tiles.Reveals, tiles.Net);

        // The door stands at the top of the arc.
        Assert.Equal(4, PathCurve.Of(f.Model.Get(arc)!).At(door.Along, Face).X, 6);
        Assert.True(height > door.Height);
    }

    [Fact]
    public void The_floor_runs_through_a_door_in_a_curved_wall_to_its_axis()
    {
        var (f, room, _, door) = HalfRound();
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = Library(f, "Living room") });
        var laminate = M(f, room, "floor", "Laminate");
        // The threshold: between the room's face and the axis, the door's width along the arc at each radius between them.
        var threshold = door.Width * (R - Face / 2) / R * Face;
        Near(laminate.Gross + threshold, laminate.Net);
        // The plan's hatch takes the threshold too: the floor's loops and the threshold's own region.
        var o = Rooms.Live(f.Model.Get(room)!, f.Model, f.Session.Catalog)!;
        var spec = RoomFinishes.SpecOf(f.Model, Finishes(f, room).First())!;
        var regions = RoomFinishes.PlanRegions(f.Model, f.Model.Get(room)!, o, spec, "floor");
        Assert.Equal(2, regions.Count);
        Near(threshold, Math.Abs(Polygon2.SignedArea(regions[1][0])));
    }

    [Fact]
    public void A_window_in_a_curved_wall_cuts_the_walls_finish_and_its_bottom_is_lined()
    {
        var (f, room, arc, _) = HalfRound();
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = arc, ["along"] = 2 });
        var window = Openings.PlacedOn(f.Model, arc).Single(p => Math.Abs(p.Along - 2) < 1e-6);
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = Library(f, "Bathroom") });
        var door = Openings.PlacedOn(f.Model, arc).Single(p => Math.Abs(p.Along - 2) > 1e-6);
        var plaster = M(f, room, "walls", "Plaster");
        var middle = (R - Face - 0.015 / 2) / R;
        Near(plaster.Gross - (door.Width * door.Height + window.Width * window.Height) * middle + plaster.Reveals, plaster.Net);
    }

    // ------------------------------------------------------------ straight walls: as before

    /// <summary>
    /// A room of straight walls with a door and a window, its finishes laid: every part's volume and bounds and every measure,
    /// to the nanometre, as the build before curved walls' openings were cut (25.11.2026, 9d4008538).
    /// </summary>
    static string Fingerprint(string finish)
    {
        var f = new Fixture();
        var walls = new[] { ((0.0, 0.0), (5.0, 0.0)), ((5.0, 0.0), (5.0, 4.0)), ((5.0, 4.0), (0.0, 4.0)), ((0.0, 4.0), (0.0, 0.0)) }
            .Select(x => f.Wall(x.Item1.Item1, x.Item1.Item2, x.Item2.Item1, x.Item2.Item2)).ToArray();
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["x"] = 2, ["y"] = 0 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = walls[1], ["along"] = 2 });
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = Library(f, finish) });
        static string N(double v) => Math.Round(v, 9).ToString("0.000000000", CultureInfo.InvariantCulture);
        var lines = new List<string>();
        foreach (var e in Finishes(f, room).OrderBy(e => e.Str("part")))
        {
            var g = f.Session.Geometry(e.Id)!;
            foreach (var p in g.Parts)
            {
                var b = Measure.Bounds(p.Solid)!.Value;
                lines.Add($"{p.Key} {p.Name} {N(Measure.Volume(p.Solid))} {N(b.Min.X)} {N(b.Min.Y)} {N(b.Min.Z)} {N(b.Max.X)} {N(b.Max.Y)} {N(b.Max.Z)}");
            }
            foreach (var x in RoomFinishes.Measures(g)) lines.Add($"{x.Part} {x.Layer} {x.Material} {N(x.Gross)} {N(x.Net)} {N(x.Volume)} {N(x.Reveals)}");
            var o = Rooms.Live(f.Model.Get(room)!, f.Model, f.Session.Catalog)!;
            var spec = RoomFinishes.SpecOf(f.Model, e)!;
            foreach (var r in RoomFinishes.PlanRegions(f.Model, f.Model.Get(room)!, o, spec, e.Str("part")!))
                lines.Add("region " + string.Join(" | ", r.Select(l => string.Join(" ", l.Select(q => $"{N(q.X)},{N(q.Y)}")))));
        }
        return string.Join("\n", lines);
    }

    [Theory]
    [InlineData("Bathroom")]
    [InlineData("Living room")]
    public void A_room_of_straight_walls_is_finished_exactly_as_before(string finish)
    {
        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "room-finish-straight", $"{finish}.txt")).ReplaceLineEndings("\n").TrimEnd();
        Assert.Equal(expected, Fingerprint(finish));
    }

    /// <summary>Writes the fingerprints (LAPIN_WRITE_FINGERPRINTS=dir): run once on the build before, to make the expected files.</summary>
    [Fact]
    public void Fingerprints_are_written_when_asked()
    {
        if (Environment.GetEnvironmentVariable("LAPIN_WRITE_FINGERPRINTS") is not { Length: > 0 } dir) return;
        Directory.CreateDirectory(dir);
        foreach (var finish in new[] { "Bathroom", "Living room" }) File.WriteAllText(Path.Combine(dir, $"{finish}.txt"), Fingerprint(finish) + "\n");
    }
}
