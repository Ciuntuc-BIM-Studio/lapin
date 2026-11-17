using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Civil;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The terrain: ported from the old Lapin's tin.test.ts (same cases and numbers), then on a model: its body, contours, platforms and their earthworks.</summary>
public class CivilTests
{
    static List<Vec3> Grid(int n, Func<double, double, double> z)
    {
        var pts = new List<Vec3>();
        for (var i = 0; i < n; i++) for (var j = 0; j < n; j++) pts.Add(new Vec3(i * 10, j * 10, z(i * 10, j * 10)));
        return pts;
    }

    [Fact]
    public void Delaunay_over_a_grid_2_n_1_squared_counter_clockwise_triangles_every_one_empty_of_other_points()
    {
        var tin = Tin.Delaunay(Grid(5, (_, _) => 0));
        Assert.Equal(32, tin.Triangles.Count);
        foreach (var (i, j, k) in tin.Triangles)
        {
            var (a, b, c) = (tin.Points[i], tin.Points[j], tin.Points[k]);
            Assert.True((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X) > 0);
        }
        // Random scatter: no point inside another triangle's circumcircle.
        long seed = 7;
        double Rnd() => (seed = seed * 16807 % 2147483647) / 2147483647.0;
        var scatter = Enumerable.Range(0, 80).Select(_ => { var x = Rnd(); var y = Rnd(); return new Vec3(x * 100, y * 100, Rnd() * 5); }).ToList();
        var t2 = Tin.Delaunay(scatter);
        Assert.True(t2.Triangles.Count > 100);
        foreach (var (i, j, k) in t2.Triangles)
        {
            var (a, b, c) = (t2.Points[i], t2.Points[j], t2.Points[k]);
            var d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
            double a2 = a.X * a.X + a.Y * a.Y, b2 = b.X * b.X + b.Y * b.Y, c2 = c.X * c.X + c.Y * c.Y;
            var x = (a2 * (b.Y - c.Y) + b2 * (c.Y - a.Y) + c2 * (a.Y - b.Y)) / d;
            var y = (a2 * (c.X - b.X) + b2 * (a.X - c.X) + c2 * (b.X - a.X)) / d;
            var r2 = (a.X - x) * (a.X - x) + (a.Y - y) * (a.Y - y);
            foreach (var p in t2.Points) Assert.True((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) > r2 * (1 - 1e-9) - 1e-6);
        }
    }

