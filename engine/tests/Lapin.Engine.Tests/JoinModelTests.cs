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

    [Theory]
    [InlineData("butt")]
    [InlineData("mitre")]
    public void At_every_corner_each_layer_turns_it_unbroken_so_each_layer_alone_tiles_its_own_ring(string style)
    {
        // Report #49: a corner of layered walls looked unprofessional, the outer finish of one wall stopping against the
        // other's core. Each layer now meets its like: the render, the core and the plaster each make a closed ring, butt or mitred.
        var (f, ids) = ClockwiseRoom(style);
        var bands = Walls.Section(f.Model, Get(f, ids[0]));
        foreach (var band in bands)
        {
            // Clockwise, the left is outside: a band from a to b (left of the axis positive) rings the room between those offsets.
            double Rect(double d) => (5 + 2 * d) * (3.5 + 2 * d);
            var ring = (Rect(band.To) - Rect(band.From)) * Height;
            var layer = ids.Sum(id => f.Session.Geometry(id)!.Parts.Where(p => p.Key == $"layer:{band.Index}").Sum(p => Measure.Volume(p.Solid)));
            Assert.Equal(ring, layer, 6);
        }
        Assert.Equal(RingVolume(), ids.Sum(id => Volume(f, id)), 6);
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
    public void Restyling_one_corner_recuts_both_of_its_walls()
    {
        var f = new Fixture();
        var a = f.Wall(0, 0, 5, 0, joinStyle: "mitre");
        var b = f.Wall(5, 0, 5, 4, joinStyle: "mitre");
        Assert.Single(JoinRels(f));
        var relating = JoinRels(f)[0].Relating;
        var before = new[] { Volume(f, a), Volume(f, b) };   // both built and cached
        f.Run("architecture.restyleJoins", new JsonObject { ["ids"] = new JsonArray(a, b), ["style"] = "butt" });
        var join = Assert.Single(JoinRels(f));
        Assert.Equal("butt", Style(join));
        Assert.Equal(relating, join.Relating);   // only the attributes changed
        Assert.NotEqual(before[1], Volume(f, b), 6);
        // The relating wall and the related one alike: the cached geometry is what a fresh build makes.
        foreach (var id in new[] { a, b })
            Assert.Equal(f.Session.Geometry(id, new GeometryOptions())!.Parts.Sum(p => Measure.Volume(p.Solid)), Volume(f, id), 9);
    }

    [Fact]
    public void A_corner_dragged_onto_the_other_walls_far_end_is_refused()
    {
        var f = new Fixture();
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(5, 0, 5, 4);
        Assert.Single(JoinRels(f));
        // b's joined start would come along to (5, 4), its own end: no length left.
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWall", new JsonObject { ["id"] = a, ["x2"] = 5, ["y2"] = 4 }));
        Assert.Equal(new Vec2(5, 0), Joins.Start(Get(f, b)));
        Assert.Single(JoinRels(f));
    }

    [Fact]
    public void A_join_that_no_longer_meets_does_not_refuse_the_restyle_of_the_others()
    {
        var f = new Fixture();
        var a = f.Wall(0, 0, 5, 0, joinStyle: "mitre");
        var b = f.Wall(5, 0, 5, 4, joinStyle: "mitre");
        var c = f.Wall(5, 4, 0, 4, joinStyle: "mitre");
        Assert.Equal(2, JoinRels(f).Count);
        // a moved far away behind the joins' back: its join with b is stale.
        f.Session.Edit("away", e =>
        {
            var wall = e.Model.Get(a)!;
            var attrs = wall.CloneAttrs();
            attrs["start"] = new JsonArray(20, 20);
            attrs["end"] = new JsonArray(25, 20);
            e.Replace(wall.With(attrs: attrs));
        });
        f.Run("architecture.restyleJoins", new JsonObject { ["ids"] = new JsonArray(a, b, c), ["style"] = "butt" });
        Assert.Equal("butt", Style(JoinRels(f).Single(r => r.Touches(c))));
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
        // Decision 255: the ending wall's core runs on through the crossed wall's plaster to its core (125 mm off the axis);
        // its own render and plaster stop on the crossed wall's left face (its plaster, 140 mm off), where they meet their like.
        Assert.Equal(3 + Core / 2, f.Bounds(ending).Min.Y, 9);
        Assert.Equal(3 + Core / 2, LayerBounds(f, ending, Core).Min.Y, 9);
        Assert.Equal(3 + LeftReach, LayerBounds(f, ending, Plaster).Min.Y, 9);
        Assert.Equal(3 + LeftReach, LayerBounds(f, ending, Render).Min.Y, 9);
        // The crossed wall's plaster is interrupted where the core comes through: 8 m less the core's 250 mm; its core and render are whole.
        Assert.Equal((8 - Core) * Plaster * Height, LayerVolume(f, crossed, Plaster), 9);
        Assert.Equal(8 * Core * Height, LayerVolume(f, crossed, Core), 9);
        Assert.Equal(8 * Render * Height, LayerVolume(f, crossed, Render), 9);
        Assert.Equal(2, LayerParts(f, crossed, Plaster).Count);
    }

    [Fact]
    public void A_wall_drawn_across_another_makes_an_X_with_the_one_already_there_through_and_each_layer_stops_at_its_like()
    {
        // Decision 255: two walls crossing join as an X, neither split. The one already there runs through whole; the new
        // one's core stops at its core's faces, its render and plaster at its outer faces; its plaster and render interrupted by the new core.
        var f = new Fixture();
        var through = f.Wall(0, 3, 8, 3);
        var throughBefore = f.Bounds(through);
        var crossing = f.Wall(4, 0, 4, 6);
        var rel = Assert.Single(JoinRels(f));
        Assert.Equal("X", Kind(rel));
        Assert.Equal(through, rel.Relating);
        Assert.Equal("ATPATH", (string?)rel.Attrs!["relatingConnection"]);
        Assert.Equal("ATPATH", (string?)rel.Attrs!["relatedConnection"]);
        Assert.Equal(new Vec2(4, 0), Walls.Start(Get(f, crossing)));
        Assert.Equal(new Vec2(4, 6), Walls.End(Get(f, crossing)));
        Assert.Equal(throughBefore, f.Bounds(through));
        Assert.Equal(8 * Core * Height, LayerVolume(f, through, Core), 9);
        Assert.Equal((8 - Core) * Plaster * Height, LayerVolume(f, through, Plaster), 9);
        Assert.Equal((8 - Core) * Render * Height, LayerVolume(f, through, Render), 9);
        var thickness = LeftReach + RightReach;
        Assert.Equal((6 - Core) * Core * Height, LayerVolume(f, crossing, Core), 9);
        Assert.Equal((6 - thickness) * Plaster * Height, LayerVolume(f, crossing, Plaster), 9);
        Assert.Equal((6 - thickness) * Render * Height, LayerVolume(f, crossing, Render), 9);
        Assert.Equal(2, LayerParts(f, crossing, Core).Count);
        // Nothing is built twice: together they fill exactly the union of the two bodies.
        var union = 8 * thickness * Height + 6 * thickness * Height - thickness * thickness * Height;
        Assert.Equal(union, Volume(f, through) + Volume(f, crossing), 9);
        // Swapped, the new one runs through; moved off the other, the crossing is no join and both are whole again.
        f.Run("architecture.restyleJoins", new JsonObject { ["ids"] = new JsonArray(crossing), ["swap"] = true });
        Assert.Equal(crossing, Assert.Single(JoinRels(f)).Relating);
        Assert.Equal(6 * Core * Height, LayerVolume(f, crossing, Core), 9);
        f.Run("architecture.updateWall", new JsonObject { ["id"] = crossing, ["x1"] = 12, ["x2"] = 12 });
        Assert.Empty(JoinRels(f));
        Assert.Equal(6 * thickness * Height, Volume(f, crossing), 9);
    }

    const double Core = 0.25, Render = 0.02, Plaster = 0.015;

    /// <summary>The wall's parts of the layer <paramref name="thickness"/> thick (the default type's layers differ in thickness).</summary>
    static List<GeometryPart> LayerParts(Fixture f, string wall, double thickness)
    {
        var band = Walls.Section(f.Model, Get(f, wall)).Single(b => Math.Abs(b.To - b.From - thickness) < 1e-9);
        return f.Session.Geometry(wall)!.Parts.Where(p => p.Key == $"layer:{band.Index}").ToList();
    }

    static double LayerVolume(Fixture f, string wall, double thickness) => LayerParts(f, wall, thickness).Sum(p => Measure.Volume(p.Solid));

    static (Vec3 Min, Vec3 Max) LayerBounds(Fixture f, string wall, double thickness)
    {
        var v = LayerParts(f, wall, thickness).SelectMany(p => p.Solid.Vertices).ToList();
        return (new(v.Min(q => q.X), v.Min(q => q.Y), v.Min(q => q.Z)), new(v.Max(q => q.X), v.Max(q => q.Y), v.Max(q => q.Z)));
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
        Assert.Equal(3 + Core / 2, f.Bounds(ending).Min.Y, 9);

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

    static (Vec3 Min, Vec3 Max) BoundsOf(Fixture f, string id)
    {
        var v = f.Session.Geometry(id)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        return (new(v.Min(q => q.X), v.Min(q => q.Y), v.Min(q => q.Z)), new(v.Max(q => q.X), v.Max(q => q.Y), v.Max(q => q.Z)));
    }

    static void SameBounds((Vec3 Min, Vec3 Max) want, (Vec3 Min, Vec3 Max) got)
    {
        Assert.Equal(want.Min.X, got.Min.X, 6); Assert.Equal(want.Min.Y, got.Min.Y, 6); Assert.Equal(want.Min.Z, got.Min.Z, 6);
        Assert.Equal(want.Max.X, got.Max.X, 6); Assert.Equal(want.Max.Y, got.Max.Y, 6); Assert.Equal(want.Max.Z, got.Max.Z, 6);
    }

    /// <summary>Bounds mirrored about the vertical line x = <paramref name="x"/>.</summary>
    static (Vec3 Min, Vec3 Max) MirroredX((Vec3 Min, Vec3 Max) b, double x) =>
        (new(2 * x - b.Max.X, b.Min.Y, b.Min.Z), new(2 * x - b.Min.X, b.Max.Y, b.Max.Z));

    static List<string> Made(JsonNode? result) => result!["ids"]!.AsArray().Select(x => (string)x!).ToList();

    static string WindowIn(Fixture f, string wall) =>
        Openings.FillOf(f.Model, f.Model.RelatedOf(wall, IfcClass.RelVoidsElement).Single().Id)!.Id;

    [Fact]
    public void A_room_copied_keeps_its_corners_joined()
    {
        var (f, ids) = ClockwiseRoom();
        var copies = Made(f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()), ["op"] = "copy", ["by"] = new JsonArray(10, 0) }));
        Assert.Equal(4, copies.Count);
        Assert.Equal(8, JoinRels(f).Count);
        Assert.All(copies, c => Assert.Equal(2, Joins.Of(f.Model, c).Count()));
        Assert.Equal(RingVolume(), copies.Sum(id => Volume(f, id)), 6);
        for (var i = 0; i < ids.Length; i++)
        {
            var (lo, hi) = LayerBounds(f, ids[i], Render);
            SameBounds((lo + new Vec3(10, 0, 0), hi + new Vec3(10, 0, 0)), LayerBounds(f, copies[i], Render));
        }
    }

    [Fact]
    public void A_room_mirrored_is_its_mirror_image_with_its_corners_joined_and_its_windows_facing_out()
    {
        var (f, ids) = ClockwiseRoom();
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = ids[0], ["along"] = 1 })!["id"]!;
        var line = new JsonObject { ["a"] = new JsonArray(10, 0), ["b"] = new JsonArray(10, 1) };
        var before = ids.Select(id => (Render: LayerBounds(f, id, Render), Plaster: LayerBounds(f, id, Plaster))).ToList();
        var windowBefore = BoundsOf(f, window);

        // Mirrored as a copy: the copies are the originals' mirror image, render inside, the window's sill outside.
        var copies = Made(f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()), ["op"] = "mirror", ["a"] = line["a"]!.DeepClone(), ["b"] = line["b"]!.DeepClone(), ["copy"] = true }));
        Assert.Equal(8, JoinRels(f).Count);
        Assert.All(copies, c => Assert.Equal(2, Joins.Of(f.Model, c).Count()));
        Assert.Equal(ids.Sum(id => Volume(f, id)), copies.Sum(id => Volume(f, id)), 6);
        for (var i = 0; i < ids.Length; i++)
        {
            SameBounds(MirroredX(before[i].Render, 10), LayerBounds(f, copies[i], Render));
            SameBounds(MirroredX(before[i].Plaster, 10), LayerBounds(f, copies[i], Plaster));
        }
        SameBounds(MirroredX(windowBefore, 10), BoundsOf(f, WindowIn(f, copies[0])));

        // Mirrored in place: the same image, the joins kept.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()), ["op"] = "mirror", ["a"] = line["a"]!.DeepClone(), ["b"] = line["b"]!.DeepClone() });
        Assert.Equal(8, JoinRels(f).Count);
        for (var i = 0; i < ids.Length; i++) SameBounds(MirroredX(before[i].Render, 10), LayerBounds(f, ids[i], Render));
        SameBounds(MirroredX(windowBefore, 10), BoundsOf(f, window));
    }
}
