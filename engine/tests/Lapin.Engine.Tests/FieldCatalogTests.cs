using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 84 (a tester's idea): the Parameters inspector lists every field an element's tags and formulas can read — its own
/// and its scopes' (type, storey, project, room…) — each with the token to write and its value now; and a room's fields from its
/// finish (finish.floor: its top floor layer's material…), worked out on every ask, its own floor finish inheriting the finish's.
/// </summary>
public sealed class FieldCatalogTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());

    static void Set(Fixture f, string id, string name, JsonObject more)
    {
        var a = new JsonObject { ["ids"] = new JsonArray(id), ["name"] = name };
        foreach (var (k, v) in more) a[k] = v?.DeepClone();
        f.Run("element.setParam", a);
    }

    static string Room(Fixture f, string number, string name) => (string)f.Run("architecture.addRoom", new JsonObject
    {
        ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 4, ["name"] = name, ["number"] = number,
    })!["id"]!;

    static string V(Fixture f, string id, string field) => TagText.Value(f.Model, f.Model.Get(id)!, field);

    /// <summary>The fields the query gives, by token: (group, value).</summary>
    static Dictionary<string, (string Group, string? Value)> Fields(Fixture f, string id)
    {
        var o = new Dictionary<string, (string, string?)>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in f.Session.Query("fields.available", new JsonObject { ["id"] = id })!["groups"]!.AsArray().OfType<JsonObject>())
            foreach (var x in g["fields"]!.AsArray().OfType<JsonObject>())
                o[(string)x["token"]!] = ((string)g["id"]!, (string?)x["value"]);
        return o;
    }

    [Fact]
    public void A_wall_in_a_room_on_a_storey_lists_its_own_fields_and_its_scopes_with_their_values()
    {
        var f = F();
        Set(f, f.Storey, "code", new JsonObject { ["kind"] = "text", ["value"] = "K1" });
        f.Run("project.setInfo", new JsonObject { ["number"] = "P-7" });
        f.Run("project.setParameter", new JsonObject { ["key"] = "zone", ["label"] = "Zone", ["type"] = "text", ["value"] = "north" });
        var room = Room(f, "1.04", "Office");
        var wall = f.Wall(1, 1, 5, 1);
        Set(f, wall, "Fire rating", new JsonObject { ["kind"] = "text", ["value"] = "EI 60" });
        Set(f, wall, "Wall code", new JsonObject { ["formula"] = "{storey.code}-{room.number}" });

        var fields = Fields(f, wall);
        // Its own: what every element has, its parameters (a formula's worked out), its quantities.
        Assert.Equal(("element", f.Model.Get(wall)!.Name), fields["name"]);
        Assert.Equal("Wall", fields["class"].Value);
        Assert.Equal(("params", "EI 60"), fields["Fire rating"]);
        Assert.Equal("K1-1.04", fields["Wall code"].Value);
        Assert.Contains(fields, x => x.Value.Group == "quantities" && x.Key == "Length" && x.Value.Value!.StartsWith('4'));
        Assert.Contains(fields, x => x.Value.Group == "standard" && x.Key == "Pset_WallCommon.FireRating");
        // The project's parameters, by their key alone.
        Assert.Equal(("globals", "north"), fields["zone"]);
        // Its scopes': the storey's parameter, the room it stands in, the project's information.
        Assert.Equal(("storey", "K1"), fields["storey.code"]);
        Assert.Equal(("room", "1.04"), fields["room.number"]);
        Assert.Equal(("room", "Office"), fields["room.name"]);
        Assert.Equal(("project", "P-7"), fields["project.number"]);
        // A scope it has none of is not listed (no host, no group).
        Assert.DoesNotContain(fields.Keys, k => k.StartsWith("host.", StringComparison.Ordinal) || k.StartsWith("group.", StringComparison.Ordinal));
        // Each value is what a tag would show for that token.
        foreach (var (token, (_, value)) in fields.Where(x => x.Value.Group != "quantities"))
            Assert.Equal(value, V(f, wall, token));
        // The room's own list: its number as an attribute, the storey above it.
        var roomFields = Fields(f, room);
        Assert.Equal(("attributes", "1.04"), roomFields["number"]);
        Assert.Equal("K1", roomFields["storey.code"].Value);
        Assert.Throws<CommandException>(() => f.Session.Query("fields.available", new JsonObject { ["id"] = "nothing" }));
    }

    [Fact]
    public void A_room_reads_its_finish_s_materials_and_its_floor_finish_inherits_the_finish_s_floor()
    {
        var f = F();
        var room = Room(f, "1", "Bath");
        var wall = f.Wall(1, 1, 5, 1);
        // No finish: nothing to say.
        Assert.Equal("", V(f, room, "finish.floor"));
        Assert.Equal("", V(f, room, "finishFloor"));
        var type = (string)f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = "Bathroom" })!["id"]!;
        f.Run("architecture.applyRoomFinish", new JsonObject { ["typeId"] = type, ["roomIds"] = new JsonArray(room) });
        Assert.Equal("Bathroom", V(f, room, "finish.name"));
        Assert.Equal("Ceramic tiles", V(f, room, "finish.floor"));
        Assert.Equal("Paint", V(f, room, "finish.walls"));
        Assert.Equal("Paint", V(f, room, "finish.ceiling"));
        // Its floor finish, not given, is the finish's floor; a formula and another element through its room read it too.
        Assert.Equal("Ceramic tiles", V(f, room, "finishFloor"));
        Assert.Equal("Ceramic tiles", V(f, wall, "room.finish.floor"));
        Set(f, room, "Label", new JsonObject { ["formula"] = "upper(finish.floor)" });
        Assert.Equal("CERAMIC TILES", V(f, room, "Label"));
        // The inspector lists them, worked out.
        var fields = Fields(f, room);
        Assert.Equal(("derived", "Ceramic tiles"), fields["finish.floor"]);
        Assert.Equal("Ceramic tiles", Fields(f, wall)["room.finish.floor"].Value);
        // A floor finish of the room's own comes first; cleared, the finish's again. Nothing is stored on the room.
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(room), ["attrs"] = new JsonObject { ["finishFloor"] = "Oak parquet" } });
        Assert.Equal("Oak parquet", V(f, room, "finishFloor"));
        Assert.Equal("Ceramic tiles", V(f, room, "finish.floor"));
        Assert.Null(f.Model.Get(room)!.Attrs["finish.floor"]);
        // Another finish laid: what the room reads follows at once.
        var living = (string)f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = "Living room" })!["id"]!;
        f.Run("architecture.applyRoomFinish", new JsonObject { ["typeId"] = living, ["roomIds"] = new JsonArray(room) });
        Assert.Equal("Laminate", V(f, room, "finish.floor"));
        Assert.Equal("LAMINATE", V(f, room, "Label"));
    }
}
