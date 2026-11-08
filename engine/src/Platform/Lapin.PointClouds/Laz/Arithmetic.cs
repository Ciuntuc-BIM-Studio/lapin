// LAZ's entropy coding, ported to C# from laz-rs 0.9.3 (Thomas Montaigu, Apache License 2.0), itself a port of the
// arithmetic coder of LASzip (Martin Isenburg, rapidlasso) after Amir Said's FastAC. See Laz/NOTICE.md.

namespace Lapin.PointClouds.Laz;

/// <summary>An adaptive model of a few symbols (2–2048), with a lookup table for decoding when it has more than 16.</summary>
sealed class ArithmeticModel
{
    public const int LengthShift = 15;
    const uint MaxCount = 1u << LengthShift;

    public readonly uint Symbols;
    public readonly uint[] Distribution, SymbolCount, DecoderTable;
    uint _totalCount, _updateCycle;
    public uint SymbolsUntilUpdate;
    public readonly uint LastSymbol;
    readonly uint _tableSize;
    public readonly int TableShift;

    public ArithmeticModel(uint symbols)
    {
        if (symbols < 2 || symbols > 1 << 11) throw new ArgumentOutOfRangeException(nameof(symbols));
        Symbols = symbols;
        LastSymbol = symbols - 1;
        if (symbols > 16)
        {
            var tableBits = 3;
            while (symbols > 1u << (tableBits + 2)) tableBits++;
            _tableSize = 1u << tableBits;
            TableShift = LengthShift - tableBits;
            DecoderTable = new uint[_tableSize + 2];
        }
        else DecoderTable = [];
        Distribution = new uint[symbols];
        SymbolCount = new uint[symbols];
        Array.Fill(SymbolCount, 1u);
        _updateCycle = symbols;
        Update();
        SymbolsUntilUpdate = _updateCycle = (symbols + 6) >> 1;
    }

    public void Update()
    {
        unchecked
        {
            _totalCount += _updateCycle;
            if (_totalCount > MaxCount)
            {
                _totalCount = 0;
                for (var i = 0; i < SymbolCount.Length; i++) { SymbolCount[i] = (SymbolCount[i] + 1) >> 1; _totalCount += SymbolCount[i]; }
            }
            uint sum = 0;
            var scale = 0x8000_0000u / _totalCount;
            if (_tableSize == 0)
                for (var k = 0; k < Symbols; k++) { Distribution[k] = (scale * sum) >> (31 - LengthShift); sum += SymbolCount[k]; }
            else
            {
                var s = 0;
                for (var k = 0; k < Symbols; k++)
                {
                    Distribution[k] = (scale * sum) >> (31 - LengthShift);
                    sum += SymbolCount[k];
                    var w = (int)(Distribution[k] >> TableShift);
                    while (s < w) { s++; DecoderTable[s] = (uint)(k - 1); }
                }
                DecoderTable[0] = 0;
                while (s <= _tableSize) { s++; DecoderTable[s] = Symbols - 1; }
            }
            _updateCycle = (5 * _updateCycle) >> 2;
            var max = (Symbols + 6) << 3;
            if (_updateCycle > max) _updateCycle = max;
            SymbolsUntilUpdate = _updateCycle;
        }
    }
}

/// <summary>An adaptive model of one bit.</summary>
sealed class ArithmeticBitModel
{
    public const int LengthShift = 13;
    const uint MaxCount = 1u << LengthShift;
    public uint Bit0Count = 1, BitCount = 2, Bit0Prob = 1u << (LengthShift - 1), BitsUntilUpdate = 4, UpdateCycle = 4;

    public void Update()
    {
        unchecked
        {
            BitCount += UpdateCycle;
            if (BitCount > MaxCount)
            {
                BitCount = (BitCount + 1) >> 1;
                Bit0Count = (Bit0Count + 1) >> 1;
                if (Bit0Count == BitCount) BitCount++;
            }
            var scale = 0x8000_0000u / BitCount;
            Bit0Prob = (Bit0Count * scale) >> (31 - LengthShift);
            UpdateCycle = (5 * UpdateCycle) >> 2;
            if (UpdateCycle > 64) UpdateCycle = 64;
            BitsUntilUpdate = UpdateCycle;
        }
    }
}

