using System.Net;
using System.Net.Sockets;

namespace Lapin.Host;

/// <summary>
/// The port the engine listens on (docs/python.md, How a program finds Lapin). Without a port of its own (no
/// <c>--port</c>, <c>--port 0</c> or <c>--port auto</c>) it is the first free of 14912 (L A P I N = 12 1 16 9 14 → 1 4 9 1 2),
/// 14913, … up to <see cref="Count"/> ports, so a program on this computer knows where to look: one Lapin on 14912,
/// a second on 14913. All of them taken: a port the system gives, said on stderr. <c>--port N</c> is that port, exactly.
/// </summary>
public static class EnginePort
{
    /// <summary>The first port an engine without a port of its own tries.</summary>
    public const int First = 14912;
    /// <summary>How many ports it tries: 14912–15011.</summary>
    public const int Count = 100;

    /// <summary>The <c>--port</c> argument: null for the automatic port (missing, 0, "auto"), else the port named.</summary>
    public static int? Parse(string? arg)
    {
        if (string.IsNullOrWhiteSpace(arg) || arg.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase)) return null;
        if (!int.TryParse(arg, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var p) || p < 0 || p > 65535)
            throw new ArgumentException($"--port: not a port: {arg} (a number, 0 or auto)");
        return p == 0 ? null : p;
    }

    /// <summary>
    /// Whether nothing listens on 127.0.0.1:<paramref name="port"/>, as far as can be told before binding it: a socket
    /// of our own binds it, as Kestrel's will (on Unix with SO_REUSEADDR, so the closed connections of an engine that
    /// just stopped do not hold the port), and, on Unix, nothing answers there (a program listening on all addresses,
    /// which that bind would not see, is not shadowed). Binding is what decides; this only skips the ports plainly taken.
    /// </summary>
    public static bool LooksFree(int port)
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            if (OperatingSystem.IsWindows()) s.ExclusiveAddressUse = true;
            else s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            s.Bind(new IPEndPoint(IPAddress.Loopback, port));
        }
        catch (SocketException) { return false; }
        // Windows refuses that bind under a listener on any address; elsewhere, ask (refused at once when none).
        if (OperatingSystem.IsWindows()) return true;
        try
        {
            using var c = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var connect = c.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));
            if (!connect.Wait(TimeSpan.FromMilliseconds(250))) return true;
            return false;
        }
        catch (AggregateException) { return true; }
        catch (SocketException) { return true; }
    }

    /// <summary>Whether binding failed because the port is taken (Kestrel wraps the socket's error).</summary>
    public static bool InUse(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
            if (e is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse or SocketError.AccessDenied }) return true;
        return ex is IOException && ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What <see cref="StartAsync"/> started: the server, its port, and whether the range was full.</summary>
    public sealed record Started(Microsoft.AspNetCore.Builder.WebApplication App, int Port, bool FellBack);

    /// <summary>
    /// The server built by <paramref name="build"/> for a port (0: the system's), started: on <paramref name="exact"/>
    /// when named (taken: the error), else on the first of <paramref name="first"/>… (<paramref name="count"/> ports) that
    /// binds. Another program taking a port between the look and the bind only moves us to the next.
    /// </summary>
    public static async Task<Started> StartAsync(Func<int, Microsoft.AspNetCore.Builder.WebApplication> build, int? exact, int first = First, int count = Count, TextWriter? log = null)
    {
        if (exact is { } named)
        {
            var app = build(named);
            await app.StartAsync();
            return new(app, BoundPort(app), false);
        }
        for (var port = first; port < first + count && port <= 65535; port++)
        {
            if (!LooksFree(port)) continue;
            var app = build(port);
            try
            {
                await app.StartAsync();
                return new(app, BoundPort(app), false);
            }
            catch (Exception ex) when (InUse(ex))
            {
                await app.DisposeAsync();
            }
        }
        (log ?? Console.Error).WriteLine($"[engine] Ports {first}–{first + count - 1} are all taken: listening on a port the system gives (see LAPIN_HOST_READY).");
        var any = build(0);
        await any.StartAsync();
        return new(any, BoundPort(any), true);
    }

    static int BoundPort(Microsoft.AspNetCore.Builder.WebApplication app) => new Uri(app.Urls.First()).Port;
}
