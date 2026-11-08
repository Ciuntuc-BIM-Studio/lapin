using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Mesh.Fragments;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mesh.Bos;

/// <summary>
/// What a BIM Open Schema file means as a model (docs/bos.md): each entity's IFC class (from its category: an IFC name, or
/// a Revit category), its storey (ContainedIn a storey, else Revit's Level parameter), its instances (the hidden ones,
/// spaces, left out), and the storeys with their elevations. Shared by the survey and the import.
/// </summary>
public sealed class BosView
{
    public BosData Data { get; }
    public string?[] Category { get; }
    public string[] Class { get; }
    /// <summary>Each entity's visible instances, by entity.</summary>
    public Dictionary<int, List<int>> InstancesOf { get; } = [];
    public HashSet<int> Storeys { get; } = [];
    public Dictionary<int, int> StoreyOf { get; } = [];
    public int Hidden { get; private set; }

    public BosView(BosData d)
    {
        Data = d;
        var n = d.Entities.Count;
        Category = new string?[n];
        Class = new string[n];
        for (var i = 0; i < n; i++)
        {
            var c = d.Entities[i].Category;
            Category[i] = c >= 0 && c < n ? Blank(d.Text(d.Entities[c].Name)) : null;
            Class[i] = ClassOf(Category[i]);
            if (Class[i] == IfcClass.BuildingStorey) Storeys.Add(i);
        }
        for (var k = 0; k < d.Instances.Count; k++)
        {
            var inst = d.Instances[k];
            if (inst.Entity < 0 || inst.Entity >= n || inst.Mesh < 0 || inst.Mesh >= d.Meshes.Count) continue;
            if ((inst.Flags & 1) != 0) { Hidden++; continue; }
            (InstancesOf.TryGetValue(inst.Entity, out var l) ? l : InstancesOf[inst.Entity] = []).Add(k);
        }
        // An element's storey: the one it is ContainedIn; else through what holds it (a space it is in, an assembly it is part of).
        var up = new Dictionary<int, int>();
        foreach (var (a, b, t) in d.Relations)
        {
            if (a < 0 || a >= n || b < 0 || b >= n) continue;
            if (t == BosRelation.ContainedIn && Storeys.Contains(b)) StoreyOf.TryAdd(a, b);
            else if (t is BosRelation.ContainedIn or BosRelation.PartOf or BosRelation.MemberOf or BosRelation.HostedBy) up.TryAdd(a, b);
        }
        foreach (var e in up.Keys)
        {
            if (StoreyOf.ContainsKey(e)) continue;
            var at = e;
            for (var depth = 0; depth < 8 && up.TryGetValue(at, out var next); depth++)
            {
                if (Storeys.Contains(next)) { StoreyOf[e] = next; break; }
                if (StoreyOf.TryGetValue(next, out var s)) { StoreyOf[e] = s; break; }
                at = next;
            }
        }
        // Revit's own: an element's Level parameter names its level entity.
        for (var p = 0; p < d.Parameters.Count; p++)
        {
            var (e, de, v) = d.Parameters[p];
            if (de < 0 || de >= d.Descriptors.Count || d.Descriptors[de].Type != BosParameterType.Entity || StoreyOf.ContainsKey(e)) continue;
            if (d.Text(d.Descriptors[de].Name) is "Rvt:Element:Level" or "Rvt:Element:Reference Level" && v >= 0 && v < n)
            {
                Storeys.Add(v);
                StoreyOf[e] = v;
            }
        }
    }

    public string? Name(int e) => Blank(Data.Text(Data.Entities[e].Name));

    /// <summary>An entity's GlobalId when it is an IFC one (22 characters), else null.</summary>
    public string? Guid(int e) => Data.Text(Data.Entities[e].GlobalId) is { Length: 22 } g ? g : null;

    /// <summary>An entity's own id in its file (an IFC GlobalId or a Revit UniqueId), or null.</summary>
    public string? SourceId(int e) => Blank(Data.Text(Data.Entities[e].GlobalId));

    public string StoreyKey(int s) => Guid(s) ?? (SourceId(s) is { } u ? $"uid:{u}" : $"entity:{s}");

    static string? Blank(string? s) => string.IsNullOrEmpty(s) ? null : s;

