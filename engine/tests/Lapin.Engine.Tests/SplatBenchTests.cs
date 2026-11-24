using System.Diagnostics;
using Lapin.PointClouds;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// How long the G3 formats take to index (docs/plan-gaussian-splats.md, G3's measurements): a synthetic capture of a million
/// Gaussians with harmonics of degree 3 as SOG (its images encoded by cwebp, as SplatTransform's are) and as SPZ version 4
/// (its streams by zstd), indexed with their splats. Run only when asked (LAPIN_SPLAT_BENCH=1, best in Release) and when
/// both tools are on this computer; otherwise it returns at once.
/// </summary>
public class SplatBenchTests(ITestOutputHelper output)
{
    [Fact]
    public void A_million_gaussians_as_sog_and_as_spz_version_4_index_in_seconds()
    {
        if (Environment.GetEnvironmentVariable("LAPIN_SPLAT_BENCH") != "1") return;
        string? cwebp = WebPTests.Tool("cwebp"), zstd = WebPTests.Tool("zstd");
        if (cwebp == null || zstd == null) return;
        var n = int.TryParse(Environment.GetEnvironmentVariable("LAPIN_SPLAT_BENCH_N"), out var nn) ? nn : 1_000_000;
        var dir = Directory.CreateTempSubdirectory("lapin-splat-bench-");
        try
        {
            var gs = SplatFormatTests.Capture(n, seed: 2);
            // SOG: the tests' images re-encoded by cwebp (lossless, its default effort).
            var files = SplatFormatTests.Sog(gs, 3, width: 1024);
            var k = 0;
            foreach (var name in files.Keys.Where(f => f.EndsWith(".webp")).ToList())
            {
                var im = WebPLossless.Decode(files[name]);
                var pam = Path.Combine(dir.FullName, $"{k}.pam");
                using (var f = File.Create(pam))
                {
                    f.Write(System.Text.Encoding.ASCII.GetBytes($"P7\nWIDTH {im.Width}\nHEIGHT {im.Height}\nDEPTH 4\nMAXVAL 255\nTUPLTYPE RGB_ALPHA\nENDHDR\n"));
                    foreach (var p in im.Argb) f.Write([(byte)(p >> 16), (byte)(p >> 8), (byte)p, (byte)(p >> 24)]);
                }
                var webp = Path.Combine(dir.FullName, $"{k++}.webp");
                WebPTests.Run(cwebp, "-quiet", "-lossless", "-exact", pam, "-o", webp);
                files[name] = File.ReadAllBytes(webp);
            }
            var sog = Path.Combine(dir.FullName, "bench.sog");
            File.WriteAllBytes(sog, SplatFormatTests.Zip(files));
            var j = 0;
            byte[] Compress(byte[] data)
            {
                var src = Path.Combine(dir.FullName, $"z{j}");
                var dst = Path.Combine(dir.FullName, $"z{j++}.zst");
                File.WriteAllBytes(src, data);
                WebPTests.Run(zstd, "-q", "-f", "-3", src, "-o", dst);
                return File.ReadAllBytes(dst);
            }
            var spz = Path.Combine(dir.FullName, "bench.spz");
            File.WriteAllBytes(spz, SplatFormatTests.Spz4(gs, Compress));
            foreach (var file in new[] { sog, spz })
            {
                var cache = Path.Combine(dir.FullName, Path.GetFileName(file) + ".index");
                var sw = Stopwatch.StartNew();
                var store = Indexer.Build(PointReaders.Open(new FileSource(file)), cache, "bench", new FileInfo(file).Length);
                sw.Stop();
                var splats = SplatStore.Open(cache)!;
                var line = $"{Path.GetFileName(file)}: {new FileInfo(file).Length / 1048576.0:F1} MB, {store.Points:N0} points and {splats.Count:N0} splats (degree {splats.ShDegree}) indexed in {sw.Elapsed.TotalSeconds:F2} s";
                output.WriteLine(line);
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "lapin-splat-bench.txt"), line + "\n");
            }
        }
        finally { dir.Delete(true); }
    }
}
