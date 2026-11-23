using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Site grading T2 (docs/plan-site-grading.md): feature lines as the terrain's breaklines (the surface passes exactly through
/// them, no TIN edge crosses them, the points between stay), contours drawn at an elevation, a contour dragged in plan with
/// Shepard's fading within a reach, grades typed between corners, the refusal of a corner off every terrain (Î6), T1's
/// follow-ups, a terrain without feature lines built as before, undo, and IFC (an IfcAnnotation read back).
/// </summary>
public sealed class FeatureLineTests
{
    static Fixture F() => new(new ArchitectureModule(), new CivilModule());
    static JsonArray P(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);
    static JsonArray Pts(params double[][] pts) => new([.. pts.Select(p => (JsonNode)P(p))]);

    /// <summary>A 40 × 40 m terrain with a point every 2 m (21 × 21), heights z(x, y), datum 0.</summary>
    static string Field(Fixture f, Func<double, double, double>? z = null, double datum = 0)
    {
        z ??= (_, _) => 0;
        var pts = new JsonArray();
        for (var i = 0; i <= 20; i++) for (var j = 0; j <= 20; j++) pts.Add(P(i * 2.0, j * 2.0, z(i * 2.0, j * 2.0) + datum));
        return (string)f.Run("civil.addTerrain", new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts, ["datum"] = datum, ["interval"] = 0.5, ["majorEvery"] = 2, ["depth"] = 1 })!["id"]!;
    }

    static string Line(Fixture f, JsonObject a) => (string)f.Run("civil.addFeatureLine", a)!["id"]!;

    static Terrain Ground(Fixture f, string t) => Terrains.Of(f.Model.Get(t)!);

    /// <summary>Whether two segments cross in plan inside both (not at an end).</summary>
    static bool Crosses(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        double O(Vec2 p, Vec2 q, Vec2 r) => Vec2.Cross(q - p, r - p);
        double d1 = O(c, d, a), d2 = O(c, d, b), d3 = O(a, b, c), d4 = O(a, b, d);
        var len = Math.Max((b - a).Length, (d - c).Length);
        var eps = 1e-9 * len * len;
        return (d1 > eps && d2 < -eps || d1 < -eps && d2 > eps) && (d3 > eps && d4 < -eps || d3 < -eps && d4 > eps);
    }

    [Fact]
    public void A_feature_line_is_a_breakline_the_surface_passes_through_exactly_and_no_TIN_edge_crosses()
    {
        var f = F();
        var t = Field(f);
        var before = Terrains.Points(f.Model.Get(t)!);
        // A line across the flat ground, its corners at 1, 3 and 2 m, between the survey's points.
        var id = Line(f, new JsonObject { ["points"] = Pts([3.1, 5.3, 1], [21.7, 19.9, 3], [35.3, 27.1, 2]) });
        var e = f.Model.Get(id)!;
        Assert.Equal(FeatureLines.Class, e.Class);
        Assert.Equal(t, e.Str(FeatureLines.TerrainKey));
        var g = Ground(f, t);
        var line = new[] { new Vec3(3.1, 5.3, 1), new Vec3(21.7, 19.9, 3), new Vec3(35.3, 27.1, 2) };
        // Its heights everywhere along it, exactly (the TIN's edges run along it).
        for (var k = 0; k + 1 < line.Length; k++)
            for (var s = 0.0; s <= 1.0 + 1e-12; s += 1.0 / 64)
            {
                var p = line[k] + (line[k + 1] - line[k]) * s;
                Assert.Equal(p.Z, g.ElevationAt(p.X, p.Y)!.Value, 9);
            }
        // No TIN edge crosses a segment of it: every crossing is split into a vertex of the line.
        var tin = g.Tin;
        foreach (var (a, b, c) in tin.Triangles)
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
                for (var k = 0; k + 1 < line.Length; k++)
                    Assert.False(Crosses(tin.Points[u].Xy, tin.Points[v].Xy, line[k].Xy, line[k + 1].Xy), $"edge {u}-{v} crosses segment {k}");
        // The survey's points stay where they are, at their heights; none was moved.
        Assert.Equal(before.Count, Terrains.Points(f.Model.Get(t)!).Count);
        foreach (var p in tin.Points.Where(p => before.Any(q => (q.Xy - p.Xy).Length < 1e-9))) Assert.Equal(0, p.Z, 12);
        // The terrain holds the line it is shaped by, and its quantities: lowest 0, highest 3.
        Assert.NotNull(f.Model.Get(t)!.Attrs[FeatureLines.Derived]);
        Assert.Equal(3, g.Bounds.Max.Z, 9);
    }

    [Fact]
    public void A_survey_point_within_the_tolerance_is_left_out_and_the_terrain_says_so()
    {
        var f = F();
        var t = Field(f);
        // Through the point (10, 10) and 3 cm past (20, 20.03): both conflict with it (tolerance 5 cm).
        Line(f, new JsonObject { ["points"] = Pts([6, 6, 2], [24, 24.06, 2]) });
        var g = Ground(f, t);
        Assert.DoesNotContain(g.Tin.Points, p => (p.Xy - new Vec2(20, 20)).Length < 1e-9);
        var on = new Vec2(20, 6 + 14 * 18.06 / 18);
        Assert.Equal(2, g.ElevationAt(on.X, on.Y)!.Value, 9);
        var geo = f.Session.Geometry(t)!;
        Assert.Contains(geo.Diagnostics!, d => d.Contains("tolerance"));
        // The survey keeps its points: only the triangulation leaves them out.
        Assert.Contains(Terrains.Points(f.Model.Get(t)!), p => (p.Xy - new Vec2(20, 20)).Length < 1e-9);
    }

    [Fact]
    public void A_terrain_without_feature_lines_is_built_as_before_and_one_deleted_leaves_it_so_again()
    {
        var f = F();
        var t = Field(f, (x, y) => 0.05 * x + 0.02 * y + Math.Sin(x / 7));
        var te = f.Model.Get(t)!;
        Assert.Null(te.Attrs[FeatureLines.Derived]);
        // The same TIN the survey alone triangulates.
        var plain = Tin.Delaunay(Terrains.Points(te));
        var was = Ground(f, t).Tin;
        Assert.Equal(plain.Points.Count, was.Points.Count);
        Assert.Equal(plain.Triangles, was.Triangles);
        var id = Line(f, new JsonObject { ["points"] = Pts([5, 5, 4], [30, 12, 1]) });
        Assert.NotEqual(was.Triangles.Count, Ground(f, t).Tin.Triangles.Count);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(id) });
        var te2 = f.Model.Get(t)!;
        Assert.Null(te2.Attrs[FeatureLines.Derived]);
        Assert.Equal(was.Triangles, Ground(f, t).Tin.Triangles);
        // GlobalId kept throughout.
        Assert.Equal(t, te2.Id);
    }

    [Fact]
    public void Adding_a_feature_line_is_one_undo_step_with_the_terrain()
    {
        var f = F();
        var t = Field(f);
        var was = Ground(f, t).Tin.Triangles.ToList();
        var id = Line(f, new JsonObject { ["points"] = Pts([5, 5, 1], [30, 9, 1]) });
        Assert.NotNull(f.Model.Get(t)!.Attrs[FeatureLines.Derived]);
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
        Assert.Null(f.Model.Get(t)!.Attrs[FeatureLines.Derived]);
        Assert.Equal(was, Ground(f, t).Tin.Triangles);
        f.Session.Redo();
        Assert.NotNull(f.Model.Get(id));
        Assert.NotNull(f.Model.Get(t)!.Attrs[FeatureLines.Derived]);
    }

    [Fact]
    public void A_contour_drawn_at_an_elevation_holds_the_ground_at_it_arcs_in_chords_and_draws_at_that_level()
    {
        var f = F();
        // A slope rising 0.1 m a metre east: 2.5 m runs along x = 25.
        var t = Field(f, (x, _) => 0.1 * x);
        // A closed contour round (20, 20) of radius 6: four quarter circles.
        var (corners, rises) = ArcOutline.Circle(new Vec2(20, 20), 6);
        var id = Line(f, new JsonObject
        {
            ["points"] = new JsonArray([.. corners.Select(c => (JsonNode)P(c.X, c.Y))]), ["arcs"] = new JsonArray([.. rises.Select(x => (JsonNode)x)]),
            ["closed"] = true, ["elevation"] = 2.5,
        });
        // Two corners only: a closed line needs three.
        Assert.Throws<CommandException>(() => f.Run("civil.updateFeatureLine", new JsonObject { ["id"] = id, ["points"] = Pts([1, 1], [5, 5]) }));
        var e = f.Model.Get(id)!;
        Assert.StartsWith("Contour 2.50", e.Name);
        var s = FeatureLines.SpecOf(e);
        Assert.True(s.Closed);
        var line = FeatureLines.Line(s, FeatureLines.CornerZ(s, _ => null));
        Assert.True(line.Count > 12);
        var g = Ground(f, t);
        foreach (var p in line)
        {
            Assert.Equal(6, (p.Xy - new Vec2(20, 20)).Length, 6);
            Assert.Equal(2.5, g.ElevationAt(p.X, p.Y)!.Value, 9);
        }
        // Halfway along each chord too.
        for (var i = 0; i + 1 < line.Count; i++)
        {
            var m = (line[i].Xy + line[i + 1].Xy) * 0.5;
            Assert.Equal(2.5, g.ElevationAt(m.X, m.Y)!.Value, 9);
        }
        // The terrain's own contour at 2.5 m runs along it, and in plan it has its own line style.
        var geo = f.Session.Geometry(id)!;
        var item = Assert.Single(geo.Plan!);
        Assert.Equal(FeatureLines.Color, item.Style!.Color);
        Assert.True(item.Closed);
        Assert.NotNull(geo.Lines);
        var chains = Terrains.ChainedContours(g, 2.5, []);
        Assert.Contains(chains, c => c.Count > 8 && c.All(p => Math.Abs((p - new Vec2(20, 20)).Length - 6) < 1e-6));
    }

    [Fact]
    public void A_grade_typed_between_two_corners_sets_the_next_one_and_follows_the_first()
    {
        var f = F();
        var t = Field(f);
        var id = Line(f, new JsonObject { ["points"] = Pts([4, 4, 1], [24, 4], [24, 24, 3]) });
        // The middle corner without a height of its own takes the ground's.
        var props = FeatureLines.CornerZ(FeatureLines.SpecOf(f.Model.Get(id)!), _ => 0);
        Assert.Equal(0, props[1]!.Value, 12);
        var r = f.Run("civil.setFeatureLineGrade", new JsonObject { ["id"] = id, ["segment"] = 0, ["grade"] = "2 %" })!;
        Assert.Equal(1.4, (double)r["z"]![1]!, 9);
        var g = Ground(f, t);
        Assert.Equal(1.4, g.ElevationAt(24, 4)!.Value, 9);
        Assert.Equal(1.2, g.ElevationAt(14, 4)!.Value, 9);
        // The first corner raised by a metre: the second follows at the grade.
        f.Run("civil.setFeatureLinePoints", new JsonObject { ["id"] = id, ["indices"] = new JsonArray(0), ["z"] = 2 });
        Assert.Equal(2.4, Ground(f, t).ElevationAt(24, 4)!.Value, 9);
        // One in fifty, keeping the end: the last corner (3 m) holds, the middle one is 3 − 20/50.
        f.Run("civil.setFeatureLineGrade", new JsonObject { ["id"] = id, ["segment"] = 1, ["grade"] = "1:50", ["keep"] = "end" });
        var z = FeatureLines.CornerZ(FeatureLines.SpecOf(f.Model.Get(id)!), _ => 0);
        Assert.Equal(3 - 0.4, z[1]!.Value, 9);
        Assert.Equal(3, z[2]!.Value, 9);
        Assert.Equal(0.02, FeatureLines.Grade(JsonValue.Create("1:50"))!.Value, 12);
        Assert.Equal(-0.015, FeatureLines.Grade(JsonValue.Create("-1,5 %"))!.Value, 12);
        // A contour is level: no grade.
        var c = Line(f, new JsonObject { ["points"] = Pts([4, 30], [30, 30]), ["elevation"] = 0.5 });
        Assert.Throws<CommandException>(() => f.Run("civil.setFeatureLineGrade", new JsonObject { ["id"] = c, ["segment"] = 0, ["grade"] = 0.01 }));
    }

    [Fact]
    public void A_draped_line_follows_the_ground_when_the_survey_changes()
    {
        var f = F();
        var t = Field(f, (x, _) => 0.1 * x);
        var id = Line(f, new JsonObject { ["points"] = Pts([5, 5, 9], [30, 30, 9]), ["drape"] = true });
        var derived = FeatureLines.OnTerrain(f.Model.Get(t)!);
        Assert.Equal(0.5, derived[0].Points[0].Z, 9);
        // The survey raised a metre everywhere: the draped line goes with it, in the same step.
        var pts = new JsonArray([.. Terrains.Points(f.Model.Get(t)!).Select(p => (JsonNode)P(p.X, p.Y, p.Z + 1))]);
        f.Run("civil.updateTerrain", new JsonObject { ["id"] = t, ["points"] = pts });
        Assert.Equal(1.5, FeatureLines.OnTerrain(f.Model.Get(t)!)[0].Points[0].Z, 9);
        Assert.Equal(f.Model.Get(id)!.Id, id);
    }

    [Fact]
    public void A_contour_dragged_moves_with_Shepard_fading_within_its_reach_and_nothing_beyond()
    {
        var f = F();
        var t = Field(f, (x, _) => 0.1 * x);
        var before = Terrains.Points(f.Model.Get(t)!);
        var (from, to, reach) = (new Vec2(20, 20), new Vec2(24, 20), 10.0);
        var r = f.Run("civil.dragContour", new JsonObject { ["id"] = t, ["elevation"] = 2, ["from"] = P(20, 20), ["to"] = P(24, 20), ["reach"] = reach })!;
        var id = (string)r["id"]!;
        var e = f.Model.Get(id)!;
        Assert.Equal(2, FeatureLines.SpecOf(e).Elevation);
        var g = Ground(f, t);
        // The grabbed point is where the drag put it, at the contour's level; the line holds 2 m all along.
        Assert.Equal(2, g.ElevationAt(24, 20)!.Value, 9);
        var line = FeatureLines.Line(FeatureLines.SpecOf(e), FeatureLines.SpecOf(e).Corners.Select(_ => (double?)2).ToList());
        Assert.All(line, p => Assert.Equal(2, g.ElevationAt(p.X, p.Y)!.Value, 9));
        // Its ends stay on the old contour (x = 20) where the reach ends.
        Assert.Equal(20, line[0].X, 6);
        Assert.Equal(20, line[^1].X, 6);
        Assert.Equal(10, (line[0].Xy - from).Length, 6);
        // The survey points within the reach take the height from where the field carries them; beyond it, none moved.
        var after = Terrains.Points(f.Model.Get(t)!);
        var drag = to - from;
        for (var i = 0; i < before.Count; i++)
        {
            var d = (before[i].Xy - from).Length;
            var w = FeatureLines.Shepard(d, reach);
            var expected = d >= reach ? before[i].Z : 0.1 * (before[i].X - drag.X * w);
            Assert.Equal(expected, after[i].Z, 9);
            Assert.Equal(before[i].Xy, after[i].Xy);
        }
        // A reach shorter than twice the drag would fold the ground: refused.
        Assert.Throws<CommandException>(() => f.Run("civil.dragContour", new JsonObject { ["id"] = t, ["elevation"] = 1, ["from"] = P(10, 20), ["to"] = P(13, 20), ["reach"] = 5 }));
        // Shepard between the grab and the reach's edge: 1, ½ halfway, 0 at the reach.
        Assert.Equal(1, FeatureLines.Shepard(0, reach), 12);
        Assert.Equal(0.5, FeatureLines.Shepard(5, reach), 12);
        Assert.Equal(0, FeatureLines.Shepard(10, reach), 12);
        Assert.True(FeatureLines.Shepard(2, reach) > FeatureLines.Shepard(3, reach));
        // The ground before is the existing one: cut and fill measure the drag.
        Assert.NotNull(f.Model.Get(t)!.Attrs["existingPoints"]);
        var q = f.Session.Reader.Quantities(t).First(s => s.Name == "Lapin_TerrainQuantities");
        Assert.True(q.Quantities.First(x => x.Name == "CutVolume").Value + q.Quantities.First(x => x.Name == "FillVolume").Value > 0.1);
        // One undo step: the line, the points and the snapshot.
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
        Assert.Null(f.Model.Get(t)!.Attrs["existingPoints"]);
        Assert.Equal(before.Select(p => p.Z), Terrains.Points(f.Model.Get(t)!).Select(p => p.Z));
    }

    [Fact]
    public void A_contour_drawn_and_dragged_again_is_the_same_feature_line()
    {
        var f = F();
        var t = Field(f, (x, _) => 0.1 * x);
        var id = Line(f, new JsonObject { ["points"] = Pts([15, 4], [15, 36]), ["elevation"] = 1.5 });
        var near = f.Session.Reader;
        var r = f.Run("civil.dragContour", new JsonObject { ["id"] = t, ["elevation"] = 1.5, ["from"] = P(15, 20), ["to"] = P(17, 20), ["reach"] = 8 })!;
        Assert.Equal(id, (string)r["id"]!);
        Assert.Single(f.Model.OfClass(FeatureLines.Class));
        // Its straight run within the reach got corners every eighth of it, so it bends with the field: the grabbed place moved by the whole drag.
        Assert.True(FeatureLines.SpecOf(f.Model.Get(id)!).Corners.Count > 8);
        Assert.Contains(FeatureLines.SpecOf(f.Model.Get(id)!).Corners, c => (c - new Vec2(17, 20)).Length < 1e-6);
        // Its corners beyond the reach stayed; the ground at the grabbed point's new place is 1.5.
        var s = FeatureLines.SpecOf(f.Model.Get(id)!);
        Assert.Equal(new Vec2(15, 4), s.Corners[0]);
        Assert.Equal(1.5, Ground(f, t).ElevationAt(15, 4)!.Value, 9);
        Assert.NotNull(near);
    }

    [Fact]
    public void The_contour_near_a_point_is_found_for_a_tool_to_grab()
    {
        var f = F();
        var t = Field(f, (x, _) => 0.1 * x);
        var r = f.Session.Reader;
        var found = Query(f, "civil.contourNear", new JsonObject { ["x"] = 20.3, ["y"] = 11 })!;
        Assert.Equal(t, (string)found["id"]!);
        Assert.Equal(2, (double)found["elevation"]!, 9);
        Assert.Equal(0.3, (double)found["distance"]!, 6);
        Assert.Null(Query(f, "civil.contourNear", new JsonObject { ["x"] = 20.3, ["y"] = 11, ["within"] = 0.1 }));
        Assert.NotNull(r);
    }

    static JsonNode? Query(Fixture f, string name, JsonObject a) => f.Session.Query(name, a);

    [Fact]
    public void A_corner_off_every_terrain_without_a_height_is_refused_and_named()
    {
        var f = F();
        Field(f);
        // A feature line's corner past the terrain with no height of its own.
        var ex = Assert.Throws<CommandException>(() => f.Run("civil.addFeatureLine", new JsonObject { ["points"] = Pts([5, 5], [60, 5]) }));
        Assert.Contains("Vertex 2 (60.00, 5.00)", ex.Message);
        // With its height it is fine: the part over the terrain shapes it.
        var id = Line(f, new JsonObject { ["points"] = Pts([5, 5], [60, 5, 2]) });
        Assert.NotNull(f.Model.Get(id));
        // Î6: a terrain on an outline "on the ground" with a corner over no terrain is refused, the corner named (no longer put at 0).
        var ex2 = Assert.Throws<CommandException>(() => f.Run("civil.addTerrainOnOutline", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = Pts([10, 10], [30, 10], [70, 30], [10, 30]), ["ground"] = true,
        }));
        Assert.Contains("Vertex 3 (70.00, 30.00)", ex2.Message);
        // Over the terrain, or with the corner's own height, it is made.
        f.Run("civil.addTerrainOnOutline", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts([10, 10], [30, 10], [70, 30, 1], [10, 30]), ["ground"] = true });
    }

    [Fact]
    public void New_survey_points_drop_the_outline_flag_and_an_arc_vertex_moved_in_plan_is_an_ordinary_point()
    {
        var f = F();
        var (corners, rises) = ArcOutline.Circle(new Vec2(10, 10), 8);
        var t = (string)f.Run("civil.addTerrainOnOutline", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray([.. corners.Select(c => (JsonNode)P(c.X, c.Y, 1))]),
            ["arcs"] = new JsonArray([.. rises.Select(x => (JsonNode)x)]),
        })!["id"]!;
        var pts = Terrains.Points(f.Model.Get(t)!);
        // An arc's vertex (not a corner) moved in plan through the API: it keeps the height it is given, an ordinary point now.
        var arcVertex = Enumerable.Range(0, pts.Count).First(i => corners.All(c => (c - pts[i].Xy).Length > 1e-6) && Math.Abs((pts[i].Xy - new Vec2(10, 10)).Length - 8) < 1e-6);
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = t, ["index"] = arcVertex, ["x"] = pts[arcVertex].X * 0.9 + 1, ["y"] = pts[arcVertex].Y * 0.9 + 1, ["z"] = 4 });
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = t, ["index"] = 0, ["z"] = 3 });
        Assert.Equal(4, Terrains.Points(f.Model.Get(t)!)[arcVertex].Z, 12);
        // Replacing the survey wholesale: the outline is no longer its points.
        f.Run("civil.setTerrainPoints", new JsonObject { ["id"] = t, ["indices"] = new JsonArray(0), ["z"] = 2 });
        f.Run("civil.setTerrainControls", new JsonObject { ["id"] = t, ["indices"] = new JsonArray(0) });
        var attrs = f.Model.Get(t)!.Attrs;
        Assert.True(TerrainOutlines.Holds(f.Model.Get(t)!));
        var fresh = new JsonArray([.. Enumerable.Range(0, 5).SelectMany(i => Enumerable.Range(0, 5).Select(j => (JsonNode)P(i * 5, j * 5, 1)))]);
        f.Run("civil.updateTerrain", new JsonObject { ["id"] = t, ["points"] = fresh });
        var after = f.Model.Get(t)!;
        Assert.False(TerrainOutlines.Holds(after));
        Assert.Null(after.Attrs[TerrainOutlines.OwnKey]);
        Assert.NotNull(attrs);
    }

    [Fact]
    public void Crossing_feature_lines_meet_in_a_vertex_of_both_the_first_one_holding()
    {
        var f = F();
        var t = Field(f);
        Line(f, new JsonObject { ["points"] = Pts([5, 21, 2], [35, 21, 2]) });
        Line(f, new JsonObject { ["points"] = Pts([21, 5, 1], [21, 35, 1]) });
        var g = Ground(f, t);
        Assert.Contains(g.Tin.Points, p => (p.Xy - new Vec2(21, 21)).Length < 1e-9 && Math.Abs(p.Z - 2) < 1e-9);
        Assert.Equal(2, g.ElevationAt(25, 21)!.Value, 9);
        // Î7: the terrain names the crossing and both heights; each line's props carry it; the plan marks it with both heights.
        Assert.Contains(f.Session.Geometry(t)!.Diagnostics!, d => d.Contains("cross at (21.00, 21.00)") && d.Contains("2.000") && d.Contains("1.000"));
        var second = f.Model.OfClass(FeatureLines.Class).Last();
        var props = new JsonObject();
        new FeatureLines.Props().Add(second, f.Model, props);
        var clash = Assert.Single((JsonArray)props["clashes"]!)!;
        Assert.False((bool)clash["holds"]!);
        Assert.Equal(1, (double)clash["other"]!, 9);
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
        var texts = d["pens"]!.AsArray().OfType<JsonObject>().Where(p => p["texts"] != null)
            .SelectMany(p => p["texts"]!.AsArray().OfType<JsonObject>()).Select(x => (string)x["text"]!).ToList();
        Assert.Contains(texts, txt => txt.Contains("+2.00 / +1.00"));
    }

    [Fact]
    public void A_terrain_split_sends_each_feature_line_to_its_half_and_cuts_one_across_the_cut()
    {
        var f = F();
        var t = Field(f, (x, _) => 0.1 * x);
        var across = Line(f, new JsonObject { ["points"] = Pts([4, 10, 1], [30, 10, 3]) });
        var east = Line(f, new JsonObject { ["points"] = Pts([30, 30], [36, 34]), ["elevation"] = 3.3 });
        var west = Line(f, new JsonObject { ["points"] = Pts([4, 30, 0.6], [8, 34, 0.8]) });
        var ids = (JsonArray)f.Run("civil.splitTerrain", new JsonObject { ["id"] = t, ["x1"] = 24, ["y1"] = -5, ["x2"] = 24, ["y2"] = 45 })!["ids"]!;
        var (kept, other) = ((string)ids[0]!, (string)ids[1]!);
        Assert.Equal(kept, f.Model.Get(west)!.Str(FeatureLines.TerrainKey));
        Assert.Equal(other, f.Model.Get(east)!.Str(FeatureLines.TerrainKey));
        // The one across: its part on the kept half keeps its id, the other part is a line of its own; the cut at its height there.
        var a = FeatureLines.SpecOf(f.Model.Get(across)!);
        Assert.Equal(kept, f.Model.Get(across)!.Str(FeatureLines.TerrainKey));
        var parts = f.Model.OfClass(FeatureLines.Class).Where(x => x.Str(FeatureLines.TerrainKey) == other && x.Id != east).ToList();
        var b = FeatureLines.SpecOf(Assert.Single(parts));
        var cut = 1 + 2 * 20.0 / 26;
        Assert.Equal(new Vec2(4, 10), a.Corners[0]);
        Assert.Equal(24, a.Corners[^1].X, 9);
        Assert.Equal(cut, a.Z[^1]!.Value, 9);
        Assert.Equal(24, b.Corners[0].X, 9);
        Assert.Equal(cut, b.Z[0]!.Value, 9);
        Assert.Equal(3, b.Z[^1]!.Value, 9);
        // Each half is shaped by its own lines.
        Assert.Equal(3.3, Terrains.Of(f.Model.Get(other)!).ElevationAt(33, 32)!.Value, 9);
        Assert.Equal(1 + 2 * 23.0 / 26, Terrains.Of(f.Model.Get(other)!).ElevationAt(27, 10)!.Value, 9);
        Assert.Equal(1.5, Terrains.Of(f.Model.Get(kept)!).ElevationAt(10.5, 10)!.Value, 9);
        // Merged again: every line is the merged terrain's.
        f.Run("civil.mergeTerrains", new JsonObject { ["ids"] = new JsonArray(kept, other) });
        Assert.All(f.Model.OfClass(FeatureLines.Class), x => Assert.Equal(kept, x.Str(FeatureLines.TerrainKey)));
        Assert.Equal(3.3, Terrains.Of(f.Model.Get(kept)!).ElevationAt(33, 32)!.Value, 9);
    }

    [Fact]
    public void A_feature_line_moves_and_rises_with_the_generic_commands()
    {
        var f = F();
        var t = Field(f);
        var id = Line(f, new JsonObject { ["points"] = Pts([5, 5, 1], [15, 5, 1]) });
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = new JsonArray(10, 4) });
        var s = FeatureLines.SpecOf(f.Model.Get(id)!);
        Assert.Equal(new Vec2(15, 9), s.Corners[0]);
        Assert.Equal(1, Ground(f, t).ElevationAt(20, 9)!.Value, 9);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC4X3")]
    public void A_feature_line_goes_to_IFC_as_an_annotation_and_comes_back(string schema)
    {
        var f = F();
        var t = Field(f, (x, _) => 0.1 * x);
        var id = Line(f, new JsonObject { ["points"] = Pts([5, 5, 3], [20, 8], [30, 20, 1]), ["grades"] = new JsonArray(0.01, null) });
        var c = Line(f, new JsonObject { ["points"] = Pts([25.3, 22], [25.3, 36]), ["elevation"] = 2.5 });
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "grading.ifc", new IfcExportOptions(Schema: schema));
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("IFCANNOTATION(", text);
        Assert.Contains("'Curve3D'", text);
        Assert.Contains("LapinFeatureLine", text);
        if (schema == "IFC4X3") Assert.Contains(".CONTOURLINE.", text); else Assert.DoesNotContain(".CONTOURLINE.", text);
        // No set of Lapin's own takes IFC's prefix.
        Assert.DoesNotContain("'Pset_Lapin", text);
        var g = F();
        IfcImport.Run(g.Session, ms.ToArray(), "grading.ifc", null, out _);
        var back = g.Model.Get(id);
        Assert.NotNull(back);
        Assert.Equal(FeatureLines.Class, back!.Class);
        Assert.Equal(t, back.Str(FeatureLines.TerrainKey));
        Assert.Equal(FeatureLines.SpecOf(f.Model.Get(id)!).Corners, FeatureLines.SpecOf(back).Corners);
        Assert.Equal(0.01, FeatureLines.SpecOf(back).Grades[0]);
        Assert.Equal(2.5, FeatureLines.SpecOf(g.Model.Get(c)!).Elevation);
        // The terrain comes back shaped by them, the same TIN: the contour holds 2.5 m where the slope alone gives 2.53.
        Assert.NotNull(g.Model.Get(t));
        var tb = g.Model.Get(t)!;
        Assert.True(tb.Attrs[FeatureLines.Derived] != null, string.Join(",", tb.Attrs.Select(kv => kv.Key)));
        Assert.Equal(2.5, Terrains.Of(tb).ElevationAt(25.3, 30)!.Value, 9);
        Assert.Equal(Terrains.Of(f.Model.Get(t)!).Tin.Triangles, Terrains.Of(tb).Tin.Triangles);
    }
}

