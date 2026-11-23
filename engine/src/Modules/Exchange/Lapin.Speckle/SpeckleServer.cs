using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;
using Lapin.Platform;

namespace Lapin.Speckle;

/// <summary>The account a session talks to Speckle with: its server and who the token is; never the token itself.</summary>
public sealed record SpeckleAccount(string Server, string? Name, string? Email);

/// <summary>A Speckle project or model as the pickers list it.</summary>
public sealed record SpeckleItem(string Id, string Name, string? Role, DateTime UpdatedAt);

/// <summary>A version of a model as the receive dialog lists it: its message, the application that sent it, when and by whom.</summary>
public sealed record SpeckleVersionInfo(string Id, string? Message, string? SourceApplication, DateTime CreatedAt, string? Author, string? ReferencedObject);

/// <summary>A version fetched: where it came from, and its objects as Speckle's SDK reads them.</summary>
public sealed record SpeckleFetched(SpeckleSource Source, Base Root);

/// <summary>What a send made: the version, its model and where its page is on the server.</summary>
public sealed record SpeckleSent(string ProjectId, string ModelId, string VersionId, string RootId, string Url, int Objects);

/// <summary>
/// Speckle's server through its official SDK (docs/plan-speckle.md, S1): the account, its projects and models, a send.
/// The token is held in memory only, for as long as the engine runs: the interface keeps it in the system's secure
/// store (Keychain, Credential Manager, Secret Service) and gives it again when it starts; it never goes into the project,
/// a setting, a report or a log. The SDK keeps its own cache of the objects sent (its SQLite, in the user's Speckle folder),
/// never an account (we never add one to its account store).
/// </summary>
public sealed class SpeckleServer
{
    /// <summary>Speckle's public server, the default; any self-hosted one is given by its address.</summary>
    public const string DefaultServer = "https://app.speckle.systems";

    readonly object _lock = new();
    Account? _account;

    public SpeckleAccount? SignedIn
    {
        get { lock (_lock) return _account == null ? null : new SpeckleAccount(_account.serverInfo.url, _account.userInfo.name, _account.userInfo.email); }
    }

    static IServiceProvider Services => SpeckleSdk.Services;

    /// <summary>A server's address as Speckle takes it: http(s), no path, no trailing slash.</summary>
    public static string ServerOf(string? text)
    {
        var s = (text ?? "").Trim();
        if (s.Length == 0) s = DefaultServer;
        if (!s.Contains("://", StringComparison.Ordinal)) s = "https://" + s;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https") || string.IsNullOrEmpty(u.Host))
            throw new CommandException($"„{text}” is not a Speckle server's address (https://app.speckle.systems, or your firm's).");
        return u.GetLeftPart(UriPartial.Authority);
    }

    /// <summary>Signs in: the token checked against the server (who it is), then held for the calls that follow.</summary>
    public SpeckleAccount SignIn(string server, string token, CancellationToken ct)
    {
        server = ServerOf(server);
        if (string.IsNullOrWhiteSpace(token)) throw new CommandException("The token is missing: make one in Speckle, under your profile's Developer settings → Access tokens.");
        var account = new Account { token = token.Trim(), serverInfo = new() { url = server }, userInfo = new() };
        var me = Run(async () =>
        {
            using var client = Services.GetRequiredService<IClientFactory>().Create(account);
            return await client.ActiveUser.Get(ct).ConfigureAwait(false);
        }, "sign in", server) ?? throw new CommandException("The server did not accept the token: check it, and that it may read and write projects (streams:read, streams:write, profile:read).");
        account.userInfo = new UserInfo { id = me.id, name = me.name, email = me.email };
        lock (_lock) _account = account;
        return SignedIn!;
    }

    public void SignOut() { lock (_lock) _account = null; }

    Account Current() { lock (_lock) return _account ?? throw new CommandException("Sign in to Speckle first (File → Speckle → Send… or Receive…)."); }

    /// <summary>The projects the account may see, most recently changed first; those whose name has <paramref name="search"/>.</summary>
    public List<SpeckleItem> Projects(string? search, CancellationToken ct)
    {
        var account = Current();
        return Run(async () =>
        {
            using var client = Services.GetRequiredService<IClientFactory>().Create(account);
            var filter = string.IsNullOrWhiteSpace(search) ? null : new UserProjectsFilter(search.Trim(), null, null, null, null);
            var r = await client.ActiveUser.GetProjects(100, null, filter, ct).ConfigureAwait(false);
            return r.items.Select(p => new SpeckleItem(p.id, p.name, p.role, p.updatedAt)).ToList();
        }, "list the projects", account.serverInfo.url);
    }

