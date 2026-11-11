using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform.Documents;

namespace Lapin.Platform;

/// <summary>
/// Project templates (issues #9 and #20, decision 470): a project's set-up without its model, to start new projects from.
/// <para>
/// <see cref="Strip"/> keeps the project, site, building and storeys, every type (walls, floors, doors, windows, stairs, ceilings,
/// structure…), every resource that is set-up (materials, profiles, title blocks, images, price lists, norms, calendars, type
/// mappings, the cut order) and every view, sheet and schedule, with what they use (a type's layer set, a layer set's materials).
/// The project's own attributes go too: its units and drafting standards (text, dimension and tag styles, layers), view styles,
/// graphics and filters, its location, the profile standards it offers, and of its information what belongs to the office (the
/// designer, the people who sign, the user's own parameters), not what belongs to the job (number, client, site, permit).
/// </para>
/// <para>
/// It leaves out the model: every element and annotation (walls, slabs, openings, rooms, grids, 2D drafting, insets), groups,
/// and the resources that are the project's own content (<see cref="ClassDescriptor.Template"/> false: issues, remarks, links,
/// components, documents, clash reports, IFC records, shared bodies, estimates, work schedules and tasks), unless something it
/// keeps uses one. Relationships are kept between what is kept, trimmed of what is not.
/// </para>
/// <see cref="Renew"/> gives a new project made from a template its own GlobalIds, every reference to them followed.
/// </summary>
public static class ProjectTemplates
{
    /// <summary>The kinds a template starts from (<see cref="ClassDescriptor.Kind"/>).</summary>
    static readonly HashSet<string> KeptKinds = ["type", "resource", "spatial", "view"];

    /// <summary>The kinds a template never keeps, even when something kept names one.</summary>
    static readonly HashSet<string> ModelKinds = ["element", "annotation", "group"];

    static readonly HashSet<string> SpatialRoots = [IfcClass.Project, IfcClass.Site, IfcClass.Building, IfcClass.BuildingStorey];

    /// <summary>The project's information that belongs to the job, not to the office: a new project starts without it.</summary>
    public static readonly HashSet<string> JobInfo =
        ["number", "phase", "phaseName", "contract", "client", "clientAddress", "address", "cadastral", "landBook", "permit", "permitDate"];

    /// <summary>The longest text looked through for ids (longer ones are data: an image, a mesh).</summary>
    const int MaxScanned = 4096;

    /// <summary>The project's set-up without its model, as entities and relationships in the model's order.</summary>
    public static (List<Entity> Entities, List<Relation> Relations) Strip(IModelView m, ModuleCatalog catalog)
    {
        var placed = new HashSet<string>();
        foreach (var r in m.Relations)
            if (r.Class == IfcClass.RelContainedInSpatialStructure) placed.UnionWith(r.Related);

        bool IsModel(Entity e)
        {
            if (SpatialRoots.Contains(e.Class)) return false;
            if (placed.Contains(e.Id)) return true;
            var d = catalog.Class(e.Class);
            return d != null ? ModelKinds.Contains(d.Kind) : catalog.Geometry(e.Class) != null;
        }
        bool IsRoot(Entity e)
        {
            if (SpatialRoots.Contains(e.Class)) return true;
            var d = catalog.Class(e.Class);
            return d != null && d.Template != false && KeptKinds.Contains(d.Kind);
        }

        var keep = new HashSet<string>();
        var queue = new Queue<Entity>();
        void Keep(Entity e) { if (keep.Add(e.Id)) queue.Enqueue(e); }
        foreach (var e in m.Entities) if (IsRoot(e)) Keep(e);
        // What the kept entities use: an id named in their attributes (a layer set's materials, a title block's image), and the
        // relationships relating them to something (a type's layer set), never an element.
        while (queue.Count > 0)
        {
            var e = queue.Dequeue();
            foreach (var id in NamedIds(e.Attrs, m))
                if (m.Get(id) is { } used && !IsModel(used)) Keep(used);
            foreach (var r in m.RelatedBy(e.Id))
                if (m.Get(r.Relating) is { } relating && !IsModel(relating)) Keep(relating);
        }

        var entities = new List<Entity>();
        foreach (var e in m.Entities)
        {
            if (!keep.Contains(e.Id)) continue;
            entities.Add(e.Class == IfcClass.Project ? OfficeProject(e) : e);
        }
        var relations = new List<Relation>();
        foreach (var r in m.Relations)
        {
            if (!keep.Contains(r.Relating)) continue;
            var related = r.Related.Where(keep.Contains).ToList();
            if (related.Count == 0) continue;
            relations.Add(related.Count == r.Related.Count ? r : r with { Related = related });
        }
        return (entities, relations);
    }

