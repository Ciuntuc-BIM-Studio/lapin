using System.Text.Json.Nodes;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The corridor configurator of the site's path elements (docs/spec-site.md,
/// decision 128): stations, the red line over the natural and proposed
/// ground, mitred corners that keep a strip or a wall unbroken, joints, the
/// road's template with gutters, kerbs and sidewalks, and the cross-section
/// at a station.
/// </summary>
public class CorridorTests
{
    static Fixture Site() => new(new CivilModule());

    static string Flat(Fixture f, double z = 0) =>
        (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = -20, ["y1"] = -20, ["x2"] = 60, ["y2"] = 60, ["elevation"] = z })!["id"]!;

    static JsonArray Pts(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());

    /// <summary>An L: 20 m along +x, then 10 m along +y.</summary>
    static readonly (double, double)[] L = [(0, 0), (20, 0), (20, 10)];

    static double Volume(GeometryPart p) => Measure.Volume(p.Solid);

    [Fact]
    public void A_vertical_curve_is_the_parabola_its_pvi_is_tangent_to()
    {
        // Ported from BubbleBIM Pro: grades +2 % and −2 %, a 50 m crest passes A·L/8 below its PVI.
        var p = VerticalProfile.Build([new(0, 100), new(100, 102, 50), new(200, 100)]);
        Assert.Equal(102 - 0.04 * 50 / 8, p.ElevationAt(100), 9);
        Assert.Equal(100 + 0.02 * 75, p.ElevationAt(75), 9);
        Assert.Equal(101, p.ElevationAt(150), 9);
        Assert.Equal(0, p.GradeAt(100), 9);
        Assert.NotNull(VerticalProfile.Problem([new(0, 0), new(10, 1, 30), new(20, 0)]));
    }

    [Fact]
    public void Stations_tick_at_the_interval_from_the_start_station_and_read_as_kilometres()
    {
        var c = new Chain([new(0, 0), new(20, 0), new(20, 10)], 95);
        Assert.Equal(30, c.Length, 9);
        var spec = CorridorSpec.Default with { StationStart = 95, Interval = 10 };
        Assert.Equal([95, 100, 110, 120, 125], Corridors.Stations(c, spec));
        Assert.Equal("0+095.00", Chain.Label(95));
        Assert.Equal("1+234.50", Chain.Label(1234.5));
        Assert.Equal(new Vec2(20, 5), c.PointAt(120));
        var (s, off) = c.Project(new Vec2(21, 3));
        Assert.Equal(118, s, 9);
        Assert.Equal(-1, off, 9);
    }

    [Fact]
    public void A_path_turns_a_corner_mitred_without_a_gap_or_an_overlap()
    {
        var f = Site();
        Flat(f);
        var p = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts(L), ["width"] = 2, ["surfaceThickness"] = 0.06, ["baseThickness"] = 0.2 })!["id"]!;
        var g = f.Session.Geometry(p)!;
        // A mitred strip is exactly its axis length times its width: 30 × 2.
        Assert.Equal(30 * 2 * 0.06, Volume(g.Parts.Single(x => x.Key == "surface")), 6);
        Assert.Equal(30 * 2 * 0.2, Volume(g.Parts.Single(x => x.Key == "base")), 6);
        // The outline's corners: outside (21, −1), inside (19, 1).
        var outline = g.Plan!.Single(i => i.Kind == "heavy" && i.Closed).Points;
        Assert.Contains(outline, q => (q - new Vec2(21, -1)).Length < 1e-9);
        Assert.Contains(outline, q => (q - new Vec2(19, 1)).Length < 1e-9);
    }

    [Fact]
    public void A_retaining_wall_turns_a_corner_unbroken_and_breaks_only_at_its_joints()
    {
        var f = Site();
        Flat(f);
        var w = (string)f.Run("civil.addRetainingWall", new JsonObject { ["points"] = Pts(L), ["thickness"] = 0.3, ["height"] = 1.0, ["embed"] = 0.3, ["footingWidth"] = 0.8, ["footingThickness"] = 0.3 })!["id"]!;
        // On flat ground: footing 0.8 × 0.3 from −0.6, stem 0.3 wide up to +1.0, so 1.3 high over the footing.
        var area = 0.8 * 0.3 + 0.3 * 1.3;
        var part = Assert.Single(f.Session.Geometry(w)!.Parts);
        // The section is symmetric about the axis, so the mitred sweep is its area times the axis length.
        Assert.Equal(area * 30, Volume(part), 6);
        f.Run("civil.setCorridor", new JsonObject { ["id"] = w, ["joints"] = new JsonObject { ["every"] = 6, ["width"] = 0.02 } });
        // Joints at 6, 12, 18, 24: four gaps of 2 cm.
        Assert.Equal(area * (30 - 4 * 0.02), Volume(f.Session.Geometry(w)!.Parts.Single()), 6);
        var q = f.Session.Quantities(w).Single(x => x.Name == "Lapin_RetainingWallQuantities");
        Assert.Equal(4, q.Quantities.Single(x => x.Name == "Joints").Value);
        Assert.Equal(4, f.Session.Geometry(w)!.Plan!.Count(i => i.Kind == "cut" && !i.Closed));
    }

    [Fact]
    public void A_designed_profile_lifts_the_path_off_the_ground_and_the_long_section_shows_both()
    {
        var f = Site();
        Flat(f);
        var p = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts((0, 0), (20, 0)), ["width"] = 2 })!["id"]!;
        f.Run("civil.setCorridor", new JsonObject { ["id"] = p, ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(new JsonArray(0, 1.0, 0), new JsonArray(20, 2.0, 0)) } });
        var top = Measure.Bounds(f.Session.Geometry(p)!.Parts.Single(x => x.Key == "surface").Solid)!.Value;
        Assert.Equal(2.0, top.Max.Z, 6);
        var cs = f.Session.Query("civil.crossSection", new JsonObject { ["id"] = p, ["station"] = 10 })!;
        var surface = cs["parts"]!.AsArray().Single(x => (string)x!["key"]! == "surface")!["polygon"]!.AsArray();
        Assert.Equal(1.5, surface.Max(v => (double)v![1]!), 6);
        Assert.All(cs["existing"]!.AsArray(), v => Assert.Equal(0, (double)v![1]!, 6));
        var ls = f.Session.Query("civil.corridor", new JsonObject { ["id"] = p })!;
        Assert.Equal("path", (string)ls["kind"]!);
        Assert.Equal(1.5, ls["design"]!.AsArray().Single(v => Math.Abs((double)v![0]! - 10) < 1e-6)![1]!.GetValue<double>(), 6);
        Assert.All(ls["proposed"]!.AsArray(), v => Assert.Equal(0, (double)v![1]!, 6));
        // The line across for a section view: perpendicular, through the axis at station 10.
        var line = cs["line"]!;
        var (a, b) = (new Vec2((double)line["start"]![0]!, (double)line["start"]![1]!), new Vec2((double)line["end"]![0]!, (double)line["end"]![1]!));
        Assert.Equal(10, a.X, 9);
        Assert.Equal(10, b.X, 9);
        Assert.True(a.Y < 0 && b.Y > 0);
    }

    [Fact]
    public void The_profile_fitted_to_the_ground_follows_a_slope_and_a_crown_on_a_wall_sits_over_it()
    {
        var f = Site();
        var lines = new List<string>();
        for (var i = 0; i <= 8; i++) for (var j = 0; j <= 8; j++) lines.Add($"{i * 5} {j * 5} {100 + 0.1 * i * 5}");
        f.Run("civil.addTerrain", new JsonObject { ["text"] = string.Join("\n", lines), ["datum"] = 100 });
        var p = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts((5, 20), (35, 20)), ["width"] = 2 })!["id"]!;
        f.Run("civil.profileFromGround", new JsonObject { ["id"] = p, ["ground"] = "existing" });
        var ls = f.Session.Query("civil.corridor", new JsonObject { ["id"] = p })!;
        Assert.Equal("design", (string)ls["spec"]!["profile"]!["mode"]!);
        // The plane x/10 from 0.5 to 3.5: two PVIs, the red line on the ground.
        Assert.Equal(2, ls["spec"]!["profile"]!["pvis"]!.AsArray().Count);
        foreach (var v in ls["design"]!.AsArray()) Assert.Equal(0.5 + (double)v![0]! / 10, (double)v[1]!, 3);
        var w = (string)f.Run("civil.addRetainingWall", new JsonObject { ["points"] = Pts((5, 10), (35, 10)), ["height"] = 1.0 })!["id"]!;
        f.Run("civil.profileFromGround", new JsonObject { ["id"] = w });
        var b = Measure.Bounds(f.Session.Geometry(w)!.Parts.Single().Solid)!.Value;
        Assert.Equal(3.5 + 1.0, b.Max.Z, 3);
    }

    [Fact]
    public void A_road_has_two_lanes_on_a_crown_with_gutters_kerbs_and_sidewalks()
    {
        var f = Site();
        Flat(f);
        var side = new JsonObject { ["gutter"] = true, ["gutterWidth"] = 0.5, ["gutterDepth"] = 0.05, ["kerb"] = true, ["kerbWidth"] = 0.15, ["kerbHeight"] = 0.12, ["sidewalk"] = true, ["sidewalkWidth"] = 1.5 };
        var r = (string)f.Run("civil.addPath", new JsonObject
        {
            ["points"] = Pts((0, 0), (20, 0)), ["template"] = "road", ["laneWidth"] = 3, ["crossfall"] = 2.5,
            ["left"] = side, ["right"] = side.DeepClone(),
        })!["id"]!;
        var t = Paths.TemplateOf(f.Model.Get(r)!);
        Assert.Equal(("road", 6.0, "crown"), (t.Kind, t.Width, t.Fall));
        Assert.Equal(3 + 0.5 + 0.15 + 1.5, t.Left, 9);
        var cs = f.Session.Query("civil.crossSection", new JsonObject { ["id"] = r, ["station"] = 5 })!;
        JsonArray Part(string k) => cs["parts"]!.AsArray().Where(x => (string)x!["key"]! == k).Select(x => x!["polygon"]!.AsArray()).First();
        var surface = Part("surface");
        // The crown on the axis at the ground, the edges 2.5 % × 3 m lower.
        Assert.Equal(0, surface.Max(v => (double)v![1]!), 9);
        Assert.Contains(surface, v => Math.Abs((double)v![0]! - 3) < 1e-9 && Math.Abs((double)v[1]! + 0.075) < 1e-9);
        Assert.Equal(2, cs["parts"]!.AsArray().Count(x => (string)x!["key"]! == "kerb"));
        // The sidewalk starts at the kerb's top (edge − 0.075 + 0.12) and rises 2 % away from the road.
        var walk = cs["parts"]!.AsArray().Where(x => (string)x!["key"]! == "sidewalk").Select(x => x!["polygon"]!.AsArray()).First(pg => pg.Any(v => (double)v![0]! > 0));
        Assert.Equal(-0.075 + 0.12 + 0.02 * 1.5, walk.Max(v => (double)v![1]!), 9);
        var g = f.Session.Geometry(r)!;
        foreach (var k in new[] { "surface", "base", "gutter", "kerb", "sidewalk" }) Assert.Contains(g.Parts, x => x.Key == k);
        var q = f.Session.Quantities(r).Single(x => x.Name == "Lapin_PavementQuantities");
        Assert.Equal(40, q.Quantities.Single(x => x.Name == "KerbLength").Value, 9);
        Assert.Equal(60, q.Quantities.Single(x => x.Name == "SidewalkArea").Value, 9);
        Assert.Equal(40, q.Quantities.Single(x => x.Name == "GutterLength").Value, 9);
    }

    /// <summary>
    /// Report #136: gutters, kerbs and sidewalks asked on both sides, as the configurator sends them (one side at a
    /// time, each a whole side object, and the old kerb switch off), stand on both sides, mirrored; no solid of the
    /// path reaches across its section (each piece was lofted to the first part of its kind at the next station,
    /// the left one, so the right side's pieces were blades across the road and the right side was missing).
    /// Straight and around a curve.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Side_pieces_asked_on_both_sides_stand_on_both_sides_and_nothing_reaches_across(bool curved)
    {
        var f = Site();
        Flat(f);
        var r = (string)f.Run("civil.addPath", new JsonObject
        {
            ["points"] = curved ? Pts((0, 0), (30, 0), (30, 30)) : Pts((0, 0), (30, 0)), ["template"] = "road", ["laneWidth"] = 3, ["crossfall"] = 2.5,
        })!["id"]!;
        if (curved) f.Run("civil.setCorridor", new JsonObject { ["id"] = r, ["curves"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(12, 0, 0), new JsonArray(0, 0, 0)) });
        var all = new JsonObject { ["gutter"] = true, ["kerb"] = true, ["sidewalk"] = true };
        f.Run("civil.updatePath", new JsonObject { ["id"] = r, ["left"] = all.DeepClone(), ["kerb"] = false });
        f.Run("civil.updatePath", new JsonObject { ["id"] = r, ["right"] = all.DeepClone(), ["kerb"] = false });
        var e = f.Model.Get(r)!;
        var t = Paths.TemplateOf(e);
        Assert.Equal(t.Left, t.Right, 9);
        var c = Corridors.ChainOf(e);
        var solids = Paths.Solids(f.Model, e);
        foreach (var k in new[] { "gutter", "kerb", "sidewalk" })
        {
            var list = solids[k];
            // Each piece wholly on one side of the axis, between the carriageway's edge and the outline.
            var sides = list.Select(s =>
            {
                var offs = s.Vertices.Select(v => c.Project(new Vec2(v.X, v.Y)).Offset).ToList();
                Assert.True(offs.All(o => o > t.Half - 1e-3) || offs.All(o => o < -t.Half + 1e-3), $"a {k} piece reaches across the road: {offs.Min():0.###} .. {offs.Max():0.###}");
                return offs[0] > 0 ? 1 : -1;
            }).ToList();
            Assert.Equal(sides.Count(x => x > 0), sides.Count(x => x < 0));
            var (left, right) = (list.Where((_, i) => sides[i] > 0).Sum(Measure.Volume), list.Where((_, i) => sides[i] < 0).Sum(Measure.Volume));
            Assert.True(left > 0);
            if (!curved) Assert.Equal(left, right, 6);
        }
        // Nothing outside the section's envelope.
        foreach (var s in solids.Values.SelectMany(x => x))
            foreach (var v in s.Vertices)
            {
                var o = c.Project(new Vec2(v.X, v.Y)).Offset;
                Assert.InRange(o, -t.Right - 1e-3, t.Left + 1e-3);
                Assert.InRange(v.Z, -1, 1);
            }
        // The 3D parts drawn are the same pieces.
        var g = f.Session.Geometry(r)!;
        Assert.Equal(solids["kerb"].Sum(Measure.Volume), g.Parts.Where(p => p.Key == "kerb").Sum(p => Measure.Volume(p.Solid)), 6);
    }

    /// <summary>Report #136: a road's width is its two lanes; the inspector shows the lane width there, not an alley's width it ignores, nor the old single kerb switch.</summary>
    [Fact]
    public void A_road_shows_its_lane_width_and_an_alley_its_width_in_the_inspector()
    {
        var f = Site();
        Flat(f);
        var cls = f.Session.Reader.Describe(IfcClass.Pavement)!;
        FieldDescriptor Field(string k) => cls.Fields.Single(x => x.Key == k);
        var road = f.Model.Get((string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts((0, 0), (30, 0)), ["template"] = "road", ["laneWidth"] = 3 })!["id"]!)!;
        var alley = f.Model.Get((string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts((0, 5), (30, 5)), ["width"] = 1.5 })!["id"]!)!;
        Assert.True(Field("laneWidth").Shown(road));
        Assert.False(Field("laneWidth").Shown(alley));
        foreach (var k in new[] { "width", "kerb", "kerbHeight", "kerbWidth" })
        {
            Assert.False(Field(k).Shown(road));
            Assert.True(Field(k).Shown(alley));
        }
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(road.Id), ["attrs"] = new JsonObject { ["laneWidth"] = 3.5 } });
        Assert.Equal(7, Paths.TemplateOf(f.Model.Get(road.Id)!).Width, 9);
    }

    [Fact]
    public void A_ditch_with_its_own_fall_has_its_bottom_on_the_red_line()
    {
        var f = Site();
        var t = Flat(f, 2);
        var d = (string)f.Run("civil.addDitch", new JsonObject { ["points"] = Pts((0, 0), (20, 0), (20, 10)), ["depth"] = 0.5, ["bottomWidth"] = 0.4, ["slope"] = 1 })!["id"]!;
        f.Run("civil.setCorridor", new JsonObject { ["id"] = d, ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(new JsonArray(0, 1.6, 0), new JsonArray(30, 1.0, 0)) } });
        var cs = f.Session.Query("civil.crossSection", new JsonObject { ["id"] = d, ["station"] = 15 })!;
        var ditch = cs["parts"]!.AsArray().Single()!["polygon"]!.AsArray();
        Assert.Equal(1.6 - 0.6 * 15 / 30, ditch.Min(v => (double)v![1]!), 9);
        var works = f.Session.Quantities(t).Single(x => x.Name == "Lapin_EarthworkQuantities" && x.Part == "pad:" + d);
        Assert.True(works.Quantities.Single(x => x.Name == "CutVolume").Value > 10);
    }

    [Fact]
    public void The_configurator_refuses_what_it_cannot_build()
    {
        var f = Site();
        Flat(f);
        var p = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts((0, 0), (20, 0)) })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("civil.setCorridor", new JsonObject { ["id"] = p, ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(10, 1, 30), new JsonArray(20, 0, 0)) } }));
        Assert.Throws<CommandException>(() => f.Run("civil.setCorridor", new JsonObject { ["id"] = p, ["profile"] = new JsonObject { ["mode"] = "design" } }));
        Assert.Throws<CommandException>(() => f.Run("civil.setCorridor", new JsonObject { ["id"] = p, ["interval"] = 0.1 }));
        Assert.Throws<CommandException>(() => f.Run("civil.updatePath", new JsonObject { ["id"] = p, ["crossfall"] = 40 }));
        f.Run("civil.setCorridor", new JsonObject { ["id"] = p, ["stationStart"] = 100, ["interval"] = 5, ["sections"] = new JsonArray(112.5, 104) });
        var ls = f.Session.Query("civil.corridor", new JsonObject { ["id"] = p })!;
        Assert.Equal([104, 112.5], ls["spec"]!["sections"]!.AsArray().Select(x => (double)x!));
        Assert.Equal(5, ls["stations"]!.AsArray().Count);
    }
}
