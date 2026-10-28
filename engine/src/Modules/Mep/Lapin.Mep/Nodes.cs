using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>A port of a placed node, in project space: where it is, which way it faces, what it takes, whether something is on it.</summary>
public sealed record NodePort(string NodeId, RolePort Port, Vec3 At, Vec3 Dir);

/// <summary>
/// A node of a system: a source, an endpoint, a fitting on a run, a rise or
/// drop, a cap (<see cref="Roles"/>), placed on a storey in plan with a
/// rotation and an elevation (its bottom over the storey). Its IFC class is
/// its role's; <c>role</c> and <c>systemId</c> say the rest. Its body is a
/// box of its size with a stub at each port, until a library element is
/// mapped to it; its plan symbol is the role's.
/// </summary>
public static class Nodes
{
    public static readonly string[] Classes = [.. Roles.All.Select(r => r.IfcClass).Distinct()];

    public static Role? RoleOf(Entity e) => Roles.Get(e.Str("role"));
    public static bool IsNode(Entity e) => e.Attrs.ContainsKey("role") && e.Attrs.ContainsKey("systemId") && RoleOf(e) != null;
    public static IEnumerable<Entity> Of(IModelView m, string systemId) => Classes.SelectMany(m.OfClass).Where(e => IsNode(e) && e.Str("systemId") == systemId);
    public static IEnumerable<Entity> All(IModelView m) => Classes.SelectMany(m.OfClass).Where(IsNode);

    public static Vec2 Position(Entity e) => new(e.Num("x"), e.Num("y"));
    public static double Rotation(Entity e) => e.Num("rotation") * Math.PI / 180;
    public static Vec3 Size(Entity e, Role role) => new(e.NumOrNull("width") ?? role.Size.X, e.NumOrNull("depth") ?? role.Size.Y, e.NumOrNull("height") ?? role.Size.Z);
    public static double Elevation(Entity e, Role role) => e.NumOrNull("elevation") ?? role.Elevation;

    /// <summary>The storey's elevation under the node (0 when it stands on none).</summary>
    public static double StoreyZ(IModelView m, Entity e) => Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;

    /// <summary>A node's own point into project space: turned by its rotation about its origin, raised by its elevation over its storey.</summary>
    public static Vec3 ToWorld(IModelView m, Entity e, Role role, Vec3 local)
    {
        var p = Position(e);
        var (c, s) = (Math.Cos(Rotation(e)), Math.Sin(Rotation(e)));
        return new Vec3(p.X + local.X * c - local.Y * s, p.Y + local.X * s + local.Y * c, StoreyZ(m, e) + Elevation(e, role) + local.Z);
    }

    static Vec3 TurnDir(Entity e, Vec3 d)
    {
        var (c, s) = (Math.Cos(Rotation(e)), Math.Sin(Rotation(e)));
        return new Vec3(d.X * c - d.Y * s, d.X * s + d.Y * c, d.Z);
    }

    /// <summary>The node's ports in project space. A rise's top port stands at the elevation it goes to.</summary>
    public static IReadOnlyList<NodePort> Ports(IModelView m, Entity e)
    {
        var role = RoleOf(e);
        if (role == null) return [];
        var o = new List<NodePort>();
        foreach (var p in role.Ports)
        {
            var at = ToWorld(m, e, role, p.At);
            if (role.IsRise && p.Key == "top") at = new Vec3(at.X, at.Y, RiseTop(m, e));
            o.Add(new NodePort(e.Id, p, at, TurnDir(e, p.Dir)));
        }
        return o;
    }

    /// <summary>Where a rise goes to: its own elevation on the storey it names, or a plain elevation over its storey.</summary>
    public static double RiseTop(IModelView m, Entity e)
    {
        if (e.Str("toStoreyId") is { } sid && m.Get(sid) is { Class: IfcClass.BuildingStorey } s) return s.Num("elevation") + (e.NumOrNull("toElevation") ?? Elevation(e, Roles.Require("rise")));
        return StoreyZ(m, e) + (e.NumOrNull("toElevation") ?? Elevation(e, Roles.Require("rise")) + 2.0);
    }

