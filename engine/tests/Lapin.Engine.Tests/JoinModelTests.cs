using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Structure;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Joins as the model keeps them (docs/decisions-pending.md #17–#19, #43):
/// walls join as they are drawn, a join is only an IfcRelConnectsPathElements
/// and the cuts are worked out from both walls whenever geometry is built, so
/// edits keep corners clean and a join that no longer holds is dropped.
/// </summary>
public class JoinModelTests
{
    const double Height = 3;
    /// <summary>The default type: render 20 mm on the right face, a 250 mm core on the axis, 15 mm plaster on the left.</summary>
    const double RightReach = 0.145, LeftReach = 0.14;

    static double Volume(Fixture f, string wall) => f.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static List<Relation> JoinRels(Fixture f) => f.Model.Relations.Where(r => r.Class == Joins.Class).ToList();
    static Entity Get(Fixture f, string id) => f.Model.Get(id)!;
    static string Kind(Relation r) => (string?)r.Attrs!["kind"] ?? "";
    static string Style(Relation r) => (string?)r.Attrs!["style"] ?? "";

    /// <summary>Four walls around a 5 × 3.5 room, drawn clockwise from (2, 1): the right face, with the render, is inside.</summary>
    static readonly Vec2[] Corners = [new(2, 1), new(2, 4.5), new(7, 4.5), new(7, 1)];

    static (Fixture F, string[] Ids) ClockwiseRoom(string? joinStyle = null, bool join = true)
    {
        var f = new Fixture();
        var ids = new string[Corners.Length];
        for (var i = 0; i < Corners.Length; i++)
        {
            var from = Corners[i];
            var to = Corners[(i + 1) % Corners.Length];
            var args = new JsonObject { ["storeyId"] = f.Storey, ["x1"] = from.X, ["y1"] = from.Y, ["x2"] = to.X, ["y2"] = to.Y };
            if (joinStyle != null) args["joinStyle"] = joinStyle;
            if (!join) args["join"] = false;
            ids[i] = (string)f.Run("architecture.addWall", args)!["id"]!;
        }
        return (f, ids);
    }

    /// <summary>The masonry ring of the room: the outside grows by the left reach, the inside shrinks by the right reach.</summary>
    static double RingVolume()
    {
        var outer = (5 + 2 * LeftReach) * (3.5 + 2 * LeftReach);
        var inner = (5 - 2 * RightReach) * (3.5 - 2 * RightReach);
        return (outer - inner) * Height;
    }

    [Fact]
    public void Walls_drawn_around_a_room_join_at_every_corner_and_their_bodies_tile_the_ring()
    {
        var (f, ids) = ClockwiseRoom("butt");
        var rels = JoinRels(f);
        Assert.Equal(4, rels.Count);
        Assert.All(rels, r => Assert.Equal("L", Kind(r)));
        Assert.All(rels, r => Assert.Equal("butt", Style(r)));
        Assert.Equal(RingVolume(), ids.Sum(id => Volume(f, id)), 6);
    }

    [Fact]
    public void Walls_drawn_with_no_style_asked_meet_mitred_at_the_corners_and_butt_at_a_T()
    {
        // Decision 175: a corner joins by itself as a mitre; Butt is for when the mitre is not wanted. A T is always butt.
        var (f, ids) = ClockwiseRoom();
        Assert.All(JoinRels(f), r => Assert.Equal(("L", "mitre"), (Kind(r), Style(r))));
        Assert.Equal(RingVolume(), ids.Sum(id => Volume(f, id)), 6);
        var t = f.Wall(2, 3, 7, 3);
        var tee = JoinRels(f).Where(r => r.Touches(t)).ToList();
        Assert.Equal(2, tee.Count);
        Assert.All(tee, r => Assert.Equal(("T", "butt"), (Kind(r), Style(r))));
        var beam = new Fixture(new ArchitectureModule(), new StructureModule());
        beam.Run("structure.addBeam", new JsonObject { ["storeyId"] = beam.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0 });
        beam.Run("structure.addBeam", new JsonObject { ["storeyId"] = beam.Storey, ["x1"] = 5, ["y1"] = 0, ["x2"] = 5, ["y2"] = 4 });
        Assert.Equal("mitre", Style(Assert.Single(JoinRels(beam))));
    }

    [Fact]
    public void Drawn_with_joining_off_the_same_walls_stay_apart_and_double_up_at_the_corners()
    {
        var (f, ids) = ClockwiseRoom(join: false);
        Assert.Empty(JoinRels(f));
        var total = ids.Sum(id => Volume(f, id));
        Assert.True(total > RingVolume() + 1e-6, $"{total} should exceed the ring {RingVolume()}");
    }

    [Fact]
    public void A_room_drawn_with_mitred_corners_tiles_the_ring_too_and_can_be_restyled()
    {
        var (f, ids) = ClockwiseRoom("mitre");
        Assert.All(JoinRels(f), r => Assert.Equal("mitre", Style(r)));
        Assert.Equal(RingVolume(), ids.Sum(id => Volume(f, id)), 6);

        // Moving a corner keeps the style of the joins it touches.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = ids[2], ["y2"] = 0.4 });
        Assert.All(JoinRels(f), r => Assert.Equal("mitre", Style(r)));

        f.Run("architecture.restyleJoins", new JsonObject { ["ids"] = new JsonArray(ids.Select(i => (JsonNode)i).ToArray()), ["style"] = "butt" });
        Assert.All(JoinRels(f), r => Assert.Equal("butt", Style(r)));
        Assert.Equal(4, JoinRels(f).Count);
    }

    [Fact]
    public void A_wall_drawn_onto_the_body_of_another_makes_a_T_with_the_crossed_wall_through()
    {
        var f = new Fixture();
        var crossed = f.Wall(1, 3, 9, 3);
        var crossedBefore = f.Bounds(crossed);
        // Drawn from above down into the plaster of the crossed wall, 40 mm short of its axis.
        var ending = f.Wall(5, 7, 5, 3.04);

        var rel = Assert.Single(JoinRels(f));
        Assert.Equal("T", Kind(rel));
        Assert.Equal(crossed, rel.Relating);
        Assert.Equal("ATPATH", (string?)rel.Attrs!["relatingConnection"]);
        Assert.Equal(new Vec2(5, 3), Walls.End(Get(f, ending)));
        Assert.Equal(crossedBefore, f.Bounds(crossed));
        // The ending wall's body stops on the crossed wall's left face (its plaster side, 140 mm off the axis).
        Assert.Equal(3 + LeftReach, f.Bounds(ending).Min.Y, 9);
    }

    [Fact]
    public void Moving_a_corner_carries_the_neighbour_s_end_along_and_undo_puts_both_back()
    {
        var (f, ids) = ClockwiseRoom();
        // The corner at (7, 4.5) is the end of wall 1 and the start of wall 2.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = ids[1], ["x2"] = 7.6, ["y2"] = 5.2 });
        Assert.Equal(new Vec2(7.6, 5.2), Walls.End(Get(f, ids[1])));
        Assert.Equal(new Vec2(7.6, 5.2), Walls.Start(Get(f, ids[2])));
        Assert.Equal(new Vec2(2, 4.5), Walls.Start(Get(f, ids[1])));
        Assert.Equal(new Vec2(7, 1), Walls.End(Get(f, ids[2])));
        Assert.Equal(4, JoinRels(f).Count);

        f.Session.Undo();
        Assert.Equal(new Vec2(7, 4.5), Walls.End(Get(f, ids[1])));
        Assert.Equal(new Vec2(7, 4.5), Walls.Start(Get(f, ids[2])));
        Assert.Equal(4, JoinRels(f).Count);
    }

    [Fact]
    public void With_joined_ends_held_the_dragged_end_returns_to_the_corner_the_neighbour_keeps()
    {
        var (f, ids) = ClockwiseRoom();
        f.Run("architecture.updateWall", new JsonObject { ["id"] = ids[1], ["x2"] = 7.3, ["moveJoinedEnds"] = false });
        Assert.Equal(new Vec2(7, 4.5), Walls.Start(Get(f, ids[2])));
        Assert.Equal(new Vec2(7, 4.5), Walls.End(Get(f, ids[1])));
        Assert.Equal(4, JoinRels(f).Count);
    }

    [Fact]
    public void Detached_only_the_dragged_end_moves_and_the_corner_join_goes_when_they_no_longer_meet()
    {
        var (f, ids) = ClockwiseRoom();
        // Pulled 0.3 m along its own line it still meets the neighbour: the corner holds, rewritten to the crossing.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = ids[1], ["x2"] = 7.3, ["detachJoinedEnds"] = true });
        Assert.Equal(4, JoinRels(f).Count);
        // Pulled well away: the neighbour stays, the two no longer meet, the corner join goes.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = ids[1], ["x2"] = 8.5, ["y2"] = 6, ["detachJoinedEnds"] = true });
        Assert.Equal(new Vec2(8.5, 6), Walls.End(Get(f, ids[1])));
        Assert.Equal(new Vec2(7, 4.5), Walls.Start(Get(f, ids[2])));
        Assert.True(JoinRels(f).Count < 4, "the corner pulled apart is not a join any more");
        // Dragged back onto the corner: they meet again and join.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = ids[1], ["x2"] = 7, ["y2"] = 4.5, ["detachJoinedEnds"] = true });
        Assert.Equal(4, JoinRels(f).Count);
    }

    [Fact]
    public void When_the_crossed_wall_is_moved_away_the_T_is_dropped_and_the_ending_wall_ends_square()
    {
        var f = new Fixture();
        var crossed = f.Wall(1, 3, 9, 3);
        var ending = f.Wall(5, 7, 5, 3);
        Assert.Single(JoinRels(f));
        Assert.Equal(3 + LeftReach, f.Bounds(ending).Min.Y, 9);

        f.Run("architecture.updateWall", new JsonObject { ["id"] = crossed, ["y1"] = 12, ["y2"] = 12 });
        Assert.Empty(JoinRels(f));
        Assert.Equal(3, f.Bounds(ending).Min.Y, 9);
    }

    [Fact]
    public void Joining_a_pair_again_rewrites_the_same_relationship_and_swap_turns_it_round()
    {
        var f = new Fixture();
        var a = f.Wall(1, 1, 1, 4);
        var b = f.Wall(1, 4, 4, 4);
        var first = Assert.Single(JoinRels(f));

        f.Run("architecture.joinWalls", new JsonObject { ["a"] = b, ["b"] = a });
        var again = Assert.Single(JoinRels(f));
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(first.Relating, again.Relating);

        f.Run("architecture.restyleJoins", new JsonObject { ["id"] = a, ["swap"] = true });
        var swapped = Assert.Single(JoinRels(f));
        Assert.NotEqual(first.Relating, swapped.Relating);
        Assert.Contains(swapped.Relating, new[] { a, b });
    }

    [Fact]
    public void A_join_that_is_refused_changes_nothing()
    {
        var f = new Fixture();
        var a = f.Wall(1, 1, 1, 4);
        f.Wall(1, 4, 4, 4);
        var elsewhere = f.Wall(6, 1, 6, 4);
        var before = JoinRels(f).Select(r => (r.Id, r.Relating, r.Related[0])).ToList();
        var e = Assert.Throws<CommandException>(() => f.Run("architecture.joinWalls", new JsonObject { ["a"] = a, ["b"] = elsewhere }));
        Assert.Matches("not in line|do not meet", e.Message);
        Assert.Equal(before, JoinRels(f).Select(r => (r.Id, r.Relating, r.Related[0])).ToList());
    }

    [Fact]
    public void Deleting_a_wall_drops_its_joins_and_squares_the_wall_it_left()
    {
        var f = new Fixture();
        var stays = f.Wall(1, 1, 1, 4);
        var goes = f.Wall(1, 4, 4, 4);
        var joined = f.Bounds(stays);
        f.Run("element.delete", new JsonObject { ["id"] = goes });
        Assert.Empty(JoinRels(f));
        Assert.Equal(4, f.Bounds(stays).Max.Y, 9);
        Assert.NotEqual(joined, f.Bounds(stays));
    }

    [Fact]
    public void Collapsing_a_joined_wall_to_a_point_is_refused_and_leaves_the_model_as_it_was()
    {
        var (f, ids) = ClockwiseRoom();
        var entities = ((ModelStore)f.Model).EntityCount;
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWall", new JsonObject { ["id"] = ids[3], ["x2"] = 7, ["y2"] = 1 }));
        Assert.Equal(entities, ((ModelStore)f.Model).EntityCount);
        Assert.Equal(new Vec2(2, 1), Walls.End(Get(f, ids[3])));
        Assert.Equal(4, JoinRels(f).Count);
    }

    [Fact]
    public void Joins_come_back_from_a_saved_file_with_the_same_bodies()
    {
        var (f, ids) = ClockwiseRoom();
        var dir = Directory.CreateTempSubdirectory("lapin-join-model-").FullName;
        try
        {
            var path = Path.Combine(dir, "clockwise.lapin");
            Lapin.Host.PackageFiles.Create().Save(f.Session, path);
            var g = new Fixture();
            Lapin.Host.PackageFiles.Open(g.Session, path);
            Assert.Equal(4, JoinRels(g).Count);
            foreach (var id in ids) Assert.Equal(f.Bounds(id), g.Bounds(id));
            Assert.Equal(RingVolume(), ids.Sum(id => Volume(g, id)), 6);
        }
        finally { Directory.Delete(dir, true); }
    }
}
