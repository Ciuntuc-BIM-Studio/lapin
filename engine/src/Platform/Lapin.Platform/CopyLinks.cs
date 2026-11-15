using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Links held in attributes, from one element to another (a finish's room, a wall's roofs, a downpipe's roof, a space's
/// stair, a stair's opening, a bar's host, a connection's members), kept right through copies and deletions (audit
/// 15.11.2026, §3a): copied together, a copy names the copies of what its original named, as Copy to storeys has it
/// (<see cref="StoreyCopy.Remap"/>); copied alone, it names what its original named. A deletion tells
/// <see cref="IDeletionFollowers"/> first.
/// </summary>
public static class CopyLinks
{
    /// <summary>
    /// Every copy (and what its copy hosts: openings, doors and windows, matched with its original's) names the copies of
    /// the elements its original named; <paramref name="except"/>: attributes left naming the originals. The ids mapped.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Remap(ICommandContext ctx, IReadOnlyDictionary<string, string> copies, IReadOnlySet<string>? except = null)
    {
        var m = ctx.Model;
        var names = new Dictionary<string, string>();
        foreach (var (o, c) in copies) if (c != o && m.Get(c) != null) names[o] = c;
        if (names.Count == 0) return names;
        foreach (var (o, c) in names.ToList()) StoreyCopy.MapHosted(m, o, c, names);
        foreach (var id in names.Values.Distinct().ToList())
            if (m.Get(id) is { } e && StoreyCopy.Remap(e.Attrs, names, except) is { } attrs) ctx.Replace(e.With(attrs: attrs));
        return names;
    }
}

/// <summary>
/// What follows elements deleted without being deleted with them by the core: an element naming one in an attribute (a
/// room's finish, a bar's host). Told once, before element.delete removes anything (every element still there), in the
/// same undo step: it takes away what goes with them, or lets go of them.
/// </summary>
public interface IDeletionFollowers
{
    /// <summary><paramref name="deleting"/>: everything element.delete removes (the groups' members among them).</summary>
    void Deleting(ICommandContext ctx, IReadOnlySet<string> deleting);
}
