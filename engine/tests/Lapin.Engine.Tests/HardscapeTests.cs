using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Site grading T4 (docs/plan-site-grading.md): the hardscape, a typed build-up of layers over an outline; its top level, a
/// plane at a grade or by its corners' heights (planar or triangulated); standalone or cut into a terrain whose ground then meets
/// its top edges exactly (breaklines, the survey under it left out); a kerb along chosen edges with drops; a subregion converted;
/// IFC4 and IFC 4.3; one undo step; a project without hardscapes as before.
/// </summary>
public sealed class HardscapeTests
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

    static string Add(Fixture f, JsonObject a) => (string)f.Run("civil.addHardscape", a)!["id"]!;

    static double Vol(Fixture f, string id, string? key = null) => f.Session.Geometry(id)!.Parts.Where(p => key == null || p.Key == key).Sum(p => Measure.Volume(p.Solid));

    static Quantity Q(Fixture f, string id, string set, string name, string? part = null) =>
        f.Session.Quantities(id).First(s => s.Name == set && s.Part == part).Quantities.First(q => q.Name == name);

    [Fact]
    public void A_type_s_layers_make_the_body_top_down_and_its_quantities()
    {
        var f = F();
        var id = Add(f, new JsonObject { ["points"] = Rect(0, 0, 10, 8), ["type"] = "pavers", ["elevation"] = 1.5, ["storeyId"] = f.Storey });
        var e = f.Model.Get(id)!;
        Assert.Equal(Hardscapes.Class, e.Class);
        var type = Hardscapes.TypeOf(f.Model, e)!;
        Assert.Equal("Concrete pavers", type.Name);
        Assert.True(type.Bool("indicative"));
        Assert.Equal("RIGID", e.PredefinedType);
        // The layers top down: pavers 6 cm, sand 4 cm, crushed stone 20 cm; each its own part, in its material.
        var g = f.Session.Geometry(id)!;
        Assert.Equal(["layer:0", "layer:1", "layer:2"], g.Parts.Select(p => p.Key!).ToArray());
        Assert.Equal(80 * 0.06, Vol(f, id, "layer:0"), 6);
        Assert.Equal(80 * 0.04, Vol(f, id, "layer:1"), 6);
        Assert.Equal(80 * 0.20, Vol(f, id, "layer:2"), 6);
        var b0 = Measure.Bounds(g.Parts[0].Solid)!.Value;
        var b2 = Measure.Bounds(g.Parts[2].Solid)!.Value;
        Assert.Equal(1.5, b0.Max.Z, 9);
        Assert.Equal(1.44, b0.Min.Z, 9);
        Assert.Equal(1.5 - 0.30, b2.Min.Z, 9);
        Assert.Equal("Concrete pavers", f.Model.Get(g.Parts[0].Material!)!.Name);
        Assert.Equal(80, Q(f, id, "Lapin_HardscapeQuantities", "Area").Value, 9);
        Assert.Equal(0.30, Q(f, id, "Lapin_HardscapeQuantities", "Depth").Value, 9);
        Assert.Equal(24, Q(f, id, "Lapin_HardscapeQuantities", "Volume").Value, 9);
        Assert.Equal(16, Q(f, id, "Lapin_HardscapeLayerQuantities", "Volume", "layer:2").Value, 9);
        // Laid as a floor is: its usage through its thickness, from its top down.
        var usage = Layers.ReadUsage(LayerSets.UsageOf(f.Model, id)!)!;
        Assert.True(usage.Negative);
        Assert.Equal("AXIS3", LayerSets.UsageOf(f.Model, id)!.Str("layerSetDirection"));
        // The type changed: every hardscape of it follows.
        f.Run("civil.updateHardscapeType", new JsonObject { ["id"] = type.Id, ["layers"] = new JsonArray(new JsonObject { ["material"] = "Concrete pavers", ["thickness"] = 0.08 }, new JsonObject { ["material"] = "Crushed stone", ["thickness"] = 0.3 }) });
        Assert.Equal(80 * 0.38, Vol(f, id), 6);
        Assert.False(f.Model.Get(type.Id)!.Bool("indicative"));
        // The schedule lists it.
        var grid = new Schedule().Grid(f);
        Assert.Single(grid.Rows);
    }

    sealed class Schedule
    {
        public ScheduleGrid Grid(Fixture f) => f.Session.Catalog.All<IScheduleSource>().First(s => s.Kind == "hardscape").Grid(new ModelQuery(f.Model));
    }

    [Fact]
    public void Its_top_is_level_a_plane_at_a_grade_or_by_its_corners_planar_or_triangulated()
    {
        var f = F();
        var id = Add(f, new JsonObject { ["points"] = Rect(0, 0, 10, 4), ["elevation"] = 1, ["storeyId"] = f.Storey });
        Assert.True(Hardscapes.Top(f.Model.Get(id)!).Planar);
        Assert.All(Hardscapes.Edge(f.Model.Get(id)!), p => Assert.Equal(1, p.Z, 12));
        // A plane falling 2 % towards +x from (0, 0) at 1.00: 0.80 at x = 10.
        f.Run("civil.updateHardscape", new JsonObject { ["id"] = id, ["grade"] = "2 %", ["direction"] = 0, ["origin"] = P(0, 0) });
        var top = Hardscapes.Top(f.Model.Get(id)!);
        Assert.Equal(TopSurface.Plane, f.Model.Get(id)!.Str("topMode"));
        Assert.Equal(1.0, top.At(new Vec2(0, 2)), 12);
        Assert.Equal(0.8, top.At(new Vec2(10, 2)), 12);
        Assert.Equal(0.8 - 0.06, Measure.Bounds(f.Session.Geometry(id)!.Parts[0].Solid)!.Value.Min.Z, 9);
        // A slope arrow from its high end to its low end, by its two heights: 1.20 at y = 4 down to 1.00 at y = 0, a fall of 5 %.
        f.Run("civil.updateHardscape", new JsonObject { ["id"] = id, ["arrow"] = new JsonObject { ["from"] = P(5, 4), ["to"] = P(5, 0), ["fromZ"] = 1.2, ["toZ"] = 1.0 } });
        top = Hardscapes.Top(f.Model.Get(id)!);
        Assert.Equal(0.05, f.Model.Get(id)!.Num("grade"), 12);
        Assert.Equal(1.2, top.At(new Vec2(0, 4)), 12);
        Assert.Equal(1.0, top.At(new Vec2(10, 0)), 12);
        // By its corners: three heights and the fourth in their plane, one plane.
        f.Run("civil.updateHardscape", new JsonObject { ["id"] = id, ["cornerZ"] = P(1.0, 1.1, 1.3, 1.2) });
        top = Hardscapes.Top(f.Model.Get(id)!);
        Assert.True(top.Planar);
        Assert.Null(top.Note);
        Assert.Equal(1.15, top.At(new Vec2(5, 2)), 12);
        // One corner off the plane: the top is triangulated, every corner exactly at its height, and it says so.
        f.Run("civil.updateHardscape", new JsonObject { ["id"] = id, ["cornerZ"] = P(1.0, 1.1, 1.5, 1.2) });
        var e = f.Model.Get(id)!;
        top = Hardscapes.Top(e);
        Assert.False(top.Planar);
        Assert.NotNull(top.Note);
        Assert.Equal([1.0, 1.1, 1.5, 1.2], top.CornerIndex.Select(i => top.Z[i]).ToArray());
        Assert.Contains(f.Session.Geometry(id)!.Diagnostics!, d => d.Contains("triangulated"));
        // The layers still the plan area times their thickness.
        Assert.Equal(40 * 0.06, Vol(f, id, "layer:0"), 6);
    }

    [Fact]
    public void An_arc_edge_takes_its_corners_heights_by_length_along_it()
    {
        var f = F();
        var id = Add(f, new JsonObject { ["points"] = Rect(0, 0, 10, 6), ["arcs"] = P(0, 0, 1.5, 0), ["topMode"] = "corners", ["cornerZ"] = P(1, 1, 2, 1.2), ["storeyId"] = f.Storey, ["standalone"] = true });
        var top = Hardscapes.Top(f.Model.Get(id)!);
        Assert.False(top.Planar);
        // The arc from (10, 6) at 2.00 to (0, 6) at 1.20: its middle at 1.60.
        var e0 = f.Model.Get(id)!;
        var arc = ArcOutline.Edge(Hardscapes.Corners(e0), Hardscapes.Rises(e0), 2);
        var m = arc.PointAt(arc.Length / 2);
        Assert.Equal(1.5, Math.Abs(m.Y - 6), 6);
        var mid = top.Points.Select((p, i) => (p, i)).OrderBy(x => (x.p - m).Length).First();
        Assert.True((mid.p - m).Length < 0.2);
        Assert.Equal(1.6, top.Z[mid.i], 1);
        // The area exactly, the arc as an arc.
        Assert.Equal(ArcOutline.AreaOf(f.Model.Get(id)!), Q(f, id, "Lapin_HardscapeQuantities", "Area").Value, 9);
        Assert.True(Math.Abs(Q(f, id, "Lapin_HardscapeQuantities", "Area").Value - 60) > 5);
    }

    [Fact]
    public void Cut_into_a_terrain_its_top_edges_are_breaklines_and_the_ground_under_it_goes()
    {
        var f = F();
        var t = Field(f, (x, y) => 0.05 * x + 0.02 * y);
        var before = Terrains.Points(f.Model.Get(t)!).Count;
        // A plaza at 1.00 between (10, 10) and (22, 18), over ground from 0.70 to 1.46.
        var id = Add(f, new JsonObject { ["points"] = Rect(10.3, 10.3, 22.1, 18.1), ["elevation"] = 1.0, ["type"] = "stoneSlabs" });
        Assert.Equal(t, Hardscapes.TerrainOf(f.Model, f.Model.Get(id)!)!.Id);
        var g = Terrains.Of(f.Model.Get(t)!);
        // The ground meets its top edges exactly, all round.
        var ring = Hardscapes.Edge(f.Model.Get(id)!);
        for (var k = 0; k < ring.Count; k++)
        {
            var (a, b) = (ring[k], ring[(k + 1) % ring.Count]);
            for (var s = 0.0; s <= 1 + 1e-12; s += 1.0 / 32)
            {
                var p = a + (b - a) * s;
                Assert.Equal(1.0, g.ElevationAt(p.X, p.Y)!.Value, 9);
            }
        }
        // The survey under it left out: inside, the ground is the plaza's top; outside, the survey stays.
        Assert.Equal(1.0, g.ElevationAt(16, 14)!.Value, 9);
        Assert.Equal(0.05 * 30 + 0.02 * 30, g.ElevationAt(30, 30)!.Value, 9);
        Assert.Equal(before, Terrains.Points(f.Model.Get(t)!).Count);
        Assert.Contains(f.Session.Geometry(t)!.Diagnostics!, d => d.Contains("under a hardscape"));
        // The terrain cut by its body: what it took out measured for it.
        var cut = f.Session.Quantities(t).First(s => s.Name == "Lapin_EarthworkQuantities" && s.Part == "pad:" + id).Quantities.First(q => q.Name == "CutVolume").Value;
        Assert.Equal(Vol(f, id), cut, 1);
        // Its contours stop at the outline.
        foreach (var item in f.Session.Geometry(t)!.Plan!.Where(i => i.Kind is "contour" or "contourMajor"))
            foreach (var p in item.Points) Assert.False(p.X > 10.4 && p.X < 22 && p.Y > 10.4 && p.Y < 18, $"a contour inside at {p}");
        // Raised, the ground follows; standalone, the terrain is the survey again.
        f.Run("civil.updateHardscape", new JsonObject { ["id"] = id, ["elevation"] = 1.3 });
        Assert.Equal(1.3, Terrains.Of(f.Model.Get(t)!).ElevationAt(10.3, 14)!.Value, 9);
        f.Run("civil.updateHardscape", new JsonObject { ["id"] = id, ["standalone"] = true });
        Assert.Null(f.Model.Get(t)!.Attrs[FeatureLines.Derived]);
        Assert.Equal(0.05 * 16 + 0.02 * 14, Terrains.Of(f.Model.Get(t)!).ElevationAt(16, 14)!.Value, 9);
        Assert.Null(Hardscapes.TerrainOf(f.Model, f.Model.Get(id)!));
    }

    [Fact]
    public void Standalone_it_needs_no_terrain_and_one_under_it_is_left_as_it_was()
    {
        var f = F();
        var id = Add(f, new JsonObject { ["points"] = Rect(0, 0, 3, 20), ["elevation"] = 0.15, ["storeyId"] = f.Storey, ["type"] = "asphalt" });
        Assert.Null(Hardscapes.TerrainOf(f.Model, f.Model.Get(id)!));
        Assert.Equal("FLEXIBLE", f.Model.Get(id)!.PredefinedType);
        Assert.Equal(60 * 0.35, Vol(f, id), 6);
        var g = F();
        var t = Field(g, (x, _) => 0.1 * x);
        var tris = Terrains.Of(g.Model.Get(t)!).Tin.Triangles.ToList();
        Add(g, new JsonObject { ["points"] = Rect(5, 5, 9, 9), ["elevation"] = 2, ["standalone"] = true });
        Assert.Null(g.Model.Get(t)!.Attrs[FeatureLines.Derived]);
        Assert.Equal(tris, Terrains.Of(g.Model.Get(t)!).Tin.Triangles);
    }

    [Fact]
    public void A_kerb_runs_along_the_edges_chosen_and_drops_to_its_reveal_where_asked()
    {
        var f = F();
        var id = Add(f, new JsonObject { ["points"] = Rect(0, 0, 10, 6), ["elevation"] = 1, ["storeyId"] = f.Storey });
        // Kerbs on the south and east edges, the east one dropped flush (a 0 cm reveal at a crossing).
        var r = f.Run("civil.setHardscapeKerb", new JsonObject { ["id"] = id, ["edges"] = new JsonArray(true, true, false, false), ["drop"] = new JsonObject { ["edge"] = 1, ["reveal"] = 0 } })!;
        var k = (string)r["id"]!;
        var kerb = f.Model.Get(k)!;
        Assert.Equal(Kerbs.Class, kerb.Class);
        Assert.Equal(id, kerb.Str(Kerbs.HostKey));
        Assert.Equal(16, Q(f, k, "Lapin_KerbQuantities", "Length").Value, 9);
        Assert.Equal(6, Q(f, k, "Lapin_KerbQuantities", "DroppedLength").Value, 9);
        var g = f.Session.Geometry(k)!;
        Assert.Equal(2, g.Parts.Count);
        var south = Measure.Bounds(g.Parts.Single(p => p.Key == "kerb:0").Solid)!.Value;
        var east = Measure.Bounds(g.Parts.Single(p => p.Key == "kerb:1").Solid)!.Value;
        Assert.Equal(1.12, south.Max.Z, 9);
        Assert.Equal(1 - 0.25, south.Min.Z, 9);
        Assert.Equal(1.0, east.Max.Z, 9);
        // Outside the outline: the south one below y = 0, 15 cm wide; the two meet on the corner's bisector.
        Assert.Equal(-0.15, south.Min.Y, 9);
        Assert.Equal(10.15, south.Max.X, 9);
        Assert.Equal(10.15, east.Max.X, 9);
        // Volumes: the south run's section (0.15 × 0.37) over its mitred length (10 m plus half the corner square).
        Assert.Equal(0.15 * 0.37 * (10 + 0.15 / 2), Measure.Volume(g.Parts.Single(p => p.Key == "kerb:0").Solid), 5);
        // The whole perimeter, the rounded shape: one loop.
        f.Run("civil.setHardscapeKerb", new JsonObject { ["id"] = id, ["all"] = true, ["drops"] = new JsonArray(), ["shape"] = "rounded" });
        Assert.Equal(32, Q(f, k, "Lapin_KerbQuantities", "Length").Value, 9);
        Assert.Single(f.Session.Geometry(k)!.Parts);
        // On a sloped hardscape it follows the top.
        f.Run("civil.updateHardscape", new JsonObject { ["id"] = id, ["grade"] = 0.05, ["direction"] = 0, ["origin"] = P(0, 0) });
        var b = Measure.Bounds(f.Session.Geometry(k)!.Parts[0].Solid)!.Value;
        Assert.Equal(1.12, b.Max.Z, 9);
        Assert.Equal(1 - 0.05 * 10 - 0.25, b.Min.Z, 9);
        // Deleted with its hardscape; none: removed.
        f.Run("civil.setHardscapeKerb", new JsonObject { ["id"] = id, ["on"] = false });
        Assert.Null(f.Model.Get(k));
        var k2 = (string)f.Run("civil.setHardscapeKerb", new JsonObject { ["id"] = id })!["id"]!;
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(id) });
        Assert.Null(f.Model.Get(k2));
    }

    [Fact]
    public void A_subregion_becomes_a_hardscape_with_its_outline_and_area_and_goes()
    {
        var f = F();
        var t = Field(f, (x, y) => 0.02 * x + 0.01 * y);
        f.Run("civil.addSubregion", new JsonObject { ["terrainId"] = t, ["points"] = Rect(4.5, 4.5, 12.5, 9.5), ["material"] = "Gravel" });
        var sr = Terrains.SubregionsOf(f.Model.Get(t)!).Single();
        var r = f.Run("civil.subregionToHardscape", new JsonObject { ["terrainId"] = t, ["subregionId"] = sr.Id })!;
        var id = (string)r["id"]!;
        var e = f.Model.Get(id)!;
        Assert.Empty(Terrains.SubregionsOf(f.Model.Get(t)!));
        Assert.Equal(40, Hardscapes.Area(e), 9);
        Assert.Equal(sr.Name, e.Name);
        Assert.Equal("Gravel", f.Model.Get(Hardscapes.LayersOf(f.Model, e)[0].Material)!.Name);
        // Its corners where the ground was (a plane: the ground is one there), cut into the terrain.
        var top = Hardscapes.Top(e);
        Assert.True(top.Planar);
        Assert.Equal(0.02 * 4.5 + 0.01 * 4.5, top.Z[top.CornerIndex[0]], 6);
        Assert.Equal(t, Hardscapes.TerrainOf(f.Model, e)!.Id);
        // One step: undone, the subregion is back and nothing else is there.
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
        Assert.Single(Terrains.SubregionsOf(f.Model.Get(t)!));
    }

    [Fact]
    public void Adding_one_with_its_kerb_is_one_undo_step_and_a_project_without_one_is_as_before()
    {
        var f = F();
        var t = Field(f, (x, _) => 0.03 * x);
        f.Run("civil.addSubregion", new JsonObject { ["terrainId"] = t, ["points"] = Rect(30, 30, 34, 34), ["material"] = "Gravel" });
        var path = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts([2, 20], [38, 20]) })!["id"]!;
        var tris = Terrains.Of(f.Model.Get(t)!).Tin.Triangles.ToList();
        var terrainVol = Vol(f, t);
        Assert.Null(f.Model.Get(t)!.Attrs[FeatureLines.Derived]);
        var r = f.Run("civil.addHardscape", new JsonObject { ["points"] = Rect(5, 5, 15, 12), ["kerb"] = new JsonObject { ["all"] = true } })!;
        Assert.NotNull(r["kerbId"]);
        Assert.NotNull(f.Model.Get(t)!.Attrs[FeatureLines.Derived]);
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(Hardscapes.Class));
        Assert.Empty(f.Model.OfClass(Kerbs.Class));
        Assert.Null(f.Model.Get(t)!.Attrs[FeatureLines.Derived]);
        Assert.Equal(tris, Terrains.Of(f.Model.Get(t)!).Tin.Triangles);
        Assert.Equal(terrainVol, Vol(f, t), 6);
        Assert.NotNull(f.Session.Geometry(path));
    }

    [Fact]
    public void Moved_and_mirrored_it_keeps_its_top_and_its_kerb_s_edges()
    {
        var f = F();
        var id = Add(f, new JsonObject { ["points"] = Rect(0, 0, 10, 6), ["cornerZ"] = P(1, 1.1, 1.3, 1.2), ["storeyId"] = f.Storey });
        var k = (string)f.Run("civil.setHardscapeKerb", new JsonObject { ["id"] = id, ["edges"] = new JsonArray(true, false, false, false) })!["id"]!;
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = P(5, 1) });
        var top = Hardscapes.Top(f.Model.Get(id)!);
        Assert.Equal(1.3, top.At(new Vec2(15, 7)), 9);
        Assert.Equal(-0.15 + 1, Measure.Bounds(f.Session.Geometry(k)!.Parts[0].Solid)!.Value.Min.Y, 9);
        // Mirrored across x = 20: still anticlockwise, the corner at 1.30 now at (25, 7) → (15, 7) mirrored to (25, 7); the kerb on the same edge.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "mirror", ["a"] = P(20, 0), ["b"] = P(20, 10) });
        var e = f.Model.Get(id)!;
        Assert.True(Polygon2.SignedArea(Hardscapes.Corners(e)) > 0);
        top = Hardscapes.Top(e);
        Assert.Equal(1.3, top.At(new Vec2(25, 7)), 9);
        Assert.Equal(1.0, top.At(new Vec2(35, 1)), 9);
        var kb = Measure.Bounds(f.Session.Geometry(k)!.Parts[0].Solid)!.Value;
        Assert.Equal(1 - 0.15, kb.Min.Y, 9);
        Assert.Equal(10, Q(f, k, "Lapin_KerbQuantities", "Length").Value, 9);
    }

    [Fact]
    public void A_plan_writes_its_level_or_its_fall_and_hatches_its_paving()
    {
        var f = F();
        Add(f, new JsonObject { ["points"] = Rect(0, 0, 10, 6), ["elevation"] = 1.25, ["type"] = "stoneSlabs", ["storeyId"] = f.Storey });
        Add(f, new JsonObject { ["points"] = Rect(0, 10, 20, 12.4), ["grade"] = "2 %", ["direction"] = 270, ["elevation"] = 1, ["storeyId"] = f.Storey });
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
        var pens = d["pens"]!.AsArray().OfType<JsonObject>().ToList();
        var texts = pens.Where(p => p["texts"] != null).SelectMany(p => p["texts"]!.AsArray().OfType<JsonObject>()).Select(x => (string)x["text"]!).ToList();
        Assert.Contains(texts, x => x.Contains("+1.25"));
        Assert.Contains(texts, x => x.Contains("2.0 %"));
        Assert.Contains(pens, p => (string?)p["layer"] == CivilModule.HardscapeLayer);
    }

    [Fact]
    public void The_ready_made_pavings_hatch_their_top_in_plan_and_a_material_the_project_has_keeps_its_graphics()
    {
        // A new project: concrete pavers in their 200 × 100 bond, asphalt stippled, grass pavers' blocks, crushed stone cut as gravel.
        var f = F();
        var pavers = Add(f, new JsonObject { ["points"] = Rect(0, 0, 4, 2), ["type"] = "pavers", ["elevation"] = 0, ["storeyId"] = f.Storey });
        Add(f, new JsonObject { ["points"] = Rect(10, 0, 14, 2), ["type"] = "asphalt", ["elevation"] = 0, ["storeyId"] = f.Storey });
        Add(f, new JsonObject { ["points"] = Rect(20, 0, 24, 2), ["type"] = "grassPavers", ["elevation"] = 0, ["storeyId"] = f.Storey });
        string? Look(string name, string kind) => (string?)Materials.Find(f.Model, name)!.Attrs[kind]?["hatch"];
        Assert.Equal("PAVER_200X100", Look("Concrete pavers", "elevation"));
        Assert.Equal("ASPHALT", Look("Asphalt wearing course", "elevation"));
        Assert.Equal("GRASS_PAVER_600X400", Look("Grass pavers", "elevation"));
        Assert.Equal("GRAVEL", Look("Crushed stone", "section"));
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        List<(double X0, double Y0, double X1, double Y1)> Hatch(Fixture fx, string v) => [.. ((JsonObject)fx.Session.Query("view.drawing", new JsonObject { ["id"] = v })!)["pens"]!.AsArray().OfType<JsonObject>()
            .Where(p => (string?)p["layer"] == CivilModule.HardscapeLayer && p["segments"] is JsonArray)
            .SelectMany(p => p["segments"]!.AsArray().Select(x => (double)x!).Chunk(4).Select(c => (c[0], c[1], c[2], c[3])))];
        var lines = Hatch(f, view);
        // The pavers: a course every 100 mm across the 2 m, joints every 200 mm along them, all inside the outline.
        var mine = lines.Where(l => Math.Max(l.X0, l.X1) <= 4 + 1e-6).ToList();
        Assert.InRange(mine.Count(l => Math.Abs(l.Y0 - l.Y1) < 1e-6 && Math.Abs(l.X1 - l.X0) > 3.9), 19, 21);
        Assert.True(mine.Count(l => Math.Abs(l.X0 - l.X1) < 1e-6) >= 19 * 19 / 2, $"{mine.Count} pavers' joints");
        Assert.Contains(lines, l => l.X0 > 9.9 && l.X0 < 14.1);
        Assert.Contains(lines, l => l.X0 > 19.9 && l.X0 < 24.1);
        // A project that has its own „Concrete pavers” already, with graphics of its own: they stay.
        var g = F();
        var own = g.Model.Get((string)g.Run("material.create", new JsonObject { ["name"] = "Concrete pavers" })!["id"]!)!;
        g.Run("material.update", new JsonObject { ["id"] = own.Id, ["elevation"] = new JsonObject { ["hatch"] = "TILE_300X300" } });
        var before = g.Model.Get(own.Id)!.Attrs.ToJsonString();
        Add(g, new JsonObject { ["points"] = Rect(0, 0, 4, 2), ["type"] = "pavers", ["elevation"] = 0, ["storeyId"] = g.Storey });
        Assert.Equal(before, g.Model.Get(own.Id)!.Attrs.ToJsonString());
        Assert.Equal(own.Id, Hardscapes.LayersOf(g.Model, g.Model.Get(g.Model.OfClass(Hardscapes.Class).Single().Id)!)[0].Material);
        Assert.Single(g.Model.OfClass(IfcClass.Material), x => x.Name == "Concrete pavers");
    }

    [Fact]
    public void A_sloped_sidewalk_over_an_even_survey_is_cut_into_the_ground_and_a_flat_corner_is_kept_by_the_triangulation()
    {
        // A face with three corners in a line (a hardscape's edge fanning to a row of survey points on a plane): two triangles,
        // the middle corner kept, so the terrain's body stays closed for the booleans.
        var pts = new List<Vec3> { new(46, 30, 0.912), new(32, 28, 1.412), new(34, 28, 1.441), new(36, 28, 1.47) };
        Assert.Equal(2, Triangulator.Face(pts, [0, 1, 2, 3], null, VecMath.NewellNormal(pts).Normalized()).Count);
        var f = F();
        var lines = new List<string>();
        for (var x = 0; x <= 50; x += 2) for (var y = 0; y <= 36; y += 2) lines.Add(FormattableString.Invariant($"{x} {y} {100 + 0.03 * x + 0.01 * y + 0.35 * Math.Exp(-((x - 24) * (x - 24) + (y - 20) * (y - 20)) / 180.0):0.000}"));
        var t = (string)f.Run("civil.addTerrain", new JsonObject { ["text"] = string.Join("\n", lines), ["datum"] = 100, ["storeyId"] = f.Storey })!["id"]!;
        var w = Add(f, new JsonObject { ["points"] = Rect(4, 30, 46, 32.4), ["type"] = "stoneSlabs", ["grade"] = "2 %", ["direction"] = 270, ["origin"] = P(4, 32.4), ["elevation"] = 0.93 });
        var g = f.Session.Geometry(t)!;
        Assert.DoesNotContain(g.Diagnostics ?? [], d => d.Contains("could not be cut"));
        Assert.True(f.Session.Quantities(t).First(s => s.Name == "Lapin_EarthworkQuantities" && s.Part == "pad:" + w).Quantities.First(q => q.Name == "CutVolume").Value > 1);
    }

    [Fact]
    public void The_ground_round_a_hardscape_draws_no_stray_lines_in_3D()
    {
        // The user guide's survey (falling 3 % and 1 %, a swell in the middle), a plaza and a long sidewalk cut into it. The ground
        // fanned from each edge's two corners in long slivers whose folds passed the 3D view's crease angle: stray lines out of the
        // corners, 20 m long. Its edges are given the ground with a point every metre, and the ground's coarser copies draw its
        // edges: what it draws on its top is the cut round each hardscape and the folds of the survey, none longer than a survey diagonal.
        var f = F();
        static double G(double x, double y) => 0.03 * x + 0.01 * y + 0.35 * Math.Exp(-((x - 24) * (x - 24) + (y - 20) * (y - 20)) / 180.0);
        var lines = new List<string>();
        for (var x = 0; x <= 50; x += 2) for (var y = 0; y <= 36; y += 2) lines.Add(FormattableString.Invariant($"{x} {y} {100 + G(x, y):0.000}"));
        var t = (string)f.Run("civil.addTerrain", new JsonObject { ["text"] = string.Join("\n", lines), ["datum"] = 100, ["storeyId"] = f.Storey, ["interval"] = 0.25 })!["id"]!;
        Add(f, new JsonObject { ["points"] = Rect(14, 14, 30, 26), ["type"] = "pavers", ["elevation"] = 0.95, ["kerb"] = new JsonObject { ["all"] = true } });
        Add(f, new JsonObject { ["points"] = Rect(4, 30, 46, 32.4), ["type"] = "stoneSlabs", ["grade"] = "2 %", ["direction"] = 270, ["origin"] = P(4, 32.4), ["elevation"] = 0.5 + G(4, 32.4) });
        var outlines = new[] { new List<Vec2> { new(14, 14), new(30, 14), new(30, 26), new(14, 26) }, new List<Vec2> { new(4, 30), new(46, 30), new(46, 32.4), new(4, 32.4) } };
        static double Off(List<Vec2> poly, Vec2 q) => Enumerable.Range(0, poly.Count).Min(i =>
        {
            var (a, b) = (poly[i], poly[(i + 1) % poly.Count]);
            var k = Math.Clamp(Vec2.Dot(q - a, b - a) / Vec2.Dot(b - a, b - a), 0, 1);
            return (q - (a + (b - a) * k)).Length;
        });
        // What the 3D view is sent: the ground's mesh and its coarser copies for the distance, which draw its own edges (theirs
        // drew their slivers' folds, past their 40°, out of the kerb's corners).
        var ground = f.Session.Snapshot().Render.Single(r => r.NodeId == t).Parts!.Single(p => p.Key == "ground");
        Assert.NotNull(ground.Lods);
        foreach (var (edges, level) in new[] { (ground.Edges, -1) }.Concat(ground.Lods!.Select((l, k) => (l.Edges, k))))
        {
            var drawn = 0;
            for (var i = 0; i < edges.Length; i += 6)
            {
                var (a, b) = (new Vec2(edges[i], edges[i + 1]), new Vec2(edges[i + 3], edges[i + 4]));
                var mid = (a + b) * 0.5;
                // The terrain's sides, and the cut round each hardscape, are folds it has.
                if (mid.X < 1e-3 || mid.Y < 1e-3 || mid.X > 50 - 1e-3 || mid.Y > 36 - 1e-3 || outlines.Min(o => Off(o, mid)) < 0.3) continue;
                drawn++;
                Assert.True((b - a).Length < 2.9, $"level {level}: a stray line from {a} to {b}");
            }
            Assert.True(drawn < 40, $"level {level}: {drawn} lines on the ground's top");
        }
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC4X3")]
    public void It_goes_to_IFC_as_a_pavement_with_its_layers_its_kerb_as_a_kerb_and_comes_back(string schema)
    {
        var f = F();
        var t = Field(f, (x, _) => 0.02 * x);
        var id = Add(f, new JsonObject { ["points"] = Rect(5, 5, 15, 12), ["type"] = "pavers", ["grade"] = 0.02, ["direction"] = 90, ["kerb"] = new JsonObject { ["all"] = true } });
        var kerb = Kerbs.Of(f.Model, id)!.Id;
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "plaza.ifc", new IfcExportOptions(Schema: schema));
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        if (schema == "IFC4X3")
        {
            Assert.Contains("IFCPAVEMENT(", text);
            Assert.Contains("IFCPAVEMENTTYPE(", text);
            Assert.Contains("IFCKERB(", text);
        }
        else
        {
            Assert.DoesNotContain("IFCPAVEMENT(", text);
            Assert.Contains("'Pavement'", text);
            Assert.Contains("'Kerb'", text);
            Assert.DoesNotContain("IFCKERB(", text);
        }
        Assert.Contains(".AXIS3.", text);
        Assert.Contains("IFCMATERIALLAYERSETUSAGE(", text);
        Assert.Contains("'Concrete pavers'", text);
        Assert.DoesNotContain("'Pset_Lapin", text);
        // The kerb connected to its hardscape (IfcRelConnectsElements, the hardscape relating), besides its intent.
        string Line(string gid) => text.Split('\n').First(l => l.Contains('(') && l[(l.IndexOf('(') + 1)..].StartsWith($"'{gid}'"));
        string Ref(string line) => line[..line.IndexOf('=')];
        var rel = text.Split('\n').Single(l => l.Contains("IFCRELCONNECTSELEMENTS(") && l.Contains("'Kerb'"));
        Assert.EndsWith($",{Ref(Line(id))},{Ref(Line(kerb))});", rel.Trim());
        var g = F();
        IfcImport.Run(g.Session, ms.ToArray(), "plaza.ifc", null, out _);
        var back = g.Model.Get(id);
        Assert.NotNull(back);
        Assert.Equal(Hardscapes.Class, back!.Class);
        Assert.Equal(Hardscapes.Corners(f.Model.Get(id)!), Hardscapes.Corners(back));
        Assert.Equal(0.02, back.Num("grade"), 12);
        Assert.Equal(3, Hardscapes.LayersOf(g.Model, back).Count);
        Assert.Equal(t, Hardscapes.TerrainOf(g.Model, back)?.Id);
        Assert.Equal(id, g.Model.Get(kerb)?.Str(Kerbs.HostKey));
        Assert.Equal(Vol(f, id), Vol(g, id), 6);
        // A file whose kerb's intent does not name its hardscape: the relationship does.
        var bare = text.Replace("IFCPROPERTYSINGLEVALUE('hardscape',", "IFCPROPERTYSINGLEVALUE('was',");
        Assert.NotEqual(text, bare);
        var h = F();
        IfcImport.Run(h.Session, System.Text.Encoding.UTF8.GetBytes(bare), "plaza.ifc", null, out _);
        Assert.Equal(id, h.Model.Get(kerb)?.Str(Kerbs.HostKey));
        Assert.Equal(Kerbs.Of(f.Model, id)!.Id, Kerbs.Of(h.Model, id)?.Id);
    }
}
