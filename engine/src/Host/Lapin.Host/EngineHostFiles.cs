using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Host;

/// <summary>
/// The files that tell programs on this computer where the open engines listen (docs/python.md, How a program finds
/// Lapin): <c>&lt;user-dir&gt;/hosts/host-&lt;pid&gt;.json</c> for each engine, removed when it stops, and
/// <c>&lt;user-dir&gt;/host.json</c> naming the latest started, as engines before 14912 wrote it (removed only when it
/// still names this engine). Each holds <c>{"port", "pid", "product", "version", "started"}</c>; a file whose process is
/// gone (killed) is stale, and readers skip it.
/// </summary>
public sealed class EngineHostFiles(string userDir, int port, string product)
{
    public string Folder => Path.Combine(userDir, "hosts");
    public string Own => Path.Combine(Folder, $"host-{Environment.ProcessId}.json");
    public string Latest => Path.Combine(userDir, "host.json");

    public string Text() => new JsonObject
    {
        ["port"] = port, ["pid"] = Environment.ProcessId, ["product"] = product, ["version"] = Session.AppVersion,
        ["started"] = DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
    }.ToJsonString();

    public void Write()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var text = Text();
            File.WriteAllText(Own, text);
            File.WriteAllText(Latest, text);
            // Files of engines that are gone (killed: they could not remove theirs).
            foreach (var f in Directory.EnumerateFiles(Folder, "host-*.json"))
                if (int.TryParse(Path.GetFileNameWithoutExtension(f)["host-".Length..], out var pid) && pid != Environment.ProcessId && !Alive(pid))
                    TryDelete(f);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Remove()
    {
        TryDelete(Own);
        try
        {
            if (File.Exists(Latest) && JsonNode.Parse(File.ReadAllText(Latest)) is JsonObject h && (int?)h["pid"] == Environment.ProcessId) TryDelete(Latest);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or FormatException or InvalidOperationException) { }
    }

    static bool Alive(int pid)
    {
        try { using var p = System.Diagnostics.Process.GetProcessById(pid); return !p.HasExited; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return true; }
    }

    static void TryDelete(string f)
    {
        try { File.Delete(f); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
