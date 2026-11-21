using Lapin.Geometry;
using Lapin.Drawing.Hatch;

namespace Lapin.Engine.Tests;

/// <summary>Hatches, ported from the old Lapin's hatch/pattern.test.ts, fill.test.ts and region.test.ts (same cases and numbers).</summary>
public class HatchTests
{
    static readonly string Sample = string.Join("\r\n",
        "; a test file",
        "*demo, Demo pattern ; trailing comment",
        "0, 0,0, 0,1",
        "",
        "45, 0.5, 0.25 , 1, 2, 3, -1.5, 0, -1.5",
        "*Other",
        "90, 0,0, 0, 4, bogus",
        "30, 1,2",
        "  ; indented comment",
        "60, 0,0, 0,3, 0,-2",
        "*empty-solid, nothing here");

    // ------------------------------------------------------------ .pat

    [Fact]
    public void Pat_reads_patterns_families_dashes_and_dots_and_reports_bad_lines()
    {
        var (patterns, errors) = Pat.Parse(Sample);
        Assert.Equal(["DEMO", "OTHER", "EMPTY-SOLID"], patterns.Select(p => p.Name));
        Assert.Equal(("Demo pattern", ""), (patterns[0].Description, patterns[1].Description));
        var (a, b) = (patterns[0].Families[0], patterns[0].Families[1]);
        Assert.Equal(2, patterns[0].Families.Count);
        Assert.Equal((0.0, 1.0), (a.AngleDeg, a.DeltaY));
        Assert.Empty(a.Dashes);
        Assert.Equal((45.0, new Vec2(0.5, 0.25), 1.0, 2.0), (b.AngleDeg, b.Origin, b.DeltaX, b.DeltaY));
        Assert.Equal([3, -1.5, 0, -1.5], b.Dashes);
        Assert.Equal([0, -2], patterns[1].Families[0].Dashes);
        // Bad lines, with their numbers, skipped.
        Assert.Equal(2, errors.Count);
        Assert.StartsWith("line 7:", errors[0]);
        Assert.Contains("bogus", errors[0]);
        Assert.StartsWith("line 8:", errors[1]);
        Assert.Single(patterns[1].Families);
        // No families: a solid fill.
        Assert.True(patterns[2].Solid);
        Assert.False(patterns[0].Solid);
    }

    [Fact]
    public void Pat_reports_a_family_before_any_header_and_accepts_spaces_inside_numbers()
    {
        var r = Pat.Parse("0,0,0,0,1\n*X\n0,0,0,0,1");
        Assert.Single(r.Errors);
        Assert.StartsWith("line 1:", r.Errors[0]);
        Assert.Single(r.Patterns);
        var s = Pat.Parse("*S\n 4 5 , 0 , 0 , 0 , 1 . 5");
        Assert.Empty(s.Errors);
        Assert.Equal((45.0, 1.5), (s.Patterns[0].Families[0].AngleDeg, s.Patterns[0].Families[0].DeltaY));
    }

    [Fact]
    public void Pat_round_trips_the_sample_and_the_built_in_library()
    {
        var (patterns, _) = Pat.Parse(Sample);
        var again = Pat.Parse(Pat.Serialize(patterns));
        Assert.Empty(again.Errors);
        Assert.Equal(patterns, again.Patterns);
        var lib = Pat.Parse(Pat.Serialize(HatchLibrary.BuiltInPatterns));
        Assert.Empty(lib.Errors);
        Assert.Equal(HatchLibrary.BuiltInPatterns, lib.Patterns);
    }

    [Fact]
    public void The_library_has_every_pattern_and_the_project_s_own_win()
    {
        var names = HatchLibrary.BuiltInPatterns.Select(p => p.Name).ToList();
        foreach (var n in new[] { "SOLID", "LINES45", "CROSS", "HORIZONTAL", "BRICK", "CONCRETE", "INSULATION", "EARTH", "STEEL", "WOOD", "WOODCROSS", "GRAVEL", "TILES", "BATT" })
            Assert.Contains(n, names);
        foreach (var p in HatchLibrary.BuiltInPatterns)
            if (p.Name == "SOLID") Assert.True(p.Solid);
            else if (BattHatch.Is(p)) Assert.Empty(p.Families); // drawn by its own rule, not by families
            else if (p.Tile != null) Assert.True(p.Tile.Lines.Count + p.Tile.Arcs.Count > 0, p.Name); // a drawn cell
            else { Assert.NotEmpty(p.Families); Assert.All(p.Families, f => Assert.True(Math.Abs(f.DeltaY) > 0, p.Name)); }
        Assert.Equal("BRICK", HatchLibrary.Find("brick")?.Name);
        Assert.Null(HatchLibrary.Find("nope"));
        Assert.Equal("mine", HatchLibrary.Find("Brick", [new HatchPattern("BRICK", "mine", [])])?.Description);
    }

