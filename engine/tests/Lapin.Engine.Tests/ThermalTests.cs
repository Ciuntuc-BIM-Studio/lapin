using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The R and U-value of a layered type or element (issue #67, decision 717), after EN ISO 6946: R = Rsi + Σ d/λ + Rse, U = 1/R,
/// worked out on every ask; λ the material's own, else Lapin's indicative one by name; a layer without λ named, U not given; the
/// surface resistances by case, the project's over Lapin's; tags, schedules and formulas read it; IFC writes it unless one was typed.
/// </summary>
public sealed class ThermalTests
{
    static Fixture F() => new(new ArchitectureModule());

    static void Lambda(Fixture f, string material, double lambda)
    {
        var id = Materials.Find(f.Model, material)!.Id;
        f.Run("material.setData", new JsonObject { ["id"] = id, ["params"] = new JsonArray(new JsonObject { ["pset"] = "Pset_MaterialThermal", ["name"] = "ThermalConductivity", ["value"] = lambda }) });
    }

    /// <summary>Render 10 / Mineral wool 150 / Brick 250 (core) / Plaster 15, every λ the material's own.</summary>
    static (Fixture F, string Type, string Wall) Etics()
    {
        var f = F();
        var type = f.Type("ETICS 150", ("Render", 0.01, false), ("Mineral wool", 0.15, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
        Lambda(f, "Render", 0.87);
        Lambda(f, "Mineral wool", 0.035);
        Lambda(f, "Brick", 0.80);
        Lambda(f, "Plaster", 0.87);
        var wall = f.Wall(0, 0, 6, 0, type);
        return (f, type, wall);
    }

    // By hand: 0.13 + 0.01/0.87 + 0.15/0.035 + 0.25/0.80 + 0.015/0.87 + 0.04 = 4.796949 m²K/W, U = 0.208466 W/m²K.
    const double HandR = 0.13 + 0.01 / 0.87 + 0.15 / 0.035 + 0.25 / 0.80 + 0.015 / 0.87 + 0.04;

    [Fact]
    public void A_wall_type_with_known_layers_gives_the_hand_computed_u()
    {
        var (f, type, wall) = Etics();
        Assert.Equal(4.796949, HandR, 5);
        var t = Thermal.Of(f.Model, f.Model.Get(type)!)!;
        Assert.Equal("wall", t.Kind);
        Assert.Equal("wall", t.Case);
        Assert.Equal((0.13, 0.04), (t.Rsi, t.Rse));
        Assert.Equal(HandR, t.RTotal, 9);
        Assert.Equal(0.208466, t.U!.Value, 6);
        Assert.True(t.Complete);
        Assert.All(t.Layers, l => Assert.Equal("material", l.Source));
        Assert.Equal(0.15 / 0.035, t.Layers[1].R!.Value, 9);
        // The element reads its type's layers, and so does the query.
        var e = Thermal.Of(f.Model, f.Model.Get(wall)!)!;
        Assert.Equal("type", e.From);
        Assert.Equal(t.U, e.U);
        var q = (JsonObject)f.Session.Query("thermal.of", new JsonObject { ["id"] = wall })!;
        Assert.Equal(0.2085, (double)q["u"]!, 4);
        Assert.True((bool)q["complete"]!);
        // Lapin's surface resistances are indicative; the project's own make it its own.
        Assert.True((bool)q["indicative"]!);
        f.Run("thermal.setSettings", new JsonObject { ["cases"] = new JsonObject { ["wall"] = new JsonObject { ["rsi"] = 0.13, ["rse"] = 0.04 } } });
        Assert.False(Thermal.Of(f.Model, f.Model.Get(wall)!)!.Indicative);
        // A change of λ follows at once: nothing is stored.
        Lambda(f, "Mineral wool", 0.040);
        Assert.Equal(1 / (HandR - 0.15 / 0.035 + 0.15 / 0.040), Thermal.Of(f.Model, f.Model.Get(wall)!)!.U!.Value, 9);
    }

    [Fact]
    public void A_layer_without_lambda_is_named_and_the_u_is_not_guessed()
    {
        var f = F();
        var type = f.Type("Odd wall", ("Mystery board", 0.02, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
        var t = Thermal.Of(f.Model, f.Model.Get(type)!)!;
        Assert.False(t.Complete);
        Assert.Null(t.U);
        Assert.Equal(["Mystery board"], t.Missing);
        // Brick and plaster take Lapin's indicative λ by their names, marked so.
        Assert.Equal(("indicative", 0.80), (t.Layers[1].Source, t.Layers[1].Lambda!.Value));
        Assert.Equal("solid brick masonry", t.Layers[1].As);
        Assert.True(t.Indicative);
        // Without Lapin's indicative values, they are missing too; and back.
        f.Run("thermal.setSettings", new JsonObject { ["indicative"] = false });
        Assert.Equal(["Mystery board", "Brick", "Plaster"], Thermal.Of(f.Model, f.Model.Get(type)!)!.Missing);
        f.Run("thermal.setSettings", new JsonObject { ["indicative"] = true });
        Assert.Equal(["Mystery board"], Thermal.Of(f.Model, f.Model.Get(type)!)!.Missing);
        // The fields: no U, the layers missing named.
        var wall = f.Model.Get(f.Wall(0, 0, 5, 0, type))!;
        Assert.Equal("", TagText.Value(f.Model, wall, "UValue"));
        Assert.Equal("Mystery board", TagText.Value(f.Model, wall, "UValueMissing"));
        // A layer of no thickness is skipped, never missing; an air layer takes its R from Table 8.
        var cavity = f.Type("Cavity wall", ("Brick", 0.12, false), ("Air cavity", 0.05, false), ("Paint", 0, false), ("Brick", 0.25, true));
        var c = Thermal.Of(f.Model, f.Model.Get(cavity)!)!;
        Assert.True(c.Complete);
        Assert.Equal(("air", 0.18), (c.Layers[1].Source, c.Layers[1].R!.Value));
        Assert.Equal("skipped", c.Layers[2].Source);
    }

    [Fact]
    public void The_surface_resistances_come_by_case_the_projects_over_lapins()
    {
        var (f, type, wall) = Etics();
        f.Run("thermal.setCase", new JsonObject { ["ids"] = new JsonArray(type), ["case"] = "wallUnheated" });
        var t = Thermal.Of(f.Model, f.Model.Get(wall)!)!;
        Assert.Equal(("wallUnheated", 0.13, 0.13), (t.Case, t.Rsi, t.Rse));
        Assert.Equal(HandR + 0.09, t.RTotal, 9);
        // The project's own values for a case, one undo step; null back to Lapin's.
        f.Run("thermal.setSettings", new JsonObject { ["cases"] = new JsonObject { ["wallUnheated"] = new JsonObject { ["rsi"] = 0.13, ["rse"] = 0.10 } } });
        Assert.Equal(0.10, Thermal.Of(f.Model, f.Model.Get(wall)!)!.Rse);
        f.Session.Undo();
        Assert.Equal(0.13, Thermal.Of(f.Model, f.Model.Get(wall)!)!.Rse);
        Assert.Throws<CommandException>(() => f.Run("thermal.setSettings", new JsonObject { ["cases"] = new JsonObject { ["wall"] = new JsonObject { ["rsi"] = -1, ["rse"] = 0 } } }));
        Assert.Throws<CommandException>(() => f.Run("thermal.setCase", new JsonObject { ["ids"] = new JsonArray(wall), ["case"] = "sideways" }));
        // The element's own case over its type's; null: by its type again.
        f.Run("thermal.setCase", new JsonObject { ["ids"] = new JsonArray(wall), ["case"] = "wallGround" });
        Assert.Equal(0, Thermal.Of(f.Model, f.Model.Get(wall)!)!.Rse);
        f.Run("thermal.setCase", new JsonObject { ["ids"] = new JsonArray(wall), ["case"] = null });
        Assert.Equal("wallUnheated", Thermal.Of(f.Model, f.Model.Get(wall)!)!.Case);
        var settings = (JsonObject)f.Session.Query("thermal.settings", new JsonObject())!;
        Assert.Equal(8, ((JsonArray)settings["cases"]!).Count);
        // A floor's heat flows down, a base slab's onto the ground.
        var slabType = Thermal.Of(f.Model, f.Model.OfClass(IfcClass.SlabType).FirstOrDefault() ?? f.Model.Get((string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Floor", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Screed", ["thickness"] = 0.05 }, new JsonObject { ["material"] = "Reinforced concrete", ["thickness"] = 0.15, ["core"] = true }),
        })!["id"]!)!)!;
        Assert.Equal(("floor", 0.17, 0.04), (slabType.Case, slabType.Rsi, slabType.Rse));
    }

    [Fact]
    public void Tags_schedules_and_formulas_read_it_and_a_typed_value_wins()
    {
        var (f, type, wall) = Etics();
        var w = f.Model.Get(wall)!;
        // UValue and RValue to six decimals (as a formula reads them), ThermalTransmittance to three (as a tag shows it).
        Assert.Equal("0.208466", TagText.Value(f.Model, w, "UValue"));
        Assert.Equal("0.208", TagText.Value(f.Model, w, "ThermalTransmittance"));
        Assert.Equal("4.79695", TagText.Value(f.Model, w, "RValue"));
        Assert.Equal("U = 0.208", TagText.Fill("U = {ThermalTransmittance}", f.Model, w, null, "mm", null));
        // A field schedule's column.
        var id = (string)f.Run("architecture.createSchedule", new JsonObject
        {
            ["kind"] = "fields", ["name"] = "U-values",
            ["fields"] = new JsonObject
            {
                ["filter"] = new JsonObject { ["classes"] = new JsonArray("IfcWall") },
                ["columns"] = new JsonArray(new JsonObject { ["field"] = "type" }, new JsonObject { ["field"] = "UValue", ["heading"] = "U", ["decimals"] = 3 }, new JsonObject { ["formula"] = "UValue * 2", ["heading"] = "2U", ["decimals"] = 3 }),
            },
        })!["id"]!;
        var g = new FieldSchedule().Grid(f.Session.Reader, f.Model.Get(id)!);
        var row = g.Rows.First(r => r[0] == "ETICS 150");
        Assert.Equal("0.208", row[1]);
        Assert.Equal("0.417", row[2]);
        // The type assemblies schedule: R and U on the type's row, λ and R on each layer's.
        var types = new TypeAssemblies().Grid(f.Session.Reader);
        Assert.Equal(["λ (W/mK)", "R (m²K/W)", "U (W/m²K)"], types.Columns.TakeLast(3));
        var typeRow = types.Rows.First(r => r[1] == "ETICS 150");
        Assert.Equal(("4.797", "0.208"), (typeRow[9], typeRow[10]));
        Assert.Contains(types.Rows, r => r[4] == "Mineral wool" && r[8] == "0.035" && r[9] == "4.286");
        // Typed on the type, it wins over the worked-out one in tags; UValue stays the worked-out one.
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(type), ["pset"] = "Pset_WallCommon", ["name"] = "ThermalTransmittance", ["value"] = 0.25 });
        w = f.Model.Get(wall)!;
        Assert.Equal(0.25, Thermal.Typed(f.Model, w));
        Assert.Equal("0.208466", TagText.Value(f.Model, w, "UValue"));
        Assert.StartsWith("0.25", TagText.Value(f.Model, w, "ThermalTransmittance"));
    }

    [Fact]
    public void Ifc_writes_the_worked_out_u_unless_one_was_typed()
    {
        var (f, type, _) = Etics();
        string Export()
        {
            var path = Path.Combine(Path.GetTempPath(), $"thermal-{Guid.NewGuid():N}.ifc");
            try { IfcExport.Write(f.Session, path); return File.ReadAllText(path); }
            finally { File.Delete(path); }
        }
        var text = Export();
        Assert.Contains("'Pset_WallCommon'", text);
        Assert.Contains("'ThermalTransmittance',$,IFCTHERMALTRANSMITTANCEMEASURE(0.2085)", text);
        // A layer without λ: nothing written.
        f.Run("material.setData", new JsonObject { ["id"] = Materials.Find(f.Model, "Mineral wool")!.Id, ["params"] = new JsonArray() });
        f.Run("thermal.setSettings", new JsonObject { ["indicative"] = false });
        Assert.DoesNotContain("IFCTHERMALTRANSMITTANCEMEASURE(", Export());
        // Typed: the user's value is written, not Lapin's.
        Lambda(f, "Mineral wool", 0.035);
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(type), ["pset"] = "Pset_WallCommon", ["name"] = "ThermalTransmittance", ["value"] = 0.25 });
        text = Export();
        Assert.Contains("'ThermalTransmittance',$,IFCREAL(0.25)", text);
        Assert.DoesNotContain("IFCTHERMALTRANSMITTANCEMEASURE(0.2085)", text);
    }

    [Fact]
    public void Lapins_indicative_lambdas_know_the_common_materials_by_name()
    {
        foreach (var (name, lambda) in new[]
        {
            ("Reinforced concrete", 2.30), ("Beton armat C25/30", 2.30), ("Concrete", 2.00), ("AAC block", 0.16), ("BCA", 0.16), ("Mineral wool", 0.040), ("Vată minerală", 0.040),
            ("EPS insulation", 0.040), ("Polistiren extrudat", 0.035), ("XPS", 0.035), ("Timber", 0.13), ("Gypsum board", 0.25), ("Plasterboard", 0.25), ("Plaster", 0.87),
            ("Gypsum plaster", 0.40), ("Screed", 1.40), ("Glass", 1.00), ("Brick", 0.80), ("Cărămidă cu goluri", 0.46),
        })
            Assert.Equal(lambda, Thermal.IndicativeLambda(name)!.Value.Lambda);
        Assert.Null(Thermal.IndicativeLambda("Floor finish"));
        Assert.Null(Thermal.IndicativeLambda("Material"));
        Assert.Equal(0.15, Thermal.AirResistance(0.01, "horizontal"), 9);
        Assert.Equal(0.21, Thermal.AirResistance(0.05, "downward"), 9);
    }
}
