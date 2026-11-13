using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// The loads on the bars (docs/plan-fem.md §9.2): the 45° rule on a 6 × 4 m plate (two trapezoids of 8 m², two triangles of
/// 4 m², q · 24 m² in all), a side without support giving its zone to the others, a piece with no supported side going to
/// its corners by area; on the model, a floor's area load onto its four beams (the sum on the supports the plate's load), a
/// line load on the beam under it, one off any beam on its slab piece's corners by the point rule integrated along it, a point load on a column's head, and a load
/// with nothing under it said.
/// </summary>
public class LoadMappingTests
{
    static readonly List<Vec2> Plate = [new(0, 0), new(6, 0), new(6, 4), new(0, 4)];

    static double Area(List<Vec2> ring) => ring.Count >= 3 ? Polygon2.SignedArea(ring) : 0;

    [Fact]
    public void The_45_degree_rule_shares_a_6_by_4_plate_into_two_trapezoids_and_two_triangles()
    {
        var zones = Tributary.Zones(Plate, [true, true, true, true]);
        Assert.Equal([8.0, 4.0, 8.0, 4.0], zones.Select(z => Math.Round(Area(z), 9)));
        Assert.Equal(24, zones.Sum(Area), 9);
        // The long side's load: rising to 2 m²/m over its first 2 m, level to 4 m, falling to its end.
        var p = Tributary.Profile([zones[0]], Plate[0], new Vec2(1, 0));
        Assert.Equal(8, p.Sum(t => t.Integral), 9);
        Assert.Equal(0, p.First().W0, 9);
        Assert.Equal(2, p.Max(t => Math.Max(t.W0, t.W1)), 9);
        Assert.Equal([0.0, 2.0, 4.0], p.Select(t => Math.Round(t.S0, 9)));
        Assert.Equal(2, p[1].W0, 9);
        Assert.Equal(2, p[1].W1, 9);
        // The short side's: a triangle, its peak 2 m²/m at its middle.
        var s = Tributary.Profile([zones[1]], Plate[1], new Vec2(0, 1));
        Assert.Equal(4, s.Sum(t => t.Integral), 9);
        Assert.Equal(2, s[0].W1, 9);
        Assert.Equal(2, s[0].S1, 9);
    }

    [Fact]
    public void A_side_without_support_gives_its_zone_to_the_others()
    {
        // The east side free: the north and south sides reach it, the west side's triangle stays.
        var zones = Tributary.Zones(Plate, [true, false, true, true]);
        Assert.Empty(zones[1]);
        Assert.Equal(24, zones.Sum(Area), 9);
        Assert.Equal(4, Area(zones[3]), 9);
        Assert.Equal(10, Area(zones[0]), 9);
        // Only two opposite sides held (a one-way slab): half each.
        var oneWay = Tributary.Zones(Plate, [true, false, true, false]);
        Assert.Equal(12, Area(oneWay[0]), 9);
        Assert.Equal(12, Area(oneWay[2]), 9);
        // A part of the plate loaded: each side takes what of the load is in its zone.
        var patch = new List<Vec2> { new(0, 0), new(2, 0), new(2, 1), new(0, 1) };
        var bySide = Tributary.Zones(Plate, [true, true, true, true]).Select(z => Area(Tributary.ClipTo(patch, z))).ToList();
        Assert.Equal(2, bySide.Sum(), 9);
        Assert.Equal(2 - 0.5, bySide[0], 9);
        Assert.Equal(0.5, bySide[3], 9);
    }

    [Fact]
    public void A_piece_with_no_supported_side_goes_to_its_corners_by_the_area_nearest_each()
    {
        var cells = Tributary.Cells(Plate, Plate);
        Assert.All(cells, c => Assert.Equal(6, Area(c), 9));
        var three = Tributary.Cells(Plate, [Plate[0], Plate[1], Plate[2]]);
        Assert.Equal(24, three.Sum(Area), 9);
    }

    static MappedCase Case(Fixture f, string code) =>
        AnalysisRun.Of(f.Session.Reader).Problem.Cases.Single(c => c.Loads.Id == LoadCaseTests.IdOf(f, code));

