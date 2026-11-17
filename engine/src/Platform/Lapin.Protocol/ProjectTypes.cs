using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>
/// File → Import types from a project… (issue #74, decision 669): another Lapin project's types, any kind the type packs carry
/// (wall, slab, roof, door, window, column, beam, stair…), shown with the materials each needs, chosen and brought into the open
/// project without exporting a .lapintypes first. The source is read from its bytes into a model of its own, brought up to
/// date in memory by the modules' project upgrades as an opened file is, and never written to; its types are taken out as a
/// type pack (<see cref="TypePacks.Export"/>), so the import is the pack's (library.importProjectTypes): the same rules for
/// materials and names, the types keeping their GlobalIds, one the project already has under its very GlobalId being that type.
/// </summary>
public static class ProjectTypes
{
    /// <summary>The source project's types as a pack (none: refused), with how many elements each types there.</summary>
    public static (JsonObject Pack, Dictionary<string, int> Counts) Read(Session host, byte[] bytes, string? name)
    {
        using var input = new MemoryStream(bytes, writable: false);
        if (LegacyPackage.Is(input)) throw new CommandException("This is an old Lapin project: open it and save it again, then import its types.");
        input.Position = 0;
        PackageContent content;
        try { content = LapinPackage.Read(input); }
        catch (Exception e) when (e is InvalidDataException or IOException or JsonException or KeyNotFoundException)
        {
            throw new CommandException($"This is not a Lapin project ({e.Message}).");
        }
        // As an opened file is, in memory only: the modules' upgrades on a model of its own, nothing of it ever saved.
        var (entities, relations) = host.Upgraded(content.Entities, content.Relations);
        var store = new ModelStore();
        new History(store).Load(entities, relations);
        var q = new ModelQuery(store, host.Catalog, host.Catalog.Class);
        // The project's own types as types.list has them: not those an IFC brought (kept as the file had them), not a link's.
        var types = store.Entities
            .Where(e => q.Describe(e.Class) is { Kind: "type" } && e.Attrs["imported"] is not JsonObject && e.Str(Links.Key) is null)
            .ToList();
        if (types.Count == 0) throw new CommandException("That project has no types this edition of Lapin can bring in.");
        var title = name is { Length: > 0 } ? Path.GetFileNameWithoutExtension(name.Replace('\\', '/').Split('/')[^1]) : null;
        var pack = TypePacks.Export(q, [.. types.Select(t => t.Id)], title);
        var counts = types.ToDictionary(t => t.Id, t => store.RelatedOf(t.Id, IfcClass.RelDefinesByType).Count());
        return (pack, counts);
    }

    /// <summary>
    /// {name, text, types: [{ref, class, label, name, count, materials, status: new | same | name | id, exists, differs?}], materials:
    /// [{name, differs}], rows, new, same, conflicts}: what the project in <paramref name="bytes"/> holds and what an import into the
    /// open one would do (<see cref="LibraryMerge"/>); <c>text</c> is what library.importProjectTypes takes.
    /// </summary>
    public static JsonObject Preview(Session host, byte[] bytes, string? name)
    {
        var (packJson, counts) = Read(host, bytes, name);
        var text = packJson.ToJsonString();
        var pack = TypePacks.Read(text, host.Catalog.Class);
        var q = new ModelQuery(host.Model, host.Catalog, host.Catalog.Class);
        var rows = TypePacks.Rows(q, pack, keepIds: true);
        var (materials, conflicts) = TypePacks.Conflicts(host.Model, pack, keepIds: true);
        var o = LibraryMerge.Preview(rows);
        o["name"] = pack.Name;
        o["text"] = text;
        o["types"] = new JsonArray([.. pack.Types.Select(t =>
        {
            var row = rows.First(r => r.Key == "type:" + t.Ref);
            var item = new JsonObject
            {
                ["ref"] = t.Ref, ["class"] = t.Class, ["label"] = q.Describe(t.Class)?.Label ?? t.Class, ["name"] = t.Name,
                ["count"] = counts.GetValueOrDefault(t.Ref),
                ["materials"] = new JsonArray([.. TypePacks.MaterialsOf(pack, t).Order(StringComparer.CurrentCultureIgnoreCase).Select(m => (JsonNode)m)]),
                ["status"] = row.ToJson()["status"]!.DeepClone(),
                ["exists"] = row.Match != LibraryMerge.Match.New,
            };
            if (conflicts.FirstOrDefault(c => c.Key == t.Ref) is { } c) item["differs"] = new JsonArray([.. c.Differs.Select(d => (JsonNode)d)]);
            return (JsonNode)item;
        })]);
        o["materials"] = new JsonArray([.. materials.Select(m => (JsonNode)new JsonObject { ["name"] = m.Name, ["differs"] = new JsonArray([.. m.Differs.Select(d => (JsonNode)d)]) })]);
        return o;
    }
}
