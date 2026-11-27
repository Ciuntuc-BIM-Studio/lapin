using System.Collections;
using System.Collections.Concurrent;

namespace Lapin.Drawing.Views;

/// <summary>
/// A set of classes that modules add to as they register. Such a set is the whole process's, and engines may be made
/// at once while others draw, so it is added to and read from any thread.
/// </summary>
public sealed class ClassSet : IEnumerable<string>
{
    readonly ConcurrentDictionary<string, byte> _classes = new(StringComparer.Ordinal);

    /// <summary>Adds <paramref name="cls"/>; false when it is there already.</summary>
    public bool Add(string cls) => _classes.TryAdd(cls, 0);

    public bool Contains(string cls) => _classes.ContainsKey(cls);

    public IEnumerator<string> GetEnumerator() => _classes.Select(x => x.Key).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