    static Dictionary<string, double> ByBar(Fixture f, MappedCase c)
    {
        var run = AnalysisRun.Of(f.Session.Reader);
        return c.Loads.Span.GroupBy(s => run.Problem.Frames[s.Element].Source.SourceId).ToDictionary(g => g.Key, g => -g.Sum(s => s.Load.Total.Z));
    }

    [Fact]
    public void A_floors_area_load_goes_onto_its_four_beams_by_the_45_degree_rule()
    {
        var t = AnalysisRunTests.Frame6x4();
        AnalysisRunTests.NoShells(t.F);
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        var q = Case(f, "Q-A");
        var by = ByBar(f, q);
        Assert.Equal(5 * 8, by[t.Beams[0]], 9);
        Assert.Equal(5 * 4, by[t.Beams[1]], 9);
        Assert.Equal(5 * 8, by[t.Beams[2]], 9);
        Assert.Equal(5 * 4, by[t.Beams[3]], 9);
        Assert.Equal(-120, q.Loads.Sum.Z, 9);
        Assert.Equal(0, q.Unmapped);
        // The long beam's load peaks at q · 2 = 10 kN/m.
        var run = AnalysisRun.Of(f.Session.Reader);
        var peak = q.Loads.Span.Where(s => run.Problem.Frames[s.Element].Source.SourceId == t.Beams[0]).Max(s => Math.Max(-s.Load.Qa.Z, -s.Load.Qb.Z));
        Assert.Equal(10, peak, 9);
        // The floor's own weight the same way: 0.2 m × 2500 kg/m³ × g over 24 m², on top of the beams' and columns' own.
        var g1 = Case(f, "G1");
        var slab = 0.2 * 2500 * 9.80665 / 1000;
        var beams = ByBar(f, g1);
        Assert.Equal(slab * 8 + 0.15 * 2500 * 9.80665 / 1000 * 6, beams[t.Beams[0]], 6);
        Assert.Equal(-run.Solution!.Cases.Single(c => c.Id == LoadCaseTests.IdOf(f, "Q-A")).SumReactions.Z, q.Loads.Sum.Z, 9);
    }