    // ------------------------------------------------------------ fill

    static Vec2[] Sq(double x, double y, double size, bool cw = false)
    {
        Vec2[] pts = [new(x, y), new(x + size, y), new(x + size, y + size), new(x, y + size)];
        return cw ? pts.Reverse().ToArray() : pts;
    }

    static HatchLineFamily Fam(double angle, double deltaY, double[]? dashes = null, double deltaX = 0, Vec2? origin = null) =>
        new(angle, origin ?? new Vec2(0, 0), deltaX, deltaY, dashes ?? []);

    static HatchPattern P(params HatchLineFamily[] f) => new("T", "", f);
    static double Total(IEnumerable<HatchSegment> s) => s.Sum(g => (g.B - g.A).Length);
    const double E = 1e-9;
    static bool InBox(Vec2 p, double x0, double y0, double x1, double y1) => p.X >= x0 - E && p.X <= x1 + E && p.Y >= y0 - E && p.Y <= y1 + E;
    static readonly Vec2[] Square = Sq(0, 0, 10);

    [Fact]
    public void Lines_at_45_stay_inside_a_square_and_cover_area_over_spacing()
    {
        var (segs, truncated) = HatchFill.Segments([Square], P(Fam(45, 1)), new(1));
        Assert.False(truncated);
        Assert.True(segs.Count > 10);
        Assert.All(segs, s => Assert.True(InBox(s.A, 0, 0, 10, 10) && InBox(s.B, 0, 0, 10, 10)));
        Assert.True(Math.Abs(Total(segs) - 100) / 100 < 0.05);
        // The scale multiplies the spacing.
        Assert.True(Math.Abs(Total(HatchFill.Segments([Square], P(Fam(0, 1)), new(0.5)).Segments) - 200) / 200 < 0.06);
    }

    [Fact]
    public void Lines_through_vertices_do_not_flip_intervals()
    {
        foreach (var s in HatchFill.Segments([Square], P(Fam(0, 1)), new(1)).Segments)
        {
            Assert.True(Math.Abs(s.A.Y - s.B.Y) < E);
            Assert.True(InBox(s.A, 0, 0, 10, 10) && InBox(s.B, 0, 0, 10, 10));
        }
        Vec2[] diamond = [new(0, -5), new(5, 0), new(0, 5), new(-5, 0)];
        var d = HatchFill.Segments([diamond], P(Fam(0, 1)), new(1)).Segments;
        var mid = d.First(s => Math.Abs(s.A.Y) < E);
        Assert.True(Math.Abs(Math.Abs(mid.B.X - mid.A.X) - 10) < 1e-9);
        Assert.True(Math.Abs(Total(d) - 50) < 1e-9);
    }

    [Fact]
    public void A_hole_gets_no_strokes_and_an_island_in_it_fills_again()
    {
        var segs = HatchFill.Segments([Square, Sq(3, 3, 4, true)], P(Fam(45, 0.7), Fam(0, 0.5)), new(1)).Segments;
        Assert.NotEmpty(segs);
        foreach (var s in segs)
        {
            var m = (s.A + s.B) * 0.5;
            Assert.False(m.X > 3 + E && m.X < 7 - E && m.Y > 3 + E && m.Y < 7 - E);
        }
        var len = Total(segs.Where(s => Math.Abs(s.A.Y - s.B.Y) < E));
        Assert.True(Math.Abs(len - 84 / 0.5) / (84 / 0.5) < 0.03, $"length {len}");
        var nested = HatchFill.Segments([Square, Sq(2, 2, 6), Sq(4, 4, 2)], P(Fam(0, 0.25)), new(1)).Segments;
        Assert.Contains(nested, s => s.A.X >= 4 - E && s.B.X <= 6 + E && s.A.Y > 4 && s.A.Y < 6);
    }

