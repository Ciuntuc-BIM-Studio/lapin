using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Platform;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The Script panel's Python (docs/plan-python-scripting.md, stage 2): the user's own CPython found, the kernel of the
/// lapinbim package started, spoken to and stopped (an interrupt, then killed after <see cref="ScriptHost.KillAfter"/>),
/// restarted, and the user's own scripts kept in their folder. With no Python 3.11 or newer on this computer the tests
/// that need one pass saying so (the run's output names why). These kernels run without an engine (port 0): the steps
/// they would open are python/tests/test_kernel.py's and ScriptStepTests'.
/// </summary>
public sealed class ScriptHostTests(ITestOutputHelper log) : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-script-host-").FullName;
    readonly List<JsonObject> _events = [];
    ScriptHost? _host;

    public void Dispose()
    {
        _host?.Dispose();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    ScriptHost Host(bool enabled = true) => _host = new ScriptHost(enabled, _dir, o => { lock (_events) _events.Add(o); return Task.CompletedTask; })
    {
        Port = 0,
        PackageDir = ScriptHost.FindPackage(),
    };

    /// <summary>The Python the tests run, or null (and why, in the test's output).</summary>
    bool HasPython()
    {
        var (found, why) = ScriptHost.FindPython(null);
        if (found == null) log.WriteLine($"Skipped: {why}");
        return found != null;
    }

    async Task<JsonObject> Done(int run, TimeSpan? within = null)
    {
        var until = DateTime.UtcNow + (within ?? TimeSpan.FromSeconds(30));
        while (DateTime.UtcNow < until)
        {
            lock (_events)
                if (_events.FirstOrDefault(e => (string?)e["type"] == "done" && (int?)e["id"] == run) is { } d) return d;
            await Task.Delay(20);
        }
        throw new TimeoutException($"Run {run} did not end.");
    }

    string Printed(int run)
    {
        lock (_events) return string.Concat(_events.Where(e => (string?)e["type"] is "out" or "err" && (int?)e["id"] == run).Select(e => (string?)e["text"]));
    }

    static JsonObject Args(params (string Key, JsonNode? Value)[] kv)
    {
        var o = new JsonObject();
        foreach (var (k, v) in kv) o[k] = v;
        return o;
    }

    async Task<int> Run(ScriptHost h, string code, string name = "test.py") =>
        (int)(await h.Handle("script.run", Args(("code", code), ("name", name))))!["run"]!;

    [Fact]
    public void The_package_is_found_beside_the_engine_in_a_checkout()
    {
        var dir = ScriptHost.FindPackage();
        Assert.NotNull(dir);
        Assert.True(File.Exists(Path.Combine(dir, "lapinbim", "kernel.py")));
        // An installed app's: resources/python beside resources/modules.
        var res = Directory.CreateDirectory(Path.Combine(_dir, "resources"));
        Directory.CreateDirectory(Path.Combine(res.FullName, "modules"));
        Directory.CreateDirectory(Path.Combine(res.FullName, "python", "lapinbim"));
        File.WriteAllText(Path.Combine(res.FullName, "python", "lapinbim", "kernel.py"), "");
        Assert.Equal(Path.Combine(res.FullName, "python"), ScriptHost.FindPackage(Path.Combine(res.FullName, "modules")));
    }

    [Fact]
    public void A_python_chosen_that_does_not_start_is_said_so()
    {
        var (found, why) = ScriptHost.FindPython(Path.Combine(_dir, "no-python-here"));
        Assert.Null(found);
        Assert.Contains("does not start", why);
        if (!HasPython()) return;
        var (auto, _) = ScriptHost.FindPython(null);
        Assert.True(auto!.Version >= ScriptHost.Minimum);
        // The one found, chosen by its path, is taken as it is.
        var (chosen, none) = ScriptHost.FindPython(auto.Executable);
        Assert.Null(none);
        Assert.Equal(auto.Version, chosen!.Version);
    }

    [Fact]
    public async Task A_run_prints_through_events_and_the_console_keeps_its_names_until_a_restart()
    {
        if (!HasPython()) return;
        var h = Host();
        var status = (JsonObject)(await h.Handle("script.status", []))!;
        Assert.True((bool)status["ok"]!);
        Assert.NotNull((string?)status["version"]);
        var a = await Run(h, "x = 41\nprint('hello', x + 1)");
        var done = await Done(a);
        Assert.True((bool)done["ok"]!);
        Assert.Equal("hello 42\n", Printed(a));
        lock (_events) Assert.Contains(_events, e => (string?)e["type"] == "start" && (int?)e["id"] == a);
        var b = await Run(h, "print(x)");
        await Done(b);
        Assert.Equal("41\n", Printed(b));

        await h.Handle("script.restart", []);
        var c = await Run(h, "print(x)");
        var failed = await Done(c);
        Assert.False((bool)failed["ok"]!);
        Assert.Contains("NameError", (string?)failed["error"]);
        Assert.Equal(1, (int?)failed["line"]);
    }

    [Fact]
    public async Task Only_one_run_at_a_time_and_stop_interrupts_it()
    {
        if (!HasPython()) return;
        var h = Host();
        var a = await Run(h, "import time\nprint('waiting')\ntime.sleep(30)");
        var e = await Assert.ThrowsAsync<CommandException>(() => h.Handle("script.run", Args(("code", "print(1)"))));
        Assert.Equal(ScriptHost.Running, e.Message);
        Assert.True((bool)((JsonObject)(await h.Handle("script.status", []))!)["running"]!);
        for (var i = 0; i < 1500 && Printed(a).Length == 0; i++) await Task.Delay(20);
        await h.Handle("script.stop", []);
        var done = await Done(a, TimeSpan.FromSeconds(10));
        Assert.True((bool)done["interrupted"]!);
        Assert.NotEqual(true, (bool?)done["killed"]);
        // The same kernel goes on.
        var b = await Run(h, "print('after')");
        await Done(b);
        Assert.Equal("after\n", Printed(b));
        Assert.False((bool)((JsonObject)(await h.Handle("script.stop", []))!)["stopping"]!);
    }

    [Fact]
    public async Task A_script_that_will_not_stop_is_killed_after_two_seconds()
    {
        if (!HasPython()) return;
        var h = Host();
        var a = await Run(h, "import time\nprint('stubborn')\nwhile True:\n    try:\n        time.sleep(60)\n    except KeyboardInterrupt:\n        pass\n");
        for (var i = 0; i < 1500 && Printed(a).Length == 0; i++) await Task.Delay(20);
        var t = Stopwatch.StartNew();
        await h.Handle("script.stop", []);
        var done = await Done(a, TimeSpan.FromSeconds(15));
        Assert.True(t.Elapsed >= ScriptHost.KillAfter - TimeSpan.FromMilliseconds(100), $"killed after {t.Elapsed}");
        Assert.True((bool)done["killed"]!);
        Assert.False((bool)done["ok"]!);
        lock (_events) Assert.Contains(_events, e => (string?)e["type"] == "exit");
        // The next run starts a new kernel.
        var b = await Run(h, "print('new')");
        await Done(b);
        Assert.Equal("new\n", Printed(b));
    }

    [Fact]
    public async Task My_scripts_are_files_in_the_users_folder_by_name_only()
    {
        var h = Host();
        await h.Handle("script.save", Args(("name", "Pereți perimetrali.py"), ("code", "print('ă')\n")));
        Assert.True(File.Exists(Path.Combine(_dir, "scripts", "Pereți perimetrali.py")));
        var files = (JsonObject)(await h.Handle("script.files", []))!;
        Assert.Equal(["Pereți perimetrali.py"], files["files"]!.AsArray().Select(f => (string?)f!["name"]));
        var loaded = (JsonObject)(await h.Handle("script.load", Args(("name", "Pereți perimetrali.py"))))!;
        Assert.Equal("print('ă')\n", (string?)loaded["code"]);
        foreach (var bad in new[] { "../escape.py", "a/b.py", ".hidden.py", "notes.txt", "C:\\x.py", "" })
            await Assert.ThrowsAsync<CommandException>(() => h.Handle("script.save", Args(("name", bad), ("code", "x"))));
        await Assert.ThrowsAsync<CommandException>(() => h.Handle("script.load", Args(("name", "missing.py"))));
        await h.Handle("script.delete", Args(("name", "Pereți perimetrali.py")));
        Assert.Empty(((JsonObject)(await h.Handle("script.files", []))!)["files"]!.AsArray());
    }

    [Fact]
    public async Task An_edition_without_the_scripting_module_runs_nothing()
    {
        var h = Host(enabled: false);
        var e = await Assert.ThrowsAsync<CommandException>(() => h.Handle("script.run", Args(("code", "print(1)"))));
        Assert.Equal(ScriptHost.NotInEdition, e.Message);
        await Assert.ThrowsAsync<CommandException>(() => h.Handle("script.save", Args(("name", "a.py"), ("code", "x"))));
        var status = (JsonObject)(await h.Handle("script.status", []))!;
        Assert.False((bool)status["enabled"]!);
        Assert.Equal(ScriptHost.NotInEdition, (string?)status["message"]);
    }
}
