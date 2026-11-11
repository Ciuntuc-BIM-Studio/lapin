using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Apartment types at their limit (docs/plan-units.md, step B): a flat's inner walls stop in the building's walls, per
/// instance and never carried to the others, and the doors and windows its rooms carry stand in the building's walls at
/// every instance. Then the same flats through Copy to storeys and IFC.
/// </summary>
public class UnitBoundaryTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-units").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static JsonArray P(double x, double y) => new(x, y);
    static JsonArray Ids(params string[] ids) => new([.. ids.Select(x => (JsonNode)x)]);

    sealed record Block(Fixture F, string South, string North, string[] Party, string Group, string Partition, string Stub, string RoomWest, string Component, string FacadeType);

    static List<Entity> Members(Fixture f, string group) => Groups.MembersOf(f.Model, group).Select(f.Model.Get).OfType<Entity>().ToList();
    static List<Entity> WallsOf(Fixture f, string group) => Members(f, group).Where(e => e.Class == IfcClass.Wall).ToList();
    static Entity RoomOf(Fixture f, string group, string key) => Members(f, group).Single(e => e.Class == Rooms.Class && e.Str(Components.MemberKey) == key);
    static Entity Twin(Fixture f, string group, string key) => Members(f, group).Single(e => e.Str(Components.MemberKey) == key);
    static string Key(Fixture f, string id) => f.Model.Get(id)!.Str(Components.MemberKey)!;
    static string Attrs(Fixture f, string id) => f.Model.Get(id)!.Attrs.ToJsonString();
    static string TwinOf(Fixture f, JsonNode made) => made["ids"]!.AsArray().Select(x => (string)x!).First(x => f.Model.Get(x)!.Class == Groups.Class);
    static string CopyBy(Fixture f, string group, double dx, double dy = 0) => TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "copy", ["by"] = P(dx, dy) })!);
    static List<Relation> Stops(Fixture f, string wall) => [.. Joins.Of(f.Model, wall).Where(r => r.Attrs?[Joins.StopsOn] != null)];

    /// <summary>
    /// A block 24 × 6 of three 8 m slots: a facade 300 thick, party walls at x = 8 and x = 16. In the first slot a flat: a
    /// partition at x = 4 from facade to facade, a stub from it to the party wall at y = 3, three rooms. A type of them.
    /// </summary>
    static Block Build()
    {
        var f = new Fixture(new ArchitectureModule());
        var facade = f.Type("Facade 30", ("Brick", 0.3, true));
        var inner = f.Type("Partition 10", ("Brick", 0.1, true));
        var south = f.Wall(0, 0, 24, 0, facade);
        f.Wall(24, 0, 24, 6, facade);
        var north = f.Wall(24, 6, 0, 6, facade);
        f.Wall(0, 6, 0, 0, facade);
        var party = new[] { f.Wall(8, 0, 8, 6, facade), f.Wall(16, 0, 16, 6, facade) };
        var partition = f.Wall(4, 0, 4, 6, inner);
        var stub = f.Wall(4, 3, 8, 3, inner);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = partition, ["along"] = 1.5 })!["id"]!;
        Assert.NotNull(door);
        var rooms = new[] { (2.0, 3.0), (6.0, 1.5), (6.0, 4.5) }.Select(p => (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = p.Item1, ["y"] = p.Item2 })!["id"]!).ToArray();
        var made = f.Run("component.make", new JsonObject { ["ids"] = Ids([partition, stub, .. rooms]), ["kind"] = "unit", ["name"] = "Flat A" })!;
        return new Block(f, south, north, party, (string)made["id"]!, partition, stub, rooms[0], (string)made["component"]!, facade);
    }

    /// <summary>A door of the west room, in the facade to its south, 1 m off the side's middle.</summary>
    static void DoorOnSouth(Block b, double offset = 0.5)
    {
        var side = b.F.Session.Query("architecture.roomOpenings", new JsonObject { ["id"] = b.RoomWest })!["sides"]!.AsArray()
            .Select(s => s!.AsObject()).First(s => (double)s["a"]![1]! < 1 && (double)s["b"]![1]! < 1)["index"]!.GetValue<int>();
        b.F.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = b.RoomWest, ["on"] = true, ["items"] = new JsonArray(new JsonObject { ["key"] = "in", ["kind"] = "door", ["side"] = side, ["offset"] = offset }) });
    }

    /// <summary>What a room carries: its doors tagged with it.</summary>
    static List<Entity> Carried(Fixture f, Entity room) => [.. f.Model.OfClass(IfcClass.Door).Concat(f.Model.OfClass(IfcClass.Window)).Where(d => d.Str(RoomOpenings.FillTag) == room.Id)];
    static List<Entity> RoomDoors(Fixture f) => [.. f.Model.OfClass(IfcClass.Door).Where(d => d.Str(RoomOpenings.FillTag) != null)];
    static List<Entity> RoomDoorsOn(Fixture f, string storey) => [.. RoomDoors(f).Where(d => Spatial.StoreyOf(f.Model, d.Id)?.Id == storey)];
    static (Entity Wall, double Along) WhereIs(Fixture f, Entity fill)
    {
        var opening = Openings.OpeningOf(f.Model, fill.Id)!;
        return (Openings.WallOf(f.Model, opening.Id)!, Openings.Place(f.Model, opening).Along);
    }

    // ─── The inner walls at the limit ────────────────────────────────────

    [Fact]
    public void A_copied_flat_stops_its_inner_walls_in_the_buildings_walls_and_leaves_them_alone()
    {
        var b = Build();
        var f = b.F;
        var building = new[] { b.South, b.North, b.Party[0], b.Party[1] }.ToDictionary(id => id, id => Attrs(f, id));
        var twin = CopyBy(f, b.Group, 8);
        var partition = Twin(f, twin, Key(f, b.Partition));
        var stub = Twin(f, twin, Key(f, b.Stub));

        // The partition in a T on both facades, the facade through.
        var stops = Stops(f, partition.Id);
        Assert.Equal(2, stops.Count);
        Assert.All(stops, r => { Assert.Equal("T", (string?)r.Attrs!["kind"]); Assert.Contains(r.Relating, new[] { b.South, b.North }); Assert.Equal(partition.Id, r.Related[0]); });
        // Its body stops at the facades' faces.
        var (min, max) = f.Bounds(partition.Id);
        Assert.Equal(0.15, min.Y, 3);
        Assert.Equal(5.85, max.Y, 3);
        // The stub in the next party wall, at its face; inside, joined to the partition as in the type.
        Assert.Contains(Stops(f, stub.Id), r => r.Relating == b.Party[1]);
        Assert.Equal(15.85, f.Bounds(stub.Id).Max.X, 3);
        Assert.NotNull(Joins.Between(f.Model, partition.Id, stub.Id));
        // The building is as it was.
        Assert.All(building, p => Assert.Equal(p.Value, Attrs(f, p.Key)));
    }

    [Fact]
    public void A_mirrored_and_a_turned_copy_stop_in_the_walls_they_land_at()
    {
        var b = Build();
        var f = b.F;
        var mirrored = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(b.Group), ["op"] = "mirror", ["a"] = P(8, 0), ["b"] = P(8, 1), ["copy"] = true })!);
        var stub = Twin(f, mirrored, Key(f, b.Stub));
        Assert.Contains(Stops(f, stub.Id), r => r.Relating == b.Party[0]);
        Assert.Equal(8.15, f.Bounds(stub.Id).Min.X, 3);
        Assert.Equal(2, Stops(f, Twin(f, mirrored, Key(f, b.Partition)).Id).Count);

        // Turned half a turn about the third slot's middle: its stub goes west, to the party wall at x = 16.
        var turned = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(b.Group), ["op"] = "rotate", ["center"] = P(12, 3), ["angle"] = 180, ["copy"] = true })!);
        var partitionThere = Twin(f, turned, Key(f, b.Partition));
        Assert.Equal(20, Walls.Start(partitionThere).X, 6);
        Assert.Equal(2, Stops(f, partitionThere.Id).Count);
        Assert.Contains(Stops(f, Twin(f, turned, Key(f, b.Stub)).Id), r => r.Relating == b.Party[1]);
    }

    [Fact]
    public void A_flat_moved_to_another_slot_leaves_the_old_walls_and_stops_in_the_new()
    {
        var b = Build();
        var f = b.F;
        var twin = CopyBy(f, b.Group, 8);
        var stubId = Twin(f, twin, Key(f, b.Stub)).Id;
        Assert.Contains(Stops(f, stubId), r => r.Relating == b.Party[1]);
        var party = Attrs(f, b.Party[1]);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "move", ["by"] = P(8, 0) });
        // In the third slot the stub ends at x = 24, the facade's east wall.
        Assert.DoesNotContain(Stops(f, stubId), r => r.Relating == b.Party[1]);
        Assert.Single(Stops(f, stubId));
        Assert.Equal(23.85, f.Bounds(stubId).Max.X, 3);
        Assert.Equal(party, Attrs(f, b.Party[1]));
        // One undo step.
        f.Session.Undo();
        Assert.Contains(Stops(f, stubId), r => r.Relating == b.Party[1]);
    }

    [Fact]
    public void A_flat_moved_away_does_not_drag_a_building_wall_ending_on_it()
    {
        var b = Build();
        var f = b.F;
        // A building wall drawn up to the partition: its end joins the partition's run.
        var lean = f.Wall(0, 1, 4, 1);
        Assert.NotNull(Joins.Between(f.Model, lean, b.Partition));
        var before = Attrs(f, lean);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(b.Group), ["op"] = "move", ["by"] = P(0, 20) });
        Assert.Equal(before, Attrs(f, lean));
        Assert.Null(Joins.Between(f.Model, lean, b.Partition));
    }

    [Fact]
    public void A_building_wall_moved_is_followed_in_that_instance_only_and_is_no_edit_of_the_type()
    {
        var b = Build();
        var f = b.F;
        var twin = CopyBy(f, b.Group, 8);
        var stubThere = Twin(f, twin, Key(f, b.Stub)).Id;
        var stubHere = Attrs(f, b.Stub);
        var ids = Members(f, b.Group).Concat(Members(f, twin)).Select(e => e.Id).ToHashSet();
        // The party wall at x = 16 goes 200 mm east: only the second flat reaches it.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(b.Party[1]), ["op"] = "move", ["by"] = P(0.2, 0) });
        Assert.Equal(16.2, Walls.End(f.Model.Get(stubThere)!).X, 6);
        Assert.Equal(16.05, f.Bounds(stubThere).Max.X, 3);
        Assert.Equal(stubHere, Attrs(f, b.Stub));
        Assert.Equal(ids, Members(f, b.Group).Concat(Members(f, twin)).Select(e => e.Id).ToHashSet());
    }

    [Fact]
    public void A_building_wall_slid_along_itself_leaves_the_flats_walls_straight()
    {
        var b = Build();
        var f = b.F;
        var before = Attrs(f, b.Partition);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(b.South), ["op"] = "move", ["by"] = P(0.5, 0) });
        Assert.Equal(4, Walls.Start(f.Model.Get(b.Partition)!).X, 6);
        Assert.Equal(4, Walls.End(f.Model.Get(b.Partition)!).X, 6);
        Assert.Equal(before, Attrs(f, b.Partition));
    }

    [Fact]
    public void An_edit_in_one_flat_keeps_each_flats_own_limit()
    {
        var b = Build();
        var f = b.F;
        var twin = CopyBy(f, b.Group, 8);
        var stubThere = Twin(f, twin, Key(f, b.Stub)).Id;
        f.Run("element.update", new JsonObject { ["id"] = b.Stub, ["attrs"] = new JsonObject { ["height"] = 2.5 } });
        // Followed under the same id, and stopped in its own party wall again.
        Assert.Equal(2.5, f.Model.Get(stubThere)!.Num("height"), 6);
        Assert.Contains(Stops(f, stubThere), r => r.Relating == b.Party[1]);
        Assert.Contains(Joins.Of(f.Model, b.Stub), r => r.Relating == b.Party[0]);
        Assert.Equal(15.85, f.Bounds(stubThere).Max.X, 3);
    }

    [Fact]
    public void Near_a_corner_the_building_wall_is_not_shortened()
    {
        var f = new Fixture(new ArchitectureModule());
        var south = f.Wall(0, 0, 8, 0, f.Type("Facade 30", ("Brick", 0.3, true)));
        var inner = f.Type("Partition 10", ("Brick", 0.1, true));
        var w = f.Wall(40.4, 0.2, 40.4, 3, inner);
        var made = f.Run("component.make", new JsonObject { ["ids"] = Ids(w), ["kind"] = "unit" })!;
        var before = Attrs(f, south);
        var twin = CopyBy(f, (string)made["id"]!, -40);
        var copy = WallsOf(f, twin).Single();
        Assert.Equal(south, Assert.Single(Stops(f, copy.Id)).Relating);
        Assert.Equal(before, Attrs(f, south));
        Assert.Equal(0.15, f.Bounds(copy.Id).Min.Y, 3);
        // The facade's body starts where it did.
        Assert.Equal(0, f.Bounds(south).Min.X, 3);
        Assert.Equal(8, f.Bounds(south).Max.X, 3);
    }

    [Fact]
    public void Flats_do_not_join_each_others_inner_walls()
    {
        var f = new Fixture(new ArchitectureModule());
        var w = f.Wall(0, 0, 4, 0, f.Type("Partition 10", ("Brick", 0.1, true)));
        var made = f.Run("component.make", new JsonObject { ["ids"] = Ids(w), ["kind"] = "unit" })!;
        var twin = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids((string)made["id"]!), ["op"] = "rotate", ["center"] = P(4, 0), ["angle"] = 90, ["copy"] = true })!);
        Assert.Empty(Joins.Of(f.Model, WallsOf(f, twin).Single().Id));
    }

    [Fact]
    public void A_flat_placed_before_its_building_stops_when_the_wall_is_drawn()
    {
        var b = Build();
        var f = b.F;
        var far = CopyBy(f, b.Group, 0, 50);
        var partition = Twin(f, far, Key(f, b.Partition)).Id;
        Assert.Empty(Stops(f, partition));
        var s = f.Wall(-1, 50, 9, 50, b.FacadeType);
        Assert.Contains(Stops(f, partition), r => r.Relating == s);
        // Taken away, the wall leaves it free, nothing else changed.
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(s) });
        Assert.Empty(Stops(f, partition));
        Assert.NotNull(f.Model.Get(partition));
    }

    // ─── The doors and windows the rooms carry ───────────────────────────

    [Fact]
    public void A_room_door_stands_in_the_building_wall_at_every_instance()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        var key = Key(f, b.RoomWest);
        var here = Assert.Single(Carried(f, f.Model.Get(b.RoomWest)!));
        Assert.Equal(b.South, WhereIs(f, here).Wall.Id);

        var twin = CopyBy(f, b.Group, 8);
        var there = Assert.Single(Carried(f, RoomOf(f, twin, key)));
        var (wall, along) = WhereIs(f, there);
        Assert.Equal(b.South, wall.Id);
        Assert.Equal(WhereIs(f, here).Along + 8, along, 3);
        Assert.NotEqual(here.Id, there.Id);
        // It is the building's wall that holds them: neither is a member of a flat.
        Assert.Null(Groups.Of(f.Model, b.South));
        Assert.Equal(2, RoomDoors(f).Count);
        // One step: undone, the copy's door goes with the copy.
        f.Session.Undo();
        Assert.Single(RoomDoors(f));
    }

    [Fact]
    public void Changing_a_rooms_list_reaches_the_door_of_every_instance_under_its_own_id()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        var key = Key(f, b.RoomWest);
        var twin = CopyBy(f, b.Group, 8);
        var there = Assert.Single(Carried(f, RoomOf(f, twin, key))).Id;
        var along = WhereIs(f, f.Model.Get(there)!).Along;

        // Moved along its side in the first: the second's door moves too, the same door.
        DoorOnSouth(b, -0.5);
        Assert.Equal(there, Assert.Single(Carried(f, RoomOf(f, twin, key))).Id);
        Assert.Equal(1, Math.Abs(along - WhereIs(f, f.Model.Get(there)!).Along), 3);

        // Off in the first: gone in both, the list kept.
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = b.RoomWest, ["on"] = false });
        Assert.Empty(Carried(f, RoomOf(f, twin, key)));
        Assert.Empty(RoomDoors(f));

        // On again: back in both.
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = b.RoomWest, ["on"] = true });
        Assert.Single(Carried(f, RoomOf(f, twin, key)));

        // Taken off the list: gone in both.
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = b.RoomWest, ["items"] = new JsonArray() });
        Assert.Empty(RoomDoors(f));
    }

    [Fact]
    public void A_mirrored_instances_door_is_the_other_hand_and_still_opens_into_its_room()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        var key = Key(f, b.RoomWest);
        var here = Assert.Single(Carried(f, f.Model.Get(b.RoomWest)!));
        var twin = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(b.Group), ["op"] = "mirror", ["a"] = P(8, 0), ["b"] = P(8, 1), ["copy"] = true })!);
        var there = Assert.Single(Carried(f, RoomOf(f, twin, key)));
        // The same wall, the mirror across it: the hand changes, the side it opens to does not.
        var (wall, along) = WhereIs(f, there);
        Assert.Equal(b.South, wall.Id);
        Assert.Equal(16 - WhereIs(f, here).Along, along, 3);
        Assert.NotEqual(here.Bool("flipHand"), there.Bool("flipHand"));
        Assert.Equal(here.Bool("flipSide"), there.Bool("flipSide"));
    }

    [Fact]
    public void A_door_turned_by_hand_follows_in_the_others_and_stays_after_a_move()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        var key = Key(f, b.RoomWest);
        var twin = CopyBy(f, b.Group, 8);
        var here = Assert.Single(Carried(f, f.Model.Get(b.RoomWest)!));
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = Ids(here.Id), ["axis"] = "x" });
        var flipped = f.Model.Get(here.Id)!.Bool("flipHand");
        Assert.Equal(flipped, Assert.Single(Carried(f, RoomOf(f, twin, key))).Bool("flipHand"));
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "move", ["by"] = P(8, 0) });
        Assert.Equal(flipped, Assert.Single(Carried(f, RoomOf(f, twin, key))).Bool("flipHand"));
    }

    [Fact]
    public void A_door_moved_along_its_wall_by_hand_is_an_edit_of_the_type()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        var key = Key(f, b.RoomWest);
        var twin = CopyBy(f, b.Group, 8);
        var here = Assert.Single(Carried(f, f.Model.Get(b.RoomWest)!));
        var there = Assert.Single(Carried(f, RoomOf(f, twin, key)));
        var (_, a0) = WhereIs(f, here);
        var (_, b0) = WhereIs(f, there);
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = here.Id, ["along"] = a0 - 0.4 });
        Assert.Equal(a0 - 0.4, WhereIs(f, f.Model.Get(here.Id)!).Along, 3);
        Assert.Equal(b0 - 0.4, WhereIs(f, f.Model.Get(there.Id)!).Along, 3);
    }

    [Fact]
    public void A_door_deleted_by_hand_leaves_the_list_of_every_instance()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        var key = Key(f, b.RoomWest);
        var twin = CopyBy(f, b.Group, 8);
        var here = Assert.Single(Carried(f, f.Model.Get(b.RoomWest)!));
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(here.Id) });
        Assert.Empty(Carried(f, RoomOf(f, twin, key)));
        Assert.Empty(f.Session.Query("architecture.roomOpenings", new JsonObject { ["id"] = b.RoomWest })!["items"]!.AsArray());
    }

    [Fact]
    public void A_flat_moved_along_the_facade_moves_its_door_and_one_moved_to_another_wall_remakes_it()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        var key = Key(f, b.RoomWest);
        var twin = CopyBy(f, b.Group, 8);
        var door = Assert.Single(Carried(f, RoomOf(f, twin, key))).Id;
        var along = WhereIs(f, f.Model.Get(door)!).Along;
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "move", ["by"] = P(8, 0) });
        Assert.Equal(door, Assert.Single(Carried(f, RoomOf(f, twin, key))).Id);
        Assert.Equal(along + 8, WhereIs(f, f.Model.Get(door)!).Along, 3);

        // Far from every wall, it has nowhere to stand: nothing is refused, the list stays and says so.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "move", ["by"] = P(0, 50) });
        Assert.Empty(Carried(f, RoomOf(f, twin, key)));
        var item = f.Session.Query("architecture.roomOpenings", new JsonObject { ["id"] = RoomOf(f, twin, key).Id })!["items"]!.AsArray().Single()!;
        Assert.True((bool)item["noWall"]!);
        // The old place is whole again.
        Assert.Single(RoomDoors(f));
    }

    [Fact]
    public void A_door_the_wall_refuses_does_not_stop_the_copy()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        // Something of the building's already where the copy's door would go.
        var along = WhereIs(f, Assert.Single(Carried(f, f.Model.Get(b.RoomWest)!))).Along + 8;
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = b.South, ["along"] = along });
        var twin = CopyBy(f, b.Group, 8);
        Assert.Empty(Carried(f, RoomOf(f, twin, Key(f, b.RoomWest))));
        Assert.Single(f.Model.OfClass(IfcClass.Window));
    }

    [Fact]
    public void A_room_door_in_a_wall_of_the_flat_itself_is_not_doubled_by_a_copy()
    {
        var f = new Fixture(new ArchitectureModule());
        var walls = new[] { f.Wall(0, 0, 5, 0), f.Wall(5, 0, 5, 4), f.Wall(5, 4, 0, 4), f.Wall(0, 4, 0, 0) };
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        var side = f.Session.Query("architecture.roomOpenings", new JsonObject { ["id"] = room })!["sides"]!.AsArray()
            .Select(s => s!.AsObject()).First(s => (double)s["a"]![1]! < 1 && (double)s["b"]![1]! < 1)["index"]!.GetValue<int>();
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = room, ["on"] = true, ["items"] = new JsonArray(new JsonObject { ["key"] = "d", ["kind"] = "door", ["side"] = side }) });
        var made = f.Run("component.make", new JsonObject { ["ids"] = Ids([.. walls, room]), ["kind"] = "unit" })!;
        var twin = CopyBy(f, (string)made["id"]!, 20);
        Assert.Equal(2, f.Model.OfClass(IfcClass.Door).Count());
        var otherRoom = Members(f, twin).Single(e => e.Class == Rooms.Class);
        var door = Assert.Single(Carried(f, otherRoom));
        Assert.Contains(WallsOf(f, twin), w => w.Id == WhereIs(f, door).Wall.Id);
    }

    [Fact]
    public void A_door_given_another_type_by_hand_is_an_edit_of_the_type_and_not_put_back()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        var key = Key(f, b.RoomWest);
        var twin = CopyBy(f, b.Group, 8);
        var wide = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultDoor("Door 1100") with { Width = 1.1 }).ToJson() })!["id"]!;
        var here = Assert.Single(Carried(f, f.Model.Get(b.RoomWest)!));
        f.Run("architecture.setJoineryType", new JsonObject { ["ids"] = Ids(here.Id), ["typeId"] = wide });
        Assert.Equal(wide, f.Model.RelatingOf(here.Id, IfcClass.RelDefinesByType)!.Id);
        Assert.Equal(wide, f.Model.RelatingOf(Assert.Single(Carried(f, RoomOf(f, twin, key))).Id, IfcClass.RelDefinesByType)!.Id);
        // A later edit of the room leaves it so.
        f.Run("element.update", new JsonObject { ["id"] = b.RoomWest, ["attrs"] = new JsonObject { ["department"] = "Living" } });
        Assert.Equal(wide, f.Model.RelatingOf(here.Id, IfcClass.RelDefinesByType)!.Id);
    }

    [Fact]
    public void Saved_and_opened_again_the_limit_and_the_doors_are_as_they_were()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        var key = Key(f, b.RoomWest);
        var twin = CopyBy(f, b.Group, 8);
        var path = Path.Combine(_dir, "block.lapin");
        PackageFiles.Save(f.Session, path);

        var g = new Fixture(new ArchitectureModule());
        PackageFiles.Open(g.Session, path);
        var stub = Twin(g, twin, Key(f, b.Stub));
        Assert.Contains(Stops(g, stub.Id), r => r.Relating == b.Party[1]);
        var door = Assert.Single(Carried(g, RoomOf(g, twin, key)));
        Assert.Equal(b.South, WhereIs(g, door).Wall.Id);
        // Still instances, still each its own door.
        g.Run("element.update", new JsonObject { ["id"] = b.Stub, ["attrs"] = new JsonObject { ["height"] = 2.5 } });
        Assert.Equal(2.5, Twin(g, twin, Key(f, b.Stub)).Num("height"), 6);
        Assert.Equal(door.Id, Assert.Single(Carried(g, RoomOf(g, twin, key))).Id);
    }

    [Fact]
    public void A_block_of_many_flats_follows_an_edit_in_reasonable_time()
    {
        var f = new Fixture(new ArchitectureModule());
        var facade = f.Type("Facade 30", ("Brick", 0.3, true));
        var inner = f.Type("Partition 10", ("Brick", 0.1, true));
        const int Flats = 60;
        var south = f.Wall(0, 0, 8 * Flats, 0, facade);
        f.Wall(8 * Flats, 6, 0, 6, facade);
        for (var i = 0; i <= Flats; i++) f.Wall(8 * i, 0, 8 * i, 6, facade);
        var partition = f.Wall(4, 0, 4, 6, inner);
        var stub = f.Wall(4, 3, 8, 3, inner);
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3 })!["id"]!;
        var side = f.Session.Query("architecture.roomOpenings", new JsonObject { ["id"] = room })!["sides"]!.AsArray()
            .Select(s => s!.AsObject()).First(s => (double)s["a"]![1]! < 1 && (double)s["b"]![1]! < 1)["index"]!.GetValue<int>();
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = room, ["on"] = true, ["items"] = new JsonArray(new JsonObject { ["key"] = "in", ["kind"] = "door", ["side"] = side }) });
        var made = f.Run("component.make", new JsonObject { ["ids"] = Ids(partition, stub, room), ["kind"] = "unit" })!;
        var group = (string)made["id"]!;
        var copying = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 1; i < Flats; i++) CopyBy(f, group, 8 * i);
        copying.Stop();
        Assert.Equal(Flats, Components.Instances(f.Model, (string)made["component"]!).Count());
        Assert.Equal(Flats, RoomDoors(f).Count);
        Assert.All(Components.Instances(f.Model, (string)made["component"]!), g => Assert.Equal(2, Stops(f, Members(f, g.Id).Single(e => e.Str(Components.MemberKey) == Key(f, partition)).Id).Count));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        f.Run("element.update", new JsonObject { ["id"] = stub, ["attrs"] = new JsonObject { ["height"] = 2.5 } });
        clock.Stop();
        var wall = System.Diagnostics.Stopwatch.StartNew();
        f.Run("element.update", new JsonObject { ["id"] = south, ["attrs"] = new JsonObject { ["height"] = 3.1 } });
        wall.Stop();
        Console.WriteLine($"{Flats} flats: copying {copying.ElapsedMilliseconds} ms, one wall carried {clock.ElapsedMilliseconds} ms, a building wall edited {wall.ElapsedMilliseconds} ms");
        Assert.True(clock.ElapsedMilliseconds < 30_000, $"{clock.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void A_door_of_a_room_outside_every_instance_stays_that_rooms_in_a_project_with_flats()
    {
        var b = Build();
        var f = b.F;
        // The third slot's rooms belong to no flat: a room there, with its own door.
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 20, ["y"] = 3 })!["id"]!;
        var side = f.Session.Query("architecture.roomOpenings", new JsonObject { ["id"] = room })!["sides"]!.AsArray()
            .Select(s => s!.AsObject()).First(s => (double)s["a"]![1]! < 1 && (double)s["b"]![1]! < 1)["index"]!.GetValue<int>();
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = room, ["on"] = true, ["items"] = new JsonArray(new JsonObject { ["key"] = "d", ["kind"] = "door", ["side"] = side }) });
        var door = Assert.Single(Carried(f, f.Model.Get(room)!));
        Assert.NotNull(f.Session.Query("architecture.roomOpenings", new JsonObject { ["id"] = room })!["items"]!.AsArray().Single()!["fill"]);
        // Moved by hand: as it was before flats, the door moves alone and the list is not touched.
        var list = f.Model.Get(room)!.Attrs["roomOpenings"]!.ToJsonString();
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = door.Id, ["along"] = WhereIs(f, door).Along + 0.3 });
        Assert.Equal(list, f.Model.Get(room)!.Attrs["roomOpenings"]!.ToJsonString());
    }

    [Fact]
    public void A_flat_moved_together_with_the_building_wall_goes_where_it_was_sent()
    {
        var b = Build();
        var f = b.F;
        var (x, y) = (Walls.Start(f.Model.Get(b.Partition)!).X, Walls.End(f.Model.Get(b.Partition)!).Y);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(b.Group, b.South, b.North), ["op"] = "move", ["by"] = P(0.5, 1) });
        var moved = f.Model.Get(b.Partition)!;
        Assert.Equal(x + 0.5, Walls.Start(moved).X, 6);
        Assert.Equal(y + 1, Walls.End(moved).Y, 6);
        Assert.Equal(2, Stops(f, b.Partition).Count);
    }

    [Fact]
    public void A_member_moved_with_the_building_wall_in_one_flat_is_an_edit_of_the_type()
    {
        var b = Build();
        var f = b.F;
        var twin = CopyBy(f, b.Group, 8);
        // The members alone, not the instance as a whole: the partition 300 mm east, with the south facade.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(b.Partition, b.South), ["op"] = "move", ["by"] = P(0.3, 0) });
        Assert.Equal(4.3, Walls.Start(f.Model.Get(b.Partition)!).X, 6);
        Assert.Equal(12.3, Walls.Start(Twin(f, twin, Key(f, b.Partition))).X, 6);
    }

    [Fact]
    public void A_mirrored_copy_of_a_copy_follows_an_edit_as_its_mirror()
    {
        var f = new Fixture(new ArchitectureModule());
        var south = f.Wall(0, 0, 21, 0);
        f.Wall(21, 0, 21, 6);
        f.Wall(21, 6, 0, 6);
        f.Wall(0, 6, 0, 0);
        f.Wall(7, 0, 7, 6);
        f.Wall(14, 0, 14, 6);
        var partition = f.Wall(4, 0, 4, 6);
        var stub = f.Wall(4, 2.5, 7, 2.5);
        var made = f.Run("component.make", new JsonObject { ["ids"] = Ids(partition, stub), ["kind"] = "unit" })!;
        var first = (string)made["id"]!;
        var second = CopyBy(f, first, 7);
        var third = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(second), ["op"] = "mirror", ["a"] = P(14, -0.8), ["b"] = P(14, 6.8), ["copy"] = true })!);
        var key = Key(f, partition);
        Assert.Equal(17, Walls.Start(Twin(f, third, key)).X, 6);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(partition), ["op"] = "move", ["by"] = P(0.5, 0) });
        Assert.Equal(4.5, Walls.Start(f.Model.Get(partition)!).X, 6);
        Assert.Equal(11.5, Walls.Start(Twin(f, second, key)).X, 6);
        Assert.Equal(16.5, Walls.Start(Twin(f, third, key)).X, 6);
        Assert.Equal(16.5, Walls.End(Twin(f, third, key)).X, 6);
        // The stub follows its partition, in each.
        Assert.Equal(16.5, Walls.Start(Twin(f, third, Key(f, stub))).X, 6);
        Assert.Equal(14, Walls.End(Twin(f, third, Key(f, stub))).X, 6);
        Assert.NotNull(south);
    }

    // ─── Copy to storeys ─────────────────────────────────────────────────

    /// <summary>The block's facade and party walls again on a storey above.</summary>
    static (string Upper, string South) Upper(Block b)
    {
        var f = b.F;
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        var walls = f.Model.RelatedOf(f.Storey, IfcClass.RelContainedInSpatialStructure).Where(e => e.Class == IfcClass.Wall && Groups.Of(f.Model, e.Id) == null).Select(e => e.Id).ToArray();
        var copied = f.Run("element.copyToStoreys", new JsonObject { ["ids"] = Ids(walls), ["storeyIds"] = new JsonArray(upper), ["hosted"] = false })!;
        var south = copied["ids"]!.AsArray().Select(x => f.Model.Get((string)x!)!).First(w => Math.Abs(Walls.Start(w).Y) < 1e-6 && Math.Abs(Walls.End(w).Y) < 1e-6).Id;
        return (upper, south);
    }

    [Fact]
    public void A_flat_copied_to_a_storey_stays_an_instance_with_its_walls_doors_and_limit()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        var (upper, south) = Upper(b);
        var result = f.Run("element.copyToStoreys", new JsonObject { ["ids"] = Ids(b.Group), ["storeyIds"] = new JsonArray(upper) })!;
        var twin = TwinOf(f, result);
        Assert.Equal(b.Component, Components.Of(f.Model.Get(twin)!));
        Assert.Equal(Members(f, b.Group).Count, Members(f, twin).Count);
        Assert.All(Members(f, twin), e => Assert.Equal(upper, Spatial.StoreyOf(f.Model, e.Id)!.Id));
        // The door the partition hosts came with it.
        var partition = Twin(f, twin, Key(f, b.Partition));
        Assert.Single(Openings.Of(f.Model, partition.Id));
        // The inner walls stop in that storey's walls, at their faces.
        Assert.Contains(Stops(f, partition.Id), r => r.Relating == south);
        Assert.Equal(0.15, f.Bounds(partition.Id).Min.Y, 3);
        Assert.Equal(3, f.Bounds(partition.Id).Min.Z, 3);
        // The room's door stands in that storey's facade.
        var door = Assert.Single(Carried(f, RoomOf(f, twin, Key(f, b.RoomWest))));
        Assert.Equal(south, WhereIs(f, door).Wall.Id);
        Assert.Equal(upper, Spatial.StoreyOf(f.Model, door.Id)!.Id);
    }

    [Fact]
    public void An_edit_reaches_the_flat_on_the_other_storey_under_its_ids_there()
    {
        var b = Build();
        var f = b.F;
        var (upper, _) = Upper(b);
        var twin = TwinOf(f, f.Run("element.copyToStoreys", new JsonObject { ["ids"] = Ids(b.Group), ["storeyIds"] = new JsonArray(upper) })!);
        var ids = Members(f, twin).Select(e => e.Id).ToHashSet();
        f.Run("element.update", new JsonObject { ["id"] = b.Stub, ["attrs"] = new JsonObject { ["height"] = 2.5 } });
        var stub = Twin(f, twin, Key(f, b.Stub));
        Assert.Equal(2.5, stub.Num("height"), 6);
        Assert.Equal(ids, Members(f, twin).Select(e => e.Id).ToHashSet());
        Assert.Equal(upper, Spatial.StoreyOf(f.Model, stub.Id)!.Id);
        Assert.Equal(3, f.Bounds(stub.Id).Min.Z, 3);
        // And back: an edit upstairs comes down.
        f.Run("element.update", new JsonObject { ["id"] = stub.Id, ["attrs"] = new JsonObject { ["height"] = 2.6 } });
        Assert.Equal(2.6, f.Model.Get(b.Stub)!.Num("height"), 6);
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, b.Stub)!.Id);
    }

    [Fact]
    public void A_flat_kept_in_place_on_another_storey_keeps_its_height_through_an_edit()
    {
        var b = Build();
        var f = b.F;
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        var twin = TwinOf(f, f.Run("element.copyToStoreys", new JsonObject { ["ids"] = Ids(b.Group), ["storeyIds"] = new JsonArray(upper), ["keep"] = "world" })!);
        var id = Twin(f, twin, Key(f, b.Stub)).Id;
        Assert.Equal(0, f.Bounds(id).Min.Z, 3);
        f.Run("element.update", new JsonObject { ["id"] = b.Stub, ["attrs"] = new JsonObject { ["height"] = 2.5 } });
        Assert.Equal(0, f.Bounds(id).Min.Z, 3);
        Assert.Equal(2.5, f.Bounds(id).Max.Z, 3);
    }

    [Fact]
    public void Copied_again_to_the_same_storey_a_flat_is_skipped_not_doubled()
    {
        var b = Build();
        var f = b.F;
        var (upper, _) = Upper(b);
        f.Run("element.copyToStoreys", new JsonObject { ["ids"] = Ids(b.Group), ["storeyIds"] = new JsonArray(upper) });
        var count = Components.Instances(f.Model, b.Component).Count();
        var again = f.Run("element.copyToStoreys", new JsonObject { ["ids"] = Ids(b.Group), ["storeyIds"] = new JsonArray(upper) })!;
        Assert.Equal(count, Components.Instances(f.Model, b.Component).Count());
        Assert.NotEmpty(again["skipped"]!.AsArray());
    }

    [Fact]
    public void A_facade_with_the_rooms_door_and_the_flat_copied_together_have_one_door()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        var result = f.Run("element.copyToStoreys", new JsonObject { ["ids"] = Ids(b.Group, b.South, b.North, b.Party[0]), ["storeyIds"] = new JsonArray(upper) })!;
        var twin = TwinOf(f, result);
        var door = Assert.Single(RoomDoorsOn(f, upper));
        Assert.Equal(RoomOf(f, twin, Key(f, b.RoomWest)).Id, door.Str(RoomOpenings.FillTag));
    }

    [Fact]
    public void A_facade_copied_after_the_flat_gives_the_room_its_door_once()
    {
        var b = Build();
        var f = b.F;
        DoorOnSouth(b);
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        var twin = TwinOf(f, f.Run("element.copyToStoreys", new JsonObject { ["ids"] = Ids(b.Group), ["storeyIds"] = new JsonArray(upper) })!);
        var room = RoomOf(f, twin, Key(f, b.RoomWest));
        Assert.Empty(Carried(f, room));
        f.Run("element.copyToStoreys", new JsonObject { ["ids"] = Ids(b.South), ["storeyIds"] = new JsonArray(upper) });
        Assert.Single(Carried(f, room));
        Assert.Single(RoomDoorsOn(f, upper));
    }

    [Fact]
    public void A_flat_moved_to_another_storey_stops_in_that_storeys_walls()
    {
        var b = Build();
        var f = b.F;
        var (upper, south) = Upper(b);
        var twin = CopyBy(f, b.Group, 8);
        var ids = Members(f, twin).Select(e => e.Id).ToArray();
        var partition = Twin(f, twin, Key(f, b.Partition)).Id;
        f.Run("element.moveToStorey", new JsonObject { ["ids"] = Ids(ids), ["storeyId"] = upper });
        Assert.Contains(Stops(f, partition), r => r.Relating == south);
        Assert.DoesNotContain(Stops(f, partition), r => r.Relating == b.South);
    }

    // ─── IFC ─────────────────────────────────────────────────────────────

    /// <summary>The block with a flat in each slot: the first, a straight copy, a mirrored one; a door the room carries in the facade.</summary>
    static Block Flats()
    {
        var b = Build();
        DoorOnSouth(b);
        CopyBy(b.F, b.Group, 8);
        b.F.Run("element.transform", new JsonObject { ["ids"] = Ids(b.Group), ["op"] = "mirror", ["a"] = P(12, 0), ["b"] = P(12, 1), ["copy"] = true });
        return b;
    }

    static List<string> Lines(string text, string step) => [.. text.Split('\n').Where(l => Regex.IsMatch(l, $@"=\s*{step}\("))];
    static string Gid(string line) => Regex.Match(line, @"\('([^']+)'").Groups[1].Value;

    [Fact]
    public void The_flats_go_out_in_IFC_as_groups_of_ordinary_elements_saying_their_type()
    {
        var b = Flats();
        var f = b.F;
        var path = Path.Combine(_dir, "flats.ifc");
        IfcExport.Write(f.Session, path);
        var text = File.ReadAllText(path);

        // Every wall, door and room once, under its own GlobalId.
        Assert.Equal(f.Model.OfClass(IfcClass.Wall).Count(), Lines(text, "IFCWALL(?:STANDARDCASE)?").Count);
        Assert.Equal(f.Model.OfClass(IfcClass.Door).Count(), Lines(text, "IFCDOOR").Count);
        Assert.Equal(f.Model.OfClass(Rooms.Class).Count(), Lines(text, "IFCSPACE").Count);
        var gids = new[] { "IFCWALL(?:STANDARDCASE)?", "IFCDOOR", "IFCSPACE", "IFCOPENINGELEMENT", "IFCGROUP" }.SelectMany(s => Lines(text, s)).Select(Gid).ToList();
        Assert.Equal(gids.Count, gids.Distinct().Count());
        Assert.Equal(f.Model.OfClass(IfcClass.Door).Count(), Lines(text, "IFCRELFILLSELEMENT").Count);

        // Three instances, each its members; the type by name, the same id, and where it stands.
        Assert.Equal(3, Lines(text, "IFCGROUP").Count);
        Assert.Equal(3, Regex.Matches(text, @"IFCPROPERTYSET\([^\n]*'Lapin_Unit'").Count);
        Assert.Equal(3, Regex.Matches(text, @"IFCPROPERTYSINGLEVALUE\('Type',[^\n]*'Flat A'").Count);
        Assert.Equal(3, Regex.Matches(text, $@"IFCPROPERTYSINGLEVALUE\('TypeId',[^\n]*'{Regex.Escape(b.Component)}'").Count);
        Assert.Single(Regex.Matches(text, @"IFCPROPERTYSINGLEVALUE\('Mirrored',[^\n]*IFCBOOLEAN\(\.T\.\)"));
        Assert.DoesNotContain("Pset_Lapin_Unit", text);
        // A limit join goes out like any other.
        Assert.True(Lines(text, "IFCRELCONNECTSPATHELEMENTS").Count >= f.Model.Relations.Count(r => r.Class == IfcClass.RelConnectsPathElements));
    }

    [Fact]
    public void The_flats_come_back_from_IFC_as_they_were_and_stay_instances()
    {
        var b = Flats();
        var f = b.F;
        var path = Path.Combine(_dir, "flats.ifc");
        IfcExport.Write(f.Session, path);

        var g = new Fixture(new ArchitectureModule());
        g.Session.New(plain: true);
        IfcImport.Run(g.Session, path, null, out _);

        foreach (var cls in new[] { IfcClass.Wall, IfcClass.Door, IfcClass.OpeningElement, Rooms.Class })
            Assert.Equal(f.Model.OfClass(cls).Select(e => e.Id).OrderBy(x => x), g.Model.OfClass(cls).Select(e => e.Id).OrderBy(x => x));
        Assert.Equal(
            f.Model.Relations.Count(r => r.Class == IfcClass.RelConnectsPathElements),
            g.Model.Relations.Count(r => r.Class == IfcClass.RelConnectsPathElements));
        // The same bodies, mirrored flats' layers too.
        foreach (var w in f.Model.OfClass(IfcClass.Wall))
        {
            var (a, c) = (f.Bounds(w.Id), g.Bounds(w.Id));
            Assert.Equal(a.Min.X, c.Min.X, 3); Assert.Equal(a.Min.Y, c.Min.Y, 3); Assert.Equal(a.Max.X, c.Max.X, 3); Assert.Equal(a.Max.Y, c.Max.Y, 3);
        }
        // Instances again: an edit in one reaches the others, in the imported project too.
        var component = Assert.Single(g.Model.OfClass(Components.Class));
        Assert.Equal("Flat A", component.Name);
        Assert.Equal(Components.UnitKind, component.Str(Components.KindKey));
        var instances = Components.Instances(g.Model, component.Id).ToList();
        Assert.Equal(3, instances.Count);
        var stub = Members(g, instances[0].Id).First(e => e.Str(Components.MemberKey) == Key(f, b.Stub));
        g.Run("element.update", new JsonObject { ["id"] = stub.Id, ["attrs"] = new JsonObject { ["height"] = 2.5 } });
        Assert.All(instances, i => Assert.Equal(2.5, Twin(g, i.Id, Key(f, b.Stub)).Num("height"), 6));
        Assert.Equal(3, g.Model.OfClass(IfcClass.Door).Count(d => d.Str(RoomOpenings.FillTag) != null));
    }

    [Fact]
    public void Written_again_the_file_says_the_same_of_the_flats()
    {
        var b = Flats();
        var (one, two) = (Path.Combine(_dir, "one.ifc"), Path.Combine(_dir, "two.ifc"));
        IfcExport.Write(b.F.Session, one);
        var g = new Fixture(new ArchitectureModule());
        g.Session.New(plain: true);
        IfcImport.Run(g.Session, one, null, out _);
        IfcExport.Write(g.Session, two);
        var (a, c) = (File.ReadAllText(one), File.ReadAllText(two));
        foreach (var s in new[] { "IFCGROUP", "IFCWALL(?:STANDARDCASE)?", "IFCDOOR", "IFCSPACE" })
            Assert.Equal(Lines(a, s).Select(Gid).OrderBy(x => x), Lines(c, s).Select(Gid).OrderBy(x => x));
    }

    [Fact]
    public void A_file_imported_as_one_group_makes_no_instances()
    {
        var b = Flats();
        var path = Path.Combine(_dir, "flats.ifc");
        IfcExport.Write(b.F.Session, path);
        var g = new Fixture(new ArchitectureModule());
        g.Session.New(plain: true);
        IfcImport.Run(g.Session, path, new IfcImportOptions(Group: true), out _);
        // One file, one group: groups do not nest, and no instance is made.
        var group = Assert.Single(Groups.All(g.Model));
        Assert.Null(Components.Of(group));
        Assert.Empty(g.Model.OfClass(Components.Class));
    }
}
