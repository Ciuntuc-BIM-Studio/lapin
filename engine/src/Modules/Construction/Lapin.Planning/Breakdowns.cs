using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// A product a task's rule names (docs/plan-4d5d.md, "Produs × Loc"): a code of the product breakdown (PBS), the node and every
/// item under it; <see cref="System"/> the classification it is a code of (its id or its name), none for the project's PBS.
/// </summary>
public sealed record ProductRef(string? System, string Code)
{
    /// <summary>As stored: the code alone for the project's PBS, else {system, code}.</summary>
    public JsonNode ToJson() => System is { Length: > 0 } s ? new JsonObject { ["system"] = s, ["code"] = Code } : JsonValue.Create(Code)!;

    public static List<ProductRef> ReadAll(JsonNode? n)
    {
        var o = new List<ProductRef>();
        foreach (var x in n as JsonArray ?? [])
        {
            var r = x switch
            {
                JsonValue v when v.TryGetValue<string>(out var s) && s.Trim() is { Length: > 0 } c => new ProductRef(null, c),
                JsonObject jo when ((string?)jo["code"])?.Trim() is { Length: > 0 } c => new ProductRef(((string?)jo["system"])?.Trim() is { Length: > 0 } sy ? sy : null, c),
                _ => null,
            };
            if (r != null && !o.Contains(r)) o.Add(r);
        }
        return o;
    }

    public override string ToString() => Code;
}

/// <summary>
/// The breakdowns a task is tied to the model by (the consultant's ABS / PBS / GBS): the activity is the task itself (its
/// name and kind); the product breakdown (PBS) is one of the project's classifications (<see cref="Classifications"/>: the
/// user's own code list, read from a table or typed in a tree, Lapin's small starter marked indicative) whose codes label the
/// types and elements — a type's code is its elements', an element's own wins; the geographic breakdown (GBS) is the spatial
/// structure: the building, its storeys and its rooms. A rule naming a product and locations takes the elements labelled
/// with the product (or an item under it) that stand in any of the locations: "Install doors" × "Floor 1" the doors of floor 1.
/// </summary>
public static class Breakdowns
{
    /// <summary>The project's attribute naming the classification that is its PBS (its id).</summary>
    public const string PbsKey = "pbs";
    public const string StarterName = "PBS";

    /// <summary>
    /// Lapin's starter PBS: a few products by trade, its own codes and words, from no published classification; marked
    /// indicative, to be replaced or completed by the user's.
    /// </summary>
    public static readonly IReadOnlyList<Classifications.Item> Starter =
    [
        new("10", "Substructure", null),
        new("10.10", "Foundations", "10"),
        new("10.20", "Ground slabs", "10"),
        new("20", "Structure", null),
        new("20.10", "Columns", "20"),
        new("20.20", "Beams", "20"),
        new("20.30", "Floor slabs", "20"),
        new("20.40", "Structural walls", "20"),
        new("20.50", "Stairs", "20"),
        new("30", "Envelope", null),
        new("30.10", "External walls", "30"),
        new("30.20", "Windows", "30"),
        new("30.30", "Roofs", "30"),
        new("40", "Interior", null),
        new("40.10", "Partition walls", "40"),
        new("40.20", "Doors", "40"),
        new("40.30", "Finishes", "40"),
        new("50", "Services", null),
        new("60", "External works", null),
    ];

    /// <summary>
    /// The classification that is the project's PBS: the one its <see cref="PbsKey"/> names, else one named „PBS”, else the
    /// project's only classification; null when none.
    /// </summary>
    public static Entity? Pbs(IModelView m)
    {
        if (Spatial.Project(m)?.Str(PbsKey) is { } id && m.Get(id) is { Class: Classifications.Class } chosen) return chosen;
        if (Classifications.System(m, StarterName) is { } named) return named;
        var all = Classifications.Systems(m).Take(2).ToList();
        return all.Count == 1 ? all[0] : null;
    }

    /// <summary>The system a product names: its own (by id or name), else the project's PBS.</summary>
    public static Entity? SystemOf(IModelView m, ProductRef p) => p.System is { } s ? Classifications.System(m, s) : Pbs(m);

    /// <summary>An element's code in a system: its own, else its type's (a reference's file type too); null when neither has one.</summary>
    public static string? CodeOf(IModelView m, Entity e, string systemId)
    {
        foreach (var r in Classifications.RefsOf(e)) if (r.System == systemId) return r.Code;
        if (Types.Of(m, e) is { } type) foreach (var r in Classifications.RefsOf(type)) if (r.System == systemId) return r.Code;
        return null;
    }

