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
}
