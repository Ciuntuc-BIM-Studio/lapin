using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Type packs (docs/plan-extensions.md, §9.3): chosen types of any kind (wall, floor, door, window, column, beam, stair…)
/// taken out of a project into a <c>.lapintypes</c> file and brought into another, as an extension of the kind
/// <c>types</c> brings them or Manage → Type packs… does.
///
/// <para>The file is JSON: <c>{format: "lapin-type-pack", version: 1, name?, types: [item], resources: [item]}</c>, an item
/// <c>{ref, class, name, predefinedType?, attrs, material?}</c>. <c>ref</c> is the entity's id in the project it came from;
/// any string in an item's attrs equal to a ref points at that item, so a layer set names its materials, a column type its
/// profile copy. A type's <c>material</c> is the ref of what IfcRelAssociatesMaterial gives it (its layer set, a material).
/// The resources are what the types need: their layer sets, the materials they name (by id, or by name under a key that
/// says material: a door's frame), the profile copies and anything else their attrs point at.</para>
///
/// <para>Brought in (one undo step): a material is the project's of that name when it has one, else made as the pack has it;
/// a resource identical to one of the project's (class, name, attrs) is that one, else made; a type identical to one of the
/// project's (class, name, attrs, layers) is reused, one of the same name that differs is made beside it as "Name (2)", any
/// other made. A type's own layer set comes and goes with it. Ids are always new: nothing in the project is changed.
/// The user may choose otherwise for what the project has by name (types.readPack says what differs; <see cref="LibraryMerge"/>):
/// a material updated in place from the pack or the pack's made beside it as „Name (2)”; a type updated in place (its id
/// kept, its elements following) or skipped.</para>
/// </summary>
public static class TypePacks
{
    public const string Format = "lapin-type-pack";
    public const string Extension = "lapintypes";

    /// <summary>What never comes with a type: the spatial structure (and, checked apart, elements placed in it).</summary>
    static readonly HashSet<string> Spatial = [IfcClass.Project, IfcClass.Site, IfcClass.Building, IfcClass.BuildingStorey, IfcClass.Space];

    /// <summary>Attributes a project writes for itself (a profile copy's date added, an import's marker): not compared, not carried.</summary>
    static readonly string[] Volatile = ["added", "imported"];

    // ------------------------------------------------------------ export

    /// <summary>The types <paramref name="ids"/> as a pack, with everything they need.</summary>
    public static JsonObject Export(IQueryContext q, IReadOnlyList<string> ids, string? name)
    {
        var m = q.Model;
        if (ids.Count == 0) throw new CommandException("Choose the types to export.");
        var types = new JsonArray();
        var resources = new JsonArray();
        var taken = new HashSet<string>(ids);
        var queue = new Queue<string>();
        void Need(string id) { if (taken.Add(id)) queue.Enqueue(id); }
        foreach (var id in ids.Distinct())
        {
            var t = m.Get(id) ?? throw new CommandException("There is no such type.");
            if (!IsType(q.Describe(t.Class), t)) throw new CommandException($"„{t.Name}” is not a type: only types go into a type pack.");
            var item = Item(t);
            if (m.RelatingOf(t.Id, IfcClass.RelAssociatesMaterial) is { } assoc && Carried(m, assoc)) { item["material"] = assoc.Id; Need(assoc.Id); }
            foreach (var r in References(m, t.Attrs)) Need(r);
            types.Add((JsonNode)item);
        }
        while (queue.Count > 0)
        {
            var e = m.Get(queue.Dequeue())!;
            resources.Add((JsonNode)Item(e));
            foreach (var r in References(m, e.Attrs)) Need(r);
        }
        var o = new JsonObject { ["format"] = Format, ["version"] = 1 };
        if (name?.Trim() is { Length: > 0 } n) o["name"] = n;
        o["types"] = types;
        o["resources"] = resources;
        return o;
    }

    static JsonArray Strings(IEnumerable<string> list) => new(list.Select(x => (JsonNode)x).ToArray());

    static bool IsType(ClassDescriptor? d, Entity e) => d is { Kind: "type" } || d is null && e.Class.EndsWith("Type", StringComparison.Ordinal);

