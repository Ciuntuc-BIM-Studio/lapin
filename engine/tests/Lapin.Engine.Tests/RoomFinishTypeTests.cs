using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Issue #143: a room finish's coverings carry their layers into IFC as a material layer set; the floor, walls and ceiling may
/// have types of their own (an option of the finish, off by default); a finish is duplicated as any type is, its name with it.
/// </summary>
public class RoomFinishTypeTests
{
    static (Fixture F, string Room) Room()
    {
        var f = new Fixture();
        foreach (var (a, b) in new[] { ((0.0, 0.0), (5.0, 0.0)), ((5.0, 0.0), (5.0, 4.0)), ((5.0, 4.0), (0.0, 4.0)), ((0.0, 4.0), (0.0, 0.0)) })
            f.Wall(a.Item1, a.Item2, b.Item1, b.Item2);
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        return (f, room);
    }

    static string Library(Fixture f, string name) => (string)f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = name })!["id"]!;

    static List<Entity> Finishes(Fixture f, string room) => f.Model.OfClass(RoomFinishes.Class).Where(e => e.Str("roomId") == room).ToList();

    static StepFile Export(Fixture f)
    {
        var path = Path.Combine(Path.GetTempPath(), $"finish-{Guid.NewGuid():N}.ifc");
        try { IfcExport.Write(f.Session, path); return StepFile.Read(path); }
        finally { File.Delete(path); }
    }

    /// <summary>What IFC associates with an entity: its relating material (a usage, a set, a material).</summary>
    static StepEntity? MaterialOf(StepFile file, StepEntity e) =>
        file.OfType("IFCRELASSOCIATESMATERIAL").Where(r => r[4] is List<object?> objs && objs.Contains(new StepRef(e.Id))).Select(r => file.Get(r[5])).FirstOrDefault();

    static List<(string Material, double Thickness, string? Note)> Layers(StepFile file, StepEntity set) =>
        ((List<object?>)set[0]!).Select(file.Get).Select(l => (IfcModel.Str(file.Get(l![0])![0])!, Convert.ToDouble(l[1]), IfcModel.Str(l[4]))).ToList();

    [Fact]
    public void Each_covering_goes_to_IFC_with_its_parts_layers_as_a_material_layer_set()
    {
        var (f, room) = Room();
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = Library(f, "Bathroom") });
        var file = Export(f);
        var coverings = file.OfType("IFCCOVERING").ToDictionary(c => ((StepEnum)c[8]!).Value);
        Assert.Equal(["CEILING", "CLADDING", "FLOORING"], coverings.Keys.Order());

        var floor = MaterialOf(file, coverings["FLOORING"])!;
        Assert.Equal("IFCMATERIALLAYERSETUSAGE", floor.Type);
        Assert.Equal(new StepEnum("AXIS3"), floor[1]);
        Assert.Equal(new StepEnum("NEGATIVE"), floor[2]);
        var floorSet = file.Get(floor[0])!;
        Assert.Equal("Bathroom · Floor", IfcModel.Str(floorSet[1]));
        Assert.Equal([("Ceramic tiles", 0.01), ("Tile adhesive", 0.005), ("Waterproofing", 0.002), ("Screed", 0.05)], Layers(file, floorSet).Select(l => (l.Material, l.Thickness)));

        // The walls' laid across, from the wall's face; the paint's tiles up to 2.10 m said in its layer's description.
        var walls = MaterialOf(file, coverings["CLADDING"])!;
        Assert.Equal(new StepEnum("AXIS2"), walls[1]);
        var wallLayers = Layers(file, file.Get(walls[0])!);
        Assert.Equal(["Plaster", "Paint"], wallLayers.Select(l => l.Material));
        Assert.Contains("Ceramic tiles to 2.1 m", wallLayers[1].Note);

        var ceiling = Layers(file, file.Get(MaterialOf(file, coverings["CEILING"])![0])!);
        Assert.Equal(["Plaster", "Paint"], ceiling.Select(l => l.Material));
        // The finish's one type, shared by its three parts, carries no layer set of its own.
        var type = Assert.Single(file.OfType("IFCCOVERINGTYPE"));
        Assert.Null(MaterialOf(file, type));
    }

    [Fact]
    public void Parts_typed_apart_each_have_a_type_of_their_own_with_its_layers_and_the_option_off_shares_one_again()
    {
        var (f, room) = Room();
        var bath = Library(f, "Bathroom");
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = bath });
        var before = f.Session.Geometry(Finishes(f, room).Single(e => e.Str("part") == "walls").Id)!.Parts.Sum(p => Lapin.Geometry.Measure.Volume(p.Solid));
        // Off by default: no part types, every covering of the finish's type.
        Assert.Empty(f.Model.OfClass(RoomFinishes.PartTypeClass));
        Assert.All(Finishes(f, room), e => Assert.Equal(bath, f.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType)!.Id));

        var spec = RoomFinishes.SpecOf(f.Model, f.Model.Get(bath)!)! with { PartTypes = true };
        f.Run("architecture.updateRoomFinishType", new JsonObject { ["id"] = bath, ["spec"] = spec.ToJson() });
        var parts = RoomFinishes.PartTypesOf(f.Model, bath).ToDictionary(t => t.Str("part")!);
        Assert.Equal(["ceiling", "floor", "walls"], parts.Keys.Order());
        Assert.Equal("Bathroom · Floor", parts["floor"].Name);
        Assert.Equal("FLOORING", parts["floor"].PredefinedType);
        foreach (var e in Finishes(f, room)) Assert.Equal(parts[e.Str("part")!].Id, f.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType)!.Id);
        // The geometry is the finish's as before, its spec read through the part's type.
        Assert.Equal(before, f.Session.Geometry(Finishes(f, room).Single(e => e.Str("part") == "walls").Id)!.Parts.Sum(p => Lapin.Geometry.Measure.Volume(p.Solid)), 9);

        var file = Export(f);
        var types = file.OfType("IFCCOVERINGTYPE").ToDictionary(t => IfcModel.Str(t[2])!);
        Assert.Contains("Bathroom · Walls", types.Keys);
        var wallsType = MaterialOf(file, types["Bathroom · Walls"])!;
        Assert.Equal("IFCMATERIALLAYERSET", wallsType.Type);
        Assert.Equal(["Plaster", "Paint"], Layers(file, wallsType).Select(l => l.Material));
        Assert.Equal(new StepEnum("CLADDING"), types["Bathroom · Walls"][9]);

        // Renamed, its part types follow (their ids kept); off again, the parts share the finish's type, the part types kept.
        var ids = parts.Values.Select(t => t.Id).Order().ToList();
        f.Run("architecture.updateRoomFinishType", new JsonObject { ["id"] = bath, ["spec"] = (spec with { Name = "Wet room" }).ToJson() });
        Assert.Equal("Wet room · Ceiling", f.Model.Get(parts["ceiling"].Id)!.Name);
        f.Run("architecture.updateRoomFinishType", new JsonObject { ["id"] = bath, ["spec"] = (spec with { Name = "Wet room", PartTypes = null }).ToJson() });
        Assert.All(Finishes(f, room), e => Assert.Equal(bath, f.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType)!.Id));
        Assert.Equal(ids, RoomFinishes.PartTypesOf(f.Model, bath).Select(t => t.Id).Order());
        // Saved as it was: the option off is not written.
        Assert.Null(f.Model.Get(bath)!.Attrs["spec"]!["partTypes"]);
    }

    [Fact]
    public void A_finish_is_duplicated_with_its_name_and_saved_to_a_type_pack()
    {
        var (f, _) = Room();
        var bath = Library(f, "Bathroom");
        var copy = (string)f.Run("types.duplicate", new JsonObject { ["id"] = bath })!["id"]!;
        var made = f.Model.Get(copy)!;
        Assert.Equal("Bathroom 2", made.Name);
        Assert.Equal("Bathroom 2", RoomFinishes.SpecOf(f.Model, made)!.Name);
        Assert.Equal("Bathroom", RoomFinishes.SpecOf(f.Model, f.Model.Get(bath)!)!.Name);
        var pack = f.Session.Query("types.export", new JsonObject { ["ids"] = new JsonArray(copy), ["name"] = "Finishes" })!;
        Assert.Contains("Bathroom 2", (string)pack["text"]!);
    }
}
