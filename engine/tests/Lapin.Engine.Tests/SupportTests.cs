using System.Net;
using System.Text.Json.Nodes;
using Lapin.Host;
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
    public void The_guide_opens_in_each_interface_language()
    {
        Assert.Equal("https://lapin.ciuntucbimstudio.ro/docs/", Support.Guides["guide"]);
        Assert.Equal("https://lapin.ciuntucbimstudio.ro/docs/ro/", Support.Guides["guideRo"]);
        Assert.Equal("https://lapin.ciuntucbimstudio.ro/docs/da/", Support.Guides["guideDa"]);
        Assert.Equal("https://lapin.ciuntucbimstudio.ro/docs/it/", Support.Guides["guideIt"]);
        Assert.Equal("https://lapin.ciuntucbimstudio.ro/docs/pt/", Support.Guides["guidePt"]);
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
