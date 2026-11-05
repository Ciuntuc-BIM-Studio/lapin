using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Cost;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage 1 of docs/plan-buildup-tags.md: what a material says of itself (a description, parameters with IFC's standard
/// names, how it is measured and its catalogue of thicknesses), a layer's note in its type, both through IFC and back, and
/// the takeoff and the estimate by the material's measure: a material by area a row per thickness, a thickness not in its
/// catalogue flagged (and checked before an issue).
/// </summary>
public class MaterialDataTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-material-data").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static string Material(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    static JsonObject Wool() => new()
    {
        ["description"] = "Mineral wool boards, Knauf FKD-S\nfixed with dowels",
        ["params"] = new JsonArray(
            new JsonObject { ["pset"] = "Pset_MaterialThermal", ["name"] = "ThermalConductivity", ["value"] = 0.035 },
            new JsonObject { ["pset"] = "Pset_MaterialHygroscopic", ["name"] = "UpperVaporResistanceFactor", ["value"] = 1 },
            new JsonObject { ["name"] = "Reaction to fire", ["kind"] = "text", ["value"] = "A1" },
            new JsonObject { ["pset"] = "Pset_MaterialCommon", ["name"] = "MassDensity", ["value"] = 140 }),
        ["measure"] = new JsonObject { ["by"] = "area", ["thicknesses"] = new JsonArray(50, 100, 150, 200) },
    };

    static string WallType(Fixture f, string name, params (string Material, double Thickness, bool Core, string? Note)[] layers) =>
        (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = name,
            ["layers"] = new JsonArray(layers.Select(l =>
            {
                var o = new JsonObject { ["material"] = l.Material, ["thickness"] = l.Thickness, ["core"] = l.Core };
                if (l.Note != null) o["note"] = l.Note;
                return (JsonNode)o;
            }).ToArray()),
        })!["id"]!;

    [Fact]
    public void A_materials_data_is_set_in_one_step_checked_and_undone()
    {
        var f = new Fixture();
        var id = (string)f.Run("material.create", new JsonObject { ["name"] = "Mineral wool" })!["id"]!;
        var a = Wool(); a["id"] = id;
        f.Run("material.setData", a);
        var mat = f.Model.Get(id)!;
        Assert.Equal("Mineral wool boards, Knauf FKD-S\nfixed with dowels", mat.Str("description"));
        var ps = ((JsonArray)mat.Attrs["params"]!).OfType<JsonObject>().ToList();
        Assert.Equal(4, ps.Count);
        // A standard property is held as its kind, in its set; the user's own has no set.
        Assert.Equal(("number", "Pset_MaterialThermal"), ((string)ps[0]["kind"]!, (string)ps[0]["pset"]!));
        Assert.Null(ps[2]["pset"]);
        Assert.Equal("area", (string)mat.Attrs["measure"]!["by"]!);
        // The list the interface reads has it, the density found from Pset_MaterialCommon.
        var listed = ((JsonArray)f.Session.Query("material.list", [])!).OfType<JsonObject>().Single(x => (string?)x["id"] == id);
        Assert.Equal(140, (double)listed["density"]!);
        Assert.Equal(4, ((JsonArray)listed["params"]!).Count);

        // Refused: a standard set's name it does not have, a set of one's own named Pset_, a measure Lapin does not know, thicknesses on a volume.
        Assert.Throws<CommandException>(() => f.Run("material.setData", new JsonObject { ["id"] = id, ["params"] = new JsonArray(new JsonObject { ["pset"] = "Pset_MaterialThermal", ["name"] = "Lambda", ["value"] = 1 }) }));
        Assert.Throws<CommandException>(() => f.Run("material.setData", new JsonObject { ["id"] = id, ["params"] = new JsonArray(new JsonObject { ["pset"] = "Pset_MyOwn", ["name"] = "X", ["kind"] = "text", ["value"] = "a" }) }));
        Assert.Throws<CommandException>(() => f.Run("material.setData", new JsonObject { ["id"] = id, ["measure"] = new JsonObject { ["by"] = "weight" } }));
        Assert.Throws<CommandException>(() => f.Run("material.setData", new JsonObject { ["id"] = id, ["measure"] = new JsonObject { ["by"] = "volume", ["thicknesses"] = new JsonArray(50) } }));
        Assert.Throws<CommandException>(() => f.Run("material.setData", new JsonObject { ["id"] = id, ["params"] = new JsonArray(new JsonObject { ["name"] = "A", ["kind"] = "text" }, new JsonObject { ["name"] = "a", ["kind"] = "text" }) }));

        // One key changed, the others kept; null takes one off.
        f.Run("material.setData", new JsonObject { ["id"] = id, ["description"] = null });
        Assert.Null(f.Model.Get(id)!.Str("description"));
        Assert.NotNull(f.Model.Get(id)!.Attrs["measure"]);
        f.Session.Undo();
        Assert.Equal("Mineral wool boards, Knauf FKD-S\nfixed with dowels", f.Model.Get(id)!.Str("description"));
        f.Session.Undo();
        Assert.Null(f.Model.Get(id)!.Attrs["params"]);
        Assert.Null(f.Model.Get(id)!.Attrs["measure"]);
        // material.update takes the same fields.
        f.Run("material.update", new JsonObject { ["id"] = id, ["description"] = "Rock wool" });
        Assert.Equal("Rock wool", f.Model.Get(id)!.Str("description"));
    }

    [Fact]
    public void A_materials_fields_are_found_by_name_set_squashed_name_and_symbol()
    {
        var f = new Fixture();
        var id = (string)f.Run("material.create", new JsonObject { ["name"] = "Mineral wool" })!["id"]!;
        var a = Wool(); a["id"] = id;
        f.Run("material.setData", a);
        var m = MaterialFields.Of(f.Model, f.Model.Get(id)!);
        Assert.Equal(("Mineral wool", "Mineral wool boards, Knauf FKD-S\nfixed with dowels", (string?)null), (m.Name, m.Description, m.Category));
        Assert.Equal("area", m.Measure!.By);
        Assert.Equal([50.0, 100, 150, 200], m.Measure.Thicknesses);
        foreach (var field in new[] { "λ", "ThermalConductivity", "Thermal conductivity", "Pset_MaterialThermal.ThermalConductivity", "thermalconductivity" })
            Assert.Equal(0.035, (double)m.Param(field)!.Value!);
        Assert.Equal("1", m.Text("μ"));
        Assert.Equal("A1", m.Text("ReactionToFire"));
        Assert.Equal("A1", m.Text("Lapin_Material.Reaction to fire"));
        Assert.Equal("140", m.Text("Density"));
        Assert.Equal("Pset_MaterialThermal.ThermalConductivity", m.Param("λ")!.Key);
        Assert.Null(m.Param("Compressive strength"));
        Assert.Equal("Mineral wool", m.Text("material"));
        // Without a description a tag writes its name.
        var plain = MaterialFields.Of(new Entity("brick", IfcClass.Material, "Brick", new JsonObject()));
        Assert.Equal("Brick", plain.Text("description"));
    }

    [Fact]
    public void A_layers_note_is_kept_in_its_type_its_files_and_IFC_and_comes_back()
    {
        var f = new Fixture(new ArchitectureModule());
        var type = WallType(f, "ETICS 25", ("Render", 0.01, false, null), ("Mineral wool", 0.15, false, "In a continuous layer,\nBaumit SupraFix"), ("Brick", 0.25, true, null));
        f.Wall(0, 0, 6, 0, type);
        Assert.Equal([null, "In a continuous layer,\nBaumit SupraFix", null], LayerSets.LayersOf(f.Model, type).Select(l => l.Note));
        // Saved again as the dialog sends it, and in the type files.
        Assert.Equal("In a continuous layer,\nBaumit SupraFix", (string?)TypeExchange.Json(f.Model)["types"]![0]!["layers"]![1]!["note"]);
        var csv = TypeExchange.Csv(f.Model);
        Assert.Contains("Note", csv.Split('\n')[0]);
        Assert.Contains("In a continuous layer, Baumit SupraFix", csv);
        Assert.Equal("In a continuous layer, Baumit SupraFix", TypeExchange.Read(csv).Single().Layers[1].Note);
        Assert.Equal("In a continuous layer,\nBaumit SupraFix", TypeExchange.Read(TypeExchange.Json(f.Model).ToJsonString()).Single().Layers[1].Note);

        var ifc = Path.Combine(_dir, "note.ifc");
        IfcExport.Write(f.Session, ifc, null);
        Assert.Contains("'In a continuous layer,\\X2\\000A\\X0\\Baumit SupraFix'", File.ReadAllText(ifc));
        var b = new Fixture(new ArchitectureModule());
        IfcImport.Run(b.Session, ifc, null, out _);
        var back = b.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "ETICS 25");
        Assert.Equal([null, "In a continuous layer,\nBaumit SupraFix", null], LayerSets.LayersOf(b.Model, back.Id).Select(l => l.Note));
    }

    [Fact]
    public void A_materials_properties_go_to_IFCs_standard_sets_and_come_back_from_Lapin_and_from_another_program()
    {
        var f = new Fixture(new ArchitectureModule());
        var type = WallType(f, "Wool 15", ("Mineral wool", 0.15, false, null), ("Brick", 0.25, true, null));
        f.Wall(0, 0, 6, 0, type);
        var a = Wool(); a["id"] = Material(f, "Mineral wool");
        f.Run("material.setData", a);
        var ifc = Path.Combine(_dir, "wool.ifc");
        IfcExport.Write(f.Session, ifc, null);
        var text = File.ReadAllText(ifc);
        Assert.Contains("IFCMATERIAL('Mineral wool','Mineral wool boards, Knauf FKD-S\\X2\\000A\\X0\\fixed with dowels'", text);
        Assert.Contains("IFCMATERIALPROPERTIES('Pset_MaterialThermal'", text);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('ThermalConductivity',$,IFCTHERMALCONDUCTIVITYMEASURE(0.035),$)", text);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('UpperVaporResistanceFactor',$,IFCPOSITIVERATIOMEASURE(1.),$)", text);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('MassDensity',$,IFCMASSDENSITYMEASURE(140.),$)", text);
        Assert.Contains("IFCMATERIALPROPERTIES('Lapin_Material'", text);
        Assert.DoesNotContain("'Pset_Lapin", text);

        static void Same(Entity mat)
        {
            var m = MaterialFields.Of(mat);
            Assert.Equal("Mineral wool boards, Knauf FKD-S\nfixed with dowels", m.Description);
            Assert.Equal(0.035, (double)m.Param("Pset_MaterialThermal.ThermalConductivity")!.Value!);
            Assert.Equal(1, (double)m.Param("Pset_MaterialHygroscopic.UpperVaporResistanceFactor")!.Value!);
            Assert.Equal("A1", m.Text("Reaction to fire"));
            Assert.Null(m.Param("Reaction to fire")!.Pset);
        }
        // From Lapin: the material as it was (its measure too).
        var b = new Fixture(new ArchitectureModule());
        IfcImport.Run(b.Session, ifc, null, out _);
        var back = b.Model.OfClass(IfcClass.Material).Single(x => x.Name == "Mineral wool");
        Same(back);
        Assert.Equal("area", MaterialMeasure.Of(back)!.By);
        // From another program (no Lapin id on the material): its description and its sets read into parameters.
        var foreign = Path.Combine(_dir, "foreign.ifc");
        File.WriteAllText(foreign, text.Replace("'LapinId'", "'OtherId'"));
        var c = new Fixture(new ArchitectureModule());
        IfcImport.Run(c.Session, foreign, null, out _);
        Same(c.Model.OfClass(IfcClass.Material).Single(x => x.Name == "Mineral wool"));
    }

    /// <summary>Two walls 10 m long of mineral wool 150 and 200 mm, one of 130 mm (not in the catalogue), on brick; a concrete slab measured by volume.</summary>
    static (Fixture F, string W150, string W200, string W130) Insulated()
    {
        var f = new Fixture(new ArchitectureModule(), new CostModule());
        var t150 = WallType(f, "Wool 150", ("Mineral wool", 0.15, false, null), ("Brick", 0.25, true, null));
        var t200 = WallType(f, "Wool 200", ("Mineral wool", 0.20, false, null), ("Brick", 0.25, true, null));
        var t130 = WallType(f, "Wool 130", ("Mineral wool", 0.13, false, null), ("Brick", 0.25, true, null));
        var w150 = f.Wall(0, 0, 10, 0, t150);
        var w200 = f.Wall(0, 5, 10, 5, t200);
        var w130 = f.Wall(0, 10, 10, 10, t130);
        var a = Wool(); a["id"] = Material(f, "Mineral wool");
        f.Run("material.setData", a);
        return (f, w150, w200, w130);
    }

    static double LayerArea(Fixture f, string wall, string material) =>
        f.Session.Quantities(wall).Where(s => s.Material == Material(f, material)).Sum(s => s.Quantities.First(q => q.Name == "NetSideArea").Value);

    static ScheduleGrid Takeoff(Fixture f)
    {
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "quantities" })!["id"]!;
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["groupBy"] = new JsonArray("material"), ["classes"] = new JsonArray(IfcClass.Wall) });
        return new QuantityTakeoff().Grid(f.Session.Reader, f.Model.Get(id)!);
    }

    static string Cell(ScheduleGrid g, IReadOnlyList<string> row, string column) => row[g.Columns.ToList().IndexOf(column)];

    [Fact]
    public void The_takeoff_lists_a_material_by_area_a_row_per_thickness_and_flags_one_not_in_its_catalogue()
    {
        var (f, w150, w200, w130) = Insulated();
        var g = Takeoff(f);
        Assert.Equal(["Material", "Thickness (mm)", "Count"], g.Columns.Take(3));
        var wool = g.Rows.Where(r => r[0] == "Mineral wool").ToList();
        Assert.Equal(["130", "150", "200"], wool.Select(r => r[1]));
        string N(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        Assert.Equal(N(LayerArea(f, w150, "Mineral wool")), Cell(g, wool[1], "Net area (m²)"));
        Assert.Equal(N(LayerArea(f, w200, "Mineral wool")), Cell(g, wool[2], "Net area (m²)"));
        Assert.Equal(N(LayerArea(f, w130, "Mineral wool")), Cell(g, wool[0], "Net area (m²)"));
        // By area only: no volume of its own.
        Assert.Equal("", Cell(g, wool[1], "Net volume (m³)"));
        Assert.Equal(QuantityTakeoff.NotCatalogue, Cell(g, wool[0], "Note"));
        Assert.Equal("", Cell(g, wool[1], "Note"));
        // The brick, without a rule, as before: one row, its volume, no thickness.
        var brick = g.Rows.Single(r => r[0] == "Brick");
        Assert.Equal("", brick[1]);
        Assert.Equal("3", Cell(g, brick, "Count"));
        Assert.NotEqual("", Cell(g, brick, "Net volume (m³)"));

        // The same layer in another type of the same thickness joins its row.
        var again = WallType(f, "Wool 150 b", ("Mineral wool", 0.15, false, null), ("Concrete", 0.2, true, null));
        f.Wall(0, 20, 10, 20, again);
        Assert.Equal("2", Cell(Takeoff(f), Takeoff(f).Rows.Single(r => r[0] == "Mineral wool" && r[1] == "150"), "Count"));
    }

    [Fact]
    public void Volume_mass_and_count_rules_give_cubic_metres_kilograms_and_pieces_and_no_rule_changes_nothing()
    {
        var (f, w150, _, _) = Insulated();
        var before = Takeoff(f).Rows.Single(r => r[0] == "Brick");
        var brick = Material(f, "Brick");
        f.Run("material.setData", new JsonObject { ["id"] = brick, ["measure"] = new JsonObject { ["by"] = "volume" } });
        var g = Takeoff(f);
        var row = g.Rows.Single(r => r[0] == "Brick");
        Assert.Equal(Cell(g, row, "Net volume (m³)"), before[Takeoff(f).Columns.ToList().IndexOf("Net volume (m³)")]);
        Assert.Equal("", Cell(g, row, "Net area (m²)"));
        Assert.Equal("", row[1]);
        // By mass: the volume times its density.
        f.Run("material.setData", new JsonObject { ["id"] = brick, ["measure"] = new JsonObject { ["by"] = "mass" },
            ["params"] = new JsonArray(new JsonObject { ["name"] = "Density", ["kind"] = "number", ["value"] = 1800 }) });
        g = Takeoff(f);
        var volume = f.Session.Quantities(w150).Where(s => s.Material == brick).Sum(s => s.Quantities.First(q => q.Name == "NetVolume").Value) * 3;
        Assert.Equal((volume * 1800).ToString("0.0", CultureInfo.InvariantCulture), Cell(g, g.Rows.Single(r => r[0] == "Brick"), "Weight (kg)"));
        // By count: the pieces of its board, the area over the board's, rounded up; without a board, by area with a note.
        var wool = Material(f, "Mineral wool");
        f.Run("material.setData", new JsonObject { ["id"] = wool, ["measure"] = new JsonObject { ["by"] = "count", ["board"] = new JsonArray(1.2, 0.6) } });
        g = Takeoff(f);
        var w = g.Rows.Single(r => r[0] == "Mineral wool" && r[1] == "150");
        Assert.Equal(Math.Ceiling(LayerArea(f, w150, "Mineral wool") / 0.72).ToString(CultureInfo.InvariantCulture), Cell(g, w, "Pieces"));
        f.Run("material.setData", new JsonObject { ["id"] = wool, ["measure"] = new JsonObject { ["by"] = "count" } });
        g = Takeoff(f);
        Assert.Equal("no board size: by area", Cell(g, g.Rows.Single(r => r[0] == "Mineral wool" && r[1] == "150"), "Note"));
        // Without any rule: the takeoff as it always was.
        f.Run("material.setData", new JsonObject { ["id"] = wool, ["measure"] = null });
        f.Run("material.setData", new JsonObject { ["id"] = brick, ["measure"] = null });
        g = Takeoff(f);
        Assert.DoesNotContain("Thickness (mm)", g.Columns);
        Assert.DoesNotContain("Note", g.Columns);
        Assert.Single(g.Rows, r => r[0] == "Mineral wool");
    }

    [Fact]
    public void An_article_bound_to_a_material_takes_its_quantity_by_the_materials_measure_a_line_per_thickness()
    {
        var (f, w150, w200, w130) = Insulated();
        // A price without a unit: by itself the article would count elements.
        f.Run("cost.importPrices", new JsonObject { ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("Cod;Denumire;U.M.;Material;Manoperă\nVAT01;Vată minerală;;10;5\n")), ["name"] = "p.csv" });
        f.Run("cost.setRule", new JsonObject { ["material"] = Material(f, "Mineral wool"), ["code"] = "VAT01" });
        f.Run("cost.createEstimate", new JsonObject());
        var lines = ((JsonObject)f.Session.Query("cost.estimate", new JsonObject())!)["chapters"]!.AsArray().SelectMany(c => c!["lines"]!.AsArray()).OfType<JsonObject>()
            .Where(l => (string?)l["code"] == "VAT01").ToList();
        Assert.Equal(3, lines.Count);
        var l150 = lines.Single(l => ((string)l["name"]!).EndsWith("· 150 mm"));
        Assert.Equal(LayerArea(f, w150, "Mineral wool"), (double)l150["qty"]!, 6);
        Assert.Equal(LayerArea(f, w200, "Mineral wool"), (double)lines.Single(l => ((string)l["name"]!).EndsWith("· 200 mm"))["qty"]!, 6);
        Assert.Equal("area", (string)l150["basis"]!);
        _ = w130;
    }

    [Fact]
    public void A_layer_not_of_its_materials_catalogue_is_a_warning_before_an_issue()
    {
        var (f, _, _, _) = Insulated();
        var found = CdeChecks.Run(f.Session.Reader).Where(x => x.Rule == "catalogueThickness").ToList();
        var one = Assert.Single(found);
        Assert.Equal(CdeFinding.Warn, one.Severity);
        Assert.Equal("Layer of Mineral wool in Wool 130 is 130 mm, not a catalogue thickness.", one.Message);
        Assert.Equal(f.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "Wool 130").Id, one.EntityId);
        Assert.True(MaterialMeasure.InCatalogue(null, 130));
        Assert.True(MaterialMeasure.InCatalogue(MaterialMeasure.Of(f.Model.Get(Material(f, "Mineral wool"))), 150.0000001));
    }
}
