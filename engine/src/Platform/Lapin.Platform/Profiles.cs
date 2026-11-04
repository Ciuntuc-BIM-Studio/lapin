using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A corner of a profile (metres) and the bulge of the side that leaves it (DXF's: tan of a quarter of the arc's turn, positive anticlockwise; 0 straight).</summary>
public readonly record struct ProfileVertex(double U, double V, double Bulge = 0);

/// <summary>
/// A closed 2D profile (decision 297): its corners in its own (u, v) plane (metres; v up), its sides straight or arcs,
/// and its insertion point, the point that is laid on what the profile is swept along (a wall's face at the sweep's height, a
/// footing's axis at its bottom). <see cref="Outline"/> is the profile anticlockwise, arcs as chords no more than 1 mm off
/// (<see cref="Chords"/>), relative to the insertion point.
/// </summary>
public sealed record ProfileShape(string Name, IReadOnlyList<ProfileVertex> Vertices, Vec2 Insertion)
{
    public List<Vec2> Outline()
    {
        var o = new List<Vec2>();
        var n = Vertices.Count;
        for (var i = 0; i < n; i++)
        {
            var a = Vertices[i];
            var b = Vertices[(i + 1) % n];
            var p = new Vec2(a.U, a.V);
            var q = new Vec2(b.U, b.V);
            o.Add(p - Insertion);
            var c = (q - p).Length;
            if (Math.Abs(a.Bulge) < 1e-9 || c < 1e-9) continue;
            var theta = 4 * Math.Atan(a.Bulge);
            var u = (q - p) * (1 / c);
            var d = c / 2 / Math.Tan(theta / 2);
            var centre = (p + q) * 0.5 + u.Perp * d;
            var r = (p - centre).Length;
            var a0 = Math.Atan2(p.Y - centre.Y, p.X - centre.X);
            var steps = Chords.Count(r, Math.Abs(theta), 2);
            for (var k = 1; k < steps; k++)
            {
                var t = a0 + theta * k / steps;
                o.Add(new Vec2(centre.X + r * Math.Cos(t), centre.Y + r * Math.Sin(t)) - Insertion);
            }
        }
        // Repeated points (a side of no length) are dropped; the profile turns anticlockwise.
        var clean = new List<Vec2>();
        foreach (var p in o) if (clean.Count == 0 || (p - clean[^1]).Length > 1e-7) clean.Add(p);
        if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-7) clean.RemoveAt(clean.Count - 1);
        return Polygon2.SignedArea(clean) < 0 ? clean.AsEnumerable().Reverse().ToList() : clean;
    }

    public double Area => Math.Abs(Polygon2.SignedArea(Outline()));
    public (double U0, double U1, double V0, double V1) Bounds
    {
        get
        {
            var o = Outline();
            return o.Count == 0 ? (0, 0, 0, 0) : (o.Min(p => p.X), o.Max(p => p.X), o.Min(p => p.Y), o.Max(p => p.Y));
        }
    }

    public JsonObject ToJson() => new()
    {
        ["name"] = Name,
        ["points"] = new JsonArray(Vertices.Select(v => (JsonNode)(Math.Abs(v.Bulge) > 1e-12 ? new JsonArray(R(v.U), R(v.V), R(v.Bulge)) : new JsonArray(R(v.U), R(v.V)))).ToArray()),
        ["insertion"] = new JsonArray(R(Insertion.X), R(Insertion.Y)),
    };

    static double R(double x) => Math.Round(x, 9);

    /// <summary>A profile from its JSON ({name?, points: [[u, v, bulge?]…], insertion?}); null when it has no area.</summary>
    public static ProfileShape? From(JsonNode? n, string? name = null)
    {
        if (n is not JsonObject o || o["points"] is not JsonArray pts) return null;
        var vs = new List<ProfileVertex>();
        foreach (var p in pts)
        {
            if (p is not JsonArray xy || xy.Count < 2 || Json.Number(xy[0]) is not { } u || Json.Number(xy[1]) is not { } v || !double.IsFinite(u) || !double.IsFinite(v)) return null;
            var b = xy.Count > 2 ? Json.Number(xy[2]) ?? 0 : 0;
            vs.Add(new ProfileVertex(u, v, double.IsFinite(b) ? Math.Clamp(b, -1, 1) : 0));
        }
        if (vs.Count < 2) return null;
        var ins = Args.TryPoint(o["insertion"]) ?? default;
        var shape = new ProfileShape(name ?? (string?)o["name"] ?? "Profile", vs, ins);
        return shape.Outline() is { Count: >= 3 } outline && Math.Abs(Polygon2.SignedArea(outline)) > 1e-8 ? shape : null;
    }
}

