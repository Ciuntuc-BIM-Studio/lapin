using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// SubD solids (docs/plan-studio-freeform.md §8, decision 772, stage F1b): the operation <c>subd</c>, a cage of points, faces and
/// creases (<see cref="SubD"/>) in the solid's own frame (from <c>at</c>, turned by <c>rotation</c> about the upright, as a
/// primitive or a body), made into its body by the chord rule; <c>levels</c> caps its subdivisions, an open cage takes a
/// <c>thickness</c> on its <c>side</c> (out, in, centre), and <c>symmetry</c> (a plane in its own frame) keeps it mirrored: every
/// edit of one half is made on the other. Its steps, booleans, cut priorities, quantities and IFC body are a Studio solid's; IFC
/// keeps the cage in <see cref="SubDIfc.SetName"/>.
/// </summary>
public sealed partial class StudioModule
{
    public const string SubDOperation = "subd";

    /// <summary>The shapes a SubD solid starts from.</summary>
    public static readonly string[] SubDShapes = ["box", "cylinder", "plane", "fromSolid"];

    /// <summary>Where an open SubD's thickness lies.</summary>
    public static readonly string[] SubDSides = ["out", "in", "centre"];

    /// <summary>The most faces of a solid made into a cage.</summary>
    const int MaxCageFaces = 2000;

    static void RegisterSubD(IModuleRegistry r)
    {
        r.Command("subdCreate", "SubD solid", SubDCreate);
        r.Command("subdEdit", "Edit SubD cage", SubDEdit);
        r.Command("subdOp", "Shape SubD cage", SubDOp);
        r.Command("subdSet", "SubD settings", SubDSet);

        // The cage of a SubD solid for the 3D view's edit mode: its points in the world, faces, edges, creases, borders, its mirror
        // plane and each point's mirror partner, and what its body took (the level, how far it strays from the limit).
        r.Query("studio.subdCage", (q, a) =>
        {
            if (q.Model.Get((string?)a["id"] ?? "") is not { } e || e.Str("operation") != SubDOperation || CageOf(e.Attrs["cage"]) is not { } cage) return null;
            var (at, rot) = Frame(e);
            var built = SubDBuilt(e);
            var sym = SymmetryOf(e);
            var borders = new JsonArray();
            var uses = new Dictionary<(int, int), int>();
            foreach (var f in cage.Faces)
                for (var i = 0; i < f.Length; i++)
                {
                    var (x, y) = (f[i], f[(i + 1) % f.Length]);
                    var k = (Math.Min(x, y), Math.Max(x, y));
                    uses[k] = uses.GetValueOrDefault(k) + 1;
                }
            foreach (var ((x, y), n) in uses) if (n == 1) borders.Add((JsonNode)new JsonArray(x, y));
            return new JsonObject
            {
                ["id"] = e.Id,
                ["points"] = new JsonArray(cage.Points.Select(p => (JsonNode)Features.J(World(p, at, rot))).ToArray()),
                ["faces"] = new JsonArray(cage.Faces.Select(f => (JsonNode)new JsonArray(f.Select(k => (JsonNode)k).ToArray())).ToArray()),
                ["edges"] = new JsonArray(cage.Edges().Select(x => (JsonNode)new JsonArray(x.A, x.B)).ToArray()),
                ["creases"] = new JsonArray(cage.Creases.Select(c => (JsonNode)new JsonArray(c.A, c.B, c.Sharpness)).ToArray()),
                ["borders"] = borders,
                ["closed"] = cage.IsClosed,
                ["symmetry"] = sym is { } s ? new JsonObject { ["point"] = Features.J(World(s.Point, at, rot)), ["normal"] = Features.J(Turn(s.Normal, rot)) } : null,
                ["partners"] = sym is { } s2 ? new JsonArray(SubD.Partners(cage, s2.Point, s2.Normal).Select(k => (JsonNode)k).ToArray()) : null,
                ["level"] = built.Result?.Level,
                ["deviation"] = built.Result is { } br && double.IsFinite(br.Deviation) ? Math.Round(br.Deviation, 9) : null,
                ["levels"] = e.NumOrNull("levels"),
                ["thickness"] = e.Num("thickness"),
                ["side"] = e.Str("side") ?? "out",
                ["problem"] = built.Problem,
            };
        });
    }

