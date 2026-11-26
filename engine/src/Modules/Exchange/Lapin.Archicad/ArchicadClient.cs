using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Archicad;

/// <summary>
/// How a request reaches an Archicad: one JSON body posted to its JSON API on a port of 127.0.0.1, its answer as JSON. The
/// seam the tests replay recorded answers through (<see cref="ReplayTransport"/>), so no test needs Archicad.
/// </summary>
public interface IArchicadTransport
{
    /// <summary>The answer to <paramref name="body"/>, or null when nothing answers on the port (no Archicad there).</summary>
    JsonObject? Post(int port, JsonObject body, TimeSpan timeout, CancellationToken ct);
}

/// <summary>The JSON API over HTTP, to 127.0.0.1 only.</summary>
public sealed class HttpTransport : IArchicadTransport
{
    static readonly HttpClient Http = new(new SocketsHttpHandler { UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(1) }) { Timeout = Timeout.InfiniteTimeSpan };

    public JsonObject? Post(int port, JsonObject body, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}") { Content = new StringContent(body.ToJsonString(), Encoding.UTF8) };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        try
        {
            using var res = Http.Send(req, cts.Token);
            using var s = res.Content.ReadAsStream(cts.Token);
            return JsonNode.Parse(s) as JsonObject;
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CommandException($"Archicad (port {port}) did not answer within {timeout.TotalSeconds:0} s.");
        }
        catch (System.Text.Json.JsonException) { return null; }
    }
}

/// <summary>
/// Recorded answers played back (the tests' fixtures): each command's answer by its name (Tapir's command, or Archicad's own
/// <c>API.…</c>), one port. A command with no answer recorded fails as Archicad would.
/// </summary>
public sealed class ReplayTransport(IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> answers, int port = ArchicadClient.FirstPort) : IArchicadTransport
{
    readonly Dictionary<string, int> _next = [];

    public List<string> Asked { get; } = [];

    public JsonObject? Post(int p, JsonObject body, TimeSpan timeout, CancellationToken ct)
    {
        if (p != port) return null;
        var name = ArchicadClient.NameOf(body);
        Asked.Add(name);
        if (!answers.TryGetValue(name, out var list) || list.Count == 0)
            return new JsonObject { ["succeeded"] = false, ["error"] = new JsonObject { ["code"] = 404, ["message"] = $"No answer recorded for {name}." } };
        // Asked again (a batch, a second system): the next answer recorded, the last one over and over.
        var i = _next.GetValueOrDefault(name);
        _next[name] = i + 1;
        return (JsonObject)list[Math.Min(i, list.Count - 1)].DeepClone();
    }

    /// <summary>
    /// The answers in a folder, each file named by its command (<c>GetStories.json</c>, <c>API.IsAlive.json</c>): a recording
    /// {request, response}, {responses: […]} for a command asked several times, or the response alone.
    /// </summary>
    public static ReplayTransport FromFolder(string folder, int port = ArchicadClient.FirstPort)
    {
        var o = new Dictionary<string, IReadOnlyList<JsonObject>>();
        foreach (var f in Directory.GetFiles(folder, "*.json"))
            if (JsonNode.Parse(File.ReadAllText(f)) is JsonObject j)
                o[Path.GetFileNameWithoutExtension(f)] = j["responses"] is JsonArray many ? [.. many.OfType<JsonObject>()] : [j["response"] as JsonObject ?? j];
        return new ReplayTransport(o, port);
    }
}

/// <summary>Answers recorded as they pass (to make the tests' fixtures from a live Archicad): each command's under its name, in the order asked.</summary>
public sealed class RecordingTransport(IArchicadTransport inner) : IArchicadTransport
{
    public Dictionary<string, List<(JsonObject Request, JsonObject Response)>> Recorded { get; } = [];

    public JsonObject? Post(int port, JsonObject body, TimeSpan timeout, CancellationToken ct)
    {
        var a = inner.Post(port, body, timeout, ct);
        if (a != null)
        {
            var name = ArchicadClient.NameOf(body);
            (Recorded.TryGetValue(name, out var l) ? l : Recorded[name] = []).Add(((JsonObject)body.DeepClone(), (JsonObject)a.DeepClone()));
        }
        return a;
    }

    /// <summary>The recording written to <paramref name="folder"/>, one file per command (several answers as <c>responses</c>).</summary>
    public void Save(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (var (name, list) in Recorded)
        {
            JsonObject o = list.Count == 1 ? new JsonObject { ["request"] = list[0].Request["parameters"]?.DeepClone(), ["response"] = list[0].Response.DeepClone() }
                : new JsonObject { ["responses"] = new JsonArray([.. list.Select(x => (JsonNode)x.Response.DeepClone())]) };
            File.WriteAllText(Path.Combine(folder, name + ".json"), o.ToJsonString());
        }
    }
}