    /// <summary>What matching reads of the model once per filter context: each system's parents, the locations' rooms, the elements' plan points.</summary>
    public sealed class Cache(ElementFilter.Context ctx)
    {
        readonly Dictionary<string, Dictionary<string, string?>> parents = [];
        readonly Dictionary<string, Entity?> systems = [];
        readonly Dictionary<string, Entity?> locations = [];
        readonly Dictionary<string, IReadOnlyList<Vec2>?> outlines = [];
        readonly Dictionary<string, Vec2?> points = [];
        IModelView M => ctx.Query.Model;

        public Entity? System(ProductRef p) => systems.TryGetValue(p.System ?? "", out var s) ? s : systems[p.System ?? ""] = SystemOf(M, p);

        Dictionary<string, string?> ParentsOf(Entity system)
        {
            if (parents.TryGetValue(system.Id, out var d)) return d;
            d = [];
            foreach (var i in Classifications.Items(system)) d.TryAdd(i.Code, i.Parent);
            return parents[system.Id] = d;
        }

        /// <summary>Whether a code is the wanted one or under it in its system's tree.</summary>
        public bool Under(Entity system, string code, string wanted)
        {
            var up = ParentsOf(system);
            var c = code;
            for (var i = 0; i < 64 && c != null; i++)
            {
                if (c == wanted) return true;
                c = up.GetValueOrDefault(c);
            }
            return false;
        }

        /// <summary>Whether an element is labelled with any of the products (one of its codes the product or under it).</summary>
        public bool TakesProduct(IReadOnlyList<ProductRef> products, Entity e)
        {
            foreach (var p in products)
                if (System(p) is { } s && CodeOf(M, e, s.Id) is { } code && Under(s, code, p.Code)) return true;
            return false;
        }

        /// <summary>A location by its id, else by its name among the buildings, storeys and rooms.</summary>
        public Entity? Location(string key)
        {
            if (locations.TryGetValue(key, out var l)) return l;
            l = M.Get(key) is { Class: IfcClass.Building or IfcClass.BuildingStorey or IfcClass.Space or IfcClass.Site } e ? e
                : M.OfClass(IfcClass.BuildingStorey).Concat(M.OfClass(IfcClass.Space)).Concat(M.OfClass(IfcClass.Building))
                    .FirstOrDefault(x => string.Equals(x.Name, key, StringComparison.OrdinalIgnoreCase)
                        || x.Class == IfcClass.Space && string.Equals(x.Str("number"), key, StringComparison.OrdinalIgnoreCase));
            return locations[key] = l;
        }

        /// <summary>A room's outline in plan, as its geometry draws it; null when it has none.</summary>
        IReadOnlyList<Vec2>? Outline(Entity room)
        {
            if (outlines.TryGetValue(room.Id, out var o)) return o;
            o = ctx.Query.Geometry(room.Id)?.Plan?.FirstOrDefault(p => p.Kind == "room") is { Points.Count: >= 3 } item ? item.Points : null;
            return outlines[room.Id] = o;
        }

        /// <summary>The middle of an element in plan: the middle of what its geometry built; null when it built nothing.</summary>
        Vec2? Point(Entity e)
        {
            if (points.TryGetValue(e.Id, out var p)) return p;
            p = null;
            if (ctx.Query.Geometry(e.Id) is { Parts.Count: > 0 } g)
            {
                double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
                foreach (var part in g.Parts)
                    if (Measure.Bounds(part.Solid) is { } b) { x0 = Math.Min(x0, b.Min.X); y0 = Math.Min(y0, b.Min.Y); x1 = Math.Max(x1, b.Max.X); y1 = Math.Max(y1, b.Max.Y); }
                if (x0 <= x1) p = new Vec2((x0 + x1) / 2, (y0 + y1) / 2);
            }
            return points[e.Id] = p;
        }

        /// <summary>How far off a room's outline an element still stands in it: a door in the wall round it, a wall on its edge.</summary>
        public const double RoomEdge = 0.3;

        /// <summary>
        /// Whether an element stands in any of the locations: the building its storey is in (the site: everything on it), the
        /// storey (a door on its wall's), a room on its storey whose outline holds the middle of the element in plan or lies
        /// within <see cref="RoomEdge"/> of it (a door between two rooms is in both).
        /// </summary>
        public bool InLocations(IReadOnlyList<string> keys, Entity e)
        {
            var storey = Spatial.StoreyOf(M, e, followHost: true);
            foreach (var key in keys)
            {
                switch (Location(key))
                {
                    case { Class: IfcClass.Site }: if (storey != null) return true; break;
                    case { Class: IfcClass.Building } b:
                        if (storey != null && M.RelatingOf(storey.Id, IfcClass.RelAggregates)?.Id == b.Id) return true;
                        break;
                    case { Class: IfcClass.BuildingStorey } s: if (storey?.Id == s.Id) return true; break;
                    case { Class: IfcClass.Space } room:
                        if (e.Class == IfcClass.Space || storey == null || Spatial.StoreyOf(M, room.Id)?.Id != storey.Id) break;
                        if (Outline(room) is { } outline && Point(e) is { } pt && Polygon2.Contains(outline, pt, RoomEdge)) return true;
                        break;
                }
            }
            return false;
        }
    }