/// <summary>A ready-made profile of the catalogue: what it is for, its parameters (metres) and how it is drawn from them.</summary>
public sealed record CatalogueProfile(string Id, string Label, string Use, IReadOnlyList<(string Key, string Label, double Default)> Params,
    Func<Func<string, double>, List<ProfileVertex>> Make, Func<Func<string, double>, Vec2>? InsertionAt = null)
{
    public ProfileShape Shape(JsonObject? values = null, string? name = null)
    {
        double P(string k) => Json.Number(values?[k]) is { } v && double.IsFinite(v) && v > 0 ? v : Params.FirstOrDefault(p => p.Key == k).Default;
        return new ProfileShape(name ?? Label, Make(P), InsertionAt?.Invoke(P) ?? default);
    }
}

/// <summary>
/// The project's profiles (decision 297): <c>LapinProfile</c> resources (Manage → Profiles…) a wall sweep or reveal and a
/// strip footing type are swept with. Whatever uses one keeps its id (<see cref="IdKey"/>) and a copy of it
/// (<see cref="Key"/>): the copy is what IFC carries and what is left when the profile is deleted; a profile changed rewrites
/// the copies. A catalogue of ready-made ones, by use: <c>sweep</c> (a band standing out of a wall's face, u out of the face),
/// <c>reveal</c> (a groove, u into the wall), <c>footing</c> (u across the axis, left positive, v up from the bottom; the
/// insertion point on the axis at the bottom).
/// </summary>
public static class Profiles
{
    public const string Class = "LapinProfile";
    public const string Key = "profile";
    public const string IdKey = "profileId";
    public static readonly string[] Uses = ["sweep", "reveal", "footing"];

    static ProfileVertex V(double u, double v, double b = 0) => new(u, v, b);

