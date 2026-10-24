using System.Text.Json.Nodes;
using BubbleBim.Geometry;
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
}
