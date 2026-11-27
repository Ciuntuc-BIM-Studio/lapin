using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Nodes;

namespace Lapin.Platform;

/// <summary>One line of <c>modules.json</c>: an installed extension module, the version to load, and whether it is on.</summary>
public sealed record ExtensionEntry(string Id, string Version, bool Enabled = true, bool PendingRemoval = false);

/// <summary>An installed extension as this start found it: loaded, off, or refused with why.</summary>
public sealed class ExtensionState(ExtensionEntry entry)
{
    public ExtensionEntry Entry { get; } = entry;
    public string Id => Entry.Id;
    public string Version => Entry.Version;
    /// <summary>"loaded", "disabled" (the user's), "failed" (refused at this start: <see cref="Problem"/>).</summary>
    public string Status { get; internal set; } = "disabled";
    public string? Problem { get; internal set; }
    /// <summary>What kind of refusal <see cref="Problem"/> is: "revoked" (the catalogue withdrew this version, <see cref="Revocation"/>), else null.</summary>
    public string? ProblemKind { get; internal set; }
    /// <summary>The revocation list's entry that refused it at this start.</summary>
    public Revocation? Revocation { get; internal set; }
    public ExtensionManifest? Manifest { get; internal set; }
    /// <summary>Its engine module, while it is loaded (an interface half only has none).</summary>
    public ILapinModule? Module { get; internal set; }
    public string? ModuleId { get; internal set; }
    public string? Contract { get; internal set; }
    /// <summary>The interface half's texts, read from the bytes checked at this start (§3.1): its plug-in, its CSS, its phrase tables by language.</summary>
    public string? Js { get; internal set; }
    public string? Css { get; internal set; }
    public Dictionary<string, string> Phrases { get; } = [];
    public string? License { get; internal set; }
    /// <summary>Its engine assemblies' checked bytes, by simple name (engine/&lt;name&gt;.dll and its .pdb).</summary>
    internal IReadOnlyDictionary<string, (byte[] Dll, byte[]? Pdb)>? Assemblies { get; set; }

    internal void Fail(string why, string? kind = null) { Status = "failed"; Problem = why; ProblemKind = kind; Module = null; Js = null; Css = null; Phrases.Clear(); Assemblies = null; }
}

/// <summary>What the loader needs of the host: where extensions live, the keys it trusts and what the edition has.</summary>
/// <param name="Root"><c>&lt;user-dir&gt;/extensions</c>.</param>
/// <param name="Modules">The edition's module ids (engine and interface halves), "core" among them.</param>
/// <param name="Apis">Each of the edition's modules' api number (<see cref="ModuleLoader.Apis"/>).</param>
/// <param name="Reserved">Module ids no extension may take besides the edition's: Lapin's built-in modules (the module folders beside the engine).</param>
public sealed record ExtensionHostInfo(string Root, IReadOnlyList<StoreKey> Keys, IReadOnlySet<string> Modules, IReadOnlyDictionary<string, int> Apis, IReadOnlySet<string>? Reserved = null);

/// <summary>
/// Extension modules (docs/plan-extension-modules.md §1.2, §2.1), installed under <c>&lt;user-dir&gt;/extensions/&lt;id&gt;/&lt;version&gt;/</c>
/// with <c>modules.json</c> beside them, which the engine owns. At each start of the desktop host, after the product's modules:
/// what was uninstalled or replaced is removed before anything loads; then each enabled extension is checked whole (the store's
/// signature, the zip's SHA-256, every unpacked file against FILES.sha256 and no file more, its contract, its requirements and their
/// api) and its assembly loaded into its own <see cref="ExtensionLoadContext"/>. Its <c>Register</c> runs in <see cref="ModuleCatalog.Create(IEnumerable{ILapinModule}, IEnumerable{ILapinModule}, Action{ILapinModule, Exception})"/>
/// against a recording registry. Whatever fails disables that one extension, with its reason, and the engine starts.
/// Install, update, uninstall, enable and disable take effect at the next start.
/// </summary>
public static class ExtensionLoader
{
    const string Untrimmed = "Extension modules are loaded by reflection, which only an untrimmed host (the desktop's) can do.";
    public const string ListFile = "modules.json";

