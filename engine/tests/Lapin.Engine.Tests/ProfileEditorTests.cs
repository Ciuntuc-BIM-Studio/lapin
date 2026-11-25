using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Site grading T3 (docs/plan-site-grading.md): the profile editor. A line's profile reads both grounds exactly on their
/// triangulations; vertical curves are the analytic parabola; grades typed give the exact heights; a section line over the
/// terrain changes the ground beside it within its reach (Î3's Shepard field, refused when the reach is short) and nothing
/// beyond; a cross section edited makes its cross line; every edit one undo step; the long profile and the cross sections
/// as sheet drawings; a hardscape's edge and a path's red line read as profiles.
/// </summary>
public sealed class ProfileEditorTests
{
    static Fixture F() => new(new ArchitectureModule(), new CivilModule());
    static JsonArray P(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);
    static JsonArray Pts(params double[][] pts) => new([.. pts.Select(p => (JsonNode)P(p))]);

    /// <summary>A hill: 40 × 40 m, a point every 2 m, heights a smooth bump (datum 0).</summary>
    static double Hill(double x, double y) => 3 * Math.Exp(-((x - 20) * (x - 20) + (y - 20) * (y - 20)) / 150) + 0.05 * x;

    static string Field(Fixture f, Func<double, double, double> z)
    {
        var pts = new JsonArray();
        for (var i = 0; i <= 20; i++) for (var j = 0; j <= 20; j++) pts.Add(P(i * 2.0, j * 2.0, z(i * 2.0, j * 2.0)));
        return (string)f.Run("civil.addTerrain", new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts, ["datum"] = 0, ["interval"] = 0.5, ["majorEvery"] = 2, ["depth"] = 1 })!["id"]!;
    }

    static string Id(JsonNode? n) => (string)n!["id"]!;
    static JsonObject Profile(Fixture f, string id) => (JsonObject)f.Session.Query("civil.profile", new JsonObject { ["id"] = id })!;
    static List<(double S, double? Z)> Pairs(JsonNode? n) => [.. ((JsonArray)n!).OfType<JsonArray>().Select(p => ((double)p[0]!, p[1] is null ? (double?)null : (double)p[1]!))];
    static Terrain Ground(Fixture f, string t) => Terrains.Of(f.Model.Get(t)!);
    /// <summary>Equal to 10 nm (what the profile's data is rounded to).</summary>
    static void Near(double expected, double actual) => Assert.True(Math.Abs(expected - actual) < 1e-8, $"{expected} ≠ {actual}");

    [Fact]
    public void A_profile_reads_the_existing_and_the_design_ground_exactly_on_their_triangulations()
    {
        var f = F();
        var t = Field(f, Hill);
        var line = Id(f.Run("civil.addSectionLine", new JsonObject { ["points"] = Pts([3.3, 7.1], [36.2, 31.7]) }));
        var pr = Profile(f, line);
        Assert.Equal("featureLine", (string)pr["kind"]!);
        Assert.True((bool)pr["section"]!);
        var (a, b) = (new Vec2(3.3, 7.1), new Vec2(36.2, 31.7));
        var len = (b - a).Length;
        var g = Ground(f, t);
        var existing = Pairs(pr["existing"]);
        // Many more samples than the 2 m grid would give along a 41 m line (it crosses three edges a cell).
        Assert.True(existing.Count > 30, $"{existing.Count} samples");
        // Every sample is the ground there; between two samples the ground is straight (they are its edges' crossings).
        for (var i = 0; i < existing.Count; i++)
        {
            var p = a + (b - a) * (existing[i].S / len);
            Near(g.ElevationAt(p.X, p.Y)!.Value, existing[i].Z!.Value);
            if (i == 0) continue;
            foreach (var q in new[] { 0.25, 0.5, 0.75 })
            {
                var s = existing[i - 1].S + (existing[i].S - existing[i - 1].S) * q;
                var m = a + (b - a) * (s / len);
                var linear = existing[i - 1].Z!.Value + (existing[i].Z!.Value - existing[i - 1].Z!.Value) * q;
                Near(linear, g.ElevationAt(m.X, m.Y)!.Value);
            }
        }
        // An untouched section line only reads the ground: the terrain holds nothing of it, its line is the ground.
        Assert.Null(f.Model.Get(t)!.Attrs[FeatureLines.Derived]);
        foreach (var (s, z) in Pairs(pr["line"])) { var p = a + (b - a) * (s / len); Near(g.ElevationAt(p.X, p.Y)!.Value, z!.Value); }
    }

    [Fact]
    public void A_vertical_curve_is_the_parabola_between_its_tangents_and_the_ground_passes_through_it()
    {
        var f = F();
        var t = Field(f, (_, _) => 0);
        var id = Id(f.Run("civil.addFeatureLine", new JsonObject { ["points"] = Pts([2, 20, 1], [22, 20, 2], [38, 20, 1.2]) }));
        f.Run("civil.setProfileCurve", new JsonObject { ["id"] = id, ["index"] = 1, ["length"] = 12 });
        var line = Pairs(Profile(f, id)["line"]);
        var (g1, g2, L, pvi) = ((2 - 1) / 20.0, (1.2 - 2) / 16.0, 12.0, 20.0);
        double Analytic(double s)
        {
            if (s <= pvi - L / 2) return 1 + g1 * s;
            if (s >= pvi + L / 2) return 2 + g2 * (s - pvi);
            var x = s - (pvi - L / 2);
            return 2 - g1 * L / 2 + g1 * x + (g2 - g1) * x * x / (2 * L);
        }
        Assert.True(line.Count(p => p.S > pvi - L / 2 && p.S < pvi + L / 2) >= 2);
        foreach (var (s, z) in line) Assert.Equal(Analytic(s), z!.Value, 9);
        // The middle ordinate: the curve passes (g2 − g1)·L/8 under the PVI.
        Assert.Equal(2 + (g2 - g1) * L / 8, line.First(p => Math.Abs(p.S - pvi) < 1e-6).Z!.Value, 9);
        // The terrain is shaped by it (a breakline): at the curve's points its height is the parabola's, between them within the
        // one rule's millimetre (the chords).
        var g = Ground(f, t);
        foreach (var (s, z) in line) Assert.Equal(z!.Value, g.ElevationAt(2 + s, 20)!.Value, 9);
        foreach (var s in new[] { 15.0, 17.5, 20, 23, 25.5 }) Assert.InRange(g.ElevationAt(2 + s, 20)!.Value - Analytic(s), -0.0011, 0.0011);
        // A curve longer than its neighbours allow is refused; an end has none.
        Assert.Throws<CommandException>(() => f.Run("civil.setProfileCurve", new JsonObject { ["id"] = id, ["index"] = 1, ["length"] = 40 }));
        Assert.Throws<CommandException>(() => f.Run("civil.setProfileCurve", new JsonObject { ["id"] = id, ["index"] = 0, ["length"] = 4 }));
    }

    [Fact]
    public void A_grade_typed_between_two_points_gives_the_exact_heights()
    {
        var f = F();
        Field(f, Hill);
        var id = Id(f.Run("civil.addFeatureLine", new JsonObject { ["points"] = Pts([4, 10, 5], [14, 10], [30, 10]) }));
        f.Run("civil.setFeatureLineGrade", new JsonObject { ["id"] = id, ["segment"] = 0, ["grade"] = "1:50" });
        f.Run("civil.setFeatureLineGrade", new JsonObject { ["id"] = id, ["segment"] = 1, ["grade"] = "-2,5 %" });
        var pts = (JsonArray)Profile(f, id)["points"]!;
        Assert.Equal(5.0, (double)pts[0]!["z"]!, 9);
        Assert.Equal(5 + 10 / 50.0, (double)pts[1]!["z"]!, 9);
        Assert.Equal(5 + 0.2 - 0.025 * 16, (double)pts[2]!["z"]!, 9);
        Assert.Equal(-0.025, (double)pts[1]!["grade"]!, 9);
        Assert.True(pts.All(p => (bool)p!["designed"]!));
    }

    [Fact]
    public void A_section_line_shaped_moves_the_ground_beside_it_within_its_reach_and_nothing_beyond()
    {
        var f = F();
        var t = Field(f, Hill);
        var before = Terrains.Of(f.Model.Get(t)!);
        var line = Id(f.Run("civil.addSectionLine", new JsonObject { ["points"] = Pts([4, 21], [36, 21]), ["reach"] = 6 }));
        // A point raised 1.5 m over the ground at station 16 (x = 20): the line rises to it from its ends, which stay on the ground.
        var gz = before.ElevationAt(20, 21)!.Value;
        f.Run("civil.insertProfilePoint", new JsonObject { ["id"] = line, ["station"] = 16, ["z"] = gz + 1.5 });
        var after = Ground(f, t);
        Assert.Equal(gz + 1.5, after.ElevationAt(20, 21)!.Value, 6);
        // Halfway between the end (no change) and the point the change is half: the line follows the ground plus it, linearly.
        Assert.Equal(before.ElevationAt(12, 21)!.Value + 0.75, after.ElevationAt(12, 21)!.Value, 6);
        // Across the line, the change fades by Shepard's weighting: at 3 m of a 6 m reach, half of it; the survey points there carry it.
        Assert.Equal(before.ElevationAt(20, 24)!.Value + 1.5 * FeatureLines.Shepard(3, 6), after.ElevationAt(20, 24)!.Value, 6);
        Assert.Equal(before.ElevationAt(20, 18)!.Value + 1.5 * FeatureLines.Shepard(3, 6), after.ElevationAt(20, 18)!.Value, 6);
        // Every survey point beyond the reach stays where it was, and the ground a triangle further on is untouched.
        var survey = Terrains.Points(f.Model.Get(t)!);
        foreach (var p in survey.Where(p => Math.Abs(p.Y - 21) > 6.5 || p.X < 4 - 6.5 || p.X > 36 + 6.5))
            Assert.Equal(before.ElevationAt(p.X, p.Y)!.Value, after.ElevationAt(p.X, p.Y)!.Value, 9);
        foreach (var (x, y) in new[] { (20.3, 30.1), (9.7, 11.3), (31.1, 33.3) })
            Assert.Equal(before.ElevationAt(x, y)!.Value, after.ElevationAt(x, y)!.Value, 9);
        // The survey itself is not rewritten (the change is derived); cut and fill measure it against the existing ground.
        Assert.Equal(survey.Select(p => p.Z), Terrains.Points(f.Model.Get(t)!).Select(p => p.Z));
        Assert.Equal(before.ElevationAt(20, 21)!.Value, Terrains.Existing(f.Model.Get(t)!).ElevationAt(20, 21)!.Value, 9);
        var q = f.Session.Reader.Quantities(t).First(s => s.Name == "Lapin_TerrainQuantities");
        Assert.True(q.Quantities.First(v => v.Name == "FillVolume").Value > 1);
        // A reach shorter than twice the change is refused, saying why.
        var ex = Assert.Throws<CommandException>(() => f.Run("civil.updateFeatureLine", new JsonObject { ["id"] = line, ["reach"] = 2 }));
        Assert.Contains("twice the largest change", ex.Message);
        // One undo step takes the point and the ground's change back.
        f.Session.Undo();
        Assert.Equal(gz, Ground(f, t).ElevationAt(20, 21)!.Value, 9);
        Assert.Equal(2, FeatureLines.SpecOf(f.Model.Get(line)!).Corners.Count);
        Assert.Null(f.Model.Get(t)!.Attrs[FeatureLines.Derived]);
    }

    [Fact]
    public void A_cross_section_point_dragged_makes_its_cross_line_and_shapes_the_ground_across_in_one_step()
    {
        var f = F();
        var t = Field(f, Hill);
        var axis = Id(f.Run("civil.addSectionLine", new JsonObject { ["points"] = Pts([4, 20], [36, 20]) }));
        var cross = (JsonObject)f.Session.Query("civil.crossProfile", new JsonObject { ["id"] = axis, ["station"] = 10, ["width"] = 8 })!;
        Assert.Null(cross["crossId"]);
        var ex = Pairs(cross["existing"]);
        Assert.Equal(-8, ex[0].S, 9);
        Assert.Equal(8, ex[^1].S, 9);
        var g0 = Ground(f, t);
        // Offset +3 is to the right of the axis (it runs +x, so right is −y): (14, 17).
        var z = g0.ElevationAt(14, 17)!.Value - 0.8;
        var r = f.Run("civil.setCrossPoint", new JsonObject { ["id"] = axis, ["station"] = 10, ["width"] = 8, ["offset"] = 3, ["z"] = z })!;
        var crossId = Id(r);
        var cl = f.Model.Get(crossId)!;
        Assert.True(FeatureLines.SpecOf(cl).Section);
        Assert.Equal(axis, (string)cl.Attrs[LineProfiles.CrossKey]!["id"]!);
        Assert.Equal(z, Ground(f, t).ElevationAt(14, 17)!.Value, 6);
        var again = (JsonObject)f.Session.Query("civil.crossProfile", new JsonObject { ["id"] = axis, ["station"] = 10, ["width"] = 8 })!;
        Assert.Equal(crossId, (string)again["crossId"]!);
        Assert.Contains(((JsonArray)again["points"]!).OfType<JsonObject>(), p => Math.Abs((double)p["offset"]! - 3) < 1e-6 && (bool)p["designed"]!);
        // The same point dragged again moves it, no new line.
        f.Run("civil.setCrossPoint", new JsonObject { ["id"] = axis, ["station"] = 10, ["width"] = 8, ["offset"] = 3, ["z"] = z - 0.2 });
        Assert.Single(LineProfiles.CrossLines(f.Model, axis));
        Assert.Equal(z - 0.2, Ground(f, t).ElevationAt(14, 17)!.Value, 6);
        // Undo twice: the line is gone and the ground as it was.
        f.Session.Undo();
        f.Session.Undo();
        Assert.Empty(LineProfiles.CrossLines(f.Model, axis));
        Assert.Equal(g0.ElevationAt(14, 17)!.Value, Ground(f, t).ElevationAt(14, 17)!.Value, 9);
    }

    [Fact]
    public void Every_profile_edit_is_one_undo_step_and_a_locked_point_holds()
    {
        var f = F();
        Field(f, Hill);
        var id = Id(f.Run("civil.addFeatureLine", new JsonObject { ["points"] = Pts([4, 10, 2], [20, 10, 3], [36, 10, 2]) }));
        string Attrs() => f.Model.Get(id)!.Attrs.ToJsonString();
        var start = Attrs();
        void Step(string cmd, JsonObject a)
        {
            var was = Attrs();
            a["id"] = id;
            f.Run(cmd, a);
            Assert.NotEqual(was, Attrs());
            f.Session.Undo();
            Assert.Equal(was, Attrs());
            f.Session.Redo();
        }
        Step("civil.insertProfilePoint", new JsonObject { ["station"] = 8 });
        var pts = (JsonArray)Profile(f, id)["points"]!;
        Assert.Equal(4, pts.Count);
        // Inserted without a height, the line is as it was: its height there is the line's.
        Assert.Equal(2 + 8 / 16.0, (double)pts[1]!["z"]!, 9);
        Assert.True((bool)pts[1]!["movable"]!);
        Assert.False((bool)pts[0]!["movable"]!);
        Step("civil.moveProfilePoint", new JsonObject { ["index"] = 1, ["station"] = 10, ["z"] = 2.9 });
        Assert.Equal(10, (double)((JsonArray)Profile(f, id)["points"]!)[1]!["station"]!, 6);
        Step("civil.setProfileCurve", new JsonObject { ["index"] = 2, ["length"] = 6 });
        Step("civil.lockProfilePoint", new JsonObject { ["index"] = 2, ["locked"] = true });
        Assert.Throws<CommandException>(() => f.Run("civil.moveProfilePoint", new JsonObject { ["id"] = id, ["index"] = 2, ["z"] = 1 }));
        Assert.Throws<CommandException>(() => f.Run("civil.setFeatureLinePoints", new JsonObject { ["id"] = id, ["indices"] = new JsonArray(2), ["z"] = 1 }));
        Assert.Throws<CommandException>(() => f.Run("civil.deleteProfilePoint", new JsonObject { ["id"] = id, ["index"] = 2 }));
        Step("civil.deleteProfilePoint", new JsonObject { ["index"] = 1 });
        Assert.Equal(3, ((JsonArray)Profile(f, id)["points"]!).Count);
        // A bend in plan stays at its station.
        var bent = Id(f.Run("civil.addFeatureLine", new JsonObject { ["points"] = Pts([4, 30, 2], [20, 30, 3], [30, 36, 2]) }));
        Assert.Throws<CommandException>(() => f.Run("civil.moveProfilePoint", new JsonObject { ["id"] = bent, ["index"] = 1, ["station"] = 12 }));
        Assert.NotEqual(start, Attrs());
    }

    [Fact]
    public void A_point_inserted_on_an_arc_splits_it_into_two_arcs_of_the_same_circle()
    {
        var f = F();
        Field(f, (_, _) => 0);
        var id = Id(f.Run("civil.addFeatureLine", new JsonObject { ["points"] = Pts([10, 10, 1], [30, 10, 2]), ["arcs"] = P(4) }));
        var before = FeatureLines.SpecOf(f.Model.Get(id)!).Edge(0);
        f.Run("civil.insertProfilePoint", new JsonObject { ["id"] = id, ["station"] = 9 });
        var s = FeatureLines.SpecOf(f.Model.Get(id)!);
        Assert.Equal(3, s.Corners.Count);
        Assert.Equal(before.Radius, s.Edge(0).Radius, 6);
        Assert.Equal(before.Radius, s.Edge(1).Radius, 6);
        Assert.True((s.Edge(0).Centre - before.Centre).Length < 1e-6);
        Assert.True(before.Distance(s.Corners[1]) < 1e-6);
    }

    [Fact]
    public void The_long_profile_and_the_cross_sections_are_sheet_drawings_printed_to_PDF_and_DXF()
    {
        var f = F();
        Field(f, Hill);
        var line = Id(f.Run("civil.addSectionLine", new JsonObject { ["points"] = Pts([4, 21], [36, 21]) }));
        f.Run("civil.insertProfilePoint", new JsonObject { ["id"] = line, ["station"] = 16, ["z"] = 6 });
        var longView = Id(f.Run("civil.addProfileView", new JsonObject { ["lineId"] = line, ["kind"] = "long", ["scale"] = 200, ["exaggeration"] = 5 }));
        var crossView = Id(f.Run("civil.addProfileView", new JsonObject { ["lineId"] = line, ["kind"] = "cross", ["interval"] = 8, ["width"] = 6 }));
        var sheet = new ProfileSheet();
        var lp = sheet.Pages(f.Session.Reader, f.Model.Get(longView)!, 380, 250);
        Assert.Single(lp);
        var pens = lp[0].Pens;
        Assert.Contains(pens, p => p.Layer == "PROFILE-LINE" && p.Segments.Count > 2);
        Assert.Contains(pens, p => p.Layer == "PROFILE-EXISTING" && p.Segments.Count > 10);
        Assert.Contains(pens.SelectMany(p => p.Texts), t => t.Text.StartsWith("0+016.00"));
        // At 1:200, 32 m are 160 mm wide (with its margins), heights five times as tall.
        Assert.InRange(lp[0].W, 160, 200);
        var cp = sheet.Pages(f.Session.Reader, f.Model.Get(crossView)!, 380, 250);
        // Stations 0, 8, 16, 24, 32: five small graphs, each its label.
        Assert.Equal(5, cp.SelectMany(p => p.Pens).SelectMany(p => p.Texts).Count(t => t.Text.StartsWith("0+0") && t.Bold));
        // A narrow page cuts the long profile into pages by station.
        Assert.True(sheet.Pages(f.Session.Reader, f.Model.Get(longView)!, 90, 250).Count >= 2);
        // Placed on a sheet, both print.
        var sh = Id(f.Run("sheet.create", new JsonObject { ["name"] = "Profiles", ["number"] = "C-201", ["paper"] = "A2" }));
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sh, ["viewId"] = longView });
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sh, ["viewId"] = crossView, ["x"] = 300, ["y"] = 330 });
        var placed = Sheets.Place(f.Session.Reader, f.Model.Get(sh)!);
        Assert.Equal(2, placed.Count);
        Assert.True(Sheets.Pdf(f.Session.Reader, [f.Model.Get(sh)!]).Length > 1000);
        Assert.Contains("PROFILE-LINE", System.Text.Encoding.UTF8.GetString(Sheets.Dxf(f.Session.Reader, f.Model.Get(sh)!)));
    }

    [Fact]
    public void A_hardscape_edge_and_a_path_axis_read_as_profiles_of_their_own_data()
    {
        var f = F();
        Field(f, Hill);
        var h = Id(f.Run("civil.addHardscape", new JsonObject { ["points"] = Pts([10, 10], [20, 10], [20, 16], [10, 16]), ["topMode"] = "corners", ["cornerZ"] = P(3, 3.2, 3.4, 3.1) }));
        var hp = Profile(f, h);
        Assert.Equal("hardscape", (string)hp["kind"]!);
        var z = ((JsonArray)hp["points"]!).Select(p => (double)p!["z"]!).ToArray();
        Assert.Equal([3, 3.2, 3.4, 3.1], z.Select(v => Math.Round(v, 6)));
        Assert.Equal(32, (double)hp["length"]!, 6);
        var path = Id(f.Run("civil.addPath", new JsonObject { ["points"] = Pts([2, 30], [38, 30]), ["width"] = 3 }));
        f.Run("civil.setCorridor", new JsonObject { ["id"] = path, ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(P(0, 2), P(18, 3, 8), P(36, 2.5)) } });
        var pp = Profile(f, path);
        Assert.Equal("corridor", (string)pp["kind"]!);
        Assert.Equal(3, ((JsonArray)pp["points"]!).Count);
        Assert.Equal(8, (double)((JsonArray)pp["points"]!)[1]!["curve"]!, 9);
        Assert.NotEmpty(Pairs(pp["existing"]));
    }

    [Fact]
    public void A_retaining_wall_on_a_bare_site_reads_as_a_profile_following_no_ground()
    {
        // Forum topic 258: a retaining wall drawn with no terrain, its Profile… opened. The answer has no ground series (the
        // interface reads them as empty), no points to drag, a finite line along its length, and a cross section with no ground.
        var f = F();
        var w = Id(f.Run("civil.addRetainingWall", new JsonObject { ["points"] = Pts([0, 0], [6.0667, 0]), ["height"] = 1.0 }));
        var pr = Profile(f, w);
        Assert.Equal("corridor", (string)pr["kind"]!);
        Assert.Equal("ground", (string)pr["mode"]!);
        Assert.Null(pr["existing"]);
        Assert.Null(pr["design"]);
        Assert.Empty((JsonArray)pr["points"]!);
        Assert.Equal(6.0667, (double)pr["length"]!, 6);
        var line = Pairs(pr["line"]);
        Assert.True(line.Count >= 2);
        Assert.All(line, p => Assert.True(p.Z is { } z && double.IsFinite(z)));
        var cross = (JsonArray)f.Session.Query("civil.crossProfile", new JsonObject { ["id"] = w, ["stations"] = P(1) })!;
        Assert.Single(cross);
        Assert.Null(cross[0]!["existing"]);
        Assert.NotNull(pr.ToJsonString());
    }
}
