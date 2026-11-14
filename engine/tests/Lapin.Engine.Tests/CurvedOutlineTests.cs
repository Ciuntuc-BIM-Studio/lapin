using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Curved edges on outlines (forum topic 97, stage 2): a floor's, a floor opening's, a ceiling's edges may be arcs, each its
/// rise off its chord in <c>arcs</c> beside the corners (left of the edge positive, as a wall's <c>arc</c>); the body, plans
/// and sections lay the arcs in chords by the one rule; areas and perimeters are exact; corners are rounded (a fillet) or cut
/// (a chamfer) in one undo step; IFC carries the true curve as the FootPrint (IfcIndexedPolyCurve with IfcArcIndex).
/// </summary>
public sealed class CurvedOutlineTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-curved-outlines").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);
    static JsonArray Nums(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static double Q(Fixture f, string id, string name) => f.Session.Quantities(id)[0].Quantities.Single(x => x.Name == name).Value;
    const double Thick = 0.22;

    static string Rect(Fixture f, double w = 6, double h = 4, JsonArray? arcs = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (w, 0), (w, h), (0, h)) };
        if (arcs != null) a["arcs"] = arcs;
        return (string)f.Run("architecture.addSlab", a)!["id"]!;
    }

    [Fact]
    public void The_exact_area_and_perimeter_of_an_outline_with_arcs()
    {
        var (c, r) = ArcOutline.Circle(new Vec2(1, 2), 3);
        Assert.Equal(Math.PI * 9, ArcOutline.SignedArea(c, r), 9);
        Assert.Equal(2 * Math.PI * 3, ArcOutline.Perimeter(c, r), 9);
        // Its chords no more than a millimetre off the circle, by the one rule.
        var pts = ArcOutline.Points(c, r);
        Assert.True(pts.Count >= 24);
        Assert.All(pts, p => Assert.Equal(3, (p - new Vec2(1, 2)).Length, 9));
        for (var i = 0; i < pts.Count; i++) Assert.True(3 - ((pts[i] + pts[(i + 1) % pts.Count]) * 0.5 - new Vec2(1, 2)).Length <= Chords.Tolerance + 1e-9);
        // Run the other way round, the same outline.
        var back = c.AsEnumerable().Reverse().ToList();
        Assert.Equal(-Math.PI * 9, ArcOutline.SignedArea(back, ArcOutline.Reversed(r)), 9);
    }

    [Fact]
    public void A_floor_with_a_curved_edge_is_built_along_its_arc_and_measured_exactly()
    {
        var f = new Fixture();
        // The top edge (from corner 2 to 3, running −x) bulging up by 1 m: to the right of its way, so negative.
        var slab = Rect(f, arcs: Nums(0, 0, -1, 0));
        var e = f.Model.Get(slab)!;
        Assert.Equal(4, Slabs.Corners(e).Count);
        Assert.True(Slabs.Contour(e).Count > 8);
        // The segment of a chord of 6 rising 1: r = 5, sweep 2·asin(0.6).
        var (r, t) = (5.0, 2 * Math.Asin(0.6));
        var segment = r * r / 2 * (t - Math.Sin(t));
        Assert.Equal(24 + segment, Q(f, slab, "GrossArea"), 9);
        Assert.Equal(4 + 4 + 6 + r * t, Q(f, slab, "Perimeter"), 9);
        Assert.Equal((24 + segment) * Thick, Vol(f, slab), 2);
        Assert.True(Math.Abs(Vol(f, slab) - (24 + segment) * Thick) / ((24 + segment) * Thick) < 0.002);
        // The plan's outline goes up to the arc.
        Assert.InRange(f.Session.Geometry(slab)!.Plan![0].Points.Max(p => p.Y), 5 - Chords.Tolerance, 5 + 1e-9);
        // Drawn clockwise, the same floor.
        var cw = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 4), (6, 4), (6, 0), (0, 0)), ["arcs"] = Nums(1, 0, 0, 0) })!["id"]!;
        Assert.Equal(24 + segment, Q(f, cw, "GrossArea"), 9);
        Assert.InRange(f.Session.Geometry(cw)!.Plan![0].Points.Max(p => p.Y), 5 - Chords.Tolerance, 5 + 1e-9);
    }

    [Fact]
    public void A_floor_saved_before_has_no_arcs_and_reads_unchanged()
    {
        var f = new Fixture();
        var slab = Rect(f);
        var e = f.Model.Get(slab)!;
        Assert.Null(e.Attrs[ArcOutline.Key]);
        Assert.Null(Slabs.Rises(e));
        Assert.Equal(Slabs.Corners(e), Slabs.Contour(e));
        // All straight given: none kept.
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)), ["arcs"] = Nums(0, 0, 0, 0) });
        Assert.Null(f.Model.Get(slab)!.Attrs[ArcOutline.Key]);
    }

    [Fact]
    public void An_edge_bent_and_straightened_is_one_undo_step_each_and_corners_moved_keep_their_arcs()
    {
        var f = new Fixture();
        var slab = Rect(f);
        f.Run("architecture.setEdgeArc", new JsonObject { ["id"] = slab, ["index"] = 0, ["rise"] = -0.5 });
        Assert.True(Q(f, slab, "GrossArea") > 24);
        // A corner moved (as many corners): the arcs stay.
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = Pts((0, 0), (7, 0), (6, 4), (0, 4)) });
        Assert.Equal(-0.5, Slabs.Rises(f.Model.Get(slab)!)![0], 9);
        // A corner taken out without arcs given: straight.
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = Pts((0, 0), (7, 0), (0, 4)) });
        Assert.Null(Slabs.Rises(f.Model.Get(slab)!));
        f.Session.Undo();
        f.Session.Undo();
        f.Run("architecture.setEdgeArc", new JsonObject { ["id"] = slab, ["index"] = 0, ["rise"] = 0 });
        Assert.Null(f.Model.Get(slab)!.Attrs[ArcOutline.Key]);
        Assert.Equal(24, Q(f, slab, "GrossArea"), 9);
        f.Session.Undo();
        Assert.NotNull(Slabs.Rises(f.Model.Get(slab)!));
        f.Session.Undo();
        Assert.Null(f.Model.Get(slab)!.Attrs[ArcOutline.Key]);
        // An arc that makes the outline cross itself is refused.
        Assert.Throws<CommandException>(() => f.Run("architecture.setEdgeArc", new JsonObject { ["id"] = slab, ["index"] = 0, ["rise"] = 5 }));
    }

    [Fact]
    public void A_corner_filleted_or_chamfered_takes_off_what_it_should()
    {
        var f = new Fixture();
        var slab = Rect(f);
        f.Run("architecture.roundCorner", new JsonObject { ["id"] = slab, ["index"] = 1, ["radius"] = 1 });
        var e = f.Model.Get(slab)!;
        Assert.Equal(5, Slabs.Corners(e).Count);
        Assert.True((Slabs.Corners(e)[1] - new Vec2(5, 0)).Length < 1e-9);
        Assert.True((Slabs.Corners(e)[2] - new Vec2(6, 1)).Length < 1e-9);
        // A quarter circle in place of the corner's square: 1 − π/4 taken off; the perimeter 2 shorter, π/2 longer.
        Assert.Equal(24 - (1 - Math.PI / 4), Q(f, slab, "GrossArea"), 6);
        Assert.Equal(20 - 2 + Math.PI / 2, Q(f, slab, "Perimeter"), 6);
        // The arc is tangent to both edges: every point of it a radius from (5, 1).
        Assert.All(Slabs.Contour(e).Where(p => p.X > 5 + 1e-9 && p.Y < 1 - 1e-9), p => Assert.Equal(1, (p - new Vec2(5, 1)).Length, 6));
        // A fillet is one undo step.
        f.Session.Undo();
        Assert.Equal(4, Slabs.Corners(f.Model.Get(slab)!).Count);
        f.Run("architecture.chamferCorner", new JsonObject { ["id"] = slab, ["index"] = 0, ["distance"] = 1 });
        Assert.Equal(23.5, Q(f, slab, "GrossArea"), 9);
        // Too large for its edges, or on a curved edge: refused.
        Assert.Throws<CommandException>(() => f.Run("architecture.roundCorner", new JsonObject { ["id"] = slab, ["index"] = 3, ["radius"] = 10 }));
        f.Run("architecture.roundCorner", new JsonObject { ["id"] = slab, ["index"] = 3, ["radius"] = 0.5 });
        var n = Slabs.Corners(f.Model.Get(slab)!).Count;
        Assert.Equal(6, n);
        Assert.Throws<CommandException>(() => f.Run("architecture.roundCorner", new JsonObject { ["id"] = slab, ["index"] = 4, ["radius"] = 0.2 }));
    }

    [Fact]
    public void A_round_floor_from_a_circle_and_an_opening_with_arcs_in_it()
    {
        var f = new Fixture();
        var (c, r) = ArcOutline.Circle(new Vec2(0, 0), 4);
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts([.. c.Select(p => (p.X, p.Y))]), ["arcs"] = Nums(r) })!["id"]!;
        Assert.Equal(Math.PI * 16, Q(f, slab, "GrossArea"), 6);
        var (hc, hr) = ArcOutline.Circle(new Vec2(1, 0), 1);
        var hole = (string)f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["points"] = Pts([.. hc.Select(p => (p.X, p.Y))]), ["arcs"] = Nums(hr) })!["id"]!;
        Assert.Equal(Math.PI * 15 * Thick, Vol(f, slab), 1);
        Assert.True(Math.Abs(Vol(f, slab) - Math.PI * 15 * Thick) < 0.01);
        // Moved and mirrored, the floor and its opening go along, their arcs still bulging out.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(slab), ["op"] = "mirror", ["a"] = new JsonArray(10, 0), ["b"] = new JsonArray(10, 1) });
        var e = f.Model.Get(slab)!;
        Assert.Equal(Math.PI * 16, Q(f, slab, "GrossArea"), 6);
        Assert.All(Slabs.Contour(e), p => Assert.Equal(4, (p - new Vec2(20, 0)).Length, 6));
        Assert.All(Slabs.Contour(f.Model.Get(hole)!), p => Assert.Equal(1, (p - new Vec2(19, 0)).Length, 6));
        // The opening's own edge bent the same way.
        f.Run("architecture.setEdgeArc", new JsonObject { ["id"] = hole, ["index"] = 0, ["rise"] = 0 });
        Assert.True(Vol(f, slab) > Math.PI * 15 * Thick);
    }

    [Fact]
    public void A_layer_extent_at_a_curved_edge_follows_its_arc()
    {
        var f = new Fixture();
        var slab = Rect(f, arcs: Nums(0, 0, -1, 0));
        var finish = f.Session.Geometry(slab)!.Parts.Single(p => p.Key == "layer:0");
        var before = Measure.Volume(finish.Solid);
        // The finish (20 mm) 100 mm out past the curved edge: the arc's length (r·θ) by 0.1 more, near enough.
        f.Run("architecture.setLayerExtent", new JsonObject { ["id"] = slab, ["layer"] = 0, ["edge"] = "edge", ["index"] = 2, ["offset"] = 0.1 });
        var after = f.Session.Geometry(slab)!.Parts.Where(p => p.Key == "layer:0").Sum(p => Measure.Volume(p.Solid));
        var arc = 5 * 2 * Math.Asin(0.6);
        Assert.Equal(arc * 0.1 * 0.02, after - before, 3);
        // Its grip runs along the arc.
        var edges = LayerExtents.Edges(f.Model.Get(slab)!, f.Model, f.Session.Catalog)!;
        var grip = edges["grips"]!.AsArray().OfType<JsonObject>().Single(g => (int)g["layer"]! == 0 && (int?)g["index"] == 2);
        Assert.True(grip["line"]!.AsArray().Count > 3);
    }

    [Fact]
    public void A_ceiling_takes_arcs_and_is_measured_exactly()
    {
        var f = new Fixture();
        var (c, r) = ArcOutline.Circle(new Vec2(2, 2), 2);
        var id = (string)f.Run("architecture.addCeiling", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts([.. c.Select(p => (p.X, p.Y))]), ["arcs"] = Nums(r), ["height"] = 2.6 })!["id"]!;
        var q = f.Session.Quantities(id).Single(x => x.Name == "Qto_CoveringBaseQuantities");
        Assert.Equal(Math.PI * 4, q.Quantities.Single(x => x.Name == "GrossArea").Value, 6);
        // A corner between arcs is not cut; its edges straightened, it is.
        Assert.Contains("Only a corner between two straight edges", Assert.Throws<CommandException>(() => f.Run("architecture.chamferCorner", new JsonObject { ["id"] = id, ["index"] = 0, ["distance"] = 0.2 })).Message);
        f.Run("architecture.setEdgeArc", new JsonObject { ["id"] = id, ["index"] = 3, ["rise"] = 0 });
        f.Run("architecture.setEdgeArc", new JsonObject { ["id"] = id, ["index"] = 0, ["rise"] = 0 });
        f.Run("architecture.chamferCorner", new JsonObject { ["id"] = id, ["index"] = 0, ["distance"] = 0.2 });
        Assert.Equal(5, Slabs.Corners(f.Model.Get(id)!).Count);
    }

    [Fact]
    public void A_floor_inside_walls_from_a_click()
    {
        var f = new Fixture();
        f.Wall(0, 0, 6, 0); f.Wall(6, 0, 6, 4); f.Wall(6, 4, 0, 4); f.Wall(0, 4, 0, 0);
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 3, ["y"] = 2 })!["id"]!;
        var area = Q(f, slab, "GrossArea");
        Assert.True(area > 15 && area < 24, $"{area}");
        Assert.Throws<CommandException>(() => f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 30, ["y"] = 2 }));
    }

    [Fact]
    public void Ifc_carries_the_true_curve_and_reads_back_the_arcs()
    {
        var f = new Fixture();
        var slab = Rect(f, arcs: Nums(0, 0, -1, 0));
        var gid = f.Model.Get(slab)!.Id;
        var path = Path.Combine(_dir, "curved.ifc");
        IfcExport.Write(f.Session, path);
        var step = StepFile.Read(path);
        Assert.Single(step.OfType("IFCINDEXEDPOLYCURVE"));
        Assert.Contains("IFCARCINDEX((3,5,4))", File.ReadAllText(path));
        Assert.Contains("IFCLINEINDEX((1,2))", File.ReadAllText(path));
        var g = new Fixture();
        IfcImport.Run(g.Session, path, null, out _);
        var back = g.Model.Get(gid)!;
        Assert.Equal(-1, Slabs.Rises(back)![2], 6);
        Assert.Equal(Q(f, slab, "GrossArea"), Q(g, gid, "GrossArea"), 6);
    }
}
