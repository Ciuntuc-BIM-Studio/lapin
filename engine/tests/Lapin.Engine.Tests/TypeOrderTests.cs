using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 71: any type duplicated (a deep copy with a new GlobalId, „Name (2)”), and a class's types in an order
/// of the user's, moved up and down, each one undo step.
/// </summary>
public class TypeOrderTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule());

    static string WallType(Fixture f, string name, double core) => (string)f.Run("architecture.createWallType", new JsonObject
    {
        ["name"] = name,
        ["layers"] = new JsonArray(new JsonObject { ["material"] = "Render", ["thickness"] = 0.02 }, new JsonObject { ["material"] = "Brick", ["thickness"] = core, ["core"] = true }),
    })!["id"]!;

    static List<string> Names(Fixture f, string cls) => TypeOrder.Ordered(f.Model, cls).Select(e => e.Name).ToList();

    [Fact]
    public void A_wall_type_duplicated_is_a_copy_of_its_own_with_its_own_layers_listed_after_it()
    {
        var f = F();
        var a = WallType(f, "Brick 25", 0.25);
        WallType(f, "Brick 30", 0.30);
        var copy = (string)f.Run("types.duplicate", new JsonObject { ["id"] = a })!["id"]!;
        Assert.NotEqual(a, copy);
        var c = f.Model.Get(copy)!;
        Assert.Equal("Brick 25 (2)", c.Name);
        Assert.Equal(IfcClass.WallType, c.Class);
        Assert.Equal(["Brick 25", "Brick 25 (2)", "Brick 30"], Names(f, IfcClass.WallType));
        // Its own layer set, the same layers.
        var (setA, setC) = (LayerSets.Of(f.Model, a)!, LayerSets.Of(f.Model, copy)!);
        Assert.NotEqual(setA.Id, setC.Id);
        Assert.Equal(LayerSets.LayersOf(f.Model, a).Select(l => (l.Material, l.Thickness)), LayerSets.LayersOf(f.Model, copy).Select(l => (l.Material, l.Thickness)));
        // Edited, the copy changes alone.
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = copy, ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.375, ["core"] = true }) });
        Assert.Equal(2, LayerSets.LayersOf(f.Model, a).Count);
        Assert.Single(LayerSets.LayersOf(f.Model, copy));
        // Again: the next free number, from the copy too.
        Assert.Equal("Brick 25 (3)", f.Model.Get((string)f.Run("types.duplicate", new JsonObject { ["id"] = a })!["id"]!)!.Name);
        Assert.Equal("Brick 25 (4)", f.Model.Get((string)f.Run("types.duplicate", new JsonObject { ["id"] = copy })!["id"]!)!.Name);
        // One undo step takes a duplicate back, its layer set with it.
        var sets = f.Model.OfClass(IfcClass.MaterialLayerSet).Count();
        f.Session.Undo();
        Assert.Equal(sets - 1, f.Model.OfClass(IfcClass.MaterialLayerSet).Count());
        Assert.DoesNotContain("Brick 25 (4)", Names(f, IfcClass.WallType));
        Assert.Throws<CommandException>(() => f.Run("types.duplicate", new JsonObject { ["id"] = setA.Id }));
    }

    [Fact]
    public void Types_move_up_and_down_their_list_and_new_ones_come_last()
    {
        var f = F();
        var a = WallType(f, "A", 0.2);
        var b = WallType(f, "B", 0.25);
        var c = WallType(f, "C", 0.3);
        Assert.Equal(["A", "B", "C"], Names(f, IfcClass.WallType));   // never ordered: as made
        Assert.Equal(0, (int)f.Run("types.move", new JsonObject { ["id"] = c, ["by"] = -2 })!["index"]!);
        Assert.Equal(["C", "A", "B"], Names(f, IfcClass.WallType));
        f.Run("types.move", new JsonObject { ["id"] = a, ["to"] = "down" });
        Assert.Equal(["C", "B", "A"], Names(f, IfcClass.WallType));
        // At an end it stays.
        Assert.Equal(2, (int)f.Run("types.move", new JsonObject { ["id"] = a, ["by"] = 1 })!["index"]!);
        // A new type comes after those ordered; a deleted one leaves the list.
        WallType(f, "D", 0.35);
        Assert.Equal(["C", "B", "A", "D"], Names(f, IfcClass.WallType));
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(b) });
        Assert.Equal(["C", "A", "D"], Names(f, IfcClass.WallType));
        // A whole order at once; one undo step each.
        f.Run("types.order", new JsonObject { ["class"] = IfcClass.WallType, ["ids"] = new JsonArray(a) });
        Assert.Equal(["A", "C", "D"], Names(f, IfcClass.WallType));
        f.Session.Undo();
        Assert.Equal(["C", "A", "D"], Names(f, IfcClass.WallType));
        // Another class keeps its own list, and the query reads it.
        var col = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        var dup = (string)f.Run("types.duplicate", new JsonObject { ["id"] = col })!["id"]!;
        Assert.Equal("C30 (2)", f.Model.Get(dup)!.Name);
        f.Run("types.move", new JsonObject { ["id"] = dup, ["by"] = -1 });
        var listed = (JsonArray)f.Session.Query("types.ordered", new JsonObject { ["class"] = "IfcColumnType" })!;
        Assert.Equal(["C30 (2)", "C30"], listed.Select(x => (string)x!["name"]!));
        Assert.Equal(["C", "A", "D"], Names(f, IfcClass.WallType));
    }
}
