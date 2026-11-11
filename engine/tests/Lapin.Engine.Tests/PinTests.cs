using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Mesh;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Elements pinned where they are (decision 449): not moved, turned, mirrored, raised, sent to another storey nor deleted
/// until unpinned; their parameters still change; copies are not pinned; a joined neighbour that moves leaves their ends.
/// </summary>
public class PinTests
{
    static JsonArray Ids(params string[] ids) => new([.. ids.Select(x => (JsonNode)x)]);
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static void Pin(Fixture f, bool pinned, params string[] ids) => f.Run("element.pin", new JsonObject { ["ids"] = Ids(ids), ["pinned"] = pinned });

    [Fact]
    public void A_pinned_wall_is_not_moved_turned_mirrored_raised_deleted_nor_sent_up_until_unpinned()
    {
        var f = F();
        var wall = f.Wall(0, 0, 4, 0);
        Pin(f, true, wall);
        Assert.True(Pins.Is(f.Model.Get(wall)));
        foreach (var args in new[]
        {
            new JsonObject { ["op"] = "move", ["by"] = new JsonArray(1, 0) },
            new JsonObject { ["op"] = "rotate", ["center"] = new JsonArray(0, 0), ["angle"] = 90 },
            new JsonObject { ["op"] = "mirror", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(0, 1) },
            new JsonObject { ["op"] = "move", ["by"] = new JsonArray(0, 0), ["up"] = 1 },
        })
        {
            args["ids"] = Ids(wall);
            var ex = Assert.Throws<CommandException>(() => f.Run("element.transform", args));
            Assert.Contains("pinned", ex.Message);
        }
        Assert.Throws<CommandException>(() => f.Run("element.delete", new JsonObject { ["ids"] = Ids(wall) }));
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["end"] = new JsonArray(6, 0) } }));
        var upper = Spatial.Storeys(f.Model).Last().Id;
        Assert.Throws<CommandException>(() => f.Run("element.moveToStorey", new JsonObject { ["ids"] = Ids(wall), ["storeyId"] = upper }));
        Assert.Equal(4, Args.PointOf(f.Model.Get(wall)!.Attrs["end"]).X, 9);

        Pin(f, false, wall);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(wall), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        Assert.Equal(5, Args.PointOf(f.Model.Get(wall)!.Attrs["end"]).X, 9);
    }

    [Fact]
    public void A_pinned_element_is_copied_its_copies_unpinned_and_its_parameters_still_change()
    {
        var f = F();
        var wall = f.Wall(0, 0, 4, 0);
        Pin(f, true, wall);
        var copy = (string)f.Run("element.transform", new JsonObject { ["ids"] = Ids(wall), ["op"] = "copy", ["by"] = new JsonArray(0, 3) })!["ids"]![0]!;
        Assert.False(Pins.Is(f.Model.Get(copy)));
        var arrayed = f.Run("element.array", new JsonObject { ["ids"] = Ids(wall), ["count"] = 3, ["step"] = new JsonArray(0, 6, 0) })!["ids"]!.AsArray();
        Assert.All(arrayed, id => Assert.False(Pins.Is(f.Model.Get((string)id!))));
        // A field that does not place it: changed.
        var height = f.Model.Get(wall)!.Attrs["height"] != null ? "height" : null;
        if (height != null)
        {
            f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { [height] = 2.5 } });
            Assert.Equal(2.5, Json.Number(f.Model.Get(wall)!.Attrs[height]));
        }
    }

    [Fact]
    public void A_wall_moved_leaves_a_pinned_neighbour_its_ends()
    {
        var f = F();
        var a = f.Wall(0, 0, 4, 0);
        var b = f.Wall(4, 0, 4, 3);
        Assert.NotEmpty(f.Model.RelationsOf(a, IfcClass.RelConnectsPathElements));
        Pin(f, true, b);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(a), ["op"] = "move", ["by"] = new JsonArray(0, -1) });
        Assert.Equal(0, Args.PointOf(f.Model.Get(b)!.Attrs["start"]).Y, 9);
    }

    [Fact]
    public void A_group_with_a_pinned_member_stays_and_a_group_pins_its_members()
    {
        var f = F();
        var obj = "o one\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\no two\nv 5 0 0\nv 6 0 0\nv 5 1 0\nf 4 5 6\n";
        MeshImport.Run(f.Session, Encoding.UTF8.GetBytes(obj), "pieces.obj", new JsonObject { ["up"] = "z" }, null, out _);
        var group = Assert.Single(Groups.All(f.Model)).Id;
        var members = Groups.MembersOf(f.Model, group).ToArray();
        Pin(f, true, group);
        Assert.All(members, id => Assert.True(Pins.Is(f.Model.Get(id))));
        Assert.Throws<CommandException>(() => f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "move", ["by"] = new JsonArray(1, 0) }));
        Pin(f, false, members[1]);
        Assert.Throws<CommandException>(() => f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "move", ["by"] = new JsonArray(1, 0) }));
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(members[1]), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        // Undone in one step, as any command.
        f.Session.Undo();
        f.Session.Undo();
        Assert.True(Pins.Is(f.Model.Get(members[1])));
    }

    [Fact]
    public void A_pinned_drafted_line_stays_on_its_view_and_its_copy_is_free()
    {
        var f = F();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(5, 0) } })!["ids"]![0]!;
        Pin(f, true, line);
        Assert.Throws<CommandException>(() => f.Run("drafting.transform", new JsonObject { ["ids"] = Ids(line), ["op"] = "move", ["by"] = new JsonArray(1, 0) }));
        var copy = (string)f.Run("drafting.transform", new JsonObject { ["ids"] = Ids(line), ["op"] = "copy", ["by"] = new JsonArray(0, 1) })!["ids"]![0]!;
        Assert.False(Pins.Is(f.Model.Get(copy)));
    }
}
