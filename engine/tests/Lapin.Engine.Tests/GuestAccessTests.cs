using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>A shared session's guest and the host's disk, and the dispatcher's replies to a guest.</summary>
public sealed class GuestAccessTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-guest-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static readonly Caller Guest = new("p1", "editor");

    static JsonObject Reply(EngineDispatcher d, string method, JsonObject p, Caller caller)
    {
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString(), caller);
        return JsonNode.Parse(BinaryFrame.HeaderText(reply))!.AsObject();
    }

    [Fact]
    public void A_path_anywhere_in_a_guests_call_is_refused()
    {
        foreach (var (method, p) in new (string, JsonObject)[]
        {
            ("doc.query", new JsonObject { ["name"] = "drafting.dxfInfo", ["args"] = new JsonObject { ["path"] = "/etc/hosts" } }),
            ("doc.execute", new JsonObject { ["command"] = "edit.batch", ["args"] = new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["command"] = "drafting.importDxf", ["args"] = new JsonObject { ["path"] = "/etc/hosts" } }) } }),
            ("doc.execute", new JsonObject { ["command"] = "cde.setFolder", ["args"] = new JsonObject { ["path"] = "/Users" } }),
            ("doc.execute", new JsonObject { ["command"] = "element.update", ["args"] = new JsonObject { ["id"] = "x", ["attrs"] = new JsonObject { ["folder"] = "/tmp" } } }),
        })
            Assert.Equal(HostFiles.Refused, EngineDispatcher.AllowedRemote(method, p, "editor"));
        // What only looks like one goes through: a direction as numbers, an empty path, a polyline's points.
        Assert.Null(EngineDispatcher.AllowedRemote("doc.execute", new JsonObject
        {
            ["command"] = "drafting.addDimension",
            ["args"] = new JsonObject { ["dir"] = new JsonArray(1, 0), ["path"] = new JsonArray(new JsonArray(0, 0), new JsonArray(1, 1)), ["folder"] = "" },
        }, "editor"));
    }

    [Fact]
    public void A_file_reader_refuses_a_guests_call_even_past_the_protocol()
    {
        var path = Path.Combine(_dir, "a.dxf");
        File.WriteAllText(path, "0\nEOF\n");
        HostFiles.GuestCall = true;
        try { Assert.Throws<CommandException>(() => DxfImport.Load(new JsonObject { ["path"] = path })); }
        finally { HostFiles.GuestCall = false; }
        // The host's own call reads it.
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var local = Reply(d, "doc.query", new JsonObject { ["name"] = "drafting.dxfInfo", ["args"] = new JsonObject { ["path"] = path } }, Caller.Local);
        Assert.Null((string?)local["error"]);
        var remote = Reply(d, "doc.query", new JsonObject { ["name"] = "drafting.dxfInfo", ["args"] = new JsonObject { ["path"] = path } }, Guest);
        Assert.Equal(HostFiles.Refused, (string?)remote["error"]);
        Assert.False(HostFiles.GuestCall);
    }

    [Fact]
    public void A_guests_snapshot_carries_their_own_undo()
    {
        var f = new Fixture();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        f.Wall(0, 0, 5, 0);
        Assert.True(f.Session.History.CanUndo);
        var mine = Reply(d, "doc.snapshot", [], Caller.Local);
        Assert.True((bool)mine["result"]!["state"]!["canUndo"]!);
        var theirs = Reply(d, "doc.snapshot", [], Guest);
        Assert.False((bool)theirs["result"]!["state"]!["canUndo"]!);
        Assert.Null((string?)theirs["result"]!["state"]!["undoLabel"]);
    }

    /// <summary>A module with a bug: its command runs First() on nothing.</summary>
    sealed class BuggyModule : ILapinModule
    {
        public string Id => "buggy";
        public string Name => "Buggy";
        public void Register(IModuleRegistry r) => r.Command("first", "First", (ctx, a) => new JsonObject { ["n"] = Array.Empty<int>().First() });
    }

    [Fact]
    public void An_engine_bug_is_reported_as_one_not_as_a_refusal()
    {
        var f = new Fixture(new ArchitectureModule(), new BuggyModule());
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var was = Console.Error;
        var log = new StringWriter();
        Console.SetError(log);
        JsonObject bug;
        try { bug = Reply(d, "doc.execute", new JsonObject { ["command"] = "buggy.first", ["args"] = new JsonObject() }, Caller.Local); }
        finally { Console.SetError(was); }
        Assert.StartsWith("Internal engine error", (string?)bug["error"]);
        Assert.Contains("InvalidOperationException", log.ToString());
        // A refusal the user reads stays one.
        var refused = Reply(d, "doc.execute", new JsonObject { ["command"] = "no.such" }, Caller.Local);
        Assert.DoesNotContain("Internal", (string?)refused["error"]);
    }
}
