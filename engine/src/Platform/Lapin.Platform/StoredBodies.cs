using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// An element whose body came with it (a reference from an IFC file): the
/// triangles are kept in its <c>body</c> attribute, in metres, world
/// coordinates, and drawn as they are. Such an element is shown, measured and
/// snapped to, never edited; its class stays what the file said. An element
/// the file gave only a 2D representation has no parts (<see cref="Plan2d"/>).
/// <para>
/// Or its body is shared (docs/plan-instancing.md): a <see cref="SharedClass"/>
/// holds it once, in its own coordinates, and the element names it
/// (<see cref="BodyOf"/>) with where it stands (<see cref="XfKey"/>: 12
/// numbers, the images of the X, Y and Z axes and of the origin).
/// </para>
/// </summary>
public static class StoredBodies
{
    public const string Key = "body";
    public const string SharedClass = "LapinSharedBody";
    public const string BodyOf = "bodyOf";
    public const string XfKey = "xf";

    /// <summary>An element drawn from a stored body: its own, or a shared one.</summary>
    public static bool Has(Entity e) => e.Class != SharedClass && (e.Attrs[Key] is JsonObject || e.Attrs[BodyOf] is JsonValue);

    /// <summary>The shared body an element is an instance of, and where it stands.</summary>
    public static (string Body, double[] Xf)? InstanceOf(Entity e)
    {
        if ((string?)e.Attrs[BodyOf] is not { } body || e.Attrs[XfKey] is not JsonArray a || a.Count != 12) return null;
        var xf = new double[12];
        for (var i = 0; i < 12; i++) xf[i] = Json.Number(a[i]) ?? 0;
        return (body, xf);
    }

    public static JsonArray WriteXf(IReadOnlyList<double> xf) => new([.. xf.Select(v => (JsonNode)Math.Round(v, 9))]);

    /// <summary>A point placed by an instance's 12 numbers.</summary>
    public static Vec3 Apply(double[] m, Vec3 p) =>
        new(m[9] + m[0] * p.X + m[3] * p.Y + m[6] * p.Z, m[10] + m[1] * p.X + m[4] * p.Y + m[7] * p.Z, m[11] + m[2] * p.X + m[5] * p.Y + m[8] * p.Z);

    /// <summary>
    /// The parts as the attribute holds them: coordinates to 0,1 mm, triangles by vertex index, each list
    /// one base64 string (<c>v64</c>: doubles, <c>t64</c>: ints, little-endian). A number per JSON node
    /// was some fifty bytes: a 6.8 million triangle body was 31 million nodes. Bodies written before
    /// (<c>v</c>, <c>t</c> as arrays) are read as they are (<see cref="ReadPart"/>).
    /// </summary>
    public static JsonObject Write(IEnumerable<(string Name, string Color, IReadOnlyList<Vec3> Vertices, IReadOnlyList<(int A, int B, int C)> Triangles)> parts)
    {
        var list = new JsonArray();
        foreach (var p in parts)
        {
            var v = new double[p.Vertices.Count * 3];
            for (var i = 0; i < p.Vertices.Count; i++) { var q = p.Vertices[i]; v[3 * i] = Math.Round(q.X, 4); v[3 * i + 1] = Math.Round(q.Y, 4); v[3 * i + 2] = Math.Round(q.Z, 4); }
            var t = new int[p.Triangles.Count * 3];
            for (var i = 0; i < p.Triangles.Count; i++) { var (a, b, c) = p.Triangles[i]; t[3 * i] = a; t[3 * i + 1] = b; t[3 * i + 2] = c; }
            list.Add((JsonNode)WritePart(p.Name, p.Color, v, t));
        }
        return new JsonObject { ["parts"] = list };
    }

    /// <summary>One part: its name, colour, coordinates (x y z …) and triangles (a b c …).</summary>
    public static JsonObject WritePart(string name, string color, double[] v, int[] t) => new()
    {
        ["name"] = name, ["color"] = color,
        ["v64"] = Convert.ToBase64String(System.Runtime.InteropServices.MemoryMarshal.AsBytes(v.AsSpan())),
        ["t64"] = Convert.ToBase64String(System.Runtime.InteropServices.MemoryMarshal.AsBytes(t.AsSpan())),
    };

    /// <summary>A part's coordinates and triangles, as written now (base64) or before (arrays).</summary>
    public static (double[] V, int[] T) ReadPart(JsonObject p)
    {
        double[] v;
        if ((string?)p["v64"] is { } v64) v = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, double>(Convert.FromBase64String(v64)).ToArray();
        else v = (p["v"] as JsonArray)?.Select(x => Json.Number(x) ?? 0).ToArray() ?? [];
        int[] t;
        if ((string?)p["t64"] is { } t64) t = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, int>(Convert.FromBase64String(t64)).ToArray();
        else t = (p["t"] as JsonArray)?.Select(x => (int)(Json.Number(x) ?? -1)).ToArray() ?? [];
        return (v, t);
    }