    public static readonly IReadOnlyList<CatalogueProfile> Catalogue =
    [
        // Sweeps: out of the face (u), up from the sweep's height (v).
        new("rectangle", "Rectangle", "sweep", [("depth", "Depth", 0.05), ("height", "Height", 0.1)],
            p => [V(0, 0), V(p("depth"), 0), V(p("depth"), p("height")), V(0, p("height"))]),
        new("chamfered", "Chamfered band", "sweep", [("depth", "Depth", 0.06), ("height", "Height", 0.12), ("chamfer", "Chamfer", 0.03)],
            p =>
            {
                var c = Math.Min(p("chamfer"), Math.Min(p("depth"), p("height")) * 0.95);
                return [V(0, 0), V(p("depth"), 0), V(p("depth"), p("height") - c), V(p("depth") - c, p("height")), V(0, p("height"))];
            }),
        new("cornice", "Cornice", "sweep", [("depth", "Depth", 0.15), ("height", "Height", 0.2)],
            p =>
            {
                double d = p("depth"), h = p("height");
                // A fillet at the foot, a cove rising out to the crown, a square crown on top.
                return [V(0, 0), V(0.2 * d, 0), V(0.2 * d, 0.15 * h, -0.4), V(d, 0.8 * h), V(d, h), V(0, h)];
            }),
        new("halfRound", "Half-round", "sweep", [("radius", "Radius", 0.04)],
            p => [V(0, 0, 1), V(0, 2 * p("radius"))]),
        new("sillNose", "Sill nose", "sweep", [("depth", "Depth", 0.06), ("height", "Height", 0.04)],
            p =>
            {
                var h = Math.Min(p("height"), 2 * p("depth"));
                return [V(0, 0), V(p("depth") - h / 2, 0, 1), V(p("depth") - h / 2, h), V(0, h)];
            }),
        new("drip", "Band with drip", "sweep", [("depth", "Depth", 0.08), ("height", "Height", 0.05), ("groove", "Drip groove", 0.01)],
            p =>
            {
                double d = p("depth"), h = p("height"), g = Math.Min(p("groove"), Math.Min(d / 3, h / 2));
                return [V(0, 0), V(d - 2.5 * g, 0), V(d - 2.5 * g, 0.8 * g), V(d - 1.5 * g, 0.8 * g), V(d - 1.5 * g, 0), V(d, 0), V(d, h), V(0, h)];
            }),
        // Reveals: into the wall (u), up from the groove's bottom (v).
        new("vGroove", "V-groove", "reveal", [("depth", "Depth", 0.015), ("width", "Width", 0.03)],
            p => [V(0, 0), V(p("depth"), p("width") / 2), V(0, p("width"))]),
        new("groove", "Rectangular groove", "reveal", [("depth", "Depth", 0.02), ("width", "Width", 0.03)],
            p => [V(0, 0), V(p("depth"), 0), V(p("depth"), p("width")), V(0, p("width"))]),
        new("roundGroove", "Round groove", "reveal", [("radius", "Radius", 0.015)],
            p => [V(0, 2 * p("radius"), -1), V(0, 0)]),
        // Footings: across the axis (u, left positive), up from the bottom (v); the axis on the middle unless said.
        new("footingRect", "Rectangular footing", "footing", [("width", "Width", 0.6), ("height", "Height", 0.4)],
            p => [V(-p("width") / 2, 0), V(p("width") / 2, 0), V(p("width") / 2, p("height")), V(-p("width") / 2, p("height"))]),
        new("stepped2", "Stepped footing, 2 steps", "footing", [("width1", "Bottom step width", 1.0), ("height1", "Bottom step height", 0.35), ("width2", "Top step width", 0.6), ("height2", "Top step height", 0.35)],
            p => Steps(p, 2)),
        new("stepped3", "Stepped footing, 3 steps", "footing", [("width1", "Bottom step width", 1.2), ("height1", "Bottom step height", 0.3), ("width2", "Middle step width", 0.85), ("height2", "Middle step height", 0.3), ("width3", "Top step width", 0.5), ("height3", "Top step height", 0.3)],
            p => Steps(p, 3)),
        new("trapezoid", "Sloped top footing", "footing", [("width", "Bottom width", 1.0), ("edge", "Edge height", 0.25), ("height", "Height", 0.5), ("top", "Top width", 0.4)],
            p =>
            {
                double b = p("width") / 2, t = Math.Min(p("top"), p("width")) / 2, h = p("height"), e = Math.Min(p("edge"), h);
                return [V(-b, 0), V(b, 0), V(b, e), V(t, h), V(-t, h), V(-b, e)];
            }),
        new("eccentric", "Eccentric footing (L)", "footing", [("width", "Footing width", 0.8), ("height", "Footing height", 0.4), ("stem", "Stem thickness", 0.3), ("stemHeight", "Stem height", 1.0)],
            p =>
            {
                double b = Math.Max(p("width"), p("stem")), h = p("height"), t = p("stem") / 2, s = p("stemHeight");
                // The stem on the axis, at the footing's right edge; the footing runs out to the left.
                return [V(-(b - t), 0), V(t, 0), V(t, h + s), V(-t, h + s), V(-t, h), V(-(b - t), h)];
            }),
        new("withStem", "Footing with stem", "footing", [("width", "Footing width", 0.8), ("height", "Footing height", 0.4), ("stem", "Stem thickness", 0.3), ("stemHeight", "Stem height", 1.0)],
            p =>
            {
                double b = p("width") / 2, h = p("height"), t = Math.Min(p("stem"), p("width")) / 2, s = p("stemHeight");
                return [V(-b, 0), V(b, 0), V(b, h), V(t, h), V(t, h + s), V(-t, h + s), V(-t, h), V(-b, h)];
            }),
    ];

