using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Lapin.Geometry;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Browser;

/// <summary>
/// The engine in a Web Worker: the page's worker script calls <see cref="Start"/>
/// once, then <see cref="Call"/> for each message of the protocol; the diff a
/// call causes is handed to <c>frame</c> before the reply comes back, as the
/// desktop's host sends it before the reply.
/// </summary>
[SupportedOSPlatform("browser")]
static partial class Engine
{
    static EngineDispatcher? _dispatcher;

    [JSImport("frame", "lapin-host")]
    internal static partial void Frame(byte[] frame);

    public static void Main() { }

    /// <summary>The session of the product this build is for, its journal in the browser's own storage. Returns the product's id.</summary>
    [JSExport]
    internal static string Start()
    {
        var session = new Session(ModuleCatalog.Create([.. Product.Modules(), new BrowserModule()]), Product.Manifest);
        var recovery = new Recovery(new BrowserJournalStore(), session);
        // No files of its own: projects come and go as bytes (project.openBytes, project.saveBytes).
        var files = new ProjectFiles(
            (_, _) => throw new CommandException("Pick the project to open: in the browser a project is opened from a file you choose."),
            (_, _, _, _) => throw new CommandException("In the browser a project is saved as a download (or to the file you chose)."));
        _dispatcher = new EngineDispatcher(session, files, recovery, new BrowserSupportDesk());
        return Product.Manifest.Id;
    }

    /// <summary>One call of the protocol: its reply frame (its diff, if any, went to <c>frame</c> first).</summary>
    [JSExport]
    internal static byte[] Call(string message)
    {
        var d = _dispatcher ?? throw new InvalidOperationException("The engine has not started.");
        var (reply, diff) = d.Handle(message);
        foreach (var f in d.TakeCallerFrames()) Frame(f);
        if (diff != null) foreach (var part in d.Parts(diff)) Frame(d.DiffFrame(part));
        return reply;
    }
}

/// <summary>
/// The help menu in the browser: a worker cannot wait for the network inside a
/// call, so a report comes back as the body to send (the same the desktop
/// posts) and the page posts it; the page opens the links itself.
/// </summary>
sealed class BrowserSupportDesk : ISupportDesk
{
    public System.Text.Json.Nodes.JsonObject Report(System.Text.Json.Nodes.JsonObject p) => new()
    {
        ["send"] = SupportReport.Body(p, typeof(Engine).Assembly.GetName().Version?.ToString() ?? "0", "browser", (string?)p["userAgent"] ?? "browser"),
    };

    public System.Text.Json.Nodes.JsonObject Messages(System.Text.Json.Nodes.JsonObject p)
    {
        var (endpoint, body) = SupportMessages.Request(p);
        return new() { ["endpoint"] = endpoint, ["send"] = body };
    }

    public void Open(string? link) => throw new CommandException("In the browser the page opens its links.");
}

/// <summary>
/// What the browser host adds to the product: the booleans. manifold is linked
/// into the engine's WebAssembly, so the desktop's own ManifoldBooleans runs as
/// it is; its probe (NativeLibrary.TryLoad) cannot see a library linked in, so
/// the host registers it.
/// </summary>
sealed class BrowserModule : ILapinModule
{
    public string Id => "browser";
    public string Name => "Browser";
    public void Register(IModuleRegistry r) => r.Service<ISolidBooleans>(new Lapin.Booleans.ManifoldBooleans());
}
