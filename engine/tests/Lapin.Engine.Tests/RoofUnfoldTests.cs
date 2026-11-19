using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

public class RoofUnfoldTests
{
    static JsonArray Rect10x6 =>
    [
        new JsonArray(0, 0),
        new JsonArray(10, 0),
        new JsonArray(10, 6),
        new JsonArray(0, 6),
    ];

    [Fact]
    public void Gable_roof_unfolded_slope_length_and_area_match_true_3d_geometry()
    {
        var f = new Fixture();
        var roofId = (string)f.Run("architecture.addRoof", new JsonObject
        {
            ["storeyId"] = f.Storey,
            ["points"] = Rect10x6,
            ["style"] = "dk_parcelhus",
        })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject
        {
            ["id"] = roofId,
            ["attrs"] = new JsonObject
            {
                ["pitch"] = 30,
                ["overhang"] = 0,
                ["gableOverhang"] = 0,
            },
        });

        var roof = f.Session.Model.Get(roofId)!;
        var resolved = Roof.Resolve(roof, f.Session.Model)!;
        var (shape, _) = Roof.Solve(resolved);
        Assert.NotNull(shape);

        var layout = RoofUnfold.Layout(f.Session.Model, roof);
        Assert.NotNull(layout);
        Assert.Equal(2, layout.Sheets.Count);

        var expectedSlopeLength = 3.0 / Math.Cos(30 * Math.PI / 180);
        foreach (var sheet in layout.Sheets)
        {
            Assert.Equal(10.0, sheet.Width, 4);
            Assert.Equal(expectedSlopeLength, sheet.SlopeLength, 4);
            Assert.Equal(10.0 * expectedSlopeLength, sheet.NetArea, 4);
        }

        var true3dArea = shape!.Planes.Sum(p => VecMath.NewellNormal(p.Points).Length);
        Assert.Equal(true3dArea, layout.TotalNetArea, 4);
    }

    [Fact]
    public void Hip_roof_with_opening_subtracts_hole_area()
    {
        var f = new Fixture();
        var roofId = (string)f.Run("architecture.addRoof", new JsonObject
        {
            ["storeyId"] = f.Storey,
            ["points"] = Rect10x6,
            ["shape"] = "hip",
            ["pitch"] = 30,
            ["overhang"] = 0.5,
        })!["id"]!;

        var winWidth = 0.78;
        var winLength = 1.18;
        f.Run("architecture.addRoofOpening", new JsonObject
        {
            ["roofId"] = roofId,
            ["kind"] = "window",
            ["x"] = 5.0,
            ["y"] = 1.2,
            ["width"] = winWidth,
            ["length"] = winLength,
        });

        var roof = f.Session.Model.Get(roofId)!;
        var layout = RoofUnfold.Layout(f.Session.Model, roof);
        Assert.NotNull(layout);
        Assert.Equal(4, layout.Sheets.Count);

        var withHole = layout.Sheets.Single(s => s.Holes.Count == 1);
        var expectedHoleArea = winWidth * winLength;
        Assert.Equal(expectedHoleArea, withHole.GrossArea - withHole.NetArea, 4);
        Assert.Equal(layout.TotalGrossArea - expectedHoleArea, layout.TotalNetArea, 4);
    }

    [Fact]
    public void Unfold_command_creates_idempotent_drafting_view_with_role_and_marks()
    {
        var f = new Fixture();
        var roofId = (string)f.Run("architecture.addRoof", new JsonObject
        {
            ["storeyId"] = f.Storey,
            ["points"] = Rect10x6,
            ["shape"] = "gable",
            ["pitch"] = 30,
        })!["id"]!;

        var view1 = (string)f.Run("architecture.unfoldRoofCovering", new JsonObject { ["id"] = roofId })!["id"]!;
        var view2 = (string)f.Run("architecture.unfoldRoofCovering", new JsonObject { ["id"] = roofId })!["id"]!;
        Assert.Equal(view1, view2);

        var viewEntity = f.Session.Model.Get(view1)!;
        Assert.Equal("drafting", viewEntity.Str("kind"));
        Assert.Equal(roofId, viewEntity.Str(RoofUnfold.ViewKey));

        var roles = (JsonObject)f.Session.Query("elementView.roles", new JsonObject { ["id"] = roofId })!;
        var roleDefs = ((JsonArray)roles["roles"]!).OfType<JsonObject>().ToList();
        var roleViews = ((JsonArray)roles["views"]!).OfType<JsonObject>().ToList();
        Assert.Contains(roleDefs, r => (string?)r["role"] == "roof.unfoldedCovering");
        Assert.Contains(roleViews, v => (string?)v["id"] == view1 && (string?)v["role"] == "roof.unfoldedCovering");

        var drawing = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view1 })!;
        Assert.Null(drawing["problem"]);
        Assert.NotNull(drawing["bounds"]);
        var marks = ((JsonArray)drawing["marks"]!).OfType<JsonObject>().ToList();
        Assert.Contains(marks, m => (string?)m["id"] == roofId);
        var pens = ((JsonArray)drawing["pens"]!).OfType<JsonObject>().ToList();
        Assert.NotEmpty(pens);
    }
}