/// <summary>The arithmetic decoder over a span of a file's bytes (past their end it reads zeros, as LASzip's streams do).</summary>
sealed class ArithmeticDecoder
{
    const uint MinLength = 0x0100_0000;
    byte[] _b = [];
    int _at, _end;
    uint _value, _length = 0xFFFF_FFFF;

    public ArithmeticDecoder() { }
    public ArithmeticDecoder(byte[] bytes, int at, int count) => Reset(bytes, at, count);

    public void Reset(byte[] bytes, int at, int count)
    {
        _b = bytes; _at = at; _end = Math.Min(bytes.Length, at + count);
        _value = 0; _length = 0xFFFF_FFFF;
    }

    /// <summary>Where the next byte would be read (for the first point of a chunk, read raw before the coder starts).</summary>
    public int Position { get => _at; set => _at = value; }

    byte Next() => _at < _end ? _b[_at++] : (byte)0;

    public void ReadInitBytes()
    {
        _value = (uint)Next() << 24 | (uint)Next() << 16 | (uint)Next() << 8 | Next();
    }

    public uint DecodeBit(ArithmeticBitModel m)
    {
        unchecked
        {
            var x = m.Bit0Prob * (_length >> ArithmeticBitModel.LengthShift);
            var sym = _value >= x;
            if (!sym) { _length = x; m.Bit0Count++; }
            else { _value -= x; _length -= x; }
            if (_length < MinLength) Renorm();
            if (--m.BitsUntilUpdate == 0) m.Update();
            return sym ? 1u : 0u;
        }
    }

    public uint DecodeSymbol(ArithmeticModel m)
    {
        unchecked
        {
            uint sym, n, x, y = _length;
            if (m.DecoderTable.Length > 0)
            {
                _length >>= ArithmeticModel.LengthShift;
                if (_length == 0) throw new PointCloudException("The LAZ data is damaged (its decoder ran out of range).");
                var dv = _value / _length;
                var t = dv >> m.TableShift;
                sym = m.DecoderTable[t];
                n = m.DecoderTable[t + 1] + 1;
                while (n > sym + 1)
                {
                    var k = (sym + n) >> 1;
                    if (m.Distribution[k] > dv) n = k; else sym = k;
                }
                x = m.Distribution[sym] * _length;
                if (sym != m.LastSymbol) y = m.Distribution[sym + 1] * _length;
            }
            else
            {
                x = 0; sym = 0;
                _length >>= ArithmeticModel.LengthShift;
                n = m.Symbols;
                var k = n >> 1;
                while (true)
                {
                    var z = _length * m.Distribution[k];
                    if (z > _value) { n = k; y = z; } else { sym = k; x = z; }
                    k = (sym + n) >> 1;
                    if (k == sym) break;
                }
            }
            _value -= x;
            _length = y - x;
            if (_length < MinLength) Renorm();
            m.SymbolCount[sym]++;
            if (--m.SymbolsUntilUpdate == 0) m.Update();
            return sym;
        }
    }

    public uint ReadBit()
    {
        _length >>= 1;
        var sym = _value / _length;
        _value -= _length * sym;
        if (_length < MinLength) Renorm();
        return sym;
    }

    public uint ReadBits(int bits)
    {
        if (bits > 19)
        {
            var lo = (uint)ReadShort();
            var hi = ReadBits(bits - 16) << 16;
            return hi | lo;
        }
        _length >>= bits;
        var sym = _value / _length;
        _value -= _length * sym;
        if (_length < MinLength) Renorm();
        return sym;
    }

    ushort ReadShort()
    {
        _length >>= 16;
        var sym = _value / _length;
        _value -= _length * sym;
        if (_length < MinLength) Renorm();
        return (ushort)sym;
    }

    public uint ReadInt()
    {
        var lo = (uint)ReadShort();
        var hi = (uint)ReadShort();
        return hi << 16 | lo;
    }

