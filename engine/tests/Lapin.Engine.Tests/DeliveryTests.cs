using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Background results (docs/overrides.md): a command stamps the model and starts its work; the work delivers from any
/// thread; the session applies it between calls as an undo step of its own and sends its diff, unless the model moved past it.
/// </summary>
public sealed class DeliveryTests
{
    const string Note = "LapinProbeNote";

    /// <summary>A module whose command starts work it delivers later: a note named after the args, made in the background.</summary>
    sealed class Later : ILapinModule
    {
        public string Id => "later";
        public string Name => "Later";
        /// <summary>What the last command started: its stamp, for the test to deliver when it likes.</summary>
        public DeliveryStamp Stamp;
        public IDeliveries? Deliveries;
        public bool EditToo;

        public void Register(IModuleRegistry r)
        {
            r.Class(new(Note, "Note", [], Kind: "resource"));
            r.Command("start", "Start the work", (ctx, a) =>
            {
                Deliveries = ctx.Deliveries;
                Stamp = ctx.Deliveries!.Stamp();
                if (EditToo) ctx.Add(new Entity(ctx.NewId(), Note, "started", []));
                return null;
            });
        }

        public static Action<ICommandContext> Add(string name) => ctx => ctx.Add(new Entity(ctx.NewId(), Note, name, []));
    }

    static (Fixture F, Later L) Make()
    {
        var l = new Later();
        return (new Fixture(new ArchitectureModule(), l), l);
    }

    static Entity? Named(Fixture f, string name) => f.Model.OfClass(Note).FirstOrDefault(e => e.Name == name);

    [Fact]
    public async Task A_result_delivered_from_another_thread_is_applied_on_the_sessions_as_an_undo_step_of_its_own()
    {
        var (f, l) = Make();
        l.EditToo = true;
        f.Run("later.start", []);
        var waiting = 0;
        f.Session.DeliveryWaiting += () => Interlocked.Increment(ref waiting);
        // The command's own edit does not make its work stale.
        await Task.Run(() => l.Deliveries!.Deliver("Worked out", l.Stamp, Later.Add("result")));
        Assert.Equal(1, waiting);
        Assert.True(f.Session.DeliveriesWaiting);
        Assert.Null(Named(f, "result"));   // nothing until the session takes it

        var o = Assert.Single(f.Session.TakeDeliveries());
        Assert.Equal("applied", o.Status);
        var note = Named(f, "result")!;
        Assert.Contains(o.Diff!.Nodes, n => n.Id == note.Id);
        Assert.False(f.Session.DeliveriesWaiting);
        Assert.Equal("Worked out", f.Session.History.UndoLabel);
        // Its own step: undone alone, the command's edit stays.
        f.Session.Undo();
        Assert.Null(Named(f, "result"));
        Assert.NotNull(Named(f, "started"));
    }

    [Fact]
    public void A_result_the_model_moved_past_is_dropped()
    {
        var (f, l) = Make();
        f.Run("later.start", []);
        var stamp = l.Stamp;
        var wall = f.Wall(0, 0, 5, 0);
        var other = f.Wall(0, 3, 5, 3);
        // Anything changed since, and the delivery said nothing of what it read: stale.
        l.Deliveries!.Deliver("Any", stamp, Later.Add("any"));
        // It read the first wall, which changed after the stamp: stale.
        l.Deliveries.Deliver("Read the wall", stamp, Later.Add("wall"), [wall]);
        // It read the project only, which no edit since touched: applied.
        var project = Spatial.Project(f.Model)!.Id;
        l.Deliveries.Deliver("Read the project", stamp, Later.Add("project"), [project]);
        Assert.Equal(["stale", "stale", "applied"], f.Session.TakeDeliveries().Select(o => o.Status));
        Assert.Null(Named(f, "any"));
        Assert.NotNull(Named(f, "project"));

        // Undo is a change too.
        f.Run("later.start", []);
        stamp = l.Stamp;
        f.Session.Undo();
        l.Deliveries.Deliver("After undo", stamp, Later.Add("undone"));
        Assert.Equal("stale", Assert.Single(f.Session.TakeDeliveries()).Status);
        Assert.NotNull(f.Model.Get(other));

        // Another project: whatever it read is gone.
        f.Run("later.start", []);
        stamp = l.Stamp;
        f.Session.New();
        l.Deliveries.Deliver("Old project", stamp, Later.Add("old"), [project]);
        Assert.Equal("stale", Assert.Single(f.Session.TakeDeliveries()).Status);
        Assert.Empty(f.Model.OfClass(Note));
    }

