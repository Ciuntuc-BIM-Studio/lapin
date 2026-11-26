using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Archicad;

/// <summary>An Archicad story: its index, name, level (metres), height and the IFC id Archicad exports it with.</summary>
public sealed record ArchicadStory(int Index, string Name, double Level, double Height, string? IfcId);

/// <summary>A body of an element's 3D model: its vertices (world metres) and its planar faces, each an outer loop, holes and a surface.</summary>
public sealed record ArchicadBody(IReadOnlyList<Vec3> Vertices, IReadOnlyList<ArchicadFace> Faces);

public sealed record ArchicadFace(int[] Outer, int[][] Holes, string? Surface);

/// <summary>A surface attribute: its name, its colour (#rrggbb) and how much light it lets through (0–1).</summary>
public sealed record ArchicadSurface(string Name, string Color, double Transparency);

/// <summary>A classification system and its items (by guid: code, title, parent's guid).</summary>
public sealed record ArchicadClassificationSystem(string Guid, string Name, string? Source, string? Version, IReadOnlyDictionary<string, (string Code, string Title, string? Parent)> Items);

/// <summary>
/// One element as Archicad gives it: its guid, type, story (floor index), layer and element ID; its details (Tapir's
/// <c>GetDetailsOfElements</c>), its 3D bodies, its IFC ids and type, the IFC property sets Archicad maps to it, its custom
/// properties by group, its classifications (system guid, item guid), and a hash of all of it (what a later receive compares).
/// </summary>
public sealed record ArchicadElement(string Guid, string Type, int Floor, int Layer, string IdText, JsonObject Details, IReadOnlyList<ArchicadBody> Bodies,
    string? IfcId, string? ExternalIfcId, string? IfcType, JsonObject IfcProperties, JsonObject Properties, IReadOnlyList<(string System, string Item)> Classifications, string Hash)
{
    /// <summary>The GlobalId it comes in with: the one it came into Archicad with from an IFC file, else Archicad's own IFC id.</summary>
    public string? GlobalId => ExternalIfcId is { Length: 22 } x ? x : IfcId is { Length: 22 } i ? i : null;

    public double? LowestZ => Bodies.SelectMany(b => b.Vertices).Select(v => (double?)v.Z).Min();
}

/// <summary>Where a read came from: the port, the project's name (null: untitled), Archicad's version and Tapir's.</summary>
public sealed record ArchicadSource(int Port, string? Project, bool Untitled, string? Archicad, string? Tapir)
{
    public string Label => Project ?? "Untitled Archicad project";
}

/// <summary>
/// A running Archicad's model as read (<see cref="ArchicadRead.Read"/>): its stories, its elements (subelements and 2D-only ones
/// left out, counted in <see cref="Skipped"/>), the attributes they name (composites, building materials, surfaces, profiles,
/// layers), the zones' boundaries, the classification systems, and the survey point's transform (project → survey, 12 numbers).
/// </summary>
public sealed record ArchicadModel(ArchicadSource Source, IReadOnlyList<ArchicadStory> Stories, IReadOnlyList<ArchicadElement> Elements,
    IReadOnlyDictionary<string, JsonObject> Composites, IReadOnlyDictionary<string, string> BuildingMaterials, IReadOnlyDictionary<string, ArchicadSurface> Surfaces,
    IReadOnlyDictionary<string, JsonObject> Profiles, IReadOnlyDictionary<int, string> Layers, IReadOnlyDictionary<string, JsonObject> ZoneBoundaries,
    IReadOnlyDictionary<string, ArchicadClassificationSystem> ClassificationSystems, double[]? ProjectToSurvey, IReadOnlyDictionary<string, int> Skipped,
    IReadOnlyList<string> Notes, double Milliseconds)
{
    public ArchicadStory? Story(int floor) => Stories.FirstOrDefault(s => s.Index == floor);

    /// <summary>Archicad's Project Info as Tapir's <c>GetProjectInfoFields</c> gives it (<c>[{projectInfoId, projectInfoName, projectInfoValue}]</c>); null when it was not read.</summary>
    public JsonArray? ProjectInfoFields { get; init; }
}

