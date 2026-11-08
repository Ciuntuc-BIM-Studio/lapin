using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Lapin.Engine.Tests;

/// <summary>Tiny point clouds written by the tests themselves, after the formats' specifications (never large files in the repo).</summary>
static class PointCloudSamples
{
    public sealed record P(double X, double Y, double Z, ushort R = 0, ushort G = 0, ushort B = 0, ushort I = 0, byte C = 0);

    /// <summary>A grid of points over a box: a floor and two walls, coloured by position, intensity rising, classes 2 (ground) and 6 (building).</summary>
    public static List<P> Room(int n = 40, double ox = 0, double oy = 0, double oz = 0)
    {
        var o = new List<P>();
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
            {
                var (u, v) = (i * 5.0 / (n - 1), j * 4.0 / (n - 1));
                o.Add(new P(ox + u, oy + v, oz, (ushort)(i * 6), (ushort)(j * 6), 100, (ushort)(i * 1000), 2));
                o.Add(new P(ox + u, oy, oz + j * 3.0 / (n - 1), 200, (ushort)(i * 6), 50, (ushort)(j * 1000), 6));
                o.Add(new P(ox, oy + v * 1.0, oz + i * 3.0 / (n - 1), 50, 200, (ushort)(j * 6), (ushort)(i * 500), 6));
            }
        return o;
    }

