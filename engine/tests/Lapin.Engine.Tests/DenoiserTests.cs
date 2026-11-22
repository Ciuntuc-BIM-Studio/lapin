using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Platform;
using Xunit;

namespace Lapin.Engine.Tests;

/// <summary>The path tracer's OIDN sidecar (docs/plan-render.md, S4): its PFM files and its call's checks; the program itself is run where it was fetched.</summary>
public class DenoiserTests
{
    [Fact]
    public void A_pfm_reads_back_as_it_was_written()
    {
        var rgb = new float[] { 0f, 0.5f, 1f, 2f, 3.25f, 65000f };
        var (w, h, back) = Denoiser.ReadPfm(Denoiser.Pfm(2, 1, rgb));
        Assert.Equal(2, w);
        Assert.Equal(1, h);
        Assert.Equal(rgb, back);
    }

    [Fact]
    public void A_greyscale_pfm_spreads_to_rgb()
    {
        var head = System.Text.Encoding.ASCII.GetBytes("Pf\n2 1\n-1.0\n");
        var bytes = new byte[head.Length + 8];
        head.CopyTo(bytes, 0);
        BitConverter.GetBytes(0.25f).CopyTo(bytes, head.Length);
        BitConverter.GetBytes(4f).CopyTo(bytes, head.Length + 4);
        var (_, _, rgb) = Denoiser.ReadPfm(bytes);
        Assert.Equal(new[] { 0.25f, 0.25f, 0.25f, 4f, 4f, 4f }, rgb);
    }

    [Fact]
    public async Task A_picture_of_no_size_or_without_its_light_is_refused()
    {
        var dir = Path.Combine(Path.GetTempPath(), "no-oidn");
        await Assert.ThrowsAsync<CommandException>(() => Denoiser.Run(dir, new JsonObject { ["width"] = 0, ["height"] = 4 }));
        await Assert.ThrowsAsync<CommandException>(() => Denoiser.Run(dir, new JsonObject { ["width"] = 2, ["height"] = 2 }));
        await Assert.ThrowsAsync<CommandException>(() => Denoiser.Run(dir, new JsonObject { ["width"] = 2, ["height"] = 2, ["color"] = "not base64!" }));
    }

    [Fact]
    public async Task Oidn_where_fetched_denoises_a_noisy_picture()
    {
        var dir = Denoiser.Find(null);
        if (dir == null) return; // not fetched here (node scripts/fetch-oidn.mjs)
        const int w = 32, h = 24;
        var rnd = new Random(7);
        var noisy = new float[w * h * 3];
        for (var i = 0; i < noisy.Length; i++) noisy[i] = 0.5f * (1f + (float)(rnd.NextDouble() * 1.6 - 0.8));
        var half = new byte[noisy.Length * 2];
        for (var i = 0; i < noisy.Length; i++) BitConverter.TryWriteBytes(half.AsSpan(2 * i), BitConverter.HalfToUInt16Bits((Half)noisy[i]));
        var r = await Denoiser.Run(dir, new JsonObject { ["width"] = w, ["height"] = h, ["color"] = Convert.ToBase64String(half) });
        var back = Convert.FromBase64String((string)r["color"]!);
        Assert.Equal(noisy.Length * 2, back.Length);
        double before = 0, after = 0;
        for (var i = 0; i < noisy.Length; i++)
        {
            var v = (float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(back, 2 * i));
            before += (noisy[i] - 0.5) * (noisy[i] - 0.5);
            after += (v - 0.5) * (v - 0.5);
        }
        Assert.True(after < before * 0.5, $"error {after} after, {before} before");
    }
}
