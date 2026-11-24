using System.Buffers.Binary;
using System.Numerics;

namespace Lapin.PointClouds;

/// <summary>
/// WebP lossless images (VP8L), Lapin's own decoder written after RFC 9649 (§3, "Specification for WebP Lossless
/// Bitstream"), managed only: the RIFF container (a plain <c>VP8L</c> chunk, or one inside an extended <c>VP8X</c> file), the
/// four transforms (predictor, colour, subtract green, colour indexing with pixel bundling), the prefix codes (simple and
/// normal code length codes, meta prefix codes by an entropy image), the colour cache and LZ77 backward references with
/// the 120 neighbourhood distance codes. A lossy (VP8) image is refused with a message: SOG's images are lossless by its
/// specification. Pixels come back as ARGB, one uint each (alpha in the top byte), row by row from the top left.
/// </summary>
public static class WebPLossless
{
    /// <summary>A WebP that is not lossless, or not one at all: the message names what it is.</summary>
    public sealed class WebPException(string message) : Exception(message);

    /// <summary>The transforms an image used, in the order they were read (for the tests: each one is covered).</summary>
    [Flags]
    public enum Transforms { None = 0, Predictor = 1, Color = 2, SubtractGreen = 4, ColorIndexing = 8, ColorCache = 16, MetaPrefix = 32, Backrefs = 64 }

    public sealed record Image(int Width, int Height, uint[] Argb, Transforms Used);

