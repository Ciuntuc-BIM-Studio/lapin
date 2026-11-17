using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// File → Import types from a project… (issue #74, decision 669): another .lapin's types listed with the materials each needs
/// (library.projectTypes, the source read into a model of its own and never written to), the chosen ones brought in by
/// library.importProjectTypes under the rules of a .lapintypes (materials by name, a name clash kept beside as „Name (2)” or as
/// chosen), keeping their GlobalIds, a type the project has under the very GlobalId being that type; one undo step.
/// </summary>
public sealed class ProjectTypesImportTests
{
    static Fixture F() => new(new ArchitectureModule());

    static string WallType(Fixture f, string name = "WT1 Exterior", string brick = "Brick") => (string)f.Run("architecture.createWallType", new JsonObject
    {
        ["name"] = name, ["mark"] = "WT1",
        ["layers"] = new JsonArray(
            new JsonObject { ["material"] = "Render", ["thickness"] = 0.015 },
            new JsonObject { ["material"] = "EPS insulation", ["thickness"] = 0.1 },
            new JsonObject { ["material"] = brick, ["thickness"] = 0.25, ["core"] = true },
            new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.015 }),
    })!["id"]!;

    static string DoorType(Fixture f) => (string)f.Run("architecture.createJoineryType", new JsonObject
    {
        ["spec"] = (JoinerySpec.DefaultDoor("D1 Oak") with { Width = 1.0 }).ToJson(),
    })!["id"]!;

    static Entity Material(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).Single(m => m.Name == name);

    /// <summary>The source saved as a .lapin through the package writer, as the app saves it.</summary>
    static string Save(Fixture f)
    {
        var path = Path.Combine(Path.GetTempPath(), $"types-source-{Guid.NewGuid():N}.lapin");
        PackageFiles.Save(f.Session, path);
        return path;
    }

    static JsonNode Call(EngineDispatcher d, string method, JsonObject p)
    {
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
        var o = JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
        Assert.Null((string?)o["error"]);
        return o["result"]!;
    }

    static string? Error(EngineDispatcher d, string method, JsonObject p)
    {
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
        return (string?)JsonNode.Parse(BinaryFrame.HeaderText(reply))!["error"];
    }

    static JsonObject Preview(Fixture to, string path) => (JsonObject)Call(new EngineDispatcher(to.Session, PackageFiles.Create()), "library.projectTypes", new JsonObject { ["path"] = path });

    static JsonObject Import(Fixture to, JsonObject preview, IEnumerable<string>? refs = null, JsonObject? extra = null)
    {
        var a = new JsonObject { ["text"] = (string)preview["text"]! };
        if (refs != null) a["refs"] = new JsonArray([.. refs.Select(r => (JsonNode)r)]);
        foreach (var (k, v) in extra ?? []) a[k] = v?.DeepClone();
        return (JsonObject)to.Run("library.importProjectTypes", a)!;
    }

    static JsonObject TypeRow(JsonObject preview, string name) => (JsonObject)preview["types"]!.AsArray().Single(t => (string?)t!["name"] == name)!;

    [Fact]
    public void The_chosen_types_come_with_their_materials_and_their_GlobalIds_and_the_source_file_is_not_touched()
    {
        var from = F();
        var wall = WallType(from);
        var door = DoorType(from);
        from.Wall(0, 0, 5, 0, wall);
        from.Wall(5, 0, 5, 4, wall);
        from.Run("material.update", new JsonObject { ["id"] = Material(from, "EPS insulation").Id, ["color"] = "#f0e68c" });
        var path = Save(from);
        var hash = SHA256.HashData(File.ReadAllBytes(path));
        var stamp = File.GetLastWriteTimeUtc(path);

        var to = F();
        var p = Preview(to, path);
        Assert.Equal(Path.GetFileNameWithoutExtension(path), (string?)p["name"]);
        var w = TypeRow(p, "WT1 Exterior");
        Assert.Equal(wall, (string?)w["ref"]);
        Assert.Equal(IfcClass.WallType, (string?)w["class"]);
        Assert.Equal("new", (string?)w["status"]);
        Assert.Equal(2, (int)w["count"]!);
        Assert.Equal(["Brick", "EPS insulation", "Plaster", "Render"], w["materials"]!.AsArray().Select(m => (string?)m));
        var d = TypeRow(p, "D1 Oak");
        Assert.Equal(0, (int)d["count"]!);
        Assert.Contains("Door leaf", d["materials"]!.AsArray().Select(m => (string?)m));
        Assert.Equal(0, (int)p["conflicts"]!);

        // Only the wall type chosen: it comes under its own GlobalId, its materials with it, the door's not.
        var r = Import(to, p, [wall]);
        Assert.Equal(1, (int)r["created"]!);
        var made = to.Model.Get(wall)!;
        Assert.Equal("WT1 Exterior", made.Name);
        Assert.Equal("WT1", made.Str("mark"));
        Assert.Equal(["Render", "EPS insulation", "Brick", "Plaster"], LayerSets.LayersOf(to.Model, wall).Select(l => to.Model.Get(l.Material)!.Name));
        Assert.Equal("#f0e68c", Material(to, "EPS insulation").Str("color"));
        Assert.DoesNotContain(to.Model.OfClass(IfcClass.DoorType), t => t.Name == "D1 Oak");
        Assert.DoesNotContain(to.Model.OfClass(IfcClass.Material), m => m.Name == "Door leaf");
        // Types only: no wall of the source came along.
        Assert.Empty(to.Model.OfClass(IfcClass.Wall));
        // A wall of the type is drawn with its layers.
        var placed = to.Wall(0, 0, 4, 0, wall);
        Assert.Equal(wall, to.Model.RelatingOf(placed, IfcClass.RelDefinesByType)!.Id);

        // The source as it was, byte for byte; and the source's session too.
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(path)));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
        Assert.Equal(2, from.Model.OfClass(IfcClass.Wall).Count());
        File.Delete(path);
    }

    [Fact]
    public void One_undo_takes_the_whole_import_back()
    {
        var from = F();
        WallType(from);
        DoorType(from);
        var path = Save(from);
        var to = F();
        var entities = to.Model.Entities.Select(e => e.Id).ToHashSet();
        var relations = to.Model.Relations.Count();
        var p = Preview(to, path);
        // Reading the source is not an edit.
        Assert.Equal(entities, to.Model.Entities.Select(e => e.Id).ToHashSet());
        var r = Import(to, p);
        Assert.Equal(2, (int)r["created"]!);
        Assert.Contains(to.Model.OfClass(IfcClass.Material), m => m.Name == "Door leaf");
        to.Session.Undo();
        Assert.Equal(entities, to.Model.Entities.Select(e => e.Id).ToHashSet());
        Assert.Equal(relations, to.Model.Relations.Count());
        to.Session.Redo();
        Assert.Single(to.Model.OfClass(IfcClass.WallType), t => t.Name == "WT1 Exterior");
        File.Delete(path);
    }

    [Fact]
    public void The_same_GlobalId_is_the_same_type_reused_when_unchanged_updated_in_place_when_changed()
    {
        var from = F();
        var wall = WallType(from);
        var path = Save(from);
        var to = F();
        Import(to, Preview(to, path));
        var placed = to.Wall(0, 0, 4, 0, wall);

        // Read again unchanged: the same, nothing to choose, nothing added.
        var again = Preview(to, path);
        Assert.Equal("same", (string?)TypeRow(again, "WT1 Exterior")["status"]);
        var count = to.Model.Entities.Count();
        var r = Import(to, again);
        Assert.Equal(1, (int)r["reused"]!);
        Assert.Equal(count, to.Model.Entities.Count());

        // Changed in the source (its mark, and renamed): the same type by its GlobalId, updated in place by default; its walls follow.
        from.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["mark"] = "WT9" } });
        from.Run("element.rename", new JsonObject { ["id"] = wall, ["name"] = "WT9 Renamed" });
        File.Delete(path);
        path = Save(from);
        var changed = Preview(to, path);
        var row = TypeRow(changed, "WT9 Renamed");
        Assert.Equal("id", (string?)row["status"]);
        Assert.Contains("mark", row["differs"]!.AsArray().Select(x => (string?)x));
        Assert.Equal("update", (string?)changed["rows"]!.AsArray().Single(x => (string?)x!["key"] == "type:" + wall)!["choice"]);
        r = Import(to, changed);
        Assert.Equal(1, (int)r["updated"]!);
        Assert.Single(to.Model.OfClass(IfcClass.WallType));
        Assert.Equal("WT9", to.Model.Get(wall)!.Str("mark"));
        Assert.Equal(wall, to.Model.RelatingOf(placed, IfcClass.RelDefinesByType)!.Id);

        // Kept beside instead: under a new GlobalId, the project's left as it was.
        from.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["mark"] = "WT7" } });
        File.Delete(path);
        path = Save(from);
        r = Import(to, Preview(to, path), extra: new JsonObject { ["types"] = new JsonObject { [wall] = "both" } });
        Assert.Equal(1, (int)r["created"]!);
        Assert.Equal("WT9", to.Model.Get(wall)!.Str("mark"));
        var beside = to.Model.OfClass(IfcClass.WallType).Single(t => t.Id != wall);
        Assert.Equal("WT7", beside.Str("mark"));
        Assert.True(GlobalId.IsValid(beside.Id));

        // Or skipped: nothing changes.
        count = to.Model.Entities.Count();
        r = Import(to, Preview(to, path), extra: new JsonObject { ["all"] = "skip" });
        Assert.Equal(1, (int)r["skipped"]!);
        Assert.Equal(count, to.Model.Entities.Count());
        File.Delete(path);
    }

    [Fact]
    public void A_name_clash_asks_as_a_type_pack_does_kept_beside_by_default_the_material_the_project_s()
    {
        var from = F();
        var wall = WallType(from);
        from.Run("material.update", new JsonObject { ["id"] = Material(from, "Brick").Id, ["color"] = "#aa3311" });
        var path = Save(from);

        var to = F();
        var own = WallType(to, brick: "Concrete");
        to.Run("material.update", new JsonObject { ["id"] = to.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Render").Id, ["color"] = "#ffffff" });
        // The project's own Brick, another colour.
        to.Run("architecture.createWallType", new JsonObject { ["name"] = "Own", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.3, ["core"] = true }) });
        var brick = Material(to, "Brick");
        var p = Preview(to, path);
        var row = TypeRow(p, "WT1 Exterior");
        Assert.Equal("name", (string?)row["status"]);
        Assert.Contains("layers", row["differs"]!.AsArray().Select(x => (string?)x));
        Assert.Contains(p["materials"]!.AsArray(), m => (string?)m!["name"] == "Brick");
        Assert.True((int)p["conflicts"]! >= 2);

        var r = Import(to, p);
        Assert.Equal("renamed", (string?)r["types"]![0]!["status"]);
        var made = to.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "WT1 Exterior (2)");
        // The pack's type keeps its GlobalId (free here); the project's own is as it was.
        Assert.Equal(wall, made.Id);
        Assert.Equal("Concrete", to.Model.Get(LayerSets.LayersOf(to.Model, own)[2].Material)!.Name);
        // The material by the same rule as a pack's: the project's Brick kept as it is.
        Assert.Same(brick, Material(to, "Brick"));
        Assert.Single(to.Model.OfClass(IfcClass.Material), m => m.Name == "Brick");

        // Undone, then the project's replaced and its Brick updated from the source: one type of the name, its id the project's.
        to.Session.Undo();
        r = Import(to, Preview(to, path), extra: new JsonObject { ["types"] = new JsonObject { [wall] = "update" }, ["materials"] = new JsonObject { ["Brick"] = "update" } });
        Assert.Equal(1, (int)r["updated"]!);
        Assert.Equal(1, (int)r["materialsUpdated"]!);
        Assert.Single(to.Model.OfClass(IfcClass.WallType), t => t.Name == "WT1 Exterior");
        Assert.Null(to.Model.Get(wall));
        Assert.Equal("#aa3311", Material(to, "Brick").Str("color"));
        File.Delete(path);
    }

    [Fact]
    public void What_is_not_a_project_is_refused_and_a_guest_cannot_read_one()
    {
        var to = F();
        var d = new EngineDispatcher(to.Session, PackageFiles.Create());
        Assert.Contains("not a Lapin project", Error(d, "library.projectTypes", new JsonObject { ["name"] = "x.lapin", ["data"] = Convert.ToBase64String("not a zip"u8.ToArray()) }));
        // A project with no types.
        var empty = F();
        var path = Save(empty);
        Assert.Contains("no types", Error(d, "library.projectTypes", new JsonObject { ["path"] = path }));
        File.Delete(path);
        // Reading a file is the host's own: a guest is refused, by path or by bytes.
        Assert.NotNull(EngineDispatcher.AllowedRemote("library.projectTypes", new JsonObject { ["data"] = "AA==" }, "editor"));
    }

    /// <summary>An upgrade that marks every wall type it brings up to date, as a project saved by an older build would be.</summary>
    sealed class Marking : ILapinModule
    {
        public string Id => "marking";
        public string Name => "Marking";
        public void Register(IModuleRegistry r) => r.Service<IProjectUpgrade>(new MarkTypes());

        sealed class MarkTypes : IProjectUpgrade
        {
            public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations) =>
                (entities.Select(e =>
                {
                    if (e.Class != IfcClass.WallType || e.Attrs["upgraded"] != null) return e;
                    var a = e.CloneAttrs();
                    a["upgraded"] = true;
                    return e.With(attrs: a);
                }).ToList(), relations);
        }
    }

    [Fact]
    public void The_source_is_brought_up_to_date_in_memory_as_an_opened_file_is_and_its_file_left_as_it_was()
    {
        // Written by a build without the upgrade; read by one with it.
        var from = F();
        var wall = WallType(from);
        var path = Save(from);
        var hash = SHA256.HashData(File.ReadAllBytes(path));
        var to = new Fixture(new ArchitectureModule(), new Marking());
        Import(to, Preview(to, path));
        Assert.True((bool?)to.Model.Get(wall)!.Attrs["upgraded"]);
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(path)));
        File.Delete(path);
    }

    [Fact]
    public void In_a_browser_the_bytes_serve_as_the_path_does()
    {
        var from = F();
        var wall = WallType(from);
        var path = Save(from);
        var to = F();
        var p = (JsonObject)Call(new EngineDispatcher(to.Session, PackageFiles.Create()), "library.projectTypes",
            new JsonObject { ["name"] = "House.lapin", ["data"] = Convert.ToBase64String(File.ReadAllBytes(path)) });
        Assert.Equal("House", (string?)p["name"]);
        Import(to, p);
        Assert.Equal("WT1 Exterior", to.Model.Get(wall)!.Name);
        File.Delete(path);
    }
}
