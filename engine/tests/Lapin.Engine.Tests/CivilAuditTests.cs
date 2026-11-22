using System.Text.Json.Nodes;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The audit of 22.11.2026 on the terrain (G6–G9 and §3.8): a platform's slopes reach as high as the ground round it and a
/// slope that would cross itself is refused; the maps read the designed ground (platforms flat, their slopes, a ditch's red
/// line) and the slope at the terrain's edge whole; the brush and the boundary triangulate a survey about once and a label
/// leaves the surface built; a LandXML surface is its file's triangles; a merge keeps a parcel; outlines that cross
/// themselves are refused by name; a subregion measures its part on the ground; a grid or contours too fine are drawn
/// coarser, said; a point off the terrain needs its height.
/// </summary>
public class CivilAuditTests
{
    static Fixture Site() => new(new CivilModule());

    /// <summary>A survey every 5 m over 100 × 100 m, at <paramref name="z"/>.</summary>
    static string Survey(Fixture f, Func<double, double, double> z, double size = 100, double step = 5)
    {
        var lines = new List<string>();
        for (var x = 0.0; x <= size + 1e-9; x += step) for (var y = 0.0; y <= size + 1e-9; y += step) lines.Add($"{x} {y} {z(x, y)}");
        return (string)f.Run("civil.addTerrain", new JsonObject { ["text"] = string.Join("\n", lines) })!["id"]!;
    }

    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);

    /// <summary>A U: 40 × 20 m with a notch 10 m wide and 10 m deep in its top edge (two inner corners 10 m apart).</summary>
    static JsonArray U() => Pts((10, 10), (50, 10), (50, 30), (35, 30), (35, 20), (25, 20), (25, 30), (10, 30));

    static JsonObject? Earthwork(Fixture f, string terrain, string pad) =>
        f.Session.Quantities(terrain).Where(q => q.Name == "Lapin_EarthworkQuantities" && q.Part == "pad:" + pad)
            .Select(q => new JsonObject { ["cut"] = q.Quantities.Single(x => x.Name == "CutVolume").Value, ["fill"] = q.Quantities.Single(x => x.Name == "FillVolume").Value })
            .SingleOrDefault();

    [Fact]
    public void A_platforms_cut_slope_reaches_only_as_high_as_the_ground_round_it_so_a_concave_one_levels()
    {
        var f = Site();
        // Level ground, and a hill 20 m high 50 m off: the whole terrain's top would carry the slope 44 m round the U.
        var t = Survey(f, (x, y) => x >= 85 && y >= 85 ? 20 : 0);
        var pad = (string)f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["points"] = U(), ["elevation"] = -1.0 })!["id"]!;
        f.Run("civil.updatePlatform", new JsonObject { ["id"] = pad, ["cutSlope"] = 2 });
        var g = f.Session.Geometry(t)!;
        Assert.DoesNotContain(g.Diagnostics ?? [], d => d.Contains("Platform"));
        // 700 m² a metre deep, and the slope 1:2 up to the ground: a wedge of 1 m² along its 140 m (and its corners).
        var w = Earthwork(f, t, pad)!;
        Assert.InRange((double)w["cut"]!, 820, 880);
        Assert.Equal(0, (double)w["fill"]!, 3);
        // The designed ground: the platform's level, its slope half a metre up 1 m out, the ground beyond where it meets it.
        var e = f.Model.Get(t)!;
        var ground = Terrains.Of(e);
        Assert.Equal(-1, Lapin.Civil.Analysis.Proposed(f.Model, e, ground, new Vec2(30, 15))!.Value, 9);
        Assert.Equal(-0.5, Lapin.Civil.Analysis.Proposed(f.Model, e, ground, new Vec2(30, 9))!.Value, 6);
        Assert.Equal(0, Lapin.Civil.Analysis.Proposed(f.Model, e, ground, new Vec2(30, 7))!.Value, 9);
    }

    [Fact]
    public void A_platforms_slope_that_would_cross_itself_is_refused_with_its_reason_and_not_measured()
    {
        var f = Site();
        var t = Survey(f, (_, _) => 0);
        var pad = (string)f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["points"] = U(), ["elevation"] = -1.0 })!["id"]!;
        // 1:4 from a metre down to a metre over the ground: 8 m out, past the notch's 10 m between its inner corners (5 m each side).
        f.Run("civil.updatePlatform", new JsonObject { ["id"] = pad, ["cutSlope"] = 4 });
        var g = f.Session.Geometry(t)!;
        Assert.Contains(g.Diagnostics ?? [], d => d.StartsWith("Platform 1: its cut slope (1:4) runs 8 m out") && d.Contains("would cross itself"));
        Assert.Null(Earthwork(f, t, pad));
        // The ground is left as it is there, in the maps too.
        var e = f.Model.Get(t)!;
        Assert.Equal(0, Lapin.Civil.Analysis.Proposed(f.Model, e, Terrains.Of(e), new Vec2(30, 15))!.Value, 9);
        // A steeper slope (1:1, 2 m out) levels it.
        f.Run("civil.updatePlatform", new JsonObject { ["id"] = pad, ["cutSlope"] = 1 });
        Assert.NotNull(Earthwork(f, t, pad));
    }

    [Fact]
    public void The_slope_map_reads_a_platform_flat_and_the_terrains_edge_at_its_whole_slope()
    {
        var f = Site();
        var t = Survey(f, (x, _) => 0.08 * x, 40);
        f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["x1"] = 10, ["y1"] = 10, ["x2"] = 30, ["y2"] = 20, ["elevation"] = 1.6 });
        var r = Lapin.Civil.Analysis.Of(f.Model, f.Model.Get(t)!, "slope")!;
        var h = (r.Cells[0].Max.X - r.Cells[0].Min.X) / 2;
        double Off(Vec2 c) => Math.Max(Math.Max(10 - c.X, c.X - 30), Math.Max(10 - c.Y, c.Y - 20));
        var (inside, outside) = (0, 0);
        foreach (var cell in r.Cells)
        {
            var c = (cell.Min + cell.Max) * 0.5;
            if (Off(c) < -h - 1e-6) { Assert.Equal(0, cell.Value, 6); inside++; }
            else if (Off(c) > h + 1e-6) { Assert.Equal(8, cell.Value, 6); outside++; }
        }
        Assert.True(inside > 100 && outside > 1000, $"{inside} inside, {outside} outside");
    }

    [Fact]
    public void The_designed_ground_under_a_ditch_with_a_designed_profile_is_its_red_line()
    {
        var f = Site();
        var t = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 40, ["y2"] = 40 })!["id"]!;
        var ditch = (string)f.Run("civil.addDitch", new JsonObject { ["points"] = Pts((5, 20), (35, 20)), ["depth"] = 0.5, ["bottomWidth"] = 0.4, ["slope"] = 1 })!["id"]!;
        f.Run("civil.setCorridor", new JsonObject { ["id"] = ditch, ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(new JsonArray(0, -1.0), new JsonArray(30, -2.0)) } });
        var e = f.Model.Get(t)!;
        var ground = Terrains.Of(e);
        // Station 15: the red line at −1.5; a metre off the axis, 0.8 m up its 1:1 slope from the bottom's edge.
        Assert.Equal(-1.5, Lapin.Civil.Analysis.Proposed(f.Model, e, ground, new Vec2(20, 20))!.Value, 6);
        Assert.Equal(-0.7, Lapin.Civil.Analysis.Proposed(f.Model, e, ground, new Vec2(20, 21))!.Value, 6);
        Assert.Equal(0, Lapin.Civil.Analysis.Proposed(f.Model, e, ground, new Vec2(20, 23))!.Value, 9);
        // Past its end the ground is as it was.
        Assert.Equal(0, Lapin.Civil.Analysis.Proposed(f.Model, e, ground, new Vec2(37, 20))!.Value, 9);
    }

    [Fact]
    public void A_spot_level_or_a_slope_tag_leaves_the_terrains_surface_as_it_was_built()
    {
        var f = Site();
        var t = Survey(f, (x, y) => 0.1 * x + 0.05 * y, 40);
        var before = Terrains.Of(f.Model.Get(t)!);
        f.Run("civil.addSpot", new JsonObject { ["terrainId"] = t, ["x"] = 12, ["y"] = 7 });
        f.Run("civil.addSlope", new JsonObject { ["terrainId"] = t, ["x1"] = 5, ["y1"] = 5, ["x2"] = 25, ["y2"] = 5 });
        Assert.Same(before, Terrains.Of(f.Model.Get(t)!));
        // A point moved is another surface.
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = t, ["index"] = 10, ["dz"] = 1.0 });
        Assert.NotSame(before, Terrains.Of(f.Model.Get(t)!));
    }

    /// <summary>Scattered points (no four on a circle, so the triangulation is one), heights rolling.</summary>
    static List<Vec3> Scatter(int n, double size, int seed)
    {
        var rnd = new Random(seed);
        return Enumerable.Range(0, n).Select(_ => { var (x, y) = (rnd.NextDouble() * size, rnd.NextDouble() * size); return new Vec3(x, y, Math.Sin(x / 7) * 2 + Math.Cos(y / 5)); }).ToList();
    }

    [Fact]
    public void The_brush_reads_the_ground_as_the_whole_triangulation_has_it()
    {
        var pts = Scatter(600, 60, 3);
        SculptDab[] dabs =
        [
            new("raise", new Vec2(20, 20), 5, 1.2, 0, 0.5), new("smooth", new Vec2(22, 19), 6, 0, 0, 0.8), new("lower", new Vec2(40, 35), 4, 0.7, 0, 0.5),
            new("flatten", new Vec2(30, 30), 7, 0, 1.5, 0.5), new("smooth", new Vec2(58, 58), 5, 0, 0, 1), new("raise", new Vec2(1, 30), 8, 2, 0, 0.5),
        ];
        var got = Sculpt.Apply([.. pts], [], dabs);
        var want = WholeStrokes([.. pts], dabs);
        Assert.Equal(want.Count, got.Count);
        for (var i = 0; i < want.Count; i++)
        {
            Assert.Equal(want[i].X, got[i].X, 12);
            Assert.Equal(want[i].Y, got[i].Y, 12);
            Assert.Equal(want[i].Z, got[i].Z, 9);
        }
    }

    /// <summary>The brush as it was before the audit: a whole triangulation for each stroke (the reference).</summary>
    static List<Vec3> WholeStrokes(List<Vec3> points, IEnumerable<SculptDab> dabs)
    {
        foreach (var d in dabs)
        {
            var before = new Terrain(Tin.Delaunay(points));
            var step = Math.Max(d.Radius / 4, 0.25);
            double W(Vec2 p) { var t = 1 - (p - d.At).Length / d.Radius; return t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t); }
            double Around(Vec2 p)
            {
                var sum = 0.0; var n = 0;
                foreach (var (dx, dy) in new[] { (step, 0.0), (-step, 0.0), (0.0, step), (0.0, -step) })
                    if (before.ElevationAt(p.X + dx, p.Y + dy) is { } z) { sum += z; n++; }
                return n == 0 ? before.ElevationAt(p.X, p.Y) ?? 0 : sum / n;
            }
            double Target(Vec2 p, double z0, double w) => d.Kind switch
            {
                "raise" => z0 + d.Amount * w, "lower" => z0 - d.Amount * w, "flatten" => z0 + (d.Level - z0) * w,
                _ => z0 + (Around(p) - z0) * w * d.Strength,
            };
            (long, long) Key(double x, double y) => ((long)Math.Round(x / (step * 0.45)), (long)Math.Round(y / (step * 0.45)));
            var o = new List<Vec3>();
            var taken = new HashSet<(long, long)>();
            foreach (var p in points) { var w = W(p.Xy); o.Add(w > 0 ? p with { Z = Target(p.Xy, p.Z, w) } : p); taken.Add(Key(p.X, p.Y)); }
            for (var x = d.At.X - d.Radius; x <= d.At.X + d.Radius + 1e-9; x += step)
                for (var y = d.At.Y - d.Radius; y <= d.At.Y + d.Radius + 1e-9; y += step)
                {
                    var p = new Vec2(x, y);
                    var w = W(p);
                    if (w <= 1e-6 || !taken.Add(Key(x, y)) || before.ElevationAt(x, y) is not { } z0) continue;
                    o.Add(new Vec3(x, y, Target(p, z0, w)));
                }
            points = o;
        }
        return points;
    }

    [Fact]
    public void Brush_strokes_and_a_boundary_on_a_large_survey_triangulate_it_about_once()
    {
        var f = Site();
        var survey = Scatter(12000, 120, 11);
        var t = (string)f.Run("civil.addTerrain", new JsonObject { ["points"] = new JsonArray([.. survey.Select(p => (JsonNode)new JsonArray(p.X, p.Y, p.Z))]) })!["id"]!;
        var rnd = new Random(5);
        var dabs = new JsonArray();
        for (var k = 0; k < 30; k++)
            dabs.Add((JsonNode)new SculptDab(k % 2 == 0 ? "raise" : "smooth", new Vec2(10 + rnd.NextDouble() * 100, 10 + rnd.NextDouble() * 100), 3 + rnd.NextDouble() * 3, 0.5, 0, 0.5).ToJson());
        var attrs = f.Model.Get(t)!.CloneAttrs();
        var start = Tin.Inserted;
        // The strokes and a boundary given at once, as a project opened with them: one build of the surface.
        var e = f.Model.Get(t)!.With(attrs: new JsonObject(attrs.Select(kv => KeyValuePair.Create(kv.Key, kv.Value?.DeepClone())))
        {
            ["sculpts"] = dabs, [Terrains.BoundaryKey] = Terrains.ContourJson([new(2, 2), new(118, 2), new(118, 60), new(60, 60), new(60, 118), new(2, 118)]),
        });
        var built = Terrains.Of(e);
        var work = Tin.Inserted - start;
        Assert.True(built.Tin.Triangles.Count > 10000);
        Assert.Null(built.ElevationAt(90, 90));
        // Each point of the result inserted about once (the strokes' local triangulations a few thousand more), not once per stroke.
        Assert.True(work < 4 * built.Tin.Points.Count, $"{work} insertions for {built.Tin.Points.Count} points");
    }

    /// <summary>A LandXML surface over a 4 × 4 grid of points every 10 m (ids 1–16, northing first) with the faces given, and a point on none.</summary>
    static string Xml(IEnumerable<string> faces)
    {
        var pts = string.Join("", from i in Enumerable.Range(0, 4) from j in Enumerable.Range(0, 4) select $"<P id=\"{i * 4 + j + 1}\">{j * 10} {i * 10} {i + j}</P>");
        return $"<LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\"><Surfaces><Surface name=\"EG\"><Definition surfType=\"TIN\"><Pnts>{pts}<P id=\"99\">100 100 0</P></Pnts><Faces>{string.Concat(faces)}</Faces></Definition></Surface></Surfaces></LandXML>";
    }

    /// <summary>The two triangles of the grid's cell (i, j), x from i · 10, y from j · 10.</summary>
    static IEnumerable<string> Cell(int i, int j, string attr = "")
    {
        int Id(int a, int b) => a * 4 + b + 1;
        yield return $"<F{attr}>{Id(i, j)} {Id(i + 1, j)} {Id(i + 1, j + 1)}</F>";
        yield return $"<F{attr}>{Id(i, j)} {Id(i + 1, j + 1)} {Id(i, j + 1)}</F>";
    }

    [Fact]
    public void A_LandXML_surface_is_its_files_triangles_with_its_hole_and_concave_edge()
    {
        // Every cell but the middle one (a hole) and the far corner (a notch); the hole's own faces marked invisible.
        var cells = from i in Enumerable.Range(0, 3) from j in Enumerable.Range(0, 3) where !(i == 1 && j == 1) && !(i == 2 && j == 2) select (i, j);
        var xml = Xml(cells.SelectMany(c => Cell(c.i, c.j)).Concat(Cell(1, 1, " i=\"1\"")));
        var s = LandXml.Surfaces(xml).Single();
        Assert.Equal(14, s.Triangles.Count);
        Assert.Equal(15, s.Points.Count);   // the points on no face (the notch's far corner, id 99) are not on the surface
        var f = Site();
        var id = (string)f.Run("civil.importLandXml", new JsonObject { ["text"] = xml })!["ids"]![0]!;
        var ground = Terrains.Of(f.Model.Get(id)!);
        Assert.Equal(14, ground.Tin.Triangles.Count);
        Assert.Null(ground.ElevationAt(15, 15));   // the hole
        Assert.Null(ground.ElevationAt(25, 25));   // the notch
        Assert.Equal(1, ground.ElevationAt(5, 5)!.Value, 9);
        // A point taken away (the first, at the origin) takes its triangles; the rest stay the file's.
        f.Run("civil.removeTerrainPoint", new JsonObject { ["id"] = id, ["index"] = 0 });
        ground = Terrains.Of(f.Model.Get(id)!);
        Assert.Equal(12, ground.Tin.Triangles.Count);
        Assert.Null(ground.ElevationAt(15, 15));
        // A stroke of the brush keeps to where the file's triangles are.
        f.Run("civil.sculpt", new JsonObject { ["terrainId"] = id, ["kind"] = "raise", ["x"] = 15, ["y"] = 5, ["radius"] = 3, ["amount"] = 1 });
        ground = Terrains.Of(f.Model.Get(id)!);
        Assert.Null(ground.ElevationAt(15, 15));
        Assert.Null(ground.ElevationAt(25, 25));
        Assert.True(ground.ElevationAt(15, 5) > 1.5);
    }

    [Fact]
    public void Merging_terrains_keeps_the_one_parcel_there_is_and_says_which_it_kept_of_several()
    {
        var f = Site();
        var a = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 20, ["y2"] = 20 })!["id"]!;
        var b = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = 20, ["y1"] = 0, ["x2"] = 40, ["y2"] = 20 })!["id"]!;
        f.Run("civil.setParcel", new JsonObject { ["terrainId"] = b, ["points"] = Pts((22, 2), (38, 2), (38, 18), (22, 18)) });
        var merged = (JsonObject)f.Run("civil.mergeTerrains", new JsonObject { ["ids"] = new JsonArray(a, b) })!;
        Assert.Null(merged["problems"]);
        Assert.Equal(256, ArcOutline.AreaOf(f.Model.Get(a)!, "parcel", Terrains.ParcelArcs), 6);
        // Two parcels: the first terrain's kept, and said.
        var c = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = 40, ["y1"] = 0, ["x2"] = 60, ["y2"] = 20 })!["id"]!;
        f.Run("civil.setParcel", new JsonObject { ["terrainId"] = c, ["points"] = Pts((42, 2), (48, 2), (48, 8), (42, 8)) });
        merged = (JsonObject)f.Run("civil.mergeTerrains", new JsonObject { ["ids"] = new JsonArray(a, c) })!;
        Assert.Contains(merged["problems"]!.AsArray(), p => ((string)p!).Contains("2 parcels"));
        Assert.Equal(256, ArcOutline.AreaOf(f.Model.Get(a)!, "parcel", Terrains.ParcelArcs), 6);
    }

    [Fact]
    public void An_outline_that_crosses_itself_with_straight_edges_is_refused_naming_what_it_is()
    {
        var f = Site();
        var t = Survey(f, (_, _) => 0, 40);
        // A bow: its second and fourth edges cross at (5, 7.5).
        var bow = Pts((0, 0), (20, 0), (0, 10), (10, 15));
        void Refused(string command, JsonObject a, string what)
        {
            var ex = Assert.Throws<CommandException>(() => f.Run(command, a));
            Assert.Contains($"outline of {what} crosses itself", ex.Message);
        }
        Refused("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["points"] = bow.DeepClone() }, "a platform");
        Refused("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["points"] = bow.DeepClone(), ["arcs"] = new JsonArray(0, 0, 0, 0) }, "a platform");
        Refused("civil.setBoundary", new JsonObject { ["id"] = t, ["points"] = bow.DeepClone() }, "a terrain boundary");
        Refused("civil.setParcel", new JsonObject { ["terrainId"] = t, ["points"] = bow.DeepClone() }, "a parcel");
        Refused("civil.addSubregion", new JsonObject { ["terrainId"] = t, ["points"] = bow.DeepClone() }, "a subregion");
        Refused("civil.addPlanting", new JsonObject { ["points"] = bow.DeepClone() }, "a planting zone");
        // Its corners in another order make a plain outline.
        f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["points"] = Pts((0, 0), (20, 0), (10, 15), (0, 10)) });
    }

    [Fact]
    public void A_subregions_area_is_the_part_of_it_on_the_terrain()
    {
        var f = Site();
        var t = Survey(f, (x, _) => 0.1 * x, 40);
        f.Run("civil.addSubregion", new JsonObject { ["terrainId"] = t, ["points"] = Pts((30, 30), (50, 30), (50, 50), (30, 50)), ["name"] = "Lawn" });
        var q = f.Session.Quantities(t).Single(x => x.Name == "Lapin_SubregionQuantities");
        Assert.Equal(100, q.Quantities.Single(x => x.Name == "PlanArea").Value, 6);
    }

    static List<string> PlanTexts(Fixture f)
    {
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        return [.. d["pens"]!.AsArray().OfType<JsonObject>().Where(p => p["texts"] != null).SelectMany(p => p["texts"]!.AsArray().OfType<JsonObject>()).Select(x => (string)x["text"]!)];
    }

    [Fact]
    public void A_cut_fill_grid_too_fine_to_write_is_written_coarser_and_the_terrain_says_so()
    {
        var f = Site();
        var t = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 200, ["y2"] = 200 })!["id"]!;
        f.Run("civil.updateTerrain", new JsonObject { ["id"] = t, ["cutFillGrid"] = 1 });
        Assert.Contains(f.Session.Geometry(t)!.Diagnostics ?? [], d => d.Contains("cut/fill grid every 1 m") && d.Contains("written every 5 m"));
        var values = PlanTexts(f).Count(x => x == "+0.00");
        Assert.Equal(41 * 41, values);
    }

    [Fact]
    public void Contours_too_many_to_draw_are_drawn_at_a_coarser_interval_and_the_terrain_says_so()
    {
        var f = Site();
        var t = (string)f.Run("civil.addTerrain", new JsonObject { ["text"] = "0 0 0\n100 0 500\n0 100 0", ["interval"] = 0.05 })!["id"]!;
        var g = f.Session.Geometry(t)!;
        Assert.Contains(g.Diagnostics ?? [], d => d.Contains("Contours every 0.05 m") && d.Contains("drawn every 0.25 m"));
        // Every 0.25 m from 0 to 500 (the lowest and highest only touch the ground at a corner).
        Assert.InRange(g.Plan!.Count(p => p.Kind.StartsWith("contour")), 1995, 2001);
    }

    [Fact]
    public void A_point_added_off_the_terrain_without_its_height_is_refused()
    {
        var f = Site();
        var t = Survey(f, (x, _) => 0.1 * x, 40);
        var ex = Assert.Throws<CommandException>(() => f.Run("civil.addTerrainPoint", new JsonObject { ["terrainId"] = t, ["x"] = 60, ["y"] = 10 }));
        Assert.Contains("outside the terrain", ex.Message);
        // With its height it goes in; on the ground it takes the ground's.
        f.Run("civil.addTerrainPoint", new JsonObject { ["terrainId"] = t, ["x"] = 60, ["y"] = 10, ["z"] = 6.0 });
        var r = (JsonObject)f.Run("civil.addTerrainPoint", new JsonObject { ["terrainId"] = t, ["x"] = 12.5, ["y"] = 12.5 })!;
        Assert.Equal(1.25, Terrains.Points(f.Model.Get(t)!)[(int)r["index"]!].Z, 9);
    }
}
