using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// MEP: the building's services as systems drawn as schematics on the plan
/// (docs/plan-mep.md, decision 133). A system holds the defaults; nodes
/// (sources, endpoints, fittings, rises) have ports; runs join them, their
/// parts generated; connections are port relationships that hold when things
/// move; a system is placed whole from a template and grows by endpoints that
/// join it. Commands are "mep.…". The engineering on the network (sizing,
/// checks, the assistants, the laying rules by material) is mep-pro's, which
/// hooks in through <see cref="MepHooks"/>.
/// </summary>
public sealed class MepModule : ILapinModule
{
    public string Id => "mep";
    public string Name => "MEP";

    public void Register(IModuleRegistry r)
    {
        Systems.Register(r);
        Runs.Register(r);
        // Each class once: a class may hold nodes (a stack is an IfcPipeSegment) and runs; the entity's attributes say which.
        foreach (var cls in Nodes.Classes.Union(Runs.Classes))
        {
            var isNode = Nodes.Classes.Contains(cls);
            var isRun = Runs.Classes.Contains(cls);
            var fields = (isNode ? Nodes.Fields : []).Concat(isRun ? Runs.Fields : []).GroupBy(f => f.Key).Select(g => g.First()).ToList();
            r.Class(new(cls, isRun ? Runs.Label(cls) : Nodes.Label(cls), fields, isRun ? "#c0392b" : "#8a8f98"));
            r.Geometry(cls, (e, m, _) => Runs.IsRun(e) ? Runs.Geometry(e, m) : Nodes.IsNode(e) ? Nodes.Geometry(e, m) : null);
            r.Quantities(cls, (e, g, m) => Runs.IsRun(e) ? Runs.Quantities(e, g, m) : Nodes.Quantities(e, g, m));
        }
        var placement = new MepPlacement();
        r.Service<IElementPlacement>(placement);
        r.Service<IElementUpdate>(placement);
        RegisterLayout(r);
        r.Service<IIfcNetwork>(new MepNetwork());
        r.Service<IScheduleSource>(new MepEquipmentSchedule());
        r.Service<IScheduleSource>(new MepRunSchedule());
        // On the plan the schematic is drawn from symbols, not from the bodies' cut.
        foreach (var cls in Nodes.Classes.Concat(Runs.Classes)) Generator.SymbolicInPlan.Add(cls);
        r.Catalog("roles", new JsonArray(Roles.All.Select(x => (JsonNode)new JsonObject
        {
            ["id"] = x.Id, ["label"] = x.Label, ["ifcClass"] = x.IfcClass, ["kind"] = x.Kind, ["symbol"] = x.Symbol,
            ["services"] = new JsonArray(x.Ports.Select(p => p.Service).Distinct().Select(s => (JsonNode)s).ToArray()),
            ["ports"] = new JsonArray(x.Ports.Select(p => (JsonNode)new JsonObject { ["key"] = p.Key, ["service"] = p.Service, ["flow"] = p.Flow.ToString().ToLowerInvariant(), ["pair"] = p.Pair }).ToArray()),
            ["size"] = new JsonArray(x.Size.X, x.Size.Y, x.Size.Z), ["elevation"] = x.Elevation, ["duty"] = x.Duty, ["dutyUnit"] = x.DutyUnit,
        }).ToArray()));
        r.Catalog("services", new JsonArray(Services.All.Select(s => (JsonNode)new JsonObject { ["id"] = s, ["label"] = Services.Label(s), ["defaults"] = SystemDefaults.For(s).ToJson() }).ToArray()));
        r.Catalog("materials", new JsonArray(RunMaterials.All.Select(x => (JsonNode)new JsonObject
        {
            ["id"] = x.Id, ["label"] = x.Label, ["services"] = new JsonArray(x.Services.Select(s => (JsonNode)s).ToArray()),
            ["aliases"] = new JsonArray((x.Aliases ?? []).Select(s => (JsonNode)s).ToArray()),
        }).ToArray()));

        // A node placed on a storey, in a system; dropped on a run, it splits it and takes both halves.
        r.Command("placeNode", "Place", (ctx, a) =>
        {
            var system = Systems.Require(ctx.Model, (string?)a["systemId"]);
            var role = Roles.Require((string?)a["role"]);
            var storey = (string?)a["storeyId"] is { } sid ? Spatial.Require(ctx.Model, sid, IfcClass.BuildingStorey) : Spatial.Storeys(ctx.Model).FirstOrDefault() ?? throw new CommandException("The project has no storey.");
            var node = Nodes.Place(ctx, system, role, storey.Id, a);
            // An endpoint placed with connect: joined to the system's network by a run of its own.
            if (a["connect"] is JsonValue cv && cv.TryGetValue<bool>(out var connect) && connect && role.IsEndpoint && (string?)a["onRunId"] == null)
            {
                var joined = Layout.Connect(ctx, ctx.Model.Get(system.Id)!, ctx.Model.Get(node.Id)!);
                return new JsonObject { ["id"] = node.Id, ["runId"] = joined?.Id };
            }
            if ((string?)a["onRunId"] is { } rid)
            {
                var run = ctx.Model.Get(rid) ?? throw new CommandException("There is no such run.");
                if (!Runs.IsRun(run)) throw new CommandException("Not a run.");
                if (!role.IsInline) throw new CommandException($"A {role.Label.ToLowerInvariant()} does not sit on a run.");
                Runs.SplitAt(ctx, run, ctx.Model.Get(node.Id)!);
            }
            return new JsonObject { ["id"] = node.Id };
        });

        // The network of a system (or of all): nodes with their ports and what is on them, runs with their ends.
        r.Query("network", (q, a) =>
        {
            var m = q.Model;
            var systems = (string?)a["systemId"] is { } sid ? [Systems.Require(m, sid)] : Systems.All(m).ToList();
            return new JsonArray(systems.Select(s => (JsonNode)new JsonObject
            {
                ["id"] = s.Id, ["name"] = s.Name, ["service"] = Systems.Service(s), ["defaults"] = Systems.Of(s).ToJson(),
                ["nodes"] = new JsonArray(Nodes.Of(m, s.Id).Select(n => (JsonNode)new JsonObject
                {
                    ["id"] = n.Id, ["name"] = n.Name, ["role"] = n.Str("role"), ["storeyId"] = Spatial.StoreyOf(m, n.Id)?.Id,
                    ["ports"] = new JsonArray(Nodes.Ports(m, n).Select(p => (JsonNode)new JsonObject
                    {
                        ["key"] = p.Port.Key, ["service"] = p.Port.Service, ["flow"] = p.Port.Flow.ToString().ToLowerInvariant(),
                        ["at"] = new JsonArray(p.At.X, p.At.Y, p.At.Z), ["taken"] = Runs.PortTaken(m, n, p.Port.Key),
                    }).ToArray()),
                }).ToArray()),
                ["runs"] = new JsonArray(Runs.Of(m, s.Id).Select(r2 => (JsonNode)new JsonObject
                {
                    ["id"] = r2.Id, ["name"] = r2.Name, ["storeyId"] = Spatial.StoreyOf(m, r2.Id)?.Id, ["points"] = Runs.J(Runs.Points(r2)),
                    ["start"] = End(Runs.EndOf(m, r2, "start")), ["end"] = End(Runs.EndOf(m, r2, "end")),
                }).ToArray()),
            }).ToArray());
        });

        // What is left open: every port of the system's nodes with no run on it (a pair counts as one), and every run end on nothing.
        r.Query("openPorts", (q, a) =>
        {
            var m = q.Model;
            var systems = (string?)a["systemId"] is { } sid ? [Systems.Require(m, sid)] : Systems.All(m).ToList();
            var o = new JsonArray();
            foreach (var s in systems)
            {
                var service = Systems.Service(s);
                foreach (var n in Nodes.Of(m, s.Id))
                {
                    var seen = new HashSet<string>();
                    foreach (var p in Nodes.Ports(m, n))
                    {
                        if (!Roles.Takes(p.Port, service) || seen.Contains(p.Port.Key)) continue;
                        if (p.Port.Pair != null) seen.Add(p.Port.Pair);
                        if (!Runs.PortTaken(m, n, p.Port.Key)) o.Add((JsonNode)new JsonObject { ["kind"] = "port", ["id"] = n.Id, ["name"] = n.Name, ["port"] = p.Port.Key, ["at"] = new JsonArray(p.At.X, p.At.Y, p.At.Z) });
                    }
                }
                foreach (var r2 in Runs.Of(m, s.Id))
                    foreach (var end in new[] { "start", "end" })
                        if (Runs.EndOf(m, r2, end) == null)
                        {
                            var pts = Runs.Points(r2);
                            var p = pts.Count == 0 ? default : end == "start" ? pts[0] : pts[^1];
                            o.Add((JsonNode)new JsonObject { ["kind"] = "run", ["id"] = r2.Id, ["name"] = r2.Name, ["end"] = end, ["at"] = new JsonArray(p.X, p.Y) });
                        }
            }
            return o;
        });
    }