    // ------------------------------------------------------------ the body

    sealed record Built(Solid? Solid, SubDResult? Result, string? Problem);

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<JsonObject, Built> _subd = [];

    /// <summary>The body a SubD solid's attributes make, in the world (its frame applied), kept while the attributes are the same.</summary>
    static Built SubDBuilt(Entity e)
    {
        if (_subd.TryGetValue(e.Attrs, out var known)) return known;
        Built made;
        if (CageOf(e.Attrs["cage"]) is not { } cage) made = new Built(null, null, "The SubD solid's cage is missing.");
        else
        {
            var r = SubD.Build(cage, OptionsOf(e), "studio");
            if (r.Solid == null) made = new Built(null, r, r.Problem ?? "The cage could not be subdivided.");
            else
            {
                var (at, rot) = Frame(e);
                made = new Built(SolidBuilder.Transform(r.Solid, p => World(p, at, rot)), r, null);
            }
        }
        _subd.AddOrUpdate(e.Attrs, made);
        return made;
    }

    static (Solid? Solid, string? Problem) SubDBody(Entity e)
    {
        var b = SubDBuilt(e);
        return (b.Solid, b.Problem);
    }

    /// <summary>An open SubD without a thickness: a surface, drawn but no solid (it cuts nothing and holds no volume).</summary>
    public static bool IsOpenSurface(Entity e) =>
        e.Str("operation") == SubDOperation && !(e.Num("thickness") > 0) && CageOf(e.Attrs["cage"]) is { IsClosed: false };

    static SubDOptions OptionsOf(Entity e) => new(
        MaxLevel: e.NumOrNull("levels") is { } l ? Math.Clamp((int)Math.Round(l), 1, SubD.MaxLevels) : SubD.DefaultLevels,
        Thickness: Math.Max(0, e.Num("thickness")),
        Side: SideOf(e.Str("side")));

    static SubDSide SideOf(string? s) => s switch { "in" => SubDSide.In, "centre" or "center" => SubDSide.Centre, _ => SubDSide.Out };

    // ------------------------------------------------------------ the cage as JSON, the frame

    /// <summary>A cage as kept: <c>{points: [[x, y, z]…], faces: [[i, j, k…]…], creases: [[a, b, sharpness]…]}</c>; null when malformed.</summary>
    public static SubDCage? CageOf(JsonNode? n)
    {
        if (n is not JsonObject o || o["points"] is not JsonArray pa || o["faces"] is not JsonArray fa) return null;
        var pts = new List<Vec3>(pa.Count);
        foreach (var p in pa) { if (Features.P(p) is not { } v) return null; pts.Add(v); }
        var faces = new List<int[]>(fa.Count);
        foreach (var f in fa)
        {
            if (f is not JsonArray l) return null;
            var loop = new int[l.Count];
            for (var i = 0; i < l.Count; i++) { if (Json.Number(l[i]) is not { } k) return null; loop[i] = (int)k; }
            faces.Add(loop);
        }
        var creases = new List<SubDCrease>();
        foreach (var c in o["creases"] as JsonArray ?? [])
            if (c is JsonArray { Count: >= 3 } x && Json.Number(x[0]) is { } a && Json.Number(x[1]) is { } b && Json.Number(x[2]) is { } s)
                creases.Add(new SubDCrease((int)a, (int)b, s));
        return new SubDCage(pts, faces, creases);
    }

    public static JsonObject CageJson(SubDCage c) => new()
    {
        ["points"] = new JsonArray(c.Points.Select(p => (JsonNode)Features.J(p)).ToArray()),
        ["faces"] = new JsonArray(c.Faces.Select(f => (JsonNode)new JsonArray(f.Select(k => (JsonNode)k).ToArray())).ToArray()),
        ["creases"] = new JsonArray(c.Creases.Select(x => (JsonNode)new JsonArray(x.A, x.B, Math.Round(Math.Min(x.Sharpness, SubD.Infinite), 9))).ToArray()),
    };

    static (Vec3 At, double Rot) Frame(Entity e) => (Features.P(e.Attrs["at"]) ?? Vec3.Zero, e.Num("rotation") * Math.PI / 180);

