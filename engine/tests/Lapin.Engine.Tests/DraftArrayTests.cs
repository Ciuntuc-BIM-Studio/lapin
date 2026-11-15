using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Modify → Array on drafting (forum 90): a drafted rectangle, a text or a symbol on a plan, a section or a drafting view
/// arrayed in a row, a grid, round a centre or along a path by the same element.array as the model's elements, kept as an
/// arrangement (array.update, array.explode), the view's own points carried; one undo step each.
/// </summary>
public class DraftArrayTests
{
    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());
    static void Near(Vec2 want, Vec2 got, double tol = 1e-6) => Assert.True((want - got).Length < tol, $"want {want}, got {got}");

    static string Rect(Fixture f, string view, double x, double y, double w, double h) =>
        (string)f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = view,
            ["shapes"] = new JsonArray(new JsonObject
            {
                ["type"] = "polyline", ["closed"] = true,
                ["pts"] = new JsonArray(new JsonArray(x, y), new JsonArray(x + w, y), new JsonArray(x + w, y + h), new JsonArray(x, y + h)),
            }),
        })!["ids"]![0]!;

    static Vec2 Corner(Fixture f, string id) => Drafts.ShapeOf(f.Model.Get(id)!) is Polyline p ? p.Pts[0] : throw new Exception("not a polyline");
    static List<string> Drafted(Fixture f, string view) => Drafts.On(f.Model, view).Select(e => e.Id).ToList();
    static List<string> ItemIds(Fixture f, string arr) => Arrays.Items(f.Model.Get(arr)!).Select(i => i.Ids.Single()).ToList();

    [Fact]
    public void A_drafted_rectangle_arrayed_in_a_row_is_kept_as_an_arrangement_and_undone_in_one_step()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var rect = Rect(f, plan, 0, 0, 1, 0.5);
        var r = f.Run("element.array", new JsonObject { ["ids"] = Ids(rect), ["count"] = 4, ["step"] = new JsonArray(2, 0, 0) })!;
        var arr = (string)r["array"]!;
        Assert.Equal(3, r["ids"]!.AsArray().Count);
        Assert.Equal(4, Drafted(f, plan).Count);
        var items = ItemIds(f, arr);
        Assert.Equal(rect, items[0]);
        for (var i = 0; i < 4; i++)
        {
            Near(new Vec2(2 * i, 0), Corner(f, items[i]));
            // Each copy is a drafted shape of the same view, on the same layer.
            Assert.Equal(plan, f.Model.Get(items[i])!.Str("viewId"));
            Assert.Equal(Drafts.Predefined, f.Model.Get(items[i])!.PredefinedType);
        }

        // Editable like a model array: more copies keep the first copies' ids; a new step moves them.
        f.Run("array.update", new JsonObject { ["id"] = arr, ["count"] = 5, ["step"] = new JsonArray(3, 1, 0) });
        var now = ItemIds(f, arr);
        Assert.Equal(items, now.Take(4));
        Near(new Vec2(12, 4), Corner(f, now[4]));
        Near(new Vec2(3, 1), Corner(f, now[1]));

        f.Session.Undo();
        Assert.Equal(4, Drafted(f, plan).Count);
        Near(new Vec2(2, 0), Corner(f, items[1]));
        f.Session.Undo();
        Assert.Equal([rect], Drafted(f, plan));
        Assert.Empty(Arrays.All(f.Model));
        Near(new Vec2(0, 0), Corner(f, rect));
    }

    [Fact]
    public void A_grid_a_polar_and_a_path_array_of_drafting_place_the_view_s_points()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var rect = Rect(f, plan, 0, 0, 1, 1);
        var grid = (string)f.Run("element.array", new JsonObject
        {
            ["ids"] = Ids(rect), ["kind"] = "grid", ["count"] = 3, ["rows"] = 2, ["step"] = new JsonArray(2, 0, 0), ["rowStep"] = new JsonArray(0, 3, 0),
        })!["array"]!;
        Assert.Equal(6, Drafted(f, plan).Count);
        var cell = Arrays.Items(f.Model.Get(grid)!).Single(i => i.Cell is [2, 1, 0]).Ids.Single();
        Near(new Vec2(4, 3), Corner(f, cell));
        f.Run("array.explode", new JsonObject { ["id"] = grid });
        Assert.Empty(Arrays.All(f.Model));
        Assert.Equal(6, Drafted(f, plan).Count);

        // Round a centre: the text turns with its place.
        var text = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = plan, ["at"] = new JsonArray(10, 0), ["text"] = "A" })!["id"]!;
        var polar = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(text), ["kind"] = "polar", ["count"] = 4, ["angle"] = 360, ["center"] = new JsonArray(0, 0) })!["array"]!;
        var texts = ItemIds(f, polar);
        Assert.Equal(4, texts.Count);
        Near(new Vec2(0, 10), Args.PointOf(f.Model.Get(texts[1])!.Attrs["at"]));
        Assert.Equal(90, Json.Number(f.Model.Get(texts[1])!.Attrs["angle"]) ?? 0, 6);
        Assert.Equal(plan, f.Model.Get(texts[2])!.Str("viewId"));

        // Along a drafted line of the same view.
        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 20), ["b"] = new JsonArray(8, 20) }) })!["ids"]![0]!;
        var small = Rect(f, plan, 0, 20, 0.2, 0.2);
        var along = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(small), ["kind"] = "path", ["along"] = line, ["count"] = 5, ["base"] = new JsonArray(0, 20) })!["array"]!;
        var places = ItemIds(f, along);
        for (var i = 0; i < 5; i++) Near(new Vec2(2 * i, 20), Corner(f, places[i]));
        // The line edited: the copies follow along it.
        f.Run("drafting.updateDraft", new JsonObject { ["ids"] = Ids(line), ["shape"] = new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 20), ["b"] = new JsonArray(16, 20) } });
        Near(new Vec2(16, 20), Corner(f, ItemIds(f, along)[4]));
    }

    [Fact]
    public void In_a_section_drafting_is_arrayed_in_the_view_s_own_coordinates()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var s = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 2) })!["id"]!;
        var rect = Rect(f, s, 0, 0, 0.5, 0.5);
        var arr = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(rect), ["count"] = 3, ["step"] = new JsonArray(0, 1, 0) })!["array"]!;
        var items = ItemIds(f, arr);
        Near(new Vec2(0, 2), Corner(f, items[2]));
        Assert.All(items, id => Assert.Equal(s, f.Model.Get(id)!.Str("viewId")));
    }

    [Fact]
    public void A_mixed_selection_in_a_plan_arrays_the_wall_and_the_drafted_shape_together()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var wall = f.Wall(0, 0, 0, 2);
        var rect = Rect(f, plan, 1, 0, 1, 1);
        var arr = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(wall, rect), ["count"] = 3, ["step"] = new JsonArray(4, 0, 0) })!["array"]!;
        var items = Arrays.Items(f.Model.Get(arr)!);
        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.Equal(2, i.Ids.Count));
        Assert.Equal(3, f.Model.OfClass(IfcClass.Wall).Count());
        Assert.Equal(3, Drafted(f, plan).Count);
        Near(new Vec2(9, 0), Corner(f, items[2].Ids.Single(id => f.Model.Get(id)!.Class == IfcClass.Annotation)));
        f.Session.Undo();
        Assert.Single(f.Model.OfClass(IfcClass.Wall));
        Assert.Single(Drafted(f, plan));
    }
}
