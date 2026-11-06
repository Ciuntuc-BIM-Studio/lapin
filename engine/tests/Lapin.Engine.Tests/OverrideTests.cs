using System.Reflection;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Overrides (docs/overrides.md): a module wraps another's command or query by its full id, the original called through
/// <c>next</c>, in the original's one undo step, under its name and owner; an unknown id refused at load; none in the
/// editions of this repository.
/// </summary>
public sealed class OverrideTests
{
    /// <summary>A module that wraps addWall, writing down when it runs (before and after the command it wraps).</summary>
    sealed class Wrapper(string id, List<string> log, Func<JsonObject, JsonObject>? args = null) : ILapinModule
    {
        public string Id => id;
        public string Name => id;
        public void Register(IModuleRegistry r) => r.Override("architecture.addWall", (ctx, a, next) =>
        {
            log.Add($"{id}>");
            var result = next(args?.Invoke(a) ?? a);
            log.Add($"<{id}");
            return result;
        });
    }

    static JsonObject Wall(string storey, double x2 = 5) => new() { ["storeyId"] = storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = x2, ["y2"] = 0, ["join"] = false };

    [Fact]
    public void Overrides_wrap_in_load_order_and_next_calls_the_original()
    {
        var log = new List<string>();
        var f = new Fixture(new ArchitectureModule(), new Wrapper("first", log), new Wrapper("second", log));
        var id = (string)f.Run("architecture.addWall", Wall(f.Storey))!["id"]!;
        Assert.NotNull(f.Model.Get(id));
        // The one loaded last wraps the one before it.
        Assert.Equal(["second>", "first>", "<first", "<second"], log);
        Assert.Equal([new OverrideInfo("command", "architecture.addWall", "first"), new OverrideInfo("command", "architecture.addWall", "second")], f.Session.Catalog.Overrides);
        // The command stays the original's: its owner and label (so its undo step), what a guest may call by its name.
        var entry = f.Session.Catalog.Command("architecture.addWall")!;
        Assert.Equal("architecture", entry.Module);
        Assert.Equal(f.Session.History.UndoLabel, entry.Label);
        Assert.Equal(f.Session.Catalog.Overrides, f.Session.Info().Overrides);
    }

    [Fact]
    public void An_override_changes_the_args_and_the_result()
    {
        var f = new Fixture(new ArchitectureModule(), new Wrapper("longer", [], a => { var c = (JsonObject)a.DeepClone(); c["x2"] = 7; return c; }));
        var id = (string)f.Run("architecture.addWall", Wall(f.Storey))!["id"]!;
        Assert.Equal(7, (double)f.Model.Get(id)!.Attrs["end"]![0]!, 9);
    }

    /// <summary>Calls the original twice (two walls) and adds to its result: one transaction still.</summary>
    sealed class Twice : ILapinModule
    {
        public string Id => "twice";
        public string Name => "Twice";
        public void Register(IModuleRegistry r) => r.Override("architecture.addWall", (ctx, a, next) =>
        {
            var one = next(a)!.AsObject();
            var b = (JsonObject)a.DeepClone();
            b["y1"] = 3; b["y2"] = 3;
            one["twin"] = next(b)!["id"]!.DeepClone();
            return one;
        });
    }

