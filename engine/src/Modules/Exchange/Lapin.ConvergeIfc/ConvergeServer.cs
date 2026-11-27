using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.ConvergeIfc;

/// <summary>The account a session talks to the server with: its address and who the token is; never the token itself.</summary>
public sealed record ConvergeAccount(string Server, string? Name, string? Email);

/// <summary>A project of the account, with its roles there: an author, reviewer or approver may upload.</summary>
public sealed record ConvergeProject(string Id, string Code, string Name, IReadOnlyList<string> Roles)
{
    public bool CanUpload => Roles.Any(r => r is "author" or "reviewer" or "approver");
}

/// <summary>An IFC file of a project (a container) as the pickers list it: its current version and where it stands in the workflow.</summary>
public sealed record ConvergeFile(string Id, string FileName, int VersionNo, string RevisionCode, string Status, string Sha256, long SizeBytes, DateTime? UpdatedAt);

/// <summary>A version of a file, the newest first.</summary>
public sealed record ConvergeVersion(int VersionNo, string RevisionCode, string Sha256, long SizeBytes, DateTime? UploadedAt, string? UploadedBy, bool IsCurrent);

/// <summary>What a send made: the file and version now current; <see cref="Created"/> a new file, <see cref="Unchanged"/> nothing sent (the server already holds these bytes as the current version).</summary>
public sealed record ConvergeSent(string Id, string FileName, int VersionNo, string RevisionCode, string Sha256, bool Created, bool Unchanged);

/// <summary>A version fetched to a file of this computer, its hash checked against the server's.</summary>
public sealed record ConvergeFetched(string Path, string FileName, string Sha256, int VersionNo, string RevisionCode);

/// <summary>
/// A Converge IFC server (ifc-hub) over its REST API (/api/v1): the account, its projects and IFC files, an upload as a new
/// file or a new version, a download. The token is held in memory only, for as long as the engine runs: the interface keeps
/// it in the system's secure store and gives it again when it starts; it never goes into the project, a setting, a report or a log.
/// </summary>
public sealed class ConvergeServer
{
    /// <summary>The folder (under the system's temporary one) holding the files made for a send and those fetched, each in a folder of its own.</summary>
    public static string TempRoot => Path.Combine(Path.GetTempPath(), "lapin-converge");

    readonly object _lock = new();
    readonly HttpClient _http;
    ConvergeAccount? _account;
    string? _token;

