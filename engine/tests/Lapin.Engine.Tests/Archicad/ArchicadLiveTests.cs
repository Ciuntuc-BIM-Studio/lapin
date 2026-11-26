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
