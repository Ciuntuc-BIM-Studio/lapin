using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Drafting.Cad;

namespace Lapin.Engine.Tests;

/// <summary>
/// The stair by sketch (users asked for Revit's, 19.11.2026): the two sides drawn as boundary lines, the risers across them (or
/// none, spread by the rules), the path saying which way it climbs. Each tread is the shape between two risers and the sides at its
/// height; a deep one is a landing; the stair is built, drawn, railed and measured as every stair is.
/// </summary>
public class StairSketchTests
{
    static JsonArray L(double ax, double ay, double bx, double by, double rise = 0) => new(ax, ay, bx, by, rise);
    static JsonArray Ls(params JsonArray[] ls) => new([.. ls.Select(x => (JsonNode)x)]);
    static StairSketchData Data(JsonArray boundary, JsonArray? riser = null, JsonArray? path = null, bool flip = false) =>
        StairSketchData.From(new JsonObject { ["boundary"] = boundary.DeepClone(), ["riser"] = riser?.DeepClone() ?? new JsonArray(), ["path"] = path?.DeepClone() ?? new JsonArray(), ["flip"] = flip })!;

    /// <summary>Two sides 1 m apart along +X, 5 m long.</summary>
    static JsonArray Straight() => Ls(L(0, 0, 5, 0), L(0, 1, 5, 1));