/// <summary>An Archicad found running: its port, version, Tapir's version, its project, and whether it can be received from (else why not).</summary>
public sealed record ArchicadInstance(int Port, string? Archicad, int? Build, string? Tapir, string? Project, bool Untitled, bool Teamwork, string? Problem)
{
    public bool Ok => Problem == null;

    public JsonObject ToJson() => new()
    {
        ["port"] = Port, ["archicad"] = Archicad, ["build"] = Build, ["tapir"] = Tapir, ["project"] = Project, ["untitled"] = Untitled, ["teamwork"] = Teamwork,
        ["ok"] = Ok, ["problem"] = Problem,
    };
}

/// <summary>
/// Archicad's JSON API (Graphisoft's) and the Tapir add-on's commands on it (docs/plan-archicad.md §3): instances found on ports
/// 19723–19744 by <c>API.IsAlive</c>, Tapir's version checked (2.0 at least), element lists sent in batches, generous timeouts for
/// the 3D geometry. Read only: A1 never asks Archicad to change anything (the one file it writes is an IFC to a temporary path,
/// <see cref="ArchicadModule"/>'s Send to Atrium).
/// </summary>
public sealed class ArchicadClient(IArchicadTransport transport, int port)
{
    public const int FirstPort = 19723, LastPort = 19744;
    public static readonly Version MinTapir = new(2, 0, 0);
    public const string TapirReleases = "https://github.com/ENZYME-APD/tapir-archicad-automation/releases";

    public int Port => port;
    public IArchicadTransport Transport => transport;

    /// <summary>How many elements go in one request: details and properties in large batches, the 3D geometry in smaller ones.</summary>
    public int Batch { get; init; } = 2000;
    public int GeometryBatch { get; init; } = 400;

    /// <summary>The command a body asks for: Tapir's by its name, Archicad's own as <c>API.…</c>.</summary>
    public static string NameOf(JsonObject body)
    {
        var cmd = (string?)body["command"] ?? "";
        if (cmd == "API.ExecuteAddOnCommand") return (string?)body["parameters"]?["addOnCommandId"]?["commandName"] ?? cmd;
        return cmd;
    }

    /// <summary>The Archicads running on this computer, each asked who it is; none when nothing answers.</summary>
    public static List<ArchicadInstance> Instances(IArchicadTransport transport, CancellationToken ct = default)
    {
        var o = new List<ArchicadInstance>();
        for (var p = FirstPort; p <= LastPort; p++)
        {
            ct.ThrowIfCancellationRequested();
            var alive = transport.Post(p, new JsonObject { ["command"] = "API.IsAlive" }, TimeSpan.FromSeconds(2), ct);
            // A dialog open in Archicad: even API.IsAlive answers 4001 (measured on Archicad 29): it is there, busy.
            if ((string?)alive?["error"]?["message"] is { } refused && Busy(refused)) { o.Add(new(p, null, null, null, null, false, false, BusyText(refused))); continue; }
            if (alive?["result"]?["isAlive"] is not JsonValue v || !v.TryGetValue<bool>(out var yes) || !yes) continue;
            o.Add(new ArchicadClient(transport, p).Describe(ct));
        }
        return o;
    }

    /// <summary>Who this Archicad is: its version, Tapir's, its project; the problem that stops a receive (Tapir missing or older than 2.0).</summary>
    public ArchicadInstance Describe(CancellationToken ct = default)
    {
        string? archicad = null; int? build = null;
        if (Api("GetProductInfo", null, ct, quiet: true) is JsonObject pi)
        {
            archicad = Json.Number(pi["version"]) is { } ver ? ((int)ver).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
            build = Json.Number(pi["buildNumber"]) is { } b ? (int)b : null;
        }
        string? tapir = null;
        try { tapir = (string?)Tapir("GetAddOnVersion", null, ct)?["version"]; }
        catch (CommandException ex) when (Busy(ex.Message)) { return new(port, archicad, build, null, null, false, false, BusyText(ex.Message)); }
        catch (CommandException) { tapir = null; }
        if (tapir == null)
            return new(port, archicad, build, null, null, false, false,
                $"Archicad {archicad} on port {port} answers, but the Tapir add-on does not: install Tapir 2.0 or newer for Archicad {archicad} ({TapirReleases}) and start Archicad again.");
        if (!Version.TryParse(tapir, out var tv) || tv < MinTapir)
            return new(port, archicad, build, tapir, null, false, false,
                $"Tapir {tapir} is installed in this Archicad; Lapin needs {MinTapir.ToString(2)} or newer. Download it from {TapirReleases} and replace the add-on.");
        string? project = null; var untitled = false; var teamwork = false;
        try
        {
            if (Tapir("GetProjectInfo", null, ct) is { } info)
            {
                untitled = info["isUntitled"] is JsonValue u && u.TryGetValue<bool>(out var ub) && ub;
                teamwork = info["isTeamwork"] is JsonValue t && t.TryGetValue<bool>(out var tb) && tb;
                project = (string?)info["projectName"] is { Length: > 0 } n ? n : null;
            }
        }
        catch (CommandException) { }
        return new(port, archicad, build, tapir, project, untitled, teamwork, null);
    }

