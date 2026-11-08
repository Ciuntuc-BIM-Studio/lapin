using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Lapin.Mesh.Bos;

/// <summary>A Parquet column's values: one of the arrays, by its physical type (strings for UTF-8 byte arrays, null where a row has none).</summary>
public sealed class ParquetColumn(string name)
{
    public string Name { get; } = name;
    public int[]? Ints { get; set; }
    public long[]? Longs { get; set; }
    public float[]? Floats { get; set; }
    public double[]? Doubles { get; set; }
    public bool[]? Bools { get; set; }
    public string?[]? Strings { get; set; }
    /// <summary>The column declared as an unsigned 8-bit integer (stored as INT32): written so, read as <see cref="Ints"/>.</summary>
    public bool Byte { get; set; }

    public int Count => Ints?.Length ?? Longs?.Length ?? Floats?.Length ?? Doubles?.Length ?? Bools?.Length ?? Strings?.Length ?? 0;

    /// <summary>The values as 32-bit integers, whatever integer type the file wrote them in.</summary>
    public int[] AsInts() => Ints ?? Longs?.Select(v => (int)v).ToArray() ?? Floats?.Select(v => (int)v).ToArray() ?? Doubles?.Select(v => (int)v).ToArray() ?? [];
    public long[] AsLongs() => Longs ?? Ints?.Select(v => (long)v).ToArray() ?? [];
    public float[] AsFloats() => Floats ?? Doubles?.Select(v => (float)v).ToArray() ?? Ints?.Select(v => (float)v).ToArray() ?? [];
}

/// <summary>
/// Apache Parquet files, read and written by Lapin (docs/bos.md), after the format's public specification
/// (parquet-format: the file layout, Thrift metadata, the encodings): flat schemas of required or optional
/// columns, BOOLEAN, INT32, INT64, FLOAT, DOUBLE and BYTE_ARRAY (UTF-8 strings); pages v1 and v2; PLAIN,
/// dictionary (PLAIN_DICTIONARY, RLE_DICTIONARY) and DELTA_BINARY_PACKED encodings; uncompressed, Snappy,
/// GZIP and Brotli pages. Nested columns, ZSTD and LZ4 are refused with a message. Written as one row group,
/// one PLAIN page per column, Brotli-compressed (uncompressed where Brotli is not available).
/// </summary>
public static class Parquet
{
    static readonly byte[] Magic = "PAR1"u8.ToArray();

    // Physical types, encodings and codecs (parquet.thrift).
    const int Boolean = 0, Int32 = 1, Int64 = 2, Float = 4, Double = 5, ByteArray = 6;
    const int Plain = 0, PlainDictionary = 2, Rle = 3, BitPacked = 4, DeltaBinaryPacked = 5, RleDictionary = 8;
    public const int Uncompressed = 0, Snappy = 1, Gzip = 2, Brotli = 4;

    public static List<ParquetColumn> Read(byte[] file)
    {
        if (file.Length < 12 || !file.AsSpan(0, 4).SequenceEqual(Magic) || !file.AsSpan(file.Length - 4).SequenceEqual(Magic))
            throw new InvalidDataException("not a Parquet file");
        var metaLength = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(file.Length - 8));
        if (metaLength <= 0 || metaLength > file.Length - 12) throw new InvalidDataException("its metadata is damaged");
        var meta = new Thrift.Reader(file, file.Length - 8 - metaLength).ReadStruct();

        // The schema: a root, then the leaf columns (a nested schema is refused).
        var schema = meta.Items(2).OfType<Thrift.Fields>().ToList();
        if (schema.Count == 0) throw new InvalidDataException("it has no schema");
        var leaves = schema.Skip(1).ToList();
        if (leaves.Any(s => s.Int(5) > 0)) throw new InvalidDataException("it has nested columns, which Lapin does not read");
        var columns = leaves.Select(s => new ParquetColumn(s.Text(4) ?? "")).ToList();
        var parts = leaves.Select(_ => new List<Array>()).ToList();
        for (var c = 0; c < leaves.Count; c++)
            columns[c].Byte = leaves[c].Sub(10)?.Sub(10) is { } it && it.Long(1) == 8 || leaves[c].Int(6, -1) is 11 or 15;

