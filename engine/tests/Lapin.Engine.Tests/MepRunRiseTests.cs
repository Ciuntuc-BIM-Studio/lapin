using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Mep;

namespace Lapin.Engine.Tests;

/// <summary>
/// A run whose plane changes while it is drawn (the Run tool's F, C, W): the
/// point where it changed holds two levels, [in, out], so the run goes straight
/// up or down there with a bend at each end, one element in one undo step; the
/// rise is kept through the route's edits and marked on the plan.
/// </summary>
public class MepRunRiseTests
{
    /// <summary>A heating run along X, on the floor (0.2) to x = 3, then on the ceiling (2.4) to x = 6.</summary>
    static (MepFixture F, string System, string Run) FloorThenCeiling()
    {
        var f = new MepFixture();
        var s = f.System(Services.Heating, new JsonObject { ["pair"] = false });
        var run = f.Draw(s, MepFixture.Pts((0, 0), (3, 0), (6, 0)), more: new JsonObject
        {
            ["plane"] = "floor",
            ["levels"] = new JsonArray(null, new JsonArray(null, 2.4), 2.4),
        });
        return (f, s, run);
    }

    static List<Vec3> Line(MepFixture f, string run)
    {
        var e = f.Model.Get(run)!;
        return Runs.Centreline(f.Model, e, Runs.SettingOf(f.Model, e), out _);
    }

    [Fact]
    public void The_run_rises_straight_up_where_its_plane_changed()
    {
        var (f, _, run) = FloorThenCeiling();
        var line = Line(f, run);
        Assert.Equal(4, line.Count);
        Assert.Equal(0.2, line[0].Z, 6);
        // The point where it changed: once at each height, in the same place in plan.
        Assert.Equal(new Vec2(3, 0), line[1].Xy);
        Assert.Equal(new Vec2(3, 0), line[2].Xy);
        Assert.Equal(0.2, line[1].Z, 6);
        Assert.Equal(2.4, line[2].Z, 6);
        Assert.Equal(2.4, line[3].Z, 6);
        // Stored as drawn: the floor run's own plane, the rise as [in, out].
        var e = f.Model.Get(run)!;
        Assert.Equal("floor", e.Str("plane"));
        var levels = Runs.PointLevelsOf(e)!;
        Assert.True(levels[1].Rises);
        Assert.Null(levels[1].In);
        Assert.Equal(2.4, levels[1].Out!.Value, 6);
        // Its length counts the vertical leg; its bends are the two at the rise's ends.
        Assert.Equal(6 + 2.2, line.Zip(line.Skip(1), (a, b) => (b - a).Length).Sum(), 6);
        Assert.Equal((2, 0), Layout.FittingsOf(f.Model, e));
        // The body goes up to the ceiling at the rise.
        var (min, max) = f.Bounds(run);
        Assert.True(max.Z > 2.4 && min.Z < 0.2);
    }

    [Fact]
    public void The_run_and_its_rise_are_one_undo_step()
    {
        var (f, _, run) = FloorThenCeiling();
        f.Session.Undo();
        Assert.Null(f.Model.Get(run));
    }

    [Fact]
    public void The_plan_marks_the_rise_as_the_rise_node_does_and_a_drop_with_a_cross()
    {
        var (f, s, run) = FloorThenCeiling();
        var plan = f.Session.Geometry(run)!.Plan!;
        // The ring (24 sides at least) at the rise, and the dot: the run comes up.
        Assert.Contains(plan, i => i.Kind == "symbol" && i.Closed && i.Points.All(p => Math.Abs((p - new Vec2(3, 0)).Length - 0.06) < 1e-6));
        Assert.Contains(plan, i => i.Kind == "heavy" && i.Closed && i.Points.All(p => Math.Abs((p - new Vec2(3, 0)).Length - 0.015) < 1e-6));
        // From the ceiling down to the wall: a drop, the cross.
        var down = f.Draw(s, MepFixture.Pts((0, 2), (3, 2), (6, 2)), more: new JsonObject { ["plane"] = "ceiling", ["levels"] = new JsonArray(null, new JsonArray(null, 1.1), 1.1) });
        var marks = f.Session.Geometry(down)!.Plan!;
        Assert.DoesNotContain(marks, i => i.Kind == "heavy" && i.Closed);
        Assert.Equal(2, marks.Count(i => i.Kind == "symbol" && !i.Closed && i.Points.Count == 2 && (i.Points[0] + i.Points[1]) * 0.5 is var mid && (mid - new Vec2(3, 2)).Length < 1e-6));
        // A run on one plane has no mark.
        var flat = f.Draw(s, MepFixture.Pts((0, 4), (3, 4), (6, 4)));
        Assert.Empty(Runs.Rises(Line(f, flat)));
    }

