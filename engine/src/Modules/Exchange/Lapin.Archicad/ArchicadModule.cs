using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Archicad;

/// <summary>
/// The Archicad connector (docs/plan-archicad.md, decision 882), stage A1: a running Archicad's model received through its JSON API
/// and the Tapir add-on. Queries: the Archicads running (<c>archicad.instances</c>), what a receive would do (<c>archicad.survey</c>),
/// whether Archicad's model changed since (<c>archicad.status</c>), its IFC translators and an IFC saved by it to a temporary file
/// for Send to Atrium (<c>archicad.translators</c>, <c>archicad.saveIfc</c>). The command <c>archicad.receive</c>, one undo step.
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

    static ArchicadModel ReadModel(ArchicadClient c, Action<string, int, int>? progress, CancellationToken ct)
    {
        try { return ArchicadRead.Read(c, progress, ct); }
        catch (OperationCanceledException) { throw new CommandException("Stopped: nothing was received."); }
    }

    public void Register(IModuleRegistry r)
    {
        r.Query("archicad.instances", (q, _) => new JsonArray([.. ArchicadClient.Instances(_transport, q.Cancellation).Select(i => (JsonNode)i.ToJson())]));

        r.Query("archicad.survey", (q, a) => ArchicadReceive.Survey(q.Model, ReadModel(Client(a, q.Cancellation), q.Progress, q.Cancellation), ArchicadReceiveOptions.From(a)));

        r.Query("archicad.status", (q, a) => ArchicadReceive.Status(q.Model, ReadModel(Client(a, q.Cancellation), null, q.Cancellation), ArchicadReceiveOptions.From(a).AsCopy));

        // {port?, storeys: "create" | "existing", recognize: true, origin: "project" | "survey", asCopy?}: one undo step.
        r.Command("receive", "Receive from Archicad", (ctx, a) =>
        {
            var options = ArchicadReceiveOptions.From(a);
            var model = ReadModel(Client(a, ctx.Query.Cancellation), ctx.Query.Progress, ctx.Query.Cancellation);
            try
            {
                var result = ArchicadReceive.Apply(ctx, model, options, ctx.Services, ctx.Query.Progress, ctx.Query.Cancellation).ToJson();
                result["project"] = model.Source.Label;
                result["readMs"] = Math.Round(model.Milliseconds);
                return result;
            }
            catch (OperationCanceledException) { throw new CommandException("Stopped: nothing was received."); }
        });

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
