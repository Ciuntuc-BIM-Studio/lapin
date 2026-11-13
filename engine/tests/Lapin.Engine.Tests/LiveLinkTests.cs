using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Scripting;

namespace Lapin.Engine.Tests;

/// <summary>
/// What an external program keeps live (docs/plan-external-geometry.md, L2–L3): the selection told with what it is,
/// measures recomputed when what they read changes, a connection's watches, a project script's watch kept in the project.
/// </summary>
public sealed class LiveLinkTests
{
    sealed class Recorder : EngineHub.Client
    {
        public readonly List<string> Got = [];
        public override Task SendBinary(byte[] frame) { lock (Got) Got.Add(BinaryFrame.HeaderText(frame)); return Task.CompletedTask; }
        public override Task SendText(string text) { lock (Got) Got.Add(text); return Task.CompletedTask; }
        protected override void Drop() { }

        /// <summary>The events of a name it got, their data, oldest first.</summary>
        public List<JsonNode> Events(string name)
        {
            lock (Got) return [.. Got.Select(t => JsonNode.Parse(t)!).Where(n => (string?)n["event"] == name).Select(n => n["data"]!)];
        }

        public void Clear() { lock (Got) Got.Clear(); }
    }

    static (Fixture F, EngineHub Hub, Recorder A, Recorder B) Make()
    {
        var f = new Fixture(new ArchitectureModule(), new ScriptingModule());
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var (a, b) = (new Recorder(), new Recorder());
        hub.Add(a);
        hub.Add(b);
        return (f, hub, a, b);
    }

    static int _id;

    /// <summary>A call from <paramref name="c"/>: its result, or the error it was answered with as an exception.</summary>
    static async Task<JsonNode?> Call(EngineHub hub, EngineHub.Client c, string method, JsonObject p)
    {
        var frame = await hub.Handle(new JsonObject { ["id"] = ++_id, ["method"] = method, ["params"] = p }.ToJsonString(), c);
        await c.Flushed();
        var h = JsonNode.Parse(BinaryFrame.HeaderText(frame!))!;
        if ((string?)h["error"] is { } error) throw new CommandException(error);
        return h["result"];
    }

    static Task<JsonNode?> Exec(EngineHub hub, EngineHub.Client c, string command, JsonObject args) =>
        Call(hub, c, "doc.execute", new JsonObject { ["command"] = command, ["args"] = args });

