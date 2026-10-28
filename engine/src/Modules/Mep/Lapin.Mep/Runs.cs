using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>What one end of a run is on: a node's port, or a station of another run (a junction), or nothing.</summary>
public sealed record RunEnd(string? NodeId, string? Port, string? RunId, double Station);

/// <summary>
/// A run of a system: a polyline drawn in plan on a storey, at an elevation
/// (its own, or its system's for its plane), of a size (its own, or the
/// system's), single or paired (supply and return, a separation apart). Its
/// ends connect to nodes' ports or to other runs (a junction, with a tee),
/// through <c>IfcRelConnectsPorts</c> relationships: the run relates, the
/// node or the trunk is related, the attributes say which end and which
/// port, or the station. A connected end follows its port: the first (or
/// last) leg is kept axis-aligned to meet it. The body: tubes along the
/// centreline with rounded corners, a tee at each junction, a reducer where
/// the size changes; a drainage run falls at its slope from its upstream end.
/// Its class is the service's (pipe, duct, cable segment).
/// </summary>
public static partial class Runs
{
    public static readonly string[] Classes = ["IfcPipeSegment", "IfcDuctSegment", "IfcCableSegment"];
    public const string Connects = "IfcRelConnectsPorts";

    public static bool IsRun(Entity e) => Classes.Contains(e.Class) && e.Attrs.ContainsKey("points") && e.Attrs.ContainsKey("systemId");
    public static IEnumerable<Entity> Of(IModelView m, string systemId) => Classes.SelectMany(m.OfClass).Where(e => IsRun(e) && e.Str("systemId") == systemId);
    public static IEnumerable<Entity> All(IModelView m) => Classes.SelectMany(m.OfClass).Where(IsRun);

