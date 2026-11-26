using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Host;

/// <summary>
/// The engine without its window (docs/headless-run.md): a job opens a project (or starts a new one), runs its steps (queries
/// and commands of the engine's protocol, as a guest editor would call them) and answers what each gave, then ends. A CDE server
/// runs a commit's clash tests this way, with the same modules and the same code as the desktop; tools and tests make projects
/// with it. Nothing is written but what the job asks for (<c>saveAs</c>): no recovery journal, no copy of the project, no port.
/// <code>
/// {"open": "/path/project.lapin" | null, "steps": [{"method": "doc.execute"|"doc.query"|"session.info"|"session.catalogue", "params": {…}}],
///  "saveAs": "/path/new.lapin"?}
/// </code>
/// A step's params may take an earlier step's answer: <c>{"$step": 0, "path": "id"}</c> anywhere in them is replaced by step 0's
/// result at that path (members by name, array items by number, dotted). The answer:
/// <code>
/// {"ok": true, "engine": {"version", "product", "modules"}, "open": {"notes", "absent"} | null,
///  "steps": [{"method", "result"} | {"method", "error"}], "saved": "/path"?, "milliseconds": n}
/// </code>
/// A step that fails does not stop the others (a step taking its answer fails too). <c>ok</c> is false, with <c>error</c>, only
/// when the job itself could not run: unreadable, a project that does not open, a save that failed.
/// </summary>
public static class HeadlessRun
{
    /// <summary>The methods a step may call: the model's queries and commands, and what the engine is. Never a file, a port or the history.</summary>
    public static readonly string[] Methods = ["doc.query", "doc.execute", "session.info", "session.catalogue"];

    /// <summary>Who a job's steps run as: a guest editor, so the dispatcher's guest rules apply on top (no host file named, no script, no network).</summary>
    public static readonly Caller Runner = new("headless", "editor");

    public static async Task<JsonObject> Run(Session session, ProjectFiles files, JsonObject job)
    {
        var clock = Stopwatch.StartNew();
        var dispatcher = new EngineDispatcher(session, files);
        var answer = new JsonObject
        {
            ["ok"] = true,
            ["engine"] = new JsonObject
            {
                ["version"] = Session.AppVersion, ["product"] = session.Product.Id,
                ["modules"] = new JsonArray([.. session.Catalog.Modules.Select(m => (JsonNode)m.Id)]),
            },
            ["open"] = null,
        };
        JsonObject Failed(string why)
        {
            answer["ok"] = false;
            answer["error"] = why;
            answer["milliseconds"] = clock.ElapsedMilliseconds;
            return answer;
        }

        // The project: opened as the desktop opens it, or a new one (plain: one ground floor, as an IFC opened as a project starts).
        var open = (string?)job["open"];
        var call = open != null
            ? new JsonObject { ["method"] = "project.open", ["params"] = new JsonObject { ["path"] = open } }
            : new JsonObject { ["method"] = "project.new", ["params"] = new JsonObject { ["plain"] = job["plain"] is JsonValue pv && pv.TryGetValue<bool>(out var plain) && plain } };
        var (opened, openError) = await Call(dispatcher, call, Caller.Local);
        if (openError != null) return Failed(open != null ? $"The project did not open: {openError}" : $"No new project: {openError}");
        if (open != null) answer["open"] = opened is JsonObject o ? new JsonObject { ["notes"] = o["notes"]?.DeepClone(), ["absent"] = o["absent"]?.DeepClone() } : null;

        var results = new List<JsonNode?>();
        var failed = new HashSet<int>();
        var steps = new JsonArray();
        var list = job["steps"] as JsonArray ?? [];
        for (var i = 0; i < list.Count; i++)
        {
            var step = list[i] as JsonObject;
            var method = (string?)step?["method"] ?? "";
            var o = new JsonObject { ["method"] = method };
            try
            {
                if (!Methods.Contains(method)) throw new CommandException($"A job's step calls one of {string.Join(", ", Methods)}.");
                var p = Resolve(step!["params"]?.DeepClone() ?? new JsonObject(), results, failed) as JsonObject ?? throw new CommandException("A step's params are an object.");
                var (result, error) = await Call(dispatcher, new JsonObject { ["id"] = i, ["method"] = method, ["params"] = p }, Runner);
                if (error != null) throw new CommandException(error);
                o["result"] = result;
                results.Add(result);
            }
            catch (CommandException ex)
            {
                o["error"] = ex.Message;
                failed.Add(i);
                results.Add(null);
            }
            steps.Add(o);
        }
        answer["steps"] = steps;

        if ((string?)job["saveAs"] is { Length: > 0 } saveAs)
        {
            var (_, saveError) = await Call(dispatcher, new JsonObject { ["method"] = "project.save", ["params"] = new JsonObject { ["path"] = Path.GetFullPath(saveAs) } }, Caller.Local);
            if (saveError != null) return Failed($"The project was not saved: {saveError}");
            answer["saved"] = Path.GetFullPath(saveAs);
        }
        answer["milliseconds"] = clock.ElapsedMilliseconds;
        return answer;
    }

