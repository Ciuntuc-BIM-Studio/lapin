using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// A step held open on the engine (docs/plan-python-scripting.md §8, decision 489): <c>doc.begin {label}</c>,
/// <c>doc.commit</c>, <c>doc.rollback</c>, tied to the connection that began it. Its commands join one undo step named
/// by the label; while it is open nobody else edits (the window is refused, reads go on); a lost connection or a
/// rollback puts the model back as it was.
/// </summary>
public sealed class ScriptStepTests
{
    static readonly object Kernel = new(), Window = new();

    static JsonObject WallArgs(Fixture f, double y) => new() { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = y, ["x2"] = 5, ["y2"] = y };

    static int Walls(Fixture f) => f.Model.OfClass(IfcClass.Wall).Count();

    /// <summary>A command run as the interface of <paramref name="connection"/> runs it.</summary>
    static Diff As(Fixture f, object connection, Func<Diff> run)
    {
        f.Session.Connection = connection;
        try { return run(); }
        finally { f.Session.Connection = null; }
    }

    [Fact]
    public void The_commands_of_an_open_step_are_one_undo_step_named_by_its_label()
    {
        var f = new Fixture();
        f.Wall(0, -3, 5, -3);
        As(f, Kernel, () => { f.Session.Begin("Room walls"); return f.Session.Commit(); });
        Assert.Equal("Wall", f.Session.History.UndoLabel);   // a step that changed nothing leaves no undo step

        As(f, Kernel, () =>
        {
            f.Session.Begin("Room walls");
            Assert.True(f.Session.StepOpen);
            for (var i = 0; i < 4; i++) f.RunDiff("architecture.addWall", WallArgs(f, i));
            // Each command's diff goes out as it is made, so the window shows the script at work.
            var d = f.RunDiff("architecture.addWall", WallArgs(f, 9));
            Assert.Contains(d.Nodes, n => n.Type == IfcClass.Wall);
            // Not in the history until the step ends.
            Assert.Equal("Wall", f.Session.History.UndoLabel);
            var done = f.Session.Commit();
            Assert.Equal("Room walls", done.State!.UndoLabel);
            return done;
        });
        Assert.False(f.Session.StepOpen);
        Assert.Equal(6, Walls(f));
        Assert.Equal("Room walls", f.Session.History.UndoLabel);

        // One Undo takes the five away; one Redo brings them back.
        f.Session.Undo();
        Assert.Equal(1, Walls(f));
        Assert.Equal("Wall", f.Session.History.UndoLabel);
        f.Session.Redo();
        Assert.Equal(6, Walls(f));
    }

    [Fact]
    public void A_rollback_puts_the_model_back_and_leaves_no_undo_step()
    {
        var f = new Fixture();
        var kept = f.Wall(0, -3, 5, -3);
        string? made = null;
        var back = As(f, Kernel, () =>
        {
            f.Session.Begin("Script");
            made = (string)f.Run("architecture.addWall", WallArgs(f, 0))!["id"]!;
            f.Run("element.rename", new JsonObject { ["id"] = kept, ["name"] = "Renamed by the script" });
            return f.Session.Rollback();
        });
        Assert.False(f.Session.StepOpen);
        Assert.Null(f.Model.Get(made!));
        Assert.Contains(made!, back.RemovedNodes);
        Assert.Equal(1, Walls(f));
        Assert.NotEqual("Renamed by the script", f.Model.Get(kept)!.Name);
        Assert.Contains(back.Nodes, n => n.Id == kept);
        Assert.Equal("Wall", f.Session.History.UndoLabel);
        Assert.False(f.Session.History.CanRedo);
    }

    [Fact]
    public void A_refused_command_inside_the_step_takes_back_only_itself()
    {
        var f = new Fixture();
        As(f, Kernel, () =>
        {
            f.Session.Begin("Script");
            f.Run("architecture.addWall", WallArgs(f, 0));
            Assert.ThrowsAny<ModelException>(() => f.Run("architecture.addWall", new JsonObject { ["storeyId"] = "nowhere", ["x1"] = 0, ["y1"] = 0, ["x2"] = 0, ["y2"] = 0 }));
            f.Run("architecture.addWall", WallArgs(f, 2));
            Assert.True(f.Session.StepOpen);
            return f.Session.Commit();
        });
        Assert.Equal(2, Walls(f));
        f.Session.Undo();
        Assert.Equal(0, Walls(f));
    }

