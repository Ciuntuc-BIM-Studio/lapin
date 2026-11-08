using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;

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

    [Fact]
    public void At_LOD_100_a_steel_member_is_a_single_line_on_its_axis()
    {
        // Decision 405.
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new SteelModule(), new DraftingModule());
        var member = (string)f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 5, ["z1"] = 0.5, ["x2"] = 6, ["y2"] = 5, ["z2"] = 0.5 })!["id"]!;
        var plan = Plan(f, 100);
        List<DLine> Lines() => [.. Drawing(f, plan).Lines.Where(l => l.Element == member)];
        int Cuts() => Drawing(f, plan).Cuts.Count(c => c.Element == member);
        // As its style draws it (LOD 350): its body, its outline.
        Assert.True(Lines().Count > 2);
        Lod(f, plan, 100);
        var line = Assert.Single(Lines());
        Assert.Equal(0, Cuts());
        Assert.Equal(6, (line.B - line.A).Length, 3);
        Assert.Equal(5, line.A.Y, 3);
        // Below the plan's cut: seen; in a section along it, one line too.
        Assert.Equal(LineRole.Projection, line.Role);
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, 4), ["end"] = new JsonArray(7, 4), ["scale"] = 100 })!["id"]!;
        Lod(f, section, 100);
        Assert.Single(Drawing(f, section).Lines, l => l.Element == member);
        // Seen end on (a section across it), its body draws.
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, 3), ["end"] = new JsonArray(3, 7), ["scale"] = 100 })!["id"]!;
        Lod(f, across, 100);
        Assert.Contains(Drawing(f, across).Cuts, c => c.Element == member);
    }

    [Fact]
    public void A_door_and_a_window_draw_their_opening_and_swing_at_LOD_100_simplified_at_200_whole_from_300()
    {
        // Decision 405.
        var (f, wall, window, _) = House();
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1 })!["id"]!;
        var plan = Plan(f, 50);
        int Count(string id) => Drawing(f, plan).Lines.Count(l => l.Element == id);
        var (w300, d300) = (Count(window), Count(door));
        Lod(f, plan, 200);
        var (w200, d200) = (Count(window), Count(door));
        Lod(f, plan, 100);
        var (w100, d100) = (Count(window), Count(door));
        Assert.True(w100 < w200 && w200 < w300, $"window {w100} < {w200} < {w300}");
        Assert.True(d100 < d200 && d200 <= d300, $"door {d100} < {d200} <= {d300}");
        // The door's swing stays at LOD 100: an arc of many points.
        var symbols = f.Session.Geometry(door)!.Plan!.Where(i => i.At(PlanDetail.Coarse)).ToList();
        Assert.Contains(symbols, i => !i.Closed && i.Points.Count > 4);
        // In 3D at LOD 100 a window keeps its frame and glass, its sashes go; at 200 its sashes come back.
        Lod(f, "3d", 100);
        var looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject())!;
        Assert.True((bool)looks[window]!["parts"]!["sash"]!["hidden"]!);
        Assert.Null(looks[window]!["parts"]!["frame"]);
        Lod(f, "3d", 200);
        looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject())!;
        Assert.Null(looks[window]!["parts"]!["sash"]);
    }
}
