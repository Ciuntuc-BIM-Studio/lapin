using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The join geometry of docs/transfer.md ("Îmbinări de pereți"), checked
/// analytically: the body a join leaves on each wall must be exactly the
/// wall's band clipped by the line that ends it (the other wall's face, the
/// mitre diagonal or the square cut), and the two bodies must not overlap.
/// Areas come from convex polygon clipping, so a gap or a spur of any size
/// fails.
/// </summary>
public class WallJoinTests
{
    const double Deg = Math.PI / 180;
    static Vec2 Towards(Vec2 from, double degrees, double distance) =>
        new(from.X + distance * Math.Cos(degrees * Deg), from.Y + distance * Math.Sin(degrees * Deg));
    static JoinWall Straight(Vec2 start, Vec2 end, double thickness, string alignment = "center", double offset = 0) =>
        JoinWall.FromThickness(start, end, thickness, alignment, offset);

    // ------------------------------------------------------------ polygon tools (independent of the join code)

    /// <summary>The body as a plan polygon, counter-clockwise, from its outline.</summary>
    static Vec2[] Body(JoinWall w)
    {
        var o = WallJoins.Outline(w);
        var along = (w.End - w.Start).Normalized();
        var left = along.Perp;
        return o.Corners.Select(c => w.Start + along * c.X + left * c.Y).ToArray();
    }

    static double Area(IReadOnlyList<Vec2> poly)
    {
        double twice = 0;
        for (var i = 0; i < poly.Count; i++) twice += Vec2.Cross(poly[i], poly[(i + 1) % poly.Count]);
        return Math.Abs(twice) / 2;
    }

    /// <summary>The part of a convex polygon on the left of the directed line <paramref name="p"/> → <paramref name="p"/> + <paramref name="d"/>.</summary>
    static List<Vec2> Keep(IReadOnlyList<Vec2> poly, Vec2 p, Vec2 d)
    {
        var kept = new List<Vec2>();
        for (var i = 0; i < poly.Count; i++)
        {
            var cur = poly[i];
            var nxt = poly[(i + 1) % poly.Count];
            var sc = Vec2.Cross(d, cur - p);
            var sn = Vec2.Cross(d, nxt - p);
            if (sc >= 0) kept.Add(cur);
            if ((sc >= 0) != (sn >= 0))
            {
                var t = sc / (sc - sn);
                kept.Add(cur + (nxt - cur) * t);
            }
        }
        return kept;
    }

    static List<Vec2> Ccw(IReadOnlyList<Vec2> poly)
    {
        double twice = 0;
        for (var i = 0; i < poly.Count; i++) twice += Vec2.Cross(poly[i], poly[(i + 1) % poly.Count]);
        return twice >= 0 ? poly.ToList() : poly.Reverse().ToList();
    }

    static double Overlap(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b)
    {
        var clip = Ccw(b);
        IReadOnlyList<Vec2> cur = Ccw(a);
        for (var i = 0; i < clip.Count && cur.Count > 0; i++)
            cur = Keep(cur, clip[i], clip[(i + 1) % clip.Count] - clip[i]);
        return cur.Count < 3 ? 0 : Area(cur);
    }

    /// <summary>The wall's band from its far tip to well past the joint, as drawn, before any join.</summary>
    static List<Vec2> Band(JoinWall w, Connection joinedEnd, double past = 3)
    {
        var along = (w.End - w.Start).Normalized();
        var left = along.Perp;
        var far = joinedEnd == Connection.AtStart ? w.End : w.Start;
        var outward = joinedEnd == Connection.AtEnd ? along : -along;
        var tip = (joinedEnd == Connection.AtStart ? w.Start : w.End) + outward * past;
        return Ccw([far + left * w.YMin, tip + left * w.YMin, tip + left * w.YMax, far + left * w.YMax]);
    }

