using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A module's upgrade of a project an older Lapin wrote: run on what the file
/// holds as it opens, before anything is drawn (no undo step). An upgrade
/// that has nothing to change gives the lists back as they came.
/// </summary>
public interface IProjectUpgrade
{
    /// <summary>The entities as this version keeps them, and the relations (those of entities gone left out).</summary>
    (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations);
}