    [Fact]
    public void Dashes_dots_and_the_shift_between_lines()
    {
        var dashed = HatchFill.Segments([Square], P(Fam(0, 1, [0.3, -0.2])), new(1)).Segments;
        Assert.True(Math.Abs(Total(dashed) - 60) / 60 < 0.05);   // 10 lines (y = 10 excluded by the half-open rule)
        Assert.All(dashed, s => Assert.True((s.B - s.A).Length <= 0.3 + E));
        var dots = HatchFill.Segments([Square], P(Fam(0, 1, [0, -1])), new(1, 0, new Vec2(0.5, 0.5))).Segments;
        Assert.Equal(100, dots.Count);
        Assert.All(dots, s => Assert.Equal(s.A, s.B));
        var shifted = HatchFill.Segments([Sq(0, 0, 4)], P(Fam(0, 1, [1, -1], 1)), new(1, 0, new Vec2(0, 0.5))).Segments;
        Assert.Equal([0, 2], shifted.Where(s => Math.Abs(s.A.Y - 0.5) < E).Select(s => Math.Round(s.A.X, 9)).Order());
        Assert.Equal([1, 3], shifted.Where(s => Math.Abs(s.A.Y - 1.5) < E).Select(s => Math.Round(s.A.X, 9)).Order());
    }

    [Fact]
    public void Rotation_truncation_and_patterns_that_draw_nothing()
    {
        var turned = HatchFill.Segments([Square], P(Fam(0, 1)), new(1, 90)).Segments;
        Assert.True(turned.Count > 5);
        Assert.All(turned, s => { Assert.True(Math.Abs(s.A.X - s.B.X) < 1e-9); Assert.True(Math.Abs(s.A.Y - s.B.Y) > 9.99); });
        var r = HatchFill.Segments([Square], P(Fam(0, 0.1)), new(1, MaxSegments: 5));
        Assert.True(r.Truncated);
        Assert.Equal(5, r.Segments.Count);
        Assert.Empty(HatchFill.Segments([Square], P(Fam(0, 0)), new(1)).Segments);
        Assert.Empty(HatchFill.Segments([Square], HatchLibrary.Find("SOLID")!, new(1)).Segments);
        Assert.Empty(HatchFill.Segments([Square], P(Fam(0, 1, [-1])), new(1)).Segments);
    }

    [Fact]
    public void Every_built_in_pattern_hatches_a_100_mm_square_within_it()
    {
        var box = Sq(0, 0, 100);
        foreach (var p in HatchLibrary.BuiltInPatterns)
        {
            var r = HatchFill.Segments([box], p, new(1, 15, new Vec2(3, 7)));
            Assert.False(r.Truncated, p.Name);
            if (p.Solid) continue;
            if (BattHatch.Is(p))
            {
                var batt = BattHatch.Segments([box]);
                Assert.NotEmpty(batt);
                Assert.All(batt, s => Assert.True(InBox(s.A, 0, 0, 100, 100) && InBox(s.B, 0, 0, 100, 100), p.Name));
                continue;
            }
            Assert.NotEmpty(r.Segments);
            Assert.All(r.Segments, s => Assert.True(InBox(s.A, 0, 0, 100, 100) && InBox(s.B, 0, 0, 100, 100), p.Name));
        }
    }

    // ------------------------------------------------------------ region

