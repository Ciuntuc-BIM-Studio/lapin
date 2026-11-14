using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// "Edit sketch" on an outline (forum topic 97, stage 3): a floor's outline opened as a sketch on a plan view of its own, drawn with
/// the drafting commands and constrained with the sketch constraints, then written back in one undo step; the sketch kept on the
/// element (Lapin_Intent) and opened again as it was; the element's id unchanged.
/// </summary>
public sealed class OutlineSketchTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-outline-sketch").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static double Area(Fixture f, string id) => f.Session.Quantities(id)[0].Quantities.Single(x => x.Name == "GrossArea").Value;

    static string Floor(Fixture f) =>
        (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(J(0, 0), J(6, 0), J(6, 4), J(0, 4)) })!["id"]!;

    static (string View, List<string> Ids) Edit(Fixture f, string id)
    {
        var r = f.Run("studio.editOutline", new JsonObject { ["id"] = id })!;
        return ((string)r["viewId"]!, r["ids"]!.AsArray().Select(x => (string)x!).ToList());
    }

    static Shape Of(Fixture f, string id) => Drafts.ShapeOf(f.Model.Get(id)!)!;

    [Fact]
    public void An_outline_opens_as_a_sketch_of_lines_and_arcs_on_a_view_of_its_own_with_the_element_hidden()
    {
        var f = F();
        var slab = Floor(f);
        f.Run("architecture.setEdgeArc", new JsonObject { ["id"] = slab, ["index"] = 2, ["rise"] = -1 });
        var (view, ids) = Edit(f, slab);
        var v = f.Model.Get(view)!;
        Assert.Equal(slab, v.Str(OutlineSketches.OfKey));
        Assert.Contains(slab, v.Attrs[ViewHiding.Key]!.AsArray().Select(x => (string?)x));
        Assert.Equal(4, ids.Count);
        Assert.IsType<Line>(Of(f, ids[0]));
        var arc = Assert.IsType<Arc>(Of(f, ids[2]));
        Assert.Equal(5, arc.R, 9);
        // Finished untouched, the same floor: its outline as it was.
        var before = Area(f, slab);
        f.Run("studio.finishOutline", new JsonObject { ["viewId"] = view });
        Assert.Null(f.Model.Get(view));
        Assert.Empty(Drafts.On(f.Model, view));
        Assert.Equal(before, Area(f, slab), 9);
        Assert.Equal(-1, Slabs.Rises(f.Model.Get(slab)!)![2], 6);
    }

    [Fact]
    public void Drafting_commands_change_the_sketch_and_finishing_writes_it_back_in_one_step_keeping_the_id()
    {
        var f = F();
        var slab = Floor(f);
        var gid = f.Model.Get(slab)!.Id;
        var (view, ids) = Edit(f, slab);
        // A fillet of 1 m on the corner at (6, 0) between the first two lines, as the Fillet command makes it.
        f.Run("drafting.fillet", new JsonObject { ["a"] = ids[0], ["atA"] = J(3, 0), ["b"] = ids[1], ["atB"] = J(6, 2), ["radius"] = 1 });
        f.Run("studio.finishOutline", new JsonObject { ["viewId"] = view });
        Assert.Equal("Finish sketch", f.Session.History.UndoLabel);
        var e = f.Model.Get(slab)!;
        Assert.Equal(gid, e.Id);
        Assert.Equal(5, Slabs.Corners(e).Count);
        Assert.Equal(24 - (1 - Math.PI / 4), Area(f, slab), 6);
        Assert.NotNull(e.Attrs[OutlineSketches.Key]);
        // One undo puts the floor and its sketch view back.
        f.Session.Undo();
        Assert.Equal(4, Slabs.Corners(f.Model.Get(slab)!).Count);
        Assert.NotNull(f.Model.Get(view));
    }

    [Fact]
    public void An_outline_opened_holds_its_corners_so_a_dimension_moves_it_whole_and_a_fillet_keeps_it_closed()
    {
        var f = F();
        var slab = Floor(f);
        var (view, ids) = Edit(f, slab);
        Assert.Equal(4, SketchConstraints.ListOf(SketchConstraints.CarrierOf(f.Model, view)).Count(c => (string?)c["kind"] == "coincident"));
        f.Run("drafting.fillet", new JsonObject { ["a"] = ids[1], ["atA"] = J(6, 2), ["b"] = ids[0], ["atB"] = J(3, 0), ["radius"] = 1.2 });
        // The left edge (from (0, 4) down to (0, 0)) made 6 long: the outline stretches, its corners still together.
        f.Run("studio.addConstraint", new JsonObject { ["viewId"] = view, ["kind"] = "distance", ["refs"] = new JsonArray(new JsonObject { ["shape"] = ids[3], ["part"] = "whole" }), ["value"] = 6 });
        f.Run("studio.finishOutline", new JsonObject { ["viewId"] = view });
        var e = f.Model.Get(slab)!;
        Assert.Equal(5, Slabs.Corners(e).Count);
        Assert.Equal(6, Slabs.Corners(e).Max(p => p.Y) - Slabs.Corners(e).Min(p => p.Y), 6);
    }

    [Fact]
    public void Constraints_drive_the_outline_and_come_back_when_it_is_opened_again()
    {
        var f = F();
        var slab = Floor(f);
        var (view, ids) = Edit(f, slab);
        // The corners kept together, the first edge 8 m long: the floor's width follows the dimension.
        static JsonObject R(string shape, string part) => new() { ["shape"] = shape, ["part"] = part };
        var cs = new JsonArray();
        cs.Add((JsonNode)new JsonObject { ["kind"] = "horizontal", ["refs"] = new JsonArray(R(ids[0], "whole")), ["viewId"] = view });
        cs.Add((JsonNode)new JsonObject { ["kind"] = "vertical", ["refs"] = new JsonArray(R(ids[1], "whole")), ["viewId"] = view });
        cs.Add((JsonNode)new JsonObject { ["kind"] = "horizontal", ["refs"] = new JsonArray(R(ids[2], "whole")), ["viewId"] = view });
        cs.Add((JsonNode)new JsonObject { ["kind"] = "vertical", ["refs"] = new JsonArray(R(ids[3], "whole")), ["viewId"] = view });
        cs.Add((JsonNode)new JsonObject { ["kind"] = "fix", ["refs"] = new JsonArray(R(ids[0], "start")), ["viewId"] = view });
        f.Run("studio.addConstraint", new JsonObject { ["constraints"] = cs });
        var dim = (string)f.Run("studio.addConstraint", new JsonObject { ["viewId"] = view, ["kind"] = "distance", ["refs"] = new JsonArray(R(ids[0], "whole")), ["value"] = 8 })!["id"]!;
        f.Run("studio.finishOutline", new JsonObject { ["viewId"] = view });
        Assert.Equal(32, Area(f, slab), 6);
        var sketch = (JsonObject)f.Model.Get(slab)!.Attrs[OutlineSketches.Key]!;
        Assert.Equal(10, sketch["constraints"]!.AsArray().Count);
        // Opened again: the same shapes and constraints, on new ids; a dimension changed, the floor follows.
        var again = f.Run("studio.editOutline", new JsonObject { ["id"] = slab })!;
        var view2 = (string)again["viewId"]!;
        Assert.Equal(10, (int)again["constraints"]!);
        Assert.False((bool)again["rebuilt"]!);
        var carrier = SketchConstraints.CarrierOf(f.Model, view2)!;
        var d2 = SketchConstraints.ListOf(carrier).Single(c => (string?)c["kind"] == "distance");
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = (string)d2["id"]!, ["value"] = 7 });
        f.Run("studio.finishOutline", new JsonObject { ["viewId"] = view2 });
        Assert.Equal(28, Area(f, slab), 6);
        // Its corners moved by a grip since: opened from its outline as it is, its constraints left out (said so).
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = new JsonArray(J(0, 0), J(9, 0), J(7, 4), J(0, 4)) });
        var rebuilt = f.Run("studio.editOutline", new JsonObject { ["id"] = slab })!;
        Assert.True((bool)rebuilt["rebuilt"]!);
        Assert.Equal(0, (int)rebuilt["constraints"]!);
        Assert.All(SketchConstraints.ListOf(SketchConstraints.CarrierOf(f.Model, (string)rebuilt["viewId"]!)), c => Assert.Equal("coincident", (string?)c["kind"]));
        _ = dim;
    }

    [Fact]
    public void An_open_or_branching_sketch_is_refused_and_cancelling_leaves_the_element_as_it_was()
    {
        var f = F();
        var slab = Floor(f);
        var (view, ids) = Edit(f, slab);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(ids[1]) });
        Assert.Contains("loose end", Assert.Throws<CommandException>(() => f.Run("studio.finishOutline", new JsonObject { ["viewId"] = view })).Message);
        f.Run("studio.cancelOutline", new JsonObject { ["viewId"] = view });
        Assert.Null(f.Model.Get(view));
        Assert.Equal(24, Area(f, slab), 9);
        Assert.Null(f.Model.Get(slab)!.Attrs[OutlineSketches.Key]);
        // A circle alone is a round floor.
        var (v2, ids2) = Edit(f, slab);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray([.. ids2.Select(x => (JsonNode)x)]) });
        f.Run("drafting.draw", new JsonObject { ["viewId"] = v2, ["shape"] = new JsonObject { ["type"] = "circle", ["c"] = J(3, 2), ["r"] = 2 } });
        f.Run("studio.finishOutline", new JsonObject { ["viewId"] = v2 });
        Assert.Equal(Math.PI * 4, Area(f, slab), 6);
        // Opening it twice gives the view already open.
        var (v3, _) = Edit(f, slab);
        Assert.Equal(v3, Edit(f, slab).View);
    }

    [Fact]
    public void Lines_and_arcs_chain_whichever_way_they_run()
    {
        // A semicircle on a square's top, drawn as an arc anticlockwise from the right corner, its lines in no order.
        var shapes = new List<Shape>
        {
            new Line(new Vec2(0, 2), new Vec2(0, 0)),
            new Arc(new Vec2(1, 2), 1, 0, Math.PI),
            new Line(new Vec2(2, 0), new Vec2(0, 0)),
            new Line(new Vec2(2, 0), new Vec2(2, 2)),
        };
        var (corners, rises) = OutlineSketches.Loop(shapes);
        Assert.Equal(4, corners.Count);
        Assert.Equal(4 + Math.PI / 2, Math.Abs(ArcOutline.SignedArea(corners, rises)), 9);
        // Two lines in line make one edge.
        var (c2, _) = OutlineSketches.Loop([new Line(new Vec2(0, 0), new Vec2(1, 0)), new Line(new Vec2(1, 0), new Vec2(2, 0)), new Line(new Vec2(2, 0), new Vec2(1, 1)), new Line(new Vec2(1, 1), new Vec2(0, 0))]);
        Assert.Equal(3, c2.Count);
        Assert.Contains("more than one outline", Assert.Throws<CommandException>(() => OutlineSketches.Loop([
            new Line(new Vec2(0, 0), new Vec2(1, 0)), new Line(new Vec2(1, 0), new Vec2(0, 1)), new Line(new Vec2(0, 1), new Vec2(0, 0)),
            new Line(new Vec2(5, 0), new Vec2(6, 0)), new Line(new Vec2(6, 0), new Vec2(5, 1)), new Line(new Vec2(5, 1), new Vec2(5, 0))])).Message);
    }

    [Fact]
    public void The_sketch_goes_to_ifc_as_intent_and_an_imported_element_is_not_edited()
    {
        var f = F();
        var slab = Floor(f);
        var (view, _) = Edit(f, slab);
        f.Run("studio.finishOutline", new JsonObject { ["viewId"] = view });
        var path = Path.Combine(_dir, "sketch.ifc");
        IfcExport.Write(f.Session, path);
        Assert.Contains("'outlineSketch'", File.ReadAllText(path));
        var g = F();
        IfcImport.Run(g.Session, path, null, out _);
        Assert.NotNull(g.Model.Get(slab)!.Attrs[OutlineSketches.Key]);
        // A floor kept as it came from a foreign file (a reference) is refused.
        var e = f.Model.Get(slab)!;
        Assert.True(OutlineSketches.Editable(e));
        var attrs = e.CloneAttrs();
        attrs["reference"] = true;
        Assert.False(OutlineSketches.Editable(e.With(attrs: attrs)));
    }
}
