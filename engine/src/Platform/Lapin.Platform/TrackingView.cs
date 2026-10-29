using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The model as a geometry producer sees it, noting everything it reads: the
/// entities it asked for (their attributes), the entities whose relationships
/// it walked (<see cref="RelationsKey"/>: a different thing — a storey's
/// elevation is not its list of walls) and the classes it listed. An
/// element's geometry is rebuilt exactly when an edit touches one of these,
/// so modules never declare their dependencies.
/// </summary>
sealed class TrackingView(IModelView inner) : IModelView
{
    public HashSet<string> Read { get; } = [];

    public static string ClassKey(string ifcClass) => "class:" + ifcClass;
    public static string RelationsKey(string id) => "rel:" + id;

    public Entity? Get(string id) { Read.Add(id); return inner.Get(id); }
    public Relation? GetRelation(string id) { Read.Add(id); return inner.GetRelation(id); }
    public IEnumerable<Entity> Entities { get { Read.Add("class:*"); return inner.Entities; } }
    public IEnumerable<Relation> Relations { get { Read.Add("class:*"); return inner.Relations; } }
    public IEnumerable<Entity> OfClass(string @class) { Read.Add(ClassKey(@class)); return inner.OfClass(@class); }
    /// <summary>A class's entities under a narrower key than the class's: what reads them says itself what it depends on.</summary>
    public IEnumerable<Entity> OfClassUnder(string @class, string key) { Read.Add(key); return inner.OfClass(@class); }
    /// <summary>The key touched when an element of a class gets, changes or loses its own cut priority.</summary>
    public static string CutKey(string ifcClass) => "cut:" + ifcClass;
    public IEnumerable<Relation> RelationsOf(string id, string? relationClass = null) { Read.Add(RelationsKey(id)); return inner.RelationsOf(id, relationClass); }
    public IEnumerable<Relation> RelatedBy(string id, string? relationClass = null) { Read.Add(RelationsKey(id)); return inner.RelatedBy(id, relationClass); }
    public int Order(string id) => inner.Order(id);
    public long Revision => inner.Revision;
}
