using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>
/// A tester's report #143 (0.1.814, Windows): a plan drew the whole stair, and the step numbers sat on the walking line.
/// A plan now cuts the stair at its cut plane (a break line where the nosings reach the cut, the part over it dashed or
/// hidden, the walking line on to the top) and writes the numbers beside the walking line.
/// </summary>
public class StairPlanCutTests
{
    static Fixture TwoStoreys()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 });
        return f;
    }

    static string Place(Fixture f, StairSpec spec)
    {
        var type = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        return (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0 })!["id"]!;
    }

    static string Pens(Fixture f) => ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["storeyId"] = f.Storey })!)["pens"]!.ToJsonString();

    static double Distance(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        var t = ab.Length < 1e-12 ? 0 : Math.Clamp(Vec2.Dot(p - a, ab) / Vec2.Dot(ab, ab), 0, 1);
        return (p - (a + ab * t)).Length;
    }

    static bool Near(Vec2 a, Vec2 b) => (a - b).Length < 1e-6;

    [Fact]
    public void A_straight_stair_through_the_cut_breaks_where_its_nosings_reach_the_cut_and_draws_the_rest_dashed()
    {
        // The tester's stair: straight, 18 risers up 3 m; a plan cuts at 1.20 m.
        var r = StairSolver.Resolve(StairSpec.Default(), 3.0);
        Assert.Equal(18, r.Risers);
        var (h, b) = (r.RiserHeight, r.Tread);
        var s = StairPlan.Of(r, 1.2);
        // One break line, a zigzag, through the centre line where the nosings' line is at 1.20 m, slanted across the flight.
        var br = Assert.Single(s.Breaks);
        Assert.Equal(6, br.Length);
        var mid = (br[0] + br[^1]) * 0.5;
        Assert.Equal((1.2 - h) * b / h, mid.X, 6);
        Assert.Equal(0, mid.Y, 6);
        Assert.Equal(-0.5, Math.Min(br[0].Y, br[^1].Y), 6);
        Assert.Equal(0.5, Math.Max(br[0].Y, br[^1].Y), 6);
        Assert.True(Math.Abs(br[^1].X - br[0].X) > 0.3, "slanted, not square across");
        // The risers wholly under the cut are seen, those wholly over it dashed; the ones the break crosses are both.
        foreach (var k in Enumerable.Range(1, r.Risers))
        {
            var u = (k - 1) * b;
            var on = s.Lines.Where(l => !l.Closed && l.Points.All(p => Math.Abs(p.X - u) < 1e-9)).ToList();
            Assert.NotEmpty(on);
            if (u < mid.X - 0.3) Assert.All(on, l => Assert.False(l.Above, $"riser {k}"));
            if (u > mid.X + 0.3) Assert.All(on, l => Assert.True(l.Above, $"riser {k}"));
        }
        // The flight's sides: seen up to the break, dashed past it.
        Assert.Contains(s.Lines, l => !l.Above && l.Points.Any(p => Math.Abs(p.Y + 0.5) < 1e-9) && l.Points.Max(p => p.X) < mid.X);
        Assert.Contains(s.Lines, l => l.Above && l.Points.Any(p => Math.Abs(p.Y - 0.5) < 1e-9) && l.Points.Min(p => p.X) > mid.X);
        // The walking line seen up to the break, dashed on to the top with its arrow there.
        Assert.Contains(s.Walk, w => !w.Above && Math.Abs(w.Points[^1].X - mid.X) < 1e-6);
        Assert.Contains(s.Walk, w => w.Above && Math.Abs(w.Points[0].X - mid.X) < 1e-6 && Math.Abs(w.Points[^1].X - (r.Risers - 1) * b) < 1e-6);
        Assert.Contains(s.Walk, w => w.Above && w.Points.Length == 3 && Math.Abs(w.Points[1].X - (r.Risers - 1) * b) < 1e-6);

        // Hidden: nothing over the cut, the arrow at the break.
        var hidden = StairPlan.Of(r, 1.2, "hidden");
        Assert.DoesNotContain(hidden.Lines.Concat(hidden.Walk), l => l.Above);
        Assert.Contains(hidden.Walk, w => w.Points.Length == 3 && Math.Abs(w.Points[1].X - mid.X) < 1e-6);
        Assert.Single(hidden.Breaks);
        // Whole: the stair entire, no break (as the plan drew it before).
        var whole = StairPlan.Of(r, 1.2, "whole");
        Assert.Empty(whole.Breaks);
        Assert.DoesNotContain(whole.Lines.Concat(whole.Walk), l => l.Above);
        // Double: two parallel lines.
        Assert.Equal(2, StairPlan.Of(r, 1.2, breakStyle: "double").Breaks.Count);
    }

    [Fact]
    public void A_stair_under_the_cut_draws_whole_and_one_over_it_all_dashed()
    {
        var r = StairSolver.Resolve(StairSpec.Default(), 3.0);
        var below = StairPlan.Of(r, 3.5);
        Assert.Empty(below.Breaks);
        Assert.DoesNotContain(below.Lines.Concat(below.Walk), l => l.Above);
        // The same lines as with no cut at all.
        Assert.Equal(StairPlan.Of(r, null).Lines.Count, below.Lines.Count);
        var over = StairPlan.Of(r, 0.05);
        Assert.Empty(over.Breaks);
        Assert.All(over.Lines, l => Assert.True(l.Above));
    }

    [Fact]
    public void Turning_winding_and_round_stairs_break_where_the_walking_line_reaches_the_cut()
    {
        foreach (var spec in new[]
        {
            StairSpec.Default() with { Shape = "u" },
            StairSpec.Default() with { Shape = "l" },
            StairSpec.Default() with { Shape = "l", Turn = "winders", Winders = 3 },
            StairSpec.Library["winder-u"](),
            StairSpec.Library["spiral"](),
            StairSpec.Library["curved"](),
            StairSpec.Library["split"](),
        })
            foreach (var cut in new[] { 0.6, 1.2, 1.5, 1.8, 2.4 })
            {
                var r = StairSolver.Resolve(spec, 3.0);
                var s = StairPlan.Of(r, cut);
                var what = $"{spec.Name} at {cut}";
                Assert.True(s.Lines.Any(l => !l.Above), what);
                Assert.True(s.Lines.Any(l => l.Above), what);
                Assert.True(s.Breaks.Count >= 1, what);
                // Landings and winders under the cut are seen, edge by edge; those over it dashed (but for an edge shared with one under it).
                foreach (var (outline, z) in r.Landings.Select(x => (x.Outline, x.Z)).Concat(r.Winders.Select(x => (x.Outline, x.Z))))
                {
                    var edges = Enumerable.Range(0, outline.Length).Select(i => (outline[i], outline[(i + 1) % outline.Length])).Where(e => (e.Item2 - e.Item1).Length > 1e-9)
                        .Select(e => s.Lines.First(l => l.Points.Length == 2 && ((Near(l.Points[0], e.Item1) && Near(l.Points[1], e.Item2)) || (Near(l.Points[0], e.Item2) && Near(l.Points[1], e.Item1))))).ToList();
                    if (z <= cut) Assert.All(edges, l => Assert.False(l.Above, what));
                    else Assert.Contains(edges, l => l.Above);
                    // Each edge once.
                    Assert.All(edges, l => Assert.Single(s.Lines, x => x.Points.Length == 2 && ((Near(x.Points[0], l.Points[0]) && Near(x.Points[1], l.Points[1])) || (Near(x.Points[0], l.Points[1]) && Near(x.Points[1], l.Points[0])))));
                }
                // The walking line seen up to the cut, then dashed on: once from one to the other (its polylines come before its arrow and its foot's dot).
                if (spec.Shape != "split")
                {
                    var flags = s.Walk.Take(s.Walk.Count - 2).Select(w => w.Above).ToList();
                    Assert.Equal(flags.OrderBy(x => x), flags);
                    Assert.Contains(false, flags);
                    Assert.Contains(true, flags);
                }
            }
    }

    [Fact]
    public void The_step_numbers_stand_beside_the_walking_line_never_on_it()
    {
        foreach (var spec in new[]
        {
            StairSpec.Default(),
            StairSpec.Default() with { Shape = "u" },
            StairSpec.Default() with { Shape = "l", Turn = "winders", Winders = 3 },
            StairSpec.Library["winder-u"](),
            StairSpec.Library["spiral"](),
            StairSpec.Library["curved"](),
            StairSpec.Library["split"](),
        })
        {
            var r = StairSolver.Resolve(spec, 3.0);
            var paths = StairPlan.Of(r, null).Paths;
            // At 1:50 and 1:100, a figure 2.5 mm high.
            foreach (var mm in new[] { 0.05, 0.1 })
            {
                var (radius, gap) = StairMarks.NumberSize(r, 2.5 * mm * 0.9, 0.6 * mm);
                var spots = StairMarks.NumberSpots(r, radius, gap, paths);
                Assert.Equal(Enumerable.Range(1, r.Risers), spots.Select(x => x.Number).Distinct().Order());
                foreach (var (at, n) in spots)
                    foreach (var path in paths)
                        for (var i = 0; i + 1 < path.Length; i++)
                            Assert.True(Distance(at, path[i], path[i + 1]) >= radius + gap / 2, $"{spec.Name}: number {n} on the walking line");
            }
        }
        // A straight stair's numbers on the right going up (the tester's drawing: under the line of a stair climbing to the right), within the flight.
        var straight = StairSolver.Resolve(StairSpec.Default(), 3.0);
        var (rad, g) = StairMarks.NumberSize(straight, 0.16, 0.06);
        Assert.All(StairMarks.NumberSpots(straight, rad, g, StairPlan.Of(straight, null).Paths), x => Assert.InRange(x.At.Y, -0.5 + rad - 1e-9, -rad));
    }

    [Fact]
    public void The_plan_draws_the_stair_cut_at_its_own_cut_plane_and_a_stair_under_it_whole()
    {
        var f = TwoStoreys();
        var id = Place(f, StairSpec.Default() with { Symbols = new StairPlanSymbols(Numbers: true) });
        var pens = Pens(f);
        Assert.Contains("\"STAIR\"", pens);
        Assert.Contains("\"STAIR-OVERHEAD\"", pens);
        Assert.Contains("\"STAIR-NUMBERS\"", pens);
        // The element's own plan lines are what a plan picks; the drawing leaves them to the marks.
        Assert.All(f.Session.Geometry(id)!.Plan!, i => Assert.Equal("outline", i.Kind));

        // The type's choice: nothing over the cut, or the stair whole.
        var type = f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!.Id;
        f.Run("architecture.updateStairType", new JsonObject { ["id"] = type, ["spec"] = (StairSpec.Default() with { Symbols = new StairPlanSymbols(AboveCut: "whole") }).ToJson() });
        Assert.DoesNotContain("\"STAIR-OVERHEAD\"", Pens(f));
        Assert.Contains("\"STAIR\"", Pens(f));

        // Entrance steps climb from 0.45 m under the level to it: wholly under the cut, drawn whole.
        var g = new Fixture(new ArchitectureModule());
        Place(g, StairSpec.Entrance());
        var steps = Pens(g);
        Assert.Contains("\"STAIR\"", steps);
        Assert.DoesNotContain("\"STAIR-OVERHEAD\"", steps);
    }
}
