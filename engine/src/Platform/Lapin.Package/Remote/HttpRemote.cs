using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Package.Remote;

/// <summary>
/// The tokens of one sign-in (§9.2): held in memory for the call that uses them, handed in by the interface (which keeps the
/// refresh token in the system's keychain) and handed back when a refresh replaced them. Never written by the engine.
/// </summary>
public sealed class RemoteTokens
{
    public string? Access { get; set; }
    public string? Refresh { get; set; }
    /// <summary>Whether a refresh replaced them during the call: the interface stores the new refresh token at once (it is single-use).</summary>
    public bool Changed { get; set; }
}

/// <summary>A server's capabilities (§10.1): its limits and its features.</summary>
public sealed record RemoteCapabilities(string Version, long MaxObject, long Chunk, int MaxBatch, long MaxPack, int MaxCommitsPage, IReadOnlyList<string> Features, string Foundation)
{
    public static RemoteCapabilities From(JsonObject o)
    {
        var l = o["limits"] as JsonObject ?? [];
        return new RemoteCapabilities((string?)o["version"] ?? "1.0",
            (long?)l["maxObject"] ?? 2147483648L, (long?)l["chunk"] ?? 16777216L, (int?)l["maxBatch"] ?? 10_000, (long?)l["maxPack"] ?? 4294967296L,
            (int?)l["maxCommitsPage"] ?? 1000, (o["features"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [],
            (string?)o["auth"]?["foundation"] ?? "/foundation/1.1");
    }
    public bool Has(string feature) => Features.Contains(feature, StringComparer.Ordinal);
}

/// <summary>
/// A server remote speaking lapin-remote 1.0 over HTTP (docs/spec-lapin-remote.md §9–§10): the refs with their ETag, compare-and-swap
/// with 409, objects/missing, PUT of one object up to <c>chunk</c> and resumable uploads above it (tus's <c>Upload-Offset</c>), packs,
/// the commits walk and the viewer copy. Bearer tokens come from <see cref="RemoteTokens"/>; a 401 refreshes once and repeats once.
/// Asynchronous over <see cref="HttpClient"/> (<c>SendAsync</c>), so one client serves the desktop and the browser's engine, where
/// the client is the page's fetch (Atrium docs/plan-cde-web.md §2.1 A); the engine still answers one call at a time.
/// </summary>
public sealed class HttpRemote : IRemote
{
    public const string ClientId = "lapin-desktop";
    readonly HttpClient _http;
    readonly Uri _root;
    readonly string _base, _p;
    readonly RemoteTokens _tokens;
    RemoteCapabilities? _caps;

    public string Kind => "server";
    public string ProjectId { get; }
    public List<string> Notes { get; } = [];
    public int MaxBatch => Caps.MaxBatch;
    public bool Viewers => Caps.Has("viewer");

    RemoteCapabilities Caps => _caps ?? throw new InvalidOperationException("The server's capabilities are read first (ReadyAsync).");

    /// <summary>Whether an object goes through a resumable upload (default: above the server's <c>chunk</c>, as it must).</summary>
    public Func<string, long, bool>? UseUpload { get; init; }

    /// <summary>The most bytes the first PATCH of an upload sends (null: the session's chunk). The tests' broken first chunk.</summary>
    public int? FirstPatchLimit { get; init; }

    /// <param name="root">The server's root (where <c>/foundation/versions</c> is).</param>
    /// <param name="apiBase">The protocol's base, <c>api_base_url</c> (§9.1), absolute or relative to the root.</param>
    public HttpRemote(HttpClient http, string root, string apiBase, string project, RemoteTokens tokens)
    {
        _http = http;
        _root = new Uri(root.TrimEnd('/') + "/");
        _base = Absolute(apiBase).TrimEnd('/');
        ProjectId = project;
        _p = $"{_base}/projects/{Uri.EscapeDataString(project)}";
        _tokens = tokens;
    }

    /// <summary>A server's project: its root checked, its lapin-remote base discovered (§9.1) and its capabilities read.</summary>
    public static async Task<HttpRemote> OpenAsync(HttpClient http, string root, string project, RemoteTokens tokens)
    {
        var r = new HttpRemote(http, root, await DiscoverAsync(http, root), project, tokens);
        await r.ReadyAsync();
        return r;
    }

    string Absolute(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" ? u.ToString() : new Uri(_root, url.TrimStart('/')).ToString();

    /// <summary>
    /// The server's root checked (§15: https, or http only on a loopback address) and its lapin-remote base read from Foundation's
    /// <c>GET /foundation/versions</c> (§9.1): the base is taken from <c>api_base_url</c>, never built.
    /// </summary>
    public static async Task<string> DiscoverAsync(HttpClient http, string root)
    {
        var uri = CheckRoot(root);
        var o = await Json(await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(uri, "foundation/versions"))));
        var versions = o["versions"] as JsonArray ?? (o as JsonNode as JsonArray) ?? [];
        foreach (var v in versions.OfType<JsonObject>())
            if ((string?)v["api_id"] == "lapin-remote" && ((string?)v["version_id"])?.StartsWith("1.", StringComparison.Ordinal) == true && (string?)v["api_base_url"] is { } b)
                return b;
        throw new RemoteException(404, "not_found", $"{root} does not serve lapin-remote 1.0.");
    }

    /// <summary>A server's root: an absolute http(s) URL without user information, query or fragment; https unless on a loopback address.</summary>
    public static Uri CheckRoot(string root)
    {
        if (!Uri.TryCreate(root.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidDataException($"Not a server address: {root}.");
        if (uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0) throw new InvalidDataException("A server address carries no user, query or fragment.");
        if (uri.Scheme == "http" && !uri.IsLoopback) throw new InvalidDataException("A server other than this computer is reached over https only.");
        return uri;
    }

    /// <summary>
    /// A sign-in with the resource owner password grant (§9.2): the token URL from <c>GET /foundation/1.1/auth</c>, the form posted with
    /// <c>client_id</c>. The password goes no further than this request.
    /// </summary>
    public static async Task<RemoteTokens> SignInAsync(HttpClient http, string root, string username, string password, string clientId = ClientId)
    {
        var uri = CheckRoot(root);
        var auth = await Json(await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(uri, "foundation/1.1/auth"))));
        var flows = (auth["supported_oauth2_flows"] as JsonArray)?.Select(x => (string?)x).ToList() ?? [];
        if (!flows.Contains("resource_owner_password_credentials_grant")) throw new InvalidDataException("The server does not offer a sign-in with a password.");
        var url = (string?)auth["oauth2_token_url"] ?? throw new InvalidDataException("The server names no token address.");
        return await Token(http, new Uri(uri, url), [new("grant_type", "password"), new("username", username), new("password", password), new("client_id", clientId)]);
    }

    /// <summary>Atrium Server's own endpoint for the signed-in account's password, relative to the server's root (not part of lapin-remote).</summary>
    public const string PasswordPath = "atrium/1.0/account/password";

    /// <summary>
    /// The signed-in account's password changed on an Atrium Server (<c>POST /atrium/1.0/account/password</c>, Atrium's
    /// docs/atrium-server.md): the current and the new password, with the bearer token (refreshed once on a 401). The server ends
    /// every sign-in of the account and answers a fresh pair, put into <paramref name="tokens"/> (<see cref="RemoteTokens.Changed"/>).
    /// A refusal is a <see cref="RemoteException"/> with the server's code (<c>wrong_password</c>, <c>weak_password</c>,
    /// <c>too_many_attempts</c>) and a message to show; <c>not_supported</c> when the server has no such endpoint. The passwords go no
    /// further than this request.
    /// </summary>
    public static async Task ChangePasswordAsync(HttpClient http, string root, RemoteTokens tokens, string current, string next)
    {
        var uri = CheckRoot(root);
        var r = new HttpRemote(http, uri.ToString(), await DiscoverAsync(http, uri.ToString()), "-", tokens);
        using var res = await r.Call(() => new HttpRequestMessage(HttpMethod.Post, new Uri(uri, PasswordPath))
        {
            Content = JsonBody(new JsonObject { ["currentPassword"] = current, ["newPassword"] = next }),
        });
        var text = await res.Content.ReadAsStringAsync();
        JsonObject o;
        try { o = JsonNode.Parse(text) as JsonObject ?? []; }
        catch (JsonException) { o = []; }
        if (!res.IsSuccessStatusCode)
        {
            var status = (int)res.StatusCode;
            var code = (string?)o["code"] ?? (status == 404 || status == 405 ? "not_supported" : "error");
            var message = code switch
            {
                "not_supported" => "This server does not let you change your password here: ask its administrator.",
                "unauthorized" => "Sign in to the server again.",
                "wrong_password" => "The current password is wrong.",
                "too_many_attempts" => "Too many wrong passwords: try again later.",
                _ => (string?)o["message"] ?? $"The server answered {status}.",
            };
            if (status == 401) code = "unauthorized";
            throw new RemoteException(status, code, message, o);
        }
        tokens.Access = (string?)o["access_token"] ?? throw new InvalidDataException("The server gave no new token.");
        tokens.Refresh = (string?)o["refresh_token"];
        tokens.Changed = true;
    }

    static async Task<RemoteTokens> Token(HttpClient http, Uri url, List<KeyValuePair<string, string>> form)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        using var res = await http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        JsonObject o;
        try { o = JsonNode.Parse(text) as JsonObject ?? []; }
        catch (JsonException) { o = []; }
        if (!res.IsSuccessStatusCode)
            throw new RemoteException((int)res.StatusCode, (string?)o["error"] ?? "invalid_grant", (string?)o["error_description"] ?? (string?)o["message"] ?? "The sign-in was refused.");
        return new RemoteTokens { Access = (string?)o["access_token"], Refresh = (string?)o["refresh_token"], Changed = true };
    }

