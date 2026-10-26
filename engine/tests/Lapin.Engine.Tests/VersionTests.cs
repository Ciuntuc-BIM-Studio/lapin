using System.IO.Compression;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Versions in the project's file (docs/versions.md): numbered, sharing the
/// parts they have in common with the live project and one another, an
/// update in place keeping what it replaced, a version rebuilt as a package
/// of its own, two versions compared, and a version restored as one undo step.
/// </summary>
public sealed class VersionTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-versions-").FullName;
    string PathOf(string name) => Path.Combine(_dir, name);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static Fixture Project(int walls = 1)
    {
        var f = new Fixture();
        var type = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        for (var i = 0; i < walls; i++) f.Wall(0, i * 2.0, 5, i * 2.0, type);
        return f;
    }

    static List<string> Blobs(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        return zip.Entries.Where(e => e.FullName.StartsWith("versions/blobs/", StringComparison.Ordinal)).Select(e => e.FullName).ToList();
    }

    static (List<Entity> Entities, List<Relation> Relations) Snapshot(IModelView m) => (m.Entities.ToList(), m.Relations.ToList());

    static void Same((List<Entity> Entities, List<Relation> Relations) want, IEnumerable<Entity> entities, IEnumerable<Relation> relations)
    {
        var have = entities.ToDictionary(e => e.Id);
        Assert.Equal(want.Entities.Select(e => e.Id).Order(), have.Keys.Order());
        foreach (var e in want.Entities)
            Assert.True(ModelCompare.Same(e, have[e.Id]), $"{e.Class} {e.Id}: {e.Attrs.ToJsonString()} ≠ {have[e.Id].Attrs.ToJsonString()}");
        var rels = relations.ToDictionary(r => r.Id);
        Assert.Equal(want.Relations.Select(r => r.Id).Order(), rels.Keys.Order());
        foreach (var r in want.Relations) Assert.True(ModelCompare.Same(r, rels[r.Id]), $"{r.Class} {r.Id}");
    }

    [Fact]
    public void Numbers_versions_and_stores_a_part_shared_between_them_once()
    {
        var f = Project();
        var files = PackageFiles.Create();
        var path = PathOf("house.lapin");
        files.Save(f.Session, path);
        var (v1, _) = files.SaveVersion(f.Session, "Concept", "first", asBytes: false);
        Assert.Equal(1, (int)v1["number"]!);
        Assert.Equal("Concept", (string)v1["name"]!);
        // The first version is the live project: nothing to keep apart.
        Assert.Empty(Blobs(path));

        f.Wall(0, 4, 5, 4);
        var (v2, _) = files.SaveVersion(f.Session, "", "more walls", asBytes: false);
        Assert.Equal(2, (int)v2["number"]!);
        Assert.Equal("Version 2", (string)v2["name"]!);
        Assert.Equal("more walls", (string)v2["note"]!);
        Assert.Equal([1, 2], files.VersionList(f.Session).Select(v => (int)v!["number"]!));

        // Only the parts the second version changed are kept for the first; the rest are shared with the live project.
        var versions = PackageVersions.Of(path);
        var live = versions.Require(2).Manifest.Parts.Select(p => p.Sha256).ToHashSet();
        var kept = versions.Require(1).Manifest.Parts.Where(p => !live.Contains(p.Sha256)).Select(p => p.Sha256).Distinct().Order().ToList();
        Assert.NotEmpty(kept);
        Assert.True(kept.Count < versions.Require(1).Manifest.Parts.Count, "the unchanged parts are not copied");
        Assert.Equal(kept, Blobs(path).Select(b => b["versions/blobs/".Length..]).Order());
        Assert.False(f.Session.History.Dirty);
    }

    [Fact]
    public void Updates_a_version_in_place_keeping_the_replaced_content_as_a_revision()
    {
        var f = Project();
        var files = PackageFiles.Create();
        files.Save(f.Session, PathOf("permit.lapin"));
        files.SaveVersion(f.Session, "Permit", "submitted", asBytes: false);
        var before = Snapshot(f.Model);
        f.Wall(0, 4, 5, 4);
        var (updated, _) = files.UpdateVersion(f.Session, 1, null, "fixed after review", replace: true, asBytes: false);
        Assert.Equal(1, (int)updated["number"]!);
        Assert.Equal("Permit", (string)updated["name"]!);
        Assert.Equal("fixed after review", (string)updated["note"]!);
        var revisions = (JsonArray)updated["revisions"]!;
        Assert.Single(revisions);
        Assert.Equal("submitted", (string)revisions[0]!["note"]!);
        Assert.Single(files.VersionList(f.Session));

        // The revision still opens as the project was when the version was first made.
        var versions = files.Versions(f.Session);
        var content = versions.Content(versions.Require(1).Revisions[0].Manifest);
        Same(before, content.Entities, content.Relations);
        // A rename alone keeps the content and makes no revision.
        var (renamed, _) = files.UpdateVersion(f.Session, 1, "Permit (final)", null, replace: false, asBytes: false);
        Assert.Equal("Permit (final)", (string)renamed["name"]!);
        Assert.Single((JsonArray)renamed["revisions"]!);
    }

    [Fact]
    public void Rebuilds_a_version_into_a_package_that_opens_on_its_own()
    {
        var f = Project(2);
        var files = PackageFiles.Create();
        files.Save(f.Session, PathOf("a.lapin"));
        files.SaveVersion(f.Session, "A", null, asBytes: false);
        var was = Snapshot(f.Model);
        f.Wall(0, 6, 5, 6);
        files.Save(f.Session, null);

        var (name, bytes) = files.ExportVersion(f.Session, 1, null);
        Assert.Equal("a v1.lapin", name);
        var standalone = PathOf(name);
        File.WriteAllBytes(standalone, bytes);
        var g = new Fixture();
        PackageFiles.Open(g.Session, standalone);
        Same(was, g.Model.Entities, g.Model.Relations);
        Assert.Empty(PackageVersions.Of(standalone).Versions);
    }

    [Fact]
    public void Compares_two_versions_element_by_element_and_part_by_part()
    {
        var f = Project();
        var files = PackageFiles.Create();
        files.Save(f.Session, PathOf("c.lapin"));
        files.SaveVersion(f.Session, "one wall", null, asBytes: false);
        var wall = f.Wall(0, 4, 5, 4);
        files.SaveVersion(f.Session, "two walls", null, asBytes: false);

        var diff = files.CompareVersions(f.Session, 1, 2);
        Assert.False((bool)diff["same"]!);
        var added = (JsonArray)diff["entities"]!["added"]!;
        Assert.Contains(added, e => (string)e!["id"]! == wall && (string)e["class"]! == IfcClass.Wall);
        Assert.Empty((JsonArray)diff["entities"]!["removed"]!);
        Assert.True((int)diff["relations"]!["added"]! > 0 || (int)diff["relations"]!["changed"]! > 0);
        // The parts: the walls' chunk changed (the architecture's area), the relationships' part too (the model's).
        var areas = (JsonObject)diff["parts"]!["areas"]!;
        Assert.Equal(1, (int)areas["architecture"]!);
        Assert.Equal(1, (int)areas["model"]!);
        Assert.True((int)diff["parts"]!["same"]! > 0);

        // The project as it is now is the second version.
        Assert.True((bool)files.CompareVersions(f.Session, 2, "current")["same"]!);
        var toCurrent = files.CompareVersions(f.Session, 1, "current");
        Assert.Equal("current", (string)toCurrent["b"]!);
        Assert.Contains((JsonArray)toCurrent["entities"]!["added"]!, e => (string)e!["id"]! == wall);

        // A changed entity says which fields differ.
        f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["height"] = 3.3 } });
        var changed = (JsonArray)files.CompareVersions(f.Session, 2, "current")["entities"]!["changed"]!;
        var c = Assert.Single(changed, e => (string)e!["id"]! == wall);
        Assert.Contains("height", ((JsonArray)c!["fields"]!).Select(x => (string)x!));
    }

    [Fact]
    public void Restores_a_version_as_one_undo_step()
    {
        var f = Project(2);
        var files = PackageFiles.Create();
        files.Save(f.Session, PathOf("r.lapin"));
        files.SaveVersion(f.Session, "before", null, asBytes: false);
        var v1 = Snapshot(f.Model);
        var first = f.Model.OfClass(IfcClass.Wall).First().Id;
        f.Run("element.delete", new JsonObject { ["id"] = first });
        f.Wall(0, 6, 5, 6);
        f.Run("storey.create", []);
        var edited = Snapshot(f.Model);
        Assert.NotEqual(v1.Entities.Count, edited.Entities.Count);

        files.RestoreVersion(f.Session, 1, null);
        Same(v1, f.Model.Entities, f.Model.Relations);
        Assert.True(f.Session.History.Dirty);
        Assert.Equal("Restore version 1", f.Session.History.UndoLabel);

        f.Session.Undo();
        Same(edited, f.Model.Entities, f.Model.Relations);
        f.Session.Redo();
        Same(v1, f.Model.Entities, f.Model.Relations);
    }

    [Fact]
    public void Versions_follow_the_project_through_saves_and_go_when_deleted()
    {
        var f = Project();
        var files = PackageFiles.Create();
        var a = PathOf("a.lapin");
        files.Save(f.Session, a);
        files.SaveVersion(f.Session, "v1", null, asBytes: false);
        var was = Snapshot(f.Model);
        f.Wall(0, 4, 5, 4);

        // Save as: the versions come along, their parts found in the new file from now on.
        var b = PathOf("b.lapin");
        files.Save(f.Session, b);
        Assert.Single(PackageVersions.Of(b).Versions);
        File.Delete(a);
        f.Wall(0, 6, 5, 6);
        files.Save(f.Session, null);
        var content = PackageVersions.Of(b).Content(PackageVersions.Of(b).Require(1).Manifest);
        Same(was, content.Entities, content.Relations);

        // Opened again in another session: the versions are there; a plain save keeps them.
        var g = new Fixture();
        var other = PackageFiles.Create();
        other.Open(g.Session, b);
        Assert.Single(other.VersionList(g.Session));
        other.Save(g.Session, null);
        Assert.Single(PackageVersions.Of(b).Versions);

        other.DeleteVersion(g.Session, 1, asBytes: false);
        Assert.Empty(other.VersionList(g.Session));
        Assert.Empty(PackageVersions.Of(b).Versions);
        Assert.Empty(Blobs(b));
        using var zip = ZipFile.OpenRead(b);
        Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("versions/", StringComparison.Ordinal));
    }

    [Fact]
    public void In_the_browser_the_versions_travel_in_the_bytes()
    {
        var f = Project();
        var files = new ProjectFiles((_, _) => throw new InvalidOperationException("no files"), (_, _, _) => throw new InvalidOperationException("no files"));
        var (bytes0, _) = files.SaveBytes(f.Session, "house.lapin");
        files.OpenBytes(f.Session, "house.lapin", bytes0);
        var (r1, _) = files.SaveVersion(f.Session, "v1", null, asBytes: true);
        Assert.Equal("house.lapin", (string)r1["name"]!);
        var bytes1 = Convert.FromBase64String((string)r1["data"]!);
        Assert.Single(PackageVersions.Of(bytes1).Versions);
        var was = Snapshot(f.Model);

        f.Wall(0, 4, 5, 4);
        var (bytes2, _) = files.SaveBytes(f.Session, "house.lapin");
        var versions = PackageVersions.Of(bytes2);
        var content = versions.Content(versions.Require(1).Manifest);
        Same(was, content.Entities, content.Relations);

        // Opened from those bytes in another session, the version restores.
        var g = new Fixture();
        var other = new ProjectFiles((_, _) => throw new InvalidOperationException("no files"), (_, _, _) => throw new InvalidOperationException("no files"));
        other.OpenBytes(g.Session, "house.lapin", bytes2);
        other.RestoreVersion(g.Session, 1, null);
        Same(was, g.Model.Entities, g.Model.Relations);
    }

    [Fact]
    public void The_protocol_offers_the_versions()
    {
        var f = Project();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var path = PathOf("p.lapin");
        d.Handle(new JsonObject { ["id"] = 1, ["method"] = "project.save", ["params"] = new JsonObject { ["path"] = path } }.ToJsonString());
        var (reply, diff) = d.Handle("""{"id":2,"method":"project.saveVersion","params":{"name":"Permit","note":"sent"}}""");
        Assert.NotNull(diff);
        Assert.Contains("\"number\":1", System.Text.Encoding.UTF8.GetString(reply));
        var (list, _) = d.Handle("""{"id":3,"method":"project.versions","params":{}}""");
        Assert.Contains("\"Permit\"", System.Text.Encoding.UTF8.GetString(list));
        var (cmp, _) = d.Handle("""{"id":4,"method":"project.compareVersions","params":{"a":1,"b":"current"}}""");
        Assert.Contains("\"same\":true", System.Text.Encoding.UTF8.GetString(cmp));
        var (missing, _) = d.Handle("""{"id":5,"method":"project.restoreVersion","params":{"number":7}}""");
        Assert.Contains("no version 7", System.Text.Encoding.UTF8.GetString(missing));
    }
}
