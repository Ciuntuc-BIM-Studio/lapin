using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Cde.References;

/// <summary>
/// One file of a reference document: its name, SHA-256, size and type; whether the project's file holds its bytes
/// (<see cref="Held"/>: under <c>references/blobs/</c>; a file above the keep limit is not, its hash and size staying so
/// the server's or the project folder's copy is known), and where it was added from when it is not held (<see cref="Source"/>).
/// </summary>
public sealed record ReferenceFile(string Name, string Sha256, long Size, string? Mime, bool Held, string? Source = null)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["name"] = Name, ["sha256"] = Sha256, ["size"] = Size, ["held"] = Held };
        if (Mime != null) o["mime"] = Mime;
        if (Source != null) o["source"] = Source;
        return o;
    }

    public static ReferenceFile? From(JsonNode? n) => n is JsonObject o && (string?)o["name"] is { Length: > 0 } name && (string?)o["sha256"] is { Length: 64 } sha
        ? new ReferenceFile(name, sha, (long)(Json.Number(o["size"]) ?? 0), (string?)o["mime"], o["held"] is JsonValue h && h.TryGetValue<bool>(out var held) && held, (string?)o["source"])
        : null;
}

/// <summary>
/// The documents a project receives (docs/plan-cde-references.md, decision 874): urbanism certificates, permits, land-book
/// extracts, studies, a consultant's IFC. Each is a <c>LapinReference</c> entity (a resource, never exported to IFC) with its
/// metadata (type, issuer, number, dates, phase, discipline, status), one folder of the project's folder tree, its files (the
/// bytes in the project's file, outside the project state: <see cref="IReferenceFiles"/>), a certificate's checklist of the
/// permits it asks for, and links. A register beside the issues, not a container: nothing moves between WIP and Published.
/// Nothing is deleted in the normal course: a document is withdrawn (with a reason) or superseded (by its newer one).
/// <c>expired</c> is derived from <c>validUntil</c> on every ask, never kept. The folder tree, the types and the keep limit
/// are the project's (<c>references</c> on the IfcProject), the built-in defaults (<see cref="ReferenceDefaults"/>) until it
/// changes them.
/// </summary>
public static class References
{
    public const string Class = "LapinReference";
    public const string Key = "references";

    /// <summary>A file larger than this is not kept in the project's file by default (question R-Q1): 50 MB.</summary>
    public const long DefaultKeepLimit = 50L * 1024 * 1024;

    /// <summary>The statuses kept; <c>expired</c> is not one of them (derived from <c>validUntil</c>).</summary>
    public static readonly string[] Statuses = ["requested", "received", "superseded", "withdrawn"];

    // ------------------------------------------------------------ the settings

    /// <summary>The project's settings as kept (<c>references</c> on the IfcProject); empty when it has its defaults.</summary>
    public static JsonObject Settings(IModelView m) => Spatial.Project(m)?.Attrs[Key] as JsonObject ?? [];

    public static JsonArray Folders(IModelView m) => Settings(m)["folders"] is JsonArray a ? (JsonArray)a.DeepClone() : ReferenceDefaults.Folders();

    public static JsonArray Types(IModelView m) => Settings(m)["types"] is JsonArray a ? (JsonArray)a.DeepClone() : ReferenceDefaults.Types();

    /// <summary>Each type's folder: the project's mapping, else each type's own <c>folder</c>.</summary>
    public static JsonObject TypeFolders(IModelView m) => Settings(m)["typeFolders"] is JsonObject o ? (JsonObject)o.DeepClone() : ReferenceDefaults.TypeFolders(Types(m));

    /// <summary>The issued deliverables' roles (A, S, E…) to folders, for the derived view (docs/plan-cde-references.md §2); none by default.</summary>
    public static JsonObject RoleFolders(IModelView m) => Settings(m)["roleFolders"] is JsonObject o ? (JsonObject)o.DeepClone() : [];

    /// <summary>The certificate's checklist items offered (§3.3): the project's list, else the indicative defaults.</summary>
    public static JsonArray ChecklistOptions(IModelView m) => Settings(m)["checklist"] is JsonArray a ? (JsonArray)a.DeepClone() : ReferenceDefaults.Checklist();

    public static long KeepLimit(IModelView m) => Json.Number(Settings(m)["keepLimit"]) is { } v && v >= 0 ? (long)v : DefaultKeepLimit;

    /// <summary>The language the folders are named in on disk (<c>ro</c> or <c>en</c>; the project's choice, English by default).</summary>
    public static string Language(IModelView m) => (string?)Settings(m)["language"] is { Length: > 0 } l ? l : "en";

    /// <summary>The folder under the project folder the documents' copies go into (question R-Q2): <c>Reference</c> by default.</summary>
    public static string Root(IModelView m) => (string?)Settings(m)["root"] is { Length: > 0 } r ? r : "Reference";

