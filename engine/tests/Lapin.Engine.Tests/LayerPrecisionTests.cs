using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Report #94: a layer typed as 12.5 mm (a gypsum board) is kept as 0.0125 m
/// through the type's update, the wall's thickness, the type's default name and
/// the IFC export's LayerThickness; nothing rounds it to a whole millimetre.
/// </summary>
public class LayerPrecisionTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-layer").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static JsonArray Drywall(double board) => new(
        new JsonObject { ["material"] = "Gypsum board", ["thickness"] = board },
        new JsonObject { ["material"] = "Brick", ["thickness"] = 0.3, ["core"] = true },
        new JsonObject { ["material"] = "Gypsum board", ["thickness"] = board });

    [Fact]
    public void A_layer_of_12_5_mm_round_trips_through_the_type_the_wall_and_the_IFC_export()
    {
        var f = new Fixture(new ArchitectureModule());
        var id = (string)f.Run("architecture.createWallType", new JsonObject { ["name"] = "Drywall", ["layers"] = Drywall(0.012) })!["id"]!;
        var wall = f.Wall(0, 0, 4, 0, id);
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = id, ["name"] = "Drywall", ["layers"] = Drywall(0.0125) });

        Assert.Equal([0.0125, 0.3, 0.0125], WallTypes.LayersOf(f.Model, id).Select(l => l.Thickness));
        var (min, max) = f.Bounds(wall);
        Assert.Equal(0.325, max.Y - min.Y, 9);

        var path = Path.Combine(_dir, "drywall.ifc");
        IfcExport.Write(f.Session, path);
        var step = StepFile.Read(path);
        var thicknesses = step.OfType("IFCMATERIALLAYER").Select(l => Convert.ToDouble(l[1])).ToList();
        Assert.Equal(2, thicknesses.Count(t => Math.Abs(t - 0.0125) < 1e-12));
        Assert.Contains("0.0125", File.ReadAllText(path));
    }

    [Fact]
    public void A_types_default_name_keeps_a_tenth_of_a_millimetre_and_whole_ones_stay_whole()
    {
        Assert.Equal("Brick wall 325", WallTypes.DefaultName([.. Drywall(0.0125).Select(n => new LayerSpec((string)n!["material"]!, (double)n["thickness"]!, n["core"] is not null))]));
        Assert.Equal("12.5", LayerSets.Millimetres(0.0125));
        Assert.Equal("337.5", LayerSets.Millimetres(0.3375));
        Assert.Equal("300", LayerSets.Millimetres(0.3));
    }
}
