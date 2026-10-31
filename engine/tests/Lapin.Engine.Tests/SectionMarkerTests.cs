using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Where the sections' markers show (docs/sections.md, decision 223): in the plans and in every other section or
/// elevation their plane crosses, never in themselves; hidden per view by section or by layer; a section across an
/// element moved along it and widened at either end; a click inside a cut taking the element.
/// </summary>
public sealed class SectionMarkerTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonObject? Mark(JsonObject d, string id) => ((JsonArray)d["marks"]!).Select(m => (JsonObject)m!).FirstOrDefault(m => (string?)m["id"] == id);
    static double[] Segments(JsonObject mark) => ((JsonArray)mark["segments"]!).Select(x => (double)x!).ToArray();
    static string Section(Fixture f, string name, double x0, double y0, double x1, double y1) =>
        (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["name"] = name, ["start"] = new JsonArray(x0, y0), ["end"] = new JsonArray(x1, y1) })!["id"]!;

    static (string A, string B) House(Fixture f)
    {
        f.Wall(0, 0, 10, 0); f.Wall(10, 0, 10, 8); f.Wall(10, 8, 0, 8); f.Wall(0, 8, 0, 0);
        // A-A up through the house looking west; B-B across it looking north.
        return (Section(f, "A-A", 6, -2, 6, 10), Section(f, "B-B", -2, 5, 14, 5));
    }

    [Fact]
    public void A_section_is_marked_in_the_sections_and_elevations_its_plane_crosses_and_never_in_itself()
    {
        var f = F();
        var (a, b) = House(f);
        var elevation = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        // In B-B, A-A's plane cuts at x = 8 (B-B's line starts at x = −2): a vertical trace, the name beside it.
        var inB = Drawing(f, b);
        var trace = Mark(inB, a);
        Assert.NotNull(trace);
        var segs = Segments(trace!);
        var vertical = Enumerable.Range(0, segs.Length / 4).Select(k => (X0: segs[4 * k], Y0: segs[4 * k + 1], X1: segs[4 * k + 2], Y1: segs[4 * k + 3])).Where(s => Math.Abs(s.X0 - s.X1) < 1e-6 && Math.Abs(s.Y1 - s.Y0) > 1).ToList();
        Assert.Single(vertical);
        Assert.Equal(8.0, vertical[0].X0, 6);
        Assert.Null(Mark(inB, b));
        Assert.Contains(((JsonArray)inB["pens"]!).SelectMany(p => (JsonArray?)p!["texts"] ?? []), t => (string?)t!["text"] == "A-A");
        // In A-A, B-B's plane cuts at x = 7 (A-A's line starts at y = −2).
        var inA = Drawing(f, a);
        Assert.Null(Mark(inA, a));
        var segsB = Segments(Mark(inA, b)!);
        Assert.Contains(Enumerable.Range(0, segsB.Length / 4), k => Math.Abs(segsB[4 * k] - 7) < 1e-6 && Math.Abs(segsB[4 * k + 2] - 7) < 1e-6);
        // The south elevation sees A-A's plane (across it) and not B-B's (parallel to it).
        var inE = Drawing(f, elevation);
        Assert.NotNull(Mark(inE, a));
        Assert.Null(Mark(inE, b));
        // The plan marks both, as before.
        var plan = Drawing(f, "plan:" + f.Storey);
        Assert.NotNull(Mark(plan, a));
        Assert.NotNull(Mark(plan, b));
    }

    [Fact]
    public void A_view_hides_a_marker_by_section_or_by_layer_and_the_query_counts_where_each_is_hidden()
    {
        var f = F();
        var (a, b) = House(f);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("view.showSection", new JsonObject { ["viewId"] = b, ["sectionId"] = a, ["shown"] = false });
        Assert.Null(Mark(Drawing(f, b), a));
        f.Run("view.showSectionLayer", new JsonObject { ["viewId"] = plan, ["layer"] = SectionMarkers.DefaultLayer, ["shown"] = false });
        Assert.Null(Mark(Drawing(f, plan), a));
        Assert.Null(Mark(Drawing(f, plan), b));
        // A-A moved to a layer of its own shows again in the plan; B-B stays on the hidden layer.
        f.Run("element.update", new JsonObject { ["id"] = a, ["attrs"] = new JsonObject { ["markerLayer"] = "Details" } });
        Assert.NotNull(Mark(Drawing(f, plan), a));
        Assert.Null(Mark(Drawing(f, plan), b));
        var q = (JsonObject)f.Session.Query("view.sectionMarkers", new JsonObject { ["viewId"] = plan })!;
        var sections = ((JsonArray)q["sections"]!).Select(s => (JsonObject)s!).ToDictionary(s => (string)s["id"]!);
        Assert.Equal("Details", (string?)sections[a]["layer"]);
        Assert.True((bool)sections[a]["shown"]!);
        Assert.Equal(1, (int)sections[a]["hiddenIn"]!);
        Assert.Equal(SectionMarkers.DefaultLayer, (string?)sections[b]["layer"]);
        Assert.False((bool)sections[b]["shown"]!);
        Assert.Equal(1, (int)sections[b]["hiddenIn"]!);
        var layers = ((JsonArray)q["layers"]!).Select(l => (JsonObject)l!).ToDictionary(l => (string)l["name"]!, l => (bool)l["shown"]!);
        Assert.False(layers[SectionMarkers.DefaultLayer]);
        Assert.True(layers["Details"]);
        // Shown again, the view keeps nothing of it; undo takes the hiding back.
        f.Run("view.showSectionLayer", new JsonObject { ["viewId"] = plan, ["layer"] = SectionMarkers.DefaultLayer, ["shown"] = true });
        Assert.Null(f.Model.Get(plan)!.Attrs[SectionMarkers.Key]);
        f.Session.Undo();
        Assert.Null(Mark(Drawing(f, plan), b));
        Assert.Throws<CommandException>(() => f.Run("view.showSection", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["sectionId"] = a, ["shown"] = false }));
    }

    [Fact]
    public void A_section_across_a_beam_moves_along_it_and_widens_at_either_end()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 2 })!["id"]!;
        var name = f.Model.Get(view)!.Name;
        Assert.EndsWith("2.00 m", name);
        f.Run("view.updateAcross", new JsonObject { ["id"] = view, ["station"] = 4, ["marginA"] = 1, ["marginB"] = 0.25 });
        var v = f.Model.Get(view)!;
        Assert.Equal(4.0, v.Num("station"), 9);
        Assert.EndsWith("4.00 m", v.Name);
        // The line across the 0.25 m beam at x = 4: 1 m past its left face (the line's start), 0.25 m past its right.
        Assert.Equal(4.0, (double)v.Attrs["start"]![0]!, 6);
        Assert.Equal(1.125, (double)v.Attrs["start"]![1]!, 6);
        Assert.Equal(-0.375, (double)v.Attrs["end"]![1]!, 6);
        var window = Views.Render(f.Session.Reader, v).Window!.Value;
        Assert.Equal(1.5, window.Max.X - window.Min.X, 6);
        // The station is held within the beam; a margin taken away falls back to the margin.
        f.Run("view.updateAcross", new JsonObject { ["id"] = view, ["station"] = 9, ["marginB"] = null });
        v = f.Model.Get(view)!;
        Assert.Equal(6.0, v.Num("station"), 9);
        Assert.Null(v.Attrs["marginB"]);
        Assert.Equal(-0.625, (double)v.Attrs["end"]![1]!, 6);
        // A name of the user's own stays.
        f.Run("element.rename", new JsonObject { ["id"] = view, ["name"] = "Mid-span" });
        f.Run("view.updateAcross", new JsonObject { ["id"] = view, ["station"] = 3 });
        Assert.Equal("Mid-span", f.Model.Get(view)!.Name);
        f.Session.Undo(); f.Session.Undo(); f.Session.Undo();
        Assert.Equal(4.0, f.Model.Get(view)!.Num("station"), 9);
    }

    [Fact]
    public void A_section_picks_an_element_by_its_cut_loops_and_a_section_here_from_it_finds_the_station()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3 })!["id"]!;
        // A section along the beam's axis cuts it lengthwise: its mark carries the cut loop, so a click inside it takes the beam.
        var along = Section(f, "L", -1, 0, 7, 0);
        var mark = Mark(Drawing(f, along), beam);
        Assert.NotNull(mark);
        var loops = (JsonArray)mark!["data"]!["loops"]!;
        Assert.NotEmpty(loops);
        Assert.True(((JsonArray)loops[0]!).Count >= 8);
        // Section here from a section in front of the column: the click's height is the station of the horizontal cut.
        var front = Section(f, "F", 0, 1, 4, 1);
        var z = f.Model.Get(f.Storey)!.Num("elevation") + 1.5;
        var r = (JsonObject)f.Run("rebar.sectionHere", new JsonObject { ["viewId"] = front, ["id"] = column, ["point"] = new JsonArray(2, z), ["inset"] = false })!;
        var made = f.Model.Get((string)r["viewId"]!)!;
        Assert.Equal(column, made.Str("across"));
        Assert.Equal(1.5, made.Num("station"), 6);
        Assert.Null(Drawing(f, made.Id)["problem"]);
    }
}
