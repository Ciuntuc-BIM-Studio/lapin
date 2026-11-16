using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>The one number reader (audit 15.11.2026, D2): a decimal point or comma, thousands groups, a true minus, an exponent.</summary>
public class NumbersTests
{
    [Theory]
    [InlineData("12", 12)]
    [InlineData(" 12,5 ", 12.5)]
    [InlineData("12.5", 12.5)]
    [InlineData("1,200", 1.2)]
    [InlineData("−3", -3)]
    [InlineData("-3", -3)]
    [InlineData("+3.20", 3.2)]
    [InlineData("1e3", 1000)]
    [InlineData("2.5E-3", 0.0025)]
    [InlineData(".5", 0.5)]
    [InlineData("5.", 5)]
    public void Strict_reads_a_whole_number(string text, double value) => Assert.Equal(value, Numbers.Parse(text, NumberMode.Strict)!.Value, 12);

    [Theory]
    [InlineData("1 250")]
    [InlineData("1.234,5")]
    [InlineData("12 m")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e999")]
    [InlineData("-")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("0x10")]
    public void Strict_refuses_anything_else(string? text) => Assert.Null(Numbers.Parse(text, NumberMode.Strict));

    [Theory]
    [InlineData("1,200", 1.2)]
    [InlineData("1 250", 1250)]
    [InlineData("1 250", 1250)]
    [InlineData("1 250", 1250)]
    [InlineData("1'250", 1250)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("1 234 567,89", 1234567.89)]
    [InlineData("1.234.567", 1234567)]
    [InlineData("−1 250,5", -1250.5)]
    [InlineData("12,5", 12.5)]
    [InlineData("1e3", 1000)]
    public void Grouped_reads_thousands(string text, double value) => Assert.Equal(value, Numbers.Parse(text, NumberMode.Grouped)!.Value, 9);

    [Theory]
    [InlineData("1 25")]
    [InlineData("1234 567")]
    [InlineData("1.2.3")]
    [InlineData("1,234,5")]
    [InlineData("1 234.567,8.9")]
    [InlineData("12 m")]
    public void Grouped_refuses_wrong_groups(string text) => Assert.Null(Numbers.Parse(text, NumberMode.Grouped));

    [Theory]
    [InlineData("12,5 m²", 12.5)]
    [InlineData("40 %", 40)]
    [InlineData("1:100", 1)]
    [InlineData("1 250 mm", 1250)]
    [InlineData("12 5 m", 12)]
    [InlineData("71mm", 71)]
    [InlineData("1e3 m", 1000)]
    [InlineData("12e m", 12)]
    [InlineData("−3 °C", -3)]
    public void Leading_reads_the_number_a_text_starts_with(string text, double value) => Assert.Equal(value, Numbers.Parse(text, NumberMode.Leading)!.Value, 9);

    [Theory]
    [InlineData("EI 30")]
    [InlineData("1.2.3")]
    [InlineData("—")]
    [InlineData("m 12")]
    public void Leading_refuses_a_text_that_starts_with_no_number(string text) => Assert.Null(Numbers.Parse(text, NumberMode.Leading));

    [Theory]
    [InlineData("L = 12 m", 12)]
    [InlineData("Level +3.20", 3.2)]
    [InlineData("a-3", -3)]
    [InlineData("1 250", 1250)]
    [InlineData("scale 1:50", 1)]
    public void First_reads_the_first_number_anywhere(string text, double value) => Assert.Equal(value, Numbers.Parse(text, NumberMode.First)!.Value, 9);

    [Fact]
    public void Leading_gives_what_follows()
    {
        Assert.Equal(1.25, Numbers.ParseLeading("  1,25 m", out var rest));
        Assert.Equal(" m", rest);
        Assert.Null(Numbers.ParseLeading("mm", out rest));
        Assert.Equal("mm", rest);
    }
}
