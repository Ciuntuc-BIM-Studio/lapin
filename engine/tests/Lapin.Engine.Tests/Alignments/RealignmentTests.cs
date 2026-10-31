using Lapin.Alignments;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// A design proposed from an existing track (Realignment): a line laid out from PIs and PVIs, then worn — moved
/// across and up or down by long waves of a few centimetres and some millimetres of noise, as a track measured
/// after years of traffic — read back within the shifts a tamping machine may make.
/// </summary>
public class RealignmentTests
{
    static readonly List<AlignmentPi> Design = [new(0, 0), new(400, 0, 700, 40, 40), new(900, 280)];

    /// <summary>The design worn every half metre: across by <paramref name="across"/> m in long waves, up and down by <paramref name="up"/> m, noise of a few mm; heights on a 2 ‰ grade from 10 m.</summary>
    static List<Vec3> Worn(IReadOnlyList<AlignmentPi> pis, double across = 0.03, double up = 0.03, Func<double, double>? extra = null)
    {
        var a = PiAlignment.Build(pis);
        var o = new List<Vec3>();
        uint seed = 777;
        double Next() { seed = seed * 1664525u + 1013904223u; return (seed >> 8) / (double)(1 << 24) * 2 - 1; }
        for (double s = 0; s <= a.Length; s += 0.5)
        {
            var p = a.At(s);
            var n = new Vec2(-Math.Sin(p.Direction), Math.Cos(p.Direction));
            var lateral = across * (0.7 * Math.Sin(s / 37) + 0.3 * Math.Sin(s / 11 + 1)) + 0.002 * Next() + (extra?.Invoke(s) ?? 0);
            var q = new Vec2(p.X, p.Y) + n * lateral;
            var z = 10 + 0.002 * s + up * (0.6 * Math.Sin(s / 23) + 0.4 * Math.Sin(s / 9 + 2)) + 0.002 * Next();
            o.Add(new Vec3(q.X, q.Y, z));
        }
        return o;
    }

    static (List<AlignmentPi> Pis, Realignment.HorizontalResult H, Realignment.VerticalResult V) Run(List<Vec3> measured, RealignOptions o)
    {
        var plan = measured.Select(p => new Vec2(p.X, p.Y)).ToList();
        var fit = AlignmentFit.FromMeasured(plan);
        var h = Realignment.Horizontal(fit.Pis, plan, o);
        var length = PiAlignment.Build(h.Pis).Length;
        var v = Realignment.Vertical(null, length, Realignment.Along(plan), h.Station, measured.Select(p => p.Z).ToList(), o);
        return (h.Pis, h, v);
    }

    [Fact]
    public void A_worn_track_gets_a_design_within_five_centimetres_either_way_across_and_up()
    {
        var measured = Worn(Design);
        var (pis, h, v) = Run(measured, new RealignOptions());
        Assert.Equal(0, h.MaxExcess, 6);
        Assert.Equal(0, v.MaxExcess, 6);
        Assert.True(h.Slew.All(x => Math.Abs(x) <= 0.05), $"slew up to {h.Slew.Max(Math.Abs)}");
        Assert.True(v.Lift.All(x => Math.Abs(x) <= 0.05), $"lift up to {v.Lift.Max(Math.Abs)}");
        Assert.Equal(3, pis.Count);
        Assert.True(Math.Abs(pis[1].Radius - 700) < 60, $"radius {pis[1].Radius}");
        Assert.True(v.Pvis.Count >= 2);

        // The shifts read back from the design: the same as the realignment reported.
        var back = Realignment.Of(pis, v.Pvis, measured);
        Assert.True(back.Slew.Zip(h.Slew).All(x => Math.Abs(x.First - x.Second) < 1e-4));
        Assert.True(back.Lift.Zip(v.Lift).All(x => Math.Abs(x.First - x.Second) < 1e-4));
    }

