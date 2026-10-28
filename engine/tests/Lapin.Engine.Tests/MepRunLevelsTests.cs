using System.Text.Json.Nodes;
using Lapin.Mep;
using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>A run's own elevations, one per point (mep.setRunLevels), kept through handles added and points moved; a node at its own elevation, the run jogging to it.</summary>
public class MepRunLevelsTests
{
    static (MepFixture F, string System, string Boiler, string Radiator, string Run) Heating()
    {
        var f = new MepFixture();
        var s = f.System(Services.Heating, new JsonObject { ["pair"] = false });
        var boiler = f.Node(s, "boiler", 0, 0, new JsonObject { ["elevation"] = 1.2 });
        var radiator = f.Node(s, "radiator", 6, 3);
        var run = f.Draw(s, MepFixture.Pts((-0.025, -0.175), (3, -0.175), (6, -0.175), (5.975, 2.95)), new JsonObject { ["nodeId"] = boiler }, new JsonObject { ["nodeId"] = radiator });
        return (f, s, boiler, radiator, run);
    }

    [Fact]
    public void The_boiler_stands_at_its_own_elevation_and_the_run_jogs_to_it()
    {
        var (f, _, boiler, _, run) = Heating();
        var e = f.Model.Get(run)!;
        var line = Runs.Centreline(f.Model, e, Runs.SettingOf(f.Model, e), out _, out var vertexOf);
        Assert.Equal(1.2, line[0].Z, 6);
        Assert.Null(vertexOf[0]);
        Assert.Equal(0.2, line[1].Z, 6);
        // Raised (as the profile's handle does): the run's first point follows.
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(boiler), ["attrs"] = new JsonObject { ["elevation"] = 1.5 } });
        line = Runs.Centreline(f.Model, f.Model.Get(run)!, Runs.SettingOf(f.Model, f.Model.Get(run)!), out _);
        Assert.Equal(1.5, line[0].Z, 6);
    }

    [Fact]
    public void A_point_raised_keeps_its_level_through_new_handles_and_moves()
    {
        var (f, _, _, _, run) = Heating();
        f.Run("mep.setRunLevels", new JsonObject { ["id"] = run, ["levels"] = new JsonArray(null, 2.4, 2.4, null) });
        var e = f.Model.Get(run)!;
        var line = Runs.Centreline(f.Model, e, Runs.SettingOf(f.Model, e), out _);
        Assert.Contains(line, p => Math.Abs(p.Z - 2.4) < 1e-9 && Math.Abs(p.X - 3) < 1e-9);
        // A handle in the middle of the raised leg takes its level; one between 0,2 and 2,4 takes the level in proportion.
        f.Run("mep.addHandle", new JsonObject { ["runId"] = run, ["x"] = 4.5, ["y"] = -0.175 });
        f.Run("mep.addHandle", new JsonObject { ["runId"] = run, ["x"] = 1.5, ["y"] = -0.175 });
        var levels = Runs.LevelsOf(f.Model.Get(run)!)!;
        Assert.Equal(6, levels.Count);
        Assert.Equal(2.4, levels[3]!.Value, 6);
        Assert.InRange(levels[1]!.Value, 1.2, 1.4);
        // A point moved in plan keeps its level.
        var pts = Runs.Points(f.Model.Get(run)!);
        pts[2] = new(3, -1);
        f.Run("mep.setRunPoints", new JsonObject { ["id"] = run, ["points"] = Runs.J(pts) });
        Assert.Equal(2.4, Runs.LevelsOf(f.Model.Get(run)!)![2]!.Value, 6);
        // Cleared: back to the run's elevation.
        f.Run("mep.setRunLevels", new JsonObject { ["id"] = run, ["levels"] = new JsonArray(Enumerable.Range(0, 6).Select(_ => (JsonNode?)null).ToArray()) });
        Assert.Null(Runs.LevelsOf(f.Model.Get(run)!));
    }
}
