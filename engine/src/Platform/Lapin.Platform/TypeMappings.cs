using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Gives an element another type of its kind the way its module does it (a wall keeps its core where it was, a door
/// takes the type's size): what mapping imported types needs from each module.
/// </summary>
public interface ITypeAssigner
{
    /// <summary>The type classes it assigns (IfcWallType, IfcDoorType…).</summary>
    IReadOnlyCollection<string> TypeClasses { get; }
    void Assign(IEdit edit, IServices services, Entity element, Entity type);
}

/// <summary>
/// Imported types mapped onto the project's (docs/spec-ifc.md, Î9): an import marks the types it made
/// (<c>imported: {file, name}</c>); Manage → Map imported types replaces each with one of the project's own of the same
/// class (its elements take it, as its module gives a type; the imported one goes), and remembers the choice in the
/// project, so a later import of a type of that class and name takes the project's at once.
/// </summary>
public static class TypeMappings
{
    /// <summary>The project's remembered choices: one entity, its <c>map</c> keyed by type class and the file's type name.</summary>
    public const string Class = "LapinTypeMap";

    static string Key(string typeClass, string name) => $"{typeClass}|{name.Trim().ToLowerInvariant()}";

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Type mappings", [], Kind: "resource"));
        r.Query("types.imported", (q, _) => Imported(q.Model));

        r.Command("types.map", "Map imported types", (ctx, a) =>
        {
            if (a["map"] is not JsonArray pairs || pairs.Count == 0) throw new CommandException("Say which imported types become which.");
            var remember = a["remember"] is not JsonValue rv || !rv.TryGetValue<bool>(out var keep) || keep;
            var moved = 0;
            foreach (var p in pairs.OfType<JsonObject>())
                moved += Map(ctx, ctx.Services, Type(ctx.Model, (string?)p["from"]), Type(ctx.Model, (string?)p["to"]), remember);
            return new JsonObject { ["moved"] = moved };
        });

        r.Command("types.forget", "Forget type mappings", (ctx, a) =>
        {
            if (ctx.Model.OfClass(Class).FirstOrDefault() is not { } map) return null;
            var keys = a["keys"] is JsonArray ks ? ks.Select(k => (string?)k).OfType<string>().ToHashSet() : null;
            var attrs = map.CloneAttrs();
            if (keys == null || attrs["map"] is not JsonObject o) { ctx.Remove(map.Id); return null; }
            foreach (var k in keys) o.Remove(k);
            ctx.Replace(map.With(attrs: attrs));
            return null;
        });
    }

    static Entity Type(IModelView m, string? id) => m.Get(id ?? "") ?? throw new CommandException("There is no such type.");

    /// <summary>
    /// The types imports made, each with its class, the file and name it came by, how many elements it has, and the
    /// project's other types of its class; and the remembered choices.
    /// </summary>
    public static JsonObject Imported(IModelView m)
    {
        var types = new JsonArray();
        foreach (var t in m.Entities.Where(e => e.Attrs["imported"] is JsonObject o && o["typeOf"] != null))
        {
            var imported = (JsonObject)t.Attrs["imported"]!;
            var others = m.OfClass(t.Class).Where(o => o.Id != t.Id && o.Attrs["imported"]?["typeOf"] == null)
                .Select(o => (JsonNode)new JsonObject { ["id"] = o.Id, ["name"] = o.Name }).ToArray();
            types.Add((JsonNode)new JsonObject
            {
                ["id"] = t.Id, ["name"] = t.Name, ["class"] = t.Class,
                ["file"] = (string?)imported["file"], ["sourceName"] = (string?)imported["name"] ?? t.Name,
                ["count"] = m.RelatedOf(t.Id, IfcClass.RelDefinesByType).Count(),
                ["candidates"] = new JsonArray(others),
            });
        }
        var remembered = new JsonArray();
        if (m.OfClass(Class).FirstOrDefault()?.Attrs["map"] is JsonObject map)
            foreach (var (k, v) in map)
                if (v is JsonObject o)
                    remembered.Add((JsonNode)new JsonObject { ["key"] = k, ["class"] = (string?)o["class"], ["name"] = (string?)o["name"], ["to"] = (string?)o["to"], ["toName"] = m.Get((string?)o["to"] ?? "")?.Name });
        return new JsonObject { ["types"] = types, ["remembered"] = remembered };
    }

    /// <summary><paramref name="from"/>'s elements given <paramref name="to"/>, <paramref name="from"/> gone; the choice kept when asked. How many moved.</summary>
    public static int Map(IEdit edit, IServices services, Entity from, Entity to, bool remember)
    {
        if (from.Id == to.Id) return 0;
        if (from.Class != to.Class) throw new CommandException($"{from.Name} and {to.Name} are not types of the same kind.");
        var assigner = services.All<ITypeAssigner>().FirstOrDefault(x => x.TypeClasses.Contains(from.Class))
            ?? throw new CommandException($"No module here gives elements a type of {from.Class}.");
        var elements = edit.Model.RelatedOf(from.Id, IfcClass.RelDefinesByType).ToList();
        foreach (var e in elements) assigner.Assign(edit, services, e, edit.Model.Get(to.Id)!);
        if (remember && edit.Model.Get(from.Id)?.Attrs["imported"] is JsonObject imported)
        {
            var name = (string?)imported["name"] ?? from.Name;
            var map = edit.Model.OfClass(Class).FirstOrDefault();
            if (map == null) { map = new Entity(edit.NewId(), Class, "Type mappings", new JsonObject { ["map"] = new JsonObject() }); edit.Add(map); }
            var attrs = map.CloneAttrs();
            var o = attrs["map"] as JsonObject ?? new JsonObject();
            o[Key(from.Class, name)] = new JsonObject { ["class"] = from.Class, ["name"] = name, ["to"] = to.Id };
            attrs["map"] = o;
            edit.Replace(map.With(attrs: attrs));
        }
        // The imported type goes with what it carried alone (its layer set).
        if (!edit.Model.RelatedOf(from.Id, IfcClass.RelDefinesByType).Any())
        {
            if (LayerSets.Of(edit.Model, from.Id) is { } set && !edit.Model.RelationsOf(set.Id, IfcClass.RelAssociatesMaterial).Any(r => r.Related.Any(x => x != from.Id)))
                edit.Remove(set.Id);
            edit.Remove(from.Id);
        }
        return elements.Count;
    }

    /// <summary>
    /// After an import: the types it made marked with the file and their names, and each one the project has a choice
    /// for (a type of its class and name mapped before) replaced by the project's at once. What was mapped, for the notes.
    /// </summary>
    public static int AfterImport(IEdit edit, IServices services, IEnumerable<string> created, string? file)
    {
        var map = edit.Model.OfClass(Class).FirstOrDefault()?.Attrs["map"] as JsonObject;
        var mapped = 0;
        foreach (var id in created.ToList())
        {
            if (edit.Model.Get(id) is not { } t) continue;
            var attrs = t.CloneAttrs();
            var imported = attrs["imported"] as JsonObject ?? new JsonObject();
            imported["typeOf"] = true;
            imported["name"] ??= t.Name;
            if (file != null) imported["file"] ??= file;
            attrs["imported"] = imported;
            edit.Replace(t.With(attrs: attrs));
            if (map?[Key(t.Class, (string?)imported["name"] ?? t.Name)] is JsonObject choice && edit.Model.Get((string?)choice["to"] ?? "") is { } to && to.Class == t.Class
                && services.All<ITypeAssigner>().Any(x => x.TypeClasses.Contains(t.Class)))
            {
                Map(edit, services, edit.Model.Get(id)!, to, remember: false);
                mapped++;
            }
        }
        return mapped;
    }

    /// <summary>How many of the types an import made have a type of the project's own of their kind to become (what the import's message offers).</summary>
    public static int Mappable(IModelView m, IEnumerable<string> created) =>
        created.Select(m.Get).OfType<Entity>().Count(t => m.OfClass(t.Class).Any(o => o.Id != t.Id && o.Attrs["imported"]?["typeOf"] == null));
}