    /// <summary>An entity's parameters of one name, as values: numbers, texts, entity indices.</summary>
    public IEnumerable<(string Name, string? Group, BosParameterType Type, int Value, string? Units)> Params(int e)
    {
        if (paramsOf == null)
        {
            paramsOf = [];
            for (var p = 0; p < Data.Parameters.Count; p++)
                (paramsOf.TryGetValue(Data.Parameters[p].Entity, out var l) ? l : paramsOf[Data.Parameters[p].Entity] = []).Add(p);
        }
        foreach (var p in paramsOf.GetValueOrDefault(e) ?? [])
        {
            var (_, de, v) = Data.Parameters[p];
            if (de < 0 || de >= Data.Descriptors.Count) continue;
            var (n, u, g, t) = Data.Descriptors[de];
            if (Data.Text(n) is not { Length: > 0 } name) continue;
            yield return (name, Data.Text(g), t, v, Data.Text(u));
        }
    }

    Dictionary<int, List<int>>? paramsOf;

    /// <summary>A storey's elevation (metres) from its own parameter, scaled by what its elements' lowest point says (an IFC file in millimetres, Revit in feet).</summary>
    public double? Elevation(int s, double? lowest)
    {
        foreach (var (name, _, type, v, units) in Params(s))
        {
            if (type != BosParameterType.Number || name is not ("Ifc:Elevation" or "Rvt:Level:Elevation" or "Elevation") || v < 0 || v >= Data.Numbers.Count) continue;
            double e = Data.Numbers[v];
            if (units is { } u && (u.Contains("ft", StringComparison.OrdinalIgnoreCase) || u.Contains("feet", StringComparison.OrdinalIgnoreCase))) return Math.Round(e * 0.3048, 4);
            if (lowest is { } lo && Math.Abs(e) > 1e-6)
            {
                var best = new[] { 1, 0.001, 0.3048 }.OrderBy(k => Math.Abs(e * k - lo)).First();
                return Math.Round(e * best, 4);
            }
            return Math.Round(Math.Abs(e) > 500 ? e / 1000 : e, 4);
        }
        return null;
    }

    /// <summary>An IFC class from a category: an IFC entity name as it is ("IFCWALLSTANDARDCASE" → IfcWallStandardCase), a Revit category by what it holds.</summary>
    public static string ClassOf(string? category)
    {
        if (category is not { Length: > 0 } c) return IfcClass.BuildingElementProxy;
        if (c.StartsWith("IFC", StringComparison.OrdinalIgnoreCase) && c.Length > 3 && !c.Contains(' '))
            return SpatialClasses.TryGetValue(c.ToUpperInvariant(), out var sp) ? sp : IfcModel.ClassName(c.ToUpperInvariant());
        return RevitCategories.TryGetValue(c.StartsWith("OST_", StringComparison.Ordinal) ? c[4..] : c, out var cls) ? cls : IfcClass.BuildingElementProxy;
    }

    /// <summary>The spatial structure's classes, which the products' list does not name.</summary>
    static readonly Dictionary<string, string> SpatialClasses = new()
    {
        ["IFCBUILDINGSTOREY"] = IfcClass.BuildingStorey, ["IFCBUILDING"] = "IfcBuilding", ["IFCSITE"] = "IfcSite", ["IFCPROJECT"] = "IfcProject",
        ["IFCSPACE"] = "IfcSpace", ["IFCMATERIAL"] = "IfcMaterial",
    };

