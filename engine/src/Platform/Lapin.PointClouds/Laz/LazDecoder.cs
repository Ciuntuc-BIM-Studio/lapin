// LAZ's container (the LASzip VLR, the chunk table, chunks of points), ported to C# from laz-rs 0.9.3 (Thomas Montaigu,
// Apache License 2.0), after LASzip (Martin Isenburg, rapidlasso). See Laz/NOTICE.md.

using System.Buffers.Binary;

namespace Lapin.PointClouds.Laz;

/// <summary>
/// The points of a LAZ file, chunk by chunk: the LASzip record says how the points were compressed (point by point for
/// formats 0–5, in layers for 6–10) and in chunks of how many; the chunk table where each chunk starts. Each chunk starts
/// afresh: its first point raw, the rest decoded from it.
/// </summary>
static class LazDecoder
{
    sealed record Item(int Type, int Size, int Version);

    public static IEnumerable<PointBatch> Read(LasReader las, IPointSource source, int batch, CancellationToken cancel)
    {
        var vlr = las.LazVlr!;
        if (vlr.Length < 34) throw new PointCloudException($"{source.Name}: its LASzip record is too short.");
        int compressor = BinaryPrimitives.ReadUInt16LittleEndian(vlr);
        var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(vlr.AsSpan(12));
        int nItems = BinaryPrimitives.ReadUInt16LittleEndian(vlr.AsSpan(32));
        var items = new List<Item>();
        for (var i = 0; i < nItems && 34 + 6 * i + 6 <= vlr.Length; i++)
        {
            var at = 34 + 6 * i;
            items.Add(new Item(BinaryPrimitives.ReadUInt16LittleEndian(vlr.AsSpan(at)), BinaryPrimitives.ReadUInt16LittleEndian(vlr.AsSpan(at + 2)), BinaryPrimitives.ReadUInt16LittleEndian(vlr.AsSpan(at + 4))));
        }
        var layered = compressor == 3;
        if (compressor is not (1 or 2 or 3)) throw new PointCloudException($"{source.Name}: its LAZ compressor ({compressor}) is not one Lapin reads.");
        foreach (var it in items)
        {
            var ok = layered ? it.Version == 3 && it.Type is 10 or 11 or 12 or 14 : it.Version == 2 && it.Type is 0 or 6 or 7 or 8;
            if (!ok) throw new PointCloudException(it.Type is 9 or 13
                ? $"{source.Name}: its points carry waveforms (LAS point formats 4, 5, 9, 10), which Lapin reads from LAS but not from LAZ: decompress it to LAS first."
                : $"{source.Name}: it was compressed by an old LASzip (item {it.Type}, version {it.Version}), which Lapin does not read: decompress it to LAS first.");
        }
        var recordSize = items.Sum(i => i.Size);
        if (recordSize != las.RecordLength) throw new PointCloudException($"{source.Name}: its LAZ record ({recordSize} bytes) does not match its points ({las.RecordLength}).");
        return Points(las, source, batch, cancel, compressor, chunkSize, items, layered, recordSize);
    }

    static IEnumerable<PointBatch> Points(LasReader las, IPointSource source, int batch, CancellationToken cancel, int compressor, uint chunkSize, List<Item> items, bool layered, int recordSize)
    {
        using var s = source.Open();
        // The chunk table: where it is, then each chunk's points (variable chunks) and bytes.
        var chunks = new List<(long Points, long Bytes)>();
        long first = las.PointOffset;
        var variable = chunkSize == uint.MaxValue;
        if (compressor == 1) chunks.Add((las.PointCount, source.Length - first));
        else
        {
            s.Position = first;
            var head = new byte[8];
            s.ReadExactly(head);
            var tableAt = BinaryPrimitives.ReadInt64LittleEndian(head);
            first += 8;
            if (tableAt <= first - 8 || tableAt >= source.Length)
            {
                // The writer could not go back to write it: its place is the file's last 8 bytes.
                s.Position = source.Length - 8;
                s.ReadExactly(head);
                tableAt = BinaryPrimitives.ReadInt64LittleEndian(head);
                if (tableAt <= first - 8 || tableAt >= source.Length) throw new PointCloudException($"{source.Name}: its LAZ chunk table is missing.");
            }
            s.Position = tableAt;
            var table = new byte[source.Length - tableAt];
            s.ReadExactly(table);
            var count = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(4));
            var dec = new ArithmeticDecoder(table, 8, table.Length - 8);
            dec.ReadInitBytes();
            var ic = new IntegerDecompressor(32, 2);
            long prevPoints = 0, prevBytes = 0;
            long left = las.PointCount;
            for (var i = 0; i < count; i++)
            {
                long points = variable ? (uint)ic.Decompress(dec, (int)prevPoints, 0) : Math.Min(chunkSize, left);
                long bytes = (uint)ic.Decompress(dec, (int)prevBytes, 1);
                chunks.Add((points, bytes));
                (prevPoints, prevBytes) = (points, bytes);
                left -= points;
            }
        }

