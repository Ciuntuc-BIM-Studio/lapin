using System.Text.Json.Nodes;
using Lapin.Package;
using Lapin.Package.Remote;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>
/// The open project's history (docs/spec-lapin-remote.md §6), held with the other tools' entries of its file
/// (<see cref="PackageForeign.History"/>): read with the file, written back with every save, changed by the history and remote
/// calls (<see cref="RemoteCommands"/>), which work on a copy and keep it once it is written.
/// </summary>
public sealed partial class ProjectFiles
{
    /// <summary>The open project's history: the file's, or an empty one (a project never committed). Never changed in place: <see cref="PackageHistory.Clone"/> first.</summary>
    public PackageHistory History(Session session) => Foreign(session).History;

    /// <summary>
    /// The open project's copy state (Lapin.Platform's <see cref="Lapin.Platform.ICopyState"/>: a connector's link) as the history's
    /// <c>connectors</c>: read from the history, written by keeping a changed one (saved with the file, never committed).
    /// </summary>
    public Lapin.Platform.ICopyState CopyStateOf(Session session) => new HistoryCopyState(this, session);

    sealed class HistoryCopyState(ProjectFiles files, Session session) : Lapin.Platform.ICopyState
    {
        public JsonObject? Get(string key) => files.History(session).Connectors.TryGetValue(key, out var v) ? (JsonObject)v.DeepClone() : null;

        public void Set(string key, JsonObject? value)
        {
            var h = files.History(session).Clone();
            if (value == null) { if (!h.Connectors.Remove(key)) return; }
            else h.Connectors[key] = (JsonObject)value.DeepClone();
            files.KeepHistory(session, h);
        }
    }

    /// <summary>A history kept for the open project, in memory until the next save (a fetch's objects, a remote added).</summary>
    public void KeepHistory(Session session, PackageHistory history)
    {
        var foreign = Foreign(session).WithHistory(history);
        if (session.Path is { } path) KeepForeign(path, foreign);
        else _page = (_page?.Versions ?? PackageVersions.None, foreign);
    }

    /// <summary>
    /// A history kept and the project's file written with it and the project as <paramref name="build"/> describes it (a commit: the
    /// commit's blobs are the live parts written now; a pull); as bytes for the browser. A project without a file is saved first.
    /// A write that fails leaves the history as it was.
    /// </summary>
    public (JsonObject Result, Diff? Diff) WriteHistory(Session session, PackageHistory history, PackageBuild build, bool asBytes, JsonObject result)
    {
        if (session.Path == null && !asBytes) throw new CommandException("Save the project first: its history lives in its file.");
        var before = History(session);
        KeepHistory(session, history);
        try { return Commit(session, build, Versions(session), asBytes, result); }
        catch
        {
            KeepHistory(session, before);
            throw;
        }
    }
}
