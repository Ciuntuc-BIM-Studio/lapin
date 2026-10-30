using System.Buffers.Binary;
using System.Text;

namespace Lapin.Mesh.Fragments;

/// <summary>
/// Enough of the FlatBuffers wire format to write and walk a Fragments file,
/// with no generated code: tables with their vtables, vectors of scalars,
/// structs, strings and tables, and offsets. Written back to front as every
/// FlatBuffers builder is (the root comes last, at the lowest address), with
/// the same alignment rules, so what the official library reads is byte for
/// byte what its own builder would have laid out.
/// </summary>
public sealed class FlatBuilder
{
    byte[] _buf;
    /// <summary>Where the written data starts: it grows downwards from the end of the buffer.</summary>
    int _space;
    int _minAlign = 1;
    int[] _vtable = [];
    int _vtableInUse;
    int _objectStart;
    bool _nested;

    public FlatBuilder(int initial = 1 << 16)
    {
        _buf = new byte[Math.Max(initial, 16)];
        _space = _buf.Length;
    }

    /// <summary>How many bytes are written: an offset counts from the end of the buffer.</summary>
    public int Offset() => _buf.Length - _space;

    public void Pad(int n) { for (var i = 0; i < n; i++) _buf[--_space] = 0; }

    /// <summary>Room and alignment for <paramref name="size"/> bytes that follow <paramref name="additional"/> bytes about to be written.</summary>
    public void Prep(int size, int additional)
    {
        if (size > _minAlign) _minAlign = size;
        var alignSize = (~(_buf.Length - _space + additional) + 1) & (size - 1);
        while (_space < alignSize + size + additional) Grow();
        Pad(alignSize);
    }

    void Grow()
    {
        var old = _buf;
        var bigger = new byte[old.Length * 2];
        Buffer.BlockCopy(old, 0, bigger, bigger.Length - old.Length, old.Length);
        _space += bigger.Length - old.Length;
        _buf = bigger;
    }

    // ------------------------------------------------------------ scalars, back to front

