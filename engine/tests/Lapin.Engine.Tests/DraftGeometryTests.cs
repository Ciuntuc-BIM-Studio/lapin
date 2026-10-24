using BubbleBim.Geometry;
using Lapin.Drafting.Cad;

namespace Lapin.Engine.Tests;

/// <summary>Drafted shapes' geometry and edits, ported from the old Lapin's geometry.test.ts and edit-ops.test.ts (same cases and numbers).</summary>
public class DraftGeometryTests
{
    const double E = 1e-7;
    static void Near(double a, double b) => Assert.True(Math.Abs(a - b) < E, $"expected {b}, got {a}");
    static void NearPt(Vec2 p, Vec2 q) { Near(p.X, q.X); Near(p.Y, q.Y); }
    static bool HasPt(IEnumerable<Vec2> list, Vec2 q) => list.Any(p => (p - q).Length < 1e-6);
    static Vec2 P(double x, double y) => new(x, y);
    static Line L(Vec2 a, Vec2 b) => new(a, b);
    const double H = Math.PI / 2;

    // ------------------------------------------------------------ vec

    [Fact]
    public void Normalises_angles_and_tests_ccw_sweeps_with_wrap_around()
    {
        Near(Geo.NormAngle(-H), 3 * H);
        Near(Geo.NormAngle(5 * Math.PI), Math.PI);
        Assert.True(Geo.AngleInArc(0, 3 * H, H));
        Assert.False(Geo.AngleInArc(Math.PI, 3 * H, H));
        Assert.True(Geo.AngleInArc(H, 0, H));
    }

    [Fact]
    public void Rotates_mirrors_and_normalises_safely()
    {
        NearPt(Geo.Rotate(P(2, 1), P(1, 1), H), P(1, 2));
        NearPt(Geo.Mirror(P(1, 2), P(0, 0), P(1, 0)), P(1, -2));
        NearPt(Geo.Mirror(P(2, 0), P(0, 0), P(1, 1)), P(0, 2));
        NearPt(Geo.Norm(P(0, 0)), P(0, 0));
    }

    // ------------------------------------------------------------ curves

    [Fact]
    public void Decomposes_shapes_into_primitives()
    {
        Assert.Equal(3, Geo.Prims(new Polyline([P(0, 0), P(1, 0), P(1, 1)], true)).Count);
        Assert.Equal(2, Geo.Prims(new Polyline([P(0, 0), P(1, 0), P(1, 1)], false)).Count);
        var c = Geo.Prims(new Circle(P(0, 0), 1));
        Assert.Single(c);
        Assert.True(c[0] is ArcP { Full: true });
    }

    [Fact]
    public void Computes_exact_arc_bounds_including_in_sweep_quadrants()
    {
        var b = Geo.Bounds(new Arc(P(0, 0), 2, 0, Math.PI));
        NearPt(b.Min, P(-2, 0));
        NearPt(b.Max, P(2, 2));
        var w = Geo.Bounds(new Arc(P(0, 0), 1, -0.5, 0.5));
        Near(w.Max.X, 1);
        Near(w.Min.X, Math.Cos(0.5));
    }

    [Fact]
    public void Finds_the_nearest_point_clamping_to_arc_ends()
    {
        var arc = new Arc(P(0, 0), 1, 0, H);
        NearPt(Geo.Nearest(arc, P(2, 2)).Point, P(Math.Sqrt(0.5), Math.Sqrt(0.5)));
        NearPt(Geo.Nearest(arc, P(-1, -0.1)).Point, P(0, 1));
        Near(Geo.Nearest(L(P(0, 0), P(10, 0)), P(5, 3)).Dist, 3);
    }

    [Fact]
    public void Lists_key_points_per_shape_type()
    {
        var arc = Geo.Keys(new Arc(P(0, 0), 1, 0, Math.PI));
        Assert.Equal(2, arc.Endpoints.Count);
        NearPt(arc.Midpoints[0], P(0, 1));
        Assert.Equal(3, arc.Quadrants.Count);
        var circ = Geo.Keys(new Circle(P(1, 1), 1));
        Assert.Equal(4, circ.Quadrants.Count);
        NearPt(circ.Centers[0], P(1, 1));
        var pl = Geo.Keys(new Polyline([P(0, 0), P(2, 0), P(2, 2)], true));
        Assert.Equal(3, pl.Endpoints.Count);
        Assert.Equal(3, pl.Midpoints.Count);
    }

    // ------------------------------------------------------------ intersect

    static SegP Seg(Vec2 a, Vec2 b) => new(a, b);
    static ArcP A(Vec2 c, double r, double s, double e, bool full = false) => new(c, r, s, e, full);

