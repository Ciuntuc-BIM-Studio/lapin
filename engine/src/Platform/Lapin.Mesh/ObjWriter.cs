using System.IO.Compression;
using System.Text;
using Lapin.Platform;
using static Lapin.Mesh.MeshMath;

namespace Lapin.Mesh;

/// <summary>
/// A scene as Wavefront OBJ with its MTL (report #76: what every renderer imports, Twinmotion among them), packed in a zip with
/// the materials' own images: <c>model.obj</c>, <c>model.mtl</c>, <c>textures/…</c>. Y up as most readers take OBJ (the model's
/// Z up turns: x, z, −y), metres. An object per element (its name and GlobalId), a <c>usemtl</c> per material, one material
/// per Lapin material named after it (spaces as underscores, as Blender writes them, since many readers stop at a space), with
/// its colour (Kd), shine (Ks, Ns), opacity (d, Tr), index of refraction (Ni), a light's emission (Ke), its image (map_Kd), its
/// relief (norm for a normal map, map_Bump for a bump map, or for the normal map when there is none) and
/// the PBR extension's roughness and metallic (Pr, Pm). The real coordinates' offset is not written: OBJ has no node to hold it.
/// </summary>
public static class ObjWriter
{
    public const string ObjName = "model.obj", MtlName = "model.mtl";

    /// <summary>The zip: the OBJ, its MTL and the images.</summary>
    public static byte[] Zip(MeshScene scene)
    {
        var (obj, mtl, textures) = Write(scene);
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Add(string name, byte[] bytes)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var s = entry.Open();
                s.Write(bytes);
            }
            Add(ObjName, Encoding.UTF8.GetBytes(obj));
            Add(MtlName, Encoding.UTF8.GetBytes(mtl));
            foreach (var (path, bytes) in textures) Add(path, bytes);
        }
        return ms.ToArray();
    }

    /// <summary>A material's name as OBJ and MTL write it: the project's, its spaces as underscores.</summary>
    public static string Name(string name)
    {
        var s = string.Join("_", name.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return s.Length == 0 ? "material" : s;
    }

    /// <summary>The OBJ's text, the MTL's and the images (path in the zip, bytes).</summary>
    public static (string Obj, string Mtl, List<(string Path, byte[] Bytes)> Textures) Write(MeshScene scene)
    {
        var files = UsdWriter.MapFiles(scene);
        var fileOf = files.ToDictionary(t => (t.Material, t.Kind), t => t.Path);
        var names = scene.Materials.Select(m => Name(m.Name)).ToList();

        var mtl = new StringBuilder();
        mtl.Append("# Lapin: one material per project material, after its name\n");
        for (var i = 0; i < scene.Materials.Count; i++)
        {
            var m = scene.Materials[i];
            var l = m.Pbr;
            mtl.Append($"\nnewmtl {names[i]}\n");
            mtl.Append("Ka 0 0 0\n");
            mtl.Append($"Kd {F(m.R)} {F(m.G)} {F(m.B)}\n");
            // The highlight: a metal's in its own colour, a dielectric's white and weak; sharper as the face is smoother.
            var spec = 0.04 + 0.96 * l.Metalness;
            var (sr, sg, sb) = l.Metalness > 0.5 ? (m.R * spec, m.G * spec, m.B * spec) : (spec, spec, spec);
            mtl.Append($"Ks {F(Math.Round(sr, 4))} {F(Math.Round(sg, 4))} {F(Math.Round(sb, 4))}\n");
            mtl.Append($"Ns {F(Math.Round(Math.Pow(1 - l.Roughness, 2) * 1000, 2))}\n");
            if (l.Emissive != null) { var (er, eg, eb) = MeshScene.Rgb(l.Emissive); mtl.Append($"Ke {F(er)} {F(eg)} {F(eb)}\n"); }
            mtl.Append($"Ni {F(Math.Round(l.Ior, 4))}\n");
            mtl.Append($"d {F(m.Alpha)}\n");
            mtl.Append($"Tr {F(Math.Round(1 - m.Alpha, 4))}\n");
            mtl.Append($"Pr {F(Math.Round(l.Roughness, 4))}\n");
            mtl.Append($"Pm {F(Math.Round(l.Metalness, 4))}\n");
            mtl.Append($"illum {(m.Alpha < 1 || l.Transmission > 0 ? 4 : 2)}\n");
            if (fileOf.TryGetValue((i, "color"), out var path)) mtl.Append($"map_Kd {path}\n");
            // Relief (the MTL's PBR extension): the normal map as norm; a bump map as map_Bump at its strength (-bm). A material with
            // a normal map and no bump map writes it as map_Bump too, as Blender reads and writes a normal map there.
            var bump = fileOf.TryGetValue((i, "bump"), out var bumpPath);
            if (fileOf.TryGetValue((i, "normal"), out var normalPath))
            {
                mtl.Append($"norm {normalPath}\n");
                if (!bump) mtl.Append($"map_Bump -bm {F(Math.Round(l.NormalStrength, 4))} {normalPath}\n");
            }
            if (bump) mtl.Append($"map_Bump -bm {F(Math.Round(l.BumpStrength, 4))} {bumpPath}\n");
        }

        var obj = new StringBuilder();
        obj.Append($"# Lapin: {scene.Project}, metres, Y up\nmtllib {MtlName}\n");
        int v = 0, vt = 0;
        foreach (var g in scene.Groups)
            foreach (var e in g.Elements.SelectMany(x => x.Flatten()))
            {
                if (e.Parts.Count == 0) continue;
                obj.Append($"\no {Name(e.Name)}_{e.GlobalId}\n");
                foreach (var p in e.Parts)
                {
                    var n = p.Positions.Length / 3;
                    for (var i = 0; i < n; i++) obj.Append($"v {F(p.Positions[i * 3])} {F(p.Positions[i * 3 + 2])} {F(-p.Positions[i * 3 + 1])}\n");
                    for (var i = 0; i < n; i++) obj.Append($"vn {F(p.Normals[i * 3])} {F(p.Normals[i * 3 + 2])} {F(-p.Normals[i * 3 + 1])}\n");
                    var uv = p.Uvs is { } u && u.Length == n * 2;
                    if (uv) for (var i = 0; i < n; i++) obj.Append($"vt {F(p.Uvs![i * 2])} {F(p.Uvs[i * 2 + 1])}\n");
                    obj.Append($"usemtl {names[p.Material]}\n");
                    for (var t = 0; t + 2 < p.Indices.Length; t += 3)
                    {
                        obj.Append('f');
                        for (var k = 0; k < 3; k++)
                        {
                            var a = p.Indices[t + k] + 1;
                            obj.Append(uv ? $" {v + a}/{vt + a}/{v + a}" : $" {v + a}//{v + a}");
                        }
                        obj.Append('\n');
                    }
                    v += n;
                    if (uv) vt += n;
                }
            }
        return (obj.ToString(), mtl.ToString(), files.Select(f => (f.Path, f.Texture.Bytes)).ToList());
    }
}
