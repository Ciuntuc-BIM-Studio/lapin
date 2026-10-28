using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The network core (docs/plan-mep.md, stage 1): systems, nodes with ports, runs, connections that hold, parts.</summary>
public class MepNetworkTests
{
    static Vec3 PortAt(MepFixture f, string nodeId, string key) => Nodes.Port(f.Model, f.Model.Get(nodeId)!, key)!.At;

    [Fact]
    public void A_system_has_its_services_defaults_and_a_paired_run_joins_a_boiler_to_a_radiator()
    {
        var f = new MepFixture();
        var s = f.System();
        var system = f.Model.Get(s)!;
        Assert.Equal(Systems.Class, system.Class);
        var d = Systems.Of(system);
        Assert.True(d.Pair);
        Assert.Equal(0.2, d.FloorElevation);
        var boiler = f.Node(s, "boiler", 0, 0);
        var radiator = f.Node(s, "radiator", 4, 3);
        // The boiler's supply leaves its front (−Y); the radiator's ports face down: the run goes from one to the other.
        var run = f.Draw(s, MepFixture.Pts((0, -0.5), (4, -0.5), (4, 3)), new JsonObject { ["nodeId"] = boiler }, new JsonObject { ["nodeId"] = radiator });
        var e = f.Model.Get(run)!;
        Assert.Equal("IfcPipeSegment", e.Class);
        Assert.Equal(boiler, Runs.EndOf(f.Model, e, "start")!.NodeId);
        Assert.Equal("supply", Runs.EndOf(f.Model, e, "start")!.Port);
        Assert.Equal(radiator, Runs.EndOf(f.Model, e, "end")!.NodeId);
        var g = f.Session.Geometry(run)!;
        Assert.Contains(g.Parts, p => p.Key == "supply");
        Assert.Contains(g.Parts, p => p.Key == "return");
        Assert.Contains(g.Parts, p => p.Key!.StartsWith("bend:", StringComparison.Ordinal));
        // Both ends are pinned to the ports: the centreline starts between the boiler's pair and ends between the radiator's.
        var line = Runs.Centreline(f.Model, e, Runs.SettingOf(f.Model, e), out _);
        var bs = (PortAt(f, boiler, "supply") + PortAt(f, boiler, "return")) * 0.5;
        var rs = (PortAt(f, radiator, "supply") + PortAt(f, radiator, "return")) * 0.5;
        Assert.Equal(bs.X, line[0].X, 6); Assert.Equal(bs.Y, line[0].Y, 6); Assert.Equal(bs.Z, line[0].Z, 6);
        Assert.Equal(rs.X, line[^1].X, 6); Assert.Equal(rs.Y, line[^1].Y, 6); Assert.Equal(rs.Z, line[^1].Z, 6);
        // The wall-hung boiler's ports stand at 1 m: the run jogs down from them to its floor elevation and runs level.
        Assert.Equal(1.0, line[0].Z, 6);
        Assert.Equal(0.2, line[1].Z, 6);
        Assert.Equal(line[0].X, line[1].X, 6);
        // Nothing is open on either, and the quantities count both pipes.
        Assert.DoesNotContain(f.Open(s).OfType<JsonObject>(), o => (string?)o["id"] is var id && (id == boiler || id == radiator));
        var q = f.Session.Quantities(run).Single().Quantities.ToDictionary(x => x.Name, x => x.Value);
        Assert.Equal(2 * q["Length"], q["PipeLength"], 9);
        Assert.Equal(0.02, q["Size"]);
        Assert.Contains(g.Plan!, i => i.Kind == "symbol" && !i.Closed && i.Points.Count >= 3);
    }

