using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage 5 of docs/plan-material-graphics.md: a view's filters change the hatch and the fill of the elements they take
/// (cutHatch, cutFill, hatchColor, hatchHalftone, cutLine; surfaceHatch, surfaceFill for an elevation's faces), field by field,
/// the first filter in the view's list giving each; they lie over the subcategory, the style's set and the category; rows are
/// checked; a view whose filters give none of them draws as before.
/// </summary>
public class FilterHatchTests
{
    static string Mat(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    /// <summary>Two concrete walls, the first load-bearing and rated EI 60, the second neither; a plan at 1:50.</summary>
    static (Fixture F, string Plan, string Bearing, string Partition) Walls()
    {
        var f = new Fixture();
        var type = f.Type("Concrete 20", ("Concrete", 0.2, true));
        var bearing = f.Wall(0, 0, 5, 0, type);
        var partition = f.Wall(0, 4, 5, 4, type);
        f.Run("element.setParam", new JsonObject { ["id"] = bearing, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        f.Run("element.setParam", new JsonObject { ["id"] = bearing, ["pset"] = "Pset_WallCommon", ["name"] = "FireRating", ["value"] = "EI 60" });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        return (f, plan, bearing, partition);
    }

    static string Filter(Fixture f, string name, string param, string op, string value) => (string)f.Run("viewFilter.create", new JsonObject
    {
        ["name"] = name,
        ["rules"] = new JsonArray(new JsonObject { ["field"] = "param", ["name"] = param, ["op"] = op, ["value"] = value }),
    })!["id"]!;

    static void Rows(Fixture f, string view, params JsonObject[] rows) =>
        f.Run("view.graphics", new JsonObject { ["id"] = view, ["filters"] = new JsonArray(rows.Select(r => (JsonNode)r).ToArray()) });

    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    static ResolvedMaterial? Band(Fixture f, string view, string wall)
    {
        var v = Views.Find(f.Model, view)!;
        var cut = Render(f, view).Generated!.Cuts.First(c => c.Element == wall);
        return ViewMaterialGraphics.For(f.Model, v, "plan", 50).Band(cut);
    }

    [Fact]
    public void Load_bearing_walls_cut_solid_black()
    {
        var (f, plan, bearing, partition) = Walls();
        var lb = Filter(f, "Load-bearing", "LoadBearing", "equals", "yes");
        Assert.Equal(1, (int)f.Session.Query("viewFilter.count", new JsonObject { ["id"] = lb })!);
        Rows(f, plan, new JsonObject { ["id"] = lb, ["cutHatch"] = "none", ["cutFill"] = "#000000" });
        var b = Band(f, plan, bearing)!;
        Assert.Equal((null, "#000000", "Load-bearing"), (b.Hatch, b.Fill, b.Filters));
        // The partition keeps its concrete.
        Assert.Equal("CONCRETE", Band(f, plan, partition)!.Hatch);
        var pens = Render(f, plan).Pens;
        Assert.Contains(pens, p => p.Layer == PenLayer.Fill && p.Fill == "#000000");
        // The filter's colour for the lines no longer pales the fill it asks for.
        Rows(f, plan, new JsonObject { ["id"] = lb, ["color"] = "#ff0000", ["cutHatch"] = "none", ["cutFill"] = "#000000" });
        pens = Render(f, plan).Pens;
        Assert.Contains(pens, p => p.Layer == PenLayer.Fill && p.Fill == "#000000");
        Assert.Contains(pens, p => p.Layer == PenLayer.Cut && p.Stroke == "#ff0000");
    }

    [Fact]
    public void Fire_rated_walls_hatch_red_and_the_rest_draws_as_before()
    {
        var (f, plan, bearing, partition) = Walls();
        var fire = Filter(f, "Fire rated", "FireRating", "beginsWith", "EI");
        Rows(f, plan, new JsonObject { ["id"] = fire, ["cutHatch"] = "cross", ["hatchColor"] = "#ff0000", ["cutLine"] = new JsonObject { ["color"] = "#ff0000", ["weight"] = 0.7 } });
        var b = Band(f, plan, bearing)!;
        Assert.Equal(("CROSS", 1.0, "#ff0000", true), (b.Hatch, b.HatchScale, b.HatchColor, b.KeepsHatch));
        Assert.Equal(new MaterialLine("#ff0000", 0.7), b.CutLine);
        var pens = Render(f, plan).Pens;
        Assert.Contains(pens, p => p.Layer == PenLayer.Hatch && p.Stroke == "#ff0000" && p.Segments.Count > 0);
        Assert.Contains(pens, p => p.Layer == PenLayer.Hatch && p.Stroke == "#000000" && p.Segments.Count > 0);
        Assert.Contains(pens, p => p.Layer == PenLayer.Cut && p.Stroke == "#ff0000" && p.Width == 0.7);
        // The two walls of one material do not join while the filter draws them apart (a third, beside the first, would).
        Assert.NotEqual(b.JoinAs, Band(f, plan, partition)!.JoinAs);
        // The hatch halftone alone washes the material's own hatch.
        Rows(f, plan, new JsonObject { ["id"] = fire, ["hatchHalftone"] = true });
        b = Band(f, plan, bearing)!;
        Assert.Equal(("CONCRETE", true), (b.Hatch, b.Halftone));
    }

    [Fact]
    public void A_filter_lies_over_the_subcategory_the_set_and_the_category()
    {
        var (f, plan, bearing, partition) = Walls();
        var set = (string)f.Run("materialGraphicsSet.create", new JsonObject
        {
            ["name"] = "Plain",
            ["entries"] = new JsonArray(new JsonObject { ["match"] = new JsonObject(), ["section"] = new JsonObject { ["hatch"] = "LINES45", ["fill"] = "#0000ff" } }),
        })!["id"]!;
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["materialGraphicsSet"] = set });
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "walls", ["sub"] = "core", ["value"] = new JsonObject { ["cutHatch"] = "GRAVEL", ["fillColor"] = "#00ff00" } });
        Assert.Equal(("GRAVEL", "#00ff00"), (Band(f, plan, bearing)!.Hatch, Band(f, plan, bearing)!.Fill));
        var lb = Filter(f, "Load-bearing", "LoadBearing", "equals", "yes");
        Rows(f, plan, new JsonObject { ["id"] = lb, ["cutHatch"] = "CROSS" });
        // The filter's hatch over the subcategory's; its fill, which the filter leaves, the subcategory's.
        var b = Band(f, plan, bearing)!;
        Assert.Equal(("CROSS", "#00ff00", set), (b.Hatch, b.Fill, b.GraphicsSet));
        Assert.Equal("GRAVEL", Band(f, plan, partition)!.Hatch);
        // The category's cut hatch and fill colour do not lie over the filter's (the more particular).
        Rows(f, plan, new JsonObject { ["id"] = lb, ["cutHatch"] = "SOLID", ["cutFill"] = "#123456" });
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "walls", ["value"] = new JsonObject { ["fillColor"] = "#ff00ff", ["cutHatch"] = "DOTS" } });
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "walls", ["sub"] = "core", ["value"] = null });
        var pens = Render(f, plan).Pens;
        Assert.Contains(pens, p => p.Fill == "#123456");
        Assert.Contains(pens, p => p.Fill == "#ff00ff");
    }

    [Fact]
    public void The_first_filter_in_the_view_s_list_gives_each_field()
    {
        var (f, plan, bearing, _) = Walls();
        var lb = Filter(f, "Load-bearing", "LoadBearing", "equals", "yes");
        var fire = Filter(f, "Fire rated", "FireRating", "beginsWith", "EI");
        Rows(f, plan, new JsonObject { ["id"] = lb, ["cutFill"] = "#ff0000" }, new JsonObject { ["id"] = fire, ["cutFill"] = "#0000ff", ["cutHatch"] = "CROSS" });
        var b = Band(f, plan, bearing)!;
        Assert.Equal(("CROSS", "#ff0000", "Load-bearing + Fire rated"), (b.Hatch, b.Fill, b.Filters));
        // Reordered: the other one's fill.
        Rows(f, plan, new JsonObject { ["id"] = fire, ["cutFill"] = "#0000ff", ["cutHatch"] = "CROSS" }, new JsonObject { ["id"] = lb, ["cutFill"] = "#ff0000" });
        b = Band(f, plan, bearing)!;
        Assert.Equal(("CROSS", "#0000ff", "Fire rated"), (b.Hatch, b.Fill, b.Filters));
    }

    [Fact]
    public void An_elevation_takes_a_filter_s_surface_hatch_and_fill_and_a_legend_lists_the_filter()
    {
        var f = new Fixture(new Lapin.Architecture.ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        var wall = f.Wall(0, 0, 8, 0, f.Type("Concrete 20", ("Concrete", 0.2, true)), height: 3);
        f.Run("element.setParam", new JsonObject { ["id"] = wall, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        var lb = Filter(f, "Load-bearing", "LoadBearing", "equals", "yes");
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south", ["scale"] = 50 })!["id"]!;
        Assert.DoesNotContain(Render(f, south).Pens, p => p.Fill == "#ffee00");
        Rows(f, south, new JsonObject { ["id"] = lb, ["surfaceFill"] = "#ffee00", ["surfaceHatch"] = "CROSS", ["hatchColor"] = "#aa0000" });
        var pens = Render(f, south).Pens;
        Assert.Contains(pens, p => p.Fill == "#ffee00");
        Assert.Contains(pens, p => p.Layer == SurfaceHatches.Layer && p.Stroke == "#aa0000" && p.Segments.Count > 0);
        // A sheet's legend: the filter a row of its own, drawn as the view draws it.
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 100 })!["id"]!;
        Rows(f, plan, new JsonObject { ["id"] = lb, ["cutHatch"] = "none", ["cutFill"] = "#00aa00" });
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = plan, ["x"] = 150, ["y"] = 140 });
        var rows = DrawingKeys.Entries(f.Session.Reader, PaperViews.Find(f.Model, PaperViews.IdOf(s))!, "materials");
        var row = Assert.Single(rows, r => r.Name == "Load-bearing");
        Assert.Equal(("#00aa00", (string?)null), (row.Fill, row.Hatch));
    }

    [Fact]
    public void Rows_are_checked_and_a_view_whose_filters_give_no_hatch_draws_as_before()
    {
        var (f, plan, bearing, _) = Walls();
        var lb = Filter(f, "Load-bearing", "LoadBearing", "equals", "yes");
        var before = Band(f, plan, bearing)!;
        Rows(f, plan, new JsonObject { ["id"] = lb, ["color"] = "#ff0000", ["pen"] = 3 });
        Assert.False(VisibilityGraphics.Of(f.Model, Views.Find(f.Model, plan)!).HasFilterGraphics);
        Assert.Equal(before, Band(f, plan, bearing));
        // The fill still paled by the filter's colour, as decision 394 drew it.
        f.Run("material.update", new JsonObject { ["id"] = Mat(f, "Concrete"), ["section"] = new JsonObject { ["fill"] = "#dddddd" } });
        Assert.Contains(Render(f, plan).Pens, p => p.Layer == PenLayer.Fill && p.Fill == HexColor.Mix("#ff0000", 0.6));
        Assert.Throws<CommandException>(() => Rows(f, plan, new JsonObject { ["id"] = lb, ["cutHatch"] = "NO_SUCH" }));
        Assert.Throws<CommandException>(() => Rows(f, plan, new JsonObject { ["id"] = lb, ["cutFill"] = "black" }));
        Assert.Throws<CommandException>(() => Rows(f, plan, new JsonObject { ["id"] = lb, ["hatchHalftone"] = "yes" }));
        Assert.Throws<CommandException>(() => Rows(f, plan, new JsonObject { ["id"] = lb, ["cutLine"] = new JsonObject { ["weight"] = -1 } }));
        Assert.Throws<CommandException>(() => Rows(f, plan, new JsonObject { ["id"] = lb, ["nope"] = 1 }));
        Rows(f, plan, new JsonObject { ["id"] = lb, ["cutHatch"] = "cross", ["cutFill"] = "", ["hatchColor"] = "#AA0000" });
        var row = f.Session.Query("view.visibility", new JsonObject { ["id"] = plan })!["graphics"]!["filters"]![0]!;
        Assert.Equal(("CROSS", "none", "#aa0000"), ((string?)row["cutHatch"], (string?)row["cutFill"], (string?)row["hatchColor"]));
        var fields = f.Session.Query("view.visibility", new JsonObject { ["id"] = plan })!["filterFields"]!.AsArray().Select(x => (string)x!);
        Assert.Equal(VisibilityGraphics.FilterFields, fields);
    }
}