    public void PutByte(byte v) { _buf[--_space] = v; }
    public void PutSbyte(sbyte v) { _buf[--_space] = unchecked((byte)v); }
    public void PutUshort(ushort v) { _space -= 2; BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(_space), v); }
    public void PutShort(short v) { _space -= 2; BinaryPrimitives.WriteInt16LittleEndian(_buf.AsSpan(_space), v); }
    public void PutInt(int v) { _space -= 4; BinaryPrimitives.WriteInt32LittleEndian(_buf.AsSpan(_space), v); }
    public void PutUint(uint v) { _space -= 4; BinaryPrimitives.WriteUInt32LittleEndian(_buf.AsSpan(_space), v); }
    public void PutFloat(float v) { _space -= 4; BinaryPrimitives.WriteSingleLittleEndian(_buf.AsSpan(_space), v); }
    public void PutDouble(double v) { _space -= 8; BinaryPrimitives.WriteDoubleLittleEndian(_buf.AsSpan(_space), v); }

    public void PrependByte(byte v) { Prep(1, 0); PutByte(v); }
    public void PrependSbyte(sbyte v) { Prep(1, 0); PutSbyte(v); }
    public void PrependUshort(ushort v) { Prep(2, 0); PutUshort(v); }
    public void PrependInt(int v) { Prep(4, 0); PutInt(v); }
    public void PrependUint(uint v) { Prep(4, 0); PutUint(v); }
    public void PrependFloat(float v) { Prep(4, 0); PutFloat(v); }
    public void PrependDouble(double v) { Prep(8, 0); PutDouble(v); }

    /// <summary>An offset to something written earlier, relative to where it is written.</summary>
    public void PrependUOffset(int off)
    {
        Prep(4, 0);
        if (off > Offset()) throw new InvalidOperationException("FlatBuffers: an offset must point at something already written.");
        PutInt(Offset() - off + 4);
    }

    // ------------------------------------------------------------ vectors and strings

    public void StartVector(int elemSize, int count, int alignment)
    {
        Prep(4, elemSize * count);
        Prep(alignment, elemSize * count);
        _nested = true;
    }

    public int EndVector(int count)
    {
        _nested = false;
        PutInt(count);
        return Offset();
    }

    public int CreateString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        StartVector(1, bytes.Length + 1, 1);
        PutByte(0);
        _space -= bytes.Length;
        Buffer.BlockCopy(bytes, 0, _buf, _space, bytes.Length);
        return EndVector(bytes.Length);
    }

    public int CreateStringVector(IReadOnlyList<string> values)
    {
        var offs = new int[values.Count];
        for (var i = 0; i < values.Count; i++) offs[i] = CreateString(values[i]);
        return CreateOffsetVector(offs);
    }

    public int CreateOffsetVector(IReadOnlyList<int> offs)
    {
        StartVector(4, offs.Count, 4);
        for (var i = offs.Count - 1; i >= 0; i--) PrependUOffset(offs[i]);
        return EndVector(offs.Count);
    }

    public int CreateUintVector(IReadOnlyList<uint> values)
    {
        StartVector(4, values.Count, 4);
        for (var i = values.Count - 1; i >= 0; i--) PutUint(values[i]);
        return EndVector(values.Count);
    }

    public int CreateIntVector(IReadOnlyList<int> values)
    {
        StartVector(4, values.Count, 4);
        for (var i = values.Count - 1; i >= 0; i--) PutInt(values[i]);
        return EndVector(values.Count);
    }

    public int CreateUshortVector(IReadOnlyList<ushort> values)
    {
        StartVector(2, values.Count, 2);
        for (var i = values.Count - 1; i >= 0; i--) PutUshort(values[i]);
        return EndVector(values.Count);
    }

    // ------------------------------------------------------------ tables

    public void StartObject(int fields)
    {
        if (_vtable.Length < fields) _vtable = new int[fields];
        Array.Clear(_vtable, 0, fields);
        _vtableInUse = fields;
        _objectStart = Offset();
        _nested = true;
    }

    void Slot(int slot) => _vtable[slot] = Offset();

    public void AddOffset(int slot, int off) { if (off == 0) return; PrependUOffset(off); Slot(slot); }
    public void AddUint(int slot, uint v, uint def = 0, bool force = false) { if (v == def && !force) return; PrependUint(v); Slot(slot); }
    public void AddSbyte(int slot, sbyte v, sbyte def = 0) { if (v == def) return; PrependSbyte(v); Slot(slot); }
    /// <summary>A struct written just before, inline in the table.</summary>
    public void AddStruct(int slot, int off) { if (off == 0) return; Slot(slot); }

    public int EndObject()
    {
        PrependInt(0); // the offset to the vtable, patched below
        var objectOffset = Offset();
        for (var i = _vtableInUse - 1; i >= 0; i--)
            PrependUshort((ushort)(_vtable[i] != 0 ? objectOffset - _vtable[i] : 0));
        PrependUshort((ushort)(objectOffset - _objectStart));
        PrependUshort((ushort)((_vtableInUse + 2) * 2));
        BinaryPrimitives.WriteInt32LittleEndian(_buf.AsSpan(_buf.Length - objectOffset), Offset() - objectOffset);
        _nested = false;
        return objectOffset;
    }

    public byte[] Finish(int root)
    {
        if (_nested) throw new InvalidOperationException("FlatBuffers: a table or vector is still open.");
        Prep(_minAlign, 4);
        PrependUOffset(root);
        var o = new byte[_buf.Length - _space];
        Buffer.BlockCopy(_buf, _space, o, 0, o.Length);
        return o;
    }
}

