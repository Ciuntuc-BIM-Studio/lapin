using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// A cut into a solid's face goes with its solid (a user's report): a group moved, turned, mirrored or copied carries
/// the voids cutting its members (shapes drawn on a face and pushed in, faces pushed into a wall), and a union's or an
/// intersection's tools, though they are not members themselves; one element moved alone carries them too. Their ids
/// stay, each command is one undo step, and a copy's void cuts the copy.
/// </summary>
public class StudioGroupVoidTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static JsonArray J3(Vec3 p) => [p.X, p.Y, p.Z];
    static JsonArray Ids(params string[] ids) => [.. ids.Select(x => (JsonNode)x)];
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static string Id(JsonNode? r, string key = "id") => (string)r![key]!;

    /// <summary>The cube less its pocket: 2 × 2 × 2, a 0.2 × 0.2 square on its east face pushed 0.4 in.</summary>
    const double Cut = 8 - 0.2 * 0.2 * 0.4;

    static string Plan(Fixture f) => Id(f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey }));

    /// <summary>A 2 × 2 square at (x, y) on the plan extruded 2 m up.</summary>
    static string Cube(Fixture f, string plan, double x, double y)
    {
        var sq = (string)f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = plan,
            ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(x, y), J(x + 2, y), J(x + 2, y + 2), J(x, y + 2)), ["closed"] = true },
        })!["ids"]![0]!;
        return Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = sq, ["depth"] = 2 }));
    }

    /// <summary>The square pocket on the east face (x = 2) of the cube at the origin: the void's id.</summary>
    static string Pocket(Fixture f, string cube)
    {
        var r = f.Run("studio.onFace", new JsonObject
        {
            ["id"] = cube, ["point"] = J3(new(2, 0.3, 0.3)), ["normal"] = J3(new Vec3(1, 0, 0)), ["distance"] = -0.4,
            ["outline"] = new JsonArray(J3(new(2, 0.2, 0.2)), J3(new(2, 0.4, 0.2)), J3(new(2, 0.4, 0.4)), J3(new(2, 0.2, 0.4))),
        })!;
        Assert.Equal("cut", (string)r["how"]!);
        return Id(r);
    }

    static (Fixture F, string A, string B, string Void, string Group) Grouped()
    {
        var f = F();
        var plan = Plan(f);
        var a = Cube(f, plan, 0, 0);
        var b = Cube(f, plan, 1, 0); // overlapping A, as in the report
        var v = Pocket(f, a);
        Assert.Equal(Cut, Vol(f, a), 6);
        var g = Id(f.Run("group.create", new JsonObject { ["ids"] = Ids(a, b) }));
        Assert.Null(Groups.Of(f.Model, v));
        return (f, a, b, v, g);
    }

    [Fact]
    public void A_group_moved_carries_the_void_cutting_its_member_in_one_undo_step()
    {
        var (f, a, _, v, g) = Grouped();
        var before = f.Model.Get(v)!.Attrs.ToJsonString();
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(g), ["op"] = "move", ["by"] = J(5, 3) });
        Assert.Equal(5, f.Bounds(a).Min.X, 6);
        // Still cut, by the same void (its id kept), now where the solid is.
        Assert.Equal(Cut, Vol(f, a), 6);
        Assert.NotNull(f.Model.Get(v));
        Assert.NotEqual(before, f.Model.Get(v)!.Attrs.ToJsonString());
        // One undo step puts both back.
        f.Session.Undo();
        Assert.Equal(0, f.Bounds(a).Min.X, 6);
        Assert.Equal(before, f.Model.Get(v)!.Attrs.ToJsonString());
        Assert.Equal(Cut, Vol(f, a), 6);
    }

    [Fact]
    public void A_group_moved_up_turned_or_mirrored_keeps_its_member_cut()
    {
        var (f, a, _, _, g) = Grouped();
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(g), ["op"] = "move", ["by"] = J(0, 0), ["up"] = 1.5 });
        Assert.Equal(1.5, f.Bounds(a).Min.Z, 6);
        Assert.Equal(Cut, Vol(f, a), 6);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(g), ["op"] = "rotate", ["center"] = J(-3, 1), ["angle"] = 90 });
        Assert.Equal(Cut, Vol(f, a), 6);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(g), ["op"] = "mirror", ["a"] = J(10, 0), ["b"] = J(10, 1) });
        Assert.Equal(Cut, Vol(f, a), 6);
        // The pocket is in the mirrored solid where the mirror put it: not where it was (the solid would be whole there).
        Assert.True(f.Bounds(a).Min.X > 10);
    }

    [Fact]
    public void A_group_copied_copies_the_void_and_the_copy_cuts_the_copy()
    {
        var (f, a, _, v, g) = Grouped();
        var made = f.Run("element.transform", new JsonObject { ["ids"] = Ids(g), ["op"] = "copy", ["by"] = J(10, 0) })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        var twin = made.Select(f.Model.Get).Single(e => e!.Class == Groups.Class)!;
        var copyOfA = Groups.MembersOf(f.Model, twin.Id).Single(id => f.Bounds(id).Min.X < 10.5);
        Assert.Equal(Cut, Vol(f, copyOfA), 6);
        Assert.Equal(Cut, Vol(f, a), 6);
        // A void of its own, cutting the copy; the original's stays the original's.
        var voids = f.Model.Relations.Where(x => x.Class == IfcClass.RelInterferesElements && x.Relating == copyOfA).SelectMany(x => x.Related).ToList();
        Assert.Single(voids);
        Assert.NotEqual(v, voids[0]);
        Assert.True(StudioModule.IsVoid(f.Model.Get(voids[0])!));
        Assert.Contains(f.Model.Relations, x => x.Class == IfcClass.RelInterferesElements && x.Relating == a && x.Related.Contains(v));
        f.Session.Undo();
        Assert.Null(f.Model.Get(voids[0]));
    }

    [Fact]
    public void A_solid_moved_alone_carries_its_void_and_a_void_cutting_one_that_stays_stays()
    {
        var f = F();
        var plan = Plan(f);
        var a = Cube(f, plan, 0, 0);
        var v = Pocket(f, a);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(a), ["op"] = "move", ["by"] = J(0, 4) });
        Assert.Equal(Cut, Vol(f, a), 6);
        // A solid made to cut two (A and C): only A moved, it stays, still cutting C.
        var c = Cube(f, plan, 0, 8);
        var tool = (string)f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = plan,
            ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0.5, 0), J(1.5, 0), J(1.5, 10), J(0.5, 10)), ["closed"] = true },
        })!["ids"]![0]!;
        var both = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = tool, ["depth"] = 1, ["cut"] = Ids(a, c) }));
        var cBefore = Vol(f, c);
        Assert.True(cBefore < 8);
        var bothBefore = f.Model.Get(both)!.Attrs.ToJsonString();
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(a), ["op"] = "move", ["by"] = J(20, 0) });
        Assert.Equal(bothBefore, f.Model.Get(both)!.Attrs.ToJsonString());
        Assert.Equal(cBefore, Vol(f, c), 6);
        Assert.Equal(Cut, Vol(f, a), 6);
        Assert.NotNull(f.Model.Get(v));
    }

    [Fact]
    public void A_face_pushed_into_a_grouped_wall_goes_with_the_group()
    {
        var f = F();
        var plan = Plan(f);
        var wall = f.Wall(0, 0, 6, 0);
        var cube = Cube(f, plan, 0, 5);
        var whole = Vol(f, wall);
        var y = f.Bounds(wall).Min.Y;
        var r = f.Run("studio.pushPull", new JsonObject { ["id"] = wall, ["point"] = J3(new(3, y, 1)), ["normal"] = J3(new Vec3(0, -1, 0)), ["distance"] = -0.05 })!;
        Assert.Equal("cut", (string)r["how"]!);
        var cut = Vol(f, wall);
        Assert.True(cut < whole - 1e-6);
        var g = Id(f.Run("group.create", new JsonObject { ["ids"] = Ids(wall, cube) }));
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(g), ["op"] = "move", ["by"] = J(0, 3) });
        Assert.Equal(cut, Vol(f, wall), 6);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(g), ["op"] = "rotate", ["center"] = J(0, 0), ["angle"] = 30 });
        Assert.Equal(cut, Vol(f, wall), 6);
    }

    [Fact]
    public void A_unions_tool_goes_with_its_grouped_target()
    {
        var f = F();
        var plan = Plan(f);
        var a = Cube(f, plan, 0, 0);
        var t = Cube(f, plan, 1, 1);
        var b = Cube(f, plan, 10, 0);
        f.Run("studio.boolean", new JsonObject { ["op"] = "union", ["target"] = a, ["tools"] = Ids(t) });
        var joined = Vol(f, a);
        Assert.Equal(8 + 8 - 1 * 1 * 2, joined, 6);
        var g = Id(f.Run("group.create", new JsonObject { ["ids"] = Ids(a, b) }));
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(g), ["op"] = "move", ["by"] = J(0, 7) });
        Assert.Equal(joined, Vol(f, a), 6);
        Assert.Equal(7, f.Bounds(a).Min.Y, 6);
        Assert.Equal(10, f.Bounds(a).Max.Y, 6);
    }
    [Fact]
    public void A_group_arrayed_or_copied_to_a_storey_takes_its_voids_to_every_copy()
    {
        var (f, _, _, _, g) = Grouped();
        var voids = () => f.Model.Relations.Count(x => x.Class == IfcClass.RelInterferesElements);
        Assert.Equal(1, voids());
        f.Run("element.array", new JsonObject { ["ids"] = Ids(g), ["count"] = 3, ["step"] = new JsonArray(10, 0, 0) });
        Assert.Equal(3, voids());
        var cut = f.Model.Relations.Where(x => x.Class == IfcClass.RelInterferesElements).Select(x => x.Relating).ToList();
        Assert.All(cut, id => Assert.Equal(Cut, Vol(f, id), 6));
        var upper = Id(f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 }));
        f.Run("element.copyToStoreys", new JsonObject { ["ids"] = Ids(g), ["storeyIds"] = Ids(upper) });
        var up = f.Model.Relations.Where(x => x.Class == IfcClass.RelInterferesElements && Spatial.StoreyOf(f.Model, x.Relating)?.Id == upper).ToList();
        var r = Assert.Single(up);
        Assert.Equal(upper, Spatial.StoreyOf(f.Model, r.Related[0])?.Id);
        Assert.Equal(Cut, Vol(f, r.Relating), 6);
    }
}