    /// <summary>The stored body as geometry: a solid of triangles per part; an instance's, the shared parts placed.</summary>
    public static ElementGeometry? Geometry(Entity e, IModelView model, IServices services)
    {
        if (InstanceOf(e) is var (bodyId, xf))
        {
            if (model.Get(bodyId) is not { Class: SharedClass } shared) return new ElementGeometry([], null, null, [$"The shared body {bodyId} is missing."]);
            var placed = SharedParts(shared).Select(p => p with { Solid = Place(p.Solid, xf) }).ToList();
            return new ElementGeometry(placed, Plan2d.Has(e) ? Plan2d.PlanItems(e) : null, Instance: new SharedInstance(bodyId, xf));
        }
        if (e.Attrs[Key] is not JsonObject body) return null;
        return new ElementGeometry(_shared.GetValue(body, b => Parts(b)), Plan2d.Has(e) ? Plan2d.PlanItems(e) : null);
    }

    /// <summary>
    /// The parts of every stored body, built once for as long as it is the same body: a snapshot, an opened
    /// project and every rebuild took them again from the triangles (a 6.8 million triangle Revit body, 40 s each time).
    /// </summary>
    static readonly ConditionalWeakTable<JsonObject, List<GeometryPart>> _shared = [];

    /// <summary>Above this many triangles a part is read face by face as it is: merging coplanar ones costs more than it gives on a curved mesh that big.</summary>
    const int MergeUpTo = 100_000;

    /// <summary>A shared body's parts in its own coordinates, built once for as long as the body is the same.</summary>
    public static IReadOnlyList<GeometryPart> SharedParts(Entity shared) =>
        shared.Attrs[Key] is JsonObject body ? _shared.GetValue(body, b => Parts(b)) : [];

    /// <summary>
    /// A solid placed by an instance's numbers. A move alone keeps its faces; a turn
    /// turns their normals; any other map (a scale) derives them again.
    /// </summary>
    public static Solid Place(Solid s, double[] m)
    {
        var verts = new Vec3[s.Vertices.Length];
        for (var i = 0; i < verts.Length; i++) verts[i] = Apply(m, s.Vertices[i]);
        Vec3 x = new(m[0], m[1], m[2]), y = new(m[3], m[4], m[5]), z = new(m[6], m[7], m[8]);
        const double eps = 1e-9;
        if ((x - new Vec3(1, 0, 0)).Length < eps && (y - new Vec3(0, 1, 0)).Length < eps && (z - new Vec3(0, 0, 1)).Length < eps)
            return s with { Vertices = verts };
        var rigid = Math.Abs(x.Length - 1) < 1e-6 && Math.Abs(y.Length - 1) < 1e-6 && Math.Abs(z.Length - 1) < 1e-6
            && Math.Abs(Vec3.Dot(x, y)) < 1e-6 && Math.Abs(Vec3.Dot(y, z)) < 1e-6 && Math.Abs(Vec3.Dot(x, z)) < 1e-6
            && Vec3.Dot(Vec3.Cross(x, y), z) > 0;
        if (rigid)
            return s with { Vertices = verts, Faces = [.. s.Faces.Select(f => f with { Normal = x * f.Normal.X + y * f.Normal.Y + z * f.Normal.Z })] };
        return SolidBuilder.Transform(s, p => Apply(m, p));
    }

    static List<GeometryPart> Parts(JsonObject body)
    {
        var list = new List<GeometryPart>();
        if (body["parts"] is not JsonArray parts) return list;
        foreach (var p in parts.OfType<JsonObject>())
        {
            var (v, t) = ReadPart(p);
            var vertices = new Vec3[v.Length / 3];
            for (var k = 0; k < vertices.Length; k++) vertices[k] = new Vec3(v[3 * k], v[3 * k + 1], v[3 * k + 2]);
            // Coplanar triangles merged into faces where the mesh is a closed solid, so the drawings show the body's
            // edges, not the triangulation's; the triangles as they are where it is not.
            var tris = new List<(int A, int B, int C)>(t.Length / 3);
            for (var k = 0; k + 2 < t.Length; k += 3)
                if (t[k] >= 0 && t[k + 1] >= 0 && t[k + 2] >= 0 && t[k] < vertices.Length && t[k + 1] < vertices.Length && t[k + 2] < vertices.Length) tris.Add((t[k], t[k + 1], t[k + 2]));
            Solid? merged = null;
            if (tris.Count <= MergeUpTo)
                try { merged = SolidFromMesh.Build(vertices, tris); } catch (InvalidOperationException) { merged = null; }
            if (merged != null && merged.Faces.Length > 0)
            {
                list.Add(new GeometryPart((string?)p["name"] ?? "Body", (string?)p["color"] ?? "#c9ced6", merged, null, $"part:{list.Count}"));
                continue;
            }
            var faces = new List<Face>();
            for (var k = 0; k + 2 < t.Length; k += 3)
            {
                int a = t[k], b = t[k + 1], c = t[k + 2];
                if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length) continue;
                var n = Vec3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (n.Length < 1e-14) continue;
                faces.Add(new Face([a, b, c], [], n.Normalized()));
            }
            if (faces.Count == 0) continue;
            list.Add(new GeometryPart((string?)p["name"] ?? "Body", (string?)p["color"] ?? "#c9ced6", new Solid(vertices, [.. faces]), null, $"part:{list.Count}"));
        }
        return list;
    }
}