    public static NodePort? Port(IModelView m, Entity e, string key) => Ports(m, e).FirstOrDefault(p => p.Port.Key == key);

    /// <summary>The colour of a role's body.</summary>
    public static string Color(Role role) => role.Kind switch { "source" => "#8a8f98", "inline" => "#b0803a", "rise" => "#6f7f99", _ => "#dcdcdc" };

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var role = RoleOf(e);
        if (role == null) return new ElementGeometry([], Diagnostics: ["A node needs its role."]);
        var parts = new List<GeometryPart>();
        var size = Size(e, role);
        if (role.IsRise)
        {
            // The riser: a vertical tube between its bottom and the elevation it goes to.
            var system = m.Get(e.Str("systemId") ?? "");
            var radius = (system != null ? Systems.Of(system).Size : 0.02) / 2;
            var bottom = ToWorld(m, e, role, Vec3.Zero);
            var top = new Vec3(bottom.X, bottom.Y, RiseTop(m, e));
            if (Pipes.Tube(top.Z > bottom.Z ? bottom : top, top.Z > bottom.Z ? top : bottom, radius, 12, "riser") is { } tube)
                parts.Add(new GeometryPart("Riser", "#6f7f99", tube, null, "riser"));
        }
        else
        {
            // Its schematic volume, turned and raised as the node is.
            foreach (var piece in Bodies.Of(m, e, role, size)) parts.Add(new GeometryPart(piece.Label, piece.Color, piece.Solid, null, piece.Key));
            // A stub at each port, so the port shows and a run has something to meet.
            foreach (var p in Ports(m, e))
                if (Pipes.Tube(p.At, p.At + p.Dir * 0.05, Math.Max(0.008, (p.Port.Size > 0 ? p.Port.Size : 0.02) / 2), 8, "port") is { } stub)
                    parts.Add(new GeometryPart($"Port {p.Port.Key}", "#5a5a5a", stub, null, "port:" + p.Port.Key));
        }
        // The plan snaps to the node and to its ports, where a run starts or ends.
        return new ElementGeometry(parts, Symbol(m, e, role, size), [Position(e), .. Ports(m, e).Select(p => p.At.Xy)]);
    }

    /// <summary>The role's symbol on the plan (the engineering symbol, not the box): what the schematic shows.</summary>
    static List<PlanItem> Symbol(IModelView m, Entity e, Role role, Vec3 size)
    {
        var o = new List<PlanItem>();
        Vec2 P(double x, double y) => ToWorld(m, e, role, new Vec3(x, y, 0)).Xy;
        double w = size.X / 2, d = size.Y / 2;
        Vec2[] box = [P(-w, -d), P(w, -d), P(w, d), P(-w, d)];
        o.Add(new PlanItem("hit", true, box));
        List<Vec2> Circle(double r, int n = 24) => Enumerable.Range(0, n).Select(k => P(r * Math.Cos(2 * Math.PI * k / n), r * Math.Sin(2 * Math.PI * k / n))).ToList();
        switch (role.Symbol)
        {
            case "rise":
            {
                // ISO 4067: a ring; a dot for a rise (the pipe comes up to the eye), a cross for a drop.
                var up = RiseTop(m, e) > ToWorld(m, e, role, Vec3.Zero).Z;
                o.Add(new PlanItem("symbol", true, Circle(0.06)));
                if (up) o.Add(new PlanItem("heavy", true, Circle(0.015, 12)));
                else { o.Add(new PlanItem("symbol", false, [P(-0.042, -0.042), P(0.042, 0.042)])); o.Add(new PlanItem("symbol", false, [P(-0.042, 0.042), P(0.042, -0.042)])); }
                break;
            }
            case "valve":
                o.Add(new PlanItem("symbol", true, [P(-w, -d), P(w, d), P(w, -d), P(-w, d)]));
                break;
            case "meter":
                o.Add(new PlanItem("symbol", true, Circle(Math.Min(w, d))));
                o.Add(new PlanItem("symbol", false, [P(-w, 0), P(-Math.Min(w, d), 0)]));
                o.Add(new PlanItem("symbol", false, [P(Math.Min(w, d), 0), P(w, 0)]));
                break;
            case "boiler":
                o.Add(new PlanItem("symbol", true, box));
                o.Add(new PlanItem("symbol", false, [P(-w, -d), P(w, d)]));
                break;
            case "radiator":
                o.Add(new PlanItem("symbol", true, box));
                o.Add(new PlanItem("symbol", false, [P(-w, 0), P(w, 0)]));
                break;
            case "outlet":
                o.Add(new PlanItem("symbol", true, Circle(0.05)));
                o.Add(new PlanItem("symbol", false, [P(0, 0.05), P(0, 0.12)]));
                o.Add(new PlanItem("symbol", false, [P(-0.05, 0.12), P(0.05, 0.12)]));
                break;
            case "light":
                o.Add(new PlanItem("symbol", true, Circle(0.06)));
                o.Add(new PlanItem("symbol", false, [P(-0.042, -0.042), P(0.042, 0.042)]));
                o.Add(new PlanItem("symbol", false, [P(-0.042, 0.042), P(0.042, -0.042)]));
                break;
            case "switch":
                o.Add(new PlanItem("symbol", true, Circle(0.04)));
                o.Add(new PlanItem("symbol", false, [P(0.028, 0.028), P(0.1, 0.1)]));
                o.Add(new PlanItem("symbol", false, [P(0.1, 0.1), P(0.14, 0.07)]));
                break;
            case "fan":
                o.Add(new PlanItem("symbol", true, Circle(Math.Min(w, d))));
                o.Add(new PlanItem("symbol", true, [P(-0.05, -0.06), P(0.08, 0), P(-0.05, 0.06)]));
                break;
            case "diffuser":
                o.Add(new PlanItem("symbol", true, box));
                o.Add(new PlanItem("symbol", false, [P(-w, -d), P(w, d)]));
                o.Add(new PlanItem("symbol", false, [P(-w, d), P(w, -d)]));
                break;
            case "stack":
                o.Add(new PlanItem("heavy", true, Circle(0.055)));
                break;
            case "cap":
                o.Add(new PlanItem("heavy", false, [P(0, -0.04), P(0, 0.04)]));
                break;
            default:
                o.Add(new PlanItem("symbol", true, box));
                break;
        }
        // The ports, as short ticks, so a run finds them by eye.
        foreach (var p in Ports(m, e))
            if (Math.Abs(p.Dir.Z) < 0.9) o.Add(new PlanItem("symbol", false, [p.At.Xy, p.At.Xy + p.Dir.Xy * 0.06]));
        return o;
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        var role = RoleOf(e);
        if (role == null) yield break;
        var list = new List<Quantity> { new("Count", 1, "COUNT") };
        if (role.Duty > 0 || e.NumOrNull("duty") != null) list.Add(new("Duty", e.NumOrNull("duty") ?? role.Duty, "COUNT", role.DutyUnit));
        if (role.IsRise) list.Add(new("Length", Math.Abs(RiseTop(m, e) - (StoreyZ(m, e) + Elevation(e, role))), "LENGTH"));
        yield return new QuantitySet("Lapin_Mep", list);
    }

    /// <summary>A node placed: on the storey, in the system, with the role's defaults and what the command gives over them.</summary>
    public static Entity Place(ICommandContext ctx, Entity system, Role role, string storeyId, JsonObject a)
    {
        var service = Systems.Service(system);
        if (!role.Ports.Any(p => Roles.Takes(p, service))) throw new CommandException($"A {role.Label.ToLowerInvariant()} has no port for {Services.Label(service).ToLowerInvariant()}.");
        var attrs = new JsonObject
        {
            ["role"] = role.Id, ["systemId"] = system.Id,
            ["x"] = Json.Number(a["x"]) ?? throw new CommandException("Where? (x, y)"), ["y"] = Json.Number(a["y"]) ?? throw new CommandException("Where? (x, y)"),
            ["rotation"] = Json.Number(a["rotation"]) ?? 0,
        };
        foreach (var k in new[] { "elevation", "width", "depth", "height", "duty", "toElevation" }) if (Json.Number(a[k]) is { } v) attrs[k] = v;
        if ((string?)a["toStoreyId"] is { } to) attrs["toStoreyId"] = Spatial.Require(ctx.Model, to, IfcClass.BuildingStorey).Id;
        if ((string?)a["name"] is { Length: > 0 } nm) attrs["name"] = nm;
        var count = Of(ctx.Model, system.Id).Count(n => n.Str("role") == role.Id) + 1;
        var node = new Entity(ctx.NewId(), role.IfcClass, (string?)a["name"] is { Length: > 0 } name ? name : $"{role.Label} {count}", attrs, role.Predefined);
        ctx.Add(node);
        Spatial.Contain(ctx, storeyId, node.Id);
        return node;
    }

    /// <summary>The fields every node shows (the module registers the classes: a class may hold nodes and runs both).</summary>
    public static readonly FieldDescriptor[] Fields =
    [
        new("role", "Role", "choice", Choices: [.. Roles.All.Select(x => x.Id)], ReadOnly: true),
        new("x", "X", "length"), new("y", "Y", "length"), new("rotation", "Rotation", "number", Min: -360, Max: 360),
        new("elevation", "Elevation", "length"), new("width", "Width", "length", Min: 0.01), new("depth", "Depth", "length", Min: 0.01), new("height", "Height", "length", Min: 0.01),
        new("duty", "Duty", "number", Min: 0), new("toElevation", "Goes to (elevation)", "length"),
    ];

    public static string Label(string cls) => Roles.All.First(x => x.IfcClass == cls).Label;
}

