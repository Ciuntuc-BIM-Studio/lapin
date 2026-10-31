using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>A column's reinforcement on its type (decision 218): stirrup patterns (a diamond, two inside) and its longitudinal bars with their starter bars.</summary>
public class ColumnReinforcementTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonArray Pens(JsonObject d) => (JsonArray)d["pens"]!;
    static int Dots(JsonObject d) => Pens(d).Where(p => p!["fill"] != null && (string?)p["layer"] == "DRAFT-FILL" && (string?)p["fill"] != "#222222").Sum(p => (p!["loops"] as JsonArray)?.Count ?? 0);
    static int Lines(JsonObject d, string layer) => Pens(d).Where(p => (string?)p!["layer"] == layer && (double)p["width"]! > 0.2).Sum(p => ((p!["segments"] as JsonArray)?.Count ?? 0) / 4);

    [Fact]
    public void A_column_s_stirrups_may_take_a_diamond_or_two_inner_ones_each_kind_with_its_own_mark()
    {
        var f = F();
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3 })!["id"]!;
        var type = f.Model.RelatingOf(column, IfcClass.RelDefinesByType)!.Id;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["enabled"] = true, ["step"] = 0.2, ["pattern"] = "diamond" });
        var laid = TypeStirrups.Of(f.Model, f.Model.Get(column)!)!;
        var loops = laid.LoopsAt(1.0);
        Assert.Equal(2, loops.Count);
        // The diamond's corners at the perimeter stirrup's sides' middles: a rhombus whose side is half the diagonal of 0.242 × 0.242.
        Assert.Equal(0.242 * Math.Sqrt(2) / 2, loops[1].A, 9);
        Assert.All(loops[1].Corners, p => Assert.True(Math.Abs(Math.Abs(p.X - 2) - 0.121) < 1e-9 || Math.Abs(Math.Abs(p.Y - 3) - 0.121) < 1e-9));
        Assert.True(laid.Spec.InnerMarks is { Count: 1 } && laid.Spec.InnerMarks[0] != laid.Spec.Mark && laid.Spec.InnerMarks[0] > 0);
        var entries = RebarSchedule.Entries(f.Model);
        Assert.Contains(entries, x => x.Mark == laid.Spec.Mark && x.Shape == "Stirrup");
        Assert.Contains(entries, x => x.Mark == laid.Spec.InnerMarks![0] && x.Shape == "Diamond stirrup");
        // Two inside, one each way: three loops; the inner ones as wide as the perimeter's one way, as thick as a bar and two stirrups the other.
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["pattern"] = "cross" });
        laid = TypeStirrups.Of(f.Model, f.Model.Get(column)!)!;
        loops = laid.LoopsAt(1.0);
        Assert.Equal(3, loops.Count);
        Assert.Equal(0.242, loops[1].A, 9);
        Assert.Equal(0.016 + 2 * 0.008 + 0.004, loops[1].B, 9);
        Assert.Equal(loops[1].B, loops[2].A, 9);
        Assert.Equal(2, laid.Spec.InnerMarks!.Count);
        // Across: three stirrups bent; up a vertical section: two lines per station (the perimeter's and the wide inner one fall on one line, the narrow one is its own).
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = column, ["station"] = 1.0 })!["id"]!;
        Assert.InRange(Lines(Drawing(f, across), RebarShapes.StirrupLayer), 60, 400);
        var through = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 2), ["end"] = new JsonArray(4, 2) })!["id"]!;
        Assert.Equal(2 * laid.Stations.Count, Lines(Drawing(f, through), RebarShapes.StirrupLayer));
        // A beam keeps the single perimeter stirrup whatever is asked.
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id, ["enabled"] = true, ["pattern"] = "diamond" });
        Assert.Equal("single", TypeStirrups.SpecOf(f.Model, f.Model.Get(beam)!)!.Pattern);
    }

    [Fact]
    public void A_column_type_s_bars_go_round_its_faces_and_past_its_top_by_the_lap()
    {
        var f = F();
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3 })!["id"]!;
        var type = f.Model.RelatingOf(column, IfcClass.RelDefinesByType)!.Id;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["enabled"] = true });
        f.Run("rebar.setColumnBars", new JsonObject { ["id"] = type, ["enabled"] = true, ["perFace"] = 3, ["diameter"] = 0.016 });
        var laid = ColumnBars.Of(f.Model, f.Model.Get(column)!)!;
        var height = Columns.Spec(f.Model, f.Model.Get(column)!).Height;
        // Three a face, the corners shared: eight bars, 25 + 8 + 8 mm in from the faces of the 0.3 × 0.3 column, up to 1 m past the top.
        Assert.Equal(8, laid.Bars.Count);
        Assert.All(laid.Bars, b => Assert.True(Math.Abs(Math.Abs(b[0].X - 2) - 0.109) < 1e-9 || Math.Abs(Math.Abs(b[0].Y - 3) - 0.109) < 1e-9));
        Assert.Equal(8, laid.Bars.Select(b => (Math.Round(b[0].X, 6), Math.Round(b[0].Y, 6))).Distinct().Count());
        Assert.All(laid.Bars, b => Assert.Equal(height + 1.0, b[1].Z - b[0].Z, 9));
        Assert.Equal(height + 1.0, laid.Length, 9);
        Assert.True(laid.Spec.Mark > 0 && laid.Spec.Mark != TypeStirrups.SpecOf(f.Model, f.Model.Get(column)!)!.Mark);
        Assert.Contains(RebarSchedule.Entries(f.Model), x => x.Mark == laid.Spec.Mark && x.Count == 8 && Math.Abs(x.Length - height - 1.0) < 1e-9);
        // Cut horizontally: eight dots; in the structure's plan too; up a vertical section through it: eight lines and the label.
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = column, ["station"] = 1.0 })!["id"]!;
        Assert.Equal(8, Dots(Drawing(f, across)));
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["discipline"] = "Structure" })!["id"]!;
        Assert.Equal(8, Dots(Drawing(f, plan)));
        var through = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 2), ["end"] = new JsonArray(4, 2) })!["id"]!;
        var d = Drawing(f, through);
        Assert.Equal(8, Lines(d, RebarShapes.LongitudinalLayer));
        Assert.Contains($"8Ø16 L={Math.Round((height + 1.0) * 1000)}", Pens(d).SelectMany(p => (p!["texts"] as JsonArray)?.Select(t => (string?)t!["text"]) ?? []));
        Assert.Throws<CommandException>(() => f.Run("rebar.setColumnBars", new JsonObject { ["id"] = f.Model.RelatingOf((string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!, IfcClass.RelDefinesByType)!.Id, ["enabled"] = true }));
    }
}
