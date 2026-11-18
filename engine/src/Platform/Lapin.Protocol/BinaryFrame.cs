using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Lapin.Protocol;

/// <summary>
/// What the engine sends: one binary WebSocket frame per message, so meshes
/// travel as raw little-endian arrays instead of JSON numbers.
/// <code>
/// "LPF1"            4 bytes, the frame's mark
/// header length     uint32 LE, the JSON header's bytes
/// header            the message as JSON (UTF-8), padded with spaces to a multiple of 4
/// buffers           every float[] / int[] of the message, and a file's bytes (<see cref="FrameBytes"/>), back to back, each 4-aligned
/// </code>
/// In the header an array is a reference: <c>{"$buf":offset,"n":count,"t":"f32"|"i32"|"u8"}</c>,
/// its offset counted from the start of the buffers. The interface reads them
/// as Float32Array / Int32Array / Uint8Array views, without copying.
/// </summary>
public static class BinaryFrame
{
    public static ReadOnlySpan<byte> Mark => "LPF1"u8;

    /// <summary>
    /// The buffers of a frame, kept as they are until the frame is written and copied once, into it (report #156: a 150 MB file
    /// went through a growing stream and its copy, where the browser's engine has 2 GB for everything).
    /// </summary>
    sealed class Buffers
    {
        readonly List<(Array Array, int Start, int Count)> _parts = [];
        public long Length { get; private set; }
        public long Add(float[] a) => Add(a, 0, a.Length * sizeof(float));
        public long Add(int[] a) => Add(a, 0, a.Length * sizeof(int));
        public long Add(ArraySegment<byte> a) => Add(a.Array ?? [], a.Offset, a.Count);

        long Add(Array array, int start, int count)
        {
            var at = Length;
            _parts.Add((array, start, count));
            // Each buffer 4-aligned: a file's bytes, of any length, may come before floats, which a Float32Array reads only from a multiple of 4.
            Length += (count + 3) & ~3;
            return at;
        }

        public void CopyTo(Span<byte> o)
        {
            var at = 0;
            foreach (var (array, start, count) in _parts)
            {
                ReadOnlySpan<byte> bytes = array switch
                {
                    float[] f => MemoryMarshal.AsBytes(f.AsSpan()),
                    int[] i => MemoryMarshal.AsBytes(i.AsSpan()),
                    _ => ((byte[])array).AsSpan(start, count),
                };
                bytes.CopyTo(o[at..]);
                at += (count + 3) & ~3;
            }
        }
    }

    /// <summary>The buffers of the frame being encoded on this thread (the converters write into them).</summary>
    [ThreadStatic] static Buffers? _current;

    sealed class FloatsConverter : JsonConverter<float[]>
    {
        public override float[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter w, float[] value, JsonSerializerOptions options) => Ref(w, _current!.Add(value), value.Length, "f32");
    }

    sealed class IntsConverter : JsonConverter<int[]>
    {
        public override int[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter w, int[] value, JsonSerializerOptions options) => Ref(w, _current!.Add(value), value.Length, "i32");
    }

