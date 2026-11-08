// LAZ's point items, ported to C# from laz-rs 0.9.3 (Thomas Montaigu, Apache License 2.0), a port of LASzip's
// LASreadItemCompressed_*_v2 and _v3 (Martin Isenburg, rapidlasso). See Laz/NOTICE.md. Decoding only, and of the
// layered (v3) items only the layers Lapin draws with: positions, intensity, classification and colour.

using System.Buffers.Binary;

namespace Lapin.PointClouds.Laz;

static class ReturnTables
{
    public static readonly byte[,] Map = {
        { 15, 14, 13, 12, 11, 10, 9, 8 }, { 14, 0, 1, 3, 6, 10, 10, 9 }, { 13, 1, 2, 4, 7, 11, 11, 10 }, { 12, 3, 4, 5, 8, 12, 12, 11 },
        { 11, 6, 7, 8, 9, 13, 13, 12 }, { 10, 10, 11, 12, 13, 14, 14, 13 }, { 9, 10, 11, 12, 13, 14, 15, 14 }, { 8, 9, 10, 11, 12, 13, 14, 15 },
    };
    public static readonly byte[,] Level = {
        { 0, 1, 2, 3, 4, 5, 6, 7 }, { 1, 0, 1, 2, 3, 4, 5, 6 }, { 2, 1, 0, 1, 2, 3, 4, 5 }, { 3, 2, 1, 0, 1, 2, 3, 4 },
        { 4, 3, 2, 1, 0, 1, 2, 3 }, { 5, 4, 3, 2, 1, 0, 1, 2 }, { 6, 5, 4, 3, 2, 1, 0, 1 }, { 7, 6, 5, 4, 3, 2, 1, 0 },
    };
    public static readonly byte[,] Map6 = {
        { 0, 1, 2, 3, 4, 5, 3, 4, 4, 5, 5, 5, 5, 5, 5, 5 }, { 1, 0, 1, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 },
        { 2, 1, 2, 4, 4, 4, 4, 4, 4, 4, 4, 3, 3, 3, 3, 3 }, { 3, 3, 4, 5, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        { 4, 3, 4, 4, 5, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 }, { 5, 3, 4, 4, 4, 5, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        { 3, 3, 4, 4, 4, 4, 5, 4, 4, 4, 4, 4, 4, 4, 4, 4 }, { 4, 3, 4, 4, 4, 4, 4, 5, 4, 4, 4, 4, 4, 4, 4, 4 },
        { 4, 3, 4, 4, 4, 4, 4, 4, 5, 4, 4, 4, 4, 4, 4, 4 }, { 5, 3, 4, 4, 4, 4, 4, 4, 4, 5, 4, 4, 4, 4, 4, 4 },
        { 5, 3, 4, 4, 4, 4, 4, 4, 4, 4, 5, 4, 4, 4, 4, 4 }, { 5, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 4, 4, 4 },
        { 5, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 4, 4 }, { 5, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 4 },
        { 5, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5 }, { 5, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5 },
    };
    public static readonly byte[,] Level8 = {
        { 0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7 }, { 1, 0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 7, 7, 7, 7, 7 },
        { 2, 1, 0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 7, 7, 7, 7 }, { 3, 2, 1, 0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 7, 7, 7 },
        { 4, 3, 2, 1, 0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 7, 7 }, { 5, 4, 3, 2, 1, 0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 7 },
        { 6, 5, 4, 3, 2, 1, 0, 1, 2, 3, 4, 5, 6, 7, 7, 7 }, { 7, 6, 5, 4, 3, 2, 1, 0, 1, 2, 3, 4, 5, 6, 7, 7 },
        { 7, 7, 6, 5, 4, 3, 2, 1, 0, 1, 2, 3, 4, 5, 6, 7 }, { 7, 7, 7, 6, 5, 4, 3, 2, 1, 0, 1, 2, 3, 4, 5, 6 },
        { 7, 7, 7, 7, 6, 5, 4, 3, 2, 1, 0, 1, 2, 3, 4, 5 }, { 7, 7, 7, 7, 7, 6, 5, 4, 3, 2, 1, 0, 1, 2, 3, 4 },
        { 7, 7, 7, 7, 7, 7, 6, 5, 4, 3, 2, 1, 0, 1, 2, 3 }, { 7, 7, 7, 7, 7, 7, 7, 6, 5, 4, 3, 2, 1, 0, 1, 2 },
        { 7, 7, 7, 7, 7, 7, 7, 7, 6, 5, 4, 3, 2, 1, 0, 1 }, { 7, 7, 7, 7, 7, 7, 7, 7, 7, 6, 5, 4, 3, 2, 1, 0 },
    };
}

/// <summary>A pointwise item (LAZ for point formats 0–5): its first point raw, then each point from the shared decoder.</summary>
interface IPointwiseItem
{
    int Size { get; }
    void First(ReadOnlySpan<byte> raw);
    void Next(ArithmeticDecoder dec, Span<byte> out_);
}

/// <summary>Point10, version 2: the 20 bytes of formats 0–5.</summary>
sealed class Point10V2 : IPointwiseItem
{
    readonly byte[] _last = new byte[20];
    readonly ushort[] _lastIntensity = new ushort[16];
    readonly StreamingMedian[] _xMedian = new StreamingMedian[16], _yMedian = new StreamingMedian[16];
    readonly int[] _lastHeight = new int[8];
    readonly ArithmeticModel _changed = new(64);
    readonly ArithmeticModel?[] _scanAngle = new ArithmeticModel?[2], _bitByte = new ArithmeticModel?[256], _classification = new ArithmeticModel?[256], _userData = new ArithmeticModel?[256];
    readonly IntegerDecompressor _intensity = new(16, 4), _sourceId = new(16), _dx = new(32, 2), _dy = new(32, 22), _z = new(32, 20);

    public int Size => 20;

    public void First(ReadOnlySpan<byte> raw)
    {
        raw[..20].CopyTo(_last);
        // LASzip starts from the first point with its intensity zero.
        _last[12] = 0; _last[13] = 0;
    }

    static ArithmeticModel M(ArithmeticModel?[] a, int i, uint n = 256) => a[i] ??= new ArithmeticModel(n);

    public void Next(ArithmeticDecoder dec, Span<byte> o)
    {
        unchecked
        {
            var p = _last.AsSpan();
            var changed = (int)dec.DecodeSymbol(_changed);
            int r, n, m, l;
            if (changed != 0)
            {
                if ((changed & 32) != 0) p[14] = (byte)dec.DecodeSymbol(M(_bitByte, p[14]));
                r = p[14] & 7; n = (p[14] >> 3) & 7;
                m = ReturnTables.Map[n, r]; l = ReturnTables.Level[n, r];
                if ((changed & 16) != 0)
                {
                    var v = (ushort)_intensity.Decompress(dec, _lastIntensity[m], (uint)(m < 3 ? m : 3));
                    BinaryPrimitives.WriteUInt16LittleEndian(p[12..], v);
                    _lastIntensity[m] = v;
                }
                else BinaryPrimitives.WriteUInt16LittleEndian(p[12..], _lastIntensity[m]);
                if ((changed & 8) != 0) p[15] = (byte)dec.DecodeSymbol(M(_classification, p[15]));
                if ((changed & 4) != 0)
                {
                    var dir = (p[14] >> 6) & 1;
                    var v = (sbyte)dec.DecodeSymbol(M(_scanAngle, dir));
                    p[16] = (byte)((sbyte)p[16] + v);
                }
                if ((changed & 2) != 0) p[17] = (byte)dec.DecodeSymbol(M(_userData, p[17]));
                if ((changed & 1) != 0)
                    BinaryPrimitives.WriteUInt16LittleEndian(p[18..], (ushort)_sourceId.Decompress(dec, BinaryPrimitives.ReadUInt16LittleEndian(p[18..]), 0));
            }
            else
            {
                r = p[14] & 7; n = (p[14] >> 3) & 7;
                m = ReturnTables.Map[n, r]; l = ReturnTables.Level[n, r];
            }
            var diff = _dx.Decompress(dec, _xMedian[m].Get(), n == 1 ? 1u : 0u);
            BinaryPrimitives.WriteInt32LittleEndian(p, BinaryPrimitives.ReadInt32LittleEndian(p) + diff);
            _xMedian[m].Add(diff);
            var k = _dx.K;
            var ctx = (n == 1 ? 1u : 0u) + (k < 20 ? k & 0xFFFF_FFFEu : 20u);
            diff = _dy.Decompress(dec, _yMedian[m].Get(), ctx);
            BinaryPrimitives.WriteInt32LittleEndian(p[4..], BinaryPrimitives.ReadInt32LittleEndian(p[4..]) + diff);
            _yMedian[m].Add(diff);
            k = (_dx.K + _dy.K) / 2;
            ctx = (n == 1 ? 1u : 0u) + (k < 18 ? k & 0xFFFF_FFFEu : 18u);
            var z = _z.Decompress(dec, _lastHeight[l], ctx);
            BinaryPrimitives.WriteInt32LittleEndian(p[8..], z);
            _lastHeight[l] = z;
            p.CopyTo(o);
        }
    }
}

/// <summary>GPS time, version 2: decoded only to keep the shared decoder in step.</summary>
sealed class GpsTimeV2 : IPointwiseItem
{
    const int Multi = 500, MultiMinus = -10, MultiUnchanged = Multi - MultiMinus + 1, MultiCodeFull = Multi - MultiMinus + 2, MultiTotal = Multi - MultiMinus + 6;
    readonly ArithmeticModel _multi = new(MultiTotal), _zeroDiff = new(6);
    readonly IntegerDecompressor _ic = new(32, 9);
    int _last, _next;
    readonly long[] _times = new long[4];
    readonly int[] _diffs = new int[4], _extreme = new int[4];

    public int Size => 8;
    public void First(ReadOnlySpan<byte> raw) => _times[0] = BinaryPrimitives.ReadInt64LittleEndian(raw);

    public void Next(ArithmeticDecoder dec, Span<byte> o)
    {
        Decode(dec);
        BinaryPrimitives.WriteInt64LittleEndian(o, _times[_last]);
    }

    void Decode(ArithmeticDecoder dec)
    {
        unchecked
        {
            if (_diffs[_last] == 0)
            {
                var multi = (int)dec.DecodeSymbol(_zeroDiff);
                if (multi == 1)
                {
                    _diffs[_last] = _ic.Decompress(dec, 0, 0);
                    _times[_last] += _diffs[_last];
                    _extreme[_last] = 0;
                }
                else if (multi == 2) Full(dec);
                else if (multi > 2) { _last = (_last + multi - 2) & 3; Decode(dec); }
                return;
            }
            var mu = (int)dec.DecodeSymbol(_multi);
            if (mu == 1)
            {
                _times[_last] += _ic.Decompress(dec, _diffs[_last], 1);
                _extreme[_last] = 0;
            }
            else if (mu < MultiUnchanged)
            {
                int d;
                if (mu == 0)
                {
                    d = _ic.Decompress(dec, 0, 7);
                    if (++_extreme[_last] > 3) { _diffs[_last] = d; _extreme[_last] = 0; }
                }
                else if (mu < Multi) d = _ic.Decompress(dec, mu * _diffs[_last], mu < 10 ? 2u : 3u);
                else if (mu == Multi)
                {
                    d = _ic.Decompress(dec, mu * _diffs[_last], 4);
                    if (++_extreme[_last] > 3) { _diffs[_last] = d; _extreme[_last] = 0; }
                }
                else
                {
                    mu = Multi - mu;
                    if (mu > MultiMinus) d = _ic.Decompress(dec, mu * _diffs[_last], 5);
                    else
                    {
                        d = _ic.Decompress(dec, MultiMinus * _diffs[_last], 6);
                        if (++_extreme[_last] > 3) { _diffs[_last] = d; _extreme[_last] = 0; }
                    }
                }
                _times[_last] += d;
            }
            else if (mu == MultiCodeFull) Full(dec);
            else if (mu > MultiCodeFull) { _last = (_last + mu - MultiCodeFull) & 3; Decode(dec); }
        }
    }

    void Full(ArithmeticDecoder dec)
    {
        unchecked
        {
            _next = (_next + 1) & 3;
            _times[_next] = (long)_ic.Decompress(dec, (int)(_times[_last] >> 32), 8) << 32;
            _times[_next] |= dec.ReadInt();
            _last = _next;
            _diffs[_last] = 0;
            _extreme[_last] = 0;
        }
    }
}

/// <summary>The RGB models of version 2 (shared by the layered version 3's contexts).</summary>
sealed class RgbModels
{
    public readonly ArithmeticModel Used = new(128), LoR = new(256), HiR = new(256), LoG = new(256), HiG = new(256), LoB = new(256), HiB = new(256);

    static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);

    /// <summary>The next colour from the last (red, green, blue, 16 bits each).</summary>
    public (ushort R, ushort G, ushort B) Decode(ArithmeticDecoder dec, (ushort R, ushort G, ushort B) last)
    {
        unchecked
        {
            var sym = dec.DecodeSymbol(Used);
            int r, g, b;
            r = (sym & 1) != 0 ? (byte)(dec.DecodeSymbol(LoR) + (last.R & 0xFF)) : last.R & 0xFF;
            r |= (sym & 2) != 0 ? (byte)(dec.DecodeSymbol(HiR) + (last.R >> 8)) << 8 : last.R & 0xFF00;
            if ((sym & 64) != 0)
            {
                var diff = (r & 0xFF) - (last.R & 0xFF);
                g = (sym & 4) != 0 ? (byte)(dec.DecodeSymbol(LoG) + Clamp(diff + (last.G & 0xFF))) : last.G & 0xFF;
                if ((sym & 16) != 0)
                {
                    diff = (diff + (g & 0xFF) - (last.G & 0xFF)) / 2;
                    b = (byte)(dec.DecodeSymbol(LoB) + Clamp(diff + (last.B & 0xFF)));
                }
                else b = last.B & 0xFF;
                diff = (r >> 8) - (last.R >> 8);
                if ((sym & 8) != 0) g |= (byte)(dec.DecodeSymbol(HiG) + Clamp(diff + (last.G >> 8))) << 8;
                else g |= last.G & 0xFF00;
                if ((sym & 32) != 0)
                {
                    diff = (diff + (g >> 8) - (last.G >> 8)) / 2;
                    b |= (byte)(dec.DecodeSymbol(HiB) + Clamp(diff + (last.B >> 8))) << 8;
                }
                else b |= last.B & 0xFF00;
            }
            else { g = r; b = r; }
            return ((ushort)r, (ushort)g, (ushort)b);
        }
    }
}

sealed class Rgb12V2 : IPointwiseItem
{
    readonly RgbModels _m = new();
    (ushort R, ushort G, ushort B) _last;
    public int Size => 6;
    public void First(ReadOnlySpan<byte> raw) => _last = (BinaryPrimitives.ReadUInt16LittleEndian(raw), BinaryPrimitives.ReadUInt16LittleEndian(raw[2..]), BinaryPrimitives.ReadUInt16LittleEndian(raw[4..]));
    public void Next(ArithmeticDecoder dec, Span<byte> o)
    {
        _last = _m.Decode(dec, _last);
        BinaryPrimitives.WriteUInt16LittleEndian(o, _last.R);
        BinaryPrimitives.WriteUInt16LittleEndian(o[2..], _last.G);
        BinaryPrimitives.WriteUInt16LittleEndian(o[4..], _last.B);
    }
}

/// <summary>Extra bytes, version 2: each byte the last plus a symbol.</summary>
sealed class BytesV2(int count) : IPointwiseItem
{
    readonly byte[] _last = new byte[count];
    readonly ArithmeticModel[] _m = Enumerable.Range(0, count).Select(_ => new ArithmeticModel(256)).ToArray();
    public int Size => count;
    public void First(ReadOnlySpan<byte> raw) => raw[..count].CopyTo(_last);
    public void Next(ArithmeticDecoder dec, Span<byte> o)
    {
        for (var i = 0; i < count; i++) _last[i] = unchecked((byte)(_last[i] + dec.DecodeSymbol(_m[i])));
        _last.CopyTo(o);
    }
}

/// <summary>
/// Point14, version 3 (formats 6–10, layered): the changes, returns and x/y in one layer, z, classification and intensity
/// each in their own; the flags, scan angle, user data, point source and GPS time layers are skipped (Lapin draws none of
/// them, and nothing it decodes depends on them). Four contexts, one per scanner channel.
/// </summary>
sealed class Point14V3
{
    sealed class Context
    {
        public bool Unused = true;
        public int X, Y, Z;
        public ushort Intensity;
        public int Return, Returns;
        public byte Classification;
        public bool GpsChange;
        public readonly ushort[] LastIntensity = new ushort[8];
        public readonly StreamingMedian[] XMedian = new StreamingMedian[12], YMedian = new StreamingMedian[12];
        public readonly int[] LastZ = new int[8];
        public readonly ArithmeticModel[] Changed = Enumerable.Range(0, 8).Select(_ => new ArithmeticModel(128)).ToArray();
        public readonly ArithmeticModel ScannerChannel = new(3);
        public readonly ArithmeticModel?[] NumberOfReturns = new ArithmeticModel?[16], ReturnNumber = new ArithmeticModel?[16], Classes = new ArithmeticModel?[64];
        public readonly ArithmeticModel ReturnGpsSame = new(13);
        public readonly IntegerDecompressor Dx = new(32, 2), Dy = new(32, 22), Dz = new(32, 20), DIntensity = new(16, 4);