/// <summary>A table in a finished buffer, read by its fields' vtable slots.</summary>
public readonly struct FlatTable(byte[] buf, int pos)
{
    public readonly byte[] Buf = buf;
    public readonly int Pos = pos;

    public static FlatTable Root(byte[] buf) => new(buf, BinaryPrimitives.ReadInt32LittleEndian(buf));

    /// <summary>A field's offset from the table, or 0 when the file has no such field.</summary>
    int Field(int slot)
    {
        var vtable = Pos - BinaryPrimitives.ReadInt32LittleEndian(Buf.AsSpan(Pos));
        var len = BinaryPrimitives.ReadUInt16LittleEndian(Buf.AsSpan(vtable));
        var o = 4 + slot * 2;
        return o < len ? BinaryPrimitives.ReadUInt16LittleEndian(Buf.AsSpan(vtable + o)) : 0;
    }

    int Indirect(int at) => at + BinaryPrimitives.ReadInt32LittleEndian(Buf.AsSpan(at));

    public bool Has(int slot) => Field(slot) != 0;
    public uint Uint(int slot, uint def = 0) => Field(slot) is var o and not 0 ? BinaryPrimitives.ReadUInt32LittleEndian(Buf.AsSpan(Pos + o)) : def;
    public sbyte Sbyte(int slot, sbyte def = 0) => Field(slot) is var o and not 0 ? unchecked((sbyte)Buf[Pos + o]) : def;
    public ushort Ushort(int slot, ushort def = 0) => Field(slot) is var o and not 0 ? BinaryPrimitives.ReadUInt16LittleEndian(Buf.AsSpan(Pos + o)) : def;
    /// <summary>Where an inline struct starts, or −1.</summary>
    public int Struct(int slot) => Field(slot) is var o and not 0 ? Pos + o : -1;
    public FlatTable? Table(int slot) => Field(slot) is var o and not 0 ? new FlatTable(Buf, Indirect(Pos + o)) : null;

    public string? String(int slot) => Field(slot) is var o and not 0 ? ReadString(Indirect(Pos + o)) : null;

    string ReadString(int at)
    {
        var n = BinaryPrimitives.ReadInt32LittleEndian(Buf.AsSpan(at));
        return Encoding.UTF8.GetString(Buf, at + 4, n);
    }

    /// <summary>(start of the elements, their count), or null.</summary>
    public (int Start, int Count)? Vector(int slot)
    {
        var o = Field(slot);
        if (o == 0) return null;
        var v = Indirect(Pos + o);
        return (v + 4, BinaryPrimitives.ReadInt32LittleEndian(Buf.AsSpan(v)));
    }

    public uint[]? Uints(int slot)
    {
        if (Vector(slot) is not var (start, n)) return null;
        var o = new uint[n];
        for (var i = 0; i < n; i++) o[i] = BinaryPrimitives.ReadUInt32LittleEndian(Buf.AsSpan(start + i * 4));
        return o;
    }

    public int[]? Ints(int slot)
    {
        if (Vector(slot) is not var (start, n)) return null;
        var o = new int[n];
        for (var i = 0; i < n; i++) o[i] = BinaryPrimitives.ReadInt32LittleEndian(Buf.AsSpan(start + i * 4));
        return o;
    }

    public ushort[]? Ushorts(int slot)
    {
        if (Vector(slot) is not var (start, n)) return null;
        var o = new ushort[n];
        for (var i = 0; i < n; i++) o[i] = BinaryPrimitives.ReadUInt16LittleEndian(Buf.AsSpan(start + i * 2));
        return o;
    }

    public float[]? Floats(int slot, int perElement)
    {
        if (Vector(slot) is not var (start, n)) return null;
        var o = new float[n * perElement];
        for (var i = 0; i < o.Length; i++) o[i] = BinaryPrimitives.ReadSingleLittleEndian(Buf.AsSpan(start + i * 4));
        return o;
    }

    public string[]? Strings(int slot)
    {
        if (Vector(slot) is not var (start, n)) return null;
        var o = new string[n];
        for (var i = 0; i < n; i++) o[i] = ReadString(Indirect(start + i * 4));
        return o;
    }

    public FlatTable[]? Tables(int slot)
    {
        if (Vector(slot) is not var (start, n)) return null;
        var o = new FlatTable[n];
        for (var i = 0; i < n; i++) o[i] = new FlatTable(Buf, Indirect(start + i * 4));
        return o;
    }
}