/// <summary>
/// Nodes and runs move, turn and mirror in plan as any element; a node's
/// connected runs follow it, and their points are written where they are
/// drawn (<see cref="Runs.Refollow"/>), so their handles stay on them. Runs
/// carried by the same transformation move by themselves.
/// </summary>
sealed class MepPlacement : IElementPlacement, IElementUpdate
{
    public bool Handles(Entity e) => Nodes.IsNode(e) || Runs.IsRun(e);

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        var attrs = e.CloneAttrs();
        if (Nodes.IsNode(e))
        {
            var p = t.Point(Nodes.Position(e));
            attrs["x"] = p.X; attrs["y"] = p.Y;
            attrs["rotation"] = t.Angle(Nodes.Rotation(e)) * 180 / Math.PI;
            ctx.Replace(e.With(attrs: attrs));
            Runs.RefollowOn(ctx, e.Id, moving);
            return;
        }
        attrs["points"] = Runs.J(Runs.Points(e).Select(t.Point).ToList());
        ctx.Replace(e.With(attrs: attrs));
        // Its ends on what stays: followed (an end on what moves with it moves by itself); the branches off it that stay follow it.
        var onMoving = new[] { "start", "end" }.Any(end => Runs.EndOf(ctx.Model, e, end) is { } x && moving.Contains(x.NodeId ?? x.RunId ?? ""));
        if (!onMoving) Runs.Refollow(ctx, e.Id, moving);
        else foreach (var (branch, _) in Runs.BranchesOf(ctx.Model, e.Id).ToList()) if (!moving.Contains(branch.Id)) Runs.Refollow(ctx, branch.Id, moving);
    }

    public void Updated(ICommandContext ctx, Entity before, Entity after)
    {
        if (Nodes.IsNode(after)) Runs.RefollowOn(ctx, after.Id);
        else if (Runs.IsRun(after)) Runs.Refollow(ctx, after.Id);
    }
}
