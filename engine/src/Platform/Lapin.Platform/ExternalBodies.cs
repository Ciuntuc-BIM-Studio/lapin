using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Elements whose body an external program computes (docs/plan-external-geometry.md, step L1): <c>element.createBody</c>
/// and <c>element.setBody</c>, one undo step each. The body is a stored body (<see cref="StoredBodies"/>): a shared body
/// (<see cref="StoredBodies.SharedClass"/>) found by the SHA-256 of its content, so a body set on many elements is kept
/// once, and the element an instance of it, placed by its 12 numbers. Everything Lapin does with a reference it does with
/// one (drawn, cut in plans and sections, picked, moved, turned, copied, exported); the program that made it is its
/// <see cref="OwnerKey"/>, and an edit that would reshape it here is refused (<see cref="Refused"/>).
/// </summary>
public static class ExternalBodies
{
    /// <summary>Who made the body: <c>script:&lt;id&gt;</c> or the program's own words.</summary>
    public const string OwnerKey = "owner";
    /// <summary>The program's own attributes (an object), written to IFC with the intent and read back.</summary>
    public const string ExtraKey = "externalAttrs";
    /// <summary>A shared body's content hash (SHA-256 of its stored parts, lowercase hex).</summary>
    public const string HashKey = "contentHash";
    /// <summary>The owner an element made without one is given.</summary>
    public const string DefaultOwner = "script";

    public const int MaxTriangles = 2_000_000;
    public const int MaxParts = 1000;
    /// <summary>The farthest a body's point may lie from the project's origin (m).</summary>
    public const double MaxCoordinate = 10_000;
    /// <summary>How far a face's points may lie from its plane (m).</summary>
    public const double Planarity = 0.001;

    public const string Refused = "This shape is made by a script; change it in the script, or move, rotate or cut it here.";

    /// <summary>Whether the element's body is a program's (made by <c>element.createBody</c>).</summary>
    public static bool IsOwned(Entity e) => e.Attrs[OwnerKey] is JsonValue && StoredBodies.Has(e);

    public static void Register(IModuleRegistry r)
    {
        r.Command("element.createBody", "Create body", (ctx, a) => Create(ctx, a));
        r.Command("element.setBody", "Set body", (ctx, a) => Set(ctx, a));
        r.Service<IDrivenElements>(new Guard());
    }

    // ------------------------------------------------------------ the commands

    static JsonNode Create(ICommandContext ctx, JsonObject a)
    {
        var notes = new List<string>();
        var asked = ((string?)a["ifcClass"])?.Trim();
        var cls = IfcElementClasses.Resolve(asked);
        if (cls == null)
        {
            cls = IfcClass.BuildingElementProxy;
            if (!string.IsNullOrEmpty(asked)) notes.Add($"„{asked}” is not an IFC element class: the element is an {cls}.");
        }
        var storey = StoreyFrom(ctx, a["storey"] ?? a["storeyId"]);
        var id = ((string?)a["globalId"])?.Trim() is { Length: > 0 } gid ? Pinned(ctx.Model, gid) : ctx.NewId();
        var scale = UnitsOf(a["units"]);
        var body = Read(a["body"], scale);
        var xf = PlacementOf(a["placement"], scale) ?? Identity;
        var (sharedId, hash) = SharedFor(ctx, body);
        var attrs = new JsonObject
        {
            [StoredBodies.BodyOf] = sharedId,
            [StoredBodies.XfKey] = StoredBodies.WriteXf(xf),
            [OwnerKey] = ((string?)a["owner"])?.Trim() is { Length: > 0 and <= 300 } owner ? owner : DefaultOwner,
        };
        if (a["attrs"] is JsonObject extra) attrs[ExtraKey] = extra.DeepClone();
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : cls[3..];
        var e = new Entity(id, cls, name, attrs);
        ctx.Add(e);
        Spatial.Contain(ctx, storey.Id, e.Id);
        if ((string?)a["type"] is { Length: > 0 } typeId)
        {
            var type = ctx.Model.Get(typeId) ?? throw new CommandException($"There is no type {typeId}.");
            if (ctx.Describe(type.Class)?.Kind != "type" && !type.Class.EndsWith("Type", StringComparison.Ordinal)) throw new CommandException($"„{type.Name}” is not a type.");
            ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        }
        if ((string?)a["material"] is { Length: > 0 } material)
        {
            var m = ctx.Model.Get(material) is { Class: IfcClass.Material } byId ? byId : Materials.Ensure(ctx, material);
            ctx.Relate(IfcClass.RelAssociatesMaterial, m.Id, e.Id);
        }
        return Result(e.Id, sharedId, hash, body, notes);
    }

