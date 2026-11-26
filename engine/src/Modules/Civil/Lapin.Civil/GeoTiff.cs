using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// Pure C# TIFF 6.0 / GeoTIFF reader (no external dependencies, works on desktop and browser WASM):
/// reads 1-band DEM rasters (float32, float64, int16, uint16, int32, uint8; uncompressed, Deflate/Zlib, or LZW;
/// strip or tile layout; horizontal differencing predictor 2 and floating-point predictor 3) together with
/// GeoTIFF georeferencing tags (ModelPixelScale, ModelTiepoint, ModelTransformation, GeoKeyDirectory EPSG)
/// or a companion world file (.tfw). Also decodes 3/4-band 8-bit RGB GeoTIFFs into PNG for orthophotos.
/// </summary>
public static class GeoTiff
{
    public sealed record RgbImage(int Width, int Height, byte[] PngBytes, (double X, double Y)[] MapCorners, string? Crs);

    /// <summary>Reads a single-band DEM GeoTIFF into an <see cref="ElevationGrid"/>.</summary>
    public static ElevationGrid ReadDem(byte[] bytes, double[]? worldFile = null)
    {
        var tiff = Parse(bytes);
        if (tiff.SamplesPerPixel != 1)
            throw new CommandException($"Expected a 1-band elevation GeoTIFF, got {tiff.SamplesPerPixel} bands (for RGB imagery, use Orthophoto).");
        var transform = tiff.Transform ?? worldFile
            ?? throw new CommandException("The GeoTIFF has no georeferencing tags; provide a world file (.tfw) or corners.");
        var raw = DecodePixels(bytes, tiff);
        var heights = new double[tiff.Width * tiff.Height];
        var bytesPerSample = tiff.BitsPerSample / 8;
        var nodata = tiff.NoData;

        for (var i = 0; i < heights.Length; i++)
        {
            var off = i * bytesPerSample;
            double v = (tiff.SampleFormat, tiff.BitsPerSample) switch
            {
                (3, 32) => tiff.LittleEndian
                    ? BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(off, 4))
                    : BinaryPrimitives.ReadSingleBigEndian(raw.AsSpan(off, 4)),
                (3, 64) => tiff.LittleEndian
                    ? BinaryPrimitives.ReadDoubleLittleEndian(raw.AsSpan(off, 8))
                    : BinaryPrimitives.ReadDoubleBigEndian(raw.AsSpan(off, 8)),
                (2, 16) => tiff.LittleEndian
                    ? BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(off, 2))
                    : BinaryPrimitives.ReadInt16BigEndian(raw.AsSpan(off, 2)),
                (2, 32) => tiff.LittleEndian
                    ? BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(off, 4))
                    : BinaryPrimitives.ReadInt32BigEndian(raw.AsSpan(off, 4)),
                (1, 16) => tiff.LittleEndian
                    ? BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(off, 2))
                    : BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(off, 2)),
                (1, 32) => tiff.LittleEndian
                    ? BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(off, 4))
                    : BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(off, 4)),
                (1, 8) => raw[off],
                (2, 8) => (sbyte)raw[off],
                _ => throw new CommandException($"Unsupported GeoTIFF DEM sample format ({tiff.SampleFormat}) / depth ({tiff.BitsPerSample}-bit)."),
            };
            if (!double.IsFinite(v) || (nodata is { } nd && Math.Abs(v - nd) <= Math.Max(1e-6, Math.Abs(nd) * 1e-6)))
                v = double.NaN;
            heights[i] = v;
        }
        return new ElevationGrid(tiff.Width, tiff.Height, heights, transform, tiff.Crs);
    }

    /// <summary>Reads a 3- or 4-band 8-bit RGB GeoTIFF orthophoto and encodes it as PNG with its map corners.</summary>
    public static RgbImage ReadOrthophoto(byte[] bytes, double[]? worldFile = null)
    {
        var tiff = Parse(bytes);
        if (tiff.SamplesPerPixel is not (3 or 4) || tiff.BitsPerSample != 8)
            throw new CommandException("A GeoTIFF orthophoto must be 8-bit RGB or RGBA.");
        var transform = tiff.Transform ?? worldFile
            ?? throw new CommandException("The GeoTIFF has no georeferencing tags; provide a world file (.tfw).");
        var raw = DecodePixels(bytes, tiff);
        var png = EncodePng(tiff.Width, tiff.Height, tiff.SamplesPerPixel, raw);
        var corners = Orthophotos.MapCorners(transform, tiff.Width, tiff.Height);
        return new RgbImage(tiff.Width, tiff.Height, png, corners, tiff.Crs);
    }

    /// <summary>Checks whether a byte buffer starts with a TIFF magic header (II 42 or MM 42).</summary>
    public static bool IsTiff(ReadOnlySpan<byte> b) =>
        b.Length >= 8 && ((b[0] == 'I' && b[1] == 'I' && b[2] == 42 && b[3] == 0) || (b[0] == 'M' && b[1] == 'M' && b[2] == 0 && b[3] == 42));

    // ─── IFD parsing ───

    sealed class TiffInfo
    {
        public bool LittleEndian;
        public int Width, Height;
        public int BitsPerSample = 8;
        public int Compression = 1;
        public int SamplesPerPixel = 1;
        public int SampleFormat = 1; // 1=uint, 2=int, 3=float
        public int Predictor = 1;
        public int RowsPerStrip;
        public int TileWidth, TileHeight;
        public long[] StripOffsets = [];
        public long[] StripByteCounts = [];
        public long[] TileOffsets = [];
        public long[] TileByteCounts = [];
        public double[]? Transform;
        public string? Crs;
        public double? NoData;
    }

    static TiffInfo Parse(byte[] b)
    {
        if (b.Length < 8) throw new CommandException("The file is too small to be a TIFF.");
        bool le;
        if (b[0] == 'I' && b[1] == 'I') le = true;
        else if (b[0] == 'M' && b[1] == 'M') le = false;
        else throw new CommandException("The file is not a TIFF.");

        ushort U16(int o) => le ? BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o, 2)) : BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(o, 2));
        uint U32(int o) => le ? BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o, 4)) : BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o, 4));
        double F64(int o) => le ? BinaryPrimitives.ReadDoubleLittleEndian(b.AsSpan(o, 8)) : BinaryPrimitives.ReadDoubleBigEndian(b.AsSpan(o, 8));

        var magic = U16(2);
        if (magic != 42) throw new CommandException(magic == 43 ? "BigTIFF is not supported yet; export as standard GeoTIFF." : "The file is not a valid TIFF.");
        var ifdOffset = (int)U32(4);
        if (ifdOffset <= 0 || ifdOffset + 2 > b.Length) throw new CommandException("Invalid TIFF IFD offset.");

        var info = new TiffInfo { LittleEndian = le };
        double[]? pixelScale = null, tiepoint = null, modelTransform = null;
        ushort[]? geoKeys = null;

        var count = U16(ifdOffset);
        var pos = ifdOffset + 2;
        for (var i = 0; i < count && pos + 12 <= b.Length; i++, pos += 12)
        {
            var tag = U16(pos);
            var type = U16(pos + 2);
            var n = (int)U32(pos + 4);
            var valOff = pos + 8;

            int TypeSize() => type switch { 1 or 2 or 6 or 7 => 1, 3 or 8 => 2, 4 or 9 or 11 => 4, 5 or 10 or 12 => 8, _ => 1 };
            int DataOffset() => n * TypeSize() <= 4 ? valOff : (int)U32(valOff);

            long ReadIntAt(int o) => type switch
            {
                1 => b[o],
                3 => U16(o),
                4 => U32(o),
                _ => U32(o),
            };
            long FirstInt() => ReadIntAt(DataOffset());
            long[] ReadInts()
            {
                var off = DataOffset();
                var sz = TypeSize();
                var arr = new long[n];
                for (var k = 0; k < n && off + k * sz + sz <= b.Length; k++) arr[k] = ReadIntAt(off + k * sz);
                return arr;
            }
            double[] ReadDoubles()
            {
                var off = DataOffset();
                var arr = new double[n];
                for (var k = 0; k < n && off + k * 8 + 8 <= b.Length; k++) arr[k] = F64(off + k * 8);
                return arr;
            }
            ushort[] ReadShorts()
            {
                var off = DataOffset();
                var arr = new ushort[n];
                for (var k = 0; k < n && off + k * 2 + 2 <= b.Length; k++) arr[k] = U16(off + k * 2);
                return arr;
            }

            switch (tag)
            {
                case 256: info.Width = (int)FirstInt(); break;
                case 257: info.Height = (int)FirstInt(); break;
                case 258: info.BitsPerSample = (int)FirstInt(); break;
                case 259: info.Compression = (int)FirstInt(); break;
                case 273: info.StripOffsets = ReadInts(); break;
                case 277: info.SamplesPerPixel = (int)FirstInt(); break;
                case 278: info.RowsPerStrip = (int)FirstInt(); break;
                case 279: info.StripByteCounts = ReadInts(); break;
                case 284:
                    if (FirstInt() == 2) throw new CommandException("Planar-separated TIFFs are not supported; save with interleaved (chunky) pixels.");
                    break;
                case 317: info.Predictor = (int)FirstInt(); break;
                case 322: info.TileWidth = (int)FirstInt(); break;
                case 323: info.TileHeight = (int)FirstInt(); break;
                case 324: info.TileOffsets = ReadInts(); break;
                case 325: info.TileByteCounts = ReadInts(); break;
                case 339: info.SampleFormat = (int)FirstInt(); break;
                case 33550: pixelScale = ReadDoubles(); break;
                case 33922: tiepoint = ReadDoubles(); break;
                case 34264: modelTransform = ReadDoubles(); break;
                case 34735: geoKeys = ReadShorts(); break;
                case 42113:
                    var off = DataOffset();
                    if (off >= 0 && off + n <= b.Length)
                    {
                        var s = Encoding.ASCII.GetString(b, off, n).Trim('\0', ' ');
                        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var nd) && double.IsFinite(nd))
                            info.NoData = nd;
                    }
                    break;
            }
        }

        if (info.Width < 2 || info.Height < 2) throw new CommandException("Invalid TIFF image dimensions.");
        if ((long)info.Width * info.Height > 16_000_000) throw new CommandException("GeoTIFF is larger than 16 megapixels; crop or resample it first.");
        if (info.RowsPerStrip <= 0) info.RowsPerStrip = info.Height;

        // GeoTIFF affine transform (pixel centre convention as world files).
        if (modelTransform is { Length: >= 16 })
        {
            double a = modelTransform[0], b1 = modelTransform[1], c0 = modelTransform[3];
            double d = modelTransform[4], e = modelTransform[5], f0 = modelTransform[7];
            info.Transform = [a, d, b1, e, c0 + 0.5 * a + 0.5 * b1, f0 + 0.5 * d + 0.5 * e];
        }
        else if (pixelScale is { Length: >= 2 } && tiepoint is { Length: >= 6 } && pixelScale[0] > 0 && pixelScale[1] > 0)
        {
            double sx = pixelScale[0], sy = -pixelScale[1];
            // Tiepoint maps raster (i, j) to map (x, y); raster (0, 0) is upper-left pixel corner → pixel centre (0,0) is at (0.5, 0.5).
            double originX = tiepoint[3] - tiepoint[0] * sx;
            double originY = tiepoint[4] - tiepoint[1] * sy;
            info.Transform = [sx, 0, 0, sy, originX + 0.5 * sx, originY + 0.5 * sy];
        }

        if (geoKeys is { Length: >= 8 })
            info.Crs = ReadGeoKeysCrs(geoKeys);

        return info;
    }

    static string? ReadGeoKeysCrs(ushort[] keys)
    {
        var count = keys[3];
        int? projected = null, geographic = null;
        for (var i = 0; i < count && 4 + (i + 1) * 4 <= keys.Length; i++)
        {
            var id = keys[4 + i * 4];
            var loc = keys[4 + i * 4 + 1];
            var val = keys[4 + i * 4 + 3];
            if (loc != 0 || val is 0 or 32767) continue;
            if (id == 3072) projected = val;
            else if (id == 2048) geographic = val;
        }
        var code = projected ?? geographic;
        return code is > 0 ? $"EPSG:{code}" : null;
    }

    // ─── Strip / Tile decoding ───

    static byte[] DecodePixels(byte[] file, TiffInfo info)
    {
        var pixelBytes = info.SamplesPerPixel * (info.BitsPerSample / 8);
        var outBuf = new byte[info.Width * info.Height * pixelBytes];

        if (info.TileOffsets.Length > 0 && info.TileWidth > 0 && info.TileHeight > 0)
        {
            var tilesX = (info.Width + info.TileWidth - 1) / info.TileWidth;
            var tilesY = (info.Height + info.TileHeight - 1) / info.TileHeight;
            var tileRowBytes = info.TileWidth * pixelBytes;
            for (var ty = 0; ty < tilesY; ty++)
                for (var tx = 0; tx < tilesX; tx++)
                {
                    var ti = ty * tilesX + tx;
                    if (ti >= info.TileOffsets.Length) break;
                    var raw = Decompress(file, (int)info.TileOffsets[ti], (int)info.TileByteCounts[ti], info.Compression, tileRowBytes * info.TileHeight);
                    UndoPredictor(raw, info.TileWidth, info.TileHeight, info);
                    var copyW = Math.Min(info.TileWidth, info.Width - tx * info.TileWidth);
                    var copyH = Math.Min(info.TileHeight, info.Height - ty * info.TileHeight);
                    for (var r = 0; r < copyH; r++)
                        Buffer.BlockCopy(raw, r * tileRowBytes, outBuf, ((ty * info.TileHeight + r) * info.Width + tx * info.TileWidth) * pixelBytes, copyW * pixelBytes);
                }
        }
        else if (info.StripOffsets.Length > 0)
        {
            var rowBytes = info.Width * pixelBytes;
            var row = 0;
            for (var s = 0; s < info.StripOffsets.Length && row < info.Height; s++)
            {
                var rowsHere = Math.Min(info.RowsPerStrip, info.Height - row);
                var raw = Decompress(file, (int)info.StripOffsets[s], (int)info.StripByteCounts[s], info.Compression, rowsHere * rowBytes);
                UndoPredictor(raw, info.Width, rowsHere, info);
                var len = Math.Min(raw.Length, rowsHere * rowBytes);
                Buffer.BlockCopy(raw, 0, outBuf, row * rowBytes, len);
                row += rowsHere;
            }
        }
        else
            throw new CommandException("The TIFF contains neither strips nor tiles.");

        return outBuf;
    }

    static byte[] Decompress(byte[] file, int offset, int count, int compression, int expectedBytes)
    {
        if (offset < 0 || count < 0 || offset + count > file.Length)
            throw new CommandException("Corrupt TIFF strip/tile offset.");
        return compression switch
        {
            1 => file.AsSpan(offset, count).ToArray(),
            8 or 32946 => InflateZlib(file, offset, count, expectedBytes),
            5 => DecompressLzw(file, offset, count, expectedBytes),
            _ => throw new CommandException($"Unsupported TIFF compression ({compression}). Supported: uncompressed, Deflate, LZW."),
        };
    }

    static byte[] InflateZlib(byte[] file, int offset, int count, int expected)
    {
        using var ms = new MemoryStream(file, offset, count, writable: false);
        using var zs = new ZLibStream(ms, CompressionMode.Decompress);
        using var outMs = new MemoryStream(expected);
        zs.CopyTo(outMs);
        return outMs.ToArray();
    }

    /// <summary>Standard TIFF 6.0 MSB-first LZW decompression.</summary>
    static byte[] DecompressLzw(byte[] file, int offset, int count, int expected)
    {
        const int Clear = 256, Eoi = 257;
        var outBuf = new List<byte>(expected);
        var table = new List<byte[]>(4096);
        void Reset()
        {
            table.Clear();
            for (var i = 0; i < 256; i++) table.Add([(byte)i]);
            table.Add([]); // 256 Clear
            table.Add([]); // 257 EOI
        }
        Reset();

        int bitPos = 0, codeBits = 9;
        int ReadCode()
        {
            var bytePos = bitPos >> 3;
            if (bytePos >= count) return Eoi;
            var shift = bitPos & 7;
            uint acc = file[offset + bytePos];
            if (bytePos + 1 < count) acc = (acc << 8) | file[offset + bytePos + 1];
            else acc <<= 8;
            if (bytePos + 2 < count) acc = (acc << 8) | file[offset + bytePos + 2];
            else acc <<= 8;
            bitPos += codeBits;
            return (int)((acc >> (24 - shift - codeBits)) & ((1u << codeBits) - 1));
        }

        byte[]? prev = null;
        while (true)
        {
            var code = ReadCode();
            if (code == Eoi) break;
            if (code == Clear)
            {
                Reset();
                codeBits = 9;
                prev = null;
                continue;
            }
            byte[] entry;
            if (code < table.Count) entry = table[code];
            else if (code == table.Count && prev != null)
            {
                entry = new byte[prev.Length + 1];
                Buffer.BlockCopy(prev, 0, entry, 0, prev.Length);
                entry[^1] = prev[0];
            }
            else break;

            outBuf.AddRange(entry);
            if (prev != null && table.Count < 4096)
            {
                var next = new byte[prev.Length + 1];
                Buffer.BlockCopy(prev, 0, next, 0, prev.Length);
                next[^1] = entry[0];
                table.Add(next);
                // TIFF LZW increments code width one entry early (at 511, 1023, 2047).
                if (table.Count is 511 or 1023 or 2047) codeBits++;
            }
            prev = entry;
        }
        return [.. outBuf];
    }

    static void UndoPredictor(byte[] buf, int width, int height, TiffInfo info)
    {
        if (info.Predictor == 1) return;
        var spp = info.SamplesPerPixel;
        var bps = info.BitsPerSample / 8;
        var rowBytes = width * spp * bps;

        if (info.Predictor == 2)
        {
            for (var r = 0; r < height; r++)
            {
                var rowStart = r * rowBytes;
                if (bps == 1)
                {
                    for (var i = spp; i < rowBytes && rowStart + i < buf.Length; i++)
                        buf[rowStart + i] = unchecked((byte)(buf[rowStart + i] + buf[rowStart + i - spp]));
                }
                else if (bps == 2)
                {
                    var stride = spp * 2;
                    for (var i = stride; i + 1 < rowBytes && rowStart + i + 1 < buf.Length; i += 2)
                    {
                        var prev = info.LittleEndian
                            ? BinaryPrimitives.ReadUInt16LittleEndian(buf.AsSpan(rowStart + i - stride, 2))
                            : BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(rowStart + i - stride, 2));
                        var cur = info.LittleEndian
                            ? BinaryPrimitives.ReadUInt16LittleEndian(buf.AsSpan(rowStart + i, 2))
                            : BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(rowStart + i, 2));
                        var sum = unchecked((ushort)(prev + cur));
                        if (info.LittleEndian) BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(rowStart + i, 2), sum);
                        else BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(rowStart + i, 2), sum);
                    }
                }
            }
        }
        else if (info.Predictor == 3)
        {
            // Adobe TIFF Technical Note 3: floating-point horizontal differencing across byte planes (stored big-endian per plane).
            var tmp = new byte[rowBytes];
            var samplesPerRow = width * spp;
            for (var r = 0; r < height; r++)
            {
                var rowStart = r * rowBytes;
                if (rowStart + rowBytes > buf.Length) break;
                Buffer.BlockCopy(buf, rowStart, tmp, 0, rowBytes);
                for (var i = 1; i < rowBytes; i++) tmp[i] = unchecked((byte)(tmp[i] + tmp[i - 1]));
                for (var s = 0; s < samplesPerRow; s++)
                    for (var b = 0; b < bps; b++)
                    {
                        // Plane 0 is MSB, plane bps-1 is LSB.
                        var dstByte = info.LittleEndian ? (bps - 1 - b) : b;
                        buf[rowStart + s * bps + dstByte] = tmp[b * samplesPerRow + s];
                    }
            }
        }
    }

    // ─── Minimal PNG encoder for RGB/RGBA GeoTIFF orthophotos ───

    static byte[] EncodePng(int width, int height, int channels, byte[] rgb)
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        void WriteChunk(string type, byte[] data)
        {
            Span<byte> len = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(len, (uint)data.Length);
            ms.Write(len);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            ms.Write(typeBytes);
            ms.Write(data);
            Span<byte> crc = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typeBytes, data));
            ms.Write(crc);
        }

        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = channels == 4 ? (byte)6 : (byte)2; // RGBA or RGB
        WriteChunk("IHDR", ihdr);

        using var idat = new MemoryStream();
        using (var zs = new ZLibStream(idat, CompressionLevel.Fastest, leaveOpen: true))
        {
            var rowLen = width * channels;
            for (var r = 0; r < height; r++)
            {
                zs.WriteByte(0); // filter: None
                zs.Write(rgb, r * rowLen, rowLen);
            }
        }
        WriteChunk("IDAT", idat.ToArray());
        WriteChunk("IEND", []);
        return ms.ToArray();
    }

    static uint Crc32(byte[] a, byte[] b)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var x in a) crc = CrcStep(crc, x);
        foreach (var x in b) crc = CrcStep(crc, x);
        return crc ^ 0xFFFFFFFF;
    }

    static uint CrcStep(uint crc, byte v)
    {
        crc ^= v;
        for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        return crc;
    }
}