    static Vec3 Turn(Vec3 p, double rot) => new(p.X * Math.Cos(rot) - p.Y * Math.Sin(rot), p.X * Math.Sin(rot) + p.Y * Math.Cos(rot), p.Z);
    static Vec3 World(Vec3 p, Vec3 at, double rot) => at + Turn(p, rot);
    static Vec3 Local(Vec3 p, Vec3 at, double rot) => Turn(p - at, -rot);

    /// <summary>The mirror plane a SubD keeps, in its own frame: <c>symmetry: {point, normal}</c>.</summary>
    static (Vec3 Point, Vec3 Normal)? SymmetryOf(Entity e) =>
        e.Attrs["symmetry"] is JsonObject s && Features.P(s["point"]) is { } p && Features.P(s["normal"]) is { } n && n.Length > 1e-12 ? (p, n.Normalized()) : null;

    static Entity SubDSolid(IModelView m, string? id) =>
        m.Get(id ?? "") is { } e && e.Str("operation") == SubDOperation ? e : throw new CommandException("Not a SubD solid.");

    /// <summary>The cage written back to the solid (same id, same GlobalId), its body checked first.</summary>
    static void WriteCage(ICommandContext ctx, Entity e, SubDCage cage, Action<JsonObject>? more = null)
    {
        if (SubD.Problem(cage) is { } p) throw new CommandException(p);
        var attrs = e.CloneAttrs();
        attrs["cage"] = CageJson(cage);
        more?.Invoke(attrs);
        var probe = new Entity(e.Id, e.Class, e.Name, attrs, e.PredefinedType);
        if (SubD.Build(cage, OptionsOf(probe)).Problem is { } bp) throw new CommandException(bp);
        ctx.Replace(e.With(attrs: attrs));
    }

    // ------------------------------------------------------------ commands

