using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Cde.References;

/// <summary>
/// The reference documents' copies in the project folder (docs/plan-cde-references.md §3.6): when the project has a folder
/// (<c>cde.setFolder</c>) that is on this computer, a document's files are written to
/// <c>&lt;folder&gt;/Reference/&lt;its folder's path&gt;/&lt;file name&gt;</c>, and moved with it. The register is the truth:
/// a folder that is not there, a copy deleted or a different file in its place is a warning, never an error, and nothing on
/// the disk is deleted. <see cref="Scan"/> lists what the folder holds that the register does not.
/// </summary>
public static class ReferenceDisk
{
    /// <summary>The folder the copies go into (<c>&lt;project folder&gt;/Reference</c>); null when the project has no folder, or it is not on this computer.</summary>
    public static string? RootPath(IModelView m)
    {
        try { return Cde.Folder(m) is { } f && Directory.Exists(f) ? Path.Combine(f, FileNames.Stem(References.Root(m), "Reference")) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    /// <summary>The directory of a document folder's copies (the root for a document without a folder); null without a project folder.</summary>
    public static string? FolderPath(IModelView m, string? folderId)
    {
        if (RootPath(m) is not { } root) return null;
        if (folderId == null) return root;
        var index = References.Index(References.Folders(m));
        if (!index.TryGetValue(folderId, out var f)) return root;
        var lang = References.Language(m);
        return Path.Combine([root, .. f.Names.Select(n => FileNames.Stem(References.NameOf(n, lang), "Folder"))]);
    }

    public static string? CopyPath(IModelView m, string? folderId, string name) => FolderPath(m, folderId) is { } dir ? Path.Combine(dir, FileNames.Stem(name, "file")) : null;

    /// <summary>Why no copy is written: the project's folder is set but not here (null: it is, or none is set).</summary>
    static string? Missing(IModelView m) => Cde.Folder(m) is { } f && RootPath(m) == null ? $"The project folder {f} is not on this computer: no copy of the document was written there." : null;

    static bool Same(string path, string sha)
    {
        try
        {
            using var s = File.OpenRead(path);
            return Hashes.Sha256Hex(s) == sha;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>The files written into the document folder's directory; what could not be, said.</summary>
    public static List<string> Write(IModelView m, string? folderId, IReadOnlyList<(ReferenceFile File, byte[] Bytes)> files)
    {
        var warnings = new List<string>();
        if (files.Count == 0) return warnings;
        if (Missing(m) is { } why) { warnings.Add(why); return warnings; }
        if (FolderPath(m, folderId) is not { } dir) return warnings;
        foreach (var (file, bytes) in files)
        {
            var path = Path.Combine(dir, FileNames.Stem(file.Name, "file"));
            try
            {
                if (File.Exists(path))
                {
                    if (!Same(path, file.Sha256)) warnings.Add($"A different file named {file.Name} is in {dir} already: it was left as it is, and no copy was written.");
                    continue;
                }
                Directory.CreateDirectory(dir);
                AtomicFile.Write(path, bytes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"{file.Name} could not be copied into the project folder ({ex.Message}).");
            }
        }
        return warnings;
    }

    /// <summary>A document's copies moved from one document folder to another.</summary>
    public static List<string> Move(IModelView m, string? from, string? to, IReadOnlyList<ReferenceFile> files, IReferenceFiles? store) =>
        from == to ? [] : MovePaths(m, FolderPath(m, from), FolderPath(m, to), files, store);

    /// <summary>
    /// The copies moved between two directories: each found where it was (and still the same bytes) is moved; one missing is written
    /// anew from the project's file when it holds it, else said; a different file where it goes is left, and said.
    /// </summary>
    public static List<string> MovePaths(IModelView m, string? fromDir, string? toDir, IReadOnlyList<ReferenceFile> files, IReferenceFiles? store)
    {
        var warnings = new List<string>();
        if (files.Count == 0 || fromDir == toDir) return warnings;
        if (Missing(m) is { } why) { warnings.Add(why); return warnings; }
        if (toDir == null) return warnings;
        foreach (var file in files)
        {
            var stem = FileNames.Stem(file.Name, "file");
            var dst = Path.Combine(toDir, stem);
            var src = fromDir != null ? Path.Combine(fromDir, stem) : null;
            try
            {
                if (File.Exists(dst))
                {
                    if (!Same(dst, file.Sha256)) warnings.Add($"A different file named {file.Name} is in {toDir} already: it was left as it is.");
                    else if (src != null && File.Exists(src) && Same(src, file.Sha256)) File.Delete(src);
                    continue;
                }
                Directory.CreateDirectory(toDir);
                if (src != null && File.Exists(src) && Same(src, file.Sha256)) { File.Move(src, dst); continue; }
                if (file.Held && store != null && store.Has(file.Sha256))
                {
                    AtomicFile.Write(dst, store.Get(file.Sha256));
                    if (src != null) warnings.Add($"{file.Name} was not in {fromDir}: it was written anew from the project's file.");
                }
                else warnings.Add($"{file.Name} was not in {fromDir}, and the project's file does not hold it: no copy in {toDir}.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                warnings.Add($"{file.Name} could not be moved in the project folder ({ex.Message}).");
            }
        }
        return warnings;
    }

    const int MaxScanned = 10_000;

    /// <summary>
    /// What the project folder's <c>Reference/</c> holds that the register does not (each with the document folder its directory
    /// matches, in either language, and a type from its folder or its name), and the register's copies not found where they go
    /// (<c>missing</c>; <c>misplaced</c> when the same bytes are elsewhere under it).
    /// </summary>
    public static JsonObject Scan(IModelView m)
    {
        var root = RootPath(m);
        var o = new JsonObject { ["root"] = root, ["exists"] = root != null && Directory.Exists(root) };
        if (root == null)
        {
            o["warning"] = Cde.Folder(m) is { } f ? $"The project folder {f} is not on this computer." : "The project has no folder (Manage → Project folder…).";
            o["files"] = new JsonArray();
            o["missing"] = new JsonArray();
            return o;
        }
        var index = References.Index(References.Folders(m));
        // Each folder's directory below the root, in both languages (and the language the copies are written in), to its id.
        var byDir = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in index.Values.Where(f => f.Derived == null))
            foreach (var lang in new[] { References.Language(m), "en", "ro" })
                byDir.TryAdd(string.Join('/', f.Names.Select(n => FileNames.Stem(References.NameOf(n, lang), "Folder"))), f.Id);
        var typeByFolder = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (type, folder) in References.TypeFolders(m)) if ((string?)folder is { } fid) typeByFolder.TryAdd(fid, type);
        var registered = new Dictionary<string, List<(Entity Doc, ReferenceFile File)>>(StringComparer.Ordinal);
        foreach (var e in References.All(m))
            foreach (var file in References.Files(e))
                (registered.TryGetValue(file.Sha256, out var l) ? l : registered[file.Sha256] = []).Add((e, file));
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        var list = new JsonArray();
        var scanned = 0;
        IEnumerable<string> paths;
        try { paths = Directory.Exists(root) ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories) : []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { paths = []; o["warning"] = ex.Message; }
        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            if (name.StartsWith('.') || name.EndsWith(".saving", StringComparison.Ordinal)) continue;
            if (++scanned > MaxScanned) { o["truncated"] = true; break; }
            string sha;
            long size;
            try { using var s = File.OpenRead(path); size = s.Length; sha = Hashes.Sha256Hex(s); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            found.TryAdd(sha, path);
            if (registered.ContainsKey(sha)) continue;
            var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
            var dir = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
            string? folderId = null;
            for (var d = dir; d.Length > 0; d = d.Contains('/') ? d[..d.LastIndexOf('/')] : "")
                if (byDir.TryGetValue(d, out var id)) { folderId = id; break; }
            var type = ReferenceSuggest.TypeOf(Path.GetFileNameWithoutExtension(name)) ?? (folderId != null ? typeByFolder.GetValueOrDefault(folderId) : null);
            var item = new JsonObject { ["path"] = path, ["relative"] = rel, ["name"] = name, ["size"] = size, ["sha256"] = sha };
            if (folderId != null) item["folder"] = folderId;
            if (type != null) item["type"] = type;
            list.Add((JsonNode)item);
        }
        o["files"] = list;
        var missing = new JsonArray();
        foreach (var e in References.All(m))
            foreach (var file in References.Files(e))
            {
                var expected = CopyPath(m, e.Str("folder"), file.Name)!;
                if (File.Exists(expected) && Same(expected, file.Sha256)) continue;
                var item = new JsonObject { ["id"] = e.Id, ["name"] = file.Name, ["expected"] = expected };
                if (found.TryGetValue(file.Sha256, out var elsewhere)) item["foundAt"] = elsewhere;
                missing.Add((JsonNode)item);
            }
        o["missing"] = missing;
        return o;
    }
}