    public ConvergeServer(HttpMessageHandler? handler = null)
    {
        // No client-wide timeout: a model is hundreds of megabytes; the caller's cancellation stops a call.
        _http = handler == null ? new HttpClient { Timeout = Timeout.InfiniteTimeSpan } : new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public ConvergeAccount? SignedIn { get { lock (_lock) return _account; } }

    /// <summary>
    /// A server's address as the connector takes it: https (http only on this computer, so a token never crosses a network in the
    /// clear), no path, no trailing slash. There is no default: the server is the firm's own.
    /// </summary>
    public static string ServerOf(string? text)
    {
        var s = (text ?? "").Trim();
        if (s.Length == 0) throw new CommandException("Which server? Give its address, e.g. https://cde.example.com.");
        if (!s.Contains("://", StringComparison.Ordinal)) s = "https://" + s;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https") || string.IsNullOrEmpty(u.Host))
            throw new CommandException($"„{text}” is not a server's address (https://cde.example.com).");
        if (u.Scheme == "http" && !u.IsLoopback)
            throw new CommandException("Use https: the token would travel in the clear over http (http is accepted for this computer only).");
        return u.GetLeftPart(UriPartial.Authority);
    }

    /// <summary>Signs in: the token checked against the server (who it is), then held for the calls that follow.</summary>
    public ConvergeAccount SignIn(string? server, string? token, CancellationToken ct)
    {
        var address = ServerOf(server);
        var secret = (token ?? "").Trim();
        if (secret.Length == 0) throw new CommandException("The token is missing: make one on the server, in the account menu under API tokens.");
        var me = One(Json(Run(() => Request(HttpMethod.Get, address, secret, "/api/v1/me", null, ct), "sign in", address), "sign in"), "sign in");
        var account = new ConvergeAccount(address, Str(me, "name"), Str(me, "email"));
        lock (_lock) { _account = account; _token = secret; }
        return account;
    }

    public void SignOut() { lock (_lock) { _account = null; _token = null; } }

    (string Server, string Token) Current()
    {
        lock (_lock) return _account is { } a && _token != null ? (a.Server, _token) : throw new CommandException("Sign in to the server first (File → Collaborate → Converge IFC).");
    }

    /// <summary>The projects the account belongs to, with its roles in each.</summary>
    public List<ConvergeProject> Projects(CancellationToken ct)
    {
        var (server, token) = Current();
        var me = One(Json(Run(() => Request(HttpMethod.Get, server, token, "/api/v1/me", null, ct), "list the projects", server), "list the projects"), "list the projects");
        return (me["projects"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(p => new ConvergeProject(Str(p, "project_id") ?? "", Str(p, "code") ?? "", Str(p, "name") ?? "",
                (p["roles"] as JsonArray ?? []).Select(r => (string?)r ?? "").Where(r => r.Length > 0).ToList()))
            .Where(p => p.Id.Length > 0).OrderBy(p => p.Code, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>The IFC files of a project, by name.</summary>
    public List<ConvergeFile> Files(string projectId, CancellationToken ct)
    {
        var (server, token) = Current();
        var rows = Many(Json(Run(() => Request(HttpMethod.Get, server, token, $"/api/v1/projects/{Uri.EscapeDataString(projectId)}/containers", null, ct), "list the files", server), "list the files"), "list the files");
        return rows.OfType<JsonObject>().Where(c => Str(c, "doc_type") == "ifc").Select(FileOf)
            .OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>A file as it stands now.</summary>
    public ConvergeFile GetFile(string containerId, CancellationToken ct)
    {
        var (server, token) = Current();
        return FileOf(One(Json(Run(() => Request(HttpMethod.Get, server, token, $"/api/v1/containers/{Uri.EscapeDataString(containerId)}", null, ct), "read the file", server), "read the file"), "read the file"));
    }

    /// <summary>A file's versions, the newest first.</summary>
    public List<ConvergeVersion> Versions(string containerId, CancellationToken ct)
    {
        var (server, token) = Current();
        var rows = Many(Json(Run(() => Request(HttpMethod.Get, server, token, $"/api/v1/containers/{Uri.EscapeDataString(containerId)}/versions", null, ct), "list the versions", server), "list the versions"), "list the versions");
        return rows.OfType<JsonObject>().Select(v => new ConvergeVersion(
            (int?)v["version_no"] ?? 0, Str(v, "revision_code") ?? "", Str(v, "sha256") ?? "", (long?)v["size_bytes"] ?? 0,
            Date(Str(v, "uploaded_at")), Str(v, "uploaded_by"), v["is_current"] is JsonValue c && c.TryGetValue<bool>(out var b) && b)).ToList();
    }

    /// <summary>
    /// The file at <paramref name="path"/> sent to <paramref name="projectId"/>: as a new version of <paramref name="containerId"/>, or as a
    /// new file named <paramref name="fileName"/>. When the file's bytes are already the current version's, nothing is sent.
    /// </summary>
    public ConvergeSent Send(string projectId, string path, string? fileName, string? containerId, Action<string, int, int>? progress, CancellationToken ct)
    {
        var (server, token) = Current();
        if (!System.IO.File.Exists(path)) throw new CommandException("There is no file to send.");
        var sha = HashOf(path);
        var revising = containerId is { Length: > 0 };
        ConvergeFile? current = revising ? GetFile(containerId!, ct) : null;
        if (current != null && string.Equals(current.Sha256, sha, StringComparison.OrdinalIgnoreCase))
            return new ConvergeSent(current.Id, current.FileName, current.VersionNo, current.RevisionCode, sha, false, true);
        var name = current?.FileName ?? fileName?.Trim();
        if (string.IsNullOrEmpty(name)) throw new CommandException("Name the file, or choose the one to add a version to.");
        var route = revising
            ? $"/api/v1/containers/{Uri.EscapeDataString(containerId!)}/versions"
            : $"/api/v1/projects/{Uri.EscapeDataString(projectId)}/containers";
        progress?.Invoke("Uploading to the server", 0, 0);
        var answer = One(Json(Run(() => Request(HttpMethod.Post, server, token, route, () => Multipart(path, name, progress), ct), "send the file", server), "send the file"), "send the file");
        var f = FileOf(answer);
        return new ConvergeSent(f.Id, f.FileName, f.VersionNo, f.RevisionCode, f.Sha256, !revising, false);
    }

    /// <summary>
    /// A version (the current one when <paramref name="versionNo"/> is null) downloaded into a folder of its own under <see cref="TempRoot"/>,
    /// its SHA-256 checked against the server's before the file is kept; an .ifczip is unpacked to its IFC (the import reads
    /// a file by its path as .ifc). With <paramref name="to"/>, the checked file then takes that place, over what was there: a
    /// download that fails or arrives damaged never touches it.
    /// </summary>
    public ConvergeFetched Fetch(string containerId, int? versionNo, Action<string, int, int>? progress, CancellationToken ct, string? to = null)
    {
        var (server, token) = Current();
        var file = GetFile(containerId, ct);
        var version = Versions(containerId, ct).FirstOrDefault(v => versionNo == null ? v.IsCurrent : v.VersionNo == versionNo)
            ?? throw new CommandException(versionNo == null ? "The file has no version." : $"The file has no version {versionNo}.");
        var path = TempPath(file.FileName);
        progress?.Invoke("Downloading from the server", 0, 0);
        var route = $"/api/v1/containers/{Uri.EscapeDataString(containerId)}/versions/{version.VersionNo}/file";
        try
        {
            Run(() => { Download(server, token, route, path, version.SizeBytes, progress, ct); return true; }, "fetch the file", server);
            var sha = HashOf(path);
            if (!string.Equals(sha, version.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new CommandException("The file arrived damaged (its checksum differs from the server's): nothing was kept. Try again.");
            var got = path.EndsWith(".ifczip", StringComparison.OrdinalIgnoreCase) ? Run(() => Unzip(path), "unpack the file", server) : path;
            if (to is { Length: > 0 })
            {
                Run(() => { System.IO.File.Move(got, to, overwrite: true); return true; }, "save the file", server);
                Discard(path);
                got = to;
            }
            return new ConvergeFetched(got, file.FileName, sha, version.VersionNo, version.RevisionCode);
        }
        catch { Discard(path); throw; }
    }

    /// <summary>The largest IFC an .ifczip is unpacked to (a guard against an archive that unpacks without end).</summary>
    const long MaxUnpacked = 4L << 30;

    /// <summary>The IFC inside the .ifczip at <paramref name="path"/> (its largest .ifc entry), written beside it; the archive removed.</summary>
    static string Unzip(string path)
    {
        var ifc = Path.ChangeExtension(path, ".ifc");
        try
        {
            using (var zip = ZipFile.OpenRead(path))
            {
                var entry = zip.Entries.Where(e => e.Name.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase)).MaxBy(e => e.Length)
                    ?? throw new CommandException("The .ifczip holds no .ifc file.");
                if (entry.Length > MaxUnpacked) throw new CommandException("The file inside the .ifczip is too large.");
                // The entry's name is never used as a path: the archive is another's.
                using var input = entry.Open();
                using var output = System.IO.File.Create(ifc);
                var buffer = new byte[1 << 20];
                long done = 0;
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if ((done += n) > MaxUnpacked) throw new CommandException("The file inside the .ifczip is too large.");
                    output.Write(buffer, 0, n);
                }
            }
        }
        catch (InvalidDataException) { throw new CommandException("The .ifczip is damaged."); }
        System.IO.File.Delete(path);
        return ifc;
    }

    /// <summary>A file this connector made (inside a folder of its own under <see cref="TempRoot"/>) removed with that folder; any other path is left alone.</summary>
    public static bool Discard(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(TempRoot));
            var dir = Path.GetDirectoryName(full);
            if (dir == null || !string.Equals(Path.GetDirectoryName(dir), root, StringComparison.OrdinalIgnoreCase)) return false;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>A path for a file about to be made for a send or a fetch: a folder of its own under <see cref="TempRoot"/>.</summary>
    public static string TempPath(string? name)
    {
        var dir = Path.Combine(TempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, SafeName(name));
    }

    static string SafeName(string? name)
    {
        var s = Path.GetFileName((name ?? "").Replace('\\', '/'));
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Length == 0 ? "model.ifc" : s;
    }

    static string HashOf(string path)
    {
        using var s = System.IO.File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }

    // ------------------------------------------------------------------------------------------------------ HTTP

    static MultipartFormDataContent Multipart(string path, string name, Action<string, int, int>? progress)
    {
        var content = new MultipartFormDataContent();
        var file = new StreamContent(new ProgressStream(System.IO.File.OpenRead(path), new FileInfo(path).Length, progress));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "file", SafeName(name));
        return content;
    }

    HttpResponseMessage Request(HttpMethod method, string server, string token, string route, Func<HttpContent>? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, server + route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body != null) request.Content = body();
        var response = _http.Send(request, HttpCompletionOption.ResponseContentRead, ct);
        if (!response.IsSuccessStatusCode) { var refusal = Refusal(response); response.Dispose(); throw refusal; }
        return response;
    }

    void Download(string server, string token, string route, string path, long expected, Action<string, int, int>? progress, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, server + route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = _http.Send(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw Refusal(response);
        using var input = response.Content.ReadAsStream(ct);
        using var output = System.IO.File.Create(path);
        var buffer = new byte[1 << 20];
        long done = 0;
        int n;
        while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, n);
            done += n;
            progress?.Invoke("Downloading from the server", (int)Math.Min(done, int.MaxValue), (int)Math.Min(expected, int.MaxValue));
        }
    }

    /// <summary>A server's refusal as the user reads it: its own message when it gave one, else what the status means.</summary>
    static ServerRefused Refusal(HttpResponseMessage r)
    {
        string? message = null;
        try { message = Str(JsonNode.Parse(r.Content.ReadAsStringAsync().GetAwaiter().GetResult()) as JsonObject, "message"); }
        catch (JsonException) { /* not JSON: the status says it */ }
        return new ServerRefused(r.StatusCode, message);
    }

    sealed class ServerRefused(HttpStatusCode status, string? detail) : Exception("The server refused the call.")
    {
        public HttpStatusCode Status { get; } = status;
        public string? Detail { get; } = detail;
    }

    static JsonNode Json(HttpResponseMessage r, string what)
    {
        using (r)
        {
            try { return JsonNode.Parse(r.Content.ReadAsStringAsync().GetAwaiter().GetResult()) ?? throw new JsonException(); }
            catch (JsonException) { throw Unexpected(what); }
        }
    }

    static CommandException Unexpected(string what) => new($"The server could not {what}: its answer is not what a Converge IFC server gives (is the address right?).");

    /// <summary>An answer that is one object (an account, a file), or the call refused as not a Converge IFC server's.</summary>
    static JsonObject One(JsonNode n, string what) => n as JsonObject ?? throw Unexpected(what);

    /// <summary>An answer that is a list (the files of a project, the versions of a file), or the call refused as not a Converge IFC server's.</summary>
    static JsonArray Many(JsonNode n, string what) => n as JsonArray ?? throw Unexpected(what);

    static T Run<T>(Func<T> work, string what, string server)
    {
        try { return work(); }
        catch (CommandException) { throw; }
        catch (OperationCanceledException) { throw new CommandException("Stopped: nothing more was sent."); }
        catch (ServerRefused s)
        {
            var why = s.Status switch
            {
                HttpStatusCode.Unauthorized => "the server did not accept the token (check it, or make a new one)",
                HttpStatusCode.Forbidden => s.Detail is { Length: > 0 } ? s.Detail : "the account may not do that here (it needs the author role in the project)",
                HttpStatusCode.NotFound => "it is not there (is the address right, and are you in the project?)",
                HttpStatusCode.RequestEntityTooLarge => s.Detail is { Length: > 0 } ? s.Detail : "the file is larger than the server takes",
                _ => s.Detail is { Length: > 0 } ? s.Detail : $"the server answered {(int)s.Status}",
            };
            throw new CommandException($"The server ({server}) could not {what}: {why}");
        }
        catch (HttpRequestException h) { throw new CommandException($"The server ({server}) could not be reached: {h.Message}"); }
        catch (IOException io) { throw new CommandException($"Could not {what}: {io.Message}"); }
    }

    static ConvergeFile FileOf(JsonObject n) => new(Str(n, "container_id") ?? "", Str(n, "filename") ?? "", (int?)n["version_no"] ?? 0,
        Str(n, "revision_code") ?? "", Str(n, "status") ?? "", Str(n, "sha256") ?? "", (long?)n["size_bytes"] ?? 0, Date(Str(n, "updated_at")));

    static string? Str(JsonObject? o, string key) => o?[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    static DateTime? Date(string? s) => s != null && DateTimeOffset.TryParse(s, out var d) ? d.UtcDateTime : null;

    /// <summary>A file read for an upload, its progress told to the engine's (stage, done, total).</summary>
    sealed class ProgressStream(Stream inner, long length, Action<string, int, int>? progress) : Stream
    {
        long _done;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = inner.Read(buffer, offset, count);
            _done += n;
            progress?.Invoke("Uploading to the server", (int)Math.Min(_done, int.MaxValue), (int)Math.Min(length, int.MaxValue));
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