    /// <summary>The models of a project, most recently changed first.</summary>
    public List<SpeckleItem> Models(string projectId, CancellationToken ct)
    {
        var account = Current();
        return Run(async () =>
        {
            using var client = Services.GetRequiredService<IClientFactory>().Create(account);
            var r = await client.Model.GetModels(projectId, 100, null, null, ct).ConfigureAwait(false);
            return r.items.Select(x => new SpeckleItem(x.id, x.name, null, x.updatedAt)).ToList();
        }, "list the models", account.serverInfo.url);
    }

    /// <summary>A model's versions, the newest first (50 at most).</summary>
    public List<SpeckleVersionInfo> Versions(string projectId, string modelId, CancellationToken ct)
    {
        var account = Current();
        return Run(async () =>
        {
            using var client = Services.GetRequiredService<IClientFactory>().Create(account);
            var r = await client.Version.GetVersions(modelId, projectId, 50, null, null, ct).ConfigureAwait(false);
            return r.items.Select(Info).ToList();
        }, "list the versions", account.serverInfo.url);
    }

    static SpeckleVersionInfo Info(global::Speckle.Sdk.Api.GraphQL.Models.Version v) =>
        new(v.id, v.message, v.sourceApplication, v.createdAt, v.authorUser?.name, v.referencedObject);

    /// <summary>
    /// A version's objects downloaded (the model's latest when no <paramref name="versionId"/>), through the SDK's cache; the
    /// version then marked received by Lapin on the server (what Speckle shows in its history), if the server takes it.
    /// </summary>
    public SpeckleFetched Fetch(string projectId, string modelId, string? versionId, Action<string, int, int>? progress, CancellationToken ct)
    {
        var account = Current();
        var server = account.serverInfo.url;
        return Run(async () =>
        {
            using var client = Services.GetRequiredService<IClientFactory>().Create(account);
            var model = await client.Model.Get(modelId, projectId, ct).ConfigureAwait(false);
            global::Speckle.Sdk.Api.GraphQL.Models.Version version;
            if (string.IsNullOrEmpty(versionId))
                version = (await client.Version.GetVersions(modelId, projectId, 1, null, null, ct).ConfigureAwait(false)).items.FirstOrDefault()
                    ?? throw new CommandException($"The model „{model.name}” has no version yet.");
            else version = await client.Version.Get(versionId, projectId, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(version.referencedObject)) throw new CommandException("That version holds no objects.");
            progress?.Invoke("Downloading from Speckle", 0, 0);
            // Receive2 reads every version a 2.x server holds, and a newer server's bundles as a best-effort tree; the SDK marks it
            // legacy in favour of Receive3, which reads bundles only (Speckle 2026.9 on), so not what a self-hosted 2.x server has.
#pragma warning disable CS0618
            var root = await Services.GetRequiredService<IOperations>().Receive2(new Uri(server), projectId, version.referencedObject, account.token, new Progress(progress), ct).ConfigureAwait(false);
#pragma warning restore CS0618
            try { await client.Version.Received(new MarkReceivedVersionInput(version.id, projectId, SpeckleConvert.SourceApplication, null), ct).ConfigureAwait(false); }
            catch (Exception) { /* only Speckle's history: a server that refuses it receives just the same */ }
            return new SpeckleFetched(new SpeckleSource(server, projectId, modelId, model.name, version.id, version.sourceApplication, version.message), root);
        }, "receive", server, "Stopped: nothing was received.");
    }