    /// <summary>The face line of <paramref name="w"/> on the side of <paramref name="p"/> (near) or opposite it (far): a point and the axis direction.</summary>
    static (Vec2 P, Vec2 D) FaceOf(JoinWall w, Vec2 p, bool near)
    {
        var along = (w.End - w.Start).Normalized();
        var left = along.Perp;
        var onLeft = Vec2.Dot(p - w.Start, left) >= 0;
        var y = onLeft == near ? w.YMax : w.YMin;
        return (w.Start + left * y, along);
    }

    /// <summary>The side of the directed line through <paramref name="p"/> along <paramref name="d"/> that holds <paramref name="q"/>, as the line to clip with.</summary>
    static (Vec2 P, Vec2 D) SideHolding((Vec2 P, Vec2 D) line, Vec2 q) =>
        Vec2.Cross(line.D, q - line.P) >= 0 ? line : (line.P, -line.D);

    static void SameShape(IReadOnlyList<Vec2> got, IReadOnlyList<Vec2> want)
    {
        var common = Overlap(got, want);
        Assert.Equal(Area(want), Area(got), 9);
        Assert.Equal(Area(want), common, 9);
    }

    /// <summary>
    /// The join leaves no gap, no overlap and no spur: each body equals its own
    /// band clipped by the line that ends it, and the bodies are disjoint.
    /// </summary>
    static void Clean(WallJoin join, JoinWall a, JoinWall b)
    {
        var bodyA = Body(join.A.Wall);
        var bodyB = Body(join.B.Wall);
        Assert.Equal(0, Overlap(bodyA, bodyB), 9);
        var (first, second, w1, w2) = join.Relating == 'a' ? (join.A, join.B, a, b) : (join.B, join.A, b, a);
        var far1 = first.Connection == Connection.AtStart ? w1.End : w1.Start;
        var far2 = second.Connection == Connection.AtStart ? w2.End : w2.Start;
        var body1 = Body(first.Wall);
        var body2 = Body(second.Wall);
        switch (join.Kind)
        {
            case JoinKind.T:
                Assert.Equal(w1, first.Wall);
                var nearFace = SideHolding(FaceOf(w1, far2, near: true), far2);
                SameShape(body2, Keep(Band(w2, second.Connection), nearFace.P, nearFace.D));
                break;
            case JoinKind.L when join.Style == JoinStyle.Butt:
                var outer = SideHolding(FaceOf(w2, far1, near: false), far1);
                SameShape(body1, Keep(Band(w1, first.Connection), outer.P, outer.D));
                var near = SideHolding(FaceOf(w1, far2, near: true), far2);
                SameShape(body2, Keep(Band(w2, second.Connection), near.P, near.D));
                break;
            case JoinKind.L:
                // Mitre: the two cut edges are one and the same segment, from the inner corner to the outer corner.
                var cut1 = CutEdge(body1, first.Connection);
                var cut2 = CutEdge(body2, second.Connection);
                Assert.Equal(cut1.Length, cut2.Length, 9);
                Assert.Equal(0, Math.Min((cut1.P - cut2.P).Length + (cut1.Q - cut2.Q).Length, (cut1.P - cut2.Q).Length + (cut1.Q - cut2.P).Length), 9);
                break;
            default:
                var across = (join.Point, (w1.End - w1.Start).Normalized().Perp);
                var side1 = SideHolding(across, far1);
                var side2 = SideHolding(across, far2);
                SameShape(body1, Keep(Band(w1, first.Connection), side1.P, side1.D));
                SameShape(body2, Keep(Band(w2, second.Connection), side2.P, side2.D));
                break;
        }
    }

    /// <summary>The edge of the body at the joined end: right-end → left-end, or left-start → right-start.</summary>
    static (Vec2 P, Vec2 Q, double Length) CutEdge(Vec2[] body, Connection end)
    {
        var (p, q) = end == Connection.AtEnd ? (body[1], body[2]) : (body[3], body[0]);
        return (p, q, (q - p).Length);
    }

    // ------------------------------------------------------------ corners