    /// <summary>One call through the dispatcher, as the window's go: its result (JSON), or why it failed.</summary>
    static async Task<(JsonNode? Result, string? Error)> Call(EngineDispatcher dispatcher, JsonObject call, Caller caller)
    {
        call["id"] ??= 0;
        var (frame, _) = await dispatcher.HandleAsync(call.ToJsonString(), caller);
        var reply = JsonNode.Parse(BinaryFrame.HeaderText(frame)) as JsonObject ?? throw new InvalidOperationException("The engine's reply is not an object.");
        if ((string?)reply["error"] is { } error) return (null, error);
        return (reply["result"]?.DeepClone(), null);
    }

    /// <summary>Every <c>{"$step": n, "path": "a.b"}</c> in <paramref name="node"/> replaced by step n's answer at that path.</summary>
    public static JsonNode? Resolve(JsonNode? node, IReadOnlyList<JsonNode?> results, IReadOnlySet<int> failed)
    {
        switch (node)
        {
            case JsonObject o when o.ContainsKey("$step"):
            {
                var n = o["$step"] is JsonValue v && v.TryGetValue<int>(out var k) ? k : throw new CommandException("$step names an earlier step by its number.");
                if (n < 0 || n >= results.Count) throw new CommandException($"$step {n} is not an earlier step.");
                if (failed.Contains(n)) throw new CommandException($"Step {n}, whose answer this step takes, failed.");
                var at = results[n];
                foreach (var part in ((string?)o["path"] ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries))
                    at = at switch
                    {
                        JsonObject x => x[part],
                        JsonArray a when int.TryParse(part, out var i) && i >= 0 && i < a.Count => a[i],
                        _ => throw new CommandException($"Step {n}'s answer has nothing at {o["path"]}."),
                    };
                return at?.DeepClone();
            }
            case JsonObject o:
            {
                var copy = new JsonObject();
                foreach (var (key, value) in o) copy[key] = Resolve(value, results, failed);
                return copy;
            }
            case JsonArray a:
                return new JsonArray([.. a.Select(x => Resolve(x, results, failed))]);
            default:
                return node?.DeepClone();
        }
    }

    /// <summary>
    /// <c>Lapin.Host --product &lt;p&gt; --modules-dir &lt;d&gt; --run &lt;job.json | -&gt; [--out &lt;answer.json&gt;]</c>: the job read from the
    /// file (or standard input), its answer written to the file (or standard output). Exit code 0 when the job ran (its steps may
    /// have failed: see each), 1 when it could not.
    /// </summary>
    public static async Task<int> RunFile(Session session, string jobPath, string? outPath)
    {
        JsonObject answer;
        try
        {
            var text = jobPath == "-" ? await Console.In.ReadToEndAsync() : await File.ReadAllTextAsync(jobPath);
            var job = JsonNode.Parse(text) as JsonObject ?? throw new CommandException("A job is a JSON object.");
            answer = await Run(session, PackageFiles.Create(), job);
        }
        catch (Exception ex) when (ex is CommandException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            answer = new JsonObject { ["ok"] = false, ["error"] = ex.Message };
        }
        var json = answer.ToJsonString();
        if (outPath != null) await File.WriteAllTextAsync(outPath, json);
        else { Console.Out.Write(json); Console.Out.Flush(); }
        return answer["ok"] is JsonValue ok && ok.TryGetValue<bool>(out var b) && b ? 0 : 1;
    }
}
