using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// The IFC import (docs/spec-ifc.md, Importul): Lapin's own file comes back
/// as the same model, a second import updates instead of doubling, and a file
/// without Lapin's intent comes in as references that show but do not edit.
/// </summary>
public class IfcImportTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc-import").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture Full() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new CivilModule(), new PhasesModule(), new StudioModule());

    /// <summary>A small house: a zoned wall and plain ones with a door and a window, a floor, a column, a strip footing, two axes.</summary>
    static Fixture House(out string wall)
    {
        var f = Full();
        var plain = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        var zoned = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Zoned 30",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.3, ["core"] = true }),
            ["zones"] = new JsonArray(
                new JsonObject { ["key"] = "masonry", ["name"] = "Masonry", ["role"] = "masonry", ["height"] = "rest", ["material"] = "Brick" },
                new JsonObject { ["key"] = "ring-beam", ["name"] = "Ring beam", ["role"] = "ringBeam", ["height"] = 0.25, ["material"] = "Reinforced concrete" }),
        })!["id"]!;
        wall = f.Wall(0, 0, 6, 0, plain);
        f.Wall(6, 0, 6, 4, zoned);
        f.Wall(6, 4, 0, 4, plain);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4 });
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 4), new JsonArray(0, 4)) });
        var ct = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C 30x30", ["section"] = new JsonObject { ["shape"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = ct, ["x"] = 3, ["y"] = 2 });
        f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 });
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = 0, ["y1"] = -1, ["x2"] = 0, ["y2"] = 5 });
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = -1, ["y1"] = 0, ["x2"] = 7, ["y2"] = 0 });
        return f;
    }

    string Export(Fixture f, string name, IfcExportOptions? options = null)
    {
        var path = Path.Combine(_dir, name);
        IfcExport.Write(f.Session, path, options);
        return path;
    }

    static Dictionary<string, int> Counts(IModelView m) => m.Entities.GroupBy(e => e.Class).ToDictionary(g => g.Key, g => g.Count());
    static Dictionary<string, int> RelationCounts(IModelView m) => m.Relations.GroupBy(r => r.Class).ToDictionary(g => g.Key, g => g.Count());
    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void Lapins_own_file_comes_back_as_the_same_model()
    {
        var a = House(out var wall);
        var path = Export(a, "house.ifc");
        var b = Full();
        var r = IfcImport.Run(b.Session, path, null, out _);
        Assert.True(r.References == 0, $"references: {r.References}; {string.Join("; ", r.Notes)}");

        Assert.Equal(Counts(a.Model), Counts(b.Model));
        Assert.Equal(RelationCounts(a.Model), RelationCounts(b.Model));
        Assert.Equal(Spatial.Storeys(a.Model).Select(s => (s.Name, s.Num("elevation"))), Spatial.Storeys(b.Model).Select(s => (s.Name, s.Num("elevation"))));
        // Every element: the same attributes, the same body.
        foreach (var e in a.Model.Entities.Where(e => a.Session.Catalog.Geometry(e.Class) != null))
        {
            var twin = b.Model.Get(e.Id);
            Assert.NotNull(twin);
            Assert.Equal(e.Class, twin.Class);
            Assert.Equal(e.Attrs.ToJsonString(), twin.Attrs.ToJsonString());
            Assert.Equal(Volume(a, e.Id), Volume(b, e.Id), 6);
        }
        Assert.Equal(a.Session.Quantities(wall).SelectMany(q => q.Quantities.Select(x => (q.Name, x.Name, Math.Round(x.Value, 6)))),
            b.Session.Quantities(wall).SelectMany(q => q.Quantities.Select(x => (q.Name, x.Name, Math.Round(x.Value, 6)))));
        // Undo takes the whole import back.
        b.Session.Undo();
        Assert.Empty(b.Model.OfClass(IfcClass.Wall));
    }

    [Fact]
    public void Lapins_own_sets_carry_no_Pset_prefix_which_IFC_keeps_for_its_own()
    {
        // IfcPropertySet: "Property sets that are not declared as part of the IFC specification shall have a Name value not including the Pset_ prefix."
        var a = House(out var wall);
        a.Run("element.setParam", new JsonObject { ["id"] = wall, ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "EI60" });
        var text = File.ReadAllText(Export(a, "house.ifc"));
        var names = System.Text.RegularExpressions.Regex.Matches(text, @"IFC(?:PROPERTYSET\('[^']*',[^,]*,|MATERIALPROPERTIES\()'([^']*)'").Select(m => m.Groups[1].Value).ToHashSet();
        Assert.Contains("Lapin_Intent", names);
        Assert.Contains("Lapin_Parameters", names);
        Assert.Contains("Pset_WallCommon", names);
        Assert.All(names.Where(n => n.StartsWith("Pset_", StringComparison.Ordinal)), n => Assert.Matches(@"^Pset_(Wall|Slab|Roof|Column|Beam|Footing|Door|Window|Member|Plate|Covering|Railing|Stair|Ramp|Chimney|BuildingElementProxy)Common$", n));
    }

    [Fact]
    public void A_file_written_before_the_sets_lost_their_Pset_prefix_still_comes_back_as_the_same_model()
    {
        var a = House(out _);
        var path = Export(a, "old.ifc");
        File.WriteAllText(path, File.ReadAllText(path).Replace("'Lapin_Intent'", "'Pset_Lapin_Intent'"));
        var b = Full();
        var r = IfcImport.Run(b.Session, path, null, out _);
        Assert.True(r.References == 0, $"references: {r.References}; {string.Join("; ", r.Notes)}");
        Assert.Equal(Counts(a.Model), Counts(b.Model));
    }

    [Fact]
    public void A_second_import_updates_the_elements_instead_of_doubling_them()
    {
        var a = House(out var wall);
        var path = Export(a, "house.ifc");
        var b = Full();
        var first = IfcImport.Run(b.Session, path, null, out _);
        var before = Counts(b.Model);
        // The wall grew in the meantime; the file brings it back.
        b.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["height"] = 3.5 });
        var second = IfcImport.Run(b.Session, path, null, out _);
        Assert.Equal(before, Counts(b.Model));
        Assert.Equal(RelationCounts(a.Model), RelationCounts(b.Model));
        Assert.Equal(first.Elements, second.Updated);
        Assert.Equal(0, second.Elements);
        Assert.Equal(a.Model.Get(wall)!.Num("height"), b.Model.Get(wall)!.Num("height"), 9);
    }

    [Fact]
    public void A_file_without_Lapins_intent_comes_in_as_references()
    {
        var a = House(out var wall);
        var path = Export(a, "house.ifc");
        // The same file as another program would write it: no intent sets.
        var foreign = Path.Combine(_dir, "foreign.ifc");
        File.WriteAllText(foreign, File.ReadAllText(path).Replace(IfcExport.IntentSetName, "Pset_Other"));
        var b = Full();
        var r = IfcImport.Run(b.Session, foreign, null, out _);
        Assert.Equal(0, r.Elements);
        Assert.True(r.References >= 8, $"{r.References} references; {string.Join("; ", r.Notes)}");

        // The plain wall is a group in the file: its layers and lintels come as references of their own; the door comes with its body.
        var door = b.Model.OfClass(IfcClass.Door).Single();
        Assert.True(StoredBodies.Has(door));
        Assert.True(Volume(b, door.Id) > 0);
        var layer = b.Model.Entities.Single(e => e.Class == "IfcBuildingElementPart" && e.Name == "Plaster");
        Assert.True(Volume(b, layer.Id) > 0);
        Assert.Equal(b.Storey, Spatial.StoreyOf(b.Model, layer.Id)!.Id);
        // The zoned wall's masonry is cut by nothing; the floor by nothing: their bodies are what the file holds.
        var slab = b.Model.OfClass(IfcClass.Slab).Single();
        Assert.Equal(Volume(a, slab.Id), Volume(b, slab.Id), 3);
        // References are not edited.
        var ex = Assert.Throws<CommandException>(() => b.Run("element.update", new JsonObject { ["id"] = slab.Id, ["attrs"] = new JsonObject { ["offset"] = 0.1 } }));
        Assert.Contains("reference", ex.Message);
        // They export again, with their class and body.
        var again = StepFile.Read(Export(b, "again.ifc"));
        Assert.Single(again.OfType("IFCSLAB"));
        Assert.Single(again.OfType("IFCDOOR"));
        _ = wall;
    }

    [Fact]
    public void Reference_mode_keeps_even_Lapins_own_file_as_it_is()
    {
        var a = House(out _);
        var path = Export(a, "house.ifc");
        var b = Full();
        var r = IfcImport.Run(b.Session, path, new IfcImportOptions("reference"), out _);
        Assert.Equal(0, r.Elements);
        Assert.True(r.References > 0);
        Assert.All(b.Model.Entities.Where(e => b.Session.Catalog.Geometry(e.Class) != null), e => Assert.True(StoredBodies.Has(e)));
    }
}
