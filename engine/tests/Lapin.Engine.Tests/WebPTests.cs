using System.Diagnostics;
using Lapin.PointClouds;

namespace Lapin.Engine.Tests;

/// <summary>
/// Lapin's WebP lossless decoder (RFC 9649; docs/plan-gaussian-splats.md, G3): small images made by libwebp's own encoder
/// (`cwebp -lossless -exact`, 1.6.0) from the pictures <see cref="Pixel"/> draws, kept here as base64, each decoded back to
/// the same pixels; between them every transform (predictor, colour, subtract green, colour indexing with and without pixel
/// bundling), the colour cache, backward references and meta prefix codes are met. Where cwebp is on this computer, larger
/// pictures are encoded at several efforts and decoded too. A lossy image is refused with a message.
/// </summary>
public class WebPTests
{
    /// <summary>The pictures the fixtures were made from (the same integer formulas as the generating script).</summary>
    internal static (int R, int G, int B, int A) Pixel(string kind, int x, int y, int w, int h)
    {
        switch (kind)
        {
            case "gradient":
            {
                var n = (x * 7919 + y * 104729) % 23;
                return ((x * 255 / (w - 1) + n) % 256, (y * 255 / (h - 1) + n / 2) % 256, ((x + y) * 3 + n) % 256, 255 - x * y % 64);
            }
            case "palette12":
            {
                var k = (x / 3 + y / 2 * 5) % 12;
                return (k * 20, 255 - k * 17, k * 53 % 256, 255);
            }
            case "palette2": return ((x ^ y) >> 1 & 1) == 1 ? (250, 10, 10, 255) : (10, 10, 250, 255);
            case "palette200":
            {
                var k = (x * 13 + y * 7) % 200;
                return (k, k * 3 % 256, k * 7 % 256, 255);
            }
            case "tiles":
            {
                var k = (x / 8 * 31 + y / 8 * 17) % 300;
                return (k % 256, k * 5 % 256, k * 11 % 256, 128 + k % 128);
            }
            case "corr":
            {
                // Green and its neighbours alike: subtract green.
                var g = (x * 3 + y * 2 + x * y % 7) % 256;
                return ((g + 9 + x % 3) % 256, g, (g + 40 + y % 3) % 256, 255);
            }
            case "quad":
            {
                // Four quarters of different statistics: meta prefix codes.
                var hsh = (uint)((ulong)x * 2654435761UL ^ (ulong)y * 40503UL);
                var q = x * 2 / w + 2 * (y * 2 / h);
                return q switch
                {
                    0 => ((int)(hsh % 4 * 60), (int)(255 - hsh % 4 * 60), 100, 255),
                    1 => (40, (int)((hsh >> 3) % 8 * 30), 200, 255),
                    2 => ((int)((hsh >> 5) % 3 * 120), 50, (int)((hsh >> 7) % 3 * 120), 255),
                    _ => (x % 256, y % 256, (int)((hsh >> 9) % 2 * 255), 255),
                };
            }
            default: return (17, 34, 51, 200);
        }
    }

    static readonly Dictionary<string, (int W, int H)> Sizes = new()
    {
        ["gradient"] = (64, 48), ["palette12"] = (40, 30), ["palette2"] = (33, 9), ["palette200"] = (50, 20), ["tiles"] = (96, 64), ["one"] = (1, 1), ["corr"] = (48, 32), ["quad"] = (64, 64),
    };

