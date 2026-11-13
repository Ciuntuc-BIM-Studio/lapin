using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// A tester's topics #36–#38 (decisions 550, 550): a field or a formula reads beyond its element — its storey's parameters
/// ({storey.code}), its project's (project.number, and the project's own parameters by their key from anywhere), a door's
/// rooms on either side and its wall; a text parameter worked out by a formula ({storey.code}.{Hall}.{Flat}.{number} → K1.A.3.1).
/// Nothing is stored: a storey's parameter changed is what the room's stamp, its formula and the IFC file say at once.
/// </summary>
public sealed class ParamScopeTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-param-scopes").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());

    static void Set(Fixture f, string id, string name, JsonObject more)
    {
        var a = new JsonObject { ["ids"] = new JsonArray(id), ["name"] = name };
        foreach (var (k, v) in more) a[k] = v?.DeepClone();
        f.Run("element.setParam", a);
    }

    static string Room(Fixture f, double x, double y, double w, double d, string number, string name) => (string)f.Run("architecture.addRoom", new JsonObject
    {
        ["storeyId"] = f.Storey, ["x1"] = x, ["y1"] = y, ["x2"] = x + w, ["y2"] = y + d, ["name"] = name, ["number"] = number,
    })!["id"]!;

    static List<string> Texts(Fixture f, string plan) =>
        Views.Render(new Q(f), Views.Find(f.Model, plan)!).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();

    static string V(Fixture f, string id, string field) => TagText.Value(f.Model, f.Model.Get(id)!, field);

    [Fact]
    public void A_room_reads_its_storey_s_parameter_in_a_text_formula_and_its_stamp_follows_a_change()
    {
        var f = F();
        Set(f, f.Storey, "code", new JsonObject { ["kind"] = "text", ["value"] = "K1" });
        var room = Room(f, 0, 0, 5, 4.9, "1", "Living");
        Set(f, room, "Hall", new JsonObject { ["kind"] = "text", ["value"] = "A" });
        Set(f, room, "Flat", new JsonObject { ["kind"] = "number", ["value"] = 3 });
        // A formula with fields is a text parameter of its own accord.
        Set(f, room, "Room index", new JsonObject { ["formula"] = "{storey.code}.{Hall}.{Flat}.{number}" });
        var stored = (f.Model.Get(room)!.Attrs["params"] as JsonArray)!.OfType<JsonObject>().Single(p => (string)p["name"]! == "Room index");
        Assert.Equal("text", (string)stored["kind"]!);
        Assert.Null(stored["value"]);
        Assert.Equal("K1.A.3.1", V(f, room, "Room index"));
        Assert.Equal("K1", V(f, room, "storey.code"));
        Assert.Equal(f.Model.Get(f.Storey)!.Name, V(f, room, "storey.name"));
        // The room's stamp shows it, and its name and area under it.
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "room", ["template"] = "{Room index} | {name} | {area}" } });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        Assert.Contains("K1.A.3.1", Texts(f, plan));
        // The storey's code changed: the formula, the stamp and the file follow, nothing stored on the room.
        Set(f, f.Storey, "code", new JsonObject { ["value"] = "K2" });
        Assert.Equal("K2.A.3.1", V(f, room, "Room index"));
        Assert.Contains("K2.A.3.1", Texts(f, plan));
        Assert.DoesNotContain("K1.A.3.1", Texts(f, plan));
        // The room schedule shows the rooms' parameters, the formula worked out.
        var grid = new RoomSchedule().Grid(new Q(f));
        var column = grid.Columns.ToList().IndexOf("Room index");
        Assert.True(column > 0);
        Assert.Equal("K2.A.3.1", grid.Rows[0][column]);
        var path = Path.Combine(_dir, "rooms.ifc");
        IfcExport.Write(f.Session, path);
        Assert.Contains("'K2.A.3.1'", File.ReadAllText(path));
    }

    [Fact]
    public void A_formula_reads_a_scope_s_numbers_and_the_project_s_parameters_by_their_key()
    {
        var f = F();
        f.Run("project.setParameter", new JsonObject { ["key"] = "zone", ["label"] = "Zone", ["type"] = "text", ["value"] = "north" });
        f.Run("project.setParameter", new JsonObject { ["key"] = "fee", ["label"] = "Fee", ["type"] = "number", ["value"] = "1.5" });
        f.Run("project.setInfo", new JsonObject { ["number"] = "P-12" });
        Set(f, f.Storey, "Floor height", new JsonObject { ["kind"] = "length", ["value"] = 3.2 });
        var wall = f.Wall(0, 0, 5, 0);
        // A number from the storey; the project's parameter by its key alone and by project.…; a field of its information.
        Set(f, wall, "Courses", new JsonObject { ["formula"] = "round(storey.FloorHeight / 0.2)" });
        Set(f, wall, "Cost", new JsonObject { ["formula"] = "fee * 10" });
        Set(f, wall, "Label", new JsonObject { ["formula"] = "upper(zone) & \"/\" & project.number" });
        Assert.Equal("16", V(f, wall, "Courses"));
        Assert.Equal("15", V(f, wall, "Cost"));
        Assert.Equal("NORTH/P-12", V(f, wall, "Label"));
        Assert.Equal("text", (string)(f.Model.Get(wall)!.Attrs["params"] as JsonArray)!.OfType<JsonObject>().Single(p => (string)p["name"]! == "Label")["kind"]!);
        // A tag field reads them too: the project's parameter by its key, the project's information, the building.
        Assert.Equal("north", V(f, wall, "zone"));
        Assert.Equal("P-12", V(f, wall, "project.number"));
        Assert.Equal(Spatial.Building(f.Model)!.Name, V(f, wall, "building.name"));
        // A scope the element has none of is empty, not another field's.
        Assert.Equal("", V(f, wall, "group.name"));
        Assert.Equal("", V(f, wall, "host.name"));
        // The fields a tag may choose list the storey's and the project's.
        var fields = TagText.Fields(f.Model, f.Model.Get(wall)!).OfType<JsonObject>().Select(x => (string)x["field"]!).ToList();
        Assert.Contains("storey.Floor height", fields);
        Assert.Contains("project.zone", fields);
        Assert.Contains("project.number", fields);
        // The query the interface lists the scopes with: the storey named for the wall, no host.
        var scopes = f.Session.Query("params.scopes", new JsonObject { ["id"] = wall })!["scopes"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal(f.Storey, (string?)scopes.Single(s => (string)s["scope"]! == "storey")["id"]);
        Assert.Null((string?)scopes.Single(s => (string)s["scope"]! == "host")["id"]);
        Assert.Contains(scopes, s => (string)s["scope"]! == "toRoom");
    }

    [Fact]
    public void A_door_reads_its_wall_and_the_rooms_on_either_side_turned_over_with_it()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);
        Set(f, wall, "Fire rating", new JsonObject { ["kind"] = "text", ["value"] = "EI 30" });
        var outside = Room(f, 0, -4, 6, 3.8, "1", "Hall");
        var inside = Room(f, 0, 0.2, 6, 3.8, "2", "Office");
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 3 })!["id"]!;
        Assert.Equal("EI 30", V(f, door, "host.Fire rating"));
        var (from, to) = (V(f, door, "fromRoom.number"), V(f, door, "toRoom.number"));
        Assert.Equal(new[] { "1", "2" }, new[] { from, to }.Order().ToArray());
        Assert.Equal(to, V(f, door, "room.number"));
        Set(f, door, "Leads", new JsonObject { ["formula"] = "fromRoom.name & \" → \" & toRoom.name" });
        Assert.Equal($"{V(f, door, "fromRoom.name")} → {V(f, door, "toRoom.name")}", V(f, door, "Leads"));
        // Turned over, its rooms change sides.
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = new JsonArray(door), ["axis"] = "y" });
        Assert.Equal(from, V(f, door, "toRoom.number"));
        Assert.Equal(to, V(f, door, "fromRoom.number"));
        // A wall's room: the one its middle stands in (none here: it stands between them).
        Assert.Equal("", V(f, wall, "room.name"));
        Assert.Equal(outside, ParamScopes.Of(f.Model, f.Model.Get(door)!, from == "1" ? "toRoom" : "fromRoom")?.Id);
        Assert.NotNull(inside);
    }

    [Fact]
    public void A_formula_reading_itself_across_scopes_gives_no_value_and_says_why()
    {
        var f = F();
        var room = Room(f, 0, 0, 4, 4, "1", "Room");
        // The storey reads the room's parameter by no scope it has: unknown; the room reads itself through the storey: none.
        Set(f, f.Storey, "Code", new JsonObject { ["kind"] = "text", ["formula"] = "concat(\"K\"; Level)" });
        Set(f, f.Storey, "Level", new JsonObject { ["kind"] = "number", ["value"] = 1 });
        Set(f, room, "A", new JsonObject { ["formula"] = "B & storey.Code" });
        Set(f, room, "B", new JsonObject { ["formula"] = "A & \"x\"" });
        Assert.Equal("K1", V(f, room, "storey.Code"));
        Assert.Equal("", V(f, room, "A"));
        var a = f.Session.Query("params.evaluate", new JsonObject { ["id"] = room })!["params"]!.AsArray().OfType<JsonObject>().Single(p => (string)p["name"]! == "A");
        Assert.Null(a["value"]);
        Assert.NotNull((string?)a["error"]);
    }
}