    /// <summary>A LAS file: version 1.<paramref name="minor"/>, the point format given (0–10), the WKT or GeoTIFF keys given.</summary>
    public static byte[] Las(IReadOnlyList<P> pts, int format = 2, int minor = 2, double scale = 0.001, double[]? offset = null, string? wkt = null, ushort[]? geoKeys = null, bool rgb16 = true)
    {
        offset ??= [0, 0, 0];
        var headerSize = minor >= 4 ? 375 : 227;
        var vlrs = new MemoryStream();
        var vlrCount = 0;
        void Vlr(string user, ushort id, byte[] body)
        {
            var h = new byte[54];
            Encoding.ASCII.GetBytes(user).CopyTo(h, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(18), id);
            BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(20), (ushort)body.Length);
            vlrs.Write(h); vlrs.Write(body); vlrCount++;
        }
        if (wkt != null) Vlr("LASF_Projection", 2112, Encoding.UTF8.GetBytes(wkt + "\0"));
        if (geoKeys != null) { var b = new byte[geoKeys.Length * 2]; for (var i = 0; i < geoKeys.Length; i++) BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(2 * i), geoKeys[i]); Vlr("LASF_Projection", 34735, b); }
        var len = format switch { 0 => 20, 1 => 28, 2 => 26, 3 => 34, 6 => 30, 7 => 36, 8 => 38, _ => throw new ArgumentException("format") };
        var head = new byte[headerSize];
        "LASF"u8.CopyTo(head);
        head[24] = 1; head[25] = (byte)minor;
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(94), (ushort)headerSize);
        var offsetToPoints = (uint)(headerSize + vlrs.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(96), offsetToPoints);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(100), (uint)vlrCount);
        head[104] = (byte)format;
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(105), (ushort)len);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(107), format >= 6 ? 0u : (uint)pts.Count);
        for (var k = 0; k < 3; k++) { BinaryPrimitives.WriteDoubleLittleEndian(head.AsSpan(131 + 8 * k), scale); BinaryPrimitives.WriteDoubleLittleEndian(head.AsSpan(155 + 8 * k), offset[k]); }
        void D(int at, double v) => BinaryPrimitives.WriteDoubleLittleEndian(head.AsSpan(at), v);
        D(179, pts.Max(p => p.X)); D(187, pts.Min(p => p.X)); D(195, pts.Max(p => p.Y)); D(203, pts.Min(p => p.Y)); D(211, pts.Max(p => p.Z)); D(219, pts.Min(p => p.Z));
        if (minor >= 4) BinaryPrimitives.WriteUInt64LittleEndian(head.AsSpan(247), (ulong)pts.Count);
        var o = new MemoryStream();
        o.Write(head); vlrs.WriteTo(o);
        var rec = new byte[len];
        foreach (var p in pts)
        {
            Array.Clear(rec);
            BinaryPrimitives.WriteInt32LittleEndian(rec, (int)Math.Round((p.X - offset[0]) / scale));
            BinaryPrimitives.WriteInt32LittleEndian(rec.AsSpan(4), (int)Math.Round((p.Y - offset[1]) / scale));
            BinaryPrimitives.WriteInt32LittleEndian(rec.AsSpan(8), (int)Math.Round((p.Z - offset[2]) / scale));
            BinaryPrimitives.WriteUInt16LittleEndian(rec.AsSpan(12), p.I);
            if (format >= 6) { rec[14] = 0x11; rec[16] = p.C; } else { rec[14] = 0x09; rec[15] = (byte)(p.C & 0x1F); }
            var at = format switch { 2 => 20, 3 => 28, 7 or 8 => 30, _ => -1 };
            if (at >= 0)
            {
                ushort S(ushort v) => rgb16 ? (ushort)(v * 257) : v;
                BinaryPrimitives.WriteUInt16LittleEndian(rec.AsSpan(at), S(p.R));
                BinaryPrimitives.WriteUInt16LittleEndian(rec.AsSpan(at + 2), S(p.G));
                BinaryPrimitives.WriteUInt16LittleEndian(rec.AsSpan(at + 4), S(p.B));
            }
            o.Write(rec);
        }
        return o.ToArray();
    }

    public static byte[] PlyAscii(IReadOnlyList<P> pts)
    {
        var sb = new StringBuilder($"ply\nformat ascii 1.0\ncomment made by Lapin's tests\nelement vertex {pts.Count}\nproperty float x\nproperty float y\nproperty float z\nproperty uchar red\nproperty uchar green\nproperty uchar blue\nelement face 1\nproperty list uchar int vertex_indices\nend_header\n");
        foreach (var p in pts) sb.Append(string.Create(CultureInfo.InvariantCulture, $"{p.X} {p.Y} {p.Z} {p.R} {p.G} {p.B}\n"));
        sb.Append("3 0 1 2\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    public static byte[] PlyBinary(IReadOnlyList<P> pts, bool big = false)
    {
        var o = new MemoryStream();
        o.Write(Encoding.ASCII.GetBytes($"ply\nformat binary_{(big ? "big" : "little")}_endian 1.0\nelement vertex {pts.Count}\nproperty double x\nproperty double y\nproperty double z\nproperty uchar red\nproperty uchar green\nproperty uchar blue\nproperty float scalar_Intensity\nproperty uchar scalar_Classification\nend_header\n"));
        var b = new byte[8];
        foreach (var p in pts)
        {
            foreach (var v in new[] { p.X, p.Y, p.Z }) { if (big) BinaryPrimitives.WriteDoubleBigEndian(b, v); else BinaryPrimitives.WriteDoubleLittleEndian(b, v); o.Write(b, 0, 8); }
            o.WriteByte((byte)p.R); o.WriteByte((byte)p.G); o.WriteByte((byte)p.B);
            if (big) BinaryPrimitives.WriteSingleBigEndian(b, p.I / 65535f); else BinaryPrimitives.WriteSingleLittleEndian(b, p.I / 65535f);
            o.Write(b, 0, 4);
            o.WriteByte(p.C);
        }
        return o.ToArray();
    }

    public static byte[] Text(IReadOnlyList<P> pts, string kind)
    {
        var sb = new StringBuilder();
        if (kind == "pts") sb.Append(pts.Count).Append('\n');
        if (kind == "csv") sb.Append("X;Y;Z;Intensity;Red;Green;Blue;Classification\n");
        foreach (var p in pts)
            sb.Append(kind switch
            {
                "pts" => string.Create(CultureInfo.InvariantCulture, $"{p.X:0.000} {p.Y:0.000} {p.Z:0.000} {p.I / 16 - 2048} {p.R} {p.G} {p.B}\n"),
                "csv" => string.Create(CultureInfo.InvariantCulture, $"{p.X:0.000};{p.Y:0.000};{p.Z:0.000};{p.I};{p.R};{p.G};{p.B};{p.C}\n").Replace('.', ','),
                _ => string.Create(CultureInfo.InvariantCulture, $"{p.X:0.000} {p.Y:0.000} {p.Z:0.000} {p.R} {p.G} {p.B}\n"),
            });
        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
