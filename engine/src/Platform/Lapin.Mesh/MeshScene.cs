using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mesh;

/// <summary>
/// What a mesh export takes: its format (glb, gltf, usdz, usda, dae), what it shows
/// (a 3D view's visible elements, else the whole model), at the end of which
/// phase, whether the elements' edges go too, and where the origin is: the
/// project's base point (the default), its internal origin, or the survey
/// point's real coordinates (on the root, never in the vertices).
/// </summary>
public sealed record MeshExportOptions(string Format = "glb", string? ViewId = null, string? Phase = null, bool Edges = false, string Origin = "base")
{
    public static readonly string[] Formats = ["glb", "gltf", "usdz", "usda", "dae"];
    public static readonly string[] Origins = ["base", "internal", "survey"];

    public static MeshExportOptions From(JsonObject p)
    {
        var format = ((string?)p["format"] ?? "glb").ToLowerInvariant();
        if (!Formats.Contains(format)) throw new CommandException($"„{format}” is not a 3D format: {string.Join(", ", Formats)}.");
        var origin = (string?)p["origin"] ?? "base";
        if (!Origins.Contains(origin)) throw new CommandException($"„{origin}” is not base, internal or survey.");
        return new(format, (string?)p["viewId"] is { Length: > 0 } v ? v : null, (string?)p["phase"] is { Length: > 0 } ph ? ph : null,
            p["edges"] is JsonValue ev && ev.TryGetValue<bool>(out var e) && e, origin);
    }

    public string Extension => Format;
}

/// <summary>A material's own image: its file as it came (PNG or JPEG), its name, and how large it lies on the model (metres).</summary>
public sealed record MeshTexture(string Name, string Mime, byte[] Bytes, double Width, double Height);

/// <summary>A material as the exports write it: its name, its colour (0–1) and opacity, the IFC category it has, its own image.</summary>
public sealed record MeshMaterial(string Name, float R, float G, float B, float Alpha, string? Category, MeshTexture? Texture = null);

/// <summary>
/// One material's triangles of an element: flat shaded (vertices per face), positions and normals Z up, metres, from the export's origin;
/// texture coordinates (u, v per vertex) when its material has an image.
/// </summary>
public sealed record MeshPart(int Material, float[] Positions, float[] Normals, int[] Indices, float[]? Uvs = null);

/// <summary>An element: its GlobalId, name, IFC class and type, its parts by material, and its edges as segments (when asked for).</summary>
public sealed record MeshElement(string GlobalId, string Name, string IfcClass, string? Type, List<MeshPart> Parts, float[] Edges);

/// <summary>A storey (or the site, for what stands on none) with its elements.</summary>
public sealed record MeshGroup(string GlobalId, string Name, string IfcClass, double Elevation, List<MeshElement> Elements);

/// <summary>
/// The scene every mesh format writes (docs/plan-mesh-export.md): the
/// project and building, a group per storey (and the site's), an element per
/// visible element with its parts merged by material, the materials, and the
/// root's offset to real coordinates (the survey point's, when asked).
/// Coordinates are metres, Z up, as the model has them; a writer turns them
/// as its format asks (glTF: Y up).
/// </summary>
public sealed record MeshScene(string Project, string Building, List<MeshGroup> Groups, List<MeshMaterial> Materials, (double X, double Y, double Z) RootOffset, List<string> Notes)
{
    public int ElementCount => Groups.Sum(g => g.Elements.Count);
    public int TriangleCount => Groups.Sum(g => g.Elements.Sum(e => e.Parts.Sum(p => p.Indices.Length / 3)));