    /// <summary>
    /// A SubD solid: <c>{shape: box | cylinder | plane | fromSolid, at: [x, y] or [x, y, z], storeyId?, rotation?, class?, name?}</c>
    /// and its sizes: a box's <c>width</c>, <c>length</c>, <c>height</c> (or <c>sizes: [w, l, h]</c>) and <c>segments</c> (a number
    /// or [nx, ny, nz]); a cylinder's <c>radius</c>, <c>height</c> (<c>sizes: [r, h]</c>), <c>sides</c> and <c>segments</c> up; a
    /// plane's <c>width</c>, <c>length</c> (<c>sizes: [w, l]</c>) and <c>segments</c> [nx, ny]; <c>fromSolid</c> the faces of
    /// element <c>sourceId</c> as they are built now (it stays). Also <c>thickness</c>, <c>side</c>, <c>levels</c>.
    /// </summary>
    static JsonNode? SubDCreate(ICommandContext ctx, JsonObject a)
    {
        var shape = ((string?)a["shape"])?.Trim() ?? "box";
        if (!SubDShapes.Contains(shape)) throw new CommandException($"„{shape}” is not a SubD shape ({string.Join(", ", SubDShapes)}).");
        var sizes = (a["sizes"] as JsonArray)?.Select(Json.Number).ToList() ?? [];
        double Size(string key, double? given, int at, double fallback)
        {
            var v = given ?? (at < sizes.Count ? sizes[at] : null) ?? fallback;
            if (!(v > 0)) throw new CommandException($"The {key} of a SubD {shape} is more than 0.");
            return v;
        }
        var segs = a["segments"] is JsonArray sa ? sa.Select(x => (int)Math.Round(Json.Number(x) ?? 1)).ToList()
            : Json.Number(a["segments"]) is { } one ? [(int)Math.Round(one)] : [];
        int Seg(int i, int fallback) { var n = i < segs.Count ? segs[i] : segs.Count == 1 ? segs[0] : fallback; return Math.Clamp(n, 1, 64); }
        Entity? storey = (string?)a["storeyId"] is { Length: > 0 } sid ? Spatial.Require(ctx.Model, sid, IfcClass.BuildingStorey) : null;
        SubDCage cage;
        Vec3 at = default;
        switch (shape)
        {
            case "box": cage = SubDCages.Box(Size("width", Json.Number(a["width"]), 0, 1), Size("length", Json.Number(a["length"]), 1, 1), Size("height", Json.Number(a["height"]), 2, 1), Seg(0, 1), Seg(1, 1), Seg(2, 1)); break;
            case "cylinder":
                var sides = (int)Math.Round(Json.Number(a["sides"]) ?? 8);
                if (sides is < 3 or > 128) throw new CommandException("A SubD cylinder has 3 to 128 sides.");
                cage = SubDCages.Cylinder(Size("radius", Json.Number(a["radius"]), 0, 0.5), Size("height", Json.Number(a["height"]), 1, 1), sides, Seg(0, 1));
                break;
            case "plane": cage = SubDCages.Plane(Size("width", Json.Number(a["width"]), 0, 2), Size("length", Json.Number(a["length"]), 1, 2), Seg(0, 4), Seg(1, 4)); break;
            default:
            {
                var source = ctx.Model.Get((string?)a["sourceId"] ?? (string?)a["id"] ?? "") ?? throw new CommandException("Pick the solid whose faces make the cage.");
                var parts = ctx.Query.Geometry(source.Id)?.Parts ?? [];
                if (parts.Count == 0) throw new CommandException($"„{source.Name}” has no body to make a cage of.");
                var verts = new List<Vec3>();
                var faces = new List<int[]>();
                foreach (var p in parts)
                {
                    if (p.Solid.Faces.Any(f => f.Holes.Length > 0)) throw new CommandException("A face of that body has a hole: a cage's faces have none.");
                    var off = verts.Count;
                    verts.AddRange(p.Solid.Vertices);
                    faces.AddRange(p.Solid.Faces.Select(f => f.Outer.Select(k => k + off).ToArray()));
                }
                var (made, problem) = SubDCages.FromSolid(new Solid([.. verts], [.. faces.Select(f => new Face(f, [], Vec3.UnitZ))]), MaxCageFaces);
                if (made == null) throw new CommandException(problem!);
                at = new Vec3((made.Points.Min(p => p.X) + made.Points.Max(p => p.X)) / 2, (made.Points.Min(p => p.Y) + made.Points.Max(p => p.Y)) / 2, made.Points.Min(p => p.Z));
                var origin = at;
                cage = SubDCages.Move(made, Enumerable.Range(0, made.Points.Length), p => p - origin);
                storey ??= Spatial.StoreyOf(ctx.Model, source.Id);
                break;
            }
        }
        if (shape != "fromSolid")
        {
            storey ??= Spatial.Storeys(ctx.Model).FirstOrDefault() ?? throw new CommandException("The project has no storey.");
            if (a["at"] is not JsonArray xy || xy.Count < 2) throw new CommandException("A SubD solid needs the point it stands on [x, y].");
            at = new Vec3(Json.Number(xy[0]) ?? 0, Json.Number(xy[1]) ?? 0, xy.Count > 2 ? Json.Number(xy[2]) ?? storey.Num("elevation") : storey.Num("elevation"));
        }
        storey ??= Spatial.Storeys(ctx.Model).FirstOrDefault() ?? throw new CommandException("The project has no storey.");
        var (cls, exportAs) = ClassOf((string?)a["class"]);
        var attrs = new JsonObject { ["operation"] = SubDOperation, ["cage"] = CageJson(cage), ["at"] = Features.J(at), ["rotation"] = Json.Number(a["rotation"]) ?? 0 };
        if (exportAs != null) attrs["exportAs"] = exportAs;
        SettingsInto(attrs, a);
        var e = new Entity(ctx.NewId(), cls, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"SubD {ctx.Model.Entities.Count(x => x.Str("operation") == SubDOperation) + 1}", attrs);
        if (SubD.Build(cage, OptionsOf(e)).Problem is { } bp) throw new CommandException(bp);
        ctx.Add(e);
        Spatial.Contain(ctx, storey.Id, e.Id);
        return new JsonObject { ["id"] = e.Id };
    }

