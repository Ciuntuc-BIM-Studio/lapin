using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Host;

/// <summary>
/// Origins as a browser sends them (RFC 6454: <c>scheme://host[:port]</c>), compared exactly: scheme, host and port
/// equal, never a prefix, so <c>http://localhost.evil.com</c> or <c>http://127.0.0.1.evil.com</c> is not the app's own
/// (docs/plan-web-connect.md).
/// </summary>
public static class WebOrigins
{
    /// <summary>The desktop shell's own pages (Tauri on macOS/Linux, and on Windows).</summary>
    static readonly string[] Tauri = ["tauri://localhost", "http://tauri.localhost", "https://tauri.localhost"];
    static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "[::1]"];

    /// <summary>
    /// <paramref name="origin"/> when it is a serialized http(s) origin exactly as a browser writes it (its scheme and host
    /// in lower case, no default port spelled out, no path, no user, no trailing slash), else null. Tauri's own origin as it is.
    /// </summary>
    public static string? Normalize(string? origin)
    {
        if (string.IsNullOrEmpty(origin) || origin.Length > 512) return null;
        if (origin == Tauri[0]) return origin;
        if (Canonical(origin) is not { } canonical) return null;
        return string.Equals(origin, canonical, StringComparison.Ordinal) ? canonical : null;
    }

    /// <summary>An origin the user typed (Preferences): a trailing slash and upper case forgiven, else as <see cref="Normalize"/>.</summary>
    public static string? NormalizeTyped(string? origin)
    {
        var t = origin?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(t) || t.Length > 512) return null;
        if (Canonical(t) is not { } canonical) return null;
        return string.Equals(t, canonical, StringComparison.OrdinalIgnoreCase) ? canonical : null;
    }

    static string? Canonical(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var u)) return null;
        if (u.Scheme is not ("http" or "https")) return null;
        if (u.UserInfo.Length > 0 || u.AbsolutePath != "/" || u.Query.Length > 0 || u.Fragment.Length > 0) return null;
        var host = u.HostNameType == UriHostNameType.IPv6 ? u.Host : u.IdnHost;
        if (host.Length == 0) return null;
        return u.IsDefaultPort ? $"{u.Scheme}://{host}" : $"{u.Scheme}://{host}:{u.Port}";
    }

    /// <summary>Whether a page of this origin is the app's own: a page served on this computer's loopback (http, any port), or the desktop shell's.</summary>
    public static bool Own(string? origin)
    {
        if (Normalize(origin) is not { } o) return false;
        if (Tauri.Contains(o)) return true;
        var u = new Uri(o);
        return u.Scheme == "http" && LoopbackHosts.Contains(u.HostNameType == UriHostNameType.IPv6 ? u.Host : u.IdnHost);
    }

    /// <summary>Whether a site of this origin may ask the user to connect: https, not the app's own. An http site's page could be changed on the way.</summary>
    public static bool Pairable(string origin) => origin.StartsWith("https://", StringComparison.Ordinal) && !Own(origin);

    /// <summary>The host name an origin shows (what the participant is called when the site gives no name).</summary>
    public static string HostOf(string origin) => Uri.TryCreate(origin, UriKind.Absolute, out var u) ? u.Host : origin;
}

/// <summary>A site the user let connect for good (Preferences → Connected sites): its origin, whether it may edit, and since when.</summary>
public sealed record ConnectedSite(string Origin, bool Edit, DateTimeOffset AddedAt)
{
    public JsonObject ToJson() => new() { ["origin"] = Origin, ["edit"] = Edit, ["addedAt"] = AddedAt.ToString("o") };
}

/// <summary>
/// The sites the user let connect for good: <c>connected-sites.json</c> in the user's folder on this computer, never in a
/// project; empty until the user answers "Always allow". <c>{"version":1,"sites":[{"origin","edit","addedAt"}]}</c>.
/// Without a user folder (tests, an engine started without one) the list lives in memory only.
/// </summary>
public sealed class ConnectedSites
{
    public const string FileName = "connected-sites.json";
    readonly string? _file;
    readonly object _lock = new();
    readonly List<ConnectedSite> _sites = [];

