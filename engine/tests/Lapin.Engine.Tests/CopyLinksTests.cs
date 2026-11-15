using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Architecture.Stairs;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Links between elements held in attributes kept right through element.transform's move, turn, mirror and copy and through
/// deletion (audit 15.11.2026, §3a): a copy names the copies of what its original named; what is laid for, hung on or drawn in
/// an element goes, follows or lets go with it; each in one undo step.
/// </summary>
public class CopyLinksTests
{
    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);
    static JsonArray Ids(params string[] ids) => new([.. ids.Select(x => (JsonNode)x)]);
    static string Id(JsonNode? r) => (string)r!["id"]!;
    static List<string> Made(JsonNode? r) => r!["ids"]!.AsArray().Select(x => (string)x!).ToList();

    static JsonNode? Move(Fixture f, double dx, double dy, params string[] ids) => f.Run("element.transform", new JsonObject { ["ids"] = Ids(ids), ["op"] = "move", ["by"] = new JsonArray(dx, dy) });
    static List<string> Copy(Fixture f, double dx, double dy, params string[] ids) => Made(f.Run("element.transform", new JsonObject { ["ids"] = Ids(ids), ["op"] = "copy", ["by"] = new JsonArray(dx, dy) }));
    static JsonNode? Rotate(Fixture f, double cx, double cy, double angle, params string[] ids) =>
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(ids), ["op"] = "rotate", ["center"] = new JsonArray(cx, cy), ["angle"] = angle });
    /// <summary>Mirrored about the line x = <paramref name="x"/>.</summary>
    static JsonNode? Mirror(Fixture f, double x, bool copy, params string[] ids) =>
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(ids), ["op"] = "mirror", ["a"] = new JsonArray(x, 0), ["b"] = new JsonArray(x, 1), ["copy"] = copy });

    static List<Vec2> Points(Entity e) => e.Attrs["points"]!.AsArray().Select(p => Args.PointOf(p)).ToList();
    static int Count(Fixture f) => f.Model.Entities.Count();

    // ------------------------------------------------------------ G2: lines drawn in plan

    [Fact]
    public void A_railing_drawn_in_plan_moves_turns_mirrors_and_its_copy_stands_apart()
    {
        var f = new Fixture(new ArchitectureModule());
        var r = Id(f.Run("architecture.addRailing", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (4, 0), (4, 2)) }));
        Move(f, 1, 2, r);
        Assert.Equal([new Vec2(1, 2), new Vec2(5, 2), new Vec2(5, 4)], Points(f.Model.Get(r)!));
        Rotate(f, 1, 2, 90, r);
        var turned = Points(f.Model.Get(r)!);
        Assert.Equal(1, turned[1].X, 9);
        Assert.Equal(6, turned[1].Y, 9);
        Mirror(f, 0, false, r);
        Assert.Equal(-1, Points(f.Model.Get(r)!)[1].X, 9);
        var before = Points(f.Model.Get(r)!);
        var copy = Assert.Single(Copy(f, 10, 0, r));
        Assert.Equal(before.Select(p => p + new Vec2(10, 0)), Points(f.Model.Get(copy)!));
        Assert.Equal(before, Points(f.Model.Get(r)!));
        // Its body goes with its points.
        Assert.Equal(f.Bounds(r).Min.X + 10, f.Bounds(copy).Min.X, 6);
    }

    [Fact]
    public void A_skirting_drawn_along_a_line_moves_and_mirrored_keeps_its_board_on_the_mirrored_side()
    {
        var f = new Fixture(new ArchitectureModule());
        var s = Id(f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (3, 0)), ["side"] = "left" }));
        Move(f, 0, 1, s);
        Assert.Equal([new Vec2(0, 1), new Vec2(3, 1)], Points(f.Model.Get(s)!));
        var board = f.Bounds(s);
        Assert.True(board.Min.Y >= 1 - 1e-6, "on the left of a line running +x: above it");
        Mirror(f, 5, false, s);
        var e = f.Model.Get(s)!;
        Assert.Equal("right", e.Str("side"));
        Assert.Equal([new Vec2(10, 1), new Vec2(7, 1)], Points(e));
        // The line runs −x now: its right is still above it, as the board was.
        Assert.True(f.Bounds(s).Min.Y >= 1 - 1e-6);
    }

    // ------------------------------------------------------------ G3, M6: a room's finish

    static (Fixture F, string Room, string Type) FinishedRoom()
    {
        var f = new Fixture(new ArchitectureModule());
        var room = Id(f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (4, 0), (4, 3), (0, 3)) }));
        var type = Id(f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = "Ground floor on slab" }));
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = type });
        return (f, room, type);
    }

    static Entity SlabOf(Fixture f, string room) => f.Model.OfClass(IfcClass.Slab).Single(s => s.Str("finishOf") == room);

    [Fact]
    public void A_room_deleted_takes_its_finish_its_skirting_and_its_slab_and_undo_brings_them_back()
    {
        var (f, room, _) = FinishedRoom();
        var laid = RoomFinishes.Laid(f.Model, room).Select(e => e.Id).ToList();
        Assert.Equal(5, laid.Count);   // floor, walls, ceiling, skirting, slab
        var before = Count(f);
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(room) });
        Assert.All(laid, id => Assert.Null(f.Model.Get(id)));
        f.Session.Undo();
        Assert.Equal(before, Count(f));
        Assert.All(laid, id => Assert.NotNull(f.Model.Get(id)));
        // Deleted with some of its pieces selected too: the same.
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(room, laid[0]) });
        Assert.All(laid, id => Assert.Null(f.Model.Get(id)));
    }

    [Fact]
    public void A_room_moved_turned_or_mirrored_takes_its_finish_along_keeping_every_GlobalId()
    {
        var (f, room, _) = FinishedRoom();
        var laid = RoomFinishes.Laid(f.Model, room).Select(e => e.Id).Order().ToList();
        Move(f, 10, 5, room);
        Assert.Equal(laid, RoomFinishes.Laid(f.Model, room).Select(e => e.Id).Order());
        var slab = SlabOf(f, room);
        Assert.Equal(10, Slabs.Contour(slab).Min(p => p.X), 6);
        Assert.Equal(5, Slabs.Contour(slab).Min(p => p.Y), 6);
        var skirting = f.Model.OfClass(Skirtings.Class).Single(s => s.Str("finishOf") == room);
        Assert.Equal(10, Points(skirting).Min(p => p.X), 6);
        Rotate(f, 10, 5, 90, room);
        Assert.Equal(7, Slabs.Contour(SlabOf(f, room)).Min(p => p.X), 6);
        Mirror(f, 0, false, room);
        Assert.Equal(-10, Slabs.Contour(SlabOf(f, room)).Min(p => p.X), 6);
        Assert.Equal(laid, RoomFinishes.Laid(f.Model, room).Select(e => e.Id).Order());
    }

    [Fact]
    public void A_finished_room_copied_gets_a_finish_of_its_own_once_and_a_piece_copied_alone_makes_nothing()
    {
        var (f, room, type) = FinishedRoom();
        var laid = RoomFinishes.Laid(f.Model, room).Select(e => e.Id).ToList();
        var copy = Assert.Single(Copy(f, 10, 0, room));
        Assert.Equal(type, f.Model.Get(copy)!.Str("finishType"));
        var own = RoomFinishes.Laid(f.Model, copy).ToList();
        Assert.Equal(5, own.Count);
        Assert.Equal(10, Slabs.Contour(SlabOf(f, copy)).Min(p => p.X), 6);
        Assert.Equal(laid.Order(), RoomFinishes.Laid(f.Model, room).Select(e => e.Id).Order());
        // Copied with its pieces selected (a window round them all): one finish for the copy, its pieces the copies.
        var made = Copy(f, 0, 10, [room, .. laid]);
        var twin = made.Single(id => f.Model.Get(id)?.Class == Rooms.Class);
        Assert.Equal(5, RoomFinishes.Laid(f.Model, twin).Count());
        Assert.Equal(10, Slabs.Contour(SlabOf(f, twin)).Min(p => p.Y), 6);
        Assert.Equal(5, RoomFinishes.Laid(f.Model, room).Count());
        // A piece alone: it belongs to its room, so no copy stays; one undo step for each.
        var before = Count(f);
        Copy(f, 0, 20, laid[0], laid[3]);
        Assert.Equal(before, Count(f));
        Assert.Equal(5, RoomFinishes.Laid(f.Model, room).Count());
    }

    [Fact]
    public void A_changed_finish_type_lays_its_rooms_again_keeping_the_GlobalIds_of_what_stays()
    {
        var (f, room, type) = FinishedRoom();
        string Of(string part) => RoomFinishes.Laid(f.Model, room).Single(e => e.Str("part") == part || (part == "skirting" && e.Class == Skirtings.Class) || (part == "slab" && e.Class == IfcClass.Slab)).Id;
        var (floor, walls, ceiling, skirting, slab) = (Of("floor"), Of("walls"), Of("ceiling"), Of("skirting"), Of("slab"));
        var spec = RoomFinishes.SpecOf(f.Model, f.Model.Get(type)!)! with { Ceiling = null, Slab = new FinishSlab("Reinforced concrete", 0.2, 0) };
        f.Run("architecture.updateRoomFinishType", new JsonObject { ["id"] = type, ["spec"] = spec.ToJson() });
        Assert.Equal(floor, Of("floor"));
        Assert.Equal(walls, Of("walls"));
        Assert.Equal(skirting, Of("skirting"));
        Assert.Equal(slab, Of("slab"));
        Assert.Null(f.Model.Get(ceiling));
        Assert.Equal(4, RoomFinishes.Laid(f.Model, room).Count());
        // The slab, kept, is 200 mm now.
        Assert.Equal(4 * 3 * 0.2, f.Session.Geometry(slab)!.Parts.Sum(p => Measure.Volume(p.Solid)), 6);
    }

    // ------------------------------------------------------------ roofs: downpipes, attached walls

    static JsonArray House10x6 => Pts((0, 0), (10, 0), (10, 6), (0, 6));

    [Fact]
    public void A_roof_copied_with_its_downpipes_gives_each_roof_its_own_once()
    {
        var f = new Fixture(new ArchitectureModule());
        var roof = Id(f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = House10x6, ["style"] = "fr_provence" }));
        var pipes = Made(f.Run("architecture.makeDownpipes", new JsonObject { ["id"] = roof }));
        Assert.NotEmpty(pipes);
        int On(string r) => f.Model.OfClass(Drainage.DownpipeClass).Count(p => p.Str("roofId") == r);
        var copy = Copy(f, 20, 0, [roof, .. pipes]).Single(id => f.Model.Get(id)?.Class == IfcClass.Roof);
        Assert.Equal(pipes.Count, On(roof));
        Assert.Equal(pipes.Count, On(copy));
        // A downpipe alone stays on its gutter: no copy is made, a move leaves it.
        var before = Count(f);
        Copy(f, 5, 0, pipes[0]);
        Assert.Equal(before, Count(f));
    }

    [Fact]
    public void A_wall_attached_to_a_roof_copied_with_it_is_attached_to_the_roofs_copy()
    {
        var f = new Fixture(new ArchitectureModule());
        var roof = Id(f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = House10x6, ["style"] = "traditional_campie" }));
        var wall = f.Wall(5, 0.5, 5, 5.5);
        f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = Ids(wall), ["roofIds"] = Ids(roof) });
        var made = Copy(f, 30, 0, roof, wall);
        var (roofCopy, wallCopy) = (made.Single(id => f.Model.Get(id)?.Class == IfcClass.Roof), made.Single(id => f.Model.Get(id)?.Class == IfcClass.Wall));
        Assert.Equal([roofCopy], f.Model.Get(wallCopy)!.Attrs["roofIds"]!.AsArray().Select(x => (string)x!));
        Assert.Equal(f.Bounds(wall).Max.Z, f.Bounds(wallCopy).Max.Z, 6);
        // Copied alone, it stays attached to the roof it was.
        var alone = Assert.Single(Copy(f, 1, 0, wall));
        Assert.Equal([roof], f.Model.Get(alone)!.Attrs["roofIds"]!.AsArray().Select(x => (string)x!));
        // Arrays copy as copies do.
        var arr = Made(f.Run("element.array", new JsonObject { ["ids"] = Ids(roof, wall), ["count"] = 2, ["step"] = new JsonArray(0, 20, 0) }));
        var arrWall = arr.Single(id => f.Model.Get(id)?.Class == IfcClass.Wall);
        var arrRoof = arr.Single(id => f.Model.Get(id)?.Class == IfcClass.Roof);
        Assert.Equal([arrRoof], f.Model.Get(arrWall)!.Attrs["roofIds"]!.AsArray().Select(x => (string)x!));
    }

    // ------------------------------------------------------------ stairs

    static (Fixture F, string Upper, string Slab) TwoStoreys()
    {
        var f = new Fixture(new ArchitectureModule());
        var upper = Id(f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 }));
        var slab = Id(f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = upper, ["points"] = Pts((-12, -6), (12, -6), (12, 6), (-12, 6)) }));
        return (f, upper, slab);
    }

    static string Stair(Fixture f, string shape = "straight", string side = "left", double x = 0, double y = 0)
    {
        var spec = StairSpec.Default() with { Shape = shape, Name = shape };
        var type = Id(f.Run("architecture.createStairType", new JsonObject { ["spec"] = spec.ToJson() }));
        return Id(f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = x, ["y"] = y, ["x2"] = x + 1, ["y2"] = y, ["turn"] = side }));
    }

    static List<Entity> OpeningsOf(Fixture f, string stair) => f.Model.OfClass(IfcClass.OpeningElement).Where(o => o.Str("stairId") == stair).ToList();

    [Fact]
    public void A_stair_copied_or_moved_has_its_one_opening_in_the_floor_above_and_a_floor_copied_alone_takes_none()
    {
        var (f, _, slab) = TwoStoreys();
        var stair = Stair(f);
        Assert.Single(OpeningsOf(f, stair));
        // The copy its own, in the floor over it; the original keeps its one.
        var copy = Assert.Single(Copy(f, 5, 0, stair));
        Assert.Equal(slab, f.Model.RelatingOf(Assert.Single(OpeningsOf(f, copy)).Id, IfcClass.RelVoidsElement)!.Id);
        Assert.Single(OpeningsOf(f, stair));
        // The floor copied alone: no second opening for either stair.
        var slabCopy = Assert.Single(Copy(f, 0, 30, slab));
        Assert.Single(OpeningsOf(f, stair));
        Assert.Single(OpeningsOf(f, copy));
        Assert.DoesNotContain(f.Model.RelatedOf(slabCopy, IfcClass.RelVoidsElement), o => o.Str("stairId") != null);
        // Floor and stair copied together: the floor's copy holds the stair's copy's opening.
        var both = Copy(f, 0, 60, stair, slab);
        var (s2, f2) = (both.Single(id => f.Model.Get(id)?.Class == IfcClass.Stair), both.Single(id => f.Model.Get(id)?.Class == IfcClass.Slab));
        Assert.Equal(f2, f.Model.RelatingOf(Assert.Single(OpeningsOf(f, s2)).Id, IfcClass.RelVoidsElement)!.Id);
        Assert.Single(OpeningsOf(f, stair));
        // Moved out from under the floor: its opening goes; back, it comes again.
        Move(f, 100, 0, stair);
        Assert.Empty(OpeningsOf(f, stair));
        Move(f, -100, 0, stair);
        Assert.Single(OpeningsOf(f, stair));
        // Deleted: every opening that named it goes.
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(stair) });
        Assert.Empty(OpeningsOf(f, stair));
    }

    [Fact]
    public void A_mirrored_L_stair_turns_the_other_way_and_its_space_under_and_railing_change_sides()
    {
        var (f, _, _) = TwoStoreys();
        var stair = Stair(f, "l", "left", 2, 0);
        f.Run("architecture.updateStair", new JsonObject { ["id"] = stair, ["railingSides"] = "left" });
        var nook = Id(f.Run("architecture.addUnderStair", new JsonObject { ["stairId"] = stair, ["style"] = "storage", ["side"] = "left" }));
        var before = f.Bounds(stair);
        var copy = Mirror(f, 0, true, stair)!["ids"]!.AsArray().Select(x => (string)x!).Single();
        var e = f.Model.Get(copy)!;
        Assert.Equal("right", e.Str("turn"));
        Assert.Equal("right", e.Str("railingSides"));
        // Its body the mirror image of the original's.
        var after = f.Bounds(copy);
        Assert.Equal(-before.Max.X, after.Min.X, 6);
        Assert.Equal(-before.Min.X, after.Max.X, 6);
        Assert.Equal(before.Min.Y, after.Min.Y, 6);
        Assert.Equal(before.Max.Y, after.Max.Y, 6);
        Assert.Single(OpeningsOf(f, copy));
        // The original's space stays under the original, on its side.
        Assert.Equal(stair, f.Model.Get(nook)!.Str("host"));
        Assert.Equal("left", f.Model.Get(nook)!.Str("side"));
        // Mirrored with its space: the space under the copy, open on the other side.
        var made = Mirror(f, 0, true, stair, nook)!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        var (s2, n2) = (made.Single(id => f.Model.Get(id)?.Class == IfcClass.Stair), made.Single(id => f.Model.Get(id)?.Class == UnderStairs.Class));
        Assert.Equal(s2, f.Model.Get(n2)!.Str("host"));
        Assert.Equal("right", f.Model.Get(n2)!.Str("side"));
        Assert.Null(f.Session.Geometry(n2)!.Diagnostics);
        // Mirrored in place, the stair itself turns the other way, its space with it.
        Mirror(f, 0, false, stair);
        Assert.Equal("right", f.Model.Get(stair)!.Str("turn"));
        Assert.Equal("right", f.Model.Get(nook)!.Str("side"));
        Assert.Single(OpeningsOf(f, stair));
    }

    [Fact]
    public void A_space_under_a_stair_copied_with_it_is_under_the_copy()
    {
        var (f, _, _) = TwoStoreys();
        var stair = Stair(f);
        var nook = Id(f.Run("architecture.addUnderStair", new JsonObject { ["stairId"] = stair, ["style"] = "storage" }));
        var made = Copy(f, 4, 0, stair, nook);
        var (s2, n2) = (made.Single(id => f.Model.Get(id)?.Class == IfcClass.Stair), made.Single(id => f.Model.Get(id)?.Class == UnderStairs.Class));
        Assert.Equal(s2, f.Model.Get(n2)!.Str("host"));
        Assert.Equal(f.Bounds(nook).Min.X + 4, f.Bounds(n2).Min.X, 6);
        Assert.Single(UnderStairs.Of(f.Model, stair));
    }

    // ------------------------------------------------------------ steel

    [Fact]
    public void A_hall_deleted_or_rebuilt_takes_the_connections_made_to_its_members_on_their_own()
    {
        var f = new Fixture(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());
        var hall = Id(f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = new JsonObject { ["secondary"] = false, ["nodes"] = false } }));
        f.Run("steel.findNodes", new JsonObject());
        var found = f.Model.OfClass(SteelNodes.Class).Select(n => n.Id).ToList();
        Assert.NotEmpty(found);
        Assert.Empty(Halls.Nodes(f.Model, hall));
        bool Dangling() => f.Model.OfClass(SteelNodes.Class).Any(n => SteelNodes.MembersOf(n).Any(j => f.Model.Get(j.Id) == null));
        // Fewer bays: the last frame's members go, and the connections found on them.
        f.Run("steel.updateHall", new JsonObject { ["id"] = hall, ["attrs"] = new JsonObject { ["bays"] = new JsonArray(6, 6) } });
        Assert.False(Dangling());
        Assert.True(f.Model.OfClass(SteelNodes.Class).Count() < found.Count);
        var before = Count(f);
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(hall) });
        Assert.Empty(f.Model.OfClass(SteelNodes.Class));
        f.Session.Undo();
        Assert.Equal(before, Count(f));
        Assert.False(Dangling());
    }

    // ------------------------------------------------------------ rebar

    [Fact]
    public void A_bars_host_deleted_lets_the_bar_go_and_leaves_it_where_the_cover_had_laid_it()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());
        var beam = Id(f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 }));
        var top = Beams.Top(f.Model, f.Model.Get(beam)!);
        // A section along the beam, 30 mm off its axis: the cover's places are off the plane.
        var along = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0.03), ["end"] = new JsonArray(6, 0.03), ["scale"] = 50 }));
        var bar = Id(f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(1, top - 0.2), ["params"] = new JsonObject { ["length"] = 4 }, ["diameter"] = 0.016, ["across"] = new JsonObject { ["count"] = 3, ["mode"] = "cover" } }));
        Assert.Equal(beam, f.Model.Get(bar)!.Str("host"));
        List<double> Ys() => Rebar3D.Bars(f.Model, null, id => f.Session.Geometry(id), f.Model.Get(bar)!, RebarSettings.Of(f.Model)).Select(b => Math.Round(b[0].Y, 6)).Order().ToList();
        var placed = Ys();
        Assert.Equal(3, placed.Count);
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(beam) });
        var e = f.Model.Get(bar)!;
        Assert.Null(e.Str("host"));
        Assert.Equal("step", Rebar3D.Across.Of(e)!.Mode);
        Assert.Equal(placed, Ys());
        f.Session.Undo();
        Assert.Equal(beam, f.Model.Get(bar)!.Str("host"));
        Assert.Equal("cover", Rebar3D.Across.Of(f.Model.Get(bar)!)!.Mode);
    }

    // ------------------------------------------------------------ M4: a ceiling's grid

    [Fact]
    public void A_ceilings_grid_goes_with_it_moved_turned_mirrored_or_copied_its_fixtures_in_their_cells()
    {
        var f = new Fixture(new ArchitectureModule());
        var c = Id(f.Run("architecture.addCeiling", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (3.6, 0), (3.6, 4.8), (0, 4.8)), ["height"] = 2.6 }));
        f.Run("architecture.updateCeiling", new JsonObject { ["id"] = c, ["origin"] = new JsonArray(0.1, 0.2) });
        f.Run("architecture.ceilingFixture", new JsonObject { ["id"] = c, ["x"] = 1.0, ["y"] = 1.0, ["kind"] = "light" });
        Vec2 Light(string id)
        {
            var e = f.Model.Get(id)!;
            var g = Ceilings.GridOf(f.Model, e);
            var (_, i, j) = Assert.Single(Ceilings.Fixtures(e));
            Assert.True(g.Whole(i, j));
            return g.World(new Vec2((i + 0.5) * g.W, (j + 0.5) * g.L));
        }
        var at = Light(c);
        var whole = Ceilings.GridOf(f.Model, f.Model.Get(c)!).WholeCells().Count();
        void Same(Vec2 want, string id)
        {
            var got = Light(id);
            Assert.Equal(want.X, got.X, 6);
            Assert.Equal(want.Y, got.Y, 6);
            Assert.Equal(whole, Ceilings.GridOf(f.Model, f.Model.Get(id)!).WholeCells().Count());
        }
        Move(f, 2, 1, c);
        Same(at + new Vec2(2, 1), c);
        Rotate(f, 0, 0, 90, c);
        var turned = new Vec2(-(at.Y + 1), at.X + 2);
        Same(turned, c);
        Assert.Equal(90, f.Model.Get(c)!.Num(Ceilings.TurnKey), 6);
        Mirror(f, 0, false, c);
        var mirrored = new Vec2(-turned.X, turned.Y);
        Same(mirrored, c);
        var copy = Assert.Single(Copy(f, 10, 0, c));
        Same(mirrored + new Vec2(10, 0), copy);
        // A centred grid stays centred, turned with its ceiling.
        f.Run("architecture.updateCeiling", new JsonObject { ["id"] = copy, ["origin"] = null });
        var centred = Light(copy);
        Rotate(f, 0, 0, 180, copy);
        Assert.Null(f.Model.Get(copy)!.Attrs["origin"]);
        var back = Light(copy);
        Assert.Equal(-centred.X, back.X, 6);
        Assert.Equal(-centred.Y, back.Y, 6);
    }
}