    /// <summary>A WebP file's pixels.</summary>
    public static Image Decode(ReadOnlySpan<byte> file)
    {
        if (file.Length < 12 || !file.StartsWith("RIFF"u8) || !file[8..].StartsWith("WEBP"u8))
            throw new WebPException("not a WebP image (no RIFF … WEBP header)");
        var end = (int)Math.Min(file.Length, 8L + BinaryPrimitives.ReadUInt32LittleEndian(file[4..]));
        var at = 12;
        while (at + 8 <= end)
        {
            var tag = file.Slice(at, 4);
            var size = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(file[(at + 4)..]), (uint)(end - at - 8));
            var body = file.Slice(at + 8, size);
            if (tag.SequenceEqual("VP8L"u8)) return DecodeVp8L(body);
            if (tag.SequenceEqual("VP8 "u8)) throw new WebPException("a lossy WebP image (VP8): Lapin reads lossless WebP only, as SOG's images must be");
            at += 8 + size + (size & 1);
        }
        throw new WebPException("a WebP file without an image chunk");
    }

    /// <summary>A VP8L chunk's bitstream (from its signature byte, 0x2f).</summary>
    public static Image DecodeVp8L(ReadOnlySpan<byte> data)
    {
        if (data.Length < 5 || data[0] != 0x2f) throw new WebPException("its VP8L signature is missing");
        var br = new BitReader(data[1..].ToArray());
        var width = (int)br.Read(14) + 1;
        var height = (int)br.Read(14) + 1;
        br.Read(1); // alpha_is_used: a hint only.
        if (br.Read(3) != 0) throw new WebPException("a VP8L version other than 0");
        var d = new Decoder(br);
        var argb = d.ImageStream(width, height);
        return new Image(width, height, argb, d.Used);
    }

    /// <summary>Bits read least significant first, as VP8L writes them; past the end they are zeros (a cut file is caught by its size checks).</summary>
    sealed class BitReader(byte[] data)
    {
        ulong _buf;
        int _bits;
        int _pos;
        public bool Overrun;

        void Fill()
        {
            while (_bits <= 56)
            {
                if (_pos < data.Length) _buf |= (ulong)data[_pos] << _bits;
                else if (_pos >= data.Length + 8) Overrun = true;
                _pos++;
                _bits += 8;
            }
        }

        public uint Peek(int n) { if (_bits < n) Fill(); return (uint)(_buf & ((1UL << n) - 1)); }
        public void Skip(int n) { _buf >>= n; _bits -= n; }
        public uint Read(int n)
        {
            if (n == 0) return 0;
            var v = Peek(n);
            Skip(n);
            return v;
        }
    }

    /// <summary>
    /// A canonical prefix code (RFC 9649 §3.7.2.1; deflate's assignment of codes from lengths), decoded through a table of its
    /// first <see cref="Primary"/> bits and, for longer codes, bit by bit.
    /// </summary>
    sealed class PrefixCode
    {
        const int Primary = 10;
        readonly int[] _table = [];   // (symbol << 4) | length for codes up to Primary bits; −1 for longer
        readonly int[] _count = new int[16];
        readonly int[] _symbols = [];
        readonly int _single = -1;   // a code of one symbol: no bits at all

        public PrefixCode(int[] lengths)
        {
            var nonzero = 0;
            var last = 0;
            for (var s = 0; s < lengths.Length; s++) if (lengths[s] > 0) { nonzero++; last = s; _count[lengths[s]]++; }
            if (nonzero == 0) { _single = 0; return; }       // an empty code: decodes as symbol 0 (§3.7.2.1.1, note).
            if (nonzero == 1) { _single = last; return; }
            // Complete (Kraft's sum exactly 1), as the format asks.
            long kraft = 0;
            for (var l = 1; l < 16; l++) kraft += (long)_count[l] << (15 - l);
            if (kraft != 1L << 15) throw new WebPException("a prefix code that is not complete");
            var offs = new int[16];
            for (var l = 1; l < 16; l++) offs[l] = offs[l - 1] + _count[l - 1];
            _symbols = new int[nonzero];
            for (var s = 0; s < lengths.Length; s++) if (lengths[s] > 0) _symbols[offs[lengths[s]]++] = s;
            _table = new int[1 << Primary];
            Array.Fill(_table, -1);
            var code = 0;
            var k = 0;
            for (var l = 1; l < 16; l++)
            {
                for (var i = 0; i < _count[l]; i++, k++, code++)
                {
                    if (l > Primary) continue;
                    var rev = Reverse(code, l);
                    for (var j = rev; j < 1 << Primary; j += 1 << l) _table[j] = _symbols[k] << 4 | l;
                }
                code <<= 1;
            }
        }

        static int Reverse(int code, int len)
        {
            var r = 0;
            for (var i = 0; i < len; i++) { r = r << 1 | code & 1; code >>= 1; }
            return r;
        }

        public int Read(BitReader br)
        {
            if (_single >= 0) return _single;
            var e = _table[br.Peek(Primary)];
            if (e >= 0) { br.Skip(e & 15); return e >> 4; }
            // Longer than the table: canonical decoding bit by bit (code, first and index per length).
            int c = 0, first = 0, index = 0;
            for (var l = 1; l < 16; l++)
            {
                c |= (int)br.Read(1);
                var n = _count[l];
                if (c - first < n) return _symbols[index + c - first];
                index += n;
                first += n;
                first <<= 1;
                c <<= 1;
            }
            throw new WebPException("a prefix code read past its longest code");
        }
    }

    /// <summary>The five codes of a prefix code group: green (with lengths and the cache), red, blue, alpha, distance.</summary>
    sealed record Group(PrefixCode Green, PrefixCode Red, PrefixCode Blue, PrefixCode Alpha, PrefixCode Dist);

    static readonly int[] CodeLengthOrder = [17, 18, 0, 1, 2, 3, 4, 5, 16, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

    /// <summary>The neighbourhood of the smallest distance codes (RFC 9649 §3.6.2.2.1): (xi, yi) for codes 1–120.</summary>
    static readonly sbyte[] DistanceMap =
    [
        0, 1, 1, 0, 1, 1, -1, 1, 0, 2, 2, 0, 1, 2, -1, 2, 2, 1, -2, 1, 2, 2, -2, 2, 0, 3, 3, 0, 1, 3, -1, 3, 3, 1, -3, 1, 2, 3, -2, 3,
        3, 2, -3, 2, 0, 4, 4, 0, 1, 4, -1, 4, 4, 1, -4, 1, 3, 3, -3, 3, 2, 4, -2, 4, 4, 2, -4, 2, 0, 5, 3, 4, -3, 4, 4, 3, -4, 3,
        5, 0, 1, 5, -1, 5, 5, 1, -5, 1, 2, 5, -2, 5, 5, 2, -5, 2, 4, 4, -4, 4, 3, 5, -3, 5, 5, 3, -5, 3, 0, 6, 6, 0, 1, 6, -1, 6,
        6, 1, -6, 1, 2, 6, -2, 6, 6, 2, -6, 2, 4, 5, -4, 5, 5, 4, -5, 4, 3, 6, -3, 6, 6, 3, -6, 3, 0, 7, 7, 0, 1, 7, -1, 7, 5, 5,
        -5, 5, 7, 1, -7, 1, 4, 6, -4, 6, 6, 4, -6, 4, 2, 7, -2, 7, 7, 2, -7, 2, 3, 7, -3, 7, 7, 3, -7, 3, 5, 6, -5, 6, 6, 5, -6, 5,
        8, 0, 4, 7, -4, 7, 7, 4, -7, 4, 8, 1, 8, 2, 6, 6, -6, 6, 8, 3, 5, 7, -5, 7, 7, 5, -7, 5, 8, 4, 6, 7, -6, 7, 7, 6, -7, 6,
        8, 5, 7, 7, -7, 7, 8, 6, 8, 7,
    ];

    abstract record Transform(int Width);
    sealed record PredictorT(int Width, int Bits, uint[] Modes) : Transform(Width);
    sealed record ColorT(int Width, int Bits, uint[] Elements) : Transform(Width);
    sealed record SubtractGreenT(int Width) : Transform(Width);
    sealed record IndexingT(int Width, int Bits, uint[] Palette) : Transform(Width);

    sealed class Decoder(BitReader br)
    {
        public Transforms Used;

        /// <summary>The main image: its transforms, its spatially coded pixels, the transforms undone last read first.</summary>
        public uint[] ImageStream(int width, int height)
        {
            var transforms = new List<Transform>();
            var xsize = width;
            var seen = 0;
            while (br.Read(1) == 1)
            {
                var type = (int)br.Read(2);
                if ((seen & 1 << type) != 0) throw new WebPException("a transform used twice");
                seen |= 1 << type;
                switch (type)
                {
                    case 0:
                    {
                        var bits = (int)br.Read(3) + 2;
                        transforms.Add(new PredictorT(xsize, bits, EntropyCoded(Div(xsize, bits), Div(height, bits))));
                        Used |= Transforms.Predictor;
                        break;
                    }
                    case 1:
                    {
                        var bits = (int)br.Read(3) + 2;
                        transforms.Add(new ColorT(xsize, bits, EntropyCoded(Div(xsize, bits), Div(height, bits))));
                        Used |= Transforms.Color;
                        break;
                    }
                    case 2:
                        transforms.Add(new SubtractGreenT(xsize));
                        Used |= Transforms.SubtractGreen;
                        break;
                    default:
                    {
                        var size = (int)br.Read(8) + 1;
                        var pal = EntropyCoded(size, 1);
                        // Subtraction-coded: each entry adds to the one before, channel by channel.
                        for (var i = 1; i < size; i++) pal[i] = AddPixels(pal[i], pal[i - 1]);
                        var bits = size <= 2 ? 3 : size <= 4 ? 2 : size <= 16 ? 1 : 0;
                        transforms.Add(new IndexingT(xsize, bits, pal));
                        xsize = Div(xsize, bits);
                        Used |= Transforms.ColorIndexing;
                        break;
                    }
                }
            }
            var data = SpatiallyCoded(xsize, height);
            for (var t = transforms.Count - 1; t >= 0; t--) data = Undo(transforms[t], data, height);
            return data;
        }

        static int Div(int n, int bits) => (n + (1 << bits) - 1) >> bits;

        uint[] EntropyCoded(int w, int h)
        {
            var cacheBits = CacheBits();
            return Pixels(w, h, cacheBits, ReadGroups(1, cacheBits), null, 0, 0);
        }

        uint[] SpatiallyCoded(int w, int h)
        {
            var cacheBits = CacheBits();
            uint[]? meta = null;
            var metaBits = 0;
            var metaW = 0;
            var groups = 1;
            if (br.Read(1) == 1)
            {
                metaBits = (int)br.Read(3) + 2;
                metaW = Div(w, metaBits);
                meta = EntropyCoded(metaW, Div(h, metaBits));
                for (var i = 0; i < meta.Length; i++) { meta[i] = meta[i] >> 8 & 0xffff; groups = Math.Max(groups, (int)meta[i] + 1); }
                Used |= Transforms.MetaPrefix;
            }
            return Pixels(w, h, cacheBits, ReadGroups(groups, cacheBits), meta, metaBits, metaW);
        }

        int CacheBits()
        {
            if (br.Read(1) == 0) return 0;
            var bits = (int)br.Read(4);
            if (bits is < 1 or > 11) throw new WebPException("a colour cache size outside 1–11 bits");
            Used |= Transforms.ColorCache;
            return bits;
        }

        Group[] ReadGroups(int n, int cacheBits)
        {
            var g = new Group[n];
            var green = 256 + 24 + (cacheBits > 0 ? 1 << cacheBits : 0);
            for (var i = 0; i < n; i++) g[i] = new Group(ReadCode(green), ReadCode(256), ReadCode(256), ReadCode(256), ReadCode(40));
            return g;
        }

        PrefixCode ReadCode(int alphabet)
        {
            var lengths = new int[alphabet];
            if (br.Read(1) == 1)
            {
                // Simple code length code: one or two symbols of length 1.
                var two = br.Read(1) == 1;
                var s0 = (int)br.Read(br.Read(1) == 1 ? 8 : 1);
                if (s0 >= alphabet) throw new WebPException("a simple prefix code's symbol outside its alphabet");
                lengths[s0] = 1;
                if (two)
                {
                    var s1 = (int)br.Read(8);
                    if (s1 >= alphabet) throw new WebPException("a simple prefix code's symbol outside its alphabet");
                    lengths[s1] = 1;
                }
                return new PrefixCode(lengths);
            }
            var cl = new int[19];
            var num = (int)br.Read(4) + 4;
            for (var i = 0; i < num; i++) cl[CodeLengthOrder[i]] = (int)br.Read(3);
            var clCode = new PrefixCode(cl);
            var max = alphabet;
            if (br.Read(1) == 1)
            {
                var nbits = 2 + 2 * (int)br.Read(3);
                max = 2 + (int)br.Read(nbits);
                if (max > alphabet) throw new WebPException("a prefix code with more symbols than its alphabet");
            }
            var prev = 8;
            var sym = 0;
            while (sym < alphabet)
            {
                if (max-- == 0) break;
                var c = clCode.Read(br);
                if (c < 16)
                {
                    lengths[sym++] = c;
                    if (c != 0) prev = c;
                    continue;
                }
                var (extra, offset, value) = c switch { 16 => (2, 3, prev), 17 => (3, 3, 0), _ => (7, 11, 0) };
                var repeat = offset + (int)br.Read(extra);
                if (sym + repeat > alphabet) throw new WebPException("a prefix code's lengths run past its alphabet");
                for (var k = 0; k < repeat; k++) lengths[sym++] = value;
            }
            return new PrefixCode(lengths);
        }

        static int LengthOrDistance(int prefix, BitReader br)
        {
            if (prefix < 4) return prefix + 1;
            var extra = (prefix - 2) >> 1;
            var offset = (2 + (prefix & 1)) << extra;
            return offset + (int)br.Read(extra) + 1;
        }

        /// <summary>LZ77-coded pixels (§3.7.2.3): literals, backward references, colour cache hits.</summary>
        uint[] Pixels(int w, int h, int cacheBits, Group[] groups, uint[]? meta, int metaBits, int metaW)
        {
            var total = w * h;
            var o = new uint[total];
            var cache = cacheBits > 0 ? new uint[1 << cacheBits] : null;
            var shift = 32 - cacheBits;
            var cached = 0;   // pixels before this one are in the cache
            var pos = 0;
            var mask = metaBits > 0 ? (1 << metaBits) - 1 : -1;
            var g = groups[0];
            while (pos < total)
            {
                var x = pos % w;
                if (meta != null && (x & mask) == 0) g = groups[meta[(pos / w >> metaBits) * metaW + (x >> metaBits)]];
                var s = g.Green.Read(br);
                if (s < 256)
                {
                    var r = g.Red.Read(br);
                    var b = g.Blue.Read(br);
                    var a = g.Alpha.Read(br);
                    o[pos++] = (uint)(a << 24 | r << 16 | s << 8 | b);
                }
                else if (s < 256 + 24)
                {
                    var length = LengthOrDistance(s - 256, br);
                    var code = LengthOrDistance(g.Dist.Read(br), br);
                    int dist;
                    if (code > 120) dist = code - 120;
                    else
                    {
                        dist = DistanceMap[2 * (code - 1)] + DistanceMap[2 * (code - 1) + 1] * w;
                        if (dist < 1) dist = 1;
                    }
                    if (dist > pos || length > total - pos) throw new WebPException("a backward reference outside the image");
                    for (var k = 0; k < length; k++, pos++) o[pos] = o[pos - dist];
                    Used |= Transforms.Backrefs;
                    // The prefix group may change inside the copy: the next symbol picks it again.
                    if (meta != null && pos < total) g = groups[meta[(pos / w >> metaBits) * metaW + (pos % w >> metaBits)]];
                }
                else
                {
                    if (cache == null) throw new WebPException("a colour cache code without a colour cache");
                    while (cached < pos) cache[(0x1e35a7bd * o[cached]) >> shift] = o[cached++];
                    var idx = s - 280;
                    if (idx >= cache.Length) throw new WebPException("a colour cache code outside the cache");
                    o[pos++] = cache[idx];
                }
                if (cache != null) while (cached < pos) cache[(0x1e35a7bd * o[cached]) >> shift] = o[cached++];
                if (br.Overrun) throw new WebPException("its image data is cut short");
            }
            return o;
        }

        static uint AddPixels(uint a, uint b) =>
            ((a & 0xff00ff00u) + (b & 0xff00ff00u)) & 0xff00ff00u | ((a & 0x00ff00ffu) + (b & 0x00ff00ffu)) & 0x00ff00ffu;

        static uint Average2(uint a, uint b) => (((a ^ b) & 0xfefefefeu) >> 1) + (a & b);

        static int Ch(uint p, int s) => (int)(p >> s) & 0xff;

        static uint Select(uint l, uint t, uint tl)
        {
            var pl = 0;
            var pt = 0;
            for (var s = 0; s < 32; s += 8)
            {
                var p = Ch(l, s) + Ch(t, s) - Ch(tl, s);
                pl += Math.Abs(p - Ch(l, s));
                pt += Math.Abs(p - Ch(t, s));
            }
            return pl < pt ? l : t;
        }

        static uint ClampFull(uint a, uint b, uint c)
        {
            uint o = 0;
            for (var s = 0; s < 32; s += 8) o |= (uint)Math.Clamp(Ch(a, s) + Ch(b, s) - Ch(c, s), 0, 255) << s;
            return o;
        }

        static uint ClampHalf(uint a, uint b)
        {
            uint o = 0;
            for (var s = 0; s < 32; s += 8) o |= (uint)Math.Clamp(Ch(a, s) + (Ch(a, s) - Ch(b, s)) / 2, 0, 255) << s;
            return o;
        }

        static uint Predict(int mode, uint l, uint t, uint tr, uint tl) => mode switch
        {
            0 => 0xff000000u,
            1 => l,
            2 => t,
            3 => tr,
            4 => tl,
            5 => Average2(Average2(l, tr), t),
            6 => Average2(l, tl),
            7 => Average2(l, t),
            8 => Average2(tl, t),
            9 => Average2(t, tr),
            10 => Average2(Average2(l, tl), Average2(t, tr)),
            11 => Select(l, t, tl),
            12 => ClampFull(l, t, tl),
            13 => ClampHalf(Average2(l, t), tl),
            _ => 0xff000000u, // modes 14 and 15 are not in the format; libwebp treats them as 0.
        };

        static int Delta(uint t, int c) => ((sbyte)(byte)t * (sbyte)(byte)c) >> 5;

        static uint[] Undo(Transform t, uint[] p, int height)
        {
            var w = t.Width;
            switch (t)
            {
                case SubtractGreenT:
                    for (var i = 0; i < p.Length; i++)
                    {
                        var g = p[i] >> 8 & 0xff;
                        p[i] = p[i] & 0xff00ff00u | ((p[i] & 0x00ff00ffu) + (g << 16 | g)) & 0x00ff00ffu;
                    }
                    return p;
                case ColorT c:
                {
                    var bw = Div(w, c.Bits);
                    for (var y = 0; y < height; y++)
                        for (var x = 0; x < w; x++)
                        {
                            var e = c.Elements[(y >> c.Bits) * bw + (x >> c.Bits)];
                            uint g2r = e & 0xff, g2b = e >> 8 & 0xff, r2b = e >> 16 & 0xff;
                            var i = y * w + x;
                            var px = p[i];
                            var green = (int)(px >> 8 & 0xff);
                            var red = (int)(px >> 16 & 0xff) + Delta(g2r, green);
                            var blue = (int)(px & 0xff) + Delta(g2b, green) + Delta(r2b, red & 0xff);
                            p[i] = px & 0xff00ff00u | (uint)(red & 0xff) << 16 | (uint)(blue & 0xff);
                        }
                    return p;
                }
                case PredictorT pr:
                {
                    var bw = Div(w, pr.Bits);
                    for (var y = 0; y < height; y++)
                        for (var x = 0; x < w; x++)
                        {
                            var i = y * w + x;
                            uint pred;
                            if (y == 0) pred = x == 0 ? 0xff000000u : p[i - 1];
                            else if (x == 0) pred = p[i - w];
                            else
                            {
                                var mode = (int)(pr.Modes[(y >> pr.Bits) * bw + (x >> pr.Bits)] >> 8 & 0xf);
                                // On the rightmost column the top-right is the row's first pixel: the next in memory, as the format says.
                                pred = Predict(mode, p[i - 1], p[i - w], p[i - w + 1], p[i - w - 1]);
                            }
                            p[i] = AddPixels(p[i], pred);
                        }
                    return p;
                }
                case IndexingT ix:
                {
                    // The packed width came in; the image goes out at the width before the transform.
                    var packed = Div(w, ix.Bits);
                    var o = new uint[w * height];
                    var per = 1 << ix.Bits;
                    var bitsPer = 8 >> ix.Bits;
                    var m = (1 << bitsPer) - 1;
                    for (var y = 0; y < height; y++)
                        for (var x = 0; x < w; x++)
                        {
                            var g = (int)(p[y * packed + x / per] >> 8 & 0xff);
                            var idx = ix.Bits == 0 ? g : g >> (x % per * bitsPer) & m;
                            o[y * w + x] = idx < ix.Palette.Length ? ix.Palette[idx] : 0;
                        }
                    return o;
                }
            }
            return p;
        }
    }
}
