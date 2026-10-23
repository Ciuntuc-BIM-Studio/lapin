using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>A section a column or a beam carries: a rectangle (width × depth) or a circle (its diameter), metres, centred.</summary>
public sealed record Section(string Kind, double Width, double Depth)
{
    public static Section Rectangle(double width, double depth) => new("rectangle", width, depth);
    public static Section Circle(double diameter) => new("circle", diameter, diameter);

    public double Area => Kind == "circle" ? Math.PI * Width * Width / 4 : Width * Depth;

    /// <summary>The outline, counter-clockwise, about the origin (a circle as 24 sides).</summary>
    public IReadOnlyList<Vec2> Outline()
    {
        if (Kind == "circle")
            return Enumerable.Range(0, 24).Select(i => new Vec2(Width / 2 * Math.Cos(i * Math.PI / 12), Width / 2 * Math.Sin(i * Math.PI / 12))).ToList();
        return [new(-Width / 2, -Depth / 2), new(Width / 2, -Depth / 2), new(Width / 2, Depth / 2), new(-Width / 2, Depth / 2)];
    }

    public JsonObject ToJson() => new() { ["kind"] = Kind, ["width"] = Width, ["depth"] = Depth };

    public static Section? From(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        var kind = (string?)o["kind"] ?? "rectangle";
        var w = Json.Number(o["width"]) ?? Json.Number(o["diameter"]) ?? 0;
        var d = kind == "circle" ? w : Json.Number(o["depth"]) ?? 0;
        return new Section(kind, w, d);
    }

    public static Section Parse(JsonNode? n)
    {
        var s = From(n) ?? throw new CommandException("A section is a rectangle (width, depth) or a circle (diameter).");
        if (s.Kind is not ("rectangle" or "circle")) throw new CommandException("A section is a rectangle or a circle.");
        if (!(s.Width > 0 && s.Depth > 0)) throw new CommandException("A section needs a positive size.");
        return s;
    }
}

/// <summary>What the structure's elements share: the storey they stand on, points and axes in their attributes.</summary>
static class Place
{
    public static Vec2 Point(Entity e, string key = "at") =>
        e.Attrs[key] is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : default;

    public static JsonArray Json2(Vec2 p) => new(p.X, p.Y);

    public static double Elevation(IModelView m, Entity e) => Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;

    public static double StoreyHeight(IModelView m, Entity e) => Spatial.StoreyOf(m, e.Id)?.Num("height", 3) ?? 3;

    /// <summary>A footprint turned by <paramref name="rotation"/> (radians) and moved to <paramref name="at"/>.</summary>
    public static Vec2[] Placed(IEnumerable<Vec2> outline, Vec2 at, double rotation)
    {
        var (c, s) = (Math.Cos(rotation), Math.Sin(rotation));
        return outline.Select(p => new Vec2(at.X + p.X * c - p.Y * s, at.Y + p.X * s + p.Y * c)).ToArray();
    }

    public static Entity Storey(ICommandContext ctx, JsonObject a) =>
        Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);

    public static Vec2 P(JsonObject a, string x, string y) =>
        new(Json.Number(a[x]) ?? throw new CommandException($"Missing {x}."), Json.Number(a[y]) ?? throw new CommandException($"Missing {y}."));

    public static Entity? TypeOf(IModelView m, string id) => m.RelatingOf(id, IfcClass.RelDefinesByType);

    /// <summary>The type named in the args, of the class; else the project's first of the class, else <paramref name="make"/>'s.</summary>
    public static Entity TypeFor(ICommandContext ctx, JsonObject a, string typeClass, Func<Entity> make, Func<Entity, bool>? fits = null)
    {
        if ((string?)a["typeId"] is { } id)
            return ctx.Model.Get(id) is { } t && t.Class == typeClass && (fits == null || fits(t)) ? t : throw new CommandException($"There is no such {typeClass}.");
        return ctx.Model.OfClass(typeClass).FirstOrDefault(t => fits == null || fits(t)) ?? make();
    }

    /// <summary>The part's solid less what cuts it by priority.</summary>
    public static List<GeometryPart> CutByPriority(Entity e, IModelView m, IServices? services, List<GeometryPart> parts, List<string> problems)
    {
        if (parts.Count == 0 || services?.Get<ICutters>() is not { } cutters) return parts;
        var bounds = parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        var all = (new Vec3(bounds.Min(b => b.Min.X), bounds.Min(b => b.Min.Y), bounds.Min(b => b.Min.Z)), new Vec3(bounds.Max(b => b.Max.X), bounds.Max(b => b.Max.Y), bounds.Max(b => b.Max.Z)));
        var by = cutters.For(e, m, all);
        if (by.Count == 0) return parts;
        if (services.Get<ISolidBooleans>() is not { } booleans) { problems.Add("Cut priorities need the boolean library (manifold), missing here."); return parts; }
        var o = new List<GeometryPart>();
        foreach (var part in parts)
        {
            try { if (booleans.Difference(part.Solid, by, part.Solid.Tag) is { } cut) o.Add(part with { Solid = cut }); }
            catch (InvalidOperationException ex) { problems.Add($"{part.Name}: could not be cut ({ex.Message})."); o.Add(part); }
        }
        return o;
    }
}
