using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Fillet and chamfer on walls (a user's request): two walls that meet, or would meet carried on, trimmed back to a curved
/// wall tangent to both (a chamfer: a straight one) added between and joined to both, in one undo step; no radius makes a
/// sharp corner; the two walls keep their GlobalIds and the openings their place in plan.
/// </summary>
public sealed class WallFilletTests
{
    static JsonObject Args(string a, string b, double radius, params (string K, JsonNode V)[] more)
    {
        var o = new JsonObject { ["a"] = a, ["b"] = b, ["radius"] = radius };
        foreach (var (k, v) in more) o[k] = v;
        return o;
    }

    static (Fixture F, string A, string B) Corner(double? heightB = null)
    {
        var f = new Fixture();
        var a = f.Wall(0, 0, 5, 0, height: 3);
        var b = f.Wall(5, 0, 5, 4, height: heightB ?? 3);
        return (f, a, b);
    }

    static double Thickness(Fixture f, string id) { var bands = Walls.Section(f.Model, f.Model.Get(id)!); return bands.Max(x => x.To) - bands.Min(x => x.From); }
    static Vec2 S(Fixture f, string id) => Walls.Start(f.Model.Get(id)!);
    static Vec2 E(Fixture f, string id) => Walls.End(f.Model.Get(id)!);
    static void Near(Vec2 want, Vec2 got) { Assert.Equal(want.X, got.X, 6); Assert.Equal(want.Y, got.Y, 6); }

    [Fact]
    public void A_fillet_trims_both_walls_and_adds_a_curved_wall_tangent_to_both_joined_in_one_step()
    {
        var (f, a, b) = Corner();
        Assert.NotNull(Joins.Between(f.Model, a, b));
        var walls = f.Model.OfClass(IfcClass.Wall).Count();
        var r = f.Run("architecture.filletWalls", Args(a, b, 1))!;
        var arc = (string)r["id"]!;
        // The walls keep their ids, trimmed back 1 m from the corner (a right angle: the tangent points are a radius away).
        Near(new(4, 0), E(f, a));
        Near(new(5, 1), S(f, b));
        Near(new(0, 0), S(f, a));
        var c = PathCurve.Of(f.Model.Get(arc)!);
        Assert.True(c.IsArc);
        Assert.Equal(1, c.Radius, 6);
        Near(new(4, 1), c.Centre);
        Near(new(4, 0), c.Start);
        Near(new(5, 1), c.End);
        Assert.Equal(Math.PI / 2, c.Sweep, 6);
        // The same type, height and base as the first wall; a usage of its own.
        Assert.Equal(Walls.TypeOf(f.Model, a)!.Id, Walls.TypeOf(f.Model, arc)!.Id);
        Assert.Equal(3, Walls.HeightOf(f.Model, f.Model.Get(arc)!), 9);
        Assert.NotEqual(Walls.UsageOf(f.Model, a)!.Id, Walls.UsageOf(f.Model, arc)!.Id);
        // Joined to both, the old corner gone.
        Assert.Null(Joins.Between(f.Model, a, b));
        Assert.NotNull(Joins.Between(f.Model, a, arc));
        Assert.NotNull(Joins.Between(f.Model, arc, b));
        Assert.True(f.Session.Geometry(arc)!.Parts.Sum(p => Measure.Volume(p.Solid)) > 0);
        // The body of the arc: a quarter ring of the wall's thickness round its axis's radius, as tall as the wall.
        var t = Thickness(f, a);
        Assert.Equal(Math.PI / 2 * 1 * t * 3, f.Session.Geometry(arc)!.Parts.Sum(p => Measure.Volume(p.Solid)), 2);
        // One undo step.
        f.Session.Undo();
        Assert.Equal(walls, f.Model.OfClass(IfcClass.Wall).Count());
        Near(new(5, 0), E(f, a));
        Assert.NotNull(Joins.Between(f.Model, a, b));
    }

    [Fact]
    public void Walls_that_do_not_meet_yet_are_carried_on_to_the_fillet_as_CAD_does_and_no_radius_makes_a_sharp_corner()
    {
        var f = new Fixture();
        var a = f.Wall(0, 0, 3, 0, height: 3);
        var b = f.Wall(5, 1.5, 5, 4, height: 3);
        Assert.Null(Joins.Between(f.Model, a, b));
        f.Run("architecture.filletWalls", Args(a, b, 1));
        Near(new(4, 0), E(f, a));
        Near(new(5, 1), S(f, b));
        f.Session.Undo();
        Assert.Null(f.Run("architecture.filletWalls", Args(a, b, 0))!["id"]);
        Near(new(5, 0), E(f, a));
        Near(new(5, 0), S(f, b));
        var j = Joins.Between(f.Model, a, b)!;
        Assert.Equal("L", (string?)j.Attrs!["kind"]);
        Assert.Equal(2, f.Model.OfClass(IfcClass.Wall).Count());
    }

    [Fact]
    public void The_part_kept_is_the_one_picked_and_a_crossing_is_trimmed_to_it()
    {
        var f = new Fixture();
        // Two walls crossing at (5, 0): A from 0 to 8, B from −3 to 4 up the y axis at x = 5.
        var a = f.Wall(0, 0, 8, 0, height: 3);
        var b = f.Wall(5, -3, 5, 4, height: 3);
        f.Run("architecture.filletWalls", Args(a, b, 0.5, ("atA", new JsonArray(1, 0)), ("atB", new JsonArray(5, 3))));
        Near(new(0, 0), S(f, a));
        Near(new(4.5, 0), E(f, a));
        Near(new(5, 0.5), S(f, b));
        Near(new(5, 4), E(f, b));
    }

    [Fact]
    public void A_chamfer_adds_a_straight_wall_cutting_the_corner()
    {
        var (f, a, b) = Corner();
        var r = f.Run("architecture.chamferWalls", new JsonObject { ["a"] = a, ["b"] = b, ["distance"] = 1.0 })!;
        var piece = f.Model.Get((string)r["id"]!)!;
        Assert.False(PathCurve.Of(piece).IsArc);
        Near(new(4, 0), E(f, a));
        Near(new(5, 1), S(f, b));
        Assert.Equal(Math.Sqrt(2), (Walls.End(piece) - Walls.Start(piece)).Length, 6);
        Assert.NotNull(Joins.Between(f.Model, a, piece.Id));
        Assert.NotNull(Joins.Between(f.Model, piece.Id, b));
    }

    [Fact]
    public void The_radius_may_be_the_inside_or_the_outside_face_s()
    {
        var (f, a, b) = Corner();
        // A turns left into B: the inside of the bend is A's left face (its axis on its core's middle, the faces as its layers lie).
        var bands = Walls.Section(f.Model, f.Model.Get(a)!);
        var (left, right) = (bands.Max(x => x.To), bands.Min(x => x.From));
        var r = f.Run("architecture.filletWalls", Args(a, b, 1, ("line", "inside")))!;
        Assert.Equal(1 + left, PathCurve.Of(f.Model.Get((string)r["id"]!)!).Radius, 6);
        f.Session.Undo();
        r = f.Run("architecture.filletWalls", Args(a, b, 1, ("line", "outside")))!;
        Assert.Equal(1 + right, PathCurve.Of(f.Model.Get((string)r["id"]!)!).Radius, 6);
        f.Session.Undo();
        // Smaller than the inside face's offset on the axis: the inside face would turn round nothing.
        Assert.Throws<CommandException>(() => f.Run("architecture.filletWalls", Args(a, b, left / 2)));
    }

    [Fact]
    public void Walls_that_differ_are_refused_unless_the_first_one_s_is_taken_and_a_radius_too_large_is_refused()
    {
        var (f, a, b) = Corner(heightB: 2.5);
        var e = Assert.Throws<CommandException>(() => f.Run("architecture.filletWalls", Args(a, b, 1)));
        Assert.Contains("differ", e.Message);
        var r = f.Run("architecture.filletWalls", Args(a, b, 1, ("matchFirst", true)))!;
        Assert.Equal(3, Walls.HeightOf(f.Model, f.Model.Get((string)r["id"]!)!), 9);
        f.Session.Undo();
        Assert.Throws<CommandException>(() => f.Run("architecture.filletWalls", Args(a, b, 4.5, ("matchFirst", true))));
        // Parallel walls never meet.
        var g = new Fixture();
        var p = g.Wall(0, 0, 4, 0, height: 3);
        var q = g.Wall(0, 2, 4, 2, height: 3);
        Assert.Throws<CommandException>(() => g.Run("architecture.filletWalls", Args(p, q, 1)));
    }

    [Fact]
    public void Openings_stay_where_they_are_in_plan_and_one_in_the_part_cut_away_refuses_the_fillet()
    {
        var (f, a, b) = Corner();
        // B runs from the corner: its start moves, its window keeps its place in plan (along goes down by the trim).
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = b, ["x"] = 5, ["y"] = 2.5 });
        var opening = Openings.Of(f.Model, b).Single();
        Assert.Equal(2.5, opening.Num("along"), 6);
        f.Run("architecture.filletWalls", Args(a, b, 0.5));
        Assert.Equal(2.0, f.Model.Get(opening.Id)!.Num("along"), 6);
        f.Session.Undo();
        // A door near the corner on A would fall into what is cut away.
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["x"] = 4.3, ["y"] = 0 });
        Assert.Equal(4.3, Openings.Of(f.Model, a).Single().Num("along"), 6);
        var e = Assert.Throws<CommandException>(() => f.Run("architecture.filletWalls", Args(a, b, 1.5)));
        Assert.Contains("cut away", e.Message);
        // A smaller radius keeps it.
        f.Run("architecture.filletWalls", Args(a, b, 0.2));
    }

    [Fact]
    public void Curved_walls_are_refused()
    {
        var f = new Fixture();
        var a = f.Wall(0, 0, 5, 0, height: 3);
        var b = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 5, ["y1"] = 0, ["x2"] = 5, ["y2"] = 4, ["arc"] = 0.5, ["height"] = 3 })!["id"]!;
        Assert.Contains("curved", Assert.Throws<CommandException>(() => f.Run("architecture.filletWalls", Args(a, b, 1))).Message);
    }

    [Fact]
    public void Trim_and_extend_work_on_straight_walls()
    {
        var f = new Fixture();
        // Wall across (0, 0) -> (6, 0) crossed at x = 2 and x = 4.
        var w = f.Wall(0, 0, 6, 0, height: 3);
        f.Wall(2, -2, 2, 2, height: 3);
        f.Wall(4, -2, 4, 2, height: 3);

        // Trim the overhanging right end (5, 0) -> shortens end to x = 4.
        f.Run("architecture.trimWall", new JsonObject { ["id"] = w, ["at"] = new JsonArray(5, 0) });
        Assert.Equal(4, Walls.End(f.Model.Get(w)!).X, 6);

        // Extend the right end back to a new wall at x = 6.
        f.Wall(6, -2, 6, 2, height: 3);
        f.Run("architecture.extendWall", new JsonObject { ["id"] = w, ["at"] = new JsonArray(3.5, 0) });
        Assert.Equal(6, Walls.End(f.Model.Get(w)!).X, 6);

        // Trim the middle segment between x = 2 and x = 4 -> splits into two walls (0..2 and 4..6).
        var split = (JsonObject)f.Run("architecture.trimWall", new JsonObject { ["id"] = w, ["at"] = new JsonArray(3, 0) })!;
        Assert.Equal(2, ((JsonArray)split["ids"]!).Count);
        Assert.Equal(2, Walls.End(f.Model.Get(w)!).X, 6);
        var second = f.Model.Get((string)((JsonArray)split["ids"]!)[1]!)!;
        Assert.Equal(4, Walls.Start(second).X, 6);
        Assert.Equal(6, Walls.End(second).X, 6);
    }
}
