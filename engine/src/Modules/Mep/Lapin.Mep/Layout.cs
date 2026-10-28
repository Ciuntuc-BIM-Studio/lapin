using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>The smallest system of a service that resolves: its source, one endpoint, the run between them; drainage runs from the endpoint to its stack.</summary>
public sealed record SystemTemplate(string Service, string Source, string Endpoint, string[] Endpoints);

/// <summary>
/// The system as it is laid out on the plan: placed whole from a template
/// (a boiler and a radiator joined, a meter and a basin, a stack and a WC…)
/// so it resolves from the start, then grown by endpoints that connect
/// themselves — a run routed from the nearest free source port or run of the
/// system (to it, for drainage), orthogonal around the storey's walls.
/// </summary>
public static class Layout
{
    public static readonly SystemTemplate[] Templates =
    [
        new(Services.Heating, "boiler", "radiator", ["radiator"]),
        new(Services.ColdWater, "waterMeter", "basin", ["basin", "sink", "wc", "shower", "bath"]),
        new(Services.HotWater, "valve", "basin", ["basin", "sink", "shower", "bath"]),
        new(Services.Drainage, "stack", "wc", ["wc", "basin", "sink", "shower", "bath"]),
        new(Services.Electrical, "panel", "outlet", ["outlet", "light"]),
        new(Services.SupplyAir, "fan", "diffuser", ["diffuser"]),
    ];

    public static SystemTemplate? TemplateOf(string service) => Templates.FirstOrDefault(t => t.Service == service);

    /// <summary>Where a new endpoint goes beside its source when no point is given: 3 m out, 2 m aside.</summary>
    public static readonly Vec2 Offset = new(3, 2);

    /// <summary>A system placed whole: its source at the point, its first endpoint beside it, the run between them.</summary>
    public static (Entity System, Entity Source, Entity Endpoint, Entity? Run) PlaceSystem(ICommandContext ctx, string service, string storeyId, Vec2 at, string? name = null, Vec2? endpointAt = null)
    {
        var t = TemplateOf(service) ?? throw new CommandException($"There is no system template for {Services.Label(service).ToLowerInvariant()}.");
        var system = Systems.Create(ctx, service, name);
        var source = Nodes.Place(ctx, system, Roles.Require(t.Source), storeyId, new JsonObject { ["x"] = at.X, ["y"] = at.Y });
        var e = endpointAt ?? at + Offset;
        var endpoint = Nodes.Place(ctx, system, Roles.Require(t.Endpoint), storeyId, new JsonObject { ["x"] = e.X, ["y"] = e.Y });
        var run = Connect(ctx, ctx.Model.Get(system.Id)!, ctx.Model.Get(endpoint.Id)!);
        return (system, source, endpoint, run);
    }

    /// <summary>The endpoint's free port for the service: what it takes (supply), or what it gives (drainage).</summary>
    static NodePort? FreePort(IModelView m, Entity node, string service)
    {
        var drain = service == Services.Drainage;
        return Nodes.Ports(m, node).FirstOrDefault(p => Roles.Takes(p.Port, service) && !Runs.PortTaken(m, node, p.Port.Key)
            && (p.Port.Flow == Flow.Both || (drain ? p.Port.Flow == Flow.Out : p.Port.Flow == Flow.In)));
    }

