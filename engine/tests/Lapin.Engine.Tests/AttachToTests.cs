using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;
using Xunit;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage A1 of docs/plan-attach-to.md: the platform's sampler (<see cref="BodySampler"/>), the contract
/// (<see cref="IAttachable"/>) and the commands <c>element.attachTo</c>, <c>detach</c>, <c>attachments</c> and
/// <c>attachPreview</c>, with walls as their first class. Walls keep every number of <c>RoofTests</c> (those tests are
/// unchanged); the classes after walls (columns, A2; Studio solids, A5) are stood in for by a probe of this file's own
/// (<see cref="ProbeModule"/>): a box whose top goes to the lowest underside over its centre, stored as relations.
/// </summary>
public class AttachToTests
{
    static JsonArray P(double x, double y) => new(x, y);
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static IReadOnlyList<string> Problems(Fixture f, string id) => f.Session.Geometry(id)!.Diagnostics ?? [];
    static string Attrs(Fixture f, string id) => f.Model.Get(id)!.Attrs.ToJsonString();
    static JsonObject Q(Fixture f, string name, JsonObject a) => (JsonObject)f.Session.Query(name, a)!;

    /// <summary>The scene of RoofTests' body test: a Studio loft from a 2 × 1 square at +3.00 to the same square 8 m on at +6.00, and a 13 m wall along it.</summary>
    static (Fixture F, string Body, string Wall, string Upper) Loft()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule(), new StudioModule());
        var upper = (string)f.Run("storey.create", [])!["id"]!;            // +3.00
        var top = (string)f.Run("storey.create", [])!["id"]!;              // +6.00
        string Plan(string storey) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey })!["id"]!;
        string Draw(string view, double x0, double x1) => (string)f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = view,
            ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(P(x0, 0), P(x1, 0), P(x1, 1), P(x0, 1)), ["closed"] = true },
        })!["ids"]![0]!;
        var a = Draw(Plan(upper), 0, 2);
        var b = Draw(Plan(top), 8, 10);
        var body = (string)f.Run("studio.loft", new JsonObject { ["profileId"] = a, ["profileIds"] = new JsonArray(a, b) })!["id"]!;
        return (f, body, f.Wall(-1, 0.5, 12, 0.5), upper);
    }

    static JsonObject AttachTop(string id, params string[] targets) =>
        new() { ["ids"] = new JsonArray(id), ["end"] = "top", ["targets"] = new JsonArray([.. targets.Select(t => (JsonNode)t)]) };

    // ------------------------------------------------------------ walls: the same as attachToRoof

    [Fact]
    public void AttachTo_on_a_wall_writes_what_attachToRoof_writes_and_builds_the_same_body()
    {
        var (f, body, wall, _) = Loft();
        var plain = Vol(f, wall);
        f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(wall), ["roofIds"] = new JsonArray(body) });
        var (attrs, volume, bounds) = (Attrs(f, wall), Vol(f, wall), f.Bounds(wall));
        var vertices = f.Session.Geometry(wall)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        // RoofTests' numbers: up to 6 under the body's end, the slope's triangle on the plain wall's 13 · 3.
        Assert.Equal(6, bounds.Max.Z, 2);
        Assert.Equal(plain * (39 + 12) / 39, volume, 1);
        f.Session.Undo();
        Assert.Equal(3, f.Bounds(wall).Max.Z, 6);

        f.Run("element.attachTo", AttachTop(wall, body));
        Assert.Equal(attrs, Attrs(f, wall));
        Assert.Equal(volume, Vol(f, wall), 9);
        Assert.Equal(bounds, f.Bounds(wall));
        Assert.Equal(vertices, f.Session.Geometry(wall)!.Parts.SelectMany(p => p.Solid.Vertices).ToList());
    }

    [Fact]
    public void AttachTo_a_gable_roof_is_attachToRoof_to_it()
    {
        var f = new Fixture();
        var attic = (string)f.Run("storey.create", new JsonObject { ["name"] = "Attic" })!["id"]!;   // +3.00
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = attic, ["points"] = new JsonArray(P(0, 0), P(10, 0), P(10, 6), P(0, 6)) })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["shape"] = "gable", ["ridgeDirection"] = "x" } });
        var a = f.Wall(1, 3, 9, 3);
        var b = f.Wall(1, 3, 9, 3);
        f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(a), ["roofIds"] = new JsonArray(roof) });
        f.Run("element.attachTo", AttachTop(b, roof));
        Assert.Equal(f.Model.Get(a)!.Attrs["roofIds"]!.ToJsonString(), f.Model.Get(b)!.Attrs["roofIds"]!.ToJsonString());
        Assert.Equal("attach", f.Model.Get(b)!.Str("roofTop"));
        Assert.True(f.Bounds(a).Max.Z > 6);
        Assert.Equal(f.Bounds(a).Max.Z, f.Bounds(b).Max.Z, 9);
        Assert.Equal(Vol(f, a), Vol(f, b), 9);
    }

    [Fact]
    public void A_level_is_the_walls_own_top_as_setWallTop_sets_it()
    {
        var (f, _, wall, upper) = Loft();
        var other = f.Wall(-1, 3, 12, 3);
        f.Run("element.attachTo", new JsonObject { ["ids"] = new JsonArray(wall), ["end"] = "top", ["level"] = new JsonObject { ["storey"] = upper, ["offset"] = -0.2 } });
        f.Run("architecture.setWallTop", new JsonObject { ["ids"] = new JsonArray(other), ["top"] = upper, ["topOffset"] = -0.2 });
        foreach (var key in new[] { "top", "topOffset", "height" })
            Assert.Equal(f.Model.Get(other)!.Attrs[key]!.ToJsonString(), f.Model.Get(wall)!.Attrs[key]!.ToJsonString());
        Assert.Equal(2.8, f.Bounds(wall).Max.Z, 6);
        Assert.Throws<CommandException>(() => f.Run("element.attachTo", new JsonObject { ["ids"] = new JsonArray(wall), ["level"] = new JsonObject { ["storey"] = "below" } }));
    }

    // ------------------------------------------------------------ refusals

    [Fact]
    public void What_has_no_such_end_is_refused_by_name_and_the_whole_command_with_it()
    {
        var (f, body, wall, _) = Loft();
        var host = f.Wall(0, 5, 6, 5);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = host, ["along"] = 1.5 })!["id"]!;
        var doorName = f.Model.Get(door)!.Name;
        var before = Attrs(f, wall);
        var ex = Assert.Throws<CommandException>(() => f.Run("element.attachTo", new JsonObject { ["ids"] = new JsonArray(wall, door), ["end"] = "top", ["targets"] = new JsonArray(body) }));
        Assert.Equal($"„{doorName}” has no top to attach.", ex.Message);
        Assert.Equal(before, Attrs(f, wall));
        // Walls have no bottom to attach yet (A8).
        ex = Assert.Throws<CommandException>(() => f.Run("element.attachTo", new JsonObject { ["ids"] = new JsonArray(wall), ["end"] = "bottom", ["targets"] = new JsonArray(body) }));
        Assert.Contains("has no bottom to attach", ex.Message);
        // A wall to a wall: today's message.
        ex = Assert.Throws<CommandException>(() => f.Run("element.attachTo", AttachTop(wall, host)));
        Assert.Equal("A wall rises to a roof or to a body over it, not to another wall.", ex.Message);
        Assert.Equal(ex.Message, Assert.Throws<CommandException>(() => f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(wall), ["roofIds"] = new JsonArray(host) })).Message);
        // Itself, nothing, a gap or a mode walls cannot keep, a target that is not there.
        Assert.Contains("itself", Assert.Throws<CommandException>(() => f.Run("element.attachTo", AttachTop(wall, wall))).Message);
        Assert.Throws<CommandException>(() => f.Run("element.attachTo", new JsonObject { ["ids"] = new JsonArray(wall), ["end"] = "top" }));
        var gap = AttachTop(wall, body); gap["offset"] = 0.1;
        Assert.Contains("no gap", Assert.Throws<CommandException>(() => f.Run("element.attachTo", gap)).Message);
        var trim = AttachTop(wall, body); trim["mode"] = "trim";
        Assert.Throws<CommandException>(() => f.Run("element.attachTo", trim));
        Assert.Throws<CommandException>(() => f.Run("element.attachTo", AttachTop(wall, "no-such-id")));
        Assert.Equal(before, Attrs(f, wall));
    }

    // ------------------------------------------------------------ circles

    [Fact]
    public void A_circle_of_attachments_is_refused_naming_both()
    {
        var f = new Fixture(new ArchitectureModule(), new ProbeModule());
        var probe = Probe(f, 2, 0.5, 4, 0.5);      // a box 4 to 4.5 over the wall
        var wall = f.Wall(-1, 0.5, 6, 0.5);
        f.Run("element.attachTo", AttachTop(wall, probe));
        Assert.Equal(4, f.Bounds(wall).Max.Z, 6);
        var (wn, pn) = (f.Model.Get(wall)!.Name, f.Model.Get(probe)!.Name);
        var ex = Assert.Throws<CommandException>(() => f.Run("element.attachTo", AttachTop(probe, wall)));
        Assert.Contains($"„{pn}”", ex.Message);
        Assert.Contains($"„{wn}”", ex.Message);
        Assert.Equal($"„{pn}” cannot be attached to „{wn}”: „{wn}” is attached to „{pn}”.", ex.Message);
        // Through a third: the path is named.
        var middle = Probe(f, 2, 0.5, 5, 0.5);
        f.Run("element.attachTo", AttachTop(probe, middle));
        ex = Assert.Throws<CommandException>(() => f.Run("element.attachTo", AttachTop(middle, wall)));
        Assert.Contains("which is attached to", ex.Message);
        // In one command, the second closing the circle the first opened: refused whole.
        var c = Probe(f, 8, 0, 4, 0.5);
        var d = Probe(f, 8, 0, 6, 0.5);
        Assert.Throws<CommandException>(() => f.Run("element.attachTo", new JsonObject { ["ids"] = new JsonArray(c, d), ["end"] = "top", ["targets"] = new JsonArray(d, c) }));
        Assert.Empty(AttachRelations.Of(f.Model, c, "top"));
    }

    [Fact]
    public void A_circle_forced_by_hand_overflows_nothing_and_each_stands_at_its_own_height_saying_so()
    {
        var f = new Fixture(new ArchitectureModule(), new ProbeModule());
        var probe = Probe(f, 2, 0.5, 4, 0.5);
        var wall = f.Wall(-1, 0.5, 6, 0.5);
        f.Run("element.attachTo", AttachTop(probe, wall));
        // What the command refuses, made by a script: the wall attached to the probe attached to it.
        f.Session.Edit("by hand", e =>
        {
            var w = e.Model.Get(wall)!;
            var attrs = w.CloneAttrs();
            attrs["roofTop"] = "attach";
            attrs["roofIds"] = new JsonArray(probe);
            e.Replace(w.With(attrs: attrs));
        });
        Assert.Equal(3, f.Bounds(wall).Max.Z, 6);
        Assert.Contains(Problems(f, wall), p => p.Contains("depend on each other") && p.Contains($"„{f.Model.Get(probe)!.Name}”"));
        Assert.Equal(4.5, f.Bounds(probe).Max.Z, 6);
        Assert.Contains(Problems(f, probe), p => p.Contains("depend on each other"));
        // Built the other way round (the probe first), the same.
        f.Session.Edit("touch", e => e.Replace(e.Model.Get(probe)!.With(attrs: e.Model.Get(probe)!.CloneAttrs())));
        _ = f.Session.Geometry(probe);
        Assert.Equal(3, f.Bounds(wall).Max.Z, 6);
        // The relation made by hand too (a probe to a probe to it): no overflow either.
        var other = Probe(f, 2, 0.5, 6, 0.5);
        f.Run("element.attachTo", AttachTop(other, probe));
        f.Session.Edit("by hand", e => e.AddRelation(new Relation(e.NewId(), IfcClass.RelConnectsElements, other, [probe], new JsonObject { ["attach"] = "top", ["mode"] = "both" })));
        Assert.Equal(6.5, f.Bounds(other).Max.Z, 6);
        Assert.Contains(Problems(f, other), p => p.Contains("depend on each other"));
    }

    // ------------------------------------------------------------ undo, detach, attachments, preview

    [Fact]
    public void One_undo_restores_the_previous_attachment_and_redo_reapplies_it()
    {
        var (f, body, wall, upper) = Loft();
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = upper, ["points"] = new JsonArray(P(-3, -4), P(14, -4), P(14, 5), P(-3, 5)) })!["id"]!;
        f.Run("element.attachTo", AttachTop(wall, roof));
        var first = Attrs(f, wall);
        var firstTop = f.Bounds(wall).Max.Z;
        f.Run("element.attachTo", AttachTop(wall, body));
        var second = Attrs(f, wall);
        Assert.Equal(6, f.Bounds(wall).Max.Z, 2);
        f.Session.Undo();
        Assert.Equal(first, Attrs(f, wall));
        Assert.Equal(firstTop, f.Bounds(wall).Max.Z, 9);
        f.Session.Redo();
        Assert.Equal(second, Attrs(f, wall));
        Assert.Equal(6, f.Bounds(wall).Max.Z, 2);
        // add: the body joins the roof instead of replacing it.
        f.Session.Undo();
        var add = AttachTop(wall, body); add["add"] = true;
        f.Run("element.attachTo", add);
        Assert.Equal([roof, body], ((JsonArray)f.Model.Get(wall)!.Attrs["roofIds"]!).Select(x => (string)x!).ToArray());
    }

    [Fact]
    public void Attachments_detach_and_the_preview_say_what_the_engine_builds()
    {
        var (f, body, wall, _) = Loft();
        var classes = Q(f, "element.attachments", []);
        Assert.Contains("IfcWall", ((JsonArray)classes["classes"]!).Select(x => (string)x!));

        // The ghost: the end as it would be, nothing written.
        var revision = f.Model.Revision;
        var ghost = (JsonObject)Q(f, "element.attachPreview", AttachTop(wall, body))["elements"]![0]!;
        Assert.Equal(revision, f.Model.Revision);
        Assert.NotEqual("attach", f.Model.Get(wall)!.Str("roofTop"));
        Assert.Equal(6, (double)ghost["built"]!, 2);
        Assert.Equal(3, (double)ghost["height"]!, 6);
        var outline = ((JsonArray)ghost["outline"]!).Select(p => (double)p![2]!).ToList();
        Assert.Equal(6, outline.Max(), 2);
        Assert.Equal(2 * ((JsonArray)ghost["profile"]!).Count, outline.Count);

        f.Run("element.attachTo", AttachTop(wall, body));
        var end = (JsonObject)Q(f, "element.attachments", new JsonObject { ["id"] = wall })["elements"]![0]!["ends"]![0]!;
        Assert.True((bool)end["attached"]!);
        Assert.Equal(body, (string)end["targets"]![0]!["id"]!);
        Assert.Equal("both", (string)end["mode"]!);
        Assert.Equal(f.Bounds(wall).Max.Z, (double)end["result"]!["built"]!, 3);
        Assert.Empty((JsonArray)end["problems"]!);
        // The preview of what it is equals the end as built.
        Assert.Equal(ghost["profile"]!.ToJsonString(), end["result"]!["profile"]!.ToJsonString());

        // A door has nothing to attach.
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = f.Wall(0, 5, 6, 5), ["along"] = 1.5 })!["id"]!;
        Assert.False((bool)Q(f, "element.attachments", new JsonObject { ["id"] = door })["elements"]![0]!["attachable"]!);

        // Detached: its own 3 m, roofs ignored, as detachFromRoof (none) leaves it; one undo brings it back.
        f.Run("element.detach", new JsonObject { ["ids"] = new JsonArray(wall) });
        Assert.Equal(3, f.Bounds(wall).Max.Z, 6);
        Assert.Equal("none", f.Model.Get(wall)!.Str("roofTop"));
        Assert.Null(f.Model.Get(wall)!.Attrs["roofIds"]);
        end = (JsonObject)Q(f, "element.attachments", new JsonObject { ["id"] = wall })["elements"]![0]!["ends"]![0]!;
        Assert.False((bool)end["attached"]!);
        Assert.Equal("above", (string)end["level"]!["storey"]!);
        f.Session.Undo();
        Assert.Equal(6, f.Bounds(wall).Max.Z, 2);
        Assert.Throws<CommandException>(() => f.Run("element.detach", new JsonObject { ["ids"] = new JsonArray(door) }));
    }

    [Fact]
    public void A_relation_stored_end_goes_with_its_deleted_target()
    {
        var f = new Fixture(new ArchitectureModule(), new ProbeModule());
        var under = Probe(f, 0, 0, 4, 0.5);
        var probe = Probe(f, 0, 0, 0, 1);
        f.Run("element.attachTo", AttachTop(probe, under));
        Assert.Equal(4, f.Bounds(probe).Max.Z, 6);
        f.Run("element.delete", new JsonObject { ["id"] = under });
        Assert.Empty(AttachRelations.Of(f.Model, probe, "top"));
        Assert.Equal(1, f.Bounds(probe).Max.Z, 6);
        f.Session.Undo();
        Assert.Single(AttachRelations.Of(f.Model, probe, "top"));
        Assert.Equal(4, f.Bounds(probe).Max.Z, 6);
    }

    // ------------------------------------------------------------ the sampler

    /// <summary>A square at height z over x0..x1, y −1..1, looking down or up.</summary>
    static IEnumerable<BodySampler.Tri> Square(double x0, double x1, double z, bool down) =>
    [
        BodySampler.Of(new Vec3(x0, -1, z), new Vec3(x1, -1, z), new Vec3(x1, 1, z), down),
        BodySampler.Of(new Vec3(x0, -1, z), new Vec3(x1, 1, z), new Vec3(x0, 1, z), down),
    ];

    [Fact]
    public void The_sampler_reads_undersides_top_faces_and_areas()
    {
        // A slab's underside at 3 and its top at 3.2, over x 0..2; a step's top at 0.5 over x 0..2.
        var tris = Square(0, 2, 3, true).Concat(Square(0, 2, 3.2, false)).Concat(Square(0, 2, 0.5, false)).ToList();
        // Along a line from x −1 for 4 m, base 0, own height 2.5: up to 3 under the slab, steps found to a millimetre.
        var under = BodySampler.Underside(tris, new Vec2(-1, 0), new Vec2(1, 0), 4, 0, 2.5)!;
        Assert.Equal(3, BodySampler.Eval(under, 2), 9);
        Assert.Equal(2.5, BodySampler.Eval(under, 0.5), 9);
        Assert.Contains(under, p => Math.Abs(p.U - 1) < 1e-3 && Math.Abs(p.V - 3) < 1e-9);
        Assert.Contains(under, p => Math.Abs(p.U - 3) < 1e-3 && Math.Abs(p.V - 3) < 1e-9);
        // Its mirror for a bottom: the highest top face under the element's top (2.5), the slab's top above it left out.
        var over = BodySampler.Overside(tris, new Vec2(-1, 0), new Vec2(1, 0), 4, 0, 2.5)!;
        Assert.Equal(0.5, BodySampler.Eval(over, 2), 9);
        Assert.Equal(0, BodySampler.Eval(over, 0.5), 9);
        // Nothing over the line: null.
        Assert.Null(BodySampler.Underside(tris, new Vec2(5, 0), new Vec2(1, 0), 2, 0, 2.5));
        // Over an area: a section's points, one off the slab.
        Assert.Equal([3, 3, null], BodySampler.Area(tris, [new Vec2(1, 0), new Vec2(1.9, 0.9), new Vec2(2.5, 0)], 0, top: true));
        Assert.Equal([3.2, null], BodySampler.Area(tris, [new Vec2(1, 0), new Vec2(2.5, 0)], 3.5, top: false));
        Assert.Equal([0.5, null], BodySampler.Area(tris, [new Vec2(1, 0), new Vec2(2.5, 0)], 3.1, top: false));
        Assert.Equal(3.2, BodySampler.OverAt(tris, new Vec2(1, 0), 3.5));
        Assert.Equal(0.5, BodySampler.OverAt(tris, new Vec2(1, 0), 3.1));
        Assert.Equal(3, BodySampler.UnderAt(tris, new Vec2(1, 0), 0.6));
        Assert.Null(BodySampler.UnderAt(tris, new Vec2(1, 0), 3.0));
    }

    [Fact]
    public void The_sampler_never_reads_an_element_for_itself_and_leaves_its_guard_clean()
    {
        var f = new Fixture(new ArchitectureModule(), new ProbeModule());
        var a = Probe(f, 0, 0, 4, 0.5);
        var b = Probe(f, 0, 0, 0, 1);
        var services = (IServices)f.Session.Catalog;
        var s = BodySampler.Sample(f.Model.Get(b)!, [f.Model.Get(b)!, f.Model.Get(a)!], f.Model, services);
        Assert.False(s.InCycle);
        Assert.Equal(4, s.Tris.Count);   // a's top and bottom, two triangles each; none of b's own
        Assert.Equal(2, s.Tris.Count(t => t.Down));
        Assert.False(BodySampler.Building(b));
    }

    // ------------------------------------------------------------ the probe

    static string Probe(Fixture f, double x, double y, double z, double h) =>
        (string)f.Run("probe.add", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y, ["z"] = z, ["h"] = h })!["id"]!;

    /// <summary>
    /// A class after walls, as A2 and A5 will add them: a 1 × 1 box (<c>x</c>, <c>y</c> its centre, <c>z</c> its base, <c>h</c>
    /// its own height) whose top, attached by relations (<see cref="AttachRelations"/>), goes to the lowest underside over its
    /// centre through <see cref="BodySampler.Sample"/>; in a circle it stands at its own height and says so.
    /// </summary>
    sealed class ProbeModule : ILapinModule, IAttachable
    {
        public const string Class = "LapinTestProbe";
        public string Id => "probe";
        public string Name => "Probe";

        public void Register(IModuleRegistry r)
        {
            r.Class(new(Class, "Probe", [], "#888888"));
            r.Command("add", "Add probe", (ctx, a) =>
            {
                var e = new Entity(ctx.NewId(), Class, $"Probe {ctx.Model.OfClass(Class).Count() + 1}", new JsonObject
                {
                    ["x"] = Json.Number(a["x"]) ?? 0, ["y"] = Json.Number(a["y"]) ?? 0, ["z"] = Json.Number(a["z"]) ?? 0, ["h"] = Json.Number(a["h"]) ?? 1,
                });
                ctx.Add(e);
                Spatial.Contain(ctx, (string)a["storeyId"]!, e.Id);
                return new JsonObject { ["id"] = e.Id };
            });
            r.Geometry(Class, Build);
            r.Service<IAttachable>(this);
        }

        static ElementGeometry Build(Entity e, IModelView m, IServices services)
        {
            var (x, y, z, h) = (e.Num("x"), e.Num("y"), e.Num("z"), e.Num("h"));
            var top = z + h;
            var problems = new List<string>();
            var targets = AttachRelations.Of(m, e.Id, Attachments.Top).Select(r => m.Get(r.Relating)).OfType<Entity>().ToList();
            if (targets.Count > 0)
            {
                var s = BodySampler.Sample(e, targets, m, services);
                if (s.InCycle) problems.Add(Attachments.CycleProblem(m, services, e, Attachments.Top) ?? "Its top depends on itself.");
                else if (BodySampler.UnderAt(s.Tris, new Vec2(x, y), z) is { } hit) top = hit;
            }
            var box = Prism.Box(new Vec3(x, y, (z + top) / 2), 1, 1, top - z)!;
            return new ElementGeometry([new GeometryPart("Probe", "#888888", box)], Diagnostics: problems.Count > 0 ? problems : null);
        }

        public IReadOnlyList<string> Classes => [Class];
        public bool Handles(Entity e) => e.Class == Class;
        public IReadOnlyList<string> Ends(Entity e) => [Attachments.Top];
        public string? Refuse(IModelView m, Entity e, AttachSpec spec, IReadOnlyList<Entity> targets) => spec.Level != null ? "No levels for probes." : null;
        public void Attach(ICommandContext ctx, Entity e, AttachSpec spec) => AttachRelations.Set(ctx, e, spec);
        public void Detach(ICommandContext ctx, Entity e, string end) => AttachRelations.Clear(ctx, e, end);
        public AttachState? StateOf(IModelView m, Entity e, string end) => AttachRelations.State(m, e, end);
        public JsonObject Result(IModelView m, IServices services, Entity e, string end, AttachSpec? asIf) => new() { ["problems"] = new JsonArray() };
    }
}