        public void From(Context c) => From(c.X, c.Y, c.Z, c.Intensity, c.Return, c.Returns, c.Classification);
        public void From(int x, int y, int z, ushort intensity, int ret, int rets, byte cls)
        {
            Unused = false;
            (X, Y, Z, Intensity, Return, Returns, Classification, GpsChange) = (x, y, z, intensity, ret, rets, cls, false);
            Array.Fill(LastIntensity, intensity);
            Array.Fill(LastZ, z);
        }
    }

    readonly Context[] _ctx = [new(), new(), new(), new()];
    int _current;
    public int Current => _current;
    readonly ArithmeticDecoder _xy = new(), _z = new(), _class = new(), _intensity = new();
    bool _hasZ, _hasClass, _hasIntensity;
    readonly uint[] _sizes = new uint[9];

    public const int Size = 30;

    public void First(ReadOnlySpan<byte> raw)
    {
        foreach (var c in _ctx) c.Unused = true;
        _current = (raw[15] >> 4) & 3;
        _ctx[_current].From(BinaryPrimitives.ReadInt32LittleEndian(raw), BinaryPrimitives.ReadInt32LittleEndian(raw[4..]), BinaryPrimitives.ReadInt32LittleEndian(raw[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(raw[12..]), raw[14] & 15, raw[14] >> 4, raw[16]);
    }

    public void ReadSizes(byte[] b, ref int at)
    {
        for (var i = 0; i < 9; i++) { _sizes[i] = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at)); at += 4; }
    }

