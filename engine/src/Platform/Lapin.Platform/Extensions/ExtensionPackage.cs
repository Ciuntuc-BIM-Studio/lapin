using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lapin.Platform;

/// <summary>Why an extension package or an installed extension is refused: a sentence for Extensions → Installed.</summary>
public sealed class ExtensionException(string message) : Exception(message);

/// <summary>
/// What the engine reads of an extension's catalogue manifest (docs/plan-extension-modules.md §1.1; the interface's manifest.ts and
/// the catalogue's tools/lib.mjs hold the full rules): the fields the loader checks and shows. <see cref="Sha256"/>, <see cref="Size"/>
/// and the signature are the catalogue's, over the package zip.
/// </summary>
public sealed record ExtensionManifest(
    string Id, string Version, string Kind, string Name, string License, string[] Permissions,
    string Sha256, long Size, string? KeyId, string? Signature, string? EulaFile, string? EulaVersion,
    string? EngineAssembly = null, string? Contract = null, string? Sdk = null)
{
    /// <summary>The manifest as the catalogue gave it: the package's manifest.json must be the same but for <c>package</c>.</summary>
    public JsonObject? Json { get; init; }

    /// <summary>The engine module's id: the extension id's last part (ro.ciuntucbimstudio.planning-plus → planning-plus).</summary>
    public string ModuleId => Id[(Id.LastIndexOf('.') + 1)..];

    public static ExtensionManifest Parse(JsonObject o)
    {
        string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
        var p = o["package"] as JsonObject ?? throw new ExtensionException("The manifest has no package.");
        var sig = p["signature"] as JsonObject;
        var eula = o["eula"] as JsonObject;
        // A whole number however it came (parsed text, or a value made in code): its JSON text read.
        long size = p["size"] is JsonValue sv && sv.GetValueKind() == System.Text.Json.JsonValueKind.Number
            && long.TryParse(sv.ToJsonString(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var l) ? l : -1;
        var name = o["name"] switch { JsonValue v when v.TryGetValue<string>(out var s) => s, JsonObject n => Str(n["en"]), _ => "" };
        return new ExtensionManifest(
            Str(o["id"]), Str(o["version"]), Str(o["kind"]), name is { Length: > 0 } ? name : Str(o["id"]), Str(o["license"]),
            (o["permissions"] as JsonArray)?.Select(Str).ToArray() ?? [],
            Str(p["sha256"]), size, sig == null ? null : Str(sig["keyId"]), sig == null ? null : Str(sig["value"]),
            eula == null ? null : Str(eula["file"]), eula == null ? null : Str(eula["version"]),
            o["engine"] is JsonObject engine ? Str(engine["assembly"]) : null, o["contract"] is JsonValue ? Str(o["contract"]) : null,
            o["sdk"] is JsonValue ? Str(o["sdk"]) : null) { Json = o };
    }
}

/// <summary>
/// A <c>.lapinext</c> package (docs/plan-extension-modules.md §1.1): a zip with manifest.json, module.json, engine/*.dll (managed
/// only), ui/plugin.js, ui/plugin.css, ui/i18n/&lt;lang&gt;.json, LICENSE.txt and FILES.sha256 (every other file's SHA-256 and size,
/// one per line: <c>&lt;sha256&gt; &lt;size&gt; &lt;path&gt;</c>). Checked whole: the store's signature over the zip's id, version, SHA-256
/// and size, every entry against FILES.sha256 and no entry unlisted, safe paths, and no native code.
/// </summary>
public static partial class ExtensionPackage
{
    public const string Kind = "module";
    /// <summary>The permission a module must declare: nothing restricts a C# module (§4.2).</summary>
    public const string FullPermission = "full";
    public const string FilesList = "FILES.sha256";
    /// <summary>The bookkeeping files the engine keeps beside an unpacked version: the zip as signed and the catalogue's manifest.</summary>
    public const string KeptZip = "package.lapinext", KeptManifest = "signed.json";
    /// <summary>The catalogue's limit on a package (manifest.ts, MAX_PACKAGE); unpacked, four times it.</summary>
    public const long MaxPackage = 64L * 1024 * 1024, MaxUnpacked = 4 * MaxPackage;
    const int MaxEntries = 4000;

    [GeneratedRegex(@"^[a-z0-9]+(?:[.-][a-z0-9]+)*$")] private static partial Regex IdPattern();
    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?$")] private static partial Regex VersionPattern();
    [GeneratedRegex(@"^[0-9a-f]{64}$")] private static partial Regex ShaPattern();
    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_.+-]*$")] private static partial Regex SegmentPattern();

    public static bool IsId(string s) => s.Length <= 100 && IdPattern().IsMatch(s);
    public static bool IsVersion(string s) => s.Length <= 64 && VersionPattern().IsMatch(s);

    /// <summary>A checked package: its manifest, its files (path → SHA-256, size) and the module.json it carries (null: an interface half only).</summary>
    public sealed record Checked(ExtensionManifest Manifest, IReadOnlyDictionary<string, (string Sha256, long Size)> Files, ModuleLoader.ModuleManifest? Module)
    {
        /// <summary>Every entry's checked bytes, FILES.sha256 among them: what an install unpacks.</summary>
        public IReadOnlyDictionary<string, byte[]> Entries { get; init; } = new Dictionary<string, byte[]>();
    }

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>
    /// The package <paramref name="zip"/>, as the catalogue manifest <paramref name="signed"/> describes and signs it, checked whole; an
    /// <see cref="ExtensionException"/> says what is wrong. Never runs anything of it.
    /// </summary>
    public static Checked Check(byte[] zip, ExtensionManifest signed, IReadOnlyList<StoreKey> keys)
    {
        if (!IsId(signed.Id)) throw new ExtensionException("The manifest's id is not an extension id.");
        if (!IsVersion(signed.Version)) throw new ExtensionException("The manifest's version is not a semantic version.");
        if (signed.Kind != Kind) throw new ExtensionException($"„{signed.Id}” is not an extension module (kind „{signed.Kind}”).");
        if (!signed.Permissions.Contains(FullPermission)) throw new ExtensionException($"„{signed.Id}” runs code but does not say so (permission „{FullPermission}”).");
        if (!ShaPattern().IsMatch(signed.Sha256) || signed.Size <= 0 || signed.Size > MaxPackage) throw new ExtensionException("The manifest's package.sha256 or package.size is wrong.");
        // The signature first, on what the catalogue says: an unsigned module never loads (§4.1), whatever catalogue listed it.
        if (signed.KeyId == null || signed.Signature == null) throw new ExtensionException($"„{signed.Id}” is not signed by the store: an extension module must be.");
        if (!StoreKeys.Verify(keys, signed.KeyId, signed.Signature, StoreKeys.PackageMessage(signed.Id, signed.Version, signed.Sha256, signed.Size)))
            throw new ExtensionException($"The store's signature of „{signed.Id}” {signed.Version} does not hold: it may have been changed.");
        if (zip.LongLength != signed.Size || Sha256Hex(zip) != signed.Sha256) throw new ExtensionException($"The package of „{signed.Id}” is not the one the store signed (its SHA-256 or size differs).");

        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var lower = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        try
        {
            using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
            if (archive.Entries.Count > MaxEntries) throw new ExtensionException("The package holds too many files.");
            foreach (var e in archive.Entries)
            {
                if (e.FullName.EndsWith('/')) { if (e.Length != 0) throw new ExtensionException("A folder entry has content."); continue; }
                var path = SafePath(e.FullName) ?? throw new ExtensionException($"The package holds a file with a path Lapin does not take: „{e.FullName}”.");
                if (!lower.Add(path)) throw new ExtensionException($"The package holds „{path}” twice.");
                total += e.Length;
                if (e.Length > MaxUnpacked || total > MaxUnpacked) throw new ExtensionException("The package unpacks to too much.");
                using var s = e.Open();
                var ms = new MemoryStream();
                CopyAtMost(s, ms, MaxUnpacked - (total - e.Length));
                entries[path] = ms.ToArray();
            }
        }
        catch (InvalidDataException) { throw new ExtensionException("The package is not a zip Lapin can read."); }

        if (!entries.TryGetValue(FilesList, out var listBytes)) throw new ExtensionException($"The package has no {FilesList}.");
        var files = ParseFilesList(listBytes);
        foreach (var (path, bytes) in entries)
        {
            if (path == FilesList) continue;
            if (path is KeptZip or KeptManifest) throw new ExtensionException($"„{path}” is a name the engine keeps for itself.");
            if (!files.TryGetValue(path, out var want)) throw new ExtensionException($"„{path}” is in the package but not in {FilesList}.");
            if (want.Size != bytes.LongLength || want.Sha256 != Sha256Hex(bytes)) throw new ExtensionException($"„{path}” is not the file {FilesList} lists.");
            if (NativeCode(path, bytes) is { } why) throw new ExtensionException($"„{path}” is native code ({why}): extension modules carry managed code only.");
            // engine/ holds flat managed assemblies, their portable .pdb and the module's command catalogue: nothing else.
            if (path.StartsWith("engine/", StringComparison.Ordinal))
            {
                var name = path["engine/".Length..];
                if (name.Contains('/')) throw new ExtensionException($"„{path}”: engine/ has no subfolders.");
                if (!(name.EndsWith(".dll", StringComparison.Ordinal) || name.EndsWith(".pdb", StringComparison.Ordinal) || name == "Lapin.catalogue.json"))
                    throw new ExtensionException($"„{path}”: engine/ holds only .dll, .pdb and Lapin.catalogue.json files.");
            }
        }
        foreach (var path in files.Keys)
            if (!entries.ContainsKey(path)) throw new ExtensionException($"{FilesList} lists „{path}”, which the package does not hold.");

        if (!entries.TryGetValue("manifest.json", out var inner)) throw new ExtensionException("The package has no manifest.json.");
        try
        {
            var o = JsonNode.Parse(inner) as JsonObject ?? throw new ExtensionException("The package's manifest.json is not an object.");
            if ((string?)o["id"] != signed.Id || (string?)o["version"] != signed.Version)
                throw new ExtensionException("The package's manifest.json is for another extension or version than the store signed.");
            // The same manifest as the catalogue's but for its package (which is about the zip, so cannot be in it).
            if (signed.Json is { } catalogue)
            {
                var a = (JsonObject)catalogue.DeepClone();
                var b = (JsonObject)o.DeepClone();
                a.Remove("package");
                b.Remove("package");
                if (!JsonNode.DeepEquals(a, b)) throw new ExtensionException("The package's manifest.json differs from the catalogue's.");
            }
        }
        catch (System.Text.Json.JsonException) { throw new ExtensionException("The package's manifest.json is not JSON."); }
        catch (InvalidOperationException) { throw new ExtensionException("The package's manifest.json has a wrong id or version."); }

        ModuleLoader.ModuleManifest? module = null;
        if (entries.TryGetValue("module.json", out var mj))
        {
            try { module = ModuleLoader.ParseModule(Encoding.UTF8.GetString(mj), "module.json"); }
            catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException) { throw new ExtensionException($"The package's module.json cannot be read: {e.Message}"); }
            if (module.Id != signed.ModuleId) throw new ExtensionException($"module.json's id is „{module.Id}”; it must be „{signed.ModuleId}”, the extension id's last part.");
            if (module.Id == CoreModule.ModuleId) throw new ExtensionException("An extension module cannot be „core”.");
            if (signed.EngineAssembly != module.Assembly) throw new ExtensionException($"The manifest's engine.assembly („{signed.EngineAssembly}”) is not module.json's („{module.Assembly}”).");
            if (signed.Contract != module.Contract) throw new ExtensionException($"The manifest's contract („{signed.Contract}”) is not module.json's („{module.Contract}”).");
            if (module.Assembly is not { Length: > 0 } || SafePath(module.Assembly) is null || module.Assembly.Contains('/') || !module.Assembly.EndsWith(".dll", StringComparison.Ordinal))
                throw new ExtensionException("The package's module.json names no assembly in engine/.");
            if (!entries.ContainsKey("engine/" + module.Assembly)) throw new ExtensionException($"module.json names engine/{module.Assembly}, which the package does not hold.");
        }
        else if (entries.Keys.Any(k => k.StartsWith("engine/", StringComparison.Ordinal)) || signed.EngineAssembly != null)
            throw new ExtensionException("The package has an engine half but no module.json.");
        if (signed.EulaFile is { } eula && (eula.Contains('/') || eula == "LICENSE.txt" || !(eula.EndsWith(".txt", StringComparison.Ordinal) || eula.EndsWith(".md", StringComparison.Ordinal)) || !entries.ContainsKey(eula)))
            throw new ExtensionException($"The licence agreement „{eula}” is not a .txt or .md file at the package's root.");
        return new Checked(signed, files, module) { Entries = entries };
    }

    static void CopyAtMost(Stream from, Stream to, long max)
    {
        var buf = new byte[81920];
        long n = 0;
        int r;
        while ((r = from.Read(buf, 0, buf.Length)) > 0)
        {
            n += r;
            if (n > max) throw new ExtensionException("The package unpacks to too much.");
            to.Write(buf, 0, r);
        }
    }

    /// <summary>A zip entry's path when it is one Lapin unpacks: relative, forward slashes, plain segments (no "..", no drive, no control characters).</summary>
    public static string? SafePath(string path)
    {
        if (path.Length is 0 or > 240 || path.Contains('\\') || path.StartsWith('/') || path.Contains(':')) return null;
        var parts = path.Split('/');
        if (parts.Length > 8) return null;
        foreach (var p in parts)
            if (p.Length == 0 || p.Length > 120 || !SegmentPattern().IsMatch(p) || p.EndsWith('.') || p == "..") return null;
        return path;
    }

    /// <summary>FILES.sha256 read: each line <c>&lt;sha256&gt; &lt;size&gt; &lt;path&gt;</c>, blank lines skipped.</summary>
    public static Dictionary<string, (string Sha256, long Size)> ParseFilesList(byte[] bytes)
    {
        var o = new Dictionary<string, (string, long)>(StringComparer.Ordinal);
        var n = 0;
        foreach (var raw in Encoding.UTF8.GetString(bytes).Split('\n'))
        {
            n++;
            var line = raw.TrimEnd('\r');
            if (line.Trim().Length == 0) continue;
            var parts = line.Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3 || !ShaPattern().IsMatch(parts[0]) || !long.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var size) || SafePath(parts[2]) is null)
                throw new ExtensionException($"{FilesList} line {n} is not „<sha256> <size> <path>”.");
            if (!o.TryAdd(parts[2], (parts[0], size))) throw new ExtensionException($"{FilesList} lists „{parts[2]}” twice.");
        }
        return o;
    }

    /// <summary>Why a file is native code, from its first bytes (a PE without a CLI header, Mach-O, ELF) or its name; null when it is not.</summary>
    public static string? NativeCode(string path, byte[] b)
    {
        var lower = path.ToLowerInvariant();
        if (lower.EndsWith(".dylib") || lower.EndsWith(".so") || lower.Contains(".so.") || lower.EndsWith(".exe") || lower.EndsWith(".node") || lower.EndsWith(".jnilib")) return "by its name";
        if (b.Length >= 4)
        {
            var m = BitConverter.ToUInt32(b, 0);
            if (m is 0xFEEDFACE or 0xFEEDFACF or 0xCEFAEDFE or 0xCFFAEDFE or 0xCAFEBABE or 0xBEBAFECA) return "a Mach-O file";
            if (b[0] == 0x7F && b[1] == (byte)'E' && b[2] == (byte)'L' && b[3] == (byte)'F') return "an ELF file";
        }
        if (b.Length >= 2 && b[0] == (byte)'M' && b[1] == (byte)'Z')
        {
            try
            {
                using var pe = new System.Reflection.PortableExecutable.PEReader(new MemoryStream(b));
                var h = pe.PEHeaders;
                if (h.CorHeader == null) return "a PE file without managed code";
                // Mixed-mode (C++/CLI) assemblies carry native code too; so does a precompiled (ReadyToRun) one. The rules of the
                // catalogue's tools/lapinext.mjs: a PE32 for any CPU, IL only, not 32-bit only, no native entry point.
                var flags = h.CorHeader.Flags;
                if ((flags & System.Reflection.PortableExecutable.CorFlags.ILOnly) == 0) return "a mixed-mode assembly";
                if (h.PEHeader?.Magic != System.Reflection.PortableExecutable.PEMagic.PE32 || h.CoffHeader.Machine != System.Reflection.PortableExecutable.Machine.I386
                    || (flags & (System.Reflection.PortableExecutable.CorFlags.Requires32Bit | System.Reflection.PortableExecutable.CorFlags.NativeEntryPoint | System.Reflection.PortableExecutable.CorFlags.ILLibrary)) != 0
                    || h.CorHeader.ManagedNativeHeaderDirectory.Size != 0)
                    return "an assembly built for one processor, or precompiled";
            }
            catch (BadImageFormatException) { return "an unreadable PE file"; }
        }
        else if (lower.EndsWith(".dll")) return "a .dll that is no assembly";
        return null;
    }
}
