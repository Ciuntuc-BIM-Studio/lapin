using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Mesh;

/// <summary>What a mesh export wrote: its format, how many elements and triangles, and what it left out.</summary>
public sealed record MeshExportResult(string Format, int Elements, int Triangles, int Materials, IReadOnlyList<string> Notes)
{
    public JsonObject ToJson() => new()
    {
        ["format"] = Format, ["elements"] = Elements, ["triangles"] = Triangles, ["materials"] = Materials,
        ["notes"] = new JsonArray(Notes.Select(n => (JsonNode)n).ToArray()),
    };
}

/// <summary>A session as a 3D mesh file (docs/plan-mesh-export.md): glTF 2.0 / GLB or OpenUSD (USDA, USDZ).</summary>
public static class MeshExport
{
    public static (byte[] Bytes, MeshExportResult Result) Write(Session session, MeshExportOptions o)
    {
        var scene = MeshScene.Build(session, o);
        if (scene.ElementCount == 0) throw new CommandException("There is nothing to export: no element is shown.");
        var bytes = o.Format switch
        {
            "glb" => GltfWriter.Glb(scene),
            "gltf" => GltfWriter.Gltf(scene),
            "usdz" => UsdWriter.Usdz(scene),
            "usda" => UsdWriter.Usda(scene),
            _ => throw new CommandException($"„{o.Format}” is not a 3D format."),
        };
        return (bytes, new MeshExportResult(o.Format, scene.ElementCount, scene.TriangleCount, scene.Materials.Count, scene.Notes));
    }
}
