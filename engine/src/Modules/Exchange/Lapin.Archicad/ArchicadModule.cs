using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Archicad;

/// <summary>
/// The Archicad connector (docs/plan-archicad.md, decision 882), stage A1: a running Archicad's model received through its JSON API
/// and the Tapir add-on. Queries: the Archicads running (<c>archicad.instances</c>), what a receive would do (<c>archicad.survey</c>),
/// whether Archicad's model changed since (<c>archicad.status</c>), its Project Info (<c>archicad.projectInfo</c>, A1b), its IFC translators and an IFC saved by it to a temporary file
/// for Send to Atrium (<c>archicad.translators</c>, <c>archicad.saveIfc</c>). The commands <c>archicad.receive</c> and <c>archicad.update</c>
/// (Get latest from Archicad, A2: only what changed since the state captured at the last receive), each one undo step.
/// None is a guest's to make (EngineDispatcher.HostOnlyQueries and HostOnlyCommands: Archicad runs on the host's computer).
/// Desktop only (module.json).
/// </summary>
public sealed class ArchicadModule : ILapinModule
{
    public const string ModuleId = "archicad";
    public string Id => ModuleId;
    public string Name => "Archicad";

    IArchicadTransport _transport;

    /// <summary>How Archicad is reached (the tests' recorded answers swapped for others: Archicad read anew).</summary>
    internal IArchicadTransport Transport { get => _transport; set => _transport = value; }

    public ArchicadModule() : this(new HttpTransport()) { }

    /// <summary>A module that reaches Archicad through <paramref name="transport"/> (the tests' recorded answers).</summary>
    public ArchicadModule(IArchicadTransport transport) => _transport = transport;

    /// <summary>The Archicad asked for (its port), else the one running when there is one, else why not.</summary>
    ArchicadClient Client(JsonObject a, CancellationToken ct)
    {
        if (Json.Number(a["port"]) is { } p)
        {
            var port = (int)p;
            if (port < ArchicadClient.FirstPort || port > ArchicadClient.LastPort) throw new CommandException($"Archicad listens on ports {ArchicadClient.FirstPort}–{ArchicadClient.LastPort}.");
            return new ArchicadClient(_transport, port);
        }
        var all = ArchicadClient.Instances(_transport, ct);
        return all.Count switch
        {
            0 => throw new CommandException("No Archicad is running on this computer (with its JSON API on ports 19723–19744): open Archicad with your project."),
            1 => new ArchicadClient(_transport, all[0].Port),
            _ => throw new CommandException($"{all.Count} Archicads are running: choose one (port)."),
        };
    }

    /// <summary>The Archicad an update asks: the port given, else the most recent link's when it answers there, else the one running.</summary>
    ArchicadClient UpdateClient(JsonObject a, ICopyState? copy, CancellationToken ct)
    {
        if (Json.Number(a["port"]) is not null) return Client(a, ct);
        foreach (var l in ArchicadLink.All(copy).Values.OrderByDescending(l => l.At, StringComparer.Ordinal))
            if (_transport.Post(l.Port, new JsonObject { ["command"] = "API.IsAlive" }, TimeSpan.FromSeconds(2), ct)?["result"]?["isAlive"] is JsonValue v && v.TryGetValue<bool>(out var alive) && alive)
                return new ArchicadClient(_transport, l.Port);
        return Client(a, ct);
    }

    static ArchicadModel ReadModel(ArchicadClient c, Action<string, int, int>? progress, CancellationToken ct)
    {
        try { return ArchicadRead.Read(c, progress, ct); }
        catch (OperationCanceledException) { throw new CommandException("Stopped: nothing was received."); }
    }

