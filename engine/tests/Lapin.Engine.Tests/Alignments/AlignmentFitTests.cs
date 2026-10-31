using Lapin.Alignments;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>A design alignment read back from a measured centre line (AlignmentFit): points every half metre, a few millimetres of noise.</summary>
public class AlignmentFitTests
{
    /// <summary>The PI chain sampled every half metre, each point moved by up to <paramref name="noise"/> m either way (a fixed sequence).</summary>
    static List<Vec2> Measured(IReadOnlyList<AlignmentPi> pis, double noise = 0.004)
    {
        var a = PiAlignment.Build(pis);
        var o = new List<Vec2>();
        uint seed = 12345;
        double Next() { seed = seed * 1664525u + 1013904223u; return (seed >> 8) / (double)(1 << 24) * 2 - 1; }
        for (double s = 0; s <= a.Length; s += 0.5)
        {
            var p = a.At(s);
            o.Add(new Vec2(p.X + Next() * noise, p.Y + Next() * noise));
        }
        return o;
    }

    [Fact]
    public void A_straight_line_is_two_PIs()
    {
        var fit = AlignmentFit.FromMeasured(Measured([new(0, 0), new(300, 40)]));
        Assert.Equal(2, fit.Pis.Count);
        Assert.Equal(0, fit.Curves);
        Assert.True(fit.MaxDeviation < 0.02, $"deviation {fit.MaxDeviation}");
        // The last sample lies half a metre short of the design's end at most; the fitted end is there, on the line.
        var end = fit.Pis[^1].P;
        Assert.True((end - new Vec2(300, 40)).Length < 0.5 && Math.Abs(end.X * 40 - end.Y * 300) / Math.Sqrt(300 * 300 + 40 * 40) < 0.02, $"end at {end.X}, {end.Y}");
    }

    [Fact]
    public void A_curve_between_two_straights_comes_back_with_its_radius_clothoids_and_PI()
    {
        var design = new List<AlignmentPi> { new(0, 0), new(400, 0, 600, 40, 40), new(800, 300) };
        var fit = AlignmentFit.FromMeasured(Measured(design));
        Assert.Equal(3, fit.Pis.Count);
        Assert.Equal(1, fit.Curves);
        var pi = fit.Pis[1];
        Assert.True(Math.Abs(pi.Radius - 600) < 18, $"radius {pi.Radius}");
        Assert.True((pi.P - new Vec2(400, 0)).Length < 0.5, $"PI at {pi.P.X}, {pi.P.Y}");
        Assert.True(Math.Abs(pi.SpiralIn - 40) <= 12 && Math.Abs(pi.SpiralOut - 40) <= 12, $"spirals {pi.SpiralIn}, {pi.SpiralOut}");
        Assert.True(fit.MaxDeviation < 0.06, $"deviation {fit.MaxDeviation}");
    }

    [Fact]
    public void A_line_that_is_all_curve_takes_its_own_end_tangents()
    {
        // 300 m of a 1200 m arc, no straight either side: the PIs are the ends and where the end tangents meet.
        var design = new List<AlignmentPi> { new(-2000, 0), new(0, 0, 1200), new(2000, 600) };
        var all = Measured(design);
        var built = PiAlignment.Build(design);
        var arc = built.Segments.First(x => x.Kind == PiSegmentKind.Arc);
        var part = all.Where((_, i) => i * 0.5 >= arc.S + 5 && i * 0.5 <= arc.S + 305).ToList();
        var fit = AlignmentFit.FromMeasured(part);
        Assert.Equal(3, fit.Pis.Count);
        Assert.True(Math.Abs(fit.Pis[1].Radius - 1200) < 40, $"radius {fit.Pis[1].Radius}");
        Assert.True(fit.MaxDeviation < 0.06, $"deviation {fit.MaxDeviation}");
    }

    [Fact]
    public void A_curve_at_the_start_then_a_long_straight_as_a_track_survey_often_begins()
    {
        var design = new List<AlignmentPi> { new(-3000, 0), new(0, 0, 1500), new(400, 120) };
        var all = Measured(design);
        var built = PiAlignment.Build(design);
        var arc = built.Segments.First(x => x.Kind == PiSegmentKind.Arc);
        var from = arc.S + arc.Length - 30;
        var part = all.Where((_, i) => i * 0.5 >= from).ToList();
        var fit = AlignmentFit.FromMeasured(part);
        Assert.Equal(3, fit.Pis.Count);
        Assert.Equal(1, fit.Curves);
        Assert.True(Math.Abs(fit.Pis[1].Radius - 1500) < 150, $"radius {fit.Pis[1].Radius}");
        Assert.True(fit.MaxDeviation < 0.06, $"deviation {fit.MaxDeviation}");
    }
}
