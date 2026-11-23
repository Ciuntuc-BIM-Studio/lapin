using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Speckle;

/// <summary>
/// The Speckle connector (docs/plan-speckle.md, decision 804), stage S1: the model sent to a Speckle server as a new
/// version of a model, its elements Speckle data objects keyed by their GlobalIds. Every call is a query: sending changes
/// nothing of the project (no undo step), and none is a guest's to make (EngineDispatcher.HostOnlyQueries: the account
/// is the host's). Desktop only (module.json).
/// </summary>
public sealed class SpeckleModule : ILapinModule
{
    public const string ModuleId = "speckle";
    public string Id => ModuleId;
    public string Name => "Speckle";

    /// <summary>The account of this engine, in memory only (<see cref="SpeckleServer"/>).</summary>
    readonly SpeckleServer _server = new();

    public void Register(IModuleRegistry r)
    {
        // Who is signed in, on which server: the accounts of this session (one at most), never their tokens.
        r.Query("speckle.accounts", (_, _) => new JsonArray(_server.SignedIn is { } a ? [AccountJson(a)] : []));

        // Signs in with a personal access token (Speckle → profile → Developer settings → Access tokens), checked against the
        // server; held in memory for this session, the interface keeping it in the system's secure store.
        r.Query("speckle.setAccount", (q, a) =>
        {
            var server = (string?)a["server"];
            var token = (string?)a["token"];
            return AccountJson(_server.SignIn(server ?? SpeckleServer.DefaultServer, token ?? "", q.Cancellation));
        });

        r.Query("speckle.forgetAccount", (_, _) =>
        {
            _server.SignOut();
            return new JsonObject { ["signedOut"] = true };
        });

        // The projects the account sees (search: those whose name has it), most recently changed first.
        r.Query("speckle.projects", (q, a) =>
            new JsonArray(_server.Projects((string?)a["search"], q.Cancellation).Select(p => (JsonNode)ItemJson(p)).ToArray()));

        r.Query("speckle.models", (q, a) =>
        {
            var projectId = (string?)a["projectId"] is { Length: > 0 } p ? p : throw new CommandException("Which project (projectId)?");
            return new JsonArray(_server.Models(projectId, q.Cancellation).Select(m => (JsonNode)ItemJson(m)).ToArray());
        });

        // What a send would take, counted, no network: elements, meshes, materials, triangles and what is left out.
        r.Query("speckle.preview", (q, a) => Summary(SpeckleConvert.Convert(q, SpeckleSendScope.From(a))));

        // The model sent: the whole model, the 3D view in front as it shows it (viewId, hidden, planes), or the selection (ids);
        // to a project's model (modelId) or a new one (modelName), as a version with its message. Answers the version and its page.
        r.Query("speckle.send", (q, a) =>
        {
            var projectId = (string?)a["projectId"] is { Length: > 0 } p ? p : throw new CommandException("Which project (projectId)?");
            var modelId = (string?)a["modelId"] is { Length: > 0 } mid ? mid : null;
            var modelName = (string?)a["modelName"];
            var message = (string?)a["message"];
            if (_server.SignedIn == null) throw new CommandException("Sign in to Speckle first.");
            q.Progress?.Invoke("Converting the model", 0, 0);
            var conversion = SpeckleConvert.Convert(q, SpeckleSendScope.From(a));
            var sent = _server.Send(conversion.Root, projectId, modelId, modelName, message, q.Progress, q.Cancellation);
            var o = Summary(conversion);
            o["projectId"] = sent.ProjectId;
            o["modelId"] = sent.ModelId;
            o["versionId"] = sent.VersionId;
            o["rootId"] = sent.RootId;
            o["url"] = sent.Url;
            o["objects"] = sent.Objects;
            return o;
        });

        // A version's page in the system's browser: only a page of the server signed in to.
        r.Query("speckle.openVersion", (_, a) =>
        {
            var url = (string?)a["url"];
            if (!_server.IsOwnPage(url)) throw new CommandException("Only a page of the Speckle server signed in to is opened.");
            Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true });
            return new JsonObject { ["opened"] = true };
        });
    }

    static JsonObject AccountJson(SpeckleAccount a) => new() { ["server"] = a.Server, ["name"] = a.Name, ["email"] = a.Email };

    static JsonObject ItemJson(SpeckleItem i) => new()
    {
        ["id"] = i.Id, ["name"] = i.Name, ["role"] = i.Role, ["updatedAt"] = i.UpdatedAt.ToUniversalTime().ToString("o"),
    };

    static JsonObject Summary(SpeckleConversion c) => new()
    {
        ["elements"] = c.Elements, ["meshes"] = c.Meshes, ["materials"] = c.Materials, ["triangles"] = c.Triangles,
        ["notes"] = new JsonArray(c.Notes.Select(n => (JsonNode)n).ToArray()),
    };
}
