using System.IO.Compression;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The .lapin package, format 3: what is saved opens again exactly, in any product.</summary>
public sealed class PackageTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-tests-").FullName;
    string PathOf(string name) => Path.Combine(_dir, name);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static Fixture Project()
    {
        var f = new Fixture();
        f.Run("storey.create", []);
        var type = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, false), ("Insulation", 0.1, false));
        f.Wall(0, 0, 5, 0, type);
        var w = f.Wall(5, 0, 5, 4, type);
        f.Run("architecture.flipWall", new JsonObject { ["id"] = w });
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = Spatial.Storeys(f.Model).Last().Id, ["x1"] = 0, ["y1"] = 0, ["x2"] = 3, ["y2"] = 0, ["height"] = 2.75 });
        // A roof with its per-edge rules: nested attributes travel too.
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 4, ["style"] = "fr_provence" })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["edges"] = new JsonArray(null, new JsonObject { ["kind"] = "eave", ["pitch"] = 35 }) });
        return f;
    }

    static void SameModel(IModelView a, IModelView b)
    {
        Assert.Equal(a.Entities.Select(e => e.Id), b.Entities.Select(e => e.Id));   // creation order kept
        foreach (var e in a.Entities)
        {
            var o = b.Get(e.Id)!;
            Assert.Equal((e.Class, e.Name, e.PredefinedType), (o.Class, o.Name, o.PredefinedType));
            Assert.True(JsonNode.DeepEquals(e.Attrs, o.Attrs), $"{e.Class} {e.Id}: {e.Attrs.ToJsonString()} ≠ {o.Attrs.ToJsonString()}");
        }
        Assert.Equal(a.Relations.Select(r => r.Id), b.Relations.Select(r => r.Id));
        foreach (var r in a.Relations)
        {
            var o = b.GetRelation(r.Id)!;
            Assert.Equal((r.Class, r.Relating), (o.Class, o.Relating));
            Assert.Equal(r.Related, o.Related);
        }
    }

    [Fact]
    public void A_saved_project_opens_again_exactly()
    {
        var f = Project();
        var path = PathOf("house.lapin");
        PackageFiles.Create().Save(f.Session, path);
        Assert.False(f.Session.History.Dirty);

        var g = new Fixture();
        PackageFiles.Open(g.Session, path);
        SameModel(f.Model, g.Model);
        Assert.Equal(path, g.Session.Path);
        Assert.False(g.Session.History.CanUndo);
        foreach (var wall in f.Model.OfClass(IfcClass.Wall))
        {
            Assert.Equal(f.Bounds(wall.Id), g.Bounds(wall.Id));
            Assert.Equal(f.Usage(wall.Id), g.Usage(wall.Id));
        }
    }

    [Fact]
    public void The_model_is_split_by_module_class_and_storey_and_every_part_is_hashed()
    {
        var f = Project();
        var path = PathOf("parts.lapin");
        PackageFiles.Save(f.Session, path);
        using var zip = ZipFile.OpenRead(path);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        var storeys = Spatial.Storeys(f.Model).Select(s => s.Id).ToList();
        Assert.Contains("manifest.json", names);
        Assert.Contains("project/document.json", names);
        Assert.Contains("model/relations.arrow", names);
        Assert.Contains($"model/architecture/IfcWall/{storeys[0]}.arrow", names);
        Assert.Contains($"model/architecture/IfcWall/{storeys[1]}.arrow", names);
        Assert.Contains("model/architecture/IfcWallType/_.arrow", names);
        Assert.Contains("model/core/IfcBuildingStorey/_.arrow", names);
        Assert.Contains("model/core/IfcMaterialLayerSetUsage/_.arrow", names);

        var manifest = LapinPackage.Read(path).Manifest;
        Assert.Equal(3, manifest.Version);
        Assert.Equal("architecture", manifest.Classes[IfcClass.Wall]);
        Assert.All(manifest.Parts, p => Assert.Equal(64, p.Sha256.Length));
    }

    [Fact]
    public void A_damaged_part_is_refused()
    {
        var f = Project();
        var path = PathOf("damaged.lapin");
        PackageFiles.Save(f.Session, path);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            zip.GetEntry("model/relations.arrow")!.Delete();
            using var s = zip.CreateEntry("model/relations.arrow").Open();
            s.Write("not arrow"u8);
        }
        var e = Assert.Throws<InvalidDataException>(() => LapinPackage.Read(path));
        Assert.Contains("damaged", e.Message);
    }

    [Fact]
    public void A_product_without_a_module_keeps_its_elements_and_writes_them_back_intact()
    {
        var f = Project();
        var first = PathOf("full.lapin");
        PackageFiles.Save(f.Session, first);

        // Opened, changed and saved by a product without architecture.
        var bare = new Fixture(new Placeholder());
        PackageFiles.Open(bare.Session, first);
        bare.Run("storey.create", []);
        var second = PathOf("bare.lapin");
        PackageFiles.Save(bare.Session, second);
        using (var zip = ZipFile.OpenRead(second))
            Assert.Contains(zip.Entries, e => e.FullName.StartsWith("model/architecture/IfcWall/", StringComparison.Ordinal));

        // Back in the full product: the walls are as they were, and editable.
        var again = new Fixture();
        PackageFiles.Open(again.Session, second);
        foreach (var wall in f.Model.OfClass(IfcClass.Wall))
        {
            Assert.True(JsonNode.DeepEquals(wall.Attrs, again.Model.Get(wall.Id)!.Attrs));
            Assert.Equal(f.Bounds(wall.Id), again.Bounds(wall.Id));
        }
        Assert.Equal(3, Spatial.Storeys(again.Model).Count());
        again.Run("architecture.flipWall", new JsonObject { ["id"] = f.Model.OfClass(IfcClass.Wall).First().Id });
    }

    [Fact]
    public void A_project_from_a_newer_lapin_is_refused_with_a_reason()
    {
        var path = PathOf("new.lapin");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        using (var s = zip.CreateEntry("manifest.json").Open())
            s.Write("""{"format":"lapin-project","version":9,"app":"x","product":"x","modules":[],"parts":[],"classes":{}}"""u8);
        var e = Assert.Throws<InvalidDataException>(() => LapinPackage.Read(path));
        Assert.Contains("newer", e.Message);
    }

    sealed class Placeholder : ILapinModule
    {
        public string Id => "placeholder";
        public string Name => "Placeholder";
        public void Register(IModuleRegistry registry) { }
    }
}
