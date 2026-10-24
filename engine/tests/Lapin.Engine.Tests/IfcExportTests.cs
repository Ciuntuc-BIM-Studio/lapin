using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Phases;
using Lapin.Platform;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// The IFC export (docs/spec-ifc.md): the file parses, every element comes
/// with its relationships and sets, the openings leave the host's body uncut,
/// the ids hold between two exports, a zoned wall is a group of parts, IFC
/// 4.3 names the civil elements, and a phase filters what is written.
/// </summary>
public class IfcExportTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture Full() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new CivilModule(), new PhasesModule(), new StudioModule());

    string _notes = "";

    string Export(Fixture f, string name, IfcExportOptions? options = null)
    {
        var path = Path.Combine(_dir, name);
        _notes = string.Join("; ", IfcExport.Write(f.Session, path, options).Notes);
        return path;
    }

    static List<StepEntity> RepresentationsOf(StepFile f, StepEntity product) =>
        product[6] == null ? [] : Items(f.Get(product[6])![2]).Select(r => f.Get(r)!).ToList();

    static int FacesOf(StepFile f, StepEntity product)
    {
        var body = RepresentationsOf(f, product).Single(r => (string?)r[1] == "Body");
        return Items(body[3]).Sum(b => Items(f.Get(f.Get(b)![0])![0]).Count);
    }

    static StepEntity One(StepFile f, string type) => Assert.Single(f.OfType(type));
    static List<object?> Items(object? o) => o as List<object?> ?? [];
    static StepRef Ref(StepEntity e) => new(e.Id);

    static List<StepEntity> SetsOf(StepFile f, StepEntity e) =>
        f.OfType("IFCRELDEFINESBYPROPERTIES").Where(r => Items(r[4]).Contains(Ref(e))).Select(r => f.Get(r[5])!).ToList();

    static List<string?> PropertyNames(StepFile f, StepEntity set) => Items(set[4]).Select(p => (string?)f.Get(p)![0]).ToList();

    [Fact]
    public void A_small_house_exports_with_its_elements_relationships_and_sets()
    {
        var f = Full();
        var type = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        var wall = f.Wall(0, 0, 6, 0, type);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4 });
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 4), new JsonArray(0, 4)) });
        var ct = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C 30x30", ["section"] = new JsonObject { ["shape"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = ct, ["x"] = 3, ["y"] = 2 });
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = 0, ["y1"] = -1, ["x2"] = 0, ["y2"] = 5 });
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = -1, ["y1"] = 0, ["x2"] = 7, ["y2"] = 0 });

        var path = Export(f, "house.ifc");
        var step = StepFile.Read(path);
        Assert.Equal("IFC4", step.Schema);
        Assert.Single(step.OfType("IFCPROJECT"));
        Assert.Equal(Spatial.Storeys(f.Model).Count(), step.OfType("IFCBUILDINGSTOREY").Count());
        Assert.Single(step.OfType("IFCSLAB"));
        Assert.Single(step.OfType("IFCCOLUMN"));
        Assert.Single(step.OfType("IFCGRID"));
        Assert.Single(step.OfType("IFCDOOR"));
        Assert.Single(step.OfType("IFCWINDOW"));
        Assert.Equal(2, step.OfType("IFCOPENINGELEMENT").Count());
        Assert.Equal(2, step.OfType("IFCRELVOIDSELEMENT").Count());
        Assert.Equal(2, step.OfType("IFCRELFILLSELEMENT").Count());
        Assert.Single(step.OfType("IFCWALLTYPE"));
        Assert.True(step.OfType("IFCMATERIALLAYERSETUSAGE").Count() >= 2, "the wall and the slab use their layer sets");
        Assert.Contains(step.OfType("IFCRELCONTAINEDINSPATIALSTRUCTURE"), r => Items(r[4]).Count >= 5);

        // The wall keeps its id and carries its axis. Its door and window give it lintels, so it is a group: the layers and
        // the lintels are elements of their own under it; the layers the lintels do not touch are prisms the openings have not cut.
        var w = One(step, "IFCWALL");
        Assert.Equal(wall, (string?)w[0]);
        var reps = RepresentationsOf(step, w);
        Assert.DoesNotContain(reps, r => (string?)r[1] == "Body");
        Assert.Contains(reps, r => (string?)r[1] == "Axis");
        var parts = Items(step.OfType("IFCRELAGGREGATES").Single(r => Equals(r[4], Ref(w)))[5]).Select(p => step.Get(p)!).ToList();
        Assert.True(parts.Count >= 5, $"3 layers and the lintels: {parts.Count} parts; {_notes}");
        foreach (var layer in parts.Where(p => ((string?)p[7])!.EndsWith("/layer:0", StringComparison.Ordinal) || ((string?)p[7])!.EndsWith("/layer:2", StringComparison.Ordinal)))
            Assert.Equal(6, FacesOf(step, layer));
        Assert.True(step.OfType("IFCBEAM").Count(b => Equals(b[8], new StepEnum("LINTEL"))) >= 2);

        // The door says its size; the wall carries its intent and the common set.
        Assert.Equal(0.9, (double)One(step, "IFCDOOR")[9]!, 6);
        Assert.Equal(2.1, (double)One(step, "IFCDOOR")[8]!, 6);
        var sets = SetsOf(step, w);
        var intent = sets.Single(s => (string?)s[2] == "Pset_Lapin_Intent");
        Assert.Contains("height", PropertyNames(step, intent));
        Assert.Contains("start", PropertyNames(step, intent));
        Assert.Contains(sets, s => (string?)s[2] == "Pset_WallCommon");
        Assert.Contains(sets, s => s.Type == "IFCELEMENTQUANTITY" && (string?)s[2] == "Qto_WallBaseQuantities");
    }

    [Fact]
    public void Two_exports_of_one_project_are_the_same_file_but_for_the_time_stamp()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 5, 0);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 2 });
        static string Data(string path) => string.Join("\n", File.ReadAllLines(path).Where(l => !l.StartsWith("FILE_NAME", StringComparison.Ordinal)));
        Assert.Equal(Data(Export(f, "a.ifc")), Data(Export(f, "b.ifc")));
    }

    [Fact]
    public void A_zoned_wall_is_a_group_of_parts_and_one_body_in_the_simple_detail()
    {
        var f = Full();
        var type = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Zoned 30",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.3, ["core"] = true }),
            ["zones"] = new JsonArray(
                new JsonObject { ["key"] = "first-course", ["name"] = "First course", ["role"] = "masonry", ["height"] = 0.25, ["material"] = "AAC block" },
                new JsonObject { ["key"] = "masonry", ["name"] = "Masonry", ["role"] = "masonry", ["height"] = "rest", ["material"] = "Brick" },
                new JsonObject { ["key"] = "ring-beam", ["name"] = "Ring beam", ["role"] = "ringBeam", ["height"] = 0.25, ["material"] = "Reinforced concrete" }),
        })!["id"]!;
        var wall = f.Wall(0, 0, 5, 0, type);

        var grouped = StepFile.Read(Export(f, "grouped.ifc"));
        var w = One(grouped, "IFCWALL");
        Assert.DoesNotContain(RepresentationsOf(grouped, w), r => (string?)r[1] == "Body");
        var parts = Items(grouped.OfType("IFCRELAGGREGATES").Single(r => Equals(r[4], Ref(w)))[5]);
        Assert.True(parts.Count == 3, $"{parts.Count} parts; {_notes}");
        Assert.Equal(2, grouped.OfType("IFCBUILDINGELEMENTPART").Count());
        Assert.Contains(grouped.OfType("IFCBEAM"), b => (string?)b[4] == "Ring beam" && Equals(b[8], new StepEnum("USERDEFINED")));
        Assert.Equal(wall, (string?)w[0]);

        var simple = StepFile.Read(Export(f, "simple.ifc", new IfcExportOptions(Detail: "simple")));
        var ws = One(simple, "IFCWALL");
        Assert.Contains(RepresentationsOf(simple, ws), r => (string?)r[1] == "Body");
        Assert.DoesNotContain(simple.OfType("IFCRELAGGREGATES"), r => Equals(r[4], Ref(ws)));
        Assert.Empty(simple.OfType("IFCBUILDINGELEMENTPART"));
    }

    [Fact]
    public void IFC4X3_names_the_terrain_and_the_platform_as_4_3_does()
    {
        var f = Full();
        var lines = new List<string>();
        for (var i = 0; i <= 4; i++) for (var j = 0; j <= 4; j++) lines.Add($"{i * 10} {j * 10} {100 + 0.05 * i * 10}");
        var terrain = (string)f.Run("civil.addTerrain", new JsonObject { ["text"] = string.Join("\n", lines), ["datum"] = 100 })!["id"]!;
        f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = terrain, ["x1"] = 10, ["y1"] = 10, ["x2"] = 30, ["y2"] = 20, ["elevation"] = 1.0 });

        var ifc4 = StepFile.Read(Export(f, "site4.ifc"));
        Assert.True(ifc4.OfType("IFCGEOGRAPHICELEMENT").Any(), $"no terrain written; {_notes}");
        Assert.Equal("IFC4", ifc4.Schema);
        Assert.Equal(new StepEnum("TERRAIN"), One(ifc4, "IFCGEOGRAPHICELEMENT")[8]);
        Assert.Equal("Earthworks cut", (string?)One(ifc4, "IFCBUILDINGELEMENTPROXY")[4]);
        Assert.Empty(ifc4.OfType("IFCEARTHWORKSCUT"));

        var ifc43 = StepFile.Read(Export(f, "site43.ifc", new IfcExportOptions(Schema: "IFC4X3")));
        Assert.Equal("IFC4X3_ADD2", ifc43.Schema);
        Assert.Single(ifc43.OfType("IFCEARTHWORKSCUT"));
        Assert.Empty(ifc43.OfType("IFCBUILDINGELEMENTPROXY"));
    }

    [Fact]
    public void A_phase_exports_what_exists_at_its_end()
    {
        var f = Full();
        var old = f.Wall(0, 0, 5, 0);
        var later = f.Wall(0, 3, 5, 3);
        f.Run("phases.assign", new JsonObject { ["id"] = old, ["created"] = "existing", ["demolished"] = "new-construction" });
        f.Run("phases.assign", new JsonObject { ["id"] = later, ["created"] = "new-construction" });

        var existing = StepFile.Read(Export(f, "existing.ifc", new IfcExportOptions(Phase: "existing")));
        Assert.True(existing.OfType("IFCWALL").Count() == 1, $"{existing.OfType("IFCWALL").Count()} walls in the existing phase; {_notes}");
        Assert.Equal(old, (string?)One(existing, "IFCWALL")[0]);
        Assert.Contains(SetsOf(existing, One(existing, "IFCWALL")), s => (string?)s[2] == "Pset_Lapin_Phase");

        var all = StepFile.Read(Export(f, "all.ifc"));
        Assert.Equal(2, all.OfType("IFCWALL").Count());
    }
}