    /// <summary>One of Archicad's own commands (<c>API.<paramref name="name"/></c>): its result, or an error.</summary>
    public JsonObject? Api(string name, JsonObject? parameters, CancellationToken ct = default, TimeSpan? timeout = null, bool quiet = false)
    {
        var body = new JsonObject { ["command"] = "API." + name };
        if (parameters != null) body["parameters"] = parameters;
        var a = transport.Post(port, body, timeout ?? TimeSpan.FromSeconds(60), ct) ?? throw new CommandException($"Archicad no longer answers on port {port}.");
        if (a["succeeded"] is JsonValue s && s.TryGetValue<bool>(out var ok) && ok) return a["result"] as JsonObject ?? new JsonObject();
        if (quiet) return null;
        throw new CommandException($"Archicad refused {name}: {ErrorText(a)}");
    }

    /// <summary>One of Tapir's commands: its answer (<c>addOnCommandResponse</c>), or an error.</summary>
    public JsonObject? Tapir(string name, JsonObject? parameters, CancellationToken ct = default, TimeSpan? timeout = null)
    {
        var body = new JsonObject
        {
            ["command"] = "API.ExecuteAddOnCommand",
            ["parameters"] = new JsonObject
            {
                ["addOnCommandId"] = new JsonObject { ["commandNamespace"] = "TapirCommand", ["commandName"] = name },
                ["addOnCommandParameters"] = parameters ?? new JsonObject(),
            },
        };
        var a = transport.Post(port, body, timeout ?? TimeSpan.FromSeconds(120), ct) ?? throw new CommandException($"Archicad no longer answers on port {port}.");
        if (!(a["succeeded"] is JsonValue s && s.TryGetValue<bool>(out var ok) && ok)) throw new CommandException($"Archicad refused {name}: {ErrorText(a)}");
        var r = a["result"]?["addOnCommandResponse"] as JsonObject ?? a["result"] as JsonObject ?? new JsonObject();
        // Tapir answers a failed command inside a successful call: {success: false, error: {…}}.
        if (r["success"] is JsonValue sv && sv.TryGetValue<bool>(out var sOk) && !sOk) throw new CommandException($"Archicad refused {name}: {ErrorText(r)}");
        return r;
    }

    /// <summary>
    /// A command over a list of elements, in batches (<paramref name="size"/> elements each), its answers' list under
    /// <paramref name="key"/> joined in the order asked.
    /// </summary>
    public List<JsonNode?> Each(string name, IReadOnlyList<string> guids, string key, Func<JsonArray, JsonObject> parameters, int size, CancellationToken ct = default, TimeSpan? timeout = null)
    {
        var o = new List<JsonNode?>(guids.Count);
        for (var i = 0; i < guids.Count; i += size)
        {
            ct.ThrowIfCancellationRequested();
            var batch = Elements(guids.Skip(i).Take(size));
            var r = Tapir(name, parameters(batch), ct, timeout);
            var list = r?[key] as JsonArray ?? [];
            foreach (var n in list) o.Add(n?.DeepClone());
            for (var k = list.Count; k < Math.Min(size, guids.Count - i); k++) o.Add(null);
        }
        return o;
    }

    /// <summary>Elements as Archicad lists them: [{elementId: {guid}}].</summary>
    public static JsonArray Elements(IEnumerable<string> guids) =>
        new([.. guids.Select(g => (JsonNode)new JsonObject { ["elementId"] = new JsonObject { ["guid"] = g } })]);

    /// <summary>Whether Archicad refused because it is busy with a dialog (its JSON API answers 4001 "Invalid program status (there is an open modal dialog: …)").</summary>
    public static bool Busy(string message) => message.Contains("modal dialog", StringComparison.OrdinalIgnoreCase) || message.Contains("Invalid program status", StringComparison.OrdinalIgnoreCase)
        || message.StartsWith("Archicad is busy", StringComparison.Ordinal);

    /// <summary>What to tell the user when Archicad is busy with a dialog: which one, when it says.</summary>
    public static string BusyText(string message)
    {
        var m = System.Text.RegularExpressions.Regex.Match(message, @"modal dialog:\s*([^)]+)\)");
        return $"Archicad is busy: a dialog is open in it{(m.Success ? $" ({m.Groups[1].Value.Trim()})" : "")}. Close it in Archicad, then try again.";
    }

    static string ErrorText(JsonObject a) =>
        (string?)a["error"]?["message"] ?? (string?)a["result"]?["addOnCommandResponse"]?["error"]?["message"] ?? a["error"]?.ToJsonString() ?? "no reason given";
}
