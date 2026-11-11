using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>A room's doors and windows chosen in its configurator (docs/rooms.md): ordinary ones in the walls on the sides asked, off by default, one undo step.</summary>
public class RoomOpeningTests
{
    /// <summary>A 5 × 4 room with its four walls, found from them by a click.</summary>
    static (Fixture F, string Room) Room()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Wall(0, 0, 5, 0);
        f.Wall(5, 0, 5, 4);
        f.Wall(5, 4, 0, 4);
        f.Wall(0, 4, 0, 0);
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        return (f, room);
    }

    static JsonObject Read(Fixture f, string room) => f.Session.Query("architecture.roomOpenings", new JsonObject { ["id"] = room })!.AsObject();

    /// <summary>The side lying along y = <paramref name="y"/>.</summary>
    static int SideAlong(Fixture f, string room, double y) => Read(f, room)["sides"]!.AsArray()
        .Select(s => s!.AsObject()).First(s => Math.Abs((double)s["a"]![1]! - y) < 0.3 && Math.Abs((double)s["b"]![1]! - y) < 0.3)["index"]!.GetValue<int>();

    static JsonArray Items(params JsonObject[] items) => new([.. items.Select(i => (JsonNode)i)]);

    [Fact]
    public void A_new_room_has_none_and_the_option_off()
    {
        var (f, room) = Room();
        var read = Read(f, room);
        Assert.False((bool)read["on"]!);
        Assert.Empty(read["items"]!.AsArray());
        Assert.Equal(4, read["sides"]!.AsArray().Count);
        Assert.All(read["sides"]!.AsArray(), s => Assert.NotNull((string?)s!["wall"]));
        Assert.Empty(f.Model.OfClass(IfcClass.Door));
    }

    [Fact]
    public void A_door_and_a_window_go_in_the_walls_of_their_sides_in_one_undo()
    {
        var (f, room) = Room();
        var south = SideAlong(f, room, 0);
        var north = SideAlong(f, room, 4);
        f.Run("architecture.setRoomOpenings", new JsonObject
        {
            ["id"] = room, ["on"] = true,
            ["items"] = Items(new JsonObject { ["kind"] = "door", ["side"] = south, ["offset"] = 1 }, new JsonObject { ["kind"] = "window", ["side"] = north, ["sill"] = 0.9 }),
        });
        var door = f.Model.OfClass(IfcClass.Door).Single();
        var window = f.Model.OfClass(IfcClass.Window).Single();
        Assert.Equal(room, door.Str(RoomOpenings.FillTag));
        Assert.Equal(0, Walls.Start(Openings.WallOf(f.Model, Openings.OpeningOf(f.Model, door.Id)!.Id)!).Y, 6);
        Assert.Equal(4, Walls.Start(Openings.WallOf(f.Model, Openings.OpeningOf(f.Model, window.Id)!.Id)!).Y, 6);
        Assert.Equal(0.9, Openings.Place(f.Model, Openings.OpeningOf(f.Model, window.Id)!).Sill, 6);
        var read = Read(f, room)["items"]!.AsArray();
        Assert.Equal(2, read.Count);
        Assert.Equal(south, (int)read[0]!["side"]!);
        Assert.Equal(north, (int)read[1]!["side"]!);
        Assert.Equal(1, Math.Abs((double)read[0]!["offset"]!), 3);
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(IfcClass.Door));
        Assert.Empty(f.Model.OfClass(IfcClass.Window));
        Assert.False((bool)Read(f, room)["on"]!);
    }

    [Fact]
    public void A_door_opens_into_the_room_whichever_way_its_wall_was_drawn()
    {
        var (f, room) = Room();
        foreach (var y in new[] { 0.0, 4.0 })
            f.Run("architecture.setRoomOpenings", new JsonObject
            {
                ["id"] = room, ["on"] = true,
                ["items"] = Items(new JsonObject { ["kind"] = "door", ["side"] = SideAlong(f, room, y) }),
            });
        var door = f.Model.OfClass(IfcClass.Door).Single();
        var wall = Openings.WallOf(f.Model, Openings.OpeningOf(f.Model, door.Id)!.Id)!;
        // The north wall runs from x = 5 to x = 0, the room on its left, so its right is the outside: nothing to flip.
        Assert.False(door.Bool("flipSide"));
        Assert.Equal(4, Walls.Start(wall).Y, 6);

        var (g, other) = (new Fixture(new ArchitectureModule()), "");
        g.Wall(5, 0, 0, 0);
        g.Wall(5, 4, 5, 0);
        g.Wall(0, 4, 5, 4);
        g.Wall(0, 0, 0, 4);
        other = (string)g.Run("architecture.addRoom", new JsonObject { ["storeyId"] = g.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        g.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = other, ["on"] = true, ["items"] = Items(new JsonObject { ["kind"] = "door", ["side"] = SideAlong(g, other, 0) }) });
        // Here the room lies on the wall's right.
        Assert.True(g.Model.OfClass(IfcClass.Door).Single().Bool("flipSide"));
    }

    [Fact]
    public void Changing_the_list_keeps_the_door_by_its_key_and_takes_away_what_left()
    {
        var (f, room) = Room();
        var south = SideAlong(f, room, 0);
        f.Run("architecture.setRoomOpenings", new JsonObject
        {
            ["id"] = room, ["on"] = true,
            ["items"] = Items(new JsonObject { ["key"] = "d", ["kind"] = "door", ["side"] = south }, new JsonObject { ["key"] = "w", ["kind"] = "window", ["side"] = SideAlong(f, room, 4) }),
        });
        var door = f.Model.OfClass(IfcClass.Door).Single().Id;
        var before = f.Model.OfClass(IfcClass.OpeningElement).Count();
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = room, ["items"] = Items(new JsonObject { ["key"] = "d", ["kind"] = "door", ["side"] = south, ["offset"] = -1 }) });
        Assert.Equal(door, f.Model.OfClass(IfcClass.Door).Single().Id);
        Assert.Empty(f.Model.OfClass(IfcClass.Window));
        Assert.Equal(before - 1, f.Model.OfClass(IfcClass.OpeningElement).Count());
    }

    [Fact]
    public void Off_takes_them_away_and_on_brings_them_back_where_they_were()
    {
        var (f, room) = Room();
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = room, ["on"] = true, ["items"] = Items(new JsonObject { ["key"] = "d", ["kind"] = "door", ["side"] = SideAlong(f, room, 0), ["offset"] = 1 }) });
        var along = Openings.Place(f.Model, f.Model.OfClass(IfcClass.OpeningElement).Single()).Along;
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = room, ["on"] = false });
        Assert.Empty(f.Model.OfClass(IfcClass.Door));
        Assert.Empty(f.Model.OfClass(IfcClass.OpeningElement));
        Assert.Single(Read(f, room)["items"]!.AsArray());
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = room, ["on"] = true });
        Assert.Equal(along, Openings.Place(f.Model, f.Model.OfClass(IfcClass.OpeningElement).Single()).Along, 6);
    }

    [Fact]
    public void A_door_placed_on_its_wall_is_left_alone()
    {
        var (f, room) = Room();
        var wall = f.Model.OfClass(IfcClass.Wall).First().Id;
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1 });
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = room, ["on"] = true, ["items"] = new JsonArray() });
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = room, ["on"] = false });
        Assert.Single(f.Model.OfClass(IfcClass.Door));
    }

    [Fact]
    public void A_side_without_a_wall_says_so()
    {
        var f = new Fixture(new ArchitectureModule());
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(3, 0), new JsonArray(3, 3), new JsonArray(0, 3)) })!["id"]!;
        var ex = Assert.Throws<CommandException>(() => f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = room, ["on"] = true, ["items"] = Items(new JsonObject { ["kind"] = "door", ["side"] = 0 }) }));
        Assert.Contains("no straight wall", ex.Message);
        Assert.False((bool)Read(f, room)["on"]!);
    }

    [Fact]
    public void A_copy_of_the_room_with_its_walls_takes_over_the_copied_door_and_does_not_double_it()
    {
        var (f, room) = Room();
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = room, ["on"] = true, ["items"] = Items(new JsonObject { ["key"] = "d", ["kind"] = "door", ["side"] = SideAlong(f, room, 0) }) });
        var ids = new JsonArray([.. f.Model.OfClass(IfcClass.Wall).Select(w => (JsonNode)w.Id), room]);
        var copied = f.Run("element.transform", new JsonObject { ["ids"] = ids, ["op"] = "copy", ["by"] = new JsonArray(20, 0) })!;
        var twin = copied["ids"]!.AsArray().Select(x => (string)x!).First(x => f.Model.Get(x)!.Class == Rooms.Class);
        Assert.Equal(2, f.Model.OfClass(IfcClass.Door).Count());
        // The copy's entry names the first room's door: it is not this room's, and the point has moved with the room.
        var read = Read(f, twin)["items"]!.AsArray().Single()!;
        Assert.Null((string?)read["fill"]);
        Assert.Equal(room, f.Model.OfClass(IfcClass.Door).First().Str(RoomOpenings.FillTag));
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = twin, ["on"] = true });
        Assert.Equal(2, f.Model.OfClass(IfcClass.Door).Count());
        Assert.Equal(twin, f.Model.Get((string)Read(f, twin)["items"]!.AsArray().Single()!["fill"]!)!.Str(RoomOpenings.FillTag));
    }
}
