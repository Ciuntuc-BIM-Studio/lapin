using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Host;

/// <summary>
/// The open project shared, as a game's listen server shares its match
/// (docs/plan-multiplayer.md): this engine stays the one that decides, the
/// project stays on this computer, and guests join through a relay with the
/// invitation: a link (the browser) or its code (the desktop's Join session).
/// <para>
/// The relay only pairs and moves frames. Each guest's frames are encrypted
/// end to end (AES-256-GCM) with a key drawn from the invitation's secret,
/// which never reaches the relay, and a salt of the guest's connection, so no
/// two connections share a key; every frame carries its direction and its
/// count, so none can be replayed or turned round. A guest is an editor or a
/// viewer, limited to what <see cref="EngineDispatcher.AllowedRemote"/> lets
/// through: nothing on this computer's disk. While shared, a project with a
/// file is saved every few minutes.
/// </para>
/// </summary>
public sealed class Sharing(EngineHub hub)
{
    /// <summary>The protocol's version: a guest of another one is refused with a word to update.</summary>
    public const int Protocol = 1;
    public const string DefaultRelay = "wss://lapin.ciuntucbimstudio.ro/relay";
    public const string DefaultJoinPage = "https://lapin.ciuntucbimstudio.ro/web/";
    static readonly string[] Colors = ["#d9480f", "#1971c2", "#2f9e44", "#ae3ec9", "#e8590c", "#0c8599", "#c2255c", "#5c940d", "#6741d9", "#f08c00"];

    readonly SemaphoreSlim _relaySending = new(1, 1);
    readonly Dictionary<uint, Guest> _guests = [];
    readonly Dictionary<string, Participant> _known = [];
    ClientWebSocket? _relay;
    CancellationTokenSource? _stop;
    string? _room;
    byte[]? _secret;
    string _relayUrl = DefaultRelay;
    string _role = "editor";
    int _count;

    /// <summary>The computer's own user, as the others see them.</summary>
    public Participant Host { get; } = new(History.Me, Environment.UserName is { Length: > 0 } u ? u : "Host", Colors[0], "owner");

    public bool Active => _relay?.State == WebSocketState.Open;

    public async Task<JsonNode?> Handle(string method, JsonObject p)
    {
        switch (method)
        {
            case "share.start": return await Start(p);
            case "share.stop": await Stop("The host stopped sharing."); return Status();
            case "share.status": return Status();
            case "share.setRole":
            {
                var who = Known((string?)p["id"]);
                who.Role = (string?)p["role"] == "viewer" ? "viewer" : "editor";
                foreach (var g in Guests().Where(g => g.Participant == who)) g.Caller = new Caller(who.Id, who.Role);
                await hub.BroadcastPresence();
                return Status();
            }
            case "share.kick":
            {
                var who = Known((string?)p["id"]);
                foreach (var g in Guests().Where(g => g.Participant == who)) await Close(g, "The host asked you to leave.");
                _known.Remove(who.Token);
                return Status();
            }
            default: throw new CommandException($"Unknown method „{method}”.");
        }
    }

    Participant Known(string? id) => _known.Values.FirstOrDefault(x => x.Id == id) ?? throw new CommandException("There is no such participant.");

    Guest[] Guests() { lock (_guests) return [.. _guests.Values]; }

    JsonObject Status()
    {
        var code = _room != null && _secret != null ? Code(_room, _secret, _relayUrl) : null;
        var page = Environment.GetEnvironmentVariable("LAPIN_JOIN_PAGE") ?? DefaultJoinPage;
        return new JsonObject
        {
            ["active"] = Active, ["code"] = code, ["link"] = code == null ? null : $"{page}#join={code}",
            ["role"] = _role, ["host"] = Host.Name, ["relay"] = _relayUrl,
            ["guests"] = new JsonArray(Guests().Where(g => g.Participant != null).Select(g => (JsonNode)g.Participant!.ToJson()).ToArray()),
        };
    }

    /// <summary>
    /// The invitation's code: the room, then the secret (base64url); the
    /// relay's address after them only when it is not Lapin's own.
    /// </summary>
    public static string Code(string room, byte[] secret, string relay) =>
        $"{room}.{Base64Url(secret)}{(relay == DefaultRelay ? "" : "." + Base64Url(Encoding.UTF8.GetBytes(relay)))}";

