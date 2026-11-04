using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Room finishes (decision 265): a finish type laid in a room as its floor, walls and ceiling, following the room's outline,
/// each measured gross (the outline, nothing deducted) and net (the doors, windows and floor openings taken out); its skirting
/// and its structural slab made with it.
/// </summary>
public class RoomFinishTests
{
    // The default wall type: render 20 mm on the right, a 250 mm core, plaster 15 mm on the left; walls drawn anticlockwise
    // round 5 × 4 m axes have their plaster inside, so the room is 4.72 × 3.72 m between the plaster's faces.
    const double Inside = 0.125 + 0.015;
    const double W = 5 - 2 * Inside, D = 4 - 2 * Inside;

    static (Fixture F, string Room, string[] Walls) Room()
    {
        var f = new Fixture();
        var walls = new[] { ((0.0, 0.0), (5.0, 0.0)), ((5.0, 0.0), (5.0, 4.0)), ((5.0, 4.0), (0.0, 4.0)), ((0.0, 4.0), (0.0, 0.0)) }
            .Select(x => f.Wall(x.Item1.Item1, x.Item1.Item2, x.Item2.Item1, x.Item2.Item2)).ToArray();
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        return (f, room, walls);
    }

    static string Library(Fixture f, string name) => (string)f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = name })!["id"]!;

    static List<Entity> Finishes(Fixture f, string room) => f.Model.OfClass(RoomFinishes.Class).Where(e => e.Str("roomId") == room).ToList();

    static RoomFinishes.FinishMeasure M(Fixture f, string room, string part, string material) =>
        RoomFinishes.Measures(f.Session.Geometry(Finishes(f, room).Single(e => e.Str("part") == part).Id)).Single(x => x.Material == material);

    [Fact]
    public void A_bathroom_lays_its_floor_walls_and_ceiling_in_the_room_and_measures_them_gross_and_net()
    {
        var (f, room, walls) = Room();
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["x"] = 2, ["y"] = 0 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = walls[1], ["along"] = 2 });
        var door = Openings.PlacedOn(f.Model, walls[0]).Single();
        var window = Openings.PlacedOn(f.Model, walls[1]).Single();
        var bathroom = Library(f, "Bathroom");
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomIds"] = new JsonArray(room), ["typeId"] = bathroom });

        Assert.Equal(["ceiling", "floor", "walls"], Finishes(f, room).Select(e => e.Str("part")!).Order());
        Assert.Equal(bathroom, f.Model.Get(room)!.Str("finishType"));
        var height = Rooms.HeightOf(f.Model.Get(room)!, f.Model);

        // The floor: its tiles over the room, from the level down; a bathroom's does not run on through the door.
        var tiles = M(f, room, "floor", "Ceramic tiles");
        Assert.Equal(W * D, tiles.Gross, 6);
        Assert.Equal(W * D, tiles.Net, 6);
        var floor = f.Session.Geometry(Finishes(f, room).Single(e => e.Str("part") == "floor").Id)!;
        var top = Measure.Bounds(floor.Parts.First(p => p.Key == "floor:0").Solid)!.Value;
        Assert.Equal(0, top.Max.Z, 6);
        Assert.Equal(-0.01, top.Min.Z, 6);

        // The walls: the tiles to 2.10 m all round, the paint above them; the door and the window taken out of the net only.
        var perimeter = 2 * (W + D);
        var wallTiles = M(f, room, "walls", "Ceramic tiles");
        Assert.Equal(perimeter * 2.1, wallTiles.Gross, 6);
        var windowInTiles = Math.Max(0, Math.Min(2.1, window.Sill + window.Height) - window.Sill) * window.Width;
        Assert.Equal(perimeter * 2.1 - door.Width * Math.Min(door.Height, 2.1) - windowInTiles + wallTiles.Reveals, wallTiles.Net, 3);
        var paint = M(f, room, "walls", "Paint");
        Assert.Equal(perimeter * (height - 0.011 - 2.1), paint.Gross, 6);
        var plaster = M(f, room, "walls", "Plaster");
        Assert.Equal(perimeter * (height - 0.011), plaster.Gross, 6);
        Assert.Equal(perimeter * (height - 0.011) - door.Width * door.Height - window.Width * window.Height + plaster.Reveals, plaster.Net, 3);
        // The reveals lined from the room's face to the frame: the door's jambs and head, the window's jambs, head and bottom (no sill board inside).
        const double centre = (-0.145 + 0.14) / 2, t = 0.015;
        double Depth(Openings.Placed p) => Inside - Math.Max(centre - (p.Spec!.Frame.Offset + p.Spec.Frame.Depth / 2), centre - (p.Spec.Frame.Offset - p.Spec.Frame.Depth / 2)) + t;
        var doorLining = Depth(door) * (2 * door.Height + door.Width - 2 * t);
        var windowLining = Depth(window) * (2 * window.Height + 2 * (window.Width - 2 * t));
        Assert.Equal(doorLining + windowLining, plaster.Reveals, 6);
        Assert.True(plaster.Reveals > 2 * door.Height * 0.05, $"the plaster lines the reveals ({plaster.Reveals:0.000} m²)");
        Assert.True(M(f, room, "walls", "Paint").Reveals > 0, "the paint lines them too, above the tiles");

        // The ceiling: no slab over the room, so at its height; the covering's own quantities read its face.
        var ceiling = f.Session.Geometry(Finishes(f, room).Single(e => e.Str("part") == "ceiling").Id)!;
        Assert.Equal(height, ceiling.Parts.Max(p => Measure.Bounds(p.Solid)!.Value.Max.Z), 6);
        var cover = f.Session.Quantities(Finishes(f, room).Single(e => e.Str("part") == "floor").Id).Single(q => q.Name == "Qto_CoveringBaseQuantities");
        Assert.Equal(W * D, cover.Quantities.Single(q => q.Name == "GrossArea").Value, 6);

        // The schedule lists each material of each part, then the totals.
        var grid = new RoomFinishSchedule().Grid(f.Session.Reader);
        Assert.Contains(grid.Rows, r => r[3] == "Walls" && r[4] == "Ceramic tiles");
        Assert.Contains(grid.Rows, r => r[0] == "Total" && r[3] == "Floor" && r[4] == "Ceramic tiles");
    }

    [Fact]
    public void A_living_room_takes_its_floor_through_the_door_lays_its_skirting_and_a_slab_when_asked_and_a_new_finish_replaces_it()
    {
        var (f, room, walls) = Room();
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["x"] = 2, ["y"] = 0 });
        var door = Openings.PlacedOn(f.Model, walls[0]).Single();
        var living = Library(f, "Living room");
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = living });
        // The floor goes on through the door to the wall's axis: its net is more than the room's area by the threshold.
        var laminate = M(f, room, "floor", "Laminate");
        Assert.Equal(W * D, laminate.Gross, 6);
        Assert.Equal(W * D + door.Width * Inside, laminate.Net, 6);
        var skirting = Assert.Single(f.Model.OfClass(Skirtings.Class), s => s.Str("finishOf") == room);
        Assert.Equal("MDF 80 bevelled", f.Model.RelatingOf(skirting.Id, IfcClass.RelDefinesByType)!.Name);

        // The ground floor on its slab: the slab under the floor's layers, made a floor of its own; the skirting a tile one now.
        var ground = Library(f, "Ground floor on slab");
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = ground });
        var slab = Assert.Single(f.Model.OfClass(IfcClass.Slab), s => s.Str("finishOf") == room);
        var floorThickness = 0.01 + 0.005 + 0.05 + 0.1;
        Assert.Equal(-floorThickness, Slabs.Top(f.Model, slab), 6);
        Assert.Equal(W * D * 0.15, f.Session.Geometry(slab.Id)!.Parts.Sum(p => Measure.Volume(p.Solid)), 6);
        Assert.Equal("Tile 80 flush", f.Model.RelatingOf(Assert.Single(f.Model.OfClass(Skirtings.Class), s => s.Str("finishOf") == room).Id, IfcClass.RelDefinesByType)!.Name);
        Assert.Equal(3, Finishes(f, room).Count);

        // Taken off: everything it laid goes.
        f.Run("architecture.removeRoomFinish", new JsonObject { ["roomId"] = room });
        Assert.Empty(RoomFinishes.Laid(f.Model, room));
        Assert.Null(f.Model.Get(room)!.Str("finishType"));
    }

    [Fact]
    public void A_finish_follows_its_room_and_takes_the_floor_openings_out_and_a_changed_type_lays_its_rooms_again()
    {
        var (f, room, walls) = Room();
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(-1, -1), new JsonArray(6, -1), new JsonArray(6, 5), new JsonArray(-1, 5)),
        })!["id"]!;
        f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["points"] = new JsonArray(new JsonArray(1, 1), new JsonArray(2, 1), new JsonArray(2, 2), new JsonArray(1, 2)) });
        var tech = Library(f, "Technical room");
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = tech });
        var epoxy = M(f, room, "floor", "Epoxy coating");
        Assert.Equal(W * D, epoxy.Gross, 6);
        Assert.Equal(W * D - 1, epoxy.Net, 6);
        // No ceiling in a technical room.
        Assert.DoesNotContain(Finishes(f, room), e => e.Str("part") == "ceiling");

        // The east wall moved out by a metre: the room grows, and its finish with it.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = walls[1], ["x1"] = 6, ["x2"] = 6 });
        Assert.Equal((W + 1) * D, M(f, room, "floor", "Epoxy coating").Gross, 6);

        // The type changed: a ceiling added, the room laid again.
        var spec = RoomFinishes.SpecOf(f.Model, f.Model.Get(tech)!)! with { Ceiling = [new FinishLayer("Paint", 0.001)] };
        f.Run("architecture.updateRoomFinishType", new JsonObject { ["id"] = tech, ["spec"] = spec.ToJson() });
        Assert.Contains(Finishes(f, room), e => e.Str("part") == "ceiling");
        Assert.Throws<CommandException>(() => RoomFinishSpec.From(new JsonObject { ["name"] = "Bad", ["floor"] = new JsonArray(new JsonObject { ["material"] = "Tiles" }) }));
    }

    // ------------------------------------------------------------ the plan hatch (decision 299)

    static (string Plan, Views.Rendered R) Plan(Fixture f, string? plan = null, JsonObject? attrs = null)
    {
        plan ??= (string)f.Run("view.create", attrs ?? new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        return (plan, Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!));
    }

    static List<(Vec2 A, Vec2 B)> Hatch(Views.Rendered r) => r.Pens.Where(p => p.Layer == Rooms.FinishLayer).SelectMany(p => p.Segments).ToList();

    static bool Horizontal((Vec2 A, Vec2 B) s) => Math.Abs(s.A.Y - s.B.Y) < 1e-9;
    static bool Vertical((Vec2 A, Vec2 B) s) => Math.Abs(s.A.X - s.B.X) < 1e-9;

    [Fact]
    public void A_finished_floor_is_hatched_in_plan_by_its_material_under_the_walls_and_the_room_or_the_view_style_turns_it_off()
    {
        var (f, room, walls) = Room();
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["x"] = 2, ["y"] = 0 });
        var (plan, bare) = Plan(f);
        Assert.Empty(Hatch(bare));

        // A bathroom's floor is ceramic tiles: a square grid, inside the room's outline, under the walls' cut lines.
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = Library(f, "Bathroom") });
        var (_, r) = Plan(f, plan);
        var tiles = Hatch(r);
        Assert.NotEmpty(tiles);
        Assert.All(tiles, s => Assert.True(Horizontal(s) || Vertical(s)));
        Assert.Contains(tiles, Horizontal);
        Assert.Contains(tiles, Vertical);
        Assert.All(tiles, s => Assert.True(s.A.X >= Inside - 1e-6 && s.A.X <= 5 - Inside + 1e-6 && s.A.Y >= Inside - 1e-6 && s.A.Y <= 4 - Inside + 1e-6));
        var pen = r.Pens.Single(p => p.Layer == Rooms.FinishLayer);
        Assert.Equal(RoomFinishes.HatchColor, pen.Stroke);
        Assert.True(r.Pens.IndexOf(pen) < r.Pens.FindIndex(p => p.Layer == PenLayer.Cut));

        // The living room's laminate is boards; its floor runs on through the door, and so does its hatch.
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = Library(f, "Living room") });
        var boards = Hatch(Plan(f, plan).R);
        Assert.NotEmpty(boards);
        var spec = RoomFinishes.SpecOf(f.Model, f.Model.OfClass(RoomFinishes.TypeClass).Single(t => t.Name == "Living room"))!;
        var o = Rooms.Of(f.Model.Get(room)!, f.Model, f.Session.Geometry)!;
        Assert.Equal(2, RoomFinishes.PlanRegions(f.Model, f.Model.Get(room)!, o, spec, "floor").Count);
        Assert.Equal("WOOD", RoomFinishes.HatchOf(f.Model, spec, "floor")!.Pattern);

        // The room says not: no hatch; null brings it back.
        f.Run("element.update", new JsonObject { ["id"] = room, ["attrs"] = new JsonObject { ["finishHatch"] = false } });
        Assert.Empty(Hatch(Plan(f, plan).R));
        f.Run("element.update", new JsonObject { ["id"] = room, ["attrs"] = new JsonObject { ["finishHatch"] = null } });
        Assert.NotEmpty(Hatch(Plan(f, plan).R));

        // The view's style: off in Structural, on in Architectural; the view may turn it off itself.
        Assert.False(ViewStyles.Defaults.Single(s => s.Id == "structural").FinishHatch);
        Assert.True(ViewStyles.Defaults.Single(s => s.Id == "architectural").FinishHatch);
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["style"] = "structural" });
        Assert.Empty(Hatch(Plan(f, plan).R));
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["style"] = null, ["finishHatch"] = false });
        Assert.Empty(Hatch(Plan(f, plan).R));
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["finishHatch"] = null });
        Assert.NotEmpty(Hatch(Plan(f, plan).R));
        // A style saved before the field: on in an architectural one, off in a structural one.
        Assert.True(ViewStyles.Style.From(new JsonObject { ["id"] = "x", ["discipline"] = "Architecture" }).FinishHatch);
        Assert.False(ViewStyles.Style.From(new JsonObject { ["id"] = "y", ["discipline"] = "Structure" }).FinishHatch);
    }

    [Fact]
    public void The_finish_types_own_hatch_wins_over_the_materials_and_none_draws_nothing()
    {
        var (f, room, _) = Room();
        var living = Library(f, "Living room");
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = living });
        var (plan, _) = Plan(f);

        // The material's own surface pattern (Manage → Materials, Elevation) wins over what its name suggests.
        var laminate = Materials.Find(f.Model, "Laminate")!.Id;
        f.Run("material.update", new JsonObject { ["id"] = laminate, ["elevation"] = new JsonObject { ["hatch"] = "LINES45" } });
        var diagonal = Hatch(Plan(f, plan).R);
        Assert.NotEmpty(diagonal);
        Assert.All(diagonal, s => Assert.False(Horizontal(s) || Vertical(s)));

        // The type's own pattern wins over the material's: horizontal lines, in the colour given.
        var spec = RoomFinishes.SpecOf(f.Model, f.Model.Get(living)!)! with { FloorHatch = new FinishHatch("own", "horizontal", 2, 0, "#336699") };
        f.Run("architecture.updateRoomFinishType", new JsonObject { ["id"] = living, ["spec"] = spec.ToJson() });
        Assert.Equal("HORIZONTAL", RoomFinishes.SpecOf(f.Model, f.Model.Get(living)!)!.FloorHatch!.Pattern);
        var (_, r) = Plan(f, plan);
        var own = Hatch(r);
        Assert.NotEmpty(own);
        Assert.All(own, s => Assert.True(Horizontal(s)));
        Assert.Equal("#336699", r.Pens.Single(p => p.Layer == Rooms.FinishLayer).Stroke);

        // None: no hatch; an own hatch without a pattern is refused.
        f.Run("architecture.updateRoomFinishType", new JsonObject { ["id"] = living, ["spec"] = (spec with { FloorHatch = new FinishHatch("none") }).ToJson() });
        Assert.Empty(Hatch(Plan(f, plan).R));
        Assert.Throws<CommandException>(() => f.Run("architecture.updateRoomFinishType", new JsonObject { ["id"] = living, ["spec"] = (spec with { FloorHatch = new FinishHatch("own") }).ToJson() }));
    }

    [Fact]
    public void A_reflected_ceiling_plan_hatches_the_ceiling_and_a_colour_scheme_fills_under_the_hatch()
    {
        var (f, room, _) = Room();
        var bathroom = Library(f, "Bathroom");
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = bathroom });
        var spec = RoomFinishes.SpecOf(f.Model, f.Model.Get(bathroom)!)! with { CeilingHatch = new FinishHatch("own", "CROSS") };
        f.Run("architecture.updateRoomFinishType", new JsonObject { ["id"] = bathroom, ["spec"] = spec.ToJson() });
        var (plan, _) = Plan(f);
        f.Run("view.setRange", new JsonObject { ["id"] = plan, ["look"] = "up" });
        var ceiling = Hatch(Plan(f, plan).R);
        Assert.NotEmpty(ceiling);
        Assert.All(ceiling, s => Assert.False(Horizontal(s) || Vertical(s)));

        // A plan with a colour scheme: the room's fill first, the floor's hatch over it, both under the walls.
        var (plan2, _) = Plan(f);
        f.Run("architecture.viewColorScheme", new JsonObject { ["viewId"] = plan2, ["schemeId"] = "by-function" });
        f.Run("element.update", new JsonObject { ["id"] = room, ["attrs"] = new JsonObject { ["fillColor"] = "#123456" } });
        var r = Plan(f, plan2).R;
        var fill = r.Pens.FindIndex(p => p.Layer == "ROOM-FILL");
        var hatch = r.Pens.FindIndex(p => p.Layer == Rooms.FinishLayer);
        Assert.True(fill >= 0 && hatch > fill && hatch < r.Pens.FindIndex(p => p.Layer == PenLayer.Cut));
    }
}