    [Fact]
    public void A_square_corner_of_equal_walls_lets_the_first_run_to_the_outer_face_and_stops_the_second_at_the_near_face()
    {
        var up = Straight(new(1, 1), new(1, 6), 0.25);
        var right = Straight(new(1, 6), new(5, 6), 0.25);
        var join = WallJoins.Compute(up, right);
        Assert.Equal(JoinKind.L, join.Kind);
        Assert.Equal(JoinStyle.Butt, join.Style);
        Assert.Equal('a', join.Relating);
        Assert.Equal(new Vec2(1, 6), join.Point);
        Assert.True(join.A.RunsThrough);
        Assert.False(join.B.RunsThrough);
        Assert.Equal(Connection.AtEnd, join.A.Connection);
        Assert.Equal(Connection.AtStart, join.B.Connection);
        // The first wall goes 125 mm on, to the far face of the second; the second starts 125 mm in, at the first one's face.
        Assert.Equal(0.125, join.A.Wall.EndCut!.Value.Left, 9);
        Assert.Equal(0.125, join.A.Wall.EndCut!.Value.Right, 9);
        Assert.Equal(-0.125, join.B.Wall.StartCut!.Value.Left, 9);
        Assert.Equal(-0.125, join.B.Wall.StartCut!.Value.Right, 9);
        Assert.True(WallJoins.Outline(join.A.Wall).Rectangular);
        Assert.True(WallJoins.Outline(join.B.Wall).Rectangular);
        Clean(join, up, right);
    }

    [Fact]
    public void The_thicker_wall_runs_through_a_corner_unless_the_priority_says_otherwise()
    {
        var thin = Straight(new(0, 2), new(4, 2), 0.18);
        var thick = Straight(new(4, 2), new(4, -1), 0.36);
        Assert.Equal('b', WallJoins.Compute(thin, thick).Relating);
        Assert.True(WallJoins.Compute(thin, thick).B.RunsThrough);
        Clean(WallJoins.Compute(thin, thick), thin, thick);

        var forced = WallJoins.Compute(thin, thick, new JoinOptions(Priority: 'a'));
        Assert.Equal('a', forced.Relating);
        Assert.True(forced.A.RunsThrough);
        Assert.Equal(0.18, forced.A.Wall.EndCut!.Value.Right, 9);
        Clean(forced, thin, thick);
    }

    public static TheoryData<int, bool, bool> CornerCases()
    {
        var data = new TheoryData<int, bool, bool>();
        foreach (var angle in new[] { 25, 50, 75, 100, 130, 155 })
            foreach (var towardsA in new[] { true, false })
                foreach (var towardsB in new[] { true, false })
                    data.Add(angle, towardsA, towardsB);
        return data;
    }

    [Theory]
    [MemberData(nameof(CornerCases))]
    public void Corners_at_any_angle_are_clean_whichever_way_each_wall_is_drawn(int angle, bool towardsA, bool towardsB)
    {
        var corner = new Vec2(3, 2);
        var farA = Towards(corner, 200, 4);
        var farB = Towards(corner, 200 + angle, 3.5);
        var a = towardsA ? Straight(farA, corner, 0.3) : Straight(corner, farA, 0.3);
        var b = towardsB ? Straight(farB, corner, 0.3) : Straight(corner, farB, 0.3);
        var join = WallJoins.Compute(a, b);
        Assert.Equal(JoinKind.L, join.Kind);
        Assert.Equal(towardsA ? Connection.AtEnd : Connection.AtStart, join.A.Connection);
        Assert.Equal(towardsB ? Connection.AtEnd : Connection.AtStart, join.B.Connection);
        Assert.Equal(corner.X, join.Point.X, 9);
        Assert.Equal(corner.Y, join.Point.Y, 9);
        Clean(join, a, b);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(90)]
    [InlineData(140)]
    public void Corners_of_unequal_walls_with_faces_on_the_axis_and_offsets_are_clean(int angle)
    {
        var corner = new Vec2(-1, 0.5);
        var a = Straight(Towards(corner, 90, 3), corner, 0.365, "left", 0.02);
        var b = Straight(corner, Towards(corner, 90 - angle, 2.5), 0.24, "right");
        var join = WallJoins.Compute(a, b);
        Assert.Equal('a', join.Relating);
        Assert.False(WallJoins.Outline(join.A.Wall).Rectangular == (angle != 90));
        Clean(join, a, b);
        var swapped = WallJoins.Compute(a, b, new JoinOptions(Priority: 'b'));
        Assert.Equal('b', swapped.Relating);
        Clean(swapped, a, b);
    }

