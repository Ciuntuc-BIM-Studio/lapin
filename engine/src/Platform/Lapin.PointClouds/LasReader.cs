using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace Lapin.PointClouds;

/// <summary>
/// LAS 1.0–1.4 (ASPRS, "LAS Specification 1.4 R15"), point data record formats 0–10, and LAZ, the same file with its points
/// compressed (<see cref="Laz.LazDecoder"/>). Read: the header's scale, offset and box, the coordinate system from the
/// OGC WKT or the GeoTIFF keys (its EPSG code and its linear unit), and per point its coordinates, intensity,
/// classification and colour. Waveforms, GPS time, returns and extra bytes are skipped.
/// </summary>
public sealed partial class LasReader : IPointReader
{
    readonly IPointSource _source;
    internal int PointFormat { get; }
    internal int RecordLength { get; }
    internal long PointCount { get; }
    internal long PointOffset { get; }
    internal double[] Scale { get; } = new double[3];
    internal double[] Offset { get; } = new double[3];
    internal bool Compressed { get; }
    internal byte[]? LazVlr { get; private set; }
    internal int VersionMinor { get; }
    public PointCloudHeader Header { get; }

    public LasReader(IPointSource source)
    {
        _source = source;
        using var s = source.Open();
        var h = new byte[375];
        var n = s.ReadAtLeast(h, 227, throwOnEndOfStream: false);
        if (n < 227 || !h.AsSpan(0, 4).SequenceEqual("LASF"u8)) throw new PointCloudException($"{source.Name} is not a LAS file.");
        int major = h[24], minor = h[25];
        VersionMinor = minor;
        var headerSize = BinaryPrimitives.ReadUInt16LittleEndian(h.AsSpan(94));
        PointOffset = BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(96));
        var vlrCount = BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(100));
        var formatId = h[104];
        Compressed = (formatId & 0x80) != 0;
        PointFormat = formatId & 0x3F;
        RecordLength = BinaryPrimitives.ReadUInt16LittleEndian(h.AsSpan(105));
        long count = BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(107));
        for (var k = 0; k < 3; k++)
        {
            Scale[k] = BinaryPrimitives.ReadDoubleLittleEndian(h.AsSpan(131 + 8 * k));
            Offset[k] = BinaryPrimitives.ReadDoubleLittleEndian(h.AsSpan(155 + 8 * k));
        }
        double D(int at) => BinaryPrimitives.ReadDoubleLittleEndian(h.AsSpan(at));
        var bounds = new[] { D(187), D(203), D(219), D(179), D(195), D(211) };
        long evlrStart = 0; uint evlrCount = 0;
        if (minor >= 4 && n >= 375 && headerSize >= 375)
        {
            evlrStart = (long)BinaryPrimitives.ReadUInt64LittleEndian(h.AsSpan(235));
            evlrCount = BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(243));
            var count64 = (long)BinaryPrimitives.ReadUInt64LittleEndian(h.AsSpan(247));
            if (count64 > 0) count = count64;
        }
        PointCount = count;
        if (PointFormat > 10) throw new PointCloudException($"{source.Name}: LAS point format {PointFormat} is not one the specification knows.");
        if (RecordLength < BaseLength(PointFormat)) throw new PointCloudException($"{source.Name}: its points are {RecordLength} bytes, shorter than format {PointFormat}'s {BaseLength(PointFormat)}.");
        if (Scale.Any(x => !(x > 0) || double.IsInfinity(x))) throw new PointCloudException($"{source.Name}: its header's scale is not a number above zero.");

        // The variable length records after the header: the coordinate system, LASzip's description of the compression.
        var notes = new List<string>();
        string? wkt = null; ushort[]? geoKeys = null;
        s.Position = headerSize;
        var vlrHead = new byte[54];
        for (var i = 0; i < vlrCount && s.Position + 54 <= PointOffset; i++)
        {
            s.ReadExactly(vlrHead);
            var user = Encoding.ASCII.GetString(vlrHead, 2, 16).TrimEnd('\0', ' ');
            var id = BinaryPrimitives.ReadUInt16LittleEndian(vlrHead.AsSpan(18));
            var len = BinaryPrimitives.ReadUInt16LittleEndian(vlrHead.AsSpan(20));
            var body = new byte[len];
            s.ReadExactly(body);
            Record(user, id, body);
        }
        // LAS 1.4's extended records after the points (a long WKT may live there).
        if (evlrCount > 0 && evlrStart > 0 && evlrStart < source.Length)
        {
            s.Position = evlrStart;
            var eh = new byte[60];
            for (var i = 0; i < evlrCount && s.Position + 60 <= source.Length; i++)
            {
                s.ReadExactly(eh);
                var user = Encoding.ASCII.GetString(eh, 2, 16).TrimEnd('\0', ' ');
                var id = BinaryPrimitives.ReadUInt16LittleEndian(eh.AsSpan(18));
                var len = (long)BinaryPrimitives.ReadUInt64LittleEndian(eh.AsSpan(20));
                if (len > 16 << 20 || s.Position + len > source.Length) break;
                var body = new byte[len];
                s.ReadExactly(body);
                Record(user, id, body);
            }
        }
        void Record(string user, ushort id, byte[] body)
        {
            if (user == "LASF_Projection" && id == 2112) wkt = Encoding.UTF8.GetString(body).TrimEnd('\0');
            else if (user == "LASF_Projection" && id == 34735 && body.Length >= 8)
            {
                geoKeys = new ushort[body.Length / 2];
                for (var k = 0; k < geoKeys.Length; k++) geoKeys[k] = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(2 * k));
            }
            else if (user == "laszip encoded" && id == 22204) LazVlr = body;
        }
        if (Compressed && LazVlr == null) throw new PointCloudException($"{source.Name} says its points are compressed, but it has no LASzip record.");

        var (crs, units) = wkt != null ? FromWkt(wkt) : geoKeys != null ? FromGeoKeys(geoKeys) : (null, null);
        var fmt = $"{(Compressed ? "LAZ" : "LAS")} {major}.{minor}, point format {PointFormat}";
        Header = new PointCloudHeader
        {
            Format = fmt, Count = count, Bounds = bounds.All(double.IsFinite) && bounds[3] >= bounds[0] ? bounds : null,
            HasColor = PointFormat is 2 or 3 or 5 or 7 or 8 or 10, HasIntensity = true, HasClassification = true,
            Crs = crs, CrsText = wkt, Units = units, Precision = [.. Scale], Notes = notes,
        };
    }

    /// <summary>A point format's own bytes, before any extra bytes.</summary>
    internal static int BaseLength(int format) => format switch
    {
        0 => 20, 1 => 28, 2 => 26, 3 => 34, 4 => 57, 5 => 63, 6 => 30, 7 => 36, 8 => 38, 9 => 59, 10 => 67, _ => 0,
    };

    /// <summary>Where a format's red, green and blue are in a record; -1 when it has none.</summary>
    internal static int RgbAt(int format) => format switch { 2 => 20, 3 or 5 => 28, 7 or 8 or 10 => 30, _ => -1 };

    public IEnumerable<PointBatch> Read(int batch = 65536, CancellationToken cancel = default)
    {
        if (Compressed) return Laz.LazDecoder.Read(this, _source, batch, cancel);
        return ReadRaw(batch, cancel);
    }

    IEnumerable<PointBatch> ReadRaw(int batch, CancellationToken cancel)
    {
        using var s = _source.Open();
        s.Position = PointOffset;
        var b = new PointBatch(batch);
        var buf = new byte[RecordLength * Math.Min(batch, 8192)];
        var left = PointCount;
        while (left > 0)
        {
            cancel.ThrowIfCancellationRequested();
            var take = (int)Math.Min(left, buf.Length / RecordLength);
            var got = s.ReadAtLeast(buf.AsSpan(0, take * RecordLength), take * RecordLength, throwOnEndOfStream: false) / RecordLength;
            if (got == 0) break;
            for (var i = 0; i < got; i++)
            {
                if (b.Full) { yield return b; b.Count = 0; }
                AddRecord(b, buf.AsSpan(i * RecordLength, RecordLength));
            }
            left -= got;
            if (got < take) break;
        }
        if (b.Count > 0) yield return b;
    }

    /// <summary>One point record (as stored, or as LAZ gave it back) into the batch.</summary>
    internal void AddRecord(PointBatch b, ReadOnlySpan<byte> r)
    {
        var x = BinaryPrimitives.ReadInt32LittleEndian(r) * Scale[0] + Offset[0];
        var y = BinaryPrimitives.ReadInt32LittleEndian(r[4..]) * Scale[1] + Offset[1];
        var z = BinaryPrimitives.ReadInt32LittleEndian(r[8..]) * Scale[2] + Offset[2];
        var intensity = BinaryPrimitives.ReadUInt16LittleEndian(r[12..]);
        var cls = PointFormat >= 6 ? r[16] : (byte)(r[15] & 0x1F);
        ushort cr = 0, cg = 0, cb = 0;
        var at = RgbAt(PointFormat);
        if (at >= 0)
        {
            cr = BinaryPrimitives.ReadUInt16LittleEndian(r[at..]);
            cg = BinaryPrimitives.ReadUInt16LittleEndian(r[(at + 2)..]);
            cb = BinaryPrimitives.ReadUInt16LittleEndian(r[(at + 4)..]);
        }
        b.Add(x, y, z, cr, cg, cb, intensity, cls);
    }

    /// <summary>A WKT's EPSG code (its outermost AUTHORITY, or a WKT2 ID) and its linear unit.</summary>
    internal static (string? Crs, string? Units) FromWkt(string wkt)
    {
        string? code = null;
        // WKT1: the last AUTHORITY["EPSG","n"] closing the outermost PROJCS/COMPD_CS is the system's own.
        var auths = AuthorityRx().Matches(wkt);
        var outer = wkt.TrimStart();
        if (auths.Count > 0)
        {
            // In a compound system the horizontal one is first: its authority is the one closing PROJCS.
            var projcs = outer.IndexOf("PROJCS[", StringComparison.OrdinalIgnoreCase);
            if (projcs >= 0)
            {
                var end = Closing(wkt, projcs + 6);
                var inside = auths.Where(m => m.Index > projcs && m.Index < end).ToList();
                if (inside.Count > 0) code = inside[^1].Groups[1].Value;
            }
            code ??= auths[^1].Groups[1].Value;
        }
        string? units = null;
        foreach (Match m in UnitRx().Matches(wkt))
        {
            var name = m.Groups[1].Value.ToLowerInvariant();
            if (name.Contains("degree") || name.Contains("radian") || name.Contains("grad")) continue;
            units = name.Contains("us survey") || name.Contains("foot_us") || name.Contains("us_survey") ? "usft" : name.Contains("foot") || name.Contains("feet") ? "ft" : name.Contains("met") ? "m" : units;
            break;
        }
        return (code != null ? $"EPSG:{code}" : null, units);
    }

    static int Closing(string s, int open)
    {
        var depth = 0;
        for (var i = open; i < s.Length; i++)
        {
            if (s[i] == '[') depth++;
            else if (s[i] == ']' && --depth == 0) return i;
        }
        return s.Length;
    }

    /// <summary>The GeoTIFF keys' projected system (ProjectedCSTypeGeoKey 3072) and its linear unit (ProjLinearUnitsGeoKey 3076).</summary>
    internal static (string? Crs, string? Units) FromGeoKeys(ushort[] k)
    {
        string? crs = null, units = null;
        var count = k.Length >= 4 ? k[3] : 0;
        for (var i = 0; i < count && 4 + 4 * i + 3 < k.Length; i++)
        {
            var (id, loc, value) = (k[4 + 4 * i], k[4 + 4 * i + 1], k[4 + 4 * i + 3]);
            if (loc != 0) continue;
            if (id == 3072 && value is > 0 and < 32767) crs = $"EPSG:{value}";
            else if (id == 3076) units = value switch { 9001 => "m", 9002 => "ft", 9003 => "usft", _ => units };
        }
        return (crs, units);
    }

    [GeneratedRegex("AUTHORITY\\[\\s*\"EPSG\"\\s*,\\s*\"?(\\d+)\"?\\s*\\]", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorityRx();
    [GeneratedRegex("(?:UNIT|LENGTHUNIT)\\[\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex UnitRx();
}
