using System.Diagnostics;
using Lapin.Host;
using Lapin.Platform;
using Lapin.Protocol;

// The local engine. Started by the desktop shell (or by `pnpm dev`):
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
var port = EnginePort.Parse(Arg("--port", "auto"));
var parentPid = int.Parse(Arg("--parent-pid", "0"));
// The user's own data on this computer: the recovery journals and the project templates. The shell passes its app data folder.
var userDir = Arg("--user-dir", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lapin"));
Views.UseProduct(product);
// Point clouds' indexes (docs/point-clouds.md) in the user's folder too, never in a project.
PointCloudLinks.CacheDir ??= Path.Combine(userDir, "PointClouds");
var session = new Session(ModuleCatalog.Create(modules), product);
using var recovery = new Recovery(userDir, session);

var hub = new EngineHub(session, PackageFiles.Create(), recovery, userDir);
// The lapinbim package the Script panel's Python runs (docs/plan-python-scripting.md): named, else beside the modules (the app's resources/python), else the checkout's python/.
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
    // asked by a loopback name, so a page of another site whose name was pointed at 127.0.0.1 does not read it.
    web.MapGet("/health", (HttpContext ctx) => EngineHub.LoopbackHost(ctx.Request.Host.Host) ? Results.Json(hub.Hello()) : Results.StatusCode(403));
    web.Map("/ws", async ctx =>
    {
        if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
        // Only the app's own pages may connect (a browser tab on another site must not drive the engine).
        var origin = ctx.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && !EngineHub.AllowedOrigin(origin)) { ctx.Response.StatusCode = 403; return; }
        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        await hub.Serve(ws, ctx.RequestAborted);
    });
    return web;
}

var started = await EnginePort.StartAsync(Build, port);
var app = started.App;
var bound = started.Port;
hub.Port = bound;
hub.Scripts.Port = bound;
app.Lifetime.ApplicationStopping.Register(hub.Scripts.Dispose);
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
