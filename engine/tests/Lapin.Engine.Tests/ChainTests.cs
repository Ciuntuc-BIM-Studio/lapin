using System.Text.Json.Nodes;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The path elements' axis on the IFC 4.3 alignment (docs/plan-rail.md, R1):
/// a PI with a radius rounds the corner, the sweep follows the curve, the
/// plan outline too; a PI without one keeps the mitre, exactly as before.
/// </summary>
public class ChainTests
{
    static Fixture Site() => new(new CivilModule());
    static string Flat(Fixture f) => (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = -20, ["y1"] = -20, ["x2"] = 60, ["y2"] = 60, ["elevation"] = 0 })!["id"]!;
    static JsonArray Pts(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());
    static JsonArray Curves(params (double R, double In, double Out)[] c) => new(c.Select(q => (JsonNode)new JsonArray(q.R, q.In, q.Out)).ToArray());
    static double Volume(GeometryPart p) => Measure.Volume(p.Solid);

    /// <summary>An L: 20 m along +x, then 10 m along +y.</summary>
    static readonly (double, double)[] L = [(0, 0), (20, 0), (20, 10)];
    /// <summary>The L rounded with R = 5 at its corner: 30 − 2·5 + 5·π/2.</summary>
    const double RoundedLength = 30 - 10 + 5 * Math.PI / 2;

    [Fact]
    public void A_radius_at_a_vertex_rounds_the_corner_into_an_arc_and_the_stations_follow_it()
    {
        var c = new Chain([new(0, 0), new(20, 0), new(20, 10)], [PiCurve.None, new PiCurve(5), PiCurve.None], 100);
        Assert.NotNull(c.Alignment);
        Assert.Null(c.Problem);
        Assert.Equal(RoundedLength, c.Length, 9);
        // Line, arc, line.
        Assert.Equal(3, c.Segments);
        Assert.False(c.IsCurved(0)); Assert.True(c.IsCurved(1)); Assert.False(c.IsCurved(2));
        Assert.Equal(115, c.StationOf(1), 9);
        // The middle of the arc: on the circle about (15, 5), heading north-east.
        var mid = 100 + 15 + 5 * Math.PI / 4;
        var p = c.PointAt(mid);
        Assert.Equal(5, (p - new Vec2(15, 5)).Length, 9);
        Assert.Equal(Math.Sqrt(0.5), c.DirectionAt(mid).X, 9);
        Assert.Equal(Math.Sqrt(0.5), c.DirectionAt(mid).Y, 9);
        // Across the arc: the normal, not a mitre; one metre left is one metre nearer the centre.
        Assert.Equal(4, (p + c.Across(mid, 1) - new Vec2(15, 5)).Length, 9);
        // The inverse: a point one metre right of the arc's middle.
        var (station, offset) = c.Project(p - c.Across(mid, 1));
        Assert.Equal(mid, station, 8);
        Assert.Equal(-1, offset, 8);
        // The outline follows the curve: the outer edge stays R + 1 from the centre.
        var outer = c.Offset(-1);
        Assert.True(outer.Count > 10);
        Assert.All(outer.Where(q => q.X > 15 && q.Y < 5), q => Assert.Equal(6, (q - new Vec2(15, 5)).Length, 3));
    }

    [Fact]
    public void Without_a_radius_the_chain_is_the_polyline_it_always_was()
    {
        var plain = new Chain([new(0, 0), new(20, 0), new(20, 10)], 95);
        var corners = new Chain([new(0, 0), new(20, 0), new(20, 10)], [PiCurve.None, PiCurve.None, PiCurve.None], 95);
        Assert.Null(corners.Alignment);
        Assert.Equal(plain.Length, corners.Length);
        Assert.Equal(plain.Offset(1), corners.Offset(1));
        // The mitre at the corner: the left edge meets at (19, 1).
        Assert.Contains(plain.Offset(1), q => (q - new Vec2(19, 1)).Length < 1e-9);
        Assert.Equal([new(0, 0), new(20, 0), new(20, 10)], plain.Axis());
    }

    [Fact]
    public void A_curve_that_does_not_fit_is_refused_with_the_reason_and_the_axis_stays_the_polyline()
    {
        var c = new Chain([new(0, 0), new(20, 0), new(20, 10)], [PiCurve.None, new PiCurve(500), PiCurve.None]);
        Assert.Null(c.Alignment);
        Assert.NotNull(c.Problem);
        Assert.Equal(30, c.Length, 9);
    }

    [Fact]
    public void A_path_rounded_at_its_corner_is_swept_along_the_arc_and_drawn_through_it()
    {
        var f = Site();
        Flat(f);
        var p = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts(L), ["width"] = 2, ["surfaceThickness"] = 0.06, ["baseThickness"] = 0.2 })!["id"]!;
        f.Run("civil.setCorridor", new JsonObject { ["id"] = p, ["curves"] = Curves((0, 0, 0), (5, 0, 0), (0, 0, 0)) });
        var g = f.Session.Geometry(p)!;
        // A band symmetric about its axis has the area times the axis length (Pappus); the chords of the sweep lose under 0.1 %.
        AssertWithin(RoundedLength * 2 * 0.06, Volume(g.Parts.Single(x => x.Key == "surface")), 1e-3);
        // No corner at (21, −1) any more: the outline keeps away from it.
        var outline = g.Plan!.Single(i => i.Kind == "heavy" && i.Closed).Points;
        Assert.All(outline, q => Assert.True((q - new Vec2(21, -1)).Length > 2));
        Assert.Empty(g.Diagnostics ?? []);
        var ls = f.Session.Query("civil.corridor", new JsonObject { ["id"] = p })!;
        Assert.True((bool)ls["curved"]!);
        Assert.Equal(RoundedLength, (double)ls["length"]!, 6);
        Assert.Equal(5, (double)ls["spec"]!["curves"]![1]![0]!);
        // The cross-section in the arc looks along the tangent.
        var cs = f.Session.Query("civil.crossSection", new JsonObject { ["id"] = p, ["station"] = 15 + 5 * Math.PI / 4 })!;
        Assert.Equal(Math.Sqrt(0.5), (double)cs["dir"]![0]!, 6);
        Assert.Equal(Math.Sqrt(0.5), (double)cs["dir"]![1]!, 6);
        // Back to a polyline.
        f.Run("civil.setCorridor", new JsonObject { ["id"] = p, ["curves"] = new JsonArray() });
        Assert.Equal(30 * 2 * 0.06, Volume(f.Session.Geometry(p)!.Parts.Single(x => x.Key == "surface")), 6);
    }

    [Fact]
    public void A_curve_that_does_not_fit_is_refused_by_the_command()
    {
        var f = Site();
        Flat(f);
        var p = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts(L), ["width"] = 2 })!["id"]!;
        var ex = Assert.Throws<CommandException>(() => f.Run("civil.setCorridor", new JsonObject { ["id"] = p, ["curves"] = Curves((0, 0, 0), (500, 0, 0), (0, 0, 0)) }));
        Assert.Contains("PI 2", ex.Message);
    }

    [Fact]
    public void A_retaining_wall_follows_a_rounded_corner_unbroken()
    {
        var f = Site();
        Flat(f);
        var w = (string)f.Run("civil.addRetainingWall", new JsonObject { ["points"] = Pts(L), ["thickness"] = 0.3, ["height"] = 1.0, ["embed"] = 0.3, ["footingWidth"] = 0.8, ["footingThickness"] = 0.3 })!["id"]!;
        f.Run("civil.setCorridor", new JsonObject { ["id"] = w, ["curves"] = Curves((0, 0, 0), (5, 0, 0), (0, 0, 0)) });
        var area = 0.8 * 0.3 + 0.3 * 1.3;
        var part = Assert.Single(f.Session.Geometry(w)!.Parts);
        AssertWithin(area * RoundedLength, Volume(part), 1e-3);
    }

    static void AssertWithin(double expected, double actual, double relative) =>
        Assert.True(Math.Abs(actual - expected) <= relative * Math.Abs(expected), $"{actual} is not within {relative:P1} of {expected}");
}
