using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Mep;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Fire protection (a tester's request, report #89): the detection and alarm loop (a panel, detectors, call points,
/// sounders joined one after another and closed back to the panel) and the hydrants' water (a pump set, hydrants with
/// their reach), their coverage on the plan, IFC and the bill of materials.
/// </summary>
public class MepFireTests
{
    static JsonObject Place(MepFixture f, string service, double x, double y) =>
        (JsonObject)f.Run("mep.placeSystem", new JsonObject { ["service"] = service, ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y })!;

    static string Device(MepFixture f, string system, string role, double x, double y) =>
        (string)((JsonObject)f.Run("mep.placeNode", new JsonObject { ["systemId"] = system, ["role"] = role, ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y, ["connect"] = true })!)["id"]!;

    /// <summary>The run that ends on a node's port, and what its start is on.</summary>
    static (string? From, string? Port) CameFrom(MepFixture f, string node, string port = "in")
    {
        foreach (var run in Runs.All(f.Model))
            if (Runs.EndOf(f.Model, run, "end") is { NodeId: { } n, Port: { } p } && n == node && p == port)
                return (Runs.EndOf(f.Model, run, "start")?.NodeId, Runs.EndOf(f.Model, run, "start")?.Port);
        return (null, null);
    }

    [Fact]
    public void Fire_detection_and_hydrants_are_services_with_their_defaults_materials_and_ifc_systems()
    {
        Assert.Contains(Services.FireAlarm, Services.All);
        Assert.Contains(Services.FireWater, Services.All);
        Assert.Equal("IfcCableSegment", Services.RunClass(Services.FireAlarm));
        Assert.Equal("IfcPipeSegment", Services.RunClass(Services.FireWater));
        Assert.Equal("fire-cable", SystemDefaults.For(Services.FireAlarm).Material);
        Assert.Equal("ceiling", SystemDefaults.For(Services.FireAlarm).Plane);
        Assert.Equal("galvanised-pipe", SystemDefaults.For(Services.FireWater).Material);
        Assert.Equal(0.05, SystemDefaults.For(Services.FireWater).Size);
        Assert.Equal("FIREPROTECTION", MepNetwork.SystemTypeOf(Services.FireAlarm));
        Assert.Equal("FIREPROTECTION", MepNetwork.SystemTypeOf(Services.FireWater));
        Assert.Equal("CABLE", MepNetwork.KindOf(Services.FireAlarm));
        Assert.Equal(["steel", "galvanised-pipe"], RunMaterials.For(Services.FireWater).Select(m => m.Id).ToArray());

        foreach (var (id, cls, pre) in new[]
        {
            ("firePanel", "IfcUnitaryControlElement", "ALARMPANEL"), ("smokeDetector", "IfcSensor", "SMOKESENSOR"), ("heatDetector", "IfcSensor", "HEATSENSOR"),
            ("callPoint", "IfcAlarm", "BREAKGLASSBUTTON"), ("sounder", "IfcAlarm", "SIREN"), ("sounderBeacon", "IfcAlarm", "LIGHT"),
            ("fireTank", "IfcTank", "STORAGE"), ("firePump", "IfcPump", "ENDSUCTION"), ("hydrant", "IfcFireSuppressionTerminal", "HOSEREEL"),
            ("externalHydrant", "IfcFireSuppressionTerminal", "FIREHYDRANT"), ("breechingInlet", "IfcFireSuppressionTerminal", "BREECHINGINLET"), ("sprinkler", "IfcFireSuppressionTerminal", "SPRINKLER"),
        })
        {
            var role = Roles.Require(id);
            Assert.Equal(cls, role.IfcClass);
            Assert.Equal(pre, role.Predefined);
        }
        // The devices of a loop chain; the detectors carry the planning table's radius, a hydrant its hose and jet.
        Assert.All(new[] { "smokeDetector", "heatDetector", "callPoint", "sounder", "sounderBeacon" }, id => Assert.True(Roles.Require(id).Chain));
        Assert.Equal(5.8, Roles.Require("smokeDetector").Coverage);
        Assert.Equal(3.6, Roles.Require("heatDetector").Coverage);
        Assert.Equal(26, Roles.Require("hydrant").HoseLength + Roles.Require("hydrant").JetLength);
    }

    [Fact]
    public void Devices_placed_one_after_another_make_a_loop_from_the_panel_that_closes_back_to_it()
    {
        var f = new MepFixture();
        var r = Place(f, Services.FireAlarm, 0, 0);
        var system = (string)r["id"]!;
        var panel = (string)r["sourceId"]!;
        var first = (string)r["endpointId"]!;
        // The first detector hangs on the panel's loop port.
        Assert.Equal((panel, "loop"), CameFrom(f, first));
        // Each next device goes on from the one before: the loop's open end, not the panel.
        var second = Device(f, system, "smokeDetector", 8, 2);
        Assert.Equal((first, "out"), CameFrom(f, second));
        var call = Device(f, system, "callPoint", 8, -3);
        Assert.Equal((second, "out"), CameFrom(f, call));
        var sounder = Device(f, system, "sounder", 2, -3);
        Assert.Equal((call, "out"), CameFrom(f, sounder));
        Assert.All(Runs.Of(f.Model, system), run => Assert.Equal("IfcCableSegment", f.Model.Get(run.Id)!.Class));

        // The bill says the loop is open until it is closed back to the panel's return.
        var grid = new FireSchedule().Grid(f.Session.Reader);
        Assert.Contains(grid.Rows, row => row[1] == "Loops" && row[5] == "open");
        f.Run("mep.closeLoop", new JsonObject { ["systemId"] = system });
        Assert.Equal((sounder, "out"), CameFrom(f, panel, "return"));
        grid = new FireSchedule().Grid(f.Session.Reader);
        Assert.Contains(grid.Rows, row => row[1] == "Loops" && row[5] == "closed");
        Assert.Contains(grid.Rows, row => row[1] == "Devices" && row[2] == "Smoke detector" && row[5] == "2");
        Assert.Contains(grid.Rows, row => row[1] == "Devices" && row[2] == "Manual call point" && row[5] == "1");
        var cable = Assert.Single(grid.Rows, row => row[2] == "Loop cable");
        Assert.Equal("Fire-resistant cable", cable[3]);
        Assert.True(double.Parse(cable[5], System.Globalization.CultureInfo.InvariantCulture) > 20);
        // Closed, there is nothing left to close.
        Assert.ThrowsAny<Exception>(() => f.Run("mep.closeLoop", new JsonObject { ["systemId"] = system }));
    }

    [Fact]
    public void A_detector_and_a_hydrant_draw_their_coverage_and_a_node_may_set_its_own()
    {
        var f = new MepFixture();
        var alarm = Place(f, Services.FireAlarm, 0, 0);
        var detector = (string)alarm["endpointId"]!;
        static double Radius(MepFixture f, string id)
        {
            var at = Nodes.Position(f.Model.Get(id)!);
            var circle = f.Session.Geometry(id)!.Plan!.Where(i => i.Kind == "reach" && i.Closed).OrderByDescending(i => i.Points.Count).First();
            return circle.Points.Average(p => (p - at).Length);
        }
        Assert.Equal(5.8, Radius(f, detector), 2);
        f.Run("element.update", new JsonObject { ["id"] = detector, ["attrs"] = new JsonObject { ["coverage"] = 4.4 } });
        Assert.Equal(4.4, Radius(f, detector), 2);

        var water = Place(f, Services.FireWater, 0, 20);
        var hydrant = (string)water["endpointId"]!;
        Assert.Equal(26, Radius(f, hydrant), 1);
        f.Run("element.update", new JsonObject { ["id"] = hydrant, ["attrs"] = new JsonObject { ["hoseLength"] = 30 } });
        Assert.Equal(36, Radius(f, hydrant), 1);
        // A call point covers nothing drawn.
        var call = Device(f, (string)alarm["id"]!, "callPoint", 3, 3);
        Assert.DoesNotContain(f.Session.Geometry(call)!.Plan!, i => i.Kind == "reach");
    }

    [Fact]
    public void Hydrants_join_the_pump_set_and_the_reserve_feeds_the_pump_by_its_own_port()
    {
        var f = new MepFixture();
        var r = Place(f, Services.FireWater, 0, 0);
        var system = (string)r["id"]!;
        var pump = (string)r["sourceId"]!;
        Assert.Equal((pump, "out"), CameFrom(f, (string)r["endpointId"]!));
        // A second hydrant tees onto the network (the pump's outlet is taken), never onto the reserve or the fire brigade inlet.
        f.Node(system, "fireTank", -6, 0);
        f.Node(system, "breechingInlet", 10, -4);
        var second = Device(f, system, "hydrant", 12, 6);
        var run = Runs.All(f.Model).Single(x => Runs.EndOf(f.Model, x, "end") is { NodeId: var n } && n == second);
        Assert.NotNull(Runs.EndOf(f.Model, run, "start")!.RunId);
        Assert.All(Runs.Of(f.Model, system), x => Assert.Equal("galvanised-pipe", Runs.SettingOf(f.Model, x).Material));

        var grid = new FireSchedule().Grid(f.Session.Reader);
        Assert.Contains(grid.Rows, row => row[1] == "Terminals" && row[2] == "Internal hydrant" && row[5] == "2");
        Assert.Contains(grid.Rows, row => row[1] == "Equipment" && row[2] == "Fire pump set");
        Assert.Contains(grid.Rows, row => row[1] == "Pipes" && row[3] == "Galvanised steel pipe" && row[4] == "50");
        Assert.Contains(grid.Rows, row => row[2] == "Tee");
    }

    [Fact]
    public void The_fire_network_goes_to_ifc_with_its_classes_and_a_fire_protection_system()
    {
        var f = new MepFixture();
        var alarm = Place(f, Services.FireAlarm, 0, 0);
        Device(f, (string)alarm["id"]!, "callPoint", 4, 3);
        var water = Place(f, Services.FireWater, 0, 15);
        var path = Path.Combine(Path.GetTempPath(), $"fire-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            Assert.Equal(["FIREPROTECTION", "FIREPROTECTION"], step.OfType("IFCDISTRIBUTIONSYSTEM").Select(s => ((StepEnum)s[6]!).Value).ToArray());
            Assert.Equal("ALARMPANEL", ((StepEnum)Assert.Single(step.OfType("IFCUNITARYCONTROLELEMENT"))[8]!).Value);
            Assert.Equal("SMOKESENSOR", ((StepEnum)Assert.Single(step.OfType("IFCSENSOR"))[8]!).Value);
            Assert.Equal("BREAKGLASSBUTTON", ((StepEnum)Assert.Single(step.OfType("IFCALARM"))[8]!).Value);
            Assert.Equal("HOSEREEL", ((StepEnum)Assert.Single(step.OfType("IFCFIRESUPPRESSIONTERMINAL"))[8]!).Value);
            Assert.Equal("ENDSUCTION", ((StepEnum)Assert.Single(step.OfType("IFCPUMP"))[8]!).Value);
            Assert.NotEmpty(step.OfType("IFCCABLESEGMENT"));
            Assert.Contains(step.OfType("IFCDISTRIBUTIONPORT"), p => ((StepEnum)p[8]!).Value == "CABLE" && ((StepEnum)p[9]!).Value == "FIREPROTECTION");
            Assert.Empty(step.OfType("IFCBUILDINGELEMENTPROXY"));

            // Read back: the same elements, their classes and predefined types.
            var g = new MepFixture();
            IfcImport.Run(g.Session, path, null, out _);
            foreach (var id in new[] { (string)alarm["sourceId"]!, (string)alarm["endpointId"]!, (string)water["endpointId"]! })
            {
                var before = f.Model.Get(id)!;
                var after = g.Model.Get(id) ?? throw new Xunit.Sdk.XunitException($"{before.Name} did not come back");
                Assert.Equal(before.Class, after.Class);
                Assert.Equal(before.PredefinedType, after.PredefinedType);
            }
        }
        finally { File.Delete(path); }
    }
}
