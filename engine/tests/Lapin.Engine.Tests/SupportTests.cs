using System.Net;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Protocol;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Reports from the app reach the feedback endpoint with what it expects; tried against a local stand-in, never the real server.</summary>
public class SupportTests
{
    [Fact]
    public async Task A_report_is_posted_with_its_context_and_the_number_comes_back()
    {
        var port = Random.Shared.Next(49000, 59000);
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/api/feedback/");
        listener.Start();
        JsonObject? received = null;
        var server = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            received = (JsonObject)JsonNode.Parse(ctx.Request.InputStream)!;
            ctx.Response.StatusCode = 201;
            await ctx.Response.OutputStream.WriteAsync("{\"number\":42}"u8.ToArray());
            ctx.Response.Close();
        });
        Environment.SetEnvironmentVariable("LAPIN_FEEDBACK_URL", $"http://127.0.0.1:{port}/api/feedback/");
        try
        {
            var r = await Support.Report(new JsonObject { ["kind"] = "bug", ["title"] = "Walls", ["description"] = "A wall vanished.", ["errors"] = new JsonArray("x") }, "1.2.3");
            await server;
            Assert.Equal(42, (double)r["number"]!);
            Assert.Equal(("lapin", "bug", "Walls", ""), ((string)received!["app"]!, (string)received["kind"]!, (string)received["title"]!, (string)received["website"]!));
            Assert.Equal("lapin-pro 1.2.3", (string)received["context"]!["version"]!);
            await Assert.ThrowsAsync<CommandException>(() => Support.Report(new JsonObject { ["kind"] = "bug", ["title"] = "", ["description"] = "x" }, "1"));
            await Assert.ThrowsAsync<CommandException>(() => Support.Report(new JsonObject { ["kind"] = "spam", ["title"] = "t", ["description"] = "x" }, "1"));
            Assert.Throws<CommandException>(() => Support.Open("http://example.com"));
        }
        finally { Environment.SetEnvironmentVariable("LAPIN_FEEDBACK_URL", null); }
    }

    [Fact]
    public async Task A_report_waiting_on_a_slow_server_holds_up_no_other_call()
    {
        // Audit 22.11.2026: support.report ran inside the hub's gate, so a slow server held every window up to 30 s.
        var port = Random.Shared.Next(49000, 59000);
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/api/feedback/");
        listener.Start();
        var release = new TaskCompletionSource();
        var server = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            await release.Task;
            ctx.Response.StatusCode = 201;
            await ctx.Response.OutputStream.WriteAsync("{\"number\":7}"u8.ToArray());
            ctx.Response.Close();
        });
        Environment.SetEnvironmentVariable("LAPIN_FEEDBACK_URL", $"http://127.0.0.1:{port}/api/feedback/");
        try
        {
            var hub = new EngineHub(new Fixture().Session, PackageFiles.Create());
            var report = hub.Handle(new JsonObject { ["id"] = 1, ["method"] = "support.report", ["params"] = new JsonObject { ["kind"] = "bug", ["title"] = "t", ["description"] = "d" } }.ToJsonString());
            var other = hub.Handle("""{"id":2,"method":"doc.snapshot","params":{}}""");
            var first = await Task.WhenAny(other, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.Same(other, first);
            Assert.False(report.IsCompleted);
            release.SetResult();
            var answer = Lapin.Protocol.BinaryFrame.Decode((await report)!).Header.RootElement;
            Assert.Equal(7, answer.GetProperty("result").GetProperty("number").GetDouble());
            await server;
            var guest = new Guest { Caller = new Caller("guest-1", "editor") };
            var refused = Lapin.Protocol.BinaryFrame.Decode((await hub.Handle("""{"id":3,"method":"support.messages","params":{}}""", guest))!).Header.RootElement;
            Assert.Equal("Only the host can do that.", refused.GetProperty("error").GetString());
        }
        finally { Environment.SetEnvironmentVariable("LAPIN_FEEDBACK_URL", null); }
    }

    sealed class Guest : EngineHub.Client
    {
        public override Task SendBinary(byte[] frame) => Task.CompletedTask;
        public override Task SendText(string text) => Task.CompletedTask;
        protected override void Drop() { }
    }

    [Fact]
    public void The_guide_opens_in_each_interface_language()
    {
        Assert.Equal("https://lapinbim.com/docs/", Support.Guides["guide"]);
        Assert.Equal("https://lapinbim.com/docs/ro/", Support.Guides["guideRo"]);
        Assert.Equal("https://lapinbim.com/docs/da/", Support.Guides["guideDa"]);
        Assert.Equal("https://lapinbim.com/docs/it/", Support.Guides["guideIt"]);
        Assert.Equal("https://lapinbim.com/docs/pt/", Support.Guides["guidePt"]);
        Assert.Equal(["guide", "guideDa", "guideDe", "guideEs", "guideFr", "guideIt", "guidePl", "guidePt", "guideRo"], Support.Guides.Keys.Order());
    }

    [Fact]
    public void The_forum_opens_and_a_new_topic_comes_filled_in_on_the_forum_only()
    {
        Assert.Equal("https://forum.lapinbim.com", Support.LinkUrl("forum"));
        Assert.Equal("https://forum.lapinbim.com/new-topic?title=Walls%20%26%20roofs&body=Lapin%200.1.9%0Amacos&category=lapin",
            Support.LinkUrl("forumTopic", new JsonObject { ["title"] = " Walls & roofs ", ["body"] = "Lapin 0.1.9\nmacos" }));
        Assert.Equal("https://forum.lapinbim.com/new-topic?category=lapin", Support.LinkUrl("forumTopic"));
        // Only the forum's address: a title that looks like an address stays text in its query.
        var odd = Support.LinkUrl("forumTopic", new JsonObject { ["title"] = "https://example.com/?x=1#y", ["body"] = new string('a', 5000) });
        Assert.StartsWith("https://forum.lapinbim.com/new-topic?title=https%3A%2F%2Fexample.com%2F%3Fx%3D1%23y&body=", odd);
        Assert.Equal(4000, odd.Split("body=")[1].Split('&')[0].Length);
        Assert.Throws<CommandException>(() => Support.LinkUrl("https://forum.lapinbim.com/evil"));
    }
}
