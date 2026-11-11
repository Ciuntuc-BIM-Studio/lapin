using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>CAD drafting on a view: shapes on layers, their edits against each other and the model's drawing, hatches, layers, undo.</summary>
public class DraftTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static JsonObject Line(double x1, double y1, double x2, double y2) => new() { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) };

    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static List<string> Draw(Fixture f, string view, params JsonObject[] shapes) =>
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(shapes) })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
    static Shape S(Fixture f, string id) => Drafts.ShapeOf(f.Model.Get(id)!)!;
    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;

    [Fact]
    public void Shapes_draw_on_their_layer_with_its_pen_and_offer_their_exact_shapes_for_snapping()
    {
        var f = F();
        var plan = Plan(f);
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "A-DETAIL", ["color"] = "#c0392b", ["weight"] = 0.35, ["lineType"] = "dashed" });
        var ids = f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["layer"] = "A-DETAIL", ["shapes"] = new JsonArray(Line(0, 0, 4, 0), new JsonObject { ["type"] = "circle", ["c"] = J(2, 2), ["r"] = 1 }) })!["ids"]!.AsArray();
        Assert.Equal(2, ids.Count);
        var d = Drawing(f, plan);
        var pen = d["pens"]!.AsArray().OfType<JsonObject>().Single(p => (string)p["layer"]! == "A-DETAIL");
        Assert.Equal("#c0392b", (string)pen["stroke"]!);
        Assert.Equal(0.35, (double)pen["width"]!);
        Assert.Equal(2, pen["dash"]!.AsArray().Count);
        var marks = d["marks"]!.AsArray().OfType<JsonObject>().Where(m => m["shapes"] != null).ToList();
        Assert.Contains(marks, m => (string)m["shapes"]![0]!["type"]! == "circle");
        // Hidden in a view, or the layer switched off: not drawn.
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "A-DETAIL", ["visible"] = false });
        Assert.DoesNotContain(Drawing(f, plan)["pens"]!.AsArray().OfType<JsonObject>(), p => (string)p["layer"]! == "A-DETAIL");
        // A locked layer refuses edits; layer 0 stays.
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "A-DETAIL", ["visible"] = true, ["locked"] = true });
        Assert.Throws<CommandException>(() => f.Run("drafting.transform", new JsonObject { ["ids"] = ids.DeepClone(), ["op"] = "move", ["by"] = J(1, 0) }));
        Assert.Throws<CommandException>(() => f.Run("drafting.removeLayer", new JsonObject { ["name"] = "0" }));
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "A-DETAIL", ["locked"] = false, ["newName"] = "A-DET" });
        Assert.Equal("A-DET", f.Model.Get((string)ids[0]!)!.Str("layer"));
        f.Run("drafting.removeLayer", new JsonObject { ["name"] = "A-DET" });
        Assert.Equal("0", f.Model.Get((string)ids[0]!)!.Str("layer"));
    }

    [Fact]
    public void Trim_and_extend_work_against_other_shapes_and_the_walls_the_view_cuts()
    {
        var f = F();
        f.Wall(5, -3, 5, 3); // 250 thick by default: its faces at x = 4.875 and 5.125 or so
        var plan = Plan(f);
        var (line, _) = (Draw(f, plan, Line(0, 0, 10, 0))[0], 0);
        // Picking inside the wall removes the piece between its two faces.
        // The wall's layers cut it too: the piece picked goes, the others stay as pieces.
        var r = f.Run("drafting.trim", new JsonObject { ["id"] = line, ["at"] = J(5, 0) })!["ids"]!.AsArray();
        Assert.True(r.Count >= 2);
        var first = (Lapin.Drafting.Cad.Line)S(f, line);
        Assert.Equal(0, first.A.X, 9);
        Assert.True(first.B.X is > 4.5 and < 5);
        // A short line extends to the wall's face.
        var shortLine = Draw(f, plan, Line(0, 1, 2, 1))[0];
        f.Run("drafting.extend", new JsonObject { ["id"] = shortLine, ["at"] = J(1.9, 1) });
        Assert.Equal(first.B.X, ((Lapin.Drafting.Cad.Line)S(f, shortLine)).B.X, 9);
        Assert.Throws<CommandException>(() => f.Run("drafting.extend", new JsonObject { ["id"] = shortLine, ["at"] = J(0.1, 1) }));
        // One undo per edit.
        f.Session.Undo();
        Assert.Equal(2, ((Lapin.Drafting.Cad.Line)S(f, shortLine)).B.X, 9);
    }

    [Fact]
    public void Offset_copy_rotate_mirror_and_fillet()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, plan, Line(0, 0, 10, 0), Line(5, -5, 5, 5));
        var off = (string)f.Run("drafting.offset", new JsonObject { ["id"] = ids[0], ["distance"] = 0.5, ["side"] = J(2, 3) })!["id"]!;
        Assert.Equal(0.5, ((Lapin.Drafting.Cad.Line)S(f, off)).A.Y, 9);
        var copies = f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(off), ["op"] = "copy", ["by"] = J(0, 1) })!["ids"]!.AsArray();
        Assert.Equal(1.5, ((Lapin.Drafting.Cad.Line)S(f, (string)copies[0]!)).A.Y, 9);
        f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(off), ["op"] = "rotate", ["center"] = J(0, 0), ["angle"] = 90 });
        var rot = (Lapin.Drafting.Cad.Line)S(f, off);
        Assert.Equal(-0.5, rot.A.X, 9);
        Assert.Equal(10, rot.B.Y, 9);
        f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(off), ["op"] = "mirror", ["a"] = J(0, 0), ["b"] = J(0, 1) });
        Assert.Equal(0.5, ((Lapin.Drafting.Cad.Line)S(f, off)).A.X, 9);
        var arc = (string)f.Run("drafting.fillet", new JsonObject { ["a"] = ids[0], ["atA"] = J(1, 0), ["b"] = ids[1], ["atB"] = J(5, 4), ["radius"] = 1 })!["id"]!;
        var a = (Arc)S(f, arc);
        Assert.Equal(4, a.C.X, 9);
        Assert.Equal(1, a.C.Y, 9);
        Assert.Equal(4, ((Lapin.Drafting.Cad.Line)S(f, ids[0])).B.X, 9);
    }

    [Fact]
    public void Offset_through_a_point_takes_its_distance_from_the_point()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, plan, Line(0, 0, 10, 0));
        // Through (2, 0.75): the copy lies 0.75 off the line, on that side; a point past its end still measures square to it.
        var off = (string)f.Run("drafting.offset", new JsonObject { ["id"] = ids[0], ["through"] = true, ["side"] = J(2, 0.75) })!["id"]!;
        Assert.Equal(0.75, ((Lapin.Drafting.Cad.Line)S(f, off)).A.Y, 9);
        var past = (string)f.Run("drafting.offset", new JsonObject { ["id"] = ids[0], ["through"] = true, ["side"] = J(14, -2) })!["id"]!;
        Assert.Equal(-2, ((Lapin.Drafting.Cad.Line)S(f, past)).B.Y, 9);
        // A point on the shape gives nothing to offset by.
        Assert.Throws<CommandException>(() => f.Run("drafting.offset", new JsonObject { ["id"] = ids[0], ["through"] = true, ["side"] = J(3, 0) }));
        // One undo step each.
        f.Session.Undo();
        Assert.Null(f.Model.Get(past));
    }

    [Fact]
    public void A_hatch_fills_the_smallest_outline_around_its_point_leaving_its_islands_out()
    {
        var f = F();
        var plan = Plan(f);
        Draw(f, plan,
            new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(10, 0), J(10, 10), J(0, 10)), ["closed"] = true },
            new JsonObject { ["type"] = "circle", ["c"] = J(5, 5), ["r"] = 2 });
        var h = f.Run("drafting.hatch", new JsonObject { ["viewId"] = plan, ["at"] = J(1, 1), ["pattern"] = "cross" })!;
        Assert.Equal(2, (int)h["loops"]!);
        var inside = f.Run("drafting.hatch", new JsonObject { ["viewId"] = plan, ["at"] = J(5, 5), ["pattern"] = "SOLID", ["color"] = "#cccccc" })!;
        Assert.Equal(1, (int)inside["loops"]!);
        var pens = Drawing(f, plan)["pens"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(pens, p => (string?)p["fill"] == "#cccccc");
        Assert.Throws<CommandException>(() => f.Run("drafting.hatch", new JsonObject { ["viewId"] = plan, ["at"] = J(50, 50) }));
        Assert.Throws<CommandException>(() => f.Run("drafting.hatch", new JsonObject { ["viewId"] = plan, ["at"] = J(1, 1), ["pattern"] = "NOPE" }));
    }

    [Fact]
    public void Explode_splits_polylines_and_rectangles_into_lines_on_the_same_layer_in_one_undo_step_and_leaves_the_rest()
    {
        // Report #121, decision 430: JOIN welds lines into a polyline, EXPLODE is the way back.
        var f = F();
        var plan = Plan(f);
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "A-DET", ["color"] = "#c0392b", ["weight"] = 0.35 });
        var ids = f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = plan, ["layer"] = "A-DET",
            ["shapes"] = new JsonArray(
                new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(4, 0), J(4, 3), J(0, 3)), ["closed"] = true },
                new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(10, 0), J(12, 0), J(12, 2)), ["closed"] = false },
                new JsonObject { ["type"] = "circle", ["c"] = J(20, 0), ["r"] = 1 }),
        })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        var hatch = (string)f.Run("drafting.hatch", new JsonObject { ["viewId"] = plan, ["at"] = J(1, 1), ["pattern"] = "SOLID" })!["id"]!;
        var rect = ids[0];
        var gid = (string)f.Run("group.create", new JsonObject { ["ids"] = new JsonArray(rect) })!["id"]!;
        var before = f.Model.OfClass(IfcClass.Annotation).Count();
        var r = (JsonObject)f.Run("drafting.explode", new JsonObject { ["ids"] = new JsonArray(ids.Select(i => (JsonNode)i).ToArray()) })!;
        // The rectangle gave four lines, the open polyline two; the circle stayed.
        Assert.Equal(2, (int)r["exploded"]!);
        Assert.Equal(6, ((JsonArray)r["ids"]!).Count);
        Assert.Equal(1, (int)r["kept"]!);
        Assert.Equal(before + 4, f.Model.OfClass(IfcClass.Annotation).Count());
        var lines = ((JsonArray)r["ids"]!).Select(x => f.Model.Get((string)x!)!).ToList();
        Assert.All(lines, e => { Assert.IsType<Lapin.Drafting.Cad.Line>(Drafts.ShapeOf(e)); Assert.Equal("A-DET", e.Str("layer")); });
        Assert.Equal(4, lines.Take(4).Select(e => (Lapin.Drafting.Cad.Line)Drafts.ShapeOf(e)!).Count(l => Math.Abs((l.B - l.A).Length - 4) < 1e-9 || Math.Abs((l.B - l.A).Length - 3) < 1e-9));
        Assert.Equal(lines.Count, lines.Select(e => e.Name).Distinct().Count());
        // The rectangle's lines are in its group; the first kept the entity.
        Assert.Equal(rect, lines[0].Id);
        Assert.All(lines.Take(4), e => Assert.Equal(gid, Groups.Of(f.Model, e.Id)?.Id));
        Assert.IsType<Circle>(S(f, ids[2]));
        Assert.NotNull(f.Model.Get(hatch));
        // Lines, circles, hatches alone give nothing to explode; a locked layer refuses.
        Assert.Throws<CommandException>(() => f.Run("drafting.explode", new JsonObject { ["ids"] = new JsonArray(ids[2], hatch) }));
        // One undo step brings the polylines back.
        f.Session.Undo();
        Assert.Equal(before, f.Model.OfClass(IfcClass.Annotation).Count());
        Assert.IsType<Lapin.Drafting.Cad.Polyline>(S(f, rect));
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "A-DET", ["locked"] = true });
        Assert.Throws<CommandException>(() => f.Run("drafting.explode", new JsonObject { ["ids"] = new JsonArray(rect) }));
    }

    [Fact]
    public void Explode_splits_a_3D_polylines_heights_with_it()
    {
        var f = F();
        var plan = Plan(f);
        var id = Draw(f, plan, new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(4, 0), J(4, 3)), ["closed"] = false })[0];
        var e = f.Model.Get(id)!;
        var attrs = e.CloneAttrs();
        attrs["heights"] = new JsonArray(0.0, 1.0, 2.5);
        f.Session.Edit("heights", c => c.Replace(e.With(attrs: attrs)));
        var r = (JsonObject)f.Run("drafting.explode", new JsonObject { ["id"] = id })!;
        var zs = ((JsonArray)r["ids"]!).Select(x => f.Model.Get((string)x!)!.Attrs["heights"]!.AsArray().Select(z => (double)z!).ToArray()).ToList();
        Assert.Equal([0.0, 1.0], zs[0]);
        Assert.Equal([1.0, 2.5], zs[1]);
    }

    [Fact]
    public void A_solid_hatch_can_be_clicked_by_its_area_and_its_outline_and_a_pattern_one_too()
    {
        // Report #122, decision 429: a SOLID hatch draws no lines, so the interface had nothing to pick it by.
        var f = F();
        var plan = Plan(f);
        Draw(f, plan, new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(10, 0), J(10, 10), J(0, 10)), ["closed"] = true });
        var solid = (string)f.Run("drafting.hatch", new JsonObject { ["viewId"] = plan, ["at"] = J(1, 1), ["pattern"] = "SOLID", ["color"] = "#cccccc" })!["id"]!;
        var cross = (string)f.Run("drafting.hatch", new JsonObject { ["viewId"] = plan, ["at"] = J(1, 1), ["pattern"] = "cross" })!["id"]!;
        var marks = Drawing(f, plan)["marks"]!.AsArray().OfType<JsonObject>().ToList();
        var m = marks.Single(x => (string?)x["id"] == solid);
        Assert.True((bool)m["hit"]!["solid"]!);
        Assert.Equal(8, m["hit"]!["loops"]![0]!.AsArray().Count);
        Assert.Equal(16, m["segments"]!.AsArray().Count);
        var p = marks.Single(x => (string?)x["id"] == cross);
        Assert.False((bool)p["hit"]!["solid"]!);
        Assert.True(p["segments"]!.AsArray().Count >= 16);
    }

    [Fact]
    public void Drafting_goes_to_DXF_on_its_own_layer()
    {
        var f = F();
        var plan = Plan(f);
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "A-NOTE" });
        f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["layer"] = "A-NOTE", ["shape"] = Line(0, 0, 3, 0) });
        var dxf = System.Text.Encoding.Latin1.GetString(Views.Dxf(f.Session.Reader, Views.Find(f.Model, plan)!));
        Assert.Contains("A-NOTE", dxf);
    }
}
