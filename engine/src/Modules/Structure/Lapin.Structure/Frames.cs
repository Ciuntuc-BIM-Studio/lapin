using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// A section a column, a beam or a steel member carries, metres, centred on its box: a rectangle (width × depth), a circle
/// (its diameter), a hot-rolled steel profile I (and H), U, T or L (its depth, its flanges' width, the web's and the flanges'
/// thickness), a hollow section RHS (width × depth, its wall <see cref="Web"/>) or CHS (its diameter, its wall), or a
/// cold-formed C, Z or Σ (SIGMA): depth, flanges, the sheet's thickness <see cref="Web"/> and the lips <see cref="Lip"/>,
/// bent sharp (decision 268: generic shapes with dimensions, no manufacturer's catalogue). A profile can come from the
/// catalogue by its name (IPE 300, HEA 240, UPN 200, L 100×100×10, RHS 150×100×6, CHS 114.3×5, C 200×65×2).
/// </summary>
public sealed record Section(string Kind, double Width, double Depth, double Web = 0, double Flange = 0, string? Name = null, double Lip = 0)
{
    public static readonly string[] Profiles = ["I", "U", "T", "L", "RHS", "CHS", "C", "Z", "SIGMA"];
    /// <summary>The thin-walled kinds: one sheet thickness (<see cref="Web"/>, <see cref="Flange"/> the same).</summary>
    public static readonly string[] ThinWalled = ["RHS", "CHS", "C", "Z", "SIGMA"];

    public static Section Rectangle(double width, double depth) => new("rectangle", width, depth);
    public static Section Circle(double diameter) => new("circle", diameter, diameter);

    public bool IsProfile => Profiles.Contains(Kind);
    public bool IsHollow => Kind is "RHS" or "CHS";

    /// <summary>The section's area: a circle's and a CHS's exactly (not its chords'), any other its outline's less its holes'.</summary>
    public double Area => Kind == "circle" ? Math.PI * Width * Width / 4
        : Kind == "CHS" ? Math.PI / 4 * (Width * Width - (Width - 2 * Web) * (Width - 2 * Web))
        : Math.Abs(Polygon2.SignedArea(Outline())) - Holes().Sum(h => Math.Abs(Polygon2.SignedArea(h)));

    /// <summary>The outline, counter-clockwise, about the box's centre (a circle by the chord rule; a profile's web vertical, an L, a U and a C open to the right; a hollow section's outside).</summary>
    public IReadOnlyList<Vec2> Outline()
    {
        double w = Width / 2, d = Depth / 2, tw = Web, tf = Flange;
        return Kind switch
        {
            "circle" or "CHS" => Ring(w),
            "I" => [new(-w, -d), new(w, -d), new(w, -d + tf), new(tw / 2, -d + tf), new(tw / 2, d - tf), new(w, d - tf), new(w, d), new(-w, d), new(-w, d - tf), new(-tw / 2, d - tf), new(-tw / 2, -d + tf), new(-w, -d + tf)],
            "U" => [new(-w, -d), new(w, -d), new(w, -d + tf), new(-w + tw, -d + tf), new(-w + tw, d - tf), new(w, d - tf), new(w, d), new(-w, d)],
            "T" => [new(-tw / 2, -d), new(tw / 2, -d), new(tw / 2, d - tf), new(w, d - tf), new(w, d), new(-w, d), new(-w, d - tf), new(-tw / 2, d - tf)],
            "L" => [new(-w, -d), new(w, -d), new(w, -d + tf), new(-w + tw, -d + tf), new(-w + tw, d), new(-w, d)],
            "C" or "Z" or "SIGMA" => Polygon2.EnsureCcw(Sheet(Centreline(), tw)),
            _ => [new(-w, -d), new(w, -d), new(w, d), new(-w, d)],
        };
    }

    /// <summary>A hollow section's inside, clockwise; none for the others.</summary>
    public IReadOnlyList<IReadOnlyList<Vec2>> Holes()
    {
        double w = Width / 2, d = Depth / 2, t = Web;
        return Kind switch
        {
            "RHS" when t > 0 && t < w && t < d => [new Vec2[] { new(-w + t, -d + t), new(-w + t, d - t), new(w - t, d - t), new(w - t, -d + t) }],
            "CHS" when t > 0 && t < w => [Ring(w - t).AsEnumerable().Reverse().ToList()],
            _ => [],
        };
    }

    static List<Vec2> Ring(double r) => Chords.Circle(r) is var n ? Enumerable.Range(0, n).Select(i => new Vec2(r * Math.Cos(2 * Math.PI * i / n), r * Math.Sin(2 * Math.PI * i / n))).ToList() : [];

