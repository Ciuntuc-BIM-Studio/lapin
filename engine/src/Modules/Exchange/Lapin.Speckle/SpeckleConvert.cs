using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Mesh;
using Lapin.Mesh.Fragments;
using Lapin.Model;
using Lapin.Platform;
using Speckle.Objects.Data;
using Speckle.Objects.Other;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;

namespace Lapin.Speckle;

/// <summary>
/// What a send takes (docs/plan-speckle.md, S1): the whole model, the model as a 3D view shows it (its id, and what only the
/// interface knows of it: the elements it does not draw, the planes it cuts by), or the elements selected; at the end of a
/// phase; from the project's base point (the default, as the 3D exports) or its internal origin.
/// </summary>
public sealed record SpeckleSendScope(string What = "model", string? ViewId = null, IReadOnlySet<string>? Hidden = null, IReadOnlyList<ClipPlane>? Planes = null,
    IReadOnlySet<string>? Ids = null, string? Phase = null, string Origin = "base")
{
    public static readonly string[] Whats = ["model", "view", "selection"];

    public static SpeckleSendScope From(JsonObject a)
    {
        var what = (string?)a["what"] ?? "model";
        if (!Whats.Contains(what)) throw new CommandException($"„{what}” is not what a send takes: model, view or selection.");
        var origin = (string?)a["origin"] ?? "base";
        if (origin is not ("base" or "internal")) throw new CommandException($"„{origin}” is not base or internal.");
        static HashSet<string>? Set(JsonNode? n) => (n as JsonArray)?.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>().ToHashSet();
        var ids = Set(a["ids"]);
        if (what == "selection" && ids is not { Count: > 0 }) throw new CommandException("Nothing is selected to send.");
        return new SpeckleSendScope(what, what == "view" ? ((string?)a["viewId"] is { Length: > 0 } v ? v : GraphicsSets.View3d) : null,
            what == "view" ? Set(a["hidden"]) : null, what == "view" ? MeshExportOptions.PlanesOf(a["planes"]) : null,
            what == "selection" ? ids : null, (string?)a["phase"] is { Length: > 0 } ph ? ph : null, origin);
    }

    /// <summary>The 3D scene this scope asks for (the same scene the GLB export writes, through VisibilityGraphics).</summary>
    public MeshExportOptions MeshOptions => new("glb", ViewId, Phase, Edges: false, Origin: Origin, Hidden: Hidden is { Count: > 0 } ? Hidden : null,
        Planes: Planes is { Count: > 0 } ? Planes : null);
}

/// <summary>What a conversion made: the root to send and its counts (elements, meshes, materials), and what it left out.</summary>
public sealed record SpeckleConversion(RootCollection Root, int Elements, int Meshes, int Materials, int Triangles, IReadOnlyList<string> Notes);

/// <summary>
/// The model as Speckle objects (docs/plan-speckle.md, S1), no network: Speckle's v3 object model as its own connectors
/// send it. A tree of <see cref="Collection"/>s, Project → Building → Storey → Category (the IFC class), the site's
/// elements and the opening voids beside the building; each element a <see cref="DataObject"/> whose
/// <c>applicationId</c> is its IFC GlobalId (so a new version updates the same objects), its <c>displayValue</c> the
/// meshes of the scene the GLB export builds (<see cref="MeshScene"/>: one per material, metres, Z up, from the
/// project's base point), its <c>properties</c> what Lapin knows of it: IFC class, name, type, storey, phase, its
/// materials and layers, its property sets and quantities (as the Fragments export gathers them, <see cref="FragContext"/>).
/// The materials go as Speckle's <see cref="RenderMaterialProxy"/>s (colour, opacity, roughness, metalness) naming the
/// meshes they colour; the storeys as <see cref="LevelProxy"/>s naming their elements.
/// Lossy: the materials' images (textures) and the elements' edges are not sent; a placed copy (a row of the same
/// chairs) goes as its own meshes, since the scene holds each element's triangles where they stand.
/// </summary>
public static class SpeckleConvert
{
    /// <summary>The application Speckle is told sent the version, and the source the objects name.</summary>
    public const string SourceApplication = "lapin";

