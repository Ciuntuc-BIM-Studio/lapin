using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Issue #145: a false ceiling placed in a room covers that room as it is now (a room separation line bounds it); with no height
/// given it hangs just under the slab above (its type's plenum below it), else at 2.70 m; its type may place it over the finished
/// floor instead, which it is then measured from; and a room may take its false ceiling as its ceiling (an option, off by default).
/// </summary>
public class FalseCeilingRoomTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());

    /// <summary>Four walls 0.2 m thick whose inner faces close a 6 × 4 m room from (0, 0) to (6, 4).</summary>
    static Fixture Box()
    {
        var f = F();
        var t = f.Type("Block 200", ("Block", 0.2, true));
        f.Wall(-0.1, -0.1, 6.1, -0.1, t);
        f.Wall(6.1, -0.1, 6.1, 4.1, t);
        f.Wall(6.1, 4.1, -0.1, 4.1, t);
        f.Wall(-0.1, 4.1, -0.1, -0.1, t);
        return f;
    }

    static string Room(Fixture f, double x, double y) =>
        (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y })!["id"]!;

    /// <summary>A storey above at 3 m and a floor on it over the whole box; the floor's underside.</summary>
    static double SlabAbove(Fixture f)
    {
        var upper = (string)f.Run("storey.create", new JsonObject { ["elevation"] = 3.0 })!["id"]!;
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = upper, ["points"] = new JsonArray(new JsonArray(-1, -1), new JsonArray(7, -1), new JsonArray(7, 5), new JsonArray(-1, 5)),
        })!["id"]!;
        var s = f.Model.Get(slab)!;
        return Slabs.Top(f.Model, s) + Slabs.Section(f.Model, s).Min(b => b.From);
    }

    static string Plain(Fixture f, CeilingSpec? spec = null) =>
        (string)f.Run("architecture.createCeilingType", new JsonObject { ["spec"] = (spec ?? CeilingSpec.Plain()).ToJson() })!["id"]!;

    static string Ceiling(Fixture f, double x, double y, string type, double? height = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y, ["typeId"] = type };
        if (height != null) a["height"] = height;
        return (string)f.Run("architecture.addCeiling", a)!["id"]!;
    }

    static Dictionary<string, double> Space(Fixture f, string room) =>
        f.Session.Quantities(room).Single(s => s.Name == "Qto_SpaceBaseQuantities").Quantities.ToDictionary(q => q.Name, q => q.Value);

    [Fact]
    public void A_ceiling_in_a_room_divided_by_a_separation_line_covers_only_that_room()
    {
        var f = Box();
        var west = Room(f, 1.5, 2);
        f.Run("architecture.addRoomDivider", new JsonObject
        {
            ["storeyId"] = f.Storey, ["kind"] = "line", ["points"] = new JsonArray(new JsonArray(3, 0), new JsonArray(3, 4)),
        });
        Room(f, 4.5, 2);
        var id = Ceiling(f, 1.5, 2, Plain(f));
        var contour = Slabs.Contour(f.Model.Get(id)!);
        Assert.Equal(12, Math.Abs(Polygon2.SignedArea(contour)), 6);
        Assert.True(contour.Max(p => p.X) <= 3 + 1e-6);
        Assert.Equal(west, f.Model.Get(id)!.Str("roomId"));
        // The other side's ceiling is the other half.
        var east = Ceiling(f, 4.5, 2, Plain(f));
        Assert.True(Slabs.Contour(f.Model.Get(east)!).Min(p => p.X) >= 3 - 1e-6);
    }

    [Fact]
    public void With_no_height_given_a_ceiling_hangs_just_under_the_slab_above_its_plenum_below_it_else_at_2_70()
    {
        var f = Box();
        Room(f, 3, 2);
        var plain = Plain(f);
        // No slab above: 2.70 m over the storey, as before.
        Assert.Equal(2.7, f.Model.Get(Ceiling(f, 3, 2, plain))!.Num("height"), 9);

        var soffit = SlabAbove(f);
        var board = Measure.Bounds(f.Session.Geometry(Ceiling(f, 3, 2, plain))!.Parts.Single().Solid)!.Value;
        Assert.Equal(soffit, board.Max.Z, 6);
        // A grid ceiling's tees reach the slab; a type's plenum keeps it that far below.
        var grid = (string)f.Run("architecture.createCeilingType", new JsonObject { ["spec"] = (CeilingSpec.Default() with { Plenum = 0.3 }).ToJson() })!["id"]!;
        var g = Ceiling(f, 3, 2, grid);
        var spec = CeilingSpec.Default();
        Assert.Equal(soffit - 0.3 - Math.Max(spec.GridDepth, 0.003 + spec.Thickness), Ceilings.Underside(f.Model, f.Model.Get(g)!), 6);
        // A height given is the ceiling's.
        Assert.Equal(2.4, f.Model.Get(Ceiling(f, 3, 2, plain, 2.4))!.Num("height"), 9);
    }

    [Fact]
    public void A_type_placing_its_ceilings_over_the_finished_floor_measures_them_from_the_rooms_floor_as_the_wall_finish_is()
    {
        var f = Box();
        var room = Room(f, 3, 2);
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(room), ["attrs"] = new JsonObject { ["baseOffset"] = 0.2 } });
        SlabAbove(f);
        var type = Plain(f, CeilingSpec.Plain() with { Placement = "floor", Height = 2.5 });
        var id = Ceiling(f, 3, 2, type);
        var c = f.Model.Get(id)!;
        Assert.Equal("floor", c.Str(Ceilings.HeightFromKey));
        Assert.Equal(2.5, c.Num("height"), 9);
        Assert.Equal(0.2 + 2.5, Ceilings.Underside(f.Model, c), 9);
        // Turned back to the storey's level: the same height is now over the storey, 0.2 lower.
        f.Run("architecture.updateCeiling", new JsonObject { ["id"] = id, [Ceilings.HeightFromKey] = null });
        Assert.Equal(2.5, Ceilings.Underside(f.Model, f.Model.Get(id)!), 9);
        // A drawn ceiling has no room to be measured from.
        var drawn = (string)f.Run("architecture.addCeiling", new JsonObject
        {
            ["storeyId"] = f.Storey, ["typeId"] = type, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(1, 0), new JsonArray(1, 1), new JsonArray(0, 1)),
        })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("architecture.updateCeiling", new JsonObject { ["id"] = drawn, [Ceilings.HeightFromKey] = "floor" }));
    }

    [Fact]
    public void A_room_takes_its_false_ceiling_as_its_ceiling_only_when_it_says_so()
    {
        var f = Box();
        var room = Room(f, 3, 2);
        var soffit = SlabAbove(f);
        var bath = (string)f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = "Bathroom" })!["id"]!;
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = bath });
        Ceiling(f, 3, 2, Plain(f), 2.4);
        var walls = f.Model.OfClass(RoomFinishes.Class).Single(e => e.Str("roomId") == room && e.Str("part") == "walls").Id;
        double WallsTop() => f.Session.Geometry(walls)!.Parts.Max(p => Measure.Bounds(p.Solid)!.Value.Max.Z);

        // By default (as before): the slab above is the room's ceiling, the walls' finish up to the finish's own ceiling under it.
        Assert.Equal(soffit - 0.011, WallsTop(), 6);
        Assert.Equal(soffit - 0.011, Space(f, room)["FinishCeilingHeight"], 6);

        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(room), ["attrs"] = new JsonObject { [RoomFinishes.CeilingFromKey] = RoomFinishes.FalseCeilingValue } });
        Assert.Equal(2.4, WallsTop(), 6);
        var q = Space(f, room);
        Assert.Equal(2.4, q["FinishCeilingHeight"], 6);
        Assert.Equal(q["NetFloorArea"] * 2.4, q["NetVolume"], 6);
    }

    [Fact]
    public void A_room_with_no_finish_takes_its_false_ceilings_height_too()
    {
        var f = Box();
        var room = Room(f, 3, 2);
        Ceiling(f, 3, 2, Plain(f), 2.5);
        Assert.False(Space(f, room).ContainsKey("FinishCeilingHeight"));
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(room), ["attrs"] = new JsonObject { [RoomFinishes.CeilingFromKey] = RoomFinishes.FalseCeilingValue } });
        var q = Space(f, room);
        Assert.Equal(2.5, q["FinishCeilingHeight"], 6);
        Assert.Equal(24 * 2.5, q["NetVolume"], 6);
    }
}