        var b = new PointBatch(batch);
        var record = new byte[recordSize];
        long at = first, done = 0;
        foreach (var (points, bytes) in chunks)
        {
            cancel.ThrowIfCancellationRequested();
            if (done >= las.PointCount || points <= 0 || bytes <= 0) break;
            var n = Math.Min(points, las.PointCount - done);
            var buf = new byte[bytes];
            s.Position = at;
            var got = s.ReadAtLeast(buf, (int)bytes, throwOnEndOfStream: false);
            if (got < recordSize) break;
            at += bytes;
            foreach (var _ in layered ? Layered(buf, items, record, n) : Pointwise(buf, items, record, n))
            {
                if (b.Full) { yield return b; b.Count = 0; }
                las.AddRecord(b, record);
            }
            done += n;
        }
        if (b.Count > 0) yield return b;
    }

    /// <summary>A pointwise chunk: each point into <paramref name="record"/> in turn.</summary>
    static IEnumerable<int> Pointwise(byte[] buf, List<Item> items, byte[] record, long n)
    {
        var decs = items.Select(it => it.Type switch
        {
            6 => (IPointwiseItem)new Point10V2(), 7 => new GpsTimeV2(), 8 => new Rgb12V2(), _ => new BytesV2(it.Size),
        }).ToList();
        var o = 0;
        foreach (var d in decs) { d.First(buf.AsSpan(o, d.Size)); o += d.Size; }
        buf.AsSpan(0, record.Length).CopyTo(record);
        yield return 0;
        var dec = new ArithmeticDecoder(buf, record.Length, buf.Length - record.Length);
        dec.ReadInitBytes();
        for (long k = 1; k < n; k++)
        {
            o = 0;
            foreach (var d in decs) { d.Next(dec, record.AsSpan(o, d.Size)); o += d.Size; }
            yield return 0;
        }
    }

    /// <summary>A layered chunk: the first point raw, its count, every item's layer sizes, the layers; then each point.</summary>
    static IEnumerable<int> Layered(byte[] buf, List<Item> items, byte[] record, long n)
    {
        var point = new Point14V3();
        Rgb14V3? rgb = null;
        var at = 0;
        var rgbAt = -1;
        foreach (var it in items)
        {
            switch (it.Type)
            {
                case 10: point.First(buf.AsSpan(at, 30)); break;
                case 11 or 12: rgb = new Rgb14V3(); rgb.First(buf.AsSpan(at, 6), point.Current); rgbAt = at; break;
            }
            at += it.Size;
        }
        buf.AsSpan(0, record.Length).CopyTo(record);
        yield return 0;
        var count = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(at));
        at += 4;
        var skip = new List<uint>();
        foreach (var it in items)
            switch (it.Type)
            {
                case 10: point.ReadSizes(buf, ref at); break;
                case 11: rgb!.ReadSizes(buf, ref at); break;
                case 12: rgb!.ReadSizes(buf, ref at); skip.Add(BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(at))); at += 4; break;
                case 14: for (var k = 0; k < it.Size; k++) { skip.Add(BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(at))); at += 4; } break;
            }
        var skipAt = 0;
        foreach (var it in items)
            switch (it.Type)
            {
                case 10: point.ReadLayers(buf, ref at); break;
                case 11: rgb!.ReadLayers(buf, ref at); break;
                case 12: rgb!.ReadLayers(buf, ref at); at += (int)skip[skipAt++]; break;
                case 14: for (var k = 0; k < it.Size; k++) at += (int)skip[skipAt++]; break;
            }
        var total = Math.Min(n, count);
        for (long k = 1; k < total; k++)
        {
            point.Next(record);
            if (rgb != null) rgb.Next(record.AsSpan(rgbAt, 6), point.Current);
            yield return 0;
        }
    }
}
