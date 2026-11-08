using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A view's level of development (decisions 401, 404): a way of viewing, never taken from the scale. LOD 100 draws a layered
/// wall as one outline hatched as its core, 200 its layers, 300 the details of its doors and windows, 350 the connections,
/// 400 a link's bars; a view takes its style's level unless it sets its own, and a project made before keeps how its views drew.
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

    static void Lod(Fixture f, string view, int? lod) => f.Run("view.setStyle", new JsonObject { ["id"] = view, ["detail"] = lod });

    /// <summary>The CUT segments the plan prints.</summary>
    static int CutSegments(Fixture f, string view) =>
        Views.Render(new Q(f), Views.Find(f.Model, view)!).Pens.Where(p => p.Layer == "CUT").Sum(p => p.Segments.Count + p.Loops.Sum(l => l.Count));

    [Fact]
    public void A_level_is_read_as_the_LOD_Specification_names_it_and_never_from_the_scale()
    {
        Assert.Equal(PlanDetail.Lod300, PlanDetail.LodOf(JsonValue.Create("lod300")));
        Assert.Equal(PlanDetail.Lod350, PlanDetail.LodOf(JsonValue.Create(350)));
        Assert.Equal(PlanDetail.Lod100, PlanDetail.LodOf(JsonValue.Create("coarse")));
        Assert.Equal(PlanDetail.Lod400, PlanDetail.LodOf(JsonValue.Create("fine")));
        Assert.Null(PlanDetail.LodOf(JsonValue.Create(250)));
        var (f, _, _, _) = House();
        // The style's level, whatever the scale: 1:50, 1:100 and 1:200 alike.
        foreach (var scale in new[] { 50.0, 100, 200 })
            Assert.Equal(PlanDetail.Lod350, ViewDetail.Of(f.Model, f.Model.Get(Plan(f, scale))!));
        var structural = Plan(f, 100);
        f.Run("view.setStyle", new JsonObject { ["id"] = structural, ["style"] = "structural" });
        Assert.Equal(PlanDetail.Lod400, ViewDetail.Of(f.Model, f.Model.Get(structural)!));
        // A style's own level; a view's over it; refused when it is not one.
        f.Run("viewStyle.update", new JsonObject { ["id"] = "structural", ["detail"] = 200 });
        Assert.Equal(PlanDetail.Lod200, ViewDetail.Of(f.Model, f.Model.Get(structural)!));
        Lod(f, structural, 300);
        Assert.Equal(PlanDetail.Lod300, ViewDetail.Of(f.Model, f.Model.Get(structural)!));
        Assert.Throws<CommandException>(() => Lod(f, structural, 250));
    }

    [Fact]
    public void LOD_100_one_outline_hatched_as_the_core_200_the_layers_300_the_sills()
    {
        var (f, wall, window, brick) = House();
        var plan = Plan(f, 50);
        string[] Keys(string view, string id) => [.. Drawing(f, view).Cuts.Where(c => c.Element == id).Select(c => c.Key ?? "").Distinct().Order()];
        string[] Materials(string view, string id) => [.. Drawing(f, view).Cuts.Where(c => c.Element == id).Select(c => c.Material ?? "").Distinct()];
        Lod(f, plan, 200);
        Assert.Equal(["layer:0", "layer:1", "layer:2"], Keys(plan, wall));
        Assert.Equal(3, Materials(plan, wall).Length);
        var medium = CutSegments(f, plan);
        Lod(f, plan, 100);
        Assert.Equal([brick], Materials(plan, wall));
        Assert.True(CutSegments(f, plan) < medium, $"LOD 100 {CutSegments(f, plan)}, 200 {medium}");
        // The window's sills, cut by a section across the wall through it, from LOD 300 (the style's 350), at any scale.
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = wall, ["station"] = 3, ["scale"] = 200 })!["id"]!;
        Assert.Contains(Keys(across, window), k => k.StartsWith("sill"));
        Lod(f, across, 200);
        Assert.DoesNotContain(Keys(across, window), k => k.StartsWith("sill"));
        // A 3D view at LOD 200 leaves the sills out as parts.
        Lod(f, "3d", 200);
        var looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject())!;
        Assert.Contains(looks[window]!["parts"]!.AsObject(), p => p.Key.StartsWith("sill"));
    }

    [Fact]
    public void A_project_made_before_keeps_how_its_views_drew()
    {
        var (f, _, _, _) = House();
        var p50 = Plan(f, 50);
        var p100 = Plan(f, 100);
        var p200 = Plan(f, 200);
        var own = Plan(f, 100);
        f.Run("view.graphics", new JsonObject { ["id"] = own, ["detailLevel"] = "coarse" });
        // As an older Lapin kept it: no mark on the project, no level on the views.
        var es = f.Model.Entities.Select(e =>
        {
            if (e.Class == IfcClass.Project) { var a = e.CloneAttrs(); a.Remove(ViewDetail.RevKey); return e.With(attrs: a); }
            if (e.Class == Views.Class) { var a = e.CloneAttrs(); a.Remove("styleOverrides"); return e.With(attrs: a); }
            return e;
        }).ToList();
        var (up, _) = f.Session.Upgraded(es, f.Model.Relations.ToList());
        int? Stored(string id) => PlanDetail.LodOf(up.Single(e => e.Id == id).Attrs["styleOverrides"]?["detail"]);
        Assert.Equal(PlanDetail.Lod400, Stored(p50));
        Assert.Equal(PlanDetail.Lod200, Stored(p100));
        Assert.Equal(PlanDetail.Lod100, Stored(p200));
        // One that set its own keeps it as it was.
        Assert.Null(Stored(own));
        var project = up.Single(e => e.Class == IfcClass.Project);
        Assert.Equal(1, (int)project.Attrs[ViewDetail.RevKey]!);
        Assert.Equal(PlanDetail.Lod400, PlanDetail.LodOf(project.Attrs["view3d"]?["styleOverrides"]?["detail"]));
        // Upgraded once: a project with the mark is left as it is.
        var (again, _) = f.Session.Upgraded(up, f.Model.Relations.ToList());
        Assert.Same(up.Single(e => e.Id == p100), again.Single(e => e.Id == p100));
        // A new project has the mark: its views take their style's level.
        Assert.NotNull(Spatial.Project(f.Model)!.Attrs[ViewDetail.RevKey]);
    }
}
