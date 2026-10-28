using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Coordinate systems (docs/plan-urban.md, stage 1): Stereo 70, UTM, Web Mercator against PROJ's values; the plan against the map.</summary>
public class GeoTests
{
    // PROJ 9 (pyproj): EPSG:4326 → EPSG:3844 through "Pulkovo 1942(58) to WGS 84 (19)", → EPSG:32635/32634.
    [Theory]
    [InlineData(26.1025, 44.4268, 587904.3249, 325824.3903)]
    [InlineData(23.6, 46.77, 393210.9462, 586549.3827)]
    [InlineData(28.65, 44.17, 791987.0536, 303276.9688)]
    public void Stereo70_matches_PROJ_both_ways(double lon, double lat, double e, double n)
    {
        var (x, y) = GeoRef.FromWgs84(GeoRef.Stereo70, lon, lat);
        Assert.Equal(e, x, 0.01);
        Assert.Equal(n, y, 0.01);
        var (lo, la) = GeoRef.ToWgs84("3844", e, n);
        Assert.Equal(lon, lo, 1e-7);
        Assert.Equal(lat, la, 1e-7);
    }

    [Theory]
    [InlineData("EPSG:32635", 26.1025, 44.4268, 428561.4934, 4919670.0321)]
    [InlineData("EPSG:32635", 28.65, 44.17, 631909.5359, 4892078.2901)]
    [InlineData("EPSG:32634", 21.23, 45.75, 517889.4685, 5066297.1078)]
    public void Utm_matches_PROJ_both_ways(string crs, double lon, double lat, double e, double n)
    {
        var (x, y) = GeoRef.FromWgs84(crs, lon, lat);
        Assert.Equal(e, x, 0.001);
        Assert.Equal(n, y, 0.001);
        var (lo, la) = GeoRef.ToWgs84(crs, e, n);
        Assert.Equal(lon, lo, 1e-9);
        Assert.Equal(lat, la, 1e-9);
    }

    [Fact]
    public void Systems_convert_into_one_another_and_unknown_ones_are_refused()
    {
        var (e, n) = GeoRef.Convert("EPSG:32635", GeoRef.Stereo70, 428561.4934, 4919670.0321);
        Assert.Equal(587904.3249, e, 0.01);
        Assert.Equal(325824.3903, n, 0.01);
        var (mx, my) = GeoRef.FromWgs84(GeoRef.WebMercator, 26.1025, 44.4268);
        var (lo, la) = GeoRef.ToWgs84(GeoRef.WebMercator, mx, my);
        Assert.Equal(26.1025, lo, 1e-9);
        Assert.Equal(44.4268, la, 1e-9);
        Assert.Null(GeoRef.Get("EPSG:2154"));
        Assert.Throws<ArgumentException>(() => GeoRef.ToWgs84("EPSG:2154", 0, 0));
    }

    [Fact]
    public void The_plan_ties_to_the_map_through_the_survey_point_and_true_north()
    {
        var f = new Fixture();
        f.Run("project.location", new JsonObject
        {
            ["crs"] = "3844",
            ["survey"] = new JsonObject { ["x"] = 10, ["y"] = 5, ["easting"] = 587900, ["northing"] = 325820, ["elevation"] = 80 },
            ["base"] = new JsonObject { ["northAngle"] = 30 },
        });
        Assert.Equal(GeoRef.Stereo70, Location.Crs(f.Model));
        var (e, n) = GeoRef.ToMap(f.Model, new Vec2(10, 5));
        Assert.Equal(587900, e, 9);
        Assert.Equal(325820, n, 9);
        // The plan's +X turned 30° from east (clockwise, as the project's north is turned): a point 10 m along it lands there on the map.
        var (e2, n2) = GeoRef.ToMap(f.Model, new Vec2(20, 5));
        Assert.Equal(587900 + 10 * Math.Cos(Math.PI / 6), e2, 9);
        Assert.Equal(325820 - 10 * Math.Sin(Math.PI / 6), n2, 9);
        var back = GeoRef.ToPlan(f.Model, e2, n2);
        Assert.Equal(20, back.X, 9);
        Assert.Equal(5, back.Y, 9);
        var geo = (JsonObject)f.Session.Query("project.geo", new JsonObject { ["x"] = 10, ["y"] = 5 })!;
        Assert.Equal(26.1025, (double)geo["lon"]!, 1e-4);
        Assert.Equal(44.4268, (double)geo["lat"]!, 1e-4);
        Assert.Throws<CommandException>(() => f.Run("project.location", new JsonObject { ["crs"] = "EPSG:2154" }));
    }
}