    public void Register(IModuleRegistry r)
    {
        r.Query("archicad.instances", (q, _) => new JsonArray([.. ArchicadClient.Instances(_transport, q.Cancellation).Select(i => (JsonNode)i.ToJson())]));

        r.Query("archicad.survey", (q, a) => ArchicadReceive.Survey(q.Model, ReadModel(Client(a, q.Cancellation), q.Progress, q.Cancellation), ArchicadReceiveOptions.From(a)));

        // Cheap (A2): Archicad's changes since the link's state counted, no element read; {full: true}: the whole model read and compared by hash.
        r.Query("archicad.status", (q, a) => a["full"] is JsonValue fv && fv.TryGetValue<bool>(out var full) && full
            ? ArchicadReceive.Status(q.Model, ReadModel(Client(a, q.Cancellation), null, q.Cancellation), ArchicadReceiveOptions.From(a).AsCopy)
            : ArchicadUpdate.Status(_transport, q.CopyState, q.Model, Json.Number(a["port"]) is { } sp ? (int)sp : null, q.Cancellation));

        // {port?, storeys: "create" | "existing", recognize: true, origin: "project" | "survey", asCopy?, georeference: true}: one undo step.
        // Archicad's state is captured before the read and kept as the model's link (A2), so Get latest asks only what changed since.
        r.Command("receive", "Receive from Archicad", (ctx, a) =>
        {
            var options = ArchicadReceiveOptions.From(a);
            var c = Client(a, ctx.Query.Cancellation);
            var notes = new List<string>();
            var state = ArchicadUpdate.Capture(c, ctx.Query.Cancellation, notes);
            try
            {
                var model = ReadModel(c, ctx.Query.Progress, ctx.Query.Cancellation);
                var result = ArchicadReceive.Apply(ctx, model, options, ctx.Services, ctx.Query.Progress, ctx.Query.Cancellation).ToJson();
                result["project"] = model.Source.Label;
                result["readMs"] = Math.Round(model.Milliseconds);
                if (notes.Count > 0 && result["notes"] is JsonArray n) foreach (var x in notes) n.Add((JsonNode)x);
                ArchicadUpdate.Linked(ctx, c, model, options, state);
                result["linked"] = state != null && ctx.Query.CopyState != null;
                return result;
            }
            catch (OperationCanceledException) { ArchicadUpdate.Release(c, state); throw new CommandException("Stopped: nothing was received."); }
            catch { ArchicadUpdate.Release(c, state); throw; }
        });

        // Get latest from Archicad (A2): {port?, asCopy?}: only what changed since the link's state, else the whole model compared; one undo step.
        r.Command("update", "Get latest from Archicad", (ctx, a) => ArchicadUpdate.Run(ctx, UpdateClient(a, ctx.Query.CopyState, ctx.Query.Cancellation), a));

        // Archicad's Project Info as Tapir gives it (raw, for Send to Atrium's proposal) and what taking it would do here (A1b).
        r.Query("archicad.projectInfo", (q, a) => ArchicadProjectInfo.Read(Client(a, q.Cancellation), q.Model, q.Cancellation));

        r.Query("archicad.translators", (q, a) =>
        {
            var c = Client(a, q.Cancellation);
            return new JsonArray([.. (c.Tapir("GetIFCExportTranslators", null, q.Cancellation)?["translators"] as JsonArray ?? []).OfType<JsonObject>()
                .Select(t => (JsonNode)new JsonObject { ["name"] = (string?)t["name"], ["preview"] = t["preview"] is JsonValue v && v.TryGetValue<bool>(out var b) && b })]);
        });

        // Send to Atrium's first step: Archicad saves the open model as IFC (with the translator asked for) to a temporary file, whose
        // bytes come back (the interface puts them on the project's server as a reference model) and which is then deleted.
        r.Query("archicad.saveIfc", (q, a) => SaveIfc(Client(a, q.Cancellation), (string?)a["translator"], q.Progress, q.Cancellation));
    }

    internal static JsonObject SaveIfc(ArchicadClient c, string? translator, Action<string, int, int>? progress, CancellationToken ct)
    {
        var me = c.Describe(ct);
        if (!me.Ok) throw new CommandException(me.Problem!);
        var folder = Path.Combine(Path.GetTempPath(), "lapin-archicad");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"{Guid.NewGuid():N}.ifc");
        progress?.Invoke("Archicad is saving the IFC", 0, 0);
        var p = new JsonObject { ["method"] = "save", ["ifcFilePath"] = file, ["fileType"] = "ifc" };
        if (translator is { Length: > 0 }) p["translatorName"] = translator;
        try
        {
            try { c.Tapir("IFCFileOperation", p, ct, TimeSpan.FromMinutes(30)); }
            catch (CommandException ex)
            {
                throw new CommandException($"Archicad did not save the IFC ({ex.Message.Replace("Archicad refused IFCFileOperation: ", "")}). A demo or education copy of Archicad may refuse to save; "
                    + "in that case receive the model instead (File → Archicad → Receive…), then File → Collaborate → Save to server.");
            }
            if (!File.Exists(file) || new FileInfo(file).Length == 0)
                throw new CommandException("Archicad answered, but wrote no IFC file. A demo or education copy of Archicad may refuse to save: receive the model instead (File → Archicad → Receive…), then File → Collaborate → Save to server.");
            var bytes = File.ReadAllBytes(file);
            var name = (me.Project is { Length: > 0 } n ? n : "Archicad model") + ".ifc";
            return new JsonObject
            {
                ["name"] = string.Concat(name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)), ["size"] = bytes.LongLength,
                ["sha256"] = Lapin.Model.Hashes.Sha256Hex(bytes), ["data"] = Convert.ToBase64String(bytes), ["translator"] = translator,
            };
        }
        finally { try { if (File.Exists(file)) File.Delete(file); } catch (IOException) { } }
    }
}
