using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// A project holding a module's content opened in an edition without that module (docs/overrides.md, Content of an
/// absent module): its own class and its group are kept as they are and written back as they came, the group's members
/// stay ordinary elements, the group is left out of IFC, and the interface is told which modules are missing.
/// </summary>
public class AbsentModuleTests
{
    /// <summary>A module that groups walls under a spec of its own class, the group marked as its own.</summary>
    sealed class Owner : ILapinModule
    {
        public const string SpecClass = "TestOwnerSpec";
        public string Id => "owner";
        public string Name => "Owner";

        public void Register(IModuleRegistry r)
        {
            r.Class(new(SpecClass, "Owner spec", [], "#888888", Kind: "resource"));
            r.Command("make", "Own", (ctx, a) =>
            {
                var spec = new Entity(ctx.NewId(), SpecClass, "Spec", new JsonObject { ["walls"] = a["ids"]!.DeepClone(), ["n"] = 2 });
                ctx.Add(spec);
                var group = Groups.Make(ctx, "Owned", Args.Ids(a))!;
                var g = group.CloneAttrs();
                g[ModuleOwned.Key] = Id;
                g["spec"] = spec.Id;
                g["members"] = a["ids"]!.DeepClone();
                ctx.Replace(group.With(attrs: g));
                return new JsonObject { ["id"] = group.Id, ["spec"] = spec.Id };
            });
        }
    }

    static JsonArray Ids(params string[] ids) => new([.. ids.Select(x => (JsonNode)x)]);

    static byte[] Save(Session s)
    {
        using var ms = new MemoryStream();
        ProjectPackage.Write(s, ms);
        return ms.ToArray();
    }

    static Fixture Open(byte[] bytes, params ILapinModule[] modules)
    {
        var f = new Fixture(modules);
        var content = LapinPackage.Read(new MemoryStream(bytes));
        f.Session.Load(content.Entities, content.Relations, null, content.Classes);
        return f;
    }

    static (byte[] Bytes, string Group, string Spec, string A, string B, string C) Saved()
    {
        var f = new Fixture(new ArchitectureModule(), new Owner());
        var a = f.Wall(0, 0, 4, 0);
        var b = f.Wall(4, 0, 4, 3);
        var c = f.Wall(4, 3, 0, 3);
        var r = f.Run("owner.make", new JsonObject { ["ids"] = Ids(a, b, c) })!;
        Assert.Empty(f.Session.AbsentModules());
        return (Save(f.Session), (string)r["id"]!, (string)r["spec"]!, a, b, c);
    }