    public static SpeckleConversion Convert(IQueryContext q, SpeckleSendScope scope)
    {
        SpeckleSdk.Ensure();
        var m = q.Model;
        var scene = MeshScene.Build(q, scope.MeshOptions);
        if (scope.Ids != null) scene = Only(scene, scope.Ids);
        if (scene.ElementCount == 0) throw new CommandException(scope.What switch
        {
            "selection" => "Nothing selected has a body to send.",
            "view" => "The view shows nothing to send.",
            _ => "There is nothing to send: the model has no element with a body.",
        });
        var context = FragContext.Of(q, scene);
        var project = Spatial.Project(m);
        var building = Spatial.Building(m);
        var phases = PhasesOf(project);

        // The materials, each a render material named as in the project; the meshes each colours, by their applicationId.
        var materials = scene.Materials.Select((mm, i) => new RenderMaterial
        {
            name = mm.Name,
            diffuse = Argb(mm.R, mm.G, mm.B),
            opacity = Math.Clamp(mm.Alpha, 0, 1),
            roughness = Math.Clamp(mm.Pbr.Roughness, 0, 1),
            metalness = Math.Clamp(mm.Pbr.Metalness, 0, 1),
            applicationId = MaterialId(mm.Name, i),
        }).ToList();
        var coloured = materials.Select(_ => new List<string>()).ToList();
        var levels = new List<LevelProxy>();
        int meshes = 0, elements = 0;

        DataObject Element(MeshElement el, MeshGroup group)
        {
            var e = m.Get(el.GlobalId);
            var display = new List<Base>();
            foreach (var part in el.Parts)
            {
                var id = $"{el.GlobalId}/{part.Material}";
                display.Add(MeshOf(part, id));
                coloured[part.Material].Add(id);
                meshes++;
            }
            if (el.Parts.Count > 0) elements++;
            var o = new DataObject
            {
                name = el.Name is { Length: > 0 } n ? n : el.IfcClass,
                applicationId = el.GlobalId,
                displayValue = display,
                properties = PropertiesOf(m, e, el, group, context, phases),
            };
            // An assembly (a Studio box with its bottom and top): its parts nested, as Speckle's connectors nest an element's own.
            if (el.Children is { Count: > 0 } children) o["@elements"] = children.Select(c => (Base)Element(c, group)).ToList();
            return o;
        }

        Collection GroupOf(MeshGroup g)
        {
            var c = new Collection { name = g.Name, applicationId = g.GlobalId };
            c["ifcClass"] = g.IfcClass;
            if (g.IfcClass == IfcClass.BuildingStorey) c["elevation"] = Math.Round(g.Elevation, 6);
            foreach (var cls in g.Elements.GroupBy(x => x.IfcClass).OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                // A category: the elements of one IFC class on this storey, named by the class.
                var category = new Collection { name = cls.Key, applicationId = $"{g.GlobalId}/{cls.Key}" };
                category["ifcClass"] = cls.Key;
                category.elements = cls.Select(x => (Base)Element(x, g)).ToList();
                c.elements.Add(category);
            }
            if (g.IfcClass == IfcClass.BuildingStorey)
                levels.Add(new LevelProxy
                {
                    applicationId = "level:" + g.GlobalId,
                    value = new DataObject
                    {
                        name = g.Name,
                        displayValue = [],
                        properties = new Dictionary<string, object?> { ["elevation"] = Math.Round(g.Elevation, 6), ["units"] = "m", ["GlobalId"] = g.GlobalId },
                    },
                    objects = g.Elements.SelectMany(x => x.Flatten()).Select(x => x.GlobalId).ToList(),
                });
            return c;
        }

        var root = new RootCollection
        {
            name = scene.Project,
            applicationId = project?.Id,
            properties = new Dictionary<string, object?>
            {
                ["source"] = "Lapin " + Session.AppVersion,
                ["units"] = "m",
                ["origin"] = scope.Origin == "internal" ? "internal origin" : "project base point",
                ["what"] = scope.What,
            },
        };
        var buildingCollection = new Collection { name = scene.Building, applicationId = building?.Id };
        buildingCollection["ifcClass"] = IfcClass.Building;
        foreach (var g in scene.Groups)
        {
            // The storeys stand in the building; the site's elements and the opening voids beside it, under the project.
            if (g.IfcClass == IfcClass.BuildingStorey) buildingCollection.elements.Add(GroupOf(g));
            else root.elements.Add(GroupOf(g));
        }
        root.elements.Insert(0, buildingCollection);
        if (scene.Notes.Count > 0) root.properties["notes"] = scene.Notes.ToList();

        var proxies = materials.Select((mat, i) => (mat, objects: coloured[i])).Where(x => x.objects.Count > 0)
            .Select(x => new RenderMaterialProxy { value = x.mat, objects = x.objects, applicationId = x.mat.applicationId }).ToList();
        root["renderMaterialProxies"] = proxies;
        if (levels.Count > 0) root["levelProxies"] = levels;
        return new SpeckleConversion(root, elements, meshes, proxies.Count, scene.TriangleCount, scene.Notes);
    }

