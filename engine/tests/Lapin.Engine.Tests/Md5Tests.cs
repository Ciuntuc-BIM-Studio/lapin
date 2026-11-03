using System.Text;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Lapin's own MD5 (the browser's .NET has none) gives what the platform's does, so derived GlobalIds never change.</summary>
public class Md5Tests
{
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("abc")]
    [InlineData("message digest")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345678901234567890")]
    public void It_matches_the_RFC_1321_test_suite_and_the_platforms(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(System.Security.Cryptography.MD5.HashData(bytes), Md5.HashData(bytes));
    }

    [Fact]
    public void It_matches_the_platforms_at_every_length_round_a_block()
    {
        var r = new Random(7);
        for (var n = 0; n < 300; n++)
        {
            var bytes = new byte[n];
            r.NextBytes(bytes);
            Assert.Equal(System.Security.Cryptography.MD5.HashData(bytes), Md5.HashData(bytes));
        }
        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", Convert.ToHexStringLower(Md5.HashData("abc"u8)));
    }
}
