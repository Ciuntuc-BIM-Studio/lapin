using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Site grading T5 (docs/plan-site-grading.md; forum 230 §4): grading between chosen edges (benlungu's two plazas at −2.00 and
/// −1.00 with curved edges bridged by a green slope), live as its edges change; slopes from an edge down or up to the ground at
/// 1:N in cut and 1:M in fill, the daylight line exact on the ground's triangles, convex corners fanned and concave ones met on
/// their bisector, refused beyond the widest reach; one undo step; a project without gradings as before; IFC both ways.
/// </summary>
public sealed class GradingTests
{
    static Fixture F() => new(new ArchitectureModule(), new CivilModule());
    static JsonArray P(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);
    static JsonArray Pts(params double[][] pts) => new([.. pts.Select(p => (JsonNode)P(p))]);
    static JsonArray Rect(double x0, double y0, double x1, double y1) => Pts([x0, y0], [x1, y0], [x1, y1], [x0, y1]);

    /// <summary>A 40 × 40 m terrain surveyed every 2 m, heights z(x, y), datum 0.</summary>
    static string Field(Fixture f, Func<double, double, double>? z = null)
    {
        z ??= (_, _) => 0;
        var pts = new JsonArray();
        for (var i = 0; i <= 20; i++) for (var j = 0; j <= 20; j++) pts.Add(P(i * 2.0, j * 2.0, z(i * 2.0, j * 2.0)));
        return (string)f.Run("civil.addTerrain", new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts, ["datum"] = 0, ["interval"] = 0.5, ["depth"] = 2 })!["id"]!;
    }

    static string Hardscape(Fixture f, JsonObject a) => (string)f.Run("civil.addHardscape", a)!["id"]!;

    static Quantity Q(Fixture f, string id, string set, string name) =>
        f.Session.Quantities(id).First(s => s.Name == set).Quantities.First(q => q.Name == name);

    static Gradings.Shape Shape(Fixture f, string id) => Gradings.ShapeOf(f.Model, f.Model.Get(id)!);

    /// <summary>
    /// benlungu's case: a lower plaza at −2.00 whose edge towards the other bulges out in an arc, an upper one at −1.00 whose edge
    /// towards it curves in, on ground at −1.50.
    /// </summary>
    static (Fixture F, string T, string Lower, string Upper) Plazas()
    {
        var f = F();
        var t = Field(f, (_, _) => -1.5);
        var lower = Hardscape(f, new JsonObject { ["points"] = Rect(5, 5, 15, 15), ["arcs"] = P(0, -2, 0, 0), ["elevation"] = -2, ["type"] = "pavers", ["name"] = "Lower plaza" });
        var upper = Hardscape(f, new JsonObject { ["points"] = Rect(22, 5, 32, 15), ["arcs"] = P(0, 0, 0, 2), ["elevation"] = -1, ["type"] = "pavers", ["name"] = "Upper plaza" });
        return (f, t, lower, upper);
    }

    static string Bridge(Fixture f, string lower, string upper) =>
        (string)f.Run("civil.addGrading", new JsonObject { ["kind"] = "patch", ["edges"] = new JsonArray(new JsonObject { ["id"] = lower }, new JsonObject { ["id"] = upper }) })!["id"]!;

    [Fact]
    public void Two_plazas_at_different_levels_are_bridged_by_a_patch_whose_edges_hold_their_heights_with_no_gap()
    {
        var (f, t, lower, upper) = Plazas();
        var id = Bridge(f, lower, upper);
        var e = f.Model.Get(id)!;
        Assert.Equal(Gradings.Class, e.Class);
        Assert.Equal(t, e.Str(Gradings.TerrainKey));
        var s = Shape(f, id);
        Assert.Empty(s.Problems);
        // Ruled between the two facing stretches: the lower plaza's arc (out to x = 17) and the upper's (in to x = 24).
        Assert.Equal("loft", s.Mode);
        Assert.NotEmpty(s.Rulings);
        var low = s.Ring.Where(p => Math.Abs(p.Z + 2) < 1e-9).ToList();
        var high = s.Ring.Where(p => Math.Abs(p.Z + 1) < 1e-9).ToList();
        Assert.Equal(s.Ring.Count, low.Count + high.Count);
        Assert.True(low.Max(p => p.X) > 16.9 && low.All(p => p.X >= 15 - 1e-9));
        Assert.True(high.Min(p => p.X) > 21.9 && high.All(p => p.X <= 24 + 1e-9));
        // The arcs' stretches only: none of the plazas' far sides.
        Assert.DoesNotContain(s.Ring, p => p.X < 14 || p.X > 25);
        // The ground holds both edges exactly, at their points and between them.
        var ground = Terrains.Of(f.Model.Get(t)!);
        for (var i = 0; i < s.Ring.Count; i++)
        {
            var (a, b) = (s.Ring[i], s.Ring[(i + 1) % s.Ring.Count]);
            Assert.Equal(a.Z, ground.ElevationAt(a.X, a.Y)!.Value, 9);
            if (Math.Abs(a.Z - b.Z) < 1e-12)
            {
                var mid = (a + b) * 0.5;
                Assert.Equal(a.Z, ground.ElevationAt(mid.X, mid.Y)!.Value, 9);
            }
        }
        // No gap: the ground covers the whole patch, between the two levels.
        var ring = s.Ring.Select(p => p.Xy).ToList();
        for (var x = 14.0; x <= 25; x += 0.25)
            for (var y = 4.0; y <= 16; y += 0.25)
            {
                if (!Polygon2.Contains(ring, new Vec2(x, y))) continue;
                var z = ground.ElevationAt(x, y);
                Assert.NotNull(z);
                Assert.InRange(z!.Value, -2 - 1e-9, -1 + 1e-9);
            }
        // Its area is its boundary's, exactly; its cut and fill against the ground at −1.50 as a fine grid counts them.
        Assert.Equal(Math.Abs(Polygon2.SignedArea(ring)), Q(f, id, "Lapin_GradingQuantities", "PlanArea").Value, 6);
        double cut = 0, fill = 0;
        const double h = 0.02;
        for (var x = 14.0 + h / 2; x < 25; x += h)
            for (var y = 4.0 + h / 2; y < 16; y += h)
            {
                if (!Polygon2.Contains(ring, new Vec2(x, y)) || ground.ElevationAt(x, y) is not { } z) continue;
                var dz = z + 1.5;
                if (dz > 0) fill += dz * h * h; else cut -= dz * h * h;
            }
        Assert.InRange(Q(f, id, "Lapin_EarthworkQuantities", "CutVolume").Value, cut * 0.99, cut * 1.01);
        Assert.InRange(Q(f, id, "Lapin_EarthworkQuantities", "FillVolume").Value, fill * 0.99, fill * 1.01);
        Assert.True(cut > 1 && fill > 1);
        // In 3D a grass skin over it, on the ground's own triangles; in plan its boundary.
        var g = f.Session.Geometry(id)!;
        Assert.Single(g.Parts);
        Assert.Equal("Grass", f.Model.Get(g.Parts[0].Material!)!.Name);
        Assert.Contains(g.Plan!, p => p.Closed);
        // The terrain says where its survey went, and draws its contours on through the patch.
        Assert.Contains(f.Session.Geometry(t)!.Diagnostics!, d => d.Contains("inside a grading"));
        Assert.Contains(f.Session.Geometry(t)!.Plan!, p => p.Kind is "contour" or "contourMajor" && p.Points.Any(q => Polygon2.Contains(ring, q)));
    }

    [Fact]
    public void The_patch_follows_a_plaza_that_changes_in_the_same_undo_step()
    {
        var (f, t, lower, upper) = Plazas();
        var id = Bridge(f, lower, upper);
        f.Run("civil.updateHardscape", new JsonObject { ["id"] = upper, ["elevation"] = -0.5 });
        var s = Shape(f, id);
        Assert.Contains(s.Ring, p => Math.Abs(p.Z + 0.5) < 1e-9);
        Assert.DoesNotContain(s.Ring, p => Math.Abs(p.Z + 1) < 1e-9);
        var ground = Terrains.Of(f.Model.Get(t)!);
        foreach (var p in s.Ring.Where(p => Math.Abs(p.Z + 0.5) < 1e-9)) Assert.Equal(-0.5, ground.ElevationAt(p.X, p.Y)!.Value, 9);
        // Moved in plan, the plaza takes the patch with it.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(upper), ["op"] = "move", ["by"] = P(2, 0) });
        var moved = Shape(f, id);
        Assert.True(moved.Ring.Max(p => p.X) > 25.9, string.Join(" ", moved.Ring.Select(p => $"({p.X:0.##},{p.Y:0.##},{p.Z:0.##})")) + " | " + string.Join(";", moved.Problems) + " | " + string.Join(" ", Hardscapes.Corners(f.Model.Get(upper)!)));
        f.Session.Undo();
        f.Session.Undo();
        s = Shape(f, id);
        Assert.Contains(s.Ring, p => Math.Abs(p.Z + 1) < 1e-9);
        ground = Terrains.Of(f.Model.Get(t)!);
        foreach (var p in s.Ring.Where(p => Math.Abs(p.Z + 1) < 1e-9)) Assert.Equal(-1, ground.ElevationAt(p.X, p.Y)!.Value, 9);
    }

    [Fact]
    public void An_edge_of_its_own_takes_typed_heights_and_a_linked_one_refuses_them_until_detached()
    {
        var (f, t, lower, upper) = Plazas();
        // A patch from the lower plaza to a line of its own on the ground, its heights typed.
        var own = (string)f.Run("civil.addGrading", new JsonObject
        {
            ["kind"] = "patch", ["edges"] = new JsonArray(new JsonObject { ["id"] = lower }, new JsonObject { ["points"] = Pts([20, 4], [20, 16]) }),
        })!["id"]!;
        var line = Shape(f, own).Edges[1];
        Assert.True(line.Own);
        Assert.All(line.Points, p => Assert.Equal(-1.5, p.Z, 9));
        f.Run("civil.setGradingPoints", new JsonObject { ["id"] = own, ["edge"] = 1, ["indices"] = new JsonArray(0), ["z"] = -0.8 });
        var ground = Terrains.Of(f.Model.Get(t)!);
        Assert.Equal(-0.8, ground.ElevationAt(20, 4)!.Value, 9);
        Assert.Equal(-1.5, ground.ElevationAt(20, 16)!.Value, 9);
        Assert.Equal(-1.15, ground.ElevationAt(20, 10)!.Value, 9);
        f.Session.Undo();
        Assert.Equal(-1.5, Terrains.Of(f.Model.Get(t)!).ElevationAt(20, 4)!.Value, 9);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(own) });
        // An edge that follows a plaza takes the plaza's heights: refused, naming it, until it is detached.
        var id = Bridge(f, lower, upper);
        var ex = Assert.Throws<CommandException>(() => f.Run("civil.setGradingPoints", new JsonObject { ["id"] = id, ["edge"] = 1, ["indices"] = new JsonArray(0), ["z"] = -0.8 }));
        Assert.Contains("Upper plaza", ex.Message);
        var was = Shape(f, id).Ring;
        f.Run("civil.detachGradingEdge", new JsonObject { ["id"] = id, ["edge"] = 1 });
        Assert.Null(Shape(f, id).Edges[1].Source);
        Assert.Equal(was.Count, Shape(f, id).Ring.Count);
        f.Run("civil.setGradingPoints", new JsonObject { ["id"] = id, ["edge"] = 1, ["dz"] = 0.25 });
        Assert.All(Shape(f, id).Edges[1].Points, p => Assert.Equal(-0.75, p.Z, 9));
        // Detached, it no longer follows the plaza.
        f.Run("civil.updateHardscape", new JsonObject { ["id"] = upper, ["elevation"] = 0 });
        Assert.DoesNotContain(Shape(f, id).Ring, p => Math.Abs(p.Z) < 1e-9);
    }

    [Fact]
    public void Standalone_the_patch_is_a_surface_of_its_own()
    {
        var f = F();
        var a = Hardscape(f, new JsonObject { ["points"] = Rect(0, 0, 10, 10), ["elevation"] = -2, ["standalone"] = true, ["storeyId"] = f.Storey });
        var b = Hardscape(f, new JsonObject { ["points"] = Rect(16, 0, 26, 10), ["elevation"] = -1, ["standalone"] = true, ["storeyId"] = f.Storey });
        var id = Bridge(f, a, b);
        Assert.Null(f.Model.Get(id)!.Str(Gradings.TerrainKey));
        var s = Shape(f, id);
        Assert.Equal(60, s.PlanArea, 6);
        // A ruled surface between two parallel straight edges: a plane rising 1 m over 6.
        foreach (var (pa, pb, pc) in s.Surface!.Triangles)
            foreach (var p in new[] { s.Surface.Points[pa], s.Surface.Points[pb], s.Surface.Points[pc] })
                Assert.Equal(-2 + (p.X - 10) / 6, p.Z, 9);
        Assert.Single(f.Session.Geometry(id)!.Parts);
        Assert.DoesNotContain(f.Session.Quantities(id), q => q.Name == "Lapin_EarthworkQuantities");
    }

    /// <summary>A terrain rising 10 % towards +x (0 at x = 0), a level hardscape at 1.50 over its middle: ground below it at the west, above at the east.</summary>
    static (Fixture F, string T, string H) Hillside()
    {
        var f = F();
        var t = Field(f, (x, _) => 0.1 * x);
        var h = Hardscape(f, new JsonObject { ["points"] = Rect(10, 10, 20, 20), ["elevation"] = 1.5, ["type"] = "gravel", ["name"] = "Yard" });
        return (f, t, h);
    }

    [Fact]
    public void A_slope_from_an_edge_meets_the_ground_at_1_to_2_in_cut_and_1_to_3_in_fill_its_daylight_line_exact()
    {
        var (f, t, h) = Hillside();
        var id = (string)f.Run("civil.addGrading", new JsonObject { ["kind"] = "slope", ["edges"] = new JsonArray(new JsonObject { ["id"] = h }), ["cutSlope"] = "1:2", ["fillSlope"] = 3 })!["id"]!;
        var s = Shape(f, id);
        Assert.Empty(s.Problems);
        var strip = Assert.Single(s.Strips);
        Assert.True(strip.Closed);
        var baseGround = Terrains.Base(f.Model.Get(t)!);
        for (var i = 0; i < strip.Origins.Count; i++)
        {
            var (o, d) = (strip.Origins[i], strip.Ends[i]);
            // Every daylight point on the ground as it was, exactly.
            Assert.Equal(d.Z, baseGround.ElevationAt(d.X, d.Y)!.Value, 9);
            var run = (d.Xy - o.Xy).Length;
            if (run < 1e-9) continue;
            // Square to a straight side (not at a corner): the full slope, 1:2 up in cut, 1:3 down in fill.
            var corner = (o.X is 10 or 20) && (o.Y is 10 or 20);
            if (!corner) Assert.Equal(strip.Cut[i] ? 0.5 : -1.0 / 3, (d.Z - o.Z) / run, 9);
        }
        // The east side cuts: 1.50 + s/2 = 2.00 + s/10, s = 1.25; the west fills: 1.50 − s/3 = 1.00 − s/10, s = 15/7.
        Assert.Contains(strip.Ends, d => Math.Abs(d.X - 21.25) < 1e-9 && Math.Abs(d.Y - 15) < 1e-9 && Math.Abs(d.Z - 2.125) < 1e-9);
        Assert.Contains(strip.Ends, d => Math.Abs(d.X - (10 - 15.0 / 7)) < 1e-9 && Math.Abs(d.Y - 15) < 1e-9 && Math.Abs(d.Z - 1 + 1.5 / 7) < 1e-9);
        Assert.Contains(strip.Cut, c => c);
        Assert.Contains(strip.Cut, c => !c);
        // The convex corners fan: from the north-east corner, rays every 10° at most, each a line of a cone at 1:2.
        var fan = Enumerable.Range(0, strip.Origins.Count).Where(i => (strip.Origins[i].Xy - new Vec2(20, 20)).Length < 1e-9).ToList();
        Assert.True(fan.Count >= 10);
        foreach (var i in fan)
        {
            var dir = (strip.Ends[i].Xy - strip.Origins[i].Xy).Normalized();
            var sExpected = 0.5 / (0.5 - 0.1 * dir.X);
            Assert.Equal(sExpected, (strip.Ends[i].Xy - strip.Origins[i].Xy).Length, 9);
        }
        // The terrain holds the daylight line and the edge exactly.
        var ground = Terrains.Of(f.Model.Get(t)!);
        foreach (var d in strip.Ends) Assert.Equal(d.Z, ground.ElevationAt(d.X, d.Y)!.Value, 9);
        foreach (var o in strip.Origins) Assert.Equal(1.5, ground.ElevationAt(o.X, o.Y)!.Value, 9);
        // In plan: its daylight line, and hachures from the top of the slope.
        Assert.Contains(f.Session.Geometry(id)!.Plan!, p => p.Style?.Pattern == "dashed");
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var drawing = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
        Assert.Contains(drawing["pens"]!.AsArray().OfType<JsonObject>(), p => (string?)p["layer"] == CivilModule.GradingLayer);
        // Quantities: both cut and fill; written as the earthwork it mostly is.
        Assert.True(Q(f, id, "Lapin_EarthworkQuantities", "CutVolume").Value > 0);
        Assert.True(Q(f, id, "Lapin_EarthworkQuantities", "FillVolume").Value > 0);
        Assert.True(Q(f, id, "Lapin_GradingQuantities", "DaylightLength").Value > 40);
    }

    [Fact]
    public void A_concave_corner_meets_on_its_bisector_and_every_daylight_point_lies_its_width_from_the_edge()
    {
        var f = F();
        var t = Field(f);
        // An L at +1.00 on level ground: a fill of 1 m at 1:3 runs 3 m out everywhere, round the convex corners too.
        var h = Hardscape(f, new JsonObject { ["points"] = Pts([10, 10], [24, 10], [24, 16], [16, 16], [16, 24], [10, 24]), ["elevation"] = 1, ["type"] = "gravel" });
        var id = (string)f.Run("civil.addGrading", new JsonObject { ["kind"] = "slope", ["edges"] = new JsonArray(new JsonObject { ["id"] = h }) })!["id"]!;
        var s = Shape(f, id);
        var strip = Assert.Single(s.Strips);
        var outline = new List<Vec2> { new(10, 10), new(24, 10), new(24, 16), new(16, 16), new(16, 24), new(10, 24) };
        double Distance(Vec2 p) => Enumerable.Range(0, outline.Count).Min(i => Polyline2.SegmentDistance(p, outline[i], outline[(i + 1) % outline.Count]));
        foreach (var d in strip.Ends)
        {
            Assert.Equal(0, d.Z, 9);
            Assert.Equal(3, Distance(d.Xy), 9);
        }
        // The concave corner's ray along its bisector, to (19, 19); the rays near it that would have crossed it left out, said.
        Assert.Contains(Enumerable.Range(0, strip.Origins.Count), i => (strip.Origins[i].Xy - new Vec2(16, 16)).Length < 1e-9 && (strip.Ends[i].Xy - new Vec2(19, 19)).Length < 1e-9);
        Assert.Contains(s.Problems, p => p.Contains("concave bend"));
        for (var i = 0; i < strip.Origins.Count; i++)
            for (var j = i + 1; j < strip.Origins.Count; j++)
                Assert.False(Polyline2.Crosses(strip.Origins[i].Xy, strip.Ends[i].Xy, strip.Origins[j].Xy, strip.Ends[j].Xy));
        // Its fill: the 1 m embankment, a prism of 1.5 m² a metre along the outline's length plus the cones and less the bend.
        var fill = Q(f, id, "Lapin_EarthworkQuantities", "FillVolume").Value;
        Assert.InRange(fill, 1.5 * 52 * 0.9, 1.5 * 52 * 1.2);
    }

    [Fact]
    public void A_slope_that_does_not_reach_the_ground_within_its_widest_is_refused_or_stops_and_says_so()
    {
        var f = F();
        var t = Field(f);
        var h = Hardscape(f, new JsonObject { ["points"] = Rect(10, 10, 20, 20), ["elevation"] = 2, ["type"] = "gravel" });
        // 2 m of fill at 1:3 needs 6 m: refused within 4.
        var ex = Assert.Throws<CommandException>(() => f.Run("civil.addGrading", new JsonObject { ["kind"] = "slope", ["edges"] = new JsonArray(new JsonObject { ["id"] = h }), ["maxWidth"] = 4 }));
        Assert.Contains("within 4 m", ex.Message);
        // Near the terrain's edge it stops where the ground runs out: the rest is graded, and it says where it stopped.
        var near = Hardscape(f, new JsonObject { ["points"] = Rect(30, 10, 36, 20), ["elevation"] = 2, ["type"] = "gravel" });
        var id = (string)f.Run("civil.addGrading", new JsonObject { ["kind"] = "slope", ["edges"] = new JsonArray(new JsonObject { ["id"] = near }) })!["id"]!;
        var s = Shape(f, id);
        Assert.NotEmpty(s.Strips);
        Assert.All(s.Strips, x => Assert.False(x.Closed));
        Assert.Contains(s.Problems, p => p.Contains("it stops there"));
        Assert.Contains(f.Session.Geometry(id)!.Diagnostics!, d => d.Contains("it stops there"));
        // A level to reach: down to +0.50 at 1:3 runs 4.5 m out.
        var lv = (string)f.Run("civil.addGrading", new JsonObject { ["kind"] = "slope", ["edges"] = new JsonArray(new JsonObject { ["id"] = h }), ["target"] = "level", ["level"] = 0.5 })!["id"]!;
        Assert.All(Shape(f, lv).Strips.SelectMany(x => x.Ends), d => Assert.Equal(0.5, d.Z, 9));
    }

    [Fact]
    public void One_undo_step_and_a_project_without_gradings_as_before()
    {
        var (f, t, lower, upper) = Plazas();
        var before = f.Model.Get(t)!.Attrs[FeatureLines.Derived]!.ToJsonString();
        Assert.DoesNotContain(Gradings.GradedKey, before);
        var tinBefore = Terrains.Of(f.Model.Get(t)!).Tin;
        var id = Bridge(f, lower, upper);
        Assert.NotEqual(before, f.Model.Get(t)!.Attrs[FeatureLines.Derived]!.ToJsonString());
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
        Assert.Equal(before, f.Model.Get(t)!.Attrs[FeatureLines.Derived]!.ToJsonString());
        // Deleted, it leaves the terrain exactly as it was.
        id = Bridge(f, lower, upper);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(id) });
        Assert.Equal(before, f.Model.Get(t)!.Attrs[FeatureLines.Derived]!.ToJsonString());
        var tinAfter = Terrains.Of(f.Model.Get(t)!).Tin;
        Assert.Equal(tinBefore.Points.Count, tinAfter.Points.Count);
        Assert.Equal(tinBefore.Triangles, tinAfter.Triangles);
        // A terrain with nothing on it keeps no derived lines.
        var g = F();
        var bare = Field(g);
        Assert.Null(g.Model.Get(bare)!.Attrs[FeatureLines.Derived]);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC4X3")]
    public void It_goes_to_IFC_as_an_earthwork_and_comes_back(string schema)
    {
        var (f, t, h) = Hillside();
        var slope = (string)f.Run("civil.addGrading", new JsonObject { ["kind"] = "slope", ["edges"] = new JsonArray(new JsonObject { ["id"] = h }), ["fillSlope"] = 2 })!["id"]!;
        var e = f.Model.Get(slope)!;
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "grading.ifc", new IfcExportOptions(Schema: schema));
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        if (schema == "IFC4X3")
            Assert.True(text.Contains("IFCEARTHWORKSFILL(") || text.Contains("IFCEARTHWORKSCUT("));
        else
        {
            Assert.DoesNotContain("IFCEARTHWORKSFILL(", text);
            Assert.Contains("IFCBUILDINGELEMENTPROXY(", text);
        }
        var g = F();
        IfcImport.Run(g.Session, ms.ToArray(), "grading.ifc", null, out _);
        var back = g.Model.Get(slope);
        Assert.NotNull(back);
        Assert.Equal(Gradings.Class, back!.Class);
        Assert.Equal(t, back.Str(Gradings.TerrainKey));
        Assert.Equal(e.Attrs["edges"]!.ToJsonString(), back.Attrs["edges"]!.ToJsonString());
        Assert.Equal(Shape(f, slope).PlanArea, Gradings.ShapeOf(g.Model, back).PlanArea, 6);
        // The terrain comes back holding it.
        var ground = Terrains.Of(g.Model.Get(t)!);
        foreach (var d in Gradings.ShapeOf(g.Model, back).Strips[0].Ends) Assert.Equal(d.Z, ground.ElevationAt(d.X, d.Y)!.Value, 6);
    }
}
