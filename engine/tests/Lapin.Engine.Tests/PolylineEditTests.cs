using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Split, Trim/Extend and Offset on elements along a polyline of points and arcs (audit 2026-11-15 §15, the leftovers of
/// decision 619): railings, MEP runs, paths and roads. One undo step each; the first part keeps its GlobalId; the parts' lengths
/// add up to the element's.
/// </summary>
public sealed class PolylineEditTests
{
    static JsonArray P(double x, double y) => new(x, y);
    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);
    static string[] Ids(JsonNode? r) => [.. ((JsonArray)r!["ids"]!).Select(n => (string)n!)];
    static void Near(double x, double y, Vec2 got) { Assert.Equal(x, got.X, 6); Assert.Equal(y, got.Y, 6); }

    static double Q(Fixture f, string id, string set, string name) =>
        f.Session.Quantities(id).Single(q => q.Name == set).Quantities.Single(q => q.Name == name).Value;

    // ─── The line ─────────────────────────────────────────────────────────────

    [Fact]
    public void A_line_is_sliced_at_its_stations_and_its_arc_cut_into_two_arcs_of_the_same_circle()
    {
        // A straight leg, then a half circle of radius 1 (rise 1 to the left) from (2, 0) to (2, 2).
        var line = new PolylineEdits.Line([new(0, 0), new(2, 0), new(2, 2)], [0, -1]);
        Assert.Equal(2 + Math.PI, line.Length, 9);
        var a = line.Slice(0, 2 + Math.PI / 2);
        var b = line.Slice(2 + Math.PI / 2, line.Length);
        Assert.Equal(line.Length, new PolylineEdits.Line(a.Points, a.Rises).Length + new PolylineEdits.Line(b.Points, b.Rises).Length, 6);
        Near(3, 1, a.Points[^1]);
        Assert.Equal([0, 1, null], a.From);
        // Each quarter circle's rise: r (1 − cos 45°).
        Assert.Equal(1 - Math.Cos(Math.PI / 4), Math.Abs(a.Rises[1]), 6);
        Assert.Equal(1 - Math.Cos(Math.PI / 4), Math.Abs(b.Rises[0]), 6);
        // Carried on before its start, along its first segment.
        Near(-1, 0, line.Slice(-1, 1).Points[0]);
    }

    [Fact]
    public void An_offset_line_has_its_corners_where_the_moved_segments_meet_and_its_arcs_concentric()
    {
        var line = new PolylineEdits.Line([new(0, 0), new(4, 0), new(4, 3)], null);
        var o = line.Offset(1);
        Near(0, 1, o.Points[0]); Near(3, 1, o.Points[1]); Near(3, 3, o.Points[2]);
        var arc = new PolylineEdits.Line([new(1, 0), new(-1, 0)], [-1]).Offset(0.5);
        // The half circle of radius 1 round (0, 0), moved half a metre to its left (inwards): radius 0.5.
        Near(0.5, 0, arc.Points[0]); Near(-0.5, 0, arc.Points[1]);
        Assert.Equal(0.5, Math.Abs(arc.Rises[0]), 6);
    }

    // ─── Railings ─────────────────────────────────────────────────────────────

    static string Railing(Fixture f, JsonArray points, JsonArray? arcs = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["points"] = points };
        if (arcs != null) a["arcs"] = arcs;
        return (string)f.Run("architecture.addRailing", a)!["id"]!;
    }

    static double RailLength(Fixture f, string id) => Q(f, id, "Qto_RailingBaseQuantities", "Length");

    [Fact]
    public void A_railing_split_on_its_arc_gives_two_railings_whose_lengths_add_up_the_first_keeping_its_id()
    {
        var f = new Fixture(new ArchitectureModule());
        var r = Railing(f, Pts((0, 0), (2, 0), (2, 2)), new JsonArray(0, -1));
        var length = RailLength(f, r);
        Assert.Equal(2 + Math.PI, length, 6);
        var ids = Ids(f.Run("element.split", new JsonObject { ["id"] = r, ["at"] = P(3.05, 1) }));
        Assert.Equal(r, ids[0]);
        Assert.Equal(length, RailLength(f, ids[0]) + RailLength(f, ids[1]), 6);
        Near(3, 1, Railings.Points(f.Model.Get(ids[1])!)[0]);
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, ids[1])!.Id);
        Assert.Equal(f.Model.Get(r)!.Str("style"), f.Model.Get(ids[1])!.Str("style"));
        f.Session.Undo();
        Assert.Single(f.Model.OfClass(Railings.Class));
        Assert.Equal(length, RailLength(f, r), 6);
    }

    [Fact]
    public void A_railing_is_trimmed_at_a_drafted_line_extended_to_one_and_offset_parallel()
    {
        var f = new Fixture(new ArchitectureModule());
        var r = Railing(f, Pts((0, 0), (4, 0), (4, 3)));
        // Trimmed where the line y = 2 crosses its second segment, the part picked kept.
        f.Run("element.trimExtend", new JsonObject { ["id"] = r, ["at"] = P(2, 0), ["line"] = new JsonArray(P(-10, 2), P(10, 2)) });
        Near(4, 2, Railings.Points(f.Model.Get(r)!)[^1]);
        Assert.Equal(6, RailLength(f, r), 6);
        // Extended back before its start to x = -1.
        f.Run("element.trimExtend", new JsonObject { ["id"] = r, ["at"] = P(2, 0), ["line"] = new JsonArray(P(-1, -5), P(-1, 5)) });
        Near(-1, 0, Railings.Points(f.Model.Get(r)!)[0]);
        Assert.Equal(7, RailLength(f, r), 6);
        // Offset 0.5 m inside the corner.
        var copy = (string)f.Run("element.offset", new JsonObject { ["id"] = r, ["side"] = P(2, 1), ["distance"] = 0.5 })!["id"]!;
        var pts = Railings.Points(f.Model.Get(copy)!);
        Near(-1, 0.5, pts[0]); Near(3.5, 0.5, pts[1]); Near(3.5, 2, pts[2]);
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, copy)!.Id);
    }

    [Fact]
    public void A_pinned_railing_is_not_split_and_a_cut_too_near_an_end_is_refused()
    {
        var f = new Fixture(new ArchitectureModule());
        var r = Railing(f, Pts((0, 0), (4, 0)));
        Assert.Throws<CommandException>(() => f.Run("element.split", new JsonObject { ["id"] = r, ["at"] = P(0.01, 0) }));
        f.Run("element.pin", new JsonObject { ["ids"] = new JsonArray(r), ["pinned"] = true });
        Assert.Throws<CommandException>(() => f.Run("element.split", new JsonObject { ["id"] = r, ["at"] = P(2, 0) }));
    }

    // ─── MEP runs ─────────────────────────────────────────────────────────────

    static double RunLength(MepFixture f, string id) => f.Session.Quantities(id).Single(q => q.Name == "Lapin_Mep").Quantities.Single(q => q.Name == "Length").Value;

    [Fact]
    public void A_run_split_takes_a_coupling_between_its_parts_its_end_and_branches_on_the_part_they_are_on()
    {
        var f = new MepFixture();
        var sys = f.System(Lapin.Mep.Services.ColdWater, new JsonObject { ["pair"] = false });
        var run = f.Id("mep.drawRun", new JsonObject { ["systemId"] = sys, ["points"] = Pts((0, 0), (6, 0), (6, 4)) });
        var basin = f.Node(sys, "basin", 6, 4.3);
        f.Run("mep.connect", new JsonObject { ["runId"] = run, ["end"] = "end", ["to"] = new JsonObject { ["nodeId"] = basin } });
        var branch = f.Id("mep.drawRun", new JsonObject { ["systemId"] = sys, ["points"] = Pts((2, 3), (2, 0)), ["end"] = new JsonObject { ["runId"] = run, ["station"] = 2 } });
        var far = f.Id("mep.drawRun", new JsonObject { ["systemId"] = sys, ["points"] = Pts((8, 2), (6, 2)), ["end"] = new JsonObject { ["runId"] = run, ["station"] = 8 } });
        var length = RunLength(f, run);

        var ids = Ids(f.Run("element.split", new JsonObject { ["id"] = run, ["at"] = P(4, 0.1) }));
        Assert.Equal(run, ids[0]);
        var second = ids[1];
        Assert.Equal(length, RunLength(f, run) + RunLength(f, second), 6);
        // A coupling between the two, at the cut; the basin on the second; the branches on the parts they join.
        var coupling = Lapin.Mep.Runs.EndOf(f.Model, f.Model.Get(run)!, "end")!.NodeId!;
        Assert.Equal("coupling", f.Model.Get(coupling)!.Str("role"));
        Assert.Equal("IfcPipeFitting", f.Model.Get(coupling)!.Class);
        Assert.Equal(coupling, Lapin.Mep.Runs.EndOf(f.Model, f.Model.Get(second)!, "start")!.NodeId);
        Near(4, 0, Lapin.Mep.Nodes.Position(f.Model.Get(coupling)!));
        Assert.Equal(basin, Lapin.Mep.Runs.EndOf(f.Model, f.Model.Get(second)!, "end")!.NodeId);
        Assert.Equal(run, Lapin.Mep.Runs.EndOf(f.Model, f.Model.Get(branch)!, "end")!.RunId);
        var farEnd = Lapin.Mep.Runs.EndOf(f.Model, f.Model.Get(far)!, "end")!;
        Assert.Equal(second, farEnd.RunId);
        Assert.Equal(4, farEnd.Station, 6);
        Assert.Equal(Spatial.StoreyOf(f.Model, run)!.Id, Spatial.StoreyOf(f.Model, second)!.Id);

        f.Session.Undo();
        Assert.Null(f.Model.Get(second));
        Assert.Null(f.Model.Get(coupling));
        Assert.Equal(basin, Lapin.Mep.Runs.EndOf(f.Model, f.Model.Get(run)!, "end")!.NodeId);
    }

    [Fact]
    public void A_run_split_without_a_coupling_leaves_two_open_ends_and_a_trim_lets_go_of_its_port()
    {
        var f = new MepFixture();
        var sys = f.System(Lapin.Mep.Services.ColdWater, new JsonObject { ["pair"] = false });
        var run = f.Id("mep.drawRun", new JsonObject { ["systemId"] = sys, ["points"] = Pts((0, 0), (6, 0)) });
        var basin = f.Node(sys, "basin", 6, 0.3);
        f.Run("mep.connect", new JsonObject { ["runId"] = run, ["end"] = "end", ["to"] = new JsonObject { ["nodeId"] = basin } });
        var ids = Ids(f.Run("element.split", new JsonObject { ["id"] = run, ["at"] = P(2, 0), ["coupling"] = false }));
        Assert.Null(Lapin.Mep.Runs.EndOf(f.Model, f.Model.Get(ids[0])!, "end"));
        Assert.Null(Lapin.Mep.Runs.EndOf(f.Model, f.Model.Get(ids[1])!, "start"));
        Assert.DoesNotContain(f.Model.OfClass("IfcPipeFitting"), n => n.Str("role") == "coupling");
        // The second part trimmed back from the basin: its end lets go of the port.
        f.Run("element.trimExtend", new JsonObject { ["id"] = ids[1], ["at"] = P(3, 0), ["line"] = new JsonArray(P(5, -1), P(5, 1)) });
        Assert.Null(Lapin.Mep.Runs.EndOf(f.Model, f.Model.Get(ids[1])!, "end"));
        Near(5, 0, Lapin.Mep.Runs.Points(f.Model.Get(ids[1])!)[^1]);
        // A branch on the part a trim cuts away refuses.
        var branch = f.Id("mep.drawRun", new JsonObject { ["systemId"] = sys, ["points"] = Pts((1, 3), (1, 0)), ["end"] = new JsonObject { ["runId"] = ids[0], ["station"] = 1 } });
        var e = Assert.Throws<CommandException>(() => f.Run("element.trimExtend", new JsonObject { ["id"] = ids[0], ["at"] = P(1.8, 0), ["line"] = new JsonArray(P(1.5, -1), P(1.5, 1)) }));
        Assert.Contains("cut away", e.Message);
        // An offset copy runs parallel.
        var copy = (string)f.Run("element.offset", new JsonObject { ["id"] = ids[0], ["side"] = P(1, -1), ["distance"] = 0.3 })!["id"]!;
        Near(0, -0.3, Lapin.Mep.Runs.Points(f.Model.Get(copy)!)[0]);
    }

    // ─── Paths and roads ──────────────────────────────────────────────────────

    static JsonArray Curves(params (double R, double A, double B)[] c) => new([.. c.Select(x => (JsonNode)new JsonArray(x.R, x.A, x.B))]);
    static double PathLength(Fixture f, string id) => Q(f, id, "Lapin_PavementQuantities", "Length");

    [Fact]
    public void A_path_split_keeps_its_stations_profile_and_terrain_cut_on_both_parts_whose_lengths_and_volumes_add_up()
    {
        var f = new Fixture(new Lapin.Civil.CivilModule());
        f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = -20, ["y1"] = -20, ["x2"] = 60, ["y2"] = 60, ["elevation"] = 0 });
        var p = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts((0, 0), (20, 0), (20, 10)), ["width"] = 2 })!["id"]!;
        f.Run("civil.setCorridor", new JsonObject
        {
            ["id"] = p, ["stationStart"] = 100, ["sections"] = new JsonArray(105, 125),
            ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(new JsonArray(100, 0), new JsonArray(130, 1.5)) },
        });
        var terrain = Lapin.Civil.Terrains.TerrainOf(f.Model, p);
        var (length, area) = (PathLength(f, p), Q(f, p, "Lapin_PavementQuantities", "GrossArea"));

        var ids = Ids(f.Run("element.split", new JsonObject { ["id"] = p, ["at"] = P(12, 0.4) }));
        Assert.Equal(p, ids[0]);
        Assert.Equal(length, PathLength(f, p) + PathLength(f, ids[1]), 6);
        Assert.Equal(area, Q(f, p, "Lapin_PavementQuantities", "GrossArea") + Q(f, ids[1], "Lapin_PavementQuantities", "GrossArea"), 6);
        var (a, b) = (Lapin.Civil.CorridorSpec.Of(f.Model.Get(p)!), Lapin.Civil.CorridorSpec.Of(f.Model.Get(ids[1])!));
        Assert.Equal(100, a.StationStart, 9);
        Assert.Equal(112, b.StationStart, 9);
        Assert.Equal([105.0], a.Sections);
        Assert.Equal([125.0], b.Sections);
        Assert.Equal(2, b.Pvis.Count);
        Assert.Equal("design", b.Mode);
        if (terrain != null) Assert.Equal(terrain.Id, Lapin.Civil.Terrains.TerrainOf(f.Model, ids[1])?.Id);
        f.Session.Undo();
        Assert.Single(f.Model.OfClass(IfcClass.Pavement));
    }

    [Fact]
    public void A_road_with_a_curve_is_cut_on_a_straight_stretch_keeping_its_curve_but_not_in_the_curve()
    {
        var f = new Fixture(new Lapin.Civil.CivilModule());
        var p = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts((0, 0), (40, 0), (40, 40)), ["width"] = 6 })!["id"]!;
        f.Run("civil.setCorridor", new JsonObject { ["id"] = p, ["curves"] = Curves((0, 0, 0), (10, 0, 0), (0, 0, 0)) });
        // In the curve (it runs from (30, 0) round to (40, 10)): refused.
        var e = Assert.Throws<CommandException>(() => f.Run("element.split", new JsonObject { ["id"] = p, ["at"] = P(39.9, 0.1) }));
        Assert.Contains("curve", e.Message);
        var ids = Ids(f.Run("element.split", new JsonObject { ["id"] = p, ["at"] = P(40, 30) }));
        var first = Lapin.Civil.CorridorSpec.Of(f.Model.Get(p)!);
        Assert.Equal(10, first.Curves[1].Radius, 9);
        Assert.Null(Lapin.Civil.Corridors.ChainOf(f.Model.Get(p)!).Problem);
        // The second starts where the first ends, along the curved axis.
        Assert.Equal(Lapin.Civil.Corridors.ChainOf(f.Model.Get(p)!).End, Lapin.Civil.CorridorSpec.Of(f.Model.Get(ids[1])!).StationStart, 6);
        // Trimmed back on its first tangent, extended again.
        f.Run("element.trimExtend", new JsonObject { ["id"] = p, ["at"] = P(20, 0), ["line"] = new JsonArray(P(5, -5), P(5, 5)) });
        Assert.Equal(5, Lapin.Civil.CorridorSpec.Of(f.Model.Get(p)!).StationStart, 6);
    }
}