public sealed class FeatureLineFrameTests
{
    static JsonArray P(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);

    /// <summary>
    /// A terrain and its feature line on a storey at +1.20 with a datum of 100: the terrain's body, the ground's heights and the
    /// feature line's line in space all stand in the same frame, the model's heights (z − datum), whatever the storey's elevation:
    /// the 3D view draws them, and their grips, without adding the storey's.
    /// </summary>
    [Fact]
    public void On_a_storey_at_an_elevation_the_line_sits_on_the_terrain_s_own_surface()
    {
        var f = new Fixture(new ArchitectureModule(), new CivilModule());
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["elevation"] = 1.2 });
        var pts = new JsonArray();
        for (var i = 0; i <= 10; i++) for (var j = 0; j <= 10; j++) pts.Add(P(i * 3.0, j * 3.0, 100 + 0.05 * i * 3 + 0.02 * j * 3));
        var t = (string)f.Run("civil.addTerrain", new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts, ["datum"] = 100 })!["id"]!;
        var id = (string)f.Run("civil.addFeatureLine", new JsonObject { ["points"] = new JsonArray(P(4, 4), P(20, 9, 1.5), P(26, 25)) })!["id"]!;
        var ground = Terrains.Of(f.Model.Get(t)!);
        var body = f.Session.Geometry(t)!.Parts[0].Solid;
        // The body's top is the ground's height (z − datum), not raised by the storey's 1.20.
        Assert.Equal(0.05 * 30 + 0.02 * 30, body.Vertices.Max(v => v.Z), 6);
        var line = Assert.Single(f.Session.Geometry(id)!.Lines!);
        foreach (var p in line) Assert.Equal(ground.ElevationAt(p.X, p.Y)!.Value + 0.02, p.Z, 9);
        // The corners as the interface gets them (cornerZ) are those heights too.
        var cz = FeatureLines.CornerZ(FeatureLines.SpecOf(f.Model.Get(id)!), q => FeatureLines.GroundOf(f.Model, f.Model.Get(id)!)(q));
        Assert.Equal(1.5, cz[1]!.Value, 12);
        Assert.Equal(ground.ElevationAt(4, 4)!.Value, cz[0]!.Value, 9);
    }
}