    /// <summary>
    /// <paramref name="root"/> sent to a project's model (one made, named <paramref name="newModel"/>, when no
    /// <paramref name="modelId"/>), then a version of it with <paramref name="message"/>; how the upload goes to
    /// <paramref name="progress"/> (stage, done, total).
    /// </summary>
    public SpeckleSent Send(Base root, string projectId, string? modelId, string? newModel, string? message, Action<string, int, int>? progress, CancellationToken ct)
    {
        var account = Current();
        var server = account.serverInfo.url;
        return Run(async () =>
        {
            using var client = Services.GetRequiredService<IClientFactory>().Create(account);
            if (string.IsNullOrEmpty(modelId))
            {
                var name = (newModel ?? "").Trim();
                if (name.Length == 0) throw new CommandException("Choose a model, or name a new one.");
                modelId = (await client.Model.Create(new CreateModelInput(name, null, projectId), ct).ConfigureAwait(false)).id;
            }
            progress?.Invoke("Sending to Speckle", 0, 0);
            // Send2 and Version.Create are what every Speckle server takes (2.x, as the self-hosted ones run, and the newer);
            // the SDK marks them legacy for servers from 2026.9 on, where the version is made once the server has ingested
            // the objects (the link works a moment later). Its ingestion API (SendPipeline, client.Ingestion) is not on 2.x
            // servers: moving to it, with this as the fallback, is left for when it is checked against such a server.
#pragma warning disable CS0618
            var sent = await Services.GetRequiredService<IOperations>().Send2(new Uri(server), projectId, account.token, root, new Progress(progress), ct).ConfigureAwait(false);
            progress?.Invoke("Making the version", 0, 0);
            var version = await client.Version.Create(new CreateVersionInput(sent.RootId, modelId, projectId, string.IsNullOrWhiteSpace(message) ? null : message.Trim(), SpeckleConvert.SourceApplication, null, null), ct).ConfigureAwait(false);
#pragma warning restore CS0618
            return new SpeckleSent(projectId, modelId, version.id, sent.RootId, VersionUrl(server, projectId, modelId, version.id), sent.ConvertedReferences.Count);
        }, "send", server);
    }

    /// <summary>A version's page on its server (Speckle's web viewer).</summary>
    public static string VersionUrl(string server, string projectId, string modelId, string versionId) =>
        $"{server.TrimEnd('/')}/projects/{Uri.EscapeDataString(projectId)}/models/{Uri.EscapeDataString(modelId)}@{Uri.EscapeDataString(versionId)}";

    /// <summary>Whether <paramref name="url"/> is a page of the server signed in to (the only one Lapin opens for it).</summary>
    public bool IsOwnPage(string? url)
    {
        if (url == null || !Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        string? server;
        lock (_lock) server = _account?.serverInfo.url;
        return server != null && u.GetLeftPart(UriPartial.Authority) == server && u.AbsolutePath.StartsWith("/projects/", StringComparison.Ordinal);
    }

    /// <summary>
    /// The SDK's async work, waited for on the engine's thread (one call at a time, as every call is); what goes wrong is told
    /// as a refusal the user reads, never with the token in it.
    /// </summary>
    static T Run<T>(Func<Task<T>> work, string what, string server, string stopped = "Stopped: nothing more was sent.")
    {
        try { return Task.Run(work).GetAwaiter().GetResult(); }
        catch (CommandException) { throw; }
        catch (OperationCanceledException) { throw new CommandException(stopped); }
        catch (Exception ex)
        {
            var inner = ex is AggregateException { InnerException: { } i } ? i : ex;
            var why = inner switch
            {
                HttpRequestException h when h.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                    "the server refused the token (check it, and its scopes: streams:read, streams:write, profile:read)",
                HttpRequestException => $"the server could not be reached ({inner.Message})",
                _ when inner.Message.Contains("FORBIDDEN", StringComparison.OrdinalIgnoreCase) || inner.Message.Contains("not authorized", StringComparison.OrdinalIgnoreCase) =>
                    "the server refused (the token may not reach this project, or lacks streams:write)",
                _ => inner.Message,
            };
            throw new CommandException($"Speckle ({server}) could not {what}: {why}");
        }
    }

    /// <summary>The SDK's progress (objects serialised, bytes uploaded) as the engine's.</summary>
    sealed class Progress(Action<string, int, int>? sink) : IProgress<ProgressArgs>
    {
        public void Report(ProgressArgs value)
        {
            if (sink == null) return;
            var stage = value.ProgressEvent switch
            {
                ProgressEvent.UploadBytes or ProgressEvent.UploadingObjects => "Uploading to Speckle",
                ProgressEvent.FindingChildren => "Gathering the objects",
                ProgressEvent.DownloadBytes or ProgressEvent.DownloadObjects or ProgressEvent.CacheCheck => "Downloading from Speckle",
                ProgressEvent.DeserializeObject => "Reading the objects",
                _ => "Preparing the objects",
            };
            sink(stage, (int)Math.Min(value.Count, int.MaxValue), (int)Math.Min(value.Total ?? 0, int.MaxValue));
        }
    }
}
