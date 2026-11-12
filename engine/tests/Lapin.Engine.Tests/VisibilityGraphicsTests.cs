using System.Text;
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

    /// <summary>The elements a view's drawing marks: what the plan's snaps may take (forum #31, decision 491).</summary>
    static HashSet<string> Marked(Fixture f, string view) =>
        ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!)["marks"]!.AsArray().Select(x => (string)x!["id"]!).ToHashSet();

    [Fact]
    public void A_hidden_element_leaves_nothing_in_the_drawing_to_snap_to()
    {
        var f = F();
        var wall = f.Wall(0, 0, 5, 0);
        var plan = Plan(f);
        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 3), ["b"] = new JsonArray(5, 3) }) })!["ids"]![0]!;
        Assert.Contains(wall, Marked(f, plan));
        Assert.Contains(line, Marked(f, plan));
        // Walls hidden in the view: the wall has no mark (nothing to pick or snap to) and no cut line; the drafted line keeps its own.
        G(f, new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = Hidden });
        Assert.DoesNotContain(wall, Marked(f, plan));
        Assert.Contains(line, Marked(f, plan));
        var pens = ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!)["pens"]!.AsArray();
        Assert.DoesNotContain(pens, p => (string?)p!["layer"] == "CUT");
    }

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
    static HashSet<string> Owners(Fixture f, string view) =>
        Views.Render(new Q(f), Views.Find(f.Model, view)!).Marks.Select(mk => mk.Owner).OfType<string>().ToHashSet();

    [Fact]
    public void Annotation_categories_hide_dimensions_grids_and_texts_by_their_kind()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var plan = Plan(f);
        var dim = (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(5, 0), ["at"] = new JsonArray(0, 1) })!["id"]!;
        Assert.Contains(dim, Owners(f, plan));
        G(f, new JsonObject { ["id"] = plan, ["annotation"] = "dimensions", ["value"] = Hidden });
        Assert.DoesNotContain(dim, Owners(f, plan));
        // The wall stays; the dialog lists the annotation categories.
        Assert.NotEmpty(Cuts(f, plan));
        var vis = (JsonObject)f.Session.Query("view.visibility", new JsonObject { ["id"] = plan })!;
        Assert.Contains("grids", vis["annotations"]!.AsArray().Select(x => (string)x!));
        G(f, new JsonObject { ["id"] = plan, ["annotation"] = "dimensions", ["value"] = null });
        Assert.Contains(dim, Owners(f, plan));
        Assert.Throws<CommandException>(() => G(f, new JsonObject { ["id"] = plan, ["annotation"] = "clouds", ["value"] = Hidden }));
        Assert.Equal("roomTags", VisibilityGraphics.AnnotationOf(new Entity("r", IfcClass.Space, "Room", []), new ViewMark("r")));
        Assert.Null(VisibilityGraphics.AnnotationOf(new Entity("r", IfcClass.Space, "Room", []), new ViewMark("r") { Under = true }));
    }

    [Fact]
    public void A_dxf_import_is_hidden_halftone_or_hidden_by_layer_in_one_view()
    {
        var f = F();
        var plan = Plan(f);
        var dxf = string.Join("\n", ["0", "SECTION", "2", "ENTITIES", "0", "LINE", "8", "A-WALL", "10", "0", "20", "0", "11", "1000", "21", "0", "0", "LINE", "8", "A-TEXT", "10", "0", "20", "500", "11", "1000", "21", "500", "0", "ENDSEC", "0", "EOF"]);
        f.Run("drafting.importDxf", new JsonObject { ["viewId"] = plan, ["name"] = "survey.dxf", ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(dxf)), ["units"] = "mm" });
        var shapes = Lapin.Drafting.Cad.Drafts.On(f.Model, plan).ToList();
        Assert.Equal(2, shapes.Count);
        var import = shapes[0].Str(VisibilityGraphics.DxfImportKey)!;
        var listed = f.Session.Query("view.visibility", new JsonObject { ["id"] = plan })!["imports"]!.AsArray().OfType<JsonObject>().Single();
        Assert.Equal("survey.dxf", (string)listed["name"]!);
        Assert.Equal(["A-TEXT", "A-WALL"], listed["layers"]!.AsArray().Select(x => (string)x!));
        var wallLine = shapes.Single(e => e.Str("layer") == "A-WALL").Id;
        G(f, new JsonObject { ["id"] = plan, ["import"] = import, ["layer"] = "A-TEXT", ["value"] = Hidden });
        Assert.Equal([wallLine], Owners(f, plan).Intersect(shapes.Select(e => e.Id)));
        // The import's own row keeps its layers; hidden, nothing of it is drawn.
        G(f, new JsonObject { ["id"] = plan, ["import"] = import, ["value"] = new JsonObject { ["halftone"] = true } });
        var pens = ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!)["pens"]!.AsArray().OfType<JsonObject>();
        Assert.Equal(Links.Halftone, (string?)pens.Single(p => (string?)p["layer"] == "A-WALL")["stroke"]);
        Assert.True((bool)f.Session.Query("view.visibility", new JsonObject { ["id"] = plan })!["graphics"]!["imports"]![import]!["layers"]!["A-TEXT"]!["hidden"]!);
        G(f, new JsonObject { ["id"] = plan, ["import"] = import, ["value"] = Hidden });
        Assert.Empty(Owners(f, plan).Intersect(shapes.Select(e => e.Id)));
    }

    [Fact]
    public void A_filter_hides_what_its_rules_take_in_the_views_that_use_it()
    {
        var f = F();
        var thick = f.Type("Brick 300", ("Brick", 0.3, true));
        var thin = f.Type("Partition", ("Gypsum board", 0.1, true));
        var a = f.Wall(0, 0, 5, 0, thick);
        var b = f.Wall(0, 3, 5, 3, thin);
        f.Run("element.setParam", new JsonObject { ["id"] = b, ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "EI 60" });
        var plan = Plan(f);
        var other = Plan(f);
        var partitions = (string)f.Run("viewFilter.create", new JsonObject { ["name"] = "Partitions", ["rules"] = new JsonArray(new JsonObject { ["field"] = "type", ["op"] = "contains", ["value"] = "partition" }) })!["id"]!;
        Assert.Equal(1, (int)f.Session.Query("viewFilter.count", new JsonObject { ["id"] = partitions })!);
        // Not used by the view: nothing changes.
        Assert.Equal(2, Cuts(f, plan).Count);
        G(f, new JsonObject { ["id"] = plan, ["filters"] = new JsonArray(new JsonObject { ["id"] = partitions, ["hidden"] = true }) });
        Assert.Equal([a], Cuts(f, plan).Keys);
        Assert.Equal(2, Cuts(f, other).Count);
        // Rules by material and by a parameter; any of them.
        f.Run("viewFilter.update", new JsonObject { ["id"] = partitions, ["match"] = "any", ["rules"] = new JsonArray(
            new JsonObject { ["field"] = "material", ["op"] = "equals", ["value"] = "Brick" },
            new JsonObject { ["field"] = "param", ["name"] = "Fire rating", ["op"] = "hasValue" }) });
        Assert.Empty(Cuts(f, plan));
        // The first filter in the view's order that says gives it: one that shows, then the hiding one.
        var bricks = (string)f.Run("viewFilter.create", new JsonObject { ["name"] = "Brick", ["rules"] = new JsonArray(new JsonObject { ["field"] = "material", ["op"] = "equals", ["value"] = "brick" }) })!["id"]!;
        G(f, new JsonObject { ["id"] = plan, ["filters"] = new JsonArray(new JsonObject { ["id"] = bricks, ["hidden"] = false }, new JsonObject { ["id"] = partitions, ["hidden"] = true }) });
        Assert.Equal([a], Cuts(f, plan).Keys);
        // A filter gone is left out; one never made is refused.
        f.Run("viewFilter.delete", new JsonObject { ["id"] = partitions });
        Assert.Equal(2, Cuts(f, plan).Count);
        Assert.Throws<CommandException>(() => G(f, new JsonObject { ["id"] = plan, ["filters"] = new JsonArray(new JsonObject { ["id"] = partitions }) }));
        Assert.Throws<CommandException>(() => f.Run("viewFilter.create", new JsonObject { ["rules"] = new JsonArray(new JsonObject { ["field"] = "colour" }) }));
        // In 3D too.
        G(f, new JsonObject { ["id"] = "3d", ["filters"] = new JsonArray(new JsonObject { ["id"] = bricks, ["hidden"] = true }) });
        Assert.True((bool)f.Session.Query("view.looks3d", new JsonObject())![a]!["hidden"]!);
    }
    static List<JsonObject> CutPens(Fixture f, string view) =>
        ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!)["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string?)p["layer"] == "CUT").ToList();

    [Fact]
    public void A_filter_s_colour_and_lines_and_a_category_s_halftone_draw_in_the_plan_and_the_3D()
    {
        var f = F();
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(0, 3, 5, 3);
        f.Run("element.setParam", new JsonObject { ["id"] = b, ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "REI 120" });
        var plan = Plan(f);
        var fire = (string)f.Run("viewFilter.create", new JsonObject { ["name"] = "Fire walls", ["rules"] = new JsonArray(new JsonObject { ["field"] = "param", ["name"] = "Fire rating", ["op"] = "beginsWith", ["value"] = "REI" }) })!["id"]!;
        G(f, new JsonObject { ["id"] = plan, ["filters"] = new JsonArray(new JsonObject { ["id"] = fire, ["color"] = "#ff0000", ["pattern"] = "dashed" }) });
        var pens = CutPens(f, plan);
        var red = pens.Where(p => (string?)p["stroke"] == "#ff0000").ToList();
        Assert.NotEmpty(red);
        Assert.All(red, p => Assert.NotNull(p["dash"]));
        Assert.Contains(pens, p => (string?)p["stroke"] != "#ff0000");
        // The category halftone: every wall grey but the one the filter colours first.
        G(f, new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = new JsonObject { ["halftone"] = true } });
        pens = CutPens(f, plan);
        Assert.Contains(pens, p => (string?)p["stroke"] == Links.Halftone);
        Assert.Contains(pens, p => (string?)p["stroke"] == "#ff0000");
        // In 3D: the colour, a transparency as opacity, the halftone at 35 %.
        G(f, new JsonObject { ["id"] = "3d", ["filters"] = new JsonArray(new JsonObject { ["id"] = fire, ["color"] = "#ff0000", ["transparency"] = 60 }) });
        G(f, new JsonObject { ["id"] = "3d", ["category"] = "walls", ["value"] = new JsonObject { ["halftone"] = true } });
        var looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject())!;
        Assert.Equal("#ff0000", (string)looks[b]!["color"]!);
        Assert.Equal(0.4, (double)looks[b]!["opacity"]!, 6);
        Assert.Equal(0.35, (double)looks[a]!["opacity"]!, 6);
    }

    [Fact]
    public void An_annotation_category_s_colour_and_the_view_s_detail_level()
    {
        var f = F();
        var plan = Plan(f);
        var dim = (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(5, 0), ["at"] = new JsonArray(0, 1) })!["id"]!;
        G(f, new JsonObject { ["id"] = plan, ["annotation"] = "dimensions", ["value"] = new JsonObject { ["color"] = "#0000ff" } });
        var mk = Views.Render(new Q(f), Views.Find(f.Model, plan)!).Marks.First(x => x.Owner == dim);
        Assert.All(mk.Lines, l => Assert.Equal("#0000ff", l.Color));
        G(f, new JsonObject { ["id"] = plan, ["annotation"] = "dimensions", ["value"] = new JsonObject { ["halftone"] = true } });
        mk = Views.Render(new Q(f), Views.Find(f.Model, plan)!).Marks.First(x => x.Owner == dim);
        Assert.All(mk.Lines, l => Assert.Equal(Links.Halftone, l.Color));
        // Detail level: kept, read back, refused when it is not one.
        G(f, new JsonObject { ["id"] = plan, ["detailLevel"] = "coarse" });
        Assert.Equal(PlanDetail.Lod100, VisibilityGraphics.Of(f.Model, f.Model.Get(plan)!).DetailLevel);
        G(f, new JsonObject { ["id"] = plan, ["detailLevel"] = null });
        Assert.Null(VisibilityGraphics.Of(f.Model, f.Model.Get(plan)!).DetailLevel);
        Assert.Throws<CommandException>(() => G(f, new JsonObject { ["id"] = plan, ["detailLevel"] = "rough" }));
    }
}