    static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    async Task<JsonNode> Start(JsonObject p)
    {
        if (Active) return Status();
        if ((string?)p["name"] is { Length: > 0 } name) Host.Name = name.Trim();
        _role = (string?)p["role"] == "viewer" ? "viewer" : "editor";
        _relayUrl = (string?)p["relay"] ?? Environment.GetEnvironmentVariable("LAPIN_RELAY") ?? DefaultRelay;
        // A new invitation each time: the last one's guests cannot come back with it.
        _room = RandomRoom();
        _secret = RandomNumberGenerator.GetBytes(16);
        _known.Clear();
        var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        try { await ws.ConnectAsync(new Uri($"{_relayUrl.TrimEnd('/')}/host?room={_room}"), CancellationToken.None); }
        catch (Exception ex) when (ex is WebSocketException or HttpRequestException or UriFormatException)
        {
            ws.Dispose();
            throw new CommandException($"The relay could not be reached ({_relayUrl}): {ex.Message}");
        }
        _relay = ws;
        _stop = new CancellationTokenSource();
        _ = Task.Run(() => Listen(ws, _stop.Token));
        _ = Task.Run(() => AutoSave(_stop.Token));
        await hub.BroadcastPresence();
        var status = Status();
        await hub.TellLocal("share", status);
        return status;
    }

    static string RandomRoom()
    {
        const string alphabet = "abcdefghijkmnpqrstuvwxyz23456789";
        return string.Concat(RandomNumberGenerator.GetBytes(12).Select(b => alphabet[b % alphabet.Length]));
    }

    public async Task Stop(string why)
    {
        var ws = _relay;
        _relay = null;
        _stop?.Cancel();
        foreach (var g in Guests()) await Leave(g, why);
        if (ws != null)
        {
            try { if (ws.State == WebSocketState.Open) await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); }
            catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException) { }
            ws.Dispose();
        }
        _room = null;
        _secret = null;
        await hub.BroadcastPresence();
        await hub.TellLocal("share", Status());
    }

    /// <summary>The relay's frames, until it closes: guests coming, their frames, guests leaving.</summary>
    async Task Listen(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[1 << 16];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(buffer, ct);
                    if (r.MessageType == WebSocketMessageType.Close) throw new WebSocketException("The relay closed.");
                    ms.Write(buffer, 0, r.Count);
                } while (!r.EndOfMessage);
                var frame = ms.ToArray();
                if (frame.Length < 5) continue;
                var gid = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(1));
                switch (frame[0])
                {
                    case 1: lock (_guests) _guests[gid] = new Guest(this, gid); break;
                    case 2:
                        Guest? gone;
                        lock (_guests) _guests.Remove(gid, out gone);
                        if (gone != null) await Leave(gone, null);
                        break;
                    case 3:
                        Guest? g;
                        lock (_guests) _guests.TryGetValue(gid, out g);
                        if (g != null) await Receive(g, frame.AsMemory(5));
                        break;
                }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            if (!ct.IsCancellationRequested && ReferenceEquals(_relay, ws)) await Stop("The connection to the relay was lost.");
        }
    }

    /// <summary>One frame from a guest: first its connection's salt, then its hello, then its calls.</summary>
    async Task Receive(Guest g, ReadOnlyMemory<byte> payload)
    {
        if (g.Crypto == null)
        {
            if (payload.Length != 17 || payload.Span[0] != 0) { await Close(g, null); return; }
            g.Crypto = new ShareCrypto(_secret!, _room!, payload.Span[1..].ToArray(), host: true);
            return;
        }
        if (g.Crypto.Open(payload.Span) is not { } plain || plain.Length < 1 || plain[0] != 1) { await Close(g, null); return; }
        var text = Encoding.UTF8.GetString(plain, 1, plain.Length - 1);
        if (g.Participant == null) { await Hello(g, text); return; }
        if (await hub.Handle(text, g) is { } reply) await g.SendBinary(reply);
    }

    /// <summary>A guest says who they are: welcomed as a participant (the same as before, when they come back with their token), or refused.</summary>
    async Task Hello(Guest g, string text)
    {
        var hello = (JsonNode.Parse(text) as JsonObject)?["hello"] as JsonObject;
        if (hello == null) { await Close(g, null); return; }
        if ((int?)hello["protocol"] != Protocol)
        {
            await g.SendText(new JsonObject { ["refused"] = $"The host runs Lapin {Session.AppVersion}, another version: update Lapin (Help → Check for updates) and join again." }.ToJsonString());
            await Close(g, null);
            return;
        }
        var name = ((string?)hello["name"])?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 40)] : "Guest";
        if ((string?)hello["token"] is not { } token || !_known.TryGetValue(token, out var who))
        {
            var number = ++_count;
            token = Base64Url(RandomNumberGenerator.GetBytes(16));
            who = new Participant($"p{number}", name, Colors[number % Colors.Length], _role) { Token = token };
            _known[token] = who;
        }
        else who.Name = name;
        g.Participant = who;
        g.Caller = new Caller(who.Id, who.Role);
        hub.Add(g);
        await g.SendText(new JsonObject
        {
            ["welcome"] = new JsonObject
            {
                ["id"] = who.Id, ["name"] = who.Name, ["color"] = who.Color, ["role"] = who.Role, ["token"] = who.Token,
                ["host"] = Host.Name, ["app"] = Session.AppVersion, ["product"] = hub.Session.Product.Id,
                ["modules"] = new JsonArray(hub.Session.Product.Modules.Select(m => (JsonNode)m).ToArray()),
            },
        }.ToJsonString());
        await hub.BroadcastPresence();
        await hub.TellLocal("share", Status());
    }

    async Task Leave(Guest g, string? why)
    {
        if (why != null) { try { await g.SendText(new JsonObject { ["event"] = "ended", ["data"] = why }.ToJsonString()); } catch (WebSocketException) { } }
        await hub.Remove(g);
        await hub.TellLocal("share", Status());
    }

    async Task Close(Guest g, string? why)
    {
        if (why != null) await g.SendText(new JsonObject { ["event"] = "ended", ["data"] = why }.ToJsonString());
        await ToRelay(4, g.Id, ReadOnlyMemory<byte>.Empty);
    }

    async Task ToRelay(byte type, uint guest, ReadOnlyMemory<byte> payload)
    {
        if (_relay is not { State: WebSocketState.Open } ws) return;
        var frame = new byte[5 + payload.Length];
        frame[0] = type;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(1), guest);
        payload.CopyTo(frame.AsMemory(5));
        await _relaySending.WaitAsync();
        try { await ws.SendAsync(frame, WebSocketMessageType.Binary, true, CancellationToken.None); }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException) { }
        finally { _relaySending.Release(); }
    }

    /// <summary>While shared, a project with a file is saved every few minutes: the work of all is not left to the host's Save.</summary>
    async Task AutoSave(CancellationToken ct)
    {
        var every = TimeSpan.FromMinutes(int.TryParse(Environment.GetEnvironmentVariable("LAPIN_SHARE_AUTOSAVE_MIN"), out var m) && m > 0 ? m : 5);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(every, ct);
                if (hub.Session.Path is { Length: > 0 } && hub.Session.History.Dirty)
                    await hub.Handle("{\"method\":\"project.save\",\"params\":{}}");
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>A guest through the relay: an engine client whose frames are sealed for them.</summary>
    sealed class Guest(Sharing owner, uint id) : EngineHub.Client
    {
        public uint Id => id;
        public ShareCrypto? Crypto { get; set; }
        public override Task SendBinary(byte[] frame) => Send(2, frame);
        public override Task SendText(string text) => Send(1, Encoding.UTF8.GetBytes(text));

        Task Send(byte kind, byte[] body) => Crypto == null ? Task.CompletedTask : owner.ToRelay(3, id, Crypto.Seal(kind, body));
    }
}