    static JsonNode Set(ICommandContext ctx, JsonObject a)
    {
        var id = (string?)a["id"] ?? throw new CommandException("Which element? (id)");
        var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
        if (!IsOwned(e)) throw new CommandException($"„{e.Name}” was not made by a script: its body is not set from outside.");
        var scale = UnitsOf(a["units"]);
        var body = Read(a["body"], scale);
        var xf = PlacementOf(a["placement"], scale) ?? (StoredBodies.InstanceOf(e) is var (_, was) ? was : Identity);
        var old = StoredBodies.InstanceOf(e)?.Body;
        var (sharedId, hash) = SharedFor(ctx, body);
        var attrs = e.CloneAttrs();
        attrs.Remove(StoredBodies.Key);
        attrs[StoredBodies.BodyOf] = sharedId;
        attrs[StoredBodies.XfKey] = StoredBodies.WriteXf(xf);
        ctx.Replace(e.With(attrs: attrs));
        // The body it had goes when no element uses it any more (as element.delete leaves none behind).
        if (old != null && old != sharedId && !ctx.Model.Entities.Any(x => x.Str(StoredBodies.BodyOf) == old)) ctx.Remove(old);
        return Result(e.Id, sharedId, hash, body, []);
    }

    static JsonObject Result(string id, string shared, string hash, Body body, List<string> notes) => new()
    {
        ["id"] = id, ["shared"] = shared, ["hash"] = hash, ["closed"] = body.Closed, ["triangles"] = body.Triangles,
        ["notes"] = new JsonArray([.. notes.Concat(body.Notes).Select(n => (JsonNode)n)]),
    };

    static Entity StoreyFrom(ICommandContext ctx, JsonNode? n)
    {
        if ((string?)n is not { Length: > 0 } s) return Spatial.EnsureStorey(ctx);
        if (ctx.Model.Get(s) is { Class: IfcClass.BuildingStorey } byId) return byId;
        return Spatial.Storeys(ctx.Model).FirstOrDefault(x => string.Equals(x.Name, s, StringComparison.OrdinalIgnoreCase))
            ?? throw new CommandException($"There is no storey „{s}”.");
    }

    /// <summary>A GlobalId the caller pins: an IFC GlobalId, and no entity or relationship of the model's has it.</summary>
    static string Pinned(IModelView m, string gid)
    {
        if (!GlobalId.IsValid(gid)) throw new CommandException($"„{gid}” is not an IFC GlobalId (22 characters of 0-9, A-Z, a-z, _ and $, the first 0-3).");
        if (m.Get(gid) is { } taken) throw new CommandException($"The GlobalId {gid} is taken by „{taken.Name}”.");
        if (m.GetRelation(gid) != null) throw new CommandException($"The GlobalId {gid} is taken by a relationship.");
        return gid;
    }