    [Fact]
    public void Without_its_module_a_modules_content_is_kept_and_its_members_are_ordinary_elements()
    {
        var (bytes, group, spec, a, b, c) = Saved();
        var f = Open(bytes, new ArchitectureModule());
        // The interface is told once which modules the project needs.
        Assert.Equal(["owner"], f.Session.AbsentModules());
        var groupBefore = f.Model.Get(group)!.Attrs.ToJsonString();
        var specBefore = f.Model.Get(spec)!.Attrs.ToJsonString();

        // Its members are edited as any element: moved, changed, deleted, a door put in one.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(a), ["op"] = "move", ["by"] = new JsonArray(0, -1) });
        f.Run("architecture.updateWall", new JsonObject { ["id"] = b, ["height"] = 3.4 });
        f.Run("element.rename", new JsonObject { ["id"] = b, ["name"] = "Mine" });
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(c) });
        Assert.Null(f.Model.Get(c));
        Assert.Equal(3.4, f.Model.Get(b)!.Num("height"), 9);
        Assert.Equal("Mine", f.Model.Get(b)!.Name);

        // The group itself is not changed, let go, filled or emptied by hand.
        foreach (var (cmd, args) in new (string, JsonObject)[]
        {
            ("group.ungroup", new JsonObject { ["ids"] = Ids(group) }),
            ("group.remove", new JsonObject { ["ids"] = Ids(a) }),
            ("group.create", new JsonObject { ["ids"] = Ids(a) }),
            ("element.rename", new JsonObject { ["id"] = group, ["name"] = "X" }),
            ("element.update", new JsonObject { ["id"] = group, ["attrs"] = new JsonObject { ["spec"] = "x" } }),
        })
        {
            var ex = Assert.Throws<CommandException>(() => f.Run(cmd, args));
            Assert.Contains("„owner”, which this edition does not have", ex.Message);
        }
        var plain = f.Wall(0, 6, 4, 6);
        var mine = (string)f.Run("group.create", new JsonObject { ["ids"] = Ids(plain) })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("group.add", new JsonObject { ["id"] = group, ["ids"] = Ids(plain) }));
        Assert.Throws<CommandException>(() => f.Run("group.add", new JsonObject { ["id"] = mine, ["ids"] = Ids(a) }));

        // Copied, its members make a plain group of plain copies.
        var copies = f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "move", ["by"] = new JsonArray(20, 0), ["copy"] = true })!;
        var twin = Groups.All(f.Model).Single(g => g.Id != group && g.Id != mine);
        Assert.Null(ModuleOwned.Of(twin));
        Assert.Equal(2, Groups.MembersOf(f.Model, twin.Id).Count());

        // Every member deleted: the group stays (it is the module's, and so is what it says of them).
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(group) });
        Assert.Null(f.Model.Get(a));
        Assert.Null(f.Model.Get(b));
        Assert.NotNull(f.Model.Get(group));
        Assert.Equal(groupBefore, f.Model.Get(group)!.Attrs.ToJsonString());
        Assert.Equal(specBefore, f.Model.Get(spec)!.Attrs.ToJsonString());
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(a));

        // Saved and opened again with the module: its class still its, its entities as they came, the members as edited.
        var back = Open(Save(f.Session), new ArchitectureModule(), new Owner());
        Assert.Empty(back.Session.AbsentModules());
        Assert.Equal(groupBefore, back.Model.Get(group)!.Attrs.ToJsonString());
        Assert.Equal(specBefore, back.Model.Get(spec)!.Attrs.ToJsonString());
        Assert.Equal("Mine", back.Model.Get(b)!.Name);
        Assert.Equal(-1, back.Model.Get(a)!.Attrs["start"]![1]!.GetValue<double>(), 9);
        Assert.Equal(new[] { a, b }.Order(StringComparer.Ordinal), Groups.MembersOf(back.Model, group).Order(StringComparer.Ordinal));
        Assert.Equal("owner", LapinPackage.Read(new MemoryStream(Save(back.Session))).Classes[Owner.SpecClass]);
    }

    [Fact]
    public void A_project_saved_without_the_module_writes_its_class_back_under_its_module()
    {
        var (bytes, _, spec, _, _, _) = Saved();
        var f = Open(bytes, new ArchitectureModule());
        var again = LapinPackage.Read(new MemoryStream(Save(f.Session)));
        Assert.Equal("owner", again.Classes[Owner.SpecClass]);
        Assert.Contains(again.Entities, e => e.Id == spec && e.Class == Owner.SpecClass);
    }

    [Fact]
    public void Without_its_module_a_modules_group_stays_out_of_the_IFC_file()
    {
        var (bytes, group, _, a, _, _) = Saved();
        foreach (var withModule in new[] { false, true })
        {
            var f = withModule ? Open(bytes, new ArchitectureModule(), new Owner()) : Open(bytes, new ArchitectureModule());
            var plain = f.Wall(0, 6, 4, 6);
            f.Run("group.create", new JsonObject { ["ids"] = Ids(plain), ["name"] = "Mine" });
            using var ms = new MemoryStream();
            IfcExport.Write(f.Session, ms, "absent.ifc", new IfcExportOptions());
            var text = Encoding.UTF8.GetString(ms.ToArray());
            Assert.Equal(4, text.Split('\n').Count(l => l.Contains("=IFCWALL(") || l.Contains("=IFCWALLSTANDARDCASE(")));
            Assert.Contains($"'{a}'", text);
            // Without the module its group is left out (with it, the module would say; this one says nothing, so it is written).
            Assert.Equal(withModule ? 2 : 1, text.Split('\n').Count(l => l.Contains("=IFCGROUP(")));
            Assert.DoesNotContain(ModuleOwned.Key, text);
            if (!withModule) Assert.DoesNotContain("'Owned'", text);
        }
    }
}
