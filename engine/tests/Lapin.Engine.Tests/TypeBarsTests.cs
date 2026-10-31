using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Longitudinal bars configured on a beam's or a column's type (decision 216): laid in space inside the stirrups,
/// the corners first; dots across, lines along; each row a mark in the schedule.
/// </summary>
public class TypeBarsTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonArray Pens(JsonObject d) => (JsonArray)d["pens"]!;
    static int Dots(JsonObject d) => Pens(d).Where(p => p!["fill"] != null && (string?)p["layer"] == "DRAFT-FILL").Sum(p => (p!["loops"] as JsonArray)?.Count ?? 0);
    static int Lines(JsonObject d) => Pens(d).Where(p => (string?)p!["layer"] == RebarShapes.LongitudinalLayer && (p["segments"] as JsonArray)?.Count > 0 && (double)p["width"]! > 0.2).Sum(p => ((JsonArray)p!["segments"]!).Count / 4);

    [Fact]
    public void A_beam_type_s_rows_lie_inside_the_stirrups_corners_first_and_each_row_takes_a_mark()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var type = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["enabled"] = true });
        f.Run("rebar.setTypeBars", new JsonObject { ["id"] = type, ["enabled"] = true, ["top"] = new JsonObject { ["count"] = 2, ["diameter"] = 0.012 }, ["bottom"] = new JsonObject { ["count"] = 3, ["diameter"] = 0.016 }, ["extend"] = 0.2 });
        var laid = TypeBars.Of(f.Model, f.Model.Get(beam)!)!;
        var bottom = laid.Groups.Single(g => g.Name == "bottom");
        var top = laid.Groups.Single(g => g.Name == "top");
        Assert.Equal(3, bottom.Bars.Count);
        Assert.Equal(2, top.Bars.Count);
        // The beam's top is the storey's top (+3), its depth 0.4: the bottom bars 25 + 8 + 8 mm up from the soffit, the top ones 25 + 8 + 6 down from the top.
        var topZ = Beams.Top(f.Model, f.Model.Get(beam)!);
        Assert.All(bottom.Bars, b => Assert.Equal(topZ - 0.4 + 0.041, b[0].Z, 9));
        Assert.All(top.Bars, b => Assert.Equal(topZ - 0.039, b[0].Z, 9));
        // Across: 0.25 wide, 25 + 8 + 8 in from each face: the corners at ±0.084, the third in the middle.
        Assert.Equal(new[] { -0.084, 0.0, 0.084 }, bottom.Bars.Select(b => Math.Round(b[0].Y, 9)).Order());
        // From end to end plus the extension each way.
        Assert.Equal(-0.2, bottom.Bars[0][0].X, 9);
        Assert.Equal(6.2, bottom.Bars[0][1].X, 9);
        Assert.Equal(6.4, bottom.Length, 9);
        Assert.True(top.Row.Mark > 0 && bottom.Row.Mark > 0 && top.Row.Mark != bottom.Row.Mark);
        var stirrupMark = TypeStirrups.SpecOf(f.Model, f.Model.Get(beam)!)!.Mark;
        Assert.NotEqual(stirrupMark, top.Row.Mark);
        // In the schedule, each row under its mark.
        var entries = RebarSchedule.Entries(f.Model);
        Assert.Contains(entries, x => x.Mark == bottom.Row.Mark && x.Count == 3 && Math.Abs(x.Length - 6.4) < 1e-9 && Math.Abs(x.Diameter - 0.016) < 1e-9);
        Assert.Contains(entries, x => x.Mark == top.Row.Mark && x.Count == 2);
    }

    [Fact]
    public void The_bars_draw_as_dots_across_and_lines_along_with_a_label_for_each_row()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var type = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id;
        f.Run("rebar.setTypeBars", new JsonObject { ["id"] = type, ["enabled"] = true, ["top"] = new JsonObject { ["count"] = 2, ["diameter"] = 0.012 }, ["bottom"] = new JsonObject { ["count"] = 3, ["diameter"] = 0.016 }, ["side"] = new JsonObject { ["count"] = 1, ["diameter"] = 0.01 } });
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 3 })!["id"]!;
        // 2 + 3 + 2 side bars: seven dots.
        Assert.Equal(7, Dots(Drawing(f, across)));
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, -2), ["end"] = new JsonArray(7, -2) })!["id"]!;
        var d = Drawing(f, along);
        Assert.Equal(7, Lines(d));
        var texts = Pens(d).SelectMany(p => (p!["texts"] as JsonArray)?.Select(t => (string?)t!["text"]) ?? []).ToList();
        Assert.Contains("3Ø16 L=6000", texts);
        Assert.Contains("2Ø12 L=6000", texts);
    }

    [Fact]
    public void A_column_s_bars_go_round_its_faces_with_the_corners_shared()
    {
        var f = F();
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3 })!["id"]!;
        var type = f.Model.RelatingOf(column, IfcClass.RelDefinesByType)!.Id;
        f.Run("rebar.setTypeBars", new JsonObject { ["id"] = type, ["enabled"] = true, ["perFace"] = new JsonObject { ["count"] = 3, ["diameter"] = 0.016 } });
        var laid = TypeBars.Of(f.Model, f.Model.Get(column)!)!;
        var all = laid.Groups.Single();
        // Three a face, the corners shared: eight bars round a 0.3 × 0.3 column, 41 mm in from the faces.
        Assert.Equal(8, all.Bars.Count);
        Assert.All(all.Bars, b => Assert.True(Math.Abs(Math.Abs(b[0].X - 2) - 0.109) < 1e-9 || Math.Abs(Math.Abs(b[0].Y - 3) - 0.109) < 1e-9));
        Assert.Equal(8, all.Bars.Select(b => (Math.Round(b[0].X, 6), Math.Round(b[0].Y, 6))).Distinct().Count());
        Assert.Throws<CommandException>(() => f.Run("rebar.setTypeBars", new JsonObject { ["id"] = type, ["perFace"] = new JsonObject { ["count"] = 1, ["diameter"] = 0.016 } }));
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = column, ["station"] = 1.0 })!["id"]!;
        Assert.Equal(8, Dots(Drawing(f, across)));
    }
}
