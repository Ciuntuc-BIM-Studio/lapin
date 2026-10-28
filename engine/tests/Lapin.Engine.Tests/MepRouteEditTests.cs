using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>A run's route edited after it is placed (decision 164): its points where it is drawn, a leg slid square to itself, a detour round an obstacle, the path drawn again.</summary>
public class MepRouteEditTests
{
    static (MepFixture F, string System, string Boiler, string Radiator, string Run) Heating()
    {
        var f = new MepFixture();
        var s = f.System(Services.Heating, new JsonObject { ["pair"] = false });
        var boiler = f.Node(s, "boiler", 0, 0);
        var r = f.Run("mep.placeNode", new JsonObject { ["systemId"] = s, ["role"] = "radiator", ["x"] = 6, ["y"] = 4, ["connect"] = true })!;
        return (f, s, boiler, (string)r["id"]!, (string)r["runId"]!);
    }

    static List<Vec2> Stored(MepFixture f, string run) => Runs.Points(f.Model.Get(run)!);

    /// <summary>The centreline as the plan sees it: one point where the 3D has a vertical jog.</summary>
    static List<Vec2> Drawn(MepFixture f, string run)
    {
        var e = f.Model.Get(run)!;
        var o = new List<Vec2>();
        foreach (var p in Runs.Centreline(f.Model, e, Runs.SettingOf(f.Model, e), out _)) if (o.Count == 0 || (p.Xy - o[^1]).Length > 1e-9) o.Add(p.Xy);
        return o;
    }