    public void ReadLayers(byte[] b, ref int at)
    {
        void Layer(ArithmeticDecoder d, uint size, ref bool has, bool want, ref int pos)
        {
            has = false;
            if (want && size > 0) { d.Reset(b, pos, (int)size); d.ReadInitBytes(); has = true; }
            pos += (int)size;
        }
        var always = true;
        Layer(_xy, _sizes[0], ref always, true, ref at);
        Layer(_z, _sizes[1], ref _hasZ, true, ref at);
        Layer(_class, _sizes[2], ref _hasClass, true, ref at);
        // Flags, then intensity, then scan angle, user data, point source, GPS time.
        at += (int)_sizes[3];
        Layer(_intensity, _sizes[4], ref _hasIntensity, true, ref at);
        for (var i = 5; i < 9; i++) at += (int)_sizes[i];
    }

    /// <summary>The next point into a record of format 6's layout (x, y, z, intensity, returns, class).</summary>
    public void Next(Span<byte> o)
    {
        unchecked
        {
            var c = _ctx[_current];
            var lpr = (c.Return == 1 ? 1 : 0) + (c.Return >= c.Returns ? 2 : 0) + (c.GpsChange ? 4 : 0);
            var changed = (int)_xy.DecodeSymbol(c.Changed[lpr]);
            if ((changed & 64) != 0)
            {
                var diff = (int)_xy.DecodeSymbol(c.ScannerChannel);
                var channel = (_current + diff + 1) % 4;
                if (_ctx[channel].Unused) _ctx[channel].From(c);
                _current = channel;
                c = _ctx[_current];
            }
            var gpsChanged = (changed & 16) != 0;
            int n = (changed & 4) != 0 ? (int)_xy.DecodeSymbol(c.NumberOfReturns[c.Returns] ??= new ArithmeticModel(16)) : c.Returns;
            c.Returns = n;
            int r;
            switch (changed & 3)
            {
                case 0: r = c.Return; break;
                case 1: r = (c.Return + 1) % 16; break;
                case 2: r = (c.Return + 15) % 16; break;
                default:
                    if (gpsChanged) r = (int)_xy.DecodeSymbol(c.ReturnNumber[c.Return] ??= new ArithmeticModel(16));
                    else r = (int)((c.Return + _xy.DecodeSymbol(c.ReturnGpsSame) + 2) % 16);
                    break;
            }
            c.Return = r;
            int m = ReturnTables.Map6[n, r], l = ReturnTables.Level8[n, r];
            var cpr = (r == 1 ? 2 : 0) + (r >= n ? 1 : 0);
            var idx = (m << 1) | (gpsChanged ? 1 : 0);
            var dx = c.Dx.Decompress(_xy, c.XMedian[idx].Get(), n == 1 ? 1u : 0u);
            c.X += dx;
            c.XMedian[idx].Add(dx);
            var k = c.Dx.K;
            var ctx = (n == 1 ? 1u : 0u) + (k < 20 ? k & 0xFFFF_FFFEu : 20u);
            var dy = c.Dy.Decompress(_xy, c.YMedian[idx].Get(), ctx);
            c.Y += dy;
            c.YMedian[idx].Add(dy);
            if (_hasZ)
            {
                k = (c.Dx.K + c.Dy.K) / 2;
                ctx = (n == 1 ? 1u : 0u) + (k < 18 ? k & 0xFFFF_FFFEu : 18u);
                c.Z = c.Dz.Decompress(_z, c.LastZ[l], ctx);
                c.LastZ[l] = c.Z;
            }
            if (_hasClass)
            {
                var ccc = ((c.Classification & 0x1F) << 1) + (cpr == 3 ? 1 : 0);
                c.Classification = (byte)_class.DecodeSymbol(c.Classes[ccc] ??= new ArithmeticModel(256));
            }
            if (_hasIntensity)
            {
                var ii = (cpr << 1) | (gpsChanged ? 1 : 0);
                c.Intensity = (ushort)c.DIntensity.Decompress(_intensity, c.LastIntensity[ii], (uint)cpr);
                c.LastIntensity[ii] = c.Intensity;
            }
            c.GpsChange = gpsChanged;
            BinaryPrimitives.WriteInt32LittleEndian(o, c.X);
            BinaryPrimitives.WriteInt32LittleEndian(o[4..], c.Y);
            BinaryPrimitives.WriteInt32LittleEndian(o[8..], c.Z);
            BinaryPrimitives.WriteUInt16LittleEndian(o[12..], c.Intensity);
            o[14] = (byte)((c.Return & 15) | (c.Returns << 4));
            o[15] = (byte)(_current << 4);
            o[16] = c.Classification;
        }
    }
}

/// <summary>RGB, version 3 (layered): version 2's coding in one layer, a context per scanner channel.</summary>
sealed class Rgb14V3
{
    readonly RgbModels[] _models = [new(), new(), new(), new()];
    readonly bool[] _unused = new bool[4];
    readonly (ushort R, ushort G, ushort B)[] _last = new (ushort, ushort, ushort)[4];
    int _lastContext;
    readonly ArithmeticDecoder _dec = new();
    uint _size;
    bool _has;

