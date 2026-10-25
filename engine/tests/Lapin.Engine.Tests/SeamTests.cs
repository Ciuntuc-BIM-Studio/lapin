using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Visual seams across storeys (asked 25.10.2026): where one material's surface
/// goes on from one element into another in the same plane — a facade's render
/// over the storey above — no line is drawn between them, in 3D as in the
/// elevations and sections; the elements stay apart. A different finish above,
/// or a material whose joints are drawn, keeps its line.
/// </summary>
public class SeamTests
{
    static Fixture Full() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());

    /// <summary>A wall on the ground floor and one on a new storey above it, each of its type.</summary>
    static (Fixture F, string Below, string Above, string Storey1) Stacked(Fixture f, string typeBelow, string? typeAbove = null, double x2Above = 6)
    {
        var below = f.Wall(0, 0, 6, 0, typeBelow);
        var s1 = (string)f.Run("storey.create", new JsonObject())!["id"]!;
        var above = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = s1, ["x1"] = 0, ["y1"] = 0, ["x2"] = x2Above, ["y2"] = 0, ["typeId"] = typeAbove ?? typeBelow })!["id"]!;
        return (f, below, above, s1);
    }

    static string Rendered(Fixture f) => f.Type("Rendered", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Render", 0.02, false));

    /// <summary>The part's edge segments lying in the horizontal plane z, as (x0, x1) along the wall.</summary>
    static List<(double X0, double X1)> AlongAtZ(RenderPart p, double z)
    {
        var o = new List<(double, double)>();
        var e = p.Edges;
        for (var i = 0; i + 5 < e.Length; i += 6)
            if (Math.Abs(e[i + 2] - z) < 1e-4 && Math.Abs(e[i + 5] - z) < 1e-4 && Math.Abs(e[i] - e[i + 3]) > 1e-4)
                o.Add((Math.Min(e[i], e[i + 3]), Math.Max(e[i], e[i + 3])));
        return o;
    }

    static RenderItem Item(Diff d, string id) => d.Render.Single(r => r.NodeId == id);
    static RenderPart Part(RenderItem item, string name) => item.Parts!.Single(p => p.Name == name);

    /// <summary>The drawing's segments of one pen layer that run horizontally at <paramref name="y"/>, as (x0, x1).</summary>
    static List<(double X0, double X1)> HorizontalAt(JsonObject d, string layer, double y)
    {
        var o = new List<(double, double)>();
        foreach (var p in d["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string)p["layer"]! == layer))
        {
            if (p["segments"] is not JsonArray a) continue;
            for (var i = 0; i + 3 < a.Count; i += 4)
            {
                double x0 = (double)a[i]!, y0 = (double)a[i + 1]!, x1 = (double)a[i + 2]!, y1 = (double)a[i + 3]!;
                if (Math.Abs(y0 - y) < 1e-6 && Math.Abs(y1 - y) < 1e-6) o.Add((Math.Min(x0, x1), Math.Max(x0, x1)));
            }
        }
        return o;
    }

    static JsonObject View(Fixture f, string kind, JsonObject args)
    {
        args["kind"] = kind;
        var id = (string)f.Run("view.create", args)!["id"]!;
        return (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = id })!;
    }

    [Fact]
    public void One_type_on_two_storeys_shows_no_seam_in_3D_and_the_walls_stay_two_elements()
    {
        var f = Full();
        var (g, below, above, _) = Stacked(f, Rendered(f));
        var snap = g.Session.Snapshot();
        foreach (var id in new[] { below, above })
            foreach (var part in Item(snap, id).Parts!)
                Assert.Empty(AlongAtZ(part, 3));
        // Where nothing goes on, the edges stay: the bottom of the lower wall, the top of the upper.
        Assert.NotEmpty(AlongAtZ(Part(Item(snap, below), "Render"), 0));
        Assert.NotEmpty(AlongAtZ(Part(Item(snap, above), "Render"), 6));
        Assert.Equal(2, g.Model.OfClass(Lapin.Model.IfcClass.Wall).Count());
    }

    [Fact]
    public void A_shorter_wall_above_leaves_the_rest_of_the_edge_below()
    {
        var f = Full();
        var (g, below, _, _) = Stacked(f, Rendered(f), x2Above: 4);
        var render = Part(Item(g.Session.Snapshot(), below), "Render");
        // The outer face's top edge runs on only where the wall above is not: 4 to 6.
        var along = AlongAtZ(render, 3);
        Assert.All(along, s => Assert.True(s.X0 >= 4 - 1e-6, $"an edge from {s.X0}"));
        Assert.Contains(along, s => Math.Abs(s.X0 - 4) < 1e-6 && Math.Abs(s.X1 - 6) < 1e-6);
    }

    [Fact]
    public void A_different_finish_above_keeps_its_edge_in_3D_and_its_line_in_the_elevation_and_the_section()
    {
        var f = Full();
        var stone = f.Type("Stone-faced", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Stone", 0.02, false));
        var (g, below, above, _) = Stacked(f, Rendered(f), stone);
        var snap = g.Session.Snapshot();
        // Plaster and brick go on; the render meets stone.
        Assert.Empty(AlongAtZ(Part(Item(snap, below), "Plaster"), 3));
        Assert.Empty(AlongAtZ(Part(Item(snap, below), "Brick"), 3));
        Assert.NotEmpty(AlongAtZ(Part(Item(snap, below), "Render"), 3));
        Assert.NotEmpty(AlongAtZ(Part(Item(snap, above), "Stone"), 3));
        // The elevation of the render face shows the change of material; the plaster face shows none.
        // Layers run from the right of the drawn direction (south) to its left: render and stone make the north facade.
        var north = View(g, "elevation", new JsonObject { ["direction"] = "north" });
        var south = View(g, "elevation", new JsonObject { ["direction"] = "south" });
        var line = HorizontalAt(north, "SEEN", 3);
        Assert.Single(line);
        Assert.Equal(6, line[0].X1 - line[0].X0, 6);
        Assert.Empty(HorizontalAt(south, "SEEN", 3));
        // Across the wall, the cut shows the storey line only through the finish that changes: 2 cm, not the whole wall.
        var section = View(g, "section", new JsonObject { ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 2) });
        var cut = HorizontalAt(section, "CUT", 3);
        Assert.NotEmpty(cut);
        Assert.All(cut, s => Assert.True(s.X1 - s.X0 <= 0.02 + 1e-6, $"a cut line {s.X1 - s.X0} m long at the storey"));
    }

    [Fact]
    public void One_type_on_two_storeys_shows_no_storey_line_in_the_elevation_nor_in_the_section()
    {
        var f = Full();
        var (g, _, _, _) = Stacked(f, Rendered(f));
        foreach (var dir in new[] { "north", "south" })
            Assert.Empty(HorizontalAt(View(g, "elevation", new JsonObject { ["direction"] = dir }), "SEEN", 3));
        var section = View(g, "section", new JsonObject { ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 2) });
        Assert.Empty(HorizontalAt(section, "CUT", 3));
    }

    [Fact]
    public void Taking_the_wall_above_away_brings_the_edge_below_back_through_the_diff_and_undo_takes_it_again()
    {
        var f = Full();
        var (g, below, above, _) = Stacked(f, Rendered(f));
        g.Session.Snapshot();
        // The lower wall is not edited, yet its look changes: the diff carries it again.
        var gone = g.RunDiff("element.delete", new JsonObject { ["ids"] = new JsonArray(above) });
        Assert.Contains(above, gone.RenderRemoved);
        Assert.NotEmpty(AlongAtZ(Part(Item(gone, below), "Render"), 3));
        var back = g.Session.Undo();
        Assert.Empty(AlongAtZ(Part(Item(back, below), "Render"), 3));
        Assert.Empty(AlongAtZ(Part(Item(back, above), "Render"), 3));
    }

    [Fact]
    public void A_material_whose_joints_are_drawn_keeps_its_seams_as_lines()
    {
        var f = Full();
        var (g, below, above, _) = Stacked(f, Rendered(f));
        var snap = g.Session.Snapshot();
        var render = Part(Item(snap, below), "Render").Material!;
        var d = g.RunDiff("material.update", new JsonObject { ["id"] = render, ["drawJoints"] = true });
        Assert.NotEmpty(AlongAtZ(Part(Item(d, below), "Render"), 3));
        Assert.NotEmpty(AlongAtZ(Part(Item(d, above), "Render"), 3));
        Assert.Empty(AlongAtZ(Part(Item(d, below), "Brick"), 3));
        var north = View(g, "elevation", new JsonObject { ["direction"] = "north" });
        Assert.Single(HorizontalAt(north, "SEEN", 3));
    }
}
