using System.Diagnostics;
using Lapin.Host;
using Lapin.Platform;
using Lapin.Protocol;

// The local engine. Started by the desktop shell (or by `pnpm dev`), or without a window to run a job (--run, HeadlessRun):
//   Lapin.Host --product <product.json> --modules-dir <dir> [--user-dir <dir>] [--port auto|N] [--parent-pid <pid>] [--python-dir <dir>]
// It listens on 127.0.0.1 only: without a port of its own (auto, 0 or none: the desktop app's) on the first free of
// 14912, 14913, … (EnginePort); with --port N on N, exactly. It prints one line the shell waits for:
//   LAPIN_HOST_READY {"port":14912}

string Arg(string name, string? fallback = null)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback ?? throw new ArgumentException($"Missing {name}");
}

var product = ModuleLoader.ReadProduct(Arg("--product"));
var modules = ModuleLoader.Load(Arg("--modules-dir"), product.Modules);

// Without a window (docs/headless-run.md): --run <job.json | -> [--out <answer.json>] opens a project, runs the job's steps,
// answers and ends; no port, no recovery journal, nothing kept in a user folder (the run's own temporary one, removed after).
if (Array.IndexOf(args, "--run") >= 0)
{
    var scratch = Path.Combine(Path.GetTempPath(), $"lapin-run-{Guid.NewGuid():N}");
    Directory.CreateDirectory(scratch);
    try
    {
        Views.UseProduct(product);
        PointCloudLinks.CacheDir = Path.Combine(scratch, "PointClouds");
        Lapin.Package.PackageBases.Folder = Path.Combine(scratch, "bases");
        // A copy of the project opened (what a save carries over) is not needed: the job saves only with saveAs, from the session.
        Lapin.Package.PackageBases.MaxCopy = 0;
        var headless = new Session(ModuleCatalog.Create(modules), product);
        return await HeadlessRun.RunFile(headless, Arg("--run"), Arg("--out", "") is { Length: > 0 } outPath ? outPath : null);
    }
    finally
    {
        try { Directory.Delete(scratch, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
var port = EnginePort.Parse(Arg("--port", "auto"));
var parentPid = int.Parse(Arg("--parent-pid", "0"));
// The user's own data on this computer: the recovery journals and the project templates. The shell passes its app data folder.
var userDir = Arg("--user-dir", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lapin"));
Views.UseProduct(product);
// Point clouds' indexes (docs/point-clouds.md) in the user's folder too, never in a project.
PointCloudLinks.CacheDir ??= Path.Combine(userDir, "PointClouds");
// The copies of the open projects' files (what a save carries over and a journal starts from, audit 15.11.2026 G1) beside the journals;
// those of Lapins that ended, which no journal starts from, go (before this session's first is taken).
Lapin.Package.PackageBases.Folder ??= Path.Combine(userDir, "recovery", "bases");
Lapin.Package.PackageBases.Sweep(new FileJournalStore(Path.Combine(userDir, "recovery")).Starts());
// Extension modules (docs/plan-extension-modules.md §2.1): after the product's, each checked whole (the store's signature, every file's
// hash, its contract and requirements) and loaded into its own context; one that fails, or whose Register throws, is turned off with why,
// and the engine starts.
var extensionHost = new ExtensionHostInfo(Path.Combine(userDir, "extensions"), StoreKeys.All,
    product.Modules.Append(CoreModule.ModuleId).ToHashSet(), ModuleLoader.Apis(Arg("--modules-dir"), product.Modules),
    Directory.Exists(Arg("--modules-dir")) ? Directory.EnumerateDirectories(Arg("--modules-dir")).Select(Path.GetFileName).OfType<string>().ToHashSet() : null);
List<ExtensionState> extensions;
try { extensions = ExtensionLoader.Load(extensionHost); }
catch (Exception e) when (e is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"lapin: extension modules not loaded: {e.Message}");
    extensions = [];
}
var catalog = ModuleCatalog.Create(modules, extensions.Where(x => x.Module != null).Select(x => x.Module!), (m, e) => ExtensionLoader.Failed(extensions, m, e));
// The modules an extension brought are marked, so the projects saved here name their extension (docs/plan-extension-modules.md §4.4).
foreach (var x in extensions.Where(x => x.Module != null && catalog.Modules.Any(m => m.Id == x.Module.Id))) catalog.MarkExtension(x.Module!.Id, x.Id, x.Version);
foreach (var x in extensions.Where(x => x.Problem != null)) Console.Error.WriteLine($"lapin: extension {x.Id} {x.Version} not loaded: {x.Problem}");
var session = new Session(catalog, product);
using var recovery = new Recovery(userDir, session);

var hub = new EngineHub(session, PackageFiles.Create(), recovery, userDir) { Extensions = new ExtensionInstaller(extensionHost, extensions) };
// The lapinbim package the Script panel's Python runs (docs/plan-python-scripting.md): named, else beside the modules (the app's resources/python), else the checkout's python/.
hub.ModulesDir = Arg("--modules-dir");
hub.Scripts.PackageDir = Arg("--python-dir", "") is { Length: > 0 } pythonDir ? pythonDir : ScriptHost.FindPackage(Arg("--modules-dir"));

// The server on one port; built again for the next when another program took the port first (EnginePort.StartAsync).
WebApplication Build(int listenPort)
{
    var builder = WebApplication.CreateSlimBuilder(args);
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
    builder.WebHost.UseKestrel(o => o.Listen(System.Net.IPAddress.Loopback, listenPort));
    var web = builder.Build();
    web.UseWebSockets();
    // Who answers here, for a program looking for Lapin on 14912… (docs/python.md): what engine.hello answers. Only
    // asked by a loopback name and this port, so a page of another site whose name was pointed at 127.0.0.1 does not read
    // it, and never for a page of another site (no CORS headers either: a browser keeps the answer from it).
    web.MapGet("/health", (HttpContext ctx) =>
        EngineHub.AllowedHost(ctx.Request.Host.Host, ctx.Request.Host.Port, listenPort)
        && EngineHub.Classify(ctx.Request.Headers.Origin.ToString(), out _) == EngineHub.OriginKind.Own
            ? Results.Json(hub.Hello()) : Results.StatusCode(403));
    // A browser sign-in's answer (RFC 8252 §7.3: the server sends the browser back to this loopback address with the code, or an
    // error, and the state it was given; Lapin.Protocol.BrowserSignIn). Taken only for a sign-in this engine started; nothing of it logged.
    web.MapGet("/oauth/callback", (HttpContext ctx) =>
    {
        var q = ctx.Request.Query;
        string? One(string k) => q[k].Count == 1 ? q[k].ToString() : null;
        var (_, page) = Lapin.Protocol.BrowserSignIn.Arrive(One("state"), One("code"), One("error"), One("error_description"), One("iss"));
        ctx.Response.Headers.CacheControl = "no-store";
        ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
        ctx.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'";
        return Results.Content(page, "text/html; charset=utf-8");
    });
    web.Map("/ws", async ctx =>
    {
        if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
        // Asked by this engine's own address only (DNS rebinding: another site's name pointed at 127.0.0.1 is refused).
        if (!EngineHub.AllowedHost(ctx.Request.Host.Host, ctx.Request.Host.Port, listenPort)) { ctx.Response.StatusCode = 403; return; }
        // The app's own pages (and programs, which send no Origin) as ever; an https site is let in paired with nothing
        // until the user lets it (docs/plan-web-connect.md); any other origin refused.
        var kind = EngineHub.Classify(ctx.Request.Headers.Origin.ToString(), out var site);
        if (kind == EngineHub.OriginKind.Refused) { ctx.Response.StatusCode = 403; return; }
        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        await hub.Serve(ws, ctx.RequestAborted, site);
    });
    return web;
}

var started = await EnginePort.StartAsync(Build, port);
var app = started.App;
var bound = started.Port;
hub.Port = bound;
hub.Scripts.Port = bound;
app.Lifetime.ApplicationStopping.Register(hub.Scripts.Dispose);
// The browser sign-in's answer comes back here, on this engine's own port; the server's page opens in the system's browser.
Lapin.Protocol.BrowserSignIn.CallbackUrl = $"http://127.0.0.1:{bound}/oauth/callback";
Lapin.Protocol.BrowserSignIn.OpenBrowser = url =>
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("https" or "http")) return;
    Process.Start(new ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true });
};
Console.WriteLine($"LAPIN_HOST_READY {{\"port\":{bound}}}");
Console.Out.Flush();

// Where tools on this computer find the open engines (lapinbim, the MCP bridge; docs/python.md): a file per engine,
// removed when it stops, and host.json naming the latest started (what readers from before 14912 read).
var hostFiles = new EngineHostFiles(userDir, bound, product.Id);
hostFiles.Write();
app.Lifetime.ApplicationStopping.Register(hostFiles.Remove);

// Never outlive the window that started us, nor the disk image we run from
// (an ejected image leaves code that cannot load: a watchdog outside it ends us).
ExitGuard.Arm();
ExitGuard.Watch();
_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(1500);
        if (parentPid <= 0) continue;
        try { if (Process.GetProcessById(parentPid).HasExited) break; }
        catch (ArgumentException) { break; }
    }
    // The window is gone: stop cleanly, but not for longer than a few seconds.
    var stop = app.StopAsync();
    if (await Task.WhenAny(stop, Task.Delay(5000)) != stop) Environment.Exit(0);
});

await app.WaitForShutdownAsync();

return 0;
