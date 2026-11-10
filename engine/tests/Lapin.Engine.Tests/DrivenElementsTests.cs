using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The hooks for modules that drive elements from a specification of their own (docs/overrides.md, Driven elements):
/// <see cref="IDrivenElements"/> asked before core's generic commands act on such elements and told after every command,
/// and <see cref="IIfcExportFilter"/> keeping a module's groups and keys out of IFC. A tiny generator stands for such a
/// module: it groups walls under a spec of its own that holds each one's height.
/// </summary>
public class DrivenElementsTests
{
    /// <summary>A test generator: <c>gen.make {ids}</c> groups walls as its own, each with a role, their heights kept in a spec entity.</summary>
    sealed class Generator : ILapinModule, IDrivenElements, IIfcExportFilter
    {
        public const string SpecClass = "TestGenSpec", RoleKey = "genRole", OwnKey = "generator";
        public readonly List<string> Asked = [];

        public string Id => "gen";
        public string Name => "Generator";

        public void Register(IModuleRegistry r)
        {
            r.Class(new(SpecClass, "Generator spec", [], "#888888", Kind: "resource"));
            r.Service<IDrivenElements>(this);
            r.Service<IIfcExportFilter>(this);
            r.Command("make", "Generate", (ctx, a) =>
            {
                var ids = Args.Ids(a);
                var heights = new JsonObject();
                for (var i = 0; i < ids.Count; i++)
                {
                    var e = ctx.Model.Get(ids[i])!;
                    var attrs = e.CloneAttrs();
                    attrs[RoleKey] = $"r{i}";
                    ctx.Replace(e.With(attrs: attrs));
                    heights[$"r{i}"] = e.Num("height");
                }
                var spec = new Entity(ctx.NewId(), SpecClass, "Spec", new JsonObject { ["heights"] = heights });
                ctx.Add(spec);
                var group = Groups.Make(ctx, "Generated", ids)!;
                var g = group.CloneAttrs();
                g[OwnKey] = spec.Id;
                ctx.Replace(group.With(attrs: g));
                return new JsonObject { ["id"] = group.Id, ["spec"] = spec.Id };
            });
        }

        static string? Role(Entity e) => e.Str(RoleKey);
        static bool Own(Entity e) => e.Class == Groups.Class && e.Str(OwnKey) != null;

        public bool Drives(IModelView m, Entity e) => Role(e) != null || Own(e);

        static Entity SpecOf(IModelView m, string memberOrGroup)
        {
            var g = m.Get(memberOrGroup) is { } x && Own(x) ? x : Groups.Of(m, memberOrGroup)!;
            return m.Get(g.Str(OwnKey)!)!;
        }

        static void SetHeights(ICommandContext ctx, Entity spec, Action<JsonObject> change)
        {
            var attrs = spec.CloneAttrs();
            change((JsonObject)attrs["heights"]!);
            ctx.Replace(spec.With(attrs: attrs));
        }

        public void Before(ICommandContext ctx, DrivenEdit edit, DrivenOutcome outcome)
        {
            var m = ctx.Model;
            Asked.Add(edit.Command);
            var entities = edit.Ids.Select(id => m.Get(id)!).ToList();
            switch (edit.Command)
            {
                case "element.delete":
                    // A member deleted leaves the spec; the group goes with all of its.
                    foreach (var e in entities)
                    {
                        if (m.Get(e.Id) == null) continue;
                        var spec = SpecOf(m, e.Id);
                        var members = Own(e) ? Groups.MembersOf(m, e.Id).ToList() : [e.Id];
                        SetHeights(ctx, spec, h => { foreach (var id in members) h.Remove(Role(m.Get(id)!)!); });
                        foreach (var id in members) { ctx.Remove(id); outcome.Handled.Add(id); }
                        if (Own(e)) { ctx.Remove(e.Id); ctx.Remove(spec.Id); }
                        outcome.Handled.Add(e.Id);
                    }
                    break;
                case "element.transform":
                    if (entities.FirstOrDefault(e => !Own(e)) is { } member && !entities.Any(e => Own(e) && Groups.Of(m, member.Id)?.Id == e.Id))
                        throw new CommandException($"„{member.Name}” moves with its generator.");
                    foreach (var g in entities.Where(Own))
                    {
                        var members = Groups.MembersOf(m, g.Id).ToList();
                        if (edit.Copy)
                        {
                            // A copy is a generated group of its own, its spec copied.
                            var spec = SpecOf(m, g.Id);
                            var twinSpec = new Entity(ctx.NewId(), SpecClass, "Spec", spec.CloneAttrs());
                            ctx.Add(twinSpec);
                            var copies = members.Select(id => Placement.Copy(ctx, m.Get(id)!, edit.Transform!)).ToList();
                            var twin = Groups.Make(ctx, g.Name, copies)!;
                            var ta = twin.CloneAttrs();
                            ta[OwnKey] = twinSpec.Id;
                            ctx.Replace(twin.With(attrs: ta));
                            outcome.Made.Add(twin.Id);
                        }
                        else
                        {
                            var moving = members.ToHashSet();
                            foreach (var id in members) Placement.Transform(ctx, m.Get(id)!, edit.Transform!, moving);
                            var ga = m.Get(g.Id)!.CloneAttrs();
                            var at = edit.Transform!.Point(Args.PointOf(ga["at"] ?? new JsonArray(0, 0)));
                            ga["at"] = new JsonArray(at.X, at.Y);
                            ctx.Replace(m.Get(g.Id)!.With(attrs: ga));
                        }
                        outcome.Handled.Add(g.Id);
                        outcome.Handled.UnionWith(members);
                    }
                    break;
                case "element.update":
                    if (edit.Args["attrs"] is JsonObject values && values.ContainsKey("start"))
                        throw new CommandException("A generated wall's ends are its generator's.");
                    break;
                case "element.rename":
                    foreach (var id in edit.Ids) outcome.Handled.Add(id);  // its generator names it
                    break;
                case "group.ungroup":
                    // Let go: the members become plain walls, the spec goes.
                    foreach (var g in entities.Where(Own))
                    {
                        foreach (var id in Groups.MembersOf(m, g.Id).ToList())
                        {
                            var attrs = m.Get(id)!.CloneAttrs();
                            attrs.Remove(RoleKey);
                            ctx.Replace(m.Get(id)!.With(attrs: attrs));
                        }
                        var spec = SpecOf(m, g.Id);
                        ctx.Remove(g.Id);
                        ctx.Remove(spec.Id);
                        outcome.Handled.Add(g.Id);
                    }
                    break;
                default:
                    throw new CommandException($"„{entities[0].Name}” belongs to a generator ({edit.Command}).");
            }
        }