    [Fact]
    public void Axis_ends_drawn_short_of_or_past_the_corner_are_brought_onto_it()
    {
        var shortOne = Straight(new(0, 0), new(3.8, 0), 0.25);
        var pastOne = Straight(new(4, 0.15), new(4, 5), 0.25);
        var join = WallJoins.Compute(shortOne, pastOne);
        Assert.Equal(new Vec2(4, 0), join.Point);
        Assert.Equal(new Vec2(4, 0), join.A.Wall.End);
        Assert.Equal(new Vec2(4, 0), join.B.Wall.Start);
        Clean(join, shortOne, pastOne);

        // Beyond the tolerance (twice the thicker wall) it is no corner.
        var farOne = Straight(new(0, 0), new(3.3, 0), 0.25);
        Assert.Contains("do not meet", Assert.Throws<CommandException>(() => WallJoins.Compute(farOne, pastOne)).Message);
        // A wider tolerance takes it.
        Clean(WallJoins.Compute(farOne, pastOne, new JoinOptions(Tolerance: 1)), farOne, pastOne);
    }

    [Fact]
    public void A_cut_already_on_the_other_end_is_left_alone()
    {
        var already = new EndCut(0.07, -0.04);
        var a = Straight(new(0, 0), new(4, 0), 0.25) with { StartCut = already };
        var b = Straight(new(4, 0), new(4, 3), 0.25);
        var join = WallJoins.Compute(a, b);
        Assert.Equal(already, join.A.Wall.StartCut);
        Assert.NotNull(join.A.Wall.EndCut);
        Assert.Null(join.B.Wall.EndCut);
    }

    // ------------------------------------------------------------ T

    [Theory]
    [InlineData(90)]
    [InlineData(40)]
    [InlineData(65)]
    [InlineData(115)]
    [InlineData(160)]
    public void A_wall_ending_on_another_s_run_stops_at_its_face_and_leaves_it_untouched(int angle)
    {
        var through = Straight(new(-3, 2), new(5, 2), 0.25);
        var hit = new Vec2(1.5, 2);
        // Drawn from far away to the face of the through wall, as one draws it on screen.
        var ending = Straight(Towards(hit, angle, 4), Towards(hit, angle, 0.125 / Math.Sin(angle * Deg)), 0.2);
        var join = WallJoins.Compute(through, ending);
        Assert.Equal(JoinKind.T, join.Kind);
        Assert.Equal('a', join.Relating);
        Assert.Equal(Connection.AtPath, join.A.Connection);
        Assert.True(join.A.RunsThrough);
        Assert.Equal(through, join.A.Wall);
        Assert.Equal(Connection.AtEnd, join.B.Connection);
        Assert.Equal(hit.X, join.B.Wall.End.X, 9);
        Assert.Equal(hit.Y, join.B.Wall.End.Y, 9);
        Assert.Equal(angle == 90, WallJoins.Outline(join.B.Wall).Rectangular);
        Clean(join, through, ending);
    }

    [Fact]
    public void A_T_given_the_ending_wall_first_with_unequal_offset_bodies_is_clean()
    {
        var through = Straight(new(2, -3), new(2, 4), 0.4, "right", -0.05);
        var ending = Straight(new(2, 1), Towards(new(2, 1), 210, 4), 0.15, "left");
        var join = WallJoins.Compute(ending, through);
        Assert.Equal(JoinKind.T, join.Kind);
        Assert.Equal('b', join.Relating);
        Assert.Equal(Connection.AtStart, join.A.Connection);
        Assert.Equal(Connection.AtPath, join.B.Connection);
        Clean(join, ending, through);
    }