    static List<ProfileVertex> Steps(Func<string, double> p, int n)
    {
        // Bottom up, each centred on the axis: up the right side, then down the left.
        var right = new List<ProfileVertex>();
        var z = 0.0;
        for (var i = 1; i <= n; i++)
        {
            var w = p($"width{i}") / 2;
            right.Add(V(w, z));
            z += p($"height{i}");
            right.Add(V(w, z));
        }
        var left = right.AsEnumerable().Reverse().Select(v => V(-v.U, v.V));
        return [.. right, .. left];
    }

    public static CatalogueProfile? CatalogueEntry(string? id) => Catalogue.FirstOrDefault(c => c.Id == id);

    /// <summary>The profile an element or type uses: the project's profile it names, else the copy it keeps; null when it has neither.</summary>
    public static ProfileShape? Of(IModelView m, Entity owner)
    {
        if (owner.Str(IdKey) is { } id && m.Get(id) is { Class: Class } p && Read(p) is { } own) return own;
        return ProfileShape.From(owner.Attrs[Key]);
    }

    /// <summary>A profile resource's shape.</summary>
    public static ProfileShape? Read(Entity profile) => ProfileShape.From(profile.Attrs[Key], profile.Name);

    /// <summary>Sets an owner's profile: its id and its copy (a shape given without a project profile keeps only the copy).</summary>
    public static void Assign(JsonObject attrs, ProfileShape shape, string? id)
    {
        attrs[Key] = shape.ToJson();
        if (id != null) attrs[IdKey] = id; else attrs.Remove(IdKey);
    }

    /// <summary>
    /// The profile an argument names: <c>profileId</c> (a project profile), <c>catalog</c> and <c>params</c> (a ready-made one,
    /// made a project profile the first time, or the one already made with the same values), or <c>profile</c> (a shape typed).
    /// </summary>
    public static (ProfileShape Shape, string? Id)? FromArgs(ICommandContext ctx, JsonObject a)
    {
        if ((string?)a[IdKey] is { Length: > 0 } pid)
        {
            var p = Spatial.Require(ctx.Model, pid, Class);
            return (Read(p) ?? throw new CommandException($"„{p.Name}” has no outline."), p.Id);
        }
        if ((string?)a["catalog"] is { Length: > 0 } cid)
        {
            var made = Ensure(ctx, cid, a["params"] as JsonObject);
            return (Read(made)!, made.Id);
        }
        if (a[Key] is JsonObject o)
            return (ProfileShape.From(o) ?? throw new CommandException("The profile needs three points at least, enclosing an area."), null);
        return null;
    }

    /// <summary>A ready-made profile in the project: the one made from it with the same values, else a new one.</summary>
    public static Entity Ensure(ICommandContext ctx, string catalogId, JsonObject? values)
    {
        var c = CatalogueEntry(catalogId) ?? throw new CommandException($"The catalogue has no profile „{catalogId}”.");
        var shape = c.Shape(values);
        var json = shape.ToJson();
        foreach (var p in ctx.Model.OfClass(Class))
            if ((p.Attrs["catalog"] as JsonObject)?["id"]?.ToString() == catalogId && JsonNode.DeepEquals(p.Attrs[Key]?["points"], json["points"])) return p;
        return Create(ctx, NameFor(c, values), shape, c.Use, catalogId, values);
    }

    static string NameFor(CatalogueProfile c, JsonObject? values)
    {
        var dims = c.Params.Select(p => Json.Number(values?[p.Key]) is { } v && v > 0 ? v : p.Default).Select(v => (v * 1000).ToString("0", System.Globalization.CultureInfo.InvariantCulture));
        return $"{c.Label} {string.Join("×", dims)}";
    }