    [Fact]
    public void Segment_segment_crossing_miss_touching_end_collinear_overlap()
    {
        var x = Geo.Intersect(Seg(P(0, 0), P(2, 2)), Seg(P(0, 2), P(2, 0)));
        Assert.Single(x);
        NearPt(x[0], P(1, 1));
        Assert.Empty(Geo.Intersect(Seg(P(0, 0), P(1, 0)), Seg(P(2, -1), P(2, 1))));
        Assert.Single(Geo.Intersect(Seg(P(0, 0), P(1, 0)), Seg(P(1, 0), P(1, 1))));
        var ov = Geo.Intersect(Seg(P(0, 0), P(3, 0)), Seg(P(1, 0), P(5, 0)));
        Assert.Equal(2, ov.Count);
        Assert.True(HasPt(ov, P(1, 0)) && HasPt(ov, P(3, 0)));
    }

    [Fact]
    public void Segment_arc_respects_the_sweep_and_handles_tangency()
    {
        var upper = A(P(0, 0), 1, 0, Math.PI);
        Assert.Equal(2, Geo.Intersect(Seg(P(-2, 0.5), P(2, 0.5)), upper).Count);
        Assert.Empty(Geo.Intersect(Seg(P(-2, -0.5), P(2, -0.5)), upper));
        var tan = Geo.Intersect(Seg(P(-2, 1), P(2, 1)), upper);
        Assert.Single(tan);
        NearPt(tan[0], P(0, 1));
        Assert.Empty(Geo.Intersect(Seg(P(0, 0), P(0.5, 0)), A(P(0, 0), 1, 0, 0, true)));
    }

    [Fact]
    public void Arc_arc_two_points_tangency_concentric_outside_sweep()
    {
        var a = A(P(0, 0), 1, 0, 0, true);
        var b = A(P(1, 0), 1, 0, 0, true);
        var x = Geo.Intersect(a, b);
        Assert.Equal(2, x.Count);
        Assert.True(HasPt(x, P(0.5, Math.Sqrt(3) / 2)));
        var t = Geo.Intersect(a, A(P(2, 0), 1, 0, 0, true));
        Assert.Single(t);
        NearPt(t[0], P(1, 0));
        Assert.Empty(Geo.Intersect(a, A(P(0, 0), 2, 0, 0, true)));
        Assert.Empty(Geo.Intersect(a, A(P(0, 0), 1, 0, 0, true)));
        Assert.Single(Geo.Intersect(A(P(0, 0), 1, 0, Math.PI), b));
    }

    [Fact]
    public void Shape_level_intersections_and_rays()
    {
        var sq = new Polyline([P(0, 0), P(2, 0), P(2, 2), P(0, 2)], true);
        Assert.Equal(2, Geo.Intersect(sq, L(P(-1, 1), P(3, 1))).Count);
        var hits = Geo.Ray(P(-5, 1), P(1, 0), sq);
        Assert.Equal(2, hits.Count);
        Near(hits[0].T, 5);
        Near(hits[1].T, 7);
        Assert.Empty(Geo.Ray(P(5, 1), P(1, 0), sq));
        var c = Geo.Ray(P(0, 0), P(1, 0), new Circle(P(0, 0), 3));
        Assert.Single(c);
        Near(c[0].T, 3);
    }

    // ------------------------------------------------------------ transform

    [Fact]
    public void Translates_rotates_and_scales()
    {
        var t = (Line)Geo.Translate(L(P(0, 0), P(1, 0)), P(1, 2));
        NearPt(t.B, P(2, 2));
        var r = (Arc)Geo.Rotate(new Arc(P(0, 0), 1, 0, H), P(0, 0), H);
        Near(r.Start, H);
        Near(r.End, Math.PI);
        var s = (Circle)Geo.Scale(new Circle(P(1, 0), 1), P(0, 0), 2);
        Near(s.R, 2);
        NearPt(s.C, P(2, 0));
    }

    [Fact]
    public void Mirroring_an_arc_keeps_the_same_geometric_point_set()
    {
        var arc = new Arc(P(1, 1), 2, 0.2, 1.4);
        var (a, b) = (P(0, 0), P(1, 3));
        var m = Geo.Mirror(arc, a, b);
        List<Vec2> Pts(Shape s) { var k = Geo.Keys(s); return [.. k.Endpoints, .. k.Midpoints]; }
        foreach (var p in Pts(arc).Select(p => Geo.Mirror(p, a, b))) Assert.True(HasPt(Pts(m), p));
        NearPt(Geo.Keys(m).Midpoints[0], Geo.Mirror(Geo.Keys(arc).Midpoints[0], a, b));
    }

    // ------------------------------------------------------------ offset

