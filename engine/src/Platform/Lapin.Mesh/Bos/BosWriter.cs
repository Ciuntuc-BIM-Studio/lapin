using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Mesh.Fragments;
using Lapin.Platform;

namespace Lapin.Mesh.Bos;

/// <summary>
/// The mesh scene as a BIM Open Schema file (docs/bos.md), the way Ara 3D's IFC converter writes one: the project, site,
/// building and storeys, each element under its IFC class's category (an entity named after it, IFCWALL…) with its
/// GlobalId, its type, its materials and parameters (its IFC attributes as <c>Ifc:</c> parameters grouped by its class,
/// its property and quantity sets by their names); relations (ContainedIn its storey, PartOf the spatial structure,
/// MemberOf its assembly, HasMaterial); and its triangles, welded, as meshes of tenths of a millimetre placed by a
/// translation, identical meshes stored once.
/// </summary>
public static class BosWriter
{
    public static byte[] Write(MeshScene scene, FragContext? context = null)
    {
        var d = new BosData();
        d.Documents.Add((d.Intern(scene.Project), -1));
        var categories = new Dictionary<string, int>();
        int Category(string ifcClass)
        {
            var name = ifcClass.ToUpperInvariant();
            if (!categories.TryGetValue(name, out var c)) categories[name] = c = d.Entity(name);
            return c;
        }
        var numbers = new Dictionary<float, int>();
        void Param(int entity, string name, string? group, JsonNode? value, string? units = null)
        {
            if (value is not JsonValue v) return;
            if (v.TryGetValue<bool>(out var b)) { d.Parameters.Add((entity, d.Descriptor(name, group, BosParameterType.Int, units), b ? 1 : 0)); return; }
            if (v.GetValueKind() == JsonValueKind.Number && v.TryGetValue<double>(out var x))
            {
                if (units == null && x == Math.Round(x) && Math.Abs(x) < int.MaxValue && v.TryGetValue<int>(out var i)) { d.Parameters.Add((entity, d.Descriptor(name, group, BosParameterType.Int, units), i)); return; }
                var f = (float)x;
                if (!numbers.TryGetValue(f, out var n)) { numbers[f] = n = d.Numbers.Count; d.Numbers.Add(f); }
                d.Parameters.Add((entity, d.Descriptor(name, group, BosParameterType.Number, units), n));
                return;
            }
            if (v.TryGetValue<string>(out var s)) d.Parameters.Add((entity, d.Descriptor(name, group, BosParameterType.String, units), d.Intern(s)));
        }

        // The spatial structure.
        var project = d.Entity(scene.Project, Category("IfcProject"), context?.ProjectGuid);
        var site = d.Entity("Site", Category("IfcSite"), context?.SiteGuid);
        var building = d.Entity(scene.Building, Category("IfcBuilding"), context?.BuildingGuid);
        d.Relations.Add((site, project, BosRelation.PartOf));
        d.Relations.Add((building, site, BosRelation.PartOf));

        // Materials, as the exports name them; and the geometry's colours.
        foreach (var m in scene.Materials)
        {
            static byte B(double x) => (byte)Math.Clamp(Math.Round(x * 255), 0, 255);
            d.Materials.Add((B(m.R), B(m.G), B(m.B), B(m.Alpha), B(m.Pbr.Roughness), B(m.Pbr.Metalness)));
        }
        var materialEntities = new Dictionary<string, int>();
        var typeEntities = new Dictionary<string, int>();
        var meshIndex = new Dictionary<string, List<int>>();
        var transformIndex = new Dictionary<(float, float, float), int>();

        foreach (var g in scene.Groups)
        {
            int container;
            if (g.IfcClass == "IfcSite") container = site;
            else
            {
                container = d.Entity(g.Name, Category(g.IfcClass), g.GlobalId is { Length: 22 } gid ? gid : null);
                d.Relations.Add((container, building, BosRelation.PartOf));
                Param(container, "Ifc:Elevation", "IFCBUILDINGSTOREY", Math.Round(g.Elevation, 6), "m");
            }
            foreach (var root in g.Elements) Element(root, container, -1);
        }
        if (scene.Clouds.Count > 0) scene.Notes.Add("BIM Open Schema has no point clouds: they were left out.");
        return d.Write(Session.AppVersion);

        void Element(MeshElement el, int container, int assembly)
        {
            var e = d.Entity(el.Name, Category(el.IfcClass), el.GlobalId is { Length: 22 } gid ? gid : null);
            if (assembly >= 0) d.Relations.Add((e, assembly, BosRelation.MemberOf));
            else d.Relations.Add((e, container, BosRelation.ContainedIn));
            var group = el.IfcClass.ToUpperInvariant();
            if (context?.Element(el.GlobalId) is { } data)
            {
                foreach (var a in data.Attributes) Param(e, $"Ifc:{a.Name}", group, a.Value);
                if (data.Type is { } t)
                {
                    if (!typeEntities.TryGetValue(t.Id, out var te)) typeEntities[t.Id] = te = d.Entity(t.Name, Category(el.IfcClass + "Type"), t.Id is { Length: 22 } ? t.Id : null);
                    var row = d.Entities[e];
                    d.Entities[e] = row with { Type = te };
                }
                foreach (var name in data.Materials)
                {
                    if (!materialEntities.TryGetValue(name, out var me)) materialEntities[name] = me = d.Entity(name, Category("IfcMaterial"));
                    d.Relations.Add((e, me, BosRelation.HasMaterial));
                }
                foreach (var set in data.Sets)
                    foreach (var item in set.Items)
                        // A file's IFC attributes, kept by its import as Attributes, go back as Ifc: parameters of the class.
                        Param(e, set.Name == "Attributes" ? $"Ifc:{item.Name}" : item.Name, set.Name == "Attributes" ? group : set.Name, item.Value.Value, item.Category switch
                        {
                            "IFCQUANTITYLENGTH" => "m", "IFCQUANTITYAREA" => "m²", "IFCQUANTITYVOLUME" => "m³", "IFCQUANTITYWEIGHT" => "kg", _ => null,
                        });
            }
            byte flags = el.IfcClass == "IfcSpace" ? (byte)1 : (byte)0;
            foreach (var part in el.Parts) Geometry(e, part, flags);
            foreach (var child in el.Children ?? []) Element(child, container, e);
        }

        /// <summary>Whether mesh <paramref name="m"/> holds exactly these vertices and indices.</summary>
        bool Same(int m, List<int> vx, List<int> vy, List<int> vz, List<int> idx)
        {
            var (v0, v1, i0, i1) = d.MeshRange(m);
            if (v1 - v0 != vx.Count || i1 - i0 != idx.Count) return false;
            for (var i = 0; i < vx.Count; i++) if (d.VertexX[v0 + i] != vx[i] || d.VertexY[v0 + i] != vy[i] || d.VertexZ[v0 + i] != vz[i]) return false;
            for (var i = 0; i < idx.Count; i++) if (d.Indices[i0 + i] != idx[i]) return false;
            return true;
        }

        /// <summary>A part's triangles as a mesh: welded at a tenth of a millimetre, around its middle (kept as the instance's translation), shared when another part has the same.</summary>
        void Geometry(int entity, MeshPart part, byte flags)
        {
            var p = part.Positions;
            var n = p.Length / 3;
            if (n == 0 || part.Indices.Length < 3) return;
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            for (var i = 0; i < n; i++)
            {
                minX = Math.Min(minX, p[i * 3]); maxX = Math.Max(maxX, p[i * 3]);
                minY = Math.Min(minY, p[i * 3 + 1]); maxY = Math.Max(maxY, p[i * 3 + 1]);
                minZ = Math.Min(minZ, p[i * 3 + 2]); maxZ = Math.Max(maxZ, p[i * 3 + 2]);
            }
            // The middle, to the millimetre, so equal parts moved by whole millimetres weld to the same mesh.
            var (cx, cy, cz) = (Math.Round((minX + maxX) / 2, 3), Math.Round((minY + maxY) / 2, 3), Math.Round((minZ + maxZ) / 2, 3));
            var weld = new Dictionary<(int, int, int), int>();
            var vx = new List<int>(); var vy = new List<int>(); var vz = new List<int>();
            var map = new int[n];
            for (var i = 0; i < n; i++)
            {
                var key = ((int)Math.Round((p[i * 3] - cx) * BosData.VertexScale), (int)Math.Round((p[i * 3 + 1] - cy) * BosData.VertexScale), (int)Math.Round((p[i * 3 + 2] - cz) * BosData.VertexScale));
                if (!weld.TryGetValue(key, out var k)) { weld[key] = k = vx.Count; vx.Add(key.Item1); vy.Add(key.Item2); vz.Add(key.Item3); }
                map[i] = k;
            }
            var idx = new List<int>(part.Indices.Length);
            for (var i = 0; i + 2 < part.Indices.Length; i += 3)
            {
                int a = map[part.Indices[i]], b = map[part.Indices[i + 1]], c = map[part.Indices[i + 2]];
                if (a == b || b == c || a == c) continue;
                idx.Add(a); idx.Add(b); idx.Add(c);
            }
            if (idx.Count == 0) return;
            var signature = Signature(vx, vy, vz, idx);
            var candidates = meshIndex.TryGetValue(signature, out var l) ? l : meshIndex[signature] = [];
            var mesh = candidates.FirstOrDefault(m => Same(m, vx, vy, vz, idx), -1);
            if (mesh < 0)
            {
                candidates.Add(mesh = d.Meshes.Count);
                d.Meshes.Add((d.Indices.Count, d.VertexX.Count));
                d.VertexX.AddRange(vx); d.VertexY.AddRange(vy); d.VertexZ.AddRange(vz);
                d.Indices.AddRange(idx);
            }
            var tkey = ((float)cx, (float)cy, (float)cz);
            if (!transformIndex.TryGetValue(tkey, out var transform))
            {
                transformIndex[tkey] = transform = d.Transforms.Count;
                d.Transforms.Add((tkey.Item1, tkey.Item2, tkey.Item3, 0, 0, 0, 1, 1, 1, 1));
            }
            d.Instances.Add((entity, part.Material >= 0 && part.Material < d.Materials.Count ? part.Material : -1, mesh, transform, flags));
        }
    }

    static string Signature(List<int> x, List<int> y, List<int> z, List<int> idx)
    {
        var h = new HashCode();
        for (var i = 0; i < x.Count; i++) { h.Add(x[i]); h.Add(y[i]); h.Add(z[i]); }
        foreach (var i in idx) h.Add(i);
        return $"{x.Count}:{idx.Count}:{h.ToHashCode()}:{x[0]},{y[0]},{z[0]}:{x[^1]},{y[^1]},{z[^1]}";
    }
}
