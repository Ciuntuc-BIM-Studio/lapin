using System.Reflection;
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
    /// <summary>A module's manifest: its assembly, and the modules it cannot work without.</summary>
    public sealed record ModuleManifest(string Id, string Name, string Assembly, string[]? Requires = null);

    static readonly List<string> SearchDirs = [];
    static bool _hooked;

    public static ProductManifest ReadProduct(string path) =>
        JsonSerializer.Deserialize<ProductManifest>(File.ReadAllText(path), Json.Options)
        ?? throw new InvalidOperationException($"Bad product manifest {path}.");

    public static ModuleManifest ReadModule(string modulesDir, string id)
    {
        var path = Path.Combine(modulesDir, id, "module.json");
        if (!File.Exists(path)) throw new InvalidOperationException($"Module „{id}” is in the product but not installed ({path}).");
        return JsonSerializer.Deserialize<ModuleManifest>(File.ReadAllText(path), Json.Options)
               ?? throw new InvalidOperationException($"Bad module manifest {path}.");
    }

    /// <summary>
    /// The engine modules of <paramref name="moduleIds"/>: every id with an
    /// engine half in <paramref name="modulesDir"/> (a UI-only module has none;
    /// "core" is the platform's). A module whose requirements the product
    /// lacks is refused: the package would be broken.
    /// </summary>
    public static List<ILapinModule> Load(string modulesDir, IEnumerable<string> moduleIds)
    {
        Hook();
        var ids = moduleIds.Where(id => id != CoreModule.ModuleId && Directory.Exists(Path.Combine(modulesDir, id))).ToList();
        var manifests = ids.Select(id => ReadModule(modulesDir, id)).ToList();
        foreach (var m in manifests)
            foreach (var r in m.Requires ?? [])
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

    /// <summary>A module's own dependencies (another module's contract) are looked up in every module folder.</summary>
    static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
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

    public static Assembly[] LoadedModuleAssemblies() =>
        AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name?.StartsWith("Lapin.") == true).ToArray();
}