    static void WriteSettings(ICommandContext ctx, Action<JsonObject> change)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        var settings = (attrs[Key] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
        change(settings);
        attrs[Key] = settings;
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>One folder of the tree: its node, its parent's id, and its names from the top (in each language).</summary>
    public sealed record FolderInfo(string Id, JsonObject Node, string? Parent, IReadOnlyList<JsonObject> Names)
    {
        public string? Derived => (string?)Node["derived"];
        public string Name(string lang) => NameOf(Node["name"], lang);
        public string Path(string lang) => string.Join(" / ", Names.Select(n => NameOf(n, lang)));
    }

    /// <summary>A folder's or a type's name in a language: that one, else English, else Romanian, else any.</summary>
    public static string NameOf(JsonNode? name, string lang) => name switch
    {
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonObject o => (string?)o[lang] ?? (string?)o["en"] ?? (string?)o["ro"] ?? o.Select(kv => (string?)kv.Value).FirstOrDefault(x => x != null) ?? "",
        _ => "",
    };

    /// <summary>Every folder of a tree by id, in the tree's order.</summary>
    public static Dictionary<string, FolderInfo> Index(JsonArray tree)
    {
        var o = new Dictionary<string, FolderInfo>(StringComparer.Ordinal);
        void Walk(JsonArray nodes, string? parent, List<JsonObject> names, int depth)
        {
            if (depth > 16) throw new CommandException("The folder tree is deeper than 16 levels.");
            foreach (var n in nodes)
            {
                if (n is not JsonObject node || (string?)node["id"] is not { Length: > 0 } id) throw new CommandException("Each folder has an id.");
                var name = node["name"] is JsonObject no ? no : new JsonObject { ["en"] = NameOf(node["name"], "en") };
                if (NameOf(name, "en").Trim().Length == 0) throw new CommandException($"The folder {id} has no name.");
                var path = new List<JsonObject>(names) { name };
                if (!o.TryAdd(id, new FolderInfo(id, node, parent, path))) throw new CommandException($"Two folders have the id {id}.");
                Walk(node["children"] as JsonArray ?? [], id, path, depth + 1);
            }
        }
        Walk(tree, null, [], 0);
        return o;
    }

    static JsonObject? TypeOf(IModelView m, string? id) => id == null ? null : Types(m).OfType<JsonObject>().FirstOrDefault(t => (string?)t["id"] == id);

    // ------------------------------------------------------------ the documents

    public static bool Any(IModelView m) => m.OfClass(Class).Any();

    public static IEnumerable<Entity> All(IModelView m) => m.OfClass(Class);

    public static List<ReferenceFile> Files(Entity e) => e.Attrs["files"] is JsonArray a ? a.Select(ReferenceFile.From).OfType<ReferenceFile>().ToList() : [];

    /// <summary>Every hash a document keeps in the project's file: what a save writes under <c>references/blobs/</c>, once each.</summary>
    public static HashSet<string> HeldHashes(IModelView m) => All(m).SelectMany(Files).Where(f => f.Held).Select(f => f.Sha256).ToHashSet(StringComparer.Ordinal);

    static Entity Require(IModelView m, string? id) => m.Get(id ?? "") is { Class: Class } e ? e : throw new CommandException("There is no such reference document.");

    static string Today(JsonObject a) => ReferenceDates.Parse((string?)a["today"]) ?? ReferenceDates.Iso8601(DateOnly.FromDateTime(DateTime.Now));

    /// <summary>The status as shown: the one kept, and <c>expired</c> for a received document past its <c>validUntil</c>.</summary>
    public static string StatusOf(Entity e, string today)
    {
        var status = e.Str("status") ?? "received";
        return status == "received" && e.Str("validUntil") is { } until && string.CompareOrdinal(until, today) < 0 ? "expired" : status;
    }

    static string Author(JsonObject a) => ((string?)a["author"])?.Trim() is { Length: > 0 } s ? s : "Lapin";

    static string Now() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>A document as the interface and the generator read it: its fields, its folder's path, its status as shown.</summary>
    public static JsonObject ToJson(IModelView m, Entity e, string today, Dictionary<string, FolderInfo>? folders = null)
    {
        folders ??= Index(Folders(m));
        var lang = Language(m);
        var o = new JsonObject { ["id"] = e.Id, ["title"] = e.Name };
        foreach (var (k, v) in e.Attrs) o[k] = v?.DeepClone();
        o["status"] = StatusOf(e, today);
        o["expired"] = StatusOf(e, today) == "expired";
        if (e.Str("folder") is { } f && folders.TryGetValue(f, out var info)) o["folderPath"] = info.Path(lang);
        if (TypeOf(m, e.Str("type")) is { } t) o["typeName"] = NameOf(t["name"], lang);
        var by = All(m).Where(x => x.Str("supersededBy") == e.Id).Select(x => (JsonNode)x.Id).ToArray();
        if (by.Length > 0) o["supersedes"] = new JsonArray(by);
        return o;
    }

    /// <summary>The documents and their files as <c>references/index.json</c> lists them, for whoever opens the package without Lapin (Lapin reads the records).</summary>
    public static JsonObject Index(IModelView m)
    {
        var folders = Index(Folders(m));
        var lang = Language(m);
        return new JsonObject
        {
            ["format"] = "lapin-references/1",
            ["blobs"] = "references/blobs/<sha256>",
            ["documents"] = new JsonArray([.. All(m).Select(e => (JsonNode)new JsonObject
            {
                ["id"] = e.Id, ["title"] = e.Name, ["type"] = e.Str("type"), ["issuer"] = e.Str("issuer"), ["number"] = e.Str("number"), ["date"] = e.Str("date"),
                ["validUntil"] = e.Str("validUntil"), ["status"] = e.Str("status"),
                ["folder"] = e.Str("folder") is { } f && folders.TryGetValue(f, out var info) ? info.Path(lang) : e.Str("folder"),
                ["files"] = new JsonArray([.. Files(e).Select(x => (JsonNode)new JsonObject { ["name"] = x.Name, ["sha256"] = x.Sha256, ["size"] = x.Size, ["held"] = x.Held })]),
            })]),
        };
    }

    // ------------------------------------------------------------ fields

    /// <summary>
    /// The fields a command sets, over <paramref name="attrs"/>: a key absent leaves its field, an empty string (or null) clears it.
    /// Dates are kept as ISO days; the type must be one of the project's; the folder one of its tree's (not a derived one).
    /// </summary>
    static void Apply(IModelView m, JsonObject attrs, JsonObject a, Dictionary<string, FolderInfo> folders, string? selfId)
    {
        bool Clears(string key) => a[key] is null || a[key] is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim().Length == 0;
        if (a.ContainsKey("type"))
        {
            if (Clears("type")) attrs.Remove("type");
            else
            {
                var type = ((string?)a["type"])!.Trim();
                if (TypeOf(m, type) == null) throw new CommandException($"There is no document type {type} in the project's list.");
                attrs["type"] = type;
            }
        }
        // Named one by one (not looped over a list) so the command catalogue sees every argument.
        foreach (var (key, value) in new[] { ("issuer", (string?)a["issuer"]), ("number", (string?)a["number"]), ("discipline", (string?)a["discipline"]) })
        {
            if (!a.ContainsKey(key)) continue;
            if (Clears(key)) attrs.Remove(key); else attrs[key] = value!.Trim();
        }
        foreach (var (key, value) in new[] { ("date", (string?)a["date"]), ("validFrom", (string?)a["validFrom"]), ("validUntil", (string?)a["validUntil"]) })
        {
            if (!a.ContainsKey(key)) continue;
            if (Clears(key)) { attrs.Remove(key); continue; }
            attrs[key] = ReferenceDates.Parse(value) ?? throw new CommandException($"{key}: a date is written 2026-11-25 or 25.11.2026.");
        }
        if (a.ContainsKey("phase"))
        {
            var phases = a["phase"] switch
            {
                JsonArray list => list.Select(x => ((string?)x)?.Trim()).OfType<string>().Where(x => x.Length > 0).Distinct().ToList(),
                JsonValue v when v.TryGetValue<string>(out var s) => s.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                _ => [],
            };
            if (phases.Count == 0) attrs.Remove("phase"); else attrs["phase"] = new JsonArray([.. phases.Select(x => (JsonNode)x)]);
        }
        if (a.ContainsKey("folder"))
        {
            if (Clears("folder")) attrs.Remove("folder");
            else attrs["folder"] = CheckFolder(folders, (string?)a["folder"]);
        }
        if (a.ContainsKey("requires"))
        {
            if (a["requires"] is not JsonArray list || list.Count == 0) attrs.Remove("requires");
            else attrs["requires"] = Requires(m, list, selfId);
        }
        if (a.ContainsKey("relatesTo"))
        {
            var ids = (a["relatesTo"] as JsonArray ?? []).Select(x => ((string?)x)?.Trim()).OfType<string>().Where(x => x.Length > 0 && x != selfId).Distinct().ToList();
            if (ids.Count == 0) attrs.Remove("relatesTo"); else attrs["relatesTo"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]);
        }
        if (a["fields"] is JsonObject fields)
        {
            var kept = attrs["fields"] as JsonObject ?? new JsonObject();
            foreach (var (k, v) in fields)
            {
                if (v is null || v is JsonValue sv && sv.TryGetValue<string>(out var s) && s.Length == 0) kept.Remove(k);
                else kept[k] = v.DeepClone();
            }
            if (kept.Count == 0) attrs.Remove("fields"); else attrs["fields"] = kept;
        }
        if (a.ContainsKey("status"))
        {
            var status = ((string?)a["status"])?.Trim();
            if (status is not ("requested" or "received")) throw new CommandException("A document is requested or received here: superseding and withdrawing have their own commands.");
            attrs["status"] = status;
        }
    }

