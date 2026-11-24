using System.Buffers.Binary;
using System.Numerics;

namespace Lapin.PointClouds;

/// <summary>
/// Zstandard decompression, Lapin's own after RFC 8878, managed only: frames one after the other (skippable frames stepped
/// over), raw, RLE and compressed blocks; literals raw, RLE or Huffman coded (a tree by direct or FSE-coded weights, or the
/// previous one; one or four streams); sequences with their FSE tables (predefined, RLE, described or repeated) and the three
/// repeat offsets. No dictionaries (SPZ uses none): a frame that names one is refused. The content checksum is not checked.
/// </summary>
public static class Zstd
{
    public sealed class ZstdException(string message) : Exception(message);

    const uint FrameMagic = 0xFD2FB528;

    /// <summary>What a decompression met (for the tests: each part of the format is covered).</summary>
    [Flags]
    public enum Features
    {
        None = 0, RawBlock = 1, RleBlock = 2, CompressedBlock = 4, RawLiterals = 8, RleLiterals = 16, HuffmanLiterals = 32, TreelessLiterals = 64,
        FourStreams = 128, DirectWeights = 256, FseWeights = 512, PredefinedTable = 1024, RleTable = 2048, FseTable = 4096, RepeatTable = 8192,
        RepeatOffset = 16384, Skippable = 32768, Checksum = 65536,
    }

    /// <summary>Every frame of <paramref name="src"/> decompressed, one after the other; <paramref name="expected"/>, when known, sizes the output.</summary>
    public static byte[] Decompress(ReadOnlySpan<byte> src, int expected = -1) => Decompress(src, out _, expected);

    public static byte[] Decompress(ReadOnlySpan<byte> src, out Features used, int expected = -1)
    {
        var o = new Output(expected > 0 ? expected : Math.Max(1024, src.Length * 4));
        var at = 0;
        while (at < src.Length)
        {
            if (src.Length - at < 4) throw new ZstdException("trailing bytes after the last frame");
            var magic = BinaryPrimitives.ReadUInt32LittleEndian(src[at..]);
            if ((magic & 0xFFFFFFF0) == 0x184D2A50)
            {
                if (src.Length - at < 8) throw new ZstdException("a skippable frame cut short");
                var size = BinaryPrimitives.ReadUInt32LittleEndian(src[(at + 4)..]);
                if (size > src.Length - at - 8) throw new ZstdException("a skippable frame cut short");
                at += 8 + (int)size;
                o.Used |= Features.Skippable;
                continue;
            }
            if (magic != FrameMagic) throw new ZstdException("not a Zstandard frame (its magic number is wrong)");
            at = Frame(src, at + 4, o);
        }
        used = o.Used;
        return o.ToArray();
    }

    /// <summary>The decompressed bytes, growing as needed; matches refer back into them.</summary>
    sealed class Output(int capacity)
    {
        public byte[] Buf = new byte[capacity];
        public int Length;
        public Features Used;

        public void Ensure(int more)
        {
            if (Length + more <= Buf.Length) return;
            var n = Math.Max(Buf.Length * 2L, (long)Length + more);
            if (n > Array.MaxLength) throw new ZstdException("the decompressed data is too large");
            Array.Resize(ref Buf, (int)n);
        }

        public byte[] ToArray() => Length == Buf.Length ? Buf : Buf.AsSpan(0, Length).ToArray();
    }

    /// <summary>What a frame carries from block to block: the repeat offsets, the last Huffman table, the last sequence tables.</summary>
    sealed class FrameState
    {
        public readonly int[] Rep = [1, 4, 8];
        public Huffman? Huf;
        public Fse? LL, OF, ML;
        public int FrameStart;
        public long Window;
    }