    [Fact]
    public void Offsets_a_line_toward_the_side_point()
    {
        var up = (Line)Ops.Offset(L(P(0, 0), P(4, 0)), 1, P(2, 5))!;
        NearPt(up.A, P(0, 1));
        NearPt(up.B, P(4, 1));
        var down = (Line)Ops.Offset(L(P(0, 0), P(4, 0)), 1, P(2, -5))!;
        Near(down.A.Y, -1);
    }

    [Fact]
    public void Offsets_circles_and_arcs_refusing_a_collapsed_radius()
    {
        Near(((Circle)Ops.Offset(new Circle(P(0, 0), 2), 0.5, P(5, 0))!).R, 2.5);
        var inn = (Arc)Ops.Offset(new Arc(P(0, 0), 2, 0, H), 0.5, P(0.5, 0.5))!;
        Near(inn.R, 1.5);
        Near(inn.End, H);
        Assert.Null(Ops.Offset(new Circle(P(0, 0), 1), 2, P(0.1, 0)));
    }

    [Fact]
    public void Offsets_an_open_L_polyline_with_a_miter_join()
    {
        var o = (Polyline)Ops.Offset(new Polyline([P(0, 0), P(4, 0), P(4, 4)], false), 1, P(2, 1))!;
        Assert.Equal(3, o.Pts.Count);
        NearPt(o.Pts[0], P(0, 1));
        NearPt(o.Pts[1], P(3, 1));
        NearPt(o.Pts[2], P(3, 4));
    }

    [Fact]
    public void Offsets_closed_polylines_inward_and_outward_regardless_of_winding()
    {
        var cw = new Polyline([P(0, 0), P(0, 4), P(4, 4), P(4, 0)], true);
        var inward = (Polyline)Ops.Offset(cw, 1, P(2, 2))!;
        Assert.True(inward.Closed);
        Assert.Equal(4, inward.Pts.Count);
        foreach (var p in inward.Pts) { Near(Math.Abs(p.X - 2), 1); Near(Math.Abs(p.Y - 2), 1); }
        var outward = (Polyline)Ops.Offset(cw, 1, P(10, 2))!;
        Assert.True(HasPt(outward.Pts, P(-1, -1)));
        Assert.True(HasPt(outward.Pts, P(5, 5)));
    }

    [Fact]
    public void Joins_collinear_consecutive_segments_without_a_spurious_corner()
    {
        var o = (Polyline)Ops.Offset(new Polyline([P(0, 0), P(2, 0), P(4, 0)], false), 1, P(1, 1))!;
        NearPt(o.Pts[1], P(2, 1));
        NearPt(o.Pts[2], P(4, 1));
    }

    // ------------------------------------------------------------ trim

    static readonly Shape[] Cutters = [L(P(2, -1), P(2, 1)), L(P(6, -1), P(6, 1))];

    [Fact]
    public void Trim_removes_the_middle_of_a_line_between_two_cutters()
    {
        var target = L(P(0, 0), P(10, 0));
        var r = Ops.Trim(target, [target, .. Cutters], P(4, 0.1))!;
        Assert.Equal(2, r.Count);
        var (a, b) = ((Line)r[0], (Line)r[1]);
        NearPt(a.A, P(0, 0)); NearPt(a.B, P(2, 0)); NearPt(b.A, P(6, 0)); NearPt(b.B, P(10, 0));
    }

    [Fact]
    public void Trim_removes_an_end_piece_and_returns_null_without_intersections()
    {
        var r = Ops.Trim(L(P(0, 0), P(10, 0)), Cutters, P(9, 0))!;
        Assert.Equal(2, r.Count);
        NearPt(((Line)r[1]).A, P(2, 0));
        NearPt(((Line)r[1]).B, P(6, 0));
        Assert.Null(Ops.Trim(L(P(0, 5), P(10, 5)), Cutters, P(1, 5)));
    }

    [Fact]
    public void Trim_turns_a_cut_circle_into_the_remaining_arc()
    {
        var circle = new Circle(P(0, 0), 1);
        var r = Ops.Trim(circle, [L(P(0, -2), P(0, 2))], P(1, 0))!;
        var arc = Assert.IsType<Arc>(Assert.Single(r));
        Near(arc.Start, H);
        Near(arc.End, 3 * H);
        NearPt(Geo.Keys(arc).Midpoints[0], P(-1, 0));
        Assert.Null(Ops.Trim(circle, [L(P(0, 0), P(0, 2))], P(1, 0)));
    }