    /// <summary>The settings given (<c>levels</c>, <c>thickness</c>, <c>side</c>) checked and written; null clears levels or thickness.</summary>
    static void SettingsInto(JsonObject attrs, JsonObject a)
    {
        if (a.ContainsKey("levels"))
        {
            if (a["levels"] == null) attrs.Remove("levels");
            else if (Json.Number(a["levels"]) is { } l && l >= 1 && l <= SubD.MaxLevels) attrs["levels"] = Math.Round(l);
            else throw new CommandException($"A SubD is subdivided 1 to {SubD.MaxLevels} times at most.");
        }
        if (a.ContainsKey("thickness"))
        {
            var t = a["thickness"] == null ? 0 : Json.Number(a["thickness"]) ?? throw new CommandException("The thickness is a length.");
            if (t < 0) throw new CommandException("The thickness is 0 (a surface) or more.");
            if (t > 0) attrs["thickness"] = t; else attrs.Remove("thickness");
        }
        if ((string?)a["side"] is { } side)
        {
            if (!SubDSides.Contains(side)) throw new CommandException($"The thickness lies {string.Join(", ", SubDSides)} of the surface.");
            attrs["side"] = side;
        }
    }

    /// <summary>The points a selection names: <c>vertices</c> [i…], <c>edges</c> [[a, b]…], <c>faces</c> [f…].</summary>
    static List<int> Selected(SubDCage cage, JsonObject a)
    {
        static List<int> Ints(JsonNode? n) => (n as JsonArray)?.Select(x => Json.Number(x) is { } d ? (int)d : -1).ToList() ?? [];
        try { return SubDCages.PointsOf(cage, Ints(a["vertices"]), Pairs(a["edges"]), Ints(a["faces"])); }
        catch (ArgumentException ex) { throw new CommandException(ex.Message); }
    }

