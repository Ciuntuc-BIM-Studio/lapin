using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Copy to storeys (ArchiCAD's copy to stories): the selection copied onto several storeys at once, one undo step.</summary>
public class StoreyCopyTests
{
    /// <summary>A ground floor at ±0.00 and two storeys of 3 m over it.</summary>
    static (Fixture F, string First, string Second) Building(params ILapinModule[] modules)
    {
        var f = new Fixture(modules.Length == 0 ? [new ArchitectureModule(), new DraftingModule()] : modules);
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var second = (string)f.Run("storey.create", new JsonObject { ["name"] = "Second floor", ["elevation"] = 6.0, ["height"] = 3.0 })!["id"]!;
        return (f, first, second);
    }

    static JsonObject Copy(Fixture f, IEnumerable<string> ids, IEnumerable<string> storeys, JsonObject? options = null)
    {
        var a = new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()), ["storeyIds"] = new JsonArray(storeys.Select(x => (JsonNode)x).ToArray()) };
        foreach (var (k, v) in options ?? []) a[k] = v?.DeepClone();
        return (JsonObject)f.Run("element.copyToStoreys", a)!;
    }

    static List<Entity> On(Fixture f, string storey, string cls) =>
        f.Model.Entities.Where(e => e.Class == cls && Spatial.StoreyOf(f.Model, e.Id)?.Id == storey).ToList();

    [Fact]
    public void Two_walls_with_a_window_copied_to_two_storeys_keep_their_type_their_window_and_their_join()
    {
        var (f, first, second) = Building();
        var ground = f.Storey;
        var type = f.Type("Brick 25", ("Plaster", 0.02, false), ("Brick", 0.25, true));
        var a = f.Wall(0, 0, 6, 0, type);
        var b = f.Wall(6, 0, 6, 4, type);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = a, ["along"] = 3 })!["id"]!;
        f.Run("drafting.addTag", new JsonObject { ["viewId"] = "plan:" + ground, ["element"] = window, ["at"] = new JsonArray(3, -1) });
        Assert.NotNull(Joins.Between(f.Model, a, b));
        var before = f.Model.Entities.Count();

        var r = Copy(f, [a, b, window], [first, second]);
        Assert.Equal(4, r["ids"]!.AsArray().Count(x => f.Model.Get((string)x!)?.Class == IfcClass.Wall));
        Assert.Empty(r["skipped"]!.AsArray());
        foreach (var (storey, z) in new[] { (first, 3.0), (second, 6.0) })
        {
            var walls = On(f, storey, IfcClass.Wall);
            Assert.Equal(2, walls.Count);
            // The same type, new GlobalIds, joined as the originals are, among themselves only.
            Assert.All(walls, w => Assert.Equal(type, f.Model.RelatingOf(w.Id, IfcClass.RelDefinesByType)!.Id));
            Assert.All(walls, w => Assert.True(GlobalId.IsValid(w.Id) && w.Id != a && w.Id != b));
            Assert.NotNull(Joins.Between(f.Model, walls[0].Id, walls[1].Id));
            Assert.All(walls, w => Assert.All(Joins.Of(f.Model, w.Id), j => Assert.Contains(Joins.Other(j, w.Id), walls.Select(x => x.Id))));
            // The window with its wall, on the storey; the wall stands on the storey's level.
            var win = Assert.Single(On(f, storey, IfcClass.Window));
            var opening = f.Model.RelatingOf(win.Id, IfcClass.RelFillsElement)!;
            Assert.Contains(f.Model.RelatingOf(opening.Id, IfcClass.RelVoidsElement)!.Id, walls.Select(w => w.Id));
            Assert.All(walls, w => Assert.Equal(z, f.Bounds(w.Id).Min.Z, 6));
            // The window's tag goes to the storey's plan, tagging the copy.
            var tag = Assert.Single(f.Model.OfClass(IfcClass.Annotation), t => t.PredefinedType == "TAG" && t.Str("viewId") == "plan:" + storey);
            Assert.Equal(win.Id, tag.Str("element"));
        }
        // The originals as they were.
        Assert.NotNull(Joins.Between(f.Model, a, b));
        Assert.Equal(ground, Spatial.StoreyOf(f.Model, a)!.Id);
        // One undo step takes every copy away.
        f.Session.Undo();
        Assert.Equal(before, f.Model.Entities.Count());
        Assert.Empty(On(f, first, IfcClass.Wall));
        Assert.Empty(On(f, second, IfcClass.Wall));
    }

    [Fact]
    public void With_the_storey_a_copy_keeps_its_height_over_its_level_kept_in_place_it_keeps_its_place_in_space()
    {
        var (f, first, _) = Building();
        var w = f.Wall(0, 0, 5, 0);
        f.Run("element.update", new JsonObject { ["id"] = w, ["attrs"] = new JsonObject { ["baseOffset"] = 0.5 } });
        Assert.Equal(0.5, f.Model.Get(w)!.Num("baseOffset"), 9);
        var rel = (string)Copy(f, [w], [first])["ids"]![0]!;
        Assert.Equal(0.5, f.Model.Get(rel)!.Num("baseOffset"), 9);
        Assert.Equal(3.5, f.Bounds(rel).Min.Z, 6);
        // Kept in place: on the first floor, its base 2.50 under the level, where the original stands.
        var abs = (string)Copy(f, [w], [first], new JsonObject { ["keep"] = "world" })["ids"]![0]!;
        Assert.Equal(first, Spatial.StoreyOf(f.Model, abs)!.Id);
        Assert.Equal(-2.5, f.Model.Get(abs)!.Num("baseOffset"), 9);
        Assert.Equal(0.5, f.Bounds(abs).Min.Z, 6);
        Assert.Throws<CommandException>(() => Copy(f, [w], [first], new JsonObject { ["keep"] = "sky" }));
        Assert.Throws<CommandException>(() => Copy(f, [w], []));
    }

    [Fact]
    public void A_storey_that_already_holds_the_same_element_in_the_same_place_is_skipped_and_reported()
    {
        var (f, first, second) = Building();
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(5, 0, 5, 4);
        Copy(f, [a], [first]);
        var r = Copy(f, [a, b], [first, second]);
        // On the first floor only the second wall is new; on the second floor both are.
        var skipped = Assert.Single(r["skipped"]!.AsArray());
        Assert.Equal(first, (string?)skipped!["storeyId"]);
        Assert.Equal(a, (string?)skipped["id"]);
        Assert.Equal(2, On(f, first, IfcClass.Wall).Count);
        Assert.Equal(2, On(f, second, IfcClass.Wall).Count);
        // Without the check a second copy stands on the first.
        Copy(f, [a], [first], new JsonObject { ["skipDuplicates"] = false });
        Assert.Equal(3, On(f, first, IfcClass.Wall).Count);
        // The storey an element is on is not copied onto.
        Assert.Empty(Copy(f, [a], [f.Storey])["ids"]!.AsArray());
    }

    [Fact]
    public void A_group_and_an_assembly_are_copied_whole()
    {
        var (f, first, _) = Building(new ArchitectureModule(), new Lapin.Studio.StudioModule());
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(5, 0, 5, 4);
        var c = f.Wall(10, 0, 14, 0);
        var d = f.Wall(10, 2, 14, 2);
        var group = (string)f.Run("group.create", new JsonObject { ["ids"] = new JsonArray(a, b), ["name"] = "Core" })!["id"]!;
        string assembly = "";
        f.Session.Edit("Assembly", edit =>
        {
            assembly = edit.NewId();
            edit.Add(new Entity(assembly, "IfcElementAssembly", "Pair", []));
            Spatial.Contain(edit, f.Storey, assembly);
            edit.Relate(IfcClass.RelAggregates, assembly, c);
            edit.Relate(IfcClass.RelAggregates, assembly, d);
        });
        var r = Copy(f, [group, assembly], [first]);
        var twin = Assert.Single(Groups.All(f.Model), g => g.Id != group);
        Assert.Equal("Core", twin.Name);
        Assert.Contains(twin.Id, r["ids"]!.AsArray().Select(x => (string?)x));
        var members = Groups.MembersOf(f.Model, twin.Id).ToList();
        Assert.Equal(2, members.Count);
        Assert.All(members, m => Assert.Equal(first, Spatial.StoreyOf(f.Model, m)!.Id));
        Assert.NotNull(Joins.Between(f.Model, members[0], members[1]));
        var copyAsm = Assert.Single(On(f, first, "IfcElementAssembly"));
        var parts = f.Model.RelatedOf(copyAsm.Id, IfcClass.RelAggregates).ToList();
        Assert.Equal(2, parts.Count);
        Assert.All(parts, p => Assert.Equal(first, Spatial.StoreyOf(f.Model, p.Id)!.Id));
        Assert.DoesNotContain(parts, p => p.Id == c || p.Id == d);
        Assert.Equal(2, f.Model.RelatedOf(assembly, IfcClass.RelAggregates).Count());
        // Relations off: the copies stand on their own, no group, no joins.
        var (g2, second, _) = Building();
        var x = g2.Wall(0, 0, 5, 0);
        var y = g2.Wall(5, 0, 5, 4);
        var grp = (string)g2.Run("group.create", new JsonObject { ["ids"] = new JsonArray(x, y) })!["id"]!;
        Copy(g2, [grp], [second], new JsonObject { ["relations"] = false });
        var copies = On(g2, second, IfcClass.Wall);
        Assert.Equal(2, copies.Count);
        Assert.Null(Joins.Between(g2.Model, copies[0].Id, copies[1].Id));
        Assert.Single(Groups.All(g2.Model));
    }

    [Fact]
    public void Without_hosted_things_walls_go_alone_and_a_door_asks_for_its_wall()
    {
        var (f, first, _) = Building();
        var a = f.Wall(0, 0, 6, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["along"] = 2 })!["id"]!;
        Assert.Throws<CommandException>(() => Copy(f, [door], [first]));
        Copy(f, [a], [first], new JsonObject { ["hosted"] = false });
        var wall = Assert.Single(On(f, first, IfcClass.Wall));
        Assert.Empty(f.Model.RelatedOf(wall.Id, IfcClass.RelVoidsElement));
        Assert.Empty(On(f, first, IfcClass.Door));
        Assert.Single(f.Model.RelatedOf(a, IfcClass.RelVoidsElement));
    }

    [Fact]
    public void A_stair_copied_with_its_railing_climbs_to_the_storey_above_its_new_one_through_an_opening_of_its_own()
    {
        var (f, first, second) = Building();
        static JsonArray Square() => new(new JsonArray(-2, -2), new JsonArray(8, -2), new JsonArray(8, 6), new JsonArray(-2, 6));
        var slab1 = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = first, ["points"] = Square() })!["id"]!;
        var slab2 = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = second, ["points"] = Square() })!["id"]!;
        var spec = StairSpec.Default() with { Shape = "straight", Turn = "landing", Name = "Straight" };
        var type = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var stair = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0, ["topStoreyId"] = first })!["id"]!;
        var railing = (string)f.Run("architecture.addRailing", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 1.5), new JsonArray(4, 1.5)) })!["id"]!;
        Assert.Equal(slab1, f.Model.RelatingOf(StairBuilder.OpeningOf(f.Model, stair)!.Id, IfcClass.RelVoidsElement)!.Id);
        var g = f.Session.Geometry(stair)!;
        Assert.Contains(g.Parts, p => p.Key?.StartsWith("railing:") == true);

        Copy(f, [stair, railing], [first]);
        var copy = Assert.Single(On(f, first, IfcClass.Stair));
        Assert.Equal(type, f.Model.RelatingOf(copy.Id, IfcClass.RelDefinesByType)!.Id);
        // Fixed to the storey above its own: on the first floor, to the second.
        Assert.Equal(second, copy.Str("topStoreyId"));
        Assert.Equal(slab2, f.Model.RelatingOf(StairBuilder.OpeningOf(f.Model, copy.Id)!.Id, IfcClass.RelVoidsElement)!.Id);
        Assert.Equal(slab1, f.Model.RelatingOf(StairBuilder.OpeningOf(f.Model, stair)!.Id, IfcClass.RelVoidsElement)!.Id);
        var cg = f.Session.Geometry(copy.Id)!;
        Assert.Contains(cg.Parts, p => p.Key?.StartsWith("railing:") == true);
        Assert.Equal(3.0, f.Bounds(copy.Id).Min.Z, 1);
        Assert.Single(On(f, first, Railings.Class));
        // A floor copied without its stair leaves the stair's opening behind.
        Copy(f, [slab1], [second], new JsonObject { ["skipDuplicates"] = false });
        var floors = On(f, second, IfcClass.Slab);
        Assert.Equal(2, floors.Count);
        var copiedFloor = floors.Single(s => s.Id != slab2);
        Assert.DoesNotContain(f.Model.RelatedOf(copiedFloor.Id, IfcClass.RelVoidsElement), o => o.Str("stairId") != null);
    }

    [Fact]
    public void A_room_goes_with_its_finishes_and_the_ifc_export_writes_each_copy_with_its_own_GlobalId()
    {
        var (f, first, _) = Building();
        foreach (var (x1, y1, x2, y2) in new[] { (0.0, 0.0, 5.0, 0.0), (5.0, 0.0, 5.0, 4.0), (5.0, 4.0, 0.0, 4.0), (0.0, 4.0, 0.0, 0.0) }) f.Wall(x1, y1, x2, y2);
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        var finishType = (string)f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = "Living room" })!["id"]!;
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = finishType });
        var laid = RoomFinishes.Laid(f.Model, room).Count();
        Assert.True(laid > 0);
        Copy(f, [room], [first]);
        var copy = Assert.Single(On(f, first, Rooms.Class));
        Assert.Equal(laid, RoomFinishes.Laid(f.Model, copy.Id).Count());
        Assert.All(RoomFinishes.Laid(f.Model, copy.Id), e => Assert.Equal(first, Spatial.StoreyOf(f.Model, e.Id)!.Id));
        Assert.Equal(laid, RoomFinishes.Laid(f.Model, room).Count());
        using var ms = new MemoryStream();
        Lapin.Ifc.IfcExport.Write(f.Session, ms, "copy.ifc");
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains($"'{room}'", text);
        Assert.Contains($"'{copy.Id}'", text);
    }

    [Fact]
    public void A_wall_attached_to_a_floor_copied_with_it_is_attached_to_the_floors_copy_unless_relations_are_left()
    {
        var (f, first, second) = Building();
        var w = f.Wall(0, 0, 5, 0);
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(-1, -1), new JsonArray(6, -1), new JsonArray(6, 1), new JsonArray(-1, 1)), ["offset"] = 2.5 })!["id"]!;
        f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(w), ["roofIds"] = new JsonArray(slab) });
        Copy(f, [w, slab], [first]);
        var wall = Assert.Single(On(f, first, IfcClass.Wall));
        var floor = Assert.Single(On(f, first, IfcClass.Slab));
        Assert.Equal(floor.Id, (string?)wall.Attrs["roofIds"]![0]);
        Copy(f, [w, slab], [second], new JsonObject { ["relations"] = false });
        Assert.Equal(slab, (string?)Assert.Single(On(f, second, IfcClass.Wall)).Attrs["roofIds"]![0]);
    }

    [Fact]
    public void A_guests_copies_are_their_own_undo_in_a_shared_session()
    {
        var (f, first, second) = Building();
        var w = f.Wall(0, 0, 5, 0);
        f.Session.Author = "p1";
        Copy(f, [w], [first, second]);
        f.Session.Author = History.Me;
        var mine = f.Wall(0, 5, 5, 5);
        Assert.Single(On(f, first, IfcClass.Wall));
        // The guest's undo takes both copies; the host's wall drawn after stays.
        f.Session.Undo("p1");
        Assert.Empty(On(f, first, IfcClass.Wall));
        Assert.Empty(On(f, second, IfcClass.Wall));
        Assert.NotNull(f.Model.Get(mine));
    }
}