    static Fixture TwoStoreys()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 });
        return f;
    }

    [Fact]
    public void Without_risers_drawn_the_rules_risers_are_spread_evenly_between_the_sides()
    {
        var spec = StairSpec.Default();
        var r = StairSketch.Resolve(spec, 3.0, null, Data(Straight()));
        Assert.Equal(StairSolver.Resolve(spec, 3.0).Risers, r.Risers);
        Assert.Equal(3.0 / r.Risers, r.RiserHeight, 9);
        Assert.Equal(5.0 / (r.Risers - 1), r.Tread, 6);
        Assert.Equal(r.Risers - 1, r.Winders.Count);
        Assert.Empty(r.Landings);
        Assert.Empty(r.Flights);
        Assert.Equal(1.0, r.Width, 6);
        // Each tread its height, a step up from the one before; together they cover the sketch.
        for (var k = 0; k < r.Winders.Count; k++) Assert.Equal((k + 1) * r.RiserHeight, r.Winders[k].Z, 9);
        Assert.Equal(5.0, r.Winders.Sum(w => Polygon2.SignedArea(w.Outline)), 6);
        Assert.All(r.Winders, w => Assert.True(Polygon2.SignedArea(w.Outline) > 0));
        // The foot at the middle of the first riser, climbing along +X; the top at the last.
        Assert.Equal(0, (r.Foot.At - new Vec2(0, 0.5)).Length, 6);
        Assert.Equal(1, r.Foot.Dir.X, 6);
        Assert.Equal(0, (r.Top.At - new Vec2(5, 0.5)).Length, 6);
    }

    [Fact]
    public void The_path_and_flip_say_which_way_it_climbs()
    {
        var spec = StairSpec.Default();
        var path = Ls(L(4.8, 0.5, 0.2, 0.5));
        var r = StairSketch.Resolve(spec, 3.0, null, Data(Straight(), path: path));
        Assert.Equal(-1, r.Foot.Dir.X, 6);
        Assert.Equal(0, (r.Foot.At - new Vec2(5, 0.5)).Length, 6);
        var flipped = StairSketch.Resolve(spec, 3.0, null, Data(Straight(), path: path, flip: true));
        Assert.Equal(1, flipped.Foot.Dir.X, 6);
    }

    [Fact]
    public void Drawn_risers_are_the_stair_s_risers_and_a_wide_gap_between_two_is_a_landing()
    {
        // Two sides of 4 m; risers at 0, 0.3, 0.6, then a 1.2 m landing, then 2.1, 2.4 … (the boundary drawn in two pieces each side).
        var boundary = Ls(L(0, 0, 2, 0), L(2, 0, 4, 0), L(0, 1.1, 4, 1.1));
        double[] xs = [0, 0.3, 0.6, 1.8, 2.1, 2.4, 2.7, 3.0];
        // Drawn a little short of one side and past the other, as by eye.
        var risers = Ls([.. xs.Reverse().Select(x => L(x, -0.03, x, 1.12))]);
        var r = StairSketch.Resolve(StairSpec.Default(), 1.6, null, Data(boundary, risers));
        Assert.Equal(8, r.Risers);
        Assert.Equal(0.2, r.RiserHeight, 9);
        Assert.Single(r.Landings);
        Assert.Equal(6, r.Winders.Count);
        Assert.Equal(3 * 0.2, r.Landings[0].Z, 9);
        Assert.Equal(1.2 * 1.1, Polygon2.SignedArea(r.Landings[0].Outline), 6);
        Assert.Equal(0.3, r.Tread, 6);
        Assert.Equal(1.1, r.Width, 6);
        // Trimmed to the sides: each riser exactly across.
        Assert.All(r.Sketch!.Risers, line => Assert.Equal(1.1, (line[^1] - line[0]).Length, 6));
        Assert.Contains(r.Checks, c => c.Key == "landing" && c.Ok);
    }

    [Fact]
    public void A_curved_side_gives_winders_that_fan_round_it()
    {
        // A quarter turn: the inner side an arc of radius 0.3, the outer one of radius 1.3, both from the +X axis round to +Y.
        static double Rise(double rad) => -rad * (1 - Math.Cos(Math.PI / 4));
        var boundary = Ls(L(0.3, 0, 0, 0.3, Rise(0.3)), L(1.3, 0, 0, 1.3, Rise(1.3)));
        var r = StairSketch.Resolve(StairSpec.Default(), 1.0, null, Data(boundary));
        var area = Math.PI / 4 * (1.3 * 1.3 - 0.3 * 0.3);
        Assert.Equal(area, r.Winders.Sum(w => Polygon2.SignedArea(w.Outline)), 2);
        Assert.All(r.Winders, w => Assert.True(Polygon2.SignedArea(w.Outline) > 0));
        // Climbing round anticlockwise from the +X axis.
        Assert.True(r.Foot.Dir.Y > 0.99);
    }

    [Fact]
    public void A_sketch_that_makes_no_stair_is_refused_saying_why()
    {
        var spec = StairSpec.Default();
        Assert.Contains("two sides", Assert.Throws<CommandException>(() => StairSketch.Resolve(spec, 3, null, Data(Ls(L(0, 0, 5, 0))))).Message);
        Assert.Contains("closed", Assert.Throws<CommandException>(() => StairSketch.Resolve(spec, 3, null,
            Data(Ls(L(0, 0, 5, 0), L(5, 0, 5, 1), L(5, 1, 0, 1), L(0, 1, 0, 0))))).Message);
        Assert.Contains("does not reach", Assert.Throws<CommandException>(() => StairSketch.Resolve(spec, 3, null,
            Data(Straight(), Ls(L(0, 0, 0, 1), L(2, 0, 2, 0.5))))).Message);
        Assert.Contains("cross", Assert.Throws<CommandException>(() => StairSketch.Resolve(spec, 3, null,
            Data(Straight(), Ls(L(0, 0, 0, 1), L(1, 0, 2, 1), L(2, 0, 1, 1), L(3, 0, 3, 1))))).Message);
    }

    [Fact]
    public void The_command_places_the_stair_at_its_first_riser_and_keeps_the_sketch_in_its_own_frame()
    {
        var f = TwoStoreys();
        // The straight sketch turned to climb along +Y, away from the origin.
        var boundary = Ls(L(10, 2, 10, 7), L(9, 2, 9, 7));
        var id = (string)f.Run("architecture.addSketchStair", new JsonObject { ["storeyId"] = f.Storey, ["boundary"] = boundary })!["id"]!;
        var stair = f.Model.Get(id)!;
        Assert.Equal(9.5, Args.Point(stair.Attrs["at"], "at").X, 6);
        Assert.Equal(2, Args.Point(stair.Attrs["at"], "at").Y, 6);
        Assert.Equal(90, stair.Num("rotation"), 6);
        var g = StairBuilder.Geometry(stair, f.Model)!;
        Assert.Null(g.Diagnostics);
        Assert.Contains(g.Parts, p => p.Key == "winders");
        Assert.Contains(g.Parts, p => p.Key!.StartsWith("railing:", StringComparison.Ordinal));
        // Its body: from the floor at the foot to the floor above at the top.
        var (lo, hi) = f.Bounds(id);
        Assert.InRange(lo.X, 8.9, 9.01);
        Assert.InRange(hi.Y, 6.99, 7.3);
        // The sketch comes back in plan, as drawn.
        var back = StairSketchData.From(f.Session.Query("architecture.stairSketch", new JsonObject { ["id"] = id }))!;
        Assert.Contains(back.Boundary, p => (p.A - new Vec2(10, 2)).Length < 1e-6 && (p.B - new Vec2(10, 7)).Length < 1e-6);
        // Moved, the stair takes its sketch with it.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        var moved = StairSketchData.From(f.Session.Query("architecture.stairSketch", new JsonObject { ["id"] = id }))!;
        Assert.Contains(moved.Boundary, p => (p.A - new Vec2(11, 2)).Length < 1e-6);
    }

    [Fact]
    public void Drawn_again_the_stair_keeps_its_id_a_bad_sketch_is_refused_and_nothing_changes()
    {
        var f = TwoStoreys();
        var id = (string)f.Run("architecture.addSketchStair", new JsonObject { ["storeyId"] = f.Storey, ["boundary"] = Straight() })!["id"]!;

        var risers = Ls([.. Enumerable.Range(0, 16).Select(i => L(i * 0.3, 0, i * 0.3, 1))]);
        f.Run("architecture.updateStairSketch", new JsonObject { ["id"] = id, ["boundary"] = Straight(), ["riser"] = risers.DeepClone() });
        var r = StairBuilder.Resolve(f.Model, f.Model.Get(id)!)!.Value.Resolved;
        Assert.Equal(16, r.Risers);
        Assert.Single(f.Model.OfClass(IfcClass.Stair));
        var before = f.Model.Get(id)!.Attrs[StairSketchData.Key]!.ToJsonString();
        Assert.Throws<CommandException>(() => f.Run("architecture.updateStairSketch", new JsonObject { ["id"] = id, ["boundary"] = Ls(L(0, 0, 5, 0)) }));
        Assert.Equal(before, f.Model.Get(id)!.Attrs[StairSketchData.Key]!.ToJsonString());
        // The check while drawing: what the sketch makes, how many risers the height asks.
        var check = f.Session.Query("architecture.stairSketchCheck", new JsonObject { ["storeyId"] = f.Storey, ["boundary"] = Straight(), ["riser"] = risers.DeepClone() })!;
        Assert.True((bool)check["ok"]!);
        Assert.Equal(16, (int)check["risers"]!);
        Assert.Equal(StairSolver.Resolve(StairSpec.Default(), 3.0).Risers, (int)check["needed"]!);
        // Its sketch taken away: the type's shape again.
        f.Run("architecture.updateStairSketch", new JsonObject { ["id"] = id, ["sketch"] = null });
        Assert.Null(StairBuilder.Resolve(f.Model, f.Model.Get(id)!)!.Value.Resolved.Sketch);
    }

    [Fact]
    public void The_plan_draws_its_treads_and_its_walking_line_cut_at_the_cut_plane()
    {
        var r = StairSketch.Resolve(StairSpec.Default(), 3.0, null, Data(Straight()));
        var whole = StairPlan.Of(r, null);
        Assert.Single(whole.Paths);
        Assert.Empty(whole.Breaks);
        Assert.DoesNotContain(whole.Lines, l => l.Above);
        var cut = StairPlan.Of(r, 1.2);
        Assert.NotEmpty(cut.Breaks);
        Assert.Contains(cut.Lines, l => l.Above);
        Assert.Contains(cut.Lines, l => !l.Above);
        // Every step numbered, the last on the floor above.
        var numbers = StairMarks.StepCentres(r).Select(x => x.Number).ToList();
        Assert.Equal(Enumerable.Range(1, r.Risers), numbers.Order());
    }
}

