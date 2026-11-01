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
/// A longitudinal bar cut in two with a lap (docs/plan-rebar.md, decision 227): the pieces' lengths, hooks and marks,
/// both in the schedule; a polyline cut on a segment; the refusals; the cut at a click; the stock length warning; the settings.
/// </summary>
public class RebarSplitTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static string Section(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(20, 0) })!["id"]!;

    static string Straight(Fixture f, string view, double length, double d = 0.016, JsonObject? extra = null)
    {
        var a = new JsonObject { ["viewId"] = view, ["shape"] = "straight", ["at"] = new JsonArray(1, 0.5), ["params"] = new JsonObject { ["length"] = length }, ["diameter"] = d };
        if (extra != null) foreach (var (k, v) in extra) a[k] = v?.DeepClone();
        return (string)f.Run("rebar.add", a)!["id"]!;
    }

    [Fact]
    public void A_14_m_bar_split_at_7_m_with_50_diameters_gives_two_pieces_of_7_4_m_the_second_with_a_new_mark_both_in_the_schedule()
    {
        var f = F();
        var view = Section(f);
        var bar = Straight(f, view, 14, 0.016, new JsonObject { ["count"] = 3, ["layer"] = "Custom", ["array"] = new JsonObject { ["nx"] = 2, ["dx"] = 0.1 }, ["hooks"] = new JsonObject { ["start"] = new JsonObject { ["angle"] = 90, ["length"] = 0.2 }, ["end"] = new JsonObject { ["angle"] = 135, ["length"] = 0.16 } } });
        var mark = Rebars.Mark(f.Model.Get(bar)!);
        var r = (JsonObject)f.Run("rebar.split", new JsonObject { ["id"] = bar, ["station"] = 7 })!;
        Assert.Equal(bar, (string?)r["first"]);
        Assert.Equal(0.8, (double)r["lap"]!, 9);
        var second = (string)r["second"]!;
        var e1 = f.Model.Get(bar)!;
        var e2 = f.Model.Get(second)!;
        // The first: 0 → 7.4 m, straight still, its start hook kept, none at the cut.
        Assert.Equal("straight", e1.Str("shape"));
        Assert.Equal(7.4, (double)e1.Attrs["params"]!["length"]!, 9);
        Assert.Equal(90, (double)e1.Attrs["hooks"]!["start"]!["angle"]!, 9);
        Assert.Null(e1.Attrs["hooks"]!["end"]);
        Assert.Equal(mark, Rebars.Mark(e1));
        // The second: 6.6 → 14 m, from the lap's start, the end hook on it, the next mark, drawn beside its lap.
        Assert.Equal("straight", e2.Str("shape"));
        Assert.Equal(7.4, (double)e2.Attrs["params"]!["length"]!, 9);
        Assert.Equal(1 + 6.6, (double)e2.Attrs["at"]![0]!, 9);
        Assert.Equal(0.5, (double)e2.Attrs["at"]![1]!, 9);
        Assert.Equal(135, (double)e2.Attrs["hooks"]!["end"]!["angle"]!, 9);
        Assert.Null(e2.Attrs["hooks"]!["start"]);
        Assert.Equal(mark + 1, Rebars.Mark(e2));
        Assert.Equal(0.016, e2.Num("lapOffset"), 9);
        // Both keep what the original had: count, array, layer, diameter, view.
        foreach (var e in new[] { e1, e2 })
        {
            Assert.Equal(3, Rebars.Count(e));
            Assert.Equal(2, Rebars.Array(e).Nx);
            Assert.Equal("Custom", e.Str("layer"));
            Assert.Equal(0.016, e.Num("diameter"), 9);
            Assert.Equal(view, e.Str("viewId"));
        }
        // The schedule has both marks with their lengths, hooks included (7.4 + 0.2, 7.4 + 0.16), six bars each.
        var entries = RebarSchedule.Entries(f.Model);
        var first = Assert.Single(entries, x => x.Mark == mark);
        var next = Assert.Single(entries, x => x.Mark == mark + 1);
        Assert.Equal(7.6, first.Length, 6);
        Assert.Equal(7.56, next.Length, 6);
        Assert.Equal(6, first.Count);
        Assert.Equal(6, next.Count);
        // The drawing shows both bars, the second one diameter and half a millimetre of paper (1:100 → 0.05 m) off the first.
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
        var marks = (JsonArray)d["marks"]!;
        Assert.Contains(marks, m => (string?)m!["id"] == second);
        var pens = (JsonArray)d["pens"]!;
        var ys = pens.Where(p => (string?)p!["layer"] == "Custom").SelectMany(p => (p!["segments"] as JsonArray)?.Select((v, i) => (Value: (double)v!, Index: i)).Where(x => x.Index % 4 == 1).Select(x => x.Value) ?? []).Select(y => Math.Round(y, 4)).Distinct().OrderBy(y => y).ToList();
        Assert.Contains(0.5, ys);
        Assert.Contains(Math.Round(0.5 + 0.016 + 0.05, 4), ys);
    }

    [Fact]
    public void A_polyline_cut_on_its_second_segment_gives_two_polylines_with_the_right_vertices_and_an_L_bar_two_polylines_too()
    {
        var f = F();
        var view = Section(f);
        // 0→(6,0)→(6,2)→(10,2): 12 m; cut at 7 m on the vertical leg with a 1 m lap: the first ends at (6, 1.5), the second starts at (6, 0.5).
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shape"] = "polyline", ["at"] = new JsonArray(2, 1), ["diameter"] = 0.012, ["vertices"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 2), new JsonArray(10, 2)) })!["id"]!;
        var r = (JsonObject)f.Run("rebar.split", new JsonObject { ["id"] = bar, ["station"] = 7, ["lap"] = 1 })!;
        var e1 = f.Model.Get(bar)!;
        var e2 = f.Model.Get((string)r["second"]!)!;
        static List<(double X, double Y)> V(Entity e) => ((JsonArray)e.Attrs["vertices"]!).Select(v => (Math.Round((double)v![0]!, 6), Math.Round((double)v[1]!, 6))).ToList();
        Assert.Equal("polyline", e1.Str("shape"));
        Assert.Equal([(0, 0), (6, 0), (6, 1.5)], V(e1));
        Assert.Equal("polyline", e2.Str("shape"));
        Assert.Equal([(0, 0), (0, 1.5), (4, 1.5)], V(e2));
        Assert.Equal(2 + 6, (double)e2.Attrs["at"]![0]!, 9);
        Assert.Equal(1 + 0.5, (double)e2.Attrs["at"]![1]!, 9);
        Assert.Equal(7.5, RebarShapes.Length(Rebars.SpecOf(e1)), 9);
        Assert.Equal(5.5, RebarShapes.Length(Rebars.SpecOf(e2)), 9);
        // An L bar (a = 8, b = 1) cut at 4 m: two polylines, the Eurocode radius kept as the polyline's bend radius.
        var l = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shape"] = "L", ["at"] = new JsonArray(0, 3), ["diameter"] = 0.016, ["params"] = new JsonObject { ["a"] = 8, ["b"] = 1 } })!["id"]!;
        var rl = (JsonObject)f.Run("rebar.split", new JsonObject { ["id"] = l, ["station"] = 4 })!;
        var l1 = f.Model.Get(l)!;
        var l2 = f.Model.Get((string)rl["second"]!)!;
        Assert.Equal("polyline", l1.Str("shape"));
        Assert.Equal([(0, 0), (4.4, 0)], V(l1));
        Assert.Equal([(0, 0), (4.4, 0), (4.4, 1)], V(l2));
        Assert.Equal(0.032, (double)l2.Attrs["params"]!["bendRadius"]!, 9);
        Assert.Equal(0.032, RebarShapes.RadiusOf(Rebars.SpecOf(l2)), 9);
        Assert.StartsWith("Polyline bar", l1.Name);
    }

    [Fact]
    public void A_stirrup_a_cut_too_near_the_end_and_one_outside_are_refused()
    {
        var f = F();
        var view = Section(f);
        var stirrup = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shape"] = "stirrup", ["at"] = new JsonArray(0, 0) })!["id"]!;
        var ex = Assert.Throws<CommandException>(() => f.Run("rebar.split", new JsonObject { ["id"] = stirrup, ["station"] = 0.5 }));
        Assert.Equal("Only a longitudinal bar is split.", ex.Message);
        var bar = Straight(f, view, 14);
        ex = Assert.Throws<CommandException>(() => f.Run("rebar.split", new JsonObject { ["id"] = bar, ["station"] = 0.3 }));
        Assert.Equal("The cut is too close to the end.", ex.Message);
        ex = Assert.Throws<CommandException>(() => f.Run("rebar.split", new JsonObject { ["id"] = bar, ["station"] = 13.8 }));
        Assert.Equal("The cut is too close to the end.", ex.Message);
        ex = Assert.Throws<CommandException>(() => f.Run("rebar.split", new JsonObject { ["id"] = bar, ["station"] = 15 }));
        Assert.Equal("The cut is outside the bar.", ex.Message);
        Assert.Throws<CommandException>(() => f.Run("rebar.split", new JsonObject { ["id"] = bar }));
        Assert.Single(f.Model.OfClass(Rebars.Class), e => e.Str("shape") == "straight");
        // The preview says the same without throwing, and the lengths when it can be made.
        var bad = (JsonObject)f.Session.Query("rebar.splitPreview", new JsonObject { ["id"] = bar, ["station"] = 0.3 })!;
        Assert.Equal("The cut is too close to the end.", (string?)bad["error"]);
        var good = (JsonObject)f.Session.Query("rebar.splitPreview", new JsonObject { ["id"] = bar, ["station"] = 5, ["lap"] = 1 })!;
        Assert.Equal(5.5, (double)good["first"]!, 9);
        Assert.Equal(9.5, (double)good["second"]!, 9);
        Assert.Equal(1.0, (double)good["lap"]!, 9);
        Assert.Null(good["error"]);
    }

    [Fact]
    public void A_click_cuts_at_the_nearest_point_of_the_bar_through_its_rotation()
    {
        var f = F();
        var view = Section(f);
        // A 10 m bar from (1, 0.5) turned 90°: it runs up to (1, 10.5); a click at (1.3, 4.5) is 4 m along it.
        var bar = Straight(f, view, 10, 0.012, new JsonObject { ["rotation"] = 90 });
        var r = (JsonObject)f.Run("rebar.split", new JsonObject { ["id"] = bar, ["point"] = new JsonArray(1.3, 4.5) })!;
        Assert.Equal(4.0, (double)r["station"]!, 6);
        Assert.Equal(0.6, (double)r["lap"]!, 9);
        Assert.Equal(4.3, (double)f.Model.Get(bar)!.Attrs["params"]!["length"]!, 9);
        var second = f.Model.Get((string)r["second"]!)!;
        Assert.Equal(1.0, (double)second.Attrs["at"]![0]!, 9);
        Assert.Equal(0.5 + 3.7, (double)second.Attrs["at"]![1]!, 9);
        Assert.Equal(90, second.Num("rotation"), 9);
    }

    [Fact]
    public void Describe_flags_a_bar_longer_than_the_stock_length_and_the_settings_keep_the_stock_and_lap_lengths()
    {
        var f = F();
        var view = Section(f);
        var longBar = Straight(f, view, 14, 0.016);
        var shortBar = Straight(f, view, 8, 0.016);
        var d = (JsonObject)f.Session.Query("rebar.describe", new JsonObject { ["id"] = longBar })!;
        Assert.True((bool)d["overStock"]!);
        Assert.True((bool)d["splittable"]!);
        Assert.Equal(12, (double)d["stockLength"]!, 9);
        Assert.Equal(0.8, (double)d["lap"]!, 9);
        Assert.Null(((JsonObject)f.Session.Query("rebar.describe", new JsonObject { ["id"] = shortBar })!)["overStock"]);
        // The defaults: 12 m stock, 50Ø lap.
        var st = RebarSettings.Of(f.Model);
        Assert.Equal(12, st.StockLength, 9);
        Assert.Null(st.LapLength);
        Assert.Equal(0.6, st.LapOf(0.012), 9);
        // Set to 6 m stock and a lap of 0.9 m: the 8 m bar is over the stock now, and a split takes the new lap.
        f.Run("rebar.setSettings", new JsonObject { ["stockLength"] = 6, ["lapLength"] = 0.9 });
        st = RebarSettings.Of(f.Model);
        Assert.Equal(6, st.StockLength, 9);
        Assert.Equal(0.9, st.LapLength!.Value, 9);
        Assert.Equal(0.9, st.LapOf(0.012), 9);
        var resolved = (JsonObject)f.Session.Query("rebar.settings", new JsonObject())!;
        Assert.Equal(6, (double)resolved["stockLength"]!, 9);
        Assert.Equal(0.9, (double)resolved["lapLength"]!, 9);
        Assert.True((bool)((JsonObject)f.Session.Query("rebar.describe", new JsonObject { ["id"] = shortBar })!)["overStock"]!);
        var r = (JsonObject)f.Run("rebar.split", new JsonObject { ["id"] = shortBar, ["station"] = 4 })!;
        Assert.Equal(0.9, (double)r["lap"]!, 9);
        Assert.Equal(3, f.Model.OfClass(Rebars.Class).Count());
        // Undo takes the split back as one step.
        f.Session.Undo();
        Assert.Equal(8.0, (double)f.Model.Get(shortBar)!.Attrs["params"]!["length"]!, 9);
        Assert.Equal(2, f.Model.OfClass(Rebars.Class).Count());
        // The lap back to 50Ø with null; the stock bounded.
        f.Run("rebar.setSettings", new JsonObject { ["lapLength"] = null });
        Assert.Null(RebarSettings.Of(f.Model).LapLength);
        Assert.Null(((JsonObject)f.Session.Query("rebar.settings", new JsonObject())!)["lapLength"]);
        Assert.Throws<CommandException>(() => f.Run("rebar.setSettings", new JsonObject { ["stockLength"] = 30 }));
    }
}