    static JsonObject Item(Entity e)
    {
        var attrs = e.CloneAttrs();
        foreach (var k in Volatile) attrs.Remove(k);
        var o = new JsonObject { ["ref"] = e.Id, ["class"] = e.Class, ["name"] = e.Name };
        if (e.PredefinedType != null) o["predefinedType"] = e.PredefinedType;
        o["attrs"] = attrs;
        return o;
    }

    /// <summary>Whether an entity can come along with a type: not the spatial structure, not an element placed in it.</summary>
    static bool Carried(IModelView m, Entity e) =>
        !Spatial.Contains(e.Class) && m.RelatingOf(e.Id, IfcClass.RelContainedInSpatialStructure) is null && m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is null;

    /// <summary>The entities a JSON tree points at: by id anywhere, a material by name under a key that says material.</summary>
    static IEnumerable<string> References(IModelView m, JsonNode? node)
    {
        var found = new List<string>();
        Walk(node, false, s =>
        {
            if (m.Get(s) is { } e && Carried(m, e)) found.Add(e.Id);
        }, s =>
        {
            if (Materials.Find(m, s) is { } mat) found.Add(mat.Id);
        });
        return found.Distinct();
    }

    /// <summary>Every string of a tree; those under a key naming a material also to <paramref name="named"/>.</summary>
    static void Walk(JsonNode? node, bool material, Action<string> any, Action<string> named, int depth = 0)
    {
        if (depth > 32) return;
        switch (node)
        {
            case JsonObject o:
                foreach (var (k, v) in o) Walk(v, material || k.Contains("material", StringComparison.OrdinalIgnoreCase), any, named, depth + 1);
                break;
            case JsonArray a:
                foreach (var v in a) Walk(v, material, any, named, depth + 1);
                break;
            case JsonValue v when v.TryGetValue<string>(out var s) && s.Length > 0:
                any(s);
                if (material) named(s);
                break;
        }
    }

    // ------------------------------------------------------------ the file read

    public sealed record PackItem(string Ref, string Class, string Name, string? PredefinedType, JsonObject Attrs, string? Material);
    public sealed record Pack(string? Name, IReadOnlyList<PackItem> Types, IReadOnlyList<PackItem> Resources);

    /// <summary>A pack read and checked; a bad one refused whole, with a sentence.</summary>
    public static Pack Read(string text, Func<string, ClassDescriptor?> describe)
    {
        JsonNode? doc;
        try { doc = JsonNode.Parse(text.TrimStart('﻿')); } catch (JsonException) { throw new CommandException("The file is not a Lapin type pack: it is not JSON."); }
        if (doc is not JsonObject o || (string?)o["format"] != Format) throw new CommandException("The file is not a Lapin type pack (.lapintypes).");
        if (Json.Number(o["version"]) is not { } version || version > 1) throw new CommandException("This type pack was written by a newer Lapin: update Lapin to read it.");
        var refs = new HashSet<string>();
        PackItem ItemOf(JsonNode? n, bool type)
        {
            if (n is not JsonObject i || (string?)i["ref"] is not { Length: > 0 } r || (string?)i["class"] is not { Length: > 0 } cls
                || ((string?)i["name"])?.Trim() is not { Length: > 0 } name || i["attrs"] is not JsonObject attrs)
                throw new CommandException("Every item of a type pack has a ref, a class, a name and its attrs.");
            if (!refs.Add(r)) throw new CommandException($"The ref „{r}” is in the pack twice.");
            var d = describe(cls);
            if (type && d is not { Kind: "type" }) throw new CommandException($"„{name}” is a {cls}: not a kind of type this edition of Lapin has.");
            if (!type && (Spatial.Contains(cls) || cls.StartsWith("IfcRel", StringComparison.Ordinal) || d is { Kind: "element" }))
                throw new CommandException($"„{name}” is a {cls}: it cannot come with types.");
            return new PackItem(r, cls, name, (string?)i["predefinedType"], (JsonObject)attrs.DeepClone(), type ? (string?)i["material"] : null);
        }
        var types = (o["types"] as JsonArray ?? []).Select(n => ItemOf(n, true)).ToList();
        if (types.Count == 0) throw new CommandException("The type pack has no type.");
        var resources = (o["resources"] as JsonArray ?? []).Select(n => ItemOf(n, false)).ToList();
        foreach (var t in types)
            if (t.Material != null && !resources.Any(x => x.Ref == t.Material)) throw new CommandException($"The material of „{t.Name}” is not in the pack.");
        return new Pack(((string?)o["name"])?.Trim() is { Length: > 0 } pn ? pn : null, types, resources);
    }

