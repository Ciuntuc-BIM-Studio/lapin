using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The lines between a cut element's layers (forum: "change the weight and colour of the lines that separate the layers"):
/// their own role, separation, set by the project's object styles, a view's category and an element, narrowest first;
/// what is not set comes from the cut lines, as before.
/// </summary>
public class LayerLinesTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static JsonObject Spec(int? pen = null, string? color = null, string? pattern = null)
    {
        var o = new JsonObject();
        if (pen != null) o["pen"] = pen;
        if (color != null) o["color"] = color;
        if (pattern != null) o["pattern"] = pattern;
        return o;
    }
    static double PenMm(int pen) => LineStyles.PenWidth(LineStyles.DefaultPenTable(), pen, 100);

    [Fact]
    public void The_separation_role_resolves_at_each_level_the_narrowest_winning_field_by_field()
    {
        var project = new JsonObject { ["walls"] = new JsonObject { ["separation"] = Spec(1, "#111111", "dotted") } };
        var view = new JsonObject { ["walls"] = new JsonObject { ["lines"] = new JsonObject { ["separation"] = Spec(3, "#333333") } } };
        var element = new JsonObject { ["w1"] = new JsonObject { ["separation"] = Spec(5) } };

        // The project's object styles alone.
        var r = LineStyles.For(project, null, null, null, 100)!(LineRole.Separation, "IfcWall", "w1")!;
        Assert.Equal(PenMm(1), r.WidthMm);
        Assert.Equal("#111111", r.Color);
        Assert.Equal(Dashes.Of("dotted"), r.Dash);
        // The view's category over them.
        r = LineStyles.For(project, view, null, null, 100)!(LineRole.Separation, "IfcWall", "w1")!;
        Assert.Equal(PenMm(3), r.WidthMm);
        Assert.Equal("#333333", r.Color);
        Assert.Equal(Dashes.Of("dotted"), r.Dash);
        // The element's own over both; its other fields from the next level.
        r = LineStyles.For(project, view, element, null, 100)!(LineRole.Separation, "IfcWall", "w1")!;
        Assert.Equal(PenMm(5), r.WidthMm);
        Assert.Equal("#333333", r.Color);
        Assert.Equal(Dashes.Of("dotted"), r.Dash);
        // Another element of the category takes the view's.
        Assert.Equal(PenMm(3), LineStyles.For(project, view, element, null, 100)!(LineRole.Separation, "IfcWall", "w2")!.WidthMm);
        // Each level alone, and a separation style never styles the cut lines.
        Assert.Equal(PenMm(3), LineStyles.For(null, view, null, null, 100)!(LineRole.Separation, "IfcWall", null)!.WidthMm);
        Assert.Equal(PenMm(5), LineStyles.For(null, null, element, null, 100)!(LineRole.Separation, "IfcWall", "w1")!.WidthMm);
        Assert.Null(LineStyles.For(project, view, element, null, 100)!(LineRole.Cut, "IfcWall", "w1"));
        // Floors are a category of their own.
        Assert.Null(LineStyles.For(project, view, null, null, 100)!(LineRole.Separation, "IfcSlab", null));
    }

    static (Fixture f, string plan, string wall) LayeredWall()
    {
        var f = F();
        f.EveryLayerInPlans();
        var type = f.Type("Layered", ("Plaster", 0.015, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
        var wall = f.Wall(0, 0, 5, 0, type);
        return (f, Plan(f), wall);
    }

    /// <summary>The cut pens: the layer lines (the two boundaries at y = ±0.125) and the rest (the contour).</summary>
    static (List<Pen> layers, List<Pen> contour) CutPens(Fixture f, string plan)
    {
        var cut = Views.Render(f.Session.Reader, f.Model.Get(plan)!).Pens.Where(p => p.Layer == PenLayer.Cut).ToList();
        static bool Between(Pen p) => p.Segments.Count > 0 && p.Segments.All(s => Math.Abs(Math.Abs(s.A.Y) - 0.125) < 1e-6 && Math.Abs(Math.Abs(s.B.Y) - 0.125) < 1e-6);
        return (cut.Where(Between).ToList(), cut.Where(p => !Between(p)).ToList());
    }

    [Fact]
    public void A_views_layer_lines_change_only_the_lines_between_the_layers_not_the_contour()
    {
        var (f, plan, _) = LayeredWall();
        var (layers0, contour0) = CutPens(f, plan);
        var before = Assert.Single(layers0);
        Assert.Equal(Pens.Separation, before.Width, 9);
        Assert.Equal("#000000", before.Stroke);
        var contourBefore = contour0.Select(p => (p.Width, p.Stroke, p.Segments.Count)).ToList();

        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = new JsonObject { ["lines"] = new JsonObject { ["separation"] = Spec(1, "#ff0000", "dashed") } } });
        var (layers, contour) = CutPens(f, plan);
        var after = Assert.Single(layers);
        Assert.Equal(PenMm(1), after.Width, 9);
        Assert.Equal("#ff0000", after.Stroke);
        Assert.Equal(Dashes.Of("dashed"), after.Dash);
        Assert.Equal(2, after.Segments.Count);
        // The contour as it was.
        Assert.Equal(contourBefore, contour.Select(p => (p.Width, p.Stroke, p.Segments.Count)).ToList());

        // Cleared: today's drawing again.
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = null });
        Assert.Equal(Pens.Separation, Assert.Single(CutPens(f, plan).layers).Width, 9);
    }

    [Fact]
    public void Object_styles_take_the_layer_lines_and_what_they_leave_unset_comes_from_the_cut_lines()
    {
        var (f, plan, _) = LayeredWall();
        f.Run("view.objectStyle", new JsonObject { ["category"] = "walls", ["role"] = "separation", ["spec"] = Spec(color: "#0000ff") });
        Assert.Equal("#0000ff", (string?)f.Session.Query("view.objectStyles", new JsonObject())!["objectStyles"]!["walls"]!["separation"]!["color"]);
        var layers = Assert.Single(CutPens(f, plan).layers);
        Assert.Equal("#0000ff", layers.Stroke);
        Assert.Equal(Pens.Separation, layers.Width, 9);
        // A cut pen for walls: the layer lines take its width (as they always did), keeping their own colour.
        f.Run("view.objectStyle", new JsonObject { ["category"] = "walls", ["role"] = "cut", ["spec"] = Spec(7) });
        var (l2, contour) = CutPens(f, plan);
        Assert.Equal(PenMm(7), Assert.Single(l2).Width, 9);
        Assert.Equal("#0000ff", l2[0].Stroke);
        Assert.All(contour, p => Assert.Equal("#000000", p.Stroke));
        Assert.Contains(contour, p => Math.Abs(p.Width - PenMm(7)) < 1e-9);
        // Their own pen wins over the cut's.
        f.Run("view.objectStyle", new JsonObject { ["category"] = "walls", ["role"] = "separation", ["spec"] = Spec(2, "#0000ff") });
        Assert.Equal(PenMm(2), Assert.Single(CutPens(f, plan).layers).Width, 9);
    }

    [Fact]
    public void A_category_line_weight_still_thins_the_layer_lines_unless_they_have_a_pen_of_their_own()
    {
        var (f, plan, _) = LayeredWall();
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = new JsonObject { ["lineWeight"] = "0.7" } });
        Assert.Equal(Pens.SeenWeight(0.7), Assert.Single(CutPens(f, plan).layers).Width, 9);
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = new JsonObject { ["lineWeight"] = "0.7", ["lines"] = new JsonObject { ["separation"] = Spec(3) } } });
        Assert.Equal(PenMm(3), Assert.Single(CutPens(f, plan).layers).Width, 9);
    }

    [Fact]
    public void An_elements_own_layer_lines_in_a_view_win_over_its_category()
    {
        var (f, plan, wall) = LayeredWall();
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = new JsonObject { ["lines"] = new JsonObject { ["separation"] = Spec(1) } } });
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["element"] = wall, ["value"] = new JsonObject { ["separation"] = Spec(4, "#00ff00") } });
        var layers = Assert.Single(CutPens(f, plan).layers);
        Assert.Equal(PenMm(4), layers.Width, 9);
        Assert.Equal("#00ff00", layers.Stroke);
    }
}
