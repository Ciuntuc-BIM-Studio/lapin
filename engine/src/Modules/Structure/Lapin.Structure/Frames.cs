using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// A section a column or a beam carries, metres, centred on its box: a
/// rectangle (width × depth), a circle (its diameter), or a steel profile I
/// (and H), U, T or L: its depth, its flanges' width, the web's and the
/// flanges' thickness. A profile can come from the catalogue by its name
/// (IPE 300, HEA 240, UPN 200, L 100×100×10).
/// </summary>
public sealed record Section(string Kind, double Width, double Depth, double Web = 0, double Flange = 0, string? Name = null)
{
    public static readonly string[] Profiles = ["I", "U", "T", "L"];

    public static Section Rectangle(double width, double depth) => new("rectangle", width, depth);
    public static Section Circle(double diameter) => new("circle", diameter, diameter);

    public bool IsProfile => Profiles.Contains(Kind);

    public double Area => Kind == "circle" ? Math.PI * Width * Width / 4 : Math.Abs(Polygon2.SignedArea(Outline()));

    /// <summary>The outline, counter-clockwise, about the box's centre (a circle as 24 sides; a profile's web vertical, an L and a U open to the right).</summary>
    public IReadOnlyList<Vec2> Outline()
    {
        double w = Width / 2, d = Depth / 2, tw = Web, tf = Flange;
        return Kind switch
        {
            "circle" => Enumerable.Range(0, 24).Select(i => new Vec2(w * Math.Cos(i * Math.PI / 12), w * Math.Sin(i * Math.PI / 12))).ToList(),
            "I" => [new(-w, -d), new(w, -d), new(w, -d + tf), new(tw / 2, -d + tf), new(tw / 2, d - tf), new(w, d - tf), new(w, d), new(-w, d), new(-w, d - tf), new(-tw / 2, d - tf), new(-tw / 2, -d + tf), new(-w, -d + tf)],
            "U" => [new(-w, -d), new(w, -d), new(w, -d + tf), new(-w + tw, -d + tf), new(-w + tw, d - tf), new(w, d - tf), new(w, d), new(-w, d)],
            "T" => [new(-tw / 2, -d), new(tw / 2, -d), new(tw / 2, d - tf), new(w, d - tf), new(w, d), new(-w, d), new(-w, d - tf), new(-tw / 2, d - tf)],
            "L" => [new(-w, -d), new(w, -d), new(w, -d + tf), new(-w + tw, -d + tf), new(-w + tw, d), new(-w, d)],
            _ => [new(-w, -d), new(w, -d), new(w, d), new(-w, d)],
        };
    }

    /// <summary>How a type list shows it: 30×30, Ø40, I/H 300×290, IPE 300.</summary>
    public string Label => Name ?? Kind switch
    {
        "circle" => $"Ø{Width * 1000:0}",
        "rectangle" => $"{Width * 100:0}×{Depth * 100:0}",
        "I" => $"I/H {Depth * 1000:0}×{Width * 1000:0}",
        "L" => $"L {Width * 1000:0}×{Depth * 1000:0}×{Flange * 1000:0}",
        _ => $"{Kind} {Depth * 1000:0}×{Width * 1000:0}",
    };

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["kind"] = Kind, ["width"] = Width, ["depth"] = Depth };
        if (IsProfile) { o["web"] = Web; o["flange"] = Flange; }
        if (Name != null) o["name"] = Name;
        return o;
    }

    /// <summary>The steel profiles a type can be made of by name (mm: depth, width, web, flange).</summary>
    public static readonly IReadOnlyDictionary<string, Section> Catalogue = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase)
    {
        ["IPE 200"] = P("I", 200, 100, 5.6, 8.5, "IPE 200"), ["IPE 240"] = P("I", 240, 120, 6.2, 9.8, "IPE 240"), ["IPE 300"] = P("I", 300, 150, 7.1, 10.7, "IPE 300"),
        ["IPE 360"] = P("I", 360, 170, 8, 12.7, "IPE 360"), ["IPE 400"] = P("I", 400, 180, 8.6, 13.5, "IPE 400"),
        ["HEA 200"] = P("I", 190, 200, 6.5, 10, "HEA 200"), ["HEA 240"] = P("I", 230, 240, 7.5, 12, "HEA 240"), ["HEA 300"] = P("I", 290, 300, 8.5, 14, "HEA 300"),
        ["HEB 200"] = P("I", 200, 200, 9, 15, "HEB 200"), ["HEB 300"] = P("I", 300, 300, 11, 19, "HEB 300"),
        ["UPN 200"] = P("U", 200, 75, 8.5, 11.5, "UPN 200"), ["UPN 300"] = P("U", 300, 100, 10, 16, "UPN 300"),
        ["L 80×80×8"] = P("L", 80, 80, 8, 8, "L 80×80×8"), ["L 100×100×10"] = P("L", 100, 100, 10, 10, "L 100×100×10"),
    };

    static Section P(string kind, double depth, double width, double web, double flange, string name) => new(kind, width / 1000, depth / 1000, web / 1000, flange / 1000, name);

    public static Section? From(JsonNode? n)
    {
        if (n is JsonValue v && v.TryGetValue<string>(out var named)) return Catalogue.GetValueOrDefault(named.Replace("x", "×"));
        if (n is not JsonObject o) return null;
        if ((string?)o["name"] is { } nm && Catalogue.TryGetValue(nm.Replace("x", "×"), out var c) && o["width"] == null) return c;
        var kind = (string?)o["kind"] ?? "rectangle";
        if (kind == "H") kind = "I";
        var w = Json.Number(o["width"]) ?? Json.Number(o["diameter"]) ?? 0;
        var d = kind == "circle" ? w : Json.Number(o["depth"]) ?? 0;
        return new Section(kind, w, d, Json.Number(o["web"]) ?? 0, Json.Number(o["flange"]) ?? 0, (string?)o["name"]);
    }

    public static Section Parse(JsonNode? n)
    {
        var s = From(n) ?? throw new CommandException("A section is a rectangle (width, depth), a circle (diameter), a profile I, U, T or L (depth, width, web, flange), or a catalogue name (IPE 300).");
        if (s.Kind is not ("rectangle" or "circle") && !s.IsProfile) throw new CommandException("A section is a rectangle, a circle, or a profile I, U, T or L.");
        if (!(s.Width > 0 && s.Depth > 0)) throw new CommandException("A section needs a positive size.");
        if (s.IsProfile && !(s.Web > 0 && s.Flange > 0 && s.Web < s.Width && 2 * s.Flange < s.Depth)) throw new CommandException("A profile's web and flanges must be thinner than it.");
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

    /// <summary>A footprint turned by <paramref name="rotation"/> (degrees, as the attribute holds it) and moved to <paramref name="at"/>.</summary>
    public static Vec2[] Placed(IEnumerable<Vec2> outline, Vec2 at, double rotation)
    {
        var (c, s) = (Math.Cos(rotation * Math.PI / 180), Math.Sin(rotation * Math.PI / 180));
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
