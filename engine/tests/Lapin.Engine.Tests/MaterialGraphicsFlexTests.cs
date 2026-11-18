using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage 0b of docs/plan-material-graphics.md (decision 678), the flexible material configurator: on each material and kind
/// of view, the hatch's colour, weight, line type, opacity and halftone, the fill's opacity, a background hatch, and its
/// cut and separator lines reach the pens of plans, sections, elevations, the legend, the preview, DXF, IFC and Bonsai;
/// what is left unset draws as before; material.update checks them, takes several materials in one step and resets a field.
/// </summary>
public class MaterialGraphicsFlexTests
{
    static string Material(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    static void Set(Fixture f, string material, string kind, JsonObject g) => f.Run("material.update", new JsonObject { ["id"] = material, [kind] = g });

    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    /// <summary>A plastered concrete wall with an EPS layer in a plan: the concrete hatched CONCRETE, the EPS RIGID, the plaster GYPSUM.</summary>
    static (Fixture F, string Plan, string Wall) Wall()
    {
        var f = new Fixture();
        f.EveryLayerInPlans();
        var type = f.Type("Insulated", ("Gypsum plaster", 0.015, false), ("Concrete", 0.2, true), ("EPS insulation", 0.1, false));
        var wall = f.Wall(0, 0, 5, 0, type);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        return (f, plan, wall);
    }

    static List<Pen> Hatches(IEnumerable<Pen> pens, string layer = PenLayer.Hatch) => pens.Where(p => p.Layer == layer && p.Segments.Count > 0).ToList();

    [Fact]
    public void A_hatch_takes_its_colour_weight_line_type_opacity_and_halftone()
    {
        var (f, plan, _) = Wall();
        var concrete = Material(f, "Concrete");
        Set(f, concrete, "section", new JsonObject { ["hatchColor"] = "#cc0000", ["hatchWeight"] = 0.35, ["hatchPen"] = 1, ["hatchLineType"] = "dashed" });
        var pen = Hatches(Render(f, plan).Pens).Single(p => p.Stroke == "#cc0000");
        // The weight wins over the pen; the dashes are the line type's.
        Assert.Equal(0.35, pen.Width);
        Assert.Equal([3.0, 1.5], pen.Dash!);
        // The other materials' hatches as before: ink, 0.13, continuous.
        Assert.Contains(Hatches(Render(f, plan).Pens), p => p.Stroke == "#000000" && p.Width == Pens.Default.Hatch && p.Dash == null);
        Set(f, concrete, "section", new JsonObject { ["hatchOpacity"] = 50 });
        Assert.Contains(Hatches(Render(f, plan).Pens), p => p.Stroke == "#cc000080");
        Set(f, concrete, "section", new JsonObject { ["hatchColor"] = null, ["hatchOpacity"] = null, ["halftone"] = true });
        Assert.Contains(Hatches(Render(f, plan).Pens), p => p.Stroke == "#808080" && p.Width == 0.35);
    }

    [Fact]
    public void A_fill_takes_its_opacity_and_a_background_hatch_draws_under_the_hatch()
    {
        var (f, plan, _) = Wall();
        var concrete = Material(f, "Concrete");
        Set(f, concrete, "section", new JsonObject
        {
            ["fill"] = "#ffff00", ["fillOpacity"] = 50,
            ["backgroundHatch"] = new JsonObject { ["hatch"] = "lines45", ["hatchColor"] = "#0000ff", ["hatchWeight"] = 0.18, ["hatchScale"] = 2 },
        });
        var pens = Render(f, plan).Pens;
        Assert.Contains(pens, p => p.Layer == PenLayer.Fill && p.Fill == "#ffff0080");
        var hatches = Hatches(pens);
        var bg = hatches.FindIndex(p => p.Stroke == "#0000ff" && p.Width == 0.18);
        var fg = hatches.FindIndex(p => p.Stroke == "#000000");
        Assert.True(bg >= 0 && fg > bg, "the background hatch is drawn, under the hatch");
        // Its own scale: twice the 45° lines' spacing, so half as many strokes as the same band at 1.
        Assert.Equal("LINES45", MaterialGraphics.Resolve(f.Model.Get(concrete), "section").Background!.Hatch);
        // A background with no pattern is none.
        Set(f, concrete, "section", new JsonObject { ["backgroundHatch"] = new JsonObject { ["hatchColor"] = "#0000ff" } });
        Assert.Null(f.Model.Get(concrete)!.Attrs["section"]!["backgroundHatch"]);
    }

    [Fact]
    public void A_material_s_cut_and_separator_lines_draw_its_outline_and_its_lines_against_the_other_layers()
    {
        var (f, plan, _) = Wall();
        var before = Render(f, plan).Pens;
        var concrete = Material(f, "Concrete");
        Set(f, concrete, "section", new JsonObject
        {
            ["cutLine"] = new JsonObject { ["color"] = "#00aa00", ["weight"] = 0.7 },
            ["separatorLine"] = new JsonObject { ["color"] = "#aa00aa", ["weight"] = 0.18, ["lineType"] = "dashed" },
        });
        var pens = Render(f, plan).Pens;
        // The concrete's ends are its outline: cut lines of its own.
        var outline = pens.Single(p => p.Layer == PenLayer.Cut && p.Stroke == "#00aa00");
        Assert.Equal(0.7, outline.Width);
        Assert.Equal(2, outline.Segments.Count);
        // Its two lines against the plaster and the EPS: its separator line.
        var sep = pens.Single(p => p.Layer == PenLayer.Cut && p.Stroke == "#aa00aa");
        Assert.Equal(0.18, sep.Width);
        Assert.Equal([3.0, 1.5], sep.Dash!);
        Assert.Equal(2, sep.Segments.Count);
        Assert.Equal(10, sep.Segments.Sum(s => (s.B - s.A).Length), 6);
        // The other layers' outlines as before.
        Assert.Contains(pens, p => p.Layer == PenLayer.Cut && p.Stroke == "#000000" && p.Width == Pens.Default.Cut);
        // Where two materials set one, the heavier wins: the EPS's 0.5 over the concrete's 0.18 on their line.
        Set(f, Material(f, "EPS insulation"), "section", new JsonObject { ["separatorLine"] = new JsonObject { ["color"] = "#ff8800", ["weight"] = 0.5 } });
        pens = Render(f, plan).Pens;
        Assert.Single(pens.Single(p => p.Stroke == "#aa00aa").Segments);
        Assert.Single(pens.Single(p => p.Stroke == "#ff8800" && p.Layer == PenLayer.Cut).Segments);
        // Unset again (null): the drawing is as it was.
        Set(f, concrete, "section", new JsonObject { ["cutLine"] = null, ["separatorLine"] = null });
        Set(f, Material(f, "EPS insulation"), "section", new JsonObject { ["reset"] = new JsonArray("separatorLine") });
        Assert.Equal(MaterialGraphicsReferenceTests.Snapshot(before), MaterialGraphicsReferenceTests.Snapshot(Render(f, plan).Pens));
    }

    [Fact]
    public void A_material_that_sets_none_draws_as_before_and_its_list_says_nothing_new()
    {
        var (f, plan, _) = Wall();
        var before = MaterialGraphicsReferenceTests.Snapshot(Render(f, plan).Pens);
        var concrete = Material(f, "Concrete");
        var resolved = MaterialGraphics.Resolve(f.Model.Get(concrete), "section");
        Assert.False(resolved.Flex);
        foreach (var key in ViewGraphics.FlexFields) Assert.False(resolved.ToJson().ContainsKey(key));
        // Every field set, then each taken back: the same pens.
        Set(f, concrete, "section", new JsonObject
        {
            ["hatchColor"] = "#123456", ["hatchWeight"] = 0.5, ["hatchLineType"] = "dotted", ["hatchOpacity"] = 30, ["halftone"] = true, ["fillOpacity"] = 20,
            ["backgroundHatch"] = new JsonObject { ["hatch"] = "CROSS" }, ["cutLine"] = new JsonObject { ["weight"] = 1 }, ["separatorLine"] = new JsonObject { ["color"] = "#ff0000" },
        });
        Assert.NotEqual(before, MaterialGraphicsReferenceTests.Snapshot(Render(f, plan).Pens));
        Set(f, concrete, "section", new JsonObject { ["reset"] = new JsonArray(ViewGraphics.FlexFields.Select(x => (JsonNode)x).ToArray()) });
        Assert.Equal(before, MaterialGraphicsReferenceTests.Snapshot(Render(f, plan).Pens));
        Assert.Null(f.Model.Get(concrete)!.Attrs["section"]);
    }

    [Fact]
    public void A_plan_takes_the_section_s_fields_unless_it_sets_its_own()
    {
        var f = new Fixture();
        var brick = (string)f.Run("material.create", new JsonObject { ["name"] = "Brick" })!["id"]!;
        Set(f, brick, "section", new JsonObject { ["hatchColor"] = "#aa0000", ["cutLine"] = new JsonObject { ["color"] = "#00aa00", ["weight"] = 0.7 } });
        Set(f, brick, "plan", new JsonObject { ["cutLine"] = new JsonObject { ["weight"] = 0.35 } });
        var plan = MaterialGraphics.Resolve(f.Model.Get(brick), "plan");
        Assert.Equal("#aa0000", plan.HatchColor);
        Assert.Equal(new MaterialLine("#00aa00", 0.35), plan.CutLine);
        Assert.Null(MaterialGraphics.Resolve(f.Model.Get(brick), "elevation").HatchColor);
    }

    [Fact]
    public void Material_update_checks_the_new_fields()
    {
        var f = new Fixture(new Lapin.Architecture.ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        var m = (string)f.Run("material.create", new JsonObject { ["name"] = "Brick" })!["id"]!;
        foreach (var bad in new JsonObject[]
        {
            new() { ["hatchColor"] = "red" }, new() { ["hatchWeight"] = 0 }, new() { ["hatchWeight"] = 9 }, new() { ["hatchOpacity"] = 101 }, new() { ["fillOpacity"] = -1 },
            new() { ["hatchLineType"] = "zigzag" }, new() { ["halftone"] = "yes" }, new() { ["backgroundHatch"] = new JsonObject { ["hatch"] = "NOPE" } },
            new() { ["backgroundHatch"] = new JsonObject { ["pen"] = 1 } }, new() { ["cutLine"] = new JsonObject { ["width"] = 1 } }, new() { ["separatorLine"] = "heavy" },
            new() { ["reset"] = new JsonArray("colour") },
        })
            Assert.Throws<CommandException>(() => Set(f, m, "section", bad));
        Assert.Null(f.Model.Get(m)!.Attrs["section"]);
        // A project's own line type is one.
        f.Run("standards.setLineType", new JsonObject { ["name"] = "Mine", ["dashes"] = new JsonArray(2, 1) });
        Set(f, m, "section", new JsonObject { ["hatchLineType"] = "mine" });
        Assert.Equal("Mine", (string?)f.Model.Get(m)!.Attrs["section"]!["hatchLineType"]);
    }

    [Fact]
    public void Several_materials_take_one_change_in_one_undo_step_and_reset_takes_a_field_back()
    {
        var f = new Fixture();
        var a = (string)f.Run("material.create", new JsonObject { ["name"] = "Brick" })!["id"]!;
        var b = (string)f.Run("material.create", new JsonObject { ["name"] = "Concrete" })!["id"]!;
        var r = f.Run("material.update", new JsonObject { ["ids"] = new JsonArray(a, b), ["section"] = new JsonObject { ["hatchColor"] = "#ff0000", ["hatch"] = "CROSS" } });
        Assert.Equal(2, (int)r!["changed"]!);
        Assert.All(new[] { a, b }, id => Assert.Equal(("#ff0000", "CROSS"), (MaterialGraphics.Resolve(f.Model.Get(id), "section").HatchColor, MaterialGraphics.Resolve(f.Model.Get(id), "section").Hatch)));
        f.Session.Undo();
        Assert.All(new[] { a, b }, id => Assert.Null(f.Model.Get(id)!.Attrs["section"]));
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["ids"] = new JsonArray(a, b), ["name"] = "Same" }));
        // reset: back to what the name suggests (null on hatch stays "none").
        f.Session.Redo();
        Set(f, a, "section", new JsonObject { ["hatch"] = null });
        Assert.Null(MaterialGraphics.Resolve(f.Model.Get(a), "section").Hatch);
        Set(f, a, "section", new JsonObject { ["reset"] = new JsonArray("hatch") });
        Assert.Equal("MASONRY", MaterialGraphics.Resolve(f.Model.Get(a), "section").Hatch);
        Assert.Equal("#ff0000", MaterialGraphics.Resolve(f.Model.Get(a), "section").HatchColor);
    }

    [Fact]
    public void An_elevation_s_surfaces_take_the_hatch_colour_and_the_background_hatch()
    {
        var f = new Fixture();
        var type = f.Type("Clad", ("Facing brick", 0.1, false), ("Concrete", 0.2, true), ("Facing brick", 0.1, false));
        f.Wall(0, 0, 5, 0, type);
        Set(f, Material(f, "Facing brick"), "elevation", new JsonObject
        {
            ["hatchColor"] = "#884422", ["hatchWeight"] = 0.25, ["fill"] = "#ffeecc", ["fillOpacity"] = 40,
            ["backgroundHatch"] = new JsonObject { ["hatch"] = "LINES45", ["hatchColor"] = "#cccccc" },
        });
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var pens = Render(f, view).Pens;
        var surface = Hatches(pens, SurfaceHatches.Layer);
        var bg = surface.FindIndex(p => p.Stroke == "#cccccc");
        var fg = surface.FindIndex(p => p.Stroke == "#884422" && p.Width == 0.25);
        Assert.True(bg >= 0 && fg > bg);
        Assert.Contains(pens, p => p.Layer == SurfaceHatches.FillLayer && p.Fill == "#ffeecc66");
    }

    [Fact]
    public void The_preview_draws_a_sample_by_the_drawings_own_path()
    {
        var f = new Fixture();
        var m = (string)f.Run("material.create", new JsonObject { ["name"] = "Brick" })!["id"]!;
        Set(f, m, "section", new JsonObject { ["separatorLine"] = new JsonObject { ["color"] = "#00aa00" } });
        var r = f.Session.Query("material.preview", new JsonObject { ["id"] = m, ["kind"] = "section", ["scale"] = 20, ["graphics"] = new JsonObject { ["hatchColor"] = "#ff0000" } })!;
        var pens = r["pens"]!.AsArray();
        // The draft's colour on its hatch, the saved separator line on its two lines against the neighbours, the outline in ink.
        Assert.Contains(pens, p => (string?)p!["layer"] == "HATCH" && (string?)p["stroke"] == "#ff0000");
        Assert.Contains(pens, p => (string?)p!["layer"] == "CUT" && (string?)p["stroke"] == "#00aa00" && p["segments"]!.AsArray().Count == 8);
        Assert.Contains(pens, p => (string?)p!["layer"] == "CUT" && (string?)p["stroke"] == "#000000");
        Assert.Equal("#ff0000", (string?)r["resolved"]!["hatchColor"]);
        // 60 × 15 mm at 1:20 between neighbours half as thick: 1.2 m by 0.6 m.
        Assert.Equal(new[] { 0, 0, 1.2, 0.6 }, r["bounds"]!.AsArray().Select(x => Math.Round((double)x!, 6)));
        // The draft is not saved; a material not yet made previews by its name.
        Assert.Null(MaterialGraphics.Resolve(f.Model.Get(m), "section").HatchColor);
        var named = f.Session.Query("material.preview", new JsonObject { ["name"] = "Concrete C25/30", ["kind"] = "plan", ["scale"] = 50, ["layout"] = "band" })!;
        Assert.Equal("CONCRETE", (string?)named["resolved"]!["hatch"]);
        Assert.Throws<CommandException>(() => f.Session.Query("material.preview", new JsonObject { ["kind"] = "cut" }));
    }

    [Fact]
    public void The_material_legend_s_swatch_draws_the_hatch_colour_and_background_hatch()
    {
        var f = new Fixture(new Lapin.Architecture.ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        var type = f.Type("Concrete 20", ("Concrete", 0.2, true));
        f.Wall(0, 0, 8, 0, type);
        Set(f, Material(f, "Concrete"), "section", new JsonObject { ["hatchColor"] = "#ff0000", ["backgroundHatch"] = new JsonObject { ["hatch"] = "LINES45", ["hatchColor"] = "#0000ff" } });
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = "plan:" + f.Storey, ["x"] = 150, ["y"] = 140 });
        f.Run("sheet.addLegend", new JsonObject { ["viewId"] = PaperViews.IdOf(s), ["at"] = new JsonArray(0.3, 0.25) });
        var pens = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!).Pens;
        Assert.Contains(pens, p => p.Layer == "LEGEND" && p.Stroke == "#ff0000" && p.Segments.Count > 0);
        Assert.Contains(pens, p => p.Layer == "LEGEND" && p.Stroke == "#0000ff" && p.Segments.Count > 0);
    }

    [Fact]
    public void Dxf_carries_the_hatch_colour_weight_and_opacity()
    {
        var (f, plan, _) = Wall();
        Set(f, Material(f, "Concrete"), "section", new JsonObject { ["hatchColor"] = "#cc0000", ["hatchWeight"] = 0.35, ["hatchOpacity"] = 50 });
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = plan, ["x"] = 150, ["y"] = 140 });
        var dxf = Encoding.Latin1.GetString(Sheets.Dxf(f.Session.Reader, f.Model.Get(s)!));
        // The colour as a true colour (0xcc0000), the weight 0.35 mm, the opacity as transparency (alpha 128).
        Assert.Contains("420\n" + 0xcc0000, dxf);
        Assert.Contains("370\n35\n", dxf);
        Assert.Contains("440\n" + (0x02000000 | 128), dxf);
    }

    [Fact]
    public void Ifc_carries_the_fill_area_style_both_ways()
    {
        var f = new Fixture();
        var type = f.Type("Brick 25", ("Brick masonry", 0.25, true));
        f.Wall(0, 0, 5, 0, type);
        var brick = Material(f, "Brick masonry");
        Set(f, brick, "section", new JsonObject
        {
            ["hatch"] = "MASONRY", ["hatchScale"] = 2, ["hatchColor"] = "#aa0000", ["hatchWeight"] = 0.25, ["fill"] = "#ffeedd",
            ["backgroundHatch"] = new JsonObject { ["hatch"] = "CROSS", ["hatchColor"] = "#0000aa" },
        });
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "flex.ifc", new IfcExportOptions());
        var text = Encoding.UTF8.GetString(ms.ToArray());
        var style = text.Split('\n').Single(l => l.Contains("IFCFILLAREASTYLE('Section hatch'"));
        // A fill colour and two hatchings, each with its curve style named after its pattern.
        Assert.Equal(2, text.Split('\n').Count(l => l.Contains("IFCFILLAREASTYLEHATCHING(")));
        Assert.Contains(text.Split('\n'), l => l.Contains("IFCCURVESTYLE('MASONRY'") && l.Contains("IFCPOSITIVELENGTHMEASURE(0.00025)"));
        Assert.Contains(text.Split('\n'), l => l.Contains("IFCCURVESTYLE('Background CROSS'"));
        Assert.DoesNotContain(text.Split('\n'), l => l.Contains("IFCFILLAREASTYLE('Plan hatch'"));
        // Back into Lapin by its intent, all of it; and from another program (no Lapin_Intent), from the fill area style.
        foreach (var foreign in new[] { false, true })
        {
            var ifc = foreign ? text.Replace("'Lapin_Intent'", "'Other_Set'") : text;
            var path = Path.Combine(Path.GetTempPath(), $"lapin-flex-{Guid.NewGuid():N}.ifc");
            File.WriteAllText(path, ifc);
            try
            {
                var g = new Fixture();
                IfcImport.Run(g.Session, path, null, out _);
                var back = g.Model.OfClass(IfcClass.Material).First(m => m.Name == "Brick masonry");
                var r = MaterialGraphics.Resolve(back, "section");
                Assert.Equal(("MASONRY", 2.0, "#aa0000", 0.25, "#ffeedd"), (r.Hatch, Math.Round(r.HatchScale, 3), r.HatchColor, r.HatchWeight!.Value, r.Fill));
                Assert.Equal(("CROSS", "#0000aa"), (r.Background!.Hatch, r.Background.HatchColor));
            }
            finally { File.Delete(path); }
        }
    }

    [Fact]
    public void Bonsai_s_patterns_take_the_hatch_colour_and_the_background_hatch()
    {
        var f = new Fixture();
        var brick = (string)f.Run("material.create", new JsonObject { ["name"] = "Brick" })!["id"]!;
        Set(f, brick, "section", new JsonObject
        {
            ["hatchColor"] = "#aa0000", ["backgroundHatch"] = new JsonObject { ["hatch"] = "LINES45", ["hatchColor"] = "#0000aa" },
            ["cutLine"] = new JsonObject { ["color"] = "#00aa00", ["weight"] = 0.7 },
        });
        var sheet = BonsaiStyles.Of(f.Model);
        Assert.Contains("stroke=\"#aa0000\"", sheet.Patterns);
        Assert.Contains("stroke=\"#0000aa\"", sheet.Patterns);
        Assert.Contains(sheet.Css.Split('\n'), l => l.StartsWith(".cut.material-Brick", StringComparison.Ordinal) && l.Contains("stroke: #00aa00") && l.Contains("stroke-width: 0.7"));
    }
}
