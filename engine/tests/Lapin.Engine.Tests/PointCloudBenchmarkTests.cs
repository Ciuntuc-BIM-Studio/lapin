using System.Buffers.Binary;
using System.Diagnostics;
using Lapin.PointClouds;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The index's speed and memory on a large cloud, optional: <c>LAPIN_POINTCLOUD_BENCH=&lt;millions of points&gt;</c> writes a
/// synthetic LAS 1.2 (format 2: a building's floors, walls and a terrain, coloured) to the temporary folder, indexes it and
/// reports the time, the peak memory and the nodes; <c>LAPIN_POINTCLOUD_BENCH_FILE=&lt;path&gt;</c> indexes a real file instead.
/// </summary>
public class PointCloudBenchmarkTests(ITestOutputHelper output)
{
    [Fact]
    public void Index_a_large_cloud()
    {
        var file = Environment.GetEnvironmentVariable("LAPIN_POINTCLOUD_BENCH_FILE");
        var millions = Environment.GetEnvironmentVariable("LAPIN_POINTCLOUD_BENCH") is { } m && double.TryParse(m, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        if (file == null && millions <= 0) return;
        if (file == null)
        {
            file = Path.Combine(Path.GetTempPath(), $"lapin-bench-{millions}M.las");
            if (!File.Exists(file)) WriteSynthetic(file, (long)(millions * 1_000_000));
        }
        var dir = Path.Combine(Path.GetTempPath(), "lapin-bench-index");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var sw = Stopwatch.StartNew();
        var reader = PointReaders.Open(new FileSource(file));
        long peakBytes = 0;
        var store = Indexer.Build(reader, dir, "bench", new FileInfo(file).Length, (_, _) => peakBytes = Math.Max(peakBytes, Environment.WorkingSet));
        var seconds = sw.Elapsed.TotalSeconds;
        var peak = peakBytes / 1048576.0;
        sw.Restart();
        long served = 0;
        foreach (var n in store.Nodes.Take(200)) served += store.Read(n.Name).Count;
        output.WriteLine($"{reader.Header.Format}: {store.Points:N0} points indexed in {seconds:0.0} s ({store.Points / seconds / 1e6:0.00} M/s), peak working set {peak:0} MB, {store.Nodes.Count} nodes, largest {store.Nodes.Max(n => n.Count)}; 200 tiles ({served:N0} points) read in {sw.ElapsedMilliseconds} ms");
    }

    /// <summary>A LAS of n points written as it goes: ten floors of a 60 × 40 m building, its walls, the ground round it.</summary>
    static void WriteSynthetic(string path, long n)
    {
        using var f = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
        var head = new byte[227];
        "LASF"u8.CopyTo(head);
        head[24] = 1; head[25] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(94), 227);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(96), 227);
        head[104] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(105), 26);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(107), (uint)n);
        for (var k = 0; k < 3; k++) BinaryPrimitives.WriteDoubleLittleEndian(head.AsSpan(131 + 8 * k), 0.001);
        void D(int at, double v) => BinaryPrimitives.WriteDoubleLittleEndian(head.AsSpan(at), v);
        D(179, 100); D(187, -20); D(195, 80); D(203, -20); D(211, 35); D(219, -1);
        f.Write(head);
        var rnd = new Random(7);
        var rec = new byte[26];
        var buf = new MemoryStream(1 << 22);
        for (long i = 0; i < n; i++)
        {
            double x, y, z; ushort r, g, b; byte c;
            var kind = rnd.Next(10);
            if (kind < 3) { x = -20 + rnd.NextDouble() * 120; y = -20 + rnd.NextDouble() * 100; z = Math.Sin(x / 15) * 0.5 - 0.5; (r, g, b, c) = (90, 140, 60, 2); }
            else if (kind < 6) { x = rnd.NextDouble() * 60; y = rnd.NextDouble() * 40; z = rnd.Next(11) * 3.2; (r, g, b, c) = (200, 200, 190, 6); }
            else { var t = rnd.NextDouble() * 200; z = rnd.NextDouble() * 32; (x, y) = t < 60 ? (t, 0.0) : t < 100 ? (60.0, t - 60) : t < 160 ? (160 - t, 40.0) : (0.0, 200 - t); (r, g, b, c) = (180, 90, 70, 6); }
            BinaryPrimitives.WriteInt32LittleEndian(rec, (int)(x / 0.001));
            BinaryPrimitives.WriteInt32LittleEndian(rec.AsSpan(4), (int)(y / 0.001));
            BinaryPrimitives.WriteInt32LittleEndian(rec.AsSpan(8), (int)(z / 0.001));
            BinaryPrimitives.WriteUInt16LittleEndian(rec.AsSpan(12), (ushort)rnd.Next(65535));
            rec[15] = c;
            BinaryPrimitives.WriteUInt16LittleEndian(rec.AsSpan(20), (ushort)(r * 257));
            BinaryPrimitives.WriteUInt16LittleEndian(rec.AsSpan(22), (ushort)(g * 257));
            BinaryPrimitives.WriteUInt16LittleEndian(rec.AsSpan(24), (ushort)(b * 257));
            buf.Write(rec);
            if (buf.Length > (1 << 22) - 26) { buf.WriteTo(f); buf.SetLength(0); }
        }
        buf.WriteTo(f);
    }
}
