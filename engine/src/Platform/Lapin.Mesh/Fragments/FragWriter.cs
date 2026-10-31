using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Mesh.Fragments;

/// <summary>
/// A scene as a That Open Fragments file (<c>.frag</c>, docs/fragments.md): a
/// zlib-compressed FlatBuffer after <see cref="FragSchema"/>. There is no
/// published writer outside the TypeScript library; the conventions here are
/// the ones read off real files (the ones <c>IfcImporter</c> makes) and are
/// kept where the schema left a choice:
/// <list type="bullet">
/// <item>Items with geometry come first, so <c>meshes_items</c> is the identity; an assembly (a Studio box with its bottom and top) is an
/// item without geometry after them, <c>IsDecomposedBy</c> its parts, which <c>Decompose</c> it and are contained in the storey too.</item>
/// <item>Geometry is stored per element near the origin: one global transform holds the element's centre in double precision,
/// its shells hold points relative to it in float32 (world coordinates would spend the mantissa on the distance to the origin:
/// at 400,000 m a float32 steps by ~30 mm).</item>
/// <item>One sample per material of an element; identical shells (the same points, triangles and faces about their centre) are stored once.</item>
/// <item>Geometry resources (transforms, representations, materials, samples) take ids above every item's.</item>
/// <item>Attributes are JSON triples <c>["Name","Wall 1","IFCLABEL"]</c>; the GlobalId goes to <c>guids</c> instead. Relations are JSON arrays <c>["ContainedInStructure",12]</c>.</item>
/// <item>The spatial tree alternates an entity node (its local id) and a category node (its children of one class), as the library's tree reader expects.</item>
/// <item>Y up, as three.js: the model's (x, y, z) is written (x, z, −y), a rotation, so the winding stays.</item>
/// </list>
/// </summary>
public static class FragWriter
{
    /// <summary>The file: zlib-compressed unless <paramref name="raw"/> (what <c>core.load(bytes, {raw})</c> expects either way).</summary>
    public static byte[] Write(MeshScene scene, bool raw = false, string? modelGuid = null)
    {
        var data = Build(scene, modelGuid);
        if (raw) return data;
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(data);
        return ms.ToArray();
    }

    static readonly JsonSerializerOptions JsonText = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    static string Json(params JsonNode?[] values) => new JsonArray(values).ToJsonString(JsonText);

    sealed record Item(uint LocalId, string Category, string? Guid, List<string> Attributes, List<string> Relations);

    sealed class Geometry
    {
        public readonly List<(double X, double Y, double Z)> Centres = [];
        public readonly List<(float[] Points, int[] Triangles, ushort[] FaceIds, float[] Min, float[] Max)> Shells = [];
        readonly Dictionary<string, int> _shellIndex = [];
        public readonly List<(byte R, byte G, byte B, byte A, bool TwoSided)> Materials = [];
        readonly Dictionary<(byte, byte, byte, byte, bool), int> _materialIndex = [];
        public readonly List<(int Item, int Material, int Representation)> Samples = [];

        int Material(MeshMaterial m)
        {
            static byte C(float v) => (byte)Math.Clamp((int)Math.Round(v * 255), 0, 255);
            var key = (C(m.R), C(m.G), C(m.B), C(m.Alpha), m.Alpha < 1);
            if (_materialIndex.TryGetValue(key, out var i)) return i;
            Materials.Add(key);
            return _materialIndex[key] = Materials.Count - 1;
        }

