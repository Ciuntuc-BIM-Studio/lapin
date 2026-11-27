using System.Reflection;
using System.Runtime.Loader;

namespace Lapin.Platform;

/// <summary>
/// One extension's own assembly load context (docs/plan-extension-modules.md §2.1, step 4), not collectible: a module's delegates
/// sit in the catalog, the session and its caches. An assembly the host or a product module has already loaded (Lapin.Model,
/// Lapin.Platform, Lapin.Planning…) and the framework's are the Default context's copies, so <see cref="ILapinModule"/> stays one
/// type whatever version the extension was built against; everything else comes from the extension's own engine folder only,
/// so two extensions may carry different versions of the same package, and neither reaches a product module.
/// <para>
/// The assemblies are loaded from the bytes the loader has just checked against FILES.sha256, never read again from the disk: what
/// runs is what was verified, and no file of the folder stays open (Windows locks loaded DLLs).
/// </para>
/// </summary>
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Extension modules are loaded by reflection, which only an untrimmed host (the desktop's) can do.")]
sealed class ExtensionLoadContext(string name, IReadOnlyDictionary<string, (byte[] Dll, byte[]? Pdb)> assemblies)
    : AssemblyLoadContext($"extension:{name}", isCollectible: false)
{
    static HashSet<string>? _framework;

    /// <summary>The simple names of the framework's assemblies (the trusted platform assemblies the host was started with).</summary>
    static HashSet<string> Framework => _framework ??= ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "")
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Select(Path.GetFileNameWithoutExtension).OfType<string>()
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The extension's assembly named <paramref name="simple"/>, from its checked bytes.</summary>
    public Assembly LoadOwn(string simple)
    {
        var (dll, pdb) = assemblies[simple];
        return pdb == null ? LoadFromStream(new MemoryStream(dll)) : LoadFromStream(new MemoryStream(dll), new MemoryStream(pdb));
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var simple = assemblyName.Name;
        if (simple == null) return null;
        // Shared: what the Default context holds already, whatever its version.
        foreach (var a in Default.Assemblies)
            if (string.Equals(a.GetName().Name, simple, StringComparison.OrdinalIgnoreCase)) return a;
        // The framework's, never the extension's own copy of it (null sends the name on to the Default context).
        if (Framework.Contains(simple) || simple is "mscorlib" or "netstandard" || simple.StartsWith("System.", StringComparison.Ordinal)) return null;
        return assemblies.ContainsKey(simple) ? LoadOwn(simple) : null;
    }

    /// <summary>No native libraries of its own (§1.1): what the extension asks for is the process's, as for any program.</summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) => IntPtr.Zero;
}