/// <summary>The stair by sketch's board in Studio: lines drawn by role on a plan of its own, finished into a stair, opened again, dropped.</summary>
public class StairSketchBoardTests
{
    static Fixture F()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Drafting.DraftingModule(), new Lapin.Studio.StudioModule());
        f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 });
        return f;
    }

    static void Line(Fixture f, string view, double ax, double ay, double bx, double by, string? role) =>
        f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = view, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = new JsonArray(ax, ay), ["b"] = new JsonArray(bx, by) },
            ["role"] = role,
        });

    [Fact]
    public void Lines_drawn_by_role_on_the_board_make_the_stair_and_open_again_as_they_were()
    {
        var f = F();
        var view = (string)f.Run("studio.editStairSketch", new JsonObject { ["storeyId"] = f.Storey })!["viewId"]!;
        Line(f, view, 0, 0, 5, 0, "boundary");
        Line(f, view, 0, 1, 5, 1, null);           // no role: a side too
        Line(f, view, -1, -1, 6, -1, "draft");      // construction: left out
        // An arc of a riser would do; straight risers here, the first and the last only: two risers, one long tread.
        Line(f, view, 0, 0, 0, 1, "riser");
        Line(f, view, 5, 0, 5, 1, "riser");
        var check = f.Session.Query("studio.stairSketchCheck", new JsonObject { ["viewId"] = view })!;
        Assert.True((bool)check["ok"]!);
        Assert.Equal(2, (int)check["risers"]!);
        // Not enough: the risers spread by the rules instead (the risers taken away).
        foreach (var d in Drafts.On(f.Model, view).Where(d => d.Str("role") == "riser").ToList()) f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(d.Id) });
        var id = (string)f.Run("studio.finishStairSketch", new JsonObject { ["viewId"] = view })!["id"]!;
        Assert.Null(f.Model.Get(view));
        var r = StairBuilder.Resolve(f.Model, f.Model.Get(id)!)!.Value.Resolved;
        Assert.NotNull(r.Sketch);
        Assert.Equal(StairSolver.Resolve(StairSpec.Default(), 3.0).Risers, r.Risers);

        // Opened again: its two sides as boundary lines, the stair hidden; cancelled, nothing changes.
        var again = (string)f.Run("studio.editStairSketch", new JsonObject { ["id"] = id })!["viewId"]!;
        var drafts = Drafts.On(f.Model, again).ToList();
        Assert.Equal(2, drafts.Count(d => d.Str("role") == "boundary"));
        Assert.Equal("#16a34a", Drafts.RoleColor("boundary"));
        f.Run("studio.cancelStairSketch", new JsonObject { ["viewId"] = again });
        Assert.Null(f.Model.Get(again));
        Assert.Empty(Drafts.On(f.Model, again));

        // A sketch that makes no stair: refused, the board kept.
        var bad = (string)f.Run("studio.editStairSketch", new JsonObject { ["storeyId"] = f.Storey })!["viewId"]!;
        Line(f, bad, 0, 0, 5, 0, "boundary");
        Assert.Throws<CommandException>(() => f.Run("studio.finishStairSketch", new JsonObject { ["viewId"] = bad }));
        Assert.NotNull(f.Model.Get(bad));
    }
}