    /// <summary>The project as a template keeps it: its default name, its information without the job's.</summary>
    static Entity OfficeProject(Entity project)
    {
        var attrs = project.CloneAttrs();
        if (attrs[ProjectInfo.Key] is JsonObject info)
            foreach (var key in JobInfo) info.Remove(key);
        return project.With(name: "Project", attrs: attrs);
    }

    /// <summary>The ids of entities of <paramref name="m"/> an attribute names: a text that is one, or holds one ("plan:&lt;storey&gt;").</summary>
    static IEnumerable<string> NamedIds(JsonNode? node, IModelView m)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var (k, v) in o)
                {
                    if (m.Get(k) != null) yield return k;
                    foreach (var id in NamedIds(v, m)) yield return id;
                }
                break;
            case JsonArray a:
                foreach (var v in a) foreach (var id in NamedIds(v, m)) yield return id;
                break;
            case JsonValue v when v.TryGetValue<string>(out var s):
                if (m.Get(s) != null) { yield return s; break; }
                if (s.Length <= GlobalIdLength || s.Length > MaxScanned) break;
                for (var i = 0; i + GlobalIdLength <= s.Length; i++)
                {
                    var part = s.Substring(i, GlobalIdLength);
                    if (m.Get(part) != null) yield return part;
                }
                break;
        }
    }

    const int GlobalIdLength = 22;

    /// <summary>
    /// The same entities and relationships under new GlobalIds (a new project from a template is a project of its own): every id
    /// that is a GlobalId gets a new one, and every text that names one (a value, a key, an id inside a text) names the new one.
    /// Ids that are not GlobalIds stay as they are.
    /// </summary>
    public static (List<Entity> Entities, List<Relation> Relations) Renew(IEnumerable<Entity> entities, IEnumerable<Relation> relations, Func<string>? newId = null)
    {
        newId ??= GlobalId.New;
        var es = entities.ToList();
        var rs = relations.ToList();
        var map = new Dictionary<string, string>();
        foreach (var e in es) if (GlobalId.IsValid(e.Id)) map[e.Id] = newId();
        foreach (var r in rs) if (GlobalId.IsValid(r.Id)) map[r.Id] = newId();
        var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();

        string To(string s)
        {
            if (map.TryGetValue(s, out var to)) return to;
            if (s.Length <= GlobalIdLength || s.Length > MaxScanned) return s;
            System.Text.StringBuilder? b = null;
            var last = 0;
            for (var i = 0; i + GlobalIdLength <= s.Length;)
            {
                if (lookup.TryGetValue(s.AsSpan(i, GlobalIdLength), out var hit))
                {
                    b ??= new System.Text.StringBuilder(s.Length);
                    b.Append(s, last, i - last).Append(hit);
                    i += GlobalIdLength;
                    last = i;
                }
                else i++;
            }
            if (b == null) return s;
            return b.Append(s, last, s.Length - last).ToString();
        }
        JsonNode? Walk(JsonNode? n) => n switch
        {
            JsonObject o => new JsonObject(o.Select(kv => new KeyValuePair<string, JsonNode?>(To(kv.Key), Walk(kv.Value)))),
            JsonArray a => new JsonArray(a.Select(Walk).ToArray()),
            JsonValue v when v.TryGetValue<string>(out var s) => JsonValue.Create(To(s)),
            _ => n?.DeepClone(),
        };

        var outEntities = es.Select(e => new Entity(To(e.Id), e.Class, e.Name, (JsonObject)Walk(e.Attrs)!, e.PredefinedType)).ToList();
        var outRelations = rs.Select(r => new Relation(To(r.Id), r.Class, To(r.Relating), r.Related.Select(To).ToList(), r.Attrs == null ? null : (JsonObject)Walk(r.Attrs)!)).ToList();
        return (outEntities, outRelations);
    }
}