    async Task<bool> RefreshTokens()
    {
        if (_tokens.Refresh is not { Length: > 0 } refresh) return false;
        try
        {
            var foundation = (await CapabilitiesAsync()).Foundation.TrimStart('/').TrimEnd('/');
            var auth = await Json(await _http.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(_root, foundation + "/auth"))));
            var url = (string?)auth["oauth2_token_url"] ?? throw new InvalidDataException("The server names no token address.");
            var t = await Token(_http, new Uri(_root, url), [new("grant_type", "refresh_token"), new("refresh_token", refresh), new("client_id", ClientId)]);
            _tokens.Access = t.Access;
            _tokens.Refresh = t.Refresh;
            _tokens.Changed = true;
            return true;
        }
        catch (RemoteException) { _tokens.Refresh = null; _tokens.Changed = true; return false; }
    }

    // ------------------------------------------------------------ requests

    /// <summary>A request with the bearer token; on 401 the tokens are refreshed once and the request made once more (<paramref name="make"/> builds it anew).</summary>
    async Task<HttpResponseMessage> Call(Func<HttpRequestMessage> make, bool anonymous = false)
    {
        Task<HttpResponseMessage> Once()
        {
            var req = make();
            if (!anonymous && _tokens.Access is { Length: > 0 } t) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", t);
            return _http.SendAsync(req);
        }
        var res = await Once();
        if (res.StatusCode == HttpStatusCode.Unauthorized && !anonymous && await RefreshTokens())
        {
            res.Dispose();
            res = await Once();
        }
        return res;
    }

    /// <summary>A response that is not a success turned into the protocol's refusal (§9.6).</summary>
    static async Task<RemoteException> Error(HttpResponseMessage res)
    {
        JsonObject body = [];
        try { body = JsonNode.Parse(await res.Content.ReadAsStringAsync()) as JsonObject ?? []; }
        catch (JsonException) { }
        var status = (int)res.StatusCode;
        var code = (string?)body["code"] ?? status switch { 401 => "unauthorized", 403 => "forbidden", 404 => "not_found", 409 => "conflict", 413 => "too_large", _ => "error" };
        var message = status switch
        {
            401 => "Sign in to the server again.",
            403 => "Your role in this project does not allow this.",
            404 when code == "not_found" => "The project does not exist on the server, or is not yours to see.",
            _ => (string?)body["message"] ?? $"The server answered {status}.",
        };
        return new RemoteException(status, code, message, body);
    }

    static async Task<JsonObject> Json(HttpResponseMessage res)
    {
        using (res)
        {
            if (!res.IsSuccessStatusCode) throw await Error(res);
            var text = await res.Content.ReadAsStringAsync();
            return JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException("The server's answer is not a JSON object.");
        }
    }

    static async Task Ok(HttpResponseMessage res)
    {
        using (res) if (!res.IsSuccessStatusCode) throw await Error(res);
    }

    static StringContent JsonBody(JsonNode body)
    {
        var c = new StringContent(body.ToJsonString(), Encoding.UTF8);
        c.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json; charset=utf-8");
        return c;
    }

    static ByteArrayContent Octets(byte[] bytes, int offset, int count, string type = "application/octet-stream")
    {
        var c = new ByteArrayContent(bytes, offset, count);
        c.Headers.ContentType = new MediaTypeHeaderValue(type);
        return c;
    }

    // ------------------------------------------------------------ endpoints

    public async Task ReadyAsync() => await CapabilitiesAsync();

    public async Task<RemoteCapabilities> CapabilitiesAsync()
    {
        if (_caps != null) return _caps;
        var o = await Json(await Call(() => new HttpRequestMessage(HttpMethod.Get, $"{_base}/capabilities"), anonymous: true));
        if ((string?)o["protocol"] != "lapin-remote" || (o["majors"] as JsonArray)?.Any(x => (int?)x == 1) != true) throw new InvalidDataException("The server does not speak lapin-remote 1.");
        return _caps = RemoteCapabilities.From(o);
    }

    /// <summary>The projects the signed-in user is a member of (§10.2), every page.</summary>
    public async Task<List<RemoteProject>> ProjectsAsync()
    {
        var o = new List<RemoteProject>();
        string? cursor = null;
        do
        {
            var url = $"{_base}/projects" + (cursor != null ? $"?cursor={Uri.EscapeDataString(cursor)}" : "");
            var page = await Json(await Call(() => new HttpRequestMessage(HttpMethod.Get, url)));
            foreach (var p in (page["projects"] as JsonArray ?? []).OfType<JsonObject>())
                if ((string?)p["id"] is { } id) o.Add(new RemoteProject(id, (string?)p["name"] ?? id, RefNames.Main, (string?)p["role"]));
            cursor = page["more"] is JsonValue m && m.TryGetValue<bool>(out var more) && more ? (string?)page["cursor"] : null;
        } while (cursor != null);
        return o;
    }

    /// <summary>A new project on the server (feature <c>projects.create</c>); its id.</summary>
    public static async Task<RemoteProject> CreateProjectAsync(HttpClient http, string root, string apiBase, RemoteTokens tokens, string name)
    {
        var r = new HttpRemote(http, root, apiBase, "-", tokens);
        if (!(await r.CapabilitiesAsync()).Has("projects.create")) throw new InvalidDataException("The server does not let projects be made from Lapin: ask its administrator.");
        var o = await Json(await r.Call(() => new HttpRequestMessage(HttpMethod.Post, $"{r._base}/projects") { Content = JsonBody(new JsonObject { ["name"] = name }) }));
        return new RemoteProject((string?)o["id"] ?? throw new InvalidDataException("The server gave the project no id."), (string?)o["name"] ?? name, (string?)o["defaultRef"] ?? RefNames.Main, (string?)o["role"]);
    }

    public async Task<RemoteProject> ProjectAsync()
    {
        await ReadyAsync();
        var o = await Json(await Call(() => new HttpRequestMessage(HttpMethod.Get, _p)));
        return new RemoteProject((string?)o["id"] ?? ProjectId, (string?)o["name"] ?? ProjectId, (string?)o["defaultRef"] ?? RefNames.Main, (string?)o["role"]);
    }

    static List<RemoteRef> ReadRefs(JsonObject o) =>
        (o["refs"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(r => RefNames.Valid((string?)r["name"]) && ObjectIds.Valid((string?)r["target"]))
            .Select(r => new RemoteRef((string)r["name"]!, (string)r["target"]!, (string?)r["updatedAt"], RemoteAuthor.From(r["updatedBy"])))
            .ToList();

    public async Task<RefsResult> RefsAsync(string? etag)
    {
        using var res = await Call(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{_p}/refs");
            if (etag != null) req.Headers.TryAddWithoutValidation("If-None-Match", etag);
            return req;
        });
        var tag = res.Headers.ETag?.ToString();
        if (res.StatusCode == HttpStatusCode.NotModified) return new RefsResult(true, [], tag ?? etag);
        if (!res.IsSuccessStatusCode) throw await Error(res);
        var o = JsonNode.Parse(await res.Content.ReadAsStringAsync()) as JsonObject ?? [];
        return new RefsResult(false, ReadRefs(o), tag);
    }

    public async Task<RefsResult> UpdateRefsAsync(IReadOnlyList<RefUpdate> updates)
    {
        var body = new JsonObject
        {
            ["updates"] = new JsonArray(updates.Select(u => (JsonNode)new JsonObject { ["name"] = u.Name, ["old"] = u.Old, ["new"] = u.New }).ToArray()),
        };
        using var res = await Call(() => new HttpRequestMessage(HttpMethod.Post, $"{_p}/refs/update") { Content = JsonBody(body) });
        if (!res.IsSuccessStatusCode) throw await Error(res);
        var o = JsonNode.Parse(await res.Content.ReadAsStringAsync()) as JsonObject ?? [];
        return new RefsResult(false, ReadRefs(o), res.Headers.ETag?.ToString());
    }

    public async Task<IReadOnlyList<string>> MissingAsync(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0) return [];
        var body = new JsonObject { ["hashes"] = new JsonArray(ids.Select(i => (JsonNode)i).ToArray()) };
        var o = await Json(await Call(() => new HttpRequestMessage(HttpMethod.Post, $"{_p}/objects/missing") { Content = JsonBody(body) }));
        return (o["missing"] as JsonArray ?? []).Select(x => (string?)x).Where(ObjectIds.Valid).Select(x => x!).ToList();
    }

    public async Task PutAsync(string id, byte[] bytes, ObjectKind kind)
    {
        var caps = await CapabilitiesAsync();
        if (bytes.LongLength > caps.MaxObject) throw new RemoteException(413, "too_large", $"An object of {bytes.LongLength} bytes is above the server's limit ({caps.MaxObject}).");
        if (UseUpload?.Invoke(id, bytes.LongLength) ?? bytes.LongLength > caps.Chunk) { await Upload(id, bytes); return; }
        await Ok(await Call(() => new HttpRequestMessage(HttpMethod.Put, $"{_p}/objects/{id}") { Content = Octets(bytes, 0, bytes.Length) }));
    }

    /// <summary>
    /// A resumable upload (§10.9): a session opened (or resumed), then PATCHes of up to <c>chunk</c> bytes at the server's offset; a
    /// PATCH that breaks off is followed by <c>HEAD</c>, which tells where to go on from; an offset the server disagrees with (409) too.
    /// </summary>
    async Task Upload(string id, byte[] bytes)
    {
        var start = await Json(await Call(() => new HttpRequestMessage(HttpMethod.Post, $"{_p}/uploads") { Content = JsonBody(new JsonObject { ["sha256"] = id, ["size"] = bytes.LongLength }) }));
        if (start["exists"] is JsonValue ev && ev.TryGetValue<bool>(out var exists) && exists) return;
        var session = (string?)start["id"] ?? throw new InvalidDataException("The server opened no upload session.");
        var url = $"{_p}/uploads/{Uri.EscapeDataString(session)}";
        var chunk = (long?)start["chunk"] ?? (await CapabilitiesAsync()).Chunk;
        var offset = (long?)start["offset"] ?? 0;
        var first = true;
        var failures = 0;
        while (offset < bytes.LongLength)
        {
            var length = (int)Math.Min(chunk, bytes.LongLength - offset);
            if (first && FirstPatchLimit is { } limit) length = Math.Min(length, limit);
            first = false;
            HttpResponseMessage res;
            try
            {
                var at = offset;
                res = await Call(() =>
                {
                    var req = new HttpRequestMessage(HttpMethod.Patch, url) { Content = Octets(bytes, (int)at, length, "application/offset+octet-stream") };
                    req.Headers.TryAddWithoutValidation("Upload-Offset", at.ToString(CultureInfo.InvariantCulture));
                    return req;
                });
            }
            catch (HttpRequestException) when (++failures <= 5) { offset = await Where(url); continue; }
            catch (IOException) when (++failures <= 5) { offset = await Where(url); continue; }
            using (res)
            {
                if (res.StatusCode == HttpStatusCode.Conflict)
                {
                    if (++failures > 5) throw await Error(res);
                    offset = Offset(res) ?? await Where(url);
                    continue;
                }
                if (!res.IsSuccessStatusCode) throw await Error(res);
                if (res.StatusCode == HttpStatusCode.Created) return;
                offset = Offset(res) ?? offset + length;
            }
        }
    }

    static long? Offset(HttpResponseMessage res) =>
        res.Headers.TryGetValues("Upload-Offset", out var v) && long.TryParse(v.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var o) ? o : null;

    async Task<long> Where(string url)
    {
        using var res = await Call(() => new HttpRequestMessage(HttpMethod.Head, url));
        if (!res.IsSuccessStatusCode) throw await Error(res);
        return Offset(res) ?? throw new InvalidDataException("The server did not say where the upload stands.");
    }

    public async Task<byte[]?> GetAsync(string id)
    {
        using var res = await Call(() => new HttpRequestMessage(HttpMethod.Get, $"{_p}/objects/{id}"));
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode) throw await Error(res);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        if (ObjectIds.Of(bytes) != id) throw new InvalidDataException($"The server sent bytes that do not hash to {id[..12]}….");
        return bytes;
    }

    public async Task<IReadOnlyDictionary<string, byte[]>> GetManyAsync(IReadOnlyList<string> ids)
    {
        var o = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var distinct = ids.Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0) return o;
        if ((await CapabilitiesAsync()).Has("pack"))
        {
            var body = new JsonObject { ["hashes"] = new JsonArray(distinct.Select(i => (JsonNode)i).ToArray()) };
            using var res = await Call(() => new HttpRequestMessage(HttpMethod.Post, $"{_p}/objects/pack") { Content = JsonBody(body) });
            if (!res.IsSuccessStatusCode) throw await Error(res);
            try
            {
                using var ms = new MemoryStream(await res.Content.ReadAsByteArrayAsync());
                using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                foreach (var e in zip.Entries)
                {
                    if (!ObjectIds.Valid(e.FullName)) continue;
                    using var s = e.Open();
                    using var b = new MemoryStream();
                    s.CopyTo(b);
                    var bytes = b.ToArray();
                    // An entry that does not hash to its name is taken again on its own below.
                    if (ObjectIds.Of(bytes) == e.FullName) o[e.FullName] = bytes;
                }
            }
            catch (InvalidDataException) { }
        }
        foreach (var id in distinct.Where(i => !o.ContainsKey(i)))
            o[id] = await GetAsync(id) ?? throw new RemoteException(404, "object_missing", $"The server lacks {id[..12]}….");
        return o;
    }

    public async Task<IReadOnlyList<(string Id, byte[] Bytes)>> CommitsAsync(string from, IReadOnlyList<string> stop)
    {
        var o = new List<(string, byte[])>();
        string? cursor = null;
        var limit = Math.Min(100, (await CapabilitiesAsync()).MaxCommitsPage);
        do
        {
            var q = new StringBuilder($"{_p}/commits?from={from}");
            if (stop.Count > 0) q.Append("&stop=").Append(string.Join(',', stop.Take(100)));
            q.Append("&limit=").Append(limit.ToString(CultureInfo.InvariantCulture));
            if (cursor != null) q.Append("&cursor=").Append(Uri.EscapeDataString(cursor));
            var url = q.ToString();
            var page = await Json(await Call(() => new HttpRequestMessage(HttpMethod.Get, url)));
            foreach (var c in (page["commits"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var id = ObjectIds.Require((string?)c["id"], "commit");
                // The commit as JSON: canonical, so its canonical form is its bytes (§10.10, B9), checked against its id.
                var bytes = Jcs.Canonicalize(c["commit"]);
                if (ObjectIds.Of(bytes) != id) throw new InvalidDataException($"The server's commit {id[..12]}… does not hash to its id.");
                o.Add((id, bytes));
            }
            cursor = page["more"] is JsonValue m && m.TryGetValue<bool>(out var more) && more ? (string?)page["cursor"] : null;
        } while (cursor != null);
        return o;
    }

    public async Task PutViewerAsync(string commit, string ifc, long size, string schema)
    {
        var body = new JsonObject { ["ifc"] = ifc, ["schema"] = schema };
        await Ok(await Call(() => new HttpRequestMessage(HttpMethod.Put, $"{_p}/commits/{commit}/viewer") { Content = JsonBody(body) }));
    }

    /// <summary>
    /// Foundation's current user (OpenCDE 1.1, <c>GET /foundation/1.1/current-user</c>) with the access token: its JSON, or null when
    /// the server does not say.
    /// </summary>
    public static async Task<JsonObject?> CurrentUserAsync(HttpClient http, string root, string? access)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(CheckRoot(root), "foundation/1.1/current-user"));
        if (access is { Length: > 0 }) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using var res = await http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return null;
        return JsonNode.Parse(await res.Content.ReadAsStringAsync()) as JsonObject;
    }

    public IReadOnlyList<DetachedEntry> Detached() => [];

    public void Dispose() { }
}
