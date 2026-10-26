using System.Globalization;
using System.Text;
using System.Xml;

namespace Lapin.Mesh;

/// <summary>
/// A scene as COLLADA 1.4.1 (Khronos), the version SketchUp, Blender, Rhino
/// and 3ds Max read, written after the specification and valid against its
/// schema: Z up and metres, as the asset says, so nothing turns. A visual
/// scene with the project (its offset to real coordinates), the building, a
/// node per storey and a node per element, each element one geometry whose
/// triangles go by material (Lambert effects, the material's colour as it is
/// shown, its opacity), and its identity in an <c>extra</c> (GlobalId, IFC
/// class, type, the name as given: COLLADA names are NCNames). Edges, when
/// the scene has them, are <c>lines</c> in the same geometry.
/// </summary>
public static class ColladaWriter
{
    const string Ns = "http://www.collada.org/2005/11/COLLADASchema";

    static string F(float v) => (v == 0 ? 0f : v).ToString("R", CultureInfo.InvariantCulture);
    static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>A name as COLLADA 1.4.1 takes it (an NCName: no spaces, not starting with a digit).</summary>
    static string Name(string name) => UsdWriter.Identifier(name);

    public static byte[] Dae(MeshScene scene, DateTime? now = null)
    {
        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, IndentChars = " " };
        using (var w = XmlWriter.Create(ms, settings))
        {
            w.WriteStartDocument();
            w.WriteStartElement("COLLADA", Ns);
            w.WriteAttributeString("version", "1.4.1");

            var stamp = (now ?? DateTime.UtcNow).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            w.WriteStartElement("asset");
            w.WriteStartElement("contributor"); w.WriteElementString("authoring_tool", "Lapin"); w.WriteEndElement();
            w.WriteElementString("created", stamp);
            w.WriteElementString("modified", stamp);
            w.WriteStartElement("unit"); w.WriteAttributeString("name", "meter"); w.WriteAttributeString("meter", "1"); w.WriteEndElement();
            w.WriteElementString("up_axis", "Z_UP");
            w.WriteEndElement();

            var edges = scene.Groups.Any(g => g.Elements.Any(e => e.Edges.Length >= 6));
            var materials = scene.Materials.Select((m, i) => (Id: $"m{i}", Name: Name(m.Name), m.R, m.G, m.B, m.Alpha)).ToList();
            if (edges) materials.Add(("medges", "Edges", 0, 0, 0, 1));

            w.WriteStartElement("library_effects");
            foreach (var m in materials)
            {
                w.WriteStartElement("effect"); w.WriteAttributeString("id", m.Id + "-fx");
                w.WriteStartElement("profile_COMMON");
                w.WriteStartElement("technique"); w.WriteAttributeString("sid", "common");
                w.WriteStartElement("lambert");
                w.WriteStartElement("diffuse");
                w.WriteStartElement("color"); w.WriteAttributeString("sid", "diffuse"); w.WriteString($"{F(m.R)} {F(m.G)} {F(m.B)} 1"); w.WriteEndElement();
                w.WriteEndElement();
                if (m.Alpha < 1)
                {
                    // A_ONE: the opacity is the transparent colour's alpha times the transparency.
                    w.WriteStartElement("transparent"); w.WriteAttributeString("opaque", "A_ONE");
                    w.WriteElementString("color", $"1 1 1 {F(m.Alpha)}");
                    w.WriteEndElement();
                    w.WriteStartElement("transparency"); w.WriteElementString("float", "1"); w.WriteEndElement();
                }
                w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement();
            }
            w.WriteEndElement();

            w.WriteStartElement("library_materials");
            foreach (var m in materials)
            {
                w.WriteStartElement("material"); w.WriteAttributeString("id", m.Id); w.WriteAttributeString("name", m.Name);
                w.WriteStartElement("instance_effect"); w.WriteAttributeString("url", "#" + m.Id + "-fx"); w.WriteEndElement();
                w.WriteEndElement();
            }
            w.WriteEndElement();

            // One geometry per element: its parts' points one after the other (and its edges' after them), triangles by material.
            var geometryOf = new Dictionary<MeshElement, string>();
            w.WriteStartElement("library_geometries");
            var n = 0;
            foreach (var g in scene.Groups)
                foreach (var e in g.Elements)
                {
                    var id = $"g{n++}";
                    geometryOf[e] = id;
                    Geometry(w, id, e);
                }
            w.WriteEndElement();

            w.WriteStartElement("library_visual_scenes");
            w.WriteStartElement("visual_scene"); w.WriteAttributeString("id", "scene"); w.WriteAttributeString("name", Name(scene.Project));
            w.WriteStartElement("node"); w.WriteAttributeString("id", "project"); w.WriteAttributeString("name", Name(scene.Project));
            var (rx, ry, rz) = scene.RootOffset;
            if (rx != 0 || ry != 0 || rz != 0) { w.WriteStartElement("translate"); w.WriteAttributeString("sid", "location"); w.WriteString($"{F(rx)} {F(ry)} {F(rz)}"); w.WriteEndElement(); }
            w.WriteStartElement("node"); w.WriteAttributeString("id", "building"); w.WriteAttributeString("name", Name(scene.Building));
            var s = 0;
            foreach (var g in scene.Groups)
            {
                w.WriteStartElement("node"); w.WriteAttributeString("id", $"s{s++}"); w.WriteAttributeString("name", Name(g.Name));
                foreach (var e in g.Elements)
                {
                    var gid = geometryOf[e];
                    w.WriteStartElement("node"); w.WriteAttributeString("id", gid + "-node"); w.WriteAttributeString("name", Name(e.Name));
                    w.WriteStartElement("instance_geometry"); w.WriteAttributeString("url", "#" + gid);
                    w.WriteStartElement("bind_material"); w.WriteStartElement("technique_common");
                    foreach (var mi in e.Parts.Select(p => p.Material).Distinct().Select(i => materials[i].Id).Concat(e.Edges.Length >= 6 ? ["medges"] : []))
                    {
                        w.WriteStartElement("instance_material"); w.WriteAttributeString("symbol", mi); w.WriteAttributeString("target", "#" + mi); w.WriteEndElement();
                    }
                    w.WriteEndElement(); w.WriteEndElement();
                    w.WriteEndElement();
                    Identity(w, e.GlobalId, e.IfcClass, e.Type, e.Name, g.Name);
                    w.WriteEndElement();
                }
                Identity(w, g.GlobalId, g.IfcClass, null, g.Name, null);
                w.WriteEndElement();
            }
            w.WriteEndElement(); // building
            w.WriteEndElement(); // project
            w.WriteEndElement(); // visual_scene
            w.WriteEndElement(); // library_visual_scenes

            w.WriteStartElement("scene");
            w.WriteStartElement("instance_visual_scene"); w.WriteAttributeString("url", "#scene"); w.WriteEndElement();
            w.WriteEndElement();

            w.WriteEndElement();
            w.WriteEndDocument();
        }
        return ms.ToArray();
    }

    static void Identity(XmlWriter w, string globalId, string ifcClass, string? type, string name, string? storey)
    {
        w.WriteStartElement("extra");
        w.WriteStartElement("technique"); w.WriteAttributeString("profile", "Lapin");
        w.WriteElementString("globalId", globalId);
        w.WriteElementString("ifcClass", ifcClass);
        if (type != null) w.WriteElementString("type", type);
        w.WriteElementString("name", name);
        if (storey != null) w.WriteElementString("storey", storey);
        w.WriteEndElement();
        w.WriteEndElement();
    }

    static void Geometry(XmlWriter w, string id, MeshElement e)
    {
        var positions = new List<float>();
        var normals = new List<float>();
        var groups = new List<(int Material, List<int> Indices)>();
        foreach (var p in e.Parts)
        {
            var baseIndex = positions.Count / 3;
            positions.AddRange(p.Positions);
            normals.AddRange(p.Normals);
            var group = groups.FirstOrDefault(x => x.Material == p.Material);
            if (group.Indices == null) { group = (p.Material, []); groups.Add(group); }
            group.Indices.AddRange(p.Indices.Select(i => i + baseIndex));
        }
        // The edges' points after the faces', with a normal each so the two arrays stay index for index.
        var edgeStart = positions.Count / 3;
        if (e.Edges.Length >= 6)
        {
            positions.AddRange(e.Edges);
            for (var i = 0; i < e.Edges.Length / 3; i++) normals.AddRange([0, 0, 1]);
        }

        w.WriteStartElement("geometry"); w.WriteAttributeString("id", id); w.WriteAttributeString("name", Name(e.Name));
        w.WriteStartElement("mesh");
        Source(w, id + "-positions", positions, ["X", "Y", "Z"]);
        Source(w, id + "-normals", normals, ["X", "Y", "Z"]);
        w.WriteStartElement("vertices"); w.WriteAttributeString("id", id + "-vertices");
        w.WriteStartElement("input"); w.WriteAttributeString("semantic", "POSITION"); w.WriteAttributeString("source", "#" + id + "-positions"); w.WriteEndElement();
        w.WriteEndElement();
        foreach (var (material, indices) in groups)
        {
            w.WriteStartElement("triangles"); w.WriteAttributeString("material", $"m{material}"); w.WriteAttributeString("count", (indices.Count / 3).ToString(CultureInfo.InvariantCulture));
            Input(w, "VERTEX", id + "-vertices");
            Input(w, "NORMAL", id + "-normals");
            w.WriteElementString("p", string.Join(' ', indices));
            w.WriteEndElement();
        }
        if (e.Edges.Length >= 6)
        {
            var segments = e.Edges.Length / 6;
            w.WriteStartElement("lines"); w.WriteAttributeString("material", "medges"); w.WriteAttributeString("count", segments.ToString(CultureInfo.InvariantCulture));
            Input(w, "VERTEX", id + "-vertices");
            w.WriteElementString("p", string.Join(' ', Enumerable.Range(edgeStart, segments * 2)));
            w.WriteEndElement();
        }
        w.WriteEndElement();
        w.WriteEndElement();
    }

    static void Input(XmlWriter w, string semantic, string source)
    {
        w.WriteStartElement("input"); w.WriteAttributeString("semantic", semantic); w.WriteAttributeString("source", "#" + source); w.WriteAttributeString("offset", "0");
        w.WriteEndElement();
    }

    static void Source(XmlWriter w, string id, List<float> values, string[] names)
    {
        w.WriteStartElement("source"); w.WriteAttributeString("id", id);
        w.WriteStartElement("float_array"); w.WriteAttributeString("id", id + "-array"); w.WriteAttributeString("count", values.Count.ToString(CultureInfo.InvariantCulture));
        w.WriteString(string.Join(' ', values.Select(F)));
        w.WriteEndElement();
        w.WriteStartElement("technique_common");
        w.WriteStartElement("accessor"); w.WriteAttributeString("source", "#" + id + "-array"); w.WriteAttributeString("count", (values.Count / names.Length).ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("stride", names.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var n in names) { w.WriteStartElement("param"); w.WriteAttributeString("name", n); w.WriteAttributeString("type", "float"); w.WriteEndElement(); }
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
    }
}