    /// <summary>A material's applicationId: its name, which the project keeps unique; a colour without a material by its place.</summary>
    static string MaterialId(string name, int index) => $"material:{name}#{index}";

    /// <summary>One material's triangles of an element as a Speckle mesh: metres, faces as [3, a, b, c].</summary>
    static global::Speckle.Objects.Geometry.Mesh MeshOf(MeshPart part, string applicationId)
    {
        var vertices = new List<double>(part.Positions.Length);
        foreach (var v in part.Positions) vertices.Add(Math.Round(v, 6));
        var faces = new List<int>(part.Indices.Length / 3 * 4);
        for (var i = 0; i + 2 < part.Indices.Length; i += 3)
        {
            faces.Add(3);
            faces.Add(part.Indices[i]);
            faces.Add(part.Indices[i + 1]);
            faces.Add(part.Indices[i + 2]);
        }
        return new global::Speckle.Objects.Geometry.Mesh { vertices = vertices, faces = faces, units = global::Speckle.Sdk.Common.Units.Meters, applicationId = applicationId };
    }

    /// <summary>A colour as Speckle's diffuse takes it: an ARGB integer, opaque (the opacity is its own).</summary>
    static int Argb(float r, float g, float b)
    {
        static int C(float x) => (int)Math.Round(Math.Clamp(x, 0, 1) * 255);
        return unchecked((int)(0xFF000000u | (uint)(C(r) << 16) | (uint)(C(g) << 8) | (uint)C(b)));
    }

    /// <summary>The scene with only the elements of <paramref name="ids"/> (an assembly when it or one of its parts is chosen).</summary>
    static MeshScene Only(MeshScene scene, IReadOnlySet<string> ids)
    {
        static bool Keep(MeshElement e, IReadOnlySet<string> ids) => e.Flatten().Any(x => ids.Contains(x.GlobalId));
        var groups = scene.Groups.Select(g => g with { Elements = g.Elements.Where(e => Keep(e, ids)).ToList() }).Where(g => g.Elements.Count > 0).ToList();
        return scene with { Groups = groups };
    }

    /// <summary>The project's phases by id: their names.</summary>
    static Dictionary<string, string> PhasesOf(Entity? project) =>
        ((project?.Attrs["phases"] as JsonObject)?["phases"] as JsonArray)?.OfType<JsonObject>()
            .Where(p => (string?)p["id"] is { Length: > 0 })
            .GroupBy(p => (string)p["id"]!).ToDictionary(g => g.Key, g => (string?)g.First()["name"] ?? g.Key) ?? [];

