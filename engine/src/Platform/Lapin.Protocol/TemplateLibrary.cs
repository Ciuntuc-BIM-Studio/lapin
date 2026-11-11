using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>
/// Project templates kept on this computer (issues #9 and #20, decision 470): each a .lapin file in the user's folder
/// (<c>&lt;user-dir&gt;/templates/&lt;name&gt;.lapin</c>, the desktop's app data folder), named by its file, so a template can be
/// copied to another computer or shared with an office as it is. A template is an ordinary project package holding the
/// project's set-up and no model (<see cref="ProjectTemplates"/>); any project's file serves as one, its model left out when a
/// new project is made from it. The browser keeps its templates itself (the interface's IndexedDB), as bytes
/// (<see cref="Make"/>, <c>project.newFromTemplate</c> with <c>data</c>).
/// </summary>
public sealed class TemplateLibrary(string folder)
{
    public string Folder { get; } = folder;

    /// <summary>The open project as a template's package: its set-up, no model, no versions, no issues' files.</summary>
    public static byte[] Make(Session session)
    {
        var (entities, relations) = session.TemplateContent();
        return Write(session, entities, relations, session.ModuleOf);
    }

    static byte[] Write(Session session, IEnumerable<Entity> entities, IEnumerable<Relation> relations, Func<string, string> moduleOf)
    {
        var store = new ModelStore();
        new History(store).Load(entities, relations);
        var build = LapinPackage.Build(store, moduleOf, id => Spatial.StoreyOf(store, id)?.Id,
            $"Lapin {typeof(TemplateLibrary).Assembly.GetName().Version}", session.Product.Id,
            session.Catalog.Modules.Select(m => new PackageModule(m.Id, m.SchemaVersion)).ToList());
        using var ms = new MemoryStream();
        LapinPackage.Write(ms, build);
        return ms.ToArray();
    }

    /// <summary>A template's (or any project's) package read for a new project; an old project (format 2) is refused.</summary>
    public static PackageContent Read(byte[] bytes)
    {
        using var input = new MemoryStream(bytes, writable: false);
        if (LegacyPackage.Is(input)) throw new CommandException("This is an old Lapin project: open it and save it, then use it as a template.");
        input.Position = 0;
        try { return LapinPackage.Read(input); }
        catch (Exception e) when (e is InvalidDataException or IOException or System.Text.Json.JsonException)
        {
            throw new CommandException($"This is not a Lapin project or template ({e.Message}).");
        }
    }

    /// <summary>A new project from a template's package.</summary>
    public static Diff NewProject(Session session, byte[] bytes)
    {
        var content = Read(bytes);
        return session.NewFromTemplate(content.Entities, content.Relations, content.Classes);
    }

    /// <summary>A template's name as its file's: what the systems accept (no slashes, colons, quotes…), at most 80 characters.</summary>
    public static string IdOf(string name)
    {
        var bad = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var s = new string(name.Trim().Select(c => bad.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().TrimStart('.');
        if (s.Length > 80) s = s[..80].Trim();
        return s.Length == 0 ? throw new CommandException("Give the template a name.") : s;
    }

    string PathOf(string id) => Path.Combine(Folder, IdOf(id) + LapinPackage.Suffix);

    /// <summary>The templates, by name: {id, name, at, size}.</summary>
    public JsonArray List()
    {
        var list = new JsonArray();
        if (!Directory.Exists(Folder)) return list;
        foreach (var f in Directory.GetFiles(Folder, "*" + LapinPackage.Suffix).Order(StringComparer.CurrentCultureIgnoreCase))
        {
            var info = new FileInfo(f);
            var id = Path.GetFileNameWithoutExtension(f);
            list.Add((JsonNode)new JsonObject { ["id"] = id, ["name"] = id, ["at"] = info.LastWriteTimeUtc.ToString("o"), ["size"] = info.Length });
        }
        return list;
    }

    public bool Has(string id) => File.Exists(PathOf(id));

    /// <summary>A template's package.</summary>
    public byte[] Bytes(string id) => File.Exists(PathOf(id)) ? File.ReadAllBytes(PathOf(id)) : throw new CommandException($"There is no template „{id}”.");

    /// <summary>
    /// A template kept under <paramref name="name"/>: its id; null when one has that name and <paramref name="overwrite"/> is
    /// not asked (nothing written). Written beside, then moved over, so a template is never half written.
    /// </summary>
    public string? Keep(string name, byte[] bytes, bool overwrite)
    {
        var id = IdOf(name);
        var path = PathOf(id);
        if (File.Exists(path) && !overwrite) return null;
        Directory.CreateDirectory(Folder);
        var temp = path + ".part";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
        return id;
    }

    /// <summary>A file (a template exported, or any project) as a template's package: its set-up, its model left out.</summary>
    public static byte[] FromFile(Session session, byte[] bytes)
    {
        var content = Read(bytes);
        var store = new ModelStore();
        new History(store).Load(content.Entities, content.Relations);
        var (entities, relations) = ProjectTemplates.Strip(store, session.Catalog);
        if (!entities.Any(e => e.Class == IfcClass.BuildingStorey)) throw new CommandException("The file has no storey: it is not a Lapin project.");
        string ModuleOf(string cls) => session.Catalog.OwnerOf(cls) ?? content.Classes.GetValueOrDefault(cls, "unknown");
        return Write(session, entities, relations, ModuleOf);
    }

    /// <summary>A file (a template exported, or any project) taken into the library, its model left out.</summary>
    public string? Import(Session session, string name, byte[] bytes, bool overwrite) => Keep(name, FromFile(session, bytes), overwrite);

    public void Delete(string id)
    {
        var path = PathOf(id);
        if (!File.Exists(path)) throw new CommandException($"There is no template „{id}”.");
        File.Delete(path);
    }

    /// <summary>A template renamed: its new id; refused when another has the name.</summary>
    public string Rename(string id, string name)
    {
        var from = PathOf(id);
        if (!File.Exists(from)) throw new CommandException($"There is no template „{id}”.");
        var to = IdOf(name);
        if (to == IdOf(id)) return to;
        if (File.Exists(PathOf(to))) throw new CommandException($"A template is already called „{to}”.");
        File.Move(from, PathOf(to));
        return to;
    }
}
