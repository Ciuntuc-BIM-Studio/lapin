using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// An element whose body came with it (a reference from an IFC file): the
/// triangles are kept in its <c>body</c> attribute, in metres, world
/// coordinates, and drawn as they are. Such an element is shown, measured and
/// snapped to, never edited; its class stays what the file said.
/// </summary>
public static class StoredBodies
{
    public const string Key = "body";

    public static bool Has(Entity e) => e.Attrs[Key] is JsonObject;

    /// <summary>The parts as the attribute holds them: coordinates to 0,1 mm, triangles by vertex index.</summary>
    public static JsonObject Write(IEnumerable<(string Name, string Color, IReadOnlyList<Vec3> Vertices, IReadOnlyList<(int A, int B, int C)> Triangles)> parts)
    {
        var list = new JsonArray();
        foreach (var p in parts)
        {
            var v = new JsonArray();
            foreach (var q in p.Vertices) { v.Add((JsonNode)Math.Round(q.X, 4)); v.Add((JsonNode)Math.Round(q.Y, 4)); v.Add((JsonNode)Math.Round(q.Z, 4)); }
            var t = new JsonArray();
            foreach (var (a, b, c) in p.Triangles) { t.Add((JsonNode)a); t.Add((JsonNode)b); t.Add((JsonNode)c); }
            list.Add((JsonNode)new JsonObject { ["name"] = p.Name, ["color"] = p.Color, ["v"] = v, ["t"] = t });
        }
        return new JsonObject { ["parts"] = list };
    }

    /// <summary>The stored body as geometry: a solid of triangles per part.</summary>
    public static ElementGeometry? Geometry(Entity e, IModelView model, IServices services)
    {
        if (e.Attrs[Key] is not JsonObject body || body["parts"] is not JsonArray parts) return null;
        var list = new List<GeometryPart>();
        foreach (var p in parts.OfType<JsonObject>())
        {
            var v = (p["v"] as JsonArray)?.Select(x => Json.Number(x) ?? 0).ToArray() ?? [];
            var t = (p["t"] as JsonArray)?.Select(x => (int)(Json.Number(x) ?? -1)).ToArray() ?? [];
            var vertices = new Vec3[v.Length / 3];
            for (var k = 0; k < vertices.Length; k++) vertices[k] = new Vec3(v[3 * k], v[3 * k + 1], v[3 * k + 2]);
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
        return new ElementGeometry(list);
    }
}
