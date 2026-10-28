using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The Site tab's imports in real coordinates (docs/plan-urban.md, stage 1): DXF survey points, GeoJSON, CityJSON, an orthophoto.</summary>
public class GeoImportTests
{
    static Fixture F() => new(new ArchitectureModule(), new CivilModule());

    static string Dxf(params string[] entities) =>
        "0\nSECTION\n2\nENTITIES\n" + string.Concat(entities) + "0\nENDSEC\n0\nEOF\n";
    static string Point(double x, double y, double z, string layer = "PCT") => $"0\nPOINT\n8\n{layer}\n10\n{x}\n20\n{y}\n30\n{z}\n";
    static string Text(double x, double y, string value, string layer = "COTE") => $"0\nTEXT\n8\n{layer}\n10\n{x}\n20\n{y}\n30\n0\n40\n0.5\n1\n{value}\n";

    [Fact]
    public void Survey_points_from_DXF_make_a_terrain_near_the_origin_tied_to_Stereo_70()
    {
        var f = F();
        // Three POINTs with their heights, and a point without one whose height is written beside it.
        var dxf = Dxf(Point(587900, 325820, 80.5), Point(587930, 325820, 81.2), Point(587910, 325850, 82.0), Point(587940, 325860, 0, "X"), Text(587940.4, 325860.3, "83.45"));
        var r = (JsonObject)f.Run("civil.importDxfPoints", new JsonObject { ["text"] = dxf })!;
        Assert.Equal(4, (int)r["points"]!);
        Assert.Equal(80, (double)r["datum"]!);
        // The project now stands on Stereo 70 with its survey point at the plan's origin, on the first point.
        Assert.Equal(GeoRef.Stereo70, Location.Crs(f.Model));
        var t = f.Model.Get((string)r["id"]!)!;
        var pts = Terrains.Points(t);
        Assert.Contains(pts, p => Math.Abs(p.X) < 1e-9 && Math.Abs(p.Y) < 1e-9 && Math.Abs(p.Z - 80.5) < 1e-9);
        Assert.Contains(pts, p => Math.Abs(p.X - 40) < 1e-9 && Math.Abs(p.Y - 40) < 1e-9 && Math.Abs(p.Z - 83.45) < 1e-9);
        var (e, n) = GeoRef.ToMap(f.Model, new Vec2(30, 0));
        Assert.Equal(587930, e, 6);
        Assert.Equal(325820, n, 6);
        var layers = (JsonArray)f.Session.Query("civil.dxfPointLayers", new JsonObject { ["text"] = dxf })!;
        Assert.Contains(layers, l => (string)l!["layer"]! == "PCT");
    }