        /// <summary>A member's height changed by any command: written into the spec.</summary>
        public void After(ICommandContext ctx, IEdit edit, string command)
        {
            foreach (var c in edit.EntityChanges)
                if (c.After is { } e && Role(e) is { } role && Groups.Of(ctx.Model, e.Id) != null)
                {
                    var spec = SpecOf(ctx.Model, e.Id);
                    if (Json.Number(spec.Attrs["heights"]![role]) is { } h && Math.Abs(h - e.Num("height")) > 1e-9)
                        SetHeights(ctx, spec, x => x[role] = e.Num("height"));
                }
        }

        public bool SkipsGroup(IModelView model, Entity group) => Own(group);
        public IEnumerable<string> PrivateKeys => [RoleKey];
    }

    static JsonArray Ids(params string[] ids) => new([.. ids.Select(x => (JsonNode)x)]);

    static (Fixture F, Generator G, string Group, string Spec, string A, string B) Made()
    {
        var g = new Generator();
        var f = new Fixture(new ArchitectureModule(), g);
        var a = f.Wall(0, 0, 4, 0);
        var b = f.Wall(4, 0, 4, 3);
        var r = f.Run("gen.make", new JsonObject { ["ids"] = Ids(a, b) })!;
        return (f, g, (string)r["id"]!, (string)r["spec"]!, a, b);
    }

    static JsonObject Heights(Fixture f, string spec) => f.Model.Get(spec)!.Attrs["heights"]!.AsObject();