    [Fact]
    public void A_region_is_the_smallest_loop_round_the_point_and_its_islands()
    {
        var big = Sq(0, 0, 100);
        var mid = Sq(10, 10, 50, true);
        var islandA = Sq(20, 20, 10);
        var nested = Sq(22, 22, 4);
        var islandB = Sq(40, 40, 10, true);
        var outside = Sq(200, 200, 5);
        Vec2[] degenerate = [new(15, 15), new(16, 16), new(17, 17)];
        IReadOnlyList<Vec2>[] loops = [big, mid, islandA, nested, islandB, outside, degenerate];
        Assert.Equal(9, HatchRegion.LoopArea(Sq(0, 0, 3)));
        Assert.Equal(9, HatchRegion.LoopArea(Sq(0, 0, 3, true)));

        var r = HatchRegion.At(new Vec2(12, 12), loops)!;
        Assert.Same(mid, r[0]);
        Assert.Equal(4, r.Count);
        Assert.Contains(islandA, r.Skip(1).Take(2));
        Assert.Contains(islandB, r.Skip(1).Take(2));
        Assert.Same(nested, r[3]);

        var ring = HatchRegion.At(new Vec2(80, 80), loops)!;
        Assert.Same(big, ring[0]);
        Assert.Same(mid, ring[1]);
        Assert.Contains(islandA, ring.Skip(2));
        Assert.Contains(nested, ring.Skip(2));

        var island = HatchRegion.At(new Vec2(21, 21), loops)!;
        Assert.Same(islandA, island[0]);
        Assert.Equal([nested], island.Skip(1));

        Assert.Null(HatchRegion.At(new Vec2(-5, -5), loops));
        Assert.Null(HatchRegion.At(new Vec2(1, 1), []));
        var touching = Sq(0, 0, 5);
        Assert.Equal([touching], HatchRegion.At(new Vec2(50, 50), [Sq(0, 0, 100), touching])!.Skip(1));
    }

    [Fact]
    public void A_repeated_outline_is_dropped_and_islands_in_holes_come_after_the_holes()
    {
        var outer = Sq(0, 0, 10);
        var hole = Sq(4, 4, 2);
        Vec2[] again = [hole[2], hole[3], hole[0], hole[1]];
        Assert.Equal(2, HatchRegion.At(new Vec2(1, 1), [outer, hole, outer, again])!.Count);
        var region = HatchRegion.At(new Vec2(1, 1), [Sq(0, 0, 100), Sq(10, 10, 40), Sq(20, 20, 20), Sq(26, 26, 5)])!;
        Assert.Equal(4, region.Count);
        Assert.Equal(Sq(10, 10, 40), region[1]);
    }
}

public class BattHatchTests
{
    /// <summary>The looped line stays inside its band, runs its whole length, and is clipped at a mitred end.</summary>
    [Fact]
    public void The_batt_line_fills_a_band_and_stays_inside_it()
    {
        Vec2[] band = [new(0, 0), new(2, 0), new(2, 0.1), new(0, 0.1)];
        var segs = BattHatch.Segments([band]);
        Assert.True(segs.Count > 200, $"{segs.Count} segments");
        foreach (var s in segs) foreach (var p in new[] { s.A, s.B })
        {
            Assert.InRange(p.X, -1e-6, 2 + 1e-6);
            Assert.InRange(p.Y, -1e-6, 0.1 + 1e-6);
        }
        Assert.True(segs.Min(s => Math.Min(s.A.X, s.B.X)) < 0.05 && segs.Max(s => Math.Max(s.A.X, s.B.X)) > 1.95, "the line runs the whole band");
        // A vertical band (a wall in section) is drawn up it; a mitred end keeps the line inside the polygon.
        Vec2[] mitre = [new(0, 0), new(0.2, 0.2), new(0.2, 3), new(0, 3)];
        var up = BattHatch.Segments([mitre]);
        Assert.True(up.Count > 100);
        Assert.DoesNotContain(up, s => s.A.Y < s.A.X - 1e-6 || s.B.Y < s.B.X - 1e-6);
        // Too thin on paper: nothing.
        Assert.Empty(BattHatch.Segments([band], minWidth: 0.2));
    }