    [Fact]
    public void Trim_opens_a_closed_polyline_cut_at_two_points()
    {
        var sq = new Polyline([P(0, 0), P(4, 0), P(4, 4), P(0, 4)], true);
        var pl = Assert.IsType<Polyline>(Assert.Single(Ops.Trim(sq, [L(P(2, -1), P(2, 5))], P(4, 2))!));
        Assert.False(pl.Closed);
        Assert.Equal(4, pl.Pts.Count);
        NearPt(pl.Pts[0], P(2, 4));
        NearPt(pl.Pts[1], P(0, 4));
        NearPt(pl.Pts[3], P(2, 0));
    }

    [Fact]
    public void Trims_an_arc()
    {
        var r = Assert.IsType<Arc>(Assert.Single(Ops.Trim(new Arc(P(0, 0), 1, 0, Math.PI), [L(P(0, 0), P(0, 2))], P(1, 0.1))!));
        Near(r.Start, H);
        Near(r.End, Math.PI);
    }

    // ------------------------------------------------------------ extend

    static readonly Line Wall = L(P(10, -5), P(10, 5));

    [Fact]
    public void Extends_the_line_end_nearest_the_pick_to_the_boundary()
    {
        var r = (Line)Ops.Extend(L(P(0, 0), P(4, 0)), [Wall, L(P(20, -5), P(20, 5))], P(3.9, 0))!;
        NearPt(r.A, P(0, 0));
        NearPt(r.B, P(10, 0));
        Assert.Null(Ops.Extend(L(P(0, 0), P(4, 0)), [Wall], P(0.1, 0)));
    }

    [Fact]
    public void Extends_the_last_segment_of_an_open_polyline()
    {
        var r = (Polyline)Ops.Extend(new Polyline([P(0, 0), P(0, 2), P(3, 2)], false), [Wall], P(3, 2))!;
        NearPt(r.Pts[2], P(10, 2));
    }

    [Fact]
    public void Extends_an_arc_along_its_circle()
    {
        var r = (Arc)Ops.Extend(new Arc(P(0, 0), 1, 0, H / 2), [L(P(-2, 0.5), P(0, 0.5))], P(0.7, 0.7))!;
        Near(r.Start, 0);
        Near(r.End, 5 * Math.PI / 6);
    }

    // ------------------------------------------------------------ fillet and chamfer

    static readonly Line Hz = L(P(0, 0), P(10, 0));
    static readonly Line Vt = L(P(5, -5), P(5, 5));

    [Fact]
    public void Fillets_perpendicular_lines_with_r_1()
    {
        var f = Ops.Fillet(Hz, P(1, 0), Vt, P(5, 4), 1)!.Value;
        NearPt(f.First.A, P(0, 0)); NearPt(f.First.B, P(4, 0));
        NearPt(f.Second.A, P(5, 1)); NearPt(f.Second.B, P(5, 5));
        NearPt(f.Arc!.C, P(4, 1));
        Near(f.Arc.R, 1);
        var k = Geo.Keys(f.Arc);
        Assert.True(HasPt(k.Endpoints, P(4, 0)));
        Assert.True(HasPt(k.Endpoints, P(5, 1)));
        Near((k.Midpoints[0] - P(5, 0)).Length, Math.Sqrt(2) - 1);
    }

    [Fact]
    public void Radius_0_makes_a_corner_parallel_or_oversize_returns_null()
    {
        var f = Ops.Fillet(L(P(0, 0), P(4, 0)), P(1, 0), L(P(5, 1), P(5, 5)), P(5, 4), 0)!.Value;
        Assert.Null(f.Arc);
        NearPt(f.First.B, P(5, 0));
        NearPt(f.Second.A, P(5, 0));
        Assert.Null(Ops.Fillet(Hz, P(1, 0), L(P(0, 1), P(9, 1)), P(1, 1), 1));
        Assert.Null(Ops.Fillet(Hz, P(1, 0), Vt, P(5, 4), 50));
    }

    [Fact]
    public void Chamfers_with_two_distances()
    {
        var c = Ops.Chamfer(Hz, P(1, 0), Vt, P(5, 4), 1, 2)!.Value;
        NearPt(c.Line.A, P(4, 0));
        NearPt(c.Line.B, P(5, 2));
    }

    // ------------------------------------------------------------ measure (measure.test.ts)

    [Fact]
    public void Measures_lengths_and_areas()
    {
        Near(Geo.Length(L(P(0, 0), P(3, 4))), 5);
        var rect = new Polyline([P(0, 0), P(3, 0), P(3, 2), P(0, 2)], true);
        Near(Geo.Length(rect), 10);
        Near(Geo.Area(rect)!.Value, 6);
        Near(Geo.Area(new Circle(P(0, 0), 1))!.Value, Math.PI);
        var arc = new Arc(P(0, 0), 2, 0, H);
        Near(Geo.Length(arc), Math.PI);
        Near(arc.Sweep * 180 / Math.PI, 90);
    }
}