    /// <summary>
    /// A run that joins a node's free port to the network of its system on its
    /// storey: from (to, for drainage) the nearest free port of a source, else
    /// the nearest point of a run (a tee). Null when there is nothing to join.
    /// </summary>
    public static Entity? Connect(ICommandContext ctx, Entity system, Entity node)
    {
        var m = ctx.Model;
        var service = Systems.Service(system);
        var storey = Spatial.StoreyOf(m, node.Id)?.Id;
        if (storey == null || FreePort(m, node, service) is not { } port) return null;
        var drain = service == Services.Drainage;
        var to = port.At.Xy;
        JsonObject? other = null; Vec2 from = default;
        // A source's free port, or an inline fitting's (a meter where the supply enters).
        var source = Nodes.Of(m, system.Id).Where(n => n.Id != node.Id && Nodes.RoleOf(n) is { IsSource: true } or { IsInline: true } && Spatial.StoreyOf(m, n.Id)?.Id == storey)
            .SelectMany(n => Nodes.Ports(m, n).Where(p => Roles.Takes(p.Port, service) && !Runs.PortTaken(m, n, p.Port.Key)
                && (p.Port.Flow == Flow.Both || (drain ? p.Port.Flow == Flow.In : p.Port.Flow == Flow.Out))).Select(p => (Node: n, Port: p)))
            .OrderBy(x => (x.Port.At.Xy - to).Length).FirstOrDefault();
        if (source.Node != null) { other = new JsonObject { ["nodeId"] = source.Node.Id, ["port"] = source.Port.Port.Key }; from = source.Port.At.Xy; }
        else
        {
            var best = Runs.Of(m, system.Id).Where(r => Spatial.StoreyOf(m, r.Id)?.Id == storey).Select(r => (Run: r, Near: Pipes.Nearest(Runs.Points(r), to)))
                .OrderBy(x => x.Near.Distance).FirstOrDefault();
            if (best.Run == null) return null;
            from = Pipes.AtStation(Runs.Points(best.Run), best.Near.Station).At;
            other = new JsonObject { ["runId"] = best.Run.Id, ["at"] = new JsonArray(from.X, from.Y) };
        }
        // Routed round the walls when a router is there (mep-pro); else an L.
        var route = (from - to).Length < 1e-6 ? null : MepHooks.Router?.Route(m, storey, from, to);
        var pts = route?.ToList() ?? [from, new Vec2(to.X, from.Y), to];
        if (pts.Count < 2 || Pipes.Length(pts) < 1e-6) pts = [from, from + new Vec2(0.3, 0), to];
        var mine = new JsonObject { ["nodeId"] = node.Id, ["port"] = port.Port.Key };
        if (drain) pts.Reverse();
        return Runs.Draw(ctx, system, storey, new JsonObject
        {
            ["points"] = Runs.J(pts),
            ["start"] = drain ? mine : other,
            ["end"] = drain ? other : mine,
        });
    }

    /// <summary>A handle added to a run where a point falls nearest it.</summary>
    public static void InsertHandle(ICommandContext ctx, Entity run, Vec2 p)
    {
        var pts = Runs.Points(run);
        var (station, _) = Pipes.Nearest(pts, p);
        var (at, leg, _) = Pipes.AtStation(pts, station);
        if (pts.Any(q => (q - at).Length < 1e-4)) return;
        var old = Runs.Points(run);
        pts.Insert(leg + 1, at);
        var attrs = run.CloneAttrs();
        attrs["points"] = Runs.J(pts);
        if (Runs.RemapLevels(old, Runs.LevelsOf(run), pts, Runs.SettingOf(ctx.Model, run).Elevation) is { } levels) attrs["levels"] = levels;
        ctx.Replace(run.With(attrs: attrs));
    }

    /// <summary>A run's bends (a turn of its centreline, a jog among them) and tees (a branch on it), each once per pipe of a pair.</summary>
    public static (int Bends, int Tees) FittingsOf(IModelView m, Entity run)
    {
        var s = Runs.SettingOf(m, run);
        var line = Runs.Centreline(m, run, s, out _);
        var turns = 0;
        for (var i = 1; i + 1 < line.Count; i++)
        {
            var a = (line[i] - line[i - 1]).Normalized();
            var b = (line[i + 1] - line[i]).Normalized();
            if (Vec3.Dot(a, b) < 1 - 1e-6) turns++;
        }
        var pipes = s.Pair ? 2 : 1;
        return (turns * pipes, Runs.BranchesOf(m, run.Id).Count() * pipes);
    }
}
