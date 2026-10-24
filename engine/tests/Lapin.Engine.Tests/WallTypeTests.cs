using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Wall types in the model. Ported from the old Lapin's catalog-type.test.ts:
/// a type is written with its layer set; a changed type renames, rewrites its
/// layers, and every usage follows, leaving nothing behind.
/// </summary>
public class WallTypeTests
{
    static JsonArray Brick(double outer) => new(
        new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.02 },
        new JsonObject { ["material"] = "Brick", ["thickness"] = outer },
        new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.02 });

    [Fact]
    public void A_type_is_written_with_its_layer_set_and_its_materials_once()
    {
        var f = new Fixture();
        var id = (string)f.Run("architecture.createWallType", new JsonObject { ["name"] = "Brick 30", ["mark"] = "W1", ["layers"] = Brick(0.25) })!["id"]!;
        var type = f.Model.Get(id)!;
        Assert.Equal(IfcClass.WallType, type.Class);
        Assert.Equal("Brick 30", type.Name);
        Assert.Equal("STANDARD", type.PredefinedType);
        Assert.Equal("W1", type.Str("mark"));
        var set = WallTypes.LayerSetOf(f.Model, id)!;
        Assert.Equal([0.02, 0.25, 0.02], Layers.Read(set).Select(l => l.Thickness));
        // Plaster twice, one material.
        Assert.Equal(2, f.Model.OfClass(IfcClass.Material).Count());
        Assert.Equal(Layers.Read(set)[0].Material, Layers.Read(set)[2].Material);
        // A second type reuses the project's materials.
        f.Run("architecture.createWallType", new JsonObject { ["name"] = "Brick 35", ["layers"] = Brick(0.3) });
        Assert.Equal(2, f.Model.OfClass(IfcClass.Material).Count());
    }

    [Fact]
    public void A_changed_type_renames_rewrites_its_layers_and_its_walls_follow_leaving_nothing_behind()
    {
        var f = new Fixture();
        var id = (string)f.Run("architecture.createWallType", new JsonObject { ["name"] = "Brick 30", ["layers"] = Brick(0.25) })!["id"]!;
        var wall = f.Wall(0, 0, 4, 0, id);
        var usage = Walls.UsageOf(f.Model, wall)!.Id;
        var set = WallTypes.LayerSetOf(f.Model, id)!.Id;
        Assert.Equal(-0.145, f.Usage(wall).Offset, 9);

        var count = ((ModelStore)f.Model).EntityCount;
        var relations = ((ModelStore)f.Model).RelationCount;
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = id, ["name"] = "Brick 35", ["layers"] = Brick(0.3) });

        Assert.Equal("Brick 35", f.Model.Get(id)!.Name);
        Assert.Equal(set, WallTypes.LayerSetOf(f.Model, id)!.Id);
        Assert.Equal([0.02, 0.3, 0.02], WallTypes.LayersOf(f.Model, id).Select(l => l.Thickness));
        // The wall's own usage follows: the same usage, its axis on the new core.
        Assert.Equal(usage, Walls.UsageOf(f.Model, wall)!.Id);
        Assert.Equal(-0.17, f.Usage(wall).Offset, 9);
        var (min, max) = f.Bounds(wall);
        Assert.Equal(0.34, max.Y - min.Y, 9);
        // Nothing was left behind: no old set, layer or material.
        Assert.Equal(count, ((ModelStore)f.Model).EntityCount);
        Assert.Equal(relations, ((ModelStore)f.Model).RelationCount);
    }

    [Fact]
    public void Deleting_a_wall_takes_its_usage_but_not_its_type()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 4, 0);
        var type = Walls.TypeOf(f.Model, wall)!.Id;
        var usage = Walls.UsageOf(f.Model, wall)!.Id;
        f.Run("element.delete", new JsonObject { ["id"] = wall });
        Assert.Null(f.Model.Get(wall));
        Assert.Null(f.Model.Get(usage));
        Assert.NotNull(f.Model.Get(type));
        Assert.Empty(f.Model.RelationsOf(wall));
    }

    [Fact]
    public void A_type_in_use_cannot_be_deleted_an_unused_one_can()
    {
        var f = new Fixture();
        var used = f.Type("Used", ("Brick", 0.25, true));
        var spare = f.Type("Spare", ("Brick", 0.3, true));
        f.Wall(0, 0, 5, 0, used);
        var ex = Assert.Throws<CommandException>(() => f.Run("element.delete", new JsonObject { ["id"] = used }));
        Assert.Contains("1 element(s)", ex.Message);
        f.Run("element.delete", new JsonObject { ["id"] = spare });
        Assert.Null(f.Model.Get(spare));
    }
}
