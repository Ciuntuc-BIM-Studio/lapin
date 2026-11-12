using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis;

/// <summary>A user's load as read from its entity: its kind (area, line, point), case, plan points, height above its storey's level, value and direction.</summary>
public sealed record UserLoad(Entity Entity, string Kind, string? CaseId, IReadOnlyList<Vec2> Points, double Z, double Value, string Direction)
{
    /// <summary>The force's unit vector.</summary>
    public Vec3 Dir => UserLoads.Vector(Direction);
    /// <summary>The kind as IFC's structural actions name it: planar, curve, point.</summary>
    public string ActionKind => Kind switch { UserLoads.Area => "planar", UserLoads.Line => "curve", _ => "point" };
    /// <summary>The area or length the value is per (1 for a point load).</summary>
    public double Measure => Kind switch
    {
        UserLoads.Area => Math.Abs(Polygon2.SignedArea(Points)),
        UserLoads.Line => Points.Count == 2 ? (Points[1] - Points[0]).Length : 0,
        _ => 1,
    };
    /// <summary>The vertical force (kN, down positive): the projection on −Z.</summary>
    public double Vertical => Value * Measure * -Dir.Z;
}

/// <summary>
/// The user's loads (docs/plan-fem.md §8.3): <c>IfcStructuralPlanarAction</c> (an outline, kN/m²), <c>IfcStructuralCurveAction</c>
/// (a line, kN/m) and <c>IfcStructuralPointAction</c> (a point, kN), Kind annotation so the IFC export does not take them for
/// elements (it writes them in the structural analysis view), each on a storey, in a case (<c>caseId</c>), at <c>z</c> above
/// its storey's level, of a value above zero, in a direction (down, up, ±x, ±y). Drawn in plan by the module's marks (the
/// outline with a sparse hatch, the line with short arrows, an arrow; each with its value) and in 3D as lines and arrows.
/// </summary>
public static class UserLoads
{
    public const string AreaClass = "IfcStructuralPlanarAction", LineClass = "IfcStructuralCurveAction", PointClass = "IfcStructuralPointAction";
    public const string Area = "area", Line = "line", Point = "point";
    public static readonly string[] Classes = [AreaClass, LineClass, PointClass];
    public static readonly string[] Directions = ["down", "up", "+x", "-x", "+y", "-y"];
    public const string Color = "#c2410c";

    public static string KindOf(string cls) => cls switch { AreaClass => Area, LineClass => Line, _ => Point };

    public static Vec3 Vector(string? direction) => direction switch
    {
        "up" => new Vec3(0, 0, 1),
        "+x" => new Vec3(1, 0, 0),
        "-x" => new Vec3(-1, 0, 0),
        "+y" => new Vec3(0, 1, 0),
        "-y" => new Vec3(0, -1, 0),
        _ => new Vec3(0, 0, -1),
    };

    public static string Unit(string kind) => kind switch { Area => "kN/m²", Line => "kN/m", _ => "kN" };

    public static UserLoad? Read(Entity e)
    {
        if (!Classes.Contains(e.Class)) return null;
        var kind = KindOf(e.Class);
        IReadOnlyList<Vec2> pts = kind switch
        {
            Area => Args.StoredPoints(e.Attrs["points"]),
            Line => Args.TryPoint(e.Attrs["a"]) is { } a && Args.TryPoint(e.Attrs["b"]) is { } b ? [a, b] : [],
            _ => Args.TryPoint(e.Attrs["at"]) is { } p ? [p] : [],
        };
        return new UserLoad(e, kind, e.Str("caseId"), pts, e.Num("z"), e.Num("value"), e.Str("direction") is { } d && Directions.Contains(d) ? d : "down");
    }

