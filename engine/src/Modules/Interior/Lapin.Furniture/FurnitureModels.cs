using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Furniture;

/// <summary>
/// A furniture model (docs/plan-furniture.md, Configuratorul flexibil, stage 2; decision 477): a CC0 mesh shipped in
/// Lapin.Furniture.Models, as its register (furniture-assets.json) describes it — its name and group, its IFC type,
/// its extent at its own proportions (<see cref="Natural"/>, metres), the size a new type starts with, the least and
/// the most a type may take, its stretch zone on each axis (fractions of its extent; none: the axis scales whole), its
/// material slots mapped to Lapin's roles, the height it hangs at (lamps), whether it backs onto walls, whether its
/// plan shows seats.
/// </summary>
public sealed record FurnitureModel(string Id, string Name, string Group, string Predefined, string? ObjectType,
    double[] Natural, double[] Size, double[] Min, double[] Max, (double From, double To)?[] Zones,
    IReadOnlyDictionary<string, string> Slots, double Mount, bool Against, bool Seats, string Licence, string Source)
{
    /// <summary>The roles its slots take, in Lapin's order of roles.</summary>
    public IReadOnlyList<string> Roles => [.. FurnitureKinds.RoleOrder.Where(r => Slots.Values.Contains(r))];

    /// <summary>A size on an axis (0 x, 1 y, 2 z) kept between the model's least and most.</summary>
    public double Clamp(int axis, double v) => Math.Clamp(v, Min[axis], Max[axis]);
}

/// <summary>
/// The models (their register, read once) and a piece built from one: the mesh stretched by zones to the type's size
/// (a 3D nine-slice: what lies before an axis's zone keeps its place, what lies after moves by the growth, the band
/// between stretches; an axis without a zone scales whole), each slot's triangles a solid of its role, the plan the
/// body seen from above with the engine's hidden-line drawing (and a sofa's seats), hung pieces drawn overhead.
/// </summary>
public static class FurnitureModels
{
    public const string Key = "model";

    /// <summary>The roles a model's slots may take, with the materials new types take for them (indicative), beyond the kinds' own.</summary>
    public static readonly string[] Roles = ["upholstery", "accent", "body", "top", "legs", "metal", "glass", "shade", "foliage", "pot"];

    static IReadOnlyList<FurnitureModel>? _all;
    static readonly object Gate = new();

    /// <summary>Every model of the register, in its order.</summary>
    public static IReadOnlyList<FurnitureModel> All
    {
        get
        {
            if (_all != null) return _all;
            lock (Gate) return _all ??= Read();
        }
    }

    public static FurnitureModel? Find(string? id) => id == null ? null : All.FirstOrDefault(m => m.Id == id);

    /// <summary>A spec's model: the one it names, else the first.</summary>
    public static FurnitureModel Of(Spec s) => Find(s.Str("model")) ?? All[0];

    static IReadOnlyList<FurnitureModel> Read()
    {
        using var stream = typeof(FurnitureModels).Assembly.GetManifestResourceStream("furniture-assets.json")
            ?? throw new InvalidOperationException("The furniture models' register is missing from Lapin.Furniture.");
        var root = JsonNode.Parse(stream)!;
        static double[] V(JsonNode? n) => [.. n!.AsArray().Select(x => (double)x!)];
        var list = new List<FurnitureModel>();
        foreach (var m in root["models"]!.AsArray())
        {
            var zones = new (double, double)?[3];
            for (var k = 0; k < 3; k++)
                if (m!["zones"]?["xyz"[k].ToString()] is JsonArray { Count: 2 } z) zones[k] = ((double)z[0]!, (double)z[1]!);
            var slots = m!["slots"]!.AsObject().ToDictionary(p => p.Key, p => (string)p.Value!);
            list.Add(new FurnitureModel((string)m["id"]!, (string)m["name"]!, (string)m["group"]!, (string)m["predefined"]!, (string?)m["objectType"],
                V(m["natural"]), V(m["size"]), V(m["min"]), V(m["max"]), zones, slots, (double?)m["mount"] ?? 0, (bool?)m["against"] ?? true, (bool?)m["seats"] ?? false,
                (string)m["licence"]!, (string)m["source"]!));
        }
        return list;
    }