    public static List<ExtensionEntry> ReadList(string root)
    {
        var path = Path.Combine(root, ListFile);
        var o = new List<ExtensionEntry>();
        if (!File.Exists(path)) return o;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path))?["modules"] is not JsonArray a) return o;
            foreach (var n in a.OfType<JsonObject>())
            {
                var id = (string?)n["id"] ?? "";
                var version = (string?)n["version"] ?? "";
                // A line naming no folder Lapin would make is never followed.
                if (!ExtensionPackage.IsId(id) || !ExtensionPackage.IsVersion(version) || o.Any(x => x.Id == id)) continue;
                o.Add(new ExtensionEntry(id, version, n["enabled"] is JsonValue e && e.TryGetValue<bool>(out var en) ? en : true, n["pendingRemoval"] is JsonValue r && r.TryGetValue<bool>(out var rm) && rm));
            }
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException or FormatException) { }
        return o;
    }

    public static void WriteList(string root, IEnumerable<ExtensionEntry> entries)
    {
        Directory.CreateDirectory(root);
        var a = new JsonArray();
        foreach (var e in entries)
        {
            var o = new JsonObject { ["id"] = e.Id, ["version"] = e.Version, ["enabled"] = e.Enabled };
            if (e.PendingRemoval) o["pendingRemoval"] = true;
            a.Add((JsonNode)o);
        }
        AtomicFile.WriteText(Path.Combine(root, ListFile), new JsonObject { ["format"] = "lapin-extension-modules", ["version"] = 1, ["modules"] = a }.ToJsonString(new() { WriteIndented = true }) + "\n");
    }

    /// <summary>
    /// What is gone at this start, before anything loads: uninstalled extensions' folders and their lines, versions no line names
    /// (the one an update replaced), half-unpacked folders, and folders of extensions modules.json does not list.
    /// </summary>
    public static List<ExtensionEntry> Sweep(string root)
    {
        var entries = ReadList(root);
        if (!Directory.Exists(root)) return entries;
        var kept = entries.Where(e => !e.PendingRemoval).ToList();
        if (kept.Count != entries.Count) WriteList(root, kept);
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var id = Path.GetFileName(dir);
            var entry = kept.FirstOrDefault(e => e.Id == id);
            if (entry == null) { TryDelete(dir); continue; }
            foreach (var v in Directory.EnumerateDirectories(dir))
                if (Path.GetFileName(v) != entry.Version) TryDelete(v);
        }
        return kept;
    }

    static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Console.Error.WriteLine($"lapin: could not remove {dir}: {e.Message}"); }
    }

    /// <summary>The installed extensions, swept, checked and (those that pass) loaded. Their modules then go to <see cref="ModuleCatalog.Create(IEnumerable{ILapinModule}, IEnumerable{ILapinModule}, Action{ILapinModule, Exception})"/>.</summary>
    [RequiresUnreferencedCode(Untrimmed)]
    public static List<ExtensionState> Load(ExtensionHostInfo host)
    {
        var o = new List<ExtensionState>();
        var moduleIds = new HashSet<string>(host.Modules);
        var entries = Sweep(host.Root);
        // The newest revocation list the engine verified (§4.3), kept from an earlier start: offline, it is the one used.
        var revoked = entries.Count > 0 ? ExtensionRevocations.ReadStored(host.Root, host.Keys) : null;
        foreach (var entry in entries)
        {
            var state = new ExtensionState(entry);
            o.Add(state);
            try
            {
                // Withdrawn by the catalogue: refused before anything of it is read but its kept manifest (its name and SHA-256).
                var kept = KeptManifest(host.Root, entry);
                if (revoked?.Match(entry.Id, entry.Version, kept?.Sha256) is { } withdrawn)
                {
                    state.Manifest = kept;
                    state.Revocation = withdrawn;
                    state.Fail(ExtensionRevocations.Problem(withdrawn), ExtensionRevocations.Kind);
                    continue;
                }
                var c = Verify(host, entry, state);
                if (!entry.Enabled) { state.Status = "disabled"; continue; }
                // Its module id (its id's last part) names its commands, its CSS and its interface module: one of a kind.
                var mid = c.Manifest.ModuleId;
                if (host.Reserved?.Contains(mid) == true || !moduleIds.Add(mid)) throw new ExtensionException($"Its module „{mid}” has the id of a module Lapin has already (built in, or another extension's).");
                if (c.Module is { } m)
                {
                    if (LapinContract.Problem(m.Contract) is { } contract) throw new ExtensionException(contract);
                    foreach (var r in m.RequiredIds())
                    {
                        if (!host.Modules.Contains(r) && r != CoreModule.ModuleId) throw new ExtensionException($"It needs „{r}”, which this edition does not have.");
                        if (LapinContract.ApiProblem(r, m.RangeOf(r), host.Apis.TryGetValue(r, out var api) ? api : null) is { } apiProblem) throw new ExtensionException(apiProblem);
                    }
                    state.Module = Instantiate(state, m);
                }
                state.Status = "loaded";
            }
            catch (ExtensionException e) { state.Fail(e.Message); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or TypeLoadException or MissingMethodException or System.Reflection.ReflectionTypeLoadException or System.Reflection.TargetInvocationException or InvalidOperationException or InvalidCastException or FileLoadException)
            {
                state.Fail($"It could not be loaded: {e.GetType().Name}: {e.Message}");
            }
        }
        return o;
    }

    /// <summary>
    /// An installed version checked whole, at every start: its kept zip against the store's signature in its kept manifest, its
    /// files list from that zip, and the unpacked folder against the list, file by file, with no file more. Its interface texts are
    /// read from the checked bytes.
    /// </summary>
    static ExtensionPackage.Checked Verify(ExtensionHostInfo host, ExtensionEntry entry, ExtensionState state)
    {
        var dir = Path.Combine(host.Root, entry.Id, entry.Version);
        if (!Directory.Exists(dir)) throw new ExtensionException("Its files are missing: install it again.");
        var manifestPath = Path.Combine(dir, ExtensionPackage.KeptManifest);
        var zipPath = Path.Combine(dir, ExtensionPackage.KeptZip);
        if (!File.Exists(manifestPath) || !File.Exists(zipPath)) throw new ExtensionException("Its package is missing: install it again.");
        ExtensionManifest manifest;
        try { manifest = ExtensionManifest.Parse(JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject ?? throw new ExtensionException("Its manifest is damaged: install it again.")); }
        catch (System.Text.Json.JsonException) { throw new ExtensionException("Its manifest is damaged: install it again."); }
        state.Manifest = manifest;
        if (manifest.Id != entry.Id || manifest.Version != entry.Version) throw new ExtensionException("Its folder holds another extension or version.");
        var c = ExtensionPackage.Check(File.ReadAllBytes(zipPath), manifest, host.Keys);

        var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var root = Path.GetFullPath(dir);
        foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/');
            if (rel is ExtensionPackage.KeptZip or ExtensionPackage.KeptManifest or ExtensionPackage.FilesList) continue;
            if (!c.Files.TryGetValue(rel, out var want)) throw new ExtensionException($"Its folder holds „{rel}”, which is not in its package: it was not loaded.");
            var b = File.ReadAllBytes(f);
            if (b.LongLength != want.Size || ExtensionPackage.Sha256Hex(b) != want.Sha256) throw new ExtensionException($"Its file „{rel}” was changed: it was not loaded. Install it again.");
            bytes[rel] = b;
        }
        foreach (var rel in c.Files.Keys)
            if (!bytes.ContainsKey(rel)) throw new ExtensionException($"Its file „{rel}” is missing: install it again.");

        state.Contract = c.Module?.Contract;
        state.ModuleId = c.Module?.Id;
        state.Js = bytes.TryGetValue("ui/plugin.js", out var js) ? Encoding.UTF8.GetString(js) : null;
        state.Css = bytes.TryGetValue("ui/plugin.css", out var css) ? Encoding.UTF8.GetString(css) : null;
        foreach (var (k, v) in bytes)
            if (k.StartsWith("ui/i18n/", StringComparison.Ordinal) && k.EndsWith(".json", StringComparison.Ordinal) && k.Count(ch => ch == '/') == 2)
                state.Phrases[k["ui/i18n/".Length..^".json".Length]] = Encoding.UTF8.GetString(v);
        state.License = bytes.TryGetValue("LICENSE.txt", out var lic) ? Encoding.UTF8.GetString(lic) : null;
        state.Assemblies = bytes.Where(x => x.Key.StartsWith("engine/", StringComparison.Ordinal) && x.Key.Count(ch => ch == '/') == 1 && x.Key.EndsWith(".dll", StringComparison.Ordinal))
            .ToDictionary(x => x.Key["engine/".Length..^".dll".Length], x => (x.Value, bytes.GetValueOrDefault(x.Key[..^".dll".Length] + ".pdb")), StringComparer.OrdinalIgnoreCase);
        return c;
    }

    /// <summary>The catalogue manifest kept beside an installed version, read as it is (not checked: Verify does that); null when it cannot be read.</summary>
    internal static ExtensionManifest? KeptManifest(string root, ExtensionEntry entry)
    {
        try
        {
            var path = Path.Combine(root, entry.Id, entry.Version, ExtensionPackage.KeptManifest);
            return File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject j ? ExtensionManifest.Parse(j) : null;
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ExtensionException or InvalidOperationException) { return null; }
    }

    [RequiresUnreferencedCode(Untrimmed)]
    static ILapinModule Instantiate(ExtensionState state, ModuleLoader.ModuleManifest m)
    {
        var simple = m.Assembly[..^".dll".Length];
        var ctx = new ExtensionLoadContext(state.Id, state.Assemblies!);
        var asm = ctx.LoadOwn(simple);
        var type = asm.GetTypes().FirstOrDefault(t => typeof(ILapinModule).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                   ?? throw new ExtensionException($"{m.Assembly} has no engine module (ILapinModule) Lapin can make.");
        var module = (ILapinModule)Activator.CreateInstance(type)!;
        if (module.Id != m.Id) throw new ExtensionException($"{m.Assembly} says it is „{module.Id}”, its module.json says „{m.Id}”.");
        return module;
    }

    /// <summary>
    /// The extensions' modules registered by <see cref="ModuleCatalog"/>: one whose <c>Register</c> threw (an extension built
    /// against an API that changed) is turned off with why, and nothing of it stays in the catalog.
    /// </summary>
    public static void Failed(IEnumerable<ExtensionState> states, ILapinModule module, Exception e)
    {
        var s = states.FirstOrDefault(x => ReferenceEquals(x.Module, module));
        s?.Fail($"It could not start: {e.GetType().Name}: {e.Message}");
    }
}
