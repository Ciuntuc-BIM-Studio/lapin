using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Walls. An IfcWall is its axis (start → end, storey-local metres), its
/// height and its base offset over the storey; its section comes from its
/// type's layers, laid by the wall's own IfcMaterialLayerSetUsage with the
/// axis on the core. A wall without layers keeps its own thickness, centred.
/// </summary>
public static class Walls
{
    public const double DefaultHeight = 3.0;
    public const double DefaultThickness = 0.25;
    const double MinLength = 1e-3;

    public static Vec2 Start(Entity w) => Point(w.Attrs["start"]);
    public static Vec2 End(Entity w) => Point(w.Attrs["end"]);

    static Vec2 Point(JsonNode? n) =>
        n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : default;

    static JsonArray Array(Vec2 p) => [p.X, p.Y];

    public static JsonObject Attrs(Vec2 start, Vec2 end, double height, double baseOffset, double? thickness = null)
    {
        var o = new JsonObject { ["start"] = Array(start), ["end"] = Array(end), ["height"] = height, ["baseOffset"] = baseOffset };
        if (thickness != null) o["thickness"] = thickness;
        return o;
    }

    public static Entity? TypeOf(IModelView m, string wallId) =>
        m.RelatingOf(wallId, IfcClass.RelDefinesByType) is { Class: IfcClass.WallType } t ? t : null;

    /// <summary>The wall's own layer set usage, if it has one.</summary>
    public static Entity? UsageOf(IModelView m, string wallId) =>
        m.RelatingOf(wallId, IfcClass.RelAssociatesMaterial) is { Class: IfcClass.MaterialLayerSetUsage } u ? u : null;

    /// <summary>A new wall on the storey, of the type (the project's default when none), joined where it meets the walls already there.</summary>
    public static Entity Add(IEdit edit, string storeyId, Vec2 start, Vec2 end, double height, double baseOffset, Entity? type, JoinStyle joinStyle = JoinStyle.Butt)
    {
        var wall = AddUnjoined(edit, storeyId, start, end, height, baseOffset, type);
        Joins.Auto(edit, wall.Id, joinStyle);
        return edit.Model.Get(wall.Id)!;
    }

    public static Entity AddUnjoined(IEdit edit, string storeyId, Vec2 start, Vec2 end, double height, double baseOffset, Entity? type)
    {
        if ((end - start).Length < MinLength) throw new CommandException("A wall needs two different points.");
        if (!(height > 0)) throw new CommandException("A wall's height must be positive.");
        type ??= WallTypes.EnsureDefault(edit);
        var wall = new Entity(edit.NewId(), IfcClass.Wall, type.Name, Attrs(start, end, height, baseOffset), "STANDARD");
        edit.Add(wall);
        Spatial.Contain(edit, storeyId, wall.Id);
        SetType(edit, wall, type);
        return wall;
    }

    /// <summary>The wall becomes of <paramref name="type"/>: its usage points at the type's layers, its axis on their core.</summary>
    public static void SetType(IEdit edit, Entity wall, Entity type)
    {
        edit.Relate(IfcClass.RelDefinesByType, type.Id, wall.Id);
        LayOut(edit, wall, type);
    }

    /// <summary>
    /// Lays the type's layers on the wall: its own usage (made when it has
    /// none) points at the type's layer set, with the axis on the core,
    /// mirrored when the wall runs the negative way. A type without layers
    /// leaves the wall its own thickness.
    /// </summary>
    public static void LayOut(IEdit edit, Entity wall, Entity type)
    {
        var set = WallTypes.LayerSetOf(edit.Model, type.Id);
        var usage = UsageOf(edit.Model, wall.Id);
        if (set == null)
        {
            if (usage != null) edit.Remove(usage.Id);
            return;
        }
        var layers = Layers.Read(set);
        var negative = usage != null && Layers.ReadUsage(usage) is { Negative: true };
        var next = new LayerUsage(set.Id, negative, Layers.CoreOffset(layers, negative));
        if (usage == null)
        {
            var made = new Entity(edit.NewId(), IfcClass.MaterialLayerSetUsage, "", Layers.WriteUsage(next));
            edit.Add(made);
            edit.Relate(IfcClass.RelAssociatesMaterial, made.Id, wall.Id);
        }
        else edit.Replace(usage.With(attrs: Layers.WriteUsage(next)));
    }

