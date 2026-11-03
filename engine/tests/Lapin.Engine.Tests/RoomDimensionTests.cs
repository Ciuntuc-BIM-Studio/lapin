using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// A room's interior dimensions made at once (a tester's request, decision 249): an aligned string inside along each side,
/// its points at the corners and at the jambs of the doors and windows in that side, held on the walls so they follow them.
/// </summary>
public class RoomDimensionTests
{
    static Fixture New() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());

    /// <summary>A 5 × 4 m box of 20 cm walls on their axes (the room inside 4.80 × 3.80), a door in the south wall at 2 m, the room.</summary>
    static (Fixture F, string[] Walls, string Room) Box()
    {
        var f = New();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        string[] walls = [f.Wall(0, 0, 5, 0, type), f.Wall(5, 0, 5, 4, type), f.Wall(5, 4, 0, 4, type), f.Wall(0, 4, 0, 0, type)];
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["along"] = 2 });
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        return (f, walls, room);
    }

    static string Plan(Fixture f) => "plan:" + f.Storey;

    static List<(Entity Dim, List<Vec2> Pts)> Strings(Fixture f)
    {
        var drawing = Views.Generate(f.Session.Reader, Views.Find(f.Model, Plan(f))!).Drawing!;
        return f.Model.OfClass(IfcClass.Annotation).Where(d => d.Str(RoomDimensions.RoomKey) != null)
            .Select(d => (d, Dimensions.Points(f.Model, "plan", d, drawing).Pts)).ToList();
    }

    static void Near(Vec2 p, double x, double y) => Assert.True((p - new Vec2(x, y)).Length < 1e-6, $"({p.X}, {p.Y}) ≉ ({x}, {y})");

    [Fact]
    public void Each_side_has_its_string_inside_with_the_door_jambs_on_it()
    {
        var (f, _, room) = Box();
        var r = f.Run("drafting.dimensionRooms", new JsonObject { ["viewId"] = Plan(f) })!;
        Assert.Equal(1, (int)r["rooms"]!);
        Assert.Equal(4, (int)r["strings"]!);
        var strings = Strings(f);
        Assert.Equal(4, strings.Count);
        Assert.All(strings, s => Assert.Equal(room, s.Dim.Str(RoomDimensions.RoomKey)));
        // Every point held on what it measures, and every string's line inside the room.
        Assert.All(strings, s => Assert.All(s.Dim.Attrs["refs"]!.AsArray(), x => Assert.NotNull(x)));
        Assert.All(strings, s => { var at = Args.PointOf(s.Dim.Attrs["at"]); Assert.InRange(at.X, 0.1, 4.9); Assert.InRange(at.Y, 0.1, 3.9); });
        // The south side: its corners and the door's jambs, the door 2 m along.
        var south = strings.Single(s => s.Pts.All(p => Math.Abs(p.Y - 0.1) < 1e-6)).Pts.OrderBy(p => p.X).ToList();
        Assert.Equal(4, south.Count);
        Near(south[0], 0.1, 0.1); Near(south[3], 4.9, 0.1);
        Assert.Equal(4, south[1].X + south[2].X, 6);
        Assert.True(south[2].X - south[1].X > 0.6, "the door's opening is measured");
        // The other sides: corner to corner.
        Assert.All(strings.Where(s => s.Pts != south && s.Pts.Count != 4), s => Assert.Equal(2, s.Pts.Count));
    }

    [Fact]
    public void The_strings_follow_a_wall_moved_and_are_made_anew_when_asked_again()
    {
        var (f, walls, room) = Box();
        f.Run("drafting.dimensionRooms", new JsonObject { ["viewId"] = Plan(f), ["ids"] = new JsonArray(room) });
        f.Run("architecture.updateWall", new JsonObject { ["id"] = walls[1], ["x1"] = 6, ["y1"] = 0, ["x2"] = 6, ["y2"] = 4 });
        var south = Strings(f).Single(s => s.Pts.Count == 4).Pts.OrderBy(p => p.X).ToList();
        Near(south[^1], 5.9, 0.1);
        // Asked again: four strings still, not eight.
        f.Run("drafting.dimensionRooms", new JsonObject { ["viewId"] = Plan(f) });
        Assert.Equal(4, Strings(f).Count);
    }

    [Fact]
    public void Asked_on_a_site_plan_the_rooms_are_dimensioned_on_their_floor_plan_at_the_walls_faces()
    {
        // A tester's report (decision 271): run on the site plan, the strings stayed there, held on the walls' axes.
        var (f, _, _) = Box();
        var site = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["site"] = "situation", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("drafting.dimensionRooms", new JsonObject { ["viewId"] = site });
        var r = f.Run("drafting.dimensionRooms", new JsonObject { ["viewId"] = site })!;
        Assert.Equal(4, (int)r["strings"]!);
        Assert.Equal(f.Model.Get(f.Storey)!.Name, (string)r["views"]![0]!);
        var strings = Strings(f);
        Assert.Equal(4, strings.Count);
        Assert.All(strings, s => Assert.Equal(Plan(f), s.Dim.Str("viewId")));
        Assert.All(strings, s => Assert.All(s.Dim.Attrs["refs"]!.AsArray(), x => Assert.Equal("edge", (string?)x!["kind"])));
        var south = strings.Single(s => s.Pts.Count == 4).Pts.OrderBy(p => p.X).ToList();
        Near(south[0], 0.1, 0.1); Near(south[^1], 4.9, 0.1);
    }

    [Fact]
    public void Dimensions_move_and_copy_to_another_plan_holding_on_what_it_cuts()
    {
        // Decision 274: a string made on the site plan (where it held on nothing) moved to the floor plan holds on the walls there.
        var (f, walls, _) = Box();
        var site = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["site"] = "situation", ["storeyId"] = f.Storey })!["id"]!;
        var dim = (string)f.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = site, ["a"] = new JsonArray(0.1, 2), ["b"] = new JsonArray(4.9, 2), ["at"] = new JsonArray(2.5, 2.5),
        })!["id"]!;
        var text = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = site, ["at"] = new JsonArray(1, 1), ["text"] = "Note" })!["id"]!;
        f.Run("drafting.toView", new JsonObject { ["ids"] = new JsonArray(dim, text), ["viewId"] = Plan(f) });
        var moved = f.Model.Get(dim)!;
        Assert.Equal(Plan(f), moved.Str("viewId"));
        Assert.Equal(Plan(f), f.Model.Get(text)!.Str("viewId"));
        Assert.All(moved.Attrs["refs"]!.AsArray(), x => Assert.Equal("edge", (string?)x!["kind"]));
        // It follows the east wall now.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = walls[1], ["x1"] = 6, ["y1"] = 0, ["x2"] = 6, ["y2"] = 4 });
        var drawing = Views.Generate(f.Session.Reader, Views.Find(f.Model, Plan(f))!).Drawing!;
        Near(Dimensions.Points(f.Model, "plan", f.Model.Get(dim)!, drawing).Pts[^1], 5.9, 2);

        // Copied back: the original stays, the copy is on the site plan.
        var copies = f.Run("drafting.toView", new JsonObject { ["ids"] = new JsonArray(dim), ["viewId"] = site, ["copy"] = true })!["ids"]!.AsArray();
        var copy = f.Model.Get((string)copies[0]!)!;
        Assert.NotEqual(dim, copy.Id);
        Assert.Equal(site, copy.Str("viewId"));
        Assert.Equal(Plan(f), f.Model.Get(dim)!.Str("viewId"));
        Assert.Throws<CommandException>(() => f.Run("drafting.toView", new JsonObject { ["ids"] = new JsonArray(walls[0]), ["viewId"] = site }));
    }

    [Fact]
    public void Only_rooms_on_a_plan_are_dimensioned()
    {
        var (f, walls, _) = Box();
        Assert.Throws<CommandException>(() => f.Run("drafting.dimensionRooms", new JsonObject { ["viewId"] = Plan(f), ["ids"] = new JsonArray(walls[0]) }));
        Assert.Throws<CommandException>(() => f.Run("drafting.dimensionRooms", new JsonObject { ["viewId"] = Plan(f), ["offset"] = -1 }));
        Assert.Throws<CommandException>(() => f.Run("drafting.dimensionRooms", new JsonObject { ["viewId"] = "nothing" }));
    }

    [Fact]
    public void Sides_in_line_are_one_side_and_short_ones_are_left_out()
    {
        // An L-shaped room with a pier's 10 cm return and a side split in two in line.
        Vec2[] outline = [new(0, 0), new(2, 0), new(4, 0), new(4, 3), new(2, 3), new(2, 3.1), new(0, 3.1)];
        var made = RoomDimensions.Strings(outline, [new Vec2(1, 0)], 0.5);
        Assert.Equal(5, made.Count);
        var bottom = made.Single(m => m.Pts.All(p => Math.Abs(p.Y) < 1e-9)).Pts;
        Assert.Equal([0, 1, 4], bottom.Select(p => p.X));
        Assert.DoesNotContain(made, m => (m.Pts[^1] - m.Pts[0]).Length < RoomDimensions.MinSide);
    }
}