    static string CheckFolder(Dictionary<string, FolderInfo> folders, string? id)
    {
        if (id == null || !folders.TryGetValue(id, out var f)) throw new CommandException($"There is no folder {id} in the project's tree.");
        if (f.Derived != null) throw new CommandException($"„{f.Name("en")}” shows what the project issued: it holds no reference documents.");
        return id;
    }

    /// <summary>A certificate's checklist as given: each item a key (a default's, or made from its label), a label, and the document that answers it.</summary>
    static JsonArray Requires(IModelView m, JsonArray list, string? selfId)
    {
        var defaults = ChecklistOptions(m).OfType<JsonObject>().Where(x => (string?)x["key"] != null).GroupBy(x => (string)x["key"]!).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var o = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in list)
        {
            JsonObject item = n switch
            {
                JsonObject x => x,
                JsonValue v when v.TryGetValue<string>(out var s) => new JsonObject { ["key"] = s },
                _ => throw new CommandException("A checklist item is a key, or {key, label, document}."),
            };
            var label = item["label"]?.DeepClone();
            var key = ((string?)item["key"])?.Trim() is { Length: > 0 } k ? k : Slug(NameOf(label, "en"));
            if (key.Length == 0) throw new CommandException("A checklist item has a key or a label.");
            if (!seen.Add(key)) throw new CommandException($"The checklist names {key} twice.");
            if (label == null || NameOf(label, "en").Length == 0) label = defaults.TryGetValue(key, out var d) ? d["label"]!.DeepClone() : (JsonNode)key;
            var entry = new JsonObject { ["key"] = key, ["label"] = label };
            if (((string?)item["document"])?.Trim() is { Length: > 0 } doc)
            {
                if (doc == selfId || m.Get(doc) is not { Class: Class }) throw new CommandException($"The checklist item {key} names a document that is not a reference document of this project.");
                entry["document"] = doc;
            }
            o.Add((JsonNode)entry);
        }
        return o;
    }

    /// <summary>A key made from a label: lowercase ASCII, Romanian letters without their marks (by hand: a trimmed engine may not normalise), dashes between words.</summary>
    static string Slug(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in s.ToLowerInvariant())
        {
            var plain = c switch { 'ă' or 'â' or 'á' or 'à' or 'ä' => 'a', 'î' or 'í' => 'i', 'ș' or 'ş' => 's', 'ț' or 'ţ' => 't', 'é' or 'è' or 'ë' => 'e', 'ó' or 'ö' or 'ő' => 'o', 'ú' or 'ü' or 'ű' => 'u', _ => c };
            sb.Append(char.IsAsciiLetterOrDigit(plain) ? plain : '-');
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
    }

    // ------------------------------------------------------------ files

    /// <summary>A file a command brings: its name and bytes (base64 in <c>data</c> or <c>bytes</c>, or read from <c>path</c> on a desktop host).</summary>
    static (string Name, byte[] Bytes, string? Path, string? Mime) Incoming(JsonNode? n)
    {
        if (n is not JsonObject f) throw new CommandException("A file is {name, data} (base64), or {path} on this computer.");
        var data = (string?)f["data"] ?? (string?)f["bytes"];
        var name = ((string?)f["name"])?.Trim();
        if (data != null)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(data); }
            catch (FormatException) { throw new CommandException("A file's content is not base64."); }
            if (string.IsNullOrEmpty(name)) throw new CommandException("A file has a name.");
            return (System.IO.Path.GetFileName(name), bytes, null, (string?)f["mime"]);
        }
        if ((string?)f["path"] is { Length: > 0 } p)
        {
            var path = HostFiles.Check(p);
            if (!File.Exists(path)) throw new CommandException($"There is no file at {path}.");
            return (string.IsNullOrEmpty(name) ? System.IO.Path.GetFileName(path) : System.IO.Path.GetFileName(name), File.ReadAllBytes(path), path, (string?)f["mime"]);
        }
        throw new CommandException("A file is {name, data} (base64), or {path} on this computer.");
    }

    public static string MimeOf(string name) => System.IO.Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".tif" or ".tiff" => "image/tiff",
        ".ifc" => "application/x-step",
        ".dxf" => "image/vnd.dxf",
        ".dwg" => "image/vnd.dwg",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".odt" => "application/vnd.oasis.opendocument.text",
        ".txt" => "text/plain",
        ".zip" => "application/zip",
        _ => "application/octet-stream",
    };

    /// <summary>
    /// The files given, kept: each hashed, its bytes put in the store (once per hash) unless larger than the keep limit; a name
    /// already used by the document gets a number. Gives back the records and the bytes (for the project folder's copy).
    /// </summary>
    static List<(ReferenceFile File, byte[] Bytes)> Keep(ICommandContext ctx, JsonNode? files, IReadOnlyCollection<string> taken, List<string> warnings)
    {
        var store = ctx.ReferenceFiles ?? throw new CommandException("This host keeps no reference documents' files.");
        var limit = KeepLimit(ctx.Model);
        var names = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        var o = new List<(ReferenceFile, byte[])>();
        foreach (var n in files as JsonArray ?? [])
        {
            var (name, bytes, path, mime) = Incoming(n);
            var unique = name;
            for (var i = 2; !names.Add(unique); i++) unique = $"{System.IO.Path.GetFileNameWithoutExtension(name)} ({i}){System.IO.Path.GetExtension(name)}";
            var sha = Hashes.Sha256Hex(bytes);
            var held = bytes.LongLength <= limit;
            if (held) store.Put(bytes);
            else warnings.Add($"{unique} ({bytes.LongLength / 1048576.0:0.#} MB) is larger than the project keeps ({limit / 1048576.0:0.#} MB): its hash and size are kept, its file stays {(path != null ? "where it is" : "in the project folder or on the server")}.");
            o.Add((new ReferenceFile(unique, sha, bytes.LongLength, mime ?? MimeOf(unique), held, held ? null : path), bytes));
        }
        return o;
    }

    /// <summary>The bytes of a document's file: the project's file's, else the project folder's copy, else where it was added from (each checked against its hash).</summary>
    public static (ReferenceFile File, byte[] Bytes) Bytes(IQueryContext q, Entity e, string? name)
    {
        var files = Files(e);
        var file = (name is { Length: > 0 } ? files.FirstOrDefault(f => f.Name == name) : files.FirstOrDefault())
                   ?? throw new CommandException(files.Count == 0 ? "This document has no file (it is requested)." : $"The document has no file {name}.");
        if (file.Held && q.ReferenceFiles is { } store && store.Has(file.Sha256)) return (file, store.Get(file.Sha256));
        foreach (var path in new[] { ReferenceDisk.CopyPath(q.Model, e.Str("folder"), file.Name), file.Source }.OfType<string>())
        {
            try
            {
                if (!File.Exists(path)) continue;
                var bytes = File.ReadAllBytes(path);
                if (Hashes.Sha256Hex(bytes) == file.Sha256) return (file, bytes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        throw new CommandException(file.Held
            ? "The project's file no longer holds this document's file."
            : $"{file.Name} is larger than the project keeps: its copy is not in the project folder here, nor where it was added from. Its SHA-256 is {file.Sha256}.");
    }

    /// <summary>The pages asked: a list of numbers, or a range in text ("1-3", "2,5").</summary>
    static HashSet<int>? Pages(JsonNode? n)
    {
        switch (n)
        {
            case null: return null;
            case JsonArray a: return [.. a.Select(x => Json.Number(x)).OfType<double>().Select(x => (int)x)];
            case JsonValue v when v.TryGetValue<int>(out var one): return [one];
            case JsonValue v when v.TryGetValue<string>(out var s):
                var o = new HashSet<int>();
                foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var r = part.Split('-', StringSplitOptions.TrimEntries);
                    if (r.Length == 2 && int.TryParse(r[0], out var lo) && int.TryParse(r[1], out var hi) && hi >= lo && hi - lo < 10_000) for (var i = lo; i <= hi; i++) o.Add(i);
                    else if (int.TryParse(part, out var p)) o.Add(p);
                    else throw new CommandException("Pages are numbers or ranges: 1-3, 5.");
                }
                return o;
            default: return null;
        }
    }

    /// <summary>A file's text by pages: a PDF's text layer, a plain text file as it is; nothing for other files.</summary>
    public static JsonObject Text(IQueryContext q, Entity e, string? name, JsonNode? pages)
    {
        var (file, bytes) = Bytes(q, e, name);
        var o = new JsonObject { ["name"] = file.Name };
        if (file.Mime == "text/plain" || file.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            o["pageCount"] = 1;
            o["pages"] = new JsonArray(new JsonObject { ["page"] = 1, ["text"] = System.Text.Encoding.UTF8.GetString(bytes) });
            return o;
        }
        if (!(file.Mime == "application/pdf" || file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            o["pageCount"] = 0;
            o["pages"] = new JsonArray();
            o["warning"] = "Only a PDF's or a text file's text is read.";
            return o;
        }
        var r = PdfRemarks.PdfPageText.Read(bytes, Pages(pages));
        o["pageCount"] = r.PageCount;
        o["pages"] = new JsonArray([.. r.Pages.Select(p => (JsonNode)new JsonObject { ["page"] = p.Page, ["text"] = p.Text })]);
        if (r.Unreadable != null) o["warning"] = $"No text: {r.Unreadable}. No suggestions can be made from it.";
        return o;
    }

    // ------------------------------------------------------------ the checklist

    /// <summary>
    /// A certificate's checklist with each item's state: <c>missing</c> (no document answers it, or the one that did was withdrawn),
    /// <c>requested</c>, <c>received</c> or <c>expired</c>, read from the document it names, or from that one's newest successor when
    /// it was superseded. The defaults not ticked yet come with it (<c>options</c>).
    /// </summary>
    public static JsonObject Checklist(IModelView m, Entity e, string today)
    {
        var lang = Language(m);
        var items = new JsonArray();
        var chosen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in (e.Attrs["requires"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var key = (string?)item["key"] ?? "";
            chosen.Add(key);
            var o = new JsonObject { ["key"] = key, ["label"] = item["label"]?.DeepClone(), ["text"] = NameOf(item["label"], lang) };
            var state = "missing";
            if ((string?)item["document"] is { } docId && Latest(m, docId) is { } doc)
            {
                o["document"] = doc.Id;
                if (doc.Id != docId) o["answeredBy"] = docId;
                state = StatusOf(doc, today) switch { "received" => "received", "expired" => "expired", "requested" => "requested", _ => "missing" };
                o["number"] = doc.Str("number");
                o["date"] = doc.Str("date");
                o["issuer"] = doc.Str("issuer");
                o["validUntil"] = doc.Str("validUntil");
            }
            o["state"] = state;
            items.Add((JsonNode)o);
        }
        var options = ChecklistOptions(m).OfType<JsonObject>().Where(x => (string?)x["key"] is { } k && !chosen.Contains(k)).Select(x =>
        {
            var o = (JsonObject)x.DeepClone();
            o["text"] = NameOf(o["label"], lang);
            return (JsonNode)o;
        }).ToList();
        return new JsonObject
        {
            ["id"] = e.Id, ["items"] = items, ["options"] = new JsonArray([.. options]),
            ["missing"] = items.Count(x => (string?)x!["state"] is "missing" or "expired"),
            ["complete"] = items.Count > 0 && items.All(x => (string?)x!["state"] == "received"),
        };
    }

    /// <summary>The document, or the newest one that superseded it; null when it is gone.</summary>
    static Entity? Latest(IModelView m, string id)
    {
        var e = m.Get(id) is { Class: Class } x ? x : null;
        for (var guard = 0; e != null && e.Str("supersededBy") is { } next && guard < 32; guard++)
            e = m.Get(next) is { Class: Class } n ? n : e;
        return e;
    }

    // ------------------------------------------------------------ the folders with their counts

    /// <summary>
    /// The folder tree with each folder's count (its own documents, and with its subfolders'): the documents' folders, and for the
    /// derived folders what the project issued (the written documents and the sheets, each once however many issues sent it).
    /// </summary>
    public static JsonObject FolderTree(IModelView m)
    {
        var tree = Folders(m);
        var index = Index(tree);
        var lang = Language(m);
        var own = All(m).GroupBy(e => e.Str("folder") ?? "").ToDictionary(g => g.Key, g => g.Count());
        var issues = CdeIssues.All(m);
        var issuedSheets = issues.SelectMany(i => i.Sheets).Distinct().Count();
        var issuedDocuments = issues.SelectMany(i => i.Files).Select(f => f.DocumentId).OfType<string>().Distinct().Count();
        int Count(JsonObject node)
        {
            var id = (string)node["id"]!;
            var n = (string?)node["derived"] switch { "sheets" => issuedSheets, "documents" => issuedDocuments, _ => own.GetValueOrDefault(id) };
            node["count"] = n;
            node["text"] = NameOf(node["name"], lang);
            var total = n;
            foreach (var child in (node["children"] as JsonArray ?? []).OfType<JsonObject>()) total += Count(child);
            node["total"] = total;
            return total;
        }
        foreach (var node in tree.OfType<JsonObject>()) Count(node);
        return new JsonObject
        {
            ["folders"] = tree, ["typeFolders"] = TypeFolders(m), ["roleFolders"] = RoleFolders(m),
            ["unfiled"] = own.Where(kv => !index.ContainsKey(kv.Key)).Sum(kv => kv.Value),
            ["language"] = lang, ["root"] = Root(m), ["keepLimit"] = KeepLimit(m),
            ["projectFolder"] = ReferenceDisk.RootPath(m),
        };
    }

    // ------------------------------------------------------------ registration

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Reference document", [], "#888888", Kind: "resource", Template: false));

        // A document received (with its files) or asked for (none yet: requested). A file already held is kept once; the type's
        // folder unless one is given; a copy in the project folder when it has one (a missing folder is a warning).
        r.Command("references.add", "Add reference document", (ctx, a) =>
        {
            var m = ctx.Model;
            var folders = Index(Folders(m));
            var warnings = new List<string>();
            var kept = Keep(ctx, a["files"], [], warnings);
            var title = ((string?)a["title"])?.Trim() is { Length: > 0 } t ? t
                : kept.Count > 0 ? System.IO.Path.GetFileNameWithoutExtension(kept[0].File.Name) : throw new CommandException("A document has a title, or a file to take it from.");
            var now = Now();
            var attrs = new JsonObject { ["status"] = kept.Count > 0 ? "received" : "requested", ["addedAt"] = now, ["addedBy"] = Author(a) };
            Apply(m, attrs, a, folders, null);
            if (attrs["type"] == null && TypeOf(m, "altul") != null) attrs["type"] = "altul";
            if (attrs["folder"] == null && (string?)attrs["type"] is { } typeId && (string?)TypeFolders(m)[typeId] is { } byType && folders.TryGetValue(byType, out var bf) && bf.Derived == null) attrs["folder"] = byType;
            if ((string?)attrs["status"] == "requested")
            {
                attrs["requestedAt"] = ReferenceDates.Parse((string?)a["requestedAt"]) ?? now[..10];
                attrs["requestedBy"] = ((string?)a["requestedBy"])?.Trim() is { Length: > 0 } rb ? rb : Author(a);
            }
            attrs["files"] = new JsonArray([.. kept.Select(k => (JsonNode)k.File.ToJson())]);
            var e = new Entity(ctx.NewId(), Class, title, attrs);
            ctx.Add(e);
            // The same bytes in another document already: said, never refused (a permit and the application it answers may share an annex).
            var shas = kept.Select(k => k.File.Sha256).ToHashSet();
            var same = All(m).Where(x => x.Id != e.Id && Files(x).Any(f => shas.Contains(f.Sha256))).Select(x => (JsonNode)x.Id).ToArray();
            warnings.AddRange(ReferenceDisk.Write(m, attrs.ContainsKey("folder") ? (string?)attrs["folder"] : null, kept));
            foreach (var w in warnings) ctx.Query.Warn(w);
            return new JsonObject
            {
                ["id"] = e.Id, ["files"] = new JsonArray([.. kept.Select(k => (JsonNode)k.File.ToJson())]),
                ["duplicates"] = new JsonArray(same), ["warnings"] = new JsonArray([.. warnings.Select(w => (JsonNode)w)]),
            };
        });

        // The fields changed (the folder too: its copy in the project folder follows); a requested document gets its files with references.attach.
        r.Command("references.update", "Edit reference document", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var attrs = e.CloneAttrs();
            Apply(ctx.Model, attrs, a, Index(Folders(ctx.Model)), e.Id);
            attrs["modifiedAt"] = Now();
            attrs["modifiedBy"] = Author(a);
            var title = ((string?)a["title"])?.Trim() is { Length: > 0 } t ? t : e.Name;
            ctx.Replace(e.With(name: title, attrs: attrs));
            var warnings = ReferenceDisk.Move(ctx.Model, e.Str("folder"), (string?)attrs["folder"], Files(e), ctx.Query.ReferenceFiles);
            foreach (var w in warnings) ctx.Query.Warn(w);
            return ToJson(ctx.Model, ctx.Model.Get(e.Id)!, Today(a));
        });

        // Documents moved to another folder (dropped on it): only their folder changes, and their copies in the project folder.
        r.Command("references.move", "Move reference documents", (ctx, a) =>
        {
            var folders = Index(Folders(ctx.Model));
            var folder = CheckFolder(folders, (string?)a["folder"]);
            var ids = a["ids"] is JsonArray list ? list.Select(x => (string?)x).OfType<string>().ToList() : (string?)a["id"] is { } one ? [one] : [];
            if (ids.Count == 0) throw new CommandException("Which documents: ids.");
            var moved = 0;
            foreach (var id in ids.Distinct())
            {
                var e = Require(ctx.Model, id);
                if (e.Str("folder") == folder) continue;
                var attrs = e.CloneAttrs();
                attrs["folder"] = folder;
                attrs["modifiedAt"] = Now();
                attrs["modifiedBy"] = Author(a);
                ctx.Replace(e.With(attrs: attrs));
                foreach (var w in ReferenceDisk.Move(ctx.Model, e.Str("folder"), folder, Files(e), ctx.Query.ReferenceFiles)) ctx.Query.Warn(w);
                moved++;
            }
            return new JsonObject { ["moved"] = moved };
        });

        // More files for a document (a permit's annexes; the file of a document that was requested, which is then received).
        r.Command("references.attach", "Add files to reference document", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var warnings = new List<string>();
            var existing = Files(e);
            var kept = Keep(ctx, a["files"], [.. existing.Select(f => f.Name)], warnings);
            if (kept.Count == 0) throw new CommandException("No file to add.");
            var attrs = e.CloneAttrs();
            attrs["files"] = new JsonArray([.. existing.Select(f => (JsonNode)f.ToJson()), .. kept.Select(k => (JsonNode)k.File.ToJson())]);
            if (e.Str("status") == "requested") attrs["status"] = "received";
            attrs["modifiedAt"] = Now();
            attrs["modifiedBy"] = Author(a);
            ctx.Replace(e.With(attrs: attrs));
            warnings.AddRange(ReferenceDisk.Write(ctx.Model, e.Str("folder"), kept));
            foreach (var w in warnings) ctx.Query.Warn(w);
            return new JsonObject { ["files"] = new JsonArray([.. kept.Select(k => (JsonNode)k.File.ToJson())]), ["warnings"] = new JsonArray([.. warnings.Select(w => (JsonNode)w)]) };
        });

        // A file taken off a document (added by mistake): its record goes; its bytes leave the project's file at the next save unless another document names them.
        r.Command("references.detach", "Remove file from reference document", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var name = (string?)a["name"] ?? throw new CommandException("Which file: name.");
            var files = Files(e);
            if (files.All(f => f.Name != name)) throw new CommandException($"The document has no file {name}.");
            var attrs = e.CloneAttrs();
            attrs["files"] = new JsonArray([.. files.Where(f => f.Name != name).Select(f => (JsonNode)f.ToJson())]);
            attrs["modifiedAt"] = Now();
            attrs["modifiedBy"] = Author(a);
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // A document replaced by its newer one (a permit renewed): kept, linked to the one that supersedes it.
        r.Command("references.supersede", "Supersede reference document", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var by = Require(ctx.Model, (string?)a["by"]);
            if (by.Id == e.Id) throw new CommandException("A document does not supersede itself.");
            var x = by;
            for (var guard = 0; x.Str("supersededBy") is { } next && guard < 64; guard++)
            {
                if (next == e.Id) throw new CommandException("That would make the two documents supersede each other.");
                if (ctx.Model.Get(next) is not { Class: Class } n) break;
                x = n;
            }
            var attrs = e.CloneAttrs();
            attrs["status"] = "superseded";
            attrs["supersededBy"] = by.Id;
            attrs["modifiedAt"] = Now();
            attrs["modifiedBy"] = Author(a);
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // A document no longer valid for the project (annulled, sent by mistake to the wrong project): kept, with why.
        r.Command("references.withdraw", "Withdraw reference document", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var reason = ((string?)a["reason"])?.Trim();
            if (string.IsNullOrEmpty(reason)) throw new CommandException("A withdrawal says why (reason).");
            var attrs = e.CloneAttrs();
            attrs["status"] = "withdrawn";
            attrs["reason"] = reason;
            attrs["withdrawnAt"] = Now();
            attrs["modifiedAt"] = Now();
            attrs["modifiedBy"] = Author(a);
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // A document added by mistake, taken out of the register (the host's alone: a guest is refused). The links to it go with it;
        // its copy in the project folder stays (the register is the truth, the disk is never cleaned behind the user's back).
        r.Command("references.delete", "Delete reference document", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            ctx.Remove(e.Id);
            foreach (var other in All(ctx.Model).ToList())
            {
                var attrs = other.CloneAttrs();
                var changed = false;
                if (other.Str("supersededBy") == e.Id) { attrs.Remove("supersededBy"); if (other.Str("status") == "superseded") attrs["status"] = "received"; changed = true; }
                if (attrs["requires"] is JsonArray req)
                    foreach (var item in req.OfType<JsonObject>().Where(i => (string?)i["document"] == e.Id)) { item.Remove("document"); changed = true; }
                if (attrs["relatesTo"] is JsonArray rel && rel.Any(x => (string?)x == e.Id))
                {
                    var rest = rel.Select(x => (string?)x).Where(x => x != null && x != e.Id).Select(x => (JsonNode)x!).ToArray();
                    if (rest.Length == 0) attrs.Remove("relatesTo"); else attrs["relatesTo"] = new JsonArray(rest);
                    changed = true;
                }
                if (changed) ctx.Replace(other.With(attrs: attrs));
            }
            return null;
        });

        // The project's folder tree (its own copy from now on), with the types' and the roles' folders; a folder holding documents stays.
        // A folder renamed or moved: its documents' copies in the project folder follow.
        r.Command("references.setFolders", "Reference document folders", (ctx, a) =>
        {
            var m = ctx.Model;
            var before = Index(Folders(m));
            var beforePaths = All(m).Select(e => (e, ReferenceDisk.FolderPath(m, e.Str("folder")))).ToList();
            var tree = a["folders"] is JsonArray t ? (JsonArray)t.DeepClone() : Folders(m);
            var index = Index(tree);
            foreach (var e in All(m))
                if (e.Str("folder") is { } f && !index.ContainsKey(f))
                    throw new CommandException($"The folder „{before.GetValueOrDefault(f)?.Name("en") ?? f}” holds documents: move them first.");
            // The mapping given over the current one (an empty value takes a key out); a key whose folder the new tree lacks goes.
            JsonObject Mapping(string key, JsonNode? value, JsonObject current)
            {
                var o = new JsonObject();
                foreach (var (k, v) in current) if ((string?)v is { } id && index.ContainsKey(id)) o[k] = id;
                if (value is not JsonObject given) return o;
                foreach (var (k, v) in given)
                {
                    if ((string?)v is not { Length: > 0 } id) { o.Remove(k); continue; }
                    if (!index.ContainsKey(id)) throw new CommandException($"{key}: there is no folder {id} in the tree.");
                    o[k] = id;
                }
                return o;
            }
            var typeFolders = Mapping("typeFolders", a["typeFolders"], TypeFolders(m));
            var roleFolders = Mapping("roleFolders", a["roleFolders"], RoleFolders(m));
            var language = ((string?)a["language"])?.Trim() is { Length: > 0 } l ? l : null;
            WriteSettings(ctx, s =>
            {
                s["folders"] = tree;
                s["typeFolders"] = typeFolders;
                s["roleFolders"] = roleFolders;
                if (language != null) s["language"] = language;
            });
            foreach (var (e, from) in beforePaths)
                if (from != ReferenceDisk.FolderPath(ctx.Model, e.Str("folder")))
                    foreach (var w in ReferenceDisk.MovePaths(ctx.Model, from, ReferenceDisk.FolderPath(ctx.Model, e.Str("folder")), Files(e), ctx.Query.ReferenceFiles)) ctx.Query.Warn(w);
            return FolderTree(ctx.Model);
        });

        // The project's type list (its own copy from now on): each type's names, default folder, default validity and extra fields; a type in use stays.
        r.Command("references.setTypes", "Reference document types", (ctx, a) =>
        {
            var m = ctx.Model;
            if (a["types"] is not JsonArray given || given.Count == 0) throw new CommandException("The list has at least one type.");
            var folders = Index(Folders(m));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var types = new JsonArray();
            foreach (var n in given)
            {
                if (n is not JsonObject t || ((string?)t["id"])?.Trim() is not { Length: > 0 } id) throw new CommandException("Each type has an id.");
                if (!ids.Add(id)) throw new CommandException($"Two types have the id {id}.");
                if (NameOf(t["name"], "en").Trim().Length == 0) throw new CommandException($"The type {id} has no name.");
                var type = (JsonObject)t.DeepClone();
                type["id"] = id;
                if ((string?)type["folder"] is { Length: > 0 } f) CheckFolder(folders, f); else type.Remove("folder");
                if (type["validityMonths"] != null && Json.Number(type["validityMonths"]) is not (> 0 and <= 600)) throw new CommandException($"The type {id}: its validity is a number of months.");
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var field in type["fields"] as JsonArray ?? [])
                    if (field is not JsonObject fo || ((string?)fo["key"])?.Trim() is not { Length: > 0 } key || !keys.Add(key)) throw new CommandException($"The type {id}: each extra field has a key of its own.");
                types.Add((JsonNode)type);
            }
            foreach (var e in All(m))
                if (e.Str("type") is { } used && !ids.Contains(used)) throw new CommandException($"Documents are of the type {used}: it stays in the list.");
            var typeFolders = TypeFolders(m);
            foreach (var t in types.OfType<JsonObject>())
                if ((string?)t["folder"] is { } f) typeFolders[(string)t["id"]!] = f;
            foreach (var k in typeFolders.Select(kv => kv.Key).Where(k => !ids.Contains(k)).ToList()) typeFolders.Remove(k);
            WriteSettings(ctx, s => { s["types"] = types; s["typeFolders"] = typeFolders; });
            return Types(ctx.Model);
        });

        // The certificate's checklist items offered (the project's own list from now on; null: the indicative defaults again). Items a
        // certificate ticked stay on it whatever the list becomes.
        r.Command("references.setChecklist", "Reference documents' checklist", (ctx, a) =>
        {
            JsonArray? items = null;
            if (a["checklist"] is JsonArray given)
            {
                items = [];
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var n in given)
                {
                    if (n is not JsonObject item) throw new CommandException("A checklist item is {key, label, group?, folder?}.");
                    var label = item["label"]?.DeepClone();
                    var key = ((string?)item["key"])?.Trim() is { Length: > 0 } k ? k : Slug(NameOf(label, "en"));
                    if (key.Length == 0 || label == null || NameOf(label, "en").Trim().Length == 0) throw new CommandException("Each checklist item has a key and a label.");
                    if (!keys.Add(key)) throw new CommandException($"The checklist names {key} twice.");
                    var o = new JsonObject { ["key"] = key, ["label"] = label };
                    if (((string?)item["group"])?.Trim() is { Length: > 0 } g) o["group"] = g;
                    if (((string?)item["folder"])?.Trim() is { Length: > 0 } f) o["folder"] = CheckFolder(Index(Folders(ctx.Model)), f);
                    items.Add((JsonNode)o);
                }
            }
            else if (a["checklist"] != null) throw new CommandException("The checklist is a list of items, or null for the defaults.");
            WriteSettings(ctx, s => { if (items != null) s["checklist"] = items; else s.Remove("checklist"); });
            return ChecklistOptions(ctx.Model);
        });

        // The largest file kept in the project's file, in bytes (question R-Q1: 50 MB by default); null: the default again.
        r.Command("references.setKeepLimit", "Reference documents' size limit", (ctx, a) =>
        {
            var limit = Json.Number(a["bytes"]);
            if (limit is < 0) throw new CommandException("The limit is a number of bytes.");
            WriteSettings(ctx, s => { if (limit is { } v) s["keepLimit"] = (long)v; else s.Remove("keepLimit"); });
            return new JsonObject { ["keepLimit"] = KeepLimit(ctx.Model) };
        });

        // ---- queries (a guest of a shared session may ask every one of these but the scan, which reads the host's disk)

        // The documents, filtered: by folder (deep: with its subfolders), type, phase, status (expired included), those expiring within N days, and text.
        r.Query("references.list", (q, a) =>
        {
            var m = q.Model;
            var today = Today(a);
            var folders = Index(Folders(m));
            var folder = (string?)a["folder"];
            var deep = a["deep"] is JsonValue dv && dv.TryGetValue<bool>(out var d) && d;
            HashSet<string>? inFolder = null;
            if (folder is { Length: > 0 })
            {
                inFolder = [folder];
                if (deep)
                    foreach (var f in folders.Values)
                        for (var p = f.Parent; p != null; p = folders.GetValueOrDefault(p)?.Parent)
                            if (p == folder) { inFolder.Add(f.Id); break; }
            }
            var type = (string?)a["type"];
            var phase = (string?)a["phase"];
            var status = (string?)a["status"];
            var text = ((string?)a["q"])?.Trim();
            var within = Json.Number(a["expiringWithin"]);
            var until = within is { } w ? ReferenceDates.Iso8601(ReferenceDates.Day(today)!.Value.AddDays((int)w)) : null;
            var o = new JsonArray();
            foreach (var e in All(m).OrderBy(x => x.Str("addedAt"), StringComparer.Ordinal))
            {
                if (inFolder != null && !inFolder.Contains(e.Str("folder") ?? "")) continue;
                if (type is { Length: > 0 } && e.Str("type") != type) continue;
                if (phase is { Length: > 0 } && !(e.Attrs["phase"] as JsonArray ?? []).Any(x => string.Equals((string?)x, phase, StringComparison.OrdinalIgnoreCase))) continue;
                var shown = StatusOf(e, today);
                if (status is { Length: > 0 } && shown != status) continue;
                if (until != null && !(shown == "received" && e.Str("validUntil") is { } vu && string.CompareOrdinal(vu, until) <= 0)) continue;
                if (text is { Length: > 0 } && !new[] { e.Name, e.Str("number"), e.Str("issuer") }.Any(x => x != null && x.Contains(text, StringComparison.OrdinalIgnoreCase))) continue;
                o.Add((JsonNode)ToJson(m, e, today, folders));
            }
            return o;
        });

        r.Query("references.get", (q, a) => ToJson(q.Model, Require(q.Model, (string?)a["id"]), Today(a)));

        // A document's file (the first when no name is given), as base64.
        r.Query("references.file", (q, a) =>
        {
            var (file, bytes) = Bytes(q, Require(q.Model, (string?)a["id"]), (string?)a["name"]);
            return new JsonObject { ["name"] = file.Name, ["mime"] = file.Mime, ["size"] = bytes.LongLength, ["sha256"] = file.Sha256, ["data"] = Convert.ToBase64String(bytes) };
        });

        // A file's text by pages (a PDF's text layer: a scan has none), marked as the document's content.
        r.Query("references.text", (q, a) => Text(q, Require(q.Model, (string?)a["id"]), (string?)a["name"], a["pages"]));

        // The number, date, issuer, validity and type the text suggests, each with the words it was read from: for the user to confirm.
        r.Query("references.suggest", (q, a) =>
        {
            var e = Require(q.Model, (string?)a["id"]);
            var text = Text(q, e, (string?)a["name"], a["pages"]);
            var all = string.Join("\n\n", (text["pages"] as JsonArray ?? []).Select(p => (string?)p?["text"] ?? ""));
            var o = ReferenceSuggest.From(all, Types(q.Model));
            // No validity written, but the document's (or the suggested) type has a default one: offered as such, marked indicative.
            var date = (string?)o["date"] ?? e.Str("date");
            if (o["validUntil"] == null && ReferenceDates.Day(date) is { } from && TypeOf(q.Model, (string?)o["type"] ?? e.Str("type")) is { } t && Json.Number(t["validityMonths"]) is { } months)
            {
                o["validUntil"] = ReferenceDates.Iso8601(from.AddMonths((int)months));
                o["validUntilIndicative"] = true;
            }
            if ((string?)text["warning"] is { } why) o["warning"] = why;
            return o;
        });

        r.Query("references.checklist", (q, a) => Checklist(q.Model, Require(q.Model, (string?)a["id"]), Today(a)));

        r.Query("references.folders", (q, _) => FolderTree(q.Model));

        // The type list, and the issuers the project's documents name (remembered: derived from them) before the defaults.
        r.Query("references.types", (q, _) => new JsonObject
        {
            ["types"] = Types(q.Model), ["typeFolders"] = TypeFolders(q.Model), ["checklist"] = ChecklistOptions(q.Model),
            ["issuers"] = new JsonArray([.. All(q.Model).Select(e => e.Str("issuer")).OfType<string>().Concat(ReferenceDefaults.Issuers).Distinct(StringComparer.OrdinalIgnoreCase).Select(x => (JsonNode)x)]),
            ["statuses"] = new JsonArray([.. Statuses.Append("expired").Select(x => (JsonNode)x)]),
        });

        // The project folder's Reference/ read: the files there the register does not hold (with a folder and a type suggested from
        // their path), and the register's copies not found there. The host's alone (it reads the host's disk).
        r.Query("references.scan", (q, _) => ReferenceDisk.Scan(q.Model));
    }
}
