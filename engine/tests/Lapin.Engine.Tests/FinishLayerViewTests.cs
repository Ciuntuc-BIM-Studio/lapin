using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Finish layers by the kind of view (the forum's topic #30, Poland): a layer of a wall or floor type is a finish by its own
/// category when it gives one (IFC4's IfcMaterialLayer.Category), else by its material's; a view style says, apart for walls and
/// for floors, whether plans and whether sections and elevations draw the finishes. Visibility / Graphics resolves it, so the
/// plan drawn, the sheet and the exports agree; what is left draws its cut outline at the faces it keeps. Quantities and the
/// IFC body are the model's, whatever a view shows.
/// </summary>
public sealed class FinishLayerViewTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-finish-views").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

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

    static string WallType(Fixture f, string name, params JsonObject[] layers) =>
        (string)f.Run("architecture.createWallType", new JsonObject { ["name"] = name, ["layers"] = new JsonArray(layers.Select(l => (JsonNode)l).ToArray()) })!["id"]!;

    static JsonObject L(string material, double thickness, bool core = false, string? category = null)
    {
        var o = new JsonObject { ["material"] = material, ["thickness"] = thickness, ["core"] = core };
        if (category != null) o["category"] = category;
        return o;
    }

    /// <summary>A plastered brick wall (plaster 15, brick 250 core, plaster 15) along x from 0 to 5, a floor of the default type (finish 20, screed 50, concrete 150) under it.</summary>
    static (Fixture F, string Wall, string Slab) Room()
    {
        var f = new Fixture();
        var type = f.Type("Plastered brick", ("Plaster", 0.015, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
        var wall = f.Wall(0, 0, 5, 0, type);
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(-1, -2), new JsonArray(6, -2), new JsonArray(6, 2), new JsonArray(-1, 2)) })!["id"]!;
        // The choices by element kind are the advanced detail of the partial structure display (decision 500): over the entire model here, as a project made before draws.
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["layersPlan"] = "all" });
        return (f, wall, slab);
    }

    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static string Section(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(2.5, -1), ["end"] = new JsonArray(2.5, 1) })!["id"]!;

    /// <summary>The Polish practice set on the Architectural style: plans without wall or floor finishes; sections with the floors' finishes but not the walls'.</summary>
    static void Polish(Fixture f) => f.Run("viewStyle.update", new JsonObject
    {
        ["id"] = "architectural", ["wallFinishesPlan"] = false, ["floorFinishesPlan"] = false, ["wallFinishesSection"] = false, ["floorFinishesSection"] = true,
    });

    [Fact]
    public void A_layer_is_a_finish_by_its_own_category_else_by_its_materials()
    {
        var f = new Fixture();
        // A cement board is no finish by its name; marked one on this layer, it is. A lime plaster marked "Other" on its layer is none here.
        var type = WallType(f, "Boarded", L("Cement board", 0.0125, category: "Finish"), L("Brick", 0.25, true), L("Lime plaster", 0.015, category: "Other"), L("Silicate render", 0.01));
        var wall = f.Wall(0, 0, 5, 0, type);
        Assert.Equal(new string?[] { "Finish", null, "Other", null }, LayerSets.LayersOf(f.Model, type).Select(l => l.Category));
        var plan = Plan(f);
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["layers"] = "noFinishes" });
        Assert.Equal(["layer:1", "layer:2"], Keys(f, plan, wall));
        // Visibility / Graphics reads the layer's category too: the board is in the walls' finishes, the lime plaster in their other layers.
        var vg = VisibilityGraphics.Of(f.Model, Views.Find(f.Model, plan)!);
        var e = f.Model.Get(wall)!;
        Assert.Equal("finishes", vg.SubOf(e, "layer:0"));
        Assert.Equal("otherLayers", vg.SubOf(e, "layer:2"));
        Assert.Equal("finishes", vg.SubOf(e, "layer:3"));
        // A category no one knows is refused; none puts the material's back.
        Assert.Throws<CommandException>(() => WallType(f, "Bad", L("Brick", 0.25, true), L("Plaster", 0.015, category: "Paint")));
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["layers"] = new JsonArray(L("Cement board", 0.0125), L("Brick", 0.25, true), L("Lime plaster", 0.015), L("Silicate render", 0.01)) });
        Assert.Equal(["layer:0", "layer:1"], Keys(f, plan, wall));
    }

    [Fact]
    public void Plans_and_sections_draw_the_wall_and_floor_finishes_as_their_style_says()
    {
        var (f, wall, slab) = Room();
        var plan = Plan(f);
        var section = Section(f);
        // By default every view draws them all.
        Assert.Equal(["layer:0", "layer:1", "layer:2"], Keys(f, plan, wall));
        Assert.Equal(["layer:0", "layer:1", "layer:2"], Keys(f, section, wall));
        Assert.Equal(["layer:0", "layer:1", "layer:2"], Keys(f, section, slab));
        Polish(f);
        // Plans: the brick alone; the floor's finish left out too (the screed and the concrete are no finishes).
        Assert.Equal(["layer:1"], Keys(f, plan, wall));
        var inPlan = VisibilityGraphics.Of(f.Model, Views.Find(f.Model, plan)!);
        Assert.True(inPlan.HidesPart(f.Model.Get(slab)!, "layer:0"));
        Assert.False(inPlan.HidesPart(f.Model.Get(slab)!, "layer:1"));
        // Sections: the walls without their plaster, the floor with its finish.
        Assert.Equal(["layer:1"], Keys(f, section, wall));
        Assert.Equal(["layer:0", "layer:1", "layer:2"], Keys(f, section, slab));
        // An elevation reads as a section.
        var elevation = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        Assert.True(VisibilityGraphics.Of(f.Model, Views.Find(f.Model, elevation)!).HidesPart(f.Model.Get(wall)!, "layer:0"));
        // A view may say otherwise over its style, and take the style's again.
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["wallFinishesPlan"] = true });
        Assert.Equal(["layer:0", "layer:1", "layer:2"], Keys(f, plan, wall));
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["wallFinishesPlan"] = null });
        Assert.Equal(["layer:1"], Keys(f, plan, wall));
        // A 3D view keeps its own layers: the rule is the 2D drawings'.
        Assert.False(VisibilityGraphics.Of(f.Model, GraphicsSets.As3dView(f.Model)).HidesPart(f.Model.Get(wall)!, "layer:0"));
        // The model is the same: quantities of the plaster as before.
        Assert.Contains(f.Session.Quantities(wall), q => q.Part == "layer:0" && q.Quantities.Single(x => x.Name == "NetVolume").Value > 0);
    }

    [Fact]
    public void The_style_keeps_the_four_choices_and_an_older_style_draws_every_finish()
    {
        var f = new Fixture();
        Polish(f);
        var s = ViewStyles.All(f.Model).Single(x => x.Id == "architectural");
        Assert.False(s.WallFinishesPlan);
        Assert.False(s.FloorFinishesPlan);
        Assert.False(s.WallFinishesSection);
        Assert.True(s.FloorFinishesSection);
        var json = s.ToJson();
        Assert.False((bool)json["wallFinishesPlan"]!);
        // A style saved before the choices (none of the four fields): every finish drawn, as before.
        var old = ViewStyles.Style.From(new JsonObject { ["id"] = "x", ["name"] = "Old", ["layers"] = "all" });
        Assert.True(old.WallFinishesPlan && old.FloorFinishesPlan && old.WallFinishesSection && old.FloorFinishesSection);
        Assert.Throws<CommandException>(() => f.Run("view.setStyle", new JsonObject { ["id"] = Plan(f), ["floorFinishesPlan"] = "no" }));
    }

    [Fact]
    public void A_plan_without_the_finishes_draws_the_walls_cut_outline_at_its_core()
    {
        var (f, _, _) = Room();
        var plan = Plan(f);
        Polish(f);
        var r = Views.Render(new Q(f), Views.Find(f.Model, plan)!, print: true);
        // The brick's faces at y ±0.125 are the cut outline; nothing is drawn at the plaster's faces (±0.14).
        static bool Along(Views.Rendered r, double y) => r.Pens.Where(p => p.Layer == PenLayer.Cut)
            .Any(p => p.Segments.Any(s => Math.Abs(s.A.Y - y) < 1e-6 && Math.Abs(s.B.Y - y) < 1e-6 && Math.Abs(s.A.X - s.B.X) > 1));
        Assert.True(Along(r, 0.125));
        Assert.True(Along(r, -0.125));
        Assert.False(Along(r, 0.14));
        Assert.False(Along(r, -0.14));
    }

    [Fact]
    public void The_interface_snaps_only_to_what_the_plan_draws()
    {
        var (f, wall, _) = Room();
        var plan = Plan(f);
        // The wall's cut pieces say their layers' roles; the drawing says which categories' finishes it leaves out.
        Assert.Equal(["core", "finishes", "finishes"], f.Session.Geometry(wall)!.Plan!.Where(p => p.Kind == "cut").Select(p => p.Sub ?? "").Order().ToList());
        Assert.Empty((JsonArray)Views.Drawing(new Q(f), Views.Find(f.Model, plan)!)["hiddenFinishes"]!);
        Polish(f);
        Assert.Equal(["slabs", "walls"], ((JsonArray)Views.Drawing(new Q(f), Views.Find(f.Model, plan)!)["hiddenFinishes"]!).Select(x => (string)x!).ToList());
        Assert.Equal(["walls"], ((JsonArray)Views.Drawing(new Q(f), Views.Find(f.Model, Section(f))!)["hiddenFinishes"]!).Select(x => (string)x!).ToList());
    }

    [Fact]
    public void A_layers_category_goes_to_IFC_and_the_package_and_comes_back()
    {
        var f = new Fixture();
        var type = WallType(f, "Boarded", L("Cement board", 0.0125, category: "Finish"), L("Brick", 0.25, true), L("Lime plaster", 0.015));
        f.Wall(0, 0, 4, 0, type);
        var ifc = Path.Combine(_dir, "boarded.ifc");
        IfcExport.Write(f.Session, ifc, null);
        Assert.Contains(",'Finish',$);", File.ReadAllText(ifc));
        var b = new Fixture();
        IfcImport.Run(b.Session, ifc, null, out _);
        var back = b.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "Boarded");
        Assert.Equal(new string?[] { "Finish", null, null }, LayerSets.LayersOf(b.Model, back.Id).Select(l => l.Category));
        var lapin = Path.Combine(_dir, "boarded.lapin");
        Lapin.Host.PackageFiles.Save(f.Session, lapin);
        var c = new Fixture();
        Lapin.Host.PackageFiles.Open(c.Session, lapin);
        Assert.Equal(new string?[] { "Finish", null, null }, LayerSets.LayersOf(c.Model, type).Select(l => l.Category));
    }
}
