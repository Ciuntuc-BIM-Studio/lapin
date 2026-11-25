using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;

namespace Lapin.Package.Remote;

/// <summary>The four kinds of objects of lapin-remote 1.0 (docs/spec-lapin-remote.md §4).</summary>
public enum ObjectKind { Blob, Tree, Commit, Tag }

/// <summary>Who made a commit or a tag: <c>&lt;scheme&gt;:&lt;value&gt;</c> (<c>atrium:…</c>, <c>local:…</c>) and a display name; never an e-mail address.</summary>
public sealed record RemoteAuthor(string Id, string Name)
{
    static readonly Regex Grammar = new("^[a-z][a-z0-9]{0,15}:[A-Za-z0-9._-]{1,128}$", RegexOptions.CultureInvariant);
    public static bool ValidId(string id) => Grammar.IsMatch(id);
    public JsonObject ToJson() => new() { ["id"] = Id, ["name"] = Name };
    public static RemoteAuthor? From(JsonNode? n) => n is JsonObject o && (string?)o["id"] is { } id && (string?)o["name"] is { } name ? new RemoteAuthor(id, name) : null;
    /// <summary>The author's id as a folder remote's ref entry names it (§7.3): <c>:</c> as <c>-</c>.</summary>
    public string Key => Id.Replace(':', '-');
}

/// <summary>Object ids (§2): the SHA-256 of the bytes, 64 lowercase hex characters, compared ordinally.</summary>
public static class ObjectIds
{
    public const string Empty = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    public const string Zero = "0000000000000000000000000000000000000000000000000000000000000000";

    public static string Of(ReadOnlySpan<byte> bytes) => Hashes.Sha256Hex(bytes);

    /// <summary>Exactly 64 of [0-9a-f]: an id with an uppercase letter or of another length is refused, never folded.</summary>
    public static bool Valid(string? id)
    {
        if (id is not { Length: 64 }) return false;
        foreach (var c in id) if (c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) return false;
        return true;
    }

    public static string Require(string? id, string what = "id") => Valid(id) ? id! : throw new InvalidDataException($"Not an object id ({what}): {id}.");
}

/// <summary>A commit (§4.3), as its canonical bytes and what Lapin reads of them; members it does not know stay in the bytes.</summary>
public sealed record CommitObject(string Id, byte[] Bytes, int V, string Tree, IReadOnlyList<string> Parents, RemoteAuthor Author, string At, string Message, int? Entities, string? App)
{
    /// <summary>Whether this Lapin can check the commit out (a <c>v</c> it knows, §14).</summary>
    public bool Known => V == 1;
}

/// <summary>An issued or a version's file a tag names (§4.4).</summary>
public sealed record TagFile(string Name, string Sha256, long Size);

/// <summary>A tag (§4.4): an issue's or a numbered version's.</summary>
public sealed record TagObject(string Id, byte[] Bytes, int V, string Commit, string Kind, int Number, string? Revision, string? Name, IReadOnlyList<TagFile> Files, RemoteAuthor Author, string At);

/// <summary>
/// The objects of lapin-remote 1.0 made and read: a tree is the package's manifest without <c>foreign</c>, as canonical JSON
/// (§4.2); commits and tags are canonical JSON (§4.3, §4.4). Every object read is checked canonical and hashed.
/// </summary>
public static class RemoteObjects
{
    /// <summary>The largest tree, commit or tag (§4): 16 MiB.</summary>
    public const int MaxJsonObject = 16 * 1024 * 1024;

