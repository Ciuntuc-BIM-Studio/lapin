using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// An outline with every edge offset at once (Archicad's "Offset all edges"; the grips' "Offset outline…"): straight edges
/// parallel and mitred, arcs concentric, a corner where an arc meets an edge rounded when growing and trimmed when shrinking,
/// what cannot be offset refused with its reason (ArcOutline.Offset, the query outline.offset).
/// </summary>
public sealed class OutlineOffsetTests
{
    static List<Vec2> Square(double s) => [new(0, 0), new(s, 0), new(s, s), new(0, s)];
    static void Near(Vec2 want, Vec2 got) { Assert.Equal(want.X, got.X, 9); Assert.Equal(want.Y, got.Y, 9); }

    [Fact]
    public void A_square_grows_and_shrinks_with_mitred_corners()
    {
        var (c, r, from) = ArcOutline.Offset(Square(4), null, 0.5);
        Assert.Null(r);
        Assert.Equal([0, 1, 2, 3], from);
        Near(new(-0.5, -0.5), c[0]); Near(new(4.5, -0.5), c[1]); Near(new(4.5, 4.5), c[2]); Near(new(-0.5, 4.5), c[3]);
        var (s, _, _) = ArcOutline.Offset(Square(4), null, -0.5);
        Near(new(0.5, 0.5), s[0]); Near(new(3.5, 3.5), s[2]);
        Assert.Equal(9, ArcOutline.SignedArea(s, null), 9);
    }

    [Fact]
    public void Outward_is_outward_whichever_way_the_outline_runs()
    {
        var cw = Square(4);
        cw.Reverse();
        var (c, _, _) = ArcOutline.Offset(cw, null, 0.5);
        // It keeps its way round (clockwise) and its corners' order, grown.
        Assert.Equal(-25, ArcOutline.SignedArea(c, null), 9);
        Near(new(-0.5, 4.5), c[0]);
    }

    [Fact]
    public void An_L_shape_keeps_its_inner_corner_mitred()
    {
        List<Vec2> l = [new(0, 0), new(4, 0), new(4, 2), new(2, 2), new(2, 4), new(0, 4)];
        var (c, _, _) = ArcOutline.Offset(l, null, 0.25);
        Assert.Equal(6, c.Count);
        Near(new(2.25, 2.25), c[3]);
        Near(new(4.25, 2.25), c[2]);
    }

    [Fact]
    public void A_circle_offsets_to_a_concentric_circle()
    {
        var (c0, r0) = ArcOutline.Circle(new Vec2(1, 2), 2);
        var (c, r, _) = ArcOutline.Offset(c0, r0, 0.5);
        Assert.Equal(4, c.Count);
        Assert.All(c, p => Assert.Equal(2.5, (p - new Vec2(1, 2)).Length, 9));
        Assert.Equal(Math.PI * 2.5 * 2.5, ArcOutline.SignedArea(c, r), 9);
        var (s, rs, _) = ArcOutline.Offset(c0, r0, -1.5);
        Assert.Equal(Math.PI * 0.25, ArcOutline.SignedArea(s, rs), 9);
        // Inward past its radius there is nothing left.
        var e = Assert.Throws<CommandException>(() => ArcOutline.Offset(c0, r0, -2.5));
        Assert.Contains("to nothing", e.Message);
    }

    [Fact]
    public void A_filleted_corner_stays_tangent_its_arc_concentric()
    {
        // A convex outline grown by d gains its perimeter times d and π d² (tangent edges): its fillet's radius grows by d.
        var (c0, r0) = ArcOutline.Fillet(Square(4), null, 2, 1);
        var (c, r, from) = ArcOutline.Offset(c0, r0, 0.3);
        Assert.Equal(c0.Count, c.Count);
        var want = ArcOutline.SignedArea(c0, r0) + ArcOutline.Perimeter(c0, r0) * 0.3;
        // Three mitred corners add their squares beyond the quarter circles: 3 × (1 − π/4) d².
        Assert.Equal(want + Math.PI * 0.09 + 3 * (1 - Math.PI / 4) * 0.09, ArcOutline.SignedArea(c, r), 9);
        var arc = ArcOutline.Edge(c, r, 2);
        Assert.Equal(1.3, arc.Radius, 9);
        Assert.Equal(Enumerable.Range(0, c0.Count), from);
    }