    [Fact]
    public void A_failing_or_empty_result_leaves_the_model_as_it_was()
    {
        var (f, l) = Make();
        f.Run("later.start", []);
        l.Deliveries!.Deliver("Fails", l.Stamp, ctx => { Later.Add("half")(ctx); throw new CommandException("No."); });
        l.Deliveries.Deliver("Nothing", l.Stamp, _ => { });
        var o = f.Session.TakeDeliveries();
        Assert.Equal(["failed", "empty"], o.Select(x => x.Status));
        Assert.Equal("No.", o[0].Error);
        Assert.Empty(f.Model.OfClass(Note));
        Assert.False(f.Session.History.CanUndo);
    }

    [Fact]
    public void A_stamp_from_a_query_stands_before_the_next_edit()
    {
        var (f, l) = Make();
        var deliveries = f.Session.Deliveries;
        var stamp = deliveries.Stamp();
        deliveries.Deliver("Unchanged", stamp, Later.Add("a"));
        Assert.Equal("applied", Assert.Single(f.Session.TakeDeliveries()).Status);
        // That delivery was an edit: a stamp taken before it is stale now.
        deliveries.Deliver("After it", stamp, Later.Add("b"));
        Assert.Equal("stale", Assert.Single(f.Session.TakeDeliveries()).Status);
    }

    /// <summary>A client that keeps what it is sent: the frames' headers and the text events.</summary>
    sealed class Recorder : EngineHub.Client
    {
        public readonly List<string> Got = [];
        public override Task SendBinary(byte[] frame) { lock (Got) Got.Add(BinaryFrame.HeaderText(frame)); return Task.CompletedTask; }
        public override Task SendText(string text) { lock (Got) Got.Add(text); return Task.CompletedTask; }
        protected override void Drop() { }
    }

    [Fact]
    public async Task The_host_sends_a_delivery_to_every_client_as_a_diff()
    {
        var (f, l) = Make();
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var client = new Recorder();
        hub.Add(client);
        await hub.Handle(new JsonObject { ["id"] = 1, ["method"] = "doc.execute", ["params"] = new JsonObject { ["command"] = "later.start" } }.ToJsonString());
        await client.Flushed();
        lock (client.Got) client.Got.Clear();

        _ = Task.Run(() => l.Deliveries!.Deliver("Worked out", l.Stamp, Later.Add("result")));
        for (var i = 0; i < 500 && Named(f, "result") == null; i++) await Task.Delay(10);
        var note = Named(f, "result")!;
        Assert.NotNull(note);
        for (var i = 0; i < 500; i++) { lock (client.Got) if (client.Got.Count >= 2) break; await Task.Delay(10); }
        await client.Flushed();
        string[] got;
        lock (client.Got) got = [.. client.Got];
        var diff = JsonNode.Parse(got[0])!;
        Assert.Equal("diff", (string?)diff["event"]);
        Assert.Contains(diff["data"]!["nodes"]!.AsArray(), n => (string?)n!["id"] == note.Id);
        var told = JsonNode.Parse(got[1])!;
        Assert.Equal("delivery", (string?)told["event"]);
        Assert.Equal("applied", (string?)told["data"]!["status"]);
    }
}