    [Fact]
    public void A_GeoJSON_parcel_in_WGS_84_lands_converted_and_outlines_the_terrain()
    {
        var f = F();
        f.Run("project.location", new JsonObject { ["crs"] = "EPSG:3844", ["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = 587900, ["northing"] = 325820 } });
        var terrain = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = -50, ["y1"] = -50, ["x2"] = 50, ["y2"] = 50, ["elevation"] = 0 })!["id"]!;
        // A small square around Bucharest's point (26.1025, 44.4268), given in WGS 84 as GeoJSON is.
        var (c0, c1) = (GeoRef.ToWgs84(GeoRef.Stereo70, 587904.3249, 325824.3903), GeoRef.ToWgs84(GeoRef.Stereo70, 587924.3249, 325844.3903));
        string P(double lon, double lat) => $"[{lon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
        var ring = $"[{P(c0.Lon, c0.Lat)},{P(c1.Lon, c0.Lat)},{P(c1.Lon, c1.Lat)},{P(c0.Lon, c1.Lat)},{P(c0.Lon, c0.Lat)}]";
        var json = "{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"properties\":{\"nr_cad\":\"12345\"},\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[" + ring + "]}},"
            + "{\"type\":\"Feature\",\"properties\":{\"name\":\"Fence\"},\"geometry\":{\"type\":\"LineString\",\"coordinates\":[" + P(c0.Lon, c0.Lat) + "," + P(c1.Lon, c1.Lat) + "]}}]}";
        var r = (JsonObject)f.Run("civil.importGeoJson", new JsonObject { ["text"] = json })!;
        var parcel = f.Model.Get((string)r["parcel"]!)!;
        Assert.Equal("12345", parcel.Name);
        Assert.Equal("parcel", SiteFeatures.Kind(parcel));
        var first = SiteFeatures.Points(parcel)[0];
        Assert.Equal(4.3249, first.X, 0.02);
        Assert.Equal(4.3903, first.Y, 0.02);
        Assert.Equal(2, ((JsonArray)r["ids"]!).Count);
        Assert.NotNull(f.Model.Get(terrain)!.Attrs["parcel"]);
        var area = f.Session.Quantities(parcel.Id).Single().Quantities.Single(q => q.Name == "Area").Value;
        Assert.InRange(area, 395, 405);
    }

    [Fact]
    public void A_CityJSON_building_comes_in_with_its_surfaces_at_its_height()
    {
        var f = F();
        // A 10 × 8 × 6 m box on Stereo 70, compressed by the file's transform, its ground at 100 m.
        var json = """
        {"type":"CityJSON","version":"2.0","metadata":{"referenceSystem":"https://www.opengis.net/def/crs/EPSG/0/3844"},
         "transform":{"scale":[0.001,0.001,0.001],"translate":[587900,325820,100]},
         "vertices":[[0,0,0],[10000,0,0],[10000,8000,0],[0,8000,0],[0,0,6000],[10000,0,6000],[10000,8000,6000],[0,8000,6000]],
         "CityObjects":{"b1":{"type":"Building","attributes":{"name":"Neighbour"},"geometry":[{"type":"Solid","lod":"2","boundaries":[[[[0,3,2,1]],[[4,5,6,7]],[[0,1,5,4]],[[1,2,6,5]],[[2,3,7,6]],[[3,0,4,7]]]]}]}}}
        """;
        var r = (JsonObject)f.Run("civil.importCityJson", new JsonObject { ["text"] = json })!;
        Assert.Equal(100, (double)r["datum"]!);
        var id = (string)((JsonArray)r["ids"]!)[0]!;
        Assert.Equal("Neighbour", f.Model.Get(id)!.Name);
        var b = Measure.Bounds(f.Session.Geometry(id)!.Parts[0].Solid)!.Value;
        Assert.Equal(0, b.Min.X, 6);
        Assert.Equal(10, b.Max.X, 6);
        Assert.Equal(8, b.Max.Y, 6);
        Assert.Equal(0, b.Min.Z, 6);
        Assert.Equal(6, b.Max.Z, 6);
        Assert.Equal(480, Measure.Volume(f.Session.Geometry(id)!.Parts[0].Solid), 3);
    }

    [Fact]
    public void An_orthophoto_lies_where_its_world_file_puts_it()
    {
        var f = F();
        f.Run("project.location", new JsonObject { ["crs"] = "EPSG:3844", ["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = 587900, ["northing"] = 325820 } });
        // A 4 × 2 pixel PNG, 0.5 m a pixel, its upper-left pixel's centre at (587900.25, 325830.75).
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAIAAADwyuo0AAAAEElEQVR4nGP4z8AARwzIHABvqgf5gNwAKAAAAABJRU5ErkJggg==");
        Assert.Equal((4, 2), Orthophotos.PixelSize(png));
        var r = (JsonObject)f.Run("civil.addOrthophoto", new JsonObject
        {
            ["data"] = Convert.ToBase64String(png), ["mime"] = "image/png", ["name"] = "ortho", ["worldFile"] = "0.5\n0\n0\n-0.5\n587900.25\n325830.75\n",
        })!;
        var c = Orthophotos.Corners(f.Model.Get((string)r["id"]!)!);
        Assert.Equal(new Vec2(0, 11), c[0]);
        Assert.Equal(new Vec2(2, 11), c[1]);
        Assert.Equal(new Vec2(2, 10), c[2]);
        Assert.Equal(new Vec2(0, 10), c[3]);
        Assert.Throws<CommandException>(() => f.Run("civil.addOrthophoto", new JsonObject { ["data"] = Convert.ToBase64String(png), ["mime"] = "image/png" }));
    }
}