    static int Frame(ReadOnlySpan<byte> src, int at, Output o)
    {
        if (at >= src.Length) throw new ZstdException("a frame header cut short");
        var fhd = src[at++];
        var fcsFlag = fhd >> 6;
        var single = (fhd >> 5 & 1) == 1;
        if ((fhd >> 3 & 1) != 0) throw new ZstdException("a frame header with its reserved bit set");
        var checksum = (fhd >> 2 & 1) == 1;
        var didFlag = fhd & 3;
        long window = 0;
        if (!single)
        {
            var wd = src[at++];
            var log = 10 + (wd >> 3);
            window = (1L << log) + ((1L << log) / 8) * (wd & 7);
        }
        var didSize = didFlag switch { 0 => 0, 1 => 1, 2 => 2, _ => 4 };
        if (didSize > 0)
        {
            uint did = 0;
            for (var i = 0; i < didSize; i++) did |= (uint)src[at + i] << (8 * i);
            at += didSize;
            if (did != 0) throw new ZstdException("a frame that needs a dictionary, which Lapin does not have");
        }
        var fcsSize = fcsFlag switch { 0 => single ? 1 : 0, 1 => 2, 2 => 4, _ => 8 };
        long fcs = -1;
        if (fcsSize > 0)
        {
            ulong v = 0;
            for (var i = 0; i < fcsSize; i++) v |= (ulong)src[at + i] << (8 * i);
            at += fcsSize;
            fcs = (long)(fcsSize == 2 ? v + 256 : v);
            if (fcs < 0 || fcs > Array.MaxLength) throw new ZstdException("a frame larger than Lapin decompresses at once");
            o.Ensure((int)fcs);
        }
        if (single) window = fcs;
        var st = new FrameState { FrameStart = o.Length, Window = window };
        var blockMax = (int)Math.Min(128 * 1024, window > 0 ? window : 128 * 1024);
        while (true)
        {
            if (src.Length - at < 3) throw new ZstdException("a block header cut short");
            var bh = src[at] | src[at + 1] << 8 | src[at + 2] << 16;
            at += 3;
            var last = (bh & 1) == 1;
            var type = bh >> 1 & 3;
            var size = bh >> 3;
            switch (type)
            {
                case 0:
                    if (size > src.Length - at) throw new ZstdException("a raw block cut short");
                    o.Ensure(size);
                    src.Slice(at, size).CopyTo(o.Buf.AsSpan(o.Length));
                    o.Length += size;
                    at += size;
                    o.Used |= Features.RawBlock;
                    break;
                case 1:
                    if (at >= src.Length) throw new ZstdException("an RLE block cut short");
                    o.Ensure(size);
                    o.Buf.AsSpan(o.Length, size).Fill(src[at]);
                    o.Length += size;
                    at += 1;
                    o.Used |= Features.RleBlock;
                    break;
                case 2:
                    if (size > src.Length - at) throw new ZstdException("a compressed block cut short");
                    if (size > blockMax) throw new ZstdException("a block larger than the frame allows");
                    Block(src.Slice(at, size), o, st);
                    o.Used |= Features.CompressedBlock;
                    at += size;
                    break;
                default: throw new ZstdException("a block of the reserved type");
            }
            if (last) break;
        }
        if (checksum) { at += 4; o.Used |= Features.Checksum; }
        if (fcs >= 0 && o.Length - st.FrameStart != fcs) throw new ZstdException("a frame whose content is not the size its header says");
        return at;
    }

    // ─── Compressed blocks ───

    static void Block(ReadOnlySpan<byte> b, Output o, FrameState st)
    {
        var (literals, used) = Literals(b, st, o);
        Sequences(b[used..], literals, o, st);
    }

