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
    /// head on each face every 0.8·h before the fit (no stretch of more than two of those without a touch on either face).
    /// </summary>
    static void AssertBatt(List<HatchSegment> segs, Func<Vec2, (double S, double Y)> frame, double h, double s0, double s1, string what, double slack = 0)
    {
        Assert.True(segs.Count > 50, $"{what}: {segs.Count} segments");
        var pts = segs.SelectMany(s => new[] { s.A, s.B }).Select(frame).ToList();
        Assert.All(pts, p => Assert.True(Math.Abs(p.Y) <= h + slack + 1e-6, $"{what}: a point {p.Y:0.####} off the axis, the faces at ±{h}"));
        Assert.All(pts, p => Assert.InRange(p.S, s0 - 1e-6, s1 + 1e-6));
        var pitch = BattHatch.Head * 2 * h;
        foreach (var face in new[] { -1, 1 })
        {
            var touches = pts.Where(p => face * p.Y >= h * 0.98 - slack).Select(p => p.S).Order().ToList();
            Assert.True(touches.Count > 0, $"{what}: the face at {face * h} is never touched");
            // Along the band a touch every pitch (within the fit); from either end, a loop and a half past a 45° mitre's run (2h:
            // a notch or a mitre at an end may clip a head there).
            var gaps = touches.Zip(touches.Skip(1), (a, b) => b - a).Where(g => g > h * 0.1).ToList();
            Assert.True(gaps.Count == 0 || gaps.Max() <= 1.5 * pitch, $"{what}: the face at {face * h} goes {(gaps.Count == 0 ? 0 : gaps.Max()):0.###} untouched between heads (pitch {pitch:0.###})");
            var ends = Math.Max(touches[0] - s0, s1 - touches[^1]);
            Assert.True(ends <= 2 * h + 1.5 * pitch, $"{what}: the face at {face * h} goes {ends:0.###} untouched from an end (pitch {pitch:0.###})");
        }
    }

    /// <summary>
    /// The batt's loops as drawn: the segments chained end to end into closed outlines (a whole loop is one: its head's arc,
    /// its two sides, its tip). A loop cut short by the outline would leave open ends (a stub, a half head): none may, so every
    /// point is met by an even number of segments (two; four where heads touch at a point their arcs share). Returns the
    /// outlines' points, loops touching that way counted as one.
    /// </summary>
    static List<List<Vec2>> WholeLoops(List<HatchSegment> segs, string what)
    {
        var key = new Dictionary<(long, long), int>();
        var pts = new List<Vec2>();
        int Id(Vec2 p)
        {
            var k = ((long)Math.Round(p.X * 1e8), (long)Math.Round(p.Y * 1e8));
            if (!key.TryGetValue(k, out var i)) { i = pts.Count; key[k] = i; pts.Add(p); }
            return i;
        }
        var adj = new Dictionary<int, List<int>>();
        foreach (var s in segs)
        {
            var (a, b) = (Id(s.A), Id(s.B));
            if (a == b) continue;
            (adj.TryGetValue(a, out var la) ? la : adj[a] = []).Add(b);
            (adj.TryGetValue(b, out var lb) ? lb : adj[b] = []).Add(a);
        }
        var open = adj.Where(e => e.Value.Count % 2 != 0).ToList();
        Assert.True(open.Count == 0, $"{what}: {open.Count} open ends (a partial loop or a stub), the first at ({(open.Count > 0 ? pts[open[0].Key].X : 0):0.####}, {(open.Count > 0 ? pts[open[0].Key].Y : 0):0.####}), degrees {string.Join(",", open.Take(12).Select(e => e.Value.Count))}");
        var seen = new HashSet<int>();
        var loops = new List<List<Vec2>>();
        foreach (var v in adj.Keys)
        {
            if (!seen.Add(v)) continue;
            var loop = new List<Vec2> { pts[v] };
            var stack = new Stack<int>([v]);
            while (stack.Count > 0) foreach (var u in adj[stack.Pop()]) if (seen.Add(u)) { loop.Add(pts[u]); stack.Push(u); }
            loops.Add(loop);
        }
        return loops;
    }

    /// <summary>
    /// For random bands (lengths, thicknesses, angles; each end square, mitred either way at 30°–60°, or a T's square end), in
    /// either orientation and at any hatch scale: every stroke belongs to a whole loop, every point of every loop inside the
    /// band within 1e-6; at a square end the chain reaches the end (to its heads' chords), at a mitred one it stops within a pitch of where the
    /// mid-line meets the mitre, plus the mitre's run across the half band; the loops evenly spread, their width within 15 % of
    /// the nominal one when there are enough of them to choose from.
    /// </summary>
    [Fact]
    public void Every_batt_stroke_belongs_to_a_whole_loop_inside_the_band_and_the_chain_reaches_both_ends()
    {
        var rnd = new Random(754);
        for (var t = 0; t < 400; t++)
        {
            var len = 0.3 + rnd.NextDouble() * 5.7;
            var h = (0.04 + rnd.NextDouble() * 0.26) / 2;
            var scale = rnd.Next(3) == 0 ? 0.6 + rnd.NextDouble() * 0.5 : 1;
            var flip = rnd.Next(2) == 0;
            var a = rnd.NextDouble() * 2 * Math.PI;
            var d = new Vec2(Math.Cos(a), Math.Sin(a));
            var n = d.Perp;
            var o = new Vec2(rnd.NextDouble() * 20 - 10, rnd.NextDouble() * 20 - 10);
            Vec2 P(double s, double y) => o + d * s + n * y;
            // Each end's run: how far the face at +h goes past the mid-line's end (negative: falls short); 0 a square end.
            double Run() => rnd.Next(3) switch { 0 => 0, 1 => h * Math.Tan((30 + rnd.NextDouble() * 30) * Math.PI / 180), _ => -h * Math.Tan((30 + rnd.NextDouble() * 30) * Math.PI / 180) };
            var (r0, r1) = (Run(), Run());
            len = Math.Max(len, 2 * (Math.Abs(r0) + Math.Abs(r1)) + 0.2);
            Vec2[] band = [P(r0, h), P(-r0, -h), P(len - r1, -h), P(len + r1, h)];
            var what = $"band {t}: {len:0.###} × {2 * h:0.###}, runs {r0:0.###} / {r1:0.###}, scale {scale:0.##}{(flip ? ", flipped" : "")}";
            var segs = BattHatch.Segments([band], scale: scale, flip: flip);
            Assert.True(segs.Count > 0, what);
            Assert.All(segs.SelectMany(s => new[] { s.A, s.B }), p => Assert.True(Polygon2.Contains(band, p, 1e-6), $"{what}: a point outside the band"));
            var loops = WholeLoops(segs, what);
            (double S, double Y) F(Vec2 p) => (Vec2.Dot(p - o, d), Vec2.Dot(p - o, n));
            var all = loops.SelectMany(l => l).Select(F).ToList();
            double lo = all.Min(p => p.S), hi = all.Max(p => p.S);
            var w0 = BattHatch.Head * 2 * h * scale;
            Assert.True(lo - 0 <= (r0 == 0 ? Chords.Tolerance : w0 + Math.Abs(r0)), $"{what}: the chain starts {lo:0.####} in");
            Assert.True(len - hi <= (r1 == 0 ? Chords.Tolerance : w0 + Math.Abs(r1)), $"{what}: the chain stops {len - hi:0.####} short");
            // The loops' width from the chain's length, (n + 1)·w/2, n the heads (where each touches its face).
            var heads = segs.SelectMany(s => new[] { s.A, s.B }).Select(F).Where(p => Math.Abs(p.Y) >= h - 1e-9).Select(p => Math.Round(p.S * 1e7)).Distinct().Count();
            var w = 2 * (hi - lo) / (heads + 1);
            // Evenly spread: the heads alternate faces every w/2, none left out between (a gap of w or more).
            var touch = segs.SelectMany(s => new[] { s.A, s.B }).Select(F).Where(p => Math.Abs(p.Y) >= h - 1e-9).Select(p => p.S).Distinct().Order().ToList();
            var gap = touch.Zip(touch.Skip(1), (x, y) => y - x).DefaultIfEmpty(0).Max();
            Assert.True(gap <= 0.75 * w, $"{what}: a gap of {gap:0.####} between heads, the loops {w:0.####} wide");
            if (heads >= 8) Assert.True(w / w0 is >= 0.85 and <= 1.15, $"{what}: {heads} loops {w / w0:0.###} of the nominal width");
        }
    }

    /// <summary>
    /// The user's case: two walls of a 150 mm wool layer meeting at an L, mitred on the corner's diagonal. Each wall's batt
    /// ends on a whole loop on its side of the mitre (no cut teardrop, no loose stub, nothing over the mitre), both close to it.
    /// </summary>
    [Fact]
    public void At_an_L_corner_each_walls_batt_ends_on_a_whole_loop_short_of_the_mitre()
    {
        // The outer corner at (3.15, 0), the inner at (3, 0.15): one band along x, the other up y.
        Vec2[] along = [new(0, 0), new(3.15, 0), new(3, 0.15), new(0, 0.15)];
        Vec2[] up = [new(3.15, 0), new(3.15, 3), new(3, 3), new(3, 0.15)];
        foreach (var flip in new[] { false, true })
        {
            foreach (var (band, name) in new[] { (along, "along"), (up, "up") })
            {
                var segs = BattHatch.Segments([band], flip: flip);
                var loops = WholeLoops(segs, name);
                var heads = segs.SelectMany(s => new[] { s.A, s.B }).Where(p => name == "along" ? p.Y is < 1e-9 or > 0.15 - 1e-9 : p.X is < 3 + 1e-9 or > 3.15 - 1e-9).Distinct().Count();
                Assert.True(heads > 40, $"{name}: {heads} loops");
                // On its own side of the mitre (x − 3.15 = −y for the band along x, the other side for the one up y).
                Assert.All(loops.SelectMany(l => l), p => Assert.True(name == "along" ? p.X + p.Y <= 3.15 + 1e-6 : p.X + p.Y >= 3.15 - 1e-6, name));
                // Close to it: within a loop and the mitre's run of the mid-line's end (3.075 along either wall).
                var far = name == "along" ? loops.SelectMany(l => l).Max(p => p.X) : loops.SelectMany(l => l).Min(p => p.Y);
                Assert.True(name == "along" ? 3.075 - far <= 0.06 + 0.075 : far - 0.075 <= 0.06 + 0.075, $"{name}: ends at {far:0.###}");
            }
        }
    }

    /// <summary>A layer cut by an opening (two pieces, square at the opening's reveals) and a T's square end: whole loops touching each end.</summary>
    [Fact]
    public void A_batt_cut_by_an_opening_or_ending_square_ends_on_whole_loops_touching_the_cut()
    {
        Vec2[] left = [new(0, 0), new(1.37, 0), new(1.37, 0.1), new(0, 0.1)];
        Vec2[] right = [new(2.27, 0), new(4.05, 0), new(4.05, 0.1), new(2.27, 0.1)];
        var segs = BattHatch.Segments([left]).Concat(BattHatch.Segments([right])).ToList();
        var loops = WholeLoops(segs, "opening");
        var xs = loops.SelectMany(l => l).Select(p => p.X).ToList();
        // Each piece's chain from end to end (its heads touch the ends, to the chords of their arcs), nothing in the opening.
        Assert.InRange(xs.Min(), 0, Chords.Tolerance);
        Assert.InRange(xs.Max(), 4.05 - Chords.Tolerance, 4.05);
        Assert.InRange(xs.Where(x => x < 2).Max(), 1.37 - Chords.Tolerance, 1.37);
        Assert.InRange(xs.Where(x => x > 2).Min(), 2.27, 2.27 + Chords.Tolerance);
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
    /// The construction in the user's reference band, 1200 × 300 (h = 150): 19 loops 120 wide (1200 = 20 × 60: a whole number,
    /// nothing partial at either end), heads of radius 60 tangent to the faces, those on one face touching (centres 120 apart),
    /// the faces alternating every 60, the first on the top face touching the start, the last touching the end; each loop's two
    /// sides tangent to its head and meeting at its tip on its own axis at the opposite heads' centre height (y = ∓90), exactly
    /// where the two opposite heads beside it touch; a loop's side parallel to its neighbour's facing side; no side crossing another.
    /// </summary>
    [Fact]
    public void The_batt_s_teardrops_fit_the_band_touch_its_faces_and_point_to_where_the_opposite_heads_touch()
    {
        Assert.Equal((19, 120.0, 60.0), BattHatch.Fit(1200, 150));
        foreach (var len in new[] { 1000.0, 1234.5, 777, 400 })
        {
            var (n, w, r) = BattHatch.Fit(len, 150);
            Assert.Equal(len, (n + 1) * w / 2, 9);
            Assert.Equal(w / 2, r, 12);
            Assert.InRange(w, 0.75 * 120, 1.5 * 120);
        }
        // Too short for two loops: one, as wide as the band is long.
        Assert.Equal((1, 100.0, 50.0), BattHatch.Fit(100, 150));

        var strokes = BattHatch.Strokes(0, 1200, 150);
        var straight = strokes.Where(s => s.Count == 2).ToList();
        var arcs = strokes.Where(s => s.Count > 2).ToList();
        Assert.Equal(38, straight.Count);
        Assert.Equal(38, arcs.Count);
        var centres = Enumerable.Range(0, 19).Select(j => new Vec2(60 + 60 * j, j % 2 == 0 ? 90 : -90)).ToList();
        Assert.Equal(0, centres[0].X - 60, 9);
        Assert.Equal(1200, centres[^1].X + 60, 9);
        // Heads on one face touch: centres two radii apart.
        Assert.Equal(120, centres[2].X - centres[0].X, 9);
        // Each head touches its face (its arc split there), the first the top one; every arc point on its head, within the band.
        foreach (var c in centres) Assert.Contains(arcs, a => (a[^1] - new Vec2(c.X, Math.Sign(c.Y) * 150)).Length < 1e-9);
        Assert.All(arcs, a => Assert.All(a, p => Assert.Contains(centres, c => Math.Abs((p - c).Length - 60) < 1e-9)));
        var all = strokes.SelectMany(s => s).ToList();
        Assert.All(all, p => Assert.InRange(p.X, -1e-9, 1200 + 1e-9));
        Assert.All(all, p => Assert.InRange(p.Y, -150 - 1e-9, 150 + 1e-9));
        // Each loop's sides: from its head, tangent there (square to the radius), to its tip, on the circles of the two opposite
        // heads beside it (where they touch), at their centres' height.
        for (var j = 0; j < 19; j++)
        {
            var c = centres[j];
            var tip = new Vec2(c.X, -c.Y);
            var sides = straight.Where(s => (s[1] - tip).Length < 1e-9 && Math.Abs((s[0] - c).Length - 60) < 1e-9).ToList();
            Assert.Equal(2, sides.Count);
            Assert.All(sides, s => Assert.Equal(0, Vec2.Dot(s[1] - s[0], s[0] - c), 6));
            // Symmetric: one on each side of the loop's axis, mirror images.
            Assert.Equal(0, sides[0][0].X - c.X + (sides[1][0].X - c.X), 9);
            Assert.Equal(sides[0][0].Y, sides[1][0].Y, 9);
            foreach (var o in new[] { -60.0, 60 }) Assert.Equal(60, (tip - new Vec2(c.X + o, tip.Y)).Length, 9);
        }
        // The sides leave the heads α below their sides, sin α = R / 2(h − R) = 1/3.
        var alpha = Math.Asin(60.0 / 180);
        Assert.Contains(straight, s => (s[0] - new Vec2(60 + 60 * Math.Cos(alpha), 90 - 60 * Math.Sin(alpha))).Length < 1e-9);
        // A loop's right side and the next loop's left side are parallel (half-turn copies), and no side crosses another.
        var right0 = straight.Single(s => (s[1] - new Vec2(60, -90)).Length < 1e-9 && s[0].X > 60);
        var left1 = straight.Single(s => (s[1] - new Vec2(120, 90)).Length < 1e-9 && s[0].X < 120);
        Assert.Equal(0, Vec2.Cross(right0[1] - right0[0], left1[1] - left1[0]), 6);
        for (var i = 0; i < straight.Count; i++)
            for (var k = i + 1; k < straight.Count; k++)
                Assert.Null(Crossing(straight[i][0], straight[i][1], straight[k][0], straight[k][1]));
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
            Assert.Equal(0.12, bottom, 4);
            // Flipped: the other way round.
            var flipped = BattHatch.Segments([band], flip: true);
            Assert.Equal(0.06, flipped.SelectMany(s => new[] { s.A, s.B }).Where(p => p.Y < 1e-4).Min(p => p.X), 4);
        }
        // Up the page: the first head (at the bottom) on the left face.
        Vec2[] up = [new(0, 0), new(0.3, 0), new(0.3, 1.2), new(0, 1.2)];
        Assert.Equal(0.06, BattHatch.Segments([up]).SelectMany(s => new[] { s.A, s.B }).Where(p => p.X < 1e-4).Min(p => p.Y), 4);
        // The hatch scale stretches the loops, heads with them (then fitted again; held to 0.5–1.15, the fit never past it:
        // 1200 / 69 rounds to 16 loops 141 wide, over 138, so 17 loops 2400/18 wide).
        Assert.Equal((17, 2400.0 / 18, 1200.0 / 18), BattHatch.Fit(1200, 150, 1.15));
        Assert.Equal(BattHatch.Fit(1200, 150, 1.15), BattHatch.Fit(1200, 150, 2));
        Assert.Equal((39, 60.0, 30.0), BattHatch.Fit(1200, 150, 0.5));
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

    /// <summary>
    /// Two bands meeting at a corner, the inner corner at <paramref name="i"/>: the first along <paramref name="a0"/> (radians)
    /// into it, the second turned by <paramref name="turnDeg"/> out of it, <paramref name="t1"/> and <paramref name="t2"/>
    /// thick, the outer faces on the outside of the turn; <paramref name="kind"/> 0 mitred, 1 the first running through (the
    /// second butting against it), 2 the second running through. Each with its axis along its middle from its start.
    /// </summary>
    internal static (Vec2[] A, Vec2[] B, BandAxis AxA, BandAxis AxB, Vec2 I, Vec2 O) Corner(Vec2 i, double a0, double turnDeg, double t1, double t2, double la, double lb, int kind)
    {
        var t = turnDeg * Math.PI / 180;
        var ta = new Vec2(Math.Cos(a0), Math.Sin(a0));
        var tb = new Vec2(Math.Cos(a0 + t), Math.Sin(a0 + t));
        var sg = Math.Sign(t);
        var outA = ta.Perp * -sg; var outB = tb.Perp * -sg;
        var o = Polyline2.LineIntersection(i + outA * t1, ta, i + outB * t2, tb)!.Value;
        var s = i - ta * la; var e = i + tb * lb;
        Vec2[] a, b;
        if (kind == 0) { a = [s, i, o, s + outA * t1]; b = [i, e, e + outB * t2, o]; }
        else if (kind == 1)
        {
            var x = Polyline2.LineIntersection(i, ta, i + outB * t2, tb)!.Value;
            a = [s, x, o, s + outA * t1]; b = [i, e, e + outB * t2, x];
        }
        else
        {
            var y = Polyline2.LineIntersection(i, tb, i + outA * t1, ta)!.Value;
            a = [s, i, y, s + outA * t1]; b = [y, e, e + outB * t2, o];
        }
        return (a, b, BandAxis.Straight(s + outA * (t1 / 2), ta), BandAxis.Straight(i + outB * (t2 / 2), tb), i, o);
    }

    static double Distance(List<HatchSegment> segs, Vec2 p) => segs.Min(s => Polyline2.SegmentDistance(p, s.A, s.B));

    /// <summary>Whether two drawings hold the same segments (either way round), their ends within <paramref name="tol"/>.</summary>
    static bool Same(IEnumerable<HatchSegment> x, IEnumerable<HatchSegment> y, double tol = 1e-7)
    {
        var (a, b) = (x.ToList(), y.ToList());
        if (a.Count != b.Count) return false;
        var cell = 10 * tol;
        (long, long) K(Vec2 p) => ((long)Math.Floor(p.X / cell), (long)Math.Floor(p.Y / cell));
        var grid = new Dictionary<(long, long), List<int>>();
        for (var i = 0; i < b.Count; i++) { var k = K(b[i].A); (grid.TryGetValue(k, out var l) ? l : grid[k] = []).Add(i); k = K(b[i].B); (grid.TryGetValue(k, out l) ? l : grid[k] = []).Add(i); }
        var used = new bool[b.Count];
        bool Near(Vec2 p, Vec2 q) => (p - q).Length <= tol;
        foreach (var s in a)
        {
            var (kx, ky) = K(s.A);
            var found = false;
            for (var dx = -1; dx <= 1 && !found; dx++)
                for (var dy = -1; dy <= 1 && !found; dy++)
                    if (grid.TryGetValue((kx + dx, ky + dy), out var l))
                        foreach (var i in l)
                            if (!used[i] && ((Near(s.A, b[i].A) && Near(s.B, b[i].B)) || (Near(s.A, b[i].B) && Near(s.B, b[i].A)))) { used[i] = true; found = true; break; }
            if (!found) return false;
        }
        return true;
    }

    static List<(long, long, long, long)> Keys(IEnumerable<HatchSegment> segs, Func<Vec2, Vec2>? map = null)
    {
        map ??= p => p;
        (long, long) K(Vec2 p) { var q = map(p); return ((long)Math.Round(q.X * 1e6), (long)Math.Round(q.Y * 1e6)); }
        return [.. segs.Select(s => { var (a, b) = (K(s.A), K(s.B)); return a.CompareTo(b) <= 0 ? (a.Item1, a.Item2, b.Item1, b.Item2) : (b.Item1, b.Item2, a.Item1, a.Item2); }).Order()];
    }

    /// <summary>
    /// For random corners (turning 20°–135° either way, mitred or butted either way, 40–300 mm, any direction, either flip,
    /// at any hatch scale), two bands drawn together: every stroke belongs to a whole loop inside the two bands; along the
    /// joined mid-line, round the corner too, never more than a loop width from a stroke; the outer corner within a loop
    /// width of a stroke (corners sharper than about 60° within reach of the largest head the fan draws, 1.7 times the
    /// chain's, tangent to both faces); the same drawn
    /// mirrored (the flip mirrored with it) and with the bands given the other way round; and of two thicknesses, the
    /// two drawn as each alone.
    /// </summary>
    [Fact]
    public void At_a_corner_two_bands_batts_go_round_it_as_one_in_whole_loops()
    {
        var rnd = new Random(7541);
        var (equal, apart) = (0, new List<string>());
        for (var n = 0; n < 400; n++)
        {
            var thick = 0.04 + rnd.NextDouble() * 0.26;
            var turn = (20 + rnd.NextDouble() * 115) * (rnd.Next(2) == 0 ? 1 : -1);
            var kind = Math.Abs(turn) is > 45 and < 120 ? rnd.Next(3) : 0;
            var a0 = rnd.NextDouble() * 2 * Math.PI;
            var i = new Vec2(rnd.NextDouble() * 20 - 10, rnd.NextDouble() * 20 - 10);
            double la = 0.6 + rnd.NextDouble() * 3, lb = 0.6 + rnd.NextDouble() * 3;
            var scale = rnd.Next(3) == 0 ? 0.7 + rnd.NextDouble() * 0.4 : 1;
            var flip = rnd.Next(2) == 0;
            var unequal = rnd.Next(8) == 0;
            var t2 = unequal ? thick * (rnd.Next(2) == 0 ? 0.6 : 1.5) : thick;
            var what = $"corner {n}: turn {turn:0.#}°, {(kind == 0 ? "mitred" : kind == 1 ? "first through" : "second through")}, {thick:0.###}/{t2:0.###}, scale {scale:0.##}{(flip ? ", flipped" : "")}";
            var (a, b, ax, bx, ic, oc) = Corner(i, a0, turn, thick, t2, la, lb, kind);
            var drawn = BattHatch.Segments([new BattHatch.BattBand([a], ax, scale, flip), new BattHatch.BattBand([b], bx, scale, flip)]);
            var segs = drawn.SelectMany(x => x).ToList();
            if (unequal)
            {
                // Two thicknesses: each ends on whole loops, as alone.
                Assert.Equal(Keys(BattHatch.Segments([a], axis: ax, scale: scale, flip: flip)), Keys(drawn[0]));
                Assert.Equal(Keys(BattHatch.Segments([b], axis: bx, scale: scale, flip: flip)), Keys(drawn[1]));
                continue;
            }
            // Turned (not laid as two ends): the two drawn otherwise than each alone.
            equal++;
            if (Same(segs, BattHatch.Segments([a], axis: ax, scale: scale, flip: flip).Concat(BattHatch.Segments([b], axis: bx, scale: scale, flip: flip)))) apart.Add(what);
            WholeLoops(segs, what);
            Assert.All(segs.SelectMany(s => new[] { s.A, s.B }), p => Assert.True(Polygon2.Contains(a, p, 1e-6) || Polygon2.Contains(b, p, 1e-6), $"{what}: a point outside the bands"));
            var h = thick / 2;
            var w0 = BattHatch.Head * 2 * h * scale;
            // Along the joined mid-line: from a loop width in from each free end, through the corner of the mid-lines.
            var t = turn * Math.PI / 180;
            var ta = new Vec2(Math.Cos(a0), Math.Sin(a0));
            var tb = new Vec2(Math.Cos(a0 + t), Math.Sin(a0 + t));
            var sg = Math.Sign(t);
            var cm = Polyline2.LineIntersection(ic + ta.Perp * (-sg * h), ta, ic + tb.Perp * (-sg * h), tb)!.Value;
            var start = ic - ta * la + ta.Perp * (-sg * h) + ta * (w0 + 2 * h);
            var end = ic + tb * lb + tb.Perp * (-sg * h) - tb * (w0 + 2 * h);
            // Every half loop width (a gap of a loop width cannot hide between two samples): a stroke within a loop width, looked
            // up in a grid of the strokes' points (every quarter loop width along each).
            var grid = new HashSet<(long, long)>();
            foreach (var x in segs)
            {
                var m = Math.Max(1, (int)Math.Ceiling((x.B - x.A).Length / (w0 / 4)));
                for (var k = 0; k <= m; k++) { var pt = x.A + (x.B - x.A) * ((double)k / m); grid.Add(((long)Math.Floor(pt.X / (w0 / 4)), (long)Math.Floor(pt.Y / (w0 / 4)))); }
            }
            bool Covered(Vec2 x)
            {
                var (cx, cy) = ((long)Math.Floor(x.X / (w0 / 4)), (long)Math.Floor(x.Y / (w0 / 4)));
                for (var dx = -3; dx <= 3; dx++) for (var dy = -3; dy <= 3; dy++) if (grid.Contains((cx + dx, cy + dy))) return true;
                return false;
            }
            foreach (var (p, q) in new[] { (start, cm), (cm, end) })
            {
                var steps = Math.Max(1, (int)Math.Ceiling((q - p).Length / (w0 / 2)));
                for (var k = 0; k <= steps; k++)
                {
                    var x = p + (q - p) * ((double)k / steps);
                    Assert.True(Covered(x), $"{what}: no stroke within a loop width ({w0:0.####}) of the mid-line at {k}/{steps}");
                }
            }
            // The outer corner: within a loop width of a stroke, or of the head that fits it (tangent to both faces).
            var interior = Math.PI - Math.Abs(t);
            var reach = Math.Max(w0, 1.7 * w0 / 2 * (1 / Math.Sin(interior / 2) - 1)) * 1.1;
            Assert.True(Distance(segs, oc) <= reach, $"{what}: the outer corner {Distance(segs, oc):0.####} from a stroke (reach {reach:0.####})");
            // Mirrored (the flip with it): the same drawing mirrored.
            Vec2 M(Vec2 p) => new(2 * i.X - p.X, p.Y);
            var (ma, mb) = (a.Select(M).ToArray(), b.Select(M).ToArray());
            BandAxis Mirror(Vec2 origin, Vec2 dir) => BandAxis.Straight(M(origin), new Vec2(-dir.X, dir.Y));
            var axo = ax.FromBand(0, 0); var axd = ax.FromBand(1, 0) - axo;
            var bxo = bx.FromBand(0, 0); var bxd = bx.FromBand(1, 0) - bxo;
            var mirrored = BattHatch.Segments([new BattHatch.BattBand([ma], Mirror(axo, axd), scale, !flip), new BattHatch.BattBand([mb], Mirror(bxo, bxd), scale, !flip)]).SelectMany(x => x);
            Assert.True(Same(segs.Select(x => new HatchSegment(M(x.A), M(x.B))), mirrored, 1e-6), $"{what}: mirrored, drawn otherwise");
            // The bands the other way round: the same strokes.
            var swapped = BattHatch.Segments([new BattHatch.BattBand([b], bx, scale, flip), new BattHatch.BattBand([a], ax, scale, flip)]).SelectMany(x => x);
            Assert.True(Same(segs, swapped, 1e-6), $"{what}: given the other way round, drawn otherwise");
        }
        // Every corner of one thickness turned.
        Assert.True(apart.Count == 0, $"{apart.Count} of {equal} corners laid as two ends: {string.Join("; ", apart.Take(8))}");
    }

    /// <summary>
    /// The user's corner (two walls of a 150 mm wool layer, mitred at a right angle): the chain goes round it, a fan of
    /// loops in the corner square with a head tangent to both outer faces, every loop whole; drawn alone each band keeps
    /// its own whole loops as before. Bands that may not join (another material), a T (the through band runs past the
    /// other's outer face) and a band with a hole draw as alone.
    /// </summary>
    [Fact]
    public void An_L_of_one_wool_layer_draws_one_batt_round_the_corner_and_a_T_two()
    {
        Vec2[] along = [new(0, 0), new(3.15, 0), new(3, 0.15), new(0, 0.15)];
        Vec2[] up = [new(3.15, 0), new(3.15, 3), new(3, 3), new(3, 0.15)];
        var ax = BandAxis.Straight(new Vec2(0, 0.075), new Vec2(1, 0));
        var ux = BandAxis.Straight(new Vec2(3.075, 0), new Vec2(0, 1));
        var drawn = BattHatch.Segments([new BattHatch.BattBand([along], ax), new BattHatch.BattBand([up], ux)]);
        var all = drawn.SelectMany(x => x).ToList();
        WholeLoops(all, "L");
        // The corner square (3–3.15 × 0–0.15) holds strokes, one of them the head in the outer corner: tangent to both faces.
        var inCorner = all.SelectMany(s => new[] { s.A, s.B }).Where(p => p.X > 3 + 1e-6 && p.Y < 0.15 - 1e-6).ToList();
        Assert.True(inCorner.Count > 20, $"{inCorner.Count} points in the corner square");
        Assert.True(Distance(all, new Vec2(3.15, 0)) < 0.03 * Math.Sqrt(2), $"outer corner {Distance(all, new Vec2(3.15, 0)):0.####} from a stroke");
        // Not joined (another material): each as alone.
        var apart = BattHatch.Segments([new BattHatch.BattBand([along], ax), new BattHatch.BattBand([up], ux)], joins: (_, _) => false);
        Assert.Equal(Keys(BattHatch.Segments([along], axis: ax)), Keys(apart[0]));
        Assert.Equal(Keys(BattHatch.Segments([up], axis: ux)), Keys(apart[1]));
        // A T: the through band runs on past the joining one; each as alone.
        Vec2[] through = [new(0, 0), new(6, 0), new(6, 0.15), new(0, 0.15)];
        Vec2[] stem = [new(3, 0.15), new(3.15, 0.15), new(3.15, 3), new(3, 3)];
        var tx = BandAxis.Straight(new Vec2(0, 0.075), new Vec2(1, 0));
        var sx = BandAxis.Straight(new Vec2(3.075, 0.15), new Vec2(0, 1));
        var t = BattHatch.Segments([new BattHatch.BattBand([through], tx), new BattHatch.BattBand([stem], sx)]);
        Assert.Equal(Keys(BattHatch.Segments([through], axis: tx)), Keys(t[0]));
        Assert.Equal(Keys(BattHatch.Segments([stem], axis: sx)), Keys(t[1]));
        // A band alone is laid as before, square ends and all.
        Vec2[] box = [new(0, 0), new(2, 0), new(2, 0.1), new(0, 0.1)];
        Assert.Equal(Keys(BattHatch.Segments([box])), Keys(BattHatch.Segments([new BattHatch.BattBand([box])])[0]));
    }

    /// <summary>
    /// A closed ring of four walls (a 150 mm layer round a 4 × 3 m room, mitred): every corner turned, the loops whole, and
    /// nowhere along the ring's mid-line more than a loop width from a stroke.
    /// </summary>
    [Fact]
    public void A_closed_ring_of_four_walls_turns_every_corner()
    {
        double w = 4, hh = 3, d = 0.15;
        Vec2[] inner = [new(0, 0), new(w, 0), new(w, hh), new(0, hh)];
        Vec2[] outer = [new(-d, -d), new(w + d, -d), new(w + d, hh + d), new(-d, hh + d)];
        var bands = new List<BattHatch.BattBand>();
        for (var k = 0; k < 4; k++)
        {
            Vec2[] q = [inner[k], outer[k], outer[(k + 1) % 4], inner[(k + 1) % 4]];
            var dir = (inner[(k + 1) % 4] - inner[k]).Normalized();
            bands.Add(new BattHatch.BattBand([q], BandAxis.Straight(inner[k] + dir.Perp * (-d / 2), dir)));
        }
        var all = BattHatch.Segments(bands).SelectMany(x => x).ToList();
        WholeLoops(all, "ring");
        var w0 = BattHatch.Head * d;
        Vec2[] mid = [new(-d / 2, -d / 2), new(w + d / 2, -d / 2), new(w + d / 2, hh + d / 2), new(-d / 2, hh + d / 2)];
        for (var k = 0; k < 4; k++)
            for (var j = 0; j <= 100; j++)
            {
                var p = mid[k] + (mid[(k + 1) % 4] - mid[k]) * (j / 100.0);
                Assert.True(Distance(all, p) <= w0, $"side {k}, {j}/100: {Distance(all, p):0.####} from a stroke");
            }
        foreach (var o in outer) Assert.True(Distance(all, o) < w0, $"outer corner {o}");
    }

    /// <summary>
    /// Foam board's circles and rigid foam's rounded squares and diamonds are drawn whole or not at all where a layer ends
    /// (a mitre, a cut), never cut to a "C" (every stroke in a closed outline, inside the band); a lattice tile (one whose
    /// motifs touch their copies) and line patterns are clipped as before; the flag goes through the .pat text.
    /// </summary>
    [Fact]
    public void Closed_tile_motifs_are_drawn_whole_where_a_layer_ends()
    {
        // A 15 × 4.3 mm band (paper mm at scale 1), mitred at one end.
        Vec2[] band = [new(0, 0), new(15, 0), new(10.7, 4.3), new(0, 4.3)];
        foreach (var name in new[] { "FOAMBOARD", "RIGIDFOAM" })
        {
            var p = HatchLibrary.Find(name)!;
            Assert.True(p.Tile!.Whole, name);
            foreach (var (angle, origin) in new[] { (0.0, new Vec2(0, 0)), (17.0, new Vec2(0.37, 0.91)), (90.0, new Vec2(-1.3, 2.2)) })
            {
                var (segs, _) = HatchFill.Segments([band], p, new HatchOptions(1, angle, origin));
                Assert.True(segs.Count > 12, $"{name} {angle}: {segs.Count}");
                Assert.All(segs.SelectMany(x => new[] { x.A, x.B }), q => Assert.True(Polygon2.Contains(band, q, 1e-9), $"{name} {angle}: a point outside"));
                // Rigid foam's diamonds sit on the cells' corners, apart from its squares: each its own closed outline too.
                WholeLoops(segs, $"{name} {angle}");
            }
        }
        // Clipped as before: a lattice of lozenges (each touching its neighbours) leaves its cut pieces at the edge.
        var lattice = new HatchPattern("LOZ", "", []) { Tile = new HatchTile(4, 2, [new(new(0, 1), new(2, 0)), new(new(2, 0), new(4, 1)), new(new(4, 1), new(2, 2)), new(new(2, 2), new(0, 1))], []) { Whole = true } };
        var (cut, _) = HatchFill.Segments([band], lattice, new HatchOptions(1, 0, new Vec2(0.3, 0.2)));
        Assert.Contains(cut, x => Math.Abs(x.A.X) < 1e-9 || Math.Abs(x.B.X) < 1e-9);
        // The flag goes through the .pat text, and a user's tile has it off.
        var back = Pat.Parse(Pat.Serialize([HatchLibrary.Find("FOAMBOARD")!])).Patterns.Single();
        Assert.True(back.Tile!.Whole);
        Assert.Equal(HatchLibrary.Find("FOAMBOARD")!.Tile, back.Tile);
        Assert.False(TileFit.Make("CIRCLES", "", HatchUnits.Paper, 2, 2, [], [new TileArc(new Vec2(1, 1), 0.6, 0, 360)]).Tile!.Whole);
    }

    /// <summary>Drawing a mitred L together costs no more than about twice drawing its two bands alone.</summary>
    [Fact]
    public void Turning_a_corner_costs_about_what_laying_the_bands_does()
    {
        Vec2[] along = [new(0, 0), new(3.15, 0), new(3, 0.15), new(0, 0.15)];
        Vec2[] up = [new(3.15, 0), new(3.15, 3), new(3, 3), new(3, 0.15)];
        var ax = BandAxis.Straight(new Vec2(0, 0.075), new Vec2(1, 0));
        var ux = BandAxis.Straight(new Vec2(3.075, 0), new Vec2(0, 1));
        for (var k = 0; k < 5; k++) { BattHatch.Segments([along], axis: ax); BattHatch.Segments([new BattHatch.BattBand([along], ax), new BattHatch.BattBand([up], ux)]); }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var k = 0; k < 40; k++) { BattHatch.Segments([along], axis: ax); BattHatch.Segments([up], axis: ux); }
        var alone = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        for (var k = 0; k < 40; k++) BattHatch.Segments([new BattHatch.BattBand([along], ax), new BattHatch.BattBand([up], ux)]);
        var together = sw.Elapsed.TotalMilliseconds;
        Assert.True(together <= 2.5 * alone + 20, $"together {together:0.#} ms, alone {alone:0.#} ms (40 runs)");
    }
}
