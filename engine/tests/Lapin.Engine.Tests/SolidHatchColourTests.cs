using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 88: a layer whose material is hatched Solid with a fill colour (insulation in solid yellow) draws in that
/// colour, in plans, sections, sheets and the 3D's caps drawn as a sheet, for walls and floors alike, and a surface's
/// solid hatch in an elevation the same; a solid hatch without a fill stays ink, and the other patterns are unchanged.
/// </summary>
public class SolidHatchColourTests
{
    const string Yellow = "#ffff00";

    static string Material(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    static void Graphics(Fixture f, string material, string kind, JsonObject g) => f.Run("material.update", new JsonObject { ["id"] = material, [kind] = g });

    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    /// <summary>The colours the cut's solid hatches are drawn in (the hatch layer's filled pens).</summary>
    static List<string> Solids(IEnumerable<Pen> pens, string layer = PenLayer.Hatch) =>
        pens.Where(p => p.Layer == layer && p.Fill != null && p.Loops.Count > 0).Select(p => p.Fill!).ToList();

    /// <summary>A concrete core with a mineral wool layer, the wool hatched Solid and filled yellow.</summary>
    static (Fixture F, string Wall) InsulatedWall()
    {
        var f = new Fixture();
        f.EveryLayerInPlans();
        var type = f.Type("Insulated", ("Mineral wool", 0.1, false), ("Concrete", 0.2, true));
        var wall = f.Wall(0, 0, 5, 0, type);
        Graphics(f, Material(f, "Mineral wool"), "section", new JsonObject { ["hatch"] = "SOLID", ["fill"] = Yellow });
        return (f, wall);
    }

    [Fact]
    public void A_wall_layer_hatched_solid_is_drawn_in_its_fill_colour_in_a_plan()
    {
        var (f, _) = InsulatedWall();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var pens = Render(f, plan).Pens;
        // Before the fix the solid lay over the fill in black: the plan showed a dark band, not the yellow.
        Assert.Equal([Yellow], Solids(pens));
        // The concrete core keeps its line hatch, in ink.
        Assert.Contains(pens, p => p.Layer == PenLayer.Hatch && p.Stroke == "#000000" && p.Segments.Count > 0);
    }

    [Fact]
    public void A_wall_layer_hatched_solid_is_drawn_in_its_fill_colour_in_a_section_and_on_a_sheet()
    {
        var (f, wall) = InsulatedWall();
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = wall, ["station"] = 2.5 })!["id"]!;
        Assert.Equal([Yellow], Solids(Render(f, section).Pens));
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = section, ["x"] = 200, ["y"] = 150 });
        var page = Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!);
        Assert.Contains(page.Pens, p => p.Fill == Yellow && p.Loops.Count > 0);
        Assert.DoesNotContain(page.Pens, p => p.Layer == PenLayer.Hatch && p.Fill == "#000000");
        Assert.NotEmpty(Sheets.Pdf(f.Session.Reader, [f.Model.Get(sheet)!]));
    }

    [Fact]
    public void A_cap_drawn_as_a_sheet_fills_a_solid_layer_in_its_fill_colour()
    {
        var (f, wall) = InsulatedWall();
        var item = f.Session.Caps([ClipPlane.Through(new Vec3(2.5, 0, 0), new Vec3(-1, 0, 0))], drawing: true).Single(c => c.NodeId == wall);
        var wool = item.Parts.Single(p => p.Material == Material(f, "Mineral wool"));
        Assert.Equal(Yellow, wool.Fill);
        Assert.Empty(wool.Hatch!);
    }

    [Fact]
    public void A_floor_layer_hatched_solid_is_drawn_in_its_fill_colour_in_a_section()
    {
        var f = new Fixture();
        var type = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Floor",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Screed", ["thickness"] = 0.05 },
                new JsonObject { ["material"] = "XPS insulation", ["thickness"] = 0.1 },
                new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.15, ["core"] = true }),
        })!["id"]!;
        f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 5), new JsonArray(0, 5)), ["typeId"] = type,
        });
        Graphics(f, Material(f, "XPS insulation"), "section", new JsonObject { ["hatch"] = "SOLID", ["fill"] = Yellow });
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 7) })!["id"]!;
        Assert.Equal([Yellow], Solids(Render(f, section).Pens));
    }

    [Fact]
    public void A_surface_hatched_solid_is_drawn_in_its_fill_colour_in_an_elevation()
    {
        var f = new Fixture();
        var type = f.Type("Rendered", ("Render", 0.02, false), ("Concrete", 0.2, true), ("Render", 0.02, false));
        f.Wall(0, 0, 5, 0, type);
        Graphics(f, Material(f, "Render"), "elevation", new JsonObject { ["hatch"] = "SOLID", ["fill"] = Yellow });
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        Assert.Equal([Yellow], Solids(Render(f, view).Pens, SurfaceHatches.Layer));
    }

    [Fact]
    public void A_solid_hatch_without_a_fill_stays_ink_and_a_line_hatch_keeps_its_fill_under_it()
    {
        var f = new Fixture();
        f.EveryLayerInPlans();
        var type = f.Type("Insulated", ("Mineral wool", 0.1, false), ("Concrete", 0.2, true));
        f.Wall(0, 0, 5, 0, type);
        var wool = Material(f, "Mineral wool");
        Graphics(f, wool, "section", new JsonObject { ["hatch"] = "SOLID" });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        Assert.Equal(["#000000"], Solids(Render(f, plan).Pens));
        // A line pattern over a yellow fill: the fill under it, the lines in ink, no solid.
        Graphics(f, wool, "section", new JsonObject { ["hatch"] = "LINES45", ["fill"] = Yellow });
        var pens = Render(f, plan).Pens;
        Assert.Empty(Solids(pens));
        Assert.Contains(pens, p => p.Layer == PenLayer.Fill && p.Fill == Yellow);
        Assert.Contains(pens, p => p.Layer == PenLayer.Hatch && p.Stroke == "#000000" && p.Segments.Count > 0);
    }

    [Fact]
    public void A_solid_hatch_takes_the_material_s_hatch_colour_over_its_fill_and_its_opacity()
    {
        // Decision 678: the hatch's own colour wins over the fill a solid is otherwise drawn in, at the hatch's opacity.
        var (f, _) = InsulatedWall();
        Graphics(f, Material(f, "Mineral wool"), "section", new JsonObject { ["hatchColor"] = "#ff8800", ["hatchOpacity"] = 50 });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        Assert.Equal(["#ff880080"], Solids(Render(f, plan).Pens));
        // Halftone alone on a solid with a fill: the fill washed halfway to white.
        Graphics(f, Material(f, "Mineral wool"), "section", new JsonObject { ["hatchColor"] = null, ["hatchOpacity"] = null, ["halftone"] = true });
        Assert.Equal([HexColor.Mix(Yellow, 0.5)], Solids(Render(f, plan).Pens));
    }
}