    static readonly Dictionary<string, string> Files = new()
    {
        ["gradient"] = "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",
        ["palette12"] = "UklGRowAAABXRUJQVlA4TIAAAAAvJ0AHAF8gFkz8aYfQF8NQTM38Z7QDq9q2ovDvHAEiQASIIBEkgkSQCBJBIkDB4/Qf0X8hQUK1ukLMi9LGrT7sMZ251CZmxm8hHilfpXVikcpY5zem1AYiY6wDkUlnBpFZpAKRCXsEkWmdAJFxqweRyVcBkVHagMjEI4HI/ParCg==",
        ["palette2"] = "UklGRjQAAABXRUJQVlA4TCcAAAAvIAACAA8wCleAP4T+/EcLFLKRBO0xjb/BYN2HENH/QVBNA1DNvwMA",
        ["palette200"] = "UklGRmQAAABXRUJQVlA4TFgAAAAvMcAEALkKRPQ/dhFd6H8YFEOSJFEMw/gDDMNg3Xs8KwQCKYzyBQJEkx6leoyE2rZtmNTV7QqUUXvHMBI4BFHbCaJOwRpAtv7bTWDf8RcwXfFfEBqAH4s+",
        ["tiles"] = "UklGRnYBAABXRUJQVlA4TGoBAAAvX8APEAkyaRvae/4l95qAiP5PAFZWfywUpG3AxL/zHQExCP1PHiQBUGzbWt5cFRpAojmOwTEpptGVyaNP6xgck2OYRE9/kJ9iFEmSoox9wpEC5jv/SsbJGXBi29aynCn8mtzr/ydLUkk6EMbnyT26J6g0CAwhSXbVCo/gCxzOB8o5E3ypM/kCCtu2begDOo38ILSCv1kY25XjBRbG9ufavuZ/5/9nP6Y0s6FbQASWkgCHuEBobEtlNji6aCxFwbj2rmMBXtgshczBDgv0AF7NgJigEVrCw/cKkJw57Z2lkiO4Er+W88brANZEoNxwwvW7b0BqIAdw5su49u5XBYQeEj4NHChZkjeQAoxCS84bSxEwrr1r+4NnDZaCfmBbHXR7HghQa9iaXwF1I3BPRca1d8IERTRwCZY8iQ5WJxqQHZCQpawtOBU8xrV3PwAyAXE6mAPIGMQiTdjfXgFDd/AhAUIHYbdGYEgjAA==",
        ["one"] = "UklGRh4AAABXRUJQVlA4TBEAAAAvAAAAEAdQkUZ0psiBiOh/AAA=",
        ["corr"] = "UklGRoYAAABXRUJQVlA4THkAAAAvL8AHAM2VIaL/sYuI/gfYSJLUhsX//7mbQjQ4KSOTtknP+fc7CLJtepv7Ay6hyG3bJrtLN/cRYU9JSceuoonJDUcaZtCjKCFMxwjBkaKNex0jOFI0vETh29EM/ZAHLVhsD4riy/agKF4X24OittcFvx2F6+L3/jMGAA==",
        ["quad"] = "UklGRnQEAABXRUJQVlA4TGgEAAAvP8APAAEGkSQ14mYXPvp3TEZARP8nIKpE73meLiqp8lfVhSS1AQo6oEUYwm7QF3YLWG0DIUHi/99EewgEkvaXHKX/E9AMf1zUJ0Fx2zaOtP/aqVf+ETEB/N84U0RKIWtWaMUNKrBq247bHBHxEoFO+g4EIzCEjmwKIQgCoQgCQUTqQS/NeSXgqI0kSbIWQUHo5Xjlb78JbAEVg2YQSQqQpQEhDfEmkBCGpAHByG3bMC1gbDo47pB0fwiDbCMV4XGHcLiPYJBtpCI87iEM9xEifu/zw7YvKP7wekKETiwQ8lwJBWZObNvxH05cGCIshCidkOeVUODE8ax4Z2HTI8KZEGUi5DkTChzPjnf2faxOWJ14nrg0vvLKJscRJsJRJsbz3Cig8SDHWgirE9bCmCwJHwHOhCATIdE3Y4EkrIw83uXK0CIT4SAT4USPhO+d0He7OiOPtVEC9kYeC6O0vtNGCVgYpfWdNkrCd94bqX2n5j4BS6MM7uc+AXsj3Xdm7tP6ThslYG2k8dDY7t5IwAt67jUe7sW7jRI73819ztz963/Xz30CFkZpfef/21//v2UbLdmq7Hp+yeq+MmUzYXNvrHz91XxVV1V1863qKlXN9zanjVeq6o/bA1S2IEeYyh6kKnabbUlLTFuyM+p4/a7fLv7U6qUtmnIOULhdAXfTCVC3nNBtt0VbW4xL8bsY5U8ti7LFVp2U1h9R7zkEtkWj0Svnp2lXNv1G2n4aRvE7X8WnZYeNex4MNRZgoFmM/90YEsIUpcWaa9ubQtAMklz9oNlQ1LPZkdCw0GbueKxWvEtgWWiqHy02FEK75XrMkqEw/GYKYLfh1lZWzLCwTPuaQsaPsNEhKLJTo+LVL8ugxNuEq9++3Ihtl36V3xE0GKT00y82ftbtIDx3bMASk5zfcSQIJkG0lfK/0bqtaXmWb/LZDb3CIAjKG/3e5E5+saaI/usuWbFx/YRpAqHKKW/k0vvflb6PZTf6H2psYicXgkaI0vNGZ06N7OxGfR/b3zFNNqorgEkR+73I9J5GGTFY2aiXjEqRZXDay5p/Dqb+hU1b4SkaDAZJ6NvajTR+Ru93yo/fmezp2vlpzh//ovS7Z3Lxl3cvTped0bH0pZyYf9nM5Vy9j+vlZ7xiT3G17czWz8bPvZtjPm5nT5dUp09cxvzLzty8rTgtHqPG9wCAMdtqN85zntuxkf3/zWNHIY3o0NDB7jKxbXM28/Ptjt+dKM9Py3/abK0OQnOAUssarwxu8plPOa+Xu1+TLACARL13u59RJ4O1ufGVXgIO/m5D5+H8boPGtMEaa/7fKgfYvcNck0jN1AhbtOvdec+6s7nbEu5tphVvX3XaVjUAh62pW2SFhHubq4Fr8ZYZAWkbdGmeH1BaXl7MH9ux3ano+ee/w5Bg2N1ud8uzNd+3yxrCNjuhD9OPHboIhribSqudvJHZbn5xagsHoqNAA9ODY9+15vtWZ3JNeoYKu/3dHigSNAyLfs2dFhQ=",
    };

