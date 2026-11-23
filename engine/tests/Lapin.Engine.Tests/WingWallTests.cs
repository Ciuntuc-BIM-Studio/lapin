using System.Text;
using System.Text.Json.Nodes;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Platform;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The retaining wall with batters and the warped wing (docs/plan-wing-walls.md, S1–S2): faces that lean from the crown's edges, a
/// crown that slopes along a segment, corners whose faces meet on the lines their planes cross on, volumes and formwork against
/// hand-reckoned values, a warped face as an exact B-spline patch (its volume and area on the true surface, IFC4 IfcAdvancedBrep), and
/// the walls drawn before unchanged.
/// </summary>
public class WingWallTests(ITestOutputHelper output)
{
    static Fixture Site() => new(new CivilModule());

    static void Flat(Fixture f, double z = 0) =>
        f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = -30, ["y1"] = -30, ["x2"] = 60, ["y2"] = 60, ["elevation"] = z });

    static JsonArray Pts(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());

    /// <summary>A wall on flat ground at 0: crown 0.4 thick at 3.0 (fixed), the footing 0.8 × 0.3 under −0.3 (embedded 0.3), the high ground on the left.</summary>
    static string Wall(Fixture f, JsonObject? more = null, params (double, double)[] pts)
    {
        var a = new JsonObject { ["points"] = Pts(pts), ["thickness"] = 0.4, ["topElevation"] = 3.0, ["embed"] = 0.3, ["footingWidth"] = 0.8, ["footingThickness"] = 0.3 };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("civil.addRetainingWall", a)!["id"]!;
    }

    static Solid Body(Fixture f, string id) => Assert.Single(f.Session.Geometry(id)!.Parts).Solid;
    static double Q(Fixture f, string id, string name) => f.Session.Quantities(id).Single(x => x.Name == "Lapin_RetainingWallQuantities").Quantities.Single(x => x.Name == name).Value;

    static void Closed(Solid s)
    {
        // Every edge in two faces, once each way: a closed, oriented shell.
        var uses = new Dictionary<(int, int), int>();
        foreach (var f in s.Faces)
            foreach (var loop in f.Holes.Prepend(f.Outer))
                for (var k = 0; k < loop.Length; k++) { var key = (loop[k], loop[(k + 1) % loop.Length]); uses[key] = uses.GetValueOrDefault(key) + 1; }
        Assert.All(uses, u => { Assert.Equal(1, u.Value); Assert.True(uses.ContainsKey((u.Key.Item2, u.Key.Item1)), $"edge {u.Key} has no twin"); });
    }

    /// <summary>The area and the centroid's offset of a polygon in (w, z).</summary>
    static (double Area, double W) Section(params (double W, double Z)[] p)
    {
        double a = 0, cw = 0;
        for (var i = 0; i < p.Length; i++)
        {
            var (q, r) = (p[i], p[(i + 1) % p.Length]);
            var cr = q.W * r.Z - r.W * q.Z;
            a += cr;
            cw += (q.W + r.W) * cr;
        }
        return (a / 2, cw / (3 * a));
    }

    [Fact]
    public void A_wall_without_batters_is_built_as_before()
    {
        var f = Site();
        Flat(f);
        var w = Wall(f, null, (0, 0), (20, 0), (20, 10));
        Assert.False(BatteredWalls.Is(f.Model.Get(w)!));
        // The old wall's inverted T: 0.8 × 0.3 footing, 0.4 stem from −0.3 to 3.0; mitred, symmetric, so its area times its axis.
        Assert.Equal((0.8 * 0.3 + 0.4 * 3.3) * 30, Measure.Volume(Body(f, w)), 6);
        Assert.Equal(2 * 3.3 * 30 + 2 * 0.3 * 30, Q(f, w, "FormworkArea"), 6);
        // Upright batters set are no batters.
        f.Run("civil.updateRetainingWall", new JsonObject { ["id"] = w, ["frontBatter"] = 0 });
        Assert.False(BatteredWalls.Is(f.Model.Get(w)!));
        Assert.Equal((0.8 * 0.3 + 0.4 * 3.3) * 30, Measure.Volume(Body(f, w)), 6);
    }

    [Fact]
    public void A_front_batter_leans_the_face_from_the_crown_and_measures_by_hand()
    {
        var f = Site();
        Flat(f);
        var w = Wall(f, new JsonObject { ["frontBatter"] = 10 }, (0, 0), (10, 0));
        var s = Body(f, w);
        Closed(s);
        Assert.All(Enumerable.Range(0, s.Faces.Length), i => Assert.True(Measure.FacePlanarity(s, i) < 1e-9));
        // Stem 3.3 high, 0.4 at the crown, 0.73 at its base; the footing reaches 5 cm past it either side (0.8 would leave 3.5 cm), 0.83 wide.
        var stem = 0.4 * 3.3 + 0.1 * 3.3 * 3.3 / 2;
        Assert.Equal(stem * 10, Q(f, w, "StemVolume"), 6);
        Assert.Equal(0.83 * 0.3 * 10, Q(f, w, "FootingVolume"), 6);
        Assert.Equal((stem + 0.249) * 10, Q(f, w, "ConcreteVolume"), 6);
        Assert.Equal(Math.Sqrt(3.3 * 3.3 + 0.33 * 0.33) * 10, Q(f, w, "FormworkFront"), 6);
        Assert.Equal(3.3 * 10, Q(f, w, "FormworkBack"), 6);
        Assert.Equal(2 * (stem + 0.249), Q(f, w, "FormworkEnds"), 6);
        Assert.Equal(2 * 0.3 * 10, Q(f, w, "FormworkFooting"), 6);
        Assert.Equal(0.4 * 10, Q(f, w, "CrownArea"), 6);
        // The crown keeps its edges on the path's offsets; the foot of the front is 0.33 further out (high ground on the left: front on −y).
        Assert.Contains(s.Vertices, p => Math.Abs(p.Y + 0.2) < 1e-9 && Math.Abs(p.Z - 3.0) < 1e-9);
        Assert.Contains(s.Vertices, p => Math.Abs(p.Y + 0.53) < 1e-9 && Math.Abs(p.Z + 0.3) < 1e-9);
        // The section square to it shows the lean.
        var (parts, left, right) = Corridors.Section(f.Model, f.Model.Get(w)!, 5);
        Assert.Equal(-0.58, right, 9);
        Assert.Contains(parts[0].Polygon, p => Math.Abs(p.X + 0.53) < 1e-9 && Math.Abs(p.Y + 0.3) < 1e-9);
        Assert.Equal(stem + 0.249, Math.Abs(Section([.. parts[0].Polygon.Select(p => (p.X, p.Y))]).Area), 9);
    }

    [Fact]
    public void A_crown_sloping_along_its_segment_keeps_its_thickness_and_thickens_the_stem_where_it_is_tall()
    {
        var f = Site();
        Flat(f);
        var w = Wall(f, new JsonObject { ["frontBatter"] = 10, ["backBatter"] = 20 }, (0, 0), (10, 0));
        f.Run("civil.setRetainingWallSegment", new JsonObject { ["id"] = w, ["index"] = 0, ["topStart"] = 3.0, ["topEnd"] = 1.0 });
        var s = Body(f, w);
        Closed(s);
        Assert.All(Enumerable.Range(0, s.Faces.Length), i => Assert.True(Measure.FacePlanarity(s, i) < 1e-9));
        // h from 3.3 to 1.3: ∫ (0.4 h + (0.1 + 0.05) h²/2) dx over 10 m.
        double h0 = 3.3, h1 = 1.3;
        var stem = 0.4 * (h0 + h1) / 2 * 10 + 0.15 / 2 * 10 * (h0 * h0 + h0 * h1 + h1 * h1) / 3;
        Assert.Equal(stem, Q(f, w, "StemVolume"), 6);
        // The footing from the tallest section: front −0.2 − 0.33, back 0.2 + 0.165, 0.895 wide (wider than 0.8), and 5 cm past either side.
        Assert.Equal(0.995 * 0.3 * 10, Q(f, w, "FootingVolume"), 6);
        // The crown 0.4 thick all along: its face 10 × 0.4 sloping by 2 in 10.
        Assert.Equal(0.4 * Math.Sqrt(100 + 4), Q(f, w, "CrownArea"), 6);
        // At its low end the front's foot is 1.3 × 0.1 out.
        Assert.Contains(s.Vertices, p => Math.Abs(p.X - 10) < 1e-9 && Math.Abs(p.Y + 0.33) < 1e-9 && Math.Abs(p.Z + 0.3) < 1e-9);
        // Undone, the segment's crown is the wall's again.
        f.Session.Undo();
        Assert.Null(f.Model.Get(w)!.Attrs["segments"]);
    }

    [Fact]
    public void A_corner_joins_its_two_battered_faces_on_the_line_their_planes_cross_on()
    {
        var f = Site();
        Flat(f);
        // 10 m along +x, then 6 m along +y: a left turn, its outside the front (−y, then +x).
        var w = Wall(f, new JsonObject { ["frontBatter"] = 10 }, (0, 0), (10, 0), (10, 6));
        var s = Body(f, w);
        Closed(s);
        Assert.All(Enumerable.Range(0, s.Faces.Length), i => Assert.True(Measure.FacePlanarity(s, i) < 1e-9));
        // The fronts: y = −0.2 − (3 − z)·0.1 and x = 10.2 + (3 − z)·0.1 meet on x − 10 = −y: at the crown (10.2, −0.2), at the foot (10.53, −0.53).
        Assert.Contains(s.Vertices, p => (p - new Vec3(10.2, -0.2, 3.0)).Length < 1e-9);
        Assert.Contains(s.Vertices, p => (p - new Vec3(10.53, -0.53, -0.3)).Length < 1e-9);
        // The backs meet inside at (9.8, 0.2), upright.
        Assert.Contains(s.Vertices, p => (p - new Vec3(9.8, 0.2, 3.0)).Length < 1e-9);
        // Two legs of one section, flat crown: the section swept along the path, less what the corner takes (Pappus: its centroid's way).
        var (stem, sw) = Section((-0.53, -0.3), (0.2, -0.3), (0.2, 3.0), (-0.2, 3.0));
        var (foot, fw) = Section((-0.58, -0.6), (0.25, -0.6), (0.25, -0.3), (-0.58, -0.3));
        // The centroid's way round a 90° corner, offset w towards the inside (+w): 16 − 2w.
        Assert.Equal(stem * (16 - 2 * sw), Q(f, w, "StemVolume"), 6);
        Assert.Equal(stem * (16 - 2 * sw) + foot * (16 - 2 * fw), Q(f, w, "ConcreteVolume"), 6);
        // The plan cuts the crown, 0.4 thick, round the corner.
        var cut = f.Session.Geometry(w)!.Plan!.Single(p => p.Kind == "cut" && p.Closed);
        Assert.Contains(cut.Points, p => (p - new Vec2(10.2, -0.2)).Length < 1e-9);
        Assert.Contains(cut.Points, p => (p - new Vec2(9.8, 0.2)).Length < 1e-9);
    }

    [Fact]
    public void A_wing_dropping_from_a_corner_keeps_the_crown_level_over_it_and_steps_nowhere()
    {
        var f = Site();
        Flat(f);
        var w = Wall(f, new JsonObject { ["frontBatter"] = 10 }, (0, 0), (10, 0), (10, 6));
        f.Run("civil.setRetainingWallSegment", new JsonObject { ["id"] = w, ["index"] = 1, ["topStart"] = 3.0, ["topEnd"] = 1.0 });
        var s = Body(f, w);
        Closed(s);
        Assert.All(Enumerable.Range(0, s.Faces.Length), i => Assert.True(Measure.FacePlanarity(s, i) < 1e-9));
        // No face is a step: every upward face is the crown or the footing's top.
        var e = f.Model.Get(w)!;
        var built = BatteredWalls.Of(s)!;
        var areas = BatteredWalls.Areas(built, s);
        Assert.False(areas.ContainsKey("step"));
        Assert.False(areas.ContainsKey("other"));
        // The wing's slope starts where the corner ends: past where the footings' backs cross (y = 0.25).
        var wing = built.Legs[1];
        Assert.Equal(0.25, wing.S0, 9);
        Assert.All(s.Vertices.Where(p => p.X > 9.7 && p.Y <= 0.25 + 1e-9 && p.Z > 2), p => Assert.Equal(3.0, p.Z, 9));
        Assert.Contains(s.Vertices, p => Math.Abs(p.Y - 6) < 1e-9 && Math.Abs(p.Z - 1.0) < 1e-9);
        Assert.True(BatteredWalls.Is(e));
    }

    [Fact]
    public void A_warped_wing_turns_its_face_from_one_batter_to_another_exactly()
    {
        var f = Site();
        Flat(f);
        var w = Wall(f, null, (0, 0), (8, 0));
        // From 10:1 at its start to the embankment's 2:3 at its free end, its crown dropping from 3 to 1.
        f.Run("civil.setRetainingWallSegment", new JsonObject { ["id"] = w, ["index"] = 0, ["frontBatter"] = 10, ["warp"] = true, ["frontBatterEnd"] = 2.0 / 3, ["topStart"] = 3.0, ["topEnd"] = 1.0 });
        var s = Body(f, w);
        Closed(s);
        var built = BatteredWalls.Of(s)!;
        var warp = Assert.Single(built.Pieces.Warps);
        Assert.True(warp.Front);
        // The patch is the face: the front at every station and height (w = −0.2 − (top − z)·k, k from 0.1 to 1.5).
        for (var x = 0.0; x <= 8; x += 0.5)
            for (var v = 0.0; v <= 1; v += 0.25)
            {
                var top = 3.0 - 2 * x / 8;
                var z = -0.3 + v * (top + 0.3);
                var k = 0.1 + 1.4 * x / 8;
                var p = new Vec3(x, -0.2 - (top - z) * k, z);
                Assert.True(new SplineSurface(warp.Patch).Distance(p) < 1e-9, $"{p}");
            }
        // Its volume on the true surface: ∫ (0.4 h + k h²/2) dx, h = 3.3 − x/4, k = 0.1 + 0.175 x, by Simpson (exact: a cubic).
        double H(double x) => 3.3 - x / 4;
        double K(double x) => 0.1 + 0.175 * x;
        double A(double x) => 0.4 * H(x) + K(x) * H(x) * H(x) / 2;
        var exact = 8.0 / 6 * (A(0) + 4 * A(4) + A(8));
        Assert.Equal(exact, Q(f, w, "StemVolume"), 9);
        // The facets stay within the chord rule's millimetre of it.
        var facets = BatteredWalls.Make(built.Legs, built.Corners).Stem.Sum(Measure.Volume);
        Assert.InRange(facets / exact, 0.999, 1.001);
        // Its area, the patch's (against the facets of a fine grid of it).
        var fine = 0.0;
        const int n = 400;
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
            {
                var (a, b, c, d) = (warp.Patch.At((double)i / n, (double)j / n), warp.Patch.At((i + 1.0) / n, (double)j / n), warp.Patch.At((i + 1.0) / n, (j + 1.0) / n), warp.Patch.At((double)i / n, (j + 1.0) / n));
                fine += (Vec3.Cross(b - a, c - a).Length + Vec3.Cross(c - a, d - a).Length) / 2;
            }
        output.WriteLine($"front {Q(f, w, "FormworkFront"):0.######} m², fine grid {fine:0.######} m²");
        Assert.Equal(fine, Q(f, w, "FormworkFront"), 4);
        // Exactly: its planes and the one patch, the base's parabola a quadratic.
        var exactBody = BatteredWalls.Exact(built, s, 0.003);
        Assert.NotNull(exactBody);
        var kinds = exactBody!.SurfaceKinds();
        output.WriteLine(string.Join(", ", kinds.Select(k => $"{k.Key} {k.Value}")));
        Assert.Equal(1, kinds["bspline"]);
        Assert.DoesNotContain(exactBody.Edges, e => e.Curve is SplineCurve c && c.Curve.Degree > 2);
    }

    [Fact]
    public void A_portal_with_a_warped_wing_on_each_side_closes_and_is_exact()
    {
        var f = Site();
        Flat(f);
        // A wing 6 m back on the left, the face wall 12 m, a wing 6 m back on the right: the face 10:1 throughout, the wings warping to 1:1.
        var w = Wall(f, new JsonObject { ["frontBatter"] = 10 }, (0, 6), (0, 0), (12, 0), (12, 6));
        // The left wing is drawn towards the corner: its start is its free end.
        f.Run("civil.setRetainingWallSegment", new JsonObject { ["id"] = w, ["index"] = 0, ["warp"] = true, ["frontBatter"] = 1.0, ["frontBatterEnd"] = 10, ["topStart"] = 1.0, ["topEnd"] = 3.0 });
        f.Run("civil.setRetainingWallSegment", new JsonObject { ["id"] = w, ["index"] = 2, ["warp"] = true, ["frontBatterEnd"] = 1.0, ["topStart"] = 3.0, ["topEnd"] = 1.0 });
        var s = Body(f, w);
        Closed(s);
        var built = BatteredWalls.Of(s)!;
        Assert.Equal(2, built.Pieces.Warps.Count);
        // Its warp at its start is the free end's batter.
        Assert.True(built.Legs[0].Kf0 > built.Legs[0].Kf1);
        var exact = BatteredWalls.Exact(built, s, 0.003);
        Assert.NotNull(exact);
        Assert.Equal(2, exact!.SurfaceKinds()["bspline"]);
        // Its volume: the facets' less what they miss of the two warps.
        Assert.Equal(Measure.Volume(s) + built.Correction, Q(f, w, "ConcreteVolume"), 9);
        Assert.True(Math.Abs(built.Correction) < 0.002 * Q(f, w, "ConcreteVolume"));
    }

    [Fact]
    public void A_warped_wing_writes_to_IFC4_as_an_advanced_brep_and_reads_back_with_its_volume()
    {
        var f = Site();
        Flat(f);
        var w = Wall(f, null, (0, 0), (8, 0));
        f.Run("civil.setRetainingWallSegment", new JsonObject { ["id"] = w, ["index"] = 0, ["frontBatter"] = 10, ["warp"] = true, ["frontBatterEnd"] = 2.0 / 3, ["topStart"] = 3.0, ["topEnd"] = 1.0 });
        var (bytes, result) = IfcExport.Bytes(f.Session, "wing.ifc", new IfcExportOptions(AdvancedBrep: true));
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("=IFCADVANCEDBREP(", text);
        Assert.Contains("=IFCBSPLINESURFACEWITHKNOTS(", text);
        Assert.Contains("'RETAININGWALL'", text);
        var model = new IfcModel(StepFile.Read(bytes));
        var product = model.Products.Single(p => p.Name == f.Model.Get(w)!.Name);
        var meshes = model.Shape(product.Id).Body.Select(x => x.Mesh).ToList();
        var v = meshes.Sum(m => { var x = m.Welded(); var o = x.V[0]; return Math.Abs(x.T.Sum(t => Vec3.Dot(x.V[t.A] - o, Vec3.Cross(x.V[t.B] - o, x.V[t.C] - o))) / 6); });
        Assert.InRange(v / Q(f, w, "ConcreteVolume"), 0.995, 1.005);
        // A wall of planes is exact as its facets: written as a faceted brep, read back with its volume.
        var g = Wall(f, new JsonObject { ["frontBatter"] = 10 }, (0, 10), (10, 10), (10, 16));
        var (plain, _) = IfcExport.Bytes(f.Session, "walls.ifc");
        var back = new IfcModel(StepFile.Read(plain));
        var wall = back.Products.Single(p => p.Name == f.Model.Get(g)!.Name);
        var vol = back.Shape(wall.Id).Body.Select(x => x.Mesh).Sum(m => { var x = m.Welded(); var o = x.V[0]; return Math.Abs(x.T.Sum(t => Vec3.Dot(x.V[t.A] - o, Vec3.Cross(x.V[t.B] - o, x.V[t.C] - o))) / 6); });
        Assert.Equal(Q(f, g, "ConcreteVolume"), vol, 6);
    }

    [Fact]
    public void A_segment_takes_a_length_and_a_turn_and_the_ones_after_it_keep_theirs()
    {
        var f = Site();
        Flat(f);
        var w = Wall(f, null, (0, 0), (10, 0), (10, 6), (14, 6));
        f.Run("civil.setRetainingWallSegment", new JsonObject { ["id"] = w, ["index"] = 1, ["length"] = 4, ["angle"] = 45 });
        var path = RetainingWalls.Path(f.Model.Get(w)!);
        Assert.Equal(new Vec2(10, 0), path[1]);
        var r = Math.Sqrt(0.5);
        Assert.True((path[2] - new Vec2(10 + 4 * r, 4 * r)).Length < 1e-9);
        // The last turned −90° from the one before as it did: now along (r, −r), still 4 long.
        Assert.True((path[3] - (path[2] + new Vec2(4 * r, -4 * r))).Length < 1e-9);
        Assert.Throws<CommandException>(() => f.Run("civil.setRetainingWallSegment", new JsonObject { ["id"] = w, ["index"] = 3, ["topEnd"] = 1 }));
    }
}