    /// <summary>
    /// A file's bytes in a reply (<see cref="FrameBytes"/>): in a frame, one of its buffers (<c>"t":"u8"</c>), never text; written
    /// outside a frame (a log, a test's JSON), as base64.
    /// </summary>
    public sealed class FrameBytesConverter : JsonConverter<FrameBytes>
    {
        public override FrameBytes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter w, FrameBytes value, JsonSerializerOptions options)
        {
            if (_current is { } buffers) Ref(w, buffers.Add(value.Bytes), value.Bytes.Count, "u8");
            else w.WriteBase64StringValue(value.Bytes);
        }
    }

    /// <summary>A value whose type is known only when it is written (a reply's result): a JSON node as it is, anything else by its own contract.</summary>
    sealed class AnyConverter : JsonConverter<object>
    {
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter w, object value, JsonSerializerOptions options)
        {
            if (value is JsonNode node) { node.WriteTo(w, options); return; }
            JsonSerializer.Serialize(w, value, options.GetTypeInfo(value.GetType()));
        }
    }

    static void Ref(Utf8JsonWriter w, long offset, int count, string type)
    {
        w.WriteStartObject();
        w.WriteNumber("$buf", offset);
        w.WriteNumber("n", count);
        w.WriteString("t", type);
        w.WriteEndObject();
    }

    /// <summary>
    /// The engine's options with the protocol's contracts (generated at build
    /// time); where reflection is allowed (the desktop), anything else too.
    /// </summary>
    static readonly JsonSerializerOptions Options = CreateOptions(contractsOnly: false);

    /// <summary>The generated contracts alone, as a trimmed engine has them (tests prove the frames come out the same).</summary>
    static readonly JsonSerializerOptions ContractsOnly = CreateOptions(contractsOnly: true);

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Reflection only where the runtime allows it (JsonSerializer.IsReflectionEnabledByDefault, a feature switch): the desktop. A trimmed engine turns the switch off and uses the generated contracts alone.")]
    static JsonSerializerOptions CreateOptions(bool contractsOnly)
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault && !contractsOnly
                ? JsonTypeInfoResolver.Combine(ProtocolJson.Default, new DefaultJsonTypeInfoResolver())
                : ProtocolJson.Default,
        };
        o.Converters.Add(new FloatsConverter());
        o.Converters.Add(new IntsConverter());
        o.Converters.Add(new AnyConverter());
        o.MakeReadOnly();
        return o;
    }

    /// <summary>The message as one frame. Float and int arrays anywhere in it go to the buffers.</summary>
    public static byte[] Encode(object message) => Encode(message, contractsOnly: false);

    /// <summary>The message as one frame; <paramref name="contractsOnly"/>: written by the generated contracts alone, never by reflection.</summary>
    public static byte[] Encode(object message, bool contractsOnly)
    {
        if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("The frame format is little-endian.");
        var buffers = _current = new Buffers();
        byte[] header;
        try { header = JsonSerializer.SerializeToUtf8Bytes(message, (contractsOnly ? ContractsOnly : Options).GetTypeInfo(message.GetType())); }
        finally { _current = null; }
        var padded = (header.Length + 3) & ~3;
        var o = new byte[8 + padded + buffers.Length];
        Mark.CopyTo(o);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(4), (uint)padded);
        header.CopyTo(o, 8);
        o.AsSpan(8 + header.Length, padded - header.Length).Fill((byte)' ');
        buffers.CopyTo(o.AsSpan(8 + padded));
        return o;
    }

    /// <summary>A frame's header and buffers (for tests and tools; the interface has its own reader).</summary>
    public static (JsonDocument Header, ReadOnlyMemory<byte> Buffers) Decode(byte[] frame)
    {
        if (frame.Length < 8 || !frame.AsSpan(0, 4).SequenceEqual(Mark)) throw new InvalidDataException("Not a Lapin frame.");
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(4));
        return (JsonDocument.Parse(frame.AsMemory(8, length)), frame.AsMemory(8 + length));
    }

    public static float[] Floats(JsonElement reference, ReadOnlyMemory<byte> buffers) =>
        MemoryMarshal.Cast<byte, float>(Slice(reference, buffers, 4)).ToArray();

    public static int[] Ints(JsonElement reference, ReadOnlyMemory<byte> buffers) =>
        MemoryMarshal.Cast<byte, int>(Slice(reference, buffers, 4)).ToArray();

    /// <summary>A file's bytes in a reply: a buffer (<c>"t":"u8"</c>), or base64 text when the caller did not ask for them so.</summary>
    public static byte[] Bytes(JsonElement value, ReadOnlyMemory<byte> buffers) =>
        value.ValueKind == JsonValueKind.String ? value.GetBytesFromBase64() : Slice(value, buffers, 1).ToArray();

    static ReadOnlySpan<byte> Slice(JsonElement r, ReadOnlyMemory<byte> buffers, int size) =>
        buffers.Span.Slice(r.GetProperty("$buf").GetInt32(), r.GetProperty("n").GetInt32() * size);

    /// <summary>For a log line: the header as text.</summary>
    public static string HeaderText(byte[] frame) => Encoding.UTF8.GetString(frame, 8, (int)BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(4))).TrimEnd();
}

/// <summary>
/// A file's bytes in a reply, sent in the frame as they are (<c>{"$buf":offset,"n":count,"t":"u8"}</c>) rather than as base64
/// in its JSON (report #156): a large export no longer becomes a string a third larger, which JSON caps at 166 million
/// characters (125 MB of file) and which the browser's engine could hardly hold beside the file.
/// </summary>
[JsonConverter(typeof(BinaryFrame.FrameBytesConverter))]
public sealed class FrameBytes(ArraySegment<byte> bytes)
{
    public ArraySegment<byte> Bytes { get; } = bytes;
}