/// <summary>
/// Reading a running Archicad's model through Tapir (docs/plan-archicad.md §2–§3), read only: the elements and their details, the
/// 3D geometry, IFC ids, types and property sets, classifications, custom properties, and the attributes they name. Each command
/// is asked once over all the elements (in batches), so a recording of the answers replays the read (<see cref="ReplayTransport"/>).
/// </summary>
public static class ArchicadRead
{
    /// <summary>
    /// The parts of hierarchical elements, whose 3D Archicad gives with their owner too (a stair's treads, a column's segments): the
    /// owner comes in, they do not.
    /// </summary>
    public static bool Subelement(string type) =>
        type is "ColumnSegment" or "BeamSegment" or "Riser" or "Tread" or "StairStructure"
        || type.StartsWith("CurtainWall", StringComparison.Ordinal) && type != "CurtainWall"
        || type.StartsWith("Railing", StringComparison.Ordinal) && type != "Railing";

    public static ArchicadModel Read(ArchicadClient c, Action<string, int, int>? progress = null, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var me = c.Describe(ct);
        if (!me.Ok) throw new CommandException(me.Problem!);
        var source = new ArchicadSource(c.Port, me.Project, me.Untitled, me.Archicad, me.Tapir);
        var notes = new List<string>();
        var skipped = new Dictionary<string, int>();
        void Skip(string why) => skipped[why] = skipped.GetValueOrDefault(why) + 1;

        progress?.Invoke("Reading Archicad's stories", 0, 0);
        var stories = new List<ArchicadStory>();
        foreach (var s in (c.Tapir("GetStories", null, ct)?["stories"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var index = (int)(Json.Number(s["index"]) ?? 0);
            var name = (string?)s["name"] is { Length: > 0 } n ? n.Trim() : $"Story {index}";
            stories.Add(new(index, name, Json.Number(s["level"]) ?? 0, Json.Number(s["height"]) ?? 3, (string?)s["ifcId"] is { Length: 22 } id ? id : null));
        }

        progress?.Invoke("Reading Archicad's elements", 0, 0);
        var all = (c.Tapir("GetAllElements", null, ct)?["elements"] as JsonArray ?? []).OfType<JsonObject>().Select(e => (string?)e["elementId"]?["guid"]).OfType<string>().ToList();
        var details = c.Each("GetDetailsOfElements", all, "detailsOfElements", b => new JsonObject { ["elements"] = b }, c.Batch, ct);
        // The elements received: not a subelement (its owner brings its 3D), with details.
        var keep = new List<(string Guid, JsonObject Detail)>();
        for (var i = 0; i < all.Count; i++)
        {
            if (details[i] is not JsonObject d || (string?)d["type"] is not { } type) { Skip("no details (an error)"); continue; }
            if (Subelement(type)) { Skip("a part of another element (its owner brings it)"); continue; }
            keep.Add((all[i], d));
        }
        var guids = keep.Select(k => k.Guid).ToList();

        progress?.Invoke("Reading Archicad's 3D geometry", 0, guids.Count);
        var geometry = c.Each("Get3DGeometryOfElements", guids, "geometries3D", b => new JsonObject { ["elements"] = b }, c.GeometryBatch, ct, TimeSpan.FromMinutes(10));
        progress?.Invoke("Reading the IFC ids and properties", 0, 0);
        var ifcIds = c.Each("GetIFCIdsOfElements", guids, "elementIFCIds", b => new JsonObject { ["elements"] = b }, c.Batch, ct);
        var ifcTypes = c.Each("GetIFCTypeOfElements", guids, "elementIFCTypes", b => new JsonObject { ["elements"] = b }, c.Batch, ct);
        var ifcProps = c.Each("GetIFCPropertiesOfElements", guids, "elementIFCProperties", b => new JsonObject { ["elements"] = b }, c.Batch, ct);

        // Classifications: every system, its items' codes and titles.
        var systems = new Dictionary<string, ArchicadClassificationSystem>();
        var systemIds = new JsonArray();
        foreach (var s in (c.Tapir("GetAllClassificationSystems", null, ct)?["classificationSystems"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if ((string?)s["classificationSystemId"]?["guid"] is not { } sg) continue;
            systemIds.Add((JsonNode)new JsonObject { ["classificationSystemId"] = new JsonObject { ["guid"] = sg } });
            var items = new Dictionary<string, (string, string, string?)>();
            void Walk(JsonArray? list, string? parent)
            {
                foreach (var n in (list ?? []).OfType<JsonObject>())
                {
                    if (n["classificationItem"] is not JsonObject it || (string?)it["classificationItemId"]?["guid"] is not { } ig) continue;
                    items[ig] = ((string?)it["id"] ?? "", (string?)it["name"] ?? "", parent);
                    Walk(it["children"] as JsonArray, ig);
                }
            }
            if (c.Api("GetAllClassificationsInSystem", new JsonObject { ["classificationSystemId"] = new JsonObject { ["guid"] = sg } }, ct, quiet: true) is { } tree)
                Walk(tree["classificationItems"] as JsonArray, null);
            systems[sg] = new(sg, (string?)s["name"] ?? "Archicad classification", (string?)s["source"] is { Length: > 0 } src ? src : null, (string?)s["version"] is { Length: > 0 } v ? v : null, items);
        }
        var classes = systemIds.Count == 0 ? guids.Select(_ => (JsonNode?)null).ToList()
            : c.Each("GetClassificationsOfElements", guids, "elementClassifications", b => new JsonObject { ["elements"] = b, ["classificationSystemIds"] = systemIds.DeepClone() }, c.Batch, ct);

        // Custom properties (Archicad's property manager; its built-in ones are its own parameters, read as details): by group.
        progress?.Invoke("Reading the properties", 0, 0);
        var defs = (c.Tapir("GetAllProperties", new JsonObject { ["propertyTypes"] = new JsonArray("Custom"), ["includeAvailability"] = false }, ct)?["properties"] as JsonArray ?? [])
            .OfType<JsonObject>().Where(p => p["propertyId"] is JsonObject).ToList();
        var values = defs.Count == 0 ? guids.Select(_ => (JsonNode?)null).ToList()
            : c.Each("GetPropertyValuesOfElements", guids, "propertyValuesForElements", b => new JsonObject
            {
                ["elements"] = b, ["properties"] = new JsonArray([.. defs.Select(p => (JsonNode)new JsonObject { ["propertyId"] = p["propertyId"]!.DeepClone() })]),
                ["includeAvailability"] = true, ["useSIUnits"] = true,
            }, Math.Max(10, Math.Min(c.Batch, 20000 / Math.Max(1, defs.Count))), ct);

        // The attributes the details and bodies name.
        var compositeIds = new HashSet<string>(); var profileIds = new HashSet<string>(); var materialIds = new HashSet<string>(); var surfaceIds = new HashSet<string>();
        foreach (var (_, d) in keep) Collect(d, compositeIds, profileIds, materialIds);
        var composites = Attributes(c, "GetComposites", "composites", compositeIds, ct);
        foreach (var comp in composites.Values)
            foreach (var skin in (comp["skins"] as JsonArray ?? []).OfType<JsonObject>())
                if (Guid(skin["buildingMaterialId"]) is { } bm) materialIds.Add(bm);
        var profiles = Attributes(c, "GetProfiles", "profiles", profileIds, ct);
        var materials = Attributes(c, "GetBuildingMaterials", "buildingMaterials", materialIds, ct).ToDictionary(kv => kv.Key, kv => (string?)kv.Value["name"] ?? "Building material");
        var bodies = new List<IReadOnlyList<ArchicadBody>>();
        foreach (var g in geometry) bodies.Add(Bodies(g as JsonObject, surfaceIds));
        var surfaces = Attributes(c, "GetSurfaces", "surfaces", surfaceIds, ct).ToDictionary(kv => kv.Key, kv => Surface(kv.Value));
        var layers = new Dictionary<int, string>();
        foreach (var l in (c.Tapir("GetAttributesByType", new JsonObject { ["attributeType"] = "Layer" }, ct)?["attributes"] as JsonArray ?? []).OfType<JsonObject>())
            if (Json.Number(l["index"]) is { } li) layers[(int)li] = (string?)l["name"] ?? "";

        // The zones' boundaries (what each side of each is: a wall, a slab's edge, a virtual one).
        var zoneGuids = keep.Where(k => (string?)k.Detail["type"] == "Zone").Select(k => k.Guid).ToList();
        var zoneBoundaries = new Dictionary<string, JsonObject>();
        JsonObject? zb = null;
        if (zoneGuids.Count > 0)
            try { zb = c.Tapir("GetZoneBoundaries", new JsonObject { ["zones"] = ArchicadClient.Elements(zoneGuids) }, ct); }
            catch (CommandException ex) { notes.Add($"The zones' boundaries were not read ({ex.Message})."); }
        if (zb != null)
        {
            var list = zb["zoneBoundariesOfZones"] as JsonArray ?? zb["zones"] as JsonArray ?? [];
            for (var i = 0; i < list.Count && i < zoneGuids.Count; i++) if (list[i] is JsonObject z) zoneBoundaries[zoneGuids[i]] = z;
        }

        double[]? toSurvey = null;
        try
        {
            if (c.Tapir("GetSurveyPointTransformation", null, ct)?["projectToSurvey"] is JsonArray m && m.Count == 12) toSurvey = [.. m.Select(x => Json.Number(x) ?? 0)];
        }
        catch (CommandException) { }

        // Archicad's Project Info (A1b): the project's name, number, client, site, designer… mapped by ProjectInfoMapping at the receive.
        var (infoFields, infoProblem) = ProjectInfoFields(c, ct);
        if (infoProblem != null) notes.Add(infoProblem);

        var elements = new List<ArchicadElement>();
        for (var i = 0; i < keep.Count; i++)
        {
            var (guid, d) = keep[i];
            var type = (string)d["type"]!;
            var det = d["details"] as JsonObject ?? new JsonObject();
            var ids = ifcIds[i] as JsonObject;
            var props = Properties(values[i], defs);
            var ifc = IfcProperties(ifcProps[i]);
            var cls = new List<(string, string)>();
            foreach (var x in (classes[i]?["classificationIds"] as JsonArray ?? []).OfType<JsonObject>())
                if (Guid(x["classificationSystemId"]) is { } sys && Guid(x["classificationItemId"]) is { } item) cls.Add((sys, item));
            var hash = Hashes.Sha256Hex(string.Join("\n", type, d.ToJsonString(), geometry[i]?.ToJsonString() ?? "", ids?.ToJsonString() ?? "", ifc.ToJsonString(), props.ToJsonString(),
                string.Join(";", cls.Select(x => x.Item1 + ":" + x.Item2))))[..32];
            elements.Add(new ArchicadElement(guid, type, (int)(Json.Number(d["floorIndex"]) ?? 0), (int)(Json.Number(d["layerIndex"]) ?? 0), (string?)d["id"] ?? "", (JsonObject)det.DeepClone(),
                bodies[i], (string?)ids?["ifcId"], (string?)ids?["externalIFCId"], (string?)ifcTypes[i]?["ifcType"], ifc, props, cls, hash));
        }
        return new ArchicadModel(source, stories, elements, composites, materials, surfaces, profiles, layers, zoneBoundaries, systems, toSurvey, skipped, notes, sw.Elapsed.TotalMilliseconds)
        {
            ProjectInfoFields = infoFields,
        };
    }

    /// <summary>Archicad's Project Info (Tapir's <c>GetProjectInfoFields</c>, its list as it is), or why it was not read.</summary>
    public static (JsonArray? Fields, string? Problem) ProjectInfoFields(ArchicadClient c, CancellationToken ct = default)
    {
        try
        {
            var r = c.Tapir("GetProjectInfoFields", null, ct);
            return (r?["fields"] as JsonArray is { } list ? (JsonArray)list.DeepClone() : new JsonArray(), null);
        }
        catch (CommandException ex) { return (null, $"Archicad's Project Info was not read ({ex.Message})."); }
    }

    static string? Guid(JsonNode? n) => (string?)n?["guid"] ?? (string?)n?["attributeId"]?["guid"];

    /// <summary>The attributes a detail names (composites, profiles, building materials), wherever in it.</summary>
    static void Collect(JsonNode? n, HashSet<string> composites, HashSet<string> profiles, HashSet<string> materials)
    {
        switch (n)
        {
            case JsonObject o:
                foreach (var (k, v) in o)
                {
                    if (k == "compositeId" && Guid(v) is { } cg) composites.Add(cg);
                    else if (k == "profileId" && Guid(v) is { } pg) profiles.Add(pg);
                    else if (k == "buildingMaterialId" && Guid(v) is { } mg) materials.Add(mg);
                    else Collect(v, composites, profiles, materials);
                }
                break;
            case JsonArray a:
                foreach (var v in a) Collect(v, composites, profiles, materials);
                break;
        }
    }

    static Dictionary<string, JsonObject> Attributes(ArchicadClient c, string command, string key, HashSet<string> ids, CancellationToken ct)
    {
        var o = new Dictionary<string, JsonObject>();
        if (ids.Count == 0) return o;
        var asked = ids.Order(StringComparer.Ordinal).ToList();
        var r = c.Tapir(command, new JsonObject { ["attributeIds"] = new JsonArray([.. asked.Select(g => (JsonNode)new JsonObject { ["attributeId"] = new JsonObject { ["guid"] = g } })]) }, ct);
        foreach (var a in (r?[key] as JsonArray ?? []).OfType<JsonObject>())
            if (Guid(a["attributeId"]) is { } g) o[g] = a;
        return o;
    }

    static ArchicadSurface Surface(JsonObject s)
    {
        static int B(JsonNode? x) => (int)Math.Round(Math.Clamp(Json.Number(x) ?? 0.8, 0, 1) * 255);
        var c = s["surfaceColor"];
        var color = c == null ? "#c9ced6" : $"#{B(c["red"]):x2}{B(c["green"]):x2}{B(c["blue"]):x2}";
        return new((string?)s["name"] ?? "Surface", color, Math.Clamp((Json.Number(s["transparency"]) ?? 0) / 100.0, 0, 1));
    }

    /// <summary>An element's bodies as Tapir gives them, the surfaces they name added to <paramref name="surfaces"/>; none for an error or a 2D element.</summary>
    public static IReadOnlyList<ArchicadBody> Bodies(JsonObject? g, HashSet<string>? surfaces = null)
    {
        var o = new List<ArchicadBody>();
        foreach (var b in (g?["bodies"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var v = (b["vertices"] as JsonArray ?? []).OfType<JsonObject>().Select(p => new Vec3(Json.Number(p["x"]) ?? 0, Json.Number(p["y"]) ?? 0, Json.Number(p["z"]) ?? 0)).ToList();
            var faces = new List<ArchicadFace>();
            foreach (var p in (b["polygons"] as JsonArray ?? []).OfType<JsonObject>())
            {
                int[] Loop(JsonNode? ids) => [.. (ids as JsonArray ?? []).Select(x => (int)(Json.Number(x) ?? -1)).Where(x => x >= 0 && x < v.Count)];
                var outer = Loop(p["vertexIds"]);
                if (outer.Length < 3) continue;
                var holes = (p["holes"] as JsonArray ?? []).OfType<JsonObject>().Select(h => Loop(h["vertexIds"])).Where(h => h.Length >= 3).ToArray();
                var surface = Guid(p["surfaceId"]);
                if (surface != null) surfaces?.Add(surface);
                faces.Add(new(outer, holes, surface));
            }
            if (v.Count > 0 && faces.Count > 0) o.Add(new(v, faces));
        }
        return o;
    }

    /// <summary>The custom properties with a value, by group (set) and name; unavailable ones and Archicad's "&lt;Undefined&gt;" left out.</summary>
    static JsonObject Properties(JsonNode? values, List<JsonObject> defs)
    {
        var o = new JsonObject();
        var list = values?["propertyValues"] as JsonArray;
        if (list == null) return o;
        for (var i = 0; i < list.Count && i < defs.Count; i++)
        {
            if (list[i] is not JsonObject pv || pv["isAvailable"] is JsonValue av && av.TryGetValue<bool>(out var available) && !available) continue;
            if (pv["propertyValue"]?["value"] is not JsonNode value) continue;
            if (value is JsonValue jv && jv.TryGetValue<string>(out var t) && (t.Length == 0 || t == "<Undefined>")) continue;
            var group = (string?)defs[i]["propertyGroupName"] is { Length: > 0 } g ? g : "Archicad properties";
            var name = (string?)defs[i]["propertyName"] ?? "Property";
            if (o[group] is not JsonObject set) o[group] = set = new JsonObject();
            set[name] = value is JsonValue ? value.DeepClone() : value.ToJsonString();
        }
        return o;
    }

    /// <summary>The IFC property sets Archicad maps to an element, by their IFC names (standard sets included: they are the file's).</summary>
    static JsonObject IfcProperties(JsonNode? n)
    {
        var o = new JsonObject();
        foreach (var p in (n?["ifcProperties"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if ((string?)p["propertySetName"] is not { Length: > 0 } set || (string?)p["name"] is not { Length: > 0 } name) continue;
            if (o[set] is not JsonObject so) o[set] = so = new JsonObject();
            var value = p["value"];
            so[name] = value is JsonValue ? value.DeepClone() : value?.ToJsonString();
        }
        return o;
    }

    /// <summary>
    /// A body as triangles, each face triangulated in its plane with its holes (concave ones too: ear clipping with the holes bridged),
    /// grouped by surface: (surface guid or null, vertices, triangles).
    /// </summary>
    public static List<(string? Surface, List<Vec3> Vertices, List<(int A, int B, int C)> Triangles)> Triangulate(IEnumerable<ArchicadBody> bodies, Vec3 shift = default)
    {
        var bySurface = new Dictionary<string, (string? Surface, List<Vec3> V, List<(int, int, int)> T, Dictionary<(int Body, int Vertex), int> Map)>();
        var bodyIndex = 0;
        foreach (var b in bodies)
        {
            foreach (var f in b.Faces)
            {
                var key = f.Surface ?? "";
                if (!bySurface.TryGetValue(key, out var part)) bySurface[key] = part = (f.Surface, [], [], []);
                var loop = f.Outer.Select(i => b.Vertices[i]).ToList();
                var normal = VecMath.NewellNormal(loop);
                if (normal.Length < 1e-12) continue;
                List<(int A, int B, int C)> tris;
                try { tris = Triangulator.Face(b.Vertices, f.Outer, f.Holes.Length > 0 ? f.Holes : null, normal.Normalized()); }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IndexOutOfRangeException) { continue; }
                foreach (var (x, y, z) in tris)
                {
                    int Map(int vi)
                    {
                        if (!part.Map.TryGetValue((bodyIndex, vi), out var k)) { k = part.V.Count; part.V.Add(b.Vertices[vi] + shift); part.Map[(bodyIndex, vi)] = k; }
                        return k;
                    }
                    part.T.Add((Map(x), Map(y), Map(z)));
                }
            }
            bodyIndex++;
        }
        return [.. bySurface.Values.Where(p => p.T.Count > 0).Select(p => (p.Surface, p.V, p.T))];
    }

    /// <summary>Each body as one closed mesh (the gate's measure of Archicad's own shape): its faces triangulated, its vertices its own.</summary>
    public static List<MeshBuilder> Meshes(IEnumerable<ArchicadBody> bodies, Vec3 shift = default)
    {
        var o = new List<MeshBuilder>();
        foreach (var b in bodies)
        {
            var m = new MeshBuilder();
            foreach (var v in b.Vertices) m.V.Add(v + shift);
            foreach (var f in b.Faces)
            {
                var normal = VecMath.NewellNormal(f.Outer.Select(i => b.Vertices[i]).ToList());
                if (normal.Length < 1e-12) continue;
                try { m.T.AddRange(Triangulator.Face(b.Vertices, f.Outer, f.Holes.Length > 0 ? f.Holes : null, normal.Normalized())); }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IndexOutOfRangeException) { }
            }
            if (m.T.Count > 0) o.Add(m);
        }
        return o;
    }

    internal static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
}
