using System.Text;

namespace Lapin.Mesh.Bos;

/// <summary>
/// Apache Thrift's compact protocol, as much as Parquet's metadata needs (docs/bos.md): a struct read into its fields by id
/// (numbers as long, doubles, binaries, lists, nested structs, booleans) and a struct written from typed fields. Written
/// after the protocol's public specification (thrift/doc/specs/thrift-compact-protocol.md); no dependency.
/// </summary>
public static class Thrift
{
    public const byte TStop = 0, TTrue = 1, TFalse = 2, TByte = 3, TI16 = 4, TI32 = 5, TI64 = 6, TDouble = 7, TBinary = 8, TList = 9, TSet = 10, TMap = 11, TStruct = 12;

    /// <summary>A read struct: each field by its id.</summary>
    public sealed class Fields : Dictionary<short, object>
    {
        public long Long(short id, long or = 0) => TryGetValue(id, out var v) && v is long l ? l : or;
        public int Int(short id, int or = 0) => (int)Long(id, or);
        public bool Bool(short id, bool or = false) => TryGetValue(id, out var v) && v is bool b ? b : or;
        public string? Text(short id) => TryGetValue(id, out var v) && v is byte[] b ? Encoding.UTF8.GetString(b) : null;
        public Fields? Sub(short id) => TryGetValue(id, out var v) ? v as Fields : null;
        public List<object> Items(short id) => TryGetValue(id, out var v) && v is List<object> l ? l : [];
    }

    /// <summary>Reads compact-protocol structs from a buffer.</summary>
    public sealed class Reader(byte[] data, int position)
    {
        public int Position { get; private set; } = position;

        byte Next()
        {
            if (Position >= data.Length) throw new InvalidDataException("The Thrift structure ends early.");
            return data[Position++];
        }

        ulong Varint()
        {
            ulong v = 0;
            for (var shift = 0; shift < 64; shift += 7)
            {
                var b = Next();
                v |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return v;
            }
            throw new InvalidDataException("A Thrift varint is too long.");
        }

        long Zigzag() { var u = Varint(); return (long)(u >> 1) ^ -(long)(u & 1); }

        public Fields ReadStruct(int depth = 0)
        {
            if (depth > 32) throw new InvalidDataException("The Thrift structure nests too deeply.");
            var o = new Fields();
            short last = 0;
            while (true)
            {
                var head = Next();
                if (head == TStop) return o;
                var type = (byte)(head & 0x0F);
                var delta = head >> 4;
                var id = delta != 0 ? (short)(last + delta) : (short)Zigzag();
                last = id;
                o[id] = Value(type, depth);
            }
        }

        object Value(byte type, int depth)
        {
            switch (type)
            {
                case TTrue: return true;
                case TFalse: return false;
                case TByte: return (long)(sbyte)Next();
                case TI16 or TI32 or TI64: return Zigzag();
                case TDouble:
                {
                    if (Position + 8 > data.Length) throw new InvalidDataException("The Thrift structure ends early.");
                    var d = BitConverter.ToDouble(data, Position);
                    Position += 8;
                    return d;
                }
                case TBinary:
                {
                    var n = (int)Varint();
                    if (n < 0 || Position + n > data.Length) throw new InvalidDataException("A Thrift binary runs past the end.");
                    var b = data.AsSpan(Position, n).ToArray();
                    Position += n;
                    return b;
                }
                case TList or TSet:
                {
                    var head = Next();
                    var size = head >> 4;
                    var element = (byte)(head & 0x0F);
                    if (size == 15) size = (int)Varint();
                    if (size < 0 || size > data.Length) throw new InvalidDataException("A Thrift list is too long.");
                    var list = new List<object>(size);
                    for (var i = 0; i < size; i++) list.Add(element is TTrue or TFalse ? Next() == TTrue : Value(element, depth + 1));
                    return list;
                }
                case TMap:
                {
                    var size = (int)Varint();
                    if (size == 0) return new List<object>();
                    var kinds = Next();
                    var list = new List<object>(size * 2);
                    for (var i = 0; i < size; i++) { list.Add(Value((byte)(kinds >> 4), depth + 1)); list.Add(Value((byte)(kinds & 0x0F), depth + 1)); }
                    return list;
                }
                case TStruct: return ReadStruct(depth + 1);
                default: throw new InvalidDataException($"An unknown Thrift type {type}.");
            }
        }
    }

    /// <summary>Writes compact-protocol structs: fields in increasing id order.</summary>
    public sealed class Writer
    {
        readonly MemoryStream o = new();
        readonly Stack<short> lasts = new();
        short last;

        public byte[] ToArray() => o.ToArray();

        void Varint(ulong v)
        {
            while (v >= 0x80) { o.WriteByte((byte)(v | 0x80)); v >>= 7; }
            o.WriteByte((byte)v);
        }

        void Zigzag(long v) => Varint((ulong)((v << 1) ^ (v >> 63)));

        void Head(short id, byte type)
        {
            var delta = id - last;
            if (delta is > 0 and <= 15) o.WriteByte((byte)((delta << 4) | type));
            else { o.WriteByte(type); Zigzag(id); }
            last = id;
        }

        public Writer Int(short id, long v) { Head(id, TI32); Zigzag(v); return this; }
        public Writer Long(short id, long v) { Head(id, TI64); Zigzag(v); return this; }
        public Writer SByte(short id, sbyte v) { Head(id, TByte); o.WriteByte((byte)v); return this; }
        public Writer Bool(short id, bool v) { Head(id, v ? TTrue : TFalse); return this; }
        public Writer Text(short id, string v) { Head(id, TBinary); var b = Encoding.UTF8.GetBytes(v); Varint((ulong)b.Length); o.Write(b); return this; }

        public Writer Begin(short id) { Head(id, TStruct); lasts.Push(last); last = 0; return this; }
        public Writer End() { o.WriteByte(TStop); last = lasts.Pop(); return this; }

        /// <summary>A list header; its elements follow (structs: <see cref="BeginElement"/>/<see cref="End"/>, i32: <see cref="ListInt"/>).</summary>
        public Writer BeginList(short id, byte element, int count)
        {
            Head(id, TList);
            if (count < 15) o.WriteByte((byte)((count << 4) | element));
            else { o.WriteByte((byte)(0xF0 | element)); Varint((ulong)count); }
            return this;
        }

        public Writer BeginElement() { lasts.Push(last); last = 0; return this; }
        public Writer ListInt(long v) { Zigzag(v); return this; }
        public Writer ListText(string v) { var b = Encoding.UTF8.GetBytes(v); Varint((ulong)b.Length); o.Write(b); return this; }
        /// <summary>Ends the outermost struct written with <see cref="Writer"/> (no field id of its own).</summary>
        public Writer Finish() { o.WriteByte(TStop); return this; }
    }
}
