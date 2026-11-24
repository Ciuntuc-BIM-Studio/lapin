using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// CAD's edits on elements (audit 2026-11-15, §6): split, trim/extend to a boundary and offset on walls, beams and strip
/// footings (straight and curved), and floors and ceilings split or trimmed by a line. One undo step each; the first part keeps
/// its GlobalId, the quantities of the parts add up to the element's, joins are made again, and what was not touched exports
/// to IFC as it did.
/// </summary>
public sealed class ElementEditTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule());
    static Vec2 S(Fixture f, string id) => Joins.Start(f.Model.Get(id)!);
    static Vec2 E(Fixture f, string id) => Joins.End(f.Model.Get(id)!);
    static void Near(double x, double y, Vec2 got) { Assert.Equal(x, got.X, 6); Assert.Equal(y, got.Y, 6); }
    static JsonArray P(double x, double y) => new(x, y);

    static double Q(Fixture f, string id, string set, string name) =>
        f.Session.Quantities(id).Single(q => q.Name == set).Quantities.Single(q => q.Name == name).Value;

    static string[] Ids(JsonNode? r) => [.. ((JsonArray)r!["ids"]!).Select(n => (string)n!)];

    [Fact]
    public void A_straight_wall_split_gives_two_walls_whose_quantities_add_up_joined_in_line_its_door_on_the_second()
    {
        var f = F();
        var w = f.Wall(0, 0, 6, 0, height: 3);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = w, ["x"] = 4.5, ["y"] = 0 })!["id"]!;
        var opening = f.Model.RelatingOf(door, IfcClass.RelFillsElement)!.Id;
        var (volume, length, area) = (Q(f, w, "Qto_WallBaseQuantities", "NetVolume"), Q(f, w, "Qto_WallBaseQuantities", "Length"), Q(f, w, "Qto_WallBaseQuantities", "NetSideArea"));
        var type = f.Model.RelatingOf(w, IfcClass.RelDefinesByType)!.Id;

        var ids = Ids(f.Run("element.split", new JsonObject { ["id"] = w, ["at"] = P(3, 0.2) }));
        Assert.Equal(w, ids[0]);
        var second = ids[1];
        Near(0, 0, S(f, w)); Near(3, 0, E(f, w));
        Near(3, 0, S(f, second)); Near(6, 0, E(f, second));
        Assert.Equal(type, f.Model.RelatingOf(second, IfcClass.RelDefinesByType)!.Id);
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, second)!.Id);
        Assert.NotEqual(Walls.UsageOf(f.Model, w)!.Id, Walls.UsageOf(f.Model, second)!.Id);
        // The door went with the part it stands in, at the same place in plan.
        Assert.Equal(second, f.Model.RelatingOf(opening, IfcClass.RelVoidsElement)!.Id);
        Assert.Equal(1.5, f.Model.Get(opening)!.Num("along"), 9);
        // The two parts meet in line, and add up to the wall.
        Assert.NotNull(Joins.Between(f.Model, w, second));
        Assert.Equal(length, Q(f, w, "Qto_WallBaseQuantities", "Length") + Q(f, second, "Qto_WallBaseQuantities", "Length"), 6);
        Assert.Equal(volume, Q(f, w, "Qto_WallBaseQuantities", "NetVolume") + Q(f, second, "Qto_WallBaseQuantities", "NetVolume"), 6);
        Assert.Equal(area, Q(f, w, "Qto_WallBaseQuantities", "NetSideArea") + Q(f, second, "Qto_WallBaseQuantities", "NetSideArea"), 6);

        // One undo step: the one wall again, its door on it.
        f.Session.Undo();
        Assert.Single(f.Model.OfClass(IfcClass.Wall));
        Near(6, 0, E(f, w));
        Assert.Equal(w, f.Model.RelatingOf(opening, IfcClass.RelVoidsElement)!.Id);
    }

    [Fact]
    public void A_wall_split_beside_a_T_is_cut_exactly_where_clicked_and_keeps_its_T()
    {
        // Forum topic 248: the cut clicked on the corner of a wall that stops on this one (its face's endpoint) went in
        // half a face lower, the T worked out again as a corner that pulled the cut to it.
        var f = F();
        var v = f.Wall(0, -3, 0, 3);
        var h = f.Wall(0, 0, 5, 0);
        var t = f.Session.Catalog.Get<IPathElements>()!.FrameOf(f.Model.Get(v)!, f.Model)!.Thickness;
        Assert.Equal("T", (string?)Joins.Between(f.Model, v, h)!.Attrs!["kind"]);

        var second = Ids(f.Run("element.split", new JsonObject { ["id"] = v, ["at"] = P(t / 2, t / 2) }))[1];
        Near(0, -3, S(f, v)); Near(0, t / 2, E(f, v));
        Near(0, t / 2, S(f, second)); Near(0, 3, E(f, second));
        // The wall that stopped on it still stops on the part it meets, where it was.
        var tee = Joins.Between(f.Model, v, h)!;
        Assert.Equal("T", (string?)tee.Attrs!["kind"]);
        Near(0, 0, S(f, h)); Near(5, 0, E(f, h));
        Assert.NotNull(Joins.Between(f.Model, v, second));
    }

    [Fact]
    public void A_split_hands_the_far_ends_joins_to_the_second_part_and_refuses_a_cut_through_a_door()
    {
        var f = F();
        var a = f.Wall(0, 0, 6, 0, height: 3);
        var b = f.Wall(6, 0, 6, 4, height: 3);
        var c = f.Wall(0, 0, 0, 4, height: 3);
        Assert.NotNull(Joins.Between(f.Model, a, b));
        Assert.NotNull(Joins.Between(f.Model, a, c));
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["x"] = 2, ["y"] = 0 });
        var e = Assert.Throws<CommandException>(() => f.Run("element.split", new JsonObject { ["id"] = a, ["at"] = P(2, 0) }));
        Assert.Contains("across the cut", e.Message);

        var second = Ids(f.Run("element.split", new JsonObject { ["id"] = a, ["at"] = P(4, 0) }))[1];
        Assert.NotNull(Joins.Between(f.Model, a, c));
        Assert.Null(Joins.Between(f.Model, a, b));
        Assert.NotNull(Joins.Between(f.Model, second, b));
        Assert.NotNull(Joins.Between(f.Model, a, second));
    }

    [Fact]
    public void A_curved_wall_split_keeps_its_circle_and_its_length()
    {
        var f = F();
        var w = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -2, ["y1"] = 0, ["x2"] = 2, ["y2"] = 0, ["arc"] = 2, ["height"] = 3 })!["id"]!;
        var (length, volume) = (Q(f, w, "Qto_WallBaseQuantities", "Length"), Q(f, w, "Qto_WallBaseQuantities", "NetVolume"));
        var second = Ids(f.Run("element.split", new JsonObject { ["id"] = w, ["at"] = P(0, 2.1) }))[1];
        Near(0, 2, E(f, w));
        Near(0, 2, S(f, second));
        Near(2, 0, E(f, second));
        Assert.Equal(2, PathCurve.Of(f.Model.Get(second)!).Radius, 6);
        Assert.Equal(length, Q(f, w, "Qto_WallBaseQuantities", "Length") + Q(f, second, "Qto_WallBaseQuantities", "Length"), 6);
        Assert.InRange(Q(f, w, "Qto_WallBaseQuantities", "NetVolume") + Q(f, second, "Qto_WallBaseQuantities", "NetVolume"), volume * 0.995, volume * 1.005);
    }

    [Fact]
    public void A_split_where_another_element_crosses_and_a_beam_split_add_up()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 8, ["y2"] = 0 })!["id"]!;
        var line = new JsonArray(P(5, -1), P(5, 1));
        var volume = Q(f, beam, "Qto_BeamBaseQuantities", "NetVolume");
        var second = Ids(f.Run("element.split", new JsonObject { ["id"] = beam, ["line"] = line }))[1];
        Near(5, 0, E(f, beam));
        Near(5, 0, S(f, second));
        Assert.Equal(IfcClass.Beam, f.Model.Get(second)!.Class);
        Assert.Equal(volume, Q(f, beam, "Qto_BeamBaseQuantities", "NetVolume") + Q(f, second, "Qto_BeamBaseQuantities", "NetVolume"), 6);

        var strip = (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 5, ["x2"] = 8, ["y2"] = 5 })!["id"]!;
        var crossing = f.Wall(3, 3, 3, 7, height: 3);
        var other = Ids(f.Run("element.split", new JsonObject { ["id"] = strip, ["with"] = crossing }))[1];
        Near(3, 5, E(f, strip));
        Near(3, 5, S(f, other));
    }

    [Fact]
    public void Trim_and_extend_bring_the_end_on_the_far_side_to_an_axis_a_face_or_a_line_keeping_the_part_picked()
    {
        var f = F();
        var w = f.Wall(0, 0, 6, 0, height: 3);
        var cross = f.Wall(4, -2, 4, 2, height: 3);
        // The crossing wall runs up (+y): the wall kept lies on its left, where its YMax face is.
        var left = Walls.Frame(f.Model.Get(cross)!, f.Model)!.YMax;

        // Picked left of the crossing wall: the right end comes back to its near face.
        f.Run("element.trimExtend", new JsonObject { ["id"] = w, ["at"] = P(1, 0), ["boundary"] = cross, ["face"] = "near" });
        Near(4 - left, 0, E(f, w));
        Near(0, 0, S(f, w));
        // To a drafted line past the start: the start goes on to it.
        f.Run("element.trimExtend", new JsonObject { ["id"] = w, ["at"] = P(1, 0), ["line"] = new JsonArray(P(-1, -1), P(-1, 1)) });
        Near(-1, 0, S(f, w));
        // Picked right of the line x = 1: the start is trimmed to it.
        f.Run("element.trimExtend", new JsonObject { ["id"] = w, ["at"] = P(2, 0), ["line"] = new JsonArray(P(1, -1), P(1, 1)) });
        Near(1, 0, S(f, w));
        // To the crossing wall's axis: joined to it there.
        f.Run("element.trimExtend", new JsonObject { ["id"] = w, ["at"] = P(2, 0), ["boundary"] = cross });
        Near(4, 0, E(f, w));
        Assert.NotNull(Joins.Between(f.Model, w, cross));
        Assert.Throws<CommandException>(() => f.Run("element.trimExtend", new JsonObject { ["id"] = w, ["at"] = P(2, 0), ["line"] = new JsonArray(P(0, 3), P(6, 3)) }));
    }

    [Fact]
    public void A_curved_element_is_carried_on_along_its_circle_and_an_opening_stays_where_it_is_in_plan()
    {
        var f = F();
        var w = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 2, ["y1"] = 0, ["x2"] = 0, ["y2"] = 2, ["arc"] = Math.Sqrt(2) - 2, ["height"] = 3 })!["id"]!;
        Assert.Equal(2, PathCurve.Of(f.Model.Get(w)!).Radius, 6);
        // A quarter of the circle round the origin, (2, 0) to (0, 2): its end carried on round it to the line x = −2, a half circle.
        f.Run("element.trimExtend", new JsonObject { ["id"] = w, ["at"] = P(1.4, 1.4), ["line"] = new JsonArray(P(-2, -1), P(-2, 1)) });
        Near(-2, 0, E(f, w));
        Assert.Equal(2, PathCurve.Of(f.Model.Get(w)!).Radius, 6);
        Assert.Equal(Math.PI * 2, Q(f, w, "Qto_WallBaseQuantities", "Length"), 6);

        // A straight wall's start trimmed: its window keeps its place in plan.
        var s = f.Wall(0, 5, 8, 5, height: 3);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = s, ["x"] = 6, ["y"] = 5 })!["id"]!;
        var opening = f.Model.RelatingOf(window, IfcClass.RelFillsElement)!.Id;
        f.Run("element.trimExtend", new JsonObject { ["id"] = s, ["at"] = P(7, 5), ["line"] = new JsonArray(P(2, 4), P(2, 6)) });
        Assert.Equal(4, f.Model.Get(opening)!.Num("along"), 9);
        var e = Assert.Throws<CommandException>(() => f.Run("element.trimExtend", new JsonObject { ["id"] = s, ["at"] = P(3, 5), ["line"] = new JsonArray(P(5, 4), P(5, 6)) }));
        Assert.Contains("cut away", e.Message);
    }

    [Fact]
    public void Offset_copies_an_element_parallel_by_its_axis_or_between_its_faces_and_a_curved_one_concentric()
    {
        var f = F();
        var w = f.Wall(0, 0, 6, 0, height: 3);
        var t = Walls.Section(f.Model, f.Model.Get(w)!);
        var thick = t.Max(x => x.To) - t.Min(x => x.From);
        var copy = (string)f.Run("element.offset", new JsonObject { ["id"] = w, ["side"] = P(3, 2), ["distance"] = 3 })!["id"]!;
        Near(0, 3, S(f, copy)); Near(6, 3, E(f, copy));
        Assert.Equal(f.Model.RelatingOf(w, IfcClass.RelDefinesByType)!.Id, f.Model.RelatingOf(copy, IfcClass.RelDefinesByType)!.Id);
        var byFace = (string)f.Run("element.offset", new JsonObject { ["id"] = w, ["side"] = P(3, -2), ["distance"] = 1, ["from"] = "face" })!["id"]!;
        Near(0, -(1 + thick), S(f, byFace));
        Assert.Throws<CommandException>(() => f.Run("element.offset", new JsonObject { ["id"] = w, ["side"] = P(3, 2), ["distance"] = thick / 2 }));

        var arc = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -2, ["y1"] = 10, ["x2"] = 2, ["y2"] = 10, ["arc"] = 2 })!["id"]!;
        var inner = (string)f.Run("element.offset", new JsonObject { ["id"] = arc, ["side"] = P(0, 10.5), ["distance"] = 0.5 })!["id"]!;
        Assert.Equal(1.5, PathCurve.Of(f.Model.Get(inner)!).Radius, 6);
        Near(-1.5, 10, S(f, inner));
    }

    [Fact]
    public void A_floor_split_by_a_line_gives_two_floors_whose_areas_add_up_with_their_arcs_and_openings()
    {
        var f = F();
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey,
            ["points"] = new JsonArray(P(0, 0), P(8, 0), P(8, 4), P(0, 4)),
            // The top edge bulges out by 1 m.
            ["arcs"] = new JsonArray(0, 0, 1, 0),
        })!["id"]!;
        var hole = (string)f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["x1"] = 5, ["y1"] = 1, ["x2"] = 7, ["y2"] = 2 })!["id"]!;
        var area = ArcOutline.SignedArea(Slabs.Corners(f.Model.Get(slab)!), Slabs.Rises(f.Model.Get(slab)!));
        var volume = Q(f, slab, "Qto_SlabBaseQuantities", "NetVolume");

        Assert.Contains("crosses the opening", Assert.Throws<CommandException>(() => f.Run("architecture.splitOutline", new JsonObject { ["id"] = slab, ["a"] = P(6, -1), ["b"] = P(6, 6) })).Message);
        var ids = Ids(f.Run("architecture.splitOutline", new JsonObject { ["id"] = slab, ["a"] = P(4, -1), ["b"] = P(4, 6), ["keep"] = P(1, 1) }));
        Assert.Equal(slab, ids[0]);
        var (one, two) = (f.Model.Get(slab)!, f.Model.Get(ids[1])!);
        Assert.True(Slabs.Corners(one).All(p => p.X <= 4 + 1e-9));
        Assert.True(Slabs.Corners(two).All(p => p.X >= 4 - 1e-9));
        // Each keeps its half of the arc.
        Assert.NotNull(Slabs.Rises(one));
        Assert.NotNull(Slabs.Rises(two));
        Assert.Equal(Math.Abs(area), Math.Abs(ArcOutline.SignedArea(Slabs.Corners(one), Slabs.Rises(one))) + Math.Abs(ArcOutline.SignedArea(Slabs.Corners(two), Slabs.Rises(two))), 6);
        Assert.Equal(ids[1], f.Model.RelatingOf(hole, IfcClass.RelVoidsElement)!.Id);
        Assert.Equal(volume, Q(f, slab, "Qto_SlabBaseQuantities", "NetVolume") + Q(f, ids[1], "Qto_SlabBaseQuantities", "NetVolume"), 2);
        Assert.Equal(f.Model.RelatingOf(slab, IfcClass.RelDefinesByType)!.Id, f.Model.RelatingOf(ids[1], IfcClass.RelDefinesByType)!.Id);

        // Trimmed by a line, the side picked kept.
        f.Run("architecture.trimOutline", new JsonObject { ["id"] = slab, ["a"] = P(2, -1), ["b"] = P(2, 6), ["keep"] = P(3, 1) });
        Assert.True(Slabs.Corners(f.Model.Get(slab)!).All(p => p.X >= 2 - 1e-9 && p.X <= 4 + 1e-9));
        Assert.Contains("lies in the part cut away", Assert.Throws<CommandException>(() => f.Run("architecture.trimOutline", new JsonObject { ["id"] = ids[1], ["a"] = P(4.5, -1), ["b"] = P(4.5, 6), ["keep"] = P(4.2, 1) })).Message);
    }

    [Fact]
    public void A_line_that_crosses_an_outline_more_than_twice_is_refused()
    {
        // A U: the line y = 3 crosses its two arms.
        var u = new List<Vec2> { new(0, 0), new(6, 0), new(6, 4), new(4, 4), new(4, 2), new(2, 2), new(2, 4), new(0, 4) };
        Assert.Contains("4 times", Assert.Throws<CommandException>(() => OutlineCuts.Split(u, null, new Vec2(-1, 3), new Vec2(7, 3))).Message);
        var (left, right) = OutlineCuts.Split(u, null, new Vec2(-1, 1), new Vec2(7, 1));
        Assert.Equal(6 * 1, Math.Abs(ArcOutline.SignedArea(right.Corners, right.Rises)), 9);
        Assert.Equal(24 - 4 - 6, Math.Abs(ArcOutline.SignedArea(left.Corners, left.Rises)), 9);
    }

    [Fact]
    public void A_split_leaves_the_ifc_of_the_elements_it_did_not_touch_as_it_was()
    {
        var f = F();
        var a = f.Wall(0, 0, 6, 0, height: 3);
        var untouched = f.Wall(0, 5, 6, 5, height: 3);
        string Line(string id)
        {
            using var ms = new MemoryStream();
            IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
            var text = Encoding.UTF8.GetString(ms.ToArray());
            var line = text.Split('\n').Single(l => l.Contains("IFCWALL(") && l.Contains("'" + id + "'"));
            // The entity numbers shift with what is added before: the rest is the wall's.
            return Regex.Replace(line, "#\\d+", "#");
        }
        var before = Line(untouched);
        f.Run("element.split", new JsonObject { ["id"] = a, ["at"] = P(2, 0) });
        Assert.Equal(before, Line(untouched));
        Assert.Equal(before.Length, Line(untouched).Length);
    }

}