    /// <summary>
    /// A coordinate <paramref name="u"/> (metres from the model's low side, at its own proportions, out of
    /// <paramref name="natural"/>) on an axis stretched to <paramref name="target"/>: before the zone it stays, after it
    /// it moves by the growth, in it it scales; without a zone the whole axis scales.
    /// </summary>
    public static double Stretch(double u, double natural, double target, (double From, double To)? zone)
    {
        if (natural < 1e-9) return u;
        if (zone is not { } z) return u * target / natural;
        double a = z.From * natural, b = z.To * natural, band = b - a;
        var grown = target - (natural - band);
        if (band < 1e-9) return u <= a ? u : u + grown - band;
        if (u <= a) return u;
        if (u >= b) return u + grown - band;
        return a + (u - a) * grown / band;
    }

    /// <summary>The catalog the types dialog shows: the models (with their preview's path), the swatches per role and the looks.</summary>
    public static JsonObject Catalog() => new()
    {
        ["models"] = new JsonArray([.. All.Select(m => (JsonNode)new JsonObject
        {
            ["id"] = m.Id, ["name"] = m.Name, ["group"] = m.Group, ["predefined"] = m.Predefined, ["objectType"] = m.ObjectType,
            ["size"] = Arr(m.Size), ["min"] = Arr(m.Min), ["max"] = Arr(m.Max), ["mount"] = m.Mount, ["against"] = m.Against,
            ["roles"] = new JsonArray([.. m.Roles.Select(r => (JsonNode)r)]),
            ["zones"] = new JsonArray([.. m.Zones.Select(z => z is { } v ? (JsonNode)Arr([v.From, v.To]) : null)]),
            ["thumbnail"] = $"furniture-models/{m.Id}.png", ["licence"] = m.Licence, ["source"] = m.Source,
        })]),
        ["swatches"] = new JsonObject([.. Swatches.Select(p => new KeyValuePair<string, JsonNode?>(p.Key,
            new JsonArray([.. p.Value.Select(n => (JsonNode)new JsonObject { ["name"] = n, ["color"] = FurnitureKinds.ColorOf(n) })])))]),
        ["looks"] = new JsonArray([.. Looks.Select(l => (JsonNode)new JsonObject
        {
            ["key"] = l.Key, ["label"] = l.Label,
            ["materials"] = new JsonObject([.. l.Materials.Select(p => new KeyValuePair<string, JsonNode?>(p.Key, p.Value))]),
        })]),
    };

    static JsonArray Arr(double[] v) => new([.. v.Select(x => (JsonNode)x)]);

    /// <summary>The colour variants per role: materials a type can take with a click (indicative colours, the project's own when it has them).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Swatches = new Dictionary<string, string[]>
    {
        ["upholstery"] = ["Upholstery fabric grey", "Fabric charcoal", "Linen beige", "Bouclé off-white", "Velvet sage", "Velvet terracotta", "Velvet navy", "Leather cognac"],
        ["accent"] = ["Fabric charcoal", "Linen beige", "Velvet terracotta", "Velvet sage", "Velvet navy"],
        ["body"] = ["Oak veneer", "Walnut veneer", "Black-stained ash", "Lacquered MDF white", "Lacquered MDF anthracite"],
        ["top"] = ["Oak solid wood", "Walnut veneer", "Black-stained ash", "Lacquered MDF white", "Marble white"],
        ["legs"] = ["Oak solid wood", "Walnut veneer", "Black powder-coated steel", "Brushed steel", "Brass"],
        ["metal"] = ["Brushed steel", "Black powder-coated steel", "Brass"],
        ["glass"] = ["Black glass", "Clear glass"],
        ["shade"] = ["Lamp shade linen", "Opal glass", "Black powder-coated steel", "Brass"],
        ["foliage"] = ["Plant foliage", "Olive foliage"],
        ["pot"] = ["Terracotta", "Concrete grey", "Ceramic white", "Black powder-coated steel"],
    };

