using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The protocol's calls around the project's file (audit 15.11.2026): nothing written to the file during a script's open
/// step, even by its owner (P1); an edit whose journal cannot be written still sends its diff, and says so (P10); a version
/// compared with the project as it is now shows no change an upgrade alone made (P17); an IFC export never writes over a
/// file the project links or was imported from (P14).
/// </summary>
public sealed class ProtocolSafetyTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-protocol-safety-").FullName;
    string At(string name) => Path.Combine(_dir, name);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static readonly object Kernel = new();

    static JsonObject Call(EngineDispatcher d, string method, JsonObject p, Caller? caller = null)
    {
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString(), caller ?? Caller.Local);
        return (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
    }

    static JsonObject WallArgs(Fixture f, double y) => new() { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = y, ["x2"] = 5, ["y2"] = y };

    // ------------------------------------------------------------ P1

    [Fact]
    public void Nothing_is_written_to_the_file_during_a_scripts_open_step_even_by_its_owner()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule(), new StructureModule());
        f.Wall(0, 0, 5, 0);
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plan", ["number"] = "A-101", ["paper"] = "A3" })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Plan", ["scale"] = 100 })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = plan, ["x"] = 150, ["y"] = 120 });
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = sheet, ["fields"] = new JsonObject { ["drawnBy"] = "IC", ["checkedBy"] = "AB", ["date"] = "30.10.2026", ["phase"] = "DTAC" } });
        var files = PackageFiles.Create();
        var path = At("house.lapin");
        files.Save(f.Session, path);
        files.SaveVersion(f.Session, "Concept", null, asBytes: false);
        var d = new EngineDispatcher(f.Session, files);
        var owner = Caller.Local with { Connection = Kernel };
        Assert.Null(Call(d, "doc.begin", new JsonObject { ["label"] = "Script" }, owner)["error"]);
        Assert.Null(Call(d, "doc.execute", new JsonObject { ["command"] = "architecture.addWall", ["args"] = WallArgs(f, 3) }, owner)["error"]);
        var held = File.ReadAllBytes(path);

        foreach (var (method, p) in new (string, JsonObject)[]
                 {
                     ("project.save", []), ("project.saveVersion", new() { ["name"] = "Mid step" }),
                     ("project.updateVersion", new() { ["number"] = 1, ["replace"] = true }), ("project.deleteVersion", new() { ["number"] = 1 }),
                 })
            Assert.Contains("open", (string?)Call(d, method, p, owner)["error"] ?? "no error");

        // An issue by the step's owner stands in the step, but its version is not saved half way through it.
        var issue = Call(d, "cde.issue", new JsonObject { ["sheets"] = new JsonArray(sheet), ["toFolder"] = false }, owner);
        Assert.Null(issue["error"]);
        Assert.Null(issue["result"]!["version"]);
        Assert.Contains("open", (string)issue["result"]!["versionError"]!);
        Assert.Equal(held, File.ReadAllBytes(path));

        Assert.Null(Call(d, "doc.rollback", [], owner)["error"]);
        Assert.Single(f.Model.OfClass(IfcClass.Wall));
        Assert.Equal(held, File.ReadAllBytes(path));
        Assert.Single(files.VersionList(f.Session));
    }

    // ------------------------------------------------------------ P10

    [Fact]
    public void An_edit_whose_journal_cannot_be_written_still_sends_its_diff_and_says_so()
    {
        var f = new Fixture();
        f.Session.HistoryChanged += _ => throw new IOException("No space left on device");
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var (reply, diff) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = "doc.execute", ["params"] = new JsonObject { ["command"] = "architecture.addWall", ["args"] = WallArgs(f, 0) } }.ToJsonString());
        var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
        Assert.Null(o["error"]);
        Assert.NotNull(o["result"]);
        Assert.Contains("recovery journal", (string)o["warnings"]![0]!);
        Assert.NotNull(diff);
        Assert.Contains(diff!.Nodes, n => n.Type == IfcClass.Wall);
        Assert.Single(f.Model.OfClass(IfcClass.Wall));

        // Said once: the next call's reply is clean unless the journal fails again.
        var (next, _) = d.Handle(new JsonObject { ["id"] = 2, ["method"] = "session.info", ["params"] = new JsonObject() }.ToJsonString());
        Assert.Null(JsonNode.Parse(BinaryFrame.HeaderText(next))!["warnings"]);
    }

    // ------------------------------------------------------------ P17

    /// <summary>An upgrade that marks every wall it brings up to date, as a project saved by an older build would be.</summary>
    sealed class Marking : ILapinModule
    {
        public string Id => "marking";
        public string Name => "Marking";
        public void Register(IModuleRegistry r) => r.Service<IProjectUpgrade>(new MarkWalls());

        sealed class MarkWalls : IProjectUpgrade
        {
            public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations) =>
                (entities.Select(e =>
                {
                    if (e.Class != IfcClass.Wall || e.Attrs["upgraded"] != null) return e;
                    var a = e.CloneAttrs();
                    a["upgraded"] = true;
                    return e.With(attrs: a);
                }).ToList(), relations);
        }
    }

    [Fact]
    public void A_version_compared_with_the_project_shows_no_change_an_upgrade_alone_made()
    {
        var f = new Fixture(new ArchitectureModule(), new Marking());
        f.Wall(0, 0, 5, 0);
        var path = At("old.lapin");
        var files = PackageFiles.Create();
        files.Save(f.Session, path);
        files.SaveVersion(f.Session, "Before the upgrade", null, asBytes: false);

        // Opened again: the walls brought up to date; the version holds them as they were saved.
        var g = new Fixture(new ArchitectureModule(), new Marking());
        var again = PackageFiles.Create();
        again.Open(g.Session, path);
        Assert.All(g.Model.OfClass(IfcClass.Wall), w => Assert.NotNull(w.Attrs["upgraded"]));
        var compared = again.CompareVersions(g.Session, JsonValue.Create(1), JsonValue.Create("current"));
        Assert.True((bool)compared["same"]!, compared.ToJsonString());
    }

    // ------------------------------------------------------------ P14

    [Fact]
    public void An_ifc_export_never_writes_over_a_file_the_project_links()
    {
        var f = new Fixture();
        f.Wall(0, 0, 5, 0);
        var linked = At("Site.ifc");
        File.WriteAllText(linked, "ISO-10303-21; the neighbour's model");
        f.Session.Edit("Link", edit => Links.Create(edit, "ifc", "Site.ifc", linked));
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());

        // The same file, also named with another case where the disk ignores it.
        var names = OperatingSystem.IsLinux() ? new[] { linked } : [linked, Path.Combine(_dir, "SITE.IFC")];
        foreach (var name in names)
            Assert.Contains("never writes over it", (string?)Call(d, "project.exportIfc", new JsonObject { ["path"] = name })["error"] ?? "no error");
        Assert.Equal("ISO-10303-21; the neighbour's model", File.ReadAllText(linked));

        var mine = At("Mine.ifc");
        Assert.Null(Call(d, "project.exportIfc", new JsonObject { ["path"] = mine })["error"]);
        Assert.True(File.Exists(mine));
    }
}
