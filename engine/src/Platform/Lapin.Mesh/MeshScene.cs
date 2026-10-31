using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mesh;

/// <summary>
/// What a mesh export takes: its format (glb, gltf, usdz, usda, dae, frag), what it shows
/// (a 3D view's visible elements, else the whole model), at the end of which
/// phase, whether the elements' edges go too, and where the origin is: the
/// project's base point (the default), its internal origin, or the survey
/// point's real coordinates (on the root, never in the vertices).
/// "As the view shows it", the interface adds what only it knows of the view in
/// front: <see cref="Hidden"/>, the elements it does not draw (the storeys it does
/// not show, by the rule of ui/src/viewport/storeys3d.ts, and what is hidden or
/// isolated for now), and <see cref="Planes"/>, the planes it cuts the model by
/// (the cut above the storeys shown, a section shown in 3D, the section box),
/// which cut the export too, closed by <see cref="SectionCaps"/> in the parts'
/// materials unless <see cref="Caps"/> is off. <see cref="Layers"/>: which layers of
/// layered elements go (all, noFinishes, core; docs/view-styles.md); without it, a 3D
/// view's export takes the view's own layers mode, and the whole model every layer.
/// </summary>
public sealed record MeshExportOptions(string Format = "glb", string? ViewId = null, string? Phase = null, bool Edges = false, string Origin = "base",
    IReadOnlySet<string>? Hidden = null, IReadOnlyList<ClipPlane>? Planes = null, bool Caps = true, string? Layers = null)
{
    public static readonly string[] Formats = ["glb", "gltf", "usdz", "usda", "dae", "frag"];
    public static readonly string[] Origins = ["base", "internal", "survey"];

    public static MeshExportOptions From(JsonObject p)
    {
        var format = ((string?)p["format"] ?? "glb").ToLowerInvariant();
        if (!Formats.Contains(format)) throw new CommandException($"„{format}” is not a 3D format: {string.Join(", ", Formats)}.");
        var origin = (string?)p["origin"] ?? "base";
        if (!Origins.Contains(origin)) throw new CommandException($"„{origin}” is not base, internal or survey.");
        var hidden = (p["hidden"] as JsonArray)?.Select(x => x is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : null).OfType<string>().ToHashSet();
        return new(format, (string?)p["viewId"] is { Length: > 0 } v ? v : null, (string?)p["phase"] is { Length: > 0 } ph ? ph : null,
            p["edges"] is JsonValue ev && ev.TryGetValue<bool>(out var e) && e, origin,
            hidden is { Count: > 0 } ? hidden : null, PlanesOf(p["planes"]) is { Count: > 0 } planes ? planes : null,
            !(p["caps"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && !c),
            p["layers"] is { } l ? ViewStyles.LayersOf(l) : null);
    }

    /// <summary>Clipping planes as the 3D view gives them: [{normal: [x, y, z], constant}] (at most twelve; a malformed one is skipped).</summary>
    public static List<ClipPlane> PlanesOf(JsonNode? n)
    {
        var o = new List<ClipPlane>();
        if (n is not JsonArray list) return o;
        foreach (var item in list)
        {
            if (o.Count == 12) break;
            if (item is not JsonObject p || p["normal"] is not JsonArray { Count: 3 } v || Json.Number(p["constant"]) is not { } k || !double.IsFinite(k)) continue;
            if (Json.Number(v[0]) is not { } x || Json.Number(v[1]) is not { } y || Json.Number(v[2]) is not { } z) continue;
            var normal = new Vec3(x, y, z);
            if (double.IsFinite(normal.Length) && normal.Length > 1e-12) o.Add(new ClipPlane(normal, k));
        }
        return o;
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

/// <summary>
/// An element: its GlobalId, name, IFC class (the class it is exported as) and type, its parts by material, and its edges as
/// segments (when asked for). An assembly (a Studio box with its bottom and top, docs/plan-studio-thick.md) has no parts of its
/// own: its <see cref="Children"/> are the elements it aggregates, which every format writes as a group node containing them.
/// </summary>
public sealed record MeshElement(string GlobalId, string Name, string IfcClass, string? Type, List<MeshPart> Parts, float[] Edges, List<MeshElement>? Children = null)
{
    public bool IsAssembly => Children is { Count: > 0 };

    /// <summary>The element and, after it, its children, in depth.</summary>
    public IEnumerable<MeshElement> Flatten() => Children == null ? [this] : new[] { this }.Concat(Children.SelectMany(c => c.Flatten()));
}

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
    /// <summary>The elements with a body (an assembly counts its parts, not itself).</summary>
    public int ElementCount => Groups.Sum(g => g.Elements.SelectMany(e => e.Flatten()).Count(e => e.Parts.Count > 0));
    public int TriangleCount => Groups.Sum(g => g.Elements.SelectMany(e => e.Flatten()).Sum(e => e.Parts.Sum(p => p.Indices.Length / 3)));

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
        if (o.Hidden != null) hidden.UnionWith(o.Hidden);
        var planes = MeshClip.Unit(o.Planes ?? []);
        var filters = session.Catalog.All<IViewElementFilter>();
        // The layers a 3D view shows (its style's, or its own), the parts it leaves out left out here too.
        var layers = o.Layers ?? (view != null ? ViewStyles.Of(m, view).Layers : "all");
        var bands = new Dictionary<string, IReadOnlyList<LayerBand>?>();

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

        var elementOf = new Dictionary<string, (MeshGroup Group, MeshElement Element)>();
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
            var parts = geo?.Parts.Where(p => p.Solid.Faces.Length > 0 && ViewStyles.Shows3d(m, layers, e.Id, p, bands)).ToList() ?? [];
            if (parts.Count == 0) continue;
            // Cut as the view cuts it: each part's cap (in its material) closes what the planes open.
            var caps = planes.Length > 0 && o.Caps ? SectionCaps.Make(parts.Select(p => p.Solid).ToList(), planes) : null;

            var byMaterial = new Dictionary<int, List<TriMesh>>();
            var edges = new List<float>();
            for (var i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                var mesh = MeshClip.Clip(Tessellator.Tessellate(p.Solid), planes);
                if (caps?[i] is { Indices.Length: > 0 } cap) mesh = TriMesh.Merge([mesh, cap]);
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
            var element = new MeshElement(e.Id, e.Name, ExportClass(e), type, meshParts, Shift([.. edges], ox, oy, oz));
            var storey = Spatial.StoreyOf(m, e.Id) ?? HostStorey(m, e);
            var group = storey != null && groups.TryGetValue(storey.Id, out var gr) ? gr : siteGroup;
            group.Elements.Add(element);
            elementOf[e.Id] = (group, element);
        }
        Assemblies(m, elementOf);

        if (layers != "all") notes.Add(layers == "core" ? "The layered elements with their cores only, as the 3D view shows them." : "The layered elements without their finishes, as the 3D view shows them.");
        if (planes.Length > 0) notes.Add($"Cut as the 3D view cuts it ({planes.Length} plane{(planes.Length == 1 ? "" : "s")}){(o.Caps ? ", the cuts closed in their materials" : "")}.");
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

    /// <summary>The class an element is exported as: a Studio solid classed by <c>exportAs</c> is written as that class, as the IFC export writes it.</summary>
    public static string ExportClass(Entity e) =>
        e.Str("exportAs") is { Length: > 3 } x && x.StartsWith("Ifc", StringComparison.Ordinal) ? x : e.Class;

    /// <summary>
    /// Assemblies without a body of their own (a Studio box with its bottom and top, an assembly read from IFC): each becomes an
    /// element of the scene whose children are the parts it aggregates, in the group of its first part; the parts leave the group's own list.
    /// </summary>
    static void Assemblies(IModelView m, Dictionary<string, (MeshGroup Group, MeshElement Element)> elementOf)
    {
        foreach (var asm in m.OfClass("IfcElementAssembly"))
        {
            if (elementOf.ContainsKey(asm.Id)) continue;
            var parts = m.RelatedOf(asm.Id, IfcClass.RelAggregates).Select(p => elementOf.GetValueOrDefault(p.Id)).Where(x => x.Element != null).ToList();
            if (parts.Count == 0) continue;
            var group = parts[0].Group;
            foreach (var (g, el) in parts) g.Elements.Remove(el);
            var node = new MeshElement(asm.Id, asm.Name, "IfcElementAssembly", asm.Str("objectType"), [], [], parts.Select(p => p.Element).ToList());
            group.Elements.Add(node);
            elementOf[asm.Id] = (group, node);
        }
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