    static List<(int A, int B)> Pairs(JsonNode? n) =>
        (n as JsonArray)?.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => ((int)(Json.Number(p[0]) ?? -1), (int)(Json.Number(p[1]) ?? -1))).ToList() ?? [];

    /// <summary>
    /// Cage points moved in the world, about their middle: <c>{id, vertices | edges | faces, scale?: s or [sx, sy, sz],
    /// rotate?: degrees about the upright or {axis: [x, y, z], angle}, translate?: [dx, dy, dz]}</c>, in that order. A symmetric
    /// cage moves the mirror images too, and keeps its points on the plane on it.
    /// </summary>
    static JsonNode? SubDEdit(ICommandContext ctx, JsonObject a)
    {
        var e = SubDSolid(ctx.Model, (string?)a["id"]);
        var cage = CageOf(e.Attrs["cage"]) ?? throw new CommandException("The SubD solid's cage is missing.");
        var picked = Selected(cage, a);
        if (picked.Count == 0) throw new CommandException("Pick the cage's points, edges or faces to move.");
        var (at, rot) = Frame(e);
        var middle = picked.Aggregate(Vec3.Zero, (s, k) => s + World(cage.Points[k], at, rot)) * (1.0 / picked.Count);
        Vec3 scale = new(1, 1, 1);
        if (a["scale"] is JsonArray sc && Features.P(sc) is { } s3) scale = s3;
        else if (Json.Number(a["scale"]) is { } s1) scale = new Vec3(s1, s1, s1);
        if (!(Math.Abs(scale.X) > 1e-9 && Math.Abs(scale.Y) > 1e-9 && Math.Abs(scale.Z) > 1e-9)) throw new CommandException("Scale by more than nothing.");
        Vec3 axis = Vec3.UnitZ;
        var angle = 0.0;
        if (a["rotate"] is JsonObject ro)
        {
            angle = Json.Number(ro["angle"]) ?? 0;
            if (Features.P(ro["axis"]) is { } ax && ax.Length > 1e-12) axis = ax.Normalized();
        }
        else if (Json.Number(a["rotate"]) is { } deg) angle = deg;
        var move = a["translate"] is { } tr ? Features.P(tr) ?? throw new CommandException("Move by [dx, dy, dz].") : Vec3.Zero;
        Vec3 Fn(Vec3 w)
        {
            var d = w - middle;
            d = new Vec3(d.X * scale.X, d.Y * scale.Y, d.Z * scale.Z);
            if (Math.Abs(angle) > 1e-12) d = Features.Turn(middle + d, middle, axis, angle * Math.PI / 180) - middle;
            return middle + d + move;
        }
        Vec3 LocalFn(Vec3 p) => Local(Fn(World(p, at, rot)), at, rot);
        var pts = (Vec3[])cage.Points.Clone();
        if (SymmetryOf(e) is { } sym)
        {
            // Moved as one half: the side most of the picked points are on; a point picked on the other side moves its partner by
            // the mirrored move, and every point of that half then sets its partner's place.
            var partners = SubD.Partners(cage, sym.Point, sym.Normal);
            double D(Vec3 p) => Vec3.Dot(p - sym.Point, sym.Normal);
            Vec3 M(Vec3 p) => p - sym.Normal * (2 * D(p));
            var side = Math.Sign(picked.Sum(k => Math.Sign(Math.Round(D(cage.Points[k]), 9))));
            if (side == 0) side = 1;
            foreach (var k in picked)
            {
                var d = D(cage.Points[k]);
                if (d * side < -1e-6 && partners[k] >= 0 && !picked.Contains(partners[k])) pts[partners[k]] = M(LocalFn(M(cage.Points[k])));
                else if (d * side >= -1e-6) pts[k] = LocalFn(cage.Points[k]);
            }
            for (var k = 0; k < pts.Length; k++)
            {
                var d = D(cage.Points[k]);
                if (Math.Abs(d) <= 1e-6) pts[k] -= sym.Normal * D(pts[k]);
                else if (d * side > 0 && partners[k] >= 0) pts[partners[k]] = M(pts[k]);
            }
        }
        else foreach (var k in picked) pts[k] = LocalFn(cage.Points[k]);
        WriteCage(ctx, e, new SubDCage(pts, cage.Faces, cage.Creases));
        return new JsonObject { ["moved"] = picked.Count };
    }

    /// <summary>
    /// The cage shaped: <c>{id, op: extrude, faces, distance}</c> (along their normal), <c>{op: loop, edge: [a, b], fraction?}</c>
    /// (an edge loop across the ring of quads that edge starts, cut at that fraction from a; ½ by default), <c>{op: delete, faces}</c>
    /// (the cage opens there), <c>{op: crease, edges: [[a, b]…], sharpness}</c> (0 smooth; 10 or more sharp for good). A
    /// symmetric cage is mirrored again from the half the edit was on. The points and faces are numbered anew.
    /// </summary>
    static JsonNode? SubDOp(ICommandContext ctx, JsonObject a)
    {
        var e = SubDSolid(ctx.Model, (string?)a["id"]);
        var cage = CageOf(e.Attrs["cage"]) ?? throw new CommandException("The SubD solid's cage is missing.");
        static List<int> Ints(JsonNode? n) => (n as JsonArray)?.Select(x => Json.Number(x) is { } d ? (int)d : -1).ToList() ?? [];
        var op = (string?)a["op"] ?? "";
        List<int> touched;
        (SubDCage? Cage, string? Problem) r;
        switch (op)
        {
            case "extrude":
            {
                var faces = Ints(a["faces"]);
                var d = Json.Number(a["distance"]) ?? throw new CommandException("How far to extrude: a distance (negative: in).");
                touched = Selected(cage, new JsonObject { ["faces"] = new JsonArray(faces.Select(x => (JsonNode)x).ToArray()) });
                r = SubDCages.Extrude(cage, faces, d);
                break;
            }
            case "loop":
            {
                if (Pairs(new JsonArray(a["edge"]?.DeepClone())) is not [var edge]) throw new CommandException("Pick the edge the loop runs across: [a, b].");
                touched = [edge.A, edge.B];
                r = SubDCages.InsertLoop(cage, edge.A, edge.B, Json.Number(a["fraction"]) ?? 0.5);
                break;
            }
            case "delete":
            {
                var faces = Ints(a["faces"]);
                touched = Selected(cage, new JsonObject { ["faces"] = new JsonArray(faces.Select(x => (JsonNode)x).ToArray()) });
                r = SubDCages.DeleteFaces(cage, faces);
                break;
            }
            case "crease":
            {
                var edges = Pairs(a["edges"]);
                touched = edges.SelectMany(x => new[] { x.A, x.B }).Where(k => k >= 0 && k < cage.Points.Length).ToList();
                var s = a["sharpness"] == null ? SubD.Infinite : Json.Number(a["sharpness"]) ?? throw new CommandException("A crease's sharpness is a number (0 smooth, 10 or more sharp for good).");
                r = SubDCages.SetCreases(cage, edges, s);
                break;
            }
            default: throw new CommandException($"„{op}” is not a cage edit (extrude, loop, delete, crease).");
        }
        if (r.Cage == null) throw new CommandException(r.Problem ?? "The cage could not be changed so.");
        var next = r.Cage;
        if (SymmetryOf(e) is { } sym)
        {
            double D(Vec3 p) => Vec3.Dot(p - sym.Point, sym.Normal);
            var side = Math.Sign(touched.Sum(k => Math.Sign(Math.Round(D(cage.Points[k]), 9))));
            var (whole, problem) = SubD.Mirror(next, sym.Point, side < 0 ? -sym.Normal : sym.Normal);
            next = whole ?? throw new CommandException(problem!);
        }
        WriteCage(ctx, e, next);
        return new JsonObject { ["points"] = next.Points.Length, ["faces"] = next.Faces.Length };
    }

    /// <summary>
    /// A SubD solid's settings: <c>{id, levels?: 1…6 or null, thickness?: m or null, side?: out | in | centre, symmetry?: x | y | z
    /// (its own axes' planes through its point), {point, normal} (in the world) or null, keep?: positive | negative}</c>. A
    /// symmetry set mirrors the cage at once from the half on the plane's positive side (or the other, <c>keep: negative</c>).
    /// </summary>
    static JsonNode? SubDSet(ICommandContext ctx, JsonObject a)
    {
        var e = SubDSolid(ctx.Model, (string?)a["id"]);
        var cage = CageOf(e.Attrs["cage"]) ?? throw new CommandException("The SubD solid's cage is missing.");
        var (at, rot) = Frame(e);
        (Vec3 Point, Vec3 Normal)? plane = null;
        var clear = false;
        if (a.ContainsKey("symmetry"))
        {
            switch (a["symmetry"])
            {
                case null: clear = true; break;
                case JsonValue v when v.TryGetValue<string>(out var axis):
                    plane = axis.ToLowerInvariant() switch
                    {
                        "x" => (Vec3.Zero, new Vec3(1, 0, 0)),
                        "y" => (Vec3.Zero, new Vec3(0, 1, 0)),
                        "z" => (Vec3.Zero, Vec3.UnitZ),
                        "none" or "" => null,
                        _ => throw new CommandException("A symmetry plane is x, y, z, a {point, normal} or none."),
                    };
                    clear = plane == null;
                    break;
                case JsonObject o when Features.P(o["point"]) is { } p && Features.P(o["normal"]) is { } n && n.Length > 1e-12:
                    plane = (Local(p, at, rot), Turn(n.Normalized(), -rot));
                    break;
                default: throw new CommandException("A symmetry plane is x, y, z, a {point, normal} or none.");
            }
        }
        var next = cage;
        if (plane is { } pl)
        {
            var normal = (string?)a["keep"] == "negative" ? -pl.Normal : pl.Normal;
            var (whole, problem) = SubD.Mirror(cage, pl.Point, normal);
            next = whole ?? throw new CommandException(problem!);
        }
        WriteCage(ctx, e, next, attrs =>
        {
            SettingsInto(attrs, a);
            if (clear) attrs.Remove("symmetry");
            if (plane is { } q) attrs["symmetry"] = new JsonObject { ["point"] = Features.J(q.Point), ["normal"] = Features.J(q.Normal) };
        });
        return null;
    }
}

/// <summary>
/// A SubD solid in IFC (decision 772): its body tessellated as any Studio solid's, its cage in Lapin's own set
/// <see cref="SetName"/> (property <c>Cage</c>, the JSON the solid keeps) rather than in its intent set, so Lapin and BubbleBIM
/// read it back editable.
/// </summary>
public sealed class SubDIfc : IIfcElementSets, IIfcExportFilter
{
    public const string SetName = "Lapin_SubD";

    public IEnumerable<IfcStandardSet> Sets(IModelView model, Entity element)
    {
        if (element.Str("operation") != StudioModule.SubDOperation || element.Attrs["cage"] is not JsonObject cage) yield break;
        yield return new IfcStandardSet(SetName, [new IfcStandardProperty("Cage", "IFCTEXT", cage.ToJsonString(), "The SubD cage: points (m, in the solid's own frame), faces, creases (a, b, sharpness).")]);
    }

    public bool IsPrivate(Entity element, string key) => key == "cage" && element.Str("operation") == StudioModule.SubDOperation;
}
