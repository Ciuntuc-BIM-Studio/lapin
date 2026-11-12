using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The table import (docs/plan-user-extensibility.md §7): numbers and units as people write them, headings guessed in any language,
/// a preview row by row, the whole table one undo step through the modules' commands; property values by GlobalId, mark or name;
/// load cases; point, line and area loads in any unit, their missing cases made.
/// </summary>
public sealed class TableImportTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new AnalysisModule());

    [Theory]
    [InlineData("2,5", 2.5)]
    [InlineData("2.5", 2.5)]
    [InlineData("1 250,75", 1250.75)]
    [InlineData("1.250,75", 1250.75)]
    [InlineData("1,250.75", 1250.75)]
    [InlineData("1,250,000", 1250000)]
    [InlineData("−3,2", -3.2)]
    [InlineData("1e3", 1000)]
    public void Numbers_read_with_a_decimal_comma_or_dot_and_thousands_grouped(string text, double value) =>
        Assert.Equal(value, TableUnits.Parse(text)!.Value, 9);

    [Fact]
    public void Headings_are_guessed_in_any_language_with_their_units()
    {
        var g = TableImport.Guess(AnalysisTables.Loads, ["Tip", "Caz", "Nivel", "X (mm)", "Y [mm]", "Valoare (daN/m²)", "Direcție", "Altceva"]);
        Assert.Equal(new[] { "kind", "case", "storey", "x", "y", "value", "direction", "" }, g.Select(x => x.Key ?? "").ToArray());
        Assert.Equal("mm", g[3].Unit);
        Assert.Equal("daN/m²", g[5].Unit);
        Assert.Equal(0.001, TableUnits.Factor("length", "mm"), 12);
        Assert.Equal(0.01, TableUnits.Factor("areaLoad", "daN/m2"), 12);
    }

    static JsonObject Table(string target, string csv, JsonObject? defaults = null) =>
        new() { ["target"] = target, ["text"] = csv, ["defaults"] = defaults };

    [Fact]
    public void Loads_come_in_from_a_table_in_their_units_their_missing_cases_made_in_one_undo_step()
    {
        var f = F();
        var storey = f.Model.Get(f.Storey)!;
        var csv = $"""
            Type;Case;Storey;x (mm);y (mm);x1;y1;x2;y2;Points;Value;Unit;Direction
            punctuala;G;{storey.Name};2500;3000;;;;;;12;kN;down
            line;G;{storey.Name};;;0;0;6;0;;450;daN/m;
            area;Snow;{storey.Name};;;;;;;"0 0; 6 0; 6 4; 0 4";1,2;kN/m²;
            point;G;{storey.Name};1000;1000;;;;;;-5;kN;
            """;
        var preview = f.Session.Query("tables.preview", Table(AnalysisTables.LoadsId, csv))!;
        Assert.Equal(4, (int)preview["ok"]!);
        Assert.Equal(0, (int)preview["errors"]!);
        var described = preview["rows"]!.AsArray().SelectMany(r => r!["steps"]!.AsArray().Select(s => (string)s!)).ToList();
        Assert.Contains(described, d => d.StartsWith("Point load 12 kN down at (2.5, 3)", StringComparison.Ordinal));
        Assert.Contains(described, d => d.StartsWith("Line load 4.5 kN/m", StringComparison.Ordinal));
        Assert.Contains(described, d => d.Contains("24 m²", StringComparison.Ordinal));
        // G and Snow are not the project's: each made once.
        Assert.Equal(1, described.Count(d => d.StartsWith("Load case Snow: new", StringComparison.Ordinal)));

        var before = UserLoads.All(f.Model).Count();
        var done = f.Run("tables.import", Table(AnalysisTables.LoadsId, csv))!;
        Assert.Equal(4, (int)done["rows"]!);
        var loads = UserLoads.All(f.Model).ToList();
        Assert.Equal(before + 4, loads.Count);
        var point = loads.Single(l => l.Kind == UserLoads.Point && Math.Abs(l.Value - 12) < 1e-9);
        Assert.Equal(2.5, point.Points[0].X, 9);
        Assert.Equal("G", LoadCases.Find(f.Model, point.CaseId)!.Code);
        Assert.Equal(4.5, loads.Single(l => l.Kind == UserLoads.Line).Value, 9);
        Assert.Equal(1.2, loads.Single(l => l.Kind == UserLoads.Area).Value, 9);
        // A negative value is the load the other way.
        Assert.Equal("up", loads.Single(l => l.Kind == UserLoads.Point && Math.Abs(l.Value - 5) < 1e-9).Direction);
        Assert.Equal("Snow", LoadCases.Find(f.Model, loads.Single(l => l.Kind == UserLoads.Area).CaseId)!.Code);

        // One undo step takes it all back, the cases made too.
        f.Session.Undo();
        Assert.Equal(before, UserLoads.All(f.Model).Count());
        Assert.DoesNotContain(LoadCases.All(f.Model), c => c.Code == "Snow");
    }

    [Fact]
    public void Rows_that_cannot_be_read_are_listed_with_why_and_refused_unless_left_out()
    {
        var f = F();
        var storey = f.Model.Get(f.Storey)!.Name;
        var csv = $"""
            Kind;Storey;x;y;Value;Unit
            point;{storey};1;1;10;kN
            point;Attic;1;1;10;kN
            point;{storey};1;1;ten;kN
            line;{storey};1;1;5;kN/m
            area;{storey};1;1;2;kN
            """;
        var rows = f.Session.Query("tables.preview", Table(AnalysisTables.LoadsId, csv))!["rows"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Null(rows[0]["error"]);
        Assert.Contains("no storey „Attic”", (string)rows[1]["error"]!);
        Assert.Contains("not a number", (string)rows[2]["error"]!);
        Assert.Contains("x1 is empty", (string)rows[3]["error"]!);
        Assert.Contains("not a unit of load per square metre", (string)rows[4]["error"]!);
        Assert.Throws<CommandException>(() => f.Run("tables.import", Table(AnalysisTables.LoadsId, csv)));
        var before = UserLoads.All(f.Model).Count();
        var a = Table(AnalysisTables.LoadsId, csv);
        a["skipErrors"] = true;
        Assert.Equal(4, (int)f.Run("tables.import", a)!["skipped"]!);
        Assert.Equal(before + 1, UserLoads.All(f.Model).Count());
    }

    [Fact]
    public void A_storey_chosen_for_the_table_is_used_where_a_row_names_none()
    {
        var f = F();
        var csv = "x;y;Value (kN)\n1;2;7\n";
        Assert.Throws<CommandException>(() => f.Run("tables.import", Table(AnalysisTables.LoadsId, csv)));
        f.Run("tables.import", Table(AnalysisTables.LoadsId, csv, new JsonObject { ["storey"] = f.Storey }));
        Assert.Contains(UserLoads.All(f.Model), l => l.Kind == UserLoads.Point && Math.Abs(l.Value - 7) < 1e-9);
    }

    [Fact]
    public void Load_cases_are_made_or_changed_by_their_code()
    {
        var f = F();
        f.Run("analysis.defaultCases", []);
        var csv = "Cod;Denumire;Acțiune;Sursă\nW;Vânt;variabila;vant\nG;Permanente;permanenta;dead\n";
        f.Run("tables.import", Table(AnalysisTables.CasesId, csv));
        var w = LoadCases.All(f.Model).Single(c => c.Code == "W");
        Assert.Equal(LoadCases.Variable, w.ActionType);
        Assert.Equal("WIND_W", w.ActionSource);
        Assert.Equal("Vânt", w.Name);
        Assert.Equal("Permanente", LoadCases.All(f.Model).Single(c => c.Code == "G").Name);
    }

    [Fact]
    public void Property_values_land_on_elements_found_by_GlobalId_mark_or_name()
    {
        var f = F();
        var w1 = f.Wall(0, 0, 5, 0);
        var w2 = f.Wall(5, 0, 5, 4);
        f.Run("element.rename", new JsonObject { ["id"] = w2, ["name"] = "W-02" });
        var csv = $"GlobalId;Pset_WallCommon.FireRating;Cost.Price;Note\n{w1};REI 120;125,5;north\nW-02;REI 60;80;\n";
        var preview = f.Session.Query("tables.preview", Table(PropertyTable.Id, csv))!;
        Assert.True(0 == (int)preview["errors"]!, preview.ToJsonString());
        f.Run("tables.import", Table(PropertyTable.Id, csv));
        JsonObject? P(string id, string? set, string name) => (f.Model.Get(id)!.Attrs["params"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(p => Params.Same(p, set, name));
        Assert.Equal("REI 120", (string)P(w1, "Pset_WallCommon", "FireRating")!["value"]!);
        Assert.Equal(125.5, (double)P(w1, "Cost", "Price")!["value"]!, 9);
        Assert.Equal("number", (string)P(w1, "Cost", "Price")!["kind"]!);
        Assert.Equal("north", (string)P(w1, null, "Note")!["value"]!);
        Assert.Equal("REI 60", (string)P(w2, "Pset_WallCommon", "FireRating")!["value"]!);
        // An element nobody has: refused with its row.
        var bad = f.Session.Query("tables.preview", Table(PropertyTable.Id, "Mark;Note\nW-99;x\n"))!["rows"]![0]!;
        Assert.Contains("W-99", (string)bad["error"]!);
    }

    [Fact]
    public void Every_target_offers_a_template_its_own_guess_reads_back()
    {
        var f = F();
        var targets = f.Session.Query("tables.targets", [])!["targets"]!.AsArray().OfType<JsonObject>().Select(t => (string)t["id"]!).ToList();
        Assert.Contains(AnalysisTables.LoadsId, targets);
        Assert.Contains(PropertyTable.Id, targets);
        foreach (var t in TableImport.Targets(f.Session.Reader.Services))
        {
            var text = TableImport.Template(t);
            var header = Tables.Csv(text)[0];
            var guess = TableImport.Guess(t, header);
            Assert.All(t.Columns, c => Assert.Contains(guess, g => g.Key == c.Key));
        }
    }
}