    [Fact]
    public void A_driven_element_deleted_is_its_modules_to_delete_and_the_rest_is_deleted_as_before()
    {
        var (f, g, group, spec, a, b) = Made();
        var plain = f.Wall(0, 5, 4, 5);
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(a, plain) });
        Assert.Contains("element.delete", g.Asked);
        Assert.Null(f.Model.Get(a));
        Assert.Null(f.Model.Get(plain));
        Assert.False(Heights(f, spec).ContainsKey("r0"));
        Assert.True(Heights(f, spec).ContainsKey("r1"));
        // One undo step brings both back, the spec with them.
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(a));
        Assert.NotNull(f.Model.Get(plain));
        Assert.True(Heights(f, spec).ContainsKey("r0"));
        // The group deleted: its members and spec with it.
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(group) });
        Assert.Null(f.Model.Get(group));
        Assert.Null(f.Model.Get(spec));
        Assert.Null(f.Model.Get(b));
    }

    [Fact]
    public void A_member_moved_alone_is_refused_and_the_whole_group_is_moved_or_copied_by_its_module()
    {
        var (f, _, group, _, a, b) = Made();
        var ex = Assert.Throws<CommandException>(() => f.Run("element.transform", new JsonObject { ["ids"] = Ids(a), ["op"] = "move", ["by"] = new JsonArray(1, 0) }));
        Assert.Contains("moves with its generator", ex.Message);
        Assert.Equal(0, Walls.Start(f.Model.Get(a)!).X, 9);

        f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "move", ["by"] = new JsonArray(1, 2) });
        Assert.Equal(new Vec2(1, 2), Walls.Start(f.Model.Get(a)!));
        Assert.Equal(new Vec2(5, 2), Walls.Start(f.Model.Get(b)!));
        Assert.Equal(1, Args.PointOf(f.Model.Get(group)!.Attrs["at"]).X, 9);

        // A copy (and an array, made of copies) is the module's: its own group with a spec of its own, returned as the command's.
        var made = f.Run("element.array", new JsonObject { ["ids"] = Ids(group), ["count"] = 3, ["step"] = new JsonArray(0, 10, 0) })!["ids"]!.AsArray();
        Assert.Equal(2, made.Count);
        Assert.All(made, id => Assert.NotNull(f.Model.Get((string)id!)!.Str(Generator.OwnKey)));
        Assert.Equal(3, f.Model.OfClass(Generator.SpecClass).Count());
        Assert.Equal(6, f.Model.OfClass(IfcClass.Wall).Count());
    }

    [Fact]
    public void Property_edits_may_be_refused_and_what_any_command_changes_is_written_back_in_the_same_step()
    {
        var (f, _, _, spec, a, _) = Made();
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = a, ["attrs"] = new JsonObject { ["start"] = new JsonArray(1, 0) } }));
        f.Run("element.update", new JsonObject { ["id"] = a, ["attrs"] = new JsonObject { ["height"] = 3.5 } });
        Assert.Equal(3.5, (double)Heights(f, spec)["r0"]!, 9);
        // Another module's command (architecture's) is seen too.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = a, ["height"] = 4.0 });
        Assert.Equal(4.0, f.Model.Get(a)!.Num("height"), 9);
        Assert.Equal(4.0, (double)Heights(f, spec)["r0"]!, 9);
        f.Session.Undo();
        Assert.Equal(3.5, (double)Heights(f, spec)["r0"]!, 9);
        // Renamed: the module took care of it (here, by keeping its own name).
        var name = f.Model.Get(a)!.Name;
        f.Run("element.rename", new JsonObject { ["id"] = a, ["name"] = "Mine" });
        Assert.Equal(name, f.Model.Get(a)!.Name);
    }

    [Fact]
    public void Group_and_component_commands_on_driven_elements_are_the_modules_and_ungroup_lets_them_go()
    {
        var (f, _, group, spec, a, b) = Made();
        var plain = f.Wall(0, 5, 4, 5);
        var other = (string)f.Run("group.create", new JsonObject { ["ids"] = Ids(plain) })!["id"]!;
        foreach (var (cmd, args) in new[]
        {
            ("group.create", new JsonObject { ["ids"] = Ids(a, plain) }),
            ("group.remove", new JsonObject { ["ids"] = Ids(a) }),
            ("group.add", new JsonObject { ["id"] = other, ["ids"] = Ids(b) }),
            ("component.make", new JsonObject { ["ids"] = Ids(group) }),
        })
        {
            var ex = Assert.Throws<CommandException>(() => f.Run(cmd, args));
            Assert.Contains("belongs to a generator", ex.Message);
        }
        Assert.Equal(group, Groups.Of(f.Model, a)?.Id);

        f.Run("group.ungroup", new JsonObject { ["ids"] = Ids(group) });
        Assert.Null(f.Model.Get(group));
        Assert.Null(f.Model.Get(spec));
        Assert.Null(f.Model.Get(a)!.Str(Generator.RoleKey));
        // Plain now: the generic commands take them.
        f.Run("group.add", new JsonObject { ["id"] = other, ["ids"] = Ids(a) });
        Assert.Equal(other, Groups.Of(f.Model, a)?.Id);
    }

    [Fact]
    public void Without_a_module_that_drives_them_the_commands_are_as_they_were()
    {
        var f = new Fixture(new ArchitectureModule());
        var a = f.Wall(0, 0, 4, 0);
        var g = (string)f.Run("group.create", new JsonObject { ["ids"] = Ids(a) })!["id"]!;
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(g), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        Assert.Equal(1, Walls.Start(f.Model.Get(a)!).X, 9);
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(g) });
        Assert.Empty(f.Model.OfClass(IfcClass.Wall));
    }

    [Fact]
    public void A_modules_own_groups_and_keys_stay_out_of_the_IFC_file()
    {
        var (f, _, _, _, _, _) = Made();
        var plain = f.Wall(0, 5, 4, 5);
        f.Run("group.create", new JsonObject { ["ids"] = Ids(plain), ["name"] = "Mine" });
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "gen.ifc", new IfcExportOptions());
        var text = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(3, text.Split('\n').Count(l => l.Contains("=IFCWALL(") || l.Contains("=IFCWALLSTANDARDCASE(")));
        // The user's group is written, the generator's is not; its key on the walls is not either.
        Assert.Single(text.Split('\n'), l => l.Contains("=IFCGROUP("));
        Assert.Contains("'Mine'", text);
        Assert.DoesNotContain("Generated", text);
        Assert.DoesNotContain(Generator.RoleKey, text);
    }
}
