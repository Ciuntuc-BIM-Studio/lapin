using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.ConvergeIfc;

/// <summary>
/// The Converge IFC connector (docs/plan-converge-ifc.md): the project sent as IFC to a Converge IFC server (an ISO 19650 common data
/// environment, ifc-hub) as a new file or a new version of one, and a file's version fetched from it for the IFC import. The IFC itself is
/// made and read by the engine's own export and import (<c>project.exportIfc</c>, <c>project.importIfc</c>, by a path), so this module
/// only carries files: every call is a query, changing nothing of the project. None is a guest's to make (EngineDispatcher.HostOnlyQueries:
/// the account is the host's). Desktop only (module.json).
/// </summary>
public sealed class ConvergeIfcModule : ILapinModule
{
    public const string ModuleId = "convergeifc";
    public string Id => ModuleId;
    public string Name => "Converge IFC";

    /// <summary>The account of this engine, in memory only (<see cref="ConvergeServer"/>).</summary>
    readonly ConvergeServer _server;

    public ConvergeIfcModule() : this(null) { }

    /// <summary>With the handler the requests go through (the tests' way in, with no network).</summary>
    internal ConvergeIfcModule(HttpMessageHandler? handler) => _server = new ConvergeServer(handler);

    public void Register(IModuleRegistry r)
    {
        // Who is signed in, on which server: the accounts of this session (one at most), never their tokens.
        r.Query("convergeifc.accounts", (_, _) => new JsonArray(_server.SignedIn is { } a ? [AccountJson(a)] : []));

        // Signs in with a personal API token (the server's account menu → API tokens), checked against the server; held in memory for
        // this session, the interface keeping it in the system's secure store.
        r.Query("convergeifc.setAccount", (q, a) => AccountJson(_server.SignIn((string?)a["server"], (string?)a["token"], q.Cancellation)));

        r.Query("convergeifc.forgetAccount", (_, _) =>
        {
            _server.SignOut();
            return new JsonObject { ["signedOut"] = true };
        });

        // The projects the account belongs to; canUpload says whether its roles there let it send.
        r.Query("convergeifc.projects", (q, _) =>
            new JsonArray(_server.Projects(q.Cancellation).Select(p => (JsonNode)new JsonObject
            {
                ["id"] = p.Id, ["code"] = p.Code, ["name"] = p.Name, ["canUpload"] = p.CanUpload,
                ["roles"] = new JsonArray(p.Roles.Select(x => (JsonNode)x).ToArray()),
            }).ToArray()));

        // A project's IFC files with their current version: what the pickers offer, to add a version to or to fetch from.
        r.Query("convergeifc.files", (q, a) =>
            new JsonArray(_server.Files(Required(a, "projectId"), q.Cancellation).Select(f => (JsonNode)new JsonObject
            {
                ["id"] = f.Id, ["fileName"] = f.FileName, ["versionNo"] = f.VersionNo, ["revisionCode"] = f.RevisionCode, ["status"] = f.Status,
                ["sha256"] = f.Sha256, ["sizeBytes"] = f.SizeBytes, ["updatedAt"] = f.UpdatedAt?.ToString("o"),
            }).ToArray()));

        // A file's versions, the newest first: what the fetch offers (the current one by default).
        r.Query("convergeifc.versions", (q, a) =>
            new JsonArray(_server.Versions(Required(a, "containerId"), q.Cancellation).Select(v => (JsonNode)new JsonObject
            {
                ["versionNo"] = v.VersionNo, ["revisionCode"] = v.RevisionCode, ["sha256"] = v.Sha256, ["sizeBytes"] = v.SizeBytes,
                ["uploadedAt"] = v.UploadedAt?.ToString("o"), ["uploadedBy"] = v.UploadedBy, ["isCurrent"] = v.IsCurrent,
            }).ToArray()));

        // A path to export the IFC to before a send: a folder of its own under the system's temporary one, the file named as given.
        r.Query("convergeifc.tempPath", (_, a) => new JsonObject { ["path"] = ConvergeServer.TempPath((string?)a["name"]) });

        // The file at `path` (made by project.exportIfc) sent to a project: a new version of `containerId`, or a new file named `fileName`;
        // nothing is sent when the server already holds these bytes as the current version (`unchanged`). `discard`: the file removed after,
        // when it is one this module made (tempPath).
        r.Query("convergeifc.send", (q, a) =>
        {
            var path = HostFiles.Check(Required(a, "path"));
            try
            {
                var s = _server.Send(Required(a, "projectId"), path, (string?)a["fileName"], (string?)a["containerId"] is { Length: > 0 } c ? c : null, q.Progress, q.Cancellation);
                return new JsonObject
                {
                    ["id"] = s.Id, ["fileName"] = s.FileName, ["versionNo"] = s.VersionNo, ["revisionCode"] = s.RevisionCode, ["sha256"] = s.Sha256,
                    ["created"] = s.Created, ["unchanged"] = s.Unchanged,
                };
            }
            finally { if (a["discard"] is JsonValue d && d.TryGetValue<bool>(out var yes) && yes) ConvergeServer.Discard(path); }
        });

        // A version (the file's current one when versionNo is left out) downloaded to a file of this computer, its checksum checked (an
        // .ifczip unpacked): to `path`, the place the user chose, else to a file of this module's own (discard); the interface then hands
        // it to the IFC import.
        r.Query("convergeifc.fetch", (q, a) =>
        {
            var to = (string?)a["path"] is { Length: > 0 } p ? HostFiles.Check(p) : null;
            var f = _server.Fetch(Required(a, "containerId"), a["versionNo"] is JsonValue v && v.TryGetValue<int>(out var n) ? n : null, q.Progress, q.Cancellation, to);
            return new JsonObject
            {
                ["path"] = f.Path, ["fileName"] = f.FileName, ["sha256"] = f.Sha256, ["versionNo"] = f.VersionNo, ["revisionCode"] = f.RevisionCode,
            };
        });

        // A file this module made or fetched removed (any other path is left alone).
        r.Query("convergeifc.discard", (_, a) => new JsonObject { ["discarded"] = ConvergeServer.Discard((string?)a["path"]) });
    }

    static string Required(JsonObject a, string key) => (string?)a[key] is { Length: > 0 } v ? v : throw new CommandException($"Which {key}?");

    static JsonObject AccountJson(ConvergeAccount a) => new() { ["server"] = a.Server, ["name"] = a.Name, ["email"] = a.Email };
}
