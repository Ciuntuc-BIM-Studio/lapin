using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Mep;

namespace Lapin.Engine.Tests;

/// <summary>The network in the IFC export (docs/plan-mep.md, stage 1): ports nested in their elements, connected, grouped in their system.</summary>
public class MepIfcNetworkTests
{
    static StepFile Export(MepFixture f)
    {
        var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "mep.ifc");
        return StepFile.Read(ms.ToArray());
    }

    static string Name(StepEntity e) => StepFile.Decode((string)e[2]!);
    static IEnumerable<StepRef> Refs(object? list) => ((List<object?>)list!).OfType<StepRef>();

    [Fact]
    public void A_heating_network_exports_its_system_ports_and_connections()
    {
        var f = new MepFixture();
        var s = f.System();
        var boiler = f.Node(s, "boiler", 0, 0);
        var radiator = f.Node(s, "radiator", 4, 3);
        var run = f.Draw(s, MepFixture.Pts((0, -0.5), (4, -0.5), (4, 3)), new JsonObject { ["nodeId"] = boiler }, new JsonObject { ["nodeId"] = radiator });
        var ifc = Export(f);

        // The elements keep their classes and predefined types; nothing falls back to a proxy.
        var b = Assert.Single(ifc.OfType("IFCBOILER"));
        Assert.Equal("WATER", ((StepEnum)b[8]!).Value);
        var rad = Assert.Single(ifc.OfType("IFCSPACEHEATER"));
        Assert.Equal("RADIATOR", ((StepEnum)rad[8]!).Value);
        var pipe = Assert.Single(ifc.OfType("IFCPIPESEGMENT"));
        Assert.Empty(ifc.OfType("IFCBUILDINGELEMENTPROXY"));

        // The boiler nests its supply and return, both PIPE ports of a HEATING system; the run its start and end.
        var nests = ifc.OfType("IFCRELNESTS").ToList();
        var boilerPorts = Refs(nests.Single(n => n[4] is StepRef r && r.Id == b.Id)[5]).Select(r => ifc.Get(r)!).ToList();
        Assert.Equal(["supply", "return"], boilerPorts.Select(Name).OrderDescending().ToArray());
        Assert.All(boilerPorts, p => Assert.Equal("IFCDISTRIBUTIONPORT", p.Type));
        Assert.All(boilerPorts, p => Assert.Equal("PIPE", ((StepEnum)p[8]!).Value));
        Assert.All(boilerPorts, p => Assert.Equal("HEATING", ((StepEnum)p[9]!).Value));
        Assert.Equal("SOURCE", ((StepEnum)boilerPorts.Single(p => Name(p) == "supply")[7]!).Value);
        Assert.Equal("SINK", ((StepEnum)boilerPorts.Single(p => Name(p) == "return")[7]!).Value);
        var runPorts = Refs(nests.Single(n => n[4] is StepRef r && r.Id == pipe.Id)[5]).Select(r => ifc.Get(r)!).ToList();
        Assert.Equal(["end", "start"], runPorts.Select(Name).Order().ToArray());
        // A port's placement hangs on its element's and stands where the port is: the boiler's supply at 1 m, facing −Y.
        var supply = boilerPorts.Single(p => Name(p) == "supply");
        var place = ifc.Get(supply[5])!;
        Assert.Equal(b[5], place[0]);
        var axis = ifc.Get(place[1])!;
        var at = ((List<object?>)ifc.Get(axis[0])![0]!).Cast<double>().ToArray();
        Assert.Equal(1.0, at[2], 6);
        var dir = ((List<object?>)ifc.Get(axis[1])![0]!).Cast<double>().ToArray();
        Assert.Equal(-1.0, dir[1], 6);

        // Two connections: the run's start on the boiler's supply, its end on the radiator's.
        var connects = ifc.OfType("IFCRELCONNECTSPORTS").ToList();
        Assert.Equal(2, connects.Count);
        var start = runPorts.Single(p => Name(p) == "start");
        var c = connects.Single(x => x[4] is StepRef r && r.Id == start.Id);
        Assert.Equal(supply.Id, ((StepRef)c[5]!).Id);

        // One HEATING system with the three elements, serving the building.
        var system = Assert.Single(ifc.OfType("IFCDISTRIBUTIONSYSTEM"));
        Assert.Equal("HEATING", ((StepEnum)system[6]!).Value);
        var group = Assert.Single(ifc.OfType("IFCRELASSIGNSTOGROUP"));
        Assert.Equal(system.Id, ((StepRef)group[6]!).Id);
        Assert.Equal(3, Refs(group[4]).Count());
        Assert.Contains(Refs(group[4]), r => r.Id == pipe.Id);
        var serves = Assert.Single(ifc.OfType("IFCRELSERVICESBUILDINGS"));
        Assert.Equal(system.Id, ((StepRef)serves[4]!).Id);
        Assert.Equal(Assert.Single(ifc.OfType("IFCBUILDING")).Id, Refs(serves[5]).Single().Id);
        _ = run;
    }

    [Fact]
    public void A_tee_is_a_port_of_the_trunk_the_branch_connects_to()
    {
        var f = new MepFixture();
        var s = f.System(Services.ColdWater);
        var meter = f.Node(s, "waterMeter", 0, 0);
        var basin = f.Node(s, "basin", 4, 2);
        var wc = f.Node(s, "wc", 6, 0);
        var trunk = f.Draw(s, MepFixture.Pts((0.1, 0), (6, 0)), new JsonObject { ["nodeId"] = meter }, new JsonObject { ["nodeId"] = wc });
        var branch = f.Draw(s, MepFixture.Pts((4, 0), (4, 2)), new JsonObject { ["runId"] = trunk, ["at"] = new JsonArray(4, 0) }, new JsonObject { ["nodeId"] = basin });
        var ifc = Export(f);
        var ports = ifc.OfType("IFCDISTRIBUTIONPORT").ToList();
        var tee = Assert.Single(ports, p => Name(p).StartsWith("tee:", StringComparison.Ordinal));
        Assert.Equal("DOMESTICCOLDWATER", ((StepEnum)tee[9]!).Value);
        // Four connections: the trunk's two ends on the meter and the WC, the branch's start on the tee and its end on the basin.
        var connects = ifc.OfType("IFCRELCONNECTSPORTS").ToList();
        Assert.Equal(4, connects.Count);
        Assert.Contains(connects, c => c[5] is StepRef r && r.Id == tee.Id);
        _ = branch;
    }
}
