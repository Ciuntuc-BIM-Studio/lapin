using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Continuous dimensions as one object: a string of points is one entity,
/// drawn as one mark with each segment's value; it is driven, moved, copied,
/// deleted, edited point by point, split and joined as a whole; older
/// projects' strings (one entity per segment) open as one; DXF continued
/// dimensions come in as strings.
/// </summary>
public class DimensionStringTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f) => "plan:" + f.Storey;
    static JsonObject Draw(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static List<JsonObject> Marks(JsonObject d, string id) => d["marks"]!.AsArray().OfType<JsonObject>().Where(m => (string)m["id"]! == id).ToList();
    static List<string> Texts(JsonObject d) => d["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string)p["layer"]! == "TEXT")
        .SelectMany(p => p["texts"]!.AsArray()).Select(t => (string)t!["text"]!).ToList();
    static JsonArray Pts(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());
    static List<Entity> Dims(Fixture f) => f.Model.OfClass(IfcClass.Annotation).Where(e => e.PredefinedType == "DIMENSION").ToList();
    static List<Vec2> Stored(Fixture f, string id) => Dimensions.Stations(f.Model.Get(id)!).Select(s => s.P).ToList();
    static List<double> Values(Fixture f, string id) =>
        f.Session.Query("drafting.dimension", new JsonObject { ["id"] = id })!["values"]!.AsArray().Select(x => Math.Round((double)x!, 6)).ToList();
    static double X(Fixture f, string wall) => f.Model.Get(wall)!.Attrs["start"]![0]!.GetValue<double>();

    static string String(Fixture f, JsonArray pts, (double X, double Y) at) =>
        (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = Plan(f), ["pts"] = pts, ["at"] = new JsonArray(at.X, at.Y) })!["id"]!;

    [Fact]
    public void A_string_is_one_dimension_drawn_as_one_mark_with_each_segment_s_value()
    {
        var f = F();
        var id = String(f, Pts((0, 0), (3, 0), (7, 0)), (0, 2));
        Assert.Single(Dims(f));
        Assert.Equal([3.0, 4.0], Values(f, id));
        var d = Draw(f, Plan(f));
        var mark = Assert.Single(Marks(d, id));
        Assert.Contains("3000", Texts(d));
        Assert.Contains("4000", Texts(d));
        // Every point snaps, where it lands on the dimension line; the grips read the points as drawn.
        Assert.Equal(6, mark["snaps"]!.AsArray().Count);
        Assert.Equal(3, mark["data"]!["points"]!.AsArray().Count);
        // Built point by point, it stays one: `after` and extendDimension put points on its end.
        var one = (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = Plan(f), ["a"] = new JsonArray(0, 5), ["b"] = new JsonArray(1, 5), ["at"] = new JsonArray(0, 6) })!["id"]!;
        Assert.Equal(one, (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = Plan(f), ["a"] = new JsonArray(1, 5), ["b"] = new JsonArray(3, 5), ["at"] = new JsonArray(0, 6), ["after"] = one })!["id"]!);
        f.Run("drafting.extendDimension", new JsonObject { ["id"] = one, ["point"] = new JsonArray(6, 5) });
        Assert.Equal(2, Dims(f).Count);
        Assert.Equal([1.0, 2.0, 3.0], Values(f, one));
        // The segment under a point: the one nearest it on the dimension line.
        Assert.Equal(2, (int)f.Session.Query("drafting.dimension", new JsonObject { ["id"] = one, ["at"] = new JsonArray(4.5, 6.1) })!["segment"]!);
    }

    [Fact]
    public void A_value_moved_by_hand_stands_off_its_line_with_a_leader_and_stays_with_its_segment()
    {
        var f = F();
        var id = String(f, Pts((0, 0), (0.12, 0), (3, 0)), (0, 2));
        static Vec2 At(JsonObject mark, int i) => new((double)mark["data"]!["texts"]![i]![0]!, (double)mark["data"]!["texts"]![i]![1]!);
        var before = Assert.Single(Marks(Draw(f, Plan(f)), id));
        var lines = before["segments"]?.AsArray().Count;
        var was = At(before, 0);
        // The 120 mm segment's value dragged up and to the left, clear of its neighbour's.
        f.Run("drafting.moveDimensionText", new JsonObject { ["id"] = id, ["segment"] = 0, ["by"] = new JsonArray(-0.4, 0.5) });
        var after = Assert.Single(Marks(Draw(f, Plan(f)), id));
        Assert.Equal(was.X - 0.4, At(after, 0).X, 6);
        Assert.Equal(was.Y + 0.5, At(after, 0).Y, 6);
        Assert.Equal(At(before, 1), At(after, 1));
        if (lines is { } n) Assert.True(after["segments"]!.AsArray().Count > n, "a leader back to its segment");
        // A point put into the other segment leaves it where it was moved; one into this segment measures it anew, its text back on the line.
        f.Run("drafting.insertDimensionPoint", new JsonObject { ["id"] = id, ["point"] = new JsonArray(2, 0) });
        Assert.NotNull(Dimensions.OffsetsOf(f.Model.Get(id)!.Attrs, 3)[0]);
        // Turned with the string, the offset turns too; split, each piece keeps its own.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "rotate", ["center"] = new JsonArray(0, 0), ["angle"] = 90 });
        var turned = Dimensions.OffsetsOf(f.Model.Get(id)!.Attrs, 3)[0]!.Value;
        Assert.Equal(-0.5, turned.X, 6);
        Assert.Equal(-0.4, turned.Y, 6);
        var ids = f.Run("drafting.splitDimension", new JsonObject { ["id"] = id })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.NotNull(Dimensions.OffsetsOf(f.Model.Get(ids[0])!.Attrs, 1)[0]);
        Assert.Null(Dimensions.OffsetsOf(f.Model.Get(ids[1])!.Attrs, 1)[0]);
        // Back on its line.
        f.Run("drafting.moveDimensionText", new JsonObject { ["id"] = ids[0], ["reset"] = true });
        Assert.Null(f.Model.Get(ids[0])!.Attrs["textAt"]);
    }

    [Fact]
    public void Driving_a_middle_segment_pushes_what_follows_and_one_undo_puts_it_back()
    {
        var f = F();
        var walls = new[] { 0.0, 3, 7, 10 }.Select(x => f.Wall(x, 0, x, 4)).ToList();
        var id = String(f, Pts((0, 2), (3, 2), (7, 2), (10, 2)), (0, 6));
        Assert.Equal([3.0, 4.0, 3.0], Values(f, id));
        f.Run("drafting.setDimensionValue", new JsonObject { ["id"] = id, ["value"] = 5, ["segment"] = 1 });
        Assert.Equal([3.0, 5.0, 3.0], Values(f, id));
        Assert.Equal([0.0, 3, 8, 11], walls.Select(w => Math.Round(X(f, w), 6)));
        // The stored points went with what they hold on.
        Assert.Equal(11, Stored(f, id)[3].X, 6);
        f.Session.Undo();
        Assert.Equal([0.0, 3, 7, 10], walls.Select(w => Math.Round(X(f, w), 6)));
        Assert.Throws<CommandException>(() => f.Run("drafting.setDimensionValue", new JsonObject { ["id"] = id, ["value"] = 5, ["segment"] = 3 }));
    }

    [Fact]
    public void A_string_moves_copies_and_goes_as_a_whole()
    {
        var f = F();
        var id = String(f, Pts((0, 0), (2, 0), (5, 0)), (0, 1));
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = new JsonArray(1, 1) });
        Assert.Equal([new Vec2(1, 1), new Vec2(3, 1), new Vec2(6, 1)], Stored(f, id));
        var copy = (string)f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "copy", ["by"] = new JsonArray(0, 3) })!["ids"]![0]!;
        Assert.NotEqual(id, copy);
        Assert.Equal([new Vec2(1, 4), new Vec2(3, 4), new Vec2(6, 4)], Stored(f, copy));
        // The copy is a dimension of its own: an edit to it leaves the original as it was.
        f.Run("drafting.removeDimensionPoint", new JsonObject { ["id"] = copy, ["index"] = 1 });
        Assert.Equal(3, Stored(f, id).Count);
        Assert.Null(f.Model.Get(copy)!.Attrs["chain"]);
        // Drafting's own transform: turned a quarter, a linear string's direction turns with it.
        var lin = (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = Plan(f), ["pts"] = Pts((0, 10), (1, 11), (3, 12)), ["at"] = new JsonArray(0, 14), ["dir"] = new JsonArray(1, 0) })!["id"]!;
        f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(lin), ["op"] = "rotate", ["center"] = new JsonArray(0, 0), ["angle"] = 90 });
        Assert.Equal([1.0, 2.0], Values(f, lin));
        Assert.Equal(1, Math.Round((double)f.Model.Get(lin)!.Attrs["dir"]![1]!, 9));
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(id) });
        Assert.Null(f.Model.Get(id));
        Assert.Equal(2, Dims(f).Count);
    }

    [Fact]
    public void Points_are_moved_inserted_and_taken_out_like_a_polyline_s()
    {
        var f = F();
        var w = f.Wall(8, 0, 8, 4);
        var id = String(f, Pts((0, 2), (2, 2), (5, 2)), (0, 6));
        f.Run("drafting.insertDimensionPoint", new JsonObject { ["id"] = id, ["index"] = 2, ["point"] = new JsonArray(3, 2) });
        Assert.Equal([2.0, 1.0, 2.0], Values(f, id));
        f.Run("drafting.removeDimensionPoint", new JsonObject { ["id"] = id, ["index"] = 1 });
        Assert.Equal([3.0, 2.0], Values(f, id));
        // Moved onto the wall's centre line, the last point holds on it and follows it.
        f.Run("drafting.moveDimensionPoint", new JsonObject { ["id"] = id, ["index"] = 2, ["point"] = new JsonArray(8, 2) });
        Assert.NotNull(Dimensions.Stations(f.Model.Get(id)!)[2].Ref);
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(w), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        Assert.Equal([3.0, 6.0], Values(f, id));
        // A dimension keeps two points.
        f.Run("drafting.removeDimensionPoint", new JsonObject { ["id"] = id, ["index"] = 0 });
        Assert.Throws<CommandException>(() => f.Run("drafting.removeDimensionPoint", new JsonObject { ["id"] = id, ["index"] = 0 }));
        // Each an undo step of its own.
        f.Session.Undo();
        Assert.Equal(3, Stored(f, id).Count);
    }

    /// <summary>As in ArchiCAD: points clicked into a selected string go where they fall along its line, inside it or beyond its ends, each holding on what it lands on.</summary>
    [Fact]
    public void Points_added_to_a_string_go_where_they_fall_along_it()
    {
        var f = F();
        var w = f.Wall(4, -3, 4, 3);
        // A string drawn right to left: the order runs that way.
        var id = String(f, Pts((9, 0), (6, 0), (0, 0)), (0, 2));
        Run(f, id, (2, -1), (7.5, 1));
        Assert.Equal([1.5, 1.5, 4.0, 2.0], Values(f, id));
        Assert.Equal([9.0, 7.5, 6.0, 2.0, 0.0], Stored(f, id).Select(p => p.X));
        // One past an end is the new end; one on the wall's centre line follows it.
        Run(f, id, (4, 0), (-1, 0.5), (10, 0));
        Assert.Equal([10.0, 9.0, 7.5, 6.0, 4.0, 2.0, 0.0, -1.0], Stored(f, id).Select(p => p.X));
        Assert.NotNull(Dimensions.Stations(f.Model.Get(id)!)[4].Ref);
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(w), ["op"] = "move", ["by"] = new JsonArray(0.5, 0) });
        Assert.Equal([1.0, 1.5, 1.5, 1.5, 2.5, 2.0, 1.0], Values(f, id));
        // A point the string measures already is not doubled; one call is one undo step.
        Assert.Throws<CommandException>(() => Run(f, id, (6, 5)));
        f.Session.Undo();
        Assert.Equal(8, Stored(f, id).Count);
        f.Session.Undo();
        Assert.Equal(5, Stored(f, id).Count);

        // An aligned string keeps its direction when a new end lies off its line.
        var a = String(f, Pts((0, 10), (4, 10)), (0, 12));
        Run(f, a, (6, 11));
        Assert.Equal([4.0, 2.0], Values(f, a));

        static void Run(Fixture f, string id, params (double X, double Y)[] p) =>
            f.Run("drafting.insertDimensionPoint", new JsonObject { ["id"] = id, ["points"] = Pts(p) });
    }

    [Fact]
    public void A_string_splits_into_single_dimensions_and_they_join_back()
    {
        var f = F();
        var id = String(f, Pts((0, 0), (2, 0), (5, 0)), (0, 1));
        f.Run("drafting.setDimensionText", new JsonObject { ["id"] = id, ["segment"] = 1, ["text"] = "≈3" });
        Assert.Contains("≈3", Texts(Draw(f, Plan(f))));
        var ids = f.Run("drafting.splitDimension", new JsonObject { ["id"] = id })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(2, ids.Count);
        Assert.Equal(id, ids[0]);
        Assert.Equal([2.0], Values(f, ids[0]));
        Assert.Equal("≈3", f.Model.Get(ids[1])!.Str("text"));
        Assert.Contains("≈3", Texts(Draw(f, Plan(f))));
        // Joined in either order: one string again, on the first one's line.
        var joined = (string)f.Run("drafting.joinDimensions", new JsonObject { ["ids"] = new JsonArray(ids[1], ids[0]) })!["id"]!;
        Assert.Single(Dims(f));
        Assert.Equal(ids[1], joined);
        Assert.Equal([2.0, 3.0], Values(f, joined));
        Assert.Contains("≈3", Texts(Draw(f, Plan(f))));
        // Dimensions on another line do not join.
        var other = String(f, Pts((5, 0), (6, 0)), (0, 3));
        Assert.Throws<CommandException>(() => f.Run("drafting.joinDimensions", new JsonObject { ["ids"] = new JsonArray(joined, other) }));
    }

    [Fact]
    public void An_older_project_s_strings_open_as_one_dimension_each()
    {
        var f = F();
        var view = Plan(f);
        var wall = f.Wall(3, 0, 3, 4);
        var entities = f.Model.Entities.ToList();
        var relations = f.Model.Relations.ToList();
        static JsonObject Old(string view, (double, double) a, (double, double) b, string? chain, int ix, JsonObject? refB = null, string? text = null)
        {
            var o = new JsonObject { ["viewId"] = view, ["a"] = new JsonArray(a.Item1, a.Item2), ["b"] = new JsonArray(b.Item1, b.Item2), ["at"] = new JsonArray(0, 6) };
            if (chain != null) { o["chain"] = chain; o["chainIx"] = ix; }
            if (refB != null) o["refB"] = refB;
            if (text != null) o["text"] = text;
            return o;
        }
        var axis = new JsonObject { ["kind"] = "axis", ["element"] = wall, ["s"] = 2 };
        // Saved out of order, as a file may hold them; the middle one holds on the wall, the last has its own text.
        entities.Add(new Entity("d2", IfcClass.Annotation, "Dimension", Old(view, (3, 2), (7, 2), "d1", 1), "DIMENSION"));
        entities.Add(new Entity("d1", IfcClass.Annotation, "Dimension", Old(view, (0, 2), (3, 2), "d1", 0, axis), "DIMENSION"));
        entities.Add(new Entity("d3", IfcClass.Annotation, "Dimension", Old(view, (7, 2), (9, 2), "d1", 2, text: "2 m"), "DIMENSION"));
        entities.Add(new Entity("single", IfcClass.Annotation, "Dimension", Old(view, (0, -2), (4, -2), null, 0), "DIMENSION"));
        f.Session.Load(entities, relations, null);
        Assert.Equal(["d1", "single"], Dims(f).Select(d => d.Id).Order());
        var d1 = f.Model.Get("d1")!;
        Assert.Null(d1.Attrs["chain"]);
        Assert.Null(d1.Attrs["a"]);
        Assert.Equal([new Vec2(0, 2), new Vec2(3, 2), new Vec2(7, 2), new Vec2(9, 2)], Stored(f, "d1"));
        Assert.Equal("axis", Dimensions.Stations(d1)[1].Ref!.Kind);
        Assert.Equal([null, null, "2 m"], Dimensions.TextsOf(d1.Attrs, 3));
        Assert.Equal([3.0, 4.0, 2.0], Values(f, "d1"));
        Assert.Equal([new Vec2(0, -2), new Vec2(4, -2)], Stored(f, "single"));
        // Driving its first segment pushes the wall and what follows, as the string did.
        f.Run("drafting.setDimensionValue", new JsonObject { ["id"] = "d1", ["value"] = 3.5 });
        Assert.Equal(3.5, X(f, wall), 6);
    }

    [Fact]
    public void A_view_duplicated_with_its_detailing_copies_a_string_as_a_string_of_its_own()
    {
        var f = F();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var id = (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["pts"] = Pts((0, 0), (2, 0), (5, 0)), ["at"] = new JsonArray(0, 1) })!["id"]!;
        var copy = (string)f.Run("view.duplicate", new JsonObject { ["id"] = plan, ["withDetailing"] = true })!["id"]!;
        var dup = Assert.Single(Dims(f), d => d.Str("viewId") == copy);
        Assert.NotEqual(id, dup.Id);
        Assert.Equal(3, Dimensions.Stations(dup).Count);
        Assert.Single(Marks(Draw(f, copy), dup.Id));
    }

    // ------------------------------------------------------------ DXF

    /// <summary>A DXF DIMENSION (aligned when <paramref name="kind"/> is 1, else rotated by <paramref name="deg"/>): its line through (dx, dy), measuring from (x1, y1) to (x2, y2), in mm.</summary>
    static string Dim(double x1, double y1, double x2, double y2, double dx, double dy, int kind = 0, double deg = 0) =>
        $"0\nDIMENSION\n8\nDIMS\n70\n{kind}\n10\n{dx}\n20\n{dy}\n13\n{x1}\n23\n{y1}\n14\n{x2}\n24\n{y2}\n50\n{deg}";

    static string Dxf(string entities) => string.Join("\n", new[]
    {
        "0", "SECTION", "2", "HEADER", "9", "$ACADVER", "1", "AC1015", "9", "$INSUNITS", "70", "4", "0", "ENDSEC",
        "0", "SECTION", "2", "ENTITIES", entities, "0", "ENDSEC", "0", "EOF",
    });

    [Fact]
    public void Dxf_continued_dimensions_come_in_as_one_string_unless_asked_not_to()
    {
        // A continued string 0–3–5–9 m on the line y = −0.5 (written out of order), and a baseline dimension 0–9 on another line.
        var dxf = Dxf(string.Join("\n",
            Dim(3000, 0, 5000, 0, 5000, -500), Dim(0, 0, 3000, 0, 3000, -500), Dim(5000, 0, 9000, 0, 9000, -500), Dim(0, 0, 9000, 0, 9000, -1500)));
        JsonObject Import(Fixture f, bool? join)
        {
            var a = new JsonObject { ["viewId"] = Plan(f), ["name"] = "dims.dxf", ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(dxf)) };
            if (join is { } j) a["joinDimensions"] = j;
            return (JsonObject)f.Run("drafting.importDxf", a)!;
        }
        var f = F();
        var r = Import(f, null);
        Assert.Equal(4, (int)r["dimensions"]!);
        Assert.Equal(1, (int)r["dimensionStrings"]!);
        var dims = Dims(f);
        Assert.Equal(2, dims.Count);
        var s = dims.Single(d => Dimensions.Stations(d).Count == 4);
        Assert.Equal([0.0, 3, 5, 9], Dimensions.Stations(s).Select(x => Math.Round(x.P.X, 6)));
        Assert.Equal([3.0, 2.0, 4.0], Values(f, s.Id));
        var g = F();
        Import(g, false);
        Assert.Equal(4, Dims(g).Count);
    }
}
