using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Platform;

namespace Lapin.Host;

/// <summary>
/// Python in Lapin (docs/plan-python-scripting.md, stage 2; decision 489): the user's own CPython (3.11 or newer, found
/// on this computer or chosen in Preferences → Developer extras), run as a child of the engine with
/// <c>python -I -B -u</c> and the kernel of the <c>lapinbim</c> package that ships with the app
/// (<c>lapinbim.kernel</c>). The kernel connects back to this engine over the protocol like any script, so what it does
/// is ordinary commands; each Run is one step held open on the engine (<c>doc.begin</c>/<c>doc.commit</c>, one undo
/// step named after the script; a lost connection rolls it back).
/// <para>
/// The host's methods, never a guest's (<see cref="EngineHub"/> refuses them to the network): <c>script.run {code,
/// name?, console?, python?}</c>, <c>script.stop</c> (an interrupt, then the process killed after
/// <see cref="KillAfter"/>), <c>script.restart {python?}</c>, <c>script.status {python?, refresh?}</c>, and the user's
/// own scripts in <c>&lt;user-dir&gt;/scripts</c>: <c>script.files</c>, <c>script.load {name}</c>, <c>script.save
/// {name, code}</c>, <c>script.delete {name}</c>, <c>script.reveal {name, editor?}</c> (opened in an external editor).
/// What the kernel prints and how each run ends go to the computer's own windows as <c>script.output</c> events.
/// </para>
/// <para>
/// Stage 3: the kernel's token (<see cref="NewKernelToken"/>; <c>script.join</c>, handled by the hub, joins a connection
/// of its Python to its run's step), the window's selection (<c>script.selected {ids}</c>, <c>script.selection</c>) and
/// trust in a project's scripts by the SHA-256 of their code (<c>script.trusted</c>, <c>script.trust</c>, <see cref="TrustFile"/>).
/// </para>
/// <para>
/// Stage 4: a script's tools (<c>script.run {tool, source}</c>: what the run calls, <see cref="ToolOf"/>; the kernel says
/// back the tools a script registers, <c>lapin.ui.tool</c>), its questions to the user (<c>script.ask</c>, answered by a
/// window with <c>script.answer</c>: <see cref="Ask"/>), trust taken back (<c>script.untrust</c>), and Mine listed with
/// each script's first lines and hash (its plug-in header).
/// </para>
/// Nothing of this runs where the edition has no <c>scripting</c> module.
/// </summary>
public sealed class ScriptHost(bool enabled, string? userDir, Func<JsonObject, Task> output) : IDisposable
{
    public const string NotInEdition = "Python scripts are not part of this edition.";
    public const string Running = "A script is running: stop it first.";
    public static readonly Version Minimum = new(3, 11);

    /// <summary>How long a script has to stop after its interrupt before its process is killed.</summary>
    public static TimeSpan KillAfter { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>The engine's port, for the kernel to connect back to (0: it runs without the engine, as the tests' kernel does).</summary>
    public int Port { get; set; }

    /// <summary>The folder that holds the <c>lapinbim</c> package (the app's resources/python, or the repository's python/ in development).</summary>
    public string? PackageDir { get; set; } = FindPackage();

    public bool Enabled => enabled;

    readonly SemaphoreSlim _lock = new(1, 1);
    Process? _proc;
    PythonFound? _python;
    string? _pythonFor;
    string? _why;
    int _nextRun;
    int? _running;
    bool _stopping;
    /// <summary>Why the kernel is being killed (its run's end says so, whichever of the kill and its output's end comes first).</summary>
    volatile string? _killedWhy;

    // ------------------------------------------------------------ the kernel's own connections (stage 3)

    /// <summary>What the Python of the kernel running now shows to be it: given to it alone (its environment), new with each process.</summary>
    volatile string? _token;

