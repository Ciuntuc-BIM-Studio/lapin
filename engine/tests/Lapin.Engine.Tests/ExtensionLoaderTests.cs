using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>What the fixture extension modules call from their <c>Register</c> (their IL is made by <see cref="ExtensionLoaderTests"/>).</summary>
public static class ExtensionFixture
{
    /// <summary>0: registers <c>&lt;id&gt;.ping</c>; 1: registers it, then throws as a module built against a changed API would; 2: names a command outside its id; 3: overrides another module's command.</summary>
    public static void Register(IModuleRegistry r, string id, string label, int mode)
    {
        if (mode == 2) { r.Command("architecture.evil", label, (_, _) => null); return; }
        if (mode == 3) { r.Override("core.rename", (ctx, args, next) => next(args)); return; }
        r.Command("ping", label, (_, _) => JsonValue.Create(label));
        r.Catalog("things", new JsonObject { ["label"] = label });
        if (mode == 1) throw new MissingMethodException("Lapin.Planning.Plan", "Gone");
    }
}

/// <summary>
/// Extension modules' engine loader (docs/plan-extension-modules.md §2.1, S2): a fixture module made here (its IL emitted with
/// PersistedAssemblyBuilder, no compiler needed), packed as a .lapinext signed with a throwaway TEST key, installed under a temporary
/// folder and loaded as the desktop host does at its start. Nothing is written outside the temporary folder, nothing goes to the network.
/// </summary>
public sealed class ExtensionLoaderTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "lapin-ext-tests", Guid.NewGuid().ToString("N"));
    static readonly byte[] Seed = Enumerable.Range(1, 32).Select(i => (byte)(i * 7)).ToArray();
    static readonly byte[] OtherSeed = Enumerable.Range(1, 32).Select(i => (byte)(i * 11)).ToArray();
    static StoreKey KeyOf(byte[] seed) { var pub = Ed25519.PublicKey(seed); return new StoreKey(StoreKey.IdOf(pub), Convert.ToBase64String(pub)); }
    static readonly StoreKey TestKey = KeyOf(Seed);

    public void Dispose() { try { Directory.Delete(_root, true); } catch (DirectoryNotFoundException) { } }

    ExtensionHostInfo Host(params string[] modules) => new(_root, [TestKey],
        (modules.Length == 0 ? ["core", "phases", "planning", "cost", "architecture"] : modules).ToHashSet(),
        new Dictionary<string, int> { ["core"] = 1, ["planning"] = 1, ["cost"] = 1, ["phases"] = 1, ["architecture"] = 1 },
        new HashSet<string> { "planning", "cost", "architecture", "structure" });

    // ------------------------------------------------------------------ the fixture

    /// <summary>A dependency assembly, Lapin.FixtureDep at <paramref name="version"/>, whose Info.Version() says it.</summary>
    static byte[] EmitDep(int version)
    {
        var ab = new PersistedAssemblyBuilder(new AssemblyName("Lapin.FixtureDep") { Version = new Version(version, 0, 0, 0) }, typeof(object).Assembly);
        var tb = ab.DefineDynamicModule("Lapin.FixtureDep").DefineType("FixtureDep.Info", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class, typeof(object));
        var m = tb.DefineMethod("Version", MethodAttributes.Public | MethodAttributes.Static, typeof(string), Type.EmptyTypes);
        var il = m.GetILGenerator();
        il.Emit(OpCodes.Ldstr, $"dep {version}");
        il.Emit(OpCodes.Ret);
        tb.CreateType();
        var ms = new MemoryStream();
        ab.Save(ms);
        return ms.ToArray();
    }

    /// <summary>A module assembly whose ILapinModule is <paramref name="moduleId"/> and whose Register calls <see cref="ExtensionFixture.Register"/>, its label from <paramref name="dep"/>'s Info.Version() when given.</summary>
    static byte[] EmitModule(string moduleId, int mode = 0, byte[]? dep = null, string? claimedId = null)
    {
        MethodInfo? fromDep = null;
        AssemblyLoadContext? scratch = null;
        if (dep != null)
        {
            scratch = new AssemblyLoadContext("fixture-dep", isCollectible: true);
            fromDep = scratch.LoadFromStream(new MemoryStream(dep)).GetType("FixtureDep.Info")!.GetMethod("Version");
        }
        var name = "Ext." + moduleId;
        var ab = new PersistedAssemblyBuilder(new AssemblyName(name) { Version = new Version(1, 0, 0, 0) }, typeof(object).Assembly);
        var tb = ab.DefineDynamicModule(name).DefineType("Fixture.Module", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, typeof(object), [typeof(ILapinModule)]);
        tb.DefineDefaultConstructor(MethodAttributes.Public);
        const MethodAttributes Impl = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot;
        void Getter(string prop, string value)
        {
            var g = tb.DefineMethod("get_" + prop, Impl | MethodAttributes.SpecialName, typeof(string), Type.EmptyTypes);
            var il = g.GetILGenerator();
            il.Emit(OpCodes.Ldstr, value);
            il.Emit(OpCodes.Ret);
            tb.DefineMethodOverride(g, typeof(ILapinModule).GetProperty(prop)!.GetMethod!);
        }
        Getter("Id", claimedId ?? moduleId);
        Getter("Name", "Fixture " + moduleId);
        var reg = tb.DefineMethod("Register", Impl, typeof(void), [typeof(IModuleRegistry)]);
        var r = reg.GetILGenerator();
        r.Emit(OpCodes.Ldarg_1);
        r.Emit(OpCodes.Ldstr, moduleId);
        if (fromDep != null) r.Emit(OpCodes.Call, fromDep); else r.Emit(OpCodes.Ldstr, "Ping");
        r.Emit(OpCodes.Ldc_I4, mode);
        r.Emit(OpCodes.Call, typeof(ExtensionFixture).GetMethod(nameof(ExtensionFixture.Register))!);
        r.Emit(OpCodes.Ret);
        tb.DefineMethodOverride(reg, typeof(ILapinModule).GetMethod(nameof(ILapinModule.Register))!);
        tb.CreateType();
        var ms = new MemoryStream();
        ab.Save(ms);
        scratch?.Unload();
        return ms.ToArray();
    }

    sealed record Package(JsonObject Manifest, byte[] Zip);

    /// <summary>
    /// A signed .lapinext of the extension <c>ro.test.&lt;moduleId&gt;</c>. <paramref name="edit"/> changes the files before FILES.sha256
    /// is made, <paramref name="afterList"/> after it; <paramref name="manifest"/> the catalogue manifest before it is signed.
    /// </summary>
    static Package Pack(string moduleId = "fixture", string version = "1.0.0", int mode = 0, byte[]? dep = null, string contract = "1.x",
        JsonNode? requires = null, Action<SortedDictionary<string, byte[]>>? edit = null, Action<SortedDictionary<string, byte[]>>? afterList = null,
        Action<JsonObject>? manifest = null, byte[]? signWith = null, bool sign = true, string? claimedId = null)
    {
        var id = "ro.test." + moduleId;
        var asm = $"Ext.{moduleId}.dll";
        var m = new JsonObject
        {
            ["id"] = id, ["version"] = version, ["kind"] = "module", ["name"] = $"Fixture {moduleId}", ["license"] = "LicenseRef-Test",
            ["lapin"] = ">=0.1.0", ["permissions"] = new JsonArray("full"), ["engine"] = new JsonObject { ["assembly"] = asm }, ["contract"] = contract,
            ["ui"] = new JsonObject(), ["sdk"] = "1.x",
        };
        manifest?.Invoke(m);
        var module = new JsonObject { ["id"] = moduleId, ["name"] = $"Fixture {moduleId}", ["assembly"] = asm, ["contract"] = contract, ["requires"] = requires?.DeepClone() ?? new JsonObject { ["planning"] = "^1" } };
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["manifest.json"] = Encoding.UTF8.GetBytes(m.ToJsonString()),
            ["module.json"] = Encoding.UTF8.GetBytes(module.ToJsonString()),
            ["engine/" + asm] = EmitModule(moduleId, mode, dep, claimedId),
            ["ui/plugin.js"] = Encoding.UTF8.GetBytes($"export default {{ id: '{moduleId}' }};\n"),
            ["ui/i18n/ro.json"] = Encoding.UTF8.GetBytes("{\"phrases\":{\"Ping\":\"Ping\"},\"patterns\":[]}"),
            ["LICENSE.txt"] = Encoding.UTF8.GetBytes("Test licence.\n"),
        };
        if (dep != null) files["engine/Lapin.FixtureDep.dll"] = dep;
        edit?.Invoke(files);
        var list = new StringBuilder();
        foreach (var (path, bytes) in files) list.Append($"{ExtensionPackage.Sha256Hex(bytes)} {bytes.Length} {path}\n");
        files[ExtensionPackage.FilesList] = Encoding.UTF8.GetBytes(list.ToString());
        afterList?.Invoke(files);
        var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, bytes) in files)
            {
                using var s = z.CreateEntry(path).Open();
                s.Write(bytes);
            }
        var zip = ms.ToArray();
        var sha = ExtensionPackage.Sha256Hex(zip);
        var p = new JsonObject { ["url"] = $"https://example.invalid/{id}-{version}.lapinext", ["sha256"] = sha, ["size"] = zip.Length };
        if (sign)
        {
            var seed = signWith ?? Seed;
            p["signature"] = new JsonObject { ["keyId"] = KeyOf(seed).Id, ["value"] = Convert.ToBase64String(Ed25519.Sign(seed, StoreKeys.PackageMessage(id, version, sha, zip.Length))) };
        }
        m["package"] = p;
        return new Package(m, zip);
    }

    JsonObject Install(Package p, ExtensionHostInfo? host = null) =>
        new ExtensionInstaller(host ?? Host(), []).Install(new JsonObject { ["manifest"] = p.Manifest.DeepClone(), ["data"] = Convert.ToBase64String(p.Zip) });

    /// <summary>A start of the engine: the extensions loaded, then the catalog made with them.</summary>
    (List<ExtensionState> States, ModuleCatalog Catalog) Start(ExtensionHostInfo? host = null)
    {
        var states = ExtensionLoader.Load(host ?? Host());
        var catalog = ModuleCatalog.Create([], states.Where(s => s.Module != null).Select(s => s.Module!), (m, e) => ExtensionLoader.Failed(states, m, e));
        return (states, catalog);
    }

    string VersionDir(string moduleId = "fixture", string version = "1.0.0") => Path.Combine(_root, "ro.test." + moduleId, version);

    // ------------------------------------------------------------------ the cases of the design's table

    [Fact]
    public void A_signed_extension_loads_and_registers_its_command()
    {
        Assert.True((bool)Install(Pack())["restart"]!);
        var (states, catalog) = Start();
        var s = Assert.Single(states);
        Assert.Equal("loaded", s.Status);
        Assert.Null(s.Problem);
        var cmd = catalog.Command("fixture.ping");
        Assert.NotNull(cmd);
        Assert.Equal("fixture", cmd!.Module);
        Assert.Contains(catalog.Modules, m => m.Id == "fixture");
        Assert.True(catalog.Catalogs.ContainsKey("fixture.things"));
        // The module is the host's ILapinModule (one type across contexts) in a context of its own.
        Assert.NotSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(s.Module!.GetType().Assembly));
        // Its interface half, from the checked bytes.
        var list = new ExtensionInstaller(Host(), states).Modules([]);
        var e = Assert.Single(list["installed"]!.AsArray())!;
        Assert.Equal("loaded", (string?)e["status"]);
        Assert.Equal("fixture", (string?)e["module"]);
        var plugin = Assert.Single(list["modules"]!.AsArray())!;
        Assert.Equal("ro.test.fixture", (string?)plugin["id"]);
        Assert.Equal("1.x", (string?)plugin["sdk"]);
        Assert.Contains("export default", (string?)plugin["js"]);
        Assert.Contains("phrases", (string?)plugin["phrases"]!["ro"]);
        Assert.Equal("1.0", (string?)list["contract"]);
        Assert.False((bool)list["restart"]!);
    }

    [Fact]
    public void A_changed_file_disables_the_extension_and_the_engine_still_starts()
    {
        Install(Pack());
        var dll = Path.Combine(VersionDir(), "engine", "Ext.fixture.dll");
        var b = File.ReadAllBytes(dll);
        b[^1] ^= 0xFF;
        File.WriteAllBytes(dll, b);
        var (states, catalog) = Start();
        Assert.Equal("failed", states[0].Status);
        Assert.Contains("was changed", states[0].Problem);
        Assert.Null(catalog.Command("fixture.ping"));
        Assert.NotNull(catalog.Command("core.rename") ?? catalog.Commands.FirstOrDefault());
    }

    [Fact]
    public void A_file_added_to_the_folder_or_one_missing_disables_it()
    {
        Install(Pack());
        File.WriteAllBytes(Path.Combine(VersionDir(), "engine", "Evil.dll"), [0x4D, 0x5A]);
        Assert.Contains("not in its package", Start().States[0].Problem);
        File.Delete(Path.Combine(VersionDir(), "engine", "Evil.dll"));
        Assert.Equal("loaded", Start().States[0].Status);
        File.Delete(Path.Combine(VersionDir(), "LICENSE.txt"));
        Assert.Contains("missing", Start().States[0].Problem);
    }

    [Fact]
    public void A_package_whose_files_differ_from_its_list_is_refused_at_install()
    {
        var p = Pack(afterList: f => f["ui/plugin.js"] = Encoding.UTF8.GetBytes("changed"));
        Assert.Contains("not the file", Assert.Throws<ExtensionException>(() => Install(p)).Message);
        var q = Pack(afterList: f => f["engine/Extra.pdb"] = [1, 2, 3]);
        Assert.Contains("not in FILES.sha256", Assert.Throws<ExtensionException>(() => Install(q)).Message);
        Assert.False(Directory.Exists(Path.Combine(_root, "ro.test.fixture", "1.0.0")));
    }

    [Fact]
    public void A_wrong_signature_is_refused_at_install_and_at_start()
    {
        // Signed by a key Lapin does not carry.
        Assert.Contains("does not hold", Assert.Throws<ExtensionException>(() => Install(Pack(signWith: OtherSeed))).Message);
        // A zip other than the one signed.
        var p = Pack();
        var other = Pack(version: "1.0.0", edit: f => f["LICENSE.txt"] = Encoding.UTF8.GetBytes("Another licence."));
        Assert.Contains("not the one the store signed", Assert.Throws<ExtensionException>(() => Install(p with { Zip = other.Zip })).Message);
        // Installed right, then its kept manifest's signature changed: refused at the next start.
        Install(p);
        var kept = Path.Combine(VersionDir(), ExtensionPackage.KeptManifest);
        var j = JsonNode.Parse(File.ReadAllText(kept))!.AsObject();
        var sig = Convert.FromBase64String((string)j["package"]!["signature"]!["value"]!);
        sig[5] ^= 1;
        j["package"]!["signature"]!["value"] = Convert.ToBase64String(sig);
        File.WriteAllText(kept, j.ToJsonString());
        var (states, catalog) = Start();
        Assert.Contains("does not hold", states[0].Problem);
        Assert.Null(catalog.Command("fixture.ping"));
        // The kept zip replaced by another with a matching kept manifest (re-hashed but not re-signed): refused.
        Install(p);
        File.WriteAllBytes(Path.Combine(VersionDir(), ExtensionPackage.KeptZip), other.Zip);
        var j2 = JsonNode.Parse(File.ReadAllText(kept))!.AsObject();
        j2["package"]!["sha256"] = ExtensionPackage.Sha256Hex(other.Zip);
        File.WriteAllText(kept, j2.ToJsonString());
        Assert.Contains("does not hold", Start().States[0].Problem);
    }

    [Fact]
    public void An_unsigned_module_is_refused()
    {
        Assert.Contains("not signed", Assert.Throws<ExtensionException>(() => Install(Pack(sign: false))).Message);
        // Installed signed, its signature then taken out of the kept manifest.
        Install(Pack());
        var kept = Path.Combine(VersionDir(), ExtensionPackage.KeptManifest);
        var j = JsonNode.Parse(File.ReadAllText(kept))!.AsObject();
        j["package"]!.AsObject().Remove("signature");
        File.WriteAllText(kept, j.ToJsonString());
        Assert.Contains("not signed", Start().States[0].Problem);
    }

    [Fact]
    public void A_newer_contract_is_refused_with_why()
    {
        Install(Pack(contract: "1.9"));
        var s = Start().States[0];
        Assert.Equal("failed", s.Status);
        Assert.Contains("update Lapin", s.Problem);
        Install(Pack(contract: "2.x"));
        Assert.Contains("built for Lapin's contract 2.x", Start().States[0].Problem);
        Install(Pack(contract: "1.0"));
        Assert.Equal("loaded", Start().States[0].Status);
    }

    [Fact]
    public void A_missing_requirement_or_api_disables_it_with_why()
    {
        Install(Pack(requires: new JsonObject { ["planning"] = "^1", ["cost"] = "^1" }));
        var s = Start(Host("core", "architecture")).States[0];
        Assert.Equal("failed", s.Status);
        Assert.Contains("„planning”, which this edition does not have", s.Problem);
        Install(Pack(requires: new JsonObject { ["planning"] = "^2" }));
        Assert.Contains("api ^2", Start().States[0].Problem);
        Install(Pack(requires: new JsonArray("planning")));
        Assert.Equal("loaded", Start().States[0].Status);
    }

    [Fact]
    public void A_Register_that_throws_leaves_nothing_and_the_others_load()
    {
        Install(Pack("broken", mode: 1));
        Install(Pack("fine"));
        var (states, catalog) = Start();
        var broken = states.Single(s => s.Id == "ro.test.broken");
        Assert.Equal("failed", broken.Status);
        Assert.Contains("MissingMethodException", broken.Problem);
        // What it registered before throwing is not in the catalog.
        Assert.Null(catalog.Command("broken.ping"));
        Assert.False(catalog.Catalogs.ContainsKey("broken.things"));
        Assert.DoesNotContain(catalog.Modules, m => m.Id == "broken");
        Assert.Equal("loaded", states.Single(s => s.Id == "ro.test.fine").Status);
        Assert.NotNull(catalog.Command("fine.ping"));
    }

    [Fact]
    public void An_extension_names_only_under_its_own_id_and_overrides_nothing()
    {
        Install(Pack("outside", mode: 2));
        Install(Pack("wraps", mode: 3));
        var (states, catalog) = Start();
        Assert.Contains("outside its own id", states.Single(s => s.Id == "ro.test.outside").Problem);
        Assert.Contains("override nothing", states.Single(s => s.Id == "ro.test.wraps").Problem);
        Assert.Null(catalog.Command("architecture.evil"));
        Assert.Empty(catalog.Overrides);
    }

    [Fact]
    public void Two_extensions_carry_different_versions_of_one_dependency()
    {
        Install(Pack("one", dep: EmitDep(1)));
        Install(Pack("two", dep: EmitDep(2)));
        var (states, catalog) = Start();
        Assert.All(states, s => Assert.Equal("loaded", s.Status));
        Assert.Equal("dep 1", catalog.Command("one.ping")!.Label);
        Assert.Equal("dep 2", catalog.Command("two.ping")!.Label);
        // Neither copy reached the Default context, where the product's modules are.
        Assert.DoesNotContain(AssemblyLoadContext.Default.Assemblies, a => a.GetName().Name == "Lapin.FixtureDep");
    }

    [Fact]
    public void Uninstall_removes_the_files_at_the_next_start_and_an_update_its_old_version()
    {
        Install(Pack());
        var (states, _) = Start();
        var installer = new ExtensionInstaller(Host(), states);
        installer.Uninstall(new JsonObject { ["id"] = "ro.test.fixture" });
        // Still there while this start runs, and the list says a restart would change something.
        Assert.True(Directory.Exists(VersionDir()));
        Assert.True((bool)installer.Modules([])["restart"]!);
        var next = Start().States;
        Assert.Empty(next);
        Assert.False(Directory.Exists(Path.Combine(_root, "ro.test.fixture")));
        Assert.Empty(ExtensionLoader.ReadList(_root));

        // An update unpacks beside the version running; the old one goes at the start after the switch.
        Install(Pack());
        Install(Pack(version: "1.1.0"));
        Assert.True(Directory.Exists(VersionDir()));
        var s = Assert.Single(Start().States);
        Assert.Equal("1.1.0", s.Version);
        Assert.Equal("loaded", s.Status);
        Assert.False(Directory.Exists(VersionDir()));
        // A half-unpacked folder and a folder modules.json does not name go too.
        Directory.CreateDirectory(Path.Combine(_root, "ro.test.fixture", "1.2.0.tmp-x"));
        Directory.CreateDirectory(Path.Combine(_root, "ro.test.stray", "1.0.0"));
        Start();
        Assert.False(Directory.Exists(Path.Combine(_root, "ro.test.fixture", "1.2.0.tmp-x")));
        Assert.False(Directory.Exists(Path.Combine(_root, "ro.test.stray")));
    }

    [Fact]
    public void Disable_and_enable_take_effect_at_the_next_start()
    {
        Install(Pack());
        var installer = new ExtensionInstaller(Host(), []);
        installer.Disable(new JsonObject { ["id"] = "ro.test.fixture" });
        var (states, catalog) = Start();
        Assert.Equal("disabled", states[0].Status);
        Assert.Null(catalog.Command("fixture.ping"));
        installer.Enable(new JsonObject { ["id"] = "ro.test.fixture" });
        Assert.Equal("loaded", Start().States[0].Status);
        Assert.Throws<ExtensionException>(() => installer.Disable(new JsonObject { ["id"] = "ro.test.none" }));
    }

    [Fact]
    public void Native_code_unsafe_paths_and_mismatched_manifests_are_refused()
    {
        Assert.Contains("native code", Assert.Throws<ExtensionException>(() => Install(Pack(edit: f => f["engine/libx.dylib"] = [0xCF, 0xFA, 0xED, 0xFE, 0, 0]))).Message);
        Assert.Contains("native code", Assert.Throws<ExtensionException>(() => Install(Pack(edit: f => f["ui/x.bin"] = [0x7F, (byte)'E', (byte)'L', (byte)'F', 1]))).Message);
        Assert.Contains("native code", Assert.Throws<ExtensionException>(() => Install(Pack(edit: f => f["engine/Fake.dll"] = [(byte)'M', (byte)'Z', 0, 0]))).Message);
        Assert.Contains("subfolders", Assert.Throws<ExtensionException>(() => Install(Pack(edit: f => f["engine/sub/a.pdb"] = [1]))).Message);
        Assert.Contains("only .dll, .pdb", Assert.Throws<ExtensionException>(() => Install(Pack(edit: f => f["engine/x.deps.json"] = [1]))).Message);
        Assert.Contains("path Lapin does not take", Assert.Throws<ExtensionException>(() => Install(Pack(edit: f => f["../escape.txt"] = [1]))).Message);
        Assert.Contains("differs from the catalogue's", Assert.Throws<ExtensionException>(() => Install(Pack(edit: f => f["manifest.json"] = Encoding.UTF8.GetBytes(
            JsonNode.Parse(f["manifest.json"])!.AsObject().With(o => o["name"] = "Other").ToJsonString())))).Message);
        Assert.Contains("extension id's last part", Assert.Throws<ExtensionException>(() => Install(Pack(edit: f => f["module.json"] = Encoding.UTF8.GetBytes(
            JsonNode.Parse(f["module.json"])!.AsObject().With(o => o["id"] = "other").ToJsonString())))).Message);
        Assert.Contains("permission „full”", Assert.Throws<ExtensionException>(() => Install(Pack(manifest: m => m["permissions"] = new JsonArray()))).Message);
        Assert.Contains("not an extension module", Assert.Throws<ExtensionException>(() => Install(Pack(manifest: m => m["kind"] = "script"))).Message);
        Assert.Contains("is not module.json's", Assert.Throws<ExtensionException>(() => Install(Pack(manifest: m => m["contract"] = "1.0"))).Message);
        Assert.Empty(Directory.Exists(_root) ? Directory.GetDirectories(_root) : []);
        Assert.Null(ExtensionPackage.SafePath("a/../b"));
        Assert.Null(ExtensionPackage.SafePath("C:x"));
        Assert.Null(ExtensionPackage.SafePath("a\\b"));
        Assert.Equal("ui/i18n/ro.json", ExtensionPackage.SafePath("ui/i18n/ro.json"));
    }

    [Fact]
    public void A_licence_agreement_must_be_accepted_to_install()
    {
        var p = Pack(manifest: m => m["eula"] = new JsonObject { ["file"] = "EULA.md", ["version"] = "2" }, edit: f => f["EULA.md"] = Encoding.UTF8.GetBytes("# Terms\n"));
        var installer = new ExtensionInstaller(Host(), []);
        var args = new JsonObject { ["manifest"] = p.Manifest.DeepClone(), ["data"] = Convert.ToBase64String(p.Zip) };
        var seen = installer.Inspect((JsonObject)args.DeepClone());
        Assert.Equal("# Terms\n", (string?)seen["eula"]!["text"]);
        Assert.Equal("Test licence.\n", (string?)seen["licenseText"]);
        Assert.Contains("accepted", Assert.Throws<ExtensionException>(() => installer.Install((JsonObject)args.DeepClone())).Message);
        args["eulaAccepted"] = "1";
        Assert.Throws<ExtensionException>(() => installer.Install((JsonObject)args.DeepClone()));
        args["eulaAccepted"] = "2";
        installer.Install(args);
        Assert.Equal("loaded", Start().States[0].Status);
    }

    [Fact]
    public void A_module_id_taken_by_a_built_in_or_another_extension_is_refused()
    {
        Install(Pack("cost"));
        Assert.Contains("has the id of a module Lapin has already", Start().States[0].Problem);
        Install(Pack("liar", claimedId: "other"));
        Assert.Contains("says it is „other”", Start().States.Single(s => s.Id == "ro.test.liar").Problem);
    }

    [Fact]
    public async Task The_host_answers_extension_modules_calls_for_its_own_interface_only()
    {
        static System.Text.Json.JsonElement Header(byte[] frame) => Lapin.Protocol.BinaryFrame.Decode(frame).Header.RootElement;
        Install(Pack());
        var (states, _) = Start();
        var hub = new Lapin.Host.EngineHub(new Fixture().Session, Lapin.Host.PackageFiles.Create()) { Extensions = new ExtensionInstaller(Host(), states) };
        const string list = """{"id":1,"method":"extensions.modules","params":{}}""";
        var r = Header((await hub.Handle(list))!).GetProperty("result");
        Assert.Equal("ro.test.fixture", r.GetProperty("modules")[0].GetProperty("id").GetString());
        var guest = new HubGuest { Caller = new Lapin.Protocol.Caller("guest-1", "editor") };
        foreach (var m in new[] { "extensions.modules", "extensions.install", "extensions.uninstall", "extensions.enable", "extensions.disable", "extensions.inspect" })
            Assert.Equal("Only the host can do that.", Header((await hub.Handle(new JsonObject { ["id"] = 2, ["method"] = m, ["params"] = new JsonObject { ["id"] = "ro.test.fixture" } }.ToJsonString(), guest))!).GetProperty("error").GetString());
        // A refusal is the loader's sentence.
        Assert.Contains("not an installed extension module", Header((await hub.Handle("""{"id":3,"method":"extensions.disable","params":{"id":"ro.test.none"}}"""))!).GetProperty("error").GetString());
        // A host without a user folder (no desktop): none.
        var bare = new Lapin.Host.EngineHub(new Fixture().Session, Lapin.Host.PackageFiles.Create());
        Assert.Contains("desktop app only", Header((await bare.Handle(list))!).GetProperty("error").GetString());
    }

    sealed class HubGuest : Lapin.Host.EngineHub.Client
    {
        public override Task SendBinary(byte[] frame) => Task.CompletedTask;
        public override Task SendText(string text) => Task.CompletedTask;
        protected override void Drop() { }
    }

    // ------------------------------------------------------------------ the pieces

    [Fact]
    public void Ed25519_agrees_with_the_RFC_and_with_the_interfaces_vectors()
    {
        static byte[] Hex(string s) => Convert.FromHexString(s);
        // RFC 8032 §7.1, TEST 1 and TEST 2: the same as ui/src/platform/extensions/signature.test.ts.
        var vectors = new[]
        {
            (Secret: "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60", Pub: "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a", Msg: "", Sig: "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b"),
            (Secret: "4ccd089b28ff96da9db6c346ec114e0f5b8a319f35aba624da8cf6ed4fb8a6fb", Pub: "3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c", Msg: "72", Sig: "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00"),
        };
        var ts = File.ReadAllText(Path.Combine(RepoRoot(), "ui", "src", "platform", "extensions", "signature.test.ts"));
        foreach (var v in vectors)
        {
            Assert.Contains(v.Pub, ts);
            Assert.Contains(v.Sig, ts);
            var msg = Hex(v.Msg);
            Assert.True(Ed25519.Verify(Hex(v.Pub), Hex(v.Sig), msg));
            Assert.Equal(v.Pub, Convert.ToHexStringLower(Ed25519.PublicKey(Hex(v.Secret))));
            Assert.Equal(v.Sig, Convert.ToHexStringLower(Ed25519.Sign(Hex(v.Secret), msg)));
            var bad = Hex(v.Sig);
            bad[10] ^= 1;
            Assert.False(Ed25519.Verify(Hex(v.Pub), bad, msg));
            Assert.False(Ed25519.Verify(Hex(v.Pub), Hex(v.Sig), "x"u8));
            var high = Hex(v.Sig);
            high[63] |= 0xF0; // S past the group order
            Assert.False(Ed25519.Verify(Hex(v.Pub), high, msg));
        }
        Assert.False(Ed25519.Verify(Enumerable.Repeat((byte)255, 32).ToArray(), new byte[64], "x"u8));
        Assert.False(Ed25519.Verify(new byte[31], new byte[64], "x"u8));
    }

    [Fact]
    public void The_engines_store_keys_are_the_interfaces()
    {
        var ts = File.ReadAllText(Path.Combine(RepoRoot(), "ui", "src", "platform", "extensions", "keys.ts"));
        var listed = Regex.Matches(ts, @"\{\s*id:\s*'([0-9a-f]{16})',\s*publicKey:\s*'([A-Za-z0-9+/=]+)'\s*\}").Select(m => new StoreKey(m.Groups[1].Value, m.Groups[2].Value)).ToList();
        Assert.NotEmpty(listed);
        Assert.Equal(listed, StoreKeys.All);
        foreach (var k in StoreKeys.All) Assert.Equal(k.Id, StoreKey.IdOf(Convert.FromBase64String(k.PublicKey)));
    }

    [Fact]
    public void Contract_and_api_ranges()
    {
        Assert.Null(LapinContract.Problem("1.x"));
        Assert.Null(LapinContract.Problem("1.0"));
        Assert.Null(LapinContract.Problem("1.2", 1, 3));
        Assert.NotNull(LapinContract.Problem("1.4", 1, 3));
        Assert.NotNull(LapinContract.Problem("2.x"));
        Assert.NotNull(LapinContract.Problem(null));
        Assert.NotNull(LapinContract.Problem(">=1"));
        Assert.Null(LapinContract.ApiProblem("planning", "^1", 1));
        Assert.Null(LapinContract.ApiProblem("planning", null, 7));
        Assert.Null(LapinContract.ApiProblem("planning", ">=1", 3));
        Assert.NotNull(LapinContract.ApiProblem("planning", "^1", 2));
        Assert.NotNull(LapinContract.ApiProblem("planning", "~1", 1));
    }

    [Fact]
    public void Every_open_module_says_its_api()
    {
        var dirs = Directory.GetFiles(Path.Combine(RepoRoot(), "engine", "src", "Modules"), "module.json", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")).ToList();
        Assert.Contains(dirs, f => f.Contains("Lapin.Planning"));
        foreach (var f in dirs)
        {
            var m = ModuleLoader.ParseModule(File.ReadAllText(f), f);
            Assert.True(m.Api >= 1, $"{f} has no api");
            Assert.Contains(CoreModule.ModuleId, m.RequiredIds());
        }
    }

    static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string here = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", ".."));
}

static class ExtensionJsonEdit
{
    public static JsonObject With(this JsonObject o, Action<JsonObject> f) { f(o); return o; }
}