    /// <summary>Looks: materials for several roles at once (a role the model has not is left out).</summary>
    public static readonly IReadOnlyList<(string Key, string Label, IReadOnlyDictionary<string, string> Materials)> Looks =
    [
        ("nordic", "Nordic", new Dictionary<string, string> { ["upholstery"] = "Linen beige", ["accent"] = "Fabric charcoal", ["body"] = "Oak veneer", ["top"] = "Oak solid wood", ["legs"] = "Oak solid wood", ["metal"] = "Brushed steel", ["shade"] = "Lamp shade linen", ["pot"] = "Ceramic white" }),
        ("dark", "Dark", new Dictionary<string, string> { ["upholstery"] = "Fabric charcoal", ["accent"] = "Linen beige", ["body"] = "Black-stained ash", ["top"] = "Black-stained ash", ["legs"] = "Black powder-coated steel", ["metal"] = "Black powder-coated steel", ["glass"] = "Black glass", ["shade"] = "Black powder-coated steel", ["pot"] = "Concrete grey" }),
        ("warm", "Warm", new Dictionary<string, string> { ["upholstery"] = "Velvet terracotta", ["accent"] = "Linen beige", ["body"] = "Walnut veneer", ["top"] = "Walnut veneer", ["legs"] = "Walnut veneer", ["metal"] = "Brass", ["shade"] = "Opal glass", ["pot"] = "Terracotta" }),
        ("soft", "Soft", new Dictionary<string, string> { ["upholstery"] = "Bouclé off-white", ["accent"] = "Velvet sage", ["body"] = "Oak veneer", ["top"] = "Marble white", ["legs"] = "Brass", ["metal"] = "Brass", ["glass"] = "Clear glass", ["shade"] = "Opal glass", ["pot"] = "Ceramic white" }),
    ];

    // ---- A piece ----

    /// <summary>The spec's model stretched to its size, its solids by role, its plan (overhead when hung above 1.2 m).</summary>
    public static void Build(Builder b, Spec s)
    {
        var m = Of(s);
        var (w, d, h) = SizeOf(s, m);
        var mount = Math.Max(0, s.Num("mount", m.Mount));
        if (Stretched(m, w, d, h) is not { } body)
        {
            b.Warnings.Add($"The model „{m.Name}” could not be read.");
            b.PlanRect("symbol", -w / 2, w / 2, -d / 2, d / 2);
            return;
        }
        foreach (var (role, verts, tris) in body.Parts)
            b.Mesh(role, [.. verts.Select(v => v with { Z = v.Z + mount })], tris);
        var kind = mount + 0.05 >= 1.2 ? "overhead" : "symbol";
        foreach (var line in body.Plan) b.Line(kind, [.. line.Select(p => (p.X, p.Y))]);
        foreach (var line in body.Seats) b.LineFrom(PlanDetail.Medium, kind, line.Select(p => (p.X, p.Y)));
    }

    /// <summary>The size a spec asks of its model, each axis kept between the model's least and most.</summary>
    public static (double W, double D, double H) SizeOf(Spec s, FurnitureModel m) => (m.Clamp(0, s.W), m.Clamp(1, s.D), m.Clamp(2, s.H));

    /// <summary>A model stretched to a size, in the piece's frame (x across, y from front −d/2 to back d/2, z up from 0).</summary>
    public sealed record Body(IReadOnlyList<(string Role, Vec3[] Vertices, (int A, int B, int C)[] Triangles)> Parts, IReadOnlyList<Vec2[]> Plan, IReadOnlyList<Vec2[]> Seats);

    static readonly Dictionary<string, Body?> Bodies = [];