    /// <summary>
    /// The connections of the kernel running now, one for the engine's open step (<see cref="Session.Connection"/>): its
    /// own (<c>lapin</c>) and any a script opens itself (<c>lapinbim.Lapin()</c>), each joined by <c>script.join</c>
    /// with the kernel's token. New with each process.
    /// </summary>
    public object KernelKey { get; private set; } = new();

    /// <summary>What a connection is told when its token is not the kernel's.</summary>
    public const string NotKernel = "That is not the Script panel's Python: the connection stays its own.";

    /// <summary>A new kernel's token (and its key): the one before no longer joins anything. Start gives it to the process it starts.</summary>
    public string NewKernelToken()
    {
        KernelKey = new object();
        var token = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        _token = token;
        return token;
    }

    /// <summary>Whether <paramref name="token"/> is the running kernel's (compared in constant time).</summary>
    public bool IsKernel(string? token)
    {
        var t = _token;
        if (t == null || string.IsNullOrEmpty(token) || token.Length != t.Length) return false;
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(t), Encoding.ASCII.GetBytes(token));
    }

    // ------------------------------------------------------------ the window's selection (stage 3)

    string[] _selection = [];

    /// <summary>What the computer's window has selected, as it last said (<c>script.selected {ids}</c>): <c>lapin.selection()</c> reads it.</summary>
    public IReadOnlyList<string> Selection => _selection;

    // ------------------------------------------------------------ the package and Python

    /// <summary>The folder holding <c>lapinbim/kernel.py</c>: beside the modules (an installed app's resources), else up from the engine (a checkout's python/).</summary>
    public static string? FindPackage(string? modulesDir = null)
    {
        static bool Has(string dir) => File.Exists(Path.Combine(dir, "lapinbim", "kernel.py"));
        if (modulesDir != null && Path.GetDirectoryName(Path.GetFullPath(modulesDir).TrimEnd(Path.DirectorySeparatorChar)) is { } res && Has(Path.Combine(res, "python")))
            return Path.Combine(res, "python");
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (Has(Path.Combine(d.FullName, "python"))) return Path.Combine(d.FullName, "python");
        return null;
    }

    /// <summary>A Python found: how to start it (a command and the arguments before the script's), its version, where it is.</summary>
    public sealed record PythonFound(string Command, string[] Args, Version Version, string Executable);

    /// <summary>
    /// The Python to run: <paramref name="preferred"/> when given (a path or a command), else the first of LAPIN_PYTHON,
    /// <c>py -3</c> on Windows, <c>python3</c> and <c>python</c> on the PATH and the usual places (python.org, Homebrew,
    /// pyenv) that is 3.11 or newer. A GUI app on macOS has a short PATH, so the places are looked at too. Null, and why,
    /// when there is none.
    /// </summary>
    public static (PythonFound? Found, string? Why) FindPython(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var p = Probe(preferred.Trim(), []);
            if (p == null) return (null, $"The Python chosen in Preferences ({preferred.Trim()}) does not start.");
            return p.Version >= Minimum ? (p, null) : (null, $"The Python chosen in Preferences is {p.Version}; Lapin needs Python {Minimum} or newer.");
        }
        var tried = new List<(string, string[])>();
        if (Environment.GetEnvironmentVariable("LAPIN_PYTHON") is { Length: > 0 } env) tried.Add((env, []));
        if (OperatingSystem.IsWindows())
        {
            tried.Add(("py", ["-3"]));
            tried.Add(("python", []));
            tried.Add(("python3", []));
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            foreach (var v in new[] { "314", "313", "312", "311" }) tried.Add((Path.Combine(local, "Programs", "Python", $"Python{v}", "python.exe"), []));
        }
        else
        {
            tried.Add(("python3", []));
            tried.Add(("python", []));
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var f in new[] { "/opt/homebrew/bin/python3", "/usr/local/bin/python3", "/Library/Frameworks/Python.framework/Versions/Current/bin/python3",
                         Path.Combine(home, ".pyenv/shims/python3"), Path.Combine(home, ".local/bin/python3"), "/usr/bin/python3" })
                tried.Add((f, []));
        }
        PythonFound? older = null;
        foreach (var (cmd, args) in tried)
        {
            if (Path.IsPathRooted(cmd) && !File.Exists(cmd)) continue;
            if (Probe(cmd, args) is not { } p) continue;
            if (p.Version >= Minimum) return (p, null);
            older ??= p;
        }
        return (null, older != null
            ? $"Python {older.Version} was found ({older.Executable}); Lapin needs Python {Minimum} or newer: install it from python.org, or choose it in Preferences → Developer extras."
            : $"Python was not found on this computer. Install Python {Minimum} or newer from python.org, or choose it in Preferences → Developer extras.");
    }

    /// <summary>Its version and where it is, asked of it (five seconds at most); null when it does not start or answer.</summary>
    static PythonFound? Probe(string cmd, string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(cmd) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            foreach (var a in new[] { "-I", "-c", "import sys; print('%d.%d.%d' % sys.version_info[:3]); print(sys.executable)" }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p == null) return null;
            var text = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(5000)) { try { p.Kill(true); } catch (InvalidOperationException) { } return null; }
            var lines = text.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return p.ExitCode == 0 && lines.Length >= 1 && Version.TryParse(lines[0], out var v) ? new PythonFound(cmd, args, v, lines.Length > 1 ? lines[1] : cmd) : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException) { return null; }
    }

    // ------------------------------------------------------------ the calls

    public async Task<JsonNode?> Handle(string method, JsonObject p)
    {
        try { return await Call(method, p); }
        catch (UnauthorizedAccessException ex) { throw new CommandException(ex.Message); }
    }

    async Task<JsonNode?> Call(string method, JsonObject p)
    {
        if (method == "script.status") return await Status((string?)p["python"], p["refresh"] is JsonValue r && r.TryGetValue<bool>(out var b) && b);
        if (!enabled) throw new CommandException(NotInEdition);
        switch (method)
        {
            case "script.run": return await Run(p);
            case "script.stop": return await Stop();
            case "script.restart": await Kill(null); return await Status((string?)p["python"], refresh: true);
            case "script.files": return Files();
            case "script.load":
            {
                var path = PathOf(p);
                if (!File.Exists(path)) throw new CommandException($"There is no script „{Path.GetFileName(path)}” in your scripts.");
                return new JsonObject { ["name"] = Path.GetFileName(path), ["code"] = await File.ReadAllTextAsync(path), ["modified"] = File.GetLastWriteTimeUtc(path).ToString("O") };
            }
            case "script.save":
            {
                var path = PathOf(p);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, (string?)p["code"] ?? "", new UTF8Encoding(false));
                return new JsonObject { ["name"] = Path.GetFileName(path), ["modified"] = File.GetLastWriteTimeUtc(path).ToString("O") };
            }
            case "script.delete":
            {
                var path = PathOf(p);
                if (File.Exists(path)) File.Delete(path);
                return new JsonObject { ["deleted"] = Path.GetFileName(path) };
            }
            case "script.reveal": return Reveal(PathOf(p), (string?)p["editor"]);
            case "script.selected":
                _selection = (p["ids"] as JsonArray ?? []).Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>().Take(100_000).ToArray();
                return new JsonObject { ["count"] = _selection.Length };
            case "script.selection": return new JsonObject { ["ids"] = new JsonArray(_selection.Select(i => (JsonNode)i).ToArray()) };
            case "script.trusted": return new JsonObject { ["trusted"] = Trusted().Contains(Sha(p)) };
            case "script.trust":
            {
                var sha = Sha(p);
                var set = Trusted();
                if (set.Add(sha)) await WriteTrusted(set);
                return new JsonObject { ["trusted"] = true };
            }
            case "script.untrust":
            {
                var sha = Sha(p);
                var set = Trusted();
                if (set.Remove(sha)) await WriteTrusted(set);
                return new JsonObject { ["trusted"] = false };
            }
            case "script.answer": return Answer(p);
            default: throw new CommandException($"Unknown call „{method}”.");
        }
    }

    async Task<JsonObject> Status(string? preferred, bool refresh)
    {
        await _lock.WaitAsync();
        try
        {
            if (enabled && (refresh || _pythonFor != (preferred ?? "") || (_python == null && _why == null))) Choose(preferred);
            return new JsonObject
            {
                ["enabled"] = enabled,
                ["ok"] = enabled && _python != null && PackageDir != null,
                ["python"] = _python?.Executable,
                ["version"] = _python?.Version.ToString(),
                ["message"] = !enabled ? NotInEdition : PackageDir == null ? "The lapinbim package is missing from this installation." : _why,
                ["alive"] = _proc is { HasExited: false },
                ["running"] = _running != null,
                ["scripts"] = ScriptsDir,
            };
        }
        finally { _lock.Release(); }
    }

    void Choose(string? preferred)
    {
        (_python, _why) = FindPython(preferred);
        _pythonFor = preferred ?? "";
    }

    async Task<JsonObject> Run(JsonObject p)
    {
        var code = (string?)p["code"] ?? throw new CommandException("The script's code is missing.");
        var name = ((string?)p["name"])?.Trim() is { Length: > 0 } n ? n : "Script";
        var console = p["console"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c;
        // Stage 4: a script's tool (what its run calls) and where the script is (the window's key for it, said back with the tools it registers).
        var tool = console ? null : ToolOf(p["tool"] as JsonObject);
        var source = ((string?)p["source"])?.Trim() is { Length: > 0 and <= 300 } s ? s : null;
        int id;
        await _lock.WaitAsync();
        try
        {
            if (_running != null) throw new CommandException(Running);
            if (_proc is not { HasExited: false })
            {
                var preferred = (string?)p["python"];
                if (_python == null || _pythonFor != (preferred ?? "")) Choose(preferred);
                Start();
            }
            id = ++_nextRun;
            _running = id;
            _stopping = false;
            await Send(new JsonObject { ["op"] = "run", ["id"] = id, ["code"] = code, ["name"] = name, ["console"] = console, ["tool"] = tool?.DeepClone(), ["source"] = source });
        }
        finally { _lock.Release(); }
        await output(new JsonObject { ["type"] = "start", ["id"] = id, ["name"] = name, ["console"] = console, ["tool"] = tool, ["source"] = source });
        return new JsonObject { ["run"] = id, ["version"] = _python?.Version.ToString() };
    }

    async Task<JsonObject> Stop()
    {
        int id;
        await _lock.WaitAsync();
        try
        {
            if (_running is not { } r) return new JsonObject { ["stopping"] = false };
            id = r;
            _stopping = true;
            // A question the script waits on is taken away with it (the windows close its dialog).
            CancelKernelAsks();
            await Send(new JsonObject { ["op"] = "interrupt" });
        }
        finally { _lock.Release(); }
        _ = Task.Run(async () =>
        {
            await Task.Delay(KillAfter);
            if (_running == id) await Kill($"The script did not stop within {KillAfter.TotalSeconds:0} s: its Python process was ended, and nothing it did is kept.");
        });
        return new JsonObject { ["stopping"] = true };
    }

    void Start()
    {
        var python = _python ?? throw new CommandException(_why ?? "Python was not found.");
        var package = PackageDir ?? throw new CommandException("The lapinbim package is missing from this installation.");
        var psi = new ProcessStartInfo(python.Command)
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, StandardInputEncoding = new UTF8Encoding(false),
        };
        foreach (var a in python.Args) psi.ArgumentList.Add(a);
        // Isolated (no current folder, no PYTHON* variables), no bytecode written beside the package (an app's signed resources), unbuffered.
        foreach (var a in new[] { "-I", "-B", "-u", "-c", Bootstrap, package }) psi.ArgumentList.Add(a);
        if (Port > 0) psi.Environment["LAPIN_PORT"] = Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        else psi.Environment.Remove("LAPIN_PORT");
        psi.Environment["LAPIN_SCRIPT_KERNEL"] = "1";
        // Its own connections show it with this (script.join): each one the script opens joins its run's step, nobody else's does.
        psi.Environment["LAPIN_SCRIPT_TOKEN"] = NewKernelToken();
        if (userDir != null) psi.Environment["LAPIN_USER_DIR"] = userDir;
        if (ScriptsDir is { } dir) { Directory.CreateDirectory(dir); psi.WorkingDirectory = dir; }
        var proc = Process.Start(psi) ?? throw new CommandException($"Python ({python.Executable}) did not start.");
        _proc = proc;
        _ = Task.Run(() => Pump(proc, proc.StandardOutput, err: false));
        _ = Task.Run(() => Pump(proc, proc.StandardError, err: true));
    }

    /// <summary>The kernel's own start: its package first on the path (PYTHONPATH is ignored in isolated mode), then its loop.</summary>
    const string Bootstrap = "import sys; sys.path.insert(0, sys.argv[1]); from lapinbim.kernel import main; main()";

    async Task Send(JsonObject msg)
    {
        if (_proc is not { HasExited: false } proc) throw new CommandException("The Python process is not running.");
        try
        {
            await proc.StandardInput.WriteLineAsync(msg.ToJsonString());
            await proc.StandardInput.FlushAsync();
        }
        catch (IOException) { throw new CommandException("The Python process is not running."); }
    }

    /// <summary>The kernel's lines (JSON, one per line) to the windows; anything else it writes (Python failing before its loop) as error text.</summary>
    async Task Pump(Process proc, StreamReader reader, bool err)
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                JsonObject? msg = null;
                if (!err && line.StartsWith('{'))
                    try { msg = JsonNode.Parse(line) as JsonObject; } catch (JsonException) { }
                msg ??= new JsonObject { ["type"] = "err", ["text"] = line + "\n" };
                if ((string?)msg["type"] == "done" && msg["id"] is JsonValue v && v.TryGetValue<int>(out var id))
                {
                    if (_running != id) continue;   // a run already ended (killed) speaks no more
                    if (_stopping) msg["interrupted"] = true;
                    _running = null;
                }
                await output(msg);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        if (err) return;
        try { await proc.WaitForExitAsync(); } catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
        await Ended(proc, null);
    }

    /// <summary>The process gone (it ended, or was killed): the run it had ends with it, and the windows are told.</summary>
    async Task Ended(Process proc, string? why)
    {
        int? run;
        lock (this)
        {
            if (!ReferenceEquals(_proc, proc)) return;
            _proc = null;
            _token = null;
            run = _running;
            _running = null;
            why ??= _killedWhy;
            _killedWhy = null;
        }
        CancelKernelAsks();
        if (run is { } id)
            await output(new JsonObject
            {
                ["type"] = "done", ["id"] = id, ["ok"] = false, ["interrupted"] = _stopping, ["killed"] = why != null,
                ["error"] = why ?? $"The Python process ended (exit code {(proc.HasExited ? proc.ExitCode : -1)}); nothing the script did is kept.",
            });
        await output(new JsonObject { ["type"] = "exit", ["code"] = proc.HasExited ? proc.ExitCode : -1 });
    }

    /// <summary>The kernel killed (Restart; a script that would not stop): its connection drops, so its step is rolled back.</summary>
    async Task Kill(string? why)
    {
        var proc = _proc;
        if (proc == null) return;
        why ??= "Python was restarted; nothing the script did is kept.";
        _killedWhy = why;
        try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        try { await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch (TimeoutException) { } catch (InvalidOperationException) { }
        await Ended(proc, why);
    }

    public void Dispose()
    {
        try { if (_proc is { HasExited: false } p) p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
    }

    // ------------------------------------------------------------ trust in a project's scripts (stage 3)

    /// <summary>
    /// The project scripts trusted on this computer, by the SHA-256 of their code (docs/plan-python-scripting.md §7):
    /// <c>&lt;user-dir&gt;/scripting-trust.json</c>. A project's script never runs as it opens; its first run, or its first
    /// after its code changed, asks (the interface's Run once / Trust / Cancel), and Trust keeps its hash here.
    /// </summary>
    public string? TrustFile => userDir == null ? null : Path.Combine(userDir, "scripting-trust.json");

    static readonly Regex Hex64 = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    static string Sha(JsonObject p) =>
        ((string?)p["sha256"])?.Trim().ToLowerInvariant() is { } s && Hex64.IsMatch(s) ? s : throw new CommandException("A script's SHA-256 is 64 hexadecimal digits.");

    /// <summary>A script's code as its hash (UTF-8, lowercase hex): what trust is kept by.</summary>
    public static string HashOf(string code) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    HashSet<string> Trusted()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (TrustFile is not { } f || !File.Exists(f)) return set;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(f))?["trusted"] is JsonArray a)
                foreach (var n in a) if (n is JsonValue v && v.TryGetValue<string>(out var s) && Hex64.IsMatch(s)) set.Add(s);
        }
        catch (JsonException) { }
        return set;
    }

    async Task WriteTrusted(HashSet<string> set)
    {
        var f = TrustFile ?? throw new CommandException("Trust is kept by the desktop app.");
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        var json = new JsonObject { ["trusted"] = new JsonArray(set.Order(StringComparer.Ordinal).Select(s => (JsonNode)s).ToArray()) };
        await File.WriteAllTextAsync(f + ".saving", json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Move(f + ".saving", f, overwrite: true);
    }

    // ------------------------------------------------------------ a script's tools and questions (stage 4)

    /// <summary>The most of a script's start <c>script.files</c> gives (its header, # lapin: …, is in its first lines).</summary>
    public const int HeadLength = 4096;

    /// <summary>A script's first <see cref="HeadLength"/> characters, cut at the end of a line.</summary>
    public static string Head(string text)
    {
        if (text.Length <= HeadLength) return text;
        var cut = text.LastIndexOf('\n', HeadLength - 1);
        return cut > 0 ? text[..(cut + 1)] : text[..HeadLength];
    }

    static readonly Regex Identifier = new(@"^[A-Za-z_][A-Za-z0-9_]{0,99}$", RegexOptions.CultureInvariant);

    /// <summary>
    /// What a tool's run calls (<c>script.run {tool}</c>; the kernel's side is python/lapinbim/kernel.py): its name (what
    /// <c>lapin.ui.tool</c> registered it as, or its header's <c>tool=</c>), the function of the script it calls, if any
    /// (a name, never an expression), and whether the script runs as <c>__main__</c> (a header tool with no <c>run=</c>).
    /// Null when the run is not a tool's.
    /// </summary>
    public static JsonObject? ToolOf(JsonObject? t)
    {
        if (t == null) return null;
        var name = ((string?)t["name"])?.Trim() is { Length: > 0 and <= 120 } n ? n : throw new CommandException("A script's tool has a name.");
        var call = (string?)t["call"] is { Length: > 0 } c ? Identifier.IsMatch(c) ? c : throw new CommandException("A tool calls a function of its script by its name.") : null;
        var main = t["main"] is JsonValue m && m.TryGetValue<bool>(out var b) && b;
        return new JsonObject { ["name"] = name, ["call"] = call, ["main"] = main };
    }

    /// <summary>A question a script asks (<c>lapin.ui.ask</c>): whose connection asked it, whether that is the kernel's (a Stop takes it away), and its answer to come.</summary>
    sealed record Question(object Owner, bool Kernel, TaskCompletionSource<JsonObject> Answer);

    readonly System.Collections.Concurrent.ConcurrentDictionary<string, Question> _asks = new();

    /// <summary>The questions waiting for a window's answer, by their key.</summary>
    public IReadOnlyCollection<string> Asking => [.. _asks.Keys];

    public static readonly HashSet<string> FieldKinds = ["number", "text", "choice", "bool", "element", "storey", "type"];
    public const int MaxFields = 64;

    /// <summary>
    /// <c>script.ask {title, fields, text?, ok?}</c> (<c>lapin.ui.ask</c>): the question checked (1 to <see
    /// cref="MaxFields"/> fields, each a key once and a kind of <see cref="FieldKinds"/>; nothing a window would run), sent
    /// to this computer's windows as a <c>script.output</c> event <c>{type: "ask", ask, title, fields…}</c>; the task ends
    /// with the first window's answer (<c>script.answer</c>): <c>{values}</c>, or <c>{cancelled: true}</c> when it is
    /// cancelled, when the script is stopped (<see cref="CancelKernelAsks"/>) or its connection goes (<see cref="Forget"/>).
    /// The hub answers the asking connection when it ends, holding nothing up meanwhile.
    /// </summary>
    public Task<JsonObject> Ask(JsonObject p, object owner, bool kernel)
    {
        if (!enabled) throw new CommandException(NotInEdition);
        var title = ((string?)p["title"])?.Trim() is { Length: > 0 } t ? t[..Math.Min(t.Length, 200)] : "Script";
        var fields = p["fields"] as JsonArray ?? throw new CommandException("A question has its fields.");
        if (fields.Count == 0) throw new CommandException("A question has its fields.");
        if (fields.Count > MaxFields) throw new CommandException($"A question has at most {MaxFields} fields.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in fields)
        {
            if (f is not JsonObject o || ((string?)o["key"])?.Trim() is not { Length: > 0 and <= 64 } key) throw new CommandException("Each field of a question has a key.");
            if (!keys.Add(key)) throw new CommandException($"The field „{key}” is in the question twice.");
            if ((string?)o["kind"] is not { } kind || !FieldKinds.Contains(kind)) throw new CommandException($"The field „{key}” is of no kind a question has.");
        }
        var ask = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
        var q = new Question(owner, kernel, new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously));
        _asks[ask] = q;
        var e = new JsonObject { ["type"] = "ask", ["ask"] = ask, ["title"] = title, ["fields"] = fields.DeepClone() };
        if (((string?)p["text"])?.Trim() is { Length: > 0 } text) e["text"] = text[..Math.Min(text.Length, 2000)];
        if (((string?)p["ok"])?.Trim() is { Length: > 0 } ok) e["ok"] = ok[..Math.Min(ok.Length, 40)];
        _ = output(e);
        return q.Answer.Task;
    }

    /// <summary><c>script.answer {ask, values? | cancel}</c>: the first window's answer ends the question; the others close theirs.</summary>
    JsonObject Answer(JsonObject p)
    {
        var key = (string?)p["ask"] ?? "";
        if (!_asks.TryRemove(key, out var q)) return new JsonObject { ["answered"] = false };
        var cancel = p["cancel"] is JsonValue c && c.TryGetValue<bool>(out var b) && b;
        q.Answer.TrySetResult(cancel || p["values"] is not JsonObject values
            ? new JsonObject { ["cancelled"] = true }
            : new JsonObject { ["values"] = values.DeepClone() });
        _ = output(new JsonObject { ["type"] = "asked", ["ask"] = key });
        return new JsonObject { ["answered"] = true };
    }

    void Cancel(Func<Question, bool> which, bool stopped)
    {
        foreach (var (key, q) in _asks.ToArray())
            if (which(q) && _asks.TryRemove(key, out _))
            {
                var answer = new JsonObject { ["cancelled"] = true };
                if (stopped) answer["stopped"] = true;
                q.Answer.TrySetResult(answer);
                _ = output(new JsonObject { ["type"] = "asked", ["ask"] = key });
            }
    }

    /// <summary>
    /// The Script panel's Python stopped or gone: its questions answered <c>{cancelled, stopped}</c> (lapinbim raises
    /// KeyboardInterrupt on it, so a Stop never lets the script go on as if the user had only cancelled), their dialogs closed.
    /// </summary>
    public void CancelKernelAsks() => Cancel(q => q.Kernel, stopped: true);

    /// <summary>A connection gone: the questions it asked are taken away.</summary>
    public void Forget(object owner) => Cancel(q => ReferenceEquals(q.Owner, owner), stopped: false);

    // ------------------------------------------------------------ the user's own scripts

    /// <summary>Where the user's scripts are ("Mine" in the Script panel): plain .py files in the user's folder.</summary>
    public string? ScriptsDir => userDir == null ? null : Path.Combine(userDir, "scripts");

    static readonly Regex Name = new(@"^[^/\\:*?""<>|\x00-\x1f.][^/\\:*?""<>|\x00-\x1f]{0,118}\.py$", RegexOptions.CultureInvariant);

    /// <summary>A script of the user's folder by its name (a file name ending .py, no folder), never a path elsewhere.</summary>
    string PathOf(JsonObject p)
    {
        var dir = ScriptsDir ?? throw new CommandException("Your scripts are kept by the desktop app.");
        var name = ((string?)p["name"])?.Trim() ?? throw new CommandException("Which script?");
        if (!Name.IsMatch(name)) throw new CommandException($"„{name}” is not a script's name: letters, digits and spaces, ending .py.");
        return Path.Combine(dir, name);
    }

    JsonObject Files()
    {
        var list = new JsonArray();
        if (ScriptsDir is { } dir && Directory.Exists(dir))
            foreach (var f in Directory.GetFiles(dir, "*.py").Order(StringComparer.OrdinalIgnoreCase))
            {
                var i = new FileInfo(f);
                if (!Name.IsMatch(i.Name)) continue;
                var o = new JsonObject { ["name"] = i.Name, ["modified"] = i.LastWriteTimeUtc.ToString("O"), ["bytes"] = i.Length };
                // Stage 4: its first lines (the plug-in header, # lapin: …, says its tool) and the hash of its code.
                if (i.Length <= 4 << 20)
                    try
                    {
                        var text = File.ReadAllText(f);
                        o["head"] = Head(text);
                        o["sha256"] = HashOf(text);
                    }
                    catch (IOException) { }
                list.Add(o);
            }
        return new JsonObject { ["dir"] = ScriptsDir, ["files"] = list };
    }

    /// <summary>
    /// A script opened in an external editor: the one named (a command: <c>code</c>, <c>subl</c>…), else the system's
    /// text editor (TextEdit's <c>open -t</c> on macOS, Notepad on Windows, xdg-open on Linux), never the system's own
    /// action for .py files, which may run it.
    /// </summary>
    static JsonObject Reveal(string path, string? editor)
    {
        if (!File.Exists(path)) throw new CommandException($"There is no script „{Path.GetFileName(path)}” in your scripts: save it first.");
        ProcessStartInfo psi;
        if (!string.IsNullOrWhiteSpace(editor)) { psi = new ProcessStartInfo(editor.Trim()) { UseShellExecute = false }; psi.ArgumentList.Add(path); }
        else if (OperatingSystem.IsMacOS()) { psi = new ProcessStartInfo("open") { UseShellExecute = false }; psi.ArgumentList.Add("-t"); psi.ArgumentList.Add(path); }
        else if (OperatingSystem.IsWindows()) { psi = new ProcessStartInfo("notepad.exe") { UseShellExecute = false }; psi.ArgumentList.Add(path); }
        else { psi = new ProcessStartInfo("xdg-open") { UseShellExecute = false }; psi.ArgumentList.Add(path); }
        try { Process.Start(psi)?.Dispose(); }
        catch (System.ComponentModel.Win32Exception) { throw new CommandException($"The editor „{psi.FileName}” did not start."); }
        return new JsonObject { ["path"] = path };
    }
}
