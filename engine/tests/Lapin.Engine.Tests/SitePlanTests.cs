using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drawing;
using Lapin.Geometry;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The Site (docs/site-plans.md, decision 169): a site plan and a location
/// plan in every new project, the whole model seen from above whatever the
/// storey, what is under the ground left out; the north arrow, the map's
/// grid and the scale bar over it; the parcel's corners numbered and listed
/// with their map coordinates.
/// </summary>
public sealed class SitePlanTests
{
    static Fixture Project()
    {
        var f = new Fixture(new ArchitectureModule(), new CivilModule());
        f.Session.New();
        return f;
    }

    static string Storey(Fixture f, string name) => Spatial.Storeys(f.Model).Single(s => s.Name == name).Id;
    static Entity SiteView(Fixture f, string purpose) => f.Model.OfClass(Views.Class).Single(v => SitePlans.Of(v) == purpose);

    [Fact]
    public void A_new_project_has_a_site_plan_and_a_location_plan_on_the_ground_floor()
    {
        var f = Project();
        var site = SiteView(f, SitePlans.Situation);
        var location = SiteView(f, SitePlans.LocationPlan);
        Assert.Equal(("Site plan", 500.0), (site.Name, site.Num("scale")));
        Assert.Equal(("Location plan", 2000.0), (location.Name, location.Num("scale")));
        Assert.Equal(Storey(f, "Ground floor"), site.Str("storeyId"));
        Assert.False(f.Session.History.CanUndo);
        // One made by hand stands on the terrain's storey when there is one.
        var terrain = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["storeyId"] = Storey(f, "Foundations"), ["x1"] = -20, ["y1"] = -20, ["x2"] = 30, ["y2"] = 30, ["elevation"] = 0 })!["id"]!;
        var made = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["site"] = "location", ["name"] = "Location 1:5000", ["scale"] = 5000 })!["id"]!;
        var v = f.Model.Get(made)!;
        Assert.Equal((Storey(f, "Foundations"), 5000.0, "location"), (v.Str("storeyId"), v.Num("scale"), v.Str("site")));
        Assert.Throws<CommandException>(() => f.Run("view.create", new JsonObject { ["kind"] = "plan", ["site"] = "roof" }));
        Assert.NotNull(terrain);
    }

    [Fact]
    public void A_site_plan_sees_every_storey_from_above_and_leaves_out_what_is_under_the_ground()
    {
        var f = Project();
        var ground = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = Storey(f, "Ground floor"), ["x1"] = 0, ["y1"] = 0, ["x2"] = 8, ["y2"] = 0 })!["id"]!;
        var first = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = Storey(f, "First floor"), ["x1"] = 0, ["y1"] = 6, ["x2"] = 8, ["y2"] = 6 })!["id"]!;
        // A foundation wall from −1.50 to −0.30: wholly under the ground.
        var footing = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = Storey(f, "Foundations"), ["x1"] = 0, ["y1"] = 12, ["x2"] = 8, ["y2"] = 12, ["height"] = 1.2 })!["id"]!;
        var site = SiteView(f, SitePlans.Situation);
        var (d, spec, problem) = Views.Generate(f.Session.Reader, site);
        Assert.True(d != null, problem);
        var drawn = d!.Lines.Select(l => l.Element).ToHashSet();
        Assert.Contains(ground, drawn);
        Assert.Contains(first, drawn);
        Assert.DoesNotContain(footing, drawn);
        // Nothing is cut and nothing hidden is drawn: it is all seen from above.
        Assert.Empty(d.Cuts);
        Assert.All(d.Lines, l => Assert.Equal(LineRole.Projection, l.Role));
        Assert.False(spec!.Hidden);
        // The floor plan of the ground floor still shows its own storey alone.
        var (plan, _, _) = Views.Generate(f.Session.Reader, Views.Find(f.Model, "plan:" + Storey(f, "Ground floor"))!);
        Assert.DoesNotContain(first, plan!.Cuts.Select(c => c.Element).Concat(plan.Lines.Select(l => l.Element)));
        // It has no view range and no storeys to choose.
        Assert.Throws<CommandException>(() => f.Run("view.setRange", new JsonObject { ["id"] = site.Id, ["cutHeight"] = 2 }));
        Assert.Throws<CommandException>(() => f.Run("view.setStoreys", new JsonObject { ["id"] = site.Id, ["mode"] = "home" }));
    }

    static List<string> Texts(Fixture f, Entity view) => Views.Render(f.Session.Reader, view).Marks.SelectMany(m => m.Texts).Select(t => t.Text.Text).ToList();

    [Fact]
    public void A_site_plan_has_a_north_arrow_a_scale_bar_and_the_maps_grid_when_tied_to_the_map()
    {
        var f = Project();
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = Storey(f, "Ground floor"), ["x1"] = 0, ["y1"] = 0, ["x2"] = 120, ["y2"] = 0 });
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = Storey(f, "Ground floor"), ["x1"] = 0, ["y1"] = 0, ["x2"] = 0, ["y2"] = 120 });
        var site = SiteView(f, SitePlans.Situation);
        var texts = Texts(f, site);
        Assert.Contains("N", texts);
        // 60 mm at 1:500 is 30 m: a bar of 20 m in five parts.
        Assert.Contains("20 m", texts);
        Assert.Contains("1:500", texts);
        Assert.DoesNotContain(texts, t => t.StartsWith("E ", StringComparison.Ordinal));
        // Tied to Stereo 70: crosses every 50 m (a tenth of the scale), eastings and northings round.
        f.Run("project.location", new JsonObject { ["crs"] = "EPSG:3844", ["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = 587_020, ["northing"] = 326_480, ["elevation"] = 0 } });
        texts = Texts(f, f.Model.Get(site.Id)!);
        Assert.Contains("E 587 050", texts);
        Assert.Contains("N 326 500", texts);
        Assert.Contains("N 326 550", texts);
        Assert.DoesNotContain("E 587 025", texts);
        // Off when asked.
        f.Run("view.setSite", new JsonObject { ["id"] = site.Id, ["gridStep"] = 0, ["northArrow"] = false, ["scaleBar"] = false });
        Assert.DoesNotContain(Texts(f, f.Model.Get(site.Id)!), t => t is "N" or "1:500" || t.StartsWith("E ", StringComparison.Ordinal));
        Assert.Throws<CommandException>(() => f.Run("view.setSite", new JsonObject { ["id"] = Storey(f, "Ground floor") }));
    }

    [Fact]
    public void The_parcels_corners_are_numbered_and_listed_with_their_map_coordinates()
    {
        var f = Project();
        f.Run("civil.addFlatTerrain", new JsonObject { ["storeyId"] = Storey(f, "Ground floor"), ["x1"] = -10, ["y1"] = -10, ["x2"] = 50, ["y2"] = 40, ["elevation"] = 0 });
        f.Run("civil.setParcel", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(30, 0), new JsonArray(30, 20), new JsonArray(0, 20)) });
        f.Run("project.location", new JsonObject { ["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = 587_000, ["northing"] = 326_000, ["elevation"] = 0 } });
        var texts = Texts(f, SiteView(f, SitePlans.Situation));
        Assert.Contains("1", texts);
        Assert.Contains("4", texts);
        // The location plan names no corners.
        Assert.DoesNotContain("4", Texts(f, SiteView(f, SitePlans.LocationPlan)));
        var grid = new ParcelCoordinates().Grid(f.Session.Reader);
        Assert.Equal(5, grid.Rows.Count);
        Assert.Equal(["2", "326000.000", "587030.000", "20.00"], grid.Rows[1]);
        Assert.Equal("600.00 m²", grid.Rows[4][3]);
    }

    static bool Filled(Fixture f, Entity view) => Views.Render(f.Session.Reader, view).Marks.Any(m => m.Under && m.Fills.Any(x => x.Color == SitePlans.ProjectColor));

    [Fact]
    public void A_location_plan_fills_the_projects_buildings_and_a_site_plan_does_when_asked()
    {
        var f = Project();
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = Storey(f, "Ground floor"), ["x1"] = 0, ["y1"] = 0, ["x2"] = 8, ["y2"] = 0 });
        Assert.True(Filled(f, SiteView(f, SitePlans.LocationPlan)));
        var site = SiteView(f, SitePlans.Situation);
        Assert.False(Filled(f, site));
        f.Run("view.setSite", new JsonObject { ["id"] = site.Id, ["highlight"] = true });
        Assert.True(Filled(f, f.Model.Get(site.Id)!));
        // A neighbour imported as a site feature is the site's, never filled as the project.
        var g = new Fixture(new ArchitectureModule(), new CivilModule());
        g.Session.New();
        g.Run("civil.importGeoJson", new JsonObject { ["storeyId"] = Storey(g, "Ground floor"), ["text"] = """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{},"geometry":{"type":"Polygon","coordinates":[[[0,0],[10,0],[10,10],[0,0]]]}}]}""", ["crs"] = "EPSG:3844" });
        Assert.False(Filled(g, SiteView(g, SitePlans.LocationPlan)));
    }

    // A 4 × 2 pixel RGB PNG.
    const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAIAAADwyuo0AAAAEElEQVR4nGP4z8AARwzIHABvqgf5gNwAKAAAAABJRU5ErkJggg==";

    [Fact]
    public void An_orthophoto_lies_under_the_site_plans_drawing_and_prints_on_its_sheet()
    {
        var f = Project();
        f.Run("project.location", new JsonObject { ["crs"] = "EPSG:3844", ["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = 587900, ["northing"] = 325820 } });
        var ortho = (string)f.Run("civil.addOrthophoto", new JsonObject
        {
            ["data"] = Png, ["mime"] = "image/png", ["name"] = "ortho", ["worldFile"] = "0.5\n0\n0\n-0.5\n587900.25\n325830.75\n", ["storeyId"] = Storey(f, "Foundations"),
        })!["id"]!;
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = Storey(f, "Ground floor"), ["x1"] = 0, ["y1"] = 0, ["x2"] = 8, ["y2"] = 0 });
        var image = f.Model.Get(ortho)!.Str("image")!;
        static PenImage? Picture(Views.Rendered r) => r.Pens.FirstOrDefault(p => p.Images.Count > 0)?.Images[0];
        // Under everything in the site plan, whatever storey it is on, laid by its corners.
        var site = SiteView(f, SitePlans.Situation);
        var drawn = Views.Render(f.Session.Reader, site);
        Assert.Equal(0, drawn.Pens.FindIndex(p => p.Images.Count > 0));
        Assert.Equal(image, Picture(drawn)!.ImageId);
        Assert.Equal([new Vec2(0, 11), new Vec2(2, 11), new Vec2(2, 10), new Vec2(0, 10)], Picture(drawn)!.Corners);
        // A floor plan shows its own storey's alone.
        Assert.Null(Picture(Views.Render(f.Session.Reader, Views.Find(f.Model, "plan:" + Storey(f, "Ground floor"))!)));
        Assert.NotNull(Picture(Views.Render(f.Session.Reader, Views.Find(f.Model, "plan:" + Storey(f, "Foundations"))!)));
        // On a sheet: in paper millimetres (1 m is 2 mm at 1:500), clipped to its viewport; the PDF carries the image.
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Site" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = site.Id, ["x"] = 200, ["y"] = 150 });
        var page = Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!);
        var pen = page.Pens.Single(p => p.Images.Count > 0);
        Assert.NotNull(pen.Clip);
        var c = pen.Images[0].Corners;
        Assert.Equal(4, (c[1] - c[0]).Length, 6);
        Assert.Equal(2, (c[3] - c[0]).Length, 6);
        var pdf = System.Text.Encoding.Latin1.GetString(Sheets.Pdf(f.Session.Reader, [f.Model.Get(sheet)!]));
        Assert.Contains("/Subtype /Image /Width 4 /Height 2 /ColorSpace /DeviceRGB", pdf);
        Assert.Contains("/XObject << /Im0", pdf);
    }

    [Fact]
    public void A_png_is_read_to_its_samples_and_a_jpeg_kept_as_it_is()
    {
        var png = Raster.Read(Convert.FromBase64String(Png))!;
        Assert.Equal((4, 2, "DeviceRGB", 24, false), (png.W, png.H, png.ColorSpace, png.Data.Length, png.Jpeg));
        Assert.Null(png.Alpha);
        // A JPEG's frame header: 3 × 2 pixels, three components.
        byte[] jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x02, 0x00, 0x03, 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xD9];
        var j = Raster.Read(jpg)!;
        Assert.Equal((3, 2, "DeviceRGB", true), (j.W, j.H, j.ColorSpace, j.Jpeg));
        Assert.Null(Raster.Read([1, 2, 3, 4, 5]));
    }
}
