using Lapin.Cost;

namespace Lapin.Engine.Tests;

/// <summary>
/// Audit 15.11.2026, P19: money rounded half away from zero on the double came out a ban short where the decimal number
/// written is a half (1.005 is 1.00499… as a double). Rounded as the decimal it is written as now, so every line, sum and
/// total of the estimate and its forms (F3, F2, C6–C9, which all round through it) gets its ban.
/// </summary>
public class MoneyRoundingTests
{
    [Theory]
    [InlineData(1.005, 1.01)]
    [InlineData(2.675, 2.68)]
    [InlineData(1.015, 1.02)]
    [InlineData(10.125, 10.13)]
    [InlineData(-1.005, -1.01)]
    [InlineData(0.004999, 0)]
    [InlineData(1234567.895, 1234567.9)]
    [InlineData(3.14159, 3.14)]
    public void Money_is_rounded_to_the_ban_half_away_from_zero_as_written(double v, double expected) =>
        Assert.Equal(expected, Money.Round(v));

    [Fact]
    public void A_products_half_ban_rounds_up()
    {
        // 1.5 × 0.67 = 1.005 as written, 1.0050000000000001 or 1.00499… as a double: one ban more either way.
        Assert.Equal(1.01, Money.Round(1.5 * 0.67));
        Assert.Equal(5.01, Money.Round(100.1 * 5 / 100));
        var line = new EstimateLine("k", "Chapter", "C001") { Quantity = 1.5, M = 0.67 };
        Assert.Equal(1.01, line.VM);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1e30)]
    public void Numbers_beyond_a_decimal_do_not_throw(double v)
    {
        var r = Money.Round(v);
        Assert.True(double.IsNaN(v) ? double.IsNaN(r) : r == v);
    }
}
