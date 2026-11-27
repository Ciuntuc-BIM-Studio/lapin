using System.Text.Json.Nodes;

namespace Lapin.Platform;

/// <summary>
/// The desktop host's <c>extensions.install</c>, <c>uninstall</c>, <c>enable</c>, <c>disable</c> and <c>modules</c> (docs/plan-extension-modules.md
/// §1.2, §4.3): a package is checked whole (<see cref="ExtensionPackage.Check"/>) before a byte of it is written, unpacked beside the
/// running version and named in modules.json; every change takes effect at the next start, when <see cref="ExtensionLoader"/> checks
/// it all again. Only this computer's own interface reaches these (the host refuses a guest); the browser engine has none.
/// </summary>
/// <param name="host">Where extensions live and the store keys.</param>
/// <param name="started">What this start loaded and refused.</param>
public sealed class ExtensionInstaller(ExtensionHostInfo host, IReadOnlyList<ExtensionState> started)
{
    public ExtensionHostInfo Host => host;
    public IReadOnlyList<ExtensionState> Started => started;

    RevocationList? _revoked = ExtensionRevocations.ReadStored(host.Root, host.Keys);
    /// <summary>The revocation list in force: the newest the engine verified, kept under the extensions' folder (§4.3).</summary>
    public RevocationList? Revoked => _revoked;

    /// <summary>A version the list in force withdraws is not installed: why.</summary>
    void RefuseRevoked(ExtensionManifest m)
    {
        if (_revoked?.Match(m.Id, m.Version, m.Sha256) is { } r)
            throw new ExtensionException($"„{m.Name}” {m.Version} is not installed: {ExtensionRevocations.Problem(r)}");
    }

    /// <summary>
    /// <c>{data?, sig?}</c>: the catalogue's revocation list and its signature file, each in base64, as the interface read them beside
    /// the index. The engine verifies the signature itself with the store keys it carries (never trusting the interface) and keeps
    /// the list when it is newer than the one kept; an older one is left. Without <c>data</c>: the list in force, as it is.
    /// Answers when the list in force was made, how many entries it has, whether the one given was taken (or was older), and the
    /// installed extension modules it withdraws (each turned off at the next start).
    /// </summary>
    public JsonObject Revocations(JsonObject p)
    {
        bool taken = false, older = false;
        if (Str(p, "data") is { Length: > 0 } data)
        {
            byte[] bytes, sig;
            try { bytes = Convert.FromBase64String(data); sig = Convert.FromBase64String(Str(p, "sig")); }
            catch (FormatException) { throw new ExtensionException("The revocation list is not base64."); }
            var outcome = ExtensionRevocations.Accept(host.Root, host.Keys, bytes, sig);
            _revoked = outcome.Current;
            taken = outcome.Taken;
            older = outcome.Older;
        }
        var o = ExtensionRevocations.Describe(_revoked);
        o["taken"] = taken;
        o["older"] = older;
        var withdrawn = new JsonArray();
        foreach (var e in ExtensionLoader.ReadList(host.Root))
        {
            var sha = started.FirstOrDefault(x => x.Id == e.Id && x.Version == e.Version)?.Manifest?.Sha256 ?? ExtensionLoader.KeptManifest(host.Root, e)?.Sha256;
            if (_revoked?.Match(e.Id, e.Version, sha) is { } r) withdrawn.Add((JsonNode)new JsonObject { ["id"] = e.Id, ["version"] = e.Version, ["reason"] = r.Reason, ["date"] = r.Date });
        }
        o["withdrawn"] = withdrawn;
        return o;
    }

