using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Other tools' entries in a .lapin (docs/format-lapin.md, Părți străine): kept
/// byte for byte through open, edit and save, listed in the manifest with their
/// hashes; Lapin's own names always Lapin's; a file without any saved as before.
/// </summary>
public sealed class PackageForeignTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-foreign-").FullName;
    string PathOf(string name) => Path.Combine(_dir, name);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static readonly byte[] Dag = Encoding.UTF8.GetBytes("{\n  \"nodes\": [\"walls\", \"roof\"],\n  \"edges\": [[0, 1]]\n}\n");
    // Not UTF-8 clean on purpose, and a CRLF: what is kept is the bytes, not text.
    static readonly byte[] Python = [.. Encoding.UTF8.GetBytes("import lapin\r\nwall = lapin.wall(0, 0, 5, 0)\r\n"), 0xFF, 0x00, 0xFE];
    static readonly DateTimeOffset Then = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    static Fixture Project()
    {
        var f = new Fixture();
        f.Run("storey.create", []);
        f.Wall(0, 0, 5, 0);
        return f;
    }

    static void Add(ZipArchive zip, string name, byte[] bytes)
    {
        var e = zip.CreateEntry(name);
        e.LastWriteTime = Then;
        using var s = e.Open();
        s.Write(bytes);
    }

    /// <summary>A project saved by Lapin, then given entries by another tool: its own, and some under Lapin's names.</summary>
    string Tooled(Fixture f, string name)
    {
        var path = PathOf(name);
        PackageFiles.Create().Save(f.Session, path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        Add(zip, "dag/manifest.json", Dag);
        Add(zip, "source/model.py", Python);
        Add(zip, "README.txt", "made by a tool"u8.ToArray());
        Add(zip, "model/evil/IfcWall/_.arrow", [1, 2, 3]);
        Add(zip, "Project/document.json", "{}"u8.ToArray());
        Add(zip, "derived/cache.bin", [4]);
        Add(zip, "dag/../model/x", [5]);
        return path;
    }

    static Dictionary<string, byte[]> Entries(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        return zip.Entries.ToDictionary(e => e.FullName, e => { using var s = e.Open(); using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray(); });
    }

    static JsonObject ManifestOf(string path) => (JsonObject)JsonNode.Parse(Entries(path)["manifest.json"])!;

    [Fact]
    public void Another_tools_entries_survive_open_edit_and_save_byte_for_byte()
    {
        var path = Tooled(Project(), "house.lapin");
        var files = PackageFiles.Create();
        var g = new Fixture();
        files.Open(g.Session, path);
        g.Wall(5, 0, 5, 4);
        files.Save(g.Session, null);

        var entries = Entries(path);
        Assert.Equal(Dag, entries["dag/manifest.json"]);
        Assert.Equal(Python, entries["source/model.py"]);
        Assert.Equal("made by a tool"u8.ToArray(), entries["README.txt"]);
        // Lapin's names stay Lapin's: an entry under them is not kept, whatever its case; nor a path that climbs out.
        Assert.DoesNotContain("model/evil/IfcWall/_.arrow", entries.Keys);
        Assert.DoesNotContain("Project/document.json", entries.Keys);
        Assert.DoesNotContain("derived/cache.bin", entries.Keys);
        Assert.DoesNotContain("dag/../model/x", entries.Keys);
        // When they were written comes along too.
        using (var zip = ZipFile.OpenRead(path)) Assert.Equal(Then.DateTime, zip.GetEntry("source/model.py")!.LastWriteTime.DateTime);

        // Listed apart from the project's parts, with what their bytes hash to.
        var manifest = ManifestOf(path);
        var foreign = manifest["foreign"]!.AsArray().Select(p => ((string)p!["path"]!, (string)p["sha256"]!, (long)p["size"]!)).ToList();
        Assert.Equal([("README.txt", Hex(entries["README.txt"]), 14L), ("dag/manifest.json", Hex(Dag), Dag.Length), ("source/model.py", Hex(Python), Python.Length)], foreign);
        Assert.DoesNotContain(manifest["parts"]!.AsArray(), p => !((string)p!["path"]!).StartsWith("model/") && !((string)p["path"]!).StartsWith("project/"));

        // The edit is in, and the next round keeps them again, saved under another name too.
        var h = new Fixture();
        var again = PackageFiles.Create();
        again.Open(h.Session, path);
        Assert.Equal(2, h.Model.OfClass(IfcClass.Wall).Count());
        h.Wall(5, 4, 0, 4);
        var other = PathOf("copy.lapin");
        again.Save(h.Session, other);
        Assert.Equal(Python, Entries(other)["source/model.py"]);
        Assert.Equal(Dag, Entries(other)["dag/manifest.json"]);
    }

    static string Hex(byte[] b) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(b));

    [Fact]
    public void A_file_without_foreign_entries_saves_as_before()
    {
        var f = Project();
        var path = PathOf("plain.lapin");
        var files = PackageFiles.Create();
        files.Save(f.Session, path);
        var g = new Fixture();
        files.Open(g.Session, path);
        files.Save(g.Session, null);

        // What this Lapin wrote before foreign entries were kept: the same build written with nothing else.
        using var before = new MemoryStream();
        LapinPackage.Write(before, ProjectPackage.Build(g.Session));
        before.Position = 0;
        Dictionary<string, byte[]> old;
        using (var zip = new ZipArchive(before, ZipArchiveMode.Read))
            old = zip.Entries.ToDictionary(e => e.FullName, e => { using var s = e.Open(); using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray(); });
        var now = Entries(path);
        Assert.Equal(old.Keys, now.Keys);
        foreach (var (name, bytes) in old) Assert.Equal(bytes, now[name]);
        Assert.False(ManifestOf(path).ContainsKey("foreign"));
        Assert.Empty(PackageForeign.Of(path).Parts);
    }

    /// <summary>The manifest as a Lapin before foreign entries knew it: no <c>Foreign</c>.</summary>
    sealed record OldManifest(string Format, int Version, string App, string Product, IReadOnlyList<PackageModule> Modules, IReadOnlyList<PackagePart> Parts, IReadOnlyDictionary<string, string> Classes);

    [Fact]
    public void An_older_Lapin_reads_a_manifest_with_foreign_entries_as_before()
    {
        var f = Project();
        var path = Tooled(f, "house.lapin");
        var files = PackageFiles.Create();
        var g = new Fixture();
        files.Open(g.Session, path);
        files.Save(g.Session, null);
        // The older reader's options were the same (web names, unknown members skipped); its parts are only the project's, all checked.
        var old = JsonSerializer.Deserialize<OldManifest>(Entries(path)["manifest.json"], new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(3, old.Version);
        Assert.All(old.Parts, p => Assert.True(p.Path.StartsWith("model/") || p.Path.StartsWith("project/")));
        var content = LapinPackage.Read(path);
        Assert.Equal(f.Model.Entities.Count(), content.Entities.Count);
    }

    [Fact]
    public void In_the_browser_they_travel_in_the_bytes_but_not_into_versions()
    {
        var path = Tooled(Project(), "house.lapin");
        var g = new Fixture();
        var files = new ProjectFiles((_, _) => throw new InvalidOperationException("no files"), (_, _, _, _, _) => throw new InvalidOperationException("no files"));
        files.OpenBytes(g.Session, "house.lapin", File.ReadAllBytes(path));
        g.Wall(5, 0, 5, 4);
        var (r, _) = files.SaveVersion(g.Session, "v1", null, asBytes: true);
        var bytes = Convert.FromBase64String((string)r["data"]!);
        Assert.Equal(Python, PackageForeign.Of(bytes).Parts.Single(p => p.Path == "source/model.py").Bytes);
        // A version keeps the project, not what other tools left beside it.
        var version = PackageVersions.Of(bytes).Require(1);
        Assert.Null(version.Manifest.Foreign);
        Assert.DoesNotContain(version.Manifest.Parts, p => p.Path.StartsWith("dag/") || p.Path.StartsWith("source/"));
        var (saved, _) = files.SaveBytes(g.Session, "house.lapin");
        Assert.Equal(Dag, PackageForeign.Of(saved).Parts.Single(p => p.Path == "dag/manifest.json").Bytes);
        // A new project starts without them.
        files.Reset();
        g.Session.New(true, null);
        var (fresh, _) = files.SaveBytes(g.Session, "new.lapin");
        Assert.Empty(PackageForeign.Of(fresh).Parts);
    }

    [Theory]
    [InlineData("dag/manifest.json", true)]
    [InlineData("source/model.py", true)]
    [InlineData("anything/deep/x.bin", true)]
    [InlineData("notes.txt", true)]
    [InlineData("manifest.json", false)]
    [InlineData("MANIFEST.JSON", false)]
    [InlineData("model/core/IfcWall/_.arrow", false)]
    [InlineData("Model/x", false)]
    [InlineData("project/document.json", false)]
    [InlineData("versions/index.json", false)]
    [InlineData("issues/blobs/abc", false)]
    [InlineData("journal/ops.jsonl", false)]
    [InlineData("analytics/a.parquet", false)]
    [InlineData("/abs", false)]
    [InlineData("a\\b", false)]
    [InlineData("a//b", false)]
    [InlineData("./a", false)]
    [InlineData("a/../model/x", false)]
    public void Which_names_are_another_tools(string path, bool kept) => Assert.Equal(kept, PackageForeign.Keeps(path));
}
