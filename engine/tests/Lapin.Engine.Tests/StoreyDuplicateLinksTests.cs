using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Architecture.Stairs;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// storey.duplicate copies through element.transform's copy and Copy to storeys' remap (audit 15.11.2026, follow-up): a copy on
/// the new storey names the copies of what its original named (a wall its roof, a finish its room), a roof's downpipes are
/// copied once, with the roof, and a stair's copy has its own opening; one undo step.
/// </summary>
public class StoreyDuplicateLinksTests
{
    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);
    static JsonArray Ids(params string[] ids) => new([.. ids.Select(x => (JsonNode)x)]);
    static string Id(JsonNode? r) => (string)r!["id"]!;

    static string Duplicate(Fixture f, string storey) => Id(f.Run("storey.duplicate", new JsonObject { ["id"] = storey }));
    static List<Entity> On(Fixture f, string storey) => f.Model.RelatedOf(storey, IfcClass.RelContainedInSpatialStructure).ToList();

    [Fact]
    public void A_duplicated_storeys_walls_point_to_its_own_roof_copy_and_its_downpipes_are_not_doubled()
    {
        var f = new Fixture(new ArchitectureModule());
        var roof = Id(f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (10, 0), (10, 6), (0, 6)), ["style"] = "traditional_campie" }));
        var pipes = f.Run("architecture.makeDownpipes", new JsonObject { ["id"] = roof })!["ids"]!.AsArray().Count;
        Assert.True(pipes > 0);
        var wall = f.Wall(5, 0.5, 5, 5.5);
        f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = Ids(wall), ["roofIds"] = Ids(roof) });
        var before = f.Model.Entities.Count();

        var copy = Duplicate(f, f.Storey);
        var on = On(f, copy);
        var roofCopy = Assert.Single(on, e => e.Class == IfcClass.Roof);
        var wallCopy = Assert.Single(on, e => e.Class == IfcClass.Wall);
        Assert.Equal([roofCopy.Id], wallCopy.Attrs["roofIds"]!.AsArray().Select(x => (string)x!));
        Assert.Equal([roof], f.Model.Get(wall)!.Attrs["roofIds"]!.AsArray().Select(x => (string)x!));
        // Each roof its own downpipes, once, on its storey.
        var all = f.Model.OfClass(Drainage.DownpipeClass).ToList();
        Assert.Equal(2 * pipes, all.Count);
        Assert.Equal(pipes, all.Count(p => p.Str("roofId") == roof));
        Assert.Equal(pipes, all.Count(p => p.Str("roofId") == roofCopy.Id));
        Assert.All(all.Where(p => p.Str("roofId") == roofCopy.Id), p => Assert.Equal(copy, Spatial.StoreyOf(f.Model, p.Id)?.Id));
        // The wall's copy reaches its roof copy, 3 m up.
        Assert.Equal(f.Bounds(wall).Max.Z + 3, f.Bounds(wallCopy.Id).Max.Z, 6);
        // One undo step.
        f.Session.Undo();
        Assert.Equal(before, f.Model.Entities.Count());
    }

    [Fact]
    public void A_duplicated_storeys_rooms_keep_their_finishes()
    {
        var f = new Fixture(new ArchitectureModule());
        var room = Id(f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (4, 0), (4, 3), (0, 3)) }));
        var type = Id(f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = "Ground floor on slab" }));
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = type });
        var laid = RoomFinishes.Laid(f.Model, room).Select(e => e.Id).Order().ToList();
        Assert.Equal(5, laid.Count);
        var pieces = f.Model.Entities.Count();

        var copy = Duplicate(f, f.Storey);
        var twin = Assert.Single(On(f, copy), e => e.Class == Rooms.Class);
        Assert.Equal(type, twin.Str("finishType"));
        var own = RoomFinishes.Laid(f.Model, twin.Id).ToList();
        Assert.Equal(5, own.Count);
        Assert.All(own, p => Assert.Equal(copy, Spatial.StoreyOf(f.Model, p.Id)?.Id));
        Assert.Equal(laid, RoomFinishes.Laid(f.Model, room).Select(e => e.Id).Order());
        // Its finish slab on the new storey, over its room.
        var slab = f.Model.OfClass(IfcClass.Slab).Single(s => s.Str("finishOf") == twin.Id);
        Assert.Equal(f.Bounds(f.Model.OfClass(IfcClass.Slab).Single(s => s.Str("finishOf") == room).Id).Min.Z + 3, f.Bounds(slab.Id).Min.Z, 6);
        Assert.Equal(1, f.Model.OfClass(Rooms.Class).Count(r => r.Id != room));
    }

    [Fact]
    public void A_duplicated_storeys_stairs_have_their_own_opening_in_the_floor_above()
    {
        var f = new Fixture(new ArchitectureModule());
        var level1 = Id(f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 }));
        var level2 = Id(f.Run("storey.create", new JsonObject { ["name"] = "Level 2", ["elevation"] = 6.0, ["height"] = 3.0 }));
        var floor = Id(f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = level1, ["points"] = Pts((-12, -6), (12, -6), (12, 6), (-12, 6)) }));
        var top = Id(f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = level2, ["points"] = Pts((-12, -6), (12, -6), (12, 6), (-12, 6)) }));
        var spec = StairSpec.Default() with { Shape = "straight", Name = "straight" };
        var type = Id(f.Run("architecture.createStairType", new JsonObject { ["spec"] = spec.ToJson() }));
        var stair = Id(f.Run("architecture.addStair", new JsonObject { ["storeyId"] = level1, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0 }));
        List<Entity> OpeningsOf(string s) => f.Model.OfClass(IfcClass.OpeningElement).Where(o => o.Str("stairId") == s).ToList();
        string HostOf(Entity o) => f.Model.RelatingOf(o.Id, IfcClass.RelVoidsElement)!.Id;
        Assert.Equal(top, HostOf(Assert.Single(OpeningsOf(stair))));

        var copy = Duplicate(f, level1);
        var on = On(f, copy);
        var stairCopy = Assert.Single(on, e => e.Class == IfcClass.Stair);
        var floorCopy = Assert.Single(on, e => e.Class == IfcClass.Slab);
        // The copy climbs to Level 2 (moved up over the new storey), through an opening of its own in Level 2's floor.
        Assert.Equal(top, HostOf(Assert.Single(OpeningsOf(stairCopy.Id))));
        // The original now climbs to the new storey: its one opening is in the copied floor, which holds no other.
        Assert.Equal(floorCopy.Id, HostOf(Assert.Single(OpeningsOf(stair))));
        Assert.Single(f.Model.RelatedOf(floorCopy.Id, IfcClass.RelVoidsElement), o => o.Str("stairId") != null);
        Assert.Single(f.Model.RelatedOf(top, IfcClass.RelVoidsElement), o => o.Str("stairId") != null);
        Assert.DoesNotContain(f.Model.RelatedOf(floor, IfcClass.RelVoidsElement), o => o.Str("stairId") != null);
    }
}