    /// <summary>The session's visible elements as a scene.</summary>
    public static MeshScene Build(Session session, MeshExportOptions o)
    {
        var m = session.Model;
        var notes = new List<string>();
        var project = Spatial.Project(m);
        var view = o.ViewId == null ? null
            : o.ViewId == GraphicsSets.View3d ? GraphicsSets.As3dView(m)
            : m.Get(o.ViewId) is { Class: Views.Class } v && v.Str("kind") == "3d" ? v : throw new CommandException("A 3D export takes a 3D view, or the whole model.");
        var graphics = view == null ? null : GraphicsSets.Of(m, view);
        var hidden = (view?.Attrs["hiddenElements"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        var filters = session.Catalog.All<IViewElementFilter>();

        // Where the export's origin is in the model, and what the root adds back to reach real coordinates.
        var basePoint = Location.Base(m);
        var (ox, oy, oz) = o.Origin == "internal" ? (0.0, 0.0, 0.0) : (basePoint.At.X, basePoint.At.Y, basePoint.Elevation);
        var root = (0.0, 0.0, 0.0);
        if (o.Origin == "survey")
        {
            if (Location.Survey(m) is { Easting: { } e, Northing: { } n } survey)
            {
                (ox, oy, oz) = (survey.At.X, survey.At.Y, 0);
                root = (e, n, survey.Elevation);
            }
            else notes.Add("The project has no survey point with its coordinates: the export is from the base point.");
        }

        var materials = new List<MeshMaterial>();
        var materialIndex = new Dictionary<string, int>();
        int MaterialOf(GeometryPart p)
        {
            var mat = p.Material != null ? m.Get(p.Material) : null;
            var key = mat?.Id ?? "colour:" + p.Color;
            if (materialIndex.TryGetValue(key, out var i)) return i;
            var (r, g, b) = Rgb(mat?.Str("color") ?? p.Color);
            var alpha = 1 - Math.Clamp(mat?.NumOrNull("transparency") ?? 0, 0, 1);
            MeshTexture? texture = null;
            if (mat != null && Images.Of(m, mat) is { } image)
            {
                var size = Images.SizeOf(mat);
                try { texture = new MeshTexture(image.Name, image.Str("mime") ?? "image/png", Images.Bytes(image), size[0], size[1]); }
                catch (FormatException) { notes.Add($"The image of „{mat.Name}” could not be read: the material goes out in its colour."); }
            }
            materials.Add(new MeshMaterial(mat?.Name ?? "Colour " + p.Color.TrimStart('#'), r, g, b, (float)alpha, mat?.Str("category"), texture));
            return materialIndex[key] = materials.Count - 1;
        }

        var groups = new Dictionary<string, MeshGroup>();
        foreach (var s in Spatial.Storeys(m))
            groups[s.Id] = new MeshGroup(s.Id, s.Name, IfcClass.BuildingStorey, s.Num("elevation"), []);
        var site = m.OfClass(IfcClass.Site).FirstOrDefault();
        var siteGroup = new MeshGroup(site?.Id ?? "site", site?.Name ?? "Site", IfcClass.Site, 0, []);

        foreach (var e in m.Entities.ToList())
        {
            if (!IsElement(session, e) || !InPhase(project, e, o.Phase) || hidden.Contains(e.Id)) continue;
            if (Links.Of(m, e) is { } link && !Links.Visible(link)) continue;
            // Rooms are volumes of air: a model without a view's graphics leaves them out, as the 3D view does.
            if (view == null && e.Class == IfcClass.Space) continue;
            if (view != null)
            {
                if (GraphicsSets.Hides3d(graphics, e)) continue;
                if (filters.Select(f => f.For(m, view, e)).FirstOrDefault(x => x != null) is { Hide: true }) continue;
            }
            ElementGeometry? geo;
            try { geo = session.Geometry(e.Id); }
            catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException) { notes.Add($"„{e.Name}” was left out: {ex.Message}"); continue; }
            var parts = geo?.Parts.Where(p => p.Solid.Faces.Length > 0).ToList() ?? [];
            if (parts.Count == 0) continue;

            var byMaterial = new Dictionary<int, List<TriMesh>>();
            var edges = new List<float>();
            foreach (var p in parts)
            {
                var mesh = Tessellator.Tessellate(p.Solid);
                if (mesh.Indices.Length == 0) continue;
                var k = MaterialOf(p);
                (byMaterial.TryGetValue(k, out var meshes) ? meshes : byMaterial[k] = []).Add(mesh);
                if (o.Edges) edges.AddRange(mesh.Edges);
            }
            if (byMaterial.Count == 0) continue;
            var meshParts = byMaterial.OrderBy(kv => kv.Key).Select(kv =>
            {
                var merged = TriMesh.Merge(kv.Value);
                // A material's image is laid on in the model's own metres, before the origin moves (as the 3D view lays it).
                var uvs = materials[kv.Key].Texture is { } t ? BoxUv(merged.Positions, merged.Normals, t.Width, t.Height) : null;
                return new MeshPart(kv.Key, Shift(merged.Positions, ox, oy, oz), merged.Normals, merged.Indices, uvs);
            }).ToList();
            var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.Name;
            var element = new MeshElement(e.Id, e.Name, e.Class, type, meshParts, Shift([.. edges], ox, oy, oz));
            var storey = Spatial.StoreyOf(m, e.Id) ?? HostStorey(m, e);
            (storey != null && groups.TryGetValue(storey.Id, out var gr) ? gr : siteGroup).Elements.Add(element);
        }

        var list = groups.Values.Where(g => g.Elements.Count > 0).ToList();
        if (siteGroup.Elements.Count > 0) list.Insert(0, siteGroup);
        return new MeshScene(project?.Name ?? "Project", Spatial.Building(m)?.Name ?? "Building", list, materials, root, notes);
    }

    /// <summary>
    /// Texture coordinates by box projection, as the 3D view lays a texture: the
    /// face's dominant axis drops out (Z up: on a wall u runs along it and v up),
    /// metres over the image's width and height. v grows upwards.
    /// </summary>
    public static float[] BoxUv(float[] positions, float[] normals, double width, double height)
    {
        var n = positions.Length / 3;
        var uv = new float[n * 2];
        for (var i = 0; i < n; i++)
        {
            double x = positions[i * 3], y = positions[i * 3 + 1], z = positions[i * 3 + 2];
            double ax = Math.Abs(normals[i * 3]), ay = Math.Abs(normals[i * 3 + 1]), az = Math.Abs(normals[i * 3 + 2]);
            var (u, v) = az >= ax && az >= ay ? (x, y) : ax >= ay ? (y, z) : (x, z);
            uv[i * 2] = (float)(u / width);
            uv[i * 2 + 1] = (float)(v / height);
        }
        return uv;
    }

    static float[] Shift(float[] xyz, double ox, double oy, double oz)
    {
        if (ox == 0 && oy == 0 && oz == 0) return xyz;
        var o = new float[xyz.Length];
        for (var i = 0; i + 2 < xyz.Length; i += 3)
        {
            o[i] = (float)(xyz[i] - ox);
            o[i + 1] = (float)(xyz[i + 1] - oy);
            o[i + 2] = (float)(xyz[i + 2] - oz);
        }
        return o;
    }

    /// <summary>What the IFC export takes as an element: a body of its own, an element's class (no opening, grid or annotation).</summary>
    static bool IsElement(Session s, Entity e) =>
        (s.Catalog.Geometry(e.Class) != null || StoredBodies.Has(e))
        && e.Class is not (IfcClass.OpeningElement or IfcClass.Grid or IfcClass.Annotation)
        && (s.Catalog.Class(e.Class)?.Kind ?? "element") == "element";

    /// <summary>A door or window stands on its wall's storey.</summary>
    static Entity? HostStorey(IModelView m, Entity e) =>
        m.RelatingOf(e.Id, IfcClass.RelFillsElement) is { } opening && m.RelatingOf(opening.Id, IfcClass.RelVoidsElement) is { } host ? Spatial.StoreyOf(m, host.Id) : null;

    /// <summary>Whether the element exists at the end of the phase asked for (as the IFC export and the phases module see it).</summary>
    static bool InPhase(Entity? project, Entity e, string? phase)
    {
        if (phase == null || project == null) return true;
        var phases = ((project.Attrs["phases"] as JsonObject)?["phases"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        int Index(string? id) => id == null ? -1 : phases.FindIndex(p => (string?)p["id"] == id);
        var v = Index(phase);
        if (v < 0) return true;
        var c = Index(e.Str("phaseCreated"));
        if (c < 0) c = Math.Max(0, phases.FindIndex(p => (string?)p["kind"] == "construction"));
        var d = Index(e.Str("phaseDemolished"));
        return c <= v && (d < 0 || d >= v);
    }

    /// <summary>#rrggbb as 0–1 (sRGB, as the formats take a base colour).</summary>
    public static (float R, float G, float B) Rgb(string? hex)
    {
        if (hex is not { Length: 7 } || hex[0] != '#'
            || !int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return (0.79f, 0.81f, 0.84f);
        return (((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
    }
}
