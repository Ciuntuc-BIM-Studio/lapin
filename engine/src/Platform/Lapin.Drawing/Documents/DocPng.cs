using System.IO.Compression;
using System.Text;

namespace Lapin.Drawing.Documents;

/// <summary>
/// A PNG written by hand (the W3C's PNG specification): 8-bit RGB, no filter, one zlib IDAT. The fallback a DOCX picture
/// needs when it has its SVG alone (Word wants a bitmap blip beside the SVG one).
/// </summary>
public static class DocPng
{
    /// <summary>The image from its rows of RGB bytes, top first (<paramref name="rgb"/>: W × H × 3).</summary>
    public static byte[] Encode(int w, int h, byte[] rgb)
    {
        if (w < 1 || h < 1 || rgb.Length < w * h * 3) throw new ArgumentException("rgb holds fewer than w × h pixels");
        var raw = new byte[h * (w * 3 + 1)];
        for (var y = 0; y < h; y++) Buffer.BlockCopy(rgb, y * w * 3, raw, y * (w * 3 + 1) + 1, w * 3);   // filter 0 a row
        using var z = new MemoryStream();
        using (var d = new ZLibStream(z, CompressionLevel.Optimal)) d.Write(raw);
        using var f = new MemoryStream();
        f.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BigEndian(ihdr, 0, (uint)w);
        BigEndian(ihdr, 4, (uint)h);
        ihdr[8] = 8; ihdr[9] = 2;   // 8 bits, truecolour; compression, filter and interlace 0
        Chunk(f, "IHDR", ihdr);
        Chunk(f, "IDAT", z.ToArray());
        Chunk(f, "IEND", []);
        return f.ToArray();
    }

    /// <summary>The image from its rows of RGBA bytes, top first (<paramref name="rgba"/>: W × H × 4), see-through where alpha is 0.</summary>
    public static byte[] EncodeRgba(int w, int h, byte[] rgba)
    {
        if (w < 1 || h < 1 || rgba.Length < w * h * 4) throw new ArgumentException("rgba holds fewer than w × h pixels");
        var raw = new byte[h * (w * 4 + 1)];
        for (var y = 0; y < h; y++) Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
        using var z = new MemoryStream();
        using (var d = new ZLibStream(z, CompressionLevel.Fastest)) d.Write(raw);
        using var f = new MemoryStream();
        f.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BigEndian(ihdr, 0, (uint)w);
        BigEndian(ihdr, 4, (uint)h);
        ihdr[8] = 8; ihdr[9] = 6;   // 8 bits, truecolour with alpha
        Chunk(f, "IHDR", ihdr);
        Chunk(f, "IDAT", z.ToArray());
        Chunk(f, "IEND", []);
        return f.ToArray();
    }

    /// <summary>One white pixel.</summary>
    public static byte[] White() => Encode(1, 1, [255, 255, 255]);

    static void Chunk(Stream f, string type, byte[] data)
    {
        var head = new byte[4];
        BigEndian(head, 0, (uint)data.Length);
        f.Write(head);
        var t = Encoding.ASCII.GetBytes(type);
        f.Write(t);
        f.Write(data);
        var crc = Crc(Crc(0xFFFFFFFF, t), data) ^ 0xFFFFFFFF;
        BigEndian(head, 0, crc);
        f.Write(head);
    }

    static readonly uint[] Table = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    static uint Crc(uint c, byte[] data)
    {
        foreach (var b in data) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c;
    }

    static void BigEndian(byte[] b, int at, uint v) { b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v; }
}
