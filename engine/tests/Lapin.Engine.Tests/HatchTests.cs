using BubbleBim.Geometry;
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
        foreach (var n in new[] { "SOLID", "LINES45", "CROSS", "HORIZONTAL", "BRICK", "CONCRETE", "INSULATION", "EARTH", "STEEL", "WOOD", "WOODCROSS", "GRAVEL", "TILES" })
            Assert.Contains(n, names);
        foreach (var p in HatchLibrary.BuiltInPatterns)
            if (p.Name == "SOLID") Assert.True(p.Solid);
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
