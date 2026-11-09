using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>Components (docs/components.md): instances of Studio solids, floors and rooms; what changes in one follows in the others, in one undo step, under the ids they had; never walls.</summary>
public class ComponentTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());

    static JsonArray P(double x, double y) => new(x, y);
    static JsonArray Ring(double x, double y, double w, double h) => new(P(x, y), P(x + w, y), P(x + w, y + h), P(x, y + h));
    static string Id(JsonNode? r) => (string)r!["id"]!;
    static JsonArray Ids(params string[] ids) => new([.. ids.Select(x => (JsonNode)x)]);

    /// <summary>A 2 × 1 floor at (0, 0) with a box on it, made a component, and a second instance 10 m along.</summary>
    static (Fixture F, string Group, string Twin, string Slab, string Box, string Component) Two()
    {
        var f = F();
        var slab = Id(f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Ring(0, 0, 2, 1) }));
        var box = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = new JsonArray(0.5, 0.5), ["width"] = 0.5, ["length"] = 0.5, ["height"] = 1 }));
        var made = f.Run("component.make", new JsonObject { ["ids"] = Ids(slab, box), ["name"] = "Bay" })!;
        var group = Id(made);
        var copy = f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "copy", ["by"] = P(10, 0) })!;
        var twin = copy["ids"]!.AsArray().Select(x => (string)x!).First(x => f.Model.Get(x)!.Class == Groups.Class);
        return (f, group, twin, slab, box, (string)made["component"]!);
    }

    static Entity MemberOf(Fixture f, string group, string cls) => Groups.MembersOf(f.Model, group).Select(f.Model.Get).OfType<Entity>().First(e => e.Class == cls);
    static IEnumerable<(double X, double Y)> Corners(Entity slab) => slab.Attrs["contour"]!.AsArray().Select(p => (Json.Number(p![0]) ?? 0, Json.Number(p![1]) ?? 0));
    static double MinX(Entity slab) => Corners(slab).Min(c => c.X);
    static double MaxX(Entity slab) => Corners(slab).Max(c => c.X);

    [Fact]
    public void Many_instances_follow_an_edit_in_reasonable_time()
    {
        var (f, group, _, slab, _, component) = Two();
        for (var i = 2; i <= 200; i++) f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "copy", ["by"] = P(10 * i, 0) });
        Assert.Equal(201, Components.Instances(f.Model, component).Count());
        var clock = System.Diagnostics.Stopwatch.StartNew();
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = Ring(0, 0, 3, 1) });
        clock.Stop();
        Console.WriteLine($"An edit carried to 200 instances of two members: {clock.ElapsedMilliseconds} ms");
        Assert.True(clock.ElapsedMilliseconds < 20_000, $"{clock.ElapsedMilliseconds} ms");
        Assert.All(Components.Instances(f.Model, component), g => Assert.Equal(3, MaxX(MemberOf(f, g.Id, IfcClass.Slab)) - MinX(MemberOf(f, g.Id, IfcClass.Slab)), 6));
    }

    [Fact]
    public void Walls_cannot_be_part_of_a_component()
    {
        var f = F();
        var wall = f.Wall(0, 0, 4, 0);
        var ex = Assert.Throws<CommandException>(() => f.Run("component.make", new JsonObject { ["ids"] = Ids(wall) }));
        Assert.Contains("only Studio solids, floors and rooms", ex.Message);
        Assert.Empty(f.Model.OfClass(Components.Class));
    }

    [Fact]
    public void A_copy_is_another_instance_with_the_same_members()
    {
        var (f, group, twin, _, _, component) = Two();
        Assert.Equal(component, f.Model.Get(twin)!.Str(Components.Key));
        Assert.Equal(2, Components.Instances(f.Model, component).Count());
        var keys = (string g) => Groups.MembersOf(f.Model, g).Select(f.Model.Get).Select(e => e!.Str(Components.MemberKey)).OrderBy(x => x).ToList();
        Assert.Equal(keys(group), keys(twin));
        Assert.DoesNotContain(null, keys(twin));
    }

    [Fact]
    public void What_changes_in_one_follows_in_the_other_in_one_undo_under_the_same_ids()
    {
        var (f, group, twin, slab, _, _) = Two();
        var twinSlab = MemberOf(f, twin, IfcClass.Slab).Id;
        var twinBox = MemberOf(f, twin, "IfcBuildingElementProxy").Id;
        Assert.Equal(10, MinX(f.Model.Get(twinSlab)!), 6);

        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = Ring(0, 0, 5, 1) });

        // Under the ids they had, wider by the same, the group whole.
        Assert.Equal(15, MaxX(f.Model.Get(twinSlab)!), 6);
        Assert.Equal(2, Groups.MembersOf(f.Model, twin).Count());
        Assert.NotNull(f.Model.Get(twinBox));
        Assert.Equal(IfcClass.Slab, MemberOf(f, twin, IfcClass.Slab).Class);
        Assert.Equal(twinSlab, MemberOf(f, twin, IfcClass.Slab).Id);
        Assert.Equal(twin, Groups.Of(f.Model, twinSlab)!.Id);

        // Edited from the other instance, back to the first.
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = twinSlab, ["points"] = Ring(10, 0, 3, 1) });
        Assert.Equal(3, MaxX(f.Model.Get(slab)!), 6);

        // One undo step each.
        f.Session.Undo();
        Assert.Equal(5, MaxX(f.Model.Get(slab)!), 6);
        Assert.Equal(15, MaxX(f.Model.Get(twinSlab)!), 6);
        f.Session.Undo();
        Assert.Equal(2, MaxX(f.Model.Get(slab)!), 6);
        Assert.Equal(12, MaxX(f.Model.Get(twinSlab)!), 6);
    }

    [Fact]
    public void Moving_or_turning_an_instance_leaves_the_others()
    {
        var (f, group, twin, slab, _, _) = Two();
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "move", ["by"] = P(0, 7) });
        Assert.Equal(0, Corners(f.Model.Get(slab)!).Min(c => c.Y), 6);
        // Turned 90°, an edit of the first reaches it turned.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "rotate", ["center"] = P(10, 7), ["angle"] = 90 });
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = Ring(0, 0, 5, 1) });
        var turned = MemberOf(f, twin, IfcClass.Slab);
        Assert.Equal(5, Corners(turned).Max(c => c.Y) - Corners(turned).Min(c => c.Y), 6);
        Assert.Equal(1, Corners(turned).Max(c => c.X) - Corners(turned).Min(c => c.X), 6);
        Assert.Equal(group, Groups.Of(f.Model, slab)!.Id);
    }

    [Fact]
    public void A_member_taken_out_goes_from_the_others_and_a_solid_edited_follows()
    {
        var (f, _, twin, slab, box, _) = Two();
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(box) });
        Assert.Single(Groups.MembersOf(f.Model, twin));
        Assert.Equal(IfcClass.Slab, MemberOf(f, twin, IfcClass.Slab).Class);
        Assert.NotNull(f.Model.Get(slab));
    }

    [Fact]
    public void A_solid_drawn_from_a_sketch_follows_with_its_sketch_and_leaves_no_lines_behind()
    {
        var f = F();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var rect = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(P(0, 0), P(1, 0), P(1, 1), P(0, 1)), ["closed"] = true };
        var profile = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = rect })!["ids"]![0]!;
        var solid = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = profile, ["depth"] = 2 }));
        var group = Id(f.Run("component.make", new JsonObject { ["ids"] = Ids(solid) }));
        var copied = f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "copy", ["by"] = P(5, 0) })!;
        var twin = copied["ids"]!.AsArray().Select(x => (string)x!).First(x => f.Model.Get(x)!.Class == Groups.Class);
        var twinSolid = Groups.MembersOf(f.Model, twin).Single();
        var shapes = f.Model.OfClass(IfcClass.Annotation).Count();
        Assert.Equal(2, shapes);

        f.Run("element.update", new JsonObject { ["id"] = solid, ["attrs"] = new JsonObject { ["depth"] = 3 } });

        Assert.Equal(3, f.Model.Get(twinSolid)!.Num("depth"), 6);
        Assert.Equal(shapes, f.Model.OfClass(IfcClass.Annotation).Count());
        Assert.NotEqual(profile, f.Model.Get(twinSolid)!.Str("profile"));
        Assert.NotNull(f.Model.Get(f.Model.Get(twinSolid)!.Str("profile")!));
    }

    [Fact]
    public void Copied_to_another_storey_it_stands_there_and_still_follows()
    {
        var f = F();
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First", ["elevation"] = 3 })!["id"]!;
        var ground = Spatial.Storeys(f.Model).First().Id;
        var slab = Id(f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = ground, ["points"] = Ring(0, 0, 2, 1) }));
        var box = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = new JsonArray(0.5, 0.5), ["storeyId"] = ground, ["width"] = 0.5, ["length"] = 0.5, ["height"] = 1 }));
        var group = Id(f.Run("component.make", new JsonObject { ["ids"] = Ids(slab, box) }));
        f.Run("element.copyToStoreys", new JsonObject { ["ids"] = Ids(group), ["storeyIds"] = Ids(first) });
        var twin = Groups.All(f.Model).Single(g => g.Id != group);
        Assert.Equal(f.Model.Get(group)!.Str(Components.Key), twin.Str(Components.Key));
        var z = (string g) => Json.Number(Groups.MembersOf(f.Model, g).Select(f.Model.Get).First(e => e!.Str("operation") != null)!.Attrs["at"]![2]) ?? 0;
        Assert.Equal(3, z(twin.Id), 6);

        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = Ring(0, 0, 5, 1) });

        Assert.Equal(5, MaxX(MemberOf(f, twin.Id, IfcClass.Slab)), 6);
        Assert.Equal(first, Spatial.StoreyOf(f.Model, MemberOf(f, twin.Id, IfcClass.Slab).Id)!.Id);
        Assert.Equal(3, z(twin.Id), 6);
    }

    [Fact]
    public void Made_unique_it_goes_its_own_way_and_a_component_without_instances_goes()
    {
        var (f, group, twin, slab, _, component) = Two();
        f.Run("component.makeUnique", new JsonObject { ["id"] = twin });
        var twinSlab = MemberOf(f, twin, IfcClass.Slab).Id;
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = Ring(0, 0, 5, 1) });
        Assert.Equal(12, MaxX(f.Model.Get(twinSlab)!), 6);
        Assert.Equal(2, f.Model.OfClass(Components.Class).Count());

        f.Run("element.delete", new JsonObject { ["ids"] = Ids(group) });
        Assert.Null(f.Model.Get(component));
    }

    [Fact]
    public void A_mirrored_instance_stays_an_instance_and_follows_mirrored()
    {
        var (f, group, _, slab, _, component) = Two();
        // A mirrored copy about the line x = -1: from x in 0..2 to x in -4..-2.
        var made = f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "mirror", ["a"] = P(-1, 0), ["b"] = P(-1, 1), ["copy"] = true })!;
        var twin = made["ids"]!.AsArray().Select(x => (string)x!).First(x => f.Model.Get(x)!.Class == Groups.Class);
        Assert.Equal(component, f.Model.Get(twin)!.Str(Components.Key));
        var floor = () => MemberOf(f, twin, IfcClass.Slab);
        Assert.Equal(-2, MaxX(floor()), 6);
        Assert.Equal(-4, MinX(floor()), 6);
        // The first made wider, away from the mirror line: the mirrored one grows the other way.
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = Ring(0, 0, 5, 3) });
        Assert.Equal(-2, MaxX(floor()), 6);
        Assert.Equal(-7, MinX(floor()), 6);
        Assert.Equal(3, Corners(floor()).Max(c => c.Y), 6);
        // Edited from the mirrored one, back to the first, the right way round.
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = floor().Id, ["points"] = Ring(-4, 0, 2, 2) });
        Assert.Equal(2, MaxX(f.Model.Get(slab)!), 6);
        Assert.Equal(0, MinX(f.Model.Get(slab)!), 6);
        Assert.Equal(2, Corners(f.Model.Get(slab)!).Max(c => c.Y), 6);
    }

    [Fact]
    public void An_instance_mirrored_in_place_then_turned_still_follows()
    {
        var (f, group, twin, slab, _, _) = Two();
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "mirror", ["a"] = P(11, 0), ["b"] = P(11, 1) });
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "rotate", ["center"] = P(11, 0), ["angle"] = 90 });
        var size = (Entity e) => (Corners(e).Max(c => c.X) - Corners(e).Min(c => c.X), Corners(e).Max(c => c.Y) - Corners(e).Min(c => c.Y));
        Assert.Equal(1, size(MemberOf(f, twin, IfcClass.Slab)).Item1, 6);
        Assert.Equal(2, size(MemberOf(f, twin, IfcClass.Slab)).Item2, 6);
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = Ring(0, 0, 6, 1) });
        Assert.Equal(1, size(MemberOf(f, twin, IfcClass.Slab)).Item1, 6);
        Assert.Equal(6, size(MemberOf(f, twin, IfcClass.Slab)).Item2, 6);
        Assert.Equal(group, Groups.Of(f.Model, slab)!.Id);
    }
}