    public static List<Vec2> Points(Entity e) => (e.Attrs["points"] as JsonArray)?.OfType<JsonArray>().Where(p => p.Count >= 2)
        .Select(p => new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0)).ToList() ?? [];
    public static JsonArray J(IEnumerable<Vec2> pts) => new(pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());

    public static Entity? SystemOf(IModelView m, Entity run) => m.Get(run.Str("systemId") ?? "") is { Class: Systems.Class } s ? s : null;

    /// <summary>What the run takes from its system, with its own values over it; the material by its id (<see cref="RunMaterials"/>).</summary>
    public sealed record Setting(string Service, double Size, double Elevation, bool Pair, double Separation, double Slope, double BendRadius, string? Material);

    public static Setting SettingOf(IModelView m, Entity run)
    {
        var system = SystemOf(m, run);
        var d = system != null ? Systems.Of(system) : SystemDefaults.For(Services.Heating);
        var service = system != null ? Systems.Service(system) : Services.Heating;
        var material = run.Str("material") ?? d.Material;
        // The bend radius the material takes (when its rules are known), unless the system or the run sets one.
        var bend = run.NumOrNull("bendRadius") ?? (Json.Number((system?.Attrs["defaults"] as JsonObject)?["bendRadius"]) is { } sb ? sb : MepHooks.Laying?.BendRadius(service, material) ?? d.BendRadius);
        return new(service, run.NumOrNull("size") ?? d.Size, run.NumOrNull("elevation") ?? d.ElevationOf(run.Str("plane")),
            run.Attrs["pair"] is JsonValue pv && pv.TryGetValue<bool>(out var pair) ? pair : d.Pair, run.NumOrNull("separation") ?? d.Separation,
            run.NumOrNull("slope") ?? d.Slope, bend, material);
    }

    // ─── Connections ────────────────────────────────────────────────────────

    /// <summary>The run's connection at an end ("start" or "end"), if any.</summary>
    public static Relation? Connection(IModelView m, string runId, string end) =>
        m.RelationsOf(runId, Connects).FirstOrDefault(r => r.Relating == runId && (string?)r.Attrs?["end"] == end);

    public static RunEnd? EndOf(IModelView m, Entity run, string end)
    {
        if (Connection(m, run.Id, end) is not { } c || c.Related.Count == 0) return null;
        var other = m.Get(c.Related[0]);
        if (other == null) return null;
        return IsRun(other) ? new RunEnd(null, null, other.Id, Json.Number(c.Attrs?["station"]) ?? 0) : new RunEnd(other.Id, (string?)c.Attrs?["port"], null, 0);
    }

    /// <summary>The runs that start or end on this node's port, by port key.</summary>
    public static IEnumerable<(Entity Run, string End, string Port)> OnNode(IModelView m, string nodeId) =>
        m.RelationsOf(nodeId, Connects).Where(r => r.Related.Contains(nodeId)).Select(r => (m.Get(r.Relating), (string?)r.Attrs?["end"] ?? "start", (string?)r.Attrs?["port"] ?? ""))
            .Where(x => x.Item1 != null && IsRun(x.Item1!)).Select(x => (x.Item1!, x.Item2, x.Item3));

    /// <summary>The branches that leave this run, with their stations.</summary>
    public static IEnumerable<(Entity Branch, double Station)> BranchesOf(IModelView m, string runId) =>
        m.RelationsOf(runId, Connects).Where(r => r.Related.Contains(runId) && r.Relating != runId).Select(r => (m.Get(r.Relating), Json.Number(r.Attrs?["station"]) ?? 0))
            .Where(x => x.Item1 != null && IsRun(x.Item1!)).Select(x => (x.Item1!, x.Item2));

    /// <summary>Whether a node's port has a run on it (a paired port counts with its pair).</summary>
    public static bool PortTaken(IModelView m, Entity node, string key)
    {
        var role = Nodes.RoleOf(node);
        var pair = role?.Ports.FirstOrDefault(p => p.Key == key)?.Pair;
        return OnNode(m, node.Id).Any(x => x.Port == key || (pair != null && x.Port == pair));
    }

    /// <summary>
    /// The port a run's end takes on a node: the one named, else the free port
    /// of the run's service facing the right way (a run's start leaves a port
    /// that gives out; its end arrives at one that takes in); a paired run
    /// takes the supply port and its pair.
    /// </summary>
    public static RolePort PickPort(IModelView m, Entity node, string service, string end, string? key)
    {
        var role = Nodes.RoleOf(node) ?? throw new CommandException("Not a node.");
        var ports = role.Ports.Where(p => Roles.Takes(p, service)).ToList();
        if (ports.Count == 0) throw new CommandException($"„{node.Name}” has no port for {Services.Label(service).ToLowerInvariant()}.");
        if (key != null) return ports.FirstOrDefault(p => p.Key == key) ?? throw new CommandException($"„{node.Name}” has no port „{key}” for that service.");
        var want = end == "start" ? Flow.Out : Flow.In;
        var free = ports.Where(p => !PortTaken(m, node, p.Key)).ToList();
        return free.FirstOrDefault(p => p.Flow == want || p.Flow == Flow.Both) ?? free.FirstOrDefault()
            ?? throw new CommandException($"Every port of „{node.Name}” has a run on it already.");
    }

    public static void Connect(ICommandContext ctx, Entity run, string end, Entity node, string? key)
    {
        if (end is not ("start" or "end")) throw new CommandException("A run connects at its start or its end.");
        var setting = SettingOf(ctx.Model, run);
        var port = PickPort(ctx.Model, node, setting.Service, end, key);
        if (PortTaken(ctx.Model, node, port.Key) && Connection(ctx.Model, run.Id, end) is not { } own) throw new CommandException($"Port „{port.Key}” of „{node.Name}” has a run on it already.");
        Disconnect(ctx, run, end);
        ctx.AddRelation(new Relation(ctx.NewId(), Connects, run.Id, [node.Id], new JsonObject { ["end"] = end, ["port"] = port.Key }));
    }

    /// <summary>A junction: the run's end on a station of another run of the same system (a tee there).</summary>
    public static void ConnectToRun(ICommandContext ctx, Entity run, string end, Entity trunk, double station)
    {
        if (trunk.Id == run.Id) throw new CommandException("A run cannot branch off itself.");
        if (trunk.Str("systemId") != run.Str("systemId")) throw new CommandException("A branch joins a run of its own system.");
        var length = Pipes.Length(Points(trunk));
        Disconnect(ctx, run, end);
        ctx.AddRelation(new Relation(ctx.NewId(), Connects, run.Id, [trunk.Id], new JsonObject { ["end"] = end, ["station"] = Math.Clamp(station, 0, Math.Max(0, length)) }));
    }

    public static void Disconnect(ICommandContext ctx, Entity run, string end)
    {
        if (Connection(ctx.Model, run.Id, end) is { } c) ctx.RemoveRelation(c.Id);
    }

    // ─── The centreline as built ───────────────────────────────────────────

    /// <summary>
    /// The run's centreline as built. In plan, a connected end moves to its
    /// port (or its junction), and the leg next to it stays axis-aligned by
    /// sliding its other point across. In height: a level run lies at its
    /// elevation and jogs vertically to a port at another height; a sloped run
    /// (drainage) starts at its upstream port and falls — to its downstream
    /// port when it has one, else at its slope.
    /// </summary>
    public static List<Vec3> Centreline(IModelView m, Entity run, Setting s, out double z0) => Centreline(m, run, s, out z0, out _);

    /// <summary>A run's own elevations, one per point (above its storey; null: the run's elevation), when it has them.</summary>
    public static List<double?>? LevelsOf(Entity run)
    {
        if (run.Attrs["levels"] is not JsonArray a || a.Count != Points(run).Count) return null;
        var o = a.Select(x => Json.Number(x)).ToList();
        return o.Any(x => x != null) ? o : null;
    }

    /// <summary>
    /// Levels carried onto new points: a point where an old one was keeps its
    /// level; a new one takes the level of the old line where it falls
    /// (between two levels, in proportion). Null when the run had none.
    /// </summary>
    public static JsonArray? RemapLevels(IReadOnlyList<Vec2> oldPts, IReadOnlyList<double?>? oldLevels, IReadOnlyList<Vec2> newPts, double baseLevel)
    {
        if (oldLevels == null || oldPts.Count != oldLevels.Count || oldPts.Count < 2) return null;
        var o = new JsonArray();
        foreach (var p in newPts)
        {
            var same = Enumerable.Range(0, oldPts.Count).FirstOrDefault(i => (oldPts[i] - p).Length < 1e-6, -1);
            if (same >= 0) { o.Add(oldLevels[same] is { } v ? (JsonNode)v : null); continue; }
            var (station, _) = Pipes.Nearest(oldPts, p);
            var (_, leg, _) = Pipes.AtStation(oldPts, station);
            double a0 = oldLevels[leg] ?? baseLevel, a1 = oldLevels[Math.Min(leg + 1, oldPts.Count - 1)] ?? baseLevel;
            double s0 = 0; for (var i = 1; i <= leg; i++) s0 += (oldPts[i] - oldPts[i - 1]).Length;
            var len = (oldPts[Math.Min(leg + 1, oldPts.Count - 1)] - oldPts[leg]).Length;
            var t = len > 1e-9 ? Math.Clamp((station - s0) / len, 0, 1) : 0;
            o.Add(oldLevels[leg] == null && oldLevels[Math.Min(leg + 1, oldPts.Count - 1)] == null ? null : (JsonNode)Math.Round(a0 + (a1 - a0) * t, 4));
        }
        return o;
    }

    /// <summary>
    /// The run's route in plan as built, what the plan draws: a connected end
    /// moved onto its port (or its junction), the leg next to it kept in its
    /// direction by sliding its other point along the leg after it; a rigid
    /// material's elbow between two ends that no longer line up.
    /// <c>Source</c>: which of the run's own points each one is. It is what
    /// is written back into the run when what an end is on moves
    /// (<see cref="Refollow"/>), so the run's points are where it is drawn.
    /// </summary>
    static (List<Vec2> Points, List<int?> Source, List<double?>? Levels, Vec3? StartAt, Vec3? EndAt) Follow(IModelView m, Entity run, Setting s, double zRun)
    {
        var pts = Points(run);
        var levels = LevelsOf(run);
        var src = Enumerable.Range(0, pts.Count).Select(i => (int?)i).ToList();
        Vec3? startAt = null, endAt = null;
        if (pts.Count < 2) return (pts, src, levels, null, null);
        foreach (var end in new[] { "start", "end" })
        {
            var i = end == "start" ? 0 : pts.Count - 1;
            if (EndOf(m, run, end) is not { } e) continue;
            Vec3? at = null;
            if (e.NodeId != null && m.Get(e.NodeId) is { } node && Nodes.Port(m, node, e.Port ?? "") is { } port)
            {
                // A paired run meets the middle of the pair; a single one its port.
                at = port.At;
                var pair = Nodes.RoleOf(node)?.Ports.FirstOrDefault(p => p.Key == port.Port.Pair);
                if (s.Pair && pair != null && Nodes.Port(m, node, pair.Key) is { } other) at = (port.At + other.At) * 0.5;
            }
            else if (e.RunId != null && m.Get(e.RunId) is { } trunk && IsRun(trunk))
            {
                var line = Centreline(m, trunk, SettingOf(m, trunk), out _);
                var (p2, _, _) = Pipes.AtStation(Points(trunk), e.Station);
                at = new Vec3(p2.X, p2.Y, line.Count > 0 ? ZAt(line, p2) : zRun);
            }
            if (at is not { } a) continue;
            var next = i == 0 ? 1 : pts.Count - 2;
            var after = i == 0 ? 2 : pts.Count - 3;
            if (next != i)
            {
                var leg = pts[next] - pts[i];
                // The end leg keeps its direction: its other point slides along the leg after it to stay in line with the moved end.
                var moved = after >= 0 && after < pts.Count && after != i && Pipes.Intersect(a.Xy, leg, pts[after], pts[next] - pts[after]) is { } x ? x
                    : Math.Abs(leg.X) >= Math.Abs(leg.Y) ? new Vec2(pts[next].X, a.Y) : new Vec2(a.X, pts[next].Y);
                pts[next] = moved;
            }
            pts[i] = a.Xy;
            if (i == 0) startAt = a; else endAt = a;
        }
        // A rigid material between two ends that no longer line up on its directions: an elbow between them (the rules come with mep-pro).
        if (pts.Count == 2 && MepHooks.Laying is { } laying && laying.Breaks(s.Service, s.Material, pts))
        {
            var d = pts[1] - pts[0];
            pts.Insert(1, Math.Abs(d.X) >= Math.Abs(d.Y) ? new Vec2(pts[1].X, pts[0].Y) : new Vec2(pts[0].X, pts[1].Y));
            src.Insert(1, null);
            levels?.Insert(1, levels[0] is { } l0 && levels[2] is { } l2 ? (l0 + l2) / 2 : levels[0] ?? levels[2]);
            // A material that turns less than a right angle cuts the elbow into the turns it may make.
            var laid = laying.Lay(s.Service, s.Material, pts);
            if (laid.Count != pts.Count) { pts = laid; src = [.. pts.Select((_, k) => k == 0 ? (int?)0 : k == pts.Count - 1 ? 1 : null)]; levels = null; }
        }
        return (pts, src, levels, startAt, endAt);
    }

    /// <summary>The run's points as it is drawn in plan: its ends on what they are on.</summary>
    public static List<Vec2> Followed(IModelView m, Entity run)
    {
        var s = SettingOf(m, run);
        return Follow(m, run, s, Nodes.StoreyZ(m, run) + s.Elevation).Points;
    }

    /// <summary>
    /// The run's centreline in 3D. <paramref name="vertexOf"/> tells, for each
    /// of its points, which of the run's own points it is (null: a jog to a
    /// port, or an elbow the material asked for).
    /// </summary>
    public static List<Vec3> Centreline(IModelView m, Entity run, Setting s, out double z0, out List<int?> vertexOf)
    {
        z0 = Nodes.StoreyZ(m, run);
        var zRun = z0 + s.Elevation;
        vertexOf = [];
        if (Points(run).Count < 2) return [];
        var (pts, src, levels, startAt, endAt) = Follow(m, run, s, zRun);
        var o = new List<Vec3>(pts.Count + 2);
        // The run's own elevations, set in its profile: each point at its own, the ends jogging to their ports.
        if (levels != null)
        {
            if (startAt is { } sl && Math.Abs(sl.Z - (z0 + (levels[0] ?? s.Elevation))) > 1e-6) { o.Add(sl); vertexOf.Add(null); }
            for (var i = 0; i < pts.Count; i++) { o.Add(new Vec3(pts[i].X, pts[i].Y, z0 + (levels[i] ?? s.Elevation))); vertexOf.Add(src[i]); }
            if (endAt is { } el && Math.Abs(el.Z - (z0 + (levels[^1] ?? s.Elevation))) > 1e-6) { o.Add(el); vertexOf.Add(null); }
            return o;
        }
        if (s.Slope > 1e-9)
        {
            var zStart = startAt?.Z ?? zRun;
            var total = Pipes.Length(pts);
            double station = 0;
            for (var i = 0; i < pts.Count; i++)
            {
                if (i > 0) station += (pts[i] - pts[i - 1]).Length;
                var z = endAt is { } eAt && total > 1e-9 ? zStart + (eAt.Z - zStart) * (station / total) : zStart - s.Slope * station;
                o.Add(new Vec3(pts[i].X, pts[i].Y, z));
                vertexOf.Add(src[i]);
            }
            return o;
        }
        if (startAt is { } sa && Math.Abs(sa.Z - zRun) > 1e-6) { o.Add(sa); vertexOf.Add(null); }
        for (var i = 0; i < pts.Count; i++) { o.Add(new Vec3(pts[i].X, pts[i].Y, zRun)); vertexOf.Add(src[i]); }
        if (endAt is { } ea && Math.Abs(ea.Z - zRun) > 1e-6) { o.Add(ea); vertexOf.Add(null); }
        return o;
    }

    static double ZAt(List<Vec3> line, Vec2 p)
    {
        var best = double.PositiveInfinity; var z = line[0].Z;
        for (var i = 1; i < line.Count; i++)
        {
            var a = line[i - 1].Xy; var d = line[i].Xy - a; var l2 = Vec2.Dot(d, d);
            var t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a, d) / l2, 0, 1);
            var dist = (p - (a + d * t)).Length;
            if (dist < best) { best = dist; z = line[i - 1].Z + (line[i].Z - line[i - 1].Z) * t; }
        }
        return z;
    }

    // ─── Geometry ───────────────────────────────────────────────────────────

    public static ElementGeometry? Geometry(Entity run, IModelView m)
    {
        var s = SettingOf(m, run);
        var line = Centreline(m, run, s, out _);
        if (line.Count < 2) return new ElementGeometry([], Diagnostics: ["A run needs two points."]);
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        var radius = s.Size / 2;
        var color = s.Service switch { Services.Heating => "#c0392b", Services.ColdWater => "#2e86de", Services.HotWater => "#e67e22", Services.Drainage => "#7f8c8d", Services.Electrical => "#f1c40f", _ => "#95a5a6" };
        var lines = s.Pair ? new[] { ("supply", Shift(line, s.Separation / 2)), ("return", Shift(line, -s.Separation / 2)) } : [("pipe", line)];
        foreach (var (name, pts) in lines)
        {
            var (legs, bends) = Pipes.Fillet(pts, Math.Max(radius * 2 * s.BendRadius, radius * 2));
            var tubes = new List<Solid>();
            for (var i = 0; i < legs.Count; i++)
                if ((Services.IsAir(s.Service) && run.NumOrNull("height") is { } h ? Pipes.Box(legs[i].A, legs[i].B, s.Size, h, "pipe") : Pipes.Tube(legs[i].A, legs[i].B, radius, 12, "pipe")) is { } t) tubes.Add(t);
            if (tubes.Count > 0) parts.Add(new GeometryPart(name == "pipe" ? "Pipe" : name == "supply" ? "Supply" : "Return", name == "return" ? "#2e4a9e" : color, Openings.Combine(tubes, "pipe"), null, name));
            var elbows = new List<Solid>();
            foreach (var bend in bends)
                for (var i = 1; i < bend.Count; i++)
                    if (Pipes.Tube(bend[i - 1], bend[i], radius, 12, "bend") is { } t) elbows.Add(t);
            if (elbows.Count > 0) parts.Add(new GeometryPart("Bends", color, Openings.Combine(elbows, "bend"), null, "bend:" + name));
            plan.Add(new PlanItem("symbol", false, Spots(pts)));
        }
        // A tee where a branch leaves: a short stub across the trunk at the junction.
        foreach (var (branch, station) in BranchesOf(m, run.Id))
        {
            var (at, _, dir) = Pipes.AtStation(Points(run), station);
            var z = ZAt(line, at);
            var across = new Vec3(-dir.Y, dir.X, 0);
            var centre = new Vec3(at.X, at.Y, z);
            if (Pipes.Tube(centre - across * (radius * 1.6), centre + across * (radius * 1.6), radius * 1.15, 12, "tee") is { } tee) parts.Add(new GeometryPart("Tee", color, tee, null, "tee:" + branch.Id));
            plan.Add(new PlanItem("heavy", true, [at + across.Xy * 0.03, at - across.Xy * 0.03, at - across.Xy * 0.03 + dir * 0.03, at + across.Xy * 0.03 + dir * 0.03]));
        }
        plan.Add(new PlanItem("hit", false, Spots(line)));
        return new ElementGeometry(parts, plan, line.Select(p => p.Xy).ToList());
    }

    /// <summary>
    /// One pipe of a pair: the centreline shifted sideways in plan. A vertical
    /// jog (two points on one spot in plan, at a port) is shifted as its spot,
    /// so the plan offset never sees a leg of no length.
    /// </summary>
    /// <summary>A 3D line as the plan sees it: a point where it stands, once (a vertical jog is one point).</summary>
    static List<Vec2> Spots(List<Vec3> line)
    {
        var o = new List<Vec2>();
        foreach (var p in line) if (o.Count == 0 || (p.Xy - o[^1]).Length > 1e-9) o.Add(p.Xy);
        return o;
    }

    static List<Vec3> Shift(List<Vec3> line, double offset)
    {
        var spots = new List<Vec2>();
        var spotOf = new int[line.Count];
        for (var i = 0; i < line.Count; i++)
        {
            if (spots.Count == 0 || (line[i].Xy - spots[^1]).Length > 1e-9) spots.Add(line[i].Xy);
            spotOf[i] = spots.Count - 1;
        }
        if (spots.Count < 2) return line;
        var flat = Pipes.Offset(spots, offset);
        return line.Select((p, i) => new Vec3(flat[spotOf[i]].X, flat[spotOf[i]].Y, p.Z)).ToList();
    }

    public static IEnumerable<QuantitySet> Quantities(Entity run, ElementGeometry? g, IModelView m)
    {
        var s = SettingOf(m, run);
        var line = Centreline(m, run, s, out _);
        double length = 0;
        for (var i = 1; i < line.Count; i++) length += (line[i] - line[i - 1]).Length;
        yield return new QuantitySet("Lapin_Mep", [new("Length", length, "LENGTH"), new("PipeLength", s.Pair ? 2 * length : length, "LENGTH"), new("Size", s.Size, "LENGTH")]);
    }

    // ─── Commands ───────────────────────────────────────────────────────────

    static List<Vec2> PointsArg(JsonNode? n)
    {
        var pts = (n as JsonArray)?.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0)).ToList() ?? [];
        if (pts.Count < 2) throw new CommandException("A run needs at least two points [[x, y], …].");
        return pts;
    }

    /// <summary>An end as a command names it: {nodeId, port?} or {runId, station? | at: [x, y]}.</summary>
    static void ConnectEnd(ICommandContext ctx, Entity run, string end, JsonNode? spec)
    {
        if (spec is not JsonObject o) return;
        if ((string?)o["nodeId"] is { } nid)
        {
            var node = ctx.Model.Get(nid) ?? throw new CommandException("There is no such node.");
            if (!Nodes.IsNode(node)) throw new CommandException($"„{node.Name}” is not a node of a system.");
            Connect(ctx, run, end, node, (string?)o["port"]);
        }
        else if ((string?)o["runId"] is { } rid)
        {
            var trunk = ctx.Model.Get(rid) ?? throw new CommandException("There is no such run.");
            if (!IsRun(trunk)) throw new CommandException("Not a run.");
            var station = Json.Number(o["station"]) ?? (o["at"] is JsonArray at && at.Count >= 2 ? Pipes.Nearest(Points(trunk), new Vec2(Json.Number(at[0]) ?? 0, Json.Number(at[1]) ?? 0)).Station : Pipes.Nearest(Points(trunk), Points(run)[end == "start" ? 0 : ^1]).Station);
            ConnectToRun(ctx, run, end, trunk, station);
        }
    }

    /// <summary>The points laid as the material allows (the system's, or the run's own), when its rules are known; else as they are.</summary>
    static List<Vec2> Lay(string service, string? material, IReadOnlyList<Vec2> pts) => MepHooks.Laying?.Lay(service, material, pts) ?? [.. pts];

    public static Entity Draw(ICommandContext ctx, Entity system, string storeyId, JsonObject a)
    {
        var service = Systems.Service(system);
        var attrs = new JsonObject { ["systemId"] = system.Id, ["points"] = J(Lay(service, (string?)a["material"] ?? Systems.Of(system).Material, PointsArg(a["points"]))) };
        foreach (var k in new[] { "elevation", "size", "separation", "slope", "height" }) if (Json.Number(a[k]) is { } v) attrs[k] = v;
        if ((string?)a["plane"] is "floor" or "ceiling" or "wall") attrs["plane"] = (string)a["plane"]!;
        if (a["pair"] is JsonValue pv && pv.TryGetValue<bool>(out var pair)) attrs["pair"] = pair;
        if ((string?)a["material"] is { Length: > 0 } mat) attrs["material"] = mat;
        var count = Of(ctx.Model, system.Id).Count() + 1;
        var run = new Entity(ctx.NewId(), Services.RunClass(service), (string?)a["name"] is { Length: > 0 } name ? name : $"{Services.Label(service)} run {count}", attrs, "RIGIDSEGMENT");
        ctx.Add(run);
        Spatial.Contain(ctx, storeyId, run.Id);
        ConnectEnd(ctx, run, "start", a["start"]);
        ConnectEnd(ctx, run, "end", a["end"]);
        // Kept as drawn (a short leg the user drew between two corners stays): its points are written where it is drawn once what it is on moves, or its route is edited.
        return ctx.Model.Get(run.Id)!;
    }

    /// <summary>
    /// A fitting dropped on a run: the run is cut at the nearest station into
    /// two, the first ending on the fitting's "in" port, the second starting
    /// on its "out"; what the whole run was connected to stays on the halves.
    /// </summary>
    public static void SplitAt(ICommandContext ctx, Entity run, Entity node)
    {
        var pts = Points(run);
        var (station, dist) = Pipes.Nearest(pts, Nodes.Position(node));
        if (dist > 0.5) throw new CommandException("The fitting must be dropped on the run.");
        var (at, leg, _) = Pipes.AtStation(pts, station);
        var first = pts.Take(leg + 1).Append(at).ToList();
        var second = new List<Vec2> { at }.Concat(pts.Skip(leg + 1)).ToList();
        var endConn = Connection(ctx.Model, run.Id, "end");
        var attrs = run.CloneAttrs();
        attrs["points"] = J(first);
        ctx.Replace(run.With(attrs: attrs));
        var tail = new Entity(ctx.NewId(), run.Class, run.Name + " (after " + node.Name + ")", run.CloneAttrs(), run.PredefinedType);
        tail.Attrs["points"] = J(second);
        ctx.Add(tail);
        if (Spatial.StoreyOf(ctx.Model, run.Id) is { } storey) Spatial.Contain(ctx, storey.Id, tail.Id);
        if (endConn != null)
        {
            ctx.RemoveRelation(endConn.Id);
            ctx.AddRelation(new Relation(ctx.NewId(), Connects, tail.Id, endConn.Related, endConn.Attrs?.DeepClone() as JsonObject));
        }
        // The branches past the cut move to the tail, their stations counted from it.
        foreach (var rel in ctx.Model.RelationsOf(run.Id, Connects).Where(r => r.Related.Contains(run.Id) && r.Relating != run.Id).ToList())
        {
            var st = Json.Number(rel.Attrs?["station"]) ?? 0;
            if (st <= station) continue;
            ctx.RemoveRelation(rel.Id);
            var na = rel.Attrs?.DeepClone() as JsonObject ?? [];
            na["station"] = st - station;
            ctx.AddRelation(new Relation(ctx.NewId(), Connects, rel.Relating, [tail.Id], na));
        }
        var role = Nodes.RoleOf(node)!;
        var service = SettingOf(ctx.Model, run).Service;
        var inPort = role.Ports.FirstOrDefault(p => Roles.Takes(p, service) && p.Flow == Flow.In) ?? role.Ports[0];
        var outPort = role.Ports.FirstOrDefault(p => Roles.Takes(p, service) && p.Flow == Flow.Out) ?? role.Ports[^1];
        Connect(ctx, ctx.Model.Get(run.Id)!, "end", node, inPort.Key);
        Connect(ctx, ctx.Model.Get(tail.Id)!, "start", node, outPort.Key);
    }

    /// <summary>The fields every run shows.</summary>
    public static readonly FieldDescriptor[] Fields =
    [
        new("size", "Size (diameter)", "length", Min: 0.004), new("height", "Height (rectangular duct)", "length", Min: 0.03),
        new("elevation", "Elevation", "length"), new("plane", "Plane", "choice", Choices: ["floor", "ceiling", "wall"]),
        new("pair", "Pair (supply and return)", "bool"), new("separation", "Separation", "length", Min: 0), new("slope", "Slope", "number", Min: 0, Max: 0.5),
        new("material", "Material", "choice", Choices: [.. RunMaterials.All.Select(x => x.Id)]),
    ];

    public static string Label(string cls) => cls switch { "IfcDuctSegment" => "Duct run", "IfcCableSegment" => "Cable run", _ => "Pipe run" };

    public static void Register(IModuleRegistry r)
    {
        // A run drawn in plan: points, its own settings, and what its ends are on ({nodeId, port?} or {runId, station? | at}).
        r.Command("drawRun", "Draw run", (ctx, a) =>
        {
            var system = Systems.Require(ctx.Model, (string?)a["systemId"]);
            var storey = (string?)a["storeyId"] is { } sid ? Spatial.Require(ctx.Model, sid, IfcClass.BuildingStorey) : Spatial.Storeys(ctx.Model).FirstOrDefault() ?? throw new CommandException("The project has no storey.");
            var run = Draw(ctx, system, storey.Id, a);
            return new JsonObject { ["id"] = run.Id };
        });

        r.Command("connect", "Connect run", (ctx, a) =>
        {
            var run = ctx.Model.Get((string?)a["runId"] ?? "") ?? throw new CommandException("Which run?");
            if (!IsRun(run)) throw new CommandException("Not a run.");
            var end = (string?)a["end"] ?? "end";
            ConnectEnd(ctx, run, end, a["to"] ?? a);
            return null;
        });

        r.Command("disconnect", "Disconnect run", (ctx, a) =>
        {
            var run = ctx.Model.Get((string?)a["runId"] ?? "") ?? throw new CommandException("Which run?");
            Disconnect(ctx, run, (string?)a["end"] ?? "end");
            return null;
        });

        // A run's elevations, one per point, above its storey (null: back to the run's elevation); all null: none of its own.
        r.Command("setRunLevels", "Run elevations", (ctx, a) =>
        {
            var run = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which run?");
            if (!IsRun(run)) throw new CommandException("Not a run.");
            var n = Points(run).Count;
            if (a["levels"] is not JsonArray given || given.Count != n) throw new CommandException($"One elevation per point ({n}), or null.");
            var attrs = run.CloneAttrs();
            if (given.Any(x => Json.Number(x) != null)) attrs["levels"] = new JsonArray(given.Select(x => Json.Number(x) is { } v ? (JsonNode)Math.Round(v, 4) : null).ToArray());
            else attrs.Remove("levels");
            ctx.Replace(run.With(attrs: attrs));
            return null;
        });

        r.Command("setRunPoints", "Move run", (ctx, a) =>
        {
            var run = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which run?");
            if (!IsRun(run)) throw new CommandException("Not a run.");
            // The elevations follow the points (or come with them); the connected ends stay on what they are on.
            SetRoute(ctx, run, PointsArg(a["points"]), a["levels"] as JsonArray);
            return null;
        });
        RegisterRouteEdits(r);
    }
}