    [Fact]
    public void A_partly_loaded_floor_gives_each_beam_what_falls_in_its_zone()
    {
        var t = AnalysisRunTests.Frame6x4();
        AnalysisRunTests.NoShells(t.F);
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 2, 1), ["value"] = 4 });
        var by = ByBar(f, Case(f, "Q-A"));
        Assert.Equal(4 * 1.5, by[t.Beams[0]], 9);
        Assert.Equal(4 * 0.5, by[t.Beams[3]], 9);
        Assert.False(by.ContainsKey(t.Beams[1]));
    }

    [Fact]
    public void A_line_load_goes_on_the_beam_under_it_and_one_off_any_beam_on_its_slab_pieces_corners()
    {
        var t = AnalysisRunTests.Frame6x4();
        AnalysisRunTests.NoShells(t.F);
        var f = t.F;
        // Along the south beam's axis, 2 m of it: on that beam, over that stretch.
        f.Run("analysis.addLineLoad", new JsonObject { ["storeyId"] = t.First, ["a"] = new JsonArray(1, 0.1), ["b"] = new JsonArray(3, 0.1), ["value"] = 6 });
        var q = Case(f, "Q-A");
        var on = Assert.Single(q.Loads.Span);
        var run = AnalysisRun.Of(f.Session.Reader);
        Assert.Equal(t.Beams[0], run.Problem.Frames[on.Element].Source.SourceId);
        Assert.Equal(1, on.Load.A, 9);
        Assert.Equal(3, on.Load.B, 9);
        Assert.Equal(-6, on.Load.Qa.Z, 9);
        Assert.Equal(0, q.Spread);
        // Off any beam, from (1, 1) to (1, 3) at 6 kN/m: the point rule along it, the floor's corners (the columns' heads) by the
        // bilinear weights integrated — ξ = 1/6, η from 1/4 to 3/4 — so 5/12, 1/12, 1/12, 5/12 of its 12 kN. No bar loaded.
        f.Run("analysis.addLineLoad", new JsonObject { ["storeyId"] = t.First, ["a"] = new JsonArray(1, 1), ["b"] = new JsonArray(1, 3), ["value"] = 6, ["caseId"] = LoadCaseTests.IdOf(f, "G2") });
        var g2 = Case(f, "G2");
        Assert.Equal(1, g2.Spread);
        Assert.Empty(g2.Loads.Span);
        Assert.Equal(-12, g2.Loads.Sum.Z, 9);
        run = AnalysisRun.Of(f.Session.Reader);
        var at = g2.Loads.Nodal.ToDictionary(x => run.Problem.Problem.Nodes[x.Key].Xy, x => -x.Value.Z);
        Assert.Equal(5, at[new Vec2(0, 0)], 9);
        Assert.Equal(1, at[new Vec2(6, 0)], 9);
        Assert.Equal(1, at[new Vec2(6, 4)], 9);
        Assert.Equal(5, at[new Vec2(0, 4)], 9);
        Assert.Equal(12, run.Solution!.Cases.Single(c => c.Id == LoadCaseTests.IdOf(f, "G2")).SumReactions.Z, 9);
        Assert.Contains(AnalysisRunTests.Run(f)["notes"]!.AsArray(), n => ((string)n!).Contains("integrated along"));
    }

    [Fact]
    public void A_point_load_goes_on_a_columns_head_and_one_with_nothing_under_it_is_said()
    {
        var t = AnalysisRunTests.Frame6x4();
        AnalysisRunTests.NoShells(t.F);
        var f = t.F;
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = t.First, ["at"] = new JsonArray(6, 4), ["value"] = 15 });
        var q = Case(f, "Q-A");
        var (node, force) = Assert.Single(q.Loads.Nodal);
        var run = AnalysisRun.Of(f.Session.Reader);
        Assert.Equal(new Vec3(6, 4, 2.9), run.Problem.Problem.Nodes[node]);
        Assert.Equal(-15, force.Z, 9);
        // On a beam's span: a force on it there.
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = t.First, ["at"] = new JsonArray(2, 0.2), ["value"] = 7, ["caseId"] = LoadCaseTests.IdOf(f, "G2") });
        var g2 = Case(f, "G2");
        var p = Assert.Single(g2.Loads.Span);
        Assert.True(p.Load.Point);
        Assert.Equal(2, p.Load.A, 9);
        // Far from everything: left out, said.
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = t.First, ["at"] = new JsonArray(20, 20), ["value"] = 3, ["caseId"] = LoadCaseTests.IdOf(f, "S") });
        var s = Case(f, "S");
        Assert.Equal(3, s.Unmapped, 9);
        var run2 = AnalysisRunTests.Run(f);
        Assert.Equal(3, (double)run2["cases"]!.AsArray().Single(c => (string?)c!["code"] == "S")!["unmapped"]!, 9);
        Assert.Contains(run2["notes"]!.AsArray(), n => ((string)n!).StartsWith("„S”: 3 kN"));
    }
    [Fact]
    public void A_point_load_on_a_slab_goes_to_its_corners_by_the_bilinear_shape_functions()
    {
        // The floor's corners are the columns' heads: a force at (2, 1) of 6 × 4 m gives them 1/2, 1/4, 1/12 and 1/6 of it.
        var t = AnalysisRunTests.Frame6x4();
        AnalysisRunTests.NoShells(t.F);
        var f = t.F;
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = t.First, ["at"] = new JsonArray(2, 1), ["value"] = 24 });
        var q = Case(f, "Q-A");
        Assert.Empty(q.Loads.Span);
        Assert.Equal(1, q.OnCorners);
        var run = AnalysisRun.Of(f.Session.Reader);
        var at = q.Loads.Nodal.ToDictionary(x => run.Problem.Problem.Nodes[x.Key].Xy, x => -x.Value.Z);
        Assert.Equal(12, at[new Vec2(0, 0)], 9);
        Assert.Equal(6, at[new Vec2(6, 0)], 9);
        Assert.Equal(2, at[new Vec2(6, 4)], 9);
        Assert.Equal(4, at[new Vec2(0, 4)], 9);
        Assert.Equal(-24, q.Loads.Sum.Z, 9);
        Assert.Equal(0, q.Unmapped);
        Assert.Contains(AnalysisRunTests.Run(f)["notes"]!.AsArray(), n => ((string)n!).Contains("shared by the slab piece's corners"));
    }
}
