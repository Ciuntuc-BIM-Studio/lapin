using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// What a selected wall's temporary dimensions measure to (decision 693): every straight wall of the storey as a line, its
/// faces and its core's faces left of its axis, and what is joined at each of its ends. The interface works out the parallel
/// walls either side and the walls an end runs toward from these, for a wall selected or one being drawn.
/// </summary>
public class WallLinesTests
{
    static JsonObject Lines(Fixture f, JsonObject a) => f.Session.Query("architecture.wallLines", a)!.AsObject();
    static JsonObject WallIn(JsonObject r, string id) => r["walls"]!.AsArray().Select(w => w!.AsObject()).Single(w => (string?)w["id"] == id);
    static double[] Nums(JsonNode? n) => n!.AsArray().Select(x => Math.Round((double)x!, 6)).ToArray();
    static string[] Ids(JsonNode? n) => n?.AsArray().Select(x => (string)x!).OrderBy(x => x).ToArray() ?? [];

    [Fact]
    public void Each_straight_wall_of_the_storey_comes_with_its_axis_faces_and_core_faces()
    {
        var f = new Fixture();
        var brick = f.Type("Brick 300", ("Brick", 0.3, true));
        var layered = f.Type("Plastered brick", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        var a = f.Wall(0, 0, 6, 0, brick);
        var b = f.Wall(0, 3, 6, 3, layered);
        var r = Lines(f, new JsonObject { ["storeyId"] = f.Storey });

        Assert.Equal(f.Storey, (string?)r["storeyId"]);
        var wa = WallIn(r, a);
        Assert.Equal(new[] { 0.0, 0.0 }, Nums(wa["start"]));
        Assert.Equal(new[] { 6.0, 0.0 }, Nums(wa["end"]));
        Assert.Equal(new[] { -0.15, 0.15 }, Nums(wa["faces"]));
        Assert.Null(wa["core"]);                              // one layer: the core is the whole wall

        // The axis on the core's centre: the plaster (exterior) to the right, the insulation to the left.
        var wb = WallIn(r, b);
        Assert.Equal(new[] { -0.145, 0.225 }, Nums(wb["faces"]));
        Assert.Equal(new[] { -0.125, 0.125 }, Nums(wb["core"]));
        Assert.Equal((string?)f.Model.Get(b)!.Name, (string?)wb["name"]);
    }

    [Fact]
    public void A_flipped_wall_has_its_faces_the_other_way_round()
    {
        var f = new Fixture();
        var layered = f.Type("Plastered brick", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        var w = f.Wall(0, 0, 6, 0, layered);
        f.Run("architecture.flipWall", new JsonObject { ["ids"] = new JsonArray(w) });
        var r = Lines(f, new JsonObject { ["id"] = w });
        Assert.Equal(new[] { -0.225, 0.145 }, Nums(WallIn(r, w)["faces"]));
    }

    [Fact]
    public void What_is_joined_at_each_end_is_said()
    {
        var f = new Fixture();
        var t = f.Type("Brick 300", ("Brick", 0.3, true));
        var host = f.Wall(0, 0, 6, 0, t);
        var corner = f.Wall(6, 0, 6, 4, t);                   // an L at the host's end
        var tee = f.Wall(3, 0, 3, 4, t);                      // a T on the host's side
        var r = Lines(f, new JsonObject { ["id"] = host });
        var h = WallIn(r, host);
        Assert.Equal([], Ids(h["joins"]!["start"]));
        Assert.Equal(new[] { corner }, Ids(h["joins"]!["end"]));
        Assert.Contains(tee, Ids(h["joins"]!["path"]));
        Assert.Equal(new[] { host }, Ids(WallIn(r, tee)["joins"]!["start"]));
    }

    [Fact]
    public void Curved_walls_and_other_storeys_walls_are_left_out_and_nothing_changes()
    {
        var f = new Fixture();
        var t = f.Type("Brick 300", ("Brick", 0.3, true));
        var straight = f.Wall(0, 0, 6, 0, t);
        var curved = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 5, ["x2"] = 6, ["y2"] = 5, ["x3"] = 3, ["y3"] = 6, ["typeId"] = t })!["id"]!;
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var above = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = upper, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["typeId"] = t })!["id"]!;
        var before = f.Model.Revision;
        var r = Lines(f, new JsonObject { ["storeyId"] = f.Storey });
        var ids = r["walls"]!.AsArray().Select(w => (string?)w!["id"]).ToList();
        Assert.Contains(straight, ids);
        Assert.DoesNotContain(curved, ids);
        Assert.DoesNotContain(above, ids);
        Assert.Equal(before, f.Model.Revision);
        Assert.Throws<CommandException>(() => Lines(f, new JsonObject()));
    }
}