        foreach (var rg in meta.Items(4).OfType<Thrift.Fields>())
        {
            var chunks = rg.Items(1).OfType<Thrift.Fields>().ToList();
            for (var c = 0; c < chunks.Count && c < leaves.Count; c++)
            {
                var md = chunks[c].Sub(3) ?? throw new InvalidDataException("a column chunk has no metadata");
                var optional = leaves[c].Int(3) == 1;
                parts[c].Add(ReadChunk(file, md, optional));
            }
        }
        for (var c = 0; c < columns.Count; c++) Assign(columns[c], leaves[c].Int(1), parts[c]);
        return columns;
    }

    static void Assign(ParquetColumn col, int type, List<Array> parts)
    {
        T[] Join<T>() => parts.SelectMany(p => (T[])p).ToArray();
        switch (type)
        {
            case Boolean: col.Bools = Join<bool>(); break;
            case Int32: col.Ints = Join<int>(); break;
            case Int64: col.Longs = Join<long>(); break;
            case Float: col.Floats = Join<float>(); break;
            case Double: col.Doubles = Join<double>(); break;
            case ByteArray: col.Strings = Join<string?>(); break;
            default: throw new InvalidDataException($"column „{col.Name}” has a physical type ({type}) Lapin does not read");
        }
    }

    /// <summary>A column chunk's values (nulls where an optional row has none), its pages decoded one after the other.</summary>
    static Array ReadChunk(byte[] file, Thrift.Fields md, bool optional)
    {
        var type = md.Int(1);
        var codec = md.Int(4);
        var total = md.Long(5);
        var start = md.TryGetValue(11, out var d) && d is long dict && dict > 0 ? dict : md.Long(9);
        var pos = (int)start;
        Array? dictionary = null;
        var values = new List<Array>();
        long read = 0;
        while (read < total)
        {
            var header = new Thrift.Reader(file, pos);
            var ph = header.ReadStruct();
            pos = header.Position;
            var compressed = ph.Int(3);
            var uncompressedSize = ph.Int(2);
            if (compressed < 0 || pos + compressed > file.Length) throw new InvalidDataException("a page runs past the end of the file");
            var raw = file.AsSpan(pos, compressed);
            pos += compressed;
            switch (ph.Int(1))
            {
                case 2: // dictionary page
                {
                    var dh = ph.Sub(7)!;
                    var data = Decompress(raw, codec, uncompressedSize);
                    var at = 0;
                    dictionary = PlainValues(data, ref at, type, dh.Int(1));
                    break;
                }
                case 0: // data page v1: [repetition levels][definition levels][values], all compressed together
                {
                    var dh = ph.Sub(5)!;
                    var n = dh.Int(1);
                    var data = Decompress(raw, codec, uncompressedSize);
                    var at = 0;
                    bool[]? defined = null;
                    if (optional)
                    {
                        var len = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at));
                        at += 4;
                        defined = Hybrid(data.AsSpan(at, len), 1, n).Select(v => v == 1).ToArray();
                        at += len;
                    }
                    values.Add(Page(data, at, dh.Int(2), type, n, defined, dictionary));
                    read += n;
                    break;
                }
                case 3: // data page v2: levels uncompressed ahead of the (maybe compressed) values
                {
                    var dh = ph.Sub(8)!;
                    var n = dh.Int(1);
                    var defLen = dh.Int(5);
                    var repLen = dh.Int(6);
                    var levels = raw[..(repLen + defLen)];
                    bool[]? defined = optional && defLen > 0 ? Hybrid(levels.Slice(repLen, defLen), 1, n).Select(v => v == 1).ToArray() : null;
                    var body = raw[(repLen + defLen)..];
                    var data = dh.Bool(7, true) ? Decompress(body, codec, uncompressedSize - repLen - defLen) : body.ToArray();
                    values.Add(Page(data, 0, dh.Int(4), type, n, defined, dictionary));
                    read += n;
                    break;
                }
                default: break; // an index page: skipped
            }
        }
        return Concat(type, values);
    }

    static Array Concat(int type, List<Array> parts) => type switch
    {
        Boolean => parts.SelectMany(p => (bool[])p).ToArray(),
        Int32 => parts.SelectMany(p => (int[])p).ToArray(),
        Int64 => parts.SelectMany(p => (long[])p).ToArray(),
        Float => parts.SelectMany(p => (float[])p).ToArray(),
        Double => parts.SelectMany(p => (double[])p).ToArray(),
        ByteArray => parts.SelectMany(p => (string?[])p).ToArray(),
        _ => throw new InvalidDataException($"a physical type ({type}) Lapin does not read"),
    };

    /// <summary>A data page's <paramref name="n"/> rows: the defined ones decoded by <paramref name="encoding"/>, the others null or zero.</summary>
    static Array Page(byte[] data, int at, int encoding, int type, int n, bool[]? defined, Array? dictionary)
    {
        var present = defined?.Count(x => x) ?? n;
        Array dense;
        switch (encoding)
        {
            case Plain: dense = PlainValues(data, ref at, type, present); break;
            case PlainDictionary or RleDictionary:
            {
                if (dictionary == null) throw new InvalidDataException("a dictionary-encoded page has no dictionary");
                var width = data[at];
                var idx = Hybrid(data.AsSpan(at + 1), width, present);
                dense = Array.CreateInstance(dictionary.GetType().GetElementType()!, present);
                for (var i = 0; i < present; i++) dense.SetValue(dictionary.GetValue(idx[i]), i);
                break;
            }
            case Rle when type == Boolean:
            {
                // Booleans run-length encoded: a length, then the hybrid at one bit.
                var len = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at));
                dense = Hybrid(data.AsSpan(at + 4, len), 1, present).Select(x => x == 1).ToArray();
                break;
            }
            case DeltaBinaryPacked:
            {
                var longs = Delta(data, ref at, present);
                dense = type == Int64 ? longs : type == Int32 ? longs.Select(v => (int)v).ToArray() : throw new InvalidDataException("DELTA_BINARY_PACKED on a non-integer column");
                break;
            }
            default: throw new InvalidDataException($"an encoding ({encoding}) Lapin does not read");
        }
        if (defined == null) return dense;
        var full = Array.CreateInstance(dense.GetType().GetElementType()!, n);
        for (int i = 0, k = 0; i < n; i++) if (defined[i]) full.SetValue(dense.GetValue(k++), i);
        return full;
    }

    static Array PlainValues(byte[] data, ref int at, int type, int n)
    {
        switch (type)
        {
            case Int32: { var o = new int[n]; for (var i = 0; i < n; i++, at += 4) o[i] = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at)); return o; }
            case Int64: { var o = new long[n]; for (var i = 0; i < n; i++, at += 8) o[i] = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(at)); return o; }
            case Float: { var o = new float[n]; for (var i = 0; i < n; i++, at += 4) o[i] = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(at)); return o; }
            case Double: { var o = new double[n]; for (var i = 0; i < n; i++, at += 8) o[i] = BinaryPrimitives.ReadDoubleLittleEndian(data.AsSpan(at)); return o; }
            case Boolean: { var o = new bool[n]; for (var i = 0; i < n; i++) o[i] = (data[at + (i >> 3)] >> (i & 7) & 1) == 1; at += (n + 7) / 8; return o; }
            case ByteArray:
            {
                var o = new string?[n];
                for (var i = 0; i < n; i++)
                {
                    var len = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at));
                    at += 4;
                    if (len < 0 || at + len > data.Length) throw new InvalidDataException("a string runs past its page");
                    o[i] = Encoding.UTF8.GetString(data, at, len);
                    at += len;
                }
                return o;
            }
            default: throw new InvalidDataException($"a physical type ({type}) Lapin does not read");
        }
    }

    /// <summary>The RLE / bit-packing hybrid: <paramref name="n"/> values of <paramref name="width"/> bits.</summary>
    static int[] Hybrid(ReadOnlySpan<byte> data, int width, int n)
    {
        var o = new int[n];
        var k = 0;
        var at = 0;
        var bytes = (width + 7) / 8;
        while (k < n && at < data.Length)
        {
            ulong header = 0;
            for (var shift = 0; ; shift += 7) { var b = data[at++]; header |= (ulong)(b & 0x7F) << shift; if ((b & 0x80) == 0) break; }
            if ((header & 1) == 1)
            {
                var groups = (int)(header >> 1);
                var count = groups * 8;
                long bit = 0;
                for (var i = 0; i < count; i++)
                {
                    var v = 0;
                    for (var b = 0; b < width; b++, bit++) if ((data[at + (int)(bit >> 3)] >> (int)(bit & 7) & 1) == 1) v |= 1 << b;
                    if (k < n) o[k++] = v;
                }
                at += groups * width;
            }
            else
            {
                var run = (int)(header >> 1);
                var v = 0;
                for (var b = 0; b < bytes; b++) v |= data[at + b] << (8 * b);
                at += bytes;
                for (var i = 0; i < run && k < n; i++) o[k++] = v;
            }
        }
        return o;
    }

    static long[] Delta(byte[] data, ref int at, int n)
    {
        ulong Varint(ref int p) { ulong v = 0; for (var s = 0; ; s += 7) { var b = data[p++]; v |= (ulong)(b & 0x7F) << s; if ((b & 0x80) == 0) return v; } }
        long Zig(ref int p) { var u = Varint(ref p); return (long)(u >> 1) ^ -(long)(u & 1); }
        var blockSize = (int)Varint(ref at);
        var miniblocks = (int)Varint(ref at);
        var count = (int)Varint(ref at);
        var o = new long[Math.Max(n, 0)];
        if (count == 0 || n == 0) return o;
        var prev = Zig(ref at);
        o[0] = prev;
        var k = 1;
        var per = blockSize / Math.Max(1, miniblocks);
        while (k < count && k < n)
        {
            var minDelta = Zig(ref at);
            var widths = data.AsSpan(at, miniblocks).ToArray();
            at += miniblocks;
            for (var m = 0; m < miniblocks && k < count; m++)
            {
                var w = widths[m];
                long bit = 0;
                for (var i = 0; i < per; i++)
                {
                    ulong v = 0;
                    for (var b = 0; b < w; b++, bit++) if ((data[at + (int)(bit >> 3)] >> (int)(bit & 7) & 1) == 1) v |= 1UL << b;
                    if (k < count && k < n) { prev += minDelta + (long)v; o[k++] = prev; }
                }
                at += per * w / 8;
            }
        }
        return o;
    }

    static byte[] Decompress(ReadOnlySpan<byte> raw, int codec, int size)
    {
        switch (codec)
        {
            case Uncompressed: return raw.ToArray();
            case Snappy: return SnappyCodec.Decompress(raw);
            case Gzip:
            {
                using var gz = new GZipStream(new MemoryStream(raw.ToArray()), CompressionMode.Decompress);
                using var ms = new MemoryStream(Math.Max(size, 0));
                gz.CopyTo(ms);
                return ms.ToArray();
            }
            case Brotli:
            {
                var o = new byte[Math.Max(size, 0)];
                try
                {
                    if (BrotliDecoder.TryDecompress(raw, o, out var written) && written == o.Length) return o;
                }
                catch (PlatformNotSupportedException) { throw new InvalidDataException("its pages are Brotli-compressed, which this build of Lapin cannot read (the desktop app can)"); }
                throw new InvalidDataException("a Brotli page is damaged");
            }
            default:
                throw new InvalidDataException(codec switch { 6 => "its pages are ZSTD-compressed, which Lapin does not read", 5 or 7 => "its pages are LZ4-compressed, which Lapin does not read", _ => $"a compression ({codec}) Lapin does not read" });
        }
    }

    // ------------------------------------------------------------ writing

    /// <summary>
    /// The columns as a Parquet file: one row group, a PLAIN data page (v1) per column, Brotli unless
    /// <paramref name="codec"/> says otherwise; strings optional (UTF-8), the others required, bytes as INT32 tagged UINT_8.
    /// </summary>
    public static byte[] Write(IReadOnlyList<ParquetColumn> columns, int codec = Brotli)
    {
        var rows = columns.Count == 0 ? 0 : columns[0].Count;
        if (columns.Any(c => c.Count != rows)) throw new ArgumentException("the columns of a table differ in length");
        var o = new MemoryStream();
        o.Write(Magic);
        var chunks = new List<(long Offset, long Size, long Raw, int Type, bool Optional, int Codec)>();
        foreach (var c in columns)
        {
            var (type, optional) = TypeOf(c);
            var body = new MemoryStream();
            if (optional)
            {
                // Definition levels: every row has a value, or not, in runs.
                var levels = new MemoryStream();
                var strings = c.Strings!;
                for (var i = 0; i < strings.Length;)
                {
                    var j = i;
                    var defined = strings[i] != null;
                    while (j < strings.Length && (strings[j] != null) == defined) j++;
                    WriteVarint(levels, (ulong)(j - i) << 1);
                    levels.WriteByte(defined ? (byte)1 : (byte)0);
                    i = j;
                }
                var len = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(len, (int)levels.Length);
                body.Write(len);
                levels.Position = 0;
                levels.CopyTo(body);
            }
            WritePlain(body, c, type);
            var raw = body.ToArray();
            var (packed, used) = Compress(raw, codec);
            var header = new Thrift.Writer()
                .Int(1, 0).Int(2, raw.Length).Int(3, packed.Length)
                .Begin(5).Int(1, rows).Int(2, Plain).Int(3, Rle).Int(4, Rle).End()
                .Finish().ToArray();
            var offset = o.Position;
            o.Write(header);
            o.Write(packed);
            chunks.Add((offset, o.Position - offset, header.Length + raw.Length, type, optional, used));
        }

        // The file's metadata: version, schema (a root and its columns), rows, the row group.
        var w = new Thrift.Writer().Int(1, 1);
        w.BeginList(2, Thrift.TStruct, columns.Count + 1);
        w.BeginElement().Text(4, "schema").Int(5, columns.Count).End();
        for (var i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            var (type, optional) = (chunks[i].Type, chunks[i].Optional);
            w.BeginElement().Int(1, type).Int(3, optional ? 1 : 0).Text(4, c.Name);
            if (type == ByteArray) w.Int(6, 0).Begin(10).Begin(1).End().End();
            else if (c.Byte) w.Int(6, 11).Begin(10).Begin(10).SByte(1, 8).Bool(2, false).End().End();
            else if (type == Int32) w.Int(6, 17).Begin(10).Begin(10).SByte(1, 32).Bool(2, true).End().End();
            else if (type == Int64) w.Int(6, 18).Begin(10).Begin(10).SByte(1, 64).Bool(2, true).End().End();
            w.End();
        }
        w.Long(3, rows);
        w.BeginList(4, Thrift.TStruct, 1).BeginElement();
        w.BeginList(1, Thrift.TStruct, columns.Count);
        for (var i = 0; i < columns.Count; i++)
        {
            var ch = chunks[i];
            w.BeginElement().Long(2, ch.Offset).Begin(3)
                .Int(1, ch.Type);
            w.BeginList(2, Thrift.TI32, 2).ListInt(Plain).ListInt(Rle);
            w.BeginList(3, Thrift.TBinary, 1).ListText(columns[i].Name);
            w.Int(4, ch.Codec).Long(5, rows).Long(6, ch.Raw).Long(7, ch.Size).Long(9, ch.Offset)
                .End().End();
        }
        w.Long(2, chunks.Sum(c => c.Raw)).Long(3, rows).End();
        w.Text(6, "Lapin (BIM Open Schema)");
        var meta = w.Finish().ToArray();
        o.Write(meta);
        Span<byte> tail = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(tail, meta.Length);
        o.Write(tail);
        o.Write(Magic);
        return o.ToArray();
    }

    static (int Type, bool Optional) TypeOf(ParquetColumn c) =>
        c.Strings != null ? (ByteArray, true) : c.Ints != null ? (Int32, false) : c.Longs != null ? (Int64, false) : c.Floats != null ? (Float, false)
        : c.Doubles != null ? (Double, false) : c.Bools != null ? (Boolean, false) : (Int32, false);

    static void WritePlain(Stream o, ParquetColumn c, int type)
    {
        Span<byte> b = stackalloc byte[8];
        switch (type)
        {
            case Int32: foreach (var v in c.Ints ?? []) { BinaryPrimitives.WriteInt32LittleEndian(b, v); o.Write(b[..4]); } break;
            case Int64: foreach (var v in c.Longs!) { BinaryPrimitives.WriteInt64LittleEndian(b, v); o.Write(b); } break;
            case Float: foreach (var v in c.Floats!) { BinaryPrimitives.WriteSingleLittleEndian(b, v); o.Write(b[..4]); } break;
            case Double: foreach (var v in c.Doubles!) { BinaryPrimitives.WriteDoubleLittleEndian(b, v); o.Write(b); } break;
            case Boolean:
            {
                var bools = c.Bools!;
                for (var i = 0; i < bools.Length; i += 8)
                {
                    byte x = 0;
                    for (var k = 0; k < 8 && i + k < bools.Length; k++) if (bools[i + k]) x |= (byte)(1 << k);
                    o.WriteByte(x);
                }
                break;
            }
            case ByteArray:
                foreach (var s in c.Strings!)
                {
                    if (s == null) continue;
                    var bytes = Encoding.UTF8.GetBytes(s);
                    BinaryPrimitives.WriteInt32LittleEndian(b, bytes.Length);
                    o.Write(b[..4]);
                    o.Write(bytes);
                }
                break;
        }
    }

    static (byte[] Bytes, int Codec) Compress(byte[] raw, int codec)
    {
        if (codec != Brotli || raw.Length == 0) return (raw, Uncompressed);
        try
        {
            var o = new byte[BrotliEncoder.GetMaxCompressedLength(raw.Length)];
            if (BrotliEncoder.TryCompress(raw, o, out var written, quality: 6, window: 22)) return (o[..written], Brotli);
        }
        catch (PlatformNotSupportedException) { }
        return (raw, Uncompressed);
    }

    static void WriteVarint(Stream o, ulong v)
    {
        while (v >= 0x80) { o.WriteByte((byte)(v | 0x80)); v >>= 7; }
        o.WriteByte((byte)v);
    }
}