    /// <summary>The model stretched to w × d × h, kept for the next piece of that size (null when its mesh cannot be read).</summary>
    public static Body? Stretched(FurnitureModel m, double w, double d, double h)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"{m.Id}|{w:0.#####}|{d:0.#####}|{h:0.#####}");
        lock (Bodies) if (Bodies.TryGetValue(key, out var ready)) return ready;
        var body = Make(m, w, d, h);
        lock (Bodies)
        {
            if (Bodies.Count > 256) Bodies.Clear();
            Bodies[key] = body;
        }
        return body;
    }

    static Body? Make(FurnitureModel m, double w, double d, double h)
    {
        if (ModelMeshes.Get(m.Id) is not { } mesh) return null;
        double[] target = [w, d, h];
        Vec3 Map(Vec3 p)
        {
            // From the model's low corner, at its own proportions (its extent is Natural), to the piece's frame.
            var u = new[] { (p.X - mesh.Min.X) * mesh.Scale[0], (p.Y - mesh.Min.Y) * mesh.Scale[1], (p.Z - mesh.Min.Z) * mesh.Scale[2] };
            return new Vec3(
                Stretch(u[0], m.Natural[0], target[0], m.Zones[0]) - w / 2,
                Stretch(u[1], m.Natural[1], target[1], m.Zones[1]) - d / 2,
                Stretch(u[2], m.Natural[2], target[2], m.Zones[2]));
        }
        var parts = new List<(string, Vec3[], (int, int, int)[])>();
        foreach (var role in FurnitureKinds.RoleOrder)
        {
            var verts = new List<Vec3>();
            var tris = new List<(int, int, int)>();
            foreach (var piece in mesh.Pieces.Where(p => m.Slots.GetValueOrDefault(p.Slot, "body") == role))
            {
                var at = verts.Count;
                verts.AddRange(piece.Vertices.Select(Map));
                tris.AddRange(piece.Triangles.Select(t => (t.A + at, t.B + at, t.C + at)));
            }
            if (tris.Count > 0) parts.Add((role, verts.ToArray(), tris.ToArray()));
        }
        return new Body(parts, PlanOf(parts, h), SeatsOf(m, w, d));
    }

    /// <summary>The body seen from above, its hidden lines removed (the engine's drawing), chained into polylines.</summary>
    static List<Vec2[]> PlanOf(List<(string Role, Vec3[] Vertices, (int A, int B, int C)[] Triangles)> parts, double h)
    {
        var solids = parts.Select((p, i) => (MeshSolid(p.Vertices, p.Triangles), i)).Where(x => x.Item1 != null).Select(x => (x.Item1!, x.i)).ToList();
        var view = new ViewFrame(new Vec3(0, 0, h + 1), new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, -1));
        var (lines, _, _) = HiddenLines.Draw(solids, view);
        return Chain(lines.Where(l => (l.B - l.A).Length > 0.004).Select(l => (l.A, l.B)).ToList());
    }

    /// <summary>A sofa's seats in plan: lines across its seat, the stretched band shared out in seats about 650 mm wide.</summary>
    static List<Vec2[]> SeatsOf(FurnitureModel m, double w, double d)
    {
        if (!m.Seats || m.Zones[0] is not { } zx) return [];
        double x0 = zx.From * m.Natural[0] - w / 2, x1 = w / 2 - (1 - zx.To) * m.Natural[0];
        double back = m.Zones[1] is { } zy ? d / 2 - (1 - zy.To) * m.Natural[1] : d / 2 - 0.25 * d;
        var n = Math.Max(1, (int)Math.Round((x1 - x0) / 0.65));
        return [.. Enumerable.Range(1, n - 1).Select(i => { var x = x0 + (x1 - x0) * i / n; return new[] { new Vec2(x, -d / 2), new Vec2(x, back) }; })];
    }

    /// <summary>Triangles as a solid: coplanar neighbours merged into faces when they can be, else each triangle a face.</summary>
    public static Solid? MeshSolid(IReadOnlyList<Vec3> verts, IReadOnlyList<(int A, int B, int C)> tris)
    {
        if (tris.Count == 0) return null;
        if (tris.Count >= 4 && SolidFromMesh.Build(verts, tris, "furniture") is { } merged) return merged;
        var faces = new List<Face>();
        foreach (var (a, b, c) in tris)
        {
            var n = Vec3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (n.Length < 1e-14) continue;
            faces.Add(new Face([a, b, c], [], n.Normalized()));
        }
        return faces.Count == 0 ? null : new Solid([.. verts], [.. faces], "furniture");
    }

    /// <summary>Segments joined end to end into polylines (a straight run of them one segment).</summary>
    static List<Vec2[]> Chain(List<(Vec2 A, Vec2 B)> segs)
    {
        static (long, long) K(Vec2 p) => ((long)Math.Round(p.X * 1e5), (long)Math.Round(p.Y * 1e5));
        var at = new Dictionary<(long, long), List<int>>();
        for (var i = 0; i < segs.Count; i++)
            foreach (var p in new[] { segs[i].A, segs[i].B })
            {
                if (!at.TryGetValue(K(p), out var l)) at[K(p)] = l = [];
                l.Add(i);
            }
        var used = new bool[segs.Count];
        var o = new List<Vec2[]>();
        for (var i = 0; i < segs.Count; i++)
        {
            if (used[i]) continue;
            used[i] = true;
            var line = new LinkedList<Vec2>([segs[i].A, segs[i].B]);
            void Grow(bool atEnd)
            {
                while (true)
                {
                    var end = atEnd ? line.Last!.Value : line.First!.Value;
                    var next = at[K(end)].FirstOrDefault(j => !used[j], -1);
                    if (next < 0) return;
                    used[next] = true;
                    var far = K(segs[next].A) == K(end) ? segs[next].B : segs[next].A;
                    if (atEnd) line.AddLast(far); else line.AddFirst(far);
                }
            }
            Grow(true);
            Grow(false);
            o.Add(Straighten([.. line]));
        }
        return o;
    }

    /// <summary>A polyline without its points that lie on the straight line through their neighbours.</summary>
    static Vec2[] Straighten(Vec2[] p)
    {
        if (p.Length < 3) return p;
        var o = new List<Vec2> { p[0] };
        for (var i = 1; i < p.Length - 1; i++)
        {
            var (a, b, c) = (o[^1], p[i], p[i + 1]);
            if (Math.Abs(Vec2.Cross(b - a, c - a)) > 1e-7 * Math.Max(1e-6, (c - a).Length)) o.Add(b);
        }
        o.Add(p[^1]);
        return [.. o];
    }

    // ---- Materials ----

    /// <summary>The roles a type of the spec uses: its model's for a type of a model, else its kind's.</summary>
    public static IReadOnlyList<string> RolesOf(Spec s) => s.Kind.Key == Key ? Of(s).Roles : s.Kind.Roles;
}