    static readonly Dictionary<string, string> RevitCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Walls"] = "IfcWall", ["Floors"] = "IfcSlab", ["Roofs"] = "IfcRoof", ["Doors"] = "IfcDoor", ["Windows"] = "IfcWindow",
        ["Columns"] = "IfcColumn", ["Structural Columns"] = "IfcColumn", ["StructuralColumns"] = "IfcColumn", ["Structural Framing"] = "IfcBeam", ["StructuralFraming"] = "IfcBeam",
        ["Structural Foundations"] = "IfcFooting", ["StructuralFoundation"] = "IfcFooting", ["Stairs"] = "IfcStair", ["Railings"] = "IfcRailing", ["StairsRailing"] = "IfcRailing",
        ["Ramps"] = "IfcRamp", ["Ceilings"] = "IfcCovering", ["Furniture"] = "IfcFurniture", ["Furniture Systems"] = "IfcFurniture", ["Casework"] = "IfcFurniture",
        ["Rooms"] = "IfcSpace", ["Areas"] = "IfcSpace", ["Spaces"] = "IfcSpace", ["MEP Spaces"] = "IfcSpace", ["Curtain Panels"] = "IfcPlate", ["CurtainWallPanels"] = "IfcPlate",
        ["Curtain Wall Mullions"] = "IfcMember", ["CurtainWallMullions"] = "IfcMember", ["Generic Models"] = "IfcBuildingElementProxy", ["Plumbing Fixtures"] = "IfcSanitaryTerminal",
        ["Lighting Fixtures"] = "IfcLightFixture", ["Electrical Fixtures"] = "IfcOutlet", ["Electrical Equipment"] = "IfcElectricDistributionBoard", ["Mechanical Equipment"] = "IfcUnitaryEquipment",
        ["Ducts"] = "IfcDuctSegment", ["Duct Fittings"] = "IfcDuctFitting", ["Pipes"] = "IfcPipeSegment", ["Pipe Fittings"] = "IfcPipeFitting", ["Air Terminals"] = "IfcAirTerminal",
        ["Cable Trays"] = "IfcCableCarrierSegment", ["Conduits"] = "IfcCableCarrierSegment", ["Topography"] = "IfcGeographicElement", ["Toposolid"] = "IfcGeographicElement",
        ["Site"] = "IfcSite", ["Planting"] = "IfcGeographicElement", ["Entourage"] = "IfcBuildingElementProxy", ["Levels"] = "IfcBuildingStorey", ["Structural Rebar"] = "IfcReinforcingBar",
    };
}

/// <summary>A BIM Open Schema file before it is imported, in the IFC survey's shape: its elements by kind and its storeys, so File → Import BIM Open Schema… offers the IFC import's choice.</summary>
public static class BosSurvey
{
    public static string KindOf(BosView v, int e) => IfcSurvey.KindOf(v.Class[e], v.Name(e));

    public static JsonObject Of(BosView v, string name, long size)
    {
        var kinds = new Dictionary<string, int>();
        var per = new Dictionary<int, int>();
        var none = 0;
        foreach (var e in v.InstancesOf.Keys)
        {
            if (v.Storeys.Contains(e)) continue;
            var kind = KindOf(v, e);
            kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
            if (v.StoreyOf.TryGetValue(e, out var s)) per[s] = per.GetValueOrDefault(s) + 1; else none++;
        }
        var storeys = per.Keys.Select(s => (v.StoreyKey(s), v.Name(s) ?? $"Storey {s}", v.Elevation(s, null) ?? 0, per[s])).OrderBy(s => s.Item3);
        return IfcSurvey.Shape(name, size, "BIM Open Schema", v.Data.Generator, kinds, storeys, none);
    }
}