    public void First(ReadOnlySpan<byte> raw, int context)
    {
        Array.Fill(_unused, true);
        _last[context] = (BinaryPrimitives.ReadUInt16LittleEndian(raw), BinaryPrimitives.ReadUInt16LittleEndian(raw[2..]), BinaryPrimitives.ReadUInt16LittleEndian(raw[4..]));
        _unused[context] = false;
        _lastContext = context;
    }

    public void ReadSizes(byte[] b, ref int at) { _size = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at)); at += 4; }

    public void ReadLayers(byte[] b, ref int at)
    {
        _has = _size > 0;
        if (_has) { _dec.Reset(b, at, (int)_size); _dec.ReadInitBytes(); }
        at += (int)_size;
    }

    public void Next(Span<byte> o, int context)
    {
        var last = _last[_lastContext];
        if (_lastContext != context)
        {
            _lastContext = context;
            if (_unused[context]) { _last[context] = last; _unused[context] = false; }
            last = _last[context];
        }
        if (_has) _last[_lastContext] = last = _models[_lastContext].Decode(_dec, last);
        BinaryPrimitives.WriteUInt16LittleEndian(o, last.R);
        BinaryPrimitives.WriteUInt16LittleEndian(o[2..], last.G);
        BinaryPrimitives.WriteUInt16LittleEndian(o[4..], last.B);
    }
}
