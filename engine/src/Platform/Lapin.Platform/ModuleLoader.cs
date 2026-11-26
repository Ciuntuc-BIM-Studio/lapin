using System.Runtime.Loader;
using System.Text.Json;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Loads a product's modules from a folder laid out as
/// <c>&lt;dir&gt;/&lt;moduleId&gt;/module.json + *.dll</c>: what the build writes
/// to engine/artifacts/modules and what a product package ships. A module
/// that is not in the folder does not exist for that product.
/// </summary>
public static class ModuleLoader
{
    /// <summary>
    /// A module's manifest: its assembly, and the modules it cannot work without. <paramref name="Requires"/> is a list of ids
    /// (any version), or for an extension module an object of id → api range (<c>{"planning": "^1"}</c>, docs/plan-extension-modules.md
    /// §1.3). <paramref name="Api"/> is the version of the public surface an open module offers extensions (raised when it breaks);
    /// <paramref name="Contract"/> the platform contract an extension module is built against (<see cref="LapinContract"/>).
    /// </summary>
    public sealed record ModuleManifest(string Id, string Name, string Assembly, System.Text.Json.Nodes.JsonNode? Requires = null, int? Api = null, string? Contract = null)
    {
        /// <summary>The ids of the modules it requires, from either form of <see cref="Requires"/>.</summary>
        public IEnumerable<string> RequiredIds() => Requires switch
        {
            System.Text.Json.Nodes.JsonArray a => a.Select(x => x?.GetValue<string>()).OfType<string>(),
            System.Text.Json.Nodes.JsonObject o => o.Select(x => x.Key),
            _ => [],
        };

        /// <summary>The api range it asks of <paramref name="id"/>: null for a bare list (any).</summary>
        public string? RangeOf(string id) => Requires is System.Text.Json.Nodes.JsonObject o && o[id] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    }

    /// <summary>Why loading from a folder needs the whole code: the desktop host does it, untrimmed; a trimmed host (the browser's) builds its modules in.</summary>
    const string Untrimmed = "Modules are loaded from their folder by reflection, which only an untrimmed host (the desktop's) can do.";

    static readonly List<string> SearchDirs = [];
    static bool _hooked;

    public static ProductManifest ReadProduct(string path) =>
        JsonSerializer.Deserialize(File.ReadAllText(path), PlatformJson.Default.ProductManifest)
        ?? throw new InvalidOperationException($"Bad product manifest {path}.");

    public static ModuleManifest ReadModule(string modulesDir, string id)
    {
        var path = Path.Combine(modulesDir, id, "module.json");
        if (!File.Exists(path)) throw new InvalidOperationException($"Module „{id}” is in the product but not installed ({path}).");
        return ParseModule(File.ReadAllText(path), path);
    }

    /// <summary>A module.json's text read; <paramref name="where"/> names it in the error.</summary>
    public static ModuleManifest ParseModule(string json, string where) =>
        JsonSerializer.Deserialize(json, PlatformJson.Default.ModuleManifest)
        ?? throw new InvalidOperationException($"Bad module manifest {where}.");

    /// <summary>The api number each of a product's modules offers extensions (module.json's <c>api</c>; 1 when it names none). "core" has the contract's major.</summary>
    public static Dictionary<string, int> Apis(string modulesDir, IEnumerable<string> moduleIds)
    {
        var o = new Dictionary<string, int> { [CoreModule.ModuleId] = LapinContract.Major };
        foreach (var id in moduleIds)
            if (id != CoreModule.ModuleId && File.Exists(Path.Combine(modulesDir, id, "module.json")))
                o[id] = ReadModule(modulesDir, id).Api ?? 1;
        return o;
    }

    /// <summary>
    /// The engine modules of <paramref name="moduleIds"/>: every id with an
    /// engine half in <paramref name="modulesDir"/> (a UI-only module has none;
    /// "core" is the platform's). A module whose requirements the product
    /// lacks is refused: the package would be broken.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(Untrimmed)]
    public static List<ILapinModule> Load(string modulesDir, IEnumerable<string> moduleIds)
    {
        Hook();
        var ids = moduleIds.Where(id => id != CoreModule.ModuleId && Directory.Exists(Path.Combine(modulesDir, id))).ToList();
        var manifests = ids.Select(id => ReadModule(modulesDir, id)).ToList();
        foreach (var m in manifests)
            foreach (var r in m.RequiredIds())
                if (r != CoreModule.ModuleId && !ids.Contains(r))
                    throw new InvalidOperationException($"Module „{m.Id}” needs „{r}”, which the product does not have.");

        var o = new List<ILapinModule>();
        foreach (var manifest in manifests)
        {
            var dir = Path.Combine(modulesDir, manifest.Id);
            lock (SearchDirs) SearchDirs.Add(dir);
            var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(Path.Combine(dir, manifest.Assembly)));
            var type = asm.GetTypes().FirstOrDefault(t => typeof(ILapinModule).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                       ?? throw new InvalidOperationException($"{manifest.Assembly} has no ILapinModule.");
            var module = (ILapinModule)Activator.CreateInstance(type)!;
            if (module.Id != manifest.Id) throw new InvalidOperationException($"{manifest.Assembly} says it is „{module.Id}”, the folder says „{manifest.Id}”.");
            o.Add(module);
        }
        return o;
    }

    /// <summary>
    /// A module's own dependencies (another module's contract) are looked up in every module folder; so are the native
    /// libraries a module's packages bring (the Speckle SDK's SQLite), under <c>runtimes/&lt;rid&gt;/native</c> as NuGet lays
    /// them, or beside its assemblies.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(Untrimmed)]
    static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) =>
        {
            lock (SearchDirs)
                foreach (var dir in SearchDirs)
                    foreach (var p in NativeCandidates(dir, name))
                        if (File.Exists(p)) return System.Runtime.InteropServices.NativeLibrary.Load(Path.GetFullPath(p));
            return IntPtr.Zero;
        };
        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            lock (SearchDirs)
                foreach (var dir in SearchDirs)
                {
                    var p = Path.Combine(dir, name.Name + ".dll");
                    if (File.Exists(p)) return ctx.LoadFromAssemblyPath(Path.GetFullPath(p));
                }
            return null;
        };
    }

    /// <summary>Where a native library named <paramref name="name"/> (e_sqlite3) may lie in a module's folder: this platform's runtimes folder, then beside the assemblies.</summary>
    public static IEnumerable<string> NativeCandidates(string dir, string name)
    {
        var file = OperatingSystem.IsWindows() ? $"{name}.dll" : OperatingSystem.IsMacOS() ? $"lib{name}.dylib" : $"lib{name}.so";
        if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dylib", StringComparison.Ordinal) || name.Contains(".so", StringComparison.Ordinal)) file = name;
        var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
        yield return Path.Combine(dir, "runtimes", rid, "native", file);
        yield return Path.Combine(dir, file);
    }
}