    /// <summary>
    /// What an element's Speckle object says of it: its IFC class, GlobalId, name, type, storey and phases, its attributes,
    /// materials and layers, its property sets and its quantities (each with its unit), in plain values.
    /// </summary>
    internal static Dictionary<string, object?> PropertiesOf(IModelView m, Entity? e, MeshElement el, MeshGroup group, FragContext context, IReadOnlyDictionary<string, string> phases)
    {
        var o = new Dictionary<string, object?>
        {
            ["IFC class"] = el.IfcClass,
            ["GlobalId"] = el.GlobalId,
            ["Name"] = el.Name,
        };
        if (el.Type is { Length: > 0 } type) o["Type"] = type;
        o[group.IfcClass == IfcClass.BuildingStorey ? "Storey" : "Container"] = group.Name;
        if (e != null)
        {
            if (e.Str("phaseCreated") is { Length: > 0 } pc) o["Phase created"] = phases.GetValueOrDefault(pc, pc);
            if (e.Str("phaseDemolished") is { Length: > 0 } pd) o["Phase demolished"] = phases.GetValueOrDefault(pd, pd);
        }
        if (context.Element(el.GlobalId) is not { } data) return o;

        var attributes = new Dictionary<string, object?>();
        foreach (var a in data.Attributes.Where(a => a.Name != "Tag")) attributes[a.Name] = Plain(a.Value);
        if (attributes.Count > 0) o["Attributes"] = attributes;
        if (data.Materials.Count > 0) o["Materials"] = data.Materials.Cast<object?>().ToList();
        // A layered element's build-up, from its type's layer set: each layer's material, thickness and whether it is the core.
        if (e != null && m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } t && LayerSets.LayersOf(m, t.Id) is { Count: > 0 } layers)
            o["Layers"] = layers.Select(l => (object?)new Dictionary<string, object?>
            {
                ["Material"] = m.Get(l.Material)?.Name ?? l.Material,
                ["Thickness"] = Math.Round(l.Thickness, 6),
                ["Core"] = l.Core,
            }).ToList();

        var psets = new Dictionary<string, object?>();
        var qsets = new Dictionary<string, object?>();
        foreach (var set in data.Sets)
        {
            var name = set.Description is { Length: > 0 } part ? $"{set.Name} ({part})" : set.Name;
            var items = new Dictionary<string, object?>();
            foreach (var item in set.Items)
                items[item.Name] = set.Quantities
                    ? new Dictionary<string, object?> { ["value"] = Plain(item.Value.Value), ["units"] = UnitsOf(item.Category) }
                    : Plain(item.Value.Value);
            (set.Quantities ? qsets : psets)[name] = items;
        }
        if (psets.Count > 0) o["Property sets"] = psets;
        if (qsets.Count > 0) o["Quantities"] = qsets;
        return o;
    }

    static string? UnitsOf(string category) => category switch
    {
        "IFCQUANTITYLENGTH" => "m",
        "IFCQUANTITYAREA" => "m²",
        "IFCQUANTITYVOLUME" => "m³",
        "IFCQUANTITYWEIGHT" => "kg",
        _ => null,
    };

    /// <summary>A JSON value as a plain one the Speckle serializer writes: text, a whole number, a real, a boolean, or null.</summary>
    static object? Plain(JsonNode? n) => n switch
    {
        null => null,
        JsonValue v when v.GetValueKind() == JsonValueKind.String => (string?)v,
        JsonValue v when v.GetValueKind() is JsonValueKind.True or JsonValueKind.False => (bool)v,
        JsonValue v when v.TryGetValue<long>(out var l) => l,
        JsonValue v when v.TryGetValue<int>(out var i) => (long)i,
        JsonValue v when Json.Number(v) is { } d => d,
        _ => n.ToJsonString(),
    };
}
