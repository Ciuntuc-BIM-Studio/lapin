using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Protocol;
using Lapin.Scripting;

namespace Lapin.Engine.Tests;

/// <summary>The host answers every call, even one that fails in a way the engine did not foresee: the window never loses its connection (audit 15.11.2026, P20).</summary>
public sealed class HostSafetyTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-host-safety-").FullName;

    public void Dispose()
    {
        foreach (var f in Directory.GetFiles(_dir, "*", SearchOption.AllDirectories))
        {
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(f, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetAttributes(f, FileAttributes.Normal);
        }
        Directory.Delete(_dir, recursive: true);
    }

    static JsonObject Reply(byte[]? reply) => (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply!))!;

    EngineHub Hub() => new(new Fixture(new ArchitectureModule(), new ScriptingModule()).Session, PackageFiles.Create(), userDir: _dir);

    [Fact]
    public async Task A_script_saved_over_a_read_only_file_is_answered_with_why()
    {
        var hub = Hub();
        var scripts = Path.Combine(_dir, "scripts");
        Directory.CreateDirectory(scripts);
        var file = Path.Combine(scripts, "walls.py");
        File.WriteAllText(file, "print('kept')");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        var reply = Reply(await hub.Handle(new JsonObject { ["id"] = 1, ["method"] = "script.save", ["params"] = new JsonObject { ["name"] = "walls.py", ["code"] = "print('new')" } }.ToJsonString()));
        Assert.Contains("denied", (string?)reply["error"] ?? "no error", StringComparison.OrdinalIgnoreCase);
        Assert.Equal("print('kept')", File.ReadAllText(file));
    }

    [Fact]
    public async Task A_file_the_host_may_not_read_and_a_call_without_a_method_name_are_answered_and_the_next_call_goes_on()
    {
        var hub = Hub();
        if (!OperatingSystem.IsWindows())
        {
            var file = Path.Combine(_dir, "locked.lapinext");
            File.WriteAllText(file, "{}");
            File.SetUnixFileMode(file, UnixFileMode.None);
            var reply = Reply(await hub.Handle(new JsonObject { ["id"] = 1, ["method"] = "extensions.fetch", ["params"] = new JsonObject { ["url"] = file } }.ToJsonString()));
            Assert.Equal(1, (int)reply["id"]!);
            Assert.NotNull(reply["error"]);
        }

        // A method that is not a name, in a message that looks like one of the host's own.
        var odd = Reply(await hub.Handle("""{"id":2,"method":5,"params":{"note":"share."}}"""));
        Assert.Equal(2, (int)odd["id"]!);
        Assert.NotNull(odd["error"]);

        var hello = Reply(await hub.Handle(new JsonObject { ["id"] = 3, ["method"] = "engine.hello" }.ToJsonString()));
        Assert.Null(hello["error"]);
    }
}
