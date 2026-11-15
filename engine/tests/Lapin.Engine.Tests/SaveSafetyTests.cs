using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Host;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Saving never loses what the file held beside the model (audit 15.11.2026): a project whose file is renamed, moved or
/// deleted while it is open still saves with its versions and issued files, and its unsaved work still comes back from the
/// journal (G1); versions whose index could not be read are carried over byte for byte, never dropped (P6); a damaged
/// version part or issued file is not copied into a new save, and said (P22); a save that fails leaves no temporary file (P23).
/// </summary>
public sealed class SaveSafetyTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-save-safety-").FullName;
    string At(string name) => Path.Combine(_dir, name);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StructureModule());

    static string Sheet(Fixture f)
    {
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Ground floor plan", ["number"] = "A-101", ["paper"] = "A3" })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Ground floor plan", ["scale"] = 100 })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = plan, ["x"] = 150, ["y"] = 120 });
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = s, ["fields"] = new JsonObject { ["drawnBy"] = "IC", ["checkedBy"] = "AB", ["date"] = "30.10.2026", ["phase"] = "DTAC" } });
        return s;
    }

    static JsonObject Call(EngineDispatcher d, string method, JsonObject p)
    {
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
        return (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
    }

    /// <summary>A saved project with three versions (the first keeping parts apart, the third an issue's) and an issue's files in its file.</summary>
    string Saved()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var s = Sheet(f);
        var files = PackageFiles.Create();
        var path = At("house.lapin");
        files.Save(f.Session, path);
        files.SaveVersion(f.Session, "Concept", null, asBytes: false);
        f.Wall(0, 3, 5, 3);
        files.SaveVersion(f.Session, "More walls", null, asBytes: false);
        var r = Call(new EngineDispatcher(f.Session, files), "cde.issue", new JsonObject { ["sheets"] = new JsonArray(s), ["name"] = "For the client", ["toFolder"] = false });
        Assert.Null(r["error"]);
        Assert.True((bool)r["result"]!["saved"]!);
        Assert.NotEmpty(Entries(path, "versions/blobs/"));
        Assert.NotEmpty(Entries(path, PackageIssues.Prefix));
        return path;
    }

    static Dictionary<string, byte[]> Entries(string path, string prefix)
    {
        using var zip = ZipFile.OpenRead(path);
        return zip.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal)).ToDictionary(e => e.FullName, e =>
        {
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        });
    }

    static void Replace(string path, string entry, byte[] bytes)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        zip.GetEntry(entry)!.Delete();
        using var s = zip.CreateEntry(entry).Open();
        s.Write(bytes);
    }

    /// <summary>Every version of the file restores, and every issued file is what its hash says.</summary>
    static void Whole(string path, int versions)
    {
        var v = PackageVersions.Of(path);
        Assert.Null(v.Unreadable);
        Assert.Equal(versions, v.Versions.Count);
        foreach (var version in v.Versions) Assert.NotEmpty(v.Content(version.Manifest).Entities);
        var issues = PackageIssues.Of(path);
        // Get checks the bytes against their hash.
        foreach (var name in Entries(path, PackageIssues.Prefix).Keys) Assert.NotEmpty(issues.Get(name[PackageIssues.Prefix.Length..]));
    }

    // ------------------------------------------------------------ G1: the file gone while the project is open

    [Fact]
    public void A_project_whose_file_is_deleted_while_open_saves_with_its_versions_and_issued_files()
    {
        var path = Saved();
        var issued = Entries(path, PackageIssues.Prefix).Keys.Order().ToList();
        var g = F();
        var files = PackageFiles.Create();
        files.Open(g.Session, path);
        File.Delete(path);
        g.Wall(0, 6, 5, 6);

        // Saved where it was: the file is written anew, with everything the old one held.
        files.Save(g.Session, null);
        Assert.Empty(files.TakeWarnings());
        Whole(path, 3);
        Assert.Equal(issued, Entries(path, PackageIssues.Prefix).Keys.Order());

        // A version saved after the file went again, through the version calls.
        File.Delete(path);
        var (v, _) = files.SaveVersion(g.Session, "After", null, asBytes: false);
        Assert.Equal(4, (int)v["number"]!);
        Assert.Null(v["warnings"]);
        Whole(path, 4);
    }

    [Fact]
    public void A_project_whose_file_is_moved_while_open_saves_elsewhere_with_its_versions_and_issued_files()
    {
        var path = Saved();
        var issued = Entries(path, PackageIssues.Prefix).Keys.Order().ToList();
        var g = F();
        var files = PackageFiles.Create();
        files.Open(g.Session, path);
        File.Move(path, At("moved away.lapin"));
        var copy = At("copy.lapin");
        files.Save(g.Session, copy);
        Assert.Empty(files.TakeWarnings());
        Whole(copy, 3);
        Assert.Equal(issued, Entries(copy, PackageIssues.Prefix).Keys.Order());
        Assert.Equal(copy, g.Session.Path);
    }

    /// <summary>The session of a Lapin that stops without closing: its journal let go as a crash lets it go.</summary>
    static string CrashedWork(string userDir, string path)
    {
        var id = Run();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return id;

        [MethodImpl(MethodImplOptions.NoInlining)]
        string Run()
        {
            var f = F();
            var files = PackageFiles.Create();
            _ = new Recovery(userDir, f.Session);
            files.Open(f.Session, path);
            return f.Wall(0, 9, 5, 9);
        }
    }

    [Fact]
    public void Work_on_a_file_deleted_since_comes_back_from_the_journal_and_saves_with_its_versions()
    {
        var path = Saved();
        var user = At("user");
        var wall = CrashedWork(user, path);
        File.Delete(path);

        var h = F();
        var files = PackageFiles.Create();
        using var recovery = new Recovery(user, h.Session);
        var d = new EngineDispatcher(h.Session, files, recovery);
        var work = recovery.List().Single();
        Assert.Equal(path, work.Path);
        var r = Call(d, "recovery.restore", new JsonObject { ["path"] = work.File });
        Assert.Null(r["error"]);
        Assert.NotNull(h.Model.Get(wall));
        Assert.Equal(path, h.Session.Path);
        Assert.Equal(3, files.VersionList(h.Session).Count);

        var back = At("back.lapin");
        files.Save(h.Session, back);
        Assert.Empty(files.TakeWarnings());
        Whole(back, 3);
        Assert.NotEmpty(Entries(back, PackageIssues.Prefix));
    }

    // ------------------------------------------------------------ P6: an index that could not be read

    [Fact]
    public void Versions_whose_index_cannot_be_read_are_carried_over_byte_for_byte_and_cannot_be_changed()
    {
        var path = Saved();
        var oldLive = PackageVersions.Of(path).Versions.Count;
        Assert.Equal(3, oldLive);
        Replace(path, "versions/index.json", "{ not json"u8.ToArray());
        var before = Entries(path, "versions/");
        List<string> oldParts;
        using (var zip = ZipFile.OpenRead(path)) oldParts = JsonNode.Parse(zip.GetEntry("manifest.json")!.Open())!["parts"]!.AsArray().Select(p => (string)p!["sha256"]!).ToList();

        var g = F();
        var files = PackageFiles.Create();
        files.Open(g.Session, path);
        Assert.NotNull(files.Versions(g.Session).Unreadable);
        Assert.Contains("could not be read", Assert.Throws<InvalidDataException>(() => files.SaveVersion(g.Session, "x", null, asBytes: false)).Message);
        Assert.Throws<InvalidDataException>(() => files.DeleteVersion(g.Session, 1, asBytes: false));

        // The live parts change: those the file had are kept as blobs, since a version may name them.
        g.Wall(0, 12, 5, 12);
        files.Save(g.Session, null);
        Assert.Contains(files.TakeWarnings(), w => w.Contains("versions could not be read", StringComparison.Ordinal));
        var after = Entries(path, "versions/");
        foreach (var (name, bytes) in before) Assert.Equal(bytes, after[name]);
        List<string> newParts;
        using (var zip = ZipFile.OpenRead(path)) newParts = JsonNode.Parse(zip.GetEntry("manifest.json")!.Open())!["parts"]!.AsArray().Select(p => (string)p!["sha256"]!).ToList();
        foreach (var sha in oldParts.Except(newParts)) Assert.Contains("versions/blobs/" + sha, after.Keys);
    }

    [Fact]
    public void Versions_that_can_neither_be_read_nor_carried_over_refuse_a_save_over_a_file_and_go_without_them_elsewhere()
    {
        var path = Saved();
        Replace(path, "versions/index.json", "null"u8.ToArray());
        // Read from the file itself (no copy), which then goes: what the versions were is nowhere to be read.
        var versions = PackageVersions.Of(path);
        Assert.NotNull(versions.Unreadable);
        var g = F();
        PackageFiles.Open(g.Session, path);
        var build = ProjectPackage.Build(g.Session);
        var other = At("other.lapin");
        File.Move(path, other);
        var held = File.ReadAllBytes(other);

        Assert.Contains("Save it under another name", Assert.Throws<InvalidDataException>(() => LapinPackage.Write(other, build, versions)).Message);
        Assert.Equal(held, File.ReadAllBytes(other));
        Assert.False(File.Exists(other + ".saving"));

        var fresh = At("fresh.lapin");
        var report = LapinPackage.Write(fresh, build, versions);
        Assert.Contains(report.Warnings, w => w.Contains("without them", StringComparison.Ordinal));
        Assert.Empty(Entries(fresh, "versions/"));
    }

    // ------------------------------------------------------------ P22: damaged parts and files

    [Fact]
    public void A_damaged_version_part_or_issued_file_is_not_copied_into_a_new_save_and_is_said()
    {
        var path = Saved();
        var blob = Entries(path, "versions/blobs/").Keys.First();
        var issued = Entries(path, PackageIssues.Prefix).Keys.First();
        Replace(path, blob, "damaged"u8.ToArray());
        Replace(path, issued, "damaged"u8.ToArray());

        var g = F();
        var files = PackageFiles.Create();
        files.Open(g.Session, path);
        var next = At("next.lapin");
        files.Save(g.Session, next);
        var warnings = files.TakeWarnings();
        Assert.Contains(warnings, w => w.Contains("version", StringComparison.Ordinal) && w.Contains("damaged", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.Contains("issued file", StringComparison.Ordinal) && w.Contains("damaged", StringComparison.Ordinal));
        Assert.DoesNotContain(blob, Entries(next, "versions/blobs/").Keys);
        Assert.DoesNotContain(issued, Entries(next, PackageIssues.Prefix).Keys);
        // The issued file the save did not write is not held: asked for, it says it is gone, never hands other bytes.
        var sha = issued[PackageIssues.Prefix.Length..];
        Assert.False(files.Issues(g.Session).Has(sha));
        Assert.Throws<InvalidDataException>(() => files.Issues(g.Session).Get(sha));
    }

    // ------------------------------------------------------------ P23: a failed write

    [Fact]
    public void A_save_that_fails_leaves_the_file_as_it_was_and_no_temporary_file()
    {
        var f = new Fixture();
        f.Wall(0, 0, 5, 0);
        var path = At("p.lapin");
        PackageFiles.Create().Save(f.Session, path);
        var before = File.ReadAllBytes(path);
        Assert.Throws<InvalidOperationException>(() =>
            LapinPackage.Write(path, f.Session.Model, _ => throw new InvalidOperationException("boom"), _ => null, "Lapin", "lapin", []));
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".saving"));
    }
}