    [Fact]
    public void A_T_from_the_right_hand_side_of_the_through_wall_is_clean()
    {
        var through = Straight(new(6, 0), new(-2, 0), 0.3, offset: 0.1);
        var ending = Straight(new(2, -4), new(2, 0), 0.2);
        var join = WallJoins.Compute(through, ending);
        Assert.Equal(JoinKind.T, join.Kind);
        Clean(join, through, ending);
    }

    [Fact]
    public void A_T_is_never_mitred()
    {
        var join = WallJoins.Compute(Straight(new(0, 1), new(7, 1), 0.25), Straight(new(3, 1), new(3, 5), 0.25), new JoinOptions(Style: JoinStyle.Mitre));
        Assert.Equal(JoinKind.T, join.Kind);
        Assert.Equal(JoinStyle.Butt, join.Style);
    }

    // ------------------------------------------------------------ in line

    [Fact]
    public void Walls_in_line_with_a_small_gap_both_stop_square_halfway()
    {
        var a = Straight(new(0, 1), new(2.6, 1), 0.3);
        var b = Straight(new(2.64, 1), new(5, 1), 0.2, "left");
        var join = WallJoins.Compute(a, b);
        Assert.Equal(JoinKind.Butt, join.Kind);
        Assert.Equal(JoinStyle.Butt, join.Style);
        Assert.Equal('a', join.Relating);
        Assert.Equal(2.62, join.Point.X, 9);
        Assert.Equal(1, join.Point.Y, 9);
        Assert.Equal(Connection.AtEnd, join.A.Connection);
        Assert.Equal(Connection.AtStart, join.B.Connection);
        Assert.False(join.A.RunsThrough);
        Assert.False(join.B.RunsThrough);
        Assert.Equal(EndCut.Square, join.A.Wall.EndCut);
        Assert.Equal(EndCut.Square, join.B.Wall.StartCut);
        Clean(join, a, b);
    }

    [Fact]
    public void Walls_in_line_drawn_a_little_over_each_other_are_pulled_back_to_the_middle()
    {
        var a = Straight(new(0, 0), new(3.12, 0), 0.2);
        var b = Straight(new(3, 0), new(6, 0), 0.2);
        var join = WallJoins.Compute(a, b);
        Assert.Equal(3.06, join.Point.X, 9);
        Assert.Equal(3.06, join.A.Wall.End.X, 9);
        Assert.Equal(3.06, join.B.Wall.Start.X, 9);
        Clean(join, a, b);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(300)]
    public void Walls_in_line_drawn_towards_each_other_meet_whatever_the_direction(int angle)
    {
        var meeting = new Vec2(1, 3);
        var a = Straight(Towards(meeting, angle + 180, 3), meeting, 0.2, offset: 0.05);
        var b = Straight(Towards(meeting, angle, 2.5), meeting, 0.25);
        var join = WallJoins.Compute(a, b, new JoinOptions(Priority: 'b'));
        Assert.Equal(JoinKind.Butt, join.Kind);
        Assert.Equal('b', join.Relating);
        Assert.Equal(Connection.AtEnd, join.A.Connection);
        Assert.Equal(Connection.AtEnd, join.B.Connection);
        Clean(join, a, b);
    }

    [Fact]
    public void Walls_in_line_drawn_away_from_each_other_meet_at_their_starts()
    {
        var a = Straight(new(2, 2), new(2, 6), 0.25);
        var b = Straight(new(2, 1.9), new(2, -3), 0.25);
        var join = WallJoins.Compute(a, b);
        Assert.Equal(Connection.AtStart, join.A.Connection);
        Assert.Equal(Connection.AtStart, join.B.Connection);
        Assert.Equal(1.95, join.Point.Y, 9);
        Clean(join, a, b);
    }

