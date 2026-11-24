using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Package;
using Lapin.Package.Remote;
using Lapin.Platform;
using Lapin.Platform.Cde.Remarks;

namespace Lapin.Protocol;

/// <summary>
/// The project's history and its remote (lapin-remote 1.0, docs/spec-lapin-remote.md; decision 793, stage S1): <c>history.*</c>
/// (status, log, commit) and <c>remote.*</c> (add, remove, list, projects, signIn, changePassword, fetch, pull, push, clone). None is a guest's
/// (<see cref="AllowedRemote"/> lets none through): only the host speaks to the remote. A server's tokens come with each call
/// (<c>token</c>, <c>refreshToken</c>) and go back when a refresh replaced them (<c>tokens</c>); the engine never keeps them.
/// The author is the caller's <c>author</c> ({id, name}: <c>local:&lt;installation&gt;</c>, or <c>atrium:&lt;user&gt;</c> when signed in).
/// </summary>
public sealed partial class EngineDispatcher
{
    /// <summary>
    /// The HTTP client the server remotes use (tests give one over a fake handler). Its field is an <see cref="object"/>, and the shared
    /// client lives in a class of its own (<see cref="SharedHttp"/>), so that no field of the dispatcher names a type of System.Net.Http:
    /// in the browser that assembly is a lazy part, and Mono's AOT code loads the types of every field of a class it touches, which would
    /// make every start fetch it (docs/plan-browser-engine.md, Lazy parts). Only a remote call reaches <see cref="Http"/>.
    /// </summary>
    public HttpClient? RemoteHttp { get => (HttpClient?)_remoteHttp; init => _remoteHttp = value; }

    readonly object? _remoteHttp;

    /// <summary>The clock of the history (tests fix it).</summary>
    public Func<DateTime> RemoteNow { get; init; } = () => DateTime.UtcNow;