    static string Str(JsonObject p, string key) => p[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

    /// <summary>
    /// <c>{manifest, data, eulaAccepted?}</c>: the catalogue's manifest of the extension (as the catalogue lists it, its package's
    /// SHA-256, size and signature with it), the package zip in base64, and the licence agreement's version the user accepted.
    /// </summary>
    public JsonObject Install(JsonObject p)
    {
        if (p["manifest"] is not JsonObject manifestJson) throw new ExtensionException("Install needs the extension's manifest.");
        byte[] zip;
        try { zip = Convert.FromBase64String(Str(p, "data")); }
        catch (FormatException) { throw new ExtensionException("The package is not base64."); }
        var manifest = ExtensionManifest.Parse(manifestJson);
        RefuseRevoked(manifest);
        var c = ExtensionPackage.Check(zip, manifest, host.Keys);
        if (manifest.EulaVersion is { Length: > 0 } eula && Str(p, "eulaAccepted") != eula)
            throw new ExtensionException($"„{manifest.Name}” is installed only once its licence agreement ({eula}) is accepted.");

        var idDir = Path.Combine(host.Root, manifest.Id);
        var final = Path.Combine(idDir, manifest.Version);
        var temp = Path.Combine(idDir, $"{manifest.Version}.tmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var root = Path.GetFullPath(temp) + Path.DirectorySeparatorChar;
            foreach (var (rel, bytes) in c.Entries)
            {
                var path = Path.GetFullPath(Path.Combine(temp, rel));
                // SafePath has refused every way out already; this is the last word.
                if (!path.StartsWith(root, StringComparison.Ordinal)) throw new ExtensionException($"„{rel}” would be written outside the extension's folder.");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, bytes);
            }
            File.WriteAllBytes(Path.Combine(temp, ExtensionPackage.KeptZip), zip);
            File.WriteAllText(Path.Combine(temp, ExtensionPackage.KeptManifest), manifestJson.ToJsonString() + "\n");
            // The same version again (a damaged copy): its folder is replaced. Its assemblies were loaded from memory, so nothing holds it.
            if (Directory.Exists(final)) Directory.Delete(final, recursive: true);
            Directory.Move(temp, final);
        }
        catch
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); } catch (IOException) { }
            throw;
        }
        var list = ExtensionLoader.ReadList(host.Root).Where(e => e.Id != manifest.Id).ToList();
        list.Add(new ExtensionEntry(manifest.Id, manifest.Version));
        ExtensionLoader.WriteList(host.Root, list);
        return new JsonObject { ["id"] = manifest.Id, ["version"] = manifest.Version, ["restart"] = true };
    }

    /// <summary>
    /// <c>{manifest, data}</c>, checked as an install checks it, nothing written: what the install dialog shows before the user agrees
    /// (the licence text, the licence agreement's, the module, its contract and requirements).
    /// </summary>
    public JsonObject Inspect(JsonObject p)
    {
        if (p["manifest"] is not JsonObject manifestJson) throw new ExtensionException("Inspect needs the extension's manifest.");
        byte[] zip;
        try { zip = Convert.FromBase64String(Str(p, "data")); }
        catch (FormatException) { throw new ExtensionException("The package is not base64."); }
        var inspected = ExtensionManifest.Parse(manifestJson);
        RefuseRevoked(inspected);
        var c = ExtensionPackage.Check(zip, inspected, host.Keys);
        string? Text(string? rel) => rel != null && c.Entries.TryGetValue(rel, out var t) ? System.Text.Encoding.UTF8.GetString(t) : null;
        var o = new JsonObject
        {
            ["id"] = c.Manifest.Id,
            ["version"] = c.Manifest.Version,
            ["name"] = c.Manifest.Name,
            ["license"] = c.Manifest.License,
            ["licenseText"] = Text("LICENSE.txt"),
            ["engine"] = c.Module != null,
            ["ui"] = c.Entries.ContainsKey("ui/plugin.js"),
        };
        if (c.Manifest.EulaVersion != null) o["eula"] = new JsonObject { ["file"] = c.Manifest.EulaFile, ["version"] = c.Manifest.EulaVersion, ["text"] = Text(c.Manifest.EulaFile) };
        if (c.Module is { } m)
        {
            o["module"] = m.Id;
            o["contract"] = m.Contract;
            if (LapinContract.Problem(m.Contract) is { } problem) o["problem"] = problem;
            else if (m.RequiredIds().FirstOrDefault(r => r != CoreModule.ModuleId && !host.Modules.Contains(r)) is { } missing) o["problem"] = $"It needs „{missing}”, which this edition does not have.";
        }
        return o;
    }

    JsonObject Change(JsonObject p, Func<ExtensionEntry, ExtensionEntry> change)
    {
        var id = Str(p, "id");
        var list = ExtensionLoader.ReadList(host.Root);
        var i = list.FindIndex(e => e.Id == id);
        if (i < 0) throw new ExtensionException($"„{id}” is not an installed extension module.");
        list[i] = change(list[i]);
        ExtensionLoader.WriteList(host.Root, list);
        return new JsonObject { ["id"] = id, ["enabled"] = list[i].Enabled, ["pendingRemoval"] = list[i].PendingRemoval, ["restart"] = true };
    }

    /// <summary><c>{id}</c>: removed at the next start, before anything loads.</summary>
    public JsonObject Uninstall(JsonObject p) => Change(p, e => e with { PendingRemoval = true });
    /// <summary><c>{id}</c>: loaded from the next start (an uninstall not yet done is taken back).</summary>
    public JsonObject Enable(JsonObject p) => Change(p, e => e with { Enabled = true, PendingRemoval = false });
    /// <summary><c>{id}</c>: not loaded from the next start, kept installed.</summary>
    public JsonObject Disable(JsonObject p) => Change(p, e => e with { Enabled = false });

    /// <summary>
    /// The installed extension modules. <c>modules</c>: the interface halves of those this start loaded, as the interface loader
    /// takes them before boot (ui/src/platform/extensions/plugins.ts: <c>{id, version, sdk, js, css?, phrases?}</c>, phrases by
    /// language as the table's JSON text), read from the bytes checked at this start. <c>installed</c>: every one, as modules.json
    /// says now and as this start found it (status, why it was refused, whether a restart would change anything), for
    /// Extensions → Installed. And Lapin's contract version.
    /// </summary>
    public JsonObject Modules(JsonObject p)
    {
        var now = ExtensionLoader.ReadList(host.Root);
        var installed = new JsonArray();
        var plugins = new JsonArray();
        var restart = false;
        foreach (var id in started.Select(s => s.Id).Concat(now.Select(e => e.Id)).Distinct())
        {
            var s = started.FirstOrDefault(x => x.Id == id);
            var e = now.FirstOrDefault(x => x.Id == id);
            var o = new JsonObject
            {
                ["id"] = id,
                ["version"] = s?.Version ?? e?.Version,
                ["status"] = s?.Status ?? "pending",
                ["enabled"] = e?.Enabled ?? false,
                ["pendingRemoval"] = e == null || e.PendingRemoval,
            };
            // What the next start does differently from this one.
            var changes = e == null || e.PendingRemoval || s == null || e.Version != s.Version || e.Enabled != s.Entry.Enabled;
            if (changes) { o["restart"] = true; restart = true; }
            if (e != null && s != null && e.Version != s.Version) o["nextVersion"] = e.Version;
            if (s?.Problem is { } problem) o["problem"] = problem;
            if (s?.ProblemKind is { } kind) o["problemKind"] = kind;
            // Installed since this start: its name from the manifest it was installed with (checked at the next start).
            var shown = s?.Manifest;
            if (shown == null && e != null)
            {
                try { shown = JsonNode.Parse(File.ReadAllText(Path.Combine(host.Root, e.Id, e.Version, ExtensionPackage.KeptManifest))) is JsonObject j ? ExtensionManifest.Parse(j) : null; }
                catch (Exception x) when (x is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ExtensionException) { }
            }
            if (shown is { } m)
            {
                o["name"] = m.Name;
                o["license"] = m.License;
                o["permissions"] = new JsonArray([.. m.Permissions.Select(x => (JsonNode)JsonValue.Create(x)!)]);
                if (m.EulaVersion != null) o["eula"] = new JsonObject { ["file"] = m.EulaFile, ["version"] = m.EulaVersion };
            }
            // Withdrawn by the list in force: refused at this start already, or (a list taken since) at the next.
            var revocation = e != null
                ? _revoked?.Match(e.Id, e.Version, (s?.Version == e.Version ? s.Manifest?.Sha256 : null) ?? ExtensionLoader.KeptManifest(host.Root, e)?.Sha256)
                : s?.Revocation;
            if (revocation != null)
            {
                o["revoked"] = revocation.ToJson();
                if (s?.Status == "loaded" && e is { PendingRemoval: false }) { o["restart"] = true; restart = true; }
            }
            if (s?.ModuleId is { } moduleId) o["module"] = moduleId;
            if (s?.Contract is { } contract) o["contract"] = contract;
            installed.Add((JsonNode)o);
            if (s is { Status: "loaded", Js: { } js })
            {
                var phrases = new JsonObject();
                foreach (var (lang, text) in s.Phrases) phrases[lang] = text;
                var plugin = new JsonObject { ["id"] = s.Id, ["version"] = s.Version, ["sdk"] = s.Manifest?.Sdk ?? "", ["js"] = js, ["phrases"] = phrases };
                if (s.Css != null) plugin["css"] = s.Css;
                plugins.Add((JsonNode)plugin);
            }
        }
        return new JsonObject { ["contract"] = LapinContract.Version, ["restart"] = restart, ["modules"] = plugins, ["installed"] = installed };
    }
}
