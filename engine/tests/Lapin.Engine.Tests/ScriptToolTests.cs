using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Scripting;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage 4 of docs/plan-python-scripting.md (decision 489): a guest of a shared session never deletes, renames, groups,
/// moves or copies a project's script through the generic commands, alone or in a batch (the user's answer of 12.11.2026);
/// <c>lapin.ui.ask</c>'s questions go from a script's connection to this computer's windows and come back with the first
/// window's answer (or nothing, when it is cancelled, the script stopped or its connection lost); the user's scripts are
/// listed with their header's lines; a script's tool run passes what it calls to the kernel.
/// </summary>
public sealed class ScriptToolTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-script-tools-").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    static Fixture Project()
    {
        var f = new Fixture(new ArchitectureModule(), new ScriptingModule());
        f.Run("storey.create", []);
        return f;
    }

    static string SaveScript(Fixture f, string name = "walls.py") =>
        (string)f.Run("scripting.save", new JsonObject { ["name"] = name, ["code"] = "print('hi')\n" })!["id"]!;

    static JsonObject Exec(string command, JsonObject args) => new() { ["command"] = command, ["args"] = args };

    [Fact]
    public void A_guest_never_deletes_renames_groups_moves_or_copies_a_projects_script()
    {
        var f = Project();
        var script = SaveScript(f);
        var other = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0 })!["id"]!;
        var wall = f.Wall(0, 0, 5, 0);
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var guest = new Caller("guest-1", "editor");
        string? Error(JsonObject p)
        {
            var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = "doc.execute", ["params"] = p }.ToJsonString(), guest);
            var h = BinaryFrame.Decode(reply).Header.RootElement;
            return h.TryGetProperty("error", out var e) ? e.GetString() : null;
        }
        var refused = new[]
        {
            Exec("element.delete", new JsonObject { ["ids"] = new JsonArray(script) }),
            Exec("element.delete", new JsonObject { ["ids"] = new JsonArray(wall, script) }),   // with an element of the guest's own: all or nothing
            Exec("element.rename", new JsonObject { ["id"] = script, ["name"] = "x.py" }),
            Exec("element.update", new JsonObject { ["id"] = script, ["attrs"] = new JsonObject { ["description"] = "x" } }),
            Exec("group.create", new JsonObject { ["ids"] = new JsonArray(script, wall) }),
            Exec("element.copyToStoreys", new JsonObject { ["ids"] = new JsonArray(script), ["storeyIds"] = new JsonArray(other) }),
            Exec("element.moveToStorey", new JsonObject { ["ids"] = new JsonArray(script), ["storeyId"] = other }),
            Exec("element.transform", new JsonObject { ["ids"] = new JsonArray(script), ["op"] = "copy", ["by"] = new JsonArray(1.0, 0.0) }),
            Exec("edit.batch", new JsonObject { ["steps"] = new JsonArray(Exec("element.delete", new JsonObject { ["ids"] = new JsonArray(script) })) }),
            Exec("edit.batch", new JsonObject { ["steps"] = new JsonArray(
                Exec("element.rename", new JsonObject { ["id"] = wall, ["name"] = "South" }),
                Exec("element.delete", new JsonObject { ["ids"] = new JsonArray(script) })) }),
        };
        foreach (var p in refused) Assert.Equal("Only the host can do that.", Error(p));
        Assert.Single(f.Model.OfClass(ScriptingModule.Class));
        Assert.NotNull(f.Model.Get(wall));
        Assert.Equal("walls.py", f.Model.Get(script)!.Name);
        // The guest's own work goes on.
        Assert.Null(Error(Exec("element.delete", new JsonObject { ["ids"] = new JsonArray(wall) })));
        Assert.Null(f.Model.Get(wall));

        // The host deletes it like any content, one undo step; Undo brings it back.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(script) });
        Assert.Empty(f.Model.OfClass(ScriptingModule.Class));
        f.Session.Undo();
        Assert.Single(f.Model.OfClass(ScriptingModule.Class));
    }

    // ------------------------------------------------------------------ lapin.ui.ask through the host

    sealed class Recorder : EngineHub.Client
    {
        public readonly List<string> Got = [];
        public override Task SendBinary(byte[] frame) { lock (Got) Got.Add(BinaryFrame.HeaderText(frame)); return Task.CompletedTask; }
        public override Task SendText(string text) { lock (Got) Got.Add(text); return Task.CompletedTask; }
        protected override void Drop() { }

        public JsonNode[] All() { lock (Got) return [.. Got.Select(t => JsonNode.Parse(t)!)]; }

        /// <summary>The first of what it got that holds, waited for (the hub posts on its own pace).</summary>
        public async Task<JsonNode> Wait(Func<JsonNode, bool> holds, double seconds = 10)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < until)
            {
                await Flushed();
                if (All().FirstOrDefault(holds) is { } n) return n;
                await Task.Delay(10);
            }
            throw new TimeoutException("Nothing came.");
        }
    }

    static string Call(int id, string method, JsonObject? p = null) => new JsonObject { ["id"] = id, ["method"] = method, ["params"] = p ?? [] }.ToJsonString();

    static string? Error(byte[]? frame) => BinaryFrame.Decode(frame!).Header.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;

    static JsonObject Question() => new()
    {
        ["title"] = "Perimeter walls",
        ["fields"] = new JsonArray(
            new JsonObject { ["key"] = "width", ["kind"] = "number", ["label"] = "Width", ["value"] = 8.0, ["unit"] = "m" },
            new JsonObject { ["key"] = "closed", ["kind"] = "bool", ["value"] = true }),
    };

    static bool IsAsk(JsonNode n) => (string?)n["event"] == "script.output" && (string?)n["data"]?["type"] == "ask";
    static bool IsReply(JsonNode n, int id) => (int?)n["id"] == id;

    [Fact]
    public async Task A_question_goes_to_the_windows_and_the_first_answer_comes_back_to_the_script()
    {
        var f = Project();
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var script = new Recorder();
        var window = new Recorder();
        var second = new Recorder();
        foreach (var c in new[] { script, window, second }) hub.Add(c);

        // Asked: no answer yet (the script's connection is not held: it may still call), the windows get the question.
        Assert.Null(await hub.Handle(Call(1, "script.ask", Question()), script));
        var ask = (await window.Wait(IsAsk))["data"]!;
        Assert.Equal("Perimeter walls", (string?)ask["title"]);
        Assert.Equal(2, ask["fields"]!.AsArray().Count);
        await second.Wait(IsAsk);
        Assert.Null(Error(await hub.Handle(Call(2, "doc.snapshot"), script)));
        Assert.DoesNotContain(script.All(), n => IsReply(n, 1));

        var key = (string)ask["ask"]!;
        var answered = await hub.Handle(Call(3, "script.answer", new JsonObject { ["ask"] = key, ["values"] = new JsonObject { ["width"] = 10.5, ["closed"] = false } }), window);
        Assert.True(JsonNode.Parse(BinaryFrame.HeaderText(answered!))!["result"]!["answered"]!.GetValue<bool>());
        var reply = await script.Wait(n => IsReply(n, 1));
        Assert.Equal(10.5, reply["result"]!["values"]!["width"]!.GetValue<double>());
        Assert.False(reply["result"]!["values"]!["closed"]!.GetValue<bool>());
        // The other window's dialog closes; its late answer finds nothing to answer.
        await second.Wait(n => (string?)n["data"]?["type"] == "asked" && (string?)n["data"]?["ask"] == key);
        var late = await hub.Handle(Call(4, "script.answer", new JsonObject { ["ask"] = key, ["cancel"] = true }), second);
        Assert.False(JsonNode.Parse(BinaryFrame.HeaderText(late!))!["result"]!["answered"]!.GetValue<bool>());
    }

    [Fact]
    public async Task A_cancelled_question_answers_cancelled_and_a_lost_script_takes_its_question_away()
    {
        var f = Project();
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var script = new Recorder();
        var window = new Recorder();
        hub.Add(script);
        hub.Add(window);
        await hub.Handle(Call(1, "script.ask", Question()), script);
        var key = (string)(await window.Wait(IsAsk))["data"]!["ask"]!;
        await hub.Handle(Call(2, "script.answer", new JsonObject { ["ask"] = key, ["cancel"] = true }), window);
        var reply = await script.Wait(n => IsReply(n, 1));
        Assert.True(reply["result"]!["cancelled"]!.GetValue<bool>());

        // Asked again, then the script's connection goes (a terminal script closed): the windows close the dialog.
        await hub.Handle(Call(3, "script.ask", Question()), script);
        var again = (string)(await window.Wait(n => IsAsk(n) && (string?)n["data"]!["ask"] != key))["data"]!["ask"]!;
        await hub.Remove(script);
        await window.Wait(n => (string?)n["data"]?["type"] == "asked" && (string?)n["data"]?["ask"] == again);
        Assert.Empty(hub.Scripts.Asking);
    }

    [Fact]
    public async Task A_guest_cannot_ask_nor_answer_and_a_question_is_checked()
    {
        var f = Project();
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var guest = new Recorder { Caller = new Caller("guest-1", "editor") };
        var script = new Recorder();
        hub.Add(guest);
        hub.Add(script);
        Assert.Equal("Only the host can do that.", Error(await hub.Handle(Call(1, "script.ask", Question()), guest)));
        Assert.Equal("Only the host can do that.", Error(await hub.Handle(Call(2, "script.answer", new JsonObject { ["ask"] = "a", ["cancel"] = true }), guest)));
        // The guest's window never sees the host's questions.
        await hub.Handle(Call(3, "script.ask", Question()), script);
        await guest.Flushed();
        Assert.DoesNotContain(guest.All(), IsAsk);

        // No fields, too many, an unknown kind, a key twice: refused at once.
        var bad = new[]
        {
            new JsonObject { ["title"] = "x", ["fields"] = new JsonArray() },
            new JsonObject { ["title"] = "x", ["fields"] = new JsonArray(Enumerable.Range(0, 65).Select(i => (JsonNode)new JsonObject { ["key"] = $"k{i}", ["kind"] = "text" }).ToArray()) },
            new JsonObject { ["title"] = "x", ["fields"] = new JsonArray(new JsonObject { ["key"] = "a", ["kind"] = "html" }) },
            new JsonObject { ["title"] = "x", ["fields"] = new JsonArray(new JsonObject { ["key"] = "a", ["kind"] = "text" }, new JsonObject { ["key"] = "a", ["kind"] = "number" }) },
        };
        var id = 10;
        foreach (var p in bad) Assert.NotNull(Error(await hub.Handle(Call(id++, "script.ask", p), script)));
    }

    [Fact]
    public async Task Stop_cancels_the_kernels_questions_but_not_another_scripts()
    {
        var hub = new EngineHub(Project().Session, PackageFiles.Create());
        var token = hub.Scripts.NewKernelToken();
        var kernel = new Recorder();
        var terminal = new Recorder();
        var window = new Recorder();
        foreach (var c in new[] { kernel, terminal, window }) hub.Add(c);
        await hub.Handle(Call(1, "script.join", new JsonObject { ["token"] = token }), kernel);
        await hub.Handle(Call(2, "script.ask", Question()), kernel);
        await hub.Handle(Call(3, "script.ask", Question()), terminal);
        Assert.Equal(2, hub.Scripts.Asking.Count);
        hub.Scripts.CancelKernelAsks();
        var reply = await kernel.Wait(n => IsReply(n, 2));
        Assert.True(reply["result"]!["cancelled"]!.GetValue<bool>());
        Assert.True(reply["result"]!["stopped"]!.GetValue<bool>());   // lapinbim stops the script on it, never goes on as if cancelled
        Assert.Single(hub.Scripts.Asking);
        Assert.DoesNotContain(terminal.All(), n => IsReply(n, 3));
    }

    // ------------------------------------------------------------------ the user's scripts' headers, trust taken back, a tool's run

    [Fact]
    public async Task Mine_lists_each_scripts_header_lines_and_trust_can_be_taken_back()
    {
        var host = new ScriptHost(true, _dir, _ => Task.CompletedTask);
        var code = "# lapin: name=\"Perimeter walls\", tool=\"Perimeter walls\", icon=\"square\", run=\"perimeter\"\n# SPDX-License-Identifier: MIT\n\ndef perimeter():\n    pass\n" + new string('#', 20_000);
        await host.Handle("script.save", new JsonObject { ["name"] = "walls.py", ["code"] = code });
        var file = ((JsonObject)(await host.Handle("script.files", []))!)["files"]!.AsArray().Single()!;
        var head = (string)file["head"]!;
        Assert.StartsWith("# lapin: name=\"Perimeter walls\"", head);
        Assert.True(head.Length <= ScriptHost.HeadLength);
        Assert.Equal(ScriptHost.HashOf(code), (string?)file["sha256"]);

        var sha = ScriptHost.HashOf(code);
        await host.Handle("script.trust", new JsonObject { ["sha256"] = sha });
        Assert.True((bool)((JsonObject)(await host.Handle("script.trusted", new JsonObject { ["sha256"] = sha }))!)["trusted"]!);
        await host.Handle("script.untrust", new JsonObject { ["sha256"] = sha });
        Assert.False((bool)((JsonObject)(await host.Handle("script.trusted", new JsonObject { ["sha256"] = sha }))!)["trusted"]!);
    }

    [Fact]
    public void A_tools_run_says_what_it_calls_and_nothing_else()
    {
        Assert.Equal("""{"name":"Perimeter walls","call":"perimeter","main":false}""", ScriptHost.ToolOf(JsonNode.Parse("""{"name":" Perimeter walls ","call":"perimeter","extra":1}""") as JsonObject)!.ToJsonString());
        Assert.Equal("""{"name":"Walls","call":null,"main":true}""", ScriptHost.ToolOf(JsonNode.Parse("""{"name":"Walls","main":true}""") as JsonObject)!.ToJsonString());
        Assert.Null(ScriptHost.ToolOf(null));
        foreach (var bad in new[] { """{"name":""}""", """{"name":"x","call":"os.system"}""", """{"name":"x","call":"1a"}""", """{"call":"f"}""" })
            Assert.Throws<CommandException>(() => ScriptHost.ToolOf(JsonNode.Parse(bad) as JsonObject));
    }

    [Fact]
    public void A_palettes_button_carries_its_values_within_limits()
    {
        Assert.Equal("""{"name":"Make","call":"make","main":false,"palette":"Wall tools","values":{"height":3,"kind":"brick"}}""",
            ScriptHost.ToolOf(JsonNode.Parse("""{"name":"Make","call":"make","palette":" Wall tools ","values":{"height":3,"kind":"brick"}}""") as JsonObject)!.ToJsonString());
        Assert.Throws<CommandException>(() => ScriptHost.ToolOf(new JsonObject { ["name"] = "Make", ["palette"] = "" }));
        // No values: none sent on.
        Assert.False(ScriptHost.ToolOf(JsonNode.Parse("""{"name":"Make"}""") as JsonObject)!.ContainsKey("values"));
        var many = new JsonObject();
        for (var i = 0; i <= ScriptHost.MaxToolValues; i++) many[$"k{i}"] = i;
        var big = new JsonObject { ["text"] = new string('x', ScriptHost.MaxToolValuesBytes) };
        foreach (var bad in new JsonNode[] { many, big, new JsonArray(1, 2), JsonValue.Create("x") })
            Assert.Throws<CommandException>(() => ScriptHost.ToolOf(new JsonObject { ["name"] = "Make", ["values"] = bad }));
    }
}
