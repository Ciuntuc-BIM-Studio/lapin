using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>
/// My furniture library (docs/plan-furniture.md, Configuratorul flexibil, stage 3; decision 476): the furniture types a user
/// keeps on this computer to use in any project, as project templates are kept (decision 470). On the desktop each entry is a
/// JSON file in the user's folder (<c>&lt;user-dir&gt;/furniture/&lt;id&gt;.json</c>); the browser keeps the same entries in
/// its IndexedDB and asks the engine only to check, pack and unpack them (<see cref="Check"/>, <see cref="Pack"/>,
/// <see cref="Unpack"/>), so a file written by one is read by the other.
/// <para>
/// An entry: <c>{id, name, category, tags, article?, price?, thumbnail?, at, type}</c>. <c>type</c> is the furniture type as
/// the furniture module gives it (<c>furniture.libraryItem</c>: its spec, IFC type and materials) and takes it back
/// (<c>furniture.insertFromLibrary</c>); the library keeps it as it is, so a kind added later (a mesh-based piece) is kept too.
/// The price is the user's own, indicative, in the project's currency; the thumbnail an image as a data URL (the engine's
/// preview drawn by the interface).
/// </para>
/// <para>
/// The file a library goes out as (<c>.lapinlib</c>): UTF-8 JSON, <c>{"format": "lapin-furniture-library", "version": 1,
/// "items": [entry…]}</c>, one entry or the whole library. Read back, an entry whose id is already there is skipped when it is
/// the same, and comes in under a new id when it is not.
/// </para>
/// </summary>
public sealed class FurnitureLibrary(string folder)
{
    public const string Format = "lapin-furniture-library";
    public const int Version = 1;
    public const string Suffix = ".lapinlib";

    /// <summary>The categories an entry is filed under; any other is filed under "other".</summary>
    public static readonly string[] Categories = ["kitchen", "living", "bedroom", "bathroom", "office", "storage", "other"];

    /// <summary>The largest thumbnail kept (a data URL's length).</summary>
    public const int MaxThumbnail = 512 * 1024;

    public string Folder { get; } = folder;

    // ---- entries ----

    /// <summary>
    /// An entry as the library keeps it: its name trimmed (one is needed), a known category, its tags trimmed and once each,
    /// an article code of at most 64 characters, a price that is a number not below 0, a thumbnail that is an image's data URL,
    /// and the type it holds. An id is given when it has none.
    /// </summary>
    public static JsonObject Check(JsonObject given)
    {
        var name = ((string?)(given["name"] as JsonValue))?.Trim();
        if (string.IsNullOrEmpty(name)) throw new CommandException("Give the furniture a name.");
        if (name.Length > 120) name = name[..120].Trim();
        var type = given["type"] as JsonObject ?? throw new CommandException("The library entry holds no furniture type.");
        if (type["spec"] is not JsonObject) throw new CommandException("The library entry's furniture type has no spec.");
        var category = (string?)(given["category"] as JsonValue) is { } c && Categories.Contains(c) ? c : "other";
        var tags = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (given["tags"] is JsonArray ts)
            foreach (var t in ts)
                if (((string?)(t as JsonValue))?.Trim() is { Length: > 0 } tag && tag.Length <= 40 && seen.Add(tag)) tags.Add((JsonNode)tag);
        var o = new JsonObject
        {
            ["id"] = (string?)(given["id"] as JsonValue) is { } id && IsId(id) ? id : NewId(),
            ["name"] = name,
            ["category"] = category,
            ["tags"] = tags,
        };
        if (((string?)(given["article"] as JsonValue))?.Trim() is { Length: > 0 } article) o["article"] = article.Length > 64 ? article[..64] : article;
        if (Json.Number(given["price"]) is { } price)
        {
            if (!double.IsFinite(price) || price < 0) throw new CommandException("A price is a number, 0 or more.");
            o["price"] = Math.Round(price, 2);
        }
        if ((string?)(given["thumbnail"] as JsonValue) is { Length: > 0 } thumb)
        {
            if (!thumb.StartsWith("data:image/", StringComparison.Ordinal) || thumb.Length > MaxThumbnail) throw new CommandException("A thumbnail is an image of at most 512 KB.");
            o["thumbnail"] = thumb;
        }
        o["at"] = (string?)(given["at"] as JsonValue) is { } at && DateTime.TryParse(at, null, System.Globalization.DateTimeStyles.RoundtripKind, out _) ? at : DateTime.UtcNow.ToString("o");
        o["type"] = type.DeepClone();
        return o;
    }

    static string NewId() => Guid.NewGuid().ToString("N");

    /// <summary>An id is what a file can be named: letters, digits, - and _, at most 64.</summary>
    static bool IsId(string id) => id.Length is > 0 and <= 64 && id.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');

    // ---- the file ----