    [Fact]
    public void A_fixed_point_holds_the_track_within_its_band()
    {
        var measured = Worn(Design);
        // A bridge from 200 to 240 m: across within ± 3 mm.
        var (_, h, _) = Run(measured, new RealignOptions { SlewBands = [new(200, 240, -0.003, 0.003)] });
        var along = Realignment.Along(measured.Select(p => new Vec2(p.X, p.Y)).ToList());
        var on = Enumerable.Range(0, along.Length).Where(i => along[i] >= 200 && along[i] <= 240).ToList();
        var worst = on.Max(i => Math.Abs(h.Slew[i]));
        // The noise alone is ± 2 mm and a straight cannot bend with what is left of the wave there: a few mm past the band at most.
        Assert.True(worst <= 0.006, $"slew on the bridge up to {worst}");
        Assert.True(h.Slew.All(x => Math.Abs(x) <= 0.051), $"slew elsewhere up to {h.Slew.Max(Math.Abs)}");
    }

    [Fact]
    public void A_tamping_that_only_lifts_keeps_every_lift_up()
    {
        var measured = Worn(Design);
        var (_, _, v) = Run(measured, new RealignOptions { LiftBands = [new(0, 1e9, 0, 0.06)] });
        Assert.True(v.Lift.All(x => x >= -0.002 && x <= 0.061), $"lifts from {v.Lift.Min()} to {v.Lift.Max()}");
        Assert.Equal(0, v.MaxExcess, 2);
    }

    [Fact]
    public void A_kink_too_big_for_the_limits_is_reported_not_hidden()
    {
        // 15 cm out over 30 m in the middle of the straight: no curve of the design can follow that within 5 cm.
        var measured = Worn(Design, extra: s => s is > 150 and < 180 ? 0.15 * Math.Sin((s - 150) / 30 * Math.PI) : 0);
        var (_, h, _) = Run(measured, new RealignOptions());
        Assert.True(h.MaxExcess > 0.02, $"excess {h.MaxExcess}");
    }

    [Fact]
    public void A_long_wave_on_a_straight_bigger_than_the_limits_gets_curves_of_large_radius()
    {
        // 9 cm either way over a 600 m wave on a 1 km straight: a straight cannot hold it within 5 cm, gentle curves can.
        List<AlignmentPi> straight = [new(0, 0), new(1000, 0)];
        var measured = Worn(straight, across: 0, up: 0.01, extra: s => 0.09 * Math.Sin(s / 1000 * 2 * Math.PI * 1.6));
        var plan = measured.Select(p => new Vec2(p.X, p.Y)).ToList();
        var h = Realignment.Horizontal(straight, plan, new RealignOptions());
        Assert.True(h.Added >= 1, $"{h.Added} added");
        Assert.True(h.MaxExcess < 0.002, $"excess {h.MaxExcess}, added {h.Added}: {string.Join(" | ", h.Pis.Select(p => $"{p.X:0.###},{p.Y:0.####} R{p.Radius:0}"))}");
        Assert.All(h.Pis.Skip(1).SkipLast(1), p => Assert.True(p.Radius >= 2000, $"radius {p.Radius}"));
    }

    [Fact]
    public void A_locked_PI_stays_where_it_is()
    {
        var measured = Worn(Design);
        var plan = measured.Select(p => new Vec2(p.X, p.Y)).ToList();
        var fit = AlignmentFit.FromMeasured(plan);
        var h = Realignment.Horizontal(fit.Pis, plan, new RealignOptions { Locked = [1] });
        // Curves added around it move it along the list: the result says where it is now.
        var i = Assert.Single(h.Locked);
        Assert.Equal(fit.Pis[1].X, h.Pis[i].X, 3);
        Assert.Equal(fit.Pis[1].Y, h.Pis[i].Y, 3);
        Assert.Equal(fit.Pis[1].Radius, h.Pis[i].Radius, 0);
    }

    [Fact]
    public void Five_kilometres_with_five_curves_are_realigned_in_seconds()
    {
        List<AlignmentPi> line = [new(0, 0), new(800, 0, 900, 60, 60), new(1600, 400, 1200, 80, 80), new(2500, 450, 800, 60, 60), new(3200, 100, 1500, 50, 50), new(4200, 300, 1000, 60, 60), new(5000, 900)];
        var measured = Worn(line);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var (pis, h, v) = Run(measured, new RealignOptions());
        clock.Stop();
        Assert.True(clock.Elapsed.TotalSeconds < 30, $"{clock.Elapsed.TotalSeconds:0.0} s for {measured.Count} points");
        Assert.Equal(7, pis.Count);
        Assert.Equal(0, h.MaxExcess, 3);
        Assert.Equal(0, v.MaxExcess, 3);
    }
}
