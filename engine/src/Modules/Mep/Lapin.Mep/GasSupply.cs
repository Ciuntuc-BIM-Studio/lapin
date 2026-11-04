using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// The gas supply in the street (decision 285): a main laid along a road, an
/// alignment, a drafted line or a wall (any element a section can follow,
/// <see cref="ISectionPaths"/>), at an offset from it and a depth under the
/// finished surface (the terrain's, <see cref="IGround"/>, else the storey's
/// level), in PE; and the service connection (branșament) from it to a
/// building's meter box: a tapping tee on the main, the service pipe at its
/// depth, rising into the box. Both are runs of a gas system like any other
/// (their points and their own elevations, <c>levels</c>, below the storey),
/// so the plan, the 3D, the IFC network and the lists take them; the cover
/// (<c>cover</c>), the offset, the element followed and the warning tape are
/// kept on them as data. A system for the street is medium pressure.
/// </summary>
public static class GasSupply
{
    /// <summary>The cover over a gas main and over a service pipe, metres (to the pipe's top), and the sizes they take by default.</summary>
    public const double MainDepth = 0.9, ServiceDepth = 0.8, MainSize = 0.063, ServiceSize = 0.032;

    /// <summary>How far over the pipe its warning tape is laid, metres.</summary>
    public const double TapeAbove = 0.3;

    /// <summary>The finished surface's height at a point (model height): the terrain where there is one, else the storey's level.</summary>
    public static Func<Vec2, double> Surface(IModelView m, IServices? services, double storeyZ)
    {
        var ground = services?.Get<IGround>();
        return ground == null ? _ => storeyZ : p => ground.ElevationAt(m, p.X, p.Y) ?? storeyZ;
    }

    /// <summary>A pipe's elevations over its storey, one per point, with <paramref name="depth"/> of cover over its top under the surface.</summary>
    public static JsonArray LevelsAtDepth(IReadOnlyList<Vec2> pts, Func<Vec2, double> surface, double storeyZ, double depth, double size) =>
        new(pts.Select(p => (JsonNode)Math.Round(surface(p) - depth - size / 2 - storeyZ, 4)).ToArray());

    /// <summary>The storey a street's pipes are drawn on: the given one, else the ground floor (the highest at or under 0), else the first.</summary>
    static Entity StoreyFor(IModelView m, string? id)
    {
        if (id != null) return Spatial.Require(m, id, IfcClass.BuildingStorey);
        var all = Spatial.Storeys(m).ToList();
        if (all.Count == 0) throw new CommandException("The project has no storey.");
        return all.Where(s => s.Num("elevation") <= 1e-6).OrderByDescending(s => s.Num("elevation")).FirstOrDefault() ?? all[0];
    }

    /// <summary>The system a main goes in: the one named, else the project's medium-pressure gas system, else a new one (PE, Ø63, medium pressure).</summary>
    static Entity StreetSystem(ICommandContext ctx, string? id)
    {
        if (id != null)
        {
            var s = Systems.Require(ctx.Model, id);
            if (Systems.Service(s) != Services.Gas) throw new CommandException($"„{s.Name}” is not a gas system.");
            return s;
        }
        var existing = Systems.All(ctx.Model).FirstOrDefault(s => Systems.Service(s) == Services.Gas && Systems.Of(s).Pressure == "medium");
        return existing ?? Systems.Create(ctx, Services.Gas, "Gas main", new JsonObject { ["pressure"] = "medium", ["material"] = "pe", ["size"] = MainSize, ["plane"] = "floor" });
    }

    /// <summary>The part of a polyline between two stations (from its start), the ends cut there.</summary>
    public static List<Vec2> Between(IReadOnlyList<Vec2> pts, double from, double to)
    {
        var length = Pipes.Length(pts);
        from = Math.Clamp(from, 0, length); to = Math.Clamp(to, 0, length);
        if (to - from < 1e-6) throw new CommandException("The main needs a length: its end station after its start.");
        var o = new List<Vec2> { Pipes.AtStation(pts, from).At };
        double at = 0;
        for (var i = 1; i < pts.Count; i++)
        {
            at += (pts[i] - pts[i - 1]).Length;
            if (at > from + 1e-6 && at < to - 1e-6) o.Add(pts[i]);
        }
        o.Add(Pipes.AtStation(pts, to).At);
        return SectionPaths.Clean(o);
    }