    public static Entity Create(ICommandContext ctx, string name, ProfileShape shape, string use, string? catalogId = null, JsonObject? values = null)
    {
        var attrs = new JsonObject { [Key] = (shape with { Name = name }).ToJson(), ["use"] = Uses.Contains(use) ? use : "sweep" };
        if (catalogId != null) attrs["catalog"] = new JsonObject { ["id"] = catalogId, ["params"] = values?.DeepClone() ?? new JsonObject() };
        var e = new Entity(ctx.NewId(), Class, string.IsNullOrWhiteSpace(name) ? "Profile" : name.Trim(), attrs);
        ctx.Add(e);
        return e;
    }

    /// <summary>A profile changed: everything using it takes the new copy (its geometry follows the profile itself already).</summary>
    static void Propagate(ICommandContext ctx, Entity profile)
    {
        if (Read(profile) is not { } shape) return;
        foreach (var e in ctx.Model.Entities.Where(x => x.Str(IdKey) == profile.Id).ToList())
        {
            var attrs = e.CloneAttrs();
            attrs[Key] = shape.ToJson();
            ctx.Replace(e.With(attrs: attrs));
        }
    }

    /// <summary>
    /// The profile of a drafted closed outline (an IfcAnnotation DRAFT, a closed polyline in its view's plane): its points as
    /// drawn, moved so the insertion point (bottom left for a sweep or a reveal, bottom middle for a footing) is the origin.
    /// </summary>
    public static ProfileShape FromDrafting(IModelView m, string annotationId, string use, string? name)
    {
        if (m.Get(annotationId) is not { Class: IfcClass.Annotation } e || e.PredefinedType != "DRAFT") throw new CommandException("Not a drafted shape: select a closed polyline drawn with Drafting.");
        if (e.Attrs["shape"] is not JsonObject shape || (string?)shape["type"] != "polyline" || shape["closed"] is not JsonValue cv || !cv.TryGetValue<bool>(out var closed) || !closed)
            throw new CommandException("Not a closed outline: draw the profile as a closed polyline or a rectangle.");
        var pts = Args.StoredPoints(shape["pts"]);
        var bulges = (shape["bulges"] as JsonArray)?.Select(b => Json.Number(b) ?? 0).ToList();
        if (pts.Count < 3) throw new CommandException("The outline needs three points at least.");
        var (minX, maxX, minY) = (pts.Min(p => p.X), pts.Max(p => p.X), pts.Min(p => p.Y));
        var origin = use == "footing" ? new Vec2((minX + maxX) / 2, minY) : new Vec2(minX, minY);
        var vs = pts.Select((p, i) => new ProfileVertex(p.X - origin.X, p.Y - origin.Y, bulges != null && i < bulges.Count ? bulges[i] : 0)).ToList();
        var made = new ProfileShape(name ?? e.Name, vs, default);
        if (made.Outline().Count < 3 || made.Area < 1e-8) throw new CommandException("The outline encloses no area.");
        return made;
    }

    static int NextNumber(IModelView m) => 1 + m.OfClass(Class).Select(e =>
    {
        var i = e.Name.Length;
        while (i > 0 && char.IsAsciiDigit(e.Name[i - 1])) i--;
        return i < e.Name.Length && int.TryParse(e.Name.AsSpan(i), out var n) ? n : 0;
    }).DefaultIfEmpty(0).Max();

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Profile", [new("use", "Use", "choice", Choices: Uses)], "#9a8f80", Kind: "resource"));
        r.Catalog("profileCatalogue", new JsonArray(Catalogue.Select(c => (JsonNode)new JsonObject
        {
            ["id"] = c.Id, ["label"] = c.Label, ["use"] = c.Use,
            ["params"] = new JsonArray(c.Params.Select(p => (JsonNode)new JsonObject { ["key"] = p.Key, ["label"] = p.Label, ["default"] = p.Default }).ToArray()),
            ["shape"] = c.Shape().ToJson(),
        }).ToArray()));

