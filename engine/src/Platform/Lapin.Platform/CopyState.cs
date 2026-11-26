using System.Text.Json.Nodes;

namespace Lapin.Platform;

/// <summary>
/// State that belongs to this copy of the project and never to its model or its commits: a connector's link to the program it
/// receives from (the Archicad state captured after a receive, docs/plan-archicad.md A2), as the remotes' last-seen refs are this
/// copy's. A host's dispatcher keeps it with the project's history (<c>history/remotes.json</c>, member <c>connectors</c>), so it is
/// saved with the file and never committed nor undone; a session on its own keeps it in memory (<see cref="MemoryCopyState"/>).
/// </summary>
public interface ICopyState
{
    /// <summary>The value kept under <paramref name="key"/> (a copy), or null.</summary>
    JsonObject? Get(string key);

    /// <summary>The value kept under <paramref name="key"/> replaced; null removes it.</summary>
    void Set(string key, JsonObject? value);
}

/// <summary>Copy state in memory: a session without a host (tests, scripts).</summary>
public sealed class MemoryCopyState : ICopyState
{
    readonly Dictionary<string, JsonObject> _held = new(StringComparer.Ordinal);

    public JsonObject? Get(string key) => _held.TryGetValue(key, out var v) ? (JsonObject)v.DeepClone() : null;

    public void Set(string key, JsonObject? value)
    {
        if (value == null) _held.Remove(key);
        else _held[key] = (JsonObject)value.DeepClone();
    }
}