        /// <summary>The element's parts about its centre, each a shell (shared with any identical one) in its material.</summary>
        public void Add(MeshElement e, IReadOnlyList<MeshMaterial> materials)
        {
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            foreach (var p in e.Parts)
                for (var i = 0; i + 2 < p.Positions.Length; i += 3)
                {
                    minX = Math.Min(minX, p.Positions[i]); maxX = Math.Max(maxX, p.Positions[i]);
                    minY = Math.Min(minY, p.Positions[i + 1]); maxY = Math.Max(maxY, p.Positions[i + 1]);
                    minZ = Math.Min(minZ, p.Positions[i + 2]); maxZ = Math.Max(maxZ, p.Positions[i + 2]);
                }
            var (cx, cy, cz) = ((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
            var item = Centres.Count;
            Centres.Add((cx, cz, -cy)); // Y up
            foreach (var p in e.Parts)
            {
                if (p.Indices.Length < 3) continue;
                var (points, tris) = Welded(p, cx, cy, cz);
                var faces = FaceIds(points, tris);
                var key = Key(points, tris, faces);
                if (!_shellIndex.TryGetValue(key, out var shell))
                {
                    float[] lo = [float.MaxValue, float.MaxValue, float.MaxValue], hi = [float.MinValue, float.MinValue, float.MinValue];
                    for (var i = 0; i < points.Length; i++) { lo[i % 3] = Math.Min(lo[i % 3], points[i]); hi[i % 3] = Math.Max(hi[i % 3], points[i]); }
                    Shells.Add((points, tris, faces, lo, hi));
                    shell = _shellIndex[key] = Shells.Count - 1;
                }
                Samples.Add((item, Material(materials[p.Material]), shell));
            }
        }

        /// <summary>The part's vertices about the centre, turned Y up, the same point once (the mesh has a vertex per face corner).</summary>
        static (float[] Points, int[] Triangles) Welded(MeshPart p, double cx, double cy, double cz)
        {
            var index = new Dictionary<(float, float, float), int>();
            var points = new List<float>();
            var map = new int[p.Positions.Length / 3];
            for (var i = 0; i < map.Length; i++)
            {
                // To 0,01 mm: two copies of one body differ by float noise about their centres, and must hash to one shell.
                static float Q(double v) { var r = (float)Math.Round(v, 5); return r == 0 ? 0 : r; }
                var q = (Q(p.Positions[i * 3] - cx), Q(p.Positions[i * 3 + 2] - cz), Q(-(p.Positions[i * 3 + 1] - cy)));
                if (!index.TryGetValue(q, out var k)) { k = index[q] = index.Count; points.Add(q.Item1); points.Add(q.Item2); points.Add(q.Item3); }
                map[i] = k;
            }
            var tris = new int[p.Indices.Length / 3 * 3];
            for (var i = 0; i < tris.Length; i++) tris[i] = map[p.Indices[i]];
            return ([.. points], tris);
        }

        /// <summary>One id per triangle, shared by the triangles in one plane: Fragments' face, the thing a pick highlights whole.</summary>
        static ushort[] FaceIds(float[] pts, int[] tris)
        {
            var planes = new Dictionary<(double, double, double, double), int>();
            var o = new ushort[tris.Length / 3];
            for (var t = 0; t < o.Length; t++)
            {
                int a = tris[t * 3] * 3, b = tris[t * 3 + 1] * 3, c = tris[t * 3 + 2] * 3;
                double ux = pts[b] - pts[a], uy = pts[b + 1] - pts[a + 1], uz = pts[b + 2] - pts[a + 2];
                double vx = pts[c] - pts[a], vy = pts[c + 1] - pts[a + 1], vz = pts[c + 2] - pts[a + 2];
                double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len > 0) { nx /= len; ny /= len; nz /= len; }
                var d = nx * pts[a] + ny * pts[a + 1] + nz * pts[a + 2];
                static double R(double v) { var r = Math.Round(v, 4); return r == 0 ? 0 : r; }
                var key = (R(nx), R(ny), R(nz), R(d));
                if (!planes.TryGetValue(key, out var id)) id = planes[key] = planes.Count;
                o[t] = (ushort)(id % 65536);
            }
            return o;
        }

        static string Key(float[] points, int[] tris, ushort[] faces)
        {
            using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            h.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(points.AsSpan()));
            h.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(tris.AsSpan()));
            h.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(faces.AsSpan()));
            return Convert.ToHexString(h.GetHashAndReset());
        }
    }

    /// <summary>The uncompressed FlatBuffer.</summary>
    public static byte[] Build(MeshScene scene, string? modelGuid = null)
    {
        var geometry = new Geometry();
        var items = new List<Item>();
        uint next = 1;
        var elementIds = new Dictionary<MeshElement, uint>(ReferenceEqualityComparer.Instance);
        // Items with geometry first, in the order their meshes are added.
        foreach (var g in scene.Groups)
            foreach (var e in g.Elements.SelectMany(x => x.Flatten()))
            {
                if (e.IsAssembly) continue;
                geometry.Add(e, scene.Materials);
                var attrs = new List<string> { Json("Name", e.Name, "IFCLABEL") };
                if (e.Type != null) attrs.Add(Json("ObjectType", e.Type, "IFCLABEL"));
                var item = new Item(next++, e.IfcClass.ToUpperInvariant(), Guid22(e.GlobalId), attrs, []);
                elementIds[e] = item.LocalId;
                items.Add(item);
            }
        // Then the assemblies (a Studio box with its bottom and top): items without geometry, decomposed by their parts.
        foreach (var g in scene.Groups)
            foreach (var e in g.Elements.SelectMany(x => x.Flatten()).Where(x => x.IsAssembly))
            {
                var attrs = new List<string> { Json("Name", e.Name, "IFCLABEL") };
                if (e.Type != null) attrs.Add(Json("ObjectType", e.Type, "IFCLABEL"));
                var item = new Item(next++, e.IfcClass.ToUpperInvariant(), Guid22(e.GlobalId), attrs, []);
                elementIds[e] = item.LocalId;
                items.Add(item);
                item.Relations.Add(Json(["IsDecomposedBy", .. e.Children!.Select(c => (JsonNode?)elementIds[c])]));
                foreach (var c in e.Children!) items[(int)elementIds[c] - 1].Relations.Add(Json("Decomposes", item.LocalId));
            }
        // Then the spatial structure: the storeys (and the site, for what stands on none), the building, the site, the project.
        var groupItems = new List<(MeshGroup Group, Item Item)>();
        foreach (var g in scene.Groups)
        {
            var attrs = new List<string> { Json("Name", g.Name, "IFCLABEL") };
            if (g.IfcClass == "IfcBuildingStorey") attrs.Add(Json("Elevation", g.Elevation, "IFCLENGTHMEASURE"));
            var item = new Item(next++, g.IfcClass.ToUpperInvariant(), Guid22(g.GlobalId), attrs, []);
            groupItems.Add((g, item));
            items.Add(item);
        }
        var building = new Item(next++, "IFCBUILDING", null, [Json("Name", scene.Building, "IFCLABEL")], []);
        var siteGroup = groupItems.FirstOrDefault(x => x.Group.IfcClass == "IfcSite");
        var site = siteGroup.Item ?? new Item(next++, "IFCSITE", null, [Json("Name", "Site", "IFCLABEL")], []);
        var project = new Item(next++, "IFCPROJECT", null, [Json("Name", scene.Project, "IFCLABEL")], []);
        if (siteGroup.Item == null) items.Add(site);
        items.Add(building);
        items.Add(project);

        // Every element, an assembly and its parts alike, is contained in its storey (so a reader places the parts without following the decomposition).
        foreach (var (g, storey) in groupItems)
        {
            var all = g.Elements.SelectMany(el => el.Flatten()).ToList();
            var contained = all.Select(el => (JsonNode?)elementIds[el]).ToArray();
            storey.Relations.Add(Json(["ContainsElements", .. contained]));
            foreach (var el in all) items[(int)elementIds[el] - 1].Relations.Add(Json("ContainedInStructure", storey.LocalId));
            if (g.IfcClass == "IfcBuildingStorey") storey.Relations.Add(Json("Decomposes", building.LocalId));
        }
        var storeys = groupItems.Where(x => x.Group.IfcClass == "IfcBuildingStorey").Select(x => (JsonNode?)x.Item.LocalId).ToArray();
        if (storeys.Length > 0) building.Relations.Add(Json(["IsDecomposedBy", .. storeys]));
        building.Relations.Add(Json("Decomposes", site.LocalId));
        site.Relations.Add(Json("IsDecomposedBy", building.LocalId));
        site.Relations.Add(Json("Decomposes", project.LocalId));
        project.Relations.Add(Json("IsDecomposedBy", site.LocalId));

        // Synthetic ids for the geometry resources, above every item's.
        uint[] Allocate(int n) { var o = new uint[n]; for (var i = 0; i < n; i++) o[i] = next++; return o; }
        var globalTransformIds = Allocate(geometry.Centres.Count);
        var representationIds = Allocate(geometry.Shells.Count);
        var materialIds = Allocate(geometry.Materials.Count);
        var sampleIds = Allocate(geometry.Samples.Count);
        var localTransformIds = Allocate(1);
        var maxLocalId = next;

        var b = new FlatBuilder(1 << 20);

        // ------------------------------------------------------------ meshes
        var shellOffsets = geometry.Shells.Select(s => ShellOffset(b, s.Points, s.Triangles, s.FaceIds)).ToList();
        var shellsVec = b.CreateOffsetVector(shellOffsets);
        var circleExtrusionsVec = b.CreateOffsetVector([]);

        b.StartVector(FragSchema.SizeRepresentation, geometry.Shells.Count, 4);
        for (var i = geometry.Shells.Count - 1; i >= 0; i--)
        {
            var (_, _, _, lo, hi) = geometry.Shells[i];
            b.Prep(4, FragSchema.SizeRepresentation);
            b.Pad(3);
            b.PutSbyte(FragSchema.RepresentationShell);
            b.Prep(4, 24);
            b.Prep(4, 12); b.PutFloat(hi[2]); b.PutFloat(hi[1]); b.PutFloat(hi[0]);
            b.Prep(4, 12); b.PutFloat(lo[2]); b.PutFloat(lo[1]); b.PutFloat(lo[0]);
            b.PutInt(i);
        }
        var representationsVec = b.EndVector(geometry.Shells.Count);

        b.StartVector(FragSchema.SizeMaterial, geometry.Materials.Count, 1);
        for (var i = geometry.Materials.Count - 1; i >= 0; i--)
        {
            var (r, g, bl, a, two) = geometry.Materials[i];
            b.Prep(1, FragSchema.SizeMaterial);
            b.PutSbyte(FragSchema.StrokeDefault);
            b.PutSbyte(two ? FragSchema.RenderedFacesTwo : FragSchema.RenderedFacesOne);
            b.PutByte(a); b.PutByte(bl); b.PutByte(g); b.PutByte(r);
        }
        var materialsVec = b.EndVector(geometry.Materials.Count);

        b.StartVector(FragSchema.SizeSample, geometry.Samples.Count, 4);
        for (var i = geometry.Samples.Count - 1; i >= 0; i--)
        {
            var (item, material, representation) = geometry.Samples[i];
            b.Prep(4, FragSchema.SizeSample);
            b.PutInt(0); b.PutInt(representation); b.PutInt(material); b.PutInt(item);
        }
        var samplesVec = b.EndVector(geometry.Samples.Count);

        b.StartVector(FragSchema.SizeTransform, geometry.Centres.Count, 8);
        for (var i = geometry.Centres.Count - 1; i >= 0; i--) PutTransform(b, geometry.Centres[i]);
        var globalTransformsVec = b.EndVector(geometry.Centres.Count);

        b.StartVector(FragSchema.SizeTransform, 1, 8);
        PutTransform(b, (0, 0, 0));
        var localTransformsVec = b.EndVector(1);

        var meshesItemsVec = b.CreateUintVector(Enumerable.Range(0, geometry.Centres.Count).Select(i => (uint)i).ToArray());
        var materialIdsVec = b.CreateUintVector(materialIds);
        var representationIdsVec = b.CreateUintVector(representationIds);
        var sampleIdsVec = b.CreateUintVector(sampleIds);
        var localTransformIdsVec = b.CreateUintVector(localTransformIds);
        var globalTransformIdsVec = b.CreateUintVector(globalTransformIds);

        b.StartObject(FragSchema.Meshes.Fields);
        // `coordinates`: the model's own placement; the geometry is in the project's coordinates already, so the identity (the app inverts it on load).
        b.Prep(8, FragSchema.SizeTransform);
        PutTransform(b, (0, 0, 0));
        b.AddStruct(FragSchema.Meshes.Coordinates, b.Offset());
        b.AddOffset(FragSchema.Meshes.MeshesItems, meshesItemsVec);
        b.AddOffset(FragSchema.Meshes.Samples, samplesVec);
        b.AddOffset(FragSchema.Meshes.Representations, representationsVec);
        b.AddOffset(FragSchema.Meshes.Materials, materialsVec);
        b.AddOffset(FragSchema.Meshes.CircleExtrusions, circleExtrusionsVec);
        b.AddOffset(FragSchema.Meshes.Shells, shellsVec);
        b.AddOffset(FragSchema.Meshes.LocalTransforms, localTransformsVec);
        b.AddOffset(FragSchema.Meshes.GlobalTransforms, globalTransformsVec);
        b.AddOffset(FragSchema.Meshes.MaterialIds, materialIdsVec);
        b.AddOffset(FragSchema.Meshes.RepresentationIds, representationIdsVec);
        b.AddOffset(FragSchema.Meshes.SampleIds, sampleIdsVec);
        b.AddOffset(FragSchema.Meshes.LocalTransformIds, localTransformIdsVec);
        b.AddOffset(FragSchema.Meshes.GlobalTransformIds, globalTransformIdsVec);
        var meshesOff = b.EndObject();

        // ------------------------------------------------------------ the model
        var attributeOffsets = new List<int>();
        foreach (var item in items)
        {
            var data = b.CreateStringVector(item.Attributes);
            b.StartObject(FragSchema.Attribute.Fields);
            b.AddOffset(FragSchema.Attribute.Data, data);
            attributeOffsets.Add(b.EndObject());
        }
        var attributesVec = b.CreateOffsetVector(attributeOffsets);

        var relationOffsets = new List<int>();
        var relationItems = new List<int>();
        foreach (var item in items.Where(i => i.Relations.Count > 0))
        {
            var data = b.CreateStringVector(item.Relations);
            b.StartObject(FragSchema.Relation.Fields);
            b.AddOffset(FragSchema.Relation.Data, data);
            relationOffsets.Add(b.EndObject());
            relationItems.Add((int)item.LocalId);
        }
        var relationsVec = b.CreateOffsetVector(relationOffsets);
        var relationsItemsVec = b.CreateIntVector(relationItems);

        var metadata = new JsonObject
        {
            ["schema"] = "IFC4",
            ["names"] = new JsonArray(scene.Project, DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture), "", "", "Lapin", "Lapin", ""),
            ["descriptions"] = new JsonArray(),
            ["crs"] = scene.RootOffset is var (east, north, up) && (east != 0 || north != 0 || up != 0)
                ? new JsonObject { ["mapConversion"] = new JsonObject { ["eastings"] = east, ["northings"] = north, ["orthogonalHeight"] = up, ["xAxisAbscissa"] = 1.0, ["xAxisOrdinate"] = 0.0, ["scale"] = 1.0 } }
                : null,
        };
        var metadataOff = b.CreateString(metadata.ToJsonString(JsonText));
        var guidOff = b.CreateString(modelGuid ?? System.Guid.NewGuid().ToString());
        var withGuid = items.Where(i => i.Guid != null).ToList();
        var guidsVec = b.CreateStringVector(withGuid.Select(i => i.Guid!).ToList());
        var guidsItemsVec = b.CreateUintVector(withGuid.Select(i => i.LocalId).ToList());
        var localIdsVec = b.CreateUintVector(items.Select(i => i.LocalId).ToList());
        var categoriesVec = b.CreateStringVector(items.Select(i => i.Category).ToList());
        var uniqueAttributesVec = b.CreateStringVector([]);
        var relationNamesVec = b.CreateStringVector([]);
        var indexesVec = b.CreateOffsetVector([]);

        // The spatial tree: entity nodes and category nodes in turn; an assembly's parts under it, by class.
        int EntityNode(Item item, IEnumerable<(string Category, List<(Item Item, MeshElement? Element)> Children)> groups)
        {
            var categoryOffs = new List<int>();
            foreach (var (category, children) in groups)
            {
                var childOffs = children.Select(c => EntityNode(c.Item, c.Element is { IsAssembly: true } a ? ByClass(a.Children!) : [])).ToList();
                categoryOffs.Add(CategoryNode(category, childOffs));
            }
            return EntityNodeOf(item, categoryOffs);
        }
        int EntityNodeOf(Item item, List<int> childOffs)
        {
            var childrenVec = b.CreateOffsetVector(childOffs);
            b.StartObject(FragSchema.Spatial.Fields);
            b.AddOffset(FragSchema.Spatial.Children, childrenVec);
            b.AddUint(FragSchema.Spatial.LocalId, item.LocalId, force: true);
            return b.EndObject();
        }
        int CategoryNode(string category, List<int> childOffs)
        {
            var childrenVec = b.CreateOffsetVector(childOffs);
            var categoryOff = b.CreateString(category);
            b.StartObject(FragSchema.Spatial.Fields);
            b.AddOffset(FragSchema.Spatial.Category, categoryOff);
            b.AddOffset(FragSchema.Spatial.Children, childrenVec);
            return b.EndObject();
        }
        IEnumerable<(string, List<(Item Item, MeshElement? Element)>)> ByClass(IEnumerable<MeshElement> elements) =>
            elements.GroupBy(el => el.IfcClass.ToUpperInvariant()).Select(x => (x.Key, x.Select(el => (items[(int)elementIds[el] - 1], (MeshElement?)el)).ToList()));
        var storeyNodes = groupItems.Where(x => x.Group.IfcClass == "IfcBuildingStorey").Select(x => EntityNode(x.Item, ByClass(x.Group.Elements))).ToList();
        var buildingNode = EntityNodeOf(building, storeyNodes.Count > 0 ? [CategoryNode("IFCBUILDINGSTOREY", storeyNodes)] : []);
        var siteChildren = new List<int> { CategoryNode("IFCBUILDING", [buildingNode]) };
        if (siteGroup.Group != null) foreach (var (category, children) in ByClass(siteGroup.Group.Elements)) siteChildren.Add(CategoryNode(category, children.Select(c => EntityNode(c.Item, c.Element is { IsAssembly: true } a ? ByClass(a.Children!) : [])).ToList()));
        var siteNode = EntityNodeOf(site, siteChildren);
        var spatialOff = CategoryNode("IFCPROJECT", [EntityNodeOf(project, [CategoryNode("IFCSITE", [siteNode])])]);

        b.StartObject(FragSchema.Model.Fields);
        b.AddOffset(FragSchema.Model.Metadata, metadataOff);
        b.AddOffset(FragSchema.Model.Guids, guidsVec);
        b.AddOffset(FragSchema.Model.GuidsItems, guidsItemsVec);
        b.AddUint(FragSchema.Model.MaxLocalId, maxLocalId);
        b.AddOffset(FragSchema.Model.LocalIds, localIdsVec);
        b.AddOffset(FragSchema.Model.Categories, categoriesVec);
        b.AddOffset(FragSchema.Model.Meshes, meshesOff);
        b.AddOffset(FragSchema.Model.Attributes, attributesVec);
        b.AddOffset(FragSchema.Model.Relations, relationsVec);
        b.AddOffset(FragSchema.Model.RelationsItems, relationsItemsVec);
        b.AddOffset(FragSchema.Model.Guid, guidOff);
        b.AddOffset(FragSchema.Model.SpatialStructure, spatialOff);
        b.AddOffset(FragSchema.Model.UniqueAttributes, uniqueAttributesVec);
        b.AddOffset(FragSchema.Model.RelationNames, relationNamesVec);
        b.AddOffset(FragSchema.Model.Indexes, indexesVec);
        var modelOff = b.EndObject();
        // No file identifier: the reference files leave those four bytes zero.
        return b.Finish(modelOff);
    }

    /// <summary>An IFC GlobalId as the file carries it (22 characters); anything else is no guid.</summary>
    static string? Guid22(string id) => id.Length == 22 ? id : null;

    /// <summary>A Transform struct: the position in double, the x and y directions in float, written in reverse field order.</summary>
    static void PutTransform(FlatBuilder b, (double X, double Y, double Z) pos)
    {
        b.Prep(8, FragSchema.SizeTransform);
        b.Prep(4, 12); b.PutFloat(0); b.PutFloat(1); b.PutFloat(0); // y direction
        b.Prep(4, 12); b.PutFloat(0); b.PutFloat(0); b.PutFloat(1); // x direction
        b.Prep(8, 24); b.PutDouble(pos.Z); b.PutDouble(pos.Y); b.PutDouble(pos.X);
    }

    /// <summary>One Shell: its points, a profile per triangle, and the face each triangle belongs to; holes stay empty.</summary>
    static int ShellOffset(FlatBuilder b, float[] points, int[] tris, ushort[] faceIds)
    {
        var n = points.Length / 3;
        var big = n > FragSchema.MaxSmallPoints;
        var profileOffsets = new List<int>(tris.Length / 3);
        for (var t = 0; t < tris.Length; t += 3)
        {
            var data = big
                ? b.CreateUintVector([(uint)tris[t], (uint)tris[t + 1], (uint)tris[t + 2]])
                : b.CreateUshortVector([(ushort)tris[t], (ushort)tris[t + 1], (ushort)tris[t + 2]]);
            b.StartObject(FragSchema.ShellProfile.Fields);
            b.AddOffset(FragSchema.ShellProfile.Indices, data);
            profileOffsets.Add(b.EndObject());
        }
        var profilesVec = b.CreateOffsetVector(profileOffsets);
        var emptyVec = b.CreateOffsetVector([]);
        var faceIdsVec = b.CreateUshortVector(faceIds);
        b.StartVector(FragSchema.SizeFloatVector, n, 4);
        for (var i = n - 1; i >= 0; i--) { b.Prep(4, 12); b.PutFloat(points[i * 3 + 2]); b.PutFloat(points[i * 3 + 1]); b.PutFloat(points[i * 3]); }
        var pointsVec = b.EndVector(n);

        b.StartObject(FragSchema.Shell.Fields);
        b.AddOffset(big ? FragSchema.Shell.BigProfiles : FragSchema.Shell.Profiles, profilesVec);
        b.AddOffset(big ? FragSchema.Shell.Profiles : FragSchema.Shell.BigProfiles, emptyVec);
        b.AddOffset(FragSchema.Shell.Holes, emptyVec);
        b.AddOffset(FragSchema.Shell.BigHoles, emptyVec);
        b.AddOffset(FragSchema.Shell.Points, pointsVec);
        b.AddOffset(FragSchema.Shell.ProfilesFaceIds, faceIdsVec);
        b.AddSbyte(FragSchema.Shell.Type, big ? FragSchema.ShellTypeBig : FragSchema.ShellTypeNone);
        return b.EndObject();
    }
}
