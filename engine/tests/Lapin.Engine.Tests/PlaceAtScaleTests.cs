using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The forum's topic #31 (decision 547): a view on a sheet placed again at another scale beside itself, as a detail of it: the
/// view duplicated with its annotations, the scale set, cropped to the part drawn on the viewport (or its box as it is), on the
/// same sheet beside the original; one undo step.
/// </summary>
public class PlaceAtScaleTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static (Fixture F, string Plan, string Sheet, string Vp) House()
    {
        var f = F();
        foreach (var (a, b) in new[] { ((0.0, 0.0), (10.0, 0.0)), ((10.0, 0.0), (10.0, 8.0)), ((10.0, 8.0), (0.0, 8.0)), ((0.0, 8.0), (0.0, 0.0)) })
            f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = a.Item1, ["y1"] = a.Item2, ["x2"] = b.Item1, ["y2"] = b.Item2 });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 100, ["name"] = "Ground floor" })!["id"]!;
        f.Run("drafting.addText", new JsonObject { ["viewId"] = plan, ["at"] = new JsonArray(1, 1), ["text"] = "Corner" });
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A1" })!["id"]!;
        var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = plan, ["x"] = 200, ["y"] = 250 })!["viewportId"]!;
        return (f, plan, sheet, vp);
    }

    static JsonObject Drawing(Fixture f, string sheet) => (JsonObject)f.Session.Query("sheet.drawing", new JsonObject { ["id"] = sheet })!;
    static JsonObject Vp(Fixture f, string sheet, string id) => (JsonObject)Drawing(f, sheet)["viewports"]!.AsArray().First(v => (string?)v!["id"] == id)!;
    static double N(JsonNode? n) => (double)n!;

    [Fact]
    public void A_part_drawn_on_a_viewport_goes_beside_it_at_the_scale_asked_with_its_annotations()
    {
        var (f, plan, sheet, vpId) = House();
        var src = Vp(f, sheet, vpId);
        // Round (2, 2): a 30 × 20 mm rectangle on the 1:100 viewport (3 × 2 m), at 1:25 a 120 × 80 mm box.
        Vec(f, sheet, src, 2, 2, out var px, out var py);
        var r = f.Run("sheet.placeAtScale", new JsonObject
        {
            ["id"] = sheet, ["viewportId"] = vpId, ["scale"] = 25,
            ["x0"] = px - 15, ["y0"] = py - 10, ["x1"] = px + 15, ["y1"] = py + 10,
        })!;
        var copyId = (string)r["viewId"]!;
        var copy = f.Model.Get(copyId)!;
        Assert.Equal("Ground floor 1:25", copy.Name);
        Assert.Equal(25, copy.Num("scale"));
        Assert.Equal("plan", copy.Str("kind"));
        Assert.Equal(1, f.Model.OfClass(IfcClass.Annotation).Count(x => x.Str("viewId") == copyId));
        // The original as it was.
        Assert.Equal(100, f.Model.Get(plan)!.Num("scale"));
        var detail = Vp(f, sheet, (string)r["viewportId"]!);
        Assert.Equal(120, N(detail["x1"]) - N(detail["x0"]), 6);
        Assert.Equal(80, N(detail["y1"]) - N(detail["y0"]), 6);
        Assert.Equal((2.0, 2.0), (Math.Round(N(detail["cx"]), 6), Math.Round(N(detail["cy"]), 6)));
        Assert.Equal(25, N(detail["scale"]));
        // Beside it, on its right, clear of it.
        Assert.True(N(detail["x0"]) >= N(src["x1"]) + 9.999, $"{N(detail["x0"])} after {N(src["x1"])}");
        Assert.Equal(N(src["y0"]) + N(src["y1"]), N(detail["y0"]) + N(detail["y1"]), 6);
        // One undo step takes it all back.
        f.Session.Undo();
        Assert.Null(f.Model.Get(copyId));
        Assert.Single(Drawing(f, sheet)["viewports"]!.AsArray());
        Assert.Single(f.Model.OfClass(IfcClass.Annotation));
    }

    [Fact]
    public void Without_a_part_it_takes_the_viewport_as_cropped_and_a_view_is_found_by_its_sheet()
    {
        var (f, plan, sheet, vpId) = House();
        var src = Vp(f, sheet, vpId);
        f.Run("sheet.cropViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["x0"] = N(src["x0"]), ["y0"] = N(src["y0"]), ["x1"] = N(src["x0"]) + 50, ["y1"] = N(src["y0"]) + 40 });
        var cropped = Vp(f, sheet, vpId);
        // By the view's id (the project browser): the sheet it is on.
        var r = f.Run("sheet.placeAtScale", new JsonObject { ["viewId"] = plan, ["scale"] = 50 })!;
        Assert.Equal(sheet, (string)r["sheetId"]!);
        var detail = Vp(f, sheet, (string)r["viewportId"]!);
        Assert.Equal(100, N(detail["x1"]) - N(detail["x0"]), 6);
        Assert.Equal(80, N(detail["y1"]) - N(detail["y0"]), 6);
        Assert.Equal(N(cropped["cx"]), N(detail["cx"]), 6);
        Assert.Equal(N(cropped["cy"]), N(detail["cy"]), 6);
        // Again: the name taken, a copy's; a view on no sheet, a part too small or a scale out of range is refused.
        var again = (string)f.Run("sheet.placeAtScale", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["scale"] = 50 })!["viewId"]!;
        Assert.Equal("Ground floor 1:50 copy", f.Model.Get(again)!.Name);
        var loose = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("sheet.placeAtScale", new JsonObject { ["viewId"] = loose, ["scale"] = 20 }));
        Assert.Throws<CommandException>(() => f.Run("sheet.placeAtScale", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["scale"] = 100, ["x0"] = N(cropped["x0"]), ["y0"] = N(cropped["y0"]), ["x1"] = N(cropped["x0"]) + 5, ["y1"] = N(cropped["y0"]) + 5 }));
        Assert.Throws<CommandException>(() => f.Run("sheet.placeAtScale", new JsonObject { ["id"] = sheet, ["viewportId"] = vpId, ["scale"] = 0 }));
    }

    /// <summary>Where a point of the view's drawing lies on the paper, in the viewport as placed (y down on the paper).</summary>
    static void Vec(Fixture f, string sheet, JsonObject vp, double x, double y, out double px, out double py)
    {
        var k = 1000 / N(vp["scale"]);
        px = (N(vp["x0"]) + N(vp["x1"])) / 2 + (x - N(vp["cx"])) * k;
        py = (N(vp["y0"]) + N(vp["y1"])) / 2 - (y - N(vp["cy"])) * k;
    }
}
