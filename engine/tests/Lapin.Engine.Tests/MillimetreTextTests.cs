using System.Globalization;
using Lapin.Model;
using Lapin.Rebar;

namespace Lapin.Engine.Tests;

/// <summary>
/// The millimetre texts left after audit stage 2 (§9): a bar's diameter now through <see cref="Numbers.Mm"/>, with the same
/// text for every diameter; the two kept on their own (a joinery schedule's sizes, a document's number under 1 cm) because
/// the one way writes „0” where they write „-0” for a value between −0.5 mm and 0, and their text was to stay the same.
/// </summary>
public sealed class MillimetreTextTests
{
    static string Before(double m) => Math.Round(m * 1000).ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void A_diameter_reads_as_it_always_did()
    {
        for (var mm = 4.0; mm <= 60; mm += 0.25) Assert.Equal("Ø" + Before(mm / 1000), RebarShapes.Dia(mm / 1000));
        Assert.Equal("Ø12", RebarShapes.Dia(0.012));
        Assert.Equal("Ø12", RebarShapes.Dia(0.0125));
    }

    [Fact]
    public void The_two_left_on_their_own_would_say_minus_zero_otherwise()
    {
        Assert.Equal("-0", Before(-0.0004));
        Assert.Equal("0", Numbers.Mm(-0.0004));
        // Every other value the same.
        foreach (var m in new[] { 0, 0.0004, 0.0125, 0.9, 1.25, -0.3, -2.0005 }) Assert.Equal(Before(m), Numbers.Mm(m));
    }
}
