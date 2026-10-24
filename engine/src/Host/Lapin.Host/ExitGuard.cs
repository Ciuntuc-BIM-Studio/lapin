using System.Diagnostics;

namespace Lapin.Host;

/// <summary>
/// The engine must not outlive the disk image it runs from. When a DMG is
/// ejected by force while the engine runs from it, its code can no longer be
/// paged in: the runtime spins at full CPU and no managed thread runs again,
/// so nothing inside the engine can notice. A watchdog outside the image, a
/// /bin/sh loop, checks every second that the executable is still there and
/// kills the engine (SIGKILL, which still works) when it is not; it ends by
/// itself when the engine does.
/// </summary>
public static class ExitGuard
{
    const string Loop = "while kill -0 \"$1\" 2>/dev/null; do [ -e \"$2\" ] || { kill -9 \"$1\"; exit 0; }; sleep 1; done";

    static string? _image;

    /// <summary>Remember the executable the engine runs from.</summary>
    public static void Arm(string? image = null) => _image = image ?? Environment.ProcessPath;

    /// <summary>The executable the engine runs from no longer exists.</summary>
    public static bool ImageGone() => _image is { } p && !File.Exists(p);

    /// <summary>Start the watchdog (macOS and Linux; on Windows a running image cannot be removed).</summary>
    public static void Watch()
    {
        if (OperatingSystem.IsWindows() || _image is null || !File.Exists("/bin/sh")) return;
        try
        {
            var psi = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, RedirectStandardInput = false };
            foreach (var a in new[] { "-c", Loop, "lapin-guard", Environment.ProcessId.ToString(), _image }) psi.ArgumentList.Add(a);
            Process.Start(psi)?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception) { }
        catch (InvalidOperationException) { }
    }
}
