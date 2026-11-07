using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Visibility / Graphics as in Revit (docs/plan-visibility-graphics.md): subcategories, links per view, a style's set.</summary>
public class VisibilityGraphicsTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static string Plan(Fixture f, string? storey = null) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey ?? f.Storey })!["id"]!;

    /// <summary>The cut keys each element draws in a view.</summary>
    static Dictionary<string, List<string>> Cuts(Fixture f, string view)
    {
        var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, view)!);
        Assert.True(d != null, problem);
        return d!.Cuts.GroupBy(c => c.Element).ToDictionary(g => g.Key, g => g.Select(c => c.Key ?? "").Distinct().Order().ToList());
    }

    static void G(Fixture f, JsonObject a) => f.Run("view.graphics", a);
    static JsonObject Hidden => new() { ["hidden"] = true };

    [Fact]
    public void The_new_categories_reach_what_had_none()
    {
        Assert.Equal("rebar", LineStyles.CategoryOf("LapinRebar"));
        Assert.Equal("rebar", LineStyles.CategoryOf("IfcReinforcingBar"));
        Assert.Equal("coverings", LineStyles.CategoryOf("LapinRoomFinish"));
        Assert.Equal("coverings", LineStyles.CategoryOf("IfcCovering"));
        Assert.Equal("mep", LineStyles.CategoryOf("IfcPipeSegment"));
        Assert.Equal("formwork", LineStyles.CategoryOf("LapinFormwork"));
        Assert.Equal("walls", LineStyles.CategoryOf("IfcWall"));
        Assert.All(new[] { "rebar", "coverings", "mep", "formwork", "scaffolds", "steelConnections", "transport", "shading" }, c => Assert.Contains(c, LineStyles.Categories));
    }

    [Fact]
    public void A_subcategory_leaves_out_the_walls_finishes_in_a_plan_a_sheet_and_the_3D()
    {
        var f = F();
        var type = f.Type("Plastered brick", ("Lime plaster", 0.02, false), ("Brick", 0.25, true), ("Mineral wool", 0.1, false), ("Silicate render", 0.01, false));
        var wall = f.Wall(0, 0, 5, 0, type);
        var plan = Plan(f);
        Assert.Equal(["layer:0", "layer:1", "layer:2", "layer:3"], Cuts(f, plan)[wall]);
        G(f, new JsonObject { ["id"] = plan, ["category"] = "walls", ["sub"] = "finishes", ["value"] = Hidden });
        Assert.Equal(["layer:1", "layer:2"], Cuts(f, plan)[wall]);
        // The insulation too, as a subcategory of its own (by its material's category); the core stays.
        f.Run("material.update", new JsonObject { ["id"] = Materials.Find(f.Model, "Mineral wool")!.Id, ["category"] = "Insulation" });
        G(f, new JsonObject { ["id"] = plan, ["category"] = "walls", ["sub"] = "insulation", ["value"] = Hidden });
        Assert.Equal(["layer:1"], Cuts(f, plan)[wall]);
        // The category's own row keeps its subcategories, and hides all of them.
        G(f, new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = new JsonObject { ["lineColor"] = "#ff0000" } });
        Assert.Equal(["layer:1"], Cuts(f, plan)[wall]);
        G(f, new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = Hidden });
        Assert.False(Cuts(f, plan).ContainsKey(wall));
        f.Session.Undo();
        // Its subcategories as the dialog reads them.
        var vis = (JsonObject)f.Session.Query("view.visibility", new JsonObject { ["id"] = plan })!;
        Assert.Contains("finishes", vis["subcategories"]!["walls"]!.AsArray().Select(x => (string)x!));
        Assert.True((bool)vis["graphics"]!["categories"]!["walls"]!["sub"]!["finishes"]!["hidden"]!);
        // The 3D: the finishes' parts hidden as looks of their own.
        G(f, new JsonObject { ["id"] = "3d", ["category"] = "walls", ["sub"] = "finishes", ["value"] = Hidden });
        var looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject())!;
        var parts = looks[wall]!["parts"]!.AsObject();
        Assert.Equal(["layer:0", "layer:3"], parts.Select(p => p.Key).Order());
        Assert.True((bool)parts["layer:0"]!["hidden"]!);
    }

    /// <summary>A small IFC: one wall, written by Lapin itself.</summary>
    static byte[] SmallIfc()
    {
        var f = F();
        f.Wall(0, 0, 4, 0);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "small.ifc", new IfcExportOptions("IFC4", "group", null));
        return ms.ToArray();
    }

    /// <summary>A host wall and a linked one on the same storey, and a plan of it.</summary>
    static (Fixture F, string Host, string Linked, string Link, string Plan) Linked()
    {
        var f = F();
        var r = IfcImport.Link(f.Session, SmallIfc(), "structure.ifc", null, null, out _);
        var linked = f.Model.OfClass(IfcClass.Wall).Single(w => w.Str(Links.Key) == r.LinkId);
        var storey = Spatial.StoreyOf(f.Model, linked.Id)!.Id;
        var host = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = storey, ["x1"] = 0, ["y1"] = 5, ["x2"] = 4, ["y2"] = 5 })!["id"]!;
        return (f, host, linked.Id, r.LinkId!, Plan(f, storey));
    }

    [Fact]
    public void A_link_is_hidden_halftone_or_ruled_by_its_own_categories_in_one_view()
    {
        var (f, host, linked, link, plan) = Linked();
        var other = Plan(f, Spatial.StoreyOf(f.Model, linked)!.Id);
        Assert.True(Cuts(f, plan).ContainsKey(linked));
        // By host view: the view's categories reach the link's elements.
        G(f, new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = Hidden });
        Assert.Empty(Cuts(f, plan));
        // Custom: the link's own categories rule it, the view's no longer reach it.
        G(f, new JsonObject { ["id"] = plan, ["link"] = link, ["value"] = new JsonObject { ["display"] = "custom" } });
        Assert.Equal([linked], Cuts(f, plan).Keys);
        G(f, new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = null });
        G(f, new JsonObject { ["id"] = plan, ["link"] = link, ["category"] = "walls", ["value"] = Hidden });
        Assert.Equal([host], Cuts(f, plan).Keys);
        // The other view shows both.
        Assert.Equal(2, Cuts(f, other).Count);
        // Back to by host: its own rows stay, unused.
        G(f, new JsonObject { ["id"] = plan, ["link"] = link, ["value"] = new JsonObject { ["display"] = "host" } });
        Assert.Equal(2, Cuts(f, plan).Count);
        var row = (JsonObject)f.Session.Query("view.visibility", new JsonObject { ["id"] = plan })!["graphics"]!["links"]![link]!;
        Assert.Equal("host", (string)row["display"]!);
        Assert.True((bool)row["categories"]!["walls"]!["hidden"]!);
        // Hidden in this view alone; then halftone in it.
        G(f, new JsonObject { ["id"] = plan, ["link"] = link, ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Equal([host], Cuts(f, plan).Keys);
        Assert.Equal(2, Cuts(f, other).Count);
        G(f, new JsonObject { ["id"] = plan, ["link"] = link, ["value"] = new JsonObject { ["halftone"] = true } });
        var pens = ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!)["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string?)p["layer"] == "CUT").ToList();
        Assert.Contains(pens, p => (string?)p["stroke"] == Links.Halftone);
        Assert.Contains(pens, p => (string?)p["stroke"] != Links.Halftone);
        Assert.Throws<CommandException>(() => G(f, new JsonObject { ["id"] = plan, ["link"] = link, ["value"] = new JsonObject { ["display"] = "linked" } }));
        Assert.Throws<CommandException>(() => G(f, new JsonObject { ["id"] = plan, ["link"] = "nothing", ["value"] = Hidden }));
    }

    [Fact]
    public void The_3D_view_takes_a_link_s_own_rows_as_looks()
    {
        var (f, host, linked, link, _) = Linked();
        G(f, new JsonObject { ["id"] = "3d", ["link"] = link, ["value"] = new JsonObject { ["display"] = "custom" } });
        G(f, new JsonObject { ["id"] = "3d", ["link"] = link, ["class"] = IfcClass.Wall, ["value"] = Hidden });
        var looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject { ["id"] = "3d" })!;
        Assert.True((bool)looks[linked]!["hidden"]!);
        Assert.False(looks.ContainsKey(host));
        Assert.Equal([linked], f.Session.Query("view.hidden3d", new JsonObject())!.AsArray().Select(x => (string)x!));
        G(f, new JsonObject { ["id"] = "3d", ["link"] = link, ["class"] = IfcClass.Wall, ["value"] = null });
        G(f, new JsonObject { ["id"] = "3d", ["link"] = link, ["value"] = new JsonObject { ["display"] = "custom", ["halftone"] = true } });
        looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject { ["id"] = "3d" })!;
        Assert.Equal(0.35, (double)looks[linked]!["opacity"]!, 6);
        // The dialog lists the link with the classes it brought.
        var listed = f.Session.Query("view.visibility", new JsonObject { ["id"] = "3d" })!["links"]!.AsArray().OfType<JsonObject>().Single();
        Assert.Equal("structure", (string)listed["name"]!);
        Assert.Contains(IfcClass.Wall, listed["classes"]!.AsArray().Select(x => (string)x!));
    }

    [Fact]
    public void A_view_style_s_graphics_set_is_its_template()
    {
        var f = F();
        var wall = f.Wall(0, 0, 5, 0);
        var plan = Plan(f);
        var other = Plan(f);
        G(f, new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = Hidden });
        var set = (string)f.Run("view.saveGraphicsSet", new JsonObject { ["id"] = plan, ["name"] = "No walls" })!["id"]!;
        // The architectural style names the set: a view without graphics of its own takes it.
        Assert.True(Cuts(f, other).ContainsKey(wall));
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["graphicsSet"] = set });
        Assert.False(Cuts(f, other).ContainsKey(wall));
        var vis = (JsonObject)f.Session.Query("view.visibility", new JsonObject { ["id"] = other })!;
        Assert.Equal(set, (string?)vis["graphicsSet"]);
        Assert.True((bool)vis["fromStyle"]!);
        // Its own graphics again: they start from the set's, and stay its own once emptied.
        f.Run("view.useGraphicsSet", new JsonObject { ["id"] = other, ["setId"] = null });
        Assert.False(Cuts(f, other).ContainsKey(wall));
        G(f, new JsonObject { ["id"] = other, ["category"] = "walls", ["value"] = null });
        Assert.True(Cuts(f, other).ContainsKey(wall));
        // The set removed: the style forgets it, the views that took it from the style keep its rows.
        var third = Plan(f);
        Assert.False(Cuts(f, third).ContainsKey(wall));
        f.Run("view.removeGraphicsSet", new JsonObject { ["setId"] = set });
        Assert.Null(ViewStyles.All(f.Model).Single(s => s.Id == "architectural").GraphicsSet);
        Assert.False(Cuts(f, third).ContainsKey(wall));
        Assert.True(Cuts(f, other).ContainsKey(wall));
    }

    [Fact]
    public void An_old_view_s_graphics_read_as_before()
    {
        var f = F();
        var wall = f.Wall(0, 0, 5, 0);
        var plan = Plan(f);
        // As a project saved before decision 391 holds it: the category's row, no subcategories, no links, no style's set.
        G(f, new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = new JsonObject { ["hidden"] = true, ["lineColor"] = "#ff0000" } });
        Assert.Equal("""{"categories":{"walls":{"hidden":true,"lineColor":"#ff0000"}}}""", f.Model.Get(plan)!.Attrs["graphics"]!.ToJsonString());
        Assert.False(Cuts(f, plan).ContainsKey(wall));
        Assert.Null(GraphicsSets.EffectiveSet(f.Model, f.Model.Get(plan)!));
    }
}