    static void Same(string kind, WebPLossless.Image im, int w, int h)
    {
        Assert.Equal((w, h), (im.Width, im.Height));
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var (r, g, b, a) = Pixel(kind, x, y, w, h);
                var p = im.Argb[y * w + x];
                Assert.True(p == (uint)(a << 24 | r << 16 | g << 8 | b), $"{kind} ({x}, {y}): {p:x8}, not {a:x2}{r:x2}{g:x2}{b:x2}");
            }
    }

    [Fact]
    public void Images_made_by_cwebp_come_back_pixel_for_pixel_every_transform_met()
    {
        var used = WebPLossless.Transforms.None;
        foreach (var (kind, b64) in Files)
        {
            var im = WebPLossless.Decode(Convert.FromBase64String(b64));
            Same(kind, im, Sizes[kind].W, Sizes[kind].H);
            used |= im.Used;
        }
        foreach (var t in Enum.GetValues<WebPLossless.Transforms>().Where(t => t != WebPLossless.Transforms.None))
            Assert.True(used.HasFlag(t), $"no fixture uses {t} ({used})");
    }

    [Fact]
    public void A_lossy_webp_or_a_file_that_is_none_is_refused_with_a_message()
    {
        // A RIFF WEBP with a VP8 (lossy) chunk: its bitstream is not looked at.
        var lossy = "RIFF\0\0\0\0WEBPVP8 \x04\0\0\0abcd"u8.ToArray();
        BitConverter.GetBytes(lossy.Length - 8).CopyTo(lossy, 4);
        var e = Assert.Throws<WebPLossless.WebPException>(() => WebPLossless.Decode(lossy));
        Assert.Contains("lossy", e.Message);
        Assert.Throws<WebPLossless.WebPException>(() => WebPLossless.Decode("not an image at all"u8));
        // Cut short: an error, never a picture of zeros.
        var cut = Convert.FromBase64String(Files["quad"])[..400];
        Assert.ThrowsAny<Exception>(() => WebPLossless.Decode(cut));
    }

    /// <summary>The cwebp on this computer, if any (Homebrew's, or on the PATH).</summary>
    internal static string? Tool(string name)
    {
        foreach (var dir in new[] { "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin" }.Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)))
        {
            var p = Path.Combine(dir, name);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    internal static void Run(string exe, params string[] args)
    {
        var psi = new ProcessStartInfo(exe) { RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        Assert.True(p.ExitCode == 0, $"{exe} failed: {p.StandardError.ReadToEnd()}");
    }

    [Fact]
    public void Larger_pictures_encoded_by_cwebp_here_at_several_efforts_decode_exactly()
    {
        var cwebp = Tool("cwebp");
        if (cwebp == null) return; // Not on this computer: the fixtures above stand alone.
        var dir = Directory.CreateTempSubdirectory("lapin-webp-");
        try
        {
            var rnd = new Random(7);
            var cases = new (int W, int H, string[] Args, Func<int, int, uint> Px)[]
            {
                (300, 200, ["-z", "9"], (x, y) => 0xff000000u | (uint)(x * 255 / 299) << 16 | (uint)(y * 255 / 199) << 8 | (uint)((x * y) & 0xff)),
                (257, 129, ["-m", "6", "-q", "100"], (x, y) => (uint)rnd.Next() & 0xff3f3f3fu | 0x80000000u),
                (512, 64, ["-z", "0"], (x, y) => (uint)(((x / 5 + y / 3) % 7) * 0x10203040)),
                (128, 128, ["-near_lossless", "100", "-m", "4"], (x, y) => 0xff000000u | (uint)((x + y) % 251) << 16 | (uint)(x * 3 % 256) << 8 | (uint)(y * 5 % 256)),
            };
            for (var c = 0; c < cases.Length; c++)
            {
                var (w, h, args, px) = cases[c];
                var pixels = new uint[w * h];
                for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) pixels[y * w + x] = px(x, y);
                var pam = Path.Combine(dir.FullName, $"{c}.pam");
                using (var f = File.Create(pam))
                {
                    f.Write(System.Text.Encoding.ASCII.GetBytes($"P7\nWIDTH {w}\nHEIGHT {h}\nDEPTH 4\nMAXVAL 255\nTUPLTYPE RGB_ALPHA\nENDHDR\n"));
                    foreach (var p in pixels) f.Write([(byte)(p >> 16), (byte)(p >> 8), (byte)p, (byte)(p >> 24)]);
                }
                var webp = Path.Combine(dir.FullName, $"{c}.webp");
                Run(cwebp, ["-quiet", "-lossless", "-exact", .. args, pam, "-o", webp]);
                var im = WebPLossless.Decode(File.ReadAllBytes(webp));
                Assert.Equal((w, h), (im.Width, im.Height));
                Assert.Equal(pixels, im.Argb);
            }
        }
        finally { dir.Delete(true); }
    }
}
