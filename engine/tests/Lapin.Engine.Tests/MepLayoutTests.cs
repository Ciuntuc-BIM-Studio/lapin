using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Mep;
using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>The layout workflow: a system placed whole, endpoints that connect themselves, handles on runs, the resolve at the end.</summary>
public class MepLayoutTests
{
    [Fact]
    public void A_heating_system_placed_whole_resolves_from_the_start_and_follows_its_radiator()
    {
        var f = new MepFixture();
        var r = (JsonObject)f.Run("mep.placeSystem", new JsonObject { ["service"] = Services.Heating, ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 })!;
        var system = (string)r["id"]!;
        var boiler = (string)r["sourceId"]!;
        var radiator = (string)r["endpointId"]!;
        var run = f.Model.Get((string)r["runId"]!)!;
        Assert.Equal(boiler, Runs.EndOf(f.Model, run, "start")!.NodeId);
        Assert.Equal(radiator, Runs.EndOf(f.Model, run, "end")!.NodeId);
        Assert.Empty(f.Open(system));
        Assert.Single(Runs.Of(f.Model, system));
        // The radiator moved to another room: the run follows it, still connected.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(radiator), ["op"] = "move", ["by"] = new JsonArray(4, 3) });
        run = f.Model.Get(run.Id)!;
        Assert.Equal(radiator, Runs.EndOf(f.Model, run, "end")!.NodeId);
        var line = Runs.Centreline(f.Model, run, Runs.SettingOf(f.Model, run), out _);
        var port = Nodes.Port(f.Model, f.Model.Get(radiator)!, "supply")!.At;
        var pair = Nodes.Port(f.Model, f.Model.Get(radiator)!, "return")!.At;
        var mid = (port + pair) * 0.5;
        Assert.Equal(mid.X, line[^1].X, 6);
        Assert.Equal(mid.Y, line[^1].Y, 6);
    }

    [Fact]
    public void A_second_radiator_placed_with_connect_branches_off_the_run()
    {
        var f = new MepFixture();
        var r = (JsonObject)f.Run("mep.placeSystem", new JsonObject { ["service"] = Services.Heating, ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 })!;
        var system = (string)r["id"]!;
        var trunk = (string)r["runId"]!;
        var second = (JsonObject)f.Run("mep.placeNode", new JsonObject { ["systemId"] = system, ["role"] = "radiator", ["storeyId"] = f.Storey, ["x"] = 6, ["y"] = -3, ["connect"] = true })!;
        var branch = f.Model.Get((string)second["runId"]!)!;
        // The boiler's only pair is taken: the new run starts on the first one (a tee) and ends on the new radiator.
        Assert.Equal(trunk, Runs.EndOf(f.Model, branch, "start")!.RunId);
        Assert.Equal((string)second["id"]!, Runs.EndOf(f.Model, branch, "end")!.NodeId);
        // The fittings the system makes: one tee per pipe of the pair where the branch leaves, the corners of both runs.
        var fittings = (JsonObject)f.Session.Query("mep.fittings", new JsonObject { ["systemId"] = system })!;
        Assert.Equal(2, (int)fittings["tees"]!);
        Assert.True((int)fittings["bends"]! >= 2);
        Assert.Empty(f.Open(system));
    }

    [Fact]
    public void A_drainage_system_runs_from_its_wc_to_the_stack_and_a_basin_joins_it()
    {
        var f = new MepFixture();
        var r = (JsonObject)f.Run("mep.placeSystem", new JsonObject { ["service"] = Services.Drainage, ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 })!;
        var system = (string)r["id"]!;
        var run = f.Model.Get((string)r["runId"]!)!;
        Assert.Equal((string)r["endpointId"]!, Runs.EndOf(f.Model, run, "start")!.NodeId);
        Assert.Equal("waste", Runs.EndOf(f.Model, run, "start")!.Port);
        Assert.Equal((string)r["sourceId"]!, Runs.EndOf(f.Model, run, "end")!.NodeId);
        var basin = (JsonObject)f.Run("mep.placeNode", new JsonObject { ["systemId"] = system, ["role"] = "basin", ["storeyId"] = f.Storey, ["x"] = 1, ["y"] = 4, ["connect"] = true })!;
        var branch = f.Model.Get((string)basin["runId"]!)!;
        Assert.Equal((string)basin["id"]!, Runs.EndOf(f.Model, branch, "start")!.NodeId);
        Assert.Equal(run.Id, Runs.EndOf(f.Model, branch, "end")!.RunId);
    }

    [Theory]
    [InlineData(Services.ColdWater)]
    [InlineData(Services.HotWater)]
    [InlineData(Services.Electrical)]
    [InlineData(Services.SupplyAir)]
    public void Every_template_places_a_system_with_nothing_open_on_its_endpoint(string service)
    {
        var f = new MepFixture();
        var r = (JsonObject)f.Run("mep.placeSystem", new JsonObject { ["service"] = service, ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 })!;
        Assert.NotNull(r["runId"]);
        var open = f.Open((string)r["id"]!).OfType<JsonObject>().ToList();
        Assert.DoesNotContain(open, o => (string?)o["id"] == (string)r["runId"]!);
        Assert.DoesNotContain(open, o => (string?)o["id"] == (string)r["endpointId"]!);
    }

    [Fact]
    public void A_handle_is_added_where_the_point_falls_on_the_run()
    {
        var f = new MepFixture();
        var s = f.System();
        var run = f.Draw(s, MepFixture.Pts((0, 0), (4, 0), (4, 3)));
        f.Run("mep.addHandle", new JsonObject { ["runId"] = run, ["x"] = 2, ["y"] = 0.1 });
        var pts = Runs.Points(f.Model.Get(run)!);
        Assert.Equal(4, pts.Count);
        Assert.Equal(new Vec2(2, 0), pts[1]);
    }
}