/// <summary>
/// A BIM Open Schema file (<c>.bos</c>, docs/bos.md) into the open project, the way a Fragments file comes in: each entity
/// with visible geometry a reference drawn from its stored body, in the IFC class its category names, with its
/// parameters as property sets (an IFC attribute's group, its class, read as Attributes); entities sharing a mesh and a
/// material instances of one shared body; storeys matched by name, made when missing; as one group; recognised into
/// native elements or made editable on request. Ids are the file's GlobalIds (a Revit UniqueId gives a stable derived
/// one), so importing the file again updates what it brought.
/// </summary>
public static class BosImport
{
    public static BosView Read(byte[] bytes, string name)
    {
        try { return new BosView(BosData.Read(bytes)); }
        catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentException or OverflowException or FormatException)
        {
            throw new CommandException($"„{name}” is not a BIM Open Schema file Lapin can read: {ex.Message}");
        }
    }

    public static FragImportResult Run(Session session, BosView view, string name, out Diff diff, FragImportOptions? options = null)
    {
        var o = options ?? new FragImportOptions();
        var reader = new Reader(session, view, name, o);
        EditableReport? editable = null;
        MeshRecognitionReport? recognition = null;
        void Apply(IEdit edit)
        {
            var before = edit.Model.Entities.Select(e => e.Id).ToHashSet();
            reader.Apply(edit);
            var brought = edit.Model.Entities.Where(e => !before.Contains(e.Id)).Select(e => e.Id).ToList();
            if (o.Recognize) recognition = MeshRecognition.Recognize(edit, session.Catalog, brought, o.Fidelity);
            if (o.Editable) editable = EditableReferences.Make(edit, session.Catalog, brought);
        }
        diff = session.Edit($"Import {name}", edit =>
        {
            if (o.Group) Groups.Around(edit, Path.GetFileNameWithoutExtension(name), Apply);
            else Apply(edit);
        });
        return reader.Result with { Editable = editable, Recognition = recognition };
    }

    sealed class Reader(Session session, BosView v, string name, FragImportOptions o)
    {
        readonly BosData d = v.Data;
        readonly List<string> notes = [];
        readonly Dictionary<string, int> byClass = [];
        readonly Dictionary<string, string> glassIds = [];
        int elements, shared, storeysMade, skipped, native, leftOut;
        public FragImportResult Result => new(elements, shared, storeysMade, skipped, notes, byClass, native, leftOut);

        public void Apply(IEdit edit)
        {
            // The storeys the elements stand on, matched to the project's by name, made when missing.
            var lowest = new Dictionary<int, double>();
            foreach (var (e, list) in v.InstancesOf)
                if (v.StoreyOf.TryGetValue(e, out var s))
                    foreach (var k in list) lowest[s] = Math.Min(lowest.GetValueOrDefault(s, double.MaxValue), MinZ(k));
            var building = Spatial.EnsureBuilding(edit);
            var storeyOf = new Dictionary<int, string>();
            foreach (var s in v.InstancesOf.Keys.Where(v.StoreyOf.ContainsKey).Select(e => v.StoreyOf[e]).Distinct())
            {
                if (o.Storeys != null && !o.Storeys.Contains(v.StoreyKey(s))) continue;
                var storeyName = v.Name(s) ?? $"Storey {s}";
                var existing = Spatial.Storeys(edit.Model).FirstOrDefault(x => string.Equals(x.Name, storeyName, StringComparison.OrdinalIgnoreCase));
                if (existing != null) { storeyOf[s] = existing.Id; continue; }
                var lo = lowest.TryGetValue(s, out var l) && l < double.MaxValue ? Math.Round(l, 2) : (double?)null;
                var z = v.Elevation(s, lo) ?? lo ?? 0;
                var storey = new Entity(v.Guid(s) is { } g && edit.Model.Get(g) == null ? g : edit.NewId(), IfcClass.BuildingStorey, storeyName, new JsonObject { ["elevation"] = z, ["height"] = 3.0 });
                edit.Add(storey);
                edit.Relate(IfcClass.RelAggregates, building.Id, storey.Id);
                storeyOf[s] = storey.Id;
                storeysMade++;
            }
            if (!Spatial.Storeys(edit.Model).Any()) storeysMade++;
            var fallback = Spatial.EnsureStorey(edit, session.Product).Id;

            // A mesh in one material used by more than one entity (each with that one instance) is a shared body.
            var uses = new Dictionary<(int Mesh, int Material), int>();
            foreach (var (_, list) in v.InstancesOf)
                if (list.Count == 1) { var i = d.Instances[list[0]]; uses[(i.Mesh, i.Material)] = uses.GetValueOrDefault((i.Mesh, i.Material)) + 1; }
            var sharedIds = new Dictionary<(int, int), string?>();

            var joins = new Dictionary<string, List<string>>();
            var total = v.InstancesOf.Count;
            var done = 0;
            foreach (var (e, list) in v.InstancesOf.OrderBy(kv => kv.Key))
            {
                if (++done % 500 == 0) session.Progress?.Invoke("Bringing the elements in", done, total);
                if (v.Storeys.Contains(e)) { skipped++; continue; }
                if (o.Kinds != null && !o.Kinds.Contains(BosSurvey.KindOf(v, e))) { leftOut++; continue; }
                var storeyKey = v.StoreyOf.TryGetValue(e, out var se) ? v.StoreyKey(se) : "";
                if (o.Storeys != null && !o.Storeys.Contains(storeyKey)) { leftOut++; continue; }
                var cls = v.Class[e];
                var id = v.Guid(e) ?? DerivedId(v.SourceId(e) is { } u ? $"uid\n{u}" : $"entity\n{e}");
                if (edit.Model.Get(id) is { } own && !StoredBodies.Has(own)) { native++; continue; }
                var json = new JsonObject();
                if (list.Count == 1 && uses.GetValueOrDefault((d.Instances[list[0]].Mesh, d.Instances[list[0]].Material)) > 1)
                {
                    var inst = d.Instances[list[0]];
                    var key = (inst.Mesh, inst.Material);
                    if (!sharedIds.TryGetValue(key, out var bodyId))
                    {
                        var parts = Parts([list[0]], world: false);
                        bodyId = null;
                        if (parts.Count > 0)
                        {
                            bodyId = DerivedId($"shared-body\n{inst.Mesh}\n{inst.Material}");
                            Put(edit, new Entity(bodyId, StoredBodies.SharedClass, $"{cls[3..]} (shared)", new JsonObject { [StoredBodies.Key] = Stored(edit, parts), ["source"] = $"bos:{inst.Mesh}:{inst.Material}" }));
                            shared++;
                        }
                        sharedIds[key] = bodyId;
                    }
                    if (bodyId == null) { skipped++; continue; }
                    json[StoredBodies.BodyOf] = bodyId;
                    json[StoredBodies.XfKey] = StoredBodies.WriteXf(Xf(inst.Transform));
                }
                else
                {
                    var parts = Parts(list, world: true);
                    if (parts.Count == 0) { skipped++; continue; }
                    json[StoredBodies.Key] = Stored(edit, parts);
                }
                var (properties, predefined) = Properties(e);
                if (properties.Count > 0) json["properties"] = properties;
                var ent = d.Entities[e];
                json["bos"] = new JsonObject { ["entity"] = e, ["localId"] = ent.LocalId, ["category"] = v.Category[e], ["sourceId"] = v.SourceId(e) };
                Put(edit, new Entity(id, cls, v.Name(e) ?? cls[3..], json, predefined));
                var storeyId = v.StoreyOf.TryGetValue(e, out var sl) && storeyOf.TryGetValue(sl, out var st) ? st : fallback;
                if (Spatial.StoreyOf(edit.Model, id)?.Id != storeyId) (joins.TryGetValue(storeyId, out var js) ? js : joins[storeyId] = []).Add(id);
                elements++;
                byClass[cls] = byClass.GetValueOrDefault(cls) + 1;
            }
            foreach (var (storeyId, ids) in joins) edit.RelateAll(IfcClass.RelContainedInSpatialStructure, storeyId, ids);
            if (native > 0) notes.Add($"{native} element(s) are already in the project as Lapin elements (the file came from it): they were left as they are.");
            if (v.Hidden > 0) notes.Add($"{v.Hidden} hidden instance(s) (spaces and the like, hidden in the file) were left out.");
        }

        double MinZ(int k)
        {
            var inst = d.Instances[k];
            var (v0, v1, _, _) = d.MeshRange(inst.Mesh);
            var t = Transform(inst.Transform);
            var min = double.MaxValue;
            for (var i = v0; i < v1; i += Math.Max(1, (v1 - v0) / 64)) min = Math.Min(min, t(Vertex(i)).Z);
            return min;
        }

        Vec3 Vertex(int i) => new(d.VertexX[i] / (double)BosData.VertexScale, d.VertexY[i] / (double)BosData.VertexScale, d.VertexZ[i] / (double)BosData.VertexScale);

        /// <summary>A transform as a function: scaled, rotated by its quaternion, moved (the schema's Scaling · Rotation · Translation, row vectors).</summary>
        Func<Vec3, Vec3> Transform(int t)
        {
            if (t < 0 || t >= d.Transforms.Count) return p => p;
            var x = d.Transforms[t];
            var q = Normalize(x.QX, x.QY, x.QZ, x.QW);
            return p =>
            {
                var s = new Vec3(p.X * x.SX, p.Y * x.SY, p.Z * x.SZ);
                return Rotate(q, s) + new Vec3(x.TX, x.TY, x.TZ);
            };
        }

        static (double X, double Y, double Z, double W) Normalize(double x, double y, double z, double w)
        {
            var n = Math.Sqrt(x * x + y * y + z * z + w * w);
            return n < 1e-12 ? (0, 0, 0, 1) : (x / n, y / n, z / n, w / n);
        }

        static Vec3 Rotate((double X, double Y, double Z, double W) q, Vec3 v)
        {
            var u = new Vec3(q.X, q.Y, q.Z);
            var t = 2 * Cross(u, v);
            return v + q.W * t + Cross(u, t);
        }

        static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        bool Mirrors(int t) => t >= 0 && t < d.Transforms.Count && d.Transforms[t].SX * d.Transforms[t].SY * d.Transforms[t].SZ < 0;

        /// <summary>An instance's twelve numbers: the images of the axes and the origin under its transform.</summary>
        double[] Xf(int t)
        {
            var f = Transform(t);
            var origin = f(new Vec3(0, 0, 0));
            var o = new double[12];
            Vec3[] axes = [new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)];
            for (var k = 0; k < 3; k++) { var a = f(axes[k]) - origin; o[k * 3] = a.X; o[k * 3 + 1] = a.Y; o[k * 3 + 2] = a.Z; }
            o[9] = origin.X; o[10] = origin.Y; o[11] = origin.Z;
            return o;
        }

        /// <summary>Instances as a stored body's parts: placed by their transforms (<paramref name="world"/>), or in the mesh's own frame.</summary>
        List<(string Name, string Color, IReadOnlyList<Vec3> Vertices, IReadOnlyList<(int A, int B, int C)> Triangles, (byte R, byte G, byte B, byte A) Rgba)> Parts(List<int> instances, bool world)
        {
            var o = new List<(string, string, IReadOnlyList<Vec3>, IReadOnlyList<(int, int, int)>, (byte, byte, byte, byte))>();
            foreach (var k in instances)
            {
                var inst = d.Instances[k];
                var (v0, v1, i0, i1) = d.MeshRange(inst.Mesh);
                if (v1 <= v0 || i1 - i0 < 3 || v1 > d.VertexX.Count || i1 > d.Indices.Count) continue;
                var f = world ? Transform(inst.Transform) : (p => p);
                var vertices = new List<Vec3>(v1 - v0);
                for (var i = v0; i < v1; i++) vertices.Add(f(Vertex(i)));
                var flip = world && Mirrors(inst.Transform);
                var tris = new List<(int, int, int)>((i1 - i0) / 3);
                var n = vertices.Count;
                for (var i = i0; i + 2 < i1; i += 3)
                {
                    int a = d.Indices[i], b = d.Indices[i + 1], c = d.Indices[i + 2];
                    if (a < 0 || b < 0 || c < 0 || a >= n || b >= n || c >= n || a == b || b == c || a == c) continue;
                    tris.Add(flip ? (a, c, b) : (a, b, c));
                }
                if (tris.Count == 0) continue;
                var m = inst.Material >= 0 && inst.Material < d.Materials.Count ? d.Materials[inst.Material] : ((byte)200, (byte)200, (byte)200, (byte)255, (byte)128, (byte)0);
                o.Add(("Body", $"#{m.Item1:x2}{m.Item2:x2}{m.Item3:x2}", vertices, tris, (m.Item1, m.Item2, m.Item3, m.Item4)));
            }
            return o;
        }

        /// <summary>The parts as a stored body; a see-through colour (glass) takes a material of that colour and transparency, one per colour.</summary>
        JsonObject Stored(IEdit edit, List<(string Name, string Color, IReadOnlyList<Vec3> Vertices, IReadOnlyList<(int A, int B, int C)> Triangles, (byte R, byte G, byte B, byte A) Rgba)> parts)
        {
            var body = StoredBodies.Write(parts.Select(p => (p.Name, p.Color, p.Vertices, p.Triangles)));
            if (body["parts"] is not JsonArray written) return body;
            for (var i = 0; i < parts.Count && i < written.Count; i++)
            {
                var c = parts[i].Rgba;
                if (c.A >= 255 || written[i] is not JsonObject part) continue;
                var colour = $"#{c.R:x2}{c.G:x2}{c.B:x2}";
                if (!glassIds.TryGetValue(colour + c.A, out var materialId))
                {
                    var material = Lapin.Platform.Materials.Ensure(edit, $"BOS {colour.TrimStart('#').ToUpperInvariant()} {Math.Round(100.0 * c.A / 255)}%", colour);
                    var a = material.CloneAttrs();
                    a["transparency"] = Math.Round(1 - c.A / 255.0, 3);
                    edit.Replace(material.With(attrs: a));
                    glassIds[colour + c.A] = materialId = material.Id;
                }
                part["material"] = materialId;
            }
            return body;
        }

        /// <summary>
        /// An entity's parameters as property sets: a set per group; an IFC attribute (<c>Ifc:Name</c> grouped by its class) under
        /// Attributes; a Revit parameter (<c>Rvt:Scope:Name</c>) by its last name. Its type's and materials' names with the attributes.
        /// </summary>
        (JsonObject Properties, string? Predefined) Properties(int e)
        {
            var properties = new JsonObject();
            var attributes = new JsonObject();
            string? predefined = null;
            foreach (var (pname, group, type, value, units) in v.Params(e))
            {
                var node = Value(type, value);
                if (node == null) continue;
                string set, key;
                if (pname.StartsWith("Ifc:", StringComparison.Ordinal) && (group == null || group.StartsWith("IFC", StringComparison.Ordinal) && !group.Contains(' ')))
                {
                    set = "Attributes";
                    key = pname[4..];
                }
                else if (pname.StartsWith("Rvt:", StringComparison.Ordinal) || pname.StartsWith("Bos:", StringComparison.Ordinal))
                {
                    set = group ?? (pname.StartsWith("Rvt:") ? "Revit" : "BIM Open Schema");
                    key = pname[(pname.LastIndexOf(':') + 1)..];
                }
                else { set = group ?? "Properties"; key = pname; }
                if (key == "PredefinedType" && node is JsonValue pv && pv.TryGetValue<string>(out var p)) predefined = p.Trim('.');
                var target = set == "Attributes" ? attributes : (properties[set] as JsonObject ?? (JsonObject)(properties[set] = new JsonObject()));
                target[key] = node;
            }
            if (d.Entities[e].Type is var t && t >= 0 && t < d.Entities.Count && v.Name(t) is { } typeName) attributes["Type"] = typeName;
            var materials = new List<string>();
            foreach (var (a, b, rt) in RelationsOf(e))
            {
                if (rt == BosRelation.HasMaterial && v.Name(b) is { } mn) materials.Add(mn);
                if (rt == BosRelation.HasLayer)
                    foreach (var (_, b2, rt2) in RelationsOf(b)) if (rt2 == BosRelation.HasMaterial && v.Name(b2) is { } ln) materials.Add(ln);
                _ = a;
            }
            if (materials.Count > 0) attributes["Material"] = string.Join(", ", materials.Distinct());
            if (attributes.Count > 0) properties["Attributes"] = attributes;
            return (properties, predefined);
        }

        Dictionary<int, List<int>>? relationsOf;
        IEnumerable<(int A, int B, BosRelation Type)> RelationsOf(int e)
        {
            if (relationsOf == null)
            {
                relationsOf = [];
                for (var r = 0; r < d.Relations.Count; r++) (relationsOf.TryGetValue(d.Relations[r].A, out var l) ? l : relationsOf[d.Relations[r].A] = []).Add(r);
            }
            foreach (var r in relationsOf.GetValueOrDefault(e) ?? []) yield return d.Relations[r];
        }

        JsonNode? Value(BosParameterType type, int value) => type switch
        {
            BosParameterType.Int => value,
            BosParameterType.Number => value >= 0 && value < d.Numbers.Count && float.IsFinite(d.Numbers[value]) ? Round(d.Numbers[value]) : null,
            BosParameterType.String => d.Text(value) is { } s ? s : null,
            BosParameterType.Entity => value >= 0 && value < d.Entities.Count ? v.Name(value) : null,
            BosParameterType.Point => value >= 0 && value < d.Points.Count ? $"{Round(d.Points[value].X)}, {Round(d.Points[value].Y)}, {Round(d.Points[value].Z)}" : null,
            _ => null,
        };

        /// <summary>A float as the file meant it: seven significant digits, not the double's tail.</summary>
        static double Round(float f) => double.Parse(f.ToString("G7", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);

        string DerivedId(string key)
        {
            var bytes = Md5.HashData(Encoding.UTF8.GetBytes(name + "\n" + key));
            return GlobalId.FromGuid(new Guid(bytes));
        }

        static void Put(IEdit edit, Entity e)
        {
            if (edit.Model.Get(e.Id) != null) edit.Replace(e); else edit.Add(e);
        }
    }
}