    /// <summary>
    /// A gas main along an element (<c>alongId</c>: a road, an alignment, a
    /// drafted line) or a drawn line (<c>points</c>), from station <c>from</c>
    /// to <c>to</c> (the element's own stations), <c>offset</c> to its left
    /// (negative: right), <c>depth</c> under the surface.
    /// </summary>
    public static Entity MainAlong(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        List<Vec2> path; double station0 = 0; string? along = null;
        if ((string?)a["alongId"] is { } aid)
        {
            var e = m.Get(aid) ?? throw new CommandException("There is no such element to follow.");
            var followed = SectionPaths.PathOfElement(m, ctx.Services, e) ?? throw new CommandException($"„{e.Name}” has no line a main can follow (a road, an alignment, a drafted line, a wall).");
            path = SectionPaths.Clean(followed.Points);
            station0 = followed.Station0;
            along = e.Id;
        }
        else path = SectionPaths.Clean(((a["points"] as JsonArray) ?? []).Select((p, i) => Args.Point(p, $"point {i + 1} of the main")));
        if (path.Count < 2) throw new CommandException("A main needs a line to follow: a road, an alignment, a drafted line, or two points.");
        var length = Pipes.Length(path);
        var from = (Json.Number(a["from"]) ?? station0) - station0;
        var to = (Json.Number(a["to"]) ?? station0 + length) - station0;
        if (from > 1e-6 || to < length - 1e-6) path = Between(path, from, to);
        var offset = Json.Number(a["offset"]) ?? 0;
        if (Math.Abs(offset) > 1e-9) path = Pipes.Offset(path, offset);
        var system = StreetSystem(ctx, (string?)a["systemId"]);
        var storey = StoreyFor(m, (string?)a["storeyId"]);
        var depth = Json.Number(a["depth"]) ?? MainDepth;
        var size = Json.Number(a["size"]) ?? Systems.Of(system).Size;
        var storeyZ = storey.Num("elevation");
        var args = new JsonObject
        {
            ["points"] = Runs.J(path), ["levels"] = LevelsAtDepth(path, Surface(m, ctx.Services, storeyZ), storeyZ, depth, size),
            ["size"] = size, ["material"] = (string?)a["material"] ?? "pe", ["cover"] = depth, ["offset"] = offset, ["warningTape"] = true,
            ["name"] = (string?)a["name"] is { Length: > 0 } n ? n : $"Gas main {Runs.Of(m, system.Id).Count() + 1}",
        };
        if (along != null) args["along"] = along;
        return Runs.Draw(ctx, system, storey.Id, args);
    }