    /// <summary>A manifest as a tree: its canonical bytes, <c>foreign</c> left out (other tools' entries are never a project state).</summary>
    public static byte[] Tree(PackageManifest manifest)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(manifest with { Foreign = null }, PackageJson.Default.PackageManifest);
        return Jcs.Canonicalize(json);
    }

    /// <summary>A tree's bytes read as the manifest of a package; refused when not canonical, not a tree, or its paths break §4.2.</summary>
    public static PackageManifest ReadTree(byte[] bytes)
    {
        if (!Jcs.IsCanonical(bytes)) throw new InvalidDataException("A tree is not canonical JSON.");
        using (var doc = JsonDocument.Parse(bytes))
        {
            if (doc.RootElement.TryGetProperty("foreign", out _)) throw new InvalidDataException("A tree may not hold other tools' entries (foreign).");
        }
        var manifest = JsonSerializer.Deserialize(bytes, PackageJson.Default.PackageManifest) ?? throw new InvalidDataException("A tree is unreadable.");
        if (manifest.Format != LapinPackage.Format) throw new InvalidDataException($"Not a tree ({manifest.Format}).");
        if (manifest.Version != LapinPackage.Version) throw new InvalidDataException($"The tree is of package format {manifest.Version}: made by a newer Lapin.");
        if (manifest.Parts is null) throw new InvalidDataException("A tree lists no parts.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in manifest.Parts)
        {
            if (!TreePath(p.Path)) throw new InvalidDataException($"A tree names a part at a path no project holds: {p.Path}.");
            if (!seen.Add(p.Path)) throw new InvalidDataException($"A tree names {p.Path} twice.");
            ObjectIds.Require(p.Sha256, p.Path);
            if (p.Size < 0) throw new InvalidDataException($"A tree gives {p.Path} a negative size.");
        }
        return manifest;
    }

    static readonly string[] NeverParts = ["manifest.json", "versions", "issues", "references", "history", "derived", "journal"];

    /// <summary>A part's path as §4.2 allows it: relative, plain segments, 1–1024 bytes, not under a name that is never a part.</summary>
    public static bool TreePath(string? path)
    {
        if (string.IsNullOrEmpty(path) || Encoding.UTF8.GetByteCount(path) > 1024 || path.Contains('\\') || path.Contains('\0')) return false;
        var segments = path.Split('/');
        if (segments.Any(s => s is "" or "." or "..")) return false;
        return !NeverParts.Contains(segments[0], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>When, as a commit writes it (§4.3): UTC, to the second.</summary>
    public static string Stamp(DateTime now) => now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>A new commit's canonical bytes.</summary>
    public static byte[] Commit(string tree, IReadOnlyList<string> parents, RemoteAuthor author, DateTime at, string message, string? app, string? product, int? entities)
    {
        ObjectIds.Require(tree, "tree");
        foreach (var p in parents) ObjectIds.Require(p, "parent");
        if (parents.Distinct(StringComparer.Ordinal).Count() != parents.Count || parents.Count > 16) throw new InvalidDataException("A commit's parents are distinct, at most 16.");
        if (!RemoteAuthor.ValidId(author.Id)) throw new InvalidDataException($"Not an author id: {author.Id}.");
        var name = string.IsNullOrWhiteSpace(author.Name) ? "Lapin" : author.Name.Trim();
        if (name.Length > 256) name = name[..256];
        if (Encoding.UTF8.GetByteCount(message) > 65536) throw new InvalidDataException("A commit's message is at most 64 KiB.");
        var o = new JsonObject
        {
            ["format"] = "lapin-commit", ["v"] = 1, ["tree"] = tree,
            ["parents"] = new JsonArray(parents.Select(p => (JsonNode)p).ToArray()),
            ["author"] = new JsonObject { ["id"] = author.Id, ["name"] = name },
            ["at"] = Stamp(at), ["message"] = message,
        };
        if (app != null) o["app"] = app;
        if (product != null) o["product"] = product;
        if (entities is { } n) o["entities"] = n;
        return Jcs.Canonicalize(o);
    }

    /// <summary>What a JSON object's <c>format</c> says it is (a tree, a commit or a tag); null for anything else (a blob, or not JSON).</summary>
    public static ObjectKind? KindOf(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes[0] != (byte)'{' || bytes.Length > MaxJsonObject) return null;
        try
        {
            using var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("format", out var f) || f.ValueKind != JsonValueKind.String) return null;
            return f.GetString() switch { "lapin-project" => ObjectKind.Tree, "lapin-commit" => ObjectKind.Commit, "lapin-tag" => ObjectKind.Tag, _ => null };
        }
        catch (JsonException) { return null; }
    }

    /// <summary>A commit's bytes read; refused when they are not canonical, not a commit, or lack what a commit must have. <paramref name="id"/> is checked against the bytes.</summary>
    public static CommitObject ReadCommit(byte[] bytes, string? id = null)
    {
        var actual = Check(bytes, id);
        using var doc = JsonDocument.Parse(bytes);
        var r = doc.RootElement;
        if (Str(r, "format") != "lapin-commit") throw new InvalidDataException("Not a commit.");
        var v = Int(r, "v") ?? throw new InvalidDataException("A commit has no v.");
        var tree = ObjectIds.Require(Str(r, "tree"), "tree");
        var parents = r.TryGetProperty("parents", out var pa) && pa.ValueKind == JsonValueKind.Array
            ? pa.EnumerateArray().Select(x => ObjectIds.Require(x.ValueKind == JsonValueKind.String ? x.GetString() : null, "parent")).ToList()
            : throw new InvalidDataException("A commit has no parents list.");
        var author = Author(r) ?? throw new InvalidDataException("A commit has no author.");
        return new CommitObject(actual, bytes, v, tree, parents, author, Str(r, "at") ?? "", Str(r, "message") ?? "", Int(r, "entities"), Str(r, "app"));
    }

    /// <summary>A tag's bytes read, as <see cref="ReadCommit"/> reads a commit.</summary>
    public static TagObject ReadTag(byte[] bytes, string? id = null)
    {
        var actual = Check(bytes, id);
        using var doc = JsonDocument.Parse(bytes);
        var r = doc.RootElement;
        if (Str(r, "format") != "lapin-tag") throw new InvalidDataException("Not a tag.");
        var v = Int(r, "v") ?? throw new InvalidDataException("A tag has no v.");
        var commit = ObjectIds.Require(Str(r, "commit"), "commit");
        var kind = Str(r, "kind") ?? throw new InvalidDataException("A tag has no kind.");
        var number = Int(r, "number") ?? throw new InvalidDataException("A tag has no number.");
        var files = new List<TagFile>();
        if (r.TryGetProperty("files", out var fa) && fa.ValueKind == JsonValueKind.Array)
            foreach (var f in fa.EnumerateArray())
                files.Add(new TagFile(Str(f, "name") ?? "", ObjectIds.Require(Str(f, "sha256"), "file"), Long(f, "size") ?? 0));
        return new TagObject(actual, bytes, v, commit, kind, number, Str(r, "revision"), Str(r, "name"), files, Author(r) ?? new RemoteAuthor("local:unknown", "?"), Str(r, "at") ?? "");
    }

    /// <summary>The bytes' id, checked against the one given; refused when they are not canonical JSON or too large.</summary>
    static string Check(byte[] bytes, string? id)
    {
        if (bytes.Length > MaxJsonObject) throw new InvalidDataException("An object above 16 MiB is not a tree, a commit or a tag.");
        var actual = ObjectIds.Of(bytes);
        if (id != null && actual != id) throw new InvalidDataException($"The bytes do not hash to {id}.");
        if (!Jcs.IsCanonical(bytes)) throw new InvalidDataException($"The object {actual[..12]}… is not canonical JSON.");
        return actual;
    }

    static string? Str(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static int? Int(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
    static long? Long(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var i) ? i : null;
    static RemoteAuthor? Author(JsonElement r) =>
        r.TryGetProperty("author", out var a) && Str(a, "id") is { } id && Str(a, "name") is { } name ? new RemoteAuthor(id, name) : null;
}

/// <summary>Ref names (§5.1): main, personal branches, versions, issues; anything else refused.</summary>
public static class RefNames
{
    public const string Main = "refs/heads/main";
    static readonly Regex Seg = new("^[a-z0-9][a-z0-9._-]{0,63}$", RegexOptions.CultureInvariant);
    static readonly Regex Version = new("^refs/tags/v/[1-9][0-9]{0,8}$", RegexOptions.CultureInvariant);
    static readonly Regex Issue = new("^refs/tags/issue/[1-9][0-9]{0,8}-[PC][0-9]{2,3}$", RegexOptions.CultureInvariant);

    public static bool Valid(string? name)
    {
        if (name is null || name.Length > 255 || name.Any(c => c > 127)) return false;
        if (name == Main || Version.IsMatch(name) || Issue.IsMatch(name)) return true;
        if (!name.StartsWith("refs/heads/wip/", StringComparison.Ordinal)) return false;
        var rest = name["refs/heads/wip/".Length..].Split('/');
        return rest.Length == 2 && rest.All(s => Seg.IsMatch(s) && !s.Contains("..", StringComparison.Ordinal));
    }

    public static bool IsBranch(string name) => name.StartsWith("refs/heads/", StringComparison.Ordinal);
    public static bool IsTag(string name) => name.StartsWith("refs/tags/", StringComparison.Ordinal);

    /// <summary>An author's slug (§5.1): the display name lowercased, every run of other characters as one <c>-</c>.</summary>
    public static string Slug(string name)
    {
        var sb = new StringBuilder();
        // Letters with marks as their base letter, by a table: the engine runs without ICU (InvariantGlobalization), where Normalize keeps them.
        foreach (var ch in name)
        {
            var c = char.ToLowerInvariant(ch);
            var at = Accented.IndexOf(c);
            if (at >= 0) c = Plain[at];
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var s = sb.ToString().Trim('-');
        if (s.Length == 0) s = "user";
        return s.Length > 64 ? s[..64].TrimEnd('-') : s;
    }

    const string Accented = "àáâãäåāăąçćčďđèéêëēėęěìíîïīįłñńňòóôõöøōőŕřśšşșťţțùúûüūůűųýÿźżž";
    const string Plain = "aaaaaaaaa" + "ccc" + "dd" + "eeeeeeee" + "iiiiii" + "l" + "nnn" + "oooooooo" + "rr" + "ssss" + "ttt" + "uuuuuuuu" + "yy" + "zzz";

    /// <summary>A personal branch's name for now (§5.1: <c>YYYY-MM-DD-HHMM</c>, <c>-2</c>, <c>-3</c> when taken).</summary>
    public static string Wip(string author, DateTime now, Func<string, bool> taken)
    {
        var stem = $"refs/heads/wip/{Slug(author)}/{now.ToUniversalTime():yyyy-MM-dd-HHmm}";
        if (!taken(stem)) return stem;
        for (var i = 2; ; i++) if (!taken($"{stem}-{i}")) return $"{stem}-{i}";
    }
}