    [Fact]
    public void A_constraint_edge_is_kept_exactly_split_where_a_point_lies_on_it_and_the_rest_stays_Delaunay()
    {
        // Two points close either side of y = 5: plain Delaunay joins them across the line from (0, 5) to (10, 5).
        Vec3[] pts = [new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0), new(0, 5, 0), new(10, 5, 0), new(5, 4.9, 0), new(5, 5.1, 0)];
        bool Has(Tin t, int a, int b) => t.Triangles.Any(x => new[] { (x.A, x.B), (x.B, x.C), (x.C, x.A) }.Any(e => (e.Item1 == a && e.Item2 == b) || (e.Item1 == b && e.Item2 == a)));
        double Area(Tin t) => t.Triangles.Sum(x => ((t.Points[x.B].X - t.Points[x.A].X) * (t.Points[x.C].Y - t.Points[x.A].Y) - (t.Points[x.B].Y - t.Points[x.A].Y) * (t.Points[x.C].X - t.Points[x.A].X)) / 2);
        Assert.False(Has(Tin.Delaunay(pts), 4, 5));
        var c = Tin.Delaunay(pts, [(4, 5)]);
        Assert.True(Has(c, 4, 5));
        Assert.Equal(100, Area(c), 9);
        Assert.All(c.Triangles, x => Assert.True((c.Points[x.B].X - c.Points[x.A].X) * (c.Points[x.C].Y - c.Points[x.A].Y) - (c.Points[x.B].Y - c.Points[x.A].Y) * (c.Points[x.C].X - c.Points[x.A].X) > 0));
        Assert.Equal(2 * 8 - 2 - 6, c.Triangles.Count);   // 2n − 2 − hull points, (0, 5) and (10, 5) on it
        // A point on the constraint splits it: both halves are edges.
        Vec3[] on = [.. pts[..6], new(5, 5, 0), new(3, 6, 0)];
        var d = Tin.Delaunay(on, [(4, 5)]);
        Assert.True(Has(d, 4, 6) && Has(d, 6, 5));
    }

    [Fact]
    public void Interpolates_a_planar_surface_exactly_and_meets_a_slope_line_where_it_should()
    {
        var terrain = new Terrain(Tin.Delaunay(Grid(6, (x, y) => 100 + 0.1 * x + 0.02 * y)));
        Assert.Equal(100 + 1.7 + 0.46, terrain.ElevationAt(17, 23)!.Value, 9);
        Assert.Null(terrain.ElevationAt(-1, 0));
        // From 3 m above the ground at (10, 10), down a 1 : 2 slope along +x: the ground rises 0.1 a metre, the line falls 0.5: they meet after 5 m.
        var hit = terrain.Hit(new Vec3(10, 10, 100 + 1 + 0.2 + 3), new Vec3(2, 0, -1), 50);
        Assert.NotNull(hit);
        Assert.Equal(15, hit!.Value.X, 3);
        Assert.Equal(terrain.ElevationAt(hit.Value.X, hit.Value.Y)!.Value, hit.Value.Z, 3);
        Assert.Null(terrain.Hit(new Vec3(10, 10, 200), new Vec3(1, 0, 0), 20));
    }

    [Fact]
    public void Reads_survey_points_in_xyz_and_PENZD_forms_and_TINs_from_meshes()
    {
        Assert.Equal([new Vec3(1, 2, 3), new Vec3(4, 5, 6)], Tin.ParseSurveyPoints("# header\n1,2,3\n4 5 6\n"));
        Assert.Equal([new Vec3(500, 4500, 120.5)], Tin.ParseSurveyPoints("7,4500.0,500.0,120.5,TREE"));
        // Named points, as a total station exports them; a header line stays out.
        Assert.Equal([new Vec3(634210.55, 512345.20, 245.37), new Vec3(634215, 512350, 245.9)],
            Tin.ParseSurveyPoints("Name,N,E,Z,Code\nP1,512345.20,634210.55,245.37,CORNER\nST12;512350,00;634215,00;245,90;STATION\n"));
        var tin = Tin.FromMesh([0, 0, 0, 10, 0, 0, 0, 10, 0, 10, 0, 0, 0, 10, 0, 10, 10, 1], [0, 1, 2, 3, 4, 5]);
        Assert.Equal(4, tin.Points.Count);
        Assert.Equal(2, tin.Triangles.Count);
    }

    // ------------------------------------------------------------ on a model

    static Fixture F() => new(new Lapin.Architecture.ArchitectureModule(), new CivilModule());

    /// <summary>A 40 × 40 m slope: z = 100 + 0.05 x (surveyed), the datum at 100.</summary>
    static string Slope(Fixture f)
    {
        var lines = new List<string>();
        for (var i = 0; i <= 4; i++) for (var j = 0; j <= 4; j++) lines.Add($"{i * 10} {j * 10} {100 + 0.05 * i * 10}");
        return (string)f.Run("civil.addTerrain", new JsonObject { ["text"] = string.Join("\n", lines), ["datum"] = 100, ["interval"] = 0.5, ["majorEvery"] = 2, ["depth"] = 1 })!["id"]!;
    }

    /// <summary>Report #43: "I can't move the ground from one level to another, even though I changed its placement".</summary>
    [Fact]
    public void A_terrain_moved_to_another_storey_rises_or_falls_with_the_level()
    {
        var f = F();
        var t = (string)f.Run("civil.addTerrain", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(10, 0, 0), new JsonArray(0, 10, 0), new JsonArray(10, 10, 0)) })!["id"]!;
        var top = f.Bounds(t).Max.Z;
        var cts = (string)f.Run("storey.create", new JsonObject { ["name"] = "CTS", ["elevation"] = Spatial.Storeys(f.Model).First(s => s.Id == f.Storey).Num("elevation") - 0.5 })!["id"]!;
        f.Run("element.moveToStorey", new JsonObject { ["ids"] = new JsonArray(t), ["storeyId"] = cts });
        Assert.Equal(top - 0.5, f.Bounds(t).Max.Z, 6);
        // Kept where it is, it stays.
        f.Run("element.moveToStorey", new JsonObject { ["ids"] = new JsonArray(t), ["storeyId"] = f.Storey, ["keep"] = "world" });
        Assert.Equal(top - 0.5, f.Bounds(t).Max.Z, 6);
    }

    [Fact]
    public void A_terrain_is_the_ground_as_a_solid_down_to_its_base_with_its_contours_in_plan()
    {
        var f = F();
        var t = Slope(f);
        var g = f.Session.Geometry(t)!;
        // Over 40 × 40, the ground from 0 to 2 m (datum 100), its base 1 m below the lowest point: 40·40·(1 + 1) on average.
        Assert.Equal(40 * 40 * 2.0, Measure.Volume(g.Parts[0].Solid), 6);
        Assert.Equal(-1, Measure.Bounds(g.Parts[0].Solid)!.Value.Min.Z, 9);
        // Contours every 0.5 m from 0 to 2: five levels, each a line across (x = 0, 10, 20, 30, 40), the major ones (0, 1, 2) heavier.
        var plan = g.Plan!;
        // The lowest one lies on the terrain's edge, drawn as its edge.
        Assert.Equal([20.0, 40.0], plan.Where(p => p.Kind == "contourMajor").SelectMany(p => p.Points.Select(x => Math.Round(x.X, 6))).Distinct().Order());
        Assert.Equal([10.0, 30.0], plan.Where(p => p.Kind == "contour").SelectMany(p => p.Points.Select(x => Math.Round(x.X, 6))).Distinct().Order());
        var q = f.Session.Quantities(t)[0];
        Assert.Equal(1600, q.Quantities.Single(x => x.Name == "PlanArea").Value, 6);
        Assert.Equal(2, q.Quantities.Single(x => x.Name == "MaxElevation").Value, 9);
        Assert.Equal(1.5, (double)f.Session.Query("civil.elevationAt", new JsonObject { ["x"] = 30, ["y"] = 5 })!, 9);
        // The earth is a material, hatched as earth in sections.
        Assert.Equal("Earth", f.Model.Get(f.Model.Get(t)!.Str("material")!)!.Name);
        Assert.Throws<CommandException>(() => f.Run("civil.addTerrain", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(1, 1, 0), new JsonArray(2, 2, 0)) }));
    }

    [Fact]
    public void A_platform_levels_the_ground_cutting_what_is_above_and_filling_what_is_below()
    {
        var f = F();
        var t = Slope(f);
        var before = Measure.Volume(f.Session.Geometry(t)!.Parts[0].Solid);
        // 20 × 10 from x = 10 to 30, where the ground goes from 0.5 to 1.5: levelled at 1.0, half is cut, half filled.
        var pad = (string)f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["x1"] = 10, ["y1"] = 10, ["x2"] = 30, ["y2"] = 20, ["elevation"] = 1.0 })!["id"]!;
        var g = f.Session.Geometry(t)!;
        var works = f.Session.Quantities(t).Single(q => q.Name == "Lapin_EarthworkQuantities");
        var (cut, fill) = (works.Quantities.Single(x => x.Name == "CutVolume").Value, works.Quantities.Single(x => x.Name == "FillVolume").Value);
        // The ground above 1.0 over x 20–30 is a wedge: ½ · 10 · 0.5 · 10 = 25 m³; the fill below is the same.
        Assert.Equal(25, cut, 4);
        Assert.Equal(25, fill, 4);
        Assert.Equal(before, Measure.Volume(g.Parts[0].Solid), 4);
        // In plan: the platform's outline, and no contour inside it.
        Assert.Contains(g.Plan!, p => p.Kind == "heavy" && p.Closed);
        Assert.DoesNotContain(g.Plan!, p => p.Kind.StartsWith("contour") && p.Points.All(x => x.X > 10.01 && x.X < 29.99 && x.Y > 10.01 && x.Y < 19.99));
        // Raised to 2.0: all fill (10 · 20 · (2 − 1) on average = 200 m³).
        f.Run("civil.updatePlatform", new JsonObject { ["id"] = pad, ["elevation"] = 2.0 });
        works = f.Session.Quantities(t).Single(q => q.Name == "Lapin_EarthworkQuantities");
        Assert.Equal(0, works.Quantities.Single(x => x.Name == "CutVolume").Value, 4);
        Assert.Equal(200, works.Quantities.Single(x => x.Name == "FillVolume").Value, 4);
        // The terrain deleted takes its platforms; undo brings both back.
        f.Run("element.delete", new JsonObject { ["id"] = t });
        Assert.Null(f.Model.Get(pad));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(pad));
    }

    [Fact]
    public void A_plan_draws_the_contours_and_labels_the_major_ones_and_a_section_cuts_the_earth()
    {
        var f = F();
        var t = Slope(f);
        f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["x1"] = 10, ["y1"] = 10, ["x2"] = 30, ["y2"] = 20, ["elevation"] = 1.0 });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        var pens = d["pens"]!.AsArray().OfType<JsonObject>().ToList();
        var texts = pens.Where(p => p["texts"] != null).SelectMany(p => p["texts"]!.AsArray().OfType<JsonObject>()).Select(x => (string)x["text"]!).ToList();
        Assert.Contains("+1.00", texts);
        Assert.Contains("+2.00", texts);
        Assert.Contains("Platform 1 +1.00", texts);
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-5, 15), ["end"] = new JsonArray(45, 15) })!["id"]!;
        var s = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = section })!;
        Assert.True(s["problem"] == null, (string?)s["problem"]);
        var layers = s["pens"]!.AsArray().OfType<JsonObject>().Select(p => (string)p["layer"]!).ToList();
        Assert.True(layers.Contains("HATCH"), string.Join(",", layers));
    }

    [Fact]
    public void LandXML_surfaces_become_terrains_and_a_breakline_holds_its_line()
    {
        // A 3 × 3 TIN, step 20, z = 0.1 x, as LandXML writes it: northing first.
        var pts = string.Join("", from i in Enumerable.Range(0, 3) from j in Enumerable.Range(0, 3) select $"<P id=\"{i * 3 + j + 1}\">{j * 20} {i * 20} {0.1 * i * 20}</P>");
        var xml = $"<LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\"><Surfaces><Surface name=\"Existing ground\"><Definition surfType=\"TIN\"><Pnts>{pts}</Pnts><Faces><F>1 2 4</F></Faces></Definition></Surface></Surfaces></LandXML>";
        var s = LandXml.Surfaces(xml).Single();
        Assert.Equal(9, s.Points.Count);
        Assert.Equal(new Vec3(40, 0, 4), s.Points[6]);
        var f = F();
        var id = (string)f.Run("civil.importLandXml", new JsonObject { ["text"] = xml })!["ids"]![0]!;
        Assert.Equal("Existing ground", f.Model.Get(id)!.Name);
        Assert.Equal(8, Terrains.Of(f.Model.Get(id)!).Tin.Triangles.Count);
        // A ridge along y = 20 at z 10: the ground there follows it, not the plane.
        f.Run("civil.setBreakline", new JsonObject { ["id"] = id, ["points"] = new JsonArray(new JsonArray(0, 20, 10), new JsonArray(40, 20, 10)) });
        Assert.Equal(10, Terrains.Of(f.Model.Get(id)!).ElevationAt(15, 20)!.Value, 6);
        Assert.Throws<CommandException>(() => f.Run("civil.importLandXml", new JsonObject { ["text"] = "<nope" }));
    }

    [Fact]
    public void A_LandXML_surface_in_feet_or_US_survey_feet_is_read_in_metres()
    {
        string Xml(string units) => $"<LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\"><Units>{units}</Units><Surfaces><Surface name=\"EG\"><Definition surfType=\"TIN\"><Pnts>"
            + "<P id=\"1\">0 0 100</P><P id=\"2\">0 1000 100</P><P id=\"3\">1000 0 110</P></Pnts></Definition></Surface></Surfaces></LandXML>";
        var feet = LandXml.Surfaces(Xml("<Imperial linearUnit=\"foot\" areaUnit=\"squareFoot\"/>")).Single().Points;
        Assert.Equal(new Vec3(304.8, 0, 30.48), new Vec3(Math.Round(feet[1].X, 9), feet[1].Y, Math.Round(feet[1].Z, 9)));
        var survey = LandXml.Surfaces(Xml("<Imperial linearUnit=\"USSurveyFoot\"/>")).Single().Points;
        Assert.Equal(1000 * 1200.0 / 3937, survey[2].Y, 9);
        Assert.Equal(1000, LandXml.Surfaces(Xml("<Metric linearUnit=\"meter\"/>")).Single().Points[1].X);
    }

    [Fact]
    public void Irregular_concave_boundary_clips_the_TIN_and_split_and_merge_roundtrip()
    {
        var f = F();
        var t = Slope(f);
        // L-shaped boundary: 40x40 minus the top-right 20x20 quadrant (area = 1600 - 400 = 1200 m²).
        var lShape = new JsonArray(
            new JsonArray(0, 0), new JsonArray(40, 0), new JsonArray(40, 20),
            new JsonArray(20, 20), new JsonArray(20, 40), new JsonArray(0, 40));
        f.Run("civil.setBoundary", new JsonObject { ["id"] = t, ["points"] = lShape });
        var clipped = Terrains.Of(f.Model.Get(t)!);
        Assert.NotNull(clipped.ElevationAt(10, 10));
        Assert.Null(clipped.ElevationAt(30, 30)); // inside the concave notch
        var geom = Terrains.Geometry(f.Model.Get(t)!, f.Model, null);
        Assert.NotNull(geom);
        Assert.Single(geom!.Parts);
        var qty = Terrains.Quantities(f.Model.Get(t)!, geom, f.Model).First();
        var area = qty.Quantities.First(i => i.Name == "PlanArea").Value;
        Assert.Equal(1200, area, 1);

        // Split along x = 15 into two terrains, then merge them back.
        var split = (JsonObject)f.Run("civil.splitTerrain", new JsonObject { ["id"] = t, ["x1"] = 15, ["y1"] = -5, ["x2"] = 15, ["y2"] = 45 })!;
        var ids = split["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(2, ids.Count);
        Assert.Equal(t, ids[0]);   // the larger half stays the terrain, its GlobalId kept
        var merged = (JsonObject)f.Run("civil.mergeTerrains", new JsonObject { ["ids"] = new JsonArray(ids[0], ids[1]) })!;
        var mergedId = (string)merged["id"]!;
        Assert.Equal(t, mergedId);
        Assert.Null(f.Model.Get(ids[1]));
        Assert.NotNull(Terrains.Of(f.Model.Get(mergedId)!).ElevationAt(10, 10));
    }

    [Fact]
    public void Slope_tags_and_cut_fill_grid_draw_in_plan_and_sculpt_reports_graded_volumes()
    {
        var f = F();
        var t = Slope(f);
        // Two-point grade line along +x on z = 0.05 x -> 5.0 % (1:20.0), plus cut/fill grid and a raise sculpt.
        f.Run("civil.addSlope", new JsonObject { ["id"] = t, ["x1"] = 5, ["y1"] = 5, ["x2"] = 25, ["y2"] = 5 });
        f.Run("civil.sculpt", new JsonObject { ["id"] = t, ["kind"] = "raise", ["x"] = 20, ["y"] = 20, ["radius"] = 6, ["amount"] = 1.0 });
        f.Run("civil.updateTerrain", new JsonObject { ["id"] = t, ["cutFillGrid"] = 10 });

        var geom = Terrains.Geometry(f.Model.Get(t)!, f.Model, null);
        var qty = Terrains.Quantities(f.Model.Get(t)!, geom, f.Model).First();
        Assert.True(qty.Quantities.First(i => i.Name == "FillVolume").Value > 10);
        Assert.True(qty.Quantities.First(i => i.Name == "SculptFill").Value > 10);

        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        var texts = d["pens"]!.AsArray().OfType<JsonObject>().Where(p => p["texts"] != null)
            .SelectMany(p => p["texts"]!.AsArray().OfType<JsonObject>()).Select(x => (string)x["text"]!).ToList();
        Assert.Contains(texts, txt => txt.Contains("5.0 %"));
        Assert.Contains(texts, txt => txt.StartsWith("+1.00") || txt.StartsWith("+0."));

        // Snapshot existing makes the sculpted surface the new baseline (zero fill vs snapshot).
        f.Run("civil.snapshotExisting", new JsonObject { ["id"] = t });
        var geom2 = Terrains.Geometry(f.Model.Get(t)!, f.Model, null);
        var qty2 = Terrains.Quantities(f.Model.Get(t)!, geom2, f.Model).First();
        Assert.Equal(0, qty2.Quantities.First(i => i.Name == "FillVolume").Value, 2);
    }

    [Fact]
    public void Vertical_point_editing_and_subregions_and_sloped_pads_work()
    {
        var f = F();
        var t = Slope(f);
        var beforeZ = Terrains.Of(f.Model.Get(t)!).ElevationAt(0, 0)!.Value;

        // T3: move point 0 up by 2 m in one undo step, then undo it.
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = t, ["index"] = 0, ["dz"] = 2.0 });
        Assert.Equal(beforeZ + 2.0, Terrains.Of(f.Model.Get(t)!).ElevationAt(0, 0)!.Value, 6);
        f.Session.Undo();
        Assert.Equal(beforeZ, Terrains.Of(f.Model.Get(t)!).ElevationAt(0, 0)!.Value, 6);

        // T3: add and remove a point.
        var added = (JsonObject)f.Run("civil.addTerrainPoint", new JsonObject { ["id"] = t, ["x"] = 15, ["y"] = 15, ["z"] = 5.0 })!;
        Assert.Equal(5.0, Terrains.Of(f.Model.Get(t)!).ElevationAt(15, 15)!.Value, 6);
        f.Run("civil.removeTerrainPoint", new JsonObject { ["id"] = t, ["index"] = (int)added["index"]! });

        // T4: material subregion adds a GeometryPart and Lapin_SubregionQuantities without changing ground elevation.
        var srPts = new JsonArray(new JsonArray(5, 5), new JsonArray(15, 5), new JsonArray(15, 15), new JsonArray(5, 15));
        f.Run("civil.addSubregion", new JsonObject { ["terrainId"] = t, ["points"] = srPts, ["name"] = "Courtyard", ["material"] = "Gravel" });
        var geom = Terrains.Geometry(f.Model.Get(t)!, f.Model, null)!;
        Assert.Contains(geom.Parts, p => p.Name == "Courtyard");
        var qSets = Terrains.Quantities(f.Model.Get(t)!, geom, f.Model).ToList();
        var srQty = qSets.First(q => q.Name == "Lapin_SubregionQuantities");
        Assert.Equal(100, srQty.Quantities.First(i => i.Name == "PlanArea").Value, 1);

        // T4: sloped platform accepts cutSlope / fillSlope.
        var padId = (string)f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["x1"] = 20, ["y1"] = 20, ["x2"] = 30, ["y2"] = 30, ["elevation"] = 1.0 })!["id"]!;
        f.Run("civil.updatePlatform", new JsonObject { ["id"] = padId, ["cutSlope"] = 1.5, ["fillSlope"] = 1.5 });
        Assert.Equal(1.5, f.Model.Get(padId)!.Num("cutSlope"));
    }

    static double TerrainQty(Fixture f, string t, string name) =>
        f.Session.Quantities(t).Single(q => q.Name == "Lapin_TerrainQuantities").Quantities.FirstOrDefault(x => x.Name == name)?.Value ?? 0;

    [Fact]
    public void Splitting_a_terrain_keeps_its_platforms_and_ditches_each_on_the_half_it_lies_in_and_merging_brings_them_back()
    {
        var f = F();
        var t = Slope(f);
        var pad = (string)f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["x1"] = 22, ["y1"] = 10, ["x2"] = 30, ["y2"] = 20, ["elevation"] = 1.3 })!["id"]!;
        var ditch = (string)f.Run("civil.addDitch", new JsonObject { ["terrainId"] = t, ["points"] = new JsonArray(new JsonArray(2, 30), new JsonArray(10, 30)) })!["id"]!;
        f.Run("civil.addSpot", new JsonObject { ["terrainId"] = t, ["x"] = 5, ["y"] = 5 });
        f.Run("civil.addSlope", new JsonObject { ["terrainId"] = t, ["x1"] = 25, ["y1"] = 5, ["x2"] = 35, ["y2"] = 5 });
        var cutBefore = TerrainQty(f, t, "CutVolume");
        Assert.True(cutBefore > 1);

        // Along x = 15: the right half (25 × 40) is the larger and stays the terrain; the ditch (x 2–10) goes to the left one.
        var ids = ((JsonObject)f.Run("civil.splitTerrain", new JsonObject { ["id"] = t, ["x1"] = 15, ["y1"] = -5, ["x2"] = 15, ["y2"] = 45 })!)["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(t, ids[0]);
        var other = ids[1];
        Assert.NotNull(f.Model.Get(pad));
        Assert.NotNull(f.Model.Get(ditch));
        Assert.Equal([pad], Terrains.Pads(f.Model, t).Select(x => x.Id));
        Assert.Equal([ditch], Terrains.DitchesOf(f.Model, other).Select(x => x.Id));
        Assert.Empty(Terrains.DitchesOf(f.Model, t));
        // The spot level (x 5) goes with the left half, the slope tag (x 25-35) stays.
        Assert.Single(Terrains.Spots(f.Model.Get(other)!));
        Assert.Empty(Terrains.Spots(f.Model.Get(t)!));
        Assert.Single(Terrains.SlopeTags(f.Model.Get(t)!));
        Assert.Empty(Terrains.SlopeTags(f.Model.Get(other)!));
        Assert.Equal(1000, TerrainQty(f, t, "PlanArea"), 1);
        Assert.Equal(600, TerrainQty(f, other, "PlanArea"), 1);
        Assert.Contains(f.Session.Quantities(t), q => q.Name == "Lapin_EarthworkQuantities");
        Assert.True(TerrainQty(f, t, "CutVolume") > 1);
        Assert.Equal(Spatial.StoreyOf(f.Model, t)?.Id, Spatial.StoreyOf(f.Model, other)?.Id);

        // One undo step brings the whole terrain back, with both on it.
        f.Session.Undo();
        Assert.Null(f.Model.Get(other));
        Assert.Null(f.Model.Get(t)!.Attrs[Terrains.BoundaryKey]);
        Assert.Equal([pad], Terrains.Pads(f.Model, t).Select(x => x.Id));
        Assert.Equal([ditch], Terrains.DitchesOf(f.Model, t).Select(x => x.Id));
        f.Session.Redo();
        Assert.Equal([ditch], Terrains.DitchesOf(f.Model, other).Select(x => x.Id));

        // Merged back (the second first: it keeps its GlobalId and takes the platform).
        var merged = (string)((JsonObject)f.Run("civil.mergeTerrains", new JsonObject { ["ids"] = new JsonArray(other, t) })!)["id"]!;
        Assert.Equal(other, merged);
        Assert.Null(f.Model.Get(t));
        Assert.Equal([pad], Terrains.Pads(f.Model, other).Select(x => x.Id));
        Assert.Equal([ditch], Terrains.DitchesOf(f.Model, other).Select(x => x.Id));
        Assert.Equal(cutBefore, TerrainQty(f, other, "CutVolume"), 3);
        Assert.Single(Terrains.Spots(f.Model.Get(other)!));
        Assert.Single(Terrains.SlopeTags(f.Model.Get(other)!));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(t));
        Assert.Equal([pad], Terrains.Pads(f.Model, t).Select(x => x.Id));
        Assert.Equal([ditch], Terrains.DitchesOf(f.Model, other).Select(x => x.Id));
    }

    [Fact]
    public void Merging_terrains_on_different_datums_keeps_every_existing_surface_and_the_strokes_heights()
    {
        var f = F();
        string Add(int x0, double datum)
        {
            var lines = new List<string>();
            for (var i = 0; i <= 2; i++) for (var j = 0; j <= 4; j++) lines.Add($"{x0 + i * 10} {j * 10} {datum + 0.02 * (x0 + i * 10)}");
            return (string)f.Run("civil.addTerrain", new JsonObject { ["text"] = string.Join("\n", lines), ["datum"] = datum, ["interval"] = 0.5, ["depth"] = 1 })!["id"]!;
        }
        var a = Add(0, 100);
        var b = Add(40, 50);
        // A: raised, then snapshotted (its existing ground is the raised one: no fill left). B: raised, and flattened to 2.0 elsewhere.
        f.Run("civil.sculpt", new JsonObject { ["terrainId"] = a, ["kind"] = "raise", ["x"] = 10, ["y"] = 20, ["radius"] = 6, ["amount"] = 1.0 });
        f.Run("civil.snapshotExisting", new JsonObject { ["id"] = a });
        f.Run("civil.sculpt", new JsonObject { ["terrainId"] = b, ["kind"] = "raise", ["x"] = 50, ["y"] = 12, ["radius"] = 5, ["amount"] = 1.0 });
        f.Run("civil.sculpt", new JsonObject { ["terrainId"] = b, ["kind"] = "flatten", ["x"] = 50, ["y"] = 30, ["radius"] = 5, ["level"] = 2.0 });
        Assert.Equal(0, TerrainQty(f, a, "FillVolume"), 2);
        var fillB = TerrainQty(f, b, "FillVolume");
        var cutB = TerrainQty(f, b, "CutVolume");
        Assert.True(fillB > 10);
        var flatB = Terrains.Of(f.Model.Get(b)!).ElevationAt(50, 30)!.Value;
        Assert.Equal(2.0, flatB, 2);

        var m = (string)((JsonObject)f.Run("civil.mergeTerrains", new JsonObject { ["ids"] = new JsonArray(a, b) })!)["id"]!;
        Assert.Equal(a, m);
        var merged = f.Model.Get(m)!;
        Assert.Equal(100, merged.Num("datum"));
        // The existing surface is A's snapshot beside B's survey: the fill is B's raise only, as before.
        Assert.True(merged.Attrs["existingPoints"] is JsonArray { Count: > 15 });
        Assert.Equal(fillB, TerrainQty(f, m, "FillVolume"), 0);
        Assert.Equal(cutB, TerrainQty(f, m, "CutVolume"), 0);
        // Heights stay the model's: B's flatten still at 2.0, its ground where it was.
        var tm = Terrains.Of(merged);
        Assert.Equal(flatB, tm.ElevationAt(50, 30)!.Value, 3);
        Assert.Equal(0.02 * 60, tm.ElevationAt(60, 0)!.Value - 0, 3);
        Assert.Equal(Terrains.Existing(f.Model.Get(m)!).ElevationAt(10, 20)!.Value, tm.ElevationAt(10, 20)!.Value, 3);
    }

    [Fact]
    public void A_subregions_skin_follows_the_ground_along_a_breakline_and_keeps_its_area()
    {
        var f = F();
        var t = Slope(f);
        // A ridge at model height 3.0 across the grid's diagonals: no survey point lies on it, so only the constraint holds it.
        f.Run("civil.setBreakline", new JsonObject { ["id"] = t, ["points"] = new JsonArray(new JsonArray(3, 7, 103), new JsonArray(36, 33, 103)) });
        // A concave (L-shaped) subregion over most of it: 30 × 30 less the 15 × 15 corner = 675 m².
        var outline = new JsonArray(new JsonArray(5, 5), new JsonArray(35, 5), new JsonArray(35, 20), new JsonArray(20, 20), new JsonArray(20, 35), new JsonArray(5, 35));
        f.Run("civil.addSubregion", new JsonObject { ["terrainId"] = t, ["points"] = outline, ["name"] = "Gravel bed", ["material"] = "Gravel" });
        var e = f.Model.Get(t)!;
        var ground = Terrains.Of(e);
        var sr = Terrains.SubregionsOf(e).Single();
        var surface = Terrains.SubregionSurface(ground, ArcOutline.Points(sr.Contour, sr.Arcs))!;
        var skin = new Terrain(surface);
        double plan = surface.Triangles.Sum(x => Math.Abs((surface.Points[x.B].X - surface.Points[x.A].X) * (surface.Points[x.C].Y - surface.Points[x.A].Y) - (surface.Points[x.B].Y - surface.Points[x.A].Y) * (surface.Points[x.C].X - surface.Points[x.A].X)) / 2);
        Assert.Equal(675, plan, 3);
        // Along the ridge and a hand either side of it, inside the subregion: the skin's top (1.5 cm over its surface) within 2 cm of the ground.
        var (p, q) = (new Vec2(3, 7), new Vec2(36, 33));
        var normal = new Vec2(-(q - p).Y, (q - p).X) * (1 / (q - p).Length);
        var checkedPoints = 0;
        for (var k = 1; k < 20; k++)
            foreach (var off in new[] { -0.3, 0.0, 0.3 })
            {
                var s = p + (q - p) * (k / 20.0) + normal * off;
                if (!Polygon2.Contains(ArcOutline.Points(sr.Contour, sr.Arcs), s)) continue;
                var top = skin.ElevationAt(s.X, s.Y)!.Value + 0.015;
                Assert.InRange(top - ground.ElevationAt(s.X, s.Y)!.Value, -0.02, 0.02);
                checkedPoints++;
            }
        Assert.True(checkedPoints > 20);
        Assert.Equal(3.0, ground.ElevationAt(15, 7 + 26 * 12 / 33.0)!.Value, 6);
        // The part is a closed skin, in the geometry with its area.
        var geom = f.Session.Geometry(t)!;
        Assert.Contains(geom.Parts, x => x.Name == "Gravel bed");
        Assert.Contains(geom.Plan!, x => x.Kind == "symbol" && x.Closed && x.Points.Count == 6);
        Assert.Equal(675, f.Session.Quantities(t).Single(x => x.Name == "Lapin_SubregionQuantities").Quantities.Single(x => x.Name == "PlanArea").Value, 3);
    }
}
