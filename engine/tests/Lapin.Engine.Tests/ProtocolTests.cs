using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Host;

namespace Lapin.Engine.Tests;

/// <summary>The wire: the hub answers in binary frames whose meshes are raw arrays, referenced from the JSON header.</summary>
public class ProtocolTests
{
    [Fact]
    public async Task A_reply_is_a_frame_with_its_meshes_as_buffers()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0);
        var expected = f.Session.Snapshot().Render.Single(r => r.NodeId == wall);
        var hub = new EngineHub(f.Session, PackageFiles.Create());

        var frame = await hub.Handle("""{"id":7,"method":"doc.snapshot"}""");
        var (header, buffers) = BinaryFrame.Decode(frame);
        var root = header.RootElement;
        Assert.Equal(7, root.GetProperty("id").GetInt32());
        var item = root.GetProperty("result").GetProperty("render").EnumerateArray().Single(r => r.GetProperty("nodeId").GetString() == wall);
        var parts = item.GetProperty("parts").EnumerateArray().ToList();
        Assert.Equal(expected.Parts!.Count, parts.Count);
        for (var i = 0; i < parts.Count; i++)
        {
            var positions = parts[i].GetProperty("positions");
            Assert.Equal("f32", positions.GetProperty("t").GetString());
            Assert.Equal(0, positions.GetProperty("$buf").GetInt32() % 4);
            Assert.Equal(expected.Parts[i].Positions, BinaryFrame.Floats(positions, buffers));
            Assert.Equal(expected.Parts[i].Indices, BinaryFrame.Ints(parts[i].GetProperty("indices"), buffers));
            Assert.Equal(expected.Parts[i].Normals, BinaryFrame.Floats(parts[i].GetProperty("normals"), buffers));
        }
        // The header is padded to 4 bytes, so every buffer view is aligned in the frame too.
        Assert.Equal(0, (frame.Length - buffers.Length) % 4);
    }

    [Fact]
    public async Task An_error_comes_back_with_its_call_id()
    {
        var hub = new EngineHub(new Fixture().Session, PackageFiles.Create());
        var (header, _) = BinaryFrame.Decode(await hub.Handle("""{"id":3,"method":"doc.execute","params":{"command":"architecture.flipWall","args":{"id":"nope"}}}"""));
        Assert.Equal(3, header.RootElement.GetProperty("id").GetInt32());
        Assert.Contains("no IfcWall", header.RootElement.GetProperty("error").GetString());
        (header, _) = BinaryFrame.Decode(await hub.Handle("""{"id":4,"method":"nothing"}"""));
        Assert.Contains("Unknown method", header.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void Plain_numbers_stay_json_only_arrays_of_floats_and_ints_become_buffers()
    {
        var frame = BinaryFrame.Encode(new { a = 1.5, list = new[] { 1.0, 2.0 }, floats = new[] { 1f, 2f, 3f }, ints = new[] { 4, 5 } });
        var (header, buffers) = BinaryFrame.Decode(frame);
        var r = header.RootElement;
        Assert.Equal(1.5, r.GetProperty("a").GetDouble());
        Assert.Equal(JsonValueKind.Array, r.GetProperty("list").ValueKind);
        Assert.Equal([1f, 2f, 3f], BinaryFrame.Floats(r.GetProperty("floats"), buffers));
        Assert.Equal([4, 5], BinaryFrame.Ints(r.GetProperty("ints"), buffers));
        Assert.Equal(12, r.GetProperty("ints").GetProperty("$buf").GetInt32());
        _ = JsonNode.Parse(BinaryFrame.HeaderText(frame));
    }
}