    [Fact]
    public void Everything_an_override_does_is_the_commands_one_undo_step()
    {
        var f = new Fixture(new ArchitectureModule(), new Twice());
        var r = f.Run("architecture.addWall", Wall(f.Storey))!;
        Assert.Equal(2, f.Model.OfClass(IfcClass.Wall).Count());
        Assert.NotNull(f.Model.Get((string)r["twin"]!));
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(IfcClass.Wall));
    }

    /// <summary>Replaces addWall: refuses walls shorter than a metre, never calling the original for them.</summary>
    sealed class Refuser : ILapinModule
    {
        public string Id => "refuser";
        public string Name => "Refuser";
        public void Register(IModuleRegistry r) => r.Override("architecture.addWall", (ctx, a, next) =>
            (double?)a["x2"] < 1 ? throw new CommandException("Too short.") : next(a));
    }

    [Fact]
    public void An_override_may_replace_the_command_and_a_refusal_changes_nothing()
    {
        var f = new Fixture(new ArchitectureModule(), new Refuser());
        Assert.Equal("Too short.", Assert.Throws<CommandException>(() => f.Run("architecture.addWall", Wall(f.Storey, 0.5))).Message);
        Assert.Empty(f.Model.OfClass(IfcClass.Wall));
        Assert.False(f.Session.History.CanUndo);
        f.Run("architecture.addWall", Wall(f.Storey, 2));
        Assert.Single(f.Model.OfClass(IfcClass.Wall));
    }

    sealed class Bad(string name, bool query = false) : ILapinModule
    {
        public string Id => "bad";
        public string Name => "Bad";
        public void Register(IModuleRegistry r)
        {
            if (query) r.OverrideQuery(name, (ctx, a, next) => next(a));
            else r.Override(name, (ctx, a, next) => next(a));
        }
    }

    [Fact]
    public void Overriding_what_the_product_lacks_fails_at_load()
    {
        var e = Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([new ArchitectureModule(), new Bad("architecture.nope")]));
        Assert.Contains("„bad” overrides the command architecture.nope, which no module of this product registers", e.Message);
        e = Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([new Bad("structure.addColumn")]));
        Assert.Contains("structure.addColumn", e.Message);
        e = Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([new Bad("probe.nope", query: true)]));
        Assert.Contains("overrides the query probe.nope", e.Message);
        e = Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([new ArchitectureModule(), new Bad("addWall")]));
        Assert.Contains("name it in full", e.Message);
    }

    /// <summary>A module with a query, and one that wraps it.</summary>
    sealed class Asked : ILapinModule
    {
        public string Id => "probe";
        public string Name => "Probe";
        public void Register(IModuleRegistry r) => r.Query("echo", (ctx, a) => new JsonObject { ["said"] = (string?)a["say"] });
    }

    sealed class Louder : ILapinModule
    {
        public string Id => "louder";
        public string Name => "Louder";
        public void Register(IModuleRegistry r) => r.OverrideQuery("probe.echo", (ctx, a, next) =>
            new JsonObject { ["said"] = ((string?)next(new JsonObject { ["say"] = ((string?)a["say"])?.ToUpperInvariant() })!["said"]) + "!" });
    }

    [Fact]
    public void A_query_is_overridden_the_same_way()
    {
        Assert.Equal("hi", (string?)new Fixture(new ArchitectureModule(), new Asked()).Session.Query("probe.echo", new JsonObject { ["say"] = "hi" })!["said"]);
        var f = new Fixture(new ArchitectureModule(), new Louder(), new Asked());
        // Wrapped though it loaded before the module whose query it wraps.
        Assert.Equal("HI!", (string?)f.Session.Query("probe.echo", new JsonObject { ["say"] = "hi" })!["said"]);
        Assert.Equal([new OverrideInfo("query", "probe.echo", "louder")], f.Session.Catalog.Overrides);
    }

    [Fact]
    public void Without_overrides_the_catalog_is_as_it_was()
    {
        var f = new Fixture(new ArchitectureModule());
        Assert.Empty(f.Session.Catalog.Overrides);
        Assert.Null(f.Session.Info().Overrides);
        // Nothing about overrides goes out to the interface.
        Assert.DoesNotContain("overrides", BinaryFrame.HeaderText(BinaryFrame.Encode(new Reply(1, f.Session.Info()))));
    }

    // ------------------------------------------------------------ the lapin edition

    static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "products/lapin.json"))) return d.FullName;
        throw new InvalidOperationException("No products/lapin.json above the tests.");
    }

    /// <summary>
    /// The lapin edition's engine modules (products/lapin.json completed with what each module.json requires, as
    /// scripts/product.mjs does) override nothing: an override belongs to another edition (docs/overrides.md).
    /// </summary>
    [Fact]
    public void The_lapin_edition_overrides_nothing()
    {
        var repo = Repo();
        var requires = new Dictionary<string, string[]>();
        foreach (var f in Directory.GetFiles(Path.Combine(repo, "engine/src/Modules"), "module.json", SearchOption.AllDirectories))
        {
            var m = JsonNode.Parse(File.ReadAllText(f))!;
            requires[(string)m["id"]!] = (m["requires"] as JsonArray)?.Select(x => (string)x!).ToArray() ?? [];
        }
        var product = JsonNode.Parse(File.ReadAllText(Path.Combine(repo, "products/lapin.json")))!;
        var ids = new List<string>();
        void Visit(string id)
        {
            if (id == CoreModule.ModuleId || ids.Contains(id) || !requires.TryGetValue(id, out var needs)) return;   // UI-only: no engine half
            foreach (var r in needs) Visit(r);
            ids.Add(id);
        }
        foreach (var id in product["modules"]!.AsArray()) Visit((string)id!);
        Assert.Contains("architecture", ids);

        // Each module from the assemblies beside the tests (the test project references every module of this repository).
        var types = Directory.GetFiles(AppContext.BaseDirectory, "Lapin.*.dll")
            .Select(p => { try { return Assembly.Load(AssemblyName.GetAssemblyName(p)); } catch (BadImageFormatException) { return null; } })
            .OfType<Assembly>()
            .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>().ToArray(); } })
            .Where(t => typeof(ILapinModule).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null && t != typeof(CoreModule));
        var modules = new Dictionary<string, ILapinModule>();
        foreach (var t in types) { var m = (ILapinModule)Activator.CreateInstance(t)!; if (ids.Contains(m.Id)) modules.TryAdd(m.Id, m); }
        Assert.Equal(ids.Order(), modules.Keys.Order());

        var catalog = ModuleCatalog.Create(ids.Select(id => modules[id]));
        Assert.Empty(catalog.Overrides);
    }
}
