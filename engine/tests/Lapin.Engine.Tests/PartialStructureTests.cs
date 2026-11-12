using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The partial structure display (decision 500, as ArchiCAD's): one choice per view, from its view style for a view of its
/// kind or its own, of the entire model, the model without its finishes, the cores only, or the cores of the load-bearing
/// elements only. A new project's plans draw without the finishes (the plasters gone, the insulation kept) and its sections
/// the entire model; a project made before draws as it drew. Visibility / Graphics resolves it for the drawings, the sheets
/// and the exports; quantities stay the model's.
/// </summary>
public sealed class PartialStructureTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static List<string> Keys(Fixture f, string view, string element)
    {
        var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, view)!);
        Assert.True(d != null, problem);
        return d!.Cuts.Where(c => c.Element == element).Select(c => c.Key ?? "").Distinct().Order().ToList();
    }

    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static string Section(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(2.5, -1), ["end"] = new JsonArray(2.5, 1) })!["id"]!;

    /// <summary>An insulated wall along x from 0 to 5: render 10, mineral wool 100 (category Insulation), brick 250 (the core), plaster 15.</summary>
    static (Fixture F, string Wall) Insulated()
    {
        var f = new Fixture();
        var type = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Insulated brick",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Render", ["thickness"] = 0.01 },
                new JsonObject { ["material"] = "Mineral wool", ["thickness"] = 0.1, ["category"] = "Insulation" },
                new JsonObject { ["material"] = "Brick", ["thickness"] = 0.25, ["core"] = true },
                new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.015 }),
        })!["id"]!;
        return (f, f.Wall(0, 0, 5, 0, type));
    }

    /// <summary>Pset_WallCommon.LoadBearing set as the properties set it; the analytical model reads the same.</summary>
    static void Bears(Fixture f, string id, bool on) => f.Run("element.setParam", new JsonObject { ["id"] = id, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = on });

    [Fact]
    public void A_new_projects_plans_leave_out_the_finishes_and_its_sections_draw_the_entire_model()
    {
        var (f, wall) = Insulated();
        var plan = Plan(f);
        var section = Section(f);
        // The plan: the wool and the brick, the render and the plaster left out; the section: all four.
        Assert.Equal(["layer:1", "layer:2"], Keys(f, plan, wall));
        Assert.Equal(["layer:0", "layer:1", "layer:2", "layer:3"], Keys(f, section, wall));
        Assert.Equal("noFinishes", ViewStyles.Of(f.Model, Views.Find(f.Model, plan)!).Layers);
        Assert.Equal("all", ViewStyles.Of(f.Model, Views.Find(f.Model, section)!).Layers);
        // A level's default plan reads as a plan; a 3D view as the style's other views, the entire model.
        Assert.Equal("noFinishes", ViewStyles.Of(f.Model, Views.Find(f.Model, "plan:" + f.Storey)!).Layers);
        Assert.Equal("all", ViewStyles.Of(f.Model, GraphicsSets.As3dView(f.Model)).Layers);
        // Visibility / Graphics says the same of each part: the drawings, sheets and exports ask it.
        var vg = VisibilityGraphics.Of(f.Model, Views.Find(f.Model, plan)!);
        Assert.Equal("noFinishes", vg.Structure);
        Assert.True(vg.HidesPart(f.Model.Get(wall)!, "layer:3"));
        Assert.False(vg.HidesPart(f.Model.Get(wall)!, "layer:1"));
        Assert.False(VisibilityGraphics.Of(f.Model, Views.Find(f.Model, section)!).HidesPart(f.Model.Get(wall)!, "layer:3"));
        // The model is the same: the plaster's quantities as before.
        Assert.Contains(f.Session.Quantities(wall), q => q.Part == "layer:3" && q.Quantities.Single(x => x.Name == "NetVolume").Value > 0);
    }

    [Fact]
    public void Each_preset_draws_its_layers()
    {
        var (f, wall) = Insulated();
        var plan = Plan(f);
        void Set(string mode) => f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["layers"] = mode });
        Set("all");
        Assert.Equal(["layer:0", "layer:1", "layer:2", "layer:3"], Keys(f, plan, wall));
        Set("noFinishes");
        Assert.Equal(["layer:1", "layer:2"], Keys(f, plan, wall));
        // The core only: the insulation goes too.
        Set("core");
        Assert.Equal(["layer:2"], Keys(f, plan, wall));
        // The cores of the load-bearing elements only: a wall said to bear load keeps its core, one that does not is not drawn.
        Set("coreLoadBearing");
        Assert.Empty(Keys(f, plan, wall));
        Bears(f, wall, true);
        Assert.Equal(["layer:2"], Keys(f, plan, wall));
        Assert.Throws<CommandException>(() => Set("partial"));
    }

    [Fact]
    public void The_cores_of_load_bearing_elements_only_leave_out_what_bears_no_load()
    {
        var f = new Fixture();
        var type = f.Type("Plastered brick", ("Plaster", 0.015, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
        var bearing = f.Wall(0, 0, 5, 0, type);
        var partition = f.Wall(0, 3, 5, 3, type);
        var unsaid = f.Wall(0, 6, 5, 6, type);
        Bears(f, bearing, true);
        Bears(f, partition, false);
        var plan = Plan(f);
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["layers"] = "coreLoadBearing" });
        Assert.Equal(["layer:1"], Keys(f, plan, bearing));
        Assert.Empty(Keys(f, plan, partition));
        // A wall that says nothing bears none (Pset_WallCommon.LoadBearing unset), as the analytical model takes it.
        Assert.Empty(Keys(f, plan, unsaid));
        var vg = VisibilityGraphics.Of(f.Model, Views.Find(f.Model, plan)!);
        Assert.True(vg.Hides(f.Model.Get(partition)!));
        Assert.False(vg.Hides(f.Model.Get(bearing)!));
        // Columns, beams, footings, floors and roofs bear load unless they say they do not; a covering never does; a door is no matter of it.
        Assert.True(ViewStyles.BearsLoad(f.Model, new Entity("c", IfcClass.Column, "C", [])));
        Assert.True(ViewStyles.BearsLoad(f.Model, new Entity("s", IfcClass.Slab, "S", [])));
        Assert.False(ViewStyles.BearsLoad(f.Model, new Entity("s2", IfcClass.Slab, "S", new JsonObject { ["loadBearing"] = false })));
        Assert.False(ViewStyles.BearsLoad(f.Model, new Entity("k", "IfcCovering", "K", [])));
        Assert.True(ViewStyles.BearsLoad(f.Model, new Entity("d", IfcClass.Door, "D", [])));
        // The 3D marks every part of a partition hidden by the mode, so a 3D view or its export set to it leaves it out.
        var bands = new Dictionary<string, IReadOnlyList<LayerBand>?>();
        var g = f.Session.Geometry(partition)!;
        Assert.All(g.Parts, p => Assert.Contains("coreLoadBearing", ViewStyles.HiddenBy(f.Model, partition, p, bands) ?? []));
        Assert.DoesNotContain("coreLoadBearing", ViewStyles.HiddenBy(f.Model, bearing, f.Session.Geometry(bearing)!.Parts.Single(p => p.Key == "layer:1"), bands) ?? []);
    }

    [Fact]
    public void A_style_says_it_for_its_plans_its_sections_and_its_other_views_and_a_view_sets_its_own()
    {
        var (f, wall) = Insulated();
        var plan = Plan(f);
        var section = Section(f);
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["layersPlan"] = "core", ["layersSection"] = "noFinishes" });
        Assert.Equal(["layer:2"], Keys(f, plan, wall));
        Assert.Equal(["layer:1", "layer:2"], Keys(f, section, wall));
        var s = ViewStyles.All(f.Model).Single(x => x.Id == "architectural");
        Assert.Equal(("core", "noFinishes", "all"), (s.LayersFor("plan"), s.LayersFor("elevation"), s.LayersFor("3d")));
        Assert.Equal("core", (string)s.ToJson()["layersPlan"]!);
        // Unset, the plans take the style's layers again.
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["layersPlan"] = null });
        Assert.Equal(["layer:0", "layer:1", "layer:2", "layer:3"], Keys(f, plan, wall));
        // A view's own wins, in one undo step, and goes back to its style's.
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["layers"] = "noFinishes" });
        Assert.Equal(["layer:1", "layer:2"], Keys(f, plan, wall));
        f.Session.Undo();
        Assert.Equal(["layer:0", "layer:1", "layer:2", "layer:3"], Keys(f, plan, wall));
        // A level's default plan given one from its corner becomes the level's plan view with it, one step.
        var other = new Fixture();
        var made = (string)other.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = other.Storey, ["layers"] = "core" })!["id"]!;
        Assert.Equal("core", ViewStyles.Of(other.Model, other.Model.Get(made)!).Layers);
        Assert.Throws<CommandException>(() => other.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = other.Storey, ["layers"] = "half" }));
    }

    [Fact]
    public void A_plan_rules_what_its_cut_plane_cuts_and_sees_its_floors_as_they_are()
    {
        var f = new Fixture();
        // Parquet 15 (category Finish), screed 50, concrete 150 (the core), its top on the level.
        var type = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Floor",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Parquet", ["thickness"] = 0.015, ["category"] = "Finish" },
                new JsonObject { ["material"] = "Screed", ["thickness"] = 0.05 },
                new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.15, ["core"] = true }),
        })!["id"]!;
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["typeId"] = type,
            ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(4, 0), new JsonArray(4, 4), new JsonArray(0, 4)),
        })!["id"]!;
        var plan = Plan(f);
        // Seen under the cut, the floor shows as it is: its parquet on top, though the plan leaves the finishes out of what it cuts.
        List<DLine> Seen()
        {
            var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, plan)!);
            Assert.True(d != null, problem);
            return d!.Lines.Where(l => l.Element == slab).ToList();
        }
        var without = Seen();
        Assert.NotEmpty(without);
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["layers"] = "all" });
        Assert.Equal(Seen().Count, without.Count);
        // A section without the finishes cuts the floor without its parquet.
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(2, -1), ["end"] = new JsonArray(2, 5) })!["id"]!;
        f.Run("view.setStyle", new JsonObject { ["id"] = section, ["layers"] = "noFinishes" });
        Assert.Equal(["layer:1", "layer:2"], Keys(f, section, slab));
    }

    [Fact]
    public void The_drawing_tells_the_interface_which_layers_it_leaves_out()
    {
        var (f, _) = Insulated();
        var plan = Plan(f);
        JsonObject Drawn() => Views.Drawing(new Q(f), Views.Find(f.Model, plan)!);
        Assert.Equal("noFinishes", (string)Drawn()["structure"]!);
        Assert.Equal(["finishes"], ((JsonArray)Drawn()["hiddenSubs"]!).Select(x => (string)x!));
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["layers"] = "core" });
        Assert.Equal(["finishes", "insulation", "membranes"], ((JsonArray)Drawn()["hiddenSubs"]!).Select(x => (string)x!));
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["layers"] = "all" });
        Assert.Empty((JsonArray)Drawn()["hiddenSubs"]!);
    }

    /// <summary>The model as a project saved before decision 500 holds it: no mark, no styles of its own (or the ones given).</summary>
    static List<Entity> Saved(Fixture f, JsonArray? styles = null) => f.Model.Entities.Select(e =>
    {
        if (e.Class != IfcClass.Project) return e;
        var a = e.CloneAttrs();
        a.Remove(ViewStyles.StructureRevKey);
        a.Remove(ViewStyles.Key);
        if (styles != null) a[ViewStyles.Key] = styles;
        return e.With(attrs: a);
    }).ToList();

    [Fact]
    public void A_project_made_before_draws_as_it_drew()
    {
        var (f, wall) = Insulated();
        var plan = Plan(f);
        var section = Section(f);
        var (up, rels) = f.Session.Upgraded(Saved(f), f.Model.Relations.ToList());
        var project = up.Single(e => e.Class == IfcClass.Project);
        Assert.Equal(1, (int)project.Attrs[ViewStyles.StructureRevKey]!);
        // Its styles written as it read them: the plans the entire model, the structural ones the cores.
        var styles = ((JsonArray)project.Attrs[ViewStyles.Key]!).OfType<JsonObject>().Select(ViewStyles.Style.From).ToList();
        Assert.All(styles, s => Assert.Null(s.LayersPlan));
        Assert.Equal("core", styles.Single(s => s.Id == "structural").Layers);
        f.Session.Load(up, rels, null);
        Assert.Equal(["layer:0", "layer:1", "layer:2", "layer:3"], Keys(f, plan, wall));
        Assert.Equal(["layer:0", "layer:1", "layer:2", "layer:3"], Keys(f, section, wall));
        // Upgraded once.
        var (again, _) = f.Session.Upgraded(up, rels);
        Assert.Same(up.Single(e => e.Class == IfcClass.Project), again.Single(e => e.Class == IfcClass.Project));
    }

    [Fact]
    public void A_project_made_before_keeps_its_own_styles_and_decision_496s_choices()
    {
        var (f, wall) = Insulated();
        var plan = Plan(f);
        var section = Section(f);
        // Its own style: every layer, with decision 496's choice of plans without the walls' finishes.
        var own = new JsonArray(new JsonObject { ["id"] = "architectural", ["name"] = "Architectural", ["discipline"] = "Architecture", ["layers"] = "all", ["joins"] = true, ["overhead"] = true, ["wallFinishesPlan"] = false });
        var (up, rels) = f.Session.Upgraded(Saved(f, own), f.Model.Relations.ToList());
        f.Session.Load(up, rels, null);
        var s = ViewStyles.All(f.Model).Single(x => x.Id == "architectural");
        Assert.Null(s.LayersPlan);
        Assert.False(s.WallFinishesPlan);
        // What it drew: the plan without the render and the plaster (decision 496), the wool kept; the section all of it.
        Assert.Equal("all", ViewStyles.Of(f.Model, Views.Find(f.Model, plan)!).Layers);
        Assert.Equal(["layer:1", "layer:2"], Keys(f, plan, wall));
        Assert.Equal(["layer:0", "layer:1", "layer:2", "layer:3"], Keys(f, section, wall));
    }

    [Fact]
    public void A_new_projects_default_wall_stands_its_core_on_the_structural_slab()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0);
        var type = f.Model.RelatingOf(wall, IfcClass.RelDefinesByType)!;
        var layers = LayerSets.LayersOf(f.Model, type.Id);
        Assert.True(layers.Single(l => l.Core).ExtendDown);
        Assert.All(layers.Where(l => !l.Core), l => Assert.NotEqual(true, l.ExtendDown));
        Assert.All(layers, l => Assert.NotEqual(true, l.ExtendUp));
    }
}
