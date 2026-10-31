using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Sections across an element at a station (decision 210) and views inset in another (decision 211): the plane
/// perpendicular to a beam's axis, the window round its section, a column's horizontal cut, the section following its
/// element, and a cross-section laid beside a longitudinal one in the same view.
/// </summary>
public class AcrossSectionTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static double[] Bounds(JsonObject d) => ((JsonArray)d["bounds"]!).Select(x => (double)x!).ToArray();
    static double[] Dir(JsonObject d) => ((JsonArray)d["frame"]!["dir"]!).Select(x => (double)x!).ToArray();

    [Fact]
    public void A_section_across_a_beam_cuts_it_perpendicular_to_its_axis_and_sees_only_the_window_round_it()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        // A column the plane cuts too, standing the storey's full height, and a beam 5 m to the side, cut as well: both outside the window.
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 0 });
        f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 5, ["x2"] = 6, ["y2"] = 5 });
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 2 })!["id"]!;
        var v = f.Model.Get(view)!;
        Assert.Equal(2.0, v.Num("station"), 9);
        Assert.Equal(20, v.Num("scale"), 9);
        Assert.StartsWith("Beam", v.Name);
        Assert.NotNull(v.Attrs["start"]);
        var d = Drawing(f, view);
        Assert.Null(d["problem"]);
        Assert.NotEmpty((JsonArray)d["pens"]!);
        // Looking along the beam, towards its end.
        var dir = Dir(d);
        Assert.Equal(1.0, dir[0], 6);
        Assert.Equal(0.0, dir[2], 6);
        // What is drawn: the beam's 0.25 × 0.4 section, the column clipped to the window (0.5 m over and under the beam), the
        // levels' marks beside; not the column's full height, not the beam 5 m to the side.
        var b = Bounds(d);
        Assert.InRange(b[2] - b[0], 0.25, 0.9);
        Assert.InRange(b[3] - b[1], 0.8, 1.6);
    }

    [Fact]
    public void The_section_follows_its_beam_and_a_station_past_the_end_is_held_within_it()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 9 })!["id"]!;
        Assert.Equal(6.0, f.Model.Get(view)!.Num("station"), 9);
        var before = ((JsonArray)Drawing(f, view)["frame"]!["origin"]!).Select(x => (double)x!).ToArray();
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(beam), ["op"] = "move", ["by"] = new JsonArray(0, 1) });
        var after = ((JsonArray)Drawing(f, view)["frame"]!["origin"]!).Select(x => (double)x!).ToArray();
        Assert.Equal(before[1] + 1, after[1], 6);
        // In its storey's plan the section is a short line across the beam.
        var plan = Drawing(f, "plan:" + f.Storey);
        Assert.Contains((JsonArray)plan["marks"]!, m => (string?)m!["id"] == view);
    }

    [Fact]
    public void A_column_is_cut_by_a_horizontal_plane_at_the_height_asked_and_seen_from_above()
    {
        var f = F();
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3 })!["id"]!;
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = column, ["station"] = 1.5 })!["id"]!;
        var d = Drawing(f, view);
        Assert.Null(d["problem"]);
        Assert.Equal(-1.0, Dir(d)[2], 6);
        // The column's 0.3 × 0.3 cut and the marks beside it.
        var b = Bounds(d);
        Assert.InRange(b[2] - b[0], 0.25, 0.9);
        Assert.InRange(b[3] - b[1], 0.25, 0.9);
    }

    [Fact]
    public void A_view_inset_in_another_draws_the_source_at_its_point_and_Section_here_makes_both_at_a_click()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 2 })!["id"]!;
        // A longitudinal section 2 m in front of the beam, looking at it.
        var host = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, -2), ["end"] = new JsonArray(7, -2), ["scale"] = 50 })!["id"]!;
        var inset = (string)f.Run("view.inset", new JsonObject { ["viewId"] = host, ["source"] = across, ["at"] = new JsonArray(9, 2) })!["id"]!;
        var d = Drawing(f, host);
        var mark = ((JsonArray)d["marks"]!).Single(m => (string?)m!["id"] == inset)!;
        var segs = ((JsonArray)mark["segments"]!).Select(x => (double)x!).ToArray();
        Assert.True(segs.Length >= 8);
        // Everything of it lies round (9, 2): the source at the host's scale is 2.5 times its size, so a few metres each way, no more.
        for (var i = 0; i < segs.Length; i += 2) { Assert.InRange(segs[i], 5.0, 13.0); Assert.InRange(segs[i + 1], -2.0, 6.0); }
        Assert.Equal(across, (string?)mark["data"]!["source"]);
        // Section here: the click at host x = 3 is model x = 2, so the station is 2; the inset stands on the host.
        var r = (JsonObject)f.Run("rebar.sectionHere", new JsonObject { ["viewId"] = host, ["id"] = beam, ["point"] = new JsonArray(3, 2.8) })!;
        var made = f.Model.Get((string)r["viewId"]!)!;
        Assert.Equal(beam, made.Str("across"));
        Assert.Equal(2.0, made.Num("station"), 6);
        var ins = f.Model.Get((string)r["insetId"]!)!;
        Assert.Equal(host, ins.Str("viewId"));
        Assert.Equal(made.Id, ins.Str("source"));
        // The inset moves with the Modify tools, as any annotation with a point.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(inset), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        Assert.Equal(10.0, (double)f.Model.Get(inset)!.Attrs["at"]![0]!, 9);
    }
}