    // ------------------------------------------------------------ what differs

    /// <summary>A material or a type of the pack the project has under the same name, and what differs (<see cref="LibraryMerge"/>).</summary>
    public sealed record Conflict(string Key, string Class, string Name, IReadOnlyList<string> Differs);

    /// <summary>
    /// Before the import: each material of the pack the project has by name with another definition, and each type of the
    /// pack the project has by class and name that is not the same, with what differs (ids compared by what they name).
    /// </summary>
    public static (List<Conflict> Materials, List<Conflict> Types) Conflicts(IModelView m, Pack pack)
    {
        var byRef = pack.Types.Concat(pack.Resources).ToDictionary(i => i.Ref);
        var materials = new List<Conflict>();
        foreach (var r in pack.Resources.Where(x => x.Class == IfcClass.Material))
            if (Materials.Find(m, r.Name) is { } found && LibraryMerge.Differs(Comparable(found.Attrs), Comparable(r.Attrs)) is { Count: > 0 } d
                && !materials.Any(x => string.Equals(x.Name, r.Name, StringComparison.OrdinalIgnoreCase)))
                materials.Add(new Conflict(r.Name, r.Class, r.Name, d));
        var types = new List<Conflict>();
        foreach (var t in pack.Types)
        {
            var same = m.OfClass(t.Class).Where(e => string.Equals(e.Name, t.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (same.Count == 0) continue;
            var theirs = (JsonObject)Named(Comparable(t.Attrs), s => byRef.TryGetValue(s, out var x) ? x.Name : null)!;
            List<string>? least = null;
            foreach (var e in same)
            {
                var ours = (JsonObject)Named(Comparable(e.Attrs), s => s != e.Id && m.Get(s) is { } x ? x.Name : null)!;
                var d = LibraryMerge.Differs(ours, theirs);
                var assoc = m.RelatingOf(e.Id, IfcClass.RelAssociatesMaterial);
                var mine = t.Material is { } mr ? byRef[mr] : null;
                var materialDiffers = mine is null ? assoc != null
                    : assoc is null || assoc.Class != mine.Class
                    || (mine.Class == IfcClass.Material ? !string.Equals(assoc.Name, mine.Name, StringComparison.OrdinalIgnoreCase)
                        : !JsonNode.DeepEquals(Named(Comparable(assoc.Attrs), s => m.Get(s)?.Name), Named(Comparable(mine.Attrs), s => byRef.TryGetValue(s, out var x) ? x.Name : null)));
                if (materialDiffers) d.Add(mine?.Class == IfcClass.Material || assoc?.Class == IfcClass.Material ? "material" : "layers");
                if (least is null || d.Count < least.Count) least = d;
            }
            if (least is { Count: > 0 }) types.Add(new Conflict(t.Ref, t.Class, t.Name, least));
        }
        return (materials, types);
    }

    /// <summary>A tree with every string a <paramref name="nameOf"/> knows written as what it names (an id as „@Brick”), for comparing two projects' entities.</summary>
    static JsonNode? Named(JsonNode? n, Func<string, string?> nameOf) => n switch
    {
        JsonObject o => new JsonObject(o.Select(kv => KeyValuePair.Create(kv.Key, Named(kv.Value, nameOf)))),
        JsonArray a => new JsonArray(a.Select(v => Named(v, nameOf)).ToArray()),
        JsonValue v when v.TryGetValue<string>(out var s) && s.Length > 0 && nameOf(s) is { } name => JsonValue.Create("@" + name),
        _ => n?.DeepClone(),
    };

    // ------------------------------------------------------------ the import

    public sealed record Outcome(string Ref, string Class, string Name, string Id, string Status);

    /// <summary>
    /// The user's choices for what the project already has (<see cref="LibraryMerge"/>): a material by name (keep, the default;
    /// update; both), a type by its ref or name (both, the default: an identical one reused, one that differs made beside
    /// it; update; skip).
    /// </summary>
    public sealed record Choices(IReadOnlyDictionary<string, string> Materials, IReadOnlyDictionary<string, string> Types)
    {
        public static readonly Choices None = new(new Dictionary<string, string>(), new Dictionary<string, string>());
        public string Material(string name) => Materials.TryGetValue(name, out var c) ? c : LibraryMerge.Keep;
        public string Type(PackItem t) => Types.TryGetValue(t.Ref, out var c) || Types.TryGetValue(t.Name, out c) ? c : LibraryMerge.Both;
    }

    /// <summary>The pack's chosen types (none chosen: all) brought into the project; what became of each, and how many materials were made.</summary>
    public static (List<Outcome> Types, int MaterialsMade) Import(ICommandContext ctx, Pack pack, IReadOnlyCollection<string>? chosen)
    {
        var (types, tally) = Import(ctx, pack, chosen, Choices.None);
        return (types, tally.Made);
    }

    /// <summary>The pack's chosen types brought in as <paramref name="choices"/> say for what the project has by name; one undo step.</summary>
    public static (List<Outcome> Types, LibraryMerge.MaterialTally Materials) Import(ICommandContext ctx, Pack pack, IReadOnlyCollection<string>? chosen, Choices choices)
    {
        var types = chosen is { Count: > 0 } ? pack.Types.Where(t => chosen.Contains(t.Ref)).ToList() : [.. pack.Types];
        if (types.Count == 0) throw new CommandException("None of the chosen types is in the pack.");
        var byRef = pack.Types.Concat(pack.Resources).ToDictionary(i => i.Ref);
        var materialsByName = pack.Resources.Where(r => r.Class == IfcClass.Material)
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var owned = types.Where(t => t.Material != null && byRef[t.Material].Class != IfcClass.Material).Select(t => t.Material!).ToHashSet();

        // What the chosen types need, by ref (their own layer sets apart: they come with their type). A material named under a
        // material key comes when the pack carries it (the export takes every one the project had); any other name is the module's to settle.
        var needed = new HashSet<string>();
        var stack = new Stack<PackItem>(types);
        foreach (var t in types) if (t.Material != null) stack.Push(byRef[t.Material]);
        var visited = new HashSet<string>();
        while (stack.Count > 0)
        {
            var item = stack.Pop();
            if (!visited.Add(item.Ref)) continue;
            if (!owned.Contains(item.Ref) && !types.Contains(item)) needed.Add(item.Ref);
            Walk(item.Attrs, false, s => { if (byRef.TryGetValue(s, out var x) && x.Ref != item.Ref) stack.Push(x); }, s =>
            {
                if (!byRef.ContainsKey(s) && materialsByName.TryGetValue(s, out var mat)) stack.Push(mat);
            });
        }

        var map = new Dictionary<string, string>();
        // A material kept beside the project's as „Name (2)”: what names it by its name names the new one.
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tally = new LibraryMerge.MaterialTally();
        // Materials: the project's of that name (kept as it is, or updated from the pack, or the pack's made beside it), else made as the pack has it.
        foreach (var r in needed.Select(x => byRef[x]).Where(x => x.Class == IfcClass.Material).ToList())
        {
            if (Materials.Find(ctx.Model, r.Name) is { } found)
            {
                var attrs = Mapped(r.Attrs, map, names);
                if (JsonNode.DeepEquals(Comparable(found.Attrs), Comparable(attrs))) { map[r.Ref] = found.Id; continue; }
                switch (choices.Material(r.Name))
                {
                    case LibraryMerge.Update:
                        foreach (var k in Volatile) if (found.Attrs[k] is { } v) attrs[k] = v.DeepClone();
                        ctx.Replace(found.With(attrs: attrs));
                        map[r.Ref] = found.Id;
                        tally.Updated++;
                        continue;
                    case LibraryMerge.Both:
                        var name = LibraryMerge.FreeName(ctx.Model, IfcClass.Material, r.Name);
                        var made = new Entity(ctx.NewId(), IfcClass.Material, name, attrs, r.PredefinedType);
                        ctx.Add(made);
                        map[r.Ref] = made.Id;
                        names[r.Name] = name;
                        tally.Made++;
                        tally.Renamed.Add(name);
                        continue;
                    default:
                        map[r.Ref] = found.Id;
                        tally.Kept++;
                        continue;
                }
            }
            var mat = new Entity(ctx.NewId(), IfcClass.Material, r.Name, r.Attrs, r.PredefinedType);
            ctx.Add(mat);
            map[r.Ref] = mat.Id;
            tally.Made++;
        }
        // The other resources, then the types, each once what it points at is settled; a cycle settles with new ids.
        var pending = needed.Select(x => byRef[x]).Where(x => x.Class != IfcClass.Material).Cast<PackItem>().Concat(types).ToList();
        var outcomes = new List<Outcome>();
        while (pending.Count > 0)
        {
            var ready = pending.Where(i => Refs(i, byRef).All(r => map.ContainsKey(r) || owned.Contains(r) || r == i.Ref)
                && (i.Material is null || map.ContainsKey(i.Material) || owned.Contains(i.Material))
                && (i.Material is null || !owned.Contains(i.Material) || Refs(byRef[i.Material], byRef).All(map.ContainsKey))).ToList();
            if (ready.Count == 0)
            {
                // Pointing at each other: new ids for all of them first.
                foreach (var i in pending) map[i.Ref] = ctx.NewId();
                ready = pending;
            }
            foreach (var i in ready)
            {
                pending.Remove(i);
                if (types.Contains(i)) outcomes.Add(PlaceType(ctx, i, byRef, map, names, choices.Type(i)));
                else PlaceResource(ctx, i, map, names);
            }
        }
        return (outcomes, tally);
    }

    static IEnumerable<string> Refs(PackItem i, Dictionary<string, PackItem> byRef)
    {
        var o = new List<string>();
        Walk(i.Attrs, false, s => { if (byRef.ContainsKey(s)) o.Add(s); }, _ => { });
        return o;
    }

    /// <summary>The pack's attrs with its refs turned into the project's ids, and a material kept beside the project's named by its new name.</summary>
    static JsonObject Mapped(JsonObject attrs, Dictionary<string, string> map, Dictionary<string, string>? names = null) => (JsonObject)MapNode(attrs, map, names, false)!;

    static JsonNode? MapNode(JsonNode? n, Dictionary<string, string> map, Dictionary<string, string>? names, bool material) => n switch
    {
        JsonObject o => new JsonObject(o.Select(kv => KeyValuePair.Create(kv.Key, MapNode(kv.Value, map, names, material || kv.Key.Contains("material", StringComparison.OrdinalIgnoreCase))))),
        JsonArray a => new JsonArray(a.Select(v => MapNode(v, map, names, material)).ToArray()),
        JsonValue v when v.TryGetValue<string>(out var s) && map.TryGetValue(s, out var id) => JsonValue.Create(id),
        JsonValue v when material && names is { Count: > 0 } && v.TryGetValue<string>(out var s) && names.TryGetValue(s, out var renamed) => JsonValue.Create(renamed),
        _ => n?.DeepClone(),
    };

    static JsonObject Comparable(JsonObject attrs)
    {
        var c = (JsonObject)attrs.DeepClone();
        foreach (var k in Volatile) c.Remove(k);
        return c;
    }

    static bool Same(Entity e, string cls, string name, JsonObject attrs) =>
        e.Class == cls && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase) && JsonNode.DeepEquals(Comparable(e.Attrs), Comparable(attrs));

    static void PlaceResource(ICommandContext ctx, PackItem r, Dictionary<string, string> map, Dictionary<string, string> names)
    {
        var attrs = Mapped(r.Attrs, map, names);
        if (!map.ContainsKey(r.Ref) && ctx.Model.OfClass(r.Class).FirstOrDefault(e => Same(e, r.Class, r.Name, attrs)) is { } same) { map[r.Ref] = same.Id; return; }
        var id = map.TryGetValue(r.Ref, out var given) ? given : ctx.NewId();
        ctx.Add(new Entity(id, r.Class, r.Name, attrs, r.PredefinedType));
        map[r.Ref] = id;
    }

    static Outcome PlaceType(ICommandContext ctx, PackItem t, Dictionary<string, PackItem> byRef, Dictionary<string, string> map, Dictionary<string, string> names, string choice)
    {
        var attrs = Mapped(t.Attrs, map, names);
        var set = t.Material is { } mr && byRef[mr].Class != IfcClass.Material ? byRef[mr] : null;
        var setAttrs = set is null ? null : Mapped(set.Attrs, map, names);
        var cycle = map.ContainsKey(t.Ref);
        var sameName = ctx.Model.OfClass(t.Class).Where(e => string.Equals(e.Name, t.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!cycle)
            foreach (var e in sameName)
            {
                if (!Same(e, t.Class, t.Name, attrs)) continue;
                var assoc = ctx.Model.RelatingOf(e.Id, IfcClass.RelAssociatesMaterial);
                var fits = t.Material is null ? assoc is null
                    : set is null ? assoc?.Id == map[t.Material]
                    : assoc is { } a && a.Class == set.Class && JsonNode.DeepEquals(Comparable(a.Attrs), Comparable(setAttrs!));
                if (!fits) continue;
                map[t.Ref] = e.Id;
                if (set != null) map[set.Ref] = assoc!.Id;
                return new Outcome(t.Ref, t.Class, e.Name, e.Id, "reused");
            }
        if (!cycle && sameName.Count > 0 && choice == LibraryMerge.Skip)
        {
            map[t.Ref] = sameName[0].Id;
            return new Outcome(t.Ref, t.Class, sameName[0].Name, sameName[0].Id, "skipped");
        }
        if (!cycle && sameName.Count > 0 && choice == LibraryMerge.Update) return UpdateType(ctx, t, sameName[0], attrs, set, setAttrs, map);
        var name = t.Name;
        if (sameName.Count > 0)
        {
            name = LibraryMerge.FreeName(ctx.Model, t.Class, t.Name);
            // A door's or window's spec carries its name too.
            if (attrs["spec"] is JsonObject spec && (string?)spec["name"] == t.Name) spec["name"] = name;
        }
        var id = map.TryGetValue(t.Ref, out var given) ? given : ctx.NewId();
        var type = new Entity(id, t.Class, name, attrs, t.PredefinedType);
        ctx.Add(type);
        map[t.Ref] = id;
        if (set != null)
        {
            var setId = map.TryGetValue(set.Ref, out var sg) && ctx.Model.Get(sg) is null ? sg : ctx.NewId();
            ctx.Add(new Entity(setId, set.Class, set.Name == t.Name ? name : set.Name, setAttrs!, set.PredefinedType));
            map[set.Ref] = setId;
            ctx.Relate(IfcClass.RelAssociatesMaterial, setId, id);
        }
        else if (t.Material is { } matRef && map.TryGetValue(matRef, out var matId)) ctx.Relate(IfcClass.RelAssociatesMaterial, matId, id);
        return new Outcome(t.Ref, t.Class, name, id, sameName.Count > 0 ? "renamed" : "created");
    }

    /// <summary>
    /// The project's type of that name made the pack's in place: its id (and GlobalId) kept, so its elements follow; its layer
    /// set rewritten in place when it has one of the class, and its elements laid out again by their module (as its own type
    /// edit does: <see cref="ITypeAssigner.Updated"/>).
    /// </summary>
    static Outcome UpdateType(ICommandContext ctx, PackItem t, Entity existing, JsonObject attrs, PackItem? set, JsonObject? setAttrs, Dictionary<string, string> map)
    {
        foreach (var k in Volatile) if (existing.Attrs[k] is { } v) attrs[k] = v.DeepClone();
        if (attrs["spec"] is JsonObject spec && spec["name"] is not null) spec["name"] = existing.Name;
        ctx.Replace(existing.With(attrs: attrs, predefinedType: t.PredefinedType));
        map[t.Ref] = existing.Id;
        var assoc = ctx.Model.RelatingOf(existing.Id, IfcClass.RelAssociatesMaterial);
        if (set != null)
        {
            if (assoc is { } a && a.Class == set.Class && ctx.Model.RelatedOf(a.Id, IfcClass.RelAssociatesMaterial).All(x => x.Id == existing.Id))
            {
                ctx.Replace(a.With(attrs: setAttrs!));
                map[set.Ref] = a.Id;
            }
            else
            {
                var setId = ctx.NewId();
                ctx.Add(new Entity(setId, set.Class, set.Name == t.Name ? existing.Name : set.Name, setAttrs!, set.PredefinedType));
                map[set.Ref] = setId;
                ctx.Relate(IfcClass.RelAssociatesMaterial, setId, existing.Id);
            }
        }
        else if (t.Material is { } matRef && map.TryGetValue(matRef, out var matId)) ctx.Relate(IfcClass.RelAssociatesMaterial, matId, existing.Id);
        var now = ctx.Model.Get(existing.Id)!;
        foreach (var assigner in ctx.Services.All<ITypeAssigner>().Where(x => x.TypeClasses.Contains(t.Class))) assigner.Updated(ctx, ctx.Services, now);
        return new Outcome(t.Ref, t.Class, existing.Name, existing.Id, "updated");
    }

    // ------------------------------------------------------------ commands

    public static void Register(IModuleRegistry r)
    {
        // The project's types, for choosing which to export: {types: [{id, class, label, name, count}]}.
        r.Query("types.list", (q, _) =>
        {
            var o = new JsonArray();
            foreach (var e in q.Model.Entities)
            {
                if (q.Describe(e.Class) is not { Kind: "type" } d || e.Attrs["imported"] is JsonObject) continue;
                o.Add((JsonNode)new JsonObject { ["id"] = e.Id, ["class"] = e.Class, ["label"] = d.Label, ["name"] = e.Name, ["count"] = q.Model.RelatedOf(e.Id, IfcClass.RelDefinesByType).Count() });
            }
            return new JsonObject { ["types"] = o };
        });
        // {ids, name?} → {text, file}: the types as a .lapintypes pack.
        r.Query("types.export", (q, a) =>
        {
            var ids = (a["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [];
            var name = (string?)a["name"];
            var text = Export(q, ids, name).ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            var file = (name?.Trim() is { Length: > 0 } n ? string.Concat(n.Split(Path.GetInvalidFileNameChars())) : "Types") + "." + Extension;
            return new JsonObject { ["text"] = text, ["file"] = file };
        });
        // {text} → {name, types: [{ref, class, label, name, exists, differs?}], materials: [{name, differs}]}: what a pack holds, before
        // choosing, and what the project has by name that differs from it (a type's differs: what of it; a material's: what of its definition).
        r.Query("types.readPack", (q, a) =>
        {
            var pack = Read((string?)a["text"] ?? "", q.Describe);
            var (materials, conflicts) = Conflicts(q.Model, pack);
            return new JsonObject
            {
                ["name"] = pack.Name,
                ["types"] = new JsonArray(pack.Types.Select(t =>
                {
                    var o = new JsonObject
                    {
                        ["ref"] = t.Ref, ["class"] = t.Class, ["label"] = q.Describe(t.Class)?.Label ?? t.Class, ["name"] = t.Name,
                        ["exists"] = q.Model.OfClass(t.Class).Any(e => string.Equals(e.Name, t.Name, StringComparison.OrdinalIgnoreCase)),
                    };
                    if (conflicts.FirstOrDefault(c => c.Key == t.Ref) is { } c) o["differs"] = Strings(c.Differs);
                    return (JsonNode)o;
                }).ToArray()),
                ["materials"] = new JsonArray(materials.Select(m => (JsonNode)new JsonObject { ["name"] = m.Name, ["differs"] = Strings(m.Differs) }).ToArray()),
            };
        });
        // {text, refs?, materials?: {name: keep|update|both}, types?: {ref: both|update|skip}} → {types: [{ref, class, name, id, status:
        // created|reused|renamed|updated|skipped}], created, reused, updated, skipped, materials, materialsUpdated, materialsKept, materialsRenamed}: one undo step.
        // Without choices, as before: a material of the same name the project's, a type that differs made beside it.
        r.Command("types.importFrom", "Import types", (ctx, a) =>
        {
            var pack = Read((string?)a["text"] ?? throw new CommandException("Nothing to bring in."), ctx.Describe);
            var chosen = (a["refs"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet();
            var choices = new Choices(LibraryMerge.Choices(a["materials"], LibraryMerge.MaterialChoices), LibraryMerge.Choices(a["types"], LibraryMerge.TypeChoices));
            var (types, materials) = Import(ctx, pack, chosen, choices);
            return new JsonObject
            {
                ["types"] = new JsonArray(types.Select(t => (JsonNode)new JsonObject { ["ref"] = t.Ref, ["class"] = t.Class, ["name"] = t.Name, ["id"] = t.Id, ["status"] = t.Status }).ToArray()),
                ["created"] = types.Count(t => t.Status is "created" or "renamed"),
                ["reused"] = types.Count(t => t.Status == "reused"),
                ["updated"] = types.Count(t => t.Status == "updated"),
                ["skipped"] = types.Count(t => t.Status == "skipped"),
                ["materials"] = materials.Made,
                ["materialsUpdated"] = materials.Updated,
                ["materialsKept"] = materials.Kept,
                ["materialsRenamed"] = Strings(materials.Renamed),
            };
        });
    }
}