    void Renorm()
    {
        unchecked
        {
            do { _value = (_value << 8) | Next(); _length <<= 8; } while (_length < MinLength);
        }
    }
}

/// <summary>An integer predicted and corrected: the correction's bit length coded, then its bits (LASzip's IntegerCompressor, decoding).</summary>
sealed class IntegerDecompressor
{
    readonly uint _contexts, _bitsHigh, _corrBits, _corrRange;
    readonly int _corrMin;
    readonly ArithmeticModel[] _bits;
    readonly ArithmeticBitModel _corrector0 = new();
    readonly ArithmeticModel[] _corrector;
    public uint K { get; private set; }

    public IntegerDecompressor(uint bits = 16, uint contexts = 1, uint bitsHigh = 8, uint range = 0)
    {
        _contexts = contexts; _bitsHigh = bitsHigh;
        if (range != 0)
        {
            _corrBits = 0; _corrRange = range;
            while (range != 0) { range >>= 1; _corrBits++; }
            if (_corrRange == 1u << (int)(_corrBits - 1)) _corrBits--;
            _corrMin = -(int)(_corrRange / 2);
        }
        else if (bits != 0 && bits < 32) { _corrBits = bits; _corrRange = 1u << (int)bits; _corrMin = -(int)(_corrRange / 2); }
        else { _corrBits = 32; _corrRange = 0; _corrMin = int.MinValue; }
        _bits = new ArithmeticModel[_contexts];
        for (var i = 0; i < _contexts; i++) _bits[i] = new ArithmeticModel(_corrBits + 1);
        _corrector = new ArithmeticModel[_corrBits];
        for (var i = 1; i <= _corrBits; i++) _corrector[i - 1] = new ArithmeticModel(i <= _bitsHigh ? 1u << i : 1u << (int)_bitsHigh);
    }

    public int Decompress(ArithmeticDecoder dec, int pred, uint context)
    {
        unchecked
        {
            int c;
            K = dec.DecodeSymbol(_bits[context]);
            if (K != 0)
            {
                if (K < 32)
                {
                    if (K <= _bitsHigh) c = (int)dec.DecodeSymbol(_corrector[K - 1]);
                    else
                    {
                        var k1 = (int)(K - _bitsHigh);
                        c = (int)dec.DecodeSymbol(_corrector[K - 1]);
                        var c1 = dec.ReadBits(k1);
                        c = (c << k1) | (int)c1;
                    }
                    if (c >= (int)(1u << (int)(K - 1))) c += 1;
                    else c -= (int)((1u << (int)K) - 1);
                }
                else c = _corrMin;
            }
            else c = (int)dec.DecodeBit(_corrector0);
            var real = pred + c;
            if (real < 0) real += (int)_corrRange;
            else if (real >= (int)_corrRange) real -= (int)_corrRange;
            return real;
        }
    }
}

/// <summary>The median of the last five values, kept as they come.</summary>
struct StreamingMedian
{
    int _v0, _v1, _v2, _v3, _v4;
    bool _low;

    public readonly int Get() => _v2;

    public void Add(int v)
    {
        if (!_low)
        {
            if (v < _v2)
            {
                _v4 = _v3; _v3 = _v2;
                if (v < _v0) { _v2 = _v1; _v1 = _v0; _v0 = v; }
                else if (v < _v1) { _v2 = _v1; _v1 = v; }
                else _v2 = v;
            }
            else
            {
                if (v < _v3) { _v4 = _v3; _v3 = v; }
                else _v4 = v;
                _low = true;
            }
        }
        else
        {
            if (_v2 < v)
            {
                _v0 = _v1; _v1 = _v2;
                if (_v4 < v) { _v2 = _v3; _v3 = _v4; _v4 = v; }
                else if (_v3 < v) { _v2 = _v3; _v3 = v; }
                else _v2 = v;
            }
            else
            {
                if (_v1 < v) { _v0 = _v1; _v1 = v; }
                else _v0 = v;
                _low = false;
            }
        }
    }
}