    [Fact]
    public void While_a_step_is_open_the_others_cannot_edit_undo_or_open_another_project_but_they_read()
    {
        var f = new Fixture();
        f.Wall(0, -3, 5, -3);
        As(f, Kernel, () => { f.Session.Begin("Script"); f.Run("architecture.addWall", WallArgs(f, 0)); return f.Session.Snapshot(); });
        As(f, Window, () =>
        {
            var e = Assert.Throws<CommandException>(() => f.Run("architecture.addWall", WallArgs(f, 1)));
            Assert.Equal(Session.ScriptEditing, e.Message);
            Assert.Equal(Session.ScriptEditing, Assert.Throws<CommandException>(() => f.Session.Undo()).Message);
            Assert.Equal(Session.ScriptEditing, Assert.Throws<CommandException>(() => f.Session.Redo()).Message);
            Assert.Equal(Session.ScriptEditing, Assert.Throws<CommandException>(() => f.Session.New()).Message);
            Assert.Throws<CommandException>(() => f.Session.Commit());   // nor end the script's step
            Assert.Throws<CommandException>(() => f.Session.Rollback());
            // Reads go on.
            Assert.Equal(2, f.Session.Snapshot().Nodes.Count(n => n.Type == IfcClass.Wall));
            return f.Session.Snapshot();
        });
        // The script itself cannot undo inside its step, nor begin a second one.
        As(f, Kernel, () =>
        {
            Assert.Throws<CommandException>(() => f.Session.Undo());
            Assert.Throws<CommandException>(() => f.Session.Begin("Again"));
            return f.Session.Commit();
        });
        As(f, Window, () => f.RunDiff("architecture.addWall", WallArgs(f, 1)));
        Assert.Equal(3, Walls(f));
    }

    [Fact]
    public void The_journal_gets_the_step_once_when_it_is_committed_and_nothing_of_a_rolled_back_one()
    {
        var f = new Fixture();
        var events = new List<HistoryEvent>();
        f.Session.HistoryChanged += events.Add;
        As(f, Kernel, () =>
        {
            f.Session.Begin("Facades");
            f.Run("architecture.addWall", WallArgs(f, 0));
            f.Run("architecture.addWall", WallArgs(f, 1));
            Assert.Empty(events);
            return f.Session.Commit();
        });
        var tx = Assert.Single(events);
        Assert.Equal("tx", tx.Kind);
        Assert.Equal("Facades", tx.Transaction!.Label);
        Assert.Equal(2, tx.Transaction.Entities.Count(c => c.After?.Class == IfcClass.Wall));
        events.Clear();
        As(f, Kernel, () => { f.Session.Begin("Gone"); f.Run("architecture.addWall", WallArgs(f, 2)); return f.Session.Rollback(); });
        Assert.Empty(events);
    }

    [Fact]
    public void Deliveries_wait_for_the_step_to_end()
    {
        var f = new Fixture();
        var d = f.Session.Deliveries;
        var stamp = d.Stamp();
        As(f, Kernel, () => { f.Session.Begin("Script"); return f.Session.Snapshot(); });
        d.Deliver("Later", stamp, ctx => ctx.Add(new Entity(ctx.NewId(), IfcClass.Wall, "late", [])));
        Assert.Empty(f.Session.TakeDeliveries());
        Assert.True(f.Session.DeliveriesWaiting);
        As(f, Kernel, () => f.Session.Commit());
        Assert.Equal("applied", Assert.Single(f.Session.TakeDeliveries()).Status);
    }

    // ------------------------------------------------------------------ over the protocol, through the host

    /// <summary>A client that keeps the frames' headers and the text events it is sent.</summary>
    sealed class Recorder : EngineHub.Client
    {
        public readonly List<string> Got = [];
        public override Task SendBinary(byte[] frame) { lock (Got) Got.Add(BinaryFrame.HeaderText(frame)); return Task.CompletedTask; }
        public override Task SendText(string text) { lock (Got) Got.Add(text); return Task.CompletedTask; }
        protected override void Drop() { }
    }

    static string Call(int id, string method, JsonObject? p = null) => new JsonObject { ["id"] = id, ["method"] = method, ["params"] = p ?? [] }.ToJsonString();

    static JsonElement Header(byte[]? frame) => BinaryFrame.Decode(frame!).Header.RootElement;

    static string? Error(byte[]? frame) => Header(frame).TryGetProperty("error", out var e) ? e.GetString() : null;

    static string Execute(int id, Fixture f, double y) =>
        Call(id, "doc.execute", new JsonObject { ["command"] = "architecture.addWall", ["args"] = WallArgs(f, y) });

