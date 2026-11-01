using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Views on sheets cut to what they should show (decision 236): named view boundaries drawn on a view (Annotate), a viewport
/// bound to one, the same view on several sheets by its boundaries, a viewport cropped and panned on the sheet, pinned ones.
/// </summary>
public class SheetBoundaryTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static (Fixture F, string Plan, string Sheet) House()
    {
        var f = F();
        foreach (var (a, b) in new[] { ((0.0, 0.0), (10.0, 0.0)), ((10.0, 0.0), (10.0, 8.0)), ((10.0, 8.0), (0.0, 8.0)), ((0.0, 8.0), (0.0, 0.0)) })
            f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = a.Item1, ["y1"] = a.Item2, ["x2"] = b.Item1, ["y2"] = b.Item2 });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans" })!["id"]!;
        return (f, plan, sheet);
    }

    static JsonObject Drawing(Fixture f, string sheet) => (JsonObject)f.Session.Query("sheet.drawing", new JsonObject { ["id"] = sheet })!;
    static JsonObject Vp(Fixture f, string sheet, int i = 0) => (JsonObject)Drawing(f, sheet)["viewports"]![i]!;
    static double N(JsonNode? n) => (double)n!;

    [Fact]
    public void A_view_boundary_has_a_name_of_its_own_and_is_drawn_on_the_screen_never_printed()
    {
        var (f, plan, _) = House();
        var a = (JsonObject)f.Run("view.addBoundary", new JsonObject { ["viewId"] = plan, ["a"] = new JsonArray(-1, -1), ["b"] = new JsonArray(5, 9) })!;
        Assert.Equal("Boundary 1", (string?)a["name"]);
        var b = (string)f.Run("view.addBoundary", new JsonObject { ["viewId"] = plan, ["a"] = new JsonArray(5, -1), ["b"] = new JsonArray(11, 9), ["name"] = "East wing" })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("view.addBoundary", new JsonObject { ["viewId"] = plan, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(1, 1), ["name"] = "east WING" }));
        Assert.Throws<CommandException>(() => f.Run("view.updateBoundary", new JsonObject { ["id"] = b, ["name"] = "Boundary 1" }));
        Assert.Throws<CommandException>(() => f.Run("view.addBoundary", new JsonObject { ["viewId"] = plan, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(0.01, 3) }));
        // On the screen: dashed, with its name; for print: not there.
        string[] Texts(bool print) => [.. Views.Render(new ViewQuery(f), f.Model.Get(plan)!, print: print).Pens.SelectMany(p => p.Texts.Select(t => t.Text))];
        Assert.Contains("East wing", Texts(false));
        Assert.DoesNotContain("East wing", Texts(true));
    }

    [Fact]
    public void A_viewport_bound_to_a_boundary_shows_exactly_it_and_the_view_goes_on_another_sheet_by_another()
    {
        var (f, plan, sheet) = House();
        var west = (string)f.Run("view.addBoundary", new JsonObject { ["viewId"] = plan, ["a"] = new JsonArray(-1, -1), ["b"] = new JsonArray(5, 9), ["name"] = "West" })!["id"]!;
        var east = (string)f.Run("view.addBoundary", new JsonObject { ["viewId"] = plan, ["a"] = new JsonArray(5, -1), ["b"] = new JsonArray(11, 9), ["name"] = "East" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = plan, ["boundaryId"] = west, ["x"] = 200, ["y"] = 200 });
        // 6 × 10 m at 1:50: 120 × 200 mm, round its middle (2, 4).
        var vp = Vp(f, sheet);
        Assert.Equal((120.0, 200.0), (Math.Round(N(vp["x1"]) - N(vp["x0"]), 6), Math.Round(N(vp["y1"]) - N(vp["y0"]), 6)));
        Assert.Equal((2.0, 4.0, "West"), (N(vp["cx"]), N(vp["cy"]), (string?)vp["boundaryName"]));
        Assert.Contains(Drawing(f, sheet)["pens"]!.AsArray(), p => p!["texts"] is JsonArray ts && ts.Any(t => (string?)t!["text"] == $"{f.Model.Get(plan)!.Name} · West"));
        // The same boundary twice is refused; the other boundary goes on another sheet.
        var other = (string)f.Run("sheet.create", new JsonObject { ["name"] = "East" })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("sheet.addViewport", new JsonObject { ["id"] = other, ["viewId"] = plan, ["boundaryId"] = west }));
        f.Run("sheet.addViewport", new JsonObject { ["id"] = other, ["viewId"] = plan, ["boundaryId"] = east });
        Assert.Equal(8.0, N(Vp(f, other)["cx"]));
        // Resized in the view, the viewport follows.
        f.Run("view.updateBoundary", new JsonObject { ["id"] = west, ["max"] = new JsonArray(4, 9) });
        vp = Vp(f, sheet);
        Assert.Equal(100.0, Math.Round(N(vp["x1"]) - N(vp["x0"]), 6));
        // Set free of it, the viewport keeps its box on the paper.
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = (string)vp["id"]!, ["boundary"] = null });
        var free = Vp(f, sheet);
        Assert.Null(free["boundary"]);
        Assert.Equal((N(vp["x0"]), N(vp["x1"]), N(vp["cx"])), (N(free["x0"]), N(free["x1"]), N(free["cx"])));
    }

    [Fact]
    public void Cropped_on_the_sheet_the_drawing_stays_where_it_is_and_a_bound_one_resizes_its_boundary()
    {
        var (f, plan, sheet) = House();
        var vpId = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = plan, ["x"] = 300, ["y"] = 250 })!["viewportId"]!;
        var before = Vp(f, sheet);
        // The right edge pulled in by 40 mm, the top down by 20: the drawing point under any paper point is the same.
        f.Run("sheet.cropViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["x0"] = N(before["x0"]), ["y0"] = N(before["y0"]) + 20, ["x1"] = N(before["x1"]) - 40, ["y1"] = N(before["y1"]) });
        var after = Vp(f, sheet);
        Assert.Equal(N(before["x1"]) - 40, N(after["x1"]), 6);
        Vec2 Model(JsonObject v, double px, double py) => new(N(v["cx"]) + (px - (N(v["x0"]) + N(v["x1"])) / 2) / 20, N(v["cy"]) - (py - (N(v["y0"]) + N(v["y1"])) / 2) / 20);
        var p = (N(before["x0"]) + 10, N(before["y1"]) - 10);
        Assert.Equal(Model(before, p.Item1, p.Item2).X, Model(after, p.Item1, p.Item2).X, 9);
        Assert.Equal(Model(before, p.Item1, p.Item2).Y, Model(after, p.Item1, p.Item2).Y, 9);
        // Panned: the box stays, the drawing slides 20 mm right (1 m at 1:50).
        f.Run("sheet.panViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["dx"] = 20, ["dy"] = 0 });
        var panned = Vp(f, sheet);
        Assert.Equal(N(after["x0"]), N(panned["x0"]), 9);
        Assert.Equal(N(after["cx"]) - 1, N(panned["cx"]), 9);

        // Bound: cropping resizes the boundary in the view.
        var b = (string)f.Run("view.addBoundary", new JsonObject { ["viewId"] = plan, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(4, 4) })!["id"]!;
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["boundary"] = b });
        var bound = Vp(f, sheet);
        f.Run("sheet.cropViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["x0"] = N(bound["x0"]), ["y0"] = N(bound["y0"]), ["x1"] = N(bound["x1"]) + 20, ["y1"] = N(bound["y1"]) });
        var (min, max) = ViewBoundaries.Box(f.Model.Get(b)!);
        Assert.Equal((0.0, 0.0, 5.0, 4.0), (Math.Round(min.X, 6), Math.Round(min.Y, 6), Math.Round(max.X, 6), Math.Round(max.Y, 6)));
    }

    [Fact]
    public void A_pinned_viewport_stays_put_until_unpinned()
    {
        var (f, plan, sheet) = House();
        var vpId = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = plan, ["x"] = 300, ["y"] = 250 })!["viewportId"]!;
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["pinned"] = true });
        Assert.True((bool)Vp(f, sheet)["pinned"]!);
        Assert.Throws<CommandException>(() => f.Run("sheet.updateViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["x"] = 100 }));
        Assert.Throws<CommandException>(() => f.Run("sheet.cropViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["x0"] = 250 }));
        Assert.Throws<CommandException>(() => f.Run("sheet.panViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["dx"] = 5 }));
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["pinned"] = false });
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["x"] = 100 });
        Assert.Equal(100.0, (N(Vp(f, sheet)["x0"]) + N(Vp(f, sheet)["x1"])) / 2, 6);
    }

    sealed class ViewQuery(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
