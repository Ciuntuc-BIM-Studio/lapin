using System.Text.Json.Nodes;
using BubbleBim.Geometry;
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
}