/// <summary>Snappy decompression (google/snappy's format description): literals and back-references. No dependency.</summary>
public static class SnappyCodec
{
    public static byte[] Decompress(ReadOnlySpan<byte> src)
    {
        var at = 0;
        ulong length = 0;
        for (var shift = 0; ; shift += 7) { var b = src[at++]; length |= (ulong)(b & 0x7F) << shift; if ((b & 0x80) == 0) break; }
        if (length > int.MaxValue) throw new InvalidDataException("a Snappy block is too large");
        var o = new byte[length];
        var k = 0;
        while (at < src.Length)
        {
            var tag = src[at++];
            switch (tag & 3)
            {
                case 0:
                {
                    var len = (tag >> 2) + 1;
                    if (len > 60)
                    {
                        var extra = len - 60;
                        len = 0;
                        for (var i = 0; i < extra; i++) len |= src[at++] << (8 * i);
                        len += 1;
                    }
                    src.Slice(at, len).CopyTo(o.AsSpan(k));
                    at += len; k += len;
                    break;
                }
                case 1: Copy(4 + ((tag >> 2) & 7), ((tag >> 5) << 8) | src[at++]); break;
                case 2: Copy((tag >> 2) + 1, src[at] | src[at + 1] << 8); at += 2; break;
                default: Copy((tag >> 2) + 1, src[at] | src[at + 1] << 8 | src[at + 2] << 16 | src[at + 3] << 24); at += 4; break;
            }
        }
        if (k != o.Length) throw new InvalidDataException("a Snappy block is damaged");
        return o;

        void Copy(int len, int offset)
        {
            if (offset <= 0 || offset > k || k + len > o.Length) throw new InvalidDataException("a Snappy block is damaged");
            for (var i = 0; i < len; i++, k++) o[k] = o[k - offset];
        }
    }
}