    static readonly ConditionalWeakTable<ElementFilter.Context, Cache> Caches = [];

    public static Cache Of(ElementFilter.Context ctx) => Caches.GetValue(ctx, c => new Cache(c));

    // ------------------------------------------------------------ counts

    /// <summary>
    /// The PBS's items with how many of the candidates each labels (an item counts its own and those under it), in the tree's
    /// order (each parent before its children), and how many candidates have no code of it.
    /// </summary>
    public static (JsonArray Items, int Unlabelled) Counted(IModelView m, Entity system, IEnumerable<ElementFacts> candidates)
    {
        var items = Classifications.Items(system);
        var parent = new Dictionary<string, string?>();
        foreach (var i in items) parent.TryAdd(i.Code, i.Parent);
        var counts = new Dictionary<string, int>();
        var none = 0;
        foreach (var f in candidates)
        {
            if (CodeOf(m, f.Element, system.Id) is not { } code) { none++; continue; }
            var c = code;
            var seen = new HashSet<string>();
            for (var k = 0; k < 64 && c != null && seen.Add(c); k++) { counts[c] = counts.GetValueOrDefault(c) + 1; c = parent.GetValueOrDefault(c); }
        }
        var children = items.GroupBy(i => i.Parent is { } p && parent.ContainsKey(p) ? p : "").ToDictionary(g => g.Key, g => g.ToList());
        var o = new JsonArray();
        void Walk(string key, int depth)
        {
            if (depth > 32 || !children.TryGetValue(key, out var list)) return;
            foreach (var i in list)
            {
                o.Add((JsonNode)new JsonObject { ["code"] = i.Code, ["title"] = i.Title, ["parent"] = i.Parent, ["depth"] = depth, ["count"] = counts.GetValueOrDefault(i.Code) });
                Walk(i.Code, depth + 1);
            }
        }
        Walk("", 0);
        return (o, none);
    }

    /// <summary>The locations a rule can name, in the spatial structure's order: the building, each storey and the rooms on it (rooms with their storey).</summary>
    public static JsonArray Locations(IModelView m, IEnumerable<ElementFacts> candidates)
    {
        var byStorey = candidates.GroupBy(f => f.Storey ?? "").ToDictionary(g => g.Key, g => g.Count());
        var o = new JsonArray();
        foreach (var b in m.OfClass(IfcClass.Building))
            o.Add((JsonNode)new JsonObject { ["id"] = b.Id, ["name"] = b.Name, ["level"] = "building", ["count"] = byStorey.Where(kv => kv.Key.Length > 0 && m.RelatingOf(kv.Key, IfcClass.RelAggregates)?.Id == b.Id).Sum(kv => kv.Value) });
        var rooms = m.OfClass(IfcClass.Space).GroupBy(r => Spatial.StoreyOf(m, r.Id)?.Id ?? "").ToDictionary(g => g.Key, g => g.ToList());
        foreach (var s in Spatial.Storeys(m))
        {
            o.Add((JsonNode)new JsonObject { ["id"] = s.Id, ["name"] = s.Name, ["level"] = "storey", ["count"] = byStorey.GetValueOrDefault(s.Id) });
            foreach (var r in (rooms.GetValueOrDefault(s.Id) ?? []).OrderBy(r => r.Str("number") ?? r.Name, StringComparer.OrdinalIgnoreCase))
                o.Add((JsonNode)new JsonObject { ["id"] = r.Id, ["name"] = r.Str("number") is { Length: > 0 } n ? $"{n} {r.Name}" : r.Name, ["level"] = "room", ["storey"] = s.Id });
        }
        return o;
    }

    /// <summary>A product as words: its code and its title in its system („40.20 Doors”).</summary>
    public static string Label(IModelView m, ProductRef p) =>
        SystemOf(m, p) is { } s && Classifications.Items(s).FirstOrDefault(i => i.Code == p.Code) is { Title.Length: > 0 } item ? $"{p.Code} {item.Title}" : p.Code;
}
