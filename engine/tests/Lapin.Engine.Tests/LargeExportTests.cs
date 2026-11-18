using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Drafting;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Protocol;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Report #156: a 33-bay steel hall exported to IFC in the browser gave a file of zero bytes. The IFC is 149 MB; as base64 in
/// the reply's JSON it was over JSON's cap (166 million characters) on the desktop and out of memory in the browser's engine.
/// A file now goes back as a buffer of the frame when the caller asks (`binary`), and a caller that did not ask is told why.
/// </summary>
public class LargeExportTests
{
    static JsonElement Header(byte[] frame, out ReadOnlyMemory<byte> buffers)
    {
        var (header, b) = BinaryFrame.Decode(frame);
        buffers = b;
        return header.RootElement;
    }

    [Fact]
    public void A_33_bay_hall_goes_out_as_ifc_in_the_frame_and_a_text_reply_says_it_is_too_large()
    {
        var f = new Fixture(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());
        // The reporter's hall: one span, 33 bays, its purlins, side rails and bracing (the configurator's default secondary steel).
        var bays = new JsonArray([.. Enumerable.Range(0, 33).Select(_ => (JsonNode)6.0)]);
        f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = new JsonObject { ["bays"] = bays, ["span"] = 20, ["bracing"] = new JsonArray(1, 17, 33) } });
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());

        var binary = Header(d.Handle("""{"id":1,"method":"project.exportIfc","params":{"binary":true}}""").Reply, out var buffers);
        Assert.False(binary.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null, binary.GetRawText());
        var result = binary.GetProperty("result");
        var bytes = BinaryFrame.Bytes(result.GetProperty("data"), buffers);
        // Over what base64 in JSON can carry (125 MB), and whole.
        Assert.True(bytes.Length > 125_000_000, $"{bytes.Length} bytes");
        Assert.Equal(bytes.Length, result.GetProperty("bytes").GetInt32());
        Assert.StartsWith("ISO-10303-21;", Encoding.ASCII.GetString(bytes, 0, 20));
        Assert.EndsWith("END-ISO-10303-21;\n", Encoding.ASCII.GetString(bytes, bytes.Length - 30, 30));

        // A caller asking for text (a script, the MCP bridge) is told what to do, not "Internal engine error".
        var text = Header(d.Handle("""{"id":2,"method":"project.exportIfc"}""").Reply, out _);
        var message = text.GetProperty("error").GetString()!;
        Assert.DoesNotContain("Internal", message);
        Assert.Contains("149 MB", message);
        Assert.Contains("binary", message);
    }

    [Fact]
    public void A_small_export_comes_back_the_same_as_text_or_as_bytes_and_as_the_file_written()
    {
        var f = new Fixture(new Lapin.Architecture.ArchitectureModule());
        f.Wall(0, 0, 5, 0);
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var asText = Header(d.Handle("""{"id":1,"method":"project.exportIfc"}""").Reply, out _).GetProperty("result").GetProperty("data");
        var asBytes = Header(d.Handle("""{"id":2,"method":"project.exportIfc","params":{"binary":true}}""").Reply, out var buffers).GetProperty("result").GetProperty("data");
        Assert.Equal(JsonValueKind.String, asText.ValueKind);
        Assert.Equal("u8", asBytes.GetProperty("t").GetString());
        // The same file but for the minute in its header.
        static string Body(byte[] b) => Encoding.ASCII.GetString(b)[Encoding.ASCII.GetString(b).IndexOf("DATA;", StringComparison.Ordinal)..];
        Assert.Equal(Body(BinaryFrame.Bytes(asText, default)), Body(BinaryFrame.Bytes(asBytes, buffers)));
    }

    [Fact]
    public void A_frame_carries_a_files_bytes_with_the_floats_after_them_still_aligned_and_the_same_by_contracts_alone()
    {
        var file = Encoding.ASCII.GetBytes("ISO-10303-21;\n"); // 14 bytes: what follows starts at 16
        var floats = new byte[8];
        BitConverter.TryWriteBytes(floats.AsSpan(0, 4), 1.5f);
        BitConverter.TryWriteBytes(floats.AsSpan(4, 4), 2.5f);
        static JsonNode Bytes(byte[] b) => JsonValue.Create(new FrameBytes(b), ProtocolJson.Default.FrameBytes)!;
        var reply = new Reply(7, new JsonObject { ["data"] = Bytes(file), ["p"] = Bytes(floats) });
        var frame = BinaryFrame.Encode(reply);
        Assert.Equal(frame, BinaryFrame.Encode(reply, contractsOnly: true));
        var root = Header(frame, out var buffers).GetProperty("result");
        Assert.Equal(file, BinaryFrame.Bytes(root.GetProperty("data"), buffers));
        Assert.Equal(16, root.GetProperty("p").GetProperty("$buf").GetInt32());
        Assert.Equal(1.5f, BitConverter.ToSingle(buffers.Span[16..20]));
        Assert.Equal(2.5f, BitConverter.ToSingle(buffers.Span[20..24]));
        // Outside a frame (a log line), the bytes are base64.
        Assert.Contains(Convert.ToBase64String(file), new JsonObject { ["data"] = JsonValue.Create(new FrameBytes(file), ProtocolJson.Default.FrameBytes) }.ToJsonString());
    }

    [Fact]
    public void A_step_file_kept_as_utf8_chunks_writes_whole_lines_the_same_into_a_stream_or_an_array()
    {
        var step = new Step();
        // More than one chunk (a megabyte each), with characters escaped and lines of every length.
        for (var i = 0; i < 40_000; i++) step.Add("IFCPROPERTYSINGLEVALUE", Step.S($"Wärme {i} 熱"), Step.Null, Step.Typed("IFCLABEL", Step.S(new string('x', i % 97))), Step.Null);
        Assert.True(step.DataLength > 2 << 20);
        using var ms = new MemoryStream();
        step.Write(ms, "a.ifc", "ViewDefinition [ReferenceView_V1.2]");
        var array = step.ToArray("a.ifc", "ViewDefinition [ReferenceView_V1.2]");
        var a = Encoding.UTF8.GetString(ms.ToArray());
        var b = Encoding.UTF8.GetString(array);
        // The header's time may differ by a second.
        Assert.Equal(a[a.IndexOf("DATA;", StringComparison.Ordinal)..], b[b.IndexOf("DATA;", StringComparison.Ordinal)..]);
        var lines = b[(b.IndexOf("DATA;\n", StringComparison.Ordinal) + 6)..b.IndexOf("ENDSEC;\nEND", StringComparison.Ordinal)].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(40_000, lines.Length);
        Assert.All(lines, l => Assert.Matches(@"^#\d+=IFCPROPERTYSINGLEVALUE\('W\\X2\\00E4\\X0\\rme \d+ \\X2\\71B1\\X0\\',\$,IFCLABEL\('x*'\),\$\);$", l));
    }
}