    /// <summary>The one client of the server remotes when the host gives none, made at the first remote call (a class's statics are set up when it is first used).</summary>
    static class SharedHttp
    {
        internal static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(10) };
    }

    HttpClient Http => (HttpClient?)_remoteHttp ?? SharedHttp.Client;

    /// <summary>
    /// The way in to <see cref="RemoteCallAsync"/> from <see cref="HandleAsync(string, Caller)"/> and <see cref="Dispatch"/>, which every
    /// call runs: never inlined, so that their code does not set up <see cref="RemoteCallAsync"/>'s state machine (whose fields may hold
    /// what an await spills, of System.Net.Http's types) until a history or remote call comes. The browser's engine fetches that
    /// assembly then (docs/plan-browser-engine.md, Lazy parts).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    Task<(object? Result, Diff? Diff)> RemoteCall(string method, JsonObject p) => RemoteCallAsync(method, p);

    /// <summary>A server's projects (<c>remote.projects</c>): in a method of its own, so that no client is spilled into <see cref="RemoteCallAsync"/>'s state machine.</summary>
    async Task<IReadOnlyList<RemoteProject>> ServerProjects(string url, RemoteTokens tokens)
    {
        var http = Http;
        var remote = new HttpRemote(http, url, await HttpRemote.DiscoverAsync(http, url), "-", tokens);
        return await remote.ProjectsAsync();
    }

    /// <summary>A new project on a server (<c>remote.add</c> with <c>create</c>), apart for the same reason as <see cref="ServerProjects"/>.</summary>
    async Task<RemoteProject> CreateServerProject(string root, RemoteTokens tokens, string title)
    {
        var http = Http;
        return await HttpRemote.CreateProjectAsync(http, root, await HttpRemote.DiscoverAsync(http, root), tokens, title);
    }

    static bool IsRemoteCall(string method) => method.StartsWith("remote.", StringComparison.Ordinal) || method.StartsWith("history.", StringComparison.Ordinal);

    static RemoteAuthor AuthorOf(JsonObject p)
    {
        if (RemoteAuthor.From(p["author"]) is { } a && RemoteAuthor.ValidId(a.Id)) return a with { Name = string.IsNullOrWhiteSpace(a.Name) ? "Lapin" : a.Name.Trim() };
        // A caller that names nobody: this computer, by a stable id made of its names (the interface sends its installation's own).
        var id = Lapin.Model.Hashes.Sha256Hex($"{Environment.MachineName}/{Environment.UserName}")[..16];
        return new RemoteAuthor($"local:{id}", Environment.UserName is { Length: > 0 } u ? u : "Lapin");
    }

    static RemoteTokens TokensOf(JsonObject p) => new() { Access = (string?)p["token"], Refresh = (string?)p["refreshToken"] };

    static bool Flag(JsonObject p, string key) => p[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>A remote of the project opened: a folder (as its folder path), or a server (its lapin-remote base read from Foundation).</summary>
    async Task<IRemote> OpenRemote(RemoteEntry e, RemoteAuthor me, RemoteTokens tokens) => e.Kind switch
    {
        "folder" => FolderRemote.Open(FolderRemote.PathOf(e.Url), e.Project, me, RemoteNow),
        "server" => await HttpRemote.ConnectAsync(Http, e.Url, e.Project, tokens),
        _ => throw new CommandException($"A remote of kind „{e.Kind}” is not known to this Lapin."),
    };

    RemoteEntry TheRemote(PackageHistory h, JsonObject p)
    {
        var name = (string?)p["remote"] ?? "origin";
        return h.Remotes.FirstOrDefault(r => r.Name == name) ?? throw new CommandException(h.Remotes.Count == 0 ? "The project has no remote: add one in Manage → Remote." : $"The project has no remote „{name}”.");
    }

    static void Tokens(JsonObject result, RemoteTokens tokens)
    {
        if (tokens.Changed) result["tokens"] = new JsonObject { ["accessToken"] = tokens.Access, ["refreshToken"] = tokens.Refresh };
    }

    /// <summary>
    /// The history and remote calls (asynchronous: a server is reached with <see cref="HttpClient.SendAsync(HttpRequestMessage)"/>, which
    /// the browser's engine needs). The host awaits them as one call of its queue (<see cref="HandleAsync(string, Caller)"/>): the session does
    /// not change under a push or a pull.
    /// </summary>
    async Task<(object? Result, Diff? Diff)> RemoteCallAsync(string method, JsonObject p)
    {
        session.RefuseDuringStep(ownerToo: true);
        var me = AuthorOf(p);
        var tokens = TokensOf(p);
        var asBytes = Flag(p, "bytes");
        var now = RemoteNow();
        switch (method)
        {
            case "history.status": return (Status(files.History(session)), null);
            case "history.log": return (Log(files.History(session), p["limit"] is JsonValue lv && lv.TryGetValue<int>(out var limit) ? limit : 200), null);
            case "history.commit":
            {
                var message = (string?)p["message"] ?? "";
                var h = files.History(session).Clone();
                if (h.Unreadable is { } why) throw new CommandException($"The project's history could not be read ({why}): it is kept as it is and cannot take a commit.");
                var build = ProjectPackage.Build(session);
                CommitObject commit;
                try { commit = h.CommitLive(build.Manifest, me, message, session.Model.Entities.Count(), now, IssuesApart(h)); }
                catch (InvalidDataException e) { throw new CommandException(e.Message); }
                var (r, d) = files.WriteHistory(session, h, build, asBytes, new JsonObject { ["commit"] = CommitJson(commit, h) });
                r["status"] = Status(files.History(session));
                return (r, d);
            }
            case "remote.signIn":
            {
                var url = (string?)p["url"] ?? throw new CommandException("Which server?");
                var t = await HttpRemote.SignInAsync(Http, url, (string?)p["username"] ?? throw new CommandException("Whose account?"), (string?)p["password"] ?? throw new CommandException("The password is missing."), (string?)p["clientId"] ?? HttpRemote.ClientId);
                var o = new JsonObject { ["accessToken"] = t.Access, ["refreshToken"] = t.Refresh };
                if (await CurrentUser(url, t) is { } user) o["user"] = user;
                return (o, null);
            }
            case "remote.changePassword":
            {
                // Atrium Server's own call (not lapin-remote): the server ends every sign-in and hands back a fresh pair. A refusal is
                // an answer, not an error, so that tokens a refresh replaced on the way still reach the interface.
                var url = (string?)p["url"] ?? throw new CommandException("Which server?");
                var current = (string?)p["currentPassword"] ?? throw new CommandException("The current password is missing.");
                var next = (string?)p["newPassword"] ?? throw new CommandException("The new password is missing.");
                if (next.Length < 8) throw new CommandException("The new password needs at least 8 characters.");
                var o = new JsonObject();
                try { await HttpRemote.ChangePasswordAsync(Http, url, tokens, current, next); o["changed"] = true; }
                catch (RemoteException e) { o["changed"] = false; o["code"] = e.Code; o["message"] = e.Message; }
                Tokens(o, tokens);
                return (o, null);
            }
            case "remote.projects":
            {
                var kind = (string?)p["kind"] ?? "server";
                var url = (string?)p["url"] ?? throw new CommandException("Which remote?");
                JsonArray list;
                if (kind == "folder")
                {
                    var folder = FolderRemote.PathOf(url);
                    list = File.Exists(Path.Combine(folder, FolderRemote.MarkerFile))
                        ? new JsonArray([.. FolderRemote.Projects(folder).Select(x => (JsonNode)new JsonObject { ["id"] = x.Id, ["name"] = x.Name, ["role"] = "owner" })])
                        : [];
                }
                else
                {
                    list = new JsonArray([.. (await ServerProjects(url, tokens)).Select(x => (JsonNode)new JsonObject { ["id"] = x.Id, ["name"] = x.Name, ["role"] = x.Role })]);
                }
                var o = new JsonObject { ["projects"] = list };
                Tokens(o, tokens);
                return (o, null);
            }
            case "remote.add":
            {
                var h = files.History(session).Clone();
                if (h.Remotes.Count > 0) throw new CommandException("The project has a remote already: remove it first (one remote per project for now).");
                var kind = (string?)p["kind"] ?? throw new CommandException("A folder or a server?");
                var url = (string?)p["url"] ?? throw new CommandException("Which remote?");
                var project = (string?)p["project"];
                var title = (string?)p["projectName"] ?? (session.Path is { } sp ? Path.GetFileNameWithoutExtension(sp) : "Project");
                string normal;
                if (kind == "folder")
                {
                    var folder = FolderRemote.PathOf(url);
                    if (!Path.IsPathRooted(folder)) throw new CommandException("The folder's path is not a full one.");
                    project ??= FolderRemote.Create(folder, title, now);
                    using (FolderRemote.Open(folder, project, me, RemoteNow)) { }
                    normal = FolderRemote.UrlOf(folder);
                }
                else if (kind == "server")
                {
                    var root = HttpRemote.CheckRoot(url);
                    normal = root.ToString().TrimEnd('/');
                    if (project == null)
                    {
                        if (!Flag(p, "create")) throw new CommandException("Which project on the server?");
                        project = (await CreateServerProject(normal, tokens, title)).Id;
                    }
                }
                else throw new CommandException("A remote is a folder or a server.");
                h.Remotes.Add(new RemoteEntry((string?)p["name"] ?? "origin", kind, normal, project));
                var (o, d) = KeepRemotes(h, asBytes);
                Tokens(o, tokens);
                return (o, d);
            }
            case "remote.remove":
            {
                var h = files.History(session).Clone();
                var e = TheRemote(h, p);
                h.Remotes.Remove(e);
                h.Seen.Remove(e.Name);
                return KeepRemotes(h, asBytes);
            }
            case "remote.fetch":
            {
                var h = files.History(session).Clone();
                var e = TheRemote(h, p);
                using var remote = await OpenRemote(e, me, tokens);
                var f = await RemoteOps.FetchAsync(remote, h, e.Name, me, now);
                files.KeepHistory(session, h);
                var o = new JsonObject
                {
                    ["unchanged"] = f.Unchanged, ["commits"] = f.Commits, ["trees"] = f.Trees, ["blobs"] = f.Blobs, ["tags"] = f.Tags,
                    ["notes"] = new JsonArray([.. f.Notes.Select(n => (JsonNode)n)]), ["status"] = Status(h),
                };
                Tokens(o, tokens);
                return (o, null);
            }
            case "remote.syncIssues": return await SyncIssues(p, me, tokens, now, asBytes);
            case "remote.issueInfo":
            {
                var h = files.History(session);
                return (IssueInfo(h, IssueRemote(h, p)), null);
            }
            case "remote.atrium": return await Atrium(p, tokens);
            case "remote.handoverReport": return await HandoverReport(p, tokens, now);
            case "remote.submitIssue": return await SubmitIssue(p, me, tokens);
            case "remote.issueTag": return await IssueTag(p, me, tokens, now);
            case "remote.push": return await Push(p, me, tokens, now);
            case "remote.pull": return await Pull(p, me, tokens, now, asBytes);
            case "remote.clone": return await Clone(p, me, tokens, now);
            default: throw new CommandException($"Unknown method „{method}”.");
        }
    }

    /// <summary>The remotes changed: written into the project's file at once when it has one (they are the project's), else kept for its first save.</summary>
    (JsonObject Result, Diff? Diff) KeepRemotes(PackageHistory h, bool asBytes)
    {
        if (session.Path == null && !asBytes)
        {
            files.KeepHistory(session, h);
            return (Status(h), null);
        }
        var (r, d) = files.WriteHistory(session, h, ProjectPackage.Build(session), asBytes, []);
        var o = Status(files.History(session));
        foreach (var (k, v) in r) o[k] = v?.DeepClone();
        return (o, d);
    }

    /// <summary>Foundation's current user (OpenCDE 1.1), for the <c>atrium:</c> author id; null when the server does not say.</summary>
    async Task<JsonObject?> CurrentUser(string url, RemoteTokens t)
    {
        try
        {
            var o = await HttpRemote.CurrentUserAsync(Http, url, t.Access);
            if (o == null) return null;
            // Only a public id (a UUID) becomes the author's id; Foundation's id may be the e-mail address, which never goes into a commit (§4.3).
            var user = new JsonObject { ["name"] = (string?)o["name"] ?? (string?)o["display_name"] };
            if ((string?)o["id"] is { } id && Guid.TryParse(id, out var g)) user["id"] = $"atrium:{g:D}";
            return user;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or System.Text.Json.JsonException or InvalidDataException or NotSupportedException) { return null; }
    }

    JsonObject Status(PackageHistory h)
    {
        var build = ProjectPackage.Build(session);
        var o = new JsonObject
        {
            ["branch"] = h.Head, ["head"] = h.HeadCommit, ["uncommitted"] = h.HeadCommit == null ? build.Manifest.Parts.Count > 0 : h.Uncommitted(build.Manifest, IssuesApart(h)),
            ["saved"] = session.Path != null,
            ["problems"] = new JsonArray([.. h.Problems.Select(x => (JsonNode)x)]),
            ["unreadable"] = h.Unreadable,
        };
        if (h.HeadCommit is { } head && h.HasCommit(head)) o["commit"] = CommitJson(h.Commit(head), h);
        var remotes = new JsonArray();
        foreach (var r in h.Remotes)
        {
            var (ahead, behind) = h.AheadBehind(r.Name);
            var seen = h.Seen.GetValueOrDefault(r.Name);
            remotes.Add((JsonNode)new JsonObject
            {
                ["name"] = r.Name, ["kind"] = r.Kind, ["url"] = r.Url, ["project"] = r.Project, ["ahead"] = ahead, ["behind"] = behind,
                ["fetchedAt"] = seen?.FetchedAt, ["remoteHead"] = seen?.Refs.GetValueOrDefault(h.Head),
            });
        }
        o["remotes"] = remotes;
        return o;
    }

    static JsonObject CommitJson(CommitObject c, PackageHistory h) => new()
    {
        ["id"] = c.Id, ["message"] = c.Message, ["at"] = c.At, ["author"] = c.Author.ToJson(), ["entities"] = c.Entities,
        ["parents"] = new JsonArray([.. c.Parents.Select(x => (JsonNode)x)]), ["app"] = c.App,
        ["tags"] = new JsonArray([.. h.Refs.Where(r => RefNames.IsTag(r.Key) && h.KindOfHeld(r.Value) == ObjectKind.Tag && h.Tag(r.Value).Commit == c.Id).Select(r => (JsonNode)r.Key)]),
    };

    static JsonArray Log(PackageHistory h, int limit)
    {
        if (h.HeadCommit is not { } head) return [];
        var remote = h.Seen.Values.SelectMany(s => s.Refs.Values).Where(h.HasCommit).ToList();
        var onRemote = h.Closure(remote).Where(kv => kv.Value.Kind == ObjectKind.Commit).Select(kv => kv.Key).ToHashSet(StringComparer.Ordinal);
        return new JsonArray([.. h.Walk(head, Math.Clamp(limit, 1, 5000)).Select(c =>
        {
            var o = CommitJson(c, h);
            o["pushed"] = onRemote.Contains(c.Id);
            return (JsonNode)o;
        })]);
    }

    /// <summary>
    /// The viewer copy of a commit (§12.2): the IFC4 export of the project as it is, the same as File → Export → IFC (its GlobalIds,
    /// what VisibilityGraphics shows), when the project is that commit; null otherwise (its absence breaks nothing).
    /// </summary>
    Func<string, (byte[] Ifc, string Schema)?> Viewer(PackageHistory h) => commit =>
    {
        if (commit != h.HeadCommit || h.Uncommitted(ProjectPackage.Build(session).Manifest, IssuesApart(h))) return null;
        ReportProgress("Exporting the viewer copy", 0, 0);
        var (bytes, _) = IfcExport.Bytes(session, "viewer.ifc", new IfcExportOptions("IFC4"));
        return (bytes, "IFC4");
    };

    async Task<(object? Result, Diff? Diff)> Push(JsonObject p, RemoteAuthor me, RemoteTokens tokens, DateTime now)
    {
        var h = files.History(session).Clone();
        var e = TheRemote(h, p);
        if (h.HeadCommit == null) throw new CommandException("There is nothing to push: commit first.");
        using var remote = await OpenRemote(e, me, tokens);
        ReportProgress("Pushing", 0, 0);
        var outcome = await RemoteOps.PushAsync(remote, h, e.Name, [(h.Head, h.Head)], now, Flag(p, "noViewer") ? null : Viewer(h));
        files.KeepHistory(session, h);
        var o = new JsonObject
        {
            ["pushed"] = outcome.Pushed, ["uploaded"] = outcome.Uploaded.Count, ["asked"] = outcome.Asked.Count,
            ["notes"] = new JsonArray([.. outcome.Notes.Select(n => (JsonNode)n)]),
        };
        if (!outcome.Pushed)
        {
            o["moved"] = outcome.Moved;
            o["current"] = outcome.Current;
            o["message"] = outcome.Behind
                ? "The remote has commits this project does not have: pull first."
                : $"{outcome.Moved?["refs/heads/".Length..]} moved on the remote{(outcome.MovedBy is { } by ? $" ({by.Name}{(outcome.MovedAt is { } at ? $", {at}" : "")})" : "")} while you pushed: pull first. Nothing was changed on the remote.";
        }
        o["status"] = Status(h);
        Tokens(o, tokens);
        return (o, null);
    }

    /// <summary>
    /// A pull, fast-forward only (§8.4): refused with uncommitted changes; the remote fetched; on a fast-forward the model brought to
    /// the remote's commit in one undo step and the branch moved (logged <c>pull</c>), the file saved. Histories that diverged are
    /// refused, unless <c>resolve</c> says what to do: <c>branch</c> (my commits pushed as <c>wip/&lt;me&gt;/&lt;date&gt;</c>, then the
    /// branch to the remote's) or <c>discard</c> (my commits kept in the file on a local wip branch, the branch to the remote's).
    /// </summary>
    async Task<(object? Result, Diff? Diff)> Pull(JsonObject p, RemoteAuthor me, RemoteTokens tokens, DateTime now, bool asBytes)
    {
        var h = files.History(session).Clone();
        var e = TheRemote(h, p);
        if (h.HeadCommit != null && h.Uncommitted(ProjectPackage.Build(session).Manifest, IssuesApart(h)))
            throw new CommandException("The project has changes that are not committed: commit first, then pull.");
        using var remote = await OpenRemote(e, me, tokens);
        var fetched = await RemoteOps.FetchAsync(remote, h, e.Name, me, now);
        var (kind, local, theirs) = RemoteOps.Stand(h, e.Name);
        var o = new JsonObject { ["notes"] = new JsonArray([.. fetched.Notes.Select(n => (JsonNode)n)]) };
        Tokens(o, tokens);
        switch (kind)
        {
            case PullKind.UpToDate or PullKind.Ahead:
                files.KeepHistory(session, h);
                o["pulled"] = false;
                o["kind"] = kind == PullKind.Ahead ? "ahead" : "upToDate";
                o["message"] = kind == PullKind.Ahead ? "The project is ahead of the remote: nothing to pull (push)." : "The project is up to date.";
                o["status"] = Status(h);
                return (o, null);
            case PullKind.Diverged:
            {
                var resolve = (string?)p["resolve"];
                if (resolve is not ("branch" or "discard"))
                {
                    files.KeepHistory(session, h);
                    o["pulled"] = false;
                    o["kind"] = "diverged";
                    o["message"] = "Your commits and the remote's went different ways: keep your work as a branch on the remote, or discard your commits (they stay in the file).";
                    o["status"] = Status(h);
                    return (o, null);
                }
                var wip = RefNames.Wip(me.Name, now, n => h.Refs.ContainsKey(n) || h.Seen.GetValueOrDefault(e.Name)?.Refs.ContainsKey(n) == true);
                if (resolve == "branch")
                {
                    h.Refs[wip] = local!;
                    var pushed = await RemoteOps.PushAsync(remote, h, e.Name, [(wip, wip)], now, fetchRefs: false);
                    if (!pushed.Pushed) throw new CommandException($"Your work could not be kept as a branch on the remote: {pushed.Moved} moved.");
                    h.Refs.Remove(wip);
                    o["branch"] = wip["refs/heads/".Length..];
                }
                else
                {
                    h.Move(wip, local, me, "discard", now);
                    o["branch"] = wip["refs/heads/".Length..];
                }
                break;
            }
        }
        // Fast-forward (or after the divergence was resolved): the model to the remote's commit, one undo step.
        var target = h.Commit(theirs!);
        if (!target.Known) throw new CommandException("The remote's commit was made by a newer Lapin: update Lapin to pull it.");
        PackageContent content;
        try { content = h.Content(target.Tree); }
        catch (InvalidDataException ex) { throw new CommandException(ex.Message); }
        var (entities, relations) = session.Upgraded(content.Entities, content.Relations);
        // A server's issues are synced on their own (remote.syncIssues): the remarks here stay as they are, whatever the commit held.
        if (IssuesApart(h) != null) entities = [.. entities.Where(x => x.Class != Remarks.Class), .. session.Model.OfClass(Remarks.Class)];
        var diff = session.Edit("Pull", edit => { ModelCompare.Apply(edit, entities, relations); session.AdoptClasses(content.Classes); });
        h.Move(h.Head, theirs, me, "pull", now);
        var (r, saved) = files.WriteHistory(session, h, ProjectPackage.Build(session), asBytes, o);
        r["pulled"] = true;
        r["kind"] = kind == PullKind.Diverged ? "resolved" : "fastForward";
        r["commit"] = CommitJson(target, h);
        r["status"] = Status(files.History(session));
        // The edit's diff carries the model's change; the save's (the title, saved) follows it.
        return (r, saved == null ? diff : Merge(diff, saved));
    }

    static Diff Merge(Diff edit, Diff saved) => edit with { Version = saved.Version, State = saved.State };

    /// <summary>A clone (§8.3): the remote's project opened as a new one, its history the remote's, written to <c>path</c> (the desktop) or kept for its first save.</summary>
    async Task<(object? Result, Diff? Diff)> Clone(JsonObject p, RemoteAuthor me, RemoteTokens tokens, DateTime now)
    {
        var kind = (string?)p["kind"] ?? throw new CommandException("A folder or a server?");
        var url = (string?)p["url"] ?? throw new CommandException("Which remote?");
        var project = (string?)p["project"] ?? throw new CommandException("Which project?");
        var entry = new RemoteEntry("origin", kind, kind == "folder" ? FolderRemote.UrlOf(FolderRemote.PathOf(url)) : HttpRemote.CheckRoot(url).ToString().TrimEnd('/'), project);
        var path = (string?)p["path"] is { Length: > 0 } pp ? HostFiles.Check(pp.EndsWith(LapinPackage.Suffix, StringComparison.OrdinalIgnoreCase) ? pp : pp + LapinPackage.Suffix) : null;
        if (path != null && File.Exists(path)) throw new CommandException($"{Path.GetFileName(path)} exists already: a clone goes into a new file.");
        var h = PackageHistory.Empty();
        string? tree;
        using (var remote = await OpenRemote(entry, me, tokens)) tree = await RemoteOps.CloneAsync(remote, h, entry, me, now);
        if (tree == null) throw new CommandException("The remote's project has no commit yet: nothing to clone.");
        // The commit as a package of exactly its parts (§12.1), opened as any package is (§8.3), then given the history and saved.
        byte[] package;
        try { package = h.Package(tree); }
        catch (InvalidDataException ex) { throw new CommandException(ex.Message); }
        files.Reset();
        Diff diff;
        if (path != null)
        {
            AtomicFile.Write(path, package);
            diff = files.Open(session, path);
            files.KeepHistory(session, h);
            if (files.Save(session, null) is { } s) diff = Merge(diff, s);
        }
        else
        {
            diff = files.OpenBytes(session, $"{entry.Project}.lapin", package);
            files.KeepHistory(session, h);
        }
        var o = new JsonObject { ["path"] = path, ["status"] = Status(files.History(session)) };
        Tokens(o, tokens);
        return (o, diff);
    }

    // ------------------------------------------------------------ issues (Atrium docs/plan-cde-web.md W3)

    /// <summary>
    /// When the project's remote is a server, its remarks are the server's issues, synced on their own (<see cref="IssueSync"/>): their
    /// parts are left out of "uncommitted" and of a commit's "nothing changed", and a pull keeps the remarks here. A folder remote has no
    /// issues of its own: remarks travel with the commits there, as everything else.
    /// </summary>
    static Func<string, bool>? IssuesApart(PackageHistory h) => h.Remotes.Any(r => r.Kind == "server") ? IsIssuePart : null;

    static bool IsIssuePart(string path) => path.StartsWith("model/", StringComparison.Ordinal) && path.Contains($"/{Remarks.Class}/", StringComparison.Ordinal);

    static RemoteEntry? IssueRemote(PackageHistory h, JsonObject p)
    {
        var name = (string?)p["remote"];
        return h.Remotes.FirstOrDefault(r => r.Kind == "server" && (name == null || r.Name == name));
    }

    /// <summary>
    /// What the interface needs to show the issues of a server project without asking the server: whether the project has one, and from
    /// the last sync the account, its role, the project's rules, its members' names and its BCF choices, the topics the server holds,
    /// and how many remarks wait to be sent.
    /// </summary>
    JsonObject IssueInfo(PackageHistory h, RemoteEntry? e)
    {
        var o = new JsonObject { ["linked"] = e != null };
        if (e == null) return o;
        o["server"] = e.Url;
        o["project"] = e.Project;
        if (h.Issues.GetValueOrDefault(e.Name) is not { } st || (string?)st["project"] != e.Project) return o;
        foreach (var key in new[] { "account", "name", "role", "isAdmin", "rules", "people", "extensions", "syncedAt", "last" })
            o[key] = st[key]?.DeepClone();
        var known = st["topics"] as JsonObject ?? [];
        o["known"] = new JsonArray([.. known.Select(kv => (JsonNode)kv.Key)]);
        var local = Remarks.All(session.Model).ToDictionary(t => t.Guid.ToString("D"));
        var pending = 0;
        foreach (var (g, t) in local)
        {
            if (known[g] is not JsonObject k || k["base"] is not JsonObject b) { pending++; continue; }
            var fields = IssueSync.FieldsOf(t);
            var comments = (k["comments"] as JsonArray ?? []).Select(x => (string?)x).ToHashSet();
            var viewpoints = (k["viewpoints"] as JsonArray ?? []).Select(x => (string?)x).ToHashSet();
            if (IssueSync.Fields.Any(f => !IssueSync.SameValue(fields[f], b[f]))
                || t.Comments.Any(c => !comments.Contains(c.Guid.ToString("D"))) || t.Viewpoints.Any(v => !viewpoints.Contains(v.Guid.ToString("D"))))
                pending++;
        }
        pending += known.Count(kv => !local.ContainsKey(kv.Key));
        o["pending"] = pending;
        return o;
    }

    /// <summary>
    /// The remarks synced with the server project's issues (<see cref="IssueSync"/>): what came from the server applied in one undo step
    /// ("Sync issues"), the sync's state kept with the history (in remotes.json, never in a commit); in the browser the file is written in
    /// memory with it, on the desktop the next save writes it.
    /// </summary>
    async Task<(object? Result, Diff? Diff)> SyncIssues(JsonObject p, RemoteAuthor me, RemoteTokens tokens, DateTime now, bool asBytes)
    {
        var h = files.History(session).Clone();
        var e = IssueRemote(h, p) ?? throw new CommandException("Issues are synced with a server: connect one in File → Collaborate….");
        IssueSync.Outcome outcome;
        using (var remote = (HttpRemote)await OpenRemote(e, me, tokens))
        {
            ReportProgress("Syncing issues", 0, 0);
            outcome = await new IssueSync(remote, e.Project).RunAsync(Remarks.All(session.Model), h.Issues.GetValueOrDefault(e.Name), e.Url, now);
        }
        h.Issues[e.Name] = outcome.State;
        Diff? diff = null;
        if (outcome.Topics.Count > 0)
            diff = session.Edit("Sync issues", edit =>
            {
                foreach (var (g, t) in outcome.Topics)
                {
                    var id = Remarks.IdOf(g);
                    var existing = edit.Model.Get(id);
                    if (t == null) { if (existing is { Class: Remarks.Class }) edit.Remove(id); }
                    else if (existing is { Class: Remarks.Class }) edit.Replace(Remarks.ToEntity(t));
                    else if (existing == null) edit.Add(Remarks.ToEntity(t));
                }
            });
        var o = new JsonObject { ["report"] = outcome.Report };
        Tokens(o, tokens);
        if (asBytes)
        {
            var (r, saved) = files.WriteHistory(session, h, ProjectPackage.Build(session), true, o);
            r["info"] = IssueInfo(files.History(session), e);
            r["status"] = Status(files.History(session));
            return (r, diff == null ? saved : saved == null ? diff : Merge(diff, saved));
        }
        files.KeepHistory(session, h);
        o["info"] = IssueInfo(h, e);
        o["status"] = Status(h);
        return (o, diff);
    }

    static readonly string[] AtriumMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    static readonly System.Text.RegularExpressions.Regex ReadOnlyObject = new(
        "^lapin-remote/1\\.0/projects/[A-Za-z0-9._-]{1,128}/(commits/[0-9a-f]{64}/(package|viewer)|objects/[0-9a-f]{64})$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>The register's path with its file's name (<c>?name=</c>), the one request whose body is a file (W5).</summary>
    static readonly System.Text.RegularExpressions.Regex RegisterPath = new(
        "^atrium/1\\.0/projects/[A-Za-z0-9._-]{1,128}/register(\\?name=[^&#]*)?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>A project's IDS file by its (encoded) name, no query (W6c): put as the file itself, and read back as bytes for a download.</summary>
    static readonly System.Text.RegularExpressions.Regex IdsPath = new(
        "^atrium/1\\.0/projects/[A-Za-z0-9._-]{1,128}/ids/[A-Za-z0-9._~%!$&'()*+,;=@-]{1,800}\\.[iI][dD][sS]$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// One request to the server's own API (Atrium's <c>/atrium/1.0</c>: the account, its administration, members, rules, activity;
    /// BCF 3.0; and the making of a project, <c>POST /lapin-remote/1.0/projects</c>) on the sign-in the call carries, for the desktop's
    /// interface, which cannot reach the server itself. Its status, JSON and ETag are the answer, whatever the status.
    /// </summary>
    async Task<(object? Result, Diff? Diff)> Atrium(JsonObject p, RemoteTokens tokens)
    {
        var url = (string?)p["url"] ?? IssueRemote(files.History(session), p)?.Url ?? throw new CommandException("Which server?");
        var method = ((string?)p["method"] ?? "GET").ToUpperInvariant();
        var path = ((string?)p["path"] ?? "").TrimStart('/');
        var bare = path.Split('?')[0];
        // W4: a commit's package, its viewer record and an object (its IFC copy, an issued file) read for the desktop's containers view.
        var download = method == "GET" && (ReadOnlyObject.IsMatch(bare) || IdsPath.IsMatch(path));
        var allowed = bare.StartsWith("atrium/1.0/", StringComparison.Ordinal) || bare.StartsWith("bcf/3.0/", StringComparison.Ordinal)
            || (bare == "lapin-remote/1.0/projects" && method == "POST") || download;
        if (!allowed || !AtriumMethods.Contains(method) || path.Contains("..", StringComparison.Ordinal) || path.Contains('\\') || path.Contains("//", StringComparison.Ordinal) || path.Contains('#'))
            throw new CommandException("Not a request of the server's own API.");
        // W5: a project's document register goes up as the file itself (CSV or XLSX, base64 in `data`); W6c: an IDS file, the same way.
        // No other request carries bytes.
        byte[]? raw = null;
        if ((string?)p["data"] is { } data)
        {
            if (method != "PUT" || !(RegisterPath.IsMatch(path) || IdsPath.IsMatch(path))) throw new CommandException("Only a document register or an IDS file is sent as a file.");
            try { raw = Convert.FromBase64String(data); }
            catch (FormatException) { throw new CommandException("The file's bytes are not base64."); }
        }
        var remote = (HttpRemote)await HttpRemote.ConnectAsync(Http, url, "-", tokens);
        var bytes = download && Flag(p, "bytes");
        var a = await remote.ApiAsync(new HttpMethod(method), path, raw == null ? p["body"]?.DeepClone() : null, (string?)p["ifMatch"], bytes, raw, (string?)p["contentType"]);
        var o = new JsonObject { ["status"] = a.Status, ["body"] = a.Json?.DeepClone(), ["etag"] = a.ETag };
        if (a.Bytes is { } b)
        {
            if ((string?)p["saveTo"] is { Length: > 0 } to) { Lapin.Platform.AtomicFile.Write(RefuseOverRead(HostFiles.Check(to)), b); o["saved"] = to; o["bytes"] = b.Length; }
            else o["data"] = Convert.ToBase64String(b);
        }
        Tokens(o, tokens);
        return (o, null);
    }
}
