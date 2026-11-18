using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage 4 of docs/plan-material-graphics.md (decision 674), what stage C of docs/plan-visibility-graphics.md left: a subcategory
/// of Visibility / Graphics (a layer's role: core, finishes, insulation…) gives its cut hatch, fill colour, line colour and weight
/// and halftone. Each reaches the pens; it lies over the style's material graphics set and over its category's cut hatch and
/// fill colour; graphics sets and a link's own rows carry it; the 3D takes its halftone and colour per part; rows are checked.
/// </summary>
public class SubcategoryGraphicsTests
{
    static string Mat(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    /// <summary>A plastered concrete wall with an EPS layer (its material an insulation), a plan at 1:50.</summary>
    static (Fixture F, string Plan, string Wall) Wall()
    {
        var f = new Fixture();
        f.EveryLayerInPlans();
        var type = f.Type("Insulated", ("Gypsum plaster", 0.015, false), ("Concrete", 0.2, true), ("EPS insulation", 0.1, false));
        var wall = f.Wall(0, 0, 5, 0, type);
        f.Run("material.update", new JsonObject { ["id"] = Mat(f, "EPS insulation"), ["category"] = "Insulation" });
        f.Run("material.update", new JsonObject { ["id"] = Mat(f, "Gypsum plaster"), ["category"] = "Finish" });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        return (f, plan, wall);
    }

    static void Sub(Fixture f, string view, string sub, JsonObject? value, string category = "walls") =>
        f.Run("view.graphics", new JsonObject { ["id"] = view, ["category"] = category, ["sub"] = sub, ["value"] = value });

    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    static ResolvedMaterial Band(Fixture f, string view, string material)
    {
        var v = Views.Find(f.Model, view)!;
        var cut = Render(f, view).Generated!.Cuts.First(c => c.Material == Mat(f, material));
        return ViewMaterialGraphics.For(f.Model, v, "plan", 50).Band(cut)!;
    }

    [Fact]
    public void A_subcategory_s_cut_hatch_and_fill_reach_its_bands_and_the_pens()
    {
        var (f, plan, _) = Wall();
        Assert.Equal("RIGID", Band(f, plan, "EPS insulation").Hatch);
        Sub(f, plan, "insulation", new JsonObject { ["cutHatch"] = "cross" });
        var eps = Band(f, plan, "EPS insulation");
        Assert.Equal(("CROSS", 1.0, true), (eps.Hatch, eps.HatchScale, eps.KeepsHatch));
        // The core keeps its own.
        Assert.Equal("CONCRETE", Band(f, plan, "Concrete").Hatch);
        // No hatch, a fill: the band filled green.
        Sub(f, plan, "insulation", new JsonObject { ["cutHatch"] = "none", ["fillColor"] = "#00ff00" });
        Assert.Null(Band(f, plan, "EPS insulation").Hatch);
        Assert.Contains(Render(f, plan).Pens, p => p.Layer == PenLayer.Fill && p.Fill == "#00ff00");
        // Hidden as before.
        Sub(f, plan, "insulation", new JsonObject { ["hidden"] = true, ["fillColor"] = "#00ff00" });
        Assert.DoesNotContain(Render(f, plan).Generated!.Cuts, c => c.Material == Mat(f, "EPS insulation"));
    }

    [Fact]
    public void A_subcategory_s_line_colour_weight_and_halftone_reach_the_pens()
    {
        var (f, plan, _) = Wall();
        Sub(f, plan, "core", new JsonObject { ["lineColor"] = "#00aa00", ["lineWeight"] = "0.7" });
        var core = Band(f, plan, "Concrete");
        Assert.Equal(new MaterialLine("#00aa00", 0.7), core.CutLine);
        // The lines between its layers two pens lighter, as a category's.
        Assert.Equal(new MaterialLine("#00aa00", 0.35), core.SeparatorLine);
        var pens = Render(f, plan).Pens;
        Assert.Contains(pens, p => p.Layer == PenLayer.Cut && p.Stroke == "#00aa00" && p.Width == 0.7);
        // Halftone: the hatch, the fill and the lines washed halfway to white.
        Sub(f, plan, "core", new JsonObject { ["halftone"] = true, ["fillColor"] = "#ff0000" });
        core = Band(f, plan, "Concrete");
        Assert.True(core.Halftone);
        Assert.Equal(HexColor.Mix("#ff0000", 0.5), core.Fill);
        Assert.Equal(HexColor.Mix("#000000", 0.5), core.CutLine!.Color);
        pens = Render(f, plan).Pens;
        Assert.Contains(pens, p => p.Layer == PenLayer.Fill && p.Fill == HexColor.Mix("#ff0000", 0.5));
        Assert.Contains(pens, p => p.Layer == PenLayer.Hatch && p.Stroke == HexColor.Mix("#000000", 0.5));
        Assert.Contains(pens, p => p.Layer == PenLayer.Cut && p.Stroke == HexColor.Mix("#000000", 0.5));
        // Taken off, the band draws as before.
        Sub(f, plan, "core", null);
        Assert.False(Band(f, plan, "Concrete").Flex);
    }

    [Fact]
    public void A_subcategory_lies_over_the_style_s_set_and_over_its_category_s_hatch_and_fill()
    {
        var (f, plan, _) = Wall();
        var set = (string)f.Run("materialGraphicsSet.create", new JsonObject
        {
            ["name"] = "Plain",
            ["entries"] = new JsonArray(new JsonObject { ["match"] = new JsonObject(), ["section"] = new JsonObject { ["hatch"] = "LINES45", ["fill"] = "#0000ff" } }),
        })!["id"]!;
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["materialGraphicsSet"] = set });
        Assert.Equal(("LINES45", "#0000ff"), (Band(f, plan, "EPS insulation").Hatch, Band(f, plan, "EPS insulation").Fill));
        Sub(f, plan, "insulation", new JsonObject { ["cutHatch"] = "CROSS", ["fillColor"] = "#00ff00" });
        var eps = Band(f, plan, "EPS insulation");
        Assert.Equal(("CROSS", "#00ff00"), (eps.Hatch, eps.Fill));
        Assert.Equal(set, eps.GraphicsSet);
        // The category's fill colour and solid cut hatch over the set, but not over the subcategory's own.
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = new JsonObject { ["fillColor"] = "#ff00ff", ["cutHatch"] = "SOLID" } });
        Sub(f, plan, "insulation", new JsonObject { ["cutHatch"] = "none", ["fillColor"] = "#00ff00" });
        var pens = Render(f, plan).Pens;
        Assert.Contains(pens, p => p.Fill == "#00ff00");
        Assert.Contains(pens, p => p.Fill == "#ff00ff");
        // Without the subcategory's, the category's everywhere.
        Sub(f, plan, "insulation", null);
        Assert.DoesNotContain(Render(f, plan).Pens, p => p.Fill == "#00ff00");
    }

    [Fact]
    public void Graphics_sets_carry_a_subcategory_s_graphics_and_rows_are_checked()
    {
        var (f, plan, _) = Wall();
        f.Run("view.saveGraphicsSet", new JsonObject { ["id"] = plan, ["name"] = "Insulation green" });
        Sub(f, plan, "insulation", new JsonObject { ["fillColor"] = "#00ff00", ["lineWeight"] = 0.5 });
        // The set changed for every view that takes it; the weight kept as the pen it is.
        var other = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        var setId = (string)Views.Find(f.Model, plan)!.Attrs["graphicsSet"]!;
        f.Run("view.useGraphicsSet", new JsonObject { ["id"] = other, ["setId"] = setId });
        Assert.Equal("#00ff00", Band(f, other, "EPS insulation").Fill);
        var vis = (JsonObject)f.Session.Query("view.visibility", new JsonObject { ["id"] = other })!;
        Assert.Equal("0.5", (string?)vis["graphics"]!["categories"]!["walls"]!["sub"]!["insulation"]!["lineWeight"]);
        Assert.Equal(VisibilityGraphics.SubFields, vis["subcategoryFields"]!.AsArray().Select(x => (string)x!));
        Assert.Throws<CommandException>(() => Sub(f, plan, "insulation", new JsonObject { ["cutHatch"] = "NO_SUCH" }));
        Assert.Throws<CommandException>(() => Sub(f, plan, "insulation", new JsonObject { ["fillColor"] = "green" }));
        Assert.Throws<CommandException>(() => Sub(f, plan, "insulation", new JsonObject { ["lineWeight"] = "thick" }));
        Assert.Throws<CommandException>(() => Sub(f, plan, "insulation", new JsonObject { ["nope"] = true }));
    }

    [Fact]
    public void The_3D_takes_a_subcategory_s_halftone_and_colour_per_part()
    {
        var (f, _, wall) = Wall();
        Sub(f, "3d", "insulation", new JsonObject { ["halftone"] = true });
        Sub(f, "3d", "finishes", new JsonObject { ["fillColor"] = "#ff0000" });
        var looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject())!;
        var parts = looks[wall]!["parts"]!.AsObject();
        Assert.Equal(0.35, (double)parts["layer:2"]!["opacity"]!);
        Assert.Equal("#ff0000", (string?)parts["layer:0"]!["color"]);
        Assert.Null(parts["layer:1"]);
    }
}
