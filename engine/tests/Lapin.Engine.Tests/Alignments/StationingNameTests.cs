using Lapin.Alignments;

namespace Lapin.Engine.Tests;

/// <summary>The names generated referents get: a whole kilometre "km N", else the kilometrage.</summary>
public class StationingNameTests
{
    [Fact]
    public void A_post_at_zero_or_a_hair_under_it_is_km_0_never_km_minus_0()
    {
        Assert.Equal("km 0", Stationing.ReferentName(0));
        Assert.Equal("km 0", Stationing.ReferentName(-1e-12));
        Assert.Equal("km 12", Stationing.ReferentName(12000));
        Assert.Equal("1+200", Stationing.ReferentName(1200));
    }
}
