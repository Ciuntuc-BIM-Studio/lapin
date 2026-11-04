using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Gas and fuel oil installations (the tester's request #72, decision 285): a residence's gas installation, fuel oil, the street's main and service connection, IFC, the bill of materials.</summary>
public class MepGasTests
{
    static JsonObject Place(MepFixture f, string service, double x, double y) =>
        (JsonObject)f.Run("mep.placeSystem", new JsonObject { ["service"] = service, ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y })!;

    static List<Vec3> Line(MepFixture f, string run) { var e = f.Model.Get(run)!; return Runs.Centreline(f.Model, e, Runs.SettingOf(f.Model, e), out _); }

    [Fact]
    public void Gas_and_fuel_oil_are_services_with_their_defaults_materials_and_ifc_systems()
    {
        Assert.Contains(Services.Gas, Services.All);
        Assert.Contains(Services.FuelOil, Services.All);
        var gas = SystemDefaults.For(Services.Gas);
        Assert.Equal("steel", gas.Material);
        Assert.Equal("natural", gas.Gas);
        Assert.Equal("low", gas.Pressure);
        Assert.Equal("wall", gas.Plane);
        Assert.Equal("copper", SystemDefaults.For(Services.FuelOil).Material);
        Assert.Null(SystemDefaults.For(Services.FuelOil).Pressure);
        Assert.Equal("GAS", MepNetwork.SystemTypeOf(Services.Gas));
        Assert.Equal("FUEL", MepNetwork.SystemTypeOf(Services.FuelOil));
        // The pipe materials: steel, copper, multilayer and PE for gas; copper and steel for fuel oil.
        Assert.Equal(["steel", "copper", "pex-al-pex", "pe"], RunMaterials.For(Services.Gas).Select(m => m.Id).ToArray());
        Assert.Equal(["steel", "copper"], RunMaterials.For(Services.FuelOil).Select(m => m.Id).ToArray());

        // A system's gas and pressure class are data, changed like any default; a value that is neither is refused quietly.
        var f = new MepFixture();
        var s = f.System(Services.Gas);
        f.Run("mep.updateSystem", new JsonObject { ["id"] = s, ["defaults"] = new JsonObject { ["gas"] = "lpg", ["pressure"] = "medium" } });
        Assert.Equal("lpg", Systems.Of(f.Model.Get(s)!).Gas);
        Assert.Equal("medium", Systems.Of(f.Model.Get(s)!).Pressure);
        f.Run("mep.updateSystem", new JsonObject { ["id"] = s, ["defaults"] = new JsonObject { ["pressure"] = "very high" } });
        Assert.Equal("medium", Systems.Of(f.Model.Get(s)!).Pressure);
        // A heating system has no gas to name.
        Assert.Null(Systems.Of(f.Model.Get(f.System())!).Gas);
    }

    [Fact]
    public void The_gas_parts_have_their_classes_and_ports_and_an_appliance_is_an_endpoint_only_for_gas()
    {
        foreach (var (id, cls, pre) in new[]
        {
            ("meterBox", "IfcDistributionChamberElement", "METERCHAMBER"), ("regulator", "IfcValve", "PRESSUREREDUCING"), ("gasMeter", "IfcFlowMeter", "GASMETER"),
            ("gasShutoff", "IfcValve", "GASCOCK"), ("gasTap", "IfcValve", "GASTAP"), ("flexConnector", "IfcPipeSegment", "FLEXIBLESEGMENT"),
            ("gasBoiler", "IfcBoiler", "WATER"), ("cooker", "IfcBurner", "USERDEFINED"), ("waterHeater", "IfcBoiler", "WATER"), ("tappingTee", "IfcPipeFitting", "JUNCTION"),
            ("oilTank", "IfcTank", "STORAGE"), ("oilFilter", "IfcFilter", "OILFILTER"), ("fillPoint", "IfcPipeFitting", "ENTRY"), ("ventCap", "IfcPipeFitting", "EXIT"), ("oilBoiler", "IfcBoiler", "WATER"),
        })
        {
            var role = Roles.Require(id);
            Assert.Equal(cls, role.IfcClass);
            Assert.Equal(pre, role.Predefined);
            Assert.True(role.Ports.Any(p => p.Service is Services.Gas or Services.FuelOil or "*"), $"{id} has a gas or fuel oil port");
        }
        // The inline parts go in and out; the tapping tee's outlet stands up from the main.
        foreach (var id in new[] { "regulator", "gasMeter", "gasShutoff", "gasTap", "oilFilter" })
            Assert.Equal([Flow.In, Flow.Out], Roles.Require(id).Ports.Select(p => p.Flow).ToArray());
        Assert.Equal(1.0, Roles.Require("tappingTee").Ports.Single(p => p.Key == "branch").Dir.Z);
        // A gas boiler is the heating's source and the gas installation's endpoint; a cooker an endpoint anywhere.
        var boiler = Roles.Require("gasBoiler");
        Assert.Equal("source", boiler.KindIn(Services.Heating));
        Assert.Equal("endpoint", boiler.KindIn(Services.Gas));
        Assert.Equal("endpoint", Roles.Require("oilBoiler").KindIn(Services.FuelOil));
        Assert.True(boiler.Consumption > 0 && Roles.Require("cooker").Consumption > 0);
        Assert.Equal("C", boiler.Flue);

        // Placed, a node keeps its own consumption and flue type, and the role's object type.
        var f = new MepFixture();
        var s = f.System(Services.Gas);
        var cooker = f.Node(s, "cooker", 2, 2, new JsonObject { ["consumption"] = 1.1, ["flue"] = "A" });
        var e = f.Model.Get(cooker)!;
        Assert.Equal("IfcBurner", e.Class);
        Assert.Equal("Gas cooker", e.Str("objectType"));
        Assert.Equal(1.1, Nodes.Consumption(e, Roles.Require("cooker")));
        // A buried tank lies with its top 0.6 m under the ground.
        var o = f.System(Services.FuelOil);
        var tank = f.Model.Get(f.Node(o, "oilTank", 0, 0, new JsonObject { ["placement"] = "buried" }))!;
        Assert.Equal(-2.1, Nodes.Elevation(tank, Roles.Require("oilTank")), 6);
        Assert.True(f.Bounds(tank.Id).Max.Z < -0.5);
    }

    [Fact]
    public void A_house_installation_runs_from_the_meter_box_through_regulator_and_meter_to_two_appliances_sleeved_through_a_wall()
    {
        var f = new MepFixture();
        // A wall between the meter box (outside, at y < 0) and the kitchen.
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -2, ["y1"] = 1, ["x2"] = 8, ["y2"] = 1, ["join"] = false })!["id"]!;
        var r = Place(f, Services.Gas, 0, 0);
        var system = (string)r["id"]!;
        var box = (string)r["sourceId"]!;
        var boiler = (string)r["endpointId"]!;
        Assert.Equal("meterBox", f.Model.Get(box)!.Str("role"));
        Assert.Equal("gasBoiler", f.Model.Get(boiler)!.Str("role"));
        var run = (string)r["runId"]!;
        Assert.Equal(box, Runs.EndOf(f.Model, f.Model.Get(run)!, "start")!.NodeId);
        Assert.Equal("gas", Runs.EndOf(f.Model, f.Model.Get(run)!, "end")!.Port);
        // The gas runs on the wall at 2.1 m, in steel.
        var setting = Runs.SettingOf(f.Model, f.Model.Get(run)!);
        Assert.Equal(2.1, setting.Elevation, 6);
        Assert.Equal("steel", setting.Material);

        // The regulator, the meter and the main shut-off dropped on the run, in that order from the box: the run is cut at each.
        var pts = Runs.Points(f.Model.Get(run)!);
        var leg = pts[1] - pts[0];
        Vec2 At(double k) => pts[0] + leg * k;
        var regulator = f.Node(system, "regulator", At(0.2).X, At(0.2).Y, new JsonObject { ["onRunId"] = run, ["rotation"] = Math.Atan2(leg.Y, leg.X) * 180 / Math.PI });
        var tail = Runs.Of(f.Model, system).Single(x => Runs.EndOf(f.Model, x, "start")?.NodeId == regulator);
        var meter = f.Node(system, "gasMeter", At(0.5).X, At(0.5).Y, new JsonObject { ["onRunId"] = tail.Id, ["rotation"] = Math.Atan2(leg.Y, leg.X) * 180 / Math.PI });
        Assert.Equal(3, Runs.Of(f.Model, system).Count());
        Assert.Equal("in", Runs.EndOf(f.Model, f.Model.Get(run)!, "end")!.Port);
        Assert.Equal(regulator, Runs.EndOf(f.Model, f.Model.Get(run)!, "end")!.NodeId);

        // A cooker in the kitchen joins by a run of its own: a tee on the run to the boiler (every source port is taken).
        var cooker = (JsonObject)f.Run("mep.placeNode", new JsonObject { ["systemId"] = system, ["role"] = "cooker", ["storeyId"] = f.Storey, ["x"] = 5, ["y"] = 4, ["connect"] = true })!;
        var branch = f.Model.Get((string)cooker["runId"]!)!;
        Assert.NotNull(Runs.EndOf(f.Model, branch, "start")!.RunId);
        Assert.Equal((string)cooker["id"]!, Runs.EndOf(f.Model, branch, "end")!.NodeId);
        // Only the box's inlet is open: the service connection from the street comes to it.
        var open = Assert.Single(f.Open(system))!;
        Assert.Equal(box, (string?)open["id"]);
        Assert.Equal("in", (string?)open["port"]);

        // Every run crossing the wall goes through it in a sleeve, the wall's thickness long and 10 mm proud of each face.
        var thickness = Walls.Frame(f.Model.Get(wall)!, f.Model)!.Thickness;
        var crossing = Runs.Of(f.Model, system).Where(x => f.Session.Geometry(x.Id)!.Parts.Any(p => p.Key == "sleeve:" + wall)).ToList();
        Assert.NotEmpty(crossing);
        foreach (var x in crossing)
        {
            var sleeve = Sleeves.Of(f.Model, x, Runs.SettingOf(f.Model, x), Line(f, x.Id)).Single();
            Assert.Equal(wall, sleeve.WallId);
            Assert.Equal(Runs.SettingOf(f.Model, x).Size + 0.02, sleeve.Diameter, 6);
            Assert.True(sleeve.Length >= thickness + 0.02 - 1e-6, $"sleeve {sleeve.Length} over a {thickness} wall");
        }
        // A heating run through the same wall takes none.
        var heat = f.System();
        var hr = f.Draw(heat, MepFixture.Pts((3, -1), (3, 3)));
        Assert.DoesNotContain(f.Session.Geometry(hr)!.Parts, p => p.Key?.StartsWith("sleeve:", StringComparison.Ordinal) == true);
        _ = meter;
    }

    [Fact]
    public void A_heating_systems_gas_boiler_joins_the_gas_installation()
    {
        var f = new MepFixture();
        var heating = Place(f, Services.Heating, 0, 0);
        var gas = f.System(Services.Gas);
        var box = f.Node(gas, "meterBox", -3, 0);
        // A gas boiler placed in the heating system as its source, then joined to the gas system.
        var boiler = f.Node((string)heating["id"]!, "gasBoiler", 0, 4);
        var joined = (string)f.Run("mep.joinSystem", new JsonObject { ["systemId"] = gas, ["nodeId"] = boiler })!["id"]!;
        var run = f.Model.Get(joined)!;
        Assert.Equal(box, Runs.EndOf(f.Model, run, "start")!.NodeId);
        Assert.Equal("gas", Runs.EndOf(f.Model, run, "end")!.Port);
        Assert.Equal(gas, run.Str("systemId"));
        // The list of the gas system names the boiler among its appliances.
        var grid = new GasSchedule().Grid(f.Session.Reader);
        Assert.Contains(grid.Rows, row => row[1] == "Appliances" && row[2] == "Gas boiler (type C)" && row[5] == "1");
    }

    [Fact]
    public void A_fuel_oil_installation_joins_its_fill_point_vent_cap_and_boiler_to_the_right_ports_of_the_tank()
    {
        var f = new MepFixture();
        var r = Place(f, Services.FuelOil, 0, 0);
        var system = (string)r["id"]!;
        var tank = (string)r["sourceId"]!;
        var supply = f.Model.Get((string)r["runId"]!)!;
        Assert.Equal("supply", Runs.EndOf(f.Model, supply, "start")!.Port);
        Assert.Equal("oil", Runs.EndOf(f.Model, supply, "end")!.Port);
        JsonObject Add(string role, double x, double y) => (JsonObject)f.Run("mep.placeNode", new JsonObject { ["systemId"] = system, ["role"] = role, ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y, ["connect"] = true })!;
        // The fill pipe runs from the fill point outside into the tank's fill; the vent from the tank's vent to the cap.
        var fill = f.Model.Get((string)Add("fillPoint", -4, -3)["runId"]!)!;
        Assert.Equal("out", Runs.EndOf(f.Model, fill, "start")!.Port);
        Assert.Equal(tank, Runs.EndOf(f.Model, fill, "end")!.NodeId);
        Assert.Equal("fill", Runs.EndOf(f.Model, fill, "end")!.Port);
        var vent = f.Model.Get((string)Add("ventCap", -4, 3)["runId"]!)!;
        Assert.Equal("vent", Runs.EndOf(f.Model, vent, "start")!.Port);
        Assert.Equal("in", Runs.EndOf(f.Model, vent, "end")!.Port);
        // The filter dropped on the supply.
        var pts = Runs.Points(supply);
        var mid = pts[0] + (pts[1] - pts[0]) * 0.5;
        f.Node(system, "oilFilter", mid.X, mid.Y, new JsonObject { ["onRunId"] = supply.Id });
        Assert.Equal("in", Runs.EndOf(f.Model, f.Model.Get(supply.Id)!, "end")!.Port);
        // Only the return is left open: the boiler's and the tank's, a run the user draws when the burner takes two pipes.
        var open = f.Open(system).Select(o => (string?)o?["port"] ?? "").Order().ToArray();
        Assert.Equal(["oilReturn", "return"], open);
    }

    [Fact]
    public void A_street_main_lies_along_its_line_at_an_offset_and_depth_and_a_service_connection_rises_into_the_meter_box()
    {
        var f = new MepFixture();
        // The road's line: a wall stands for any element a main can follow (a road, an alignment, a drafted line).
        var street = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = -10, ["x2"] = 30, ["y2"] = -10, ["join"] = false })!["id"]!;
        var main = (JsonObject)f.Run("mep.mainAlong", new JsonObject { ["alongId"] = street, ["offset"] = 2, ["depth"] = 1.0, ["from"] = 5, ["to"] = 25 })!;
        var mainId = (string)main["id"]!;
        var system = f.Model.Get((string)main["systemId"]!)!;
        Assert.Equal(Services.Gas, Systems.Service(system));
        Assert.Equal("medium", Systems.Of(system).Pressure);
        var e = f.Model.Get(mainId)!;
        Assert.Equal("pe", Runs.SettingOf(f.Model, e).Material);
        Assert.Equal(street, e.Str("along"));
        Assert.True(e.Attrs["warningTape"]!.GetValue<bool>());
        // 2 m to the left of the line, from station 5 to 25, its axis 1 m plus half its size under the storey's level (no terrain).
        var line = Line(f, mainId);
        Assert.Equal(5, line[0].X, 6); Assert.Equal(25, line[^1].X, 6);
        Assert.All(line, p => Assert.Equal(-8, p.Y, 6));
        Assert.All(line, p => Assert.Equal(-(1.0 + GasSupply.MainSize / 2), p.Z, 6));

        // The meter box on the house's facade; the service connection from the main to it.
        var box = f.Node(system.Id, "meterBox", 12, 0);
        var conn = (JsonObject)f.Run("mep.serviceConnection", new JsonObject { ["mainId"] = mainId, ["toNodeId"] = box })!;
        var tee = f.Model.Get((string)conn["teeId"]!)!;
        var pipe = f.Model.Get((string)conn["id"]!)!;
        // The main is cut by the tapping tee, nearest the box, at the main's depth.
        Assert.Equal("tappingTee", tee.Str("role"));
        var teeAt = Nodes.Position(tee);
        Assert.Equal(-8, teeAt.Y, 6);
        var boxIn = Nodes.Port(f.Model, f.Model.Get(box)!, "in")!.At;
        Assert.Equal(boxIn.X, teeAt.X, 6);
        Assert.Equal(-(1.0 + GasSupply.MainSize / 2), Nodes.Port(f.Model, tee, "in")!.At.Z, 6);
        var halves = Runs.Of(f.Model, system.Id).Where(x => x.Id != pipe.Id).ToList();
        Assert.Equal(2, halves.Count);
        Assert.Contains(halves, h => Runs.EndOf(f.Model, h, "end")?.Port == "in" && Runs.EndOf(f.Model, h, "end")?.NodeId == tee.Id);
        Assert.Contains(halves, h => Runs.EndOf(f.Model, h, "start")?.Port == "out" && Runs.EndOf(f.Model, h, "start")?.NodeId == tee.Id);
        // The service pipe: from the tee's outlet, at 0.8 m of cover, to the box's inlet, rising into it there.
        Assert.Equal("branch", Runs.EndOf(f.Model, pipe, "start")!.Port);
        Assert.Equal(box, Runs.EndOf(f.Model, pipe, "end")!.NodeId);
        Assert.Equal("Service connection", pipe.Str("objectType"));
        var sp = Line(f, pipe.Id);
        // (The tee's outlet stands 0.12 m over the main's axis: the pipe starts there and comes to its own depth.)
        Assert.Equal(-(1.0 + GasSupply.MainSize / 2) + 0.12, sp[0].Z, 6);
        Assert.Equal(-(GasSupply.ServiceDepth + GasSupply.ServiceSize / 2), sp[^2].Z, 6);
        Assert.Equal(boxIn.Z, sp[^1].Z, 6);
        Assert.Equal(boxIn.X, sp[^2].X, 6); Assert.Equal(boxIn.Y, sp[^2].Y, 6);
        var (_, riser, plan) = GasSchedule.Lengths(f.Model, pipe);
        // Its vertical lengths: the riser into the box, and the tee's outlet down (or up) to the pipe's depth.
        Assert.Equal(boxIn.Z - sp[^2].Z + Math.Abs(sp[1].Z - sp[0].Z), riser, 4);
        Assert.Equal(8, plan, 6);
    }

    [Fact]
    public void A_terrain_puts_the_depth_under_the_ground_not_under_the_storey()
    {
        // The finished surface 0.5 m up at x = 0 and 1.5 m at x = 10: the main follows it 0.9 m down.
        var levels = GasSupply.LevelsAtDepth([new Vec2(0, 0), new Vec2(10, 0)], p => 0.5 + p.X / 10, 0, 0.9, 0.06);
        Assert.Equal(0.5 - 0.9 - 0.03, (double)levels[0]!, 6);
        Assert.Equal(1.5 - 0.9 - 0.03, (double)levels[1]!, 6);
        // Cut between two stations.
        var part = GasSupply.Between([new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10)], 5, 15);
        Assert.Equal([new Vec2(5, 0), new Vec2(10, 0), new Vec2(10, 5)], part);
    }

    [Fact]
    public void The_network_goes_to_ifc_as_gas_and_fuel_systems_and_comes_back_with_its_classes()
    {
        var f = new MepFixture();
        var r = Place(f, Services.Gas, 0, 0);
        var gas = (string)r["id"]!;
        var run = (string)r["runId"]!;
        var pts = Runs.Points(f.Model.Get(run)!);
        var mid = pts[0] + (pts[1] - pts[0]) * 0.5;
        var meter = f.Node(gas, "gasMeter", mid.X, mid.Y, new JsonObject { ["onRunId"] = run });
        var cooker = (string)((JsonObject)f.Run("mep.placeNode", new JsonObject { ["systemId"] = gas, ["role"] = "cooker", ["storeyId"] = f.Storey, ["x"] = 5, ["y"] = 5, ["connect"] = true })!)["id"]!;
        var oil = Place(f, Services.FuelOil, 20, 0);
        var path = Path.Combine(Path.GetTempPath(), $"gas-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            var systems = step.OfType("IFCDISTRIBUTIONSYSTEM").Select(s => ((StepEnum)s[6]!).Value).Order().ToArray();
            Assert.Equal(["FUEL", "GAS"], systems);
            Assert.Equal("GASMETER", ((StepEnum)Assert.Single(step.OfType("IFCFLOWMETER"))[8]!).Value);
            Assert.Equal("METERCHAMBER", ((StepEnum)Assert.Single(step.OfType("IFCDISTRIBUTIONCHAMBERELEMENT"))[8]!).Value);
            var burner = Assert.Single(step.OfType("IFCBURNER"));
            Assert.Equal("USERDEFINED", ((StepEnum)burner[8]!).Value);
            Assert.Equal("Gas cooker", StepFile.Decode((string)burner[4]!));
            Assert.Equal("STORAGE", ((StepEnum)Assert.Single(step.OfType("IFCTANK"))[8]!).Value);
            Assert.Empty(step.OfType("IFCBUILDINGELEMENTPROXY"));
            // The gas ports say so: a GAS system type on every port of the gas network.
            var gasPorts = step.OfType("IFCDISTRIBUTIONPORT").Where(p => ((StepEnum)p[9]!).Value == "GAS").ToList();
            Assert.True(gasPorts.Count >= 8, $"{gasPorts.Count} gas ports");
            Assert.Contains(step.OfType("IFCDISTRIBUTIONPORT"), p => ((StepEnum)p[9]!).Value == "FUEL");

            // Read back into another project: the same elements, their classes and predefined types, where they were.
            var g = new MepFixture();
            IfcImport.Run(g.Session, path, null, out _);
            foreach (var id in new[] { meter, cooker, (string)r["sourceId"]!, (string)oil["sourceId"]! })
            {
                var before = f.Model.Get(id)!;
                var after = g.Model.Get(id) ?? throw new Xunit.Sdk.XunitException($"{before.Name} did not come back");
                Assert.Equal(before.Class, after.Class);
                Assert.Equal(f.Bounds(id).Min.Z, g.Bounds(id).Min.Z, 3);
            }
            Assert.Equal("GASMETER", g.Model.Get(meter)!.PredefinedType);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void The_bill_of_materials_counts_pipes_by_material_and_size_the_fittings_the_tape_and_the_appliances_consumption()
    {
        var f = new MepFixture();
        var r = Place(f, Services.Gas, 0, 0);
        var gas = (string)r["id"]!;
        f.Run("mep.placeNode", new JsonObject { ["systemId"] = gas, ["role"] = "cooker", ["storeyId"] = f.Storey, ["x"] = 6, ["y"] = 5, ["connect"] = true });
        var street = f.Draw(gas, MepFixture.Pts((-5, -6), (20, -6)), more: new JsonObject { ["material"] = "pe", ["size"] = 0.063, ["warningTape"] = true });
        var grid = new GasSchedule().Grid(f.Session.Reader);
        Assert.Equal(["System", "Group", "Item", "Material", "Size (mm)", "Quantity", "Unit"], grid.Columns);
        string Q(string item, string material = "", string? size = null) =>
            grid.Rows.Single(row => row[2] == item && (material == "" || row[3] == material) && (size == null || row[4] == size))[5];
        // The steel inside, the PE outside, each with its length.
        var steel = Runs.Of(f.Model, gas).Where(x => x.Id != street).Sum(x => GasSchedule.Lengths(f.Model, x).Length);
        Assert.Equal(Math.Round(steel, 2), double.Parse(Q("Pipe", "Black steel", "22"), System.Globalization.CultureInfo.InvariantCulture), 2);
        Assert.Equal("25", Q("Pipe", "PE 100 (buried)", "63"));
        Assert.Equal("25", Q("Warning tape"));
        Assert.Equal("1", Q("Tee", "Black steel"));
        Assert.Equal("1", Q("Meter box"));
        Assert.Equal("1", Q("Gas boiler (type C)"));
        Assert.Equal("1", Q("Gas cooker (type A)"));
        // The consumption summed for mep-pro's sizing: 2.8 + 0.8 m³/h, 24 + 7.5 kW.
        Assert.Equal("3.6", Q("Installed consumption"));
        Assert.Equal("31.5", Q("Installed heat output"));
        Assert.Contains(grid.Rows, row => row[0].Contains("natural gas, low pressure", StringComparison.Ordinal));
    }
}
