using System.IO.Compression;

namespace Lapin.Drawing.Export;

/// <summary>
/// An image as a PDF draws it: a JPEG as it is (DCTDecode), a PNG decoded to
/// its samples (grey or RGB, 8 bits) and its alpha apart, both to be deflated.
/// No library: PNG's chunks, zlib and its five row filters (an interlaced PNG
/// is not read).
/// </summary>
public sealed record Raster(int W, int H, string ColorSpace, byte[] Data, bool Jpeg, byte[]? Alpha = null)
{
    public static Raster? Read(byte[] b)
    {
        try { return b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 ? Jpg(b) : b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 ? Png(b) : null; }
        catch (Exception e) when (e is InvalidDataException or IndexOutOfRangeException or ArgumentException) { return null; }
    }

    static Raster? Jpg(byte[] b)
    {
        var i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF) { i++; continue; }
            var marker = b[i + 1];
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                var (h, w, n) = ((b[i + 5] << 8) | b[i + 6], (b[i + 7] << 8) | b[i + 8], b[i + 9]);
                var cs = n switch { 1 => "DeviceGray", 3 => "DeviceRGB", 4 => "DeviceCMYK", _ => null };
                return cs == null || w == 0 || h == 0 ? null : new Raster(w, h, cs, b, true);
            }
            if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7) { i += 2; continue; }
            i += 2 + ((b[i + 2] << 8) | b[i + 3]);
        }
        return null;
    }

    static int Be(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

    static Raster? Png(byte[] b)
    {
        int w = 0, h = 0, depth = 0, type = 0, interlace = 0;
        byte[]? palette = null, trns = null;
        using var idat = new MemoryStream();
        for (var i = 8; i + 8 <= b.Length;)
        {
            var len = Be(b, i);
            var kind = System.Text.Encoding.ASCII.GetString(b, i + 4, 4);
            var at = i + 8;
            switch (kind)
            {
                case "IHDR": (w, h, depth, type, interlace) = (Be(b, at), Be(b, at + 4), b[at + 8], b[at + 9], b[at + 12]); break;
                case "PLTE": palette = b[at..(at + len)]; break;
                case "tRNS": trns = b[at..(at + len)]; break;
                case "IDAT": idat.Write(b, at, len); break;
            }
            if (kind == "IEND") break;
            i = at + len + 4;
        }
        var channels = type switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
        if (w <= 0 || h <= 0 || channels == 0 || interlace != 0 || (type == 3 && palette == null)) return null;
        // The rows, unfiltered.
        var rowBytes = (w * channels * depth + 7) / 8;
        var bpp = Math.Max(1, channels * depth / 8);
        var raw = new byte[(rowBytes + 1) * h];
        idat.Position = 0;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress)) z.ReadExactly(raw);
        var rows = new byte[rowBytes * h];
        for (var y = 0; y < h; y++)
        {
            var f = raw[y * (rowBytes + 1)];
            var src = y * (rowBytes + 1) + 1;
            var o = y * rowBytes;
            for (var x = 0; x < rowBytes; x++)
            {
                int a = x >= bpp ? rows[o + x - bpp] : 0, up = y > 0 ? rows[o - rowBytes + x] : 0, c = x >= bpp && y > 0 ? rows[o - rowBytes + x - bpp] : 0;
                var v = raw[src + x];
                rows[o + x] = (byte)(f switch
                {
                    1 => v + a,
                    2 => v + up,
                    3 => v + ((a + up) >> 1),
                    4 => v + Paeth(a, up, c),
                    _ => v,
                });
            }
        }
        // Each sample as 8 bits: a 16-bit sample's high byte, a narrower one stretched (a palette index kept).
        int Sample(int y, int k)
        {
            if (depth == 8) return rows[y * rowBytes + k];
            if (depth == 16) return rows[y * rowBytes + 2 * k];
            var bit = k * depth;
            var v = (rows[y * rowBytes + bit / 8] >> (8 - depth - bit % 8)) & ((1 << depth) - 1);
            return type == 3 ? v : v * 255 / ((1 << depth) - 1);
        }
        var colour = type is 2 or 6 || type == 3;
        var data = new byte[w * h * (colour ? 3 : 1)];
        var alpha = type is 4 or 6 || (type == 3 && trns != null) ? new byte[w * h] : null;
        var n = 0;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++, n++)
            {
                if (type == 3)
                {
                    var p = Math.Min(Sample(y, x), palette!.Length / 3 - 1);
                    (data[3 * n], data[3 * n + 1], data[3 * n + 2]) = (palette[3 * p], palette[3 * p + 1], palette[3 * p + 2]);
                    if (alpha != null) alpha[n] = p < trns!.Length ? trns[p] : (byte)255;
                    continue;
                }
                var s = x * channels;
                if (colour) (data[3 * n], data[3 * n + 1], data[3 * n + 2]) = ((byte)Sample(y, s), (byte)Sample(y, s + 1), (byte)Sample(y, s + 2));
                else data[n] = (byte)Sample(y, s);
                if (alpha != null) alpha[n] = (byte)Sample(y, s + channels - 1);
            }
        // An alpha that hides nothing is left out.
        if (alpha != null && alpha.All(v => v == 255)) alpha = null;
        return new Raster(w, h, colour ? "DeviceRGB" : "DeviceGray", data, false, alpha);
    }

    static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
