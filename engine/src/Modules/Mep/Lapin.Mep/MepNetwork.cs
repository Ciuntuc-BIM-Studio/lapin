using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// The network as the IFC export takes it: a node's ports from its role, a
/// run's ports at its two ends and at each tee, the connections from the
/// port relationships, the systems with their nodes and runs.
/// </summary>
public sealed class MepNetwork : IIfcNetwork
{
    public IEnumerable<IfcPort> PortsOf(IModelView m, Entity e)
    {
        if (Nodes.IsNode(e))
        {
            var system = m.Get(e.Str("systemId") ?? "");
            var service = system != null ? Lapin.Mep.Systems.Service(system) : null;
            foreach (var p in Nodes.Ports(m, e))
            {
                var svc = p.Port.Service == "*" ? service ?? Services.Heating : p.Port.Service;
                yield return new IfcPort(p.Port.Key, p.At, p.Dir, FlowOf(p.Port.Flow), KindOf(svc), SystemTypeOf(svc));
            }
            yield break;
        }
        if (!Runs.IsRun(e)) yield break;
        var run = Runs.SystemOf(m, e) is { } sys ? Lapin.Mep.Systems.Service(sys) : Services.Heating;
        var line = Runs.Centreline(m, e, Runs.SettingOf(m, e), out _);
        if (line.Count >= 2)
        {
            // Upstream is the start: it takes what comes; the end gives.
            yield return new IfcPort("start", line[0], (line[0] - line[1]).Normalized(), "SINK", KindOf(run), SystemTypeOf(run));
            yield return new IfcPort("end", line[^1], (line[^1] - line[^2]).Normalized(), "SOURCE", KindOf(run), SystemTypeOf(run));
        }
        var z = line.Count > 0 ? line[0].Z : Nodes.StoreyZ(m, e) + Runs.SettingOf(m, e).Elevation;
        foreach (var (branch, station) in Runs.BranchesOf(m, e.Id))
        {
            var (at, _, dir) = Pipes.AtStation(Runs.Points(e), station);
            var bp = Runs.Points(branch);
            var endOn = Runs.EndOf(m, branch, "start")?.RunId == e.Id ? "start" : "end";
            var toward = bp.Count >= 2 ? (endOn == "start" ? bp[1] - bp[0] : bp[^2] - bp[^1]) : new Vec2(-dir.Y, dir.X);
            yield return new IfcPort("tee:" + branch.Id, new Vec3(at.X, at.Y, z), new Vec3(toward.X, toward.Y, 0).Normalized(), "SOURCE", KindOf(run), SystemTypeOf(run));
        }
    }

    public IEnumerable<IfcPortConnection> Connections(IModelView m)
    {
        foreach (var run in Runs.All(m))
            foreach (var end in new[] { "start", "end" })
            {
                if (Runs.EndOf(m, run, end) is not { } e) continue;
                if (e.NodeId != null) yield return new IfcPortConnection(run.Id, end, e.NodeId, e.Port ?? "");
                else if (e.RunId != null) yield return new IfcPortConnection(run.Id, end, e.RunId, "tee:" + run.Id);
            }
    }

    public IEnumerable<IfcSystemGroup> Systems(IModelView m) =>
        Lapin.Mep.Systems.All(m).Select(s => new IfcSystemGroup(s.Id, s.Name, SystemTypeOf(Lapin.Mep.Systems.Service(s)),
            [.. Nodes.Of(m, s.Id).Select(n => n.Id), .. Runs.Of(m, s.Id).Select(r => r.Id)]));

    static string FlowOf(Flow flow) => flow switch { Flow.In => "SINK", Flow.Out => "SOURCE", _ => "SOURCEANDSINK" };

    /// <summary>What carries the service: a pipe, a duct or a cable (IfcDistributionPortTypeEnum).</summary>
    public static string KindOf(string service) => service == Services.Electrical ? "CABLE" : Services.IsAir(service) ? "DUCT" : "PIPE";

    /// <summary>The service as IfcDistributionSystemEnum names it.</summary>
    public static string SystemTypeOf(string service) => service switch
    {
        Services.Heating => "HEATING",
        Services.ColdWater => "DOMESTICCOLDWATER",
        Services.HotWater => "DOMESTICHOTWATER",
        Services.Drainage => "WASTEWATER",
        Services.SupplyAir => "VENTILATION",
        Services.ExtractAir => "EXHAUST",
        Services.Electrical => "ELECTRICAL",
        _ => "USERDEFINED",
    };
}
