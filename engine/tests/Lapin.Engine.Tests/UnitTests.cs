using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Apartment types (docs/plan-units.md): components that take walls, with their doors; what changes in one instance follows in the others, mirrored too, under the same ids.</summary>
public class UnitTests
{
    static JsonArray P(double x, double y) => new(x, y);
    static JsonArray Ids(params string[] ids) => new([.. ids.Select(x => (JsonNode)x)]);
    static double[] Pt(JsonNode? n) => [.. n!.AsArray().Select(v => Json.Number(v) ?? 0)];

    /// <summary>A 5 × 4 flat of four joined walls, a door in the south one and a room, made a type, and a second instance 20 m along.</summary>
    static (Fixture F, string Group, string Twin, string Door, string Component) Two(int copies = 1)
    {
        var f = new Fixture(new ArchitectureModule());
        var walls = new[] { f.Wall(0, 0, 5, 0), f.Wall(5, 0, 5, 4), f.Wall(5, 4, 0, 4), f.Wall(0, 4, 0, 0) };
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["along"] = 1 })!["id"]!;
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        var made = f.Run("component.make", new JsonObject { ["ids"] = Ids([.. walls, room]), ["kind"] = "unit", ["name"] = "Flat A" })!;
        var group = (string)made["id"]!;
        string twin = "";
        for (var i = 1; i <= copies; i++)
        {
            var c = f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "copy", ["by"] = P(20 * i, 0) })!;
            twin = c["ids"]!.AsArray().Select(x => (string)x!).First(x => f.Model.Get(x)!.Class == Groups.Class);
        }
        return (f, group, twin, door, (string)made["component"]!);
    }

    static List<Entity> WallsOf(Fixture f, string group) => Groups.MembersOf(f.Model, group).Select(f.Model.Get).OfType<Entity>().Where(e => e.Class == IfcClass.Wall).ToList();
    static List<Entity> DoorsOf(Fixture f, string group) => WallsOf(f, group).SelectMany(w => Openings.Of(f.Model, w.Id)).Select(o => Openings.FillOf(f.Model, o.Id)).OfType<Entity>().ToList();

    [Fact]
    public void A_plain_component_still_refuses_walls_and_a_type_takes_them()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 4, 0);
        Assert.Throws<CommandException>(() => f.Run("component.make", new JsonObject { ["ids"] = Ids(wall) }));
        var made = f.Run("component.make", new JsonObject { ["ids"] = Ids(wall), ["kind"] = "unit" })!;
        Assert.Equal("Apartment type 1", f.Model.Get((string)made["component"]!)!.Name);
    }

    [Fact]
    public void A_door_without_its_wall_is_refused()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 4, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1 })!["id"]!;
        var ex = Assert.Throws<CommandException>(() => f.Run("component.make", new JsonObject { ["ids"] = Ids(door), ["kind"] = "unit" }));
        Assert.Contains("wall that is not selected", ex.Message);
    }

    [Fact]
    public void A_copy_is_another_instance_with_walls_joins_door_and_room()
    {
        var (f, group, twin, _, component) = Two();
        Assert.Equal(component, Components.Of(f.Model.Get(twin)!));
        Assert.Equal(4, WallsOf(f, twin).Count);
        Assert.Single(DoorsOf(f, twin));
        Assert.Contains(Groups.MembersOf(f.Model, twin), id => f.Model.Get(id)!.Class == Rooms.Class);
        var joins = (string g) => WallsOf(f, g).SelectMany(w => f.Model.RelationsOf(w.Id, IfcClass.RelConnectsPathElements)).DistinctBy(r => r.Id).Count();
        Assert.True(joins(group) > 0);
        Assert.Equal(joins(group), joins(twin));
    }

    [Fact]
    public void A_wall_changed_in_one_follows_in_the_other_in_one_undo_under_the_same_ids()
    {
        var (f, group, twin, _, _) = Two();
        var wallIds = WallsOf(f, twin).Select(w => w.Id).ToHashSet();
        var doorId = DoorsOf(f, twin).Single().Id;
        var edited = WallsOf(f, group).First(w => Openings.Of(f.Model, w.Id).Any());
        f.Run("element.update", new JsonObject { ["id"] = edited.Id, ["attrs"] = new JsonObject { ["height"] = 3.3 } });
        Assert.Equal(wallIds, WallsOf(f, twin).Select(w => w.Id).ToHashSet());
        Assert.Equal(doorId, DoorsOf(f, twin).Single().Id);
        Assert.Equal(1, WallsOf(f, twin).Count(w => Math.Abs(w.Num("height") - 3.3) < 1e-9));
        // Joined as before: the copy joins the walls that stayed.
        var joins = (string g) => WallsOf(f, g).SelectMany(w => f.Model.RelationsOf(w.Id, IfcClass.RelConnectsPathElements)).DistinctBy(r => r.Id).Count();
        Assert.Equal(joins(group), joins(twin));
        f.Session.Undo();
        Assert.Equal(0, WallsOf(f, twin).Count(w => Math.Abs(w.Num("height") - 3.3) < 1e-9));
    }

    [Fact]
    public void Only_what_changed_is_made_again_in_the_others()
    {
        var (f, group, twin, _, _) = Two();
        var edited = WallsOf(f, group).First(w => !Openings.Of(f.Model, w.Id).Any());
        var key = edited.Str(Components.MemberKey);
        f.Run("element.update", new JsonObject { ["id"] = edited.Id, ["attrs"] = new JsonObject { ["height"] = 3.3 } });
        Assert.Equal(1, WallsOf(f, twin).Count(w => Math.Abs(w.Num("height") - 3.3) < 1e-9));
        Assert.Equal(key, WallsOf(f, twin).First(w => Math.Abs(w.Num("height") - 3.3) < 1e-9).Str(Components.MemberKey));
    }

    [Fact]
    public void A_door_added_or_taken_away_in_one_follows()
    {
        var (f, group, twin, _, _) = Two();
        var plain = WallsOf(f, group).First(w => !Openings.Of(f.Model, w.Id).Any());
        var added = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = plain.Id, ["along"] = 1 })!["id"]!;
        Assert.Equal(2, DoorsOf(f, twin).Count);
        var stable = DoorsOf(f, twin).Select(d => d.Id).ToHashSet();
        f.Run("element.update", new JsonObject { ["id"] = plain.Id, ["attrs"] = new JsonObject { ["height"] = 3.1 } });
        Assert.Equal(stable, DoorsOf(f, twin).Select(d => d.Id).ToHashSet());
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(added) });
        Assert.Single(DoorsOf(f, twin));
    }

    [Fact]
    public void A_mirrored_instance_has_its_door_turned_and_follows_mirrored()
    {
        var (f, group, _, _, _) = Two();
        var made = f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "mirror", ["a"] = P(-1, 0), ["b"] = P(-1, 1), ["copy"] = true })!;
        var twin = made["ids"]!.AsArray().Select(x => (string)x!).First(x => f.Model.Get(x)!.Class == Groups.Class);
        var xs = () => WallsOf(f, twin).SelectMany(w => new[] { Pt(w.Attrs["start"])[0], Pt(w.Attrs["end"])[0] }).ToList();
        Assert.Equal(-7, xs().Min(), 6);
        Assert.Equal(-2, xs().Max(), 6);
        Assert.NotEqual(DoorsOf(f, group).Single().Bool("flipSide"), DoorsOf(f, twin).Single().Bool("flipSide"));
        // The east wall moved 1 m further east: in the mirrored one, further west.
        var east = WallsOf(f, group).First(w => Math.Abs(Pt(w.Attrs["start"])[0] - 5) < 1e-6 && Math.Abs(Pt(w.Attrs["end"])[0] - 5) < 1e-6);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(east.Id), ["op"] = "move", ["by"] = P(1, 0) });
        Assert.Equal(-8, xs().Min(), 6);
        Assert.Equal(-2, xs().Max(), 6);
    }

    [Fact]
    public void A_room_carrying_its_door_keeps_it_its_own_in_the_other_instance()
    {
        var (f, group, twin, _, _) = Two();
        var room = Groups.MembersOf(f.Model, group).First(id => f.Model.Get(id)!.Class == Rooms.Class);
        var side = f.Session.Query("architecture.roomOpenings", new JsonObject { ["id"] = room })!["sides"]!.AsArray()
            .Select(s => s!.AsObject()).First(s => Math.Abs((double)s["a"]![1]! - 4) < 0.3 && Math.Abs((double)s["b"]![1]! - 4) < 0.3)["index"]!.GetValue<int>();
        f.Run("architecture.setRoomOpenings", new JsonObject { ["id"] = room, ["on"] = true, ["items"] = new JsonArray(new JsonObject { ["key"] = "w", ["kind"] = "window", ["side"] = side }) });
        var otherRoom = Groups.MembersOf(f.Model, twin).First(id => f.Model.Get(id)!.Class == Rooms.Class);
        var windows = f.Model.OfClass(IfcClass.Window).ToList();
        Assert.Equal(2, windows.Count);
        Assert.Contains(windows, w => w.Str(RoomOpenings.FillTag) == room);
        Assert.Contains(windows, w => w.Str(RoomOpenings.FillTag) == otherRoom);
        Assert.NotNull((string?)f.Session.Query("architecture.roomOpenings", new JsonObject { ["id"] = otherRoom })!["items"]!.AsArray().Single()!["fill"]);
    }

    [Fact]
    public void Many_flats_follow_an_edit_in_reasonable_time()
    {
        var (f, group, _, _, component) = Two(100);
        var edited = WallsOf(f, group).First(w => !Openings.Of(f.Model, w.Id).Any());
        var clock = System.Diagnostics.Stopwatch.StartNew();
        f.Run("element.update", new JsonObject { ["id"] = edited.Id, ["attrs"] = new JsonObject { ["height"] = 3.3 } });
        clock.Stop();
        Console.WriteLine($"One wall edited, carried to 100 flats: {clock.ElapsedMilliseconds} ms");
        Assert.All(Components.Instances(f.Model, component), g => Assert.Equal(1, WallsOf(f, g.Id).Count(w => Math.Abs(w.Num("height") - 3.3) < 1e-9)));
        Assert.True(clock.ElapsedMilliseconds < 30_000, $"{clock.ElapsedMilliseconds} ms");
    }
}
