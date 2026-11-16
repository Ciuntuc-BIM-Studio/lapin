using System.Text.Json.Nodes;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>One colour validator (audit 15.11.2026, D9): #rrggbb, #rrggbbaa or #rgb only where each was accepted, never #zzzzzz.</summary>
public class HexColorReadTests
{
    [Theory]
    [InlineData("#a8a8a8", true)]
    [InlineData("#A8A8A8", true)]
    [InlineData("#zzzzzz", false)]
    [InlineData("#12345g", false)]
    [InlineData("a8a8a8", false)]
    [InlineData("#a8a8a8ff", false)]
    [InlineData("#abc", false)]
    [InlineData(" #a8a8a8", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_stored_colour_is_a_hash_and_six_hex_digits(string? s, bool ok) => Assert.Equal(ok, HexColor.IsColour(s));

    [Fact]
    public void Opacity_and_the_short_form_only_when_asked()
    {
        Assert.True(HexColor.IsColour("#a8a8a880", alpha: true));
        Assert.False(HexColor.IsColour("#a8a8a8zz", alpha: true));
        Assert.True(HexColor.IsColour("#abc", shortForm: true));
        Assert.False(HexColor.IsColour("#abz", shortForm: true));
    }

    [Fact]
    public void Read_gives_the_fallback_for_anything_else()
    {
        Assert.Equal("#102030", HexColor.Read(JsonValue.Create("#102030"), "#ffffff"));
        Assert.Equal("#ffffff", HexColor.Read(JsonValue.Create("#zzzzzz"), "#ffffff"));
        Assert.Equal("#ffffff", HexColor.Read(JsonValue.Create(12), "#ffffff"));
        Assert.Equal("#ffffff", HexColor.Read(null, "#ffffff"));
        Assert.Null(HexColor.Read(JsonValue.Create("red"), null));
        Assert.Equal("#10203040", HexColor.Read(JsonValue.Create("#10203040"), null, alpha: true));
    }

    [Fact]
    public void Css_colours_read_the_short_form_and_opacity()
    {
        Assert.True(HexColor.TryParseCss("#abc", out var r, out var g, out var b));
        Assert.Equal((0xaa, 0xbb, 0xcc), (r, g, b));
        Assert.True(HexColor.TryParseCss("abc", out r, out g, out b));
        Assert.Equal((0xaa, 0xbb, 0xcc), (r, g, b));
        Assert.True(HexColor.TryParseCss("#11223380", out r, out g, out b));
        Assert.Equal((0x11, 0x22, 0x33), (r, g, b));
        Assert.False(HexColor.TryParseCss("#zzzzzz", out _, out _, out _));
        Assert.Equal(((byte)128, (byte)128, (byte)128), Lapin.Drafting.Cad.Underlays3d.Rgb("#zzzzzz"));
        Assert.Equal(((byte)0xaa, (byte)0xbb, (byte)0xcc), Lapin.Drafting.Cad.Underlays3d.Rgb("#abc"));
    }

    [Fact]
    public void Washed_out_and_shaded_colours()
    {
        Assert.Equal("#ff9999", HexColor.Mix("#ff0000", 0.6));
        Assert.Equal("#ff999980", HexColor.Mix("#ff000080", 0.6)); // fixed: a colour with an opacity was not washed out
        Assert.Equal("#zzzzzz", HexColor.Mix("#zzzzzz", 0.6));
        Assert.Equal("#806040", HexColor.Scale("#ffc080", 0.5));
        Assert.Equal("#zzzzzz", HexColor.Scale("#zzzzzz", 0.5)); // fixed: threw
    }

    [Fact]
    public void A_colour_with_an_opacity_splits()
    {
        Assert.Equal(("#112233", 128 / 255.0), HexColor.SplitAlpha("#11223380"));
        Assert.Equal(("#112233", 1.0), HexColor.SplitAlpha("#112233"));
        Assert.Equal(("#zzzzzz80", 1.0), HexColor.SplitAlpha("#zzzzzz80"));
        Assert.Equal(Lapin.Ifc.IfcModel.Opacity("#11223380"), HexColor.SplitAlpha("#11223380"));
    }
}