    /// <summary>
    /// A service connection: a tapping tee on the main (<c>mainId</c>) where
    /// <c>at</c> falls (else nearest the box), the service pipe from its outlet
    /// at <c>depth</c> under the surface, square off the main and on to the
    /// box (<c>toNodeId</c>: a meter box, or any node with a free gas inlet),
    /// rising into it there. The main is split by the tee, as by any fitting.
    /// </summary>
    public static (Entity Tee, Entity Pipe) ServiceConnection(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        var main = m.Get((string?)a["mainId"] ?? "") ?? throw new CommandException("Which main? (mainId)");
        if (!Runs.IsRun(main) || Runs.SystemOf(m, main) is not { } system || Systems.Service(system) != Services.Gas) throw new CommandException("The service connection starts on a gas main.");
        var box = m.Get((string?)a["toNodeId"] ?? "") ?? throw new CommandException("Which meter box? (toNodeId)");
        if (!Nodes.IsNode(box)) throw new CommandException($"„{box.Name}” is not a node of a system.");
        var boxPort = Runs.PickPort(m, box, Services.Gas, "end", (string?)a["port"]);
        var boxAt = Nodes.Port(m, box, boxPort.Key)!.At;
        var pts = Runs.Points(main);
        var aim = a["at"] is JsonArray at ? Args.Point(at, "at") : boxAt.Xy;
        var (station, _) = Pipes.Nearest(pts, aim);
        // Not on a corner: a tee sits on a straight piece of the main.
        var (p, _, dir) = Pipes.AtStation(pts, station);
        var mainZ = Runs.ZAt(Runs.Centreline(m, main, Runs.SettingOf(m, main), out var mainStoreyZ), p);
        var mainStorey = Spatial.StoreyOf(m, main.Id) ?? throw new CommandException("The main is on no storey.");
        var tee = Nodes.Place(ctx, system, Roles.Require("tappingTee"), mainStorey.Id, new JsonObject
        {
            ["x"] = p.X, ["y"] = p.Y, ["rotation"] = Math.Round(Math.Atan2(dir.Y, dir.X) * 180 / Math.PI, 6), ["elevation"] = Math.Round(mainZ - mainStoreyZ, 6),
        });
        Runs.SplitAt(ctx, ctx.Model.Get(main.Id)!, ctx.Model.Get(tee.Id)!);
        // The route: square off the main toward the box, then along to it; or the points given, from the tee to the box.
        List<Vec2> route;
        if (a["points"] is JsonArray given && given.Count >= 1)
            route = SectionPaths.Clean([p, .. given.Select((q, i) => Args.Point(q, $"point {i + 1} of the service pipe")), boxAt.Xy]);
        else
        {
            var n = new Vec2(-dir.Y, dir.X);
            var foot = p + n * Vec2.Dot(boxAt.Xy - p, n);
            route = SectionPaths.Clean([p, foot, boxAt.Xy]);
        }
        if (route.Count < 2) route = [p, p + new Vec2(-dir.Y, dir.X) * 0.3];
        var storey = Spatial.StoreyOf(m, box.Id) ?? mainStorey;
        var storeyZ = storey.Num("elevation");
        var depth = Json.Number(a["depth"]) ?? ServiceDepth;
        var size = Json.Number(a["size"]) ?? ServiceSize;
        var count = Runs.Of(ctx.Model, system.Id).Count(r => r.Str("objectType") == "Service connection") + 1;
        var pipe = Runs.Draw(ctx, system, storey.Id, new JsonObject
        {
            ["points"] = Runs.J(route), ["levels"] = LevelsAtDepth(route, Surface(m, ctx.Services, storeyZ), storeyZ, depth, size),
            ["size"] = size, ["material"] = (string?)a["material"] ?? "pe", ["cover"] = depth, ["warningTape"] = true,
            ["name"] = (string?)a["name"] is { Length: > 0 } nm ? nm : $"Service connection {count}",
            ["start"] = new JsonObject { ["nodeId"] = tee.Id, ["port"] = "branch" },
            ["end"] = new JsonObject { ["nodeId"] = box.Id, ["port"] = boxPort.Key },
        });
        var attrs = pipe.CloneAttrs();
        attrs["objectType"] = "Service connection";
        ctx.Replace(pipe.With(attrs: attrs));
        return (ctx.Model.Get(tee.Id)!, ctx.Model.Get(pipe.Id)!);
    }

    public static void Register(IModuleRegistry r)
    {
        // A gas main along a road, an alignment or a drafted line (alongId), or a drawn line (points), at an offset and a depth.
        r.Command("mainAlong", "Gas main", (ctx, a) =>
        {
            var main = MainAlong(ctx, a);
            return new JsonObject { ["id"] = main.Id, ["systemId"] = main.Str("systemId") };
        });
        // A service connection from a gas main to a meter box: the tapping tee, the service pipe at its depth, the riser into the box.
        r.Command("serviceConnection", "Service connection", (ctx, a) =>
        {
            var (tee, pipe) = ServiceConnection(ctx, a);
            return new JsonObject { ["id"] = pipe.Id, ["teeId"] = tee.Id };
        });
        // A node joined to another system's network by a run of its own (a heating system's gas boiler to the gas installation).
        r.Command("joinSystem", "Join to system", (ctx, a) =>
        {
            var system = Systems.Require(ctx.Model, (string?)a["systemId"]);
            var node = ctx.Model.Get((string?)a["nodeId"] ?? "") ?? throw new CommandException("Which node?");
            if (!Nodes.IsNode(node)) throw new CommandException($"„{node.Name}” is not a node of a system.");
            var service = Systems.Service(system);
            if (!(Nodes.RoleOf(node)?.Ports.Any(p => Roles.Takes(p, service)) ?? false)) throw new CommandException($"„{node.Name}” has no port for {Services.Label(service).ToLowerInvariant()}.");
            var run = Layout.Connect(ctx, system, node) ?? throw new CommandException($"Nothing of „{system.Name}” to join on this storey, or its port is taken.");
            return new JsonObject { ["id"] = run.Id };
        });
    }
}
