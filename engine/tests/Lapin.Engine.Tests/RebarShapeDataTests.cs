using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Bar shapes as data, the bending schedule by the standard and BVBS (docs/plan-user-extensibility.md §6.3–6.4): a shape code of the
/// user's (its legs' lengths parameters or formulas, its turns), bars of it that keep their definition, a change of it that its bars
/// follow; the schedule's A, B, C; the BF2D record and its checksum as BVBS's guideline (version 3.1) computes it.
/// </summary>
public sealed class RebarShapeDataTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new RebarModule());
    static string Section(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(5, 0), ["scale"] = 20 })!["id"]!;

    static JsonObject Shape21() => new()
    {
        ["code"] = "21", ["label"] = "U bar", ["standard"] = "BS 8666",
        ["params"] = new JsonArray(new JsonObject { ["key"] = "A", ["default"] = 0.3 }, new JsonObject { ["key"] = "B", ["default"] = 0.5 }, new JsonObject { ["key"] = "C", ["default"] = 0.3 }),
        ["legs"] = new JsonArray(new JsonObject { ["length"] = "A", ["turn"] = "-90" }, new JsonObject { ["length"] = "B", ["turn"] = "-90" }, new JsonObject { ["length"] = "C" }),
    };

    [Fact]
    public void The_checksum_is_the_guideline_s()
    {
        // The guideline's own example: „abcde@C” → 78.
        Assert.Equal(78, RebarBvbs.Checksum("abcde@C"));
        // And its position 8 (its project's name broken over a line, as the document prints it): C83.
        Assert.Equal(83, RebarBvbs.Checksum("BF2D@HjZEICON\nBewehrungslis@rZEICON@i1@p8@l3604@n14@e4.361098@d14@gBSt500S@s56@v@Gl1095@w-45@l1414@w45@l1095@w0@C"));
    }

    [Fact]
    public void A_shape_of_the_user_s_draws_bars_that_keep_it_and_follow_its_change()
    {
        var f = F();
        var shape = (string)f.Run("rebar.createShape", new JsonObject { ["def"] = Shape21() })!["id"]!;
        var view = Section(f);
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shapeId"] = shape, ["at"] = new JsonArray(0, 1), ["diameter"] = 0.012, ["params"] = new JsonObject { ["B"] = 0.6 } })!["id"]!;
        var e = f.Model.Get(bar)!;
        Assert.StartsWith(RebarShapeData.Prefix, e.Str("shape"));
        Assert.Equal(shape, e.Str("shapeId"));
        Assert.NotNull(e.Attrs["shapeDef"]);
        var s = Rebars.SpecOf(e);
        Assert.Equal(0.3 + 0.6 + 0.3, RebarShapes.Length(s), 9);
        // The schedule by the standard: its code and its A, B, C.
        var sched = f.Session.Query("rebar.bendingSchedule", [])!;
        var row = sched["rows"]![0]!.AsArray();
        Assert.Equal("BS 8666 21", (string)row[2]!);
        Assert.Equal([300, 600, 300], new[] { (double)row[3]!, (double)row[4]!, (double)row[5]! });

        // The shape changed (its middle leg a formula of the others): its bar takes it.
        var changed = Shape21();
        ((JsonObject)changed["legs"]![1]!)["length"] = "B + A";
        f.Run("rebar.updateShape", new JsonObject { ["id"] = shape, ["def"] = changed });
        var after = Rebars.SpecOf(f.Model.Get(bar)!);
        Assert.Equal(0.3 + 0.9 + 0.3, RebarShapes.Length(after), 9);
        // Deleted from the library, the bar keeps its own copy.
        f.Run("rebar.deleteShape", new JsonObject { ["id"] = shape });
        Assert.Equal(0.3 + 0.9 + 0.3, RebarShapes.Length(Rebars.SpecOf(f.Model.Get(bar)!)), 9);

        // A library file carries the shapes.
        var g = F();
        var id2 = (string)g.Run("rebar.createShape", new JsonObject { ["def"] = Shape21() })!["id"]!;
        var text = (string)g.Session.Query("rebar.exportShapes", [])!["text"]!;
        Assert.Equal(1, (int)f.Run("rebar.importShapes", new JsonObject { ["text"] = text })!["added"]!);
        Assert.Equal(0, (int)g.Run("rebar.importShapes", new JsonObject { ["text"] = text })!["added"]!);
        Assert.NotNull(id2);
    }

    [Fact]
    public void A_shape_that_does_not_read_is_refused_with_why()
    {
        var f = F();
        JsonObject Bad(Action<JsonObject> change) { var d = Shape21(); change(d); return d; }
        Assert.Throws<CommandException>(() => f.Run("rebar.createShape", new JsonObject { ["def"] = Bad(d => d.Remove("code")) }));
        Assert.Throws<CommandException>(() => f.Run("rebar.createShape", new JsonObject { ["def"] = Bad(d => ((JsonObject)d["legs"]![0]!)["length"] = "Z * 2") }));
        Assert.Throws<CommandException>(() => f.Run("rebar.createShape", new JsonObject { ["def"] = Bad(d => ((JsonObject)d["legs"]![0]!)["length"] = "A - 1") }));
        Assert.Throws<CommandException>(() => f.Run("rebar.createShape", new JsonObject { ["def"] = Bad(d => ((JsonObject)d["legs"]![0]!)["turn"] = "270") }));
    }

    [Fact]
    public void A_mark_is_one_BF2D_record_with_its_outside_legs_bends_and_checksum()
    {
        var f = F();
        f.Run("project.setInfo", new JsonObject { ["name"] = "Casa P" });
        var view = Section(f);
        f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shape"] = "L", ["at"] = new JsonArray(0, 0), ["diameter"] = 0.012, ["count"] = 4, ["params"] = new JsonObject { ["a"] = 1.0, ["b"] = 0.4 } });
        var r = f.Session.Query("rebar.exportBvbs", new JsonObject { ["index"] = "A" })!;
        var text = (string)r["text"]!;
        Assert.EndsWith("\r\n", text);
        var record = text.TrimEnd('\r', '\n');
        Assert.StartsWith("BF2D@HjCasa P@r", record);
        Assert.Contains("@iA@p1@l1400@n4@e1.243@d12@gB500C@s48@v@Gl1006@w90@l406@w0@C", record);
        var at = record.LastIndexOf("@C", StringComparison.Ordinal) + 2;
        Assert.Equal(RebarBvbs.Checksum(record[..at]).ToString(System.Globalization.CultureInfo.InvariantCulture) + "@", record[at..]);
        Assert.Equal(1, (int)r["records"]!);
    }
}
