using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 80 (a tester's idea): a room separation line drawn in plan bounds rooms as a wall would — a 6 × 4 m room split
/// by a line into two of 3 × 4 m, by an L into 18 and 6 m², by a rectangle into the zone inside it and the rest; deleted, the
/// room is whole again. Nothing is stored on the rooms: they find their outlines again. It is drawn dashed in its storey's
/// plans, has no body, and goes to IFC as an IfcVirtualElement that comes back as one.
/// </summary>
public sealed class RoomDividerTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-room-dividers").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

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

    static string Divider(Fixture f, string kind, params double[] xy) => (string)f.Run("architecture.addRoomDivider", new JsonObject
    {
        ["storeyId"] = f.Storey, ["kind"] = kind,
        ["points"] = new JsonArray([.. xy.Chunk(2).Select(p => (JsonNode)new JsonArray(p[0], p[1]))]),
    })!["id"]!;

    static double Area(Fixture f, string room) => Rooms.NetAreaOf(f.Model.Get(room)!, f.Model, f.Session.Geometry);

    [Fact]
    public void A_line_splits_a_6_by_4_room_into_two_of_3_by_4_and_deleting_it_merges_them_back()
    {
        var f = Box();
        var west = Room(f, 1.5, 2);
        Assert.Equal(24, Area(f, west), 2);
        var line = Divider(f, "line", 3, 0, 3, 4);
        // The room keeps up: its outline found again on its side of the line, nothing stored.
        Assert.Equal(12, Area(f, west), 2);
        // The other side is free: a room placed there is the other half.
        var east = Room(f, 4.5, 2);
        Assert.Equal(12, Area(f, east), 2);
        Assert.True(Rooms.Live(f.Model.Get(west)!, f.Model, f.Session.Catalog)!.Holds(new Lapin.Geometry.Vec2(1, 1)));
        Assert.False(Rooms.Live(f.Model.Get(west)!, f.Model, f.Session.Catalog)!.Holds(new Lapin.Geometry.Vec2(5, 1)));
        // A room cannot be placed on the line itself, nor twice on one side.
        Assert.Throws<CommandException>(() => Room(f, 1, 3));
        // The line moved: both rooms follow.
        f.Run("architecture.updateRoomDivider", new JsonObject { ["id"] = line, ["kind"] = "line", ["points"] = new JsonArray(new JsonArray(2, 0), new JsonArray(2, 4)) });
        Assert.Equal(8, Area(f, west), 2);
        Assert.Equal(16, Area(f, east), 2);
        // Deleted, it is one room again (undo brings the line back, the rooms split again).
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(line) });
        Assert.Null(f.Model.Get(line));
        Assert.Equal(24, Area(f, west), 2);
        f.Session.Undo();
        Assert.Equal(8, Area(f, west), 2);
        // Its length is its quantity.
        Assert.Equal(4, f.Session.Quantities(line).Single().Quantities.Single(q => q.Name == "Length").Value, 3);
    }

    [Fact]
    public void An_L_shaped_divider_and_a_rectangle_make_zones_of_their_own()
    {
        var f = Box();
        var big = Room(f, 1.5, 2);
        Divider(f, "polyline", 3, 0, 3, 2, 6, 2);
        var corner = Room(f, 4.5, 1);
        Assert.Equal(6, Area(f, corner), 2);
        Assert.Equal(18, Area(f, big), 2);

        var g = Box();
        var hall = Room(g, 0.5, 0.5);
        // A rectangle standing free in the room: the zone inside it is a room of its own, the rest goes round it.
        Divider(g, "rectangle", 2, 1, 4, 3);
        var zone = Room(g, 3, 2);
        Assert.Equal(4, Area(g, zone), 2);
        Assert.Equal(20, Area(g, hall), 2);
        // An arc from wall to wall (centred at (4, 2), bulging into the corner) cuts the corner off beyond it.
        var h = Box();
        var main = Room(h, 1, 1);
        Divider(h, "arc", 4, 4, 6, 2, 4 + Math.Sqrt(2), 2 + Math.Sqrt(2));
        var bay = Room(h, 5.6, 3.6);
        Assert.Equal(4 - Math.PI, Area(h, bay), 2);
        Assert.Equal(20 + Math.PI, Area(h, main), 2);
        // A circle standing free: the round zone inside it.
        var round = Divider(h, "circle", 2, 2, 3, 2);
        var disc = Room(h, 2, 2);
        Assert.Equal(Math.PI, Area(h, disc), 2);
        Assert.Equal(20, Area(h, main), 2);
        Assert.Equal(2 * Math.PI, RoomDividers.LengthOf(h.Model.Get(round)!), 2);
    }

    [Fact]
    public void It_is_drawn_dashed_in_its_storey_s_plans_and_goes_to_IFC_and_back_as_a_virtual_element()
    {
        var f = Box();
        var room = Room(f, 1.5, 2);
        var line = Divider(f, "line", 3, 0, 3, 4);
        var e = f.Model.Get(line)!;
        Assert.Equal("IfcVirtualElement", e.Class);
        Assert.Equal("BOUNDARY", e.PredefinedType);
        // No body: nothing in 3D; in plan, its line.
        Assert.Empty(f.Session.Geometry(line)!.Parts);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var (d, _, problem) = Views.Generate(f.Session.Reader, Views.Find(f.Model, plan)!);
        Assert.Null(problem);
        Assert.Contains(d!.Lines, l => l.Element == line);
        // Bad input is refused, said why.
        Assert.Throws<CommandException>(() => Divider(f, "line", 1, 1, 1, 1));
        Assert.Throws<CommandException>(() => Divider(f, "arc", 0, 0, 1, 1, 2, 2));
        Assert.Throws<CommandException>(() => Divider(f, "spiral", 0, 0, 1, 1));

        foreach (var schema in new[] { "IFC4", "IFC4X3" })
        {
            var path = Path.Combine(_dir, $"divider-{schema}.ifc");
            IfcExport.Write(f.Session, path, new IfcExportOptions(Schema: schema));
            var text = File.ReadAllText(path);
            Assert.Contains("IFCVIRTUALELEMENT(", text);
            Assert.Contains("'FootPrint','Curve2D'", text);
            if (schema == "IFC4X3") Assert.Contains(".BOUNDARY.", text);
            else Assert.DoesNotContain(".BOUNDARY.", text);
            // Read back: a room separation line again, the room split by it.
            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            var back = g.Model.Get(line);
            Assert.NotNull(back);
            Assert.True(RoomDividers.Is(back!));
            Assert.Equal(4, RoomDividers.LengthOf(back!), 3);
            Assert.Equal(12, Rooms.NetAreaOf(g.Model.Get(room)!, g.Model, g.Session.Geometry), 1);
        }
    }

    [Fact]
    public void Drafted_linework_converts_to_room_separation_lines()
    {
        var f = Box();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var drawn = f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = plan, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = new JsonArray(3, 0), ["b"] = new JsonArray(3, 4) }),
        })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        f.Run("drafting.convert", new JsonObject { ["viewId"] = plan, ["converter"] = "roomDivider", ["ids"] = new JsonArray(drawn[0]) });
        Assert.Single(f.Model.OfClass(RoomDividers.Class));
        var room = Room(f, 1, 1);
        Assert.Equal(12, Area(f, room), 2);
    }
}