    /// <summary>
    /// A cold-formed section's sheet as a line through its thickness's middle: a C's web on the left, its flanges and lips to
    /// the right; a Z's web in the middle, its bottom flange to the left and its top to the right; a Σ a C whose web folds in
    /// (towards the flanges) by a quarter of the flanges over its middle half.
    /// </summary>
    public IReadOnlyList<Vec2> Centreline()
    {
        double h = Web / 2, x = Width / 2 - h, y = Depth / 2 - h, lip = Lip > h ? Depth / 2 - Lip : double.NaN;
        var o = new List<Vec2>();
        switch (Kind)
        {
            case "C" or "SIGMA":
                if (!double.IsNaN(lip)) o.Add(new(x, -lip));
                o.AddRange([new(x, -y), new(-x, -y)]);
                if (Kind == "SIGMA")
                {
                    var fold = Width / 4;
                    o.AddRange([new(-x, -Depth / 4 - fold / 2), new(-x + fold, -Depth / 4 + fold / 2), new(-x + fold, Depth / 4 - fold / 2), new(-x, Depth / 4 + fold / 2)]);
                }
                o.AddRange([new(-x, y), new(x, y)]);
                if (!double.IsNaN(lip)) o.Add(new(x, lip));
                break;
            case "Z":
                if (!double.IsNaN(lip)) o.Add(new(-x, -lip));
                o.AddRange([new(-x, -y), new(0, -y), new(0, y), new(x, y)]);
                if (!double.IsNaN(lip)) o.Add(new(x, lip));
                break;
        }
        return o;
    }

    /// <summary>An open line made a sheet <paramref name="t"/> thick about it: each side offset by half, the corners mitred, the ends square.</summary>
    public static List<Vec2> Sheet(IReadOnlyList<Vec2> line, double t)
    {
        if (line.Count < 2) return [];
        List<Vec2> Side(double off)
        {
            var o = new List<Vec2>();
            for (var i = 0; i < line.Count; i++)
            {
                var din = i > 0 ? (line[i] - line[i - 1]).Normalized() : (line[1] - line[0]).Normalized();
                var dout = i < line.Count - 1 ? (line[i + 1] - line[i]).Normalized() : din;
                // The mitre: the two offset lines meet along the bisector, 1 / cos(half the turn) out.
                var n = (din.Perp + dout.Perp).Normalized();
                var cos = Vec2.Dot(n, din.Perp);
                o.Add(line[i] + n * (off / Math.Max(cos, 1e-6)));
            }
            return o;
        }
        var left = Side(t / 2);
        var right = Side(-t / 2);
        right.Reverse();
        return [.. left, .. right];
    }

