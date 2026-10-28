using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.WebSockets;

// The relay of shared sessions (docs/plan-multiplayer.md). A host's engine
// opens /relay/host?room=<room>; each guest opens /relay/join?room=<room>.
// The relay moves binary frames between them and reads none: they are
// encrypted end to end with the invitation's secret, which never reaches it.
//
//   host → relay   [3][guest u32][payload]   to that guest
//                  [4][guest u32]            close that guest
//   relay → host   [1][guest u32]            a guest came
//                  [2][guest u32]            a guest left
//                  [3][guest u32][payload]   from that guest
//   guest ↔ relay  [payload]                 as it is
//
//   Lapin.Relay [--urls http://127.0.0.1:3464]   (LAPIN_RELAY_MAX_ROOMS, LAPIN_RELAY_MAX_GUESTS)

var builder = WebApplication.CreateSlimBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

var maxRooms = int.TryParse(Environment.GetEnvironmentVariable("LAPIN_RELAY_MAX_ROOMS"), out var mr) ? mr : 500;
var maxGuests = int.TryParse(Environment.GetEnvironmentVariable("LAPIN_RELAY_MAX_GUESTS"), out var mg) ? mg : 16;
const int MaxMessage = 256 << 20;
var rooms = new ConcurrentDictionary<string, Room>();

static bool ValidRoom(string? room) => room is { Length: >= 8 and <= 32 } && room.All(c => char.IsAsciiLetterOrDigit(c));

app.MapGet("/relay/health", () => Results.Json(new { ok = true, rooms = rooms.Count }));

app.Map("/relay/host", async ctx =>
{
    var id = ctx.Request.Query["room"].ToString();
    if (!ctx.WebSockets.IsWebSocketRequest || !ValidRoom(id)) { ctx.Response.StatusCode = 400; return; }
    if (rooms.Count >= maxRooms) { ctx.Response.StatusCode = 503; return; }
    var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    var room = new Room(ws);
    if (!rooms.TryAdd(id, room)) { await ws.CloseAsync((WebSocketCloseStatus)4009, "room taken", default); return; }
    try
    {
        await foreach (var frame in Receive(ws, ctx.RequestAborted))
        {
            if (frame.Length < 5) continue;
            var guest = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(1));
            if (!room.Guests.TryGetValue(guest, out var g)) continue;
            if (frame[0] == 3) await g.Send(frame.AsMemory(5));
            else if (frame[0] == 4) await g.Close();
        }
    }
    finally
    {
        rooms.TryRemove(new(id, room));
        foreach (var g in room.Guests.Values) await g.Close();
    }
});

app.Map("/relay/join", async ctx =>
{
    var id = ctx.Request.Query["room"].ToString();
    if (!ctx.WebSockets.IsWebSocketRequest || !ValidRoom(id)) { ctx.Response.StatusCode = 400; return; }
    if (!rooms.TryGetValue(id, out var room)) { ctx.Response.StatusCode = 404; return; }
    if (room.Guests.Count >= maxGuests) { ctx.Response.StatusCode = 503; return; }
    var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    var guest = new Peer(ws);
    var gid = room.Join(guest);
    try
    {
        await room.Host.Send(Header(1, gid));
        await foreach (var frame in Receive(ws, ctx.RequestAborted))
        {
            var message = new byte[frame.Length + 5];
            Header(3, gid).CopyTo(message, 0);
            frame.CopyTo(message, 5);
            await room.Host.Send(message);
        }
    }
    finally
    {
        room.Guests.TryRemove(gid, out _);
        try { await room.Host.Send(Header(2, gid)); } catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException) { }
    }
});

app.Run();

static byte[] Header(byte type, uint guest)
{
    var h = new byte[5];
    h[0] = type;
    BinaryPrimitives.WriteUInt32BigEndian(h.AsSpan(1), guest);
    return h;
}

// Whole binary messages, until the socket closes or one is too big.
static async IAsyncEnumerable<byte[]> Receive(WebSocket ws, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
{
    var buffer = new byte[1 << 16];
    while (ws.State == WebSocketState.Open)
    {
        using var ms = new MemoryStream();
        WebSocketReceiveResult r;
        try
        {
            do
            {
                r = await ws.ReceiveAsync(buffer, ct);
                if (r.MessageType == WebSocketMessageType.Close) yield break;
                ms.Write(buffer, 0, r.Count);
                if (ms.Length > MaxMessage) yield break;
            } while (!r.EndOfMessage);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException) { yield break; }
        yield return ms.ToArray();
    }
}

/// <summary>A socket and the turn to send on it.</summary>
sealed class Peer(WebSocket ws)
{
    readonly SemaphoreSlim _sending = new(1, 1);

    public async Task Send(ReadOnlyMemory<byte> frame)
    {
        await _sending.WaitAsync();
        try { if (ws.State == WebSocketState.Open) await ws.SendAsync(frame, WebSocketMessageType.Binary, true, default); }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or OperationCanceledException) { }
        finally { _sending.Release(); }
    }

    public async Task Close()
    {
        try { if (ws.State == WebSocketState.Open) await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, default); }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException) { }
    }
}

/// <summary>One shared session: its host and its guests by number.</summary>
sealed class Room(WebSocket host)
{
    uint _next;
    public Peer Host { get; } = new(host);
    public ConcurrentDictionary<uint, Peer> Guests { get; } = new();

    public uint Join(Peer guest)
    {
        var id = Interlocked.Increment(ref _next);
        Guests[id] = guest;
        return id;
    }
}
