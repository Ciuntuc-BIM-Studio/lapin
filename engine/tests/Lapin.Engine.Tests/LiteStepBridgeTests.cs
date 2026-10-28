using System.Text.Json;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

public class LiteStepBridgeTests
{
    [Fact]
    public async Task Schema_returns_parameters_and_dag()
    {
        var f = new Fixture();
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var frame = await hub.Handle("""{"id":101,"method":"doc.execute","params":{"command":"litestep.schema"}}""");
        var (header, _) = BinaryFrame.Decode(frame);
        var res = header.RootElement.GetProperty("result");
        Assert.True(res.TryGetProperty("parameters", out var p));
        Assert.True(res.TryGetProperty("dag", out var d));
        Assert.True(p.GetArrayLength() >= 5);
        Assert.True(d.GetProperty("nodes").GetArrayLength() >= 9);
    }

    [Fact]
    public async Task GetParams_returns_current_parameters()
    {
        var f = new Fixture();
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var frame = await hub.Handle("""{"id":102,"method":"doc.execute","params":{"command":"litestep.getParams"}}""");
        var (header, _) = BinaryFrame.Decode(frame);
        var res = header.RootElement.GetProperty("result");
        Assert.True(res.TryGetProperty("CABIN_LENGTH", out var l));
        Assert.True(l.GetDouble() > 0);
    }

    [Fact]
    public async Task Recompute_imports_model_into_session()
    {
        var f = new Fixture();
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var frame = await hub.Handle("""{"id":103,"method":"doc.execute","params":{"command":"litestep.recompute","args":{"params":{"CABIN_LENGTH":6000.0,"CABIN_WIDTH":4000.0,"NUM_LOGS_PER_WALL":7,"ROOF_PITCH":35.0,"RANDOM_SEED":42}}}}""");
        var (header, _) = BinaryFrame.Decode(frame);
        var root = header.RootElement;
        Assert.False(root.TryGetProperty("error", out var err), err.ValueKind == JsonValueKind.String ? err.GetString() : "error");
        var res = root.GetProperty("result");
        var elements = res.GetProperty("elements").GetInt32();
        var references = res.GetProperty("references").GetInt32();
        Assert.True(elements + references > 0);

        // Verify elements exist in model
        var entities = f.Session.Model.Entities.ToList();
        Assert.NotEmpty(entities);
        // StoredBodies elements (the logs) should be present
        var storedBodies = entities.Where(e => StoredBodies.Has(e)).ToList();
        Assert.NotEmpty(storedBodies);
    }
}
