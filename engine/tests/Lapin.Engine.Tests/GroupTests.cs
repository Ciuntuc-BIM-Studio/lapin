using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Mesh;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Groups (docs/groups.md): IfcGroup and IfcRelAssignsToGroup; selected, a group is its members (deleted, moved,
/// copied into a group of its own); an imported file comes in as one; a group with no members is not kept.
/// </summary>
public class GroupTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-groups").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Id(JsonNode? r) => (string)r!["id"]!;
    static JsonArray Ids(params string[] ids) => [.. ids.Select(x => (JsonNode)x)];

    [Fact]
    public void A_group_holds_its_members_and_selected_it_is_them()
    {
        var f = F();
        var (a, b, c) = (f.Wall(0, 0, 4, 0), f.Wall(4, 0, 4, 3), f.Wall(10, 0, 14, 0));
        var g = Id(f.Run("group.create", new JsonObject { ["ids"] = Ids(a, b) }));
        Assert.Equal("Group 1", f.Model.Get(g)!.Name);
        Assert.Equal(new[] { a, b }.Order(), Groups.MembersOf(f.Model, g).Order());
        Assert.Equal(g, Groups.Of(f.Model, a)!.Id);
        Assert.Null(Groups.Of(f.Model, c));
        // A wall is in one group: grouped again, it leaves the first (one emptied goes).
        var g2 = Id(f.Run("group.create", new JsonObject { ["ids"] = Ids(a, b, c), ["name"] = "Block" }));
        Assert.Null(f.Model.Get(g));
        Assert.Equal(3, Groups.MembersOf(f.Model, g2).Count());
        // Moved as one.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(g2), ["op"] = "move", ["by"] = new JsonArray(0, 5) });
        Assert.Equal(5, Joins.Start(f.Model.Get(a)!).Y, 9);
        Assert.Equal(5, Joins.Start(f.Model.Get(c)!).Y, 9);
        // Copied: three new walls in a group of their own, with its name.
        var made = f.Run("element.transform", new JsonObject { ["ids"] = Ids(g2), ["op"] = "copy", ["by"] = new JsonArray(0, 10) })!["ids"]!.AsArray();
        var twin = made.Select(x => (string)x!).Select(f.Model.Get).Single(e => e!.Class == Groups.Class)!;
        Assert.Equal("Block", twin.Name);
        Assert.Equal(3, Groups.MembersOf(f.Model, twin.Id).Count());
        Assert.Equal(6, f.Model.OfClass(IfcClass.Wall).Count());
        // Deleted: the group and its members, one undo step back.
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(twin.Id) });
        Assert.Null(f.Model.Get(twin.Id));
        Assert.Equal(3, f.Model.OfClass(IfcClass.Wall).Count());
        f.Session.Undo();
        Assert.Equal(6, f.Model.OfClass(IfcClass.Wall).Count());
    }

    [Fact]
    public void Members_leave_and_a_group_left_empty_goes()
    {
        var f = F();
        var (a, b) = (f.Wall(0, 0, 4, 0), f.Wall(4, 0, 4, 3));
        var g = Id(f.Run("group.create", new JsonObject { ["ids"] = Ids(a, b) }));
        f.Run("group.remove", new JsonObject { ["ids"] = Ids(a) });
        Assert.Null(Groups.Of(f.Model, a));
        Assert.Equal([b], Groups.MembersOf(f.Model, g));
        f.Run("group.add", new JsonObject { ["id"] = g, ["ids"] = Ids(a) });
        Assert.Equal(2, Groups.MembersOf(f.Model, g).Count());
        // A member deleted on its own leaves the group; the last one takes it.
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(a) });
        Assert.NotNull(f.Model.Get(g));
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(b) });
        Assert.Null(f.Model.Get(g));
        // Ungrouped: the members stay.
        var (c, d) = (f.Wall(0, 0, 4, 0), f.Wall(4, 0, 4, 3));
        var g2 = Id(f.Run("group.create", new JsonObject { ["ids"] = Ids(c, d) }));
        f.Run("group.ungroup", new JsonObject { ["ids"] = Ids(g2) });
        Assert.Null(f.Model.Get(g2));
        Assert.NotNull(f.Model.Get(c));
        Assert.Throws<CommandException>(() => f.Run("group.create", new JsonObject { ["ids"] = Ids(f.Storey) }));
    }

    [Fact]
    public void An_imported_file_comes_in_as_one_group_and_groups_go_out_in_IFC()
    {
        var f = F();
        var (a, b) = (f.Wall(0, 0, 4, 0), f.Wall(4, 0, 4, 3));
        f.Run("group.create", new JsonObject { ["ids"] = Ids(a, b), ["name"] = "Core" });
        var path = Path.Combine(_dir, "house.ifc");
        IfcExport.Write(f.Session, path);
        var text = File.ReadAllText(path);
        Assert.Contains("IFCGROUP(", text);
        Assert.Contains("'Core'", text);
        Assert.Contains("IFCRELASSIGNSTOGROUP(", text);

        var g = F();
        g.Session.New(plain: true);
        IfcImport.Run(g.Session, path, new IfcImportOptions(Group: true), out _);
        var group = Assert.Single(Groups.All(g.Model));
        Assert.Equal("house", group.Name);
        Assert.Equal(2, Groups.MembersOf(g.Model, group.Id).Count());
        Assert.All(Groups.MembersOf(g.Model, group.Id), id => Assert.Equal(IfcClass.Wall, g.Model.Get(id)!.Class));
        // Without the option (a link, an import the journal recorded before groups): none.
        var h = F();
        h.Session.New(plain: true);
        IfcImport.Run(h.Session, path, null, out _);
        Assert.Empty(Groups.All(h.Model));
    }

    [Fact]
    public void A_mesh_file_comes_in_as_one_group()
    {
        var f = F();
        var obj = "o one\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\no two\nv 5 0 0\nv 6 0 0\nv 5 1 0\nf 4 5 6\n";
        MeshImport.Run(f.Session, Encoding.UTF8.GetBytes(obj), "pieces.obj", new JsonObject(), null, out _);
        var group = Assert.Single(Groups.All(f.Model));
        Assert.Equal("pieces", group.Name);
        Assert.Equal(2, Groups.MembersOf(f.Model, group.Id).Count());
    }
}