/// <summary>A model's mesh read from its GLB: each slot's triangles in Lapin's frame (Z up, front −y), its low corner, its scale to its natural extent.</summary>
sealed record ModelMesh(IReadOnlyList<(string Slot, Vec3[] Vertices, (int A, int B, int C)[] Triangles)> Pieces, Vec3 Min, double[] Scale);

/// <summary>
/// The models' meshes, read once each. The only place that touches Lapin.Furniture.Models and Lapin.Mesh (both lazy
/// parts of the browser's engine): a project without such pieces never loads them (docs/plan-browser-engine.md).
/// </summary>
static class ModelMeshes
{
    static readonly Dictionary<string, ModelMesh?> Read = [];

    public static ModelMesh? Get(string id)
    {
        lock (Read)
        {
            if (Read.TryGetValue(id, out var m)) return m;
            return Read[id] = Load(id);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static ModelMesh? Load(string id)
    {
        var model = FurnitureModels.Find(id);
        var bytes = Lapin.Furniture.Models.FurnitureModelFiles.Read(id);
        if (model == null || bytes == null) return null;
        // glTF is Y up with its front along +Z: Lapin's frame is Z up with the front along −y.
        var pieces = Lapin.Mesh.MeshImport.Gltf(bytes, true, null, []);
        var list = pieces.Where(p => p.Triangles.Count > 0)
            .Select(p => (p.Material ?? "_defaultMat", p.Vertices.Select(v => new Vec3(v.X, -v.Z, v.Y)).ToArray(), p.Triangles.ToArray())).ToList();
        if (list.Count == 0) return null;
        var all = list.SelectMany(p => p.Item2).ToList();
        var min = new Vec3(all.Min(v => v.X), all.Min(v => v.Y), all.Min(v => v.Z));
        var ext = new[] { all.Max(v => v.X) - min.X, all.Max(v => v.Y) - min.Y, all.Max(v => v.Z) - min.Z };
        var scale = Enumerable.Range(0, 3).Select(k => ext[k] > 1e-9 ? model.Natural[k] / ext[k] : 1.0).ToArray();
        return new ModelMesh(list, min, scale);
    }
}
