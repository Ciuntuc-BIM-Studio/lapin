using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Arrays that keep their arrangement (decision 481): in a row, round a centre and along a path, the rule kept on a
/// LapinArray beside the copies; a changed count or spacing, or the path edited, places the items again with their ids
/// kept, the added copied from the original, the surplus deleted; one undo step each; exploded, the copies stay plain.
/// </summary>
public class ArrayArrangementTests
{
    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());
    static Vec2 P(JsonNode? n) => Args.PointOf(n);
    static Vec2 Start(Fixture f, string id) => P(f.Model.Get(id)!.Attrs["start"]);
    static Vec2 End(Fixture f, string id) => P(f.Model.Get(id)!.Attrs["end"]);
    static Vec2 Mid(Fixture f, string id) => (Start(f, id) + End(f, id)) * 0.5;
    static Vec2 Dir(Fixture f, string id) => (End(f, id) - Start(f, id)).Normalized();
    static void Near(Vec2 want, Vec2 got, double tol = 1e-6) => Assert.True((want - got).Length < tol, $"want {want}, got {got}");

    static Entity Array(Fixture f, string arrayId) => f.Model.Get(arrayId)!;
    static List<List<string>> Items(Fixture f, string arrayId) => Arrays.Items(Array(f, arrayId)).Select(i => i.Ids).ToList();
    static List<string> Copies(Fixture f, string arrayId) => Items(f, arrayId).Skip(1).Select(i => i.Single()).ToList();

    [Fact]
    public void A_row_keeps_its_arrangement_and_a_new_count_keeps_the_first_copies_ids()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 0, 2);
        var r = f.Run("element.array", new JsonObject { ["ids"] = Ids(wall), ["count"] = 4, ["step"] = new JsonArray(3, 0, 0) })!;
        var arr = (string)r["array"]!;
        Assert.Equal(3, r["ids"]!.AsArray().Count);
        Assert.Equal(Arrays.Class, Array(f, arr).Class);
        var first = Copies(f, arr);
        Assert.Equal(wall, Items(f, arr)[0].Single());
        Near(new Vec2(9, 0), Start(f, first[2]));
        // Asked of a copy, the arrangement says which item it is.
        var of = f.Session.Query("array.of", new JsonObject { ["id"] = first[1] })!;
        Assert.Equal(arr, (string)of["id"]!);
        Assert.Equal(2, (int)of["index"]!);
        Assert.Equal(4, (int)of["count"]!);

        // 4 → 6: the three copies keep their ids and places, two more after them.
        var walls = f.Model.OfClass(IfcClass.Wall).Count();
        f.Run("array.update", new JsonObject { ["id"] = first[0], ["count"] = 6 });
        var six = Copies(f, arr);
        Assert.Equal(5, six.Count);
        Assert.Equal(first, six.Take(3));
        Near(new Vec2(15, 0), Start(f, six[4]));
        Assert.Equal(walls + 2, f.Model.OfClass(IfcClass.Wall).Count());

        // 6 → 3: the surplus deleted, the first two still the same.
        f.Run("array.update", new JsonObject { ["id"] = arr, ["count"] = 3 });
        Assert.Equal(first.Take(2), Copies(f, arr));
        Assert.Equal(3, f.Model.OfClass(IfcClass.Wall).Count());

        // A new spacing moves them, the ids kept.
        f.Run("array.update", new JsonObject { ["id"] = arr, ["step"] = new JsonArray(0, 4, 0) });
        Near(new Vec2(0, 8), Start(f, first[1]));
        Assert.Equal(first.Take(2), Copies(f, arr));

        // Each change one undo step: back to six copies at 3 m, then four.
        f.Session.Undo();
        Near(new Vec2(6, 0), Start(f, first[1]));
        f.Session.Undo();
        Assert.Equal(6, f.Model.OfClass(IfcClass.Wall).Count());
        Assert.Equal(six, Copies(f, arr));
        f.Session.Undo();
        Assert.Equal(first, Copies(f, arr));
        f.Session.Undo();
        Assert.Single(f.Model.OfClass(IfcClass.Wall));
        Assert.Empty(Arrays.All(f.Model));
    }

    [Fact]
    public void A_row_filling_an_extent_shares_it_out_again_for_a_new_count()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 0, 1);
        var arr = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(wall), ["count"] = 4, ["fit"] = new JsonArray(9, 0, 0) })!["array"]!;
        Near(new Vec2(3, 0), Start(f, Copies(f, arr)[0]));
        f.Run("array.update", new JsonObject { ["id"] = arr, ["count"] = 7 });
        var c = Copies(f, arr);
        Near(new Vec2(1.5, 0), Start(f, c[0]));
        Near(new Vec2(9, 0), Start(f, c[5]));
    }

    [Fact]
    public void A_copy_moved_by_hand_keeps_its_offset_when_the_rule_moves_it()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 0, 1);
        var arr = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(wall), ["count"] = 3, ["step"] = new JsonArray(2, 0, 0) })!["array"]!;
        var c = Copies(f, arr);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(c[1]), ["op"] = "move", ["by"] = new JsonArray(0, 0.5) });
        f.Run("array.update", new JsonObject { ["id"] = arr, ["step"] = new JsonArray(3, 0, 0) });
        Near(new Vec2(6, 0.5), Start(f, c[1]));
        // One deleted by hand stays a gap: the others keep their places and ids.
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(c[0]) });
        Assert.Empty(Items(f, arr)[1]);
        f.Run("array.update", new JsonObject { ["id"] = arr, ["count"] = 4 });
        Assert.Empty(Items(f, arr)[1]);
        Near(new Vec2(9, 0), Start(f, Items(f, arr)[3].Single()));
    }

    [Fact]
    public void A_polar_array_keeps_its_ids_for_a_new_count()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(5, 0, 6, 0);
        var arr = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(wall), ["kind"] = "polar", ["count"] = 4, ["angle"] = 360, ["center"] = new JsonArray(0, 0) })!["array"]!;
        var c = Copies(f, arr);
        Near(new Vec2(0, 5), Start(f, c[0]));
        f.Run("array.update", new JsonObject { ["id"] = arr, ["count"] = 8 });
        var eight = Copies(f, arr);
        Assert.Equal(c, eight.Take(3));
        // A full turn shared by eight: the first copy at 45°, the ids kept.
        Near(new Vec2(5 * Math.Cos(Math.PI / 4), 5 * Math.Sin(Math.PI / 4)), Start(f, c[0]));
        Near(new Vec2(0, 5), Start(f, eight[1]));
        Assert.Equal(7, eight.Count);
    }

    /// <summary>A path: an arched wall from (0, 0) to (20, 0) rising 5 m to its left.</summary>
    static (Fixture F, string Path, PathCurve Curve) Arched()
    {
        var f = new Fixture(new ArchitectureModule());
        var path = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 20, ["y2"] = 0, ["arc"] = 5 })!["id"]!;
        return (f, path, PathCurve.Of(f.Model.Get(path)!));
    }

    [Fact]
    public void Along_a_curved_path_the_copies_stand_on_it_turned_to_its_tangent_and_set_off_it()
    {
        var (f, path, c) = Arched();
        // The original: 1 m along the tangent at the path's start, 2 m off it to the right (inside the curve: the path edited below sweeps outside, so it never runs through a copy).
        var (p0, t0) = (c.PointAt(0), c.TangentAt(0));
        var src = f.Wall(p0.X + t0.Perp.X * -2 - t0.X * 0.5, p0.Y + t0.Perp.Y * -2 - t0.Y * 0.5, p0.X + t0.Perp.X * -2 + t0.X * 0.5, p0.Y + t0.Perp.Y * -2 + t0.Y * 0.5);
        var r = f.Run("element.array", new JsonObject
        {
            ["ids"] = Ids(src), ["kind"] = "path", ["along"] = path, ["count"] = 5, ["offset"] = -2, ["orient"] = "tangent",
        })!;
        var arr = (string)r["array"]!;
        var items = Items(f, arr).Select(i => i.Single()).ToList();
        Assert.Equal(5, items.Count);
        Assert.Equal(src, items[0]);
        for (var i = 0; i < 5; i++)
        {
            var s = c.Length * i / 4;
            Near(c.At(s, -2), Mid(f, items[i]), 1e-6);
            Near(c.TangentAt(s), Dir(f, items[i]), 1e-6);
        }
        // The preview is the rule's: the same places.
        var pv = f.Session.Query("array.preview", new JsonObject { ["id"] = arr })!;
        Assert.Equal(5, pv["places"]!.AsArray().Count);
        Assert.True(pv["path"]!.AsArray().Count > 10);

        // The path edited (its end moved, its rise changed): the copies follow it, every id kept, in the same undo step.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = path, ["x1"] = 0, ["y1"] = 0, ["x2"] = 30, ["y2"] = 0, ["arc"] = 8 });
        var c2 = PathCurve.Of(f.Model.Get(path)!);
        Assert.Equal(items, Items(f, arr).Select(i => i.Single()));
        for (var i = 0; i < 5; i++)
        {
            var s = c2.Length * i / 4;
            Near(c2.At(s, -2), Mid(f, items[i]), 1e-6);
            Near(c2.TangentAt(s), Dir(f, items[i]), 1e-6);
        }
        f.Session.Undo();
        Near(c.At(c.Length / 2, -2), Mid(f, items[2]), 1e-6);
        Assert.Equal(PathCurve.Of(f.Model.Get(path)!).Length, c.Length, 9);
    }

    [Fact]
    public void Along_a_path_by_spacing_with_its_ends_held_back_fixed_or_flipped()
    {
        var f = new Fixture(new ArchitectureModule());
        var src = f.Wall(0, 0, 0, 1);
        // 20 m of polyline (10 east, 10 north), 1 m held back at each end, every 3 m: 0, 3, 6 … 18 → 7 items.
        var arr = (string)f.Run("element.array", new JsonObject
        {
            ["ids"] = Ids(src), ["kind"] = "path", ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 10)),
            ["spacing"] = 3, ["start"] = 1, ["end"] = 1, ["orient"] = "fixed", ["base"] = new JsonArray(0, 0),
        })!["array"]!;
        var items = Items(f, arr).Select(i => i.Single()).ToList();
        Assert.Equal(7, items.Count);
        Near(new Vec2(1, 0), Start(f, items[0]));
        Near(new Vec2(10, 3), Start(f, items[4]));
        // Fixed: none turned.
        Assert.All(items, id => Near(new Vec2(0, 1), Dir(f, id)));
        // No count kept: it follows from the spacing.
        Assert.Null(Array(f, arr).Attrs["count"]);
        f.Run("array.update", new JsonObject { ["id"] = arr, ["spacing"] = 2 });
        Assert.Equal(items, Items(f, arr).Take(7).Select(i => i.Single()));
        Assert.Equal(10, Items(f, arr).Count);
        Assert.Throws<CommandException>(() => f.Run("array.update", new JsonObject { ["id"] = arr, ["count"] = 12 }));
    }

    [Fact]
    public void Along_a_drafted_arc_and_a_curved_road_exactly()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule(), new CivilModule());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var arc = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "arc", ["c"] = new JsonArray(0, 0), ["r"] = 10, ["start"] = 0, ["end"] = Math.PI }) })!["ids"]![0]!;
        var src = f.Wall(10, -0.5, 10, 0.5);
        var arr = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(src), ["kind"] = "path", ["along"] = arc, ["count"] = 5 })!["array"]!;
        var items = Items(f, arr).Select(i => i.Single()).ToList();
        for (var i = 0; i < 5; i++)
        {
            var a = Math.PI * i / 4;
            Near(new Vec2(10 * Math.Cos(a), 10 * Math.Sin(a)), Mid(f, items[i]), 1e-6);
            Near(new Vec2(-Math.Sin(a), Math.Cos(a)), Dir(f, items[i]), 1e-6);
        }

        // A road rounded at its corner by a 15 m curve: the copies on its alignment, along the arc's tangent.
        var road = (string)f.Run("civil.addPath", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 30), new JsonArray(40, 30), new JsonArray(40, 70)), ["width"] = 6 })!["id"]!;
        f.Run("civil.setCorridor", new JsonObject { ["id"] = road, ["curves"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(15, 0, 0), new JsonArray(0, 0, 0)) });
        var chain = Corridors.ChainOf(f.Model.Get(road)!);
        Assert.NotNull(chain.Alignment);
        var lamp = f.Wall(0, 34, 1, 34);
        var r2 = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(lamp), ["kind"] = "path", ["along"] = road, ["count"] = 9, ["offset"] = 4, ["base"] = new JsonArray(0.5, 34) })!["array"]!;
        var lamps = Items(f, r2).Select(i => i.Single()).ToList();
        for (var i = 0; i < 9; i++)
        {
            var s = chain.Start + chain.Length * i / 8;
            Near(chain.PointAt(s) + chain.DirectionAt(s).Perp * 4, Mid(f, lamps[i]), 1e-6);
            Near(chain.DirectionAt(s), Dir(f, lamps[i]), 1e-6);
        }
    }

    [Fact]
    public void A_path_deleted_leaves_its_line_and_the_array_can_still_change()
    {
        var (f, path, c) = Arched();
        var src = f.Wall(-0.5, -3, 0.5, -3);
        var arr = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(src), ["kind"] = "path", ["along"] = path, ["count"] = 3, ["base"] = new JsonArray(0, -3), ["orient"] = "fixed" })!["array"]!;
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(path) });
        var a = Array(f, arr);
        Assert.Null(a.Str("along"));
        Assert.True(a.Attrs["points"]!.AsArray().Count > 10);
        f.Run("array.update", new JsonObject { ["id"] = arr, ["count"] = 5 });
        Assert.Equal(5, Items(f, arr).Count);
        Near(c.PointAt(c.Length / 2), Mid(f, Items(f, arr)[2].Single()), 2e-3);
    }

    [Fact]
    public void A_group_is_arrayed_whole_and_explode_leaves_plain_copies()
    {
        var f = new Fixture(new ArchitectureModule());
        var a = f.Wall(0, 0, 2, 0);
        var b = f.Wall(2, 0, 2, 2);
        var g = (string)f.Run("group.create", new JsonObject { ["ids"] = Ids(a, b) })!["id"]!;
        var arr = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(g), ["count"] = 3, ["step"] = new JsonArray(5, 0, 0) })!["array"]!;
        var items = Items(f, arr);
        // Each item is its group: the members go with it.
        Assert.All(items, i => Assert.Equal(Groups.Class, f.Model.Get(i.Single())!.Class));
        Assert.Equal(3, Groups.All(f.Model).Count());
        f.Run("array.update", new JsonObject { ["id"] = arr, ["count"] = 2 });
        Assert.Equal(2, Groups.All(f.Model).Count());
        Assert.Equal(4, f.Model.OfClass(IfcClass.Wall).Count());

        var ids = f.Run("array.explode", new JsonObject { ["id"] = arr })!["ids"]!.AsArray();
        Assert.Equal(2, ids.Count);
        Assert.Empty(Arrays.All(f.Model));
        Assert.Equal(4, f.Model.OfClass(IfcClass.Wall).Count());
        Assert.Null(f.Session.Query("array.of", new JsonObject { ["id"] = items[1].Single() }));
        f.Session.Undo();
        Assert.Single(Arrays.All(f.Model));
    }

    [Fact]
    public void An_array_left_with_nothing_goes_and_none_is_written_to_IFC()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 0, 2);
        var arr = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(wall), ["count"] = 3, ["step"] = new JsonArray(2, 0, 0) })!["array"]!;
        var dir = Directory.CreateTempSubdirectory("lapin-array-").FullName;
        var file = Path.Combine(dir, "a.ifc");
        IfcExport.Write(f.Session, file);
        var text = File.ReadAllText(file);
        Assert.DoesNotContain("LapinArray", text);
        Assert.DoesNotContain("refAngle", text);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(text, "IFCWALL\\(").Count);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(f.Model.OfClass(IfcClass.Wall).Select(w => (JsonNode)w.Id).ToArray()) });
        Assert.Null(f.Model.Get(arr));
        Directory.Delete(dir, true);
    }

    [Fact]
    public void A_copy_is_made_as_element_transform_makes_one_so_a_driving_module_is_asked_with_Copy()
    {
        // The spy stands for a module that drives what it is asked about: it sees every copy the array makes.
        var spy = new Spy();
        var f = new Fixture(new ArchitectureModule(), spy);
        var wall = f.Wall(0, 0, 0, 2);
        spy.Watch = wall;
        var arr = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(wall), ["count"] = 3, ["step"] = new JsonArray(2, 0, 0) })!["array"]!;
        Assert.Equal(2, spy.Copies);
        f.Run("array.update", new JsonObject { ["id"] = arr, ["count"] = 5 });
        Assert.Equal(4, spy.Copies);
    }

    sealed class Spy : ILapinModule, IDrivenElements
    {
        public string Id => "spy";
        public string Name => "Spy";
        public void Register(IModuleRegistry r) => r.Service<IDrivenElements>(this);
        public string? Watch;
        public int Copies;
        public bool Drives(IModelView m, Entity e) => e.Id == Watch;
        public void Before(ICommandContext ctx, DrivenEdit edit, DrivenOutcome outcome)
        {
            if (edit.Command == "element.transform" && edit.Copy) Copies++;
        }
    }
}
