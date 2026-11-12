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
/// other made. A type's own layer set comes and goes with it. Ids are always new: nothing in the project is changed.</para>
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

    // ------------------------------------------------------------ the import

    public sealed record Outcome(string Ref, string Class, string Name, string Id, string Status);

    /// <summary>The pack's chosen types (none chosen: all) brought into the project; what became of each, and how many materials were made.</summary>
    public static (List<Outcome> Types, int MaterialsMade) Import(ICommandContext ctx, Pack pack, IReadOnlyCollection<string>? chosen)
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
        var made = 0;
        // Materials: the project's of that name, else made as the pack has it.
        foreach (var r in needed.Select(x => byRef[x]).Where(x => x.Class == IfcClass.Material).ToList())
        {
            if (Materials.Find(ctx.Model, r.Name) is { } found) { map[r.Ref] = found.Id; continue; }
            var mat = new Entity(ctx.NewId(), IfcClass.Material, r.Name, r.Attrs, r.PredefinedType);
            ctx.Add(mat);
            map[r.Ref] = mat.Id;
            made++;
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
                if (types.Contains(i)) outcomes.Add(PlaceType(ctx, i, byRef, map));
                else PlaceResource(ctx, i, map);
            }
        }
        return (outcomes, made);
    }

    static IEnumerable<string> Refs(PackItem i, Dictionary<string, PackItem> byRef)
    {
        var o = new List<string>();
        Walk(i.Attrs, false, s => { if (byRef.ContainsKey(s)) o.Add(s); }, _ => { });
        return o;
    }

    /// <summary>The pack's attrs with its refs turned into the project's ids.</summary>
    static JsonObject Mapped(JsonObject attrs, Dictionary<string, string> map) => (JsonObject)MapNode(attrs, map)!;

    static JsonNode? MapNode(JsonNode? n, Dictionary<string, string> map) => n switch
    {
        JsonObject o => new JsonObject(o.Select(kv => KeyValuePair.Create(kv.Key, MapNode(kv.Value, map)))),
        JsonArray a => new JsonArray(a.Select(v => MapNode(v, map)).ToArray()),
        JsonValue v when v.TryGetValue<string>(out var s) && map.TryGetValue(s, out var id) => JsonValue.Create(id),
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

    static void PlaceResource(ICommandContext ctx, PackItem r, Dictionary<string, string> map)
    {
        var attrs = Mapped(r.Attrs, map);
        if (!map.ContainsKey(r.Ref) && ctx.Model.OfClass(r.Class).FirstOrDefault(e => Same(e, r.Class, r.Name, attrs)) is { } same) { map[r.Ref] = same.Id; return; }
        var id = map.TryGetValue(r.Ref, out var given) ? given : ctx.NewId();
        ctx.Add(new Entity(id, r.Class, r.Name, attrs, r.PredefinedType));
        map[r.Ref] = id;
    }

    static Outcome PlaceType(ICommandContext ctx, PackItem t, Dictionary<string, PackItem> byRef, Dictionary<string, string> map)
    {
        var attrs = Mapped(t.Attrs, map);
        var set = t.Material is { } mr && byRef[mr].Class != IfcClass.Material ? byRef[mr] : null;
        var setAttrs = set is null ? null : Mapped(set.Attrs, map);
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
        var name = t.Name;
        if (sameName.Count > 0)
        {
            var n = 2;
            while (ctx.Model.OfClass(t.Class).Any(e => string.Equals(e.Name, $"{t.Name} ({n})", StringComparison.OrdinalIgnoreCase))) n++;
            name = $"{t.Name} ({n})";
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
        // {text} → {name, types: [{ref, class, label, name, exists}]}: what a pack holds, before choosing.
        r.Query("types.readPack", (q, a) =>
        {
            var pack = Read((string?)a["text"] ?? "", q.Describe);
            return new JsonObject
            {
                ["name"] = pack.Name,
                ["types"] = new JsonArray(pack.Types.Select(t => (JsonNode)new JsonObject
                {
                    ["ref"] = t.Ref, ["class"] = t.Class, ["label"] = q.Describe(t.Class)?.Label ?? t.Class, ["name"] = t.Name,
                    ["exists"] = q.Model.OfClass(t.Class).Any(e => string.Equals(e.Name, t.Name, StringComparison.OrdinalIgnoreCase)),
                }).ToArray()),
            };
        });
        // {text, refs?} → {types: [{ref, class, name, id, status: created|reused|renamed}], created, reused, materials}: one undo step.
        r.Command("types.importFrom", "Import types", (ctx, a) =>
        {
            var pack = Read((string?)a["text"] ?? throw new CommandException("Nothing to bring in."), ctx.Describe);
            var chosen = (a["refs"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet();
            var (types, materials) = Import(ctx, pack, chosen);
            return new JsonObject
            {
                ["types"] = new JsonArray(types.Select(t => (JsonNode)new JsonObject { ["ref"] = t.Ref, ["class"] = t.Class, ["name"] = t.Name, ["id"] = t.Id, ["status"] = t.Status }).ToArray()),
                ["created"] = types.Count(t => t.Status != "reused"),
                ["reused"] = types.Count(t => t.Status == "reused"),
                ["materials"] = materials,
            };
        });
    }
}