        // {name?, use?, catalog?, params?} a ready-made one with its values, or {name?, use?, profile: {points, insertion?}} one typed,
        // or {name?, use?, annotationId} a drafted closed outline.
        r.Command("profile.create", "New profile", (ctx, a) =>
        {
            var use = (string?)a["use"] is { } u && Uses.Contains(u) ? u : null;
            var name = (string?)a["name"];
            Entity made;
            if ((string?)a["catalog"] is { Length: > 0 } cid)
            {
                var c = CatalogueEntry(cid) ?? throw new CommandException($"The catalogue has no profile „{cid}”.");
                made = Create(ctx, string.IsNullOrWhiteSpace(name) ? NameFor(c, a["params"] as JsonObject) : name, c.Shape(a["params"] as JsonObject), use ?? c.Use, cid, a["params"] as JsonObject);
            }
            else if ((string?)a["annotationId"] is { Length: > 0 } aid)
            {
                var shape = FromDrafting(ctx.Model, aid, use ?? "sweep", name);
                made = Create(ctx, string.IsNullOrWhiteSpace(name) ? $"Profile {NextNumber(ctx.Model)}" : name, shape, use ?? "sweep");
            }
            else
            {
                var shape = ProfileShape.From(a[Key]) ?? throw new CommandException("The profile needs three points at least, enclosing an area.");
                made = Create(ctx, string.IsNullOrWhiteSpace(name) ? $"Profile {NextNumber(ctx.Model)}" : name, shape, use ?? "sweep");
            }
            return new JsonObject { ["id"] = made.Id };
        });

        // {id, name?, use?, profile?: {points, insertion?}, params?: the ready-made one's values again}: what uses it follows.
        r.Command("profile.update", "Change profile", (ctx, a) =>
        {
            var p = Spatial.Require(ctx.Model, (string?)a["id"], Class);
            var attrs = p.CloneAttrs();
            var name = (string?)a["name"] is { } n && !string.IsNullOrWhiteSpace(n) ? n.Trim() : p.Name;
            if ((string?)a["use"] is { } u && Uses.Contains(u)) attrs["use"] = u;
            if (a["params"] is JsonObject values && (attrs["catalog"] as JsonObject)?["id"]?.ToString() is { } cid && CatalogueEntry(cid) is { } c)
            {
                attrs[Key] = c.Shape(values, name).ToJson();
                attrs["catalog"] = new JsonObject { ["id"] = cid, ["params"] = values.DeepClone() };
            }
            else if (a[Key] is JsonObject o)
            {
                attrs[Key] = (ProfileShape.From(o) ?? throw new CommandException("The profile needs three points at least, enclosing an area.")).ToJson();
                attrs.Remove("catalog");
            }
            if (attrs[Key] is JsonObject k) k["name"] = name;
            ctx.Replace(p.With(name: name, attrs: attrs));
            Propagate(ctx, ctx.Model.Get(p.Id)!);
            return null;
        });

        // {id} a profile or what uses one, {catalog, params} a ready-made one with these values, or {profile}: its outline and area.
        r.Query("profile.outline", (q, a) =>
        {
            var shape = (string?)a["id"] is { } id && q.Model.Get(id) is { } e ? (e.Class == Class ? Read(e) : Of(q.Model, e))
                : CatalogueEntry((string?)a["catalog"]) is { } c ? c.Shape(a["params"] as JsonObject)
                : ProfileShape.From(a[Key]);
            if (shape == null) return null;
            return new JsonObject
            {
                ["outline"] = new JsonArray(shape.Outline().Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 6), Math.Round(p.Y, 6))).ToArray()),
                ["area"] = shape.Area,
                [Key] = shape.ToJson(),
            };
        });
    }
}