    /// <summary>
    /// Flips the wall across its axis: its ends stay, its layers swap faces
    /// (the usage's sense turns and its offset changes sign), so the body is
    /// mirrored across the axis. A wall flips only that way.
    /// </summary>
    public static void Flip(IEdit edit, Entity wall, string axis = "y")
    {
        if (axis == "x") throw new CommandException("A wall flips across its axis (Flip Y): its ends stay where they are.");
        if (UsageOf(edit.Model, wall.Id) is not { } usage || Layers.ReadUsage(usage) is not { } u) return;
        edit.Replace(usage.With(attrs: Layers.WriteUsage(u with { Negative = !u.Negative, Offset = -u.Offset })));
    }

    /// <summary>A layer band of the wall's section: across the axis (left of it positive), its material.</summary>
    public readonly record struct Band(double From, double To, string? Material, string Name);

    /// <summary>The wall's section, first layer to last; one centred band for a wall without layers.</summary>
    public static IReadOnlyList<Band> Section(IModelView m, Entity wall)
    {
        if (UsageOf(m, wall.Id) is { } usageEntity && Layers.ReadUsage(usageEntity) is { } usage && m.Get(usage.ForLayerSet) is { } set)
        {
            var layers = Layers.Read(set);
            var bands = Layers.Bands(layers, usage);
            return layers.Select((l, i) => new Band(bands[i].From, bands[i].To, l.Material, l.Name ?? m.Get(l.Material)?.Name ?? $"Layer {i + 1}")).ToList();
        }
        var t = wall.NumOrNull("thickness") ?? DefaultThickness;
        return [new Band(-t / 2, t / 2, null, wall.Name)];
    }

    /// <summary>
    /// The wall's body: a prism per layer, cut at each end the way its joins
    /// say (the cut runs straight across the thickness, so each layer takes its
    /// share of it), standing on its storey.
    /// </summary>
    public static ElementGeometry? Geometry(Entity wall, IModelView m)
    {
        var start = Start(wall);
        var end = End(wall);
        if ((end - start).Length < MinLength) return new ElementGeometry([], Diagnostics: ["The wall has no length."]);
        var problems = new List<string>();
        var ends = Joins.EndsOf(m, wall, problems);
        if (ends[0] is { } s) start = s.Point;
        if (ends[1] is { } e) end = e.Point;
        var startCut = ends[0]?.Cut ?? EndCut.Square;
        var endCut = ends[1]?.Cut ?? EndCut.Square;
        var along = end - start;
        var length = along.Length;
        var dir = along.Normalized();
        var left = dir.Perp;
        var storey = Spatial.StoreyOf(m, wall.Id);
        var z = (storey?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        var height = wall.Num("height", DefaultHeight);

        var bands = Section(m, wall);
        var yMin = bands.Min(b => b.From);
        var yMax = bands.Max(b => b.To);
        // A cut at a face: Right at yMin, Left at yMax, straight in between.
        double At(EndCut c, double y) => yMax - yMin < 1e-12 ? c.Right : c.Right + (c.Left - c.Right) * (y - yMin) / (yMax - yMin);
        Vec2 P(double x, double y) => start + dir * x + left * y;

        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        foreach (var band in bands)
        {
            if (!(band.To - band.From > 1e-9)) continue;
            Vec2[] footprint =
            [
                P(-At(startCut, band.From), band.From), P(length + At(endCut, band.From), band.From),
                P(length + At(endCut, band.To), band.To), P(-At(startCut, band.To), band.To),
            ];
            if (Prism.ExtrudeFootprint(footprint, z, height, band.Name) is { } solid)
                parts.Add(new GeometryPart(band.Name, Materials.ColorOf(m, band.Material), solid, band.Material));
            plan.Add(new PlanItem("cut", true, footprint));
        }
        return new ElementGeometry(parts, plan, [start, end], problems.Count > 0 ? problems : null);
    }
}