    [Fact]
    public void An_in_line_join_is_never_mitred()
    {
        var join = WallJoins.Compute(Straight(new(0, 0), new(2, 0), 0.2), Straight(new(2, 0), new(5, 0), 0.2), new JoinOptions(Style: JoinStyle.Mitre));
        Assert.Equal(JoinKind.Butt, join.Kind);
        Assert.Equal(JoinStyle.Butt, join.Style);
    }

    // ------------------------------------------------------------ refusals

    static string RefusedWith(Func<WallJoin> act) => Assert.Throws<CommandException>(() => act()).Message;

    [Fact]
    public void Walls_that_cross_must_be_split_first()
    {
        var m = RefusedWith(() => WallJoins.Compute(Straight(new(-2, 1), new(3, 1), 0.2), Straight(new(1, -2), new(1, 4), 0.2)));
        Assert.Contains("cross", m);
    }

    [Fact]
    public void Walls_whose_axes_would_cross_far_from_any_end_do_not_meet()
    {
        var m = RefusedWith(() => WallJoins.Compute(Straight(new(-5, 0), new(-2, 0), 0.2), Straight(new(0, 1), new(0, 5), 0.2)));
        Assert.Contains("do not meet", m);
    }

    [Fact]
    public void Parallel_walls_off_each_other_s_line_do_not_join()
    {
        var m = RefusedWith(() => WallJoins.Compute(Straight(new(0, 0), new(3, 0), 0.2), Straight(new(3, 0.4), new(6, 0.4), 0.2)));
        Assert.Contains("not in line", m);
    }

    [Fact]
    public void Walls_in_line_that_overlap_more_than_the_tolerance_do_not_join()
    {
        var m = RefusedWith(() => WallJoins.Compute(Straight(new(0, 0), new(4, 0), 0.2), Straight(new(2.5, 0), new(7, 0), 0.2)));
        Assert.Contains("in line", m);
        var far = RefusedWith(() => WallJoins.Compute(Straight(new(0, 0), new(4, 0), 0.2), Straight(new(4.6, 0), new(7, 0), 0.2)));
        Assert.Contains("in line", far);
    }

    [Fact]
    public void A_corner_that_would_leave_a_wall_without_a_body_is_refused()
    {
        var wide = Straight(new(-4, 0), new(0, 0), 1.5);
        var stub = Straight(new(0, 0), new(0, 0.5), 0.2);
        var m = RefusedWith(() => WallJoins.Compute(wide, stub, new JoinOptions(Priority: 'a')));
        Assert.Contains("too short", m);
    }

    [Fact]
    public void Bad_input_is_refused_by_name()
    {
        var fine = Straight(new(0, 0), new(1, 0), 0.2);
        Assert.Contains("thickness", RefusedWith(() => WallJoins.Compute(fine with { YMin = 0.1, YMax = 0.1 }, fine)));
        Assert.Contains("distinct", RefusedWith(() => WallJoins.Compute(fine, fine with { End = fine.Start })));
        Assert.Contains("non-finite", RefusedWith(() => WallJoins.Compute(fine with { Start = new(0, double.PositiveInfinity) }, fine)));
        var other = Straight(new(1, 0), new(1, 1), 0.2);
        Assert.Contains("tolerance", RefusedWith(() => WallJoins.Compute(fine, other, new JoinOptions(Tolerance: -0.1))));
        Assert.Contains("tolerance", RefusedWith(() => WallJoins.Compute(fine, other, new JoinOptions(Tolerance: double.NaN))));
        Assert.Contains("priority", RefusedWith(() => WallJoins.Compute(fine, other, new JoinOptions(Priority: 'c'))));
    }

    // ------------------------------------------------------------ the body

    [Fact]
    public void The_alignment_puts_the_axis_on_the_centre_or_on_a_face_and_the_offset_shifts_it_left()
    {
        static (double, double) Band(JoinWall w) => (Math.Round(w.YMin, 9), Math.Round(w.YMax, 9));
        Assert.Equal((-0.15, 0.15), Band(Straight(default, new(1, 0), 0.3)));
        Assert.Equal((-0.3, 0), Band(Straight(default, new(1, 0), 0.3, "left")));
        Assert.Equal((0, 0.3), Band(Straight(default, new(1, 0), 0.3, "right")));
        Assert.Equal((-0.11, 0.19), Band(Straight(default, new(1, 0), 0.3, offset: 0.04)));
        Assert.Equal((-0.26, 0.04), Band(Straight(default, new(1, 0), 0.3, "left", 0.04)));
    }