    /// <summary>Entries as a library file's bytes.</summary>
    public static byte[] Pack(IEnumerable<JsonObject> entries)
    {
        var doc = new JsonObject
        {
            ["format"] = Format,
            ["version"] = Version,
            ["items"] = new JsonArray([.. entries.Select(e => (JsonNode)Check(e))]),
        };
        return Encoding.UTF8.GetBytes(doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>A library file's entries, each checked; a file that is not one is refused, and so is one from a newer Lapin.</summary>
    public static List<JsonObject> Unpack(byte[] bytes)
    {
        JsonNode? doc;
        try { doc = JsonNode.Parse(bytes); }
        catch (JsonException e) { throw new CommandException($"This is not a Lapin furniture library ({e.Message})."); }
        if (doc is not JsonObject o || (string?)(o["format"] as JsonValue) != Format) throw new CommandException("This is not a Lapin furniture library.");
        if (Json.Number(o["version"]) is not { } v || v > Version) throw new CommandException("This furniture library was written by a newer Lapin: update Lapin to read it.");
        if (o["items"] is not JsonArray items) throw new CommandException("The furniture library has no items.");
        return [.. items.OfType<JsonObject>().Select(Check)];
    }

    // ---- on this computer ----

    string PathOf(string id) => IsId(id) ? Path.Combine(Folder, id + ".json") : throw new CommandException("There is no such furniture in your library.");

    /// <summary>Every entry, by name.</summary>
    public List<JsonObject> List()
    {
        var list = new List<JsonObject>();
        if (!Directory.Exists(Folder)) return list;
        foreach (var f in Directory.GetFiles(Folder, "*.json"))
        {
            try
            {
                if (JsonNode.Parse(File.ReadAllBytes(f)) is JsonObject o) list.Add(Check(o));
            }
            // A file that is not an entry (one half copied by hand) is left where it is, unlisted.
            catch (Exception e) when (e is JsonException or CommandException or IOException) { }
        }
        return [.. list.OrderBy(e => (string?)e["name"], StringComparer.CurrentCultureIgnoreCase)];
    }

    public JsonObject Get(string id)
    {
        var path = PathOf(id);
        if (!File.Exists(path)) throw new CommandException("There is no such furniture in your library.");
        return Check(JsonNode.Parse(File.ReadAllBytes(path)) as JsonObject ?? throw new CommandException("The library entry is damaged."));
    }

    /// <summary>An entry kept: its id. Written beside, then moved over, so an entry is never half written.</summary>
    public string Keep(JsonObject entry)
    {
        var e = Check(entry);
        var id = (string)e["id"]!;
        Directory.CreateDirectory(Folder);
        var path = PathOf(id);
        var temp = path + ".part";
        File.WriteAllText(temp, e.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
        return id;
    }

    /// <summary>A new entry: under a new id whatever it says.</summary>
    public string Add(JsonObject entry)
    {
        var e = (JsonObject)entry.DeepClone();
        e["id"] = NewId();
        e["at"] = DateTime.UtcNow.ToString("o");
        return Keep(e);
    }

    /// <summary>An entry changed: its name, category, tags, article and price as given (null: taken off); its type stays.</summary>
    public JsonObject Update(string id, JsonObject patch)
    {
        var e = Get(id);
        foreach (var k in new[] { "name", "category", "tags", "article", "price", "thumbnail" })
            if (patch.ContainsKey(k)) { if (patch[k] is null) e.Remove(k); else e[k] = patch[k]!.DeepClone(); }
        Keep(e);
        return Get(id);
    }

    /// <summary>A copy under a new id, named "… copy".</summary>
    public string Duplicate(string id)
    {
        var e = Get(id);
        e["name"] = (string?)e["name"] + " copy";
        return Add(e);
    }

    public void Delete(string id)
    {
        var path = PathOf(id);
        if (!File.Exists(path)) throw new CommandException("There is no such furniture in your library.");
        File.Delete(path);
    }

    /// <summary>The entries asked for (none: all of them) as a library file.</summary>
    public byte[] Export(IEnumerable<string>? ids)
    {
        var all = List();
        var wanted = ids?.ToHashSet();
        return Pack(wanted == null ? all : all.Where(e => wanted.Contains((string)e["id"]!)));
    }

    /// <summary>
    /// A library file taken in: the ids added, and how many were already here as they are. An entry whose id is here with
    /// something else in it comes in under a new id, so nothing kept is overwritten.
    /// </summary>
    public (List<string> Added, int Skipped) Import(byte[] bytes)
    {
        var added = new List<string>();
        var skipped = 0;
        foreach (var e in Unpack(bytes))
        {
            var id = (string)e["id"]!;
            if (File.Exists(PathOf(id)))
            {
                if (Same(Get(id), e)) { skipped++; continue; }
                e["id"] = NewId();
            }
            added.Add(Keep(e));
        }
        return (added, skipped);
    }

    /// <summary>Two entries holding the same: their name, category, tags, article, price, thumbnail and type (not when they were kept).</summary>
    public static bool Same(JsonObject a, JsonObject b)
    {
        static JsonObject Body(JsonObject e) { var c = (JsonObject)e.DeepClone(); c.Remove("at"); c.Remove("id"); return c; }
        return JsonNode.DeepEquals(Body(a), Body(b));
    }
}
