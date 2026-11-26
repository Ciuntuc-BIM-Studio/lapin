using System.Text.Json.Nodes;
using Lapin.Archicad;
using Lapin.Architecture;
using Lapin.Structure;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The Archicad connector against a running Archicad (docs/plan-archicad.md, A1): only when <c>LAPIN_ARCHICAD_PORT</c> names its port
/// (Archicad open with Tapir 2.0 or newer); read only. With <c>LAPIN_ARCHICAD_RECORD</c> naming a folder, every answer is recorded
/// there as the fixtures the other tests replay.
/// </summary>
public sealed class ArchicadLiveTests(ITestOutputHelper output)
{
    [Fact]
    public void A_running_Archicad_is_received()
    {
        if (Environment.GetEnvironmentVariable("LAPIN_ARCHICAD_PORT") is not { Length: > 0 } portText || !int.TryParse(portText, out var port)) return;
        var recording = new RecordingTransport(new HttpTransport());
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new ArchicadModule(recording));
        var instances = (JsonArray)f.Session.Query("archicad.instances", new JsonObject())!;
        output.WriteLine(instances.ToJsonString());
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var survey = f.Session.Query("archicad.survey", new JsonObject { ["port"] = port })!;
        output.WriteLine($"survey {sw.ElapsedMilliseconds} ms: {survey.ToJsonString()}");
        if (Environment.GetEnvironmentVariable("LAPIN_ARCHICAD_RECORD") is { Length: > 0 } folder)
        {
            // The recording of one read (the receive's own reads go after it), as the fixtures replay it.
            recording.Save(folder);
            recording.Recorded.Clear();
        }
        sw.Restart();
        var result = f.Run("archicad.receive", new JsonObject { ["port"] = port })!;
        output.WriteLine($"receive {sw.ElapsedMilliseconds} ms: {result.ToJsonString()}");
        Assert.True((int)result["elements"]! > 0);
        sw.Restart();
        var again = f.Run("archicad.receive", new JsonObject { ["port"] = port })!;
        output.WriteLine($"again {sw.ElapsedMilliseconds} ms: {again.ToJsonString()}");
        Assert.Equal((int)result["elements"]!, (int)again["unchanged"]!);

        // Get latest (A2): nothing changed in Archicad since the receive, so the cheap status counts none and the update reads nothing.
        sw.Restart();
        var status = f.Session.Query("archicad.status", new JsonObject { ["port"] = port })!;
        output.WriteLine($"status {sw.ElapsedMilliseconds} ms: {status.ToJsonString()}");
        sw.Restart();
        var latest = f.Run("archicad.update", new JsonObject { ["port"] = port })!;
        output.WriteLine($"update {sw.ElapsedMilliseconds} ms: {latest.ToJsonString()}");
        Assert.Equal("incremental", (string?)latest["mode"]);

        // LAPIN_ARCHICAD_WAIT=<seconds>: change the model in Archicad meanwhile; the status is asked every 2 s, and once it counts a
        // change, Get latest reads only what changed.
        if (Environment.GetEnvironmentVariable("LAPIN_ARCHICAD_WAIT") is { Length: > 0 } waitText && int.TryParse(waitText, out var wait))
        {
            var until = DateTime.UtcNow.AddSeconds(wait);
            JsonNode? changed = null;
            while (DateTime.UtcNow < until)
            {
                var s = f.Session.Query("archicad.status", new JsonObject { ["port"] = port })!;
                if (s["changed"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c) { changed = s; break; }
                Thread.Sleep(2000);
            }
            output.WriteLine($"status after waiting: {changed?.ToJsonString() ?? "no change seen"}");
            if (changed != null)
            {
                Thread.Sleep(5000); // the rest of the user's edit
                sw.Restart();
                var after = f.Run("archicad.update", new JsonObject { ["port"] = port })!;
                output.WriteLine($"update after the change {sw.ElapsedMilliseconds} ms: {after.ToJsonString()}");
                Assert.Equal("incremental", (string?)after["mode"]);
            }
        }

        // Send to Atrium's first step: Archicad saves an IFC to a temporary file (a demo Archicad may refuse: the message says so).
        var translators = (JsonArray)f.Session.Query("archicad.translators", new JsonObject { ["port"] = port })!;
        output.WriteLine($"translators: {translators.ToJsonString()}");
        sw.Restart();
        try
        {
            var ifc = (JsonObject)f.Session.Query("archicad.saveIfc", new JsonObject { ["port"] = port })!;
            // The two routes agree element by element: every GlobalId received is one Archicad's own IFC export writes.
            var text = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String((string)ifc["data"]!));
            var received = f.Model.Entities.Where(e => e.Attrs[ArchicadReceive.Key] is JsonObject meta && (string?)meta["role"] == null).Select(e => e.Id).ToList();
            var missing = received.Where(id => !text.Contains($"'{id}'", StringComparison.Ordinal)).ToList();
            output.WriteLine($"GlobalIds in Archicad's IFC: {received.Count - missing.Count} of {received.Count}{(missing.Count > 0 ? " (missing " + string.Join(", ", missing) + ")" : "")}");
            output.WriteLine($"saveIfc {sw.ElapsedMilliseconds} ms: {ifc["name"]} {ifc["size"]} bytes, sha256 {ifc["sha256"]}, starts {System.Text.Encoding.ASCII.GetString(Convert.FromBase64String((string)ifc["data"]!)[..60])}");
        }
        catch (Lapin.Platform.CommandException ex) { output.WriteLine($"saveIfc refused after {sw.ElapsedMilliseconds} ms: {ex.Message}"); }
    }
}
