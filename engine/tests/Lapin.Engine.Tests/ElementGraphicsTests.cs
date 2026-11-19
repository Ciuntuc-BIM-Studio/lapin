using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Decision 718 (issue #88): an element's graphics overridden in a view, the third level over its material and its view's
/// category: the cut hatch (pattern or none, scale, angle), the cut fill, the surface hatch and fill of an elevation, the hatch
/// colour and halftone, the cut and projection lines, the whole element halftone; kept on the view (not its set), one undo step,
/// drawn by the engine so sheets, PDF and DXF follow.
/// </summary>
public class ElementGraphicsTests
{
    static string Mat(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;
    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    static (Fixture F, string Plan, string A, string B) Walls()
    {
        var f = new Fixture();
        var type = f.Type("Concrete 20", ("Concrete", 0.2, true));
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(0, 4, 5, 4, type);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        return (f, plan, a, b);
    }

    static ResolvedMaterial? Band(Fixture f, string view, string element, string kind = "plan", double scale = 50)
    {
        var v = Views.Find(f.Model, view)!;
        var cut = Render(f, view).Generated!.Cuts.First(c => c.Element == element);
        return ViewMaterialGraphics.For(f.Model, v, kind, scale).Band(cut);
    }

    static void Set(Fixture f, string view, string element, JsonObject? value) =>
        f.Run("view.elementGraphics", new JsonObject { ["id"] = view, ["ids"] = new JsonArray(element), ["value"] = value });

    [Fact]
    public void An_element_s_cut_hatch_and_fill_in_one_view_leave_the_others_and_its_material_alone()
    {
        var (f, plan, a, b) = Walls();
        var other = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        Assert.Equal("CONCRETE", Band(f, plan, a)!.Hatch);
        Set(f, plan, a, new JsonObject { ["cutHatch"] = "none", ["cutFill"] = "#000000" });
        var band = Band(f, plan, a)!;
        Assert.Equal(((string?)null, "#000000", true, true), (band.Hatch, band.Fill, band.KeepsHatch, band.KeepsFill));
        Assert.Contains(Render(f, plan).Pens, p => p.Layer == PenLayer.Fill && p.Fill == "#000000");
        // The other wall of the type, the other view and the material as they were.
        Assert.Equal("CONCRETE", Band(f, plan, b)!.Hatch);
        Assert.Equal("CONCRETE", Band(f, other, a)!.Hatch);
        Assert.Equal("CONCRETE", MaterialGraphics.Resolve(f.Model.Get(Mat(f, "Concrete")), "plan").Hatch);
        // Over the category's cut hatch and fill (the more particular).
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = new JsonObject { ["fillColor"] = "#ff00ff" } });
        var pens = Render(f, plan).Pens;
        Assert.Contains(pens, p => p.Fill == "#000000");
        Assert.Contains(pens, p => p.Fill == "#ff00ff");
        // One undo step takes it back.
        f.Session.Undo();
        f.Session.Undo();
        Assert.Equal("CONCRETE", Band(f, plan, a)!.Hatch);
        Assert.Null(Views.Find(f.Model, plan)!.Attrs[ElementGraphics.Key]);
    }

    [Fact]
    public void A_hatch_s_scale_and_angle_change_the_material_s_own_pattern_or_the_one_given()
    {
        var (f, plan, a, _) = Walls();
        f.Run("view.elementGraphics", new JsonObject { ["id"] = plan, ["ids"] = new JsonArray(a), ["patch"] = new JsonObject { ["cutHatchScale"] = 2.5, ["cutHatchAngle"] = 30 } });
        var band = Band(f, plan, a)!;
        Assert.Equal(("CONCRETE", 2.5, 30.0), (band.Hatch, band.HatchScale, band.HatchAngle));
        f.Run("view.elementGraphics", new JsonObject { ["id"] = plan, ["ids"] = new JsonArray(a), ["patch"] = new JsonObject { ["cutHatch"] = "cross", ["hatchColor"] = "#AA0000" } });
        band = Band(f, plan, a)!;
        Assert.Equal(("CROSS", 2.5, 30.0, "#aa0000"), (band.Hatch, band.HatchScale, band.HatchAngle, band.HatchColor));
        Assert.Contains(Render(f, plan).Pens, p => p.Layer == PenLayer.Hatch && p.Stroke == "#aa0000" && p.Segments.Count > 0);
        // A field patched to null goes; the rest stays.
        f.Run("view.elementGraphics", new JsonObject { ["id"] = plan, ["ids"] = new JsonArray(a), ["patch"] = new JsonObject { ["cutHatchScale"] = null } });
        Assert.Equal(("CROSS", 1.0), (Band(f, plan, a)!.Hatch, Band(f, plan, a)!.HatchScale));
        var rows = f.Session.Query("view.elementGraphicsOf", new JsonObject { ["id"] = plan })!["rows"]!;
        Assert.Equal("CROSS", (string?)rows[a]!["cutHatch"]);
        Assert.Null(rows[a]!["cutHatchScale"]);
    }

    [Fact]
    public void A_strip_foundation_s_hatch_scale_in_a_section_and_its_hatched_elevation_taken_off()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var strip = (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 2), ["scale"] = 20 })!["id"]!;
        var before = Band(f, section, strip, "section", 20)!;
        Set(f, section, strip, new JsonObject { ["cutHatchScale"] = 3 });
        var after = Band(f, section, strip, "section", 20)!;
        Assert.Equal((before.Hatch, 3.0), (after.Hatch, after.HatchScale));
        // Its faces seen in an elevation: the concrete given a surface hatch, then taken off for this footing alone.
        f.Run("material.update", new JsonObject { ["id"] = f.Session.Geometry(strip)!.Parts.Select(p => p.Material).OfType<string>().First(), ["elevation"] = new JsonObject { ["hatch"] = "CROSS", ["hatchColor"] = "#00aa00" } });
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south", ["scale"] = 50 })!["id"]!;
        Assert.Contains(Render(f, south).Pens, p => p.Layer == SurfaceHatches.Layer && p.Stroke == "#00aa00" && p.Segments.Count > 0);
        Set(f, south, strip, new JsonObject { ["surfaceHatch"] = "none", ["surfaceFill"] = "none" });
        Assert.DoesNotContain(Render(f, south).Pens, p => p.Layer == SurfaceHatches.Layer && p.Stroke == "#00aa00" && p.Segments.Count > 0);
    }

    [Fact]
    public void Its_lines_and_halftone_lie_over_the_material_s_and_the_category_s()
    {
        var (f, plan, a, b) = Walls();
        f.Run("material.update", new JsonObject { ["id"] = Mat(f, "Concrete"), ["plan"] = new JsonObject { ["cutLine"] = new JsonObject { ["color"] = "#0000ff", ["weight"] = 0.5 } } });
        Assert.Contains(Render(f, plan).Pens, p => p.Layer == PenLayer.Cut && p.Stroke == "#0000ff");
        Set(f, plan, a, new JsonObject { ["cutLine"] = new JsonObject { ["color"] = "#ff0000", ["weight"] = 1.0, ["lineType"] = "dashed" } });
        var pens = Render(f, plan).Pens;
        Assert.Contains(pens, p => p.Layer == PenLayer.Cut && p.Stroke == "#ff0000" && p.Width == 1.0 && p.Dash is { Length: > 0 });
        // The other wall keeps its material's line.
        Assert.Contains(pens, p => p.Layer == PenLayer.Cut && p.Stroke == "#0000ff");
        Set(f, plan, a, new JsonObject { ["halftone"] = true });
        var vg = VisibilityGraphics.Of(f.Model, Views.Find(f.Model, plan)!);
        Assert.Equal(Links.Halftone, vg.ColorOf(f.Model.Get(a)!));
        Assert.Null(vg.ColorOf(f.Model.Get(b)!));
        Assert.Contains(Render(f, plan).Pens, p => p.Layer == PenLayer.Cut && p.Stroke == Links.Halftone);
    }

    [Fact]
    public void It_stays_on_the_view_that_takes_a_set_reaches_every_view_at_once_and_rows_are_checked()
    {
        var (f, plan, a, b) = Walls();
        var set = (string)f.Run("view.saveGraphicsSet", new JsonObject { ["id"] = plan, ["name"] = "Shared" })!["id"]!;
        var other = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        f.Run("view.useGraphicsSet", new JsonObject { ["id"] = other, ["setId"] = set });
        Set(f, plan, a, new JsonObject { ["cutFill"] = "#00ff00" });
        // The set has no element rows: the other view of the set is untouched.
        Assert.Null(Band(f, other, a)!.Fill);
        Assert.Equal("#00ff00", Band(f, plan, a)!.Fill);
        // Every plan, section and elevation at once, both walls, one step.
        f.Run("view.elementGraphics", new JsonObject { ["scope"] = "all", ["ids"] = new JsonArray(a, b), ["value"] = new JsonObject { ["cutHatch"] = "SOLID", ["cutFill"] = "#111111" } });
        Assert.Equal("#111111", Band(f, other, b)!.Fill);
        f.Session.Undo();
        Assert.Null(Band(f, other, b)!.Fill);
        Assert.Throws<CommandException>(() => Set(f, plan, a, new JsonObject { ["cutHatch"] = "NO_SUCH" }));
        Assert.Throws<CommandException>(() => Set(f, plan, a, new JsonObject { ["cutFill"] = "black" }));
        Assert.Throws<CommandException>(() => Set(f, plan, a, new JsonObject { ["cutHatchScale"] = 0 }));
        Assert.Throws<CommandException>(() => Set(f, plan, a, new JsonObject { ["cutLine"] = new JsonObject { ["weight"] = 9 } }));
        Assert.Throws<CommandException>(() => Set(f, plan, a, new JsonObject { ["nope"] = 1 }));
        Assert.Throws<CommandException>(() => Set(f, plan, "nothing", new JsonObject { ["halftone"] = true }));
        // Cleared: the view draws as before and keeps no rows.
        Set(f, plan, a, null);
        Assert.Null(Views.Find(f.Model, plan)!.Attrs[ElementGraphics.Key]);
        Assert.False(VisibilityGraphics.Of(f.Model, Views.Find(f.Model, plan)!).HasElementGraphics);
    }
}