    /// <summary>
    /// What a batt line must do in a band of half-width h laid along <paramref name="dir"/>: every point inside the faces
    /// (|y| ≤ h), heads on both faces (points within 2 % of h of each face), and the faces touched all along the band, a
    /// head on each face every 2·1.5·0.4·h before the fit (no stretch of more than two of those without a touch on either face).
    /// </summary>
    static void AssertBatt(List<HatchSegment> segs, Func<Vec2, (double S, double Y)> frame, double h, double s0, double s1, string what, double slack = 0)
    {
        Assert.True(segs.Count > 50, $"{what}: {segs.Count} segments");
        var pts = segs.SelectMany(s => new[] { s.A, s.B }).Select(frame).ToList();
        Assert.All(pts, p => Assert.True(Math.Abs(p.Y) <= h + slack + 1e-6, $"{what}: a point {p.Y:0.####} off the axis, the faces at ±{h}"));
        Assert.All(pts, p => Assert.InRange(p.S, s0 - 1e-6, s1 + 1e-6));
        var pitch = 2 * BattHatch.Spacing * BattHatch.Head * h;
        foreach (var face in new[] { -1, 1 })
        {
            var touches = pts.Where(p => face * p.Y >= h * 0.98 - slack).Select(p => p.S).Order().ToList();
            Assert.True(touches.Count > 0, $"{what}: the face at {face * h} is never touched");
            // Along the band a touch every pitch (within the fit); from either end, two and a half (a notch or a mitre at an
            // end may clip a head there).
            var gaps = touches.Zip(touches.Skip(1), (a, b) => b - a).Where(g => g > h * 0.1).ToList();
            Assert.True(gaps.Count == 0 || gaps.Max() <= 1.5 * pitch, $"{what}: the face at {face * h} goes {(gaps.Count == 0 ? 0 : gaps.Max()):0.###} untouched between heads (pitch {pitch:0.###})");
            var ends = Math.Max(touches[0] - s0, s1 - touches[^1]);
            Assert.True(ends <= 2.5 * pitch, $"{what}: the face at {face * h} goes {ends:0.###} untouched from an end (pitch {pitch:0.###})");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(147)]
    public void A_batt_band_at_any_angle_loops_on_both_faces_along_its_whole_length(double deg)
    {
        // A 100 mm layer 3 m long, at the angle, mitred at one end (45°) and with a corner piece at the other.
        var a = deg * Math.PI / 180;
        var d = new Vec2(Math.Cos(a), Math.Sin(a));
        var n = d.Perp;
        Vec2 P(double s, double y) => new Vec2(5, 2) + d * s + n * y;
        Vec2[] band = [P(0, -0.05), P(3, -0.05), P(3.1, 0.05), P(0.1, 0.05), P(0.1, 0.04), P(0, 0.04)];
        var segs = BattHatch.Segments([band]);
        (double, double) Frame(Vec2 p) => (Vec2.Dot(p - P(0, 0), d), Vec2.Dot(p - P(0, 0), n));
        AssertBatt(segs, Frame, 0.05, 0, 3.1, $"{deg}°");
    }

    [Fact]
    public void A_batt_band_laid_along_a_curved_axis_bends_with_it_between_its_faces()
    {
        // A layer 100 mm thick between radii 4.95 and 5.05 round (0, 0), over 120°, laid in the arc's own frame.
        double r = 5, from = Math.PI / 6, sweep = 2 * Math.PI / 3;
        var outer = Chords.Arc(new Vec2(0, 0), r + 0.05, from, sweep);
        var inner = Chords.Arc(new Vec2(0, 0), r - 0.05, from, sweep);
        inner.Reverse();
        var band = outer.Concat(inner).ToArray();
        // s along the arc anticlockwise from its start, y to its left (towards the centre).
        (double S, double Y) ToBand(Vec2 p) { var t = Math.Atan2(p.Y, p.X) - from; return (t * r, r - p.Length); }
        Vec2 FromBand(double s, double y) { var t = from + s / r; return new Vec2(Math.Cos(t), Math.Sin(t)) * (r - y); }
        var segs = BattHatch.Segments([band], axis: new BandAxis(ToBand, FromBand));
        // The chords of the faces stray 1 mm at most from the arcs (Chords): the faces within 1 mm of ±h in the arc's frame.
        AssertBatt(segs, ToBand, 0.05, 0, r * sweep, "curved", Chords.Tolerance);
        // Without the axis, the band's longest edge (one chord) would lay a straight line across the curve: the frame matters.
        Assert.NotEqual(segs.Count, BattHatch.Segments([band]).Count);
    }

    static Vec2? Crossing(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        var (r, s) = (b - a, d - c);
        var den = Vec2.Cross(r, s);
        if (Math.Abs(den) < 1e-12) return null;
        var t = Vec2.Cross(c - a, s) / den;
        var u = Vec2.Cross(c - a, r) / den;
        return t > 1e-9 && t < 1 - 1e-9 && u > 1e-9 && u < 1 - 1e-9 ? a + r * t : null;
    }

    /// <summary>
    /// The construction in the user's reference band, 1200 × 300 (h = 150): heads of radius 0.4·h = 60 tangent to the faces,
    /// 13 of them 90 apart (1200 = 2·60 + 12·90: a whole number, nothing partial at either end), the first against the top
    /// face touching the start, the last touching the end; every straight stroke tangent to the two heads it joins, two per
    /// neighbouring pair; and the only crossings of the strokes the two low strokes of each head between the end ones, under
    /// it, r / sin φ − (h − r) = 44.164 past the mid-line, φ = atan(90 / 180) their tilt.
    /// </summary>
    [Fact]
    public void The_batt_s_heads_fit_the_band_touch_its_faces_and_their_strokes_cross_under_them()
    {
        Assert.Equal((13, 90.0, 60.0), BattHatch.Fit(1200, 150));
        foreach (var len in new[] { 1000.0, 1234.5, 777, 400 })
        {
            var (n, d, r) = BattHatch.Fit(len, 150);
            Assert.Equal(len, 2 * r + (n - 1) * d, 9);
            Assert.InRange(d, 0.75 * 90, 1.5 * 90);
        }
        // Too short for two heads: one, centred.
        Assert.Equal((1, 0.0, 60.0), BattHatch.Fit(150, 150));

        var strokes = BattHatch.Strokes(0, 1200, 150);
        var straight = strokes.Where(s => s.Count == 2).ToList();
        var arcs = strokes.Where(s => s.Count > 2).ToList();
        Assert.Equal(24, straight.Count);
        Assert.Equal(26, arcs.Count);
        var centres = Enumerable.Range(0, 13).Select(j => new Vec2(60 + 90 * j, j % 2 == 0 ? 90 : -90)).ToList();
        // Each head touches its face (its arc split there), the first the top one; all arcs on their heads, within the band.
        foreach (var c in centres) Assert.Contains(arcs, a => (a[^1] - new Vec2(c.X, Math.Sign(c.Y) * 150)).Length < 1e-9);
        Assert.All(arcs, a => Assert.All(a, p => Assert.Contains(centres, c => Math.Abs((p - c).Length - 60) < 1e-9)));
        var all = strokes.SelectMany(s => s).ToList();
        Assert.Equal(0, centres[0].X - 60, 9);
        Assert.Equal(1200, centres[^1].X + 60, 9);
        Assert.All(all, p => Assert.InRange(p.X, -1e-9, 1200 + 1e-9));
        Assert.All(all, p => Assert.InRange(p.Y, -150 - 1e-9, 150 + 1e-9));
        // Each straight stroke tangent at both ends: on a head, square to its radius there, and the heads at its ends neighbours.
        foreach (var s in straight)
        {
            var ends = s.Select(p => centres.FindIndex(c => Math.Abs((p - c).Length - 60) < 1e-9)).ToList();
            Assert.DoesNotContain(-1, ends);
            Assert.Equal(1, Math.Abs(ends[0] - ends[1]));
            for (var k = 0; k < 2; k++) Assert.Equal(0, Vec2.Dot(s[1] - s[0], s[k] - centres[ends[k]]), 6);
        }
        // The crossings: under each head between the end ones, at 44.164 past the mid-line on the far side.
        var crossings = new List<Vec2>();
        for (var i = 0; i < straight.Count; i++)
            for (var k = i + 1; k < straight.Count; k++)
                if (Crossing(straight[i][0], straight[i][1], straight[k][0], straight[k][1]) is { } x) crossings.Add(x);
        Assert.Equal(11, crossings.Count);
        var far = 60 / Math.Sin(Math.Atan2(90, 180)) - 90;
        Assert.Equal(44.164, far, 3);
        for (var j = 1; j < 12; j++)
            Assert.Contains(crossings, x => (x - new Vec2(centres[j].X, -Math.Sign(centres[j].Y) * far)).Length < 1e-6);
    }

    [Fact]
    public void The_first_head_lies_on_the_bands_left_face_whatever_its_winding_and_flips_and_stretches_when_asked()
    {
        // A band from left to right, traced either way: the first head (at the left end) touches the top face.
        Vec2[] ccw = [new(0, 0), new(1.2, 0), new(1.2, 0.3), new(0, 0.3)];
        Vec2[] cw = [.. ccw.Reverse()];
        foreach (var band in new[] { ccw, cw })
        {
            var segs = BattHatch.Segments([band]);
            var top = segs.SelectMany(s => new[] { s.A, s.B }).Where(p => p.Y > 0.3 - 1e-4).Min(p => p.X);
            var bottom = segs.SelectMany(s => new[] { s.A, s.B }).Where(p => p.Y < 1e-4).Min(p => p.X);
            Assert.Equal(0.06, top, 4);
            Assert.Equal(0.15, bottom, 4);
            // Flipped: the other way round.
            var flipped = BattHatch.Segments([band], flip: true);
            Assert.Equal(0.06, flipped.SelectMany(s => new[] { s.A, s.B }).Where(p => p.Y < 1e-4).Min(p => p.X), 4);
        }
        // Up the page: the first head (at the bottom) on the left face.
        Vec2[] up = [new(0, 0), new(0.3, 0), new(0.3, 1.2), new(0, 1.2)];
        Assert.Equal(0.06, BattHatch.Segments([up]).SelectMany(s => new[] { s.A, s.B }).Where(p => p.X < 1e-4).Min(p => p.Y), 4);
        // The hatch scale stretches the spacing (then fitted again): twice as far apart, 7 heads instead of 13.
        Assert.Equal((7, 180.0, 60.0), BattHatch.Fit(1200, 150, 2));
        // A material's hatch angle of half a turn flips the batt; a quarter turn or none does not.
        Assert.True(BattHatch.FlipsAt(180));
        Assert.True(BattHatch.FlipsAt(-180));
        Assert.False(BattHatch.FlipsAt(0));
        Assert.False(BattHatch.FlipsAt(90));
    }

    [Fact]
    public void Insulation_materials_take_the_batt_by_name_and_the_library_knows_it()
    {
        Assert.Equal("BATT", Lapin.Drawing.MaterialGraphics.FromName("Vată minerală").Hatch);
        // Rigid boards a fine square grid, as Revit's "Insulation - Rigid" (decision 460).
        Assert.Equal("RIGID", Lapin.Drawing.MaterialGraphics.FromName("XPS").Hatch);
        Assert.True(BattHatch.Is(HatchLibrary.Find("batt")));
    }

    /// <summary>
    /// The insulation patterns of the user's reference sheet, drawn by Lapin: each in the library, hatching a 20 mm square
    /// within it, and its scale its size (at twice the scale, the strokes of a cell twice as long and about a quarter as many).
    /// The material-name guesses do not take them (insulation stays BATT, rigid boards RIGID).
    /// </summary>
    [Theory]
    [InlineData("FOAMBOARD", true)]
    [InlineData("REFLECTIVE", true)]
    [InlineData("BLANKET", false)]
    [InlineData("FIBERGLASS", true)]
    [InlineData("BLOWNIN", true)]
    [InlineData("RIGIDFOAM", true)]
    [InlineData("ACOUSTIC", true)]
    [InlineData("RIGIDFIBER", false)]
    [InlineData("COTTONFIBER", true)]
    public void The_insulation_patterns_hatch_a_square_at_their_scale(string name, bool tile)
    {
        var p = HatchLibrary.Find(name);
        Assert.NotNull(p);
        Assert.False(p!.Solid);
        Assert.Equal(tile, p.Tile != null);
        Assert.Equal(HatchUnits.Paper, p.Units);
        Vec2[] sq = [new(0, 0), new(20, 0), new(20, 20), new(0, 20)];
        var one = HatchFill.Segments([sq], p, new HatchOptions(1)).Segments;
        var two = HatchFill.Segments([sq], p, new HatchOptions(2)).Segments;
        Assert.NotEmpty(one);
        Assert.All(one.Concat(two), s => Assert.True(InBox(s.A) && InBox(s.B), name));
        // Twice the scale: fewer strokes over the same square, and the ink (their total length) about half.
        Assert.True(two.Count < one.Count, $"{name}: {two.Count} strokes at ×2, {one.Count} at ×1");
        double Ink(List<HatchSegment> l) => l.Sum(s => (s.B - s.A).Length);
        Assert.InRange(Ink(two) / Ink(one), 0.35, 0.65);
        static bool InBox(Vec2 v) => v.X >= -1e-6 && v.X <= 20 + 1e-6 && v.Y >= -1e-6 && v.Y <= 20 + 1e-6;
        Assert.NotEqual(name, Lapin.Drawing.MaterialGraphics.FromName("Mineral wool").Hatch);
    }
}