/// <summary>
/// A profile swept along a line in plan (decision 297): the profile stands across the line, its x to the left of the way
/// the line runs (negative: to the right), its y up from <c>z0</c>; at each corner the section lies in the corner's bisecting
/// plane (mitred). At either end the section may turn to meet another line's (<c>startNormal</c>, <c>endNormal</c>: that
/// line's left normal, so two sweeps meeting there are mitred into one another), or reach past the end along the line by an
/// amount that varies straight across it (<c>startReach</c>, <c>endReach</c>: a join's cut, straight across the body).
/// </summary>
public static class ProfileSweeps
{
    public static Solid? Along(IReadOnlyList<Vec2> line, IReadOnlyList<Vec2> profile, double z0, Vec2? startNormal = null, Vec2? endNormal = null,
        Func<double, double>? startReach = null, Func<double, double>? endReach = null, string tag = "sweep") =>
        Rings(line, profile, z0, startNormal, endNormal, startReach, endReach) is { } rings ? Solid(rings, tag) : null;

    /// <summary>The sections the sweep passes through, one at each point of the line, each the profile's points in order; null when there is no line or no profile.</summary>
    public static List<Vec3[]>? Rings(IReadOnlyList<Vec2> line, IReadOnlyList<Vec2> profile, double z0, Vec2? startNormal = null, Vec2? endNormal = null,
        Func<double, double>? startReach = null, Func<double, double>? endReach = null)
    {
        var pts = new List<Vec2>();
        foreach (var p in line) if (pts.Count == 0 || (p - pts[^1]).Length > 1e-7) pts.Add(p);
        if (pts.Count < 2 || profile.Count < 3) return null;
        var count = pts.Count;
        var segs = count - 1;
        var dirs = new Vec2[segs];
        var normals = new Vec2[segs];
        for (var i = 0; i < segs; i++)
        {
            dirs[i] = (pts[i + 1] - pts[i]).Normalized();
            normals[i] = dirs[i].Perp;
        }
        static Vec2 Mitre(Vec2 a, Vec2 b)
        {
            var dot = Vec2.Dot(a, b);
            return dot < -0.95 ? a : (a + b) * (1 / (1 + dot));
        }
        var rings = new List<Vec3[]>();
        for (var i = 0; i < count; i++)
        {
            var incoming = i > 0 ? normals[i - 1] : startNormal ?? normals[0];
            var outgoing = i < count - 1 ? normals[i] : endNormal ?? normals[segs - 1];
            var mitre = Mitre(incoming, outgoing);
            var ring = new Vec3[profile.Count];
            for (var k = 0; k < profile.Count; k++)
            {
                var q = profile[k];
                var at = pts[i] + mitre * q.X;
                if (i == 0 && startReach != null) at -= dirs[0] * startReach(q.X);
                if (i == count - 1 && endReach != null) at += dirs[segs - 1] * endReach(q.X);
                ring[k] = at.At(z0 + q.Y);
            }
            rings.Add(ring);
        }
        return rings;
    }

    /// <summary>The closed body through the sections: their sides joined ring to ring, the first and the last closing its ends.</summary>
    public static Solid Solid(IReadOnlyList<Vec3[]> rings, string tag = "sweep")
    {
        var verts = new List<Vec3>();
        var idx = rings.Select(r => r.Select(v => { verts.Add(v); return verts.Count - 1; }).ToArray()).ToList();
        var faces = new List<FaceInput>();
        var np = rings[0].Length;
        for (var i = 0; i + 1 < idx.Count; i++)
        {
            var (lo, hi) = (idx[i], idx[i + 1]);
            for (var k = 0; k < np; k++)
            {
                var j = (k + 1) % np;
                faces.Add(new FaceInput([lo[k], lo[j], hi[j], hi[k]], null, null, "side"));
            }
        }
        faces.Add(new FaceInput(idx[0].Reverse().ToArray(), null, null, "start"));
        faces.Add(new FaceInput(idx[^1], null, null, "end"));
        var solid = SolidBuilder.Make(verts, faces, tag);
        return Measure.SignedVolume(solid) < 0 ? SolidBuilder.Flip(solid) : solid;
    }
}
