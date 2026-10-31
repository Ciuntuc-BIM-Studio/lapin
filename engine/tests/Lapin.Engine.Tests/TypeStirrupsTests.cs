using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stirrups configured on a beam's or a column's type (decision 215): laid along the axis at one step or in three
/// zones, inside the cover; drawn as the view lies to the axis (a rectangle across, lines along); in the schedule under the type's mark.
/// </summary>
public class TypeStirrupsTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static int StirrupSegments(JsonObject d) => ((JsonArray)d["pens"]!).Where(p => (string?)p!["layer"] == RebarShapes.StirrupLayer).Sum(p => ((p!["segments"] as JsonArray)?.Count ?? 0) / 4);

    [Fact]
    public void A_beam_type_s_stirrups_stand_along_the_axis_inside_the_cover_at_one_step_or_in_three_zones()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var type = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!;
        Assert.Null(TypeStirrups.Of(f.Model, f.Model.Get(beam)!));
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type.Id, ["enabled"] = true, ["step"] = 0.15 });
        var laid = TypeStirrups.Of(f.Model, f.Model.Get(beam)!)!;
        // 6 m at 150 mm: 41 stirrups, one at each end; the loop 0.25 − 2·0.025 − 0.008 wide and 0.4 − 0.058 deep, on the bar's axis.
        Assert.Equal(41, laid.Stations.Count);
        Assert.Equal(0.192, laid.A, 9);
        Assert.Equal(0.342, laid.B, 9);
        Assert.True(laid.Spec.Mark >= 1);
        var loop = laid.LoopsAt(3)[0].Corners;
        Assert.All(loop, p => Assert.Equal(3, p.X, 9));
        Assert.Equal(0.192, loop.Max(p => p.Y) - loop.Min(p => p.Y), 9);
        Assert.Equal(0.342, loop.Max(p => p.Z) - loop.Min(p => p.Z), 9);
        Assert.Equal(2 * (0.192 + 0.342) + 2 * 0.08, laid.BarLength(laid.LoopsAt(3)[0]), 9);
        // Three zones, a third each: 100 at the ends, 200 in the middle: 20 + 10 + 20 + 1.
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type.Id, ["mode"] = "zones", ["zones"] = new JsonArray(new JsonObject { ["share"] = 1.0 / 3, ["step"] = 0.1 }, new JsonObject { ["share"] = 1.0 / 3, ["step"] = 0.2 }, new JsonObject { ["share"] = 1.0 / 3, ["step"] = 0.1 }) });
        laid = TypeStirrups.Of(f.Model, f.Model.Get(beam)!)!;
        Assert.Equal(51, laid.Stations.Count);
        Assert.Equal("51Ø8/100-200", laid.Label);
        // In the schedule under the type's mark; off again, gone.
        Assert.Contains(RebarSchedule.Entries(f.Model), e => e.Mark == laid.Spec.Mark && e.Count == 51 && e.Shape == "Stirrup");
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type.Id, ["enabled"] = false });
        Assert.Null(TypeStirrups.Of(f.Model, f.Model.Get(beam)!));
    }

    [Fact]
    public void The_stirrups_draw_as_the_view_lies_to_the_axis_a_rectangle_across_lines_along_and_only_in_structural_plans()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id, ["enabled"] = true, ["step"] = 0.15 });
        // Across the beam: the stirrup as it is bent (its rounded corners and hooks: many short segments), the ones behind it within the window drawn once.
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 3 })!["id"]!;
        Assert.InRange(StirrupSegments(Drawing(f, across)), 20, 200);
        // Along it, 2 m in front: a line for each stirrup, and the label.
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, -2), ["end"] = new JsonArray(7, -2) })!["id"]!;
        var d = Drawing(f, along);
        Assert.Equal(41, StirrupSegments(d));
        var texts = ((JsonArray)d["pens"]!).SelectMany(p => (p!["texts"] as JsonArray)?.Select(t => (string?)t!["text"]) ?? []).ToList();
        Assert.Contains("41Ø8/150", texts);
        // The storey's plan: none, unless it is the structure's.
        Assert.Equal(0, StirrupSegments(Drawing(f, "plan:" + f.Storey)));
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["discipline"] = "Structure" })!["id"]!;
        Assert.Equal(41, StirrupSegments(Drawing(f, plan)));
    }

    [Fact]
    public void A_column_type_s_stirrups_go_up_its_height_horizontal_at_each_step()
    {
        var f = F();
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3 })!["id"]!;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = f.Model.RelatingOf(column, IfcClass.RelDefinesByType)!.Id, ["enabled"] = true, ["step"] = 0.2 });
        var laid = TypeStirrups.Of(f.Model, f.Model.Get(column)!)!;
        var height = Columns.Spec(f.Model, f.Model.Get(column)!).Height;
        Assert.Equal((int)Math.Ceiling(height / 0.2 + 1 - 1e-9), laid.Stations.Count);
        var loop = laid.LoopsAt(1.0)[0].Corners;
        Assert.All(loop, p => Assert.Equal(loop[0].Z, p.Z, 9));
        Assert.Equal(0.242, loop.Max(p => p.X) - loop.Min(p => p.X), 9);
        // Cut horizontally at 1 m: the rectangle; in a vertical section through it: a line per stirrup.
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = column, ["station"] = 1.0 })!["id"]!;
        Assert.InRange(StirrupSegments(Drawing(f, across)), 20, 200);
        var through = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 2), ["end"] = new JsonArray(4, 2) })!["id"]!;
        Assert.Equal(laid.Stations.Count, StirrupSegments(Drawing(f, through)));
    }
}