    static void SameLine(List<Vec2> a, List<Vec2> b)
    {
        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++) { Assert.Equal(a[i].X, b[i].X, 5); Assert.Equal(a[i].Y, b[i].Y, 5); }
    }

    static void Orthogonal(List<Vec2> pts)
    {
        for (var i = 1; i < pts.Count; i++)
            Assert.True(Math.Abs(pts[i].X - pts[i - 1].X) < 1e-6 || Math.Abs(pts[i].Y - pts[i - 1].Y) < 1e-6, $"leg {i - 1} is not axis-aligned");
    }

    static Vec2 PortOf(MepFixture f, string run, string end)
    {
        var e = Runs.EndOf(f.Model, f.Model.Get(run)!, end)!;
        return Nodes.Port(f.Model, f.Model.Get(e.NodeId!)!, e.Port!)!.At.Xy;
    }

    [Fact]
    public void A_node_moved_writes_its_runs_where_they_are_drawn_and_a_detour_survives()
    {
        var (f, _, _, radiator, run) = Heating();
        var first = Stored(f, run);
        f.Run("mep.detour", new JsonObject { ["id"] = run, ["from"] = new JsonArray(1.5, first[0].Y), ["to"] = new JsonArray(3, first[0].Y), ["offset"] = -0.6 });
        Assert.Equal(first.Count + 4, Stored(f, run).Count);
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(radiator), ["op"] = "move", ["by"] = new JsonArray(1, 1.5) });
        var pts = Stored(f, run);
        SameLine(pts, Drawn(f, run));
        Assert.Equal(PortOf(f, run, "end").X, pts[^1].X, 6);
        Assert.Equal(PortOf(f, run, "end").Y, pts[^1].Y, 6);
        // The bypass is where it was.
        Assert.Contains(pts, p => Math.Abs(p.X - 1.5) < 1e-6 && Math.Abs(p.Y - (first[0].Y - 0.6)) < 1e-6);
        Assert.Contains(pts, p => Math.Abs(p.X - 3) < 1e-6 && Math.Abs(p.Y - (first[0].Y - 0.6)) < 1e-6);
        Orthogonal(pts);
        // Changed in its properties (element.update): the same.
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(radiator), ["attrs"] = new JsonObject { ["y"] = 3 } });
        SameLine(Stored(f, run), Drawn(f, run));
    }

    [Fact]
    public void An_interior_leg_slides_square_to_itself_and_its_neighbours_stretch()
    {
        var f = new MepFixture();
        var s = f.System(Services.Heating, new JsonObject { ["pair"] = false });
        var run = f.Draw(s, MepFixture.Pts((0, 0), (4, 0), (4, 3), (8, 3)));
        f.Run("mep.slideLeg", new JsonObject { ["id"] = run, ["leg"] = 1, ["by"] = new JsonArray(1, 0.7) });
        var pts = Stored(f, run);
        SameLine([new(0, 0), new(5, 0), new(5, 3), new(8, 3)], pts);
        Orthogonal(pts);
    }

    [Fact]
    public void The_first_leg_of_a_connected_run_slides_with_a_jog_at_its_port()
    {
        var (f, s, boiler, _, run) = Heating();
        var before = Stored(f, run);
        f.Run("mep.slideLeg", new JsonObject { ["id"] = run, ["leg"] = 0, ["by"] = new JsonArray(0.3, -0.5) });
        var pts = Stored(f, run);
        // The port, a jog square off it, the slid leg (its far corner stretched down the next leg): one point more, one bend more.
        Assert.Equal(before.Count + 1, pts.Count);
        Assert.Equal(pts[0].X, pts[1].X, 6);
        Assert.Equal(before[0].Y - 0.5, pts[1].Y, 6);
        Assert.Equal(PortOf(f, run, "start").X, pts[0].X, 6);
        Assert.Equal(PortOf(f, run, "start").Y, pts[0].Y, 6);
        Assert.Equal(before[0].Y - 0.5, pts[2].Y, 6);
        Orthogonal(pts);
        SameLine(pts, Drawn(f, run));
        Assert.Equal(boiler, Runs.EndOf(f.Model, f.Model.Get(run)!, "start")!.NodeId);
        var net = (JsonArray)f.Session.Query("mep.network", new JsonObject { ["systemId"] = s })!;
        var r = net[0]!["runs"]!.AsArray().First(x => (string?)x!["id"] == run)!;
        Assert.Equal(boiler, (string?)r["start"]!["nodeId"]);
    }

    [Fact]
    public void A_detour_adds_four_corners_out_along_and_back_in_one_undo_step()
    {
        var (f, _, boiler, radiator, run) = Heating();
        var before = Stored(f, run);
        var y = before[0].Y;
        f.Run("mep.detour", new JsonObject { ["id"] = run, ["from"] = new JsonArray(3.5, y + 0.05), ["to"] = new JsonArray(1.5, y - 0.02), ["side"] = new JsonArray(2, y + 1) });
        var pts = Stored(f, run);
        Assert.Equal(before.Count + 4, pts.Count);
        SameLine([new(1.5, y), new(1.5, y + 1), new(3.5, y + 1), new(3.5, y)], pts.Skip(1).Take(4).ToList());
        Assert.Equal(Pipes.Length(before) + 2, Pipes.Length(pts), 6);
        Orthogonal(pts);
        Assert.Equal(boiler, Runs.EndOf(f.Model, f.Model.Get(run)!, "start")!.NodeId);
        Assert.Equal(radiator, Runs.EndOf(f.Model, f.Model.Get(run)!, "end")!.NodeId);
        // An offset given with a side takes the side's sign.
        f.Session.Undo();
        SameLine(before, Stored(f, run));
        f.Run("mep.detour", new JsonObject { ["id"] = run, ["from"] = new JsonArray(1.5, y), ["to"] = new JsonArray(3.5, y), ["offset"] = 0.4, ["side"] = new JsonArray(2, y - 3) });
        Assert.Contains(Stored(f, run), p => Math.Abs(p.Y - (y - 0.4)) < 1e-6);
        Assert.Throws<CommandException>(() => f.Run("mep.detour", new JsonObject { ["id"] = run, ["from"] = new JsonArray(1, y), ["to"] = new JsonArray(1.001, y), ["offset"] = 0.4 }));
    }

    [Fact]
    public void A_run_redrawn_keeps_its_id_system_size_and_its_ends_on_their_ports()
    {
        var (f, s, boiler, radiator, run) = Heating();
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(run), ["attrs"] = new JsonObject { ["size"] = 0.025 } });
        f.Run("mep.redrawRun", new JsonObject { ["id"] = run, ["points"] = MepFixture.Pts((0, -1), (3, -1), (3, 2), (6, 2), (6, 3.5)) });
        var e = f.Model.Get(run)!;
        Assert.Equal(s, e.Str("systemId"));
        Assert.Equal(0.025, e.Num("size"), 9);
        var pts = Stored(f, run);
        Assert.Equal(5, pts.Count);
        Assert.Equal(PortOf(f, run, "start").X, pts[0].X, 6);
        Assert.Equal(PortOf(f, run, "start").Y, pts[0].Y, 6);
        Assert.Equal(PortOf(f, run, "end").X, pts[^1].X, 6);
        Assert.Equal(PortOf(f, run, "end").Y, pts[^1].Y, 6);
        Assert.Equal(3, pts[1].X, 6);
        Assert.Equal(2, pts[2].Y, 6);
        Orthogonal(pts);
        SameLine(pts, Drawn(f, run));
        Assert.Equal(boiler, Runs.EndOf(f.Model, e, "start")!.NodeId);
        Assert.Equal(radiator, Runs.EndOf(f.Model, e, "end")!.NodeId);
    }

    [Fact]
    public void A_branch_stays_where_it_joined_when_its_trunk_takes_a_detour()
    {
        var f = new MepFixture();
        var s = f.System(Services.Heating, new JsonObject { ["pair"] = false });
        var trunk = f.Draw(s, MepFixture.Pts((0, 0), (10, 0)));
        var branch = f.Draw(s, MepFixture.Pts((6, 0), (6, 3)), new JsonObject { ["runId"] = trunk, ["at"] = new JsonArray(6, 0) });
        f.Run("mep.detour", new JsonObject { ["id"] = trunk, ["from"] = new JsonArray(1, 0), ["to"] = new JsonArray(3, 0), ["offset"] = 0.5 });
        Assert.Equal(7, Runs.EndOf(f.Model, f.Model.Get(branch)!, "start")!.Station, 6);
        Assert.Equal(6, Stored(f, branch)[0].X, 6);
        Assert.Equal(0, Stored(f, branch)[0].Y, 6);
    }
}
