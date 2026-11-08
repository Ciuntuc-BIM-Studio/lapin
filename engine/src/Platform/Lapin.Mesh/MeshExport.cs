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

/// <summary>A session as a 3D mesh file (docs/plan-mesh-export.md): glTF 2.0 / GLB, OpenUSD (USDA, USDZ), COLLADA 1.4.1, OBJ with its MTL (zipped), That Open Fragments (docs/fragments.md) or BIM Open Schema (docs/bos.md).</summary>
public static class MeshExport
{
    public static (byte[] Bytes, MeshExportResult Result) Write(Session session, MeshExportOptions o)
    {
        var scene = MeshScene.Build(session, o);
        // The point clouds, only when asked (decision 396): a sample of each shown, its classes hidden in the 3D view left out.
        if (o.PointClouds) scene = WithClouds(session, scene, o);
        if (scene.ElementCount == 0 && scene.Clouds.Count == 0) throw new CommandException("There is nothing to export: no element is shown.");
        var bytes = o.Format switch
        {
            "glb" => GltfWriter.Glb(scene),
            "gltf" => GltfWriter.Gltf(scene),
            "usdz" => UsdWriter.Usdz(scene),
            "usda" => UsdWriter.Usda(scene),
            "dae" => ColladaWriter.Dae(scene),
            "obj" => ObjWriter.Zip(scene),
            "frag" => Fragments.FragWriter.Write(scene, context: Fragments.FragContext.Of(session, scene, o)),
            "bos" => Bos.BosWriter.Write(scene, Fragments.FragContext.Of(session, scene, o),
                Lapin.Platform.Coordination.OpeningVoids.Of(session.Model, session.Catalog, Lapin.Platform.Coordination.VoidOptions.All)),
            _ => throw new CommandException($"„{o.Format}” is not a 3D format."),
        };
        // One file with no room for an image: the materials' own textures go out in GLB, glTF and USDZ.
        if (o.Format is "usda" or "dae" or "frag" or "bos" && scene.Materials.Any(m => m.Texture != null))
            scene.Notes.Add($"{o.Format.ToUpperInvariant()} is one file without the materials' own images: their textures go out in GLB, glTF and USDZ.");
        return (bytes, new MeshExportResult(o.Format, scene.ElementCount, scene.TriangleCount, scene.Materials.Count, scene.Notes));
    }

    /// <summary>The most points an export carries, all clouds together.</summary>
    public const int MaxCloudPoints = 5_000_000;

    static MeshScene WithClouds(Session session, MeshScene scene, MeshExportOptions o)
    {
        var m = session.Model;
        var view = o.ViewId == null ? null : o.ViewId == GraphicsSets.View3d ? GraphicsSets.As3dView(m) : m.Get(o.ViewId);
        var graphics = view == null ? null : GraphicsSets.Of(m, view);
        var clouds = PointCloudLinks.All(m).Select(l => (Link: l, Look: PointCloudLinks.LookIn(graphics, l))).Where(x => x.Look.Shown && PointCloudLinks.StoreOf(x.Link) != null).ToList();
        if (clouds.Count == 0) return scene;
        var list = new List<MeshCloud>();
        foreach (var (link, look) in clouds)
        {
            var (pos, col) = PointCloudLinks.Sample(m, link, look, MaxCloudPoints / clouds.Count);
            for (var i = 0; i + 2 < pos.Length; i += 3)
            {
                pos[i] -= (float)scene.Origin.X; pos[i + 1] -= (float)scene.Origin.Y; pos[i + 2] -= (float)scene.Origin.Z;
            }
            list.Add(new MeshCloud(link.Name, pos, col));
        }
        if (o.Format is not ("glb" or "gltf")) scene.Notes.Add("Point clouds go out in GLB and glTF only: this format leaves them out.");
        else scene.Notes.Add($"Point clouds: {list.Sum(c => c.Positions.Length / 3):N0} points of {list.Count} cloud(s), a sample of each of at most {MaxCloudPoints / clouds.Count:N0}.");
        return scene with { Clouds = list };
    }
}