    [Fact]
    public void Where_an_arc_meets_an_edge_at_a_corner_growing_rounds_it_and_shrinking_trims_it()
    {
        // A 6 × 4 rectangle whose top edge bulges out by 1 m: it meets its sides at an angle.
        List<Vec2> pts = [new(0, 0), new(6, 0), new(6, 4), new(0, 4)];
        double[] rises = [0, 0, -1, 0];
        var (c, r, from) = ArcOutline.Offset(pts, rises, 0.5);
        // Two corners more, each a round of 0.5 m about the old corner.
        Assert.Equal(6, c.Count);
        Assert.NotNull(r);
        Assert.Equal([0, 1, 1, 2, 2, 3], from);
        var round = ArcOutline.Edge(c, r, 2);
        Assert.True(round.IsArc);
        Assert.Equal(0.5, round.Radius, 9);
        Assert.Equal(0.5, (c[2] - new Vec2(6, 4)).Length, 9);
        Assert.Equal(0.5, (c[3] - new Vec2(6, 4)).Length, 9);
        var top = ArcOutline.Edge(c, r, 3);
        Assert.Equal(ArcOutline.Edge(pts, rises, 2).Radius + 0.5, top.Radius, 9);
        Assert.Equal(ArcOutline.Edge(pts, rises, 2).Centre.X, top.Centre.X, 9);
        Assert.Equal(ArcOutline.Edge(pts, rises, 2).Centre.Y, top.Centre.Y, 9);

        var (s, sr, _) = ArcOutline.Offset(pts, rises, -0.5);
        Assert.Equal(4, s.Count);
        // The side trimmed where it crosses the smaller arc.
        var shrunk = ArcOutline.Edge(s, sr, 2);
        Assert.Equal(ArcOutline.Edge(pts, rises, 2).Radius - 0.5, shrunk.Radius, 9);
        Assert.Equal(shrunk.Radius, (s[2] - shrunk.Centre).Length, 9);
        Assert.Equal(5.5, s[2].X, 9);
    }

    [Fact]
    public void What_would_vanish_or_cross_itself_is_refused()
    {
        Assert.Contains("vanish", Assert.Throws<CommandException>(() => ArcOutline.Offset(Square(2), null, -1)).Message);
        Assert.Contains("vanish", Assert.Throws<CommandException>(() => ArcOutline.Offset(Square(2), null, -1.5)).Message);
        // A thin notch: its short edge vanishes as the outline grows (its sides close on it).
        List<Vec2> notch = [new(0, 0), new(6, 0), new(6, 4), new(3.2, 4), new(3.2, 1), new(2.8, 1), new(2.8, 4), new(0, 4)];
        Assert.Throws<CommandException>(() => ArcOutline.Offset(notch, null, 0.3));
        Assert.Throws<CommandException>(() => ArcOutline.Offset(Square(2), null, double.NaN));
        // Nothing to offset: the same outline.
        var (same, _, _) = ArcOutline.Offset(Square(2), null, 0);
        Assert.Equal(Square(2), same);
    }