    /// <summary>The shared body holding <paramref name="body"/>: the one with its content hash, else a new one.</summary>
    static (string Id, string Hash) SharedFor(ICommandContext ctx, Body body)
    {
        var stored = StoredBodies.Write(body.Parts.Select(p => (p.Name, p.Color, (IReadOnlyList<Vec3>)p.Vertices, (IReadOnlyList<(int, int, int)>)p.Triangles)));
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(stored.ToJsonString())));
        if (ctx.Model.OfClass(StoredBodies.SharedClass).FirstOrDefault(x => x.Str(HashKey) == hash) is { } found) return (found.Id, hash);
        var shared = new Entity(ctx.NewId(), StoredBodies.SharedClass, "Script body (shared)", new JsonObject { [StoredBodies.Key] = stored, [HashKey] = hash, ["source"] = "script" });
        ctx.Add(shared);
        return (shared.Id, hash);
    }

    // ------------------------------------------------------------ units and placement

    /// <summary>
    /// Metres per unit the body and its placement are given in: <c>"m"</c> (the default, Lapin's unit) or <c>"mm"</c> (a
    /// program working in integer millimetres, as Lite-STEP does, sends its numbers as they are).
    /// </summary>
    public static double UnitsOf(JsonNode? n) => ((string?)n)?.Trim().ToLowerInvariant() switch
    {
        null or "" or "m" => 1,
        "mm" => 0.001,
        "cm" => 0.01,
        var u => throw new CommandException($"„{u}” is not a unit a body is given in (m, cm or mm)."),
    };

    static readonly double[] Identity = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0];

    /// <summary>
    /// The 12 numbers of a placement: a 4×4 matrix (16 numbers, row major, or 4 rows of 4; the last row 0 0 0 1), or
    /// <c>{at: [x, y, z], angle}</c> (degrees round Z). Null: none given.
    /// </summary>
    public static double[]? PlacementOf(JsonNode? n, double scale = 1)
    {
        switch (n)
        {
            case null: return null;
            case JsonObject o:
            {
                var at = o["at"] is JsonArray p ? Numbers(p, "placement.at") : [0, 0, 0];
                if (at.Length is not (2 or 3)) throw new CommandException("placement.at is [x, y] or [x, y, z].");
                var deg = Json.Number(o["angle"]) ?? 0;
                if (!double.IsFinite(deg)) throw new CommandException("placement.angle must be a number.");
                var (c, s) = (Math.Cos(deg * Math.PI / 180), Math.Sin(deg * Math.PI / 180));
                return Checked([c, s, 0, -s, c, 0, 0, 0, 1, at[0] * scale, at[1] * scale, (at.Length > 2 ? at[2] : 0) * scale]);
            }
            case JsonArray rows:
            {
                var m = rows.Count == 4 && rows.All(r => r is JsonArray) ? rows.SelectMany(r => Numbers((JsonArray)r!, "placement")).ToArray() : Numbers(rows, "placement");
                if (m.Length != 16) throw new CommandException("A placement matrix has 16 numbers (4 rows of 4).");
                if (Math.Abs(m[12]) > 1e-9 || Math.Abs(m[13]) > 1e-9 || Math.Abs(m[14]) > 1e-9 || Math.Abs(m[15] - 1) > 1e-9)
                    throw new CommandException("A placement matrix's last row is 0 0 0 1.");
                // Its columns are the images of X, Y and Z; its last column the origin's.
                return Checked([m[0], m[4], m[8], m[1], m[5], m[9], m[2], m[6], m[10], m[3] * scale, m[7] * scale, m[11] * scale]);
            }
            default: throw new CommandException("A placement is a 4×4 matrix or {at, angle}.");
        }
    }

    static double[] Checked(double[] xf)
    {
        var det = Vec3.Dot(Vec3.Cross(new Vec3(xf[0], xf[1], xf[2]), new Vec3(xf[3], xf[4], xf[5])), new Vec3(xf[6], xf[7], xf[8]));
        if (!(Math.Abs(det) > 1e-12)) throw new CommandException("The placement flattens the body (its matrix has no inverse).");
        if (Math.Abs(xf[9]) > MaxCoordinate || Math.Abs(xf[10]) > MaxCoordinate || Math.Abs(xf[11]) > MaxCoordinate)
            throw new CommandException($"The placement is more than {MaxCoordinate / 1000:0} km from the project's origin.");
        return xf;
    }

    static double[] Numbers(JsonArray a, string what)
    {
        var o = new double[a.Count];
        for (var i = 0; i < a.Count; i++)
            o[i] = Json.Number(a[i]) is { } d && double.IsFinite(d) ? d : throw new CommandException($"{what}: item {i} is not a number.");
        return o;
    }

    // ------------------------------------------------------------ the body

    public sealed record Part(string Name, string Color, List<Vec3> Vertices, List<(int A, int B, int C)> Triangles);

    /// <summary>A body read and checked: its parts, whether it is closed, its triangles, what was said of it.</summary>
    public sealed record Body(IReadOnlyList<Part> Parts, bool Closed, int Triangles, IReadOnlyList<string> Notes);

    /// <summary>
    /// A body as given: <c>{positions, indices}</c> (a mesh), <c>{faces: [{outer, holes?}]}</c> (planar polygons, points
    /// [x, y, z]), or <c>{parts: [{name?, color?, …one of those}]}</c>. Refused, saying why, when it is not one.
    /// </summary>
    public static Body Read(JsonNode? n, double scale = 1)
    {
        if (n is not JsonObject o) throw new CommandException("The body is missing: {positions, indices}, {faces} or {parts}.");
        var given = o["parts"] is JsonArray list ? list.OfType<JsonObject>().ToList() : [o];
        if (given.Count == 0) throw new CommandException("The body has no parts.");
        if (given.Count > MaxParts) throw new CommandException($"A body has at most {MaxParts} parts.");
        var parts = new List<Part>();
        var total = 0;
        for (var i = 0; i < given.Count; i++)
        {
            var p = given[i];
            var name = ((string?)p["name"])?.Trim() is { Length: > 0 } nm ? nm : given.Count == 1 ? "Body" : $"Part {i + 1}";
            var color = (string?)p["color"] is { } c && System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$") ? c.ToLowerInvariant() : "#c9ced6";
            // Polygons {faces: [{outer, holes}]}; else a mesh (faces beside vertices, as a mesh library names its triangles, are triangles).
            var (v, t) = p["faces"] is JsonArray faces && p["vertices"] == null ? Faces(faces, name, scale) : Mesh(p, name, scale);
            total += t.Count;
            if (total > MaxTriangles) throw new CommandException($"A body has at most {MaxTriangles:N0} triangles.");
            if (t.Count == 0) throw new CommandException($"{name} has no triangle with an area.");
            parts.Add(Weld(name, color, v, t));
        }
        var closed = parts.All(IsClosed);
        var notes = new List<string>();
        if (!closed) notes.Add("The body is not closed (an edge not shared by exactly two faces): it is drawn and exported, its volume is not measured.");
        return new Body(parts, closed, total, notes);
    }

    /// <summary>
    /// A triangle mesh: <c>positions</c> (or <c>vertices</c>) and <c>indices</c> (or <c>triangles</c>, or <c>faces</c> beside
    /// <c>vertices</c>), each flat (x y z …, a b c …) or one list per point and per triangle, as numpy's N×3 and M×3 arrays
    /// come out of a mesh library (manifold3d, trimesh, ifcopenshell's geometry).
    /// </summary>
    static (List<Vec3> V, List<(int, int, int)> T) Mesh(JsonObject p, string name, double scale)
    {
        if ((p["positions"] ?? p["vertices"]) is not JsonArray pos0 || (p["indices"] ?? p["triangles"] ?? p["faces"]) is not JsonArray idx0)
            throw new CommandException($"{name}: a mesh is {{positions: [x, y, z, …], indices: [a, b, c, …]}}.");
        var (pos, idx) = (Flat(pos0, name), Flat(idx0, name));
        if (pos.Count % 3 != 0) throw new CommandException($"{name}: positions hold x, y, z for each point ({pos.Count} numbers).");
        if (idx.Count % 3 != 0) throw new CommandException($"{name}: indices hold three points for each triangle ({idx.Count} numbers).");
        if (idx.Count / 3 > MaxTriangles) throw new CommandException($"A body has at most {MaxTriangles:N0} triangles.");
        var xyz = Numbers(pos, $"{name}: positions");
        var v = new List<Vec3>(xyz.Length / 3);
        for (var i = 0; i < xyz.Length; i += 3) v.Add(Point(xyz[i] * scale, xyz[i + 1] * scale, xyz[i + 2] * scale, name));
        var t = new List<(int, int, int)>(idx.Count / 3);
        for (var i = 0; i < idx.Count; i += 3)
        {
            int Ix(int k) => Json.Number(idx[k]) is { } d && d == Math.Floor(d) && d >= 0 && d < v.Count ? (int)d
                : throw new CommandException($"{name}: index {k} ({idx[k]?.ToJsonString()}) names no point (there are {v.Count}).");
            var (a, b, c) = (Ix(i), Ix(i + 1), Ix(i + 2));
            if (Vec3.Cross(v[b] - v[a], v[c] - v[a]).Length < 1e-14) continue;
            t.Add((a, b, c));
        }
        return (v, t);
    }

    /// <summary>A list as numbers one after the other: flat as it is, or rows of three.</summary>
    static JsonArray Flat(JsonArray a, string name)
    {
        if (a.Count == 0 || a[0] is not JsonArray) return a;
        var o = new JsonArray();
        foreach (var row in a)
        {
            if (row is not JsonArray { Count: 3 } r) throw new CommandException($"{name}: each row of a mesh's lists holds three numbers.");
            foreach (var x in r) o.Add(x?.DeepClone());
        }
        return o;
    }

    static (List<Vec3> V, List<(int, int, int)> T) Faces(JsonArray faces, string name, double scale)
    {
        var v = new List<Vec3>();
        var t = new List<(int, int, int)>();
        for (var f = 0; f < faces.Count; f++)
        {
            var face = faces[f] as JsonObject ?? throw new CommandException($"{name}: face {f} is {{outer, holes?}}.");
            var outer = Loop(face["outer"], $"{name}, face {f}", scale);
            var holes = (face["holes"] as JsonArray ?? []).Select((h, k) => Loop(h, $"{name}, face {f}, hole {k}", scale)).ToList();
            var normal = VecMath.NewellNormal(outer);
            if (normal.Length < 1e-12) throw new CommandException($"{name}: face {f} has no area.");
            normal = normal.Normalized();
            var origin = outer[0];
            foreach (var p in outer.Concat(holes.SelectMany(h => h)))
                if (Math.Abs(Vec3.Dot(p - origin, normal)) > Planarity) throw new CommandException($"{name}: face {f} is not planar (a point {Math.Abs(Vec3.Dot(p - origin, normal)) * 1000:0.#} mm off its plane).");
            var start = v.Count;
            v.AddRange(outer);
            var outerIx = Enumerable.Range(start, outer.Count).ToArray();
            var holeIx = new int[holes.Count][];
            for (var k = 0; k < holes.Count; k++)
            {
                holeIx[k] = Enumerable.Range(v.Count, holes[k].Count).ToArray();
                v.AddRange(holes[k]);
            }
            foreach (var tri in Triangulator.Face(v, outerIx, holeIx, normal)) t.Add(tri);
        }
        return (v, t);
    }

    static List<Vec3> Loop(JsonNode? n, string what, double scale)
    {
        if (n is not JsonArray pts || pts.Count < 3) throw new CommandException($"{what}: a loop is at least three points [x, y, z].");
        var o = new List<Vec3>(pts.Count);
        for (var i = 0; i < pts.Count; i++)
        {
            if (pts[i] is not JsonArray { Count: 3 } xyz) throw new CommandException($"{what}: point {i} is not [x, y, z].");
            var d = Numbers(xyz, what);
            o.Add(Point(d[0] * scale, d[1] * scale, d[2] * scale, what));
        }
        return o;
    }

    static Vec3 Point(double x, double y, double z, string what) =>
        Math.Abs(x) > MaxCoordinate || Math.Abs(y) > MaxCoordinate || Math.Abs(z) > MaxCoordinate
            ? throw new CommandException($"{what}: a point lies more than {MaxCoordinate / 1000:0} km from the origin; bodies are in metres.")
            : new Vec3(x, y, z);

    /// <summary>The points within 0.1 mm made one (as the stored body rounds them), the triangles over them, those left with no area dropped.</summary>
    static Part Weld(string name, string color, List<Vec3> v, List<(int A, int B, int C)> t)
    {
        var map = new Dictionary<(long, long, long), int>();
        var verts = new List<Vec3>();
        var ix = new int[v.Count];
        for (var i = 0; i < v.Count; i++)
        {
            var key = ((long)Math.Round(v[i].X * 1e4), (long)Math.Round(v[i].Y * 1e4), (long)Math.Round(v[i].Z * 1e4));
            if (!map.TryGetValue(key, out var k)) { k = map[key] = verts.Count; verts.Add(new Vec3(key.Item1 / 1e4, key.Item2 / 1e4, key.Item3 / 1e4)); }
            ix[i] = k;
        }
        var tris = new List<(int, int, int)>(t.Count);
        foreach (var (a, b, c) in t)
        {
            var (x, y, z) = (ix[a], ix[b], ix[c]);
            if (x == y || y == z || x == z || Vec3.Cross(verts[y] - verts[x], verts[z] - verts[x]).Length < 1e-14) continue;
            tris.Add((x, y, z));
        }
        return new Part(name, color, verts, tris);
    }

    /// <summary>Closed: every edge of a triangle met once the other way by another (a consistently wound, watertight surface).</summary>
    static bool IsClosed(Part p)
    {
        var edges = new Dictionary<(int, int), int>();
        foreach (var (a, b, c) in p.Triangles)
            foreach (var e in new[] { (a, b), (b, c), (c, a) })
                edges[e] = edges.GetValueOrDefault(e) + 1;
        foreach (var ((a, b), n) in edges)
            if (n != 1 || edges.GetValueOrDefault((b, a)) != 1) return false;
        return edges.Count > 0;
    }

    // ------------------------------------------------------------ quantities

    /// <summary>
    /// What a program's element measures (its stored body as built): the generic set <c>BaseQuantities</c>, with the
    /// volume of its closed parts (<c>NetVolume</c>, <c>GrossVolume</c>), its surface (<c>NetSurfaceArea</c>) and its
    /// bounding box (<c>Length</c>, <c>Width</c>, <c>Height</c>).
    /// </summary>
    public static IReadOnlyList<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) return [];
        var parts = g.Parts.Select(p => p.Solid).ToList();
        // Its body: the shared one it is an instance of, or its own (a mirrored copy, or one read back from IFC).
        var stored = StoredBodies.InstanceOf(e) is var (bodyId, _) ? m.Get(bodyId)?.Attrs[StoredBodies.Key] as JsonObject : e.Attrs[StoredBodies.Key] as JsonObject;
        var closed = stored != null && ClosedStored(stored);
        var area = parts.Sum(Measure.SurfaceArea);
        var boxes = parts.Select(Measure.Bounds).OfType<(Vec3 Min, Vec3 Max)>().ToList();
        var q = new List<Quantity>();
        if (closed)
        {
            var volume = parts.Sum(Measure.Volume);
            q.Add(new("NetVolume", volume, "VOLUME", "Net volume"));
            q.Add(new("GrossVolume", volume, "VOLUME", "Gross volume"));
        }
        q.Add(new("NetSurfaceArea", area, "AREA", "Surface area"));
        if (boxes.Count > 0)
        {
            var (lo, hi) = (new Vec3(boxes.Min(b => b.Min.X), boxes.Min(b => b.Min.Y), boxes.Min(b => b.Min.Z)), new Vec3(boxes.Max(b => b.Max.X), boxes.Max(b => b.Max.Y), boxes.Max(b => b.Max.Z)));
            q.Add(new("Length", hi.X - lo.X, "LENGTH", "Length (x)"));
            q.Add(new("Width", hi.Y - lo.Y, "LENGTH", "Width (y)"));
            q.Add(new("Height", hi.Z - lo.Z, "LENGTH", "Height"));
        }
        return [new QuantitySet("BaseQuantities", q)];
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<JsonObject, object> _closed = [];

    /// <summary>Whether a stored body's parts are all closed, worked out once for as long as it is the same body.</summary>
    static bool ClosedStored(JsonObject stored) => (bool)_closed.GetValue(stored, s =>
    {
        foreach (var p in (s["parts"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var (v, t) = StoredBodies.ReadPart(p);
            var tris = new List<(int, int, int)>();
            for (var i = 0; i + 2 < t.Length; i += 3) tris.Add((t[i], t[i + 1], t[i + 2]));
            if (!IsClosed(new Part("", "", [], tris))) return false;
        }
        return true;
    });

    // ------------------------------------------------------------ edits that would reshape it

    /// <summary>
    /// A program's element moves, turns, copies, goes to another storey, joins a group, is renamed, takes parameters and is
    /// deleted like any; its fields changed here (element.update) would be its shape's, which is the program's: refused.
    /// </summary>
    sealed class Guard : IDrivenElements
    {
        public bool Drives(IModelView m, Entity e) => IsOwned(e);

        public void Before(ICommandContext ctx, DrivenEdit edit, DrivenOutcome outcome)
        {
            if (edit.Command == "element.update") throw new CommandException(Refused);
        }
    }
}
