using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 227: a dimension value moved off its place takes a leader back to its segment's middle that ends at the
/// value's box, a small gap (0.5 mm on paper) short of it, and never crosses its digits: moved above the line, below it,
/// beside the segment, or off a vertical dimension; its opening-height rows, moved with it, are kept clear of too. A value
/// in its place draws no leader, as before. At 1:100 a paper mm is 0.1 m and a value 0.25 m high.
/// </summary>
public class DimensionLeaderTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f) => "plan:" + f.Storey;
    const double Mm = 0.1, H = 0.25, Gap = DimensionTextPlacement.LeaderGap * Mm;

    /// <summary>A 1.40 m dimension from (0, 0) to (1.4, 0) along <paramref name="dir"/> x (or y), its line 2 m off.</summary>
    static string Dim(Fixture f, bool vertical = false) =>
        (string)f.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = Plan(f),
            ["pts"] = vertical ? new JsonArray(new JsonArray(0, 0), new JsonArray(0, 1.0)) : new JsonArray(new JsonArray(0, 0), new JsonArray(1.4, 0)),
            ["at"] = vertical ? new JsonArray(-2, 0) : new JsonArray(0, 2), ["dir"] = vertical ? new JsonArray(0, 1) : new JsonArray(1, 0),
        })!["id"]!;

    static ViewMark Mark(Fixture f, string dim, bool print = false) => Views.Render(f.Session.Reader, Views.Find(f.Model, Plan(f))!, print).Marks.Single(m => m.Owner == dim);

    static void Move(Fixture f, string dim, double x, double y) =>
        f.Run("drafting.moveDimensionText", new JsonObject { ["id"] = dim, ["segment"] = 0, ["by"] = new JsonArray(x, y) });

    /// <summary>The value's box on the drawing: its anchor (the middle of its baseline) and its width, upright or turned a quarter.</summary>
    static (double X0, double X1, double Y0, double Y1) Box(PenText t)
    {
        var w = BuildupText.Measure(t.Text, t.Height, false) * Mm;
        var turned = Math.Abs(Math.Abs(t.Angle) - 90) < 1;
        // Upright: the box stands on the baseline; turned (reading bottom to top) it stands left of it.
        return turned ? (t.At.X - H, t.At.X, t.At.Y - w / 2, t.At.Y + w / 2) : (t.At.X - w / 2, t.At.X + w / 2, t.At.Y, t.At.Y + H);
    }

    /// <summary>Whether a line passes through a box (sampled along it).</summary>
    static bool Crosses((Vec2 A, Vec2 B) l, (double X0, double X1, double Y0, double Y1) b) =>
        Enumerable.Range(0, 1001).Select(i => l.A + (l.B - l.A) * (i / 1000.0)).Any(p => p.X > b.X0 && p.X < b.X1 && p.Y > b.Y0 && p.Y < b.Y1);

    static (Vec2 A, Vec2 B) Leader(Fixture f, string dim, int before)
    {
        var lines = Mark(f, dim).Lines;
        Assert.Equal(before + 1, lines.Count);
        return (lines[^1].A, lines[^1].B);
    }

    [Fact]
    public void A_value_in_its_place_draws_no_leader_as_before()
    {
        var f = F();
        var dim = Dim(f);
        var lines = Mark(f, dim).Lines.Count;
        // Moved a little along, still over its segment: no leader either.
        Move(f, dim, 0.2, 0);
        Assert.Equal(lines, Mark(f, dim).Lines.Count);
        var at = Mark(f, dim).Texts.Single().Text.At;
        Assert.Equal(0.9, at.X, 6);
        Assert.Equal(2.08, at.Y, 6);
    }

    [Fact]
    public void Moved_below_the_line_the_leader_stops_over_the_value_short_of_it()
    {
        var f = F();
        var dim = Dim(f);
        var before = Mark(f, dim).Lines.Count;
        Move(f, dim, 0, -0.8);
        var t = Mark(f, dim).Texts.Single().Text;
        var leader = Leader(f, dim, before);
        Assert.Equal(new Vec2(0.7, 2), leader.A);
        // It ends the gap above the value's top (the value stands at 2.08 − 0.8 = 1.28, 0.25 high).
        Assert.Equal(0.7, leader.B.X, 6);
        Assert.Equal(1.28 + H + Gap, leader.B.Y, 6);
        Assert.False(Crosses(leader, Box(t)), "the leader never crosses the digits");
    }

    [Fact]
    public void Moved_above_the_line_the_leader_stops_under_the_value()
    {
        var f = F();
        var dim = Dim(f);
        var before = Mark(f, dim).Lines.Count;
        Move(f, dim, 0, 0.6);
        var leader = Leader(f, dim, before);
        Assert.Equal(2.68 - Gap, leader.B.Y, 6);
        Assert.False(Crosses(leader, Box(Mark(f, dim).Texts.Single().Text)));
    }

    [Fact]
    public void Moved_beside_the_segment_the_leader_runs_to_the_values_near_side()
    {
        var f = F();
        var dim = Dim(f);
        var before = Mark(f, dim).Lines.Count;
        Move(f, dim, 1.5, -0.3);
        var t = Mark(f, dim).Texts.Single().Text;
        var leader = Leader(f, dim, before);
        var b = Box(t);
        Assert.Equal(b.X0 - Gap, leader.B.X, 6);
        Assert.False(Crosses(leader, b));
        // Moved beside, on the line's own row: it ends at the value's side, not under its baseline across it.
        Move(f, dim, 0, 0.3);
        var level = Leader(f, dim, before);
        Assert.Equal(Box(Mark(f, dim).Texts.Single().Text).X0 - Gap, level.B.X, 6);
        Assert.False(Crosses(level, Box(Mark(f, dim).Texts.Single().Text)));
    }

    [Fact]
    public void A_vertical_dimensions_value_moved_off_its_line_is_not_crossed()
    {
        var f = F();
        var dim = Dim(f, vertical: true);
        var before = Mark(f, dim).Lines.Count;
        Move(f, dim, -0.6, 0.1);
        var t = Mark(f, dim).Texts.Single().Text;
        var leader = Leader(f, dim, before);
        Assert.Equal(new Vec2(-2, 0.5), leader.A);
        var b = Box(t);
        Assert.False(Crosses(leader, b), $"the leader {leader} crosses the value {b}");
        Assert.Equal(b.X1 + Gap, leader.B.X, 6);
        // On paper too: the same lines.
        Assert.Equal(Mark(f, dim).Lines[^1].B, Mark(f, dim, print: true).Lines[^1].B);
    }

    [Fact]
    public void The_opening_rows_moved_with_the_value_are_kept_clear_of()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        f.Session.New();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var wall = f.Wall(0, 0, 6, 0, type, height: 3.0);
        f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["width"] = 1.2, ["height"] = 1.5, ["sill"] = 0.9 });
        f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["id"] = Standards.Standard, ["openingHeight"] = "hp" } });
        var dim = (string)f.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = Plan(f), ["pts"] = new JsonArray(new JsonArray(2.4, -0.1), new JsonArray(3.6, -0.1)), ["at"] = new JsonArray(2.4, -1.1), ["dir"] = new JsonArray(1, 0),
        })!["id"]!;
        var before = Mark(f, dim).Lines.Count;
        // Moved up: the height rows come along, under the value; the leader from the line stops under the lowest row.
        f.Run("drafting.moveDimensionText", new JsonObject { ["id"] = dim, ["segment"] = 0, ["by"] = new JsonArray(0, 1.2) });
        var texts = Mark(f, dim).Texts.Select(x => x.Text).ToList();
        Assert.Equal(["1200", "h=1500", "p=900"], texts.Select(x => x.Text));
        var leader = Leader(f, dim, before);
        foreach (var t in texts) Assert.False(Crosses(leader, Box(t)), $"the leader crosses {t.Text}");
        Assert.Equal(texts.Min(x => x.At.Y) - Gap, leader.B.Y, 6);
    }

    [Fact]
    public void The_leader_end_is_the_nearest_point_of_the_boxes_grown_by_the_gap()
    {
        var r = new Vec2(1, 0);
        var up = new Vec2(0, 1);
        var value = new TextRoom.Box("d", TextRoom.Kind.Text, new Vec2(0, 1), r, up, 0.3, 0.1);
        var row = new TextRoom.Box("d", TextRoom.Kind.Text, new Vec2(0, 0.6), r, up, 0.2, 0.1);
        var below = DimensionTextPlacement.LeaderEnd([value, row], new Vec2(0, 0), 0.05)!.Value;
        Assert.Equal(0, below.X, 9);
        Assert.Equal(0.45, below.Y, 9);
        var beside = DimensionTextPlacement.LeaderEnd([value, row], new Vec2(-2, 0.6), 0.05)!.Value;
        Assert.Equal(-0.35, beside.X, 9);
        Assert.Equal(0.6, beside.Y, 9);
        Assert.Null(DimensionTextPlacement.LeaderEnd([value], new Vec2(0.1, 1.05), 0.05));
    }
}