    [Fact]
    public void The_query_offsets_without_changing_the_model_and_tells_a_problem()
    {
        var f = new Fixture();
        var before = f.Model.Entities.Count();
        var r = (JsonObject)f.Session.Query("outline.offset", new JsonObject
        {
            ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(4, 0), new JsonArray(4, 4), new JsonArray(0, 4)),
            ["distance"] = 0.25,
        })!;
        Assert.Equal(-0.25, (double)r["points"]![0]![0]!, 9);
        Assert.Null(r["arcs"]);
        Assert.Equal(4, ((JsonArray)r["from"]!).Count);
        Assert.Equal(before, f.Model.Entities.Count());
        var bad = (JsonObject)f.Session.Query("outline.offset", new JsonObject
        {
            ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(1, 0), new JsonArray(1, 1), new JsonArray(0, 1)),
            ["distance"] = -0.6,
        })!;
        Assert.Contains("vanish", (string)bad["problem"]!);
    }

    [Fact]
    public void A_floor_offset_through_its_update_is_one_undo_step()
    {
        var f = new Fixture();
        var id = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 4), new JsonArray(0, 4)),
            ["arcs"] = new JsonArray(0, 0, -1, 0),
        })!["id"]!;
        var slab = f.Model.Get(id)!;
        var r = (JsonObject)f.Session.Query("outline.offset", new JsonObject { ["points"] = slab.Attrs["contour"]!.DeepClone(), ["arcs"] = slab.Attrs["arcs"]!.DeepClone(), ["distance"] = 0.5 })!;
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = id, ["points"] = r["points"]!.DeepClone(), ["arcs"] = r["arcs"]!.DeepClone() });
        var grown = f.Model.Get(id)!;
        Assert.Equal(6, ((JsonArray)grown.Attrs["contour"]!).Count);
        f.Session.Undo();
        Assert.Equal(4, ((JsonArray)f.Model.Get(id)!.Attrs["contour"]!).Count);
    }

    /// <summary>How far a point is from the ellipse: sampled round it, then narrowed down about the nearest sample.</summary>
    static double FromEllipse(EllipseEdge e, Vec2 p)
    {
        const int N = 2048;
        var h = 2 * Math.PI / N;
        var t = Enumerable.Range(0, N).Select(k => k * h).MinBy(x => (e.At(x) - p).Length);
        var (lo, hi) = (t - h, t + h);
        for (var k = 0; k < 100; k++)
        {
            var (m1, m2) = (lo + (hi - lo) / 3, hi - (hi - lo) / 3);
            if ((e.At(m1) - p).Length < (e.At(m2) - p).Length) hi = m2; else lo = m1;
        }
        return (e.At((lo + hi) / 2) - p).Length;
    }

    [Fact]
    public void An_ellipse_offsets_to_the_curve_at_that_distance_laid_by_the_one_rule()
    {
        // An ellipse (audit S4): a 3 × 1.5 m one, four quarter edges. The curve at a distance from it is no ellipse: its offset
        // is laid in chords no more than the one rule's tolerance off it, its corners on it, every edge straight.
        var (corners, edges) = EllipseEdge.Whole(new Vec2(1, 2), new Vec2(3, 0), 0.5);
        var e = edges[0]!;
        foreach (var d in new[] { 0.5, -0.4 })
        {
            var (c, r, from) = ArcOutline.Offset(corners, null, d, edges);
            Assert.True(c.Count > 40);
            Assert.Null(r);
            Assert.Equal(c.Count, from.Count);
            Assert.All(from, k => Assert.InRange(k, 0, 3));
            Assert.All(c, p => Assert.Equal(Math.Abs(d), FromEllipse(e, p), 6));
            for (var i = 0; i < c.Count; i++)
                Assert.InRange(FromEllipse(e, (c[i] + c[(i + 1) % c.Count]) * 0.5) - Math.Abs(d), -Chords.Tolerance, Chords.Tolerance);
            // A convex outline grown (or shrunk) by d gains (loses) its perimeter times d, and π d².
            var want = ArcOutline.SignedArea(corners, null, edges) + ArcOutline.Perimeter(corners, null, edges) * d + Math.PI * d * d;
            Assert.InRange(ArcOutline.SignedArea(c, null) - want, -0.02, 0.02);
        }
        // Inward as far as its tightest radius of curvature (b²/a = 0.75 m) it would fold on itself.
        Assert.Throws<CommandException>(() => ArcOutline.Offset(corners, null, -0.8, edges));
    }

    [Fact]
    public void A_half_ellipse_closed_by_a_straight_edge_offsets_both()
    {
        var (whole, edges) = EllipseEdge.Whole(new Vec2(0, 0), new Vec2(3, 0), 0.5);
        List<Vec2> half = [whole[0], whole[1], whole[2]];
        EllipseEdge?[] el = [edges[0], edges[1], null];
        var (c, _, from) = ArcOutline.Offset(half, null, 0.25, el);
        // The straight edge (from (−3, 0) to (3, 0)) moved down by 0.25, mitred with the curve's ends.
        var k = from.IndexOf(2);
        Assert.Equal(-0.25, c[k].Y, 9);
        Assert.Equal(-0.25, c[(k + 1) % c.Count].Y, 9);
        Assert.Equal(-3.25, c[k].X, 6);
        Assert.Equal(3.25, c[(k + 1) % c.Count].X, 6);
    }

    [Fact]
    public void An_elliptical_floor_is_offset_by_the_query_and_written_by_its_update_one_undo_step()
    {
        var f = new Fixture();
        var (corners, edges) = EllipseEdge.Whole(new Vec2(0, 0), new Vec2(3, 0), 0.5);
        var id = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray([.. corners.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]),
            ["ellipses"] = new JsonArray([.. edges.Select(x => (JsonNode?)x!.ToJson())]),
        })!["id"]!;
        var slab = f.Model.Get(id)!;
        var area = ArcOutline.AreaOf(slab);
        var r = (JsonObject)f.Session.Query("outline.offset", new JsonObject
        {
            ["points"] = slab.Attrs["contour"]!.DeepClone(), ["ellipses"] = slab.Attrs["ellipses"]!.DeepClone(), ["distance"] = 0.5,
        })!;
        Assert.Null(r["problem"]);
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = id, ["points"] = r["points"]!.DeepClone(), ["arcs"] = r["arcs"]?.DeepClone() });
        var grown = f.Model.Get(id)!;
        Assert.Null(grown.Attrs["ellipses"]);
        Assert.InRange(ArcOutline.AreaOf(grown) - (area + ArcOutline.PerimeterOf(slab) * 0.5 + Math.PI * 0.25), -0.02, 0.02);
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(id)!.Attrs["ellipses"]);
    }
}