/// <summary>
/// One guest connection's encryption: AES-256-GCM, its key drawn (HKDF-SHA256)
/// from the invitation's secret, with the room and the connection's salt.
/// A frame is [nonce 12: direction 4, count 8][ciphertext + tag 16]; its
/// plaintext [kind 1][body], kind 1 JSON text, 2 an engine frame. A frame out
/// of order, from the wrong side, or that does not open is refused.
/// </summary>
public sealed class ShareCrypto
{
    readonly AesGcm _aes;
    readonly uint _out;
    readonly uint _in;
    ulong _sent;
    ulong _received;
    readonly object _lock = new();

    public ShareCrypto(byte[] secret, string room, byte[] salt, bool host)
    {
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, 32, [.. Encoding.UTF8.GetBytes(room), .. salt], Encoding.UTF8.GetBytes("lapin-share-v1"));
        _aes = new AesGcm(key, 16);
        (_out, _in) = host ? (1u, 0u) : (0u, 1u);
    }

    public byte[] Seal(byte kind, byte[] body)
    {
        var frame = new byte[12 + 1 + body.Length + 16];
        var nonce = frame.AsSpan(0, 12);
        lock (_lock)
        {
            BinaryPrimitives.WriteUInt32BigEndian(nonce, _out);
            BinaryPrimitives.WriteUInt64BigEndian(nonce[4..], ++_sent);
            var plain = new byte[1 + body.Length];
            plain[0] = kind;
            body.CopyTo(plain, 1);
            _aes.Encrypt(nonce, plain, frame.AsSpan(12, plain.Length), frame.AsSpan(12 + plain.Length, 16));
        }
        return frame;
    }

    public byte[]? Open(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 12 + 16 + 1) return null;
        var nonce = frame[..12];
        if (BinaryPrimitives.ReadUInt32BigEndian(nonce) != _in) return null;
        var count = BinaryPrimitives.ReadUInt64BigEndian(nonce[4..]);
        lock (_lock)
        {
            if (count <= _received) return null;
            var plain = new byte[frame.Length - 12 - 16];
            try { _aes.Decrypt(nonce, frame[12..^16], frame[^16..], plain); }
            catch (CryptographicException) { return null; }
            _received = count;
            return plain;
        }
    }
}
