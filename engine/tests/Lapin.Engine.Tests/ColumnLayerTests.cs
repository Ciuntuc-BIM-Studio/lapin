using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Decision 719 (forum topic 169, issue #108): a column type's finish layers round its core (plaster, fireproofing boards,
/// insulation), outward, all round or on some faces: parts keyed layer:1… in their materials, cut in plans and sections with
/// their materials' hatches and ruled by Visibility / Graphics' subcategories and the layers modes as a wall's layers, measured
/// by layer, written to IFC as a constituent set; a column without them as before.
/// </summary>
public class ColumnLayerTests
{
    static Fixture Make() => new(new ArchitectureModule(), new StructureModule(), new Lapin.Drafting.DraftingModule());

    static JsonObject Layer(string material, double t, params string[] faces)
    {
        var o = new JsonObject { ["material"] = material, ["thickness"] = t };
        if (faces.Length > 0) o["faces"] = new JsonArray([.. faces.Select(x => (JsonNode)x)]);
        return o;
    }

    /// <summary>A 300 × 300 column 3 m tall, its type with the layers given.</summary>
    static (Fixture F, string Type, string Column) Column(params JsonObject[] layers)
    {
        var f = Make();
        var args = new JsonObject { ["name"] = "C30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 }, ["height"] = 3 };
        if (layers.Length > 0) args[ColumnLayers.Key] = new JsonArray([.. layers]);
        var type = (string)f.Run("structure.createColumnType", args)!["id"]!;
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 2, ["y"] = 2 })!["id"]!;
        return (f, type, column);
    }

    static double Vol(Fixture f, string id, string key) => f.Session.Geometry(id)!.Parts.Where(p => p.Key == key).Sum(p => Measure.Volume(p.Solid));
    static double Q(Fixture f, string id, string set, string name, string? part = null) =>
        f.Session.Quantities(id).Single(s => s.Name == set && s.Part == part).Quantities.Single(q => q.Name == name).Value;

    [Fact]
    public void A_column_without_layers_builds_as_before()
    {
        var (f, _, column) = Column();
        var g = f.Session.Geometry(column)!;
        Assert.Equal("body", Assert.Single(g.Parts).Key);
        Assert.Equal(0.27, Q(f, column, "Qto_ColumnBaseQuantities", "NetVolume"), 6);
        Assert.DoesNotContain(f.Session.Quantities(column), s => s.Name == "Lapin_ColumnLayerQuantities");
        Assert.Null(LayerSets.DrawnBandsOf(f.Model, column));
    }

    [Fact]
    public void Plaster_all_round_and_a_fire_board_on_one_face_are_parts_of_their_own_measured_by_layer()
    {
        var (f, _, column) = Column(Layer("Plaster", 0.015), Layer("Fire board", 0.025, "+x"));
        var g = f.Session.Geometry(column)!;
        Assert.Equal(["body", "layer:1", "layer:2"], g.Parts.Select(p => p.Key!).Distinct().ToArray());
        Assert.Equal(0.27, Vol(f, column, "body"), 6);
        // 330 × 330 less the core, 3 m; the board 25 mm on the +x face, 330 mm wide (flush with the plaster of the faces beside).
        Assert.Equal((0.33 * 0.33 - 0.09) * 3, Vol(f, column, "layer:1"), 6);
        Assert.Equal(0.025 * 0.33 * 3, Vol(f, column, "layer:2"), 6);
        // The core's quantities alone, the layers' apart.
        Assert.Equal(0.27, Q(f, column, "Qto_ColumnBaseQuantities", "NetVolume"), 6);
        Assert.Equal(0.015, Q(f, column, "Lapin_ColumnLayerQuantities", "Width", "layer:1"), 9);
        Assert.Equal((0.33 * 0.33 - 0.09) * 3, Q(f, column, "Lapin_ColumnLayerQuantities", "NetVolume", "layer:1"), 6);
        Assert.Equal(0.33 * 3, Q(f, column, "Lapin_ColumnLayerQuantities", "NetSideArea", "layer:2"), 6);
        // Its envelope (what cuts a wall round it) is the outermost outline.
        var env = Columns.Envelope(f.Model.Get(column)!, f.Model)!;
        Assert.Equal(0.355 * 0.33 * 3, Measure.Volume(env), 6);
    }

    [Fact]
    public void Turned_columns_carry_their_layers_on_their_own_faces()
    {
        var (f, type, _) = Column(Layer("Fire board", 0.02, "+x"));
        var turned = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 6, ["y"] = 2, ["rotation"] = 90 })!["id"]!;
        var (min, max) = Measure.Bounds(f.Session.Geometry(turned)!.Parts.Single(p => p.Key == "layer:1").Solid)!.Value;
        // Its +x face turned to +y: the board 20 mm over y = 2.15.
        Assert.Equal((2.15, 2.17), (Math.Round(min.Y, 6), Math.Round(max.Y, 6)));
        Assert.Equal((5.85, 6.15), (Math.Round(min.X, 6), Math.Round(max.X, 6)));
    }

    [Fact]
    public void Plans_cut_the_layers_in_their_materials_and_Visibility_Graphics_hides_the_finishes()
    {
        var (f, _, column) = Column(Layer("Plaster", 0.015), Layer("Mineral wool", 0.05, "+x", "-x"));
        // A new project's plans leave the finishes out (decision 500's default): as the column's plaster, like a wall's.
        var plan0 = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        Assert.DoesNotContain(Views.Render(f.Session.Reader, Views.Find(f.Model, plan0)!).Generated!.Cuts, c => c.Element == column && c.Key == "layer:1");
        f.EveryLayerInPlans();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        var cuts = Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!).Generated!.Cuts.Where(c => c.Element == column).ToList();
        Assert.Contains(cuts, c => c.Key == "layer:1" && f.Model.Get(c.Material!)!.Name == "Plaster");
        Assert.Contains(cuts, c => c.Key == "layer:2");
        var vg = VisibilityGraphics.Of(f.Model, Views.Find(f.Model, plan)!);
        var e = f.Model.Get(column)!;
        Assert.Equal(("finishes", (string?)null), (vg.SubOf(e, "layer:1"), vg.SubOf(e, "body")));
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "columns", ["sub"] = "finishes", ["value"] = new JsonObject { ["hidden"] = true } });
        vg = VisibilityGraphics.Of(f.Model, Views.Find(f.Model, plan)!);
        Assert.True(vg.HidesPart(e, "layer:1"));
        Assert.False(vg.HidesPart(e, "body"));
        cuts = Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!).Generated!.Cuts.Where(c => c.Element == column).ToList();
        Assert.DoesNotContain(cuts, c => c.Key == "layer:1");
        Assert.Contains(cuts, c => c.Key == "body");
        // The core-only layers mode (decision 500) keeps the core alone, in 3D too.
        var part = f.Session.Geometry(column)!.Parts.First(p => p.Key == "layer:1");
        Assert.Contains("core", ViewStyles.HiddenBy(f.Model, column, part, []) ?? []);
        Assert.Null(ViewStyles.HiddenBy(f.Model, column, f.Session.Geometry(column)!.Parts.First(p => p.Key == "body"), []));
    }

    [Fact]
    public void A_type_s_layers_are_changed_checked_and_taken_away()
    {
        var (f, type, column) = Column(Layer("Plaster", 0.015));
        f.Run("structure.updateType", new JsonObject { ["id"] = type, [ColumnLayers.Key] = new JsonArray(Layer("Plaster", 0.02), Layer("Board", 0.0125, "+y", "-y")) });
        Assert.Equal((0.34 * 0.34 - 0.09) * 3, Vol(f, column, "layer:1"), 6);
        Assert.True(Vol(f, column, "layer:2") > 0);
        Assert.Throws<CommandException>(() => f.Run("structure.updateType", new JsonObject { ["id"] = type, [ColumnLayers.Key] = new JsonArray(Layer("Plaster", 0)) }));
        Assert.Throws<CommandException>(() => f.Run("structure.updateType", new JsonObject { ["id"] = type, [ColumnLayers.Key] = new JsonArray(Layer("Plaster", 0.01, "up")) }));
        Assert.Throws<CommandException>(() => f.Run("structure.updateType", new JsonObject { ["id"] = type, [ColumnLayers.Key] = new JsonArray(new JsonObject { ["thickness"] = 0.01 }) }));
        f.Run("structure.updateType", new JsonObject { ["id"] = type, [ColumnLayers.Key] = null });
        Assert.Equal("body", Assert.Single(f.Session.Geometry(column)!.Parts).Key);
        Assert.Null(f.Model.Get(type)!.Attrs[ColumnLayers.Key]);
    }

    [Fact]
    public void IFC_carries_the_core_and_the_layers_as_a_constituent_set()
    {
        var (f, _, column) = Column(Layer("Plaster", 0.015), Layer("Fire board", 0.025, "+x"));
        var dir = Directory.CreateTempSubdirectory("lapin-column-layers").FullName;
        try
        {
            var path = Path.Combine(dir, "c.ifc");
            IfcExport.Write(f.Session, path);
            var text = File.ReadAllText(path);
            Assert.Contains("IFCMATERIALCONSTITUENTSET('C30'", text);
            Assert.Contains("IFCMATERIALCONSTITUENT('Core',$,", text);
            Assert.Contains("IFCMATERIALCONSTITUENT('Plaster',$,", text);
            Assert.Contains(",'Finish');", text);
            Assert.Contains("IFCMATERIALCONSTITUENT('Fire board',$,", text);
        }
        finally { Directory.Delete(dir, true); }
    }
}
