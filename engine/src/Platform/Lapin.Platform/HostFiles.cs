namespace Lapin.Platform;

/// <summary>
/// The host's own disk, guarded: whatever reads or writes a file (or a folder) by a path a call gave asks here
/// first. A call from a guest of a shared session (docs/plan-multiplayer.md) is refused, whatever let it through
/// (the protocol's <c>AllowedRemote</c> is the first line, this the second).
/// </summary>
public static class HostFiles
{
    static readonly AsyncLocal<bool> _guest = new();

    /// <summary>Whether the call being handled is a guest's: the dispatcher sets it round each call.</summary>
    public static bool GuestCall { get => _guest.Value; set => _guest.Value = value; }

    public const string Refused = "A guest cannot reach files on the host's computer.";

    /// <summary><paramref name="path"/> as given, unless the call is a guest's.</summary>
    public static string Check(string path) => GuestCall ? throw new CommandException(Refused) : path;
}