    [Fact]
    public async Task A_measure_is_told_when_what_it_reads_changes_and_only_then()
    {
        var (f, hub, a, b) = Make();
        var wall = f.Wall(0, 0, 5, 0, height: 3);
        var v = await Call(hub, a, "measure.define", new JsonObject { ["name"] = $"wall.{wall}.top" });
        Assert.Equal(3, (double)v!["value"]!, 6);
        await Call(hub, a, "measure.define", new JsonObject { ["name"] = "twice", ["id"] = wall, ["expr"] = "attr.height * 2" });
        await Call(hub, a, "measure.define", new JsonObject { ["name"] = $"storey.{f.Model.Get(f.Storey)!.Name}.elevation" });
        var bbox = await Call(hub, a, "measure.get", new JsonObject { ["name"] = $"element.{wall}.bbox" });
        Assert.Equal(5, (double)bbox!["value"]![1]![0]!, 6);
        a.Clear();

        // Another wall: nothing the measures read.
        f.Wall(0, 4, 5, 4, height: 3);
        await Exec(hub, b, "architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 8, ["x2"] = 5, ["y2"] = 8, ["height"] = 3 });
        Assert.Empty(a.Events("measure"));

        await Exec(hub, b, "element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["height"] = 3.5 } });
        await a.Flushed();
        var told = a.Events("measure").ToDictionary(e => (string)e["name"]!);
        Assert.Equal(3.5, (double)told[$"wall.{wall}.top"]["value"]!, 6);
        Assert.Equal(3, (double)told[$"wall.{wall}.top"]["was"]!, 6);
        Assert.Equal(7, (double)told["twice"]["value"]!, 6);
        Assert.Equal(2, told.Count);
        // The other connection defined none: it is told none.
        Assert.Empty(b.Events("measure"));

        a.Clear();
        await Exec(hub, b, "storey.update", new JsonObject { ["id"] = f.Storey, ["elevation"] = 0.5 });
        await a.Flushed();
        Assert.Contains(a.Events("measure"), e => ((string)e["name"]!).StartsWith("storey.") && (double)e["value"]! == 0.5);

        var list = await Call(hub, a, "measure.list", []);
        Assert.Equal(3, list!["measures"]!.AsArray().Count);
        await Call(hub, a, "measure.remove", new JsonObject { ["name"] = "twice" });
        await Assert.ThrowsAsync<CommandException>(() => Call(hub, a, "measure.get", new JsonObject { ["name"] = "twice" }));
        await Assert.ThrowsAsync<CommandException>(() => Call(hub, a, "measure.define", new JsonObject { ["name"] = "nothing.known" }));
        // A query measure: its result is the value.
        var q = await Call(hub, a, "measure.define", new JsonObject { ["name"] = "deps", ["query"] = "core.dependents", ["args"] = new JsonObject { ["id"] = wall } });
        Assert.NotNull(q!["value"]!["dependents"]);
    }

    [Fact]
    public async Task A_watch_is_told_of_anothers_change_never_of_its_own()
    {
        var (f, hub, a, b) = Make();
        var wall = f.Wall(0, 0, 5, 0, height: 3);
        var w = await Call(hub, a, "script.watch", new JsonObject { ["ids"] = new JsonArray(wall), ["measures"] = new JsonArray($"wall.{wall}.top") });
        var watchId = (string)w!["watch"]!;
        a.Clear();

        await Exec(hub, a, "element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["height"] = 4 } });
        await a.Flushed();
        Assert.Empty(a.Events("watch"));

        await Exec(hub, b, "element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["height"] = 4.5 } });
        await a.Flushed();
        var told = Assert.Single(a.Events("watch"));
        Assert.Equal(watchId, (string)told["watch"]!);
        Assert.Equal(wall, (string)told["ids"]![0]!);
        Assert.Equal(4.5, (double)told["measures"]![$"wall.{wall}.top"]!, 6);

        // Undo is a change too; once unwatched, nothing.
        a.Clear();
        await Call(hub, b, "doc.undo", []);
        await a.Flushed();
        Assert.Single(a.Events("watch"));
        await Call(hub, a, "script.unwatch", new JsonObject { ["watch"] = watchId });
        a.Clear();
        await Call(hub, b, "doc.redo", []);
        await a.Flushed();
        Assert.Empty(a.Events("watch"));
        await Assert.ThrowsAsync<CommandException>(() => Call(hub, a, "script.watch", new JsonObject { ["ids"] = new JsonArray("nothing") }));
        // Gone with its connection.
        await Call(hub, a, "script.watch", new JsonObject { ["ids"] = new JsonArray(wall) });
        await hub.Remove(a);
        Assert.Empty(((JsonObject)hub.Live.List(a))["watches"]!.AsArray());
    }

    [Fact]
    public async Task The_selection_is_told_with_what_it_is()
    {
        var (f, hub, a, _) = Make();
        var wall = f.Wall(0, 0, 5, 0, height: 3);
        await hub.Handle(new JsonObject { ["method"] = "script.selected", ["params"] = new JsonObject { ["ids"] = new JsonArray(wall) } }.ToJsonString(), a);
        for (var i = 0; i < 200 && a.Events("selection").Count == 0; i++) await Task.Delay(10);
        var sel = Assert.Single(a.Events("selection"));
        var item = sel["items"]![0]!;
        Assert.Equal(wall, (string)item["globalId"]!);
        Assert.Equal("IfcWall", (string)item["ifcClass"]!);
        Assert.Equal(f.Storey, (string)item["storey"]!);
        Assert.Equal(wall, (string)item["path"]!.AsArray()[^1]!);
        Assert.Equal(3, (double)item["bbox"]![1]![2]!, 6);
        var details = await Call(hub, a, "script.selection", new JsonObject { ["details"] = true });
        Assert.Equal("IfcWall", (string)details!["items"]![0]!["ifcClass"]!);
    }

    [Fact]
    public async Task A_project_scripts_watch_is_kept_in_the_project_and_an_untrusted_script_is_not_run()
    {
        var (f, hub, a, b) = Make();
        EngineHub.RegenerateAfter = TimeSpan.FromMilliseconds(20);
        var wall = f.Wall(0, 0, 5, 0, height: 3);
        var script = (string)f.Run("scripting.save", new JsonObject { ["name"] = "truss.py", ["code"] = "print('again')\n" })!["id"]!;
        await Call(hub, a, "script.watch", new JsonObject { ["script"] = "truss.py", ["ids"] = new JsonArray(wall), ["measures"] = new JsonArray($"wall.{wall}.top") });
        Assert.Equal("Watch for script", f.Session.History.UndoLabel);
        var kept = f.Model.Get(script)!.Attrs[ScriptingModule.WatchKey]!;
        Assert.Equal(wall, (string)kept["ids"]![0]!);
        a.Clear();

        await Exec(hub, b, "element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["height"] = 4 } });
        JsonNode? note = null;
        for (var i = 0; i < 300 && note == null; i++)
        {
            note = a.Events("script.output").FirstOrDefault(e => (string?)e["type"] == "note");
            if (note == null) await Task.Delay(10);
        }
        Assert.NotNull(note);
        Assert.Contains("not trusted", (string)note!["text"]!);
        // The watch undone with the project's other steps; unwatched, the script keeps no watch.
        await Call(hub, a, "script.unwatch", new JsonObject { ["script"] = script });
        Assert.Null(f.Model.Get(script)!.Attrs[ScriptingModule.WatchKey]);
    }
}