    static (byte[] Literals, int Used) Literals(ReadOnlySpan<byte> b, FrameState st, Output o)
    {
        if (b.Length < 1) throw new ZstdException("a literals section cut short");
        var type = b[0] & 3;
        var sf = b[0] >> 2 & 3;
        if (type is 0 or 1)
        {
            int regen, head;
            switch (sf)
            {
                case 0 or 2: regen = b[0] >> 3; head = 1; break;
                case 1: regen = (b[0] >> 4) + (b[1] << 4); head = 2; break;
                default: regen = (b[0] >> 4) + (b[1] << 4) + (b[2] << 12); head = 3; break;
            }
            o.Used |= type == 0 ? Features.RawLiterals : Features.RleLiterals;
            if (type == 0)
            {
                if (head + regen > b.Length) throw new ZstdException("raw literals cut short");
                return (b.Slice(head, regen).ToArray(), head + regen);
            }
            if (head >= b.Length) throw new ZstdException("RLE literals cut short");
            var rle = new byte[regen];
            Array.Fill(rle, b[head]);
            return (rle, head + 1);
        }
        int regenSize, compSize, hsize;
        var streams = sf == 0 ? 1 : 4;
        switch (sf)
        {
            case 0 or 1:
            {
                var h = b[0] | b[1] << 8 | b[2] << 16;
                regenSize = h >> 4 & 0x3FF; compSize = h >> 14 & 0x3FF; hsize = 3;
                break;
            }
            case 2:
            {
                var h = (long)BinaryPrimitives.ReadUInt32LittleEndian(b);
                regenSize = (int)(h >> 4 & 0x3FFF); compSize = (int)(h >> 18 & 0x3FFF); hsize = 4;
                break;
            }
            default:
            {
                var h = (long)BinaryPrimitives.ReadUInt32LittleEndian(b) | (long)b[4] << 32;
                regenSize = (int)(h >> 4 & 0x3FFFF); compSize = (int)(h >> 22 & 0x3FFFF); hsize = 5;
                break;
            }
        }
        if (hsize + compSize > b.Length) throw new ZstdException("compressed literals cut short");
        var data = b.Slice(hsize, compSize);
        var at = 0;
        o.Used |= (type == 2 ? Features.HuffmanLiterals : Features.TreelessLiterals) | (streams == 4 ? Features.FourStreams : 0);
        if (type == 2) { st.Huf = Huffman.Read(data, out var tree, out var direct); at = tree; o.Used |= direct ? Features.DirectWeights : Features.FseWeights; }
        else if (st.Huf == null) throw new ZstdException("treeless literals without a previous Huffman table");
        var huf = st.Huf;
        var lit = new byte[regenSize];
        if (streams == 1) huf.DecodeStream(data[at..], lit);
        else
        {
            if (data.Length - at < 6) throw new ZstdException("a literals jump table cut short");
            int s1 = BinaryPrimitives.ReadUInt16LittleEndian(data[at..]), s2 = BinaryPrimitives.ReadUInt16LittleEndian(data[(at + 2)..]), s3 = BinaryPrimitives.ReadUInt16LittleEndian(data[(at + 4)..]);
            var rest = data[(at + 6)..];
            var s4 = rest.Length - s1 - s2 - s3;
            if (s4 < 0) throw new ZstdException("a literals jump table larger than its streams");
            var seg = (regenSize + 3) / 4;
            if (seg * 3 > regenSize) throw new ZstdException("four literal streams for too few literals");
            huf.DecodeStream(rest[..s1], lit.AsSpan(0, seg));
            huf.DecodeStream(rest.Slice(s1, s2), lit.AsSpan(seg, seg));
            huf.DecodeStream(rest.Slice(s1 + s2, s3), lit.AsSpan(2 * seg, seg));
            huf.DecodeStream(rest.Slice(s1 + s2 + s3, s4), lit.AsSpan(3 * seg));
        }
        return (lit, hsize + compSize);
    }

