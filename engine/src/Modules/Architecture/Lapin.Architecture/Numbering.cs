using Lapin.Model;

namespace Lapin.Architecture;

/// <summary>The number a new element's name takes ("D3", "Roof 4").</summary>
static class Numbering
{
    /// <summary>One past the highest number ending these entities' names: never their count, which after a delete would repeat a name.</summary>
    public static int Next(IEnumerable<Entity> entities) => 1 + entities.Select(e => Trailing(e.Name)).DefaultIfEmpty(0).Max();

    static int Trailing(string name)
    {
        var i = name.Length;
        while (i > 0 && char.IsAsciiDigit(name[i - 1])) i--;
        return i < name.Length && int.TryParse(name.AsSpan(i), out var n) ? n : 0;
    }
}
