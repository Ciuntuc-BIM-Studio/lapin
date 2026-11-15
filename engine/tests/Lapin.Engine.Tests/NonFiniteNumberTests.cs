using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// The audit of 15.11.2026, root cause b: "NaN" and "Infinity" are no numbers. <see cref="Json.Number"/> reads them as
/// none, so a command falls back to its default or refuses; and the range checks on a spec are written so a NaN
/// that came some other way fails them instead of slipping through (<c>!(x &gt;= lo &amp;&amp; x &lt;= hi)</c>).
/// </summary>
public class NonFiniteNumberTests
{
    [Theory]
    [InlineData("\"NaN\"")]
    [InlineData("\"nan\"")]
    [InlineData("\"Infinity\"")]
    [InlineData("\"-Infinity\"")]
    [InlineData("\"1e400\"")]
    [InlineData("1e400")]
    [InlineData("-1e400")]
    public void A_non_finite_value_is_no_number(string json)
    {
        Assert.Null(Json.Number(JsonNode.Parse(json)));
        Assert.Null(Json.Number(new JsonObject { ["x"] = JsonNode.Parse(json) }["x"]));
    }

    [Fact]
    public void Finite_values_read_as_before_however_they_were_written()
    {
        Assert.Equal(2.5, Json.Number(JsonNode.Parse("2.5")));
        Assert.Equal(2.5, Json.Number(JsonNode.Parse("\"2.5\"")));
        Assert.Equal(3, Json.Number(JsonValue.Create(3)));
        Assert.Equal(1e300, Json.Number(JsonNode.Parse("1e300")));
        Assert.Equal(0.1, Json.Number(JsonValue.Create(0.1m)));
        Assert.Null(Json.Number(JsonValue.Create(double.NaN)));
        Assert.Null(Json.Number(JsonValue.Create(double.PositiveInfinity)));
        Assert.Null(Json.Number(JsonValue.Create(float.NaN)));
        Assert.Null(Json.Number(JsonValue.Create("ten")));
        Assert.Null(Json.Number(null));
    }

    [Fact]
    public void A_wood_spec_with_a_NaN_fails_its_range_checks()
    {
        Assert.Null(FrameSpec.Default.Problem());
        Assert.NotNull((FrameSpec.Default with { Spacing = double.NaN }).Problem());
        Assert.NotNull((FrameSpec.Default with { GapTop = double.NaN }).Problem());
        Assert.NotNull((FrameSpec.Default with { PanelMin = double.NaN }).Problem());
        Assert.Null(JoistSpec.Default.Problem());
        Assert.NotNull((JoistSpec.Default with { Spacing = double.NaN }).Problem());
        Assert.NotNull((JoistSpec.Default with { Spacing = double.PositiveInfinity }).Problem());
    }

    [Fact]
    public void A_wood_frame_read_with_a_NaN_spacing_keeps_the_default()
    {
        var o = FrameSpec.Default.ToJson();
        o["spacing"] = "NaN";
        Assert.Equal(FrameSpec.Default.Spacing, FrameSpec.From(o).Spacing);
    }
}
