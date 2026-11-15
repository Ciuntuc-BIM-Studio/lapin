using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// An element another was made from and kept hidden (forum topic 101: the solids a layered wall was made of, kept with
/// <c>supersededBy</c> naming the wall): no view draws it, no export writes it, while the element made of it is there.
/// Deleted, that element gives the solids back; Show the original solids clears the mark.
/// </summary>
public static class Superseded
{
    public const string Key = "supersededBy";

    /// <summary>Whether the element is kept hidden behind another that still exists.</summary>
    public static bool Hidden(IModelView m, Entity e) => e.Str(Key) is { Length: > 0 } id && m.Get(id) != null;
}
