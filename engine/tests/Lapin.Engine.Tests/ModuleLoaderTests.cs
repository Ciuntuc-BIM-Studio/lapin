using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>A product's modules are loaded from their folders; a module whose requirements the product lacks is refused.</summary>
public sealed class ModuleLoaderTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-modules-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>engine/artifacts/modules, where the build puts every module.</summary>
    static string BuiltModules()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "artifacts/modules/architecture"))) return Path.Combine(d.FullName, "artifacts/modules");
        throw new InvalidOperationException("Build the engine first: no artifacts/modules/architecture.");
    }

    string Install(string id, string[]? requires = null)
    {
        var target = Path.Combine(_dir, id);
        Directory.CreateDirectory(target);
        foreach (var f in Directory.GetFiles(Path.Combine(BuiltModules(), "architecture"))) File.Copy(f, Path.Combine(target, Path.GetFileName(f)));
        if (requires != null)
        {
            var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(target, "module.json")))!.AsObject();
            manifest["requires"] = new JsonArray(requires.Select(r => (JsonNode)r).ToArray());
            File.WriteAllText(Path.Combine(target, "module.json"), manifest.ToJsonString());
        }
        return target;
    }

    [Fact]
    public void Loads_the_products_engine_modules_and_skips_ui_only_ones_and_core()
    {
        Install("architecture");
        var modules = ModuleLoader.Load(_dir, ["core", "architecture", "drafting"]);
        Assert.Equal(["architecture"], modules.Select(m => m.Id));
        var catalog = ModuleCatalog.Create(modules);
        Assert.Equal(["core", "architecture"], catalog.Modules.Select(m => m.Id));
        Assert.Equal("architecture", catalog.OwnerOf("IfcWall"));
        Assert.Equal("core", catalog.OwnerOf("IfcBuildingStorey"));
        Assert.NotNull(catalog.Command("architecture.addWall"));
    }

    [Fact]
    public void A_module_whose_requirement_is_missing_is_refused()
    {
        Install("architecture", ["structure"]);
        var e = Assert.Throws<InvalidOperationException>(() => ModuleLoader.Load(_dir, ["core", "architecture"]));
        Assert.Contains("needs „structure”", e.Message);
    }

    [Fact]
    public void A_class_has_one_owner()
    {
        var e = Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([new Architecture.ArchitectureModule(), new Twin()]));
        Assert.Contains("IfcWall is owned by „architecture”", e.Message);
        Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([new Grabby()]));
    }

    sealed class Twin : ILapinModule
    {
        public string Id => "twin";
        public string Name => "Twin";
        public void Register(IModuleRegistry r) => r.Class(new("IfcWall", "Wall", []));
    }

    sealed class Grabby : ILapinModule
    {
        public string Id => "grabby";
        public string Name => "Grabby";
        public void Register(IModuleRegistry r) => r.Class(new("IfcBuildingStorey", "Storey", []));
    }
}