    [Fact]
    public void A_moved_radiator_keeps_its_run_which_follows_with_its_last_leg_kept_square()
    {
        var f = new MepFixture();
        var s = f.System();
        var boiler = f.Node(s, "boiler", 0, 0);
        var radiator = f.Node(s, "radiator", 4, 3);
        var run = f.Draw(s, MepFixture.Pts((0, -0.5), (4, -0.5), (4, 3)), new JsonObject { ["nodeId"] = boiler }, new JsonObject { ["nodeId"] = radiator });
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(radiator), ["op"] = "move", ["by"] = new JsonArray(1.5, 0) });
        var e = f.Model.Get(run)!;
        Assert.Equal(radiator, Runs.EndOf(f.Model, e, "end")!.NodeId);
        var line = Runs.Centreline(f.Model, e, Runs.SettingOf(f.Model, e), out _);
        var rs = (PortAt(f, radiator, "supply") + PortAt(f, radiator, "return")) * 0.5;
        Assert.Equal(rs.X, line[^1].X, 6);
        Assert.Equal(rs.Z, line[^1].Z, 6);
        // The last plan leg ran along Y: its other corner slid in X to stay in line, at the run's level; the end jogs down to the port.
        var corner = line[^3];
        Assert.Equal(rs.X, corner.X, 6);
        Assert.Equal(line[1].Y, corner.Y, 6);
        Assert.Equal(0.2, corner.Z, 6);
    }

    [Fact]
    public void A_valve_dropped_on_a_run_splits_it_and_takes_both_halves()
    {
        var f = new MepFixture();
        var s = f.System(Services.ColdWater);
        var meter = f.Node(s, "waterMeter", 0, 0);
        var basin = f.Node(s, "basin", 6, 0);
        var run = f.Draw(s, MepFixture.Pts((0.2, 0), (6, 0)), new JsonObject { ["nodeId"] = meter }, new JsonObject { ["nodeId"] = basin });
        var valve = f.Node(s, "valve", 3, 0, new JsonObject { ["onRunId"] = run, ["rotation"] = 0 });
        var runs = Runs.Of(f.Model, s).ToList();
        Assert.Equal(2, runs.Count);
        var first = f.Model.Get(run)!;
        var second = runs.Single(r => r.Id != run);
        Assert.Equal(valve, Runs.EndOf(f.Model, first, "end")!.NodeId);
        Assert.Equal("in", Runs.EndOf(f.Model, first, "end")!.Port);
        Assert.Equal(valve, Runs.EndOf(f.Model, second, "start")!.NodeId);
        Assert.Equal("out", Runs.EndOf(f.Model, second, "start")!.Port);
        Assert.Equal(basin, Runs.EndOf(f.Model, second, "end")!.NodeId);
        Assert.Equal("cold", Runs.EndOf(f.Model, second, "end")!.Port);
        Assert.DoesNotContain(f.Open(s).OfType<JsonObject>(), o => (string?)o["id"] == valve);
        // A second run on the taken port is refused.
        Assert.Throws<CommandException>(() => f.Draw(s, MepFixture.Pts((6, 2), (6, 0.1)), null, new JsonObject { ["nodeId"] = basin, ["port"] = "cold" }));
    }

    [Fact]
    public void A_branch_leaves_a_trunk_at_a_station_where_a_tee_is_made()
    {
        var f = new MepFixture();
        var s = f.System();
        var boiler = f.Node(s, "boiler", 0, 0);
        var r1 = f.Node(s, "radiator", 6, 3);
        var r2 = f.Node(s, "radiator", 3, 3);
        var trunk = f.Draw(s, MepFixture.Pts((0, -0.5), (6, -0.5), (6, 3)), new JsonObject { ["nodeId"] = boiler }, new JsonObject { ["nodeId"] = r1 });
        var branch = f.Draw(s, MepFixture.Pts((3, -0.5), (3, 3)), new JsonObject { ["runId"] = trunk, ["at"] = new JsonArray(3, -0.5) }, new JsonObject { ["nodeId"] = r2 });
        var end = Runs.EndOf(f.Model, f.Model.Get(branch)!, "start")!;
        Assert.Equal(trunk, end.RunId);
        Assert.Equal(3.0, end.Station, 6);
        Assert.Contains(f.Session.Geometry(trunk)!.Parts, p => p.Key == "tee:" + branch);
        // The branch starts on the trunk's centreline, at the trunk's elevation.
        var line = Runs.Centreline(f.Model, f.Model.Get(branch)!, Runs.SettingOf(f.Model, f.Model.Get(branch)!), out _);
        Assert.Equal(-0.5, line[0].Y, 6);
        Assert.Equal(0.2, line[0].Z, 6);
    }

    [Fact]
    public void A_rise_takes_a_run_from_the_floor_to_the_ceiling_with_a_riser_between()
    {
        var f = new MepFixture();
        var s = f.System(Services.ColdWater);
        var meter = f.Node(s, "waterMeter", 0, 0);
        var rise = f.Node(s, "rise", 4, 0, new JsonObject { ["elevation"] = 0.2, ["toElevation"] = 2.4 });
        var low = f.Draw(s, MepFixture.Pts((0.2, 0), (4, 0)), new JsonObject { ["nodeId"] = meter }, new JsonObject { ["nodeId"] = rise, ["port"] = "bottom" });
        var high = f.Draw(s, MepFixture.Pts((4, 0), (8, 0)), new JsonObject { ["nodeId"] = rise, ["port"] = "top" }, null, new JsonObject { ["plane"] = "ceiling" });
        var riser = f.Bounds(rise, "riser");
        Assert.Equal(0.2, riser.Min.Z, 3);
        Assert.Equal(2.4, riser.Max.Z, 3);
        var lowLine = Runs.Centreline(f.Model, f.Model.Get(low)!, Runs.SettingOf(f.Model, f.Model.Get(low)!), out _);
        var highLine = Runs.Centreline(f.Model, f.Model.Get(high)!, Runs.SettingOf(f.Model, f.Model.Get(high)!), out _);
        Assert.Equal(0.2, lowLine[^1].Z, 6);
        Assert.Equal(2.4, highLine[0].Z, 6);
        Assert.Equal(2.4, highLine[^1].Z, 6);
        // The symbol: a ring with a dot for a rise.
        var plan = f.Session.Geometry(rise)!.Plan!;
        Assert.Contains(plan, i => i.Kind == "heavy" && i.Closed && i.Points.Count == 12);
        Assert.DoesNotContain(f.Open(s).OfType<JsonObject>(), o => (string?)o["id"] == rise);
    }

    [Fact]
    public void A_drainage_run_falls_at_its_slope_from_the_fixture_and_a_wrong_service_is_refused()
    {
        var f = new MepFixture();
        var s = f.System(Services.Drainage);
        var basin = f.Node(s, "basin", 0, 0);
        var stack = f.Node(s, "stack", 4, 0);
        var run = f.Draw(s, MepFixture.Pts((0, 0), (4, 0)), new JsonObject { ["nodeId"] = basin }, new JsonObject { ["nodeId"] = stack });
        var e = f.Model.Get(run)!;
        var line = Runs.Centreline(f.Model, e, Runs.SettingOf(f.Model, e), out _);
        Assert.Equal("waste", Runs.EndOf(f.Model, e, "start")!.Port);
        // Pinned at both ends: from the basin's waste (0.8 m) down to the stack's inlet (0.5 m), the fall is that drop.
        Assert.Equal(0.8, line[0].Z, 6);
        Assert.Equal(0.5, line[^1].Z, 6);
        Assert.Equal(0.11, Runs.SettingOf(f.Model, e).Size);
        // A run with a free end falls at the slope from its fixture.
        var free = f.Model.Get(f.Draw(s, MepFixture.Pts((0, 0), (0, -3)), new JsonObject { ["nodeId"] = f.Node(s, "wc", 0, 0) }))!;
        var fl = Runs.Centreline(f.Model, free, Runs.SettingOf(f.Model, free), out _);
        Assert.Equal(fl[0].Z - 0.02 * (fl[^1].Xy - fl[0].Xy).Length, fl[^1].Z, 6);
        Assert.Throws<CommandException>(() => f.Node(s, "radiator", 2, 2));
        // Open: the stack's vent and outlet remain.
        var open = f.Open(s).OfType<JsonObject>().Where(o => (string?)o["id"] == stack).Select(o => (string?)o["port"]).ToList();
        Assert.Contains("out", open);
        Assert.Contains("vent", open);
        Assert.DoesNotContain("in", open);
    }

    [Fact]
    public void Overrides_win_over_the_systems_defaults_and_a_changed_default_reaches_the_rest()
    {
        var f = new MepFixture();
        var s = f.System(Services.Heating, new JsonObject { ["size"] = 0.025 });
        var a = f.Draw(s, MepFixture.Pts((0, 0), (3, 0)));
        var b = f.Draw(s, MepFixture.Pts((0, 1), (3, 1)), more: new JsonObject { ["size"] = 0.032, ["plane"] = "ceiling", ["pair"] = false });
        Assert.Equal(0.025, Runs.SettingOf(f.Model, f.Model.Get(a)!).Size);
        Assert.Equal(0.2, Runs.SettingOf(f.Model, f.Model.Get(a)!).Elevation);
        Assert.Equal(0.032, Runs.SettingOf(f.Model, f.Model.Get(b)!).Size);
        Assert.Equal(2.4, Runs.SettingOf(f.Model, f.Model.Get(b)!).Elevation);
        Assert.False(Runs.SettingOf(f.Model, f.Model.Get(b)!).Pair);
        f.Run("mep.updateSystem", new JsonObject { ["id"] = s, ["defaults"] = new JsonObject { ["ceilingElevation"] = 2.7, ["size"] = 0.028 } });
        Assert.Equal(0.028, Runs.SettingOf(f.Model, f.Model.Get(a)!).Size);
        Assert.Equal(2.7, Runs.SettingOf(f.Model, f.Model.Get(b)!).Elevation);
        Assert.Equal(0.032, Runs.SettingOf(f.Model, f.Model.Get(b)!).Size);
        // Two open ends on each run.
        Assert.Equal(4, f.Open(s).Count);
        f.Session.Undo();
        Assert.Equal(2.4, Runs.SettingOf(f.Model, f.Model.Get(b)!).Elevation);
    }
}
