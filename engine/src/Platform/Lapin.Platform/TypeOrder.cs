using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Any type duplicated, and the types of a class in an order of the user's (forum #71, decision 559): wall, floor, door,
/// window, stair, column, beam types… are listed (every type list and picker) in the order the project keeps for their
/// class, <c>typeOrder</c> on the project (<c>{class: [ids]}</c>); a type the list does not name comes after those it
/// names, in creation order, so a project that never ordered its types lists them as before. The order is kept on the
/// project rather than on each type, so moving a type changes no type and rebuilds none of its elements.
/// <para>
/// <c>types.duplicate</c> copies a type whole: its attributes, with a new id (a new GlobalId), named „Name (2)” (the
/// next number no type of its class has), its own material set (a layer set, a profile set, a constituent set) copied
/// too so the copy is edited apart, the materials, classifications and other things it is related to shared; the copy
/// is listed just after the original. <c>types.move</c> moves a type up or down its class's list, <c>types.order</c>
/// gives a class's whole order. Each is one undo step.
/// </para>
/// </summary>
public static class TypeOrder
{
    public const string Key = "typeOrder";

    /// <summary>Whether an entity is a type: its class says so (a module's descriptor), or its name ends in Type.</summary>
    public static bool IsType(ClassDescriptor? d, Entity e) => d is { Kind: "type" } || d is null && e.Class.EndsWith("Type", StringComparison.Ordinal);

    /// <summary>A class's types in the project's order: those its list names, in that order, then the others in creation order.</summary>
    public static List<Entity> Ordered(IModelView m, string cls)
    {
        var all = m.OfClass(cls).ToList();
        var listed = Listed(m, cls);
        if (listed.Count == 0) return all;
        var rank = listed.Select((id, i) => (id, i)).GroupBy(p => p.id).ToDictionary(g => g.Key, g => g.First().i);
        return [.. all.Select((e, i) => (e, i)).OrderBy(p => rank.TryGetValue(p.e.Id, out var r) ? r : int.MaxValue).ThenBy(p => p.i).Select(p => p.e)];
    }

    /// <summary>The ids the project's list names for a class, as it keeps them (some may be gone).</summary>
    static List<string> Listed(IModelView m, string cls) =>
        Spatial.Project(m)?.Attrs[Key]?[cls] is JsonArray a ? [.. a.Select(x => (string?)x).OfType<string>()] : [];

    /// <summary>The class's order written as <paramref name="ids"/> (the types still there; the others after them).</summary>
    static void Write(ICommandContext ctx, string cls, IEnumerable<string> ids)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        var all = attrs[Key] as JsonObject ?? [];
        all = (JsonObject)all.DeepClone();
        all[cls] = new JsonArray([.. ids.Distinct().Where(id => ctx.Model.Get(id)?.Class == cls).Select(id => (JsonNode)id)]);
        attrs[Key] = all;
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>„Name (2)”, or the next number no other type of the class has („Wall (2)” gives „Wall (3)”).</summary>
    public static string CopyName(IModelView m, string cls, string name)
    {
        var taken = m.OfClass(cls).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var match = Regex.Match(name, @"^(.*) \((\d+)\)$");
        var (stem, n) = match.Success ? (match.Groups[1].Value, int.Parse(match.Groups[2].Value) + 1) : (name, 2);
        while (taken.Contains($"{stem} ({n})")) n++;
        return $"{stem} ({n})";
    }

    /// <summary>The material sets a type owns: copied with it, where a material of the library is shared.</summary>
    static bool OwnSet(Entity e) => e.Class.StartsWith("IfcMaterial", StringComparison.Ordinal) && e.Class.EndsWith("Set", StringComparison.Ordinal);

    /// <summary>A type copied whole, listed just after it (see the class's summary).</summary>
    public static Entity Duplicate(ICommandContext ctx, Entity type, string? name)
    {
        var m = ctx.Model;
        var copy = new Entity(ctx.NewId(), type.Class, name?.Trim() is { Length: > 0 } n ? n : CopyName(m, type.Class, type.Name), type.CloneAttrs(), type.PredefinedType);
        ctx.Add(copy);
        foreach (var rel in m.RelatedBy(type.Id).ToList())
        {
            if (m.Get(rel.Relating) is not { } relating) continue;
            if (rel.Class == "IfcRelAssociatesMaterial" && OwnSet(relating))
            {
                var set = new Entity(ctx.NewId(), relating.Class, relating.Name == type.Name ? copy.Name : relating.Name, relating.CloneAttrs(), relating.PredefinedType);
                ctx.Add(set);
                ctx.Relate(rel.Class, set.Id, copy.Id);
            }
            else ctx.Relate(rel.Class, relating.Id, copy.Id);
        }
        var order = Ordered(ctx.Model, type.Class).Select(e => e.Id).Where(id => id != copy.Id).ToList();
        order.Insert(order.IndexOf(type.Id) + 1, copy.Id);
        Write(ctx, type.Class, order);
        return copy;
    }

    static Entity RequireType(ICommandContext ctx, string? id) =>
        id is { } i && ctx.Model.Get(i) is { } t && IsType(ctx.Describe(t.Class), t) ? t : throw new CommandException("There is no such type.");

    public static void Register(IModuleRegistry r)
    {
        // A type copied whole ({id, name?}) → {id}.
        r.Command("types.duplicate", "Duplicate type", (ctx, a) =>
            new JsonObject { ["id"] = Duplicate(ctx, RequireType(ctx, (string?)a["id"]), (string?)a["name"]).Id });

        // A type moved up (by −1) or down (by +1, or more) its class's list; at an end it stays → {index}.
        r.Command("types.move", "Move type", (ctx, a) =>
        {
            var type = RequireType(ctx, (string?)a["id"]);
            var by = (int)Math.Round(Lapin.Model.Json.Number(a["by"]) ?? ((string?)a["to"] switch { "up" => -1, "down" => 1, _ => 0 }));
            var order = Ordered(ctx.Model, type.Class).Select(e => e.Id).ToList();
            var at = order.IndexOf(type.Id);
            var to = Math.Clamp(at + by, 0, order.Count - 1);
            if (to != at)
            {
                order.RemoveAt(at);
                order.Insert(to, type.Id);
                Write(ctx, type.Class, order);
            }
            return new JsonObject { ["index"] = to };
        });

        // A class's whole order ({class, ids}): the types named in that order, the others after them.
        r.Command("types.order", "Order types", (ctx, a) =>
        {
            var cls = (string?)a["class"] ?? throw new CommandException("Which class of types?");
            if (a["ids"] is not JsonArray ids) throw new CommandException("The types' ids, in their order.");
            var named = ids.Select(x => (string?)x).OfType<string>().Where(id => ctx.Model.Get(id)?.Class == cls).ToList();
            Write(ctx, cls, [.. named, .. Ordered(ctx.Model, cls).Select(e => e.Id).Where(id => !named.Contains(id))]);
            return null;
        });

        // A class's types in the project's order → [{id, name}].
        r.Query("types.ordered", (q, a) =>
        {
            var cls = (string?)a["class"] ?? throw new CommandException("Which class of types?");
            return new JsonArray([.. Ordered(q.Model, cls).Select(e => (JsonNode)new JsonObject { ["id"] = e.Id, ["name"] = e.Name })]);
        });
    }
}
