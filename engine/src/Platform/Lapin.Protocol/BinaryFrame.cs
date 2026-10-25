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
/// buffers           every float[] / int[] of the message, back to back, each 4-aligned
/// </code>
/// In the header an array is a reference: <c>{"$buf":offset,"n":count,"t":"f32"|"i32"}</c>,
/// its offset counted from the start of the buffers. The interface reads them
/// as Float32Array / Int32Array views, without copying.
/// </summary>
public static class BinaryFrame
{
    public static ReadOnlySpan<byte> Mark => "LPF1"u8;

    sealed class Buffers
    {
        public readonly MemoryStream Data = new();
        public long Add(ReadOnlySpan<byte> bytes)
        {
            var at = Data.Length;
            Data.Write(bytes);
            return at;
        }
    }

    /// <summary>The buffers of the frame being encoded on this thread (the converters write into them).</summary>
    [ThreadStatic] static Buffers? _current;

    sealed class FloatsConverter : JsonConverter<float[]>
    {
        public override float[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter w, float[] value, JsonSerializerOptions options) => Ref(w, _current!.Add(MemoryMarshal.AsBytes(value.AsSpan())), value.Length, "f32");
    }

    sealed class IntsConverter : JsonConverter<int[]>
    {
        public override int[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter w, int[] value, JsonSerializerOptions options) => Ref(w, _current!.Add(MemoryMarshal.AsBytes(value.AsSpan())), value.Length, "i32");
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
        var o = new byte[8 + padded + buffers.Data.Length];
        Mark.CopyTo(o);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(4), (uint)padded);
        header.CopyTo(o, 8);
        o.AsSpan(8 + header.Length, padded - header.Length).Fill((byte)' ');
        buffers.Data.GetBuffer().AsSpan(0, (int)buffers.Data.Length).CopyTo(o.AsSpan(8 + padded));
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

    static ReadOnlySpan<byte> Slice(JsonElement r, ReadOnlyMemory<byte> buffers, int size) =>
        buffers.Span.Slice(r.GetProperty("$buf").GetInt32(), r.GetProperty("n").GetInt32() * size);

    /// <summary>For a log line: the header as text.</summary>
    public static string HeaderText(byte[] frame) => Encoding.UTF8.GetString(frame, 8, (int)BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(4))).TrimEnd();
}
