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
}