    public ConnectedSites(string? userDir)
    {
        _file = userDir == null ? null : Path.Combine(userDir, FileName);
        if (_file == null || !File.Exists(_file)) return;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(_file))?["sites"] is not JsonArray list) return;
            foreach (var s in list.OfType<JsonObject>())
            {
                // An entry that is not an origin as a browser sends it would never match one: left out.
                if (WebOrigins.Normalize((string?)s["origin"]) is not { } origin || !WebOrigins.Pairable(origin) || _sites.Any(x => x.Origin == origin)) continue;
                var edit = s["edit"] is JsonValue ev && ev.TryGetValue<bool>(out var e) && e;
                var at = DateTimeOffset.TryParse((string?)s["addedAt"], out var t) ? t : DateTimeOffset.UnixEpoch;
                _sites.Add(new ConnectedSite(origin, edit, at));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            // Unreadable: no site is let in by it (the list starts empty), and the next change writes it anew.
            Console.Error.WriteLine($"[engine] {FileName} could not be read: {ex.Message}");
        }
    }

    public IReadOnlyList<ConnectedSite> List() { lock (_lock) return [.. _sites]; }

    public ConnectedSite? Find(string origin) { lock (_lock) return _sites.FirstOrDefault(s => s.Origin == origin); }

    /// <summary>The site added, or its edit grant changed (its date kept); written at once.</summary>
    public ConnectedSite Set(string origin, bool edit)
    {
        lock (_lock)
        {
            var i = _sites.FindIndex(s => s.Origin == origin);
            var site = new ConnectedSite(origin, edit, i >= 0 ? _sites[i].AddedAt : DateTimeOffset.UtcNow);
            if (i >= 0) _sites[i] = site; else _sites.Add(site);
            Save();
            return site;
        }
    }

    /// <summary>The site taken off the list (false: it was not on it); written at once.</summary>
    public bool Revoke(string origin)
    {
        lock (_lock)
        {
            if (_sites.RemoveAll(s => s.Origin == origin) == 0) return false;
            Save();
            return true;
        }
    }

    void Save()
    {
        if (_file == null) return;
        var json = new JsonObject { ["version"] = 1, ["sites"] = new JsonArray(_sites.Select(s => (JsonNode)s.ToJson()).ToArray()) };
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        AtomicFile.WriteText(_file, json.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>What a connection from another site may do: nothing until the user lets it in, then read, and edit if granted.</summary>
public sealed class WebAccess(string origin)
{
    public string Origin { get; } = origin;
    public bool Paired { get; internal set; }
    public bool Edit { get; internal set; }
    /// <summary>Let in by the list (<see cref="ConnectedSites"/>), not for this connection only.</summary>
    public bool Remembered { get; internal set; }
    public string? Name { get; internal set; }
}

/// <summary>
/// Web apps on other sites connecting to this engine (docs/plan-web-connect.md). A connection whose page is not one of
/// the app's own is let in, but paired with nothing: it may say hello (redacted) and ask to connect
/// (<c>pair.request</c>); the computer's own windows are asked (<c>pair.ask</c>) and the user answers
/// (<c>pair.answer</c>: once, always, or no; to read, or to edit too). Paired, it is a guest of the engine like a shared
/// session's (<see cref="EngineDispatcher.AllowedRemote"/>), a reader or an editor. The list of sites let in for good is
/// the user's (<c>sites.list</c>, <c>sites.set</c>, <c>sites.revoke</c>).
/// </summary>
public sealed class WebConnect(EngineHub hub, ConnectedSites sites)
{
    /// <summary>How long a request waits for the user before it is answered "no one answered".</summary>
    public static TimeSpan AskTimeout { get; set; } = TimeSpan.FromMinutes(2);
    /// <summary>How long a site the user refused (or revoked) is refused without asking again.</summary>
    public static TimeSpan DenyCooldown { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>The methods a connection from another site may call before it is paired.</summary>
    public static readonly string[] UnpairedMethods = ["pair.request", "engine.hello"];
    /// <summary>The host's own methods (past the queue) a paired site may call; every other is refused.</summary>
    public static readonly string[] PairedOwnMethods = ["pair.request", "engine.hello", "presence.update"];

    static readonly string[] Colors = ["#0c8599", "#5c940d", "#c2255c", "#6741d9", "#f08c00"];

    readonly object _lock = new();
    readonly List<EngineHub.Client> _connections = [];
    readonly Dictionary<string, Pending> _pending = [];
    readonly Dictionary<string, DateTimeOffset> _refusedUntil = [];
    int _count;

    public ConnectedSites Sites => sites;

    sealed class Pending(string requestId, string origin, string? name, bool wantsEdit)
    {
        public string RequestId { get; } = requestId;
        public string Origin { get; } = origin;
        public string? Name { get; } = name;
        public bool WantsEdit { get; } = wantsEdit;
        public List<(EngineHub.Client Client, JsonNode? Id)> Waiting { get; } = [];
        public CancellationTokenSource Timeout { get; } = new();
    }

    /// <summary>A connection from another site, unpaired.</summary>
    public void Track(EngineHub.Client c)
    {
        lock (_lock) _connections.Add(c);
    }

    EngineHub.Client[] Connections() { lock (_lock) return [.. _connections]; }

    /// <summary>A connection gone: off the list, and out of a request it waits on (withdrawn when it was the last).</summary>
    public async Task Forget(EngineHub.Client c)
    {
        Pending? withdrawn = null;
        bool had;
        lock (_lock)
        {
            had = _connections.Remove(c);
            foreach (var p in _pending.Values.ToList())
            {
                p.Waiting.RemoveAll(w => w.Client == c);
                if (p.Waiting.Count == 0) { _pending.Remove(p.Origin); withdrawn = p; }
            }
        }
        if (withdrawn != null)
        {
            withdrawn.Timeout.Cancel();
            await hub.TellLocal("pair.done", new JsonObject { ["requestId"] = withdrawn.RequestId, ["origin"] = withdrawn.Origin, ["decision"] = "withdrawn" });
        }
        if (had && c.Web?.Paired == true) await TellSites();
    }

    /// <summary>A site's <c>engine.hello</c>: that this is Lapin, which product and version, and where the connection stands; nothing of this computer's.</summary>
    public JsonObject Hello(WebAccess web) => new()
    {
        ["engine"] = "lapin", ["ok"] = true, ["product"] = hub.Session.Product.Id, ["version"] = Session.AppVersion,
        ["paired"] = web.Paired, ["edit"] = web.Paired && web.Edit,
    };

    /// <summary>
    /// A site asks to connect: let in at once when the list has it, refused when the user refused it a moment ago, else
    /// the windows asked and the reply posted when the user answers. Null: the reply comes later.
    /// </summary>
    public async Task<JsonObject?> Request(EngineHub.Client client, JsonNode? id, JsonObject p)
    {
        var web = client.Web ?? throw new CommandException("This window is the app's own: it needs no pairing.");
        if (web.Paired) return Granted(web);
        var name = ((string?)p["name"])?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 60)] : null;
        var wantsEdit = p["edit"] is JsonValue ev && ev.TryGetValue<bool>(out var e) && e;
        if (sites.Find(web.Origin) is { } site)
        {
            await Grant(client, name, site.Edit, remembered: true);
            return Granted(web);
        }
        Pending? ask = null;
        lock (_lock)
        {
            if (_refusedUntil.TryGetValue(web.Origin, out var until) && until > DateTimeOffset.UtcNow)
                return new JsonObject { ["granted"] = false, ["reason"] = "denied" };
            if (_pending.TryGetValue(web.Origin, out var waiting))
            {
                // Another tab of the same site: it waits on the same question.
                if (!waiting.Waiting.Any(w => w.Client == client)) waiting.Waiting.Add((client, id?.DeepClone()));
                return null;
            }
            ask = new Pending(Base64Url(RandomNumberGenerator.GetBytes(16)), web.Origin, name, wantsEdit);
            ask.Waiting.Add((client, id?.DeepClone()));
            _pending[web.Origin] = ask;
        }
        await hub.TellLocal("pair.ask", new JsonObject
        {
            ["requestId"] = ask.RequestId, ["origin"] = ask.Origin, ["name"] = ask.Name, ["wantsEdit"] = ask.WantsEdit,
        });
        _ = ExpireLater(ask);
        return null;
    }

    async Task ExpireLater(Pending ask)
    {
        try { await Task.Delay(AskTimeout, ask.Timeout.Token); }
        catch (TaskCanceledException) { return; }
        lock (_lock) { if (!_pending.TryGetValue(ask.Origin, out var still) || still != ask) return; _pending.Remove(ask.Origin); }
        foreach (var (c, id) in ask.Waiting) ReplyTo(c, id, new JsonObject { ["granted"] = false, ["reason"] = "timeout" });
        await hub.TellLocal("pair.done", new JsonObject { ["requestId"] = ask.RequestId, ["origin"] = ask.Origin, ["decision"] = "timeout" });
    }

    /// <summary>The user's answer to a request (this computer's windows only): once, always (the list), or deny; to read, or to edit too.</summary>
    public async Task<JsonObject> Answer(JsonObject p)
    {
        var requestId = (string?)p["requestId"] ?? throw new CommandException("Which request?");
        var decision = (string?)p["decision"];
        if (decision is not ("once" or "always" or "deny")) throw new CommandException("The decision is once, always or deny.");
        var edit = p["edit"] is JsonValue ev && ev.TryGetValue<bool>(out var e) && e;
        Pending ask;
        lock (_lock)
        {
            ask = _pending.Values.FirstOrDefault(x => x.RequestId == requestId) ?? throw new CommandException("There is no such request: it was answered, withdrawn or it expired.");
            _pending.Remove(ask.Origin);
            if (decision == "deny") _refusedUntil[ask.Origin] = DateTimeOffset.UtcNow + DenyCooldown;
            else _refusedUntil.Remove(ask.Origin);
        }
        ask.Timeout.Cancel();
        if (decision == "always") sites.Set(ask.Origin, edit);
        foreach (var (c, id) in ask.Waiting)
        {
            if (decision == "deny") { ReplyTo(c, id, new JsonObject { ["granted"] = false, ["reason"] = "denied" }); continue; }
            await Grant(c, ask.Name, edit, remembered: decision == "always");
            ReplyTo(c, id, Granted(c.Web!));
        }
        await hub.TellLocal("pair.done", new JsonObject { ["requestId"] = requestId, ["origin"] = ask.Origin, ["decision"] = decision, ["edit"] = decision != "deny" && edit });
        return new JsonObject { ["origin"] = ask.Origin, ["decision"] = decision, ["edit"] = decision != "deny" && edit, ["connections"] = decision == "deny" ? 0 : ask.Waiting.Count };
    }

    static JsonObject Granted(WebAccess web) => new() { ["granted"] = true, ["edit"] = web.Edit, ["remembered"] = web.Remembered };

    static void ReplyTo(EngineHub.Client c, JsonNode? id, JsonObject result)
    {
        if (id != null) c.Post(BinaryFrame.Encode(new Reply(id, result)));
    }

    /// <summary>A connection let in: a participant of its own (a reader or an editor), among the clients that get the diffs.</summary>
    async Task Grant(EngineHub.Client c, string? name, bool edit, bool remembered)
    {
        if (c.Web is not { } web) return;
        int number;
        lock (_lock) number = ++_count;
        var host = WebOrigins.HostOf(web.Origin);
        // The site's own name never stands alone: the origin is what the user trusts.
        var shown = name is { Length: > 0 } ? $"{name} ({host})" : host;
        web.Name = name;
        web.Remembered = remembered;
        var participant = new Participant($"w{number}", shown, Colors[number % Colors.Length], "reader");
        c.Participant = participant;
        SetEdit(c, edit);
        web.Paired = true;
        hub.Admit(c);
        await hub.BroadcastPresence();
        await TellSites();
    }

    static void SetEdit(EngineHub.Client c, bool edit)
    {
        var role = edit ? "editor" : EngineDispatcher.ReaderRole;
        c.Web!.Edit = edit;
        if (c.Participant is { } who) { who.Role = role; c.Caller = new Caller(who.Id, role); }
    }

    // ------------------------------------------------------------ the list (Preferences → Connected sites)

    public JsonObject List()
    {
        var live = Connections().Where(c => c.Web is { Paired: true }).ToList();
        return new JsonObject
        {
            ["sites"] = new JsonArray(sites.List().Select(s =>
            {
                var o = s.ToJson();
                o["connected"] = live.Count(c => c.Web!.Origin == s.Origin);
                return (JsonNode)o;
            }).ToArray()),
            ["connections"] = new JsonArray(live.Select(c => (JsonNode)new JsonObject
            {
                ["origin"] = c.Web!.Origin, ["name"] = c.Web.Name, ["edit"] = c.Web.Edit, ["remembered"] = c.Web.Remembered,
                ["participant"] = c.Participant?.Id,
            }).ToArray()),
        };
    }

    /// <summary>A site put on the list, or its edit grant changed; its connections now take the new grant.</summary>
    public async Task<JsonObject> Set(JsonObject p)
    {
        var origin = WebOrigins.NormalizeTyped((string?)p["origin"]) ?? throw new CommandException("That is not a site's origin (https://name[:port]).");
        if (!WebOrigins.Pairable(origin)) throw new CommandException("Only an https site that is not this app's own can be connected.");
        var edit = p["edit"] is JsonValue ev && ev.TryGetValue<bool>(out var e) && e;
        sites.Set(origin, edit);
        lock (_lock) _refusedUntil.Remove(origin);
        foreach (var c in Connections().Where(c => c.Web is { Paired: true } w && w.Origin == origin))
        {
            SetEdit(c, edit);
            c.Web!.Remembered = true;
        }
        await hub.BroadcastPresence();
        await TellSites();
        return List();
    }

    /// <summary>A site off the list, and its connections dropped at once (refused for a while, so it does not ask again at once).</summary>
    public async Task<JsonObject> Revoke(JsonObject p)
    {
        var origin = WebOrigins.NormalizeTyped((string?)p["origin"]) ?? throw new CommandException("That is not a site's origin (https://name[:port]).");
        var removed = sites.Revoke(origin);
        Pending? ask;
        lock (_lock)
        {
            _refusedUntil[origin] = DateTimeOffset.UtcNow + DenyCooldown;
            _pending.Remove(origin, out ask);
        }
        if (ask != null)
        {
            ask.Timeout.Cancel();
            await hub.TellLocal("pair.done", new JsonObject { ["requestId"] = ask.RequestId, ["origin"] = origin, ["decision"] = "deny" });
        }
        var dropped = 0;
        foreach (var c in Connections().Where(c => c.Web?.Origin == origin))
        {
            c.Web!.Paired = false;
            c.Caller = Unpaired;
            dropped++;
            await hub.Disconnect(c, System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation, "The user revoked this site's access to Lapin.");
        }
        await TellSites();
        var o = List();
        o["revoked"] = removed;
        o["dropped"] = dropped;
        return o;
    }

    /// <summary>The caller of a connection not let in: remote, with no role, so nothing would let it through even past the gate.</summary>
    public static readonly Caller Unpaired = new("web", EngineDispatcher.NoRole);

    /// <summary>The list and the live connections, to the windows (Preferences keeps up with them).</summary>
    Task TellSites() => hub.TellLocal("sites", List());

    static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