    /// <summary>How a type list shows it: 30×30, Ø40, I/H 300×290, IPE 300, RHS 150×100×6, CHS 114.3×5, C 200×65×2.</summary>
    public string Label => Name ?? Kind switch
    {
        "circle" => $"Ø{Width * 1000:0}",
        "rectangle" => $"{Width * 100:0}×{Depth * 100:0}",
        "I" => $"I/H {Depth * 1000:0}×{Width * 1000:0}",
        "L" => $"L {Width * 1000:0}×{Depth * 1000:0}×{Flange * 1000:0}",
        "RHS" => $"RHS {Depth * 1000:0.#}×{Width * 1000:0.#}×{Web * 1000:0.#}",
        "CHS" => $"CHS {Width * 1000:0.#}×{Web * 1000:0.#}",
        "C" or "Z" or "SIGMA" => $"{(Kind == "SIGMA" ? "Σ" : Kind)} {Depth * 1000:0}×{Width * 1000:0}×{Web * 1000:0.#}",
        _ => $"{Kind} {Depth * 1000:0}×{Width * 1000:0}",
    };

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["kind"] = Kind, ["width"] = Width, ["depth"] = Depth };
        if (IsProfile) { o["web"] = Web; o["flange"] = Flange; }
        if (Lip > 0) o["lip"] = Lip;
        if (Name != null) o["name"] = Name;
        return o;
    }

    /// <summary>The steel profiles a type can be made of by name (mm: depth, width, web, flange; a hollow or cold-formed section's thickness as both, a cold-formed one's lips).</summary>
    public static readonly IReadOnlyDictionary<string, Section> Catalogue = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase)
    {
        ["IPE 200"] = P("I", 200, 100, 5.6, 8.5, "IPE 200"), ["IPE 240"] = P("I", 240, 120, 6.2, 9.8, "IPE 240"), ["IPE 300"] = P("I", 300, 150, 7.1, 10.7, "IPE 300"),
        ["IPE 360"] = P("I", 360, 170, 8, 12.7, "IPE 360"), ["IPE 400"] = P("I", 400, 180, 8.6, 13.5, "IPE 400"),
        ["HEA 200"] = P("I", 190, 200, 6.5, 10, "HEA 200"), ["HEA 240"] = P("I", 230, 240, 7.5, 12, "HEA 240"), ["HEA 300"] = P("I", 290, 300, 8.5, 14, "HEA 300"),
        ["HEB 200"] = P("I", 200, 200, 9, 15, "HEB 200"), ["HEB 300"] = P("I", 300, 300, 11, 19, "HEB 300"),
        ["UPN 200"] = P("U", 200, 75, 8.5, 11.5, "UPN 200"), ["UPN 300"] = P("U", 300, 100, 10, 16, "UPN 300"),
        ["L 80×80×8"] = P("L", 80, 80, 8, 8, "L 80×80×8"), ["L 100×100×10"] = P("L", 100, 100, 10, 10, "L 100×100×10"),
        ["RHS 100×100×5"] = P("RHS", 100, 100, 5, 5, "RHS 100×100×5"), ["RHS 150×100×6"] = P("RHS", 150, 100, 6, 6, "RHS 150×100×6"), ["RHS 200×100×8"] = P("RHS", 200, 100, 8, 8, "RHS 200×100×8"),
        ["CHS 88.9×4"] = P("CHS", 88.9, 88.9, 4, 4, "CHS 88.9×4"), ["CHS 114.3×5"] = P("CHS", 114.3, 114.3, 5, 5, "CHS 114.3×5"), ["CHS 168.3×6"] = P("CHS", 168.3, 168.3, 6, 6, "CHS 168.3×6"),
        ["C 150×50×2"] = P("C", 150, 50, 2, 2, "C 150×50×2", 15), ["C 200×65×2"] = P("C", 200, 65, 2, 2, "C 200×65×2", 20),
        ["Z 150×55×2"] = P("Z", 150, 110, 2, 2, "Z 150×55×2", 15), ["Z 200×65×2"] = P("Z", 200, 130, 2, 2, "Z 200×65×2", 20),
        ["Σ 200×62×2"] = P("SIGMA", 200, 62, 2, 2, "Σ 200×62×2", 20),
    };

    static Section P(string kind, double depth, double width, double web, double flange, string name, double lip = 0) => new(kind, width / 1000, depth / 1000, web / 1000, flange / 1000, name, lip / 1000);

    public static Section? From(JsonNode? n)
    {
        if (n is JsonValue v && v.TryGetValue<string>(out var named)) return Catalogue.GetValueOrDefault(named.Replace("x", "×"));
        if (n is not JsonObject o) return null;
        if ((string?)o["name"] is { } nm && Catalogue.TryGetValue(nm.Replace("x", "×"), out var c) && o["width"] == null) return c;
        var kind = (string?)o["kind"] ?? "rectangle";
        if (kind == "H") kind = "I";
        if (kind == "Σ") kind = "SIGMA";
        var w = Json.Number(o["width"]) ?? Json.Number(o["diameter"]) ?? 0;
        var d = kind is "circle" or "CHS" ? w : Json.Number(o["depth"]) ?? 0;
        // A thin-walled section takes one thickness for its web and flanges.
        var t = Json.Number(o["thickness"]);
        var web = Json.Number(o["web"]) ?? t ?? 0;
        var flange = Json.Number(o["flange"]) ?? (ThinWalled.Contains(kind) ? web : t ?? 0);
        return new Section(kind, w, d, web, flange, (string?)o["name"], Json.Number(o["lip"]) ?? 0);
    }

    public static Section Parse(JsonNode? n)
    {
        var s = From(n) ?? throw new CommandException("A section is a rectangle (width, depth), a circle (diameter), a profile I, U, T, L (depth, width, web, flange), RHS, CHS, C, Z or Σ (depth, width, thickness, lip), or a catalogue name (IPE 300).");
        if (s.Kind is not ("rectangle" or "circle") && !s.IsProfile) throw new CommandException("A section is a rectangle, a circle, or a profile I, U, T, L, RHS, CHS, C, Z or Σ.");
        if (!(s.Width > 0 && s.Depth > 0)) throw new CommandException("A section needs a positive size.");
        if (s.Kind is "RHS" or "CHS" && !(s.Web > 0 && 2 * s.Web < Math.Min(s.Width, s.Depth))) throw new CommandException("A hollow section's wall must be thinner than half of it.");
        else if (s.Kind is "C" or "Z" or "SIGMA" && !(s.Web > 0 && 2 * s.Web < s.Width && s.Lip >= 0 && s.Lip < s.Depth / 2)) throw new CommandException("A cold-formed section's sheet must be thinner than its flanges, its lips shorter than half its depth.");
        else if (s.Kind is "I" or "U" or "T" or "L" && !(s.Web > 0 && s.Flange > 0 && s.Web < s.Width && 2 * s.Flange < s.Depth)) throw new CommandException("A profile's web and flanges must be thinner than it.");
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

    public static Vec2 P(JsonObject a, string x, string y) => Args.Point(a, x, y);

    public static Entity? TypeOf(IModelView m, string id) => m.RelatingOf(id, IfcClass.RelDefinesByType);

    /// <summary>The type named in the args, of the class; else the project's first of the class, else <paramref name="make"/>'s.</summary>
    public static Entity TypeFor(ICommandContext ctx, JsonObject a, string typeClass, Func<Entity> make, Func<Entity, bool>? fits = null)
    {
        if ((string?)a["typeId"] is { } id)
            return ctx.Model.Get(id) is { } t && t.Class == typeClass && (fits == null || fits(t)) ? t : throw new CommandException($"There is no such {typeClass}.");
        return ctx.Model.OfClass(typeClass).FirstOrDefault(t => fits == null || fits(t)) ?? make();
    }

    /// <summary>The part's solid less what cuts it by priority.</summary>
}