    [Fact]
    public void A_slanted_cut_makes_a_four_point_outline()
    {
        var w = Straight(new(2, 1), new(2, 5), 0.3) with { EndCut = new EndCut(0.25, -0.12) };
        var o = WallJoins.Outline(w);
        Assert.False(o.Rectangular);
        Assert.Equal(4, o.Length, 9);
        Assert.Equal([new Vec2(0, -0.15), new Vec2(3.88, -0.15), new Vec2(4.25, 0.15), new Vec2(0, 0.15)], o.Corners);
        Assert.True(WallJoins.Outline(w with { EndCut = new EndCut(0.25, 0.25) }).Rectangular);
    }

    [Fact]
    public void Cuts_that_eat_the_whole_wall_are_refused()
    {
        var w = Straight(new(0, 0), new(0.5, 0), 0.3) with { StartCut = new EndCut(-0.3, 0), EndCut = new EndCut(-0.3, 0) };
        Assert.Contains("too short", Assert.Throws<CommandException>(() => WallJoins.Outline(w)).Message);
    }

    // ------------------------------------------------------------ mitre

    [Fact]
    public void A_mitred_square_corner_cuts_both_walls_on_the_diagonal_and_neither_runs_through()
    {
        var a = Straight(new(0, 0), new(4, 0), 0.3);
        var b = Straight(new(4, 0), new(4, 3), 0.3);
        var join = WallJoins.Compute(a, b, new JoinOptions(Style: JoinStyle.Mitre));
        Assert.Equal(JoinKind.L, join.Kind);
        Assert.Equal(JoinStyle.Mitre, join.Style);
        Assert.Equal('a', join.Relating);
        Assert.False(join.A.RunsThrough);
        Assert.False(join.B.RunsThrough);
        // Inside faces stop 150 mm before the axis tip, outside faces reach 150 mm past it.
        Assert.Equal(-0.15, join.A.Wall.EndCut!.Value.Left, 9);
        Assert.Equal(0.15, join.A.Wall.EndCut!.Value.Right, 9);
        Assert.Equal(-0.15, join.B.Wall.StartCut!.Value.Left, 9);
        Assert.Equal(0.15, join.B.Wall.StartCut!.Value.Right, 9);
        Assert.False(WallJoins.Outline(join.A.Wall).Rectangular);
        Clean(join, a, b);
    }

    [Theory]
    [InlineData(35)]
    [InlineData(70)]
    [InlineData(120)]
    public void A_mitre_between_walls_of_different_thickness_shares_one_cut_edge(int angle)
    {
        var corner = new Vec2(2, 2);
        var a = Straight(new(-2, 2), corner, 0.25);
        var b = Straight(corner, Towards(corner, angle, 3), 0.12);
        var join = WallJoins.Compute(a, b, new JoinOptions(Style: JoinStyle.Mitre, Priority: 'b'));
        Assert.Equal(JoinStyle.Mitre, join.Style);
        Assert.Equal('b', join.Relating);
        Clean(join, a, b);
        // Each body is exactly its band up to the shared edge: together they cover what the two bands cover before the corner.
        var cut = CutEdge(Body(join.A.Wall), Connection.AtEnd);
        var dir = (cut.Q - cut.P).Normalized();
        var sideA = SideHolding((cut.P, dir), a.Start);
        var sideB = SideHolding((cut.P, dir), b.End);
        SameShape(Body(join.A.Wall), Keep(Band(a, Connection.AtEnd), sideA.P, sideA.D));
        SameShape(Body(join.B.Wall), Keep(Band(b, Connection.AtStart), sideB.P, sideB.D));
    }
}
