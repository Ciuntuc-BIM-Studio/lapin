using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Decision 733 (a user: "core face to core face temporary dimensions whenever I draw"): <c>view.dimLines</c>, one query for
/// every temporary dimension in plan, each module's elements as lines with their axis, faces (finish included), core faces
/// and, for a wall that keeps one, its reference line: walls (architecture), columns and beams (structure). The core-face
/// distances the interface works out come straight from these numbers, checked here for layered walls and columns.
/// </summary>
public class DimLinesTests
{
    static Fixture Make() => new(new ArchitectureModule(), new StructureModule());
    static JsonObject Lines(Fixture f, JsonObject a) => f.Session.Query("view.dimLines", a)!.AsObject();
    static List<JsonObject> All(JsonObject r) => r["lines"]!.AsArray().Select(x => x!.AsObject()).ToList();
    static JsonObject Of(JsonObject r, string id, string? part = null) => All(r).Single(l => (string?)l["id"] == id && (string?)l["part"] == part);
    static double[] Nums(JsonNode? n) => n!.AsArray().Select(x => Math.Round((double)x!, 6)).ToArray();
    static string[] Ids(JsonNode? n) => n?.AsArray().Select(x => (string)x!).OrderBy(x => x).ToArray() ?? [];

    /// <summary>Plaster 20 (exterior, right), brick 250 (core), insulation 100: faces −0.145…0.225, core ±0.125.</summary>
    static string Layered(Fixture f) => f.Type("Plastered brick", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));

    static string ColumnType(Fixture f, double w, double d, params (string Material, double T)[] layers)
    {
        var a = new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = w, ["depth"] = d }, ["height"] = 3 };
        if (layers.Length > 0) a[ColumnLayers.Key] = new JsonArray([.. layers.Select(l => (JsonNode)new JsonObject { ["material"] = l.Material, ["thickness"] = l.T })]);
        return (string)f.Run("structure.createColumnType", a)!["id"]!;
    }

    static string Column(Fixture f, string type, double x, double y, double rotation = 0) =>
        (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = x, ["y"] = y, ["rotation"] = rotation })!["id"]!;

    [Fact]
    public void Walls_come_as_they_do_for_wall_lines_with_their_core_always_and_the_reference_line_they_keep()
    {
        var f = Make();
        var brick = f.Type("Brick 300", ("Brick", 0.3, true));
        var plain = f.Wall(0, 0, 6, 0, brick);
        var layered = f.Wall(0, 3, 6, 3, Layered(f));
        var kept = (string)f.Run("architecture.addWall", new JsonObject
        {
            ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 6, ["x2"] = 6, ["y2"] = 6, ["typeId"] = Layered(f), ["reference"] = "exterior", ["rememberReference"] = true,
        })!["id"]!;
        var r = Lines(f, new JsonObject { ["storeyId"] = f.Storey });

        var p = Of(r, plain);
        Assert.Equal("wall", (string?)p["kind"]);
        Assert.Equal(new[] { -0.15, 0.15 }, Nums(p["faces"]));
        Assert.Equal(new[] { -0.15, 0.15 }, Nums(p["core"]));     // one layer: the core is the whole wall
        Assert.Null(p["reference"]);
        var l = Of(r, layered);
        Assert.Equal(new[] { -0.145, 0.225 }, Nums(l["faces"]));
        Assert.Equal(new[] { -0.125, 0.125 }, Nums(l["core"]));
        // Drawn on its exterior face and keeping it: the line 0.145 right of its axis, at y = 6.
        var k = Of(r, kept);
        Assert.Equal(-0.145, (double)k["reference"]!, 6);
        Assert.Equal(6.0, Nums(k["start"])[1] + (double)k["reference"]!, 6);

        // The same walls, the same numbers, as architecture.wallLines gives them.
        var old = f.Session.Query("architecture.wallLines", new JsonObject { ["storeyId"] = f.Storey })!["walls"]!.AsArray().Select(x => x!.AsObject()).ToList();
        foreach (var w in old)
        {
            var n = Of(r, (string)w["id"]!);
            Assert.Equal(Nums(w["start"]), Nums(n["start"]));
            Assert.Equal(Nums(w["faces"]), Nums(n["faces"]));
            Assert.Equal(Ids(w["joins"]!["start"]), Ids(n["joins"]!["start"]));
        }
    }

    [Fact]
    public void Core_face_to_core_face_between_two_layered_walls_is_the_clear_distance_between_their_brick()
    {
        var f = Make();
        var t = Layered(f);
        var south = f.Wall(0, 0, 6, 0, t);
        var north = f.Wall(6, 4, 0, 4, t);                     // drawn the other way: its left is the room's outside
        var r = Lines(f, new JsonObject { ["storeyId"] = f.Storey });
        var s = Of(r, south);
        var n = Of(r, north);
        // South's left is +y: its core face toward north at 0 + 0.125. North runs −x, its left is −y: its core face toward south at 4 − 0.125.
        var sCore = Nums(s["start"])[1] + Nums(s["core"]).Max();
        var nCore = Nums(n["start"])[1] - Nums(n["core"]).Max();
        Assert.Equal(3.75, nCore - sCore, 6);
        // Finish face to finish face: the insulation 0.1 beyond each core.
        var sFace = Nums(s["start"])[1] + Nums(s["faces"]).Max();
        var nFace = Nums(n["start"])[1] - Nums(n["faces"]).Max();
        Assert.Equal(3.55, nFace - sFace, 6);
    }

    [Fact]
    public void A_column_is_two_lines_through_its_point_its_core_the_section_its_faces_the_finish_layers()
    {
        var f = Make();
        var plastered = ColumnType(f, 0.4, 0.3, ("Plaster", 0.015));
        var c = Column(f, plastered, 2, 2);
        var r = Lines(f, new JsonObject { ["storeyId"] = f.Storey, ["kinds"] = new JsonArray("column") });
        Assert.All(All(r), x => Assert.Equal("column", (string?)x["kind"]));

        var x = Of(r, c, "x");
        Assert.Equal(new[] { 1.785, 2.0 }, Nums(x["start"]));      // the plaster's outer face, its width 0.4 + 2 × 0.015 along x
        Assert.Equal(new[] { 2.215, 2.0 }, Nums(x["end"]));
        Assert.Equal(new[] { -0.165, 0.165 }, Nums(x["faces"]));
        Assert.Equal(new[] { -0.15, 0.15 }, Nums(x["core"]));
        var y = Of(r, c, "y");
        Assert.Equal(new[] { 2.0, 1.835 }, Nums(y["start"]));
        Assert.Equal(new[] { -0.215, 0.215 }, Nums(y["faces"]));
        Assert.Equal(new[] { -0.2, 0.2 }, Nums(y["core"]));

        // Turned 90°: its x runs up the plan.
        var turned = Column(f, plastered, 6, 2, 90);
        var tx = Of(Lines(f, new JsonObject { ["id"] = turned }), turned, "x");
        Assert.Equal(new[] { 6.0, 1.785 }, Nums(tx["start"]));
        Assert.Equal(new[] { 6.0, 2.215 }, Nums(tx["end"]));
    }

    [Fact]
    public void Column_core_face_to_wall_core_face_leaves_out_the_plaster_of_both()
    {
        var f = Make();
        var wall = f.Wall(0, 0, 6, 0, Layered(f));
        var c = Column(f, ColumnType(f, 0.3, 0.3, ("Plaster", 0.015)), 3, 2);
        var r = Lines(f, new JsonObject { ["storeyId"] = f.Storey });
        var w = Of(r, wall);
        var cx = Of(r, c, "x");
        // The column's x line runs along the wall: its core face toward the wall at 2 − 0.15, the wall's at 0 + 0.125.
        var wallCore = Nums(w["start"])[1] + Nums(w["core"]).Max();
        var colCore = Nums(cx["start"])[1] + Nums(cx["core"]).Min();
        Assert.Equal(1.725, colCore - wallCore, 6);
        var colFace = Nums(cx["start"])[1] + Nums(cx["faces"]).Min();
        var wallFace = Nums(w["start"])[1] + Nums(w["faces"]).Max();
        Assert.Equal(1.61, colFace - wallFace, 6);
    }

    [Fact]
    public void A_beam_is_its_axis_with_its_width_and_its_joins_curved_beams_and_other_storeys_left_out_and_nothing_changes()
    {
        var f = Make();
        var bt = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.25, ["depth"] = 0.5 } })!["id"]!;
        string Beam(double x1, double y1, double x2, double y2, double? x3 = null, double? y3 = null)
        {
            var a = new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = bt, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 };
            if (x3 != null) { a["x3"] = x3; a["y3"] = y3; }
            return (string)f.Run("structure.addBeam", a)!["id"]!;
        }
        var b = Beam(0, 0, 6, 0);
        var corner = Beam(6, 0, 6, 4);
        var curved = Beam(0, 5, 6, 5, 3, 6);
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var above = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = upper, ["typeId"] = bt, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var before = f.Model.Revision;
        var r = Lines(f, new JsonObject { ["storeyId"] = f.Storey });
        var l = Of(r, b);
        Assert.Equal("beam", (string?)l["kind"]);
        Assert.Equal(new[] { -0.125, 0.125 }, Nums(l["faces"]));
        Assert.Equal(new[] { -0.125, 0.125 }, Nums(l["core"]));
        Assert.Equal(new[] { corner }, Ids(l["joins"]!["end"]));
        var ids = All(r).Select(x => (string?)x["id"]).ToList();
        Assert.DoesNotContain(curved, ids);
        Assert.DoesNotContain(above, ids);
        Assert.Equal(before, f.Model.Revision);
        Assert.Throws<CommandException>(() => Lines(f, new JsonObject()));
        Assert.Empty(All(Lines(f, new JsonObject { ["storeyId"] = f.Storey, ["kinds"] = new JsonArray("slab") })));
    }
}
