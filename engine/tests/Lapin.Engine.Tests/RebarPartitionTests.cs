using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Rebar partitions as Revit's (a tester's idea, decision 483): a text on a bar that numbers it apart from the others, lists
/// it in the schedule under its own heading (or alone, the schedule asking for one), and that a view filter reads to show or
/// hide it; a type's stirrups and column bars have a partition each.
/// </summary>
public class RebarPartitionTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static string Section(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(6, 0) })!["id"]!;

    static string Bar(Fixture f, string view, string? partition, double length = 2) =>
        (string)f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shape"] = "straight", ["at"] = new JsonArray(1, 1), ["params"] = new JsonObject { ["length"] = length }, ["partition"] = partition })!["id"]!;

    [Fact]
    public void Each_partition_numbers_its_bars_from_one()
    {
        var f = F();
        var view = Section(f);
        var b1 = Bar(f, view, "Beams");
        var b2 = Bar(f, view, "Beams", 3);
        var w1 = Bar(f, view, "Walls");
        var n1 = Bar(f, view, null);
        Assert.Equal([1, 2, 1, 1], new[] { b1, b2, w1, n1 }.Select(id => Rebars.Mark(f.Model.Get(id)!)));
        Assert.Equal("Beams", Rebars.Partition(f.Model.Get(b1)));
        Assert.Equal("", Rebars.Partition(f.Model.Get(n1)));
        var run = (string)f.Run("rebar.addRun", new JsonObject { ["viewId"] = view, ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(1, 0), ["partition"] = " Walls " })!["id"]!;
        Assert.Equal(2, Rebars.Mark(f.Model.Get(run)!));
        Assert.Equal("Walls", Rebars.Partition(f.Model.Get(run)));
        // Moved to no partition through the properties: the field cleared.
        f.Run("element.update", new JsonObject { ["id"] = w1, ["attrs"] = new JsonObject { ["partition"] = "" } });
        Assert.Equal("", Rebars.Partition(f.Model.Get(w1)));
        Assert.Equal(["", "Beams", "Walls"], ((JsonArray)f.Session.Query("rebar.partitions", new JsonObject())!).Select(x => (string)x!));
    }

    [Fact]
    public void The_schedule_lists_each_partition_apart_or_only_those_it_asks_for()
    {
        var f = F();
        var view = Section(f);
        Bar(f, view, "Beams");
        Bar(f, view, "Walls", 3);
        var source = new RebarSchedule();
        var all = source.Grid(f.Session.Reader);
        Assert.Equal("Partition", all.Columns[0]);
        // Mark 1 twice, each in its partition, and each partition's mass after its rows.
        Assert.Contains(all.Rows, r => r[0] == "Beams" && r[1] == "1" && r[5] == "2.00");
        Assert.Contains(all.Rows, r => r[0] == "Walls" && r[1] == "1" && r[5] == "3.00");
        Assert.Equal(2, all.Rows.Count(r => r[3] == "Total" && r[0] != ""));
        Assert.DoesNotContain(all.Rows, r => r[8] != "");
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "rebar" })!["id"]!;
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["partitions"] = new JsonArray("Walls") });
        var walls = source.Grid(f.Session.Reader, f.Model.Get(id)!);
        Assert.DoesNotContain(walls.Rows, r => r[0] == "Beams");
        Assert.Contains(walls.Rows, r => r[0] == "Walls" && r[1] == "1");
        // Without partitions the schedule is as it was: no column for them.
        var g = F();
        Bar(g, Section(g), null);
        Assert.Equal("Mark", new RebarSchedule().Grid(g.Session.Reader).Columns[0]);
    }

    [Fact]
    public void A_view_filter_on_the_partition_hides_its_bars()
    {
        var f = F();
        var view = Section(f);
        var beam = Bar(f, view, "Beam stirrups");
        var wall = Bar(f, view, "Wall bars");
        var filter = (string)f.Run("viewFilter.create", new JsonObject
        {
            ["name"] = "Beam stirrups",
            ["rules"] = new JsonArray(new JsonObject { ["field"] = "param", ["name"] = "Partition", ["op"] = "equals", ["value"] = "Beam stirrups" }),
        })!["id"]!;
        f.Run("view.graphics", new JsonObject { ["id"] = view, ["filters"] = new JsonArray(new JsonObject { ["id"] = filter, ["hidden"] = true }) });
        var marks = ((JsonArray)((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!)["marks"]!).Select(m => (string?)m!["id"]).ToList();
        Assert.DoesNotContain(beam, marks);
        Assert.Contains(wall, marks);
    }

    [Fact]
    public void A_types_stirrups_and_column_bars_take_a_partition_each()
    {
        var f = F();
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3, ["typeId"] = type });
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["enabled"] = true, ["partition"] = "Column stirrups" });
        f.Run("rebar.setColumnBars", new JsonObject { ["id"] = type, ["enabled"] = true, ["partition"] = "Column bars" });
        var t = f.Model.Get(type)!;
        Assert.Equal(1, StirrupSpec.Of(t)!.Mark);
        Assert.Equal(1, ColumnBarsSpec.Of(t)!.Mark);
        var entries = RebarSchedule.Entries(f.Model);
        Assert.Contains(entries, x => x.Partition == "Column stirrups" && x.Shape == "Stirrup");
        Assert.Contains(entries, x => x.Partition == "Column bars" && x.Shape != "Stirrup");
        // A bar drawn in the stirrups' partition takes the next mark there.
        Assert.Equal(2, Rebars.Mark(f.Model.Get(Bar(f, Section(f), "Column stirrups"))!));
        // Moved to another partition, the stirrups are numbered again in it.
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["partition"] = "Column bars" });
        Assert.Equal(2, StirrupSpec.Of(f.Model.Get(type)!)!.Mark);
    }

    [Fact]
    public void A_split_bar_s_second_piece_stays_in_its_partition()
    {
        var f = F();
        var view = Section(f);
        Bar(f, view, "Slabs");
        var bar = Bar(f, view, "Slabs", 30);
        Bar(f, view, null);
        f.Run("rebar.split", new JsonObject { ["id"] = bar, ["from"] = 6.5, ["to"] = 7.5 });
        var slabs = f.Model.OfClass(Rebars.Class).Where(e => Rebars.Partition(e) == "Slabs").Select(Rebars.Mark).Order().ToList();
        Assert.Equal(3, slabs.Count);
        Assert.Equal([1, 2, 3], slabs);
    }
}
