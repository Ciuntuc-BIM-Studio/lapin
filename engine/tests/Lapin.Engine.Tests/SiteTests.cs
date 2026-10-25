using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The site tools (docs/spec-site.md, decision 122): a flat terrain and the
/// brush on it, a ditch and a path that cut the ground and measure what they
/// take, a retaining wall stepping on a slope, plantings sown by a seed, and
/// how they all export.
/// </summary>
public class SiteTests
{
    static Fixture Site() => new(new CivilModule());

    static string Flat(Fixture f, double z = 0) =>
        (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 40, ["y2"] = 40, ["elevation"] = z })!["id"]!;

    /// <summary>A slope rising 0,1 m per metre along x, 40 × 40 m.</summary>
    static string Slope(Fixture f)
    {
        var lines = new List<string>();
        for (var i = 0; i <= 8; i++) for (var j = 0; j <= 8; j++) lines.Add($"{i * 5} {j * 5} {100 + 0.1 * i * 5}");
        return (string)f.Run("civil.addTerrain", new JsonObject { ["text"] = string.Join("\n", lines), ["datum"] = 100 })!["id"]!;
    }

    static double GroundAt(Fixture f, string terrain, double x, double y) => Terrains.Of(f.Model.Get(terrain)!).ElevationAt(x, y)!.Value;
    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void The_brush_raises_lowers_flattens_and_smooths_the_ground_and_can_be_cleared()
    {
        var f = Site();
        var t = Flat(f);
        Assert.Equal(0, GroundAt(f, t, 20, 20), 9);
        f.Run("civil.sculpt", new JsonObject { ["kind"] = "raise", ["x"] = 20, ["y"] = 20, ["radius"] = 5, ["amount"] = 1.0 });
        Assert.InRange(GroundAt(f, t, 20, 20), 0.9, 1.0);
        Assert.InRange(GroundAt(f, t, 22.5, 20), 0.4, 0.6);                       // half way to the rim
        Assert.Equal(0, GroundAt(f, t, 30, 20), 6);                                 // untouched beyond the radius
        f.Run("civil.sculpt", new JsonObject { ["kind"] = "lower", ["x"] = 20, ["y"] = 20, ["radius"] = 5, ["amount"] = 1.0 });
        Assert.InRange(GroundAt(f, t, 20, 20), -0.05, 0.05);                       // back down
        f.Run("civil.sculpt", new JsonObject { ["kind"] = "flatten", ["x"] = 10, ["y"] = 10, ["radius"] = 4, ["level"] = 2.0 });
        Assert.InRange(GroundAt(f, t, 10, 10), 1.9, 2.0);
        f.Run("civil.sculpt", new JsonObject { ["kind"] = "smooth", ["x"] = 10, ["y"] = 10, ["radius"] = 6, ["strength"] = 1 });
        Assert.True(GroundAt(f, t, 10, 10) < 1.9, "smoothing pulls the peak down");
        Assert.Equal(4, ((JsonArray)f.Model.Get(t)!.Attrs["sculpts"]!).Count);
        f.Run("civil.clearSculpt", new JsonObject { ["id"] = t, ["last"] = true });
        Assert.Equal(3, ((JsonArray)f.Model.Get(t)!.Attrs["sculpts"]!).Count);
        f.Run("civil.clearSculpt", new JsonObject { ["id"] = t });
        Assert.Equal(0, GroundAt(f, t, 10, 10), 9);
        Assert.Null(f.Model.Get(t)!.Attrs["sculpts"]);
    }

    [Fact]
    public void A_ditch_takes_its_channel_out_of_the_ground_and_measures_it()
    {
        var f = Site();
        var t = Flat(f);
        var before = Volume(f, t);
        var d = (string)f.Run("civil.addDitch", new JsonObject { ["points"] = new JsonArray(new JsonArray(5, 20), new JsonArray(35, 20)), ["depth"] = 0.5, ["bottomWidth"] = 0.4, ["slope"] = 1 })!["id"]!;
        var after = Volume(f, t);
        // 30 m of a trapezoid 0,4 wide at the bottom, 1,4 at the top, 0,5 deep: 0,45 m² a metre.
        Assert.InRange(before - after, 12.5, 14.5);
        var works = f.Session.Quantities(t).Single(q => q.Name == "Lapin_EarthworkQuantities" && q.Part == "pad:" + d);
        Assert.InRange(works.Quantities.Single(x => x.Name == "CutVolume").Value, 12.5, 14.5);
        Assert.Contains(f.Session.Geometry(t)!.Plan!, i => i.Kind == "heavy" && !i.Closed);
        f.Run("civil.updateDitch", new JsonObject { ["id"] = d, ["depth"] = 1.0 });
        Assert.True(before - Volume(f, t) > 20);
    }

    [Fact]
    public void A_path_lies_on_the_ground_with_its_courses_and_the_ground_is_cut_for_it()
    {
        var f = Site();
        var t = Slope(f);
        var before = Volume(f, t);
        var p = (string)f.Run("civil.addPath", new JsonObject { ["points"] = new JsonArray(new JsonArray(5, 20), new JsonArray(35, 20)), ["width"] = 2, ["surfaceThickness"] = 0.06, ["baseThickness"] = 0.2, ["kerb"] = true })!["id"]!;
        var g = f.Session.Geometry(p)!;
        var surface = g.Parts.Single(x => x.Key == "surface");
        var @base = g.Parts.Single(x => x.Key == "base");
        Assert.Contains(g.Parts, x => x.Key == "kerb");
        Assert.InRange(Measure.Volume(surface.Solid), 30 * 2 * 0.06 * 0.95, 30 * 2 * 0.06 * 1.05);
        Assert.InRange(Measure.Volume(@base.Solid), 30 * 2 * 0.2 * 0.95, 30 * 2 * 0.2 * 1.05);
        // The surface climbs with the ground: at x = 5 it is at 0,5, at x = 35 at 3,5.
        var b = Measure.Bounds(surface.Solid)!.Value;
        Assert.InRange(b.Min.Z, 0.5 - 0.07, 0.5);
        Assert.InRange(b.Max.Z, 3.5 - 0.01, 3.5 + 0.01);
        Assert.True(before - Volume(f, t) > 10, "the ground gives way to the courses");
        var q = f.Session.Quantities(p).Single(x => x.Name == "Lapin_PavementQuantities");
        Assert.Equal(60, q.Quantities.Single(x => x.Name == "GrossArea").Value, 6);
        Assert.Equal(60, q.Quantities.Single(x => x.Name == "KerbLength").Value, 6);
    }

    [Fact]
    public void A_retaining_wall_steps_up_a_slope_with_its_footing_under_the_low_ground()
    {
        var f = Site();
        Slope(f);
        // Along y at x = 20: the ground is 2,0 m; the high ground to the left (towards +x) when drawn towards -y… drawn from (20, 5) to (20, 35): left is -x, the low side.
        var w = (string)f.Run("civil.addRetainingWall", new JsonObject { ["points"] = new JsonArray(new JsonArray(20, 5), new JsonArray(20, 35)), ["thickness"] = 0.3, ["height"] = 1.0, ["side"] = "right", ["embed"] = 0.3, ["footingWidth"] = 0.8, ["footingThickness"] = 0.3 })!["id"]!;
        var g = f.Session.Geometry(w)!;
        var part = Assert.Single(g.Parts);
        var b = Measure.Bounds(part.Solid)!.Value;
        // The high ground (x ≈ 20,65) is at 2,065: the crown at 3,065; the low ground (x ≈ 19,35) at 1,935: the footing's underside at 1,335.
        Assert.InRange(b.Max.Z, 3.0, 3.1);
        Assert.InRange(b.Min.Z, 1.3, 1.4);
        Assert.Equal(0.8, b.Max.X - b.Min.X, 6);
        var q = f.Session.Quantities(w).Single(x => x.Name == "Lapin_RetainingWallQuantities");
        Assert.Equal(30, q.Quantities.Single(x => x.Name == "Length").Value, 6);
        Assert.InRange(q.Quantities.Single(x => x.Name == "ConcreteVolume").Value, 30 * (0.8 * 0.3 + 0.3 * 1.4) * 0.95, 30 * (0.8 * 0.3 + 0.3 * 1.5) * 1.05);
        f.Run("civil.updateRetainingWall", new JsonObject { ["id"] = w, ["topElevation"] = 5.0 });
        Assert.Equal(5.0, Measure.Bounds(f.Session.Geometry(w)!.Parts[0].Solid)!.Value.Max.Z, 6);
    }

    [Fact]
    public void Plantings_stand_on_the_ground_and_a_zone_grows_the_same_way_from_its_seed()
    {
        var f = Site();
        var t = Slope(f);
        var one = (string)f.Run("civil.addPlanting", new JsonObject { ["x"] = 20, ["y"] = 20 })!["id"]!;
        var g = f.Session.Geometry(one)!;
        var trunk = Assert.Single(g.Parts);
        Assert.InRange(Measure.Bounds(trunk.Solid)!.Value.Min.Z, GroundAt(f, t, 20, 20) - 1e-6, GroundAt(f, t, 20, 20) + 1e-6);
        var zone = (string)f.Run("civil.addPlanting", new JsonObject { ["points"] = new JsonArray(new JsonArray(5, 5), new JsonArray(35, 5), new JsonArray(35, 35), new JsonArray(5, 35)), ["density"] = 2, ["seed"] = 7 })!["id"]!;
        var plants = Plantings.Plants(f.Model, f.Model.Get(zone)!);
        Assert.Equal(18, plants.Count);                                            // 2 per 100 m² over 900 m²
        Assert.All(plants, p => Assert.True(p.At.X >= 5 && p.At.X <= 35 && p.At.Y >= 5 && p.At.Y <= 35));
        Assert.Equal(plants.Select(p => (p.At.X, p.At.Y, p.Species.Name)), Plantings.Plants(f.Model, f.Model.Get(zone)!).Select(p => (p.At.X, p.At.Y, p.Species.Name)));
        var q = f.Session.Quantities(zone);
        Assert.Equal(18, q.First(x => x.Part == null).Quantities.Single(x => x.Name == "Count").Value, 6);
        Assert.True(q.Count(x => x.Part != null) >= 2, "counted by species");
        Assert.True(f.Session.Geometry(zone)!.Plan!.Count(i => i.Kind == "symbol") >= 18);
    }

    [Fact]
    public void The_site_exports_as_pavement_trench_retaining_wall_and_vegetation()
    {
        var f = Site();
        Flat(f);
        f.Run("civil.addDitch", new JsonObject { ["points"] = new JsonArray(new JsonArray(5, 20), new JsonArray(35, 20)) });
        f.Run("civil.addPath", new JsonObject { ["points"] = new JsonArray(new JsonArray(5, 10), new JsonArray(35, 10)) });
        f.Run("civil.addRetainingWall", new JsonObject { ["points"] = new JsonArray(new JsonArray(5, 30), new JsonArray(35, 30)) });
        f.Run("civil.addPlanting", new JsonObject { ["x"] = 20, ["y"] = 25 });
        foreach (var (schema, pavement, trench) in new[] { ("IFC4X3", "IFCPAVEMENT", "IFCEARTHWORKSCUT"), ("IFC4", "IFCSLAB", "IFCBUILDINGELEMENTPROXY") })
        {
            var path = Path.Combine(Path.GetTempPath(), $"lapin-site-{Guid.NewGuid():N}.ifc");
            try
            {
                IfcExport.Write(f.Session, path, new IfcExportOptions(schema));
                var step = StepFile.Read(path);
                Assert.Single(step.OfType(pavement));
                Assert.Single(step.OfType(trench));
                var wall = Assert.Single(step.OfType("IFCWALL"));
                Assert.Equal(new StepEnum("USERDEFINED"), wall[8]);
                Assert.Equal("RETAININGWALL", (string?)wall[4]);
                Assert.Equal(2, step.OfType("IFCGEOGRAPHICELEMENT").Count());
                if (schema == "IFC4X3") Assert.Contains(step.OfType("IFCGEOGRAPHICELEMENT"), g => Equals(g[8], new StepEnum("VEGETATION")));
            }
            finally { File.Delete(path); }
        }
    }

    [Fact]
    public void The_analysis_colours_slopes_and_cut_and_fill_and_tells_their_areas()
    {
        var f = Site();
        var t = Slope(f);
        // A 10 % slope everywhere: one band, the whole ground.
        var slope = Analysis.Of(f.Model, f.Model.Get(t)!, "slope")!;
        Assert.All(slope.Cells, c => Assert.Equal("gentle", c.Band.Key));
        var legend = (JsonObject)f.Session.Query("civil.analysis", new JsonObject { ["id"] = t, ["kind"] = "slope" })!;
        Assert.InRange((double)legend["area"]!, 1500, 1600);
        // A platform at 2,0 over ground from 1,0 to 3,0: cut on the high side, fill on the low one.
        f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["x1"] = 10, ["y1"] = 10, ["x2"] = 30, ["y2"] = 20, ["elevation"] = 2.0 });
        var cutFill = Analysis.Of(f.Model, f.Model.Get(t)!, "cutfill")!;
        Assert.Contains(cutFill.Cells, c => c.Band.Key.StartsWith("cut", StringComparison.Ordinal));
        Assert.Contains(cutFill.Cells, c => c.Band.Key.StartsWith("fill", StringComparison.Ordinal));
        Assert.Contains(cutFill.Cells, c => c.Band.Key == "level" && c.Min.X < 5);
        // Switched on, the terrain carries the film in 3D.
        f.Run("civil.updateTerrain", new JsonObject { ["id"] = t, ["analysis"] = "cutfill" });
        Assert.True(f.Session.Geometry(t)!.Parts.Count(p => p.Key!.StartsWith("analysis:", StringComparison.Ordinal)) >= 3);
        f.Run("civil.updateTerrain", new JsonObject { ["id"] = t, ["analysis"] = "none" });
        Assert.Single(f.Session.Geometry(t)!.Parts);
        Assert.Throws<CommandException>(() => f.Run("civil.updateTerrain", new JsonObject { ["id"] = t, ["analysis"] = "rainbow" }));
    }

    [Fact]
    public void Spot_levels_and_the_parcel_are_kept_on_the_terrain_and_measured()
    {
        var f = Site();
        var t = Slope(f);
        f.Run("civil.addSpot", new JsonObject { ["x"] = 10, ["y"] = 10 });
        f.Run("civil.addSpot", new JsonObject { ["x"] = 20, ["y"] = 20 });
        Assert.Equal(2, Terrains.Spots(f.Model.Get(t)!).Count);
        f.Run("civil.removeSpot", new JsonObject { ["id"] = t, ["x"] = 10.2, ["y"] = 10.1 });
        Assert.Equal([new Lapin.Geometry.Vec2(20, 20)], Terrains.Spots(f.Model.Get(t)!));
        var r = (JsonObject)f.Run("civil.setParcel", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(30, 0), new JsonArray(30, 20), new JsonArray(0, 20)) })!;
        Assert.Equal(600, (double)r["area"]!, 6);
        var q = f.Session.Quantities(t).Single(x => x.Name == "Lapin_TerrainQuantities");
        Assert.Equal(600, q.Quantities.Single(x => x.Name == "ParcelArea").Value, 6);
        Assert.Equal(100, q.Quantities.Single(x => x.Name == "ParcelPerimeter").Value, 6);
        f.Run("civil.setParcel", new JsonObject { ["id"] = t });
        Assert.DoesNotContain(f.Session.Quantities(t).Single(x => x.Name == "Lapin_TerrainQuantities").Quantities, x => x.Name == "ParcelArea");
    }

    [Fact]
    public void A_terrain_goes_out_as_LandXML_and_comes_back_the_same()
    {
        var f = Site();
        var t = Slope(f);
        f.Run("civil.sculpt", new JsonObject { ["kind"] = "raise", ["x"] = 20, ["y"] = 20, ["radius"] = 5, ["amount"] = 1 });
        var o = (JsonObject)f.Session.Query("civil.landXml", new JsonObject())!;
        var xml = (string)o["text"]!;
        Assert.Contains("surfType=\"TIN\"", xml);
        var surfaces = LandXml.Surfaces(xml);
        var tin = Terrains.Of(f.Model.Get(t)!).Tin;
        Assert.Equal(tin.Points.Count, surfaces[0].Points.Count);
        Assert.Equal(tin.Triangles.Count, surfaces[0].Faces);
        // Surveyed heights: the datum (100) back on; x and y where they were (the slope tops out at x = 40; the raised ground at (20, 20) is 103).
        var top = surfaces[0].Points.MaxBy(p => p.Z);
        Assert.InRange(top.Z, 103.9, 104.1);
        Assert.InRange(top.X, 39, 41);
        Assert.InRange(surfaces[0].Points.Where(p => Math.Abs(p.X - 20) < 0.3 && Math.Abs(p.Y - 20) < 0.3).Max(p => p.Z), 102.9, 103.05);
        var back = (string)f.Run("civil.importLandXml", new JsonObject { ["text"] = xml, ["datum"] = 100 })!["ids"]![0]!;
        Assert.InRange(GroundAt(f, back, 20, 20), GroundAt(f, t, 20, 20) - 0.02, GroundAt(f, t, 20, 20) + 0.02);
    }

    [Fact]
    public void The_planting_schedule_counts_every_species_and_shows_as_a_table()
    {
        var f = new Fixture(new ArchitectureModule(), new CivilModule());
        Flat(f);
        f.Run("civil.addPlanting", new JsonObject { ["points"] = new JsonArray(new JsonArray(5, 5), new JsonArray(35, 5), new JsonArray(35, 35), new JsonArray(5, 35)), ["density"] = 2, ["seed"] = 7 });
        f.Run("civil.addPlanting", new JsonObject { ["x"] = 2, ["y"] = 2 });
        var grid = new PlantingSchedule().Grid(f.Session.Reader);
        Assert.Equal(["Species", "Kind", "Height (m)", "Crown (m)", "Count"], grid.Columns);
        Assert.Equal("Total", grid.Rows[^1][0]);
        Assert.Equal("19", grid.Rows[^1][4]);
        Assert.True(grid.Rows.Count >= 3);
        var s = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "planting" })!["id"]!;
        Assert.Equal("Planting schedule", f.Model.Get(s)!.Name);
        var layout = (JsonObject)f.Session.Query("schedule.layout", new JsonObject { ["id"] = s })!;
        Assert.True(((JsonArray)layout["pens"]!).Count > 0);
        Assert.Equal(grid.Rows.Count, ((JsonArray)layout["table"]!["rows"]!).Count);
        var csv = (string)f.Session.Query("schedule.csv", new JsonObject { ["id"] = s })!;
        Assert.StartsWith("Species,Kind,Height (m),Crown (m),Count", csv);
        Assert.Throws<CommandException>(() => f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "furniture" }));
    }
}