    [Fact]
    public void The_rise_is_kept_when_the_route_is_edited()
    {
        var (f, _, run) = FloorThenCeiling();
        // The last point dragged (its points sent alone, as the grip does): the rise stays where it was.
        f.Run("mep.setRunPoints", new JsonObject { ["id"] = run, ["points"] = MepFixture.Pts((0, 0), (3, 0), (6, 2)) });
        Assert.True(Runs.PointLevelsOf(f.Model.Get(run)!)![1].Rises);
        // The rise's own point dragged: it goes with it.
        f.Run("mep.setRunPoints", new JsonObject { ["id"] = run, ["points"] = MepFixture.Pts((0, 0), (3, 1), (6, 2)) });
        var line = Line(f, run);
        Assert.Single(Runs.Rises(line));
        Assert.Equal(new Vec2(3, 1), Runs.Rises(line).Single().At);
        // A handle added on the ceiling leg: on the ceiling; on the floor leg: on the floor.
        f.Run("mep.addHandle", new JsonObject { ["runId"] = run, ["x"] = 4.5, ["y"] = 1.5 });
        f.Run("mep.addHandle", new JsonObject { ["runId"] = run, ["x"] = 1.5, ["y"] = 0.5 });
        var levels = Runs.PointLevelsOf(f.Model.Get(run)!)!;
        Assert.Equal(5, levels.Count);
        Assert.Null(levels[1].In);
        Assert.True(levels[2].Rises);
        Assert.Equal(2.4, levels[3].In!.Value, 6);
        Assert.Single(Runs.Rises(Line(f, run)));
    }

    [Fact]
    public void A_path_redrawn_with_levels_takes_them_and_one_without_keeps_the_old()
    {
        var (f, _, run) = FloorThenCeiling();
        f.Run("mep.redrawRun", new JsonObject { ["id"] = run, ["points"] = MepFixture.Pts((0, 0), (2, 0), (2, 3), (6, 3)), ["levels"] = new JsonArray(null, null, new JsonArray(null, 1.1), 1.1) });
        var line = Line(f, run);
        Assert.Equal(new Vec2(2, 3), Runs.Rises(line).Single().At);
        Assert.Equal(1.1, line[^1].Z, 6);
        // The same path again with no levels: the run keeps its own.
        f.Run("mep.redrawRun", new JsonObject { ["id"] = run, ["points"] = MepFixture.Pts((0, 0), (2, 0), (2, 3), (6, 3)) });
        Assert.Equal(new Vec2(2, 3), Runs.Rises(Line(f, run)).Single().At);
    }

    [Fact]
    public void Levels_are_set_with_rises_and_a_pair_of_one_height_is_one_level()
    {
        var (f, _, run) = FloorThenCeiling();
        f.Run("mep.setRunLevels", new JsonObject { ["id"] = run, ["levels"] = new JsonArray(null, new JsonArray(1.0, 1.0), 1.0) });
        var e = f.Model.Get(run)!;
        Assert.Equal(1.0, (double)((JsonArray)e.Attrs["levels"]!)[1]!, 6);
        Assert.Empty(Runs.Rises(Line(f, run)));
        // The old reading (one level per point) gives where the run arrives at a rise.
        f.Run("mep.setRunLevels", new JsonObject { ["id"] = run, ["levels"] = new JsonArray(null, new JsonArray(0.5, 2.0), 2.0) });
        Assert.Equal(0.5, Runs.LevelsOf(f.Model.Get(run)!)![1]!.Value, 6);
    }
}
