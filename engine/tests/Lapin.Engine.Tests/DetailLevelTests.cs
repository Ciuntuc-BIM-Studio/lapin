using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A view's detail level as in Revit (decision 401): coarse draws a layered wall as one outline hatched as its core,
/// medium its layers, fine its layers and the details of its doors and windows; by the scale when the view sets none
/// (fine at 1:50, medium at 1:100, coarse at 1:200).
/// </summary>
public class DetailLevelTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static (Fixture F, string Wall, string Window, string Brick) House()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var type = f.Type("Rendered", ("Render", 0.02, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
        var wall = f.Wall(0, 0, 6, 0, type);
        var details = new JoineryDetails(SillInside: new JoinerySill(true, 0.04, 0.03, 0.02, 0, "Timber"), SillOutside: new JoinerySill(true, 0.05, 0.02, 0.03, 10, "Render"));
        var typeId = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultWindow("Dressed") with { Details = details }).ToJson() })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["typeId"] = typeId, ["sill"] = 0.9 })!["id"]!;
        return (f, wall, window, Materials.Find(f.Model, "Brick")!.Id);
    }

    static string Plan(Fixture f, double scale) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = scale })!["id"]!;

    static ViewDrawing Drawing(Fixture f, string view)
    {
        var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, view)!);
        Assert.True(d != null, problem);
        return d!;
    }

    /// <summary>The CUT segments the plan prints.</summary>
    static int CutSegments(Fixture f, string view) =>
        Views.Render(new Q(f), Views.Find(f.Model, view)!).Pens.Where(p => p.Layer == "CUT").Sum(p => p.Segments.Count + p.Loops.Sum(l => l.Count));

    [Fact]
    public void The_scale_says_the_level_when_the_view_sets_none()
    {
        Assert.Equal(PlanDetail.Fine, PlanDetail.OfBuilding(20));
        Assert.Equal(PlanDetail.Fine, PlanDetail.OfBuilding(50));
        Assert.Equal(PlanDetail.Medium, PlanDetail.OfBuilding(75));
        Assert.Equal(PlanDetail.Medium, PlanDetail.OfBuilding(100));
        Assert.Equal(PlanDetail.Coarse, PlanDetail.OfBuilding(200));
        Assert.Equal(PlanDetail.Coarse, PlanDetail.OfBuilding(500));
        // The site's symbols keep their own scales (a track's rails at 1:500).
        Assert.Equal(PlanDetail.Fine, PlanDetail.Of(200));
        Assert.Equal(PlanDetail.Medium, PlanDetail.Of(500));
    }

    [Fact]
    public void Fine_draws_the_sills_medium_the_layers_coarse_one_outline_hatched_as_the_core()
    {
        var (f, wall, window, brick) = House();
        var fine = Plan(f, 50);
        var medium = Plan(f, 100);
        var coarse = Plan(f, 200);
        string[] Keys(string view, string id) => [.. Drawing(f, view).Cuts.Where(c => c.Element == id).Select(c => c.Key ?? "").Distinct().Order()];
        string[] Materials(string view, string id) => [.. Drawing(f, view).Cuts.Where(c => c.Element == id).Select(c => c.Material ?? "").Distinct()];
        // The wall's three layers at every level; its materials one at coarse, the core's.
        Assert.Equal(["layer:0", "layer:1", "layer:2"], Keys(fine, wall));
        Assert.Equal(["layer:0", "layer:1", "layer:2"], Keys(medium, wall));
        Assert.Equal(3, Materials(medium, wall).Length);
        Assert.Equal([brick], Materials(coarse, wall));
        // The window's sills, cut by a section across the wall through it, only at fine.
        string Across(double scale) => (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = wall, ["station"] = 3, ["scale"] = scale })!["id"]!;
        var (s50, s100, s200) = (Across(50), Across(100), Across(200));
        Assert.Contains(Keys(s50, window), k => k.StartsWith("sill"));
        Assert.DoesNotContain(Keys(s100, window), k => k.StartsWith("sill"));
        Assert.DoesNotContain(Keys(s200, window), k => k.StartsWith("sill"));
        // Coarse prints no line between the layers: fewer cut lines than medium for the same wall.
        Assert.True(CutSegments(f, coarse) < CutSegments(f, medium), $"coarse {CutSegments(f, coarse)}, medium {CutSegments(f, medium)}");
        // The view's own level over its scale: a 1:200 set to fine draws the sills and the layers' lines again.
        f.Run("view.graphics", new JsonObject { ["id"] = coarse, ["detailLevel"] = "fine" });
        f.Run("view.graphics", new JsonObject { ["id"] = s200, ["detailLevel"] = "fine" });
        Assert.Contains(Keys(s200, window), k => k.StartsWith("sill"));
        Assert.Equal(3, Materials(coarse, wall).Length);
        // And a 3D view set to medium leaves the window's sills out as parts.
        f.Run("view.graphics", new JsonObject { ["id"] = "3d", ["detailLevel"] = "medium" });
        var looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject())!;
        Assert.Contains(looks[window]!["parts"]!.AsObject(), p => p.Key.StartsWith("sill"));
        Assert.False(looks.ContainsKey(wall) && looks[wall]!["parts"] is JsonObject wp && wp.Any(p => p.Key.StartsWith("layer:")));
    }
}