    static readonly int[] LLBase = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24, 28, 32, 40, 48, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536];
    static readonly int[] LLBits = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 3, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
    static readonly int[] MLBase =
    [
        3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34,
        35, 37, 39, 41, 43, 47, 51, 59, 67, 83, 99, 131, 259, 515, 1027, 2051, 4099, 8195, 16387, 32771, 65539,
    ];
    static readonly int[] MLBits =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 2, 2, 3, 3, 4, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
    ];
    static readonly short[] LLDefault = [4, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 2, 1, 1, 1, 1, 1, -1, -1, -1, -1];
    static readonly short[] MLDefault =
    [
        1, 4, 3, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1, -1, -1,
    ];
    static readonly short[] OFDefault = [1, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1];
    static readonly Fse LLPredef = Fse.Build(LLDefault, 6), MLPredef = Fse.Build(MLDefault, 6), OFPredef = Fse.Build(OFDefault, 5);

    static void Sequences(ReadOnlySpan<byte> b, byte[] lit, Output o, FrameState st)
    {
        if (b.Length < 1) throw new ZstdException("a sequences section cut short");
        int n, at;
        if (b[0] < 128) { n = b[0]; at = 1; }
        else if (b[0] < 255) { n = ((b[0] - 128) << 8) + b[1]; at = 2; }
        else { n = b[1] + (b[2] << 8) + 0x7F00; at = 3; }
        if (n == 0)
        {
            Append(o, lit);
            return;
        }
        var modes = b[at++];
        if ((modes & 3) != 0) throw new ZstdException("a sequences header with its reserved bits set");
        foreach (var m in new[] { modes >> 6 & 3, modes >> 4 & 3, modes >> 2 & 3 })
            o.Used |= m switch { 0 => Features.PredefinedTable, 1 => Features.RleTable, 2 => Features.FseTable, _ => Features.RepeatTable };
        st.LL = Table(modes >> 6 & 3, b, ref at, LLPredef, st.LL, 35, 9);
        st.OF = Table(modes >> 4 & 3, b, ref at, OFPredef, st.OF, 31, 8);
        st.ML = Table(modes >> 2 & 3, b, ref at, MLPredef, st.ML, 52, 9);
        var bits = new BackBits(b[at..]);
        int sLL = (int)bits.Read(st.LL.Log), sOF = (int)bits.Read(st.OF.Log), sML = (int)bits.Read(st.ML.Log);
        var litAt = 0;
        var rep = st.Rep;
        for (var i = 0; i < n; i++)
        {
            int llCode = st.LL.Symbol[sLL], ofCode = st.OF.Symbol[sOF], mlCode = st.ML.Symbol[sML];
            if (ofCode > 31) throw new ZstdException("an offset code above 31");
            var ofValue = (1L << ofCode) + (long)bits.Read(ofCode);
            var ml = MLBase[mlCode] + (int)bits.Read(MLBits[mlCode]);
            var ll = LLBase[llCode] + (int)bits.Read(LLBits[llCode]);
            long offset;
            if (ofValue > 3)
            {
                offset = ofValue - 3;
                rep[2] = rep[1]; rep[1] = rep[0]; rep[0] = (int)offset;
            }
            else
            {
                // Repeat offsets (§3.1.1.5); with no literals they shift by one, and 3 is the first less a byte.
                o.Used |= Features.RepeatOffset;
                var idx = (int)ofValue - (ll == 0 ? 0 : 1);
                if (idx == 0) offset = rep[0];
                else
                {
                    offset = idx == 3 ? rep[0] - 1 : rep[idx];
                    if (offset == 0) offset = 1; // corrupt input, as the reference decoder takes it
                    if (idx == 1) { rep[1] = rep[0]; rep[0] = (int)offset; }
                    else { rep[2] = rep[1]; rep[1] = rep[0]; rep[0] = (int)offset; }
                }
            }
            if (i < n - 1)
            {
                sLL = st.LL.Next(sLL, ref bits);
                sML = st.ML.Next(sML, ref bits);
                sOF = st.OF.Next(sOF, ref bits);
            }
            // Execution: the literals, then the match from the output.
            if (ll > lit.Length - litAt) throw new ZstdException("a sequence takes more literals than there are");
            o.Ensure(ll + ml);
            lit.AsSpan(litAt, ll).CopyTo(o.Buf.AsSpan(o.Length));
            o.Length += ll;
            litAt += ll;
            if (offset <= 0 || offset > o.Length - st.FrameStart) throw new ZstdException("a match before the frame's start");
            var from = o.Length - (int)offset;
            if (offset >= ml) o.Buf.AsSpan(from, ml).CopyTo(o.Buf.AsSpan(o.Length));
            else for (var k = 0; k < ml; k++) o.Buf[o.Length + k] = o.Buf[from + k];
            o.Length += ml;
        }
        if (!bits.Finished) throw new ZstdException("a sequences bitstream not consumed exactly");
        Append(o, lit.AsSpan(litAt));
    }

    static void Append(Output o, ReadOnlySpan<byte> s)
    {
        o.Ensure(s.Length);
        s.CopyTo(o.Buf.AsSpan(o.Length));
        o.Length += s.Length;
    }

    static Fse Table(int mode, ReadOnlySpan<byte> b, ref int at, Fse predefined, Fse? previous, int maxSymbol, int maxLog)
    {
        switch (mode)
        {
            case 0: return predefined;
            case 1:
                if (at >= b.Length) throw new ZstdException("an RLE sequence table cut short");
                return Fse.Rle(b[at++]);
            case 2:
            {
                var (norm, log, used) = Fse.ReadNCount(b[at..], maxSymbol, maxLog);
                at += used;
                return Fse.Build(norm, log);
            }
            default: return previous ?? throw new ZstdException("a repeated sequence table with none before it");
        }
    }

    // ─── Bitstreams read backwards (§3.1.1.3.2.1.2, §4.2.2) ───

    /// <summary>
    /// A bitstream written forwards and read from its end: past its last 1 bit (the padding's mark), each read takes the bits
    /// just below the position as a little-endian number; bits before the stream's start read as zeros (an overflow).
    /// </summary>
    ref struct BackBits
    {
        readonly ReadOnlySpan<byte> _s;
        long _pos;

        public BackBits(ReadOnlySpan<byte> s)
        {
            _s = s;
            if (s.Length == 0 || s[^1] == 0) throw new ZstdException("a bitstream without its end mark");
            _pos = (long)(s.Length - 1) * 8 + (31 - BitOperations.LeadingZeroCount((uint)s[^1]));
        }

        /// <summary>Bits left before the start (negative: read past it).</summary>
        public readonly long Left => _pos;
        public readonly bool Finished => _pos == 0;
        public readonly bool Overflow => _pos < 0;

        /// <summary>The 64 bits below <paramref name="p"/> (stream bits [p − 64, p)), those before the start zeros.</summary>
        readonly ulong Window(long p)
        {
            if (p <= 0) return 0;
            var lo = p - 64;
            if (lo >= 0)
            {
                var i = (int)(lo >> 3);
                var sh = (int)(lo & 7);
                var v = BinaryPrimitives.ReadUInt64LittleEndian(_s[i..]) >> sh;
                if (sh > 0) v |= (ulong)_s[i + 8] << (64 - sh);
                return v;
            }
            // Fewer than 64 bits below: the stream's first bytes, shifted to the top.
            ulong w = 0;
            var bytes = (int)((p + 7) >> 3);
            for (var k = 0; k < bytes; k++) w |= (ulong)_s[k] << (8 * k);
            if (p < 64) w &= (1UL << (int)p) - 1;
            return w << (int)(64 - p);
        }

        public ulong Peek(int n) => n == 0 ? 0 : Window(_pos) >> (64 - n);
        public void Skip(int n) => _pos -= n;
        public ulong Read(int n)
        {
            var v = Peek(n);
            _pos -= n;
            return v;
        }
    }

    // ─── FSE (§4.1) ───

    sealed class Fse
    {
        public int Log;
        public byte[] Symbol = [];
        public byte[] Bits = [];
        public ushort[] Base = [];

        public int Next(int state, ref BackBits bits) => Base[state] + (int)bits.Read(Bits[state]);

        public static Fse Rle(byte symbol) => new() { Log = 0, Symbol = [symbol], Bits = [0], Base = [0] };

        /// <summary>The decoding table of a normalized distribution (−1: less than one, a cell at the table's end).</summary>
        public static Fse Build(short[] norm, int log)
        {
            var size = 1 << log;
            var t = new Fse { Log = log, Symbol = new byte[size], Bits = new byte[size], Base = new ushort[size] };
            var high = size - 1;
            var next = new int[norm.Length];
            for (var s = 0; s < norm.Length; s++)
                if (norm[s] == -1) { t.Symbol[high--] = (byte)s; next[s] = 1; }
                else next[s] = norm[s];
            var pos = 0;
            var step = (size >> 1) + (size >> 3) + 3;
            var mask = size - 1;
            for (var s = 0; s < norm.Length; s++)
                for (var i = 0; i < norm[s]; i++)
                {
                    t.Symbol[pos] = (byte)s;
                    do pos = (pos + step) & mask; while (pos > high);
                }
            if (pos != 0) throw new ZstdException("an FSE distribution that does not fill its table");
            for (var u = 0; u < size; u++)
            {
                var s = t.Symbol[u];
                var x = next[s]++;
                var nb = log - (31 - BitOperations.LeadingZeroCount((uint)x));
                t.Bits[u] = (byte)nb;
                t.Base[u] = (ushort)((x << nb) - size);
            }
            return t;
        }

        /// <summary>A distribution as the format describes it (§4.1.1), read forwards: its counts, its accuracy, the bytes it took.</summary>
        public static (short[] Norm, int Log, int Used) ReadNCount(ReadOnlySpan<byte> b, int maxSymbol, int maxLog)
        {
            if (b.Length < 1) throw new ZstdException("an FSE table description cut short");
            long bitPos = 0;
            var bytes = b.ToArray();
            uint Bits(int n)
            {
                uint v = 0;
                for (var k = 0; k < n; k++)
                {
                    var p = bitPos + k;
                    var by = p >> 3;
                    if (by < bytes.Length) v |= (uint)(bytes[(int)by] >> (int)(p & 7) & 1) << k;
                }
                return v;
            }
            var log = (int)Bits(4) + 5;
            bitPos = 4;
            if (log > maxLog) throw new ZstdException("an FSE table finer than the format allows");
            var norm = new List<short>();
            var remaining = (1 << log) + 1;
            var threshold = 1 << log;
            var nbBits = log + 1;
            var previous0 = false;
            while (remaining > 1)
            {
                if (previous0)
                {
                    // Zeros' repeat flags: 2 bits each, a 3 asks for another.
                    while (true)
                    {
                        var r = (int)Bits(2);
                        bitPos += 2;
                        for (var k = 0; k < r; k++) norm.Add(0);
                        if (r != 3) break;
                    }
                    if (norm.Count > maxSymbol + 1) throw new ZstdException("an FSE table description with too many symbols");
                }
                var max = 2 * threshold - 1 - remaining;
                int count;
                var low = (int)Bits(nbBits - 1);
                if (low < max) { count = low; bitPos += nbBits - 1; }
                else
                {
                    count = (int)Bits(nbBits);
                    if (count >= threshold) count -= max;
                    bitPos += nbBits;
                }
                count--;
                remaining -= count < 0 ? -count : count;
                norm.Add((short)count);
                if (norm.Count > maxSymbol + 1) throw new ZstdException("an FSE table description with too many symbols");
                previous0 = count == 0;
                while (remaining < threshold) { nbBits--; threshold >>= 1; }
            }
            if (remaining != 1) throw new ZstdException("an FSE table description whose counts do not add up");
            var used = (int)((bitPos + 7) >> 3);
            if (used > b.Length) throw new ZstdException("an FSE table description cut short");
            return ([.. norm], log, used);
        }
    }

    // ─── Huffman (§4.2) ───

    sealed class Huffman
    {
        int _maxBits;
        byte[] _sym = [];
        byte[] _len = [];

        /// <summary>A tree description (§4.2.1): its weights, direct or FSE coded, the last one worked out.</summary>
        public static Huffman Read(ReadOnlySpan<byte> b, out int used, out bool direct)
        {
            if (b.Length < 1) throw new ZstdException("a Huffman tree description cut short");
            var head = b[0];
            var weights = new List<byte>();
            direct = head >= 128;
            if (head >= 128)
            {
                var n = head - 127;
                var bytes = (n + 1) / 2;
                if (1 + bytes > b.Length) throw new ZstdException("a Huffman tree description cut short");
                for (var i = 0; i < n; i++) weights.Add((byte)(i % 2 == 0 ? b[1 + i / 2] >> 4 : b[1 + i / 2] & 15));
                used = 1 + bytes;
            }
            else
            {
                if (1 + head > b.Length) throw new ZstdException("a Huffman tree description cut short");
                var s = b.Slice(1, head);
                var (norm, log, nc) = Fse.ReadNCount(s, 255, 6);
                var t = Fse.Build(norm, log);
                var bits = new BackBits(s[nc..]);
                int s1 = (int)bits.Read(log), s2 = (int)bits.Read(log);
                // Two states take turns; when a state's update reads past the start, the other's symbol is the last.
                while (true)
                {
                    if (weights.Count > 254) throw new ZstdException("too many Huffman weights");
                    weights.Add(t.Symbol[s1]);
                    s1 = t.Next(s1, ref bits);
                    if (bits.Overflow) { weights.Add(t.Symbol[s2]); break; }
                    if (weights.Count > 254) throw new ZstdException("too many Huffman weights");
                    weights.Add(t.Symbol[s2]);
                    s2 = t.Next(s2, ref bits);
                    if (bits.Overflow) { weights.Add(t.Symbol[s1]); break; }
                }
                used = 1 + head;
            }
            return FromWeights(weights);
        }

        static Huffman FromWeights(List<byte> weights)
        {
            long sum = 0;
            foreach (var w in weights)
            {
                if (w > 11) throw new ZstdException("a Huffman weight above 11");
                if (w > 0) sum += 1L << (w - 1);
            }
            if (sum == 0) throw new ZstdException("a Huffman tree without symbols");
            var maxBits = 64 - BitOperations.LeadingZeroCount((ulong)sum);  // highbit(sum) + 1
            var rest = (1L << maxBits) - sum;
            if (rest == 0 || (rest & (rest - 1)) != 0) throw new ZstdException("Huffman weights that do not complete a tree");
            weights.Add((byte)(BitOperations.Log2((ulong)rest) + 1));
            if (maxBits > 11) throw new ZstdException("a Huffman code longer than 11 bits");
            var h = new Huffman { _maxBits = maxBits, _sym = new byte[1 << maxBits], _len = new byte[1 << maxBits] };
            var pos = 0;
            for (var w = 1; w <= maxBits; w++)
                for (var s = 0; s < weights.Count; s++)
                {
                    if (weights[s] != w) continue;
                    var entries = 1 << (w - 1);
                    var len = (byte)(maxBits + 1 - w);
                    h._sym.AsSpan(pos, entries).Fill((byte)s);
                    h._len.AsSpan(pos, entries).Fill(len);
                    pos += entries;
                }
            if (pos != 1 << maxBits) throw new ZstdException("Huffman weights that do not fill their table");
            return h;
        }

        public void DecodeStream(ReadOnlySpan<byte> s, Span<byte> o)
        {
            var bits = new BackBits(s);
            for (var i = 0; i < o.Length; i++)
            {
                var v = (int)bits.Peek(_maxBits);
                o[i] = _sym[v];
                bits.Skip(_len[v]);
            }
            if (!bits.Finished) throw new ZstdException("a Huffman stream not consumed exactly");
        }
    }
}
