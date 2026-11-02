using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The bar bending schedule counts what is drawn: bars on a deleted view go with it, a hooked column bar is scheduled as
/// long as it is drawn, a type's mark on elements of different lengths gives a row per length; a distribution come to a
/// point is refused; a bar left out of the schedule still stands in space; zones whose shares are all zero are refused.
/// </summary>
public class RebarAuditTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static string Section(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(6, 0) })!["id"]!;

    static string Column(Fixture f, string storey, double width, double depth, out string type)
    {
        type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = $"C{width}x{depth}", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = width, ["depth"] = depth } })!["id"]!;
        return (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = storey, ["x"] = 2, ["y"] = 3, ["typeId"] = type })!["id"]!;
    }

    [Fact]
    public void Bars_on_a_deleted_view_go_with_it()
    {
        var f = F();
        var view = Section(f);
        var other = Section(f);
        f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shape"] = "straight", ["at"] = new JsonArray(1, 1), ["params"] = new JsonObject { ["length"] = 2 } });
        f.Run("rebar.addRun", new JsonObject { ["viewId"] = view, ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(1, 0) });
        f.Run("rebar.add", new JsonObject { ["viewId"] = other, ["shape"] = "straight", ["at"] = new JsonArray(1, 1), ["params"] = new JsonObject { ["length"] = 2 } });
        Assert.Equal(3, RebarSchedule.Entries(f.Model).Count);
        f.Run("element.delete", new JsonObject { ["id"] = view });
        Assert.Single(RebarSchedule.Entries(f.Model));
        Assert.Single(f.Model.OfClass(Rebars.Class).Concat(f.Model.OfClass(Rebars.RunClass)));
        f.Session.Undo();
        Assert.Equal(3, RebarSchedule.Entries(f.Model).Count);
    }

    [Fact]
    public void A_hooked_column_s_bars_are_scheduled_as_long_as_they_are_drawn()
    {
        var f = F();
        var column = Column(f, f.Storey, 0.25, 0.5, out var type);
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["enabled"] = true });
        f.Run("rebar.setColumnBars", new JsonObject { ["id"] = type, ["enabled"] = true, ["perFace"] = 3, ["diameter"] = 0.02, ["top"] = "hooks" });
        var laid = ColumnBars.Of(f.Model, f.Model.Get(column)!)!;
        var drawn = laid.Bars.Sum(b => b.Zip(b.Skip(1), (p, q) => (q - p).Length).Sum());
        var scheduled = RebarSchedule.Entries(f.Model).Where(x => x.Mark == laid.Spec.Mark).Sum(x => x.Count * x.Length);
        Assert.Equal(drawn, scheduled, 6);
        // The tails differ by the face they turn across: rows of one mark, each its own length, none in conflict.
        var rows = RebarSchedule.Rows(RebarSchedule.Entries(f.Model), out _).Where(r => r[0] == laid.Spec.Mark.ToString()).ToList();
        Assert.True(rows.Count > 1);
        Assert.All(rows, r => Assert.Equal("", r[7]));
        Assert.Equal(laid.Bars.Count, rows.Sum(r => int.Parse(r[3])));
    }

    [Fact]
    public void A_type_s_mark_on_columns_of_different_heights_gives_a_row_per_length()
    {
        var f = F();
        var low = Column(f, f.Storey, 0.3, 0.3, out var type);
        var elevation = Spatial.Storeys(f.Model).First(x => x.Id == f.Storey).Num("elevation");
        var height = Columns.Spec(f.Model, f.Model.Get(low)!).Height;
        var above = (string)f.Run("storey.create", new JsonObject { ["name"] = "Above", ["elevation"] = elevation + height })!["id"]!;
        f.Run("storey.update", new JsonObject { ["id"] = above, ["height"] = height + 1 });
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = above, ["x"] = 2, ["y"] = 3, ["typeId"] = type });
        f.Run("rebar.setColumnBars", new JsonObject { ["id"] = type, ["enabled"] = true, ["perFace"] = 2, ["diameter"] = 0.016 });
        var mark = ColumnBarsSpec.Of(f.Model.Get(type)!)!.Mark;
        var rows = RebarSchedule.Rows(RebarSchedule.Entries(f.Model), out _).Where(r => r[0] == mark.ToString()).ToList();
        // The lower one's starter bars, the upper one's hooks: two lines, each buildable.
        Assert.Equal(2, rows.Count);
        Assert.NotEqual(rows[0][4], rows[1][4]);
        Assert.All(rows, r => Assert.Equal("", r[7]));
        Assert.Equal(["Straight bar", "L bar"], rows.Select(r => r[2]).OrderByDescending(x => x).ToList());
    }

    [Fact]
    public void A_distribution_cannot_come_to_a_point_and_its_step_reads_without_a_path()
    {
        var f = F();
        var view = Section(f);
        var run = (string)f.Run("rebar.addRun", new JsonObject
        {
            ["viewId"] = view, ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(1, 0),
            ["zones"] = new JsonArray(new JsonObject { ["length"] = 0.3, ["step"] = 0.1 }, new JsonObject { ["length"] = 0.4, ["step"] = 0.2 }),
        })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("rebar.updateRun", new JsonObject { ["id"] = run, ["end"] = new JsonArray(0, 0) }));
        Assert.NotEmpty(RebarSchedule.Entries(f.Model));
        Assert.Equal("100", Runs.StepText(0, 0.1, [new Runs.Zone(0.3, 0.1), new Runs.Zone(0.4, 0.2)]));
    }

    [Fact]
    public void A_bar_left_out_of_the_schedule_still_stands_in_space()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var top = Beams.Top(f.Model, f.Model.Get(beam)!);
        var along = Section(f);
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(1, top - 0.36), ["params"] = new JsonObject { ["length"] = 4 } })!["id"]!;
        var tubes = (int)f.Session.Query("rebar.bodies", [])!["triangles"]!;
        Assert.True(tubes > 0);
        // Counted elsewhere: not in the schedule, but still in 3D and in every other view.
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["excludeSchedule"] = true });
        Assert.Empty(RebarSchedule.Entries(f.Model));
        Assert.Equal(tubes, (int)f.Session.Query("rebar.bodies", [])!["triangles"]!);
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 2.0 })!["id"]!;
        var drawing = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = across })!;
        Assert.Contains(((JsonArray)drawing["marks"]!), mk => ((string?)mk!["id"])?.StartsWith(bar + ":projection") == true);
    }

    [Fact]
    public void Stirrup_zones_whose_shares_are_all_zero_are_refused()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var type = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id;
        Assert.Throws<CommandException>(() => f.Run("rebar.setTypeStirrups", new JsonObject
        {
            ["id"] = type, ["enabled"] = true, ["mode"] = "zones",
            ["zones"] = new JsonArray(new JsonObject { ["share"] = 0, ["step"] = 0.1 }, new JsonObject { ["share"] = 0, ["step"] = 0.2 }),
        }));
    }
}
