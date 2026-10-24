using System.Diagnostics;
using Lapin.Host;
using Lapin.Platform;

// The local engine. Started by the desktop shell (or by `pnpm dev`):
//   Lapin.Host --product <product.json> --modules-dir <dir> [--user-dir <dir>] [--port 0] [--parent-pid <pid>]
// It listens on 127.0.0.1 only and prints one line the shell waits for:
//   LAPIN_HOST_READY {"port":12345}

string Arg(string name, string? fallback = null)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback ?? throw new ArgumentException($"Missing {name}");
}

var product = ModuleLoader.ReadProduct(Arg("--product"));
var modules = ModuleLoader.Load(Arg("--modules-dir"), product.Modules);
var port = int.Parse(Arg("--port", "0"));
var parentPid = int.Parse(Arg("--parent-pid", "0"));
// The user's own data on this computer: the recovery journals. The shell passes its app data folder.
var userDir = Arg("--user-dir", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lapin"));
var session = new Session(ModuleCatalog.Create(modules), product);
using var recovery = new Recovery(userDir, session);

var builder = WebApplication.CreateSlimBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseKestrel(o => o.Listen(System.Net.IPAddress.Loopback, port));
var app = builder.Build();
app.UseWebSockets();

var hub = new EngineHub(session, PackageFiles.Create(), recovery);
app.MapGet("/health", () => Results.Json(new { ok = true, product = product.Id, modules = product.Modules }));
app.Map("/ws", async ctx =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    // Only the app's own pages may connect (a browser tab on another site must not drive the engine).
    var origin = ctx.Request.Headers.Origin.ToString();
    if (origin.Length > 0 && !EngineHub.AllowedOrigin(origin)) { ctx.Response.StatusCode = 403; return; }
    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    await hub.Serve(ws, ctx.RequestAborted);
});

await app.StartAsync();
var bound = new Uri(app.Urls.First()).Port;
Console.WriteLine($"LAPIN_HOST_READY {{\"port\":{bound}}}");
Console.Out.Flush();

// Where tools on this machine find the open engine (the MCP bridge): its port, while it runs.
var hostFile = Path.Combine(userDir, "host.json");
try
{
    Directory.CreateDirectory(userDir);
    File.WriteAllText(hostFile, $"{{\"port\":{bound},\"pid\":{Environment.ProcessId},\"product\":\"{product.Id}\"}}");
    app.Lifetime.ApplicationStopping.Register(() => { try { File.Delete(hostFile); } catch (IOException) { } });
}
catch (IOException) { }
catch (UnauthorizedAccessException) { }

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
