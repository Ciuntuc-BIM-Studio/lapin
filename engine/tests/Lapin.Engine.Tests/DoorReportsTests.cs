using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Testers' reports #82 (a window would not move with MOVE, only by dragging), #83 (the door schedule kept a door's
/// type's hand after the door was flipped to the other one) and #91 (the schedule named the swing the other way round
/// from the plan).
/// </summary>
public sealed class DoorReportsTests
{
    /// <summary>A room 10 × 6: the south wall drawn west to east, the north one east to west.</summary>
    static Fixture Room(out string south, out string north)
    {
        var f = new Fixture(new ArchitectureModule());
        south = f.Wall(0, 0, 10, 0);
        f.Wall(10, 0, 10, 6);
        north = f.Wall(10, 6, 0, 6);
        f.Wall(0, 6, 0, 0);
        return f;
    }

    static string Add(Fixture f, string kind, string wall, double along) =>
        (string)f.Run(kind == "door" ? "architecture.addDoor" : "architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = along })!["id"]!;

    static Entity OpeningOf(Fixture f, string fill) => Openings.OpeningOf(f.Model, fill)!;
    static double Along(Fixture f, string fill) => OpeningOf(f, fill).Num("along");
    static string WallOf(Fixture f, string fill) => Openings.WallOf(f.Model, OpeningOf(f, fill).Id)!.Id;

    static JsonNode? Transform(Fixture f, string op, double dx, double dy, params string[] ids) =>
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]), ["op"] = op, ["by"] = new JsonArray(dx, dy) });

    [Fact]
    public void Moving_a_window_slides_it_along_its_wall_by_what_the_move_has_along_it()
    {
        var f = Room(out var south, out _);
        var window = Add(f, "window", south, 2);
        // MOVE, a base point, 500 typed along the wall.
        Transform(f, "move", 0.5, 0, window);
        Assert.Equal(2.5, Along(f, window), 9);
        // A second point off the wall's line: only the part along the wall counts.
        Transform(f, "move", 1, 0.8, window);
        Assert.Equal(3.5, Along(f, window), 9);
        Assert.Equal(south, WallOf(f, window));
        Assert.Equal(0.9, OpeningOf(f, window).Num("sill"), 9);
        // Its opening selected instead: the same.
        Transform(f, "move", -1, 0, OpeningOf(f, window).Id);
        Assert.Equal(2.5, Along(f, window), 9);
        // Both selected: moved once.
        Transform(f, "move", 1, 0, window, OpeningOf(f, window).Id);
        Assert.Equal(3.5, Along(f, window), 9);
    }

    [Fact]
    public void A_window_moved_into_another_wall_goes_into_it_and_keeps_the_way_it_opens()
    {
        var f = Room(out var south, out var north);
        var door = Add(f, "door", south, 2);
        Transform(f, "move", 0, 6, door);
        Assert.Equal(north, WallOf(f, door));
        // The north wall runs east to west: 2 m from the west end is 8 m along it.
        Assert.Equal(8, Along(f, door), 9);
        Assert.DoesNotContain(Openings.Of(f.Model, south), _ => true);
        // Its wall's direction and faces are the other way round: both flips keep it opening into the room, hinges where they were.
        var d = f.Model.Get(door)!;
        Assert.True(d.Bool("flipHand"));
        Assert.True(d.Bool("flipSide"));
    }

    [Fact]
    public void A_move_that_leaves_no_room_is_refused_and_nothing_moves()
    {
        var f = Room(out var south, out _);
        var a = Add(f, "window", south, 2);
        var b = Add(f, "window", south, 5);
        Assert.Throws<CommandException>(() => Transform(f, "move", 9, 0, a));
        Assert.Throws<CommandException>(() => Transform(f, "move", 3, 0, a));
        Assert.Equal(2, Along(f, a), 9);
        // Two moved together by more than the gap between them: they do not meet on the way.
        var c = Add(f, "window", south, 3.3);
        Transform(f, "move", 0.5, 0, a, c);
        Assert.Equal(2.5, Along(f, a), 9);
        Assert.Equal(3.8, Along(f, c), 9);
        Assert.Equal(5, Along(f, b), 9);
    }

    [Fact]
    public void Copying_a_door_puts_a_new_one_of_its_type_along_the_wall()
    {
        var f = Room(out var south, out _);
        var door = Add(f, "door", south, 2);
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = new JsonArray(door), ["axis"] = "x" });
        var made = Transform(f, "copy", 1.5, 0, door)!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        var copy = Assert.Single(made);
        Assert.Equal(IfcClass.Door, f.Model.Get(copy)!.Class);
        Assert.Equal("D2", f.Model.Get(copy)!.Name);
        Assert.Equal(3.5, Along(f, copy), 9);
        Assert.Equal(2, Along(f, door), 9);
        Assert.Equal(f.Model.RelatingOf(door, IfcClass.RelDefinesByType)!.Id, f.Model.RelatingOf(copy, IfcClass.RelDefinesByType)!.Id);
        Assert.True(f.Model.Get(copy)!.Bool("flipHand"));
        Assert.Equal(Spatial.StoreyOf(f.Model, door)!.Id, Spatial.StoreyOf(f.Model, copy)!.Id);
        // A bare opening copies with its size.
        var hole = (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = south, ["along"] = 7, ["width"] = 0.6, ["height"] = 0.6, ["sill"] = 1 })!["id"]!;
        var twin = Assert.Single(Transform(f, "copy", 1.2, 0, hole)!["ids"]!.AsArray().Select(x => (string)x!));
        Assert.Equal(8.2, f.Model.Get(twin)!.Num("along"), 9);
        Assert.Equal(0.6, f.Model.Get(twin)!.Num("width"), 9);
        Assert.Equal(south, Openings.WallOf(f.Model, twin)!.Id);
    }

    [Fact]
    public void A_door_mirrored_on_its_own_changes_hand_and_one_moved_with_its_wall_goes_with_it()
    {
        var f = Room(out var south, out _);
        var door = Add(f, "door", south, 2);
        // A mirror line across the wall through the door: the other hand, the same side.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(door), ["op"] = "mirror", ["a"] = new JsonArray(2, -1), ["b"] = new JsonArray(2, 1) });
        Assert.True(f.Model.Get(door)!.Bool("flipHand"));
        Assert.False(f.Model.Get(door)!.Bool("flipSide"));
        Assert.Equal(2, Along(f, door), 9);
        // With its wall: carried by the wall, its place along it unchanged.
        Transform(f, "move", 0, -1, door, south);
        Assert.Equal(2, Along(f, door), 9);
        Assert.Equal(-1, Walls.Start(f.Model.Get(south)!).Y, 9);
    }

    // ─── #83: the schedule lists each hand ──────────────────────────────────

    static string Flip(Fixture f, string id, string axis) { f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = new JsonArray(id), ["axis"] = axis }); return id; }
    static string Opening(ScheduleData data, ScheduleEntry e) => ScheduleLayout.Rows(data, new ScheduleTable(["operation"], [], new Dictionary<string, Dictionary<string, string>>(), null)).Single().Value(e);

    [Fact]
    public void A_door_flipped_to_the_other_hand_is_listed_as_its_types_mirror_image()
    {
        var f = Room(out var south, out var north);
        var a = Add(f, "door", south, 2);
        var b = Flip(f, Add(f, "door", south, 5), "x");
        var c = Flip(f, Add(f, "door", north, 3), "y");
        // Both flips: the type turned round, its hand the same.
        Flip(f, Flip(f, Add(f, "door", north, 7), "x"), "y");
        var data = Schedules.Of(f.Session.Reader, "door");
        Assert.Equal(["D", "D-M"], data.Entries.Select(e => e.Mark));
        var (own, mirror) = (data.Entries[0], data.Entries[1]);
        Assert.Equal(2, own.Total);
        Assert.Equal(2, mirror.Total);
        // The type's swing-left (IFC's left) opens to the right of the line from its handle to its hinges (report #91).
        Assert.Equal("swing right", Opening(data, own));
        Assert.Equal("swing left", Opening(data, mirror));
        Assert.Equal("Door 900×2100 (mirrored)", mirror.Name);
        Assert.Equal(new[] { b, c }.Order(), mirror.Occurrences.Select(o => o.Id).Order());
        Assert.Equal(own.Key, mirror.TypeKey);
        Assert.Contains(a, own.Occurrences.Select(o => o.Id));
        // Flipped back: one entry again.
        Flip(f, b, "x");
        Flip(f, c, "y");
        data = Schedules.Of(f.Session.Reader, "door");
        Assert.Equal(4, Assert.Single(data.Entries).Total);
        // Every door flipped: the mirror image alone, the type not listed as unused.
        foreach (var e in data.Entries[0].Occurrences) Flip(f, e.Id, "x");
        data = Schedules.Of(f.Session.Reader, "door");
        Assert.Equal("D-M", Assert.Single(data.Entries).Mark);
    }

    [Fact]
    public void A_mirrored_window_has_its_panels_across_and_each_the_other_hand()
    {
        var m = Joinery.Mirrored(JoinerySpec.DefaultWindow() with { Columns = [2, 1] });
        Assert.Equal([1, 2], m.Columns);
        Assert.Equal([(1, "tilt-turn-right"), (0, "side-left")], m.Panels.Select(p => (p.Col, p.Operation)));
        Assert.Equal(JoinerySpec.DefaultWindow().Panels.Select(p => p.Operation), Joinery.Mirrored(Joinery.Mirrored(JoinerySpec.DefaultWindow())).Panels.Select(p => p.Operation));
    }

    [Fact]
    public void The_cards_elevation_shows_the_door_as_it_stands_seen_from_the_side_it_opens_to()
    {
        var f = Room(out var south, out _);
        var door = Add(f, "door", south, 2);
        Flip(f, Add(f, "door", south, 5), "x");
        // In the model the south wall runs west to east and the door opens north, into the room: its handle is east of its
        // middle, so someone in the room looking south at it has the handle on the left and the hinges on the right.
        var handle = f.Session.Geometry(door)!.Parts.Single(p => p.Name == "handle").Solid.Vertices;
        Assert.True(handle.Average(v => v.X) > 2);
        Assert.True(f.Session.Geometry(door)!.Parts.Single(p => p.Name == "leaf").Solid.Vertices.Max(v => v.Y) < 0.1);
        var data = Schedules.Of(f.Session.Reader, "door");
        var bands = ScheduleLayout.Bands(data, new ScheduleTable(null, [], new Dictionary<string, Dictionary<string, string>>(), null), drawingsOf: _ => ["interior"], note: ScheduleLayout.HandNote(data, ["interior"]));
        // The opening symbol (handle convention: the lines meet at the handle) in each card: two thin strokes meeting at their apex.
        var thin = bands.SelectMany(b => b.Pens).Where(p => p.Layer == "SCHEDULE-SYMBOLS" && Math.Abs(p.Width - 0.13) < 1e-9).SelectMany(p => p.Segments).ToList();
        Assert.Equal(4, thin.Count);
        var cards = thin.OrderBy(s => Math.Min(s.A.X, s.B.X)).Chunk(2).ToList();
        foreach (var (card, handleLeft) in cards.Zip(new[] { true, false }))
        {
            var apex = card[0].A.Equals(card[1].A) || card[0].A.Equals(card[1].B) ? card[0].A : card[0].B;
            var ends = card.SelectMany(s => new[] { s.A, s.B }).Where(p => !p.Equals(apex)).ToList();
            Assert.Equal(handleLeft, apex.X < ends.Average(p => p.X));
        }
        var texts = bands.SelectMany(b => b.Pens).SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        Assert.Contains(texts, t => t.StartsWith("Swing left or right: the leaf opens to the left or right of the line from its handle to its hinges", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.StartsWith("Elevations seen from the interior", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.StartsWith("-M: the type's mirror image", StringComparison.Ordinal));
    }

    // ─── #91: the schedule's hand is the plan's ─────────────────────────────

    /// <summary>
    /// A hinged door's hand read off its plan symbol, the architects' way (report #91): the arc's centre is the hinge, its
    /// end along the wall the handle (the leaf shut), its other end the leaf open; looking from the handle towards the
    /// hinge, a leaf opening to the left is a left swing. Also IFC's hand from the same drawing: looking the way the leaf
    /// opens, the hinge on the left is IFC's LEFT.
    /// </summary>
    static (string Architects, string Ifc, Vec2 Opens) HandInPlan(Fixture f, string door)
    {
        var arc = f.Session.Geometry(door)!.Plan!.Single(i => i.Kind == "symbol" && !i.Closed && i.Points.Count > 4).Points;
        var (a, b, c) = (arc[0], arc[arc.Count / 2], arc[^1]);
        // The circle through three points of the arc.
        var d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
        double S(Vec2 p) => p.X * p.X + p.Y * p.Y;
        var hinge = new Vec2((S(a) * (b.Y - c.Y) + S(b) * (c.Y - a.Y) + S(c) * (a.Y - b.Y)) / d, (S(a) * (c.X - b.X) + S(b) * (a.X - c.X) + S(c) * (b.X - a.X)) / d);
        // Every wall here runs along X: the handle is the arc's end level with the hinge.
        var (handle, open) = Math.Abs(a.Y - hinge.Y) < Math.Abs(c.Y - hinge.Y) ? (a, c) : (c, a);
        var toHinge = hinge - handle;
        var opens = open - hinge;
        var cross = toHinge.X * opens.Y - toHinge.Y * opens.X;
        // Facing the way it opens, the hinge on the left: the hinge's offset from the handle is to the left of `opens`.
        var ifcLeft = opens.X * toHinge.Y - opens.Y * toHinge.X > 0;
        return (cross > 0 ? "left" : "right", ifcLeft ? "LEFT" : "RIGHT", opens);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void A_doors_swing_in_the_schedule_is_the_one_its_plan_shows(bool hand, bool side, bool northWall)
    {
        var f = Room(out var south, out var north);
        var door = Add(f, "door", northWall ? north : south, 3);
        if (hand) Flip(f, door, "x");
        if (side) Flip(f, door, "y");
        var (architects, ifc, opens) = HandInPlan(f, door);
        var data = Schedules.Of(f.Session.Reader, "door");
        var entry = Assert.Single(data.Entries);
        Assert.Equal("swing " + architects, Opening(data, entry));
        Assert.Equal(hand != side, entry.Mirrored);
        // IFC keeps its own rule, the other way round from the architects': the placement's +Y the way the plan's leaf
        // opens, and the hand looking that way.
        var place = new JoineryIfc().Place(f.Model, f.Model.Get(door)!)!;
        Assert.True(place.Y.X * opens.X + place.Y.Y * opens.Y > 0);
        var type = JoineryIfc.Of(Openings.SpecOf(f.Model, door)!, place.Mirrored);
        Assert.Equal("SINGLE_SWING_" + ifc, type.Operation);
        Assert.NotEqual(architects.ToUpperInvariant(), ifc);
    }

    [Fact]
    public void A_pair_of_leaves_is_a_double_door_and_a_sliding_door_keeps_its_side()
    {
        var pair = JoinerySpec.DefaultDoor() with { Width = 1.6, Columns = [1, 1], Panels = [new(0, 0, "swing-left"), new(1, 0, "swing-right")] };
        Assert.Equal("double swing", ScheduleLayout.OperationText(pair));
        Assert.Equal("double swing", ScheduleLayout.OperationText(Joinery.Mirrored(pair)));
        var withSide = JoinerySpec.DefaultDoor() with { Width = 1.2, Columns = [2, 1], Panels = [new(0, 0, "swing-left"), new(1, 0, "fixed")] };
        Assert.Equal("swing right + fixed", ScheduleLayout.OperationText(withSide));
        var sliding = JoinerySpec.DefaultDoor() with { Panels = [new(0, 0, "door-sliding-left")] };
        Assert.Equal("sliding left", ScheduleLayout.OperationText(sliding));
        // A window keeps IFC's hand.
        Assert.Equal("tilt & turn left + side hung right", ScheduleLayout.OperationText(JoinerySpec.DefaultWindow()));
    }
}
