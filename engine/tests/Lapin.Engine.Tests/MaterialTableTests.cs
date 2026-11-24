using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Material data from a table (forum topic #251): a row finds its material by name (case and spaces aside) or by its id, every
/// other column is a parameter (<c>Set.Name</c> a property set, a bare heading the material's own, the Data tab's presets by their
/// names and symbols, units in the heading or the cell), rows with no such material skipped or made; one undo step; the export
/// reads back unchanged.
/// </summary>
public sealed class MaterialTableTests
{
    static JsonObject Table(string csv, string? ifMissing = null) =>
        new() { ["target"] = MaterialTable.Id, ["text"] = csv, ["defaults"] = ifMissing == null ? null : new JsonObject { ["ifMissing"] = ifMissing } };

    static Entity Mat(Fixture f, string name) => Materials.Find(f.Model, name)!;

    static JsonObject? P(Entity mat, string? set, string name) =>
        (mat.Attrs["params"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(p => Params.Same(p, set, name));

    static double Num(Entity mat, string? set, string name) => Json.Number(P(mat, set, name)!["value"])!.Value;

    static Fixture WithMaterials()
    {
        var f = new Fixture();
        f.Run("material.create", new JsonObject { ["name"] = "EPS 100 F + Ejotherm STR-U 2G" });
        f.Run("material.create", new JsonObject { ["name"] = "Concrete C25/30" });
        f.Run("material.create", new JsonObject { ["name"] = "Steel S355" });
        // A parameter the import leaves as it is.
        f.Run("material.setData", new JsonObject
        {
            ["id"] = Mat(f, "Steel S355").Id,
            ["params"] = new JsonArray(new JsonObject { ["name"] = "Supplier", ["kind"] = "text", ["value"] = "ArcelorMittal" }),
        });
        return f;
    }

    [Fact]
    public void Rows_find_their_materials_by_name_case_and_spaces_aside_and_write_their_property_sets_in_one_undo_step()
    {
        var f = WithMaterials();
        var csv = """
            Material;Pset_MaterialThermal.ThermalConductivity;Pset_MaterialCommon.MassDensity;pset_materialhygroscopic.uppervaporresistancefactor;Fire class
            eps 100 f +  ejotherm str-u 2g;0,035;20;60;E
            STEEL S355;;7850;;A1
            """;
        var preview = f.Session.Query("tables.preview", Table(csv))!;
        Assert.True(0 == (int)preview["errors"]!, preview.ToJsonString());
        Assert.Equal(2, (int)preview["tally"]!["matched"]!);

        var before = f.Session.Model.Get(Mat(f, "Steel S355").Id)!;
        f.Run("tables.import", Table(csv));
        var eps = Mat(f, "EPS 100 F + Ejotherm STR-U 2G");
        Assert.Equal(0.035, Num(eps, "Pset_MaterialThermal", "ThermalConductivity"), 9);
        Assert.Equal(20, Num(eps, "Pset_MaterialCommon", "MassDensity"), 9);
        // The set and property named as IFC writes them, whatever the heading's case.
        Assert.Equal(60, Num(eps, "Pset_MaterialHygroscopic", "UpperVaporResistanceFactor"), 9);
        Assert.Equal("UpperVaporResistanceFactor", (string)P(eps, "Pset_MaterialHygroscopic", "UpperVaporResistanceFactor")!["name"]!);
        Assert.Equal("E", (string)P(eps, null, "Fire class")!["value"]!);
        var steel = Mat(f, "Steel S355");
        Assert.Equal(7850, Num(steel, "Pset_MaterialCommon", "MassDensity"), 9);
        // An empty cell leaves the material as it was; its other parameters stay.
        Assert.Null(P(steel, "Pset_MaterialThermal", "ThermalConductivity"));
        Assert.Equal("ArcelorMittal", (string)P(steel, null, "Supplier")!["value"]!);

        // One undo step takes the whole table back.
        f.Session.Undo();
        Assert.Null(P(Mat(f, "EPS 100 F + Ejotherm STR-U 2G"), "Pset_MaterialThermal", "ThermalConductivity"));
        Assert.Equal(before.Attrs.ToJsonString(), Mat(f, "Steel S355").Attrs.ToJsonString());
    }

    [Fact]
    public void Units_in_the_heading_or_the_cell_are_read_as_the_Data_tab_holds_its_values()
    {
        var f = WithMaterials();
        var csv = """
            Name;E (GPa);fy (MPa);λ (mW/(m·K));Density (t/m³);Pset_MaterialMechanical.YoungModulus (MPa);Compressive strength (N/mm²)
            Concrete C25/30;33;;;2,5;;25
            Steel S355;210000 MPa;355;;7,85;210000;
            EPS 100 F + Ejotherm STR-U 2G;;;35;;;
            """;
        var preview = f.Session.Query("tables.preview", Table(csv))!;
        Assert.True(0 == (int)preview["errors"]!, preview.ToJsonString());
        // Each column says where it goes.
        var columns = preview["columns"]!.AsArray().OfType<JsonObject>().ToDictionary(c => (string)c["heading"]!, c => (string)c["target"]!);
        Assert.Contains("Young's modulus", columns["E (GPa)"]);
        Assert.Contains("MPa", columns["E (GPa)"]);
        Assert.Contains("Pset_MaterialThermal.ThermalConductivity", columns["λ (mW/(m·K))"]);

        f.Run("tables.import", Table(csv));
        var concrete = Mat(f, "Concrete C25/30");
        // E in the Data tab's unit (MPa), the density in IFC's (kg/m³).
        Assert.Equal(33000, Num(concrete, null, "Young's modulus"), 6);
        Assert.Equal(2500, Num(concrete, "Pset_MaterialCommon", "MassDensity"), 6);
        Assert.Equal(25, Num(concrete, null, "Compressive strength"), 6);
        var steel = Mat(f, "Steel S355");
        // A unit written in the cell counts over the heading's.
        Assert.Equal(210000, Num(steel, null, "Young's modulus"), 6);
        Assert.Equal(355, Num(steel, null, "Yield strength"), 6);
        // A standard set's value in SI, as IFC holds it.
        Assert.Equal(210e9, Num(steel, "Pset_MaterialMechanical", "YoungModulus"), 0);
        Assert.Equal(0.035, Num(Mat(f, "EPS 100 F + Ejotherm STR-U 2G"), "Pset_MaterialThermal", "ThermalConductivity"), 9);
        // The structural analysis reads them back.
        var sm = Lapin.Platform.Analysis.StructuralMaterials.Of(f.Model, concrete);
        Assert.Equal(33e9, sm.E!.Value, 0);
    }

    [Fact]
    public void Rows_with_no_such_material_are_skipped_or_made_as_chosen()
    {
        var f = WithMaterials();
        var csv = "Material;Pset_MaterialCommon.MassDensity;Description;Category\nRockwool Frontrock;140;Mineral wool board;Insulation\nSteel S355;7850;;\n";
        var skipped = f.Session.Query("tables.preview", Table(csv))!;
        Assert.Equal(1, (int)skipped["tally"]!["skipped"]!);
        Assert.Equal(1, (int)skipped["tally"]!["matched"]!);
        Assert.Contains("skipped", string.Join(" ", skipped["rows"]![0]!["steps"]!.AsArray().Select(s => (string)s!)));
        f.Run("tables.import", Table(csv));
        Assert.Null(Materials.Find(f.Model, "Rockwool Frontrock"));

        var made = f.Session.Query("tables.preview", Table(csv, "Create"))!;
        Assert.Equal(1, (int)made["tally"]!["new"]!);
        f.Run("tables.import", Table(csv, "create"));
        var rw = Mat(f, "Rockwool Frontrock");
        Assert.Equal(140, Num(rw, "Pset_MaterialCommon", "MassDensity"), 9);
        Assert.Equal("Mineral wool board", rw.Str("description"));
        Assert.Equal("Insulation", rw.Str("category"));
    }

    [Fact]
    public void An_id_column_finds_the_material_and_a_changed_name_renames_it()
    {
        var f = WithMaterials();
        var id = Mat(f, "Steel S355").Id;
        f.Run("tables.import", Table($"Id;Material;Supplier\n{id};Steel S355 JR;Ferro\n"));
        var steel = f.Model.Get(id)!;
        Assert.Equal("Steel S355 JR", steel.Name);
        Assert.Equal("Ferro", (string)P(steel, null, "Supplier")!["value"]!);
    }

    [Fact]
    public void Values_and_columns_that_cannot_be_read_are_listed_with_their_rows()
    {
        var f = WithMaterials();
        var csv = "Material;Pset_MaterialCommon.MassDensity\nSteel S355;heavy\nConcrete C25/30;2400\nsteel s355;7850\n";
        var rows = f.Session.Query("tables.preview", Table(csv))!["rows"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains("not a number", (string)rows[0]["error"]!);
        Assert.Null(rows[1]["error"]);
        // The same material twice: the second row is refused.
        Assert.Contains("row 2", (string)rows[2]["error"]!);
        Assert.Throws<CommandException>(() => f.Run("tables.import", Table(csv)));

        var bad = f.Session.Query("tables.preview", Table("Material;Pset_MaterialThermal.Colour\nSteel S355;red\n"))!;
        Assert.Contains("has no property", (string)bad["rows"]![0]!["error"]!);
        Assert.Contains(bad["columns"]!.AsArray().OfType<JsonObject>(), c => c["error"] != null);
    }

    [Fact]
    public void The_export_reads_back_as_it_was()
    {
        var f = WithMaterials();
        f.Run("tables.import", Table("Material;Pset_MaterialThermal.ThermalConductivity;E (MPa);Fire class\nEPS 100 F + Ejotherm STR-U 2G;0,035;;E\nSteel S355;;210000;A1\n"));
        var before = f.Model.OfClass(IfcClass.Material).OrderBy(m => m.Id).Select(m => m.Attrs.ToJsonString()).ToList();

        var csv = f.Session.Query("material.exportData", new JsonObject { ["format"] = "csv" })!;
        Assert.EndsWith(".csv", (string)csv["file"]!);
        var text = (string)csv["text"]!;
        var header = Tables.Csv(text)[0];
        Assert.Equal("Material", header[0]);
        Assert.Contains("Pset_MaterialThermal.ThermalConductivity", header);
        Assert.Contains("Young's modulus", header);
        var preview = f.Session.Query("tables.preview", Table(text))!;
        Assert.True(0 == (int)preview["errors"]!, preview.ToJsonString());
        Assert.Equal(3, (int)preview["tally"]!["matched"]!);
        f.Run("tables.import", Table(text));
        Assert.Equal(before, f.Model.OfClass(IfcClass.Material).OrderBy(m => m.Id).Select(m => m.Attrs.ToJsonString()).ToList());

        // The workbook reads back the same way.
        var xlsx = f.Session.Query("material.exportData", new JsonObject { ["format"] = "xlsx" })!;
        Assert.EndsWith(".xlsx", (string)xlsx["file"]!);
        var rows = Tables.Read(Convert.FromBase64String((string)xlsx["data"]!));
        Assert.Equal(header, rows[0]);
        var a = new JsonObject { ["target"] = MaterialTable.Id, ["data"] = (string)xlsx["data"]! };
        Assert.Equal(0, (int)f.Session.Query("tables.preview", a)!["errors"]!);
    }
}
