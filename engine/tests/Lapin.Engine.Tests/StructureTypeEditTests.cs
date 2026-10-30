using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>A structure type changed after it was made (report #42: "I typed a dimension wrong and wanted to correct the mistake").</summary>
public class StructureTypeEditTests
{
    static Fixture Both() => new(new ArchitectureModule(), new StructureModule());

    [Fact]
    public void A_column_type_typed_wrong_is_corrected_and_its_columns_follow()
    {
        var f = Both();
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "25x25", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.25, ["depth"] = 0.025 } })!["id"]!;
        var col = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 0, ["y"] = 0 })!["id"]!;
        var (a0, a1) = f.Bounds(col);
        Assert.Equal(0.025, a1.Y - a0.Y, 6);
        f.Run("structure.updateType", new JsonObject { ["id"] = type, ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.25, ["depth"] = 0.25 } });
        var (b0, b1) = f.Bounds(col);
        Assert.Equal(0.25, b1.Y - b0.Y, 6);
        // Its own name stays; the step undoes as one.
        Assert.Equal("25x25", f.Model.Get(type)!.Name);
        f.Session.Undo();
        Assert.Equal(0.025, f.Model.Get(type)!.Attrs["section"]!["depth"]!.GetValue<double>(), 6);
    }

    [Fact]
    public void A_type_named_after_its_size_takes_the_new_size_with_its_elements()
    {
        var f = Both();
        var type = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.25, ["depth"] = 0.4 } })!["id"]!;
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0 })!["id"]!;
        Assert.Equal("Beam 25×40", f.Model.Get(beam)!.Name);
        f.Run("structure.updateType", new JsonObject { ["id"] = type, ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.5 } });
        Assert.Equal("Beam 30×50", f.Model.Get(type)!.Name);
        Assert.Equal("Beam 30×50", f.Model.Get(beam)!.Name);
        f.Run("structure.updateType", new JsonObject { ["id"] = type, ["name"] = "B1" });
        Assert.Equal("B1", f.Model.Get(beam)!.Name);
    }

    [Fact]
    public void Footing_types_change_their_steps_core_and_bottom()
    {
        var f = Both();
        var pad = (string)f.Run("structure.createPadType", new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["length"] = 1.0, ["width"] = 1.0, ["height"] = 0.5 }) })!["id"]!;
        var p = (string)f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = pad, ["x"] = 0, ["y"] = 0 })!["id"]!;
        f.Run("structure.updateType", new JsonObject { ["id"] = pad, ["bottom"] = -2.0, ["steps"] = new JsonArray(new JsonObject { ["length"] = 1.5, ["width"] = 1.2, ["height"] = 0.6 }) });
        var (lo, hi) = f.Bounds(p);
        Assert.Equal(1.5, hi.X - lo.X, 6);
        Assert.Equal(0.6, hi.Z - lo.Z, 6);
        Assert.Equal("PF 150×120", f.Model.Get(pad)!.Name);

        var strip = (string)f.Run("structure.createStripType", new JsonObject { ["layers"] = new JsonArray(new JsonObject { ["material"] = "Reinforced concrete", ["thickness"] = 0.3, ["core"] = true }) })!["id"]!;
        f.Run("structure.updateType", new JsonObject { ["id"] = strip, ["core"] = 0.4, ["footingWidth"] = 0.8 });
        Assert.Equal(0.4, LayerSets.LayersOf(f.Model, strip).Single().Thickness, 6);
        Assert.Equal(0.8, f.Model.Get(strip)!.Num("footingWidth"), 6);
        Assert.Equal("SF 40", f.Model.Get(strip)!.Name);
        Assert.Throws<CommandException>(() => f.Run("structure.updateType", new JsonObject { ["id"] = strip, ["footingWidth"] = -1 }));
    }
}