    static void RegisterLayout(IModuleRegistry r)
    {
        // A system placed whole, resolving from the start: its source at the point, an endpoint beside it (or at "to"), the run between them.
        r.Command("placeSystem", "New system", (ctx, a) =>
        {
            var service = (string?)a["service"] ?? Services.Heating;
            var storey = (string?)a["storeyId"] is { } sid ? Spatial.Require(ctx.Model, sid, IfcClass.BuildingStorey) : Spatial.Storeys(ctx.Model).FirstOrDefault() ?? throw new CommandException("The project has no storey.");
            var at = new Vec2(Json.Number(a["x"]) ?? 0, Json.Number(a["y"]) ?? 0);
            Vec2? to = a["to"] is JsonArray t && t.Count >= 2 ? new Vec2(Json.Number(t[0]) ?? 0, Json.Number(t[1]) ?? 0) : null;
            var (system, source, endpoint, run) = Layout.PlaceSystem(ctx, service, storey.Id, at, (string?)a["name"], to);
            return new JsonObject { ["id"] = system.Id, ["sourceId"] = source.Id, ["endpointId"] = endpoint.Id, ["runId"] = run?.Id };
        });
        r.Catalog("templates", new JsonArray(Layout.Templates.Select(t => (JsonNode)new JsonObject
        {
            ["service"] = t.Service, ["source"] = t.Source, ["endpoint"] = t.Endpoint, ["endpoints"] = new JsonArray(t.Endpoints.Select(e => (JsonNode)e).ToArray()),
        }).ToArray()));
        // A handle on a run, where a point falls nearest it.
        r.Command("addHandle", "Add handle", (ctx, a) =>
        {
            var run = ctx.Model.Get((string?)a["runId"] ?? "") ?? throw new CommandException("Which run?");
            if (!Runs.IsRun(run)) throw new CommandException("Not a run.");
            Layout.InsertHandle(ctx, run, new Vec2(Json.Number(a["x"]) ?? 0, Json.Number(a["y"]) ?? 0));
            return null;
        });
        // The fittings a system's runs make (bends and tees), counted at the end of an edit.
        r.Query("fittings", (q, a) =>
        {
            var system = Systems.Require(q.Model, (string?)a["systemId"]);
            int bends = 0, tees = 0;
            foreach (var run in Runs.Of(q.Model, system.Id))
            {
                var (b, t) = Layout.FittingsOf(q.Model, run);
                bends += b; tees += t;
            }
            return new JsonObject { ["bends"] = bends, ["tees"] = tees };
        });
    }

    static JsonNode? End(RunEnd? e) => e == null ? null : e.NodeId != null
        ? new JsonObject { ["nodeId"] = e.NodeId, ["port"] = e.Port }
        : new JsonObject { ["runId"] = e.RunId, ["station"] = e.Station };
}
