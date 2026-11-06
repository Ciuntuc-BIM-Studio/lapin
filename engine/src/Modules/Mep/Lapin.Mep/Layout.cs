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
        new(Services.Gas, "meterBox", "gasBoiler", ["gasBoiler", "cooker", "waterHeater"]),
        new(Services.FuelOil, "oilTank", "oilBoiler", ["oilBoiler", "fillPoint", "ventCap"]),
        // Fire protection (report #89): the panel and a first detector on its loop; the pump set and a first hydrant.
        new(Services.FireAlarm, "firePanel", "smokeDetector", ["smokeDetector", "heatDetector", "callPoint", "sounder", "sounderBeacon"]),
        new(Services.FireWater, "firePump", "hydrant", ["hydrant", "externalHydrant", "sprinkler"]),
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

    /// <summary>
    /// The endpoint's free port for the service: what it takes (supply), or
    /// what it gives (drainage); else one the other way (a fill point gives
    /// to its tank), with <c>Against</c> set: its run then flows from it.
    /// </summary>
    static (NodePort Port, bool Against)? FreePort(IModelView m, Entity node, string service)
    {
        var drain = service == Services.Drainage;
        var free = Nodes.Ports(m, node).Where(p => Roles.Takes(p.Port, service) && !Runs.PortTaken(m, node, p.Port.Key)).ToList();
        if (free.FirstOrDefault(p => p.Port.Flow == Flow.Both || (drain ? p.Port.Flow == Flow.Out : p.Port.Flow == Flow.In)) is { } own) return (own, false);
        return free.FirstOrDefault() is { } other ? (other, true) : null;
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
        if (storey == null || FreePort(m, node, service) is not { } free) return null;
        var (port, against) = free;
        // Drainage flows from the endpoint, and so does an endpoint that gives (a fill point to its tank).
        var drain = (service == Services.Drainage) != against;
        var to = port.At.Xy;
        JsonObject? other = null; Vec2 from = default;
        // A device on a loop goes on from the loop's open end: a device whose cable came in and has not gone on (the nearest, when there are several).
        if (Nodes.RoleOf(node) is { Chain: true } && LoopEnd(m, system, node, storey, to) is { } end)
            return Draw(ctx, system, storey, end.At.Xy, to, new JsonObject { ["nodeId"] = end.NodeId, ["port"] = end.Port.Key }, new JsonObject { ["nodeId"] = node.Id, ["port"] = port.Port.Key }, false);
        // A source's free port, or an inline fitting's (a meter where the supply enters), of the same tag (a tank's fill for a fill point).
        var source = Nodes.Of(m, system.Id).Where(n => n.Id != node.Id && Nodes.RoleOf(n)?.KindIn(service) is "source" or "inline" && Spatial.StoreyOf(m, n.Id)?.Id == storey)
            .SelectMany(n => Nodes.Ports(m, n).Where(p => Roles.Takes(p.Port, service) && !Runs.PortTaken(m, n, p.Port.Key) && p.Port.Link == port.Port.Link
                && (p.Port.Flow == Flow.Both || (drain ? p.Port.Flow == Flow.In : p.Port.Flow == Flow.Out))).Select(p => (Node: n, Port: p)))
            .OrderBy(x => (x.Port.At.Xy - to).Length).FirstOrDefault();
        if (source.Node != null) { other = new JsonObject { ["nodeId"] = source.Node.Id, ["port"] = source.Port.Port.Key }; from = source.Port.At.Xy; }
        else
        {
            // A tagged port (a vent, a fill) is joined to its own port only, never teed onto the supply.
            if (port.Port.Link != null) return null;
            var best = Runs.Of(m, system.Id).Where(r => Spatial.StoreyOf(m, r.Id)?.Id == storey).Select(r => (Run: r, Near: Pipes.Nearest(Runs.Points(r), to)))
                .OrderBy(x => x.Near.Distance).FirstOrDefault();
            if (best.Run == null) return null;
            from = Pipes.AtStation(Runs.Points(best.Run), best.Near.Station).At;
            other = new JsonObject { ["runId"] = best.Run.Id, ["at"] = new JsonArray(from.X, from.Y) };
        }
        return Draw(ctx, system, storey, from, to, other, new JsonObject { ["nodeId"] = node.Id, ["port"] = port.Port.Key }, drain);
    }

    /// <summary>A run from one point to another, routed round the walls when a router is there (mep-pro), else an L; reversed for drainage.</summary>
    static Entity Draw(ICommandContext ctx, Entity system, string storey, Vec2 from, Vec2 to, JsonObject? other, JsonObject mine, bool drain)
    {
        var route = (from - to).Length < 1e-6 ? null : MepHooks.Router?.Route(ctx.Model, storey, from, to);
        var pts = route?.ToList() ?? [from, new Vec2(to.X, from.Y), to];
        if (pts.Count < 2 || Pipes.Length(pts) < 1e-6) pts = [from, from + new Vec2(0.3, 0), to];
        if (drain) pts.Reverse();
        return Runs.Draw(ctx, system, storey, new JsonObject
        {
            ["points"] = Runs.J(pts),
            ["start"] = drain ? mine : other,
            ["end"] = drain ? other : mine,
        });
    }

    /// <summary>
    /// Where a loop goes on from, on a storey: the free "out" of a device whose "in" is taken (the cable came to it and has
    /// not gone on), the nearest to <paramref name="near"/>; null when there is none (the panel's own loop port is the start).
    /// </summary>
    public static NodePort? LoopEnd(IModelView m, Entity system, Entity? except, string storey, Vec2 near) =>
        Nodes.Of(m, system.Id).Where(n => n.Id != except?.Id && Nodes.RoleOf(n) is { Chain: true } && Spatial.StoreyOf(m, n.Id)?.Id == storey
                && Runs.PortTaken(m, n, "in") && !Runs.PortTaken(m, n, "out"))
            .Select(n => Nodes.Port(m, n, "out")).OfType<NodePort>()
            .OrderBy(p => (p.At.Xy - near).Length).FirstOrDefault();

    /// <summary>
    /// The loop closed: a run from its open end back to the panel's free return (Class A wiring, a fault on the loop isolated
    /// from both sides). Null when the loop has no open end on a storey with a panel whose return is free.
    /// </summary>
    public static Entity? CloseLoop(ICommandContext ctx, Entity system)
    {
        var m = ctx.Model;
        foreach (var panel in Nodes.Of(m, system.Id).Where(n => Nodes.RoleOf(n)?.Id == "firePanel" && !Runs.PortTaken(m, n, "return")))
        {
            var storey = Spatial.StoreyOf(m, panel.Id)?.Id;
            if (storey == null || Nodes.Port(m, panel, "return") is not { } back) continue;
            if (LoopEnd(m, system, null, storey, back.At.Xy) is not { } end) continue;
            return Draw(ctx, system, storey, end.At.Xy, back.At.Xy, new JsonObject { ["nodeId"] = end.NodeId, ["port"] = end.Port.Key },
                new JsonObject { ["nodeId"] = panel.Id, ["port"] = "return" }, false);
        }
        return null;
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