    [Fact]
    public async Task Over_the_protocol_the_step_is_the_connections_and_the_window_is_refused_while_it_is_open()
    {
        var f = new Fixture();
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var kernel = new Recorder();
        var window = new Recorder();
        hub.Add(kernel);
        hub.Add(window);
        Assert.Null(Error(await hub.Handle(Call(1, "doc.begin", new JsonObject { ["label"] = "walls.py" }), kernel)));
        Assert.Null(Error(await hub.Handle(Execute(2, f, 0), kernel)));
        Assert.Null(Error(await hub.Handle(Execute(3, f, 1), kernel)));
        Assert.Equal(Session.ScriptEditing, Error(await hub.Handle(Execute(4, f, 2), window)));
        Assert.Equal(Session.ScriptEditing, Error(await hub.Handle(Call(5, "doc.undo"), window)));
        Assert.Equal(Session.ScriptEditing, Error(await hub.Handle(Call(6, "doc.commit"), window)));
        Assert.Null(Error(await hub.Handle(Call(7, "doc.snapshot"), window)));
        Assert.Null(Error(await hub.Handle(Call(8, "doc.commit"), kernel)));
        Assert.Equal(2, Walls(f));
        Assert.Equal("walls.py", f.Session.History.UndoLabel);
        // The window's Undo takes the whole run away.
        Assert.Null(Error(await hub.Handle(Call(9, "doc.undo"), window)));
        Assert.Equal(0, Walls(f));
        // Nothing open: commit and rollback say so.
        Assert.Equal(Session.NoStep, Error(await hub.Handle(Call(10, "doc.commit"), kernel)));
        Assert.Equal(Session.NoStep, Error(await hub.Handle(Call(11, "doc.rollback"), kernel)));
    }

    [Fact]
    public async Task A_lost_connection_rolls_its_step_back_and_every_window_gets_the_diff()
    {
        var f = new Fixture();
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var kernel = new Recorder();
        var window = new Recorder();
        hub.Add(kernel);
        hub.Add(window);
        await hub.Handle(Call(1, "doc.begin", new JsonObject { ["label"] = "Killed" }), kernel);
        await hub.Handle(Execute(2, f, 0), kernel);
        var made = f.Model.OfClass(IfcClass.Wall).Single().Id;
        await window.Flushed();
        lock (window.Got) window.Got.Clear();

        await hub.Remove(kernel);
        Assert.False(f.Session.StepOpen);
        Assert.Equal(0, Walls(f));
        Assert.False(f.Session.History.CanUndo);
        await window.Flushed();
        string[] got;
        lock (window.Got) got = [.. window.Got];
        var diff = got.Select(t => JsonNode.Parse(t)!).Single(n => (string?)n["event"] == "diff");
        Assert.Contains(diff["data"]!["removedNodes"]!.AsArray(), n => (string?)n == made);
        // The window edits again.
        Assert.Null(Error(await hub.Handle(Execute(3, f, 0), window)));
    }

    [Fact]
    public async Task A_guest_can_neither_open_a_step_nor_run_a_script()
    {
        var session = new Session(ModuleCatalog.Create([new ArchitectureModule()]), new ProductManifest("test", "Test", ["core", "architecture", "scripting"]));
        session.New();
        var hub = new EngineHub(session, PackageFiles.Create());
        var guest = new Recorder { Caller = new Caller("guest-1", "editor") };
        hub.Add(guest);
        foreach (var m in new[] { "doc.begin", "doc.commit", "doc.rollback" })
            Assert.Equal("Only the host can do that.", Error(await hub.Handle(Call(1, m, new JsonObject { ["label"] = "x" }), guest)));
        foreach (var m in new[] { "script.run", "script.stop", "script.restart", "script.status", "script.files", "script.load", "script.save", "script.delete", "script.reveal" })
            Assert.Equal("Only the host can do that.", Error(await hub.Handle(Call(2, m, new JsonObject { ["code"] = "print(1)", ["name"] = "a.py" }), guest)));
        Assert.False(session.StepOpen);
    }

    [Fact]
    public async Task Scripts_are_refused_where_the_edition_has_no_scripting_module()
    {
        var f = new Fixture();   // core and architecture only
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        Assert.Equal(ScriptHost.NotInEdition, Error(await hub.Handle(Call(1, "script.run", new JsonObject { ["code"] = "print(1)" }))));
        var status = Header(await hub.Handle(Call(2, "script.status"))).GetProperty("result");
        Assert.False(status.GetProperty("enabled").GetBoolean());
    }
}