    public static IEnumerable<UserLoad> All(IModelView m) => Classes.SelectMany(m.OfClass).Select(Read).OfType<UserLoad>();

    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));

    public static void Register(IModuleRegistry r)
    {
        IReadOnlyList<FieldDescriptor> Fields(string unit) =>
        [
            new("caseId", "Load case", "ref"),
            new("value", $"Value ({unit})", "number", Min: 0),
            new("direction", "Direction", "choice", Choices: Directions),
            new("z", "Height above the storey", "length"),
        ];
        r.Class(new(AreaClass, "Area load", Fields("kN/m²"), Color, Kind: "annotation"));
        r.Class(new(LineClass, "Line load", Fields("kN/m"), Color, Kind: "annotation"));
        r.Class(new(PointClass, "Point load", Fields("kN"), Color, Kind: "annotation"));
        foreach (var c in Classes) r.Geometry(c, Geometry);

        // {storeyId, points, value, caseId?, z?, direction?} → {id}; without a case, the variable case Q-A (made if missing).
        r.Command("addAreaLoad", "Area load", (ctx, a) =>
        {
            var pts = Args.Points(a);
            if (pts.Count < 3) throw new CommandException("An area load needs three points at least.");
            if (Math.Abs(Polygon2.SignedArea(pts)) < 1e-6) throw new CommandException("An area load's outline has no area.");
            return Add(ctx, a, AreaClass, "Area load", ("points", new JsonArray(pts.Select(p => (JsonNode)J(p)).ToArray())));
        });
        r.Command("addLineLoad", "Line load", (ctx, a) =>
        {
            var (p, q) = (Args.Point(a["a"], "line's start"), Args.Point(a["b"], "line's end"));
            if ((q - p).Length < 1e-3) throw new CommandException("A line load needs a length.");
            return Add(ctx, a, LineClass, "Line load", ("a", J(p)), ("b", J(q)));
        });
        r.Command("addPointLoad", "Point load", (ctx, a) =>
            Add(ctx, a, PointClass, "Point load", ("at", J(Args.Point(a["at"], "load's point")))));
    }

    /// <summary>A load of <paramref name="cls"/> at <paramref name="place"/> (its points), its value, height, direction and case from the command's arguments, on its storey.</summary>
    static JsonNode Add(ICommandContext ctx, JsonObject a, string cls, string name, params (string Key, JsonNode Value)[] place)
    {
        var attrs = new JsonObject();
        foreach (var (k, v) in place) attrs[k] = v;
        var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"], IfcClass.BuildingStorey);
        var value = Json.Number(a["value"]) ?? throw new CommandException("A load needs its value.");
        attrs["value"] = CheckValue(value);
        var z = Json.Number(a["z"]) ?? 0;
        if (!double.IsFinite(z) || Math.Abs(z) > 1000) throw new CommandException("A load's height above its storey is a number of metres.");
        attrs["z"] = z;
        var direction = (string?)a["direction"] ?? "down";
        if (!Directions.Contains(direction)) throw new CommandException($"A load's direction is {string.Join(", ", Directions)}.");
        attrs["direction"] = direction;
        // Its case by id, else by its code or name (a table's rows name it; docs/plan-user-extensibility.md §7.2), else the default.
        var caseId = (string?)a["caseId"] is { Length: > 0 } given
            ? LoadCases.Find(ctx.Model, given)?.Id ?? throw new CommandException("There is no such load case.")
            : (string?)a["caseName"] is { Length: > 0 } named
                ? (LoadCases.All(ctx.Model).FirstOrDefault(c => string.Equals(c.Code, named, StringComparison.OrdinalIgnoreCase)) ?? LoadCases.All(ctx.Model).FirstOrDefault(c => string.Equals(c.Name, named, StringComparison.OrdinalIgnoreCase)))?.Id
                  ?? throw new CommandException($"There is no load case „{named}”.")
                : LoadCases.DefaultForLoads(ctx);
        attrs["caseId"] = caseId;
        var e = new Entity(ctx.NewId(), cls, name, attrs);
        ctx.Add(e);
        Spatial.Contain(ctx, storey.Id, e.Id);
        return new JsonObject { ["id"] = e.Id };
    }

    public static double CheckValue(double v) =>
        double.IsFinite(v) && v > 0 && v < 1e6 ? v : throw new CommandException("A load's value is a number above zero.");

    /// <summary>What element.update has changed checked: a value above zero, a case of the project, a height that is a number.</summary>
    public static void Check(IModelView m, Entity e)
    {
        CheckValue(e.Num("value"));
        if (LoadCases.Find(m, e.Str("caseId")) == null) throw new CommandException("A load is in a load case of the project.");
        if (!double.IsFinite(e.Num("z"))) throw new CommandException("A load's height above its storey is a number of metres.");
    }

    /// <summary>A load's storey level (m).</summary>
    public static double Level(IModelView m, Entity e) => Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;

    /// <summary>A number as a load's label writes it: two decimals at most.</summary>
    public static string Format(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The arrows a load draws in 3D: each a shaft ending at <paramref name="at"/> in the force's direction, with two barbs.</summary>
    public static IEnumerable<IReadOnlyList<Vec3>> Arrow(Vec3 at, Vec3 dir, double length = 0.5)
    {
        var tail = at - dir * length;
        yield return [tail, at];
        var side = Math.Abs(dir.Z) > 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 0, 1);
        var barb = length * 0.25;
        yield return [at - dir * barb + side * (barb * 0.5), at, at - dir * barb - side * (barb * 0.5)];
    }

    /// <summary>A few points spread over an outline (a grid of up to 4 × 4 inside it, else its interior point), where its arrows stand.</summary>
    public static List<Vec2> Spread(IReadOnlyList<Vec2> outline)
    {
        var (lo, hi) = (new Vec2(outline.Min(p => p.X), outline.Min(p => p.Y)), new Vec2(outline.Max(p => p.X), outline.Max(p => p.Y)));
        var o = new List<Vec2>();
        const int n = 4;
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
            {
                var p = new Vec2(lo.X + (hi.X - lo.X) * (i + 0.5) / n, lo.Y + (hi.Y - lo.Y) * (j + 0.5) / n);
                if (Polygon2.Contains(outline, p)) o.Add(p);
            }
        if (o.Count == 0) o.Add(Polygon2.InteriorPoint(outline));
        return o;
    }

    /// <summary>A load's geometry: no body; its outline, line or point to pick in plan, and in 3D its outline or line at its level with arrows in the force's direction.</summary>
    static ElementGeometry? Geometry(Entity e, IModelView m, IServices services)
    {
        if (Read(e) is not { } l || l.Points.Count == 0) return new ElementGeometry([], Diagnostics: ["The load has no place."]);
        var z = Level(m, e) + l.Z;
        var dir = l.Dir;
        var lines = new List<IReadOnlyList<Vec3>>();
        var plan = new List<PlanItem>();
        switch (l.Kind)
        {
            case Area:
                lines.Add([.. l.Points.Select(p => p.At(z)), l.Points[0].At(z)]);
                foreach (var p in Spread(l.Points)) lines.AddRange(Arrow(p.At(z), dir));
                plan.Add(new PlanItem("hit", true, l.Points));
                break;
            case Line:
                lines.Add([l.Points[0].At(z), l.Points[1].At(z)]);
                for (var i = 0; i <= 4; i++) lines.AddRange(Arrow((l.Points[0] + (l.Points[1] - l.Points[0]) * (i / 4.0)).At(z), dir));
                plan.Add(new PlanItem("hit", false, l.Points));
                break;
            default:
                lines.AddRange(Arrow(l.Points[0].At(z), dir, 1.0));
                var c = l.Points[0];
                plan.Add(new PlanItem("hit", true, [c + new Vec2(-0.1, -0.1), c + new Vec2(0.1, -0.1), c + new Vec2(0.1, 0.1), c + new Vec2(-0.1, 0.1)]));
                break;
        }
        return new ElementGeometry([], plan, [.. l.Points], Lines: lines, LineColor: Color);
    }

    /// <summary>A load moves, turns, mirrors and copies as any element on a plan: its points (its direction stays as it was given).</summary>
    public sealed class Placement : IElementPlacement
    {
        public bool Handles(Entity e) => Classes.Contains(e.Class);

        public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
        {
            var attrs = e.CloneAttrs();
            foreach (var key in new[] { "a", "b", "at" })
                if (Args.TryPoint(attrs[key]) is { } p) attrs[key] = J(t.Point(p));
            if (attrs["points"] is JsonArray)
                attrs["points"] = new JsonArray(Args.StoredPoints(attrs["points"]).Select(p => (JsonNode)J(t.Point(p))).ToArray());
            ctx.Replace(e.With(attrs: attrs));
        }
    }
}
